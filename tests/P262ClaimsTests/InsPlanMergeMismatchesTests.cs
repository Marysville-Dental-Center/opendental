// P262-CLAIMS item 21: the detailed warning of Combine Insurance Plans, the fields that do not match. Rules without a database (fields given by the
// test), then the scratch database with the real fields and their shown values, and the benefits line.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using UnitTestsCore;

namespace P262ClaimsTests {
	[TestClass]
	public class InsPlanMergeMismatchesTests {
		private static readonly List<InsPlanMergeMismatches.Field> _listFieldsPlain=new List<InsPlanMergeMismatches.Field>() {
			new InsPlanMergeMismatches.Field(nameof(InsPlan.GroupNum),"Group Num",x => x.GroupNum),
			new InsPlanMergeMismatches.Field(nameof(InsPlan.UseAltCode),"Use Alternate Code",x => x.UseAltCode),
			new InsPlanMergeMismatches.Field(nameof(InsPlan.PerVisitPatAmount),"Per Visit",x => x.PerVisitPatAmount,x => ((double)x).ToString("F2")),
		};

		[TestMethod]
		public void GetMismatches_ListsEachDifferingFieldOfEachOtherPlan() {
			InsPlan insPlanKept=new InsPlan() { PlanNum=1,GroupNum="100",UseAltCode=false,PerVisitPatAmount=10 };
			InsPlan insPlanSame=new InsPlan() { PlanNum=2,GroupNum="100",UseAltCode=false,PerVisitPatAmount=10 };
			InsPlan insPlanDiffers=new InsPlan() { PlanNum=3,GroupNum="200",UseAltCode=true,PerVisitPatAmount=10 };
			List<InsPlanMismatch> listMismatches=InsPlanMergeMismatches.GetMismatches(insPlanKept,new[] { insPlanKept,insPlanSame,insPlanDiffers },
				_listFieldsPlain);
			Assert.AreEqual(2,listMismatches.Count);
			Assert.IsTrue(listMismatches.All(x => x.PlanNumOther==3),"The kept plan and the identical plan have no rows.");
			Assert.AreEqual("Group Num",listMismatches[0].Label);
			Assert.AreEqual("100",listMismatches[0].ValueKept);
			Assert.AreEqual("200",listMismatches[0].ValueOther);
			Assert.AreEqual(nameof(InsPlan.UseAltCode),listMismatches[1].FieldName);
			Assert.AreEqual("No",listMismatches[1].ValueKept,"True and false are shown as Yes and No.");
			Assert.AreEqual("Yes",listMismatches[1].ValueOther);
		}

		[TestMethod]
		public void GetMismatches_PlansInTheOrderGiven() {
			InsPlan insPlanKept=new InsPlan() { PlanNum=1,GroupNum="1" };
			List<InsPlanMismatch> listMismatches=InsPlanMergeMismatches.GetMismatches(insPlanKept,
				new[] { new InsPlan() { PlanNum=9,GroupNum="9" },new InsPlan() { PlanNum=4,GroupNum="4" } },_listFieldsPlain);
			CollectionAssert.AreEqual(new List<long>() { 9,4 },listMismatches.Select(x => x.PlanNumOther).ToList());
		}

		[TestMethod]
		public void GetMismatches_BlankTextsAreShown() {
			InsPlan insPlanKept=new InsPlan() { PlanNum=1,GroupNum="" };
			InsPlanMismatch insPlanMismatch=InsPlanMergeMismatches.GetMismatches(insPlanKept,new[] { new InsPlan() { PlanNum=2,GroupNum="5" } },
				_listFieldsPlain).Single();
			Assert.AreEqual("(blank)",insPlanMismatch.ValueKept);
			Assert.AreEqual("5",insPlanMismatch.ValueOther);
		}

