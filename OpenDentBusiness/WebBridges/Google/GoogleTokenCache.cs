// Vendor-direct integrations: the per-address Gmail access token cache. Access tokens live in memory only; refreshes never write to the database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CodeBase;
using CodeBase.VendorDirect;
using OpenDentBusiness.VendorDirect;

namespace OpenDentBusiness {
	///<summary>One access token per process per Gmail address (google.md 3.4). An entry is keyed by EmailAddressNum plus a SHA-256 fingerprint of the
	///stored (encrypted) refresh token and of the office's OAuth client, so a re-authorized address or a changed client secret gets a fresh entry.
	///Refreshes are single-flight: a statement batch makes one token request. Google doesn't rotate the refresh token, so each workstation refreshes
	///on its own and nothing is written back (the non-rotating strategy). After Google refuses a refresh token (invalid_grant, invalid_client,
	///admin_policy_enforced) the entry is marked: later calls fail at once, and the user sees one message per address per session. A network error
	///never marks it.</summary>
	public static class GoogleTokenCache {
		private static readonly ConcurrentDictionary<string,Entry> _dictEntries=new ConcurrentDictionary<string,Entry>();

		private sealed class Entry {
			public long EmailAddressNum;
			///<summary>A fingerprint of the stored (encrypted) refresh token alone, to find entries by token.</summary>
			public string TokenFingerprint;
			public TokenCache TokenCache=new TokenCache();
			///<summary>The scopes from the most recent token response, space-separated.</summary>
			public volatile string Scope="";
			///<summary>Why Google refused the refresh token, once it has.</summary>
			public volatile string RejectReason="";
		}

		///<summary>An access token for a Google address, refreshing it if it has under 5 minutes left. Blocks a pool thread, never the UI thread's
		///context. Throws GoogleAuthException: NotConfigured (program link turned off, or no client ID), NotAuthorized (blank, legacy or unreadable refresh token, no network
		///call), NeedsReauthorization / InvalidClient / AdminPolicy (Google refused it, now or earlier this session) or Unreachable.</summary>
		public static string GetAccessToken(EmailAddress emailAddress) {
			Meth.NoCheckMiddleTierRole();
			return VendorHttp.RunSync(() => GetAccessTokenAsync(emailAddress));
		}

		///<summary>GetAccessToken, asynchronously.</summary>
		public static Task<string> GetAccessTokenAsync(EmailAddress emailAddress,CancellationToken cancellationToken=default(CancellationToken)) {
			Meth.NoCheckMiddleTierRole();
			if(emailAddress==null) {
				throw new ArgumentNullException(nameof(emailAddress));
			}
			return GetAccessTokenAsync(emailAddress.EmailAddressNum,emailAddress.EmailUsername,emailAddress.RefreshToken,
				GoogleOAuthSettings.GetClientCredentialsOrThrow(GoogleOAuthSettings.MessageNotConfiguredSend),null,cancellationToken);
		}

		///<summary>The core of GetAccessToken, with every input passed in (tests use it with a fake HttpClient). storedRefreshToken is the
		///emailaddress.RefreshToken value (osd1$). httpClient null uses Google.GetHttpClient.</summary>
		public static async Task<string> GetAccessTokenAsync(long emailAddressNum,string mailbox,string storedRefreshToken,
			GoogleClientCredentials googleClientCredentials,HttpClient httpClient,CancellationToken cancellationToken)
		{
			OAuthAccessToken oAuthAccessToken=await GetTokenAsync(emailAddressNum,mailbox,storedRefreshToken,googleClientCredentials,httpClient,
				cancellationToken).ConfigureAwait(false);
			return oAuthAccessToken.Value;
		}

