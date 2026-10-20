// Integration tests for the EMAIL-UI package's token and download paths against a scratch MariaDB database on this computer: a Gmail or Microsoft
// 365 address that is turned off, not authorized, or holds a token issued under Open Dental's app never reaches a vendor, and refreshing never
// writes a token to the database. Skipped (inconclusive) when no server answers at 127.0.0.1:3306 as root with a blank password, or when
// VENDORDIRECT_TEST_DB=skip. No network call is made. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the
// repository root.
using System;
using CodeBase;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenDentBusiness;
using OpenDentBusiness.Microsoft365;

namespace VendorDirectTests.Email {
	[TestClass]
	public class EmailOAuthDbTests {
		private static readonly string DB_NAME=ScratchDb.Name("vendordirect_email_ui_tests");
		private const string CLIENT_ID_GOOGLE="123456789012-exampleclient.apps.googleusercontent.com";
		private const string CLIENT_ID_MICROSOFT="0b6c1f7e-3f0e-4c55-9a51-1d2f5e6a7b8c";
		private const string TENANT_ID="9d8c7b6a-5e4f-4a3b-8c2d-1e0f9a8b7c6d";
		private static string _skipReason;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			if(Environment.GetEnvironmentVariable("VENDORDIRECT_TEST_DB")=="skip") {
				_skipReason="VENDORDIRECT_TEST_DB=skip";
				return;
			}
			try {
				using(MySqlConnection connection=new MySqlConnection("Server=127.0.0.1;Port=3306;User ID=root;Password=;Connection Timeout=20")) {
					connection.Open();
					Exec(connection,"DROP DATABASE IF EXISTS "+DB_NAME);
					Exec(connection,"CREATE DATABASE "+DB_NAME+" CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
				}
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
				using(MySqlConnection connection=new MySqlConnection("Server=127.0.0.1;Port=3306;User ID=root;Password=;Connection Timeout=20")) {
					connection.Open();
					Exec(connection,"DROP DATABASE IF EXISTS "+DB_NAME);
				}
			}
			catch(Exception) {
				//Leaving the scratch database behind is harmless; the next run drops it.
			}
		}

		///<summary>Fresh tables for each test, and this class's database as the current one (other test classes switch it too).</summary>
		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			using(MySqlConnection connection=new MySqlConnection("Server=127.0.0.1;Port=3306;User ID=root;Password=;Connection Timeout=20;Database="+DB_NAME)) {
				connection.Open();
				Exec(connection,"DROP TABLE IF EXISTS program,programproperty,preference,signalod,emailaddress");
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
				Exec(connection,"CREATE TABLE signalod (SignalNum bigint(20) NOT NULL AUTO_INCREMENT,DateViewing date NOT NULL DEFAULT '0001-01-01',"
					+"SigDateTime datetime NOT NULL DEFAULT '0001-01-01 00:00:00',FKey bigint(20) NOT NULL,FKeyType varchar(255) NOT NULL,"
					+"IType tinyint(4) NOT NULL,RemoteRole tinyint(4) NOT NULL,MsgValue text NOT NULL,PRIMARY KEY (SignalNum)) "
					+"ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
				Exec(connection,"CREATE TABLE emailaddress (EmailAddressNum bigint(20) NOT NULL AUTO_INCREMENT,SMTPserver varchar(255) NOT NULL,"
					+"EmailUsername varchar(255) NOT NULL,EmailPassword varchar(255) NOT NULL,ServerPort int(11) NOT NULL,UseSSL tinyint(4) NOT NULL,"
					+"SenderAddress varchar(255) NOT NULL,Pop3ServerIncoming varchar(255) NOT NULL,ServerPortIncoming int(11) NOT NULL,"
					+"UserNum bigint(20) NOT NULL,AccessToken varchar(2000) NOT NULL,RefreshToken text NOT NULL,DownloadInbox tinyint(4) NOT NULL,"
					+"QueryString varchar(1000) NOT NULL,AuthenticationType tinyint(4) NOT NULL,PRIMARY KEY (EmailAddressNum),KEY UserNum (UserNum)) "
					+"ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
				Exec(connection,"INSERT INTO preference (PrefName,ValueString) VALUES ('RandomPrimaryKeys','0')");
			}
			new DataConnection().SetDb("127.0.0.1",DB_NAME,"root","","root","",DatabaseType.MySql,true);
			Prefs.RefreshCache();
			Programs.RefreshCache();
			ProgramProperties.RefreshCache();
			GoogleTokenCache.ClearAll();
			Microsoft365Session.ForgetAll();
		}

		private static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.ExecuteNonQuery();
			}
		}

		private static void SetEnabled(ProgramName programName,bool isEnabled) {
			Program program=Programs.GetCur(programName);
			Program programOld=program.Copy();
			program.Enabled=isEnabled;
			Programs.Update(program,programOld);
			Programs.RefreshCache();
		}

		private static void SetUpGmail(bool isEnabled) {
			GoogleOAuthSettings.EnsureProgram();
			Programs.RefreshCache();
			ProgramProperties.RefreshCache();
			GoogleOAuthSettings.SetClientId(CLIENT_ID_GOOGLE);
			Assert.IsTrue(GoogleOAuthSettings.SetClientSecret("synthetic-client-secret"));
			ProgramProperties.RefreshCache();
			SetEnabled(ProgramName.GmailApi,isEnabled);
		}

		private static void SetUpMicrosoft(bool isEnabled) {
			Microsoft365Program.EnsureProgram();
			Microsoft365Config config=new Microsoft365Config();
			config.ClientId=CLIENT_ID_MICROSOFT;
			config.TenantId=TENANT_ID;
			Microsoft365Program.SaveConfig(config);
			ProgramProperties.RefreshCache();
			SetEnabled(ProgramName.Microsoft365Email,isEnabled);
		}

