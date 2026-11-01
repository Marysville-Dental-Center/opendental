// 25.4 v25_4-account-recurringcharge-fail-email with PaySimple: which PaySimple failures e-mail the patient. Each test runs one recurring charge
// through RecurringChargerator.SendPaySimple and then SendFailEmailIfNeeded, as SendCharges does. PaySimple requests go to a fake handler
// (PaySimple.SetTestHttpHandler), so nothing reaches the network; the credentials, cards and patients are synthetic and the e-mail sender is replaced.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using Task=System.Threading.Tasks.Task;
using CodeBase;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenPmsDentalBusiness;
using UnitTestsCore;

namespace M2RECURTests {
	[TestClass]
	public class PaySimpleDeclineTests {
		private const string OFFICE_ADDRESS="billing@office.example.com";
		private const string USER="APIUserTest01";
		private const string KEY="fake-key-for-tests-only";
		private const string MSG_RESULT_UNKNOWN="The result of this PaySimple transaction is unknown";
		private const string MSG_NO_CREDENTIALS="PaySimple Username, Key, or PayType is empty.";
		private List<EmailMessage> _listEmailMessagesSent;
		private FakePaySimpleHandler _fakePaySimpleHandler;
		private TestChargerator _testChargerator;

		[TestInitialize]
		public void TestInitialize() {
			RecurDb.Begin();
			RecurDb.LogInWith(EnumPermType.EmailSend);
			RecurDb.ClearOfficeEmailAddresses();
			RecurDb.SetOfficeEmailAddress(OFFICE_ADDRESS);
			Prefs.UpdateBool(PrefName.RecurringChargesFailEmailEnabled,true);
			_listEmailMessagesSent=new List<EmailMessage>();
			_fakePaySimpleHandler=new FakePaySimpleHandler();
			PaySimple.SetTestHttpHandler(_fakePaySimpleHandler,TimeSpan.FromMilliseconds(1),TimeSpan.FromSeconds(5));
			_testChargerator=new TestChargerator(new TestLog());
			_testChargerator.ActionSendFailEmail=(emailMessage,emailAddress) => {
				_listEmailMessagesSent.Add(emailMessage);
				EmailMessages.Insert(emailMessage);
			};
		}

		[TestCleanup]
		public void TestCleanup() {
			PaySimple.SetTestHttpHandler(null);
			if(RecurDb.SkipReason!=null) {
				return;
			}
			Prefs.UpdateBool(PrefName.RecurringChargesFailEmailEnabled,false);
			DataCore.NonQ("UPDATE program SET Enabled=0 WHERE ProgName='"+POut.String(ProgramName.PaySimple.ToString())+"'");
			Programs.RefreshCache();
		}

		#region Helpers
		///<summary>Turns PaySimple on at the practice level, in the sandbox. Without credentials, only the payment type is stored.</summary>
		private long SetUpPaySimple(bool hasCredentials) {
			Programs.InsertIfMissing(ProgramName.PaySimple,"PaySimple");
			DataCore.NonQ("UPDATE program SET Enabled=1 WHERE ProgName='"+POut.String(ProgramName.PaySimple.ToString())+"'");
			Programs.RefreshCache();
			long programNum=Programs.GetProgramNum(ProgramName.PaySimple);
			DataCore.NonQ("DELETE FROM programproperty WHERE ProgramNum="+POut.Long(programNum)+" AND PropertyDesc IN('"
				+POut.String(PaySimple.PropertyDescs.PaySimpleApiUserName)+"','"+POut.String(PaySimple.PropertyDescs.PaySimpleApiKey)+"','"
				+POut.String(PaySimple.PropertyDescs.PaySimplePayTypeCC)+"','"+POut.String(PaySimple.PropertyDescs.PaySimpleEnvironment)+"')");
			Def defPayType=Defs.GetDefsForCategory(DefCat.PaymentTypes,true).FirstOrDefault();
			InsertProperty(programNum,PaySimple.PropertyDescs.PaySimplePayTypeCC,(defPayType==null ? 1 : defPayType.DefNum).ToString());
			InsertProperty(programNum,PaySimple.PropertyDescs.PaySimpleEnvironment,PaySimple.Environments.Sandbox);
			if(hasCredentials) {
				InsertProperty(programNum,PaySimple.PropertyDescs.PaySimpleApiUserName,USER);
				InsertProperty(programNum,PaySimple.PropertyDescs.PaySimpleApiKey,KEY);//Plain text is read as Open Dental stored it.
			}
			ProgramProperties.RefreshCache();
			_testChargerator.SetProgram(Programs.GetCur(ProgramName.PaySimple));//SendCharges sets it before SendPaySimple.
			return programNum;
		}

