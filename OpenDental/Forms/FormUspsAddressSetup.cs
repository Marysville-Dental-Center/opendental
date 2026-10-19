// Vendor-direct integrations: the setup window for the USPS address validation program link (the office's own USPS app credentials).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using CodeBase;
using OpenDental.VendorDirect;
using OpenDentBusiness;
using OpenDentBusiness.VendorDirect;

namespace OpenDental {
	///<summary>Setup > Program Links > USPS address validation. Stores the Consumer Key, the Consumer Secret (osd1$, masked, high security; changed only
	///when re-entered, never shown again) and the environment, and turns the program link on or off. Vendor-neutral: no links, logos or sign-up.</summary>
	public partial class FormUspsAddressSetup:FormODBase {
		private Program _program;
		///<summary>What the secret row held when the window opened.</summary>
		private SecretReadResult _secretReadResult;
		private bool _canEditSecret;

		public FormUspsAddressSetup() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormUspsAddressSetup_Load(object sender,EventArgs e) {
			if(UspsAddresses.EnsureProgram()) {
				Programs.RefreshCache();
				ProgramProperties.RefreshCache();
			}
			_program=Programs.GetCur(ProgramName.USPSAddresses);
			if(_program==null) {
				MsgBox.Show(this,"The USPS address validation program link could not be created.");
				DialogResult=DialogResult.Cancel;
				Close();
				return;
			}
			comboEnvironment.Items.Add(Lan.g(this,"Production"),UspsEnvironment.Production);
			comboEnvironment.Items.Add(Lan.g(this,"Testing"),UspsEnvironment.Testing);
			checkEnabled.Checked=_program.Enabled;
			textConsumerKey.Text=ProgramProperties.GetValOrDefault(_program.ProgramNum,UspsAddresses.PropConsumerKey,0);
			UspsEnvironment uspsEnvironment=UspsAddresses.ParseEnvironment(
				ProgramProperties.GetValOrDefault(_program.ProgramNum,UspsAddresses.PropEnvironment,0));
			comboEnvironment.SelectedIndex=(uspsEnvironment==UspsEnvironment.Testing) ? 1 : 0;
			_secretReadResult=VendorCredentials.ReadSecret(_program.ProgramNum,UspsAddresses.PropConsumerSecret,0,LegacyPolicy.AcceptPlaintext);
			List<ProgramProperty> listProgramProperties=ProgramProperties.GetForProgram(_program.ProgramNum);
			_canEditSecret=ProgramProperties.CanEditProperties(listProgramProperties);
			textConsumerSecret.ReadOnly=!_canEditSecret;
			FillSecretStatus();
			labelTestResult.Text="";
		}

		private void FillSecretStatus() {
			if(!_canEditSecret) {
				labelSecretStatus.Text=Lan.g(this,"You need the permission to manage high security program properties to change the secret.");
			}
			else if(_secretReadResult.HasValue) {
				labelSecretStatus.Text=Lan.g(this,"A secret is saved. Leave blank to keep it.");
			}
			else if(_secretReadResult.NeedsReentry) {
				labelSecretStatus.Text=Lan.g(this,"The saved secret can't be read. Enter it again.");
			}
			else {
				labelSecretStatus.Text=Lan.g(this,"Not set.");
			}
		}

		private UspsEnvironment GetSelectedEnvironment() {
			if(comboEnvironment.SelectedIndex==1) {
				return UspsEnvironment.Testing;
			}
			return UspsEnvironment.Production;
		}

		///<summary>The settings as currently entered: the typed secret, else the saved one.</summary>
		private UspsConfig GetConfigFromForm() {
			UspsConfig uspsConfig=new UspsConfig();
			uspsConfig.ConsumerKey=textConsumerKey.Text.Trim();
			uspsConfig.ConsumerSecret=textConsumerSecret.Text.Trim();
			if(uspsConfig.ConsumerSecret=="") {
				uspsConfig.ConsumerSecret=_secretReadResult.PlainText;
			}
			uspsConfig.Environment=GetSelectedEnvironment();
			return uspsConfig;
		}