		private static EmailAddress InsertAddress(OAuthType oAuthType,string accessToken,string refreshToken,bool downloadInbox=false) {
			EmailAddress emailAddress=new EmailAddress();
			emailAddress.EmailUsername="frontdesk@example-dental.test";
			emailAddress.SMTPserver=(oAuthType==OAuthType.Google) ? "smtp.gmail.com" : "smtp.outlook.com";
			emailAddress.SenderAddress="";
			emailAddress.EmailPassword="";
			emailAddress.Pop3ServerIncoming="";
			emailAddress.QueryString="";
			emailAddress.AuthenticationType=oAuthType;
			emailAddress.AccessToken=accessToken;
			emailAddress.RefreshToken=refreshToken;
			emailAddress.DownloadInbox=downloadInbox;
			EmailAddresses.Insert(emailAddress);
			return emailAddress;
		}

		[TestMethod]
		public void Gmail_TurnedOffMakesNoCall() {
			SetUpGmail(isEnabled:false);
			EmailAddress emailAddress=InsertAddress(OAuthType.Google,"",SecretsTestHelper.Protect("synthetic refresh token"));
			GoogleAuthException ex=AssertEx.Throws<GoogleAuthException>(() => EmailMessages.GetOAuthAccessToken(emailAddress));
			Assert.AreEqual(GoogleAuthReason.NotConfigured,ex.Reason);
			Assert.AreEqual(GoogleOAuthSettings.MessageNotEnabled,ex.Message);
		}

		[TestMethod]
		public void Gmail_BlankOrLegacyTokenNeedsAuthorizationWithoutACall() {
			SetUpGmail(isEnabled:true);
			//A legacy row: a plain-text refresh token issued to Open Dental's OAuth client, which no one else can refresh.
			EmailAddress emailAddressLegacy=InsertAddress(OAuthType.Google,"ya29.legacy-access-token","1//legacy-refresh-token",downloadInbox:true);
			GoogleAuthException ex=AssertEx.Throws<GoogleAuthException>(() => EmailMessages.GetOAuthAccessToken(emailAddressLegacy));
			Assert.AreEqual(GoogleAuthReason.NotAuthorized,ex.Reason);
			Assert.AreEqual("frontdesk@example-dental.test needs to be authorized. Open Setup > Email, edit the address, and click Authorize.",ex.Message);
			Assert.IsTrue(GoogleTokenCache.NeedsReauthorization(emailAddressLegacy),"FormEmailMessageEdit stops before sending.");
			ODException exReceive=AssertEx.Throws<ODException>(() => EmailMessages.ReceiveFromInbox(emailAddressLegacy));
			StringAssert.Contains(exReceive.Message,"needs to be authorized");
			//Refreshing writes nothing: the row keeps its values (Save after a new authorization overwrites them).
			AssertEx.Throws<GoogleAuthException>(() => EmailMessages.RefreshGmailToken(emailAddressLegacy));
			EmailAddress emailAddressDb=EmailAddresses.GetOneFromDb(emailAddressLegacy.EmailAddressNum);
			Assert.AreEqual("ya29.legacy-access-token",emailAddressDb.AccessToken);
			Assert.AreEqual("1//legacy-refresh-token",emailAddressDb.RefreshToken);
			EmailAddress emailAddressBlank=InsertAddress(OAuthType.Google,"","");
			Assert.AreEqual(GoogleAuthReason.NotAuthorized,
				AssertEx.Throws<GoogleAuthException>(() => EmailMessages.GetOAuthAccessToken(emailAddressBlank)).Reason);
		}

		[TestMethod]
		public void Microsoft_TurnedOffOrNotSignedInMakesNoCall() {
			SetUpMicrosoft(isEnabled:false);
			EmailAddress emailAddress=InsertAddress(OAuthType.Microsoft,"oid.tid",SecretsTestHelper.Protect("{\"synthetic\":\"cache\"}"),downloadInbox:true);
			ODException exOff=AssertEx.Throws<ODException>(() => EmailMessages.GetOAuthAccessToken(emailAddress));
			Assert.AreEqual(Microsoft365Errors.MessageTurnedOff,exOff.Message);
			Microsoft365Exception exInner=EmailMessages.FindException<Microsoft365Exception>(exOff);
			Assert.IsNotNull(exInner);
			Assert.AreEqual(Microsoft365ErrorKind.NotConfigured,exInner.Kind);
			ODException exReceiveOff=AssertEx.Throws<ODException>(() => EmailMessages.ReceiveFromInbox(emailAddress));
			Assert.AreEqual(Microsoft365Errors.MessageTurnedOff,exReceiveOff.Message);
			SetEnabled(ProgramName.Microsoft365Email,true);
			EmailAddress emailAddressBlank=InsertAddress(OAuthType.Microsoft,"","",downloadInbox:true);
			ODException exBlank=AssertEx.Throws<ODException>(() => EmailMessages.GetOAuthAccessToken(emailAddressBlank));
			Assert.AreEqual("frontdesk@example-dental.test needs to be re-authenticated. Please sign out and back in to continue.",exBlank.Message);
			Microsoft365Exception exBlankInner=EmailMessages.FindException<Microsoft365Exception>(exBlank);
			Assert.IsTrue(exBlankInner.IsAuthError,"The email window offers a new sign-in.");
			ODException exReceiveBlank=AssertEx.Throws<ODException>(() => EmailMessages.ReceiveFromInbox(emailAddressBlank));
			StringAssert.Contains(exReceiveBlank.Message,"needs to be re-authenticated");
			Assert.IsTrue(EmailMessages.FindException<Microsoft365Exception>(exReceiveBlank).IsAuthError);
		}
	}
}
