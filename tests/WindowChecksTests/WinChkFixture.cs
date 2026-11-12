// The run's set-up and clean-up, and what every check test shares: the opt-in, the scratch copy of the database, recording each check's
// result, and failing the test for a failed check.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WindowChecksTests {
	[TestClass]
	public class WinChkFixture {
		private static bool _hasPrechecked;
		[AssemblyInitialize]
		public static void AssemblyInitialize(TestContext testContext) {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
		}

		[AssemblyCleanup]
		public static void AssemblyCleanup() {
			if(!ScratchDb.IsMade) {
				return;
			}
			try {
				PZTests.RunCleanUp();
			}
			catch(Exception ex) {
				Results.Log("Z failed: "+ex);
			}
			if(Environment.GetEnvironmentVariable("OPMS_WINDOW_CHECKS_KEEP_DB")=="1") {
				Results.Log("Kept "+ScratchDb.Name+" (OPMS_WINDOW_CHECKS_KEEP_DB=1)");
			}
			else {
				try {
					ScratchDb.Drop();
				}
				catch(Exception ex) {
					Results.Log("Drop failed: "+ex.Message);
				}
			}
			//The report, from every result in results.tsv (the last of each check), so the run that ends last leaves the whole report.
			string reportError=Report.Write();
			Results.Log(reportError=="" ? "REPORT.md written" : "REPORT.md not written: "+reportError);
		}

		///<summary>Call first in a check test: skips the test (inconclusive) when the checks are off, the group is not asked for, or the copy of
		///the database cannot be made.</summary>
		public static void Begin(string group) {
			string skipReason=WinChkEnv.GetSkipReason(group);
			if(skipReason!=null) {
				Assert.Inconclusive(skipReason);
			}
			skipReason=ScratchDb.GetSkipReason();
			if(skipReason!=null) {
				Assert.Inconclusive(skipReason);
			}
			if(!_hasPrechecked) {
				_hasPrechecked=true;
				PZTests.RunPrechecks();
			}
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
		}

		///<summary>Records the check's result and fails the test when the check failed. NEEDS-SCREEN, SKIPPED and PARTLY pass the test: the report
		///lists them for an on-screen pass.</summary>
		public static void Finish(CheckResult checkResult) {
			Results.Write(checkResult);
			Console.WriteLine(checkResult.Id+" "+checkResult.Status+": "+checkResult.Summary);
			if(checkResult.Status==CheckStatus.Failed) {
				Assert.Fail(checkResult.Id+" FAILED: "+checkResult.Summary);
			}
		}

		///<summary>Records a check that headless cannot reach, or that the checklist skips.</summary>
		public static void Record(string id,string title,string status,string reason) {
			CheckResult checkResult=new CheckResult(id,title) { StatusForced=status,Reason=reason };
			Finish(checkResult);
		}
	}
}
