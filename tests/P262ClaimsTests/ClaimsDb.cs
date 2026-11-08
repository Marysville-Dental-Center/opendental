// One scratch database for the P262-CLAIMS database tests: a fresh 24.3.63 database (built like tools/New-DevDatabase.ps1) upgraded by the fork
// migration runner to the Open Dental 26.2 schema. Synthetic data only (made-up carrier, provider and patient names). Skipped (inconclusive) when
// the local MariaDB does not answer, or when P262_TEST_DB=skip. Dropped at the end unless P262_KEEP_DB=1.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Globalization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;

namespace P262ClaimsTests {
	[TestClass]
	public class ClaimsDb {
		public static readonly string DbName=ScratchDb.Name("main");
		///<summary>Null when the database is ready; otherwise why the database tests are skipped.</summary>
		public static string SkipReason;
		private static bool _isBuilt;

		[AssemblyInitialize]
		public static void AssemblyInitialize(TestContext testContext) {
			UseUsEnglish();
			SkipReason=ScratchDb.GetSkipReason();
			if(SkipReason!=null) {
				return;
			}
			try {
				ScratchDb.DropStale();
				ScratchDb.CreateFresh24_3(DbName);
				_isBuilt=true;
				ForkMigrationRunner.Run();
				Cache.Refresh(InvalidType.AllLocal);
				Security.CurUser=Userods.GetUser(1)??new Userod { UserNum=1,UserName="synthetic" };
			}
			catch(Exception ex) {
				SkipReason="Could not build the scratch database "+DbName+": "+ex.GetBaseException().Message;
			}
		}

		[AssemblyCleanup]
		public static void AssemblyCleanup() {
			if(_isBuilt) {
				ScratchDb.DropQuietly(DbName);
			}
		}

		///<summary>Call from [TestInitialize] of a database test: skips the test when there is no database, otherwise connects to it.</summary>
		public static void Begin() {
			UseUsEnglish();
			if(SkipReason!=null) {
				Assert.Inconclusive(SkipReason);
			}
			ScratchDb.Connect(DbName);
			Security.CurUser=Userods.GetUser(1)??new Userod { UserNum=1,UserName="synthetic" };
		}

		///<summary>A short unique text for synthetic names.</summary>
		public static string NewSuffix() {
			return Guid.NewGuid().ToString("N").Substring(0,8);
		}

		///<summary>Amounts and dates are checked as an en-US workstation shows them.</summary>
		public static void UseUsEnglish() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			Thread.CurrentThread.CurrentUICulture=new CultureInfo("en-US");
			CultureInfo.DefaultThreadCurrentCulture=new CultureInfo("en-US");
			CultureInfo.DefaultThreadCurrentUICulture=new CultureInfo("en-US");
		}
	}
}
