using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using OpenDentBusiness;
using OpenDentBusiness.VendorDirect;
using OpenDental.UI;
using System.Linq;
using System.Diagnostics;
using CodeBase;

namespace OpenDental {
	///<summary>PaySimple settings per clinic: the office's own merchant API user name and key (the key is stored as osd1$), payment types and the
	///environment. Also the credential test, the practice-wide hourly ACH status check, a manual ACH status check, and a review of the webhook
	///subscriptions in the PaySimple account (an office coming from Open Dental may still have subscriptions that send its payment events there).
	///Nothing here creates a webhook.</summary>
	public partial class FormPaySimpleSetup:FormODBase {

		private Program _program;
		///<summary>Local cache of all of the ClinicNums the current user has permission to access at the time the form loads.  Filled at the same time
		///as comboClinic and is used to set programproperty.ClinicNum when saving.</summary>
		private List<long> _listUserClinicNums;
		///<summary>List of PaySimple program properties for all clinics.
		///Includes properties with ClinicNum=0, the headquarters props or props not assigned to a clinic.
		///"PaySimple API Key" values stay as stored (osd1$, or plain text from Open Dental) and are only decrypted into textKey.</summary>
		private List<ProgramProperty> _listProgramProperties;

		///<summary>Used to revert the slected index in the clinic drop down box if the user tries to change clinics
		///and the payment type has not been set.</summary>
		private int _indexClinicRevert;
		private List<Def> _listDefsPaymentType;

		public FormPaySimpleSetup() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormPaySimpleSetup_Load(object sender,EventArgs e) {
			_program=Programs.GetCur(ProgramName.PaySimple);
			if(_program==null) {
				MsgBox.Show(this,"The PaySimple entry is missing from the database.");//should never happen
				return;
			}
			//Rows that older databases lack (environment, hourly ACH check), and masking of the key rows. Before the list is loaded, so Sync keeps them.
			if(PaySimple.EnsureProperties()) {
				ProgramProperties.RefreshCache();
				DataValid.SetInvalid(InvalidType.Programs);
			}
			checkEnabled.Checked=_program.Enabled;
			//The Payment Portal is an eService, which this build doesn't have. The setting is kept for schema compatibility and saved as off.
			checkAllowOnlinePayments.Visible=false;
			comboEnvironment.Items.Clear();
			comboEnvironment.Items.Add(Lan.g(this,"Production"),PaySimple.Environments.Production);
			comboEnvironment.Items.Add(Lan.g(this,"Sandbox"),PaySimple.Environments.Sandbox);
			if(!PrefC.HasClinicsEnabled) {//clinics are not enabled, use ClinicNum 0 to indicate 'Headquarters' or practice level program properties
				checkEnabled.Text=Lan.g(this,"Enabled");
				groupPaySettings.Text=Lan.g(this,"Payment Settings");
				comboClinic.Visible=false;
				labelClinic.Visible=false;
				labelClinicEnable.Visible=false;
				_listUserClinicNums=new List<long>() { 0 };//if clinics are disabled, programproperty.ClinicNum will be set to 0
			}
			else {//Using clinics
				groupPaySettings.Text=Lan.g(this,"Clinic Payment Settings");
				_listUserClinicNums=new List<long>();
				comboClinic.Items.Clear();
				//if PaySimple is enabled and the user is restricted to a clinic, don't allow the user to disable for all clinics
				if(Security.CurUser.ClinicIsRestricted) {
					if(checkEnabled.Checked) {
						checkEnabled.Enabled=false;
					}
				}
				else {
					comboClinic.Items.Add(Lan.g(this,"Headquarters"));
					//this way both lists have the same number of items in it and if 'Headquarters' is selected the programproperty.ClinicNum will be set to 0
					_listUserClinicNums.Add(0);
					comboClinic.SelectedIndex=0;
				}
				List<Clinic> listClinics=Clinics.GetForUserod(Security.CurUser);
				for(int i=0;i<listClinics.Count;i++) {
					comboClinic.Items.Add(listClinics[i].Abbr);
					_listUserClinicNums.Add(listClinics[i].ClinicNum);
					if(Clinics.ClinicNum==listClinics[i].ClinicNum) {
						comboClinic.SelectedIndex=i;
						if(!Security.CurUser.ClinicIsRestricted) {
							comboClinic.SelectedIndex++;//increment the SelectedIndex to account for 'Headquarters' in the list at position 0 if the user is not restricted.
						}
					}
				}
				_indexClinicRevert=comboClinic.SelectedIndex;
			}
			_listProgramProperties=ProgramProperties.GetForProgram(_program.ProgramNum);
			for(int i=0;i<_listProgramProperties.Count;i++) {
				if(_listProgramProperties[i].IsHighSecurity) {
					_listProgramProperties[i].TagOD=_listProgramProperties[i].PropertyValue;
				}
			}
			if(PrefC.HasClinicsEnabled) {
				List<Clinic> listClinics=Clinics.GetForUserod(Security.CurUser);
				for(int i=0;i<listClinics.Count;i++) {
					AddNeededProgramProperties(listClinics[i].ClinicNum);
				}
			}
			//The key rows are high security: only users with ManageHighSecurityProgProperties may change the key.
			if(!ProgramProperties.CanEditProperties(_listProgramProperties.FindAll(x => x.PropertyDesc==PaySimple.PropertyDescs.PaySimpleApiKey))) {
				textKey.ReadOnly=true;
			}
			checkAchStatusCheck.Checked=ProgramProperties.GetPropValFromList(_listProgramProperties,PaySimple.PropertyDescs.PaySimpleAchStatusCheck,0)!="0";
			FillFields();
		}

