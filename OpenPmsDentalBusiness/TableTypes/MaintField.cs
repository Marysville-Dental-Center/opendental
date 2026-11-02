// Routine maintenance (docs/specs/routine-maintenance.md): one result field of a maintenance item. Fork-only table, not Open Dental schema.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.ComponentModel;

namespace OpenPmsDentalBusiness {
	///<summary>One result field of a maintenance item, filled in when a completion is recorded: pass/fail, a number with a unit and optional limits,
	///text, a date or an attachment. maintrecordvalue keeps a copy of the name and unit with each value.</summary>
	[Serializable]
	public class MaintField:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long MaintFieldNum;
		///<summary>FK to maintitem.MaintItemNum.</summary>
		public long MaintItemNum;
		///<summary>0-based position among the item's fields.</summary>
		public int ItemOrder;
		///<summary>Label, for example "Chamber temperature". Required.</summary>
		public string FieldName;
		///<summary>Enum:EnumMaintFieldType PassFail, Number, Text, Date or Attachment (never ChecklistStep).</summary>
		public EnumMaintFieldType FieldType;
		///<summary>Number fields: the unit shown next to the value, for example "°C".</summary>
		public string Unit;
		///<summary>Number fields: ValueMin applies.</summary>
		public bool HasMin;
		///<summary>Number fields: the lower limit (inclusive). A lower value is out of range and flagged.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.Double,DecimalPlaces=6)]
		public double ValueMin;
		///<summary>Number fields: ValueMax applies.</summary>
		public bool HasMax;
		///<summary>Number fields: the upper limit (inclusive). A higher value is out of range and flagged.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.Double,DecimalPlaces=6)]
		public double ValueMax;
		///<summary>A completion cannot be recorded without a value for this field.</summary>
		public bool IsRequired;

		///<summary></summary>
		public MaintField Copy() {
			return (MaintField)this.MemberwiseClone();
		}
	}

	///<summary>The kind of a maintenance result field (maintfield.FieldType) or of a recorded value (maintrecordvalue.FieldType).</summary>
	public enum EnumMaintFieldType {
		///<summary>0 - Pass or Fail. Fail is flagged.</summary>
		[Description("Pass/Fail")]
		PassFail,
		///<summary>1 - A number, with a unit and optional limits.</summary>
		[Description("Number")]
		Number,
		///<summary>2 - Free text.</summary>
		[Description("Text")]
		Text,
		///<summary>3 - A date.</summary>
		[Description("Date")]
		Date,
		///<summary>4 - A file stored in the images folder under Maintenance.</summary>
		[Description("Attachment")]
		Attachment,
		///<summary>5 - Only in maintrecordvalue: a checklist step, ticked or not. Never a field type.</summary>
		[Description("Checklist Step")]
		ChecklistStep,
	}
}
