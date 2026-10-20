// Adoption and the startup matrix on scratch databases with synthetic data (backlog M1-03 test approach). No office data is used.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace ReleaseFlowTests {
	[TestClass]
	public class AdoptionDbTests {
		private const string DB_NAME="releaseflow_adoption";
		private static readonly DateTime _dateTimeUtc=new DateTime(2026,10,19,8,0,0,DateTimeKind.Utc);

		[ClassCleanup]
		public static void ClassCleanup() {
			ScratchDb.Drop(DB_NAME);
		}

		///<summary>A synthetic database at the given Open Dental version, with Open Dental update rows, HQ addresses and two patients of one family.
		///</summary>
		private static void CreateOpenDentalCopy(string version,bool is253) {
			ScratchDb.Create(DB_NAME,is253);
			ScratchDb.SetPref(DB_NAME,"DataBaseVersion",version);
			ScratchDb.SetPref(DB_NAME,"ProgramVersion",version);
			ScratchDb.SetPref(DB_NAME,"CorruptedDatabase","0");
			ScratchDb.SetPref(DB_NAME,"UpdateInProgressOnComputerName","");
			ScratchDb.SetPref(DB_NAME,"RandomPrimaryKeys","0");
			//Synthetic HQ addresses, built from the blocked-host list so no Open Dental host name is written in this file.
			string hostHq=CodeBase.VendorDirect.HqHosts.BlockedDomains[0];
			ScratchDb.SetPref(DB_NAME,"UpdateServerAddress","https://"+hostHq+":1943/Service1.asmx");
			ScratchDb.SetPref(DB_NAME,"WebServiceHQServerURL","https://www."+hostHq+"/x.asmx");
			ScratchDb.SetPref(DB_NAME,"SomeFuturePortalUrl","https://portal."+hostHq+"/office");//Unknown name, HQ host: blanked.
			ScratchDb.SetPref(DB_NAME,"SomeVendorUrl","https://vendor.example/api");//Not HQ: kept.
			ScratchDb.SetPref(DB_NAME,"SomeTemplate","Visit https://"+hostHq+" for help");//Text, not an address: kept.
			ScratchDb.Exec(DB_NAME,"INSERT INTO documentmisc (DateCreated,FileName,DocMiscType,RawBase64) VALUES "
				+"('2026-01-01','UpdateFiles.zip',0,'AAAA'),('2026-01-01','0001',1,'BBBB'),('2026-01-01','0002',1,'CCCC'),"
				+"('2026-01-01','share{x}',2,'DDDD')");
			ScratchDb.Exec(DB_NAME,"INSERT INTO patient (LName,FName,PatStatus,Email) VALUES ('Synthetic','Parent',0,'family@example.test')");
		}

		private static bool HasColumn(string column) {
			return ScratchDb.Scalar(DB_NAME,"SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='"+DB_NAME+"' AND TABLE_NAME='patient' "
				+"AND COLUMN_NAME='"+column+"'")!="0";
		}

		private static bool HasIndex(string index) {
			return ScratchDb.Scalar(DB_NAME,"SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA='"+DB_NAME+"' AND TABLE_NAME='patient' "
				+"AND INDEX_NAME='"+index+"'")!="0";
		}

		private static string CountDocMisc(int type) {
			return ScratchDb.Scalar(DB_NAME,"SELECT COUNT(*) FROM documentmisc WHERE DocMiscType="+type);
		}

		[TestMethod]
		public void OfficeCopy_25_3_48_IsAdopted() {
			CreateOpenDentalCopy("25.3.48.0",is253:true);
			Assert.IsTrue(HasColumn(Adoption.PATIENT_PORTAL_EMAIL_COLUMN));
			Assert.IsTrue(HasIndex(Adoption.PATIENT_PORTAL_EMAIL_INDEX));
			try {
				ScratchDb.Exec(DB_NAME,"INSERT INTO patient (LName,FName,PatStatus,Email) VALUES ('Synthetic','Child',0,'family@example.test')");
				Assert.Fail("Before adoption the unique index rejects a second active patient with the same e-mail.");
			}
			catch(MySqlConnector.MySqlException) {
			}
			ForkGuardResult forkGuardResultBefore=ForkVersionGuard.Evaluate(ForkVersions.ReadState(),new BaselineForkMigrationRunner());
			Assert.AreEqual(ForkGuardAction.NeedsAdoption,forkGuardResultBefore.Action,"Startup refuses the office copy until it is adopted.");
			AdoptionPreflight adoptionPreflight=Adoption.Preflight();
			Assert.IsTrue(adoptionPreflight.CanAdopt,adoptionPreflight.Message);
			FakeRunner fakeRunner=new FakeRunner("3");
			AdoptionResult adoptionResult=Adoption.Run(null,fakeRunner,_dateTimeUtc);
			//The office-only column and index are gone, so family members can share an e-mail again.
			Assert.IsTrue(adoptionResult.WasPortalEmailColumnDropped);
			Assert.IsFalse(HasColumn(Adoption.PATIENT_PORTAL_EMAIL_COLUMN));
			Assert.IsFalse(HasIndex(Adoption.PATIENT_PORTAL_EMAIL_INDEX));
			ScratchDb.Exec(DB_NAME,"INSERT INTO patient (LName,FName,PatStatus,Email) VALUES ('Synthetic','Child',0,'family@example.test')");
			Assert.IsTrue(HasIndex("indexLName"),"Other patient indexes stay.");
			//Open Dental's update rows are deleted; other rows stay.
			Assert.AreEqual(3,adoptionResult.CountUpdateFileRowsDeleted);
			Assert.AreEqual("0",CountDocMisc(0));
			Assert.AreEqual("0",CountDocMisc(1));
			Assert.AreEqual("1",CountDocMisc(2));
			//HQ addresses are blanked; everything else is kept.
			Assert.AreEqual("",ScratchDb.GetPref(DB_NAME,"UpdateServerAddress"));
			Assert.AreEqual("",ScratchDb.GetPref(DB_NAME,"WebServiceHQServerURL"));
			Assert.AreEqual("",ScratchDb.GetPref(DB_NAME,"SomeFuturePortalUrl"));
			Assert.AreEqual("https://vendor.example/api",ScratchDb.GetPref(DB_NAME,"SomeVendorUrl"));
			StringAssert.StartsWith(ScratchDb.GetPref(DB_NAME,"SomeTemplate"),"Visit https://");
			//Sentinel versions and fork markers.
			Assert.AreEqual("99.0.0.0",ScratchDb.GetPref(DB_NAME,"DataBaseVersion"));
			Assert.AreEqual("99.0.0.0",ScratchDb.GetPref(DB_NAME,"ProgramVersion"));
			Assert.AreEqual("0",ScratchDb.GetPref(DB_NAME,"ForkSchemaLevel"));
			Assert.AreEqual("25.3.48",ScratchDb.GetPref(DB_NAME,"OdSchemaCompat"));
			Assert.AreEqual("source=25.3.48.0;started=2026-10-19T08:00:00Z;adopted=2026-10-19T08:00:00Z",ScratchDb.GetPref(DB_NAME,"ForkAdoptedFrom"));
			Assert.AreEqual(1,fakeRunner.CountRuns,"Adoption hands off to the fork migration runner.");
			Assert.IsTrue(adoptionResult.ListLog.Any(x => x.Contains("fake step")));
			//The adopted copy starts in the fork, and stays locked against Open Dental programs.
			ForkGuardResult forkGuardResult=ForkVersionGuard.Evaluate(ForkVersions.ReadState(),new BaselineForkMigrationRunner());
			Assert.AreEqual(ForkGuardAction.ProceedFork,forkGuardResult.Action);
		}

		[TestMethod]
		public void Adoption_IsIdempotent() {
			CreateOpenDentalCopy("25.3.48.0",is253:true);
			Adoption.Run(null,new BaselineForkMigrationRunner(),_dateTimeUtc);
			string prefsBefore=ScratchDb.Scalar(DB_NAME,"SELECT GROUP_CONCAT(CONCAT(PrefName,'=',ValueString) ORDER BY PrefNum SEPARATOR '|') FROM preference");
			AdoptionPreflight adoptionPreflight=Adoption.Preflight();
			Assert.IsFalse(adoptionPreflight.CanAdopt);
			Assert.IsTrue(adoptionPreflight.IsAlreadyAdopted);
			try {
				Adoption.Run(null,new BaselineForkMigrationRunner(),_dateTimeUtc.AddHours(1));
				Assert.Fail("A second run is refused.");
			}
			catch(ApplicationException) {
			}
			Assert.AreEqual(prefsBefore,ScratchDb.Scalar(DB_NAME,"SELECT GROUP_CONCAT(CONCAT(PrefName,'=',ValueString) ORDER BY PrefNum SEPARATOR '|') FROM preference"));
		}

		[TestMethod]
		public void InterruptedAdoption_IsFinishedByRunningItAgain() {
			CreateOpenDentalCopy("25.3.48.0",is253:true);
			//As if the first run stopped after step 5: started record and sentinels, no fork markers yet, column still there.
			ScratchDb.SetPref(DB_NAME,"ForkAdoptedFrom","source=25.3.48.0;started=2026-10-18T07:00:00Z");
			ScratchDb.SetPref(DB_NAME,"DataBaseVersion","99.0.0.0");
			ScratchDb.SetPref(DB_NAME,"ProgramVersion","99.0.0.0");
			ForkGuardResult forkGuardResult=ForkVersionGuard.Evaluate(ForkVersions.ReadState(),new BaselineForkMigrationRunner());
			Assert.AreEqual(ForkGuardAction.NeedsAdoption,forkGuardResult.Action);
			Assert.IsTrue(forkGuardResult.IsAdoptionResume);
			AdoptionPreflight adoptionPreflight=Adoption.Preflight();
			Assert.IsTrue(adoptionPreflight.CanAdopt && adoptionPreflight.IsResume,adoptionPreflight.Message);
			Adoption.Run(null,new BaselineForkMigrationRunner(),_dateTimeUtc);
			Assert.IsFalse(HasColumn(Adoption.PATIENT_PORTAL_EMAIL_COLUMN));
			Assert.AreEqual("25.3.48",ScratchDb.GetPref(DB_NAME,"OdSchemaCompat"));
			Assert.AreEqual("source=25.3.48.0;started=2026-10-18T07:00:00Z;adopted=2026-10-19T08:00:00Z",ScratchDb.GetPref(DB_NAME,"ForkAdoptedFrom"));
		}

		[TestMethod]
		public void UnlistedVersions_AreNeverAdopted_AndNothingChanges() {
			foreach(string version in new[] { "25.3.50.0","25.4.0.0" }) {
				CreateOpenDentalCopy(version,is253:true);
				AdoptionPreflight adoptionPreflight=Adoption.Preflight();
				Assert.IsFalse(adoptionPreflight.CanAdopt,version);
				try {
					Adoption.Run(null,new BaselineForkMigrationRunner(),_dateTimeUtc);
					Assert.Fail("Adoption of "+version+" must throw.");
				}
				catch(ApplicationException) {
				}
				Assert.IsTrue(HasColumn(Adoption.PATIENT_PORTAL_EMAIL_COLUMN),version);
				Assert.AreEqual("1",CountDocMisc(0),version);
				Assert.AreEqual(version,ScratchDb.GetPref(DB_NAME,"DataBaseVersion"));
				Assert.IsNull(ScratchDb.GetPref(DB_NAME,"ForkAdoptedFrom"));
				Assert.AreEqual(ForkGuardAction.Refuse,ForkVersionGuard.Evaluate(ForkVersions.ReadState(),new BaselineForkMigrationRunner()).Action);
			}
		}

		[TestMethod]
		public void Preflight_RefusesAFailedOrRunningUpdate() {
			CreateOpenDentalCopy("25.3.48.0",is253:true);
			ScratchDb.SetPref(DB_NAME,"CorruptedDatabase","1");
			Assert.IsFalse(Adoption.Preflight().CanAdopt);
			ScratchDb.SetPref(DB_NAME,"CorruptedDatabase","0");
			ScratchDb.SetPref(DB_NAME,"UpdateInProgressOnComputerName","WS1");
			Assert.IsFalse(Adoption.Preflight().CanAdopt);
			ScratchDb.SetPref(DB_NAME,"UpdateInProgressOnComputerName","");
			ScratchDb.SetPref(DB_NAME,"ProgramVersion","25.3.50.0");
			Assert.IsFalse(Adoption.Preflight().CanAdopt,"ProgramVersion must equal DataBaseVersion.");
		}

		[TestMethod]
		public void OwnDevDatabase_24_3_63_IsAdopted() {
			CreateOpenDentalCopy("24.3.63.0",is253:false);
			Assert.AreEqual(ForkGuardAction.ProceedCompat,ForkVersionGuard.Evaluate(ForkVersions.ReadState(),new BaselineForkMigrationRunner()).Action);
			AdoptionResult adoptionResult=Adoption.Run(null,new BaselineForkMigrationRunner(),_dateTimeUtc);
			Assert.IsFalse(adoptionResult.WasPortalEmailColumnDropped,"A 24.3.63 database has no office-only column.");
			Assert.AreEqual("24.3.63",ScratchDb.GetPref(DB_NAME,"OdSchemaCompat"));
			Assert.AreEqual("99.0.0.0",ScratchDb.GetPref(DB_NAME,"DataBaseVersion"));
			Assert.AreEqual(ForkGuardAction.ProceedFork,ForkVersionGuard.Evaluate(ForkVersions.ReadState(),new BaselineForkMigrationRunner()).Action);
		}

		[TestMethod]
		public void StartupMatrix_FromTheDatabase() {
			//Adopted copy whose markers were removed afterwards.
			CreateOpenDentalCopy("25.3.48.0",is253:true);
			Adoption.Run(null,new BaselineForkMigrationRunner(),_dateTimeUtc);
			ScratchDb.Exec(DB_NAME,"DELETE FROM preference WHERE PrefName IN ('ForkSchemaLevel','OdSchemaCompat','ForkAdoptedFrom')");
			ForkGuardResult forkGuardResult=ForkVersionGuard.Evaluate(ForkVersions.ReadState(),new BaselineForkMigrationRunner());
			Assert.AreEqual(ForkGuardAction.Refuse,forkGuardResult.Action);
			Assert.AreEqual(ForkVersionGuard.EXIT_CODE_INCONSISTENT,forkGuardResult.ExitCode);
			Assert.IsFalse(Adoption.Preflight().CanAdopt,"Sentinel versions are not on the allowlist.");
			//Copy edited to 25.3.50.
			CreateOpenDentalCopy("25.3.50.0",is253:true);
			Assert.AreEqual(ForkGuardAction.Refuse,ForkVersionGuard.Evaluate(ForkVersions.ReadState(),new BaselineForkMigrationRunner()).Action);
			//Fresh 24.3.63.
			CreateOpenDentalCopy("24.3.63.0",is253:false);
			ForkDbState forkDbState=ForkVersions.ReadState();
			Assert.IsFalse(forkDbState.IsForkMarked);
			Assert.AreEqual("24.3.63.0",forkDbState.DataBaseVersion);
			Assert.IsNull(forkDbState.ForkSchemaLevel);
			Assert.AreEqual(ForkGuardAction.ProceedCompat,ForkVersionGuard.Evaluate(forkDbState,new BaselineForkMigrationRunner()).Action);
			//None of these startups read or removed Open Dental's update rows.
			Assert.AreEqual("1",CountDocMisc(0));
			Assert.AreEqual("2",CountDocMisc(1));
		}

		[TestMethod]
		public void AdoptedCopy_WithSomeMarkersRemoved_IsRefused_AndNeverAdoptedAgain() {
			foreach(string[] arrayRemoved in new[] { new[] { "ForkSchemaLevel" },new[] { "ForkSchemaLevel","OdSchemaCompat" } }) {
				CreateOpenDentalCopy("25.3.48.0",is253:true);
				Adoption.Run(null,new FakeRunner("3"),_dateTimeUtc);
				ScratchDb.SetPref(DB_NAME,"ForkSchemaLevel","3");//As if the fork migration steps had run up to level 3.
				ScratchDb.Exec(DB_NAME,"DELETE FROM preference WHERE PrefName IN ('"+string.Join("','",arrayRemoved)+"')");
				string label=string.Join("+",arrayRemoved)+" removed";
				ForkDbState forkDbState=ForkVersions.ReadState();
				Assert.IsFalse(forkDbState.IsAdoptionIncomplete,label);
				FakeRunner fakeRunner=new FakeRunner("3");
				ForkGuardResult forkGuardResult=ForkVersionGuard.Evaluate(forkDbState,fakeRunner);
				Assert.AreEqual(ForkGuardAction.Refuse,forkGuardResult.Action,label+": "+forkGuardResult.Message);
				Assert.AreEqual(ForkVersionGuard.EXIT_CODE_INCONSISTENT,forkGuardResult.ExitCode,label);
				Assert.IsFalse(forkGuardResult.IsAdoptionResume,label);
				AdoptionPreflight adoptionPreflight=Adoption.Preflight();
				Assert.IsFalse(adoptionPreflight.CanAdopt,label);
				Assert.IsFalse(adoptionPreflight.IsResume,label);
				string prefsBefore=ScratchDb.Scalar(DB_NAME,"SELECT GROUP_CONCAT(CONCAT(PrefName,'=',ValueString) ORDER BY PrefNum SEPARATOR '|') FROM preference");
				try {
					Adoption.Run(null,fakeRunner,_dateTimeUtc.AddHours(1));
					Assert.Fail(label+": adoption must not run again.");
				}
				catch(ApplicationException) {
				}
				Assert.AreEqual(0,fakeRunner.CountRuns,label+": no fork migration step ran again.");
				Assert.IsNull(ScratchDb.GetPref(DB_NAME,"ForkSchemaLevel"),label+": ForkSchemaLevel was not reset to the baseline.");
				Assert.AreEqual(prefsBefore,ScratchDb.Scalar(DB_NAME,"SELECT GROUP_CONCAT(CONCAT(PrefName,'=',ValueString) ORDER BY PrefNum SEPARATOR '|') FROM preference"),label);
			}
		}

		[TestMethod]
		public void Markers_AreWrittenAsPlainPreferenceRows() {
			CreateOpenDentalCopy("24.3.63.0",is253:false);
			Assert.IsNull(ForkVersions.GetMarker("ForkProgramVersion"));
			ForkVersions.SetMarker("ForkProgramVersion","0.0+abc");
			ForkVersions.SetMarker("ForkProgramVersion","0.0+def");
			Assert.AreEqual("0.0+def",ForkVersions.GetMarker("ForkProgramVersion"));
			Assert.AreEqual("1",ScratchDb.Scalar(DB_NAME,"SELECT COUNT(*) FROM preference WHERE PrefName='ForkProgramVersion'"));
		}

		[TestMethod]
		public void HqWebAddress_OnlyWholeAddresses() {
			string hostHq=CodeBase.VendorDirect.HqHosts.BlockedDomains[0];
			Assert.IsTrue(Adoption.IsHqWebAddress("https://www."+hostHq+"/updates/"));
			Assert.IsTrue(Adoption.IsHqWebAddress(" http://"+hostHq+" "));
			Assert.IsFalse(Adoption.IsHqWebAddress("Visit https://"+hostHq));
			Assert.IsFalse(Adoption.IsHqWebAddress("https://vendor.example/"));
			Assert.IsFalse(Adoption.IsHqWebAddress(""));
			Assert.IsFalse(Adoption.IsHqWebAddress(null));
		}

		[TestMethod]
		public void AdoptedFrom_Parsing() {
			Assert.AreEqual(new Version(25,3,48,0),Adoption.ParseAdoptedFromSource("source=25.3.48.0;started=x"));
			Assert.AreEqual("x",Adoption.ParseAdoptedFromField("source=25.3.48.0;started=x","started"));
			Assert.IsNull(Adoption.ParseAdoptedFromField("source=25.3.48.0","adopted"));
			Assert.IsNull(Adoption.ParseAdoptedFromSource(null));
		}
	}
}
