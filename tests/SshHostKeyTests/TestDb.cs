// Scratch database for the SSH host key tests, on the local MariaDB only (127.0.0.1:3306, root, blank password). It gets the Open Dental 26.1.70
// schema from the reference embedded in OpenPmsDentalBusiness (SchemaVerifier.GetReference), then the sshhostkey table from the feature's own
// fork migration step, and synthetic rows only; nothing here reads or copies an office database. Every name starts with test_fussh_. Database
// tests are skipped (inconclusive) when no server answers.
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

namespace SshHostKeyTests {
	///<summary>Creates (on first use), connects to and drops this run's scratch database. Every name starts with "test_fussh_" so the tests can
	///never drop anything else.</summary>
	[TestClass]
	public class TestDb {
		public const string Server="127.0.0.1";
		public const string User="root";
		public const string Password="";
		public const string PREFIX="test_fussh_";
		///<summary>Scratch databases left behind by a run that started this long ago are dropped by later runs. No run takes nearly this long.</summary>
		private static readonly TimeSpan _timeSpanStale=TimeSpan.FromHours(12);
		///<summary>Unique to this test run: the start time (UTC, yyMMddHHmmss), the process id and a random number, because several worktrees and
		///test runners share the one local server.</summary>
		public static readonly string DbName=PREFIX+DateTime.UtcNow.ToString("yyMMddHHmmss")+"_"+System.Diagnostics.Process.GetCurrentProcess().Id
			+"_"+new Random().Next(1000,10000);
		///<summary>What the SSH host key step logged when it created the table of the scratch database.</summary>
		public static List<string> ListChangesFirstRun=new List<string>();
		private static string _skipReason;
		private static bool _isTried;
		private static bool _isCreated;
		private static readonly object _lock=new object();

		///<summary>1 Admin in group 1 (Security Admin), 2 Front Desk in group 2 (no Security Admin).</summary>
		public const long USER_ADMIN=1;
		public const long USER_FRONT=2;
		public const long GROUP_ADMIN=1;
		public const long GROUP_FRONT=2;

		///<summary>The tables the tests write. Emptied before every database test.</summary>
		private static readonly string[] _arrayTablesWritten=new string[] { "grouppermission","preference","securitylog","securityloghash","sshhostkey",
			"usergroup","usergroupattach","userod" };

		[AssemblyCleanup]
		public static void AssemblyCleanup() {
			if(!_isCreated || Environment.GetEnvironmentVariable("FUSSH_KEEP_DB")=="1") {
				return;
			}
			try {
				Drop(DbName);
			}
			catch(Exception) {
				//Leaving the scratch database behind is harmless; a later run drops it once it is stale.
			}
		}

		///<summary>Creates the scratch database the first time: the 26.1.70 reference schema, then the SSH host key step, as on an office database.
		///</summary>
		private static void EnsureCreated() {
			lock(_lock) {
				if(_isTried) {
					return;
				}
				_isTried=true;
				try {
					using(MySqlConnection connection=OpenServer()) {
						Exec(connection,"CREATE DATABASE `"+DbName+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
					}
				}
				catch(Exception ex) {
					_skipReason="No local MariaDB at "+Server+":3306 as "+User+" with a blank password: "+ex.GetBaseException().Message;
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
				ForkMigrationContext context=new ForkMigrationContext(DbName);
				new SshHostKeyTableStep().Apply(context);
				ListChangesFirstRun=context.ListChanges.ToList();
			}
		}

		public static void Connect() {
			new DataConnection().SetDb(Server,DbName,User,Password,"","",DatabaseType.MySql,true,DevDbTls.GetSslCa(Server));
		}

		///<summary>Call from TestInitialize of a database test: marks the test inconclusive when there is no scratch database, otherwise empties the
		///tables the tests write, adds every preference and the synthetic users and groups, and logs on as Admin.</summary>
		public static void ResetOrSkip() {
			EnsureCreated();
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			Thread.CurrentThread.CurrentUICulture=new CultureInfo("en-US");
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
			Connect();
			foreach(string table in _arrayTablesWritten) {
				DataCore.NonQ("DELETE FROM "+table);
			}
			//Every preference exists, so code that reads one the tests do not care about gets "0" instead of "invalid pref name".
			List<string> listPrefRows=Enum.GetNames(typeof(PrefName)).Select(x => "('"+POut.String(x)+"','0')").ToList();
			DataCore.NonQ("INSERT INTO preference (PrefName,ValueString) VALUES "+string.Join(",",listPrefRows));
			DataCore.NonQ("INSERT INTO userod (UserNum,UserName,Password,IsHidden) VALUES ("+USER_ADMIN+",'Admin','',0),("+USER_FRONT+",'Front Desk','',0)");
			DataCore.NonQ("INSERT INTO usergroup (UserGroupNum,Description) VALUES ("+GROUP_ADMIN+",'Admin'),("+GROUP_FRONT+",'Front')");
			DataCore.NonQ("INSERT INTO usergroupattach (UserNum,UserGroupNum) VALUES ("+USER_ADMIN+","+GROUP_ADMIN+"),("+USER_FRONT+","+GROUP_FRONT+")");
			DataCore.NonQ("INSERT INTO grouppermission (NewerDate,NewerDays,UserGroupNum,PermType,FKey) VALUES('0001-01-01',0,"+GROUP_ADMIN+","
				+(int)EnumPermType.SecurityAdmin+",0)");
			Prefs.RefreshCache();
			Userods.RefreshCache();
			UserGroups.RefreshCache();
			UserGroupAttaches.RefreshCache();
			GroupPermissions.RefreshCache();
			LogOn(USER_ADMIN);
		}

		public static void LogOn(long userNum) {
			Security.CurUser=Userods.GetUser(userNum);
		}

		///<summary>Drops scratch databases that earlier runs left behind (a crash, or FUSSH_KEEP_DB=1), once they are older than _timeSpanStale. Never
		///drops the database of a run that may still be going, and never a name without this naming scheme.</summary>
		private static void DropStaleScratchDatabases() {
			Regex regex=new Regex("^"+PREFIX+@"(\d{12})_\d+_\d+(_\w+)?$");
			List<string> listNames=new List<string>();
			using(MySqlConnection connection=OpenServer())
			using(MySqlCommand command=new MySqlCommand("SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME LIKE 'test\\_fussh\\_%'",connection))
			using(MySqlDataReader reader=command.ExecuteReader()) {
				while(reader.Read()) {
					listNames.Add(reader.GetString(0));
				}
			}
			foreach(string name in listNames.Where(x => !x.StartsWith(DbName))) {
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
			MySqlConnection connection=new MySqlConnection(DevDbTls.Apply(connectionString));
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
