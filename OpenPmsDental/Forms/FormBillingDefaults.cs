using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Linq;
using OpenPmsDentalBusiness;
using System.IO;
using CodeBase;

namespace OpenPmsDental {
	///<summary>Billing Defaults. Since Open Dental 26.2 (P262-BILLING, docs/port-notes/P262-BILLING.md) the electronic billing settings have their own
	///window (FormEbillSetup, the Electronic Billing button), and the messages (General Message, Email Statements, Invoice Note) can be set per clinic
	///while clinics are on (BillingDefaultsByClinic). The Statements box stays practice-wide. Saving a clinic writes only what changed for it
	///(BillingDefaultsByClinic.SaveSettings with the settings as read), so a message it has no row for keeps following the practice defaults.</summary>
	public partial class FormBillingDefaults:FormODBase {
		///<summary>Key: ClinicNum (0 = the practice defaults). The settings as they were read, for the clinics shown so far.</summary>
		private Dictionary<long,BillingDefaultsSettings> _dictionarySettingsOld=new Dictionary<long,BillingDefaultsSettings>();
		///<summary>Key: ClinicNum (0 = the practice defaults). The settings as edited, for the clinics shown so far.</summary>
		private Dictionary<long,BillingDefaultsSettings> _dictionarySettings=new Dictionary<long,BillingDefaultsSettings>();
		///<summary>Key: ClinicNum. The messages the clinic had a row of its own for when the window read its settings. Every other message of a
		///clinic with settings of its own follows the practice defaults (BillingDefaultsByClinic.FollowPracticeDefaults).</summary>
		private Dictionary<long,List<PrefName>> _dictionaryPrefNamesWithRows=new Dictionary<long,List<PrefName>>();
		///<summary>The clinic whose messages the window shows. 0 = the practice defaults (always 0 while clinics are off).</summary>
		private long _clinicNumCur;
		///<summary>True while the window fills the message boxes, so their events do nothing.</summary>
		private bool _isLoadingSettings;

		public FormBillingDefaults() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormBillingDefaults_Load(object sender,EventArgs e) {
			textDays.Text=PrefC.GetLong(PrefName.BillingDefaultsLastDays).ToString();
			checkIntermingled.Checked=PrefC.GetBool(PrefName.BillingDefaultsIntermingle);
			checkSinglePatient.Checked=PrefC.GetBool(PrefName.BillingDefaultsSinglePatient);
			checkBoxBillShowTransSinceZero.Checked=PrefC.GetBool(PrefName.BillingShowTransSinceBalZero);
			checkInserterBarcode.Checked=StatementInserterBarcode.IsEnabled();//25.3
			FillBillingEmailType();
			if(PrefC.HasClinicsEnabled) {
				groupStatements.Text=Lan.g(this,"Statements (all clinics)");
				if(Clinics.ClinicNum==0) {
					comboClinic.IsUnassignedSelected=true;
				}
				else {
					comboClinic.ClinicNumSelected=Clinics.ClinicNum;
				}
			}
			_clinicNumCur=GetClinicNumSelected();
			ShowSettings(_clinicNumCur);
		}

		///<summary>The clinic picked in comboClinic, 0 for the practice defaults or when clinics are off.</summary>
		private long GetClinicNumSelected() {
			if(!PrefC.HasClinicsEnabled) {
				return 0;
			}
			long clinicNum=comboClinic.ClinicNumSelected;
			if(clinicNum<0) {
				return 0;
			}
			return clinicNum;
		}

		///<summary>The edited settings of the clinic, read from the database the first time the clinic is shown.</summary>
		private BillingDefaultsSettings GetSettings(long clinicNum) {
			if(!_dictionarySettings.ContainsKey(clinicNum)) {
				BillingDefaultsSettings settings=BillingDefaultsByClinic.GetSettings(clinicNum);
				_dictionarySettingsOld[clinicNum]=settings.Copy();
				_dictionarySettings[clinicNum]=settings;
				_dictionaryPrefNamesWithRows[clinicNum]=BillingDefaultsByClinic.GetPrefNamesWithRows(clinicNum);
			}
			return _dictionarySettings[clinicNum];
		}

