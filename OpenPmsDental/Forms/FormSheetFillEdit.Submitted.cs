// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using System.Windows.Forms;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	///<summary>Open Dental 25.4 sheet.DateTimeSubmitted: a "Submitted" box in Fill Sheet. Empty means not submitted. The controls are made here,
	///not in the designer, so this stays apart from the rest of the window.</summary>
	public partial class FormSheetFillEdit {
		private Label labelDateSubmitted;
		private TextBox textDateSubmitted;
		///<summary>False in the kiosk and for statements, where the box is hidden and the stored value is kept.</summary>
		private bool _isDateSubmittedShown;

		///<summary>Called from Load after the other boxes are filled. The box goes in the free space above the Add Field button (designer position
		///621,616), measured from that button so it follows the form's size and zoom. Shown only for the patient form types (the ones the Patient Forms
		///window lists), and not in the kiosk or for statements.</summary>
		private void LoadDateSubmitted() {
			labelDateSubmitted=new Label();
			labelDateSubmitted.Anchor=AnchorStyles.Bottom|AnchorStyles.Left;
			labelDateSubmitted.Location=new Point(butAddField.Left,butAddField.Top-LayoutManager.Scale(25));
			labelDateSubmitted.Size=new Size(LayoutManager.Scale(62),LayoutManager.Scale(18));
			labelDateSubmitted.Text=Lan.g(this,"Submitted");
			labelDateSubmitted.TextAlign=ContentAlignment.MiddleRight;
			LayoutManager.Add(labelDateSubmitted,butAddField.Parent);
			textDateSubmitted=new TextBox();
			textDateSubmitted.Anchor=AnchorStyles.Bottom|AnchorStyles.Left;
			textDateSubmitted.Location=new Point(butAddField.Left+LayoutManager.Scale(64),butAddField.Top-LayoutManager.Scale(26));
			textDateSubmitted.Size=new Size(LayoutManager.Scale(103),LayoutManager.Scale(20));
			textDateSubmitted.TabStop=false;
			LayoutManager.Add(textDateSubmitted,butAddField.Parent);
			textDateSubmitted.Text=FormSubmittedDates.ToDisplay(SheetCur.DateTimeSubmitted);
			_isDateSubmittedShown=!IsInTerminal && !IsStatement && FormSubmittedDates.IsPatientFormSheetType(SheetCur.SheetType);
			labelDateSubmitted.Visible=_isDateSubmittedShown;
			textDateSubmitted.Visible=_isDateSubmittedShown;
		}

		///<summary>Called on save, before the sheet is stored. Sets SheetCur.DateTimeSubmitted from the box; a new sheet saved with a signature and no
		///date entered counts as submitted now (fork decision). Shows a message and returns false when the box is not a date and time.</summary>
		private bool TrySetDateSubmitted() {
			if(textDateSubmitted==null || !_isDateSubmittedShown) {
				return true;//not shown (kiosk, statements): the stored value stays
			}
			if(!FormSubmittedDates.TryParse(textDateSubmitted.Text,SheetCur.DateTimeSubmitted,out DateTime dateTimeSubmitted)) {
				MsgBox.Show(this,"Please fix data entry errors first.");
				return false;
			}
			SheetCur.DateTimeSubmitted=FormSubmittedDates.GetToSave(dateTimeSubmitted,SheetCur.IsNew,IsAnySignatureEntered(),DateTime.Now);
			textDateSubmitted.Text=FormSubmittedDates.ToDisplay(SheetCur.DateTimeSubmitted);
			return true;
		}

		///<summary>True if any signature box on the sheet holds a signature.</summary>
		private bool IsAnySignatureEntered() {
			for(int i=0;i<panelMain.Controls.Count;i++) {
				if(panelMain.Controls[i] is OpenPmsDental.UI.SignatureBoxWrapper signatureBoxWrapper && !signatureBoxWrapper.SigIsBlank) {
					return true;
				}
			}
			return false;
		}
	}
}
