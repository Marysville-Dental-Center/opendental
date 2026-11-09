// A scratch database on the local MariaDB (127.0.0.1:3306, root, blank password) for the e-mail template test moved here from the legacy
// UnitTests project (package S0-T of the .NET 10 plan), copied from tests/MiddleTierMockTests/ScratchDb.cs: the seed dump with the convert scripts
// and the fork migrations run on it, then the steps the legacy harness ran after them (UnitTests/DatabaseTools.cs FreshFromDump) and its unit test
// user. Every name starts with s0t_ plus this run's id, so a test can never drop anything else and runs from other worktrees never collide.
// Synthetic rows only. Skipped (inconclusive) when the server does not answer, or when S0T_TEST_DB=skip. S0T_KEEP_DB=1 keeps the database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using DataConnectionBase;
using MySqlConnector;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;
using UnitTestsCore;

namespace LibWebTests {
	///<summary>Creates, connects to and drops the s0t_ scratch databases of this assembly.</summary>
	public static class ScratchDb {
		public const string Server="127.0.0.1";
		public const string User="root";
		public const string Password="";
		public const string PREFIX="s0t_";
		///<summary>The user the legacy harness logged in as (UnitTests/Misc/TestBase.cs).</summary>
		public const string UnitTestUserName="UnitTest";
		public const string UnitTestPassword="password";
		private static readonly TimeSpan _timeSpanStale=TimeSpan.FromHours(12);
		///<summary>Start time (UTC, yyMMddHHmmss), process id and a random number.</summary>
		private static readonly string _runId=DateTime.UtcNow.ToString("yyMMddHHmmss",CultureInfo.InvariantCulture)+"_"
			+System.Diagnostics.Process.GetCurrentProcess().Id+"_"+new Random().Next(1000,10000);

		///<summary>This run's database name for the given purpose, like s0t_261108101500_1234_5678_web. At most 64 characters.</summary>
		public static string Name(string purpose) {
			string name=PREFIX+_runId+"_"+purpose;
			if(name.Length>64 || !Regex.IsMatch(name,"^[a-z0-9_]+$")) {
				throw new ArgumentException("Not a usable scratch database name: "+name);
			}
			return name;
		}

		///<summary>Null when the server answers with a blank global sql_mode (the convert scripts need it, as OpenPmsDental.exe sets at start-up);
		///otherwise why the database tests are skipped.</summary>
		public static string GetSkipReason() {
			if(Environment.GetEnvironmentVariable("S0T_TEST_DB")=="skip") {
				return "S0T_TEST_DB=skip";
			}
			try {
				using(MySqlConnection connection=OpenServer()) {
					string sqlMode=Scalar(connection,"SELECT @@GLOBAL.sql_mode");
					if(sqlMode!="" && sqlMode.ToUpperInvariant()!="NO_AUTO_CREATE_USER") {
						return "Global sql_mode is '"+sqlMode+"'; the convert scripts need a blank sql_mode.";
					}
				}
				return null;
			}
			catch(Exception ex) {
				return "No local MariaDB at "+Server+":3306 as "+User+" with a blank password: "+ex.GetBaseException().Message;
			}
		}

		public static MySqlConnection OpenServer(string database="") {
			MySqlConnection connection=new MySqlConnection(DevDbTls.Apply("Server="+Server+";Port=3306;User ID="+User+";Password="+Password
				+(database=="" ? "" : ";Database="+database)+";Connection Timeout=15;Allow User Variables=true;Default Command Timeout=600"));
			connection.Open();
			return connection;
		}

		///<summary>Builds the database and leaves the business layer connected to it as the unit test user.</summary>
		public static void Create(string name) {
			Drop(name);
			using(MySqlConnection connection=OpenServer()) {
				Exec(connection,"CREATE DATABASE `"+name+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
			}
			Connect(name);
			Security.CurUser=new Userod { UserNum=1,UserName="s0t" };
			DataCore.NonQ(File.ReadAllText(FindRepoFile(Path.Combine("UnitTests","Resources","dump.txt")),new UTF8Encoding(false)));
			Prefs.UpdateStringNoCache(PrefName.CorruptedDatabase,"1");
			ConvertDatabases.FromVersion=new Version(DataCore.GetScalar("SELECT ValueString FROM preference WHERE PrefName='DataBaseVersion'"));
			ConvertDatabases.InvokeConvertMethods();
			Prefs.UpdateStringNoCache(PrefName.CorruptedDatabase,"0");
			ForkMigrationRunner.Run();
			//Specific caches are invalid after the convert scripts and migrations (FreshFromDump).
			Cache.Refresh(InvalidType.Prefs,InvalidType.Programs);
			AutoCodes.SetToDefault();
			ProcButtons.SetToDefault();
			ProcedureCodes.ResetApptProcsQuickAdd();
			ProcedureCodes.RefreshCache();
			if(Userods.GetUserByName(UnitTestUserName,false)==null) {
				UserodT.CreateUser(UnitTestUserName,UnitTestPassword,new List<long> { 1 });
			}
			SetUnitTestUser();
		}

		///<summary>The legacy TestBase.CreateAndSetUnitTestUser.</summary>
		public static void SetUnitTestUser() {
			Security.CurUser=Userods.GetUserByName(UnitTestUserName,false);
			Security.PasswordTyped=UnitTestPassword;
		}

		///<summary>Connects the business layer directly to the database (no Middle Tier), as the legacy harness did.</summary>
		public static void Connect(string name) {
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
			DataConnection.DBtype=DatabaseType.MySql;
			new DataConnection().SetDb(Server,name,User,Password,User,Password,DatabaseType.MySql,true,DevDbTls.GetSslCa(Server));
		}

		///<summary>Drops this assembly's s0t_ leftovers of earlier runs (a crash, or S0T_KEEP_DB=1) once they are older than 12 hours.</summary>
		public static void DropStale() {
			Regex regex=new Regex("^"+PREFIX+@"(\d{12})_\d+_\d+_[a-z0-9_]+$");
			List<string> listNames=new List<string>();
			using(MySqlConnection connection=OpenServer())
			using(MySqlCommand command=new MySqlCommand("SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME LIKE 's0t\\_%'",connection))
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

		public static void Drop(string name) {
			if(!name.StartsWith(PREFIX)) {
				throw new ArgumentException("Tests only drop databases whose name starts with "+PREFIX+".");
			}
			using(MySqlConnection connection=OpenServer()) {
				Exec(connection,"DROP DATABASE IF EXISTS `"+name+"`");
			}
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
				return value==null || value==DBNull.Value ? "" : Convert.ToString(value,CultureInfo.InvariantCulture);
			}
		}

		///<summary>Walks up from the test assembly's folder to the repository root and returns the path of a file in it.</summary>
		public static string FindRepoFile(string relativePath) {
			DirectoryInfo directoryInfo=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
			while(directoryInfo!=null) {
				string candidate=Path.Combine(directoryInfo.FullName,relativePath);
				if(File.Exists(candidate)) {
					return candidate;
				}
				directoryInfo=directoryInfo.Parent;
			}
			throw new FileNotFoundException("Not found above the test folder: "+relativePath);
		}
	}
}
