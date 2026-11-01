// Benefit rules without a database: which benefits take teeth (exclusions, 25.3/25.4; age limits, 26.1), which age limit governs a tooth, the
// Other Benefits grid row rules, and the benefit period of treatment plan loop entries (25.4).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace M2ESTTests {
	[TestClass]
	public class BenefitRulesTests {
		[TestInitialize]
		public void TestInitialize() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
		}

		private static Benefit AgeLimit(byte age,string toothRange="",long codeGroupNum=7,long covCatNum=0,long codeNum=0,
			BenefitCoverageLevel coverageLevel=BenefitCoverageLevel.None)
		{
			return new Benefit { PlanNum=1,BenefitType=InsBenefitType.Limitations,QuantityQualifier=BenefitQuantity.AgeLimit,Quantity=age,
				CodeGroupNum=codeGroupNum,CovCatNum=covCatNum,CodeNum=codeNum,ToothRange=toothRange,CoverageLevel=coverageLevel };
		}

		#region Which benefits take teeth
		[TestMethod]
		public void CanHaveTeeth_ExclusionsAndAgeLimitsOnly() {
			Assert.IsTrue(Benefits.CanHaveTeeth(InsBenefitType.Exclusions,BenefitQuantity.None));
			Assert.IsTrue(Benefits.CanHaveTeeth(InsBenefitType.Exclusions,BenefitQuantity.AgeLimit));
			Assert.IsTrue(Benefits.CanHaveTeeth(InsBenefitType.Limitations,BenefitQuantity.AgeLimit));
			Assert.IsFalse(Benefits.CanHaveTeeth(InsBenefitType.Limitations,BenefitQuantity.None),"Annual max.");
			Assert.IsFalse(Benefits.CanHaveTeeth(InsBenefitType.Limitations,BenefitQuantity.Months),"Frequency.");
			Assert.IsFalse(Benefits.CanHaveTeeth(InsBenefitType.Limitations,BenefitQuantity.NumberOfServices));
			Assert.IsFalse(Benefits.CanHaveTeeth(InsBenefitType.CoInsurance,BenefitQuantity.None));
			Assert.IsFalse(Benefits.CanHaveTeeth(InsBenefitType.Deductible,BenefitQuantity.AgeLimit));
			Assert.IsFalse(Benefits.CanHaveTeeth(InsBenefitType.WaitingPeriod,BenefitQuantity.Months));
		}

		[TestMethod]
		public void ValidationErrors_TeethAllowedOnAgeLimits() {
			Benefit benefitAgeLimit=AgeLimit(14,"2,3,14,15");
			Assert.IsFalse(Benefits.GetValidationErrors(benefitAgeLimit,0,false).Any(x => x.Field==EnumBenefitField.Teeth),
				"Age limits take teeth from 26.1.");
			Benefit benefitExclusion=new Benefit { PlanNum=1,BenefitType=InsBenefitType.Exclusions,CodeNum=5,ToothRange="2" };
			Assert.AreEqual(0,Benefits.GetValidationErrors(benefitExclusion,0,false).Count);
			Benefit benefitMax=new Benefit { PlanNum=1,BenefitType=InsBenefitType.Limitations,MonetaryAmt=1000,ToothRange="2" };
			BenefitValidationError error=Benefits.GetValidationErrors(benefitMax,0,false).Single(x => x.Field==EnumBenefitField.Teeth);
			Assert.AreEqual("Teeth can only be entered for exclusions and age limitations.",error.Message);
		}

		[TestMethod]
		public void OtherBenefitsRow_KeepsTeethForAgeLimitsAndDropsThemElsewhere() {
			Benefit benefitBase=new Benefit { CodeGroupNum=7 };
			OtherBenefitsRowInput input=new OtherBenefitsRowInput { TypeIndex=(int)InsBenefitType.Limitations,QuantityQualifier=BenefitQuantity.AgeLimit,
				Quantity="14",Teeth="2-3,14-15" };
			Benefit benefit=Benefits.GetFromOtherBenefitsRow(benefitBase,input,1,0,ToothNumberingNomenclature.Universal,out List<BenefitValidationError> listErrors);
			Assert.AreEqual(0,listErrors.Count,string.Join("; ",listErrors.Select(x => x.Message)));
			Assert.AreEqual("2,3,14,15",benefit.ToothRange,"Ranges are expanded for storage.");
			Assert.AreEqual(14,benefit.Quantity);
			//The same teeth on a frequency row are not kept (the Teeth cell is read-only there).
			input.QuantityQualifier=BenefitQuantity.Months;
			input.Quantity="6";
			benefit=Benefits.GetFromOtherBenefitsRow(benefitBase,input,1,0,ToothNumberingNomenclature.Universal,out listErrors);
			Assert.AreEqual(0,listErrors.Count);
			Assert.AreEqual("",benefit.ToothRange);
			//Bad teeth on an age limit row mark the Teeth cell.
			input.QuantityQualifier=BenefitQuantity.AgeLimit;
			input.Quantity="14";
			input.Teeth="2,99";
			benefit=Benefits.GetFromOtherBenefitsRow(benefitBase,input,1,0,ToothNumberingNomenclature.Universal,out listErrors);
			Assert.IsNull(benefit);
			Assert.IsTrue(listErrors.Any(x => x.Field==EnumBenefitField.Teeth));
		}

		[TestMethod]
		public void OtherBenefitsRow_InternationalTeethAreStoredInternally() {
			OtherBenefitsRowInput input=new OtherBenefitsRowInput { TypeIndex=(int)InsBenefitType.Limitations,QuantityQualifier=BenefitQuantity.AgeLimit,
				Quantity="14",Teeth="16,17" };//FDI: upper right first and second molars
			Benefit benefit=Benefits.GetFromOtherBenefitsRow(new Benefit { CodeGroupNum=7 },input,1,0,ToothNumberingNomenclature.FDI,
				out List<BenefitValidationError> listErrors);
			Assert.AreEqual(0,listErrors.Count);
			Assert.AreEqual("2,3",benefit.ToothRange);
		}
		#endregion Which benefits take teeth

		#region Which age limit governs a tooth
		[TestMethod]
		public void AgeLimitForTeeth_RowWithTeethAppliesOnlyToItsTeeth() {
			Benefit benefit=AgeLimit(14,"2,3,14,15");
			List<Benefit> listBenefits=new List<Benefit> { benefit };
			Assert.IsTrue(Benefits.IsAgeLimitForTeeth(benefit,listBenefits,"3",""));
			Assert.IsFalse(Benefits.IsAgeLimitForTeeth(benefit,listBenefits,"4",""),"A premolar is not affected.");
			Assert.IsFalse(Benefits.IsAgeLimitForTeeth(benefit,listBenefits,"",""),"No tooth (quadrant, arch, mouth) never matches teeth.");
			Assert.IsTrue(Benefits.IsAgeLimitForTeeth(benefit,listBenefits,"","13,14"),"Any tooth of a tooth range.");
			Assert.IsFalse(Benefits.IsAgeLimitForTeeth(null,listBenefits,"3",""));
		}

		[TestMethod]
		public void AgeLimitForTeeth_RowWithoutTeethGovernsTheOtherTeeth() {
			Benefit benefitAll=AgeLimit(18);
			Benefit benefitMolars=AgeLimit(14,"2,3,14,15");
			List<Benefit> listBenefits=new List<Benefit> { benefitAll,benefitMolars };
			Assert.IsFalse(Benefits.IsAgeLimitForTeeth(benefitAll,listBenefits,"3",""),"The row with teeth governs tooth 3.");
			Assert.IsTrue(Benefits.IsAgeLimitForTeeth(benefitMolars,listBenefits,"3",""));
			Assert.IsTrue(Benefits.IsAgeLimitForTeeth(benefitAll,listBenefits,"4",""),"The row without teeth governs the rest.");
			Assert.IsTrue(Benefits.IsAgeLimitForTeeth(benefitAll,listBenefits,"",""),"Including procedures with no tooth.");
			Assert.IsTrue(Benefits.IsAgeLimitForTeeth(benefitAll,null,"3",""),"Nothing to compare with.");
		}

		[TestMethod]
		public void AgeLimitForTeeth_OnlyRowsOfTheSameScopeCompete() {
			Benefit benefitAll=AgeLimit(18,codeGroupNum:7);
			List<Benefit> listBenefits=new List<Benefit> {
				benefitAll,
				AgeLimit(14,"3",codeGroupNum:8),//another code group
				AgeLimit(14,"3",codeGroupNum:0,covCatNum:4),//a category
				AgeLimit(14,"3",codeGroupNum:7,coverageLevel:BenefitCoverageLevel.Family),//a family row
				AgeLimit(0,"3",codeGroupNum:7),//no age: not a limit
				new Benefit { BenefitType=InsBenefitType.Exclusions,CodeGroupNum=7,ToothRange="3" },//exclusions never consider age
			};
			Assert.IsTrue(Benefits.IsAgeLimitForTeeth(benefitAll,listBenefits,"3",""));
			listBenefits.Add(AgeLimit(14,"3",codeGroupNum:7,coverageLevel:BenefitCoverageLevel.Individual));
			Assert.IsFalse(Benefits.IsAgeLimitForTeeth(benefitAll,listBenefits,"3",""),"Individual and none are the same scope (not family).");
		}

		[TestMethod]
		public void AgeLimitForTeeth_PrimaryTeeth() {
			Benefit benefit=AgeLimit(10,Tooth.ParseToothList("A-B,I-J",ToothNumberingNomenclature.Universal));
			Assert.AreEqual("A,B,I,J",benefit.ToothRange);
			Assert.IsTrue(Benefits.IsAgeLimitForTeeth(benefit,new List<Benefit> { benefit },"I",""));
			Assert.IsFalse(Benefits.IsAgeLimitForTeeth(benefit,new List<Benefit> { benefit },"C",""));
		}
		#endregion Which age limit governs a tooth

		#region Benefit periods of loop entries
		[TestMethod]
		public void BenefitPeriod_CalendarAndServiceYears() {
			Benefits.GetBenefitPeriod(new DateTime(2027,3,15),new InsPlan { MonthRenew=0 },out DateTime dateStart,out DateTime dateEnd);
			Assert.AreEqual(new DateTime(2027,1,1),dateStart);
			Assert.AreEqual(new DateTime(2027,12,31),dateEnd);
			Benefits.GetBenefitPeriod(new DateTime(2027,3,15),new InsPlan { MonthRenew=7 },out dateStart,out dateEnd);
			Assert.AreEqual(new DateTime(2026,7,1),dateStart);
			Assert.AreEqual(new DateTime(2027,6,30),dateEnd);
			Benefits.GetBenefitPeriod(new DateTime(2027,7,1),new InsPlan { MonthRenew=7 },out dateStart,out dateEnd);
			Assert.AreEqual(new DateTime(2027,7,1),dateStart,"The renewal day starts the new period.");
			Benefits.GetBenefitPeriod(new DateTime(2027,6,30),new InsPlan { MonthRenew=7 },out dateStart,out dateEnd);
			Assert.AreEqual(new DateTime(2026,7,1),dateStart,"The day before is still the old period.");
		}

		[TestMethod]
		public void LoopHistInPeriod_OnlyMarkedEntriesAreFiltered() {
			DateTime dateStart=new DateTime(2027,1,1);
			DateTime dateEnd=new DateTime(2027,12,31);
			Benefit benefitYear=new Benefit { TimePeriod=BenefitTimePeriod.CalendarYear };
			ClaimProcHist histUnmarked=new ClaimProcHist { ProcDate=new DateTime(2026,10,25) };
			Assert.IsTrue(Benefits.IsLoopHistInPeriod(histUnmarked,benefitYear,dateStart,dateEnd),
				"Claims and other loops keep counting entries whatever their date.");
			ClaimProcHist histLastYear=new ClaimProcHist { ProcDate=new DateTime(2026,12,31),IsDateAsOf=true };
			ClaimProcHist histFirstDay=new ClaimProcHist { ProcDate=new DateTime(2027,1,1),IsDateAsOf=true };
			ClaimProcHist histLastDay=new ClaimProcHist { ProcDate=new DateTime(2027,12,31,14,0,0),IsDateAsOf=true };
			ClaimProcHist histNextYear=new ClaimProcHist { ProcDate=new DateTime(2028,1,1),IsDateAsOf=true };
			Assert.IsFalse(Benefits.IsLoopHistInPeriod(histLastYear,benefitYear,dateStart,dateEnd));
			Assert.IsTrue(Benefits.IsLoopHistInPeriod(histFirstDay,benefitYear,dateStart,dateEnd));
			Assert.IsTrue(Benefits.IsLoopHistInPeriod(histLastDay,benefitYear,dateStart,dateEnd));
			Assert.IsFalse(Benefits.IsLoopHistInPeriod(histNextYear,benefitYear,dateStart,dateEnd));
			Benefit benefitLifetime=new Benefit { TimePeriod=BenefitTimePeriod.Lifetime };
			Assert.IsTrue(Benefits.IsLoopHistInPeriod(histNextYear,benefitLifetime,dateStart,dateEnd),"Lifetime benefits count every entry.");
			Assert.IsFalse(Benefits.IsLoopHistInPeriod(histNextYear,null,dateStart,dateEnd));
			Assert.IsTrue(Benefits.IsLoopHistInPeriod(null,benefitYear,dateStart,dateEnd));
		}
		#endregion Benefit periods of loop entries
	}
}