		[TestMethod]
		public void AreSame_TextsExactlyAmountsToTheCent() {
			Assert.IsTrue(InsPlanMergeMismatches.AreSame("a","a"));
			Assert.IsFalse(InsPlanMergeMismatches.AreSame("a","A"),"Texts are compared exactly.");
			Assert.IsTrue(InsPlanMergeMismatches.AreSame(null,""),"No text and an empty text are the same.");
			Assert.IsTrue(InsPlanMergeMismatches.AreSame(10.0,10.0000001));
			Assert.IsFalse(InsPlanMergeMismatches.AreSame(10.0,10.01));
			Assert.IsTrue(InsPlanMergeMismatches.AreSame(5L,5L));
			Assert.IsFalse(InsPlanMergeMismatches.AreSame(5L,6L));
			Assert.IsTrue(InsPlanMergeMismatches.AreSame(EnumCobRule.Basic,EnumCobRule.Basic));
			Assert.IsFalse(InsPlanMergeMismatches.AreSame(EnumCobRule.Basic,EnumCobRule.Standard));
		}

		[TestMethod]
		public void GetFields_EveryUserVisibleColumnOnce() {
			//Every InsPlan column but the key, the security columns and the display-only NumberSubscribers. A column added later fails here until it is
			//compared (or added to this list of exceptions with a reason).
			List<string> listExcluded=new List<string>() { nameof(InsPlan.PlanNum),nameof(InsPlan.SecUserNumEntry),nameof(InsPlan.SecDateEntry),
				nameof(InsPlan.SecDateTEdit),nameof(InsPlan.NumberSubscribers) };
			List<string> listColumns=typeof(InsPlan).GetFields(BindingFlags.Public|BindingFlags.Instance).Select(x => x.Name)
				.Where(x => !listExcluded.Contains(x)).ToList();
			List<string> listCompared=InsPlanMergeMismatches.GetFields().Select(x => x.FieldName).ToList();
			Assert.AreEqual(listCompared.Count,listCompared.Distinct().Count(),"No field twice.");
			CollectionAssert.AreEquivalent(listColumns,listCompared);
			Assert.IsTrue(InsPlanMergeMismatches.GetFields().All(x => !string.IsNullOrWhiteSpace(x.Label)));
		}

		[TestMethod]
		public void GetFields_EachSeesAChangeOfItsColumn() {
			//A plan that differs from another in any one compared column gives a row for it.
			InsPlan insPlanA=new InsPlan() { PlanNum=1 };
			foreach(InsPlanMergeMismatches.Field field in InsPlanMergeMismatches.GetFields()) {
				FieldInfo fieldInfo=typeof(InsPlan).GetField(field.FieldName);
				InsPlan insPlanB=insPlanA.Copy();
				object value=fieldInfo.GetValue(insPlanB);
				fieldInfo.SetValue(insPlanB,GetOtherValue(fieldInfo.FieldType,value));
				Assert.IsFalse(InsPlanMergeMismatches.AreSame(field.FuncGetValue(insPlanA),field.FuncGetValue(insPlanB)),field.FieldName);
			}
		}

		[TestMethod]
		public void GetBenefitMismatches_SameSetInAnyOrderIsNotListed() {
			InsPlan insPlanKept=new InsPlan() { PlanNum=1 };
			InsPlan insPlanSame=new InsPlan() { PlanNum=2 };
			InsPlan insPlanDiffers=new InsPlan() { PlanNum=3 };
			InsPlan insPlanNone=new InsPlan() { PlanNum=4 };
			Dictionary<long,List<Benefit>> dictBenefits=new Dictionary<long,List<Benefit>>() {
				{ 1,new List<Benefit>() { Percent(1,10,80),Percent(1,11,50) } },
				{ 2,new List<Benefit>() { Percent(2,11,50),Percent(2,10,80) } },//same set, other order and keys
				{ 3,new List<Benefit>() { Percent(3,10,80),Percent(3,11,60) } },
			};
			List<InsPlanMismatch> listMismatches=InsPlanMergeMismatches.GetBenefitMismatches(insPlanKept,
				new[] { insPlanKept,insPlanSame,insPlanDiffers,insPlanNone },dictBenefits);
			CollectionAssert.AreEqual(new List<long>() { 3,4 },listMismatches.Select(x => x.PlanNumOther).ToList());
			Assert.AreEqual(InsPlanMergeMismatches.FIELD_BENEFITS,listMismatches[0].FieldName);
			Assert.AreEqual("2 benefits",listMismatches[0].ValueKept);
			Assert.AreEqual("2 benefits, not the same",listMismatches[0].ValueOther);
			Assert.AreEqual("0 benefits, not the same",listMismatches[1].ValueOther);
		}

