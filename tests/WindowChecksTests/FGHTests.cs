// Groups F, G and H of the checklist: the dashboard (in process), the reports (in process; G2's RDL file is copied into the scratch copy's A to Z
// folder before the program starts, as the Custom Reports menu is built at start-up, and deleted afterwards), and RdlDesigner (out of process).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WindowChecksTests {
	[TestClass]
	public class FGHTests {
		[TestMethod]
		public void F1toF4_Dashboard() {
			WinChkFixture.Begin("F");
			using(InProcHost inProcHost=InProcHost.Start("chart")) {
				WinChkFixture.Finish(inProcHost.Run("F1","Dashboard: open"));
			}
			string reason="Not scripted in this run: the step goes through the graph cell's right-click context menu (F2, F4) or the dashboard's Setup "
				+"mode and a tab name double-click (F3). Reachable in process through the context menu items' handlers in a later run; for now an on-screen pass.";
			WinChkFixture.Record("F2","Dashboard: edit a cell",CheckStatus.NeedsScreen,reason);
			WinChkFixture.Record("F3","Dashboard: edit a tab",CheckStatus.NeedsScreen,reason);
			WinChkFixture.Record("F4","Dashboard: print preview",CheckStatus.NeedsScreen,reason);
		}

		[TestMethod]
		public void G1_FormReportComplex() {
			WinChkFixture.Begin("G");
			using InProcHost inProcHost=InProcHost.Start("chart");
			WinChkFixture.Finish(inProcHost.Run("G1","FormReportComplex (Appointments report)"));
		}

		[TestMethod]
		public void G2_RdlReport() {
			WinChkFixture.Begin("G");
			string reportFolderName=ScratchDb.Scalar("SELECT ValueString FROM preference WHERE PrefName='ReportFolderName'");
			string folder=Path.Combine(ScratchDb.AtoZFolder,reportFolderName);
			bool isFolderMade=!Directory.Exists(folder);
			Directory.CreateDirectory(folder);
			string path=Path.Combine(folder,"Unsched Treat Plans.rdl");
			File.Copy(Path.Combine(WinChkEnv.RepoRoot,"OpenPmsDental","Resources","Unsched Treat Plans.rdl"),path);
			CheckResult checkResult;
			try {
				using InProcHost inProcHost=InProcHost.Start("chart");
				checkResult=inProcHost.Run("G2","RDL report");
			}
			finally {
				File.Delete(path);
				if(isFolderMade) {
					Directory.Delete(folder);
				}
			}
			checkResult.Note("The .rdl was copied into the scratch copy's A to Z folder's "+reportFolderName+" folder before the start and deleted afterwards"
				+(isFolderMade ? " (with the folder this check made)" : ""));
			checkResult.Expect(!File.Exists(path),"Undo: the copied file is deleted");
			WinChkFixture.Finish(checkResult);
		}

		[TestMethod]
		public void H1H2_RdlDesigner() {
			WinChkFixture.Begin("H");
			CheckResult checkResultH1=new CheckResult("H1","RdlDesigner: open and edit");
			string folderTemp=Path.Combine(Path.GetTempPath(),"stage1-rdl");
			Directory.CreateDirectory(folderTemp);
			string pathCopy=Path.Combine(folderTemp,"Unsched Treat Plans.rdl");
			File.Copy(Path.Combine(WinChkEnv.RepoRoot,"OpenPmsDental","Resources","Unsched Treat Plans.rdl"),pathCopy,true);
			string folderDesigner=Path.Combine(WinChkEnv.RepoRoot,"RdlDesign","bin","Debug");
			string pathState=Path.Combine(folderDesigner,"designerstate.xml");
			bool hadState=File.Exists(pathState);
			try {
				//The copy is named on the command line, but RdlDesigner reads command-line files only while restoring its designerstate.xml, and none
				//exists: it starts empty. File > Open is a file dialog, so opening and editing the copy is for an on-screen pass.
				using ProgramProcess programProcess=ProgramProcess.Start(Path.Combine(folderDesigner,"RdlDesigner.exe"),"\""+pathCopy+"\"");
				IntPtr hwnd=programProcess.WaitForWindow((h,className,title) => className!="#32770" && title!="",TimeSpan.FromSeconds(60));
				if(checkResultH1.Expect(hwnd!=IntPtr.Zero,"RdlDesigner opens ("+Native.GetText(hwnd)+")")) {
					Thread.Sleep(3000);
					string title=Native.GetText(hwnd);
					checkResultH1.Note("Title: "+title+"; message boxes: "+string.Join(" || ",programProcess.ListDialogsSeen));
					checkResultH1.Expect(programProcess.ListDialogsSeen.Count==0,"No message box at the start");
					checkResultH1.AddShot(Shot.Window(hwnd,"H1-designer.png"),"RdlDesigner with the copy open");
					checkResultH1.ForPerson("File > Open of the copy (a file dialog), View > RDL Text, adding a Text box to the page header and File > Save go through RdlDesigner's menus and its "
						+"design surface's mouse work: an on-screen pass");
					double seconds=programProcess.CloseAndTimeExit(hwnd,TimeSpan.FromSeconds(30));
					if(seconds<0) {
						//It may ask to save: answer No, never Save.
						IntPtr hwndAsk=programProcess.GetWindows().FirstOrDefault(x => Native.IsDialogClass(x));
						if(hwndAsk!=IntPtr.Zero) {
							checkResultH1.Note("On close: "+Native.GetDialogMessage(hwndAsk));
							Native.ClickDialogButton(hwndAsk,"No");
						}
						if(!programProcess.Process.WaitForExit(15000)) {
							programProcess.Kill();
							checkResultH1.Fail("RdlDesigner did not exit after WM_CLOSE");
						}
					}
				}
			}
			finally {
				if(!hadState && File.Exists(pathState)) {
					File.Delete(pathState);
					checkResultH1.Note("designerstate.xml, written on close, deleted");
				}
				Directory.Delete(folderTemp,true);
			}
			WinChkFixture.Finish(checkResultH1);
			WinChkFixture.Record("H2","RdlDesigner: preview",CheckStatus.NeedsScreen,"View > Preview (F5) is a menu and key of RdlDesigner's MDI window; not reached "
				+"headless in this run. Its expected error (no data provider configuration beside the designer) is for a person to read.");
		}
	}
}
