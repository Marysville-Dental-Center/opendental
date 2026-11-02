// Routine maintenance (docs/specs/routine-maintenance.md, "Maintenance board"): the rows of the due and overdue board. No database access.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenPmsDentalBusiness {
	///<summary>One item on the maintenance board.</summary>
	public class MaintBoardRow {
		public MaintItem MaintItem;
		///<summary>DateTime.MinValue when the schedule gives no date.</summary>
		public DateTime DateDue;
		public EnumMaintStatus Status;
		///<summary>The latest completion (by date done, then by entry), or null.</summary>
		public MaintRecord MaintRecordLast;
	}

	///<summary>Works out the board: each item's due date and status, in the board's order.</summary>
	public static class MaintBoard {
		///<summary>A row per item, sorted: overdue (oldest due date first), due soon, upcoming, each by due date and then name; items without a
		///schedule, then hidden items, last. listMaintRecords may hold the completions of every item.</summary>
		public static List<MaintBoardRow> GetRows(List<MaintItem> listMaintItems,List<MaintRecord> listMaintRecords,DateTime dateToday) {
			List<MaintBoardRow> listRows=new List<MaintBoardRow>();
			Dictionary<long,List<MaintRecord>> dictRecords=(listMaintRecords??new List<MaintRecord>()).GroupBy(x => x.MaintItemNum)
				.ToDictionary(x => x.Key,x => x.ToList());
			foreach(MaintItem maintItem in listMaintItems) {
				if(!dictRecords.TryGetValue(maintItem.MaintItemNum,out List<MaintRecord> listRecordsItem)) {
					listRecordsItem=new List<MaintRecord>();
				}
				MaintBoardRow row=new MaintBoardRow();
				row.MaintItem=maintItem;
				row.DateDue=MaintSchedules.GetDueDate(maintItem,listRecordsItem);
				row.Status=MaintSchedules.GetStatus(maintItem,row.DateDue,dateToday);
				row.MaintRecordLast=listRecordsItem.OrderByDescending(x => x.DateDone).ThenByDescending(x => x.MaintRecordNum).FirstOrDefault();
				listRows.Add(row);
			}
			return listRows
				.OrderBy(x => MaintSchedules.GetSortRank(x.Status))
				.ThenBy(x => x.DateDue.Year<1880 ? DateTime.MaxValue : x.DateDue)
				.ThenBy(x => x.MaintItem.ItemName,StringComparer.CurrentCultureIgnoreCase)
				.ThenBy(x => x.MaintItem.MaintItemNum)
				.ToList();
		}
	}
}
