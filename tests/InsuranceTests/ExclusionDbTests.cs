// Exclusions limited to teeth (benefit.ToothRange, Open Dental 25.3) against a scratch database: IsExcluded for code, code group and category
// rows, the stored column (including NULL from an Open Dental database), the deductible, and full insurance estimates. Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using UnitTestsCore;

namespace InsuranceTests {
	[TestClass]
	public class ExclusionDbTests {
		private const long PLAN_NUM=990001;

		[TestInitialize]
		public void TestInitialize() {
			InsDb.Begin();
		}

		private static Benefit Exclusion(long codeNum=0,long codeGroupNum=0,long covCatNum=0,string toothRange="") {
			return new Benefit {
				PlanNum=PLAN_NUM,
				BenefitType=InsBenefitType.Exclusions,
				CodeNum=codeNum,
				CodeGroupNum=codeGroupNum,
				CovCatNum=covCatNum,
				ToothRange=toothRange,
			};
		}

		[TestMethod]
		public void IsExcluded_ProcCodeRowLimitedToTeeth() {
			ProcedureCode procedureCode=ProcedureCodeT.CreateProcCode("Z4750");
			List<Benefit> listBenefits=new List<Benefit> { Exclusion(codeNum:procedureCode.CodeNum,toothRange:"2,15") };
			Assert.IsTrue(Benefits.IsExcluded("Z4750",listBenefits,PLAN_NUM,0,"2",""));
			Assert.IsTrue(Benefits.IsExcluded("Z4750",listBenefits,PLAN_NUM,0,"15",""));
			Assert.IsFalse(Benefits.IsExcluded("Z4750",listBenefits,PLAN_NUM,0,"3",""),"Other teeth are estimated normally.");
			Assert.IsFalse(Benefits.IsExcluded("Z4750",listBenefits,PLAN_NUM,0,"",""),"A procedure with no tooth is not excluded by a tooth-limited row.");
			Assert.IsFalse(Benefits.IsExcluded("Z4750",listBenefits,PLAN_NUM,0),"The overload without teeth is a procedure with no tooth.");
			Assert.IsTrue(Benefits.IsExcluded("Z4750",listBenefits,PLAN_NUM,0,"","13,14,15"),"Bridges: any tooth of the range.");
			Assert.IsFalse(Benefits.IsExcluded("Z4750",listBenefits,PLAN_NUM,0,"","12,13,14"));
			Assert.IsFalse(Benefits.IsExcluded("Z4750",listBenefits,PLAN_NUM+1,0,"2",""),"Another plan's row does not apply.");
		}

		[TestMethod]
		public void IsExcluded_BlankToothRangeAppliesToEveryTooth() {
			ProcedureCode procedureCode=ProcedureCodeT.CreateProcCode("Z4751");
			List<Benefit> listBenefits=new List<Benefit> { Exclusion(codeNum:procedureCode.CodeNum) };
			Assert.IsTrue(Benefits.IsExcluded("Z4751",listBenefits,PLAN_NUM,0,"3",""));
			Assert.IsTrue(Benefits.IsExcluded("Z4751",listBenefits,PLAN_NUM,0,"",""));
			Assert.IsTrue(Benefits.IsExcluded("Z4751",listBenefits,PLAN_NUM,0));
		}

		[TestMethod]
		public void IsExcluded_CodeGroupRangeRowLimitedToTeeth() {
			CodeGroup codeGroup=CodeGroupDbTests.NewCodeGroup("Z4100-Z4199");
			List<Benefit> listBenefits=new List<Benefit> { Exclusion(codeGroupNum:codeGroup.CodeGroupNum,toothRange:"30") };
			Assert.IsTrue(Benefits.IsExcluded("Z4150",listBenefits,PLAN_NUM,0,"30",""));
			Assert.IsFalse(Benefits.IsExcluded("Z4150",listBenefits,PLAN_NUM,0,"31",""));
			Assert.IsFalse(Benefits.IsExcluded("Z4200",listBenefits,PLAN_NUM,0,"30",""),"Outside the group's range.");
			listBenefits[0].ToothRange="";
			Assert.IsTrue(Benefits.IsExcluded("Z4199",listBenefits,PLAN_NUM,0,"",""));
		}

