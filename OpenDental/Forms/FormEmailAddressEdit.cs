using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using OpenDentBusiness;
using CodeBase;
using CodeBase.VendorDirect;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using OpenDental.UI;
using OpenDentBusiness.Microsoft365;
using Newtonsoft.Json;
using System.Linq;

namespace OpenDental{
///<summary></summary>
	public partial class FormEmailAddressEdit:FormODBase {
		private EmailAddress _emailAddress;
		private bool _isNew;
		private OAuthType _authenticationType=OAuthType.None;
		///<summary>The address's Gmail refresh token as it will be saved: osd1$ from an authorization in this window, or the stored value (which may
		///be a legacy token issued to Open Dental's client). Never shown. "" when not authorized.</summary>
		private string _gmailRefreshTokenStored="";
		///<summary>The account Google said was authorized in this window, or "".</summary>
		private string _gmailAuthorizedEmail="";
		///<summary>The scopes Google granted in this window's authorization, or "" if unknown.</summary>
		private string _gmailGrantedScopes="";
		///<summary>The tokens from an authorization in this window (the refresh token already removed), used to prime the access token cache once
		///the address is saved and has its EmailAddressNum. Null if not authorized in this window.</summary>
		private GoogleToken _googleTokenNew;
		///<summary>Microsoft 365: the account marker (MSAL HomeAccountId), saved to emailaddress.AccessToken. Never a bearer token.</summary>
		private string _microsoftAccountMarker="";
		///<summary>Microsoft 365: the encrypted MSAL cache (osd1$), saved to emailaddress.RefreshToken. Never shown.</summary>
		private string _microsoftStoredCache="";
		///<summary>The Microsoft 365 account that signed in in this window, or "".</summary>
		private string _microsoftSignedInAs="";
		private bool _isMicrosoftSignedInThisSession;
		///<summary>True if this window's Microsoft 365 sign-in asked for Mail.Read.</summary>
		private bool _isMicrosoftSignedInWithMailRead;
		///<summary>True if the stored sign-in changed in this window (signed in or out), so the address's session starts over after Save.</summary>
		private bool _hasMicrosoftSignInChanged;

		///<summary></summary>
		public FormEmailAddressEdit(EmailAddress emailAddress,bool isOpenedFromEmailSetup=false) {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
			_emailAddress=emailAddress;
			_authenticationType=emailAddress.AuthenticationType;
			List<long> listDefaultAddressNums=new List<long>();
			listDefaultAddressNums.Add(PrefC.GetLong(PrefName.EmailNotifyAddressNum));
			listDefaultAddressNums.Add(PrefC.GetLong(PrefName.EmailDefaultAddressNum));
			if(isOpenedFromEmailSetup && Security.IsAuthorized(EnumPermType.SecurityAdmin,suppressMessage:true)
				&& (_isNew || !listDefaultAddressNums.Contains(_emailAddress.EmailAddressNum)))
			{
				butPickUserod.Visible=true;
			}
		}

		public FormEmailAddressEdit(long userNum,bool isOpenedFromEmailSetup=false) : this(new EmailAddress() { UserNum=userNum },isOpenedFromEmailSetup) {
			_isNew=true;
		}

