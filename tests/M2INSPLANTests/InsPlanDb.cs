// One scratch database for the M2-INSPLAN window tests: a fresh 24.3.63 database (built like tools/New-DevDatabase.ps1) upgraded by the fork
// migration runner, with a unique per-run name, dropped afterwards. Synthetic rows only. Skipped (inconclusive) when the local MariaDB does not
// answer, or when FORKMIGRATION_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Globalization;
using System.Threading;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;

namespace M2INSPLANTests {
	[TestClass]
	public class InsPlanDb {
		public static readonly string DbName=TestDb.ScratchName("m2insplan");
		///<summary>Null when the database is ready; otherwise why the database tests are skipped.</summary>
		public static string SkipReason="not built yet";
		private static bool _isBuilt;
		private static bool _hasTried;
		private static readonly object _lock=new object();

		[AssemblyInitialize]
		public static void AssemblyInitialize(TestContext testContext) {
			//As ProgramEntry does before any window exists: labels draw with GDI, as in the designer, so the window tests and snapshots see the
			//program's text widths (the default, GDI+, is wider and wraps labels the program shows on one line).
			try {
				System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
			}
			catch(InvalidOperationException) {
				//A window was already created in this process; the snapshots then draw labels with GDI+.
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

		///<summary>Call at the start of a database test: builds the database on first use (so the designer tests never wait for it), skips the
		///test when there is none, otherwise connects to it.</summary>
		public static void Begin() {
			lock(_lock) {
				if(!_hasTried) {
					_hasTried=true;
					Build();
				}
			}
			if(SkipReason!=null) {
				Assert.Inconclusive(SkipReason);
			}
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			TestDb.Connect(DbName);
		}

		private static void Build() {
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
				SkipReason=null;
			}
			catch(Exception ex) {
				SkipReason="Could not build the scratch database: "+ex.GetBaseException().Message;
			}
		}
	}
}
