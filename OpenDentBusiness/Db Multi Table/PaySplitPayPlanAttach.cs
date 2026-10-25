// Which payment plans a paysplit may be attached to. Open Dental 25.2 lets a split with the "Payment Plan Prepay" unearned type attach to a patient
// payment plan, and 25.3 (backported to 25.2.55) allows attaching paysplits to payment plans in more cases. Backlog package B-PAY
// (docs/specs/port-backlog.md); behavior in docs/specs/features-25.2.md (v25_2-account-payplan-prepay-attach) and docs/specs/features-25.3.md
// (v25_3-account-paysplit-payplan-attach). Public sources name none of the 25.3 cases, so the cases below are our inference (docs/port-notes/B-PAY.md).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using CodeBase;

namespace OpenDentBusiness {
	///<summary>Rules for attaching a paysplit to a payment plan from the Paysplit window. The window asks GetOptions which plans it may offer and shows
	///the returned message when there are none.
	///
	///Kept from 24.3: insurance payment plans are never offered, and treatment planned unearned (an unearned type on a TP procedure) cannot be applied
	///to a payment plan.
	///Payment Plan Prepay (25.2): a split whose unearned type is the preference DynamicPayPlanPrepaymentUnearnedType may attach to a patient payment
	///plan (a dynamic plan, the plans Open Dental calls Patient Payment Plans), where the existing dynamic plan logic applies it to future charges. It
	///cannot also have a procedure or an adjustment, and it is never attached to a charge.
	///More cases (25.3, inferred): a split whose procedure or adjustment is production (a credit) on a patient payment plan may attach to that plan
	///even when the split's patient is neither the plan's patient nor its guarantor; and a split for a patient of another family may attach to that
	///patient's plans whose guarantor is in the paying family (24.3 refused every split from another family).</summary>
	public class PaySplitPayPlanAttach {
		///<summary>What the Paysplit window may offer. ErrorMsg is "" when ListPayPlans has at least one plan.</summary>
		public class AttachOptions {
			public List<PayPlan> ListPayPlans=new List<PayPlan>();
			public string ErrorMsg="";

			public bool IsAllowed {
				get {
					return ErrorMsg=="" && ListPayPlans.Count>0;
				}
			}
		}

		///<summary>The split and its family, as the Paysplit window has them. The plan lists are filled by GetOptions from the database, or by tests.
		///</summary>
		public class AttachContext {
			///<summary>The patient the split is for.</summary>
			public long PatNum;
			///<summary>"Is from another family" is checked: the split's patient is not in the paying family.</summary>
			public bool IsOtherFamily;
			///<summary>The paying family (the Paysplit window's family).</summary>
			public List<long> ListFamilyPatNums=new List<long>();
			///<summary>The unearned type chosen for the split, 0 for none.</summary>
			public long UnearnedType;
			///<summary>The attached procedure, or null.</summary>
			public Procedure Procedure;
			///<summary>The attached adjustment's AdjNum, or 0.</summary>
			public long AdjNum;
			///<summary>Plans where the split's patient is the patient or the guarantor (PayPlans.GetForPatNum).</summary>
			public List<PayPlan> ListPayPlansForPat=new List<PayPlan>();
			///<summary>Plans that have the split's procedure or adjustment as production (GetPayPlansForProduction).</summary>
			public List<PayPlan> ListPayPlansForProduction=new List<PayPlan>();
		}

		///<summary>True when the unearned type is the Payment Plan Prepay type (preference DynamicPayPlanPrepaymentUnearnedType).</summary>
		public static bool IsPrepayType(long unearnedType) {
			Meth.NoCheckMiddleTierRole();
			if(unearnedType==0) {
				return false;
			}
			return unearnedType==PrefC.GetLong(PrefName.DynamicPayPlanPrepaymentUnearnedType);
		}

		///<summary>The unearned types the Paysplit window lists: the visible ones and, even when its definition is hidden, the Payment Plan Prepay type,
		///which a user needs here to make a payment plan prepayment.</summary>
		public static List<Def> GetUnearnedDefsForSplitEdit() {
			Meth.NoCheckMiddleTierRole();
			List<Def> listDefs=Defs.GetDefsForCategory(DefCat.PaySplitUnearnedType,true);
			long defNumPrepay=PrefC.GetLong(PrefName.DynamicPayPlanPrepaymentUnearnedType);
			if(defNumPrepay!=0 && !listDefs.Any(x => x.DefNum==defNumPrepay)) {
				Def defPrepay=Defs.GetDef(DefCat.PaySplitUnearnedType,defNumPrepay);
				if(defPrepay!=null) {
					listDefs.Add(defPrepay);
				}
			}
			return listDefs;
		}

