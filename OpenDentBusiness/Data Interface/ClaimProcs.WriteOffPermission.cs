// B-CLAIM, v25_3-security-inswriteoffedit-clarify: one place for the date that Insurance Write-off Edit (119) is checked against.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;

namespace OpenDentBusiness {
	public partial class ClaimProcs {
		///<summary>The date the Insurance Write-off Edit permission (119) checks for a claimproc: the attached procedure's DateEntryC, or the
		///claimproc's own SecDateEntry for a total payment (no procedure, or a procedure with no date). This is the rule Edit Claim Procedure has used
		///since 24.3, and the one Open Dental 25.3 spells out in the permission's description.</summary>
		public static DateTime GetInsWriteOffEditDate(ClaimProc claimProc,Procedure procedure) {
			Meth.NoCheckMiddleTierRole();
			if(claimProc.ProcNum!=0 && procedure!=null && procedure.ProcNum==claimProc.ProcNum && procedure.ProcDate!=DateTime.MinValue) {
				return procedure.DateEntryC;
			}
			return claimProc.SecDateEntry;
		}

		///<summary>Insurance Write-off Edit (119) for this claimproc, with its date or days limit. Open Dental 25.3: the permission covers editing the
		///Write-off and Write-off Override fields and also creating and deleting claimprocs, so a write-off outside the limit cannot be replaced by
		///deleting the claimproc and adding a new one. Pass the attached procedure, or null for a total payment.</summary>
		public static bool IsInsWriteOffEditAuthorized(ClaimProc claimProc,Procedure procedure,bool suppressMessage) {
			Meth.NoCheckMiddleTierRole();
			return Security.IsAuthorized(EnumPermType.InsWriteOffEdit,GetInsWriteOffEditDate(claimProc,procedure),suppressMessage);
		}

		///<summary>Insurance Write-off Edit (119) for a window whose write-off column covers several claimprocs at once (manual ERA posting,
		///FormEtrans835ClaimPay): true only when the user has 119 for every claimproc in the list, so a write-off outside the limit cannot be typed on
		///a claimproc the ERA posting creates (supplemental payment) or receives. Each claimproc is checked with IsInsWriteOffEditAuthorized and its
		///procedure from listProcedures (a total payment uses its SecDateEntry). A claimproc not saved yet (ClaimProcNum 0, such as the total payment
		///row an ERA adds) is being created now, so it is checked with today's date. Shows no message.</summary>
		public static bool IsInsWriteOffEditAuthorizedForAll(List<ClaimProc> listClaimProcs,List<Procedure> listProcedures) {
			Meth.NoCheckMiddleTierRole();
			for(int i=0;i<listClaimProcs.Count;i++) {
				ClaimProc claimProc=listClaimProcs[i];
				if(claimProc.ClaimProcNum==0 && claimProc.ProcNum==0) {
					if(!Security.IsAuthorized(EnumPermType.InsWriteOffEdit,DateTime.Today,suppressMessage:true)) {
						return false;
					}
					continue;
				}
				Procedure procedure=null;
				if(claimProc.ProcNum!=0 && listProcedures!=null) {
					procedure=listProcedures.Find(x => x.ProcNum==claimProc.ProcNum);
				}
				if(!IsInsWriteOffEditAuthorized(claimProc,procedure,suppressMessage:true)) {
					return false;
				}
			}
			return true;
		}
	}
}
