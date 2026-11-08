using System;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using System.Collections.Generic;
using System.Linq;
using UnitTestsCore;
using System.Reflection;
using System.Data;
using System.IO;
using DataConnectionBase;

namespace UnitTests {
	[TestClass]
	public abstract class TestBase {

		protected const string UnitTestUserName="UnitTest";
		protected const string UnitTestPassword="password";
		public TestContext TestContext { get; set; }
		protected Logger.IWriteLine _log;
		///<summary>Put this in the watch window to see the logger results.</summary>
		protected string _logStr="";
		///<summary>Indicates whether the user wanted to run all tests against Middle Tier.</summary>
		private static bool _runAllAgainstMiddleTier=false;
		///<summary>The local MariaDB server the tests use (port 3306, root with a blank password). Never another host.</summary>
		public const string TestServer="127.0.0.1";
		///<summary>Set this environment variable to 1 to keep the test run's database after the run, for a look at the data a failed test left.</summary>
		public const string KeepDbVariable="OPENPMS_UNITTESTS_KEEPDB";
		private static readonly string _unitTestDbName=MakeUnitTestDbName();

		///<summary>This run's own database on the local server: unittest(major)(minor)_(yyMMddHHmmss)_(process id). Initialize builds it fresh
		///from the seed dump and Cleanup drops it, so test runs in two worktrees at the same time never share a database and no run leaves one
		///behind.</summary>
		public static string UnitTestDbName {
			get {
				return _unitTestDbName;
			}
		}

		private static string MakeUnitTestDbName() {
			Version versionBusiness=Assembly.GetAssembly(typeof(PatientStatus)).GetName().Version;
			return "unittest"+versionBusiness.Major+versionBusiness.Minor+"_"+DateTime.Now.ToString("yyMMddHHmmss")
				+"_"+System.Diagnostics.Process.GetCurrentProcess().Id;
		}

		///<summary>A MySQL user name for a test that creates a server user and drops it again: the prefix, the time of day and the process id, so two
		///runs at the same time never share it. At most 32 characters with a prefix of up to 12.</summary>
		public static string GetRunMySqlUserName(string prefix) {
			return prefix+"_"+DateTime.Now.ToString("HHmmss")+"_"+System.Diagnostics.Process.GetCurrentProcess().Id;
		}

		///<summary>A random password for a MySQL user that a test creates and drops again (letters and digits only).</summary>
		public static string GetRandomMySqlPassword() {
			return "T"+Guid.NewGuid().ToString("N").Substring(0,15);
		}

		///<summary>This run's own folder under the user's temporary folder, named like the database. Cleanup deletes it.</summary>
		public static string RunFolder {
			get {
				return Path.Combine(Path.GetTempPath(),UnitTestDbName);
			}
		}

		///<summary>This run's A-to-Z images folder (the DocPath preference), in RunFolder. The seed dump's DocPath, C:\OpenDentImages\, does not exist
		///on a build computer, and the statement tests save each statement's PDF in the patient's folder.</summary>
		public static string AtoZFolder {
			get {
				return Path.Combine(RunFolder,"AtoZ");
			}
		}

		public TestBase() {
			_log=new LogDelegate((log,ll) => {
				_logStr+=log+"\r\n";
				//To view the output, in the Test Explorer in the bottom half, click 'Output'.
				Console.WriteLine(log);
			});
		}

		///<summary>Write to _logStr and the test output window.</summary>
		protected void LogWriteLine(string log) {
			_log.WriteLine(log,LogLevel.Verbose);
		}