		private void FormEmailAddress_Load(object sender,System.EventArgs e) {
			//The Gmail API program link is inserted at runtime (Enabled=0) the first time it is needed. No Google call is made here.
			try {
				if(GoogleOAuthSettings.EnsureProgram()) {
					DataValid.SetInvalid(InvalidType.Programs);
				}
			}
			catch(Exception ex) {
				ex.DoNothing();//Without the row the Gmail option simply stays hidden.
			}
			bool enableEmailFields=true;
			if(_emailAddress!=null) {
				textSMTPserver.Text=_emailAddress.SMTPserver;
				textUsername.Text=_emailAddress.EmailUsername;
				if(!String.IsNullOrEmpty(_emailAddress.EmailPassword)) { //can happen if creating a new user email.
					textPassword.Text=MiscUtils.Decrypt(_emailAddress.EmailPassword);
				}
				textPort.Text=_emailAddress.ServerPort.ToString();
				checkSSL.Checked=_emailAddress.UseSSL;
				textSender.Text=_emailAddress.SenderAddress;
				textSMTPserverIncoming.Text=_emailAddress.Pop3ServerIncoming;
				textPortIncoming.Text=_emailAddress.ServerPortIncoming.ToString();
				//Both EmailNotifyAddressNum and EmailDefaultAddressNum could be 0 (unset), in which case we still may want to display the user.
				List<long> listDefaultAddressNums=new List<long>();
				listDefaultAddressNums.Add(PrefC.GetLong(PrefName.EmailNotifyAddressNum));
				listDefaultAddressNums.Add(PrefC.GetLong(PrefName.EmailDefaultAddressNum));
				if(_isNew || !listDefaultAddressNums.Contains(_emailAddress.EmailAddressNum)) {
					Userod userod=Userods.GetUser(_emailAddress.UserNum);
					textUserod.Tag=userod;
					textUserod.Text=userod?.UserName;
				}
				else {
					panelUserod.Visible=false;
				}
				if(_authenticationType==OAuthType.None) {
					radioPassword.Checked=true;
				}
				else if(_authenticationType==OAuthType.Google) {
					radioGmail.Checked=true;
					enableEmailFields=false;
					_gmailRefreshTokenStored=_emailAddress.RefreshToken??"";
					checkDownloadGmail.Checked=_emailAddress.DownloadInbox;
					textParams.Text=_emailAddress.QueryString;
					checkUnreadGmail.Checked=_emailAddress.QueryString.ToLower().Contains("is:unread");
				}
				else if(_authenticationType==OAuthType.Microsoft) {
					radioMicrosoft.Checked=true;
					enableEmailFields=false;
					_microsoftAccountMarker=_emailAddress.AccessToken??"";
					_microsoftStoredCache=_emailAddress.RefreshToken??"";
					checkDownloadMicrosoft.Checked=_emailAddress.DownloadInbox;
				}
			}
			//Ground rules 6 and 7: Gmail and Microsoft 365 sign-in are offered only once their program link is enabled in Setup > Program Links. An
			//address already set up for one of them still shows its state, so an office sees its own setting.
			bool isGmailEnabled=GoogleOAuthSettings.IsEnabled;
			radioGmail.Visible=isGmailEnabled || _authenticationType==OAuthType.Google;
			butAuthGoogle.Enabled=isGmailEnabled;
			bool isMicrosoft365Enabled=Microsoft365Program.IsEnabled();
			radioMicrosoft.Visible=isMicrosoft365Enabled || _authenticationType==OAuthType.Microsoft;
			butAuthMicrosoft.Enabled=isMicrosoft365Enabled;
			RefreshEmailTextFields(enableEmailFields);
		}

		private void RefreshEmailTextFields(bool isEnablingEmailFields=false) {
			//OAuth uses the API instead of ports so we can disable and autofill fields
			textPassword.Enabled=isEnablingEmailFields;
			textSMTPserver.ReadOnly=!isEnablingEmailFields;
			textPort.Enabled=isEnablingEmailFields;
			textSMTPserverIncoming.ReadOnly=!isEnablingEmailFields;
			textPortIncoming.Enabled=isEnablingEmailFields;
			panelPassword.Visible=radioPassword.Checked;
			groupMicrosoft.Visible=radioMicrosoft.Checked;
			groupGmail.Visible=radioGmail.Checked;
			textUsername.ReadOnly=false;
			if(radioGmail.Checked) {
				//The scopes asked for depend on Download incoming emails, so it can be ticked before authorizing.
				checkDownloadGmail.Enabled=true;
				if(!HasGmailToken()) { //Not authorized
					butClearTokensGmail.Enabled=false;
					textSMTPserver.Text="";
					textPort.Text="";
				}
				else {
					//The token belongs to one mailbox. Sign out to change the address.
					textUsername.ReadOnly=true;
					butClearTokensGmail.Enabled=true;
					textSMTPserver.Text="smtp.gmail.com";
					textPort.Text="0";
				}
				if(checkDownloadGmail.Checked) {
					textPassword.Text="";
					textSMTPserverIncoming.Text="pop.gmail.com";
					textPortIncoming.Text="0";
					textParams.Enabled=true;
					checkUnreadGmail.Enabled=true;
				}
				else {
					textSMTPserverIncoming.Text="";
					textPortIncoming.Text="";
					textParams.Text="";
					textParams.Enabled=false;
					checkUnreadGmail.Checked=false;
					checkUnreadGmail.Enabled=false;
				}
			}
			else if(radioMicrosoft.Checked) {
				textPassword.Text="";
				textSMTPserverIncoming.Text="";
				textPortIncoming.Text="";
				textSMTPserver.Text="";
				textPort.Text="";
				//The sign-in asks for Mail.Read only when Download incoming emails is ticked, so it can be ticked before signing in.
				checkDownloadMicrosoft.Enabled=true;
				if(!HasMicrosoftSignIn()) { //Not signed in
					butClearTokensMicrosoft.Enabled=false;
				}
				else {
					textUsername.ReadOnly=true;
					butClearTokensMicrosoft.Enabled=true;
					textSMTPserver.Text="smtp.outlook.com";
					textPort.Text="0";
					if(checkDownloadMicrosoft.Checked) {
						textSMTPserverIncoming.Text="pop.outlook.com";
						textPortIncoming.Text="0";
					}
				}
			}
			RefreshStatusLabels();
		}

