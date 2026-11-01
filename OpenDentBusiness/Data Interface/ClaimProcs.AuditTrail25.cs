// B-CLAIM: audit entries for claim procedure fields that Open Dental 25.1 and 25.3 started to log.
// - v25_1-misc-claimproc-override-audit: changes to the Ins Est Total override and the Allowed override.
// - v25_3-security-claimproc-billedtoins: changes to Fee Billed and Code Sent, under ClaimProcFieldsBilledToInsEdit (252).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;

namespace OpenDentBusiness {
	public partial class ClaimProcs {
		///<summary>How an override with no value (-1) shows in the audit trail.</summary>
		public const string OVERRIDE_BLANK_TEXT="blank";

		#region Ins Est and Allowed overrides (Open Dental 25.1)
		///<summary>The permission the override audit entry is filed under. Open Dental's choice is not public, so this follows the fork's spec
		///(INFERRED): Claim Procedure Received Edit for received and supplemental claimprocs, Proc Edit (a logging-only permission) for every other
		///status (estimates, treatment plan, preauthorizations, claims not received yet).</summary>
		public static EnumPermType GetOverrideAuditPermType(ClaimProc claimProc) {
			Meth.NoCheckMiddleTierRole();
			if(claimProc!=null && claimProc.Status.In(ClaimProcStatus.Received,ClaimProcStatus.Supplemental)) {
				return EnumPermType.ClaimProcReceivedEdit;
			}
			return EnumPermType.ProcEdit;
		}

		///<summary>An override value as the audit trail shows it: "blank" for -1 (no override), otherwise the amount as currency.</summary>
		public static string FormatOverrideForAudit(double overrideValue) {
			Meth.NoCheckMiddleTierRole();
			if(overrideValue==-1) {
				return OVERRIDE_BLANK_TEXT;
			}
			return overrideValue.ToString("C");
		}

		///<summary>What changed in the Ins Est Total override and the Allowed override between the two copies of one claimproc, or "" when neither
		///changed. "blank to $50.00" and "$50.00 to blank" both count as changes.</summary>
		public static string GetOverrideChangeText(ClaimProc claimProcNew,ClaimProc claimProcOld) {
			Meth.NoCheckMiddleTierRole();
			if(claimProcNew==null || claimProcOld==null) {
				return "";
			}
			List<string> listChanges=new List<string>();
			if(!CompareDouble.IsEqual(claimProcOld.InsEstTotalOverride,claimProcNew.InsEstTotalOverride)) {
				listChanges.Add("Ins Est Total override changed from "+FormatOverrideForAudit(claimProcOld.InsEstTotalOverride)
					+" to "+FormatOverrideForAudit(claimProcNew.InsEstTotalOverride)+".");
			}
			if(!CompareDouble.IsEqual(claimProcOld.AllowedOverride,claimProcNew.AllowedOverride)) {
				listChanges.Add("Allowed override changed from "+FormatOverrideForAudit(claimProcOld.AllowedOverride)
					+" to "+FormatOverrideForAudit(claimProcNew.AllowedOverride)+".");
			}
			return string.Join(" ",listChanges);
		}

		///<summary>Writes one audit entry when the Ins Est Total override or the Allowed override of this claimproc changed. FKey is the
		///ClaimProcNum, and the text names the procedure code, the tooth and the carrier. Nothing for a claimproc that is not saved yet, or when the
		///two copies are not the same claimproc. Call it where the claimproc is saved, once per save.</summary>
		public static void CreateAuditTrailEntryForOverrides(ClaimProc claimProcNew,ClaimProc claimProcOld) {
			Meth.NoCheckMiddleTierRole();
			if(claimProcNew==null || claimProcOld==null || claimProcNew.ClaimProcNum==0 || claimProcNew.ClaimProcNum!=claimProcOld.ClaimProcNum) {
				return;
			}
			string changeText=GetOverrideChangeText(claimProcNew,claimProcOld);
			if(changeText=="") {
				return;
			}
			SecurityLogs.MakeLogEntry(GetOverrideAuditPermType(claimProcNew),claimProcNew.PatNum,GetAuditTextPrefix(claimProcNew)+changeText,
				claimProcNew.ClaimProcNum,DateTime.MinValue);
		}

		///<summary>CreateAuditTrailEntryForOverrides for every claimproc in listClaimProcsNew that has a copy, by ClaimProcNum, in
		///listClaimProcsOld. Claimprocs with no old copy (just created) are skipped.</summary>
		public static void CreateAuditTrailEntriesForOverrides(List<ClaimProc> listClaimProcsNew,List<ClaimProc> listClaimProcsOld) {
			Meth.NoCheckMiddleTierRole();
			if(listClaimProcsNew==null || listClaimProcsOld==null) {
				return;
			}
			for(int i=0;i<listClaimProcsNew.Count;i++) {
				if(listClaimProcsNew[i]==null || listClaimProcsNew[i].ClaimProcNum==0) {
					continue;
				}
				ClaimProc claimProcOld=listClaimProcsOld.FirstOrDefault(x => x!=null && x.ClaimProcNum==listClaimProcsNew[i].ClaimProcNum);
				CreateAuditTrailEntryForOverrides(listClaimProcsNew[i],claimProcOld);
			}
		}
		#endregion Ins Est and Allowed overrides (Open Dental 25.1)

