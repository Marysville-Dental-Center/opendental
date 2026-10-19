using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using CodeBase;
using OpenDentBusiness;
using OpenDentBusiness.PostcardMania;

namespace OpenDental {
	///<summary>Setup for the PostcardMania program link: the office's own API key and secret per clinic (or the default), the sending defaults, and
	///whether a business associate agreement with the vendor is on file. Describes what the integration does and nothing more (ground rule 7).
	///Opened from Setup > Program Links and from the Postcards window.</summary>
	public partial class FormAdvertisingPostcardsSetup:FormODBase {
		private Program _program;
		///<summary>Pending edits per clinic (0 is the default account), saved together on Save.</summary>
		private Dictionary<long,AccountEdit> _dictAccountEdits=new Dictionary<long,AccountEdit>();
		///<summary>The clinic whose account the fields show.</summary>
		private long _clinicNumShowing;
		///<summary>Accounts (by token cache key) that passed Test connection in this window.</summary>
		private HashSet<string> _hashSetTestedAccounts=new HashSet<string>();

		public FormAdvertisingPostcardsSetup() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormAdvertisingPostcardsSetup_Load(object sender,EventArgs e) {
			if(PostcardManiaProgram.EnsureProgram()) {
				DataValid.SetInvalid(InvalidType.Programs);
			}
			_program=Programs.GetCur(ProgramName.PostcardMania);
			if(_program==null) {
				MsgBox.Show(this,"The PostcardMania program link could not be created.");
				DialogResult=DialogResult.Cancel;
				Close();
				return;
			}
			checkEnabled.Checked=_program.Enabled;
			comboEnvironment.Items.Add(Lan.g(this,"Sandbox"),PostcardManiaProgram.EnvironmentSandbox);
			comboEnvironment.Items.Add(Lan.g(this,"Production"),PostcardManiaProgram.EnvironmentProduction);
			comboMailClass.Items.Add(Lan.g(this,"First Class"),PcmMailClass.FirstClass);
			comboMailClass.Items.Add(Lan.g(this,"Standard"),PcmMailClass.Standard);
			comboMailClass.SelectedIndex=(PostcardManiaProgram.GetDefaultMailClass()==PcmMailClass.Standard) ? 1 : 0;
			checkUsePreferredName.Checked=PostcardManiaProgram.GetUsePreferredName();
			checkSendPatientReference.Checked=PostcardManiaProgram.GetSendPatientReference();
			checkBaaOnFile.Checked=PostcardManiaProgram.IsBaaOnFile();
			labelBaaConfirmed.Text=checkBaaOnFile.Checked ? GetBaaConfirmedText() : "";
			FillClinics();
			LoadAccount(_clinicNumShowing);
		}

		private void FillClinics() {
			comboClinic.Items.Clear();
			if(!PrefC.HasClinicsEnabled) {
				labelClinic.Visible=false;
				comboClinic.Visible=false;
				checkUseDefault.Visible=false;
				_clinicNumShowing=0;
				return;
			}
			if(!Security.CurUser.ClinicIsRestricted) {
				comboClinic.Items.Add(Lan.g(this,"Default (all clinics)"),new Clinic { ClinicNum=0 });
			}
			List<Clinic> listClinics=Clinics.GetForUserod(Security.CurUser);
			for(int i=0;i<listClinics.Count;i++) {
				comboClinic.Items.Add(listClinics[i].Abbr,listClinics[i]);
			}
			if(comboClinic.Items.Count==0) {
				_clinicNumShowing=0;
				return;
			}
			comboClinic.SelectedIndex=0;
			_clinicNumShowing=comboClinic.GetSelected<Clinic>().ClinicNum;
		}

