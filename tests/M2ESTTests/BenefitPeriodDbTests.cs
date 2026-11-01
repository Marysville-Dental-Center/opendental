// Annual max, family max and deductible with loop entries from another benefit period (Open Dental 25.4), straight through
// Benefits.GetLimitationByCode and Benefits.GetDeductibleByCode, on a scratch database (the insurance categories come from it). Loop entries that
// are not marked as Treatment Plan entries (claims, other callers) keep counting whatever their date. Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2ESTTests {
	[TestClass]
	public class BenefitPeriodDbTests {
		private const long PLAN_NUM=880001;
		private const long PAT_PLAN_NUM=880002;
		private const long INS_SUB_NUM=880003;
		private const long PAT_NUM=880004;
		private const string CODE="Z7900";
		private static readonly DateTime _dateThisYear=new DateTime(2026,5,10);
		private static readonly DateTime _dateNextYear=new DateTime(2027,2,10);

		[TestInitialize]
		public void TestInitialize() {
			EstDb.Begin();
		}

		private static InsPlan Plan(byte monthRenew=0) {
			return new InsPlan { PlanNum=PLAN_NUM,MonthRenew=monthRenew };
		}

		private static Benefit Max(double amount,BenefitCoverageLevel coverageLevel=BenefitCoverageLevel.Individual,
			BenefitTimePeriod timePeriod=BenefitTimePeriod.CalendarYear)
		{
			return new Benefit { BenefitNum=coverageLevel==BenefitCoverageLevel.Family ? 2 : 1,PlanNum=PLAN_NUM,BenefitType=InsBenefitType.Limitations,
				CoverageLevel=coverageLevel,MonetaryAmt=amount,TimePeriod=timePeriod };
		}

		private static ClaimProcHist Loop(DateTime date,double amount,double deduct=0,bool isDateAsOf=true,long patNum=PAT_NUM) {
			return new ClaimProcHist { ProcDate=date,Amount=amount,Deduct=deduct,PlanNum=PLAN_NUM,InsSubNum=INS_SUB_NUM,PatNum=patNum,StrProcCode=CODE,
				Status=ClaimProcStatus.Estimate,IsDateAsOf=isDateAsOf };
		}

		private static double Limit(List<Benefit> listBenefits,DateTime dateProc,List<ClaimProcHist> listLoop,double insEst,InsPlan insPlan,
			out string note)
		{
			return Benefits.GetLimitationByCode(listBenefits,PLAN_NUM,PAT_PLAN_NUM,dateProc,CODE,new List<ClaimProcHist>(),listLoop,insPlan,PAT_NUM,out note,
				insEst,40,INS_SUB_NUM,-1,out LimitationTypeMet _);
		}

		[TestMethod]
		public void AnnualMax_LoopOfAnotherYear_DoesNotCount() {
			List<Benefit> listBenefits=new List<Benefit> { Max(1000) };
			List<ClaimProcHist> listLoop=new List<ClaimProcHist> { Loop(_dateThisYear,800) };
			Assert.AreEqual(200,Limit(listBenefits,_dateThisYear,listLoop,800,Plan(),out string note));
			StringAssert.Contains(note,"Over annual max");
			Assert.AreEqual(800,Limit(listBenefits,_dateNextYear,listLoop,800,Plan(),out note),"Next year's max is untouched.");
			Assert.AreEqual("",note);
			listLoop.Add(Loop(_dateNextYear,700));
			Assert.AreEqual(200,Limit(listBenefits,_dateThisYear,listLoop,800,Plan(),out _),"An earlier entry for next year does not count either.");
			Assert.AreEqual(300,Limit(listBenefits,_dateNextYear,listLoop,800,Plan(),out _));
		}

		[TestMethod]
		public void AnnualMax_UnmarkedLoopEntries_CountWhateverTheirDate() {
			//Claims and the other callers build loop lists from GetHistForProc, dated today: they keep the old behavior.
			List<Benefit> listBenefits=new List<Benefit> { Max(1000) };
			List<ClaimProcHist> listLoop=new List<ClaimProcHist> { Loop(_dateThisYear,800,isDateAsOf:false) };
			Assert.AreEqual(200,Limit(listBenefits,_dateNextYear,listLoop,800,Plan(),out _));
		}

		[TestMethod]
		public void AnnualMax_ServiceYear_RenewalMonthSplitsThePeriods() {
			List<Benefit> listBenefits=new List<Benefit> { Max(1000,timePeriod:BenefitTimePeriod.ServiceYear) };
			InsPlan insPlan=Plan(monthRenew:7);
			List<ClaimProcHist> listLoop=new List<ClaimProcHist> { Loop(new DateTime(2027,6,30),800) };
			Assert.AreEqual(200,Limit(listBenefits,new DateTime(2026,7,1),listLoop,800,insPlan,out _),"Same benefit year, July to June.");
			Assert.AreEqual(800,Limit(listBenefits,new DateTime(2027,7,1),listLoop,800,insPlan,out _),"The renewal day starts a new year.");
			Assert.AreEqual(800,Limit(listBenefits,new DateTime(2026,6,30),listLoop,800,insPlan,out _),"The year before.");
		}

		[TestMethod]
		public void LifetimeMax_CountsEveryLoopEntry() {
			List<Benefit> listBenefits=new List<Benefit> { Max(1000,timePeriod:BenefitTimePeriod.Lifetime) };
			List<ClaimProcHist> listLoop=new List<ClaimProcHist> { Loop(_dateThisYear,800) };
			Assert.AreEqual(200,Limit(listBenefits,_dateNextYear,listLoop,800,Plan(),out string note));
			StringAssert.Contains(note,"Over lifetime max");
		}

		[TestMethod]
		public void FamilyMax_LoopOfAnotherYear_DoesNotCount() {
			List<Benefit> listBenefits=new List<Benefit> { Max(1000),Max(1500,BenefitCoverageLevel.Family) };
			//A family member's procedure this year, and the patient's own.
			List<ClaimProcHist> listLoop=new List<ClaimProcHist> { Loop(_dateThisYear,900,patNum:PAT_NUM+1),Loop(_dateThisYear,400) };
			Assert.AreEqual(200,Limit(listBenefits,_dateThisYear,listLoop,800,Plan(),out string note),"1500-900-400 of the family max.");
			StringAssert.Contains(note,"Over family annual max");
			Assert.AreEqual(800,Limit(listBenefits,_dateNextYear,listLoop,800,Plan(),out _));
		}

		[TestMethod]
		public void Deductible_LoopOfAnotherYear_DoesNotCount() {
			Benefit benefitDeductible=new Benefit { BenefitNum=3,PlanNum=PLAN_NUM,BenefitType=InsBenefitType.Deductible,CoverageLevel=BenefitCoverageLevel.Individual,
				MonetaryAmt=50,TimePeriod=BenefitTimePeriod.CalendarYear };
			Benefit benefitDeductibleFam=new Benefit { BenefitNum=4,PlanNum=PLAN_NUM,BenefitType=InsBenefitType.Deductible,
				CoverageLevel=BenefitCoverageLevel.Family,MonetaryAmt=100,TimePeriod=BenefitTimePeriod.CalendarYear };
			List<Benefit> listBenefits=new List<Benefit> { benefitDeductible,benefitDeductibleFam };
			List<ClaimProcHist> listLoop=new List<ClaimProcHist> { Loop(_dateThisYear,50,deduct:50) };
			Assert.AreEqual(0,Benefits.GetDeductibleByCode(listBenefits,PLAN_NUM,PAT_PLAN_NUM,_dateThisYear,CODE,new List<ClaimProcHist>(),listLoop,Plan(),PAT_NUM));
			Assert.AreEqual(50,Benefits.GetDeductibleByCode(listBenefits,PLAN_NUM,PAT_PLAN_NUM,_dateNextYear,CODE,new List<ClaimProcHist>(),listLoop,Plan(),PAT_NUM),
				"Next year's deductible is due again.");
			//The family deductible: two family members this year use it up; next year it is fresh.
			List<ClaimProcHist> listLoopFamily=new List<ClaimProcHist> { Loop(_dateThisYear,50,deduct:50,patNum:PAT_NUM+1),
				Loop(_dateThisYear,50,deduct:50,patNum:PAT_NUM+2) };
			Assert.AreEqual(0,Benefits.GetDeductibleByCode(listBenefits,PLAN_NUM,PAT_PLAN_NUM,_dateThisYear,CODE,new List<ClaimProcHist>(),listLoopFamily,Plan(),
				PAT_NUM));
			Assert.AreEqual(50,Benefits.GetDeductibleByCode(listBenefits,PLAN_NUM,PAT_PLAN_NUM,_dateNextYear,CODE,new List<ClaimProcHist>(),listLoopFamily,Plan(),
				PAT_NUM));
			//Unmarked entries keep the old behavior.
			listLoop[0].IsDateAsOf=false;
			Assert.AreEqual(0,Benefits.GetDeductibleByCode(listBenefits,PLAN_NUM,PAT_PLAN_NUM,_dateNextYear,CODE,new List<ClaimProcHist>(),listLoop,Plan(),PAT_NUM));
		}
	}
}
