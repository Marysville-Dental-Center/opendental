// The routine maintenance schema: the fork-only table declarations, the steps 2026110201 to 2026110205 and 2026110207 on scratch databases, the
// schema verifier's view of them, and the adoption tools' rules (docs/specs/routine-maintenance.md, "Schema migration and the adoption tools").
// The grant step 2026110206 has its own tests (GrantStepTests).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;
using OpenPmsDentalBusiness.ForkMigrations.FeatureSteps;

namespace MaintenanceTests {
	[TestClass]
	public class MigrationTests {
		private static readonly string[] _arrayTables={ "maintfield","maintitem","maintrecord","maintrecordvalue","maintstep" };
		private static readonly Type[] _arrayTypes={ typeof(MaintField),typeof(MaintItem),typeof(MaintRecord),typeof(MaintRecordValue),typeof(MaintStep) };

		[TestInitialize]
		public void TestInitialize() {
			TestDb.SetCultureUs();
		}

		#region Declarations (no database)
		[TestMethod]
		public void ForkTables_DeclareTheFiveMaintenanceTables() {
			CollectionAssert.IsSubsetOf(_arrayTables,ForkTables.GetTableNames());
			foreach(string table in _arrayTables) {
				Assert.IsTrue(ForkTables.IsForkTable(table));
				Assert.IsTrue(ForkTables.IsForkTable(table.ToUpperInvariant()),"Names are compared without case, as MariaDB does on Windows.");
				Assert.IsFalse(SchemaVerifier.GetReference().HasTable(table),table+" is not Open Dental schema.");
				Assert.IsInstanceOfType(ForkTables.GetSet(table),typeof(MaintenanceTables));
			}
			Assert.IsFalse(ForkTables.IsForkTable("equipment"));
			Assert.IsFalse(ForkTables.IsForkTable(""));
			Assert.AreEqual("Routine maintenance",new MaintenanceTables().Feature);
		}

		[TestMethod]
		public void SchemaVerifier_AllowsEachForkTableAsAnExtraTableOnly() {
			foreach(string table in _arrayTables) {
				SchemaException schemaException=SchemaVerifier.DocumentedExceptions.Single(x => string.Equals(x.Table,table,StringComparison.OrdinalIgnoreCase));
				Assert.AreEqual(SchemaDifferenceKind.ExtraTable,schemaException.Kind);
				StringAssert.Contains(schemaException.Reason,"Routine maintenance");
				Assert.IsTrue(SchemaVerifier.DocumentedExceptions25_3_48.Any(x => x.Table==table),"The same exceptions for both references.");
			}
			//A missing fork table is not excused by the verifier (it is ForkTables.VerifyDatabase's job, below), and neither is a changed one.
			SchemaSnapshot snapshotWithExtra=new SchemaSnapshot();
			snapshotWithExtra.AddTable(SchemaTable.Parse(MaintenanceTables.CREATE_ITEM));
			SchemaVerifyResult result=SchemaVerifier.Compare(new SchemaSnapshot(),snapshotWithExtra,SchemaVerifier.DocumentedExceptions);
			Assert.IsTrue(result.IsMatch,result.GetReport());
			SchemaVerifyResult resultMissing=SchemaVerifier.Compare(snapshotWithExtra,new SchemaSnapshot(),SchemaVerifier.DocumentedExceptions);
			Assert.IsFalse(resultMissing.IsMatch);
		}

