// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using OpenDentBusiness;
using SyntheticData.Infrastructure;
using SyntheticData.Validation;
using Db=SyntheticData.Infrastructure.Db;

namespace AdoptionValidation {
	///<summary>Proves that every 24.4-25.3 column survives the program's own data layer on the adopted database. Per table, through the generated
	///CRUD class in OpenDentBusiness:
	///1. Load: every row is read with SelectMany; each 24.4-25.3 column must be mapped by a field, and every field value must equal the raw stored
	///   value (read separately over MySqlConnector), and enum values must be defined.
	///2. Save: every row is written back unchanged with Update; afterwards the row count, CHECKSUM TABLE and every per-column checksum of the table
	///   must be identical (so nothing is truncated, reformatted or reinterpreted, and no ON UPDATE timestamp moves).
	///3. Write: one row is cloned and inserted with a new key, read back and compared field by field; then every 24.4-25.3 column gets a new value
	///   (longer than the old width for widened columns, with a quote and a non-ASCII letter for text), is updated, read back, compared and checked
	///   in the raw table; the row is then deleted and the table checksums must be back to step 2.
	///It writes to the database, so it only runs on synth* scratch databases.</summary>
	public static class DataLayerRoundTrip {
		///<summary>Columns or tables without a data-layer mapping, with the reason. Their values are still preserved by step 2 (Update never writes
		///them) and by the report comparison.</summary>
		public static readonly Dictionary<string,string> DictDocumentedExceptions=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) {
			["chatattach.FileData"]="Binary column; the CRUD generator has no binary type and chat is excluded by owner decision (ReferenceSnapshotTests).",
			["chatattach.Thumbnail"]="Binary column; the CRUD generator has no binary type and chat is excluded by owner decision (ReferenceSnapshotTests).",
			["deletedobject"]="Deprecated table that 24.3 already had (DeletedObject is marked Obsolete and has no CRUD class); no code reads or writes it.",
		};
		///<summary>Columns that the round trip itself reads but never compares against the raw value: the CRUD layer or the server owns them.</summary>
		private static readonly CrudSpecialColType _crudSpecialNotWritten=CrudSpecialColType.TimeStamp|CrudSpecialColType.DateEntry
			|CrudSpecialColType.DateTEntry;

		public static RoundTripSummary Run(Db db,string database,Action<string> log) {
			LocalOnly.EnsureWritableDatabaseName(database);
			db.Use(database);
			SchemaInfo schema=SchemaInfo.Load(db,database);
			Dictionary<string,Type> dictTypes=GetTableTypes();
			RoundTripSummary summary=new RoundTripSummary();
			foreach(KeyValuePair<string,List<string>> pair in GetColumnsByTable(schema)) {
				TableRoundTrip tableRoundTrip=new TableRoundTrip { Table=pair.Key,Columns=pair.Value };
				Stopwatch stopwatch=Stopwatch.StartNew();
				try {
					RunTable(db,schema.GetTable(pair.Key),dictTypes,tableRoundTrip);
				}
				catch(Exception ex) {
					tableRoundTrip.ListWriteProblems.Add("Failed: "+(ex is TargetInvocationException ? ex.InnerException : ex).Message);
				}
				tableRoundTrip.Seconds=stopwatch.Elapsed.TotalSeconds;
				summary.Tables.Add(tableRoundTrip);
				log?.Invoke("  "+tableRoundTrip.Describe());
			}
			return summary;
		}

		///<summary>The 24.4-25.3 columns per table: every column of the new tables, and the new or changed columns of existing tables
		///(SyntheticData's Coverage lists, kept in step with docs/specs/schema-changes.json), except patient._portal_active_email, which adoption drops.
		///</summary>
		public static SortedDictionary<string,List<string>> GetColumnsByTable(SchemaInfo schema) {
			SortedDictionary<string,List<string>> dict=new SortedDictionary<string,List<string>>(StringComparer.Ordinal);
			foreach(string table in Coverage.NewTables) {
				TableInfo tableInfo=schema.GetTable(table);
				dict[table]=tableInfo==null ? new List<string>() : tableInfo.ListColumns.Where(x => !x.IsPrimaryKey).Select(x => x.Name).ToList();
			}
			foreach(string item in Coverage.NewOrChangedColumns) {
				string table=item.Split('.')[0];
				string column=item.Split('.')[1];
				if(item=="patient._portal_active_email") {
					continue;
				}
				if(!dict.TryGetValue(table,out List<string> list)) {
					list=new List<string>();
					dict[table]=list;
				}
				if(!list.Contains(column,StringComparer.OrdinalIgnoreCase)) {
					list.Add(column);
				}
			}
			return dict;
		}

