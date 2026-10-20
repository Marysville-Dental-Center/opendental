// Tests for the payment windows' rules (PAY-UI) that need no database: the ACH status text, the "Check ACH status" gate, the note lines a status
// check appended, the PayConnect 2 void outcome after a lost answer, the PayConnect 2 error text for recurring charges, and the PaySimple ACH
// card sources. Nothing leaves this computer.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Net;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.Bridges;
using OpenDentBusiness.VendorDirect;

namespace VendorDirectTests.PayUi {
	[TestClass]
	public class PaymentWindowRulesTests {
		private const string REF_NUM="REF-0001";

		[TestCleanup]
		public void TestCleanup() {
			CareCredit.ResetDependencies();
		}

		#region ACH status
		[TestMethod]
		public void GetAchStatusText_ShowsOnlyForPaySimpleAchStatuses() {
			StringAssert.EndsWith(PaymentWindowRules.GetAchStatusText(PaymentStatus.PaySimpleAchPosted),"Posted");
			StringAssert.EndsWith(PaymentWindowRules.GetAchStatusText(PaymentStatus.PaySimpleAchSettled),"Settled");
			StringAssert.EndsWith(PaymentWindowRules.GetAchStatusText(PaymentStatus.PaySimpleAchFailed),"Failed");
			StringAssert.StartsWith(PaymentWindowRules.GetAchStatusText(PaymentStatus.PaySimpleAchPosted),"ACH status");
			Assert.AreEqual("",PaymentWindowRules.GetAchStatusText(PaymentStatus.None));
		}

		[TestMethod]
		public void CanCheckAchStatus_OnlySavedPostedOrSettledAchPaymentsWithAnId() {
			Payment payment=new Payment();
			payment.PayNum=12;
			payment.ExternalId="987654";
			payment.PaymentStatus=PaymentStatus.PaySimpleAchPosted;
			Assert.IsTrue(PaymentWindowRules.CanCheckAchStatus(payment));
			payment.PaymentStatus=PaymentStatus.PaySimpleAchSettled;
			Assert.IsTrue(PaymentWindowRules.CanCheckAchStatus(payment),"A settled payment can still be returned.");
			payment.PaymentStatus=PaymentStatus.PaySimpleAchFailed;
			Assert.IsFalse(PaymentWindowRules.CanCheckAchStatus(payment),"A failed payment is final.");
			payment.PaymentStatus=PaymentStatus.None;
			Assert.IsFalse(PaymentWindowRules.CanCheckAchStatus(payment));
			payment.PaymentStatus=PaymentStatus.PaySimpleAchPosted;
			payment.ExternalId="";
			Assert.IsFalse(PaymentWindowRules.CanCheckAchStatus(payment),"Nothing to look up without the PaySimple payment ID.");
			payment.ExternalId="987654";
			payment.PayNum=0;
			Assert.IsFalse(PaymentWindowRules.CanCheckAchStatus(payment),"The check reads the saved payment.");
			Assert.IsFalse(PaymentWindowRules.CanCheckAchStatus(null));
		}

		[TestMethod]
		public void GetAppendedNoteLines_ReturnsOnlyWhatTheCheckAdded() {
			Assert.AreEqual("PaySimple ACH settled 2026-10-21",
				PaymentWindowRules.GetAppendedNoteLines("Sale note","Sale note\r\nPaySimple ACH settled 2026-10-21"));
			Assert.AreEqual("PaySimple ACH settled 2026-10-21",PaymentWindowRules.GetAppendedNoteLines("","PaySimple ACH settled 2026-10-21"));
			Assert.AreEqual("",PaymentWindowRules.GetAppendedNoteLines("Sale note","Sale note"),"Nothing appended.");
			Assert.AreEqual("",PaymentWindowRules.GetAppendedNoteLines("Sale note","Someone rewrote the note"),"Not an append: nothing to carry over.");
			Assert.AreEqual("",PaymentWindowRules.GetAppendedNoteLines(null,null));
		}

