// M2-EST: the per-procedure "Do Not Bill to Ins" decision (procedurelog.NoBillIns, Open Dental 25.4) and its audit trail.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CodeBase;

namespace OpenDentBusiness {
	public partial class Procedures {
		///<summary>Added to the audit entry when the procedure's flag is cleared because a claimproc of it was set to bill insurance.</summary>
		public const string NO_BILL_INS_CLEARED_SUFFIX="(cleared because a claimproc was set to bill insurance)";

		///<summary>The Do Not Bill Insurance value of a new estimate claimproc: the procedure's own decision (procedurelog.NoBillIns, which only the user
		///sets, in Procedure Info), or else the default of the procedure code and the plan (procedurecode.NoBillIns and its insurance plan override).
		///Used wherever estimates are created or rebuilt, so the user's decision survives a plan change (Open Dental 25.4).</summary>
		public static bool GetNoBillInsForNewEstimate(Procedure procedure,InsPlan insPlan) {
			Meth.NoCheckMiddleTierRole();
			if(procedure.NoBillIns) {
				return true;
			}
			return InsPlanPreferences.NoBillIns(ProcedureCodes.GetProcCode(procedure.CodeNum),insPlan);
		}

		///<summary>The state the Do Not Bill to Ins box in Procedure Info shows for these claimprocs (all of the procedure's): true when every one is
		///not billed to insurance, null (mixed) when some are, false when none are or there are none.</summary>
		public static bool? GetNoBillInsState(List<ClaimProc> listClaimProcs) {
			Meth.NoCheckMiddleTierRole();
			if(listClaimProcs==null || listClaimProcs.Count==0 || !listClaimProcs.Any(x => x.NoBillIns)) {
				return false;
			}
			if(listClaimProcs.All(x => x.NoBillIns)) {
				return true;
			}
			return null;
		}

		///<summary>The state of the Do Not Bill to Ins box in audit entries: "checked", "unchecked", or "mixed" (null: some claimprocs are billed to
		///insurance and some are not).</summary>
		public static string GetNoBillInsStateText(bool? isNoBillIns) {
			Meth.NoCheckMiddleTierRole();
			if(isNoBillIns==null) {
				return Lans.g("Procedures","mixed");
			}
			return isNoBillIns.Value ? Lans.g("Procedures","checked") : Lans.g("Procedures","unchecked");
		}

		///<summary>The audit text, for example "Z1234 03/15/2027: Do Not Bill to Ins changed from unchecked to checked".</summary>
		public static string GetNoBillInsLogText(Procedure procedure,string stateOld,string stateNew,bool isClearedByClaimProc) {
			Meth.NoCheckMiddleTierRole();
			string text=ProcedureCodes.GetStringProcCode(procedure.CodeNum)+" "+procedure.ProcDate.ToShortDateString()+": "
				+Lans.g("Procedures","Do Not Bill to Ins changed from")+" "+stateOld+" "+Lans.g("Procedures","to")+" "+stateNew;
			if(isClearedByClaimProc) {
				text+=" "+Lans.g("Procedures",NO_BILL_INS_CLEARED_SUFFIX);
			}
			return text;
		}

		///<summary>The permission the audit entry is written under: the one that guards the box in Procedure Info (ProcCompleteEditMisc for completed
		///procedures, ProcEdit otherwise).</summary>
		public static EnumPermType GetNoBillInsPermType(Procedure procedure) {
			Meth.NoCheckMiddleTierRole();
			if(procedure.ProcStatus.In(ProcStat.C,ProcStat.EC,ProcStat.EO)) {
				return EnumPermType.ProcCompleteEditMisc;
			}
			return EnumPermType.ProcEdit;
		}

		///<summary>Records the user's Do Not Bill to Ins decision from Procedure Info (Open Dental 25.4): saves procedurelog.NoBillIns at once, like the
		///claimprocs the box changes, and writes an audit entry when the box changed. isNoBillInsOld is the box before the click (null when it was
		///mixed). The flag becomes true only through this method, that is only when the user checks the box.</summary>
		public static void SetNoBillInsFromUser(Procedure procedure,bool isNoBillIns,bool? isNoBillInsOld) {
			Meth.NoCheckMiddleTierRole();
			if(procedure.NoBillIns!=isNoBillIns) {
				UpdateNoBillIns(procedure.ProcNum,isNoBillIns);
				procedure.NoBillIns=isNoBillIns;
			}
			string stateOld=GetNoBillInsStateText(isNoBillInsOld);
			string stateNew=GetNoBillInsStateText(isNoBillIns);
			if(stateOld==stateNew) {
				return;
			}
			SecurityLogs.MakeLogEntry(GetNoBillInsPermType(procedure),procedure.PatNum,GetNoBillInsLogText(procedure,stateOld,stateNew,false),
				procedure.ProcNum,DateTime.MinValue);
		}

		///<summary>When a claimproc that was not billed to insurance is set to bill insurance (Edit Claim Procedure), the procedure's own Do Not Bill to
		///Ins flag no longer holds, so it is cleared and the clear is audited (Open Dental 25.4, rule 3). Returns true when the flag was cleared.</summary>
		public static bool ClearNoBillInsForBilledClaimProc(Procedure procedure,ClaimProc claimProcOld,ClaimProc claimProcNew) {
			Meth.NoCheckMiddleTierRole();
			if(procedure==null || claimProcOld==null || claimProcNew==null || claimProcNew.ProcNum!=procedure.ProcNum) {
				return false;
			}
			if(!claimProcOld.NoBillIns || claimProcNew.NoBillIns || !procedure.NoBillIns) {
				return false;
			}
			UpdateNoBillIns(procedure.ProcNum,false);
			procedure.NoBillIns=false;
			SecurityLogs.MakeLogEntry(GetNoBillInsPermType(procedure),procedure.PatNum,
				GetNoBillInsLogText(procedure,GetNoBillInsStateText(true),GetNoBillInsStateText(false),true),procedure.ProcNum,DateTime.MinValue);
			return true;
		}

		///<summary>Saves procedurelog.NoBillIns of one procedure and nothing else. Does nothing for a procedure that is not in the database yet.</summary>
		public static void UpdateNoBillIns(long procNum,bool isNoBillIns) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),procNum,isNoBillIns);
				return;
			}
			if(procNum==0) {
				return;
			}
			Db.NonQ("UPDATE procedurelog SET NoBillIns="+POut.Bool(isNoBillIns)+" WHERE ProcNum="+POut.Long(procNum));
		}
	}
}
