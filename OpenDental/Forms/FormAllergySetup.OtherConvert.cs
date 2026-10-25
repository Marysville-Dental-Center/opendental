// The Allergy list opened to convert an eForm "Other" allergy (Open Dental 25.2, package B-MED).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using OpenDentBusiness;

namespace OpenDental {
	public partial class FormAllergySetup {
		///<summary>Set with IsSelectionMode to convert an "Other" allergy (OtherEntryConvertL): what the patient typed.  The window then shows a Search
		///box filled with it, and Add starts the new allergy with it as the description and picks the new allergy.  Null in every other use.  A public
		///field, so FormLauncher can set it.</summary>
		public string TextOtherToConvert;

		///<summary>True while the window is open to convert an "Other" allergy.</summary>
		private bool IsConvertingOther() {
			return IsSelectionMode && TextOtherToConvert!=null;
		}

		///<summary>Called on load.  Shows the Search box, filled with what the patient typed, when converting an "Other" allergy.</summary>
		private void ShowSearchForOtherConvert() {
			if(!IsConvertingOther()) {
				return;
			}
			labelSearch.Visible=true;
			textSearch.Visible=true;
			textSearch.Text=TextOtherToConvert;
			textSearch.TextChanged+=textSearch_TextChanged;
			Text=Lan.g(this,"Convert Other Allergy")+": "+TextOtherToConvert;
		}

		private void textSearch_TextChanged(object sender,EventArgs e) {
			FillGrid();
		}

		///<summary>Called by FillGrid.  While converting an "Other" allergy, keeps the definitions whose description contains every word in the Search
		///box.  Otherwise returns the list unchanged.</summary>
		private List<AllergyDef> FilterForOtherConvert(List<AllergyDef> listAllergyDefs) {
			if(!IsConvertingOther()) {
				return listAllergyDefs;
			}
			return OtherEntryConverts.FilterByWords(listAllergyDefs,x => x.Description,textSearch.Text);
		}

		///<summary>Called by Add before the edit window opens.  While converting, the new allergy starts with the typed text as its description.</summary>
		private void SetNewDefForOtherConvert(AllergyDef allergyDef) {
			if(!IsConvertingOther()) {
				return;
			}
			allergyDef.Description=TextOtherToConvert;
		}

		///<summary>Called by Add after the edit window closes.  While converting, a newly saved allergy is the one picked, and the window closes.
		///Returns true when the window is closing.</summary>
		private bool PickNewDefForOtherConvert(DialogResult dialogResult,AllergyDef allergyDef) {
			if(!IsConvertingOther() || dialogResult!=DialogResult.OK || allergyDef==null || allergyDef.AllergyDefNum==0) {
				return false;
			}
			AllergyDefNumSelected=allergyDef.AllergyDefNum;
			DialogResult=DialogResult.OK;
			return true;
		}
	}
}
