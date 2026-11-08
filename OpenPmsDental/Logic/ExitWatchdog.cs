// S0-F: the program's exit always ends the process. In the stage 0 window check, File > Exit closed every window after a long session that had
// used the WebView2 PDF viewer, but the process stayed for more than 3 minutes (4 waiting threads, no CPU). FormOpenDental's FormClosed disposes
// the main window and then calls Environment.Exit, and .NET Framework ends Environment.Exit within seconds even when a finalizer hangs (checked
// off-screen in the investigation of S0-F), so that process most likely never got to Environment.Exit: the main thread was held up after its
// windows were gone (the disposal of the main window destroys the window handles first and disposes the controls after), or the disposal threw
// and Main returned with a foreground thread left. Nothing recorded which. From FormClosed on, nothing waits for the user any more, so the
// watchdog gives the closing a fixed time and then ends the process itself, after writing down where the closing stood.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using CodeBase;

namespace OpenPmsDental {
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
