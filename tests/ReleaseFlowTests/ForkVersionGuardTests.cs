// The startup matrix of the version guard (backlog M1-03 test approach), as pure decisions over the stored version preferences and markers.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace ReleaseFlowTests {
	///<summary>A runner with fork migration steps, as M1-01 will provide.</summary>
	internal class FakeRunner:IForkMigrationRunner {
		public string Latest;
		public int CountRuns;

		public FakeRunner(string latest) {
			Latest=latest;
		}

		public string LatestLevel {
			get { return Latest; }
		}

		public int CompareLevels(string levelA,string levelB) {
			return BaselineForkMigrationRunner.CompareLevelsDefault(levelA,levelB);
		}

		public void RunPending(Action<string> actionLog) {
			CountRuns++;
			actionLog("fake step to "+Latest);
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

		private static ForkDbState Adopted(string schemaLevel="0") {
			ForkDbState forkDbState=Unadopted("99.0.0.0");
			forkDbState.ForkSchemaLevel=schemaLevel;
			forkDbState.OdSchemaCompat="25.3.48";
			forkDbState.ForkAdoptedFrom="source=25.3.48.0;started=2026-10-19T00:00:00Z;adopted=2026-10-19T00:01:00Z";
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
			forkGuardResult=ForkVersionGuard.Evaluate(Adopted("2"),new FakeRunner("10"));
			Assert.AreEqual(ForkGuardAction.ProceedFork,forkGuardResult.Action);
			Assert.IsTrue(forkGuardResult.HasPendingMigrations,"Level 2 is below 10, numerically.");
			Assert.IsFalse(ForkVersionGuard.Evaluate(Adopted("10"),new FakeRunner("10")).HasPendingMigrations);
		}

		[TestMethod]
		public void Adopted_ByANewerBuild_NeedsTheNewerBuild() {
			ForkGuardResult forkGuardResult=ForkVersionGuard.Evaluate(Adopted("12"),new FakeRunner("10"));
			Assert.AreEqual(ForkGuardAction.NeedsNewerBuild,forkGuardResult.Action);
			Assert.AreEqual(ForkVersionGuard.EXIT_CODE_NEWER_SCHEMA,forkGuardResult.ExitCode);
			AssertNoDowngradeOffer(forkGuardResult);
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
		}

		[TestMethod]
		public void BaselineRunner_HasNoSteps() {
			Assert.IsFalse(ForkMigrationHandOff.HasSteps(_runnerBaseline));
			Assert.IsTrue(ForkMigrationHandOff.HasSteps(new FakeRunner("1")));
			Assert.IsNotNull(ForkMigrationHandOff.Runner,"Discovery always yields a runner.");
		}
	}
}
