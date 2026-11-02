// Scratch database for the routine maintenance tests, on the local MariaDB only (127.0.0.1:3306, root, blank password). It gets the Open Dental
// 26.1.70 schema from the reference embedded in OpenPmsDentalBusiness (SchemaVerifier.GetReference), then the routine maintenance tables from the
// feature's own fork migration steps, and synthetic rows only; nothing here reads or copies an office database. Every name starts with
// test_maint_. Database tests are skipped (inconclusive) when no server answers, or when MAINT_TEST_DB=skip.
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
using OpenPmsDentalBusiness.ForkMigrations.FeatureSteps;

namespace MaintenanceTests {
	///<summary>Creates, connects to and drops this run's scratch database. Every name starts with "test_maint_" so the tests can never drop
	///anything else.</summary>
	[TestClass]
	public class TestDb {
		public const string Server="127.0.0.1";
		public const string User="root";
		public const string Password="";
		public const string PREFIX="test_maint_";
		///<summary>Scratch databases left behind by a run that started this long ago are dropped by later runs. No run takes nearly this long.</summary>
		private static readonly TimeSpan _timeSpanStale=TimeSpan.FromHours(12);
		///<summary>Unique to this test run: the start time (UTC, yyMMddHHmmss), the process id and a random number, because several worktrees and
		///test runners share the one local server.</summary>
		public static readonly string DbName=PREFIX+DateTime.UtcNow.ToString("yyMMddHHmmss")+"_"+System.Diagnostics.Process.GetCurrentProcess().Id
			+"_"+new Random().Next(1000,10000);
		///<summary>Null when the scratch database is ready; otherwise why database tests are skipped.</summary>
		public static string SkipReason { get; private set; }
		///<summary>What the maintenance steps logged when they created the tables of the scratch database.</summary>
		public static List<string> ListChangesFirstRun=new List<string>();
		private static bool _isCreated;

		///<summary>The users of every database test: 1 Admin (Security.CurUser), 2 Assistant A and 3 Assistant B in group 10 (Sterilization), 4 a
		///hidden user in that group, 5 Front Desk alone in group 20.</summary>
		public const long USER_ADMIN=1;
		public const long USER_ASSISTANT_A=2;
		public const long USER_ASSISTANT_B=3;
		public const long USER_HIDDEN=4;
		public const long USER_FRONT=5;
		public const long GROUP_STERILIZATION=10;
		public const long GROUP_FRONT=20;

		///<summary>The tables the tests write. Emptied before every database test.</summary>
		private static readonly string[] _arrayTablesWritten=new string[] { "alertitem","alertread","equipment","maintfield","maintitem","maintrecord",
			"maintrecordvalue","maintstep","preference","securitylog","securityloghash","usergroup","usergroupattach","userod" };