		[TestMethod]
		public void IsExcluded_CategoryRowLimitedToTeeth() {
			CovCat covCat=new CovCat { Description="A-INS test category "+Guid.NewGuid().ToString("N").Substring(0,6),DefaultPercent=-1,CovOrder=900,
				EbenefitCat=EbenefitCategory.None };
			CovCats.Insert(covCat);
			CovSpans.Insert(new CovSpan { CovCatNum=covCat.CovCatNum,FromCode="Z4300",ToCode="Z4399" });
			CovCats.RefreshCache();
			CovSpans.RefreshCache();
			List<Benefit> listBenefits=new List<Benefit> { Exclusion(covCatNum:covCat.CovCatNum,toothRange:"8,9") };
			Assert.IsTrue(Benefits.IsExcluded("Z4310",listBenefits,PLAN_NUM,0,"9",""));
			Assert.IsFalse(Benefits.IsExcluded("Z4310",listBenefits,PLAN_NUM,0,"10",""));
		}

		[TestMethod]
		public void ToothRange_StoredAndNullLoadsAsBlank() {
			ProcedureCode procedureCode=ProcedureCodeT.CreateProcCode("Z4752");
			Benefit benefit=Exclusion(codeNum:procedureCode.CodeNum,toothRange:"2,15");
			Benefits.Insert(benefit);
			Benefit benefitDb=Benefits.GetForPlanOrPatPlan(PLAN_NUM,0).First(x => x.BenefitNum==benefit.BenefitNum);
			Assert.AreEqual("2,15",benefitDb.ToothRange);
			Benefit benefitOld=benefitDb.Copy();
			benefitDb.ToothRange="3";
			Benefits.UpdateList(new List<Benefit> { benefitOld },new List<Benefit> { benefitDb });
			Assert.AreEqual("3",Benefits.GetForPlanOrPatPlan(PLAN_NUM,0).First(x => x.BenefitNum==benefit.BenefitNum).ToothRange,
				"UpdateList saves a change of teeth alone.");
			//Rows written by Open Dental have NULL for every tooth.
			DataCore.NonQ("UPDATE benefit SET ToothRange=NULL WHERE BenefitNum="+POut.Long(benefit.BenefitNum));
			benefitDb=Benefits.GetForPlanOrPatPlan(PLAN_NUM,0).First(x => x.BenefitNum==benefit.BenefitNum);
			Assert.AreEqual("",benefitDb.ToothRange);
			Assert.IsTrue(Benefits.IsExcluded("Z4752",new List<Benefit> { benefitDb },PLAN_NUM,0,"20",""));
			Assert.IsFalse(benefitOld.IsSimilar(benefitDb),"IsSimilar compares the teeth.");
			StringAssert.Contains(benefitOld.ToString(),"teeth 2,15");
		}

		[TestMethod]
		public void Deductible_NotAppliedToExcludedTooth() {
			Patient patient=PatientT.CreatePatient("AinsDed",lName:"Ains",fName:"Synthetic");
			InsuranceInfo insuranceInfo=InsuranceT.AddInsurance(patient,"A-INS carrier");
			InsPlan insPlan=insuranceInfo.PriInsPlan;
			ProcedureCode procedureCode=ProcedureCodeT.CreateProcCode("Z4753");
			BenefitT.CreateDeductibleGeneral(insPlan.PlanNum,BenefitCoverageLevel.Individual,50);
			Benefit benefit=new Benefit { PlanNum=insPlan.PlanNum,BenefitType=InsBenefitType.Exclusions,CodeNum=procedureCode.CodeNum,ToothRange="2",
				TimePeriod=BenefitTimePeriod.CalendarYear };
			Benefits.Insert(benefit);
			List<Benefit> listBenefits=Benefits.Refresh(insuranceInfo.ListPatPlans,insuranceInfo.ListInsSubs);
			long patPlanNum=insuranceInfo.PriPatPlan.PatPlanNum;
			double deductibleTooth2=Benefits.GetDeductibleByCode(listBenefits,insPlan.PlanNum,patPlanNum,DateTime.Today,"Z4753",new List<ClaimProcHist>(),
				new List<ClaimProcHist>(),insPlan,patient.PatNum,"2","");
			double deductibleTooth3=Benefits.GetDeductibleByCode(listBenefits,insPlan.PlanNum,patPlanNum,DateTime.Today,"Z4753",new List<ClaimProcHist>(),
				new List<ClaimProcHist>(),insPlan,patient.PatNum,"3","");
			Assert.AreEqual(0,deductibleTooth2);
			Assert.AreEqual(50,deductibleTooth3);
		}

