// Scratch databases on the local MariaDB for the library update tests, after the pattern of tests/InitSqlTests/ScratchDb.cs: every name starts
// with test_libt1_ plus this run's id, and nothing connects to any server but 127.0.0.1:3306. Synthetic data only.
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

namespace LibraryUpdateTests {
	///<summary>Creates, loads, connects to and drops scratch databases named test_libt1_*.</summary>
	public static class ScratchDb {
		public const string Server="127.0.0.1";
		public const string User="root";
		public const string Password="";
		private const string PREFIX="test_libt1_";
		private static readonly TimeSpan _timeSpanStale=TimeSpan.FromHours(12);
		///<summary>Start time (UTC, yyMMddHHmmss), process id and a random number.</summary>
		private static readonly string _runId=DateTime.UtcNow.ToString("yyMMddHHmmss",CultureInfo.InvariantCulture)+"_"
			+System.Diagnostics.Process.GetCurrentProcess().Id+"_"+new Random().Next(1000,10000);

		///<summary>This run's scratch database for the given purpose, like test_libt1_261102101500_1234_5678_pop3. At most 64 characters.</summary>
		public static string Name(string purpose) {
			string name=PREFIX+_runId+"_"+purpose;
			if(name.Length>64 || !Regex.IsMatch(name,"^[a-z0-9_]+$")) {
				throw new ArgumentException("Not a usable scratch database name: "+name);
			}
			return name;
		}

		///<summary>Null when the local server answers with a blank global sql_mode (as OpenPmsDental.exe sets it); otherwise why the database tests
		///are skipped.</summary>
		public static string GetSkipReason() {
			if(Environment.GetEnvironmentVariable("LIBRARYUPDATE_TEST_DB")=="skip") {
				return "LIBRARYUPDATE_TEST_DB=skip";
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

		public static MySqlConnection OpenServer(string database="") {
			MySqlConnection connection=new MySqlConnection(DevDbTls.Apply("Server="+Server+";Port=3306;User ID="+User+";Password="+Password
				+(database=="" ? "" : ";Database="+database)+";Connection Timeout=15;Allow User Variables=true;Default Command Timeout=600"));
			connection.Open();
			return connection;
		}

		///<summary>Drops leftovers of earlier runs (a crash) once they are older than 12 hours.</summary>
		public static void DropStale() {
			Regex regex=new Regex("^"+PREFIX+@"(\d{12})_\d+_\d+_[a-z0-9_]+$");
			List<string> listNames=new List<string>();
			using(MySqlConnection connection=OpenServer())
			using(MySqlCommand command=new MySqlCommand("SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME LIKE 'test\\_libt1\\_%'",
				connection))
			using(MySqlDataReader reader=command.ExecuteReader())
			{
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

		///<summary>Drops and creates an empty utf8mb3 database, as db/README.md tells an office to.</summary>
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

		public static void DropQuietly(string name) {
			try {
				Drop(name);
			}
			catch(Exception) {
				//The stale clean-up of a later run drops it.
			}
		}

		///<summary>Runs db/init/new-database.sql into the database statement by statement (every statement ends with a semicolon at the end of a
		///line, and no value holds a raw line break).</summary>
		public static void LoadNewDatabase(string name) {
			string text=File.ReadAllText(FindRepoFile(Path.Combine("db","init","new-database.sql")),new UTF8Encoding(false)).Replace("\r\n","\n");
			using(MySqlConnection connection=OpenServer(name)) {
				foreach(string statement in text.Split(new[] { ";\n" },StringSplitOptions.None)) {
					if(statement.Trim()=="") {
						continue;
					}
					Exec(connection,statement);
				}
			}
		}

		///<summary>Points the business layer (DataCore, Db, the caches) at the database.</summary>
		public static void Connect(string name) {
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
			new DataConnection().SetDb(Server,name,User,Password,"","",DatabaseType.MySql,true,DevDbTls.GetSslCa(Server));
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
				return value==null || value==DBNull.Value ? null : Convert.ToString(value,CultureInfo.InvariantCulture);
			}
		}

		///<summary>Walks up from the test assembly's folder to the repository root and returns the path of a file in it.</summary>
		public static string FindRepoFile(string relativePath) {
			DirectoryInfo directoryInfo=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
			while(directoryInfo!=null) {
				string candidate=Path.Combine(directoryInfo.FullName,relativePath);
				if(File.Exists(candidate)) {
					return candidate;
				}
				directoryInfo=directoryInfo.Parent;
			}
			throw new FileNotFoundException("Not found above the test folder: "+relativePath);
		}
	}
}