		///<summary>Fills the Messages box with the clinic's settings. A clinic that uses the practice defaults shows them, read-only. A clinic with
		///settings of its own shows, for each message it has no row for and that was not changed here, the practice value as it is now in this
		///window, since that is the value it uses (and saving it does not write it).</summary>
		private void ShowSettings(long clinicNum) {
			_isLoadingSettings=true;
			BillingDefaultsSettings settings=GetSettings(clinicNum);
			if(clinicNum>0 && !settings.UsePracticeDefaults) {
				BillingDefaultsByClinic.FollowPracticeDefaults(settings,_dictionarySettingsOld[clinicNum],GetSettings(0),_dictionaryPrefNamesWithRows[clinicNum]);
			}
			BillingDefaultsSettings settingsShown=settings;
			if(clinicNum>0 && settings.UsePracticeDefaults) {
				settingsShown=GetSettings(0);
			}
			checkUsePracticeDefaults.Visible=(clinicNum>0);
			checkUsePracticeDefaults.Checked=(clinicNum>0 && settings.UsePracticeDefaults);
			textNote.Text=settingsShown.GeneralMessage;
			textBillingEmailSubject.Text=settingsShown.EmailSubject;
			textBillingEmailBody.Text=settingsShown.EmailBody;
			comboBillingEmailType.SetSelectedEnum(settingsShown.EmailType);
			checkBillingEmailIncludeAutograph.Checked=settingsShown.IncludeAutograph;
			textInvoiceNote.Text=settingsShown.InvoiceNote;
			bool isEditable=!(clinicNum>0 && settings.UsePracticeDefaults);
			textNote.ReadOnly=!isEditable;
			textBillingEmailSubject.ReadOnly=!isEditable;
			textBillingEmailBody.ReadOnly=!isEditable;
			comboBillingEmailType.Enabled=isEditable;
			butBillingEmailEditHtml.Enabled=isEditable;
			checkBillingEmailIncludeAutograph.Enabled=isEditable;
			textInvoiceNote.ReadOnly=!isEditable;
			if(!PrefC.HasClinicsEnabled) {
				groupMessages.Text=Lan.g(this,"Messages");
			}
			else if(clinicNum==0) {
				groupMessages.Text=Lan.g(this,"Messages - Practice Defaults");
			}
			else if(settings.UsePracticeDefaults) {
				groupMessages.Text=Lan.g(this,"Messages - Practice Defaults, used by")+" "+Clinics.GetAbbr(clinicNum);
			}
			else {
				groupMessages.Text=Lan.g(this,"Messages -")+" "+Clinics.GetAbbr(clinicNum);
			}
			_isLoadingSettings=false;
		}

		///<summary>Copies the Messages box into the settings of the clinic shown. Nothing is copied while the clinic uses the practice defaults.</summary>
		private void StoreSettings() {
			BillingDefaultsSettings settings=GetSettings(_clinicNumCur);
			if(_clinicNumCur>0 && settings.UsePracticeDefaults) {
				return;
			}
			settings.GeneralMessage=textNote.Text;
			settings.EmailSubject=textBillingEmailSubject.Text;
			settings.EmailBody=textBillingEmailBody.Text;
			settings.EmailType=comboBillingEmailType.GetSelected<EmailType>();
			settings.IncludeAutograph=checkBillingEmailIncludeAutograph.Checked;
			settings.InvoiceNote=textInvoiceNote.Text;
		}

		private void comboClinic_SelectionChangeCommitted(object sender,EventArgs e) {
			StoreSettings();
			_clinicNumCur=GetClinicNumSelected();
			ShowSettings(_clinicNumCur);
		}

		private void checkUsePracticeDefaults_Click(object sender,EventArgs e) {
			if(_isLoadingSettings || _clinicNumCur<=0) {
				return;
			}
			BillingDefaultsSettings settings=GetSettings(_clinicNumCur);
			if(checkUsePracticeDefaults.Checked) {
				StoreSettings();//Keeps what was typed, in case the box is unchecked again before saving.
				settings.UsePracticeDefaults=true;
			}
			else {
				settings.UsePracticeDefaults=false;
				if(_dictionarySettingsOld[_clinicNumCur].UsePracticeDefaults && settings.IsSameAs(StartFromPractice(_dictionarySettingsOld[_clinicNumCur]))) {
					//The clinic had no settings of its own: start from the practice defaults as they are now in this window.
					CopyMessages(GetSettings(0),settings);
				}
			}
			ShowSettings(_clinicNumCur);
		}

		///<summary>The old settings of a clinic that used the practice defaults, as they look once it stops using them without changes.</summary>
		private static BillingDefaultsSettings StartFromPractice(BillingDefaultsSettings settingsOld) {
			BillingDefaultsSettings settings=settingsOld.Copy();
			settings.UsePracticeDefaults=false;
			return settings;
		}

		private static void CopyMessages(BillingDefaultsSettings settingsFrom,BillingDefaultsSettings settingsTo) {
			settingsTo.GeneralMessage=settingsFrom.GeneralMessage;
			settingsTo.EmailSubject=settingsFrom.EmailSubject;
			settingsTo.EmailBody=settingsFrom.EmailBody;
			settingsTo.EmailType=settingsFrom.EmailType;
			settingsTo.IncludeAutograph=settingsFrom.IncludeAutograph;
			settingsTo.InvoiceNote=settingsFrom.InvoiceNote;
		}

		private void butEbill_Click(object sender,EventArgs e) {
			using FormEbillSetup formEbillSetup=new FormEbillSetup();
			formEbillSetup.ShowDialog();
		}

