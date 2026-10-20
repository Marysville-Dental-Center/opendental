// Vendor-direct integrations: Setup > Program Links > Gmail API, where the office enters its own Google OAuth client.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Windows.Forms;
using CodeBase;
using OpenDentBusiness;
using OpenDentBusiness.VendorDirect;

namespace OpenDental {
	///<summary>The Gmail API program link (docs/specs/vendor-direct/google.md, section 3.7): the office's own Google Cloud "Desktop app" OAuth client ID
	///and secret, and the Enabled box. The secret is stored as osd1$ in a masked, high-security row, and only users with
	///ManageHighSecurityProgProperties can change it. The window describes what the integration does and nothing more: no links or promotion.
	///Nothing here calls Google. Each email address is then authorized in Setup > Email.</summary>
	public partial class FormGmailApiSetup:FormODBase {
		private Program _program;
		///<summary>The secret as loaded, to tell whether the user changed it. "" if none, unreadable, or the user may not see it.</summary>
		private string _clientSecretLoaded="";
		private bool _canEditSecret;

		public FormGmailApiSetup() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormGmailApiSetup_Load(object sender,EventArgs e) {
			if(GoogleOAuthSettings.EnsureProgram()) {
				DataValid.SetInvalid(InvalidType.Programs);
			}
			_program=Programs.GetCur(ProgramName.GmailApi);
			if(_program==null) {
				MsgBox.Show(this,"The Gmail API program link could not be added to the database.");
				DialogResult=DialogResult.Cancel;
				Close();
				return;
			}
			checkEnabled.Checked=_program.Enabled;
			textClientId.Text=GoogleOAuthSettings.GetClientId();
			_canEditSecret=Security.IsAuthorized(EnumPermType.ManageHighSecurityProgProperties,suppressMessage:true);
			SecretReadResult secretReadResult=GoogleOAuthSettings.ReadClientSecret();
			if(_canEditSecret) {
				_clientSecretLoaded=secretReadResult.PlainText;
				textClientSecret.Text=_clientSecretLoaded;
			}
			else {
				//Show that a secret is stored without putting it in the window.
				textClientSecret.Text=secretReadResult.HasValue ? "********" : "";
				textClientSecret.ReadOnly=true;
			}
			if(secretReadResult.Status==SecretReadStatus.Unreadable) {
				labelSecretStatus.Text=Lan.g(this,"The stored client secret could not be read. Enter it again.");
			}
			else if(!_canEditSecret) {
				labelSecretStatus.Text=Lan.g(this,"Changing the client secret needs the Manage High Security Program Properties permission.");
			}
			else {
				labelSecretStatus.Text="";
			}
		}

		private void butSave_Click(object sender,EventArgs e) {
			string clientId=textClientId.Text.Trim();
			if(checkEnabled.Checked && clientId=="") {
				MsgBox.Show(this,"Enter the client ID before enabling.");
				return;
			}
			if(clientId.IndexOfAny(new[] { ' ','\t','\r','\n' })>=0) {
				MsgBox.Show(this,"The client ID can't contain spaces.");
				return;
			}
			if(clientId!="" && !clientId.EndsWith(".apps.googleusercontent.com",StringComparison.OrdinalIgnoreCase)
				&& !MsgBox.Show(this,MsgBoxButtons.YesNo,"Google client IDs end in .apps.googleusercontent.com. Save this one anyway?"))
			{
				return;
			}
			string clientSecret=textClientSecret.Text.Trim();
			bool isSecretChanged=_canEditSecret && clientSecret!=_clientSecretLoaded;
			//The secret first: if it can't be encrypted, nothing is saved.
			if(isSecretChanged && !GoogleOAuthSettings.SetClientSecret(clientSecret)) {
				MsgBox.Show(this,"The client secret could not be encrypted, so nothing was saved.");
				return;
			}
			GoogleOAuthSettings.SetClientId(clientId);
			if(_program.Enabled!=checkEnabled.Checked) {
				Program programOld=_program.Copy();
				_program.Enabled=checkEnabled.Checked;
				Programs.Update(_program,programOld);
			}
			if(isSecretChanged) {
				SecurityLogs.MakeLogEntry(EnumPermType.ManageHighSecurityProgProperties,0,"Gmail API client secret was changed.",_program.ProgramNum,
					DateTime.Now);
			}
			DataValid.SetInvalid(InvalidType.Programs);
			DialogResult=DialogResult.OK;
		}
	}
}

namespace OpenDental.VendorDirect {
	///<summary>Puts the Gmail API program link in Setup > Program Links (found by ProgramLinkSetupRegistry).</summary>
	public class GmailApiProgramLinkSetup:IProgramLinkSetup {
		public ProgramName ProgName {
			get {
				return ProgramName.GmailApi;
			}
		}

		public bool EnsureProgram() {
			return GoogleOAuthSettings.EnsureProgram();
		}

		public DialogResult OpenSetup(IWin32Window owner) {
			using(FormGmailApiSetup formGmailApiSetup=new FormGmailApiSetup()) {
				return formGmailApiSetup.ShowDialog(owner);
			}
		}
	}
}