		///<summary>Shows whether each address is authorized or signed in. Tokens and caches are never shown.</summary>
		private void RefreshStatusLabels() {
			butAuthGoogle.Text=Lan.g(this,HasGmailToken() ? "Re-authorize..." : "Authorize...");
			labelGmailStatus.Text=GetGmailStatusText();
			butAuthMicrosoft.Text=Lan.g(this,"Sign in...");
			labelMicrosoftStatus.Text=GetMicrosoftStatusText();
		}

		///<summary>True if the address holds a Gmail refresh token of any kind (it may still need authorization, see IsGmailTokenUsable).</summary>
		private bool HasGmailToken() {
			return !string.IsNullOrEmpty(_gmailRefreshTokenStored);
		}

		///<summary>True for an osd1$ refresh token this program can decrypt. A legacy token (issued to Open Dental's OAuth client) isn't usable.</summary>
		private bool IsGmailTokenUsable() {
			return HasGmailToken() && GoogleOAuthSettings.ReadRefreshToken(_gmailRefreshTokenStored).Status==OpenDentBusiness.VendorDirect.SecretReadStatus.Ok;
		}

		private string GetGmailStatusText() {
			if(!HasGmailToken()) {
				return Lan.g(this,"Not authorized.");
			}
			if(!IsGmailTokenUsable()) {
				return Lan.g(this,"Needs authorization. The stored authorization can't be used by this program. Click Re-authorize.");
			}
			EmailAddress emailAddressStored=new EmailAddress();
			emailAddressStored.EmailAddressNum=_emailAddress.EmailAddressNum;
			emailAddressStored.EmailUsername=textUsername.Text;
			emailAddressStored.RefreshToken=_gmailRefreshTokenStored;
			if(_googleTokenNew==null && GoogleTokenCache.NeedsReauthorization(emailAddressStored)) {
				return Lan.g(this,"Needs authorization. Google refused the stored authorization. Click Re-authorize.");
			}
			string account=(_gmailAuthorizedEmail!="") ? _gmailAuthorizedEmail : textUsername.Text.Trim();
			return Lan.g(this,"Authorized as")+" "+account;
		}

		///<summary>True if the address holds any Microsoft 365 sign-in (it may still need a new sign-in, see IsMicrosoftSignInUsable).</summary>
		private bool HasMicrosoftSignIn() {
			return !string.IsNullOrEmpty(_microsoftAccountMarker) || !string.IsNullOrEmpty(_microsoftStoredCache);
		}

		///<summary>True for an osd1$ MSAL cache this program can decrypt plus an account marker. A cache from Open Dental's app registration isn't.</summary>
		private bool IsMicrosoftSignInUsable() {
			return !string.IsNullOrEmpty(_microsoftAccountMarker)
				&& OpenDentBusiness.VendorDirect.SecretValue.Read(_microsoftStoredCache,OpenDentBusiness.VendorDirect.LegacyPolicy.RejectPlaintext).Status
					==OpenDentBusiness.VendorDirect.SecretReadStatus.Ok;
		}

		private string GetMicrosoftStatusText() {
			if(!HasMicrosoftSignIn()) {
				return Lan.g(this,"Not signed in.");
			}
			if(!IsMicrosoftSignInUsable()) {
				return Lan.g(this,"Needs sign-in. The stored sign-in can't be used by this program. Click Sign in.");
			}
			string account=(_microsoftSignedInAs!="") ? _microsoftSignedInAs : textUsername.Text.Trim();
			return Lan.g(this,"Signed in as")+" "+account;
		}

		private void ClearGmailInfo() {
			_gmailRefreshTokenStored="";
			_gmailAuthorizedEmail="";
			_gmailGrantedScopes="";
			_googleTokenNew=null;
			checkDownloadGmail.Checked=false;
			textParams.Text="";
			checkUnreadGmail.Checked=false;
			textSMTPserver.Text="";
			textPort.Text="";
			groupGmail.Visible=false;
		}

		private void ClearMicrosoftInfo() {
			textUsername.ReadOnly=false;
			if(HasMicrosoftSignIn()) {
				_hasMicrosoftSignInChanged=true;
			}
			_microsoftAccountMarker="";
			_microsoftStoredCache="";
			_microsoftSignedInAs="";
			_isMicrosoftSignedInThisSession=false;
			_isMicrosoftSignedInWithMailRead=false;
			checkDownloadMicrosoft.Checked=false;
			textPassword.Text="";
			textSMTPserver.Text="";
			textPort.Text="";
			groupMicrosoft.Visible=false;
		}

