// Tests that the EMAIL-UI package stays wired as docs/specs/vendor-direct/README.md (3.2, 3.4, 3.5) and the Google and Microsoft 365 specs say:
// the email files use the in-repo clients instead of Bridges.dll, the defects the specs list stay fixed, and the Setup > Email window is vendor
// neutral and gated on the program links. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VendorDirectTests.Foundation;

namespace VendorDirectTests.Email {
	[TestClass]
	public class EmailUiLayoutTests {
		private static readonly string[] _arrayOwnedFiles={ @"OpenDentBusiness\Data Interface\EmailMessages.cs",@"OpenDentBusiness\Email\SendEmails.cs",
			@"OpenDental\Forms\FormEmailAddressEdit.cs",@"OpenDental\Forms\FormEmailAddressEdit.designer.cs",@"OpenDental\Forms\FormEmailMessageEdit.cs",
			@"OpenDental\Forms\FormEmailInbox.cs" };

		private static string ReadRepoFile(string relativePath) {
			return File.ReadAllText(Path.Combine(FoundationLayoutTests.GetRepositoryRoot(),relativePath));
		}

		[TestMethod]
		public void EmailFiles_UseTheInRepoMicrosoft365Client_NotBridges() {
			foreach(string file in _arrayOwnedFiles) {
				string text=ReadRepoFile(file);
				Assert.IsFalse(Regex.IsMatch(text,@"^\s*using\s+Bridges\s*;",RegexOptions.Multiline),file+" still imports Bridges.");
				Assert.IsFalse(text.Contains("Microsoft.Graph"),file+" still uses the Graph SDK types.");
				//Hosts are checked by tools/Test-NoHqHosts.ps1.
				foreach(string forbidden in new[] { "BuildOAuthUrl","WebServiceMainHQ","ODCloudClient","IsCloudServer" }) {
					Assert.IsFalse(text.Contains(forbidden),file+" contains "+forbidden);
				}
			}
			foreach(string file in new[] { @"OpenDentBusiness\Data Interface\EmailMessages.cs",@"OpenDentBusiness\Email\SendEmails.cs",
				@"OpenDental\Forms\FormEmailAddressEdit.cs",@"OpenDental\Forms\FormEmailMessageEdit.cs" })
			{
				StringAssert.Contains(ReadRepoFile(file),"using OpenDentBusiness.Microsoft365;",file);
			}
		}

		[TestMethod]
		public void EmailMessages_DefectsStayFixed() {
			string text=ReadRepoFile(@"OpenDentBusiness\Data Interface\EmailMessages.cs");
			Assert.IsFalse(text.Contains(".GetProfile("),"No token probe before sending: a send-only Gmail grant can't call getProfile (google.md defect 1).");
			Assert.IsFalse(text.Contains("TrimStart(\"GmailId\""),"Stored Gmail IDs are compared exactly (google.md defect 6).");
			Assert.IsFalse(text.Contains("TrimStart(\"MicrosoftId\""),"Stored Graph IDs are compared exactly (microsoft-365.md defect 5).");
			Assert.IsFalse(text.Contains("InnerException.Message.Contains(\"InvalidAuthenticationToken\")"),
				"Typed Microsoft365Exception instead of an unguarded string check (microsoft-365.md defect 1).");
			Assert.IsFalse(text.Contains("Google.MakeRefreshAccessTokenRequest"),"Gmail tokens come from GoogleTokenCache.");
			StringAssert.Contains(text,"GoogleTokenCache.GetAccessToken(emailAddress)");
			StringAssert.Contains(text,"Microsoft365Session.GetAccessToken(emailAddress,forceRefresh)");
			StringAssert.Contains(text,"GmailInbox.DecodeRaw(","The base64url padding is restored before decoding.");
			StringAssert.Contains(text,"GmailInbox.GetQuery(","A blank query lists the last 30 days, not the whole mailbox.");
		}

