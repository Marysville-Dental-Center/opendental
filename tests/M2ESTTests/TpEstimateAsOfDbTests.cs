// Treatment Plan module estimates as of the scheduled appointment date (Open Dental 25.4) on a scratch database, through the same calls the
// Treatment Plan module makes (TreatmentPlanModules.GetModuleData, GetLoadActiveTpData, GetActiveTpPlanTpRows), with frequency checking on (the TP
// date picker) and off. Benefit-year boundaries: calendar and service years, annual max, deductible, lifetime max, history, frequency, age and
// coverage end. Synthetic data only; dates are relative to today so the tests pass on any day.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using UnitTestsCore;

namespace M2ESTTests {
	[TestClass]
	public class TpEstimateAsOfDbTests {
		private static int _counter;

		[TestInitialize]
		public void TestInitialize() {
			EstDb.Begin();
		}

		[TestCleanup]
		public void TestCleanup() {
			if(EstDb.SkipReason==null) {
				Prefs.UpdateBool(PrefName.InsChecksFrequency,true);
			}
		}

		#region Helpers
		///<summary>What the Treatment Plan module shows for the active plan.</summary>
		private class TpResult {
			public List<TpRow> ListTpRows;
			public List<ClaimProc> ListClaimProcs;
			public LoadActiveTPData LoadActiveTPData;
		}

		///<summary>Loads the active treatment plan the way ControlTreat.LoadTP does, as of dateTimeTP (the date picker, used when frequency checking is
		///on).</summary>
		private static TpResult LoadActiveTp(Patient patient,DateTime dateTimeTP,bool isFrequency) {
			Prefs.UpdateBool(PrefName.InsChecksFrequency,isFrequency);
			TPModuleData tpModuleData=TreatmentPlanModules.GetModuleData(patient.PatNum,false);
			TreatPlan treatPlan=tpModuleData.ListTreatPlans.First(x => x.TPStatus==TreatPlanStatus.Active);
			LoadActiveTPData loadActiveTPData=TreatmentPlanModules.GetLoadActiveTpData(tpModuleData.Pat,treatPlan.TreatPlanNum,tpModuleData.BenefitList,
				tpModuleData.PatPlanList,tpModuleData.InsPlanList,dateTimeTP,tpModuleData.SubList,isFrequency,false,tpModuleData.ListSubstLinks);
			List<ClaimProcHist> listClaimProcHists=isFrequency ? loadActiveTPData.HistList : tpModuleData.HistList;
			List<ClaimProc> listClaimProcs=loadActiveTPData.ClaimProcList;
			List<TpRow> listTpRows=TreatmentPlanModules.GetActiveTpPlanTpRows(true,false,false,false,treatPlan,tpModuleData.Pat,dateTimeTP,loadActiveTPData,
				tpModuleData.InsPlanList,tpModuleData.BenefitList,tpModuleData.PatPlanList,tpModuleData.ListSubstLinks,tpModuleData.SubList,
				tpModuleData.DiscountPlanSub,tpModuleData.DiscountPlan,tpModuleData.ListProcedures,ref listClaimProcs,listClaimProcHists);
			return new TpResult { ListTpRows=listTpRows,ListClaimProcs=listClaimProcs,LoadActiveTPData=loadActiveTPData };
		}

		private static ClaimProc Estimate(TpResult tpResult,Procedure procedure,InsuranceInfo insuranceInfo) {
			ClaimProc claimProc=ClaimProcs.GetEstimate(tpResult.ListClaimProcs,procedure.ProcNum,insuranceInfo.PriInsPlan.PlanNum,insuranceInfo.PriInsSub.InsSubNum);
			Assert.IsNotNull(claimProc,"No estimate for "+procedure.ProcNum);
			return claimProc;
		}

		private static double InsEst(TpResult tpResult,Procedure procedure,InsuranceInfo insuranceInfo) {
			return ClaimProcs.GetInsEstTotal(Estimate(tpResult,procedure,insuranceInfo));
		}

		private static Patient NewPatient(DateTime birthDate=default(DateTime)) {
			_counter++;
			if(birthDate.Year<1880) {
				birthDate=DateTime.Today.AddYears(-40);
			}
			return PatientT.CreatePatient("Est"+_counter,lName:"M2est",fName:"Synthetic",birthDate:birthDate);
		}

