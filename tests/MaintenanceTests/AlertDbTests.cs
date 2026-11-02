// Maintenance alerts in the alertitem table (docs/specs/routine-maintenance.md, "Alerts"), and the audit trail entries, on the scratch database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace MaintenanceTests {
	[TestClass]
	public class AlertDbTests {
		private static readonly DateTime _dateToday=new DateTime(2026,11,2);

		[TestInitialize]
		public void TestInitialize() {
			TestDb.ResetOrSkip();
		}

		private static List<AlertItem> MaintenanceAlerts() {
			return MaintAlerts.ArrayAlertTypes.SelectMany(x => AlertItems.RefreshForType(x)).OrderBy(x => x.AlertItemNum).ToList();
		}

		private static string Describe(List<AlertItem> listAlertItems) {
			return string.Join("\r\n",listAlertItems.Select(x => x.Type+" FKey "+x.FKey+" UserNum "+x.UserNum+": "+x.Description));
		}

		[TestMethod]
		public void Sync_OneAlertPerItemAndRecipient_AndNoDuplicatesOnTheNextRun() {
			MaintItem maintItemOverdue=CrudDbTests.InsertItem("Overdue for A",_dateToday.AddDays(-3),userNum:TestDb.USER_ASSISTANT_A);
			MaintItem maintItemGroup=CrudDbTests.InsertItem("Due soon for the group",_dateToday.AddDays(2),userGroupNum:TestDb.GROUP_STERILIZATION);
			CrudDbTests.InsertItem("Overdue, nobody assigned",_dateToday.AddDays(-3));
			CrudDbTests.InsertItem("Upcoming for front desk",_dateToday.AddDays(20),userNum:TestDb.USER_FRONT);
			MaintItem maintItemHidden=CrudDbTests.InsertItem("Hidden",_dateToday.AddDays(-3),userNum:TestDb.USER_FRONT);
			MaintItem maintItemHiddenOld=maintItemHidden.Copy();
			maintItemHidden.IsHidden=true;
			MaintItems.Update(maintItemHidden,maintItemHiddenOld);
			MaintAlerts.SyncPlan syncPlan=MaintAlerts.Sync(_dateToday);
			Assert.AreEqual(3,syncPlan.ListInserts.Count);
			List<AlertItem> listAlerts=MaintenanceAlerts();
			Assert.AreEqual(3,listAlerts.Count,Describe(listAlerts));
			AlertItem alertOverdue=listAlerts.Single(x => x.FKey==maintItemOverdue.MaintItemNum);
			Assert.AreEqual(AlertType.MaintenanceOverdue,alertOverdue.Type);
			Assert.AreEqual(TestDb.USER_ASSISTANT_A,alertOverdue.UserNum);
			Assert.AreEqual(FormType.FormMaintenanceBoard,alertOverdue.FormToOpen,"127 is stored in the tinyint column and read back.");
			Assert.AreEqual(127,(int)TestDb.GetLong("SELECT FormToOpen FROM alertitem WHERE AlertItemNum="+alertOverdue.AlertItemNum));
			Assert.AreEqual(126,(int)TestDb.GetLong("SELECT Type FROM alertitem WHERE AlertItemNum="+alertOverdue.AlertItemNum));
			CollectionAssert.AreEquivalent(new[] { TestDb.USER_ASSISTANT_A,TestDb.USER_ASSISTANT_B },
				listAlerts.Where(x => x.FKey==maintItemGroup.MaintItemNum).Select(x => x.UserNum).ToList(),"Every visible member of the group; not the hidden user.");
			Assert.IsTrue(listAlerts.Where(x => x.FKey==maintItemGroup.MaintItemNum).All(x => x.Type==AlertType.MaintenanceDueSoon));
			//The next run (this or another workstation) changes nothing.
			MaintAlerts.SyncPlan syncPlanAgain=MaintAlerts.Sync(_dateToday);
			Assert.IsFalse(syncPlanAgain.IsChanging);
			CollectionAssert.AreEqual(listAlerts.Select(x => x.AlertItemNum).ToList(),MaintenanceAlerts().Select(x => x.AlertItemNum).ToList());
		}

		[TestMethod]
		public void Sync_DeletesTheExtraCopyWhenTwoWorkstationsSyncedAtOnce() {
			MaintItem maintItem=CrudDbTests.InsertItem("Overdue",_dateToday.AddDays(-1),userNum:TestDb.USER_ASSISTANT_A);
			MaintAlerts.Sync(_dateToday);
			AlertItem alertItem=MaintenanceAlerts().Single();
			AlertItem alertCopy=alertItem.Copy();
			alertCopy.AlertItemNum=0;
			AlertItems.Insert(alertCopy);
			Assert.AreEqual(2,MaintenanceAlerts().Count);
			MaintAlerts.SyncPlan syncPlan=MaintAlerts.Sync(_dateToday);
			CollectionAssert.AreEqual(new[] { alertCopy.AlertItemNum },syncPlan.ListAlertItemNumsDeleted,"The newer copy goes; the older one is kept.");
			Assert.AreEqual(alertItem.AlertItemNum,MaintenanceAlerts().Single().AlertItemNum);
		}

		[TestMethod]
		public void Sync_RecordingTheItemRemovesItsAlerts_AndTheReadMarkGoesWithThem() {
			MaintItem maintItem=CrudDbTests.InsertItem("Overdue",_dateToday.AddDays(-1),userGroupNum:TestDb.GROUP_STERILIZATION);
			MaintItem maintItemOther=CrudDbTests.InsertItem("Other",_dateToday.AddDays(-1),userNum:TestDb.USER_FRONT);
			MaintAlerts.Sync(_dateToday);
			List<AlertItem> listAlerts=MaintenanceAlerts();
			Assert.AreEqual(3,listAlerts.Count);
			AlertItem alertRead=listAlerts.First(x => x.FKey==maintItem.MaintItemNum);
			AlertReads.Insert(new AlertRead(alertRead.AlertItemNum,alertRead.UserNum));
			MaintRecords.Insert(new MaintRecord { MaintItemNum=maintItem.MaintItemNum,DateDone=_dateToday,DateDue=_dateToday.AddDays(-1),Note="" },
				new List<MaintRecordValue>());
			MaintAlerts.SyncPlan syncPlan=MaintAlerts.Sync(_dateToday);
			Assert.AreEqual(2,syncPlan.ListAlertItemNumsDeleted.Count);
			Assert.AreEqual(0,syncPlan.ListInserts.Count,"Monthly: next due a month later, upcoming.");
			List<AlertItem> listAfter=MaintenanceAlerts();
			Assert.AreEqual(maintItemOther.MaintItemNum,listAfter.Single().FKey,"Other items keep theirs.");
			Assert.AreEqual(0,TestDb.GetLong("SELECT COUNT(*) FROM alertread WHERE AlertItemNum="+alertRead.AlertItemNum));
		}

		[TestMethod]
		public void Sync_KeepsAnUnchangedAlertAndItsReadMark() {
			MaintItem maintItem=CrudDbTests.InsertItem("Due soon",_dateToday.AddDays(3),userNum:TestDb.USER_ASSISTANT_A);
			MaintAlerts.Sync(_dateToday);
			AlertItem alertItem=MaintenanceAlerts().Single();
			AlertReads.Insert(new AlertRead(alertItem.AlertItemNum,TestDb.USER_ASSISTANT_A));
			MaintAlerts.Sync(_dateToday.AddDays(1));//A day later, still due soon.
			Assert.AreEqual(alertItem.AlertItemNum,MaintenanceAlerts().Single().AlertItemNum);
			Assert.AreEqual(1,TestDb.GetLong("SELECT COUNT(*) FROM alertread WHERE AlertItemNum="+alertItem.AlertItemNum),"Read stays read.");
		}

		///<summary>What the main window's Alerts menu counts for the user (FormOpenDental.AddAlertsToMenu): the user's maintenance alerts minus the
		///ones the user has read.</summary>
		private static int UnreadCount(long userNum) {
			AlertCategoryLinks.GetTableFromCache(doRefreshCache:true);
			List<long> listAlertItemNums=AlertItems.GetAlertsItemsForUser(userNum,0).Where(x => MaintAlerts.ArrayAlertTypes.Contains(x.Type))
				.Select(x => x.AlertItemNum).ToList();
			return listAlertItemNums.Count-AlertReads.RefreshForAlertNums(userNum,listAlertItemNums).Count;
		}

		[TestMethod]
		public void Sync_TheNextOccurrenceIsANewUnreadAlert_EvenWhenItIsDueSoonAtOnce() {
			//Weekly with a lead time of 7 days (the board's default): the next occurrence is due soon as soon as this one is recorded.
			DateTime dateDue=_dateToday.AddDays(2);
			MaintItem maintItem=CrudDbTests.InsertItem("Spore test",dateDue,userNum:TestDb.USER_ASSISTANT_A,leadDays:7,
				intervalUnit:EnumMaintIntervalUnit.Weeks);
			MaintAlerts.Sync(_dateToday);
			AlertItem alertFirst=MaintenanceAlerts().Single();
			Assert.AreEqual(AlertType.MaintenanceDueSoon,alertFirst.Type);
			Assert.AreEqual("2026-11-04",alertFirst.ItemValue,"The due date, culture-independent.");
			Assert.AreEqual(1,UnreadCount(TestDb.USER_ASSISTANT_A));
			AlertReads.Insert(new AlertRead(alertFirst.AlertItemNum,TestDb.USER_ASSISTANT_A));
			Assert.AreEqual(0,UnreadCount(TestDb.USER_ASSISTANT_A),"Read.");
			//Done on time, on the due date: next due 11/11, already within the lead time.
			MaintRecords.Insert(new MaintRecord { MaintItemNum=maintItem.MaintItemNum,DateDone=dateDue,DateDue=dateDue,Note="" },new List<MaintRecordValue>());
			MaintAlerts.SyncPlan syncPlan=MaintAlerts.Sync(dateDue);
			Assert.AreEqual(1,syncPlan.ListInserts.Count);
			Assert.AreEqual(0,syncPlan.ListUpdates.Count,"Not the old row with a new text.");
			CollectionAssert.AreEqual(new[] { alertFirst.AlertItemNum },syncPlan.ListAlertItemNumsDeleted);
			AlertItem alertNext=MaintenanceAlerts().Single();
			Assert.AreNotEqual(alertFirst.AlertItemNum,alertNext.AlertItemNum,"A new alert for the new occurrence.");
			Assert.AreEqual(AlertType.MaintenanceDueSoon,alertNext.Type);
			Assert.AreEqual("2026-11-11",alertNext.ItemValue);
			Assert.AreEqual("Maintenance due 11/11/2026: Spore test",alertNext.Description);
			Assert.AreEqual(0,TestDb.GetLong("SELECT COUNT(*) FROM alertread WHERE AlertItemNum="+alertNext.AlertItemNum));
			Assert.AreEqual(0,TestDb.GetLong("SELECT COUNT(*) FROM alertread"),"The old read mark went with the old alert.");
			Assert.AreEqual(1,UnreadCount(TestDb.USER_ASSISTANT_A),"The Alerts menu shows the new reminder as unread.");
			//Later the same week, still the same occurrence: the alert and its read mark are kept.
			AlertReads.Insert(new AlertRead(alertNext.AlertItemNum,TestDb.USER_ASSISTANT_A));
			Assert.IsFalse(MaintAlerts.Sync(dateDue.AddDays(3)).IsChanging);
			Assert.AreEqual(0,UnreadCount(TestDb.USER_ASSISTANT_A));
		}

		[TestMethod]
		public void Sync_RenamingTheItemKeepsTheAlertAndItsReadMark() {
			MaintItem maintItem=CrudDbTests.InsertItem("Spore tset",_dateToday.AddDays(3),userNum:TestDb.USER_ASSISTANT_A);
			MaintAlerts.Sync(_dateToday);
			AlertItem alertItem=MaintenanceAlerts().Single();
			AlertReads.Insert(new AlertRead(alertItem.AlertItemNum,TestDb.USER_ASSISTANT_A));
			MaintItem maintItemOld=maintItem.Copy();
			maintItem.ItemName="Spore test";
			MaintItems.Update(maintItem,maintItemOld);
			MaintAlerts.SyncPlan syncPlan=MaintAlerts.Sync(_dateToday);
			Assert.AreEqual(1,syncPlan.ListUpdates.Count);
			Assert.AreEqual(0,syncPlan.ListInserts.Count+syncPlan.ListAlertItemNumsDeleted.Count);
			AlertItem alertAfter=MaintenanceAlerts().Single();
			Assert.AreEqual(alertItem.AlertItemNum,alertAfter.AlertItemNum,"Same due date: the same reminder.");
			Assert.AreEqual("Maintenance due 11/5/2026: Spore test",alertAfter.Description);
			Assert.AreEqual(0,UnreadCount(TestDb.USER_ASSISTANT_A),"A new name is not a new reminder.");
		}

		[TestMethod]
		public void Sync_DueSoonBecomesOverdue_WhenTheDueDatePasses() {
			MaintItem maintItem=CrudDbTests.InsertItem("Due in two days",_dateToday.AddDays(2),userNum:TestDb.USER_ASSISTANT_A,leadDays:7);
			//Before the lead time: nothing.
			Assert.IsFalse(MaintAlerts.Sync(_dateToday.AddDays(-10)).IsChanging);
			Assert.AreEqual(0,MaintenanceAlerts().Count);
			//It enters the lead time: due soon.
			MaintAlerts.Sync(_dateToday);
			Assert.AreEqual(AlertType.MaintenanceDueSoon,MaintenanceAlerts().Single().Type);
			//The due date passes: overdue, and the due-soon alert is gone.
			MaintAlerts.SyncPlan syncPlan=MaintAlerts.Sync(_dateToday.AddDays(3));
			Assert.AreEqual(1,syncPlan.ListInserts.Count);
			Assert.AreEqual(1,syncPlan.ListAlertItemNumsDeleted.Count);
			AlertItem alertItem=MaintenanceAlerts().Single();
			Assert.AreEqual(AlertType.MaintenanceOverdue,alertItem.Type);
			Assert.AreEqual(SeverityType.High,alertItem.Severity);
		}

		[TestMethod]
		public void Sync_ReassigningMovesTheAlert_AndHidingRemovesIt() {
			MaintItem maintItem=CrudDbTests.InsertItem("Overdue",_dateToday.AddDays(-1),userNum:TestDb.USER_ASSISTANT_A);
			MaintAlerts.Sync(_dateToday);
			MaintItem maintItemOld=maintItem.Copy();
			maintItem.UserNumAssigned=TestDb.USER_FRONT;
			MaintItems.Update(maintItem,maintItemOld);
			MaintAlerts.Sync(_dateToday);
			Assert.AreEqual(TestDb.USER_FRONT,MaintenanceAlerts().Single().UserNum);
			maintItemOld=maintItem.Copy();
			maintItem.IsHidden=true;
			MaintItems.Update(maintItem,maintItemOld);
			MaintAlerts.Sync(_dateToday);
			Assert.AreEqual(0,MaintenanceAlerts().Count);
		}

		[TestMethod]
		public void Alerts_ReachOnlyTheAssignee() {
			CrudDbTests.InsertItem("Overdue for A",_dateToday.AddDays(-1),userNum:TestDb.USER_ASSISTANT_A);
			MaintAlerts.Sync(_dateToday);
			AlertCategoryLinks.GetTableFromCache(doRefreshCache:true);
			List<AlertItem> listForA=AlertItems.GetAlertsItemsForUser(TestDb.USER_ASSISTANT_A,0);
			Assert.AreEqual(1,listForA.Count(x => x.Type==AlertType.MaintenanceOverdue),"No alert subscription needed: the alert carries the user.");
			List<AlertItem> listForFront=AlertItems.GetAlertsItemsForUser(TestDb.USER_FRONT,0);
			Assert.AreEqual(0,listForFront.Count(x => MaintAlerts.ArrayAlertTypes.Contains(x.Type)));
		}

		///<summary>Maintenance Setup for the Sterilization group (Assistant A, Assistant B and the hidden former assistant) and for Front (Front
		///Desk); Assistant A is in Front too, so they qualify twice. An Administrators group (Admin) has Security Admin but not Maintenance Setup.
		///</summary>
		private static void GrantSetup() {
			TestDb.Grant(TestDb.GROUP_STERILIZATION,EnumPermType.MaintenanceSetup);
			TestDb.Grant(TestDb.GROUP_FRONT,EnumPermType.MaintenanceSetup);
			DataCore.NonQ("INSERT INTO usergroupattach (UserNum,UserGroupNum) VALUES ("+TestDb.USER_ASSISTANT_A+","+TestDb.GROUP_FRONT+")");
			TestDb.AddGroup(30,"Administrators",TestDb.USER_ADMIN);
			TestDb.Grant(30,EnumPermType.SecurityAdmin);
			TestDb.Grant(30,EnumPermType.MaintenanceRecord);
		}

		[TestMethod]
		public void Unassigned_AlertsEveryVisibleMaintenanceSetupUser_OnceEach() {
			GrantSetup();
			CollectionAssert.AreEqual(new[] { TestDb.USER_ASSISTANT_A,TestDb.USER_ASSISTANT_B,TestDb.USER_FRONT },MaintAlerts.GetSetupUserNums(),
				"Each visible user with Maintenance Setup, once; not the hidden user, not Admin (Security Admin and Maintenance Record only).");
			MaintItem maintItemOverdue=CrudDbTests.InsertItem("Overdue, nobody assigned",_dateToday.AddDays(-3));
			MaintItem maintItemDueSoon=CrudDbTests.InsertItem("Due soon, nobody assigned",_dateToday.AddDays(2));
			CrudDbTests.InsertItem("Upcoming, nobody assigned",_dateToday.AddDays(30));
			MaintItem maintItemAssigned=CrudDbTests.InsertItem("Overdue for Admin",_dateToday.AddDays(-1),userNum:TestDb.USER_ADMIN);
			MaintAlerts.SyncPlan syncPlan=MaintAlerts.Sync(_dateToday);
			List<AlertItem> listAlerts=MaintenanceAlerts();
			Assert.AreEqual(7,listAlerts.Count,Describe(listAlerts));
			Assert.AreEqual(7,syncPlan.ListInserts.Count);
			CollectionAssert.AreEquivalent(new[] { TestDb.USER_ASSISTANT_A,TestDb.USER_ASSISTANT_B,TestDb.USER_FRONT },
				listAlerts.Where(x => x.FKey==maintItemOverdue.MaintItemNum).Select(x => x.UserNum).ToList(),"One alert per user, Assistant A once.");
			Assert.IsTrue(listAlerts.Where(x => x.FKey==maintItemOverdue.MaintItemNum).All(x => x.Type==AlertType.MaintenanceOverdue && x.Severity==SeverityType.High));
			CollectionAssert.AreEquivalent(new[] { TestDb.USER_ASSISTANT_A,TestDb.USER_ASSISTANT_B,TestDb.USER_FRONT },
				listAlerts.Where(x => x.FKey==maintItemDueSoon.MaintItemNum).Select(x => x.UserNum).ToList());
			Assert.IsTrue(listAlerts.Where(x => x.FKey==maintItemDueSoon.MaintItemNum).All(x => x.Type==AlertType.MaintenanceDueSoon),"Entering the lead time.");
			Assert.AreEqual(TestDb.USER_ADMIN,listAlerts.Single(x => x.FKey==maintItemAssigned.MaintItemNum).UserNum,
				"An assigned item still goes to its assignee only.");
			Assert.AreEqual(0,listAlerts.Count(x => x.UserNum==TestDb.USER_HIDDEN));
			//The next run (this or another workstation) changes nothing: no duplicates.
			Assert.IsFalse(MaintAlerts.Sync(_dateToday).IsChanging);
			CollectionAssert.AreEqual(listAlerts.Select(x => x.AlertItemNum).ToList(),MaintenanceAlerts().Select(x => x.AlertItemNum).ToList());
			//Each of them sees it in their own Alerts menu.
			AlertCategoryLinks.GetTableFromCache(doRefreshCache:true);
			Assert.AreEqual(2,AlertItems.GetAlertsItemsForUser(TestDb.USER_FRONT,0).Count(x => MaintAlerts.ArrayAlertTypes.Contains(x.Type)));
		}

		[TestMethod]
		public void Unassigned_TheAlertsStopWhenTheItemGetsAnAssignee() {
			GrantSetup();
			MaintItem maintItem=CrudDbTests.InsertItem("Overdue, nobody assigned",_dateToday.AddDays(-3));
			MaintAlerts.Sync(_dateToday);
			List<AlertItem> listBefore=MaintenanceAlerts();
			Assert.AreEqual(3,listBefore.Count,Describe(listBefore));
			AlertItem alertRead=listBefore.First(x => x.UserNum==TestDb.USER_FRONT);
			AlertReads.Insert(new AlertRead(alertRead.AlertItemNum,TestDb.USER_FRONT));
			//Assigned to a user: only that user's alert is left.
			MaintItem maintItemOld=maintItem.Copy();
			maintItem.UserNumAssigned=TestDb.USER_ADMIN;
			MaintItems.Update(maintItem,maintItemOld);
			MaintAlerts.SyncPlan syncPlan=MaintAlerts.Sync(_dateToday);
			Assert.AreEqual(3,syncPlan.ListAlertItemNumsDeleted.Count);
			Assert.AreEqual(1,syncPlan.ListInserts.Count);
			AlertItem alertAssigned=MaintenanceAlerts().Single();
			Assert.AreEqual(TestDb.USER_ADMIN,alertAssigned.UserNum);
			Assert.AreEqual(0,TestDb.GetLong("SELECT COUNT(*) FROM alertread"),"The read marks went with the Maintenance Setup users' alerts.");
			//Assigned to a group instead: its members, not the Maintenance Setup users who are not in it.
			maintItemOld=maintItem.Copy();
			maintItem.UserNumAssigned=0;
			maintItem.UserGroupNumAssigned=30;
			MaintItems.Update(maintItem,maintItemOld);
			MaintAlerts.Sync(_dateToday);
			Assert.AreEqual(TestDb.USER_ADMIN,MaintenanceAlerts().Single().UserNum);
			//Nobody again: back to the Maintenance Setup users.
			maintItemOld=maintItem.Copy();
			maintItem.UserGroupNumAssigned=0;
			MaintItems.Update(maintItem,maintItemOld);
			MaintAlerts.Sync(_dateToday);
			CollectionAssert.AreEquivalent(new[] { TestDb.USER_ASSISTANT_A,TestDb.USER_ASSISTANT_B,TestDb.USER_FRONT },
				MaintenanceAlerts().Select(x => x.UserNum).ToList());
		}

		[TestMethod]
		public void Unassigned_NobodyWithMaintenanceSetup_NoAlert() {
			CrudDbTests.InsertItem("Overdue, nobody assigned",_dateToday.AddDays(-3));
			Assert.AreEqual(0,MaintAlerts.GetSetupUserNums().Count);
			Assert.IsFalse(MaintAlerts.Sync(_dateToday).IsChanging,"The board still shows the item.");
			Assert.AreEqual(0,MaintenanceAlerts().Count);
		}

		[TestMethod]
		public void Sync_SkipsOtherAlertTypes() {
			AlertItems.CreateGenericAlert("A generic alert","synthetic");
			MaintAlerts.Sync(_dateToday);
			Assert.AreEqual(1,TestDb.GetLong("SELECT COUNT(*) FROM alertitem WHERE Type="+(int)AlertType.Generic),"Other alerts are never touched.");
		}

		[TestMethod]
		public void AuditTrail_SetupAndRecordingAreLogged() {
			MaintItem maintItem=CrudDbTests.InsertItem("Compressor drain",_dateToday);
			MaintAudit.LogSetup(maintItem,MaintAudit.GetTextAdded(maintItem));
			MaintRecord maintRecord=new MaintRecord { MaintItemNum=maintItem.MaintItemNum,DateDone=_dateToday,DateDue=_dateToday.AddDays(-1),IsFlagged=true };
			MaintAudit.LogRecorded(maintItem,maintRecord);
			DataTable table=DataCore.GetTable("SELECT PermType,FKey,LogText,UserNum FROM securitylog ORDER BY SecurityLogNum");
			Assert.AreEqual(2,table.Rows.Count);
			Assert.AreEqual("10000",table.Rows[0]["PermType"].ToString());
			Assert.AreEqual("Maintenance item added: Compressor drain",table.Rows[0]["LogText"].ToString());
			Assert.AreEqual(maintItem.MaintItemNum.ToString(),table.Rows[0]["FKey"].ToString());
			Assert.AreEqual("10001",table.Rows[1]["PermType"].ToString(),"smallint(6) holds the fork-only value.");
			Assert.AreEqual("Maintenance recorded: Compressor drain, done 2026-11-02, due 2026-11-01, flagged",table.Rows[1]["LogText"].ToString());
			Assert.AreEqual(TestDb.USER_ADMIN.ToString(),table.Rows[1]["UserNum"].ToString());
			Assert.AreEqual("Maintenance item edited: Compressor drain (schedule was 'Every month from 11/2/2026')",
				MaintAudit.GetTextEdited(maintItem,MaintItems.DescribeChanges(new MaintItem { ItemName=maintItem.ItemName,Description=maintItem.Description,
					ScheduleType=maintItem.ScheduleType,IntervalCount=2,IntervalUnit=maintItem.IntervalUnit,DateAnchor=maintItem.DateAnchor,LeadDays=maintItem.LeadDays },
					maintItem)));
			Assert.AreEqual("Maintenance item hidden: Compressor drain",MaintAudit.GetTextHidden(maintItem,true));
			Assert.AreEqual("Maintenance item shown again: Compressor drain",MaintAudit.GetTextHidden(maintItem,false));
		}

		[TestMethod]
		public void Attachments_NotAvailableWhenImagesAreInTheDatabase() {
			//ResetOrSkip sets every preference to 0: AtoZfolderUsed 0 is images in the database.
			Assert.AreEqual(DataStorageType.InDatabase,PrefC.AtoZfolderUsed);
			Assert.IsFalse(MaintAttachments.IsAvailable());
			Assert.IsNull(MaintAttachments.GetFolder(12));
			Assert.IsNull(MaintAttachments.GetFilePath(12,"x.pdf"));
			Assert.AreEqual(@"Maintenance\12",MaintAttachments.GetFolderRelative(12));
		}
	}
}
