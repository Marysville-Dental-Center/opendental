// One scratch database for the ODOPTS database tests, on the local MariaDB only (127.0.0.1:3306, root, blank password): a fresh 24.3.63 database
// built the way tools/New-DevDatabase.ps1 builds one (the seed dump UnitTests\Resources\dump.txt and every ConvertDatabases method), then
// upgraded by the fork migration runner to the current schema. Nothing here reads or copies an office database; every row the tests add is made
// up. Skipped (inconclusive) when the server does not answer, or when ODOPTS_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;

namespace ODOPTSTests {
	///<summary>Creates, connects to and drops this run's scratch database. Every name starts with "test_odopts_" so the tests can never drop anything
	///else.</summary>
	[TestClass]
	public class ScratchDb {
		public const string Server="127.0.0.1";
		public const string User="root";
		public const string Password="";
		private const string PREFIX="test_odopts_";
		///<summary>Scratch databases left behind by a run that started this long ago are dropped by later runs. No run takes nearly this long.</summary>
		private static readonly TimeSpan _timeSpanStale=TimeSpan.FromHours(12);
		///<summary>Unique to this test run: the start time (UTC, yyMMddHHmmss), the process id and a random number, because several worktrees and
		///test runners share the one local server.</summary>
		public static readonly string DbName=PREFIX+DateTime.UtcNow.ToString("yyMMddHHmmss")+"_"+System.Diagnostics.Process.GetCurrentProcess().Id
			+"_"+new Random().Next(1000,10000)+"_main";
		///<summary>Null when the scratch database is ready; otherwise why database tests are skipped.</summary>
		public static string SkipReason { get; private set; }
		private static bool _isCreated;

		[AssemblyInitialize]
		public static void AssemblyInitialize(TestContext testContext) {
			SetCulture();
			SkipReason=GetServerSkipReason();
			if(SkipReason!=null) {
				return;
			}
			try {
				DropStaleScratchDatabases();
				using(MySqlConnection connection=OpenServer()) {
					Exec(connection,"CREATE DATABASE `"+DbName+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
				}
				_isCreated=true;
				Connect();
				DataCore.NonQ(File.ReadAllText(FindRepoFile(Path.Combine("UnitTests","Resources","dump.txt")),new UTF8Encoding(false)));
				Security.CurUser=new Userod { UserNum=1,UserName="Synthetic" };
				Prefs.UpdateStringNoCache(PrefName.CorruptedDatabase,"1");
				ConvertDatabases.FromVersion=new Version(DataCore.GetScalar("SELECT ValueString FROM preference WHERE PrefName='DataBaseVersion'"));
				ConvertDatabases.InvokeConvertMethods();
				Prefs.UpdateStringNoCache(PrefName.CorruptedDatabase,"0");
				Cache.Refresh(InvalidType.Prefs);
				ForkMigrationRunner.Run();
				Cache.Refresh(InvalidType.AllLocal);
				Security.CurUser=Userods.GetUser(1)??new Userod { UserNum=1,UserName="Synthetic" };
			}
			catch(Exception ex) {
				SkipReason="Could not build the scratch database: "+ex.GetBaseException().Message;
			}
		}

		[AssemblyCleanup]
		public static void AssemblyCleanup() {
			if(!_isCreated || Environment.GetEnvironmentVariable("ODOPTS_KEEP_DB")=="1") {
				return;
			}
			try {
				Drop(DbName);
			}
			catch(Exception) {
				//Leaving the scratch database behind is harmless; a later run drops it once it is stale.
			}
		}

		///<summary>Call from [TestInitialize] of a database test: marks the test inconclusive when there is no scratch database, otherwise connects to
		///it, sets the culture to en-US (labels are compared in English) and removes the eight ODOPTS rows, so each test starts from a database without
		///them, as a database from 24.3 is.</summary>
		public static void Begin() {
			if(SkipReason!=null) {
				Assert.Inconclusive(SkipReason);
			}
			SetCulture();
			Connect();
			Security.CurUser=Userods.GetUser(1)??new Userod { UserNum=1,UserName="Synthetic" };
			DataCore.NonQ("DELETE FROM preference WHERE PrefName IN ("+string.Join(",",OdOptions.ArrayPrefNames.Select(x => "'"+POut.String(x)+"'"))+")");
			Prefs.RefreshCache();
		}

		///<summary>Lans.g returns the English text without touching the database only in en-US.</summary>
		public static void SetCulture() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			Thread.CurrentThread.CurrentUICulture=new CultureInfo("en-US");
		}

