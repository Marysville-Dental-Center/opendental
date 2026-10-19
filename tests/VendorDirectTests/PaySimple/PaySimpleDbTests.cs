// Integration tests for PaySimple against a scratch MariaDB database on this computer: the ACH payments query, the runtime-inserted settings,
// reading the stored key, and a charge whose result is unknown. HTTP goes to a fake handler. Skipped (inconclusive) when no server answers at
// 127.0.0.1:3306 as root with a blank password, or when VENDORDIRECT_TEST_DB=skip. The credentials are made-up test values.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using Task=System.Threading.Tasks.Task;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using Newtonsoft.Json.Linq;
using OpenDentBusiness;
using OpenDentBusiness.VendorDirect;

namespace VendorDirectTests.PaySimple {
	[TestClass]
	public class PaySimpleDbTests {
		private const string DB_NAME="vendordirect_paysimple_tests";
		private const string USER="APIUserTest01";
		private const string KEY="fake-key-for-tests-only";
		private static string _skipReason;
		private FakeHttpHandler _fakeHttpHandler;

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
					Exec(connection,"CREATE TABLE payment (PayNum bigint(20) NOT NULL AUTO_INCREMENT,PayType bigint(20) NOT NULL,"
						+"PayDate date NOT NULL DEFAULT '0000-00-00',PayAmt double NOT NULL DEFAULT 0,CheckNum varchar(25) DEFAULT '',"
						+"BankBranch varchar(25) DEFAULT '',PayNote text NOT NULL,IsSplit tinyint(1) unsigned NOT NULL DEFAULT 0,PatNum bigint(20) NOT NULL,"
						+"ClinicNum bigint(20) NOT NULL,DateEntry date NOT NULL DEFAULT '0001-01-01',DepositNum bigint(20) NOT NULL,Receipt text NOT NULL,"
						+"IsRecurringCC tinyint(4) NOT NULL,SecUserNumEntry bigint(20) NOT NULL,"
						+"SecDateTEdit timestamp NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),PaymentSource tinyint(4) NOT NULL,"
						+"ProcessStatus tinyint(4) NOT NULL,RecurringChargeDate date NOT NULL DEFAULT '0001-01-01',ExternalId varchar(255) NOT NULL,"
						+"PaymentStatus tinyint(4) NOT NULL,IsCcCompleted tinyint(4) NOT NULL,MerchantFee double NOT NULL,PRIMARY KEY (PayNum)) "
						+"ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
					Exec(connection,"CREATE TABLE patientlink (PatientLinkNum bigint(20) NOT NULL AUTO_INCREMENT,PatNumFrom bigint(20) NOT NULL,"
						+"PatNumTo bigint(20) NOT NULL,LinkType tinyint(4) NOT NULL,DateTimeLink datetime NOT NULL DEFAULT '0001-01-01 00:00:00',"
						+"PRIMARY KEY (PatientLinkNum)) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
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
			//Each test starts from an empty PaySimple setup. Other test classes may have pointed the connection elsewhere.
			new DataConnection().SetDb("127.0.0.1",DB_NAME,"root","","root","",DatabaseType.MySql,true);
			Sql("DELETE FROM program");
			Sql("DELETE FROM programproperty");
			Sql("DELETE FROM payment");
			Sql("DELETE FROM patientlink");
			Prefs.RefreshCache();
			Programs.RefreshCache();
			ProgramProperties.RefreshCache();
			_fakeHttpHandler=new FakeHttpHandler();
			OpenDentBusiness.PaySimple.SetTestHttpHandler(_fakeHttpHandler,TimeSpan.FromMilliseconds(1),TimeSpan.FromMilliseconds(300));
		}

		[TestCleanup]
		public void TestCleanup() {
			OpenDentBusiness.PaySimple.SetTestHttpHandler(null);
		}

		///<summary>Runs a command against the scratch database.</summary>
		private static void Sql(string command) {
			using(MySqlConnection connection=new MySqlConnection("Server=127.0.0.1;Port=3306;Database="+DB_NAME+";User ID=root;Password=;Connection Timeout=3")) {
				connection.Open();
				Exec(connection,command);
			}
		}

		private static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.ExecuteNonQuery();
			}
		}

		///<summary>The PaySimple program row as Open Dental 24.3 has it, enabled or not.</summary>
		private static long InsertPaySimpleProgram(bool isEnabled) {
			Programs.InsertIfMissing(ProgramName.PaySimple,"PaySimple");
			Sql("UPDATE program SET Enabled="+(isEnabled ? "1" : "0")+" WHERE ProgName='PaySimple'");
			Programs.RefreshCache();
			return Programs.GetProgramNum(ProgramName.PaySimple);
		}

