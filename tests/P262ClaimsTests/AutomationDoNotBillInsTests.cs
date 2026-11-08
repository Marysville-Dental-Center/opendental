// P262-CLAIMS item 22: the Do Not Bill to Ins automation trigger and its Carriers Match condition, rules only (no database).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace P262ClaimsTests {
	[TestClass]
	public class AutomationDoNotBillInsTests {
		[TestMethod]
		public void EnumValues_AreOpenDentals() {
			//OpenDentalDiffDocumentation26-2.xml: "DoNotBillInsuranceCheck 11-When clicking in the proc edit window", "CarriersMatch 15- ...".
			Assert.AreEqual(11,(int)EnumAutomationTrigger.DoNotBillInsuranceCheck);
			Assert.AreEqual(15,(int)AutoCondField.CarriersMatch);
			Assert.AreEqual(EnumAutomationTrigger.DoNotBillInsuranceCheck,AutomationDoNotBillIns.TRIGGER);
		}

		[TestMethod]
		public void DoesFire_OnlyWhenTheProcedureChangesToNotBillInsurance() {
			Assert.IsTrue(AutomationDoNotBillIns.DoesFire(false,true),"Unchecked to checked.");
			Assert.IsTrue(AutomationDoNotBillIns.DoesFire(null,true),"Mixed (some estimates not billed) to checked.");
			Assert.IsFalse(AutomationDoNotBillIns.DoesFire(true,true),"Already not billed: no change.");
			Assert.IsFalse(AutomationDoNotBillIns.DoesFire(true,false),"Checked to unchecked.");
			Assert.IsFalse(AutomationDoNotBillIns.DoesFire(null,false),"Mixed to unchecked.");
			Assert.IsFalse(AutomationDoNotBillIns.DoesFire(false,false));
		}

		[TestMethod]
		public void IsTriggerAllowed_OnlyDoNotBillInsuranceCheck() {
			foreach(EnumAutomationTrigger trigger in System.Enum.GetValues(typeof(EnumAutomationTrigger))) {
				Assert.AreEqual(trigger==EnumAutomationTrigger.DoNotBillInsuranceCheck,AutomationDoNotBillIns.IsTriggerAllowed(trigger),trigger.ToString());
			}
		}

		[TestMethod]
		public void GetSaveError_RefusesCarriersMatchWithAnotherTrigger() {
			List<AutomationCondition> listConditions=new List<AutomationCondition>() { Carrier(5) };
			Assert.AreEqual("",AutomationDoNotBillIns.GetSaveError(EnumAutomationTrigger.DoNotBillInsuranceCheck,listConditions));
			string error=AutomationDoNotBillIns.GetSaveError(EnumAutomationTrigger.ClaimCreate,listConditions);
			StringAssert.Contains(error,"Carriers Match");
			StringAssert.Contains(error,"DoNotBillInsuranceCheck");
			List<AutomationCondition> listOther=new List<AutomationCondition>() {
				new AutomationCondition() { CompareField=AutoCondField.Gender,Comparison=AutoCondComparison.Equals,CompareString="F" } };
			Assert.AreEqual("",AutomationDoNotBillIns.GetSaveError(EnumAutomationTrigger.ClaimCreate,listOther),"Other conditions are not this rule's.");
			Assert.AreEqual("",AutomationDoNotBillIns.GetSaveError(EnumAutomationTrigger.ClaimCreate,null));
			Assert.AreEqual("",AutomationDoNotBillIns.GetSaveError(EnumAutomationTrigger.ClaimCreate,new List<AutomationCondition>()));
		}

		[TestMethod]
		public void GetCarrierNum_ReadsTheCarrierNumFromCompareString() {
			Assert.AreEqual(42,AutomationDoNotBillIns.GetCarrierNum(Carrier(42)));
			Assert.AreEqual(42,AutomationDoNotBillIns.GetCarrierNum(new AutomationCondition() { CompareField=AutoCondField.CarriersMatch,CompareString=" 42 " }));
			Assert.AreEqual(0,AutomationDoNotBillIns.GetCarrierNum(new AutomationCondition() { CompareField=AutoCondField.CarriersMatch,CompareString="" }));
			Assert.AreEqual(0,AutomationDoNotBillIns.GetCarrierNum(new AutomationCondition() { CompareField=AutoCondField.CarriersMatch,CompareString=null }));
			Assert.AreEqual(0,AutomationDoNotBillIns.GetCarrierNum(new AutomationCondition() { CompareField=AutoCondField.CarriersMatch,CompareString="Acme" }));
			Assert.AreEqual(0,AutomationDoNotBillIns.GetCarrierNum(new AutomationCondition() { CompareField=AutoCondField.CarriersMatch,CompareString="-3" }));
			Assert.AreEqual(0,AutomationDoNotBillIns.GetCarrierNum(new AutomationCondition() { CompareField=AutoCondField.PlanNum,CompareString="42" }),
				"Only a Carriers Match condition holds a carrier.");
			Assert.AreEqual(0,AutomationDoNotBillIns.GetCarrierNum(null));
		}

		[TestMethod]
		public void IsAnyCarrierMatch_CarriersAreOred() {
			List<long> listCarrierNumsPatient=new List<long>() { 7,9 };
			Assert.IsTrue(AutomationDoNotBillIns.IsAnyCarrierMatch(listCarrierNumsPatient,new[] { Carrier(9) }));
			Assert.IsTrue(AutomationDoNotBillIns.IsAnyCarrierMatch(listCarrierNumsPatient,new[] { Carrier(3),Carrier(7) }),"Any one carrier is enough.");
			Assert.IsFalse(AutomationDoNotBillIns.IsAnyCarrierMatch(listCarrierNumsPatient,new[] { Carrier(3),Carrier(4) }));
			Assert.IsFalse(AutomationDoNotBillIns.IsAnyCarrierMatch(new List<long>(),new[] { Carrier(7) }),"A patient without insurance matches nothing.");
			Assert.IsFalse(AutomationDoNotBillIns.IsAnyCarrierMatch(listCarrierNumsPatient,new AutomationCondition[0]));
			Assert.IsFalse(AutomationDoNotBillIns.IsAnyCarrierMatch(new List<long>() { 0 },
				new[] { new AutomationCondition() { CompareField=AutoCondField.CarriersMatch,CompareString="" } }),"No carrier never matches no carrier.");
		}

		[TestMethod]
		public void IsConditionMet_FalseForEveryOtherTrigger() {
			//Returns before reading the database, so no database is needed.
			List<AutomationCondition> listConditions=new List<AutomationCondition>() { Carrier(1) };
			foreach(EnumAutomationTrigger trigger in System.Enum.GetValues(typeof(EnumAutomationTrigger))) {
				if(trigger==EnumAutomationTrigger.DoNotBillInsuranceCheck) {
					continue;
				}
				Assert.IsFalse(AutomationDoNotBillIns.IsConditionMet(trigger,listConditions,1),trigger.ToString());
			}
			Assert.IsFalse(AutomationDoNotBillIns.IsConditionMet(null,listConditions,1));
		}

		public static AutomationCondition Carrier(long carrierNum) {
			return new AutomationCondition() { CompareField=AutoCondField.CarriersMatch,Comparison=AutoCondComparison.None,CompareString=carrierNum.ToString() };
		}
	}
}
