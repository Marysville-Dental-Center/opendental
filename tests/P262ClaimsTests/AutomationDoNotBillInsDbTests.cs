// P262-CLAIMS item 22 on the scratch database: Automations.Trigger with the Do Not Bill to Ins trigger, its Carriers Match condition (ORed) and
// the other conditions (ANDed). The action is a pop-up, caught by the test instead of shown.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using UnitTestsCore;

namespace P262ClaimsTests {
	[TestClass]
	public class AutomationDoNotBillInsDbTests {
		[TestInitialize]
		public void Init() {
			ClaimsDb.Begin();
			//Each test starts with no automation, so only its own can fire.
			DataCore.NonQ("DELETE FROM automationcondition");
			DataCore.NonQ("DELETE FROM automation");
			Automations.RefreshCache();
			AutomationConditions.RefreshCache();
		}

		[TestMethod]
		public void GetCarrierNumsForPat_EveryPatPlansCarrierOnce() {
			string suffix=ClaimsDb.NewSuffix();
			Patient patient=PatientT.CreatePatient(suffix,lName:"Synthetic");
			Carrier carrierA=CarrierT.CreateCarrier("Carrier A "+suffix);
			Carrier carrierB=CarrierT.CreateCarrier("Carrier B "+suffix);
			Carrier carrierDropped=CarrierT.CreateCarrier("Carrier Dropped "+suffix);
			AddPlan(patient,carrierA,1);
			AddPlan(patient,carrierB,2);
			AddPlan(patient,carrierA,3);//a second plan with the same carrier
			InsPlan insPlanDropped=InsPlanT.CreateInsPlan(carrierDropped.CarrierNum);
			InsSubT.CreateInsSub(patient.PatNum,insPlanDropped.PlanNum);//subscriber, but no patplan: not the patient's insurance
			List<long> listCarrierNums=AutomationDoNotBillIns.GetCarrierNumsForPat(patient.PatNum);
			CollectionAssert.AreEquivalent(new List<long>() { carrierA.CarrierNum,carrierB.CarrierNum },listCarrierNums);
			Assert.AreEqual(0,AutomationDoNotBillIns.GetCarrierNumsForPat(PatientT.CreatePatient(suffix+"x").PatNum).Count);
		}

		[TestMethod]
		public void Trigger_FiresWhenThePatientHasTheCarrier() {
			Setup(out Patient patient,out Carrier carrierA,out Carrier carrierB);
			AddAutomation("Carrier A popup",EnumAutomationTrigger.DoNotBillInsuranceCheck,Carriers(carrierA));
			List<string> listShown=Fire(EnumAutomationTrigger.DoNotBillInsuranceCheck,patient);
			CollectionAssert.AreEqual(new List<string>() { "Carrier A popup" },listShown);
		}

		[TestMethod]
		public void Trigger_DoesNotFireForAnotherCarrier() {
			Setup(out Patient patient,out Carrier carrierA,out Carrier carrierB);
			Carrier carrierOther=CarrierT.CreateCarrier("Carrier Other "+ClaimsDb.NewSuffix());
			AddAutomation("Other carrier popup",EnumAutomationTrigger.DoNotBillInsuranceCheck,Carriers(carrierOther));
			Assert.AreEqual(0,Fire(EnumAutomationTrigger.DoNotBillInsuranceCheck,patient).Count);
		}

		[TestMethod]
		public void Trigger_CarriersAreOred() {
			Setup(out Patient patient,out Carrier carrierA,out Carrier carrierB);
			Carrier carrierOther=CarrierT.CreateCarrier("Carrier Other "+ClaimsDb.NewSuffix());
			AddAutomation("Any of two carriers",EnumAutomationTrigger.DoNotBillInsuranceCheck,Carriers(carrierOther,carrierB));
			CollectionAssert.AreEqual(new List<string>() { "Any of two carriers" },Fire(EnumAutomationTrigger.DoNotBillInsuranceCheck,patient));
		}

		[TestMethod]
		public void Trigger_OtherConditionsAreStillAnded() {
			Setup(out Patient patient,out Carrier carrierA,out Carrier carrierB);//the patient's gender is the default (Male)
			List<AutomationCondition> listConditions=Carriers(carrierA);
			listConditions.Add(new AutomationCondition() { CompareField=AutoCondField.Gender,Comparison=AutoCondComparison.Equals,CompareString="F" });
			AddAutomation("Carrier A and female",EnumAutomationTrigger.DoNotBillInsuranceCheck,listConditions);
			Assert.AreEqual(0,Fire(EnumAutomationTrigger.DoNotBillInsuranceCheck,patient).Count,"The carrier matches but the gender does not.");
			List<AutomationCondition> listConditionsMale=Carriers(carrierA);
			listConditionsMale.Add(new AutomationCondition() { CompareField=AutoCondField.Gender,Comparison=AutoCondComparison.Equals,CompareString="M" });
			AddAutomation("Carrier A and male",EnumAutomationTrigger.DoNotBillInsuranceCheck,listConditionsMale);
			CollectionAssert.AreEqual(new List<string>() { "Carrier A and male" },Fire(EnumAutomationTrigger.DoNotBillInsuranceCheck,patient));
		}

