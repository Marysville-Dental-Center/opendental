using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Windows.Forms;
using CodeBase;
using CodeBase.VendorDirect;
using OpenDental.VendorDirect;
using OpenDentBusiness;
using OpenDentBusiness.Qbo;

namespace OpenDental {
	///<summary>QuickBooks Online setup: the office's own Intuit app keys and redirect address, Connect and Disconnect, and the accounts and classes staff
	///may pick when sending a deposit. Tokens are never shown. Talks only to Intuit.</summary>
	public partial class FormQuickBooksOnlineSetup:FormODBase {
		///<summary>Only show Enabled checkbox if we are opening this form from the Program Links window.</summary>
		private bool _isFromFormProgramLinks;
		///<summary>Holds the current state of the QuickBooks Online program.</summary>
		private Program _progCur;
		///<summary>Holds the state of the QuickBooks Online program stored in the DB.</summary>
		private Program _progOld;
		///<summary>The saved settings, reloaded after every change to the connection.</summary>
		private QboSettings _qboSettings;
		///<summary>True after the user clicked Change... for the client secret. The secret is written only then.</summary>
		private bool _isClientSecretChanged;
		///<summary>List of deposit accounts selected for use in Open Dental.</summary>
		private List<QuickBooksOnlineAccount> _listDepositAccountsInOD;
		///<summary>List of deposit accounts available from QuickBooks Online.</summary>
		List<QuickBooksOnlineAccount> _listDepositAccountsAvailable=new List<QuickBooksOnlineAccount>();
		///<summary>List of income accounts selected for use in Open Dental.</summary>
		private List<QuickBooksOnlineAccount> _listIncomeAccountsInOD;
		///<summary>List of income accounts available from QuickBooks Online.</summary>
		List<QuickBooksOnlineAccount> _listIncomeAccountsAvailable=new List<QuickBooksOnlineAccount>();
		///<summary>List of class refs selected for use in Open Dental.</summary>
		private List<QuickBooksOnlineClassRef> _listClassRefsInOD;
		///<summary>List of class refs available from QuickBooks Online.</summary>
		List<QuickBooksOnlineClassRef> _listClassRefsAvailable=new List<QuickBooksOnlineClassRef>();
		///<summary>True when the lists could not be loaded from QBO (or weren't loaded), so "No longer in QBO" isn't shown.</summary>
		private bool _hasQboError=true;

		public FormQuickBooksOnlineSetup(bool IsFromFormProgramLinks=false) {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
			_isFromFormProgramLinks=IsFromFormProgramLinks;
		}

		private void FormQuickBooksOnlineSetup_Load(object sender,EventArgs e) {
			if(QboSettings.EnsurePropertyRows()) {
				DataValid.SetInvalid(InvalidType.Programs);
			}
			_progCur=Programs.GetCur(ProgramName.QuickBooksOnline);
			_progOld=_progCur.Copy();
			_qboSettings=QboSettings.Load();
			_listDepositAccountsInOD=QuickBooksOnlineAccount.StringAccountsToList(_qboSettings.DepositAccounts);
			_listIncomeAccountsInOD=QuickBooksOnlineAccount.StringAccountsToList(_qboSettings.IncomeAccounts);
			_listClassRefsInOD=QuickBooksOnlineClassRef.StringClassRefsToList(_qboSettings.ClassRefs);
			comboEnvironment.Items.Add(Lan.g(this,"Production"),QboEnvironment.Production);
			comboEnvironment.Items.Add(Lan.g(this,"Sandbox"),QboEnvironment.Sandbox);
			checkEnabled.Visible=_isFromFormProgramLinks;
			checkEnabled.Checked=_progCur.Enabled;
			FillConnectionFields();
			EnableOrDisableControls();
			FillAllListBoxes();
			if(checkEnabled.Checked && _qboSettings.IsConnected) {
				GetAccountsAndClassRefsFromQuickBooksOnline();
			}
		}

