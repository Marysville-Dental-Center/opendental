// Global Payments V2 (Open Dental 25.4, v25_4-payments-globalpayments-v2): Setup > Program Links > Global Payments V2. Also registers the Global
// Payments V1 (EdgeExpress) program link with Program Links, for the 25.4 rename of its description.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using CodeBase;
using CodeBase.VendorDirect;
using OpenPmsDental.UI;
using OpenPmsDental.VendorDirect;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.VendorDirect;
using OpenPmsDentalBusiness.VendorDirect.GlobalPayments;

namespace OpenPmsDental {
	///<summary>Setup for Global Payments V2: enabled (all clinics), the sandbox switch (all clinics), and per clinic the office's Global Payments API app
	///(App ID, App Key), account name, payment type, receipt printing, "prevent saving new cards" and "recurring charges force duplicates", plus the
	///clinic's terminals. The App Key is stored with CodeBase Secrets (osd1$, masked, high security), changed only when re-entered and never shown
	///again. Only one of X-Charge, Global Payments V1 and Global Payments V2 can be enabled. Vendor-neutral: no links, logos or sign-up.
	///The public setup page's "Enable payments for Payment Portal and eClipboard" box is not here: those services are excluded.</summary>
	public partial class FormGlobalPaymentsV2Setup:FormODBase {
		private Program _program;
		private List<Def> _listDefsPaymentType;
		///<summary>The settings of every clinic shown so far in this window, as edited. Written on Save.</summary>
		private Dictionary<long,ClinicSettings> _dictClinicSettings=new Dictionary<long,ClinicSettings>();
		private long _clinicNumCur;
		private bool _canEditSecret;
		private List<PayTerminal> _listPayTerminals=new List<PayTerminal>();

		///<summary>One clinic's settings while the window is open.</summary>
		private class ClinicSettings {
			public string AppId="";
			///<summary>What was typed in the App Key box: "" keeps the saved key.</summary>
			public string AppKeyEntered="";
			///<summary>The clinic's own saved key, as read when the clinic was first shown.</summary>
			public SecretReadResult AppKeySaved=new SecretReadResult(SecretReadStatus.Blank,"");
			public string AccountName="";
			public long PaymentTypeDefNum;
			public bool PrintReceipt;
			public bool PreventSavingNewCC;
			public bool ForceDuplicates;
		}

		public FormGlobalPaymentsV2Setup() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormGlobalPaymentsV2Setup_Load(object sender,EventArgs e) {
			if(GlobalPaymentsV2.EnsureProgram()) {
				Programs.RefreshCache();
				ProgramProperties.RefreshCache();
			}
			_program=Programs.GetCur(ProgramName.MeetInTheCloud);
			if(_program==null) {
				MsgBox.Show(this,"The Global Payments V2 program link could not be created.");
				DialogResult=DialogResult.Cancel;
				Close();
				return;
			}
			checkEnabled.Checked=_program.Enabled;
			checkUseSandbox.Checked=PIn.Bool(ProgramProperties.GetValOrDefault(_program.ProgramNum,GlobalPaymentsV2.PropUseSandbox,0));
			if(PrefC.HasClinicsEnabled) {
				groupPaySettings.Text=Lan.g(this,"Clinic Payment Settings");
				if(Security.CurUser.ClinicIsRestricted) {
					//A user restricted to clinics can't turn the link off for every clinic, nor switch every clinic to the sandbox.
					checkEnabled.Enabled=!_program.Enabled;
					checkUseSandbox.Enabled=false;
				}
				comboClinic.ClinicNumSelected=Clinics.ClinicNum;
				_clinicNumCur=comboClinic.ClinicNumSelected;
			}
			else {
				labelClinic.Visible=false;
				labelClinicHint.Visible=false;
				_clinicNumCur=0;
			}
			_listDefsPaymentType=Defs.GetDefsForCategory(DefCat.PaymentTypes,true);
			comboPaymentType.Items.Clear();
			comboPaymentType.Items.AddDefs(_listDefsPaymentType);
			_canEditSecret=ProgramProperties.CanEditProperties(ProgramProperties.GetForProgram(_program.ProgramNum));
			textAppKey.ReadOnly=!_canEditSecret;
			labelTestResult.Text="";
			FillClinic();
		}

