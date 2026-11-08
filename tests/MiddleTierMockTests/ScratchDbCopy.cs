// The scratch database of the Middle Tier tests copied from the legacy UnitTests project by package S0-T2 of the .NET 10 plan
// (docs/specs/net10-plan.md): FeesTests, UserodsTests and ReportsTests of this folder. It is a copy of ScratchDb's database (the seed dump with
// the convert scripts, the fork migrations and the legacy harness's defaults, made once for the assembly), so these tests start from the same
// rows as the legacy harness did, and the 300 fee schedules, the locked-out users and the temporary tables they leave never reach the other
// tests of this assembly. The copy is made on the first test that needs it, on the local MariaDB (127.0.0.1:3306, root, blank password); every
// name starts with s0t2_ plus this run's id, and it is dropped in the classes' clean-up. The SELECT-only account of ReportsTests is made here
// too, with a name of the same form, at 127.0.0.1. Leftovers of a run that crashed are dropped by a later run once they are 12 hours old.
// Skipped (inconclusive) when ScratchDb skips. Synthetic rows only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using CodeBase;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.WebServices;
using UnitTestsCore;

namespace MiddleTierMockTests {
	///<summary>The s0t2_ copy of ScratchDb's database and the SELECT-only account of this run. A test connects to the copy in [TestInitialize]
	///(BeginTest) and goes back to ScratchDb's database in [TestCleanup] (EndTest), so a test of another class never runs on the copy.</summary>
	public static class ScratchDbCopy {
		public const string PREFIX="s0t2_";
		///<summary>Accounts are made at this host only: a TCP connection to 127.0.0.1 matches it whether or not the server resolves host names.</summary>
		public const string AccountHost="127.0.0.1";
		private static readonly TimeSpan _timeSpanStale=TimeSpan.FromHours(12);
		///<summary>Start time (UTC, yyMMddHHmmss), process id and a random number, as ScratchDb names its databases.</summary>
		private static readonly string _runId=DateTime.UtcNow.ToString("yyMMddHHmmss",CultureInfo.InvariantCulture)+"_"
			+System.Diagnostics.Process.GetCurrentProcess().Id+"_"+new Random().Next(1000,10000);
		public static readonly string DbName=Name("mt");
		///<summary>True from the start of the copy until it is dropped, so a half-made copy is dropped too (Drop uses IF EXISTS).</summary>
		private static bool _isCreated;
		///<summary>Null unless the copy could not be made; then every test that needs it fails with this.</summary>
		private static string _errorCopy;
		private static bool _isStaleDropped;

		///<summary>This run's name for a database or an account, like s0t2_261108101500_1234_5678_mt. At most 64 characters.</summary>
		public static string Name(string purpose) {
			string name=PREFIX+_runId+"_"+purpose;
			if(name.Length>64 || !Regex.IsMatch(name,"^[a-z0-9_]+$")) {
				throw new ArgumentException("Not a usable scratch name: "+name);
			}
			return name;
		}

		///<summary>Call from [TestInitialize]. Skips the test when ScratchDb skips; otherwise does what ScratchDb.BeginTest does before every test
		///(in ScratchDb's database, where the preferences of an earlier test are put back), makes the copy if this run has none yet, then connects
		///the business layer directly to the copy as the unit test user, with every cache emptied so that it fills again from the copy.</summary>
		public static void BeginTest() {
			ScratchDb.BeginTest();
			if(_errorCopy!=null) {
				Assert.Fail(_errorCopy);
			}
			if(!_isCreated) {
				try {
					DropStale();
					_isCreated=true;
					Copy(ScratchDb.DbName,DbName);
				}
				catch(Exception ex) {
					_errorCopy="Could not copy "+ScratchDb.DbName+" to "+DbName+": "+ex.GetBaseException().Message;
					Assert.Fail(_errorCopy);
				}
			}
			ScratchDb.Connect(DbName);
			Cache.ClearCaches();
			ScratchDb.SetUnitTestUser();
		}

