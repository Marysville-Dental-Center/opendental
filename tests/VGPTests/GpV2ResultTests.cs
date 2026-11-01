// V-GP: reading Global Payments' answers into a transaction result, and the note, receipt and log text built from it.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using OpenDentBusiness.VendorDirect.GlobalPayments;

namespace VGPTests {
	[TestClass]
	public class GpV2ResultTests {
		private static GlobalPaymentsV2Result ReadDevice(string body) {
			JObject jObject=JObject.Parse(body);
			GlobalPaymentsV2Result result=new GlobalPaymentsV2Result();
			result.TransType=GpV2TransType.Sale;
			GpV2Json.ReadTransaction(result,GpV2Json.GetDeviceTransaction(jObject),jObject);
			return result;
		}

		[TestMethod]
		public void DeviceAnswer_Pending_UntilATransactionArrives() {
			Assert.IsTrue(GpV2Json.IsDeviceCommandPending(JObject.Parse(GpSamples.DeviceInitiated("DVC_1"))));
			Assert.IsFalse(GpV2Json.IsDeviceCommandPending(JObject.Parse(GpSamples.DeviceCompleted("DVC_1","TRN_1","100"))));
			Assert.IsFalse(GpV2Json.IsDeviceCommandPending(JObject.Parse("{\"id\":\"DVC_1\",\"status\":\"CANCELLED\"}")),"A final status ends the wait.");
			//A transaction with an ID ends the wait whatever the command status still says.
			Assert.IsFalse(GpV2Json.IsDeviceCommandPending(JObject.Parse("{\"id\":\"DVC_1\",\"status\":\"INITIATED\",\"transaction\":{\"id\":\"TRN_1\"}}")));
			Assert.IsTrue(GpV2Json.IsDeviceCommandPending(null));
		}

		[TestMethod]
		public void DeviceAnswer_Approved_ReadsTheCard() {
			GlobalPaymentsV2Result result=ReadDevice(GpSamples.DeviceCompleted("DVC_1","TRN_1","1234"));
			Assert.AreEqual(GpV2Outcome.Approved,result.Outcome);
			Assert.AreEqual("TRN_1",result.TransactionId);
			Assert.AreEqual(12.34m,result.Amount);
			Assert.AreEqual("VISA",result.CardBrand);
			Assert.AreEqual("4242",result.CardLast4);
			Assert.AreEqual("A1B2C3",result.AuthCode,"The device sample spells it auth_code.");
			Assert.AreEqual("CONTACTLESS",result.EntryMode);
			Assert.AreEqual("",result.PaymentMethodId,"The public sample's blank payment method ID is not a card token.");
			Assert.AreEqual("XXXXXXXXXXXX4242",result.GetMaskedCardNumber());
		}

		[TestMethod]
		public void DeviceAnswer_Declined() {
			GlobalPaymentsV2Result result=ReadDevice(GpSamples.DeviceCompleted("DVC_1","TRN_1","1234","51"));
			Assert.AreEqual(GpV2Outcome.Declined,result.Outcome);
			Assert.AreEqual("51",result.ResultCode);
			result=ReadDevice(GpSamples.DeviceCompleted("DVC_1","TRN_1","1234","00","DECLINED"));
			Assert.AreEqual(GpV2Outcome.Declined,result.Outcome,"A DECLINED transaction status wins.");
		}

		[TestMethod]
		public void DeviceAnswer_ReusablePaymentMethodIsKept() {
			GlobalPaymentsV2Result result=ReadDevice(GpSamples.DeviceCompleted("DVC_1","TRN_1","1234","00","","PMT_31e8"));
			Assert.AreEqual("PMT_31e8",result.PaymentMethodId);
		}

		[TestMethod]
		public void DeviceAnswer_CancelledOnTheTerminal_MovedNoMoney() {
			GlobalPaymentsV2Result result=ReadDevice("{\"id\":\"DVC_1\",\"status\":\"CANCELLED\"}");
			Assert.AreEqual(GpV2Outcome.Cancelled,result.Outcome);
			result=ReadDevice("{\"id\":\"DVC_1\",\"status\":\"FAILED\"}");
			Assert.AreEqual(GpV2Outcome.Failed,result.Outcome);
		}

