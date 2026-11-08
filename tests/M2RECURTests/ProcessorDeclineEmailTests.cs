// 25.4 v25_4-account-recurringcharge-fail-email with EdgeExpress and PayConnect 2: which answers e-mail the patient. Each test runs recurring
// charges through RecurringChargerator.SendEdgeExpress or SendPayConnect and then SendFailEmailIfNeeded, in SendCharges' order. EdgeExpress
// requests are answered by XWebs.Mock and PayConnect requests by PayConnect2.Mock, so nothing reaches the network; the credentials, cards and
// patients are synthetic and the e-mail sender is replaced.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.WebTypes.Shared.XWeb;
using UnitTestsCore;

namespace M2RECURTests {
	[TestClass]
	public class ProcessorDeclineEmailTests {
		private const string OFFICE_ADDRESS="billing@office.example.com";
		private List<EmailMessage> _listEmailMessagesSent;
		private TestChargerator _testChargerator;
		///<summary>The request XML EdgeExpress would have received, one per charge.</summary>
		private List<string> _listEdgeExpressRequests;
		private List<string> _listPayConnectLogLines;

		[TestInitialize]
		public void TestInitialize() {
			RecurDb.Begin();
			RecurDb.LogInWith(EnumPermType.EmailSend);
			RecurDb.ClearOfficeEmailAddresses();
			RecurDb.SetOfficeEmailAddress(OFFICE_ADDRESS);
			Prefs.UpdateBool(PrefName.RecurringChargesFailEmailEnabled,true);
			_listEmailMessagesSent=new List<EmailMessage>();
			_listEdgeExpressRequests=new List<string>();
			_listPayConnectLogLines=new List<string>();
			PayConnectErrorLog.ActionWriteLineForTests=line => _listPayConnectLogLines.Add(line);//Nothing is written to the log folder.
			_testChargerator=new TestChargerator(new TestLog());
			_testChargerator.ActionSendFailEmail=(emailMessage,emailAddress) => {
				_listEmailMessagesSent.Add(emailMessage);
				EmailMessages.Insert(emailMessage);
			};
		}

		[TestCleanup]
		public void TestCleanup() {
			XWebs.Mock=null;
			PayConnect2.Mock=null;
			PayConnectErrorLog.ActionWriteLineForTests=null;
			if(RecurDb.SkipReason!=null) {
				return;
			}
			Prefs.UpdateBool(PrefName.RecurringChargesFailEmailEnabled,false);
			DataCore.NonQ("UPDATE program SET Enabled=0 WHERE ProgName IN('"+POut.String(ProgramName.EdgeExpress.ToString())+"','"
				+POut.String(ProgramName.PayConnect.ToString())+"')");
			Programs.RefreshCache();
		}

		#region Helpers
		///<summary>Turns the program on at the practice level with only the given properties, and hands it to the chargerator as SendCharges does.</summary>
		private void SetUpProgram(ProgramName programName,Dictionary<string,string> dictProperties) {
			Programs.InsertIfMissing(programName,programName.ToString());
			DataCore.NonQ("UPDATE program SET Enabled=1 WHERE ProgName='"+POut.String(programName.ToString())+"'");
			Programs.RefreshCache();
			long programNum=Programs.GetProgramNum(programName);
			DataCore.NonQ("DELETE FROM programproperty WHERE ProgramNum="+POut.Long(programNum));
			foreach(KeyValuePair<string,string> pair in dictProperties) {
				DataCore.NonQ("INSERT INTO programproperty (ProgramNum,PropertyDesc,PropertyValue,ComputerName,ClinicNum,IsMasked,IsHighSecurity) VALUES ("
					+POut.Long(programNum)+",'"+POut.String(pair.Key)+"','"+POut.String(pair.Value)+"','',0,0,0)");
			}
			ProgramProperties.RefreshCache();
			_testChargerator.SetProgram(Programs.GetCur(programName));
		}

		private static string GetPayTypeDefNum() {
			Def defPayType=Defs.GetDefsForCategory(DefCat.PaymentTypes,true).FirstOrDefault();
			return (defPayType==null ? 1 : defPayType.DefNum).ToString();
		}

