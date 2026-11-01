// B-PRINT: automatic copy of saved sheets to Imaging (Open Dental 25.1).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenDentBusiness {
	///<summary>Since 25.1 the Fill Sheet window has no "Autosave to Images" box. A sheet whose layout (sheet def) has AutoCheckSaveImage on gets a PDF
	///copy in Imaging every time a save changed it. The rules live here so they can be tested; FormSheetFillEdit calls them.
	///A copy is made only when all of these hold:
	///- the sheet has a patient;
	///- a non-hidden image category is flagged Autosave Forms ("U"), as 24.3 required, even when the layout names its own category;
	///- the layout has the option on;
	///- something on the sheet changed in this save (field values, signatures, added fields or drawings, date, description, note), or the sheet
	///  is new. Opening and closing a sheet without changes writes nothing.
	///A save in a kiosk or terminal follows the same rules: a sheet the patient filled in there reaches Imaging when the patient saves it, and the
	///office's later save copies it again only if the office changed it. 24.3 made no copy in a terminal; the office's OK made it instead, because
	///the box defaulted to checked. Without the box, nothing stored with a sheet says that its last save still owes a copy (sheet.DateTSheetEdited
	///moves on with every OK, changed or not, and a copy in Imaging is not linked to its sheet), so the copy cannot wait for the office's save.</summary>
	public static class SheetAutoSaveImages {
		///<summary>The image category usage letter for Autosave Forms.</summary>
		public const string USAGE_AUTOSAVE_FORMS="U";

		///<summary>True if a PDF copy should be saved to Imaging after this save, in a kiosk or terminal too.</summary>
		public static bool IsCopyDue(long patNum,bool hasAutosaveCategory,bool isLayoutOptionOn,bool hasChanged) {
			return patNum!=0
				&& hasAutosaveCategory
				&& isLayoutOptionOn
				&& hasChanged;
		}

		///<summary>True if a non-hidden image category is flagged Autosave Forms ("U").</summary>
		public static bool HasAutosaveCategory(IEnumerable<Def> listDefsImageCatsShort) {
			return listDefsImageCatsShort!=null && listDefsImageCatsShort.Any(x => !x.IsHidden && (x.ItemValue??"").Contains(USAGE_AUTOSAVE_FORMS));
		}

		///<summary>True if the sheet's layout has "Copy to Imaging when saved" (sheetdef.AutoCheckSaveImage) on. False for a sheet without a layout.</summary>
		public static bool IsLayoutOptionOn(Sheet sheet) {
			if(sheet==null || sheet.SheetDefNum==0) {
				return false;
			}
			SheetDef sheetDef=SheetDefs.GetFirstOrDefault(x => x.SheetDefNum==sheet.SheetDefNum);
			return sheetDef!=null && sheetDef.AutoCheckSaveImage;
		}

		///<summary>A text that changes whenever something a user can change on the sheet changes: the sheet's date (to the minute), description, note
		///and kiosk order, and every field's type, name, position, size and value (signatures are stored in the signature box's value). The order of the
		///fields does not matter, because the window reloads them from the database after a save. The edit time stamp is left out on purpose:
		///every save sets it.</summary>
		public static string GetFingerprint(Sheet sheet) {
			if(sheet==null) {
				return "";
			}
			StringBuilder stringBuilder=new StringBuilder();
			//To the minute: the window shows and saves the date and time without seconds, so seconds alone are no change.
			stringBuilder.Append(sheet.DateTimeSheet.ToString("yyyy-MM-dd HH:mm",System.Globalization.CultureInfo.InvariantCulture)).Append('\u001f');
			stringBuilder.Append(NormalizeValue(sheet.Description)).Append('\u001f');
			stringBuilder.Append(NormalizeValue(sheet.InternalNote)).Append('\u001f');
			stringBuilder.Append(sheet.ShowInTerminal).Append('\u001f');
			stringBuilder.Append(sheet.PatNum).Append('\u001e');
			List<string> listFields=new List<string>();
			if(sheet.SheetFields!=null) {
				for(int i=0;i<sheet.SheetFields.Count;i++) {
					SheetField sheetField=sheet.SheetFields[i];
					if(sheetField==null) {
						continue;
					}
					listFields.Add(string.Join("\u001f",
						((int)sheetField.FieldType).ToString(),
						sheetField.FieldName??"",
						sheetField.RadioButtonValue??"",
						sheetField.RadioButtonGroup??"",
						sheetField.XPos.ToString(),
						sheetField.YPos.ToString(),
						sheetField.Width.ToString(),
						sheetField.Height.ToString(),
						NormalizeValue(sheetField.FieldValue)));
				}
			}
			listFields.Sort(StringComparer.Ordinal);
			stringBuilder.Append(string.Join("\u001e",listFields));
			return stringBuilder.ToString();
		}

		///<summary>Line endings are stored differently by the text boxes and the database, so they do not count as a change.</summary>
		private static string NormalizeValue(string value) {
			return (value??"").Replace("\r\n","\n").Replace("\r","\n");
		}

		///<summary>True if the sheet now differs from the fingerprint taken earlier, or if the sheet was new (never saved) when the window opened.</summary>
		public static bool HasChanged(string fingerprintAtOpen,bool wasNewAtOpen,Sheet sheetNow,bool isSignatureChanged=false) {
			if(wasNewAtOpen || isSignatureChanged) {
				return true;
			}
			return GetFingerprint(sheetNow)!=(fingerprintAtOpen??"");
		}
	}

	///<summary>The copy rules for one Fill Sheet window: the snapshot of the sheet taken when the window showed it, and whether a save is due a copy.
	///A save that makes a copy moves the snapshot on, so a later save in the same window copies again only if it changes the sheet again.</summary>
	public class SheetAutoSaveTracker {
		private string _fingerprintAtOpen="";
		private bool _wasNewAtOpen;

		///<summary>Call once the window shows the sheet: its fields are filled, grown and laid out, and nothing was typed yet.</summary>
		public void TakeSnapshot(Sheet sheet) {
			_wasNewAtOpen=sheet?.IsNew??false;
			_fingerprintAtOpen=SheetAutoSaveImages.GetFingerprint(sheet);
		}

		///<summary>True if this save should also put a PDF copy of the sheet in Imaging (SheetAutoSaveImages.IsCopyDue). Call after the sheet was
		///saved. When it returns true the caller makes the copy, and the snapshot moves on.</summary>
		public bool IsCopyDue(Sheet sheet,bool hasAutosaveCategory,bool isLayoutOptionOn) {
			if(sheet==null) {
				return false;
			}
			bool hasChanged=SheetAutoSaveImages.HasChanged(_fingerprintAtOpen,_wasNewAtOpen,sheet);
			bool isCopyDue=SheetAutoSaveImages.IsCopyDue(sheet.PatNum,hasAutosaveCategory,isLayoutOptionOn,hasChanged);
			if(isCopyDue) {
				TakeSnapshot(sheet);
			}
			return isCopyDue;
		}
	}
}
