using System;
using CodeBase;
using OpenDentBusiness;

namespace OpenDental.Bridges {
	///<summary>Receipts for PayConnect 2 transactions (terminal sales and voids through the cloud API). The PayConnect 1.0 VeriFone terminal path
	///(DpsPos.dll) was removed.</summary>
	public class PayConnectTerminal {

		///<summary>Builds a receipt string for a terminal transaction.</summary>
		public static string BuildReceiptString(PayConnectResponse pcResponse,bool wasElectronicallySigned,long clinicNum) {
			string result="";
			int xleft=0;
			int xright=15;
			int xmax=37;
			result+=Environment.NewLine;
			result+=CreditCardUtils.AddClinicToReceipt(clinicNum);
			//Print body
			result+="Date".PadRight(xright-xleft,'.')+DateTime.Now.ToString()+Environment.NewLine;
			result+=Environment.NewLine;
			result+=AddReceiptField("Trans Type",pcResponse.TransType.ToString());
			result+=Environment.NewLine;
			result+=AddReceiptField("Transaction #",pcResponse.RefNumber);
			result+=AddReceiptField("Account",pcResponse.CardNumber);
			result+=AddReceiptField("Card Type",pcResponse.CardType);
			result+=AddReceiptField("Entry",pcResponse.EntryMode);
			result+=AddReceiptField("Auth Code",pcResponse.AuthCode);
			result+=AddReceiptField("Result",pcResponse.Description);
			result+=AddReceiptField("MerchantId",pcResponse.MerchantId);
			result+=AddReceiptField("TerminalId",pcResponse.TerminalId);
			result+=AddReceiptField("Mode",pcResponse.Mode);
			result+=AddReceiptField("CardVerifyMthd",pcResponse.CardVerificationMethod);
			if(pcResponse.EMV!=null && !string.IsNullOrEmpty(pcResponse.EMV.AppId)) {
				result+=AddReceiptField("EMV AppId",pcResponse.EMV.AppId);
			}
			if(pcResponse.EMV!=null && !string.IsNullOrEmpty(pcResponse.EMV.TermVerifResults)) {
				result+=AddReceiptField("EMV TermResult",pcResponse.EMV.TermVerifResults);
			}
			if(pcResponse.EMV!=null && !string.IsNullOrEmpty(pcResponse.EMV.IssuerAppData)) {
				result+=AddReceiptField("EMV IssuerData",pcResponse.EMV.IssuerAppData);
			}
			if(pcResponse.EMV!=null && !string.IsNullOrEmpty(pcResponse.EMV.TransStatusInfo)) {
				result+=AddReceiptField("EMV TransInfo",pcResponse.EMV.TransStatusInfo);
			}
			if(pcResponse.EMV!=null && !string.IsNullOrEmpty(pcResponse.EMV.AuthResponseCode)) {
				result+=AddReceiptField("EMV AuthResp",pcResponse.EMV.AuthResponseCode);
			}
			result+=Environment.NewLine+Environment.NewLine+Environment.NewLine;
			Program program=Programs.GetCur(ProgramName.PayConnect);
			string integrationType=ProgramProperties.GetPropVal(program.ProgramNum,PayConnect.ProgramProperties.IntegrationType,clinicNum);
			//Void transaction already includes any surcharge amount, so no additional line item needed
			if(integrationType=="1" && pcResponse.TransType!=PayConnectResponse.TransactionType.Void) {//Surcharge integration
				result+=AddReceiptField("Surcharge Fee",pcResponse.AmountSurcharged.ToString("F2"));
			}
			if(pcResponse.TransType.In(PayConnectResponse.TransactionType.Refund,PayConnectResponse.TransactionType.Void)) {
				result+="Trans. Amt".PadRight(xright-xleft,'.')+(pcResponse.Amount*-1)+Environment.NewLine;
			}
			else {
				result+="Trans. Amt".PadRight(xright-xleft,'.')+pcResponse.Amount.ToString("F2")+Environment.NewLine;
			}
			result+=Environment.NewLine+Environment.NewLine+Environment.NewLine;
			result+="I agree to pay the above total amount according to my card issuer/bank agreement."+Environment.NewLine;
			result+=Environment.NewLine+Environment.NewLine+Environment.NewLine+Environment.NewLine+Environment.NewLine;
			if(wasElectronicallySigned) {
				result+="Electronically signed";
			}
			else {
				result+="Signature X".PadRight(xmax-xleft,'_');
			}
			return result;
		}


		///<summary>Returns the field name and value formatted to be added to a receipt string. The fieldName should be less than 15 characters.</summary>
		private static string AddReceiptField(string fieldName,string fieldValue) {
			int xleft=0;
			int xright=15;
			int xmax=37;
			string retStr="";
			fieldValue=fieldValue??"";
			retStr+=fieldName.PadRight(xright-xleft,'.');
			if(fieldValue.Length<xmax-xright) {//Short enough to fit on one line
				retStr+=fieldValue+Environment.NewLine;
			}
			else {//Put the field value on two lines
				retStr+=fieldValue.Substring(0,xmax-xright-1)+Environment.NewLine;
				retStr+="".PadRight(xright,'.')+fieldValue.Substring(xmax-xright-1)+Environment.NewLine;
			}
			return retStr;
		}
	}
}