		///<summary>EdgeExpress with synthetic merchant credentials (plain text, as Open Dental stored them). Every request is answered with
		///responseXml.</summary>
		private void SetUpEdgeExpress(string responseXml) {
			SetUpProgram(ProgramName.EdgeExpress,new Dictionary<string,string> {
				{ ProgramProperties.PropertyDescs.EdgeExpress.XWebID,"800000000001" },
				{ ProgramProperties.PropertyDescs.EdgeExpress.TerminalID,"80000001" },
				{ ProgramProperties.PropertyDescs.EdgeExpress.AuthKey,"SyntheticKeyForM2RecurTests00001" },
				{ ProgramProperties.PropertyDescs.EdgeExpress.PaymentType,GetPayTypeDefNum() },
			});
			XWebs.Mock=new XWebs.MockXWebs();
			XWebs.Mock.ResponseForRequest=(input,url) => {
				_listEdgeExpressRequests.Add(input);
				return responseXml;
			};
		}

		///<summary>EdgeExpress's direct-pay answer with the given response code.</summary>
		private static string EdgeExpressAnswer(int responseCode,string description) {
			return "<RESULT><RESPONSECODE>"+responseCode.ToString("000")+"</RESPONSECODE><RESPONSEDESCRIPTION>"+description+"</RESPONSEDESCRIPTION>"
				+"<TRANSACTIONTYPE>CREDITSALE</TRANSACTIONTYPE><MASKEDCARDNUMBER>XXXXXXXXXXXX1111</MASKEDCARDNUMBER></RESULT>";
		}

		///<summary>PayConnect 2 with the office's API key and the practice's Secret (synthetic, plain text). Every request is answered with
		///response.</summary>
		private void SetUpPayConnect(PayConnect2.PayConnect2Response response) {
			SetUpProgram(ProgramName.PayConnect,new Dictionary<string,string> {
				{ PayConnect.ProgramProperties.ProgramVersion,PayConnect.Version2 },
				{ PayConnect.ProgramProperties.ApiKey,"synthetic-integrator-key" },
				{ PayConnect.ProgramProperties.ApiSecret,"synthetic-merchant-secret" },
				{ PayConnect.ProgramProperties.UseSandbox,"1" },
				{ "PaymentType",GetPayTypeDefNum() },
			});
			Assert.IsTrue(PayConnect2.IsConfigured(0),"The practice is set up for PayConnect 2.");
			PayConnect2.Mock=new PayConnect2.MockPayConnect2 { Response=response };
		}

		///<summary>PayConnect's answer to a create, through the same parser the real requests use.</summary>
		private static PayConnect2.PayConnect2Response PayConnectAnswer(HttpStatusCode httpStatusCode,string body) {
			return PayConnect2.ParseHttpResponse(PayConnect2.ApiRoute.CreateTransaction,httpStatusCode,body);
		}

		///<summary>A card on file with a token no other card has, and its recurring charge row as SendCharges inserts it.</summary>
		private RecurringChargeData MakeCharge(Patient patient,double amount,CreditCardSource creditCardSource) {
			CreditCard creditCard=CreditCardT.CreateCard(patient.PatNum,amount,DateTime.Today.AddMonths(-2),0);
			creditCard.CCSource=creditCardSource;
			if(creditCardSource==CreditCardSource.PayConnect) {
				creditCard.PayConnectToken="pc-token-"+creditCard.CreditCardNum;
				creditCard.PayConnectTokenExp=DateTime.Today.AddYears(2);
			}
			else {
				creditCard.XChargeToken="ee-token-"+creditCard.CreditCardNum;
			}
			CreditCards.Update(creditCard);
			RecurringCharge recurringCharge=new RecurringCharge {
				PatNum=patient.PatNum,
				ChargeAmt=amount,
				ChargeStatus=RecurringChargeStatus.NotYetCharged,
				CreditCardNum=creditCard.CreditCardNum,
				DateTimeCharge=MiscData.GetNowDateTime(),
				ErrorMsg="",
			};
			RecurringCharges.Insert(recurringCharge);
			return new RecurringChargeData {
				RecurringCharge=recurringCharge,
				XChargeToken=creditCard.XChargeToken??"",
				PayConnectToken=creditCard.PayConnectToken??"",
				PayConnectTokenExp=creditCard.PayConnectTokenExp,
				CCSource=creditCard.CCSource,
				CCExpiration=creditCard.CCExpiration,
				CCNumberMasked=creditCard.CCNumberMasked,
				PatName=patient.GetNameLF(),
				RecurringChargeDate=DateTime.Today,
				CCIsRecurringActive=true,
			};
		}

