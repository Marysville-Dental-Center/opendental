// The scratch database of the EHR tests that need one (package EHR-1 of docs/specs/ehr-rebuild-plan.md, section 6), on the local MariaDB
// (127.0.0.1:3306, root, blank password), built as tests/InventoryTests/InvDb.cs builds its own: the committed db/init/new-database.sql loaded
// statement by statement, then every fork migration of this build run on it. Every name starts with ehr1_ plus this run's id, so a test can never
// drop anything else and runs from other worktrees never collide. It is built once, by the first test that asks for it (the tests of EHR-0 need
// none and never wait for it), and dropped after the last test of the assembly. Synthetic rows only. A test that needs it is skipped
// (inconclusive) when the server does not answer or EHR_TEST_DB=skip; EHR_KEEP_DB=1 keeps it for a look afterwards.
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
using OpenPmsDentalBusiness.WebServices;

namespace EhrTests {
	[TestClass]
	public class ScratchDb {
		public const string Server="127.0.0.1";
		public const string User="root";
		public const string Password="";
		public const string PREFIX="ehr1_";
		public const string RepoPathInitSql=@"db\init\new-database.sql";
		private static readonly TimeSpan _timeSpanStale=TimeSpan.FromHours(12);
		///<summary>Start time (UTC, yyMMddHHmmss), process id and a random number.</summary>
		private static readonly string _runId=DateTime.UtcNow.ToString("yyMMddHHmmss",CultureInfo.InvariantCulture)+"_"
			+System.Diagnostics.Process.GetCurrentProcess().Id+"_"+new Random().Next(1000,10000);
		///<summary>This run's database.</summary>
		public static readonly string DbName=Name("main");
		private static readonly object _lockBuild=new object();
		private static bool _isBuildTried;
		private static bool _isBuilt;
		///<summary>The culture Begin found, put back by End.</summary>
		private static CultureInfo _cultureBefore;
		///<summary>Null when the database is ready; otherwise why the database tests are skipped.</summary>
		public static string SkipReason;

		[AssemblyCleanup]
		public static void AssemblyCleanup() {
			UseDirectConnection();
			if(!_isBuilt || Environment.GetEnvironmentVariable("EHR_KEEP_DB")=="1") {
				return;
			}
			try {
				Drop(DbName);
			}
			catch(Exception) {
				//The stale clean-up of a later run drops it.
			}
		}

		///<summary>This run's database name for the given purpose, like ehr1_261108101500_1234_5678_main. At most 64 characters.</summary>
		public static string Name(string purpose) {
			string name=PREFIX+_runId+"_"+purpose;
			if(name.Length>64 || !Regex.IsMatch(name,"^[a-z0-9_]+$")) {
				throw new ArgumentException("Not a usable scratch database name: "+name);
			}
			return name;
		}

		///<summary>Call from [TestInitialize]: builds the database on the first call, skips the test when there is none, otherwise connects to it
		///directly (no Middle Tier) as user 1 (Admin) in the en-US culture.</summary>
		public static void Begin() {
			lock(_lockBuild) {
				if(!_isBuildTried) {
					_isBuildTried=true;
					Build();
				}
			}
			if(SkipReason!=null) {
				Assert.Inconclusive(SkipReason);
			}
			//Lans.g returns the English text in en-US, and touches the language table in any other culture. End puts the test's culture back.
			if(_cultureBefore==null) {
				_cultureBefore=Thread.CurrentThread.CurrentCulture;
			}
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			UseDirectConnection();
			Connect(DbName);
			Security.CurUser=Userods.GetUser(1);
			Security.PasswordTyped="";
		}

		///<summary>Call from [TestCleanup]: back to the direct connection as user 1, so no later test runs as a Middle Tier client by accident.</summary>
		public static void End() {
			if(_cultureBefore!=null) {
				Thread.CurrentThread.CurrentCulture=_cultureBefore;
				_cultureBefore=null;
			}
			UseDirectConnection();
			if(!_isBuilt || SkipReason!=null) {
				return;
			}
			Connect(DbName);
			Userods.RefreshCache();
			Security.CurUser=Userods.GetUser(1);
			Security.PasswordTyped="";
		}

		private static void Build() {
			SkipReason=GetSkipReason();
			if(SkipReason!=null) {
				return;
			}
			try {
				DropStale();
				_isBuilt=true;//Set before the database exists, so AssemblyCleanup drops a half-built one too (Drop uses IF EXISTS).
				CreateFromInitFile(DbName);
				ForkMigrationRunner.Run();
				Cache.Refresh(InvalidType.Prefs,InvalidType.Providers,InvalidType.Security);
				Userods.RefreshCache();
				Security.CurUser=Userods.GetUser(1);
			}
			catch(Exception ex) {
				SkipReason="Could not build the scratch database: "+ex.GetBaseException().Message;
			}
		}