		[TestMethod]
		public void Trigger_WithoutConditionsFiresForEveryPatient() {
			Patient patientUninsured=PatientT.CreatePatient(ClaimsDb.NewSuffix(),lName:"Synthetic");
			AddAutomation("Always",EnumAutomationTrigger.DoNotBillInsuranceCheck,new List<AutomationCondition>());
			CollectionAssert.AreEqual(new List<string>() { "Always" },Fire(EnumAutomationTrigger.DoNotBillInsuranceCheck,patientUninsured));
		}

		[TestMethod]
		public void Trigger_OnlyItsOwnTriggerFires() {
			Setup(out Patient patient,out Carrier carrierA,out Carrier carrierB);
			AddAutomation("Do not bill",EnumAutomationTrigger.DoNotBillInsuranceCheck,new List<AutomationCondition>());
			AddAutomation("Claim create",EnumAutomationTrigger.ClaimCreate,new List<AutomationCondition>());
			CollectionAssert.AreEqual(new List<string>() { "Do not bill" },Fire(EnumAutomationTrigger.DoNotBillInsuranceCheck,patient));
			CollectionAssert.AreEqual(new List<string>() { "Claim create" },Fire(EnumAutomationTrigger.ClaimCreate,patient));
		}

		[TestMethod]
		public void Trigger_CarriersMatchNeverHoldsForAnotherTrigger() {
			//An automation saved before its trigger was changed (the window refuses that now) or written by another program.
			Setup(out Patient patient,out Carrier carrierA,out Carrier carrierB);
			AddAutomation("Claim create with carrier",EnumAutomationTrigger.ClaimCreate,Carriers(carrierA));
			Assert.AreEqual(0,Fire(EnumAutomationTrigger.ClaimCreate,patient).Count);
		}

		[TestMethod]
		public void GetDisplayText_ShowsTheCarrierName() {
			Carrier carrier=CarrierT.CreateCarrier("Carrier Named "+ClaimsDb.NewSuffix());
			Assert.AreEqual(carrier.CarrierName,AutomationDoNotBillIns.GetDisplayText(AutomationDoNotBillInsTests.Carrier(carrier.CarrierNum)));
			AutomationCondition automationConditionText=new AutomationCondition() { CompareField=AutoCondField.CarriersMatch,CompareString="abc" };
			Assert.AreEqual("abc",AutomationDoNotBillIns.GetDisplayText(automationConditionText),"Not a CarrierNum: shown as stored.");
		}