		///<summary>The settings of clinicNum: the edited copy if the clinic was shown before, else read from the cache. Credentials are the clinic's own
		///rows; the other settings show what applies (the clinic's row, else Headquarters').</summary>
		private ClinicSettings GetClinicSettings(long clinicNum) {
			ClinicSettings clinicSettings;
			if(_dictClinicSettings.TryGetValue(clinicNum,out clinicSettings)) {
				return clinicSettings;
			}
			clinicSettings=new ClinicSettings();
			long programNum=_program.ProgramNum;
			clinicSettings.AppId=GetOwnValue(GlobalPaymentsV2.PropAppId,clinicNum);
			clinicSettings.AppKeySaved=SecretValue.Read(GetOwnValue(GlobalPaymentsV2.PropAppKey,clinicNum),LegacyPolicy.AcceptPlaintext);
			clinicSettings.AccountName=GetOwnValue(GlobalPaymentsV2.PropAccountName,clinicNum);
			clinicSettings.PaymentTypeDefNum=PIn.Long(ProgramProperties.GetValOrDefault(programNum,GlobalPaymentsV2.PropPaymentType,clinicNum),false);
			clinicSettings.PrintReceipt=PIn.Bool(ProgramProperties.GetValOrDefault(programNum,GlobalPaymentsV2.PropPrintReceipt,clinicNum));
			clinicSettings.PreventSavingNewCC=PIn.Bool(ProgramProperties.GetValOrDefault(programNum,GlobalPaymentsV2.PropPreventSavingNewCC,clinicNum));
			clinicSettings.ForceDuplicates=PIn.Bool(ProgramProperties.GetValOrDefault(programNum,GlobalPaymentsV2.PropForceRecurringCharge,clinicNum));
			_dictClinicSettings[clinicNum]=clinicSettings;
			return clinicSettings;
		}

		///<summary>The clinic's own row value from the cache (no fallback), or "".</summary>
		private string GetOwnValue(string desc,long clinicNum) {
			ProgramProperty programProperty=ProgramProperties.GetWhere(x => x.ProgramNum==_program.ProgramNum && x.PropertyDesc==desc && x.ClinicNum==clinicNum)
				.OrderBy(x => x.ProgramPropertyNum).FirstOrDefault();
			return programProperty?.PropertyValue??"";
		}

		private bool HasOwnRow(string desc,long clinicNum) {
			return ProgramProperties.GetWhere(x => x.ProgramNum==_program.ProgramNum && x.PropertyDesc==desc && x.ClinicNum==clinicNum).Count>0;
		}

		///<summary>Shows the settings and terminals of _clinicNumCur.</summary>
		private void FillClinic() {
			ClinicSettings clinicSettings=GetClinicSettings(_clinicNumCur);
			textAppId.Text=clinicSettings.AppId;
			textAppKey.Text=clinicSettings.AppKeyEntered;
			textAccountName.Text=clinicSettings.AccountName;
			comboPaymentType.SelectedIndex=_listDefsPaymentType.FindIndex(x => x.DefNum==clinicSettings.PaymentTypeDefNum);
			checkPrintReceipt.Checked=clinicSettings.PrintReceipt;
			checkPreventSavingNewCC.Checked=clinicSettings.PreventSavingNewCC;
			checkForceDuplicates.Checked=clinicSettings.ForceDuplicates;
			FillKeyStatus(clinicSettings);
			labelTestResult.Text="";
			FillGridTerminals();
		}

		private void FillKeyStatus(ClinicSettings clinicSettings) {
			if(!_canEditSecret) {
				labelKeyStatus.Text=Lan.g(this,"You need the permission to manage high security program properties to change the App Key.");
			}
			else if(clinicSettings.AppKeySaved.HasValue) {
				labelKeyStatus.Text=Lan.g(this,"A key is saved. Leave blank to keep it.");
			}
			else if(clinicSettings.AppKeySaved.NeedsReentry) {
				labelKeyStatus.Text=Lan.g(this,"The saved key can't be read. Enter it again.");
			}
			else {
				labelKeyStatus.Text=Lan.g(this,"Not set.");
			}
		}