		private static void InsertProperty(long programNum,string desc,string value,long clinicNum=0) {
			Sql("INSERT INTO programproperty (ProgramNum,PropertyDesc,PropertyValue,ComputerName,ClinicNum,IsMasked,IsHighSecurity) VALUES ("
				+programNum+",'"+POut.String(desc)+"','"+POut.String(value)+"','',"+clinicNum+",0,0)");
		}

		private static long InsertPayment(PaymentStatus paymentStatus,string externalId,DateTime payDate) {
			Payment payment=new Payment();
			payment.PayDate=payDate;
			payment.PayAmt=50;
			payment.PayNote="";
			payment.Receipt="";
			payment.CheckNum="";
			payment.BankBranch="";
			payment.ExternalId=externalId;
			payment.PaymentStatus=paymentStatus;
			payment.DateEntry=payDate;
			return OpenDentBusiness.Crud.PaymentCrud.Insert(payment);
		}

		[TestMethod]
		public void GetPaySimpleAchToTrack_PostedAlways_SettledWithinTheWindow() {
			DateTime dateFrom=DateTime.Today.AddDays(-75);
			long payNumPostedOld=InsertPayment(PaymentStatus.PaySimpleAchPosted,"1001",DateTime.Today.AddDays(-200));
			long payNumSettledRecent=InsertPayment(PaymentStatus.PaySimpleAchSettled,"1002",DateTime.Today.AddDays(-10));
			InsertPayment(PaymentStatus.PaySimpleAchSettled,"1003",DateTime.Today.AddDays(-100));
			InsertPayment(PaymentStatus.PaySimpleAchFailed,"1004",DateTime.Today.AddDays(-5));
			InsertPayment(PaymentStatus.None,"",DateTime.Today);
			InsertPayment(PaymentStatus.PaySimpleAchPosted,"",DateTime.Today);//No PaySimple ID: nothing to ask about.
			List<long> listPayNums=Payments.GetPaySimpleAchToTrack(dateFrom).Select(x => x.PayNum).OrderBy(x => x).ToList();
			CollectionAssert.AreEqual(new List<long> { payNumPostedOld,payNumSettledRecent },listPayNums);
		}

		[TestMethod]
		public void PaySimpleAchStore_UpdateWritesOnlyStatusAndNote() {
			long payNum=InsertPayment(PaymentStatus.PaySimpleAchPosted,"1001",DateTime.Today);
			PaySimpleAchStore paySimpleAchStore=new PaySimpleAchStore();
			Payment payment=paySimpleAchStore.GetPayment(payNum);
			Payment paymentOld=payment.Clone();
			payment.PaymentStatus=PaymentStatus.PaySimpleAchSettled;
			payment.PayNote="PaySimple ACH settled 2026-10-19";
			paySimpleAchStore.UpdatePayment(payment,paymentOld);
			Payment paymentDb=Payments.GetPayment(payNum);
			Assert.AreEqual(PaymentStatus.PaySimpleAchSettled,paymentDb.PaymentStatus);
			Assert.AreEqual("PaySimple ACH settled 2026-10-19",paymentDb.PayNote);
			Assert.AreEqual(50,paymentDb.PayAmt);
		}

		[TestMethod]
		public void EnsureProperties_InsertsPracticeRows_AndMasksKeys() {
			long programNum=InsertPaySimpleProgram(false);
			InsertProperty(programNum,OpenDentBusiness.PaySimple.PropertyDescs.PaySimpleApiKey,KEY);
			ProgramProperties.RefreshCache();
			Assert.IsTrue(OpenDentBusiness.PaySimple.EnsureProperties());
			ProgramProperties.RefreshCache();
			Assert.AreEqual("Production",ProgramProperties.GetValOrDefault(programNum,OpenDentBusiness.PaySimple.PropertyDescs.PaySimpleEnvironment,0));
			Assert.AreEqual("1",ProgramProperties.GetValOrDefault(programNum,OpenDentBusiness.PaySimple.PropertyDescs.PaySimpleAchStatusCheck,0));
			ProgramProperty programPropertyKey=ProgramProperties.GetOneFromDb(programNum,OpenDentBusiness.PaySimple.PropertyDescs.PaySimpleApiKey,0);
			Assert.IsTrue(programPropertyKey.IsMasked);
			Assert.IsTrue(programPropertyKey.IsHighSecurity);
			Assert.AreEqual(KEY,programPropertyKey.PropertyValue,"Rows are only flagged here; the key is encrypted when Setup saves.");
			Assert.IsFalse(OpenDentBusiness.PaySimple.EnsureProperties(),"Nothing left to do.");
			Assert.IsTrue(OpenDentBusiness.PaySimple.IsAchStatusCheckOn());
			Assert.AreEqual(0,_fakeHttpHandler.Requests.Count);
		}

