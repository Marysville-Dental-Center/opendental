using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using CodeBase;
using OpenDentBusiness;
using OpenDentBusiness.VendorDirect;
using EdgeExpressProps=OpenDentBusiness.ProgramProperties.PropertyDescs.EdgeExpress;
using EdgeExpressPropsDirect=OpenDentBusiness.EdgeExpress.PropertyDescs;

namespace OpenDental {
	public partial class FormEdgeExpressSetup:FormODBase {
		private Program _program;
		///<summary>Used to revert the clinic drop down if the user tries to change clinics and the payment type hasn't been set.</summary>
		private long _clinicNumRevert;
		private List<Def> _listDefs;
		///<summary>List of EdgeExpress prog props for all clinics.  Includes props with ClinicNum=0 for headquarters/props unassigned to a clinic.
		///The Auth Key rows hold the stored value: osd1$ once this window has saved it, or Open Dental's plain text before that.</summary>
		private List<ProgramProperty> _listProgramProperties;
		private List<ProgramProperty> _listProgramPropertiesWebPay=new List<ProgramProperty>();
		///<summary>The Auth Key of each clinic as it was when the window opened, decrypted. Used to log changes to the key.</summary>
		private Dictionary<long,string> _dictAuthKeysOld=new Dictionary<long,string>();

		public FormEdgeExpressSetup() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormEdgeExpressSetup_Load(object sender,EventArgs e) {
			_program=Programs.GetCur(ProgramName.EdgeExpress);
			if(_program==null) {
				return;//should never happen
			}
			if(PrefC.HasClinicsEnabled) {
				groupPaySettings.Text=Lan.g(this,"Clinic Payment Settings");
				if(Security.CurUser.ClinicIsRestricted) {
					//if program link is enabled, disable the enable check box so the restricted user cannot disable for all clinics
					checkEnabled.Enabled=!_program.Enabled;
					checkUseTestGateway.Enabled=false;//Affects all clinics.
				}
				comboClinic.ClinicNumSelected=Clinics.ClinicNum;
				_clinicNumRevert=Clinics.ClinicNum;
			}
			else {//clinics not enabled
				checkEnabled.Text=Lan.g(this,"Enabled");
				checkUseTestGateway.Text=Lan.g(this,"Use the card processor's test gateway");
				labelClinicEnable.Visible=false;
				groupPaySettings.Text=Lan.g(this,"Payment Settings");
			}
			checkEnabled.Checked=_program.Enabled;
			_listProgramProperties=ProgramProperties.GetForProgram(_program.ProgramNum);
			for(int i=0;i<_listProgramProperties.Count;i++) {
				if(_listProgramProperties[i].PropertyDesc==EdgeExpressProps.AuthKey) {
					_dictAuthKeysOld[_listProgramProperties[i].ClinicNum]=ReadAuthKey(_listProgramProperties[i].PropertyValue);
				}
			}
			if(!ProgramProperties.CanEditProperties(_listProgramProperties)) {
				textAuthKey.ReadOnly=true;
			}
			checkUseTestGateway.Checked=ProgramProperties.GetPropValFromList(_listProgramProperties,EdgeExpressPropsDirect.UseTestGateway,0)=="1";
			comboReturnMode.Items.Clear();
			comboReturnMode.Items.Add(Lan.g(this,"In this program (default)"),EdgeExpressReturnMode.Embedded);
			comboReturnMode.Items.Add(Lan.g(this,"Local listener on this computer (127.0.0.1)"),EdgeExpressReturnMode.Loopback);
			FillFields();
		}

		///<summary>The Auth Key a stored value holds: osd1$ decrypted, or Open Dental's plain text. Blank if it can't be read, so it gets re-entered.
		///</summary>
		private static string ReadAuthKey(string stored) {
			return SecretValue.ReadOrBlank(stored,LegacyPolicy.AcceptPlaintext);
		}

		///<summary>The value of the clinic's row, or the practice's row (ClinicNum 0) when the clinic has none. The card entry settings fall back to
		///the practice's at runtime too (ProgramProperties.GetValOrDefault).</summary>
		private string GetPropValWithDefault(string propertyDesc,long clinicNum) {
			if(_listProgramProperties.Exists(x => x.ClinicNum==clinicNum && x.PropertyDesc==propertyDesc)) {
				return ProgramProperties.GetPropValFromList(_listProgramProperties,propertyDesc,clinicNum);
			}
			return ProgramProperties.GetPropValFromList(_listProgramProperties,propertyDesc,0);
		}