		///<summary>Every table class in OpenDentBusiness, keyed by table name.</summary>
		public static Dictionary<string,Type> GetTableTypes() {
			Dictionary<string,Type> dict=new Dictionary<string,Type>(StringComparer.OrdinalIgnoreCase);
			//Table classes derive from TableBase; a few older ones (DeletedObject) only carry the CrudTable attribute. TableBase classes win a name clash.
			foreach(Type type in typeof(TableBase).Assembly.GetTypes().Where(x => x.IsClass && !x.IsAbstract && x.Namespace=="OpenDentBusiness"
				&& !x.Name.StartsWith("WebForms_")
				&& (x.IsSubclassOf(typeof(TableBase)) || x.GetCustomAttributes(typeof(CrudTableAttribute),false).Length>0))
				.OrderBy(x => x.IsSubclassOf(typeof(TableBase)) ? 0 : 1).ThenBy(x => x.Name))
			{
				string table=CrudTableAttribute.GetTableName(type).ToLowerInvariant();
				if(!dict.ContainsKey(table)) {
					dict[table]=type;
				}
			}
			return dict;
		}

		public static List<FieldInfo> GetDbFields(Type type) {
			List<FieldInfo> list=new List<FieldInfo>();
			foreach(FieldInfo field in type.GetFields(BindingFlags.Public|BindingFlags.Instance)) {
				CrudColumnAttribute crudColumn=GetCrudColumn(field);
				if(crudColumn!=null && crudColumn.IsNotDbColumn) {
					continue;
				}
				list.Add(field);
			}
			return list;
		}

		private static CrudColumnAttribute GetCrudColumn(FieldInfo field) {
			return field.GetCustomAttributes(typeof(CrudColumnAttribute),true).OfType<CrudColumnAttribute>().FirstOrDefault();
		}

		private static CrudSpecialColType GetSpecialType(FieldInfo field) {
			return GetCrudColumn(field)?.SpecialType??CrudSpecialColType.None;
		}