		///<summary>Patient payment plans (no insurance plans) that have the procedure or adjustment as production: dynamic plans through payplanlink,
		///old-style plans through their credits (payplancharge credits with the ProcNum).</summary>
		public static List<PayPlan> GetPayPlansForProduction(long procNum,long adjNum) {
			Meth.NoCheckMiddleTierRole();
			List<long> listPayPlanNums=new List<long>();
			if(procNum>0) {
				listPayPlanNums.AddRange(PayPlanLinks.GetForFKeyAndLinkType(procNum,PayPlanLinkType.Procedure).Select(x => x.PayPlanNum));
				listPayPlanNums.AddRange(PayPlanCharges.GetPatientPayPlanCreditsForProcs(new List<long> { procNum }).Select(x => x.PayPlanNum));
			}
			if(adjNum>0) {
				listPayPlanNums.AddRange(PayPlanLinks.GetForFKeyAndLinkType(adjNum,PayPlanLinkType.Adjustment).Select(x => x.PayPlanNum));
			}
			listPayPlanNums=listPayPlanNums.Where(x => x>0).Distinct().ToList();
			if(listPayPlanNums.Count==0) {
				return new List<PayPlan>();
			}
			return PayPlans.GetMany(listPayPlanNums.ToArray()).FindAll(x => x.PlanNum==0);
		}

		///<summary>Loads the plans for the context from the database, then applies the rules.</summary>
		public static AttachOptions GetOptions(long patNum,bool isOtherFamily,List<long> listFamilyPatNums,long unearnedType,Procedure procedure,long adjNum) {
			Meth.NoCheckMiddleTierRole();
			AttachContext attachContext=new AttachContext();
			attachContext.PatNum=patNum;
			attachContext.IsOtherFamily=isOtherFamily;
			attachContext.ListFamilyPatNums=listFamilyPatNums??new List<long>();
			attachContext.UnearnedType=unearnedType;
			attachContext.Procedure=procedure;
			attachContext.AdjNum=adjNum;
			if(patNum>0) {
				attachContext.ListPayPlansForPat=PayPlans.GetForPatNum(patNum);
			}
			attachContext.ListPayPlansForProduction=GetPayPlansForProduction(procedure?.ProcNum??0,adjNum);
			return GetOptions(attachContext);
		}

		///<summary>The rules, on plans already loaded. No database access except the preference cache.</summary>
		public static AttachOptions GetOptions(AttachContext attachContext) {
			Meth.NoCheckMiddleTierRole();
			AttachOptions attachOptions=new AttachOptions();
			bool isPrepay=IsPrepayType(attachContext.UnearnedType);
			Procedure procedure=attachContext.Procedure;
			if(!isPrepay && attachContext.UnearnedType!=0 && procedure!=null && procedure.ProcStatus==ProcStat.TP) {
				attachOptions.ErrorMsg=Lans.g("FormPaySplitEdit","Treatment planned unearned cannot be applied to payment plans.");
				return attachOptions;
			}
			if(isPrepay && (procedure!=null || attachContext.AdjNum!=0)) {
				attachOptions.ErrorMsg=Lans.g("FormPaySplitEdit","A payment plan prepayment cannot also be attached to a procedure or an adjustment.");
				return attachOptions;
			}
			List<PayPlan> listPayPlans=new List<PayPlan>();
			listPayPlans.AddRange(attachContext.ListPayPlansForPat);
			listPayPlans.AddRange(attachContext.ListPayPlansForProduction);
			listPayPlans=listPayPlans.Where(x => x!=null && x.PlanNum==0)//never insurance payment plans
				.GroupBy(x => x.PayPlanNum).Select(x => x.First())
				.OrderBy(x => x.PayPlanDate).ThenBy(x => x.PayPlanNum)
				.ToList();
			if(attachContext.IsOtherFamily) {
				//The money comes from the paying family, so only plans that family guarantees.
				listPayPlans=listPayPlans.FindAll(x => attachContext.ListFamilyPatNums.Contains(x.Guarantor));
			}
			if(isPrepay) {
				//Only patient (dynamic) payment plans apply plan prepayments to their charges.
				listPayPlans=listPayPlans.FindAll(x => x.IsDynamic);
			}
			attachOptions.ListPayPlans=listPayPlans;
			if(listPayPlans.Count>0) {
				return attachOptions;
			}
			if(attachContext.IsOtherFamily) {
				attachOptions.ErrorMsg=Lans.g("FormPaySplitEdit","The selected patient has no payment plans with a guarantor in this family.");
			}
			else if(isPrepay) {
				attachOptions.ErrorMsg=Lans.g("FormPaySplitEdit","The selected patient is not the patient or guarantor of any patient payment plans.");
			}
			else {
				attachOptions.ErrorMsg=Lans.g("FormPaySplitEdit","The selected patient is not the patient or guarantor of any payment plans.");
			}
			return attachOptions;
		}

		///<summary>True when attaching this procedure or adjustment to a split can keep the split's payment plan: the plan has it as production, and
		///the split does not carry treatment planned unearned. 24.3 always detached the plan.</summary>
		public static bool CanKeepPayPlanForProduction(long payPlanNum,long unearnedType,Procedure procedure,long adjNum) {
			Meth.NoCheckMiddleTierRole();
			if(payPlanNum==0) {
				return false;
			}
			if(IsPrepayType(unearnedType)) {
				return false;//a prepayment cannot have production
			}
			if(unearnedType!=0 && procedure!=null && procedure.ProcStatus==ProcStat.TP) {
				return false;//treatment planned unearned
			}
			return GetPayPlansForProduction(procedure?.ProcNum??0,adjNum).Any(x => x.PayPlanNum==payPlanNum);
		}