		///<summary>The pending edit for a clinic, read from the database cache the first time.</summary>
		private AccountEdit GetAccountEdit(long clinicNum) {
			AccountEdit accountEdit;
			if(_dictAccountEdits.TryGetValue(clinicNum,out accountEdit)) {
				return accountEdit;
			}
			accountEdit=new AccountEdit();
			accountEdit.ClinicNum=clinicNum;
			accountEdit.IsUseDefault=!PostcardManiaProgram.HasOwnAccount(clinicNum);
			PostcardManiaAccount account=PostcardManiaProgram.GetAccount(clinicNum);
			if(accountEdit.IsUseDefault) {
				accountEdit.ApiKey="";
				accountEdit.HasStoredSecret=false;
				accountEdit.Environment=PostcardManiaProgram.EnvironmentSandbox;
				accountEdit.ChildRefNbr="";
			}
			else {
				accountEdit.ApiKey=account.ApiKey;
				accountEdit.HasStoredSecret=account.ApiSecret!="";
				accountEdit.IsStoredSecretUnreadable=account.IsSecretUnreadable;
				accountEdit.Environment=account.Environment;
				accountEdit.ChildRefNbr=account.ChildRefNbr;
			}
			accountEdit.MarkOriginal();
			_dictAccountEdits[clinicNum]=accountEdit;
			return accountEdit;
		}

		private void LoadAccount(long clinicNum) {
			AccountEdit accountEdit=GetAccountEdit(clinicNum);
			bool isClinic=(clinicNum!=0);
			checkUseDefault.Visible=isClinic;
			checkUseDefault.Checked=isClinic && accountEdit.IsUseDefault;
			textApiKey.Text=accountEdit.ApiKey;
			textApiSecret.Text=accountEdit.SecretNew??"";
			comboEnvironment.SelectedIndex=(accountEdit.Environment==PostcardManiaProgram.EnvironmentProduction) ? 1 : 0;
			textChildRef.Text=accountEdit.ChildRefNbr;
			labelTestResult.Text="";
			SetAccountFieldsEnabled();
		}

		///<summary>Copies the fields into the pending edit for the clinic showing.</summary>
		private void StoreAccount() {
			AccountEdit accountEdit=GetAccountEdit(_clinicNumShowing);
			accountEdit.IsUseDefault=(_clinicNumShowing!=0) && checkUseDefault.Checked;
			if(accountEdit.IsUseDefault) {
				return;
			}
			accountEdit.ApiKey=textApiKey.Text.Trim();
			accountEdit.SecretNew=(textApiSecret.Text.Trim()=="") ? null : textApiSecret.Text.Trim();
			accountEdit.Environment=comboEnvironment.GetSelected<string>()??PostcardManiaProgram.EnvironmentSandbox;
			accountEdit.ChildRefNbr=textChildRef.Text.Trim();
		}

		private void SetAccountFieldsEnabled() {
			bool isEditable=!(checkUseDefault.Visible && checkUseDefault.Checked);
			textApiKey.Enabled=isEditable;
			textApiSecret.Enabled=isEditable;
			comboEnvironment.Enabled=isEditable;
			textChildRef.Enabled=isEditable;
			AccountEdit accountEdit=GetAccountEdit(_clinicNumShowing);
			labelSecretHint.Visible=isEditable && (accountEdit.HasStoredSecret || accountEdit.IsStoredSecretUnreadable);
			labelSecretHint.Text=accountEdit.IsStoredSecretUnreadable
				? Lan.g(this,"The saved secret can't be read. Enter it again.")
				: Lan.g(this,"A secret is saved. Leave this blank to keep it.");
			bool isSandbox=isEditable ? comboEnvironment.SelectedIndex!=1 : GetAccountEdit(0).Environment!=PostcardManiaProgram.EnvironmentProduction;
			labelSandbox.Visible=isSandbox;
		}

		private void comboClinic_SelectionChangeCommitted(object sender,EventArgs e) {
			StoreAccount();
			_clinicNumShowing=comboClinic.GetSelected<Clinic>()?.ClinicNum??0;
			LoadAccount(_clinicNumShowing);
		}

		private void checkUseDefault_Click(object sender,EventArgs e) {
			SetAccountFieldsEnabled();
		}

		private void comboEnvironment_SelectionChangeCommitted(object sender,EventArgs e) {
			SetAccountFieldsEnabled();
		}

