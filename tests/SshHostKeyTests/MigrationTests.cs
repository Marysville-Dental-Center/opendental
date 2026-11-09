// The SSH host keys step (2026110221) and the fork table declaration: on an empty and on an already migrated scratch database, the level, the
// declaration, the schema verifier, the adoption rule and the README.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;
using OpenPmsDentalBusiness.ForkMigrations.FeatureSteps;

namespace SshHostKeyTests {
	[TestClass]
	public class MigrationTests {
		private const string TABLE="sshhostkey";

		private static List<SchemaDifference> GetHostKeyDifferences(SchemaVerifyResult result) {
			return result.ListDifferences.FindAll(x => x.Table==TABLE);
		}

		#region Declarations (no database)
		[TestMethod]
		public void Step_HasTheFeaturesFirstLevel_AndIsValid() {
			List<ForkMigrationStep> listSteps=ForkMigrationRunner.GetSteps();
			List<ForkMigrationStep> listHostKeySteps=listSteps.Where(x => x.Level>=2026110221 && x.Level<=2026110229).ToList();
			Assert.AreEqual(1,listHostKeySteps.Count);
			Assert.IsInstanceOfType(listHostKeySteps[0],typeof(SshHostKeyTableStep));
			Assert.AreEqual(2026110221,listHostKeySteps[0].Level);
			foreach(ForkMigrationStep step in listSteps) {
				ForkMigrationRunner.ValidateStep(step);
			}
			ForkMigrationRunner.ValidateLevelsUnique(listSteps);
		}

		[TestMethod]
		public void Table_IsAForkTable_DeclaredOnce() {
			CollectionAssert.Contains(ForkTables.GetTableNames(),TABLE);
			Assert.IsInstanceOfType(ForkTables.GetSet(TABLE),typeof(SshHostKeyTables));
			Assert.AreEqual("SSH host keys",new SshHostKeyTables().Feature);
			Assert.AreEqual("2026110221",new SshHostKeyTables().Levels);
			Assert.IsNull(SchemaVerifier.GetReference().GetTable(TABLE),"Not Open Dental schema.");
			Assert.IsTrue(ForkTables.GetSchemaExceptions().Any(x => x.Table==TABLE && x.Kind==SchemaDifferenceKind.ExtraTable));
			SchemaTable schemaTable=ForkTables.GetSnapshot().GetTable(TABLE);
			Assert.AreEqual("MyISAM",schemaTable.Engine);
			Assert.AreEqual("utf8mb3",schemaTable.Charset);
			Assert.AreEqual("UNIQUE KEY",schemaTable.GetIndex("HostPort").Kind,"One row per server.");
			Assert.IsNotNull(schemaTable.GetIndex("UserNum"));
		}

		[TestMethod]
		public void TableClass_MapsTheDeclarationColumnByColumn() {
			SchemaTable schemaTable=ForkTables.GetSnapshot().GetTable(TABLE);
			Assert.AreEqual(TABLE,CrudTableAttribute.GetTableName(typeof(SshHostKey)).ToLowerInvariant());
			List<string> listFields=typeof(SshHostKey).GetFields(BindingFlags.Public|BindingFlags.Instance).Select(x => x.Name).ToList();
			CollectionAssert.AreEqual(listFields,schemaTable.Columns.Select(x => x.Name).ToList());
		}

		[TestMethod]
		public void AdoptionRule_ReadmeAndSpec_NameTheTable() {
			string json=TestDb.ReadRepoFile("tools","SyntheticData","allowed-differences.adoption.json");
			StringAssert.Contains(json,"\"Kind\": \"TableAdded\", \"Table\": \"sshhostkey\"");
			string readme=TestDb.ReadRepoFile("OpenPmsDentalBusiness","Misc","ForkMigrations","README.md");
			StringAssert.Contains(readme,"| 2026110221 | `SshHostKeySteps.cs`");
			string spec=TestDb.ReadRepoFile("docs","specs","ssh-host-keys.md");
			StringAssert.Contains(spec,"2026110221");
			StringAssert.Contains(spec,"`sshhostkey`");
		}
		#endregion Declarations (no database)