		/// <summary> TestBase.Initialize will be called before the ClassInitialize and TestInitialize methods specific to each class.
		/// Do this first so that the time the Initialize and ClassInitialize methods take doesn't get counted in the test times. </summary>
		[AssemblyInitialize]
		public static void Initialize(TestContext context) {
			ODBuild.IsUnitTest=true;//Causes FormFriendlyException to throw rather than displaying a MessageBox.
			ODInitialize.Initialize();
			PdfSharpSetup.Init();//The PDF library's licence and font fallback, as OpenPmsDental sets them at start.
			if(!UnitTestsCore.DatabaseTools.SetDbConnection(UnitTestDbName,TestServer,"3306","root","",false)) {//Put this in a config file in the future.
				UnitTestsCore.DatabaseTools.SetDbConnection("",TestServer,"3306","root","",false);
				DatabaseTools.FreshFromDump(TestServer,"3306","root","",false);//this also sets database to be unittest.
			}
			else {
				//Clear the database before running the unittests (instead of after) for two reasons
				//1- if the cleanup is done using [TestCleanup], the cleanup will not be run if the user cancels in the middle of a test while debugging
				//2- if a test fails, we may want to look at the data in the db to see why it failed.
				UnitTestsCore.DatabaseTools.ClearDb();
			}
			if(!ODBuild.IsDebug()) {
				throw new Exception("Active solution configuration must be set to Debug for running unit tests.");
			}
			CreateAndSetUnitTestUser();
			//The UnitTestsCore helpers that work on a thread of their own (DataAction.RunPractice, as StatementT.CreateStatement does) connect by
			//the DentalOffice connection name, which the program reads from ConnectionStore.xml. A test host has no such file: point the name at
			//this run's database, as the ported suites in tests/ do.
			ConnectionStoreBase.OverrideConnection(ConnectionNames.DentalOffice,new CentralConnectionBase {
				ServerName=TestServer,DatabaseName=UnitTestDbName,MySqlUser="root",MySqlPassword="" });
			//A valid A-to-Z folder has a folder named A in it (FileAtoZ.GetValidPathFromString).
			Directory.CreateDirectory(Path.Combine(AtoZFolder,"A"));
			Prefs.UpdateString(PrefName.DocPath,AtoZFolder);
			_listCodeGroupsSeed=OpenPmsDentalBusiness.Crud.CodeGroupCrud.SelectMany("SELECT * FROM codegroup");
			RevertMiddleTierSettingsIfNeeded();//Set initial connection
		}

		///<summary>The code groups of this run's database as the seed dump and the fork migrations made them, read once by Initialize.</summary>
		private static List<CodeGroup> _listCodeGroupsSeed;

		///<summary>Puts the code groups back as the seed and the fork migrations made them (see _listCodeGroupsSeed) when a test changed them, so the
		///next test does not depend on which class ran before it. ProceduresTest and BenefitsTest replace them with Open Dental 24.3's defaults
		///(CodeGroupT.ResetToDefaults) before each of their tests, which drops the Insurance History rows of the 26.1 rebuild (codegroup.ShowInHistory),
		///and the eligibility tests that ran after them found no Insurance History category.</summary>
		private static void RestoreSeedCodeGroups() {
			if(_listCodeGroupsSeed==null) {
				return;
			}
			List<CodeGroup> listCodeGroupsNow=OpenPmsDentalBusiness.Crud.CodeGroupCrud.SelectMany("SELECT * FROM codegroup");
			bool isSame=listCodeGroupsNow.Count==_listCodeGroupsSeed.Count
				&& _listCodeGroupsSeed.All(x => listCodeGroupsNow.Any(y => y.CodeGroupNum==x.CodeGroupNum
					&& !OpenPmsDentalBusiness.Crud.CodeGroupCrud.UpdateComparison(y,x)));
			if(isSame) {
				return;
			}
			DataCore.NonQ("DELETE FROM codegroup");
			for(int i=0;i<_listCodeGroupsSeed.Count;i++) {
				OpenPmsDentalBusiness.Crud.CodeGroupCrud.InsertNoCache(_listCodeGroupsSeed[i].Copy(),true);
			}
			CodeGroups.RefreshCache();
		}

		///<summary>Drops this run's database (see UnitTestDbName) and deletes its folder (RunFolder), unless the environment variable KeepDbVariable
		///is 1.</summary>
		[AssemblyCleanup]
		public static void Cleanup() {
			ConnectionStoreBase.ClearConnectionDictionary();
			if(Environment.GetEnvironmentVariable(KeepDbVariable)=="1") {
				return;
			}
			RunTestsAgainstDirectConnection();
			ODException.SwallowAnyException(() => DataCore.NonQ("DROP DATABASE IF EXISTS `"+POut.String(UnitTestDbName)+"`"));
			ODException.SwallowAnyException(() => Directory.Delete(RunFolder,true));
		}

