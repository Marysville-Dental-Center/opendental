// Tests for the EMAIL-UI package (docs/specs/vendor-direct/README.md, 3.4): the email flows on top of the in-repo Gmail and Microsoft 365 clients.
// No database and no network. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.Email;
using OpenDentBusiness.Microsoft365;

namespace VendorDirectTests.Email {
	[TestClass]
	public class EmailUiTests {
		[TestCleanup]
		public void TestCleanup() {
			EmailMessages.ClearGmailSendingLimits();
		}

		[TestMethod]
		public void ODEmailAddressToBasic_OAuthAddressesCarryNoStoredTokens() {
			foreach(OAuthType oAuthType in new[] { OAuthType.Google,OAuthType.Microsoft }) {
				EmailAddress emailAddress=new EmailAddress();
				emailAddress.EmailUsername="frontdesk@example-dental.test";
				emailAddress.EmailPassword="";
				emailAddress.AuthenticationType=oAuthType;
				emailAddress.AccessToken="account-marker-or-legacy-token";
				emailAddress.RefreshToken=SecretsTestHelper.Protect("synthetic refresh token");
				BasicEmailAddress basicEmailAddress=EmailMessages.ODEmailAddressToBasic(emailAddress);
				Assert.AreEqual("",basicEmailAddress.AccessToken,oAuthType+": the sender sets a fresh bearer token; the stored column is never sent.");
				Assert.AreEqual("",basicEmailAddress.RefreshToken,oAuthType+": the email layer never sees the refresh token or MSAL cache.");
				Assert.AreEqual((BasicOAuthType)oAuthType,basicEmailAddress.AuthenticationType);
				Assert.AreEqual("frontdesk@example-dental.test",basicEmailAddress.EmailUsername);
			}
			EmailAddress emailAddressPassword=new EmailAddress();
			emailAddressPassword.EmailUsername="billing@example-dental.test";
			emailAddressPassword.EmailPassword="";
			emailAddressPassword.AuthenticationType=OAuthType.None;
			emailAddressPassword.AccessToken="";
			emailAddressPassword.RefreshToken="";
			Assert.AreEqual(BasicOAuthType.None,EmailMessages.ODEmailAddressToBasic(emailAddressPassword).AuthenticationType);
		}

		[TestMethod]
		public void GmailSendingLimit_ReadsGooglesRetryTime() {
			DateTime utcNow=new DateTime(2026,10,19,14,0,0,DateTimeKind.Utc);
			DateTime dateTimeUntil=EmailMessages.GetGmailSendingLimitUntil("User-rate limit exceeded.  Retry after 2026-10-19T15:04:05.123Z",utcNow);
			Assert.AreEqual(new DateTime(2026,10,19,15,4,5,123,DateTimeKind.Utc),dateTimeUntil.ToUniversalTime());
			Assert.AreEqual(utcNow.AddHours(1),EmailMessages.GetGmailSendingLimitUntil("Daily user sending limit exceeded.",utcNow),"No time: one hour.");
			Assert.AreEqual(utcNow.AddHours(1),EmailMessages.GetGmailSendingLimitUntil(null,utcNow));
			Assert.AreEqual(utcNow.AddHours(1),EmailMessages.GetGmailSendingLimitUntil("Retry after 2026-10-19T13:00:00Z",utcNow),"A past time is ignored.");
			Assert.AreEqual(utcNow.AddHours(1),EmailMessages.GetGmailSendingLimitUntil("Retry after 2026-10-22T13:00:00Z",utcNow),
				"Gmail never holds a mailbox for more than 24 hours.");
		}

		[TestMethod]
		public void GmailSendingLimit_HoldsTheAddressUntilItLifts() {
			DateTime utcNow=new DateTime(2026,10,19,14,0,0,DateTimeKind.Utc);
			DateTime dateTimeUntil;
			Assert.IsFalse(EmailMessages.IsGmailSendingLimited(41,utcNow,out dateTimeUntil));
			EmailMessages.MarkGmailSendingLimit(41,utcNow.AddMinutes(30));
			Assert.IsTrue(EmailMessages.IsGmailSendingLimited(41,utcNow.AddMinutes(10),out dateTimeUntil),"The rest of a batch fails at once.");
			Assert.AreEqual(utcNow.AddMinutes(30),dateTimeUntil);
			Assert.IsFalse(EmailMessages.IsGmailSendingLimited(42,utcNow.AddMinutes(10),out dateTimeUntil),"Other addresses carry on.");
			Assert.IsFalse(EmailMessages.IsGmailSendingLimited(41,utcNow.AddMinutes(31),out dateTimeUntil),"Lifted.");
			Assert.IsFalse(EmailMessages.IsGmailSendingLimited(41,utcNow,out dateTimeUntil),"An expired mark is forgotten.");
		}

