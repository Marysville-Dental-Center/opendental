// Tests for the Microsoft 365 email settings and the error mapping (docs/specs/vendor-direct/microsoft-365.md, 3.3, 3.5 and 3.7). No network.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness.Microsoft365;

namespace VendorDirectTests.Microsoft365 {
	[TestClass]
	public class Microsoft365ConfigTests {
		private const string CLIENT_ID="11111111-2222-3333-4444-555555555555";
		private const string TENANT_ID="66666666-7777-8888-9999-000000000000";

		private static Microsoft365Config MakeConfig(string clientId,string tenantId,bool allowPersonal=false,string redirectUri="http://localhost") {
			Microsoft365Config config=new Microsoft365Config();
			config.ClientId=clientId;
			config.TenantId=tenantId;
			config.AllowPersonalAccounts=allowPersonal;
			config.RedirectUri=redirectUri;
			return config;
		}

		[TestMethod]
		public void ClientId_MustBeANonEmptyGuid() {
			Assert.IsTrue(Microsoft365Program.IsValidClientId(CLIENT_ID));
			Assert.IsTrue(Microsoft365Program.IsValidClientId(" "+CLIENT_ID.ToUpperInvariant()+" "));
			Assert.IsFalse(Microsoft365Program.IsValidClientId(""));
			Assert.IsFalse(Microsoft365Program.IsValidClientId("not-a-guid"));
			Assert.IsFalse(Microsoft365Program.IsValidClientId("00000000-0000-0000-0000-000000000000"));
		}

		[TestMethod]
		public void Tenant_AcceptsGuidDomainAndReservedWords() {
			Assert.IsTrue(Microsoft365Program.IsValidTenant(TENANT_ID));
			Assert.IsTrue(Microsoft365Program.IsValidTenant("contoso.onmicrosoft.com"));
			Assert.IsTrue(Microsoft365Program.IsValidTenant("example-dental.com"));
			Assert.IsTrue(Microsoft365Program.IsValidTenant("organizations"));
			Assert.IsTrue(Microsoft365Program.IsValidTenant("common"));
			Assert.IsTrue(Microsoft365Program.IsValidTenant("Consumers"));
			Assert.IsFalse(Microsoft365Program.IsValidTenant(""));
			Assert.IsFalse(Microsoft365Program.IsValidTenant("no spaces.com"));
			Assert.IsFalse(Microsoft365Program.IsValidTenant("https://login.microsoftonline.com/"+TENANT_ID));
		}

		[TestMethod]
		public void RedirectUri_OnlyHttpLocalhost() {
			Assert.IsTrue(Microsoft365Program.IsValidRedirectUri("http://localhost"));
			Assert.IsTrue(Microsoft365Program.IsValidRedirectUri("http://localhost:53000"));
			Assert.IsTrue(Microsoft365Program.IsValidRedirectUri("http://localhost:53000/callback"));
			Assert.IsFalse(Microsoft365Program.IsValidRedirectUri("https://localhost"));
			Assert.IsFalse(Microsoft365Program.IsValidRedirectUri("http://127.0.0.1"));
			Assert.IsFalse(Microsoft365Program.IsValidRedirectUri("http://example.com"));
			Assert.IsFalse(Microsoft365Program.IsValidRedirectUri("http://localhost/?x=1"));
			Assert.IsFalse(Microsoft365Program.IsValidRedirectUri("http://user@localhost"));
		}

		[TestMethod]
		public void Validate_RequiresIdsOnlyWhenEnabled() {
			Assert.AreEqual(0,Microsoft365Program.Validate(MakeConfig("",""),false).Count,"A disabled, blank setup can be saved.");
			Assert.AreEqual(2,Microsoft365Program.Validate(MakeConfig("",""),true).Count);
			Assert.AreEqual(0,Microsoft365Program.Validate(MakeConfig(CLIENT_ID,TENANT_ID),true).Count);
			Assert.AreEqual(1,Microsoft365Program.Validate(MakeConfig("bad",""),false).Count,"Filled-in values are always checked.");
		}

		[TestMethod]
		public void Validate_PersonalAccountsNeedTheBoxAndAPersonalTenant() {
			List<string> listErrors=Microsoft365Program.Validate(MakeConfig(CLIENT_ID,"common"),true);
			Assert.AreEqual(1,listErrors.Count,"common without Allow personal accounts is refused.");
			Assert.AreEqual(0,Microsoft365Program.Validate(MakeConfig(CLIENT_ID,"common",allowPersonal:true),true).Count);
			Assert.AreEqual(1,Microsoft365Program.Validate(MakeConfig(CLIENT_ID,TENANT_ID,allowPersonal:true),true).Count,
				"Personal accounts can't sign in to a single tenant.");
			Assert.AreEqual(0,Microsoft365Program.Validate(MakeConfig(CLIENT_ID,"organizations"),true).Count);
		}

