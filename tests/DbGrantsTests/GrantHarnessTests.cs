// The manual test of the server guide's grants for the program's database account (docs/db-connection-security.md section 5,
// docs/specs/db-connection-security.md sections 8 and 9). Run it before the office's switch-over, on the local MariaDB of a development
// computer, with names that exist nowhere on that server:
//
//   set DBGRANTS_ACCOUNT=opmsd_granttest
//   set DBGRANTS_DATABASE=opmsdgrant_od
//   set DBGRANTS_REPORT=C:\Temp\dbgrants-report.txt         (optional; default %TEMP%\dbgrants-report-<time>.txt)
//   set DBGRANTS_SKIP_CHAIN=1                                (optional; skips the Open Dental update chain and the adoption)
//   vstest.console.exe tests\DbGrantsTests\bin\Debug\DbGrantsTests.dll /Platform:x86
//
// Without DBGRANTS_ACCOUNT and DBGRANTS_DATABASE the harness is inconclusive and connects nowhere. With them it connects to 127.0.0.1:3306
// as root (blank password), creates 'DBGRANTS_ACCOUNT'@'localhost' with a random password and exactly the guide's grants, builds the
// office database, runs the program's own code as that account, proves the refusals, and drops the account and every database it made.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DbGrantsTests {
	[TestClass]
	public class GrantHarnessTests {
		public TestContext TestContext { get; set; }

		///<summary>The harness itself. Manual: it runs only when told the account's and the database's names.</summary>
		[TestMethod]
		[TestCategory("Manual")]
		public void GuideGrants_TheProgramRuns_AndEveryOtherRightIsRefused() {
			GrantSettings grantSettings=GrantSettings.FromEnvironment(out string reason);
			if(grantSettings==null) {
				Assert.Inconclusive(reason);
			}
			GrantRun grantRun=new GrantRun(grantSettings);
			string pathReport;
			try {
				pathReport=grantRun.Run();
			}
			catch(GrantPreflightException ex) {
				Assert.Fail("Nothing was created: "+ex.Message);
				return;
			}
			TestContext.WriteLine("Report: "+pathReport);
			TestContext.WriteLine(grantRun.Report.GetSummary());
			Assert.AreEqual(0,grantRun.Report.ListFailures.Count,grantRun.Report.GetSummary()+" Report: "+pathReport+"\r\n"
				+string.Join("\r\n",grantRun.Report.ListFailures));
		}

		///<summary>Without both names nothing runs; names that could touch MariaDB's own accounts and databases, or whose rights MariaDB's own
		///set-up gives every account, are refused.</summary>
		[TestMethod]
		public void Settings_RunOnlyWithBothNames_AndRefuseUnsafeNames() {
			Assert.IsNull(Settings(null,null,out string reason));
			StringAssert.Contains(reason,"Manual test");
			Assert.IsNull(Settings("opmsd_granttest",null,out _));
			Assert.IsNull(Settings(null,"opmsdgrant_od",out _));
			GrantSettings grantSettings=Settings("opmsd_granttest","opmsdgrant_od",out reason);
			Assert.IsNotNull(grantSettings,reason);
			Assert.AreEqual("opmsd_granttest",grantSettings.Account);
			Assert.AreEqual("opmsdgrant_od",grantSettings.Database);
			Assert.IsTrue(grantSettings.DoRunChain);
			foreach(string account in new[] { "root","public","mysql.sys","mariadb.sys","Opmsd","op","9opmsd","opmsd-test","opmsd test","o'x" }) {
				Assert.IsNull(Settings(account,"opmsdgrant_od",out reason),account);
			}
			foreach(string database in new[] { "test","test_od","mysql","sys","information_schema","performance_schema","OD","od","od-x","od`x",
				"a234567890123456789012345678901234567890x" })
			{
				Assert.IsNull(Settings("opmsd_granttest",database,out reason),database);
			}
			Assert.IsNull(Settings("opmsd_granttest","test_od",out reason));
			StringAssert.Contains(reason,"PUBLIC");
		}

		[TestMethod]
		public void GrantPattern_EscapedUnderscore_MatchesOnlyThatName() {
			string pattern=GrantServer.EscapeForGrant("opmsdgrant_od")+"backup\\_%";
			Assert.AreEqual("opmsdgrant\\_odbackup\\_%",pattern);
			Assert.IsTrue(GrantServer.IsMatchGrantPattern(pattern,"opmsdgrant_odbackup_11_02_2026"));
			Assert.IsTrue(GrantServer.IsMatchGrantPattern(pattern,"opmsdgrant_odbackup_11_02_2026_1"));
			Assert.IsFalse(GrantServer.IsMatchGrantPattern(pattern,"opmsdgrant_odbackupx1"));
			Assert.IsFalse(GrantServer.IsMatchGrantPattern(pattern,"opmsdgrantXodbackup_1"));
			Assert.IsFalse(GrantServer.IsMatchGrantPattern(pattern,"opmsdgrant_od"));
			Assert.IsTrue(GrantServer.IsMatchGrantPattern("opmsdgrant_odbackup_%","opmsdgrant_odbackupx1"),"Unescaped, '_' is any character.");
			Assert.IsTrue(GrantServer.IsMatchGrantPattern("test\\_%","test_od"));
			Assert.IsFalse(GrantServer.IsMatchGrantPattern("test\\_%","opmsdgrant_od"));
			Assert.IsTrue(GrantServer.IsMatchGrantPattern("test","TEST"));
			CollectionAssert.AreEqual(new[] { "test","test\\_%" },GrantServer.GetDatabasePatterns(new List<string> {
				"GRANT SELECT, INSERT ON `test`.* TO PUBLIC","GRANT SELECT ON `test\\_%`.* TO PUBLIC","GRANT USAGE ON *.* TO `x`@`localhost`" }));
			Assert.AreEqual("GRANT USAGE ON *.* TO `x`@`localhost` IDENTIFIED BY PASSWORD '<hash>'",
				GrantServer.WithoutPasswordHash("GRANT USAGE ON *.* TO `x`@`localhost` IDENTIFIED BY PASSWORD '*0123456789ABCDEF'"));
			string password=GrantServer.NewPassword();
			Assert.AreEqual(32,password.Length);
			StringAssert.Matches(password,new System.Text.RegularExpressions.Regex("^[A-Za-z0-9]+$"));
		}

		private static GrantSettings Settings(string account,string database,out string reason) {
			return GrantSettings.FromValues(new Dictionary<string,string> {
				{ GrantSettings.ENV_ACCOUNT,account },
				{ GrantSettings.ENV_DATABASE,database },
			},out reason);
		}
	}
}