		private static void RunTable(Db db,TableInfo tableInfo,Dictionary<string,Type> dictTypes,TableRoundTrip result) {
			if(tableInfo==null) {
				result.ListWriteProblems.Add("Table missing from the database.");
				return;
			}
			result.Rows=db.ScalarLong("SELECT COUNT(*) FROM `"+tableInfo.Name+"`");
			if(!dictTypes.TryGetValue(tableInfo.Name,out Type type)) {
				AddUnmappedTable(result,"no table class in OpenDentBusiness");
				return;
			}
			result.TypeName=type.Name;
			Type typeCrud=typeof(TableBase).Assembly.GetType("OpenDentBusiness.Crud."+type.Name+"Crud");
			if(typeCrud==null) {
				AddUnmappedTable(result,"no generated CRUD class for "+type.Name);
				return;
			}
			List<FieldInfo> listFields=GetDbFields(type);
			Dictionary<string,FieldInfo> dictFields=listFields.ToDictionary(x => x.Name,StringComparer.OrdinalIgnoreCase);
			FieldInfo fieldKey=listFields.FirstOrDefault(x => GetCrudColumn(x)?.IsPriKey==true);
			ColumnInfo columnKey=tableInfo.PrimaryKey;
			if(fieldKey==null || columnKey==null) {
				result.ListWriteProblems.Add("No primary key field.");
				return;
			}
			List<string> listColumnsMapped=new List<string>();
			foreach(string column in result.Columns) {
				if(dictFields.ContainsKey(column)) {
					listColumnsMapped.Add(column);
					continue;
				}
				string key=tableInfo.Name+"."+column;
				if(DictDocumentedExceptions.ContainsKey(key)) {
					result.ListDocumentedExceptions.Add(key+": "+DictDocumentedExceptions[key]);
				}
				else {
					result.ListUnmapped.Add(column);
				}
			}
			MethodInfo methodSelectMany=typeCrud.GetMethod("SelectMany",new[] { typeof(string) });
			MethodInfo methodSelectOne=typeCrud.GetMethod("SelectOne",new[] { typeof(long) });
			MethodInfo methodUpdate=typeCrud.GetMethod("Update",new[] { type });
			MethodInfo methodInsert=typeCrud.GetMethod("Insert",new[] { type,typeof(bool) });
			//1. Load.
			string orderBy=" ORDER BY `"+columnKey.Name+"`";
			System.Collections.IList listObjects=(System.Collections.IList)methodSelectMany.Invoke(null,new object[] { "SELECT * FROM `"+tableInfo.Name+"`"+orderBy });
			Dictionary<long,object[]> dictRaw=new Dictionary<long,object[]>();
			if(listColumnsMapped.Count>0) {
				foreach(object[] row in ReadRawInServerTimeZone(db,"SELECT `"+columnKey.Name+"`,"+string.Join(",",listColumnsMapped.Select(x => "`"+x+"`"))
					+" FROM `"+tableInfo.Name+"`"+orderBy))
				{
					dictRaw[Convert.ToInt64(row[0])]=row.Skip(1).ToArray();
				}
			}
			if(listObjects.Count!=result.Rows) {
				result.ListLoadProblems.Add("SelectMany returned "+listObjects.Count+" of "+result.Rows+" rows.");
			}
			foreach(object obj in listObjects) {
				long key=Convert.ToInt64(fieldKey.GetValue(obj));
				if(!dictRaw.TryGetValue(key,out object[] arrayRaw)) {
					continue;
				}
				for(int i=0;i<listColumnsMapped.Count;i++) {
					FieldInfo field=dictFields[listColumnsMapped[i]];
					ColumnInfo column=tableInfo.GetColumn(listColumnsMapped[i]);
					object value=field.GetValue(obj);
					if(!ValuesMatch(arrayRaw[i],value,column)) {
						result.LoadMismatches++;
						if(result.ListLoadProblems.Count<10) {
							result.ListLoadProblems.Add(column.Name+" of "+columnKey.Name+" "+key+": stored ["+Show(arrayRaw[i])+"], loaded ["+Show(value)+"]");
						}
					}
					if(field.FieldType.IsEnum && !field.FieldType.IsDefined(typeof(FlagsAttribute),false) && !Enum.IsDefined(field.FieldType,value)) {
						string undefined=column.Name+"="+Convert.ToInt64(value).ToString(CultureInfo.InvariantCulture)+" (no "+field.FieldType.Name+" member)";
						if(!result.ListUndefinedEnumValues.Contains(undefined)) {
							result.ListUndefinedEnumValues.Add(undefined);
						}
					}
				}
			}
			result.ColumnsLoaded=listColumnsMapped.Count;
			//2. Save every row unchanged, then compare the table cell by cell. A changed cell is either one of the CRUD layer's known first-save
			//normalizations (same meaning in the program), or the ON UPDATE timestamp of a row that had one, or a failure.
			TableReport tableReportBefore=Validator.ChecksumTable(db,tableInfo);
			Dictionary<long,object[]> dictBefore=ReadAllRaw(db,tableInfo);
			foreach(object obj in listObjects) {
				methodUpdate.Invoke(null,new[] { obj });
				result.RowsSaved++;
			}
			TableReport tableReportAfterSave=Validator.ChecksumTable(db,tableInfo);
			Dictionary<long,object[]> dictAfter=ReadAllRaw(db,tableInfo);
			ClassifySaveChanges(db,tableInfo,dictFields,dictBefore,dictAfter,result);
			//Cross-check with the report checksums: every column whose checksum moved must be one the cell comparison explained.
			foreach(string difference in CompareTableReports(tableReportBefore,tableReportAfterSave)) {
				result.ListChecksumChangesOnSave.Add(difference);
				bool isExplained=difference=="CHECKSUM TABLE" ? result.ListNormalizedColumns.Count>0
					: difference.StartsWith("column ") && result.ListNormalizedColumns.Contains(difference.Substring("column ".Length));
				if(!isExplained && result.ListChangedOnSave.Count==0) {
					result.ListChangedOnSave.Add("checksum changed without a changed cell: "+difference);
				}
			}
			foreach(string column in result.ListNormalizedColumns) {
				if(!result.ListChecksumChangesOnSave.Contains("column "+column)) {
					result.ListChangedOnSave.Add("cells of "+column+" changed but its report checksum did not");
				}
			}
			//3. Write new values through Insert and Update, on a cloned row.
			if(methodInsert==null || methodSelectOne==null || listObjects.Count==0) {
				result.ListWriteProblems.Add(listObjects.Count==0 ? "No rows to clone." : "No Insert(T,bool) or SelectOne(long) on "+typeCrud.Name+".");
				return;
			}
			object source=listObjects.Cast<object>().OrderByDescending(x => listColumnsMapped.Count(y => !IsDefault(dictFields[y].GetValue(x))))
				.ThenBy(x => Convert.ToInt64(fieldKey.GetValue(x))).First();
			object clone=typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(source,null);
			long keyNew=db.ScalarLong("SELECT IFNULL(MAX(`"+columnKey.Name+"`),0) FROM `"+tableInfo.Name+"`")+1;
			fieldKey.SetValue(clone,keyNew);
			//Columns in a unique key (chatmsg.SeqCount, for example) get a value no other row has.
			foreach(object[] row in db.Rows("SELECT DISTINCT COLUMN_NAME FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='"
				+tableInfo.Name+"' AND NON_UNIQUE=0 AND INDEX_NAME<>'PRIMARY'"))
			{
				string columnUnique=Convert.ToString(row[0]);
				if(!dictFields.TryGetValue(columnUnique,out FieldInfo fieldUnique)) {
					continue;
				}
				if(fieldUnique.FieldType==typeof(long) || fieldUnique.FieldType==typeof(int)) {
					long max=db.ScalarLong("SELECT IFNULL(MAX(`"+columnUnique+"`),0) FROM `"+tableInfo.Name+"`")+1000;
					fieldUnique.SetValue(clone,fieldUnique.FieldType==typeof(long) ? (object)max : (object)(int)max);
				}
				else if(fieldUnique.FieldType==typeof(string)) {
					string unique="rt"+keyNew.ToString(CultureInfo.InvariantCulture)+"-"+(string)fieldUnique.GetValue(clone);
					int maxLength=(int)Math.Min(tableInfo.GetColumn(columnUnique)?.MaxLength??40,40);
					fieldUnique.SetValue(clone,unique.Length>maxLength ? unique.Substring(0,maxLength) : unique);
				}
			}
			try {
				methodInsert.Invoke(null,new[] { clone,(object)true });
				object loaded=methodSelectOne.Invoke(null,new object[] { keyNew });
				CompareObjects("insert",clone,loaded,listFields,fieldKey,false,result.ListWriteProblems);
				foreach(string column in listColumnsMapped) {
					FieldInfo field=dictFields[column];
					CrudSpecialColType specialType=GetSpecialType(field);
					if((specialType&(_crudSpecialNotWritten|CrudSpecialColType.ExcludeFromUpdate))!=0) {
						result.ListNotUpdatable.Add(column);
						continue;
					}
					object valueNew=NewValue(field,field.GetValue(clone),tableInfo.GetColumn(column),tableInfo.Name+"."+column);
					if(valueNew==null) {
						result.ListNotUpdatable.Add(column+" (no new value for type "+field.FieldType.Name+")");
						continue;
					}
					field.SetValue(clone,valueNew);
				}
				methodUpdate.Invoke(null,new[] { clone });
				loaded=methodSelectOne.Invoke(null,new object[] { keyNew });
				CompareObjects("update",clone,loaded,listFields,fieldKey,true,result.ListWriteProblems);
				List<string> listWritable=listColumnsMapped.Where(x => !result.ListNotUpdatable.Any(y => y==x || y.StartsWith(x+" "))).ToList();
				if(listWritable.Count>0) {
					object[] arrayRaw=ReadRawInServerTimeZone(db,"SELECT "+string.Join(",",listWritable.Select(x => "`"+x+"`"))+" FROM `"+tableInfo.Name+"` WHERE `"
						+columnKey.Name+"`="+keyNew.ToString(CultureInfo.InvariantCulture))[0];
					for(int i=0;i<listWritable.Count;i++) {
						FieldInfo field=dictFields[listWritable[i]];
						if(!ValuesMatch(arrayRaw[i],field.GetValue(clone),tableInfo.GetColumn(listWritable[i]))) {
							result.ListWriteProblems.Add("stored "+listWritable[i]+" ["+Show(arrayRaw[i])+"] differs from the saved ["+Show(field.GetValue(clone))+"]");
						}
					}
				}
				result.ColumnsWritten=listWritable.Count;
			}
			finally {
				db.Execute("DELETE FROM `"+tableInfo.Name+"` WHERE `"+columnKey.Name+"`="+keyNew.ToString(CultureInfo.InvariantCulture));
			}
			TableReport tableReportAfterWrite=Validator.ChecksumTable(db,tableInfo);
			foreach(string difference in CompareTableReports(tableReportAfterSave,tableReportAfterWrite)) {
				result.ListWriteProblems.Add("after the test row was deleted: "+difference);
			}
		}

