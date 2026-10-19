// Tests for the PostcardMania program link against a scratch MariaDB database on this computer: the runtime program row, the per-clinic account
// with default fallback, and the encrypted secret. Skipped (inconclusive) when no server answers at 127.0.0.1:3306 as root with a blank password,
// or when VENDORDIRECT_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Linq;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenDentBusiness;
using OpenDentBusiness.PostcardMania;

namespace VendorDirectTests.PostcardMania {
	[TestClass]
	public class PostcardManiaProgramDbTests {
		private const string DB_NAME="vendordirect_postcard_tests";
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
				}
				new DataConnection().SetDb("127.0.0.1",DB_NAME,"root","","root","",DatabaseType.MySql,true);
				Prefs.RefreshCache();
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
			//Each test starts from no PostcardMania row, as a fresh office would. Re-point the connection: other test classes use their own databases.
			new DataConnection().SetDb("127.0.0.1",DB_NAME,"root","","root","",DatabaseType.MySql,true);
			using(MySqlConnection connection=new MySqlConnection("Server=127.0.0.1;Port=3306;User ID=root;Password=;Database="+DB_NAME+";Connection Timeout=3")) {
				connection.Open();
				Exec(connection,"DELETE FROM programproperty");
				Exec(connection,"DELETE FROM program");
			}
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
		public void EnsureProgram_InsertsDisabledRowAndPropertiesOnce() {
			Assert.IsTrue(PostcardManiaProgram.EnsureProgram());
			RefreshCaches();
			Assert.IsFalse(PostcardManiaProgram.EnsureProgram(),"A second call is a no-op.");
			Program program=Programs.GetCur(ProgramName.PostcardMania);
			Assert.IsNotNull(program);
			Assert.IsFalse(program.Enabled,"Off until the office turns it on.");
			Assert.AreEqual(PostcardManiaProgram.ProgDesc,program.ProgDesc);
			Assert.AreEqual(1,Programs.GetWhere(x => x.ProgName==ProgramName.PostcardMania.ToString()).Count);
			Assert.IsFalse(PostcardManiaProgram.IsEnabled());
			ProgramProperty programPropertySecret=ProgramProperties.GetOneFromDb(program.ProgramNum,PostcardManiaProgram.PropertyDescs.ApiSecret,0);
			Assert.IsTrue(programPropertySecret.IsMasked && programPropertySecret.IsHighSecurity,"The secret row is masked and high security.");
			Assert.AreEqual(PostcardManiaProgram.GetDefaultValues().Count,ProgramProperties.GetListForProgramAndClinic(program.ProgramNum,0).Count);
			Assert.AreEqual(PcmMailClass.FirstClass,PostcardManiaProgram.GetDefaultMailClass());
			Assert.IsFalse(PostcardManiaProgram.IsBaaOnFile(),"No BAA until the office records one.");
			Assert.IsFalse(PostcardManiaProgram.IsAnyAccountConfigured());
		}

		[TestMethod]
		public void Account_ClinicUsesItsOwnRowsOrTheDefault() {
			PostcardManiaProgram.EnsureProgram();
			RefreshCaches();
			Assert.IsTrue(PostcardManiaProgram.SaveAccount(0,"key-default","secret-default",PostcardManiaProgram.EnvironmentSandbox,""));
			Assert.IsTrue(PostcardManiaProgram.SaveAccount(5,"key-five","secret-five",PostcardManiaProgram.EnvironmentProduction,"clinic-5"));
			RefreshCaches();
			PostcardManiaAccount account5=PostcardManiaProgram.GetAccount(5);
			Assert.AreEqual("key-five",account5.ApiKey);
			Assert.AreEqual("secret-five",account5.ApiSecret);
			Assert.AreEqual("clinic-5",account5.ChildRefNbr);
			Assert.IsFalse(account5.IsSandbox);
			Assert.AreEqual(5,account5.ClinicNumSource);
			PostcardManiaAccount account6=PostcardManiaProgram.GetAccount(6);
			Assert.AreEqual("key-default",account6.ApiKey,"A clinic without its own key uses the default.");
			Assert.AreEqual("secret-default",account6.ApiSecret);
			Assert.AreEqual("",account6.ChildRefNbr,"The default's child reference, not a mix of rows.");
			Assert.AreEqual(0,account6.ClinicNumSource);
			Assert.AreEqual(6,account6.ClinicNum);
			Assert.IsTrue(PostcardManiaProgram.IsAnyAccountConfigured());
			//Back to the default: the clinic's key and secret are blanked.
			PostcardManiaProgram.ClearClinicAccount(5);
			RefreshCaches();
			Assert.AreEqual("key-default",PostcardManiaProgram.GetAccount(5).ApiKey);
			Assert.IsFalse(PostcardManiaProgram.HasOwnAccount(5));
		}

