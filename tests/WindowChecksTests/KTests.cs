// Group K of the checklist: the tools. Each exe is started out of process from its own folder (as a double-click starts it), its first window
// printed, and the few steps a check needs are done with UI Automation patterns (AutomationId is the WinForms control's Name) or window
// messages; never SendInput. Connection files are made for the scratch copy and removed afterwards. Help > Update (K10) runs in process.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Automation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WindowChecksTests {
	[TestClass]
	public class KTests {
		private static string Repo(params string[] arrayParts) {
			return Path.Combine(new[] { WinChkEnv.RepoRoot }.Concat(arrayParts).ToArray());
		}

		///<summary>The first visible window of the tool, or zero (message boxes that came first are recorded in the process's list).</summary>
		private static IntPtr WaitForToolWindow(ProgramProcess programProcess,string title,int seconds=60) {
			return programProcess.WaitForWindow((hwnd,className,text) => className!="#32770" && (title==null || text.Contains(title)),TimeSpan.FromSeconds(seconds));
		}

		private static AutomationElement Element(IntPtr hwnd,string automationId) {
			string error;
			AutomationElement automationElement=Uia.Try(() => AutomationElement.FromHandle(hwnd).FindFirst(TreeScope.Descendants,
				new PropertyCondition(AutomationElement.AutomationIdProperty,automationId)),20000,out error);
			return automationElement;
		}

		///<summary>The control's value (ValuePattern, else its window text), read with a timeout; null when it cannot be read.</summary>
		private static string Value(IntPtr hwnd,string automationId) {
			string error;
			return Uia.Try(() => {
				AutomationElement automationElement=AutomationElement.FromHandle(hwnd).FindFirst(TreeScope.Descendants,
					new PropertyCondition(AutomationElement.AutomationIdProperty,automationId));
				if(automationElement==null) {
					return null;
				}
				object pattern;
				if(automationElement.TryGetCurrentPattern(ValuePattern.Pattern,out pattern)) {
					return ((ValuePattern)pattern).Current.Value;
				}
				return Native.GetText(new IntPtr(automationElement.Current.NativeWindowHandle));
			},20000,out error);
		}

		///<summary>Sets the control's text with WM_SETTEXT (a window message: no input), which raises the text box's TextChanged as typing does.</summary>
		private static bool SetText(IntPtr hwnd,string automationId,string text) {
			string error;
			IntPtr hwndControl=Uia.Try(() => {
				AutomationElement automationElement=AutomationElement.FromHandle(hwnd).FindFirst(TreeScope.Descendants,
					new PropertyCondition(AutomationElement.AutomationIdProperty,automationId));
				return automationElement==null ? IntPtr.Zero : new IntPtr(automationElement.Current.NativeWindowHandle);
			},20000,out error);
			if(hwndControl==IntPtr.Zero) {
				return false;
			}
			IntPtr result;
			Native.SendMessageTimeoutString(hwndControl,0x000C,IntPtr.Zero,text,0x0002,5000,out result);//WM_SETTEXT, SMTO_ABORTIFHUNG
			return true;
		}

		private static bool Invoke(IntPtr hwnd,string automationId) {
			string error;
			IntPtr hwndButton=Uia.Try(() => {
				AutomationElement automationElement=AutomationElement.FromHandle(hwnd).FindFirst(TreeScope.Descendants,
					new PropertyCondition(AutomationElement.AutomationIdProperty,automationId));
				return automationElement==null ? IntPtr.Zero : new IntPtr(automationElement.Current.NativeWindowHandle);
			},20000,out error);
			if(hwndButton==IntPtr.Zero) {
				return false;
			}
			//Posted: the handler may open a modal box, which must not block this thread.
			Native.PostMessage(hwndButton,Native.BM_CLICK,IntPtr.Zero,IntPtr.Zero);
			return true;
		}

		///<summary>Waits for a message box of the process and returns its text (and presses the button), or "".</summary>
		private static string WaitForMessage(ProgramProcess programProcess,int seconds,string buttonToPress,CheckResult checkResult,string shot=null) {
			IntPtr hwndDialog=IntPtr.Zero;
			Stopwatch stopwatch=Stopwatch.StartNew();
			while(stopwatch.Elapsed.TotalSeconds<seconds && hwndDialog==IntPtr.Zero) {
				hwndDialog=programProcess.GetWindows().FirstOrDefault(x => Native.IsDialogClass(x));
				if(hwndDialog==IntPtr.Zero) {
					Thread.Sleep(250);
				}
			}
			if(hwndDialog==IntPtr.Zero) {
				return "";
			}
			Thread.Sleep(300);
			string text=Native.GetDialogMessage(hwndDialog);
			if(shot!=null) {
				checkResult.AddShot(Shot.Window(hwndDialog,shot),"The message");
			}
			if(!Native.ClickDialogButton(hwndDialog,buttonToPress)) {
				Native.CancelDialog(hwndDialog);
			}
			Thread.Sleep(500);
			return text;
		}

		private static void Close(ProgramProcess programProcess,IntPtr hwnd,CheckResult checkResult,string name) {
			if(programProcess.HasExited) {
				return;
			}
			double seconds=programProcess.CloseAndTimeExit(hwnd,TimeSpan.FromSeconds(30));
			if(seconds<0) {
				checkResult.Fail(name+" did not exit within 30 s after WM_CLOSE (ended by its PID)");
				programProcess.Kill();
			}
			else {
				checkResult.Verified(name+" closes ("+seconds.ToString("0.0")+" s)");
			}
		}

		[TestMethod]
		public void K1_CentralManager() {
			WinChkFixture.Begin("K");
			CheckResult checkResult=new CheckResult("K1","CentralManager");
			string folder=Repo("CentralManager","bin","Debug");
			string config=ConfigFiles.Write(folder,"CentralManagerConfig.xml");
			try {
				using ProgramProcess programProcess=ProgramProcess.Start(Path.Combine(folder,"CentralManager.exe"),"");
				string text=WaitForMessage(programProcess,90,"OK",checkResult,"K1.png");
				checkResult.Note("Message: "+text.Replace("\r"," ").Replace("\n"," "));
				checkResult.Expect(text.Contains("Database version: 99.0.0.0") && text.Contains("Versions must match"),
					"It connects and stops with the version message (Database version: 99.0.0.0, Versions must match)",text);
				checkResult.Expect(text.Contains("Program version: 24.3.63.0"),"Program version: 24.3.63.0",text);
				checkResult.Note("That is the expected stop: every database of the fork holds the 99.0.0.0 sentinel (a finding for the lead)");
				Thread.Sleep(2000);
				foreach(IntPtr hwnd in programProcess.GetWindows()) {
					checkResult.Note("Window after the message: "+Native.GetText(hwnd));
					Close(programProcess,hwnd,checkResult,"CentralManager");
				}
				if(!programProcess.HasExited && !programProcess.Process.WaitForExit(10000)) {
					programProcess.Kill();
				}
			}
			finally {
				ConfigFiles.Delete(config);
			}
			checkResult.Expect(!File.Exists(config),"The copied CentralManagerConfig.xml is deleted");
			WinChkFixture.Finish(checkResult);
		}

		[TestMethod]
		public void K2_DocumentationBuilder() {
			WinChkFixture.Begin("K");
			CheckResult checkResult=new CheckResult("K2","DocumentationBuilder");
			string statusBefore=Git("status --short DocumentationBuilder");
			if(statusBefore!="") {
				checkResult.Fail("git status --short DocumentationBuilder is not empty before the check: "+statusBefore);
				WinChkFixture.Finish(checkResult);
				return;
			}
			string config=ConfigFiles.Write(WinChkEnv.ReleaseFolder,"FreeDentalConfig.xml");
			string folder=Repo("DocumentationBuilder","bin","Debug");
			try {
				using ProgramProcess programProcess=ProgramProcess.Start(Path.Combine(folder,"DocumentationBuilder.exe"),"");
				Results.Log("K2: started");
				IntPtr hwnd=IntPtr.Zero;
				Stopwatch stopwatch=Stopwatch.StartNew();
				while(stopwatch.Elapsed.TotalSeconds<60 && hwnd==IntPtr.Zero) {
					foreach(IntPtr hwndWindow in programProcess.GetWindows()) {
						string title=Native.GetText(hwndWindow);
						if(title.Contains("Documentation Builder")) {
							hwnd=hwndWindow;
						}
						else if(title!="" && !title.Contains("Documentation")) {
							//The .NET unhandled-exception dialog: read it, Continue.
							checkResult.AddShot(Shot.Window(hwndWindow,"K2-load-exception.png"),"The start-up exception dialog");
							string details=string.Join(" | ",Native.GetChildren(hwndWindow).Select(x => Native.GetText(x)).Where(x => x!="" && x.Length<400));
							checkResult.Note("At start: '"+title+"': "+details);
							if(details.Contains("must be greater than or equal to zero")) {
								checkResult.Note("Pre-existing: the database's DataBaseVersion is the 99.0.0.0 sentinel, so no previous version can be made");
							}
							else {
								checkResult.Fail("Unexpected start-up error: "+details);
							}
							IntPtr hwndContinue=Native.GetChildren(hwndWindow).FirstOrDefault(x => Native.GetText(x).Replace("&","")=="Continue");
							if(hwndContinue!=IntPtr.Zero) {
								Native.PostMessage(hwndContinue,Native.BM_CLICK,IntPtr.Zero,IntPtr.Zero);
							}
							Thread.Sleep(1000);
						}
					}
					Thread.Sleep(300);
				}
				if(!checkResult.Expect(hwnd!=IntPtr.Zero,"'Documentation Builder' opens")) {
					return;
				}
				Results.Log("K2: window "+hwnd);
				string connection=Value(hwnd,"textConnStr")??"";
				Results.Log("K2: connection read");
				checkResult.Expect(connection.Contains(ScratchDb.Name),"It shows its connection string (to the scratch copy; not copied here)");
				if(Value(hwnd,"textVersion")!="24.3.63.0") {
					SetText(hwnd,"textVersion","24.3.63.0");
					SetText(hwnd,"textPrevVersion","24.2.1");
					checkResult.Note("Version 24.3.63.0 and previous version 24.2.1 set (WM_SETTEXT)");
				}
				Results.Log("K2: versions set");
				checkResult.Expect(Invoke(hwnd,"buttonPrefDocumentation"),"Build Pref Documentation pressed");
				Results.Log("K2: Build Pref Documentation pressed");
				string text=WaitForMessage(programProcess,60,"OK",checkResult);
				checkResult.Expect(text.Contains("Pref243.html does not exist. Creating a new file for this version."),"'..\\..\\PreferenceTable\\Pref243.html does not exist. "
					+"Creating a new file for this version.'",text);
				Thread.Sleep(3000);
				string textAfter=WaitForMessage(programProcess,5,"OK",checkResult);
				checkResult.Expect(textAfter=="","No error after it",textAfter);
				checkResult.Expect(File.Exists(Repo("DocumentationBuilder","PreferenceTable","Pref243.html")),"It writes DocumentationBuilder\\PreferenceTable\\Pref243.html");
				Results.Log("K2: after the build");
				checkResult.AddShot(Shot.Window(hwnd,"K2.png"),"Documentation Builder");
				checkResult.ForPerson("Build OD Documentation is not pressed headless: it opens the written XML in the default .xml handler (a browser), "
					+"which may contact the internet; its known unhandled exception (the missing opendental24.2 sibling folder) is not re-checked");
				Close(programProcess,hwnd,checkResult,"DocumentationBuilder");
			}
			finally {
				ConfigFiles.Delete(config);
				string pref=Repo("DocumentationBuilder","PreferenceTable","Pref243.html");
				if(File.Exists(pref)) {
					File.Delete(pref);
				}
				Git("restore DocumentationBuilder/OpenDentalDocumentation.xml");
			}
			string statusAfter=Git("status --short DocumentationBuilder");
			checkResult.Expect(statusAfter=="","Undo: git status --short DocumentationBuilder prints nothing",statusAfter);
			WinChkFixture.Finish(checkResult);
		}

		///<summary>git in the repository of this worktree (plain git, this worktree only), with a timeout.</summary>
		private static string Git(string arguments) {
			ProcessStartInfo processStartInfo=new ProcessStartInfo("git",arguments) { WorkingDirectory=WinChkEnv.RepoRoot,UseShellExecute=false,
				RedirectStandardOutput=true,RedirectStandardError=true,RedirectStandardInput=true,CreateNoWindow=true };
			processStartInfo.EnvironmentVariables["GIT_TERMINAL_PROMPT"]="0";
			using Process process=Process.Start(processStartInfo);
			process.StandardInput.Close();
			System.Threading.Tasks.Task<string> taskOut=process.StandardOutput.ReadToEndAsync();
			System.Threading.Tasks.Task<string> taskError=process.StandardError.ReadToEndAsync();
			if(!process.WaitForExit(60000)) {
				process.Kill();
				return "(git "+arguments+" did not end within 60 s)";
			}
			Results.Log("git "+arguments+": "+taskOut.Result.Trim()+" "+taskError.Result.Trim());
			return taskOut.Result.Trim();
		}

		[TestMethod]
		public void K3_K7_K9_Elevation() {
			WinChkFixture.Begin("K");
			WinChkFixture.Record("K3","DbSettings window",CheckStatus.Skipped,"Elevation: the exe asks for administrator rights (A9 checks its manifest); the harness never answers a UAC prompt.");
			WinChkFixture.Record("K7","ServiceManager window",CheckStatus.Skipped,"Elevation: the exe asks for administrator rights (A9 checks its icon and manifest).");
			WinChkFixture.Record("K9","HL7 service install",CheckStatus.Skipped,"Elevation and a service install.");
		}

		[TestMethod]
		public void K4_MySQLManager() {
			WinChkFixture.Begin("K");
			CheckResult checkResult=new CheckResult("K4","MySQLManager");
			using ProgramProcess programProcess=ProgramProcess.Start(Repo("MySQLManager","bin","Debug","MySQLManager.exe"),"");
			IntPtr hwnd=WaitForToolWindow(programProcess,"MySQL User Manager");
			if(checkResult.Expect(hwnd!=IntPtr.Zero,"'MySQL User Manager' opens")) {
				Thread.Sleep(1000);
				string server=Value(hwnd,"textServer");
				string user=Value(hwnd,"textUser");
				checkResult.Expect(server=="localhost" && user=="root","Server localhost and User root (read by UI Automation)",server+" / "+user);
				checkResult.AddShot(Shot.Window(hwnd,"K4.png"),"MySQL User Manager");
				checkResult.Note("Connect is not pressed (it would list the server's accounts)");
				Close(programProcess,hwnd,checkResult,"MySQLManager");
			}
			checkResult.Expect(programProcess.ListDialogsSeen.Count==0,"No message box",string.Join(" || ",programProcess.ListDialogsSeen));
			WinChkFixture.Finish(checkResult);
		}

		[TestMethod]
		public void K5_DatabaseIntegrityCheck() {
			WinChkFixture.Begin("K");
			CheckResult checkResult=new CheckResult("K5","DatabaseIntegrityCheck");
			using ProgramProcess programProcess=ProgramProcess.Start(Repo("DatabaseIntegrityCheck","bin","Debug","DatabaseIntegrityCheck.exe"),"");
			IntPtr hwnd=WaitForToolWindow(programProcess,"Database Integrity Check");
			if(checkResult.Expect(hwnd!=IntPtr.Zero,"'Database Integrity Check' opens")) {
				Thread.Sleep(800);
				checkResult.Note("Defaults: database '"+Value(hwnd,"textDatabase")+"', computer '"+Value(hwnd,"textComputerName")+"', user '"+Value(hwnd,"textUser")+"'");
				SetText(hwnd,"textComputerName","127.0.0.1");
				SetText(hwnd,"textDatabase",ScratchDb.Name);
				SetText(hwnd,"textUser","root");
				SetText(hwnd,"textPassword","");
				checkResult.Expect(Value(hwnd,"textDatabase")==ScratchDb.Name,"Computer 127.0.0.1, Database the scratch copy, Username root, Password empty (WM_SETTEXT)");
				checkResult.Expect(Invoke(hwnd,"butCheck"),"Check pressed (Repair never)");
				string results="";
				Stopwatch stopwatch=Stopwatch.StartNew();
				int countTables=ScratchDb.Table("SHOW TABLES").Rows.Count;
				while(stopwatch.Elapsed.TotalSeconds<240) {
					Thread.Sleep(2000);
					string message=programProcess.GetWindows().Where(x => Native.IsDialogClass(x)).Select(x => Native.GetDialogMessage(x)).FirstOrDefault();
					if(message!=null) {
						checkResult.Note("Message: "+message);
						WaitForMessage(programProcess,2,"OK",checkResult);
					}
					string resultsNow=Value(hwnd,"textResults")??"";
					if(resultsNow!="" && resultsNow==results) {
						break;
					}
					results=resultsNow;
				}
				File.WriteAllText(Path.Combine(WinChkEnv.OutFolder,"K5-results.txt"),results);
				int countReported=results.Replace("\r","").Split('\n').Count(x => x.Trim()!="");
				checkResult.Note("Results: "+countReported+" lines for "+countTables+" tables (K5-results.txt)");
				checkResult.Expect(countReported>=countTables,"Every table reported ("+countReported+" lines, "+countTables+" tables)");
				checkResult.AddShot(Shot.Window(hwnd,"K5.png"),"Database Integrity Check");
				Close(programProcess,hwnd,checkResult,"DatabaseIntegrityCheck");
			}
			WinChkFixture.Finish(checkResult);
		}

		[TestMethod]
		public void K6_CrudGenerator() {
			WinChkFixture.Begin("K");
			CheckResult checkResult=new CheckResult("K6","CrudGenerator");
			using ProgramProcess programProcess=ProgramProcess.Start(Repo("xCrudGenerator","bin","Debug","CrudGenerator.exe"),"");
			IntPtr hwnd=WaitForToolWindow(programProcess,"Crud Generator");
			checkResult.Expect(!programProcess.ListDialogsSeen.Any(x => x.Contains("invalid path")),"No 'invalid path' message",string.Join(" || ",programProcess.ListDialogsSeen));
			if(checkResult.Expect(hwnd!=IntPtr.Zero,"'Crud Generator' opens")) {
				Thread.Sleep(1500);
				string error;
				string snippet=Uia.Try(() => {
					AutomationElement automationElementList=Element(hwnd,"listClass");
					List<AutomationElement> listItems=automationElementList.FindAll(TreeScope.Children,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.ListItem))
						.Cast<AutomationElement>().ToList();
					checkResult.Expect(listItems.Count>100,"The Class list is filled ("+listItems.Count+" classes)");
					AutomationElement automationElementPatient=listItems.FirstOrDefault(x => x.Current.Name=="Patient");
					if(automationElementPatient==null) {
						return "no Patient class";
					}
					((ScrollItemPattern)automationElementPatient.GetCurrentPattern(ScrollItemPattern.Pattern)).ScrollIntoView();
					((SelectionItemPattern)automationElementPatient.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
					Thread.Sleep(500);
					AutomationElement automationElementCombo=Element(hwnd,"comboType");
					((ExpandCollapsePattern)automationElementCombo.GetCurrentPattern(ExpandCollapsePattern.Pattern)).Expand();
					Thread.Sleep(500);
					AutomationElement automationElementFirst=automationElementCombo.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.ListItem));
					if(automationElementFirst!=null) {
						((SelectionItemPattern)automationElementFirst.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
						checkResult.Note("First type: "+automationElementFirst.Current.Name);
					}
					((ExpandCollapsePattern)automationElementCombo.GetCurrentPattern(ExpandCollapsePattern.Pattern)).Collapse();
					Thread.Sleep(300);
					Invoke(hwnd,"butSnippet");
					Thread.Sleep(2000);
					return Value(hwnd,"textSnippet")??"";
				},60000,out error);
				string message=WaitForMessage(programProcess,2,"OK",checkResult);
				if(message!="") {
					checkResult.Fail("Message after Create Snippet: "+message);
				}
				checkResult.Expect(!string.IsNullOrWhiteSpace(snippet) && snippet!="no Patient class","Patient and the first Type selected (UI Automation), Create Snippet: the "
					+"snippet text shows ("+(snippet??"").Length+" characters)",error==""? snippet : error);
				checkResult.AddShot(Shot.Window(hwnd,"K6.png"),"Crud Generator");
				checkResult.Note("Run CRUD Gen is never pressed");
				Close(programProcess,hwnd,checkResult,"CrudGenerator");
			}
			WinChkFixture.Finish(checkResult);
		}

		[TestMethod]
		public void K8_Hl7DebugWindow() {
			WinChkFixture.Begin("K");
			CheckResult checkResult=new CheckResult("K8","HL7 debug window");
			string folder=Repo("OpenPmsDentalHL7","bin","Debug");
			string config=ConfigFiles.Write(folder,"FreeDentalConfig.xml");
			try {
				using ProgramProcess programProcess=ProgramProcess.Start(Path.Combine(folder,"OpenPmsDentalHL7.exe"),"");
				IntPtr hwnd=WaitForToolWindow(programProcess,"OpenPmsDentalHL7 Debug");
				if(checkResult.Expect(hwnd!=IntPtr.Zero,"'OpenPmsDentalHL7 Debug' opens")) {
					Thread.Sleep(1000);
					string server=Value(hwnd,"textServerName");
					string database=Value(hwnd,"textDatabaseName");
					string service=Value(hwnd,"textHL7Name");
					checkResult.Expect(server=="127.0.0.1" && database==ScratchDb.Name && !string.IsNullOrWhiteSpace(service),"Server Name 127.0.0.1, Database Name the scratch copy and a "
						+"service name ("+service+")",server+" / "+database+" / "+service);
					checkResult.Expect(Invoke(hwnd,"butStart"),"Start pressed");
					string output="";
					for(int i=0;i<40 && !output.Contains("Versions do not match");i++) {
						Thread.Sleep(500);
						output=Value(hwnd,"textOutput")??"";
					}
					File.WriteAllText(Path.Combine(WinChkEnv.OutFolder,"K8-output.txt"),output);
					checkResult.Expect(output.Contains("System.ApplicationException: Versions do not match.  Db version:99.0.0.0.  Application version:24.3.63.0"),
						"Output shows the version stop (System.ApplicationException: Versions do not match.  Db version:99.0.0.0.  Application version:24.3.63.0) "
						+"and a stack trace (K8-output.txt)",output.Length>300 ? output.Substring(0,300) : output);
					checkResult.Expect(!programProcess.HasExited,"No crash");
					checkResult.AddShot(Shot.Window(hwnd,"K8.png"),"OpenPmsDentalHL7 Debug");
					Close(programProcess,hwnd,checkResult,"OpenPmsDentalHL7");
				}
				checkResult.Note("The window writes 'Initialized' and the version message to the Application Event Log (source OpenPmsDentalHL7): not removable");
			}
			finally {
				ConfigFiles.Delete(config);
			}
			checkResult.Expect(!File.Exists(config),"The copied FreeDentalConfig.xml is deleted");
			WinChkFixture.Finish(checkResult);
		}

		[TestMethod]
		public void K10_UpdatePath() {
			WinChkFixture.Begin("K");
			using InProcHost inProcHost=InProcHost.Start("chart");
			WinChkFixture.Finish(inProcHost.Run("K10","Update path"));
		}
	}
}