		[TestMethod]
		public void GetBenefitMismatches_PatientLevelBenefitsAreNotThePlans() {
			InsPlan insPlanKept=new InsPlan() { PlanNum=1 };
			InsPlan insPlanOther=new InsPlan() { PlanNum=2 };
			Benefit benefitPatPlan=Percent(2,10,80);
			benefitPatPlan.PatPlanNum=55;//a patient override, not deleted by combining
			Dictionary<long,List<Benefit>> dictBenefits=new Dictionary<long,List<Benefit>>() { { 2,new List<Benefit>() { benefitPatPlan } } };
			Assert.AreEqual(0,InsPlanMergeMismatches.GetBenefitMismatches(insPlanKept,new[] { insPlanOther },dictBenefits).Count);
		}

		[TestMethod]
		public void Formats() {
			Assert.AreEqual("Category Percentage",InsPlanMergeMismatches.GetPlanTypeText(""));
			Assert.AreEqual("PPO Percentage",InsPlanMergeMismatches.GetPlanTypeText("p"));
			Assert.AreEqual("Medicaid or Flat Co-pay",InsPlanMergeMismatches.GetPlanTypeText("f"));
			Assert.AreEqual("Capitation",InsPlanMergeMismatches.GetPlanTypeText("c"));
			Assert.AreEqual("Calendar Year",InsPlanMergeMismatches.GetMonthRenewText(0));
			ClaimsDb.UseUsEnglish();
			Assert.AreEqual("July",InsPlanMergeMismatches.GetMonthRenewText(7));
			Assert.AreEqual("Use practice default",InsPlanMergeMismatches.GetYNText(YN.Unknown));
			Assert.AreEqual("Yes",InsPlanMergeMismatches.GetYNText(YN.Yes));
			Assert.AreEqual("12.50",InsPlanMergeMismatches.GetAmountText(12.5));
		}

		public static Benefit Percent(long planNum,long covCatNum,int percent) {
			return new Benefit() { PlanNum=planNum,CovCatNum=covCatNum,BenefitType=InsBenefitType.CoInsurance,Percent=percent,
				TimePeriod=BenefitTimePeriod.CalendarYear,CoverageLevel=BenefitCoverageLevel.None,ToothRange="" };
		}

		private static object GetOtherValue(Type type,object value) {
			if(type==typeof(string)) {
				return (string)value+"x";
			}
			if(type==typeof(bool)) {
				return !(bool)value;
			}
			if(type==typeof(long)) {
				return (long)value+1;
			}
			if(type==typeof(int)) {
				return (int)value+1;
			}
			if(type==typeof(byte)) {
				return (byte)((byte)value+1);
			}
			if(type==typeof(double)) {
				return (double)value+1;
			}
			if(type.IsEnum) {
				return Enum.ToObject(type,Convert.ToInt32(value)+1);
			}
			throw new NotSupportedException(type.Name);
		}
	}

	[TestClass]
	public class InsPlanMergeMismatchesDbTests {
		[TestInitialize]
		public void Init() {
			ClaimsDb.Begin();
		}

