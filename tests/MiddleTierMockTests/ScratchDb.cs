// The scratch database for the S0-T Middle Tier and ODThread tests on the local MariaDB (127.0.0.1:3306, root, blank password), copied from
// tests/ApptTests/ScratchDb.cs: the seed dump with the convert scripts and the fork migrations run on it. The steps the legacy UnitTests harness
// ran after them follow (UnitTests/DatabaseTools.cs FreshFromDump and UnitTests/Misc/TestBase.cs Initialize): the default auto codes, procedure
// buttons and quick-add procedures, the two CDCREC codes, and the user the Middle Tier calls log in as. Every name starts with s0t_ plus this
// run's id, so a test can never drop anything else and runs from other worktrees never collide. Synthetic rows only. Skipped (inconclusive) when
// the server does not answer, or when S0T_TEST_DB=skip. S0T_KEEP_DB=1 keeps the database for a look afterwards.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using CodeBase;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;
using OpenPmsDentalBusiness.WebServices;
using UnitTestsCore;

namespace MiddleTierMockTests {
	[TestClass]
	public class ScratchDb {
		public const string Server="127.0.0.1";
		public const string User="root";
		public const string Password="";
		public const string PREFIX="s0t_";
		///<summary>The user the legacy harness logged in as (UnitTests/Misc/TestBase.cs). DtoProcessor checks these credentials on every call.</summary>
		public const string UnitTestUserName="UnitTest";
		public const string UnitTestPassword="password";
		private static readonly TimeSpan _timeSpanStale=TimeSpan.FromHours(12);
		///<summary>Start time (UTC, yyMMddHHmmss), process id and a random number.</summary>
		private static readonly string _runId=DateTime.UtcNow.ToString("yyMMddHHmmss",CultureInfo.InvariantCulture)+"_"
			+System.Diagnostics.Process.GetCurrentProcess().Id+"_"+new Random().Next(1000,10000);
		public static readonly string DbName=Name("mt");
		///<summary>Null when the database is ready; otherwise why the database tests are skipped.</summary>
		public static string SkipReason;
		private static bool _isBuilt;

		///<summary>Once for the assembly, as the legacy TestBase.Initialize did: the unit test flag, the program's start-up initialization, the PDF
		///library set-up, then the scratch database and its user.</summary>
		[AssemblyInitialize]
		public static void AssemblyInitialize(TestContext testContext) {
			ODBuild.IsUnitTest=true;//Causes FormFriendlyException to throw rather than displaying a MessageBox.
			ODInitialize.Initialize();
			PdfSharpSetup.Init();
			SkipReason=GetSkipReason();
			if(SkipReason!=null) {
				return;
			}
			try {
				DropStale();
				_isBuilt=true;//Set before Create, so AssemblyCleanup drops a half-built database too (Drop uses IF EXISTS).
				Create(DbName);
			}
			catch(Exception ex) {
				SkipReason="Could not build the scratch database: "+ex.GetBaseException().Message;
			}
		}

		[AssemblyCleanup]
		public static void AssemblyCleanup() {
			UseDirectConnection();
			if(!_isBuilt || Environment.GetEnvironmentVariable("S0T_KEEP_DB")=="1") {
				return;
			}
			try {
				Drop(DbName);
			}
			catch(Exception) {
				//A later run drops it once it is stale.
			}
		}

		///<summary>This run's database name for the given purpose, like s0t_261108101500_1234_5678_mt. At most 64 characters.</summary>
		public static string Name(string purpose) {
			string name=PREFIX+_runId+"_"+purpose;
			if(name.Length>64 || !Regex.IsMatch(name,"^[a-z0-9_]+$")) {
				throw new ArgumentException("Not a usable scratch database name: "+name);
			}
			return name;
		}

		///<summary>Call from [TestInitialize]. Skips the test when there is no database; otherwise connects to it directly (no Middle Tier) as the
		///unit test user and does what the legacy TestBase.ResetTest did before every test.</summary>
		public static void BeginTest() {
			if(SkipReason!=null) {
				Assert.Inconclusive(SkipReason);
			}
			UseDirectConnection();
			Connect(DbName);
			SetUnitTestUser();
			PrefT.RevertPrefChanges();
			AgingData.ActionGetAgingDataInvoked=null;
		}