		[TestInitialize]
		public void ResetTest() {
			PrefT.RevertPrefChanges();
			RestoreSeedCodeGroups();
			//Every test starts on the real clock. Tests that need another time set it themselves (DateTime_.SetNow), and several never set it back:
			//after PrefsTests left the clock in 2024, AgingDataTests found the wrong last transaction date for a payment plan charge.
			DateTime_.ResetNow();
			AgingData.ActionGetAgingDataInvoked=null;
		}

		///<summary>This method will execute after each test in this class.</summary>
		[TestCleanup]
		public void TestCleanupBase() {
			DefT.ResetDefs();
		}

		protected static void RunTestsAgainstMiddleTier() {
			RunTestsAgainstMiddleTier(new OpenPmsDentalBusiness.WebServices.OpenDentalServerMockIIS());
		}

		protected static void RunTestsAgainstMiddleTier(OpenPmsDentalBusiness.WebServices.OpenDentalServerMockIIS mockServer) {
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientMT;
			OpenPmsDentalBusiness.WebServices.OpenDentalServerProxy.MockOpenDentalServerCur=mockServer;
		}

		protected static void RunTestsAgainstDirectConnection() {
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
			OpenPmsDentalBusiness.WebServices.OpenDentalServerProxy.MockOpenDentalServerCur=null;
		}

		protected static void RevertMiddleTierSettingsIfNeeded() {
			//If they want to run all tests against middle tier, do not revert.
			if(_runAllAgainstMiddleTier) {
				RunTestsAgainstMiddleTier();//Reset settings to default.
				return;
			}
			RunTestsAgainstDirectConnection();
		}

		public static void CreateUnitTestUser() {
			if(Userods.GetUserByName(UnitTestUserName,false)==null) {
				UserodT.CreateUser(UnitTestUserName,UnitTestPassword,new List<long> { 1 });
			}
		}

		public static void CreateAndSetUnitTestUser() {
			CreateUnitTestUser();
			//Get the Admin user, should always exist
			Security.CurUser=Userods.GetUserByName(UnitTestUserName,false);
			Security.PasswordTyped=UnitTestPassword;//For middle tier unit tests.
		}

		///<summary>Creates a database and a preference table so that DataConnection can pass our arbitrary test query.</summary>
		public static void CreateDatabaseIfNeeded(string databaseName) {
			string command=$"SELECT SCHEMA_NAME FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = '{POut.String(databaseName)}'";
			DataTable table=DataCore.GetTable(command);
			//Create a custom database if needed.
			if(table.Rows.Count==0) {
				DataCore.NonQ($"CREATE DATABASE `{POut.String(databaseName)}` CHARACTER SET utf8");
				DataCore.NonQ($@"CREATE TABLE `{POut.String(databaseName)}`.`preference` (
						`PrefName` varchar(255) NOT NULL DEFAULT '',
						`ValueString` text NOT NULL,
						`PrefNum` bigint(20) NOT NULL AUTO_INCREMENT,
						`Comments` text,
						PRIMARY KEY (`PrefNum`)
					) ENGINE=MyISAM AUTO_INCREMENT=1 DEFAULT CHARSET=utf8");
			}
		}

		public static void DropDatabase(string databaseName) {
			ODException.SwallowAnyException(() => DataCore.NonQ($"DROP DATABASE `{POut.String(databaseName)}`"));
		}

		///<summary>Executes the createTableStatement passed in if the table does not exists in the current database context.</summary>
		public static void CreateTableIfNeeded(string tableName,string createTableStatement) {
			string command=$@"SELECT * FROM INFORMATION_SCHEMA.TABLES
				WHERE TABLE_SCHEMA = DATABASE()
				AND TABLE_NAME = '{POut.String(tableName)}'
				LIMIT 1";
			DataTable table=DataCore.GetTable(command);
			if(table.Rows.Count==0) {
				DataCore.NonQ(createTableStatement);
			}
		}
	}
}
