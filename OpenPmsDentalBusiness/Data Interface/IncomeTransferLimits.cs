using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>Open Dental 25.4 behavior, written for this fork (backlog package M2-PAYW, feature v25_4-account-incometransfer-limit): with the
	///preference IncomeTransfersLimitToPatPayments on, a manual income transfer may move only money that came from patient payments. Insurance
	///income and credit adjustments never sit in paysplits, so they can never be moved into patient credit, and no source can give more than the
	///patient payments it still holds.
	///Sources: a transfer takes money with negative splits. Each negative split takes from a "source": the procedure, the adjustment or the payment
	///plan it names, or else the patient's unearned income (all unearned types together, as the Payment window pools them in transfer mode), or
	///else the patient's unallocated income. The money a source holds
	///from patient payments is the sum of the stored paysplits of every other payment on that source (real payments and earlier transfers alike;
	///earlier transfers move patient money between sources, so the sums stay a partition of it). Open Dental's per-paysplit rule cannot be used,
	///because paysplit.FSplitNum is no longer filled in (INFERRED, see docs/port-notes/M2-PAYW.md).
	///Only what a transfer takes beyond what its stored version already took is checked, so an old transfer saved again without changes is never
	///blocked. The automatic Transfer and FIFO tools are not limited (the Payment window passes their untouched result through).</summary>
	public class IncomeTransferLimits {
		///<summary>Amounts within half a cent are equal.</summary>
		private const decimal EPSILON=0.005m;

		///<summary>True when the preference is on.</summary>
		public static bool IsEnabled() {
			Meth.NoCheckMiddleTierRole();
			return PrefC.GetBool(PrefName.IncomeTransfersLimitToPatPayments);
		}

		#region Sources
		///<summary>Returns the source a split takes money from (when negative) or gives money to (when positive).</summary>
		public static TransferSource GetSource(PaySplit paySplit) {
			Meth.NoCheckMiddleTierRole();
			if(paySplit.ProcNum>0) {
				return new TransferSource(EnumTransferSourceKind.Procedure,paySplit.ProcNum,0);
			}
			if(paySplit.AdjNum>0) {
				return new TransferSource(EnumTransferSourceKind.Adjustment,paySplit.AdjNum,0);
			}
			if(paySplit.PayPlanNum>0) {
				return new TransferSource(EnumTransferSourceKind.PayPlan,paySplit.PayPlanNum,0);
			}
			if(paySplit.UnearnedType>0) {
				return new TransferSource(EnumTransferSourceKind.Unearned,0,paySplit.PatNum);
			}
			return new TransferSource(EnumTransferSourceKind.Unallocated,0,paySplit.PatNum);
		}

		///<summary>Sum of the splits per source.</summary>
		private static Dictionary<TransferSource,decimal> SumBySource(IEnumerable<PaySplit> listPaySplits) {
			Dictionary<TransferSource,decimal> dict=new Dictionary<TransferSource,decimal>();
			foreach(PaySplit paySplit in listPaySplits) {
				TransferSource transferSource=GetSource(paySplit);
				dict.TryGetValue(transferSource,out decimal amount);
				dict[transferSource]=amount+(decimal)paySplit.SplitAmt;
			}
			return dict;
		}
		#endregion Sources

		#region Rule
		///<summary>The sources that the transfer takes too much from. Pure, so it can be tested without a database.
		///listPaySplitsCur: the transfer's splits as they are about to be saved.
		///listPaySplitsSaved: the same payment's splits as stored before this edit (empty for a new transfer).
		///listPaySplitsOther: stored splits of every other payment; splits on sources that this transfer does not take from are ignored.</summary>
		public static List<IncomeTransferLimitViolation> GetViolations(List<PaySplit> listPaySplitsCur,List<PaySplit> listPaySplitsSaved,
			List<PaySplit> listPaySplitsOther)
		{
			Meth.NoCheckMiddleTierRole();
			List<IncomeTransferLimitViolation> listViolations=new List<IncomeTransferLimitViolation>();
			Dictionary<TransferSource,decimal> dictCur=SumBySource(listPaySplitsCur??new List<PaySplit>());
			Dictionary<TransferSource,decimal> dictSaved=SumBySource(listPaySplitsSaved??new List<PaySplit>());
			Dictionary<TransferSource,decimal> dictOther=SumBySource(listPaySplitsOther??new List<PaySplit>());
			foreach(KeyValuePair<TransferSource,decimal> keyValuePair in dictCur) {
				decimal amountTaken=-keyValuePair.Value;
				if(amountTaken<=EPSILON) {
					continue;//This transfer gives to the source, or takes nothing from it overall.
				}
				dictSaved.TryGetValue(keyValuePair.Key,out decimal amountSaved);
				decimal amountTakenBefore=Math.Max(0,-amountSaved);
				if(amountTaken<=amountTakenBefore+EPSILON) {
					continue;//Takes no more than the stored version of this transfer already took.
				}
				dictOther.TryGetValue(keyValuePair.Key,out decimal amountAvailable);
				if(amountTaken<=amountAvailable+EPSILON) {
					continue;
				}
				IncomeTransferLimitViolation violation=new IncomeTransferLimitViolation();
				violation.Source=keyValuePair.Key;
				violation.AmountTaken=amountTaken;
				violation.AmountAvailable=Math.Max(0,amountAvailable);
				listViolations.Add(violation);
			}
			return listViolations
				.OrderBy(x => x.Source.Kind)
				.ThenBy(x => x.Source.FKey)
				.ThenBy(x => x.Source.PatNum)
				.ToList();
		}

		///<summary>Loads the stored splits that the rule needs and returns the sources that the transfer takes too much from, with descriptions for the
		///user. payNum is the transfer's payment (0 when it has not been inserted yet).</summary>
		public static List<IncomeTransferLimitViolation> GetViolations(long payNum,List<PaySplit> listPaySplitsCur) {
			Meth.NoCheckMiddleTierRole();
			if(listPaySplitsCur.IsNullOrEmpty()) {
				return new List<IncomeTransferLimitViolation>();
			}
			List<TransferSource> listSourcesTaken=SumBySource(listPaySplitsCur)
				.Where(x => x.Value < -EPSILON)
				.Select(x => x.Key)
				.ToList();
			if(listSourcesTaken.Count==0) {
				return new List<IncomeTransferLimitViolation>();
			}
			List<PaySplit> listPaySplitsSaved=new List<PaySplit>();
			if(payNum>0) {
				listPaySplitsSaved=PaySplits.GetForPayment(payNum);
			}
			List<PaySplit> listPaySplitsOther=GetSplitsOnSources(payNum,
				listSourcesTaken.Where(x => x.Kind==EnumTransferSourceKind.Procedure).Select(x => x.FKey).ToList(),
				listSourcesTaken.Where(x => x.Kind==EnumTransferSourceKind.Adjustment).Select(x => x.FKey).ToList(),
				listSourcesTaken.Where(x => x.Kind==EnumTransferSourceKind.PayPlan).Select(x => x.FKey).ToList(),
				listSourcesTaken.Where(x => x.Kind.In(EnumTransferSourceKind.Unearned,EnumTransferSourceKind.Unallocated)).Select(x => x.PatNum).Distinct().ToList());
			List<IncomeTransferLimitViolation> listViolations=GetViolations(listPaySplitsCur,listPaySplitsSaved,listPaySplitsOther);
			for(int i=0;i<listViolations.Count;i++) {
				listViolations[i].Description=GetDescription(listViolations[i].Source);
			}
			return listViolations;
		}

		///<summary>Stored splits of other payments than payNum that sit on any of the given procedures, adjustments or payment plans, or that are
		///unearned or unallocated splits of the given patients.</summary>
		public static List<PaySplit> GetSplitsOnSources(long payNum,List<long> listProcNums,List<long> listAdjNums,List<long> listPayPlanNums,
			List<long> listPatNumsUnattached)
		{
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<PaySplit>>(MethodBase.GetCurrentMethod(),payNum,listProcNums,listAdjNums,listPayPlanNums,listPatNumsUnattached);
			}
			List<string> listClauses=new List<string>();
			if(!listProcNums.IsNullOrEmpty()) {
				listClauses.Add("ProcNum IN ("+string.Join(",",listProcNums.Select(x => POut.Long(x)))+")");
			}
			if(!listAdjNums.IsNullOrEmpty()) {
				listClauses.Add("AdjNum IN ("+string.Join(",",listAdjNums.Select(x => POut.Long(x)))+")");
			}
			if(!listPayPlanNums.IsNullOrEmpty()) {
				listClauses.Add("PayPlanNum IN ("+string.Join(",",listPayPlanNums.Select(x => POut.Long(x)))+")");
			}
			if(!listPatNumsUnattached.IsNullOrEmpty()) {
				listClauses.Add("(ProcNum=0 AND AdjNum=0 AND PayPlanNum=0 AND PatNum IN ("+string.Join(",",listPatNumsUnattached.Select(x => POut.Long(x)))+"))");
			}
			if(listClauses.Count==0) {
				return new List<PaySplit>();
			}
			string command="SELECT * FROM paysplit WHERE PayNum!="+POut.Long(payNum)+" AND ("+string.Join(" OR ",listClauses)+")";
			return Crud.PaySplitCrud.SelectMany(command);
		}
		#endregion Rule

		#region Text
		///<summary>Names a source for the user, for example "Procedure D0120 on 01/15/2026 (Smith, Jane)".</summary>
		public static string GetDescription(TransferSource transferSource) {
			Meth.NoCheckMiddleTierRole();
			try {
				switch(transferSource.Kind) {
					case EnumTransferSourceKind.Procedure:
						Procedure procedure=Procedures.GetOneProc(transferSource.FKey,false);
						if(procedure==null || procedure.ProcNum==0) {
							break;
						}
						return Lans.g("IncomeTransferLimits","Procedure")+" "+ProcedureCodes.GetStringProcCode(procedure.CodeNum)+" "
							+Lans.g("IncomeTransferLimits","on")+" "+procedure.ProcDate.ToShortDateString()+GetPatientSuffix(procedure.PatNum);
					case EnumTransferSourceKind.Adjustment:
						Adjustment adjustment=Adjustments.GetOne(transferSource.FKey);
						if(adjustment==null) {
							break;
						}
						return Lans.g("IncomeTransferLimits","Adjustment")+" "+Defs.GetName(DefCat.AdjTypes,adjustment.AdjType)+" "
							+Lans.g("IncomeTransferLimits","on")+" "+adjustment.AdjDate.ToShortDateString()+GetPatientSuffix(adjustment.PatNum);
					case EnumTransferSourceKind.PayPlan:
						PayPlan payPlan=PayPlans.GetOne(transferSource.FKey);
						if(payPlan==null) {
							break;
						}
						return Lans.g("IncomeTransferLimits","Payment plan from")+" "+payPlan.PayPlanDate.ToShortDateString()+GetPatientSuffix(payPlan.PatNum);
					case EnumTransferSourceKind.Unearned:
						return Lans.g("IncomeTransferLimits","Unearned income")+GetPatientSuffix(transferSource.PatNum);
					case EnumTransferSourceKind.Unallocated:
						return Lans.g("IncomeTransferLimits","Unallocated income")+GetPatientSuffix(transferSource.PatNum);
				}
			}
			catch(Exception) {
				//Fall through to the generic text: the description must never stop the check itself.
			}
			return transferSource.ToString();
		}

		private static string GetPatientSuffix(long patNum) {
			Patient patient=Patients.GetLim(patNum);
			if(patient==null || patient.PatNum==0) {
				return "";
			}
			return " ("+patient.GetNameLF()+")";
		}

		///<summary>The blocking message: one line per source, with what the transfer takes and what patient payments it still holds.</summary>
		public static string GetMessage(List<IncomeTransferLimitViolation> listViolations) {
			Meth.NoCheckMiddleTierRole();
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.AppendLine(Lans.g("IncomeTransferLimits","Manual income transfers are limited to the amount of patient payments."));
			stringBuilder.AppendLine(Lans.g("IncomeTransferLimits","These sources do not hold enough patient payments for this transfer:"));
			for(int i=0;i<listViolations.Count;i++) {
				string description=listViolations[i].Description;
				if(string.IsNullOrEmpty(description)) {
					description=listViolations[i].Source.ToString();
				}
				stringBuilder.AppendLine("  "+description+": "+Lans.g("IncomeTransferLimits","takes")+" "+listViolations[i].AmountTaken.ToString("c")
					+", "+Lans.g("IncomeTransferLimits","available")+" "+listViolations[i].AmountAvailable.ToString("c"));
			}
			return stringBuilder.ToString().TrimEnd();
		}
		#endregion Text

		#region Automatic transfers
		///<summary>A value fingerprint of a set of splits (order does not matter). The Payment window keeps the fingerprint of the splits that its
		///automatic Transfer button made, and skips the limit when the user saves them unchanged.</summary>
		public static string GetFingerprint(List<PaySplit> listPaySplits) {
			Meth.NoCheckMiddleTierRole();
			if(listPaySplits==null) {
				return "";
			}
			List<string> listParts=listPaySplits
				.Select(x => string.Join("|",x.PatNum,x.ProvNum,x.ClinicNum,x.ProcNum,x.AdjNum,x.PayPlanNum,x.PayPlanChargeNum,(int)x.PayPlanDebitType,
					x.UnearnedType,Math.Round((decimal)x.SplitAmt,2).ToString(CultureInfo.InvariantCulture)))
				.OrderBy(x => x,StringComparer.Ordinal)
				.ToList();
			return string.Join(";",listParts);
		}
		#endregion Automatic transfers
	}

	///<summary>What kind of thing an income transfer split takes money from.</summary>
	public enum EnumTransferSourceKind {
		Procedure,
		Adjustment,
		PayPlan,
		Unearned,
		Unallocated,
	}

	///<summary>A procedure, adjustment or payment plan (FKey), or a patient's unearned income, or a patient's unallocated income.</summary>
	[Serializable]
	public class TransferSource:IEquatable<TransferSource> {
		public EnumTransferSourceKind Kind;
		///<summary>ProcNum, AdjNum or PayPlanNum; 0 for unearned and unallocated.</summary>
		public long FKey;
		///<summary>The patient, for unearned and unallocated; 0 otherwise.</summary>
		public long PatNum;

		///<summary>For serialization.</summary>
		public TransferSource() {
		}

		public TransferSource(EnumTransferSourceKind kind,long fKey,long patNum) {
			Kind=kind;
			FKey=fKey;
			PatNum=patNum;
		}

		public bool Equals(TransferSource other) {
			if(other is null) {
				return false;
			}
			return Kind==other.Kind && FKey==other.FKey && PatNum==other.PatNum;
		}

		public override bool Equals(object obj) {
			return Equals(obj as TransferSource);
		}

		public override int GetHashCode() {
			unchecked {
				int hash=(int)Kind;
				hash=hash*397^FKey.GetHashCode();
				hash=hash*397^PatNum.GetHashCode();
				return hash;
			}
		}

		public override string ToString() {
			switch(Kind) {
				case EnumTransferSourceKind.Unearned:
					return "Unearned, PatNum #"+PatNum;
				case EnumTransferSourceKind.Unallocated:
					return "Unallocated, PatNum #"+PatNum;
				default:
					return Kind+" #"+FKey;
			}
		}
	}

	///<summary>One source that a manual income transfer takes too much from.</summary>
	[Serializable]
	public class IncomeTransferLimitViolation {
		public TransferSource Source;
		///<summary>What the transfer takes from the source overall (positive).</summary>
		public decimal AmountTaken;
		///<summary>What patient payments the source holds, not counting this transfer (never negative).</summary>
		public decimal AmountAvailable;
		///<summary>Names the source for the user.</summary>
		public string Description="";
	}
}