		[TestMethod]
		public void CarrierCombine_ConditionFollowsTheKeptCarrierAndStillFires() {
			string suffix=ClaimsDb.NewSuffix();
			Carrier carrierA=CarrierT.CreateCarrier("Carrier Dup A "+suffix);
			Carrier carrierB=CarrierT.CreateCarrier("Carrier Dup B "+suffix);
			Carrier carrierOther=CarrierT.CreateCarrier("Carrier Other "+suffix);
			Patient patientOnA=PatientT.CreatePatient(suffix+"a",lName:"Synthetic");
			AddPlan(patientOnA,carrierA,1);//moved to carrier B by the combine
			Automation automationA=AddAutomation("Carrier A popup",EnumAutomationTrigger.DoNotBillInsuranceCheck,Carriers(carrierA));
			Automation automationAOrB=AddAutomation("Carrier A or B popup",EnumAutomationTrigger.DoNotBillInsuranceCheck,Carriers(carrierA,carrierB));
			Automation automationOther=AddAutomation("Other carrier popup",EnumAutomationTrigger.DoNotBillInsuranceCheck,Carriers(carrierOther));
			//Another field whose text happens to be carrier A's number: not a carrier reference, left alone.
			Automation automationAge=AddAutomation("Age popup",EnumAutomationTrigger.ClaimCreate,new List<AutomationCondition>() {
				new AutomationCondition() { CompareField=AutoCondField.Age,Comparison=AutoCondComparison.GreaterThan,CompareString=POut.Long(carrierA.CarrierNum) }
			});
			Carriers.Combine(new List<long>() { carrierA.CarrierNum,carrierB.CarrierNum },carrierB.CarrierNum);
			Carriers.RefreshCache();
			Assert.IsNull(Carriers.GetCarrierDB(carrierA.CarrierNum),"Carrier A was combined into B and deleted.");
			List<AutomationCondition> listConditionsA=AutomationConditions.GetListByAutomationNum(automationA.AutomationNum);
			Assert.AreEqual(1,listConditionsA.Count);
			Assert.AreEqual(POut.Long(carrierB.CarrierNum),listConditionsA[0].CompareString,"The condition now holds the kept carrier.");
			Assert.AreEqual(carrierB.CarrierName,AutomationDoNotBillIns.GetDisplayText(listConditionsA[0]),"The windows show a carrier name.");
			CollectionAssert.AreEqual(new List<string>() { POut.Long(carrierB.CarrierNum),POut.Long(carrierB.CarrierNum) },
				AutomationConditions.GetListByAutomationNum(automationAOrB.AutomationNum).Select(x => x.CompareString).ToList(),
				"Both conditions hold B now; ORed, so the duplicate is harmless.");
			Assert.AreEqual(POut.Long(carrierOther.CarrierNum),
				AutomationConditions.GetListByAutomationNum(automationOther.AutomationNum)[0].CompareString,"Another carrier's condition is unchanged.");
			Assert.AreEqual(POut.Long(carrierA.CarrierNum),
				AutomationConditions.GetListByAutomationNum(automationAge.AutomationNum)[0].CompareString,"Only Carriers Match conditions change.");
			Patient patientOnB=PatientT.CreatePatient(suffix+"b",lName:"Synthetic");
			AddPlan(patientOnB,carrierB,1);
			List<string> listExpected=new List<string>() { "Carrier A popup","Carrier A or B popup" };
			CollectionAssert.AreEquivalent(listExpected,Fire(EnumAutomationTrigger.DoNotBillInsuranceCheck,patientOnB),"Each fires once (cache order).");
			CollectionAssert.AreEquivalent(listExpected,Fire(EnumAutomationTrigger.DoNotBillInsuranceCheck,patientOnA),
				"The plan that was on carrier A is on B now, and the automation still fires for it.");
		}

		[TestMethod]
		public void CombineCarriers_NothingToChange() {
			Assert.AreEqual(0,AutomationDoNotBillIns.CombineCarriers(0,new List<long>() { 5 }));
			Assert.AreEqual(0,AutomationDoNotBillIns.CombineCarriers(5,new List<long>()));
			Assert.AreEqual(0,AutomationDoNotBillIns.CombineCarriers(5,new List<long>() { 5,0 }));
		}

		#region Helpers
		private static void Setup(out Patient patient,out Carrier carrierA,out Carrier carrierB) {
			string suffix=ClaimsDb.NewSuffix();
			patient=PatientT.CreatePatient(suffix,lName:"Synthetic");
			carrierA=CarrierT.CreateCarrier("Carrier A "+suffix);
			carrierB=CarrierT.CreateCarrier("Carrier B "+suffix);
			AddPlan(patient,carrierA,1);
			AddPlan(patient,carrierB,2);
		}

		private static void AddPlan(Patient patient,Carrier carrier,byte ordinal) {
			InsPlan insPlan=InsPlanT.CreateInsPlan(carrier.CarrierNum);
			InsSub insSub=InsSubT.CreateInsSub(patient.PatNum,insPlan.PlanNum);
			PatPlanT.CreatePatPlan(ordinal,patient.PatNum,insSub.InsSubNum);
		}

		private static List<AutomationCondition> Carriers(params Carrier[] arrayCarriers) {
			return arrayCarriers.Select(x => AutomationDoNotBillInsTests.Carrier(x.CarrierNum)).ToList();
		}

		private static Automation AddAutomation(string message,EnumAutomationTrigger trigger,List<AutomationCondition> listConditions) {
			Automation automation=new Automation() {
				Description=message,
				Autotrigger=trigger,
				AutoAction=AutomationAction.PopUp,
				MessageContent=message,
				ProcCodes="",
			};
			Automations.Insert(automation);
			foreach(AutomationCondition automationCondition in listConditions) {
				automationCondition.AutomationNum=automation.AutomationNum;
				AutomationConditions.Insert(automationCondition);
			}
			Automations.RefreshCache();
			AutomationConditions.RefreshCache();
			return automation;
		}

		///<summary>Fires the trigger for the patient and returns the pop-up messages it would show, in order.</summary>
		private static List<string> Fire(EnumAutomationTrigger trigger,Patient patient) {
			List<string> listShown=new List<string>();
			Automations.Trigger<object>(trigger,null,patient.PatNum,new Dictionary<long,Dictionary<long,System.DateTime>>(),
				x => listShown.Add(x),(x,y) => false,x => { },x => { },x => null);
			return listShown;
		}
		#endregion Helpers
	}
}