		///<summary>Adds the clinic's rows to the in-memory list, copied from the ClinicNum 0 rows. Only for the user's clinics, never ClinicNum 0
		///(PaySimple.EnsureProperties creates those). Persisted by ProgramProperties.Sync on Save.</summary>
		private void AddNeededProgramProperties(long clinicNum) {
			AddNeededProgramProperty(clinicNum,PaySimple.PropertyDescs.PaySimpleApiUserName,"");
			AddNeededProgramProperty(clinicNum,PaySimple.PropertyDescs.PaySimpleApiKey,"",isSecret:true);
			AddNeededProgramProperty(clinicNum,PaySimple.PropertyDescs.PaySimplePayTypeCC,"");
			AddNeededProgramProperty(clinicNum,PaySimple.PropertyDescs.PaySimplePayTypeACH,"");
			AddNeededProgramProperty(clinicNum,PaySimple.PropertyDescs.PaySimplePreventSavingNewCC,"0");
			AddNeededProgramProperty(clinicNum,PaySimple.PropertyDescs.PaySimplePrintReceipt,"0");
			AddNeededProgramProperty(clinicNum,PaySimple.PropertyDescs.PaySimpleIsOnlinePaymentsEnabled,"0");
			AddNeededProgramProperty(clinicNum,PaySimple.PropertyDescs.PaySimpleEnvironment,PaySimple.Environments.Production);
		}

		private void AddNeededProgramProperty(long clinicNum,string propertyDesc,string defaultValue,bool isSecret=false) {
			if(_listProgramProperties.Any(x => x.ClinicNum==clinicNum && x.PropertyDesc==propertyDesc)) {
				return;
			}
			ProgramProperty programPropertyHq=_listProgramProperties.FirstOrDefault(x => x.PropertyDesc==propertyDesc && x.ClinicNum==0);
			ProgramProperty programProperty=new ProgramProperty();
			programProperty.ClinicNum=clinicNum;
			programProperty.PropertyDesc=propertyDesc;
			programProperty.ProgramNum=_program.ProgramNum;
			programProperty.PropertyValue=programPropertyHq?.PropertyValue??defaultValue;
			programProperty.IsMasked=isSecret;
			programProperty.IsHighSecurity=isSecret;
			programProperty.TagOD=programProperty.PropertyValue;//So a copied key isn't logged as altered unless the user changes it.
			_listProgramProperties.Add(programProperty);
		}

		private long GetSelectedClinicNum() {
			if(PrefC.HasClinicsEnabled) {
				return _listUserClinicNums[comboClinic.SelectedIndex];
			}
			return 0;
		}

