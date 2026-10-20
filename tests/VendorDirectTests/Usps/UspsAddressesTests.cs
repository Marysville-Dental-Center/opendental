// Tests for the USPS address validation program link: the gates that keep it off by default, the settings it reads, and the rows it inserts.
// The gate tests fill the caches directly (no database). The row tests use a scratch MariaDB database on this computer and are skipped
// (inconclusive) when no server answers at 127.0.0.1:3306 as root with a blank password, or when VENDORDIRECT_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenDentBusiness;
using OpenDentBusiness.VendorDirect;

namespace VendorDirectTests.Usps {
	[TestClass]
	public class UspsAddressesTests {
		private const long PROGRAM_NUM=9001;

		[TestCleanup]
		public void TestCleanup() {
			Programs.ClearCache();
			ProgramProperties.ClearCache();
			UspsAddresses.ClearQuotaCooldown();
		}

		///<summary>Fills the program and property caches as if the database held them. Pass null to leave a row out.</summary>
		private static void FillCaches(bool isEnabled,string key,string secretStored,string environment="Production",bool hasProgram=true) {
			List<Program> listPrograms=new List<Program>();
			if(hasProgram) {
				Program program=new Program();
				program.ProgramNum=PROGRAM_NUM;
				program.ProgName=ProgramName.USPSAddresses.ToString();
				program.ProgDesc=UspsAddresses.ProgDesc;
				program.Enabled=isEnabled;
				program.Path="";
				program.CommandLine="";
				program.Note="";
				program.PluginDllName="";
				program.ButtonImage="";
				program.FileTemplate="";
				program.FilePath="";
				program.CustErr="";
				listPrograms.Add(program);
			}
			Programs.FillCacheFromTable(OpenDentBusiness.Crud.ProgramCrud.ListToTable(listPrograms,"Program"));
			List<ProgramProperty> listProgramProperties=new List<ProgramProperty>();
			long programPropertyNum=1;
			void Add(string desc,string value,bool isSecret) {
				if(value==null) {
					return;
				}
				ProgramProperty programProperty=new ProgramProperty();
				programProperty.ProgramPropertyNum=programPropertyNum++;
				programProperty.ProgramNum=PROGRAM_NUM;
				programProperty.PropertyDesc=desc;
				programProperty.PropertyValue=value;
				programProperty.ComputerName="";
				programProperty.ClinicNum=0;
				programProperty.IsMasked=isSecret;
				programProperty.IsHighSecurity=isSecret;
				listProgramProperties.Add(programProperty);
			}
			Add(UspsAddresses.PropConsumerKey,key,false);
			Add(UspsAddresses.PropConsumerSecret,secretStored,true);
			Add(UspsAddresses.PropEnvironment,environment,false);
			ProgramProperties.FillCacheFromTable(OpenDentBusiness.Crud.ProgramPropertyCrud.ListToTable(listProgramProperties,"ProgramProperty"));
		}

		[TestMethod]
		public void IsConfigured_NeedsEnabledKeyAndSecret() {
			string secretStored=SecretsTestHelper.Protect("test-secret");
			FillCaches(true,"test-key",secretStored);
			Assert.IsTrue(UspsAddresses.IsConfigured());
			Assert.IsTrue(UspsAddresses.IsReadyToVerify());
			FillCaches(false,"test-key",secretStored);
			Assert.IsFalse(UspsAddresses.IsConfigured(),"Program disabled (the default).");
			FillCaches(true,"",secretStored);
			Assert.IsFalse(UspsAddresses.IsConfigured(),"Key empty.");
			FillCaches(true,"test-key","");
			Assert.IsFalse(UspsAddresses.IsConfigured(),"Secret empty.");
			FillCaches(true,"test-key",null);
			Assert.IsFalse(UspsAddresses.IsConfigured(),"Secret row missing.");
			FillCaches(true,"test-key",secretStored,hasProgram:false);
			Assert.IsFalse(UspsAddresses.IsConfigured(),"No program row yet.");
		}

