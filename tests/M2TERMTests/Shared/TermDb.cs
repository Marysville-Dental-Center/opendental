// One scratch database for the M2-TERM database tests: a fresh 24.3.63 database (built like tools/New-DevDatabase.ps1) upgraded by the fork
// migration runner, plus synthetic rows (a made-up procedure code, provider, fee schedule and patient). Skipped (inconclusive) when the local
// MariaDB does not answer, or when FORKMIGRATION_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Globalization;
using System.Threading;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;

namespace M2TERMTests {
	[TestClass]
	public class TermDb {
		public static readonly string DbName=TestDb.ScratchName("m2term");
		///<summary>Null when the database is ready; otherwise why the database tests are skipped.</summary>
		public static string SkipReason;
		public static long CodeNumWithFee;
		public static long CodeNumWithoutFee;
		public static long FeeSchedNum;
		public static long ProvNum;
		public static long PatNum;
		private static bool _isBuilt;

		[AssemblyInitialize]
		public static void AssemblyInitialize(TestContext testContext) {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			SkipReason=TestDb.GetSkipReason();
			if(SkipReason!=null) {
				return;
			}
			try {
				TestDb.DropStaleScratchDatabases();
				TestDb.CreateFresh24_3(DbName);
				_isBuilt=true;
				ForkMigrationRunner.Run();
				Cache.Refresh(InvalidType.AllLocal);
				Security.CurUser=Userods.GetUser(1)??new Userod { UserNum=1 };
				SeedSyntheticRows();
			}
			catch(Exception ex) {
				SkipReason="Could not build the scratch database: "+ex.GetBaseException().Message;
			}
		}

		[AssemblyCleanup]
		public static void AssemblyCleanup() {
			if(!_isBuilt || Environment.GetEnvironmentVariable("FORKMIGRATION_KEEP_DB")=="1") {
				return;
			}
			try {
				TestDb.Drop(DbName);
			}
			catch(Exception) {
				//A later run drops it once it is stale (TestDb.DropStaleScratchDatabases).
			}
		}

		///<summary>Call from [TestInitialize]: skips the test when there is no database, otherwise connects to it.</summary>
		public static void Begin() {
			if(SkipReason!=null) {
				Assert.Inconclusive(SkipReason);
			}
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			TestDb.Connect(DbName);
		}

		///<summary>Made-up codes (not real code-set codes), a provider whose fee schedule has a fee for one of them, and a patient.</summary>
		private static void SeedSyntheticRows() {
			CodeNumWithFee=OpenPmsDentalBusiness.Crud.ProcedureCodeCrud.Insert(new ProcedureCode { ProcCode="ZTRM1",Descript="Terminology test with fee",
				AbbrDesc="ZT1",ProcTime="/X/",TreatArea=TreatmentArea.Mouth });
			CodeNumWithoutFee=OpenPmsDentalBusiness.Crud.ProcedureCodeCrud.Insert(new ProcedureCode { ProcCode="ZTRM2",Descript="Terminology test no fee",
				AbbrDesc="ZT2",ProcTime="/X/",TreatArea=TreatmentArea.Mouth });
			FeeSchedNum=OpenPmsDentalBusiness.Crud.FeeSchedCrud.Insert(new FeeSched { Description="Terminology test standard",
				FeeSchedType=FeeScheduleType.Normal,ItemOrder=90 });
			ProvNum=OpenPmsDentalBusiness.Crud.ProviderCrud.Insert(new Provider { Abbr="ZTRM",LName="Term",FName="Test",ItemOrder=90,FeeSched=FeeSchedNum });
			OpenPmsDentalBusiness.Crud.FeeCrud.Insert(new Fee { CodeNum=CodeNumWithFee,FeeSched=FeeSchedNum,Amount=200,ClinicNum=0,ProvNum=0 });
			PatNum=OpenPmsDentalBusiness.Crud.PatientCrud.Insert(new Patient { LName="Termtest",FName="Pat",PatStatus=PatientStatus.Patient,
				PriProv=ProvNum,Birthdate=new DateTime(1980,1,1) });
			Prefs.UpdateInt(PrefName.InsBlueBookUcrFeePercent,80);
			Cache.Refresh(InvalidType.Prefs,InvalidType.ProcCodes,InvalidType.Providers,InvalidType.FeeScheds);
		}
	}
}