		private void FillFields() {
			long clinicNum=GetSelectedClinicNum();
			textUsername.Text=ProgramProperties.GetPropValFromList(_listProgramProperties,PaySimple.PropertyDescs.PaySimpleApiUserName,clinicNum);
			//An unreadable key shows blank: it has to be re-entered.
			textKey.Text=SecretValue.ReadOrBlank(GetKeyStoredForClinic(clinicNum),LegacyPolicy.AcceptPlaintext);
			string payTypeDefNumCC=ProgramProperties.GetPropValFromList(_listProgramProperties,PaySimple.PropertyDescs.PaySimplePayTypeCC,clinicNum);
			string payTypeDefNumACH=ProgramProperties.GetPropValFromList(_listProgramProperties,PaySimple.PropertyDescs.PaySimplePayTypeACH,clinicNum);
			checkPreventSavingNewCC.Checked=PIn.Bool(ProgramProperties.GetPropValFromList(_listProgramProperties,
				PaySimple.PropertyDescs.PaySimplePreventSavingNewCC,clinicNum));
			checkPrintReceipt.Checked=PIn.Bool(ProgramProperties.GetPropValFromList(_listProgramProperties,
				PaySimple.PropertyDescs.PaySimplePrintReceipt,clinicNum));
			comboEnvironment.SelectedIndex=(GetEnvironmentForClinic(clinicNum)==PaySimple.Environments.Sandbox) ? 1 : 0;//Items: Production, Sandbox
			_listDefsPaymentType=Defs.GetDefsForCategory(DefCat.PaymentTypes,true);
			comboPaymentTypeCC.Items.Clear();
			comboPaymentTypeCC.Items.AddDefs(_listDefsPaymentType);
			comboPaymentTypeCC.SetSelectedDefNum(PIn.Long(payTypeDefNumCC));
			comboPaymentTypeACH.Items.Clear();
			comboPaymentTypeACH.Items.AddDefs(_listDefsPaymentType);
			comboPaymentTypeACH.SetSelectedDefNum(PIn.Long(payTypeDefNumACH));
		}

		private string GetUsernameForClinic(long clinicNum) {
			return ProgramProperties.GetPropValFromList(_listProgramProperties,PaySimple.PropertyDescs.PaySimpleApiUserName,clinicNum);
		}

		///<summary>The key as stored in the list: osd1$, plain text from Open Dental, or blank.</summary>
		private string GetKeyStoredForClinic(long clinicNum) {
			return ProgramProperties.GetPropValFromList(_listProgramProperties,PaySimple.PropertyDescs.PaySimpleApiKey,clinicNum);
		}

		private string GetEnvironmentForClinic(long clinicNum) {
			return PaySimple.NormalizeEnvironment(ProgramProperties.GetPropValFromList(_listProgramProperties,PaySimple.PropertyDescs.PaySimpleEnvironment,
				clinicNum));
		}

		///<summary>The environment selected in the combo box.</summary>
		private string GetSelectedEnvironment() {
			return PaySimple.NormalizeEnvironment(comboEnvironment.GetSelected<string>());
		}

		///<summary>The credentials typed in the form for the selected clinic, unsaved.</summary>
		private PaySimple.PaySimpleCredentials GetCredentialsFromForm() {
			return new PaySimple.PaySimpleCredentials(textUsername.Text,textKey.Text,GetSelectedEnvironment());
		}

		///<summary>The credentials in the list for clinicNum, decrypted. Clinic rows only; Sync has not run yet.</summary>
		private PaySimple.PaySimpleCredentials GetCredentialsFromList(long clinicNum) {
			return new PaySimple.PaySimpleCredentials(GetUsernameForClinic(clinicNum),
				SecretValue.ReadOrBlank(GetKeyStoredForClinic(clinicNum),LegacyPolicy.AcceptPlaintext),GetEnvironmentForClinic(clinicNum));
		}

		///<summary>The value to store for the key typed in textKey: unchanged if the text matches what is stored (so an untouched key isn't rewritten),
		///otherwise osd1$. Returns false if encryption failed; the caller must not save.</summary>
		private bool TryGetKeyToStore(long clinicNum,out string keyStored) {
			string keyStoredCur=GetKeyStoredForClinic(clinicNum);
			SecretReadResult secretReadResult=SecretValue.Read(keyStoredCur,LegacyPolicy.AcceptPlaintext);
			if(secretReadResult.Status==SecretReadStatus.Ok && secretReadResult.PlainText==textKey.Text) {
				keyStored=keyStoredCur;
				return true;
			}
			return SecretValue.Protect(textKey.Text,out keyStored);
		}