		///<summary>Keeps what is on screen for _clinicNumCur.</summary>
		private void SaveFieldsToClinic() {
			ClinicSettings clinicSettings=GetClinicSettings(_clinicNumCur);
			clinicSettings.AppId=textAppId.Text.Trim();
			clinicSettings.AppKeyEntered=textAppKey.Text.Trim();
			clinicSettings.AccountName=textAccountName.Text.Trim();
			clinicSettings.PaymentTypeDefNum=(comboPaymentType.SelectedIndex>=0) ? _listDefsPaymentType[comboPaymentType.SelectedIndex].DefNum : 0;
			clinicSettings.PrintReceipt=checkPrintReceipt.Checked;
			clinicSettings.PreventSavingNewCC=checkPreventSavingNewCC.Checked;
			clinicSettings.ForceDuplicates=checkForceDuplicates.Checked;
		}

		private void comboClinic_SelectionChangeCommitted(object sender,EventArgs e) {
			if(comboClinic.ClinicNumSelected==_clinicNumCur) {
				return;
			}
			SaveFieldsToClinic();
			_clinicNumCur=comboClinic.ClinicNumSelected;
			FillClinic();
		}

		private void FillGridTerminals() {
			_listPayTerminals=PayTerminals.GetForIntegration(_clinicNumCur,GlobalPaymentsV2.CCIntegration);
			gridTerminals.BeginUpdate();
			gridTerminals.Columns.Clear();
			gridTerminals.Columns.Add(new GridColumn(Lan.g(gridTerminals.TranslationName,"Terminal Name"),220));
			gridTerminals.Columns.Add(new GridColumn(Lan.g(gridTerminals.TranslationName,"Terminal ID"),220));
			gridTerminals.ListGridRows.Clear();
			for(int i=0;i<_listPayTerminals.Count;i++) {
				GridRow row=new GridRow(_listPayTerminals[i].Name,_listPayTerminals[i].TerminalID);
				row.Tag=_listPayTerminals[i];
				gridTerminals.ListGridRows.Add(row);
			}
			gridTerminals.EndUpdate();
		}

		private void gridTerminals_CellDoubleClick(object sender,ODGridClickEventArgs e) {
			PayTerminal payTerminal=(PayTerminal)gridTerminals.ListGridRows[e.Row].Tag;
			using FormPayTerminalEdit formPayTerminalEdit=new FormPayTerminalEdit(payTerminal);
			formPayTerminalEdit.ShowDialog();
			FillGridTerminals();
		}

		private void butAddTerminal_Click(object sender,EventArgs e) {
			using FormPayTerminalEdit formPayTerminalEdit=new FormPayTerminalEdit(null,GlobalPaymentsV2.CCIntegration,_clinicNumCur);
			formPayTerminalEdit.ShowDialog();
			FillGridTerminals();
		}

		private void butDeleteTerminal_Click(object sender,EventArgs e) {
			int index=gridTerminals.GetSelectedIndex();
			if(index<0) {
				MsgBox.Show(this,"Select a terminal first.");
				return;
			}
			PayTerminal payTerminal=(PayTerminal)gridTerminals.ListGridRows[index].Tag;
			if(!MsgBox.Show(this,MsgBoxButtons.YesNo,"Delete this terminal? It will no longer be offered when taking payments.")) {
				return;
			}
			PayTerminals.Delete(payTerminal.PayTerminalNum);
			FillGridTerminals();
		}

		///<summary>The connection settings as currently entered for _clinicNumCur: the typed key, else the saved one; a clinic without its own App ID uses
		///Headquarters'.</summary>
		private GlobalPaymentsV2Config GetConfigFromForm() {
			SaveFieldsToClinic();
			ClinicSettings clinicSettings=GetClinicSettings(_clinicNumCur);
			if(_clinicNumCur!=0 && clinicSettings.AppId=="") {
				clinicSettings=GetClinicSettings(0);
			}
			GlobalPaymentsV2Config config=new GlobalPaymentsV2Config();
			config.ClinicNum=_clinicNumCur;
			config.AppId=clinicSettings.AppId;
			config.AppKey=(clinicSettings.AppKeyEntered!="") ? clinicSettings.AppKeyEntered : clinicSettings.AppKeySaved.PlainText;
			config.AccountName=clinicSettings.AccountName;
			config.UseSandbox=checkUseSandbox.Checked;
			return config;
		}

