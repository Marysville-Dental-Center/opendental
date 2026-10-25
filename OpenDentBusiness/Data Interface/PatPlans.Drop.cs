// Audit text for dropping a patient's insurance plan (backlog item v26_1-tools-audit-insplan-drop, package M2-INSPLAN): the entry names the
// plan's PlanNum, so two plans with the same carrier name can be told apart. The PlanNum was already the entry's foreign key; now it is in the
// text too.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
namespace OpenDentBusiness {
	public partial class PatPlans {
		///<summary>"(PlanNum 123)". Shared by every audit entry that records a dropped plan.</summary>
		public static string GetPlanNumLogText(long planNum) {
			Meth.NoCheckMiddleTierRole();
			return "(PlanNum "+POut.Long(planNum)+")";
		}

		///<summary>The InsPlanEdit audit text for dropping a plan from a patient in Edit Insurance Plan: "The insurance plan for the carrier
		///Example Dental (PlanNum 123) was dropped."</summary>
		public static string GetDropLogText(string carrierName,long planNum) {
			Meth.NoCheckMiddleTierRole();
			return Lans.g("FormInsPlan","The insurance plan for the carrier")+" "+carrierName+" "+GetPlanNumLogText(planNum)+" "
				+Lans.g("FormInsPlan","was dropped.");
		}
	}
}
