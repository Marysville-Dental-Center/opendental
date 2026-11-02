// Routine maintenance (docs/specs/routine-maintenance.md): one recorded completion of a maintenance item. Fork-only table, not Open Dental schema.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace OpenPmsDentalBusiness {
	///<summary>One completion of a maintenance item: who did it (the logged-on user, who records it; there is no second sign-off), when, which due
	///date it closes, and a note. Its checklist ticks and results are in maintrecordvalue. Never deleted. Its note, ticks and results can be
	///corrected (MaintRecords.Correct) by the user who recorded it or a user with MaintenanceSetup; the item, the dates and UserNum never change.
	///</summary>
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
		///<summary>FK to userod.UserNum. Who made the last correction. 0 when it was never corrected. Set by MaintRecords.Correct.</summary>
		public long UserNumCorrected;
		///<summary>When the last correction was saved (the server's time). MinValue when it was never corrected. Set by MaintRecords.Correct.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.DateT)]
		public DateTime DateTCorrected;

		///<summary></summary>
		public MaintRecord Copy() {
			return (MaintRecord)this.MemberwiseClone();
		}
	}
}
