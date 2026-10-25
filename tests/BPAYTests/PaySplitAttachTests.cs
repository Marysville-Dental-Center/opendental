// 25.2 v25_2-account-payplan-prepay-attach and 25.3 v25_3-account-paysplit-payplan-attach: which payment plans a paysplit may be attached to.
// The cases follow the 25.3 spec's test matrix; public sources name none of them, so the expected results are our inference (docs/port-notes/B-PAY.md).
// Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using UnitTestsCore;

namespace BPAYTests {
	[TestClass]
	public class PaySplitAttachTests {
		private long _defNumPrepay;

		[TestInitialize]
		public void TestInitialize() {
			BPayDb.Begin();
			BPayDb.LogInWith(EnumPermType.PayPlanEdit,EnumPermType.PaymentCreate,EnumPermType.PaymentEdit);
			_defNumPrepay=PrefC.GetLong(PrefName.DynamicPayPlanPrepaymentUnearnedType);
			Assert.AreNotEqual(0,_defNumPrepay,"Set up: the 21.2 conversion adds the Payment Plan Prepay type and its preference.");
		}

		[TestCleanup]
		public void TestCleanup() {
			Security.CurUser=new Userod();
		}

		///<summary>A guarantor and a child in one family.</summary>
		private static (Patient guarantor,Patient child) CreateFamily(string suffix) {
			Patient guarantor=BPayDb.CreatePatient(suffix+"G");
			Patient child=BPayDb.CreatePatient(suffix+"C",guarantor:guarantor.PatNum);
			return (guarantor,child);
		}

		private static PayPlan CreateDynamicPlan(long patNum,long guarantorNum,List<Procedure> listProcedures=null,List<Adjustment> listAdjustments=null) {
			Adjustment adjustment=null;
			if(listProcedures.IsNullOrEmpty() && listAdjustments.IsNullOrEmpty()) {
				adjustment=AdjustmentT.MakeAdjustment(patNum,300,DateTime.Today);
			}
			PayPlan payPlan=PayPlanT.CreateDynamicPaymentPlan(patNum,guarantorNum,DateTime.Today,0,0,50,listProcedures??new List<Procedure>(),
				listAdjustments??(adjustment==null ? new List<Adjustment>() : new List<Adjustment> { adjustment }),PayPlanFrequency.Monthly,
				runService:false,doInsertChargeForToday:false);
			return PayPlans.GetOne(payPlan.PayPlanNum);
		}

		private static List<long> FamilyPatNums(params Patient[] arrayPatients) {
			return arrayPatients.Select(x => x.PatNum).ToList();
		}

		[TestMethod]
		public void PrepayType_IsRecognized() {
			Assert.IsTrue(PaySplitPayPlanAttach.IsPrepayType(_defNumPrepay));
			Assert.IsFalse(PaySplitPayPlanAttach.IsPrepayType(0));
			Assert.IsFalse(PaySplitPayPlanAttach.IsPrepayType(PrefC.GetLong(PrefName.PrepaymentUnearnedType)));
		}

		[TestMethod]
		public void PrepayDef_IsListedEvenWhenHidden() {
			Def defPrepay=Defs.GetDef(DefCat.PaySplitUnearnedType,_defNumPrepay);
			Def defOld=defPrepay.Copy();
			try {
				defPrepay.IsHidden=true;
				Defs.Update(defPrepay);
				Cache.Refresh(InvalidType.Defs);
				Assert.IsFalse(Defs.GetDefsForCategory(DefCat.PaySplitUnearnedType,true).Any(x => x.DefNum==_defNumPrepay),"Set up: hidden.");
				Assert.IsTrue(PaySplitPayPlanAttach.GetUnearnedDefsForSplitEdit().Any(x => x.DefNum==_defNumPrepay));
			}
			finally {
				Defs.Update(defOld);
				Cache.Refresh(InvalidType.Defs);
			}
			Assert.AreEqual(1,PaySplitPayPlanAttach.GetUnearnedDefsForSplitEdit().Count(x => x.DefNum==_defNumPrepay),"Listed once when visible.");
		}

