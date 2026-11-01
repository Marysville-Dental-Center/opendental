// The Payment window's Invoice filter (26.1, v26_1-account-payment-invoice-filter) on made-up account entries, without a database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2PAYWTests {
	[TestClass]
	public class InvoiceFilterRuleTests {
		private const long PAY_NUM=900;

		private static AccountEntry Proc(long procNum,double fee,decimal amountEnd) {
			AccountEntry accountEntry=new AccountEntry(new Procedure { ProcNum=procNum,PatNum=1,ProcFee=fee,ProcStatus=ProcStat.C,ProcDate=new DateTime(2026,1,5) });
			accountEntry.AmountEnd=amountEnd;
			return accountEntry;
		}

		private static AccountEntry Adj(long adjNum,double amount,decimal amountEnd) {
			AccountEntry accountEntry=new AccountEntry(new Adjustment { AdjNum=adjNum,PatNum=1,AdjAmt=amount,AdjDate=new DateTime(2026,1,5) });
			accountEntry.AmountEnd=amountEnd;
			return accountEntry;
		}

		private static FauxAccountEntry Charge(long payPlanChargeNum,double principal,double interest,bool isPrincipal,decimal amountEnd) {
			PayPlanCharge payPlanCharge=new PayPlanCharge {
				PayPlanChargeNum=payPlanChargeNum,
				PayPlanNum=50,
				PatNum=1,
				Guarantor=1,
				Principal=principal,
				Interest=interest,
				ChargeType=PayPlanChargeType.Debit,
				ChargeDate=new DateTime(2026,1,5),
			};
			FauxAccountEntry fauxAccountEntry=new FauxAccountEntry(payPlanCharge,isPrincipal);
			fauxAccountEntry.AmountEnd=amountEnd;
			return fauxAccountEntry;
		}

		private static InvoiceInfo Invoice(long statementNum,DateTime dateSent,long[] arrayProcNums=null,long[] arrayAdjNums=null,long[] arrayChargeNums=null) {
			return new InvoiceInfo {
				StatementNum=statementNum,
				PatNum=1,
				DateSent=dateSent,
				InvoiceTotal=123.45,
				ListProcNums=(arrayProcNums??new long[0]).ToList(),
				ListAdjNums=(arrayAdjNums??new long[0]).ToList(),
				ListPayPlanChargeNums=(arrayChargeNums??new long[0]).ToList(),
			};
		}

		[TestMethod]
		public void IsMember_MatchesProceduresAdjustmentsAndBothPartsOfAPayPlanCharge() {
			InvoiceInfo invoiceInfo=Invoice(7,new DateTime(2026,1,6),arrayProcNums:new long[] { 1 },arrayAdjNums:new long[] { 2 },arrayChargeNums:new long[] { 3 });
			Assert.IsTrue(PaymentInvoiceFilter.IsMember(invoiceInfo,Proc(1,100,100)));
			Assert.IsFalse(PaymentInvoiceFilter.IsMember(invoiceInfo,Proc(9,100,100)));
			Assert.IsTrue(PaymentInvoiceFilter.IsMember(invoiceInfo,Adj(2,20,20)));
			Assert.IsFalse(PaymentInvoiceFilter.IsMember(invoiceInfo,Adj(1,20,20)),"An adjustment never matches by a procedure's number.");
			Assert.IsTrue(PaymentInvoiceFilter.IsMember(invoiceInfo,Charge(3,90,10,true,90)));
			Assert.IsTrue(PaymentInvoiceFilter.IsMember(invoiceInfo,Charge(3,90,10,false,10)));
			Assert.IsFalse(PaymentInvoiceFilter.IsMember(invoiceInfo,Charge(4,90,10,true,90)));
			//Income rows are never members.
			AccountEntry accountEntrySplit=new AccountEntry(new PaySplit { SplitNum=5,ProcNum=1,SplitAmt=10 });
			Assert.IsFalse(PaymentInvoiceFilter.IsMember(invoiceInfo,accountEntrySplit));
			Assert.IsFalse(PaymentInvoiceFilter.IsMember(null,Proc(1,100,100)));
		}

		[TestMethod]
		public void Filter_AllKeepsEverythingAndAnInvoiceKeepsOnlyItsMembers() {
			List<AccountEntry> listAccountEntries=new List<AccountEntry> { Proc(1,100,100),Proc(2,50,50),Adj(3,20,20),Charge(4,90,0,true,90) };
			Assert.AreSame(listAccountEntries,PaymentInvoiceFilter.Filter(listAccountEntries,null));
			InvoiceInfo invoiceInfo=Invoice(7,new DateTime(2026,1,6),arrayProcNums:new long[] { 1 },arrayAdjNums:new long[] { 3 });
			List<AccountEntry> listFiltered=PaymentInvoiceFilter.Filter(listAccountEntries,invoiceInfo);
			CollectionAssert.AreEqual(new[] { listAccountEntries[0],listAccountEntries[2] },listFiltered);
		}

		[TestMethod]
		public void AmountOwedBeforePayment_AddsBackThisPaymentsSplits() {
			AccountEntry accountEntry=Proc(1,100,0);
			accountEntry.SplitCollection.Add(new PaySplit { SplitNum=0,PayNum=PAY_NUM,ProcNum=1,SplitAmt=60 });//New split of this payment.
			accountEntry.SplitCollection.Add(new PaySplit { SplitNum=31,PayNum=PAY_NUM,ProcNum=1,SplitAmt=15 });//Saved split of this payment.
			accountEntry.SplitCollection.Add(new PaySplit { SplitNum=32,PayNum=12,ProcNum=1,SplitAmt=25 });//Another payment.
			Assert.AreEqual(75m,PaymentInvoiceFilter.GetAmountOwedBeforePayment(accountEntry,PAY_NUM));
		}

		[TestMethod]
		public void InvoicesToShow_OnlyWithABalance_NewestFirst_KeepingTheCurrentChoice() {
			InvoiceInfo invoicePaid=Invoice(10,new DateTime(2026,1,1),arrayProcNums:new long[] { 1 });
			InvoiceInfo invoiceOld=Invoice(11,new DateTime(2026,1,2),arrayProcNums:new long[] { 2 });
			InvoiceInfo invoiceNew=Invoice(12,new DateTime(2026,2,1),arrayAdjNums:new long[] { 3 });
			InvoiceInfo invoiceSameDay=Invoice(13,new DateTime(2026,2,1),arrayChargeNums:new long[] { 4 });
			InvoiceInfo invoiceEmpty=Invoice(14,new DateTime(2026,3,1));
			List<AccountEntry> listAccountEntries=new List<AccountEntry> { Proc(1,100,0),Proc(2,50,50),Adj(3,20,20),Charge(4,90,0,true,90) };
			List<InvoiceInfo> listInvoiceInfos=new List<InvoiceInfo> { invoicePaid,invoiceOld,invoiceNew,invoiceSameDay,invoiceEmpty };
			List<InvoiceInfo> listShown=PaymentInvoiceFilter.GetInvoicesToShow(listInvoiceInfos,listAccountEntries,PAY_NUM);
			CollectionAssert.AreEqual(new long[] { 13,12,11 },listShown.Select(x => x.StatementNum).ToArray());
			//The current choice stays even though it is paid off.
			listShown=PaymentInvoiceFilter.GetInvoicesToShow(listInvoiceInfos,listAccountEntries,PAY_NUM,statementNumKeep:10);
			CollectionAssert.AreEqual(new long[] { 13,12,11,10 },listShown.Select(x => x.StatementNum).ToArray());
			//Paid off by this payment: still listed, because the balance is judged before this payment.
			AccountEntry accountEntryPaidNow=Proc(1,100,0);
			accountEntryPaidNow.SplitCollection.Add(new PaySplit { PayNum=PAY_NUM,ProcNum=1,SplitAmt=100 });
			listShown=PaymentInvoiceFilter.GetInvoicesToShow(new List<InvoiceInfo> { invoicePaid },new List<AccountEntry> { accountEntryPaidNow },PAY_NUM);
			Assert.AreEqual(1,listShown.Count);
			Assert.AreEqual(0,PaymentInvoiceFilter.GetInvoicesToShow(null,listAccountEntries,PAY_NUM).Count);
		}

		[TestMethod]
		public void DisplayText_HasDateNumberAndTotal() {
			InvoiceInfo invoiceInfo=Invoice(77,new DateTime(2026,4,9));
			string text=PaymentInvoiceFilter.GetDisplayText(invoiceInfo);
			StringAssert.Contains(text,new DateTime(2026,4,9).ToShortDateString());
			StringAssert.Contains(text,"#77");
			StringAssert.Contains(text,123.45.ToString("c"));
		}
	}
}