		///<summary>Charges the card with EdgeExpress and decides the e-mail, in SendCharges' order.</summary>
		private RecurringChargeFailEmailResult ChargeEdgeExpress(RecurringChargeData chargeData) {
			List<CreditCardSource> listCreditCardSourcesOpenEdge=new List<CreditCardSource> {//As SendCharges passes them.
				CreditCardSource.XServer,CreditCardSource.XServerPayConnect,CreditCardSource.XWeb,CreditCardSource.XWebPortalLogin,
				CreditCardSource.EdgeExpressRCM,CreditCardSource.EdgeExpressCNP,
			};
			_testChargerator.SendEdgeExpress(chargeData,false,new StringBuilder(),listCreditCardSourcesOpenEdge);
			return _testChargerator.SendFailEmailIfNeeded(chargeData);
		}

		///<summary>Charges the card with PayConnect and decides the e-mail, in SendCharges' order.</summary>
		private RecurringChargeFailEmailResult ChargePayConnect(RecurringChargeData chargeData) {
			_testChargerator.SendPayConnect(chargeData,false,new StringBuilder());
			return _testChargerator.SendFailEmailIfNeeded(chargeData);
		}

		private static void AssertNoEmailLine(RecurringChargeData chargeData) {
			string errorMsg=chargeData.RecurringCharge.ErrorMsg??"";
			Assert.IsFalse(errorMsg.Contains(RecurringChargeFailEmails.ERROR_MSG_SENT_PREFIX),errorMsg);
			Assert.IsFalse(errorMsg.Contains(RecurringChargeFailEmails.ERROR_MSG_NOT_SENT_PREFIX),errorMsg);
		}

		///<summary>MarkDeclined's card-inactivation behaviour is Open Dental's and unchanged: ChargeDeclined when that preference is on.</summary>
		private static void AssertMarkedDeclined(RecurringChargeData chargeData) {
			Assert.AreEqual(PrefC.GetBool(PrefName.RecurringChargesInactivateDeclinedCards)
				? RecurringChargeStatus.ChargeDeclined : RecurringChargeStatus.ChargeFailed,chargeData.RecurringCharge.ChargeStatus);
		}

		///<summary>Each charge e-mails nobody: no flag, no e-mail, no outcome line, no count; the decline status is unchanged.</summary>
		private void AssertNobodyEmailed(List<RecurringChargeData> listChargeData,List<RecurringChargeFailEmailResult> listResults) {
			for(int i=0;i<listChargeData.Count;i++) {
				Assert.IsFalse(listChargeData[i].IsDeclinedByProcessor,listChargeData[i].RecurringCharge.ErrorMsg);
				Assert.AreEqual(RecurringChargeFailEmailStatus.NotNeeded,listResults[i].Status);
				AssertNoEmailLine(listChargeData[i]);
				AssertMarkedDeclined(listChargeData[i]);
			}
			Assert.AreEqual(0,_listEmailMessagesSent.Count);
			Assert.AreEqual(0,_testChargerator.FailEmailsSent+_testChargerator.FailEmailsNotSent+_testChargerator.FailEmailsSkipped);
		}
		#endregion Helpers

		#region EdgeExpress
		[TestMethod]
		public void EdgeExpress_AuthenticationError_EmailsNobodyInTheBatch() {
			//805: the card processor rejected the clinic's X-Web ID, Terminal ID or Auth Key. Every card in the batch gets the same answer.
			SetUpEdgeExpress(EdgeExpressAnswer(805,"Authentication Error (synthetic)"));
			List<RecurringChargeData> listChargeData=new List<RecurringChargeData> {
				MakeCharge(RecurDb.CreatePatient("EeAuth1",email:"ee.auth1@example.com"),20,CreditCardSource.EdgeExpressCNP),
				MakeCharge(RecurDb.CreatePatient("EeAuth2",email:"ee.auth2@example.com"),25,CreditCardSource.EdgeExpressCNP),
			};
			List<RecurringChargeFailEmailResult> listResults=listChargeData.Select(x => ChargeEdgeExpress(x)).ToList();
			Assert.AreEqual(2,_listEdgeExpressRequests.Count,"Both cards were sent to EdgeExpress.");
			foreach(RecurringChargeData chargeData in listChargeData) {
				StringAssert.Contains(chargeData.RecurringCharge.ErrorMsg,"Response from Global Payments V1: "+XWebResponseCodes.AuthenticationError);
			}
			AssertNobodyEmailed(listChargeData,listResults);
		}

