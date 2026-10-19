// Integration tests for the CareCredit settings rows against a scratch MariaDB database on this computer: rows inserted at runtime, the secret
// stored as osd1$ in a masked high-security row, Open Dental's partner code treated as not set, and the once-a-day batch lock. Skipped
// (inconclusive) when no server answers at 127.0.0.1:3306 as root with a blank password, or when VENDORDIRECT_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
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
				Exec(connection,"DROP TABLE IF EXISTS program,programproperty,preference,carecreditwebresponse,providerclinic");
				//Open Dental 24.3's providerclinic table.
				Exec(connection,"CREATE TABLE providerclinic (ProviderClinicNum bigint NOT NULL auto_increment PRIMARY KEY,ProvNum bigint NOT NULL,"
					+"ClinicNum bigint NOT NULL,DEANum varchar(15) NOT NULL,StateLicense varchar(50) NOT NULL,StateRxID varchar(255) NOT NULL,"
					+"StateWhereLicensed varchar(15) NOT NULL,CareCreditMerchantId varchar(20) NOT NULL,INDEX(ProvNum),INDEX(ClinicNum)) DEFAULT CHARSET=utf8mb3");
				//Open Dental 24.3's carecreditwebresponse table (ConvertDatabases6 and 7).
				Exec(connection,"CREATE TABLE carecreditwebresponse (CareCreditWebResponseNum bigint NOT NULL auto_increment PRIMARY KEY,"
					+"PatNum bigint NOT NULL,PayNum bigint NOT NULL,RefNumber varchar(255) NOT NULL,Amount double NOT NULL,WebToken varchar(255) NOT NULL,"
					+"ProcessingStatus varchar(255) NOT NULL,DateTimeEntry datetime NOT NULL DEFAULT '0001-01-01 00:00:00',"
					+"DateTimePending datetime NOT NULL DEFAULT '0001-01-01 00:00:00',DateTimeCompleted datetime NOT NULL DEFAULT '0001-01-01 00:00:00',"
					+"DateTimeExpired datetime NOT NULL DEFAULT '0001-01-01 00:00:00',DateTimeLastError datetime NOT NULL DEFAULT '0001-01-01 00:00:00',"
					+"LastResponseStr text NOT NULL,ClinicNum bigint NOT NULL,ServiceType varchar(255) NOT NULL,TransType varchar(255) NOT NULL,"
					+"MerchantNumber varchar(20) NOT NULL,HasLogged tinyint NOT NULL,INDEX(PatNum),INDEX(PayNum),INDEX(ClinicNum)) DEFAULT CHARSET=utf8mb3");
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
				Exec(connection,"INSERT INTO preference (PrefName,ValueString) VALUES ('RandomPrimaryKeys','0'),('EasyNoClinics','0'),"
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
			//No clinic or provider tables here: those caches are filled from lists (FillClinicsAndProviders), empty unless a test needs them.
			FillCaches(new List<Clinic>(),new List<Provider>());
		}

		[TestCleanup]
		public void TestCleanup() {
			if(_skipReason!=null) {
				return;
			}
			FillCaches(new List<Clinic>(),new List<Provider>());//Leave no clinics behind for other test classes in this process.
		}

		private static void FillCaches(List<Clinic> listClinics,List<Provider> listProviders) {
			Clinics.FillCacheFromTable(OpenDentBusiness.Crud.ClinicCrud.ListToTable(listClinics));
			Providers.FillCacheFromTable(OpenDentBusiness.Crud.ProviderCrud.ListToTable(listProviders));
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
		public void ClaimCompletedForPayment_OnlyOneCallerWinsAPendingRowWithNoPayment() {
			CareCreditWebResponse row=new CareCreditWebResponse();
			row.PatNum=55;
			row.RefNumber="REF-9";
			row.WebToken="S-9";
			row.LastResponseStr="";
			row.MerchantNumber=CareCreditTestData.MERCHANT;
			row.ServiceType=CareCreditServiceType.Prefill;
			row.TransType=CareCreditTransType.Purchase;
			row.ProcessingStatus=CareCreditWebStatus.Pending;
			CareCreditWebResponses.Insert(row);
			DateTime dateTimeCompleted=new DateTime(2026,10,19,7,51,0);
			Assert.AreEqual(1,CareCreditWebResponses.ClaimCompletedForPayment(row.CareCreditWebResponseNum,dateTimeCompleted));
			Assert.AreEqual(0,CareCreditWebResponses.ClaimCompletedForPayment(row.CareCreditWebResponseNum,dateTimeCompleted.AddSeconds(5)),
				"A second process finds the row already completed.");
			CareCreditWebResponse rowDb=CareCreditWebResponses.GetOne(row.CareCreditWebResponseNum);
			Assert.AreEqual(CareCreditWebStatus.Completed,rowDb.ProcessingStatus);
			Assert.AreEqual(dateTimeCompleted,rowDb.DateTimeCompleted);
			//A pending row that already has a payment (the modal purchase window's) is never claimed.
			CareCreditWebResponse rowWithPayment=row.Copy();
			rowWithPayment.PayNum=44;
			rowWithPayment.ProcessingStatus=CareCreditWebStatus.Pending;
			CareCreditWebResponses.Insert(rowWithPayment);
			Assert.AreEqual(0,CareCreditWebResponses.ClaimCompletedForPayment(rowWithPayment.CareCreditWebResponseNum,dateTimeCompleted));
		}

		[TestMethod]
		public void ApplyResult_TwoPollersWithTheSameStaleRowCreateOnePayment() {
			CareCreditWebResponse row=new CareCreditWebResponse();
			row.PatNum=55;
			row.ClinicNum=2;
			row.Amount=80;
			row.RefNumber="REF-10";
			row.WebToken="S-10";
			row.LastResponseStr="";
			row.MerchantNumber=CareCreditTestData.MERCHANT;
			row.ServiceType=CareCreditServiceType.Prefill;
			row.TransType=CareCreditTransType.Purchase;
			row.ProcessingStatus=CareCreditWebStatus.Pending;
			CareCreditWebResponses.Insert(row);
			//Both pollers read the row before either applied its result.
			CareCreditWebResponse rowPollerA=CareCreditWebResponses.GetOne(row.CareCreditWebResponseNum);
			CareCreditWebResponse rowPollerB=CareCreditWebResponses.GetOne(row.CareCreditWebResponseNum);
			int countPayments=0;
			CareCreditResultActions actions=CareCreditResultActions.CreateDb();
			actions.FuncInsertPayment=(patNum,clinicNum,amount,note,fee) => 700+(++countPayments);
			CareCreditPullbackResult result=CareCreditJson.ReadPullback("{\"status\":\"COMPLETED\",\"amount\":\"80.00\"}",false);
			DateTime dateTimeNow=new DateTime(2026,10,19,7,51,0);
			CareCreditJobs.ApplyResult(rowPollerA,result,dateTimeNow,actions);
			CareCreditJobs.ApplyResult(rowPollerB,result,dateTimeNow.AddSeconds(2),actions);
			Assert.AreEqual(1,countPayments);
			CareCreditWebResponse rowDb=CareCreditWebResponses.GetOne(row.CareCreditWebResponseNum);
			Assert.AreEqual(701,rowDb.PayNum,"The winner's payment stays on the row.");
			Assert.AreEqual(CareCreditWebStatus.Completed,rowDb.ProcessingStatus);
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

		#region Merchant numbers
		private const string MERCHANT_CLINIC5="5348120000000005";
		private const string MERCHANT_PROV3="5348120000000033";
		private const string MERCHANT_PROV3_CLINIC5="5348120000000035";

		///<summary>Clinics 5 (North) and 6 (South), providers 3 (DrA) and 4 (DrB), in the caches.</summary>
		private static void FillClinicsAndProviders() {
			List<Clinic> listClinics=new List<Clinic>();
			foreach(Tuple<long,string> tuple in new[] { Tuple.Create(5L,"North"),Tuple.Create(6L,"South") }) {
				Clinic clinic=new Clinic();
				clinic.ClinicNum=tuple.Item1;
				clinic.Abbr=tuple.Item2;
				clinic.Description=tuple.Item2;
				listClinics.Add(clinic);
			}
			List<Provider> listProviders=new List<Provider>();
			foreach(Tuple<long,string> tuple in new[] { Tuple.Create(3L,"DrA"),Tuple.Create(4L,"DrB") }) {
				Provider provider=new Provider();
				provider.ProvNum=tuple.Item1;
				provider.Abbr=tuple.Item2;
				listProviders.Add(provider);
			}
			FillCaches(listClinics,listProviders);
		}

		private static void SetClinicMerchantNumber(long clinicNum,string merchantNum) {
			long programNum=CareCreditSettings.GetProgramNum();
			string desc=ProgramProperties.PropertyDescs.CareCredit.CareCreditMerchantNumber;
			VendorCredentials.WriteSetting(programNum,desc,clinicNum,merchantNum);
			ProgramProperties.RefreshCache();
		}

		private static void InsertProviderClinic(long provNum,long clinicNum,string merchantNum,string deaNum="") {
			ProviderClinic providerClinic=new ProviderClinic();
			providerClinic.ProvNum=provNum;
			providerClinic.ClinicNum=clinicNum;
			providerClinic.DEANum=deaNum;
			providerClinic.StateLicense="";
			providerClinic.StateRxID="";
			providerClinic.StateWhereLicensed="";
			providerClinic.CareCreditMerchantId=merchantNum;
			OpenDentBusiness.Crud.ProviderClinicCrud.Insert(providerClinic);
		}

		private static string GetODExceptionMessage(Action action) {
			try {
				action();
			}
			catch(CodeBase.ODException ex) {
				return ex.Message;
			}
			Assert.Fail("Expected an ODException.");
			return "";
		}

		[TestMethod]
		public void GetMerchantNumber_ClinicRowElsePracticeRow() {
			FillClinicsAndProviders();
			Assert.IsTrue(PrefC.HasClinicsEnabled);
			SetClinicMerchantNumber(5,MERCHANT_CLINIC5);
			Assert.AreEqual(MERCHANT_CLINIC5,CareCredit.GetMerchantNumber(5,3,isProvOverride:false),"The clinic's own row.");
			Assert.AreEqual(CareCreditTestData.MERCHANT,CareCredit.GetMerchantNumber(6,3,isProvOverride:false),"No row for the clinic: the practice's.");
			Assert.AreEqual(CareCreditTestData.MERCHANT,CareCredit.GetMerchantNumber(0,3,isProvOverride:false),"Clinic 0 is the practice.");
			SetClinicMerchantNumber(6,"");
			Assert.AreEqual(CareCreditTestData.MERCHANT,CareCredit.GetMerchantNumber(6,3,isProvOverride:false),"A blank clinic row: the practice's.");
		}

		[TestMethod]
		public void GetMerchantNumber_ClinicsOffUsesThePracticeRow() {
			FillClinicsAndProviders();
			SetClinicMerchantNumber(5,MERCHANT_CLINIC5);//Left from when clinics were on.
			using(MySqlConnection connection=new MySqlConnection("Server=127.0.0.1;Port=3306;User ID=root;Password=;Database="+DB_NAME+";Connection Timeout=3")) {
				connection.Open();
				Exec(connection,"UPDATE preference SET ValueString='1' WHERE PrefName='EasyNoClinics'");
			}
			Prefs.RefreshCache();
			Assert.IsFalse(PrefC.HasClinicsEnabled);
			Assert.AreEqual(CareCreditTestData.MERCHANT,CareCredit.GetMerchantNumber(5,3,isProvOverride:false));
			Assert.AreEqual(CareCreditTestData.MERCHANT,CareCredit.GetMerchantNumber(0,3,isProvOverride:false));
			SetClinicMerchantNumber(0,"");
			StringAssert.Contains(GetODExceptionMessage(() => CareCredit.GetMerchantNumber(5,3,isProvOverride:false)),
				"The practice does not have a CareCredit merchant number");
		}

		[TestMethod]
		public void GetMerchantNumber_ProviderClinicRowElseProviderDefault() {
			FillClinicsAndProviders();
			InsertProviderClinic(3,0,MERCHANT_PROV3);
			InsertProviderClinic(3,5,MERCHANT_PROV3_CLINIC5);
			Assert.AreEqual(MERCHANT_PROV3_CLINIC5,CareCredit.GetMerchantNumber(5,3,isProvOverride:true),"The provider's row for the clinic.");
			Assert.AreEqual(MERCHANT_PROV3,CareCredit.GetMerchantNumber(6,3,isProvOverride:true),"No row for the clinic: the provider's default.");
			Assert.AreEqual(MERCHANT_PROV3,CareCredit.GetMerchantNumber(0,3,isProvOverride:true));
			//A row for clinic 6 kept for the DEA number, with no merchant number, doesn't hide the provider's default.
			InsertProviderClinic(3,6,"",deaNum:"AB1234563");
			Assert.AreEqual(MERCHANT_PROV3,CareCredit.GetMerchantNumber(6,3,isProvOverride:true));
			Assert.AreEqual(MERCHANT_PROV3,CareCredit.GetMerchantNumberByProv(3,6));
			//A provider with no merchant number anywhere.
			string message=GetODExceptionMessage(() => CareCredit.GetMerchantNumber(5,4,isProvOverride:true));
			StringAssert.Contains(message,"'DrB'");
			StringAssert.Contains(message,"does not have a CareCredit merchant number");
		}

		[TestMethod]
		public void GetMerchantNumber_EmptyClosedOrInvalidThrows() {
			FillClinicsAndProviders();
			SetClinicMerchantNumber(0,"");
			string message=GetODExceptionMessage(() => CareCredit.GetMerchantNumber(6,3,isProvOverride:false));
			StringAssert.Contains(message,"'South' does not have a CareCredit merchant number");
			SetClinicMerchantNumber(5,"x"+MERCHANT_CLINIC5);
			message=GetODExceptionMessage(() => CareCredit.GetMerchantNumber(5,3,isProvOverride:false));
			StringAssert.Contains(message,"'North' has a closed CareCredit merchant number");
			SetClinicMerchantNumber(5,"53481200");
			message=GetODExceptionMessage(() => CareCredit.GetMerchantNumber(5,3,isProvOverride:false));
			StringAssert.Contains(message,"not a valid 16-digit number");
			InsertProviderClinic(4,0,"x"+MERCHANT_PROV3);
			message=GetODExceptionMessage(() => CareCredit.GetMerchantNumber(5,4,isProvOverride:true));
			StringAssert.Contains(message,"'DrB' has a closed CareCredit merchant number");
			InsertProviderClinic(4,5,"12345678901234567");
			message=GetODExceptionMessage(() => CareCredit.GetMerchantNumber(5,4,isProvOverride:true));
			StringAssert.Contains(message,"not a valid 16-digit number");
		}

		[TestMethod]
		public void GetMerchantNumsAll_IsDistinctAcrossClinicsAndProviders() {
			SetClinicMerchantNumber(5,MERCHANT_CLINIC5);
			SetClinicMerchantNumber(6,MERCHANT_CLINIC5);//The same account at two clinics.
			SetClinicMerchantNumber(7,"");
			InsertProviderClinic(3,0,MERCHANT_PROV3);
			InsertProviderClinic(3,5,MERCHANT_CLINIC5+" ");//The clinic's account again, typed with a trailing space.
			InsertProviderClinic(4,0,"x"+MERCHANT_PROV3);//Closed: kept, so its open form can be recognized as closed.
			InsertProviderClinic(4,5,"",deaNum:"AB1234563");
			List<string> listMerchantNums=CareCredit.GetMerchantNumsAll();
			CollectionAssert.AreEquivalent(new List<string> { CareCreditTestData.MERCHANT,MERCHANT_CLINIC5,MERCHANT_PROV3,"x"+MERCHANT_PROV3 },listMerchantNums);
			Assert.IsTrue(CareCredit.IsMerchantNumClosed(MERCHANT_PROV3,listMerchantNums));
			Assert.IsFalse(CareCredit.IsMerchantNumClosed(MERCHANT_CLINIC5,listMerchantNums));
		}

		private static CareCreditBatchCandidate CreateBatchCandidate(long patNum,long provNum,long clinicNum) {
			Patient patient=CareCreditTestData.CreateAdult(patNum);
			CareCreditBatchCandidate candidate=new CareCreditBatchCandidate();
			candidate.AptNum=patNum*10;
			candidate.AptDateTime=new DateTime(2026,10,20,9,0,0);
			candidate.ClinicNum=clinicNum;
			candidate.ProvNum=provNum;
			candidate.Patient=patient;
			candidate.Guarantor=patient;
			candidate.EstimatedFee=250;
			candidate.EstimatedPatientPortion=100;
			return candidate;
		}

		[TestMethod]
		public void Batch_ByProviderUsesTheDefaultPastABlankClinicRow() {
			FillClinicsAndProviders();
			InsertProviderClinic(3,0,MERCHANT_PROV3);
			//DrA's row for clinic 6 is kept for the DEA number and has no merchant number: the batch uses the default, as a purchase does.
			InsertProviderClinic(3,6,"",deaNum:"AB1234563");
			List<CareCreditBatchCandidate> listCandidates=new List<CareCreditBatchCandidate> {
				CreateBatchCandidate(1,3,6),
				CreateBatchCandidate(2,4,6),//DrB has no merchant number anywhere.
			};
			CareCreditJobs.SetMerchantNumbers(listCandidates,6,isByProv:true,merchantNumClinic:"");
			Assert.AreEqual(MERCHANT_PROV3,listCandidates[0].MerchantNumber);
			Assert.AreEqual(CareCredit.GetMerchantNumber(6,3,isProvOverride:true),listCandidates[0].MerchantNumber,"The batch and a purchase agree.");
			Assert.AreEqual("DrA",listCandidates[0].ProviderAbbr);
			Assert.AreEqual("",listCandidates[1].MerchantNumber);
			CareCreditBatchPlan plan=CareCreditBatchPlanner.Plan(listCandidates,EnumCareCreditBatch.NoBalance,null);
			Assert.AreEqual(1,plan.ListSubmissions.Count);
			Assert.AreEqual(1,plan.ListSubmissions[0].PatNum);
			Assert.AreEqual(MERCHANT_PROV3,plan.ListSubmissions[0].Request.MerchantNumber);
			Assert.AreEqual(1,plan.ListErrors.Count,"Only DrB's patient is a BatchError.");
			Assert.AreEqual(2,plan.ListErrors[0].PatNum);
			//The provider's own row for the clinic wins over the default.
			InsertProviderClinic(3,5,MERCHANT_PROV3_CLINIC5);
			listCandidates=new List<CareCreditBatchCandidate> { CreateBatchCandidate(1,3,5) };
			CareCreditJobs.SetMerchantNumbers(listCandidates,5,isByProv:true,merchantNumClinic:"");
			Assert.AreEqual(MERCHANT_PROV3_CLINIC5,listCandidates[0].MerchantNumber);
		}

		[TestMethod]
		public void Batch_ByClinicUsesTheClinicsNumberForEveryone() {
			FillClinicsAndProviders();
			InsertProviderClinic(3,0,MERCHANT_PROV3);//Ignored unless merchant numbers are by provider.
			List<CareCreditBatchCandidate> listCandidates=new List<CareCreditBatchCandidate> { CreateBatchCandidate(1,3,5),CreateBatchCandidate(2,4,5) };
			CareCreditJobs.SetMerchantNumbers(listCandidates,5,isByProv:false,merchantNumClinic:MERCHANT_CLINIC5);
			CollectionAssert.AreEqual(new[] { MERCHANT_CLINIC5,MERCHANT_CLINIC5 },listCandidates.Select(x => x.MerchantNumber).ToArray());
		}
		#endregion Merchant numbers
	}
}