		[TestMethod]
		public void ConfigFromValues_DefaultsAndUnknownMethod() {
			Microsoft365Config config=Microsoft365Program.ConfigFromValues(" "+CLIENT_ID+" ",TENANT_ID,"","Carrier pigeon","");
			Assert.AreEqual(CLIENT_ID,config.ClientId);
			Assert.AreEqual("http://localhost",config.RedirectUri);
			Assert.AreEqual(Microsoft365SignInMethod.SystemBrowser,config.SignInMethod);
			Assert.IsFalse(config.AllowPersonalAccounts);
			config=Microsoft365Program.ConfigFromValues(CLIENT_ID,TENANT_ID,"http://localhost:5000","DeviceCode","1");
			Assert.AreEqual(Microsoft365SignInMethod.DeviceCode,config.SignInMethod);
			Assert.IsTrue(config.AllowPersonalAccounts);
			Assert.AreEqual("http://localhost:5000",config.GetRedirectUriOrDefault());
			Assert.IsTrue(config.IsComplete);
			Assert.IsFalse(new Microsoft365Config().IsComplete);
		}

		[TestMethod]
		public void PropertyNames_MatchTheSpec() {
			//These strings are the programproperty.PropertyDesc values; changing one orphans the office's stored setting.
			Assert.AreEqual("Application (client) ID",Microsoft365Program.PropertyClientId);
			Assert.AreEqual("Directory (tenant) ID",Microsoft365Program.PropertyTenantId);
			Assert.AreEqual("Redirect URI",Microsoft365Program.PropertyRedirectUri);
			Assert.AreEqual("Sign-in Method",Microsoft365Program.PropertySignInMethod);
			Assert.AreEqual("Allow personal accounts",Microsoft365Program.PropertyAllowPersonalAccounts);
			Assert.AreEqual(5,Microsoft365Program.ListPropertyDefaults.Count);
		}

		[TestMethod]
		public void IdentityErrors_MapToTheSpecMessages() {
			Assert.AreEqual(Microsoft365ErrorKind.AppRegistration,Microsoft365Errors.ClassifyIdentityError("unauthorized_client",
				"AADSTS700016: Application with identifier 'x' was not found in the directory."));
			Assert.AreEqual(Microsoft365ErrorKind.AppRegistration,Microsoft365Errors.ClassifyIdentityError("invalid_request","AADSTS90002: Tenant not found."));
			Assert.AreEqual(Microsoft365ErrorKind.RedirectUri,Microsoft365Errors.ClassifyIdentityError("invalid_request",
				"AADSTS50011: The redirect URI does not match."));
			Assert.AreEqual(Microsoft365ErrorKind.Consent,Microsoft365Errors.ClassifyIdentityError("invalid_grant","AADSTS65001: The user or administrator "
				+"has not consented."));
			Assert.AreEqual(Microsoft365ErrorKind.Consent,Microsoft365Errors.ClassifyIdentityError("consent_required",""));
			Assert.AreEqual(Microsoft365ErrorKind.AccessBlocked,Microsoft365Errors.ClassifyIdentityError("access_denied","AADSTS53003: Blocked by CA."));
			Assert.AreEqual(Microsoft365ErrorKind.AccessBlocked,Microsoft365Errors.ClassifyIdentityError("invalid_grant","AADSTS50105: Not assigned."));
			Assert.AreEqual(Microsoft365ErrorKind.NeedsSignIn,Microsoft365Errors.ClassifyIdentityError("invalid_grant","AADSTS50173: Grant expired."));
			Assert.AreEqual(Microsoft365ErrorKind.NeedsSignIn,Microsoft365Errors.ClassifyIdentityError("invalid_grant","AADSTS700082: Token expired."));
			Assert.AreEqual(Microsoft365ErrorKind.NeedsSignIn,Microsoft365Errors.ClassifyIdentityError("interaction_required",""));
			Assert.AreEqual(Microsoft365ErrorKind.Cancelled,Microsoft365Errors.ClassifyIdentityError("authentication_canceled",""));
			Assert.AreEqual(Microsoft365ErrorKind.Unreachable,Microsoft365Errors.ClassifyIdentityError("request_timeout",""));
			Assert.AreEqual(Microsoft365ErrorKind.General,Microsoft365Errors.ClassifyIdentityError("something_new","AADSTS1: x"));
			StringAssert.Contains(Microsoft365Errors.FromIdentityError("invalid_request","AADSTS50011: x",400,"http://localhost:5000").Message,
				"http://localhost:5000");
			Assert.AreEqual(Microsoft365Errors.MessageAppRegistration,Microsoft365Errors.FromIdentityError("","AADSTS700016: x").Message);
		}