		[TestMethod]
		public void Prepay_NoProcedure_OffersOnlyPatientDynamicPlans() {
			(Patient guarantor,Patient child)=CreateFamily("Prepay");
			PayPlan payPlanDynamic=CreateDynamicPlan(child.PatNum,guarantor.PatNum);
			PayPlan payPlanOldStyle=PayPlanT.CreatePayPlanNoCharges(child.PatNum,400,DateTime.Today,guarantorNum:guarantor.PatNum);
			PaySplitPayPlanAttach.AttachOptions attachOptions=PaySplitPayPlanAttach.GetOptions(child.PatNum,false,FamilyPatNums(guarantor,child),
				_defNumPrepay,null,0);
			Assert.IsTrue(attachOptions.IsAllowed,attachOptions.ErrorMsg);
			CollectionAssert.AreEqual(new List<long> { payPlanDynamic.PayPlanNum },attachOptions.ListPayPlans.Select(x => x.PayPlanNum).ToList(),
				"Old-style plan "+payPlanOldStyle.PayPlanNum+" does not apply prepayments.");
		}

		[TestMethod]
		public void Prepay_WithoutADynamicPlan_IsRefused() {
			(Patient guarantor,Patient child)=CreateFamily("PrepayNone");
			PayPlanT.CreatePayPlanNoCharges(child.PatNum,400,DateTime.Today,guarantorNum:guarantor.PatNum);
			PaySplitPayPlanAttach.AttachOptions attachOptions=PaySplitPayPlanAttach.GetOptions(child.PatNum,false,FamilyPatNums(guarantor,child),
				_defNumPrepay,null,0);
			Assert.IsFalse(attachOptions.IsAllowed);
			StringAssert.Contains(attachOptions.ErrorMsg,"patient payment plans");
		}

		[TestMethod]
		public void Prepay_WithAProcedureOrAdjustment_IsRefused() {
			(Patient guarantor,Patient child)=CreateFamily("PrepayProc");
			CreateDynamicPlan(child.PatNum,guarantor.PatNum);
			Procedure procedure=BPayDb.CreateProcedure(child,ProcStat.C,100);
			PaySplitPayPlanAttach.AttachOptions attachOptions=PaySplitPayPlanAttach.GetOptions(child.PatNum,false,FamilyPatNums(guarantor,child),
				_defNumPrepay,procedure,0);
			Assert.IsFalse(attachOptions.IsAllowed);
			StringAssert.Contains(attachOptions.ErrorMsg,"prepayment");
			attachOptions=PaySplitPayPlanAttach.GetOptions(child.PatNum,false,FamilyPatNums(guarantor,child),_defNumPrepay,null,12345);
			Assert.IsFalse(attachOptions.IsAllowed);
		}

		[TestMethod]
		public void FamilyMember_WhoIsThePlansPatientButNotItsGuarantor_IsAllowed() {
			(Patient guarantor,Patient child)=CreateFamily("PatNotGuar");
			PayPlan payPlan=CreateDynamicPlan(child.PatNum,guarantor.PatNum);
			PaySplitPayPlanAttach.AttachOptions attachOptions=PaySplitPayPlanAttach.GetOptions(child.PatNum,false,FamilyPatNums(guarantor,child),0,null,0);
			Assert.IsTrue(attachOptions.IsAllowed,attachOptions.ErrorMsg);
			Assert.IsTrue(attachOptions.ListPayPlans.Any(x => x.PayPlanNum==payPlan.PayPlanNum));
		}

