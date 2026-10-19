// Tests for which card-processor buttons the payment windows show (PAY-UI), against a scratch MariaDB database on this computer with clinics off.
// A processor shows only when its program link is enabled and the office's own credentials are entered and readable; there is no "show every
// processor" fallback. Nothing leaves this computer. Skipped (inconclusive) when no server answers at 127.0.0.1:3306 as root with a blank
// password, or when VENDORDIRECT_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenDentBusiness;
using OpenDentBusiness.VendorDirect;

namespace VendorDirectTests.PayUi {
	[TestClass]
	public class PaymentWindowRulesDbTests {
		private const string DB_NAME="vendordirect_payui_tests";
		private const string CONNECTION="Server=127.0.0.1;Port=3306;User ID=root;Password=;Connection Timeout=3";
		private static string _skipReason;
		private static long _programNumPayConnect;
		private static long _programNumPaySimple;
		private static long _programNumEdgeExpress;
		///<summary>No payment types at all: with clinics off the payment-type check doesn't apply, so the buttons must not depend on it.</summary>
		private static readonly List<Def> _listDefsEmpty=new List<Def>();

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
				//The rows as ConvertDatabases left them: disabled.
				Programs.InsertIfMissing(ProgramName.PayConnect,"PayConnect");
				Programs.InsertIfMissing(ProgramName.PaySimple,"PaySimple");
				Programs.InsertIfMissing(ProgramName.Xcharge,"X-Charge");
				Programs.InsertIfMissing(ProgramName.EdgeExpress,"EdgeExpress");
				Programs.RefreshCache();
				_programNumPayConnect=Programs.GetProgramNum(ProgramName.PayConnect);
				_programNumPaySimple=Programs.GetProgramNum(ProgramName.PaySimple);
				_programNumEdgeExpress=Programs.GetProgramNum(ProgramName.EdgeExpress);
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
			//Other test classes point the shared connection at their own scratch databases. Each test starts from bare, disabled program rows.
			UseScratchDb();
			Prefs.RefreshCache();
			DataCore.NonQ("DELETE FROM programproperty");
			DataCore.NonQ("UPDATE program SET Enabled=0");
			Refresh();
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

		private static void Enable(long programNum) {
			DataCore.NonQ("UPDATE program SET Enabled=1 WHERE ProgramNum="+programNum);
			Refresh();
		}

		private static void SetRow(long programNum,string desc,string value,long clinicNum=0) {
			ProgramProperty programProperty=ProgramProperties.InsertIfMissing(programNum,desc,clinicNum,"",false,false);
			ProgramProperty programPropertyOld=programProperty.Copy();
			programProperty.PropertyValue=value;
			ProgramProperties.Update(programProperty,programPropertyOld);
			Refresh();
		}

		[TestMethod]
		public void AllDisabled_NoProcessorShows() {
			//Even fully set up, a processor whose program link is off shows nothing.
			SetRow(_programNumPayConnect,OpenDentBusiness.PayConnect.ProgramProperties.ProgramVersion,"2");
			SetRow(_programNumPayConnect,OpenDentBusiness.PayConnect.ProgramProperties.ApiKey,SecretsTestHelper.Protect("integrator-key"));
			SetRow(_programNumPayConnect,OpenDentBusiness.PayConnect.ProgramProperties.ApiSecret,SecretsTestHelper.Protect("merchant-secret"));
			SetRow(_programNumPaySimple,OpenDentBusiness.PaySimple.PropertyDescs.PaySimpleApiUserName,"office-user");
			SetRow(_programNumPaySimple,OpenDentBusiness.PaySimple.PropertyDescs.PaySimpleApiKey,SecretsTestHelper.Protect("office-key"));
			SetEdgeExpressCreds("PlainTextKeyFromOpenDental000001");
			Assert.IsFalse(PaymentWindowRules.ShowPayConnect(0,_listDefsEmpty));
			Assert.IsFalse(PaymentWindowRules.ShowPaySimple(0,_listDefsEmpty));
			Assert.IsFalse(PaymentWindowRules.ShowEdgeExpress(0));
		}

