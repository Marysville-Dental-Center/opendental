// The spike that settled the harness's approach (its findings are in the report): (1) the real Debug OpenPmsDental.exe started out of process
// with its command line, its main window printed from the test process with PrintWindow, a menu reached through UI Automation, and the window
// closed with WM_CLOSE while the exit is timed; (2) the program's assemblies loaded in an AppDomain based on its output folder, the main window
// started as ProgramEntry.Main does, Help > About reached through the real menu item and printed with PrintWindow and DrawToBitmap.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Automation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WindowChecksTests {
	[TestClass]
	public class SpikeTests {
		[TestMethod]
		public void Spike1_OutOfProcess_StartPrintMenuClose() {
			WinChkFixture.Begin("SPIKE");
			CheckResult checkResult=new CheckResult("SPIKE-1","Out of process: start, print, menu by UI Automation, WM_CLOSE");
			using ProgramProcess programProcess=ProgramProcess.Start(Path.Combine(WinChkEnv.DebugFolder,"OpenPmsDental.exe"),ProgramProcess.ProgramArgs("chart"));
			IntPtr hwndMain=programProcess.WaitForWindow((hwnd,className,title) => title.Contains("Gatetest"),TimeSpan.FromSeconds(180));
			checkResult.Note("Start: "+programProcess.StopwatchStart.Elapsed.TotalSeconds.ToString("0.0")+" s; dialogs: "
				+string.Join(" || ",programProcess.ListDialogsSeen));
			if(!checkResult.Expect(hwndMain!=IntPtr.Zero,"The main window shows with patient #1 in its title")) {
				WinChkFixture.Finish(checkResult);
				return;
			}
			System.Threading.Thread.Sleep(3000);
			checkResult.Note("Title: "+Native.GetText(hwndMain)+"; rect "+Native.GetRect(hwndMain)+"; iconic "+Native.IsIconic(hwndMain));
			checkResult.AddShot(Shot.Window(hwndMain,"spike-oop-main.png"),"The main window (PrintWindow from the test process)");
			//UI Automation: Help > About.
			try {
				AutomationElement automationElementMain=AutomationElement.FromHandle(hwndMain);
				AutomationElement automationElementHelp=automationElementMain.FindFirst(TreeScope.Descendants,new AndCondition(
					new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.MenuItem),new PropertyCondition(AutomationElement.NameProperty,"Help")));
				checkResult.Note("UIA Help item: "+(automationElementHelp!=null));
				if(automationElementHelp!=null) {
					((ExpandCollapsePattern)automationElementHelp.GetCurrentPattern(ExpandCollapsePattern.Pattern)).Expand();
					System.Threading.Thread.Sleep(800);
					AutomationElement automationElementAbout=AutomationElement.RootElement.FindFirst(TreeScope.Descendants,new AndCondition(
						new PropertyCondition(AutomationElement.ProcessIdProperty,programProcess.Process.Id),
						new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.MenuItem),new PropertyCondition(AutomationElement.NameProperty,"About")));
					checkResult.Note("UIA About item: "+(automationElementAbout!=null));
					if(automationElementAbout!=null) {
						((InvokePattern)automationElementAbout.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
						IntPtr hwndAbout=programProcess.WaitForWindow((hwnd,className,title) => hwnd!=hwndMain && title.Contains("About"),TimeSpan.FromSeconds(20));
						checkResult.Note("About window by UIA: "+(hwndAbout!=IntPtr.Zero));
						if(hwndAbout!=IntPtr.Zero) {
							checkResult.AddShot(Shot.Window(hwndAbout,"spike-oop-about.png"),"About (out of process, PrintWindow)");
							Native.PostMessage(hwndAbout,Native.WM_CLOSE,IntPtr.Zero,IntPtr.Zero);
							System.Threading.Thread.Sleep(1500);
						}
					}
				}
			}
			catch(Exception ex) {
				checkResult.Note("UIA: "+ex.GetType().Name+": "+ex.Message);
			}
			double seconds=programProcess.CloseAndTimeExit(hwndMain,TimeSpan.FromSeconds(60));
			checkResult.Expect(seconds>=0 && seconds<=20,"WM_CLOSE ends the process within 20 s ("+seconds.ToString("0.0")+" s)");
			WinChkFixture.Finish(checkResult);
		}

		[TestMethod]
		public void Spike2_InProcess_StartAboutDiagnostics() {
			WinChkFixture.Begin("SPIKE");
			using InProcHost inProcHost=InProcHost.Start("chart");
			CheckResult checkResult=inProcHost.Run("SPIKE","In process: AppDomain on the program's folder, Help > About, Diagnostics");
			checkResult.Note("Patched: "+inProcHost.Patched+"; start "+inProcHost.SecondsToStart.ToString("0.0")+" s");
			WinChkFixture.Finish(checkResult);
		}
	}
}
