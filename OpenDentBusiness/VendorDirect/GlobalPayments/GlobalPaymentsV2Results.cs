// Global Payments V2 (Open Dental 25.4, v25_4-payments-globalpayments-v2): the outcome of one card transaction and how it is read from Global
// Payments' answers. Written from Global Payments' public developer documentation (developer.globalpayments.com: the Cloud Integration device
// commands and the Transactions API). The public samples show only part of each answer, so the reading is lenient and every rule that goes beyond
// them is marked INFERRED in docs/port-notes/V-GP.md.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CodeBase;
using Newtonsoft.Json.Linq;

namespace OpenDentBusiness.VendorDirect.GlobalPayments {
	///<summary>What the user asked for in the Global Payments V2 window.</summary>
	public enum GpV2TransType {
		///<summary>A charge: on a terminal (device command Authorize), or on a saved card (Transactions API, type SALE).</summary>
		Sale,
		///<summary>Money back to a card: on a terminal (device command Refund), or to a saved card (Transactions API, type REFUND).</summary>
		Refund,
		///<summary>Reverses an earlier transaction by its Global Payments transaction ID (Transactions API, reversal). No card or terminal needed.</summary>
		Void,
	}

	///<summary>How a transaction ended, as far as this program knows.</summary>
	public enum GpV2Outcome {
		///<summary>Global Payments approved it. Money moved.</summary>
		Approved,
		///<summary>Global Payments or the card issuer declined it. No money moved.</summary>
		Declined,
		///<summary>Global Payments refused the request (bad settings, a terminal that isn't registered, ...). No money moved.</summary>
		Failed,
		///<summary>The user stopped waiting and the terminal had not finished. The terminal may still finish it.</summary>
		Cancelled,
		///<summary>No final answer arrived in time. The terminal may still finish it.</summary>
		TimedOut,
		///<summary>The request may or may not have reached Global Payments (a network failure while sending a money request). Never retried.</summary>
		Unknown,
	}

	///<summary>The outcome of one Global Payments V2 transaction, normalized from Global Payments' answer. PaymentMethodId is the card token: it is
	///stored on the saved card only and never written to notes, receipts or logs.</summary>
	public sealed class GlobalPaymentsV2Result {
		public GpV2TransType TransType;
		public GpV2Outcome Outcome=GpV2Outcome.Unknown;
		///<summary>The amount, positive, in dollars: what Global Payments approved, else what was asked.</summary>
		public decimal Amount;
		///<summary>Global Payments' transaction ID (TRN_...), used to void. Not a secret.</summary>
		public string TransactionId="";
		///<summary>The device command ID (DVC_...) of a terminal transaction, for support calls when a terminal didn't answer. Not a secret.</summary>
		public string DeviceCommandId="";
		///<summary>Global Payments' status text, e.g. CAPTURED or DECLINED.</summary>
		public string Status="";
		///<summary>The issuer's or gateway's result code (payment_method.result or action.result_code).</summary>
		public string ResultCode="";
		///<summary>A message safe to show: Global Payments' own description of the result or error, or this program's.</summary>
		public string Message="";
		public string CardBrand="";
		///<summary>The last four digits only.</summary>
		public string CardLast4="";
		public string AuthCode="";
		public string EntryMode="";
		///<summary>A reusable payment method (PMT_...) Global Payments returned, or "". The card token: never shown or logged.</summary>
		public string PaymentMethodId="";
		public int ExpMonth;
		///<summary>Four digits, or 0.</summary>
		public int ExpYear;
		public string TerminalName="";
		public string TerminalId="";
		///<summary>True when the sandbox was used: nothing was really charged.</summary>
		public bool IsSandbox;

		public bool IsApproved {
			get {
				return Outcome==GpV2Outcome.Approved;
			}
		}

		///<summary>True when money may have moved without this program knowing for sure, so the user must check the terminal or the merchant portal.
		///</summary>
		public bool IsInDoubt {
			get {
				return Outcome.In(GpV2Outcome.Cancelled,GpV2Outcome.TimedOut,GpV2Outcome.Unknown);
			}
		}

