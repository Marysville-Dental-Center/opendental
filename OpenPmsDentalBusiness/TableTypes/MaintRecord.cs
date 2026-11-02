// Routine maintenance (docs/specs/routine-maintenance.md): one recorded completion of a maintenance item. Fork-only table, not Open Dental schema.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace OpenPmsDentalBusiness {
	///<summary>One completion of a maintenance item: who did it (the logged-on user, who records it; there is no second sign-off), when, which due
	///date it closes, and a note. Its checklist ticks and results are in maintrecordvalue. Never edited or deleted.</summary>
	[Serializable]
	public class MaintRecord:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long MaintRecordNum;
		///<summary>FK to maintitem.MaintItemNum.</summary>
		public long MaintItemNum;
		///<summary>The day the task was done. Never later than the day it was recorded.</summary>
		public DateTime DateDone;
		///<summary>The due date this completion closes: the item's due date when it was recorded. MinValue when the item had none.</summary>
		public DateTime DateDue;
		///<summary>FK to userod.UserNum. Who did the task and recorded it. Set by the program from the logged-on user.</summary>
		public long UserNum;
		///<summary>Free text.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.IsText | CrudSpecialColType.CleanText)]
		public string Note;
		///<summary>A result was Fail or a number was outside its limits.</summary>
		public bool IsFlagged;
		///<summary>When it was recorded. Set by the server.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.DateTEntry)]
		public DateTime SecDateTEntry;

		///<summary></summary>
		public MaintRecord Copy() {
			return (MaintRecord)this.MemberwiseClone();
		}
	}
}
