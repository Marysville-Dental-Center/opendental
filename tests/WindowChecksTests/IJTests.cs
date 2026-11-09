// Groups I and J of the checklist: the tooth chart and EHR-0/EHR-1. What this harness reaches runs in process (the chart's handlers, Show
// Features, the EHR setup, the e-mail windows) or out of process (I8's WPF chart start, I11's start-up with no DirectX format); the rest is
// recorded for an on-screen pass with the reason.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
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
			using(InProcHost inProcHost=InProcHost.Start("chart")) {
				CheckResult checkResultPrep=inProcHost.Run("IPREP","I prep: D2391 on 30 O");
				foreach(string[] arrayCheck in new[] { new[] { "I1","Refill, slider, Show Big" },new[] { "I2","Save to Images" },new[] { "I7","Graphics: save DirectX" } }) {
					CheckResult checkResult=inProcHost.Run(arrayCheck[0],arrayCheck[1]);
					if(arrayCheck[0]=="I1") {
						ATests.Merge(checkResult,checkResultPrep);
					}
					Results.Write(checkResult);
				}
			}
			string reason="Not scripted in this run: ";
			WinChkFixture.Record("I3","Treatment Plan print preview, classic and sheet",CheckStatus.NeedsScreen,reason+"a saved plan, a User Query date change and two "
				+"print previews; reachable in process later.");
			WinChkFixture.Record("I4","Referral letter",CheckStatus.NeedsScreen,reason+"a sheet copy edited in the sheet designer (mouse placement), a referral and the "
				+"Letter drop-down.");
			WinChkFixture.Record("I5","Dashboard widget, open and closed",CheckStatus.NeedsScreen,reason+"Dashboard Setup's grids and the patient dashboard toggle.");
			WinChkFixture.Record("I6","Graphical perio, and the 2D message",CheckStatus.NeedsScreen,reason+"a perio exam and the graphical perio window.");
			WinChkFixture.Record("I9","WPF developer switch, the other callers",CheckStatus.Skipped,"Optional, not run (the checklist marks it optional).");
			WinChkFixture.Record("I10","Sheet editor preview",CheckStatus.NeedsScreen,reason+"it undoes I5 and I6, which were not run.");
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
			Assert.AreEqual("",string.Join(" || ",new[] { checkResultI8,checkResultI11 }.Where(x => x.Status==CheckStatus.Failed).Select(x => x.Id+": "+x.Summary)));
		}

		[TestMethod]
		public void J1toJ17_Ehr() {
			WinChkFixture.Begin("J");
			CheckResult checkResultJ1;
			using(InProcHost inProcHost=InProcHost.Start("chart")) {
				checkResultJ1=inProcHost.Run("J1","Show Features: EHR on");
			}
			CheckResult checkResultJ3;
			CheckResult checkResultJ15;
			using(InProcHost inProcHost=InProcHost.Start("chart")) {
				CheckResult checkResultChart=inProcHost.Run("JCHART","Chart after the start");
				checkResultJ1.Expect(checkResultChart.ListVerified.Contains("EHR button present"),"Exit, start, log on: the Chart toolbar has EHR");
				ATests.Merge(checkResultJ1,checkResultChart);
				checkResultJ3=inProcHost.Run("J3","SNOMED tools hidden without rows");
				checkResultJ15=inProcHost.Run("J15","Direct messaging options");
			}
			Results.Write(checkResultJ1);
			checkResultJ3.Note("The synthetic SNOMED row of J3's [DATA] step is not inserted: J4 to J13, which use it, are not run");
			Results.Write(checkResultJ3);
			Results.Write(checkResultJ15);
			//J14: the time check is off.
			CheckResult checkResultJ14=new CheckResult("J14","Time check off");
			string folderDocuments=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Logger","TimeSync");
			string folderDebug=Path.Combine(WinChkEnv.DebugFolder,"Logger","TimeSync");
			checkResultJ14.Expect(!Directory.Exists(folderDocuments),"No Documents\\Logger\\TimeSync after the log-ons with EHR on");
			checkResultJ14.Expect(!Directory.Exists(folderDebug),"No <dbg>\\Logger\\TimeSync");
			string timeSync=ScratchDb.Scalar("SELECT ValueString FROM preference WHERE PrefName='ForkTimeSyncMode'");
			checkResultJ14.Expect(timeSync=="" || timeSync=="0","ForkTimeSyncMode: "+(timeSync=="" ? "no row" : timeSync));
			checkResultJ14.Note("Time Synchronization is never opened; text instead of a picture");
			Results.Write(checkResultJ14);
			CheckResult checkResultJ16;
			using(InProcHost inProcHost=InProcHost.Start("chart")) {
				checkResultJ16=inProcHost.Run("J16","EHR off");
			}
			using(InProcHost inProcHost=InProcHost.Start("chart")) {
				CheckResult checkResultChart=inProcHost.Run("JCHART","Chart after the start");
				checkResultJ16.Expect(checkResultChart.ListVerified.Contains("EHR button absent"),"Exit, start, log on: no EHR button in the Chart");
				ATests.Merge(checkResultJ16,checkResultChart);
			}
			checkResultJ16.ForPerson("Show Features matches J1-before.png");
			Results.Write(checkResultJ16);
			string reasonUsers="Not scripted in this run: it needs the 0.4 test users (gate-setup, gate-nosetup and their groups) and log-offs between them.";
			foreach(string[] arrayCheck in new[] { new[] { "J4","EHR window, no row" },new[] { "J5","EHR setup without Setup" },new[] { "J6","SNOMED tools hidden without Setup" },
				new[] { "J7","Add a provider row" },new[] { "J8","A name no provider has" },new[] { "J9","SNOMED tools with Setup; map refused" },
				new[] { "J10","EHR window with a row" },new[] { "J11","CPOE" },new[] { "J12","Change and delete the row; the log" },new[] { "J13","Map to ICD9 with Problem Definition Edit" } })
			{
				WinChkFixture.Record(arrayCheck[0],arrayCheck[1],CheckStatus.NeedsScreen,reasonUsers);
			}
			WinChkFixture.Record("J2","Setup Wizard, unsaved",CheckStatus.NeedsScreen,"Not scripted in this run: the Setup Wizard's pages and its information text.");
			WinChkFixture.Record("J17","Setup Wizard saves EHR",CheckStatus.NeedsScreen,"Not scripted in this run: the Setup Wizard's Basic Features page and Next.");
			Assert.AreEqual("",string.Join(" || ",new[] { checkResultJ1,checkResultJ3,checkResultJ14,checkResultJ15,checkResultJ16 }
				.Where(x => x.Status==CheckStatus.Failed).Select(x => x.Id+": "+x.Summary)));
		}
	}
}
