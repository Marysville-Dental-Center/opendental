// ODOPTS: Open Dental 25.3.48's ClaimUpdateFeeForSecondary, built from its name (its meaning is not public; INFERRED, docs/port-notes/ODOPTS.md).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>When the fee of a procedure changes after its primary claim exists, its secondary claims that are not sent yet take the new fee.
	///The primary claim is not touched: once sent its fee is what the carrier was billed, and an unsent primary claim takes the new fee when it is
	///recalculated, as before. A secondary claim that is sent or received keeps its fee. INFERRED from the name of Open Dental's setting; the
	///owner confirms it by testing (docs/port-notes/ODOPTS.md).</summary>
	public static class ClaimFeeForSecondary {
		///<summary>Open Dental's preference row, read and written by name outside PrefName so the office's value carries over. 1 = on. A missing row
		///reads as off, the behaviour of 24.3 (INFERRED; Open Dental's default is not public).</summary>
		public const string PREF_NAME="ClaimUpdateFeeForSecondary";
		///<summary>The label of the setting in Preferences, Account, Claim Receive.</summary>
		public const string LABEL="Update the fee on unsent secondary claims when a procedure fee changes";

		public static bool IsEnabled() {
			Meth.NoCheckMiddleTierRole();
			return OdOptions.GetBool(PREF_NAME,defaultValue:false);
		}

		///<summary>Saves the setting and updates this workstation's cache. Returns true if the stored value changed; the caller signals
		///InvalidType.Prefs and makes the security log entry.</summary>
		public static bool SetEnabled(bool isEnabled) {
			Meth.NoCheckMiddleTierRole();
			return OdOptions.SetBool(PREF_NAME,isEnabled,defaultValue:false);
		}

		///<summary>True for a claim whose fee may still change: not sent ("S") and not received ("R"). Claims.CalculateAndUpdate updates FeeBilled and
		///ClaimFee for exactly these.</summary>
		public static bool IsFeeOpen(Claim claim) {
			Meth.NoCheckMiddleTierRole();
			return claim!=null && !claim.ClaimStatus.In("S","R");
		}

		///<summary>The secondary claims whose fee follows the procedure's new fee: the procedure has a claimproc on a primary claim (ClaimType "P"),
		///and these claims are its secondary claims (ClaimType "S") with a NotReceived claimproc of the procedure that is not an overpayment, and are
		///not sent or received. No database access: listClaimProcs are the procedure's claimprocs and listClaims their claims. Each claim once.</summary>
		public static List<Claim> GetSecondaryClaimsToUpdate(long procNum,List<ClaimProc> listClaimProcs,List<Claim> listClaims) {
			Meth.NoCheckMiddleTierRole();
			List<Claim> listClaimsToUpdate=new List<Claim>();
			if(listClaimProcs==null || listClaims==null) {
				return listClaimsToUpdate;
			}
			List<ClaimProc> listClaimProcsProc=listClaimProcs.FindAll(x => x.ProcNum==procNum && x.ClaimNum!=0);
			bool hasPrimaryClaim=listClaimProcsProc.Any(x => listClaims.Any(y => y.ClaimNum==x.ClaimNum && y.ClaimType=="P"));
			if(!hasPrimaryClaim) {
				return listClaimsToUpdate;
			}
			for(int i=0;i<listClaimProcsProc.Count;i++) {
				ClaimProc claimProc=listClaimProcsProc[i];
				if(claimProc.Status!=ClaimProcStatus.NotReceived || claimProc.IsOverpay) {
					continue;
				}
				Claim claim=listClaims.Find(x => x.ClaimNum==claimProc.ClaimNum);
				if(claim==null || claim.ClaimType!="S" || !IsFeeOpen(claim)) {
					continue;
				}
				if(listClaimsToUpdate.Any(x => x.ClaimNum==claim.ClaimNum)) {
					continue;
				}
				listClaimsToUpdate.Add(claim);
			}
			return listClaimsToUpdate;
		}

		///<summary>The Audit Trail text of one secondary claim updated for a new procedure fee.</summary>
		public static string GetLogText(string procCode,double procFeeOld,double procFeeNew,Claim claim) {
			Meth.NoCheckMiddleTierRole();
			return Lans.g("ClaimFeeForSecondary","Secondary claim fee updated for")+" "+procCode+" "+Lans.g("ClaimFeeForSecondary","fee changed from")+" "
				+procFeeOld.ToString("c")+" "+Lans.g("ClaimFeeForSecondary","to")+" "+procFeeNew.ToString("c")+" ("+PREF_NAME+"), "
				+Lans.g("ClaimFeeForSecondary","claim dated")+" "+claim.DateService.ToShortDateString()+".";
		}

		///<summary>Call after a procedure's new fee is saved. When the setting is on and the fee changed, recalculates each secondary claim of
		///GetSecondaryClaimsToUpdate (Claims.CalculateAndUpdateSecondaries: FeeBilled from the new fee, or the UCR fee when the plan bills UCR, the
		///claim total and the estimates that follow) and makes one ClaimEdit log entry each. Returns the claims updated; empty when nothing was done.
		///</summary>
		public static List<Claim> UpdateForProcFeeChange(Procedure procedure,double procFeeOld) {
			Meth.NoCheckMiddleTierRole();
			List<Claim> listClaimsUpdated=new List<Claim>();
			if(procedure==null || procedure.ProcNum==0 || !IsEnabled() || CompareDouble.IsEqual(procFeeOld,procedure.ProcFee)) {
				return listClaimsUpdated;
			}
			List<ClaimProc> listClaimProcs=ClaimProcs.RefreshForProc(procedure.ProcNum);
			List<long> listClaimNums=listClaimProcs.Select(x => x.ClaimNum).Where(x => x!=0).Distinct().ToList();
			if(listClaimNums.Count==0) {
				return listClaimsUpdated;
			}
			List<Claim> listClaims=Claims.GetClaimsFromClaimNums(listClaimNums);
			listClaimsUpdated=GetSecondaryClaimsToUpdate(procedure.ProcNum,listClaimProcs,listClaims);
			if(listClaimsUpdated.Count==0) {
				return listClaimsUpdated;
			}
			Claims.CalculateAndUpdateSecondaries(listClaimsUpdated);
			string procCode=ProcedureCodes.GetStringProcCode(procedure.CodeNum,doThrowIfMissing:false);
			for(int i=0;i<listClaimsUpdated.Count;i++) {
				SecurityLogs.MakeLogEntry(EnumPermType.ClaimEdit,procedure.PatNum,GetLogText(procCode,procFeeOld,procedure.ProcFee,listClaimsUpdated[i]),
					listClaimsUpdated[i].ClaimNum,listClaimsUpdated[i].SecDateTEdit);
			}
			return listClaimsUpdated;
		}
	}
}
