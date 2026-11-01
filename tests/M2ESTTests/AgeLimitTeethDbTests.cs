// Age limits by tooth number (Open Dental 26.1) and exclusions by tooth (25.3/25.4) in full insurance estimates on a scratch database: a sealant-
// style code group limited to molars through a given age. Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using UnitTestsCore;

namespace M2ESTTests {
	[TestClass]
	public class AgeLimitTeethDbTests {
		private const string MOLARS="2,3,14,15,18,19,30,31";
		private static int _counter;

		[TestInitialize]
		public void TestInitialize() {
			EstDb.Begin();
		}

		private static CodeGroup NewCodeGroup(string procCodes) {
			CodeGroup codeGroup=new CodeGroup();
			codeGroup.GroupName="M2-EST teeth "+(++_counter)+" "+Guid.NewGuid().ToString("N").Substring(0,6);
			codeGroup.ProcCodes=procCodes;
			codeGroup.ItemOrder=3000+_counter;
			codeGroup.ShowInAgeLimit=true;
			CodeGroups.Insert(codeGroup);
			CodeGroups.RefreshCache();
			return codeGroup;
		}

		///<summary>A 16 year old with a plan paying 100% of the code.</summary>
		private static InsuranceInfo NewPatientWithInsurance(string procCode,out Patient patient) {
			patient=PatientT.CreatePatient("Teeth"+(++_counter),lName:"M2est",fName:"Synthetic",birthDate:DateTime.Today.AddYears(-16).AddDays(-10));
			InsuranceInfo insuranceInfo=InsuranceT.AddInsurance(patient,"M2-EST carrier");
			ProcedureCode procedureCode=ProcedureCodeT.CreateProcCode(procCode);
			BenefitT.CreatePercentForProc(insuranceInfo.PriInsPlan.PlanNum,procedureCode.CodeNum,100);
			return insuranceInfo;
		}

		private static Benefit InsertAgeLimit(long planNum,byte age,string toothRange,long codeGroupNum=0,long covCatNum=0,long codeNum=0) {
			Benefit benefit=new Benefit { PlanNum=planNum,BenefitType=InsBenefitType.Limitations,QuantityQualifier=BenefitQuantity.AgeLimit,Quantity=age,
				CodeGroupNum=codeGroupNum,CovCatNum=covCatNum,CodeNum=codeNum,ToothRange=toothRange,TimePeriod=BenefitTimePeriod.None };
			Benefits.Insert(benefit);
			return benefit;
		}

		private static double Est(List<ClaimProc> listClaimProcs,Procedure procedure) {
			return ClaimProcs.GetInsEstTotal(listClaimProcs.Single(x => x.ProcNum==procedure.ProcNum));
		}

		private static string Note(List<ClaimProc> listClaimProcs,Procedure procedure) {
			return listClaimProcs.Single(x => x.ProcNum==procedure.ProcNum).EstimateNote;
		}

		[TestMethod]
		public void CodeGroupAgeLimit_WithTeeth_AppliesOnlyToThoseTeeth() {
			InsuranceInfo insuranceInfo=NewPatientWithInsurance("Z7401",out Patient patient);
			CodeGroup codeGroup=NewCodeGroup("Z7400-Z7409");
			Benefit benefit=InsertAgeLimit(insuranceInfo.PriInsPlan.PlanNum,14,Tooth.ParseToothList("2-3,14-15,18-19,30-31",ToothNumberingNomenclature.Universal),
				codeGroupNum:codeGroup.CodeGroupNum);
			Assert.AreEqual(MOLARS,benefit.ToothRange);
			Assert.IsTrue(Benefits.IsAgeLimit(benefit),"Still a code group age limit, so it shows in the Age Limits grid.");
			insuranceInfo.ListBenefits=Benefits.Refresh(insuranceInfo.ListPatPlans,insuranceInfo.ListInsSubs);
			Procedure procMolar=ProcedureT.CreateProcedure(patient,"Z7401",ProcStat.TP,"3",50);
			Procedure procPremolar=ProcedureT.CreateProcedure(patient,"Z7401",ProcStat.TP,"4",50);
			Procedure procNoTooth=ProcedureT.CreateProcedure(patient,"Z7401",ProcStat.TP,"",50);
			List<ClaimProc> listClaimProcs=ProcedureT.ComputeEstimates(patient,insuranceInfo);
			Assert.AreEqual(0,Est(listClaimProcs,procMolar),"A molar of a 16 year old is past the age limit of 14.");
			StringAssert.Contains(Note(listClaimProcs,procMolar),"Age limitation: 14");
			Assert.AreEqual(50,Est(listClaimProcs,procPremolar),"A premolar is not affected by the row.");
			Assert.IsFalse(Note(listClaimProcs,procPremolar).Contains("Age limitation"));
			Assert.AreEqual(50,Est(listClaimProcs,procNoTooth),"A procedure with no tooth never matches a row with teeth.");
		}

