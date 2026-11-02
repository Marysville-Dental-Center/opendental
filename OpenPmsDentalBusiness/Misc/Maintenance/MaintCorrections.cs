// Routine maintenance (docs/specs/routine-maintenance.md, "Correcting a completion"): who may correct a recorded completion, what a correction
// changes and how the audit trail and the history describe it. No database access; MaintRecords.Correct saves it.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OpenPmsDentalBusiness {
	///<summary>Corrections of recorded completions. A completion is never deleted, but its checklist ticks, its results (attachments included) and
	///its note can be corrected by the user who recorded it or by a user with MaintenanceSetup. No line is added or removed, and the item, the
	///dates and who did it never change. Each changed line is checked as when recording, against the item's current field when it still exists;
	///a line whose value does not change keeps its flag. Values in the audit trail read the same on every workstation.</summary>
	public static class MaintCorrections {
		///<summary>An empty value as the audit trail writes it.</summary>
		public const string LOG_BLANK="(blank)";

		///<summary>What a correction changes (GetPlan).</summary>
		public class CorrectionPlan {
			///<summary>The completion as the correction leaves it: the stored one with the new note and IsFlagged worked out from its lines.
			///UserNumCorrected and DateTCorrected are set when it is saved (MaintRecords.Correct).</summary>
			public MaintRecord MaintRecord;
			///<summary>The lines whose value changes: copies of the stored lines with the new ValueText and IsFlagged, in the completion's order.
			///</summary>
			public List<MaintRecordValue> ListValuesChanged=new List<MaintRecordValue>();
			///<summary>The stored lines of ListValuesChanged as they are, in the same order.</summary>
			public List<MaintRecordValue> ListValuesOld=new List<MaintRecordValue>();
			public bool IsNoteChanged;
			///<summary>Each change as the audit trail writes it, in the completion's order of lines and then the note, for example
			///"Temperature: '119.5 C' to '121.5 C'" or "Photo: attachment added 20261102-101500_door.jpg".</summary>
			public List<string> ListChanges=new List<string>();
			///<summary>Why the correction cannot be saved, or null. When set, nothing else is filled in.</summary>
			public string Error;

			///<summary>True when the correction changes anything. A correction that changes nothing is neither saved nor logged.</summary>
			public bool IsChanging {
				get { return ListValuesChanged.Count>0 || IsNoteChanged; }
			}

			///<summary>True when the completion's flag changes with the correction.</summary>
			public bool IsFlagChanged(MaintRecord maintRecordOld) {
				return MaintRecord!=null && MaintRecord.IsFlagged!=maintRecordOld.IsFlagged;
			}
		}

		#region Who
		///<summary>True when the user may correct the completion: the user who recorded it (maintrecord.UserNum), or any user when hasMaintenanceSetup
		///(the user has MaintenanceSetup). False without a user or a completion.</summary>
		public static bool CanCorrect(Userod userod,MaintRecord maintRecord,bool hasMaintenanceSetup) {
			if(userod==null || maintRecord==null) {
				return false;
			}
			if(hasMaintenanceSetup) {
				return true;
			}
			return maintRecord.UserNum!=0 && maintRecord.UserNum==userod.UserNum;
		}
		#endregion Who

		#region Values
		///<summary>The item's current field of a result line (the same MaintFieldNum and the same type), which the corrected value is checked and
		///flagged against. Null for a checklist line, and for a result whose field was deleted or changed its type since.</summary>
		public static MaintField GetCurrentField(MaintRecordValue maintRecordValue,List<MaintField> listMaintFields) {
			if(maintRecordValue.FieldType==EnumMaintFieldType.ChecklistStep || maintRecordValue.MaintFieldNum==0) {
				return null;
			}
			return (listMaintFields??new List<MaintField>()).FirstOrDefault(x => x.MaintFieldNum==maintRecordValue.MaintFieldNum
				&& x.FieldType==maintRecordValue.FieldType);
		}

		///<summary>Why the corrected value of a line cannot be saved, or null when it can. A checklist line is done or not done. A result is checked as
		///when recording against its current field (required, valid for the type); a result without a current field only for its type, since its
		///setup is no longer known.</summary>
		public static string GetValueError(MaintRecordValue maintRecordValueOld,string valueTextNew,MaintField maintFieldCurrent) {
			valueTextNew=valueTextNew??"";
			if(maintRecordValueOld.FieldType==EnumMaintFieldType.ChecklistStep) {
				if(valueTextNew==MaintValues.VALUE_TRUE || valueTextNew==MaintValues.VALUE_FALSE) {
					return null;
				}
				return maintRecordValueOld.Label+": "+Lans.g("MaintCorrections","the value is not valid.");
			}
			if(maintFieldCurrent!=null) {
				return MaintValues.GetValueError(maintFieldCurrent,valueTextNew);
			}
			if(!MaintValues.IsValidForType(maintRecordValueOld.FieldType,valueTextNew)) {
				return maintRecordValueOld.Label+": "+Lans.g("MaintCorrections","the value is not valid.");
			}
			return null;
		}

		///<summary>The flag of a corrected line. A line whose value does not change keeps its flag. Otherwise: a Fail; a number outside its current
		///field's limits (a number whose field was deleted since is not flagged, because its limits are no longer known); never a checklist line or
		///any other type.</summary>
		public static bool GetFlag(MaintRecordValue maintRecordValueOld,string valueTextNew,MaintField maintFieldCurrent) {
			valueTextNew=valueTextNew??"";
			if((maintRecordValueOld.ValueText??"")==valueTextNew) {
				return maintRecordValueOld.IsFlagged;
			}
			switch(maintRecordValueOld.FieldType) {
				case EnumMaintFieldType.PassFail:
					return valueTextNew==MaintValues.VALUE_FALSE;
				case EnumMaintFieldType.Number:
					return maintFieldCurrent!=null && MaintValues.IsFlagged(maintFieldCurrent,valueTextNew);
				default:
					return false;
			}
		}
		#endregion Values

		#region Text
		///<summary>A value as the audit trail writes it, the same on every workstation whatever its culture or language: "done" or "not done", "pass"
		///or "fail", a number in invariant culture with its unit, a date as yyyy-MM-dd, the text or the file name as stored; "(blank)" when empty.
		///</summary>
		public static string GetLogText(EnumMaintFieldType fieldType,string valueText,string unit) {
			if(string.IsNullOrEmpty(valueText)) {
				return LOG_BLANK;
			}
			switch(fieldType) {
				case EnumMaintFieldType.ChecklistStep:
					return valueText==MaintValues.VALUE_TRUE ? "done" : "not done";
				case EnumMaintFieldType.PassFail:
					return valueText==MaintValues.VALUE_TRUE ? "pass" : "fail";
				case EnumMaintFieldType.Number:
					if(MaintValues.TryNumberFromDb(valueText,out double value)) {
						return value.ToString("0.######",CultureInfo.InvariantCulture)+(string.IsNullOrWhiteSpace(unit) ? "" : " "+unit.Trim());
					}
					return valueText;
				default:
					//Dates are stored as yyyy-MM-dd already; text and file names as typed.
					return valueText;
			}
		}

		///<summary>The change of one line as the audit trail writes it, or null when its value does not change: "Label: 'old' to 'new'", or for an
		///attachment "Label: attachment removed old.pdf, attachment added new.pdf" (either part alone when there was none before or none after).
		///</summary>
		public static string DescribeValueChange(MaintRecordValue maintRecordValueOld,string valueTextNew) {
			string valueTextOld=maintRecordValueOld.ValueText??"";
			valueTextNew=valueTextNew??"";
			if(valueTextOld==valueTextNew) {
				return null;
			}
			if(maintRecordValueOld.FieldType==EnumMaintFieldType.Attachment) {
				List<string> listParts=new List<string>();
				if(valueTextOld!="") {
					listParts.Add("attachment removed "+valueTextOld);
				}
				if(valueTextNew!="") {
					listParts.Add("attachment added "+valueTextNew);
				}
				return maintRecordValueOld.Label+": "+string.Join(", ",listParts);
			}
			return maintRecordValueOld.Label+": '"+GetLogText(maintRecordValueOld.FieldType,valueTextOld,maintRecordValueOld.Unit)+"' to '"
				+GetLogText(maintRecordValueOld.FieldType,valueTextNew,maintRecordValueOld.Unit)+"'";
		}

		///<summary>The change of the note as the audit trail writes it: "note: 'old' to 'new'", with "(blank)" for an empty note.</summary>
		public static string DescribeNoteChange(string noteOld,string noteNew) {
			return "note: '"+(string.IsNullOrEmpty(noteOld) ? LOG_BLANK : noteOld)+"' to '"+(string.IsNullOrEmpty(noteNew) ? LOG_BLANK : noteNew)+"'";
		}

		///<summary>The history's Corrected column: "" when the completion was never corrected, otherwise the date of the last correction and who
		///made it, in the user's culture, for example "11/3/2026 by Admin". The time is left out so that the column stays narrow enough for Results
		///(MaintHistoryGrid); the audit trail entry has it. userName is the name of maintRecord.UserNumCorrected.</summary>
		public static string GetCorrectedText(MaintRecord maintRecord,string userName) {
			if(!IsCorrected(maintRecord)) {
				return "";
			}
			string text=maintRecord.DateTCorrected.ToShortDateString();
			if(!string.IsNullOrEmpty(userName)) {
				text+=" "+Lans.g("MaintCorrections","by")+" "+userName;
			}
			return text;
		}

		///<summary>True when the completion was corrected at least once.</summary>
		public static bool IsCorrected(MaintRecord maintRecord) {
			return maintRecord!=null && (maintRecord.DateTCorrected.Year>1880 || maintRecord.UserNumCorrected!=0);
		}
		#endregion Text

		#region Plan
		///<summary>What correcting the completion changes. maintRecordOld and listValuesOld are the stored completion and its lines; noteNew is the
		///corrected note; listValuesNew holds the lines with their corrected ValueText (matched to the stored lines by MaintRecordValueNum; a stored
		///line that is not in it keeps its value; only ValueText is taken from it, never the label, unit, type or flag); listMaintFields are the
		///item's current fields. Error is set when a line of listValuesNew is not a line of the completion, or a changed value is not valid; a
		///line whose value does not change is not checked again, so a field made required since never blocks correcting another line.</summary>
		public static CorrectionPlan GetPlan(MaintRecord maintRecordOld,List<MaintRecordValue> listValuesOld,string noteNew,
			List<MaintRecordValue> listValuesNew,List<MaintField> listMaintFields)
		{
			CorrectionPlan plan=new CorrectionPlan();
			listValuesOld=listValuesOld??new List<MaintRecordValue>();
			listValuesNew=listValuesNew??new List<MaintRecordValue>();
			Dictionary<long,string> dictValueTexts=new Dictionary<long,string>();
			foreach(MaintRecordValue maintRecordValueNew in listValuesNew) {
				MaintRecordValue maintRecordValueOld=listValuesOld.FirstOrDefault(x => x.MaintRecordValueNum==maintRecordValueNew.MaintRecordValueNum);
				if(maintRecordValueOld==null || maintRecordValueOld.MaintRecordNum!=maintRecordOld.MaintRecordNum
					|| dictValueTexts.ContainsKey(maintRecordValueNew.MaintRecordValueNum))
				{
					plan.Error=Lans.g("MaintCorrections","A line of the correction is not a line of this completion.");
					return plan;
				}
				dictValueTexts[maintRecordValueNew.MaintRecordValueNum]=maintRecordValueNew.ValueText??"";
			}
			List<string> listErrors=new List<string>();
			bool isFlagged=false;
			foreach(MaintRecordValue maintRecordValueOld in listValuesOld) {
				if(!dictValueTexts.TryGetValue(maintRecordValueOld.MaintRecordValueNum,out string valueTextNew)) {
					valueTextNew=maintRecordValueOld.ValueText??"";
				}
				MaintField maintFieldCurrent=GetCurrentField(maintRecordValueOld,listMaintFields);
				bool isFlaggedLine=GetFlag(maintRecordValueOld,valueTextNew,maintFieldCurrent);
				isFlagged|=isFlaggedLine;
				string change=DescribeValueChange(maintRecordValueOld,valueTextNew);
				if(change==null) {
					continue;
				}
				string error=GetValueError(maintRecordValueOld,valueTextNew,maintFieldCurrent);
				if(error!=null) {
					listErrors.Add(error);
					continue;
				}
				MaintRecordValue maintRecordValueChanged=maintRecordValueOld.Copy();
				maintRecordValueChanged.ValueText=valueTextNew;
				maintRecordValueChanged.IsFlagged=isFlaggedLine;
				plan.ListValuesChanged.Add(maintRecordValueChanged);
				plan.ListValuesOld.Add(maintRecordValueOld.Copy());
				plan.ListChanges.Add(change);
			}
			if(listErrors.Count>0) {
				return new CorrectionPlan { Error=string.Join("\r\n",listErrors) };
			}
			noteNew=noteNew??"";
			plan.IsNoteChanged=noteNew!=(maintRecordOld.Note??"");
			if(plan.IsNoteChanged) {
				plan.ListChanges.Add(DescribeNoteChange(maintRecordOld.Note,noteNew));
			}
			plan.MaintRecord=maintRecordOld.Copy();
			plan.MaintRecord.Note=noteNew;
			plan.MaintRecord.IsFlagged=isFlagged;
			return plan;
		}
		#endregion Plan
	}
}