		private static async Task<OAuthAccessToken> GetTokenAsync(long emailAddressNum,string mailbox,string storedRefreshToken,
			GoogleClientCredentials googleClientCredentials,HttpClient httpClient,CancellationToken cancellationToken)
		{
			if(googleClientCredentials==null || !googleClientCredentials.IsConfigured) {
				throw new GoogleAuthException(GoogleAuthReason.NotConfigured,GoogleOAuthSettings.MessageNotConfiguredSend);
			}
			SecretReadResult secretReadResult=GoogleOAuthSettings.ReadRefreshToken(storedRefreshToken);
			if(secretReadResult.Status!=SecretReadStatus.Ok || !secretReadResult.HasValue) {
				throw new GoogleAuthException(GoogleAuthReason.NotAuthorized,GoogleAuthException.GetNeedsAuthorizationMessage(mailbox));
			}
			Entry entry=GetEntry(emailAddressNum,storedRefreshToken,googleClientCredentials.Fingerprint);
			string refreshToken=secretReadResult.PlainText;
			try {
				OAuthAccessToken oAuthAccessToken=await entry.TokenCache.GetAsync(
					ct => Google.RefreshAsync(googleClientCredentials.ClientId,googleClientCredentials.ClientSecret,refreshToken,ct,httpClient),
					cancellationToken).ConfigureAwait(false);
				if(!string.IsNullOrEmpty(oAuthAccessToken.Scope)) {
					entry.Scope=oAuthAccessToken.Scope;
				}
				return oAuthAccessToken;
			}
			catch(GoogleTokenRejectedException ex) {
				entry.RejectReason=ex.Reason.ToString();
				throw new GoogleAuthException(MapRejectReason(ex.Reason),GetRejectedMessage(ex.Reason,mailbox),ex);
			}
			catch(TokenRejectedException ex) {
				throw new GoogleAuthException(GoogleAuthReason.NeedsReauthorization,GoogleAuthException.GetNeedsAuthorizationMessage(mailbox),ex);
			}
			catch(ReauthorizationRequiredException ex) {
				GoogleAuthReason reason;
				if(!Enum.TryParse(entry.RejectReason,out reason)) {
					reason=GoogleAuthReason.NeedsReauthorization;
				}
				throw new GoogleAuthException(MapRejectReason(reason),GetRejectedMessage(reason,mailbox),ex);
			}
			catch(Exception ex) when(Google.IsNetworkFailure(ex,cancellationToken)) {
				throw GoogleAuthException.Unreachable(ex);
			}
		}

		private static GoogleAuthReason MapRejectReason(GoogleAuthReason reason) {
			if(reason==GoogleAuthReason.InvalidClient || reason==GoogleAuthReason.AdminPolicy) {
				return reason;
			}
			return GoogleAuthReason.NeedsReauthorization;
		}

		///<summary>google.md 3.5: a refused refresh asks for re-authorization; invalid_client also points to Program Links.</summary>
		private static string GetRejectedMessage(GoogleAuthReason reason,string mailbox) {
			string message=GoogleAuthException.GetNeedsAuthorizationMessage(mailbox);
			if(reason==GoogleAuthReason.InvalidClient) {
				message+=" Also check the client ID and secret in Setup > Program Links > Gmail API.";
			}
			else if(reason==GoogleAuthReason.AdminPolicy) {
				message=GoogleAuthException.GetRejectionMessage(GoogleAuthReason.AdminPolicy,"",mailbox);
			}
			return message;
		}

		private static string GetKey(long emailAddressNum,string storedFingerprint) {
			return POut.Long(emailAddressNum)+"|"+storedFingerprint;
		}

		private static string GetStoredFingerprint(string storedRefreshToken,string clientFingerprint) {
			return TokenCache.Fingerprint(storedRefreshToken??"",clientFingerprint??"");
		}

		private static Entry GetEntry(long emailAddressNum,string storedRefreshToken,string clientFingerprint) {
			string storedFingerprint=GetStoredFingerprint(storedRefreshToken,clientFingerprint);
			return _dictEntries.GetOrAdd(GetKey(emailAddressNum,storedFingerprint),x => {
				Entry entry=new Entry();
				entry.EmailAddressNum=emailAddressNum;
				entry.TokenFingerprint=TokenCache.Fingerprint(storedRefreshToken??"");
				return entry;
			});
		}

