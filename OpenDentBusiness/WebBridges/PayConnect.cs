using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenDentBusiness;
using System.ComponentModel;
using System.Text.RegularExpressions;
using CodeBase;
using System.IO;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http;
using OpenDentBusiness.WebTypes;
using OpenDentBusiness.PayConnectService;

namespace OpenDentBusiness {
	///<summary>PayConnect settings and receipts shared by PayConnect 2 (PayConnect2.cs). PayConnect 1.0 (the DentalXChange MerchantService SOAP
	///service, its REST patient-portal service and the VeriFone terminals run through DpsPos.dll) was removed: DentalXChange decommissioned it, and
	///it carried Open Dental's compiled-in client identity. A clinic whose "Program Version" row still says 1 counts as not set up until PayConnect
	///Setup is saved, which writes version 2.</summary>
	public class PayConnect {
		///<summary>The "Program Version" value for PayConnect 2, the only version this program runs.</summary>
		public const string Version2="2";

		///<summary>Shown where PayConnect 1.0 used to run (a clinic still set to version 1, for example in recurring charges).</summary>
		public static string GetVersion1RemovedText() {
			return Lans.g("PayConnect","PayConnect 1.0 is no longer supported. Enter the PayConnect 2.0 API key and the clinic's Secret in Setup, "
				+"Program Links, PayConnect, and save.");
		}

		///<summary>Shown when someone tries to void or refund a payment taken through PayConnect 1.0's patient portal. That needs the removed
		///PayConnect 1.0 service, so nothing is sent and nothing is recorded.</summary>
		public static string GetPortalVoidRemovedText() {
			return Lans.g("PayConnect","This payment was taken through the PayConnect 1.0 patient portal, which is no longer supported. Void or refund "
				+"it in the PayConnect merchant portal, then record the reversal here as a separate payment. Nothing was sent or changed.");
		}

		///<summary></summary>
		public static string CleanString(string str){
			return "";
		}

		///<summary>Return bool if value passed in is numeric only</summary>
		public static bool IsNumeric(string str) {
			if(str==null) {
				return false;
			}
			Regex objNotWholePattern=new Regex("[^0-9]");
			return !objNotWholePattern.IsMatch(str);
		}

		///<summary>Builds a receipt for a PayConnect 2 transaction (FormPayConnect2, recurring charges). A void prints negative amounts.</summary>
		public static string BuildReceiptString(PayConnectService.transType transType,string refNum,string patName,string cardNumber,
			string magData,string authCode,string statusDescription,List<string> messages,decimal amount,bool doShowSignatureLine,long clinicNum,string cardType="",decimal surchargeAmount=0,string cardHolder="")
		{
			string result="";
			cardNumber=cardNumber??""; //Prevents null reference exceptions when PayConnectPortal transactions don't have an associated card number
			int xmin=0;
			int xleft=xmin;
			int xright=15;
			int xmax=37;
			result+=Environment.NewLine;
			result+=CreditCardUtils.AddClinicToReceipt(clinicNum);
			//Print body
			result+="Date".PadRight(xright-xleft,'.')+DateTime.Now.ToString()+Environment.NewLine;
			result+=Environment.NewLine;
			result+="Trans Type".PadRight(xright-xleft,'.')+transType+Environment.NewLine;
			result+=Environment.NewLine;
			result+="Transaction #".PadRight(xright-xleft,'.')+refNum+Environment.NewLine;
			result+="Patient".PadRight(xright-xleft,'.')+patName+Environment.NewLine;
			if(!string.IsNullOrWhiteSpace(cardHolder)) {
				result+="Cardholder".PadRight(xright-xleft,'.')+cardHolder+Environment.NewLine;
			}
			result+="Account".PadRight(xright-xleft,'.');
			if(cardNumber.Length>4) {
				for(int i = 0;i<cardNumber.Length-4;i++) {
					result+="*";
				}
				result+=cardNumber.Substring(cardNumber.Length-4)+Environment.NewLine;//last 4 digits of card number only.
			}
			else {//Cardnumber is the last 4 digits
				result+=cardNumber.PadLeft(16,'*')+Environment.NewLine;
			}
			if(cardType.IsNullOrEmpty()) {
				cardType=CreditCardUtils.GetCardType(cardNumber);
			}
			result+="Card Type".PadRight(xright-xleft,'.')+cardType+Environment.NewLine;
			result+="Entry".PadRight(xright-xleft,'.')+(String.IsNullOrEmpty(magData) ? "Manual" : "Swiped")+Environment.NewLine;
			result+="Auth Code".PadRight(xright-xleft,'.')+authCode+Environment.NewLine;
			result+="Result".PadRight(xright-xleft,'.')+statusDescription+Environment.NewLine;
			if(messages!=null) {
				string label="Message";
				foreach(string m in messages) {
					result+=label.PadRight(xright-xleft,'.')+m+Environment.NewLine;
					label="";
				}
			}
			result+=Environment.NewLine+Environment.NewLine+Environment.NewLine;
			if(transType==PayConnectService.transType.VOID) {
				result+=GetAmountStringForReceipt((amount*-1),(surchargeAmount*-1),xleft,xright);
			}
			else {
				result+=GetAmountStringForReceipt(amount,surchargeAmount,xleft,xright);
			}
			result+=Environment.NewLine+Environment.NewLine+Environment.NewLine;
			result+="I agree to pay the above total amount according to my card issuer/bank agreement."+Environment.NewLine;
			result+=Environment.NewLine+Environment.NewLine+Environment.NewLine+Environment.NewLine+Environment.NewLine;
			if(doShowSignatureLine) {
				result+="Signature X".PadRight(xmax-xleft,'_');
			}
			else {
				result+="Electronically signed";
			}
			return result;
		}