		private void butTest_Click(object sender,EventArgs e) {
			UspsConfig uspsConfig=GetConfigFromForm();
			if(!uspsConfig.IsComplete) {
				MsgBox.Show(this,"Enter the Consumer Key and Consumer Secret first.");
				return;
			}
			if(!MsgBox.Show(this,MsgBoxButtons.OKCancel,"Test connection sends one fixed, public test address (not a patient's) to USPS. "
				+"It counts as one lookup against the office's USPS quota and may be billed. Continue?"))
			{
				return;
			}
			string result="";
			UspsApiException uspsApiException=null;
			UI.ProgressWin progressWin=new UI.ProgressWin();
			progressWin.StartingMessage=Lan.g(this,"Contacting USPS...");
			progressWin.ShowCancelButton=false;
			progressWin.ActionMain=() => {
				try {
					result=USPSAddressValidationDatas.TestConnection(uspsConfig);
				}
				catch(UspsApiException ex) {
					uspsApiException=ex;
				}
			};
			try {
				progressWin.ShowDialog();
			}
			catch(Exception ex) {
				uspsApiException=new UspsApiException(UspsErrorKind.Offline,0,"",ex.Message,ex);
			}
			if(uspsApiException!=null) {
				labelTestResult.ForeColor=System.Drawing.Color.Firebrick;
				labelTestResult.Text=Lan.g(this,"Not connected.")+" "+uspsApiException.Message;
				return;
			}
			labelTestResult.ForeColor=System.Drawing.Color.DarkGreen;
			labelTestResult.Text=result;
		}

		private void butSave_Click(object sender,EventArgs e) {
			string consumerKey=textConsumerKey.Text.Trim();
			string secretEntered=textConsumerSecret.Text.Trim();
			bool hasSecret=(secretEntered!="" || _secretReadResult.HasValue);
			if(checkEnabled.Checked && (consumerKey=="" || !hasSecret)) {
				MsgBox.Show(this,"Enter the Consumer Key and Consumer Secret before enabling.");
				return;
			}
			//An accepted plain-text secret (typed into the generic property editor) is encrypted now.
			string secretToWrite=null;
			if(secretEntered!="") {
				secretToWrite=secretEntered;
			}
			else if(_secretReadResult.NeedsReEncrypt && _canEditSecret) {
				secretToWrite=_secretReadResult.PlainText;
			}
			if(secretToWrite!=null) {
				if(!_canEditSecret) {
					MsgBox.Show(this,"You need the permission to manage high security program properties to change the secret.");
					return;
				}
				if(!VendorCredentials.WriteSecret(_program.ProgramNum,UspsAddresses.PropConsumerSecret,0,secretToWrite)) {
					MsgBox.Show(this,"The secret could not be encrypted, so nothing was saved.");
					return;
				}
				SecurityLogs.MakeLogEntry(EnumPermType.ManageHighSecurityProgProperties,0,
					_program.ProgDesc+"'s "+UspsAddresses.PropConsumerSecret+" was altered.",_program.ProgramNum,DateTime.Now);
			}
			VendorCredentials.WriteSetting(_program.ProgramNum,UspsAddresses.PropConsumerKey,0,consumerKey);
			VendorCredentials.WriteSetting(_program.ProgramNum,UspsAddresses.PropEnvironment,0,GetSelectedEnvironment().ToString());
			if(_program.Enabled!=checkEnabled.Checked) {
				_program.Enabled=checkEnabled.Checked;
				Programs.Update(_program);
			}
			DataValid.SetInvalid(InvalidType.Programs);
			//New settings: forget cached tokens, earlier rejections and the quota pause in this process.
			UspsApiClient.Default.ResetAll();
			UspsAddresses.ClearQuotaCooldown();
			DialogResult=DialogResult.OK;
		}
	}

	///<summary>Registers the USPS address validation setup with Setup > Program Links (found by ProgramLinkSetupRegistry).</summary>
	public class UspsAddressProgramLinkSetup:IProgramLinkSetup {
		public ProgramName ProgName {
			get {
				return ProgramName.USPSAddresses;
			}
		}

		public bool EnsureProgram() {
			return UspsAddresses.EnsureProgram();
		}

		public DialogResult OpenSetup(IWin32Window owner) {
			using FormUspsAddressSetup formUspsAddressSetup=new FormUspsAddressSetup();
			return formUspsAddressSetup.ShowDialog(owner);
		}
	}
}