		///<summary>Fills all but comboClinic, checkEnabled which are filled on load.</summary>
		private void FillFields() {
			long clinicNum=0;
			if(PrefC.HasClinicsEnabled && comboClinic.ClinicNumSelected>-1) {
				clinicNum=comboClinic.ClinicNumSelected;
			}
			//The Auth Key is stored as osd1$. Open Dental stored it as plain text, which is still read and is encrypted on the next save. A value that
			//doesn't decrypt shows blank, so it gets re-entered.
			textAuthKey.Text=ReadAuthKey(ProgramProperties.GetPropValFromList(_listProgramProperties,EdgeExpressProps.AuthKey,clinicNum));
			//PaymentType ComboBox
			string payTypeDefNum=ProgramProperties.GetPropValFromList(_listProgramProperties,EdgeExpressProps.PaymentType,clinicNum);
			comboPaymentType.Items.Clear();
			_listDefs=Defs.GetDefsForCategory(DefCat.PaymentTypes,true);
			comboPaymentType.Items.AddDefs(_listDefs);
			comboPaymentType.SelectedIndex=_listDefs.FindIndex(x => x.DefNum.ToString()==payTypeDefNum);
			//Other text boxes and check boxes
			textXWebID.Text=ProgramProperties.GetPropValFromList(_listProgramProperties,EdgeExpressProps.XWebID,clinicNum);
			textTerminalID.Text=ProgramProperties.GetPropValFromList(_listProgramProperties,EdgeExpressProps.TerminalID,clinicNum);
			checkPromptSig.Checked=PIn.Bool(ProgramProperties.GetPropValFromList(_listProgramProperties,EdgeExpressProps.PromptSignature,clinicNum));
			checkPrintReceipt.Checked=PIn.Bool(ProgramProperties.GetPropValFromList(_listProgramProperties,EdgeExpressProps.PrintReceipt,clinicNum));
			checkForceDuplicate.Checked=PIn.Bool(ProgramProperties.GetPropValFromList(_listProgramProperties,EdgeExpressProps.ForceRecurringCharge,clinicNum));
			checkPreventSavingNewCC.Checked=PIn.Bool(ProgramProperties.GetPropValFromList(_listProgramProperties,EdgeExpressProps.PreventSavingNewCC,clinicNum));
			checkWebPayEnabled.Checked=PIn.Bool(ProgramProperties.GetPropValFromList(_listProgramProperties,EdgeExpressProps.IsOnlinePaymentsEnabled,clinicNum));
			//Card entry page
			comboReturnMode.SetSelectedEnum(EdgeExpress.CNP.ParseReturnMode(GetPropValWithDefault(EdgeExpressPropsDirect.ReturnMode,clinicNum)));
			textReturnUrl.Text=GetPropValWithDefault(EdgeExpressPropsDirect.ReturnUrl,clinicNum);
		}

		private void comboClinic_SelectionChangeCommitted(object sender,EventArgs e) {
			if(comboClinic.ClinicNumSelected==_clinicNumRevert) {//didn't change the selected clinic
				return;
			}
			//EdgeExpress will be enabled for the clinic if the enabled checkbox is checked and the 3 XWeb fields are not blank. Don't let them switch
			//clinics or close the form with only 1 or 2 of the three fields filled in.  If they fill in 1, they must fill in the other 2.
			bool isXWebEnabled=checkEnabled.Checked && (textXWebID.Text.Trim().Length>0 || textAuthKey.Text.Trim().Length>0
				|| textTerminalID.Text.Trim().Length>0);
			if(isXWebEnabled && !ValidateXWeb()) {//error message box displayed in ValidateXWeb()
				comboClinic.ClinicNumSelected=_clinicNumRevert;//validation didn't pass, revert clinic choice so they have to fix it
				return;//if any of the X-Web fields do not pass validation, return
			}
			if(!ValidateReturnUrl()) {
				comboClinic.ClinicNumSelected=_clinicNumRevert;
				return;
			}
			//if the payment type currently set is not valid and X-Charge is enabled, revert the clinic and return, message box shown in ValidatePaymentTypes
			if(!ValidatePaymentTypes(isAllClinics:false)) {
				comboClinic.ClinicNumSelected=_clinicNumRevert;//revert clinic selection, X-Charge is enabled and the payment type is not valid
				return;
			}
			string authKeyStored;
			if(!TryProtectAuthKey(out authKeyStored)) {
				comboClinic.ClinicNumSelected=_clinicNumRevert;
				return;
			}
			SyncWithHQ(authKeyStored);//if the user just modified the HQ credentials, change any credentials that were the same as HQ to keep them synched
			UpdateAllProperties(_clinicNumRevert,authKeyStored);
			_clinicNumRevert=comboClinic.ClinicNumSelected;//now that we've updated the values for the clinic we're switching from, update _clinicNumRevert
			FillFields();
		}

