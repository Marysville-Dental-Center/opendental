// Integration tests for the EdgeExpress library side against a scratch MariaDB database on this computer: the result query uses the session's
// clinic, re-polls, local reconciliation (claim, late-approval alert, no automatic payment), the pending query, and the per-clinic settings.
// The card processor is faked through XWebs.Mock; nothing leaves this computer. Skipped (inconclusive) when no server answers at 127.0.0.1:3306
// as root with a blank password, or when VENDORDIRECT_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenDentBusiness;
using OpenDentBusiness.WebTypes.Shared.XWeb;

namespace VendorDirectTests.EdgeExpressTests {
	[TestClass]
	public class EdgeExpressDbTests {
		private static readonly string DB_NAME=ScratchDb.Name("vendordirect_edgeexpress_tests");
		private const string CONNECTION="Server=127.0.0.1;Port=3306;User ID=root;Password=;Connection Timeout=3";
		private const long PAT_NUM=501;
		private const string XWEB_ID_CLINIC_1="800000000001";
		private const string XWEB_ID_CLINIC_2="800000000002";
		private const string AUTH_KEY_CLINIC_1="PlainTextKeyFromOpenDental000001";
		private const string AUTH_KEY_CLINIC_2="EncryptedKeyForClinicTwo00000002";
		private static string _skipReason;
		private static long _programNumEdgeExpress;
		///<summary>Every request the fake card processor received, in order.</summary>
		private List<string> _listRequests;
		///<summary>The logged-in user before the test; saving a card on file writes a security log entry for the current user.</summary>
		private Userod _userodBefore;

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
					//The same definitions as Open Dental 24.3's tables (patient: only the columns Patients.GetLim reads).
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
					Exec(connection,"CREATE TABLE patient (PatNum bigint(20) NOT NULL AUTO_INCREMENT,LName varchar(100) NOT NULL DEFAULT '',"
						+"FName varchar(100) NOT NULL DEFAULT '',MiddleI varchar(100) NOT NULL DEFAULT '',Preferred varchar(100) NOT NULL DEFAULT '',"
						+"CreditType char(1) NOT NULL DEFAULT '',Guarantor bigint(20) NOT NULL DEFAULT 0,HasIns varchar(255) NOT NULL DEFAULT '',"
						+"SSN varchar(100) NOT NULL DEFAULT '',Birthdate date NOT NULL DEFAULT '0001-01-01',ClinicNum bigint(20) NOT NULL DEFAULT 0,"
						+"PRIMARY KEY (PatNum)) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
					Exec(connection,"INSERT INTO patient (PatNum,LName,FName,Guarantor,ClinicNum) VALUES ("+PAT_NUM+",'Rivera','Dana',"+PAT_NUM+",1)");
					Exec(connection,"CREATE TABLE xwebresponse (XWebResponseNum bigint(20) NOT NULL AUTO_INCREMENT,PatNum bigint(20) NOT NULL,"
						+"ProvNum bigint(20) NOT NULL,ClinicNum bigint(20) NOT NULL,PaymentNum bigint(20) NOT NULL,"
						+"DateTEntry datetime NOT NULL DEFAULT '0001-01-01 00:00:00',DateTUpdate datetime NOT NULL DEFAULT '0001-01-01 00:00:00',"
						+"TransactionStatus tinyint(4) NOT NULL,ResponseCode int(11) NOT NULL,XWebResponseCode varchar(255) NOT NULL,"
						+"ResponseDescription varchar(255) NOT NULL,OTK varchar(255) NOT NULL,HpfUrl text NOT NULL,"
						+"HpfExpiration datetime NOT NULL DEFAULT '0001-01-01 00:00:00',TransactionID varchar(255) NOT NULL,TransactionType varchar(255) NOT NULL,"
						+"Alias varchar(255) NOT NULL,CardType varchar(255) NOT NULL,CardBrand varchar(255) NOT NULL,CardBrandShort varchar(255) NOT NULL,"
						+"MaskedAcctNum varchar(255) NOT NULL,Amount double NOT NULL,ApprovalCode varchar(255) NOT NULL,CardCodeResponse varchar(255) NOT NULL,"
						+"ReceiptID int(11) NOT NULL,ExpDate varchar(255) NOT NULL,EntryMethod varchar(255) NOT NULL,ProcessorResponse varchar(255) NOT NULL,"
						+"BatchNum int(11) NOT NULL,BatchAmount double NOT NULL,AccountExpirationDate date NOT NULL DEFAULT '0001-01-01',DebugError text NOT NULL,"
						+"PayNote text NOT NULL,CCSource tinyint(4) NOT NULL,OrderId varchar(255) NOT NULL,EmailResponse varchar(255) NOT NULL,"
						+"LogGuid varchar(36) DEFAULT NULL,PRIMARY KEY (XWebResponseNum),KEY PatNum (PatNum),KEY ClinicNum (ClinicNum),"
						+"KEY PaymentNum (PaymentNum),KEY DateTUpdate (DateTUpdate)) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
					Exec(connection,"CREATE TABLE alertitem (AlertItemNum bigint(20) NOT NULL AUTO_INCREMENT,ClinicNum bigint(20) NOT NULL,"
						+"Description varchar(2000) NOT NULL,Type tinyint(4) NOT NULL,Severity tinyint(4) NOT NULL,Actions tinyint(4) NOT NULL,"
						+"FormToOpen tinyint(4) NOT NULL,FKey bigint(20) NOT NULL,ItemValue varchar(4000) NOT NULL,UserNum bigint(20) NOT NULL,"
						+"SecDateTEntry datetime NOT NULL DEFAULT '0001-01-01 00:00:00',PRIMARY KEY (AlertItemNum)) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
					//Cards on file, and the security log that saving one writes.
					Exec(connection,"CREATE TABLE creditcard (CreditCardNum bigint(20) NOT NULL AUTO_INCREMENT,PatNum bigint(20) NOT NULL,"
						+"Address varchar(255) DEFAULT NULL,Zip varchar(255) DEFAULT NULL,XChargeToken varchar(255) DEFAULT NULL,"
						+"CCNumberMasked varchar(255) DEFAULT NULL,CCExpiration date NOT NULL DEFAULT '0001-01-01',ItemOrder int(11) NOT NULL,"
						+"ChargeAmt double NOT NULL,DateStart date NOT NULL DEFAULT '0001-01-01',DateStop date NOT NULL DEFAULT '0001-01-01',"
						+"Note varchar(255) NOT NULL,PayPlanNum bigint(20) NOT NULL,PayConnectToken varchar(255) NOT NULL,"
						+"PayConnectTokenExp date NOT NULL DEFAULT '0001-01-01',Procedures text NOT NULL,CCSource tinyint(4) NOT NULL,"
						+"ClinicNum bigint(20) NOT NULL,ExcludeProcSync tinyint(4) NOT NULL,PaySimpleToken varchar(255) NOT NULL,"
						+"ChargeFrequency varchar(150) NOT NULL,CanChargeWhenNoBal tinyint(4) NOT NULL,PaymentType bigint(20) NOT NULL,"
						+"IsRecurringActive tinyint(4) NOT NULL,Nickname varchar(255) NOT NULL,CardHolderName varchar(255) NOT NULL,"//CardHolderName: 25.1.
						+"PRIMARY KEY (CreditCardNum),KEY PatNum (PatNum)) "
						+"ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
					Exec(connection,"CREATE TABLE securitylog (SecurityLogNum bigint(20) NOT NULL AUTO_INCREMENT,PermType smallint(6) NOT NULL,"
						+"UserNum bigint(20) NOT NULL,LogDateTime datetime NOT NULL DEFAULT '0001-01-01 00:00:00',LogText text DEFAULT NULL,"
						+"PatNum bigint(20) NOT NULL,CompName varchar(255) NOT NULL,FKey bigint(20) NOT NULL,LogSource tinyint(4) NOT NULL,"
						+"DefNum bigint(20) NOT NULL,DefNumError bigint(20) NOT NULL,DateTPrevious datetime NOT NULL DEFAULT '0001-01-01 00:00:00',"
						+"PRIMARY KEY (SecurityLogNum)) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
					Exec(connection,"CREATE TABLE securityloghash (SecurityLogHashNum bigint(20) NOT NULL AUTO_INCREMENT,SecurityLogNum bigint(20) NOT NULL,"
						+"LogHash varchar(255) NOT NULL,PRIMARY KEY (SecurityLogHashNum)) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
				}
				UseScratchDb();
				Prefs.RefreshCache();
				Programs.InsertIfMissing(ProgramName.Xcharge,"X-Charge");
				Programs.InsertIfMissing(ProgramName.EdgeExpress,"EdgeExpress");
				Programs.RefreshCache();
				Program programEdgeExpress=Programs.GetCur(ProgramName.EdgeExpress);
				programEdgeExpress.Enabled=true;
				Programs.Update(programEdgeExpress);
				Programs.RefreshCache();
				_programNumEdgeExpress=programEdgeExpress.ProgramNum;
				//Clinic 1 still has Open Dental's plain-text key; clinic 2's was saved by the new setup window (osd1$).
				AddCreds(1,XWEB_ID_CLINIC_1,AUTH_KEY_CLINIC_1,"80000001");
				AddCreds(2,XWEB_ID_CLINIC_2,SecretsTestHelper.Protect(AUTH_KEY_CLINIC_2),"80000002");
				ProgramProperties.RefreshCache();
			}
			catch(Exception ex) {
				_skipReason="No scratch MariaDB database: "+ex.GetBaseException().Message;
			}
		}

		private static void UseScratchDb() {
			new DataConnection().SetDb("127.0.0.1",DB_NAME,"root","","root","",DatabaseType.MySql,true);
		}

		private static void AddCreds(long clinicNum,string xWebID,string authKeyStored,string terminalID) {
			ProgramProperties.InsertIfMissing(_programNumEdgeExpress,ProgramProperties.PropertyDescs.EdgeExpress.XWebID,clinicNum,xWebID,false,false);
			ProgramProperties.InsertIfMissing(_programNumEdgeExpress,ProgramProperties.PropertyDescs.EdgeExpress.AuthKey,clinicNum,authKeyStored,true,true);
			ProgramProperties.InsertIfMissing(_programNumEdgeExpress,ProgramProperties.PropertyDescs.EdgeExpress.TerminalID,clinicNum,terminalID,false,false);
			ProgramProperties.InsertIfMissing(_programNumEdgeExpress,ProgramProperties.PropertyDescs.EdgeExpress.PaymentType,clinicNum,"1",false,false);
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			XWebs.Mock=null;
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
			//Other test classes point the shared connection at their own scratch databases.
			UseScratchDb();
			Prefs.RefreshCache();
			Programs.RefreshCache();
			ProgramProperties.RefreshCache();
			using(MySqlConnection connection=new MySqlConnection(CONNECTION)) {
				connection.Open();
				Exec(connection,"DELETE FROM "+DB_NAME+".xwebresponse");
				Exec(connection,"DELETE FROM "+DB_NAME+".alertitem");
				Exec(connection,"DELETE FROM "+DB_NAME+".creditcard");
			}
			_listRequests=new List<string>();
			_userodBefore=Security.CurUser;
		}

		[TestCleanup]
		public void TestCleanup() {
			XWebs.Mock=null;
			Security.CurUser=_userodBefore;
		}

		///<summary>A clerk is logged in, as on a workstation (the payment window, or the vendor job host running reconciliation).</summary>
		private void LogInClerk() {
			_userodBefore=Security.CurUser;
			Userod userod=new Userod();
			userod.UserNum=7;
			userod.UserName="clerk";
			Security.CurUser=userod;
		}

		private static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.ExecuteNonQuery();
			}
		}

		///<summary>Fakes the card processor: each request gets the next answer; the last one repeats.</summary>
		private void FakeGateway(params string[] arrayAnswers) {
			XWebs.Mock=new XWebs.MockXWebs();
			XWebs.Mock.ResponseForRequest=(input,url) => {
				_listRequests.Add(input);
				return arrayAnswers[Math.Min(_listRequests.Count-1,arrayAnswers.Length-1)];
			};
		}

		private static string Answer(string responseCode,string extra="") {
			return "<RESULT><RESPONSECODE>"+responseCode+"</RESPONSECODE><RESPONSEDESCRIPTION>Desc "+responseCode+"</RESPONSEDESCRIPTION>"+extra+"</RESULT>";
		}

		private static string Approval(decimal amount) {
			return Answer("000","<TRANSACTIONID>T1001</TRANSACTIONID><APPROVALCODE>A12345</APPROVALCODE><APPROVEDAMOUNT>"+amount+"</APPROVEDAMOUNT>"
				+"<MASKEDCARDNUMBER>************1111</MASKEDCARDNUMBER><EXPMONTH>12</EXPMONTH><EXPYEAR>30</EXPYEAR><TRANSACTIONTYPE>CREDITSALE</TRANSACTIONTYPE>");
		}

		private static string ApprovalWithAlias(decimal amount) {
			return Approval(amount).Replace("</RESULT>","<ALIAS>ALIAS-4242</ALIAS></RESULT>");
		}

		private static string GetElement(string xml,string name) {
			Match match=Regex.Match(xml,"<"+name+">([^<]*)</"+name+">");
			return match.Success ? match.Groups[1].Value : null;
		}

		///<summary>A pay-page session row as GetUrlForPaymentPage leaves it.</summary>
		private static XWebResponse InsertPending(long clinicNum,DateTime hpfExpiration,EdgeExpressTransType transType=EdgeExpressTransType.CreditSale,
			long paymentNum=0,XWebTransactionStatus transactionStatus=XWebTransactionStatus.EdgeExpressPending)
		{
			XWebResponse xWebResponse=new XWebResponse();
			xWebResponse.PatNum=PAT_NUM;
			xWebResponse.ClinicNum=clinicNum;
			xWebResponse.PaymentNum=paymentNum;
			xWebResponse.OrderId=XWebResponses.CreateOrderId();
			xWebResponse.TransactionType=transType.ToString();
			xWebResponse.TransactionStatus=transactionStatus;
			xWebResponse.OTK="session-token";
			xWebResponse.HpfUrl="https://ee.test.paygateway.com/HostPayService/v1/hostpay/paypage/session-token";
			xWebResponse.HpfExpiration=hpfExpiration;
			xWebResponse.DateTUpdate=DateTime.Now.AddMinutes(-10);
			xWebResponse.Amount=25;
			xWebResponse.CCSource=CreditCardSource.EdgeExpressCNP;
			xWebResponse.PayNote="";
			xWebResponse.DebugError="";
			XWebResponses.Insert(xWebResponse);
			return XWebResponses.GetOne(xWebResponse.XWebResponseNum);
		}

		private static int CountAlerts() {
			return PIn.Int(DataCore.GetScalar("SELECT COUNT(*) FROM alertitem"));
		}

		private static int CountCreditCards() {
			return PIn.Int(DataCore.GetScalar("SELECT COUNT(*) FROM creditcard"));
		}

		///<summary>Fakes the card processor with an approval that saves a card. actionInFlight runs once, while the first request waits for its
		///answer: the other path (the window or reconciliation) asks and acts in that gap, as another workstation would.</summary>
		private void FakeGatewayWithSecondPathInFlight(Action actionInFlight) {
			XWebs.Mock=new XWebs.MockXWebs();
			XWebs.Mock.ResponseForRequest=(input,url) => {
				_listRequests.Add(input);
				if(_listRequests.Count==1) {
					actionInFlight();
				}
				return ApprovalWithAlias(25);
			};
		}

		[TestMethod]
		public void ProcessTransaction_QueriesWithTheSessionsClinic_NotThePaymentsClinic() {
			//The session was created under clinic 2 (say, the current clinic in Manage Cards), but the payment belongs to clinic 1.
			XWebResponse xWebResponse=InsertPending(2,DateTime.Now.AddMinutes(5));
			Payment payment=new Payment();
			payment.PayNum=9001;
			payment.ClinicNum=1;
			FakeGateway(Approval(25));
			XWebResponse xWebResponseProcessed=EdgeExpress.CNP.ProcessTransaction(xWebResponse,payment);
			Assert.AreEqual(1,_listRequests.Count);
			Assert.AreEqual("QUERYPAYMENT",GetElement(_listRequests[0],"TRANSACTIONTYPE"));
			Assert.AreEqual(xWebResponse.OrderId,GetElement(_listRequests[0],"ORDERID"));
			Assert.AreEqual(XWEB_ID_CLINIC_2,GetElement(_listRequests[0],"XWEBID"),"The query must use the merchant account the session was made with.");
			Assert.AreEqual(AUTH_KEY_CLINIC_2,GetElement(_listRequests[0],"XWEBAUTHKEY"),"The osd1$ Auth Key is decrypted before it is sent.");
			Assert.AreEqual(XWebTransactionStatus.EdgeExpressCompletePaymentApproved,xWebResponseProcessed.TransactionStatus);
			Assert.AreEqual(9001,xWebResponseProcessed.PaymentNum);
			Assert.AreEqual(2,XWebResponses.GetOne(xWebResponse.XWebResponseNum).ClinicNum);
		}

		[TestMethod]
		public void ProcessTransaction_PlainTextKeyFromOpenDentalStillWorks() {
			XWebResponse xWebResponse=InsertPending(1,DateTime.Now.AddMinutes(5));
			FakeGateway(Answer("814"));
			XWebResponse xWebResponseProcessed=EdgeExpress.CNP.ProcessTransaction(xWebResponse,null,doCreatePayment:false);
			Assert.AreEqual(XWEB_ID_CLINIC_1,GetElement(_listRequests[0],"XWEBID"));
			Assert.AreEqual(AUTH_KEY_CLINIC_1,GetElement(_listRequests[0],"XWEBAUTHKEY"));
			Assert.AreEqual(XWebTransactionStatus.EdgeExpressPending,xWebResponseProcessed.TransactionStatus,"814 before the card is submitted means pending.");
		}

		[TestMethod]
		public void ProcessTransaction_TimedOutAndCancelledAreFinal() {
			foreach(string code in new[] { "006","103" }) {
				XWebResponse xWebResponse=InsertPending(2,DateTime.Now.AddMinutes(5));
				FakeGateway(Answer(code));
				XWebResponse xWebResponseProcessed=EdgeExpress.CNP.ProcessTransaction(xWebResponse,null,doCreatePayment:false);
				Assert.AreEqual(XWebTransactionStatus.EdgeExpressExpired,xWebResponseProcessed.TransactionStatus,code);
				Assert.AreEqual(XWebTransactionStatus.EdgeExpressExpired,XWebResponses.GetOne(xWebResponse.XWebResponseNum).TransactionStatus,code);
			}
		}

		[TestMethod]
		public void ProcessTransaction_TemporaryOutageKeepsTheSessionPending() {
			XWebResponse xWebResponse=InsertPending(2,DateTime.Now.AddMinutes(5));
			FakeGateway(Answer("820"));
			XWebResponse xWebResponseProcessed=EdgeExpress.CNP.ProcessTransaction(xWebResponse,null,doCreatePayment:false);
			Assert.AreEqual(XWebTransactionStatus.EdgeExpressPending,xWebResponseProcessed.TransactionStatus,"The card may have been charged; keep asking.");
			Assert.AreEqual(XWebTransactionStatus.EdgeExpressPending,XWebResponses.GetOne(xWebResponse.XWebResponseNum).TransactionStatus);
			Assert.AreEqual(XWebResponseCodes.TemporaryServiceOutage,xWebResponseProcessed.XWebResponseCode);
			StringAssert.Contains(EdgeExpress.CNP.GetUserMessage(xWebResponseProcessed),"temporarily unavailable");
		}

		[TestMethod]
		public void ProcessTransactionWithRepoll_CatchesACardSubmittedJustBeforeClose() {
			XWebResponse xWebResponse=InsertPending(2,DateTime.Now.AddMinutes(5));
			Payment payment=new Payment();
			payment.PayNum=9002;
			payment.ClinicNum=2;
			FakeGateway(Answer("814"),Answer("102"),Approval(25));
			int countWaits=0;
			XWebResponse xWebResponseProcessed=EdgeExpress.CNP.ProcessTransactionWithRepoll(xWebResponse,payment,true,x => countWaits++);
			Assert.AreEqual(XWebTransactionStatus.EdgeExpressCompletePaymentApproved,xWebResponseProcessed.TransactionStatus);
			Assert.AreEqual(3,_listRequests.Count);
			Assert.AreEqual(2,countWaits);
			//Still pending after every try: gives up after RepollCount more tries and leaves it for reconciliation.
			xWebResponse=InsertPending(2,DateTime.Now.AddMinutes(5));
			_listRequests.Clear();
			FakeGateway(Answer("814"));
			xWebResponseProcessed=EdgeExpress.CNP.ProcessTransactionWithRepoll(xWebResponse,payment,true,x => { });
			Assert.AreEqual(XWebTransactionStatus.EdgeExpressPending,xWebResponseProcessed.TransactionStatus);
			Assert.AreEqual(1+EdgeExpress.CNP.RepollCount,_listRequests.Count);
		}

		[TestMethod]
		public void GetPendingEdgeExpress_OnlyOpenOrRecentPayPageSessions() {
			DateTime now=DateTime.Now;
			XWebResponse xWebResponseSale=InsertPending(2,now.AddMinutes(-2),paymentNum:77);//Desktop sale: has a PaymentNum.
			XWebResponse xWebResponseAuth=InsertPending(1,now.AddMinutes(5),EdgeExpressTransType.CreditAuth);
			InsertPending(2,now.AddMinutes(-31));//Too old.
			InsertPending(2,now.AddMinutes(-2),EdgeExpressTransType.AliasUpdate);//A failed alias update is "pending" too, but not a pay page.
			InsertPending(2,now.AddMinutes(-2),transactionStatus:XWebTransactionStatus.EdgeExpressCompletePaymentApproved);
			InsertPending(2,now.AddMinutes(-2),transactionStatus:XWebTransactionStatus.HpfPending);//X-Web HPF.
			List<XWebResponse> listPending=XWebResponses.GetPendingEdgeExpress(now.Subtract(EdgeExpress.CNP.ReconcileWindow));
			CollectionAssert.AreEquivalent(new[] { xWebResponseSale.XWebResponseNum,xWebResponseAuth.XWebResponseNum },
				listPending.Select(x => x.XWebResponseNum).ToArray());
			Assert.AreEqual(2,XWebResponses.CountPendingEdgeExpress(now.Subtract(EdgeExpress.CNP.ReconcileWindow)));
			Assert.IsTrue(new EdgeExpressReconcileJob().ShouldRun());
		}

		[TestMethod]
		public void TryClaim_OnlyOneClaimWins() {
			XWebResponse xWebResponse=InsertPending(2,DateTime.Now.AddMinutes(-1));
			Assert.IsTrue(XWebResponses.TryClaim(xWebResponse.XWebResponseNum,xWebResponse.DateTUpdate));
			Assert.IsFalse(XWebResponses.TryClaim(xWebResponse.XWebResponseNum,xWebResponse.DateTUpdate),"The second workstation sees a changed row.");
			XWebResponse xWebResponseClaimed=XWebResponses.GetOne(xWebResponse.XWebResponseNum);
			Assert.IsTrue(xWebResponseClaimed.DateTUpdate>xWebResponse.DateTUpdate);
			//Claims taken within the same second still change the row, so they stay exclusive.
			Assert.IsTrue(XWebResponses.TryClaim(xWebResponseClaimed.XWebResponseNum,xWebResponseClaimed.DateTUpdate));
			Assert.IsFalse(XWebResponses.TryClaim(xWebResponseClaimed.XWebResponseNum,xWebResponseClaimed.DateTUpdate));
			XWebResponse xWebResponseDone=InsertPending(2,DateTime.Now.AddMinutes(-1),transactionStatus:XWebTransactionStatus.EdgeExpressExpired);
			Assert.IsFalse(XWebResponses.TryClaim(xWebResponseDone.XWebResponseNum,xWebResponseDone.DateTUpdate),"Only pending rows are claimed.");
		}

		[TestMethod]
		public void ReconcilePending_LateApprovalRaisesAnAlertAndPostsNothing() {
			//The payment window had payment 77 open; the user closed it before the return and the re-polls saw nothing yet.
			XWebResponse xWebResponse=InsertPending(2,DateTime.Now.Subtract(EdgeExpress.CNP.ReconcileDelay).AddMinutes(-1),paymentNum:77);
			FakeGateway(Approval(25));
			EdgeExpressReconcileResult reconcileResult=EdgeExpress.CNP.ReconcilePending();
			Assert.AreEqual(1,reconcileResult.CountChecked);
			Assert.AreEqual(1,reconcileResult.CountApproved);
			Assert.AreEqual(0,reconcileResult.CountFailed,"No payment row may be written (there is no payment table here).");
			Assert.AreEqual(XWEB_ID_CLINIC_2,GetElement(_listRequests[0],"XWEBID"));
			XWebResponse xWebResponseDb=XWebResponses.GetOne(xWebResponse.XWebResponseNum);
			Assert.AreEqual(XWebTransactionStatus.EdgeExpressCompletePaymentApproved,xWebResponseDb.TransactionStatus);
			Assert.AreEqual(77,xWebResponseDb.PaymentNum,"No payment is made, but the link to the window's payment is kept for a later void or return.");
			Assert.AreEqual(1,CountAlerts());
			string description=DataCore.GetScalar("SELECT Description FROM alertitem");
			StringAssert.Contains(description,"approved after the payment window closed");
			StringAssert.Contains(description,"Dana Rivera");
			StringAssert.Contains(DataCore.GetScalar("SELECT ItemValue FROM alertitem"),"77");
			Assert.AreEqual("2",DataCore.GetScalar("SELECT ClinicNum FROM alertitem"));
			//A second run finds nothing left to do.
			reconcileResult=EdgeExpress.CNP.ReconcilePending();
			Assert.AreEqual(0,reconcileResult.CountChecked);
			Assert.AreEqual(1,CountAlerts());
		}

		[TestMethod]
		public void ReconcilePending_LeavesOpenSessionsAndExpiresOldOnes() {
			XWebResponse xWebResponseOpen=InsertPending(2,DateTime.Now.AddMinutes(5));
			XWebResponse xWebResponseOld=InsertPending(2,DateTime.Now.Subtract(EdgeExpress.CNP.PendingGrace).AddMinutes(-1));
			FakeGateway(Answer("814"));
			EdgeExpressReconcileResult reconcileResult=EdgeExpress.CNP.ReconcilePending();
			Assert.AreEqual(1,reconcileResult.CountStillOpen,"A pay-page window may still have it.");
			Assert.AreEqual(1,_listRequests.Count,"The open session is not asked about.");
			Assert.AreEqual(1,reconcileResult.CountExpired);
			Assert.AreEqual(XWebTransactionStatus.EdgeExpressPending,XWebResponses.GetOne(xWebResponseOpen.XWebResponseNum).TransactionStatus);
			Assert.AreEqual(XWebTransactionStatus.EdgeExpressExpired,XWebResponses.GetOne(xWebResponseOld.XWebResponseNum).TransactionStatus);
			Assert.AreEqual(0,CountAlerts());
		}

		[TestMethod]
		public void ReconcilePending_WaitsUntilThePayPageWindowIsDonePolling() {
			//The window closes by HpfExpiration and then re-polls for about RepollCount*RepollDelay.
			DateTime hpfExpiration=DateTime.Now.AddMinutes(-10);
			XWebResponse xWebResponse=InsertPending(2,hpfExpiration,paymentNum:78);
			FakeGateway(Approval(25));
			EdgeExpressReconcileResult reconcileResult=EdgeExpress.CNP.ReconcilePending(hpfExpiration.AddSeconds(5));
			Assert.AreEqual(1,reconcileResult.CountStillOpen,"The window may still be asking for the result.");
			Assert.AreEqual(0,reconcileResult.CountChecked);
			Assert.AreEqual(0,_listRequests.Count);
			Assert.AreEqual(XWebTransactionStatus.EdgeExpressPending,XWebResponses.GetOne(xWebResponse.XWebResponseNum).TransactionStatus);
			reconcileResult=EdgeExpress.CNP.ReconcilePending(hpfExpiration.Add(EdgeExpress.CNP.ReconcileDelay).AddSeconds(-1));
			Assert.AreEqual(1,reconcileResult.CountStillOpen);
			Assert.AreEqual(0,_listRequests.Count);
			Assert.AreEqual(0,CountAlerts());
			reconcileResult=EdgeExpress.CNP.ReconcilePending(hpfExpiration.Add(EdgeExpress.CNP.ReconcileDelay).AddSeconds(1));
			Assert.AreEqual(1,reconcileResult.CountChecked);
			Assert.AreEqual(1,reconcileResult.CountApproved);
			Assert.AreEqual(78,XWebResponses.GetOne(xWebResponse.XWebResponseNum).PaymentNum);
		}

		[TestMethod]
		public void ProcessTransactionWithRepoll_StopsOnceReconciliationSettledTheRow() {
			XWebResponse xWebResponse=InsertPending(2,DateTime.Now.AddMinutes(-1),paymentNum:79);
			Payment payment=new Payment();
			payment.PayNum=79;
			payment.ClinicNum=2;
			FakeGateway(Answer("814"),Approval(25));
			//Another workstation's reconciliation settles the row while this window waits between re-polls.
			Action<TimeSpan> actionWait=x => {
				XWebResponse xWebResponseDb=XWebResponses.GetOne(xWebResponse.XWebResponseNum);
				xWebResponseDb.TransactionStatus=XWebTransactionStatus.EdgeExpressCompletePaymentApproved;
				XWebResponses.Update(xWebResponseDb);
			};
			XWebResponse xWebResponseProcessed=EdgeExpress.CNP.ProcessTransactionWithRepoll(xWebResponse,payment,true,actionWait);
			Assert.AreEqual(1,_listRequests.Count,"The settled row is not asked about, or acted on, again.");
			Assert.AreEqual(XWebTransactionStatus.EdgeExpressCompletePaymentApproved,xWebResponseProcessed.TransactionStatus);
			//A row already settled before the window asks is returned as it is.
			_listRequests.Clear();
			xWebResponseProcessed=EdgeExpress.CNP.ProcessTransactionWithRepoll(xWebResponse,payment,true,x => { });
			Assert.AreEqual(0,_listRequests.Count);
			Assert.AreEqual(XWebTransactionStatus.EdgeExpressCompletePaymentApproved,xWebResponseProcessed.TransactionStatus);
		}

		[TestMethod]
		public void ReconcileAndPayPageWindow_WindowSettlesFirst_OneCardNoAlert() {
			//A window with no time limit (the IE fallback) is still asking when reconciliation takes the row. Both get the approval; the window
			//settles the row first.
			XWebResponse xWebResponse=InsertPending(2,DateTime.Now.Subtract(EdgeExpress.CNP.ReconcileDelay).AddMinutes(-1),paymentNum:80);
			Payment payment=new Payment();
			payment.PayNum=80;
			payment.ClinicNum=2;
			LogInClerk();
			XWebResponse xWebResponseWindow=null;
			FakeGatewayWithSecondPathInFlight(() => xWebResponseWindow=EdgeExpress.CNP.ProcessTransaction(xWebResponse,payment));
			EdgeExpressReconcileResult reconcileResult=EdgeExpress.CNP.ReconcilePending();
			Assert.AreEqual(2,_listRequests.Count,"Both asked.");
			Assert.AreEqual(XWebTransactionStatus.EdgeExpressCompletePaymentApproved,xWebResponseWindow.TransactionStatus,xWebResponseWindow.DebugError);
			Assert.AreEqual(80,xWebResponseWindow.PaymentNum);
			Assert.AreEqual(0,reconcileResult.CountChecked,"Reconciliation doesn't act on a row the window settled.");
			Assert.AreEqual(0,reconcileResult.CountApproved);
			Assert.AreEqual(1,reconcileResult.CountClaimedElsewhere);
			Assert.AreEqual(1,CountCreditCards(),"One card on file.");
			XWebResponse xWebResponseDb=XWebResponses.GetOne(xWebResponse.XWebResponseNum);
			Assert.AreEqual(XWebTransactionStatus.EdgeExpressCompletePaymentApproved,xWebResponseDb.TransactionStatus);
			Assert.AreEqual(80,xWebResponseDb.PaymentNum,"Still linked to the payment, for a void or return.");
			Assert.AreEqual(0,CountAlerts(),"No alert to void a payment the window posted.");
		}

		[TestMethod]
		public void ReconcileAndPayPageWindow_ReconcileSettlesFirst_OneCardPaymentNumKept() {
			//Reconciliation settles the row while the window waits for its answer.
			XWebResponse xWebResponse=InsertPending(2,DateTime.Now.Subtract(EdgeExpress.CNP.ReconcileDelay).AddMinutes(-1),paymentNum:81);
			Payment payment=new Payment();
			payment.PayNum=81;
			payment.ClinicNum=2;
			LogInClerk();
			EdgeExpressReconcileResult reconcileResult=null;
			FakeGatewayWithSecondPathInFlight(() => reconcileResult=EdgeExpress.CNP.ReconcilePending());
			XWebResponse xWebResponseWindow=EdgeExpress.CNP.ProcessTransaction(xWebResponse,payment);
			Assert.AreEqual(2,_listRequests.Count);
			Assert.AreEqual(1,reconcileResult.CountApproved,"DebugError: "+XWebResponses.GetOne(xWebResponse.XWebResponseNum).DebugError);
			Assert.AreEqual(1,CountAlerts());
			Assert.AreEqual(1,CountCreditCards(),"The window saves no second card.");
			Assert.AreEqual(XWebTransactionStatus.EdgeExpressCompletePaymentApproved,xWebResponseWindow.TransactionStatus,"The window gets the settled row.");
			XWebResponse xWebResponseDb=XWebResponses.GetOne(xWebResponse.XWebResponseNum);
			Assert.AreEqual(81,xWebResponseDb.PaymentNum,"Reconciliation keeps the link to the window's payment.");
			Assert.AreEqual(81,xWebResponseWindow.PaymentNum);
		}

		[TestMethod]
		public void ProcessTransaction_NoPayment_KeepsPaymentNumOnEveryAnswer() {
			//Reconciliation, or Manage Cards, passes no payment. The full-row update must not unlink the row from the payment it was made for.
			XWebResponse xWebResponse=InsertPending(2,DateTime.Now.AddMinutes(-1),paymentNum:82);
			FakeGateway(Answer("103"));
			XWebResponse xWebResponseProcessed=EdgeExpress.CNP.ProcessTransaction(xWebResponse,null,doCreatePayment:false);
			Assert.AreEqual(XWebTransactionStatus.EdgeExpressExpired,xWebResponseProcessed.TransactionStatus);
			Assert.AreEqual(82,XWebResponses.GetOne(xWebResponse.XWebResponseNum).PaymentNum);
		}

		[TestMethod]
		public void ReconcilePending_UsesDatabaseTime() {
			//HpfExpiration is database time. A row whose session ended more than ReconcileDelay ago by the database clock is reconciled.
			DateTime dateTimeDb=MiscData.GetNowDateTime();
			XWebResponse xWebResponse=InsertPending(2,dateTimeDb.Subtract(EdgeExpress.CNP.ReconcileDelay).AddSeconds(-5));
			FakeGateway(Answer("103"));
			EdgeExpressReconcileResult reconcileResult=EdgeExpress.CNP.ReconcilePending();
			Assert.AreEqual(1,reconcileResult.CountChecked);
			Assert.AreEqual(XWebTransactionStatus.EdgeExpressExpired,XWebResponses.GetOne(xWebResponse.XWebResponseNum).TransactionStatus);
			//One still inside the window's result check by the database clock is left alone.
			xWebResponse=InsertPending(2,dateTimeDb.Subtract(EdgeExpress.CNP.ReconcileDelay).AddSeconds(30));
			reconcileResult=EdgeExpress.CNP.ReconcilePending();
			Assert.AreEqual(1,reconcileResult.CountStillOpen);
			Assert.AreEqual(XWebTransactionStatus.EdgeExpressPending,XWebResponses.GetOne(xWebResponse.XWebResponseNum).TransactionStatus);
		}

		[TestMethod]
		public void ReconcileDelay_CoversTheWindowsResultCheck() {
			Assert.IsTrue(EdgeExpress.CNP.ReconcileDelay>=TimeSpan.FromTicks(EdgeExpress.CNP.RepollDelay.Ticks*EdgeExpress.CNP.RepollCount)
				.Add(TimeSpan.FromMinutes(1)));
		}

		[TestMethod]
		public void ReturnSettings_ClinicThenPracticeThenDefault() {
			Assert.AreEqual(EdgeExpress.CNP.ReturnUrlDefault,EdgeExpress.CNP.GetReturnUrl(2),"No rows yet.");
			Assert.AreEqual(EdgeExpressReturnMode.Embedded,EdgeExpress.CNP.GetReturnMode(2));
			string desc=EdgeExpress.PropertyDescs.ReturnUrl;
			ProgramProperties.InsertIfMissing(_programNumEdgeExpress,desc,0,"https://pay.example-dental-office.com/done",false,false);
			ProgramProperties.InsertIfMissing(_programNumEdgeExpress,desc,1,"https://clinic1.example-dental-office.com/done",false,false);
			ProgramProperties.InsertIfMissing(_programNumEdgeExpress,desc,3,"https://www."+CodeBase.VendorDirect.HqHosts.BlockedDomains[0]+"/done",false,false);
			ProgramProperties.InsertIfMissing(_programNumEdgeExpress,EdgeExpress.PropertyDescs.ReturnMode,1,"Loopback",false,false);
			ProgramProperties.RefreshCache();
			try {
				Assert.AreEqual("https://clinic1.example-dental-office.com/done",EdgeExpress.CNP.GetReturnUrl(1));
				Assert.AreEqual("https://pay.example-dental-office.com/done",EdgeExpress.CNP.GetReturnUrl(2),"Falls back to the practice's row.");
				Assert.AreEqual(EdgeExpress.CNP.ReturnUrlDefault,EdgeExpress.CNP.GetReturnUrl(3),"A refused address is never used.");
				Assert.AreEqual(EdgeExpressReturnMode.Loopback,EdgeExpress.CNP.GetReturnMode(1));
				Assert.AreEqual(EdgeExpressReturnMode.Embedded,EdgeExpress.CNP.GetReturnMode(2));
			}
			finally {
				using(MySqlConnection connection=new MySqlConnection(CONNECTION)) {
					connection.Open();
					Exec(connection,"DELETE FROM "+DB_NAME+".programproperty WHERE PropertyDesc IN ('"+desc+"','"+EdgeExpress.PropertyDescs.ReturnMode+"')");
				}
				ProgramProperties.RefreshCache();
			}
		}

		[TestMethod]
		public void GetHostPayUrl_HonoursTheTestGatewaySetting() {
			ProgramProperties.InsertIfMissing(_programNumEdgeExpress,EdgeExpress.PropertyDescs.UseTestGateway,0,"1",false,false);
			ProgramProperties.RefreshCache();
			try {
				Assert.IsTrue(EdgeExpress.CNP.IsTestGateway());
				Assert.AreEqual(EdgeExpress.CNP.HostPayUrlTest,EdgeExpress.CNP.GetHostPayUrl());
				Assert.AreEqual(EdgeExpress.CNP.DirectPayUrlTest,EdgeExpress.CNP.GetDirectPayUrl());
			}
			finally {
				using(MySqlConnection connection=new MySqlConnection(CONNECTION)) {
					connection.Open();
					Exec(connection,"DELETE FROM "+DB_NAME+".programproperty WHERE PropertyDesc='"+EdgeExpress.PropertyDescs.UseTestGateway+"'");
				}
				ProgramProperties.RefreshCache();
			}
		}

		[TestMethod]
		public void GetXWebCreds_ReadsEncryptedAndPlainKeys_AndTreatsAnUnreadableKeyAsMissing() {
			WebPaymentProperties webPaymentProperties;
			ProgramProperties.GetXWebCreds(2,out webPaymentProperties);
			Assert.AreEqual(AUTH_KEY_CLINIC_2,webPaymentProperties.AuthKey);
			ProgramProperties.GetXWebCreds(1,out webPaymentProperties);
			Assert.AreEqual(AUTH_KEY_CLINIC_1,webPaymentProperties.AuthKey);
			AddCreds(4,"800000000004","osd1$this-is-not-a-valid-ciphertext","80000004");
			ProgramProperties.RefreshCache();
			AssertEx.Throws<CodeBase.ODException>(() => ProgramProperties.GetXWebCreds(4,out webPaymentProperties));
		}
	}
}