		///<summary>Null when the local server answers with a blank global sql_mode (as OpenPmsDental.exe sets it); otherwise why database tests are
		///skipped.</summary>
		public static string GetSkipReason() {
			if(Environment.GetEnvironmentVariable("EHR_TEST_DB")=="skip") {
				return "EHR_TEST_DB=skip";
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

		///<summary>An empty utf8mb3 database with the committed new-database file loaded into it, statement by statement, as db/README.md tells an
		///office to. Leaves the business layer connected to it.</summary>
		private static void CreateFromInitFile(string name) {
			Drop(name);
			using(MySqlConnection connection=OpenServer()) {
				Exec(connection,"CREATE DATABASE `"+name+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
			}
			string text=File.ReadAllText(TestPaths.Combine(RepoPathInitSql),new UTF8Encoding(false)).Replace("\r\n","\n");
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

		///<summary>Points the business layer (DataCore, Db, the caches) at the database, with no Middle Tier.</summary>
		public static void Connect(string name) {
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
			new DataConnection().SetDb(Server,name,User,Password,"","",DatabaseType.MySql,true);
		}

		///<summary>Every Middle Tier call of this thread goes through the mock server: the arguments and the result are serialized and run through
		///DtoProcessor on the mock server's own thread, connected to this run's database, as in tests/MiddleTierMockTests.</summary>
		public static void UseMockMiddleTier() {
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientMT;
			OpenDentalServerProxy.MockOpenDentalServerCur=new OpenDentalServerMockIIS(server:Server,db:DbName,user:User,password:Password,userLow:User,
				passLow:Password,dbType:DatabaseType.MySql);
		}

		public static void UseDirectConnection() {
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
			OpenDentalServerProxy.MockOpenDentalServerCur=null;
		}

		///<summary>Drops this fixture's leftovers of earlier runs (a crash, or EHR_KEEP_DB=1) once they are older than 12 hours.</summary>
		private static void DropStale() {
			Regex regex=new Regex("^"+PREFIX+@"(\d{12})_\d+_\d+_[a-z0-9_]+$");
			List<string> listNames=new List<string>();
			using(MySqlConnection connection=OpenServer())
			using(MySqlCommand command=new MySqlCommand("SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME LIKE 'ehr1\\_%'",connection))
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

		#region Synthetic rows
		private static int _countMade;

		///<summary>A made-up name unique in this run, letters only (as a provider's name may be), starting with the given letters.</summary>
		public static string UniqueName(string start) {
			int count=Interlocked.Increment(ref _countMade);
			//The count in base 26 with the letters a to z, so 1 is "b" and 26 is "ba". Each run has its own database, so the count is enough.
			return start+ToLetters(count);
		}

		private static string ToLetters(int number) {
			string letters="";
			do {
				letters=(char)('a'+number%26)+letters;
				number/=26;
			}
			while(number>0);
			return letters;
		}

		///<summary>A provider with these names, in the database and the cache.</summary>
		public static Provider MakeProvider(string lName,string fName) {
			Provider provider=new Provider();
			provider.Abbr=(lName+fName).Length>15 ? (lName+fName).Substring(0,15) : lName+fName;
			provider.LName=lName;
			provider.FName=fName;
			Providers.Insert(provider);
			Providers.RefreshCache();
			return provider;
		}

		///<summary>A user group with exactly these permissions and a user in it, with a known password (for the Middle Tier mock, which checks the
		///credentials of every call). The caches the permission checks read are refreshed.</summary>
		public static Userod MakeUser(string password,params EnumPermType[] arrayPermTypes) {
			UserGroup userGroup=new UserGroup();
			userGroup.Description=UniqueName("Ehrgroup");
			userGroup.UserGroupNum=UserGroups.Insert(userGroup);
			foreach(EnumPermType permType in arrayPermTypes) {
				GroupPermission groupPermission=new GroupPermission();
				groupPermission.UserGroupNum=userGroup.UserGroupNum;
				groupPermission.PermType=permType;
				groupPermission.NewerDate=DateTime.MinValue;
				groupPermission.NewerDays=0;
				GroupPermissions.Insert(groupPermission);
			}
			Userod userod=new Userod();
			userod.UserName=UniqueName("ehruser");
			userod.SetPassword(Authentication.GenerateLoginDetails(password,HashTypes.SHA3_512));
			userod.UserNum=Userods.Insert(userod,new List<long> { userGroup.UserGroupNum });
			UserGroups.RefreshCache();
			GroupPermissions.RefreshCache();
			Userods.RefreshCache();
			UserGroupAttaches.RefreshCache();
			return Userods.GetUser(userod.UserNum);
		}

		///<summary>Makes the user the current user, with the password the Middle Tier calls send.</summary>
		public static void LogOn(Userod userod,string password) {
			Security.CurUser=userod;
			Security.PasswordTyped=password;
		}

		///<summary>The highest SecurityLogNum so far, so a test reads only the entries it made.</summary>
		public static long GetLastSecurityLogNum() {
			return PIn.Long(DataCore.GetScalar("SELECT COALESCE(MAX(SecurityLogNum),0) FROM securitylog"));
		}

		///<summary>The security log entries after the given number, oldest first.</summary>
		public static List<SecurityLog> GetSecurityLogsAfter(long securityLogNum) {
			return OpenPmsDentalBusiness.Crud.SecurityLogCrud.SelectMany("SELECT * FROM securitylog WHERE SecurityLogNum>"+POut.Long(securityLogNum)
				+" ORDER BY SecurityLogNum");
		}
		#endregion Synthetic rows
	}
}