		///<summary>Validate XWebID, AuthKey, and TerminalID.  XWebID and TerminalID must be numbers only, 12 digits and 8 digits long respectively.
		///AuthKey must be alphanumeric, 32 digits long.</summary>
		private bool ValidateXWeb(bool suppressMessage = false) {
			//Validate ALL XWebID, AuthKey, and TerminalID.  Each is required for X-Web to work.
			if(!Regex.IsMatch(textXWebID.Text.Trim(),"^[0-9]{12}$")) {
				if(!suppressMessage) {
					MsgBox.Show(this,"XWeb ID must be 12 digits.");
				}
				return false;
			}
			if(!Regex.IsMatch(textAuthKey.Text.Trim(),"^[A-Za-z0-9]{32}$")) {
				if(!suppressMessage) {
					MsgBox.Show(this,"Auth Key must be 32 alphanumeric characters.");
				}
				return false;
			}
			if(!Regex.IsMatch(textTerminalID.Text.Trim(),"^[0-9]{8}$")) {
				if(!suppressMessage) {
					MsgBox.Show(this,"Terminal ID must be 8 digits.");
				}
				return false;
			}
			return true;
		}

		///<summary>The return URL must be blank (the built-in address) or a full https address that isn't an Open Dental host.</summary>
		private bool ValidateReturnUrl() {
			string problem=EdgeExpress.CNP.ValidateReturnUrl(textReturnUrl.Text);
			if(problem=="") {
				return true;
			}
			MessageBox.Show(Lan.g(this,"Return URL:")+" "+Lan.g(this,problem));
			return false;
		}

		///<summary>Encrypts the Auth Key in the text box for storage. Shows a message and returns false if encryption fails; nothing is saved then.
		///</summary>
		private bool TryProtectAuthKey(out string authKeyStored) {
			if(SecretValue.Protect(textAuthKey.Text.Trim(),out authKeyStored)) {
				return true;
			}
			MsgBox.Show(this,"The Auth Key could not be encrypted, so nothing was saved. Try again.");
			return false;
		}

		///<summary>If isValidateAllClinics is true, validates the PaymentType for all clinics with EdgeExpress enabled.
		///<para>If the current user is restricted to a clinic or if clinics are not enabled and the enabled checkbox is checked,
		///or if isValidateAllClinics is false, only comboPaymentType.SelectedIndex will be validated.</para>
		///<para>If clinics are enabled and isValidateAllClinics is true and the current user is not restricted to a clinic, the PaymentType for any
		///clinic with Username and Password set or with any of the XWeb settings set will be validated.</para></summary>
		private bool ValidatePaymentTypes(bool isAllClinics) {
			//if not enabled, don't worry about invalid payment type
			if(!checkEnabled.Checked) {
				return true;
			}
			bool isClientEnabled=!PrefC.HasClinicsEnabled || textXWebID.Text.Trim().Length>0 || textAuthKey.Text.Trim().Length>0  ||
				textTerminalID.Text.Trim().Length>0;
			if(isClientEnabled && comboPaymentType.SelectedIndex<0) {
				MsgBox.Show(this,"Please select a payment type first.");
				return false;
			}
			if(!isAllClinics || !PrefC.HasClinicsEnabled || Security.CurUser.ClinicIsRestricted) {
				return true;
			}
			//only validate payment types for all clinics if isAllClinics==true and clinics are enabled and the current user is not restricted to a clinic
			string propValPayType="";
			List<long> listUserClinicNums=comboClinic.ListClinics.Select(x => x.ClinicNum).ToList();
			//make sure all clinics with EdgeExpress enabled also have a payment type selected
			for(int i=0;i<listUserClinicNums.Count;i++) {
				propValPayType=ProgramProperties.GetPropValFromList(_listProgramProperties,EdgeExpressProps.PaymentType,listUserClinicNums[i]);
				//isClientEnabled will be true if both username and password are set for this clinic
				isClientEnabled=ProgramProperties.GetPropValFromList(_listProgramProperties,"Username",listUserClinicNums[i]).Length>0
					&& ProgramProperties.GetPropValFromList(_listProgramProperties,"Password",listUserClinicNums[i]).Length>0;
				if(isClientEnabled && !_listDefs.Any(x => x.DefNum.ToString()==propValPayType)) {
					MsgBox.Show(this,"Please select the payment type for all clinics with EdgeExpress enabled.");
					return false;
				}
			}
			return true;
		}

