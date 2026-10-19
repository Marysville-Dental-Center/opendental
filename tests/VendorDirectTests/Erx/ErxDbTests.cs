// Tests for the ERX package against a scratch MariaDB database on this computer: runtime rows, encrypted secrets, the local DoseSpot OID root,
// the local NewCrop provider gate and the one-time import of a reseller's preferences. No vendor is contacted. Skipped (inconclusive) when no
// server answers at 127.0.0.1:3306 as root with a blank password, or when VENDORDIRECT_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenDentBusiness;
using OpenDentBusiness.VendorDirect;

namespace VendorDirectTests.Erx {
	[TestClass]
	public class ErxDbTests {
		private const string DB_NAME="vendordirect_erx_tests";
		private const string CONNECTION="Server=127.0.0.1;Port=3306;User ID=root;Password=;Connection Timeout=20";
		private const string NPI="1234567893";
		private static string _skipReason;

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
					Exec(connection,"CREATE TABLE oidexternal (OIDExternalNum bigint(20) NOT NULL AUTO_INCREMENT,IDType varchar(255) NOT NULL,"
						+"IDInternal bigint(20) NOT NULL,IDExternal varchar(255) NOT NULL,rootExternal varchar(255) NOT NULL,PRIMARY KEY (OIDExternalNum),"
						+"KEY IDInternal (IDInternal)) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
					Exec(connection,"CREATE TABLE providererx (ProviderErxNum bigint(20) NOT NULL AUTO_INCREMENT,PatNum bigint(20) NOT NULL,"
						+"NationalProviderID varchar(255) NOT NULL,IsEnabled tinyint(4) NOT NULL,IsIdentifyProofed tinyint(4) NOT NULL,"
						+"IsSentToHq tinyint(4) NOT NULL,IsEpcs tinyint(4) NOT NULL,ErxType tinyint(4) NOT NULL,UserId varchar(255) NOT NULL,"
						+"AccountId varchar(25) NOT NULL,RegistrationKeyNum bigint(20) NOT NULL,PRIMARY KEY (ProviderErxNum),KEY PatNum (PatNum)) "
						+"ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
					Exec(connection,"INSERT INTO preference (PrefName,ValueString) VALUES ('RandomPrimaryKeys','0'),('NewCropName',''),"
						+"('NewCropPartnerName',''),('NewCropPassword',''),('NewCropAccountId','1234-W6c43')");
				}
				new DataConnection().SetDb("127.0.0.1",DB_NAME,"root","","root","",DatabaseType.MySql,true);
				Prefs.RefreshCache();
				Programs.InsertIfMissing(ProgramName.eRx,"eRx");
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
			ErxTestData.UseEnUs();
			//Other test classes may have pointed the connection at their own scratch database.
			new DataConnection().SetDb("127.0.0.1",DB_NAME,"root","","root","",DatabaseType.MySql,true);
			using(MySqlConnection connection=new MySqlConnection(CONNECTION)) {
				connection.Open();
				Exec(connection,"DELETE FROM "+DB_NAME+".programproperty");
				Exec(connection,"DELETE FROM "+DB_NAME+".oidexternal");
				Exec(connection,"DELETE FROM "+DB_NAME+".providererx");
				Exec(connection,"UPDATE "+DB_NAME+".preference SET ValueString='' WHERE PrefName IN('NewCropName','NewCropPartnerName','NewCropPassword')");
				Exec(connection,"UPDATE "+DB_NAME+".program SET Enabled=0");
			}
			Prefs.RefreshCache();
			Programs.RefreshCache();
			ProgramProperties.RefreshCache();
			ProviderErxs.RefreshCache();
			SetRaw(OpenDentBusiness.Erx.PropertyDescs.ErxOption,0,((int)ErxOption.DoseSpot).ToString());
		}