		private static void InsertProperty(long programNum,string propertyDesc,string value) {
			DataCore.NonQ("INSERT INTO programproperty (ProgramNum,PropertyDesc,PropertyValue,ComputerName,ClinicNum,IsMasked,IsHighSecurity) VALUES ("
				+POut.Long(programNum)+",'"+POut.String(propertyDesc)+"','"+POut.String(value)+"','',0,0,0)");
		}

		///<summary>A PaySimple card on file with a token no other card has, and its recurring charge row as SendCharges inserts it.</summary>
		private RecurringChargeData MakePaySimpleCharge(Patient patient,double amount) {
			CreditCard creditCard=CreditCardT.CreateCard(patient.PatNum,amount,DateTime.Today.AddMonths(-2),0);
			creditCard.PaySimpleToken=(7300000000L+creditCard.CreditCardNum).ToString();
			creditCard.CCSource=CreditCardSource.PaySimple;
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
				PaySimpleToken=creditCard.PaySimpleToken,
				CCSource=creditCard.CCSource,
				CCExpiration=creditCard.CCExpiration,
				CCNumberMasked=creditCard.CCNumberMasked,
				PatName=patient.GetNameLF(),
				RecurringChargeDate=DateTime.Today,
				CCIsRecurringActive=true,
			};
		}

		///<summary>Charges the card and decides the e-mail, in SendCharges' order.</summary>
		private RecurringChargeFailEmailResult ChargeAndDecideEmail(RecurringChargeData chargeData) {
			_testChargerator.SendPaySimple(chargeData,new StringBuilder());
			return _testChargerator.SendFailEmailIfNeeded(chargeData);
		}

		///<summary>PaySimple's answer to a charge: the payment, in its response envelope.</summary>
		private static HttpResponseMessage PaymentResponse(RecurringChargeData chargeData,string status,string providerAuthCode,bool isDecline) {
			string failureData=(providerAuthCode.ToLower().Contains("approved")) ? "null"
				: "{\"Code\":\"D5\",\"Description\":\"Insufficient funds (synthetic)\",\"MerchantActionText\":\"Contact the customer\",\"IsDecline\":"
					+(isDecline ? "true" : "false")+"}";
			string payment="{\"Id\":29124043,\"CustomerId\":8051100,\"AccountId\":"+chargeData.PaySimpleToken+",\"ReferenceId\":0,\"Status\":\""+status+"\","
				+"\"PaymentType\":\"CC\",\"PaymentSubType\":\"Moto\",\"ProviderAuthCode\":\""+providerAuthCode+"\","
				+"\"Amount\":"+chargeData.RecurringCharge.ChargeAmt.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+","
				+"\"OrderId\":null,\"PaymentDate\":\"2026-10-01T06:00:00Z\",\"ActualSettledDate\":null,\"ReturnDate\":null,\"FailureData\":"+failureData+"}";
			string envelope="{\"Meta\":{\"Errors\":null,\"HttpStatus\":\"Created\",\"HttpStatusCode\":201,\"PagingDetails\":null},\"Response\":"+payment+"}";
			return new HttpResponseMessage(HttpStatusCode.Created) { Content=new StringContent(envelope,Encoding.UTF8,"application/json") };
		}

		private static void AssertNoEmailLine(RecurringChargeData chargeData) {
			string errorMsg=chargeData.RecurringCharge.ErrorMsg??"";
			Assert.IsFalse(errorMsg.Contains(RecurringChargeFailEmails.ERROR_MSG_SENT_PREFIX),errorMsg);
			Assert.IsFalse(errorMsg.Contains(RecurringChargeFailEmails.ERROR_MSG_NOT_SENT_PREFIX),errorMsg);
		}
		#endregion Helpers