		private static string GetAmountStringForReceipt(decimal amount,decimal surchargeAmount,int xleft,int xright) {
			string receiptString="";
			if(surchargeAmount!=0) {
				receiptString+="Amt".PadRight(xright-xleft,'.')+amount.ToString("c")+Environment.NewLine;
				receiptString+="Surcharge".PadRight(xright-xleft,'.')+surchargeAmount.ToString("c")+Environment.NewLine;
				receiptString+="Total Amt".PadRight(xright-xleft,'.')+(amount+surchargeAmount).ToString("c")+Environment.NewLine;
			}
			else {
				receiptString+="Total Amt".PadRight(xright-xleft,'.')+amount.ToString("c")+Environment.NewLine;
			}
			return receiptString;
		}

		///<summary>Only deletes the credit card from Open Dental.  We currently do not delete PayConnect tokens from PayConnect anywhere like we do with XCharge/XWeb/PaySimple.
		///Throws exceptions.</summary>
		public static void DeleteCreditCard(Patient pat,CreditCard cc,LogSources logSource=LogSources.None) {
			if(pat==null) {
				throw new ODException("No Patient Found",ODException.ErrorCodes.NoPatientFound);
			}
			if(cc==null) {
				throw new ODException("No Credit Card Found",ODException.ErrorCodes.OtkArgsInvalid);
			}
			if(string.IsNullOrEmpty(cc.PayConnectToken)) {
				throw new ODException("Invalid CC Alias",ODException.ErrorCodes.OtkArgsInvalid);
			}
			CreditCards.DeleteAndRefresh(cc,logSource);
		}

		public class WebPaymentProperties {
			public bool IsPaymentsAllowed;
			///<summary>The value of the "Patient Portal Payments Token" program property.
			///This is the account token related to the username/password used for PayConnect.</summary>
			public string Token;
			public int ProgramVersion;
		}
		
		public static class ProgramProperties {
			public const string PayConnectForceRecurringCharge="PayConnectForceRecurringCharge";
			public const string DefaultProcessingMethod="DefaultProcessingMethod";
			public const string PayConnectPreventSavingNewCC="PayConnectPreventSavingNewCC";
			public const string PatientPortalPaymentsEnabled="IsOnlinePaymentsEnabled";
			public const string PatientPortalPaymentsToken="Patient Portal Payments Token";
			public const string ProgramVersion="Program Version";
			///<summary>PayConnect 2 integrator API key. Practice-wide (ClinicNum 0 only), osd1$, masked and high security.
			///ProgramProperties.GetPracticeWideDescs repeats this name so InsertForClinic never copies it.</summary>
			public const string ApiKey="API Key";
			///<summary>PayConnect 2 merchant Secret, per clinic. osd1$ once saved; a plain-text value from Open Dental still works until then.</summary>
			public const string ApiSecret="API Secret";
			///<summary>PayConnect 2: "1" sends every call to the vendor's sandbox. Practice-wide (ClinicNum 0 only).</summary>
			public const string UseSandbox="PayConnect2 Use Sandbox";
			///<summary>PayConnect 2 account type per clinic: "0" normal, "1" surcharge.</summary>
			public const string IntegrationType="PayConnect2.0 Integration Type: 0 for normal, 1 for surcharge";
		}
	}
	
