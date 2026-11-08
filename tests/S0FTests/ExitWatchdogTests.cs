// S0-F (1): the program's exit always ends the process. ExitWatchdog, started where the closing can no longer be cancelled, gives it a fixed
// time and then writes down the step it stood at, calls Environment.Exit itself and finally ends the process. Tested with its actions replaced
// (nothing exits); the wiring of FormOpenDental's FormClosed and of ProgramEntry by their source. No window is shown.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;

namespace S0FTests {
	[TestClass]
	public class ExitWatchdogTests {
		///<summary>What a test watchdog did, in order.</summary>
		private class Calls {
			private readonly object _lock=new object();
			private readonly List<string> _listCalls=new List<string>();
			public readonly ManualResetEvent EventTerminated=new ManualResetEvent(false);

			public void Add(string call) {
				lock(_lock) {
					_listCalls.Add(call);
				}
			}

			public List<string> ToList() {
				lock(_lock) {
					return _listCalls.ToList();
				}
			}
		}

		private static ExitWatchdog NewWatchdog(Calls calls,int graceSeconds,int killSeconds) {
			ExitWatchdog exitWatchdog=new ExitWatchdog();
			exitWatchdog.GraceSeconds=graceSeconds;
			exitWatchdog.KillSeconds=killSeconds;
			exitWatchdog.ActionLog=x => calls.Add("log: "+x);
			exitWatchdog.ActionExit=x => calls.Add("exit "+x);
			exitWatchdog.ActionTerminate=() => {
				calls.Add("terminate");
				calls.EventTerminated.Set();
			};
			return exitWatchdog;
		}

		[TestMethod]
		public void Watchdog_ClosingHeldUp_LogsTheStep_ExitsThenTerminates() {
			Calls calls=new Calls();
			ExitWatchdog exitWatchdog=NewWatchdog(calls,graceSeconds:1,killSeconds:1);
			exitWatchdog.Start("FormClosed: closing the browsers of the main window");
			exitWatchdog.SetStep("FormClosed: disposing the main window");
			Assert.IsTrue(calls.EventTerminated.WaitOne(TimeSpan.FromSeconds(20)),"The watchdog ended the process: "+string.Join(" | ",calls.ToList()));
			List<string> listCalls=calls.ToList();
			Assert.AreEqual(4,listCalls.Count,string.Join(" | ",listCalls));
			StringAssert.StartsWith(listCalls[0],"log: The program's exit did not end the process within 1 seconds");
			StringAssert.Contains(listCalls[0],"Last step of the closing: FormClosed: disposing the main window.","The last step is named.");
			StringAssert.Contains(listCalls[0],"Threads: ");
			Assert.AreEqual("exit 0",listCalls[1],"First Environment.Exit, from the watchdog's thread.");
			StringAssert.StartsWith(listCalls[2],"log: Environment.Exit did not end the process within 1 seconds either.");
			Assert.AreEqual("terminate",listCalls[3],"Then the process is ended outright.");
		}

		[TestMethod]
		public void Watchdog_ProcessEndsInTime_DoesNothing() {
			Calls calls=new Calls();
			ExitWatchdog exitWatchdog=NewWatchdog(calls,graceSeconds:30,killSeconds:30);
			Assert.IsFalse(exitWatchdog.IsStarted);
			exitWatchdog.Start("step");
			Assert.IsTrue(exitWatchdog.IsStarted);
			Thread.Sleep(500);
			Assert.AreEqual(0,calls.ToList().Count,"Within the grace time it only waits: the normal exit ends the process before it acts.");
		}

		[TestMethod]
		public void Watchdog_StartsOnce_LaterStartsOnlyNameTheStep() {
			Calls calls=new Calls();
			ExitWatchdog exitWatchdog=NewWatchdog(calls,graceSeconds:1,killSeconds:1);
			exitWatchdog.Start("FormClosed");
			exitWatchdog.Start("Main: Application.Run returned");
			Assert.AreEqual("Main: Application.Run returned",exitWatchdog.Step);
			Assert.IsTrue(calls.EventTerminated.WaitOne(TimeSpan.FromSeconds(20)));
			Thread.Sleep(2500);//a second watchdog would have acted by now
			List<string> listCalls=calls.ToList();
			Assert.AreEqual(1,listCalls.Count(x => x=="terminate"),"One watchdog: "+string.Join(" | ",listCalls));
			Assert.AreEqual(1,listCalls.Count(x => x=="exit 0"));
		}