		///<summary>A category percentage plan paying 100% of the code.</summary>
		private static InsuranceInfo NewInsurance(Patient patient,string procCode,int monthRenew=0) {
			InsuranceInfo insuranceInfo=InsuranceT.AddInsurance(patient,"M2-EST carrier",monthRenew:monthRenew);
			ProcedureCode procedureCode=ProcedureCodeT.CreateProcCode(procCode);
			BenefitT.CreatePercentForProc(insuranceInfo.PriInsPlan.PlanNum,procedureCode.CodeNum,100);
			return insuranceInfo;
		}

		private static Appointment NewAppointment(Patient patient,DateTime date,ApptStatus apptStatus=ApptStatus.Scheduled) {
			return AppointmentT.CreateAppointment(patient.PatNum,date.Date.AddHours(9),0,patient.PriProv,aptStatus:apptStatus);
		}

		///<summary>A treatment-planned procedure. priority is the index of the TP priority (its order in the plan). With an appointment, its date is the
		///appointment's, as the program sets it when procedures are scheduled.</summary>
		private static Procedure NewTpProc(Patient patient,string procCode,double fee,int priority,Appointment appointment=null,string toothNum="",
			long plannedAptNum=0)
		{
			DateTime procDate=(appointment==null ? DateTime.Today : appointment.AptDateTime.Date);
			return ProcedureT.CreateProcedure(patient,procCode,ProcStat.TP,toothNum,fee,procDate:procDate,priority:priority,
				aptNum:appointment?.AptNum??0,plannedAptNum:plannedAptNum);
		}

		private static DateTime DateNextYear {
			get {
				return new DateTime(DateTime.Today.Year+1,3,15);
			}
		}
		#endregion Helpers

		#region Annual max
		private static void AssertAnnualMaxPerYear(bool isFrequency) {
			Patient patient=NewPatient();
			InsuranceInfo insuranceInfo=NewInsurance(patient,"Z7010");
			BenefitT.CreateAnnualMax(insuranceInfo.PriInsPlan.PlanNum,1000);
			Procedure procA=NewTpProc(patient,"Z7010",800,0);
			Procedure procB=NewTpProc(patient,"Z7010",800,1,NewAppointment(patient,DateNextYear));
			Procedure procC=NewTpProc(patient,"Z7010",800,2);
			TpResult tpResult=LoadActiveTp(patient,DateTime.Today,isFrequency);
			Assert.AreEqual(800,InsEst(tpResult,procA,insuranceInfo));
			Assert.AreEqual(800,InsEst(tpResult,procB,insuranceInfo),"Scheduled next year: next year's max (before 25.4: 200).");
			Assert.AreEqual(200,InsEst(tpResult,procC,insuranceInfo),"Later in the plan, this year: the rest of this year's max.");
			StringAssert.Contains(Estimate(tpResult,procC,insuranceInfo).EstimateNote,"Over annual max");
			Assert.IsFalse(Estimate(tpResult,procB,insuranceInfo).EstimateNote.Contains("annual max"));
			string descriptB=tpResult.ListTpRows.Where(x => x.RowType==TpRowType.TpRow).ElementAt(1).Description;
			StringAssert.Contains(descriptB,"Estimated as of "+DateNextYear.ToShortDateString());
			Assert.IsFalse(tpResult.ListTpRows.Where(x => x.RowType==TpRowType.TpRow).ElementAt(0).Description.Contains("Estimated as of"));
			Assert.AreEqual(800,(double)tpResult.ListTpRows.Where(x => x.RowType==TpRowType.TpRow).ElementAt(1).PriIns);
			//The active plan's estimates are saved when the plan is shown as of today.
			ClaimProc claimProcDbB=ClaimProcs.RefreshForProc(procB.ProcNum).Single(x => x.Status==ClaimProcStatus.Estimate);
			Assert.AreEqual(800,ClaimProcs.GetInsEstTotal(claimProcDbB));
			Assert.AreEqual(DateNextYear,claimProcDbB.ProcDate.Date);
		}

		[TestMethod]
		public void AnnualMax_ScheduledNextYear_FrequencyCheckingOn() {
			AssertAnnualMaxPerYear(isFrequency:true);
		}

		[TestMethod]
		public void AnnualMax_ScheduledNextYear_FrequencyCheckingOff() {
			AssertAnnualMaxPerYear(isFrequency:false);
		}

