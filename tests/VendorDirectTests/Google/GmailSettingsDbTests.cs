// Tests for the Gmail API program link's rows (GMAIL-LIB) against a scratch MariaDB database on this computer, plus checks that the Gmail code
// no longer reaches Open Dental. The database tests are skipped (inconclusive) when no server answers at 127.0.0.1:3306 as root with a blank
// password, or when VENDORDIRECT_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Linq;
using CodeBase;
using CodeBase.VendorDirect;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenDentBusiness;
using VendorDirectTests.Foundation;

namespace VendorDirectTests.Gmail {
	[TestClass]
	public class GmailSettingsDbTests {
		private const string DB_NAME="vendordirect_gmail_tests";
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
				Exec(connection,"DROP TABLE IF EXISTS program,programproperty,preference,signalod");
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
				Exec(connection,"INSERT INTO preference (PrefName,ValueString) VALUES ('RandomPrimaryKeys','0')");
			}
			new DataConnection().SetDb("127.0.0.1",DB_NAME,"root","","root","",DatabaseType.MySql,true);
			Prefs.RefreshCache();
			Programs.RefreshCache();
			ProgramProperties.RefreshCache();
		}

		private static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.ExecuteNonQuery();
			}
		}

		private static void RefreshCaches() {
			Programs.RefreshCache();
			ProgramProperties.RefreshCache();
		}

		[TestMethod]
		public void EnsureProgram_InsertsDisabledRowsOnce() {
			Assert.IsTrue(GoogleOAuthSettings.EnsureProgram());
			RefreshCaches();
			Assert.IsFalse(GoogleOAuthSettings.EnsureProgram(),"Idempotent.");
			RefreshCaches();
			Program program=Programs.GetCur(ProgramName.GmailApi);
			Assert.IsNotNull(program);
			Assert.IsFalse(program.Enabled,"Off by default (rule 6).");
			Assert.AreEqual("Gmail API",program.ProgDesc);
			Assert.AreEqual("",program.Path);
			Assert.AreEqual(1,Programs.GetWhere(x => x.ProgName==ProgramName.GmailApi.ToString()).Count);
			ProgramProperty programPropertyId=ProgramProperties.GetOneFromDb(program.ProgramNum,GoogleOAuthSettings.PropertyClientId,0);
			Assert.IsFalse(programPropertyId.IsMasked,"The client ID is a public identifier.");
			ProgramProperty programPropertySecret=ProgramProperties.GetOneFromDb(program.ProgramNum,GoogleOAuthSettings.PropertyClientSecret,0);
			Assert.IsTrue(programPropertySecret.IsMasked);
			Assert.IsTrue(programPropertySecret.IsHighSecurity);
			Assert.AreEqual(2,ProgramProperties.GetWhere(x => x.ProgramNum==program.ProgramNum).Count);
			Assert.IsFalse(GoogleOAuthSettings.IsConfigured);
			Assert.IsFalse(GoogleOAuthSettings.IsEnabled);
		}

		[TestMethod]
		public void EnsureProgram_RemovesARacingDuplicate() {
			//Another workstation inserted a second GmailApi row at the same moment.
			using(MySqlConnection connection=new MySqlConnection("Server=127.0.0.1;Port=3306;User ID=root;Password=;Connection Timeout=20;Database="+DB_NAME)) {
				connection.Open();
				Exec(connection,"INSERT INTO program (ProgName,ProgDesc,Enabled,Path,CommandLine,Note,PluginDllName,ButtonImage,FileTemplate,FilePath,"
					+"IsDisabledByHq,CustErr) VALUES ('GmailApi','Gmail API',0,'','','','','','','',0,'')");
			}
			Assert.IsFalse(Programs.InsertIfMissing(ProgramName.GmailApi,"Gmail API"));
			RefreshCaches();
			GoogleOAuthSettings.EnsureProgram();
			RefreshCaches();
			Assert.AreEqual(1,Programs.GetWhere(x => x.ProgName==ProgramName.GmailApi.ToString()).Count);
			Assert.AreEqual(2,ProgramProperties.GetWhere(x => x.ProgramNum==Programs.GetProgramNum(ProgramName.GmailApi)).Count);
		}

		[TestMethod]
		public void EnsureProgram_RaisesFlagsOnAPlainSecretRow() {
			Programs.InsertIfMissing(ProgramName.GmailApi,"Gmail API");
			RefreshCaches();
			long programNum=Programs.GetProgramNum(ProgramName.GmailApi);
			ProgramProperties.InsertIfMissing(programNum,GoogleOAuthSettings.PropertyClientSecret,0,"typed-secret",false,false);
			RefreshCaches();
			Assert.IsTrue(GoogleOAuthSettings.EnsureProgram());
			ProgramProperty programPropertySecret=ProgramProperties.GetOneFromDb(programNum,GoogleOAuthSettings.PropertyClientSecret,0);
			Assert.IsTrue(programPropertySecret.IsMasked && programPropertySecret.IsHighSecurity);
			RefreshCaches();
			Assert.AreEqual("typed-secret",GoogleOAuthSettings.GetClientSecret(),"A secret typed as plain text is still used, and encrypted on the next save.");
		}

		[TestMethod]
		public void ClientSettings_SecretIsStoredEncrypted() {
			GoogleOAuthSettings.EnsureProgram();
			RefreshCaches();
			GoogleOAuthSettings.SetClientId("  "+GmailTestData.ClientId+" ");
			Assert.IsTrue(GoogleOAuthSettings.SetClientSecret(GmailTestData.ClientSecret));
			RefreshCaches();
			long programNum=GoogleOAuthSettings.GetProgramNum();
			string stored=ProgramProperties.GetOneFromDb(programNum,GoogleOAuthSettings.PropertyClientSecret,0).PropertyValue;
			SecretsTestHelper.AssertRoundTrip(GmailTestData.ClientSecret,stored);
			Assert.AreEqual(GmailTestData.ClientId,GoogleOAuthSettings.GetClientId());
			Assert.AreEqual(GmailTestData.ClientSecret,GoogleOAuthSettings.GetClientSecret());
			Assert.IsTrue(GoogleOAuthSettings.IsConfigured);
			GoogleClientCredentials googleClientCredentials=GoogleOAuthSettings.GetClientCredentials();
			Assert.AreEqual(GmailTestData.ClientId,googleClientCredentials.ClientId);
			Assert.AreEqual(GmailTestData.ClientSecret,googleClientCredentials.ClientSecret);
			string fingerprint=googleClientCredentials.Fingerprint;
			Assert.IsTrue(GoogleOAuthSettings.SetClientSecret("rotated"));
			RefreshCaches();
			Assert.AreNotEqual(fingerprint,GoogleOAuthSettings.GetClientFingerprint(),"A rotated secret gives fresh cached tokens.");
		}

		[TestMethod]
		public void ClientSettings_UnreadableSecretCountsAsMissing() {
			GoogleOAuthSettings.EnsureProgram();
			RefreshCaches();
			long programNum=GoogleOAuthSettings.GetProgramNum();
			using(MySqlConnection connection=new MySqlConnection("Server=127.0.0.1;Port=3306;User ID=root;Password=;Connection Timeout=20;Database="+DB_NAME)) {
				connection.Open();
				Exec(connection,"UPDATE programproperty SET PropertyValue='odv2e$open-dental-ciphertext' WHERE ProgramNum="+programNum
					+" AND PropertyDesc='Client Secret'");
			}
			RefreshCaches();
			Assert.AreEqual("",GoogleOAuthSettings.GetClientSecret());
			Assert.IsTrue(GoogleOAuthSettings.ReadClientSecret().NeedsReentry);
		}

		[TestMethod]
		public void Credentials_NoGoogleCallWhileTurnedOff() {
			GoogleOAuthSettings.EnsureProgram();
			RefreshCaches();
			GoogleOAuthSettings.SetClientId(GmailTestData.ClientId);
			RefreshCaches();
			GoogleAuthException ex=AssertEx.Throws<GoogleAuthException>(() => GoogleOAuthSettings.GetClientCredentialsOrThrow("no client"));
			Assert.AreEqual(GoogleAuthReason.NotConfigured,ex.Reason);
			Assert.AreEqual(GoogleOAuthSettings.MessageNotEnabled,ex.Message,"Off by default: nothing reaches Google (rule 6).");
			EmailAddress emailAddress=new EmailAddress { EmailAddressNum=1,EmailUsername="front@example.com",AuthenticationType=OAuthType.Google,
				RefreshToken=SecretsTestHelper.Protect("rt") };
			Assert.AreEqual(GoogleAuthReason.NotConfigured,AssertEx.Throws<GoogleAuthException>(() => GoogleTokenCache.GetAccessToken(emailAddress)).Reason);
			Assert.AreEqual(GoogleAuthReason.NotConfigured,
				AssertEx.Throws<GoogleAuthException>(() => EmailMessages.RefreshGmailToken(emailAddress)).Reason);
			Program program=Programs.GetCur(ProgramName.GmailApi);
			Program programOld=program.Copy();
			program.Enabled=true;
			Programs.Update(program,programOld);
			GoogleOAuthSettings.SetClientId("");
			RefreshCaches();
			ex=AssertEx.Throws<GoogleAuthException>(() => GoogleOAuthSettings.GetClientCredentialsOrThrow("no client"));
			Assert.AreEqual("no client",ex.Message);
			GoogleOAuthSettings.SetClientId(GmailTestData.ClientId);
			RefreshCaches();
			Assert.AreEqual(GmailTestData.ClientId,GoogleOAuthSettings.GetClientCredentialsOrThrow("no client").ClientId);
		}
	}

	[TestClass]
	public class GmailNoHqTests {
		private static string Read(string relativePath) {
			return File.ReadAllText(Path.Combine(FoundationLayoutTests.GetRepositoryRoot(),relativePath));
		}

		[TestMethod]
		public void GmailCode_NoLongerCallsOpenDental() {
			foreach(string file in new[] { "Google.cs","GoogleApiConnector.cs","GoogleTokenCache.cs","GoogleOAuthSettings.cs" }) {
				string text=Read(Path.Combine("OpenDentBusiness","WebBridges","Google",file));
				foreach(string forbidden in new[] { "WebServiceMainHQ","BuildOAuthUrl","GetGoogleAccessToken","PayloadHelper","ODCloudClient",
					"RegistrationKey","GoogleAuthCodeResponseHtml","www.googleapis.com/oauth2/v4" }.Concat(HqHosts.BlockedDomains))
				{
					Assert.IsFalse(text.Contains(forbidden),file+" still mentions "+forbidden);
				}
			}
		}

		[TestMethod]
		public void ResponsePageResource_IsGone() {
			string resx=Read(Path.Combine("OpenDentBusiness","Properties","Resources.resx"));
			Assert.IsFalse(resx.Contains("GoogleAuthCodeResponseHtml"));
			Assert.IsFalse(HqHosts.BlockedDomains.Any(x => resx.Contains(x+"/")),"The resources no longer load anything from Open Dental.");
			Assert.IsNull(typeof(OpenDentBusiness.Properties.Resources).GetProperty("GoogleAuthCodeResponseHtml"));
		}

		[TestMethod]
		public void SetupWindow_IsVendorNeutral() {
			string text=Read(Path.Combine("OpenDental","Forms","FormGmailApiSetup.cs"))+Read(Path.Combine("OpenDental","Forms","FormGmailApiSetup.Designer.cs"));
			foreach(string forbidden in new[] { "http://","https://","Sign in with Google","google_signin","Image","recommended","Sign up" }) {
				Assert.IsFalse(text.Contains(forbidden),"The setup window must not contain "+forbidden);
			}
			StringAssert.Contains(text,"Send and receive email through the Gmail API using your own Google Cloud OAuth client.");
		}
	}
}