		[TestMethod]
		public void IsPaySimpleAchSource_OnlyBankAccountSources() {
			Assert.IsTrue(RecurringChargerator.IsPaySimpleAchSource(CreditCardSource.PaySimpleACH));
			Assert.IsTrue(RecurringChargerator.IsPaySimpleAchSource(CreditCardSource.PaySimplePaymentPortalACH));
			Assert.IsFalse(RecurringChargerator.IsPaySimpleAchSource(CreditCardSource.PaySimple));
			Assert.IsFalse(RecurringChargerator.IsPaySimpleAchSource(CreditCardSource.PayConnect));
		}
		#endregion ACH status

		#region PayConnect 2 void
		private static PayConnect2.PayConnect2Response MakeVoidResponse() {
			PayConnect2.PayConnect2Response response=new PayConnect2.PayConnect2Response();
			response.ResponseType=PayConnect2.ResponseType.Void;
			response.Outcome=PayConnect2.PayConnectRequestOutcome.Response;
			response.VoidResponse=new PayConnect2.VoidResponse();
			response.VoidResponse.Status="Voided";
			response.VoidResponse.ReferenceId=REF_NUM;
			response.VoidResponse.Amount=1250;
			return response;
		}

		private static PayConnect2.PayConnect2Response MakeErrorResponse(PayConnect2.PayConnectRequestOutcome outcome,string message) {
			PayConnect2.PayConnect2Response response=new PayConnect2.PayConnect2Response();
			response.ResponseType=PayConnect2.ResponseType.Error;
			response.Outcome=outcome;
			response.ErrorResponse=new PayConnect2.ErrorResponse();
			response.ErrorResponse.Message=message;
			return response;
		}

		private static PayConnect2.PayConnect2Response MakeStatusResponse(string status) {
			PayConnect2.PayConnect2Response response=new PayConnect2.PayConnect2Response();
			response.ResponseType=PayConnect2.ResponseType.GetStatus;
			response.Outcome=PayConnect2.PayConnectRequestOutcome.Response;
			response.GetStatusResponse=new PayConnect2.GetStatusResponse();
			response.GetStatusResponse.Status=status;
			response.GetStatusResponse.ReferenceId=REF_NUM;
			response.GetStatusResponse.Amount=1250;
			return response;
		}

		[TestMethod]
		public void ResolveVoid_AnsweredVoid_IsVoidedWithoutAStatusCall() {
			bool wasStatusRead=false;
			PayConnect2VoidResult result=PaymentWindowRules.ResolveVoid(MakeVoidResponse(),REF_NUM,() => {
				wasStatusRead=true;
				return null;
			});
			Assert.AreEqual(PayConnect2VoidKind.Voided,result.Kind);
			Assert.IsTrue(result.IsVoided);
			Assert.AreEqual("0",result.PayConnectResponse.StatusCode);
			Assert.AreEqual(REF_NUM,result.PayConnectResponse.RefNumber);
			Assert.AreEqual(12.50m,result.PayConnectResponse.Amount);
			Assert.IsFalse(wasStatusRead,"The void answered; no second call.");
		}

		[TestMethod]
		public void ResolveVoid_LostAnswer_StatusVoided_CountsAsVoided() {
			PayConnect2VoidResult result=PaymentWindowRules.ResolveVoid(
				MakeErrorResponse(PayConnect2.PayConnectRequestOutcome.SentNoResponse,"PayConnect did not answer."),REF_NUM,
				() => MakeStatusResponse("Voided"));
			Assert.AreEqual(PayConnect2VoidKind.Voided,result.Kind);
			Assert.AreEqual("0",result.PayConnectResponse.StatusCode);
			Assert.AreEqual(PayConnectResponse.TransactionType.Void,result.PayConnectResponse.TransType);
		}

