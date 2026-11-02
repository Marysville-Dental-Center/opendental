// Due, due soon (lead time) and overdue status, and the board's order (docs/specs/routine-maintenance.md, "Status"). No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace MaintenanceTests {
	[TestClass]
	public class StatusAndBoardTests {
		private static readonly DateTime _dateToday=new DateTime(2026,11,2);

		[TestInitialize]
		public void TestInitialize() {
			TestDb.SetCultureUs();
		}

		private static MaintItem Item(int leadDays) {
			MaintItem maintItem=ScheduleTests.Interval(1,EnumMaintIntervalUnit.Months,_dateToday);
			maintItem.LeadDays=leadDays;
			return maintItem;
		}

		[TestMethod]
		public void Status_OverdueDueSoonUpcoming() {
			MaintItem maintItem=Item(7);
			Assert.AreEqual(EnumMaintStatus.Overdue,MaintSchedules.GetStatus(maintItem,_dateToday.AddDays(-1),_dateToday));
			Assert.AreEqual(EnumMaintStatus.DueSoon,MaintSchedules.GetStatus(maintItem,_dateToday,_dateToday),"Due today is due soon.");
			Assert.AreEqual(EnumMaintStatus.DueSoon,MaintSchedules.GetStatus(maintItem,_dateToday.AddDays(7),_dateToday),"The last day of the lead time.");
			Assert.AreEqual(EnumMaintStatus.Upcoming,MaintSchedules.GetStatus(maintItem,_dateToday.AddDays(8),_dateToday));
		}

		[TestMethod]
		public void Status_ZeroLeadTimeIsDueSoonOnlyOnTheDay() {
			MaintItem maintItem=Item(0);
			Assert.AreEqual(EnumMaintStatus.DueSoon,MaintSchedules.GetStatus(maintItem,_dateToday,_dateToday));
			Assert.AreEqual(EnumMaintStatus.Upcoming,MaintSchedules.GetStatus(maintItem,_dateToday.AddDays(1),_dateToday));
			Assert.AreEqual(EnumMaintStatus.Overdue,MaintSchedules.GetStatus(maintItem,_dateToday.AddDays(-1),_dateToday));
		}

		[TestMethod]
		public void Status_HiddenAndNoSchedule() {
			MaintItem maintItem=Item(7);
			maintItem.IsHidden=true;
			Assert.AreEqual(EnumMaintStatus.Hidden,MaintSchedules.GetStatus(maintItem,_dateToday.AddDays(-30),_dateToday),"A hidden item is never overdue.");
			Assert.AreEqual(EnumMaintStatus.NoSchedule,MaintSchedules.GetStatus(Item(7),DateTime.MinValue,_dateToday));
		}

		[TestMethod]
		public void Status_TimeOfDayIsIgnored() {
			MaintItem maintItem=Item(0);
			Assert.AreEqual(EnumMaintStatus.DueSoon,MaintSchedules.GetStatus(maintItem,_dateToday,_dateToday.AddHours(23)));
			Assert.AreEqual(EnumMaintStatus.DueSoon,MaintSchedules.GetStatus(maintItem,_dateToday.AddHours(8),_dateToday.AddHours(17)));
		}

		[TestMethod]
		public void DaysText() {
			Assert.AreEqual("3 overdue",MaintSchedules.GetDaysText(_dateToday.AddDays(-3),_dateToday));
			Assert.AreEqual("Today",MaintSchedules.GetDaysText(_dateToday,_dateToday));
			Assert.AreEqual("Tomorrow",MaintSchedules.GetDaysText(_dateToday.AddDays(1),_dateToday));
			Assert.AreEqual("in 5",MaintSchedules.GetDaysText(_dateToday.AddDays(5),_dateToday));
			Assert.AreEqual("",MaintSchedules.GetDaysText(DateTime.MinValue,_dateToday));
		}

		[TestMethod]
		public void Board_SortsOverdueThenDueSoonThenUpcomingThenHidden() {
			MaintItem maintItemUpcoming=new MaintItem { MaintItemNum=1,ItemName="Upcoming",ScheduleType=EnumMaintScheduleType.Interval,IntervalCount=1,
				IntervalUnit=EnumMaintIntervalUnit.Months,DateAnchor=_dateToday.AddDays(20),LeadDays=7 };
			MaintItem maintItemOverdueOld=new MaintItem { MaintItemNum=2,ItemName="Overdue old",ScheduleType=EnumMaintScheduleType.Interval,IntervalCount=1,
				IntervalUnit=EnumMaintIntervalUnit.Years,DateAnchor=_dateToday.AddDays(-40),LeadDays=7 };
			MaintItem maintItemOverdueNew=new MaintItem { MaintItemNum=3,ItemName="Overdue new",ScheduleType=EnumMaintScheduleType.Interval,IntervalCount=1,
				IntervalUnit=EnumMaintIntervalUnit.Years,DateAnchor=_dateToday.AddDays(-2),LeadDays=7 };
			MaintItem maintItemDueSoon=new MaintItem { MaintItemNum=4,ItemName="Due soon",ScheduleType=EnumMaintScheduleType.Interval,IntervalCount=1,
				IntervalUnit=EnumMaintIntervalUnit.Years,DateAnchor=_dateToday.AddDays(3),LeadDays=7 };
			MaintItem maintItemHidden=new MaintItem { MaintItemNum=5,ItemName="Hidden",ScheduleType=EnumMaintScheduleType.Interval,IntervalCount=1,
				IntervalUnit=EnumMaintIntervalUnit.Years,DateAnchor=_dateToday.AddDays(-100),LeadDays=7,IsHidden=true };
			MaintItem maintItemNoSchedule=new MaintItem { MaintItemNum=6,ItemName="No weekday",ScheduleType=EnumMaintScheduleType.Weekdays,Weekdays=0,
				DateAnchor=_dateToday };
			//Item 2 was done once, late, so it is due again next year: upcoming.
			MaintItem maintItemDone=new MaintItem { MaintItemNum=7,ItemName="Done",ScheduleType=EnumMaintScheduleType.Interval,IntervalCount=1,
				IntervalUnit=EnumMaintIntervalUnit.Years,DateAnchor=_dateToday.AddDays(-10),LeadDays=7 };
			List<MaintRecord> listRecords=new List<MaintRecord> {
				new MaintRecord { MaintRecordNum=1,MaintItemNum=7,DateDone=_dateToday.AddDays(-1),DateDue=_dateToday.AddDays(-10),UserNum=2 },
				new MaintRecord { MaintRecordNum=2,MaintItemNum=7,DateDone=_dateToday.AddDays(-400),DateDue=DateTime.MinValue,UserNum=3 },
			};
			List<MaintBoardRow> listRows=MaintBoard.GetRows(new List<MaintItem> { maintItemUpcoming,maintItemOverdueOld,maintItemOverdueNew,maintItemDueSoon,
				maintItemHidden,maintItemNoSchedule,maintItemDone },listRecords,_dateToday);
			CollectionAssert.AreEqual(new[] { "Overdue old","Overdue new","Due soon","Upcoming","Done","No weekday","Hidden" },
				listRows.Select(x => x.MaintItem.ItemName).ToArray());
			MaintBoardRow rowDone=listRows.Single(x => x.MaintItem.MaintItemNum==7);
			Assert.AreEqual(EnumMaintStatus.Upcoming,rowDone.Status);
			Assert.AreEqual(_dateToday.AddDays(-10).AddYears(1),rowDone.DateDue);
			Assert.AreEqual(1,rowDone.MaintRecordLast.MaintRecordNum,"The latest completion by date done.");
			Assert.AreEqual(EnumMaintStatus.Overdue,listRows[0].Status);
			Assert.AreEqual(EnumMaintStatus.DueSoon,listRows[2].Status);
			Assert.IsNull(listRows[0].MaintRecordLast);
		}

		[TestMethod]
		public void Board_IsAssignedTo_UserOrGroupMember() {
			Userod userod=new Userod { UserNum=2 };
			Assert.IsTrue(MaintItems.IsAssignedTo(new MaintItem { UserNumAssigned=2 },userod));
			Assert.IsFalse(MaintItems.IsAssignedTo(new MaintItem { UserNumAssigned=3 },userod));
			Assert.IsFalse(MaintItems.IsAssignedTo(new MaintItem(),userod),"Unassigned items are nobody's.");
			Assert.IsFalse(MaintItems.IsAssignedTo(new MaintItem { UserNumAssigned=2 },null));
		}
	}
}
