// Routine maintenance (docs/specs/routine-maintenance.md): maintenance items.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OpenPmsDentalBusiness {
	///<summary>Maintenance items the office defines. Not cached: the board reads them when it opens and refreshes.</summary>
	public class MaintItems {
		///<summary>The reminder lead time of a new item, in days.</summary>
		public const int LEAD_DAYS_DEFAULT=7;

		#region Get Methods
		///<summary>Every item, hidden ones only when includeHidden, ordered by name.</summary>
		public static List<MaintItem> GetAll(bool includeHidden) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<MaintItem>>(MethodBase.GetCurrentMethod(),includeHidden);
			}
			string command="SELECT * FROM maintitem ";
			if(!includeHidden) {
				command+="WHERE IsHidden=0 ";
			}
			command+="ORDER BY ItemName,MaintItemNum";
			return Crud.MaintItemCrud.SelectMany(command);
		}

		///<summary>Null if not found.</summary>
		public static MaintItem GetOne(long maintItemNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<MaintItem>(MethodBase.GetCurrentMethod(),maintItemNum);
			}
			return Crud.MaintItemCrud.SelectOne(maintItemNum);
		}

		///<summary>True when at least one completion of the item is recorded. Such an item can be hidden but not deleted.</summary>
		public static bool HasRecords(long maintItemNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetBool(MethodBase.GetCurrentMethod(),maintItemNum);
			}
			string command="SELECT COUNT(*) FROM maintrecord WHERE MaintItemNum="+POut.Long(maintItemNum);
			return Db.GetCount(command)!="0";
		}
		#endregion Get Methods

		#region Modification Methods
		///<summary>Inserts the item. SecUserNumEntry is the logged-on user.</summary>
		public static long Insert(MaintItem maintItem) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				maintItem.MaintItemNum=Meth.GetLong(MethodBase.GetCurrentMethod(),maintItem);
				return maintItem.MaintItemNum;
			}
			//Security.CurUser.UserNum gets set on MT by the DtoProcessor so it matches the user from the client WS.
			maintItem.SecUserNumEntry=Security.CurUser?.UserNum??0;
			return Crud.MaintItemCrud.Insert(maintItem);
		}

		///<summary>Updates the columns that changed. Returns true if anything changed.</summary>
		public static bool Update(MaintItem maintItem,MaintItem maintItemOld) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetBool(MethodBase.GetCurrentMethod(),maintItem,maintItemOld);
			}
			return Crud.MaintItemCrud.Update(maintItem,maintItemOld);
		}

		///<summary>Deletes an item that has no recorded completion, with its checklist steps and result fields. Throws if a completion is recorded:
		///such an item can only be hidden, so its history is kept.</summary>
		public static void Delete(long maintItemNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),maintItemNum);
				return;
			}
			if(HasRecords(maintItemNum)) {
				throw new ApplicationException(Lans.g("MaintItems","This item has recorded completions. Hide it instead, so its history is kept."));
			}
			Db.NonQ("DELETE FROM maintstep WHERE MaintItemNum="+POut.Long(maintItemNum));
			Db.NonQ("DELETE FROM maintfield WHERE MaintItemNum="+POut.Long(maintItemNum));
			Crud.MaintItemCrud.Delete(maintItemNum);
		}
		#endregion Modification Methods

		#region Misc Methods
		///<summary>A new, unsaved item with the setup window's defaults: every 1 month, a reminder lead time of 7 days, nobody assigned, not hidden,
		///and the anchor, which is the first due date, today. dateToday is the server's date (MaintAlerts.GetToday), so the new item is due today
		///unless the office picks another first due date.</summary>
		public static MaintItem CreateNew(DateTime dateToday) {
			Meth.NoCheckMiddleTierRole();
			MaintItem maintItem=new MaintItem();
			maintItem.ItemName="";
			maintItem.Description="";
			maintItem.ScheduleType=EnumMaintScheduleType.Interval;
			maintItem.IntervalCount=1;
			maintItem.IntervalUnit=EnumMaintIntervalUnit.Months;
			maintItem.DateAnchor=dateToday.Date;
			maintItem.LeadDays=LEAD_DAYS_DEFAULT;
			return maintItem;
		}

		///<summary>What changed between the two versions of an item, for the audit trail, for example "name, schedule, assignee". Empty when
		///nothing that is shown changed.</summary>
		public static string DescribeChanges(MaintItem maintItem,MaintItem maintItemOld) {
			Meth.NoCheckMiddleTierRole();
			List<string> listChanges=new List<string>();
			if(maintItem.ItemName!=maintItemOld.ItemName) {
				listChanges.Add(Lans.g("MaintItems","name was")+" '"+maintItemOld.ItemName+"'");
			}
			if(maintItem.Description!=maintItemOld.Description) {
				listChanges.Add(Lans.g("MaintItems","description"));
			}
			if(maintItem.EquipmentNum!=maintItemOld.EquipmentNum) {
				listChanges.Add(Lans.g("MaintItems","equipment"));
			}
			if(maintItem.ScheduleType!=maintItemOld.ScheduleType || maintItem.IntervalCount!=maintItemOld.IntervalCount
				|| maintItem.IntervalUnit!=maintItemOld.IntervalUnit || maintItem.Weekdays!=maintItemOld.Weekdays
				|| maintItem.DateAnchor.Date!=maintItemOld.DateAnchor.Date)
			{
				listChanges.Add(Lans.g("MaintItems","schedule was")+" '"+MaintSchedules.GetScheduleText(maintItemOld)+"'");
			}
			if(maintItem.LeadDays!=maintItemOld.LeadDays) {
				listChanges.Add(Lans.g("MaintItems","lead time was")+" "+maintItemOld.LeadDays);
			}
			if(maintItem.UserNumAssigned!=maintItemOld.UserNumAssigned || maintItem.UserGroupNumAssigned!=maintItemOld.UserGroupNumAssigned) {
				listChanges.Add(Lans.g("MaintItems","assignee"));
			}
			return string.Join(", ",listChanges);
		}

		///<summary>True when the item is assigned to the user directly or to one of the user's groups.</summary>
		public static bool IsAssignedTo(MaintItem maintItem,Userod userod) {
			Meth.NoCheckMiddleTierRole();
			if(userod==null) {
				return false;
			}
			if(maintItem.UserNumAssigned!=0) {
				return maintItem.UserNumAssigned==userod.UserNum;
			}
			if(maintItem.UserGroupNumAssigned!=0) {
				return userod.IsInUserGroup(maintItem.UserGroupNumAssigned);
			}
			return false;
		}

		///<summary>The name of the assignee: the user's name, the user group's description, or "" when nobody is assigned.</summary>
		public static string GetAssigneeText(MaintItem maintItem) {
			Meth.NoCheckMiddleTierRole();
			if(maintItem.UserNumAssigned!=0) {
				return Userods.GetName(maintItem.UserNumAssigned);
			}
			if(maintItem.UserGroupNumAssigned!=0) {
				UserGroup userGroup=UserGroups.GetGroup(maintItem.UserGroupNumAssigned);
				return userGroup==null ? "" : userGroup.Description;
			}
			return "";
		}
		#endregion Misc Methods
	}
}
