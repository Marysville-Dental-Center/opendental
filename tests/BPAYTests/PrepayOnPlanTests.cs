// 25.2 v25_2-account-payplan-prepay-attach, the database side: a Payment Plan Prepay split attached to a patient payment plan is held as that plan's
// prepayment. Deleting the plan leaves it as an unattached prepayment of the same type, income transfers keep ignoring it, and the database
// maintenance checks that look at unearned or payment plan splits do not report it. Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using UnitTestsCore;

namespace BPAYTests {
	[TestClass]
	public class PrepayOnPlanTests {
		private long _defNumPrepay;

		[TestInitialize]
		public void TestInitialize() {
			BPayDb.Begin();
			BPayDb.LogInWith(EnumPermType.PayPlanEdit,EnumPermType.PaymentCreate,EnumPermType.PaymentEdit);
			_defNumPrepay=PrefC.GetLong(PrefName.DynamicPayPlanPrepaymentUnearnedType);
		}

		[TestCleanup]
		public void TestCleanup() {
			Security.CurUser=new Userod();
		}

		private static PayPlan CreateDynamicPlan(Patient patient) {
			Adjustment adjustment=AdjustmentT.MakeAdjustment(patient.PatNum,300,DateTime.Today);
			PayPlan payPlan=PayPlanT.CreateDynamicPaymentPlan(patient.PatNum,patient.PatNum,DateTime.Today,0,0,50,new List<Procedure>(),
				new List<Adjustment> { adjustment },PayPlanFrequency.Monthly,runService:false,doInsertChargeForToday:false);
			return PayPlans.GetOne(payPlan.PayPlanNum);
		}

		///<summary>A payment with one Payment Plan Prepay split attached to the plan, as the Paysplit window now saves it.</summary>
		private PaySplit CreatePrepaySplit(Patient patient,PayPlan payPlan,double amount) {
			Payment payment=PaymentT.MakePaymentNoSplits(patient.PatNum,amount,DateTime.Today);
			return PaySplitT.CreateOne(patient.PatNum,amount,payment.PayNum,0,payPlanNum:payPlan.PayPlanNum,unearnedType:_defNumPrepay,
				datePay:DateTime.Today);
		}

		[TestMethod]
		public void AttachedPrepay_CountsAsThePlansPrepayment() {
			Patient patient=BPayDb.CreatePatient("Held");
			PayPlan payPlan=CreateDynamicPlan(patient);
			CreatePrepaySplit(patient,payPlan,75);
			List<PaySplit> listPaySplits=PaySplits.GetForPayPlans(new List<long> { payPlan.PayPlanNum });
			Assert.AreEqual(75,PayPlanEdit.GetDynamicPayPlanPrepaymentAmount(listPaySplits),0.001,
				"The existing dynamic plan logic applies this money to the plan's future charges.");
			Assert.IsFalse(PayPlanEdit.AreAnyPayPlansOverpaid(listPaySplits),"A prepayment is not an overpayment of the plan's charges.");
		}

		[TestMethod]
		public void DeletingThePlan_LeavesAnUnattachedPrepaymentOfTheSameType() {
			Patient patient=BPayDb.CreatePatient("Deleted");
			PayPlan payPlan=CreateDynamicPlan(patient);
			PaySplit paySplit=CreatePrepaySplit(patient,payPlan,60);
			PayPlans.Delete(payPlan);
			Assert.IsNull(PayPlans.GetOne(payPlan.PayPlanNum));
			PaySplit paySplitDb=PaySplits.GetOne(paySplit.SplitNum);
			Assert.AreEqual(0,paySplitDb.PayPlanNum);
			Assert.AreEqual(0,paySplitDb.PayPlanChargeNum);
			Assert.AreEqual(_defNumPrepay,paySplitDb.UnearnedType);
			Assert.AreEqual(60,paySplitDb.SplitAmt,0.001);
			Assert.IsTrue(PaySplits.IsPaySplitHashValid(paySplitDb),"PayPlanNum is not part of the row hash.");
			List<string> listLogs=BPayDb.GetPayPlanEditLogTexts(payPlan.PayPlanNum);
			Assert.AreEqual(1,listLogs.Count);
			StringAssert.Contains(listLogs[0],"1 payment plan prepayment split(s) totaling 60.00 detached from deleted payment plan #"+payPlan.PayPlanNum);
		}

		[TestMethod]
		public void DeletingAPlanWithPayments_IsStillRefused() {
			Patient patient=BPayDb.CreatePatient("Paid");
			PayPlan payPlan=CreateDynamicPlan(patient);
			PaySplit paySplitPrepay=CreatePrepaySplit(patient,payPlan,40);
			PaymentT.MakePayment(patient.PatNum,25,DateTime.Today,payPlanNum:payPlan.PayPlanNum);
			try {
				PayPlans.Delete(payPlan);
				Assert.Fail("A plan with patient payments attached cannot be deleted.");
			}
			catch(ApplicationException ex) {
				StringAssert.Contains(ex.Message,"patient payments attached");
			}
			Assert.IsNotNull(PayPlans.GetOne(payPlan.PayPlanNum));
			Assert.AreEqual(payPlan.PayPlanNum,PaySplits.GetOne(paySplitPrepay.SplitNum).PayPlanNum,"Nothing was detached.");
		}

		[TestMethod]
		public void IncomeTransfer_IgnoresThePlanPrepayment() {
			Patient patient=BPayDb.CreatePatient("Transfer");
			PayPlan payPlan=CreateDynamicPlan(patient);
			PaySplit paySplit=CreatePrepaySplit(patient,payPlan,90);
			PaymentEdit.IncomeTransferData incomeTransferData=PaymentT.BalanceAndIncomeTransfer(patient.PatNum);
			Assert.IsFalse(incomeTransferData.ListSplitsCur.Any(x => x.FSplitNum==paySplit.SplitNum),"No transfer takes money from the plan prepayment.");
			Assert.IsFalse(incomeTransferData.ListSplitsCur.Any(x => x.UnearnedType==_defNumPrepay),"No transfer moves the hidden type.");
		}

		[TestMethod]
		public void DatabaseMaintenance_DoesNotReportAttachedPrepayments() {
			Patient patient=BPayDb.CreatePatient("Dbm");
			PayPlan payPlan=CreateDynamicPlan(patient);
			List<string> listBefore=RunChecks(patient.PatNum);
			CreatePrepaySplit(patient,payPlan,55);
			List<string> listAfter=RunChecks(patient.PatNum);
			CollectionAssert.AreEqual(listBefore,listAfter);
			Assert.AreEqual("",DatabaseMaintenances.PaySplitUnearnedAttachedToCompletedProcedures(false,DbmMode.Check,patient.PatNum));
		}

		private static List<string> RunChecks(long patNum) {
			return new List<string> {
				DatabaseMaintenances.PaySplitWithInvalidPayPlanNum(false,DbmMode.Check),
				DatabaseMaintenances.PaySplitWithInvalidPayPlanChargeNum(false,DbmMode.Check),
				DatabaseMaintenances.PaySplitAttachedToInsurancePaymentPlan(false,DbmMode.Check),
				DatabaseMaintenances.PaySplitTransfersWithNoUnearnedType(false,DbmMode.Check),
				DatabaseMaintenances.PaySplitUnearnedAttachedToCompletedProcedures(false,DbmMode.Check,patNum),
			};
		}
	}
}
