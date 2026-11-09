// Shared helpers of the spell checker tests: repository paths, a fresh AppDomain for the tests that need the spell checker as a program that just
// started (no dictionary read yet), and scratch databases on the local MariaDB for the office's own words.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DataConnectionBase;
using MySqlConnector;

namespace LibSpellTests {
	internal static class RepoPaths {
		private static string _root;

		///<summary>The repository root: the first folder above the test assembly that holds LICENSE and NOTICE.</summary>
		public static string Root {
			get {
				if(_root!=null) {
					return _root;
				}
				DirectoryInfo directoryInfo=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
				while(directoryInfo!=null) {
					if(File.Exists(Path.Combine(directoryInfo.FullName,"LICENSE")) && File.Exists(Path.Combine(directoryInfo.FullName,"NOTICE"))) {
						_root=directoryInfo.FullName.TrimEnd('\\');
						return _root;
					}
					directoryInfo=directoryInfo.Parent;
				}
				throw new InvalidOperationException("LICENSE and NOTICE not found above "+AppDomain.CurrentDomain.BaseDirectory);
			}
		}

		public static string Combine(params string[] arrayParts) {
			return Path.Combine(Root,Path.Combine(arrayParts));
		}

		public static string ReadText(params string[] arrayParts) {
			return File.ReadAllText(Combine(arrayParts));
		}

		///<summary>The path relative to the repository root, for messages.</summary>
		public static string Relative(string path) {
			string full=Path.GetFullPath(path);
			if(full.StartsWith(Root+"\\",StringComparison.OrdinalIgnoreCase)) {
				return full.Substring(Root.Length+1);
			}
			return full;
		}

