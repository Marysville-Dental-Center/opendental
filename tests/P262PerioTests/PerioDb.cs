// Scratch database for the P262-PERIO tests, on the local MariaDB only (127.0.0.1:3306, root, blank password). It gets the current reference
// schema (SchemaVerifier.GetReference(), Open Dental 26.2.0, the one the CRUD matches) from the snapshot embedded in OpenPmsDentalBusiness and
// synthetic rows only; nothing here reads or copies an office database. Its name starts with "test_p262perio_" plus this run's id, so the tests
// can never drop anything else and runs from other worktrees never collide. Database tests are skipped (inconclusive) when no server answers,
// or when P262PERIO_TEST_DB=skip; P262PERIO_KEEP_DB=1 keeps the database for a look afterwards.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;

namespace P262PerioTests {
	///<summary>Creates, connects to and drops this run's scratch database.</summary>
	[TestClass]
	public class PerioDb {
		public const string Server="127.0.0.1";
		public const string User="root";
		public const string Password="";
		public const string PREFIX="test_p262perio_";
		///<summary>The user the tests act as. Its userodpref rows are the Perio Chart settings under test.</summary>
		public const long USER_NUM=1;
		///<summary>Scratch databases left behind by a run that started this long ago are dropped by later runs. No run takes nearly this long.</summary>
		private static readonly TimeSpan _timeSpanStale=TimeSpan.FromHours(12);
		///<summary>Unique to this test run: the start time (UTC, yyMMddHHmmss), the process id and a random number.</summary>
		public static readonly string DbName=PREFIX+DateTime.UtcNow.ToString("yyMMddHHmmss")+"_"+System.Diagnostics.Process.GetCurrentProcess().Id
			+"_"+new Random().Next(1000,10000);
		///<summary>Null when the scratch database is ready; otherwise why database tests are skipped.</summary>
		public static string SkipReason { get; private set; }
		private static bool _isCreated;

		///<summary>The preferences the perio code reads, at Open Dental's defaults.</summary>
		private static readonly Dictionary<string,string> _dictPrefsDefault=new Dictionary<string,string> {
			{ "RandomPrimaryKeys","0" },
			{ "DxIcdVersion","9" },//read by Procedure's constructor
			{ "UseInternationalToothNumbers","0" },
			{ "PerioRedProb","4" },
			{ "PerioRedMGJ","0" },
			{ "PerioRedGing","2" },
			{ "PerioRedCAL","6" },
			{ "PerioRedFurc","2" },
			{ "PerioRedMob","2" },
			{ "PerioSkipMissingTeeth","1" },
			{ "PerioTreatImplantsAsNotMissing","1" },
		};

