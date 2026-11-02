// Routine maintenance (docs/specs/routine-maintenance.md, "Alerts"): reminders through the program's alert system.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenPmsDentalBusiness {
	///<summary>Keeps the maintenance alerts (AlertType MaintenanceDueSoon and MaintenanceOverdue) equal to what the items' status calls for: one
	///alert per item, type and recipient, for the item's current due date. The recipient is the assigned user, or each user of the assigned user
	///group, or, for an item without an assignee, each user who has MaintenanceSetup; the alert carries the recipient's UserNum, so it reaches that
	///user whatever alert categories they subscribe to, and only that user. A hidden item and an item that is not due soon or overdue have none;
	///once an unassigned item gets an assignee, the alerts of the MaintenanceSetup users are no longer wanted. The due date is part of an alert's
	///identity (ItemValue, yyyy-MM-dd): a new due date (the next occurrence, once one is recorded) is a new reminder, inserted unread, and the
	///alert of the old one goes with its read marks. Sync can run on several workstations at once: the next run deletes any duplicate it finds,
	///and running it again changes nothing.</summary>
	public static class MaintAlerts {
		///<summary>The alert types this class owns.</summary>
		public static readonly AlertType[] ArrayAlertTypes={ AlertType.MaintenanceDueSoon,AlertType.MaintenanceOverdue };
		///<summary>Longest item name put into an alert description.</summary>
		private const int MAX_NAME_LENGTH=255;

		///<summary>What Sync changed.</summary>
		public class SyncPlan {
			///<summary>Wanted alerts that did not exist.</summary>
			public List<AlertItem> ListInserts=new List<AlertItem>();
			///<summary>Existing alerts kept whose due date is the same but whose text or options changed, for example a renamed item (updated in
			///place, so their read marks survive).</summary>
			public List<AlertItem> ListUpdates=new List<AlertItem>();
			///<summary>Existing alerts no longer wanted (including the alert of an earlier due date), and extra copies of a wanted one.</summary>
			public List<long> ListAlertItemNumsDeleted=new List<long>();

			public bool IsChanging {
				get { return ListInserts.Count>0 || ListUpdates.Count>0 || ListAlertItemNumsDeleted.Count>0; }
			}
		}

		#region Pure logic
		///<summary>The alert for one item, type, recipient and due date. ItemValue holds the due date as yyyy-MM-dd, the same on every workstation
		///whatever its culture or language, so that GetSyncPlan can tell a new due date from a new text. The user does not see it: the alert has no
		///ShowItemValue action.</summary>
		public static AlertItem CreateAlert(MaintItem maintItem,AlertType alertType,long userNum,DateTime dateDue) {
			AlertItem alertItem=new AlertItem();
			alertItem.Type=alertType;
			alertItem.FKey=maintItem.MaintItemNum;
			alertItem.UserNum=userNum;
			alertItem.ClinicNum=-1;//Every clinic: maintenance items belong to the whole practice.
			alertItem.Actions=ActionType.MarkAsRead | ActionType.OpenForm;//No Delete: the alert would only come back. It goes away when the item is done.
			alertItem.FormToOpen=FormType.FormMaintenanceBoard;
			alertItem.Severity=alertType==AlertType.MaintenanceOverdue ? SeverityType.High : SeverityType.Low;
			alertItem.ItemValue=MaintValues.DateToDb(dateDue);
			string name=maintItem.ItemName??"";
			if(name.Length>MAX_NAME_LENGTH) {
				name=name.Substring(0,MAX_NAME_LENGTH);
			}
			if(alertType==AlertType.MaintenanceOverdue) {
				alertItem.Description=Lans.g("MaintAlerts","Maintenance overdue since")+" "+dateDue.ToShortDateString()+": "+name;
			}
			else {
				alertItem.Description=Lans.g("MaintAlerts","Maintenance due")+" "+dateDue.ToShortDateString()+": "+name;
			}
			return alertItem;
		}

		///<summary>The alerts the items call for. dictDueDates holds each item's due date (GetDueDate); funcRecipients gives the UserNums an item's
		///alerts go to. Hidden items, items without a due date and upcoming items get none.</summary>
		public static List<AlertItem> GetWantedAlerts(List<MaintItem> listMaintItems,Dictionary<long,DateTime> dictDueDates,DateTime dateToday,
			Func<MaintItem,List<long>> funcRecipients)
		{
			List<AlertItem> listAlertItems=new List<AlertItem>();
			foreach(MaintItem maintItem in listMaintItems) {
				if(!dictDueDates.TryGetValue(maintItem.MaintItemNum,out DateTime dateDue)) {
					continue;
				}
				EnumMaintStatus status=MaintSchedules.GetStatus(maintItem,dateDue,dateToday);
				AlertType alertType;
				if(status==EnumMaintStatus.Overdue) {
					alertType=AlertType.MaintenanceOverdue;
				}
				else if(status==EnumMaintStatus.DueSoon) {
					alertType=AlertType.MaintenanceDueSoon;
				}
				else {
					continue;
				}
				foreach(long userNum in funcRecipients(maintItem).Distinct()) {
					listAlertItems.Add(CreateAlert(maintItem,alertType,userNum,dateDue));
				}
			}
			return listAlertItems;
		}

		///<summary>What to change so that the existing maintenance alerts equal the wanted ones, matched by type, item (FKey), recipient (UserNum) and
		///due date (ItemValue). A match is kept (the oldest copy) and updated only when its text or options differ, for example after a rename, so
		///its read marks survive; other copies of it are deleted; wanted alerts with no match are inserted; existing ones nobody wants are deleted.
		///So when the due date moves on to the next occurrence, the old alert is deleted with its read marks and the new one is inserted unread, even
		///when the item stays due soon. The match never looks at Description, which depends on the workstation's culture and language. Alerts of
		///other types must not be passed in.</summary>
		public static SyncPlan GetSyncPlan(List<AlertItem> listAlertItemsWanted,List<AlertItem> listAlertItemsExisting) {
			SyncPlan syncPlan=new SyncPlan();
			List<AlertItem> listExisting=listAlertItemsExisting.OrderBy(x => x.AlertItemNum).ToList();
			HashSet<long> setKept=new HashSet<long>();
			foreach(AlertItem alertItemWanted in listAlertItemsWanted) {
				List<AlertItem> listMatches=listExisting.FindAll(x => x.Type==alertItemWanted.Type && x.FKey==alertItemWanted.FKey
					&& x.UserNum==alertItemWanted.UserNum && (x.ItemValue??"")==(alertItemWanted.ItemValue??"") && !setKept.Contains(x.AlertItemNum));
				if(listMatches.Count==0) {
					syncPlan.ListInserts.Add(alertItemWanted);
					continue;
				}
				AlertItem alertItemKept=listMatches[0];
				setKept.Add(alertItemKept.AlertItemNum);
				if(!IsSame(alertItemKept,alertItemWanted)) {
					AlertItem alertItemUpdated=alertItemKept.Copy();
					alertItemUpdated.Description=alertItemWanted.Description;
					alertItemUpdated.Severity=alertItemWanted.Severity;
					alertItemUpdated.Actions=alertItemWanted.Actions;
					alertItemUpdated.FormToOpen=alertItemWanted.FormToOpen;
					alertItemUpdated.ClinicNum=alertItemWanted.ClinicNum;
					syncPlan.ListUpdates.Add(alertItemUpdated);
				}
			}
			syncPlan.ListAlertItemNumsDeleted=listExisting.Where(x => !setKept.Contains(x.AlertItemNum)).Select(x => x.AlertItemNum).ToList();
			return syncPlan;
		}

		///<summary>True when two matching alerts (same type, item, recipient and due date) also have the same text and options.</summary>
		private static bool IsSame(AlertItem alertItem,AlertItem alertItemOther) {
			return alertItem.Description==alertItemOther.Description
				&& alertItem.Severity==alertItemOther.Severity
				&& alertItem.Actions==alertItemOther.Actions
				&& alertItem.FormToOpen==alertItemOther.FormToOpen
				&& alertItem.ClinicNum==alertItemOther.ClinicNum;
		}
		#endregion Pure logic

		#region Database
		///<summary>The users an item's alerts go to: the assigned user, or every user of the assigned user group, or, when nobody is assigned, every
		///user who has MaintenanceSetup through any of their user groups (GetSetupUserNums); hidden users never, and each user once.</summary>
		public static List<long> GetRecipientUserNums(MaintItem maintItem) {
			Meth.NoCheckMiddleTierRole();
			if(maintItem.UserNumAssigned!=0) {
				Userod userod=Userods.GetUser(maintItem.UserNumAssigned);
				if(userod==null || userod.IsHidden) {
					return new List<long>();
				}
				return new List<long> { userod.UserNum };
			}
			if(maintItem.UserGroupNumAssigned!=0) {
				return Userods.GetForGroup(maintItem.UserGroupNumAssigned).Where(x => !x.IsHidden).Select(x => x.UserNum).Distinct().OrderBy(x => x).ToList();
			}
			return GetSetupUserNums();
		}

		///<summary>Every user who is not hidden and has MaintenanceSetup through any of their user groups, each once, in UserNum order: the recipients of
		///the alerts of items that nobody is assigned to. Read from the user, user group and permission caches.</summary>
		public static List<long> GetSetupUserNums() {
			Meth.NoCheckMiddleTierRole();
			return Userods.GetUsersByPermission(EnumPermType.MaintenanceSetup,false).Where(x => !x.IsHidden).Select(x => x.UserNum).Distinct().OrderBy(x => x)
				.ToList();
		}

		///<summary>The due date of each item, from its completions.</summary>
		public static Dictionary<long,DateTime> GetDueDates(List<MaintItem> listMaintItems,List<MaintRecord> listMaintRecords) {
			Meth.NoCheckMiddleTierRole();
			Dictionary<long,DateTime> dictDueDates=new Dictionary<long,DateTime>();
			foreach(MaintItem maintItem in listMaintItems) {
				dictDueDates[maintItem.MaintItemNum]=MaintSchedules.GetDueDate(maintItem,listMaintRecords.FindAll(x => x.MaintItemNum==maintItem.MaintItemNum));
			}
			return dictDueDates;
		}

		///<summary>Brings the maintenance alerts in line with every item's status as of dateToday (the server's date; see GetToday) and returns what
		///changed. Deleting an alert also deletes its read marks.</summary>
		public static SyncPlan Sync(DateTime dateToday) {
			Meth.NoCheckMiddleTierRole();
			List<MaintItem> listMaintItems=MaintItems.GetAll(includeHidden:false);
			List<MaintRecord> listMaintRecords=MaintRecords.GetForItems(listMaintItems.Select(x => x.MaintItemNum).ToList());
			Dictionary<long,DateTime> dictDueDates=GetDueDates(listMaintItems,listMaintRecords);
			List<AlertItem> listAlertItemsWanted=GetWantedAlerts(listMaintItems,dictDueDates,dateToday,GetRecipientUserNums);
			List<AlertItem> listAlertItemsExisting=new List<AlertItem>();
			foreach(AlertType alertType in ArrayAlertTypes) {
				listAlertItemsExisting.AddRange(AlertItems.RefreshForType(alertType));
			}
			SyncPlan syncPlan=GetSyncPlan(listAlertItemsWanted,listAlertItemsExisting);
			if(syncPlan.ListAlertItemNumsDeleted.Count>0) {
				AlertItems.Delete(syncPlan.ListAlertItemNumsDeleted);
			}
			foreach(AlertItem alertItem in syncPlan.ListUpdates) {
				AlertItems.Update(alertItem);
			}
			foreach(AlertItem alertItem in syncPlan.ListInserts) {
				AlertItems.Insert(alertItem);
			}
			return syncPlan;
		}

		///<summary>Today on the database server, so every workstation agrees on what is due.</summary>
		public static DateTime GetToday() {
			Meth.NoCheckMiddleTierRole();
			return MiscData.GetNowDateTime().Date;
		}
		#endregion Database
	}
}