		[TestMethod]
		public void IsConfigured_UnreadableSecret_IsNotConfigured() {
			FillCaches(true,"test-key","osd1$not-a-real-ciphertext");
			Assert.IsFalse(UspsAddresses.IsConfigured());
			FillCaches(true,"test-key","odv2e$Open-Dental-ciphertext");
			Assert.IsFalse(UspsAddresses.IsConfigured());
		}

		[TestMethod]
		public void IsReadyToVerify_IsFalseDuringTheQuotaCooldown() {
			FillCaches(true,"test-key",SecretsTestHelper.Protect("test-secret"));
			UspsAddresses.StartQuotaCooldown(DateTime.UtcNow);
			Assert.IsFalse(UspsAddresses.IsReadyToVerify());
			UspsAddresses.ClearQuotaCooldown();
			Assert.IsTrue(UspsAddresses.IsReadyToVerify());
		}

		[TestMethod]
		public void GetConfig_DecryptsTheSecretAndReadsTheEnvironment() {
			string secretStored=SecretsTestHelper.Protect("test-secret");
			SecretsTestHelper.AssertRoundTrip("test-secret",secretStored);
			FillCaches(true," test-key ",secretStored,"Testing");
			UspsConfig uspsConfig=UspsAddresses.GetConfig();
			Assert.AreEqual("test-key",uspsConfig.ConsumerKey);
			Assert.AreEqual("test-secret",uspsConfig.ConsumerSecret);
			Assert.AreEqual(UspsEnvironment.Testing,uspsConfig.Environment);
			Assert.IsFalse(string.IsNullOrEmpty(uspsConfig.Fingerprint));
			Assert.IsFalse(uspsConfig.Fingerprint.Contains("test-secret"));
			string fingerprintOld=uspsConfig.Fingerprint;
			FillCaches(true,"test-key",SecretsTestHelper.Protect("other-secret"),"Testing");
			Assert.AreNotEqual(fingerprintOld,UspsAddresses.GetConfig().Fingerprint,"A changed secret gives a new fingerprint.");
		}

		[TestMethod]
		public void GetConfig_TypedPlainTextSecret_IsAccepted() {
			FillCaches(true,"test-key","typed-in-plain");
			Assert.AreEqual("typed-in-plain",UspsAddresses.GetConfig().ConsumerSecret);
		}

		[TestMethod]
		public void Check_NotConfigured_ThrowsWithoutCalling() {
			FillCaches(false,"test-key",SecretsTestHelper.Protect("test-secret"));
			Address address=new Address();
			address.Address1="3120 M St NW";
			address.City="Washington";
			address.State="DC";
			UspsApiException ex=AssertEx.Throws<UspsApiException>(() => USPSAddressValidationDatas.GetUSPSAddressValidationResponse(address));
			Assert.AreEqual(UspsErrorKind.NotConfigured,ex.Kind);
		}

		[TestMethod]
		public void Check_NotEnoughAddress_ReturnsMessageWithoutCalling() {
			FillCaches(true,"test-key",SecretsTestHelper.Protect("test-secret"));
			Address address=new Address();
			address.Address1="1 Main St";
			address.City="Springfield";
			//No state and no ZIP: nothing USPS could check, so no call (the default client would reach the network) and no billed lookup.
			USPSAddressValidationResponse response=USPSAddressValidationDatas.GetUSPSAddressValidationResponse(address);
			Assert.IsNull(response.address);
			StringAssert.Contains(response.ErrorMessage,"ZIP");
		}
	}

	[TestClass]
	public class UspsAddressesDbTests {
		private static readonly string DB_NAME=ScratchDb.Name("vendordirect_usps_tests");
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
			//Point the business layer at this class's database for every test, so tests of other classes can't leave it elsewhere.
			new DataConnection().SetDb("127.0.0.1",DB_NAME,"root","","root","",DatabaseType.MySql,true);
			Prefs.RefreshCache();
			using(MySqlConnection connection=new MySqlConnection("Server=127.0.0.1;Port=3306;User ID=root;Password=;Database="+DB_NAME)) {
				connection.Open();
				Exec(connection,"DELETE FROM programproperty");
				Exec(connection,"DELETE FROM program");
			}
			Programs.RefreshCache();
			ProgramProperties.RefreshCache();
		}