		[TestMethod]
		public void ResolveVoid_UnreadableAnswer_StatusStillProcessed_IsUnknown() {
			PayConnect2VoidResult result=PaymentWindowRules.ResolveVoid(
				MakeErrorResponse(PayConnect2.PayConnectRequestOutcome.ResponseUnreadable,"Unreadable."),REF_NUM,
				() => MakeStatusResponse("Processed"));
			Assert.AreEqual(PayConnect2VoidKind.Unknown,result.Kind);
			Assert.IsNull(result.PayConnectResponse,"No void payment may be recorded for an unknown outcome.");
			StringAssert.Contains(result.Message,REF_NUM);
		}

		[TestMethod]
		public void ResolveVoid_LostAnswer_StatusCallFails_IsUnknown() {
			PayConnect2VoidResult result=PaymentWindowRules.ResolveVoid(
				MakeErrorResponse(PayConnect2.PayConnectRequestOutcome.SentNoResponse,"PayConnect did not answer."),REF_NUM,
				() => throw new WebException("timed out"));
			Assert.AreEqual(PayConnect2VoidKind.Unknown,result.Kind);
			PayConnect2VoidResult resultErrorStatus=PaymentWindowRules.ResolveVoid(
				MakeErrorResponse(PayConnect2.PayConnectRequestOutcome.SentNoResponse,"PayConnect did not answer."),REF_NUM,
				() => MakeErrorResponse(PayConnect2.PayConnectRequestOutcome.NotSent,"Could not reach PayConnect."));
			Assert.AreEqual(PayConnect2VoidKind.Unknown,resultErrorStatus.Kind);
		}

		[TestMethod]
		public void ResolveVoid_NotSent_NothingVoidedAndNoStatusCall() {
			bool wasStatusRead=false;
			PayConnect2VoidResult result=PaymentWindowRules.ResolveVoid(
				MakeErrorResponse(PayConnect2.PayConnectRequestOutcome.NotSent,"PayConnect is not set up for this clinic."),REF_NUM,() => {
					wasStatusRead=true;
					return MakeStatusResponse("Voided");
				});
			Assert.AreEqual(PayConnect2VoidKind.NotVoided,result.Kind);
			StringAssert.Contains(result.Message,"Nothing was voided");
			StringAssert.Contains(result.Message,"not set up");
			Assert.IsFalse(wasStatusRead);
		}

		[TestMethod]
		public void ResolveVoid_Refused_IsNotVoidedWithTheVendorsMessage() {
			PayConnect2VoidResult result=PaymentWindowRules.ResolveVoid(
				MakeErrorResponse(PayConnect2.PayConnectRequestOutcome.Response,"Transaction already settled."),REF_NUM,() => null);
			Assert.AreEqual(PayConnect2VoidKind.NotVoided,result.Kind);
			Assert.AreEqual("Transaction already settled.",result.Message);
			Assert.AreNotEqual("0",result.PayConnectResponse.StatusCode);
		}

		[TestMethod]
		public void ResolveVoid_NullResponse_IsUnknown() {
			Assert.AreEqual(PayConnect2VoidKind.Unknown,PaymentWindowRules.ResolveVoid(null,REF_NUM,() => null).Kind);
		}
		#endregion PayConnect 2 void

		#region PayConnect 2 errors in recurring charges
		[TestMethod]
		public void GetPayConnect2ErrorText_ValidationListsEachField() {
			PayConnect2.PayConnect2Response response=PayConnect2.ParseHttpResponse(PayConnect2.ApiRoute.CreateTransaction,HttpStatusCode.BadRequest,
				"{\"errorType\":\"VALIDATION\",\"error\":[{\"param\":\"expiration\",\"message\":\"must be MMyy\",\"value\":\"4111111111111111\"},"
				+"{\"param\":\"amount\",\"message\":\"must be positive\"}]}");
			string text=PaymentWindowRules.GetPayConnect2ErrorText(response);
			StringAssert.Contains(text,"expiration: must be MMyy");
			StringAssert.Contains(text,"amount: must be positive");
			Assert.IsFalse(text.Contains("4111111111111111"),"The rejected value can be card data and is never shown.");
			Assert.IsFalse(PaymentWindowRules.IsPayConnect2OutcomeUnknown(response),"A validation error is a definite answer.");
		}