		[TestMethod]
		public void OtherFamily_PlanGuaranteedByThePayingFamily_IsAllowed() {
			(Patient guarantor,Patient child)=CreateFamily("PayingFam");
			Patient patientOther=BPayDb.CreatePatient("OtherFam");
			PayPlan payPlanGuaranteedHere=CreateDynamicPlan(patientOther.PatNum,guarantor.PatNum);
			PayPlan payPlanOwnFamily=CreateDynamicPlan(patientOther.PatNum,patientOther.PatNum);
			PaySplitPayPlanAttach.AttachOptions attachOptions=PaySplitPayPlanAttach.GetOptions(patientOther.PatNum,true,FamilyPatNums(guarantor,child),
				0,null,0);
			Assert.IsTrue(attachOptions.IsAllowed,attachOptions.ErrorMsg);
			CollectionAssert.AreEqual(new List<long> { payPlanGuaranteedHere.PayPlanNum },attachOptions.ListPayPlans.Select(x => x.PayPlanNum).ToList(),
				"Plan "+payPlanOwnFamily.PayPlanNum+" is guaranteed by the other family.");
		}

		[TestMethod]
		public void OtherFamily_NoPlanGuaranteedByThePayingFamily_IsRefused() {
			(Patient guarantor,Patient child)=CreateFamily("PayingFam2");
			Patient patientOther=BPayDb.CreatePatient("OtherFam2");
			CreateDynamicPlan(patientOther.PatNum,patientOther.PatNum);
			PaySplitPayPlanAttach.AttachOptions attachOptions=PaySplitPayPlanAttach.GetOptions(patientOther.PatNum,true,FamilyPatNums(guarantor,child),
				0,null,0);
			Assert.IsFalse(attachOptions.IsAllowed);
			StringAssert.Contains(attachOptions.ErrorMsg,"guarantor in this family");
		}

		[TestMethod]
		public void Adjustment_ThatIsAPlanCredit_OffersThatPlan() {
			//The plan belongs to the guarantor; the adjustment is the child's, so 24.3 offered nothing for a split for the child.
			(Patient guarantor,Patient child)=CreateFamily("AdjCredit");
			Adjustment adjustment=AdjustmentT.MakeAdjustment(child.PatNum,250,DateTime.Today);
			PayPlan payPlan=CreateDynamicPlan(guarantor.PatNum,guarantor.PatNum,listAdjustments:new List<Adjustment> { adjustment });
			PaySplitPayPlanAttach.AttachOptions attachOptions=PaySplitPayPlanAttach.GetOptions(child.PatNum,false,FamilyPatNums(guarantor,child),0,null,
				adjustment.AdjNum);
			Assert.IsTrue(attachOptions.IsAllowed,attachOptions.ErrorMsg);
			CollectionAssert.AreEqual(new List<long> { payPlan.PayPlanNum },attachOptions.ListPayPlans.Select(x => x.PayPlanNum).ToList());
			Assert.IsTrue(PaySplitPayPlanAttach.CanKeepPayPlanForProduction(payPlan.PayPlanNum,0,null,adjustment.AdjNum));
		}

		[TestMethod]
		public void Procedure_ThatIsACreditOnADynamicPlan_OffersThatPlan() {
			(Patient guarantor,Patient child)=CreateFamily("ProcCredit");
			Procedure procedure=BPayDb.CreateProcedure(child,ProcStat.C,180);
			PayPlan payPlan=CreateDynamicPlan(guarantor.PatNum,guarantor.PatNum,listProcedures:new List<Procedure> { procedure });
			PaySplitPayPlanAttach.AttachOptions attachOptions=PaySplitPayPlanAttach.GetOptions(child.PatNum,false,FamilyPatNums(guarantor,child),0,
				procedure,0);
			Assert.IsTrue(attachOptions.IsAllowed,attachOptions.ErrorMsg);
			CollectionAssert.AreEqual(new List<long> { payPlan.PayPlanNum },attachOptions.ListPayPlans.Select(x => x.PayPlanNum).ToList());
			Assert.IsTrue(PaySplitPayPlanAttach.CanKeepPayPlanForProduction(payPlan.PayPlanNum,0,procedure,0));
			Assert.IsFalse(PaySplitPayPlanAttach.CanKeepPayPlanForProduction(payPlan.PayPlanNum+100000,0,procedure,0),"Another plan.");
			Assert.IsFalse(PaySplitPayPlanAttach.CanKeepPayPlanForProduction(0,0,procedure,0),"No plan.");
		}

