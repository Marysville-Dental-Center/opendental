// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using OpenPmsDentalBusiness;

namespace ImagingCapture.Save {
	///<summary>The default provider of a capture (design 10.8): the provider of the patient's appointment today (ProvHyg for a hygiene
	///appointment, otherwise ProvNum), else the patient's primary provider. The banner shows it and lets the operator change it before Start;
	///it goes into Document.ProvNum and Mount.ProvNum.</summary>
	public static class RadiographProvider {
		///<summary>The default provider for patient at now. listAppointments: the patient's appointments (any dates). Appointments today that
		///are scheduled or complete count; of several, the last one that started at or before now, else the first one today. 0 when neither an
		///appointment nor the patient names a provider.</summary>
		public static long GetDefaultProvNum(Patient patient,IEnumerable<Appointment> listAppointments,DateTime now) {
			Appointment appointment=GetAppointmentToday(listAppointments,now);
			if(appointment!=null) {
				long provNum=appointment.IsHygiene && appointment.ProvHyg!=0 ? appointment.ProvHyg : appointment.ProvNum;
				if(provNum!=0) {
					return provNum;
				}
			}
			return patient?.PriProv??0;
		}

		///<summary>The appointment GetDefaultProvNum takes the provider from, or null.</summary>
		public static Appointment GetAppointmentToday(IEnumerable<Appointment> listAppointments,DateTime now) {
			List<Appointment> listToday=(listAppointments??Enumerable.Empty<Appointment>())
				.Where(x => x!=null && x.AptDateTime.Date==now.Date && (x.AptStatus==ApptStatus.Scheduled || x.AptStatus==ApptStatus.Complete))
				.OrderBy(x => x.AptDateTime)
				.ToList();
			if(listToday.Count==0) {
				return null;
			}
			Appointment appointmentStarted=listToday.LastOrDefault(x => x.AptDateTime<=now);
			return appointmentStarted??listToday[0];
		}

		///<summary>GetDefaultProvNum with the patient's appointments from the database (Appointments.GetAppointmentsForPat).</summary>
		public static long GetDefaultProvNumFromDb(Patient patient) {
			if(patient==null) {
				return 0;
			}
			return GetDefaultProvNum(patient,Appointments.GetAppointmentsForPat(patient.PatNum),DateTime.Now);
		}
	}
}