		///<summary>Every stored (non-generated) column of every row, keyed by primary key, in the UTC session the reports use.</summary>
		private static Dictionary<long,object[]> ReadAllRaw(Db db,TableInfo tableInfo) {
			List<ColumnInfo> listColumns=tableInfo.ListColumns.Where(x => !x.IsGenerated).ToList();
			Dictionary<long,object[]> dict=new Dictionary<long,object[]>();
			foreach(object[] row in db.Rows("SELECT `"+tableInfo.PrimaryKey.Name+"`,"+string.Join(",",listColumns.Select(x => "`"+x.Name+"`"))+" FROM `"
				+tableInfo.Name+"`"))
			{
				dict[Convert.ToInt64(row[0])]=row.Skip(1).ToArray();
			}
			return dict;
		}

		///<summary>Sorts the cells that a save changed into the CRUD layer's known first-save normalizations (the program reads the old and the new
		///value the same way), the ON UPDATE timestamp of a row that had one, and real changes (failures).</summary>
		private static void ClassifySaveChanges(Db db,TableInfo tableInfo,Dictionary<string,FieldInfo> dictFields,Dictionary<long,object[]> dictBefore,
			Dictionary<long,object[]> dictAfter,TableRoundTrip result)
		{
			List<ColumnInfo> listColumns=tableInfo.ListColumns.Where(x => !x.IsGenerated).ToList();
			HashSet<string> setOnUpdate=new HashSet<string>(db.Rows("SELECT COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() "
				+"AND TABLE_NAME='"+tableInfo.Name+"' AND EXTRA LIKE '%on update%'").Select(x => Convert.ToString(x[0])),StringComparer.OrdinalIgnoreCase);
			int countFailures=0;
			void Fail(string text) {
				countFailures++;
				if(countFailures<=10) {
					result.ListChangedOnSave.Add(text);
				}
			}
			foreach(KeyValuePair<long,object[]> pair in dictBefore) {
				if(!dictAfter.TryGetValue(pair.Key,out object[] arrayAfter)) {
					Fail("row "+pair.Key+" is gone");
					continue;
				}
				bool hasNormalized=false;
				List<int> listTimestamps=new List<int>();
				for(int i=0;i<listColumns.Count;i++) {
					if(RawEquals(pair.Value[i],arrayAfter[i])) {
						continue;
					}
					if(setOnUpdate.Contains(listColumns[i].Name)) {
						listTimestamps.Add(i);
						continue;
					}
					dictFields.TryGetValue(listColumns[i].Name,out FieldInfo field);
					string kind=GetNormalizationKind(field,pair.Value[i],arrayAfter[i]);
					if(kind==null) {
						Fail(listColumns[i].Name+" of "+tableInfo.PrimaryKey.Name+" "+pair.Key+": ["+Show(pair.Value[i])+"] -> ["+Show(arrayAfter[i])+"]");
						continue;
					}
					hasNormalized=true;
					AddNormalized(result,listColumns[i].Name,kind);
				}
				foreach(int i in listTimestamps) {
					if(hasNormalized) {
						AddNormalized(result,listColumns[i].Name,"ON UPDATE timestamp of a row the save normalized");
					}
					else {
						Fail(listColumns[i].Name+" of "+tableInfo.PrimaryKey.Name+" "+pair.Key+" moved although nothing else changed");
					}
				}
			}
			foreach(long key in dictAfter.Keys.Where(x => !dictBefore.ContainsKey(x))) {
				Fail("row "+key+" appeared");
			}
			if(countFailures>10) {
				result.ListChangedOnSave.Add("... "+(countFailures-10)+" more changed cells");
			}
		}