		[TestMethod]
		public void DecideOutcome_Table() {
			Assert.AreEqual(GpV2Outcome.Approved,GpV2Json.DecideOutcome(GpV2TransType.Sale,"CAPTURED","","","","TRN_1"));
			Assert.AreEqual(GpV2Outcome.Approved,GpV2Json.DecideOutcome(GpV2TransType.Sale,"PREAUTHORIZED","","","","TRN_1"));
			Assert.AreEqual(GpV2Outcome.Declined,GpV2Json.DecideOutcome(GpV2TransType.Sale,"DECLINED","","","","TRN_1"));
			Assert.AreEqual(GpV2Outcome.Declined,GpV2Json.DecideOutcome(GpV2TransType.Sale,"CAPTURED","","05","","TRN_1"),"A result code that isn't 00.");
			Assert.AreEqual(GpV2Outcome.Declined,GpV2Json.DecideOutcome(GpV2TransType.Sale,"","COMPLETED","","DECLINED","TRN_1"));
			Assert.AreEqual(GpV2Outcome.Failed,GpV2Json.DecideOutcome(GpV2TransType.Sale,"FAILED","","","","TRN_1"));
			Assert.AreEqual(GpV2Outcome.Approved,GpV2Json.DecideOutcome(GpV2TransType.Void,"REVERSED","","","","TRN_1"));
			Assert.AreEqual(GpV2Outcome.Unknown,GpV2Json.DecideOutcome(GpV2TransType.Sale,"REVERSED","","","","TRN_1"),"REVERSED is not a sale's approval.");
			Assert.AreEqual(GpV2Outcome.Approved,GpV2Json.DecideOutcome(GpV2TransType.Sale,"","COMPLETED","00","","TRN_1"));
			Assert.AreEqual(GpV2Outcome.Approved,GpV2Json.DecideOutcome(GpV2TransType.Sale,"","COMPLETED","","SUCCESS","TRN_1"));
			Assert.AreEqual(GpV2Outcome.Approved,GpV2Json.DecideOutcome(GpV2TransType.Sale,"","COMPLETED","","","TRN_1"));
			Assert.AreEqual(GpV2Outcome.Failed,GpV2Json.DecideOutcome(GpV2TransType.Sale,"","COMPLETED","","","")," No transaction, no money.");
			Assert.AreEqual(GpV2Outcome.Unknown,GpV2Json.DecideOutcome(GpV2TransType.Sale,"PENDING","","","","TRN_1"));
		}

		[TestMethod]
		public void Helpers_MinorUnitsLast4AndErrors() {
			Assert.AreEqual(1234L,GlobalPaymentsV2.ToMinorUnits(12.34m));
			Assert.AreEqual(1235L,GlobalPaymentsV2.ToMinorUnits(12.345m),"Half away from zero.");
			Assert.AreEqual(12.34m,GpV2Json.ParseMinorUnits("1234"));
			Assert.IsNull(GpV2Json.ParseMinorUnits("12.34"));
			Assert.AreEqual("4242",GpV2Json.GetLast4("XXXX XXXX XXXX 4242"));
			Assert.AreEqual("",GpV2Json.GetLast4("XX42"));
			Assert.AreEqual("Unknown device_reference (40001)",GpV2Json.ReadErrorMessage(GpSamples.Error("INVALID_REQUEST_DATA","Unknown device_reference")));
			Assert.AreEqual("INVALID_REQUEST_DATA",GpV2Json.ReadErrorCode(GpSamples.Error("INVALID_REQUEST_DATA","x")));
			Assert.AreEqual("",GpV2Json.ReadErrorMessage("<html>not json</html>"));
		}

		[TestMethod]
		public void References_AreEightToFiftyCharacters() {
			string reference=GlobalPaymentsV2.GetRecurringReference(1,new DateTime(2026,10,25));
			Assert.AreEqual("RC1-20261025",reference);
			Assert.IsTrue(reference.Length>=8 && reference.Length<=50);
			Assert.IsTrue(GlobalPaymentsV2.GetRecurringReference(long.MaxValue,DateTime.Today).Length<=50);
			string referenceNew=GlobalPaymentsV2.NewReference("SL");
			Assert.IsTrue(referenceNew.Length>=8 && referenceNew.Length<=50);
			Assert.AreNotEqual(referenceNew,GlobalPaymentsV2.NewReference("SL"));
		}