		[TestMethod]
		public void AnnualMax_HistoryOfThisYearDoesNotReduceNextYear() {
			Patient patient=NewPatient();
			InsuranceInfo insuranceInfo=NewInsurance(patient,"Z7011");
			BenefitT.CreateAnnualMax(insuranceInfo.PriInsPlan.PlanNum,1000);
			Procedure procDone=ProcedureT.CreateProcedure(patient,"Z7011",ProcStat.C,"",600,procDate:DateTime.Today);
			ClaimProcT.AddInsPaid(patient.PatNum,insuranceInfo.PriInsPlan.PlanNum,procDone.ProcNum,600,insuranceInfo.PriInsSub.InsSubNum,0,0,DateTime.Today);
			Procedure procA=NewTpProc(patient,"Z7011",800,0);
			Procedure procB=NewTpProc(patient,"Z7011",800,1,NewAppointment(patient,DateNextYear));
			foreach(bool isFrequency in new[] { true,false }) {
				TpResult tpResult=LoadActiveTp(patient,DateTime.Today,isFrequency);
				Assert.AreEqual(400,InsEst(tpResult,procA,insuranceInfo),"600 of 1000 paid this year. Frequency checking "+isFrequency);
				Assert.AreEqual(800,InsEst(tpResult,procB,insuranceInfo),"Next year starts from the full max. Frequency checking "+isFrequency);
			}
		}

		[TestMethod]
		public void AnnualMax_ServiceYearRenewsBetweenToday_AndTheAppointment() {
			DateTime dateNextMonthFirst=new DateTime(DateTime.Today.Year,DateTime.Today.Month,1).AddMonths(1);
			Patient patient=NewPatient();
			InsuranceInfo insuranceInfo=NewInsurance(patient,"Z7012",monthRenew:dateNextMonthFirst.Month);//The benefit year starts on the 1st next month.
			Benefits.Insert(new Benefit { PlanNum=insuranceInfo.PriInsPlan.PlanNum,BenefitType=InsBenefitType.Limitations,
				CoverageLevel=BenefitCoverageLevel.Individual,MonetaryAmt=1000,TimePeriod=BenefitTimePeriod.ServiceYear });
			Procedure procA=NewTpProc(patient,"Z7012",800,0);
			Procedure procB=NewTpProc(patient,"Z7012",800,1,NewAppointment(patient,dateNextMonthFirst.AddDays(1)));
			Procedure procC=NewTpProc(patient,"Z7012",100,2,NewAppointment(patient,dateNextMonthFirst.AddDays(-1)));//last day before renewal
			Procedure procD=NewTpProc(patient,"Z7012",800,3);
			foreach(bool isFrequency in new[] { true,false }) {
				TpResult tpResult=LoadActiveTp(patient,DateTime.Today,isFrequency);
				Assert.AreEqual(800,InsEst(tpResult,procA,insuranceInfo));
				Assert.AreEqual(800,InsEst(tpResult,procB,insuranceInfo),"After the renewal month starts: the new benefit year.");
				Assert.AreEqual(100,InsEst(tpResult,procC,insuranceInfo),"The day before renewal: still this benefit year.");
				Assert.AreEqual(100,InsEst(tpResult,procD,insuranceInfo),"1000-800-100.");
			}
		}

		[TestMethod]
		public void AnnualMax_PastAppointmentAndPlannedAppointment_UseTheTpDate() {
			Patient patient=NewPatient();
			InsuranceInfo insuranceInfo=NewInsurance(patient,"Z7013");
			BenefitT.CreateAnnualMax(insuranceInfo.PriInsPlan.PlanNum,1000);
			Appointment appointmentPlanned=NewAppointment(patient,DateNextYear,ApptStatus.Planned);
			Procedure procA=NewTpProc(patient,"Z7013",800,0);
			Procedure procB=NewTpProc(patient,"Z7013",800,1,plannedAptNum:appointmentPlanned.AptNum);
			Procedure procC=NewTpProc(patient,"Z7013",100,2,NewAppointment(patient,DateNextYear,ApptStatus.UnschedList));
			foreach(bool isFrequency in new[] { true,false }) {
				TpResult tpResult=LoadActiveTp(patient,DateTime.Today,isFrequency);
				Assert.AreEqual(800,InsEst(tpResult,procA,insuranceInfo));
				Assert.AreEqual(200,InsEst(tpResult,procB,insuranceInfo),"A planned appointment does not move the estimate date.");
				Assert.AreEqual(0,InsEst(tpResult,procC,insuranceInfo),"The unscheduled list does not either.");
			}
		}