		private void butTest_Click(object sender,EventArgs e) {
			GlobalPaymentsV2Config config=GetConfigFromForm();
			if(!config.IsComplete) {
				MsgBox.Show(this,"Enter the App ID and App Key first.");
				return;
			}
			GpV2Client gpV2Client=GlobalPaymentsV2.GetClient();
			gpV2Client.Reset(config);
			Exception exception=null;
			ProgressWin progressWin=new ProgressWin();
			progressWin.StartingMessage=Lan.g(this,"Contacting Global Payments...");
			progressWin.ShowCancelButton=false;
			progressWin.ActionMain=() => {
				try {
					VendorHttp.RunSync(() => gpV2Client.GetAccessTokenAsync(config,CancellationToken.None));
				}
				catch(Exception ex) {
					exception=ex;
				}
			};
			try {
				progressWin.ShowDialog();
			}
			catch(Exception ex) {
				exception=ex;
			}
			if(exception!=null) {
				labelTestResult.ForeColor=System.Drawing.Color.Firebrick;
				labelTestResult.Text=Lan.g(this,"Not connected.")+" "+exception.Message;
				return;
			}
			List<GpV2Account> listAccounts=gpV2Client.GetAccounts(config);
			string accountForCharges=gpV2Client.GetAccountNameForCharges(new GlobalPaymentsV2Config {
				AppId=config.AppId,AppKey=config.AppKey,UseSandbox=config.UseSandbox,AccountName="" });
			labelTestResult.ForeColor=System.Drawing.Color.DarkGreen;
			labelTestResult.Text=Lan.g(this,"Connected.")+(listAccounts.Count==0 ? "" : " "+Lan.g(this,"Accounts")+": "
				+string.Join(", ",listAccounts.Select(x => x.Name)));
			if(textAccountName.Text.Trim()=="" && accountForCharges!="" && (_clinicNumCur==0 || textAppId.Text.Trim()!="")) {
				textAccountName.Text=accountForCharges;
			}
		}

		private void butTestTerminal_Click(object sender,EventArgs e) {
			int index=gridTerminals.GetSelectedIndex();
			if(index<0) {
				MsgBox.Show(this,"Select a terminal first.");
				return;
			}
			PayTerminal payTerminal=(PayTerminal)gridTerminals.ListGridRows[index].Tag;
			GlobalPaymentsV2Config config=GetConfigFromForm();
			if(!config.IsComplete) {
				MsgBox.Show(this,"Enter the App ID and App Key first.");
				return;
			}
			string status="";
			Exception exception=null;
			GpV2Client gpV2Client=GlobalPaymentsV2.GetClient();
			ProgressWin progressWin=new ProgressWin();
			progressWin.StartingMessage=Lan.g(this,"Checking the terminal...");
			progressWin.ShowCancelButton=false;
			progressWin.ActionMain=() => {
				try {
					status=VendorHttp.RunSync(() => gpV2Client.PingDeviceAsync(config,payTerminal.TerminalID,CancellationToken.None));
				}
				catch(Exception ex) {
					exception=ex;
				}
			};
			try {
				progressWin.ShowDialog();
			}
			catch(Exception ex) {
				exception=ex;
			}
			if(exception!=null) {
				MessageBox.Show(this,Lan.g(this,"The terminal check failed.")+" "+exception.Message,GlobalPaymentsV2.DisplayName);
				return;
			}
			MessageBox.Show(this,Lan.g(this,"Global Payments answered for this terminal.")+(status=="" ? "" : " "+Lan.g(this,"Status")+": "+status),
				GlobalPaymentsV2.DisplayName);
		}

