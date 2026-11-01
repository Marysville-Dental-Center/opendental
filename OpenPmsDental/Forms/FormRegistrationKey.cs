using System;
using System.Collections.Generic;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using OpenPmsDentalBusiness;
using System.Xml;
using System.Text;
using CodeBase;

namespace OpenPmsDental{

	///<summary>Shows the registration key field and the third party license texts. The registration key is optional and is not validated against
	///anything: Open Dental's registration key check and license agreement signature (which was sent to Open Dental HQ) were removed.</summary>
	public partial class FormRegistrationKey : FormODBase {

		private string _key;

		public FormRegistrationKey()
		{
			//
			// Required for Windows Form Designer support
			//
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormRegistrationKey_Load(object sender,EventArgs e) {
			//No license agreement signature is collected anymore. The license texts below are still shown for reference.
			checkAgree.Visible=false;
			_key=PrefC.GetString(PrefName.RegistrationKey);
			if(_key!=null && _key.Length==16){
				_key=_key.Substring(0,4)+"-"+_key.Substring(4,4)+"-"+_key.Substring(8,4)+"-"+_key.Substring(12,4);
			}
			textKey1.Text=_key;
			SetOKEnabled();
			FillListBoxRegistration();
			listBoxRegistration.SetSelected(0);
		}

		///<summary>Fills the listbox with licenses and the license text as a tag: the same list as FormLicense (LicenseList), the program's own
		///license, the GNU GPL, first.</summary>
		private void FillListBoxRegistration() {
			List<LicenseEntry> listLicenseEntries=LicenseList.GetEntries();
			for(int i=0;i<listLicenseEntries.Count;i++) {
				listBoxRegistration.Items.Add(listLicenseEntries[i].Name,listLicenseEntries[i].Text);
			}
		}

		private void textKey1_KeyUp(object sender,KeyEventArgs e) {
			int cursorPosition=textKey1.SelectionStart;
			textKey1.Text=textKey1.Text.ToUpper();
			int length=textKey1.Text.Length;
			if(Regex.IsMatch(textKey1.Text,@"^[A-Z0-9]{5}$")) {
				textKey1.Text=textKey1.Text.Substring(0,4)+"-"+textKey1.Text.Substring(4);
			}
			else if(Regex.IsMatch(textKey1.Text,@"^[A-Z0-9]{4}-[A-Z0-9]{5}$")) {
				textKey1.Text=textKey1.Text.Substring(0,9)+"-"+textKey1.Text.Substring(9);
			}
			else if(Regex.IsMatch(textKey1.Text,@"^[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{5}$")) {
				textKey1.Text=textKey1.Text.Substring(0,14)+"-"+textKey1.Text.Substring(14);
			}
			if(textKey1.Text.Length>length) {
				cursorPosition++;
			}
			textKey1.SelectionStart=cursorPosition;
		}

		private void textKey1_TextChanged(object sender,EventArgs e) {
			SetOKEnabled();
		}

		private void listRegistration_SelectedIndexChanged(object sender,EventArgs e) {
			richTextAgreement.Text=listBoxRegistration.GetSelected<string>();
		}

		private void checkAgree_CheckedChanged(object sender,EventArgs e) {
			SetOKEnabled();
		}

		private void SetOKEnabled(){
			//No signature is required, so OK is always available.
			butOK.Enabled=true;
		}

		private void butOK_Click(object sender, System.EventArgs e) {
			if(textKey1.Text!=""
				&& !Regex.IsMatch(textKey1.Text,@"^[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}$")
				&& !Regex.IsMatch(textKey1.Text,@"^[A-Z0-9]{16}$"))
			{
				MsgBox.Show(this,"Invalid registration key format.");
				return;
			}
			string regkey="";
			if(Regex.IsMatch(textKey1.Text,@"^[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}$")){
				regkey=textKey1.Text.Substring(0,4)+textKey1.Text.Substring(5,4)+textKey1.Text.Substring(10,4)+textKey1.Text.Substring(15,4);
			}
			else if(Regex.IsMatch(textKey1.Text,@"^[A-Z0-9]{16}$")){
				regkey=textKey1.Text;
			}
			if(Prefs.UpdateString(PrefName.RegistrationKey,regkey)) {
				Signalods.SetInvalid(InvalidType.Prefs);
			}
			//prefs refresh automatically in the calling class anyway.
			DialogResult=DialogResult.OK;
		}

	}
}
