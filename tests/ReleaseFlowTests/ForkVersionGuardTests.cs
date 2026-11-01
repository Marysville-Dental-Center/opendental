// The startup matrix of the version guard (backlog M1-03 test approach), as pure decisions over the stored version preferences and markers.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace ReleaseFlowTests {
	///<summary>A runner with fork migration steps, as M1-01 provides: the levels of its steps, ascending. Its RunPending only counts and logs.
	///</summary>
	internal class FakeRunner:IForkMigrationRunner {
		public List<string> ListLevels;
		public int CountRuns;

		public FakeRunner(params string[] arrayLevels) {
			ListLevels=arrayLevels.ToList();
		}

		public string LatestLevel {
			get { return ListLevels.Count==0 ? ForkVersions.SCHEMA_LEVEL_BASELINE : ListLevels[ListLevels.Count-1]; }
		}

		public List<string> GetLevels() {
			return ListLevels.ToList();
		}

		public int CompareLevels(string levelA,string levelB) {
			return BaselineForkMigrationRunner.CompareLevelsDefault(levelA,levelB);
		}

		public void RunPending(Action<string> actionLog) {
			CountRuns++;
			actionLog("fake step to "+LatestLevel);
		}
	}

	[TestClass]
	public class ForkVersionGuardTests {
		private static readonly IForkMigrationRunner _runnerBaseline=new BaselineForkMigrationRunner();

		private static ForkDbState Unadopted(string version,string programVersion=null) {
			ForkDbState forkDbState=new ForkDbState();
			forkDbState.DataBaseVersion=version;
			forkDbState.ProgramVersion=programVersion??version;
			return forkDbState;
		}

		///<summary>An adopted database at ForkSchemaLevel schemaLevel whose ForkMigrationsApplied is applied (null: no such row, as right after
		///adoption).</summary>
		private static ForkDbState Adopted(string schemaLevel="0",string applied=null) {
			ForkDbState forkDbState=Unadopted("99.0.0.0");
			forkDbState.ForkSchemaLevel=schemaLevel;
			forkDbState.OdSchemaCompat="25.3.48";
			forkDbState.ForkAdoptedFrom="source=25.3.48.0;started=2026-10-19T00:00:00Z;adopted=2026-10-19T00:01:00Z";
			forkDbState.ForkMigrationsApplied=applied;
			return forkDbState;
		}

		private static void AssertNoDowngradeOffer(ForkGuardResult forkGuardResult) {
			Assert.IsFalse(forkGuardResult.Message.ToLowerInvariant().Contains("downgrade"),"No downgrade is ever offered: "+forkGuardResult.Message);
		}

		[TestMethod]
		public void Fresh_24_3_63_Proceeds_OnTheCompatibilityLine() {
			ForkGuardResult forkGuardResult=ForkVersionGuard.Evaluate(Unadopted("24.3.63.0"),_runnerBaseline);
			Assert.AreEqual(ForkGuardAction.ProceedCompat,forkGuardResult.Action);
			Assert.IsTrue(forkGuardResult.CanProceed);
			Assert.AreEqual(0,forkGuardResult.ExitCode);
		}

		[TestMethod]
		public void Older_Database_Proceeds_ForTheFrozenLegacyChain() {
			Assert.AreEqual(ForkGuardAction.ProceedCompat,ForkVersionGuard.Evaluate(Unadopted("21.1.9.0"),_runnerBaseline).Action);
		}

		[TestMethod]
		public void Fresh_24_3_63_NeedsAdoption_OnceTheRunnerHasSteps() {
			ForkGuardResult forkGuardResult=ForkVersionGuard.Evaluate(Unadopted("24.3.63.0"),new FakeRunner("5"));
			Assert.AreEqual(ForkGuardAction.NeedsAdoption,forkGuardResult.Action);
			Assert.AreEqual(ForkVersionGuard.EXIT_CODE_NEEDS_ADOPTION,forkGuardResult.ExitCode);
		}

		[TestMethod]
		public void OfficeCopy_25_3_48_IsRefused_AndOffersAdoptionOnly() {
			ForkGuardResult forkGuardResult=ForkVersionGuard.Evaluate(Unadopted("25.3.48.0"),_runnerBaseline);
			Assert.AreEqual(ForkGuardAction.NeedsAdoption,forkGuardResult.Action);
			Assert.IsFalse(forkGuardResult.CanProceed,"Normal startup never adopts or converts an Open Dental database.");
			Assert.IsFalse(forkGuardResult.IsAdoptionResume);
			AssertNoDowngradeOffer(forkGuardResult);
		}

		[TestMethod]
		public void OtherOpenDentalVersions_AreRefused() {
			foreach(string version in new[] { "25.3.50.0","25.4.0.0","24.4.10.0","26.1.70.0","25.3.47.0" }) {
				ForkGuardResult forkGuardResult=ForkVersionGuard.Evaluate(Unadopted(version),new FakeRunner("3"));
				Assert.AreEqual(ForkGuardAction.Refuse,forkGuardResult.Action,version);
				Assert.AreEqual(ForkVersionGuard.EXIT_CODE_UNSUPPORTED_VERSION,forkGuardResult.ExitCode,version);
				StringAssert.Contains(forkGuardResult.Message,"25.3.48");
				AssertNoDowngradeOffer(forkGuardResult);
			}
		}

		[TestMethod]
		public void Adopted_Proceeds_AndReportsPendingSteps() {
			ForkGuardResult forkGuardResult=ForkVersionGuard.Evaluate(Adopted("0"),_runnerBaseline);
			Assert.AreEqual(ForkGuardAction.ProceedFork,forkGuardResult.Action);
			Assert.IsFalse(forkGuardResult.HasPendingMigrations);
			forkGuardResult=ForkVersionGuard.Evaluate(Adopted("2","2"),new FakeRunner("2","10"));
			Assert.AreEqual(ForkGuardAction.ProceedFork,forkGuardResult.Action);
			Assert.IsTrue(forkGuardResult.HasPendingMigrations,"Step 10 has not run.");
			CollectionAssert.AreEqual(new[] { "10" },forkGuardResult.ListPendingLevels,"Levels compare numerically: 2 is applied, 10 is not.");
			Assert.AreEqual("",forkGuardResult.Message);
			Assert.AreEqual(0,forkGuardResult.ExitCode);
			forkGuardResult=ForkVersionGuard.Evaluate(Adopted("10","2,10"),new FakeRunner("2","10"));
			Assert.AreEqual(ForkGuardAction.ProceedFork,forkGuardResult.Action);
			Assert.IsFalse(forkGuardResult.HasPendingMigrations);
			Assert.IsFalse(ForkVersionGuard.Evaluate(Adopted("10"," 2 , 10 ,"),new FakeRunner("2","10")).HasPendingMigrations,"Spaces and a trailing comma are ignored.");
		}

		///<summary>An adopted database right after adoption: ForkSchemaLevel is the baseline and there is no ForkMigrationsApplied yet, so every step
		///of the build is pending, as before.</summary>
		[TestMethod]
		public void FreshAdoption_EveryStepIsPending() {
			ForkGuardResult forkGuardResult=ForkVersionGuard.Evaluate(Adopted("0"),new FakeRunner("3","7","10"));
			Assert.AreEqual(ForkGuardAction.ProceedFork,forkGuardResult.Action);
			Assert.IsTrue(forkGuardResult.HasPendingMigrations);
			CollectionAssert.AreEqual(new[] { "3","7","10" },forkGuardResult.ListPendingLevels);
			CollectionAssert.AreEqual(new[] { "3","7","10" },ForkVersionGuard.Evaluate(Adopted("0",""),new FakeRunner("3","7","10")).ListPendingLevels,
				"An empty list is no list.");
		}

		///<summary>A step whose level is below the database's highest applied level (merged later with a lower level, or an abstract step turned on
		///later) has not run: it is pending, although ForkSchemaLevel already equals the build's latest level. Before review 1 the guard compared only
		///ForkSchemaLevel with the latest level and reported nothing pending, so startup never ran the step.</summary>
		[TestMethod]
		public void LowerStepMissingBelowAHigherAppliedLevel_IsPending() {
			ForkGuardResult forkGuardResult=ForkVersionGuard.Evaluate(Adopted("10","3,10"),new FakeRunner("3","7","10"));
			Assert.AreEqual(ForkGuardAction.ProceedFork,forkGuardResult.Action,forkGuardResult.Message);
			Assert.IsTrue(forkGuardResult.HasPendingMigrations);
			CollectionAssert.AreEqual(new[] { "7" },forkGuardResult.ListPendingLevels);
			Assert.AreEqual(0,forkGuardResult.ListUnknownLevels.Count);
			//An adopted database whose applied list was lost: the runner runs every step again (each is idempotent), so every step is pending.
			CollectionAssert.AreEqual(new[] { "3","7","10" },ForkVersionGuard.Evaluate(Adopted("10"),new FakeRunner("3","7","10")).ListPendingLevels);
		}

		[TestMethod]
		public void Adopted_ByANewerBuild_NeedsTheNewerBuild() {
			ForkGuardResult forkGuardResult=ForkVersionGuard.Evaluate(Adopted("12","10,12"),new FakeRunner("10"));
			Assert.AreEqual(ForkGuardAction.NeedsNewerBuild,forkGuardResult.Action);
			Assert.AreEqual(ForkVersionGuard.EXIT_CODE_NEWER_SCHEMA,forkGuardResult.ExitCode);
			Assert.AreEqual("The database was updated by a newer release of this program (schema level 12; this program knows up to 10).\r\n"
				+"This workstation needs the newer release.",forkGuardResult.Message,"The message of a higher ForkSchemaLevel is unchanged.");
			CollectionAssert.AreEqual(new[] { "12" },forkGuardResult.ListUnknownLevels);
			Assert.IsFalse(forkGuardResult.CanProceed);
			AssertNoDowngradeOffer(forkGuardResult);
			Assert.AreEqual(ForkGuardAction.NeedsNewerBuild,ForkVersionGuard.Evaluate(Adopted("12"),new FakeRunner("10")).Action,"Without an applied list too.");
		}

		///<summary>A level in ForkMigrationsApplied that this build does not have was written by a newer build (a step merged after this build, with a
		///level below the database's highest one), so this workstation needs that build, with the same exit code; the build's own steps are not run.
		///Also a ForkSchemaLevel this build does not have, below its latest level.</summary>
		[TestMethod]
		public void AppliedLevelTheBuildDoesNotHave_NeedsTheNewerBuild() {
			ForkGuardResult forkGuardResult=ForkVersionGuard.Evaluate(Adopted("10","3,5,10"),new FakeRunner("3","7","10"));
			Assert.AreEqual(ForkGuardAction.NeedsNewerBuild,forkGuardResult.Action);
			Assert.AreEqual(ForkVersionGuard.EXIT_CODE_NEWER_SCHEMA,forkGuardResult.ExitCode);
			Assert.IsFalse(forkGuardResult.CanProceed);
			Assert.IsFalse(forkGuardResult.HasPendingMigrations,"Nothing is run: step 7 waits for the newer build.");
			CollectionAssert.AreEqual(new[] { "5" },forkGuardResult.ListUnknownLevels);
			StringAssert.StartsWith(forkGuardResult.Message,"The database was updated by a newer release of this program (fork migration level 5, which this program "
				+"does not have; it knows up to 10).");
			StringAssert.Contains(forkGuardResult.Message,"This workstation needs the newer release.");
			AssertNoDowngradeOffer(forkGuardResult);
			forkGuardResult=ForkVersionGuard.Evaluate(Adopted("5","3,5"),new FakeRunner("3","7","10"));
			Assert.AreEqual(ForkGuardAction.NeedsNewerBuild,forkGuardResult.Action,"ForkSchemaLevel 5 is no level of this build.");
			CollectionAssert.AreEqual(new[] { "5" },forkGuardResult.ListUnknownLevels,"Named once.");
			Assert.AreEqual(ForkGuardAction.NeedsNewerBuild,ForkVersionGuard.Evaluate(Adopted("0","3"),_runnerBaseline).Action,
				"The baseline runner has no steps, so any applied level is a newer build's.");
		}

		[TestMethod]
		public void Adopted_WithMarkersRemoved_IsRefused() {
			ForkDbState forkDbState=Adopted();
			forkDbState.ForkSchemaLevel=null;
			forkDbState.OdSchemaCompat=null;
			forkDbState.ForkAdoptedFrom=null;
			ForkGuardResult forkGuardResult=ForkVersionGuard.Evaluate(forkDbState,_runnerBaseline);
			Assert.AreEqual(ForkGuardAction.Refuse,forkGuardResult.Action);
			Assert.AreEqual(ForkVersionGuard.EXIT_CODE_INCONSISTENT,forkGuardResult.ExitCode);
		}

		[TestMethod]
		public void Adopted_WithOnlySomeMarkersRemoved_IsRefused_NotResumed() {
			//ForkAdoptedFrom still records the finished adoption. Treating this as an unfinished adoption would reset ForkSchemaLevel to the baseline
			//and run every fork migration step again on a migrated database.
			ForkDbState forkDbStateNoLevel=Adopted("7");
			forkDbStateNoLevel.ForkSchemaLevel=null;
			ForkDbState forkDbStateNoLevelNoCompat=Adopted("7");
			forkDbStateNoLevelNoCompat.ForkSchemaLevel=null;
			forkDbStateNoLevelNoCompat.OdSchemaCompat=null;
			ForkDbState forkDbStateNoLevelNoVersions=Adopted("7");
			forkDbStateNoLevelNoVersions.ForkSchemaLevel=null;
			forkDbStateNoLevelNoVersions.DataBaseVersion="25.3.48.0";
			forkDbStateNoLevelNoVersions.ProgramVersion="25.3.48.0";
			foreach(ForkDbState forkDbState in new[] { forkDbStateNoLevel,forkDbStateNoLevelNoCompat,forkDbStateNoLevelNoVersions }) {
				Assert.IsFalse(forkDbState.IsAdoptionIncomplete);
				Assert.IsTrue(forkDbState.IsAdoptedWithMarkersMissing);
				foreach(IForkMigrationRunner runner in new IForkMigrationRunner[] { _runnerBaseline,new FakeRunner("10") }) {
					ForkGuardResult forkGuardResult=ForkVersionGuard.Evaluate(forkDbState,runner);
					Assert.AreEqual(ForkGuardAction.Refuse,forkGuardResult.Action,forkGuardResult.Message);
					Assert.AreEqual(ForkVersionGuard.EXIT_CODE_INCONSISTENT,forkGuardResult.ExitCode);
					Assert.IsFalse(forkGuardResult.IsAdoptionResume);
					Assert.IsFalse(forkGuardResult.Message.Contains("did not finish"),forkGuardResult.Message);
					AssertNoDowngradeOffer(forkGuardResult);
				}
				AdoptionPreflight adoptionPreflight=Adoption.Preflight(forkDbState);
				Assert.IsFalse(adoptionPreflight.CanAdopt,"The adoption tool does not adopt it again.");
				Assert.IsFalse(adoptionPreflight.IsResume);
			}
		}

		[TestMethod]
		public void Adopted_WithVersionPrefsChanged_IsRefused() {
			ForkDbState forkDbState=Adopted();
			forkDbState.DataBaseVersion="25.3.48.0";
			Assert.AreEqual(ForkGuardAction.Refuse,ForkVersionGuard.Evaluate(forkDbState,_runnerBaseline).Action);
			forkDbState=Adopted();
			forkDbState.ProgramVersion="25.4.10.0";
			Assert.AreEqual(ForkGuardAction.Refuse,ForkVersionGuard.Evaluate(forkDbState,_runnerBaseline).Action);
			forkDbState=Adopted();
			forkDbState.OdSchemaCompat=null;
			Assert.AreEqual(ForkGuardAction.Refuse,ForkVersionGuard.Evaluate(forkDbState,_runnerBaseline).Action);
		}

		[TestMethod]
		public void UnfinishedAdoption_NeedsAdoption_AsAResume() {
			ForkDbState forkDbState=Unadopted("99.0.0.0");
			forkDbState.ForkAdoptedFrom="source=25.3.48.0;started=2026-10-19T00:00:00Z";
			ForkGuardResult forkGuardResult=ForkVersionGuard.Evaluate(forkDbState,_runnerBaseline);
			Assert.AreEqual(ForkGuardAction.NeedsAdoption,forkGuardResult.Action);
			Assert.IsTrue(forkGuardResult.IsAdoptionResume);
			//Interrupted after OdSchemaCompat was written but before ForkSchemaLevel: still a resume.
			forkDbState.OdSchemaCompat="25.3.48";
			Assert.IsTrue(forkDbState.IsAdoptionIncomplete);
			Assert.IsTrue(ForkVersionGuard.Evaluate(forkDbState,_runnerBaseline).IsAdoptionResume);
			Assert.IsTrue(Adoption.Preflight(forkDbState).IsResume);
		}

		[TestMethod]
		public void MissingOrBadVersion_IsRefused() {
			Assert.AreEqual(ForkGuardAction.Refuse,ForkVersionGuard.Evaluate(Unadopted(null),_runnerBaseline).Action);
			Assert.AreEqual(ForkGuardAction.Refuse,ForkVersionGuard.Evaluate(Unadopted("not a version"),_runnerBaseline).Action);
		}

		[TestMethod]
		public void NewerProgramVersion_OnACompatDatabase_IsRefused() {
			ForkGuardResult forkGuardResult=ForkVersionGuard.Evaluate(Unadopted("24.3.63.0","25.3.48.0"),_runnerBaseline);
			Assert.AreEqual(ForkGuardAction.Refuse,forkGuardResult.Action);
		}

		[TestMethod]
		public void Versions_AllowlistAndSentinel() {
			Assert.IsTrue(ForkVersions.IsAdoptable(new Version(24,3,63,0)));
			Assert.IsTrue(ForkVersions.IsAdoptable(new Version(25,3,48,0)));
			Assert.IsFalse(ForkVersions.IsAdoptable(new Version(25,3,50,0)));
			Assert.IsFalse(ForkVersions.IsAdoptable(new Version(25,4,0,0)));
			Assert.IsFalse(ForkVersions.IsAdoptable(null));
			Assert.AreEqual(new Version(24,3,63,0),ForkVersions.CompatVersion);
			Assert.IsTrue(ForkVersions.SentinelVersion>new Version(30,0),"No Open Dental release will reach the sentinel.");
			Assert.AreEqual(new Version(25,3,48,0),ForkVersions.ParseVersion("25.3.48"),"Three and four part versions compare equal.");
			Assert.IsNull(ForkVersions.ParseVersion(""));
			Assert.IsTrue(ForkVersions.CompareReleaseVersions("0.1","0.0")>0);
			Assert.IsTrue(ForkVersions.CompareReleaseVersions("0.0","0.0.1")<0);
			Assert.AreEqual(0,ForkVersions.CompareReleaseVersions("0.0","0.0.0.0"));
		}

		[TestMethod]
		public void MarkerNames_AreForkOwned_AndNotOpenDentalPreferences() {
			string[] arrayMarkers=new[] { ForkVersions.PREF_FORK_SCHEMA_LEVEL,ForkVersions.PREF_FORK_PROGRAM_VERSION,ForkVersions.PREF_OD_SCHEMA_COMPAT,
				ForkVersions.PREF_FORK_ADOPTED_FROM };
			CollectionAssert.AreEqual(new[] { "ForkSchemaLevel","ForkProgramVersion","OdSchemaCompat","ForkAdoptedFrom" },arrayMarkers);
			foreach(string marker in arrayMarkers) {
				Assert.IsFalse(Enum.IsDefined(typeof(PrefName),marker),marker+" must not be an Open Dental preference name.");
			}
			//The runner's list of applied levels, which the guard reads too.
			Assert.AreEqual("ForkMigrationsApplied",ForkVersions.PREF_FORK_MIGRATIONS_APPLIED);
			Assert.AreEqual(OpenPmsDentalBusiness.ForkMigrations.ForkMigrationRunner.PrefNameApplied,ForkVersions.PREF_FORK_MIGRATIONS_APPLIED);
			Assert.IsFalse(Enum.IsDefined(typeof(PrefName),ForkVersions.PREF_FORK_MIGRATIONS_APPLIED));
		}

		[TestMethod]
		public void BaselineRunner_HasNoSteps() {
			Assert.IsFalse(ForkMigrationHandOff.HasSteps(_runnerBaseline));
			Assert.AreEqual(0,_runnerBaseline.GetLevels().Count);
			Assert.IsTrue(ForkMigrationHandOff.HasSteps(new FakeRunner("1")));
			Assert.IsNotNull(ForkMigrationHandOff.Runner,"Discovery always yields a runner.");
		}
	}
}
