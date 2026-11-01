// Package B-APPT (docs/specs/port-backlog.md): which appointment permissions a pinboard or unschedule action needs, after Open Dental 25.3
// (backported to 25.2.40). Items v25_3-security-apptcreate-pinboard and v25_3-security-apptmove-scope. The display text of both permissions
// is in GroupPermission.cs (package M1-02); this file is the enforcement side, called by the Appointments and Chart modules and their windows.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;

namespace OpenDentBusiness {
	///<summary>Pure rules, no database. Appointment Move covers dragging, sending to the pinboard and setting unscheduled for an appointment of
	///any status. It is not needed to move an appointment off the Unscheduled List, or to send a planned appointment to the pinboard, because a
	///planned appointment is copied, not moved. Turning a planned appointment into a scheduled one (dropping it off the pinboard) needs
	///Appointment Create and never Appointment Move.</summary>
	public static class ApptPermissionRules {
		///<summary>The permissions needed to set an appointment of this status unscheduled, in the order they are checked. Every status needs
		///Appointment Move; a complete appointment also needs Completed Appointment Edit (in 24.3 it needed only Completed Appointment Edit).</summary>
		public static List<EnumPermType> GetPermsToUnschedule(ApptStatus aptStatus) {
			List<EnumPermType> listPermTypes=new List<EnumPermType>();
			if(aptStatus==ApptStatus.Complete) {
				listPermTypes.Add(EnumPermType.AppointmentCompleteEdit);
			}
			listPermTypes.Add(EnumPermType.AppointmentMove);
			return listPermTypes;
		}

		///<summary>True if sending an appointment of this status to the pinboard needs Appointment Move. A planned appointment is copied onto
		///the pinboard, so it does not; every other status does.</summary>
		public static bool IsMoveNeededToPin(ApptStatus aptStatus) {
			return aptStatus!=ApptStatus.Planned;
		}

		///<summary>For a row of the Chart module's Planned Appointments grid: aptStatusLinked is the status of the appointment scheduled from
		///the planned appointment (None when there is none). For these statuses the button sends that appointment to the pinboard, which is a
		///move; otherwise it copies the planned appointment itself, which needs no Appointment Move.</summary>
		public static bool IsPlannedRowPinAMove(ApptStatus aptStatusLinked) {
			switch(aptStatusLinked) {
				case ApptStatus.Complete:
				case ApptStatus.Scheduled:
				case ApptStatus.UnschedList:
				case ApptStatus.Broken:
					return true;
				default:
					return false;
			}
		}

		///<summary>The permission needed to drop an appointment of this status off the pinboard onto the schedule, or None when nothing more
		///is checked at the drop: a planned appointment becomes a new scheduled appointment, which needs Appointment Create; any other
		///appointment was checked for Appointment Move when it was put on the pinboard (or came from the Unscheduled List, which needs none).</summary>
		public static EnumPermType GetPermToDropFromPinboard(ApptStatus aptStatus) {
			if(aptStatus==ApptStatus.Planned) {
				return EnumPermType.AppointmentCreate;
			}
			return EnumPermType.None;
		}
	}
}
