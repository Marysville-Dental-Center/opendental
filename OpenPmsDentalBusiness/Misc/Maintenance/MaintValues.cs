// Routine maintenance (docs/specs/routine-maintenance.md, "Result fields and validation"): result fields and recorded values. No database access.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OpenPmsDentalBusiness {
	///<summary>Validates result fields and the values recorded for them, and turns values into the culture-independent text maintrecordvalue
	///stores and back into text for the screen. Pass/fail and checklist values are "1" or "0", numbers are invariant culture, dates yyyy-MM-dd.</summary>
	public static class MaintValues {
		public const string VALUE_TRUE="1";
		public const string VALUE_FALSE="0";
		public const string DATE_FORMAT_DB="yyyy-MM-dd";

		#region Field setup
		///<summary>The field types offered in setup, in order (ChecklistStep is never a field type).</summary>
		public static List<EnumMaintFieldType> GetFieldTypesForSetup() {
			return new List<EnumMaintFieldType> { EnumMaintFieldType.PassFail,EnumMaintFieldType.Number,EnumMaintFieldType.Text,EnumMaintFieldType.Date,
				EnumMaintFieldType.Attachment };
		}

		///<summary>Why the field cannot be saved, or null when it can: a name, a setup type, and for a number a minimum not above the maximum. Limits
		///and the unit of other types are ignored (and cleared by ClearUnusedSettings).</summary>
		public static string GetFieldError(MaintField maintField) {
			if(string.IsNullOrWhiteSpace(maintField.FieldName)) {
				return Lans.g("MaintValues","Please enter a field name.");
			}
			if(!GetFieldTypesForSetup().Contains(maintField.FieldType)) {
				return Lans.g("MaintValues","Please choose a field type.");
			}
			if(maintField.FieldType==EnumMaintFieldType.Number && maintField.HasMin && maintField.HasMax && maintField.ValueMin>maintField.ValueMax) {
				return Lans.g("MaintValues","The minimum must not be greater than the maximum.");
			}
			return null;
		}

		///<summary>Clears the unit and limits of a field that is not a number, so they are not stored for nothing.</summary>
		public static void ClearUnusedSettings(MaintField maintField) {
			if(maintField.FieldType==EnumMaintFieldType.Number) {
				return;
			}
			maintField.Unit="";
			maintField.HasMin=false;
			maintField.ValueMin=0;
			maintField.HasMax=false;
			maintField.ValueMax=0;
		}

		///<summary>The limits of a number field in words, for example "2 to 8 °C", "at least 2 °C", "at most 8 °C", or "" when it has none.</summary>
		public static string GetLimitsText(MaintField maintField) {
			if(maintField.FieldType!=EnumMaintFieldType.Number || (!maintField.HasMin && !maintField.HasMax)) {
				return "";
			}
			string unit=string.IsNullOrWhiteSpace(maintField.Unit) ? "" : " "+maintField.Unit.Trim();
			if(maintField.HasMin && maintField.HasMax) {
				return FormatNumber(maintField.ValueMin)+" "+Lans.g("MaintValues","to")+" "+FormatNumber(maintField.ValueMax)+unit;
			}
			if(maintField.HasMin) {
				return Lans.g("MaintValues","at least")+" "+FormatNumber(maintField.ValueMin)+unit;
			}
			return Lans.g("MaintValues","at most")+" "+FormatNumber(maintField.ValueMax)+unit;
		}
		#endregion Field setup

		#region Numbers and dates
		///<summary>Reads a number typed in the current culture (or invariant culture). False for anything else, including infinity and NaN.</summary>
		public static bool TryParseNumber(string text,out double value) {
			value=0;
			if(string.IsNullOrWhiteSpace(text)) {
				return false;
			}
			bool isParsed=double.TryParse(text.Trim(),NumberStyles.Float,CultureInfo.CurrentCulture,out value)
				|| double.TryParse(text.Trim(),NumberStyles.Float,CultureInfo.InvariantCulture,out value);
			return isParsed && !double.IsNaN(value) && !double.IsInfinity(value);
		}

		///<summary>The number as stored: invariant culture, round-trip precision ("4.5").</summary>
		public static string NumberToDb(double value) {
			return value.ToString("R",CultureInfo.InvariantCulture);
		}

		///<summary>A stored number. False when the text is not one.</summary>
		public static bool TryNumberFromDb(string valueText,out double value) {
			return double.TryParse(valueText??"",NumberStyles.Float,CultureInfo.InvariantCulture,out value) && !double.IsNaN(value) && !double.IsInfinity(value);
		}

		///<summary>A number for the screen in the current culture, without trailing zeros.</summary>
		public static string FormatNumber(double value) {
			return value.ToString("0.######",CultureInfo.CurrentCulture);
		}

		///<summary>The date as stored, yyyy-MM-dd.</summary>
		public static string DateToDb(DateTime date) {
			return date.ToString(DATE_FORMAT_DB,CultureInfo.InvariantCulture);
		}

		///<summary>A stored date, DateTime.MinValue when the text is not one.</summary>
		public static DateTime DateFromDb(string valueText) {
			if(DateTime.TryParseExact(valueText??"",DATE_FORMAT_DB,CultureInfo.InvariantCulture,DateTimeStyles.None,out DateTime date)) {
				return date;
			}
			return DateTime.MinValue;
		}

		///<summary>True when a number field has a limit the value is outside of. The limits are inclusive.</summary>
		public static bool IsOutOfRange(MaintField maintField,double value) {
			if(maintField.FieldType!=EnumMaintFieldType.Number) {
				return false;
			}
			if(maintField.HasMin && value<maintField.ValueMin) {
				return true;
			}
			if(maintField.HasMax && value>maintField.ValueMax) {
				return true;
			}
			return false;
		}
		#endregion Numbers and dates

		#region Recorded values
		///<summary>The stored text of a value is valid for the field's type: "1" or "0" for pass/fail, a stored number, a stored date. Empty is valid
		///(blank); whether a value is required is checked by GetValueError.</summary>
		public static bool IsValidForType(EnumMaintFieldType fieldType,string valueText) {
			if(string.IsNullOrEmpty(valueText)) {
				return true;
			}
			switch(fieldType) {
				case EnumMaintFieldType.PassFail:
				case EnumMaintFieldType.ChecklistStep:
					return valueText==VALUE_TRUE || valueText==VALUE_FALSE;
				case EnumMaintFieldType.Number:
					return TryNumberFromDb(valueText,out double _);
				case EnumMaintFieldType.Date:
					return DateFromDb(valueText).Year>1880;
				default:
					return true;
			}
		}

		///<summary>Why the value cannot be recorded for the field, or null when it can: a required field needs a value, and the value must be valid
		///for the type. An out-of-range number or a Fail is allowed (it is flagged).</summary>
		public static string GetValueError(MaintField maintField,string valueText) {
			if(string.IsNullOrWhiteSpace(valueText)) {
				if(maintField.IsRequired) {
					return maintField.FieldName+": "+Lans.g("MaintValues","a value is required.");
				}
				return null;
			}
			if(!IsValidForType(maintField.FieldType,valueText)) {
				return maintField.FieldName+": "+Lans.g("MaintValues","the value is not valid.");
			}
			return null;
		}

		///<summary>True when the value is a Fail or a number outside the field's limits.</summary>
		public static bool IsFlagged(MaintField maintField,string valueText) {
			if(string.IsNullOrEmpty(valueText)) {
				return false;
			}
			if(maintField.FieldType==EnumMaintFieldType.PassFail) {
				return valueText==VALUE_FALSE;
			}
			if(maintField.FieldType==EnumMaintFieldType.Number && TryNumberFromDb(valueText,out double value)) {
				return IsOutOfRange(maintField,value);
			}
			return false;
		}

		///<summary>The recorded value of a result field, with copies of its name and unit, flagged when it is a Fail or out of range.</summary>
		public static MaintRecordValue CreateFieldValue(MaintField maintField,string valueText) {
			MaintRecordValue maintRecordValue=new MaintRecordValue();
			maintRecordValue.MaintFieldNum=maintField.MaintFieldNum;
			maintRecordValue.FieldType=maintField.FieldType;
			maintRecordValue.Label=maintField.FieldName??"";
			maintRecordValue.Unit=maintField.FieldType==EnumMaintFieldType.Number ? (maintField.Unit??"") : "";
			maintRecordValue.ValueText=valueText??"";
			maintRecordValue.IsFlagged=IsFlagged(maintField,valueText);
			return maintRecordValue;
		}

		///<summary>The recorded line of a checklist step, with a copy of its text.</summary>
		public static MaintRecordValue CreateStepValue(MaintStep maintStep,bool isDone) {
			MaintRecordValue maintRecordValue=new MaintRecordValue();
			maintRecordValue.MaintStepNum=maintStep.MaintStepNum;
			maintRecordValue.FieldType=EnumMaintFieldType.ChecklistStep;
			maintRecordValue.Label=maintStep.Description??"";
			maintRecordValue.Unit="";
			maintRecordValue.ValueText=isDone ? VALUE_TRUE : VALUE_FALSE;
			return maintRecordValue;
		}

		///<summary>A recorded value for the screen: "Done" or "Not done", "Pass" or "Fail", the number with its unit, the date, the text, or the
		///attachment's file name.</summary>
		public static string GetDisplayText(EnumMaintFieldType fieldType,string valueText,string unit) {
			if(string.IsNullOrEmpty(valueText)) {
				return "";
			}
			switch(fieldType) {
				case EnumMaintFieldType.ChecklistStep:
					return valueText==VALUE_TRUE ? Lans.g("MaintValues","Done") : Lans.g("MaintValues","Not done");
				case EnumMaintFieldType.PassFail:
					return valueText==VALUE_TRUE ? Lans.g("MaintValues","Pass") : Lans.g("MaintValues","Fail");
				case EnumMaintFieldType.Number:
					if(TryNumberFromDb(valueText,out double value)) {
						return FormatNumber(value)+(string.IsNullOrWhiteSpace(unit) ? "" : " "+unit.Trim());
					}
					return valueText;
				case EnumMaintFieldType.Date:
					DateTime date=DateFromDb(valueText);
					return date.Year>1880 ? date.ToShortDateString() : valueText;
				default:
					return valueText;
			}
		}

		///<summary>See GetDisplayText.</summary>
		public static string GetDisplayText(MaintRecordValue maintRecordValue) {
			return GetDisplayText(maintRecordValue.FieldType,maintRecordValue.ValueText,maintRecordValue.Unit);
		}
		#endregion Recorded values
	}
}
