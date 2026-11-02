// Schedule math of routine maintenance (docs/specs/routine-maintenance.md, "Schedule rules"). Pure date arithmetic; no database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace MaintenanceTests {
	[TestClass]
	public class ScheduleTests {
		[TestInitialize]
		public void TestInitialize() {
			TestDb.SetCultureUs();
		}

		public static MaintItem Interval(int count,EnumMaintIntervalUnit unit,DateTime dateAnchor) {
			return new MaintItem { MaintItemNum=1,ItemName="Synthetic item",ScheduleType=EnumMaintScheduleType.Interval,IntervalCount=count,
				IntervalUnit=unit,DateAnchor=dateAnchor };
		}

		public static MaintItem Weekdays(DateTime dateAnchor,params DayOfWeek[] arrayDays) {
			int weekdays=arrayDays.Aggregate(0,(x,y) => x|MaintSchedules.GetWeekdayBit(y));
			return new MaintItem { MaintItemNum=1,ItemName="Synthetic item",ScheduleType=EnumMaintScheduleType.Weekdays,Weekdays=weekdays,
				IntervalCount=1,DateAnchor=dateAnchor };
		}

		public static MaintItem AfterCompletion(int count,EnumMaintIntervalUnit unit,DateTime dateAnchor) {
			return new MaintItem { MaintItemNum=1,ItemName="Synthetic item",ScheduleType=EnumMaintScheduleType.AfterCompletion,IntervalCount=count,
				IntervalUnit=unit,DateAnchor=dateAnchor };
		}

		public static MaintRecord Done(DateTime dateDone,DateTime dateDue) {
			return new MaintRecord { MaintItemNum=1,DateDone=dateDone,DateDue=dateDue };
		}

		private static List<DateTime> FirstOccurrences(MaintItem maintItem,int count) {
			List<DateTime> listDates=new List<DateTime>();
			DateTime date=MaintSchedules.GetFirstOccurrenceOnOrAfter(maintItem,maintItem.DateAnchor);
			for(int i=0;i<count;i++) {
				listDates.Add(date);
				date=MaintSchedules.GetFirstOccurrenceAfter(maintItem,date);
			}
			return listDates;
		}

		private static DateTime D(int year,int month,int day) {
			return new DateTime(year,month,day);
		}

		#region Every unit
		[TestMethod]
		public void EveryNDays_FromTheAnchor() {
			MaintItem maintItem=Interval(3,EnumMaintIntervalUnit.Days,D(2026,11,2));
			CollectionAssert.AreEqual(new[] { D(2026,11,2),D(2026,11,5),D(2026,11,8),D(2026,11,11) },FirstOccurrences(maintItem,4));
			Assert.AreEqual(D(2026,11,5),MaintSchedules.GetFirstOccurrenceOnOrAfter(maintItem,D(2026,11,3)));
			Assert.AreEqual(D(2026,11,5),MaintSchedules.GetFirstOccurrenceOnOrAfter(maintItem,D(2026,11,5)));
			Assert.AreEqual(D(2026,11,8),MaintSchedules.GetFirstOccurrenceAfter(maintItem,D(2026,11,5)));
			//Across the turn of the year and a leap day.
			MaintItem maintItemDaily=Interval(1,EnumMaintIntervalUnit.Days,D(2028,2,28));
			CollectionAssert.AreEqual(new[] { D(2028,2,28),D(2028,2,29),D(2028,3,1) },FirstOccurrences(maintItemDaily,3));
		}

		[TestMethod]
		public void EveryNWeeks_FromTheAnchor() {
			MaintItem maintItem=Interval(2,EnumMaintIntervalUnit.Weeks,D(2026,12,21));
			CollectionAssert.AreEqual(new[] { D(2026,12,21),D(2027,1,4),D(2027,1,18) },FirstOccurrences(maintItem,3));
			Assert.AreEqual(D(2027,1,4),MaintSchedules.GetFirstOccurrenceOnOrAfter(maintItem,D(2026,12,22)));
		}

		[TestMethod]
		public void EveryNMonths_CountedFromTheAnchorSoMonthEndsDoNotDrift() {
			MaintItem maintItem=Interval(1,EnumMaintIntervalUnit.Months,D(2027,1,31));
			CollectionAssert.AreEqual(new[] { D(2027,1,31),D(2027,2,28),D(2027,3,31),D(2027,4,30),D(2027,5,31) },FirstOccurrences(maintItem,5));
			//In a leap year February ends on the 29th.
			MaintItem maintItemLeap=Interval(1,EnumMaintIntervalUnit.Months,D(2028,1,31));
			CollectionAssert.AreEqual(new[] { D(2028,1,31),D(2028,2,29),D(2028,3,31) },FirstOccurrences(maintItemLeap,3));
			//The 30th: February is shortened, March is the 30th again.
			MaintItem maintItem30=Interval(1,EnumMaintIntervalUnit.Months,D(2027,1,30));
			CollectionAssert.AreEqual(new[] { D(2027,1,30),D(2027,2,28),D(2027,3,30) },FirstOccurrences(maintItem30,3));
			//Every 3 months.
			MaintItem maintItemQuarter=Interval(3,EnumMaintIntervalUnit.Months,D(2026,11,30));
			CollectionAssert.AreEqual(new[] { D(2026,11,30),D(2027,2,28),D(2027,5,30),D(2027,8,30) },FirstOccurrences(maintItemQuarter,4));
			//Asking from a month end that the occurrence shortened.
			Assert.AreEqual(D(2027,3,31),MaintSchedules.GetFirstOccurrenceAfter(maintItem,D(2027,2,28)));
			Assert.AreEqual(D(2027,2,28),MaintSchedules.GetFirstOccurrenceOnOrAfter(maintItem,D(2027,2,1)));
		}

		[TestMethod]
		public void EveryNYears_Feb29AnchorIsFeb28InOtherYears() {
			MaintItem maintItem=Interval(1,EnumMaintIntervalUnit.Years,D(2028,2,29));
			CollectionAssert.AreEqual(new[] { D(2028,2,29),D(2029,2,28),D(2030,2,28),D(2031,2,28),D(2032,2,29) },FirstOccurrences(maintItem,5));
			MaintItem maintItemTwo=Interval(2,EnumMaintIntervalUnit.Years,D(2026,6,15));
			CollectionAssert.AreEqual(new[] { D(2026,6,15),D(2028,6,15),D(2030,6,15) },FirstOccurrences(maintItemTwo,3));
		}

		[TestMethod]
		public void AddUnits_ClampsMonthEndsAndReportsOverflow() {
			Assert.AreEqual(D(2027,2,28),MaintSchedules.AddUnits(D(2027,1,31),EnumMaintIntervalUnit.Months,1));
			Assert.AreEqual(D(2028,2,28),MaintSchedules.AddUnits(D(2027,2,28),EnumMaintIntervalUnit.Years,1),"February 28 stays the 28th in a leap year.");
			Assert.AreEqual(D(2029,2,28),MaintSchedules.AddUnits(D(2028,2,29),EnumMaintIntervalUnit.Years,1));
			Assert.AreEqual(D(2032,2,29),MaintSchedules.AddUnits(D(2028,2,29),EnumMaintIntervalUnit.Years,4));
			Assert.AreEqual(D(2026,11,16),MaintSchedules.AddUnits(D(2026,11,2),EnumMaintIntervalUnit.Weeks,2));
			Assert.AreEqual(DateTime.MinValue,MaintSchedules.AddUnits(D(9999,6,1),EnumMaintIntervalUnit.Years,5),"Past year 9999 there is no date.");
		}
		#endregion Every unit

		#region Weekdays
		[TestMethod]
		public void Weekdays_OnTheCheckedDaysFromTheAnchor() {
			//2026-11-02 is a Monday.
			Assert.AreEqual(DayOfWeek.Monday,D(2026,11,2).DayOfWeek);
			MaintItem maintItem=Weekdays(D(2026,11,2),DayOfWeek.Monday,DayOfWeek.Wednesday,DayOfWeek.Friday);
			CollectionAssert.AreEqual(new[] { D(2026,11,2),D(2026,11,4),D(2026,11,6),D(2026,11,9) },FirstOccurrences(maintItem,4));
			//An anchor on a Tuesday starts on the Wednesday.
			MaintItem maintItemTuesday=Weekdays(D(2026,11,3),DayOfWeek.Monday,DayOfWeek.Wednesday);
			Assert.AreEqual(D(2026,11,4),MaintSchedules.GetDueDate(maintItemTuesday,new List<MaintRecord>()));
			//Sunday is bit 1, Saturday bit 64.
			Assert.AreEqual(1,MaintSchedules.GetWeekdayBit(DayOfWeek.Sunday));
			Assert.AreEqual(64,MaintSchedules.GetWeekdayBit(DayOfWeek.Saturday));
		}

		[TestMethod]
		public void Weekdays_DueMeansDueThatDay_AndARecordClosesEverythingUpToTheDayDone() {
			MaintItem maintItem=Weekdays(D(2026,11,2),DayOfWeek.Monday,DayOfWeek.Wednesday,DayOfWeek.Friday);
			Assert.AreEqual(D(2026,11,2),MaintSchedules.GetDueDate(maintItem,new List<MaintRecord>()));
			//Not recorded on Monday: overdue on Tuesday.
			Assert.AreEqual(EnumMaintStatus.Overdue,MaintSchedules.GetStatus(maintItem,D(2026,11,2),D(2026,11,3)));
			Assert.AreEqual(EnumMaintStatus.DueSoon,MaintSchedules.GetStatus(maintItem,D(2026,11,2),D(2026,11,2)),"Due today.");
			//Recorded late on Tuesday against Monday: the next due date is Wednesday.
			Assert.AreEqual(D(2026,11,4),MaintSchedules.GetDueDate(maintItem,new List<MaintRecord> { Done(D(2026,11,3),D(2026,11,2)) }));
			//Recorded on Thursday against Monday (Wednesday missed too): Friday.
			Assert.AreEqual(D(2026,11,6),MaintSchedules.GetDueDate(maintItem,new List<MaintRecord> { Done(D(2026,11,5),D(2026,11,2)) }));
			//Recorded on Friday: next Monday.
			Assert.AreEqual(D(2026,11,9),MaintSchedules.GetDueDate(maintItem,new List<MaintRecord> { Done(D(2026,11,6),D(2026,11,6)) }));
		}

		[TestMethod]
		public void Weekdays_NoneCheckedGivesNoDate() {
			MaintItem maintItem=Weekdays(D(2026,11,2));
			Assert.AreEqual(DateTime.MinValue,MaintSchedules.GetDueDate(maintItem,new List<MaintRecord>()));
			Assert.AreEqual(EnumMaintStatus.NoSchedule,MaintSchedules.GetStatus(maintItem,DateTime.MinValue,D(2026,11,2)));
			Assert.IsNotNull(MaintSchedules.GetItemError(maintItem));
		}
		#endregion Weekdays

		#region Due date with completions
		[TestMethod]
		public void Interval_AnchorIsTheFirstDueDate_InThePastAndInTheFuture() {
			//An anchor in the past, never done: due on the anchor, overdue since then.
			MaintItem maintItemPast=Interval(1,EnumMaintIntervalUnit.Months,D(2026,8,1));
			Assert.AreEqual(D(2026,8,1),MaintSchedules.GetDueDate(maintItemPast,new List<MaintRecord>()));
			Assert.AreEqual(EnumMaintStatus.Overdue,MaintSchedules.GetStatus(maintItemPast,D(2026,8,1),D(2026,11,2)));
			//An anchor in the future: due on the anchor, upcoming.
			MaintItem maintItemFuture=Interval(1,EnumMaintIntervalUnit.Years,D(2027,3,15));
			maintItemFuture.LeadDays=14;
			Assert.AreEqual(D(2027,3,15),MaintSchedules.GetDueDate(maintItemFuture,new List<MaintRecord>()));
			Assert.AreEqual(EnumMaintStatus.Upcoming,MaintSchedules.GetStatus(maintItemFuture,D(2027,3,15),D(2026,11,2)));
			//A completion recorded before the anchor (an old schedule) does not move the first due date earlier.
			Assert.AreEqual(D(2027,3,15),MaintSchedules.GetDueDate(maintItemFuture,new List<MaintRecord> { Done(D(2026,10,1),D(2026,10,1)) }));
		}

		[TestMethod]
		public void Interval_EarlyCompletionClosesTheOccurrenceItWasRecordedAgainst() {
			MaintItem maintItem=Interval(1,EnumMaintIntervalUnit.Months,D(2026,11,1));
			//Done on October 29 against November 1: due December 1.
			Assert.AreEqual(D(2026,12,1),MaintSchedules.GetDueDate(maintItem,new List<MaintRecord> { Done(D(2026,10,29),D(2026,11,1)) }));
		}

		[TestMethod]
		public void Interval_LateCompletionAlsoClosesTheOccurrencesUpToTheDayDone() {
			MaintItem maintItem=Interval(1,EnumMaintIntervalUnit.Months,D(2026,11,1));
			//Not done until December 15, recorded against November 1: December 1 is closed too, so January 1.
			Assert.AreEqual(D(2027,1,1),MaintSchedules.GetDueDate(maintItem,new List<MaintRecord> { Done(D(2026,12,15),D(2026,11,1)) }));
			//Done on the due date itself.
			Assert.AreEqual(D(2026,12,1),MaintSchedules.GetDueDate(maintItem,new List<MaintRecord> { Done(D(2026,11,1),D(2026,11,1)) }));
			//The latest of several completions counts.
			List<MaintRecord> listRecords=new List<MaintRecord> { Done(D(2026,11,1),D(2026,11,1)),Done(D(2026,12,2),D(2026,12,1)),Done(D(2026,10,1),D(2026,10,1)) };
			Assert.AreEqual(D(2027,1,1),MaintSchedules.GetDueDate(maintItem,listRecords));
			Assert.AreEqual(D(2026,12,2),MaintSchedules.GetCoveredThrough(listRecords));
			Assert.AreEqual(D(2026,12,2),MaintSchedules.GetLastDone(listRecords));
		}

		[TestMethod]
		public void Interval_MonthEndDueDatesAfterCompletions() {
			MaintItem maintItem=Interval(1,EnumMaintIntervalUnit.Months,D(2027,1,31));
			//Recorded against January 31: February 28. Recorded against February 28 on February 28: March 31, not March 28.
			Assert.AreEqual(D(2027,2,28),MaintSchedules.GetDueDate(maintItem,new List<MaintRecord> { Done(D(2027,1,31),D(2027,1,31)) }));
			Assert.AreEqual(D(2027,3,31),MaintSchedules.GetDueDate(maintItem,new List<MaintRecord> { Done(D(2027,2,28),D(2027,2,28)) }));
		}

		[TestMethod]
		public void Interval_RecordWithoutDueDateCoversItsDayDone() {
			MaintItem maintItem=Interval(7,EnumMaintIntervalUnit.Days,D(2026,11,2));
			Assert.AreEqual(D(2026,11,16),MaintSchedules.GetDueDate(maintItem,new List<MaintRecord> { Done(D(2026,11,9),DateTime.MinValue) }));
		}

		[TestMethod]
		public void AfterCompletion_NUnitsAfterTheLastDateDone() {
			MaintItem maintItem=AfterCompletion(2,EnumMaintIntervalUnit.Weeks,D(2026,11,10));
			//Never done: the anchor.
			Assert.AreEqual(D(2026,11,10),MaintSchedules.GetDueDate(maintItem,new List<MaintRecord>()));
			//Done on November 12 (late): two weeks later, whatever the earlier due date was.
			Assert.AreEqual(D(2026,11,26),MaintSchedules.GetDueDate(maintItem,new List<MaintRecord> { Done(D(2026,11,12),D(2026,11,10)) }));
			//The latest date done counts, not the latest entry.
			List<MaintRecord> listRecords=new List<MaintRecord> { Done(D(2026,11,20),D(2026,11,10)),Done(D(2026,11,12),D(2026,11,10)) };
			Assert.AreEqual(D(2026,12,4),MaintSchedules.GetDueDate(maintItem,listRecords));
			//Days and months; a month end is clamped.
			Assert.AreEqual(D(2026,11,12),MaintSchedules.GetDueDate(AfterCompletion(10,EnumMaintIntervalUnit.Days,D(2026,1,1)),
				new List<MaintRecord> { Done(D(2026,11,2),DateTime.MinValue) }));
			Assert.AreEqual(D(2028,2,29),MaintSchedules.GetDueDate(AfterCompletion(1,EnumMaintIntervalUnit.Months,D(2028,1,1)),
				new List<MaintRecord> { Done(D(2028,1,31),DateTime.MinValue) }));
			Assert.AreEqual(D(2027,2,28),MaintSchedules.GetDueDate(AfterCompletion(3,EnumMaintIntervalUnit.Months,D(2026,1,1)),
				new List<MaintRecord> { Done(D(2026,11,30),DateTime.MinValue) }));
		}

		[TestMethod]
		public void ChangingTheSchedule_KeepsTheCompletionsCovered() {
			List<MaintRecord> listRecords=new List<MaintRecord> { Done(D(2026,11,2),D(2026,11,2)) };
			//Monthly from November 2, done on the 2nd: December 2. The office switches to every 2 weeks from the same anchor: November 16.
			Assert.AreEqual(D(2026,12,2),MaintSchedules.GetDueDate(Interval(1,EnumMaintIntervalUnit.Months,D(2026,11,2)),listRecords));
			Assert.AreEqual(D(2026,11,16),MaintSchedules.GetDueDate(Interval(2,EnumMaintIntervalUnit.Weeks,D(2026,11,2)),listRecords));
		}

		[TestMethod]
		public void NoAnchor_GivesNoDate() {
			MaintItem maintItem=Interval(1,EnumMaintIntervalUnit.Months,DateTime.MinValue);
			Assert.AreEqual(DateTime.MinValue,MaintSchedules.GetDueDate(maintItem,new List<MaintRecord>()));
			Assert.AreEqual(DateTime.MinValue,MaintSchedules.GetDueDate(AfterCompletion(1,EnumMaintIntervalUnit.Months,DateTime.MinValue),new List<MaintRecord>()));
		}
		#endregion Due date with completions

		#region Validation and text
		[TestMethod]
		public void ItemErrors() {
			MaintItem maintItem=Interval(1,EnumMaintIntervalUnit.Months,D(2026,11,2));
			Assert.IsNull(MaintSchedules.GetItemError(maintItem));
			maintItem.ItemName=" ";
			Assert.IsNotNull(MaintSchedules.GetItemError(maintItem),"A name is required.");
			maintItem.ItemName="Synthetic item";
			maintItem.IntervalCount=0;
			Assert.IsNotNull(MaintSchedules.GetItemError(maintItem));
			maintItem.IntervalCount=MaintSchedules.MAX_INTERVAL_COUNT+1;
			Assert.IsNotNull(MaintSchedules.GetItemError(maintItem));
			maintItem.IntervalCount=1;
			maintItem.LeadDays=-1;
			Assert.IsNotNull(MaintSchedules.GetItemError(maintItem));
			maintItem.LeadDays=MaintSchedules.MAX_LEAD_DAYS+1;
			Assert.IsNotNull(MaintSchedules.GetItemError(maintItem));
			maintItem.LeadDays=0;
			maintItem.UserNumAssigned=2;
			maintItem.UserGroupNumAssigned=10;
			Assert.IsNotNull(MaintSchedules.GetItemError(maintItem),"A user or a group, not both.");
			maintItem.UserGroupNumAssigned=0;
			Assert.IsNull(MaintSchedules.GetItemError(maintItem));
			maintItem.DateAnchor=DateTime.MinValue;
			Assert.IsNotNull(MaintSchedules.GetItemError(maintItem),"An anchor date is required.");
			MaintItem maintItemAfter=AfterCompletion(1,EnumMaintIntervalUnit.Years,D(2026,11,2));
			Assert.IsNotNull(MaintSchedules.GetItemError(maintItemAfter),"Years are not offered after the last completion.");
			maintItemAfter.IntervalUnit=EnumMaintIntervalUnit.Months;
			Assert.IsNull(MaintSchedules.GetItemError(maintItemAfter));
			Assert.IsNull(MaintSchedules.GetItemError(Weekdays(D(2026,11,2),DayOfWeek.Monday)),"A weekday schedule ignores the interval count.");
		}

		[TestMethod]
		public void ScheduleText() {
			Assert.AreEqual("Every month from 11/2/2026",MaintSchedules.GetScheduleText(Interval(1,EnumMaintIntervalUnit.Months,D(2026,11,2))));
			Assert.AreEqual("Every 3 days from 11/2/2026",MaintSchedules.GetScheduleText(Interval(3,EnumMaintIntervalUnit.Days,D(2026,11,2))));
			Assert.AreEqual("Mon, Wed, Fri",MaintSchedules.GetScheduleText(Weekdays(D(2026,11,2),DayOfWeek.Friday,DayOfWeek.Monday,DayOfWeek.Wednesday)));
			Assert.AreEqual("2 weeks after last done",MaintSchedules.GetScheduleText(AfterCompletion(2,EnumMaintIntervalUnit.Weeks,D(2026,11,2))));
		}
		#endregion Validation and text
	}
}
