// CRUD round trips for every table class this package added or changed, on the migrated 24.3.63 database (see Fresh243MigrationTests).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;

namespace ForkMigrationTests {
	public partial class Fresh243MigrationTests {
		///<summary>For each changed table class: fill every column field with a distinct value, Insert through the generated CRUD, read the row back
		///with SelectOne and compare every field; then change every field, Update, read back and compare again. This proves that the generated CRUD
		///and the migrated columns agree, including the new and retyped columns.</summary>
		[TestMethod]
		public void Crud_RoundTripEveryChangedTable() {
			SchemaSnapshot reference=SchemaVerifier.GetReference25_3_48();
			Dictionary<string,Type> dictTypes=ReferenceSnapshotTests.GetTableTypes()
				.ToDictionary(x => CrudTableAttribute.GetTableName(x).ToLowerInvariant(),x => x);
			List<string> listProblems=new List<string>();
			foreach(string tableName in ReferenceSnapshotTests.ChangedTables) {
				Type type=dictTypes[tableName];
				try {
					RoundTrip(type,reference.GetTable(tableName),listProblems);
				}
				catch(Exception ex) {
					listProblems.Add(type.Name+": "+ex.GetBaseException().Message);
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
		}

		private static void RoundTrip(Type type,SchemaTable table,List<string> listProblems) {
			Type typeCrud=typeof(TableBase).Assembly.GetType("OpenDentBusiness.Crud."+type.Name+"Crud",true);
			MethodInfo methodInsert=typeCrud.GetMethod("Insert",new[] { type });
			MethodInfo methodSelectOne=typeCrud.GetMethod("SelectOne",new[] { typeof(long) });
			MethodInfo methodUpdate=typeCrud.GetMethod("Update",new[] { type });
			List<FieldInfo> listFields=ReferenceSnapshotTests.GetDbFields(type).ToList();
			FieldInfo fieldPriKey=listFields.First(x => GetCrudColumn(x)?.IsPriKey==true);
			object obj=Activator.CreateInstance(type);
			Fill(obj,listFields,table,1);
			long priKey=(long)methodInsert.Invoke(null,new[] { obj });
			object objLoaded=methodSelectOne.Invoke(null,new object[] { priKey });
			Compare(type.Name+" insert",obj,objLoaded,listFields,fieldPriKey,false,listProblems);
			Fill(obj,listFields,table,2);
			fieldPriKey.SetValue(obj,priKey);
			methodUpdate.Invoke(null,new[] { obj });
			objLoaded=methodSelectOne.Invoke(null,new object[] { priKey });
			Compare(type.Name+" update",obj,objLoaded,listFields,fieldPriKey,true,listProblems);
		}

		///<summary>Sets every column field (except the key and the ones the database fills) to a value that depends on the field and the pass.</summary>
		private static void Fill(object obj,List<FieldInfo> listFields,SchemaTable table,int pass) {
			for(int i=0;i<listFields.Count;i++) {
				FieldInfo field=listFields[i];
				if(IsSkipped(field,false)) {
					continue;
				}
				SchemaColumn column=table.GetColumn(field.Name);
				object value=GetValue(field,column,i+pass*7);
				if(value!=null) {
					field.SetValue(obj,value);
				}
			}
		}

		private static object GetValue(FieldInfo field,SchemaColumn column,int seed) {
			Type type=field.FieldType;
			CrudSpecialColType specialType=GetCrudColumn(field)?.SpecialType??CrudSpecialColType.None;
			if(type.IsEnum) {
				Array values=Enum.GetValues(type);
				return values.GetValue(seed%values.Length);
			}
			if(type==typeof(string)) {
				string value="v"+seed+field.Name;
				Match match=Regex.Match(column.Definition,@"^(?:var)?char\((\d+)\)");
				if(match.Success) {
					value=value.Substring(0,Math.Min(value.Length,int.Parse(match.Groups[1].Value)));
				}
				return value;
			}
			if(type==typeof(long)) {
				return (long)(1000+seed);
			}
			if(type==typeof(int)) {
				return 10+seed;
			}
			if(type==typeof(byte)) {
				return (byte)(1+seed%100);
			}
			if(type==typeof(bool)) {
				return seed%2==0;
			}
			if(type==typeof(double)) {
				return 12.5+seed;
			}
			if(type==typeof(float)) {
				return 1.5f+seed;
			}
			if(type==typeof(DateTime)) {
				if(specialType.HasFlag(CrudSpecialColType.DateT) || column.Definition.StartsWith("datetime")) {
					return new DateTime(2024,5,6,7,8,9).AddMinutes(seed);
				}
				return new DateTime(2024,5,6).AddDays(seed);
			}
			if(type==typeof(TimeSpan)) {
				if(specialType.HasFlag(CrudSpecialColType.TimeSpanLong)) {
					return TimeSpan.FromMinutes(seed);
				}
				return new TimeSpan(1,2,seed%60);
			}
			if(type==typeof(Color)) {
				return Color.FromArgb(255,seed%256,10,20);
			}
			return null;//Left at its default and not compared.
		}

		private static void Compare(string label,object expected,object actual,List<FieldInfo> listFields,FieldInfo fieldPriKey,bool isUpdate,
			List<string> listProblems)
		{
			if(actual==null) {
				listProblems.Add(label+": SelectOne found no row.");
				return;
			}
			foreach(FieldInfo field in listFields) {
				if(field==fieldPriKey || IsSkipped(field,isUpdate)) {
					continue;
				}
				object valueExpected=field.GetValue(expected);
				object valueActual=field.GetValue(actual);
				if(valueExpected is Color colorExpected) {
					valueExpected=colorExpected.ToArgb();
					valueActual=((Color)valueActual).ToArgb();
				}
				if(valueExpected is double doubleExpected) {
					if(Math.Abs(doubleExpected-(double)valueActual)>0.0001) {
						listProblems.Add(label+": "+field.Name+" expected "+valueExpected+" but was "+valueActual);
					}
					continue;
				}
				if(!Equals(valueExpected??"",valueActual??"")) {
					listProblems.Add(label+": "+field.Name+" expected ["+valueExpected+"] but was ["+valueActual+"]");
				}
			}
		}

		///<summary>Fields the database or the CRUD layer sets on its own, so a test value is neither written nor compared.</summary>
		private static bool IsSkipped(FieldInfo field,bool isUpdate) {
			CrudColumnAttribute crudColumn=GetCrudColumn(field);
			if(crudColumn==null) {
				return false;
			}
			if(crudColumn.IsPriKey) {
				return true;
			}
			CrudSpecialColType specialType=crudColumn.SpecialType;
			if(specialType.HasFlag(CrudSpecialColType.TimeStamp) || specialType.HasFlag(CrudSpecialColType.DateEntry)
				|| specialType.HasFlag(CrudSpecialColType.DateTEntry) || specialType.HasFlag(CrudSpecialColType.DateTEntryEditable)
				|| specialType.HasFlag(CrudSpecialColType.DateEntryEditable))
			{
				return true;
			}
			if(isUpdate && specialType.HasFlag(CrudSpecialColType.ExcludeFromUpdate)) {
				return true;
			}
			return false;
		}

		private static CrudColumnAttribute GetCrudColumn(FieldInfo field) {
			return field.GetCustomAttributes(typeof(CrudColumnAttribute),true).OfType<CrudColumnAttribute>().FirstOrDefault();
		}
	}
}