		private static GlobalPaymentsV2Result MakeApprovedSale() {
			GlobalPaymentsV2Result result=new GlobalPaymentsV2Result();
			result.TransType=GpV2TransType.Sale;
			result.Outcome=GpV2Outcome.Approved;
			result.Amount=45.67m;
			result.TransactionId="TRN_abc";
			result.CardBrand="VISA";
			result.CardLast4="4242";
			result.AuthCode="A1B2C3";
			result.PaymentMethodId="PMT_do_not_show";
			result.TerminalName="Front Desk";
			result.TerminalId="T-1";
			return result;
		}

		[TestMethod]
		public void PayNote_ShowsLastFourOnly_AndNeverTheToken() {
			GlobalPaymentsV2Result result=MakeApprovedSale();
			string note=GlobalPaymentsV2.BuildPayNote(result);
			StringAssert.Contains(note,"Global Payments V2");
			StringAssert.Contains(note,"Amount: $45.67");
			StringAssert.Contains(note,"XXXXXXXXXXXX4242");
			StringAssert.Contains(note,"Terminal: Front Desk");
			StringAssert.Contains(note,GlobalPaymentsV2.NoteLabelTransaction+": TRN_abc");
			Assert.IsFalse(note.Contains("PMT_do_not_show"));
			Assert.AreEqual("TRN_abc",GlobalPaymentsV2.GetTransactionIdFromNote("Earlier note\r\n"+note));
			//FormPayment voids on cancel by reading "Transaction ID: " (EdgeExpress) and "Ref Number: " (PayConnect) lines; V2 must not look like them.
			foreach(string line in note.Replace("\r\n","\n").Split('\n')) {
				Assert.IsFalse(line.StartsWith("Transaction ID: ") || line.StartsWith("TRANSACTIONID: ") || line.StartsWith("Ref Number: "),line);
			}
		}

		[TestMethod]
		public void PayNote_RefundIsNegative_SandboxIsMarked() {
			GlobalPaymentsV2Result result=MakeApprovedSale();
			result.TransType=GpV2TransType.Refund;
			result.IsSandbox=true;
			string note=GlobalPaymentsV2.BuildPayNote(result);
			StringAssert.Contains(note,"($45.67)");
			StringAssert.StartsWith(note,"SANDBOX TEST - NOT A REAL CHARGE");
		}

		[TestMethod]
		public void Receipt_HasTheTransactionAndLastFourOnly() {
			GlobalPaymentsV2Result result=MakeApprovedSale();
			string receipt=GlobalPaymentsV2.BuildReceipt(result,"Synthetic Dental\r\n123 Main St\r\n","Pat Synthetic",new DateTime(2026,10,25,9,30,0));
			StringAssert.Contains(receipt,"Synthetic Dental");
			StringAssert.Contains(receipt,"Transaction #.."+"TRN_abc");
			StringAssert.Contains(receipt,"************4242");
			StringAssert.Contains(receipt,"$45.67");
			StringAssert.Contains(receipt,"Signature X");
			Assert.IsFalse(receipt.Contains("PMT_do_not_show"));
			result.TransType=GpV2TransType.Void;
			Assert.IsFalse(GlobalPaymentsV2.BuildReceipt(result,"","",DateTime.Now).Contains("Signature X"),"Only a sale asks for a signature.");
		}

		[TestMethod]
		public void LogText_HasAmountLastFourTerminalNotToken() {
			GlobalPaymentsV2Result result=MakeApprovedSale();
			string text=GlobalPaymentsV2.BuildLogText(result);
			StringAssert.Contains(text,"Global Payments V2 sale $45.67");
			StringAssert.Contains(text,"card ending 4242");
			StringAssert.Contains(text,"terminal Front Desk");
			StringAssert.Contains(text,"Approved");
			Assert.IsFalse(text.Contains("PMT_"));
			result.Outcome=GpV2Outcome.TimedOut;
			result.DeviceCommandId="DVC_77";
			StringAssert.Contains(GlobalPaymentsV2.BuildLogText(result),"DVC_77","A transaction in doubt names the terminal command for support.");
		}
	}
}
