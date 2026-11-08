// The scratch database of the P262-CLAIMS tests on the local MariaDB, after tests/ForkMigrationTests/TestDb.cs and tests/P262SchemaTests/ScratchDb.cs.
// Synthetic data only: nothing here reads or copies an office database, and nothing connects to any server but 127.0.0.1:3306.
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

namespace P262ClaimsTests {
	///<summary>Creates, connects to and drops scratch databases. Every name starts with "test_p262claims_" plus this run's id, so a test can never
	///drop anything else, and runs from other worktrees on the same server never collide.</summary>
	public static class ScratchDb {
		public const string Server="127.0.0.1";
		public const string User="root";
		public const string Password="";
		public const string PREFIX="test_p262claims_";
		private static readonly TimeSpan _timeSpanStale=TimeSpan.FromHours(12);
		///<summary>Start time (UTC, yyMMddHHmmss), process id and a random number.</summary>
		private static readonly string _runId=DateTime.UtcNow.ToString("yyMMddHHmmss")+"_"+System.Diagnostics.Process.GetCurrentProcess().Id+"_"
			+new Random().Next(1000,10000);

		///<summary>This run's scratch database for the given purpose, like test_p262claims_261107180000_1234_5678_main. At most 64 characters.</summary>
		public static string Name(string purpose) {
			string name=PREFIX+_runId+"_"+purpose;
			if(name.Length>64) {
				throw new ArgumentException("Scratch database name too long: "+name);
			}
			return name;
		}

		///<summary>Null when the local server answers with a blank global sql_mode (as OpenPmsDental.exe sets it); otherwise why database tests are
		///skipped.</summary>
		public static string GetSkipReason() {
			if(Environment.GetEnvironmentVariable("P262_TEST_DB")=="skip") {
				return "P262_TEST_DB=skip";
			}
			try {
				using(MySqlConnection connection=OpenServer()) {
					string sqlMode=Scalar(connection,"SELECT @@GLOBAL.sql_mode");
					if(sqlMode!="" && sqlMode.ToUpperInvariant()!="NO_AUTO_CREATE_USER") {
						return "Global sql_mode is '"+sqlMode+"'; the business layer expects a blank sql_mode.";
					}
				}
				return null;
			}
			catch(Exception ex) {
				return "No local MariaDB at "+Server+":3306 as "+User+" with a blank password: "+ex.GetBaseException().Message;
			}
		}

		public static MySqlConnection OpenServer() {
			MySqlConnection connection=new MySqlConnection("Server="+Server+";Port=3306;User ID="+User+";Password="+Password
				+";Connection Timeout=15;Allow User Variables=true;Default Command Timeout=600");
			connection.Open();
			return connection;
		}

		///<summary>Drops leftovers of earlier runs (crash, or P262_KEEP_DB=1) once they are older than 12 hours.</summary>
		public static void DropStale() {
			Regex regex=new Regex("^"+PREFIX+@"(\d{12})_\d+_\d+_[a-z0-9_]+$");
			List<string> listNames;
			using(MySqlConnection connection=OpenServer()) {
				listNames=Column(connection,"SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME LIKE 'test\\_p262claims\\_%'");
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

		///<summary>Drops and creates an empty utf8mb3 database. Only for names starting with test_p262claims_.</summary>
		public static void Recreate(string name) {
			Drop(name);
			using(MySqlConnection connection=OpenServer()) {
				Exec(connection,"CREATE DATABASE `"+name+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
			}
		}

		public static void Drop(string name) {
			if(!name.StartsWith(PREFIX)) {
				throw new ArgumentException("Tests only drop databases whose name starts with "+PREFIX+".");
			}
			using(MySqlConnection connection=OpenServer()) {
				Exec(connection,"DROP DATABASE IF EXISTS `"+name+"`");
			}
		}

		///<summary>Drops the database unless P262_KEEP_DB=1. Never throws: a later run drops leftovers once they are stale.</summary>
		public static void DropQuietly(string name) {
			if(Environment.GetEnvironmentVariable("P262_KEEP_DB")=="1") {
				return;
			}
			try {
				Drop(name);
			}
			catch(Exception) {
			}
		}

		///<summary>Points the business layer (DataCore, Db, the caches) at the database.</summary>
		public static void Connect(string name) {
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
			new DataConnection().SetDb(Server,name,User,Password,"","",DatabaseType.MySql,true);
		}

		///<summary>Builds a fresh database the way tools/New-DevDatabase.ps1 does, up to the frozen 24.3.63 convert chain: the 21.1 seed dump
		///UnitTests\Resources\dump.txt, then every ConvertDatabases method. No fork migration runs. Leaves the business layer connected to it.</summary>
		public static void CreateFresh24_3(string name) {
			Recreate(name);
			Connect(name);
			string dumpPath=FindRepoFile(Path.Combine("UnitTests","Resources","dump.txt"));
			DataCore.NonQ(File.ReadAllText(dumpPath,new UTF8Encoding(false)));
			Security.CurUser=Security.CurUser??new Userod();
			Prefs.UpdateStringNoCache(PrefName.CorruptedDatabase,"1");
			ConvertDatabases.FromVersion=new Version(DataCore.GetScalar("SELECT ValueString FROM preference WHERE PrefName='DataBaseVersion'"));
			ConvertDatabases.InvokeConvertMethods();
			Prefs.UpdateStringNoCache(PrefName.CorruptedDatabase,"0");
			Cache.Refresh(InvalidType.Prefs);
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

		public static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.CommandTimeout=600;
				mySqlCommand.ExecuteNonQuery();
			}
		}

		public static string Scalar(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				object value=mySqlCommand.ExecuteScalar();
				return value==null || value==DBNull.Value ? null : value.ToString();
			}
		}

		public static List<string> Column(MySqlConnection connection,string command) {
			List<string> list=new List<string>();
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection))
			using(MySqlDataReader reader=mySqlCommand.ExecuteReader()) {
				while(reader.Read()) {
					list.Add(reader.GetValue(0).ToString());
				}
			}
			return list;
		}
	}
}
