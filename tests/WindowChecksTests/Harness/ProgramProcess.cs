// A program or tool started out of process, as a person starts it (its own folder as the working folder), watched through its windows:
// waiting for a window, reading the message boxes it shows, pressing their buttons by BM_CLICK, printing its windows, and closing it with
// WM_CLOSE while timing the exit. Only processes this class started are ever stopped, by their PID.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace WindowChecksTests {
	public class ProgramProcess:IDisposable {
		public Process Process;
		public string ExePath;
		///<summary>Message boxes seen while waiting, as "title: text [buttons]".</summary>
		public List<string> ListDialogsSeen=new List<string>();
		private readonly HashSet<IntPtr> _hashSetDialogsSeen=new HashSet<IntPtr>();
		public Stopwatch StopwatchStart;

		public static ProgramProcess Start(string exePath,string arguments,Dictionary<string,string> dictionaryEnvironment=null) {
			ProcessStartInfo processStartInfo=new ProcessStartInfo(exePath,arguments??"") {
				UseShellExecute=false,
				WorkingDirectory=Path.GetDirectoryName(exePath),
			};
			if(dictionaryEnvironment!=null) {
				foreach(KeyValuePair<string,string> pair in dictionaryEnvironment) {
					processStartInfo.EnvironmentVariables[pair.Key]=pair.Value;
				}
			}
			ProgramProcess programProcess=new ProgramProcess { ExePath=exePath,StopwatchStart=Stopwatch.StartNew() };
			programProcess.Process=Process.Start(processStartInfo);
			Results.Log("Started "+Path.GetFileName(exePath)+" PID "+programProcess.Process.Id);
			return programProcess;
		}

		///<summary>The command line of the program for this run's copy of the database: Admin with no password, patient #1, the module named.</summary>
		public static string ProgramArgs(string module) {
			string args="ServerName="+ScratchDb.Server+" DatabaseName="+ScratchDb.Name+" MySqlUser=root MySqlPassword= UserName=Admin OdPassword= PatNum=1";
			if(!string.IsNullOrEmpty(module)) {
				args+=" Module="+module;
			}
			return args;
		}

		public bool HasExited {
			get {
				try {
					return Process.HasExited;
				}
				catch(InvalidOperationException) {
					return true;
				}
			}
		}

		public List<IntPtr> GetWindows() {
			return HasExited ? new List<IntPtr>() : Native.GetProcessWindows(Process.Id);
		}

		///<summary>Waits for a visible top-level window of the process that the predicate accepts (given hwnd, class and title). Message boxes that
		///show up meanwhile are recorded in ListDialogsSeen; dialogAction decides what to do with each (true: it was handled); unhandled ones stay.
		///Returns IntPtr.Zero on timeout or exit.</summary>
		public IntPtr WaitForWindow(Func<IntPtr,string,string,bool> predicate,TimeSpan timeout,Func<IntPtr,string,bool> dialogAction=null) {
			Stopwatch stopwatch=Stopwatch.StartNew();
			while(stopwatch.Elapsed<timeout) {
				if(HasExited) {
					return IntPtr.Zero;
				}
				foreach(IntPtr hwnd in GetWindows()) {
					string className=Native.GetClass(hwnd);
					string title=Native.GetText(hwnd);
					if(className=="#32770" && !_hashSetDialogsSeen.Contains(hwnd)) {
						_hashSetDialogsSeen.Add(hwnd);
						string text=Native.GetDialogMessage(hwnd);
						string seen=title+": "+text+" ["+string.Join("/",Native.GetDialogButtons(hwnd))+"]";
						ListDialogsSeen.Add(seen);
						Results.Log("Dialog of "+Path.GetFileName(ExePath)+": "+seen);
						if(dialogAction!=null && dialogAction(hwnd,text)) {
							continue;
						}
					}
					if(predicate(hwnd,className,title)) {
						return hwnd;
					}
				}
				Thread.Sleep(250);
			}
			return IntPtr.Zero;
		}

		///<summary>The first visible window whose title contains the text, or zero.</summary>
		public IntPtr FindWindow(string titlePart) {
			return GetWindows().FirstOrDefault(x => Native.GetText(x).IndexOf(titlePart,StringComparison.OrdinalIgnoreCase)>=0);
		}

		///<summary>Asks the window to close (WM_CLOSE) and waits for the process to end. Returns the seconds it took, or -1 when it did not end
		///within the timeout (the process is then still running: call Kill).</summary>
		public double CloseAndTimeExit(IntPtr hwnd,TimeSpan timeout) {
			Stopwatch stopwatch=Stopwatch.StartNew();
			Native.PostMessage(hwnd,Native.WM_CLOSE,IntPtr.Zero,IntPtr.Zero);
			if(Process.WaitForExit((int)timeout.TotalMilliseconds)) {
				Results.Log(Path.GetFileName(ExePath)+" PID "+Process.Id+" exited in "+stopwatch.Elapsed.TotalSeconds.ToString("0.0")+" s, code "+Process.ExitCode);
				return stopwatch.Elapsed.TotalSeconds;
			}
			return -1;
		}

		///<summary>Ends the process this class started, by its PID, with its child processes.</summary>
		public void Kill() {
			if(HasExited) {
				return;
			}
			Results.Log("Ending "+Path.GetFileName(ExePath)+" PID "+Process.Id);
			try {
				Process taskkill=Process.Start(new ProcessStartInfo("taskkill.exe","/T /F /PID "+Process.Id) { UseShellExecute=false,CreateNoWindow=true });
				taskkill.WaitForExit(15000);
			}
			catch(Exception ex) {
				Results.Log("taskkill failed: "+ex.Message);
			}
			Process.WaitForExit(15000);
		}

		public void Dispose() {
			Kill();
			Process?.Dispose();
		}
	}
}
