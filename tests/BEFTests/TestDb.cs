// Scratch databases on the local MariaDB for the B-EF tests. Synthetic data only: nothing here reads or copies an office database.
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

namespace BEFTests {
	///<summary>Creates, connects to and drops scratch databases. Every name starts with "beftest_" so a test can never drop anything else. Follows
	///tests/ForkMigrationTests/TestDb.cs: names are unique to the run (start time, process id and a random number), and databases that crashed runs
	///left behind are dropped once they are old.</summary>
	public static class TestDb {
		public const string Server="127.0.0.1";
		public const string User="root";
		public const string Password="";
		private const string PREFIX="beftest_";
		///<summary>Scratch databases left behind by a run that started this long ago are dropped by later runs. No run takes nearly this long.</summary>
		private static readonly TimeSpan _timeSpanStale=TimeSpan.FromHours(12);
		///<summary>Unique to this test run, so parallel runs from other worktrees never share or drop each other's database.</summary>
		private static readonly string _runId=DateTime.UtcNow.ToString("yyMMddHHmmss")+"_"+System.Diagnostics.Process.GetCurrentProcess().Id+"_"
			+new Random().Next(1000,10000);

		///<summary>The name of this run's scratch database for the given purpose, like beftest_261024191500_1234_5678_forms. At most 64 characters.
		///</summary>
		public static string ScratchName(string purpose) {
			string name=PREFIX+_runId+"_"+purpose;
			if(name.Length>64) {
				throw new ArgumentException("Scratch database name too long: "+name);
			}
			return name;
		}

		///<summary>Null when the server answers; otherwise why database tests are skipped.</summary>
		public static string GetSkipReason() {
			if(Environment.GetEnvironmentVariable("BEF_TEST_DB")=="skip") {
				return "BEF_TEST_DB=skip";
			}
			//The shared development server can be slow to answer while other test runs load it, so try a few times before skipping.
			string reason=null;
			for(int i=0;i<3;i++) {
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
			MySqlConnection connection=new MySqlConnection(DevDbTls.Apply("Server="+Server+";Port=3306;User ID="+User+";Password="+Password
				+";Connection Timeout=15;Allow User Variables=true"));
			connection.Open();
			return connection;
		}

		public static void Drop(string name) {
			if(!name.StartsWith(PREFIX)) {
				throw new ArgumentException("Tests only drop databases whose name starts with "+PREFIX);
			}
			using(MySqlConnection connection=OpenServer())
			using(MySqlCommand command=new MySqlCommand("DROP DATABASE IF EXISTS `"+name+"`",connection)) {
				command.ExecuteNonQuery();
			}
		}

		///<summary>Drops this fixture's scratch databases that earlier runs left behind (a crash, or BEF_KEEP_DB=1), once they are older than
		///_timeSpanStale. Never drops a database of a run that may still be going, nor a name that doesn't follow this naming scheme.</summary>
		public static void DropStaleScratchDatabases() {
			Regex regex=new Regex("^"+PREFIX+@"(\d{12})_\d+_\d+_[a-z0-9_]+$");
			List<string> listNames=new List<string>();
			using(MySqlConnection connection=OpenServer())
			using(MySqlCommand command=new MySqlCommand("SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME LIKE 'beftest\\_%'",connection))
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

		///<summary>Builds a fresh database the way tools/New-DevDatabase.ps1 does (the seed dump, the frozen 24.3.63 convert chain), then runs the
		///fork migration runner so the schema is at the Open Dental 25.3.48 level. Leaves the business layer connected to it.</summary>
		public static void CreateFresh25_3(string name) {
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
			new DataConnection().SetDb(Server,name,User,Password,"","",DatabaseType.MySql,true,DevDbTls.GetSslCa(Server));
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
	}
}
