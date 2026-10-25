// One scratch database for the B-PRINT database tests: a fresh 24.3.63 database (built like tools/New-DevDatabase.ps1) upgraded to the Open
// Dental 25.3.48 schema by the fork migration runner. Synthetic data only. Built the first time a database test class starts, dropped when the
// run ends. Skipped (inconclusive) when the local MariaDB does not answer.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Globalization;
using System.Threading;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;

namespace BPRINTTests {
	[TestClass]
	public class BPrintDb {
		public static readonly string DbName=TestDb.ScratchName("bprint");
		private static readonly object _lock=new object();
		private static bool _isBuildTried;
		///<summary>Null when the database is ready; otherwise why the database tests are skipped.</summary>
		private static string _skipReason;

		///<summary>Builds the database once per run. Call from [ClassInitialize] of a database test class.</summary>
		public static void EnsureBuilt() {
			lock(_lock) {
				if(_isBuildTried) {
					return;
				}
				_isBuildTried=true;
				UseUsEnglish();
				_skipReason=TestDb.GetSkipReason();
				if(_skipReason!=null) {
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
					_skipReason="Could not build the scratch database "+DbName+": "+ex.GetBaseException().Message;
				}
			}
		}

		[AssemblyCleanup]
		public static void AssemblyCleanup() {
			if(!_isBuildTried || Environment.GetEnvironmentVariable("FORKMIGRATION_KEEP_DB")=="1") {
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
			EnsureBuilt();
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			TestDb.Connect(DbName);
		}

		public static void RefreshCaches() {
			Cache.Refresh(InvalidType.Prefs,InvalidType.Defs,InvalidType.Sheets);
		}

		///<summary>Dates and translations are checked as an en-US workstation shows them.</summary>
		public static void UseUsEnglish() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			Thread.CurrentThread.CurrentUICulture=new CultureInfo("en-US");
			CultureInfo.DefaultThreadCurrentCulture=new CultureInfo("en-US");
			CultureInfo.DefaultThreadCurrentUICulture=new CultureInfo("en-US");
		}
	}
}