		///<summary>The masked card number as cards are stored here: twelve X's and the last four digits, or "" without them.</summary>
		public string GetMaskedCardNumber() {
			if(string.IsNullOrEmpty(CardLast4)) {
				return "";
			}
			return "XXXXXXXXXXXX"+CardLast4;
		}
	}

	///<summary>Reads Global Payments' JSON answers. Every reader is lenient: a missing or differently shaped part reads as blank, never throws.
	///</summary>
	public static class GpV2Json {
		///<summary>Device command statuses that mean the terminal hasn't finished. INFERRED: the public samples show INITIATED (just sent) and
		///COMPLETED (finished); the others are the usual names for a command that is still waiting.</summary>
		private static readonly HashSet<string> _hashSetPendingStatuses=new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
			"","INITIATED","PENDING","IN_PROGRESS","PROCESSING","QUEUED","SENT","WAITING","RECEIVED" };
		///<summary>Transaction statuses that mean the money moved (the Transactions API status list, plus the plain words a device answer may use).
		///</summary>
		private static readonly HashSet<string> _hashSetApprovedStatuses=new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
			"CAPTURED","PREAUTHORIZED","FUNDED","APPROVED","SUCCESS","SUCCESSFUL" };
		private static readonly HashSet<string> _hashSetDeclinedStatuses=new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
			"DECLINED","REJECTED" };
		private static readonly HashSet<string> _hashSetFailedStatuses=new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
			"FAILED","ERROR","FOR_REVIEW" };
		private static readonly HashSet<string> _hashSetCancelledStatuses=new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
			"CANCELLED","CANCELED","TIMEOUT","TIMED_OUT","EXPIRED","ABORTED" };

		///<summary>Parses body as a JSON object, or null.</summary>
		public static JObject TryParseObject(string body) {
			if(string.IsNullOrWhiteSpace(body)) {
				return null;
			}
			try {
				JToken jToken=JToken.Parse(body);
				return jToken as JObject;
			}
			catch(Exception ex) {
				ex.DoNothing();
				return null;
			}
		}

		///<summary>The string at a dotted path ("payment_method.card.brand"), or "".</summary>
		public static string GetString(JToken jToken,string path) {
			JToken jTokenValue=SelectPath(jToken,path);
			if(jTokenValue==null || jTokenValue.Type==JTokenType.Null || jTokenValue is JContainer) {
				return "";
			}
			return ((string)jTokenValue??"").Trim();
		}

		///<summary>The first of the dotted paths that has a value, or "".</summary>
		public static string GetFirstString(JToken jToken,params string[] arrayPaths) {
			for(int i=0;i<arrayPaths.Length;i++) {
				string value=GetString(jToken,arrayPaths[i]);
				if(value!="") {
					return value;
				}
			}
			return "";
		}

		private static JToken SelectPath(JToken jToken,string path) {
			JToken jTokenCur=jToken;
			foreach(string part in path.Split('.')) {
				if(!(jTokenCur is JObject jObject)) {
					return null;
				}
				//Property names are matched without regard to case, because the public samples aren't consistent.
				JProperty jProperty=jObject.Properties().FirstOrDefault(x => string.Equals(x.Name,part,StringComparison.OrdinalIgnoreCase));
				if(jProperty==null) {
					return null;
				}
				jTokenCur=jProperty.Value;
			}
			return jTokenCur;
		}

		///<summary>The transaction in a device command answer: the "transaction" object, or the first element when it is a list, or null.</summary>
		public static JObject GetDeviceTransaction(JObject jObjectCommand) {
			JToken jToken=SelectPath(jObjectCommand,"transaction");
			if(jToken is JObject jObject) {
				return jObject;
			}
			if(jToken is JArray jArray) {
				return jArray.OfType<JObject>().FirstOrDefault();
			}
			return null;
		}

		///<summary>True while a device command answer says the terminal hasn't finished: a pending command status and no transaction yet.</summary>
		public static bool IsDeviceCommandPending(JObject jObjectCommand) {
			if(jObjectCommand==null) {
				return true;
			}
			string status=GetString(jObjectCommand,"status");
			if(!_hashSetPendingStatuses.Contains(status)) {
				return false;
			}
			JObject jObjectTransaction=GetDeviceTransaction(jObjectCommand);
			//A transaction with an ID means the terminal answered, whatever the command status still says.
			return jObjectTransaction==null || GetString(jObjectTransaction,"id")=="";
		}

		///<summary>Amount in dollars from a minor-units string ("1234" is $12.34), or null.</summary>
		public static decimal? ParseMinorUnits(string value) {
			if(string.IsNullOrWhiteSpace(value)) {
				return null;
			}
			long cents;
			if(long.TryParse(value.Trim(),NumberStyles.Integer,CultureInfo.InvariantCulture,out cents)) {
				return cents/100m;
			}
			return null;
		}

		///<summary>Reads a finished transaction: a Transactions API answer, or the transaction of a device command answer (jObjectOuter is then the
		///command, for its action result and status). Fills the card, codes and IDs and decides the outcome.</summary>
		public static void ReadTransaction(GlobalPaymentsV2Result result,JObject jObjectTransaction,JObject jObjectOuter=null) {
			if(jObjectTransaction==null) {
				jObjectTransaction=new JObject();
			}
			result.TransactionId=GetString(jObjectTransaction,"id");
			string statusTransaction=GetString(jObjectTransaction,"status");
			string statusOuter=(jObjectOuter==null) ? "" : GetString(jObjectOuter,"status");
			result.Status=(statusTransaction!="") ? statusTransaction : statusOuter;
			//The card details sit under the transaction's payment_method; a device answer may also put them next to the transaction.
			JToken jTokenPaymentMethod=SelectPath(jObjectTransaction,"payment_method");
			if(jTokenPaymentMethod==null && jObjectOuter!=null) {
				jTokenPaymentMethod=SelectPath(jObjectOuter,"payment_method");
			}
			result.PaymentMethodId=GetString(jTokenPaymentMethod,"id");
			if(!result.PaymentMethodId.StartsWith("PMT_",StringComparison.OrdinalIgnoreCase)) {
				//Only a stored payment method can be charged again. Anything else (blank in the public device sample) is not kept.
				result.PaymentMethodId="";
			}
			result.EntryMode=GetString(jTokenPaymentMethod,"entry_mode");
			result.CardBrand=GetFirstString(jTokenPaymentMethod,"card.brand","card.type");
			string last4=GetFirstString(jTokenPaymentMethod,"card.masked_number_last4","card.number_last4","card.last4");
			result.CardLast4=GetLast4(last4);
			result.AuthCode=GetFirstString(jTokenPaymentMethod,"card.authcode","card.auth_code","card.authorization_code");
			int expMonth;
			if(int.TryParse(GetString(jTokenPaymentMethod,"card.expiry_month"),out expMonth) && expMonth>=1 && expMonth<=12) {
				result.ExpMonth=expMonth;
			}
			int expYear;
			if(int.TryParse(GetString(jTokenPaymentMethod,"card.expiry_year"),out expYear) && expYear>0) {
				result.ExpYear=(expYear<100) ? 2000+expYear : expYear;
			}
			string resultPaymentMethod=GetString(jTokenPaymentMethod,"result");
			string resultAction=GetFirstString(jObjectTransaction,"action.result_code");
			if(resultAction=="" && jObjectOuter!=null) {
				resultAction=GetString(jObjectOuter,"action.result_code");
			}
			result.ResultCode=(resultPaymentMethod!="") ? resultPaymentMethod : resultAction;
			string message=GetFirstString(jTokenPaymentMethod,"message");
			if(message!="") {
				result.Message=message;
			}
			decimal? amount=ParseMinorUnits(GetString(jObjectTransaction,"amount"));
			if(amount.HasValue && amount.Value>0) {
				result.Amount=amount.Value;
			}
			result.Outcome=DecideOutcome(result.TransType,statusTransaction,statusOuter,resultPaymentMethod,resultAction,result.TransactionId);
		}

		///<summary>The outcome from the statuses and result codes. INFERRED beyond the published status lists: a transaction status decides first;
		///then payment_method.result ("00" approves, any other code declines); then action.result_code (SUCCESS approves, DECLINED declines). A void
		///is approved by REVERSED. A command that ended without a transaction (cancelled, timed out on the terminal) moved no money.</summary>
		public static GpV2Outcome DecideOutcome(GpV2TransType transType,string statusTransaction,string statusOuter,string resultPaymentMethod,
			string resultAction,string transactionId)
		{
			string status=(statusTransaction??"").Trim();
			if(transType==GpV2TransType.Void && status.Equals("REVERSED",StringComparison.OrdinalIgnoreCase)) {
				return GpV2Outcome.Approved;
			}
			if(_hashSetDeclinedStatuses.Contains(status)) {
				return GpV2Outcome.Declined;
			}
			if(_hashSetFailedStatuses.Contains(status)) {
				return GpV2Outcome.Failed;
			}
			if(_hashSetCancelledStatuses.Contains(status)) {
				return GpV2Outcome.Cancelled;
			}
			string codePaymentMethod=(resultPaymentMethod??"").Trim();
			string codeAction=(resultAction??"").Trim();
			if(codeAction.Equals("DECLINED",StringComparison.OrdinalIgnoreCase)) {
				return GpV2Outcome.Declined;
			}
			if(codePaymentMethod!="" && codePaymentMethod!="00") {
				return GpV2Outcome.Declined;
			}
			if(_hashSetApprovedStatuses.Contains(status)) {
				return GpV2Outcome.Approved;
			}
			if(string.IsNullOrWhiteSpace(transactionId)) {
				//No transaction: the terminal stopped (or the command failed) before a card was charged.
				string statusCommand=(statusOuter??"").Trim();
				if(_hashSetCancelledStatuses.Contains(statusCommand)) {
					return GpV2Outcome.Cancelled;
				}
				if(_hashSetDeclinedStatuses.Contains(statusCommand)) {
					return GpV2Outcome.Declined;
				}
				return GpV2Outcome.Failed;
			}
			if(codePaymentMethod=="00" || codeAction.Equals("SUCCESS",StringComparison.OrdinalIgnoreCase)) {
				return GpV2Outcome.Approved;
			}
			if(status=="" && (statusOuter??"").Trim().Equals("COMPLETED",StringComparison.OrdinalIgnoreCase)) {
				//A completed command with a transaction ID and no code saying otherwise. INFERRED from the public device sample.
				return GpV2Outcome.Approved;
			}
			return GpV2Outcome.Unknown;
		}

		///<summary>The last four digits in a masked number ("XXXX1234", "1234"), or "".</summary>
		public static string GetLast4(string value) {
			string digits=new string((value??"").Where(char.IsDigit).ToArray());
			if(digits.Length<4) {
				return "";
			}
			return digits.Substring(digits.Length-4);
		}

		///<summary>Global Payments' error description from an error body (detailed_error_description, else error_description, else error_code), or "".
		///</summary>
		public static string ReadErrorMessage(string body) {
			JObject jObject=TryParseObject(body);
			if(jObject==null) {
				return "";
			}
			string description=GetFirstString(jObject,"detailed_error_description","error_description","message");
			string code=GetFirstString(jObject,"detailed_error_code","error_code");
			if(description!="" && code!="") {
				return description+" ("+code+")";
			}
			return (description!="") ? description : code;
		}

		///<summary>Global Payments' error code from an error body (error_code), or "".</summary>
		public static string ReadErrorCode(string body) {
			return GetFirstString(TryParseObject(body),"error_code","detailed_error_code");
		}
	}
}