		private static Entry FindEntry(EmailAddress emailAddress) {
			Entry entry;
			_dictEntries.TryGetValue(GetKey(emailAddress.EmailAddressNum,GetStoredFingerprint(emailAddress.RefreshToken,GoogleOAuthSettings.GetClientFingerprint())),
				out entry);
			return entry;
		}

		///<summary>Drops the cached access tokens for an address, so the next call refreshes. Keeps the "needs authorization" mark.</summary>
		public static void Invalidate(long emailAddressNum) {
			foreach(Entry entry in _dictEntries.Values.Where(x => x.EmailAddressNum==emailAddressNum)) {
				entry.TokenCache.Invalidate();
			}
		}

		///<summary>Drops the cached access tokens for one stored refresh token, whatever address it belongs to.</summary>
		public static void InvalidateStored(string storedRefreshToken) {
			string tokenFingerprint=TokenCache.Fingerprint(storedRefreshToken??"");
			foreach(Entry entry in _dictEntries.Values.Where(x => x.TokenFingerprint==tokenFingerprint)) {
				entry.TokenCache.Invalidate();
			}
		}

		///<summary>Stores the access token from a sign-in, so the first send makes no token call, and clears any "needs authorization" mark for this
		///refresh token. storedRefreshToken is the encrypted value that will be saved to emailaddress.RefreshToken.</summary>
		public static void Prime(long emailAddressNum,string storedRefreshToken,GoogleToken googleToken) {
			Meth.NoCheckMiddleTierRole();
			Prime(emailAddressNum,storedRefreshToken,googleToken,GoogleOAuthSettings.GetClientFingerprint());
		}

		///<summary>Prime with the client fingerprint passed in (tests).</summary>
		public static void Prime(long emailAddressNum,string storedRefreshToken,GoogleToken googleToken,string clientFingerprint) {
			if(googleToken==null || string.IsNullOrEmpty(googleToken.AccessToken) || string.IsNullOrEmpty(storedRefreshToken)) {
				return;
			}
			Entry entry=GetEntry(emailAddressNum,storedRefreshToken,clientFingerprint);
			entry.TokenCache.ClearReauthorization();
			entry.RejectReason="";
			entry.Scope=googleToken.Scope??"";
			OAuthAccessToken oAuthAccessToken=googleToken.ToAccessToken(DateTime.UtcNow);
			entry.TokenCache.Invalidate();
			entry.TokenCache.GetAsync(ct => System.Threading.Tasks.Task.FromResult(oAuthAccessToken)).GetAwaiter().GetResult();//Completes at once: nothing awaits.
		}

		///<summary>The scopes granted to the address, from its most recent token response this session, or "" if none yet.</summary>
		public static string GetGrantedScopes(EmailAddress emailAddress) {
			Meth.NoCheckMiddleTierRole();
			if(emailAddress==null) {
				return "";
			}
			return FindEntry(emailAddress)?.Scope??"";
		}

		///<summary>True if the address has to be authorized (again) before it can send: no client ID, a blank, legacy or unreadable refresh token, or
		///Google refused the token earlier in this session. Makes no network call.</summary>
		public static bool NeedsReauthorization(EmailAddress emailAddress) {
			Meth.NoCheckMiddleTierRole();
			if(emailAddress==null) {
				return true;
			}
			if(GoogleOAuthSettings.ReadRefreshToken(emailAddress.RefreshToken).Status!=SecretReadStatus.Ok) {
				return true;
			}
			Entry entry=FindEntry(emailAddress);
			return entry!=null && entry.TokenCache.NeedsReauthorization;
		}

		///<summary>True the first time it is asked after Google refused the address's refresh token, false after that: one message per address per
		///session, not one per message of a batch.</summary>
		public static bool ShouldShowReauthorizationMessage(EmailAddress emailAddress) {
			Meth.NoCheckMiddleTierRole();
			if(emailAddress==null) {
				return false;
			}
			Entry entry=FindEntry(emailAddress);
			return entry!=null && entry.TokenCache.ShouldShowReauthorizationMessage();
		}

		///<summary>Forgets every cached token and mark, for tests.</summary>
		public static void ClearAll() {
			_dictEntries.Clear();
		}
	}
}
