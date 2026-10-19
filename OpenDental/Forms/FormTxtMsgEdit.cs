using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using OpenDentBusiness;

namespace OpenDental {
	///<summary>Sends a text message through the CallFire program link. Open Dental's hosted Integrated Texting was removed.</summary>
	public partial class FormTxtMsgEdit:FormODBase {
		///<summary>PatNum of the patient selected.  Required if sending message without loading form or loading with patient selected.</summary>
		public long PatNum=0;
		///<summary>WirelessPhone of the patient selected.  Required if sending message without loading form.
		///Once form is loaded uses textWirelessPhone.Text instead.</summary>
		public string WirelessPhone;
		///<summary>Message text to be sent.  Required if sending message without loading form.  Once form is loaded uses textMessage.Text instead.</summary>
		public string Message;
		///<summary>TxtMsgOk status of the patient.  Required if sending message without loading form or loading with patient selected.</summary>
		public YN YNTxtMsgOk;

		public FormTxtMsgEdit() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormTxtMsgEdit_Load(object sender,EventArgs e) {
			textWirelessPhone.Text=WirelessPhone;
			textMessage.Text=Message;
			SetMessageCounts();
			if(PatNum==0) {
				radioPatient.Checked=false;
				radioOther.Checked=true;
				butPatFind.Enabled=false;
				textPatient.Enabled=false;
				textWirelessPhone.ReadOnly=false;
				textPatient.Text="";
				textWirelessPhone.Text="";
				SetFilterControlsAndAction(() => SetMessageCounts(),0,textMessage);
				return;
			}
			radioPatient.Checked=true;
			radioOther.Checked=false;
			butPatFind.Enabled=true;
			textPatient.Enabled=true;
			textWirelessPhone.ReadOnly=true;
			textPatient.Text=Patients.GetPat(PatNum).GetNameLF();
			textWirelessPhone.Text=WirelessPhone;
			SetFilterControlsAndAction(() => SetMessageCounts(),0,textMessage);
		}

		private void SetMessageCounts() {
			textCharCount.Text=textMessage.TextLength.ToString();
		}

		private void FormTxtMsgEdit_Shown(object sender,EventArgs e) {
			textMessage.Focus();
			textMessage.Select(textMessage.Text.Length,0);
		}

		/// <summary>May be called from other parts of the program without showing this form. You must still create an instance of this form though.
		/// Checks CallFire bridge, if it is OK to send a text, etc. (Buttons to load this form are usually disabled if it is not OK,
		/// but this is needed for Confirmations, Recalls, etc.).</summary>
		public bool SendText(long patNum,string wirelessPhone,string message,YN yNTxtMsgOk) {
			if(Plugins.HookMethod(this,"FormTxtMsgEdit.SendText_Start",patNum,wirelessPhone,message,yNTxtMsgOk)) {
				return false;
			}
			if(Plugins.HookMethod(this,"FormTxtMsgEdit.SendText_Start2",patNum,wirelessPhone,message,yNTxtMsgOk)) {
				return true;
			}
			if(wirelessPhone=="") {
				MsgBox.Show(this,"Please enter a phone number.");
				return false;
			}
			if(!Programs.IsEnabled(ProgramName.CallFire)) {
				MsgBox.Show(this,"CallFire Program Link must be enabled.");
				return false;
			}
			if(patNum!=0 && yNTxtMsgOk==YN.Unknown && PrefC.GetBool(PrefName.TextMsgOkStatusTreatAsNo)){
				MsgBox.Show(this,"It is not OK to text this patient.");
				return false;
			}
			if(patNum!=0 && yNTxtMsgOk==YN.No) {
				MsgBox.Show(this,"It is not OK to text this patient.");
				return false;
			}
			if(message.Length>160) {//only a limitation for CallFire
				MsgBox.Show(this,"Text length must be less than 160 characters.");
				return false;
			}
			return SendCallFire(patNum,wirelessPhone,message);  //Can pass in 0 as PatNum if no patient selected.
		}

