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
