// Vendor-direct Microsoft 365 email: the in-repo replacement for the prebuilt Bridges.dll MicrosoftApiConnector, under the office's own
// Microsoft Entra app registration. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodeBase;
using CodeBase.VendorDirect;
using MimeKit;
using OpenDentBusiness.Email;

namespace OpenDentBusiness.Microsoft365 {
	//OpenDentBusiness.Task (the task list table type) would hide System.Threading.Tasks.Task otherwise.
	using Task=System.Threading.Tasks.Task;

	///<summary>The surface the email code called on Bridges.dll, with the same names, now talking to Microsoft directly with the office's own app
	///registration (Setup > Program Links > Microsoft 365 email). Callers swap "using Bridges;" for "using OpenDentBusiness.Microsoft365;".
	///Differences from the Bridges.dll version (docs/specs/vendor-direct/microsoft-365.md, 4.2):
	///- MicrosoftTokenHelper.AccessToken is the account marker, not a bearer token. AccountInfo is the osd1$ MSAL cache.
	///- GetAccessToken: blank accountInfo signs in interactively (UI callers only); non-blank is silent only and never prompts.
	///- The Graph calls take a real bearer token (Microsoft365Session.GetAccessToken) and throw Microsoft365Exception; an expired token gives
	///  Microsoft365AuthException (IsAuthError), whose InnerException message still starts with "InvalidAuthenticationToken".
	///- RetrieveMessages returns MicrosoftMessageRef (Id, ReceivedDateTime) instead of Microsoft.Graph.Message.
	///Every call runs on the client workstation; the middle tier server never calls Microsoft.</summary>
	public static class MicrosoftApiConnector {
		///<summary>The first inbox download looks back this far.</summary>
		public static readonly TimeSpan InboxDefaultLookBack=TimeSpan.FromDays(30);

		#region Sign-in
		///<summary>Blank accountInfo: interactive sign-in in the system browser (or device code), asking for User.Read and Mail.Send. Non-blank: a
		///silent token from that cache, never a prompt. Never throws. A blank AccessToken with a blank ErrorMessage means the user cancelled.</summary>
		public static Task<MicrosoftTokenHelper> GetAccessToken(string emailUsername,string accountInfo) {
			return GetAccessToken(emailUsername,accountInfo,false,CancellationToken.None);
		}

		///<summary>As GetAccessToken(emailUsername,accountInfo), also asking for Mail.Read when includeMailRead (the address downloads its inbox).
		///cancellationToken is the progress window's Cancel; the sign-in also gives up after 5 minutes.</summary>
		public static async Task<MicrosoftTokenHelper> GetAccessToken(string emailUsername,string accountInfo,bool includeMailRead,
			CancellationToken cancellationToken)
		{
			Meth.NoCheckMiddleTierRole();
			MicrosoftTokenHelper microsoftTokenHelper=new MicrosoftTokenHelper();
			try {
				Microsoft365Config config=GetConfigForSignIn();
				if(string.IsNullOrEmpty(accountInfo)) {
					Microsoft365SignInResult microsoft365SignInResult=await Microsoft365Session.SignInInteractiveAsync(config,emailUsername,
						Microsoft365Scopes.GetSignInScopes(includeMailRead),cancellationToken).ConfigureAwait(false);
					microsoftTokenHelper.AccessToken=microsoft365SignInResult.AccountMarker;
					microsoftTokenHelper.AccountInfo=microsoft365SignInResult.StoredCache;
					microsoftTokenHelper.EmailAddress=microsoft365SignInResult.Profile.GetAddress();
					return microsoftTokenHelper;
				}
				Microsoft365Session session=new Microsoft365Session(config,new MemoryMicrosoft365CacheStore(accountInfo,""),"connector",emailUsername);
				await session.GetAccessTokenAsync(Microsoft365Scopes.ListSend,false,cancellationToken).ConfigureAwait(false);
				microsoftTokenHelper.AccessToken=session.AccountMarker;
				microsoftTokenHelper.AccountInfo=session.CurrentStoredCache;
				microsoftTokenHelper.EmailAddress=session.AccountUsername;
			}
			catch(Microsoft365Exception ex) when(ex.Kind==Microsoft365ErrorKind.Cancelled) {
				return new MicrosoftTokenHelper();//Cancelled: blank token, blank error, as the callers expect.
			}
			catch(Microsoft365Exception ex) {
				microsoftTokenHelper=new MicrosoftTokenHelper();
				microsoftTokenHelper.ErrorMessage=ex.Message;
				microsoftTokenHelper.ErrorKind=ex.Kind;
			}
			catch(OperationCanceledException) {
				return new MicrosoftTokenHelper();
			}
			return microsoftTokenHelper;
		}