		///<summary>Fills the connection group from _qboSettings. The secret box only shows whether a secret is saved.</summary>
		private void FillConnectionFields() {
			comboEnvironment.SelectedIndex=(_qboSettings.Environment==QboEnvironment.Sandbox) ? 1 : 0;
			textClientId.Text=_qboSettings.ClientId;
			_isClientSecretChanged=false;
			textClientSecret.ReadOnly=true;
			textClientSecret.Text=(_qboSettings.ClientSecret=="") ? "" : new string('*',12);
			textRedirectUri.Text=_qboSettings.RedirectUri;
			textRelayPort.Text=(_qboSettings.RelayPort>0) ? _qboSettings.RelayPort.ToString() : "";
			FillStatus();
		}

		private void FillStatus() {
			if(_qboSettings.IsConnected) {
				if(QuickBooksOnline.IsReconnectNeeded()) {
					labelStatusValue.Text=Lan.g(this,"Needs reconnecting");
					return;
				}
				string text=Lan.g(this,"Connected to")+" "+(_qboSettings.CompanyName=="" ? Lan.g(this,"a QuickBooks Online company") : _qboSettings.CompanyName);
				if(_qboSettings.RefreshTokenExpiresUtc.HasValue) {
					text+=" ("+Lan.g(this,"valid until")+" "+_qboSettings.RefreshTokenExpiresUtc.Value.ToLocalTime().ToShortDateString()+")";
				}
				labelStatusValue.Text=text;
				return;
			}
			if(_qboSettings.HasUnreadableLegacyValues) {
				labelStatusValue.Text=Lan.g(this,"The saved connection is from another installation and cannot be used. Click Connect.");
				return;
			}
			labelStatusValue.Text=Lan.g(this,"Not connected");
		}

		private void EnableOrDisableControls() {
			bool isEnabled=checkEnabled.Checked;
			comboEnvironment.Enabled=isEnabled;
			textClientId.Enabled=isEnabled;
			butChangeSecret.Enabled=isEnabled;
			textRedirectUri.Enabled=isEnabled;
			textRelayPort.Enabled=isEnabled;
			butConnect.Enabled=isEnabled;
			butDisconnect.Enabled=isEnabled && _qboSettings.HasRefreshToken;
			butConnect.Text=_qboSettings.IsConnected ? Lan.g(this,"Reconnect...") : Lan.g(this,"Connect...");
			butAddDepositAccount.Enabled=isEnabled;
			butRemoveDepositAccount.Enabled=isEnabled;
			butAddIncomeAccount.Enabled=isEnabled;
			butRemoveIncomeAccount.Enabled=isEnabled;
			butAddClass.Enabled=isEnabled;
			butRemoveClass.Enabled=isEnabled;
			listBoxDepositAccountsInOD.Enabled=isEnabled;
			listBoxDepositAccountsAvailable.Enabled=isEnabled;
			listBoxIncomeAccountsInOD.Enabled=isEnabled;
			listBoxIncomeAccountsAvailable.Enabled=isEnabled;
			listBoxClassRefsInOD.Enabled=isEnabled;
			listBoxClassRefsAvailable.Enabled=isEnabled;
		}

		///<summary>Loads the accounts and classes from QuickBooks Online on a background thread, behind a progress window the user can cancel.</summary>
		private void GetAccountsAndClassRefsFromQuickBooksOnline() {
			_hasQboError=true;
			if(!_qboSettings.IsConnected) {
				FillAllListBoxes();
				return;
			}
			string realmId=_qboSettings.RealmId;
			List<QuickBooksOnlineAccount> listDepositAccounts=null;
			List<QuickBooksOnlineAccount> listIncomeAccounts=null;
			List<QuickBooksOnlineClassRef> listClassRefs=null;
			UI.ProgressWin progressWin=new UI.ProgressWin();
			progressWin.StartingMessage=Lan.g(this,"Loading from QuickBooks Online...");
			progressWin.ActionMain=() => {
				listDepositAccounts=QuickBooksOnline.GetDepositAccounts(realmId,null);
				listIncomeAccounts=QuickBooksOnline.GetIncomeAccounts(realmId,null);
				listClassRefs=QuickBooksOnline.GetClassRefs(realmId,null);
			};
			try {
				progressWin.ShowDialog();
			}
			catch(Exception ex) {
				FriendlyException.Show(ex.Message,ex);
				FillAllListBoxes();
				return;
			}
			if(progressWin.IsCancelled) {
				FillAllListBoxes();
				return;
			}
			Exception exRequest=GetRequestException(listDepositAccounts) ?? GetRequestException(listIncomeAccounts) ?? GetRequestException(listClassRefs);
			if(exRequest!=null) {
				MessageBox.Show(this,exRequest.Message,Lan.g(this,"QuickBooks Online"));
				_listDepositAccountsAvailable=new List<QuickBooksOnlineAccount>();
				_listIncomeAccountsAvailable=new List<QuickBooksOnlineAccount>();
				_listClassRefsAvailable=new List<QuickBooksOnlineClassRef>();
				FillStatus();
				FillAllListBoxes();
				return;
			}
			_listDepositAccountsAvailable=listDepositAccounts;
			_listIncomeAccountsAvailable=listIncomeAccounts;
			_listClassRefsAvailable=listClassRefs;
			_hasQboError=false;
			FillAllListBoxes();
		}