		[TestMethod]
		public void PastScheduledAppointment_IsEstimatedAsOfToday() {
			Patient patient=NewPatient();
			InsuranceInfo insuranceInfo=NewInsurance(patient,"Z7014");
			Procedure procPast=NewTpProc(patient,"Z7014",100,0,NewAppointment(patient,DateTime.Today.AddDays(-3)));
			TpResult tpResult=LoadActiveTp(patient,DateTime.Today,isFrequency:true);
			Assert.AreEqual(DateTime.Today,tpResult.LoadActiveTPData.listProcForTP.Single(x => x.ProcNum==procPast.ProcNum).ProcDate,
				"With the TP date picker, the procedure is dated as of its estimate (today for a past appointment).");
			tpResult=LoadActiveTp(patient,DateTime.Today,isFrequency:false);
			Assert.AreEqual(DateTime.Today.AddDays(-3),tpResult.LoadActiveTPData.listProcForTP.Single(x => x.ProcNum==procPast.ProcNum).ProcDate,
				"Without it, procedure dates are never changed.");
			Assert.AreEqual(DateTime.Today.AddDays(-3),Estimate(tpResult,procPast,insuranceInfo).ProcDate.Date,
				"The unsent estimate keeps the procedure's date, as before.");
		}

		[TestMethod]
		public void FrequencyCheckingOff_DatesAreNotChangedButEstimatesAreAsOfTheAppointment() {
			Patient patient=NewPatient();
			InsuranceInfo insuranceInfo=NewInsurance(patient,"Z7015");
			BenefitT.CreateAnnualMax(insuranceInfo.PriInsPlan.PlanNum,1000);
			Procedure procA=NewTpProc(patient,"Z7015",800,0);
			Appointment appointment=NewAppointment(patient,DateNextYear);
			//A procedure whose date was not moved with its appointment: the estimate still follows the appointment.
			Procedure procB=ProcedureT.CreateProcedure(patient,"Z7015",ProcStat.TP,"",800,procDate:DateTime.Today,priority:1,aptNum:appointment.AptNum);
			TpResult tpResult=LoadActiveTp(patient,DateTime.Today,isFrequency:false);
			Assert.AreEqual(800,InsEst(tpResult,procB,insuranceInfo));
			Assert.AreEqual(DateTime.Today,tpResult.LoadActiveTPData.listProcForTP.Single(x => x.ProcNum==procB.ProcNum).ProcDate);
			Assert.AreEqual(DateTime.Today,Procedures.GetOneProc(procB.ProcNum,false).ProcDate);
			Assert.AreEqual(DateTime.Today,ClaimProcs.RefreshForProc(procB.ProcNum).Single().ProcDate.Date);
		}

		[TestMethod]
		public void TpDateInTheFuture_ScheduledProcedureBeforeIt_IsEstimatedInItsOwnYear() {
			//The date picker set into next year; a procedure scheduled this year still uses this year's history and max.
			if(DateTime.Today.Month==12 && DateTime.Today.Day>=28) {
				Assert.Inconclusive("Needs a few days left in the year.");
			}
			Patient patient=NewPatient();
			InsuranceInfo insuranceInfo=NewInsurance(patient,"Z7016");
			BenefitT.CreateAnnualMax(insuranceInfo.PriInsPlan.PlanNum,1000);
			Procedure procDone=ProcedureT.CreateProcedure(patient,"Z7016",ProcStat.C,"",700,procDate:DateTime.Today);
			ClaimProcT.AddInsPaid(patient.PatNum,insuranceInfo.PriInsPlan.PlanNum,procDone.ProcNum,700,insuranceInfo.PriInsSub.InsSubNum,0,0,DateTime.Today);
			Procedure procThisYear=NewTpProc(patient,"Z7016",800,0,NewAppointment(patient,DateTime.Today.AddDays(1)));
			Procedure procUnscheduled=NewTpProc(patient,"Z7016",800,1);
			TpResult tpResult=LoadActiveTp(patient,DateNextYear,isFrequency:true);
			Assert.AreEqual(300,InsEst(tpResult,procThisYear,insuranceInfo),"The history reaches back to this year for it.");
			Assert.AreEqual(800,InsEst(tpResult,procUnscheduled,insuranceInfo),"As of the TP date, next year.");
		}
		#endregion Annual max

