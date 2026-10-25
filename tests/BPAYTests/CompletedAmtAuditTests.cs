// 25.1 v25_1-account-payplan-completedamt-audit: every change to a payment plan's Tx Completed Amount is audited with the old and new amounts, both
// manual edits and automatic recalculations, one entry per plan, never when nothing changed. Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDental;
using OpenDentBusiness;
using UnitTestsCore;

namespace BPAYTests {
	[TestClass]
	public class CompletedAmtTextTests {
		[TestInitialize]
		public void TestInitialize() {
			BPayDb.UseEnglishCulture();
		}

		[TestMethod]
		public void Text_GivesOldNewAndDifference() {
			Assert.AreEqual("Tx Completed Amt changed from 1,200.00 to 1,350.00 (+150.00).",PayPlanAudits.GetCompletedAmtChangeText(1200,1350));
			Assert.AreEqual("Tx Completed Amt changed from 500.00 to 425.50 (-74.50).",PayPlanAudits.GetCompletedAmtChangeText(500,425.5));
		}

		[TestMethod]
		public void Text_AddsTheTrigger() {
			Assert.AreEqual("Tx Completed Amt changed from 0.00 to 80.00 (+80.00), after procedure #7 was set complete.",
				PayPlanAudits.GetCompletedAmtChangeText(0,80," after procedure #7 was set complete "));
		}

		[TestMethod]
		public void UnchangedToTheCent_IsNotLogged() {
			Assert.AreEqual("",PayPlanAudits.GetCompletedAmtChangeText(100,100));
			Assert.AreEqual("",PayPlanAudits.GetCompletedAmtChangeText(100.001,100.004));
			Assert.IsFalse(PayPlanAudits.IsChangedToCents(19.994,19.991));
			Assert.IsTrue(PayPlanAudits.IsChangedToCents(19.994,19.996));
		}
	}

	[TestClass]
	public class CompletedAmtAuditDbTests {
		[TestInitialize]
		public void TestInitialize() {
			BPayDb.Begin();
			BPayDb.LogInWith(EnumPermType.PayPlanEdit);
		}

		[TestCleanup]
		public void TestCleanup() {
			Security.CurUser=new Userod();
		}

		///<summary>An old-style patient payment plan with a credit for a treatment planned procedure (credit dated MaxValue, as FormPayPlanCredits
		///makes it), so the completed amount starts at 0.</summary>
		private static PayPlan CreatePlanWithTpCredit(Patient patient,Procedure procedure) {
			PayPlan payPlan=PayPlanT.CreatePayPlanNoCharges(patient.PatNum,procedure.ProcFee,DateTime.Today);
			PayPlanCharges.Insert(new PayPlanCharge {
				PayPlanNum=payPlan.PayPlanNum,
				PatNum=patient.PatNum,
				Guarantor=patient.PatNum,
				ProcNum=procedure.ProcNum,
				Principal=procedure.ProcFee,
				ChargeDate=DateTime.MaxValue,
				ChargeType=PayPlanChargeType.Credit,
			});
			return PayPlans.GetOne(payPlan.PayPlanNum);
		}

		[TestMethod]
		public void ProcedureSetComplete_LogsTheRecalculation() {
			Patient patient=BPayDb.CreatePatient("Complete");
			Procedure procedure=BPayDb.CreateProcedure(patient,ProcStat.TP,240);
			PayPlan payPlan=CreatePlanWithTpCredit(patient,procedure);
			procedure.ProcStatus=ProcStat.C;
			procedure.ProcDate=DateTime.Today;
			Procedures.Update(procedure,Procedures.GetOneProc(procedure.ProcNum,false));
			PayPlanCharges.UpdateAttachedPayPlanCharges(procedure);
			Assert.AreEqual(240,PayPlans.GetOne(payPlan.PayPlanNum).CompletedAmt,0.001);
			List<string> listLogs=BPayDb.GetPayPlanEditLogTexts(payPlan.PayPlanNum);
			Assert.AreEqual(1,listLogs.Count);
			StringAssert.Contains(listLogs[0],"Patient Payment Plan Tx Completed Amt changed from 0.00 to 240.00 (+240.00)");
			StringAssert.Contains(listLogs[0],"was set complete");
			string patNumLogged=DataCore.GetScalar("SELECT PatNum FROM securitylog WHERE PermType="+POut.Int((int)EnumPermType.PayPlanEdit)
				+" AND FKey="+POut.Long(payPlan.PayPlanNum));
			Assert.AreEqual(patient.PatNum.ToString(),patNumLogged);
		}