		///<summary>Sends text message to callfire.  If patNum=0 will not create commlog entry.</summary>
		private bool SendCallFire(long patNum,string wirelessPhone,string message) {
			string key=ProgramProperties.GetPropVal(ProgramName.CallFire,"Key From CallFire");
			string msg=wirelessPhone+","+message.Replace(",","");//ph#,msg Commas in msg cause error.
			try {
				CallFireService.SMSService sMSService=new CallFireService.SMSService();
				sMSService.sendSMSCampaign(
					key,
					new string[] { msg },
					"Open Dental");
			}
			catch(Exception ex) {
				MsgBox.Show(this,"Error sending text message.\r\n\r\n"+ex.Message);
				return false;
			}
			if(patNum==0) {  //No patient selected, do not make commlog.
				return true;
			}
			Commlog commlog=new Commlog();
			commlog.CommDateTime=DateTime.Now;
			commlog.DateTStamp=DateTime.Now;
			commlog.CommType=Commlogs.GetTypeAuto(CommItemTypeAuto.TEXT);
			commlog.Mode_=CommItemMode.Text;
			commlog.Note=msg;//phone,note
			commlog.PatNum=patNum;
			commlog.SentOrReceived=CommSentOrReceived.Sent;
			commlog.UserNum=Security.CurUser.UserNum;
			commlog.DateTimeEnd=DateTime.Now;
			Commlogs.Insert(commlog);
			SecurityLogs.MakeLogEntry(EnumPermType.CommlogEdit,commlog.PatNum,"Insert Text Message");
			return true;
		}

		private void butPatFind_Click(object sender,EventArgs e) {
			FrmPatientSelect frmPatientSelect=new FrmPatientSelect();
			frmPatientSelect.ShowDialog();
			if(frmPatientSelect.IsDialogCancel) {
				return;
			}
			Patient patient=Patients.GetPat(frmPatientSelect.PatNumSelected);
			PatNum=patient.PatNum;
			YNTxtMsgOk=patient.TxtMsgOk;
			textPatient.Text=patient.GetNameLF();
			textWirelessPhone.Text=patient.WirelessPhone;
		}

		private void radioOther_Click(object sender,EventArgs e) {
			if(!textWirelessPhone.ReadOnly) {//The user clicked the "Another Person" radio button multiple times consecutively.
				return;//Leave so that the phone number is not wiped out unnecessarily.
			}
			butPatFind.Enabled=false;
			textPatient.Enabled=false;
			textWirelessPhone.ReadOnly=false;
			textPatient.Text="";
			textWirelessPhone.Text="";
			PatNum=0;
			YNTxtMsgOk=YN.Unknown;
		}

		private void radioPatient_Click(object sender,EventArgs e) {
			if(textWirelessPhone.ReadOnly) {//The user clicked the "Patient" radio button multiple times consecutively.
				return;//Leave so that the phone number is not wiped out unnecessarily.
			}
			butPatFind.Enabled=true;
			textPatient.Enabled=true;
			textWirelessPhone.ReadOnly=true;
			textPatient.Text="";
			textWirelessPhone.Text="";
			PatNum=0;
			YNTxtMsgOk=YN.Unknown;
		}

		private void butSend_Click(object sender,EventArgs e) {
			if(textMessage.Text=="") {
				MsgBox.Show(this,"Please enter a message first.");
				return;
			}
			if(radioOther.Checked) {  //No patient selected
				if(textWirelessPhone.Text=="") {
					MsgBox.Show(this,"Please enter a phone number first.");
					return;
				}
				if(!MsgBox.Show(this,MsgBoxButtons.OKCancel,"You do not have a patient selected.  If you are sending a message to an existing "
						+"patient you should choose the Patient option and use the find button.  If you proceed no commlog entry "
						+"will be created.  Continue?")) {
					return;
				}
				if(!SendText(0,textWirelessPhone.Text,textMessage.Text,YN.Unknown)) {  //0 as PatNum to denote no pat specified
					return;//Allow the user to try again.  A message was already shown to the user inside SendText().
				}
				DialogResult=DialogResult.OK;
				return;
			}
			//Patient selected
			if(PatNum==0) {
				MsgBox.Show(this,"You must first select a patient with the find button, or use the Another Person option.");
				return;
			}
			if(textWirelessPhone.Text=="") {
				MsgBox.Show(this,"This patient has no wireless phone entered.  You must add a wireless phone number to their patient account first before "
					+"you can send a text message.");
				return;
			}
			if(!SendText(PatNum,textWirelessPhone.Text,textMessage.Text,YNTxtMsgOk)) {
				return;//Allow the user to try again.  A message was already shown to the user inside SendText().
			}
			DialogResult=DialogResult.OK;
		}

	}
}