		private static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.ExecuteNonQuery();
			}
		}

		private static long ErxProgramNum {
			get { return Programs.GetProgramNum(ProgramName.eRx); }
		}

		///<summary>Writes a row the way an older version left it: plain, unmasked.</summary>
		private static void SetRaw(string desc,long clinicNum,string value) {
			ProgramProperty programProperty=ProgramProperties.InsertIfMissing(ErxProgramNum,desc,clinicNum,"",false,false);
			ProgramProperty programPropertyOld=programProperty.Copy();
			programProperty.PropertyValue=value;
			ProgramProperties.Update(programProperty,programPropertyOld);
			ProgramProperties.RefreshCache();
		}

		private static ProgramProperty GetRow(string desc,long clinicNum=0) {
			return ProgramProperties.GetOneFromDb(ErxProgramNum,desc,clinicNum);
		}

		#region DoseSpot
		[TestMethod]
		public void GetDoseSpotRoot_EmptyTable_InsertsALocalRootWithoutAnyServer() {
			string root=DoseSpot.GetDoseSpotRoot();
			Assert.AreEqual(OIDInternals.LegacyOidArc+".25128."+DoseSpot.LocalRootSuffix,root);
			Assert.AreEqual(root,DoseSpot.GetDoseSpotRoot(),"Idempotent.");
			using(MySqlConnection connection=new MySqlConnection(CONNECTION)) {
				connection.Open();
				using(MySqlCommand mySqlCommand=new MySqlCommand("SELECT COUNT(*) FROM "+DB_NAME+".oidexternal",connection)) {
					Assert.AreEqual(1L,Convert.ToInt64(mySqlCommand.ExecuteScalar()));
				}
			}
			OIDExternal oIDExternal=DoseSpot.CreateOIDForPatient(4321,77);
			Assert.AreEqual(root+"."+(int)IdentifierType.Patient,oIDExternal.rootExternal);
			Assert.AreEqual("4321",DoseSpot.GetDoseSpotPatID(77).IDExternal);
		}

		[TestMethod]
		public void GetDoseSpotRoot_KeepsARootWrittenUnderThePreviousVendor() {
			OIDExternal oIDExternal=new OIDExternal();
			oIDExternal.IDType=IdentifierType.Root;
			oIDExternal.rootExternal=OIDInternals.LegacyOidArc+".25128.98765";
			OIDExternals.Insert(oIDExternal);
			Assert.AreEqual(OIDInternals.LegacyOidArc+".25128.98765",DoseSpot.GetDoseSpotRoot());
		}

		[TestMethod]
		public void EnsureProperties_AddsSecretRowsAndMasksOldClinicKeys() {
			SetRaw(OpenDentBusiness.Erx.PropertyDescs.ClinicKey,0,"plainkey");
			Assert.IsTrue(DoseSpotConfig.EnsureProperties());
			ProgramProperty programPropertyKey=GetRow(OpenDentBusiness.Erx.PropertyDescs.ClinicKey);
			Assert.IsTrue(programPropertyKey.IsMasked && programPropertyKey.IsHighSecurity);
			Assert.AreEqual("plainkey",programPropertyKey.PropertyValue,"The value is kept; it is encrypted on the next save.");
			ProgramProperty programPropertySubscription=GetRow(DoseSpotConfig.PropSubscriptionKey);
			Assert.IsTrue(programPropertySubscription.IsMasked && programPropertySubscription.IsHighSecurity);
			Assert.AreEqual("",programPropertySubscription.PropertyValue);
			Assert.AreEqual(DoseSpotEnvironment.Production.ToString(),GetRow(DoseSpotConfig.PropEnvironment).PropertyValue);
			ProgramProperties.RefreshCache();
			Assert.AreEqual(DoseSpotEnvironment.Production,DoseSpotConfig.GetEnvironment());
		}

		[TestMethod]
		public void GetClinicIdAndKey_DecryptsAcceptsPlainTextAndRefusesUnreadable() {
			SetRaw(OpenDentBusiness.Erx.PropertyDescs.ClinicID,0,"321");
			SetRaw(OpenDentBusiness.Erx.PropertyDescs.ClinicKey,0,"plain+key");
			DoseSpot.GetClinicIdAndKey(0,"",null,null,out string clinicId,out string clinicKey);
			Assert.AreEqual("321",clinicId);
			Assert.AreEqual("plain+key",clinicKey,"Open Dental's plain-text key passes through.");
			Assert.IsTrue(VendorCredentials.WriteSecret(ErxProgramNum,OpenDentBusiness.Erx.PropertyDescs.ClinicKey,0,"secret/key="));
			ProgramProperties.RefreshCache();
			SecretsTestHelper.AssertRoundTrip("secret/key=",GetRow(OpenDentBusiness.Erx.PropertyDescs.ClinicKey).PropertyValue);
			DoseSpot.GetClinicIdAndKey(0,"",null,null,out clinicId,out clinicKey);
			Assert.AreEqual("secret/key=",clinicKey);
			string stored=GetRow(OpenDentBusiness.Erx.PropertyDescs.ClinicKey).PropertyValue;
			SetRaw(OpenDentBusiness.Erx.PropertyDescs.ClinicKey,0,stored.Substring(0,stored.Length-4)+(stored.EndsWith("AAAA") ? "BBBB" : "AAAA"));
			ODException ex=AssertEx.Throws<ODException>(() => DoseSpot.GetClinicIdAndKey(0,"",null,null,out _,out _));
			StringAssert.Contains(ex.Message,"can't be read");
		}

		[TestMethod]
		public void Credentials_ComeFromSetupAndAreNotConfiguredWithoutASubscriptionKey() {
			DoseSpotConfig.EnsureProperties();
			ProgramProperties.RefreshCache();
			DoseSpotException ex=AssertEx.Throws<DoseSpotException>(() => DoseSpotCredentials.FromConfig("1","k","2"));
			Assert.AreEqual(DoseSpotErrorKind.NotConfigured,ex.Kind);
			Assert.AreEqual(3,DoseSpotConfig.GetMissingClinicItems(0).Count,"Subscription key, clinic ID and clinic key.");
			Assert.IsTrue(VendorCredentials.WriteSecret(ErxProgramNum,DoseSpotConfig.PropSubscriptionKey,0,"sub"));
			VendorCredentials.WriteSetting(ErxProgramNum,DoseSpotConfig.PropEnvironment,0,"Staging");
			SetRaw(OpenDentBusiness.Erx.PropertyDescs.ClinicID,0,"321");
			Assert.IsTrue(VendorCredentials.WriteSecret(ErxProgramNum,OpenDentBusiness.Erx.PropertyDescs.ClinicKey,0,"ck"));
			ProgramProperties.RefreshCache();
			Assert.AreEqual(0,DoseSpotConfig.GetMissingClinicItems(0).Count);
			DoseSpotCredentials doseSpotCredentials=DoseSpotCredentials.FromConfig("321","ck","2");
			Assert.AreEqual("sub",doseSpotCredentials.SubscriptionKey);
			Assert.AreEqual(DoseSpotConfig.UrlApiStaging,doseSpotCredentials.ApiBaseUrl);
			StringAssert.StartsWith(OpenDentBusiness.Erx.GetRxDoseSpotUrl("&a=1"),DoseSpotConfig.UrlSsoStaging+"&a=1");
		}

		[TestMethod]
		public void SubscriptionKey_IgnoresClinicRows() {
			Assert.IsTrue(VendorCredentials.WriteSecret(ErxProgramNum,DoseSpotConfig.PropSubscriptionKey,5,"clinic-only"));
			ProgramProperties.RefreshCache();
			Assert.IsFalse(DoseSpotConfig.ReadSubscriptionKey().HasValue);
		}
		#endregion DoseSpot

		#region NewCrop
		[TestMethod]
		public void NewCropConfig_SavesTheSecretEncryptedAndLoadsItBack() {
			Assert.IsTrue(NewCropConfig.InsertMissingProperties());
			ProgramProperties.RefreshCache();
			NewCropConfig newCropConfigFresh=NewCropConfig.Load();
			Assert.IsFalse(newCropConfigFresh.IsConfigured(),"Disabled until the office enters its values.");
			Assert.AreEqual(NewCropConfig.DefaultSiteId,newCropConfigFresh.SiteId);
			ProgramProperty programPropertyPassword=GetRow(NewCropConfig.PropPassword);
			Assert.IsTrue(programPropertyPassword.IsMasked && programPropertyPassword.IsHighSecurity);
			NewCropConfig newCropConfig=new NewCropConfig();
			newCropConfig.Environment=NewCropEnvironment.Preproduction;
			newCropConfig.PartnerName="P";
			newCropConfig.AccountName="A";
			newCropConfig.Password="pw+/=";
			newCropConfig.AccountId=NewCropConfig.GenerateAccountId();
			newCropConfig.SiteId="";
			newCropConfig.ProductName="Prod";
			Assert.IsTrue(newCropConfig.Save(false,out string error),error);
			ProgramProperties.RefreshCache();
			SecretsTestHelper.AssertRoundTrip("pw+/=",GetRow(NewCropConfig.PropPassword).PropertyValue);
			NewCropConfig newCropConfigLoaded=NewCropConfig.Load();
			Assert.IsTrue(newCropConfigLoaded.IsConfigured());
			Assert.AreEqual("pw+/=",newCropConfigLoaded.Password);
			Assert.AreEqual(NewCropEnvironment.Preproduction,newCropConfigLoaded.Environment);
			Assert.AreEqual("1",newCropConfigLoaded.SiteId);
			Assert.AreEqual(NewCropConfig.UrlUpdate1Preproduction,newCropConfigLoaded.GetUpdate1Url());
			Assert.AreEqual(NewCropConfig.UrlRxEntryPreproduction,newCropConfigLoaded.GetRxEntryUrl());
			Assert.AreEqual("pw+/=",OpenDentBusiness.NewCrop.NewCropAccountPasssword);
			Assert.AreEqual("Prod",OpenDentBusiness.NewCrop.NewCropProductName);
			//A blank password on screen leaves the stored one alone unless the user cleared it.
			newCropConfigLoaded.Password="";
			Assert.IsTrue(newCropConfigLoaded.Save(false,out error));
			ProgramProperties.RefreshCache();
			Assert.AreEqual("pw+/=",NewCropConfig.Load().Password);
		}

		[TestMethod]
		public void RefreshPrescriptions_SendsNothingWhileNotConfigured() {
			NewCropConfig.InsertMissingProperties();
			SetRaw(OpenDentBusiness.Erx.PropertyDescs.ErxOption,0,((int)ErxOption.NewCrop).ToString());
			using(MySqlConnection connection=new MySqlConnection(CONNECTION)) {
				connection.Open();
				Exec(connection,"UPDATE "+DB_NAME+".program SET Enabled=1 WHERE ProgName='eRx'");
			}
			Programs.RefreshCache();
			NewCropConfig newCropConfig=new NewCropConfig { PartnerName="P",AccountName="A",Password="pw",AccountId="ACC",ProductName="Prod" };
			Assert.IsTrue(newCropConfig.Save(false,out _));
			string stored=GetRow(NewCropConfig.PropPassword).PropertyValue;
			SetRaw(NewCropConfig.PropPassword,0,stored.Substring(0,stored.Length-4)+(stored.EndsWith("AAAA") ? "BBBB" : "AAAA"));
			Assert.IsFalse(NewCropConfig.Load().IsConfigured(),"An undecryptable password means not configured.");
			List<RxPat> listRxPats=null;
			//With the password unreadable nothing is built or sent: this returns at once instead of calling the SOAP service.
			Assert.IsFalse(OpenDentBusiness.NewCrop.RefreshPrescriptions(77,ref listRxPats));
		}

		[TestMethod]
		public void ImportLegacyPrefs_MovesAResellersCredentialsOnce() {
			Prefs.UpdateString(PrefName.NewCropName,"ResellerAccount");
			Prefs.UpdateString(PrefName.NewCropPartnerName,"ResellerPartner");
			Prefs.UpdateString(PrefName.NewCropPassword,"resellerPw");
			Prefs.RefreshCache();
			Assert.IsTrue(NewCropConfig.InsertMissingProperties());//Inserts the rows, then imports.
			Prefs.RefreshCache();
			ProgramProperties.RefreshCache();
			Assert.AreEqual("ResellerAccount",GetRow(NewCropConfig.PropAccountName).PropertyValue);
			Assert.AreEqual("ResellerPartner",GetRow(NewCropConfig.PropPartnerName).PropertyValue);
			SecretsTestHelper.AssertRoundTrip("resellerPw",GetRow(NewCropConfig.PropPassword).PropertyValue);
			Assert.AreEqual("",PrefC.GetString(PrefName.NewCropPassword),"The plain-text password preference is blanked.");
			Assert.AreEqual("1234-W6c43",PrefC.GetString(PrefName.NewCropAccountId),"The previous account ID is left alone.");
			Assert.AreEqual("",GetRow(NewCropConfig.PropAccountId).PropertyValue);
			Assert.IsFalse(NewCropConfig.ImportLegacyPrefs(),"Runs once.");
		}

		[TestMethod]
		public void ProviderGate_IsLocalAndStartsDisabled() {
			Assert.IsFalse(NewCropConfig.IsProviderEnabled(NPI));
			ProviderErx providerErx=NewCropConfig.EnsureProviderErx("123-456-7893");
			Assert.AreEqual(ErxStatus.Disabled,providerErx.IsEnabled);
			Assert.AreEqual(ErxOption.NewCrop,providerErx.ErxType);
			Assert.IsNull(NewCropConfig.EnsureProviderErx("12345"),"An NPI that isn't 10 digits gets no row.");
			Assert.IsTrue(NewCropConfig.SetProviderEnabled(NPI,true));
			Assert.IsTrue(NewCropConfig.IsProviderEnabled(NPI));
			Assert.IsFalse(NewCropConfig.SetProviderEnabled(NPI,true),"No change.");
			Assert.IsTrue(NewCropConfig.SetProviderEnabled(NPI,false));
			Assert.IsFalse(NewCropConfig.IsProviderEnabled(NPI));
			Assert.AreEqual(1,ProviderErxs.GetWhere(x => x.NationalProviderID==NPI).Count);
		}
		#endregion NewCrop
	}
}