		[TestMethod]
		public void SendEmails_RethrowsTheVendorExceptionsUnchanged() {
			string text=ReadRepoFile(@"OpenDentBusiness\Email\SendEmails.cs");
			Assert.IsFalse(text.Contains("Unable to authenticate with Google"),"The GoogleApiException is rethrown with its HTTP status.");
			Assert.IsTrue(Regex.IsMatch(text,@"catch\(GoogleApiException\)\s*\{[^}]*throw;",RegexOptions.Singleline));
			Assert.IsTrue(Regex.IsMatch(text,@"catch\(Microsoft365Exception\)\s*\{[^}]*throw;",RegexOptions.Singleline));
			StringAssert.Contains(text,"internal static MimeMessage CreateMIMEMsg(","Gmail and Microsoft 365 share one MIME builder.");
			StringAssert.Contains(text,"\"message/rfc822\"","Large Gmail messages go through the upload URI.");
		}

		[TestMethod]
		public void EmailAddressWindow_IsNeutralAndGated() {
			string textDesigner=ReadRepoFile(@"OpenDental\Forms\FormEmailAddressEdit.designer.cs");
			foreach(string removed in new[] { "textAccessTokenGmail","textRefreshTokenGmail","textAccessTokenMicrosoft","textRefreshTokenMicrosoft",
				"google_signin","butAuthMicrosoft.Image","System.Windows.Forms.Button()" })
			{
				Assert.IsFalse(textDesigner.Contains(removed),"The designer still has "+removed+": tokens are never shown and there is no vendor artwork.");
			}
			StringAssert.Contains(textDesigner,"this.butAuthGoogle = new OpenDental.UI.Button();");
			StringAssert.Contains(textDesigner,"this.butAuthMicrosoft = new OpenDental.UI.Button();");
			StringAssert.Contains(textDesigner,"\"Authorize...\"");
			StringAssert.Contains(textDesigner,"\"Sign in...\"");
			StringAssert.Contains(textDesigner,"\"Gmail (OAuth)\"");
			Assert.IsFalse(ReadRepoFile(@"OpenDental\Forms\FormEmailAddressEdit.resx").Contains("butAuthMicrosoft.Image"),"The sign-in artwork is gone.");
			string text=ReadRepoFile(@"OpenDental\Forms\FormEmailAddressEdit.cs");
			StringAssert.Contains(text,"radioGmail.Visible=isGmailEnabled || _authenticationType==OAuthType.Google;");
			StringAssert.Contains(text,"radioMicrosoft.Visible=isMicrosoft365Enabled || _authenticationType==OAuthType.Microsoft;");
			StringAssert.Contains(text,"MicrosoftApiConnector.GetAccessToken(emailUsername,\"\",","Sign in always prompts (a blank cache argument).");
			Assert.IsFalse(text.Contains(".Result"),"Sign-out never blocks the UI thread on .Result (microsoft-365.md defect 6).");
			StringAssert.Contains(text,"GoogleOAuthSettings.ProtectRefreshToken(","Only the osd1$ refresh token is kept.");
			StringAssert.Contains(text,"Google.SignOutAsync(","Sign-out revokes the grant at Google (google.md defect 8).");
		}

		[TestMethod]
		public void EmailWindows_DownloadAndReauthenticate() {
			string textInbox=ReadRepoFile(@"OpenDental\Forms\FormEmailInbox.cs");
			int indexGetMessages=textInbox.IndexOf("private void GetMessages()",StringComparison.Ordinal);
			Assert.IsTrue(indexGetMessages>0);
			int indexReceive=textInbox.IndexOf("ReceiveFromOAuthInbox(GetSelectedAddress());",indexGetMessages,StringComparison.Ordinal);
			int indexPop3=textInbox.IndexOf("Pop3ServerIncoming==\"\"",indexGetMessages,StringComparison.Ordinal);
			Assert.IsTrue(indexReceive>0 && indexReceive<indexPop3,"The manual download runs before the blank-POP3 return (google.md 3.8).");
			string textMessage=ReadRepoFile(@"OpenDental\Forms\FormEmailMessageEdit.cs");
			StringAssert.Contains(textMessage,"EmailMessages.FindException<Microsoft365Exception>(ex)");
			StringAssert.Contains(textMessage,"microsoft365Exception.IsAuthError","A refused sign-in offers Sign in again instead of clearing the tokens.");
			Assert.IsFalse(textMessage.Contains("emailAddress.RefreshToken=\"\""),"The stored sign-in is never cleared automatically.");
		}
	}
}
