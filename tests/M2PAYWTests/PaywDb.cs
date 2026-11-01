// One scratch database for the M2-PAYW tests: a fresh 24.3.63 database (built like tools/New-DevDatabase.ps1) upgraded by the fork migration
// runner (Open Dental 25.3.48 and 26.1 schema, Seed26_1's preference rows). Synthetic data only. Skipped (inconclusive) when the local MariaDB does
// not answer. The database name is unique per run (time, process id, random number) and the database is dropped afterwards.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;
using UnitTestsCore;

namespace M2PAYWTests {
	[TestClass]
	public class PaywDb {
		public static readonly string DbName=TestDb.ScratchName("m2payw");
		///<summary>Null when the database is ready; otherwise why the database tests are skipped.</summary>
		public static string SkipReason;
		///<summary>The income transfer limit preference's value right after the migration (Seed26_1), before any test changes it.</summary>
		public static string SeededLimitValue;

		[AssemblyInitialize]
		public static void AssemblyInitialize(TestContext testContext) {
			SkipReason=TestDb.GetSkipReason();
			if(SkipReason!=null) {
				return;
			}
			try {
				TestDb.DropStaleScratchDatabases();
				TestDb.CreateFresh24_3(DbName);
				ForkMigrationRunner.Run();
				SeededLimitValue=DataCore.GetScalar("SELECT ValueString FROM preference WHERE PrefName='IncomeTransfersLimitToPatPayments'");
				RefreshCaches();
			}
			catch(Exception ex) {
				SkipReason="The scratch database could not be built: "+ex.GetBaseException().Message;
			}
		}

		[AssemblyCleanup]
		public static void AssemblyCleanup() {
			if(Environment.GetEnvironmentVariable("FORKMIGRATION_KEEP_DB")=="1") {
				return;
			}
			try {
				TestDb.Drop(DbName);
			}
			catch(Exception) {
				//A later run drops it once it is stale (TestDb.DropStaleScratchDatabases).
			}
		}

		///<summary>Call from [TestInitialize] of a database test: skips the test when there is no database, otherwise connects to it.</summary>
		public static void Begin() {
			if(SkipReason!=null) {
				Assert.Inconclusive(SkipReason);
			}
			TestDb.Connect(DbName);
			Security.CurUser=Security.CurUser??new Userod();
		}

		public static void RefreshCaches() {
			Cache.Refresh(InvalidType.Prefs,InvalidType.Providers,InvalidType.Defs,InvalidType.ProcCodes,InvalidType.Security);
		}

		///<summary>A made-up patient for one test; each test gets its own family.</summary>
		public static Patient CreatePatient(string suffix) {
			return PatientT.CreatePatient(suffix,lName:"Payw",fName:"Synthetic"+suffix);
		}

		///<summary>Sets the income transfer limit preference and refreshes the preference cache.</summary>
		public static void SetLimit(bool isOn) {
			Prefs.UpdateBool(PrefName.IncomeTransfersLimitToPatPayments,isOn);
			Cache.Refresh(InvalidType.Prefs);
		}
	}
}