		///<summary>True when the split's current plan is no longer valid for a split of this unearned type: a prepayment must be on a patient (dynamic)
		///payment plan.</summary>
		public static bool MustDetachForUnearnedType(PayPlan payPlan,long unearnedType) {
			Meth.NoCheckMiddleTierRole();
			if(payPlan==null || !IsPrepayType(unearnedType)) {
				return false;
			}
			return !payPlan.IsDynamic || payPlan.PlanNum!=0;
		}

		///<summary>The charge a split attached to the plan pays, or 0. A prepayment pays no charge. Otherwise the oldest due debit, preferring one for the
		///split's procedure or adjustment. listPayPlanChargesDue is what PayPlanCharges.GetDueForPayPlan returns (credits are ignored).</summary>
		public static long GetPayPlanChargeNum(List<PayPlanCharge> listPayPlanChargesDue,long unearnedType,long procNum,long adjNum) {
			Meth.NoCheckMiddleTierRole();
			if(IsPrepayType(unearnedType) || listPayPlanChargesDue.IsNullOrEmpty()) {
				return 0;
			}
			List<PayPlanCharge> listDebits=listPayPlanChargesDue.FindAll(x => x.ChargeType==PayPlanChargeType.Debit && x.PayPlanChargeNum!=0)
				.OrderBy(x => x.ChargeDate).ThenBy(x => x.PayPlanChargeNum).ToList();
			if(listDebits.Count==0) {
				return 0;
			}
			PayPlanCharge payPlanChargeForProduction=listDebits.FirstOrDefault(x =>
				(procNum>0 && ((x.LinkType==PayPlanLinkType.Procedure && x.FKey==procNum) || x.ProcNum==procNum))
				|| (adjNum>0 && x.LinkType==PayPlanLinkType.Adjustment && x.FKey==adjNum));
			if(payPlanChargeForProduction!=null) {
				return payPlanChargeForProduction.PayPlanChargeNum;
			}
			return listDebits[0].PayPlanChargeNum;
		}

		///<summary>" AND ..." that leaves out the plan prepayments (Payment Plan Prepay splits without a procedure or adjustment) from a paysplit query,
		///or "" when no prepay type is set up.</summary>
		public static string GetNotPrepayWhereClause() {
			Meth.NoCheckMiddleTierRole();
			long defNumPrepay=PrefC.GetLong(PrefName.DynamicPayPlanPrepaymentUnearnedType);
			if(defNumPrepay==0) {
				return "";
			}
			return " AND NOT (paysplit.UnearnedType="+POut.Long(defNumPrepay)+" AND paysplit.ProcNum=0 AND paysplit.AdjNum=0)";
		}

		///<summary>25.2: when a patient payment plan is deleted, its plan prepayments become unattached prepayments of the same unearned type. Called
		///by PayPlans.Delete after it made sure no other split is attached. Writes one PayPlanEdit entry when anything was detached.</summary>
		public static void DetachPrepaymentsFromPayPlan(PayPlan payPlan) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),payPlan);
				return;
			}
			long defNumPrepay=PrefC.GetLong(PrefName.DynamicPayPlanPrepaymentUnearnedType);
			if(payPlan==null || defNumPrepay==0) {
				return;
			}
			string where="WHERE PayPlanNum="+POut.Long(payPlan.PayPlanNum)+" AND UnearnedType="+POut.Long(defNumPrepay)+" AND ProcNum=0 AND AdjNum=0";
			DataTable table=Db.GetTable("SELECT COUNT(*) CountSplits,COALESCE(SUM(SplitAmt),0) SumSplits FROM paysplit "+where);
			int countSplits=PIn.Int(table.Rows[0]["CountSplits"].ToString());
			if(countSplits==0) {
				return;
			}
			double sumSplits=PIn.Double(table.Rows[0]["SumSplits"].ToString());
			Db.NonQ("UPDATE paysplit SET PayPlanNum=0,PayPlanChargeNum=0 "+where);//neither column is in the paysplit row hash
			string logText=countSplits+" payment plan prepayment split(s) totaling "+PayPlanAudits.RoundToCents(sumSplits).ToString("N2")
				+" detached from deleted payment plan #"+payPlan.PayPlanNum+"; they stay as unearned.";
			if(Security.CurUser!=null) {
				SecurityLogs.MakeLogEntry(EnumPermType.PayPlanEdit,payPlan.PatNum,logText,payPlan.PayPlanNum,DateTime.MinValue);
			}
		}

		///<summary>The audit text part for a split's payment plan: ", payment plan #N" for a new split, "" when not attached.</summary>
		public static string GetPayPlanLogText(long payPlanNum) {
			if(payPlanNum==0) {
				return "";
			}
			return ", payment plan #"+payPlanNum;
		}
	}
}