		#region Steps on scratch databases
		[TestMethod]
		public void Step_OnTheScratchDatabase_CreatedTheTableOnce_AsDeclared() {
			TestDb.ResetOrSkip();
			Assert.AreEqual(1,TestDb.ListChangesFirstRun.Count,string.Join("\r\n",TestDb.ListChangesFirstRun));
			StringAssert.Contains(TestDb.ListChangesFirstRun[0],"CREATE TABLE `sshhostkey`");
			SchemaVerifyResult result=ForkTables.VerifyDatabase(TestDb.DbName);
			Assert.AreEqual(0,GetHostKeyDifferences(result).Count,result.GetReport());
		}

		[TestMethod]
		public void SchemaVerifier_ScratchDatabaseStillMatchesOpenDental26_1() {
			TestDb.ResetOrSkip();
			SchemaVerifyResult result=SchemaVerifier.VerifyDatabase(TestDb.DbName);
			Assert.IsTrue(result.IsMatch,result.GetReport());
			CollectionAssert.AreEquivalent(new[] { TABLE },result.ListAllowed.Where(x => x.Kind==SchemaDifferenceKind.ExtraTable).Select(x => x.Table).ToList());
		}

		[TestMethod]
		public void Step_OnAnAlreadyMigratedDatabase_ChangesNothing_AndKeepsTheRows() {
			TestDb.ResetOrSkip();
			SshHostKeys.InsertFirstUse("sftp.example.test",22,"ssh-ed25519","AAAA");
			ForkMigrationContext context=new ForkMigrationContext(TestDb.DbName);
			new SshHostKeyTableStep().Apply(context);
			Assert.AreEqual(0,context.ListChanges.Count,string.Join("\r\n",context.ListChanges));
			Assert.AreEqual(1,TestDb.GetLong("SELECT COUNT(*) FROM sshhostkey"));
			Assert.AreEqual(0,GetHostKeyDifferences(ForkTables.VerifyDatabase(TestDb.DbName)).Count);
		}

		[TestMethod]
		public void Step_OnAnEmptyDatabase_CreatesTheEmptyTable_ThenChangesNothing() {
			TestDb.ResetOrSkip();
			string dbName=TestDb.CreateExtra("empty");
			try {
				new DataConnectionBase.DataConnection().SetDb(TestDb.Server,dbName,TestDb.User,TestDb.Password,"","",DataConnectionBase.DatabaseType.MySql,true,DevDbTls.GetSslCa(TestDb.Server));
				ForkMigrationContext context=new ForkMigrationContext(dbName);
				new SshHostKeyTableStep().Apply(context);
				Assert.AreEqual(1,context.ListChanges.Count,string.Join("\r\n",context.ListChanges));
				Assert.AreEqual(1,SchemaSnapshot.FromDatabase(dbName).Tables.Count);
				Assert.AreEqual(0,GetHostKeyDifferences(ForkTables.VerifyDatabase(dbName)).Count,ForkTables.VerifyDatabase(dbName).GetReport());
				Assert.AreEqual(0,TestDb.GetLong("SELECT COUNT(*) FROM sshhostkey"),"The step writes no row.");
				ForkMigrationContext contextSecond=new ForkMigrationContext(dbName);
				new SshHostKeyTableStep().Apply(contextSecond);
				Assert.AreEqual(0,contextSecond.ListChanges.Count,string.Join("\r\n",contextSecond.ListChanges));
				//A table that differs from the declaration is reported, never altered by the step.
				DataCore.NonQ("ALTER TABLE sshhostkey MODIFY COLUMN FingerprintSha256 varchar(100) NOT NULL");
				new SshHostKeyTableStep().Apply(new ForkMigrationContext(dbName));
				SchemaVerifyResult resultChanged=ForkTables.VerifyDatabase(dbName);
				Assert.IsTrue(resultChanged.ListMismatches.Any(x => x.Table==TABLE && x.Item=="FingerprintSha256"),resultChanged.GetReport());
			}
			finally {
				TestDb.Drop(dbName);
				TestDb.Connect();
			}
		}
		#endregion Steps on scratch databases
	}
}