	///<summary>The local log of failed PayConnect calls. Open Dental 25.3 writes these to its eConnector log; this program has no eConnector, and
	///every PayConnect call goes from the desktop straight to DentalXChange, so each workstation logs its own failures: one line per failed call,
	///in the PayConnect subfolder of the Logger folder, kept DaysKept days. A line never carries a full card number, a CVV, a card token, a password
	///or a key: of the card, only the last 4 characters.</summary>
	public class PayConnectErrorLog {
		///<summary>The Logger subfolder.</summary>
		public const string SubDirectory="PayConnect";
		///<summary>How long log files are kept (the Logger default).</summary>
		public const int DaysKept=90;
		///<summary>How many lines PayConnect Setup shows.</summary>
		public const int RecentLinesShown=50;
		///<summary>Longest message kept in a line.</summary>
		private const int MAX_MESSAGE_LENGTH=500;
		///<summary>A run of 12 to 19 digits, optionally split by spaces or dashes: what a card number looks like.</summary>
		private static readonly Regex _regexCardNumber=new Regex(@"(?<![0-9])[0-9](?:[ -]?[0-9]){11,18}(?![0-9])");
		///<summary>Tests only: when set, each line goes here (without Logger's timestamp) instead of to the log folder.</summary>
		public static Action<string> ActionWriteLineForTests;

		///<summary>One failed call. Every field is optional.</summary>
		public class Entry {
			public string Workstation="";
			public string UserName="";
			///<summary>The clinic whose merchant account was used. 0 for the practice, or when the call had no clinic (setup's test connection).</summary>
			public long ClinicNum;
			public long PatNum;
			///<summary>What was attempted, for example "sale", "recurring sale", "refund", "void", "card token form", "status".</summary>
			public string Operation="";
			public int? AmountCents;
			///<summary>0 when no answer arrived.</summary>
			public int HttpStatus;
			///<summary>Whether the request reached PayConnect (PayConnect2.PayConnectRequestOutcome), or blank.</summary>
			public string Outcome="";
			///<summary>The kind of error (PayConnect2.PayConnectErrorKind), or blank.</summary>
			public string ErrorKind="";
			///<summary>PayConnect's message, or ours. Scrubbed of anything that looks like a card number.</summary>
			public string Message="";
			///<summary>Last 4 characters of the card or token only.</summary>
			public string CardLast4="";
			///<summary>The correlation ID sent as invoiceNumber, so the transaction can be looked up in the PayConnect portal. Opaque.</summary>
			public string InvoiceNumber="";
		}