		///<summary>True for a file of a git submodule under third-party\ (upstream source, which third-party\build compiles; never changed here).
		///</summary>
		public static bool IsInSubmodule(string path) {
			string relative=Relative(path);
			return relative.StartsWith(@"third-party\PDFsharp\",StringComparison.OrdinalIgnoreCase)
				|| relative.StartsWith(@"third-party\WeCantSpell.Hunspell\",StringComparison.OrdinalIgnoreCase);
		}

		///<summary>Folders never searched: build output, git data, the packages folder, worktrees, editor data.</summary>
		private static readonly string[] _arraySkippedFolders=new string[] { "bin","obj",".git",".claude",".vs","packages","node_modules" };

		///<summary>The files under the repository with the extension, without the skipped folders (they are not even entered).</summary>
		public static List<string> Files(string extension) {
			List<string> listFiles=new List<string>();
			Stack<string> stackFolders=new Stack<string>();
			stackFolders.Push(Root);
			while(stackFolders.Count>0) {
				string folder=stackFolders.Pop();
				listFiles.AddRange(Directory.GetFiles(folder,"*"+extension).Where(x => x.EndsWith(extension,StringComparison.OrdinalIgnoreCase)));
				foreach(string folderSub in Directory.GetDirectories(folder)) {
					if(!_arraySkippedFolders.Contains(Path.GetFileName(folderSub),StringComparer.OrdinalIgnoreCase)) {
						stackFolders.Push(folderSub);
					}
				}
			}
			return listFiles;
		}
	}

	///<summary>Runs a SpellCheckProbe in a new AppDomain of this test's folder and config, so the spell checker's static state starts empty, as in a
	///program that just started: no dictionary read, no word looked up. The domain is unloaded afterwards.</summary>
	internal sealed class IsolatedDomain:IDisposable {
		private readonly AppDomain _appDomain;
		public readonly SpellCheckProbe Probe;

		public IsolatedDomain() {
			string folder=Path.GetDirectoryName(typeof(SpellCheckProbe).Assembly.Location);
			AppDomainSetup appDomainSetup=new AppDomainSetup();
			appDomainSetup.ApplicationBase=folder;
			string pathConfig=typeof(SpellCheckProbe).Assembly.Location+".config";
			if(File.Exists(pathConfig)) {
				appDomainSetup.ConfigurationFile=pathConfig;//the binding redirects of this test (System.Memory and friends)
			}
			_appDomain=AppDomain.CreateDomain("LibSpellTests-"+Guid.NewGuid().ToString("N"),null,appDomainSetup);
			Probe=(SpellCheckProbe)_appDomain.CreateInstanceAndUnwrap(typeof(SpellCheckProbe).Assembly.FullName,typeof(SpellCheckProbe).FullName);
		}

		public void Dispose() {
			try {
				AppDomain.Unload(_appDomain);
			}
			catch(CannotUnloadAppDomainException) {
				//A thread of the domain did not end in time. The domain goes with the test process; the test's result does not depend on it.
			}
		}
	}

	///<summary>Scratch databases on the local MariaDB. Every name starts with test_srcref_spell_, so a test can never drop anything else. Only the two
	///tables the office's own words need are created, from db\init\new-database.sql: dictcustom, and preference with RandomPrimaryKeys off.
	///</summary>
	internal static class ScratchDb {
		public const string Server="127.0.0.1";
		public const string User="root";
		public const string Password="";
		private const string PREFIX="test_srcref_spell_";
		///<summary>Scratch databases a crashed run left behind are dropped by later runs once they are this old.</summary>
		private static readonly TimeSpan _timeSpanStale=TimeSpan.FromHours(12);
		///<summary>Unique to this test run (start time, process id, a random number), so runs from other worktrees never touch each other's
		///databases.</summary>
		private static readonly string _runId=DateTime.UtcNow.ToString("yyMMddHHmmss")+"_"+System.Diagnostics.Process.GetCurrentProcess().Id+"_"
			+new Random().Next(1000,10000);

		public static string NewName(string purpose) {
			string name=PREFIX+_runId+"_"+purpose;
			if(name.Length>64) {
				throw new ArgumentException("Scratch database name too long: "+name);
			}
			return name;
		}

		///<summary>Null when the server answers; otherwise why the database tests are skipped.</summary>
		public static string GetSkipReason() {
			try {
				using(MySqlConnection mySqlConnection=OpenServer()) {
					Scalar(mySqlConnection,"SELECT 1");
				}
				return null;
			}
			catch(Exception ex) {
				return "No local MariaDB at "+Server+":3306 as "+User+" with a blank password: "+ex.GetBaseException().Message;
			}
		}

		public static MySqlConnection OpenServer() {
			MySqlConnection mySqlConnection=new MySqlConnection(DevDbTls.Apply("Server="+Server+";Port=3306;User ID="+User+";Password="+Password
				+";Connection Timeout=15;Pooling=false"));
			mySqlConnection.Open();
			return mySqlConnection;
		}

		///<summary>Creates the database with the dictcustom and preference tables of a new database.</summary>
		public static void Create(string name) {
			CheckName(name);
			string sqlInit=RepoPaths.ReadText("db","init","new-database.sql");
			using(MySqlConnection mySqlConnection=OpenServer()) {
				Exec(mySqlConnection,"DROP DATABASE IF EXISTS `"+name+"`");
				Exec(mySqlConnection,"CREATE DATABASE `"+name+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
				Exec(mySqlConnection,"USE `"+name+"`");
				foreach(string table in new[] { "dictcustom","preference" }) {
					Match match=Regex.Match(sqlInit,@"CREATE TABLE `"+table+@"` \(.*?\) ENGINE=[^;]*;",RegexOptions.Singleline);
					if(!match.Success) {
						throw new InvalidOperationException("db\\init\\new-database.sql has no CREATE TABLE `"+table+"`.");
					}
					Exec(mySqlConnection,match.Value);
				}
				Exec(mySqlConnection,"INSERT INTO preference (PrefName,ValueString) VALUES ('RandomPrimaryKeys','0')");
			}
		}

		public static void Drop(string name) {
			CheckName(name);
			using(MySqlConnection mySqlConnection=OpenServer()) {
				Exec(mySqlConnection,"DROP DATABASE IF EXISTS `"+name+"`");
			}
		}

		///<summary>Drops the test_srcref_spell_ databases of runs that started more than 12 hours ago (a crashed run). Never one of a run that may still
		///be going.</summary>
		public static void DropStale() {
			Regex regex=new Regex("^"+PREFIX+@"(\d{12})_\d+_\d+_[a-z0-9_]+$");
			List<string> listNames=new List<string>();
			using(MySqlConnection mySqlConnection=OpenServer())
			using(MySqlCommand mySqlCommand=new MySqlCommand("SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME LIKE 'test\\_srcref\\_spell\\_%'",
				mySqlConnection))
			using(MySqlDataReader mySqlDataReader=mySqlCommand.ExecuteReader())
			{
				while(mySqlDataReader.Read()) {
					listNames.Add(mySqlDataReader.GetString(0));
				}
			}
			foreach(string name in listNames) {
				Match match=regex.Match(name);
				if(!match.Success || !DateTime.TryParseExact(match.Groups[1].Value,"yyMMddHHmmss",CultureInfo.InvariantCulture,
					DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out DateTime dateStarted))
				{
					continue;
				}
				if(DateTime.UtcNow-dateStarted<_timeSpanStale) {
					continue;
				}
				try {
					Drop(name);
				}
				catch(Exception) {
					//Another run may be dropping it at the same time.
				}
			}
		}

		public static bool Exists(string name) {
			using(MySqlConnection mySqlConnection=OpenServer()) {
				return Scalar(mySqlConnection,"SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME='"+name+"'")!="0";
			}
		}

		///<summary>The number of rows of dictcustom with this word (ignoring case, as the column's collation compares).</summary>
		public static int CountCustomWord(string name,string word) {
			CheckName(name);
			using(MySqlConnection mySqlConnection=OpenServer()) {
				Exec(mySqlConnection,"USE `"+name+"`");
				using(MySqlCommand mySqlCommand=new MySqlCommand("SELECT COUNT(*) FROM dictcustom WHERE WordText=@word",mySqlConnection)) {
					mySqlCommand.Parameters.AddWithValue("@word",word);
					return Convert.ToInt32(mySqlCommand.ExecuteScalar());
				}
			}
		}

		private static void CheckName(string name) {
			if(!name.StartsWith(PREFIX)) {
				throw new ArgumentException("These tests only touch databases whose name starts with "+PREFIX);
			}
		}

		private static void Exec(MySqlConnection mySqlConnection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,mySqlConnection)) {
				mySqlCommand.ExecuteNonQuery();
			}
		}

		private static string Scalar(MySqlConnection mySqlConnection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,mySqlConnection)) {
				object value=mySqlCommand.ExecuteScalar();
				return value==null || value==DBNull.Value ? null : value.ToString();
			}
		}
	}
}