		[TestMethod]
		public void FindException_WalksInnerAndAggregateExceptions() {
			Microsoft365AuthException exAuth=new Microsoft365AuthException(Microsoft365ErrorKind.NeedsSignIn,Microsoft365Errors.MessageNeedsSignIn,401,
				"InvalidAuthenticationToken");
			Exception exWrapped=new ODException("Error sending email",new AggregateException(new InvalidOperationException("other"),exAuth));
			Assert.AreSame(exAuth,EmailMessages.FindException<Microsoft365Exception>(exWrapped));
			Assert.AreSame(exAuth,EmailMessages.FindException<Microsoft365AuthException>(exAuth));
			Assert.IsNull(EmailMessages.FindException<GoogleAuthException>(exWrapped));
			Assert.IsNull(EmailMessages.FindException<Exception>(null));
		}

		[TestMethod]
		public void ToODException_KeepsTheMicrosoftExceptionForTheEmailWindow() {
			Microsoft365AuthException exAuth=new Microsoft365AuthException(Microsoft365ErrorKind.NeedsSignIn,Microsoft365Errors.MessageNeedsSignIn);
			ODException odException=EmailMessages.ToODException(exAuth,"frontdesk@example-dental.test");
			Assert.AreEqual("frontdesk@example-dental.test needs to be re-authenticated. Please sign out and back in to continue.",odException.Message);
			Assert.AreSame(exAuth,odException.InnerException,"FormEmailMessageEdit offers a new sign-in when it finds a Microsoft365AuthException.");
			Microsoft365Exception exUnreachable=new Microsoft365Exception(Microsoft365ErrorKind.Unreachable,Microsoft365Errors.MessageUnreachable);
			Assert.AreEqual(Microsoft365Errors.MessageUnreachable,EmailMessages.ToODException(exUnreachable,"x@example-dental.test").Message);
			Assert.IsFalse(EmailMessages.ToODException(exUnreachable,"x@example-dental.test").Message.Contains("re-authenticated"),
				"A network failure never looks like a revoked sign-in.");
		}

		[TestMethod]
		public void MicrosoftInbox_ComparesStoredIdsExactly() {
			//The old code ran TrimStart("MicrosoftId".ToCharArray()) on stored IDs, which stripped any leading M, i, c, r, o, s, f, t, I or d.
			List<string> listStored=new List<string> { "iAAMkAGI1","AAMkAGI2","dAAMkAGI3" };
			List<string> listListed=new List<string> { "iAAMkAGI1","AAMkAGI2","aamkagi2","dAAMkAGI3","AAMkAGI4","AAMkAGI4","" };
			List<string> listNew=EmailMessages.GetNewMicrosoftMessageIds(listListed,listStored);
			CollectionAssert.AreEqual(new List<string> { "aamkagi2","AAMkAGI4" },listNew,"Exact, case-sensitive, no repeats, no blanks.");
		}

		[TestMethod]
		public void GmailInbox_ComparesStoredIdsExactly() {
			//Gmail IDs are hex. The old TrimStart("GmailId".ToCharArray()) stripped a leading 'a' or 'd', so those messages downloaded again on every poll.
			List<string> listNew=GmailInbox.GetNewMessageIds(new[] { "a1b2c3","d4e5f6","18f2a","GmailId18f2b","18f2c" },
				new[] { "a1b2c3","d4e5f6","GmailId18f2a","18f2b" });
			CollectionAssert.AreEqual(new List<string> { "GmailId18f2b","18f2c" },listNew);
			Assert.AreEqual("in:inbox newer_than:30d",GmailInbox.GetQuery(""),"A blank query never lists the whole mailbox.");
			Assert.AreEqual("in:inbox newer_than:30d is:unread",GmailInbox.GetQuery("is:unread"));
		}
	}
}