		[TestMethod]
		public void RecalculationWithoutChange_IsNotLogged() {
			Patient patient=BPayDb.CreatePatient("NoChange");
			Procedure procedure=BPayDb.CreateProcedure(patient,ProcStat.TP,90);
			PayPlan payPlan=CreatePlanWithTpCredit(patient,procedure);
			PayPlans.UpdateTreatmentCompletedAmt(new List<PayPlan> { payPlan },"test");
			Assert.AreEqual(0,BPayDb.GetPayPlanEditLogTexts(payPlan.PayPlanNum).Count);
		}

		[TestMethod]
		public void BatchRecalculation_LogsOneEntryPerPlan() {
			Patient patient=BPayDb.CreatePatient("Batch");
			Procedure procedure=BPayDb.CreateProcedure(patient,ProcStat.C,150);
			PayPlan payPlan1=PayPlanT.CreatePayPlanWithCredits(patient.PatNum,50,DateTime.Today,listProcs:new List<Procedure> { procedure });
			PayPlan payPlan2=PayPlanT.CreatePayPlanWithCredits(patient.PatNum,50,DateTime.Today,listProcs:new List<Procedure> { procedure });
			//The helper leaves CompletedAmt at 0 although the credits are already dated today.
			PayPlans.UpdateTreatmentCompletedAmt(new List<PayPlan> { PayPlans.GetOne(payPlan1.PayPlanNum),PayPlans.GetOne(payPlan2.PayPlanNum) });
			Assert.AreEqual(1,BPayDb.GetPayPlanEditLogTexts(payPlan1.PayPlanNum).Count);
			Assert.AreEqual(1,BPayDb.GetPayPlanEditLogTexts(payPlan2.PayPlanNum).Count);
		}

		[TestMethod]
		public void DynamicPlanRecalculation_IsLogged() {
			Patient patient=BPayDb.CreatePatient("Dynamic");
			Procedure procedure=BPayDb.CreateProcedure(patient,ProcStat.TP,300);
			PayPlan payPlan=PayPlanT.CreateDynamicPaymentPlan(patient.PatNum,patient.PatNum,DateTime.Today,0,0,50,new List<Procedure> { procedure },
				new List<Adjustment>(),PayPlanFrequency.Monthly,dynamicPayPlanTPOptions:DynamicPayPlanTPOptions.AwaitComplete,runService:false,
				doInsertChargeForToday:false);
			//Setting the procedure complete recalculates the attached plans (Procedures.Update calls PayPlanCharges.UpdateAttachedPayPlanCharges).
			procedure.ProcStatus=ProcStat.C;
			Procedures.Update(procedure,Procedures.GetOneProc(procedure.ProcNum,false));
			List<string> listLogs=BPayDb.GetPayPlanEditLogTexts(payPlan.PayPlanNum);
			Assert.AreEqual(1,listLogs.Count);
			StringAssert.StartsWith(listLogs[0],"Payment Plan Tx Completed Amt changed from 0.00 to 300.00 (+300.00), after procedure ",
				"Dynamic plans have no plan type prefix, like their other entries.");
			StringAssert.EndsWith(listLogs[0],"was set complete.");
			//Recalculating again changes nothing and logs nothing.
			PayPlans.UpdateTreatmentCompletedAmtsDynamicPaymentPlan(new List<PayPlan> { PayPlans.GetOne(payPlan.PayPlanNum) },"again");
			Assert.AreEqual(1,BPayDb.GetPayPlanEditLogTexts(payPlan.PayPlanNum).Count);
		}

		private static DynamicPaymentPlanModuleData SaveDynamic(long payPlanNum,decimal amountOverride=-1) {
			DynamicPaymentPlanModuleData moduleData=PayPlanEdit.GetDynamicPaymentPlanModuleData(PayPlans.GetOne(payPlanNum));
			if(amountOverride>=0) {//as the Amount Override column of the window does
				moduleData.ListPayPlanProductionEntries[0].AmountOverride=amountOverride;
				moduleData.ListPayPlanLinks[0].AmountOverride=(double)amountOverride;
			}
			PayPlanTerms terms=PayPlanEdit.GetPayPlanTerms(moduleData.PayPlan,moduleData.ListPayPlanLinks);
			return PayPlanEdit.SaveDynamicPaymentPlanAndTerms(terms,moduleData,false,moduleData.PayPlan.PlanCategory);
		}