		///<summary>Copies the text boxes and combo boxes into the list for clinicNum. Returns false if the key couldn't be encrypted.</summary>
		private bool SetValuesInList(long clinicNum) {
			string keyStored;
			if(!TryGetKeyToStore(clinicNum,out keyStored)) {
				MsgBox.Show(this,"The PaySimple key could not be encrypted, so it was not saved.");
				return false;
			}
			_listProgramProperties.FindAll(x => x.ClinicNum==clinicNum && x.PropertyDesc==PaySimple.PropertyDescs.PaySimpleApiUserName)
				.ForEach(x => x.PropertyValue=textUsername.Text.Trim());//always 1 item; null safe
			_listProgramProperties.FindAll(x => x.ClinicNum==clinicNum && x.PropertyDesc==PaySimple.PropertyDescs.PaySimpleApiKey)
				.ForEach(x => x.PropertyValue=keyStored);//always 1 item; null safe
			_listProgramProperties.FindAll(x => x.ClinicNum==clinicNum
					&& x.PropertyDesc==PaySimple.PropertyDescs.PaySimplePayTypeCC
					&& comboPaymentTypeCC.SelectedIndex>-1)
				.ForEach(x => x.PropertyValue=comboPaymentTypeCC.GetSelected<Def>().DefNum.ToString());//always 1 item selected
			_listProgramProperties.FindAll(x => x.ClinicNum==clinicNum
					&& x.PropertyDesc==PaySimple.PropertyDescs.PaySimplePayTypeACH
					&& comboPaymentTypeACH.SelectedIndex>-1)
				.ForEach(x => x.PropertyValue=comboPaymentTypeACH.GetSelected<Def>().DefNum.ToString());//always 1 item selected
			_listProgramProperties.FindAll(x => x.ClinicNum==clinicNum
				&& x.PropertyDesc==PaySimple.PropertyDescs.PaySimplePreventSavingNewCC)
				.ForEach(x => x.PropertyValue=POut.Bool(checkPreventSavingNewCC.Checked));
			_listProgramProperties.FindAll(x => x.ClinicNum==clinicNum
				&& x.PropertyDesc==PaySimple.PropertyDescs.PaySimplePrintReceipt)
				.ForEach(x => x.PropertyValue=POut.Bool(checkPrintReceipt.Checked));
			_listProgramProperties.FindAll(x => x.ClinicNum==clinicNum
				&& x.PropertyDesc==PaySimple.PropertyDescs.PaySimpleEnvironment)
				.ForEach(x => x.PropertyValue=GetSelectedEnvironment());
			return true;
		}

		private void comboClinic_SelectionChangeCommitted(object sender,EventArgs e) {
			if(comboClinic.SelectedIndex==_indexClinicRevert) {//didn't change the selected clinic
				return;
			}
			//if PaySimple is enabled and the username and key are set for the current clinic,
			//make the user select a payment type before switching clinics
			if(checkEnabled.Checked && !IsClinicCurSetupDone()) {
				comboClinic.SelectedIndex=_indexClinicRevert;
				MsgBox.Show(this,"Please select a username, key, and/or payment type first.");
				return;
			}
			//The text boxes still hold the values of the clinic being left, at _indexClinicRevert.
			if(!SynchWithHQ()) {//if the user just modified the HQ credentials, change any credentials that were the same as HQ to keep them synched
				comboClinic.SelectedIndex=_indexClinicRevert;
				return;
			}
			//set the values in the list for the clinic we are switching from, at _clinicIndexRevert
			if(!SetValuesInList(_listUserClinicNums[_indexClinicRevert])) {
				comboClinic.SelectedIndex=_indexClinicRevert;
				return;
			}
			_indexClinicRevert=comboClinic.SelectedIndex;//now that we've updated the values for the clinic we're switching from, update _indexClinicRevert
			FillFields();
		}

		private bool IsClinicCurSetupDone() {
			//If nothing is set, they are OK to change clinics and save.
			if(string.IsNullOrWhiteSpace(textUsername.Text) && string.IsNullOrWhiteSpace(textKey.Text)){
				return true;
			}
			//If everything is set, they are OK to change clinics and save.
			if(!string.IsNullOrWhiteSpace(textUsername.Text) && !string.IsNullOrWhiteSpace(textKey.Text)
				&& comboPaymentTypeCC.SelectedIndex>=0 && comboPaymentTypeACH.SelectedIndex>=0)
			{
				return true;
			}
			return false;
		}

