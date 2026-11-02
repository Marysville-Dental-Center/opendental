// Scratch databases for the inventory tracking tests on the local MariaDB (127.0.0.1:3306, root, blank password). Every name starts with
// "test_inv_" plus this run's id, so a test can never drop anything else and runs from other worktrees never collide. The main database is the
// committed db/init/new-database.sql with every fork migration of this build run on it (the inventory steps, when the file predates them), and
// tracking turned on. Synthetic rows only. Skipped (inconclusive) when the server does not answer, or when INVENTORY_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;

namespace InventoryTests {
	[TestClass]
	public class InvDb {
		public const string Server="127.0.0.1";
		public const string User="root";
		public const string Password="";
		private const string PREFIX="test_inv_";
		private static readonly TimeSpan _timeSpanStale=TimeSpan.FromHours(12);
		///<summary>Start time (UTC, yyMMddHHmmss), process id and a random number.</summary>
		private static readonly string _runId=DateTime.UtcNow.ToString("yyMMddHHmmss")+"_"+System.Diagnostics.Process.GetCurrentProcess().Id+"_"
			+new Random().Next(1000,10000);
		///<summary>The database most tests use.</summary>
		public static readonly string DbMain=Name("main");
		///<summary>Null when the main database is ready; otherwise why the database tests are skipped.</summary>
		public static string SkipReason;
		public const string RepoPathInitSql=@"db\init\new-database.sql";
		private static bool _isBuilt;

		[AssemblyInitialize]
		public static void AssemblyInitialize(TestContext testContext) {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			SkipReason=GetSkipReason();
			if(SkipReason!=null) {
				return;
			}
			try {
				DropStale();
				CreateFromInitFile(DbMain);
				ForkMigrationRunner.Run();
				Cache.Refresh(InvalidType.Prefs,InvalidType.ProcCodes,InvalidType.Providers,InvalidType.Defs);
				Userods.RefreshCache();
				Security.CurUser=Userods.GetUser(1)??new Userod { UserNum=1,UserName="Admin" };
				Prefs.UpdateBool(PrefName.InventoryTrackingEnabled,true);
				Cache.Refresh(InvalidType.Prefs);
				_isBuilt=true;
			}
			catch(Exception ex) {
				SkipReason="Could not build the scratch database: "+ex.GetBaseException().Message;
			}
		}

		[AssemblyCleanup]
		public static void AssemblyCleanup() {
			if(!_isBuilt) {
				return;
			}
			DropQuietly(DbMain);
		}

		///<summary>Call from [TestInitialize]: skips the test when there is no database, otherwise connects to the main database as Admin with
		///tracking on.</summary>
		public static void Begin() {
			if(SkipReason!=null) {
				Assert.Inconclusive(SkipReason);
			}
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			Connect(DbMain);
			Security.CurUser=Userods.GetUser(1)??new Userod { UserNum=1,UserName="Admin" };
			if(!InventoryTracking.IsEnabled()) {
				Prefs.UpdateBool(PrefName.InventoryTrackingEnabled,true);
				Cache.Refresh(InvalidType.Prefs);
			}
		}

		///<summary>This run's scratch database for the given purpose, like test_inv_261102101500_1234_5678_main. At most 64 characters.</summary>
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
			if(Environment.GetEnvironmentVariable("INVENTORY_TEST_DB")=="skip") {
				return "INVENTORY_TEST_DB=skip";
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
			MySqlConnection connection=new MySqlConnection("Server="+Server+";Port=3306;User ID="+User+";Password="+Password
				+(database=="" ? "" : ";Database="+database)+";Connection Timeout=15;Allow User Variables=true;Default Command Timeout=600");
			connection.Open();
			return connection;
		}

		///<summary>Drops this fixture's leftovers of earlier runs (a crash, or INVENTORY_KEEP_DB=1) once they are older than 12 hours.</summary>
		public static void DropStale() {
			Regex regex=new Regex("^"+PREFIX+@"(\d{12})_\d+_\d+_[a-z0-9_]+$");
			List<string> listNames=new List<string>();
			using(MySqlConnection connection=OpenServer())
			using(MySqlCommand command=new MySqlCommand("SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME LIKE 'test\\_inv\\_%'",
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

		///<summary>An empty utf8mb3 database with the committed new-database file loaded into it, statement by statement, as db/README.md tells an
		///office to. Leaves the business layer connected to it.</summary>
		public static void CreateFromInitFile(string name) {
			Drop(name);
			using(MySqlConnection connection=OpenServer()) {
				Exec(connection,"CREATE DATABASE `"+name+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
			}
			string text=File.ReadAllText(FindRepoFile(RepoPathInitSql),new UTF8Encoding(false)).Replace("\r\n","\n");
			using(MySqlConnection connection=OpenServer(name)) {
				//Every statement of the generated file ends with a semicolon at the end of a line, and no value holds a raw line break.
				foreach(string statement in text.Split(new[] { ";\n" },StringSplitOptions.None)) {
					if(statement.Trim()=="") {
						continue;
					}
					Exec(connection,statement);
				}
			}
			Connect(name);
			if(!Prefs.DictIsNull()) {
				Prefs.ClearCache();
			}
			Cache.Refresh(InvalidType.Prefs);
		}

		public static void Drop(string name) {
			if(!name.StartsWith(PREFIX)) {
				throw new ArgumentException("Tests only drop databases whose name starts with "+PREFIX+".");
			}
			using(MySqlConnection connection=OpenServer()) {
				Exec(connection,"DROP DATABASE IF EXISTS `"+name+"`");
			}
		}

		///<summary>Drops the database unless INVENTORY_KEEP_DB=1, ignoring errors.</summary>
		public static void DropQuietly(string name) {
			if(Environment.GetEnvironmentVariable("INVENTORY_KEEP_DB")=="1") {
				return;
			}
			try {
				Drop(name);
			}
			catch(Exception) {
				//The stale clean-up of a later run drops it.
			}
		}

		///<summary>Points the business layer (DataCore, Db, the caches) at the database.</summary>
		public static void Connect(string name) {
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
			new DataConnection().SetDb(Server,name,User,Password,"","",DatabaseType.MySql,true);
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