		///<summary>The account the pending edits describe for a clinic (its own, or the default's), with the secret decrypted or newly typed.</summary>
		private PostcardManiaAccount BuildPendingAccount(long clinicNum) {
			AccountEdit accountEdit=GetAccountEdit(clinicNum);
			if(clinicNum!=0 && accountEdit.IsUseDefault) {
				PostcardManiaAccount accountDefault=BuildPendingAccount(0);
				accountDefault.ClinicNum=clinicNum;
				return accountDefault;
			}
			PostcardManiaAccount account=new PostcardManiaAccount();
			account.ClinicNum=clinicNum;
			account.ClinicNumSource=clinicNum;
			account.ApiKey=accountEdit.ApiKey;
			if(accountEdit.SecretNew!=null) {
				account.ApiSecret=accountEdit.SecretNew;
			}
			else if(accountEdit.HasStoredSecret) {
				account.ApiSecret=PostcardManiaProgram.GetAccount(clinicNum).ApiSecret;
			}
			account.IsSecretUnreadable=accountEdit.SecretNew==null && accountEdit.IsStoredSecretUnreadable;
			account.Environment=accountEdit.Environment;
			account.ChildRefNbr=accountEdit.ChildRefNbr;
			return account;
		}

		private void butTestConnection_Click(object sender,EventArgs e) {
			StoreAccount();
			PostcardManiaAccount account=BuildPendingAccount(_clinicNumShowing);
			if(!account.IsConfigured) {
				MsgBox.Show(this,"Enter the API key and secret first.");
				return;
			}
			PcmConnectionResult pcmConnectionResult=null;
			labelTestResult.Text="";
			try {
				UI.ProgressWin progressWin=new UI.ProgressWin();
				progressWin.ActionMain=() => {
					pcmConnectionResult=AdvertisingPostcards.TestConnection(account);
				};
				progressWin.StartingMessage=Lan.g(this,"Testing the connection")+"...";
				progressWin.ShowDialog();
				if(progressWin.IsCancelled) {
					return;
				}
			}
			catch(PostcardManiaException ex) {
				labelTestResult.Text=Lan.g(this,"Failed.");
				MsgBox.Show(this,ex.Message);
				return;
			}
			catch(Exception ex) {
				labelTestResult.Text=Lan.g(this,"Failed.");
				FriendlyException.Show(Lan.g(this,"The connection test failed."),ex);
				return;
			}
			_hashSetTestedAccounts.Add(account.GetCacheKey());
			string text=Lan.g(this,"Connected")+" ("+Lan.g(this,pcmConnectionResult.Environment)+").";
			if(pcmConnectionResult.MoneyOnAccount.HasValue) {
				text+=" "+Lan.g(this,"Money on account")+": "+pcmConnectionResult.MoneyOnAccount.Value.ToString("c");
			}
			labelTestResult.Text=text;
		}

		private string GetBaaConfirmedText() {
			string confirmed=PostcardManiaProgram.GetSetting(PostcardManiaProgram.PropertyDescs.BaaConfirmed);
			if(confirmed=="") {
				return "";
			}
			return Lan.g(this,"Recorded by")+" "+confirmed;
		}