		///<summary>For each clinic, if the Username and Key are the same as the HQ (ClinicNum=0) Username and Key, update the clinic with the
		///values in the text boxes.  Only modifies other clinics if _indexClinicRevert=0, meaning user just modified the HQ clinic credentials.
		///Keys are compared as stored: osd1$ is deterministic, so equal keys have equal ciphertexts. Returns false if the key couldn't be encrypted.
		///</summary>
		private bool SynchWithHQ() {
			if(!PrefC.HasClinicsEnabled || _listUserClinicNums[_indexClinicRevert]>0) {//using clinics, and modifying the HQ clinic. otherwise return.
				return true;
			}
			string hqUsername=ProgramProperties.GetPropValFromList(_listProgramProperties,PaySimple.PropertyDescs.PaySimpleApiUserName,0);//HQ Username before updating to value in textbox
			string hqKey=GetKeyStoredForClinic(0);//HQ Key before updating to value in textbox
			string hqKeyPlain=SecretValue.ReadOrBlank(hqKey,LegacyPolicy.AcceptPlaintext);
			string hqEnvironment=GetEnvironmentForClinic(0);
			string keyStoredNew;
			if(!TryGetKeyToStore(0,out keyStoredNew)) {
				MsgBox.Show(this,"The PaySimple key could not be encrypted, so it was not saved.");
				return false;
			}
			string hqPayTypeCC=ProgramProperties.GetPropValFromList(_listProgramProperties,PaySimple.PropertyDescs.PaySimplePayTypeCC,0);//HQ PaymentType before updating to combo box selection
			string payTypeCC="";
			if(comboPaymentTypeCC.SelectedIndex>-1) {
				payTypeCC=comboPaymentTypeCC.GetSelected<Def>().DefNum.ToString();
			}
			string hqPayTypeACH=ProgramProperties.GetPropValFromList(_listProgramProperties,PaySimple.PropertyDescs.PaySimplePayTypeACH,0);//HQ PaymentType before updating to combo box selection
			string payTypeACH="";
			if(comboPaymentTypeACH.SelectedIndex>-1) {
				payTypeACH=comboPaymentTypeACH.GetSelected<Def>().DefNum.ToString();
			}
			//for each distinct ClinicNum in the prog property list for PaySimple except HQ
			List<long> listClinicNumsDistinct=_listProgramProperties.Select(x => x.ClinicNum).Where(x => x>0).Distinct().ToList();
			for(int i=0;i<listClinicNumsDistinct.Count;i++) {
				//if this clinic has a different username or key, skip it. A plain-text copy of the HQ key (from Open Dental) also counts as the same.
				if(!_listProgramProperties.Exists(x => x.ClinicNum==listClinicNumsDistinct[i] && x.PropertyDesc==PaySimple.PropertyDescs.PaySimpleApiUserName && x.PropertyValue==hqUsername)
					|| !_listProgramProperties.Exists(x => x.ClinicNum==listClinicNumsDistinct[i] && x.PropertyDesc==PaySimple.PropertyDescs.PaySimpleApiKey
						&& (x.PropertyValue==hqKey || SecretValue.ReadOrBlank(x.PropertyValue,LegacyPolicy.AcceptPlaintext)==hqKeyPlain)))
				{
					continue;
				}
				//this clinic had a matching username and key, so update the username and key to keep it synched with HQ
				_listProgramProperties.FindAll(x => x.ClinicNum==listClinicNumsDistinct[i] && x.PropertyDesc==PaySimple.PropertyDescs.PaySimpleApiUserName)
					.ForEach(x => x.PropertyValue=textUsername.Text.Trim());//always 1 item; null safe
				_listProgramProperties.FindAll(x => x.ClinicNum==listClinicNumsDistinct[i] && x.PropertyDesc==PaySimple.PropertyDescs.PaySimpleApiKey)
					.ForEach(x => x.PropertyValue=keyStoredNew);//always 1 item; null safe
				//The same merchant account is in the same environment.
				_listProgramProperties.FindAll(x => x.ClinicNum==listClinicNumsDistinct[i] && x.PropertyDesc==PaySimple.PropertyDescs.PaySimpleEnvironment
						&& PaySimple.NormalizeEnvironment(x.PropertyValue)==hqEnvironment)
					.ForEach(x => x.PropertyValue=GetSelectedEnvironment());
				if(!string.IsNullOrEmpty(payTypeCC)) {
					//update clinic payment type if it originally matched HQ's payment type and the selected payment type is valid
					_listProgramProperties.FindAll(x => x.ClinicNum==listClinicNumsDistinct[i] && x.PropertyDesc==PaySimple.PropertyDescs.PaySimplePayTypeCC
							&& x.PropertyValue==hqPayTypeCC)
						.ForEach(x => x.PropertyValue=payTypeCC);
				}
				if(!string.IsNullOrEmpty(payTypeACH)) {
					//update clinic payment type if it originally matched HQ's payment type and the selected payment type is valid
					_listProgramProperties.FindAll(x => x.ClinicNum==listClinicNumsDistinct[i] && x.PropertyDesc==PaySimple.PropertyDescs.PaySimplePayTypeACH
							&& x.PropertyValue==hqPayTypeACH)
						.ForEach(x => x.PropertyValue=payTypeACH);
				}
			}
			return true;
		}

