// The maintenance alert plan without a database (docs/specs/routine-maintenance.md, "Alerts"): who gets which alert, and what a sync changes.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace MaintenanceTests {
	[TestClass]
	public class AlertPlanTests {
		private static readonly DateTime _dateToday=new DateTime(2026,11,2);

		[TestInitialize]
		public void TestInitialize() {
			TestDb.SetCultureUs();
		}

		private static MaintItem Item(long maintItemNum,DateTime dateAnchor,int leadDays,long userNum=0,long userGroupNum=0) {
			return new MaintItem { MaintItemNum=maintItemNum,ItemName="Item "+maintItemNum,ScheduleType=EnumMaintScheduleType.Interval,IntervalCount=1,
				IntervalUnit=EnumMaintIntervalUnit.Years,DateAnchor=dateAnchor,LeadDays=leadDays,UserNumAssigned=userNum,UserGroupNumAssigned=userGroupNum };
		}

		private static List<long> Recipients(MaintItem maintItem) {
			if(maintItem.UserNumAssigned!=0) {
				return new List<long> { maintItem.UserNumAssigned };
			}
			if(maintItem.UserGroupNumAssigned==10) {
				return new List<long> { 2,3,2 };//A duplicate on purpose: each user gets one alert.
			}
			return new List<long>();
		}

		private static List<AlertItem> Wanted(params MaintItem[] arrayItems) {
			List<MaintItem> listItems=arrayItems.ToList();
			Dictionary<long,DateTime> dictDue=MaintAlerts.GetDueDates(listItems,new List<MaintRecord>());
			return MaintAlerts.GetWantedAlerts(listItems,dictDue,_dateToday,Recipients);
		}

		[TestMethod]
		public void Wanted_DueSoonInTheLeadTime_OverdueAfter_NoneOtherwise() {
			List<AlertItem> listAlerts=Wanted(Item(1,_dateToday.AddDays(5),7,userNum:2),Item(2,_dateToday.AddDays(-1),7,userNum:2),
				Item(3,_dateToday.AddDays(30),7,userNum:2));
			Assert.AreEqual(2,listAlerts.Count);
			AlertItem alertDueSoon=listAlerts.Single(x => x.FKey==1);
			Assert.AreEqual(AlertType.MaintenanceDueSoon,alertDueSoon.Type);
			Assert.AreEqual(SeverityType.Low,alertDueSoon.Severity);
			Assert.AreEqual("Maintenance due 11/7/2026: Item 1",alertDueSoon.Description);
			AlertItem alertOverdue=listAlerts.Single(x => x.FKey==2);
			Assert.AreEqual(AlertType.MaintenanceOverdue,alertOverdue.Type);
			Assert.AreEqual(SeverityType.High,alertOverdue.Severity);
			Assert.AreEqual("Maintenance overdue since 11/1/2026: Item 2",alertOverdue.Description);
			foreach(AlertItem alertItem in listAlerts) {
				Assert.AreEqual(2,alertItem.UserNum,"The alert carries the assignee, so it reaches only that user.");
				Assert.AreEqual(-1,alertItem.ClinicNum);
				Assert.AreEqual(ActionType.MarkAsRead|ActionType.OpenForm,alertItem.Actions,"No Delete: the alert would only come back.");
				Assert.AreEqual(FormType.FormMaintenanceBoard,alertItem.FormToOpen);
				Assert.AreEqual("",alertItem.ItemValue);
			}
		}

		[TestMethod]
		public void Wanted_GroupAssigneeGetsOneAlertPerMember_UnassignedAndHiddenNone() {
			MaintItem maintItemHidden=Item(3,_dateToday.AddDays(-1),7,userNum:2);
			maintItemHidden.IsHidden=true;
			List<AlertItem> listAlerts=Wanted(Item(1,_dateToday,7,userGroupNum:10),Item(2,_dateToday.AddDays(-1),7),maintItemHidden);
			CollectionAssert.AreEquivalent(new long[] { 2,3 },listAlerts.Select(x => x.UserNum).ToList());
			Assert.IsTrue(listAlerts.All(x => x.FKey==1 && x.Type==AlertType.MaintenanceDueSoon));
		}

		[TestMethod]
		public void Plan_InsertsMissing_KeepsExisting_DeletesUnwantedAndDuplicates() {
			List<AlertItem> listWanted=Wanted(Item(1,_dateToday.AddDays(-1),7,userGroupNum:10));
			//Nothing exists: both are inserted.
			MaintAlerts.SyncPlan syncPlan=MaintAlerts.GetSyncPlan(listWanted,new List<AlertItem>());
			Assert.AreEqual(2,syncPlan.ListInserts.Count);
			Assert.AreEqual(0,syncPlan.ListUpdates.Count);
			Assert.AreEqual(0,syncPlan.ListAlertItemNumsDeleted.Count);
			//They exist, one of them twice (two workstations synced at once), plus a due-soon alert of the same item and an alert of a recorded item.
			List<AlertItem> listExisting=listWanted.Select(x => x.Copy()).ToList();
			listExisting[0].AlertItemNum=11;
			listExisting[1].AlertItemNum=12;
			AlertItem alertDuplicate=listExisting[0].Copy();
			alertDuplicate.AlertItemNum=13;
			listExisting.Add(alertDuplicate);
			AlertItem alertDueSoonOld=listExisting[1].Copy();
			alertDueSoonOld.AlertItemNum=14;
			alertDueSoonOld.Type=AlertType.MaintenanceDueSoon;
			listExisting.Add(alertDueSoonOld);
			AlertItem alertRecorded=listExisting[1].Copy();
			alertRecorded.AlertItemNum=15;
			alertRecorded.FKey=99;
			listExisting.Add(alertRecorded);
			syncPlan=MaintAlerts.GetSyncPlan(listWanted,listExisting);
			Assert.AreEqual(0,syncPlan.ListInserts.Count);
			Assert.AreEqual(0,syncPlan.ListUpdates.Count,"An unchanged alert is left alone, so its read mark survives.");
			CollectionAssert.AreEquivalent(new long[] { 13,14,15 },syncPlan.ListAlertItemNumsDeleted);
			Assert.IsTrue(syncPlan.IsChanging);
			//After those changes a second plan changes nothing.
			List<AlertItem> listAfter=listExisting.Where(x => !syncPlan.ListAlertItemNumsDeleted.Contains(x.AlertItemNum)).ToList();
			Assert.IsFalse(MaintAlerts.GetSyncPlan(listWanted,listAfter).IsChanging);
		}

		[TestMethod]
		public void Plan_UpdatesTheTextInPlace() {
			List<AlertItem> listWanted=Wanted(Item(1,_dateToday.AddDays(3),7,userNum:2));
			AlertItem alertExisting=listWanted[0].Copy();
			alertExisting.AlertItemNum=21;
			alertExisting.Description="Maintenance due 10/1/2026: Old name";
			MaintAlerts.SyncPlan syncPlan=MaintAlerts.GetSyncPlan(listWanted,new List<AlertItem> { alertExisting });
			Assert.AreEqual(1,syncPlan.ListUpdates.Count);
			Assert.AreEqual(21,syncPlan.ListUpdates[0].AlertItemNum,"The same row, so its read mark survives.");
			Assert.AreEqual(listWanted[0].Description,syncPlan.ListUpdates[0].Description);
			Assert.AreEqual(0,syncPlan.ListInserts.Count);
			Assert.AreEqual(0,syncPlan.ListAlertItemNumsDeleted.Count);
		}

		[TestMethod]
		public void Plan_NothingWanted_DeletesEveryMaintenanceAlert() {
			AlertItem alertItem=MaintAlerts.CreateAlert(Item(1,_dateToday,0,userNum:2),AlertType.MaintenanceDueSoon,2,_dateToday);
			alertItem.AlertItemNum=31;
			MaintAlerts.SyncPlan syncPlan=MaintAlerts.GetSyncPlan(new List<AlertItem>(),new List<AlertItem> { alertItem });
			CollectionAssert.AreEqual(new long[] { 31 },syncPlan.ListAlertItemNumsDeleted);
		}
	}
}