		private void butSave_Click(object sender,EventArgs e) {
			StoreAccount();
			List<AccountEdit> listAccountEdits=_dictAccountEdits.Values.ToList();
			for(int i=0;i<listAccountEdits.Count;i++) {
				AccountEdit accountEdit=listAccountEdits[i];
				if(accountEdit.ClinicNum!=0 && accountEdit.IsUseDefault) {
					continue;
				}
				if(accountEdit.ChildRefNbr.Length>50) {
					MsgBox.Show(this,"The Child Reference can be at most 50 characters.");
					return;
				}
				bool hasSecret=accountEdit.SecretNew!=null || (accountEdit.HasStoredSecret && !accountEdit.IsStoredSecretUnreadable);
				if(accountEdit.ApiKey!="" && !hasSecret) {
					MsgBox.Show(this,"Enter the API secret that goes with the API key.");
					return;
				}
				if(accountEdit.ClinicNum!=0 && accountEdit.ApiKey=="") {
					//A clinic without its own key uses the default.
					accountEdit.IsUseDefault=true;
				}
			}
			if(listAccountEdits.Any(x => x.SecretNew!=null) && !Security.IsAuthorized(EnumPermType.ManageHighSecurityProgProperties)) {
				return;
			}
			if(checkEnabled.Checked && !_program.Enabled) {
				//Rule 6: off until the office has its own credentials and a passing Test connection.
				List<long> listClinicNumsConfigured=GetClinicNumsToCheck().Where(x => BuildPendingAccount(x).IsConfigured).ToList();
				if(listClinicNumsConfigured.Count==0) {
					MsgBox.Show(this,"Enter an API key and secret before turning this on.");
					return;
				}
				if(!listClinicNumsConfigured.Any(x => _hashSetTestedAccounts.Contains(BuildPendingAccount(x).GetCacheKey()))) {
					MsgBox.Show(this,"Click Test connection and get a successful result before turning this on.");
					return;
				}
			}
			bool wasBaaOnFile=PostcardManiaProgram.IsBaaOnFile();
			for(int i=0;i<listAccountEdits.Count;i++) {
				AccountEdit accountEdit=listAccountEdits[i];
				if(!accountEdit.IsChanged()) {
					continue;
				}
				if(accountEdit.ClinicNum!=0 && accountEdit.IsUseDefault) {
					PostcardManiaProgram.ClearClinicAccount(accountEdit.ClinicNum);
					continue;
				}
				if(!PostcardManiaProgram.SaveAccount(accountEdit.ClinicNum,accountEdit.ApiKey,accountEdit.SecretNew,accountEdit.Environment,accountEdit.ChildRefNbr)) {
					MsgBox.Show(this,"The API secret could not be encrypted, so nothing was saved for this account.");
					return;
				}
				if(accountEdit.SecretNew!=null) {
					SecurityLogs.MakeLogEntry(EnumPermType.ManageHighSecurityProgProperties,0,
						"PostcardMania API secret changed for "+(accountEdit.ClinicNum==0 ? "the default account" : ("clinic "+Clinics.GetAbbr(accountEdit.ClinicNum)))+".",
						_program.ProgramNum,DateTime.Now);
				}
			}
			PostcardManiaProgram.SaveSetting(PostcardManiaProgram.PropertyDescs.DefaultMailClass,comboMailClass.GetSelected<string>()??PcmMailClass.FirstClass);
			PostcardManiaProgram.SaveSetting(PostcardManiaProgram.PropertyDescs.UsePreferredName,checkUsePreferredName.Checked ? "1" : "0");
			PostcardManiaProgram.SaveSetting(PostcardManiaProgram.PropertyDescs.SendPatientReference,checkSendPatientReference.Checked ? "1" : "0");
			if(checkBaaOnFile.Checked!=wasBaaOnFile) {
				PostcardManiaProgram.SaveSetting(PostcardManiaProgram.PropertyDescs.BaaOnFile,checkBaaOnFile.Checked ? "1" : "0");
				string confirmed=checkBaaOnFile.Checked ? (Security.CurUser.UserName+", "+DateTime.Now.ToString()) : "";
				PostcardManiaProgram.SaveSetting(PostcardManiaProgram.PropertyDescs.BaaConfirmed,confirmed);
				SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,"PostcardMania: business associate agreement "
					+(checkBaaOnFile.Checked ? "recorded as on file." : "no longer recorded as on file."));
			}
			if(checkEnabled.Checked!=_program.Enabled) {
				Program programOld=_program.Copy();
				_program.Enabled=checkEnabled.Checked;
				Programs.Update(_program,programOld);
				SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,"PostcardMania program link "+(_program.Enabled ? "enabled." : "disabled."));
			}
			DataValid.SetInvalid(InvalidType.Programs);
			DialogResult=DialogResult.OK;
			Close();
		}

		///<summary>The default (0) plus every clinic this user can see.</summary>
		private List<long> GetClinicNumsToCheck() {
			List<long> listClinicNums=new List<long> { 0 };
			if(PrefC.HasClinicsEnabled) {
				listClinicNums.AddRange(Clinics.GetForUserod(Security.CurUser).Select(x => x.ClinicNum));
			}
			return listClinicNums;
		}

		///<summary>One clinic's (or the default's) account as edited in this window.</summary>
		private class AccountEdit {
			public long ClinicNum;
			public bool IsUseDefault;
			public string ApiKey="";
			///<summary>A newly typed secret, or null to keep the stored one.</summary>
			public string SecretNew;
			public bool HasStoredSecret;
			public bool IsStoredSecretUnreadable;
			public string Environment=PostcardManiaProgram.EnvironmentSandbox;
			public string ChildRefNbr="";
			private string _original;

			public bool IsChanged() {
				return SecretNew!=null || Describe()!=_original;
			}

			public void MarkOriginal() {
				_original=Describe();
			}

			private string Describe() {
				return IsUseDefault+"|"+ApiKey+"|"+Environment+"|"+ChildRefNbr;
			}
		}
	}
}
