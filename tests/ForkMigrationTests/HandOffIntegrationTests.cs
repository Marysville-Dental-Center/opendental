// The seams between the fork migration runner (M1-01), the registries and seed rows (M1-02) and the version guard and adoption (M1-03).
// Synthetic data only. The database tests are skipped (inconclusive) when the local MariaDB does not answer.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;

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
			//The guard reads the levels of the steps the runner runs, in order; the abstract back-fill step is not among them.
			CollectionAssert.AreEqual(ForkMigrationRunner.GetSteps().Select(x => x.Level.ToString()).ToList(),runner.GetLevels());
			Assert.IsFalse(runner.GetLevels().Contains(OpenPmsDentalBusiness.ForkMigrations.FeatureSteps.NoBillInsBackfillStep.LEVEL.ToString()));
		}

		[TestMethod]
		public void Steps_SeedRowsRunAfterTheSchemaSteps() {
			List<ForkMigrationStep> listSteps=ForkMigrationRunner.GetSteps();
			ForkMigrationStep stepSeed=listSteps.Single(x => x is Seed25_3Step);
			//The 25.3.48 schema steps: the steps of this namespace that are not the seed and not the Open Dental 25.4/26.1 steps (OdParity26_1.cs,
			//M2-01) or the 26.2 step (OdParity26_2.cs, P262-SCHEMA), which come after the seed on purpose.
			List<ForkMigrationStep> listSchemaSteps=listSteps.Where(x => x.GetType().Namespace==typeof(Seed25_3Step).Namespace
				&& x.GetType().Assembly==typeof(Seed25_3Step).Assembly && !(x is Seed25_3Step) && !IsOdParity26_1Step(x) && !(x is OdParity26_2Schema)).ToList();
			Assert.IsTrue(listSchemaSteps.Count>0);
			Assert.IsTrue(listSchemaSteps.All(x => x.Level<stepSeed.Level),"Seed25_3Step must run after every 25.3.48 schema step.");
			//The 25.4 and 26.1 steps add their columns after 25.3 columns, so they must run after every 25.3.48 schema step.
			List<ForkMigrationStep> listSteps26_1=listSteps.Where(x => IsOdParity26_1Step(x)).ToList();
			Assert.AreEqual(3,listSteps26_1.Count);
			Assert.IsTrue(listSteps26_1.All(x => x.Level>listSchemaSteps.Max(y => y.Level)),"The 26.1 steps run after the 25.3.48 schema steps.");
			//The 26.2 step adds its columns after 26.1 columns, so it runs after the 26.1 steps.
			Assert.IsTrue(listSteps.Single(x => x is OdParity26_2Schema).Level>listSteps26_1.Max(x => x.Level),"The 26.2 step runs after the 26.1 steps.");
		}

		private static bool IsOdParity26_1Step(ForkMigrationStep step) {
			return step is OdParity25_4Schema || step is OdParity26_1Schema || step is OdParity26_1ClaimPaymentNote;
		}

		[TestMethod]
		public void Guard_FreshUnadopted24_3NeedsAdoptionNowThatStepsExist() {
			ForkDbState forkDbState=new ForkDbState();
			forkDbState.DataBaseVersion="24.3.63.0";
			forkDbState.ProgramVersion="24.3.63.0";
			Assert.AreEqual(ForkGuardAction.NeedsAdoption,ForkVersionGuard.Evaluate(forkDbState).Action);
		}

		///<summary>A fresh 24.3.63 database adopted with the real runner: markers consistent, nothing pending, the schema at the default reference
		///(Open Dental 26.1.70 since M2-01) and OdSchemaCompat saying so, and the seed
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
				CheckStartupDecidesFromTheAppliedLevels();
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

		///<summary>On the adopted database of Adoption_Fresh24_3WithTheRealRunner, with the real runner: the startup guard decides from
		///ForkMigrationsApplied (combined review 1). A step below the highest applied level that has not run is pending, and the runner startup calls
		///(PrefL.RunForkMigrations runs RunPending when the guard says so) runs exactly it; a level the build does not have asks for the newer build;
		///and right after adoption (ForkSchemaLevel 0, no applied list) every step is pending. The markers are put back after each case.</summary>
		private static void CheckStartupDecidesFromTheAppliedLevels() {
			IForkMigrationRunner runner=ForkMigrationHandOff.Runner;
			List<string> listLevels=runner.GetLevels();
			string appliedAll=ForkVersions.GetMarker(ForkVersions.PREF_FORK_MIGRATIONS_APPLIED);
			string schemaLevel=ForkVersions.GetMarker(ForkVersions.PREF_FORK_SCHEMA_LEVEL);
			Assert.AreEqual(runner.LatestLevel,schemaLevel);
			CollectionAssert.AreEquivalent(listLevels,ForkVersions.ReadState().GetAppliedLevels(),"Every step of the build finished.");
			//1. A lower step missing below the highest applied level: the DXC step (B-SHELL), below the Insurance History step (M2-HIST).
			string levelLower=ForkMigrationRunner.GetSteps().Single(x => x is OpenPmsDentalBusiness.ForkMigrations.FeatureSteps.DxcCreditScoreRemovedStep).Level.ToString();
			Assert.IsTrue(runner.CompareLevels(levelLower,schemaLevel)<0);
			ForkVersions.SetMarker(ForkVersions.PREF_FORK_MIGRATIONS_APPLIED,string.Join(",",listLevels.Where(x => x!=levelLower)));
			ForkGuardResult forkGuardResult=ForkVersionGuard.Evaluate(ForkVersions.ReadState());
			Assert.AreEqual(ForkGuardAction.ProceedFork,forkGuardResult.Action,forkGuardResult.Message);
			Assert.IsTrue(forkGuardResult.HasPendingMigrations,"ForkSchemaLevel is still the latest level, but a step has not run.");
			CollectionAssert.AreEqual(new[] { levelLower },forkGuardResult.ListPendingLevels);
			List<string> listLog=new List<string>();
			runner.RunPending(listLog.Add);
			Assert.IsTrue(listLog.Any(x => x.StartsWith("Fork migrations: 1 step(s) run, "+(listLevels.Count-1)+" already done.")),string.Join("\r\n",listLog));
			Assert.IsTrue(listLog.Any(x => x.StartsWith("Fork migration step "+levelLower+" ")),string.Join("\r\n",listLog));
			Assert.IsFalse(ForkVersionGuard.Evaluate(ForkVersions.ReadState()).HasPendingMigrations);
			CollectionAssert.AreEquivalent(listLevels,ForkVersions.ReadState().GetAppliedLevels());
			Assert.AreEqual(schemaLevel,ForkVersions.GetMarker(ForkVersions.PREF_FORK_SCHEMA_LEVEL));
			//2. An applied level the build does not have (a newer build's step below this build's latest level).
			string levelUnknown="2026110100";
			Assert.IsFalse(listLevels.Contains(levelUnknown));
			ForkVersions.SetMarker(ForkVersions.PREF_FORK_MIGRATIONS_APPLIED,appliedAll+","+levelUnknown);
			forkGuardResult=ForkVersionGuard.Evaluate(ForkVersions.ReadState());
			Assert.AreEqual(ForkGuardAction.NeedsNewerBuild,forkGuardResult.Action);
			Assert.AreEqual(ForkVersionGuard.EXIT_CODE_NEWER_SCHEMA,forkGuardResult.ExitCode);
			CollectionAssert.AreEqual(new[] { levelUnknown },forkGuardResult.ListUnknownLevels);
			ForkVersions.SetMarker(ForkVersions.PREF_FORK_MIGRATIONS_APPLIED,appliedAll);
			//3. Right after adoption: ForkSchemaLevel 0 and no applied list, so every step is pending.
			ForkVersions.SetMarker(ForkVersions.PREF_FORK_SCHEMA_LEVEL,ForkVersions.SCHEMA_LEVEL_BASELINE);
			DataCore.NonQ("DELETE FROM preference WHERE PrefName='"+POut.String(ForkVersions.PREF_FORK_MIGRATIONS_APPLIED)+"'");
			forkGuardResult=ForkVersionGuard.Evaluate(ForkVersions.ReadState());
			Assert.AreEqual(ForkGuardAction.ProceedFork,forkGuardResult.Action,forkGuardResult.Message);
			CollectionAssert.AreEqual(listLevels,forkGuardResult.ListPendingLevels);
			ForkVersions.SetMarker(ForkVersions.PREF_FORK_SCHEMA_LEVEL,schemaLevel);
			ForkVersions.SetMarker(ForkVersions.PREF_FORK_MIGRATIONS_APPLIED,appliedAll);
			Assert.IsFalse(ForkVersionGuard.Evaluate(ForkVersions.ReadState()).HasPendingMigrations);
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
