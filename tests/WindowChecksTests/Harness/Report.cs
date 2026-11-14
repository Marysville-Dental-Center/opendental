// REPORT.md in the results folder, written by the harness at the end of every run (AssemblyCleanup, after Z) from results.tsv: the header
// (commit, the exes' times, the database, the display, the run's times), the table of the last result of each check, the failures with their
// evidence, and the list for an on-screen pass (NEEDS-SCREEN with the reason, the PARTLY checks' "for a person" items, SKIPPED with the reason).
// Several runs (split by OPMS_WINDOW_CHECKS_GROUPS) append to the same results.tsv, so the report written after the last run covers them all.
// No connection string, password or FreeDentalConfig.xml content goes into it.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace WindowChecksTests {
	public static class Report {
		public static string PathReport {
			get {
				return Path.Combine(WinChkEnv.OutFolder,"REPORT.md");
			}
		}

		///<summary>Writes REPORT.md from results.tsv. Returns "" or the problem (the caller logs it; a run never fails for its report).</summary>
		public static string Write() {
			try {
				File.WriteAllText(PathReport,Build(),new UTF8Encoding(false));
				return "";
			}
			catch(Exception ex) {
				return ex.GetType().Name+": "+ex.Message;
			}
		}

		public static string Build() {
			Dictionary<string,string[]> dictLatest=Results.ReadLatest();
			List<string[]> listRows=dictLatest.Values.OrderBy(x => SortKey(x[1])).ToList();
			StringBuilder sb=new StringBuilder();
			sb.AppendLine("# Stage 1 gate: launch checks, headless (WINCHK-HEADLESS)");
			sb.AppendLine();
			sb.AppendLine("Written by the harness (tests/WindowChecksTests, Harness/Report.cs) at the end of the run, from results.tsv in this folder: the last "
				+"result of each check. Checklist: .git/od-tools/briefs/stage1-launch-checks.md. Every picture named below is in this folder.");
			sb.AppendLine();
			string counts=string.Join(", ",listRows.GroupBy(x => x[2]).OrderBy(x => x.Key).Select(x => x.Key+" "+x.Count()));
			sb.AppendLine("**Counts (last result per check):** "+(counts=="" ? "no results" : counts)+".");
			sb.AppendLine();
			sb.AppendLine("## Header");
			sb.AppendLine();
			foreach(string line in GetHeaderLines(listRows)) {
				sb.AppendLine("- "+line);
			}
			sb.AppendLine();
			sb.AppendLine("## Checks");
			sb.AppendLine();
			sb.AppendLine("| Check | Status | Title | What was verified | Screenshots | Run |");
			sb.AppendLine("|---|---|---|---|---|---|");
			foreach(string[] arrayFields in listRows) {
				sb.AppendLine("| "+arrayFields[1]+" | "+arrayFields[2]+" | "+Cell(arrayFields[3],200)+" | "+Cell(arrayFields[4],700)+" | "+Cell(arrayFields[5],300)
					+" | "+arrayFields[0].Replace("T"," ")+" |");
			}
			sb.AppendLine();
			sb.AppendLine("## Failures");
			sb.AppendLine();
			List<string[]> listFailed=listRows.Where(x => x[2]==CheckStatus.Failed).ToList();
			if(listFailed.Count==0) {
				sb.AppendLine("None.");
			}
			foreach(string[] arrayFields in listFailed) {
				sb.AppendLine("### "+arrayFields[1]+": "+Md(arrayFields[3]));
				sb.AppendLine();
				sb.AppendLine("- Run: "+arrayFields[0].Replace("T"," "));
				sb.AppendLine("- Evidence (the check's full text, failures first): "+Md(arrayFields[4]));
				if(arrayFields[5]!="") {
					sb.AppendLine("- Pictures: "+Md(arrayFields[5]));
				}
				sb.AppendLine();
			}
			sb.AppendLine("## For an on-screen pass");
			sb.AppendLine();
			List<string[]> listNeedsScreen=listRows.Where(x => x[2]==CheckStatus.NeedsScreen).ToList();
			sb.AppendLine("NEEDS-SCREEN ("+listNeedsScreen.Count+"): headless does not reach them.");
			sb.AppendLine();
			foreach(string[] arrayFields in listNeedsScreen) {
				sb.AppendLine("- "+arrayFields[1]+" "+Md(arrayFields[3])+": "+Md(arrayFields[4]));
			}
			sb.AppendLine();
			List<string[]> listPartly=listRows.Where(x => x[2]==CheckStatus.Partly).ToList();
			sb.AppendLine("PARTLY ("+listPartly.Count+"): what code verified passed; these items are for a person to judge.");
			sb.AppendLine();
			foreach(string[] arrayFields in listPartly) {
				sb.AppendLine("- "+arrayFields[1]+" "+Md(arrayFields[3])+": "+Md(GetPart(arrayFields[4],"For a person: ")));
			}
			sb.AppendLine();
			List<string[]> listSkipped=listRows.Where(x => x[2]==CheckStatus.Skipped).ToList();
			sb.AppendLine("SKIPPED ("+listSkipped.Count+"):");
			sb.AppendLine();
			foreach(string[] arrayFields in listSkipped) {
				sb.AppendLine("- "+arrayFields[1]+" "+Md(arrayFields[3])+": "+Md(arrayFields[4]));
			}
			sb.AppendLine();
			return sb.ToString();
		}

		///<summary>The header's lines: the commit, the exes, the database, the display and the times of the results in the table.</summary>
		private static List<string> GetHeaderLines(List<string[]> listRows) {
			List<string> listLines=new List<string>();
			listLines.Add("Commit: "+RunGit("rev-parse --abbrev-ref HEAD")+", "+RunGit("log -1 --format=\"%h %cd\""));
			foreach(string[] arrayExe in new[] { new[] { "Debug",WinChkEnv.DebugFolder },new[] { "Release",WinChkEnv.ReleaseFolder } }) {
				string pathExe=Path.Combine(arrayExe[1],"OpenPmsDental.exe");
				if(!File.Exists(pathExe)) {
					listLines.Add(arrayExe[0]+" exe: none at <worktree>\\"+Relative(pathExe));
					continue;
				}
				DateTime dateWrite=File.GetLastWriteTime(pathExe);
				listLines.Add(arrayExe[0]+" exe <worktree>\\"+Relative(pathExe)+": "+dateWrite.ToString("yyyy-MM-dd HH:mm:ss")+" local ("
					+dateWrite.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss")+" UTC)");
			}
			listLines.Add("Database: a scratch copy per run, winchk_<MMddHHmmss> on "+ScratchDb.Server+":"+ScratchDb.Port+", restored from the dump "
				+Path.GetFileName(WinChkEnv.DumpPath??"")+" and dropped at the end of the run; its A to Z folder is a scratch folder in %TEMP%. Databases of the runs "
				+"in this report (run.log): "+string.Join(", ",GetDatabasesFromLog()));
			try {
				System.Windows.Forms.Screen[] arrayScreens=System.Windows.Forms.Screen.AllScreens;
				int dpi;
				using(System.Drawing.Graphics g=System.Drawing.Graphics.FromHwnd(IntPtr.Zero)) {
					dpi=(int)g.DpiX;
				}
				listLines.Add("Display: "+arrayScreens.Length+" screen(s), primary "+System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width+"x"
					+System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height+", "+dpi+" DPI (as the test host sees it). The checks need no visible desktop: windows "
					+"are printed with PrintWindow, DrawToBitmap or RenderTargetBitmap, so the screen may be locked.");
			}
			catch(Exception ex) {
				listLines.Add("Display: not read ("+ex.Message+")");
			}
			List<DateTime> listTimes=listRows.Select(x => DateTime.ParseExact(x[0],"s",CultureInfo.InvariantCulture)).ToList();
			if(listTimes.Count>0) {
				listLines.Add("Results in the table: "+listTimes.Min().ToString("yyyy-MM-dd HH:mm:ss")+" to "+listTimes.Max().ToString("yyyy-MM-dd HH:mm:ss")
					+" local; this report written "+DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+" after the run of groups "
					+(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(WinChkEnv.VARIABLE_GROUPS)) ? "all" : Environment.GetEnvironmentVariable(WinChkEnv.VARIABLE_GROUPS))+".");
			}
			listLines.Add("Statuses: PASSED (code verified every step), PARTLY (what code verified passed; some items are for a person), NEEDS-SCREEN "
				+"(headless does not reach it), SKIPPED (elevation, or optional), FAILED (with the evidence).");
			listLines.Add("Program exits (exits.txt): "+(File.Exists(Path.Combine(WinChkEnv.OutFolder,"exits.txt"))
				? File.ReadAllLines(Path.Combine(WinChkEnv.OutFolder,"exits.txt")).Length+" timed WM_CLOSE exits recorded" : "none recorded"));
			return listLines;
		}

		private static List<string> GetDatabasesFromLog() {
			string pathLog=Path.Combine(WinChkEnv.OutFolder,"run.log");
			if(!File.Exists(pathLog)) {
				return new List<string> { ScratchDb.Name };
			}
			List<string> listNames=Regex.Matches(File.ReadAllText(pathLog),"Dropped (winchk_\\d+)").Cast<Match>().Select(x => x.Groups[1].Value).ToList();
			if(ScratchDb.IsMade && !listNames.Contains(ScratchDb.Name)) {
				listNames.Add(ScratchDb.Name);
			}
			return listNames.Skip(Math.Max(0,listNames.Count-12)).ToList();
		}

		private static string Relative(string path) {
			string root=WinChkEnv.RepoRoot.TrimEnd('\\')+"\\";
			return path.StartsWith(root,StringComparison.OrdinalIgnoreCase) ? path.Substring(root.Length) : path;
		}

		private static string RunGit(string args) {
			try {
				ProcessStartInfo processStartInfo=new ProcessStartInfo("git",args) {
					WorkingDirectory=WinChkEnv.RepoRoot,
					UseShellExecute=false,
					RedirectStandardOutput=true,
					RedirectStandardError=true,
					CreateNoWindow=true,
				};
				using Process process=Process.Start(processStartInfo);
				string output=process.StandardOutput.ReadToEnd();
				if(!process.WaitForExit(20000)) {
					process.Kill();
					return "(git did not answer)";
				}
				return output.Trim();
			}
			catch(Exception ex) {
				return "(git: "+ex.Message+")";
			}
		}

		///<summary>The text after the marker up to the next part ("Notes: ", "Verified: "), or the whole text when the marker is absent.</summary>
		private static string GetPart(string summary,string marker) {
			int index=summary.IndexOf(marker,StringComparison.Ordinal);
			if(index<0) {
				return summary;
			}
			string part=summary.Substring(index+marker.Length);
			int indexNotes=part.IndexOf(" Notes: ",StringComparison.Ordinal);
			return indexNotes<0 ? part : part.Substring(0,indexNotes);
		}

		///<summary>Group letter, then the check's number, then the rest ("P0" first, "Z" last).</summary>
		private static string SortKey(string id) {
			Match match=Regex.Match(id,"^([A-Z]+)(\\d*)(.*)$");
			if(!match.Success) {
				return "Y"+id;
			}
			string letter=match.Groups[1].Value;
			string prefix=letter=="P" ? "0" : (letter=="Z" ? "Z" : "1"+letter);
			int number=match.Groups[2].Value=="" ? 0 : int.Parse(match.Groups[2].Value);
			return prefix+number.ToString("000")+match.Groups[3].Value;
		}

		private static string Md(string text) {
			return (text??"").Replace("|","\\|").Replace("\r"," ").Replace("\n"," ");
		}

		private static string Cell(string text,int maxLength) {
			string cell=Md(text);
			return cell.Length<=maxLength ? cell : cell.Substring(0,maxLength)+" ...";
		}
	}
}