		[TestMethod]
		public void Procedure_ThatIsACreditOnAnOldStylePlan_OffersThatPlan() {
			(Patient guarantor,Patient child)=CreateFamily("OldCredit");
			Procedure procedure=BPayDb.CreateProcedure(child,ProcStat.C,120);
			PayPlan payPlan=PayPlanT.CreatePayPlanWithCredits(guarantor.PatNum,40,DateTime.Today,listProcs:new List<Procedure> { procedure });
			Assert.IsTrue(PaySplitPayPlanAttach.GetPayPlansForProduction(procedure.ProcNum,0).Any(x => x.PayPlanNum==payPlan.PayPlanNum));
		}

		[TestMethod]
		public void TreatmentPlannedUnearned_IsStillRefused() {
			(Patient guarantor,Patient child)=CreateFamily("TpUnearned");
			Procedure procedure=BPayDb.CreateProcedure(child,ProcStat.TP,200);
			CreateDynamicPlan(child.PatNum,guarantor.PatNum,listProcedures:new List<Procedure> { procedure });
			long defNumTp=PrefC.GetLong(PrefName.TpUnearnedType);
			PaySplitPayPlanAttach.AttachOptions attachOptions=PaySplitPayPlanAttach.GetOptions(child.PatNum,false,FamilyPatNums(guarantor,child),
				defNumTp==0 ? PrefC.GetLong(PrefName.PrepaymentUnearnedType) : defNumTp,procedure,0);
			Assert.IsFalse(attachOptions.IsAllowed);
			StringAssert.Contains(attachOptions.ErrorMsg,"Treatment planned unearned");
			Assert.IsFalse(PaySplitPayPlanAttach.CanKeepPayPlanForProduction(1,defNumTp==0 ? 1 : defNumTp,procedure,0));
		}

		[TestMethod]
		public void InsurancePaymentPlans_AreNeverOffered() {
			(Patient guarantor,Patient child)=CreateFamily("InsPlan");
			PayPlanT.CreatePayPlanNoCharges(child.PatNum,500,DateTime.Today,guarantorNum:child.PatNum,planNum:999999,insSubNum:999999);
			PaySplitPayPlanAttach.AttachOptions attachOptions=PaySplitPayPlanAttach.GetOptions(child.PatNum,false,FamilyPatNums(guarantor,child),0,null,0);
			Assert.IsFalse(attachOptions.IsAllowed);
			StringAssert.Contains(attachOptions.ErrorMsg,"not the patient or guarantor of any payment plans");
		}

		[TestMethod]
		public void PatientWithoutPlans_IsRefusedAsIn24_3() {
			(Patient guarantor,Patient child)=CreateFamily("NoPlans");
			PaySplitPayPlanAttach.AttachOptions attachOptions=PaySplitPayPlanAttach.GetOptions(child.PatNum,false,FamilyPatNums(guarantor,child),0,null,0);
			Assert.IsFalse(attachOptions.IsAllowed);
		}

		[TestMethod]
		public void UnearnedTypeChange_DetachesAPrepaymentFromAnOldStylePlan() {
			PayPlan payPlanDynamic=new PayPlan { IsDynamic=true };
			PayPlan payPlanOldStyle=new PayPlan { IsDynamic=false };
			PayPlan payPlanInsurance=new PayPlan { IsDynamic=false,PlanNum=5 };
			Assert.IsFalse(PaySplitPayPlanAttach.MustDetachForUnearnedType(payPlanDynamic,_defNumPrepay));
			Assert.IsTrue(PaySplitPayPlanAttach.MustDetachForUnearnedType(payPlanOldStyle,_defNumPrepay));
			Assert.IsTrue(PaySplitPayPlanAttach.MustDetachForUnearnedType(payPlanInsurance,_defNumPrepay));
			Assert.IsFalse(PaySplitPayPlanAttach.MustDetachForUnearnedType(payPlanOldStyle,0),"Other types keep 24.3 behavior.");
			Assert.IsFalse(PaySplitPayPlanAttach.MustDetachForUnearnedType(null,_defNumPrepay));
		}

