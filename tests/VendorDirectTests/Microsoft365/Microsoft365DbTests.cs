// Integration tests for the Microsoft 365 program row, its settings, the sign-in gate and the emailaddress cache store, against a scratch MariaDB
// database on this computer. Skipped (inconclusive) when no server answers at 127.0.0.1:3306 as root with a blank password, or when
// VENDORDIRECT_TEST_DB=skip. No network call to Microsoft is made. Licensed under the GNU General Public License, version 2 or later.
// See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenDentBusiness;
using OpenDentBusiness.Microsoft365;

namespace VendorDirectTests.Microsoft365 {
	[TestClass]
	public class Microsoft365DbTests {
		private static readonly string DB_NAME=ScratchDb.Name("vendordirect_m365_tests");
		private const string CLIENT_ID="0b6c1f7e-3f0e-4c55-9a51-1d2f5e6a7b8c";
		private const string TENANT_ID="9d8c7b6a-5e4f-4a3b-8c2d-1e0f9a8b7c6d";
		private static string _skipReason;

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
					//The same definitions as Open Dental 24.3's tables.
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
					Exec(connection,"CREATE TABLE emailaddress (EmailAddressNum bigint(20) NOT NULL AUTO_INCREMENT,SMTPserver varchar(255) NOT NULL,"
						+"EmailUsername varchar(255) NOT NULL,EmailPassword varchar(255) NOT NULL,ServerPort int(11) NOT NULL,UseSSL tinyint(4) NOT NULL,"
						+"SenderAddress varchar(255) NOT NULL,Pop3ServerIncoming varchar(255) NOT NULL,ServerPortIncoming int(11) NOT NULL,"
						+"UserNum bigint(20) NOT NULL,AccessToken varchar(2000) NOT NULL,RefreshToken text NOT NULL,DownloadInbox tinyint(4) NOT NULL,"
						+"QueryString varchar(1000) NOT NULL,AuthenticationType tinyint(4) NOT NULL,PRIMARY KEY (EmailAddressNum),KEY UserNum (UserNum)) "
						+"ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
					Exec(connection,"CREATE TABLE signalod (SignalNum bigint(20) NOT NULL AUTO_INCREMENT,DateViewing date NOT NULL DEFAULT '0001-01-01',"
						+"SigDateTime datetime NOT NULL DEFAULT '0001-01-01 00:00:00',FKey bigint(20) NOT NULL,FKeyType varchar(255) NOT NULL,"
						+"IType tinyint(4) NOT NULL,RemoteRole tinyint(4) NOT NULL,MsgValue text NOT NULL,PRIMARY KEY (SignalNum)) "
						+"ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
					Exec(connection,"INSERT INTO preference (PrefName,ValueString) VALUES ('RandomPrimaryKeys','0')");
				}
				new DataConnection().SetDb("127.0.0.1",DB_NAME,"root","","root","",DatabaseType.MySql,true);
				Prefs.RefreshCache();
				Programs.RefreshCache();
				ProgramProperties.RefreshCache();
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
			//Each test starts from the database this class created (another class may have pointed the connection elsewhere).
			new DataConnection().SetDb("127.0.0.1",DB_NAME,"root","","root","",DatabaseType.MySql,true);
			Programs.RefreshCache();
			ProgramProperties.RefreshCache();
		}

