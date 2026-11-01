// 25.2 v25_2-account-payplan-downpayment-audit: changing or removing a payment plan's down payment writes a PayPlanEdit entry that starts with
// "Down Payment Changed" and gives the old and new amounts and the PayPlanNum; nothing is logged when nothing changed. Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;
using OpenPmsDentalBusiness;
using UnitTestsCore;

namespace BPAYTests {
	[TestClass]
	public class DownPaymentTextTests {
		[TestInitialize]
		public void TestInitialize() {
			BPayDb.UseEnglishCulture();
		}

		[TestMethod]
		public void Text_StartsWithDownPaymentChangedAndGivesAmountsAndPlan() {
			Assert.AreEqual("Down Payment Changed from 200.00 to 150.00, PayPlanNum 12.",PayPlanAudits.GetDownPaymentChangeText(200,150,12));
		}

		[TestMethod]
		public void Removal_IsMarked() {
			Assert.AreEqual("Down Payment Changed from 200.00 to 0.00 (removed), PayPlanNum 12.",PayPlanAudits.GetDownPaymentChangeText(200,0,12));
			Assert.AreEqual("Down Payment Changed from 200.00 to 0.00 (down payment charges, removed), PayPlanNum 3.",
				PayPlanAudits.GetDownPaymentChangeText(200,0,3,"down payment charges"));
		}

		[TestMethod]
		public void NoChange_NoText() {
			Assert.AreEqual("",PayPlanAudits.GetDownPaymentChangeText(75,75,1));
			Assert.AreEqual("",PayPlanAudits.GetDownPaymentLogText(75,75,75,75.001,1));
		}

		[TestMethod]
		public void TermsChange_WinsOverTheChargesItReplaced() {
			//A new schedule for a new down payment replaces the down payment charges too; that is one change, logged once from the terms.
			Assert.AreEqual("Down Payment Changed from 100.00 to 120.00, PayPlanNum 5.",PayPlanAudits.GetDownPaymentLogText(100,120,100,120,5));
		}

		[TestMethod]
		public void ChargesChange_IsLoggedWhenTheTermsDidNotChange() {
			Assert.AreEqual("Down Payment Changed from 100.00 to 80.00 (down payment charges), PayPlanNum 5.",
				PayPlanAudits.GetDownPaymentLogText(100,100,100,80,5));
		}

		[TestMethod]
		public void SumDownPaymentCharges_CountsOnlyDownPaymentDebits() {
			List<PayPlanCharge> listPayPlanCharges=new List<PayPlanCharge> {
				new PayPlanCharge { ChargeType=PayPlanChargeType.Debit,IsDownPayment=true,Principal=60 },
				new PayPlanCharge { ChargeType=PayPlanChargeType.Debit,IsDownPayment=true,Principal=40 },
				new PayPlanCharge { ChargeType=PayPlanChargeType.Debit,IsDownPayment=false,Principal=500 },
				new PayPlanCharge { ChargeType=PayPlanChargeType.Credit,IsDownPayment=false,Principal=900 },
			};
			Assert.AreEqual(100,PayPlanAudits.SumDownPaymentCharges(listPayPlanCharges),0.001);
			Assert.AreEqual(0,PayPlanAudits.SumDownPaymentCharges(null),0.001);
		}
	}

	[TestClass]
	public class DownPaymentAuditDbTests {
		[TestInitialize]
		public void TestInitialize() {
			BPayDb.Begin();
			BPayDb.LogInWith(EnumPermType.PayPlanEdit);
		}

		[TestCleanup]
		public void TestCleanup() {
			Security.CurUser=new Userod();
		}

		private static PayPlan CreatePlan(string suffix,double downPayment) {
			Patient patient=BPayDb.CreatePatient(suffix);
			PayPlan payPlan=PayPlanT.CreatePayPlanNoCharges(patient.PatNum,600,DateTime.Today);
			payPlan.DownPayment=downPayment;
			PayPlans.Update(payPlan);
			return PayPlans.GetOne(payPlan.PayPlanNum);
		}