		///<summary>Updates the values in the local list of program properties for each clinic.
		///Only modifies other clinics if _listUserClinicNums[_indexClinicRevert]=0, meaning user just modified the HQ values.
		///If the clinic EdgeExpress client Username and Password are the same as HQ, the clinic values will be updated with the values entered.
		///If the clinic XWeb values are the same as HQ, the clinic XWeb values will be updated with the values entered.
		///If both the EdgeExpress client values and the XWeb values are the same as HQ, the payment type will be updated.
		///The values in the local list for HQ, or for the clinic modified if it was not HQ, have to be updated after calling this method.
		///"HQ" here means the practice-level (ClinicNum 0) credentials. Auth Keys are compared decrypted, so a clinic still holding Open Dental's
		///plain-text copy of the same key stays in sync. authKeyStored is the encrypted key from the text box.</summary>
		private void SyncWithHQ(string authKeyStored) {
			if(!PrefC.HasClinicsEnabled || _clinicNumRevert>0) {
				return;
			}
			string hqXWebID=ProgramProperties.GetPropValFromList(_listProgramProperties,EdgeExpressProps.XWebID,0);//HQ XWebID before updating to value in textbox
			string hqAuthKey=ReadAuthKey(ProgramProperties.GetPropValFromList(_listProgramProperties,EdgeExpressProps.AuthKey,0));//HQ AuthKey before updating to value in textbox
			string hqTerminalID=ProgramProperties.GetPropValFromList(_listProgramProperties,EdgeExpressProps.TerminalID,0);//HQ TerminalID before updating to value in textbox
			string hqPayType=ProgramProperties.GetPropValFromList(_listProgramProperties,EdgeExpressProps.PaymentType,0);//HQ PaymentType before updating to combo box selection
			string strPayTypeDefNum="";
			if(comboPaymentType.SelectedIndex>-1) {
				strPayTypeDefNum=_listDefs[comboPaymentType.SelectedIndex].DefNum.ToString();
			}
			//for each distinct ClinicNum in the prog property list for EdgeExpress except HQ
			List<long> listClinicNums=_listProgramProperties.FindAll(x => x.ClinicNum>0).Select(x => x.ClinicNum).Distinct().ToList();
			for(int i=0;i<listClinicNums.Count;i++) {
				//Updates the PaymentType in both if checks, in case the other isn't met so the payment type will be synched if either condition is true.
				//only if all three XWeb HQ values are not blank
				bool isXWebSynch=_listProgramProperties.Exists(x => x.ClinicNum==listClinicNums[i] && x.PropertyDesc==EdgeExpressProps.XWebID && x.PropertyValue==hqXWebID)
					&& _listProgramProperties.Exists(x => x.ClinicNum==listClinicNums[i] && x.PropertyDesc==EdgeExpressProps.AuthKey && ReadAuthKey(x.PropertyValue)==hqAuthKey)
					&& _listProgramProperties.Exists(x => x.ClinicNum==listClinicNums[i] && x.PropertyDesc==EdgeExpressProps.TerminalID && x.PropertyValue==hqTerminalID);
				if(!isXWebSynch) {
					continue;
				}
				UpdateProperty(listClinicNums[i],EdgeExpressProps.XWebID,textXWebID.Text.Trim());
				UpdateProperty(listClinicNums[i],EdgeExpressProps.AuthKey,authKeyStored);
				UpdateProperty(listClinicNums[i],EdgeExpressProps.TerminalID,textTerminalID.Text.Trim());
				//only synch payment type if both client and XWeb values are the same as HQ and the payment type is valid
				if(!string.IsNullOrEmpty(strPayTypeDefNum)) {
					_listProgramProperties.FindAll(x => x.ClinicNum==listClinicNums[i] && x.PropertyDesc==EdgeExpressProps.PaymentType && x.PropertyValue==hqPayType)
						.ForEach(x => x.PropertyValue=strPayTypeDefNum);//always 1 item; null safe
				}
			}
		}

