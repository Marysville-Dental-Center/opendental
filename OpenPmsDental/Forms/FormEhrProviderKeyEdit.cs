using System;
using System.Windows.Forms;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	public partial class FormEhrProviderKeyEdit:FormODBase {
		private EhrProvKey _keyCur;

		///<summary>Only used from FormEhrProviderKeys.  keyCur can be a blank new key.  keyCur cannot be null.</summary>
		public FormEhrProviderKeyEdit(EhrProvKey keyCur) {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
			_keyCur=keyCur;
		}

		private void FormEhrProviderKeyEdit_Load(object sender,EventArgs e) {
			textYear.Text=_keyCur.YearValue.ToString();
			textKey.Text=_keyCur.ProvKey;
			textLName.Text=_keyCur.LName;
			textFName.Text=_keyCur.FName;
		}

		///<summary>Deleting a row needs the Setup permission (docs/specs/ehr-rebuild-plan.md, section 5.1). EhrProvKeys.DeleteChecked checks it again,
		///turns the radiology procedures alert off when the last row goes, as before, and writes the Setup security log entry.</summary>
		private void butDelete_Click(object sender,EventArgs e) {
			if(_keyCur.IsNew) {
				DialogResult=DialogResult.Cancel;
				return;
			}
			if(!Security.IsAuthorized(EnumPermType.Setup)) {
				return;
			}
			if(!MsgBox.Show(this,MsgBoxButtons.OKCancel,"Delete?")) {
				return;
			}
			string error=EhrProvKeys.DeleteChecked(_keyCur,out bool isPrefChanged);
			if(error!="") {
				MessageBox.Show(error);
				return;
			}
			if(isPrefChanged) {
				DataValid.SetInvalid(InvalidType.Prefs);
			}
			DialogResult=DialogResult.OK;
		}

		///<summary>This open build has no licence keys (section 5). A row is saved when a provider has exactly the last and first names typed and the
		///year has two digits (EhrProvKeys.GetSaveError); the key text is optional, stored as typed and never checked. Saving needs the Setup
		///permission (section 5.1). EhrProvKeys.SaveChecked checks both again, turns the radiology procedures alert on for the table's first row, as
		///before, and writes the Setup security log entry.</summary>
		private void butSave_Click(object sender,EventArgs e) {
			if(textYear.Text==""){
				MessageBox.Show("Please enter a year.");
				return;
			}
			if(!textYear.IsValid()) {
				MessageBox.Show("Invalid year, must be two digits.");
				return;
			}
			if(!Security.IsAuthorized(EnumPermType.Setup)) {
				return;
			}
			EhrProvKey ehrProvKey=_keyCur.Copy();
			ehrProvKey.LName=textLName.Text;
			ehrProvKey.FName=textFName.Text;
			ehrProvKey.YearValue=PIn.Int(textYear.Text);
			ehrProvKey.ProvKey=textKey.Text;
			string error=EhrProvKeys.SaveChecked(ehrProvKey,_keyCur,out bool isPrefChanged);
			if(error!="") {
				MessageBox.Show(error);
				return;
			}
			_keyCur=ehrProvKey;
			if(isPrefChanged) {
				DataValid.SetInvalid(InvalidType.Prefs);
			}
			DialogResult=DialogResult.OK;
		}

	}
}