		#region Deductible and lifetime max
		[TestMethod]
		public void Deductible_ScheduledNextYear_AppliesNextYearsDeductible() {
			Patient patient=NewPatient();
			InsuranceInfo insuranceInfo=NewInsurance(patient,"Z7020");
			BenefitT.CreateDeductibleGeneral(insuranceInfo.PriInsPlan.PlanNum,BenefitCoverageLevel.Individual,50);
			Procedure procA=NewTpProc(patient,"Z7020",100,0);
			Procedure procB=NewTpProc(patient,"Z7020",100,1,NewAppointment(patient,DateNextYear));
			Procedure procC=NewTpProc(patient,"Z7020",100,2);
			foreach(bool isFrequency in new[] { true,false }) {
				TpResult tpResult=LoadActiveTp(patient,DateTime.Today,isFrequency);
				Assert.AreEqual(50,Estimate(tpResult,procA,insuranceInfo).DedEst);
				Assert.AreEqual(50,InsEst(tpResult,procA,insuranceInfo));
				Assert.AreEqual(50,Estimate(tpResult,procB,insuranceInfo).DedEst,"Next year's deductible (before 25.4: 0).");
				Assert.AreEqual(50,InsEst(tpResult,procB,insuranceInfo));
				Assert.AreEqual(0,Estimate(tpResult,procC,insuranceInfo).DedEst,"This year's deductible was met by the first procedure.");
				Assert.AreEqual(100,InsEst(tpResult,procC,insuranceInfo));
			}
		}

		[TestMethod]
		public void LifetimeMax_CountsTheWholePlanAcrossYears() {
			Patient patient=NewPatient();
			InsuranceInfo insuranceInfo=NewInsurance(patient,"Z7501");
			CovCat covCat=CovCatT.CreateCovCat("M2-EST lifetime "+Guid.NewGuid().ToString("N").Substring(0,6));
			CovSpanT.CreateCovSpan(covCat.CovCatNum,"Z7500","Z7599");
			Benefits.Insert(new Benefit { PlanNum=insuranceInfo.PriInsPlan.PlanNum,BenefitType=InsBenefitType.Limitations,CovCatNum=covCat.CovCatNum,
				CoverageLevel=BenefitCoverageLevel.Individual,MonetaryAmt=1000,TimePeriod=BenefitTimePeriod.Lifetime });
			Procedure procA=NewTpProc(patient,"Z7501",800,0);
			Procedure procB=NewTpProc(patient,"Z7501",800,1,NewAppointment(patient,DateNextYear));
			foreach(bool isFrequency in new[] { true,false }) {
				TpResult tpResult=LoadActiveTp(patient,DateTime.Today,isFrequency);
				Assert.AreEqual(800,InsEst(tpResult,procA,insuranceInfo));
				Assert.AreEqual(200,InsEst(tpResult,procB,insuranceInfo),"A lifetime max is not renewed by a new benefit year.");
				StringAssert.Contains(Estimate(tpResult,procB,insuranceInfo).EstimateNote,"Over lifetime max");
			}
		}
		#endregion Deductible and lifetime max

		#region Frequency, age and coverage
		[TestMethod]
		public void Frequency_CheckedAsOfEachAppointment() {
			Patient patient=NewPatient();
			InsuranceInfo insuranceInfo=NewInsurance(patient,"Z7101");
			BenefitT.CreateFrequencyProc(insuranceInfo.PriInsPlan.PlanNum,"Z7101",BenefitQuantity.Months,6);
			Procedure procA=NewTpProc(patient,"Z7101",100,0);
			Procedure procB=NewTpProc(patient,"Z7101",100,1,NewAppointment(patient,DateTime.Today.AddMonths(7)));
			Procedure procC=NewTpProc(patient,"Z7101",100,2,NewAppointment(patient,DateTime.Today.AddMonths(3)));
			TpResult tpResult=LoadActiveTp(patient,DateTime.Today,isFrequency:true);
			Assert.AreEqual(100,InsEst(tpResult,procA,insuranceInfo));
			Assert.AreEqual(100,InsEst(tpResult,procB,insuranceInfo),"Seven months after the first one: allowed (before 25.4: limited).");
			Assert.AreEqual(0,InsEst(tpResult,procC,insuranceInfo));
			StringAssert.Contains(Estimate(tpResult,procC,insuranceInfo).EstimateNote,"Frequency Limitation");
		}

