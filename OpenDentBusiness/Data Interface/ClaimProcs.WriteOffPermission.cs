// B-CLAIM, v25_3-security-inswriteoffedit-clarify: one place for the date that Insurance Write-off Edit (119) is checked against.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

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
	}
}
