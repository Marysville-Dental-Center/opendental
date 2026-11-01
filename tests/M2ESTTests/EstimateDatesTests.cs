// Which date a treatment-planned procedure is estimated as of (Open Dental 25.4: estimates use the scheduled appointment date). No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace M2ESTTests {
	[TestClass]
	public class EstimateDatesTests {
		private static readonly DateTime _dateToday=new DateTime(2026,10,25);
		private static readonly DateTime _dateTP=new DateTime(2026,11,2);

		[TestInitialize]
		public void TestInitialize() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
		}

		private static Appointment Appt(long aptNum,DateTime dateTime,ApptStatus apptStatus=ApptStatus.Scheduled) {
			return new Appointment { AptNum=aptNum,AptDateTime=dateTime,AptStatus=apptStatus };
		}

		private static Procedure Proc(long procNum,long aptNum=0,long plannedAptNum=0,DateTime procDate=default(DateTime)) {
			return new Procedure { ProcNum=procNum,AptNum=aptNum,PlannedAptNum=plannedAptNum,ProcDate=procDate,ProcStatus=ProcStat.TP };
		}

		[TestMethod]
		public void DateAsOf_ScheduledAppointmentInTheFuture_IsTheAppointmentDate() {
			List<Appointment> listAppts=new List<Appointment> { Appt(10,new DateTime(2027,3,15,9,30,0)) };
			Assert.AreEqual(new DateTime(2027,3,15),TpEstimateDates.GetDateAsOf(Proc(1,aptNum:10),listAppts,_dateTP,_dateToday),
				"Appointment date, without the time of day.");
		}

		[TestMethod]
		public void DateAsOf_ScheduledAppointmentInThePast_IsToday() {
			List<Appointment> listAppts=new List<Appointment> { Appt(10,new DateTime(2026,10,1,8,0,0)) };
			Assert.AreEqual(_dateToday,TpEstimateDates.GetDateAsOf(Proc(1,aptNum:10),listAppts,_dateTP,_dateToday));
			listAppts[0].AptDateTime=_dateToday.AddHours(15);
			Assert.AreEqual(_dateToday,TpEstimateDates.GetDateAsOf(Proc(1,aptNum:10),listAppts,_dateTP,_dateToday),"An appointment today counts as today.");
		}

		[TestMethod]
		public void DateAsOf_OnlyScheduledStatusesCount() {
			DateTime dateAppt=new DateTime(2027,1,20);
			Assert.AreEqual(dateAppt,TpEstimateDates.GetDateAsOf(Proc(1,aptNum:10),new List<Appointment> { Appt(10,dateAppt,ApptStatus.ASAP) },_dateTP,_dateToday),
				"The old ASAP status is still a scheduled appointment.");
			foreach(ApptStatus apptStatus in new[] { ApptStatus.UnschedList,ApptStatus.Broken,ApptStatus.Planned,ApptStatus.Complete,ApptStatus.PtNote,
				ApptStatus.None })
			{
				Assert.AreEqual(_dateTP,TpEstimateDates.GetDateAsOf(Proc(1,aptNum:10),new List<Appointment> { Appt(10,dateAppt,apptStatus) },_dateTP,_dateToday),
					apptStatus+" does not count.");
			}
		}

		[TestMethod]
		public void DateAsOf_PlannedAppointmentOnlyDoesNotCount() {
			List<Appointment> listAppts=new List<Appointment> { Appt(20,new DateTime(2027,5,1),ApptStatus.Planned) };
			Assert.AreEqual(_dateTP,TpEstimateDates.GetDateAsOf(Proc(1,plannedAptNum:20),listAppts,_dateTP,_dateToday));
			listAppts.Add(Appt(21,new DateTime(2027,5,1),ApptStatus.Scheduled));
			Assert.AreEqual(_dateTP,TpEstimateDates.GetDateAsOf(Proc(1,plannedAptNum:21),listAppts,_dateTP,_dateToday),
				"PlannedAptNum is never read, whatever its appointment's status.");
		}

		[TestMethod]
		public void DateAsOf_NotScheduled_IsTheDefaultDateOrToday() {
			Assert.AreEqual(_dateTP,TpEstimateDates.GetDateAsOf(Proc(1),new List<Appointment>(),_dateTP,_dateToday));
			Assert.AreEqual(_dateTP,TpEstimateDates.GetDateAsOf(Proc(1,aptNum:99),new List<Appointment>(),_dateTP,_dateToday),
				"An appointment that is not in the list does not count.");
			Assert.AreEqual(_dateToday,TpEstimateDates.GetDateAsOf(Proc(1),new List<Appointment>(),DateTime.MinValue,_dateToday));
			Assert.AreEqual(_dateTP,TpEstimateDates.GetDateAsOf(Proc(1),null,_dateTP.AddHours(13),_dateToday),"Dates only.");
			List<Appointment> listAppts=new List<Appointment> { Appt(10,DateTime.MinValue) };
			Assert.AreEqual(_dateTP,TpEstimateDates.GetDateAsOf(Proc(1,aptNum:10),listAppts,_dateTP,_dateToday),"An appointment with no date does not count.");
		}

		[TestMethod]
		public void Instance_AgeOnTheAsOfDate_ForScheduledProceduresOnly() {
			DateTime dateAppt=DateTime.Today.AddDays(40);
			Patient patient=new Patient { Birthdate=dateAppt.AddYears(-15).AddDays(-1) };//15 on the appointment date, 14 today
			TpEstimateDates tpEstimateDates=new TpEstimateDates(new List<Appointment> { Appt(10,dateAppt) },DateTime.Today,patient);
			Assert.AreEqual(15,tpEstimateDates.GetPatientAge(Proc(1,aptNum:10)));
			Assert.AreEqual(patient.Age,tpEstimateDates.GetPatientAge(Proc(2)),"Not scheduled: the age today, as before.");
			Assert.AreEqual(14,patient.Age);
			Assert.AreEqual(0,new TpEstimateDates(null,DateTime.Today,null).GetPatientAge(Proc(1)));
		}

		[TestMethod]
		public void Instance_ProcedureForEstimate_IsADatedCopyOnlyWhenScheduled() {
			DateTime dateAppt=new DateTime(2027,2,1);
			TpEstimateDates tpEstimateDates=new TpEstimateDates(new List<Appointment> { Appt(10,dateAppt) },_dateTP,new Patient(),_dateToday);
			Procedure procedureUnscheduled=Proc(1,procDate:new DateTime(2026,9,1));
			Assert.AreSame(procedureUnscheduled,tpEstimateDates.GetProcedureForEstimate(procedureUnscheduled));
			Procedure procedureScheduled=Proc(2,aptNum:10,procDate:new DateTime(2026,9,1));
			Procedure procedureEst=tpEstimateDates.GetProcedureForEstimate(procedureScheduled);
			Assert.AreNotSame(procedureScheduled,procedureEst);
			Assert.AreEqual(dateAppt,procedureEst.ProcDate);
			Assert.AreEqual(new DateTime(2026,9,1),procedureScheduled.ProcDate,"The procedure itself keeps its date.");
			//ComputeEstimates sets the tax on the copy and the copy's date on the unsent claimprocs.
			procedureEst.TaxAmt=12.5;
			List<ClaimProc> listClaimProcs=new List<ClaimProc> {
				new ClaimProc { ProcNum=2,ProcDate=dateAppt },
				new ClaimProc { ProcNum=2,ProcDate=dateAppt,ClaimNum=5 },
				new ClaimProc { ProcNum=3,ProcDate=dateAppt },
			};
			tpEstimateDates.FinishEstimate(procedureScheduled,procedureEst,listClaimProcs);
			Assert.AreEqual(12.5,procedureScheduled.TaxAmt);
			Assert.AreEqual(new DateTime(2026,9,1),listClaimProcs[0].ProcDate,"The unsent estimate gets the procedure's own date back.");
			Assert.AreEqual(dateAppt,listClaimProcs[1].ProcDate,"Claimprocs on a claim are left alone.");
			Assert.AreEqual(dateAppt,listClaimProcs[2].ProcDate,"Other procedures are left alone.");
		}

		[TestMethod]
		public void Instance_LoopDates_AreTheAsOfDateAndMarked() {
			DateTime dateAppt=new DateTime(2027,2,1);
			TpEstimateDates tpEstimateDates=new TpEstimateDates(new List<Appointment> { Appt(10,dateAppt) },_dateTP,new Patient(),_dateToday);
			List<ClaimProcHist> listHists=new List<ClaimProcHist> { new ClaimProcHist { ProcDate=_dateToday },new ClaimProcHist { ProcDate=_dateToday } };
			tpEstimateDates.SetLoopDates(listHists,Proc(1,aptNum:10));
			Assert.IsTrue(listHists.TrueForAll(x => x.ProcDate==dateAppt && x.IsDateAsOf));
			tpEstimateDates.SetLoopDates(listHists,Proc(2));
			Assert.IsTrue(listHists.TrueForAll(x => x.ProcDate==_dateTP && x.IsDateAsOf));
		}

		[TestMethod]
		public void Instance_EstimateDateNote_OnlyWhenTheDateDiffersFromThePlan() {
			DateTime dateAppt=new DateTime(2027,2,1);
			List<Appointment> listAppts=new List<Appointment> { Appt(10,dateAppt),Appt(11,_dateTP) };
			TpEstimateDates tpEstimateDates=new TpEstimateDates(listAppts,_dateTP,new Patient(),_dateToday);
			Assert.AreEqual("Estimated as of "+dateAppt.ToShortDateString(),tpEstimateDates.GetEstimateDateNote(Proc(1,aptNum:10)));
			Assert.AreEqual("",tpEstimateDates.GetEstimateDateNote(Proc(2)),"Not scheduled.");
			Assert.AreEqual("",tpEstimateDates.GetEstimateDateNote(Proc(3,aptNum:11)),"Scheduled on the plan's own date.");
		}

		[TestMethod]
		public void DateForHistList_IsTheEarliestDate() {
			DateTime dateToday=DateTime.Today;
			List<Appointment> listAppts=new List<Appointment> { Appt(10,dateToday.AddDays(20)),Appt(11,dateToday.AddDays(90)) };
			List<Procedure> listProcs=new List<Procedure> { Proc(1,aptNum:10),Proc(2,aptNum:11),Proc(3) };
			Assert.AreEqual(dateToday.AddDays(20),TreatmentPlanModules.GetDateForHistList(listProcs,listAppts,dateToday.AddDays(60)),
				"A scheduled procedure before the TP date reaches back further.");
			Assert.AreEqual(dateToday,TreatmentPlanModules.GetDateForHistList(listProcs,listAppts,dateToday));
			Assert.AreEqual(dateToday,TreatmentPlanModules.GetDateForHistList(null,listAppts,dateToday));
		}

		[TestMethod]
		public void CoverageEnded_OnlyWhenTermDateIsBeforeTheDate() {
			InsSub insSub=new InsSub { DateTerm=new DateTime(2027,1,31) };
			Assert.IsTrue(Procedures.IsCoverageEnded(insSub,new DateTime(2027,2,1)));
			Assert.IsFalse(Procedures.IsCoverageEnded(insSub,new DateTime(2027,1,31)),"Covered through the term date.");
			Assert.IsFalse(Procedures.IsCoverageEnded(new InsSub(),new DateTime(2027,2,1)),"No term date.");
			Assert.IsFalse(Procedures.IsCoverageEnded(null,new DateTime(2027,2,1)));
			Assert.IsFalse(Procedures.IsCoverageEnded(insSub,DateTime.MinValue));
		}

		[TestMethod]
		public void CoverageEnded_ZeroesTheEstimateButKeepsOverrides() {
			ClaimProc claimProc=new ClaimProc { Status=ClaimProcStatus.Estimate,BaseEst=80,InsEstTotal=60,DedEst=20,WriteOffEst=15,InsEstTotalOverride=10,
				DedEstOverride=5,EstimateNote="Over annual max" };
			Procedures.ZeroEstimateForCoverageEnded(claimProc,new DateTime(2027,1,31));
			Assert.AreEqual(0,claimProc.BaseEst);
			Assert.AreEqual(0,claimProc.InsEstTotal);
			Assert.AreEqual(0,claimProc.DedEst);
			Assert.AreEqual(0,claimProc.WriteOffEst);
			Assert.AreEqual(10,claimProc.InsEstTotalOverride);
			Assert.AreEqual(5,claimProc.DedEstOverride);
			Assert.AreEqual("Over annual max, Coverage ended 1/31/2027",claimProc.EstimateNote);
			Procedures.ZeroEstimateForCoverageEnded(claimProc,new DateTime(2027,1,31));
			Assert.AreEqual("Over annual max, Coverage ended 1/31/2027",claimProc.EstimateNote,"The note is added once.");
			ClaimProc claimProcNoWriteOff=new ClaimProc { Status=ClaimProcStatus.Estimate,WriteOffEst=-1,EstimateNote="" };
			Procedures.ZeroEstimateForCoverageEnded(claimProcNoWriteOff,new DateTime(2027,1,31));
			Assert.AreEqual(-1,claimProcNoWriteOff.WriteOffEst,"A blank write-off estimate stays blank.");
			ClaimProc claimProcCap=new ClaimProc { Status=ClaimProcStatus.CapEstimate,WriteOff=30,WriteOffEst=30,EstimateNote="" };
			Procedures.ZeroEstimateForCoverageEnded(claimProcCap,new DateTime(2027,1,31));
			Assert.AreEqual(0,claimProcCap.WriteOff);
			Assert.AreEqual(0,claimProcCap.WriteOffEst);
		}
	}
}