		[TestMethod]
		public void ChargeChoice_PrepayPaysNoChargeOtherwiseOldestDebitPreferringTheProduction() {
			List<PayPlanCharge> listPayPlanCharges=new List<PayPlanCharge> {
				new PayPlanCharge { PayPlanChargeNum=1,ChargeType=PayPlanChargeType.Credit,ChargeDate=DateTime.Today.AddDays(-90) },
				new PayPlanCharge { PayPlanChargeNum=2,ChargeType=PayPlanChargeType.Debit,ChargeDate=DateTime.Today.AddDays(-60),LinkType=PayPlanLinkType.Adjustment,FKey=77 },
				new PayPlanCharge { PayPlanChargeNum=3,ChargeType=PayPlanChargeType.Debit,ChargeDate=DateTime.Today.AddDays(-30),LinkType=PayPlanLinkType.Procedure,FKey=55 },
			};
			Assert.AreEqual(0,PaySplitPayPlanAttach.GetPayPlanChargeNum(listPayPlanCharges,_defNumPrepay,0,0),"A prepayment pays no charge.");
			Assert.AreEqual(2,PaySplitPayPlanAttach.GetPayPlanChargeNum(listPayPlanCharges,0,0,0),"Oldest debit; credits are never paid.");
			Assert.AreEqual(3,PaySplitPayPlanAttach.GetPayPlanChargeNum(listPayPlanCharges,0,55,0),"The debit for the split's procedure.");
			Assert.AreEqual(2,PaySplitPayPlanAttach.GetPayPlanChargeNum(listPayPlanCharges,0,0,77),"The debit for the split's adjustment.");
			Assert.AreEqual(2,PaySplitPayPlanAttach.GetPayPlanChargeNum(listPayPlanCharges,0,999,0),"Falls back to the oldest debit.");
			Assert.AreEqual(0,PaySplitPayPlanAttach.GetPayPlanChargeNum(new List<PayPlanCharge>(),0,0,0));
		}

		[TestMethod]
		public void TypeChange_ToPrepay_ClearsTheCharge_AndDoesNotLoadCharges() {
			int countLoads=0;
			Func<List<PayPlanCharge>> funcGetCharges=() => { countLoads++; return new List<PayPlanCharge>(); };
			Assert.AreEqual(0,PaySplitPayPlanAttach.GetPayPlanChargeNumAfterTypeChange(10,2,0,_defNumPrepay,funcGetCharges,0,0),"A prepayment pays no charge.");
			Assert.AreEqual(0,PaySplitPayPlanAttach.GetPayPlanChargeNumAfterTypeChange(10,2,PrefC.GetLong(PrefName.PrepaymentUnearnedType),_defNumPrepay,
				funcGetCharges,0,0),"From another unearned type too.");
			Assert.AreEqual(0,countLoads);
		}

		[TestMethod]
		public void TypeChange_FromPrepay_PicksADueChargeAgain() {
			List<PayPlanCharge> listPayPlanChargesDue=new List<PayPlanCharge> {
				new PayPlanCharge { PayPlanChargeNum=1,ChargeType=PayPlanChargeType.Credit,ChargeDate=DateTime.Today.AddDays(-90) },
				new PayPlanCharge { PayPlanChargeNum=2,ChargeType=PayPlanChargeType.Debit,ChargeDate=DateTime.Today.AddDays(-60) },
				new PayPlanCharge { PayPlanChargeNum=3,ChargeType=PayPlanChargeType.Debit,ChargeDate=DateTime.Today.AddDays(-30),LinkType=PayPlanLinkType.Procedure,FKey=55 },
			};
			Assert.AreEqual(2,PaySplitPayPlanAttach.GetPayPlanChargeNumAfterTypeChange(10,0,_defNumPrepay,0,() => listPayPlanChargesDue,0,0),
				"The oldest due debit, as attaching picks it.");
			Assert.AreEqual(3,PaySplitPayPlanAttach.GetPayPlanChargeNumAfterTypeChange(10,0,_defNumPrepay,0,() => listPayPlanChargesDue,55,0),
				"Preferring the debit for the split's procedure.");
			Assert.AreEqual(0,PaySplitPayPlanAttach.GetPayPlanChargeNumAfterTypeChange(10,0,_defNumPrepay,0,() => new List<PayPlanCharge>(),0,0),"None due.");
			Assert.AreEqual(0,PaySplitPayPlanAttach.GetPayPlanChargeNumAfterTypeChange(10,0,_defNumPrepay,0,null,0,0),"No loader.");
		}