		[TestMethod]
		public void Watchdog_ThrowingLog_StillEndsTheProcess() {
			Calls calls=new Calls();
			ExitWatchdog exitWatchdog=NewWatchdog(calls,graceSeconds:1,killSeconds:1);
			exitWatchdog.ActionLog=x => throw new IOException("The log folder cannot be written.");
			exitWatchdog.Start("step");
			Assert.IsTrue(calls.EventTerminated.WaitOne(TimeSpan.FromSeconds(20)),"A log that throws never keeps the process alive.");
			CollectionAssert.AreEqual(new[] { "exit 0","terminate" },calls.ToList());
		}

		[TestMethod]
		public void Watchdog_TheProgramsOwn_GivesTheClosingTimeButNotMinutes() {
			Assert.IsTrue(ExitWatchdog.Program.GraceSeconds>=10 && ExitWatchdog.Program.GraceSeconds<=30,
				"The closing after FormClosed takes about a second; the window check's process waited more than 3 minutes.");
			Assert.IsTrue(ExitWatchdog.Program.KillSeconds>=1 && ExitWatchdog.Program.KillSeconds<=10);
			Assert.IsFalse(ExitWatchdog.Program.IsStarted,"Nothing in the tests starts the program's watchdog.");
		}

		#region Wiring
		private static string MethodBody(string source,string signature) {
			int index=source.IndexOf(signature,StringComparison.Ordinal);
			Assert.IsTrue(index>=0,"Found: "+signature);
			Match match=new Regex(@"\r?\n\t\t\}").Match(source,index);
			Assert.IsTrue(match.Success);
			return source.Substring(index,match.Index-index);
		}

		private static void AssertOrder(string text,params string[] arrayParts) {
			int indexLast=-1;
			for(int i=0;i<arrayParts.Length;i++) {
				int index=text.IndexOf(arrayParts[i],indexLast+1,StringComparison.Ordinal);
				Assert.IsTrue(index>indexLast,"In order: "+arrayParts[i]+(i>0 ? " after "+arrayParts[i-1] : ""));
				indexLast=index;
			}
		}

		[TestMethod]
		public void Wiring_FormClosed_WatchdogFirst_BrowsersThenDispose_ThenExit() {
			string source=File.ReadAllText(S0FDb.FindRepoFile(Path.Combine("OpenPmsDental","Main Modules","FormOpenDental.cs")));
			string body=MethodBody(source,"private void FormOpenDental_FormClosed(object sender,FormClosedEventArgs e) {");
			AssertOrder(body,"ExitWatchdog.Program.Start(","DisposeWebViews(this)","try {","Dispose();","catch(Exception ex) {","Environment.Exit(0);");
			Assert.AreEqual(1,Regex.Matches(body,@"^\s*Environment\.Exit\(",RegexOptions.Multiline).Count,"One Environment.Exit, at the end.");
			string bodyDispose=MethodBody(source,"private static void DisposeWebViews(Control control) {");
			StringAssert.Contains(bodyDispose,"is CodeBase.Controls.ODWebView2 odWebView2");
			StringAssert.Contains(bodyDispose,"odWebView2.Dispose();");
			StringAssert.Contains(bodyDispose,"DisposeWebViews(controlChild);");
			string entry=File.ReadAllText(S0FDb.FindRepoFile(Path.Combine("OpenPmsDental","Main Modules","ProgramEntry.cs")));
			Assert.IsTrue(Regex.IsMatch(entry,@"Application\.Run\(formOpenDental\);\s*(//[^\n]*\n\s*)*ExitWatchdog\.Program\.Start\([^;]*\);\s*Environment\.Exit\(FormOpenDental\.ExitCode\);"),
				"Main ends the process when the message loop ends.");
		}
		#endregion Wiring
	}
}
