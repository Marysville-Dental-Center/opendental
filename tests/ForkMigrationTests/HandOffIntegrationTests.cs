// The seams between the fork migration runner (M1-01), the registries and seed rows (M1-02) and the version guard and adoption (M1-03).
// Synthetic data only. The database tests are skipped (inconclusive) when the local MariaDB does not answer.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;

namespace ForkMigrationTests {
	[TestClass]
	public class HandOffIntegrationTests {
		private static readonly string DB_NAME_ADOPT243=TestDb.ScratchName("adopt243");

		[TestMethod]
		public void HandOff_DiscoversTheForkMigrationRunner() {
			IForkMigrationRunner runner=ForkMigrationHandOff.Runner;
			Assert.IsInstanceOfType(runner,typeof(ForkMigrationRunnerAdapter));
			Assert.IsTrue(ForkMigrationHandOff.HasSteps(runner));
			Assert.AreEqual(ForkMigrationRunner.GetSteps().Max(x => x.Level).ToString(),runner.LatestLevel);
			Assert.IsTrue(runner.CompareLevels(ForkVersions.SCHEMA_LEVEL_BASELINE,runner.LatestLevel)<0,"The adoption baseline sorts below every step.");
		}

		[TestMethod]
		public void Steps_SeedRowsRunAfterTheSchemaSteps() {
			List<ForkMigrationStep> listSteps=ForkMigrationRunner.GetSteps();
			ForkMigrationStep stepSeed=listSteps.Single(x => x is Seed25_3Step);
			List<ForkMigrationStep> listSchemaSteps=listSteps.Where(x => x.GetType().Namespace==typeof(Seed25_3Step).Namespace
				&& x.GetType().Assembly==typeof(Seed25_3Step).Assembly && !(x is Seed25_3Step)).ToList();
			Assert.IsTrue(listSchemaSteps.Count>0);
			Assert.IsTrue(listSchemaSteps.All(x => x.Level<stepSeed.Level),"Seed25_3Step must run after every 25.3.48 schema step.");
		}

		[TestMethod]
		public void Guard_FreshUnadopted24_3NeedsAdoptionNowThatStepsExist() {
			ForkDbState forkDbState=new ForkDbState();
			forkDbState.DataBaseVersion="24.3.63.0";
			forkDbState.ProgramVersion="24.3.63.0";
			Assert.AreEqual(ForkGuardAction.NeedsAdoption,ForkVersionGuard.Evaluate(forkDbState).Action);
		}