		private void butTestConnection_Click(object sender,EventArgs e) {
			PaySimple.PaySimpleCredentials paySimpleCredentials=GetCredentialsFromForm();
			if(!paySimpleCredentials.IsComplete) {
				MsgBox.Show(this,"Please enter a username and key first.");
				return;
			}
			Cursor=Cursors.WaitCursor;
			string message;
			try {
				if(PaySimple.TestCredentials(paySimpleCredentials)) {
					message=Lan.g(this,"PaySimple accepted the credentials.")+" ("+Lan.g(this,paySimpleCredentials.Environment)+")";
				}
				else {
					message=Lan.g(this,"Invalid PaySimple credentials.  Check your Username and Key and try again.");
				}
			}
			catch(Exception ex) {
				message=Lan.g(this,"Could not test the credentials:")+" "+ex.Message;
			}
			finally {
				Cursor=Cursors.Default;
			}
			MsgBox.Show(message);
		}

		private void butWebhooks_Click(object sender,EventArgs e) {
			PaySimple.PaySimpleCredentials paySimpleCredentials=GetCredentialsFromForm();
			if(!paySimpleCredentials.IsComplete) {
				MsgBox.Show(this,"Please enter a username and key first.");
				return;
			}
			List<PaySimple.ApiWebhookResponse> listApiWebhookResponses;
			Cursor=Cursors.WaitCursor;
			try {
				listApiWebhookResponses=PaySimple.GetWebhooks(paySimpleCredentials);
			}
			catch(Exception ex) {
				Cursor=Cursors.Default;
				MsgBox.Show(Lan.g(this,"Could not read the webhook subscriptions:")+" "+ex.Message);
				return;
			}
			Cursor=Cursors.Default;
			if(listApiWebhookResponses.Count==0) {
				MsgBox.Show(this,"This PaySimple account has no webhook subscriptions.");
				return;
			}
			List<GridColumn> listGridColumns=new List<GridColumn>();
			listGridColumns.Add(new GridColumn(Lan.g(this,"URL"),300));
			listGridColumns.Add(new GridColumn(Lan.g(this,"Events"),200));
			listGridColumns.Add(new GridColumn(Lan.g(this,"Active"),50,HorizontalAlignment.Center));
			List<GridRow> listGridRows=new List<GridRow>();
			for(int i=0;i<listApiWebhookResponses.Count;i++) {
				GridRow gridRow=new GridRow();
				gridRow.Cells.Add(listApiWebhookResponses[i].WebhookUrl);
				gridRow.Cells.Add(string.Join(", ",listApiWebhookResponses[i].WebhookTypes??new List<string>()));
				gridRow.Cells.Add(listApiWebhookResponses[i].IsActive ? "X" : "");
				gridRow.Tag=listApiWebhookResponses[i];
				listGridRows.Add(gridRow);
			}
			using FormGridSelection formGridSelection=new FormGridSelection(listGridColumns,listGridRows,Lan.g(this,"PaySimple Webhook Subscriptions"),
				Lan.g(this,"Select the subscriptions to remove"),GridSelectionMode.MultiExtended);
			if(formGridSelection.ShowDialog()!=DialogResult.OK || formGridSelection.ListSelectedTags.Count==0) {
				return;
			}
			List<PaySimple.ApiWebhookResponse> listToRemove=formGridSelection.ListSelectedTags.OfType<PaySimple.ApiWebhookResponse>().ToList();
			string confirm=Lan.g(this,"Remove these webhook subscriptions from the PaySimple account? PaySimple will stop sending payment events to:")
				+"\r\n"+string.Join("\r\n",listToRemove.Select(x => x.WebhookUrl));
			if(MessageBox.Show(confirm,"",MessageBoxButtons.OKCancel)!=DialogResult.OK) {
				return;
			}
			List<string> listErrors=new List<string>();
			Cursor=Cursors.WaitCursor;
			for(int i=0;i<listToRemove.Count;i++) {
				try {
					PaySimple.DeleteWebhook(paySimpleCredentials,listToRemove[i].WebhookId);
				}
				catch(Exception ex) {
					listErrors.Add(listToRemove[i].WebhookUrl+": "+ex.Message);
				}
			}
			Cursor=Cursors.Default;
			if(listErrors.Count>0) {
				using MsgBoxCopyPaste msgBoxCopyPaste=new MsgBoxCopyPaste(Lan.g(this,"Some subscriptions were not removed:")+"\r\n"+string.Join("\r\n",listErrors));
				msgBoxCopyPaste.ShowDialog();
				return;
			}
			MsgBox.Show(this,"The selected webhook subscriptions were removed.");
		}