		private static Exception GetRequestException(List<QuickBooksOnlineAccount> listAccounts) {
			if(listAccounts==null) {
				return new ODException(Lan.g("FormQuickBooksOnlineSetup","Nothing was loaded from QuickBooks Online."));
			}
			return listAccounts.FirstOrDefault(x => x.RequestException!=null)?.RequestException;
		}

		private static Exception GetRequestException(List<QuickBooksOnlineClassRef> listClassRefs) {
			if(listClassRefs==null) {
				return new ODException(Lan.g("FormQuickBooksOnlineSetup","Nothing was loaded from QuickBooks Online."));
			}
			return listClassRefs.FirstOrDefault(x => x.RequestException!=null)?.RequestException;
		}

		private void FillAllListBoxes() {
			FillListBoxesWithAccounts(_listDepositAccountsInOD,_listDepositAccountsAvailable,listBoxDepositAccountsInOD,listBoxDepositAccountsAvailable);
			FillListBoxesWithAccounts(_listIncomeAccountsInOD,_listIncomeAccountsAvailable,listBoxIncomeAccountsInOD,listBoxIncomeAccountsAvailable);
			FillListBoxesWithClassRefs();
		}

		private void FillListBoxesWithAccounts(List<QuickBooksOnlineAccount> listAccountsInOD,List<QuickBooksOnlineAccount> listAccountsAvailable
			,UI.ListBox listBoxInOD,UI.ListBox listBoxAvailable)
		{
			listBoxInOD.Items.Clear();
			listBoxAvailable.Items.Clear();
			List<QuickBooksOnlineAccount> listAccountsInBoth=new List<QuickBooksOnlineAccount>();
			for(int i=0;i<listAccountsInOD.Count;i++) {
				QuickBooksOnlineAccount accountAvailable=listAccountsAvailable.FirstOrDefault(x => x.Id==listAccountsInOD[i].Id);
				if(accountAvailable!=null && QboEntityList.IsStorableName(accountAvailable.Name)) {
					//Matched by Id: take the current (fully qualified) name, so a rename or a sub-account shows and is saved correctly.
					listAccountsInOD[i].Name=accountAvailable.Name;
				}
			}
			listAccountsInOD.Sort((x,y) => string.Compare(x.Name,y.Name,StringComparison.CurrentCulture));
			listAccountsAvailable=listAccountsAvailable.OrderBy(x => x.Name).ToList();
			for(int i=0;i<listAccountsInOD.Count;i++) {
				QuickBooksOnlineAccount accountAvailable=listAccountsAvailable.FirstOrDefault(x => x.Id==listAccountsInOD[i].Id);
				string displayText=listAccountsInOD[i].Name;
				if(accountAvailable==null) {
					if(!_hasQboError) {
						displayText+=" "+Lan.g(this,"(No longer in QBO)");//We successfully got accounts from QBO and this account no longer exists.
					}
				}
				else {
					listAccountsInBoth.Add(accountAvailable);
				}
				listBoxInOD.Items.Add(displayText,listAccountsInOD[i]);
			}
			for(int i=0;i<listAccountsAvailable.Count;i++) {
				//If account already displays as selected for use in OD, then don't display it in available accounts.
				if(listAccountsInBoth.Contains(listAccountsAvailable[i])) {
					continue;
				}
				listBoxAvailable.Items.Add(listAccountsAvailable[i].Name,listAccountsAvailable[i]);
			}
		}