		private void checkDownloadGmail_CheckedChanged(object sender,EventArgs e) {
			if(checkDownloadGmail.Checked && IsGmailTokenUsable()) {
				//Google doesn't support incremental authorization for installed apps: read access means authorizing again with the full set.
				string grantedScopes=GetGmailGrantedScopes();
				if(grantedScopes!="" && !GoogleOAuthSettings.HasScope(grantedScopes,GoogleOAuthSettings.ScopeGmailReadOnly)) {
					if(!MsgBox.Show(this,MsgBoxButtons.YesNo,"Downloading needs read access. Re-authorize now?") || !AuthorizeGmail()) {
						checkDownloadGmail.Checked=false;
					}
					else if(!GoogleOAuthSettings.HasScope(_gmailGrantedScopes,GoogleOAuthSettings.ScopeGmailReadOnly)) {
						checkDownloadGmail.Checked=false;//AuthorizeGmail already said read access was not granted.
					}
				}
			}
			SetGmailDownloadFields();
			RefreshStatusLabels();
		}

		///<summary>The scopes Google granted the address, from this window's authorization or from this session's last token response. "" if unknown;
		///no network call is made to find out.</summary>
		private string GetGmailGrantedScopes() {
			if(_gmailGrantedScopes!="") {
				return _gmailGrantedScopes;
			}
			if(_emailAddress.EmailAddressNum==0 || _gmailRefreshTokenStored!=(_emailAddress.RefreshToken??"")) {
				return "";
			}
			return GoogleTokenCache.GetGrantedScopes(_emailAddress);
		}

		private void SetGmailDownloadFields() {
			if(checkDownloadGmail.Checked) {
				textSMTPserverIncoming.Text="pop.gmail.com";
				textPortIncoming.Text="0";
				checkUnreadGmail.Enabled=true;
				textParams.Enabled=true;
				checkUnreadGmail.Checked=textParams.Text.ToLower().Contains("is:unread");
			}
			else {
				textSMTPserverIncoming.Text="";
				textPortIncoming.Text="";
				textParams.Text="";
				textParams.Enabled=false;
				checkUnreadGmail.Checked=false;
				checkUnreadGmail.Enabled=false;
			}
		}

		private void checkDownloadMicrosoft_CheckedChanged(object sender,EventArgs e) {
			if(checkDownloadMicrosoft.Checked && HasMicrosoftSignIn()) {
				textSMTPserverIncoming.Text="pop.outlook.com";
				textPortIncoming.Text="0";
			}
			else {
				textSMTPserverIncoming.Text="";
				textPortIncoming.Text="";
			}
		}

		private void checkUnreadGmail_CheckedChanged(object sender,EventArgs e) {
			string strIsUnread="is:unread";
			string queryString=textParams.Text;
			if(checkUnreadGmail.Checked) {
				//Add is:unread if it is not present within the current query params.
				if(!queryString.ToLower().Contains(strIsUnread)) {
					queryString=queryString.Trim()+" "+strIsUnread;
				}
			}
			else {
				int index=queryString.ToLower().IndexOf(strIsUnread);
				//Remove is:unread if it is present within the current query params.
				if(index>-1) {
					queryString=textParams.Text.Remove(index,strIsUnread.Length);
				}
			}
			textParams.Text=queryString;
		}

		private void butDelete_Click(object sender,EventArgs e) {
			if(_isNew) {
				DialogResult=DialogResult.Cancel;
				return;
			}
			if(_emailAddress.EmailAddressNum==PrefC.GetLong(PrefName.EmailDefaultAddressNum)) {
				MsgBox.Show(this,"Cannot delete the default email address.");
				return;
			}
			if(_emailAddress.EmailAddressNum==PrefC.GetLong(PrefName.EmailNotifyAddressNum)) {
				MsgBox.Show(this,"Cannot delete the notify email address.");
				return;
			}
			Clinic clinic=Clinics.GetFirstOrDefault(x => x.EmailAddressNum==_emailAddress.EmailAddressNum);
			if(clinic!=null) {
				MessageBox.Show(Lan.g(this,"Cannot delete the email address because it is used by clinic")+" "+clinic.Description);
				return;
			}
			if(!MsgBox.Show(this,MsgBoxButtons.OKCancel,"Delete this email address?")) {
				return;
			}
			EmailAddresses.Delete(_emailAddress.EmailAddressNum);
			if(_emailAddress.AuthenticationType==OAuthType.Microsoft) {
				Microsoft365Session.Forget(_emailAddress.EmailAddressNum);
			}
			else if(_emailAddress.AuthenticationType==OAuthType.Google) {
				GoogleTokenCache.Invalidate(_emailAddress.EmailAddressNum);
			}
			DataValid.SetInvalid(InvalidType.Email);
			DialogResult=DialogResult.OK;//OK triggers a refresh for the grid.
		}