		private static void AddNormalized(TableRoundTrip result,string column,string kind) {
			string key=column+": "+kind;
			result.DictNormalized.TryGetValue(key,out long count);
			result.DictNormalized[key]=count+1;
			if(!result.ListNormalizedColumns.Contains(column)) {
				result.ListNormalizedColumns.Add(column);
			}
		}

		///<summary>The CRUD layer's first-save normalizations: a NULL text column is written as '' (PIn.String reads NULL as ""), and an enum stored as
		///text (CrudSpecialColType.EnumAsString) that is blank or unknown is written as the name of the value it was read as. Null for any other
		///change.</summary>
		public static string GetNormalizationKind(FieldInfo field,object before,object after) {
			if(field==null) {
				return null;
			}
			if(field.FieldType==typeof(string) && before==null && after is string textAfter && textAfter=="") {
				return "NULL saved as ''";
			}
#pragma warning disable CS0618//EnumAsString is marked obsolete for new columns; existing Open Dental columns use it.
			bool isEnumAsString=(GetSpecialType(field)&CrudSpecialColType.EnumAsString)!=0;
#pragma warning restore CS0618
			if(isEnumAsString && field.FieldType.IsEnum && after is string nameAfter && Enum.IsDefined(field.FieldType,nameAfter)) {
				string textBefore=before as string??"";
				if(textBefore.Trim()=="" || !Enum.GetNames(field.FieldType).Contains(textBefore)) {
					return "blank or unknown enum text saved as '"+nameAfter+"'";
				}
			}
			return null;
		}

