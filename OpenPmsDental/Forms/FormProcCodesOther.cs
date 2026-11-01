// Other Procedure Codes (Canada): Procedure Code Tools, Other Codes. Open Dental 25.4 (the window and the CDHA, DAC and RAMQ code systems) and
// 26.1 (Advanced Options need a password). Backlog package M2-CAN. The rules are in OpenDentBusiness.ProcCodesOther.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using CodeBase;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	///<summary>Imports another Canadian code system (CDHA, DAC in English or French, RAMQ) from a code file the office supplies, and with the
	///Advanced Options updates existing codes of those systems or of CDA.</summary>
	public partial class FormProcCodesOther : FormODBase {
		///<summary>True once an import saved anything, so the caller refreshes procedure codes and definitions.</summary>
		public bool IsChanged;

		public FormProcCodesOther() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormProcCodesOther_Load(object sender,EventArgs e) {
			foreach(EnumOtherCodeSystem codeSystem in Enum.GetValues(typeof(EnumOtherCodeSystem))) {
				listCodeSystem.Items.Add(Lan.g("enumEnumOtherCodeSystem",codeSystem.GetDescription()),codeSystem);
			}
			listCodeSystem.SetSelected(0);
		}

		///<summary>The Advanced Options that are checked.</summary>
		private EnumOtherCodeUpdates GetUpdates() {
			EnumOtherCodeUpdates updates=EnumOtherCodeUpdates.None;
			if(checkUpdateDescriptions.Checked) {
				updates|=EnumOtherCodeUpdates.Descriptions;
			}
			if(checkUpdateAbbreviations.Checked) {
				updates|=EnumOtherCodeUpdates.Abbreviations;
			}
			if(checkUpdateTreatArea.Checked) {
				updates|=EnumOtherCodeUpdates.TreatArea;
			}
			if(checkUpdatePaintType.Checked) {
				updates|=EnumOtherCodeUpdates.PaintType;
			}
			return updates;
		}

		private void butBrowse_Click(object sender,EventArgs e) {
			using OpenFileDialog openFileDialog=new OpenFileDialog();
			openFileDialog.Title=Lan.g(this,"Choose the code file");
			openFileDialog.Filter=Lan.g(this,"Code files")+" (*.csv;*.txt;*.tsv)|*.csv;*.txt;*.tsv|"+Lan.g(this,"All files")+" (*.*)|*.*";
			string exportPath=PrefC.GetString(PrefName.ExportPath);
			if(exportPath!="" && Directory.Exists(exportPath)) {
				openFileDialog.InitialDirectory=exportPath;
			}
			if(openFileDialog.ShowDialog()!=DialogResult.OK) {
				return;
			}
			textFile.Text=openFileDialog.FileName;
		}

		///<summary>26.1: Advanced Options change existing codes in bulk, so the user types the confirmation phrase first. Returns false if the user
		///cancels or types something else.</summary>
		private bool ConfirmAdvancedOptions(EnumOtherCodeUpdates updates) {
			if(!ProcCodesOther.RequiresConfirmation(updates)) {
				return true;
			}
			InputBox inputBox=new InputBox(Lan.g(this,"Advanced Options change existing codes in bulk:")+" "+ProcCodesOther.GetUpdatesText(updates)+".\r\n"
				+Lan.g(this,"To continue, type")+" "+ProcCodesOther.CONFIRMATION_PHRASE);
			inputBox.ShowDialog();
			if(inputBox.IsDialogCancel) {
				return false;
			}
			if(!ProcCodesOther.IsConfirmationPhrase(inputBox.StringResult)) {
				MsgBox.Show(this,"That is not the confirmation phrase. Nothing was changed.");
				return false;
			}
			return true;
		}

		private void butOK_Click(object sender,EventArgs e) {
			//Security Admin and Procedure Code Edit, as the other code tools. Checked here because the Setup Wizard can open Procedure Code Tools
			//without the Security Admin check of Procedure Codes, Tools.
			if(!Security.IsAuthorized(EnumPermType.SecurityAdmin) || !Security.IsAuthorized(EnumPermType.ProcCodeEdit)) {
				return;
			}
			if(listCodeSystem.SelectedIndex<0) {
				MsgBox.Show(this,"Choose a code system.");
				return;
			}
			EnumOtherCodeSystem codeSystem=listCodeSystem.GetSelected<EnumOtherCodeSystem>();
			EnumOtherCodeUpdates updates=GetUpdates();
			string choiceError=ProcCodesOther.GetChoiceError(codeSystem,updates);
			if(choiceError!="") {
				MessageBox.Show(this,choiceError);
				return;
			}
			string path=textFile.Text.Trim();
			if(path=="" || !File.Exists(path)) {
				MsgBox.Show(this,"Choose the code file first.");
				return;
			}
			if(!ConfirmAdvancedOptions(updates)) {
				return;
			}
			string text;
			try {
				text=ProcCodesOther.DecodeFileBytes(File.ReadAllBytes(path));
			}
			catch(Exception ex) {
				MessageBox.Show(this,Lan.g(this,"The code file could not be read:")+"\r\n"+ex.Message);
				return;
			}
			List<OtherCodeRow> listOtherCodeRows=ProcCodesOther.Parse(text,out List<string> listErrors);
			if(listOtherCodeRows.Count==0) {
				using MsgBoxCopyPaste msgBoxCopyPasteNone=new MsgBoxCopyPaste(Lan.g(this,"No procedure codes were found in the file.")+"\r\n\r\n"
					+string.Join("\r\n",listErrors));
				msgBoxCopyPasteNone.ShowDialog();
				return;
			}
			Cursor=Cursors.WaitCursor;
			OtherCodeImportPlan otherCodeImportPlan=ProcCodesOther.CreatePlanFromDb(codeSystem,updates,listOtherCodeRows,listErrors);
			Cursor=Cursors.Default;
			string summary=ProcCodesOther.GetPlanSummary(otherCodeImportPlan);
			if(!otherCodeImportPlan.HasWork()) {
				using MsgBoxCopyPaste msgBoxCopyPasteNothing=new MsgBoxCopyPaste(Lan.g(this,"Nothing to change.")+"\r\n\r\n"+summary);
				msgBoxCopyPasteNothing.ShowDialog();
				return;
			}
			if(MessageBox.Show(this,summary+"\r\n"+Lan.g(this,"Continue?"),Text,MessageBoxButtons.OKCancel)!=DialogResult.OK) {
				return;
			}
			Cursor=Cursors.WaitCursor;
			OtherCodeImportResult otherCodeImportResult=ProcCodesOther.Apply(otherCodeImportPlan);
			Cursor=Cursors.Default;
			IsChanged=true;
			DataValid.SetInvalid(InvalidType.Defs,InvalidType.ProcCodes);
			MessageBox.Show(this,ProcCodesOther.GetLogText(otherCodeImportPlan,otherCodeImportResult));
			DialogResult=DialogResult.OK;
		}
	}
}
