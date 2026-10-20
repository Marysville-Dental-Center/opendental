// Integration tests for the QuickBooks Online rows against a scratch MariaDB database on this computer: rows inserted at runtime, encrypted
// connection values, and the rotating refresh token's compare-and-swap. Skipped (inconclusive) when no server answers at 127.0.0.1:3306 as root
// with a blank password, or when VENDORDIRECT_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenDentBusiness;
using OpenDentBusiness.Qbo;

namespace VendorDirectTests.Qbo {
	[TestClass]
	public class QboDbTests {
		private static readonly string DB_NAME=ScratchDb.Name("vendordirect_qbo_tests");
		private static string _skipReason;
		private static long _programNum;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			if(Environment.GetEnvironmentVariable("VENDORDIRECT_TEST_DB")=="skip") {
				_skipReason="VENDORDIRECT_TEST_DB=skip";
				return;
			}
			try {
				using(MySqlConnection connection=new MySqlConnection("Server=127.0.0.1;Port=3306;User ID=root;Password=;Connection Timeout=3")) {
					connection.Open();
					Exec(connection,"DROP DATABASE IF EXISTS "+DB_NAME);
					Exec(connection,"CREATE DATABASE "+DB_NAME+" CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
					Exec(connection,"USE "+DB_NAME);
					//The same definitions as Open Dental 24.3's program, programproperty and preference tables.
					Exec(connection,"CREATE TABLE program (ProgramNum bigint(20) NOT NULL AUTO_INCREMENT,ProgName varchar(100) DEFAULT '',"
						+"ProgDesc varchar(100) DEFAULT '',Enabled tinyint(1) unsigned NOT NULL DEFAULT 0,Path text NOT NULL DEFAULT '',"
						+"CommandLine text NOT NULL DEFAULT '',Note text DEFAULT NULL,PluginDllName varchar(255) NOT NULL,ButtonImage text NOT NULL,"
						+"FileTemplate text NOT NULL,FilePath varchar(255) NOT NULL,IsDisabledByHq tinyint(4) NOT NULL,CustErr varchar(255) NOT NULL,"
						+"PRIMARY KEY (ProgramNum)) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
					Exec(connection,"CREATE TABLE programproperty (ProgramPropertyNum bigint(20) NOT NULL AUTO_INCREMENT,ProgramNum bigint(20) NOT NULL,"
						+"PropertyDesc varchar(255) DEFAULT '',PropertyValue text DEFAULT NULL,ComputerName varchar(255) NOT NULL,ClinicNum bigint(20) NOT NULL,"
						+"IsMasked tinyint(4) NOT NULL,IsHighSecurity tinyint(4) NOT NULL,PRIMARY KEY (ProgramPropertyNum),KEY ClinicNum (ClinicNum)) "
						+"ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
					Exec(connection,"CREATE TABLE preference (PrefName varchar(255) NOT NULL DEFAULT '',ValueString text NOT NULL,"
						+"PrefNum bigint(20) NOT NULL AUTO_INCREMENT,Comments text DEFAULT NULL,PRIMARY KEY (PrefNum),KEY PrefName (PrefName)) "
						+"ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
					Exec(connection,"INSERT INTO preference (PrefName,ValueString) VALUES ('RandomPrimaryKeys','0')");
					//As ConvertDatabases left it: the program row, disabled. TestInitialize adds its five original rows.
					Exec(connection,"INSERT INTO program (ProgName,ProgDesc,Enabled,Path,CommandLine,Note,PluginDllName,ButtonImage,FileTemplate,FilePath,"
						+"IsDisabledByHq,CustErr) VALUES ('QuickBooksOnline','QuickBooks Online',0,'','','','','','','',0,'')");
				}
				new DataConnection().SetDb("127.0.0.1",DB_NAME,"root","","root","",DatabaseType.MySql,true);
				Prefs.RefreshCache();
				Programs.RefreshCache();
				ProgramProperties.RefreshCache();
				_programNum=Programs.GetProgramNum(ProgramName.QuickBooksOnline);
			}
			catch(Exception ex) {
				_skipReason="No scratch MariaDB database: "+ex.GetBaseException().Message;
			}
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null) {
				return;
			}
			try {
				using(MySqlConnection connection=new MySqlConnection("Server=127.0.0.1;Port=3306;User ID=root;Password=;Connection Timeout=3")) {
					connection.Open();
					Exec(connection,"DROP DATABASE IF EXISTS "+DB_NAME);
				}
			}
			catch(Exception) {
				//Leaving the scratch database behind is harmless; the next run drops it.
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			//Each test starts from the rows an Open Dental database has, with the token rows encrypted by another installation. Each test class sets
			//the shared connection; take it back too.
			using(MySqlConnection connection=new MySqlConnection("Server=127.0.0.1;Port=3306;User ID=root;Password=;Database="+DB_NAME+";Connection Timeout=3")) {
				connection.Open();
				Exec(connection,"DELETE FROM programproperty");
				Exec(connection,"INSERT INTO programproperty (ProgramNum,PropertyDesc,PropertyValue,ComputerName,ClinicNum,IsMasked,IsHighSecurity) VALUES "
					+"(1,'Deposit Accounts','Checking,35','',0,0,0),"
					+"(1,'Income Accounts','Fees,79','',0,0,0),"
					+"(1,'Class Refs','','',0,0,0),"
					+"(1,'Refresh Token','odv2e$AAAAfromanotherinstall','',0,1,0),"
					+"(1,'Realm ID','odv2e$BBBBfromanotherinstall','',0,1,1)");
			}
			new DataConnection().SetDb("127.0.0.1",DB_NAME,"root","","root","",DatabaseType.MySql,true);
			Programs.RefreshCache();
			ProgramProperties.RefreshCache();
		}

		private static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.ExecuteNonQuery();
			}
		}

		[TestMethod]
		public void A_MigratedDatabase_ReadsAsNotConnected_AndKeepsTheLists() {
			QboSettings settings=QboSettings.Load();
			Assert.AreEqual(_programNum,settings.ProgramNum);
			Assert.IsFalse(settings.IsEnabled,"Off by default.");
			Assert.IsFalse(settings.IsConnected);
			Assert.IsTrue(settings.HasUnreadableLegacyValues);
			Assert.AreEqual("Checking,35",settings.DepositAccounts);
			Assert.AreEqual("",new QboProgramPropertyStore(_programNum).ReadCurrent(),"Open Dental's token is never sent.");
		}

		[TestMethod]
		public void B_EnsurePropertyRows_InsertsMissingRowsOnce_AndRaisesSecretFlags() {
			Assert.IsTrue(QboSettings.EnsurePropertyRows());
			ProgramProperties.RefreshCache();
			Assert.IsFalse(QboSettings.EnsurePropertyRows(),"Idempotent.");
			Assert.AreEqual("Production",ProgramProperties.GetOneFromDb(_programNum,QboSettings.PropEnvironment,0).PropertyValue);
			foreach(string desc in new[] { QboSettings.PropClientSecret,QboSettings.PropRefreshToken,QboSettings.PropRealmId }) {
				ProgramProperty programProperty=ProgramProperties.GetOneFromDb(_programNum,desc,0);
				Assert.IsTrue(programProperty.IsMasked && programProperty.IsHighSecurity,desc);
			}
			foreach(string desc in new[] { QboSettings.PropClientId,QboSettings.PropRedirectUri,QboSettings.PropRelayPort,QboSettings.PropRefreshTokenExpires,
				QboSettings.PropCompanyName })
			{
				ProgramProperty programProperty=ProgramProperties.GetOneFromDb(_programNum,desc,0);
				Assert.IsNotNull(programProperty,desc);
				Assert.IsFalse(programProperty.IsMasked,desc);
			}
			Assert.AreEqual("Checking,35",ProgramProperties.GetOneFromDb(_programNum,QboSettings.PropDepositAccounts,0).PropertyValue,"Existing values stay.");
		}

		[TestMethod]
		public void C_ClientSettingsAndConnection_RoundTripEncrypted() {
			Assert.IsTrue(QboSettings.SaveClientSettings(QboEnvironment.Sandbox," client-1 ","http://localhost:8765/callback",0,"secret-1"));
			Assert.IsTrue(QboSettings.SaveConnection("rt-1",new DateTime(2027,1,27,0,0,0,DateTimeKind.Utc),"4620816365165566530","Sandbox Company",
				clearEntityLists:false));
			ProgramProperties.RefreshCache();
			QboSettings settings=QboSettings.Load();
			Assert.IsTrue(settings.IsConnected);
			Assert.AreEqual("client-1",settings.ClientId);
			Assert.AreEqual("secret-1",settings.ClientSecret);
			Assert.AreEqual(QboEnvironment.Sandbox,settings.Environment);
			Assert.AreEqual("4620816365165566530",settings.RealmId);
			Assert.AreEqual("Sandbox Company",settings.CompanyName);
			Assert.AreEqual("Checking,35",settings.DepositAccounts,"Same company: the lists stay.");
			SecretsTestHelper.AssertRoundTrip("secret-1",ProgramProperties.GetOneFromDb(_programNum,QboSettings.PropClientSecret,0).PropertyValue);
			SecretsTestHelper.AssertRoundTrip("rt-1",ProgramProperties.GetOneFromDb(_programNum,QboSettings.PropRefreshToken,0).PropertyValue);
			Assert.AreEqual("",ProgramProperties.GetValFromDb(_programNum,"Access Token"),"Access tokens are never stored.");
			//Keeping the secret: null leaves it alone.
			Assert.IsTrue(QboSettings.SaveClientSettings(QboEnvironment.Sandbox,"client-1","http://localhost:8765/callback",8766,null));
			ProgramProperties.RefreshCache();
			Assert.AreEqual("secret-1",QboSettings.Load().ClientSecret);
			Assert.AreEqual(8766,QboSettings.Load().RelayPort);
		}

		[TestMethod]
		public void D_Store_RotatesWithCompareAndSwap_AndKeepsTheExpiry() {
			QboSettings.SaveConnection("rt-1",null,"123","",clearEntityLists:false);
			QboProgramPropertyStore store=new QboProgramPropertyStore(_programNum);
			Assert.AreEqual("rt-1",store.ReadCurrent());
			Assert.IsFalse(store.TryReplace("rt-stale","rt-2"),"A value another workstation replaced is never overwritten.");
			Assert.IsTrue(store.TryReplace("rt-1","rt-2"));
			Assert.AreEqual("rt-2",store.ReadCurrent());
			Assert.IsNull(store.ReadRefreshTokenExpiresUtc());
			DateTime dateTime=new DateTime(2027,2,1,8,30,0,DateTimeKind.Utc);
			store.WriteRefreshTokenExpiresUtc(dateTime);
			Assert.AreEqual(dateTime,store.ReadRefreshTokenExpiresUtc());
		}

		[TestMethod]
		public void E_AnotherCompany_ClearsTheLists_AndClearConnectionBlanksTokensOnly() {
			QboSettings.SaveEntityLists("Checking,35","Fees,79","Hygiene,5000");
			Assert.IsTrue(QboSettings.SaveConnection("rt-9",null,"999","Other Company",clearEntityLists:true));
			ProgramProperties.RefreshCache();
			QboSettings settings=QboSettings.Load();
			Assert.AreEqual("",settings.DepositAccounts);
			Assert.AreEqual("",settings.ClassRefs);
			QboSettings.SaveEntityLists("Checking,35","","");
			QboSettings.ClearConnection();
			ProgramProperties.RefreshCache();
			settings=QboSettings.Load();
			Assert.IsFalse(settings.HasRefreshToken);
			Assert.AreEqual("",settings.RealmId);
			Assert.AreEqual("",settings.CompanyName);
			Assert.IsFalse(settings.HasUnreadableLegacyValues);
			Assert.AreEqual("Checking,35",settings.DepositAccounts,"Disconnect keeps the lists.");
		}
	}
}
