// A4: every out-of-process run of the program ends with WM_CLOSE (what File > Exit's Application.Exit and the window's X both reach: the main
// window's FormClosing and FormClosed) and the seconds until the process is gone are kept here, for A4's line in the report.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;

namespace WindowChecksTests {
	public static class Exits {
		public static readonly List<string> ListExits=new List<string>();

		///<summary>Closes the program's main window, times the exit, records it (check id, seconds) and returns the seconds (-1: still running after
		///60 s, then ended by its PID). Expect: gone within 20 s.</summary>
		public static double CloseProgram(ProgramProcess programProcess,IntPtr hwndMain,string checkId,CheckResult checkResult) {
			double seconds=programProcess.CloseAndTimeExit(hwndMain,TimeSpan.FromSeconds(60));
			string line=checkId+": "+(seconds<0 ? "still running after 60 s (ended by its PID)" : seconds.ToString("0.0")+" s");
			lock(ListExits) {
				ListExits.Add(line);
			}
			File.AppendAllText(Path.Combine(WinChkEnv.OutFolder,"exits.txt"),DateTime.Now.ToString("s")+" "+line+Environment.NewLine);
			if(seconds<0) {
				programProcess.Kill();
			}
			checkResult?.Expect(seconds>=0 && seconds<=20,"The program exits within 20 s after WM_CLOSE ("+line+")");
			return seconds;
		}
	}
}
