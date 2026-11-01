// B-PRINT: automatic copy of saved sheets to Imaging (Open Dental 25.1). The "Autosave to Images" box is gone; the rules are in
// OpenDentBusiness.SheetAutoSaveImages.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	public partial class FormSheetFillEdit {
		///<summary>The sheet as it was once the window had opened and laid out its fields, and whether it was new then.</summary>
		private SheetAutoSaveTracker _sheetAutoSaveTracker=new SheetAutoSaveTracker();

		///<summary>Takes the snapshot once the window is showing: by then Load has filled, grown and laid out the fields, and the user has not typed
		///anything yet.</summary>
		protected override void OnShown(EventArgs e) {
			_sheetAutoSaveTracker.TakeSnapshot(SheetCur);
			base.OnShown(e);
		}

		///<summary>True if this save should also put a PDF copy of the sheet in Imaging: the sheet has a patient, a non-hidden image category is
		///flagged Autosave Forms (_isAutoSave), the sheet's layout has "Copy to Imaging when saved" on, and the save changed the sheet. Call after the
		///sheet was saved. When it returns true the caller makes the copy, so the snapshot moves on: a later save in the same window copies again only
		///if it changes the sheet again.
		///In a kiosk or terminal the copy is made here and false is returned: the patient is at the screen, so a copy that fails (for example a kiosk
		///computer that cannot reach the image folder) shows no error; the failure goes in the audit trail for the office instead.</summary>
		private bool IsAutoSaveCopyDue() {
			if(SheetCur==null) {
				return false;
			}
			bool isCopyDue=_sheetAutoSaveTracker.IsCopyDue(SheetCur,_isAutoSave,SheetAutoSaveImages.IsLayoutOptionOn(SheetCur));
			if(!isCopyDue || !IsInTerminal) {
				return isCopyDue;
			}
			try {
				SaveAsDocument('U',"PatientForm");
			}
			catch(Exception ex) {
				SecurityLogs.MakeLogEntry(EnumPermType.SheetEdit,SheetCur.PatNum,SheetCur.Description+" from "+SheetCur.DateTimeSheet.ToShortDateString()
					+": "+Lan.g(this,"the copy to Imaging could not be saved at the kiosk.")+" "+ex.Message);
			}
			return false;
		}
	}
}
