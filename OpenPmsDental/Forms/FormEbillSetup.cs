// The Electronic Billing window (Open Dental 26.2 release notes: "Moved Electronic Billing settings to a new window"; package P262-BILLING,
// docs/port-notes/P262-BILLING.md). The settings, their storage and their behaviour are the ones of the Electronic Billing box Billing Defaults had
// before 26.2; the code moved here from FormBillingDefaults. Opened from Billing Defaults and from the Billing List's Defaults button, which before
// 26.2 opened Billing Defaults limited to this box.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CodeBase;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	public partial class FormEbillSetup:FormODBase {
		private List<Ebill> _listEbills;
		///<summary>Stale deep copy of _listEbills to use with sync.</summary>
		private List<Ebill> _listEbillsOld;
		///<summary>The eBill corresponding to the currently selected clinic if clinics are enabled.</summary>
		private Ebill _eBillCur;
		///<summary>The eBill corresponding to the default credentials.</summary>
		private Ebill _eBillDefault;
		///<summary>Corresponds to the output path/url of the 5 items in listElectBilling</summary>
		private string[] _stringArrayOutputPaths=new string[5];

		public FormEbillSetup() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormEbillSetup_Load(object sender,EventArgs e) {
			_listEbills=Ebills.GetDeepCopy();
			_listEbillsOld=_listEbills.Select(x => x.Copy()).ToList();
			//Find the default Ebill
			for(int i=0;i<_listEbills.Count;i++) {
				if(_listEbills[i].ClinicNum==0) {
					_eBillDefault=_listEbills[i];
				}
			}
			if(_eBillDefault==null) {
				MsgBox.Show(this,"The default ebill entry is missing. Run "+nameof(DatabaseMaintenances.EbillMissingDefaultEntry)
					+" in the Database Maintenance Tool before continuing.");
				DialogResult=DialogResult.Cancel;
				return;
			}
			checkCreatePDF.Checked=PrefC.GetBool(PrefName.BillingElectCreatePDF);
			checkIncludeAdjust.Checked=PrefC.GetBool(PrefName.BillingElectIncludeAdjustDescript);
			checkIncludeClinicNum.Checked=PrefC.GetBool(PrefName.BillingElectIncludeClinicNums);
			_stringArrayOutputPaths[0]="";//Will never be used, but is helpful to keep the indexes of arrayOutputPaths aligned with the options listed in listElectBilling.
			_stringArrayOutputPaths[1]=PrefC.GetString(PrefName.BillingElectStmtUploadURL);
			_stringArrayOutputPaths[2]=PrefC.GetString(PrefName.BillingElectStmtOutputPathPos);
			_stringArrayOutputPaths[3]=PrefC.GetString(PrefName.BillingElectStmtOutputPathClaimX);
			_stringArrayOutputPaths[4]=PrefC.GetString(PrefName.BillingElectStmtOutputPathEds);
			listElectBilling.Items.AddList(EbillSettings.ArrayMethodNames.Select(x => Lan.g(this,x)).ToArray(),x => x);
			int billingUseElectronicIdx=(int)PrefC.GetEnum<BillingUseElectronicEnum>(PrefName.BillingUseElectronic);
			if(billingUseElectronicIdx<0 || billingUseElectronicIdx>=EbillSettings.ArrayMethodNames.Length) {
				billingUseElectronicIdx=0;
			}
			listElectBilling.SelectedIndex=billingUseElectronicIdx;//Sets the boxes the method uses (listElectBilling_SelectedIndexChanged).
			textStatementURL.Text=_stringArrayOutputPaths[billingUseElectronicIdx];
			textVendorId.Text=PrefC.GetString(PrefName.BillingElectVendorId);
			textVendorPMScode.Text=PrefC.GetString(PrefName.BillingElectVendorPMSCode);
			EbillSettings.ParseCreditCardChoices(PrefC.GetString(PrefName.BillingElectCreditCardChoices),
				out bool isMasterCard,out bool isVisa,out bool isDiscover,out bool isAmEx);
			checkMC.Checked=isMasterCard;
			checkV.Checked=isVisa;
			checkD.Checked=isDiscover;
			checkAmEx.Checked=isAmEx;
			_eBillCur=_eBillDefault;
			string[] stringArrayEbillAddressEnums=Enum.GetNames(typeof(EbillAddress));
			for(int i=0;i<stringArrayEbillAddressEnums.Length;i++) {
				comboPracticeAddr.Items.Add(stringArrayEbillAddressEnums[i]);
				comboRemitAddr.Items.Add(stringArrayEbillAddressEnums[i]);
				//If clinics are off don't add the Clinic specific EbillAddress enums
				if(!PrefC.HasClinicsEnabled && i==2) {
					break;
				}
			}
			if(PrefC.HasClinicsEnabled) {
				//Bold clinic specific fields.
				groupBoxBilling.Text=Lan.g(this,"Electronic Billing - Bolded fields are clinic specific");
				labelAcctNum.Font=new Font(labelAcctNum.Font,FontStyle.Bold);
				labelUserName.Font=new Font(labelUserName.Font,FontStyle.Bold);
				labelPassword.Font=new Font(labelPassword.Font,FontStyle.Bold);
				labelPracticeAddr.Font=new Font(labelPracticeAddr.Font,FontStyle.Bold);
				labelRemitAddr.Font=new Font(labelRemitAddr.Font,FontStyle.Bold);
				comboClinic.ClinicNumSelected=Clinics.ClinicNum;
				Ebill eBill=null;
				if(Clinics.ClinicNum==0) {//Use the default Ebill if OD has Headquarters selected or if clinics are disabled.
					eBill=_eBillDefault;
				}
				else {
					eBill=_listEbills.FirstOrDefault(x => x.ClinicNum==comboClinic.ClinicNumSelected);//Can be null.
				}
				//_eBillCur will be the default Ebill, the clinic's Ebill, or null if there are no existing ebills for OD's selected clinic.
				_eBillCur=eBill;
			}
			//Load _eBillCur's fields into the UI.
			LoadEbill(_eBillCur);
		}

		///<summary>Used to handle the encrypting and decrypting of a password in this form. Assumes that decryption is needed unless isDecrypted is set to true</summary>
		private string GetPassword(string eBillPassword,bool isDecrypted=false) {
			string returnValue=eBillPassword;
			if(isDecrypted) {
				if(Secrets.Encrypt(eBillPassword,out string encryptedPassword)) {
					returnValue=encryptedPassword;
				}
			}
			else {
				if(Secrets.Decrypt(eBillPassword,out string plainPassword)) {
					returnValue=plainPassword;
				}
			}
			return returnValue;
		}

		///<summary>eBill can be null, creates Ebill if needed.</summary>
		private void LoadEbill(Ebill eBill) {
			if(eBill==null) {//Matching Ebill entry not found.  Make a new entry with default values.
				eBill=new Ebill();
				eBill.ClinicNum=comboClinic.ClinicNumSelected;
				eBill.ClientAcctNumber="";
				eBill.ElectUserName="";
				eBill.ElectPassword="";
				eBill.PracticeAddress=EbillAddress.PracticePhysical;
				eBill.RemitAddress=EbillAddress.PracticeBilling;
				_listEbills.Add(eBill);
			}
			textClientAcctNumber.Text=_eBillDefault.ClientAcctNumber;
			if(eBill.ClientAcctNumber!="") {//If the Ebill field is blank use default value.
				textClientAcctNumber.Text=eBill.ClientAcctNumber;
			}
			textUserName.Text=_eBillDefault.ElectUserName;
			if(eBill.ElectUserName!="") {//If the Ebill field is blank use default value.
				textUserName.Text=eBill.ElectUserName;
			}
			textPassword.Text=GetPassword(_eBillDefault.ElectPassword);
			if(eBill.ElectPassword!="") {//If the Ebill field is blank use default value.
				textPassword.Text=GetPassword(eBill.ElectPassword);
			}
			//If clinics are disabled and the eBill had a clinic specific enum, set it to default value.  May happen if clinics were previously enabled.
			if(PrefC.HasClinicsEnabled) {
				comboPracticeAddr.SelectedIndex=(int)eBill.PracticeAddress;
				comboRemitAddr.SelectedIndex=(int)eBill.RemitAddress;
			}
			else {//No clinics
				if(eBill.PracticeAddress==EbillAddress.ClinicPhysical) {
					comboPracticeAddr.SelectedIndex=0;//PracticePhysical
				}
				else if(eBill.PracticeAddress==EbillAddress.ClinicBilling) {
					comboPracticeAddr.SelectedIndex=1;//PracticeBilling
				}
				else if(eBill.PracticeAddress==EbillAddress.ClinicPayTo) {
					comboPracticeAddr.SelectedIndex=2;//PracticePayTo
				}
				else {
					comboPracticeAddr.SelectedIndex=(int)eBill.PracticeAddress;
				}
				if(eBill.RemitAddress==EbillAddress.ClinicPhysical) {
					comboRemitAddr.SelectedIndex=0;//PracticePhysical
				}
				else if(eBill.RemitAddress==EbillAddress.ClinicBilling) {
					comboRemitAddr.SelectedIndex=1;//PracticeBilling
				}
				else if(eBill.RemitAddress==EbillAddress.ClinicPayTo) {
					comboRemitAddr.SelectedIndex=2;//PracticePayTo
				}
				else {
					comboRemitAddr.SelectedIndex=(int)eBill.RemitAddress;
				}
			}
			_eBillCur=eBill;
		}

		///<summary>Saves the current Ebill information from the UI into the cache.</summary>
		private void SaveEbill(Ebill eBill) {
			if(eBill.ClinicNum==0) {//If the ebill being edited is for the defaults use what's in the text
				eBill.ClientAcctNumber=textClientAcctNumber.Text;
				eBill.ElectUserName=textUserName.Text;
				eBill.ElectPassword=GetPassword(textPassword.Text,isDecrypted:true);
			}
			else {//If the ebill isn't the default
				if(textClientAcctNumber.Text!="" && textClientAcctNumber.Text!=_eBillDefault.ClientAcctNumber) {
					eBill.ClientAcctNumber=textClientAcctNumber.Text;
				}
				else {//Text was blank or the same as the default, blank it.
					eBill.ClientAcctNumber="";
				}
				if(textUserName.Text!="" && textUserName.Text!=_eBillDefault.ElectUserName) {
					eBill.ElectUserName=textUserName.Text;
				}
				else {//Text was blank or the same as the default, blank it.
					eBill.ElectUserName="";
				}
				if(textPassword.Text!="" && textPassword.Text!=GetPassword(_eBillDefault.ElectPassword)) {
					eBill.ElectPassword=GetPassword(textPassword.Text,isDecrypted:true);
				}
				else {//Text was blank or the same as the default, blank it.
					eBill.ElectPassword="";
				}
			}
			eBill.PracticeAddress=(EbillAddress)comboPracticeAddr.SelectedIndex;
			eBill.RemitAddress=(EbillAddress)comboRemitAddr.SelectedIndex;
		}

		private void textStatementURL_KeyUp(object sender,KeyEventArgs e) {
			if(listElectBilling.SelectedIndex<0) {
				return;
			}
			_stringArrayOutputPaths[listElectBilling.SelectedIndex]=textStatementURL.Text;
		}

		private void listElectBilling_SelectedIndexChanged(object sender,EventArgs e) {
			//If Dental X Change is selected, enable its textboxes and combo.
			if(listElectBilling.SelectedIndex==1) {
				comboRemitAddr.Enabled=true;
				textUserName.ReadOnly=false;
				textPassword.ReadOnly=false;
				textClientAcctNumber.ReadOnly=false;
				textVendorId.ReadOnly=false;
				textVendorPMScode.ReadOnly=false;
				labelBlankForDefault.Visible=true;
				labelStatementURL.Text=Lan.g(this,"URL Override");
			}
			else {
				//If Dental X Change is not selected, disable changing information for fields the selected format won't use.
				comboRemitAddr.Enabled=false;
				textUserName.ReadOnly=true;
				textPassword.ReadOnly=true;
				textClientAcctNumber.ReadOnly=true;
				textVendorId.ReadOnly=true;
				textVendorPMScode.ReadOnly=true;
				labelBlankForDefault.Visible=false;
				labelStatementURL.Text=Lan.g(this,"Output Path");
			}
			if(listElectBilling.SelectedIndex==-1) {
				return;//This will happen when resizing due to LayoutManager
			}
			textStatementURL.Text=_stringArrayOutputPaths[listElectBilling.SelectedIndex];
			checkCreatePDF.Enabled=EbillSettings.CanCreatePdf(listElectBilling.SelectedIndex);
			checkIncludeClinicNum.Visible=PrefC.HasClinicsEnabled && listElectBilling.SelectedIndex==2;
		}

		private void comboClinic_SelectionChangeCommitted(object sender,EventArgs e) {
			SaveEbill(_eBillCur);
			Ebill eBill=null;
			if((!Security.CurUser.ClinicIsRestricted || Clinics.ClinicNum==0) && comboClinic.IsUnassignedSelected) {
				eBill=_eBillDefault;
			}
			else {//Otherwise locate the Ebill from the cache.
				for(int i=0;i<_listEbills.Count;i++) {
					if(_listEbills[i].ClinicNum==comboClinic.ClinicNumSelected) {//Check for existing Ebill entry
						eBill=_listEbills[i];
						break;
					}
				}
			}
			LoadEbill(eBill);//Could be null if user switches to a clinic which has not Ebill entry yet.
		}

		private void butSave_Click(object sender,EventArgs e) {
			int methodIndex=listElectBilling.SelectedIndex;
			if(methodIndex<0) {
				methodIndex=0;
			}
			if(EbillSettings.IsOutputPathRequired(methodIndex) && !Directory.Exists(textStatementURL.Text)){
				MsgBox.Show(this,"Please choose a valid Output Path.");
				return;
			}
			string creditCard=EbillSettings.FormatCreditCardChoices(checkMC.Checked,checkV.Checked,checkD.Checked,checkAmEx.Checked);
			string billingUseElectronic=methodIndex.ToString();
			SaveEbill(_eBillCur);
			if(methodIndex==1 && string.IsNullOrEmpty(textStatementURL.Text)) {
				textStatementURL.Text=EbillSettings.DXC_URL_DEFAULT;
			}
			if(Prefs.UpdateString(PrefName.BillingUseElectronic,billingUseElectronic)
				| Prefs.UpdateString(PrefName.BillingElectVendorId,textVendorId.Text)
				| Prefs.UpdateString(PrefName.BillingElectVendorPMSCode,textVendorPMScode.Text)
				| Prefs.UpdateString(PrefName.BillingElectCreditCardChoices,creditCard)
				| Prefs.UpdateBool(PrefName.BillingElectCreatePDF,checkCreatePDF.Checked)
				| (methodIndex==1 && Prefs.UpdateString(PrefName.BillingElectStmtUploadURL,textStatementURL.Text))
				| (methodIndex==2 && Prefs.UpdateString(PrefName.BillingElectStmtOutputPathPos,textStatementURL.Text))
				| (methodIndex==2 && Prefs.UpdateBool(PrefName.BillingElectIncludeClinicNums,checkIncludeClinicNum.Checked))
				| (methodIndex==3 && Prefs.UpdateString(PrefName.BillingElectStmtOutputPathClaimX,textStatementURL.Text))
				| (methodIndex==4 && Prefs.UpdateString(PrefName.BillingElectStmtOutputPathEds,textStatementURL.Text))
				| Prefs.UpdateBool(PrefName.BillingElectIncludeAdjustDescript,checkIncludeAdjust.Checked))
			{
				DataValid.SetInvalid(InvalidType.Prefs);
			}
			if(Ebills.Sync(_listEbills,_listEbillsOld)) {//Includes the default Ebill
				DataValid.SetInvalid(InvalidType.Ebills);//Also updates cache.
			}
			DialogResult=DialogResult.OK;
		}
	}
}
