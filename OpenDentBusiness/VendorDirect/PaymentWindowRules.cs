// Vendor-direct integrations: the rules the payment windows (FormPayment, FormClaimPayEdit, FormCreditCardManage) follow for the card processors
// (docs/specs/vendor-direct/README.md, package PAY-UI). A processor's button shows only when its program link is enabled and the office has entered
// its own credentials for the clinic; there is no "show every processor" fallback and nothing is shown to promote a processor.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;
using OpenDentBusiness.Bridges;

namespace OpenDentBusiness.VendorDirect {
	///<summary>Which card-processor buttons a payment window shows, and a few results those windows act on. Reads the caches only (no vendor call),
	///except VoidPayConnect2, and never throws from the Show methods.</summary>
	public static class PaymentWindowRules {
		///<summary>The PayConnect and X-Charge row that holds the payment type DefNum, per clinic.</summary>
		public const string PayConnectPaymentTypeDesc="PaymentType";

		///<summary>True if payTypeDefNum is the DefNum of one of the payment types in listDefsPaymentType.</summary>
		public static bool IsPaymentTypeValid(string payTypeDefNum,List<Def> listDefsPaymentType) {
			if(listDefsPaymentType==null || string.IsNullOrWhiteSpace(payTypeDefNum)) {
				return false;
			}
			return listDefsPaymentType.Any(x => x.DefNum.ToString()==payTypeDefNum.Trim());
		}

		///<summary>Without clinics every lookup uses the practice rows (ClinicNum 0), whatever clinic a payment still carries.</summary>
		private static long NormalizeClinicNum(long clinicNum) {
			if(!PrefC.HasClinicsEnabled || clinicNum<0) {
				return 0;
			}
			return clinicNum;
		}

		///<summary>True if the PayConnect button shows for clinicNum. Version 2 needs PayConnect2.IsConfigured (enabled, the office's API key and the
		///clinic's Secret), with clinics on or off. Version 1 keeps its old rule until PayConnect 1.0 is removed: with clinics on it needs a user name
		///and password. With clinics on, both versions also need a valid payment type. listDefsPaymentType null skips the payment-type check (adding
		///a card, where no payment is made).</summary>
		public static bool ShowPayConnect(long clinicNum,List<Def> listDefsPaymentType) {
			try {
				Program program=Programs.GetCur(ProgramName.PayConnect);
				if(program==null || !program.Enabled) {
					return false;
				}
				clinicNum=NormalizeClinicNum(clinicNum);
				string programVersion=ProgramProperties.GetValOrDefault(program.ProgramNum,PayConnect.ProgramProperties.ProgramVersion,clinicNum);
				bool hasCredentials;
				if(programVersion=="2") {
					hasCredentials=PayConnect2.IsConfigured(clinicNum);
				}
				else if(programVersion=="1") {
					hasCredentials=true;
					if(PrefC.HasClinicsEnabled) {
						string password=Secrets.TryDecrypt(ProgramProperties.GetValOrDefault(program.ProgramNum,"Password",clinicNum));
						hasCredentials=!string.IsNullOrEmpty(ProgramProperties.GetValOrDefault(program.ProgramNum,"Username",clinicNum))
							&& !string.IsNullOrEmpty(password);
					}
				}
				else {
					return false;
				}
				if(!hasCredentials) {
					return false;
				}
				if(listDefsPaymentType==null || !PrefC.HasClinicsEnabled) {
					return true;
				}
				return IsPaymentTypeValid(ProgramProperties.GetValOrDefault(program.ProgramNum,PayConnectPaymentTypeDesc,clinicNum),listDefsPaymentType);
			}
			catch(Exception ex) {
				ex.DoNothing();
				return false;
			}
		}

		///<summary>True if the PaySimple button shows for clinicNum: enabled, and the clinic (or the practice default) has an API user name and a
		///readable key. With clinics on it also needs a valid card payment type. listDefsPaymentType null skips the payment-type check.</summary>
		public static bool ShowPaySimple(long clinicNum,List<Def> listDefsPaymentType) {
			try {
				Program program=Programs.GetCur(ProgramName.PaySimple);
				if(program==null || !program.Enabled) {
					return false;
				}
				clinicNum=NormalizeClinicNum(clinicNum);
				if(!PaySimple.HasCredentials(clinicNum)) {
					return false;
				}
				if(listDefsPaymentType==null || !PrefC.HasClinicsEnabled) {
					return true;
				}
				return IsPaymentTypeValid(ProgramProperties.GetValOrDefault(program.ProgramNum,PaySimple.PropertyDescs.PaySimplePayTypeCC,clinicNum),
					listDefsPaymentType);
			}
			catch(Exception ex) {
				ex.DoNothing();
				return false;
			}
		}