		private void butCheckAchStatuses_Click(object sender,EventArgs e) {
			if(!_program.Enabled) {
				MsgBox.Show(this,"Enable PaySimple and save first.");
				return;
			}
			if(!MsgBox.Show(this,MsgBoxButtons.OKCancel,"Ask PaySimple now for the status of every ACH payment that hasn't settled, and of recently settled ones? "
				+"This uses the saved settings."))
			{
				return;
			}
			PaySimpleAchSyncResult paySimpleAchSyncResult=null;
			ProgressWin progressWin=new ProgressWin();
			progressWin.ActionMain=() => {
				paySimpleAchSyncResult=PaySimpleAchStatusSync.Run(isManual:true);
			};
			progressWin.StartingMessage=Lan.g(this,"Checking ACH payment statuses...");
			try {
				progressWin.ShowDialog();
			}
			catch(Exception ex) {
				MsgBox.Show(Lan.g(this,"The ACH status check failed:")+" "+ex.Message);
				return;
			}
			if(paySimpleAchSyncResult==null) {
				return;
			}
			using MsgBoxCopyPaste msgBoxCopyPaste=new MsgBoxCopyPaste(paySimpleAchSyncResult.GetSummary());
			msgBoxCopyPaste.ShowDialog();
		}

		///<summary>Writes a security log entry for each high-security row (the keys) whose stored value changed.</summary>
		private void LogHighSecurityChanges() {
			for(int i=0;i<_listProgramProperties.Count;i++) {
				ProgramProperty programProperty=_listProgramProperties[i];
				if(!programProperty.IsHighSecurity || programProperty.PropertyValue==(programProperty.TagOD?.ToString()??"")) {
					continue;
				}
				StringBuilder stringBuilderLogText=new StringBuilder();
				stringBuilderLogText.Append(_program.ProgDesc+"'s "+programProperty.PropertyDesc);
				if(PrefC.HasClinicsEnabled) {
					if(programProperty.ClinicNum==0) {
						stringBuilderLogText.Append(" for Headquarters");
					}
					else {
						stringBuilderLogText.Append(" for clinic "+programProperty.ClinicNum);
					}
				}
				stringBuilderLogText.Append(" was altered.");
				SecurityLogs.MakeLogEntry(EnumPermType.ManageHighSecurityProgProperties,0,stringBuilderLogText.ToString(),_program.ProgramNum,DateTime.Now);
			}
		}