		[TestMethod]
		public void GetCredentials_DecryptsOsd1_AcceptsOpenDentalPlainText_BlanksUnreadable() {
			long programNum=InsertPaySimpleProgram(true);
			string stored=SecretsTestHelper.Protect(KEY);
			string corrupted=stored.Substring(0,stored.Length-4)+(stored.EndsWith("AAAA") ? "BBBB" : "AAAA");
			InsertProperty(programNum,OpenDentBusiness.PaySimple.PropertyDescs.PaySimpleApiUserName,USER);
			InsertProperty(programNum,OpenDentBusiness.PaySimple.PropertyDescs.PaySimpleApiKey,stored);
			InsertProperty(programNum,OpenDentBusiness.PaySimple.PropertyDescs.PaySimpleEnvironment,"Sandbox");
			InsertProperty(programNum,OpenDentBusiness.PaySimple.PropertyDescs.PaySimpleApiUserName,"APIUserClinic7",7);
			InsertProperty(programNum,OpenDentBusiness.PaySimple.PropertyDescs.PaySimpleApiKey,"plain-key-from-open-dental",7);
			InsertProperty(programNum,OpenDentBusiness.PaySimple.PropertyDescs.PaySimpleApiUserName,"APIUserClinic8",8);
			InsertProperty(programNum,OpenDentBusiness.PaySimple.PropertyDescs.PaySimpleApiKey,corrupted,8);
			ProgramProperties.RefreshCache();
			OpenDentBusiness.PaySimple.PaySimpleCredentials credentials0=OpenDentBusiness.PaySimple.GetCredentials(0);
			Assert.AreEqual(USER,credentials0.ApiUserName);
			Assert.AreEqual(KEY,credentials0.ApiKey);
			Assert.AreEqual("Sandbox",credentials0.Environment);
			OpenDentBusiness.PaySimple.PaySimpleCredentials credentials7=OpenDentBusiness.PaySimple.GetCredentials(7);
			Assert.AreEqual("plain-key-from-open-dental",credentials7.ApiKey);
			Assert.AreEqual("Sandbox",credentials7.Environment,"No clinic row: the practice's environment.");
			Assert.IsFalse(OpenDentBusiness.PaySimple.GetCredentials(8).IsComplete,"A damaged key is never sent.");
			Assert.IsFalse(OpenDentBusiness.PaySimple.HasCredentials(8));
			Assert.IsTrue(OpenDentBusiness.PaySimple.HasCredentials(0));
		}

		///<summary>A PaySimple program set up for a prepaid-card charge at the practice level, in the sandbox.</summary>
		private void SetUpForCharge() {
			long programNum=InsertPaySimpleProgram(true);
			InsertProperty(programNum,OpenDentBusiness.PaySimple.PropertyDescs.PaySimpleApiUserName,USER);
			InsertProperty(programNum,OpenDentBusiness.PaySimple.PropertyDescs.PaySimpleApiKey,SecretsTestHelper.Protect(KEY));
			InsertProperty(programNum,OpenDentBusiness.PaySimple.PropertyDescs.PaySimplePayTypeCC,"1");
			InsertProperty(programNum,OpenDentBusiness.PaySimple.PropertyDescs.PaySimpleEnvironment,"Sandbox");
			ProgramProperties.RefreshCache();
			_fakeHttpHandler.Enqueue(HttpStatusCode.Created,PaySimpleClientTests.Envelope("{\"Id\":8051100,\"FirstName\":\"UNKNOWN\",\"LastName\":\"Carrier\"}",
				httpStatusCode:201));
			_fakeHttpHandler.Enqueue(HttpStatusCode.Created,PaySimpleClientTests.Envelope("{\"Id\":7320843,\"CustomerId\":8051100,\"IsDefault\":false}",
				httpStatusCode:201));
		}

		private ApiCall MakePrepaidCharge() {
			return () => OpenDentBusiness.PaySimple.MakePayment(0,null,25.00m,"4111111111111111",new DateTime(DateTime.Today.Year+2,12,1),true,"80202","",
				0,"Carrier");
		}

		private delegate OpenDentBusiness.PaySimple.ApiResponse ApiCall();

		///<summary>The OrderId sent in the charge (the last POST to /v4/payment).</summary>
		private string GetSentOrderId() {
			RecordedRequest recordedRequest=_fakeHttpHandler.Requests.Last(x => x.Method==HttpMethod.Post && x.RequestUri.AbsolutePath=="/v4/payment");
			return (string)JObject.Parse(recordedRequest.Body)["OrderId"];
		}