		[AssemblyInitialize]
		public static void AssemblyInitialize(TestContext testContext) {
			if(Environment.GetEnvironmentVariable("P262PERIO_TEST_DB")=="skip") {
				SkipReason="P262PERIO_TEST_DB=skip";
				return;
			}
			try {
				using(MySqlConnection connection=OpenServer()) {
					Exec(connection,"CREATE DATABASE `"+DbName+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
				}
			}
			catch(Exception ex) {
				SkipReason="No local MariaDB at "+Server+":3306 as "+User+" with a blank password: "+ex.GetBaseException().Message;
				return;
			}
			_isCreated=true;
			//The server answers, so from here on a failure is a real failure, not a reason to skip.
			DropStaleScratchDatabases();
			using(MySqlConnection connection=OpenServer(DbName)) {
				foreach(SchemaTable table in SchemaVerifier.GetReference().Tables) {
					Exec(connection,table.ToDdl());
				}
			}
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
			new DataConnection().SetDb(Server,DbName,User,Password,"","",DatabaseType.MySql,true,DevDbTls.GetSslCa(Server));
			Security.CurUser=new Userod { UserNum=USER_NUM,UserName="Synthetic" };
		}

		[AssemblyCleanup]
		public static void AssemblyCleanup() {
			if(!_isCreated || Environment.GetEnvironmentVariable("P262PERIO_KEEP_DB")=="1") {
				return;
			}
			try {
				Drop(DbName);
			}
			catch(Exception) {
				//Leaving the scratch database behind is harmless; a later run drops it once it is stale.
			}
		}

		///<summary>Call from TestInitialize of a database test: marks the test inconclusive when there is no scratch database, otherwise empties the
		///tables the tests write and restores the default preferences and caches. Sets the culture to en-US, as labels are compared in English.
		///</summary>
		public static void ResetOrSkip() {
			if(SkipReason!=null) {
				Assert.Inconclusive(SkipReason);
			}
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			Thread.CurrentThread.CurrentUICulture=new CultureInfo("en-US");
			foreach(string table in new[] { "perioexam","periomeasure","procedurelog","procedurecode","preference","userodpref" }) {
				DataCore.NonQ("DELETE FROM "+table);
			}
			foreach(KeyValuePair<string,string> pair in _dictPrefsDefault) {
				DataCore.NonQ("INSERT INTO preference (PrefName,ValueString) VALUES('"+pair.Key+"','"+pair.Value+"')");
			}
			Prefs.RefreshCache();
			ProcedureCodes.RefreshCache();
			UserOdPrefs.RefreshCache();
			Security.CurUser=new Userod { UserNum=USER_NUM,UserName="Synthetic" };
		}

		public static long GetLong(string command) {
			return Convert.ToInt64(DataCore.GetScalar(command));
		}

		public static string GetString(string command) {
			object value=DataCore.GetScalar(command);
			return value==null || value==DBNull.Value ? null : value.ToString();
		}

		///<summary>Drops scratch databases that earlier runs left behind (a crash, or P262PERIO_KEEP_DB=1), once they are older than _timeSpanStale.
		///Never drops the database of a run that may still be going, and never a name without this naming scheme.</summary>
		private static void DropStaleScratchDatabases() {
			Regex regex=new Regex("^"+PREFIX+@"(\d{12})_\d+_\d+$");
			List<string> listNames=new List<string>();
			using(MySqlConnection connection=OpenServer())
			using(MySqlCommand command=new MySqlCommand("SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME LIKE 'test\\_p262perio\\_%'",
				connection))
			using(MySqlDataReader reader=command.ExecuteReader())
			{
				while(reader.Read()) {
					listNames.Add(reader.GetString(0));
				}
			}
			foreach(string name in listNames.Where(x => x!=DbName)) {
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

		private static void Drop(string name) {
			if(!name.StartsWith(PREFIX)) {
				throw new ArgumentException("Tests only drop databases whose name starts with "+PREFIX);
			}
			using(MySqlConnection connection=OpenServer()) {
				Exec(connection,"DROP DATABASE IF EXISTS `"+name+"`");
			}
		}

		private static MySqlConnection OpenServer(string database=null) {
			string connectionString="Server="+Server+";Port=3306;User ID="+User+";Password="+Password+";Connection Timeout=15";
			if(database!=null) {
				connectionString+=";Database="+database;
			}
			MySqlConnection connection=new MySqlConnection(DevDbTls.Apply(connectionString));
			connection.Open();
			return connection;
		}

		private static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.CommandTimeout=600;
				mySqlCommand.ExecuteNonQuery();
			}
		}

		///<summary>Walks up from the test assembly's folder to the repository root (the folder that holds LICENSE and NOTICE).</summary>
		public static string GetRepoRoot() {
			DirectoryInfo dir=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
			while(dir!=null) {
				if(File.Exists(Path.Combine(dir.FullName,"LICENSE")) && File.Exists(Path.Combine(dir.FullName,"NOTICE"))) {
					return dir.FullName;
				}
				dir=dir.Parent;
			}
			throw new DirectoryNotFoundException("The repository root (LICENSE and NOTICE) was not found above the test folder.");
		}

		public static string ReadRepoFile(params string[] arrayParts) {
			return File.ReadAllText(Path.Combine(new[] { GetRepoRoot() }.Concat(arrayParts).ToArray()));
		}
	}
}