		///<summary>Signs a mailbox out of this program: forgets any session holding that cache. There is no revocation endpoint for public clients;
		///the caller blanks the stored values, which discards the refresh token. Real revocation is "Revoke sessions" in Entra, or the user signing out
		///everywhere. Completes at once with no network call, so the existing caller's .Result on the UI thread can't deadlock.</summary>
		public static Task<MicrosoftTokenHelper> SignOutUser(string emailUsername,string accountInfo) {
			Meth.NoCheckMiddleTierRole();
			try {
				Microsoft365Session.ForgetByStoredCache(accountInfo);
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
			return Task.FromResult(new MicrosoftTokenHelper());
		}

		///<summary>The configuration for an interactive sign-in. Throws NotConfigured with the setup message; no network call.</summary>
		private static Microsoft365Config GetConfigForSignIn() {
			if(!Microsoft365Program.IsEnabled()) {
				throw new Microsoft365Exception(Microsoft365ErrorKind.NotConfigured,Microsoft365Errors.MessageTurnedOff);
			}
			Microsoft365Config config=Microsoft365Program.GetConfig();
			if(!config.IsComplete) {
				throw new Microsoft365Exception(Microsoft365ErrorKind.NotConfigured,Microsoft365Errors.MessageNotConfiguredSetup);
			}
			List<string> listErrors=Microsoft365Program.Validate(config,true);
			if(listErrors.Count>0) {
				throw new Microsoft365Exception(Microsoft365ErrorKind.NotConfigured,listErrors[0]);
			}
			return config;
		}
		#endregion Sign-in

		#region Graph calls with a bearer token
		///<summary>GET /me. Throws Microsoft365Exception.</summary>
		public static MicrosoftProfile GetProfile(string emailUsername,string accessToken) {
			Meth.NoCheckMiddleTierRole();
			ThrowIfTurnedOff();
			return VendorHttp.RunSync(() => Microsoft365GraphClient.Default.GetProfileAsync(emailUsername,GetFixedTokenProvider(accessToken)));
		}

		///<summary>The inbox's messages from the last 30 days, newest first, with immutable IDs. Throws Microsoft365Exception.</summary>
		public static List<MicrosoftMessageRef> RetrieveMessages(string emailUsername,string accessToken) {
			return RetrieveMessages(emailUsername,accessToken,DateTime.UtcNow.Subtract(InboxDefaultLookBack));
		}

		///<summary>The inbox's messages received at or after sinceUtc, newest first, with immutable IDs. Throws Microsoft365Exception.</summary>
		public static List<MicrosoftMessageRef> RetrieveMessages(string emailUsername,string accessToken,DateTime sinceUtc) {
			Meth.NoCheckMiddleTierRole();
			ThrowIfTurnedOff();
			return VendorHttp.RunSync(() => Microsoft365GraphClient.Default.ListInboxAsync(emailUsername,GetFixedTokenProvider(accessToken),sinceUtc));
		}

		///<summary>One message as MIME. messageId is case-sensitive. Throws Microsoft365Exception.</summary>
		public static MimeMessage GetMIMEMessage(string emailUsername,string accessToken,string messageId) {
			Meth.NoCheckMiddleTierRole();
			ThrowIfTurnedOff();
			return VendorHttp.RunSync(() => Microsoft365GraphClient.Default.GetMimeAsync(emailUsername,GetFixedTokenProvider(accessToken),messageId));
		}

		///<summary>Sends message as the signed-in user, built with the same MIME builder as Gmail (SendEmails.CreateMIMEMsg). Throws
		///Microsoft365Exception; SendOutcomeUnknown means it may have been sent (check Sent Items), and it is never retried automatically.</summary>
		public static void SendEmail(string emailUsername,string accessToken,BasicEmailMessage message) {
			Meth.NoCheckMiddleTierRole();
			ThrowIfTurnedOff();
			if(message==null) {
				throw new ArgumentNullException(nameof(message));
			}
			BasicEmailAddress basicEmailAddress=new BasicEmailAddress();
			basicEmailAddress.EmailUsername=emailUsername;
			MimeMessage mimeMessage=OpenDentBusiness.Email.SendEmail.CreateMIMEMsg(basicEmailAddress,message);
			SendMime(emailUsername,accessToken,mimeMessage);
		}

		///<summary>Sends a MIME message as the signed-in user. Throws Microsoft365Exception.</summary>
		public static void SendMime(string emailUsername,string accessToken,MimeMessage mimeMessage) {
			Meth.NoCheckMiddleTierRole();
			ThrowIfTurnedOff();
			VendorHttp.RunSync(() => Microsoft365GraphClient.Default.SendMimeAsync(emailUsername,GetFixedTokenProvider(accessToken),mimeMessage));
		}

		///<summary>A token provider for a token the caller already has. A 401 can't be fixed here, so the caller gets Microsoft365AuthException and
		///refreshes through Microsoft365Session, then calls again.</summary>
		public static Microsoft365TokenProvider GetFixedTokenProvider(string accessToken) {
			return (listScopes,forceRefresh,cancellationToken) => {
				if(forceRefresh || string.IsNullOrEmpty(accessToken)) {
					return Task.FromException<string>(new Microsoft365AuthException(Microsoft365ErrorKind.NeedsSignIn,Microsoft365Errors.MessageNeedsSignIn,
						401,"InvalidAuthenticationToken"));
				}
				return Task.FromResult(accessToken);
			};
		}

		private static void ThrowIfTurnedOff() {
			if(!Microsoft365Program.IsEnabled()) {
				throw new Microsoft365Exception(Microsoft365ErrorKind.NotConfigured,Microsoft365Errors.MessageTurnedOff);
			}
		}
		#endregion Graph calls with a bearer token
	}
}
