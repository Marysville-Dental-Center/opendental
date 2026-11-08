using System;
using System.Collections.Generic;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using OpenPmsDentalBusiness;
using System.Xml;
using System.Text;
using CodeBase;

namespace OpenPmsDental{

	///<summary>Shows the third party license texts. Open Dental's registration key and its license agreement signature (which was sent to Open
	///Dental HQ) were removed (docs/decoupling-plan.md), and with them this window's registration key box and agreement check box; the
	///RegistrationKey preference is left as it is.</summary>
	public partial class FormRegistrationKey : FormODBase {

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

		private void listRegistration_SelectedIndexChanged(object sender,EventArgs e) {
			richTextAgreement.Text=listBoxRegistration.GetSelected<string>();
		}

		private void butOK_Click(object sender, System.EventArgs e) {
			DialogResult=DialogResult.OK;
		}

	}
}