		[TestMethod]
		public void EdgeExpress_DuplicateTransaction_NoEmail() {
			//813: the same card and amount were charged minutes earlier, so the card was charged. Not a decline of the card.
			SetUpEdgeExpress(EdgeExpressAnswer(813,"Duplicate Transaction (synthetic)"));
			RecurringChargeData chargeData=MakeCharge(RecurDb.CreatePatient("EeDup",email:"ee.dup@example.com"),30,CreditCardSource.EdgeExpressCNP);
			RecurringChargeFailEmailResult result=ChargeEdgeExpress(chargeData);
			StringAssert.Contains(chargeData.RecurringCharge.ErrorMsg,"Response from Global Payments V1: "+XWebResponseCodes.DuplicateTransactionError);
			AssertNobodyEmailed(new List<RecurringChargeData> { chargeData },new List<RecurringChargeFailEmailResult> { result });
		}

		[TestMethod]
		public void EdgeExpress_Declined_EmailsThePatient() {
			SetUpEdgeExpress(EdgeExpressAnswer(1,"DECLINED (synthetic)"));
			RecurringChargeData chargeData=MakeCharge(RecurDb.CreatePatient("EeDecline",email:"ee.decline@example.com"),45.5,
				CreditCardSource.EdgeExpressCNP);
			RecurringChargeFailEmailResult result=ChargeEdgeExpress(chargeData);
			StringAssert.Contains(chargeData.RecurringCharge.ErrorMsg,"Response from Global Payments V1: "+XWebResponseCodes.Declined);
			Assert.IsTrue(chargeData.IsDeclinedByProcessor);
			AssertMarkedDeclined(chargeData);
			Assert.AreEqual(RecurringChargeFailEmailStatus.Sent,result.Status,result.Reason);
			Assert.AreEqual("ee.decline@example.com",_listEmailMessagesSent.Single().ToAddress);
			StringAssert.Contains(chargeData.RecurringCharge.ErrorMsg,RecurringChargeFailEmails.ERROR_MSG_SENT_PREFIX+"ee.decline@example.com.");
			Assert.AreEqual(1,_testChargerator.FailEmailsSent);
		}
		#endregion EdgeExpress

		#region PayConnect 2
		[TestMethod]
		public void PayConnect_SecretRejected_EmailsNobodyInTheBatch() {
			//401 with an error object: the clinic's Secret was not accepted (PayConnect2.ParseErrorResponse, SecretRejected).
			SetUpPayConnect(PayConnectAnswer(HttpStatusCode.Unauthorized,"{\"errorType\":\"RESPONSE\",\"error\":{\"message\":\"Invalid API key\"}}"));
			Assert.AreEqual(PayConnect2.PayConnectErrorKind.SecretRejected,PayConnect2.Mock.Response.ErrorResponse.Kind);
			List<RecurringChargeData> listChargeData=new List<RecurringChargeData> {
				MakeCharge(RecurDb.CreatePatient("PcSecret1",email:"pc.secret1@example.com"),20,CreditCardSource.PayConnect),
				MakeCharge(RecurDb.CreatePatient("PcSecret2",email:"pc.secret2@example.com"),25,CreditCardSource.PayConnect),
			};
			List<RecurringChargeFailEmailResult> listResults=listChargeData.Select(x => ChargePayConnect(x)).ToList();
			foreach(RecurringChargeData chargeData in listChargeData) {
				StringAssert.Contains(chargeData.RecurringCharge.ErrorMsg,"Secret");
			}
			Assert.AreEqual(2,_listPayConnectLogLines.Count,"Each failure is in the PayConnect error log.");
			AssertNobodyEmailed(listChargeData,listResults);
		}

		[TestMethod]
		public void PayConnect_ApiKeyRejected_NoEmail() {
			//401 with a plain-string error: the office's API key (ApiKeyRejected).
			SetUpPayConnect(PayConnectAnswer(HttpStatusCode.Unauthorized,"{\"error\":\"Authorization field missing\"}"));
			Assert.AreEqual(PayConnect2.PayConnectErrorKind.ApiKeyRejected,PayConnect2.Mock.Response.ErrorResponse.Kind);
			RecurringChargeData chargeData=MakeCharge(RecurDb.CreatePatient("PcKey",email:"pc.key@example.com"),20,CreditCardSource.PayConnect);
			RecurringChargeFailEmailResult result=ChargePayConnect(chargeData);
			AssertNobodyEmailed(new List<RecurringChargeData> { chargeData },new List<RecurringChargeFailEmailResult> { result });
		}