		[TestCleanup]
		public void TestCleanup() {
			if(_skipReason!=null) {
				return;
			}
			Programs.ClearCache();
			ProgramProperties.ClearCache();
		}

		private static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.ExecuteNonQuery();
			}
		}

		[TestMethod]
		public void EnsureProgram_InsertsADisabledRowAndItsPropertiesOnce() {
			Assert.IsTrue(UspsAddresses.EnsureProgram());
			Programs.RefreshCache();
			ProgramProperties.RefreshCache();
			Assert.IsFalse(UspsAddresses.EnsureProgram(),"Second call inserts nothing.");
			Programs.RefreshCache();
			List<Program> listPrograms=Programs.GetWhere(x => x.ProgName==ProgramName.USPSAddresses.ToString());
			Assert.AreEqual(1,listPrograms.Count);
			Assert.IsFalse(listPrograms[0].Enabled,"Off by default.");
			Assert.AreEqual(UspsAddresses.ProgDesc,listPrograms[0].ProgDesc);
			List<ProgramProperty> listProgramProperties=ProgramProperties.GetForProgram(listPrograms[0].ProgramNum);
			Assert.AreEqual(3,listProgramProperties.Count);
			ProgramProperty programPropertySecret=listProgramProperties.Single(x => x.PropertyDesc==UspsAddresses.PropConsumerSecret);
			Assert.IsTrue(programPropertySecret.IsMasked);
			Assert.IsTrue(programPropertySecret.IsHighSecurity);
			Assert.AreEqual("",programPropertySecret.PropertyValue);
			Assert.AreEqual("Production",listProgramProperties.Single(x => x.PropertyDesc==UspsAddresses.PropEnvironment).PropertyValue);
			ProgramProperty programPropertyKey=listProgramProperties.Single(x => x.PropertyDesc==UspsAddresses.PropConsumerKey);
			Assert.IsFalse(programPropertyKey.IsMasked);
			Assert.IsFalse(UspsAddresses.IsConfigured());
		}

		[TestMethod]
		public void SavedSecret_IsOsd1AndMakesTheProgramConfigured() {
			UspsAddresses.EnsureProgram();
			Programs.RefreshCache();
			long programNum=Programs.GetProgramNum(ProgramName.USPSAddresses);
			Assert.IsTrue(VendorCredentials.WriteSecret(programNum,UspsAddresses.PropConsumerSecret,0,"test-secret"));
			VendorCredentials.WriteSetting(programNum,UspsAddresses.PropConsumerKey,0,"test-key");
			Program program=Programs.GetProgram(programNum);
			program.Enabled=true;
			Programs.Update(program);
			Programs.RefreshCache();
			ProgramProperties.RefreshCache();
			ProgramProperty programPropertySecret=ProgramProperties.GetOneFromDb(programNum,UspsAddresses.PropConsumerSecret,0);
			SecretsTestHelper.AssertRoundTrip("test-secret",programPropertySecret.PropertyValue);
			Assert.IsTrue(programPropertySecret.IsMasked && programPropertySecret.IsHighSecurity);
			Assert.IsTrue(UspsAddresses.IsConfigured());
			Assert.AreEqual("test-secret",UspsAddresses.GetConfig().ConsumerSecret);
		}

		[TestMethod]
		public void EnsureProgram_RaisesTheFlagsOnAnUnmaskedSecretRow() {
			Programs.InsertIfMissing(ProgramName.USPSAddresses,UspsAddresses.ProgDesc);
			Programs.RefreshCache();
			long programNum=Programs.GetProgramNum(ProgramName.USPSAddresses);
			ProgramProperties.InsertIfMissing(programNum,UspsAddresses.PropConsumerSecret,0,"",false,false);
			ProgramProperties.RefreshCache();
			Assert.IsTrue(UspsAddresses.EnsureProgram());
			ProgramProperty programPropertySecret=ProgramProperties.GetOneFromDb(programNum,UspsAddresses.PropConsumerSecret,0);
			Assert.IsTrue(programPropertySecret.IsMasked && programPropertySecret.IsHighSecurity);
		}
	}
}