		[TestMethod]
		public void IdentityErrors_NeedsSignInIsAnAuthException() {
			Microsoft365Exception ex=Microsoft365Errors.FromIdentityError("invalid_grant","AADSTS50173: The provided grant has expired.",400);
			Assert.IsInstanceOfType(ex,typeof(Microsoft365AuthException));
			Assert.IsTrue(ex.IsAuthError);
			Assert.AreEqual(400,ex.HttpStatus);
			StringAssert.Contains(ex.GraphErrorCode,"AADSTS50173");
			Assert.IsFalse(Microsoft365Errors.FromIdentityError("request_timeout","").IsAuthError);
		}

		[TestMethod]
		public void GraphErrors_MapAndKeepTheLegacyInnerMessage() {
			Microsoft365Exception ex=Microsoft365Errors.FromGraphError(401,"InvalidAuthenticationToken","Access token has expired.","req-1");
			Assert.IsTrue(ex.IsAuthError);
			Assert.AreEqual("req-1",ex.RequestId);
			Assert.IsNotNull(ex.InnerException,"For one release the inner exception stays for old string checks.");
			Assert.IsTrue(ex.InnerException.Message.StartsWith("InvalidAuthenticationToken"));
			Assert.AreEqual(Microsoft365ErrorKind.MissingPermission,Microsoft365Errors.FromGraphError(403,"ErrorAccessDenied","","").Kind);
			Assert.IsTrue(Microsoft365Errors.FromGraphError(403,"ErrorAccessDenied","","").IsAuthError);
			Assert.AreEqual(Microsoft365ErrorKind.NoMailbox,Microsoft365Errors.FromGraphError(404,"MailboxNotEnabledForRESTAPI","","").Kind);
			Assert.AreEqual(Microsoft365ErrorKind.TooLarge,Microsoft365Errors.FromGraphError(413,"","","").Kind);
			Assert.AreEqual(Microsoft365ErrorKind.TooLarge,Microsoft365Errors.FromGraphError(400,"ErrorMessageSizeExceeded","","").Kind);
			Assert.AreEqual(Microsoft365ErrorKind.General,Microsoft365Errors.FromGraphError(400,"ErrorInvalidRecipients","patient@example.test","").Kind);
			Assert.IsFalse(Microsoft365Errors.FromGraphError(400,"ErrorInvalidRecipients","patient@example.test","").Message.Contains("patient@"),
				"Graph's own message can quote addresses; it is never copied.");
		}

		[TestMethod]
		public void SanitizeDetails_DropsTokenLikeRuns() {
			string details="AADSTS53003: Access has been blocked. Trace ID: "+new string('a',80)+" Correlation ID: x";
			string sanitized=Microsoft365Errors.SanitizeDetails(details);
			Assert.IsFalse(sanitized.Contains(new string('a',80)));
			StringAssert.Contains(sanitized,"AADSTS53003");
		}

		[TestMethod]
		public void Profile_MatchesMailOrUpnIgnoringCase() {
			MicrosoftProfile microsoftProfile=new MicrosoftProfile();
			microsoftProfile.Mail="Front.Desk@Example-Dental.test";
			microsoftProfile.UserPrincipalName="fd@exampledental.onmicrosoft.test";
			Assert.IsTrue(microsoftProfile.Matches("front.desk@example-dental.test"));
			Assert.IsTrue(microsoftProfile.Matches("FD@exampledental.onmicrosoft.test"));
			Assert.IsTrue(microsoftProfile.Matches(""),"A blank user name takes the signed-in account.");
			Assert.IsFalse(microsoftProfile.Matches("billing@example-dental.test"));
			microsoftProfile.Mail="";
			Assert.AreEqual("fd@exampledental.onmicrosoft.test",microsoftProfile.GetAddress());
		}

		[TestMethod]
		public void Scopes_LeastPrivilege() {
			CollectionAssert.AreEqual(new[] { "User.Read","Mail.Send" },Microsoft365Scopes.GetSignInScopes(false));
			CollectionAssert.AreEqual(new[] { "User.Read","Mail.Send","Mail.Read" },Microsoft365Scopes.GetSignInScopes(true));
			CollectionAssert.DoesNotContain(new List<string>(Microsoft365Scopes.ListSend),"Mail.ReadWrite");
		}

		[TestMethod]
		public void BrowserPages_HaveNoBracesOrExternalAssets() {
			//MSAL formats the error page with string.Format; a brace would throw. The pages load nothing from anywhere.
			foreach(bool isError in new[] { false,true }) {
				string html=Microsoft365Session.GetBrowserPageHtml(isError);
				Assert.IsFalse(html.Contains("{") || html.Contains("}"));
				Assert.IsFalse(html.Contains("http"),"No links or assets.");
				Assert.IsFalse(html.Contains("<script"));
			}
		}
	}
}