		[TestMethod]
		public void CodeGroupAgeLimit_RowWithTeethGovernsItsTeeth_RowWithoutTeethTheRest() {
			InsuranceInfo insuranceInfo=NewPatientWithInsurance("Z7411",out Patient patient);
			CodeGroup codeGroup=NewCodeGroup("Z7410-Z7419");
			InsertAgeLimit(insuranceInfo.PriInsPlan.PlanNum,18,"",codeGroupNum:codeGroup.CodeGroupNum);
			InsertAgeLimit(insuranceInfo.PriInsPlan.PlanNum,14,MOLARS,codeGroupNum:codeGroup.CodeGroupNum);
			insuranceInfo.ListBenefits=Benefits.Refresh(insuranceInfo.ListPatPlans,insuranceInfo.ListInsSubs);
			Procedure procMolar=ProcedureT.CreateProcedure(patient,"Z7411",ProcStat.TP,"30",50);
			Procedure procPremolar=ProcedureT.CreateProcedure(patient,"Z7411",ProcStat.TP,"29",50);
			List<ClaimProc> listClaimProcs=ProcedureT.ComputeEstimates(patient,insuranceInfo);
			Assert.AreEqual(0,Est(listClaimProcs,procMolar),"Molars: through 14, and the patient is 16.");
			Assert.AreEqual(50,Est(listClaimProcs,procPremolar),"Other teeth: through 18.");
			//Older than both: both denied.
			Patient patientOld=PatientT.CreatePatient("TeethOld"+(++_counter),lName:"M2est",fName:"Synthetic",birthDate:DateTime.Today.AddYears(-20));
			InsuranceInfo insuranceInfoOld=InsuranceT.AddInsurance(patientOld,"M2-EST carrier");
			BenefitT.CreatePercentForProc(insuranceInfoOld.PriInsPlan.PlanNum,ProcedureCodes.GetCodeNum("Z7411"),100);
			InsertAgeLimit(insuranceInfoOld.PriInsPlan.PlanNum,18,"",codeGroupNum:codeGroup.CodeGroupNum);
			InsertAgeLimit(insuranceInfoOld.PriInsPlan.PlanNum,14,MOLARS,codeGroupNum:codeGroup.CodeGroupNum);
			insuranceInfoOld.ListBenefits=Benefits.Refresh(insuranceInfoOld.ListPatPlans,insuranceInfoOld.ListInsSubs);
			Procedure procOldPremolar=ProcedureT.CreateProcedure(patientOld,"Z7411",ProcStat.TP,"29",50);
			listClaimProcs=ProcedureT.ComputeEstimates(patientOld,insuranceInfoOld);
			Assert.AreEqual(0,Est(listClaimProcs,procOldPremolar),"20 is past 18.");
		}

		[TestMethod]
		public void CategoryAndCodeAgeLimits_WithTeeth() {
			InsuranceInfo insuranceInfo=NewPatientWithInsurance("Z7421",out Patient patient);
			CovCat covCat=CovCatT.CreateCovCat("M2-EST teeth "+Guid.NewGuid().ToString("N").Substring(0,6));
			CovSpanT.CreateCovSpan(covCat.CovCatNum,"Z7420","Z7429");
			InsertAgeLimit(insuranceInfo.PriInsPlan.PlanNum,14,"8,9",covCatNum:covCat.CovCatNum);
			ProcedureCode procedureCodeOther=ProcedureCodeT.CreateProcCode("Z7431");
			BenefitT.CreatePercentForProc(insuranceInfo.PriInsPlan.PlanNum,procedureCodeOther.CodeNum,100);
			InsertAgeLimit(insuranceInfo.PriInsPlan.PlanNum,14,"A,B",codeNum:procedureCodeOther.CodeNum);
			insuranceInfo.ListBenefits=Benefits.Refresh(insuranceInfo.ListPatPlans,insuranceInfo.ListInsSubs);
			Procedure procCatIn=ProcedureT.CreateProcedure(patient,"Z7421",ProcStat.TP,"9",50);
			Procedure procCatOut=ProcedureT.CreateProcedure(patient,"Z7421",ProcStat.TP,"10",50);
			Procedure procCodeIn=ProcedureT.CreateProcedure(patient,"Z7431",ProcStat.TP,"B",50);
			Procedure procCodeOut=ProcedureT.CreateProcedure(patient,"Z7431",ProcStat.TP,"C",50);
			List<ClaimProc> listClaimProcs=ProcedureT.ComputeEstimates(patient,insuranceInfo);
			Assert.AreEqual(0,Est(listClaimProcs,procCatIn));
			Assert.AreEqual(50,Est(listClaimProcs,procCatOut));
			Assert.AreEqual(0,Est(listClaimProcs,procCodeIn),"Primary teeth too.");
			Assert.AreEqual(50,Est(listClaimProcs,procCodeOut));
		}

