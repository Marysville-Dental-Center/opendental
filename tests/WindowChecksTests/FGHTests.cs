// Groups F, G and H of the checklist: the dashboard (in process), the reports (in process; G2's RDL file is copied into the scratch copy's A to Z
// folder before the program starts, as the Custom Reports menu is built at start-up, and deleted afterwards), and RdlDesigner (out of process).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
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
			//F2 and F4 click the graph cell's context menu items by their PerformClick (the right-click menu itself is not shown); F3 runs Setup through
			//its menu item and the tab name's double-click through the tab control's handler.
			List<CheckResult> listCheckResults=new List<CheckResult>();
			using(InProcHost inProcHost=InProcHost.Start("chart")) {
				foreach(string[] arrayCheck in new[] { new[] { "F1","Dashboard: open" },new[] { "F2","Dashboard: edit a cell" },new[] { "F3","Dashboard: edit a tab" },
					new[] { "F4","Dashboard: print preview" } })
				{
					CheckResult checkResult=inProcHost.Run(arrayCheck[0],arrayCheck[1]);
					Results.Write(checkResult);
					listCheckResults.Add(checkResult);
				}
			}
			Assert.AreEqual("",string.Join(" || ",listCheckResults.Where(x => x.Status==CheckStatus.Failed).Select(x => x.Id+": "+x.Summary)));
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
			CheckResult checkResultH2=new CheckResult("H2","RdlDesigner: preview");
			string folderTemp=Path.Combine(Path.GetTempPath(),"stage1-rdl");
			Directory.CreateDirectory(folderTemp);
			string pathCopy=Path.Combine(folderTemp,"Unsched Treat Plans.rdl");
			File.Copy(Path.Combine(WinChkEnv.RepoRoot,"OpenPmsDental","Resources","Unsched Treat Plans.rdl"),pathCopy,true);
			string folderDesigner=Path.Combine(WinChkEnv.RepoRoot,"RdlDesign","bin","Debug");
			string pathState=Path.Combine(folderDesigner,"designerstate.xml");
			bool hadState=File.Exists(pathState);
			string stateBefore=hadState ? File.ReadAllText(pathState) : null;
			try {
				//RdlDesigner opens the files named on its command line only while it restores its designerstate.xml (RdlDesigner.GetStartupState), so an
				//empty state is written beside it first (deleted again below; Z2 checks it is gone). It starts from its own folder.
				if(!hadState) {
					File.WriteAllText(pathState,"<?xml version='1.0' encoding='UTF-8'?><designerstate><CurrentFiles /><RecentFilesMax>5</RecentFilesMax>"
						+"<RecentFiles /></designerstate>");
				}
				using ProgramProcess programProcess=ProgramProcess.Start(Path.Combine(folderDesigner,"RdlDesigner.exe"),"\""+pathCopy+"\"");
				IntPtr hwnd=programProcess.WaitForWindow((h,className,title) => className!="#32770" && title!="",TimeSpan.FromSeconds(60));
				if(checkResultH1.Expect(hwnd!=IntPtr.Zero,"RdlDesigner opens ("+Native.GetText(hwnd)+")")) {
					Thread.Sleep(4000);
					checkResultH1.Expect(programProcess.ListDialogsSeen.Count==0,"No message box at the start",string.Join(" || ",programProcess.ListDialogsSeen));
					//The copy's MDI child (its title names the file) with the layout.
					List<string> listChildTexts=Native.GetChildren(hwnd).Select(x => Native.GetText(x)).Where(x => x!="").ToList();
					checkResultH1.Expect(listChildTexts.Any(x => x.Contains("Unsched Treat Plans.rdl")) || Native.GetText(hwnd).Contains("Unsched Treat Plans.rdl"),
						"The copy is open in the designer (its window names Unsched Treat Plans.rdl)",Native.GetText(hwnd)+"; "+string.Join(", ",listChildTexts.Take(12)));
					checkResultH1.AddShot(Shot.Window(hwnd,"H1-designer.png"),"RdlDesigner with the copy open (Designer view)");
					//View > RDL Text, by the menu item's WM_COMMAND (a classic MainMenu); the XML shows in the editor.
					if(ChooseMenu(hwnd,new[] { "View","RDL Text" },checkResultH1)) {
						Thread.Sleep(2500);
						string textXml=Native.GetChildren(hwnd).Select(x => Native.GetText(x)).FirstOrDefault(x => x.Contains("<Report"))??"";
						checkResultH1.Expect(textXml!="","View > RDL Text shows the XML ("+textXml.Length+" characters, '<Report' found in the editor)");
						checkResultH1.AddShot(Shot.Window(hwnd,"H1-rdltext.png"),"RdlDesigner, View > RDL Text");
						ChooseMenu(hwnd,new[] { "View","Designer" },checkResultH1);
						Thread.Sleep(1500);
					}
					checkResultH1.ForPerson("adding a Text box to the page header (the toolbar's Text, then a click on the design surface) and File > Save are mouse work "
						+"on RdlDesigner's design surface: an on-screen pass");
					//H2: View > Preview (F5), by its WM_COMMAND.
					if(ChooseMenu(hwnd,new[] { "View","Preview" },checkResultH2)) {
						Thread.Sleep(6000);
						checkResultH2.Expect(!programProcess.HasExited,"RdlDesigner is still running after View > Preview (no crash)");
						List<IntPtr> listDialogs=programProcess.GetWindows().Where(x => x!=hwnd).ToList();
						List<string> listShown=new List<string>();
						foreach(IntPtr hwndDialog in listDialogs) {
							string text=Native.IsDialogClass(hwndDialog) ? Native.GetDialogMessage(hwndDialog)
								: string.Join(" ",Native.GetChildren(hwndDialog).Select(x => Native.GetText(x)).Where(x => x!=""));
							listShown.Add("\""+Native.GetText(hwndDialog)+"\" ("+Native.GetClass(hwndDialog)+"): "+text.Replace("\r"," ").Replace("\n"," "));
						}
						string textPreview=string.Join(" ",Native.GetChildren(hwnd).Select(x => Native.GetText(x)).Where(x => x!="" && !x.Contains("<Report")));
						checkResultH2.Note("Windows besides the designer after Preview: "+(listShown.Count==0 ? "none" : string.Join(" || ",listShown)));
						checkResultH2.Note("Texts in the designer's window after Preview: "+(textPreview.Length>600 ? textPreview.Substring(0,600)+"..." : textPreview));
						string all=string.Join(" ",listShown)+" "+textPreview;
						checkResultH2.Expect(!all.Contains("Unhandled exception") && !listDialogs.Any(x => Native.GetText(x).Contains("Unhandled")),
							"No unhandled exception dialog",all);
						checkResultH2.AddShot(Shot.Window(hwnd,"H2-preview.png"),"RdlDesigner, View > Preview");
						if(listDialogs.Count>0) {
							checkResultH2.AddShot(Shot.Window(listDialogs[0],"H2-message.png"),"What the preview showed");
						}
						bool isExpectedError=all.Contains("MySQL") || all.Contains("ODR") || all.Contains("data provider") || all.Contains("DataProvider");
						if(isExpectedError) {
							checkResultH2.Verified("The preview reports the expected missing data provider (MySQL.NET or the ODR connection): its folder has no "
								+"RdlEngineConfig.xml, ODR.dll or MySqlConnector, which the checklist expects and does not count as a failure");
						}
						else {
							checkResultH2.ForPerson("read the preview's messages in H2-preview.png: RdlViewer draws them itself, so no window text holds them (the "
								+"checklist expects an error naming the data provider MySQL.NET or the ODR connection, not a failure)");
						}
						foreach(IntPtr hwndDialog in listDialogs.Where(x => Native.IsDialogClass(x))) {
							if(!Native.ClickDialogButton(hwndDialog,"OK")) {
								Native.CancelDialog(hwndDialog);
							}
						}
						Thread.Sleep(1000);
					}
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
					checkResultH1.Note("Closed in "+seconds.ToString("0.0")+" s; message boxes seen: "+(programProcess.ListDialogsSeen.Count==0 ? "none"
						: string.Join(" || ",programProcess.ListDialogsSeen)));
				}
			}
			finally {
				if(hadState) {
					File.WriteAllText(pathState,stateBefore);
				}
				else if(File.Exists(pathState)) {
					File.Delete(pathState);
				}
				checkResultH1.Note("designerstate.xml: "+(hadState ? "restored as it was" : "written for the start, deleted again"));
				Directory.Delete(folderTemp,true);
			}
			checkResultH2.Expect(!Directory.Exists(folderTemp),"The temp folder stage1-rdl is deleted");
			Results.Write(checkResultH1);
			Results.Write(checkResultH2);
			Assert.AreEqual("",string.Join(" || ",new[] { checkResultH1,checkResultH2 }.Where(x => x.Status==CheckStatus.Failed).Select(x => x.Id+": "+x.Summary)));
		}

		///<summary>Chooses the menu item at the path of the window by its WM_COMMAND (RdlDesigner has a classic MainMenu, whose native HMENU names the
		///items); false (and a failure) when the item is absent.</summary>
		private static bool ChooseMenu(IntPtr hwnd,string[] arrayPath,CheckResult checkResult) {
			int id=Native.FindMenuCommand(hwnd,arrayPath,out bool isDisabled,out string found);
			string path=string.Join(" > ",arrayPath);
			if(!checkResult.Expect(id>0,path+" is in the menu bar",found)) {
				return false;
			}
			checkResult.Expect(!isDisabled,path+" is enabled");
			return checkResult.Expect(Native.PostMenuCommand(hwnd,id),path+" chosen (WM_COMMAND "+id+")");
		}
	}
}