		///<summary>An entry for this workstation and the logged-on user (blank when nobody is logged on).</summary>
		public static Entry MakeEntry(string operation,long clinicNum,long patNum) {
			Entry entry=new Entry();
			entry.Operation=operation??"";
			entry.ClinicNum=clinicNum;
			entry.PatNum=patNum;
			try {
				entry.Workstation=ODEnvironment.MachineName??"";
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
			try {
				entry.UserName=Security.CurUser?.UserName??"";
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
			return entry;
		}

		///<summary>The log line for an entry: tab-separated Name=value fields, in a fixed order. Logger adds the time.</summary>
		public static string FormatLine(Entry entry) {
			List<string> listFields=new List<string>();
			listFields.Add("ERROR");
			listFields.Add("Workstation="+ScrubText(entry.Workstation,100));
			listFields.Add("User="+ScrubText(entry.UserName,100));
			listFields.Add("Clinic="+entry.ClinicNum.ToString(System.Globalization.CultureInfo.InvariantCulture));
			listFields.Add("PatNum="+entry.PatNum.ToString(System.Globalization.CultureInfo.InvariantCulture));
			listFields.Add("Operation="+ScrubText(entry.Operation,100));
			listFields.Add("Amount="+(entry.AmountCents.HasValue ? FormatAmount(entry.AmountCents.Value) : ""));
			listFields.Add("HTTP="+(entry.HttpStatus>0 ? entry.HttpStatus.ToString(System.Globalization.CultureInfo.InvariantCulture) : ""));
			listFields.Add("Outcome="+ScrubText(entry.Outcome,50));
			listFields.Add("Code="+ScrubText(entry.ErrorKind,50));
			listFields.Add("Card="+GetLast4(entry.CardLast4));
			listFields.Add("Invoice="+ScrubText(entry.InvoiceNumber,50,doMaskCardNumbers:false));
			listFields.Add("Message="+ScrubText(entry.Message,MAX_MESSAGE_LENGTH));
			return string.Join("\t",listFields);
		}

		///<summary>Writes one line for the entry. Never throws: a logging failure must never break a payment.</summary>
		public static void Write(Entry entry) {
			if(entry==null) {
				return;
			}
			WriteLine(FormatLine(entry));
		}

		///<summary>The summary line written after a recurring-charge run that charged PayConnect cards.</summary>
		public static string FormatRecurringSummary(int countCards,int countApproved,int countDeclined,int countFailed) {
			string workstation="";
			try {
				workstation=ODEnvironment.MachineName??"";
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
			string userName="";
			try {
				userName=Security.CurUser?.UserName??"";
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
			return string.Join("\t",new[] {
				"SUMMARY",
				"Workstation="+ScrubText(workstation,100),
				"User="+ScrubText(userName,100),
				"Operation=recurring charges",
				"Cards="+countCards.ToString(System.Globalization.CultureInfo.InvariantCulture),
				"Approved="+countApproved.ToString(System.Globalization.CultureInfo.InvariantCulture),
				"Declined="+countDeclined.ToString(System.Globalization.CultureInfo.InvariantCulture),
				"Failed="+countFailed.ToString(System.Globalization.CultureInfo.InvariantCulture),
			});
		}

		///<summary>Writes the summary line of a recurring-charge run. Nothing is written when the run charged no PayConnect card.</summary>
		public static void WriteRecurringSummary(int countCards,int countApproved,int countDeclined,int countFailed) {
			if(countCards<=0) {
				return;
			}
			WriteLine(FormatRecurringSummary(countCards,countApproved,countDeclined,countFailed));
		}

		///<summary>The last lines of this workstation's PayConnect log, oldest first, for PayConnect Setup.</summary>
		public static List<string> GetRecentLines(int maxLines=RecentLinesShown) {
			return Logger.GetRecentLines(SubDirectory,maxLines);
		}

		///<summary>The last 4 letters or digits of a card number, masked number or token, or "" if it has fewer than 4. Anything else is dropped
		///first, so "XXXXXXXXXXXX1234" and "4111 1111 1111 1234" both give "1234".</summary>
		public static string GetLast4(string cardOrToken) {
			if(string.IsNullOrEmpty(cardOrToken)) {
				return "";
			}
			string alphanumeric=new string(cardOrToken.Where(x => char.IsLetterOrDigit(x) && x<128).ToArray());
			if(alphanumeric.Length<4) {
				return "";
			}
			return alphanumeric.Substring(alphanumeric.Length-4);
		}

		///<summary>Text made safe for one log field: tabs and line breaks become spaces, anything that looks like a card number is replaced (unless
		///doMaskCardNumbers is false, for our own opaque IDs), and the result is trimmed and cut to maxLength.</summary>
		public static string ScrubText(string text,int maxLength=MAX_MESSAGE_LENGTH,bool doMaskCardNumbers=true) {
			if(string.IsNullOrEmpty(text)) {
				return "";
			}
			StringBuilder stringBuilder=new StringBuilder(text.Length);
			foreach(char c in text) {
				stringBuilder.Append(char.IsControl(c) ? ' ' : c);
			}
			string scrubbed=stringBuilder.ToString();
			if(doMaskCardNumbers) {
				scrubbed=_regexCardNumber.Replace(scrubbed,"[card number removed]");
			}
			scrubbed=scrubbed.Trim();
			if(scrubbed.Length>maxLength) {
				scrubbed=scrubbed.Substring(0,maxLength);
			}
			return scrubbed;
		}

		private static string FormatAmount(int amountCents) {
			return (amountCents/100m).ToString("0.00",System.Globalization.CultureInfo.InvariantCulture);
		}

		private static void WriteLine(string line) {
			try {
				if(ActionWriteLineForTests!=null) {
					ActionWriteLineForTests(line);
					return;
				}
				Logger.WriteLine(line,SubDirectory,DaysKept);
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
		}
	}

	///<summary>The method of completing PayConnect credit card transactions.</summary>
	public enum PayConnectProcessingMethod {
		///<summary>Performs transactions by making a web call to PayConnect's service.</summary>
		[Description("Web Service")]
		WebService,
		///<summary>Performs the transactions on a credit card terminal hooked up to the computer.</summary>
		[Description("Terminal")]
		Terminal,
	}

}

namespace OpenDentBusiness.PayConnectService {
	///<summary>The PayConnect transaction types. This enum used to come from the PayConnect 1.0 MerchantService web reference, which was removed.
	///It keeps that namespace, name, members and order because payconnectresponseweb.TransType stores the member name (PayConnectResponseWebCrud
	///parses it), and receipts and payment notes print it.</summary>
	public enum transType {
		///<summary>0</summary>
		SALE,
		///<summary>1</summary>
		AUTH,
		///<summary>2</summary>
		RETURN,
		///<summary>3</summary>
		VOID,
		///<summary>4</summary>
		FORCE,
	}
}
