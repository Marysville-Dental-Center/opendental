// Clock-outs dated in the future (Open Dental 26.1.63, feature v26_1-manage-timecard-future-clockout, package M2-EMP in docs/specs/port-backlog.md).
// An administrator can enter the end of a shift ahead of time; until that time comes the employee counts as clocked in. The rules are this fork's
// own (see docs/port-notes/M2-EMP.md): nothing public describes Open Dental's.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDentBusiness {
	///<summary>What the clock event edit window may do with the two times it is about to save, as far as dates after today go.</summary>
	public enum EnumClockEventFutureDate {
		///<summary>Neither time is dated after today.</summary>
		Ok,
		///<summary>The first time (the clock-in, or the start of a break) is dated after today. Refused, as before.</summary>
		FirstTimeRefused,
		///<summary>The second time is dated after today, and this event or this user may not have that. Refused, as before.</summary>
		SecondTimeRefused,
		///<summary>The clock-out is dated after today. Allowed once the user confirms.</summary>
		ClockOutConfirm,
	}

	public partial class ClockEvents {
		///<summary>Checks the times the clock event edit window is about to save against the future-date rules. Only dates count, as in 24.3: a
		///time later today was already accepted without a question. Rules: the first time may never be dated after today; the clock-out (the second
		///time of a Home or Lunch event) may, but only for users with TimecardsEditAll and only after a confirmation; a break keeps refusing both,
		///because its second time is the return from the break. The event counts as a break when it was one or is being changed into one.</summary>
		public static EnumClockEventFutureDate CheckFutureDates(TimeClockStatus timeClockStatusOld,TimeClockStatus timeClockStatusNew,
			DateTime timeDisplayed1,DateTime timeDisplayed2,DateTime dateToday,bool hasTimecardsEditAll)
		{
			Meth.NoCheckMiddleTierRole();
			if(timeDisplayed1.Date>dateToday.Date) {
				return EnumClockEventFutureDate.FirstTimeRefused;
			}
			if(timeDisplayed2.Year<1880 || timeDisplayed2.Date<=dateToday.Date) {
				return EnumClockEventFutureDate.Ok;
			}
			if(timeClockStatusOld==TimeClockStatus.Break || timeClockStatusNew==TimeClockStatus.Break || !hasTimecardsEditAll) {
				return EnumClockEventFutureDate.SecondTimeRefused;
			}
			return EnumClockEventFutureDate.ClockOutConfirm;
		}

		///<summary>True when the event is a Home or Lunch event whose clock-out is entered and still ahead of now: the employee stays clocked in until
		///then. Breaks never count, since their second time is the return from the break.</summary>
		public static bool HasFutureClockOut(ClockEvent clockEvent,DateTime dateTimeNow) {
			Meth.NoCheckMiddleTierRole();
			if(clockEvent==null || clockEvent.ClockStatus==TimeClockStatus.Break || clockEvent.TimeDisplayed2.Year<1880) {
				return false;
			}
			return clockEvent.TimeDisplayed2>dateTimeNow;
		}

		///<summary>HasFutureClockOut against the time the clock buttons stamp: this computer's time when LocalTimeOverridesServerTime is on, otherwise
		///the database server's. Asks the server only when the event has a clock-out to compare.</summary>
		public static bool HasFutureClockOut(ClockEvent clockEvent) {
			Meth.NoCheckMiddleTierRole();
			if(!HasFutureClockOut(clockEvent,DateTime.MinValue)) {
				return false;//No clock-out at all, or a break.
			}
			return HasFutureClockOut(clockEvent,GetNowForClock());
		}

		///<summary>The time ClockIn and ClockOut stamp on an event.</summary>
		public static DateTime GetNowForClock() {
			Meth.NoCheckMiddleTierRole();
			if(PrefC.GetBool(PrefName.LocalTimeOverridesServerTime)) {
				return DateTime.Now;
			}
			return MiscData.GetNowDateTime();
		}

		///<summary>Security log text for saving a clock event whose clock-out is dated in the future: the employee and the old and new times.</summary>
		public static string GetFutureClockOutLogText(string employeeName,ClockEvent clockEventOld,ClockEvent clockEventNew) {
			Meth.NoCheckMiddleTierRole();
			return Lans.g("ClockEvents","Clock event edited with a future clock-out for")+" "+employeeName+". "
				+Lans.g("ClockEvents","Clock-in")+": "+FormatLogTime(clockEventOld.TimeDisplayed1)+" -> "+FormatLogTime(clockEventNew.TimeDisplayed1)+". "
				+Lans.g("ClockEvents","Clock-out")+": "+FormatLogTime(clockEventOld.TimeDisplayed2)+" -> "+FormatLogTime(clockEventNew.TimeDisplayed2)+".";
		}

		private static string FormatLogTime(DateTime dateTime) {
			if(dateTime.Year<1880) {
				return Lans.g("ClockEvents","none");
			}
			return dateTime.ToString();
		}

		///<summary>Text for the time card's Out column (In for a break): the time, and the date in front of it when the second time falls on a later
		///date than the first, as a clock-out entered for a later day does. Blank when there is no second time.</summary>
		public static string GetSecondTimeCellText(ClockEvent clockEvent,bool showSeconds) {
			Meth.NoCheckMiddleTierRole();
			if(clockEvent.TimeDisplayed2.Year<1880) {
				return "";
			}
			string time=showSeconds ? clockEvent.TimeDisplayed2.ToLongTimeString() : clockEvent.TimeDisplayed2.ToShortTimeString();
			if(clockEvent.TimeDisplayed2.Date<=clockEvent.TimeDisplayed1.Date) {
				return time;
			}
			return clockEvent.TimeDisplayed2.ToShortDateString()+" "+time;
		}

		///<summary>Width of the time card's second-time column: widthDefault, or wide enough for a date and a time when any of the events shows its
		///date there (see GetSecondTimeCellText).</summary>
		public static int GetSecondTimeColumnWidth(List<ClockEvent> listClockEvents,int widthDefault) {
			Meth.NoCheckMiddleTierRole();
			if(listClockEvents==null || !listClockEvents.Any(x => x.TimeDisplayed2.Year>=1880 && x.TimeDisplayed2.Date>x.TimeDisplayed1.Date)) {
				return widthDefault;
			}
			return Math.Max(widthDefault,WIDTH_SECOND_TIME_WITH_DATE);
		}

		///<summary>Room for a short date and a long time, like "12/31/2026 11:59:59 PM".</summary>
		public const int WIDTH_SECOND_TIME_WITH_DATE=130;
	}
}
