using System;
using CodeBase;

namespace OpenDentBusiness {
	///<summary>The Treatment Planned Procedure Edit Fee permission (EnumPermType.ProcTPEditFee, Open Dental 25.2): who may type a new fee on a treatment
	///planned procedure. Automatic fee updates (fee schedule changes, insurance changes, the fee update tools) are not covered.</summary>
	public static class ProcTPFeeEdit {
		///<summary>Treatment planned and treatment planned inactive.</summary>
		public static bool IsTreatPlanStatus(ProcStat procStat) {
			return procStat==ProcStat.TP || procStat==ProcStat.TPi;
		}

		///<summary>Whether the fee box is editable for this status. Only TP and TPi need the permission.</summary>
		public static bool CanEditFee(ProcStat procStat,bool hasProcTPEditFee) {
			return !IsTreatPlanStatus(procStat) || hasProcTPEditFee;
		}

		///<summary>True when a manual fee change on a treatment planned procedure must be logged under ProcTPEditFee.</summary>
		public static bool ShouldLog(ProcStat procStat,double feeOld,double feeNew) {
			return IsTreatPlanStatus(procStat) && !CompareDouble.IsEqual(feeOld,feeNew);
		}

		///<summary>Save-time check. The fee box is read-only only while the status is TP or TPi, so a user can type a fee under another status and then
		///switch back. What counts is the status being saved: block when it is TP or TPi, the user typed the fee (under any status), the fee differs from
		///the fee the window opened with, and the user lacks the permission. Automatic fee updates are not typed and never block.</summary>
		public static bool ShouldBlockOnSave(ProcStat procStatNew,bool isFeeTyped,double feeOld,double feeNew,bool hasProcTPEditFee) {
			return isFeeTyped && !hasProcTPEditFee && ShouldLog(procStatNew,feeOld,feeNew);
		}

		///<summary>Save-time log check: log under ProcTPEditFee when the status being saved is TP or TPi and a typed fee changed the fee, whatever the
		///status was when the fee was typed.</summary>
		public static bool ShouldLogOnSave(ProcStat procStatNew,bool isFeeTyped,double feeOld,double feeNew) {
			return isFeeTyped && ShouldLog(procStatNew,feeOld,feeNew);
		}

		///<summary>Message shown when a save is refused by ShouldBlockOnSave.</summary>
		public static string GetBlockedOnSaveText(double feeOld) {
			return Lans.g("ProcTPFeeEdit","Treatment planned fees cannot be changed without the permission")+" "+EnumPermType.ProcTPEditFee.GetDescription()+". "
				+Lans.g("ProcTPFeeEdit","The fee was restored to")+" "+feeOld.ToString("F")+".";
		}

		///<summary>"Fee for D0120 changed from 50.00 to 60.00".</summary>
		public static string GetLogText(string procCode,double feeOld,double feeNew) {
			return Lans.g("ProcTPFeeEdit","Fee for")+" "+procCode+" "+Lans.g("ProcTPFeeEdit","changed from")+" "+feeOld.ToString("F")
				+" "+Lans.g("ProcTPFeeEdit","to")+" "+feeNew.ToString("F");
		}

		///<summary>Tooltip for a fee box that is read-only because the user lacks the permission.</summary>
		public static string GetNoPermissionTip() {
			return Lans.g("ProcTPFeeEdit","Requires the permission")+" "+EnumPermType.ProcTPEditFee.GetDescription();
		}
	}
}