		///<summary>Checks every clinic shown in this window. Shows a message and returns false on the first problem.</summary>
		private bool ValidateClinics() {
			foreach(KeyValuePair<long,ClinicSettings> pair in _dictClinicSettings) {
				ClinicSettings clinicSettings=pair.Value;
				string clinicText=(pair.Key==0) ? Lan.g(this,"Headquarters") : Clinics.GetAbbr(pair.Key);
				bool hasKey=clinicSettings.AppKeyEntered!="" || clinicSettings.AppKeySaved.HasValue;
				if(clinicSettings.AppId!="" && !hasKey) {
					MessageBox.Show(this,Lan.g(this,"Enter the App Key for")+" "+clinicText+".");
					return false;
				}
				if(clinicSettings.AppId=="" && clinicSettings.AppKeyEntered!="") {
					MessageBox.Show(this,Lan.g(this,"Enter the App ID for")+" "+clinicText+".");
					return false;
				}
				if(clinicSettings.AppKeyEntered!="" && !_canEditSecret) {
					MsgBox.Show(this,"You need the permission to manage high security program properties to change the App Key.");
					return false;
				}
				//With clinics, the payment window shows the button only for a clinic with a valid payment type (as for PayConnect).
				if(checkEnabled.Checked && PrefC.HasClinicsEnabled && clinicSettings.AppId!=""
					&& !_listDefsPaymentType.Any(x => x.DefNum==clinicSettings.PaymentTypeDefNum))
				{
					MessageBox.Show(this,Lan.g(this,"Select the payment type for")+" "+clinicText+".");
					return false;
				}
			}
			return true;
		}

		///<summary>True if Headquarters or any clinic shown has an App ID and an App Key, or saved rows already do.</summary>
		private bool HasAnyCredentials() {
			foreach(KeyValuePair<long,ClinicSettings> pair in _dictClinicSettings) {
				if(pair.Value.AppId!="" && (pair.Value.AppKeyEntered!="" || pair.Value.AppKeySaved.HasValue)) {
					return true;
				}
			}
			List<long> listClinicNums=ProgramProperties.GetWhere(x => x.ProgramNum==_program.ProgramNum && x.PropertyDesc==GlobalPaymentsV2.PropAppId
				&& x.PropertyValue.Trim()!="").Select(x => x.ClinicNum).Distinct().ToList();
			return listClinicNums.Any(x => !_dictClinicSettings.ContainsKey(x) && GlobalPaymentsV2.GetConfig(x).IsComplete);
		}

		///<summary>Writes a plain setting of a clinic. A clinic's row is written only when it has one already or the value differs from Headquarters', so a
		///clinic that was only looked at keeps following Headquarters.</summary>
		private void WriteClinicSetting(long clinicNum,string desc,string value,string valueHq) {
			if(clinicNum!=0 && !HasOwnRow(desc,clinicNum) && value==valueHq) {
				return;
			}
			VendorCredentials.WriteSetting(_program.ProgramNum,desc,clinicNum,value);
		}