		[TestMethod]
		public void Charge_SendsBasicAuthAndOrderId_ToTheSandbox() {
			SetUpForCharge();
			_fakeHttpHandler.Enqueue((request,ct) => Task.FromResult(FakeHttpHandler.MakeResponse(HttpStatusCode.Created,PaySimpleClientTests.Envelope(
				PaySimpleClientTests.PaymentJson(29124043,"Authorized",(string)JObject.Parse(request.Body)["OrderId"]),httpStatusCode:201))));
			OpenDentBusiness.PaySimple.ApiResponse apiResponse=MakePrepaidCharge()();
			Assert.AreEqual("29124043",apiResponse.RefNumber);
			List<RecordedRequest> listRequests=_fakeHttpHandler.Requests;
			Assert.AreEqual(3,listRequests.Count);
			Assert.IsTrue(listRequests.All(x => x.RequestUri.Host=="sandbox-api.paysimple.com"));
			Assert.IsTrue(listRequests.All(x => x.GetHeader("Authorization")=="basic "+USER+":"+KEY));
			StringAssert.StartsWith(GetSentOrderId(),"OPMS-");
		}

		[TestMethod]
		public void Charge_DroppedConnection_FoundByOrderId_IsASuccessNotASecondCharge() {
			SetUpForCharge();
			_fakeHttpHandler.EnqueueNetworkFailure();
			_fakeHttpHandler.Enqueue((request,ct) => Task.FromResult(FakeHttpHandler.MakeResponse(HttpStatusCode.OK,PaySimpleClientTests.Envelope(
				"["+PaySimpleClientTests.PaymentJson(29124043,"Authorized",GetSentOrderId())+"]","{\"TotalItems\":1,\"Page\":1,\"ItemsPerPage\":200}"))));
			OpenDentBusiness.PaySimple.ApiResponse apiResponse=MakePrepaidCharge()();
			Assert.AreEqual("29124043",apiResponse.RefNumber);
			List<RecordedRequest> listRequests=_fakeHttpHandler.Requests;
			Assert.AreEqual(1,listRequests.Count(x => x.Method==HttpMethod.Post && x.RequestUri.AbsolutePath=="/v4/payment"),"Never posted twice.");
			Assert.AreEqual("/v4/customer/8051100/payments",listRequests.Last().RequestUri.AbsolutePath);
		}

		[TestMethod]
		public void Charge_Timeout_NotFound_IsResultUnknown() {
			SetUpForCharge();
			_fakeHttpHandler.Enqueue(async (request,ct) => {
				await Task.Delay(TimeSpan.FromSeconds(10),ct);
				return FakeHttpHandler.MakeResponse(HttpStatusCode.Created,"");
			});
			_fakeHttpHandler.Enqueue(HttpStatusCode.OK,PaySimpleClientTests.Envelope("[]","{\"TotalItems\":0,\"Page\":1,\"ItemsPerPage\":200}"));
			PaySimpleResultUnknownException ex=AssertEx.Throws<PaySimpleResultUnknownException>(() => MakePrepaidCharge()());
			Assert.AreEqual(GetSentOrderId(),ex.OrderId);
			StringAssert.Contains(ex.Message,ex.OrderId);
			Assert.AreEqual(1,_fakeHttpHandler.Requests.Count(x => x.Method==HttpMethod.Post && x.RequestUri.AbsolutePath=="/v4/payment"),
				"Never posted twice.");
		}

		[TestMethod]
		public void Charge_Declined_FoundByOrderId_StillThrowsTheDecline() {
			SetUpForCharge();
			_fakeHttpHandler.EnqueueNetworkFailure();
			_fakeHttpHandler.Enqueue((request,ct) => Task.FromResult(FakeHttpHandler.MakeResponse(HttpStatusCode.OK,PaySimpleClientTests.Envelope(
				"["+PaySimpleClientTests.PaymentJson(29124043,"Failed",GetSentOrderId(),providerAuthCode:"DECLINE")+"]",
				"{\"TotalItems\":1,\"Page\":1,\"ItemsPerPage\":200}"))));
			Exception ex=AssertEx.Throws<Exception>(() => MakePrepaidCharge()());
			StringAssert.Contains(ex.Message,"not approved");
		}

		[TestMethod]
		public void Disabled_NoCallIsMade() {
			long programNum=InsertPaySimpleProgram(false);
			InsertProperty(programNum,OpenDentBusiness.PaySimple.PropertyDescs.PaySimpleApiUserName,USER);
			InsertProperty(programNum,OpenDentBusiness.PaySimple.PropertyDescs.PaySimpleApiKey,SecretsTestHelper.Protect(KEY));
			InsertProperty(programNum,OpenDentBusiness.PaySimple.PropertyDescs.PaySimplePayTypeCC,"1");
			ProgramProperties.RefreshCache();
			AssertEx.Throws<CodeBase.ODException>(() => MakePrepaidCharge()());
			Assert.IsTrue(PaySimpleAchStatusSync.Run(isManual:true).WasSkipped);
			Assert.AreEqual(0,_fakeHttpHandler.Requests.Count);
		}
	}
}
