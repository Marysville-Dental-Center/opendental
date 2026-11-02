// A new maintenance item (docs/specs/routine-maintenance.md, "Item setup"; owner decision of 2026-11-02): the setup window's defaults, with the
// first due date (the anchor) today. No database access.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace MaintenanceTests {
	[TestClass]
	public class NewItemTests {
		private static readonly DateTime _dateToday=new DateTime(2026,11,2);

		[TestInitialize]
		public void TestInitialize() {
			TestDb.SetCultureUs();
		}

		[TestMethod]
		public void CreateNew_TheFirstDueDateDefaultsToToday() {
			MaintItem maintItem=MaintItems.CreateNew(_dateToday.AddHours(15).AddMinutes(30));
			Assert.AreEqual(_dateToday,maintItem.DateAnchor,"Today, without the time of day.");
			Assert.AreEqual(EnumMaintScheduleType.Interval,maintItem.ScheduleType);
			Assert.AreEqual(1,maintItem.IntervalCount);
			Assert.AreEqual(EnumMaintIntervalUnit.Months,maintItem.IntervalUnit);
			Assert.AreEqual(MaintItems.LEAD_DAYS_DEFAULT,maintItem.LeadDays);
			Assert.AreEqual(7,maintItem.LeadDays);
			Assert.AreEqual(0,maintItem.UserNumAssigned);
			Assert.AreEqual(0,maintItem.UserGroupNumAssigned);
			Assert.IsFalse(maintItem.IsHidden);
			Assert.AreEqual(0,maintItem.MaintItemNum,"Not saved.");
			Assert.AreEqual("",maintItem.ItemName);
			Assert.AreEqual("",maintItem.Description);
			Assert.IsNotNull(MaintSchedules.GetItemError(maintItem),"The office still names it.");
			maintItem.ItemName="Compressor drain";
			Assert.IsNull(MaintSchedules.GetItemError(maintItem),"Otherwise it can be saved as it is.");
		}

		[TestMethod]
		public void CreateNew_IsDueToday_AndTheAnchorStaysTheFirstDueDate() {
			MaintItem maintItem=MaintItems.CreateNew(_dateToday);
			maintItem.ItemName="Compressor drain";
			DateTime dateDue=MaintSchedules.GetDueDate(maintItem,new List<MaintRecord>());
			Assert.AreEqual(_dateToday,dateDue,"Never done: the anchor, today.");
			Assert.AreEqual(EnumMaintStatus.DueSoon,MaintSchedules.GetStatus(maintItem,dateDue,_dateToday));
			Assert.AreEqual("Today",MaintSchedules.GetDaysText(dateDue,_dateToday));
			//Done today: due a month later.
			Assert.AreEqual(new DateTime(2026,12,2),MaintSchedules.GetDueDate(maintItem,new List<MaintRecord> { ScheduleTests.Done(_dateToday,_dateToday) }));
			//An office that moves the first due date into the past makes a never-done item overdue at once; into the future, upcoming.
			maintItem.DateAnchor=_dateToday.AddDays(-10);
			Assert.AreEqual(EnumMaintStatus.Overdue,MaintSchedules.GetStatus(maintItem,MaintSchedules.GetDueDate(maintItem,new List<MaintRecord>()),_dateToday));
			maintItem.DateAnchor=_dateToday.AddDays(30);
			Assert.AreEqual(_dateToday.AddDays(30),MaintSchedules.GetDueDate(maintItem,new List<MaintRecord>()));
			Assert.AreEqual(EnumMaintStatus.Upcoming,MaintSchedules.GetStatus(maintItem,_dateToday.AddDays(30),_dateToday));
		}
	}
}
