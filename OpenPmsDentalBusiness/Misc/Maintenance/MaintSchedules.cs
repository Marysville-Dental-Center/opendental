// Routine maintenance (docs/specs/routine-maintenance.md, "Schedule rules" and "Status"): due dates and status. No database access.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>Works out a maintenance item's due dates from its schedule and its completions. Pure date arithmetic on whole days; the caller passes
	///today (the server's date). DateTime.MinValue means "no date".</summary>
	public static class MaintSchedules {
		///<summary>The largest IntervalCount the setup window accepts.</summary>
		public const int MAX_INTERVAL_COUNT=1000;
		///<summary>The largest LeadDays the setup window accepts.</summary>
		public const int MAX_LEAD_DAYS=365;
		///<summary>Every weekday bit (Sunday 1 ... Saturday 64).</summary>
		public const int WEEKDAYS_ALL=127;

		#region Occurrences
		///<summary>The bit of a weekday in maintitem.Weekdays: Sunday 1, Monday 2 ... Saturday 64.</summary>
		public static int GetWeekdayBit(DayOfWeek dayOfWeek) {
			return 1<<(int)dayOfWeek;
		}

		///<summary>True when the weekday is checked in the mask.</summary>
		public static bool HasWeekday(int weekdays,DayOfWeek dayOfWeek) {
			return (weekdays & GetWeekdayBit(dayOfWeek))!=0;
		}

		///<summary>Adds count units to the date (AddMonths for months and years, so a month end that the target month lacks becomes that month's last
		///day). DateTime.MinValue when the result is past year 9999.</summary>
		public static DateTime AddUnits(DateTime date,EnumMaintIntervalUnit intervalUnit,long count) {
			try {
				switch(intervalUnit) {
					case EnumMaintIntervalUnit.Days:
						return date.Date.AddDays(count);
					case EnumMaintIntervalUnit.Weeks:
						return date.Date.AddDays(count*7);
					case EnumMaintIntervalUnit.Months:
						return date.Date.AddMonths(checked((int)count));
					case EnumMaintIntervalUnit.Years:
						return date.Date.AddMonths(checked((int)(count*12)));
				}
			}
			catch(ArgumentOutOfRangeException) {
			}
			catch(OverflowException) {
			}
			return DateTime.MinValue;
		}

		///<summary>Occurrence k (0 is the anchor) of an Interval schedule, always counted from the anchor so month ends do not drift.
		///DateTime.MinValue when it is past year 9999.</summary>
		public static DateTime GetOccurrence(MaintItem maintItem,long k) {
			return AddUnits(maintItem.DateAnchor,maintItem.IntervalUnit,k*Math.Max(1,maintItem.IntervalCount));
		}

		///<summary>The first occurrence on or after the date, for Interval and Weekdays schedules. No occurrence is before the anchor. DateTime.MinValue
		///when there is none (no weekday checked, an invalid anchor, or past year 9999).</summary>
		public static DateTime GetFirstOccurrenceOnOrAfter(MaintItem maintItem,DateTime date) {
			DateTime dateAnchor=maintItem.DateAnchor.Date;
			if(dateAnchor.Year<1880) {
				return DateTime.MinValue;
			}
			date=date.Date;
			if(date<dateAnchor) {
				date=dateAnchor;
			}
			if(maintItem.ScheduleType==EnumMaintScheduleType.Weekdays) {
				if((maintItem.Weekdays & WEEKDAYS_ALL)==0) {
					return DateTime.MinValue;
				}
				for(int i=0;i<7;i++) {
					if(date>DateTime.MaxValue.Date.AddDays(-i)) {
						return DateTime.MinValue;
					}
					DateTime dateTry=date.AddDays(i);
					if(HasWeekday(maintItem.Weekdays,dateTry.DayOfWeek)) {
						return dateTry;
					}
				}
				return DateTime.MinValue;
			}
			if(date==dateAnchor) {
				return dateAnchor;
			}
			int count=Math.Max(1,maintItem.IntervalCount);
			long k;
			switch(maintItem.IntervalUnit) {
				case EnumMaintIntervalUnit.Days:
				case EnumMaintIntervalUnit.Weeks:
					long step=maintItem.IntervalUnit==EnumMaintIntervalUnit.Weeks ? count*7L : count;
					long days=(long)(date-dateAnchor).TotalDays;
					k=(days+step-1)/step;//Ceiling: the first occurrence that is not before the date.
					return GetOccurrence(maintItem,k);
				default:
					long monthsPerStep=maintItem.IntervalUnit==EnumMaintIntervalUnit.Years ? count*12L : count;
					long months=(date.Year-dateAnchor.Year)*12L+date.Month-dateAnchor.Month;
					k=Math.Max(0,months/monthsPerStep-1);//An estimate at or below the answer; AddMonths only ever shortens a month end.
					while(true) {
						DateTime dateOccurrence=GetOccurrence(maintItem,k);
						if(dateOccurrence==DateTime.MinValue) {
							return DateTime.MinValue;
						}
						if(dateOccurrence>=date) {
							return dateOccurrence;
						}
						k++;
					}
			}
		}

		///<summary>The first occurrence strictly after the date. See GetFirstOccurrenceOnOrAfter.</summary>
		public static DateTime GetFirstOccurrenceAfter(MaintItem maintItem,DateTime date) {
			if(date.Date>=DateTime.MaxValue.Date) {
				return DateTime.MinValue;
			}
			return GetFirstOccurrenceOnOrAfter(maintItem,date.Date.AddDays(1));
		}
		#endregion Occurrences

		#region Due date
		///<summary>The day through which the completions cover an Interval or Weekdays schedule: the latest of each completion's due date and date
		///done. A completion closes the occurrence it was recorded against, early or late, and every occurrence up to the day it was done.
		///DateTime.MinValue when there is no completion.</summary>
		public static DateTime GetCoveredThrough(List<MaintRecord> listMaintRecords) {
			DateTime dateCovered=DateTime.MinValue;
			foreach(MaintRecord maintRecord in listMaintRecords??new List<MaintRecord>()) {
				DateTime date=maintRecord.DateDue.Date>maintRecord.DateDone.Date ? maintRecord.DateDue.Date : maintRecord.DateDone.Date;
				if(date>dateCovered) {
					dateCovered=date;
				}
			}
			return dateCovered;
		}

		///<summary>The latest date done of the completions, DateTime.MinValue when there is none.</summary>
		public static DateTime GetLastDone(List<MaintRecord> listMaintRecords) {
			if(listMaintRecords.IsNullOrEmpty()) {
				return DateTime.MinValue;
			}
			return listMaintRecords.Max(x => x.DateDone.Date);
		}

		///<summary>The item's due date given its completions (the records of this item only). The anchor is the first due date.
		///- Interval and Weekdays: the first occurrence after the day the completions cover (GetCoveredThrough); with none, the first occurrence.
		///- AfterCompletion: the latest date done plus IntervalCount units; with no completion, the anchor.
		///DateTime.MinValue when the schedule gives no date (see GetScheduleError).</summary>
		public static DateTime GetDueDate(MaintItem maintItem,List<MaintRecord> listMaintRecords) {
			if(maintItem.DateAnchor.Year<1880) {
				return DateTime.MinValue;
			}
			if(maintItem.ScheduleType==EnumMaintScheduleType.AfterCompletion) {
				DateTime dateLastDone=GetLastDone(listMaintRecords);
				if(dateLastDone==DateTime.MinValue) {
					return maintItem.DateAnchor.Date;
				}
				return AddUnits(dateLastDone,maintItem.IntervalUnit,Math.Max(1,maintItem.IntervalCount));
			}
			DateTime dateCovered=GetCoveredThrough(listMaintRecords);
			if(dateCovered==DateTime.MinValue) {
				return GetFirstOccurrenceOnOrAfter(maintItem,maintItem.DateAnchor);
			}
			return GetFirstOccurrenceAfter(maintItem,dateCovered);
		}
		#endregion Due date

		#region Status
		///<summary>Overdue when the due date is before today; due soon from LeadDays before the due date through the due date (so due today is due
		///soon); upcoming after that. Hidden items are Hidden and an item without a due date NoSchedule.</summary>
		public static EnumMaintStatus GetStatus(MaintItem maintItem,DateTime dateDue,DateTime dateToday) {
			if(maintItem.IsHidden) {
				return EnumMaintStatus.Hidden;
			}
			if(dateDue.Year<1880) {
				return EnumMaintStatus.NoSchedule;
			}
			dateToday=dateToday.Date;
			if(dateDue.Date<dateToday) {
				return EnumMaintStatus.Overdue;
			}
			if(dateDue.Date<=dateToday.AddDays(Math.Max(0,maintItem.LeadDays))) {
				return EnumMaintStatus.DueSoon;
			}
			return EnumMaintStatus.Upcoming;
		}

		///<summary>The board's Days column: "3 overdue", "Today", "Tomorrow" or "in 5". Empty without a due date.</summary>
		public static string GetDaysText(DateTime dateDue,DateTime dateToday) {
			if(dateDue.Year<1880) {
				return "";
			}
			int days=(int)(dateDue.Date-dateToday.Date).TotalDays;
			if(days<0) {
				return (-days).ToString()+" "+Lans.g("MaintSchedules","overdue");
			}
			if(days==0) {
				return Lans.g("MaintSchedules","Today");
			}
			if(days==1) {
				return Lans.g("MaintSchedules","Tomorrow");
			}
			return Lans.g("MaintSchedules","in")+" "+days.ToString();
		}

		///<summary>The text of a status for the board.</summary>
		public static string GetStatusText(EnumMaintStatus status) {
			switch(status) {
				case EnumMaintStatus.Overdue:
					return Lans.g("MaintSchedules","Overdue");
				case EnumMaintStatus.DueSoon:
					return Lans.g("MaintSchedules","Due soon");
				case EnumMaintStatus.Upcoming:
					return Lans.g("MaintSchedules","Upcoming");
				case EnumMaintStatus.Hidden:
					return Lans.g("MaintSchedules","Hidden");
				default:
					return Lans.g("MaintSchedules","No schedule");
			}
		}

		///<summary>The board's order: overdue (oldest due date first), due soon, upcoming, each by due date and then name; items without a schedule,
		///then hidden items, last.</summary>
		public static int GetSortRank(EnumMaintStatus status) {
			switch(status) {
				case EnumMaintStatus.Overdue:
					return 0;
				case EnumMaintStatus.DueSoon:
					return 1;
				case EnumMaintStatus.Upcoming:
					return 2;
				case EnumMaintStatus.NoSchedule:
					return 3;
				default:
					return 4;
			}
		}
		#endregion Status

		#region Text and validation
		///<summary>The weekdays of the mask as short names in the current culture, Sunday first: "Mon, Wed, Fri".</summary>
		public static string GetWeekdaysText(int weekdays) {
			List<string> listNames=new List<string>();
			string[] arrayNames=CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames;
			for(int i=0;i<7;i++) {
				if(HasWeekday(weekdays,(DayOfWeek)i)) {
					listNames.Add(arrayNames[i]);
				}
			}
			return string.Join(", ",listNames);
		}

		///<summary>The schedule in words, for example "Every 3 months from 11/1/2026", "Mon, Wed, Fri" or "2 weeks after last done".</summary>
		public static string GetScheduleText(MaintItem maintItem) {
			string unit=GetUnitText(maintItem.IntervalUnit,maintItem.IntervalCount);
			switch(maintItem.ScheduleType) {
				case EnumMaintScheduleType.Weekdays:
					return GetWeekdaysText(maintItem.Weekdays);
				case EnumMaintScheduleType.AfterCompletion:
					return maintItem.IntervalCount+" "+unit+" "+Lans.g("MaintSchedules","after last done");
				default:
					string text=Lans.g("MaintSchedules","Every")+" ";
					if(maintItem.IntervalCount!=1) {
						text+=maintItem.IntervalCount+" ";
					}
					text+=unit;
					if(maintItem.DateAnchor.Year>1880) {
						text+=" "+Lans.g("MaintSchedules","from")+" "+maintItem.DateAnchor.ToShortDateString();
					}
					return text;
			}
		}

		///<summary>"day" or "days", "week" or "weeks" and so on.</summary>
		public static string GetUnitText(EnumMaintIntervalUnit intervalUnit,int count) {
			bool isPlural=count!=1;
			switch(intervalUnit) {
				case EnumMaintIntervalUnit.Weeks:
					return isPlural ? Lans.g("MaintSchedules","weeks") : Lans.g("MaintSchedules","week");
				case EnumMaintIntervalUnit.Months:
					return isPlural ? Lans.g("MaintSchedules","months") : Lans.g("MaintSchedules","month");
				case EnumMaintIntervalUnit.Years:
					return isPlural ? Lans.g("MaintSchedules","years") : Lans.g("MaintSchedules","year");
				default:
					return isPlural ? Lans.g("MaintSchedules","days") : Lans.g("MaintSchedules","day");
			}
		}

		///<summary>Why the item cannot be saved, or null when it can: a name, a valid anchor date, IntervalCount 1 to 1000 (Interval and
		///AfterCompletion), at least one weekday (Weekdays), no Years after the last completion, lead time 0 to 365, and at most one assignee.</summary>
		public static string GetItemError(MaintItem maintItem) {
			if(string.IsNullOrWhiteSpace(maintItem.ItemName)) {
				return Lans.g("MaintSchedules","Please enter a name.");
			}
			if(maintItem.DateAnchor.Year<1880) {
				return Lans.g("MaintSchedules","Please enter a valid start date.");
			}
			if(maintItem.ScheduleType==EnumMaintScheduleType.Weekdays) {
				if((maintItem.Weekdays & WEEKDAYS_ALL)==0) {
					return Lans.g("MaintSchedules","Please check at least one weekday.");
				}
			}
			else {
				if(maintItem.IntervalCount<1 || maintItem.IntervalCount>MAX_INTERVAL_COUNT) {
					return Lans.g("MaintSchedules","The number of days, weeks, months or years must be from 1 to")+" "+MAX_INTERVAL_COUNT+".";
				}
				if(!Enum.IsDefined(typeof(EnumMaintIntervalUnit),maintItem.IntervalUnit)) {
					return Lans.g("MaintSchedules","Please choose days, weeks, months or years.");
				}
				if(maintItem.ScheduleType==EnumMaintScheduleType.AfterCompletion && maintItem.IntervalUnit==EnumMaintIntervalUnit.Years) {
					return Lans.g("MaintSchedules","After the last completion, use days, weeks or months.");
				}
			}
			if(!Enum.IsDefined(typeof(EnumMaintScheduleType),maintItem.ScheduleType)) {
				return Lans.g("MaintSchedules","Please choose a schedule.");
			}
			if(maintItem.LeadDays<0 || maintItem.LeadDays>MAX_LEAD_DAYS) {
				return Lans.g("MaintSchedules","The reminder lead time must be from 0 to")+" "+MAX_LEAD_DAYS+" "+Lans.g("MaintSchedules","days.");
			}
			if(maintItem.UserNumAssigned!=0 && maintItem.UserGroupNumAssigned!=0) {
				return Lans.g("MaintSchedules","Assign the item to a user or to a user group, not both.");
			}
			return null;
		}
		#endregion Text and validation
	}

	///<summary>Where a maintenance item stands on the board. Never stored.</summary>
	public enum EnumMaintStatus {
		///<summary>The due date is before today.</summary>
		Overdue,
		///<summary>The due date is today or within the item's lead time.</summary>
		DueSoon,
		///<summary>The due date is after the lead time.</summary>
		Upcoming,
		///<summary>The schedule gives no date.</summary>
		NoSchedule,
		///<summary>The item is hidden (retired).</summary>
		Hidden,
	}
}
