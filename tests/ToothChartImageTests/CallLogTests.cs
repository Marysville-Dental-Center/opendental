// Call-log tests of the tooth chart's painting code (package TC-2, docs/specs/wpf-tooth-chart-plan.md, section 4 and section 7 item 5). Each
// scene of CallLogFixtures runs one caller's chart code on synthetic patients in a scratch database, against a target that logs every draw
// call, and the log must equal the one committed in CallLogs\(caller)\(scene).txt. The committed logs were recorded from the callers' own code
// before TC-2 moved it (the temporary harness in Harness\LegacyCallers.cs, a line-for-line copy), so a painter that gives the same logs paints
// as the callers did. OPMS_RECORD_CALL_LOGS=1 writes the logs instead of comparing (a deliberate re-record only).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using ToothChartImageTests.Harness;

namespace ToothChartImageTests {
	[TestClass]
	public class CallLogTests {
		public TestContext TestContext { get; set; }

		[TestMethod]
		public void Fixtures_CoverEveryPaintTypeStatusAndInitialKind() {
			CallLogFixtures.EnsureBuilt();
			List<Procedure> listProcedures=CallLogFixtures.GetAllProcedures(CallLogFixtures.PatNumPaint);
			List<ToothPaintingType> listPaintTypes=listProcedures.Select(x => ProcedureCodes.GetProcCode(x.CodeNum).PaintType).Distinct().ToList();
			foreach(ToothPaintingType toothPaintingType in Enum.GetValues(typeof(ToothPaintingType))) {
				Assert.IsTrue(listPaintTypes.Contains(toothPaintingType),"The Paint patient has no "+toothPaintingType+" procedure.");
				foreach(ProcStat procStat in Enum.GetValues(typeof(ProcStat))) {
					Assert.IsTrue(listProcedures.Any(x => x.ProcStatus==procStat && ProcedureCodes.GetProcCode(x.CodeNum).PaintType==toothPaintingType),
						"The Paint patient has no "+toothPaintingType+" procedure with status "+procStat+".");
				}
			}
			List<ToothInitialType> listInitialTypes=ToothInitials.GetPatientData(CallLogFixtures.PatNumPaint).Select(x => x.InitialType).Distinct().ToList();
			foreach(ToothInitialType toothInitialType in Enum.GetValues(typeof(ToothInitialType))) {
				Assert.IsTrue(listInitialTypes.Contains(toothInitialType),"The Paint patient has no "+toothInitialType+" initial.");
			}
			List<OrthoHardware> listOrthoHardwares=OrthoHardwares.GetPatientData(CallLogFixtures.PatNumOrtho);
			foreach(EnumOrthoHardwareType orthoHardwareType in Enum.GetValues(typeof(EnumOrthoHardwareType))) {
				Assert.IsTrue(listOrthoHardwares.Any(x => x.OrthoHardwareType==orthoHardwareType && !x.IsHidden),"No shown "+orthoHardwareType);
				Assert.IsTrue(listOrthoHardwares.Any(x => x.OrthoHardwareType==orthoHardwareType && x.IsHidden),"No hidden "+orthoHardwareType);
			}
			Assert.AreEqual(2,listOrthoHardwares.Select(x => x.DateExam).Distinct().Count(),"Two ortho exams.");
		}

		#region Today's code (the temporary harness)
		[TestMethod]
		public void ChartModule_TodaysCode_GivesTheCommittedLogs() {
			CheckScenes(CallLogFixtures.CALLER_CHART,"harness",CallLogFixtures.GetChartModuleScenes(),x => x.Name,x => x.User,LegacyControlChart.Run);
		}

		[TestMethod]
		public void TreatPlan_TodaysCode_GivesTheCommittedLogs() {
			CheckScenes(CallLogFixtures.CALLER_TREAT,"harness",CallLogFixtures.GetTreatPlanScenes(),x => x.Name,x => x.User,LegacyControlTreat.Run);
		}

		[TestMethod]
		public void Sheet_TodaysCode_GivesTheCommittedLogs() {
			CheckScenes(CallLogFixtures.CALLER_SHEET,"harness",CallLogFixtures.GetSheetScenes(),x => x.Name,x => x.User,LegacySheetPrinting.Run);
		}

		[TestMethod]
		public void BigChart_TodaysCode_GivesTheCommittedLogs() {
			CheckScenes(CallLogFixtures.CALLER_BIG,"harness",CallLogFixtures.GetBigChartScenes(),x => x.Name,x => x.User,LegacyFormToothChartBig.Run);
		}
		#endregion Today's code (the temporary harness)

		///<summary>Runs every scene and compares its log with the committed one (or writes it when recording). Lists every scene that differs, with
		///its first different line, and writes the call count of each scene to the test output.</summary>
		private void CheckScenes<TScene>(string caller,string source,List<TScene> listScenes,Func<TScene,string> funcName,Func<TScene,CallLogUser> funcUser,
			Func<TScene,CallLog> funcRun)
		{
			CallLogFixtures.EnsureBuilt();
			CallLogFixtures.SetCulture();
			List<string> listProblems=new List<string>();
			int countCalls=0;
			foreach(TScene scene in listScenes) {
				string name=funcName(scene);
				CallLogFixtures.ApplyUser(funcUser(scene));
				CallLog callLog;
				try {
					callLog=funcRun(scene);
				}
				finally {
					CallLogFixtures.ApplyUser(CallLogUser.Plain);
				}
				countCalls+=callLog.Count;
				TestContext.WriteLine(caller+" "+name+" ("+source+"): "+callLog.Count+" calls");
				string text=callLog.ToText(caller,name);
				if(CallLogFiles.IsRecording) {
					CallLogFiles.Write(caller,name,text);
					continue;
				}
				string textCommitted=CallLogFiles.Read(caller,name);
				if(textCommitted==null) {
					listProblems.Add(name+": no committed log at "+CallLogFiles.GetPath(caller,name));
					continue;
				}
				if(text!=textCommitted) {
					listProblems.Add(name+": "+DescribeFirstDifference(textCommitted,text));
				}
			}
			TestContext.WriteLine(caller+" ("+source+"): "+listScenes.Count+" scenes, "+countCalls+" calls");
			Assert.IsTrue(countCalls>0,caller+": no calls at all.");
			Assert.AreEqual(0,listProblems.Count,caller+" ("+source+") differs from the committed call logs:\r\n"+string.Join("\r\n",listProblems));
		}

		private static string DescribeFirstDifference(string expected,string actual) {
			string[] arrayExpected=expected.Split('\n');
			string[] arrayActual=actual.Split('\n');
			for(int i=0;i<Math.Max(arrayExpected.Length,arrayActual.Length);i++) {
				string lineExpected=i<arrayExpected.Length ? arrayExpected[i] : "(end)";
				string lineActual=i<arrayActual.Length ? arrayActual[i] : "(end)";
				if(lineExpected!=lineActual) {
					return "line "+(i+1)+": expected <"+lineExpected+"> but got <"+lineActual+">";
				}
			}
			return "same lines";
		}
	}
}