		[TestMethod]
		public void PayConnect_ServerError_NoEmail() {
			//Any 5xx is PayConnect's own problem ("PayConnect returned an error (HTTP 500)"), not the card's.
			SetUpPayConnect(PayConnectAnswer(HttpStatusCode.InternalServerError,""));
			Assert.AreEqual(PayConnect2.PayConnectErrorKind.Vendor,PayConnect2.Mock.Response.ErrorResponse.Kind);
			RecurringChargeData chargeData=MakeCharge(RecurDb.CreatePatient("Pc500",email:"pc.500@example.com"),20,CreditCardSource.PayConnect);
			RecurringChargeFailEmailResult result=ChargePayConnect(chargeData);
			StringAssert.Contains(chargeData.RecurringCharge.ErrorMsg,"HTTP 500");
			AssertNobodyEmailed(new List<RecurringChargeData> { chargeData },new List<RecurringChargeFailEmailResult> { result });
		}

		[TestMethod]
		public void PayConnect_Declined_EmailsThePatient() {
			SetUpPayConnect(PayConnectAnswer(HttpStatusCode.PaymentRequired,"{\"errorType\":\"RESPONSE\",\"error\":\"Card declined by issuer\"}"));
			RecurringChargeData chargeData=MakeCharge(RecurDb.CreatePatient("PcDecline",email:"pc.decline@example.com"),40,CreditCardSource.PayConnect);
			RecurringChargeFailEmailResult result=ChargePayConnect(chargeData);
			StringAssert.Contains(chargeData.RecurringCharge.ErrorMsg,"Card declined by issuer");
			Assert.IsTrue(chargeData.IsDeclinedByProcessor);
			AssertMarkedDeclined(chargeData);
			Assert.AreEqual(RecurringChargeFailEmailStatus.Sent,result.Status,result.Reason);
			Assert.AreEqual("pc.decline@example.com",_listEmailMessagesSent.Single().ToAddress);
			StringAssert.Contains(chargeData.RecurringCharge.ErrorMsg,RecurringChargeFailEmails.ERROR_MSG_SENT_PREFIX+"pc.decline@example.com.");
			AssertOnlyAZeroPayment(chargeData);
		}

		///<summary>PayConnect's answer to a recurring sale with 2xx and the given status, amount in cents.</summary>
		private static PayConnect2.PayConnect2Response PayConnectCreateAnswer(string status,int amountCents) {
			return PayConnectAnswer(HttpStatusCode.OK,"{\"referenceId\":\"R-REC\",\"transactionId\":901,\"status\":\""+status+"\",\"amount\":"+amountCents
				+",\"type\":\"Sale\",\"frequency\":\"Recurring\",\"authCode\":\"SYN001\"}");
		}

		///<summary>The payment rows of the charge's patient: (PayAmt, IsCcCompleted, Receipt).</summary>
		private static List<(double PayAmt,bool IsCcCompleted,string Receipt)> GetPayments(RecurringChargeData chargeData) {
			System.Data.DataTable table=DataCore.GetTable("SELECT PayAmt,IsCcCompleted,Receipt FROM payment WHERE PatNum="
				+POut.Long(chargeData.RecurringCharge.PatNum));
			return table.Select().Select(x => (PIn.Double(x["PayAmt"].ToString()),PIn.Bool(x["IsCcCompleted"].ToString()),PIn.String(x["Receipt"].ToString())))
				.ToList();
		}

		///<summary>A charge that was not made is recorded like every other decline: one $0 payment (the record that the charge was considered) with an
		///empty receipt, never a payment of the amount, and no charge counted as made. A receipt would show PayConnect's amount as agreed and signed, and
		///the payment window would offer to print or e-mail it to the patient.</summary>
		private void AssertOnlyAZeroPayment(RecurringChargeData chargeData) {
			List<(double PayAmt,bool IsCcCompleted,string Receipt)> listPayments=GetPayments(chargeData);
			Assert.AreEqual(1,listPayments.Count,"One payment row: the $0 record of the decline.");
			Assert.AreEqual(0,listPayments[0].PayAmt,0.001,"Never a payment of the amount.");
			Assert.IsFalse(listPayments[0].IsCcCompleted);
			Assert.AreEqual("",listPayments[0].Receipt,"No receipt for a charge that was not made.");
			Assert.AreNotEqual(RecurringChargeStatus.ChargeSuccessful,chargeData.RecurringCharge.ChargeStatus);
			Assert.AreEqual(0,_testChargerator.Success);
		}

