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