		private void FillListBoxesWithClassRefs() {
			listBoxClassRefsInOD.Items.Clear();
			listBoxClassRefsAvailable.Items.Clear();
			for(int i=0;i<_listClassRefsInOD.Count;i++) {
				QuickBooksOnlineClassRef classRefAvailable=_listClassRefsAvailable.FirstOrDefault(x => x.Id==_listClassRefsInOD[i].Id);
				if(classRefAvailable!=null && QboEntityList.IsStorableName(classRefAvailable.Name)) {
					_listClassRefsInOD[i].Name=classRefAvailable.Name;
				}
			}
			_listClassRefsInOD=_listClassRefsInOD.OrderBy(x => x.Name).ToList();
			_listClassRefsAvailable=_listClassRefsAvailable.OrderBy(x => x.Name).ToList();
			List<QuickBooksOnlineClassRef> listClassRefsInBoth=new List<QuickBooksOnlineClassRef>();
			for(int i=0;i<_listClassRefsInOD.Count;i++) {
				QuickBooksOnlineClassRef classRefAvailable=_listClassRefsAvailable.FirstOrDefault(x => x.Id==_listClassRefsInOD[i].Id);
				string displayText=_listClassRefsInOD[i].Name;
				if(classRefAvailable==null) {
					if(!_hasQboError) {
						displayText+=" "+Lan.g(this,"(No longer in QBO)");//We successfully got classRefs from QBO and this classRef no longer exists.
					}
				}
				else {
					listClassRefsInBoth.Add(classRefAvailable);
				}
				listBoxClassRefsInOD.Items.Add(displayText,_listClassRefsInOD[i]);
			}
			for(int i=0;i<_listClassRefsAvailable.Count;i++) {
				//If class ref already displays as selected for use in OD, then don't display it in available class refs.
				if(listClassRefsInBoth.Contains(_listClassRefsAvailable[i])) {
					continue;
				}
				listBoxClassRefsAvailable.Items.Add(_listClassRefsAvailable[i].Name,_listClassRefsAvailable[i]);
			}
		}

		private void checkEnabled_Click(object sender,EventArgs e) {
			//Can't disable QuickBooks Online if it is the currently selected deposit software.
			if(PrefC.GetEnum<AccountingSoftware>(PrefName.AccountingSoftware)==AccountingSoftware.QuickBooksOnline && _progOld.Enabled) {
				checkEnabled.Checked=true;
				MsgBox.Show(this,"You cannot disable QuickBooks Online while it is the selected Deposit Software in Setup | Preferences | Manage - General.");
				return;
			}
			EnableOrDisableControls();
			if(checkEnabled.Checked && _qboSettings.IsConnected) {
				GetAccountsAndClassRefsFromQuickBooksOnline();
			}
		}