		[TestMethod]
		public void ToothRangeProcedure_MatchesWhenAnyToothIsListed() {
			InsuranceInfo insuranceInfo=NewPatientWithInsurance("Z7441",out Patient patient);
			CodeGroup codeGroup=NewCodeGroup("Z7440-Z7449");
			InsertAgeLimit(insuranceInfo.PriInsPlan.PlanNum,14,"14",codeGroupNum:codeGroup.CodeGroupNum);
			insuranceInfo.ListBenefits=Benefits.Refresh(insuranceInfo.ListPatPlans,insuranceInfo.ListInsSubs);
			Procedure procRangeIn=ProcedureT.CreateProcedure(patient,"Z7441",ProcStat.TP,"",50,toothRange:"12,13,14");
			Procedure procRangeOut=ProcedureT.CreateProcedure(patient,"Z7441",ProcStat.TP,"",50,toothRange:"11,12,13");
			List<ClaimProc> listClaimProcs=ProcedureT.ComputeEstimates(patient,insuranceInfo);
			Assert.AreEqual(0,Est(listClaimProcs,procRangeIn));
			Assert.AreEqual(50,Est(listClaimProcs,procRangeOut));
		}

		[TestMethod]
		public void ExclusionWithTeeth_NeverConsidersAge() {
			InsuranceInfo insuranceInfo=NewPatientWithInsurance("Z7451",out Patient patient);
			ProcedureCode procedureCode=ProcedureCodes.GetProcCode(ProcedureCodes.GetCodeNum("Z7451"));
			//An exclusion with a quantity that would read as an age if it were an age limit.
			Benefits.Insert(new Benefit { PlanNum=insuranceInfo.PriInsPlan.PlanNum,BenefitType=InsBenefitType.Exclusions,CodeNum=procedureCode.CodeNum,
				ToothRange="3",Quantity=30,QuantityQualifier=BenefitQuantity.AgeLimit,TimePeriod=BenefitTimePeriod.CalendarYear });
			insuranceInfo.ListBenefits=Benefits.Refresh(insuranceInfo.ListPatPlans,insuranceInfo.ListInsSubs);
			Procedure procTooth3=ProcedureT.CreateProcedure(patient,"Z7451",ProcStat.TP,"3",50);
			Procedure procTooth4=ProcedureT.CreateProcedure(patient,"Z7451",ProcStat.TP,"4",50);
			List<ClaimProc> listClaimProcs=ProcedureT.ComputeEstimates(patient,insuranceInfo);
			Assert.AreEqual(0,Est(listClaimProcs,procTooth3),"Excluded on tooth 3 although the patient is under 30.");
			StringAssert.Contains(Note(listClaimProcs,procTooth3),"Exclusion");
			Assert.IsFalse(Note(listClaimProcs,procTooth3).Contains("Age limitation"));
			Assert.AreEqual(50,Est(listClaimProcs,procTooth4));
		}

		[TestMethod]
		public void AgeLimitWithTeeth_RoundTripsAndIsLogged() {
			InsuranceInfo insuranceInfo=NewPatientWithInsurance("Z7461",out Patient patient);
			CodeGroup codeGroup=NewCodeGroup("Z7460-Z7469");
			Benefit benefit=InsertAgeLimit(insuranceInfo.PriInsPlan.PlanNum,14,MOLARS,codeGroupNum:codeGroup.CodeGroupNum);
			Benefit benefitDb=Benefits.GetForPlanOrPatPlan(insuranceInfo.PriInsPlan.PlanNum,0).Single(x => x.BenefitNum==benefit.BenefitNum);
			Assert.AreEqual(MOLARS,benefitDb.ToothRange);
			Assert.AreEqual(MOLARS,Benefits.GetToothRangeDisplay(benefitDb),"Pairs are listed; only runs of three or more show as a range.");
			StringAssert.Contains(benefitDb.ToString(),"teeth "+MOLARS);
			benefitDb.ToothRange=Tooth.ParseToothList("18-21");
			Assert.AreEqual("18-21",Benefits.GetToothRangeDisplay(benefitDb));
			benefitDb.ToothRange=MOLARS;
			Benefit benefitOld=benefitDb.Copy();
			benefitDb.ToothRange="2,3";
			Benefits.UpdateList(new List<Benefit> { benefitOld },new List<Benefit> { benefitDb });
			Assert.AreEqual("2,3",Benefits.GetForPlanOrPatPlan(insuranceInfo.PriInsPlan.PlanNum,0).Single(x => x.BenefitNum==benefit.BenefitNum).ToothRange,
				"A change of the teeth alone is saved.");
			string logCount=DataCore.GetScalar("SELECT COUNT(*) FROM inseditlog WHERE LogType="+(int)InsEditLogType.Benefit
				+" AND FKey="+POut.Long(benefit.BenefitNum)+" AND FieldName='ToothRange'");
			Assert.AreEqual("1",logCount,"The insurance edit log records the teeth.");
		}
	}
}
