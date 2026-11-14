// Group B of the checklist: the main modules. Each module is started out of process (the Debug exe with Module=<name>) and printed under the
// checklist's file name; then one in-process session clicks each module button and reads the module's grids and controls.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WindowChecksTests {
	[TestClass]
	public class BTests {
		private static readonly string[][] _arrayModules=new[] {
			new[] { "B1","Appointments","appt","B1-appts-day.png" },
			new[] { "B2","Family","family","B2-family.png" },
			new[] { "B3","Account","account","B3-account.png" },
			new[] { "B4","Treatment Plan","txplan","B4-tp.png" },
			new[] { "B5","Chart","chart","B5-chart.png" },
			new[] { "B6","Images","images","B6-images.png" },
			new[] { "B7","Manage","manage","B7-manage.png" },
		};

		[TestMethod]
		public void B1toB7_MainModules() {
			WinChkFixture.Begin("B");
			Dictionary<string,CheckResult> dictionaryResults=new Dictionary<string,CheckResult>();
			foreach(string[] arrayModule in _arrayModules) {
				CheckResult checkResult=new CheckResult(arrayModule[0],arrayModule[1]);
				dictionaryResults[arrayModule[0]]=checkResult;
				IntPtr hwndMain=ATests.StartProgram(ATests.DebugExe,arrayModule[2],checkResult,out ProgramProcess programProcess);
				using(programProcess) {
					if(hwndMain!=IntPtr.Zero) {
						checkResult.AddShot(Shot.Window(hwndMain,arrayModule[3]),arrayModule[1]+" started with Module="+arrayModule[2]+" (out of process)");
						checkResult.Expect(programProcess.GetWindows().All(x => !Native.IsDialogClass(x)),"No message box");
						Exits.CloseProgram(programProcess,hwndMain,arrayModule[0],checkResult);
					}
				}
			}
			using(InProcHost inProcHost=InProcHost.Start("")) {
				foreach(string[] arrayModule in _arrayModules) {
					ATests.Merge(dictionaryResults[arrayModule[0]],inProcHost.Run(arrayModule[0],arrayModule[1]));
				}
			}
			List<string> listFailed=new List<string>();
			foreach(CheckResult checkResult in dictionaryResults.Values) {
				Results.Write(checkResult);
				Console.WriteLine(checkResult.Id+" "+checkResult.Status+": "+checkResult.Summary);
				if(checkResult.Status==CheckStatus.Failed) {
					listFailed.Add(checkResult.Id+": "+checkResult.Summary);
				}
			}
			Assert.AreEqual(0,listFailed.Count,string.Join(" || ",listFailed));
		}
	}
}
