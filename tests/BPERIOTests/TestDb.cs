// Scratch database for the Perio Chart tests, on the local MariaDB only (127.0.0.1:3306, root, blank password). It gets the current reference
// schema (SchemaVerifier.GetReference(), the one the CRUD matches) from the snapshot embedded in OpenPmsDentalBusiness and synthetic rows only;
// nothing here reads or copies an office database. Same naming and clean-up pattern as tests/ForkMigrationTests/TestDb.cs. Database tests are skipped (inconclusive) when no
// server answers, or when BPERIO_TEST_DB=skip.
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

namespace BPERIOTests {
	///<summary>Creates, connects to and drops this run's scratch database. Every name starts with "bperio_" so the tests can never drop anything
	///else.</summary>
	[TestClass]
	public class TestDb {
		public const string Server="127.0.0.1";
		public const string User="root";
		public const string Password="";
		private const string PREFIX="bperio_";
		///<summary>Scratch databases left behind by a run that started this long ago are dropped by later runs. No run takes nearly this long.</summary>
		private static readonly TimeSpan _timeSpanStale=TimeSpan.FromHours(12);
		///<summary>Unique to this test run: the start time (UTC, yyMMddHHmmss), the process id and a random number, because several worktrees and
		///test runners share the one local server.</summary>
		public static readonly string DbName=PREFIX+DateTime.UtcNow.ToString("yyMMddHHmmss")+"_"+System.Diagnostics.Process.GetCurrentProcess().Id
			+"_"+new Random().Next(1000,10000);
		///<summary>Null when the scratch database is ready; otherwise why database tests are skipped.</summary>
		public static string SkipReason { get; private set; }
		private static bool _isCreated;

		///<summary>The preferences the perio code reads, at Open Dental's defaults. The Recession option's row is deliberately absent: a database
		///without it must read as off.</summary>
		public static readonly Dictionary<string,string> DictPrefsDefault=new Dictionary<string,string> {
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
			if(Environment.GetEnvironmentVariable("BPERIO_TEST_DB")=="skip") {
				SkipReason="BPERIO_TEST_DB=skip";
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
			new DataConnection().SetDb(Server,DbName,User,Password,"","",DatabaseType.MySql,true);
			Security.CurUser=new Userod { UserNum=1,UserName="Synthetic" };
		}

		[AssemblyCleanup]
		public static void AssemblyCleanup() {
			if(!_isCreated || Environment.GetEnvironmentVariable("BPERIO_KEEP_DB")=="1") {
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
		///tables the perio tests write and restores the default preferences and caches. Sets the culture to en-US, as the labels are compared in
		///English.</summary>
		public static void ResetOrSkip() {
			if(SkipReason!=null) {
				Assert.Inconclusive(SkipReason);
			}
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			Thread.CurrentThread.CurrentUICulture=new CultureInfo("en-US");
			foreach(string table in new[] { "perioexam","periomeasure","procedurelog","procedurecode","preference","securitylog","securityloghash" }) {
				DataCore.NonQ("DELETE FROM "+table);
			}
			foreach(KeyValuePair<string,string> pair in DictPrefsDefault) {
				DataCore.NonQ("INSERT INTO preference (PrefName,ValueString) VALUES('"+pair.Key+"','"+pair.Value+"')");
			}
			Prefs.RefreshCache();
			ProcedureCodes.RefreshCache();
		}

		///<summary>Drops scratch databases that earlier runs left behind (a crash, or BPERIO_KEEP_DB=1), once they are older than _timeSpanStale.
		///Never drops the database of a run that may still be going, and never a name without this naming scheme.</summary>
		private static void DropStaleScratchDatabases() {
			Regex regex=new Regex("^"+PREFIX+@"(\d{12})_\d+_\d+$");
			List<string> listNames=new List<string>();
			using(MySqlConnection connection=OpenServer())
			using(MySqlCommand command=new MySqlCommand("SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME LIKE 'bperio\\_%'",connection))
			using(MySqlDataReader reader=command.ExecuteReader()) {
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
			MySqlConnection connection=new MySqlConnection(connectionString);
			connection.Open();
			return connection;
		}

		private static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.CommandTimeout=600;
				mySqlCommand.ExecuteNonQuery();
			}
		}

		public static long GetLong(string command) {
			return Convert.ToInt64(DataCore.GetScalar(command));
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
