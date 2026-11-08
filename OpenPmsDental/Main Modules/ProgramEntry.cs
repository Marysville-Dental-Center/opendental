using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;
using System.Windows.Forms;
using CodeBase;
using System.Threading;
using OpenPmsDentalBusiness;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace OpenPmsDental {
	static class ProgramEntry {
		[STAThread]
		static void Main(string[] args) {
			System.Windows.Media.RenderOptions.ProcessRenderMode=System.Windows.Interop.RenderMode.SoftwareOnly;
			//Application.EnableVisualStyles() uses version 6 of comctl32.dll instead of version 5.
			//See https://support.microsoft.com/en-us/topic/system-accessviolationexception-occurs-with-tooltips-in-windows-forms-application-71a775b2-8a03-6846-f810-76930766cda0?ui=en-us&rs=en-us&ad=us
			//See also http://stackoverflow.com/questions/8335983/accessviolationexception-on-tooltip-that-faults-comctl32-dll-net-4-0
			Application.EnableVisualStyles();//This line fixes rare AccessViolationExceptions for ToolTips on our ValidDate boxes, ValidDouble boxes, etc...
			Application.SetCompatibleTextRenderingDefault(false);//designer uses new text rendering.  This makes the exe use matching text rendering.  Before this was added, it was common for labels to be longer in the running program than they were in the designer.
			try {
				ODInitialize.Initialize();
				//The PDF library's licence and font fallback, before anything makes a PDF.
				PdfSharpSetup.Init();
				Security.CurComputerName=ODEnvironment.MachineName;
			}
			catch(Exception e) {
				FriendlyException.Show("Critical Error: "+e.Message,e,isUnhandledException:true);
				return;
			}
			//Register an EventHandler which handles unhandled exceptions.
			//AppDomain.CurrentDomain.UnhandledException+=new UnhandledExceptionEventHandler(OnUnhandeledExceptionPolicy);
			bool isSecondInstance=false ;//or more.
			Process[] processes=Process.GetProcesses();
			for(int i=0;i<processes.Length;i++) {
				if(processes[i].Id==Process.GetCurrentProcess().Id) {
					continue;
				}
				//The whole process name, never a prefix: a prefix would also match other programs whose names start the same way.
				if(ProgramNames.IsProgramProcessName(processes[i].ProcessName)) {
					isSecondInstance=true;
					break;
				}
			}
			/*
			if(args.Length>0) {//if any command line args, then we will attempt to reuse an existing OD window.
				if(isSecondInstance){
					FormCommandLinePassOff formCommandLine=new FormCommandLinePassOff();
					formCommandLine.CommandLineArgs=new string[args.Length];
					args.CopyTo(formCommandLine.CommandLineArgs,0);
					Application.Run(formCommandLine);
					return;
				}
			}*/
			string[] commandLineArgs=new string[args.Length];
			args.CopyTo(commandLineArgs,0);
			//Minimum version for dpi support is Windows 10, version 1607, which is the same as build 15063.
			//Windows recommends checking the specific windows version:
			//https://docs.microsoft.com/en-us/dotnet/desktop/winforms/high-dpi-support-in-windows-forms?view=netframeworkdesktop-4.8
			//But checking versions violates many other MS guidelines that recommend only checking feature support instead of versions.
			//Also, this page cryptically mentions that no server version supports high dpi. Checking versions would not catch that nuance:
			//https://docs.microsoft.com/en-us/windows/win32/hidpi/dpi-awareness-context
			//So we will only test if the feature is supported.
			if(!Dpi.SupportsHighDpi()){
				Dpi.SetUnaware();
			}
			string appDir=Application.StartupPath;
			if(File.Exists(Path.Combine(appDir,"NoDpi.txt"))){
				Dpi.SetUnaware();
				FormODBase.IsDpiSystem=true;
			}
			if(File.Exists(Path.Combine(appDir,"NoCustomBorders.txt"))){
				FormODBase.AreBordersMS=true;
				FormFrame.AreBordersMS=true;
			}
			//In Win10, this registry entry is for 'Let Windows try to fix apps so they're not blurry'.
			//In Win11, behavior is true, but the registry entry is gone and there is no option to change it.
			//So starting with OD 23.1, we will turn this option on for Win 10 users for consistency.
			RegistryKey registryKey=Registry.CurrentUser.OpenSubKey("Control Panel\\Desktop",RegistryKeyPermissionCheck.ReadWriteSubTree);
			object objectVal=registryKey.GetValue("EnablePerProcessSystemDPI");
			if(!ODBuild.IsDebug() && objectVal!=null) {
				int intVal=(int)objectVal;
				if(intVal==0){
					try{
						registryKey.SetValue("EnablePerProcessSystemDPI",1);
					}
					catch{
						MessageBox.Show("Unable to change registry entry.  You should go to Windows Settings, System, Display. Click on Advanced scaling settings about halfway down in blue.  Uncheck the option for 'Let Windows try to fix apps so they're not blurry'.");
						Application.Exit();
						return;
					}
					MessageBox.Show("Registry updated to EnablePerProcessSystemDPI. Please restart Open PMS - Dental.");
					Application.Exit();
					return;
				}
			}
			FormOpenDental formOpenDental=new FormOpenDental(commandLineArgs);
			Exception submittedException=null;
			Action<Exception,string> actionUnhandled=new Action<Exception,string>((e,threadName) => {
				//Try to automatically submit a bug report to HQ.
				string displayMsg="";
				try {
					//We want to submit a maximum of one exception per instance of OD.
					if(submittedException==null) {
						submittedException=e;
						BugSubmissions.SubmitException(e,out displayMsg,threadName,FormOpenDental.PatNumCur,formOpenDental.GetSelectedModuleName());
					}
				}
				catch(Exception ex) {
					ex.DoNothing();
				}
				FriendlyException.Show((displayMsg.IsNullOrEmpty()) ? "Critical Error: "+e.Message : displayMsg,e,isUnhandledException:true);
				formOpenDental.ProcessKillCommand();
			});
			CodeBase.ODThread.RegisterForUnhandledExceptions(formOpenDental,actionUnhandled);
			formOpenDental.IsSecondInstance=isSecondInstance;
			Application.AddMessageFilter(new ODGlobalUserActiveHandler());
			Application.ThreadException+=new ThreadExceptionEventHandler((object s,ThreadExceptionEventArgs e) => {
				actionUnhandled(e.Exception,"ProgramEntry");
			});
			//Some referenced assemblies were built against older Newtonsoft.Json versions (7.0.0.0 or 9.0.0.0). Point them all at the copy that ships.
			ODInitialize.FixPackageAssembly("Newtonsoft.Json",ODFileUtils.CombinePaths(AppDomain.CurrentDomain.BaseDirectory,"Newtonsoft.Json.dll"));
			MouseWatcher.Start();
			if(commandLineArgs.Any(x => x.ToLower()=="issilentupdate=true")) {
				formOpenDental.FormOpenDentalShown();//Form never shows. It returns out after not too long.
				//This prevents zombie process when drawing icons in Direct2D EnumContext.Graphics.
			}
			else {
				Application.Run(formOpenDental);
				//S0-F: the main window's FormClosed ends the process with Environment.Exit. Should the message loop end without it, the process still ends
				//here, instead of staying alive for any foreground thread that is left.
				ExitWatchdog.Program.Start("Main: Application.Run returned");
				Environment.Exit(FormOpenDental.ExitCode);
			}
		}
	}

	//S0-F: the program's exit always ends the process. In the stage 0 window check, File > Exit closed every window after a long session that
	//had used the WebView2 PDF viewer, but the process stayed for more than 3 minutes (4 waiting threads, no CPU). FormOpenDental's FormClosed
	//disposes the main window and then calls Environment.Exit, and .NET Framework ends Environment.Exit within seconds even when a finalizer
	//hangs (checked off-screen in the investigation of S0-F), so that process most likely never got to Environment.Exit: the main thread was
	//held up after its windows were gone (the disposal of the main window destroys the window handles first and disposes the controls after),
	//or the disposal threw and Main returned with a foreground thread left. Nothing recorded which. From FormClosed on, nothing waits for the
	//user any more, so the watchdog gives the closing a fixed time and then ends the process itself, after writing down where the closing stood.
	//It is here, next to Main, so that no file is added to the classic project.
	///<summary>Ends the process when the program's exit does not. Start it where the closing can no longer be cancelled (FormOpenDental's
	///FormClosed), name each step of the closing with SetStep, and call Environment.Exit as before: when the process is still running GraceSeconds
	///later, the watchdog writes the last step and the process's threads to the log (Logger, "Exit" folder), calls Environment.Exit itself (the
	///closing was held up before it), and KillSeconds after that ends the process outright (Environment.Exit itself was held up). One watchdog per
	///process (Program); tests make their own and replace the actions.</summary>
	public class ExitWatchdog {
		///<summary>The program's watchdog.</summary>
		public static readonly ExitWatchdog Program=new ExitWatchdog();
		///<summary>From Start until the watchdog acts. The closing after FormClosed normally takes about a second.</summary>
		public int GraceSeconds=15;
		///<summary>From the watchdog's Environment.Exit until it ends the process outright.</summary>
		public int KillSeconds=5;
		///<summary>Writes the watchdog's report. Never throws (the watchdog swallows what it throws).</summary>
		public Action<string> ActionLog=x => Logger.WriteLine(x,"Exit");
		///<summary>Environment.Exit, called from the watchdog's thread.</summary>
		public Action<int> ActionExit=x => Environment.Exit(x);
		///<summary>Ends the process at once, without the managed shutdown (TerminateProcess with exit code 1).</summary>
		public Action ActionTerminate=TerminateNow;
		private readonly object _lock=new object();
		private string _step="";
		private Thread _thread;
		private DateTime _dateTimeStart;

		///<summary>True once Start ran.</summary>
		public bool IsStarted {
			get {
				lock(_lock) {
					return _thread!=null;
				}
			}
		}

		///<summary>The step of the closing that is running now, for the report.</summary>
		public string Step {
			get {
				lock(_lock) {
					return _step;
				}
			}
		}

		///<summary>Starts the watchdog once, on a background thread (it never keeps the process alive itself).</summary>
		public void Start(string step) {
			lock(_lock) {
				_step=step??"";
				if(_thread!=null) {
					return;
				}
				_dateTimeStart=DateTime.Now;
				_thread=new Thread(Run);
				_thread.IsBackground=true;
				_thread.Name="ExitWatchdog";
				_thread.Start();
			}
		}

		///<summary>Names the step of the closing that starts now.</summary>
		public void SetStep(string step) {
			lock(_lock) {
				_step=step??"";
			}
		}

		private void Run() {
			Thread.Sleep(TimeSpan.FromSeconds(GraceSeconds));
			//Still running: the closing did not end the process.
			ODException.SwallowAnyException(() => ActionLog(GetReport()));
			Thread threadKill=new Thread(() => {
				Thread.Sleep(TimeSpan.FromSeconds(KillSeconds));
				ODException.SwallowAnyException(() => ActionLog("Environment.Exit did not end the process within "+KillSeconds+" seconds either. "
					+"The process is ended now."));
				ActionTerminate();
			});
			threadKill.IsBackground=true;
			threadKill.Name="ExitWatchdogKill";
			threadKill.Start();
			ActionExit(0);
		}

		///<summary>What the log gets: how long the closing has run, its last step and the process's threads.</summary>
		public string GetReport() {
			string report="The program's exit did not end the process within "+GraceSeconds+" seconds (started "+_dateTimeStart.ToString("HH:mm:ss")
				+"). Last step of the closing: "+Step+".";
			try {
				using Process process=Process.GetCurrentProcess();
				report+=" Threads: "+process.Threads.Count+" (";
				for(int i=0;i<process.Threads.Count;i++) {
					ProcessThread processThread=process.Threads[i];
					if(i>0) {
						report+=", ";
					}
					report+=processThread.Id+" "+processThread.ThreadState
						+(processThread.ThreadState==System.Diagnostics.ThreadState.Wait ? "/"+processThread.WaitReason : "");
				}
				report+=").";
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
			return report+" Environment.Exit is called again now, from the watchdog.";
		}

		[DllImport("kernel32.dll")]
		private static extern IntPtr GetCurrentProcess();

		[DllImport("kernel32.dll",SetLastError=true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool TerminateProcess(IntPtr hProcess,uint uExitCode);

		private static void TerminateNow() {
			TerminateProcess(GetCurrentProcess(),1);
		}
	}
}