		///<summary>True if the EdgeExpress button shows for clinicNum: enabled, and the clinic's X-Web ID, Terminal ID, Auth Key and payment type are
		///set and well formed (the same check every EdgeExpress request makes), with clinics on or off.</summary>
		public static bool ShowEdgeExpress(long clinicNum) {
			try {
				if(!Programs.IsEnabled(ProgramName.EdgeExpress)) {
					return false;
				}
				ProgramProperties.GetXWebCreds(NormalizeClinicNum(clinicNum),out WebTypes.Shared.XWeb.WebPaymentProperties webPaymentProperties);
				return webPaymentProperties!=null && !webPaymentProperties.IsXWeb;
			}
			catch(Exception ex) {
				ex.DoNothing();
				return false;
			}
		}

		///<summary>True if the CareCredit button shows: the program link is enabled and its settings are complete (CareCredit.IsUsable). A disabled
		///CareCredit shows nothing at all: no button, no sign-up page.</summary>
		public static bool ShowCareCredit() {
			try {
				return CareCredit.IsUsable();
			}
			catch(Exception ex) {
				ex.DoNothing();
				return false;
			}
		}

		///<summary>The read-only text shown next to the PaySimple button for an ACH payment ("ACH status: Posted", "Settled" or "Failed"), or "" when
		///the payment isn't a tracked PaySimple ACH payment.</summary>
		public static string GetAchStatusText(PaymentStatus paymentStatus) {
			string status;
			switch(paymentStatus) {
				case PaymentStatus.PaySimpleAchPosted:
					status=Lans.g("PaySimple","Posted");
					break;
				case PaymentStatus.PaySimpleAchSettled:
					status=Lans.g("PaySimple","Settled");
					break;
				case PaymentStatus.PaySimpleAchFailed:
					status=Lans.g("PaySimple","Failed");
					break;
				default:
					return "";
			}
			return Lans.g("PaySimple","ACH status")+": "+status;
		}

		///<summary>True if the payment window offers "Check ACH status" for this payment: a saved PaySimple ACH payment that is still Posted, or
		///Settled (a settled payment can still be returned).</summary>
		public static bool CanCheckAchStatus(Payment payment) {
			return payment!=null && payment.PayNum>0 && !string.IsNullOrWhiteSpace(payment.ExternalId)
				&& payment.PaymentStatus.In(PaymentStatus.PaySimpleAchPosted,PaymentStatus.PaySimpleAchSettled);
		}

		///<summary>The PayNote lines a background or manual ACH status check appended in the database since the window read the payment: what
		///payNoteDb has beyond payNoteWindow, without the leading line break. "" if nothing was appended or the note was changed in some other way.
		///</summary>
		public static string GetAppendedNoteLines(string payNoteWindow,string payNoteDb) {
			payNoteWindow=payNoteWindow??"";
			payNoteDb=payNoteDb??"";
			if(payNoteDb.Length<=payNoteWindow.Length || !payNoteDb.StartsWith(payNoteWindow,StringComparison.Ordinal)) {
				return "";
			}
			return payNoteDb.Substring(payNoteWindow.Length).TrimStart('\r','\n');
		}

		///<summary>The text added at the top of a PayConnect 2 payment note and receipt while "Use sandbox" is on, so a test row can't pass for a real
		///one.</summary>
		public const string PayConnectSandboxMarker="SANDBOX TEST - NOT A REAL CHARGE";