		private void butSave_Click(object sender,System.EventArgs e) {
			#region Validation
			//if clinics are not enabled and the PaySimple program link is enabled, make sure there is a username and key set
			//if clinics are enabled, the program link can be enabled with blank username and/or key fields for some clinics
			//clinics with blank username and/or key will essentially not have PaySimple enabled
			if(checkEnabled.Checked && !IsClinicCurSetupDone()) {//Also works for offices not using clinics
				MsgBox.Show(this,"Please enter a username, key, and/or payment type first.");
				return;
			}
			if(!SynchWithHQ()) {//if the user changes the HQ credentials, any clinic that had the same credentials will be kept in synch with HQ
				return;
			}
			long clinicNum=GetSelectedClinicNum();
			//set the values in the list for this clinic
			if(!SetValuesInList(clinicNum)) {
				return;
			}
			string payTypeCC;
			string payTypeACH;
			//make sure any other clinics with PaySimple enabled also have a payment type selected
			for(int i=0;i<_listUserClinicNums.Count;i++) {
				if(!checkEnabled.Checked) {//if program link is not enabled, do not bother checking the payment type selected
					break;
				}
				payTypeCC=ProgramProperties.GetPropValFromList(_listProgramProperties,PaySimple.PropertyDescs.PaySimplePayTypeCC,_listUserClinicNums[i]);
				payTypeACH=ProgramProperties.GetPropValFromList(_listProgramProperties,PaySimple.PropertyDescs.PaySimplePayTypeACH,_listUserClinicNums[i]);
				//if the program is enabled and the username and key fields are not blank,
				//PaySimple is enabled for this clinic so make sure the payment type is also set
				if(GetUsernameForClinic(_listUserClinicNums[i])!="" //if username set
					&& GetKeyStoredForClinic(_listUserClinicNums[i])!=""//and key set
					//and either paytype is not a valid DefNum
					&& (!_listDefsPaymentType.Any(x => x.DefNum.ToString()==payTypeCC)
						|| !_listDefsPaymentType.Any(x => x.DefNum.ToString()==payTypeACH)))
				{
					MsgBox.Show(this,"Please select payment types for all clinics with PaySimple username and key set.");
					return;
				}
			}
			//Keys Open Dental stored as plain text are encrypted now; every key row is masked and high security.
			for(int i=0;i<_listProgramProperties.Count;i++) {
				ProgramProperty programProperty=_listProgramProperties[i];
				if(programProperty.PropertyDesc!=PaySimple.PropertyDescs.PaySimpleApiKey) {
					continue;
				}
				programProperty.IsMasked=true;
				programProperty.IsHighSecurity=true;
				SecretReadResult secretReadResult=SecretValue.Read(programProperty.PropertyValue,LegacyPolicy.AcceptPlaintext);
				if(!secretReadResult.NeedsReEncrypt) {
					continue;
				}
				string keyStored;
				if(!SecretValue.Protect(secretReadResult.PlainText,out keyStored)) {
					MsgBox.Show(this,"The PaySimple key could not be encrypted, so it was not saved.");
					return;
				}
				programProperty.PropertyValue=keyStored;
			}
			//The Payment Portal is an eService, which this build doesn't have: always off.
			_listProgramProperties.FindAll(x => x.PropertyDesc==PaySimple.PropertyDescs.PaySimpleIsOnlinePaymentsEnabled)
				.ForEach(x => x.PropertyValue=POut.Bool(false));
			//Practice-wide: the hourly ACH status check.
			_listProgramProperties.FindAll(x => x.ClinicNum==0 && x.PropertyDesc==PaySimple.PropertyDescs.PaySimpleAchStatusCheck)
				.ForEach(x => x.PropertyValue=POut.Bool(checkAchStatusCheck.Checked));
			#endregion Validation
			#region Save
			if(_program.Enabled!=checkEnabled.Checked) {//only update the program if the IsEnabled flag has changed
				_program.Enabled=checkEnabled.Checked;
				Programs.Update(_program);
			}
			ProgramProperties.Sync(_listProgramProperties,_program.ProgramNum);
			LogHighSecurityChanges();
			#endregion Save
			DataValid.SetInvalid(InvalidType.Programs);
			//After programproperties are saved. User might want to save some information and then come back to finish up, so don't hit them with
			//the credential check.
			if(!checkEnabled.Checked) {
				DialogResult=DialogResult.OK;
				return;
			}
			//Test each merchant account that has credentials once, and point out any webhook subscriptions (which may still send this office's payment
			//events to Open Dental). Nothing is created or removed here.
			string errorMessage=CheckSavedAccounts();
			if(!string.IsNullOrEmpty(errorMessage)) {
				using MsgBoxCopyPaste msgBoxCopyPaste=new MsgBoxCopyPaste(errorMessage);
				msgBoxCopyPaste.ShowDialog();
			}
			DialogResult=DialogResult.OK;
		}

		///<summary>Tests the credentials of every clinic the user can see, once per merchant account. Returns the problems and notes, or "".</summary>
		private string CheckSavedAccounts() {
			StringBuilder stringBuilder=new StringBuilder();
			HashSet<string> hashSetTested=new HashSet<string>();
			int countWebhooks=0;
			Cursor=Cursors.WaitCursor;
			for(int i=0;i<_listUserClinicNums.Count;i++) {
				PaySimple.PaySimpleCredentials paySimpleCredentials=GetCredentialsFromList(_listUserClinicNums[i]);
				if(!paySimpleCredentials.IsComplete || !hashSetTested.Add(paySimpleCredentials.GetGroupingKey())) {
					continue;
				}
				string clinicAbbr=_listUserClinicNums[i]==0 ? Lan.g(this,"Headquarters") : Clinics.GetAbbr(_listUserClinicNums[i]);
				string prefix=PrefC.HasClinicsEnabled ? "-"+Lan.g(this,"Clinic")+" '"+clinicAbbr+"': " : "";
				try {
					if(!PaySimple.TestCredentials(paySimpleCredentials)) {
						stringBuilder.AppendLine(prefix+Lan.g(this,"Invalid PaySimple credentials.  Check your Username and Key and try again."));
						continue;
					}
					countWebhooks+=PaySimple.GetWebhooks(paySimpleCredentials).Count;
				}
				catch(Exception ex) {
					stringBuilder.AppendLine(prefix+ex.Message);
				}
			}
			Cursor=Cursors.Default;
			if(countWebhooks>0) {
				stringBuilder.AppendLine(Lan.g(this,"The PaySimple account has webhook subscriptions. Review them with Webhook subscriptions... and remove any "
					+"whose address isn't a system this office controls."));
			}
			return stringBuilder.ToString().TrimEnd();
		}

	}
}
