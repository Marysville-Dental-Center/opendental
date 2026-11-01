// One scratch database for the M2-CLAIMFORM database tests: a fresh 24.3.63 database (built like tools/New-DevDatabase.ps1) upgraded by the fork
// migration runner. Synthetic data only. Skipped (inconclusive) when the local MariaDB does not answer.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Globalization;
using System.Threading;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;

namespace M2CLAIMFORMTests {
	[TestClass]
	public class ClaimFormDb {
		public static readonly string DbName=TestDb.ScratchName("m2claimform");
		///<summary>Null when the database is ready; otherwise why the database tests are skipped.</summary>
		public static string SkipReason;

		[AssemblyInitialize]
		public static void AssemblyInitialize(TestContext testContext) {
			UseUsEnglish();
			SkipReason=TestDb.GetSkipReason();
			if(SkipReason!=null) {
				return;
			}
			try {
				TestDb.DropStaleScratchDatabases();
				TestDb.CreateFresh24_3(DbName);
				ForkMigrationRunner.Run();
				Security.CurUser=new Userod { UserNum=1,UserName="synthetic" };
				RefreshCaches();
			}
			catch(Exception ex) {
				SkipReason="Could not build the scratch database "+DbName+": "+ex.GetBaseException().Message;
			}
		}

		[AssemblyCleanup]
		public static void AssemblyCleanup() {
			if(Environment.GetEnvironmentVariable("FORKMIGRATION_KEEP_DB")=="1") {
				return;
			}
			try {
				if(TestDb.DatabaseExists(DbName)) {
					TestDb.Drop(DbName);
				}
			}
			catch(Exception) {
				//A later run drops it once it is stale (TestDb.DropStaleScratchDatabases).
			}
		}

		///<summary>Call from [TestInitialize] of a database test: skips the test when there is no database, otherwise connects to it.</summary>
		public static void Begin() {
			UseUsEnglish();
			if(SkipReason!=null) {
				Assert.Inconclusive(SkipReason);
			}
			TestDb.Connect(DbName);
		}

		public static void RefreshCaches() {
			Cache.Refresh(InvalidType.Prefs,InvalidType.Providers,InvalidType.Defs,InvalidType.ProcCodes,InvalidType.Carriers,InvalidType.ClaimForms);
		}

		///<summary>Amounts and dates are checked as an en-US workstation prints them.</summary>
		public static void UseUsEnglish() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			Thread.CurrentThread.CurrentUICulture=new CultureInfo("en-US");
			CultureInfo.DefaultThreadCurrentCulture=new CultureInfo("en-US");
			CultureInfo.DefaultThreadCurrentUICulture=new CultureInfo("en-US");
		}
	}
}
