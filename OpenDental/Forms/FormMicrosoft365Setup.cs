// Vendor-direct integrations: the Microsoft 365 email program link's setup window, and its registration with Setup > Program Links.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using OpenDentBusiness;
using OpenDentBusiness.Microsoft365;

namespace OpenDental {
	///<summary>Setup > Program Links > Microsoft 365 email. The office enters its own Microsoft Entra app registration: the Application (client) ID
	///and Directory (tenant) ID, plus advanced settings. Nothing is compiled in; there is no secret. Describes what the integration does and how to
	///register the app, and nothing more (ground rule 7). See docs/specs/vendor-direct/microsoft-365.md, sections 3.3, 3.7, 3.8 and 5.</summary>
	public partial class FormMicrosoft365Setup:FormODBase {
		private Program _program;

		public FormMicrosoft365Setup() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormMicrosoft365Setup_Load(object sender,EventArgs e) {
			if(Microsoft365Program.EnsureProgram()) {
				DataValid.SetInvalid(InvalidType.Programs);
			}
			_program=Programs.GetCur(ProgramName.Microsoft365Email);
			if(_program==null) {
				MsgBox.Show(this,"The Microsoft 365 email entry is missing from the database.");//Should never happen: EnsureProgram inserts it.
				DialogResult=DialogResult.Cancel;
				Close();
				return;
			}
			Microsoft365Config config=Microsoft365Program.GetConfig();
			checkEnabled.Checked=_program.Enabled;
			textClientId.Text=config.ClientId;
			textTenantId.Text=config.TenantId;
			textRedirectUri.Text=config.GetRedirectUriOrDefault();
			comboSignInMethod.Items.Add(Lan.g(this,"System browser"),Microsoft365SignInMethod.SystemBrowser);
			comboSignInMethod.Items.Add(Lan.g(this,"Device code"),Microsoft365SignInMethod.DeviceCode);
			comboSignInMethod.SetSelected(config.SignInMethod==Microsoft365SignInMethod.DeviceCode ? 1 : 0);
			checkAllowPersonal.Checked=config.AllowPersonalAccounts;
			labelPersonalWarning.Visible=checkAllowPersonal.Checked;
			textSteps.Text=GetRegistrationSteps();
		}

		///<summary>The registration steps by name, as plain text: no links to buy or sign up for anything.</summary>
		private string GetRegistrationSteps() {
			List<string> listSteps=new List<string>();
			listSteps.Add(Lan.g(this,"A tenant administrator does this once, in the Microsoft Entra admin center:"));
			listSteps.Add(Lan.g(this,"1. Entra ID > App registrations > New registration. Give it a name staff will recognize. Supported account types: "
				+"this organizational directory only (single tenant)."));
			listSteps.Add(Lan.g(this,"2. Authentication > Add a platform > Mobile and desktop applications, with the redirect URI http://localhost. "
				+"Leave Allow public client flows off unless the device code sign-in method is used. Add no client secret or certificate."));
			listSteps.Add(Lan.g(this,"3. API permissions > Microsoft Graph > Delegated: User.Read, Mail.Send, offline_access, openid, profile. Add "
				+"Mail.Read only if incoming email will be downloaded, and Mail.ReadWrite only for attachments over about 3 MB. Then Grant admin consent."));
			listSteps.Add(Lan.g(this,"4. Recommended: Enterprise apps > the app > Properties > Assignment required = Yes, then add only the users whose "
				+"mailboxes send from this program."));
			listSteps.Add(Lan.g(this,"5. Copy the Application (client) ID and Directory (tenant) ID from the app's Overview page into this window."));
			listSteps.Add(Lan.g(this,"Then, for each address: Setup > Email > the address > Microsoft > Sign in."));
			listSteps.Add(Lan.g(this,"If this database came from another program that used Microsoft sign-in, delete that program's enterprise "
				+"application in Entra ID and sign each address in again."));
			return string.Join("\r\n\r\n",listSteps);
		}

		private void checkAllowPersonal_CheckedChanged(object sender,EventArgs e) {
			labelPersonalWarning.Visible=checkAllowPersonal.Checked;
		}

		private void butSave_Click(object sender,EventArgs e) {
			Microsoft365Config config=new Microsoft365Config();
			config.ClientId=textClientId.Text.Trim();
			config.TenantId=textTenantId.Text.Trim();
			config.RedirectUri=string.IsNullOrWhiteSpace(textRedirectUri.Text) ? Microsoft365Program.DefaultRedirectUri : textRedirectUri.Text.Trim();
			config.SignInMethod=(comboSignInMethod.SelectedIndex==1) ? Microsoft365SignInMethod.DeviceCode : Microsoft365SignInMethod.SystemBrowser;
			config.AllowPersonalAccounts=checkAllowPersonal.Checked;
			List<string> listErrors=Microsoft365Program.Validate(config,checkEnabled.Checked);
			if(listErrors.Count>0) {
				MsgBox.Show(string.Join("\r\n",listErrors.Select(x => Lan.g(this,x))));
				return;
			}
			if(checkEnabled.Checked && config.SignInMethod==Microsoft365SignInMethod.DeviceCode
				&& !MsgBox.Show(this,MsgBoxButtons.YesNo,"Device code sign-in needs Allow public client flows turned on in the app registration, and "
				+"many tenants block it. Use it only where the browser can't reach this computer. Continue?"))
			{
				return;
			}
			Program programOld=_program.Copy();
			_program.Enabled=checkEnabled.Checked;
			Programs.Update(_program,programOld);
			Microsoft365Program.SaveConfig(config);
			DataValid.SetInvalid(InvalidType.Programs);
			//A new client ID or tenant means new sessions; drop the old ones so nothing keeps using the previous registration.
			Microsoft365Session.ForgetAll();
			SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,"Microsoft 365 email setup saved. Enabled: "+(checkEnabled.Checked ? "yes" : "no")+".");
			DialogResult=DialogResult.OK;
		}
	}

	///<summary>Registers the Microsoft 365 email setup with Setup > Program Links (found by reflection; see VendorDirect.ProgramLinkSetupRegistry).</summary>
	public class Microsoft365ProgramLinkSetup:VendorDirect.IProgramLinkSetup {
		public ProgramName ProgName {
			get {
				return ProgramName.Microsoft365Email;
			}
		}

		public bool EnsureProgram() {
			return Microsoft365Program.EnsureProgram();
		}

		public DialogResult OpenSetup(IWin32Window owner) {
			using(FormMicrosoft365Setup formMicrosoft365Setup=new FormMicrosoft365Setup()) {
				return formMicrosoft365Setup.ShowDialog(owner);
			}
		}
	}
}
