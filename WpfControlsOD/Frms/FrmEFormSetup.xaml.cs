using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;
using System.Xml.Serialization;
using CodeBase;
using Microsoft.Win32;
using OpenPmsDentalBusiness;
using WpfControls.UI;

namespace OpenPmsDental {
	///<summary></summary>
	public partial class FrmEFormSetup : FrmODBase {
		///<summary></summary>
		public FrmEFormSetup() {
			InitializeComponent();
			Load+=FrmEformSetup_Load;
		}

		///<summary></summary>
		private void FrmEformSetup_Load(object sender, EventArgs e) {
			Lang.F(this);
			textVIntSpaceBelowEachField.Value=PrefC.GetInt(PrefName.EformsSpaceBelowEachField);
			textVIntSpaceToRightEachField.Value=PrefC.GetInt(PrefName.EformsSpaceToRightEachField);
			//ODOPTS: Open Dental's EFormImportAllergiesAsOther and EFormImportProblemsAsOther, read by name (on when the row is missing).
			checkImportAllergiesAsOther.Checked=EFormImport.IsAllergiesAsOther();
			checkImportProblemsAsOther.Checked=EFormImport.IsProblemsAsOther();
		}

		private void butSave_Click(object sender,EventArgs e) {
			if(!textVIntSpaceBelowEachField.IsValid()
				|| !textVIntSpaceToRightEachField.IsValid())
			{
				MsgBox.Show(this,"Please fix data entry errors first.");
				return;
			}
			bool changed=false;
			changed|=Prefs.UpdateInt(PrefName.EformsSpaceBelowEachField,textVIntSpaceBelowEachField.Value);
			changed|=Prefs.UpdateInt(PrefName.EformsSpaceToRightEachField,textVIntSpaceToRightEachField.Value);
			bool isAllergiesAsOtherOld=EFormImport.IsAllergiesAsOther();
			bool isAllergiesAsOther=checkImportAllergiesAsOther.Checked==true;
			if(EFormImport.SetAllergiesAsOther(isAllergiesAsOther)) {
				changed=true;
				OdOptions.LogChange(EnumPermType.Setup,"eForm import: unmatched allergies as Other",EFormImport.PREF_ALLERGIES_AS_OTHER,
					OdOptions.GetOnOff(isAllergiesAsOtherOld),OdOptions.GetOnOff(isAllergiesAsOther));
			}
			bool isProblemsAsOtherOld=EFormImport.IsProblemsAsOther();
			bool isProblemsAsOther=checkImportProblemsAsOther.Checked==true;
			if(EFormImport.SetProblemsAsOther(isProblemsAsOther)) {
				changed=true;
				OdOptions.LogChange(EnumPermType.Setup,"eForm import: unmatched problems as Other",EFormImport.PREF_PROBLEMS_AS_OTHER,
					OdOptions.GetOnOff(isProblemsAsOtherOld),OdOptions.GetOnOff(isProblemsAsOther));
			}
			if(changed){
				DataValid.SetInvalid(InvalidType.Prefs);
			}
			IsDialogOK=true;
		}
	}
}