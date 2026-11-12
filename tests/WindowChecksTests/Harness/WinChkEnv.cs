// Where the headless window checks find the program, the database dump and their results folder, all from environment variables (the
// results never go into the repository). OPMS_WINDOW_CHECKS=1 turns the checks on; without it every test is inconclusive.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WindowChecksTests {
	public static class WinChkEnv {
		public const string VARIABLE_ON="OPMS_WINDOW_CHECKS";
		///<summary>A mysqldump of the synthetic test database (made once, by the person running the checks). Every run restores it into a fresh
		///winchk_ database.</summary>
		public const string VARIABLE_DUMP="OPMS_WINDOW_CHECKS_DUMP";
		///<summary>The results folder: screenshots, results.tsv and the logs. Default: %TEMP%\stage1-windows-headless.</summary>
		public const string VARIABLE_OUT="OPMS_WINDOW_CHECKS_OUT";
		///<summary>Comma-separated check groups to run (A to K, Z), so a long run can be split into several shorter ones. Empty: all.</summary>
		public const string VARIABLE_GROUPS="OPMS_WINDOW_CHECKS_GROUPS";
		///<summary>The Release output folder to start for A7 and A8 (tools\Build-Release.ps1). Default: OpenPmsDental\bin\Release.</summary>
		public const string VARIABLE_RELEASE="OPMS_WINDOW_CHECKS_RELEASE";
		///<summary>The folder of mysql.exe. Default: the newest MariaDB under Program Files.</summary>
		public const string VARIABLE_MYSQL_BIN="OPMS_WINDOW_CHECKS_MYSQL_BIN";

		public static bool IsOn {
			get {
				return Environment.GetEnvironmentVariable(VARIABLE_ON)=="1";
			}
		}

		///<summary>Null when the checks may run; otherwise why they are skipped.</summary>
		public static string GetSkipReason(string group) {
			if(!IsOn) {
				return VARIABLE_ON+" is not 1: the headless window checks start the program and its tools, so they run only when asked for.";
			}
			if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(VARIABLE_DUMP)) || !File.Exists(Environment.GetEnvironmentVariable(VARIABLE_DUMP))) {
				return VARIABLE_DUMP+" does not name a dump of the synthetic test database.";
			}
			List<string> listGroups=(Environment.GetEnvironmentVariable(VARIABLE_GROUPS)??"").Split(',').Select(x => x.Trim().ToUpperInvariant())
				.Where(x => x!="").ToList();
			if(group!=null && listGroups.Count>0 && !listGroups.Contains(group.ToUpperInvariant())) {
				return "Group "+group+" is not in "+VARIABLE_GROUPS+".";
			}
			return null;
		}

		///<summary>The repository root: the first folder above this assembly that holds OpenDental.sln.</summary>
		public static string RepoRoot {
			get {
				string folder=Path.GetDirectoryName(typeof(WinChkEnv).Assembly.Location);
				while(folder!=null) {
					if(File.Exists(Path.Combine(folder,"OpenDental.sln"))) {
						return folder;
					}
					folder=Path.GetDirectoryName(folder);
				}
				throw new InvalidOperationException("No OpenDental.sln above "+typeof(WinChkEnv).Assembly.Location);
			}
		}

		public static string DebugFolder {
			get {
				return Path.Combine(RepoRoot,"OpenPmsDental","bin","Debug");
			}
		}

		public static string ReleaseFolder {
			get {
				string folder=Environment.GetEnvironmentVariable(VARIABLE_RELEASE);
				return string.IsNullOrWhiteSpace(folder) ? Path.Combine(RepoRoot,"OpenPmsDental","bin","Release") : folder.Trim();
			}
		}

		public static string DumpPath {
			get {
				return Environment.GetEnvironmentVariable(VARIABLE_DUMP)?.Trim();
			}
		}

		public static string OutFolder {
			get {
				string folder=Environment.GetEnvironmentVariable(VARIABLE_OUT);
				if(string.IsNullOrWhiteSpace(folder)) {
					folder=Path.Combine(Path.GetTempPath(),"stage1-windows-headless");
				}
				Directory.CreateDirectory(folder.Trim());
				return folder.Trim();
			}
		}

		public static string MySqlBin {
			get {
				string folder=Environment.GetEnvironmentVariable(VARIABLE_MYSQL_BIN);
				if(!string.IsNullOrWhiteSpace(folder)) {
					return folder.Trim();
				}
				string programFiles=Environment.GetEnvironmentVariable("ProgramW6432")??Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
				foreach(string root in new[] { programFiles,Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) }.Distinct()) {
					if(!Directory.Exists(root)) {
						continue;
					}
					string folderBin=Directory.GetDirectories(root,"MariaDB*").OrderByDescending(x => x).Select(x => Path.Combine(x,"bin"))
						.FirstOrDefault(x => File.Exists(Path.Combine(x,"mysql.exe")));
					if(folderBin!=null) {
						return folderBin;
					}
				}
				throw new InvalidOperationException("No MariaDB client (mysql.exe) found; set "+VARIABLE_MYSQL_BIN+".");
			}
		}
	}
}
