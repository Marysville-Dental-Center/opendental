// Pay Labs when receiving a claim (Canada, Open Dental 25.4; backlog package M2-CAN, feature v25_4-canada-labfee-pay).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDentBusiness {
	///<summary>One parent procedure row of a claim payment with the lab rows attached to it.</summary>
	public class PayLabsGroup {
		public ClaimProc ClaimProcParent;
		///<summary>At most two, in the order they appear in the claim payment window.</summary>
		public List<ClaimProc> ListClaimProcsLab=new List<ClaimProc>();
	}

	public partial class ClaimProcs {
		///<summary>Canadian labs can be attached to a procedure (procedurelog.ProcNumLab), at most two per procedure.</summary>
		public const int PAY_LABS_MAX_LABS=2;

		///<summary>The procedure rows of a claim payment that have lab rows in the same payment: same claim, insurance plan and status as the
		///procedure. A procedure row without labs, a total payment row (ProcNum 0) and a lab row whose procedure is not in the payment are left out.
		///</summary>
		public static List<PayLabsGroup> GetPayLabsGroups(IList<ClaimProc> listClaimProcs,List<Procedure> listProcedures) {
			Meth.NoCheckMiddleTierRole();
			List<PayLabsGroup> listPayLabsGroups=new List<PayLabsGroup>();
			if(listClaimProcs==null || listProcedures==null) {
				return listPayLabsGroups;
			}
			Dictionary<long,Procedure> dictProcedures=new Dictionary<long,Procedure>();
			for(int i=0;i<listProcedures.Count;i++) {
				dictProcedures[listProcedures[i].ProcNum]=listProcedures[i];
			}
			for(int i=0;i<listClaimProcs.Count;i++) {
				ClaimProc claimProcParent=listClaimProcs[i];
				if(claimProcParent.ProcNum==0 || !dictProcedures.TryGetValue(claimProcParent.ProcNum,out Procedure procedureParent)
					|| procedureParent.ProcNumLab!=0)
				{
					continue;
				}
				PayLabsGroup payLabsGroup=new PayLabsGroup();
				payLabsGroup.ClaimProcParent=claimProcParent;
				for(int j=0;j<listClaimProcs.Count;j++) {
					ClaimProc claimProcLab=listClaimProcs[j];
					if(j==i || claimProcLab.ProcNum==0 || !dictProcedures.TryGetValue(claimProcLab.ProcNum,out Procedure procedureLab)
						|| procedureLab.ProcNumLab!=procedureParent.ProcNum
						|| claimProcLab.ClaimNum!=claimProcParent.ClaimNum
						|| claimProcLab.PlanNum!=claimProcParent.PlanNum
						|| claimProcLab.Status!=claimProcParent.Status)
					{
						continue;
					}
					if(payLabsGroup.ListClaimProcsLab.Count>=PAY_LABS_MAX_LABS) {
						break;
					}
					payLabsGroup.ListClaimProcsLab.Add(claimProcLab);
				}
				if(payLabsGroup.ListClaimProcsLab.Count>0) {
					listPayLabsGroups.Add(payLabsGroup);
				}
			}
			return listPayLabsGroups;
		}

		///<summary>True when the payment has a procedure row with lab rows, so Pay Labs has something to do.</summary>
		public static bool HasPayLabsGroups(IList<ClaimProc> listClaimProcs,List<Procedure> listProcedures) {
			Meth.NoCheckMiddleTierRole();
			return GetPayLabsGroups(listClaimProcs,listProcedures).Count>0;
		}

		///<summary>Pay Labs: for carriers that pay the whole amount against the main procedure. For each procedure row with lab rows, the insurance
		///payment on the procedure and its labs is added up and handed out again: each lab, in order, gets the smaller of its fee billed and what is
		///left, and the procedure keeps the rest. No amount goes below zero; when the payment is smaller than the labs, the procedure is left at zero
		///and the last lab gets less than its fee. Write-offs, deductibles and every other field are not touched, and nothing is saved: the window
		///shows the new amounts for review, and Cancel undoes them. Returns the number of rows whose payment changed. The exact split Open Dental
		///uses is not public. INFERRED.</summary>
		public static int PayLabs(IList<ClaimProc> listClaimProcs,List<Procedure> listProcedures) {
			Meth.NoCheckMiddleTierRole();
			int countChanged=0;
			List<PayLabsGroup> listPayLabsGroups=GetPayLabsGroups(listClaimProcs,listProcedures);
			for(int i=0;i<listPayLabsGroups.Count;i++) {
				PayLabsGroup payLabsGroup=listPayLabsGroups[i];
				decimal amountTotal=(decimal)payLabsGroup.ClaimProcParent.InsPayAmt;
				for(int j=0;j<payLabsGroup.ListClaimProcsLab.Count;j++) {
					amountTotal+=(decimal)payLabsGroup.ListClaimProcsLab[j].InsPayAmt;
				}
				amountTotal=Math.Round(amountTotal,2);
				if(amountTotal<=0) {
					continue;//Nothing paid, or a negative supplemental: leave it to the user.
				}
				decimal amountLeft=amountTotal;
				for(int j=0;j<payLabsGroup.ListClaimProcsLab.Count;j++) {
					ClaimProc claimProcLab=payLabsGroup.ListClaimProcsLab[j];
					decimal feeBilled=Math.Max(0,Math.Round((decimal)claimProcLab.FeeBilled,2));
					decimal amountLab=Math.Min(feeBilled,amountLeft);
					amountLeft-=amountLab;
					if(SetInsPayAmt(claimProcLab,amountLab)) {
						countChanged++;
					}
				}
				if(SetInsPayAmt(payLabsGroup.ClaimProcParent,amountLeft)) {
					countChanged++;
				}
			}
			return countChanged;
		}

		///<summary>Returns true if the amount changed.</summary>
		private static bool SetInsPayAmt(ClaimProc claimProc,decimal amount) {
			double amountNew=(double)amount;
			if(Math.Round(claimProc.InsPayAmt,2)==Math.Round(amountNew,2)) {
				return false;
			}
			claimProc.InsPayAmt=amountNew;
			return true;
		}
	}
}
