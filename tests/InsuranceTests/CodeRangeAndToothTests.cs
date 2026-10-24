// Code group ranges (24.4), tooth lists for exclusions (25.3) and the shared benefit checks of Edit Benefit and the Other Benefits grid. No
// database: everything here works on strings and objects in memory. Codes are made up (Z prefix).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace InsuranceTests {
	[TestClass]
	public class CodeRangeAndToothTests {
		[TestInitialize]
		public void TestInitialize() {
			//Messages go through Lans.g, which only skips the translation cache in en-US.
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
		}

		#region Code ranges
		[TestMethod]
		public void ProcCodeRanges_SingleCodes_MatchWholeCodesOnly() {
			ProcCodeRanges procCodeRanges=ProcCodeRanges.Parse("Z1110,Z1120");
			Assert.IsTrue(procCodeRanges.Contains("Z1110"));
			Assert.IsTrue(procCodeRanges.Contains("z1120"),"Codes compare without regard to case.");
			Assert.IsFalse(procCodeRanges.Contains("Z111"),"The 24.3 substring test let a shorter code match.");
			Assert.IsFalse(procCodeRanges.Contains("Z11101"));
			Assert.IsFalse(procCodeRanges.Contains(""));
			Assert.IsFalse(procCodeRanges.Contains(null));
		}

		[TestMethod]
		public void ProcCodeRanges_Range_HoldsEveryCodeBetweenTheEnds() {
			ProcCodeRanges procCodeRanges=ProcCodeRanges.Parse("Z2391-Z2394");
			Assert.IsFalse(procCodeRanges.Contains("Z2390"));
			Assert.IsTrue(procCodeRanges.Contains("Z2391"));
			Assert.IsTrue(procCodeRanges.Contains("Z2392"));
			Assert.IsTrue(procCodeRanges.Contains("Z2394"));
			Assert.IsFalse(procCodeRanges.Contains("Z2395"));
			Assert.IsFalse(procCodeRanges.Contains("Y2392"),"Another prefix is not in the range.");
			Assert.IsFalse(procCodeRanges.Contains("Z23920"),"A longer numeric part is not in the range.");
			Assert.IsFalse(procCodeRanges.Contains("Z2392A"),"A code with a suffix is not in the range.");
		}

		[TestMethod]
		public void ProcCodeRanges_LeadingZeros_AreKept() {
			//The 24.3 expansion rebuilt codes from integers and lost the zeros below 100.
			ProcCodeRanges procCodeRanges=ProcCodeRanges.Parse("Z0000-Z0999");
			Assert.IsTrue(procCodeRanges.Contains("Z0010"));
			Assert.IsTrue(procCodeRanges.Contains("Z0001"));
			Assert.IsTrue(procCodeRanges.Contains("Z0999"));
			Assert.IsFalse(procCodeRanges.Contains("Z1000"));
			Assert.IsFalse(procCodeRanges.Contains("Z10"));
		}

		[TestMethod]
		public void ProcCodeRanges_MixedList_RangesAndCodes() {
			ProcCodeRanges procCodeRanges=ProcCodeRanges.Parse(" Z0100-Z0199 , Z2140-Z2161,Z2750,,");
			Assert.AreEqual(3,procCodeRanges.ListEntries.Count);
			Assert.IsTrue(procCodeRanges.Contains("Z0150"));
			Assert.IsTrue(procCodeRanges.Contains("Z2160"));
			Assert.IsTrue(procCodeRanges.Contains("Z2750"));
			Assert.IsFalse(procCodeRanges.Contains("Z2751"));
			Assert.IsFalse(procCodeRanges.Contains("Z2162"));
		}

		[TestMethod]
		public void ProcCodeRanges_CodeWithDash_IsOneCode() {
			ProcCodeRanges procCodeRanges=ProcCodeRanges.Parse("Z1110-2,Z5000-Z50");
			Assert.IsTrue(procCodeRanges.Contains("Z1110-2"));
			Assert.IsFalse(procCodeRanges.Contains("Z1110"));
			Assert.IsFalse(procCodeRanges.Contains("Z1111"));
			Assert.IsTrue(procCodeRanges.Contains("Z5000-Z50"),"Ends of different lengths are not a range.");
			Assert.IsFalse(procCodeRanges.Contains("Z5000"));
		}

		[TestMethod]
		public void ProcCodeRanges_NumericCodes_RangeWithoutPrefix() {
			ProcCodeRanges procCodeRanges=ProcCodeRanges.Parse("01201-01205");
			Assert.IsTrue(procCodeRanges.Contains("01203"));
			Assert.IsFalse(procCodeRanges.Contains("01206"));
			Assert.IsFalse(procCodeRanges.Contains("1203"));
		}

		[TestMethod]
		public void ProcCodeRanges_ReversedRange_HoldsNothing() {
			ProcCodeRanges procCodeRanges=ProcCodeRanges.Parse("Z2394-Z2391");
			Assert.IsFalse(procCodeRanges.Contains("Z2392"));
			Assert.IsFalse(procCodeRanges.Contains("Z2394"));
		}

		[TestMethod]
		public void ProcCodeRanges_Get_IsCachedByText() {
			Assert.AreSame(ProcCodeRanges.Get("Z1,Z2-Z5"),ProcCodeRanges.Get("Z1,Z2-Z5"));
			Assert.AreEqual(0,ProcCodeRanges.Get(null).ListEntries.Count);
		}

		[TestMethod]
		public void ProcCodeRanges_Normalize_RemovesSpacesAndDuplicates() {
			string result=ProcCodeRanges.Normalize(" Z2391 - Z2394, Z2740 ,z2740,, Z2391-Z2394 ",out string error);
			Assert.AreEqual("",error);
			Assert.AreEqual("Z2391-Z2394,Z2740",result);
		}

		[TestMethod]
		public void ProcCodeRanges_Normalize_RefusesReversedRange() {
			string result=ProcCodeRanges.Normalize("Z2740,Z2394-Z2391",out string error);
			Assert.IsNull(result);
			StringAssert.Contains(error,"Z2394-Z2391");
		}

		[TestMethod]
		public void ProcCodeRanges_Normalize_AllowsCodesThatDoNotExist() {
			Assert.AreEqual("QQQ1,Z1110-2",ProcCodeRanges.Normalize("QQQ1,Z1110-2",out string error));
			Assert.AreEqual("",error);
		}
		#endregion Code ranges

		#region Tooth lists
		[TestMethod]
		public void ParseToothList_SinglesAndRanges_Universal() {
			Assert.AreEqual("2,15",Tooth.ParseToothList("2,15",ToothNumberingNomenclature.Universal));
			Assert.AreEqual("7,8,9,10,23,24,25,26",Tooth.ParseToothList("23-26, 7-10",ToothNumberingNomenclature.Universal));
			Assert.AreEqual("3,4,5",Tooth.ParseToothList("5-3",ToothNumberingNomenclature.Universal),"Ranges may be typed backwards.");
			Assert.AreEqual("2",Tooth.ParseToothList("2,2,2",ToothNumberingNomenclature.Universal));
			Assert.AreEqual("",Tooth.ParseToothList("  ",ToothNumberingNomenclature.Universal));
			Assert.AreEqual("",Tooth.ParseToothList(null,ToothNumberingNomenclature.Universal));
		}

		[TestMethod]
		public void ParseToothList_PrimaryAndSupernumerary() {
			Assert.AreEqual("A,B,C",Tooth.ParseToothList("a-c",ToothNumberingNomenclature.Universal));
			Assert.AreEqual("51,52,53",Tooth.ParseToothList("51-53",ToothNumberingNomenclature.Universal));
			Assert.AreEqual("AS,BS",Tooth.ParseToothList("AS-BS",ToothNumberingNomenclature.Universal));
			//Permanent first, then primary, then supernumerary.
			Assert.AreEqual("30,K,52",Tooth.ParseToothList("52,K,30",ToothNumberingNomenclature.Universal));
		}

		[TestMethod]
		public void ParseToothList_Invalid_Throws() {
			AssertThrows(() => Tooth.ParseToothList("33",ToothNumberingNomenclature.Universal));
			AssertThrows(() => Tooth.ParseToothList("2,X",ToothNumberingNomenclature.Universal));
			AssertThrows(() => Tooth.ParseToothList("30-K",ToothNumberingNomenclature.Universal),"A range mixing permanent and primary teeth.");
			AssertThrows(() => Tooth.ParseToothList("-5",ToothNumberingNomenclature.Universal));
			AssertThrows(() => Tooth.ParseToothList("2-",ToothNumberingNomenclature.Universal));
		}

		[TestMethod]
		public void ParseToothList_Fdi_StoresUniversalNumbers() {
			//FDI 17 is universal 2 and FDI 27 is universal 15.
			Assert.AreEqual("2,15",Tooth.ParseToothList("17,27",ToothNumberingNomenclature.FDI));
			//FDI 18-11 is the upper right quadrant, universal 1-8.
			Assert.AreEqual("1,2,3,4,5,6,7,8",Tooth.ParseToothList("18-11",ToothNumberingNomenclature.FDI));
			Assert.AreEqual("8,9",Tooth.ParseToothList("11-21",ToothNumberingNomenclature.FDI));
		}

		[TestMethod]
		public void ParseToothList_Haderup_LabelsWithDashes() {
			//Haderup labels contain dashes ("8-" is universal 32, "-1" is universal 24), so a single tooth must not be read as a range.
			Assert.AreEqual("32",Tooth.ParseToothList("8-",ToothNumberingNomenclature.Haderup));
			Assert.AreEqual("24",Tooth.ParseToothList("-1",ToothNumberingNomenclature.Haderup));
			Assert.AreEqual("1,2,3",Tooth.ParseToothList("8+-6+",ToothNumberingNomenclature.Haderup));
		}

		[TestMethod]
		public void DisplayToothList_CompressesRunsWithinAnArch() {
			Assert.AreEqual("2,15",Tooth.DisplayToothList("2,15",ToothNumberingNomenclature.Universal));
			Assert.AreEqual("7-10,23-26",Tooth.DisplayToothList("7,8,9,10,23,24,25,26",ToothNumberingNomenclature.Universal));
			Assert.AreEqual("15,16,17,18",Tooth.DisplayToothList("15,16,17,18",ToothNumberingNomenclature.Universal),"Runs break between the arches.");
			Assert.AreEqual("A-C",Tooth.DisplayToothList("A,B,C",ToothNumberingNomenclature.Universal));
			Assert.AreEqual("",Tooth.DisplayToothList("",ToothNumberingNomenclature.Universal));
			Assert.AreEqual("",Tooth.DisplayToothList(null,ToothNumberingNomenclature.Universal));
		}

		[TestMethod]
		public void DisplayToothList_RoundTripsThroughParse() {
			foreach(ToothNumberingNomenclature nomenclature in new[] { ToothNumberingNomenclature.Universal,ToothNumberingNomenclature.FDI,
				ToothNumberingNomenclature.Palmer,ToothNumberingNomenclature.Haderup })
			{
				string stored="1,2,3,4,9,14,15,23,24,25,26,30,A,B,C";
				string display=Tooth.DisplayToothList(stored,nomenclature);
				Assert.AreEqual(stored,Tooth.ParseToothList(display,nomenclature),nomenclature+": "+display);
			}
		}

		[TestMethod]
		public void IsAnyToothInList_ToothRangeAndNoTooth() {
			Assert.IsTrue(Tooth.IsAnyToothInList("2,15","2",""));
			Assert.IsFalse(Tooth.IsAnyToothInList("2,15","3",""));
			Assert.IsTrue(Tooth.IsAnyToothInList("2,15","","13,14,15"),"A bridge is excluded when any of its teeth is listed.");
			Assert.IsFalse(Tooth.IsAnyToothInList("2,15","","12,13,14"));
			Assert.IsFalse(Tooth.IsAnyToothInList("2,15","",""),"A procedure with no tooth is never in a list.");
			Assert.IsFalse(Tooth.IsAnyToothInList("","2",""));
		}
		#endregion Tooth lists

		#region Benefit checks
		[TestMethod]
		public void IsToothMatch_BlankOrNullMeansEveryTooth() {
			Benefit benefit=new Benefit { BenefitType=InsBenefitType.Exclusions };
			benefit.ToothRange=null;
			Assert.IsTrue(Benefits.IsToothMatch(benefit,"",""));
			benefit.ToothRange="";
			Assert.IsTrue(Benefits.IsToothMatch(benefit,"5",""));
			benefit.ToothRange="2,15";
			Assert.IsTrue(Benefits.IsToothMatch(benefit,"15",""));
			Assert.IsFalse(Benefits.IsToothMatch(benefit,"14",""));
			Assert.IsFalse(Benefits.IsToothMatch(benefit,"",""));
		}

		[TestMethod]
		public void GetValidationErrors_EditBenefitRules() {
			Benefit benefit=new Benefit { BenefitType=InsBenefitType.CoInsurance,Percent=80,CovCatNum=5 };
			Assert.AreEqual(0,Benefits.GetValidationErrors(benefit,0).Count);
			benefit=new Benefit { BenefitType=InsBenefitType.Exclusions,Percent=50,CovCatNum=5 };
			AssertField(benefit,0,EnumBenefitField.Percent);
			benefit=new Benefit { BenefitType=InsBenefitType.CoInsurance,Percent=101,CovCatNum=5 };
			AssertField(benefit,0,EnumBenefitField.Percent);
			benefit=new Benefit { BenefitType=InsBenefitType.CoInsurance,Percent=80,CovCatNum=5,CodeGroupNum=3 };
			AssertField(benefit,0,EnumBenefitField.Category);
			benefit=new Benefit { BenefitType=InsBenefitType.CoInsurance,Percent=80,CovCatNum=5,PatPlanNum=9 };
			AssertField(benefit,0,EnumBenefitField.Pat);
			Assert.AreEqual(0,Benefits.GetValidationErrors(benefit,9).Count);
			benefit=new Benefit { BenefitType=InsBenefitType.WaitingPeriod,CovCatNum=5,Quantity=6,QuantityQualifier=BenefitQuantity.Visits };
			AssertField(benefit,0,EnumBenefitField.Qualifier);
			benefit=new Benefit { BenefitType=InsBenefitType.Exclusions,CovCatNum=5,MonetaryAmt=100 };
			AssertField(benefit,0,EnumBenefitField.Amount);
			benefit=new Benefit { BenefitType=InsBenefitType.CoInsurance,Percent=80,CovCatNum=5,TreatArea=TreatmentArea.Tooth };
			AssertField(benefit,0,EnumBenefitField.TreatArea);
			benefit=new Benefit { BenefitType=InsBenefitType.Exclusions,CodeGroupNum=3,Quantity=2 };
			AssertField(benefit,0,EnumBenefitField.Qualifier);
		}

		[TestMethod]
		public void GetValidationErrors_TeethOnlyForExclusions() {
			Benefit benefit=new Benefit { BenefitType=InsBenefitType.Exclusions,CodeGroupNum=3,ToothRange="2,15" };
			Assert.AreEqual(0,Benefits.GetValidationErrors(benefit,0).Count);
			benefit.BenefitType=InsBenefitType.CoInsurance;
			benefit.Percent=50;
			AssertField(benefit,0,EnumBenefitField.Teeth);
		}

		[TestMethod]
		public void GetValidationErrors_FrequencyLimitation() {
			Benefit benefit=new Benefit { BenefitType=InsBenefitType.Limitations,CodeGroupNum=3,QuantityQualifier=BenefitQuantity.Months,Quantity=6,
				TreatArea=TreatmentArea.Tooth };
			Assert.AreEqual(0,Benefits.GetValidationErrors(benefit,0).Count,"Treat Area is allowed on frequency limitations.");
			benefit.CovCatNum=4;
			benefit.CodeGroupNum=0;
			AssertField(benefit,0,EnumBenefitField.Category);
		}

		[TestMethod]
		public void GetFromOtherBenefitsRow_CodeGroupExclusionWithTeeth() {
			Benefit benefitBase=new Benefit { CodeGroupNum=7 };
			OtherBenefitsRowInput input=new OtherBenefitsRowInput { TypeIndex=(int)InsBenefitType.Exclusions,Teeth="15, 2" };
			Benefit benefit=Benefits.GetFromOtherBenefitsRow(benefitBase,input,11,0,ToothNumberingNomenclature.Universal,out List<BenefitValidationError> listErrors);
			Assert.AreEqual(0,listErrors.Count,string.Join("; ",listErrors.Select(x => x.Message)));
			Assert.IsNotNull(benefit);
			Assert.AreEqual(InsBenefitType.Exclusions,benefit.BenefitType);
			Assert.AreEqual(7,benefit.CodeGroupNum);
			Assert.AreEqual(11,benefit.PlanNum);
			Assert.AreEqual("2,15",benefit.ToothRange);
			Assert.AreEqual(-1,benefit.Percent);
			Assert.AreEqual(-1,benefit.MonetaryAmt);
			Assert.AreEqual(0,benefitBase.ToothRange.Length,"The base benefit is not changed.");
		}

		[TestMethod]
		public void GetFromOtherBenefitsRow_TeethDroppedForOtherTypes() {
			OtherBenefitsRowInput input=new OtherBenefitsRowInput { TypeIndex=(int)InsBenefitType.CoInsurance,Percent="50",Teeth="2" };
			Benefit benefit=Benefits.GetFromOtherBenefitsRow(new Benefit { CodeGroupNum=7 },input,11,0,ToothNumberingNomenclature.Universal,out _);
			Assert.IsNotNull(benefit);
			Assert.AreEqual(50,benefit.Percent);
			Assert.AreEqual("",benefit.ToothRange);
		}

		[TestMethod]
		public void GetFromOtherBenefitsRow_BadCells_AreNamed() {
			OtherBenefitsRowInput input=new OtherBenefitsRowInput { TypeIndex=(int)InsBenefitType.Exclusions,Percent="abc",Amount="-5",Quantity="300",
				Teeth="40" };
			Benefit benefit=Benefits.GetFromOtherBenefitsRow(new Benefit { CodeGroupNum=7 },input,11,0,ToothNumberingNomenclature.Universal,
				out List<BenefitValidationError> listErrors);
			Assert.IsNull(benefit);
			List<EnumBenefitField> listFields=listErrors.Select(x => x.Field).ToList();
			CollectionAssert.Contains(listFields,EnumBenefitField.Percent);
			CollectionAssert.Contains(listFields,EnumBenefitField.Amount);
			CollectionAssert.Contains(listFields,EnumBenefitField.Quantity);
			CollectionAssert.Contains(listFields,EnumBenefitField.Teeth);
			input=new OtherBenefitsRowInput { Percent="50" };
			Assert.IsNull(Benefits.GetFromOtherBenefitsRow(new Benefit(),input,11,0,ToothNumberingNomenclature.Universal,out listErrors));
			Assert.AreEqual(EnumBenefitField.Type,listErrors[0].Field,"A row needs a type.");
		}

		[TestMethod]
		public void GetFromOtherBenefitsRow_PatOverride() {
			OtherBenefitsRowInput input=new OtherBenefitsRowInput { TypeIndex=(int)InsBenefitType.CoInsurance,Percent="70",IsPat=true,CovCatNum=4 };
			Benefit benefit=Benefits.GetFromOtherBenefitsRow(new Benefit(),input,11,22,ToothNumberingNomenclature.Universal,out _);
			Assert.AreEqual(0,benefit.PlanNum);
			Assert.AreEqual(22,benefit.PatPlanNum);
			Assert.AreEqual(4,benefit.CovCatNum);
			Assert.IsNull(Benefits.GetFromOtherBenefitsRow(new Benefit(),input,11,0,ToothNumberingNomenclature.Universal,out _),
				"No patient plan: the Pat box is not allowed.");
		}

		[TestMethod]
		public void OtherBenefitsRowInput_IsBlank() {
			Assert.IsTrue(new OtherBenefitsRowInput().IsBlank());
			Assert.IsTrue(new OtherBenefitsRowInput { CoverageLevel=BenefitCoverageLevel.Individual,TimePeriod=BenefitTimePeriod.CalendarYear }.IsBlank(),
				"Combo defaults alone do not make a benefit.");
			Assert.IsFalse(new OtherBenefitsRowInput { TypeIndex=(int)InsBenefitType.Exclusions }.IsBlank());
			Assert.IsFalse(new OtherBenefitsRowInput { Amount="10" }.IsBlank());
		}
		#endregion Benefit checks

		private static void AssertField(Benefit benefit,long patPlanNumAvailable,EnumBenefitField enumBenefitField) {
			List<BenefitValidationError> listErrors=Benefits.GetValidationErrors(benefit,patPlanNumAvailable);
			Assert.IsTrue(listErrors.Any(x => x.Field==enumBenefitField),"Expected an error on "+enumBenefitField+"; got: "
				+string.Join("; ",listErrors.Select(x => x.Field+" "+x.Message)));
		}

		private static void AssertThrows(Action action,string message="") {
			try {
				action();
			}
			catch(ApplicationException) {
				return;
			}
			Assert.Fail("Expected an ApplicationException. "+message);
		}
	}
}
