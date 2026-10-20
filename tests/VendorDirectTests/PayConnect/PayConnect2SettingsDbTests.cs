// Tests for how PayConnect 2 reads its settings from programproperty rows, against a scratch MariaDB database on this computer. They are skipped
// (inconclusive) when no server answers at 127.0.0.1:3306 as root with a blank password, or when VENDORDIRECT_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenDentBusiness;

namespace VendorDirectTests.PayConnectV2 {
	[TestClass]
	public class PayConnect2SettingsDbTests {
		private static readonly string DB_NAME=ScratchDb.Name("vendordirect_payconnect_tests");
		private const string API_KEY="test-integrator-key-0002";
		private static string _skipReason;
		private static long _programNum;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			if(Environment.GetEnvironmentVariable("VENDORDIRECT_TEST_DB")=="skip") {
				_skipReason="VENDORDIRECT_TEST_DB=skip";
				return;
			}
			try {
				using(MySqlConnection connection=new MySqlConnection("Server=127.0.0.1;Port=3306;User ID=root;Password=;Connection Timeout=15")) {
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
					//The PayConnect row as ConvertDatabases left it: disabled.
					Exec(connection,"INSERT INTO program (ProgName,ProgDesc,Enabled,Path,CommandLine,Note,PluginDllName,ButtonImage,FileTemplate,FilePath,"
						+"IsDisabledByHq,CustErr) VALUES ('PayConnect','PayConnect',0,'','','','','','','',0,'')");
				}
				new DataConnection().SetDb("127.0.0.1",DB_NAME,"root","","root","",DatabaseType.MySql,true);
				Prefs.RefreshCache();
				Programs.RefreshCache();
				_programNum=Programs.GetProgramNum(ProgramName.PayConnect);
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
				using(MySqlConnection connection=new MySqlConnection("Server=127.0.0.1;Port=3306;User ID=root;Password=;Connection Timeout=15")) {
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
			PayConnect2.Mock=null;
			PayConnect2.HttpHandlerForTests=null;
			//Each test starts from the bare, disabled program row.
			new DataConnection().SetDb("127.0.0.1",DB_NAME,"root","","root","",DatabaseType.MySql,true);
			DataCore.NonQ("DELETE FROM programproperty");
			DataCore.NonQ("UPDATE program SET Enabled=0");
			Refresh();
		}

		private static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.ExecuteNonQuery();
			}
		}

		private static void Refresh() {
			Programs.RefreshCache();
			ProgramProperties.RefreshCache();
		}

		private static void SetRow(string desc,long clinicNum,string value) {
			ProgramProperty programProperty=ProgramProperties.InsertIfMissing(_programNum,desc,clinicNum,"",false,false);
			ProgramProperty programPropertyOld=programProperty.Copy();
			programProperty.PropertyValue=value;
			ProgramProperties.Update(programProperty,programPropertyOld);
		}

		private static void SetUpConfiguredClinic(long clinicNum) {
			DataCore.NonQ("UPDATE program SET Enabled=1");
			SetRow(OpenDentBusiness.PayConnect.ProgramProperties.ApiKey,0,SecretsTestHelper.Protect(API_KEY));
			SetRow(OpenDentBusiness.PayConnect.ProgramProperties.ApiSecret,clinicNum,SecretsTestHelper.Protect("secret-"+clinicNum));
			SetRow(OpenDentBusiness.PayConnect.ProgramProperties.ProgramVersion,clinicNum,"2");
			Refresh();
		}

		[TestMethod]
		public void GetApiKey_MissingThrowsNotConfigured() {
			AssertEx.Throws<PayConnect2.PayConnectNotConfiguredException>(() => PayConnect2.GetApiKey());
			Assert.IsFalse(PayConnect2.HasCredentials(0));
		}

		[TestMethod]
		public void GetApiKey_DecryptsThePracticeRow() {
			SetRow(OpenDentBusiness.PayConnect.ProgramProperties.ApiKey,0,SecretsTestHelper.Protect(API_KEY));
			Refresh();
			Assert.AreEqual(API_KEY,PayConnect2.GetApiKey());
		}

		[TestMethod]
		public void GetApiSecretForClinic_AcceptsLegacyPlainTextAndFallsBackToThePractice() {
			SetRow(OpenDentBusiness.PayConnect.ProgramProperties.ApiSecret,0,"legacy-plain-secret");//As Open Dental stored it.
			SetRow(OpenDentBusiness.PayConnect.ProgramProperties.ApiSecret,3,SecretsTestHelper.Protect("clinic-3-secret"));
			SetRow(OpenDentBusiness.PayConnect.ProgramProperties.ApiSecret,4,"osd1$corrupted");
			Refresh();
			Assert.AreEqual("legacy-plain-secret",PayConnect2.GetApiSecretForClinic(0));
			Assert.AreEqual("clinic-3-secret",PayConnect2.GetApiSecretForClinic(3));
			Assert.AreEqual("legacy-plain-secret",PayConnect2.GetApiSecretForClinic(9),"A clinic without its own row uses the practice's.");
			Assert.AreEqual("",PayConnect2.GetApiSecretForClinic(4),"An unreadable osd1$ value is never sent as the Secret.");
		}