		[TestMethod]
		public void DynamicPlanSave_LogsEachChangeOnce() {
			Patient patient=BPayDb.CreatePatient("DynSave");
			Adjustment adjustment=AdjustmentT.MakeAdjustment(patient.PatNum,300,DateTime.Today);
			PayPlan payPlan=PayPlanT.CreateDynamicPaymentPlan(patient.PatNum,patient.PatNum,DateTime.Today,0,0,50,new List<Procedure>(),
				new List<Adjustment> { adjustment },PayPlanFrequency.Monthly,runService:false,doInsertChargeForToday:false);
			PayPlan payPlanOld=PayPlans.GetOne(payPlan.PayPlanNum);
			DynamicPaymentPlanModuleData moduleData=SaveDynamic(payPlan.PayPlanNum);
			//The window then writes its other entries; dynamic plans skip the completed amount there, so each change is logged once.
			PayPlanL.MakeSecLogEntries(moduleData.PayPlan,payPlanOld,false,false,true,false);
			SaveDynamic(payPlan.PayPlanNum,amountOverride:250);
			SaveDynamic(payPlan.PayPlanNum);//no change
			List<string> listLogs=BPayDb.GetPayPlanEditLogTexts(payPlan.PayPlanNum).FindAll(x => x.Contains("Tx Completed Amt"));
			Assert.AreEqual(2,listLogs.Count);
			Assert.AreEqual("Payment Plan Tx Completed Amt changed from 0.00 to 300.00 (+300.00).",listLogs[0]);
			Assert.AreEqual("Payment Plan Tx Completed Amt changed from 300.00 to 250.00 (-50.00).",listLogs[1]);
		}

		[TestMethod]
		public void ManualEdit_OldStylePlan_LogsAmountsAndOnlyOnce() {
			Patient patient=BPayDb.CreatePatient("Manual");
			PayPlan payPlan=PayPlanT.CreatePayPlanNoCharges(patient.PatNum,500,DateTime.Today,completedAmt:500);
			PayPlan payPlanOld=PayPlans.GetOne(payPlan.PayPlanNum);
			PayPlan payPlanNew=payPlanOld.Copy();
			payPlanNew.CompletedAmt=450;
			PayPlans.Update(payPlanNew);
			PayPlanL.MakeSecLogEntries(payPlanNew,payPlanOld,false,false,true,false);
			PayPlanL.AdvanceAuditBaseline(payPlanNew,payPlanOld,false);
			//Saving again in the same window (for example Print, then OK) does not repeat the entry.
			PayPlanL.MakeSecLogEntries(payPlanNew,payPlanOld,false,false,true,false);
			List<string> listLogs=BPayDb.GetPayPlanEditLogTexts(payPlan.PayPlanNum);
			Assert.AreEqual(1,listLogs.Count);
			Assert.AreEqual("Patient Payment Plan Tx Completed Amt changed from 500.00 to 450.00 (-50.00).",listLogs[0]);
		}

		[TestMethod]
		public void PrintingDoesNotMoveTheBaseline() {
			PayPlan payPlanOld=new PayPlan { CompletedAmt=10,DownPayment=5 };
			PayPlan payPlan=new PayPlan { CompletedAmt=20,DownPayment=7 };
			PayPlanL.AdvanceAuditBaseline(payPlan,payPlanOld,isPrinting:true);
			Assert.AreEqual(10,payPlanOld.CompletedAmt);
			Assert.AreEqual(5,payPlanOld.DownPayment);
		}

		[TestMethod]
		public void WithoutALoggedOnUser_TheEntryHasUserZero() {
			Patient patient=BPayDb.CreatePatient("Service");
			PayPlan payPlan=PayPlanT.CreatePayPlanNoCharges(patient.PatNum,100,DateTime.Today);
			Security.CurUser=null;
			try {
				Assert.IsTrue(PayPlanAudits.LogCompletedAmtChange(payPlan,0,100,"by a service"));
			}
			finally {
				Security.CurUser=new Userod();
			}
			Assert.AreEqual("0",DataCore.GetScalar("SELECT UserNum FROM securitylog WHERE PermType="+POut.Int((int)EnumPermType.PayPlanEdit)
				+" AND FKey="+POut.Long(payPlan.PayPlanNum)));
		}
	}
}