		private static bool RawEquals(object a,object b) {
			if(a is byte[] bytesA && b is byte[] bytesB) {
				return bytesA.SequenceEqual(bytesB);
			}
			return Equals(a,b);
		}

		///<summary>Reads rows with the session time zone of the business layer's own connection (the server's), so timestamp columns read as
		///OpenDentBusiness reads them. The SyntheticData connection otherwise runs in UTC, which the checksums need, so it is set back afterwards.
		///</summary>
		private static List<object[]> ReadRawInServerTimeZone(Db db,string command) {
			db.Execute("SET SESSION time_zone=@@GLOBAL.time_zone");
			try {
				return db.Rows(command);
			}
			finally {
				db.Execute("SET SESSION time_zone='+00:00'");
			}
		}

		private static void AddUnmappedTable(TableRoundTrip result,string why) {
			if(DictDocumentedExceptions.TryGetValue(result.Table,out string reason)) {
				result.ListDocumentedExceptions.Add(result.Table+": "+reason);
				return;
			}
			result.ListWriteProblems.Add(why+".");
			result.ListUnmapped.AddRange(result.Columns);
		}

		///<summary>Differences between two checksum reports of the same table, as "rows a -> b", "CHECKSUM TABLE" or "column X".</summary>
		public static List<string> CompareTableReports(TableReport before,TableReport after) {
			List<string> list=new List<string>();
			if(before.Rows!=after.Rows) {
				list.Add("rows "+before.Rows+" -> "+after.Rows);
			}
			foreach(KeyValuePair<string,string> pair in before.Columns) {
				if(!after.Columns.TryGetValue(pair.Key,out string value) || value!=pair.Value) {
					list.Add("column "+pair.Key);
				}
			}
			if(before.ChecksumTable!=after.ChecksumTable) {
				list.Add("CHECKSUM TABLE");
			}
			return list;
		}

		///<summary>True when the field value means the same as the raw stored value, judged by the database column type.</summary>
		public static bool ValuesMatch(object raw,object value,ColumnInfo column) {
			if(value is string text) {
				return (raw==null ? "" : Convert.ToString(raw,CultureInfo.InvariantCulture))==text;
			}
			if(value is DateTime dateTime) {
				DateTime dateTimeRaw=raw is DateTime d ? d : DateTime.MinValue;
				if(column!=null && column.IsDateOnly) {
					return dateTimeRaw.Date==dateTime.Date;
				}
				return Math.Abs((dateTimeRaw-dateTime).TotalSeconds)<1;
			}
			if(value is bool flag) {
				return ToLong(raw)==(flag ? 1 : 0);
			}
			if(value is double || value is float || value is decimal) {
				return Math.Abs(Convert.ToDouble(raw??0,CultureInfo.InvariantCulture)-Convert.ToDouble(value,CultureInfo.InvariantCulture))<0.000001;
			}
			if(value is TimeSpan timeSpan) {
				if(raw is TimeSpan timeSpanRaw) {
					return timeSpanRaw==timeSpan;
				}
				return ToLong(raw)==timeSpan.Ticks;
			}
			if(value is Color color) {
				return ToLong(raw)==color.ToArgb();
			}
			if(value is Enum || value is long || value is int || value is short || value is byte || value is sbyte) {
				return ToLong(raw)==Convert.ToInt64(value,CultureInfo.InvariantCulture);
			}
			return Equals(raw,value);
		}

