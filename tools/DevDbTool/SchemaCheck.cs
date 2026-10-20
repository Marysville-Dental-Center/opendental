// Licensed under the GNU General Public License, version 2. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;

namespace DevDbTool {
	///<summary>Checks that a database matches what this build of the business layer expects: version preferences, conversion flags, and
	///every table and column declared by the table types (the same set the CRUD generator works from).</summary>
	public class SchemaCheck {
		///<summary>Table types still present in the code whose tables a convert script deliberately drops, so a database at this version never
		///has them. Key is the table name, value is why.</summary>
		private static readonly Dictionary<string,string> _dictDroppedTables=new Dictionary<string,string> {
			{ "plannedappt","dropped by To24_2_1; its rows moved to appointment.ItemOrderPlanned" },
		};

		public readonly List<string> ListProblems=new List<string>();
		public readonly List<string> ListNotes=new List<string>();
		public int CountTables;
		public int CountColumns;
		public int CountHqOnlySkipped;

		public bool IsOk {
			get {
				return ListProblems.Count==0;
			}
		}

		///<summary>isForkMigrated: the database should have every fork migration (the normal case). False for a plain 24.3.63 database made with
		///--no-fork-migrations: columns that only the fork migrations add are then listed as notes, not problems.</summary>
		public static SchemaCheck Run(string databaseName,bool isForkMigrated=true) {
			SchemaCheck check=new SchemaCheck();
			check.CheckPrefs();
			check.CheckTables(databaseName,isForkMigrated);
			if(isForkMigrated) {
				check.CheckForkMigrations(databaseName);
			}
			check.CheckAdminUser();
			check.CheckEngines(databaseName);
			return check;
		}

		private void CheckPrefs() {
			Version versionProgram=DevDatabase.VersionProgram;
			string dbVersion=DevDatabase.GetPrefRaw(PrefName.DataBaseVersion);
			if(dbVersion==null) {
				ListProblems.Add("Preference DataBaseVersion is missing.");
			}
			else {
				Version versionDb=new Version(dbVersion);
				if(versionDb!=versionProgram) {
					ListProblems.Add("DataBaseVersion is "+dbVersion+" but the program is "+versionProgram+".");
				}
				if(versionDb!=ConvertDatabases.LatestVersion) {
					ListProblems.Add("DataBaseVersion is "+dbVersion+" but the newest convert method is "+ConvertDatabases.LatestVersion+".");
				}
			}
			string programVersion=DevDatabase.GetPrefRaw(PrefName.ProgramVersion);
			if(programVersion!=versionProgram.ToString()) {
				ListProblems.Add("ProgramVersion is "+(programVersion??"missing")+" but the program is "+versionProgram+".");
			}
			if(DevDatabase.GetPrefRaw(PrefName.CorruptedDatabase)!="0") {
				ListProblems.Add("CorruptedDatabase is set: a conversion did not finish.");
			}
			string updateInProgress=DevDatabase.GetPrefRaw(PrefName.UpdateInProgressOnComputerName);
			if(!string.IsNullOrEmpty(updateInProgress)) {
				ListProblems.Add("UpdateInProgressOnComputerName is '"+updateInProgress+"'.");
			}
			DataTable tableDupes=DataCore.GetTable("SELECT PrefName,COUNT(*) FROM preference GROUP BY PrefName HAVING COUNT(*)>1");
			foreach(DataRow row in tableDupes.Rows) {
				ListProblems.Add("Preference "+row[0]+" appears "+row[1]+" times.");
			}
			//Informational only: some PrefName values never get a preference row (clinicpref-only, HQ-only or optional prefs).
			HashSet<string> setPrefNames=new HashSet<string>(
				DataCore.GetTable("SELECT PrefName FROM preference").Rows.Cast<DataRow>().Select(x => x[0].ToString()),
				StringComparer.OrdinalIgnoreCase);
			int countMissingPrefs=Enum.GetNames(typeof(PrefName)).Count(x => !setPrefNames.Contains(x));
			ListNotes.Add(setPrefNames.Count+" preference rows; "+countMissingPrefs+" PrefName values have none (clinicpref-only, HQ-only or "
				+"optional prefs that the convert scripts never insert).");
		}