		private void butSave_Click(object sender,EventArgs e) {
			SaveFieldsToClinic();
			if(checkEnabled.Checked) {
				string conflict=GlobalPaymentsV2.GetExclusiveConflictMessage(ProgramName.MeetInTheCloud);
				if(conflict!="") {
					MessageBox.Show(this,conflict);
					return;
				}
				if(!HasAnyCredentials()) {
					MsgBox.Show(this,"Enter the App ID and App Key before enabling.");
					return;
				}
			}
			if(!ValidateClinics()) {
				return;
			}
			//Headquarters first, so a clinic's rows are compared against what Headquarters has now.
			ClinicSettings clinicSettingsHq=GetClinicSettings(0);
			foreach(long clinicNum in _dictClinicSettings.Keys.OrderBy(x => x)) {
				ClinicSettings clinicSettings=_dictClinicSettings[clinicNum];
				string secretToWrite=null;
				if(clinicSettings.AppKeyEntered!="") {
					secretToWrite=clinicSettings.AppKeyEntered;
				}
				else if(clinicSettings.AppKeySaved.NeedsReEncrypt && _canEditSecret) {
					secretToWrite=clinicSettings.AppKeySaved.PlainText;//A plain-text key typed into the generic property editor is encrypted now.
				}
				if(secretToWrite!=null) {
					if(!VendorCredentials.WriteSecret(_program.ProgramNum,GlobalPaymentsV2.PropAppKey,clinicNum,secretToWrite)) {
						MsgBox.Show(this,"The App Key could not be encrypted, so it was not saved.");
						return;
					}
					SecurityLogs.MakeLogEntry(EnumPermType.ManageHighSecurityProgProperties,0,_program.ProgDesc+"'s "+GlobalPaymentsV2.PropAppKey
						+" "+Lan.g(this,"was altered for")+" "+((clinicNum==0) ? Lan.g(this,"Headquarters") : Clinics.GetAbbr(clinicNum))+".",
						_program.ProgramNum,DateTime.Now);
				}
				if(clinicNum==0 || HasOwnRow(GlobalPaymentsV2.PropAppId,clinicNum) || clinicSettings.AppId!="") {
					VendorCredentials.WriteSetting(_program.ProgramNum,GlobalPaymentsV2.PropAppId,clinicNum,clinicSettings.AppId);
				}
				if(clinicNum==0 || HasOwnRow(GlobalPaymentsV2.PropAccountName,clinicNum) || clinicSettings.AccountName!="") {
					VendorCredentials.WriteSetting(_program.ProgramNum,GlobalPaymentsV2.PropAccountName,clinicNum,clinicSettings.AccountName);
				}
				WriteClinicSetting(clinicNum,GlobalPaymentsV2.PropPaymentType,(clinicSettings.PaymentTypeDefNum==0) ? "" : POut.Long(clinicSettings.PaymentTypeDefNum),
					(clinicSettingsHq.PaymentTypeDefNum==0) ? "" : POut.Long(clinicSettingsHq.PaymentTypeDefNum));
				WriteClinicSetting(clinicNum,GlobalPaymentsV2.PropPrintReceipt,POut.Bool(clinicSettings.PrintReceipt),POut.Bool(clinicSettingsHq.PrintReceipt));
				WriteClinicSetting(clinicNum,GlobalPaymentsV2.PropPreventSavingNewCC,POut.Bool(clinicSettings.PreventSavingNewCC),
					POut.Bool(clinicSettingsHq.PreventSavingNewCC));
				WriteClinicSetting(clinicNum,GlobalPaymentsV2.PropForceRecurringCharge,POut.Bool(clinicSettings.ForceDuplicates),
					POut.Bool(clinicSettingsHq.ForceDuplicates));
			}
			if(checkUseSandbox.Enabled) {
				VendorCredentials.WriteSetting(_program.ProgramNum,GlobalPaymentsV2.PropUseSandbox,0,POut.Bool(checkUseSandbox.Checked));
			}
			if(_program.Enabled!=checkEnabled.Checked) {
				_program.Enabled=checkEnabled.Checked;
				Programs.Update(_program);
				SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,GlobalPaymentsV2.DisplayName+" "
					+(checkEnabled.Checked ? Lan.g(this,"program link enabled.") : Lan.g(this,"program link disabled.")));
			}
			DataValid.SetInvalid(InvalidType.Programs);
			//New settings: forget cached tokens and earlier rejections in this process.
			GlobalPaymentsV2.GetClient().ResetAll();
			DialogResult=DialogResult.OK;
		}
	}

	///<summary>Registers the Global Payments V2 setup with Setup > Program Links (found by ProgramLinkSetupRegistry).</summary>
	public class GlobalPaymentsV2ProgramLinkSetup:IProgramLinkSetup {
		public ProgramName ProgName {
			get {
				return ProgramName.MeetInTheCloud;
			}
		}

		public bool EnsureProgram() {
			return GlobalPaymentsV2.EnsureProgram();
		}

		public DialogResult OpenSetup(IWin32Window owner) {
			using FormGlobalPaymentsV2Setup formGlobalPaymentsV2Setup=new FormGlobalPaymentsV2Setup();
			return formGlobalPaymentsV2Setup.ShowDialog(owner);
		}
	}

	///<summary>Registers Global Payments V1 (the EdgeExpress program link) with Setup > Program Links, so the 25.4 rename of its description is applied
	///when Program Links opens. Opens the same setup window as before.</summary>
	public class GlobalPaymentsV1ProgramLinkSetup:IProgramLinkSetup {
		public ProgramName ProgName {
			get {
				return ProgramName.EdgeExpress;
			}
		}

		public bool EnsureProgram() {
			if(GlobalPaymentsV1.RenameProgDescIfDefault()) {
				Programs.RefreshCache();
				return true;
			}
			return false;
		}

		public DialogResult OpenSetup(IWin32Window owner) {
			using FormEdgeExpressSetup formEdgeExpressSetup=new FormEdgeExpressSetup();
			return formEdgeExpressSetup.ShowDialog(owner);
		}
	}
}