		[TestMethod]
		public void Declined_ByPaySimple_EmailsThePatient() {
			SetUpPaySimple(hasCredentials:true);
			Patient patient=RecurDb.CreatePatient("PsDecline",email:"ps.decline@example.com");
			RecurringChargeData chargeData=MakePaySimpleCharge(patient,45.5);
			_fakePaySimpleHandler.Enqueue(request => PaymentResponse(chargeData,"Failed","DECLINE",isDecline:true));
			RecurringChargeFailEmailResult result=ChargeAndDecideEmail(chargeData);
			StringAssert.Contains(chargeData.RecurringCharge.ErrorMsg,"Payment was declined by PaySimple.","PaySimpleApi.ToSaleResponse's decline.");
			Assert.IsTrue(chargeData.IsDeclinedByProcessor);
			Assert.AreEqual(RecurringChargeFailEmailStatus.Sent,result.Status,result.Reason);
			Assert.AreEqual("ps.decline@example.com",_listEmailMessagesSent.Single().ToAddress);
			StringAssert.Contains(chargeData.RecurringCharge.ErrorMsg,RecurringChargeFailEmails.ERROR_MSG_SENT_PREFIX+"ps.decline@example.com.");
			Assert.AreEqual(1,_fakePaySimpleHandler.ListRequests.Count(x => x=="POST /v4/payment"));
		}

		[TestMethod]
		public void NotApproved_ByPaySimple_EmailsThePatient() {
			//PaySimple answered, but not with an approval, and did not call it a decline: ToSaleResponse's other message. Still PaySimple's answer.
			SetUpPaySimple(hasCredentials:true);
			Patient patient=RecurDb.CreatePatient("PsNotApproved",email:"ps.notapproved@example.com");
			RecurringChargeData chargeData=MakePaySimpleCharge(patient,20);
			_fakePaySimpleHandler.Enqueue(request => PaymentResponse(chargeData,"Failed","PICK UP CARD",isDecline:false));
			RecurringChargeFailEmailResult result=ChargeAndDecideEmail(chargeData);
			StringAssert.Contains(chargeData.RecurringCharge.ErrorMsg,"Payment was not approved by PaySimple.");
			Assert.AreEqual(RecurringChargeFailEmailStatus.Sent,result.Status,result.Reason);
			Assert.AreEqual(1,_listEmailMessagesSent.Count);
		}

		[TestMethod]
		public void ResultUnknown_MayHaveBeenCharged_NoEmail() {
			//The connection drops after the charge went out and the payment cannot be found by its OrderId: PaySimpleResultUnknownException.
			SetUpPaySimple(hasCredentials:true);
			Patient patient=RecurDb.CreatePatient("PsUnknown",email:"ps.unknown@example.com");
			RecurringChargeData chargeData=MakePaySimpleCharge(patient,30);
			_fakePaySimpleHandler.Enqueue(request => throw new HttpRequestException("Simulated dropped connection."));
			//The lookup that follows gets 404s (the handler's default), so the outcome stays unknown.
			RecurringChargeFailEmailResult result=ChargeAndDecideEmail(chargeData);
			StringAssert.Contains(chargeData.RecurringCharge.ErrorMsg,MSG_RESULT_UNKNOWN);
			Assert.IsFalse(chargeData.IsDeclinedByProcessor);
			Assert.AreEqual(RecurringChargeFailEmailStatus.NotNeeded,result.Status);
			Assert.AreEqual(0,_listEmailMessagesSent.Count);
			AssertNoEmailLine(chargeData);
			Assert.AreEqual(1,_fakePaySimpleHandler.ListRequests.Count(x => x=="POST /v4/payment"),"Never posted twice.");
		}

		[TestMethod]
		public void MissingCredentials_SetupProblem_NoEmail() {
			//A clinic without PaySimple credentials: ValidateProgram's ODException, before any request. Nobody in the batch is e-mailed.
			SetUpPaySimple(hasCredentials:false);
			Patient patient1=RecurDb.CreatePatient("PsNoCreds1",email:"ps.nocreds1@example.com");
			Patient patient2=RecurDb.CreatePatient("PsNoCreds2",email:"ps.nocreds2@example.com");
			List<RecurringChargeData> listChargeData=new List<RecurringChargeData> { MakePaySimpleCharge(patient1,20),MakePaySimpleCharge(patient2,25) };
			foreach(RecurringChargeData chargeData in listChargeData) {
				RecurringChargeFailEmailResult result=ChargeAndDecideEmail(chargeData);
				StringAssert.Contains(chargeData.RecurringCharge.ErrorMsg,MSG_NO_CREDENTIALS);
				Assert.IsFalse(chargeData.IsDeclinedByProcessor);
				Assert.AreEqual(RecurringChargeFailEmailStatus.NotNeeded,result.Status);
				AssertNoEmailLine(chargeData);
				//The card-inactivation behaviour is Open Dental's and unchanged: MarkDeclined still sets ChargeDeclined when that preference is on.
				Assert.AreEqual(PrefC.GetBool(PrefName.RecurringChargesInactivateDeclinedCards)
					? RecurringChargeStatus.ChargeDeclined : RecurringChargeStatus.ChargeFailed,chargeData.RecurringCharge.ChargeStatus);
			}
			Assert.AreEqual(0,_listEmailMessagesSent.Count);
			Assert.AreEqual(0,_testChargerator.FailEmailsSent+_testChargerator.FailEmailsNotSent);
			Assert.AreEqual(0,_fakePaySimpleHandler.ListRequests.Count,"Nothing was sent to PaySimple.");
		}