		private void butRegisterCertificate_Click(object sender,EventArgs e) {
			using FormEmailCertRegister formEmailCertRegister=new FormEmailCertRegister(textUsername.Text);
			formEmailCertRegister.ShowDialog();
		}

		private void butPickUserod_Click(object sender,EventArgs e) {
			List<Clinic> listClinics=Clinics.GetDeepCopy();
 			if(listClinics.Any(x=>x.EmailAddressNum==_emailAddress.EmailAddressNum)) {
				MsgBox.Show(this,"Cannot associate a user to an email address that is linked to a clinic.");
				return;
			}
			FrmUserPick frmUserPick=new FrmUserPick();
			frmUserPick.UserNumSuggested=((Userod)textUserod.Tag)?.UserNum??0;//Preselect current selection.
			frmUserPick.ShowDialog();
			if(!frmUserPick.IsDialogOK) {
				return;
			}
			Userod userod=Userods.GetUser(frmUserPick.UserNumSelected);
			if(userod.UserNum==(((Userod)textUserod.Tag)?.UserNum??0)) {
				return;//No change.
			}
			//grabs from the database in case the cache hasn't refreshed
			EmailAddress emailAddress=EmailAddresses.GetForUserDb(userod.UserNum);
			if(emailAddress!=null) {
				MsgBox.Show(this,"User email already exists for "+userod.UserName);
				return;
			}
			textUserod.Tag=userod;
			textUserod.Text=userod.UserName;
		}

		///<summary>True if this address is the practice's default or notify address, or a clinic's address.</summary>
		private bool IsSharedAddress() {
			if(_isNew || _emailAddress.EmailAddressNum==0) {
				return false;
			}
			if(_emailAddress.EmailAddressNum==PrefC.GetLong(PrefName.EmailDefaultAddressNum)
				|| _emailAddress.EmailAddressNum==PrefC.GetLong(PrefName.EmailNotifyAddressNum))
			{
				return true;
			}
			return Clinics.GetFirstOrDefault(x => x.EmailAddressNum==_emailAddress.EmailAddressNum)!=null;
		}

		///<summary>Asks whether to sign out of providerName. For the practice, notify or a clinic address it also warns that email stops.</summary>
		private bool ConfirmSignOut(string providerName) {
			string message=Lan.g(this,"Do you want to sign out of")+" "+providerName+"?";
			if(IsSharedAddress()) {
				message+="\r\n\r\n"+Lan.g(this,"This is a default or clinic email address. Email from it stops until someone authorizes it again.");
			}
			return MessageBox.Show(this,message,Text,MessageBoxButtons.YesNo)==DialogResult.Yes;
		}

		///<summary>Signs the address out at Google, best effort, in the background (google.md 3.2): revokes the refresh token unless another Google
		///address is the same mailbox, and drops the cached access token. A legacy token is never sent, and nothing is sent while the program link
		///is off. The caller blanks the form values.</summary>
		private void SignOutGmail() {
			long emailAddressNum=_emailAddress.EmailAddressNum;
			string storedRefreshToken=_gmailRefreshTokenStored;
			string mailbox=textUsername.Text.Trim();
			GoogleTokenCache.Invalidate(emailAddressNum);
			if(string.IsNullOrEmpty(storedRefreshToken) || !GoogleOAuthSettings.IsEnabled) {
				return;
			}
			System.Threading.Tasks.Task.Run(async () => {
				try {
					await Google.SignOutAsync(emailAddressNum,mailbox,storedRefreshToken).ConfigureAwait(false);
				}
				catch(Exception ex) {
					ex.DoNothing();//Best effort: an already revoked token or a network error changes nothing here.
				}
			});
		}