		///<summary>Call from [TestCleanup]. Goes back to the direct connection (no test runs as a Middle Tier client by accident), then does what the
		///legacy TestBase.TestCleanupBase did after every test.</summary>
		public static void EndTest() {
			UseDirectConnection();
			if(SkipReason!=null) {
				return;
			}
			Connect(DbName);
			DefT.ResetDefs();
		}

		///<summary>Every Middle Tier call of this thread goes through the mock server: the arguments and the result are serialized and run through
		///DtoProcessor, as UnitTests/Misc/TestBase.cs RunTestsAgainstMiddleTier did.</summary>
		public static void UseMockMiddleTier(OpenDentalServerMockIIS mockServer) {
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientMT;
			OpenDentalServerProxy.MockOpenDentalServerCur=mockServer;
		}

		///<summary>A mock server that connects to the scratch database with its own settings, the low user included, as the legacy MiddleTierTests
		///set it up.</summary>
		public static OpenDentalServerMockIIS NewMockServer() {
			return new OpenDentalServerMockIIS(server:Server,db:DbName,user:User,password:Password,userLow:User,passLow:Password,dbType:DatabaseType.MySql);
		}

		///<summary>As UnitTests/Misc/TestBase.cs RunTestsAgainstDirectConnection.</summary>
		public static void UseDirectConnection() {
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
			OpenDentalServerProxy.MockOpenDentalServerCur=null;
		}

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
			//FreshFromDump set the password of user 1 to 'pass' for the Middle Tier tests.
			DataCore.NonQ("UPDATE userod SET Password='qhd+xdy/iMpe3xcjbBmB6A==' WHERE UserNum=1");
			AddCdcrecCodes();
			if(Userods.GetUserByName(UnitTestUserName,false)==null) {
				UserodT.CreateUser(UnitTestUserName,UnitTestPassword,new List<long> { 1 });
			}
			SetUnitTestUser();
		}

		///<summary>The legacy TestBase.CreateAndSetUnitTestUser: the unit test user is the current user, and its typed password is what the Middle
		///Tier calls send.</summary>
		public static void SetUnitTestUser() {
			Security.CurUser=Userods.GetUserByName(UnitTestUserName,false);
			Security.PasswordTyped=UnitTestPassword;
		}

		///<summary>The few CDCREC codes the legacy harness added (UnitTests/DatabaseTools.cs AddCdcrecCodes).</summary>
		private static void AddCdcrecCodes() {
			if(DataCore.GetScalar("SELECT COUNT(*) FROM cdcrec")!="0") {
				return;
			}
			Cdcrecs.Insert(new Cdcrec() {
				CdcrecCode="2106-3",
				HeirarchicalCode="R5",
				Description="WHITE"
			});
			Cdcrecs.Insert(new Cdcrec() {
				CdcrecCode="2135-2",
				HeirarchicalCode="E1",
				Description="HISPANIC OR LATINO"
			});
		}

		///<summary>Connects the business layer directly to the database, with the low user and password set as the legacy harness did
		///(UnitTestsCore.DatabaseTools.SetDbConnection).</summary>
		public static void Connect(string name) {
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
			DataConnection.DBtype=DatabaseType.MySql;
			new DataConnection().SetDb(Server,name,User,Password,User,Password,DatabaseType.MySql,true,DevDbTls.GetSslCa(Server));
		}

		///<summary>Drops this fixture's leftovers of earlier runs (a crash, or S0T_KEEP_DB=1) once they are older than 12 hours.</summary>
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

		///<summary>The repository root: the first folder above the test assembly that holds LICENSE and NOTICE.</summary>
		public static string RepoRoot {
			get {
				DirectoryInfo directoryInfo=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
				while(directoryInfo!=null) {
					if(File.Exists(Path.Combine(directoryInfo.FullName,"LICENSE")) && File.Exists(Path.Combine(directoryInfo.FullName,"NOTICE"))) {
						return directoryInfo.FullName;
					}
					directoryInfo=directoryInfo.Parent;
				}
				throw new InvalidOperationException("LICENSE and NOTICE not found above "+AppDomain.CurrentDomain.BaseDirectory);
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
