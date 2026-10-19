// Integration tests for the CareCredit settings rows against a scratch MariaDB database on this computer: rows inserted at runtime, the secret
// stored as osd1$ in a masked high-security row, Open Dental's partner code treated as not set, and the once-a-day batch lock. Skipped
// (inconclusive) when no server answers at 127.0.0.1:3306 as root with a blank password, or when VENDORDIRECT_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Linq;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenDentBusiness;
using OpenDentBusiness.Bridges;
using OpenDentBusiness.VendorDirect;

namespace VendorDirectTests.CareCreditTests {
	[TestClass]
	public class CareCreditDbTests {
		private const string DB_NAME="vendordirect_carecredit_tests";
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
				using(MySqlConnection connection=new MySqlConnection("Server=127.0.0.1;Port=3306;User ID=root;Password=;Connection Timeout=3")) {
					connection.Open();
					Exec(connection,"DROP DATABASE IF EXISTS "+DB_NAME);
				}
			}
			catch(Exception) {
				//Leaving the scratch database behind is harmless; the next run drops it.
			}
		}

		///<summary>Each test starts from empty tables (other test classes may have pointed the connection elsewhere in between).</summary>
		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			using(MySqlConnection connection=new MySqlConnection("Server=127.0.0.1;Port=3306;User ID=root;Password=;Database="+DB_NAME+";Connection Timeout=3")) {
				connection.Open();
				Exec(connection,"DROP TABLE IF EXISTS program,programproperty,preference");
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
				Exec(connection,"INSERT INTO preference (PrefName,ValueString) VALUES ('RandomPrimaryKeys','0'),"
					+"('CareCreditBatchProcessDateTime','0001-01-01 00:00:00'),('CareCreditBatchPullbackDateTime','2000-01-01 06:45:00')");
				//The rows a migrated Open Dental database has: the program, disabled, with Open Dental's partner code and an old token.
				Exec(connection,"INSERT INTO program (ProgName,ProgDesc,Enabled,Path,CommandLine,Note,PluginDllName,ButtonImage,FileTemplate,FilePath,"
					+"IsDisabledByHq,CustErr) VALUES ('CareCredit','CareCredit',0,'','','','','','','',0,'')");
				Exec(connection,"INSERT INTO programproperty (ProgramNum,PropertyDesc,PropertyValue,ComputerName,ClinicNum,IsMasked,IsHighSecurity) VALUES "
					+"(1,'CareCreditPartnerCode','1065','',0,0,0),(1,'CareCreditOAuthToken','opaque-token-from-hq','',0,0,0),"
					+"(1,'CareCreditMerchantNumber','5348120000000001','',0,0,0),(1,'QSBatchEnabled','2','',0,0,0)");
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

		[TestMethod]
		public void EnsureRows_InsertsOnceAndKeepsExistingValues() {
			long programNum=CareCreditSettings.GetProgramNum();
			Assert.AreNotEqual(0,programNum);
			Assert.IsTrue(CareCreditSettings.EnsureRows(programNum));
			ProgramProperties.RefreshCache();
			Assert.IsFalse(CareCreditSettings.EnsureRows(programNum),"Nothing left to insert.");
			ProgramProperty programPropertySecret=ProgramProperties.GetOneFromDb(programNum,CareCreditSettings.ClientSecret,0);
			Assert.IsTrue(programPropertySecret.IsMasked && programPropertySecret.IsHighSecurity,"The secret row is masked and high security.");
			Assert.AreEqual("Sandbox",ProgramProperties.GetValOrDefault(programNum,CareCreditSettings.Environment,0));
			Assert.AreEqual("1065",ProgramProperties.GetValOrDefault(programNum,ProgramProperties.PropertyDescs.CareCredit.CareCreditPartnerCode,0),
				"Existing values are never overwritten.");
			Assert.AreEqual(1,ProgramProperties.GetWhere(x => x.PropertyDesc==CareCreditSettings.ClientId).Count);
		}

		[TestMethod]
		public void Load_DecryptsTheSecretAndTreatsOpenDentalsPartnerCodeAsNotSet() {
			long programNum=CareCreditSettings.GetProgramNum();
			CareCreditSettings.EnsureRows(programNum);
			Assert.IsTrue(VendorCredentials.WriteSecret(programNum,CareCreditSettings.ClientSecret,0,"s3cret"));
			VendorCredentials.WriteSetting(programNum,CareCreditSettings.ClientId,0,"office-id");
			ProgramProperties.RefreshCache();
			string stored=ProgramProperties.GetValOrDefault(programNum,CareCreditSettings.ClientSecret,0);
			SecretsTestHelper.AssertRoundTrip("s3cret",stored);
			CareCreditConfig config=CareCreditSettings.Load();
			Assert.AreEqual("office-id",config.ClientId);
			Assert.AreEqual("s3cret",config.ClientSecret);
			Assert.AreEqual("1065",config.PartnerCode);
			Assert.IsTrue(config.GetProblems().Any(x => x.Contains("different software vendor")));
			Assert.IsFalse(config.IsComplete);
			Assert.IsFalse(CareCredit.IsUsable(),"Disabled and incomplete: nothing may be sent.");
		}

		[TestMethod]
		public void PlainTextSecret_IsNotUsed() {
			long programNum=CareCreditSettings.GetProgramNum();
			CareCreditSettings.EnsureRows(programNum);
			VendorCredentials.WriteSetting(programNum,CareCreditSettings.ClientSecret,0,"typed-in-plain");
			ProgramProperties.RefreshCache();
			CareCreditConfig config=CareCreditSettings.Load();
			Assert.AreEqual("",config.ClientSecret);
			Assert.IsTrue(config.IsClientSecretUnreadable);
		}

		[TestMethod]
		public void BatchDay_IsClaimedOncePerDay() {
			DateTime date=new DateTime(2026,10,19);
			Assert.IsTrue(CareCreditSettings.TryClaimBatchDay(date));
			Assert.IsFalse(CareCreditSettings.TryClaimBatchDay(date),"A second host can't run the same day's batch.");
			Assert.IsTrue(CareCreditSettings.TryClaimBatchDay(date.AddDays(1)));
			long programNum=CareCreditSettings.GetProgramNum();
			Assert.AreEqual("2026-10-20",ProgramProperties.GetOneFromDb(programNum,CareCreditSettings.BatchLastRunDate,0).PropertyValue);
		}

		[TestMethod]
		public void Schedule_DefaultsAndStoredTimes() {
			Assert.AreEqual(CareCreditSettings.DefaultProcessTime,CareCreditSettings.GetProcessTime(),"Never set: 07:20.");
			Assert.AreEqual(new TimeSpan(6,45,0),CareCreditSettings.GetPullbackTime());
		}

		[TestMethod]
		public void BatchModeAndDays_ReadFromTheClinicOrPractice() {
			Assert.AreEqual(EnumCareCreditBatch.NoBalance,CareCreditSettings.GetBatchMode(0));
			Assert.AreEqual(EnumCareCreditBatch.NoBalance,CareCreditSettings.GetBatchMode(5),"A clinic without its own row uses the practice's.");
			Assert.AreEqual(2,CareCreditSettings.GetBatchDays(0),"Default 2.");
			Assert.AreEqual(CareCreditTestData.MERCHANT,CareCredit.GetMerchantNumberByClinic(5));
		}

		[TestMethod]
		public void JobHost_OnlyTheChosenComputer() {
			long programNum=CareCreditSettings.GetProgramNum();
			CareCreditSettings.EnsureRows(programNum);
			ProgramProperties.RefreshCache();
			Assert.IsFalse(CareCreditSettings.IsThisComputerJobHost(),"Not set: no background tasks.");
			VendorCredentials.WriteSetting(programNum,CareCreditSettings.JobComputerName,0,CodeBase.ODEnvironment.MachineName);
			ProgramProperties.RefreshCache();
			Assert.IsTrue(CareCreditSettings.IsThisComputerJobHost());
			Assert.IsFalse(new CareCreditPollJob().ShouldRun(),"Still not usable: disabled and incomplete.");
		}

		[TestMethod]
		public void LastAuthError_IsRecordedAndCleared() {
			long programNum=CareCreditSettings.GetProgramNum();
			CareCreditSettings.EnsureRows(programNum);
			CareCreditSettings.SetLastAuthError("CareCredit rejected the configured credentials.");
			Assert.AreEqual("CareCredit rejected the configured credentials.",ProgramProperties.GetOneFromDb(programNum,CareCreditSettings.LastAuthError,0).PropertyValue);
			ProgramProperties.RefreshCache();
			CareCreditSettings.SetLastAuthError("");
			Assert.AreEqual("",ProgramProperties.GetOneFromDb(programNum,CareCreditSettings.LastAuthError,0).PropertyValue);
		}
	}
}