		[TestMethod]
		public void Estimate_ExcludedOnlyOnListedTeeth() {
			Patient patient=PatientT.CreatePatient("AinsEst",lName:"Ains",fName:"Synthetic");
			InsuranceInfo insuranceInfo=InsuranceT.AddInsurance(patient,"A-INS carrier");
			InsPlan insPlan=insuranceInfo.PriInsPlan;
			ProcedureCode procedureCode=ProcedureCodeT.CreateProcCode("Z4754");
			BenefitT.CreatePercentForProc(insPlan.PlanNum,procedureCode.CodeNum,80);
			Benefits.Insert(new Benefit { PlanNum=insPlan.PlanNum,BenefitType=InsBenefitType.Exclusions,CodeNum=procedureCode.CodeNum,
				ToothRange=Tooth.ParseToothList("2,15",ToothNumberingNomenclature.Universal),TimePeriod=BenefitTimePeriod.CalendarYear });
			insuranceInfo.ListBenefits=Benefits.Refresh(insuranceInfo.ListPatPlans,insuranceInfo.ListInsSubs);
			Procedure procedureTooth2=ProcedureT.CreateProcedure(patient,"Z4754",ProcStat.TP,"2",1000);
			Procedure procedureTooth3=ProcedureT.CreateProcedure(patient,"Z4754",ProcStat.TP,"3",1000);
			Procedure procedureBridge=ProcedureT.CreateProcedure(patient,"Z4754",ProcStat.TP,"",1000,toothRange:"13,14,15");
			Procedure procedureNoTooth=ProcedureT.CreateProcedure(patient,"Z4754",ProcStat.TP,"",1000);
			List<ClaimProc> listClaimProcs=ProcedureT.ComputeEstimates(patient,insuranceInfo);
			ClaimProc claimProcTooth2=listClaimProcs.Single(x => x.ProcNum==procedureTooth2.ProcNum);
			ClaimProc claimProcTooth3=listClaimProcs.Single(x => x.ProcNum==procedureTooth3.ProcNum);
			ClaimProc claimProcBridge=listClaimProcs.Single(x => x.ProcNum==procedureBridge.ProcNum);
			ClaimProc claimProcNoTooth=listClaimProcs.Single(x => x.ProcNum==procedureNoTooth.ProcNum);
			Assert.AreEqual(0,claimProcTooth2.InsEstTotal,"Tooth 2 is excluded.");
			StringAssert.Contains(claimProcTooth2.EstimateNote,"Exclusion");
			Assert.AreEqual(800,claimProcTooth3.InsEstTotal,"Tooth 3 is estimated normally.");
			Assert.IsFalse(claimProcTooth3.EstimateNote.Contains("Exclusion"));
			Assert.AreEqual(0,claimProcBridge.InsEstTotal,"A bridge that includes tooth 15 is excluded.");
			Assert.AreEqual(800,claimProcNoTooth.InsEstTotal,"A procedure with no tooth is not excluded by a tooth-limited row.");
		}

