// Rules of the redesigned Edit Benefit window (Open Dental 25.2), without a database: which inputs each type uses, the check that names a field
// the type ignores (while benefits already saved that way still save), and the plain-language preview line.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace BINSHTests {
	[TestClass]
	public class BenefitEditRulesTests {
		[TestInitialize]
		public void TestInitialize() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
		}

		[TestMethod]
		public void IsFieldUsed_InputsByType() {
			//Percent for coinsurance.
			Assert.IsTrue(BenefitEditRules.IsFieldUsed(InsBenefitType.CoInsurance,EnumBenefitField.Percent));
			Assert.IsFalse(BenefitEditRules.IsFieldUsed(InsBenefitType.Limitations,EnumBenefitField.Percent));
			Assert.IsFalse(BenefitEditRules.IsFieldUsed(InsBenefitType.Exclusions,EnumBenefitField.Percent));
			//Amount for limitations and deductibles (and copayments, which the 24.3 checks allow an amount for).
			Assert.IsTrue(BenefitEditRules.IsFieldUsed(InsBenefitType.Limitations,EnumBenefitField.Amount));
			Assert.IsTrue(BenefitEditRules.IsFieldUsed(InsBenefitType.Deductible,EnumBenefitField.Amount));
			Assert.IsTrue(BenefitEditRules.IsFieldUsed(InsBenefitType.CoPayment,EnumBenefitField.Amount));
			Assert.IsFalse(BenefitEditRules.IsFieldUsed(InsBenefitType.CoInsurance,EnumBenefitField.Amount));
			//Quantity and qualifier for frequency and age limits and waiting periods.
			Assert.IsTrue(BenefitEditRules.IsFieldUsed(InsBenefitType.Limitations,EnumBenefitField.Quantity));
			Assert.IsTrue(BenefitEditRules.IsFieldUsed(InsBenefitType.WaitingPeriod,EnumBenefitField.Qualifier));
			Assert.IsFalse(BenefitEditRules.IsFieldUsed(InsBenefitType.Deductible,EnumBenefitField.Quantity));
			Assert.IsFalse(BenefitEditRules.IsFieldUsed(InsBenefitType.Exclusions,EnumBenefitField.Qualifier));
			//Treatment area for limitations, teeth for exclusions.
			Assert.IsTrue(BenefitEditRules.IsFieldUsed(InsBenefitType.Limitations,EnumBenefitField.TreatArea));
			Assert.IsFalse(BenefitEditRules.IsFieldUsed(InsBenefitType.CoInsurance,EnumBenefitField.TreatArea));
			Assert.IsTrue(BenefitEditRules.IsFieldUsed(InsBenefitType.Exclusions,EnumBenefitField.Teeth));
			Assert.IsFalse(BenefitEditRules.IsFieldUsed(InsBenefitType.Limitations,EnumBenefitField.Teeth));
			//Time period and coverage level are used by every type.
			foreach(InsBenefitType insBenefitType in Enum.GetValues(typeof(InsBenefitType))) {
				Assert.IsTrue(BenefitEditRules.IsFieldUsed(insBenefitType,EnumBenefitField.TimePeriod));
				Assert.IsTrue(BenefitEditRules.IsFieldUsed(insBenefitType,EnumBenefitField.Level));
				Assert.IsTrue(BenefitEditRules.IsFieldUsed(insBenefitType,EnumBenefitField.Category));
			}
		}

		[TestMethod]
		public void IsFieldUsed_EveryTypeTheBaselineChecksAllowAreUsed() {
			//Whatever Benefits.GetValidationErrors refuses for a type must not be offered as used: an amount on coinsurance, a percent on limitations.
			foreach(InsBenefitType insBenefitType in Enum.GetValues(typeof(InsBenefitType))) {
				Benefit benefit=new Benefit { BenefitType=insBenefitType,PlanNum=1 };
				if(BenefitEditRules.IsFieldUsed(insBenefitType,EnumBenefitField.Amount)) {
					benefit.MonetaryAmt=100;
					Assert.IsFalse(Benefits.GetValidationErrors(benefit,0,false).Any(x => x.Field==EnumBenefitField.Amount),insBenefitType.ToString());
				}
				benefit=new Benefit { BenefitType=insBenefitType,PlanNum=1 };
				if(BenefitEditRules.IsFieldUsed(insBenefitType,EnumBenefitField.Percent)) {
					benefit.Percent=80;
					Assert.IsFalse(Benefits.GetValidationErrors(benefit,0,false).Any(x => x.Field==EnumBenefitField.Percent),insBenefitType.ToString());
				}
			}
		}

		[TestMethod]
		public void GetIgnoredFieldErrors_ValueTheTypeIgnoresIsNamed() {
			Benefit benefit=new Benefit { BenefitType=InsBenefitType.Exclusions,PlanNum=1,Quantity=2,QuantityQualifier=BenefitQuantity.NumberOfServices };
			List<BenefitValidationError> listErrors=BenefitEditRules.GetIgnoredFieldErrors(benefit,new Benefit { BenefitType=InsBenefitType.Exclusions });
			CollectionAssert.AreEquivalent(new[] { EnumBenefitField.Quantity,EnumBenefitField.Qualifier },listErrors.Select(x => x.Field).ToArray());
			StringAssert.Contains(listErrors.First(x => x.Field==EnumBenefitField.Quantity).Message,"Quantity");
			StringAssert.Contains(listErrors.First(x => x.Field==EnumBenefitField.Quantity).Message,"Exclusion");
			StringAssert.Contains(listErrors.First(x => x.Field==EnumBenefitField.Qualifier).Message,"Qualifier");
		}

		[TestMethod]
		public void GetIgnoredFieldErrors_UsedValuesAreFine() {
			Benefit benefit=new Benefit { BenefitType=InsBenefitType.Limitations,PlanNum=1,MonetaryAmt=1500,TimePeriod=BenefitTimePeriod.CalendarYear,
				CoverageLevel=BenefitCoverageLevel.Individual };
			Assert.AreEqual(0,BenefitEditRules.GetIgnoredFieldErrors(benefit,null).Count);
			benefit=new Benefit { BenefitType=InsBenefitType.CoInsurance,PlanNum=1,Percent=80,TimePeriod=BenefitTimePeriod.CalendarYear };
			Assert.AreEqual(0,BenefitEditRules.GetIgnoredFieldErrors(benefit,null).Count,"Coinsurance with a time period, as plan imports make them.");
			benefit=new Benefit { BenefitType=InsBenefitType.Exclusions,PlanNum=1,ToothRange="2,15" };
			Assert.AreEqual(0,BenefitEditRules.GetIgnoredFieldErrors(benefit,null).Count);
		}

		[TestMethod]
		public void GetIgnoredFieldErrors_ValueAlreadySavedThatWayStillSaves() {
			//24.3 kept exclusions with a 0 percent for backward compatibility; such rows must still save unchanged.
			Benefit benefitLoaded=new Benefit { BenefitType=InsBenefitType.Exclusions,PlanNum=1,Percent=0 };
			Benefit benefit=benefitLoaded.Copy();
			Assert.AreEqual(0,BenefitEditRules.GetIgnoredFieldErrors(benefit,benefitLoaded).Count);
			//The same value on a new benefit is refused.
			Assert.AreEqual(1,BenefitEditRules.GetIgnoredFieldErrors(benefit,new Benefit { BenefitType=InsBenefitType.Exclusions }).Count);
			//Changing the type makes every ignored value count again.
			benefitLoaded=new Benefit { BenefitType=InsBenefitType.Limitations,PlanNum=1,MonetaryAmt=1000 };
			benefit=benefitLoaded.Copy();
			benefit.BenefitType=InsBenefitType.CoInsurance;
			List<BenefitValidationError> listErrors=BenefitEditRules.GetIgnoredFieldErrors(benefit,benefitLoaded);
			Assert.AreEqual(1,listErrors.Count);
			Assert.AreEqual(EnumBenefitField.Amount,listErrors[0].Field);
			StringAssert.Contains(listErrors[0].Message,"Amount");
		}

		[TestMethod]
		public void GetPreviewText_SpecExample() {
			Benefit benefit=new Benefit { BenefitType=InsBenefitType.Limitations,Quantity=2,QuantityQualifier=BenefitQuantity.NumberOfServices,
				TimePeriod=BenefitTimePeriod.CalendarYear,CoverageLevel=BenefitCoverageLevel.Individual };
			Assert.AreEqual("Limitation: 2 per Calendar Year, Individual, Category: Preventive",
				BenefitEditRules.GetPreviewText(benefit,"Preventive","","","",false));
		}

		[TestMethod]
		public void GetPreviewText_OtherShapes() {
			Benefit benefit=new Benefit { BenefitType=InsBenefitType.CoInsurance,Percent=80,TimePeriod=BenefitTimePeriod.CalendarYear };
			Assert.AreEqual("Coinsurance: 80%, Calendar Year, Category: Basic",BenefitEditRules.GetPreviewText(benefit,"Basic","","","",false));
			benefit=new Benefit { BenefitType=InsBenefitType.Deductible,MonetaryAmt=50,TimePeriod=BenefitTimePeriod.ServiceYear,
				CoverageLevel=BenefitCoverageLevel.Family };
			Assert.AreEqual("Deductible: $50.00, Service Year, Family, General",BenefitEditRules.GetPreviewText(benefit,"","","","",false));
			benefit=new Benefit { BenefitType=InsBenefitType.Limitations,Quantity=18,QuantityQualifier=BenefitQuantity.AgeLimit };
			Assert.AreEqual("Limitation: through age 18, Code Group: Fluoride",BenefitEditRules.GetPreviewText(benefit,"","","Fluoride","",false));
			benefit=new Benefit { BenefitType=InsBenefitType.Limitations,Quantity=5,QuantityQualifier=BenefitQuantity.Years };
			Assert.AreEqual("Limitation: every 5 years, Code: Z0330",BenefitEditRules.GetPreviewText(benefit,"","Z0330","","",false));
			benefit=new Benefit { BenefitType=InsBenefitType.WaitingPeriod,Quantity=6,QuantityQualifier=BenefitQuantity.Months };
			Assert.AreEqual("Waiting Period: 6 months, Category: Major",BenefitEditRules.GetPreviewText(benefit,"Major","","","",false));
			benefit=new Benefit { BenefitType=InsBenefitType.Limitations,Quantity=2,QuantityQualifier=BenefitQuantity.NumberOfServices,
				TimePeriod=BenefitTimePeriod.NumberInLast12Months,TreatArea=TreatmentArea.Tooth };
			Assert.AreEqual("Limitation: 2 in the Last 12 Months, Treatment Area: Tooth, Code Group: Fillings, Patient Override",
				BenefitEditRules.GetPreviewText(benefit,"","","Fillings","",true));
			benefit=new Benefit { BenefitType=InsBenefitType.Exclusions };
			Assert.AreEqual("Exclusion: teeth 2,15, Code: Z2740",BenefitEditRules.GetPreviewText(benefit,"","Z2740","","2,15",false));
			benefit=new Benefit { BenefitType=InsBenefitType.Limitations,MonetaryAmt=1500 };
			Assert.AreEqual("Limitation: $1,500.00, General",BenefitEditRules.GetPreviewText(benefit,"","","","2,15",false),
				"Teeth only show for types that use them.");
		}

		[TestMethod]
		public void GetPreviewText_ProcCodeWinsOverTheOtherNames() {
			//The window passes only the input picked under Applies to, but the line still names one thing if more are passed.
			Benefit benefit=new Benefit { BenefitType=InsBenefitType.Exclusions };
			Assert.AreEqual("Exclusion: Code: Z2740",BenefitEditRules.GetPreviewText(benefit,"Basic","Z2740","Crowns","",false));
		}
	}
}