		private static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.ExecuteNonQuery();
			}
		}

		private static void SetEnabled(bool isEnabled) {
			Program program=Programs.GetCur(ProgramName.Microsoft365Email);
			Program programOld=program.Copy();
			program.Enabled=isEnabled;
			Programs.Update(program,programOld);
			Programs.RefreshCache();
		}

		[TestMethod]
		public void EnsureProgram_InsertsADisabledRowAndItsSettingsOnce() {
			Microsoft365Program.EnsureProgram();
			Assert.IsFalse(Microsoft365Program.EnsureProgram(),"The second call inserts nothing.");
			Program program=Programs.GetCur(ProgramName.Microsoft365Email);
			Assert.IsNotNull(program);
			Assert.IsFalse(program.Enabled,"Off by default (rule 6).");
			Assert.AreEqual(Microsoft365Program.ProgDesc,program.ProgDesc);
			List<ProgramProperty> listProgramProperties=ProgramProperties.GetForProgram(program.ProgramNum);
			CollectionAssert.AreEquivalent(Microsoft365Program.ListPropertyDefaults.Select(x => x.Key).ToList(),
				listProgramProperties.Select(x => x.PropertyDesc).ToList());
			Assert.IsTrue(listProgramProperties.All(x => x.ClinicNum==0 && !x.IsHighSecurity),"No secrets at office level.");
			Assert.AreEqual("http://localhost",listProgramProperties.Single(x => x.PropertyDesc==Microsoft365Program.PropertyRedirectUri).PropertyValue);
			Assert.AreEqual(1,Programs.GetWhere(x => x.ProgName==ProgramName.Microsoft365Email.ToString()).Count);
		}

		[TestMethod]
		public void Config_SavesAndReadsBack() {
			Microsoft365Program.EnsureProgram();
			Microsoft365Config config=new Microsoft365Config();
			config.ClientId=CLIENT_ID;
			config.TenantId=TENANT_ID;
			config.RedirectUri="http://localhost:53100";
			config.SignInMethod=Microsoft365SignInMethod.DeviceCode;
			config.AllowPersonalAccounts=false;
			Microsoft365Program.SaveConfig(config);
			ProgramProperties.RefreshCache();
			Microsoft365Config configRead=Microsoft365Program.GetConfig();
			Assert.AreEqual(CLIENT_ID,configRead.ClientId);
			Assert.AreEqual(TENANT_ID,configRead.TenantId);
			Assert.AreEqual("http://localhost:53100",configRead.RedirectUri);
			Assert.AreEqual(Microsoft365SignInMethod.DeviceCode,configRead.SignInMethod);
			config.SignInMethod=Microsoft365SignInMethod.SystemBrowser;
			config.RedirectUri="";
			Microsoft365Program.SaveConfig(config);
			ProgramProperties.RefreshCache();
			Assert.AreEqual("http://localhost",Microsoft365Program.GetConfig().RedirectUri);
		}

		[TestMethod]
		public void SignIn_TurnedOffOrNotSetUpMakesNoNetworkCall() {
			Microsoft365Program.EnsureProgram();
			Microsoft365Config config=new Microsoft365Config();
			Microsoft365Program.SaveConfig(config);
			ProgramProperties.RefreshCache();
			SetEnabled(false);
			MicrosoftTokenHelper microsoftTokenHelper=MicrosoftApiConnector.GetAccessToken("frontdesk@example-dental.test","").GetAwaiter().GetResult();
			Assert.AreEqual("",microsoftTokenHelper.AccessToken);
			Assert.AreEqual(Microsoft365Errors.MessageTurnedOff,microsoftTokenHelper.ErrorMessage);
			Assert.AreEqual(Microsoft365ErrorKind.NotConfigured,microsoftTokenHelper.ErrorKind);
			Microsoft365Exception exOff=AssertEx.Throws<Microsoft365Exception>(() => MicrosoftApiConnector.SendEmail("frontdesk@example-dental.test","token",
				new OpenDentBusiness.Email.BasicEmailMessage()));
			Assert.AreEqual(Microsoft365ErrorKind.NotConfigured,exOff.Kind,"A disabled integration makes no call, even with a token in hand.");
			AssertEx.Throws<Microsoft365Exception>(() => MicrosoftApiConnector.GetProfile("frontdesk@example-dental.test","token"));
			SetEnabled(true);
			try {
				microsoftTokenHelper=MicrosoftApiConnector.GetAccessToken("frontdesk@example-dental.test","").GetAwaiter().GetResult();
				Assert.AreEqual(Microsoft365Errors.MessageNotConfiguredSetup,microsoftTokenHelper.ErrorMessage);
				Microsoft365Exception ex=AssertEx.Throws<Microsoft365Exception>(() => Microsoft365Program.GetConfigOrThrow());
				Assert.AreEqual(Microsoft365ErrorKind.NotConfigured,ex.Kind);
				EmailAddress emailAddress=new EmailAddress();
				emailAddress.EmailAddressNum=12345;
				emailAddress.EmailUsername="frontdesk@example-dental.test";
				emailAddress.AuthenticationType=OAuthType.Microsoft;
				AssertEx.Throws<Microsoft365Exception>(() => Microsoft365Session.Get(emailAddress));
			}
			finally {
				SetEnabled(false);
			}
		}

		[TestMethod]
		public void EmailAddressStore_WritesOnlyTheTokenColumnsOfAMicrosoftRow() {
			EmailAddress emailAddress=new EmailAddress();
			emailAddress.EmailUsername="frontdesk@example-dental.test";
			emailAddress.SMTPserver="smtp.outlook.com";
			emailAddress.SenderAddress="Front Desk <frontdesk@example-dental.test>";
			emailAddress.AuthenticationType=OAuthType.Microsoft;
			emailAddress.AccessToken="";
			emailAddress.RefreshToken="";
			emailAddress.EmailPassword="";
			emailAddress.Pop3ServerIncoming="pop.outlook.com";
			emailAddress.QueryString="";
			EmailAddresses.Insert(emailAddress);
			EmailAddressMicrosoft365CacheStore store=new EmailAddressMicrosoft365CacheStore(emailAddress.EmailAddressNum);
			string stored=SecretsTestHelper.Protect("{\"synthetic\":\"cache\"}");
			store.Write(stored,"oid.tid");
			Microsoft365StoredSignIn storedSignIn=store.Read();
			Assert.AreEqual(stored,storedSignIn.StoredCache);
			Assert.AreEqual("oid.tid",storedSignIn.AccountMarker);
			EmailAddress emailAddressDb=EmailAddresses.GetOneFromDb(emailAddress.EmailAddressNum);
			Assert.AreEqual("Front Desk <frontdesk@example-dental.test>",emailAddressDb.SenderAddress,"Other columns are untouched.");
			store.Clear();
			Assert.AreEqual("",store.Read().StoredCache);
			Assert.AreEqual("",store.Read().AccountMarker);
			//An address switched to another sign-in method meanwhile never gets a token back.
			emailAddressDb=EmailAddresses.GetOneFromDb(emailAddress.EmailAddressNum);
			emailAddressDb.AuthenticationType=OAuthType.None;
			EmailAddresses.Update(emailAddressDb);
			store.Write(stored,"oid.tid");
			Assert.AreEqual("",store.Read().StoredCache);
		}
	}
}