		[TestMethod]
		public void TableClasses_MapExactlyToTheirDeclarations() {
			SchemaSnapshot snapshot=ForkTables.GetSnapshot();
			List<string> listProblems=new List<string>();
			foreach(Type type in _arrayTypes) {
				string tableName=CrudTableAttribute.GetTableName(type);
				SchemaTable table=snapshot.GetTable(tableName);
				Assert.IsNotNull(table,tableName);
				List<FieldInfo> listFields=GetDbFields(type);
				CollectionAssert.AreEqual(table.Columns.Select(x => x.Name).ToList(),listFields.Select(x => x.Name).ToList(),type.Name+": fields in column order");
				Assert.AreEqual(type.Name+"Num",table.Columns[0].Name,"The primary key is <Table>Num.");
				Assert.AreEqual("bigint(20) NOT NULL AUTO_INCREMENT",table.Columns[0].Definition);
				Assert.AreEqual(tableName.ToLowerInvariant(),tableName,"Lower-case table names.");
				Assert.AreEqual("MyISAM",table.Engine);
				Assert.AreEqual("utf8mb3",table.Charset);
				foreach(FieldInfo field in listFields) {
					string definition=table.GetColumn(field.Name).Definition;
					CrudSpecialColType specialType=GetCrudColumn(field)?.SpecialType??CrudSpecialColType.None;
					if(definition.StartsWith("text") && !specialType.HasFlag(CrudSpecialColType.IsText)) {
						listProblems.Add(type.Name+"."+field.Name+" is text but not IsText");
					}
					if(definition.StartsWith("datetime") && !specialType.HasFlag(CrudSpecialColType.DateTEntry) && !specialType.HasFlag(CrudSpecialColType.DateT)) {
						listProblems.Add(type.Name+"."+field.Name+" is datetime but keeps no time");
					}
					if(definition.StartsWith("timestamp") && !specialType.HasFlag(CrudSpecialColType.TimeStamp)) {
						listProblems.Add(type.Name+"."+field.Name+" is a timestamp but not TimeStamp");
					}
					if(field.FieldType==typeof(double) && !specialType.HasFlag(CrudSpecialColType.Double)) {
						listProblems.Add(type.Name+"."+field.Name+" is a double without Double (it would be rounded to 2 places)");
					}
					if(field.FieldType==typeof(bool) && definition!="tinyint(4) NOT NULL") {
						listProblems.Add(type.Name+"."+field.Name+": "+definition);
					}
					if(field.FieldType.IsEnum && definition!="tinyint(4) NOT NULL") {
						listProblems.Add(type.Name+"."+field.Name+": "+definition);
					}
					if(field.FieldType==typeof(long) && !definition.StartsWith("bigint(20)")) {
						listProblems.Add(type.Name+"."+field.Name+": "+definition);
					}
				}
				//Every FK column (other than the primary key) has a key of its own name.
				foreach(SchemaColumn column in table.Columns.Skip(1).Where(x => x.Definition.StartsWith("bigint(20)"))) {
					if(table.GetIndex(column.Name)==null) {
						listProblems.Add(tableName+"."+column.Name+" has no key");
					}
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
		}

		[TestMethod]
		public void Steps_AreLevels2026110201To2026110207_TheTablesThenTheGrantThenTheCorrectionColumns() {
			List<ForkMigrationStep> listSteps=TestDb.GetMaintenanceSteps();
			CollectionAssert.AreEqual(new long[] { 2026110201,2026110202,2026110203,2026110204,2026110205,2026110206,2026110207 },
				listSteps.Select(x => x.Level).ToList());
			CollectionAssert.AreEqual(new long[] { 2026110201,2026110202,2026110203,2026110204,2026110205 },
				TestDb.GetMaintenanceTableSteps().Select(x => x.Level).ToList());
			Assert.IsInstanceOfType(listSteps[5],typeof(MaintPermissionGrantStep));
			Assert.IsInstanceOfType(listSteps[6],typeof(MaintRecordCorrectionColumnsStep));
			foreach(ForkMigrationStep step in listSteps) {
				ForkMigrationRunner.ValidateStep(step);
				StringAssert.StartsWith(step.Name,"Routine maintenance: ");
			}
			ForkMigrationRunner.ValidateLevelsUnique(ForkMigrationRunner.GetSteps());
			//Every step comes after the steps merged before it.
			Assert.IsTrue(ForkMigrationRunner.GetSteps().Where(x => x.Level<2026110201).All(x => x.Level<=2026110104));
		}

		[TestMethod]
		public void AdoptionAllowedDifferences_HaveATableAddedRulePerForkTable() {
			string text=TestDb.ReadRepoFile("tools","SyntheticData","allowed-differences.adoption.json");
			foreach(string table in _arrayTables) {
				Assert.AreEqual(1,Regex.Matches(text,"\"Kind\": \"TableAdded\", \"Table\": \""+table+"\"").Count,table);
				Assert.AreEqual(1,Regex.Matches(text,"\"Table\": \""+table+"\"").Count,"No other rule names "+table+".");
			}
		}

		private static List<FieldInfo> GetDbFields(Type type) {
			return type.GetFields(BindingFlags.Public|BindingFlags.Instance).Where(x => GetCrudColumn(x)?.IsNotDbColumn!=true).ToList();
		}

		private static CrudColumnAttribute GetCrudColumn(FieldInfo field) {
			return field.GetCustomAttributes(typeof(CrudColumnAttribute),true).OfType<CrudColumnAttribute>().FirstOrDefault();
		}
		#endregion Declarations (no database)

		#region Steps on scratch databases
		[TestMethod]
		public void Steps_CreatedEveryTableOnce_AndWroteNoRow() {
			TestDb.ResetOrSkip();
			Assert.AreEqual(5,TestDb.ListChangesFirstRun.Count,string.Join("\r\n",TestDb.ListChangesFirstRun));
			foreach(string table in _arrayTables) {
				Assert.AreEqual(1,TestDb.ListChangesFirstRun.Count(x => x.StartsWith("CREATE TABLE `"+table+"`")),table);
			}
		}

		[TestMethod]
		public void Tables_MatchTheirDeclarationsExactly() {
			TestDb.ResetOrSkip();
			SchemaVerifyResult result=ForkTables.VerifyDatabase(TestDb.DbName);
			Assert.AreEqual(0,result.ListDifferences.Count,result.GetReport());
		}

		[TestMethod]
		public void SchemaVerifier_ScratchDatabaseStillMatchesOpenDental26_1() {
			TestDb.ResetOrSkip();
			SchemaVerifyResult result=SchemaVerifier.VerifyDatabase(TestDb.DbName);
			Assert.IsTrue(result.IsMatch,result.GetReport());
			CollectionAssert.AreEquivalent(_arrayTables,result.ListAllowed.Where(x => x.Kind==SchemaDifferenceKind.ExtraTable).Select(x => x.Table).ToList());
		}

		[TestMethod]
		public void SecondRun_ChangesNothing() {
			TestDb.ResetOrSkip();
			ForkMigrationContext context=new ForkMigrationContext(TestDb.DbName);
			foreach(ForkMigrationStep step in TestDb.GetMaintenanceSteps()) {
				step.Apply(context);
			}
			Assert.AreEqual(0,context.ListChanges.Count,string.Join("\r\n",context.ListChanges));
		}

		///<summary>On a database that has none of the tables, the steps create exactly the declared tables, one per step, and a changed table is
		///reported by ForkTables.VerifyDatabase (the steps never alter an existing table).</summary>
		[TestMethod]
		public void Steps_OnAnEmptyDatabase_ThenAChangedTableIsReported() {
			TestDb.ResetOrSkip();
			string dbName=TestDb.CreateExtra("steps");
			try {
				new DataConnectionBase.DataConnection().SetDb(TestDb.Server,dbName,TestDb.User,TestDb.Password,"","",DataConnectionBase.DatabaseType.MySql,true);
				ForkMigrationContext context=new ForkMigrationContext(dbName);
				List<ForkMigrationStep> listSteps=TestDb.GetMaintenanceTableSteps();
				for(int i=0;i<listSteps.Count;i++) {
					int countBefore=context.ListChanges.Count;
					listSteps[i].Apply(context);
					Assert.AreEqual(countBefore+1,context.ListChanges.Count,listSteps[i]+" creates one table.");
				}
				//The table step created maintrecord complete, so the correction columns step finds nothing to add.
				new MaintRecordCorrectionColumnsStep().Apply(context);
				Assert.AreEqual(listSteps.Count,context.ListChanges.Count,string.Join("\r\n",context.ListChanges));
				SchemaVerifyResult result=ForkTables.VerifyDatabase(dbName);
				Assert.AreEqual(0,result.ListDifferences.Count,result.GetReport());
				Assert.AreEqual(5,SchemaSnapshot.FromDatabase(dbName).Tables.Count);
				DataCore.NonQ("ALTER TABLE maintstep MODIFY COLUMN Description varchar(100) NOT NULL");
				SchemaVerifyResult resultChanged=ForkTables.VerifyDatabase(dbName);
				Assert.IsFalse(resultChanged.IsMatch);
				Assert.IsTrue(resultChanged.ListMismatches.Any(x => x.Table=="maintstep" && x.Item=="Description"),resultChanged.GetReport());
				DataCore.NonQ("DROP TABLE maintfield");
				SchemaVerifyResult resultMissing=ForkTables.VerifyDatabase(dbName);
				Assert.IsTrue(resultMissing.ListMismatches.Any(x => x.Table=="maintfield" && x.Kind==SchemaDifferenceKind.MissingTable),resultMissing.GetReport());
			}
			finally {
				TestDb.Drop(dbName);
				TestDb.Connect();
			}
		}

		///<summary>maintrecord as the first build of the feature (next/maint-2) created it, without the correction columns.</summary>
		private const string CREATE_RECORD_FIRST_BUILD=@"CREATE TABLE `maintrecord` (
  `MaintRecordNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `MaintItemNum` bigint(20) NOT NULL,
  `DateDone` date NOT NULL DEFAULT '0001-01-01',
  `DateDue` date NOT NULL DEFAULT '0001-01-01',
  `UserNum` bigint(20) NOT NULL,
  `Note` text NOT NULL,
  `IsFlagged` tinyint(4) NOT NULL,
  `SecDateTEntry` datetime NOT NULL DEFAULT '0001-01-01 00:00:00',
  PRIMARY KEY (`MaintRecordNum`),
  KEY `MaintItemNum` (`MaintItemNum`),
  KEY `UserNum` (`UserNum`),
  KEY `DateDone` (`DateDone`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci";

		///<summary>Step 2026110207 on a database where the first build created maintrecord: it adds the two columns and the key, the table then matches
		///its declaration exactly, a recorded completion keeps its values (never corrected), and a second run changes nothing.</summary>
		[TestMethod]
		public void CorrectionColumnsStep_AddsTheColumnsToTheFirstBuildsTable_ThenChangesNothing() {
			TestDb.ResetOrSkip();
			string dbName=TestDb.CreateExtra("correct");
			try {
				new DataConnectionBase.DataConnection().SetDb(TestDb.Server,dbName,TestDb.User,TestDb.Password,"","",DataConnectionBase.DatabaseType.MySql,true);
				ForkMigrationContext context=new ForkMigrationContext(dbName);
				foreach(string statement in new MaintenanceTables().ArrayCreateTables) {
					context.CreateTableIfMissing(SchemaTable.Parse(statement).Name=="maintrecord" ? CREATE_RECORD_FIRST_BUILD : statement);
				}
				Assert.IsFalse(ForkTables.VerifyDatabase(dbName).IsMatch,"The first build's maintrecord lacks the correction columns.");
				DataCore.NonQ("INSERT INTO maintrecord (MaintItemNum,DateDone,DateDue,UserNum,Note,IsFlagged,SecDateTEntry) "
					+"VALUES (3,'2026-10-30','2026-11-01',2,'Done early',1,'2026-10-30 09:15:00')");
				int countBefore=context.ListChanges.Count;
				new MaintRecordCorrectionColumnsStep().Apply(context);
				Assert.AreEqual(countBefore+3,context.ListChanges.Count,"Two columns and the key: "+string.Join("\r\n",context.ListChanges));
				SchemaVerifyResult result=ForkTables.VerifyDatabase(dbName);
				Assert.AreEqual(0,result.ListDifferences.Count,result.GetReport());
				DataTable table=DataCore.GetTable("SELECT MaintItemNum,DateDone,UserNum,Note,IsFlagged,SecDateTEntry,UserNumCorrected,DateTCorrected FROM maintrecord");
				Assert.AreEqual(1,table.Rows.Count);
				Assert.AreEqual("3",table.Rows[0]["MaintItemNum"].ToString());
				Assert.AreEqual("Done early",table.Rows[0]["Note"].ToString());
				Assert.AreEqual(new DateTime(2026,10,30,9,15,0),PIn.DateT(table.Rows[0]["SecDateTEntry"].ToString()));
				Assert.AreEqual("0",table.Rows[0]["UserNumCorrected"].ToString(),"Never corrected.");
				Assert.IsTrue(PIn.DateT(table.Rows[0]["DateTCorrected"].ToString()).Year<1880);
				int countAfter=context.ListChanges.Count;
				new MaintRecordCorrectionColumnsStep().Apply(context);
				Assert.AreEqual(countAfter,context.ListChanges.Count,"A second run changes nothing.");
			}
			finally {
				TestDb.Drop(dbName);
				TestDb.Connect();
			}
		}
		#endregion Steps on scratch databases
	}
}