		[TestMethod]
		public void GetApiBaseUrl_FollowsTheSandboxSetting() {
			Assert.AreEqual(PayConnect2.UrlBaseProduction,PayConnect2.GetApiBaseUrl());
			SetRow(OpenDentBusiness.PayConnect.ProgramProperties.UseSandbox,0,"1");
			Refresh();
			Assert.IsTrue(PayConnect2.GetUseSandbox());
			Assert.AreEqual(PayConnect2.UrlBaseSandbox,PayConnect2.GetApiBaseUrl());
		}

		[TestMethod]
		public void IsConfigured_NeedsEnabledVersion2KeyAndSecret() {
			Assert.IsFalse(PayConnect2.IsConfigured(2),"Nothing set.");
			SetUpConfiguredClinic(2);
			Assert.IsTrue(PayConnect2.IsConfigured(2));
			SetRow(OpenDentBusiness.PayConnect.ProgramProperties.ProgramVersion,2,"1");
			Refresh();
			Assert.IsFalse(PayConnect2.IsConfigured(2),"Version 1 is not PayConnect 2.");
			SetRow(OpenDentBusiness.PayConnect.ProgramProperties.ProgramVersion,2,"2");
			DataCore.NonQ("UPDATE program SET Enabled=0");
			Refresh();
			Assert.IsFalse(PayConnect2.IsConfigured(2),"Disabled program.");
			DataCore.NonQ("UPDATE program SET Enabled=1");
			SetRow(OpenDentBusiness.PayConnect.ProgramProperties.ApiSecret,2,"");
			Refresh();
			Assert.IsFalse(PayConnect2.IsConfigured(2),"No Secret.");
		}

		[TestMethod]
		public void PostCreateTransaction_MissingCredentialsGiveAnErrorNotAnException() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler();
			PayConnect2.HttpHandlerForTests=fakeHttpHandler;
			try {
				PayConnect2.PayConnect2Response response=PayConnect2.PostCreateTransaction(new PayConnect2.CreateTransactionRequest(),5);
				Assert.AreEqual(PayConnect2.ResponseType.Error,response.ResponseType);
				Assert.AreEqual(PayConnect2.PayConnectRequestOutcome.NotSent,response.Outcome);
				Assert.AreEqual(PayConnect2.PayConnectErrorKind.NotConfigured,response.ErrorResponse.Kind);
				Assert.AreEqual(0,fakeHttpHandler.Requests.Count);
			}
			finally {
				PayConnect2.HttpHandlerForTests=null;
			}
		}

		[TestMethod]
		public void PostCreateTransaction_UsesTheClinicsStoredKeys() {
			SetUpConfiguredClinic(6);
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler();
			fakeHttpHandler.Enqueue(System.Net.HttpStatusCode.OK,"{\"referenceId\":\"R6\",\"status\":\"Processed\",\"type\":\"Sale\",\"amount\":100,"
				+"\"paymentMethod\":{\"cardPaymentMethod\":{\"cardLast4Digits\":\"1111\",\"network\":\"Visa\"}}}");
			PayConnect2.HttpHandlerForTests=fakeHttpHandler;
			try {
				PayConnect2.PayConnect2Response response=PayConnect2.PostCreateTransaction(new PayConnect2.CreateTransactionRequest(),6);
				Assert.AreEqual(PayConnect2.ResponseType.CreateTransaction,response.ResponseType);
				Assert.AreEqual("R6",response.TransactionResponse.ReferenceId);
				RecordedRequest recordedRequest=fakeHttpHandler.Requests[0];
				Assert.AreEqual("https://api.dentalxchange.com/payments/transactions",recordedRequest.RequestUri.AbsoluteUri);
				Assert.AreEqual(API_KEY,recordedRequest.GetHeader("API-Key"));
				Assert.AreEqual("secret-6",recordedRequest.GetHeader("Secret"));
				StringAssert.Contains(recordedRequest.Body,"\"invoiceNumber\":\"OPMS-");
				StringAssert.StartsWith(response.CorrelationId,"OPMS-");
			}
			finally {
				PayConnect2.HttpHandlerForTests=null;
			}
		}
	}
}