		[TestMethod]
		public void AgeLimit_UsesTheAgeOnTheAppointmentDate() {
			DateTime dateAppt=DateTime.Today.AddDays(30);
			Patient patient=NewPatient(birthDate:dateAppt.AddYears(-19));//18 today, 19 on the appointment date
			Assert.AreEqual(18,patient.Age);
			InsuranceInfo insuranceInfo=NewInsurance(patient,"Z7201");
			CodeGroup codeGroup=NewCodeGroup("Z7200-Z7209");
			Benefits.Insert(Benefits.CreateAgeLimitationBenefit(codeGroup.CodeGroupNum,18,insuranceInfo.PriInsPlan.PlanNum));
			Procedure procToday=NewTpProc(patient,"Z7201",100,0);
			Procedure procLater=NewTpProc(patient,"Z7201",100,1,NewAppointment(patient,dateAppt));
			foreach(bool isFrequency in new[] { true,false }) {
				TpResult tpResult=LoadActiveTp(patient,DateTime.Today,isFrequency);
				Assert.AreEqual(100,InsEst(tpResult,procToday,insuranceInfo),"18 today: covered through 18.");
				Assert.AreEqual(0,InsEst(tpResult,procLater,insuranceInfo),"19 on the appointment date.");
				StringAssert.Contains(Estimate(tpResult,procLater,insuranceInfo).EstimateNote,"Age limitation");
			}
		}

		[TestMethod]
		public void CoverageEndingBeforeTheAppointment_GivesNoEstimate() {
			Patient patient=NewPatient();
			InsuranceInfo insuranceInfo=NewInsurance(patient,"Z7301");
			InsSub insSub=insuranceInfo.PriInsSub;
			insSub.DateTerm=DateTime.Today.AddDays(10);
			InsSubs.Update(insSub);
			Procedure procUnscheduled=NewTpProc(patient,"Z7301",100,0);
			Procedure procBefore=NewTpProc(patient,"Z7301",100,1,NewAppointment(patient,DateTime.Today.AddDays(10)));
			Procedure procAfter=NewTpProc(patient,"Z7301",100,2,NewAppointment(patient,DateTime.Today.AddDays(20)));
			foreach(bool isFrequency in new[] { true,false }) {
				TpResult tpResult=LoadActiveTp(patient,DateTime.Today,isFrequency);
				Assert.AreEqual(100,InsEst(tpResult,procUnscheduled,insuranceInfo),"Not scheduled: as before.");
				Assert.AreEqual(100,InsEst(tpResult,procBefore,insuranceInfo),"Covered through the term date.");
				ClaimProc claimProcAfter=Estimate(tpResult,procAfter,insuranceInfo);
				Assert.AreEqual(0,ClaimProcs.GetInsEstTotal(claimProcAfter));
				Assert.AreEqual(0,claimProcAfter.BaseEst);
				StringAssert.Contains(claimProcAfter.EstimateNote,"Coverage ended "+insSub.DateTerm.ToShortDateString());
				Assert.AreEqual(0,(double)tpResult.ListTpRows.Where(x => x.RowType==TpRowType.TpRow).ElementAt(2).PriIns);
			}
		}

		[TestMethod]
		public void CoverageEnded_OutsideTheTreatmentPlanModule_IsIgnoredAsBefore() {
			Patient patient=NewPatient();
			InsuranceInfo insuranceInfo=NewInsurance(patient,"Z7302");
			InsSub insSub=insuranceInfo.PriInsSub;
			insSub.DateTerm=DateTime.Today.AddDays(-10);
			InsSubs.Update(insSub);
			insuranceInfo.ListInsSubs=InsSubs.RefreshForFam(Patients.GetFamily(patient.PatNum));
			insuranceInfo.ListBenefits=Benefits.Refresh(insuranceInfo.ListPatPlans,insuranceInfo.ListInsSubs);
			Procedure procedure=ProcedureT.CreateProcedure(patient,"Z7302",ProcStat.TP,"",100,procDate:DateTime.Today.AddDays(5));
			List<ClaimProc> listClaimProcs=ProcedureT.ComputeEstimates(patient,insuranceInfo);
			Assert.AreEqual(100,ClaimProcs.GetInsEstTotal(listClaimProcs.Single(x => x.ProcNum==procedure.ProcNum)),
				"Only the Treatment Plan module's scheduled procedures check the term date.");
		}
		#endregion Frequency, age and coverage

		private static CodeGroup NewCodeGroup(string procCodes) {
			CodeGroup codeGroup=new CodeGroup();
			codeGroup.GroupName="M2-EST group "+(++_counter)+" "+Guid.NewGuid().ToString("N").Substring(0,6);
			codeGroup.ProcCodes=procCodes;
			codeGroup.ItemOrder=2000+_counter;
			codeGroup.ShowInAgeLimit=true;
			CodeGroups.Insert(codeGroup);
			CodeGroups.RefreshCache();
			return codeGroup;
		}
	}
}
