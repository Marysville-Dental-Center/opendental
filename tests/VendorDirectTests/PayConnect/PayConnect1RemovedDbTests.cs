// Tests for a database whose PayConnect rows still say "Program Version" 1 after PayConnect 1.0 was removed, against a scratch MariaDB database on
// this computer with clinics off: the clinic counts as not set up (even with its old user name and password rows), the payment windows hide the
// button, and a recurring charge is marked failed with nothing sent. Skipped (inconclusive) when no server answers at 127.0.0.1:3306 as root with
// a blank password, or when VENDORDIRECT_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Text;
using CodeBase;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenDentBusiness;
using OpenDentBusiness.VendorDirect;

namespace VendorDirectTests.PayConnectV1Removal {
	[TestClass]
	public class PayConnect1RemovedDbTests {
		private const string DB_NAME="vendordirect_payconnect1_removed_tests";
		private const string CONNECTION="Server=127.0.0.1;Port=3306;User ID=root;Password=;Connection Timeout=3";
		private static string _skipReason;
		private static long _programNum;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			if(Environment.GetEnvironmentVariable("VENDORDIRECT_TEST_DB")=="skip") {
				_skipReason="VENDORDIRECT_TEST_DB=skip";
				return;
			}
			try {
				using(MySqlConnection connection=new MySqlConnection(CONNECTION)) {
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
					Exec(connection,"INSERT INTO preference (PrefName,ValueString) VALUES ('RandomPrimaryKeys','0'),('EasyNoClinics','1')");
				}
				UseScratchDb();
				Prefs.RefreshCache();
				//The row as ConvertDatabases left it: disabled.
				Programs.InsertIfMissing(ProgramName.PayConnect,"PayConnect");
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
				using(MySqlConnection connection=new MySqlConnection(CONNECTION)) {
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
			//Other test classes point the shared connection at their own scratch databases. Each test starts from an enabled program with the
			//rows an Open Dental office on PayConnect 1.0 had, plus PayConnect 2 keys.
			UseScratchDb();
			Prefs.RefreshCache();
			PayConnect2.Mock=null;
			PayConnect2.HttpHandlerForTests=null;
			DataCore.NonQ("DELETE FROM programproperty");
			DataCore.NonQ("UPDATE program SET Enabled=1 WHERE ProgramNum="+_programNum);
			Refresh();
			SetRow(PayConnect.ProgramProperties.ProgramVersion,"1");
			SetRow("Username","office-user");
			SetRow("Password",Secrets.TryEncrypt("office-password"));
			SetRow(PayConnect.ProgramProperties.ApiKey,SecretsTestHelper.Protect("integrator-key"));
			SetRow(PayConnect.ProgramProperties.ApiSecret,SecretsTestHelper.Protect("merchant-secret"));
		}

		[TestCleanup]
		public void TestCleanup() {
			PayConnect2.HttpHandlerForTests=null;
		}

		private static void UseScratchDb() {
			new DataConnection().SetDb("127.0.0.1",DB_NAME,"root","","root","",DatabaseType.MySql,true);
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

		private static void SetRow(string desc,string value) {
			ProgramProperty programProperty=ProgramProperties.InsertIfMissing(_programNum,desc,0,"",false,false);
			ProgramProperty programPropertyOld=programProperty.Copy();
			programProperty.PropertyValue=value;
			ProgramProperties.Update(programProperty,programPropertyOld);
			Refresh();
		}

		[TestMethod]
		public void Version1Clinic_IsNotSetUp_AndHidesTheButton() {
			Assert.IsFalse(PayConnect2.IsConfigured(0),"A version 1 clinic is not set up, even with keys entered.");
			Assert.IsFalse(PaymentWindowRules.ShowPayConnect(0,new List<Def>()),"The old user name and password no longer show the button.");
			Assert.IsFalse(PaymentWindowRules.ShowPayConnect(0,null));
			//Saving setup writes version 2, after which the same keys work.
			SetRow(PayConnect.ProgramProperties.ProgramVersion,PayConnect.Version2);
			Assert.IsTrue(PayConnect2.IsConfigured(0));
			Assert.IsTrue(PaymentWindowRules.ShowPayConnect(0,new List<Def>()));
		}

		[TestMethod]
		public void RecurringCharge_Version1Clinic_FailsAndSendsNothing() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler();
			PayConnect2.HttpHandlerForTests=fakeHttpHandler;
			TestLog testLog=new TestLog();
			RecurringChargerator recurringChargerator=new RecurringChargerator(testLog,true);
			RecurringChargeData recurringChargeData=new RecurringChargeData();
			recurringChargeData.RecurringCharge=new RecurringCharge();
			recurringChargeData.RecurringCharge.PatNum=42;
			recurringChargeData.RecurringCharge.ChargeStatus=RecurringChargeStatus.NotYetCharged;
			recurringChargeData.PatName="Test Patient";
			recurringChargeData.PayConnectToken="token-0001";
			StringBuilder stringBuilderResultFile=new StringBuilder();
			recurringChargerator.SendPayConnect(recurringChargeData,forceDuplicates:true,stringBuilderResultFile);
			Assert.AreEqual(RecurringChargeStatus.ChargeFailed,recurringChargeData.RecurringCharge.ChargeStatus);
			StringAssert.Contains(recurringChargeData.RecurringCharge.ErrorMsg,"PayConnect 1.0 is no longer supported");
			StringAssert.Contains(stringBuilderResultFile.ToString(),"PayConnect 1.0 is no longer supported");
			Assert.AreEqual(1,recurringChargerator.Failed);
			Assert.AreEqual(0,recurringChargerator.Success);
			Assert.AreEqual(0,fakeHttpHandler.Requests.Count,"Nothing is sent for a version 1 clinic.");
		}

		///<summary>Keeps what the recurring-charge run logs, so nothing is written to disk.</summary>
		private class TestLog:Logger.IWriteLine {
			public List<string> ListLines=new List<string>();
			public LogLevel LogLevel { get; set; }=LogLevel.Verbose;

			public void WriteLine(string data,LogLevel logLevel,string subDirectory="") {
				ListLines.Add(data);
			}
		}
	}
}