		///<summary>Call from [TestCleanup]. Goes back to the direct connection (the role and the mock server of ScratchDb.UseDirectConnection), puts
		///back in the copy what ScratchDb.EndTest would (the preferences and definitions the test changed through PrefT and DefT), then connects to
		///ScratchDb's database again with every cache emptied, so the next test of another class sees none of the copy's rows.</summary>
		public static void EndTest() {
			ScratchDb.UseDirectConnection();
			if(ScratchDb.SkipReason!=null) {
				return;
			}
			if(_isCreated && _errorCopy==null) {
				ScratchDb.Connect(DbName);
				PrefT.RevertPrefChanges();
				DefT.ResetDefs();
			}
			ScratchDb.EndTest();
			Cache.ClearCaches();
			ScratchDb.SetUnitTestUser();
		}

		///<summary>A mock server that connects to the copy with its own settings, as ScratchDb.NewMockServer does for ScratchDb's database. The low
		///user is root unless another account is given.</summary>
		public static OpenDentalServerMockIIS NewMockServer(string userLow=ScratchDb.User,string passLow=ScratchDb.Password) {
			return new OpenDentalServerMockIIS(server:ScratchDb.Server,db:DbName,user:ScratchDb.User,password:ScratchDb.Password,userLow:userLow,
				passLow:passLow,dbType:DatabaseType.MySql);
		}

		///<summary>Call from [ClassCleanup] of each class that uses the copy. Drops the copy if this run made it; a later test makes a new one.</summary>
		public static void DropCopy() {
			ScratchDb.UseDirectConnection();
			if(!_isCreated || Environment.GetEnvironmentVariable("S0T_KEEP_DB")=="1") {
				return;
			}
			try {
				MySqlConnection.ClearAllPools();
				Drop(DbName);
				_isCreated=false;
			}
			catch(Exception) {
				//A later run drops it once it is stale.
			}
		}

