// Scratch databases on the local MariaDB for the fork migration tests. Synthetic data only: nothing here reads or copies an office database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DataConnectionBase;
using MySqlConnector;
using OpenDentBusiness;

namespace ForkMigrationTests {
	///<summary>Creates, connects to and drops scratch databases. Every database name starts with "forkmig_" so a test can never drop anything
	///else.</summary>
	public static class TestDb {
		public const string Server="127.0.0.1";
		public const string User="root";
		public const string Password="";
		///<summary>The 25.3.48 reference schema loaded into MariaDB from the schema-only dump (README of the port). Optional.</summary>
		public const string DbNameOd253Schema="od_253_schema";
		private const string PREFIX="forkmig_";
		///<summary>Scratch databases left behind by a run that started this long ago are dropped by later runs. No run takes nearly this long.
		///</summary>
		private static readonly TimeSpan _timeSpanStale=TimeSpan.FromHours(12);
		///<summary>Unique to this test run: the start time (UTC, yyMMddHHmmss), the process id and a random number. Several runs share the one local
		///MariaDB server, from other worktrees or other test runners at the same time, so fixed names would let one run drop or rebuild a
		///database another run is still testing.</summary>
		private static readonly string _runId=DateTime.UtcNow.ToString("yyMMddHHmmss")+"_"+System.Diagnostics.Process.GetCurrentProcess().Id+"_"
			+new Random().Next(1000,10000);

		///<summary>The name of this run's scratch database for the given purpose, like forkmig_261019191500_1234_5678_fresh243. At most 64
		///characters, the MariaDB limit.</summary>
		public static string ScratchName(string purpose) {
			string name=PREFIX+_runId+"_"+purpose;
			if(name.Length>64) {
				throw new ArgumentException("Scratch database name too long: "+name);
			}
			return name;
		}

		///<summary>Drops this fixture's scratch databases that earlier runs left behind (a crash, or FORKMIGRATION_KEEP_DB=1), once they are older
		///than _timeSpanStale. Never drops the databases of a run that may still be going, and never a name without this run's naming scheme, such
		///as the fixed names older versions of these tests used.</summary>
		public static void DropStaleScratchDatabases() {
			Regex regex=new Regex("^"+PREFIX+@"(\d{12})_\d+_\d+_[a-z0-9_]+$");
			List<string> listNames;
			using(MySqlConnection connection=OpenServer()) {
				listNames=Column(connection,"SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME LIKE 'forkmig\\_%'");
			}
			foreach(string name in listNames) {
				Match match=regex.Match(name);
				DateTime dateStarted;
				if(!match.Success || !DateTime.TryParseExact(match.Groups[1].Value,"yyMMddHHmmss",CultureInfo.InvariantCulture,
					DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out dateStarted))
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

		///<summary>Null when the server answers; otherwise why database tests are skipped.</summary>
		public static string GetSkipReason() {
			if(Environment.GetEnvironmentVariable("FORKMIGRATION_TEST_DB")=="skip") {
				return "FORKMIGRATION_TEST_DB=skip";
			}
			try {
				using(MySqlConnection connection=OpenServer()) {
					string sqlMode=Scalar(connection,"SELECT @@GLOBAL.sql_mode");
					if(sqlMode!="" && sqlMode.ToUpperInvariant()!="NO_AUTO_CREATE_USER") {
						return "Global sql_mode is '"+sqlMode+"'; the convert scripts need a blank sql_mode, as OpenDental.exe sets at startup.";
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
				+";Connection Timeout=15;Allow User Variables=true");
			connection.Open();
			return connection;
		}

		public static bool DatabaseExists(string name) {
			using(MySqlConnection connection=OpenServer()) {
				return Scalar(connection,"SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME='"+name+"'")!="0";
			}
		}

		///<summary>Drops and creates an empty utf8mb3 database. Only for names starting with forkmig_.</summary>
		public static void Recreate(string name) {
			Drop(name);
			using(MySqlConnection connection=OpenServer()) {
				Exec(connection,"CREATE DATABASE `"+name+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
			}
		}

		public static void Drop(string name) {
			if(!name.StartsWith(PREFIX)) {
				throw new ArgumentException("Tests only drop databases whose name starts with forkmig_.");
			}
			using(MySqlConnection connection=OpenServer()) {
				Exec(connection,"DROP DATABASE IF EXISTS `"+name+"`");
			}
		}

		///<summary>Points the business layer (DataCore, Db, the caches) at the database.</summary>
		public static void Connect(string name) {
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
			new DataConnection().SetDb(Server,name,User,Password,"","",DatabaseType.MySql,true);
		}

		///<summary>Builds a fresh database the way tools/New-DevDatabase.ps1 does, stopping at the frozen 24.3.63 convert chain: the 21.1 seed dump
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

		///<summary>CHECKSUM TABLE for every base table of the database, keyed by table name. Used to prove that a migration changed no data.</summary>
		public static Dictionary<string,string> ChecksumAllTables(string name,params string[] arrayTablesToSkip) {
			Dictionary<string,string> dict=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
			using(MySqlConnection connection=OpenServer()) {
				List<string> listTables=Column(connection,"SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA='"+name+"' "
					+"AND TABLE_TYPE='BASE TABLE' ORDER BY TABLE_NAME");
				foreach(string table in listTables) {
					if(arrayTablesToSkip.Contains(table,StringComparer.OrdinalIgnoreCase)) {
						continue;
					}
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
	}
}
