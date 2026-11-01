// One scratch database for the B-MED database tests: a fresh 24.3.63 database (built like tools/New-DevDatabase.ps1) upgraded to the Open
// Dental 25.3.48 schema by the fork migration runner. Synthetic data only. Skipped (inconclusive) when the local MariaDB does not answer.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;

namespace BMEDTests {
	[TestClass]
	public class BMedDb {
		public static readonly string DbName=TestDb.ScratchName("bmed");
		///<summary>Null when the database is ready; otherwise why the database tests are skipped.</summary>
		public static string SkipReason;
		///<summary>The synthetic user that audit entries are written for.</summary>
		public const long USER_NUM=1;

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
				Security.CurUser=new Userod { UserNum=USER_NUM,UserName="synthetic" };
				Cache.Refresh(InvalidType.Prefs,InvalidType.Diseases,InvalidType.Medications);
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
			Security.CurUser=new Userod { UserNum=USER_NUM,UserName="synthetic" };
		}

		///<summary>Texts are checked as an en-US workstation shows them (Lans.g returns the English text there without reading the database).</summary>
		public static void UseUsEnglish() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			Thread.CurrentThread.CurrentUICulture=new CultureInfo("en-US");
			CultureInfo.DefaultThreadCurrentCulture=new CultureInfo("en-US");
			CultureInfo.DefaultThreadCurrentUICulture=new CultureInfo("en-US");
		}

		///<summary>A unique suffix, so names made by one test never match another test's rows (the seed data may hold rows of its own).</summary>
		public static string Unique() {
			return Guid.NewGuid().ToString("N").Substring(0,8);
		}

		///<summary>Synthetic numbers (patient numbers, codes) handed out in order, so no two tests share one.  One Random for the run: new Random()
		///objects made in the same moment return the same numbers.</summary>
		private static long _numSyntheticLast=770000+new Random().Next(0,1000)*100;

		public static long NextSyntheticNum() {
			return System.Threading.Interlocked.Add(ref _numSyntheticLast,10);
		}

		///<summary>The text of a file in the repository, for the wiring tests that check the window sources (the GUI is not launched).</summary>
		public static string ReadRepoFile(params string[] arrayParts) {
			return File.ReadAllText(TestDb.FindRepoFile(Path.Combine(arrayParts)));
		}
	}
}
