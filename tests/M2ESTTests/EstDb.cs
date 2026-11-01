// One scratch database for the M2-EST database tests: a fresh 24.3.63 database (built like tools/New-DevDatabase.ps1) upgraded to the Open
// Dental 26.1.70 schema by the fork migration runner. Synthetic data only (made-up Z codes). Skipped (inconclusive) when the local MariaDB does
// not answer, or when FORKMIGRATION_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Globalization;
using System.Threading;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;
using UnitTestsCore;

namespace M2ESTTests {
	[TestClass]
	public class EstDb {
		public static readonly string DbName=TestDb.ScratchName("m2est");
		///<summary>Null when the database is ready; otherwise why the database tests are skipped.</summary>
		public static string SkipReason;
		private static bool _isBuilt;
		///<summary>The tests' made-up codes Z7000 to Z7999 are put in the General insurance category, so that annual maximums without a category
		///apply to them as they do to real codes.</summary>
		public const string CODE_FIRST="Z7000";
		public const string CODE_LAST="Z7999";

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
				ForkMigrationRunner.Run();
				Cache.Refresh(InvalidType.AllLocal);
				Security.CurUser=Userods.GetUser(1)??new Userod { UserNum=1 };
				CovCat covCatGeneral=CovCats.GetForEbenCat(EbenefitCategory.General);
				CovSpanT.CreateCovSpan(covCatGeneral.CovCatNum,CODE_FIRST,CODE_LAST);
				_isBuilt=true;
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
			if(Security.CurUser==null) {
				Security.CurUser=Userods.GetUser(1)??new Userod { UserNum=1 };
			}
		}
	}
}
