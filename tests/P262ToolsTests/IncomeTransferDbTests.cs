// Database Maintenance check PaymentIncomeTransferIssues (Open Dental 26.2 item 23) against the scratch database. Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using Crud=OpenPmsDentalBusiness.Crud;

namespace P262ToolsTests {
	[TestClass]
	public class IncomeTransferDbTests {
		private static readonly DateTime _date=new DateTime(2026,3,2);

		[TestInitialize]
		public void TestInitialize() {
			ToolsDb.Begin();
		}

		///<summary>A $0 payment with a payment type and splits of +50 and -50.</summary>
		private static Payment NewTransferWithPayType(long payType,long depositNum=0,CreditCardSource creditCardSource=CreditCardSource.None,
			bool isRecurringCC=false)
		{
			Patient patient=ToolsDb.NewPatient();
			Payment payment=ToolsDb.InsertPayment(patient.PatNum,payType,0,_date,depositNum,creditCardSource,isRecurringCC);
			ToolsDb.InsertSplit(payment,50);
			ToolsDb.InsertSplit(payment,-50);
			return payment;
		}

		[TestMethod]
		public void WithPayType_IsFoundAndFixGivesItNoPaymentType() {
			long payType=ToolsDb.NewPayType();
			Payment payment=NewTransferWithPayType(payType);
			Assert.IsTrue(DatabaseMaintenances.GetIncomeTransfersWithPayType().Any(x => x.PayNum==payment.PayNum));
			StringAssert.Contains(DatabaseMaintenances.PaymentIncomeTransferIssues(false,DbmMode.Check),"Income transfers saved with a payment type: ");
			Assert.AreEqual(payType,ToolsDb.GetPayment(payment.PayNum).PayType,"Check changes nothing.");
			string log=DatabaseMaintenances.PaymentIncomeTransferIssues(false,DbmMode.Fix);
			StringAssert.Contains(log,"Income transfers saved with a payment type changed to no payment type: ");
			Payment paymentAfter=ToolsDb.GetPayment(payment.PayNum);
			Assert.AreEqual(0,paymentAfter.PayType);
			Assert.AreEqual(0,paymentAfter.PayAmt);
			CollectionAssert.AreEqual(new[] { 50.0,-50.0 },ToolsDb.GetSplits(payment.PayNum).Select(x => x.SplitAmt).ToArray(),"No amount changes.");
			List<DbmLog> listDbmLogs=ToolsDb.GetDbmLogs(nameof(DatabaseMaintenances.PaymentIncomeTransferIssues),payment.PayNum);
			Assert.AreEqual(1,listDbmLogs.Count);
			Assert.AreEqual(DbmLogFKeyType.Payment,listDbmLogs[0].FKeyType);
			Assert.AreEqual(DbmLogActionType.Update,listDbmLogs[0].ActionType);
			StringAssert.Contains(listDbmLogs[0].LogText,"Updated PayType from "+payType);
			Assert.IsFalse(DatabaseMaintenances.GetIncomeTransfersWithPayType().Any(x => x.PayNum==payment.PayNum));
		}

		[TestMethod]
		public void WithPayType_LeftAlone_OnDepositCardRecurringOrAccounting() {
			long payType=ToolsDb.NewPayType();
			List<Payment> listPayments=new List<Payment>();
			listPayments.Add(NewTransferWithPayType(payType,depositNum:999999));
			listPayments.Add(NewTransferWithPayType(payType,creditCardSource:CreditCardSource.PayConnect));
			listPayments.Add(NewTransferWithPayType(payType,isRecurringCC:true));
			Payment paymentAccounting=NewTransferWithPayType(payType);
			listPayments.Add(paymentAccounting);
			Transaction transaction=new Transaction();
			transaction.PayNum=paymentAccounting.PayNum;
			transaction.DateTimeEntry=DateTime.Now;
			Crud.TransactionCrud.Insert(transaction);
			DatabaseMaintenances.PaymentIncomeTransferIssues(false,DbmMode.Fix);
			List<DbmIncomeTransferRow> listRows=DatabaseMaintenances.GetIncomeTransfersWithPayType();
			for(int i=0;i<listPayments.Count;i++) {
				Assert.IsFalse(listRows.Any(x => x.PayNum==listPayments[i].PayNum),"Payment "+i+" must not be listed.");
				Assert.AreEqual(payType,ToolsDb.GetPayment(listPayments[i].PayNum).PayType,"Payment "+i+" must keep its payment type.");
			}
		}