		///<summary>A fresh 24.3.63 database adopted with the real runner: markers consistent, nothing pending, the schema at 25.3.48, and the seed
		///rows written because the source in ForkAdoptedFrom is 24.3.63 (DataBaseVersion already holds the sentinel when the steps run).</summary>
		[TestMethod]
		public void Adoption_Fresh24_3WithTheRealRunner() {
			string skipReason=TestDb.GetSkipReason();
			if(skipReason!=null) {
				Assert.Inconclusive(skipReason);
			}
			try {
				TestDb.CreateFresh24_3(DB_NAME_ADOPT243);
				Prefs.UpdateStringNoCache(PrefName.ProgramVersion,"24.3.63.0");//As tools/New-DevDatabase.ps1 stamps it before adopting.
				Assert.AreEqual(ForkGuardAction.NeedsAdoption,ForkVersionGuard.Evaluate(ForkVersions.ReadState()).Action);
				AdoptionResult adoptionResult=Adoption.Run();
				Assert.AreEqual(new Version(24,3,63,0),adoptionResult.VersionSource);
				ForkDbState forkDbState=ForkVersions.ReadState();
				ForkGuardResult forkGuardResult=ForkVersionGuard.Evaluate(forkDbState);
				Assert.AreEqual(ForkGuardAction.ProceedFork,forkGuardResult.Action,forkGuardResult.Message);
				Assert.IsFalse(forkGuardResult.HasPendingMigrations);
				Assert.AreEqual(ForkMigrationHandOff.Runner.LatestLevel,forkDbState.ForkSchemaLevel);
				Assert.AreEqual(SchemaVerifier.ReferenceVersion,forkDbState.OdSchemaCompat);
				Assert.IsTrue(forkDbState.IsAdoptionRecordFinished);
				Assert.AreEqual(0,ForkMigrationRunner.GetPendingSteps().Count);
				SchemaVerifyResult schemaVerifyResult=SchemaVerifier.VerifyDatabase(DB_NAME_ADOPT243);
				Assert.IsTrue(schemaVerifyResult.IsMatch,schemaVerifyResult.GetReport());
				foreach(PrefName prefName in Seed25_3.ListPrefDefaults.Select(x => x.PrefName)) {
					Assert.AreEqual("1",DataCore.GetScalar("SELECT COUNT(*) FROM preference WHERE PrefName='"+POut.String(prefName.ToString())+"'"),
						"Seed preference "+prefName);
				}
				//BlockoutsFlagged goes to every user group on a database from 24.3.
				Assert.AreEqual(DataCore.GetScalar("SELECT COUNT(*) FROM usergroup"),
					DataCore.GetScalar("SELECT COUNT(*) FROM grouppermission WHERE PermType="+POut.Int((int)EnumPermType.BlockoutsFlagged)));
				//A second adoption is refused and a second run of the steps changes nothing.
				Assert.IsFalse(Adoption.Preflight().CanAdopt);
				ForkMigrationResult resultAgain=ForkMigrationRunner.Run(new ForkMigrationOptions { RunAll=true });
				Assert.AreEqual(0,resultAgain.ListChanges.Count,string.Join("\r\n",resultAgain.ListChanges));
			}
			finally {
				if(Environment.GetEnvironmentVariable("FORKMIGRATION_KEEP_DB")!="1") {
					try {
						TestDb.Drop(DB_NAME_ADOPT243);
					}
					catch(Exception) {
						//A later run drops it once it is stale (TestDb.DropStaleScratchDatabases).
					}
				}
			}
		}

		///<summary>An adopted Open Dental 25.3.48 database is never seeded, whatever DataBaseVersion says.</summary>
		[TestMethod]
		public void SeedStep_OriginComesFromTheAdoptionRecord() {
			string skipReason=TestDb.GetSkipReason();
			if(skipReason!=null) {
				Assert.Inconclusive(skipReason);
			}
			string dbName=TestDb.ScratchName("seedorigin");
			try {
				TestDb.Recreate(dbName);
				TestDb.Connect(dbName);
				DataCore.NonQ("CREATE TABLE preference (PrefNum bigint(20) NOT NULL AUTO_INCREMENT,PrefName varchar(255) NOT NULL DEFAULT '',"
					+"ValueString mediumtext NOT NULL,Comments text,PRIMARY KEY (PrefNum)) ENGINE=MyISAM");
				ForkMigrationContext context=new ForkMigrationContext(dbName);
				DataCore.NonQ("INSERT INTO preference (PrefName,ValueString) VALUES ('DataBaseVersion','24.3.63.0')");
				Assert.IsTrue(Seed25_3Step.IsFrom24_3(context));
				DataCore.NonQ("UPDATE preference SET ValueString='99.0.0.0' WHERE PrefName='DataBaseVersion'");
				Assert.IsFalse(Seed25_3Step.IsFrom24_3(context));
				DataCore.NonQ("INSERT INTO preference (PrefName,ValueString) VALUES ('"+ForkVersions.PREF_FORK_ADOPTED_FROM+"','source=25.3.48.0;started=x')");
				Assert.IsFalse(Seed25_3Step.IsFrom24_3(context));
				DataCore.NonQ("UPDATE preference SET ValueString='source=24.3.63.0;started=x' WHERE PrefName='"+ForkVersions.PREF_FORK_ADOPTED_FROM+"'");
				Assert.IsTrue(Seed25_3Step.IsFrom24_3(context));
			}
			finally {
				try {
					TestDb.Drop(dbName);
				}
				catch(Exception) {
					//A later run drops it once it is stale.
				}
			}
		}
	}
}
