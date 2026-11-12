// Groups I and J of the checklist: the tooth chart and EHR-0/EHR-1. What this harness reaches runs in process (the chart's handlers, Show
// Features, the EHR setup, the e-mail windows) or out of process (I8's WPF chart start, I11's start-up with no DirectX format); the rest is
// recorded for an on-screen pass with the reason.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WindowChecksTests {
	[TestClass]
	public class IJTests {
		[TestMethod]
		public void I1toI11_ToothChart() {
			WinChkFixture.Begin("I");
			List<CheckResult> listResults=new List<CheckResult>();
			//I1 to I7 in one session (I5's dashboard copy and I6's exam stay until I10; I6's 2D setting until I7).
			using(InProcHost inProcHost=InProcHost.Start("chart")) {
				CheckResult checkResultPrep=inProcHost.Run("IPREP","I prep: D2391 on 30 O");
				foreach(string[] arrayCheck in new[] { new[] { "I1","Refill, slider, Show Big" },new[] { "I2","Save to Images" },
					new[] { "I3","Treatment Plan print preview, classic and sheet" },new[] { "I4","Referral letter" },new[] { "I5","Dashboard widget, open and closed" },
					new[] { "I6","Graphical perio, and the 2D message" },new[] { "I7","Graphics: save DirectX" } })
				{
					CheckResult checkResult=inProcHost.Run(arrayCheck[0],arrayCheck[1],timeoutSeconds:360);
					if(arrayCheck[0]=="I1") {
						ATests.Merge(checkResult,checkResultPrep);
					}
					Results.Write(checkResult);
					listResults.Add(checkResult);
				}
			}
			WinChkFixture.Record("I9","WPF developer switch, the other callers",CheckStatus.Skipped,"Optional, not run (the checklist marks it optional).");
			//I8: the WPF chart, out of process (its memory) and in process (Show Big and the resize).
			CheckResult checkResultI8=new CheckResult("I8","The WPF chart at 100%");
			using(ProgramProcess programProcessWpf=ProgramProcess.Start(ATests.DebugExe,ProgramProcess.ProgramArgs("chart"),
				new System.Collections.Generic.Dictionary<string,string> { { "OPMS_TOOTHCHART_WPF","1" } }))
			{
				IntPtr hwnd=programProcessWpf.WaitForWindow((h,className,title) => className!="#32770" && title.Contains("Gatetest"),TimeSpan.FromSeconds(180));
				if(checkResultI8.Expect(hwnd!=IntPtr.Zero,"The program starts with OPMS_TOOTHCHART_WPF=1 (out of process)")) {
					System.Threading.Thread.Sleep(4000);
					checkResultI8.AddShot(Shot.Window(hwnd,"I8-wpf-chart.png"),"The Chart with the WPF chart");
					programProcessWpf.Process.Refresh();
					checkResultI8.Note("Memory with the Chart open: working set "+programProcessWpf.Process.WorkingSet64/1048576+" MB, private "
						+programProcessWpf.Process.PrivateMemorySize64/1048576+" MB");
					Exits.CloseProgram(programProcessWpf,hwnd,"I8",checkResultI8);
				}
			}
			Environment.SetEnvironmentVariable("OPMS_TOOTHCHART_WPF","1");
			try {
				using InProcHost inProcHost=InProcHost.Start("chart");
				ATests.Merge(checkResultI8,inProcHost.Run("I8BIG","The WPF chart: Show Big and resize"));
			}
			finally {
				Environment.SetEnvironmentVariable("OPMS_TOOTHCHART_WPF",null);
			}
			checkResultI8.ForPerson("the memory after Show Big and after closing it (in process the memory is the test host's)");
			Results.Write(checkResultI8);
			//I10: the program started again (a new session): the sheet editor's preview, undoing I5 and I6.
			using(InProcHost inProcHost=InProcHost.Start("chart")) {
				CheckResult checkResultI10=inProcHost.Run("I10","Sheet editor preview",timeoutSeconds:300);
				Results.Write(checkResultI10);
				listResults.Add(checkResultI10);
			}
			//I11: start-up with no DirectX format saved.
			CheckResult checkResultI11=new CheckResult("I11","Start-up with no DirectX format saved");
			string computerName=Environment.MachineName;
			string formatBefore=ScratchDb.Scalar("SELECT DirectXFormat FROM computerpref WHERE ComputerName='"+computerName+"'");
			ScratchDb.Exec("UPDATE computerpref SET DirectXFormat='' WHERE ComputerName='"+computerName+"'");
			Stopwatch stopwatch=Stopwatch.StartNew();
			IntPtr hwndI11=ATests.StartProgram(ATests.DebugExe,"chart",checkResultI11,out ProgramProcess programProcessI11,null);
			using(programProcessI11) {
				if(hwndI11!=IntPtr.Zero) {
					System.Threading.Thread.Sleep(5000);
					checkResultI11.Note("Start to the main window with the Chart: "+stopwatch.Elapsed.TotalSeconds.ToString("0.0")+" s");
					checkResultI11.AddShot(Shot.Window(hwndI11,"I11.png"),"The Chart after the start-up search");
					Exits.CloseProgram(programProcessI11,hwndI11,"I11",checkResultI11);
				}
			}
			string formatAfter=ScratchDb.Scalar("SELECT DirectXFormat FROM computerpref WHERE ComputerName='"+computerName+"'");
			checkResultI11.Expect(formatAfter!="","The start-up search saved a DirectXFormat ("+formatAfter+")");
			checkResultI11.Note("Before: "+formatBefore+(formatAfter==formatBefore ? " (the same)" : "; restored"));
			if(formatAfter!=formatBefore) {
				ScratchDb.Exec("UPDATE computerpref SET DirectXFormat='"+formatBefore.Replace("'","''")+"' WHERE ComputerName='"+computerName+"'");
			}
			checkResultI11.ForPerson("that the chart starts in DirectX on screen (I11.png; a DirectX surface prints blank while the screen is locked)");
			using(InProcHost inProcHost=InProcHost.Start("chart")) {
				ATests.Merge(checkResultI11,inProcHost.Run("IUNDO","Undo of the prep"));
			}
			Results.Write(checkResultI11);
			listResults.Add(checkResultI8);
			listResults.Add(checkResultI11);
			Assert.AreEqual("",string.Join(" || ",listResults.Where(x => x.Status==CheckStatus.Failed).Select(x => x.Id+": "+x.Summary)));
		}

		[TestMethod]
		public void J1toJ17_Ehr() {
			WinChkFixture.Begin("J");
			List<CheckResult> listResults=new List<CheckResult>();
			CheckResult checkResultJ1;
			using(InProcHost inProcHost=InProcHost.Start("chart")) {
				checkResultJ1=inProcHost.Run("J1","Show Features: EHR on");
			}
			//Exit and start with EHR on: J1's Chart, J2's wizard, J3's SNOMED window and data row, the 0.4 users, J4 to J13 with their log-offs, J15.
			using(InProcHost inProcHost=InProcHost.Start("chart")) {
				CheckResult checkResultChart=inProcHost.Run("JCHART","Chart after the start");
				checkResultJ1.Expect(checkResultChart.ListVerified.Contains("EHR button present"),"Exit, start, log on: the Chart toolbar has EHR");
				ATests.Merge(checkResultJ1,checkResultChart);
				Results.Write(checkResultJ1);
				listResults.Add(checkResultJ1);
				foreach(string[] arrayCheck in new[] { new[] { "J2","Setup Wizard, unsaved" },new[] { "J3","SNOMED tools hidden without rows" } }) {
					CheckResult checkResult=inProcHost.Run(arrayCheck[0],arrayCheck[1]);
					Results.Write(checkResult);
					listResults.Add(checkResult);
				}
				CheckResult checkResultUsers=inProcHost.Run("JUSERS","0.4 test users");
				Results.Log("JUSERS: "+checkResultUsers.Status+": "+checkResultUsers.Summary);
				foreach(string[] arrayCheck in new[] { new[] { "J4","EHR window, no row" },new[] { "J5","EHR setup without Setup" },
					new[] { "J6","SNOMED tools hidden without Setup" },new[] { "J7","Add a provider row" },new[] { "J8","A name no provider has" },
					new[] { "J9","SNOMED tools with Setup; map refused" },new[] { "J10","EHR window with a row" },new[] { "J11","CPOE" },
					new[] { "J12","Change and delete the row; the log" },new[] { "J13","Map to ICD9 with Problem Definition Edit" } })
				{
					CheckResult checkResult;
					if(checkResultUsers.Status==CheckStatus.Failed) {
						checkResult=new CheckResult(arrayCheck[0],arrayCheck[1]);
						checkResult.Fail("The 0.4 test users were not made: "+checkResultUsers.Summary);
					}
					else {
						checkResult=inProcHost.Run(arrayCheck[0],arrayCheck[1]);
						if(arrayCheck[0]=="J4") {
							ATests.Merge(checkResult,checkResultUsers);
						}
					}
					Results.Write(checkResult);
					listResults.Add(checkResult);
				}
				CheckResult checkResultJ15=inProcHost.Run("J15","Direct messaging options");
				Results.Write(checkResultJ15);
				listResults.Add(checkResultJ15);
			}
			//J14: the time check is off, after the log-ons above with EHR on.
			CheckResult checkResultJ14=new CheckResult("J14","Time check off");
			string folderDocuments=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Logger","TimeSync");
			string folderDebug=Path.Combine(WinChkEnv.DebugFolder,"Logger","TimeSync");
			checkResultJ14.Expect(!Directory.Exists(folderDocuments),"No Documents\\Logger\\TimeSync after the log-ons with EHR on (Admin, "
				+InProc.InProcChecks.USER_SETUP+", "+InProc.InProcChecks.USER_NOSETUP+")");
			checkResultJ14.Expect(!Directory.Exists(folderDebug),"No <dbg>\\Logger\\TimeSync");
			string timeSync=ScratchDb.Scalar("SELECT ValueString FROM preference WHERE PrefName='ForkTimeSyncMode'");
			checkResultJ14.Expect(timeSync=="" || timeSync=="0","ForkTimeSyncMode: "+(timeSync=="" ? "no row" : timeSync));
			checkResultJ14.Note("Time Synchronization is never opened; text instead of a picture");
			Results.Write(checkResultJ14);
			listResults.Add(checkResultJ14);
			//J16: EHR off; exit, start: no EHR button.
			CheckResult checkResultJ16;
			using(InProcHost inProcHost=InProcHost.Start("chart")) {
				checkResultJ16=inProcHost.Run("J16","EHR off");
			}
			CheckResult checkResultJ17;
			using(InProcHost inProcHost=InProcHost.Start("chart")) {
				CheckResult checkResultChart=inProcHost.Run("JCHART","Chart after the start");
				checkResultJ16.Expect(checkResultChart.ListVerified.Contains("EHR button absent"),"Exit, start, log on: no EHR button in the Chart");
				ATests.Merge(checkResultJ16,checkResultChart);
				ATests.Merge(checkResultJ16,inProcHost.Run("J16SF","Show Features after the restart"));
				//J17: the Setup Wizard saves EHR on Next.
				checkResultJ17=inProcHost.Run("J17","Setup Wizard saves EHR");
			}
			Results.Write(checkResultJ16);
			listResults.Add(checkResultJ16);
			using(InProcHost inProcHost=InProcHost.Start("chart")) {
				CheckResult checkResultChart=inProcHost.Run("JCHART","Chart after the start");
				checkResultJ17.Expect(checkResultChart.ListVerified.Contains("EHR button present"),"Exit, start, log on: the Chart toolbar has EHR");
				ATests.Merge(checkResultJ17,checkResultChart);
				ATests.Merge(checkResultJ17,inProcHost.Run("J17OFF","Show Features: EHR off again"));
			}
			using(InProcHost inProcHost=InProcHost.Start("chart")) {
				CheckResult checkResultChart=inProcHost.Run("JCHART","Chart after the start");
				checkResultJ17.Expect(checkResultChart.ListVerified.Contains("EHR button absent"),"Exit, start, log on: no EHR button");
				ATests.Merge(checkResultJ17,checkResultChart);
			}
			Results.Write(checkResultJ17);
			listResults.Add(checkResultJ17);
			//0.4's removal: the copy's test users and groups go (the program cannot delete users; in the scratch copy the rows are deleted).
			RemoveTestUsers();
			Assert.AreEqual("",string.Join(" || ",listResults.Where(x => x.Status==CheckStatus.Failed).Select(x => x.Id+": "+x.Summary)));
		}

		///<summary>0.4's removal in the scratch copy: the two test users, their group attaches, the two groups and their permissions. Logged.</summary>
		public static void RemoveTestUsers() {
			string userNums=ScratchDb.Scalar("SELECT GROUP_CONCAT(UserNum) FROM userod WHERE UserName IN ('"+InProc.InProcChecks.USER_SETUP+"','"
				+InProc.InProcChecks.USER_NOSETUP+"')");
			string groupNums=ScratchDb.Scalar("SELECT GROUP_CONCAT(UserGroupNum) FROM usergroup WHERE Description IN ('"+InProc.InProcChecks.GROUP_SETUP+"','"
				+InProc.InProcChecks.GROUP_NOSETUP+"')");
			if(userNums!="") {
				ScratchDb.Exec("DELETE FROM usergroupattach WHERE UserNum IN ("+userNums+")");
				ScratchDb.Exec("DELETE FROM userod WHERE UserNum IN ("+userNums+")");
			}
			if(groupNums!="") {
				ScratchDb.Exec("DELETE FROM grouppermission WHERE UserGroupNum IN ("+groupNums+")");
				ScratchDb.Exec("DELETE FROM usergroup WHERE UserGroupNum IN ("+groupNums+")");
			}
			Results.Log("0.4 removal: users "+(userNums=="" ? "none" : userNums)+", groups "+(groupNums=="" ? "none" : groupNums)+" deleted from the scratch copy");
		}
	}
}