		[TestMethod]
		public void Estimate_CodeGroupRangeExclusion() {
			Patient patient=PatientT.CreatePatient("AinsGrp",lName:"Ains",fName:"Synthetic");
			InsuranceInfo insuranceInfo=InsuranceT.AddInsurance(patient,"A-INS carrier");
			InsPlan insPlan=insuranceInfo.PriInsPlan;
			ProcedureCode procedureCodeIn=ProcedureCodeT.CreateProcCode("Z4862");
			ProcedureCode procedureCodeOut=ProcedureCodeT.CreateProcCode("Z4870");
			ProcedureCodes.RefreshCache();
			CodeGroup codeGroup=CodeGroupDbTests.NewCodeGroup("Z4860-Z4869");
			BenefitT.CreatePercentForProc(insPlan.PlanNum,procedureCodeIn.CodeNum,50);
			BenefitT.CreatePercentForProc(insPlan.PlanNum,procedureCodeOut.CodeNum,50);
			Benefits.Insert(new Benefit { PlanNum=insPlan.PlanNum,BenefitType=InsBenefitType.Exclusions,CodeGroupNum=codeGroup.CodeGroupNum,
				TimePeriod=BenefitTimePeriod.CalendarYear });
			insuranceInfo.ListBenefits=Benefits.Refresh(insuranceInfo.ListPatPlans,insuranceInfo.ListInsSubs);
			Procedure procedureIn=ProcedureT.CreateProcedure(patient,"Z4862",ProcStat.TP,"5",400);
			Procedure procedureOut=ProcedureT.CreateProcedure(patient,"Z4870",ProcStat.TP,"5",400);
			List<ClaimProc> listClaimProcs=ProcedureT.ComputeEstimates(patient,insuranceInfo);
			Assert.AreEqual(0,listClaimProcs.Single(x => x.ProcNum==procedureIn.ProcNum).InsEstTotal,"Z4862 is inside the excluded range.");
			Assert.AreEqual(200,listClaimProcs.Single(x => x.ProcNum==procedureOut.ProcNum).InsEstTotal);
		}

		[TestMethod]
		public void Estimate_AgeLimitThroughCodeGroupRange() {
			//Age limitations by code group go through the range-aware membership test in GetLimitationByCode.
			Patient patient=PatientT.CreatePatient("AinsAge",lName:"Ains",fName:"Synthetic",birthDate:DateTime.Today.AddYears(-30));
			InsuranceInfo insuranceInfo=InsuranceT.AddInsurance(patient,"A-INS carrier");
			InsPlan insPlan=insuranceInfo.PriInsPlan;
			ProcedureCode procedureCodeIn=ProcedureCodeT.CreateProcCode("Z5115");
			ProcedureCode procedureCodeOut=ProcedureCodeT.CreateProcCode("Z5120");
			ProcedureCodes.RefreshCache();
			CodeGroup codeGroup=CodeGroupDbTests.NewCodeGroup("Z5110-Z5119",showInAgeLimit:true);
			BenefitT.CreatePercentForProc(insPlan.PlanNum,procedureCodeIn.CodeNum,100);
			BenefitT.CreatePercentForProc(insPlan.PlanNum,procedureCodeOut.CodeNum,100);
			Benefits.Insert(Benefits.CreateAgeLimitationBenefit(codeGroup.CodeGroupNum,18,insPlan.PlanNum));
			insuranceInfo.ListBenefits=Benefits.Refresh(insuranceInfo.ListPatPlans,insuranceInfo.ListInsSubs);
			Procedure procedureIn=ProcedureT.CreateProcedure(patient,"Z5115",ProcStat.TP,"",100);
			Procedure procedureOut=ProcedureT.CreateProcedure(patient,"Z5120",ProcStat.TP,"",100);
			List<ClaimProc> listClaimProcs=ProcedureT.ComputeEstimates(patient,insuranceInfo);
			ClaimProc claimProcIn=listClaimProcs.Single(x => x.ProcNum==procedureIn.ProcNum);
			Assert.AreEqual(0,claimProcIn.InsEstTotal,"Z5115 is in the range of the age-limited group and the patient is 30.");
			StringAssert.Contains(claimProcIn.EstimateNote,"Age limitation");
			Assert.AreEqual(100,listClaimProcs.Single(x => x.ProcNum==procedureOut.ProcNum).InsEstTotal);
		}
	}
}
