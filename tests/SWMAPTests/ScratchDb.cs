// Scratch databases on the local MariaDB for the SWMAP tests, after the pattern of tests/M201Tests/ScratchDb.cs. Nothing here reads or copies an
// office database: the tables come from the reference schemas embedded in OpenPmsDentalBusiness and every row is made up.
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

namespace SWMAPTests {
	///<summary>Creates, connects to and drops scratch databases. Every name starts with "test_swmap_" so a test can never drop anything else.
	///</summary>
	public static class ScratchDb {
		public const string Server="127.0.0.1";
		public const string User="root";
		public const string Password="";
		private const string PREFIX="test_swmap_";
		private static readonly TimeSpan _timeSpanStale=TimeSpan.FromHours(12);
		///<summary>Start time (UTC, yyMMddHHmmss), process id and a random number: several worktrees and test runners share the one local server.
		///</summary>
		private static readonly string _runId=DateTime.UtcNow.ToString("yyMMddHHmmss")+"_"+System.Diagnostics.Process.GetCurrentProcess().Id+"_"
			+new Random().Next(1000,10000);

		///<summary>This run's scratch database for the given purpose, like test_swmap_261107183000_1234_5678_office. At most 64 characters.</summary>
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
			if(Environment.GetEnvironmentVariable("SWMAP_TEST_DB")=="skip") {
				return "SWMAP_TEST_DB=skip";
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

		///<summary>Drops this fixture's leftovers of earlier runs (a crash, or SWMAP_KEEP_DB=1) once they are older than 12 hours. Never a database of a
		///run that may still be going, and never a name without this naming scheme.</summary>
		public static void DropStale() {
			Regex regex=new Regex("^"+PREFIX+@"(\d{12})_\d+_\d+_[a-z0-9_]+$");
			List<string> listNames;
			using(MySqlConnection connection=OpenServer()) {
				listNames=Column(connection,"SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME LIKE 'test\\_swmap\\_%'");
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

		///<summary>Drops and creates an empty utf8mb3 database. Only for names starting with test_swmap_.</summary>
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

		///<summary>Drops the database unless SWMAP_KEEP_DB=1. Never throws: a later run drops leftovers once they are stale.</summary>
		public static void DropQuietly(string name) {
			if(Environment.GetEnvironmentVariable("SWMAP_KEEP_DB")=="1") {
				return;
			}
			try {
				Drop(name);
			}
			catch(Exception) {
			}
		}

		///<summary>Creates the database with every table of the snapshot (no rows), or only the named tables.</summary>
		public static void CreateFromSnapshot(string name,SchemaSnapshot snapshot,params string[] arrayTablesOnly) {
			Recreate(name);
			using(MySqlConnection connection=OpenServer()) {
				Exec(connection,"USE `"+name+"`");
				foreach(SchemaTable table in snapshot.Tables) {
					if(arrayTablesOnly.Length>0 && !arrayTablesOnly.Contains(table.Name,StringComparer.OrdinalIgnoreCase)) {
						continue;
					}
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

		///<summary>Every preference row of the database as (PrefName, ValueString), in PrefNum order. ValueString null stays null.</summary>
		public static List<(string prefName,string value)> GetPrefRows(string name) {
			List<(string,string)> listRows=new List<(string,string)>();
			using(MySqlConnection connection=OpenServer()) {
				Exec(connection,"USE `"+name+"`");
				using(MySqlCommand command=new MySqlCommand("SELECT PrefName,ValueString FROM preference ORDER BY PrefNum",connection))
				using(MySqlDataReader reader=command.ExecuteReader()) {
					while(reader.Read()) {
						listRows.Add((reader.GetString(0),reader.IsDBNull(1) ? null : reader.GetString(1)));
					}
				}
			}
			return listRows;
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

		///<summary>Walks up from the test assembly's folder to the repository root (the folder that holds LICENSE and NOTICE) and reads a file there.
		///</summary>
		public static string ReadRepoFile(params string[] arrayParts) {
			DirectoryInfo dir=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
			while(dir!=null) {
				if(File.Exists(Path.Combine(dir.FullName,"LICENSE")) && File.Exists(Path.Combine(dir.FullName,"NOTICE"))) {
					return File.ReadAllText(Path.Combine(new[] { dir.FullName }.Concat(arrayParts).ToArray()));
				}
				dir=dir.Parent;
			}
			throw new DirectoryNotFoundException("The repository root (LICENSE and NOTICE) was not found above the test folder.");
		}
	}
}
