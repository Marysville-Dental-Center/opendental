using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using CodeBase;
using OpenDental.UI;
using OpenDentBusiness;
using OpenDentBusiness.VendorDirect;
using static OpenDentBusiness.PayConnect2;

namespace OpenDental{
	/// <summary>
	/// Summary description for FormBasicTemplate.
	/// </summary>
	public partial class FormPayConnectSetup : FormODBase {
		private Program _program;
		///<summary>Local cache of all of the clinic nums the current user has permission to access at the time the form loads.  Filled at the same time
		///as comboClinic and is used to set programproperty.ClinicNum when saving.</summary>
		private List<long> _listUserClinicNums;
		///<summary>List of PayConnect program properties for all clinics.
		///Includes properties with ClinicNum=0, the headquarters props or props not assigned to a clinic.</summary>
		private List<ProgramProperty> _listProgramProperties;
		private List<ProgramProperty> _listProgramPropertiesXWebWebPay=new List<ProgramProperty>();
		///<summary>Used to revert the slected index in the clinic drop down box if the user tries to change clinics
		///and the payment type has not been set.</summary>
		private int _indexClinicRevert;
		private List<Def> _listDefsPaymentType;
		private List<PayTerminal> _listPayTerminals;

		///<summary></summary>
		public FormPayConnectSetup()
		{
			//
			// Required for Windows Form Designer support
			//
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormPayConnectSetup_Load(object sender,EventArgs e) {
			_program=Programs.GetCur(ProgramName.PayConnect);
			if(_program==null) {
				MsgBox.Show(this,"The PayConnect entry is missing from the database.");//should never happen
				return;
			}
			checkEnabled.Checked=_program.Enabled;
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
				//if PayConnect is enabled and the user is restricted to a clinic, don't allow the user to disable for all clinics
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
			//Version 2.0 rows that may not exist yet (API Key, Use Sandbox, a clinic's API Secret) are added in memory and inserted on Save.
			//Opening this window writes nothing. This also marks every key and Secret row masked and high security.
			PayConnect2.AddMissingProperties(_listProgramProperties,_program.ProgramNum,_listUserClinicNums);
			for(int i=0;i<_listProgramProperties.Count;i++) {
				if(_listProgramProperties[i].IsHighSecurity){
					_listProgramProperties[i].TagOD=_listProgramProperties[i].PropertyValue;
				}
			}
			if(!ProgramProperties.CanEditProperties(_listProgramProperties)) {
				textAPISecret.ReadOnly=true;
				textAPIKey.ReadOnly=true;
				checkUseSandbox.Enabled=false;
			}
			if(PrefC.HasClinicsEnabled && Security.CurUser.ClinicIsRestricted) {
				//The API key and the sandbox switch affect every clinic.
				textAPIKey.ReadOnly=true;
				checkUseSandbox.Enabled=false;
			}
			
			FillFields();
		}

		private void FillFields() {
			long clinicNum=0;
			if(PrefC.HasClinicsEnabled) {
				clinicNum=_listUserClinicNums[comboClinic.SelectedIndex];
			}
			//PayConnect 1.0 (user name, password and its patient-portal token) was removed. The window shows version 2.0 only, and Save writes version 2.
			string payTypeDefNum=ProgramProperties.GetPropValFromList(_listProgramProperties,"PaymentType",clinicNum);
			string processingMethod=ProgramProperties.GetPropValFromList(_listProgramProperties,PayConnect.ProgramProperties.DefaultProcessingMethod,clinicNum);
			checkTerminal.Checked=PIn.Bool(ProgramProperties.GetPropValFromList(_listProgramProperties,"TerminalProcessingEnabled",clinicNum));
			//Secrets are stored as osd1$; a Secret Open Dental left in plain text is shown as-is and encrypted on Save. An unreadable value shows blank.
			textAPISecret.Text=SecretValue.ReadOrBlank(ProgramProperties.GetPropValFromList(_listProgramProperties,PayConnect.ProgramProperties.ApiSecret,clinicNum),
				LegacyPolicy.AcceptPlaintext);
			textAPIKey.Text=SecretValue.ReadOrBlank(ProgramProperties.GetPropValFromList(_listProgramProperties,PayConnect.ProgramProperties.ApiKey,0),
				LegacyPolicy.AcceptPlaintext);
			checkUseSandbox.Checked=PIn.Bool(ProgramProperties.GetPropValFromList(_listProgramProperties,PayConnect.ProgramProperties.UseSandbox,0));
			FillGridTerminals();
			checkPreventSavingNewCC.Checked=PIn.Bool(ProgramProperties.GetPropValFromList(_listProgramProperties,
				PayConnect.ProgramProperties.PayConnectPreventSavingNewCC,clinicNum));
			checkPatientPortalPayEnabled.Checked=PIn.Bool(ProgramProperties.GetPropValFromList(_listProgramProperties,PayConnect.ProgramProperties.PatientPortalPaymentsEnabled,clinicNum));
			comboPaymentType.Items.Clear();
			_listDefsPaymentType=Defs.GetDefsForCategory(DefCat.PaymentTypes,true);
			for(int i=0;i<_listDefsPaymentType.Count;i++) {
				comboPaymentType.Items.Add(_listDefsPaymentType[i].ItemName);
				if(_listDefsPaymentType[i].DefNum.ToString()==payTypeDefNum) {
					comboPaymentType.SelectedIndex=i;
				}
			}
			comboDefaultProcessing.Items.Clear();
			comboDefaultProcessing.Items.Add(Lan.g(this,PayConnectProcessingMethod.WebService.GetDescription()));
			comboDefaultProcessing.Items.Add(Lan.g(this,PayConnectProcessingMethod.Terminal.GetDescription()));
			if(processingMethod=="0" || processingMethod=="1") {
				comboDefaultProcessing.SelectedIndex=PIn.Int(ProgramProperties.GetPropValFromList(_listProgramProperties,"DefaultProcessingMethod",clinicNum));
			}
			int accountType=PIn.Int(ProgramProperties.GetPropValFromList(_listProgramProperties,PayConnect.ProgramProperties.IntegrationType,clinicNum));
			if(accountType==1) {
				checkSurcharge.Checked=true;
			}
			else {
				checkSurcharge.Checked=false;
			}
		}

		private void FillGridTerminals() {
			long clinicNum=0;
			if(PrefC.HasClinicsEnabled) {
				clinicNum=_listUserClinicNums[comboClinic.SelectedIndex];
			}
			_listPayTerminals=PayTerminals.Refresh(clinicNum);
			GridColumn col;
			gridODTerminals.BeginUpdate();
			gridODTerminals.Columns.Clear();
			gridODTerminals.ListGridRows.Clear();
			col=new GridColumn(Lan.g(this,"Name"),200);
			gridODTerminals.Columns.Add(col);
			col=new GridColumn(Lan.g(this,"Terminal ID"),200);
			gridODTerminals.Columns.Add(col);
			GridRow row;
			for(int i=0;i<_listPayTerminals.Count;i++) {
				row=new GridRow(_listPayTerminals[i].Name,_listPayTerminals[i].TerminalID);
				row.Tag=_listPayTerminals[i];
				gridODTerminals.ListGridRows.Add(row);
			}
			gridODTerminals.EndUpdate();
		}

		private void comboClinic_SelectionChangeCommitted(object sender,EventArgs e) {
			if(comboClinic.SelectedIndex==_indexClinicRevert) {//didn't change the selected clinic
				return;
			}
			//if PayConnect is enabled and the Secret is set for the current clinic, make the user select a payment type before switching clinics
			if(checkEnabled.Checked && textAPISecret.Text.Trim()!="" && comboPaymentType.SelectedIndex==-1) {
				comboClinic.SelectedIndex=_indexClinicRevert;
				MsgBox.Show(this,"Please select a payment type first.");
				return;
			}
			if(!UpdateListProgramPropertiesForClinic(_listUserClinicNums[_indexClinicRevert])) {
				comboClinic.SelectedIndex=_indexClinicRevert;
				return;
			}
			_indexClinicRevert=comboClinic.SelectedIndex;//now that we've updated the values for the clinic we're switching from, update _indexClinicRevert
			FillFields();
		}

		///<summary>Copies the window's values into the in-memory rows for clinicNum, and the version 2.0 API key and sandbox switch into the ClinicNum 0
		///rows. Returns false, having changed nothing, if a secret could not be encrypted: secrets are never stored as plain text.</summary>
		private bool UpdateListProgramPropertiesForClinic(long clinicNum) {
			string secretStored=null;
			if(!textAPISecret.ReadOnly && !SecretValue.Protect(textAPISecret.Text.Trim(),out secretStored)) {
				MsgBox.Show(this,"The Secret could not be encrypted, so it was not saved.");
				return false;
			}
			string apiKeyStored=null;
			if(!textAPIKey.ReadOnly && !SecretValue.Protect(textAPIKey.Text.Trim(),out apiKeyStored)) {
				MsgBox.Show(this,"The API key could not be encrypted, so it was not saved.");
				return false;
			}
			string payTypeSelected="";
			if(comboPaymentType.SelectedIndex>-1) {
				payTypeSelected=_listDefsPaymentType[comboPaymentType.SelectedIndex].DefNum.ToString();
			}
			string processingMethodSelected=comboDefaultProcessing.SelectedIndex.ToString();
			//set the values in the list for this clinic. The PayConnect 1.0 rows (Username, Password, the force-duplicates default and the patient-portal
			//token) are left as they are: nothing reads them any more.
			_listProgramProperties.FindAll(x => x.ClinicNum==clinicNum && x.PropertyDesc=="PaymentType")
				.ForEach(x => x.PropertyValue=payTypeSelected);//always 1 item; null safe
			_listProgramProperties.FindAll(x => x.ClinicNum==clinicNum && x.PropertyDesc==PayConnect.ProgramProperties.DefaultProcessingMethod)
				.ForEach(x => x.PropertyValue=processingMethodSelected);
			_listProgramProperties.FindAll(x => x.ClinicNum==clinicNum && x.PropertyDesc=="TerminalProcessingEnabled")
				.ForEach(x => x.PropertyValue=POut.Bool(checkTerminal.Checked));//always 1 item; null safe
			_listProgramProperties.FindAll(x => x.ClinicNum==clinicNum && x.PropertyDesc==PayConnect.ProgramProperties.PayConnectPreventSavingNewCC)
				.ForEach(x => x.PropertyValue=POut.Bool(checkPreventSavingNewCC.Checked));
			_listProgramProperties.FindAll(x => x.ClinicNum==clinicNum && x.PropertyDesc==PayConnect.ProgramProperties.PatientPortalPaymentsEnabled)
				.ForEach(x => x.PropertyValue=POut.Bool(checkPatientPortalPayEnabled.Checked));
			if(secretStored!=null) {//Null when the user can't edit high-security properties: leave the stored value alone.
				_listProgramProperties.FindAll(x => x.ClinicNum==clinicNum && x.PropertyDesc==PayConnect.ProgramProperties.ApiSecret)
					.ForEach(x => x.PropertyValue=secretStored);//always 1 item; null safe
			}
			if(apiKeyStored!=null) {
				_listProgramProperties.FindAll(x => x.ClinicNum==0 && x.PropertyDesc==PayConnect.ProgramProperties.ApiKey)
					.ForEach(x => x.PropertyValue=apiKeyStored);//practice-wide, always 1 item
			}
			if(checkUseSandbox.Enabled) {
				_listProgramProperties.FindAll(x => x.ClinicNum==0 && x.PropertyDesc==PayConnect.ProgramProperties.UseSandbox)
					.ForEach(x => x.PropertyValue=POut.Bool(checkUseSandbox.Checked));//practice-wide, always 1 item
			}
			//PayConnect 1.0 was removed, so a clinic saved here is always version 2.
			_listProgramProperties.FindAll(x => x.ClinicNum==clinicNum && x.PropertyDesc==PayConnect.ProgramProperties.ProgramVersion)
				.ForEach(x => x.PropertyValue=PayConnect.Version2);//always 1 item; null safe
			int accountType=0;
			if(checkSurcharge.Checked) {
				accountType=1;
			}
			_listProgramProperties.FindAll(x => x.ClinicNum==clinicNum && x.PropertyDesc==PayConnect.ProgramProperties.IntegrationType)
				.ForEach(x => x.PropertyValue=POut.Int(accountType));//always 1 item; null safe
			return true;
		}

		private void checkPatientPortalPayEnabled_Click(object sender,EventArgs e) {
			if(!checkPatientPortalPayEnabled.Checked) {
				return;
			}
			long clinicNum=0;
			if(PrefC.HasClinicsEnabled) {
				clinicNum=_listUserClinicNums[comboClinic.SelectedIndex];
			}
			ProgramProperty programPropertyWebPayEnabled=ProgramProperties.GetOnlinePaymentsEnabledForClinic(clinicNum,ProgramName.PayConnect);
			string msg=Lan.g(this,"Online payments are already enabled for another processor and must be disabled in order to use PayConnect online payments. "
				+"Would you like to disable the other processor for online payments?");
			if(programPropertyWebPayEnabled!=null) {
				if(MessageBox.Show(msg,"",MessageBoxButtons.OKCancel)==DialogResult.Cancel) {
					checkPatientPortalPayEnabled.Checked=false;
					return;
				}
				//User wants to set as new processor for online payments, add to the list for the current session to remove currently enabled program.
				_listProgramPropertiesXWebWebPay.Add(programPropertyWebPayEnabled);
			}
		}

		private void gridODTerminals_CellDoubleClick(object sender,ODGridClickEventArgs e) {
			PayTerminal payTerminal=(PayTerminal)gridODTerminals.ListGridRows[e.Row].Tag;
			using FormPayTerminalEdit formPayTerminalEdit=new FormPayTerminalEdit(payTerminal);
			formPayTerminalEdit.ShowDialog();
			FillGridTerminals();
		}
		
		private void butAddTerminal_Click(object sender,EventArgs e) {
			using FormPayTerminalEdit formPayTerminalEdit=new FormPayTerminalEdit();
			formPayTerminalEdit.ShowDialog();
			FillGridTerminals();
		}

		///<summary>Test connection: GET /merchants with the values typed in this window, before they are saved.</summary>
		private void butMerchantInfo_Click(object sender,EventArgs e) {
			string apiKey=textAPIKey.Text;
			string secret=textAPISecret.Text;
			bool useSandbox=checkUseSandbox.Checked;
			PayConnect2Response payConnect2Response=null;
			ProgressWin progressWin=new ProgressWin();
			progressWin.ActionMain=() => { payConnect2Response=PayConnect2.TestConnection(apiKey,secret,useSandbox); };
			progressWin.StartingMessage=Lan.g(this,"Testing the connection to PayConnect");
			try {
				progressWin.ShowDialog();
			}
			catch(Exception ex) {
				FriendlyException.Show(Lan.g(this,"Test connection failed."),ex);
				return;
			}
			if(progressWin.IsCancelled || payConnect2Response==null) {
				return;
			}
			GetMerchantInfoResponse merchantInfo=payConnect2Response.GetMerchantInfoResponse;
			if(payConnect2Response.ResponseType!=ResponseType.MerchantInfo || merchantInfo==null) {
				string message=payConnect2Response.ErrorResponse?.Message;
				if(string.IsNullOrEmpty(message)) {
					message=Lan.g(this,"PayConnect returned an unexpected answer.");
				}
				MsgBox.Show(Lan.g(this,"Test connection failed.")+"\r\n"+message);
				return;
			}
			string data=Lan.g(this,"Connected.")+(useSandbox ? " "+Lan.g(this,"(sandbox)") : "")+"\n\n"+
									Lan.g(this,"Merchant ID")+$": {merchantInfo.MerchantId}\n"+
									Lan.g(this,"Name")+$": {merchantInfo.Name}\n"+
									Lan.g(this,"Status")+$": {merchantInfo.Status}\n"+
									Lan.g(this,"Address 1")+$": {merchantInfo.Address1}\n"+
									Lan.g(this,"Address 2")+$": {merchantInfo.Address2}\n"+
									Lan.g(this,"City")+$": {merchantInfo.City}\n"+
									Lan.g(this,"State")+$": {merchantInfo.State}\n"+
									Lan.g(this,"Zip Code")+$": {merchantInfo.ZipCode}\n"+
									Lan.g(this,"Phone Number")+$": {TelephoneNumbers.ReFormat(merchantInfo.PhoneNumber)}\n"+
									Lan.g(this,"Email")+$": {merchantInfo.Email}\n"+
									Lan.g(this,"DXC Group ID")+$": {merchantInfo.DxcGroupId}\n"+
									Lan.g(this,"Reporting Emails")+$": {string.Join(", ",merchantInfo.ReportingEmails??new string[0])}\n"+
									Lan.g(this,"Refund Without Reference Enabled")+$": {merchantInfo.RefundWithoutReferenceEnabled}\n";
			MsgBox.Show(data);
		}

		///<summary>Adds the merchant's terminals (GET /terminals) to the selected clinic, skipping any whose ID is already there.</summary>
		private void butImportTerminals_Click(object sender,EventArgs e) {
			long clinicNum=0;
			if(PrefC.HasClinicsEnabled) {
				clinicNum=_listUserClinicNums[comboClinic.SelectedIndex];
			}
			ApiCredentials apiCredentials;
			try {
				apiCredentials=PayConnect2.MakeCredentials(textAPIKey.Text,textAPISecret.Text,checkUseSandbox.Checked);
			}
			catch(PayConnectNotConfiguredException ex) {
				MsgBox.Show(ex.Message);
				return;
			}
			PayConnect2Response payConnect2Response=null;
			ProgressWin progressWin=new ProgressWin();
			progressWin.ActionMain=() => { payConnect2Response=PayConnect2.GetTerminals(apiCredentials); };
			progressWin.StartingMessage=Lan.g(this,"Getting terminals from PayConnect");
			try {
				progressWin.ShowDialog();
			}
			catch(Exception ex) {
				FriendlyException.Show(Lan.g(this,"Could not get the terminals."),ex);
				return;
			}
			if(progressWin.IsCancelled || payConnect2Response==null) {
				return;
			}
			if(payConnect2Response.ResponseType!=ResponseType.Terminals || payConnect2Response.ListTerminals==null) {
				MsgBox.Show(Lan.g(this,"Could not get the terminals.")+"\r\n"+(payConnect2Response.ErrorResponse?.Message??""));
				return;
			}
			List<PayTerminal> listPayTerminals=PayTerminals.Refresh(clinicNum);
			int countAdded=0;
			for(int i=0;i<payConnect2Response.ListTerminals.Count;i++) {
				PayConnectTerminalInfo payConnectTerminalInfo=payConnect2Response.ListTerminals[i];
				//The serial is what a terminal sale sends as "terminal"; fall back to the vendor's terminal ID.
				string terminalId=payConnectTerminalInfo.Serial;
				if(string.IsNullOrWhiteSpace(terminalId)) {
					terminalId=payConnectTerminalInfo.TerminalId;
				}
				if(string.IsNullOrWhiteSpace(terminalId) || listPayTerminals.Any(x => x.TerminalID==terminalId)) {
					continue;
				}
				PayTerminal payTerminal=new PayTerminal();
				payTerminal.Name=payConnectTerminalInfo.Name;
				payTerminal.ClinicNum=clinicNum;
				payTerminal.TerminalID=terminalId;
				PayTerminals.Insert(payTerminal);
				listPayTerminals.Add(payTerminal);
				countAdded++;
			}
			FillGridTerminals();
			MsgBox.Show(Lan.g(this,"Terminals added")+": "+countAdded);
		}

		private void butSave_Click(object sender, System.EventArgs e) {
			#region Validation
			//With clinics, the program link can be enabled with a blank Secret for some clinics; those clinics essentially don't have PayConnect.
			if(checkEnabled.Checked //if PayConnect is enabled
					&& comboDefaultProcessing.SelectedIndex < 0)
			{
				MsgBox.Show(this,"Please select a default processing method type first.");
				return;
			}
			if(checkEnabled.Checked //if PayConnect is enabled
				&& comboPaymentType.SelectedIndex<0 //and the payment type is not set
				&& (!PrefC.HasClinicsEnabled  //and either clinics are not enabled (meaning this is the only set of values)
				|| textAPISecret.Text.Trim()!="")) //or clinics are enabled and this clinic has a Secret
			{
				MsgBox.Show(this,"Please select a payment type first.");
				return;
			}
			if(checkEnabled.Checked) {
				if(textAPIKey.Text.Trim()=="") {
					MsgBox.Show(this,"Please enter the API key first.");
					return;
				}
				if(textAPISecret.Text.Trim()=="") {
					MsgBox.Show(this,"Please enter a Secret first.");
					return;
				}
			}
			long clinicNum=0;
			if(PrefC.HasClinicsEnabled) {
				clinicNum=_listUserClinicNums[comboClinic.SelectedIndex];
			}
			if(!UpdateListProgramPropertiesForClinic(clinicNum)) {
				return;
			}
			//PayConnect 1.0 was removed. Every clinic this user can see moves to version 2; a clinic still on version 1 could not take payments anyway.
			_listProgramProperties.FindAll(x => _listUserClinicNums.Contains(x.ClinicNum) && x.PropertyDesc==PayConnect.ProgramProperties.ProgramVersion)
				.ForEach(x => x.PropertyValue=PayConnect.Version2);
			string payTypeCur;
			//make sure any other clinics with PayConnect enabled also have a payment type selected
			for(int i=0;i<_listUserClinicNums.Count;i++) {
				if(!checkEnabled.Checked) {//if program link is not enabled, do not bother checking the payment type selected
					break;
				}
				payTypeCur=ProgramProperties.GetPropValFromList(_listProgramProperties,"PaymentType",_listUserClinicNums[i]);
				//if the program is enabled and the clinic has a Secret (or terminal processing), PayConnect is on for this clinic, so make sure the
				//payment type is also set
				string secret=SecretValue.ReadOrBlank(ProgramProperties.GetPropValFromList(_listProgramProperties,PayConnect.ProgramProperties.ApiSecret,
					_listUserClinicNums[i]),LegacyPolicy.AcceptPlaintext);
				if((secret!="" //a Secret set
					|| ProgramProperties.GetPropValFromList(_listProgramProperties,"TerminalProcessingEnabled",_listUserClinicNums[i])=="1")//or terminal enabled
					&& !_listDefsPaymentType.Any(x => x.DefNum.ToString()==payTypeCur)) //and paytype is not a valid DefNum
				{
					MsgBox.Show(this,"Please select the payment type for all clinics with PayConnect credentials set.");
					return;
				}
			}
			#endregion Validation
			#region Save
			if(_program.Enabled!=checkEnabled.Checked) {//only update the program if the IsEnabled flag has changed
				_program.Enabled=checkEnabled.Checked;
				Programs.Update(_program);
			}
			ProgramProperties.Sync(_listProgramProperties,_program.ProgramNum);
			//Find all clinics that have PayConnect online payments enabled
			string strPayConnectOnlinePaymentsEnabled=PayConnect.ProgramProperties.PatientPortalPaymentsEnabled;
			List<ProgramProperty> listProgramPropertiesPayConnectOnlinePayments=_listProgramProperties.FindAll(x => x.PropertyDesc==strPayConnectOnlinePaymentsEnabled &&
				PIn.Bool(x.PropertyValue));
			for(int i=0;i < listProgramPropertiesPayConnectOnlinePayments.Count;i++) {
				//Find all online payment enabled program properties that we saved in this session. Only clinics that have changes will have an 
				//IsOnlinePaymentsEnabled property in memory. This is needed to ensure that we don't disable other processors if someone
				//checks to use PayConnect online payments and then decides to keep it disabled during the same session.
				ProgramProperty programPropertyWebOnlinePayments=_listProgramPropertiesXWebWebPay.FirstOrDefault(y => y.ClinicNum==listProgramPropertiesPayConnectOnlinePayments[i].ClinicNum);
				if(programPropertyWebOnlinePayments!=null) {
					ProgramProperties.UpdateProgramPropertyWithValue(programPropertyWebOnlinePayments,POut.Bool(false));
				}
			}
			#endregion Save
			for(int i=0;i<_listProgramProperties.Count;i++) {
				if(_listProgramProperties[i].IsHighSecurity && _listProgramProperties[i].PropertyValue!=_listProgramProperties[i].TagOD.ToString()) {
					StringBuilder stringBuilderLogText=new StringBuilder();
					stringBuilderLogText.Append(_program.ProgDesc+"'s "+_listProgramProperties[i].PropertyDesc);
					if(PrefC.HasClinicsEnabled) {
						long clinicNumCur=_listProgramProperties[i].ClinicNum;
						if(clinicNumCur==0) {
							stringBuilderLogText.Append(" for Headquarters");
						}
						else {
							stringBuilderLogText.Append(" for clinic "+clinicNumCur);
						}
					}
					stringBuilderLogText.Append(" was altered.");
					SecurityLogs.MakeLogEntry(EnumPermType.ManageHighSecurityProgProperties,0,stringBuilderLogText.ToString(),_program.ProgramNum,DateTime.Now);
				}
			}
			DataValid.SetInvalid(InvalidType.Programs);
			DialogResult=DialogResult.OK;
		}

	}
}