		[TestMethod]
		public void TypeChange_NotInvolvingPrepay_KeepsTheCharge_AndUnattachedSplitsHaveNone() {
			int countLoads=0;
			Func<List<PayPlanCharge>> funcGetCharges=() => { countLoads++; return new List<PayPlanCharge>(); };
			long defNumPrepayment=PrefC.GetLong(PrefName.PrepaymentUnearnedType);
			Assert.AreEqual(7,PaySplitPayPlanAttach.GetPayPlanChargeNumAfterTypeChange(10,7,0,defNumPrepayment,funcGetCharges,0,0));
			Assert.AreEqual(7,PaySplitPayPlanAttach.GetPayPlanChargeNumAfterTypeChange(10,7,defNumPrepayment,0,funcGetCharges,0,0));
			Assert.AreEqual(0,countLoads);
			Assert.AreEqual(0,PaySplitPayPlanAttach.GetPayPlanChargeNumAfterTypeChange(0,7,_defNumPrepay,0,funcGetCharges,0,0),"Not attached to a plan.");
		}

		[TestMethod]
		public void TypeChange_OnTheSplit_ClearsAndRestoresTheChargeAndItsDebitType() {
			List<PayPlanCharge> listPayPlanChargesDue=new List<PayPlanCharge> {
				new PayPlanCharge { PayPlanChargeNum=4,ChargeType=PayPlanChargeType.Debit,ChargeDate=DateTime.Today.AddDays(-10) },
			};
			//The window sequence: "Attached to Payment Plan" ticked while the type is None picks the due debit; then the Payment Plan Prepay type is chosen.
			PaySplit paySplit=new PaySplit { PayPlanNum=10,PayPlanChargeNum=4,PayPlanDebitType=PayPlanDebitTypes.Principal,UnearnedType=_defNumPrepay };
			Assert.IsTrue(PaySplitPayPlanAttach.SetPayPlanChargeAfterTypeChange(paySplit,0,() => listPayPlanChargesDue,0,0));
			Assert.AreEqual(0,paySplit.PayPlanChargeNum);
			Assert.AreEqual(PayPlanDebitTypes.Unknown,paySplit.PayPlanDebitType);
			Assert.AreEqual(10,paySplit.PayPlanNum,"The split stays on the plan as its prepayment.");
			Assert.IsFalse(PaySplitPayPlanAttach.SetPayPlanChargeAfterTypeChange(paySplit,_defNumPrepay,() => listPayPlanChargesDue,0,0),"Same type again.");
			//Back to None while still attached: a due debit is picked again.
			paySplit.UnearnedType=0;
			Assert.IsTrue(PaySplitPayPlanAttach.SetPayPlanChargeAfterTypeChange(paySplit,_defNumPrepay,() => listPayPlanChargesDue,0,0));
			Assert.AreEqual(4,paySplit.PayPlanChargeNum);
			Assert.AreEqual(PayPlanDebitTypes.Unknown,paySplit.PayPlanDebitType);
			//A change between other types keeps the charge and its debit type.
			paySplit.PayPlanDebitType=PayPlanDebitTypes.Interest;
			paySplit.UnearnedType=PrefC.GetLong(PrefName.PrepaymentUnearnedType);
			Assert.IsFalse(PaySplitPayPlanAttach.SetPayPlanChargeAfterTypeChange(paySplit,0,() => listPayPlanChargesDue,0,0));
			Assert.AreEqual(4,paySplit.PayPlanChargeNum);
			Assert.AreEqual(PayPlanDebitTypes.Interest,paySplit.PayPlanDebitType);
		}