		[TestMethod]
		public void Secret_IsStoredEncryptedAndKeptWhenNotRetyped() {
			PostcardManiaProgram.EnsureProgram();
			RefreshCaches();
			long programNum=PostcardManiaProgram.GetProgramNum();
			Assert.IsTrue(PostcardManiaProgram.SaveAccount(0,"key-1","s3cret",PostcardManiaProgram.EnvironmentSandbox,""));
			ProgramProperty programProperty=ProgramProperties.GetOneFromDb(programNum,PostcardManiaProgram.PropertyDescs.ApiSecret,0);
			SecretsTestHelper.AssertRoundTrip("s3cret",programProperty.PropertyValue);
			Assert.IsTrue(programProperty.IsMasked && programProperty.IsHighSecurity);
			Assert.IsTrue(PostcardManiaProgram.SaveAccount(0,"key-2",null,PostcardManiaProgram.EnvironmentProduction,""),"null keeps the stored secret.");
			RefreshCaches();
			PostcardManiaAccount account=PostcardManiaProgram.GetAccount(0);
			Assert.AreEqual("s3cret",account.ApiSecret);
			Assert.AreEqual("key-2",account.ApiKey);
			Assert.IsTrue(account.IsConfigured);
			Assert.IsTrue(PostcardManiaProgram.SaveAccount(0,"key-2","",PostcardManiaProgram.EnvironmentProduction,""));
			Assert.AreEqual("",ProgramProperties.GetOneFromDb(programNum,PostcardManiaProgram.PropertyDescs.ApiSecret,0).PropertyValue,"Blank stays blank.");
			RefreshCaches();
			Assert.IsFalse(PostcardManiaProgram.GetAccount(0).IsConfigured);
			//A plain-text value (never written by this program) is not sent.
			programProperty=ProgramProperties.GetOneFromDb(programNum,PostcardManiaProgram.PropertyDescs.ApiSecret,0);
			ProgramProperties.UpdateValueIfUnchanged(programProperty.ProgramPropertyNum,programProperty.PropertyValue,"typed-into-the-database");
			RefreshCaches();
			account=PostcardManiaProgram.GetAccount(0);
			Assert.AreEqual("",account.ApiSecret);
			Assert.IsTrue(account.IsSecretUnreadable);
		}

		[TestMethod]
		public void Settings_ArePracticeWide() {
			PostcardManiaProgram.EnsureProgram();
			RefreshCaches();
			PostcardManiaProgram.SaveSetting(PostcardManiaProgram.PropertyDescs.DefaultMailClass,PcmMailClass.Standard);
			PostcardManiaProgram.SaveSetting(PostcardManiaProgram.PropertyDescs.UsePreferredName,"1");
			PostcardManiaProgram.SaveSetting(PostcardManiaProgram.PropertyDescs.BaaOnFile,"1");
			RefreshCaches();
			Assert.AreEqual(PcmMailClass.Standard,PostcardManiaProgram.GetDefaultMailClass());
			Assert.IsTrue(PostcardManiaProgram.GetUsePreferredName());
			Assert.IsFalse(PostcardManiaProgram.GetSendPatientReference());
			Assert.IsTrue(PostcardManiaProgram.IsBaaOnFile());
			Assert.IsTrue(AdvertisingPostcards.FuncIsBaaOnFile());
		}

		[TestMethod]
		public void NoProgramRow_ReadsAreSafeAndNotConfigured() {
			Assert.AreEqual(0,PostcardManiaProgram.GetProgramNum());
			Assert.IsFalse(PostcardManiaProgram.IsEnabled());
			Assert.IsFalse(PostcardManiaProgram.GetAccount(3).IsConfigured);
			Assert.IsFalse(PostcardManiaProgram.IsAnyAccountConfigured());
			Assert.IsFalse(PostcardManiaProgram.IsBaaOnFile());
			Assert.AreEqual(PcmMailClass.FirstClass,PostcardManiaProgram.GetDefaultMailClass());
		}
	}
}