		[TestMethod]
		public void PayConnect_2xxDeclined_IsADecline_EmailsThePatient_AndIsNotRecordedAsPaid() {
			//Owner question M2-RECUR 6 (fixed on 2026-11-07): PayConnect answered the recurring sale with 2xx and status Declined. It was recorded as
			//paid, and nobody was told.
			SetUpPayConnect(PayConnectCreateAnswer("Declined",4000));
			Assert.AreEqual(PayConnect2.ResponseType.CreateTransaction,PayConnect2.Mock.Response.ResponseType);
			RecurringChargeData chargeData=MakeCharge(RecurDb.CreatePatient("Pc2xxDecline",email:"pc.2xx.decline@example.com"),40,
				CreditCardSource.PayConnect);
			RecurringChargeFailEmailResult result=ChargePayConnect(chargeData);
			StringAssert.Contains(chargeData.RecurringCharge.ErrorMsg,"Transaction Failed, error status Declined");
			Assert.IsTrue(chargeData.IsDeclinedByProcessor);
			AssertMarkedDeclined(chargeData);
			Assert.AreEqual(RecurringChargeFailEmailStatus.Sent,result.Status,result.Reason);
			Assert.AreEqual("pc.2xx.decline@example.com",_listEmailMessagesSent.Single().ToAddress);
			StringAssert.Contains(chargeData.RecurringCharge.ErrorMsg,RecurringChargeFailEmails.ERROR_MSG_SENT_PREFIX+"pc.2xx.decline@example.com.");
			AssertOnlyAZeroPayment(chargeData);
			Assert.AreEqual(1,_listPayConnectLogLines.Count,"The answer is in the PayConnect error log (NotApproved).");
		}

		[TestMethod]
		public void PayConnect_2xxOtherStatusNotApproved_IsADecline_WithoutEmail() {
			//A 2xx status that is neither an approval nor a decline (here Error): not charged, so not a payment; not a card decline, so no e-mail.
			SetUpPayConnect(PayConnectCreateAnswer("Error",2500));
			RecurringChargeData chargeData=MakeCharge(RecurDb.CreatePatient("Pc2xxError",email:"pc.2xx.error@example.com"),25,CreditCardSource.PayConnect);
			RecurringChargeFailEmailResult result=ChargePayConnect(chargeData);
			StringAssert.Contains(chargeData.RecurringCharge.ErrorMsg,"Transaction Failed, error status Error");
			AssertNobodyEmailed(new List<RecurringChargeData> { chargeData },new List<RecurringChargeFailEmailResult> { result });
			AssertOnlyAZeroPayment(chargeData);
		}

		[TestMethod]
		public void PayConnect_Approved_RecordsThePayment_NoEmail() {
			SetUpPayConnect(PayConnectCreateAnswer("Processed",3500));
			RecurringChargeData chargeData=MakeCharge(RecurDb.CreatePatient("PcApproved",email:"pc.approved@example.com"),35,CreditCardSource.PayConnect);
			RecurringChargeFailEmailResult result=ChargePayConnect(chargeData);
			Assert.AreEqual(RecurringChargeStatus.ChargeSuccessful,chargeData.RecurringCharge.ChargeStatus,chargeData.RecurringCharge.ErrorMsg);
			Assert.IsFalse(chargeData.IsDeclinedByProcessor);
			Assert.AreEqual(1,_testChargerator.Success);
			Assert.AreEqual(RecurringChargeFailEmailStatus.NotNeeded,result.Status);
			Assert.AreEqual(0,_listEmailMessagesSent.Count);
			List<(double PayAmt,bool IsCcCompleted,string Receipt)> listPayments=GetPayments(chargeData);
			Assert.AreEqual(1,listPayments.Count);
			Assert.AreEqual(35,listPayments[0].PayAmt,0.001,"The approved amount.");
			Assert.IsTrue(listPayments[0].IsCcCompleted);
			Assert.IsFalse(string.IsNullOrEmpty(listPayments[0].Receipt),"An approved charge keeps its receipt.");
			StringAssert.Contains(listPayments[0].Receipt,35m.ToString("c"),"The receipt shows the approved amount.");
			Assert.AreEqual(0,_listPayConnectLogLines.Count,"An approval is not an error.");
		}
		#endregion PayConnect 2
	}
}
