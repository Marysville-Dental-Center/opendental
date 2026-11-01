using System;
using System.Collections.Generic;
using System.Windows.Forms;
using CodeBase;
using OpenDentBusiness;

namespace OpenDental{
	/// <summary>
	/// Summary description for FormBasicTemplate.
	/// </summary>
	public partial class FormDunningEdit : FormODBase {
		///<summary></summary>
		public bool IsNew;
		private Dunning _dunning;
		private List<Def> _listDefsBillingTypes;

		///<summary></summary>
		public FormDunningEdit(Dunning dunning)
		{
			//
			// Required for Windows Form Designer support
			//
			_dunning=dunning.Copy();
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormDunningEdit_Load(object sender, System.EventArgs e) {
			if(PrefC.HasClinicsEnabled) {
				if(_dunning.ClinicNum==-2) {
					comboClinics.IsAllSelected=true;
				}
				else {
					comboClinics.ClinicNumSelected=_dunning.ClinicNum;
				}
			}
			if(PrefC.GetBool(PrefName.ShowFeatureSuperfamilies)) {
				checkSuperFamily.Visible=true;
				checkSuperFamily.Checked=_dunning.IsSuperFamily;
			}
			listBillType.Items.Add(Lan.g(this,"all"));
			listBillType.SetSelected(0,true);
			_listDefsBillingTypes=Defs.GetDefsForCategory(DefCat.BillingTypes,true);
			for(int i=0;i<_listDefsBillingTypes.Count;i++){
				listBillType.Items.Add(_listDefsBillingTypes[i].ItemName);
				if(_dunning.BillingType==_listDefsBillingTypes[i].DefNum){
					listBillType.SetSelected(i+1,true);
				}
			}
			switch(_dunning.AgeAccount){
				case 0:
					radioAny.Checked=true;
					break;
				case 30:
					radio30.Checked=true;
					break;
				case 60:
					radio60.Checked=true;
					break;
				case 90:
					radio90.Checked=true;
					break;
			}
			switch(_dunning.InsIsPending){
				case YN.Unknown:
					radioU.Checked=true;
					break;
				case YN.Yes:
					radioY.Checked=true;
					break;
				case YN.No:
					radioN.Checked=true;
					break;
			}
			textDaysInAdvance.Text=_dunning.DaysInAdvance.ToString();
			textDunMessage.Text=_dunning.DunMessage;
			textMessageBold.Text=_dunning.MessageBold;
			textEmailBody.Text=_dunning.EmailBody;
			textEmailSubject.Text=_dunning.EmailSubject;
			FillEmailType();
		}

		///<summary>Since Open Dental 26.1 the e-mail override can be plain text, HTML inside the e-mail master template, or raw HTML.</summary>
		private void FillEmailType() {
			comboEmailType.Items.Add(Lan.g(this,"Plain Text"),EmailType.Regular);
			comboEmailType.Items.Add(Lan.g(this,"HTML"),EmailType.Html);
			comboEmailType.Items.Add(Lan.g(this,"Raw HTML"),EmailType.RawHtml);
			comboEmailType.SetSelectedEnum(BillingEmailHtml.ToEmailType((int)_dunning.EmailType));
		}

		///<summary>Opens the HTML editor and preview that e-mail templates use. Saving there makes the override HTML, or raw HTML when that box is
		///checked.</summary>
		private void butEditHtml_Click(object sender,EventArgs e) {
			using FormEmailEdit formEmailEdit=new FormEmailEdit();
			formEmailEdit.MarkupText=textEmailBody.Text;
			formEmailEdit.IsRaw=comboEmailType.GetSelected<EmailType>()==EmailType.RawHtml;
			formEmailEdit.ShowDialog();
			if(formEmailEdit.DialogResult!=DialogResult.OK) {
				return;
			}
			textEmailBody.Text=formEmailEdit.MarkupText;
			if(formEmailEdit.IsRaw) {
				comboEmailType.SetSelectedEnum(EmailType.RawHtml);
			}
			else {
				comboEmailType.SetSelectedEnum(EmailType.Html);
			}
		}

		private void radioAny_CheckedChanged(object sender,EventArgs e) {
				labelDaysInAdvance.Visible=!radioAny.Checked;
				textDaysInAdvance.Visible=!radioAny.Checked;
		}

		private void butDelete_Click(object sender, System.EventArgs e) {
			if(IsNew){
				DialogResult=DialogResult.Cancel;
			}
			else{
				Dunnings.Delete(_dunning);
				DialogResult=DialogResult.OK;
			}
		}

		private void butSave_Click(object sender, System.EventArgs e) {
			if(!textDaysInAdvance.IsValid()) {
				MsgBox.Show(this,"Please fix data entry errors first.");
				return;
			}
			if(textDunMessage.Text=="" 
				&& textMessageBold.Text==""
				&& textEmailSubject.Text=="" 
				&& textEmailBody.Text=="") 
			{
				MsgBox.Show(this,"All messages cannot be blank.");
				return;
			}
			string emailBodyError=BillingEmailHtml.GetBodyError(textEmailBody.Text,comboEmailType.GetSelected<EmailType>());
			if(emailBodyError!="") {
				MessageBox.Show(Lan.g(this,"The email body is not valid HTML. Fix it with Edit HTML, or choose Plain Text.")+"\r\n"+emailBodyError);
				return;
			}
			_dunning.BillingType=0;
			if(listBillType.SelectedIndex>0) {
				_dunning.BillingType=_listDefsBillingTypes[listBillType.SelectedIndex-1].DefNum;
			}
			_dunning.AgeAccount=(byte)(30*new List<RadioButton> { radioAny,radio30,radio60,radio90 }.FindIndex(x => x.Checked));//0, 30, 60, or 90
			_dunning.InsIsPending=(YN)new List<RadioButton> { radioU,radioY,radioN }.FindIndex(x => x.Checked);//0=Unknown, 1=Yes, 2=No
			_dunning.DaysInAdvance=0;//default will be 0
			if(!radioAny.Checked) {
				_dunning.DaysInAdvance=PIn.Int(textDaysInAdvance.Text);//blank=0
			}
			_dunning.DunMessage=textDunMessage.Text;
			_dunning.MessageBold=textMessageBold.Text;
			_dunning.EmailBody=textEmailBody.Text;
			_dunning.EmailSubject=textEmailSubject.Text;
			_dunning.EmailType=comboEmailType.GetSelected<EmailType>();
			_dunning.IsSuperFamily=checkSuperFamily.Checked;
			if(PrefC.HasClinicsEnabled) {
				_dunning.ClinicNum=comboClinics.ClinicNumSelected;
			}
			if(IsNew){
				Dunnings.Insert(_dunning);
			}
			else{
				Dunnings.Update(_dunning);
			}
			DialogResult=DialogResult.OK;
		}

	}
}