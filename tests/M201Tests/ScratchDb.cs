// Scratch databases on the local MariaDB for the M2-01 tests, after the pattern of tests/ForkMigrationTests/TestDb.cs. Synthetic data only:
// nothing here reads or copies an office database, and nothing connects to any server but 127.0.0.1:3306.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DataConnectionBase;
using MySqlConnector;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;

namespace M201Tests {
	///<summary>Creates, connects to and drops scratch databases. Every name starts with "m201_" plus this run's id, so a test can never drop anything
	///else, and runs from other worktrees on the same server never collide.</summary>
	public static class ScratchDb {
		public const string Server="127.0.0.1";
		public const string User="root";
		public const string Password="";
		///<summary>The empty Open Dental 26.1.70 reference database that tools/New-OdReferenceDatabase.ps1 loads. Optional.</summary>
		public const string DbNameOd261Schema="od_261_schema";
		private const string PREFIX="m201_";
		private static readonly TimeSpan _timeSpanStale=TimeSpan.FromHours(12);
		///<summary>Start time (UTC, yyMMddHHmmss), process id and a random number.</summary>
		private static readonly string _runId=DateTime.UtcNow.ToString("yyMMddHHmmss")+"_"+System.Diagnostics.Process.GetCurrentProcess().Id+"_"
			+new Random().Next(1000,10000);

		///<summary>This run's scratch database for the given purpose, like m201_261024221500_1234_5678_od253. At most 64 characters.</summary>
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
			if(Environment.GetEnvironmentVariable("M201_TEST_DB")=="skip") {
				return "M201_TEST_DB=skip";
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

		public static bool DatabaseExists(string name) {
			using(MySqlConnection connection=OpenServer()) {
				return Scalar(connection,"SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME='"+name+"'")!="0";
			}
		}

		///<summary>Drops this run's leftovers of earlier runs (crash, or M201_KEEP_DB=1) once they are older than 12 hours.</summary>
		public static void DropStale() {
			Regex regex=new Regex("^"+PREFIX+@"(\d{12})_\d+_\d+_[a-z0-9_]+$");
			List<string> listNames;
			using(MySqlConnection connection=OpenServer()) {
				listNames=Column(connection,"SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME LIKE 'm201\\_%'");
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

		///<summary>Drops and creates an empty utf8mb3 database. Only for names starting with m201_.</summary>
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

		///<summary>Drops the database unless M201_KEEP_DB=1. Never throws: a later run drops leftovers once they are stale.</summary>
		public static void DropQuietly(string name) {
			if(Environment.GetEnvironmentVariable("M201_KEEP_DB")=="1") {
				return;
			}
			try {
				Drop(name);
			}
			catch(Exception) {
			}
		}

		///<summary>Creates the database with every table of the snapshot (no rows).</summary>
		public static void CreateFromSnapshot(string name,SchemaSnapshot snapshot) {
			Recreate(name);
			using(MySqlConnection connection=OpenServer()) {
				Exec(connection,"USE `"+name+"`");
				foreach(SchemaTable table in snapshot.Tables) {
					Exec(connection,table.ToDdl());
				}
			}
		}

		///<summary>Points the business layer (DataCore, Db, the caches) at the database.</summary>
		public static void Connect(string name) {
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
			new DataConnection().SetDb(Server,name,User,Password,"","",DatabaseType.MySql,true);
		}

		///<summary>Runs statements on the database through its own connection.</summary>
		public static void Exec(string name,params string[] arrayCommands) {
			using(MySqlConnection connection=OpenServer()) {
				Exec(connection,"USE `"+name+"`");
				foreach(string command in arrayCommands) {
					Exec(connection,command);
				}
			}
		}

		public static string Scalar(string name,string command) {
			using(MySqlConnection connection=OpenServer()) {
				Exec(connection,"USE `"+name+"`");
				return Scalar(connection,command);
			}
		}

		///<summary>CHECKSUM TABLE for every base table of the database, keyed by table name.</summary>
		public static Dictionary<string,string> ChecksumAllTables(string name) {
			Dictionary<string,string> dict=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
			using(MySqlConnection connection=OpenServer()) {
				List<string> listTables=Column(connection,"SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA='"+name+"' "
					+"AND TABLE_TYPE='BASE TABLE' ORDER BY TABLE_NAME");
				foreach(string table in listTables) {
					using(MySqlCommand command=new MySqlCommand("CHECKSUM TABLE `"+name+"`.`"+table+"`",connection))
					using(MySqlDataReader reader=command.ExecuteReader()) {
						reader.Read();
						dict[table]=reader.IsDBNull(1) ? "null" : reader.GetValue(1).ToString();
					}
				}
			}
			return dict;
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