		[TestMethod]
		public void PayConnect2_ShowsOnlyWithTheApiKeyAndSecret() {
			Enable(_programNumPayConnect);
			SetRow(_programNumPayConnect,OpenDentBusiness.PayConnect.ProgramProperties.ProgramVersion,"2");
			Assert.IsFalse(PaymentWindowRules.ShowPayConnect(0,_listDefsEmpty),"Enabled alone is not enough (the old clinics-off rule showed it).");
			SetRow(_programNumPayConnect,OpenDentBusiness.PayConnect.ProgramProperties.ApiSecret,SecretsTestHelper.Protect("merchant-secret"));
			Assert.IsFalse(PaymentWindowRules.ShowPayConnect(0,_listDefsEmpty),"No API key yet.");
			SetRow(_programNumPayConnect,OpenDentBusiness.PayConnect.ProgramProperties.ApiKey,SecretsTestHelper.Protect("integrator-key"));
			Assert.IsTrue(PaymentWindowRules.ShowPayConnect(0,_listDefsEmpty));
			Assert.IsTrue(PaymentWindowRules.ShowPayConnect(0,null),"Adding a card skips the payment-type check.");
			Assert.IsTrue(PaymentWindowRules.ShowPayConnect(7,_listDefsEmpty),"Clinics off: a clinic left on the payment uses the practice rows.");
		}

		[TestMethod]
		public void PayConnect_UnknownVersion_Hidden() {
			Enable(_programNumPayConnect);
			Assert.IsFalse(PaymentWindowRules.ShowPayConnect(0,_listDefsEmpty));
			SetRow(_programNumPayConnect,OpenDentBusiness.PayConnect.ProgramProperties.ProgramVersion,"1");
			Assert.IsFalse(PaymentWindowRules.ShowPayConnect(0,_listDefsEmpty),"PayConnect 1.0 was removed, so a version 1 clinic never shows the button.");
		}

		[TestMethod]
		public void PaySimple_ShowsOnlyWithAUserNameAndReadableKey() {
			Enable(_programNumPaySimple);
			Assert.IsFalse(PaymentWindowRules.ShowPaySimple(0,_listDefsEmpty),"Enabled alone is not enough.");
			SetRow(_programNumPaySimple,OpenDentBusiness.PaySimple.PropertyDescs.PaySimpleApiUserName,"office-user");
			Assert.IsFalse(PaymentWindowRules.ShowPaySimple(0,_listDefsEmpty),"No key yet.");
			SetRow(_programNumPaySimple,OpenDentBusiness.PaySimple.PropertyDescs.PaySimpleApiKey,"osd1$not-a-real-ciphertext");
			Assert.IsFalse(PaymentWindowRules.ShowPaySimple(0,_listDefsEmpty),"An unreadable key counts as missing.");
			SetRow(_programNumPaySimple,OpenDentBusiness.PaySimple.PropertyDescs.PaySimpleApiKey,SecretsTestHelper.Protect("office-key"));
			Assert.IsTrue(PaymentWindowRules.ShowPaySimple(0,_listDefsEmpty));
			SetRow(_programNumPaySimple,OpenDentBusiness.PaySimple.PropertyDescs.PaySimpleApiKey,"PlainKeyOpenDentalSaved");
			Assert.IsTrue(PaymentWindowRules.ShowPaySimple(0,_listDefsEmpty),"A key Open Dental stored in plain text still works until the next save.");
		}

		private static void SetEdgeExpressCreds(string authKeyStored) {
			SetRow(_programNumEdgeExpress,ProgramProperties.PropertyDescs.EdgeExpress.XWebID,"800000000001");
			SetRow(_programNumEdgeExpress,ProgramProperties.PropertyDescs.EdgeExpress.TerminalID,"80000001");
			SetRow(_programNumEdgeExpress,ProgramProperties.PropertyDescs.EdgeExpress.AuthKey,authKeyStored);
			SetRow(_programNumEdgeExpress,ProgramProperties.PropertyDescs.EdgeExpress.PaymentType,"1");
		}

		[TestMethod]
		public void EdgeExpress_ShowsOnlyWithWellFormedCredentials() {
			Enable(_programNumEdgeExpress);
			Assert.IsFalse(PaymentWindowRules.ShowEdgeExpress(0),"Enabled alone is not enough (the old clinics-off rule showed it).");
			SetEdgeExpressCreds("too-short");
			Assert.IsFalse(PaymentWindowRules.ShowEdgeExpress(0),"A malformed Auth Key would be refused by every request.");
			SetEdgeExpressCreds(SecretsTestHelper.Protect("EncryptedKeyForClinicTwo00000002"));
			Assert.IsTrue(PaymentWindowRules.ShowEdgeExpress(0));
			SetEdgeExpressCreds("PlainTextKeyFromOpenDental000001");
			Assert.IsTrue(PaymentWindowRules.ShowEdgeExpress(0));
		}
	}
}