		///<summary>Forgets the address's Microsoft 365 session. There is no revocation endpoint for public clients: blanking the stored cache on Save
		///discards the refresh token. Makes no network call, so it never blocks the UI thread.</summary>
		private void SignOutMicrosoft() {
			try {
				MicrosoftApiConnector.SignOutUser(textUsername.Text,_microsoftStoredCache).GetAwaiter().GetResult();//Completes at once.
				if(_emailAddress.EmailAddressNum!=0) {
					Microsoft365Session.Forget(_emailAddress.EmailAddressNum);
				}
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
		}

		private void butClearTokensGmail_Click(object sender,EventArgs e) {
			if(!ConfirmSignOut("Gmail")) {
				return;
			}
			SignOutGmail();
			_authenticationType=OAuthType.None;
			ClearGmailInfo();
			RefreshEmailTextFields(isEnablingEmailFields:true);
		}

		private void butClearTokensMicrosoft_Click(object sender,EventArgs e) {
			if(!ConfirmSignOut("Microsoft 365")) {
				return;
			}
			SignOutMicrosoft();
			_authenticationType=OAuthType.None;
			ClearMicrosoftInfo();
			RefreshEmailTextFields(isEnablingEmailFields:true);
		}

		///<summary>Authorizes this address to send (and, with Download incoming emails ticked, read) mail through the Gmail API, with the office's own
		///OAuth client: the system browser, a loopback redirect on 127.0.0.1 and PKCE, straight to Google (google.md 3.2). The person authorizing
		///must sign in to Google as this mailbox. Nothing is written to the database until Save.</summary>
		private void butAuthGoogle_Click(object sender,EventArgs e) {
			AuthorizeGmail();
			RefreshEmailTextFields();
		}

		///<summary>Runs the Gmail authorization. Returns true if the address is now authorized (the refresh token is held encrypted, ready for Save).</summary>
		private bool AuthorizeGmail() {
			if(!GoogleOAuthSettings.IsEnabled) {
				MessageBox.Show(this,Lan.g(this,GoogleOAuthSettings.MessageNotEnabled));
				return false;
			}
			if(!GoogleOAuthSettings.IsConfigured) {
				MessageBox.Show(this,Lan.g(this,GoogleOAuthSettings.MessageNotConfiguredAuthorize));
				return false;
			}
			string emailAddress=textUsername.Text.Trim();
			if(emailAddress=="") {
				MsgBox.Show(this,"Enter the email address first, then sign in to Google as that mailbox.");
				return false;
			}
			List<string> listScopes=GoogleOAuthSettings.GetScopes(isDownloadingMail:checkDownloadGmail.Checked);
			GoogleToken googleToken=null;
			Exception exception=null;
			CancellationTokenSource cancellationTokenSource=new CancellationTokenSource();
			Google.AuthorizationRequest authorizationRequest=new Google.AuthorizationRequest();
			authorizationRequest.EmailAddressNum=_emailAddress.EmailAddressNum;
			ProgressWin progressWin=new ProgressWin();
			progressWin.StartingMessage=Lan.g(this,"Waiting for authorization in the browser")+"...";
			progressWin.ActionCancel=() => cancellationTokenSource.Cancel();
			progressWin.ActionMain=() => {
				try {
					googleToken=VendorHttp.RunSync(() => authorizationRequest.MakeAccessTokenRequestAsync(emailAddress,listScopes,cancellationTokenSource.Token));
				}
				catch(Exception ex) {
					exception=ex;//Kept here: ProgressWin would rethrow only an inner exception.
				}
			};
			try {
				progressWin.ShowDialog();
			}
			catch(Exception ex) {
				exception=ex;
			}
			finally {
				if(progressWin.IsCancelled) {
					cancellationTokenSource.Cancel();
				}
				authorizationRequest.CloseListener();
			}
			if(progressWin.IsCancelled) {
				return false;
			}
			if(exception!=null) {
				GoogleAuthException googleAuthException=EmailMessages.FindException<GoogleAuthException>(exception);
				if(googleAuthException!=null && googleAuthException.Reason==GoogleAuthReason.Cancelled) {
					return false;
				}
				if(googleAuthException!=null) {
					MessageBox.Show(this,googleAuthException.Message);
					return false;
				}
				FriendlyException.Show(Lan.g(this,"Gmail authorization failed."),exception);
				return false;
			}
			if(googleToken==null) {//This should never happen. An error should be thrown.
				MsgBox.Show(this,"Gmail authorization failed.");
				return false;
			}
			string storedRefreshToken;
			if(!GoogleOAuthSettings.ProtectRefreshToken(googleToken.RefreshToken,out storedRefreshToken)) {
				MsgBox.Show(this,"The authorization could not be stored securely, so it was not kept. Try again.");
				return false;
			}
			googleToken.RefreshToken="";//Only the encrypted copy is kept.
			_gmailRefreshTokenStored=storedRefreshToken;
			_gmailAuthorizedEmail=googleToken.Email;
			_gmailGrantedScopes=googleToken.Scope??"";
			_googleTokenNew=googleToken;
			GoogleTokenCache.Prime(_emailAddress.EmailAddressNum,storedRefreshToken,googleToken);
			textSMTPserver.Text="smtp.gmail.com";
			textPort.Text="0";
			_authenticationType=OAuthType.Google;
			if(checkDownloadGmail.Checked && !GoogleOAuthSettings.HasScope(_gmailGrantedScopes,GoogleOAuthSettings.ScopeGmailReadOnly)) {
				MsgBox.Show(this,"Read access was not granted; this address can send but not download mail.");
				checkDownloadGmail.Checked=false;
				SetGmailDownloadFields();
			}
			return true;
		}

		///<summary>Signs this address in to Microsoft 365 with the office's own app registration: MSAL in the system browser (or device code, per the
		///setup), then a check that the account is this address (microsoft-365.md 3.2). Nothing is written to the database until Save.</summary>
		private void butAuthMicrosoft_Click(object sender,EventArgs e) {
			SignInMicrosoft();
			RefreshEmailTextFields();
		}

		///<summary>Runs an interactive Microsoft 365 sign-in. Always interactive: a blank cache argument, so a signed-in address can switch account.
		///Returns true if the address is now signed in (the encrypted cache and account marker are held, ready for Save).</summary>
		private bool SignInMicrosoft() {
			if(!Microsoft365Program.IsEnabled()) {
				MessageBox.Show(this,Lan.g(this,Microsoft365Errors.MessageTurnedOff));
				return false;
			}
			string emailUsername=textUsername.Text.Trim();
			bool includeMailRead=checkDownloadMicrosoft.Checked;
			MicrosoftTokenHelper microsoftTokenHelper=null;
			Exception exception=null;
			CancellationTokenSource cancellationTokenSource=new CancellationTokenSource();
			ProgressWin progressWin=new ProgressWin();
			progressWin.StartingMessage=Lan.g(this,"Waiting for the Microsoft 365 sign-in in the browser")+"...";
			progressWin.ActionCancel=() => cancellationTokenSource.Cancel();
			progressWin.ActionMain=() => {
				try {
					microsoftTokenHelper=VendorHttp.RunSync(() => MicrosoftApiConnector.GetAccessToken(emailUsername,"",includeMailRead,cancellationTokenSource.Token));
				}
				catch(Exception ex) {
					exception=ex;//Kept here: ProgressWin would rethrow only an inner exception.
				}
			};
			try {
				progressWin.ShowDialog();
			}
			catch(Exception ex) {
				exception=ex;
			}
			finally {
				if(progressWin.IsCancelled) {
					cancellationTokenSource.Cancel();
				}
			}
			if(progressWin.IsCancelled) {
				return false;
			}
			if(exception!=null) {
				FriendlyException.Show(Lan.g(this,"Microsoft 365 sign-in failed."),exception);
				return false;
			}
			if(microsoftTokenHelper==null) {
				return false;
			}
			if(microsoftTokenHelper.ErrorMessage!="") {
				MessageBox.Show(this,microsoftTokenHelper.ErrorMessage);
				return false;
			}
			if(microsoftTokenHelper.AccessToken=="") {
				return false; //Sign-in was cancelled so do nothing.
			}
			if(!string.IsNullOrWhiteSpace(microsoftTokenHelper.EmailAddress)) {
				textUsername.Text=microsoftTokenHelper.EmailAddress;
			}
			_microsoftAccountMarker=microsoftTokenHelper.AccessToken;
			_microsoftStoredCache=microsoftTokenHelper.AccountInfo;
			_microsoftSignedInAs=microsoftTokenHelper.EmailAddress;
			_isMicrosoftSignedInThisSession=true;
			_isMicrosoftSignedInWithMailRead=includeMailRead;
			_hasMicrosoftSignInChanged=true;
			textSMTPserver.Text="smtp.outlook.com";
			textPort.Text="0";
			_authenticationType=OAuthType.Microsoft;
			return true;
		}

		private void radioPassword_Click(object sender,EventArgs e) {
			if(_authenticationType==OAuthType.Google && HasGmailToken()) {
				if(!ConfirmSignOut("Gmail")) {
					radioGmail.Checked=true;
					return;
				}
				SignOutGmail();
			}
			else if(_authenticationType==OAuthType.Microsoft && HasMicrosoftSignIn()) {
				if(!ConfirmSignOut("Microsoft 365")) {
					radioMicrosoft.Checked=true;
					return;
				}
				SignOutMicrosoft();
			}
			_authenticationType=OAuthType.None;
			ClearGmailInfo();
			ClearMicrosoftInfo();
			RefreshEmailTextFields(true);
		}

		private void radioGmail_Click(object sender,EventArgs e) {
			if(_authenticationType==OAuthType.Google) {
				return;
			}
			if(_authenticationType==OAuthType.Microsoft && HasMicrosoftSignIn()) {
				if(!ConfirmSignOut("Microsoft 365")) {
					radioMicrosoft.Checked=true;
					return;
				}
				SignOutMicrosoft();
			}
			_authenticationType=OAuthType.None;
			ClearMicrosoftInfo();
			RefreshEmailTextFields();
		}

		private void radioMicrosoft_Click(object sender,EventArgs e) {
			if(_authenticationType==OAuthType.Microsoft) {
				return;
			}
			if(_authenticationType==OAuthType.Google && HasGmailToken()) {
				if(!ConfirmSignOut("Gmail")) {
					radioGmail.Checked=true;
					return;
				}
				SignOutGmail();
			}
			_authenticationType=OAuthType.None;
			ClearGmailInfo();
			RefreshEmailTextFields();
		}

		private void butSave_Click(object sender, System.EventArgs e) {
			try {
				PIn.Int(textPort.Text);
			}
			catch {
				MsgBox.Show(this,"Invalid outgoing port number.");
				return;
			}
			try {
				PIn.Int(textPortIncoming.Text);
			}
			catch {
				MsgBox.Show(this,"Invalid incoming port number.");
				return;
			}
			if(string.IsNullOrWhiteSpace(textUsername.Text)) {
				MsgBox.Show(this,"Please enter a valid email address.");
				return;
			}
			//Only checks against non-user email addresses.
			if(EmailAddresses.AddressExists(textUsername.Text,_emailAddress.EmailAddressNum)) {
				MsgBox.Show(this,"This email address already exists.");
				return;
			}
			//Microsoft 365: downloading needs Mail.Read, which a sign-in asks for only when Download incoming emails was ticked.
			if(_authenticationType==OAuthType.Microsoft && checkDownloadMicrosoft.Checked && IsMicrosoftSignInUsable()
				&& (_isMicrosoftSignedInThisSession ? !_isMicrosoftSignedInWithMailRead : !_emailAddress.DownloadInbox))
			{
				if(MsgBox.Show(this,MsgBoxButtons.YesNo,"Downloading incoming emails needs the Mail.Read permission, which this sign-in may not include. "
					+"Sign in again now?"))
				{
					if(!SignInMicrosoft()) {
						RefreshEmailTextFields();
						return;
					}
					RefreshEmailTextFields();
				}
			}
			bool isGmailAuthorizedNow=(_authenticationType==OAuthType.Google && _googleTokenNew!=null);
			bool isMicrosoftSignedInNow=(_authenticationType==OAuthType.Microsoft && _isMicrosoftSignedInThisSession);
			_emailAddress.SMTPserver=PIn.String(textSMTPserver.Text);
			_emailAddress.EmailUsername=PIn.String(textUsername.Text);
			_emailAddress.EmailPassword="";
			_emailAddress.ServerPort=PIn.Int(textPort.Text);
			_emailAddress.UseSSL=checkSSL.Checked;
			_emailAddress.SenderAddress=PIn.String(textSender.Text);
			_emailAddress.Pop3ServerIncoming=PIn.String(textSMTPserverIncoming.Text);
			_emailAddress.ServerPortIncoming=PIn.Int(textPortIncoming.Text);
			_emailAddress.UserNum=((Userod)(textUserod.Tag))?.UserNum??0;
			_emailAddress.AuthenticationType=_authenticationType;
			if(_authenticationType==OAuthType.None) {
				_emailAddress.EmailPassword=PIn.String(MiscUtils.Encrypt(textPassword.Text));
				_emailAddress.AccessToken="";
				_emailAddress.RefreshToken="";
				_emailAddress.DownloadInbox=false;
				_emailAddress.QueryString="";
			}
			else if(_authenticationType==OAuthType.Google) {
				//The refresh token is osd1$ (encrypted when it was issued, refused if encryption failed). Access tokens live in memory only.
				_emailAddress.AccessToken="";
				_emailAddress.RefreshToken=_gmailRefreshTokenStored;
				_emailAddress.DownloadInbox=checkDownloadGmail.Checked;
				_emailAddress.QueryString=textParams.Text;
			}
			else if(_authenticationType==OAuthType.Microsoft) {
				//AccessToken holds the account marker, not a bearer token; RefreshToken holds the osd1$ MSAL cache.
				_emailAddress.AccessToken=_microsoftAccountMarker;
				_emailAddress.RefreshToken=_microsoftStoredCache;
				_emailAddress.DownloadInbox=checkDownloadMicrosoft.Checked;
				_emailAddress.QueryString=""; //This value is not used by Microsoft OAuth
			}
			if(_isNew) {
				EmailAddresses.Insert(_emailAddress);
			}
			else {
				EmailAddresses.Update(_emailAddress);
			}
			if(isGmailAuthorizedNow) {
				GoogleTokenCache.Prime(_emailAddress.EmailAddressNum,_gmailRefreshTokenStored,_googleTokenNew);
				//No token and no address in the log.
				SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,"Gmail authorized for EmailAddressNum "+POut.Long(_emailAddress.EmailAddressNum));
			}
			if(_hasMicrosoftSignInChanged) {
				Microsoft365Session.Forget(_emailAddress.EmailAddressNum);
			}
			if(isMicrosoftSignedInNow) {
				SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,"Microsoft 365 signed in for EmailAddressNum "+POut.Long(_emailAddress.EmailAddressNum));
			}
			DataValid.SetInvalid(InvalidType.Email);
			DialogResult=DialogResult.OK;
		}

	}
}