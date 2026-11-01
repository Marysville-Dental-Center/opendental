// Audit text for dropping a patient's insurance plan (backlog item v26_1-tools-audit-insplan-drop, package M2-INSPLAN): the entry names the
// plan's PlanNum, so two plans with the same carrier name can be told apart. The PlanNum was already the entry's foreign key; now it is in the
// text too. The same goes for the other audit entries of paths that drop a patient from a plan through PatPlans.Delete: the X12 834 import
// (InsPlanDropPat) and Edit Insurance Plan's Delete (subscriber or whole plan).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
namespace OpenPmsDentalBusiness {
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

		///<summary>The InsPlanEdit audit text for deleting a plan in Edit Insurance Plan, which also drops the patient from it (InsPlans.Delete ends in
		///PatPlans.Delete): "The insurance plan for the carrier Example Dental (PlanNum 123) was deleted."</summary>
		public static string GetDeletePlanLogText(string carrierName,long planNum) {
			Meth.NoCheckMiddleTierRole();
			return Lans.g("FormInsPlan","The insurance plan for the carrier")+" "+carrierName+" "+GetPlanNumLogText(planNum)+" "
				+Lans.g("FormInsPlan","was deleted.");
		}

		///<summary>The InsPlanEdit audit text for deleting a subscriber in Edit Insurance Plan, which drops every patient on that subscription
		///(InsSubs.Delete calls PatPlans.Delete): "The subscriber Jane Doe with the Subscriber ID 123 (PlanNum 45) was deleted."</summary>
		public static string GetDeleteSubscriberLogText(string subscriberName,string subscriberID,long planNum) {
			Meth.NoCheckMiddleTierRole();
			return Lans.g("FormInsPlan","The subscriber")+" "+subscriberName+" "+Lans.g("FormInsPlan","with the Subscriber ID")+" "+subscriberID+" "
				+GetPlanNumLogText(planNum)+" "+Lans.g("FormInsPlan","was deleted.");
		}
	}
}