		///<summary>Updates the program property for this clinic. Creates and adds to the list if it cannot be found. The Auth Key row is always masked and
		///high security.</summary>
		void UpdateProperty(long clinicNum,string propDesc,string propVal) {
			ProgramProperty programProperty=_listProgramProperties.FirstOrDefault(x => x.ClinicNum==clinicNum && x.PropertyDesc==propDesc);
			if(programProperty==null) {
				programProperty=new ProgramProperty {
					ProgramNum=_program.ProgramNum,
					ClinicNum=clinicNum,
					PropertyDesc=propDesc,
				};
				_listProgramProperties.Add(programProperty);
			}
			programProperty.PropertyValue=propVal;
			if(propDesc==EdgeExpressProps.AuthKey) {
				programProperty.IsMasked=true;
				programProperty.IsHighSecurity=true;
			}
		}

		///<summary>authKeyStored is the encrypted Auth Key from the text box (TryProtectAuthKey).</summary>
		private void UpdateAllProperties(long clinicNum,string authKeyStored) {
			string strPayTypeDefNum="";
			if(comboPaymentType.SelectedIndex>-1) {
				strPayTypeDefNum=_listDefs[comboPaymentType.SelectedIndex].DefNum.ToString();
			}
			UpdateProperty(clinicNum,EdgeExpressProps.PromptSignature,POut.Bool(checkPromptSig.Checked));
			UpdateProperty(clinicNum,EdgeExpressProps.PrintReceipt,POut.Bool(checkPrintReceipt.Checked));
			UpdateProperty(clinicNum,EdgeExpressProps.XWebID,textXWebID.Text.Trim());
			UpdateProperty(clinicNum,EdgeExpressProps.AuthKey,authKeyStored);
			UpdateProperty(clinicNum,EdgeExpressProps.TerminalID,textTerminalID.Text.Trim());
			UpdateProperty(clinicNum,EdgeExpressProps.PaymentType,strPayTypeDefNum);
			UpdateProperty(clinicNum,EdgeExpressProps.ForceRecurringCharge,POut.Bool(checkForceDuplicate.Checked));
			UpdateProperty(clinicNum,EdgeExpressProps.PreventSavingNewCC,POut.Bool(checkPreventSavingNewCC.Checked));
			UpdateProperty(clinicNum,EdgeExpressProps.IsOnlinePaymentsEnabled,POut.Bool(checkWebPayEnabled.Checked));
			UpdateProperty(clinicNum,EdgeExpressPropsDirect.ReturnMode,comboReturnMode.GetSelected<EdgeExpressReturnMode>().ToString());
			UpdateProperty(clinicNum,EdgeExpressPropsDirect.ReturnUrl,textReturnUrl.Text.Trim());
		}

		private void checkWebPayEnabled_Click(object sender,EventArgs e) {
			if(!checkWebPayEnabled.Checked) {
				return;
			}
			long clinicNum=0;
			if(PrefC.HasClinicsEnabled) {
				clinicNum=comboClinic.ClinicNumSelected;
			}
			ProgramProperty programProperty=ProgramProperties.GetOnlinePaymentsEnabledForClinic(clinicNum,ProgramName.EdgeExpress);
			string msg=Lan.g(this,"Online payments is already enabled for another processor and must be disabled in order to use EdgeExpress online payments. "
				+"Would you like to disable the other processor for online payments?");
			if(programProperty!=null) {
				if(MessageBox.Show(msg,"",MessageBoxButtons.OKCancel)==DialogResult.Cancel) {
					checkWebPayEnabled.Checked=false;
					return;
				}
				//User wants to set as new processor for online payments, add to the list for the current session to remove currently enabled program.
				_listProgramPropertiesWebPay.Add(programProperty);
			}
		}

