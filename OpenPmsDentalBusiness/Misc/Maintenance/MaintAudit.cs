// Routine maintenance (docs/specs/routine-maintenance.md, "Permissions and the audit trail"): audit trail entries.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace OpenPmsDentalBusiness {
	///<summary>The audit trail (securitylog) entries of routine maintenance. Item changes are logged under MaintenanceSetup, recorded completions
	///and their corrections under MaintenanceRecord; FKey is the MaintItemNum. The user and time are the logged-on user and the server's, as for
	///every entry.</summary>
	public static class MaintAudit {
		public static string GetTextAdded(MaintItem maintItem) {
			return "Maintenance item added: "+maintItem.ItemName;
		}

		public static string GetTextEdited(MaintItem maintItem,string changes) {
			return "Maintenance item edited: "+maintItem.ItemName+(string.IsNullOrEmpty(changes) ? "" : " ("+changes+")");
		}

		public static string GetTextHidden(MaintItem maintItem,bool isHidden) {
			return (isHidden ? "Maintenance item hidden: " : "Maintenance item shown again: ")+maintItem.ItemName;
		}

		public static string GetTextDeleted(MaintItem maintItem) {
			return "Maintenance item deleted: "+maintItem.ItemName;
		}

		///<summary>"Maintenance recorded: name, done 2026-11-02, due 2026-11-01" and ", flagged" when a value was flagged. Dates in yyyy-MM-dd, so the
		///entry reads the same everywhere.</summary>
		public static string GetTextRecorded(MaintItem maintItem,MaintRecord maintRecord) {
			string text="Maintenance recorded: "+maintItem.ItemName+", done "+MaintValues.DateToDb(maintRecord.DateDone);
			if(maintRecord.DateDue.Year>1880) {
				text+=", due "+MaintValues.DateToDb(maintRecord.DateDue);
			}
			if(maintRecord.IsFlagged) {
				text+=", flagged";
			}
			return text;
		}

		///<summary>"Maintenance completion corrected: name, done 2026-11-02 (MaintRecordNum 7): " and every change of the plan, separated by "; ", for
		///example "Temperature: '119.5 C' to '121.5 C'; Spore test: 'fail' to 'pass'; note: '(blank)' to 'Re-read the gauge'", then ", now flagged"
		///or ", no longer flagged" when the completion's flag changes. itemName is the item's name (it may be blank when the item is gone).</summary>
		public static string GetTextCorrected(string itemName,MaintRecord maintRecordOld,MaintCorrections.CorrectionPlan correctionPlan) {
			string text="Maintenance completion corrected: "+itemName+", done "+MaintValues.DateToDb(maintRecordOld.DateDone)+" (MaintRecordNum "
				+maintRecordOld.MaintRecordNum+"): "+string.Join("; ",correctionPlan.ListChanges);
			if(correctionPlan.IsFlagChanged(maintRecordOld)) {
				text+=correctionPlan.MaintRecord.IsFlagged ? ", now flagged" : ", no longer flagged";
			}
			return text;
		}

		///<summary>Logs a change to an item under MaintenanceSetup.</summary>
		public static void LogSetup(MaintItem maintItem,string logText) {
			SecurityLogs.MakeLogEntry(EnumPermType.MaintenanceSetup,0,logText,maintItem.MaintItemNum,DateTime.MinValue);
		}

		///<summary>Logs a recorded completion under MaintenanceRecord.</summary>
		public static void LogRecorded(MaintItem maintItem,MaintRecord maintRecord) {
			SecurityLogs.MakeLogEntry(EnumPermType.MaintenanceRecord,0,GetTextRecorded(maintItem,maintRecord),maintItem.MaintItemNum,DateTime.MinValue);
		}
	}
}