		#region Email type (Open Dental 26.1)
		///<summary>The billing e-mail body can be plain text, HTML inside the e-mail master template, or raw HTML (preference BillingEmailType, and
		///since 26.2 a clinic's own BillingEmailType row). ShowSettings selects the type of the clinic shown.</summary>
		private void FillBillingEmailType() {
			comboBillingEmailType.Items.Add(Lan.g(this,"Plain Text"),EmailType.Regular);
			comboBillingEmailType.Items.Add(Lan.g(this,"HTML"),EmailType.Html);
			comboBillingEmailType.Items.Add(Lan.g(this,"Raw HTML"),EmailType.RawHtml);
			comboBillingEmailType.SetSelectedEnum(BillingEmailHtml.GetBillingDefaultsEmailType());
		}

		///<summary>Opens the HTML editor and preview that e-mail templates use. Saving there makes the body HTML, or raw HTML when that box is
		///checked.</summary>
		private void butBillingEmailEditHtml_Click(object sender,EventArgs e) {
			using FormEmailEdit formEmailEdit=new FormEmailEdit();
			formEmailEdit.MarkupText=textBillingEmailBody.Text;
			formEmailEdit.IsRaw=comboBillingEmailType.GetSelected<EmailType>()==EmailType.RawHtml;
			formEmailEdit.ShowDialog();
			if(formEmailEdit.DialogResult!=DialogResult.OK) {
				return;
			}
			textBillingEmailBody.Text=formEmailEdit.MarkupText;
			if(formEmailEdit.IsRaw) {
				comboBillingEmailType.SetSelectedEnum(EmailType.RawHtml);
			}
			else {
				comboBillingEmailType.SetSelectedEnum(EmailType.Html);
			}
		}

		///<summary>Checks the e-mail body of every changed settings (the practice defaults and each clinic with its own) against its type, and shows
		///the clinic whose body is not valid HTML.</summary>
		private bool IsBillingEmailBodyValid() {
			foreach(KeyValuePair<long,BillingDefaultsSettings> pair in _dictionarySettings) {
				BillingDefaultsSettings settings=pair.Value;
				if(settings.UsePracticeDefaults || settings.IsSameAs(_dictionarySettingsOld[pair.Key])) {
					continue;
				}
				string error=BillingEmailHtml.GetBodyError(settings.EmailBody,settings.EmailType);
				if(error=="") {
					continue;
				}
				if(pair.Key!=_clinicNumCur && PrefC.HasClinicsEnabled) {
					if(pair.Key==0) {
						comboClinic.IsUnassignedSelected=true;
					}
					else {
						comboClinic.ClinicNumSelected=pair.Key;
					}
					_clinicNumCur=GetClinicNumSelected();
					ShowSettings(_clinicNumCur);
				}
				MessageBox.Show(Lan.g(this,"The email body is not valid HTML. Fix it with Edit HTML, or choose Plain Text.")+"\r\n"+error);
				return false;
			}
			return true;
		}
		#endregion Email type (Open Dental 26.1)

		private void butSave_Click(object sender,EventArgs e) {
			if(!textDays.IsValid()) {
				MsgBox.Show(this,"Please fix data entry errors first.");
				return;
			}
			if(checkSinglePatient.Checked && checkIntermingled.Checked) {
				MsgBox.Show(this,"Cannot select both 'Intermingle family members' and 'Single patient only' as defaults.");
				return;
			}
			StoreSettings();
			if(!IsBillingEmailBodyValid()) {
				return;
			}
			if(Prefs.UpdateLong(PrefName.BillingDefaultsLastDays,PIn.Long(textDays.Text))
				| Prefs.UpdateBool(PrefName.BillingDefaultsIntermingle,checkIntermingled.Checked)
				| Prefs.UpdateBool(PrefName.BillingDefaultsSinglePatient,checkSinglePatient.Checked)
				| Prefs.UpdateBool(PrefName.BillingShowTransSinceBalZero,checkBoxBillShowTransSinceZero.Checked)
				| StatementInserterBarcode.SetEnabled(checkInserterBarcode.Checked))//25.3
			{
				DataValid.SetInvalid(InvalidType.Prefs);
			}
			//The messages: the practice defaults first, then each clinic that changed (P262-BILLING).
			bool isPrefChanged=false;
			bool isClinicPrefChanged=false;
			foreach(long clinicNum in _dictionarySettings.Keys.OrderBy(x => x)) {
				BillingDefaultsSettings settings=_dictionarySettings[clinicNum];
				if(settings.IsSameAs(_dictionarySettingsOld[clinicNum])) {
					continue;
				}
				if(!BillingDefaultsByClinic.SaveSettings(settings,_dictionarySettingsOld[clinicNum])) {
					continue;
				}
				if(clinicNum==0) {
					isPrefChanged=true;
				}
				else {
					isClinicPrefChanged=true;
				}
			}
			if(isPrefChanged) {
				DataValid.SetInvalid(InvalidType.Prefs);
			}
			if(isClinicPrefChanged) {
				DataValid.SetInvalid(InvalidType.ClinicPrefs);
			}
			DialogResult=DialogResult.OK;
		}

	}
}