		[AssemblyInitialize]
		public static void AssemblyInitialize(TestContext testContext) {
			SetCultureUs();
			if(Environment.GetEnvironmentVariable("MAINT_TEST_DB")=="skip") {
				SkipReason="MAINT_TEST_DB=skip";
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
			Connect();
			//The routine maintenance tables come from the feature's own steps, as on an office database.
			ForkMigrationContext context=new ForkMigrationContext(DbName);
			foreach(ForkMigrationStep step in GetMaintenanceSteps()) {
				step.Apply(context);
			}
			ListChangesFirstRun=context.ListChanges.ToList();
			Security.CurUser=new Userod { UserNum=USER_ADMIN,UserName="Admin" };
		}

		[AssemblyCleanup]
		public static void AssemblyCleanup() {
			if(!_isCreated || Environment.GetEnvironmentVariable("MAINT_KEEP_DB")=="1") {
				return;
			}
			try {
				Drop(DbName);
			}
			catch(Exception) {
				//Leaving the scratch database behind is harmless; a later run drops it once it is stale.
			}
		}

		///<summary>The routine maintenance steps of this build (levels 2026110201 to 2026110209), in level order.</summary>
		public static List<ForkMigrationStep> GetMaintenanceSteps() {
			return ForkMigrationRunner.GetSteps().Where(x => x.Level>=2026110201 && x.Level<=2026110209).OrderBy(x => x.Level).ToList();
		}

		public static void Connect() {
			new DataConnection().SetDb(Server,DbName,User,Password,"","",DatabaseType.MySql,true);
		}

		///<summary>Call from TestInitialize of a database test: marks the test inconclusive when there is no scratch database, otherwise empties the
		///tables the tests write, restores the preferences and adds the synthetic users and groups.</summary>
		public static void ResetOrSkip() {
			if(SkipReason!=null) {
				Assert.Inconclusive(SkipReason);
			}
			SetCultureUs();
			Connect();
			foreach(string table in _arrayTablesWritten) {
				DataCore.NonQ("DELETE FROM "+table);
			}
			//Every preference exists, so code that reads one the tests do not care about gets "0" instead of "invalid pref name".
			List<string> listPrefRows=Enum.GetNames(typeof(PrefName)).Where(x => x!=nameof(PrefName.RandomPrimaryKeys))
				.Select(x => "('"+POut.String(x)+"','0')").ToList();
			DataCore.NonQ("INSERT INTO preference (PrefName,ValueString) VALUES "+string.Join(",",listPrefRows));
			DataCore.NonQ("INSERT INTO preference (PrefName,ValueString) VALUES ('RandomPrimaryKeys','0')");
			DataCore.NonQ("INSERT INTO userod (UserNum,UserName,Password,IsHidden) VALUES "
				+"("+USER_ADMIN+",'Admin','',0),("+USER_ASSISTANT_A+",'Assistant A','',0),("+USER_ASSISTANT_B+",'Assistant B','',0),"
				+"("+USER_HIDDEN+",'Former Assistant','',1),("+USER_FRONT+",'Front Desk','',0)");
			DataCore.NonQ("INSERT INTO usergroup (UserGroupNum,Description) VALUES ("+GROUP_STERILIZATION+",'Sterilization'),("+GROUP_FRONT+",'Front')");
			DataCore.NonQ("INSERT INTO usergroupattach (UserNum,UserGroupNum) VALUES ("+USER_ASSISTANT_A+","+GROUP_STERILIZATION+"),"
				+"("+USER_ASSISTANT_B+","+GROUP_STERILIZATION+"),("+USER_HIDDEN+","+GROUP_STERILIZATION+"),("+USER_FRONT+","+GROUP_FRONT+")");
			Prefs.RefreshCache();
			Userods.RefreshCache();
			UserGroups.RefreshCache();
			UserGroupAttaches.RefreshCache();
			Security.CurUser=Userods.GetUser(USER_ADMIN);
		}

		public static void SetCultureUs() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			Thread.CurrentThread.CurrentUICulture=new CultureInfo("en-US");
		}

		///<summary>Runs action with this thread's culture and the default culture of new threads set to cultureName, as on a workstation of that
		///culture (DateTimeOD sets DefaultThreadCurrentCulture to the user's culture), then puts both back.</summary>
		public static void RunInCulture(string cultureName,Action action) {
			CultureInfo cultureOld=CultureInfo.CurrentCulture;
			CultureInfo cultureDefaultOld=CultureInfo.DefaultThreadCurrentCulture;
			CultureInfo culture=new CultureInfo(cultureName);
			try {
				CultureInfo.CurrentCulture=culture;
				CultureInfo.DefaultThreadCurrentCulture=culture;
				action();
			}
			finally {
				CultureInfo.CurrentCulture=cultureOld;
				CultureInfo.DefaultThreadCurrentCulture=cultureDefaultOld;
			}
		}

		///<summary>Drops scratch databases that earlier runs left behind (a crash, or MAINT_KEEP_DB=1), once they are older than _timeSpanStale.
		///Never drops the database of a run that may still be going, and never a name without this naming scheme.</summary>
		private static void DropStaleScratchDatabases() {
			Regex regex=new Regex("^"+PREFIX+@"(\d{12})_\d+_\d+$");
			List<string> listNames=new List<string>();
			using(MySqlConnection connection=OpenServer())
			using(MySqlCommand command=new MySqlCommand("SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME LIKE 'test\\_maint\\_%'",connection))
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

		///<summary>Creates an empty scratch database of this run's naming scheme (for a migration test that needs its own), and returns its name.
		///</summary>
		public static string CreateExtra(string suffix) {
			string name=DbName+"_"+suffix;
			using(MySqlConnection connection=OpenServer()) {
				Exec(connection,"CREATE DATABASE `"+name+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
			}
			return name;
		}

		public static void Drop(string name) {
			if(!name.StartsWith(PREFIX)) {
				throw new ArgumentException("Tests only drop databases whose name starts with "+PREFIX);
			}
			using(MySqlConnection connection=OpenServer()) {
				Exec(connection,"DROP DATABASE IF EXISTS `"+name+"`");
			}
		}

		public static MySqlConnection OpenServer(string database=null) {
			string connectionString="Server="+Server+";Port=3306;User ID="+User+";Password="+Password+";Connection Timeout=15";
			if(database!=null) {
				connectionString+=";Database="+database;
			}
			MySqlConnection connection=new MySqlConnection(connectionString);
			connection.Open();
			return connection;
		}

		public static void Exec(MySqlConnection connection,string command) {
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