		[TestMethod]
		public void GetPayConnect2ErrorText_FallsBackToTheRawErrorThenAGenericLine() {
			PayConnect2.PayConnect2Response response=MakeErrorResponse(PayConnect2.PayConnectRequestOutcome.Response,"");
			response.ErrorResponse.Error="Card declined";
			Assert.AreEqual("Card declined",PaymentWindowRules.GetPayConnect2ErrorText(response));
			response.ErrorResponse.Error=null;
			StringAssert.Contains(PaymentWindowRules.GetPayConnect2ErrorText(response),"unable to parse");
			response.ErrorResponse=null;
			Assert.AreNotEqual("",PaymentWindowRules.GetPayConnect2ErrorText(response));
			Assert.AreNotEqual("",PaymentWindowRules.GetPayConnect2ErrorText(null));
		}

		[TestMethod]
		public void IsPayConnect2OutcomeUnknown_OnlyForSentRequestsWithoutAReadableAnswer() {
			Assert.IsTrue(PaymentWindowRules.IsPayConnect2OutcomeUnknown(MakeErrorResponse(PayConnect2.PayConnectRequestOutcome.SentNoResponse,"x")));
			Assert.IsTrue(PaymentWindowRules.IsPayConnect2OutcomeUnknown(MakeErrorResponse(PayConnect2.PayConnectRequestOutcome.ResponseUnreadable,"x")));
			Assert.IsFalse(PaymentWindowRules.IsPayConnect2OutcomeUnknown(MakeErrorResponse(PayConnect2.PayConnectRequestOutcome.NotSent,"x")));
			Assert.IsFalse(PaymentWindowRules.IsPayConnect2OutcomeUnknown(MakeErrorResponse(PayConnect2.PayConnectRequestOutcome.Response,"x")));
			Assert.IsFalse(PaymentWindowRules.IsPayConnect2OutcomeUnknown(MakeStatusResponse("Processed")),"A reconciled approval is known.");
			Assert.IsFalse(PaymentWindowRules.IsPayConnect2OutcomeUnknown(null));
		}
		#endregion PayConnect 2 errors in recurring charges

		#region CareCredit and payment types
		[TestMethod]
		public void ShowCareCredit_HiddenUnlessEnabledAndComplete() {
			CareCredit.FuncIsEnabled=() => false;
			CareCredit.FuncGetConfig=() => throw new InvalidOperationException("Settings are not read while the program is off.");
			Assert.IsFalse(PaymentWindowRules.ShowCareCredit(),"Disabled: no button (and no sign-up page).");
			CareCredit.FuncIsEnabled=() => true;
			CareCredit.FuncGetConfig=() => null;
			Assert.IsFalse(PaymentWindowRules.ShowCareCredit(),"Enabled but not set up: no button.");
			CareCredit.FuncGetConfig=() => CareCreditTests.CareCreditTestData.CreateConfig();
			Assert.IsTrue(PaymentWindowRules.ShowCareCredit());
		}

		[TestMethod]
		public void IsPaymentTypeValid_MatchesADefNum() {
			List<Def> listDefs=new List<Def>();
			listDefs.Add(new Def() { DefNum=70 });
			Assert.IsTrue(PaymentWindowRules.IsPaymentTypeValid("70",listDefs));
			Assert.IsTrue(PaymentWindowRules.IsPaymentTypeValid(" 70 ",listDefs));
			Assert.IsFalse(PaymentWindowRules.IsPaymentTypeValid("71",listDefs));
			Assert.IsFalse(PaymentWindowRules.IsPaymentTypeValid("",listDefs));
			Assert.IsFalse(PaymentWindowRules.IsPaymentTypeValid("70",null));
		}
		#endregion CareCredit and payment types
	}
}