		///<summary>Points the business layer (DataCore, Db, the caches) at the scratch database.</summary>
		public static void Connect() {
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
			new DataConnection().SetDb(Server,DbName,User,Password,"","",DatabaseType.MySql,true);
		}

		///<summary>The stored value of a preference row, or null when there is no row.</summary>
		public static string GetPrefRaw(string prefName) {
			System.Data.DataTable table=DataCore.GetTable("SELECT ValueString FROM preference WHERE PrefName='"+POut.String(prefName)+"'");
			if(table.Rows.Count==0) {
				return null;
			}
			return table.Rows[0]["ValueString"].ToString();
		}

		///<summary>Writes a preference row as Open Dental would have left it (insert or update), then reloads the preference cache.</summary>
		public static void SetPrefRaw(string prefName,string value) {
			if(GetPrefRaw(prefName)==null) {
				DataCore.NonQ("INSERT INTO preference (PrefName,ValueString) VALUES ('"+POut.String(prefName)+"','"+POut.String(value)+"')");
			}
			else {
				DataCore.NonQ("UPDATE preference SET ValueString='"+POut.String(value)+"' WHERE PrefName='"+POut.String(prefName)+"'");
			}
			Prefs.RefreshCache();
		}

		private static string GetServerSkipReason() {
			if(Environment.GetEnvironmentVariable("ODOPTS_TEST_DB")=="skip") {
				return "ODOPTS_TEST_DB=skip";
			}
			try {
				using(MySqlConnection connection=OpenServer()) {
					string sqlMode=Scalar(connection,"SELECT @@GLOBAL.sql_mode");
					if(sqlMode!="" && sqlMode.ToUpperInvariant()!="NO_AUTO_CREATE_USER") {
						return "Global sql_mode is '"+sqlMode+"'; the convert scripts need a blank sql_mode, as OpenPmsDental.exe sets at startup.";
					}
				}
				return null;
			}
			catch(Exception ex) {
				return "No local MariaDB at "+Server+":3306 as "+User+" with a blank password: "+ex.GetBaseException().Message;
			}
		}

		///<summary>Drops scratch databases that earlier runs left behind (a crash, or ODOPTS_KEEP_DB=1), once they are older than _timeSpanStale.
		///Never drops the database of a run that may still be going, and never a name without this naming scheme.</summary>
		private static void DropStaleScratchDatabases() {
			Regex regex=new Regex("^"+PREFIX+@"(\d{12})_\d+_\d+_[a-z0-9_]+$");
			List<string> listNames=new List<string>();
			using(MySqlConnection connection=OpenServer())
			using(MySqlCommand command=new MySqlCommand("SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME LIKE 'test\\_odopts\\_%'",
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

		private static MySqlConnection OpenServer() {
			MySqlConnection connection=new MySqlConnection("Server="+Server+";Port=3306;User ID="+User+";Password="+Password
				+";Connection Timeout=15;Allow User Variables=true");
			connection.Open();
			return connection;
		}

		private static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.CommandTimeout=600;
				mySqlCommand.ExecuteNonQuery();
			}
		}

		private static string Scalar(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				object value=mySqlCommand.ExecuteScalar();
				return value==null || value==DBNull.Value ? null : value.ToString();
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

		///<summary>Reads a file of the repository (the folder above the test assembly that holds LICENSE and NOTICE).</summary>
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
