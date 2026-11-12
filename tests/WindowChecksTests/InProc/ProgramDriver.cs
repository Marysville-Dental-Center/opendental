// The in-process half of the harness: the program's own assemblies, loaded in an AppDomain whose base is OpenPmsDental\bin\Debug and whose
// configuration file is OpenPmsDental.exe.config (so the binding redirects and the probing are the real ones), run the main window on an STA
// thread as ProgramEntry.Main does, and the checks drive it from a second thread of that AppDomain: they post what a click would run (the
// real menu item's PerformClick, the toolbar's and module bar's handlers) to the window's thread, wait for the windows it opens, read and
// print them, and close them. Every unhandled exception (Application.ThreadException, AppDomain.UnhandledException, ODThread's, WPF's
// Dispatcher) is collected and fails the check that was running.
// The main window is never closed through the program's own closing (its FormClosed ends the process with Environment.Exit): Stop removes
// those handlers and ends the window's thread, and the AppDomain is unloaded.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace WindowChecksTests.InProc {
	///<summary>Lives in the program's AppDomain; the test's AppDomain holds a proxy. Only strings and serializable results cross.</summary>
	public class ProgramDriver:MarshalByRefObject {
		private Thread _threadUi;
		private Form _formMain;
		private int _threadIdUi;
		private readonly ManualResetEvent _manualResetEventShown=new ManualResetEvent(false);
		private readonly ManualResetEvent _manualResetEventUiEnded=new ManualResetEvent(false);
		private string _startError="";
		private readonly List<string> _listExceptions=new List<string>();
		private readonly object _lock=new object();
		public Ui UiCur;

		public override object InitializeLifetimeService() {
			return null;
		}

		///<summary>Makes this AppDomain look like the program's own process where .NET reads it from the process: Application.StartupPath and
		///ExecutablePath (the exe's folder, not the test host's) and Assembly.GetEntryAssembly (OpenPmsDental.exe). Returns what was patched.</summary>
		public string PatchProcessIdentity(string exePath) {
			List<string> listPatched=new List<string>();
			FieldInfo fieldStartupPath=typeof(Application).GetField("startupPath",BindingFlags.NonPublic|BindingFlags.Static);
			FieldInfo fieldExecutablePath=typeof(Application).GetField("executablePath",BindingFlags.NonPublic|BindingFlags.Static);
			if(fieldStartupPath!=null) {
				fieldStartupPath.SetValue(null,Path.GetDirectoryName(exePath));
				listPatched.Add("Application.StartupPath");
			}
			if(fieldExecutablePath!=null) {
				fieldExecutablePath.SetValue(null,exePath);
				listPatched.Add("Application.ExecutablePath");
			}
			Assembly assemblyExe=Assembly.Load("OpenPmsDental");
			AppDomainManager appDomainManager=new AppDomainManager();
			FieldInfo fieldEntry=typeof(AppDomainManager).GetField("m_entryAssembly",BindingFlags.NonPublic|BindingFlags.Instance);
			FieldInfo fieldManager=typeof(AppDomain).GetField("_domainManager",BindingFlags.NonPublic|BindingFlags.Instance);
			if(fieldEntry!=null && fieldManager!=null && AppDomain.CurrentDomain.DomainManager==null) {
				fieldEntry.SetValue(appDomainManager,assemblyExe);
				fieldManager.SetValue(AppDomain.CurrentDomain,appDomainManager);
				listPatched.Add("Assembly.GetEntryAssembly");
			}
			return string.Join(", ",listPatched)+"; OpenPmsDental loaded from "+assemblyExe.Location+"; config "
				+AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
		}

		///<summary>Starts the main window with the command line, as ProgramEntry.Main does, and waits until it is shown and the user is logged on.
		///Returns "" or the problem.</summary>
		///<summary>Message boxes the start-up showed and the harness answered with Cancel (cancelTexts), as "text".</summary>
		public List<string> ListStartPrompts=new List<string>();

		public string Start(string[] args,int timeoutSeconds) {
			return Start(args,timeoutSeconds,new string[0]);
		}

		///<summary>As Start; a start-up message box that contains one of cancelTexts is answered with Cancel (BM_CLICK) and recorded in
		///ListStartPrompts. Any other window the start-up opens is a problem.</summary>
		public string Start(string[] args,int timeoutSeconds,string[] cancelTexts) {
			AppDomain.CurrentDomain.UnhandledException+=(sender,e) => AddException("AppDomain.UnhandledException",e.ExceptionObject as Exception);
			_threadUi=new Thread(() => RunUi(args)) { Name="winchk UI",IsBackground=true };
			_threadUi.SetApartmentState(ApartmentState.STA);
			_threadUi.Start();
			Stopwatch stopwatchShown=Stopwatch.StartNew();
			//A prompt can come before the window shows (the release check runs in Shown, before the log-on).
			while(!_manualResetEventShown.WaitOne(200)) {
				if(stopwatchShown.Elapsed.TotalSeconds>timeoutSeconds) {
					return "The main window was not shown within "+timeoutSeconds+" s."+(_startError=="" ? "" : " "+_startError);
				}
				AnswerStartPrompts(cancelTexts);
			}
			if(_startError!="") {
				return _startError;
			}
			UiCur=new Ui(this,_formMain,_threadIdUi);
			//The log-on runs in Shown, after the window shows: wait for the user and patient #1.
			Stopwatch stopwatch=Stopwatch.StartNew();
			while(stopwatch.Elapsed.TotalSeconds<timeoutSeconds) {
				bool isReady=UiCur.Invoke(() => InProcChecks.IsLoggedOnWithPatient());
				if(isReady) {
					return "";
				}
				AnswerStartPrompts(cancelTexts);
				//A window the start-up opened (a message, Choose Database) is a problem: name it.
				IntPtr hwndMain=UiCur.Invoke(() => _formMain.Handle);
				foreach(IntPtr hwnd in Native.GetThreadWindows(_threadIdUi).Where(x => x!=hwndMain)) {
					string text=UiCur.DescribeWindow(hwnd);
					if(text!="") {
						return "The start-up opened a window: "+text;
					}
				}
				Thread.Sleep(300);
			}
			return "Not logged on with patient #1 within "+timeoutSeconds+" s.";
		}

		private void AnswerStartPrompts(string[] cancelTexts) {
			if(_threadIdUi==0) {
				return;
			}
			foreach(IntPtr hwnd in Native.GetThreadWindows(_threadIdUi).Where(x => Native.IsDialogClass(x))) {
				string text=Native.GetDialogMessage(hwnd);
				if(cancelTexts.Any(x => text.Contains(x))) {
					ListStartPrompts.Add(text);
					Native.ClickDialogButton(hwnd,"Cancel");
					Thread.Sleep(500);
				}
			}
		}

		private void RunUi(string[] args) {
			try {
				_threadIdUi=Native.GetCurrentThreadId();
				InProcChecks.InitializeAsProgramEntry();
				Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
				Application.ThreadException+=(sender,e) => AddException("Application.ThreadException",e.Exception);
				_formMain=InProcChecks.CreateMainWindow(args,(ex,threadName) => AddException("ODThread "+threadName,ex));
				_formMain.Shown+=(sender,e) => _manualResetEventShown.Set();
				InProcChecks.HookDispatcher((ex) => AddException("Dispatcher.UnhandledException",ex));
				Application.Run(_formMain);
			}
			catch(Exception ex) {
				_startError="The main window failed: "+ex;
				_manualResetEventShown.Set();
			}
			finally {
				_manualResetEventUiEnded.Set();
			}
		}

		///<summary>The program's own aborts of its worker threads (ODThread.QuitSync aborts a thread that does not end in time): noted, not failed; a
		///person sees nothing of them.</summary>
		private readonly List<string> _listAborts=new List<string>();

		public void AddException(string source,Exception ex) {
			if(ex is ThreadAbortException) {
				lock(_lock) {
					_listAborts.Add(source+": "+ex.GetType().FullName+" "+FirstFrames(ex));
				}
				return;
			}
			lock(_lock) {
				_listExceptions.Add(source+": "+(ex==null ? "(no exception object)" : ex.GetType().FullName+": "+ex.Message+" "+FirstFrames(ex)));
			}
		}

		private static string FirstFrames(Exception ex) {
			string stack=(ex.GetBaseException().StackTrace??"").Replace("\r","");
			return "["+string.Join(" / ",stack.Split('\n').Select(x => x.Trim()).Where(x => x!="").Take(6))+"]";
		}

		public int CountExceptions {
			get {
				lock(_lock) {
					return _listExceptions.Count;
				}
			}
		}

		public List<string> GetExceptionsFrom(int index) {
			lock(_lock) {
				return _listExceptions.Skip(index).ToList();
			}
		}

		///<summary>Runs one check's in-process script on a thread of this AppDomain and returns its result. A script that does not end in time is
		///reported as failed (its windows are closed by Stop).</summary>
		public CheckResult RunCheck(string id,string title,int timeoutSeconds) {
			CheckResult checkResult=new CheckResult(id,title);
			int countExceptionsBefore=CountExceptions;
			int countAbortsBefore;
			lock(_lock) {
				countAbortsBefore=_listAborts.Count;
			}
			Exception exceptionScript=null;
			Thread thread=new Thread(() => {
				try {
					UiCur.CheckResultCur=checkResult;
					InProcChecks.Run(id,UiCur,checkResult);
				}
				catch(ThreadAbortException) {
				}
				catch(Exception ex) {
					exceptionScript=ex;
				}
			}) { Name="winchk check "+id,IsBackground=true };
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
			if(!thread.Join(TimeSpan.FromSeconds(timeoutSeconds))) {
				checkResult.Fail("The check did not end within "+timeoutSeconds+" s (a window hung or an expected window never came)");
				try {
					thread.Abort();
				}
				catch(Exception) {
				}
				UiCur.CloseStrayWindows(checkResult);
			}
			if(exceptionScript!=null) {
				checkResult.Fail("The check's script stopped: "+exceptionScript.GetType().Name+": "+exceptionScript.Message+" "+FirstFrames(exceptionScript));
				UiCur.CloseStrayWindows(checkResult);
			}
			foreach(string exception in GetExceptionsFrom(countExceptionsBefore)) {
				checkResult.Fail("Unhandled exception: "+exception);
			}
			lock(_lock) {
				foreach(string abort in _listAborts.Skip(countAbortsBefore)) {
					checkResult.Note("A worker thread of the program was aborted (not shown to a person; ODThread aborts a thread that does not quit in time): "+abort);
				}
			}
			return checkResult;
		}

		///<summary>Ends the window's thread without the program's own closing (no Environment.Exit), and waits for it.</summary>
		public string Stop(int timeoutSeconds) {
			if(_formMain==null || _threadUi==null) {
				return "not started";
			}
			try {
				UiCur?.CloseStrayWindows(null);
				_formMain.BeginInvoke((Action)(() => {
					RemoveEventHandlers(_formMain,"EVENT_FORMCLOSING");
					RemoveEventHandlers(_formMain,"EVENT_FORMCLOSED");
					Application.ExitThread();
				}));
			}
			catch(Exception ex) {
				return "Stop failed: "+ex.Message;
			}
			if(!_manualResetEventUiEnded.WaitOne(TimeSpan.FromSeconds(timeoutSeconds))) {
				return "The window's thread did not end within "+timeoutSeconds+" s.";
			}
			return "";
		}

		private static void RemoveEventHandlers(Component component,string keyName) {
			FieldInfo fieldKey=typeof(Form).GetField(keyName,BindingFlags.NonPublic|BindingFlags.Static);
			PropertyInfo propertyEvents=typeof(Component).GetProperty("Events",BindingFlags.NonPublic|BindingFlags.Instance);
			EventHandlerList eventHandlerList=(EventHandlerList)propertyEvents.GetValue(component);
			object key=fieldKey.GetValue(null);
			Delegate handler=eventHandlerList[key];
			if(handler!=null) {
				eventHandlerList.RemoveHandler(key,handler);
			}
		}
	}
}
