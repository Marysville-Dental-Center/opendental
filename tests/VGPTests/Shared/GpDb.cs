// One scratch database for the V-GP database tests: a fresh 24.3.63 database (built like tools/New-DevDatabase.ps1) upgraded to the Open Dental
// 26.1.70 schema by the fork migration runner. Built on first use, dropped when the run ends. Synthetic data only. Skipped (inconclusive) when the
// local MariaDB does not answer.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;

namespace VGPTests {
	public static class GpDb {
		public static readonly string DbName=TestDb.ScratchName("vgp");
		private static readonly object _lock=new object();
		private static bool _isInitialized;
		private static bool _isCreated;
		///<summary>Null when the database is ready; otherwise why the database tests are skipped.</summary>
		private static string _skipReason;

		public static void AssemblyInitialize() {
			//Nothing yet: the database is built by the first database test, so a run of the other tests stays fast.
		}

		public static void AssemblyCleanup() {
			if(!_isCreated || Environment.GetEnvironmentVariable("FORKMIGRATION_KEEP_DB")=="1") {
				return;
			}
			try {
				TestDb.Drop(DbName);
			}
			catch(Exception) {
				//A later run drops it once it is stale (TestDb.DropStaleScratchDatabases).
			}
		}

		///<summary>Call from [TestInitialize] of a database test class: builds the database on first use, skips the test when there is no server,
		///otherwise connects to it and refreshes the caches.</summary>
		public static void Begin() {
			lock(_lock) {
				if(!_isInitialized) {
					_isInitialized=true;
					_skipReason=TestDb.GetSkipReason();
					if(_skipReason==null) {
						try {
							TestDb.DropStaleScratchDatabases();
							TestDb.CreateFresh24_3(DbName);
							_isCreated=true;
							ForkMigrationRunner.Run();
						}
						catch(Exception ex) {
							_skipReason="Could not build the scratch database: "+ex.GetBaseException().Message;
						}
					}
				}
			}
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			TestDb.Connect(DbName);
			Cache.Refresh(InvalidType.Prefs,InvalidType.Providers,InvalidType.Defs,InvalidType.Security,InvalidType.Programs);
			ProgramProperties.RefreshCache();
		}
	}
}