		[TestMethod]
		public void WithPayType_LeftAlone_WhenSplitsAreZeroOrDoNotBalance() {
			long payType=ToolsDb.NewPayType();
			Patient patient=ToolsDb.NewPatient();
			Payment paymentZeroSplits=ToolsDb.InsertPayment(patient.PatNum,payType,0,_date);//a declined card: every split $0
			ToolsDb.InsertSplit(paymentZeroSplits,0);
			Payment paymentUnbalanced=ToolsDb.InsertPayment(patient.PatNum,payType,0,_date);//PaymentAmtNotMatchPaySplitTotal's business, not this one's
			ToolsDb.InsertSplit(paymentUnbalanced,40);
			ToolsDb.InsertSplit(paymentUnbalanced,-10);
			Payment paymentReal=ToolsDb.InsertPayment(patient.PatNum,payType,25,_date);//an ordinary payment
			ToolsDb.InsertSplit(paymentReal,25);
			DatabaseMaintenances.PaymentIncomeTransferIssues(false,DbmMode.Fix);
			foreach(Payment payment in new[] { paymentZeroSplits,paymentUnbalanced,paymentReal }) {
				Assert.AreEqual(payType,ToolsDb.GetPayment(payment.PayNum).PayType);
			}
		}

		[TestMethod]
		public void NotBalanced_IsReportedForAManualFixAndNeverChanged() {
			Patient patient=ToolsDb.NewPatient();
			Payment paymentUnbalanced=ToolsDb.InsertPayment(patient.PatNum,0,0,_date);
			ToolsDb.InsertSplit(paymentUnbalanced,50);
			ToolsDb.InsertSplit(paymentUnbalanced,-30);
			Payment paymentWithAmount=ToolsDb.InsertPayment(patient.PatNum,0,25,_date);
			ToolsDb.InsertSplit(paymentWithAmount,25);
			Payment paymentBalanced=ToolsDb.InsertPayment(patient.PatNum,0,0,_date);//a good income transfer
			ToolsDb.InsertSplit(paymentBalanced,20);
			ToolsDb.InsertSplit(paymentBalanced,-20);
			List<DbmIncomeTransferRow> listRows=DatabaseMaintenances.GetIncomeTransfersNotBalanced();
			DbmIncomeTransferRow rowUnbalanced=listRows.Single(x => x.PayNum==paymentUnbalanced.PayNum);
			Assert.IsFalse(rowUnbalanced.HasAmount);
			Assert.AreEqual(20,rowUnbalanced.SplitTotal,0.001);
			Assert.IsTrue(listRows.Single(x => x.PayNum==paymentWithAmount.PayNum).HasAmount);
			Assert.IsFalse(listRows.Any(x => x.PayNum==paymentBalanced.PayNum));
			string logCheck=DatabaseMaintenances.PaymentIncomeTransferIssues(false,DbmMode.Check);
			StringAssert.Contains(logCheck,"Income transfers with an amount other than zero: ");
			StringAssert.Contains(logCheck,"Income transfers whose splits do not total zero: ");
			StringAssert.Contains(logCheck,"Manual fix needed.");
			string logFix=DatabaseMaintenances.PaymentIncomeTransferIssues(false,DbmMode.Fix);
			StringAssert.Contains(logFix,"Manual fix needed.");
			Assert.AreEqual(0,ToolsDb.GetPayment(paymentUnbalanced.PayNum).PayAmt);
			Assert.AreEqual(25,ToolsDb.GetPayment(paymentWithAmount.PayNum).PayAmt);
			CollectionAssert.AreEqual(new[] { 50.0,-30.0 },ToolsDb.GetSplits(paymentUnbalanced.PayNum).Select(x => x.SplitAmt).ToArray());
			Assert.AreEqual(0,ToolsDb.GetDbmLogs(nameof(DatabaseMaintenances.PaymentIncomeTransferIssues),paymentUnbalanced.PayNum).Count);
		}

		[TestMethod]
		public void Breakdown_ListsThePatientsOfEveryGroup() {
			long payType=ToolsDb.NewPayType();
			Payment paymentWithPayType=NewTransferWithPayType(payType);
			Patient patient=ToolsDb.NewPatient();
			Payment paymentUnbalanced=ToolsDb.InsertPayment(patient.PatNum,0,0,_date);
			ToolsDb.InsertSplit(paymentUnbalanced,12.5);
			string log=DatabaseMaintenances.PaymentIncomeTransferIssues(false,DbmMode.Breakdown);
			StringAssert.Contains(log,"   "+paymentWithPayType.PatNum+"  ");
			StringAssert.Contains(log,"   "+patient.PatNum+"  ");
			StringAssert.Contains(log,"Split total: $12.50");
			StringAssert.Contains(log,"need to be fixed manually");
			Assert.AreEqual(payType,ToolsDb.GetPayment(paymentWithPayType.PayNum).PayType,"Breakdown changes nothing.");
		}

		[TestMethod]
		public void Attribute_HasBreakdownExplainAndReplicationCheck() {
			DbmMethodAttr dbmMethodAttr=(DbmMethodAttr)Attribute.GetCustomAttribute(
				typeof(DatabaseMaintenances).GetMethod(nameof(DatabaseMaintenances.PaymentIncomeTransferIssues)),typeof(DbmMethodAttr));
			Assert.IsNotNull(dbmMethodAttr);
			Assert.IsTrue(dbmMethodAttr.HasBreakDown);
			Assert.IsTrue(dbmMethodAttr.HasExplain);
			Assert.IsTrue(dbmMethodAttr.IsReplicationUnsafe);
			Assert.IsFalse(dbmMethodAttr.IsOneOff);
		}
	}
}