		private static long ToLong(object raw) {
			if(raw==null) {
				return 0;
			}
			if(raw is bool flag) {
				return flag ? 1 : 0;
			}
			return Convert.ToInt64(raw,CultureInfo.InvariantCulture);
		}

		private static bool IsDefault(object value) {
			switch(value) {
				case null: return true;
				case string text: return text=="";
				case DateTime dateTime: return dateTime.Year<=1;
				case bool flag: return !flag;
				case Color color: return color.ToArgb()==0;
				case TimeSpan timeSpan: return timeSpan==TimeSpan.Zero;
				default:
					try {
						return Convert.ToDouble(value,CultureInfo.InvariantCulture)==0;
					}
					catch(Exception) {
						return false;
					}
			}
		}

		///<summary>A different value of the field's type that the column can hold. Text gets a quote and a non-ASCII letter; widened columns get a value
		///longer than their old width.</summary>
		public static object NewValue(FieldInfo field,object value,ColumnInfo column,string item) {
			Type type=field.FieldType;
			if(type==typeof(string)) {
				int max=(int)Math.Min(column?.MaxLength??255,400);
				int length=Math.Min(max,30);
				if(Coverage.DictWidenedOldLength.TryGetValue(item,out int oldLength)) {
					length=Math.Min(max,oldLength+(Coverage.DictWideningNotInReference.ContainsKey(item) ? 0 : 20));
				}
				string seed="Rt'é-"+field.Name+"-";
				string text="";
				while(text.Length<length) {
					text+=seed;
				}
				text=text.Substring(0,length);
				return text==(string)value ? text.Substring(0,length-1)+"x" : text;
			}
			if(type.IsEnum) {
				Array values=Enum.GetValues(type);
				foreach(object candidate in values) {
					if(!candidate.Equals(value) && Convert.ToInt64(candidate,CultureInfo.InvariantCulture)>=0
						&& Convert.ToInt64(candidate,CultureInfo.InvariantCulture)<=127)
					{
						return candidate;
					}
				}
				return null;
			}
			if(type==typeof(long)) {
				return (long)value+7;
			}
			if(type==typeof(int)) {
				return ((int)value+3)%100;
			}
			if(type==typeof(byte)) {
				return (byte)(((byte)value+1)%100);
			}
			if(type==typeof(bool)) {
				return !(bool)value;
			}
			if(type==typeof(double)) {
				return (double)value+1.25;
			}
			if(type==typeof(float)) {
				return (float)value+1.25f;
			}
			if(type==typeof(DateTime)) {
				DateTime dateTime=(DateTime)value;
				if(column!=null && column.IsDateOnly) {
					return dateTime.Year<=1 ? new DateTime(2026,1,15) : dateTime.Date.AddDays(1);
				}
				return dateTime.Year<=1 ? new DateTime(2026,1,15,10,30,0) : dateTime.AddMinutes(61);
			}
			if(type==typeof(TimeSpan)) {
				return ((TimeSpan)value).Add(TimeSpan.FromMinutes(1));
			}
			return null;
		}

		private static void CompareObjects(string label,object expected,object actual,List<FieldInfo> listFields,FieldInfo fieldKey,bool isUpdate,
			List<string> listProblems)
		{
			if(actual==null) {
				listProblems.Add(label+": SelectOne found no row.");
				return;
			}
			foreach(FieldInfo field in listFields) {
				if(field==fieldKey) {
					continue;
				}
				CrudSpecialColType specialType=GetSpecialType(field);
				if((specialType&(_crudSpecialNotWritten|CrudSpecialColType.DateEntryEditable|CrudSpecialColType.DateTEntryEditable))!=0) {
					continue;//Set by the server or by Insert (NOW()), not from the object.
				}
				if(isUpdate && (specialType&CrudSpecialColType.ExcludeFromUpdate)!=0) {
					continue;
				}
				object valueExpected=field.GetValue(expected);
				object valueActual=field.GetValue(actual);
				bool isMatch;
				if(valueExpected is double || valueExpected is float) {
					isMatch=Math.Abs(Convert.ToDouble(valueExpected)-Convert.ToDouble(valueActual))<0.000001;
				}
				else if(valueExpected is Color colorExpected) {
					isMatch=colorExpected.ToArgb()==((Color)valueActual).ToArgb();
				}
				else if(valueExpected is DateTime dateTimeExpected) {
					isMatch=Math.Abs((dateTimeExpected-(DateTime)valueActual).TotalSeconds)<1;
				}
				else {
					isMatch=Equals(valueExpected??"",valueActual??"");
				}
				if(!isMatch) {
					listProblems.Add(label+": "+field.Name+" saved ["+Show(valueExpected)+"] but read back ["+Show(valueActual)+"]");
				}
			}
		}

