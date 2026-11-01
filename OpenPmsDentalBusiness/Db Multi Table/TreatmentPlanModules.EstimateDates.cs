// M2-EST: Treatment Plan module estimates as of the scheduled appointment date (Open Dental 25.4).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenPmsDentalBusiness {
	public partial class TreatmentPlanModules {
		///<summary>The date the claimproc history of an active or inactive treatment plan is read from: the TP date, or the earliest date a procedure on
		///a scheduled appointment is estimated as of when that is earlier. The history reaches back from the benefit year (and frequency windows) of
		///that date and has no upper limit, so it covers every later date too.</summary>
		public static DateTime GetDateForHistList(List<Procedure> listProcedures,List<Appointment> listAppointments,DateTime dateTimeTP) {
			Meth.NoCheckMiddleTierRole();
			DateTime dateHist=(dateTimeTP.Year<1880 ? DateTime.Today : dateTimeTP.Date);
			if(listProcedures==null) {
				return dateHist;
			}
			for(int i=0;i<listProcedures.Count;i++) {
				if(TpEstimateDates.GetScheduledAppointment(listProcedures[i],listAppointments)==null) {
					continue;
				}
				DateTime dateAsOf=TpEstimateDates.GetDateAsOf(listProcedures[i],listAppointments,dateHist,DateTime.Today);
				if(dateAsOf<dateHist) {
					dateHist=dateAsOf;
				}
			}
			return dateHist;
		}
	}

	///<summary>The date each procedure of an active or inactive treatment plan is estimated as of (Open Dental 25.4, "Treatment Plan module estimates
	///consider the appointment date of scheduled procedures"). The rules are the fork's reading of the release note (INFERRED, see
	///docs/port-notes/M2-EST.md):
	///<para>1. A procedure on a scheduled appointment (AptNum, status Scheduled or the old ASAP) is estimated as of the appointment date, or today
	///when the appointment is in the past. Planned appointments (PlannedAptNum), the unscheduled list and broken appointments do not count.</para>
	///<para>2. Every other procedure keeps the behavior from before: the TP date picker's date when frequency checking shows the picker, today
	///otherwise, and the patient's age today.</para>
	///<para>3. For a scheduled procedure the benefit year (annual max, deductible), frequency limits, waiting periods and age limits use that date
	///(the patient's age on that date), and a plan whose coverage ended before it gives no estimate.</para>
	///Saved treatment plans are never recalculated.</summary>
	public class TpEstimateDates {
		private readonly List<Appointment> _listAppointments;
		private readonly DateTime _dateDefault;
		private readonly DateTime _dateToday;
		private readonly Patient _patient;

		///<param name="listAppointments">The appointments of the plan's procedures.</param>
		///<param name="dateDefault">The as-of date of procedures that are not on a scheduled appointment.</param>
		///<param name="patient">For the age on the as-of date.</param>
		///<param name="dateToday">Today; only tests pass another date.</param>
		public TpEstimateDates(List<Appointment> listAppointments,DateTime dateDefault,Patient patient,DateTime dateToday=default(DateTime)) {
			_listAppointments=listAppointments??new List<Appointment>();
			_dateToday=(dateToday.Year<1880 ? DateTime.Today : dateToday.Date);
			_dateDefault=(dateDefault.Year<1880 ? _dateToday : dateDefault.Date);
			_patient=patient;
		}

		///<summary>Appointment statuses that make a procedure "scheduled": Scheduled, and the deprecated ASAP status that older data may still hold.</summary>
		public static bool IsScheduledStatus(ApptStatus apptStatus) {
			return apptStatus==ApptStatus.Scheduled || apptStatus==ApptStatus.ASAP;
		}

		///<summary>The scheduled appointment the procedure is attached to (AptNum), or null. A planned appointment (PlannedAptNum) never counts.</summary>
		public static Appointment GetScheduledAppointment(Procedure procedure,List<Appointment> listAppointments) {
			if(procedure==null || procedure.AptNum==0 || listAppointments==null) {
				return null;
			}
			Appointment appointment=listAppointments.Find(x => x.AptNum==procedure.AptNum);
			if(appointment==null || !IsScheduledStatus(appointment.AptStatus) || appointment.AptDateTime.Year<1880) {
				return null;
			}
			return appointment;
		}

		///<summary>The date a procedure is estimated as of: its scheduled appointment's date (today for an appointment in the past), otherwise
		///dateDefault.</summary>
		public static DateTime GetDateAsOf(Procedure procedure,List<Appointment> listAppointments,DateTime dateDefault,DateTime dateToday) {
			Appointment appointment=GetScheduledAppointment(procedure,listAppointments);
			if(appointment==null) {
				return (dateDefault.Year<1880 ? dateToday.Date : dateDefault.Date);
			}
			if(appointment.AptDateTime.Date<dateToday.Date) {
				return dateToday.Date;
			}
			return appointment.AptDateTime.Date;
		}

		public bool IsOnScheduledAppointment(Procedure procedure) {
			return GetScheduledAppointment(procedure,_listAppointments)!=null;
		}

		public DateTime GetDateAsOf(Procedure procedure) {
			return GetDateAsOf(procedure,_listAppointments,_dateDefault,_dateToday);
		}

		///<summary>The patient's age for age limitations: on the as-of date for a scheduled procedure, today otherwise (as before).</summary>
		public int GetPatientAge(Procedure procedure) {
			if(_patient==null) {
				return 0;
			}
			if(!IsOnScheduledAppointment(procedure)) {
				return _patient.Age;
			}
			return Patients.DateToAge(_patient.Birthdate,GetDateAsOf(procedure));
		}

		///<summary>For the estimate of one procedure when the plan does not change procedure dates (frequency checking off): a copy dated as of the
		///scheduled appointment, or the procedure itself when it is not scheduled. Pass the result to ComputeEstimates with useProcDateOnProc set to
		///IsOnScheduledAppointment, then call FinishEstimate.</summary>
		public Procedure GetProcedureForEstimate(Procedure procedure) {
			if(!IsOnScheduledAppointment(procedure)) {
				return procedure;
			}
			Procedure procedureEst=procedure.Copy();
			procedureEst.ProcDate=GetDateAsOf(procedure);
			return procedureEst;
		}

		///<summary>Copies the sales tax estimate back to the procedure and gives its unsent claimprocs back the procedure's own date, which
		///ComputeEstimates set from the dated copy. Does nothing when the procedure was estimated itself.</summary>
		public void FinishEstimate(Procedure procedure,Procedure procedureEst,List<ClaimProc> listClaimProcs) {
			if(ReferenceEquals(procedure,procedureEst)) {
				return;
			}
			procedure.TaxAmt=procedureEst.TaxAmt;
			if(listClaimProcs==null || procedureEst.ProcDate.Date==procedure.ProcDate.Date) {
				return;
			}
			for(int i=0;i<listClaimProcs.Count;i++) {
				if(listClaimProcs[i].ProcNum!=procedure.ProcNum || listClaimProcs[i].ClaimNum!=0) {
					continue;
				}
				if(listClaimProcs[i].ProcDate.Date==procedureEst.ProcDate.Date) {
					listClaimProcs[i].ProcDate=procedure.ProcDate;
				}
			}
		}

		///<summary>Dates the loop entries of one procedure as of its estimate, so that later procedures of the plan count them only toward that benefit
		///period (Benefits.IsLoopHistInPeriod).</summary>
		public void SetLoopDates(List<ClaimProcHist> listClaimProcHists,Procedure procedure) {
			DateTime dateAsOf=GetDateAsOf(procedure);
			for(int i=0;i<listClaimProcHists.Count;i++) {
				listClaimProcHists[i].ProcDate=dateAsOf;
				listClaimProcHists[i].IsDateAsOf=true;
			}
		}

		///<summary>A line for the procedure's description in the Treatment Plan grid, "Estimated as of (date)", when the procedure is estimated as of
		///another date than the rest of the plan (fork addition; the 25.4 release note shows no UI). Blank otherwise.</summary>
		public string GetEstimateDateNote(Procedure procedure) {
			if(!IsOnScheduledAppointment(procedure)) {
				return "";
			}
			DateTime dateAsOf=GetDateAsOf(procedure);
			if(dateAsOf==_dateDefault) {
				return "";
			}
			return Lans.g("TableTP","Estimated as of")+" "+dateAsOf.ToShortDateString();
		}
	}
}