		///<summary>Copies every table of dbFrom (structure, indexes, engine and rows) into a new database dbTo, as root. Refuses a source with
		///views, triggers, routines or events, which a table copy would leave out.</summary>
		private static void Copy(string dbFrom,string dbTo) {
			if(!dbFrom.StartsWith(ScratchDb.PREFIX)) {
				throw new ArgumentException("Only ScratchDb's database is copied.");
			}
			Drop(dbTo);
			using(MySqlConnection connection=ScratchDb.OpenServer()) {
				string countOther=ScratchDb.Scalar(connection,"SELECT "
					+"(SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA='"+dbFrom+"' AND TABLE_TYPE<>'BASE TABLE')"
					+"+(SELECT COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA='"+dbFrom+"')"
					+"+(SELECT COUNT(*) FROM information_schema.ROUTINES WHERE ROUTINE_SCHEMA='"+dbFrom+"')"
					+"+(SELECT COUNT(*) FROM information_schema.EVENTS WHERE EVENT_SCHEMA='"+dbFrom+"')");
				if(countOther!="0") {
					throw new InvalidOperationException(dbFrom+" has views, triggers, routines or events ("+countOther+"); a table copy would leave them out.");
				}
				List<string> listTables=new List<string>();
				using(MySqlCommand command=new MySqlCommand("SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA='"+dbFrom+"' "
					+"AND TABLE_TYPE='BASE TABLE' ORDER BY TABLE_NAME",connection))
				using(MySqlDataReader reader=command.ExecuteReader())
				{
					while(reader.Read()) {
						listTables.Add(reader.GetString(0));
					}
				}
				if(listTables.Count==0) {
					throw new InvalidOperationException(dbFrom+" has no tables.");
				}
				//A row whose key is 0 keeps it, instead of taking the next AUTO_INCREMENT value.
				ScratchDb.Exec(connection,"SET SESSION sql_mode='NO_AUTO_VALUE_ON_ZERO'");
				ScratchDb.Exec(connection,"CREATE DATABASE `"+dbTo+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
				foreach(string table in listTables) {
					if(table.Contains("`")) {
						throw new InvalidOperationException("Unexpected table name: "+table);
					}
					ScratchDb.Exec(connection,"CREATE TABLE `"+dbTo+"`.`"+table+"` LIKE `"+dbFrom+"`.`"+table+"`");
					ScratchDb.Exec(connection,"INSERT INTO `"+dbTo+"`.`"+table+"` SELECT * FROM `"+dbFrom+"`.`"+table+"`");
				}
			}
		}

		///<summary>Makes the account name@127.0.0.1 with the password, allowed to SELECT in the copy and nothing else (no other database, no
		///CREATE, no INSERT), as tests/DbGrantsTests/GrantRun.cs makes its account. An account of the same name is dropped first.</summary>
		public static void CreateSelectOnlyAccount(string name,string password) {
			if(password.Contains("'") || password.Contains("\\")) {
				throw new ArgumentException("Use a password without quotes or backslashes.");
			}
			DropAccount(name);
			using(MySqlConnection connection=ScratchDb.OpenServer()) {
				ScratchDb.Exec(connection,"CREATE USER "+AccountSql(name)+" IDENTIFIED BY '"+password+"'");
				//In a GRANT, '_' in a database name is a wildcard for any one character, so each is escaped: the grant names the copy only.
				ScratchDb.Exec(connection,"GRANT SELECT ON `"+DbName.Replace("_","\\_")+"`.* TO "+AccountSql(name));
			}
		}

		///<summary>Drops the account if it exists, after closing the pooled connections that may still be logged in as it.</summary>
		public static void DropAccount(string name) {
			MySqlConnection.ClearAllPools();
			using(MySqlConnection connection=ScratchDb.OpenServer()) {
				ScratchDb.Exec(connection,"DROP USER IF EXISTS "+AccountSql(name));
			}
		}

		///<summary>Whether the account exists on the server (any host).</summary>
		public static bool AccountExists(string name) {
			CheckName(name);
			using(MySqlConnection connection=ScratchDb.OpenServer()) {
				return ScratchDb.Scalar(connection,"SELECT COUNT(*) FROM mysql.user WHERE User='"+name+"'")!="0";
			}
		}

		///<summary>Whether the database exists on the server.</summary>
		public static bool DatabaseExists(string name) {
			CheckName(name);
			using(MySqlConnection connection=ScratchDb.OpenServer()) {
				return ScratchDb.Scalar(connection,"SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME='"+name+"'")!="0";
			}
		}

		///<summary>The account as CREATE USER, GRANT and DROP USER name it.</summary>
		private static string AccountSql(string name) {
			CheckName(name);
			return "'"+name+"'@'"+AccountHost+"'";
		}

		private static void CheckName(string name) {
			if(!name.StartsWith(PREFIX) || !Regex.IsMatch(name,"^[a-z0-9_]+$")) {
				throw new ArgumentException("Tests only make and drop databases and accounts whose name starts with "+PREFIX+".");
			}
		}

		public static void Drop(string name) {
			CheckName(name);
			using(MySqlConnection connection=ScratchDb.OpenServer()) {
				ScratchDb.Exec(connection,"DROP DATABASE IF EXISTS `"+name+"`");
			}
		}

		///<summary>Once per run: drops the s0t2_ databases and accounts of earlier runs (a crash, or S0T_KEEP_DB=1) once they are older than 12
		///hours.</summary>
		private static void DropStale() {
			if(_isStaleDropped) {
				return;
			}
			_isStaleDropped=true;
			Regex regex=new Regex("^"+PREFIX+@"(\d{12})_\d+_\d+_[a-z0-9_]+$");
			List<string> listDatabases=new List<string>();
			List<string> listAccounts=new List<string>();
			using(MySqlConnection connection=ScratchDb.OpenServer()) {
				using(MySqlCommand command=new MySqlCommand("SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME LIKE 's0t2\\_%'",connection))
				using(MySqlDataReader reader=command.ExecuteReader())
				{
					while(reader.Read()) {
						listDatabases.Add(reader.GetString(0));
					}
				}
				using(MySqlCommand command=new MySqlCommand("SELECT User FROM mysql.user WHERE User LIKE 's0t2\\_%' AND Host='"+AccountHost+"'",connection))
				using(MySqlDataReader reader=command.ExecuteReader())
				{
					while(reader.Read()) {
						listAccounts.Add(reader.GetString(0));
					}
				}
			}
			foreach(string name in listDatabases) {
				if(IsStale(regex,name)) {
					ODException.SwallowAnyException(() => Drop(name));//Another run may be dropping it at the same time.
				}
			}
			foreach(string name in listAccounts) {
				if(IsStale(regex,name)) {
					ODException.SwallowAnyException(() => DropAccount(name));
				}
			}
		}

		private static bool IsStale(Regex regex,string name) {
			Match match=regex.Match(name);
			if(!match.Success || !DateTime.TryParseExact(match.Groups[1].Value,"yyMMddHHmmss",CultureInfo.InvariantCulture,
				DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out DateTime dateStarted))
			{
				return false;
			}
			return DateTime.UtcNow-dateStarted>=_timeSpanStale;
		}
	}
}
