// P262-CLAIMS (Open Dental 26.2, release note "Automation trigger added when changing a procedure to not bill to insurance"): the automation
// trigger DoNotBillInsuranceCheck (EnumAutomationTrigger 11) and its condition CarriersMatch (AutoCondField 15). Built from the 26.2 database
// documents only; see docs/port-notes/P262-CLAIMS.md for what is INFERRED.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>When the Do Not Bill to Ins trigger fires, and how its Carriers Match condition is evaluated and saved.</summary>
	public static class AutomationDoNotBillIns {
		///<summary>The trigger, and the only one the Carriers Match condition works with (automationcondition.CompareField 15: "Only used for the
		///DoNotBillInsuranceClick automation trigger").</summary>
		public const EnumAutomationTrigger TRIGGER=EnumAutomationTrigger.DoNotBillInsuranceCheck;

		///<summary>True if the trigger can carry the Carriers Match condition (DoNotBillInsuranceCheck only).</summary>
		public static bool IsTriggerAllowed(EnumAutomationTrigger automationTrigger) {
			return automationTrigger==TRIGGER;
		}

		///<summary>True when a click on Do Not Bill to Ins in the procedure edit window changes the procedure to not bill insurance: the box is now
		///checked and was not checked before (unchecked, or mixed when only some of the estimates were not billed). Unchecking never fires. INFERRED:
		///the documents say only "When clicking in the proc edit window".</summary>
		public static bool DoesFire(bool? isNoBillInsOld,bool isNoBillInsNew) {
			return isNoBillInsNew && isNoBillInsOld!=true;
		}

		///<summary>An error message for saving an automation with this trigger and these conditions, or "" when it can be saved. Saving is refused
		///while the automation has a Carriers Match condition and its trigger is not DoNotBillInsuranceCheck.</summary>
		public static string GetSaveError(EnumAutomationTrigger automationTrigger,List<AutomationCondition> listAutomationConditions) {
			if(IsTriggerAllowed(automationTrigger) || listAutomationConditions==null) {
				return "";
			}
			if(!listAutomationConditions.Any(x => x.CompareField==AutoCondField.CarriersMatch)) {
				return "";
			}
			return "The condition \""+AutoCondField.CarriersMatch.GetDescription()+"\" only works with the "+TRIGGER+" trigger. Change the trigger "
				+"or delete the condition.";
		}

		///<summary>The CarrierNum a Carriers Match condition holds in CompareString, or 0 when it holds none.</summary>
		public static long GetCarrierNum(AutomationCondition automationCondition) {
			if(automationCondition==null || automationCondition.CompareField!=AutoCondField.CarriersMatch) {
				return 0;
			}
			if(!long.TryParse((automationCondition.CompareString??"").Trim(),out long carrierNum) || carrierNum<=0) {
				return 0;
			}
			return carrierNum;
		}

		///<summary>True when one of the patient's carriers is one of the carriers of the conditions. Several Carriers Match conditions of one
		///automation are ORed ("for DoNotBillInsuranceCheck, the carriers are ORed"), so any one match is enough. False with no condition, no
		///carrier or no match.</summary>
		public static bool IsAnyCarrierMatch(IEnumerable<long> listCarrierNumsPatient,IEnumerable<AutomationCondition> listAutomationConditions) {
			if(listCarrierNumsPatient==null || listAutomationConditions==null) {
				return false;
			}
			HashSet<long> hashSetCarrierNums=new HashSet<long>(listCarrierNumsPatient.Where(x => x>0));
			return listAutomationConditions.Select(x => GetCarrierNum(x)).Any(x => x>0 && hashSetCarrierNums.Contains(x));
		}

		///<summary>The Carriers Match conditions of one automation that fired: always false for any trigger other than DoNotBillInsuranceCheck;
		///otherwise true when one of the patient's insurance carriers is one of theirs (ORed). The other conditions of the automation are still
		///ANDed with this result by the caller.</summary>
		public static bool IsConditionMet(EnumAutomationTrigger? automationTrigger,List<AutomationCondition> listAutomationConditionsCarriers,
			long patNum)
		{
			Meth.NoCheckMiddleTierRole();
			if(automationTrigger==null || !IsTriggerAllowed(automationTrigger.Value)) {
				return false;
			}
			return IsAnyCarrierMatch(GetCarrierNumsForPat(patNum),listAutomationConditionsCarriers);
		}

		///<summary>Carrier Combine (Carriers.Combine): every Carriers Match condition that holds one of the combined carriers is changed to hold the
		///carrier that is kept, so the automation still fires once the plans carry the kept carrier and the windows still show a carrier name. Two
		///conditions of one automation that now hold the same carrier are harmless: they are ORed. Called before the combined carriers are deleted.
		///Returns the number of conditions changed. INFERRED: the documents do not say what combining carriers does to these conditions.</summary>
		public static long CombineCarriers(long carrierNumKept,List<long> listCarrierNumsCombined) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetLong(MethodBase.GetCurrentMethod(),carrierNumKept,listCarrierNumsCombined);
			}
			if(carrierNumKept<=0 || listCarrierNumsCombined.IsNullOrEmpty()) {
				return 0;
			}
			List<long> listCarrierNums=listCarrierNumsCombined.FindAll(x => x>0 && x!=carrierNumKept).Distinct().ToList();
			if(listCarrierNums.Count==0) {
				return 0;
			}
			//CompareString holds the CarrierNum as text (FormAutomationConditionEdit writes POut.Long); TRIM matches what GetCarrierNum reads.
			string command="UPDATE automationcondition SET CompareString='"+POut.String(POut.Long(carrierNumKept))+"' "
				+"WHERE CompareField="+POut.Int((int)AutoCondField.CarriersMatch)+" "
				+"AND TRIM(CompareString) IN("+string.Join(",",listCarrierNums.Select(x => "'"+POut.String(POut.Long(x))+"'"))+")";
			return Db.NonQ(command);
		}

		///<summary>The carriers of the patient's insurance plans (every patplan, any ordinal), without duplicates. INFERRED: the documents say "a
		///patient's insurance carriers"; plans the patient is no longer on (no patplan) do not count.</summary>
		public static List<long> GetCarrierNumsForPat(long patNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<long>>(MethodBase.GetCurrentMethod(),patNum);
			}
			string command="SELECT DISTINCT insplan.CarrierNum FROM patplan "
				+"INNER JOIN inssub ON inssub.InsSubNum=patplan.InsSubNum "
				+"INNER JOIN insplan ON insplan.PlanNum=inssub.PlanNum "
				+"WHERE patplan.PatNum="+POut.Long(patNum);
			return Db.GetListLong(command);
		}

		///<summary>The text the automation and condition windows show for a Carriers Match condition: the carrier's name, or the stored text when it
		///is not a carrier that exists.</summary>
		public static string GetDisplayText(AutomationCondition automationCondition) {
			Meth.NoCheckMiddleTierRole();
			long carrierNum=GetCarrierNum(automationCondition);
			if(carrierNum==0) {
				return automationCondition?.CompareString??"";
			}
			string carrierName=Carriers.GetName(carrierNum);
			if(string.IsNullOrEmpty(carrierName)) {
				return automationCondition.CompareString;
			}
			return carrierName;
		}
	}
}