		///<summary>The text for a PayConnect 2 error response: its message for the user (a validation error lists each rejected field as
		///"param: message"), else the vendor's raw error, else a generic line. Never contains keys or card data. Never throws.</summary>
		public static string GetPayConnect2ErrorText(PayConnect2.PayConnect2Response response) {
			PayConnect2.ErrorResponse errorResponse=response?.ErrorResponse;
			if(errorResponse==null) {
				return Lans.g("PayConnect","Error returned from PayConnect.");
			}
			if(!string.IsNullOrWhiteSpace(errorResponse.Message)) {
				return errorResponse.Message.Trim();
			}
			List<string> listLines=(errorResponse.ListValidationErrors??new List<PayConnect2.ValidationErrorBody>())
				.Select(x => string.IsNullOrWhiteSpace(x.Param) ? (x.Message??"") : x.Param+": "+(x.Message??""))
				.Where(x => !string.IsNullOrWhiteSpace(x))
				.ToList();
			if(listLines.Count>0) {
				return Lans.g("PayConnect","PayConnect did not accept the request.")+" "+string.Join("; ",listLines);
			}
			string error="";
			try {
				error=errorResponse.Error?.ToString()??"";
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
			if(!string.IsNullOrWhiteSpace(error)) {
				return error.Trim();
			}
			return Lans.g("PayConnect","Error returned from PayConnect: unable to parse error message");
		}

		///<summary>True if a PayConnect 2 request was sent but its outcome can't be told: no answer, or an answer that couldn't be read (and, for a
		///charge, the transaction list didn't show it either). The card may or may not have been charged; never charge it again automatically.</summary>
		public static bool IsPayConnect2OutcomeUnknown(PayConnect2.PayConnect2Response response) {
			return response!=null && response.ResponseType==PayConnect2.ResponseType.Error
				&& (response.Outcome==PayConnect2.PayConnectRequestOutcome.SentNoResponse
					|| response.Outcome==PayConnect2.PayConnectRequestOutcome.ResponseUnreadable);
		}

		///<summary>Voids a PayConnect 2 transaction by reference number for the clinic. A PUT is never retried. When no answer arrives (or it can't be
		///read), the transaction's status is read once: "Voided" counts as voided; anything else means the void's outcome is unknown, and the caller
		///must not record a void payment.</summary>
		public static PayConnect2VoidResult VoidPayConnect2(string refNum,long clinicNum) {
			PayConnect2.VoidReferenceIDRequest voidReferenceIDRequest=new PayConnect2.VoidReferenceIDRequest();
			voidReferenceIDRequest.ReferenceId=refNum;
			PayConnect2.PayConnect2Response response=PayConnect2.PutVoidWithReferenceID(voidReferenceIDRequest,clinicNum);
			return ResolveVoid(response,refNum,() => PayConnect2.GetTransactionStatus(clinicNum,refNum));
		}

		///<summary>Decides what a PayConnect 2 void response means. funcGetStatus reads the transaction's status; it is called only when the void got no
		///readable answer. Public for tests.</summary>
		public static PayConnect2VoidResult ResolveVoid(PayConnect2.PayConnect2Response response,string refNum,
			Func<PayConnect2.PayConnect2Response> funcGetStatus)
		{
			PayConnect2VoidResult result=new PayConnect2VoidResult();
			if(response==null) {
				result.Kind=PayConnect2VoidKind.Unknown;
				result.Message=GetVoidUnknownMessage(refNum);
				return result;
			}
			if(response.ResponseType==PayConnect2.ResponseType.Void && response.VoidResponse!=null) {
				result.Kind=PayConnect2VoidKind.Voided;
				result.PayConnectResponse=PayConnect2.ApiResponseToPayConnectResponse(response);
				return result;
			}
			if(response.Outcome==PayConnect2.PayConnectRequestOutcome.NotSent) {
				result.Kind=PayConnect2VoidKind.NotVoided;
				result.Message=Lans.g("PayConnect","Could not reach PayConnect. Nothing was voided.");
				string detail=response.ErrorResponse?.Message;
				if(!string.IsNullOrWhiteSpace(detail)) {
					result.Message+="\r\n"+detail;
				}
				return result;
			}
			if(response.Outcome==PayConnect2.PayConnectRequestOutcome.SentNoResponse
				|| response.Outcome==PayConnect2.PayConnectRequestOutcome.ResponseUnreadable)
			{
				PayConnect2.PayConnect2Response responseStatus=null;
				try {
					responseStatus=funcGetStatus?.Invoke();
				}
				catch(Exception ex) {
					ex.DoNothing();
				}
				if(responseStatus!=null && responseStatus.ResponseType==PayConnect2.ResponseType.GetStatus && responseStatus.GetStatusResponse!=null
					&& string.Equals((responseStatus.GetStatusResponse.Status??"").Trim(),"Voided",StringComparison.OrdinalIgnoreCase))
				{
					result.Kind=PayConnect2VoidKind.Voided;
					result.PayConnectResponse=PayConnect2.ApiResponseToPayConnectResponse(responseStatus);
					result.PayConnectResponse.TransType=PayConnectResponse.TransactionType.Void;
					return result;
				}
				result.Kind=PayConnect2VoidKind.Unknown;
				result.Message=GetVoidUnknownMessage(refNum);
				return result;
			}
			result.Kind=PayConnect2VoidKind.NotVoided;
			result.PayConnectResponse=PayConnect2.ApiResponseToPayConnectResponse(response);
			result.Message=response.ErrorResponse?.Message;
			if(string.IsNullOrWhiteSpace(result.Message)) {
				result.Message=Lans.g("PayConnect","PayConnect did not void the transaction.");
			}
			return result;
		}

		private static string GetVoidUnknownMessage(string refNum) {
			return Lans.g("PayConnect","PayConnect did not answer the void, and the transaction does not show as voided. The void may or may not have gone "
				+"through. Look for reference number")+" "+(refNum??"")+" "+Lans.g("PayConnect","in the PayConnect portal before trying again.");
		}
	}

	///<summary>What a PayConnect 2 void did.</summary>
	public enum PayConnect2VoidKind {
		///<summary>PayConnect voided it (its answer, or its status after a lost answer, says so).</summary>
		Voided,
		///<summary>PayConnect refused it, or the request never left this computer. Nothing was voided.</summary>
		NotVoided,
		///<summary>The request was sent but the outcome can't be told. Don't record a void payment; check the PayConnect portal.</summary>
		Unknown,
	}

	///<summary>The result of PaymentWindowRules.VoidPayConnect2.</summary>
	public class PayConnect2VoidResult {
		public PayConnect2VoidKind Kind;
		///<summary>The void's details for the receipt and the note, when Voided (and when PayConnect refused it). Null otherwise.</summary>
		public PayConnectResponse PayConnectResponse;
		///<summary>Text for the user when not Voided. Never contains keys or card data.</summary>
		public string Message="";

		public bool IsVoided {
			get {
				return Kind==PayConnect2VoidKind.Voided;
			}
		}
	}
}