		[TestMethod]
		public void PrepaymentOnAPlan_NeverKeepsACharge() {
			PaySplit paySplit=new PaySplit { PayPlanNum=10,PayPlanChargeNum=4,PayPlanDebitType=PayPlanDebitTypes.Principal,UnearnedType=_defNumPrepay };
			Assert.IsTrue(PaySplitPayPlanAttach.ClearChargeForPrepayment(paySplit));
			Assert.AreEqual(0,paySplit.PayPlanChargeNum);
			Assert.AreEqual(PayPlanDebitTypes.Unknown,paySplit.PayPlanDebitType);
			Assert.AreEqual(10,paySplit.PayPlanNum);
			Assert.IsFalse(PaySplitPayPlanAttach.ClearChargeForPrepayment(paySplit),"Nothing left to clear.");
			PaySplit paySplitOrdinary=new PaySplit { PayPlanNum=10,PayPlanChargeNum=4,PayPlanDebitType=PayPlanDebitTypes.Principal };
			Assert.IsFalse(PaySplitPayPlanAttach.ClearChargeForPrepayment(paySplitOrdinary));
			Assert.AreEqual(4,paySplitOrdinary.PayPlanChargeNum,"Ordinary plan payments keep their charge.");
			Assert.IsFalse(PaySplitPayPlanAttach.ClearChargeForPrepayment(null));
		}

		[TestMethod]
		public void TypeChange_WithARealPlan_AttachThenPrepayThenBack() {
			(Patient guarantor,Patient child)=CreateFamily("TypeChange");
			Procedure procedure=BPayDb.CreateProcedure(child,ProcStat.C,300);
			PayPlan payPlan=PayPlanT.CreateDynamicPaymentPlan(child.PatNum,guarantor.PatNum,DateTime.Today,0,0,50,new List<Procedure> { procedure },
				new List<Adjustment>(),PayPlanFrequency.Monthly,runService:false,doInsertChargeForToday:true);
			payPlan=PayPlans.GetOne(payPlan.PayPlanNum);
			Func<List<PayPlanCharge>> funcGetCharges=() => PayPlanCharges.GetDueForPayPlan(payPlan,payPlan.Guarantor);
			List<PayPlanCharge> listPayPlanChargesDue=funcGetCharges();
			Assert.IsTrue(listPayPlanChargesDue.Any(x => x.ChargeType==PayPlanChargeType.Debit),"Set up: the plan has an issued charge due today.");
			//"Attached to Payment Plan" ticked while the unearned type is None.
			PaySplit paySplit=new PaySplit { PatNum=child.PatNum,PayPlanNum=payPlan.PayPlanNum,SplitAmt=50 };
			paySplit.PayPlanChargeNum=PaySplitPayPlanAttach.GetPayPlanChargeNum(listPayPlanChargesDue,paySplit.UnearnedType,0,0);
			long payPlanChargeNumDue=paySplit.PayPlanChargeNum;
			Assert.AreNotEqual(0,payPlanChargeNumDue);
			//Then the Payment Plan Prepay type is chosen.
			paySplit.UnearnedType=_defNumPrepay;
			PaySplitPayPlanAttach.SetPayPlanChargeAfterTypeChange(paySplit,0,funcGetCharges,0,0);
			Assert.AreEqual(0,paySplit.PayPlanChargeNum,"The prepayment no longer pays the charge, so the money is not applied twice.");
			Assert.AreEqual(payPlan.PayPlanNum,paySplit.PayPlanNum);
			//And back to None.
			paySplit.UnearnedType=0;
			PaySplitPayPlanAttach.SetPayPlanChargeAfterTypeChange(paySplit,_defNumPrepay,funcGetCharges,0,0);
			Assert.AreEqual(payPlanChargeNumDue,paySplit.PayPlanChargeNum);
		}

		[TestMethod]
		public void AuditText_AddsThePayPlanNumber() {
			Assert.AreEqual("",PaySplitPayPlanAttach.GetPayPlanLogText(0));
			Assert.AreEqual(", payment plan #42",PaySplitPayPlanAttach.GetPayPlanLogText(42));
		}
	}
}