		private void butChangeSecret_Click(object sender,EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.ManageHighSecurityProgProperties)) {
				return;
			}
			_isClientSecretChanged=true;
			textClientSecret.ReadOnly=false;
			textClientSecret.Text="";
			textClientSecret.Focus();
		}

		///<summary>Checks the connection fields. Returns false after telling the user what is wrong. isForConnect also requires the keys.</summary>
		private bool ValidateConnectionFields(bool isForConnect) {
			QboEnvironment environment=comboEnvironment.GetSelected<QboEnvironment>();
			bool hasSecret=_isClientSecretChanged ? textClientSecret.Text.Trim()!="" : _qboSettings.ClientSecret!="";
			if(isForConnect) {
				if(textClientId.Text.Trim()=="") {
					MsgBox.Show(this,"Enter the Client ID of your Intuit app.");
					return false;
				}
				if(!hasSecret) {
					MsgBox.Show(this,"Enter the Client Secret of your Intuit app.");
					return false;
				}
			}
			if(isForConnect || textRedirectUri.Text.Trim()!="") {
				string error=QboRedirectRules.Validate(environment,textRedirectUri.Text);
				if(error!="") {
					MessageBox.Show(this,Lan.g(this,error));
					return false;
				}
			}
			string errorRelay=QboRedirectRules.ValidateRelayPort(textRelayPort.Text);
			if(errorRelay!="") {
				MessageBox.Show(this,Lan.g(this,errorRelay));
				return false;
			}
			return true;
		}

		///<summary>Saves the connection fields. When the environment or client ID changed under a saved connection, asks first, then disconnects it
		///(tokens from one key set don't work with another). Returns false if the user declined or the secret could not be encrypted.</summary>
		private bool SaveConnectionFields() {
			QboEnvironment environment=comboEnvironment.GetSelected<QboEnvironment>();
			string clientId=textClientId.Text.Trim();
			bool isKeySetChanged=(environment!=_qboSettings.Environment || clientId!=_qboSettings.ClientId);
			if(isKeySetChanged && _qboSettings.HasRefreshToken) {
				if(!MsgBox.Show(this,MsgBoxButtons.YesNo,"Changing the environment or client ID disconnects the current QuickBooks Online connection. Continue?")) {
					return false;
				}
				RunDisconnect();
			}
			string clientSecretNew=_isClientSecretChanged ? textClientSecret.Text.Trim() : null;
			if(!QboSettings.SaveClientSettings(environment,clientId,textRedirectUri.Text,QboSettings.ParsePort(textRelayPort.Text),clientSecretNew)) {
				MsgBox.Show(this,"The client secret could not be encrypted, so the settings were not saved.");
				return false;
			}
			DataValid.SetInvalid(InvalidType.Programs);
			_qboSettings=QboSettings.Load();
			FillConnectionFields();
			return true;
		}

		private void butConnect_Click(object sender,EventArgs e) {
			if(!ValidateConnectionFields(isForConnect:true) || !SaveConnectionFields()) {
				return;
			}
			QboAuthRequest qboAuthRequest;
			try {
				qboAuthRequest=QuickBooksOnline.BeginAuthorization();
			}
			catch(ODException ex) {
				MessageBox.Show(this,ex.Message);
				return;
			}
			LoopbackListener loopbackListener=qboAuthRequest.CreateLoopbackListener();
			try {
				loopbackListener?.Start();
			}
			catch(SocketException) {
				loopbackListener.Dispose();
				MessageBox.Show(this,Lan.g(this,"Port")+" "+qboAuthRequest.LoopbackPort+" "
					+Lan.g(this,"is in use on this computer. Close the program using it, or register a different port with Intuit and enter it here."));
				return;
			}
			try {
				SystemBrowser.Open(qboAuthRequest.Url);
			}
			catch(Exception ex) {
				loopbackListener?.Dispose();
				MessageBox.Show(this,Lan.g(this,"The browser could not be opened.")+" "+ex.Message);
				return;
			}
			QboAuthorizationResult qboAuthorizationResult;
			using(FormQuickBooksOnlineAuthorization formQuickBooksOnlineAuthorization=new FormQuickBooksOnlineAuthorization(loopbackListener)) {
				formQuickBooksOnlineAuthorization.ShowDialog();
				if(formQuickBooksOnlineAuthorization.DialogResult!=DialogResult.OK) {
					return;
				}
				qboAuthorizationResult=formQuickBooksOnlineAuthorization.AuthorizationResult;
			}
			QboPendingConnection qboPendingConnection=null;
			UI.ProgressWin progressWin=new UI.ProgressWin();
			progressWin.StartingMessage=Lan.g(this,"Connecting to QuickBooks Online...");
			progressWin.ActionMain=() => {
				qboPendingConnection=QuickBooksOnline.ExchangeAuthorization(qboAuthRequest,qboAuthorizationResult);
			};
			try {
				progressWin.ShowDialog();
			}
			catch(ODException ex) {
				MessageBox.Show(this,ex.Message,Lan.g(this,"QuickBooks Online"));
				return;
			}
			catch(Exception ex) {
				FriendlyException.Show(Lan.g(this,"Could not connect to QuickBooks Online."),ex);
				return;
			}
			if(progressWin.IsCancelled || qboPendingConnection==null) {
				return;
			}
			if(qboPendingConnection.IsDifferentCompany
				&& !MsgBox.Show(this,MsgBoxButtons.YesNo,"This connects a different QuickBooks Online company. The saved deposit accounts, income accounts "
				+"and classes belong to the previous company and will be cleared. Continue?"))
			{
				QuickBooksOnline.DiscardPendingConnection(qboPendingConnection);
				return;
			}
			if(!QuickBooksOnline.SaveConnection(qboPendingConnection,qboPendingConnection.IsDifferentCompany)) {
				MsgBox.Show(this,"The connection could not be saved because encryption failed.");
				return;
			}
			DataValid.SetInvalid(InvalidType.Programs);
			SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,"QuickBooks Online connected");
			_qboSettings=QboSettings.Load();
			if(qboPendingConnection.IsDifferentCompany) {
				_listDepositAccountsInOD.Clear();
				_listIncomeAccountsInOD.Clear();
				_listClassRefsInOD.Clear();
			}
			FillConnectionFields();
			EnableOrDisableControls();
			GetAccountsAndClassRefsFromQuickBooksOnline();
		}

		private void butDisconnect_Click(object sender,EventArgs e) {
			string message=Lan.g(this,"Disconnect from QuickBooks Online? The connection is revoked with Intuit and removed from this database.");
			if(PrefC.GetEnum<AccountingSoftware>(PrefName.AccountingSoftware)==AccountingSoftware.QuickBooksOnline) {
				message+="\r\n\r\n"+Lan.g(this,"QuickBooks Online is the Deposit Software. Send QBO stops working until you connect again.");
			}
			if(MessageBox.Show(this,message,Lan.g(this,"QuickBooks Online"),MessageBoxButtons.YesNo)!=DialogResult.Yes) {
				return;
			}
			RunDisconnect();
			_qboSettings=QboSettings.Load();
			FillConnectionFields();
			EnableOrDisableControls();
			_hasQboError=true;
			FillAllListBoxes();
		}

		///<summary>Revokes and clears the saved connection, behind a progress window.</summary>
		private void RunDisconnect() {
			UI.ProgressWin progressWin=new UI.ProgressWin();
			progressWin.StartingMessage=Lan.g(this,"Disconnecting...");
			progressWin.ActionMain=() => QuickBooksOnline.Disconnect();
			try {
				progressWin.ShowDialog();
			}
			catch(Exception ex) {
				FriendlyException.Show(Lan.g(this,"Could not disconnect."),ex);
			}
			DataValid.SetInvalid(InvalidType.Programs);
			SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,"QuickBooks Online disconnected");
		}

		private void butAddDepositAccount_Click(object sender,EventArgs e) {
			QuickBooksOnlineAccount account=listBoxDepositAccountsAvailable.GetSelected<QuickBooksOnlineAccount>();
			if(account==null) {
				return;//No available deposit account selected.
			}
			if(!QboEntityList.IsStorableName(account.Name)) {
				MsgBox.Show(this,"Accounts from QuickBooks Online with names that contain commas or vertical bars cannot be used in Open Dental.");
				return;
			}
			_listDepositAccountsInOD.Add(account);
			FillListBoxesWithAccounts(_listDepositAccountsInOD,_listDepositAccountsAvailable,listBoxDepositAccountsInOD,listBoxDepositAccountsAvailable);
		}

		private void butRemoveDepositAccount_Click(object sender,EventArgs e) {
			QuickBooksOnlineAccount account=listBoxDepositAccountsInOD.GetSelected<QuickBooksOnlineAccount>();
			if(account==null) {
				return;//No deposit account from OD selected.
			}
			_listDepositAccountsInOD.Remove(account);
			FillListBoxesWithAccounts(_listDepositAccountsInOD,_listDepositAccountsAvailable,listBoxDepositAccountsInOD,listBoxDepositAccountsAvailable);
		}

		private void butAddIncomeAccount_Click(object sender,EventArgs e) {
			QuickBooksOnlineAccount account=listBoxIncomeAccountsAvailable.GetSelected<QuickBooksOnlineAccount>();
			if(account==null) {
				return;//No available income account selected.
			}
			if(!QboEntityList.IsStorableName(account.Name)) {
				MsgBox.Show(this,"Accounts from QuickBooks Online with names that contain commas or vertical bars cannot be used in Open Dental.");
				return;
			}
			_listIncomeAccountsInOD.Add(account);
			FillListBoxesWithAccounts(_listIncomeAccountsInOD,_listIncomeAccountsAvailable,listBoxIncomeAccountsInOD,listBoxIncomeAccountsAvailable);
		}

		private void butRemoveIncomeAccount_Click(object sender,EventArgs e) {
			QuickBooksOnlineAccount account=listBoxIncomeAccountsInOD.GetSelected<QuickBooksOnlineAccount>();
			if(account==null) {
				return;//No income account from OD selected.
			}
			_listIncomeAccountsInOD.Remove(account);
			FillListBoxesWithAccounts(_listIncomeAccountsInOD,_listIncomeAccountsAvailable,listBoxIncomeAccountsInOD,listBoxIncomeAccountsAvailable);
		}

		private void butAddClass_Click(object sender,EventArgs e) {
			QuickBooksOnlineClassRef classRef=listBoxClassRefsAvailable.GetSelected<QuickBooksOnlineClassRef>();
			if(classRef==null) {
				return;//No available class ref selected.
			}
			if(!QboEntityList.IsStorableName(classRef.Name)) {
				MsgBox.Show(this,"Classes from QuickBooks Online with names that contain commas or vertical bars cannot be used in Open Dental.");
				return;
			}
			_listClassRefsInOD.Add(classRef);
			FillListBoxesWithClassRefs();
		}

		private void butRemoveClass_Click(object sender,EventArgs e) {
			QuickBooksOnlineClassRef classRef=listBoxClassRefsInOD.GetSelected<QuickBooksOnlineClassRef>();
			if(classRef==null) {
				return;//No classRef from OD selected.
			}
			_listClassRefsInOD.Remove(classRef);
			FillListBoxesWithClassRefs();
		}

		private void butSave_Click(object sender,EventArgs e) {
			if(checkEnabled.Checked) {
				if(!ValidateConnectionFields(isForConnect:false) || !SaveConnectionFields()) {
					return;
				}
			}
			_progCur.Enabled=checkEnabled.Checked;
			Programs.Update(_progCur,_progOld);
			QboSettings.SaveEntityLists(QuickBooksOnlineAccount.ListAccountsToString(_listDepositAccountsInOD),
				QuickBooksOnlineAccount.ListAccountsToString(_listIncomeAccountsInOD),QuickBooksOnlineClassRef.ListClassRefsToString(_listClassRefsInOD));
			DataValid.SetInvalid(InvalidType.Programs);
			if(!_progOld.Enabled && _progCur.Enabled
				&& MsgBox.Show(MsgBoxButtons.YesNo,"You have enabled QuickBooks Online. Would you like to set it as your Deposit Software?"))
			{
				Prefs.UpdateInt(PrefName.AccountingSoftware,(int)AccountingSoftware.QuickBooksOnline);
				DataValid.SetInvalid(InvalidType.Prefs);
			}
			DialogResult=DialogResult.OK;
		}

	}

	///<summary>Registers this window with Setup > Program Links (vendor-direct registry): Program Links inserts the missing property rows before it
	///refreshes, and opens this window on double-click.</summary>
	public class QuickBooksOnlineProgramLinkSetup:IProgramLinkSetup {
		public ProgramName ProgName {
			get {
				return ProgramName.QuickBooksOnline;
			}
		}

		public bool EnsureProgram() {
			if(Programs.GetCur(ProgramName.QuickBooksOnline)==null) {
				return false;//Shipped by the database; never inserted here.
			}
			return QboSettings.EnsurePropertyRows();
		}

		public DialogResult OpenSetup(IWin32Window owner) {
			using(FormQuickBooksOnlineSetup formQuickBooksOnlineSetup=new FormQuickBooksOnlineSetup(true)) {
				return formQuickBooksOnlineSetup.ShowDialog(owner);
			}
		}
	}
}
