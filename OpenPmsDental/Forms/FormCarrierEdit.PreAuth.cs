// Edit Carrier's "Procedures requiring preauthorization" box (Open Dental 26.1, feature v26_1-lists-carrier-preauth-flags, package M2-CARRIER in
// docs/specs/port-backlog.md). The rules are in CarrierPreAuths; this file only reads the box, warns and fills it from the procedure code picker.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	public partial class FormCarrierEdit {
		///<summary>Checks the preauthorization box before saving. Shows why it can not be saved (spaces, an empty entry, a range that runs backwards)
		///and returns false; asks before saving codes that are not in the procedure code list. On true, preAuthCodes is the text to store: plain
		///hyphens, no repeated entries.</summary>
		private bool TryGetPreAuthCodes(out string preAuthCodes) {
			preAuthCodes=CarrierCur.PreAuthCodes??"";
			if(textPreAuthCodes.ReadOnly) {
				return true;//No permission to edit: keep what is stored.
			}
			PreAuthCodesCheck preAuthCodesCheck=CarrierPreAuths.Check(textPreAuthCodes.Text);
			if(!preAuthCodesCheck.IsValid) {
				MessageBox.Show(this,preAuthCodesCheck.Error);
				textPreAuthCodes.Focus();
				return false;
			}
			if(preAuthCodesCheck.ListUnknownCodes.Count>0) {
				string message=Lan.g(this,"These procedure codes do not exist yet:")+" "+string.Join(", ",preAuthCodesCheck.ListUnknownCodes)+"\r\n"
					+Lan.g(this,"Save them in the preauthorization list anyway?");
				if(MessageBox.Show(this,message,"",MessageBoxButtons.OKCancel)!=DialogResult.OK) {
					textPreAuthCodes.Focus();
					return false;
				}
			}
			preAuthCodes=preAuthCodesCheck.Codes;
			return true;
		}

		///<summary>Adds the codes picked in the procedure code list to the end of the box.</summary>
		private void butPreAuthCodes_Click(object sender,EventArgs e) {
			using FormProcCodes formProcCodes=new FormProcCodes();
			formProcCodes.IsSelectionMode=true;
			formProcCodes.CanAllowMultipleSelections=true;
			if(formProcCodes.ShowDialog()!=DialogResult.OK) {
				return;
			}
			List<string> listCodesPicked=formProcCodes.ListProcedureCodesSelected.Select(x => x.ProcCode).ToList();
			string codes=CarrierPreAuths.Merge(CarrierPreAuths.FromDisplay(textPreAuthCodes.Text).Replace(" ",""),listCodesPicked);
			textPreAuthCodes.Text=CarrierPreAuths.ToDisplay(codes);
		}
	}
}
