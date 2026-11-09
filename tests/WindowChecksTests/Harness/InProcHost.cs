// The test's side of an in-process session: a new AppDomain based on the program's output folder (ApplicationBase OpenPmsDental\bin\Debug,
// ConfigurationFile OpenPmsDental.exe.config), the driver created in it from this assembly, the program started there with its command line,
// and the AppDomain unloaded at the end. One session is one start of the program; "exit and start" in a check is a new session.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Diagnostics;
using System.IO;
using WindowChecksTests.InProc;

namespace WindowChecksTests {
	public class InProcHost:IDisposable {
		private AppDomain _appDomain;
		public ProgramDriver Driver;
		public string StartError="";
		public string Patched="";
		public double SecondsToStart;
		private static int _countSessions;

		public static InProcHost Start(string module,int timeoutSeconds=180) {
			InProcHost inProcHost=new InProcHost();
			string folder=WinChkEnv.DebugFolder;
			string exePath=Path.Combine(folder,"OpenPmsDental.exe");
			AppDomainSetup appDomainSetup=new AppDomainSetup {
				ApplicationBase=folder,
				ConfigurationFile=exePath+".config",
				ApplicationName="OpenPmsDental",
			};
			_countSessions++;
			Stopwatch stopwatch=Stopwatch.StartNew();
			inProcHost._appDomain=AppDomain.CreateDomain("winchk-"+_countSessions,null,appDomainSetup);
			inProcHost.Driver=(ProgramDriver)inProcHost._appDomain.CreateInstanceFromAndUnwrap(typeof(ProgramDriver).Assembly.Location,
				typeof(ProgramDriver).FullName);
			inProcHost.Patched=inProcHost.Driver.PatchProcessIdentity(exePath);
			string[] arrayArgs=ProgramProcess.ProgramArgs(module).Split(' ');
			Results.Log("In-process session "+_countSessions+" starting ("+inProcHost.Patched+")");
			inProcHost.StartError=inProcHost.Driver.Start(arrayArgs,timeoutSeconds);
			inProcHost.SecondsToStart=stopwatch.Elapsed.TotalSeconds;
			Results.Log("In-process session "+_countSessions+" start: "+(inProcHost.StartError=="" ? "ready" : inProcHost.StartError)+" after "
				+inProcHost.SecondsToStart.ToString("0.0")+" s");
			return inProcHost;
		}

		public CheckResult Run(string id,string title,int timeoutSeconds=240) {
			if(StartError!="") {
				CheckResult checkResult=new CheckResult(id,title);
				checkResult.Fail("The program did not start in process: "+StartError);
				return checkResult;
			}
			Results.Log("In-process check "+id+" starts");
			CheckResult checkResultRun=Driver.RunCheck(id,title,timeoutSeconds);
			Results.Log("In-process check "+id+": "+checkResultRun.Status);
			return checkResultRun;
		}

		public void Dispose() {
			try {
				string stop=Driver?.Stop(60);
				Results.Log("In-process session stop: "+(stop==""? "ended" : stop));
			}
			catch(Exception ex) {
				Results.Log("In-process session stop failed: "+ex.Message);
			}
			if(_appDomain!=null) {
				try {
					AppDomain.Unload(_appDomain);
					Results.Log("AppDomain unloaded");
				}
				catch(Exception ex) {
					Results.Log("AppDomain not unloaded: "+ex.GetType().Name+": "+ex.Message);
				}
				_appDomain=null;
			}
		}
	}
}
