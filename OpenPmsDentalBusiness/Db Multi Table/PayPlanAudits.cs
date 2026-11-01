// Audit trail entries for payment plan amounts: the Tx Completed Amount (Open Dental 25.1) and the down payment (Open Dental 25.2). Backlog
// package B-PAY (docs/specs/port-backlog.md); behavior in docs/specs/features-25.1.md (v25_1-account-payplan-completedamt-audit) and
// docs/specs/features-25.2.md (v25_2-account-payplan-downpayment-audit). The wording is ours.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenPmsDentalBusiness {
	///<summary>Builds and writes the PayPlanEdit audit entries for changes to a payment plan's Tx Completed Amount and down payment. Every entry
	///carries the plan's PatNum and the PayPlanNum as FKey. Nothing is logged when an amount is unchanged once rounded to cents.</summary>
	public class PayPlanAudits {
		///<summary>Start of every down payment entry, so the entries can be found in the Audit Trail.</summary>
		public const string DOWN_PAYMENT_PREFIX="Down Payment Changed";

		///<summary>True when the two amounts differ once rounded to cents.</summary>
		public static bool IsChangedToCents(double amountOld,double amountNew) {
			return RoundToCents(amountOld)!=RoundToCents(amountNew);
		}

		///<summary>The amount rounded to cents the way money is rounded everywhere else (away from zero).</summary>
		public static decimal RoundToCents(double amount) {
			return Math.Round((decimal)amount,2,MidpointRounding.AwayFromZero);
		}

		///<summary>"Patient ", "Insurance " or "" (dynamic plans), the same prefix PayPlanL uses for the other payment plan entries.</summary>
		public static string GetPlanTypePrefix(PayPlan payPlan) {
			if(payPlan.PlanNum!=0) {
				return "Insurance ";
			}
			if(payPlan.IsDynamic) {
				return "";
			}
			return "Patient ";
		}

		///<summary>For example "Tx Completed Amt changed from 1,200.00 to 1,350.00 (+150.00), after procedure #45 was set complete." Returns ""
		///when the amount did not change. The trigger, when known, says what caused an automatic recalculation.</summary>
		public static string GetCompletedAmtChangeText(double amountOld,double amountNew,string trigger="") {
			if(!IsChangedToCents(amountOld,amountNew)) {
				return "";
			}
			decimal difference=RoundToCents(amountNew)-RoundToCents(amountOld);
			string text="Tx Completed Amt changed from "+FormatAmount(amountOld)+" to "+FormatAmount(amountNew)+" ("+FormatDifference(difference)+")";
			if(!string.IsNullOrWhiteSpace(trigger)) {
				text+=", "+trigger.Trim();
			}
			return text+".";
		}

		///<summary>For example "Down Payment Changed from 200.00 to 150.00, PayPlanNum 12." Removing the down payment says "(removed)". The detail,
		///when given, says which part of the plan changed (for example the down payment charges). Returns "" when the amount did not change.</summary>
		public static string GetDownPaymentChangeText(double amountOld,double amountNew,long payPlanNum,string detail="") {
			if(!IsChangedToCents(amountOld,amountNew)) {
				return "";
			}
			string text=DOWN_PAYMENT_PREFIX+" from "+FormatAmount(amountOld)+" to "+FormatAmount(amountNew);
			List<string> listDetails=new List<string>();
			if(!string.IsNullOrWhiteSpace(detail)) {
				listDetails.Add(detail.Trim());
			}
			if(RoundToCents(amountNew)==0) {
				listDetails.Add("removed");
			}
			if(listDetails.Count>0) {
				text+=" ("+string.Join(", ",listDetails)+")";
			}
			return text+", PayPlanNum "+payPlanNum+".";
		}

		///<summary>The one down payment entry for a save of a payment plan, or "". A change of the down payment in the plan terms wins; otherwise a
		///change of the down payment charges (edited, deleted or replaced on the amortization schedule) is logged. So a new schedule made for a new
		///down payment is logged once.</summary>
		public static string GetDownPaymentLogText(double termsOld,double termsNew,double chargesOld,double chargesNew,long payPlanNum) {
			string text=GetDownPaymentChangeText(termsOld,termsNew,payPlanNum);
			if(text!="") {
				return text;
			}
			return GetDownPaymentChangeText(chargesOld,chargesNew,payPlanNum,"down payment charges");
		}

		///<summary>Sum of the principal of the down payment debits (payplancharge.IsDownPayment) in the list.</summary>
		public static double SumDownPaymentCharges(List<PayPlanCharge> listPayPlanCharges) {
			if(listPayPlanCharges==null) {
				return 0;
			}
			return listPayPlanCharges.Where(x => x.ChargeType==PayPlanChargeType.Debit && x.IsDownPayment).Sum(x => x.Principal);
		}

		///<summary>Text for an automatic recalculation caused by a procedure: "after procedure " and its code, then "was set complete" or "changed
		///status".</summary>
		public static string GetProcedureTrigger(Procedure procedure) {
			if(procedure==null) {
				return "";
			}
			string procCode=ProcedureCodes.GetStringProcCode(procedure.CodeNum,doThrowIfMissing:false);
			string what=string.IsNullOrEmpty(procCode) ? "procedure #"+procedure.ProcNum : "procedure "+procCode;
			if(procedure.ProcStatus==ProcStat.C) {
				return "after "+what+" was set complete";
			}
			return "after "+what+" changed status";
		}

		///<summary>Writes the completed amount entry for the plan when the amount changed. Returns true if an entry was written.</summary>
		public static bool LogCompletedAmtChange(PayPlan payPlan,double amountOld,double amountNew,string trigger="") {
			Meth.NoCheckMiddleTierRole();
			string text=GetCompletedAmtChangeText(amountOld,amountNew,trigger);
			if(payPlan==null || text=="") {
				return false;
			}
			MakeLogEntry(payPlan.PatNum,GetPlanTypePrefix(payPlan)+"Payment Plan "+text,payPlan.PayPlanNum);
			return true;
		}

		///<summary>Writes the down payment entry for the plan when the down payment changed. Returns true if an entry was written.</summary>
		public static bool LogDownPaymentChange(PayPlan payPlan,double termsOld,double termsNew,double chargesOld,double chargesNew) {
			Meth.NoCheckMiddleTierRole();
			if(payPlan==null) {
				return false;
			}
			string text=GetDownPaymentLogText(termsOld,termsNew,chargesOld,chargesNew,payPlan.PayPlanNum);
			if(text=="") {
				return false;
			}
			MakeLogEntry(payPlan.PatNum,text,payPlan.PayPlanNum);
			return true;
		}

		///<summary>A PayPlanEdit entry. Automatic recalculations can run where no user is logged on (a service), so the entry then has user 0.</summary>
		private static void MakeLogEntry(long patNum,string logText,long payPlanNum) {
			if(Security.CurUser!=null) {
				SecurityLogs.MakeLogEntry(EnumPermType.PayPlanEdit,patNum,logText,payPlanNum,DateTime.MinValue);
				return;
			}
			SecurityLog securityLog=new SecurityLog();
			securityLog.PermType=EnumPermType.PayPlanEdit;
			securityLog.UserNum=0;
			securityLog.LogText=logText;
			securityLog.CompName=Security.GetComplexComputerName();
			securityLog.PatNum=patNum;
			securityLog.FKey=payPlanNum;
			securityLog.LogSource=SecurityLogs.LogSource;
			securityLog.DateTPrevious=DateTime.MinValue;
			SecurityLogs.MakeLogEntry(securityLog);
		}

		private static string FormatAmount(double amount) {
			return RoundToCents(amount).ToString("N2");
		}

		private static string FormatDifference(decimal difference) {
			if(difference>0) {
				return "+"+difference.ToString("N2");
			}
			return difference.ToString("N2");
		}
	}
}