		private void CheckTables(string databaseName,bool isForkMigrated) {
			List<string> listMissing=new List<string>();
			string command="SELECT LOWER(TABLE_NAME),LOWER(COLUMN_NAME) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='"
				+POut.String(databaseName)+"'";
			Dictionary<string,HashSet<string>> dictColumns=DataCore.GetTable(command).Rows.Cast<DataRow>()
				.GroupBy(x => x[0].ToString(),x => x[1].ToString())
				.ToDictionary(x => x.Key,x => new HashSet<string>(x));
			foreach(Type type in GetTableTypes()) {
				CrudTableAttribute crudTable=type.GetCustomAttributes(typeof(CrudTableAttribute),true).OfType<CrudTableAttribute>()
					.FirstOrDefault(x => x.GetType()==typeof(CrudTableAttribute));
				if(crudTable!=null && crudTable.IsMissingInGeneral) {
					CountHqOnlySkipped++;//Internal HQ tables that a normal install never has.
					continue;
				}
				string tableName=CrudTableAttribute.GetTableName(type).ToLowerInvariant();
				if(_dictDroppedTables.TryGetValue(tableName,out string reason)) {
					if(dictColumns.ContainsKey(tableName)) {
						ListNotes.Add("Table "+tableName+" exists although it is normally "+reason+".");
					}
					continue;
				}
				CountTables++;
				if(!dictColumns.TryGetValue(tableName,out HashSet<string> setColumns)) {
					listMissing.Add("Missing table: "+tableName+" ("+type.FullName+")");
					continue;
				}
				foreach(FieldInfo field in GetDbFields(type)) {
					CountColumns++;
					if(!setColumns.Contains(field.Name.ToLowerInvariant())) {
						listMissing.Add("Missing column: "+tableName+"."+field.Name);
					}
				}
			}
			if(isForkMigrated) {
				ListProblems.AddRange(listMissing);
			}
			else if(listMissing.Count>0) {
				ListNotes.Add(listMissing.Count+" tables or columns of the table classes are missing, as expected before the fork migrations "
					+"(--no-fork-migrations).");
			}
		}

		///<summary>Every fork migration finished, and the schema matches Open Dental 25.3.48 apart from the documented exceptions.</summary>
		private void CheckForkMigrations(string databaseName) {
			List<ForkMigrationStep> listPending=ForkMigrationRunner.GetPendingSteps();
			foreach(ForkMigrationStep step in listPending) {
				ListProblems.Add("Fork migration "+step+" has not run.");
			}
			string inProgress=DevDatabase.GetPrefRaw(ForkMigrationRunner.PrefNameInProgress);
			if(!string.IsNullOrEmpty(inProgress)) {
				ListProblems.Add("Fork migration "+inProgress+" was interrupted.");
			}
			if(listPending.Count>0) {
				return;//The schema comparison would only repeat what the pending steps add.
			}
			SchemaVerifyResult result=SchemaVerifier.VerifyDatabase(databaseName);
			foreach(SchemaDifference difference in result.ListMismatches) {
				ListProblems.Add("Schema differs from Open Dental "+SchemaVerifier.ReferenceVersion+": "+difference);
			}
			ListNotes.Add("Schema matches Open Dental "+SchemaVerifier.ReferenceVersion+(result.IsMatch ? "" : " except as listed")+" ("
				+result.ListAllowed.Count+" documented exception(s), for example tables Open Dental no longer has).");
		}

		///<summary>Concrete table types, as the CRUD generator enumerates them.</summary>
		private static IEnumerable<Type> GetTableTypes() {
			Type typeTableBase=typeof(TableBase);
			return typeTableBase.Assembly.GetTypes()
				.Where(x => x.IsSubclassOf(typeTableBase) && !x.IsAbstract && !x.Name.StartsWith("WebForms_"))
				.OrderBy(x => x.Name);
		}

		///<summary>Public instance fields that map to columns, i.e. without [CrudColumn(IsNotDbColumn=true)].</summary>
		private static IEnumerable<FieldInfo> GetDbFields(Type type) {
			foreach(FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance)) {
				CrudColumnAttribute crudColumn=field.GetCustomAttributes(typeof(CrudColumnAttribute),true).OfType<CrudColumnAttribute>()
					.FirstOrDefault();
				if(crudColumn!=null && crudColumn.IsNotDbColumn) {
					continue;
				}
				yield return field;
			}
		}

		private void CheckAdminUser() {
			DataTable table=DataCore.GetTable("SELECT UserNum,Password FROM userod WHERE UserName='Admin'");
			if(table.Rows.Count==0) {
				ListNotes.Add("No user named Admin.");
				return;
			}
			string password=table.Rows[0]["Password"].ToString();
			ListNotes.Add("User Admin (UserNum "+table.Rows[0]["UserNum"]+") has "+(password=="" ? "a blank password." : "a password set."));
		}

		private void CheckEngines(string databaseName) {
			DataTable table=DataCore.GetTable("SELECT ENGINE,COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA='"
				+POut.String(databaseName)+"' AND TABLE_TYPE='BASE TABLE' GROUP BY ENGINE");
			string engines=string.Join(", ",table.Rows.Cast<DataRow>().Select(x => x[1]+" "+x[0]));
			ListNotes.Add("Tables by engine: "+engines);
			if(table.Rows.Count>1) {
				ListNotes.Add("Mixed storage engines. OpenDental.exe converts them all to the default engine (after a backup) before its next "
					+"database conversion.");
			}
		}
	}
}