		private void butSave_Click(object sender,System.EventArgs e) {
			if(_program==null) {//should never happen
				MsgBox.Show(this,"EdgeExpress entry is missing from the database.");
				return;
			}
			//EdgeExpress will be enabled for the clinic if the enabled checkbox is checked and the 3 XWeb fields are not blank. Don't let them switch
			//clinics or close the form with only 1 or 2 of the three fields filled in.  If they fill in 1, they must fill in the other 2.
			bool isXWebEnabled=checkEnabled.Checked && (textXWebID.Text.Trim().Length>0 || textAuthKey.Text.Trim().Length>0
				|| textTerminalID.Text.Trim().Length>0);
			if(isXWebEnabled && !ValidateXWeb()) {//error message box displayed in ValidateXWeb()
				return;
			}
			if(!ValidateReturnUrl()) {
				return;
			}
			string authKeyStored;
			if(!TryProtectAuthKey(out authKeyStored)) {
				return;
			}
			//if the user just modified the HQ credentials, change any credentials that were the same as HQ to keep them synched
			SyncWithHQ(authKeyStored);
			//get selected ClinicNum (if enabled), PaymentType, encrypted Password, and encrypted AuthKey
			long clinicNum=0;
			if(PrefC.HasClinicsEnabled) {
				clinicNum=comboClinic.ClinicNumSelected;
			}
			UpdateAllProperties(clinicNum,authKeyStored);
			//validate the payment type set for all clinics with EdgeExpress enabled
			if(!ValidatePaymentTypes(isAllClinics:true)) {//if validation fails, message box will already have been shown
				return;
			}
			if(checkUseTestGateway.Enabled) {
				UpdateProperty(0,EdgeExpressPropsDirect.UseTestGateway,checkUseTestGateway.Checked ? "1" : "0");
			}
			if(_program.Enabled!=checkEnabled.Checked) {
				_program.Enabled=checkEnabled.Checked;
				Programs.Update(_program);
			}
			//The vendor job host keeps its lease in a row of this program. Sync from the cache's current copy, so this window never writes a stale lease.
			_listProgramProperties.RemoveAll(x => VendorJobLease.IsLeaseProperty(x.PropertyDesc));
			_listProgramProperties.AddRange(ProgramProperties.GetForProgram(_program.ProgramNum).FindAll(x => VendorJobLease.IsLeaseProperty(x.PropertyDesc)));
			ProgramProperties.Sync(_listProgramProperties,_program.ProgramNum);
			LogAuthKeyChanges();
			string strEdgeExpressOnlinePaymentsEnabled=EdgeExpressProps.IsOnlinePaymentsEnabled;
			//Find all clinics that have EdgeExpress online payments enabled
			List<ProgramProperty> listEdgeExpressOnlinePayments=_listProgramProperties.FindAll(x => x.PropertyDesc==strEdgeExpressOnlinePaymentsEnabled &&
				PIn.Bool(x.PropertyValue));
			for(int i=0;i < listEdgeExpressOnlinePayments.Count;i++) {
				//Find all online payment enabled program properties that we saved in this session. Only clinics that have changes will have an
				//IsOnlinePaymentsEnabled property in memory. This is needed to ensure that we don't disable other processors if someone
				//checks to use EdgeExpress online payments and then decides to keep it disabled during the same session.
				ProgramProperty programProperty=_listProgramPropertiesWebPay.FirstOrDefault(y => y.ClinicNum==listEdgeExpressOnlinePayments[i].ClinicNum);
				if(programProperty!=null) {
					ProgramProperties.UpdateProgramPropertyWithValue(programProperty,POut.Bool(false));
				}
			}
			DataValid.SetInvalid(InvalidType.Programs);
			DialogResult=DialogResult.OK;
		}

		///<summary>Makes a security log entry for each clinic whose Auth Key changed (compared decrypted, so re-encrypting Open Dental's plain-text copy
		///of the same key isn't a change). The key itself is never logged.</summary>
		private void LogAuthKeyChanges() {
			List<ProgramProperty> listAuthKeys=_listProgramProperties.FindAll(x => x.PropertyDesc==EdgeExpressProps.AuthKey);
			for(int i=0;i<listAuthKeys.Count;i++) {
				string authKeyOld;
				if(!_dictAuthKeysOld.TryGetValue(listAuthKeys[i].ClinicNum,out authKeyOld)) {
					authKeyOld="";
				}
				if(ReadAuthKey(listAuthKeys[i].PropertyValue)==authKeyOld) {
					continue;
				}
				string logText=_program.ProgDesc+"'s Auth Key";
				if(PrefC.HasClinicsEnabled) {
					if(listAuthKeys[i].ClinicNum==0) {
						logText+=" for Headquarters";
					}
					else {
						logText+=" for clinic "+listAuthKeys[i].ClinicNum;
					}
				}
				logText+=" was altered.";
				SecurityLogs.MakeLogEntry(EnumPermType.ManageHighSecurityProgProperties,0,logText,_program.ProgramNum,DateTime.Now);
			}
		}

	}
}