		[TestMethod]
		public void TermsChange_IsLoggedUnderPayPlanEditWithThePlan() {
			PayPlan payPlanOld=CreatePlan("Terms",200);
			PayPlan payPlan=payPlanOld.Copy();
			payPlan.DownPayment=150;
			PayPlanL.MakeSecLogEntries(payPlan,payPlanOld,false,false,true,false,downPaymentChargesOld:200,downPaymentChargesNew:150);
			List<string> listLogs=BPayDb.GetPayPlanEditLogTexts(payPlan.PayPlanNum);
			Assert.AreEqual(1,listLogs.Count);
			Assert.AreEqual("Down Payment Changed from 200.00 to 150.00, PayPlanNum "+payPlan.PayPlanNum+".",listLogs[0]);
			Assert.AreEqual(payPlan.PatNum.ToString(),DataCore.GetScalar("SELECT PatNum FROM securitylog WHERE PermType="
				+POut.Int((int)EnumPermType.PayPlanEdit)+" AND FKey="+POut.Long(payPlan.PayPlanNum)));
		}

		[TestMethod]
		public void EditedDownPaymentCharge_IsLogged() {
			PayPlan payPlanOld=CreatePlan("Charge",200);
			PayPlan payPlan=payPlanOld.Copy();
			PayPlanL.MakeSecLogEntries(payPlan,payPlanOld,false,false,true,false,downPaymentChargesOld:200,downPaymentChargesNew:180);
			List<string> listLogs=BPayDb.GetPayPlanEditLogTexts(payPlan.PayPlanNum);
			Assert.AreEqual(1,listLogs.Count);
			StringAssert.StartsWith(listLogs[0],"Down Payment Changed from 200.00 to 180.00 (down payment charges)");
		}

		[TestMethod]
		public void RemovedDownPayment_IsLogged() {
			PayPlan payPlanOld=CreatePlan("Removed",200);
			PayPlan payPlan=payPlanOld.Copy();
			payPlan.DownPayment=0;
			PayPlanL.MakeSecLogEntries(payPlan,payPlanOld,false,false,true,false,downPaymentChargesOld:200,downPaymentChargesNew:0);
			StringAssert.Contains(BPayDb.GetPayPlanEditLogTexts(payPlan.PayPlanNum)[0],"(removed)");
		}

		[TestMethod]
		public void NothingChanged_NothingLogged() {
			PayPlan payPlanOld=CreatePlan("Same",200);
			PayPlanL.MakeSecLogEntries(payPlanOld.Copy(),payPlanOld,false,false,true,false,downPaymentChargesOld:200,downPaymentChargesNew:200);
			Assert.AreEqual(0,BPayDb.GetPayPlanEditLogTexts(payPlanOld.PayPlanNum).Count);
		}

		[TestMethod]
		public void NewPlan_IsLoggedAsCreatedOnly() {
			PayPlan payPlanOld=CreatePlan("New",0);
			payPlanOld.IsNew=true;
			PayPlan payPlan=payPlanOld.Copy();
			payPlan.DownPayment=100;
			PayPlanL.MakeSecLogEntries(payPlan,payPlanOld,false,false,true,false,downPaymentChargesOld:0,downPaymentChargesNew:100);
			List<string> listLogs=BPayDb.GetPayPlanEditLogTexts(payPlan.PayPlanNum);
			Assert.AreEqual(1,listLogs.Count);
			StringAssert.Contains(listLogs[0],"created");
		}

		[TestMethod]
		public void Printing_LogsNothing() {
			PayPlan payPlanOld=CreatePlan("Print",200);
			PayPlan payPlan=payPlanOld.Copy();
			payPlan.DownPayment=50;
			PayPlanL.MakeSecLogEntries(payPlan,payPlanOld,false,false,true,false,isPrinting:true,downPaymentChargesOld:200,downPaymentChargesNew:50);
			Assert.AreEqual(0,BPayDb.GetPayPlanEditLogTexts(payPlan.PayPlanNum).Count);
		}

		[TestMethod]
		public void DownPaymentCharges_FromTheScheduleAreSummed() {
			//An old-style plan whose amortization schedule has a down payment: the 25.1 flag marks those debits.
			Patient patient=BPayDb.CreatePatient("Schedule");
			Procedure procedure=BPayDb.CreateProcedure(patient,ProcStat.C,600);
			PayPlan payPlan=PayPlanT.CreatePayPlanWithCredits(patient.PatNum,100,DateTime.Today,listProcs:new List<Procedure> { procedure },downPayment:150);
			List<PayPlanCharge> listPayPlanCharges=PayPlanCharges.GetForPayPlan(payPlan.PayPlanNum);
			Assert.AreEqual(150,PayPlanAudits.SumDownPaymentCharges(listPayPlanCharges),0.001);
		}
	}
}
