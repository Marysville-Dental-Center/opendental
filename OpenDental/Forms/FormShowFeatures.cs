using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;
using OpenDentBusiness;
using CodeBase;
using System.Collections.Generic;
using System.Linq;

namespace OpenDental{
	///<summary></summary>
	public partial class FormShowFeatures : FormODBase {
		private bool _isClinicsEnabledInDb=false;

		///<summary></summary>
		public FormShowFeatures()
		{
			//
			// Required for Windows Form Designer support
			//
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormShowFeatures_Load(object sender, System.EventArgs e) {
			checkCapitation.Checked=!PrefC.GetBool(PrefName.EasyHideCapitation);
			checkMedicaid.Checked=!PrefC.GetBool(PrefName.EasyHideMedicaid);
			checkPublicHealth.Checked=!PrefC.GetBool(PrefName.EasyHidePublicHealth);
			checkDentalSchools.Checked=!PrefC.GetBool(PrefName.EasyHideDentalSchools);
			checkHospitals.Checked=!PrefC.GetBool(PrefName.EasyHideHospitals);
			checkInsurance.Checked=!PrefC.GetBool(PrefName.EasyHideInsurance);
			checkClinical.Checked=!PrefC.GetBool(PrefName.EasyHideClinical);
			checkBasicModules.Checked=PrefC.GetBool(PrefName.EasyBasicModules);
			_isClinicsEnabledInDb=!PrefC.GetBool(PrefName.EasyNoClinics);
			RestoreClinicCheckBox();
			checkRepeatCharges.Checked=!PrefC.GetBool(PrefName.EasyHideRepeatCharges);
			checkMedicalIns.Checked=PrefC.GetBool(PrefName.ShowFeatureMedicalInsurance);
			checkEhr.Checked=PrefC.GetBool(PrefName.ShowFeatureEhr);
			checkSuperFam.Checked=PrefC.GetBool(PrefName.ShowFeatureSuperfamilies);
			checkPatClone.Checked=PrefC.GetBool(PrefName.ShowFeaturePatientClone);
			checkShowEnterprise.Checked=PrefC.GetBool(PrefName.ShowFeatureEnterprise);
			checkShowReactivations.Checked=PrefC.GetBool(PrefName.ShowFeatureReactivations);
			checkEraShowControlId.Checked=PrefC.GetBool(PrefName.EraShowControlIdFilter);
			bool isLateChargesFeatureActive=PrefC.GetBool(PrefName.ShowFeatureLateCharges);
			if(isLateChargesFeatureActive) {
				radioLateCharges.Checked=true;
				radioBillingFinance.Checked=false;
			}
			else {
				radioBillingFinance.Checked=true;
				radioLateCharges.Checked=false;
			}
		}

		private void checkRepeatCharges_Click(object sender,EventArgs e) {
			if(checkRepeatCharges.Checked) {
				MsgBox.Show(this,"You will need to restart the program for the change to take effect.");
			}
		}

		private void checkEnableClinics_Click(object sender,EventArgs e) {
			if (!GetPasswordFromUser()) {
				RestoreClinicCheckBox();
			}
		}

		private bool GetPasswordFromUser() {
			InputBox inputBoxPassword=new InputBox("To prevent accidental changes, please enter password.  It can be found in our manual.");
			inputBoxPassword.ShowDialog();
			if(inputBoxPassword.IsDialogCancel) {
				return false;
			}
			if(inputBoxPassword.StringResult!="abracadabra") {
				MsgBox.Show(this,"Incorrect password.");
				return false;
			}
			return true;
		}

		private void checkEhr_Click(object sender,EventArgs e) {
			//The EHR (Meaningful Use) feature depended on Open Dental's proprietary EHR.dll, which this build does not load.
			//Turning the feature off is still allowed so that databases which had it on can turn it off.
			if(checkEhr.Checked) {
				checkEhr.Checked=false;
				MsgBox.Show(this,"The EHR feature is not available in this build.");
				return;
			}
			MsgBox.Show(this,"You will need to restart the program for the change to take effect.");
		}

		private void checkPublicHealth_Click(object sender,EventArgs e) {
			if(checkPublicHealth.Checked) {
				MsgBox.Show(this,"You will need to restart the program for the change to take effect.");
			}
		}

		private void checkRestart_Click(object sender,EventArgs e) {
			MsgBox.Show("You will need to restart the program for the change to take effect.");
		}

		///<summary>Restores checkEnableClinics to original value when form was opened.</summary>
		private void RestoreClinicCheckBox() {
			checkEnableClinics.Checked=_isClinicsEnabledInDb;
		}

		///<summary>Validates that PrefName.EasyNoClinics is ok to be changed and changes it when necessary.
		///If fails then restores checkEnableClinics to original value when form was opened.</summary>
		private bool IsClinicCheckBoxOk() {
			if(_isClinicsEnabledInDb==checkEnableClinics.Checked) { //No change.
				return true;
			}
			try {
				//Turn clinics on/off locally and send the signal to other workstations.
				Prefs.UpdateBool(PrefName.EasyNoClinics,!checkEnableClinics.Checked);
				DataValid.SetInvalid(InvalidType.Prefs);
			}
			catch(Exception ex) {
				//Change it back to what the db has.
				RestoreClinicCheckBox();
				MessageBox.Show(ex.Message);
				return false;
			}	
			return true;
		}

		private void butSave_Click(object sender, System.EventArgs e) {
			if(!IsClinicCheckBoxOk()) {
				return;
			}
			bool hasChanged=false;
			hasChanged |= Prefs.UpdateBool(PrefName.EasyHideCapitation,!checkCapitation.Checked);
			hasChanged |= Prefs.UpdateBool(PrefName.EasyHideMedicaid,!checkMedicaid.Checked);
			hasChanged |= Prefs.UpdateBool(PrefName.EasyHidePublicHealth,!checkPublicHealth.Checked);
			hasChanged |= Prefs.UpdateBool(PrefName.EasyHideDentalSchools,!checkDentalSchools.Checked);
			hasChanged |= Prefs.UpdateBool(PrefName.EasyHideHospitals,!checkHospitals.Checked);
			hasChanged |= Prefs.UpdateBool(PrefName.EasyHideInsurance,!checkInsurance.Checked);
			hasChanged |= Prefs.UpdateBool(PrefName.EasyHideClinical,!checkClinical.Checked);
			hasChanged |= Prefs.UpdateBool(PrefName.EasyBasicModules,checkBasicModules.Checked);
			hasChanged |= Prefs.UpdateBool(PrefName.EasyHideRepeatCharges,!checkRepeatCharges.Checked);
			hasChanged |= Prefs.UpdateBool(PrefName.ShowFeatureMedicalInsurance,checkMedicalIns.Checked);
			hasChanged |= Prefs.UpdateBool(PrefName.ShowFeatureEhr,checkEhr.Checked);
			hasChanged |= Prefs.UpdateBool(PrefName.ShowFeatureSuperfamilies,checkSuperFam.Checked);
			hasChanged |= Prefs.UpdateBool(PrefName.ShowFeaturePatientClone,checkPatClone.Checked);
			hasChanged |= Prefs.UpdateBool(PrefName.ShowFeatureEnterprise,checkShowEnterprise.Checked);
			hasChanged |= Prefs.UpdateBool(PrefName.ShowFeatureReactivations,checkShowReactivations.Checked);
			hasChanged |= Prefs.UpdateBool(PrefName.EraShowControlIdFilter,checkEraShowControlId.Checked);
			hasChanged |= Prefs.UpdateBool(PrefName.ShowFeatureLateCharges,radioLateCharges.Checked);
			if(hasChanged) {
				DataValid.SetInvalid(InvalidType.Prefs);
			}
			if(_isClinicsEnabledInDb!=checkEnableClinics.Checked) {
				MsgBox.Show(this,"You will need to restart the program for the change to take effect.");
			}
			//We should use ToolBut invalidation to redraw toolbars that could've been just enabled and stop forcing customers restarting.
			//DataValid.SetInvalid(InvalidType.ToolBut);
			DialogResult=DialogResult.OK;
		}

	}
}