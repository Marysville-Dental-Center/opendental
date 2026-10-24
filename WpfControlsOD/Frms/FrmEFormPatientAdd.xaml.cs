using System;
using System.Windows.Input;
using OpenDentBusiness;
using WpfControls.UI;

namespace OpenDental {
	///<summary>Second popup of an eForm Patient List field (Open Dental 25.2): first name, last name and birthdate of a new family member, all required.
	///OK creates the person in the form patient's family (EFormInsPick.AddFamilyMember), which logs PatientCreate.</summary>
	public partial class FrmEFormPatientAdd:FrmODBase {
		private long _patNumForm;
		private long _eFormNum;
		///<summary>After OK, the new person's PatNum.</summary>
		public long PatNumCreated;

		public FrmEFormPatientAdd(long patNumForm,long eFormNum) {
			InitializeComponent();
			_patNumForm=patNumForm;
			_eFormNum=eFormNum;
			Load+=FrmEFormPatientAdd_Load;
			PreviewKeyDown+=FrmEFormPatientAdd_PreviewKeyDown;
		}

		private void FrmEFormPatientAdd_Load(object sender,EventArgs e) {
			Lang.F(this);
		}

		private void FrmEFormPatientAdd_PreviewKeyDown(object sender,KeyEventArgs e) {
			if(butOK.IsAltKey(Key.O,e)) {
				butOK_Click(this,new EventArgs());
			}
		}

		private void butOK_Click(object sender,EventArgs e) {
			if(!textBirthdate.IsValid()) {
				MsgBox.Show(this,"Please fix the birthdate first.");
				return;
			}
			DateTime birthdate=PIn.Date(textBirthdate.Text);
			string error=EFormInsPick.ValidateNewFamilyMember(textFName.Text,textLName.Text,birthdate);
			if(error!="") {
				MsgBox.Show(error);
				return;
			}
			if(!Security.IsAuthorized(EnumPermType.PatientCreate)) {
				return;
			}
			Patient patient=EFormInsPick.AddFamilyMember(_patNumForm,textFName.Text,textLName.Text,birthdate,_eFormNum);
			PatNumCreated=patient.PatNum;
			IsDialogOK=true;
		}

		private void butCancel_Click(object sender,EventArgs e) {
			IsDialogOK=false;
		}
	}
}
