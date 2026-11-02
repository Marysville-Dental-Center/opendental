// Routine maintenance (docs/specs/routine-maintenance.md): one checklist tick or result of a recorded completion. Fork-only table, not Open Dental
// schema.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace OpenPmsDentalBusiness {
	///<summary>One line of a recorded completion: a checklist step (ticked or not) or a result field's value. The label and unit are copies taken
	///when it was recorded, so the history reads the same after the item's steps and fields change. Never edited or deleted.</summary>
	[Serializable]
	public class MaintRecordValue:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long MaintRecordValueNum;
		///<summary>FK to maintrecord.MaintRecordNum.</summary>
		public long MaintRecordNum;
		///<summary>FK to maintstep.MaintStepNum for a checklist line, otherwise 0. The step may have been deleted since.</summary>
		public long MaintStepNum;
		///<summary>FK to maintfield.MaintFieldNum for a result, otherwise 0. The field may have been deleted since.</summary>
		public long MaintFieldNum;
		///<summary>Checklist lines first, then results, each in the item's order.</summary>
		public int ItemOrder;
		///<summary>Enum:EnumMaintFieldType ChecklistStep for a checklist line, otherwise the field's type.</summary>
		public EnumMaintFieldType FieldType;
		///<summary>The step text or the field name, as it was when recorded.</summary>
		public string Label;
		///<summary>The unit of a Number field, as it was when recorded.</summary>
		public string Unit;
		///<summary>The value, culture-independent: "1" or "0" for a checklist step (done) and PassFail (pass), the number in invariant culture, the
		///date as yyyy-MM-dd, the text as typed, or the attachment's file name in the item's Maintenance folder. Empty when left blank.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.IsText)]
		public string ValueText;
		///<summary>Fail, or a number outside its limits.</summary>
		public bool IsFlagged;

		///<summary></summary>
		public MaintRecordValue Copy() {
			return (MaintRecordValue)this.MemberwiseClone();
		}
	}
}
