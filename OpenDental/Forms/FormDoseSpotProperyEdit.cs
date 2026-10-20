using System;
using System.Windows.Forms;
using OpenDental.UI;
using OpenDentBusiness;
using OpenDentBusiness.VendorDirect;
using CodeBase;
using System.Linq;
using System.Collections.Generic;

namespace OpenDental {
	///<summary>Edits one clinic's DoseSpot clinic ID and clinic key. The key is a secret: it is shown masked, passed in and out as plain text in
	///memory only, and eRx Setup encrypts it before saving.</summary>
	public partial class FormDoseSpotPropertyEdit:FormODBase {

		public string ClinicIdVal;
		///<summary>The plain clinic key. eRx Setup encrypts it on save.</summary>
		public string ClinicKeyVal;
		///<summary>eRx Setup's rows, as stored (clinic keys are osd1$ values).</summary>
		public List<ProgramProperty> ListProgramProperties;
		private Clinic _clinic;

		public FormDoseSpotPropertyEdit(Clinic clinic,string strClinicIdPropertyVal,string strClinicKeyVal,List<ProgramProperty> listProgramProperties) {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
			_clinic=clinic;
			ClinicIdVal=strClinicIdPropertyVal;
			ClinicKeyVal=strClinicKeyVal;
			ListProgramProperties=listProgramProperties;
		}

		private void FormDoseSpotPropertyEdit_Load(object sender,EventArgs e) {
			LayoutMenu();
			textClinicAbbr.Text=_clinic.Abbr;
			textClinicID.Text=ClinicIdVal;
			textClinicKey.Text=ClinicKeyVal;
			if(ClinicIdVal.Trim()!="" && ClinicKeyVal.Trim()!="") {//The clinic has values for the clinicID/clinicKey, so they are effectively registered.
				butRegisterClinic.Enabled=false;
			}
			if(_clinic.ClinicNum==0) {//Clinics disabled or is HQ.
				menuMain.Enabled=false;//There is no clinic record to edit.
			}
			if(!TryGetRegisteredClinic(out _,out _)) {
				//No other clinic has a working ID and key. This would be the first clinic: DoseSpot (or the contract holder) creates it.
				butRegisterClinic.Enabled=false;
			}
		}

		///<summary>Finds another clinic with a DoseSpot clinic ID and a key that decrypts, to register this one from. False if none.</summary>
		private bool TryGetRegisteredClinic(out string clinicId,out string clinicKey) {
			clinicId="";
			clinicKey="";
			List<ProgramProperty> listProgramPropertiesIds=ListProgramProperties
				.FindAll(x => x.ClinicNum!=_clinic.ClinicNum && x.PropertyDesc==Erx.PropertyDescs.ClinicID && !string.IsNullOrWhiteSpace(x.PropertyValue));
			for(int i=0;i<listProgramPropertiesIds.Count;i++) {
				ProgramProperty programPropertyKey=ListProgramProperties
					.FirstOrDefault(x => x.ClinicNum==listProgramPropertiesIds[i].ClinicNum && x.PropertyDesc==Erx.PropertyDescs.ClinicKey);
				SecretReadResult secretReadResult=DoseSpotConfig.ReadClinicKey(programPropertyKey?.PropertyValue);
				if(secretReadResult.HasValue) {
					clinicId=listProgramPropertiesIds[i].PropertyValue.Trim();
					clinicKey=secretReadResult.PlainText.Trim();
					return true;
				}
			}
			return false;
		}

		private void LayoutMenu() {
			menuMain.BeginUpdate();
			menuMain.Add(new MenuItemOD("Setup",menuItemSetup_Click));
			menuMain.EndUpdate();
		}

		private void menuItemSetup_Click(object sender,EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.ClinicEdit)) {
				return;
			}
			using FormClinicEdit formClinicEdit=new FormClinicEdit(_clinic.Copy());
			formClinicEdit.ShowDialog();
			if(formClinicEdit.DialogResult==DialogResult.OK) {
				Clinics.Update(formClinicEdit.ClinicCur,_clinic);
				DataValid.SetInvalid(InvalidType.Providers);
				_clinic=formClinicEdit.ClinicCur.Copy();
			}
		}

		private void checkShowKey_CheckedChanged(object sender,EventArgs e) {
			textClinicKey.UseSystemPasswordChar=!checkShowKey.Checked;
		}

		private void butClear_Click(object sender,EventArgs e) {
			textClinicID.Text="";
			textClinicKey.Text="";
		}

		private void butRegisterClinic_Click(object sender,EventArgs e) {
			if(!TryGetRegisteredClinic(out string clinicIdRegistered,out string clinicKeyRegistered)) {
				//Should never happen since we disable this button if we can't find a valid clinicID/clinicKey combo ahead of time
				MsgBox.Show(this,"No registered clinics found.  There must be at least one registered clinic before adding additional clinics.");
				return;
			}
			Cursor=Cursors.WaitCursor;
			string clinicID="";
			string clinicKey="";
			try {
				string userID=DoseSpot.GetUserID(Security.CurUser,_clinic.ClinicNum);
				DoseSpot.RegisterClinic(_clinic.ClinicNum,clinicIdRegistered,clinicKeyRegistered,userID,out clinicID,out clinicKey);
			}
			catch(Exception ex) {
				MessageBox.Show(Lan.g(this,"Error: ")+ex.Message);
				return;
			}
			finally {
				Cursor=Cursors.Default;
			}
			textClinicID.Text=clinicID;
			textClinicKey.Text=clinicKey;
			MsgBox.Show(this,"DoseSpot registered this clinic, and its ID and key were saved.\r\n"
				+"If patients in this clinic are shared with other clinics, ask DoseSpot to link the clinics before using it.");
		}

		private void butSave_Click(object sender,EventArgs e) {
			ClinicIdVal=textClinicID.Text.Trim();
			ClinicKeyVal=textClinicKey.Text.Trim();
			DialogResult=DialogResult.OK;
		}

	}
}