		#region Fee Billed and Code Sent (Open Dental 25.3)
		///<summary>Applies the Fee Billed and Code Sent typed in Edit Claim Procedure. Since Open Dental 25.3 both fields are guarded by
		///ClaimProcFieldsBilledToInsEdit (252), for new and existing claimprocs: when isAuthorized is false neither field changes.</summary>
		public static void SetBilledToInsFields(ClaimProc claimProc,string codeSent,double feeBilled,bool isAuthorized) {
			Meth.NoCheckMiddleTierRole();
			if(claimProc==null || !isAuthorized) {
				return;
			}
			claimProc.CodeSent=codeSent;
			claimProc.FeeBilled=feeBilled;
		}

		///<summary>What changed in Fee Billed and Code Sent between the two copies of one claimproc, or "" when neither changed.</summary>
		public static string GetBilledToInsChangeText(ClaimProc claimProcNew,ClaimProc claimProcOld) {
			Meth.NoCheckMiddleTierRole();
			if(claimProcNew==null || claimProcOld==null) {
				return "";
			}
			List<string> listChanges=new List<string>();
			if(!CompareDouble.IsEqual(claimProcOld.FeeBilled,claimProcNew.FeeBilled)) {
				listChanges.Add("Fee billed changed from "+claimProcOld.FeeBilled.ToString("C")+" to "+claimProcNew.FeeBilled.ToString("C")+".");
			}
			string codeSentOld=claimProcOld.CodeSent??"";
			string codeSentNew=claimProcNew.CodeSent??"";
			if(codeSentOld!=codeSentNew) {
				listChanges.Add("Code sent changed from "+(codeSentOld=="" ? OVERRIDE_BLANK_TEXT : codeSentOld)
					+" to "+(codeSentNew=="" ? OVERRIDE_BLANK_TEXT : codeSentNew)+".");
			}
			return string.Join(" ",listChanges);
		}

		///<summary>Writes one audit entry under ClaimProcFieldsBilledToInsEdit (252) when Fee Billed or Code Sent of this claimproc changed. FKey is
		///the ClaimProcNum. Nothing when the two copies are not the same claimproc.</summary>
		public static void CreateAuditTrailEntryForBilledToIns(ClaimProc claimProcNew,ClaimProc claimProcOld) {
			Meth.NoCheckMiddleTierRole();
			if(claimProcNew==null || claimProcOld==null || claimProcNew.ClaimProcNum!=claimProcOld.ClaimProcNum) {
				return;
			}
			string changeText=GetBilledToInsChangeText(claimProcNew,claimProcOld);
			if(changeText=="") {
				return;
			}
			SecurityLogs.MakeLogEntry(EnumPermType.ClaimProcFieldsBilledToInsEdit,claimProcNew.PatNum,GetAuditTextPrefix(claimProcNew)+changeText,
				claimProcNew.ClaimProcNum,DateTime.MinValue);
		}
		#endregion Fee Billed and Code Sent (Open Dental 25.3)

		///<summary>"Procedure D1234, tooth 3, Carrier Name. " for a claimproc on a procedure, "Total payment, Carrier Name. " for a total payment.
		///Parts that cannot be found are left out.</summary>
		public static string GetAuditTextPrefix(ClaimProc claimProc) {
			Meth.NoCheckMiddleTierRole();
			List<string> listParts=new List<string>();
			if(claimProc.ProcNum==0) {
				listParts.Add("Total payment");
			}
			else {
				Procedure procedure=Procedures.GetOneProc(claimProc.ProcNum,false);
				string procCode=(procedure==null || procedure.CodeNum==0) ? "" : ProcedureCodes.GetStringProcCode(procedure.CodeNum);
				if(procCode!="") {
					listParts.Add("Procedure "+procCode);
				}
				if(procedure!=null && !string.IsNullOrWhiteSpace(procedure.ToothNum)) {
					listParts.Add("tooth "+Tooth.Display(procedure.ToothNum));
				}
			}
			InsPlan insPlan=InsPlans.RefreshOne(claimProc.PlanNum);
			if(insPlan!=null) {
				string carrierName=Carriers.GetName(insPlan.CarrierNum);
				if(!string.IsNullOrWhiteSpace(carrierName)) {
					listParts.Add(carrierName);
				}
			}
			if(listParts.Count==0) {
				return "";
			}
			return string.Join(", ",listParts)+". ";
		}
	}
}