		private static string Show(object value) {
			string text=value is DateTime dateTime ? dateTime.ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture)
				: Convert.ToString(value,CultureInfo.InvariantCulture)??"null";
			return text.Length>60 ? text.Substring(0,57)+"... ("+text.Length+" chars)" : text;
		}
	}

	public class TableRoundTrip {
		public string Table;
		public string TypeName;
		public long Rows;
		///<summary>The 24.4-25.3 columns of this table that are checked.</summary>
		public List<string> Columns=new List<string>();
		public int ColumnsLoaded;
		public int ColumnsWritten;
		public List<string> ListUnmapped=new List<string>();
		public List<string> ListDocumentedExceptions=new List<string>();
		public long LoadMismatches;
		public List<string> ListLoadProblems=new List<string>();
		public List<string> ListUndefinedEnumValues=new List<string>();
		public long RowsSaved;
		public List<string> ListChangedOnSave=new List<string>();
		///<summary>First-save normalizations: "column: kind" and the number of cells.</summary>
		public SortedDictionary<string,long> DictNormalized=new SortedDictionary<string,long>(StringComparer.Ordinal);
		public List<string> ListNormalizedColumns=new List<string>();
		///<summary>What the table's report checksums say the save changed (row count, CHECKSUM TABLE, columns).</summary>
		public List<string> ListChecksumChangesOnSave=new List<string>();
		public List<string> ListNotUpdatable=new List<string>();
		public List<string> ListWriteProblems=new List<string>();
		public double Seconds;

		public bool Passed {
			get { return ListUnmapped.Count==0 && LoadMismatches==0 && ListLoadProblems.Count==0 && ListChangedOnSave.Count==0 && ListWriteProblems.Count==0; }
		}

		public string Describe() {
			string text=Table+(TypeName==null ? "" : " ("+TypeName+")")+": "+Rows+" rows, "+Columns.Count+" columns; loaded "+ColumnsLoaded+", saved "+RowsSaved
				+" rows, wrote "+ColumnsWritten+" new values"+(Passed ? ": ok" : ": FAILED");
			foreach(string problem in ListUnmapped.Select(x => "not mapped: "+x).Concat(ListLoadProblems).Concat(ListChangedOnSave.Select(x => "changed by save: "+x))
				.Concat(ListWriteProblems).Concat(DictNormalized.Select(x => "first-save normalization: "+x.Key+" ("+x.Value+" cells)")))
			{
				text+="\r\n    "+problem;
			}
			return text;
		}
	}

	public class RoundTripSummary {
		public List<TableRoundTrip> Tables=new List<TableRoundTrip>();

		public bool Passed {
			get { return Tables.All(x => x.Passed); }
		}

		public int ColumnCount {
			get { return Tables.Sum(x => x.Columns.Count); }
		}

		public long RowsSaved {
			get { return Tables.Sum(x => x.RowsSaved); }
		}

		///<summary>Every first-save normalization as "table.column: kind (n cells)".</summary>
		public List<string> ListNormalizations {
			get { return Tables.SelectMany(x => x.DictNormalized.Select(y => x.Table+"."+y.Key+" ("+y.Value+" cells)")).ToList(); }
		}

		///<summary>The report differences the round trip's first-save normalizations explain: the normalized columns and the ON UPDATE timestamps of
		///the normalized rows.</summary>
		public AllowedDifferences GetAllowedNormalizations() {
			AllowedDifferences allowed=new AllowedDifferences { Description="First-save normalizations found by the data-layer round trip." };
			foreach(TableRoundTrip table in Tables) {
				foreach(string column in table.ListNormalizedColumns) {
					allowed.Rules.Add(new AllowedRule { Kind="ColumnChanged",Table=table.Table,Column=column,
						Reason="first-save normalization: "+string.Join(", ",table.DictNormalized.Where(x => x.Key.StartsWith(column+": ")).Select(x => x.Key)) });
				}
			}
			return allowed;
		}
	}
}