		[TestMethod]
		public void ChargedThenAnErrorAfterwards_NoEmail() {
			//PaySimple approves the charge, then the card row is gone when SendPaySimple reads it back (deleted during the run): the code after the
			//charge throws, and the catch calls MarkDeclined on a charge that went through.
			SetUpPaySimple(hasCredentials:true);
			Patient patient=RecurDb.CreatePatient("PsCharged",email:"ps.charged@example.com");
			RecurringChargeData chargeData=MakePaySimpleCharge(patient,35);
			long creditCardNum=chargeData.RecurringCharge.CreditCardNum;
			_fakePaySimpleHandler.Enqueue(request => {
				using(MySqlConnection connection=TestDb.OpenServer()) {
					TestDb.Exec(connection,"DELETE FROM `"+RecurDb.DbName+"`.creditcard WHERE CreditCardNum="+creditCardNum);
				}
				return PaymentResponse(chargeData,"Authorized","Approved",isDecline:false);
			});
			RecurringChargeFailEmailResult result=ChargeAndDecideEmail(chargeData);
			Assert.AreEqual(RecurringChargeStatus.ChargeSuccessful,chargeData.RecurringCharge.ChargeStatus,"The card was charged.");
			StringAssert.Contains(chargeData.RecurringCharge.ErrorMsg,"Error processing card:","The error after the charge is still recorded.");
			Assert.IsFalse(chargeData.IsDeclinedByProcessor);
			Assert.AreEqual(RecurringChargeFailEmailStatus.NotNeeded,result.Status);
			Assert.AreEqual(0,_listEmailMessagesSent.Count);
			AssertNoEmailLine(chargeData);
			Assert.AreEqual(1,_testChargerator.Success);
		}
	}

	///<summary>A chargerator whose program can be set, as SendCharges sets it before it calls SendPaySimple.</summary>
	public class TestChargerator:RecurringChargerator {
		public TestChargerator(Logger.IWriteLine log) : base(log,true) {
		}

		public void SetProgram(Program program) {
			_progCur=program;
		}
	}

	///<summary>Answers PaySimple requests from a queue instead of the network and records "METHOD /path" for each. A responder may throw, as a
	///dropped connection does. Requests the queue does not cover get 404 with no body.</summary>
	public sealed class FakePaySimpleHandler:HttpMessageHandler {
		private readonly Queue<Func<HttpRequestMessage,HttpResponseMessage>> _queueResponders=new Queue<Func<HttpRequestMessage,HttpResponseMessage>>();
		private readonly List<string> _listRequests=new List<string>();

		public List<string> ListRequests {
			get {
				lock(_listRequests) {
					return _listRequests.ToList();
				}
			}
		}

		public void Enqueue(Func<HttpRequestMessage,HttpResponseMessage> responder) {
			lock(_queueResponders) {
				_queueResponders.Enqueue(responder);
			}
		}

		protected override System.Threading.Tasks.Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken) {
			lock(_listRequests) {
				_listRequests.Add(request.Method.Method+" "+request.RequestUri.AbsolutePath);
			}
			Func<HttpRequestMessage,HttpResponseMessage> responder=null;
			lock(_queueResponders) {
				if(_queueResponders.Count>0) {
					responder=_queueResponders.Dequeue();
				}
			}
			HttpResponseMessage response;
			try {
				response=(responder==null) ? new HttpResponseMessage(HttpStatusCode.NotFound) { Content=new StringContent("") } : responder(request);
			}
			catch(Exception ex) {
				return Task.FromException<HttpResponseMessage>(ex);
			}
			response.RequestMessage=request;
			return Task.FromResult(response);
		}
	}
}
