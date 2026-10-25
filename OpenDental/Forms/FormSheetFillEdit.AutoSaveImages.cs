// B-PRINT: automatic copy of saved sheets to Imaging (Open Dental 25.1). The "Autosave to Images" box is gone; the rules are in
// OpenDentBusiness.SheetAutoSaveImages.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using OpenDentBusiness;

namespace OpenDental {
	public partial class FormSheetFillEdit {
		///<summary>The sheet as it was once the window had opened and laid out its fields (SheetAutoSaveImages.GetFingerprint).</summary>
		private string _autoSaveFingerprintAtOpen="";
		///<summary>True if the sheet had never been saved when the window opened. Its first save always counts as a change.</summary>
		private bool _autoSaveWasNewAtOpen;

		///<summary>Takes the snapshot once the window is showing: by then Load has filled, grown and laid out the fields, and the user has not typed
		///anything yet.</summary>
		protected override void OnShown(EventArgs e) {
			TakeAutoSaveSnapshot();
			base.OnShown(e);
		}

		private void TakeAutoSaveSnapshot() {
			_autoSaveWasNewAtOpen=SheetCur?.IsNew??false;
			_autoSaveFingerprintAtOpen=SheetAutoSaveImages.GetFingerprint(SheetCur);
		}

		///<summary>True if this save should also put a PDF copy of the sheet in Imaging: the sheet has a patient, a non-hidden image category is
		///flagged Autosave Forms (_isAutoSave), the sheet's layout has "Copy to Imaging when saved" on, the window is not in a kiosk or terminal, and
		///the save changed the sheet. Call after the sheet was saved. When it returns true the caller makes the copy, so the snapshot moves on: a
		///later save in the same window copies again only if it changes the sheet again.</summary>
		private bool IsAutoSaveCopyDue() {
			if(SheetCur==null) {
				return false;
			}
			bool hasChanged=SheetAutoSaveImages.HasChanged(_autoSaveFingerprintAtOpen,_autoSaveWasNewAtOpen,SheetCur);
			bool isCopyDue=SheetAutoSaveImages.IsCopyDue(SheetCur.PatNum,_isAutoSave,SheetAutoSaveImages.IsLayoutOptionOn(SheetCur),IsInTerminal,hasChanged);
			if(isCopyDue) {
				TakeAutoSaveSnapshot();
			}
			return isCopyDue;
		}
	}
}
