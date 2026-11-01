// Scratch databases on the local MariaDB for the M2-AUDIT tests, after the pattern of tests/ForkMigrationTests/TestDb.cs. Synthetic data only:
// nothing here reads or copies an office database, and nothing connects to any server but 127.0.0.1:3306.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using DataConnectionBase;
using MySqlConnector;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;

namespace M2AUDITTests {
	///<summary>Creates, connects to and drops scratch databases. Every name starts with "m2audit_" plus this run's id (start time, process id and a
	///random number), so a test can never drop anything else and runs from other worktrees on the same server never collide.</summary>
	public static class ScratchDb {
		public const string Server="127.0.0.1";
		public const string User="root";
		public const string Password="";
		private const string PREFIX="m2audit_";
		private static readonly TimeSpan _timeSpanStale=TimeSpan.FromHours(12);
		private static readonly string _runId=DateTime.UtcNow.ToString("yyMMddHHmmss")+"_"+System.Diagnostics.Process.GetCurrentProcess().Id+"_"
			+new Random().Next(1000,10000);

		///<summary>This run's scratch database for the given purpose, like m2audit_261025101500_1234_5678_audit.</summary>
		public static string Name(string purpose) {
			string name=PREFIX+_runId+"_"+purpose;
			if(name.Length>64) {
				throw new ArgumentException("Scratch database name too long: "+name);
			}
			return name;
		}

		///<summary>Null when the local server answers; otherwise why database tests are skipped (inconclusive).</summary>
		public static string GetSkipReason() {
			if(Environment.GetEnvironmentVariable("M2AUDIT_TEST_DB")=="skip") {
				return "M2AUDIT_TEST_DB=skip";
			}
			string reason=null;
			for(int i=0;i<3;i++) {//The shared server can be slow to answer while other test runs load it.
				try {
					using(MySqlConnection connection=OpenServer()) {
						return null;
					}
				}
				catch(Exception ex) {
					reason="No local MariaDB at "+Server+":3306 as "+User+" with a blank password: "+ex.GetBaseException().Message;
				}
			}
			return reason;
		}

		public static MySqlConnection OpenServer() {
			MySqlConnection connection=new MySqlConnection("Server="+Server+";Port=3306;User ID="+User+";Password="+Password
				+";Connection Timeout=15;Allow User Variables=true;Default Command Timeout=600");
			connection.Open();
			return connection;
		}

		///<summary>Drops leftovers of earlier runs (a crash, or M2AUDIT_KEEP_DB=1) once they are older than 12 hours.</summary>
		public static void DropStale() {
			Regex regex=new Regex("^"+PREFIX+@"(\d{12})_\d+_\d+_[a-z0-9_]+$");
			List<string> listNames=new List<string>();
			using(MySqlConnection connection=OpenServer())
			using(MySqlCommand command=new MySqlCommand("SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME LIKE 'm2audit\\_%'",connection))
			using(MySqlDataReader reader=command.ExecuteReader()) {
				while(reader.Read()) {
					listNames.Add(reader.GetString(0));
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

		public static void Drop(string name) {
			if(!name.StartsWith(PREFIX)) {
				throw new ArgumentException("Tests only drop databases whose name starts with "+PREFIX+".");
			}
			using(MySqlConnection connection=OpenServer())
			using(MySqlCommand command=new MySqlCommand("DROP DATABASE IF EXISTS `"+name+"`",connection)) {
				command.ExecuteNonQuery();
			}
		}

		///<summary>Drops the database unless M2AUDIT_KEEP_DB=1. Never throws: a later run drops leftovers once they are stale.</summary>
		public static void DropQuietly(string name) {
			if(Environment.GetEnvironmentVariable("M2AUDIT_KEEP_DB")=="1") {
				return;
			}
			try {
				Drop(name);
			}
			catch(Exception) {
			}
		}

		///<summary>Builds a fresh database the way tools/New-DevDatabase.ps1 does (the seed dump, the frozen 24.3.63 convert chain), then runs the fork
		///migration runner, so the schema and the preference rows are at the level of this branch (Open Dental 26.1.70). Leaves the business layer
		///connected to it.</summary>
		public static void CreateFresh(string name) {
			Drop(name);
			using(MySqlConnection connection=OpenServer())
			using(MySqlCommand command=new MySqlCommand("CREATE DATABASE `"+name+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci",connection)) {
				command.ExecuteNonQuery();
			}
			Connect(name);
			string dumpPath=FindRepoFile(Path.Combine("UnitTests","Resources","dump.txt"));
			DataCore.NonQ(File.ReadAllText(dumpPath,new UTF8Encoding(false)));
			Security.CurUser=Security.CurUser??new Userod();
			Prefs.UpdateStringNoCache(PrefName.CorruptedDatabase,"1");
			ConvertDatabases.FromVersion=new Version(DataCore.GetScalar("SELECT ValueString FROM preference WHERE PrefName='DataBaseVersion'"));
			ConvertDatabases.InvokeConvertMethods();
			Prefs.UpdateStringNoCache(PrefName.CorruptedDatabase,"0");
			Cache.Refresh(InvalidType.Prefs);
			ForkMigrationRunner.Run();
			Cache.Refresh(InvalidType.Prefs);
		}

		///<summary>Points the business layer (DataCore, Db, the caches) at the database.</summary>
		public static void Connect(string name) {
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
			new DataConnection().SetDb(Server,name,User,Password,"","",DatabaseType.MySql,true);
		}

		///<summary>Walks up from the test assembly's folder to the repository root and returns the path of a file in it.</summary>
		public static string FindRepoFile(string relativePath) {
			DirectoryInfo dir=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
			while(dir!=null) {
				string candidate=Path.Combine(dir.FullName,relativePath);
				if(File.Exists(candidate)) {
					return candidate;
				}
				dir=dir.Parent;
			}
			throw new FileNotFoundException("Not found above the test folder: "+relativePath);
		}

		///<summary>The repository root: the first folder above the test assembly that holds LICENSE and NOTICE.</summary>
		public static string GetRepositoryRoot() {
			string root=Path.GetDirectoryName(FindRepoFile("NOTICE"));
			if(!File.Exists(Path.Combine(root,"LICENSE"))) {
				throw new FileNotFoundException("LICENSE is not next to NOTICE in "+root);
			}
			return root;
		}
	}
}
