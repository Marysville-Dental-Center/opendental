using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using OpenDentBusiness;
using WpfControls.UI;

namespace OpenDental {
	///<summary>The popup of an eForm Patient List field (Open Dental 25.2). Lists the form patient's family, read live, showing first and last name only
	///because the patient may be filling at a kiosk. Add creates a family member; it is hidden when the logged-in user lacks PatientCreate.</summary>
	public partial class FrmEFormPatientPick:FrmODBase {
		private long _patNumForm;
		private long _eFormNum;
		private List<Patient> _listPatients;
		///<summary>Set before showing to preselect a person. After OK, the picked PatNum.</summary>
		public long PatNumSelected;

		public FrmEFormPatientPick(long patNumForm,long eFormNum) {
			InitializeComponent();
			_patNumForm=patNumForm;
			_eFormNum=eFormNum;
			Load+=FrmEFormPatientPick_Load;
			listPatients.MouseDoubleClick+=listPatients_MouseDoubleClick;
			PreviewKeyDown+=FrmEFormPatientPick_PreviewKeyDown;
		}

		private void FrmEFormPatientPick_Load(object sender,EventArgs e) {
			Lang.F(this);
			if(!Security.IsAuthorized(EnumPermType.PatientCreate,suppressMessage:true)) {
				butAdd.Visible=false;
			}
			FillList();
		}

		private void FillList() {
			_listPatients=new List<Patient>();
			if(_patNumForm!=0) {
				Family family=Patients.GetFamily(_patNumForm);
				_listPatients=family.ListPats.Where(x => x.PatStatus!=PatientStatus.Deleted).ToList();
			}
			listPatients.Items.Clear();
			listPatients.Items.AddList(_listPatients,x => x.FName+" "+x.LName);
			int idx=_listPatients.FindIndex(x => x.PatNum==PatNumSelected);
			if(idx>=0) {
				listPatients.SetSelected(idx);
			}
		}

		private void butAdd_Click(object sender,EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.PatientCreate)) {
				return;
			}
			if(_patNumForm==0) {
				MsgBox.Show(this,"This form is not attached to a patient.");
				return;
			}
			FrmEFormPatientAdd frmEFormPatientAdd=new FrmEFormPatientAdd(_patNumForm,_eFormNum);
			frmEFormPatientAdd.ShowDialog();
			if(!frmEFormPatientAdd.IsDialogOK) {
				return;
			}
			PatNumSelected=frmEFormPatientAdd.PatNumCreated;
			FillList();
		}

		private void listPatients_MouseDoubleClick(object sender,MouseButtonEventArgs e) {
			if(listPatients.SelectedIndex==-1) {
				return;
			}
			PatNumSelected=listPatients.GetSelected<Patient>().PatNum;
			IsDialogOK=true;
		}

		private void FrmEFormPatientPick_PreviewKeyDown(object sender,KeyEventArgs e) {
			if(butOK.IsAltKey(Key.O,e)) {
				butOK_Click(this,new EventArgs());
			}
		}

		private void butOK_Click(object sender,EventArgs e) {
			if(listPatients.SelectedIndex==-1) {
				MsgBox.Show(this,"Please select a person first.");
				return;
			}
			PatNumSelected=listPatients.GetSelected<Patient>().PatNum;
			IsDialogOK=true;
		}

		private void butCancel_Click(object sender,EventArgs e) {
			IsDialogOK=false;
		}
	}
}