		[TestMethod]
		public void GetMismatches_ShowsNamesNotKeys() {
			string suffix=ClaimsDb.NewSuffix();
			Carrier carrierKept=CarrierT.CreateCarrier("Kept Carrier "+suffix);
			Carrier carrierOther=CarrierT.CreateCarrier("Other Carrier "+suffix);
			InsPlan insPlanKept=InsPlanT.CreateInsPlan(carrierKept.CarrierNum,feeSched:0,groupNum:"G1",planType:"");
			InsPlan insPlanOther=InsPlanT.CreateInsPlan(carrierOther.CarrierNum,feeSched:0,groupNum:"G1",planType:"p");
			insPlanOther.MonthRenew=7;
			insPlanOther.IsBlueBookEnabled=insPlanKept.IsBlueBookEnabled;
			InsPlans.Update(insPlanOther);
			insPlanOther=InsPlans.RefreshOne(insPlanOther.PlanNum);
			insPlanKept=InsPlans.RefreshOne(insPlanKept.PlanNum);
			List<InsPlanMismatch> listMismatches=InsPlanMergeMismatches.GetMismatches(insPlanKept,new[] { insPlanOther });
			CollectionAssert.AreEqual(new List<string>() { "Carrier","Plan Type","Benefit Year Renew Month" },listMismatches.Select(x => x.Label).ToList());
			Assert.AreEqual(carrierKept.CarrierName,listMismatches[0].ValueKept);
			Assert.AreEqual(carrierOther.CarrierName,listMismatches[0].ValueOther);
			Assert.AreEqual("Category Percentage",listMismatches[1].ValueKept);
			Assert.AreEqual("PPO Percentage",listMismatches[1].ValueOther);
			Assert.AreEqual("Calendar Year",listMismatches[2].ValueKept);
			Assert.AreEqual("July",listMismatches[2].ValueOther);
			StringAssert.Contains(InsPlanMergeMismatches.GetPlanDescription(insPlanOther),carrierOther.CarrierName);
			StringAssert.Contains(InsPlanMergeMismatches.GetPlanDescription(insPlanOther),"#G1");
		}

		[TestMethod]
		public void GetMismatches_IdenticalPlansHaveNone() {
			string suffix=ClaimsDb.NewSuffix();
			Carrier carrier=CarrierT.CreateCarrier("Same Carrier "+suffix);
			InsPlan insPlanA=InsPlans.RefreshOne(InsPlanT.CreateInsPlan(carrier.CarrierNum,groupNum:"S1",groupName:"Same").PlanNum);
			InsPlan insPlanB=InsPlans.RefreshOne(InsPlanT.CreateInsPlan(carrier.CarrierNum,groupNum:"S1",groupName:"Same").PlanNum);
			Assert.AreEqual(0,InsPlanMergeMismatches.GetMismatches(insPlanA,new[] { insPlanB }).Count);
			Assert.IsTrue(InsPlans.AreEqualValue(insPlanA,SetPlanNum(insPlanB,insPlanA.PlanNum)),"The plans really are the same but for the key.");
		}

		[TestMethod]
		public void GetBenefitsForPlans_PlanLevelBenefitsByPlan() {
			string suffix=ClaimsDb.NewSuffix();
			Carrier carrier=CarrierT.CreateCarrier("Benefit Carrier "+suffix);
			InsPlan insPlanA=InsPlanT.CreateInsPlan(carrier.CarrierNum);
			InsPlan insPlanB=InsPlanT.CreateInsPlan(carrier.CarrierNum);
			InsPlan insPlanC=InsPlanT.CreateInsPlan(carrier.CarrierNum);
			Benefits.Insert(InsPlanMergeMismatchesTests.Percent(insPlanA.PlanNum,1,80));
			Benefits.Insert(InsPlanMergeMismatchesTests.Percent(insPlanB.PlanNum,1,50));
			Dictionary<long,List<Benefit>> dictBenefits=InsPlanMergeMismatches.GetBenefitsForPlans(
				new List<long>() { insPlanA.PlanNum,insPlanB.PlanNum,insPlanC.PlanNum });
			Assert.AreEqual(1,dictBenefits[insPlanA.PlanNum].Count);
			Assert.AreEqual(80,dictBenefits[insPlanA.PlanNum][0].Percent);
			Assert.IsFalse(dictBenefits.ContainsKey(insPlanC.PlanNum),"A plan without benefits is missing.");
			List<InsPlanMismatch> listMismatches=InsPlanMergeMismatches.GetBenefitMismatches(insPlanA,new[] { insPlanB,insPlanC },dictBenefits);
			CollectionAssert.AreEqual(new List<long>() { insPlanB.PlanNum,insPlanC.PlanNum },listMismatches.Select(x => x.PlanNumOther).ToList());
			Assert.AreEqual(0,InsPlanMergeMismatches.GetBenefitsForPlans(new List<long>()).Count);
		}

		private static InsPlan SetPlanNum(InsPlan insPlan,long planNum) {
			InsPlan insPlanCopy=insPlan.Copy();
			insPlanCopy.PlanNum=planNum;
			return insPlanCopy;
		}
	}
}
