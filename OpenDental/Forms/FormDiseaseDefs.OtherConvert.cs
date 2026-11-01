// The Problem list opened to convert an eForm "Other" problem (Open Dental 25.2, package B-MED).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Windows.Forms;
using OpenDentBusiness;

namespace OpenDental {
	public partial class FormDiseaseDefs {
		///<summary>Set with IsSelectionMode to convert an "Other" problem (OtherEntryConvertL): what the patient typed.  The window then starts with
		///the Description search filled with it, picks one problem, and Add starts the new problem with it as the name and picks the new problem.
		///Null in every other use.  A public field, so FormLauncher can set it.</summary>
		public string TextOtherToConvert;

		///<summary>True while the window is open to convert an "Other" problem.</summary>
		private bool IsConvertingOther() {
			return IsSelectionMode && TextOtherToConvert!=null;
		}

		///<summary>Called first on load, before the search boxes are wired, so the grid fills once with the typed text as the Description search.</summary>
		private void ShowSearchForOtherConvert() {
			if(!IsConvertingOther()) {
				return;
			}
			IsMultiSelect=false;
			textDescript.Text=TextOtherToConvert;
			Text=Lan.g(this,"Convert Other Problem")+": "+TextOtherToConvert;
		}

		///<summary>Called by Add before the edit window opens.  While converting, the new problem starts with the typed text as its name.</summary>
		private void SetNewDefForOtherConvert(DiseaseDef diseaseDef) {
			if(!IsConvertingOther()) {
				return;
			}
			diseaseDef.DiseaseName=TextOtherToConvert;
		}

		///<summary>Called by Add after the new problem was saved and the grid refilled.  While converting, the new problem is the one picked and the
		///window closes.  When the current search hides the new problem, the window stays open so the user can clear the search and pick it.</summary>
		private void PickNewDefForOtherConvert(DiseaseDef diseaseDef) {
			if(!IsConvertingOther() || diseaseDef==null || diseaseDef.DiseaseDefNum==0) {
				return;
			}
			for(int i=0;i<gridMain.ListGridRows.Count;i++) {
				if(((DiseaseDef)gridMain.ListGridRows[i].Tag).DiseaseDefNum!=diseaseDef.DiseaseDefNum) {
					continue;
				}
				gridMain.SetAll(false);
				gridMain.SetSelected(i,true);
				DialogResult=DialogResult.OK;//FormClosing fills ListDiseaseDefsSelected from the selected row.
				return;
			}
		}
	}
}
