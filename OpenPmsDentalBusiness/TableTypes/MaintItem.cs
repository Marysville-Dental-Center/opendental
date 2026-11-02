// Routine maintenance (docs/specs/routine-maintenance.md): a maintenance item the office defines. Fork-only table, not Open Dental schema.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.ComponentModel;

namespace OpenPmsDentalBusiness {
	///<summary>One maintenance item: any piece of equipment, room, system or compliance routine the office maintains on a schedule. The office
	///defines every item itself; the list starts empty. Its checklist is in maintstep, its result fields in maintfield and each time it is done in
	///maintrecord. Hidden (retired) items keep their history.</summary>
	[Serializable]
	public class MaintItem:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long MaintItemNum;
		///<summary>Short name shown on the board. Required.</summary>
		public string ItemName;
		///<summary>What to do, where, which supplies.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.IsText | CrudSpecialColType.CleanText)]
		public string Description;
		///<summary>FK to equipment.EquipmentNum. 0 when the item is not linked to an asset of the Equipment list.</summary>
		public long EquipmentNum;
		///<summary>Enum:EnumMaintScheduleType How the due dates are worked out.</summary>
		public EnumMaintScheduleType ScheduleType;
		///<summary>N of "every N units" (Interval) or "N units after the last completion" (AfterCompletion). 1 or more. Not used by Weekdays.</summary>
		public int IntervalCount;
		///<summary>Enum:EnumMaintIntervalUnit The unit of IntervalCount. Years only for Interval.</summary>
		public EnumMaintIntervalUnit IntervalUnit;
		///<summary>Weekdays schedule: one bit per day, bit (int)DayOfWeek (Sunday 1, Monday 2 ... Saturday 64). 0 for the other schedules.</summary>
		public int Weekdays;
		///<summary>The first due date (Interval, AfterCompletion) or the first day the weekday schedule applies (Weekdays).</summary>
		public DateTime DateAnchor;
		///<summary>Reminder lead time in days (0 to 365): the item is due soon from this many days before its due date.</summary>
		public int LeadDays;
		///<summary>FK to userod.UserNum. The user who does the task. 0 when assigned to a user group or to nobody.</summary>
		public long UserNumAssigned;
		///<summary>FK to usergroup.UserGroupNum. A user group as the role that does the task. 0 when assigned to a user or to nobody.</summary>
		public long UserGroupNumAssigned;
		///<summary>Retired: off the board and no alerts. Completions are kept.</summary>
		public bool IsHidden;
		///<summary>FK to userod.UserNum. The user who added the item. Never updated.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.ExcludeFromUpdate)]
		public long SecUserNumEntry;
		///<summary>When the item was added. Set by the server.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.DateTEntry)]
		public DateTime SecDateTEntry;
		///<summary>Last change. Kept by the server.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.TimeStamp)]
		public DateTime SecDateTEdit;

		///<summary></summary>
		public MaintItem Copy() {
			return (MaintItem)this.MemberwiseClone();
		}
	}

	///<summary>How a maintenance item's due dates are worked out. Stored in maintitem.ScheduleType.</summary>
	public enum EnumMaintScheduleType {
		///<summary>0 - Every N days, weeks, months or years from the anchor date.</summary>
		[Description("Every")]
		Interval,
		///<summary>1 - On the checked weekdays, from the anchor date.</summary>
		[Description("On weekdays")]
		Weekdays,
		///<summary>2 - N days, weeks or months after the last completion; the anchor date until the first completion.</summary>
		[Description("After last done")]
		AfterCompletion,
	}

	///<summary>The unit of maintitem.IntervalCount.</summary>
	public enum EnumMaintIntervalUnit {
		///<summary>0</summary>
		Days,
		///<summary>1</summary>
		Weeks,
		///<summary>2</summary>
		Months,
		///<summary>3 - Interval schedules only.</summary>
		Years,
	}
}
