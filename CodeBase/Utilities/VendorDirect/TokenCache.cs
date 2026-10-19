// Vendor-direct integrations: in-memory access tokens, single-flight refresh, one retry on 401, and refresh-token persistence strategies.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBase.VendorDirect {
	///<summary>An access token as the vendor issued it. Lives in memory only: never written to the database, logs or disk.</summary>
	public sealed class OAuthAccessToken {
		///<summary>The bearer value.</summary>
		public string Value;
		///<summary>"Bearer" unless the vendor says otherwise.</summary>
		public string TokenType="Bearer";
		///<summary>When the token stops working, UTC.</summary>
		public DateTime ExpiresUtc;
		///<summary>When it was obtained, UTC. Used to size the refresh margin for short-lived tokens.</summary>
		public DateTime IssuedUtc;
		///<summary>A refresh token that came back with this one, or null. Rotating vendors (QuickBooks Online) issue a new one on each refresh.</summary>
		public string RefreshToken;
		///<summary>The granted scopes, if the vendor returned them.</summary>
		public string Scope;
		///<summary>An OpenID Connect ID token, if the vendor returned one.</summary>
		public string IdToken;
	}

	///<summary>The vendor definitely refused the credentials: invalid_grant, invalid_client, or any 400/401 from the token endpoint. The office has
	///to sign in again or fix the setup. A network error is never this exception.</summary>
	public class TokenRejectedException : Exception {
		///<summary>The OAuth error code, e.g. "invalid_grant", or "".</summary>
		public string Error { get; }
		///<summary>The HTTP status from the token endpoint, or 0.</summary>
		public int StatusCode { get; }

		public TokenRejectedException(string error,string message,int statusCode=0) : base(message) {
			Error=error??"";
			StatusCode=statusCode;
		}
	}

	///<summary>Thrown at once, with no network call, after a token for this configuration was rejected earlier in this session.</summary>
	public class ReauthorizationRequiredException : Exception {
		public ReauthorizationRequiredException(string message) : base(message) {
		}
	}

	///<summary>Caches one access token per vendor configuration, in memory only. Key it with Fingerprint over the client ID, the environment and the
	///encrypted refresh token or secret, so a changed setting gets a fresh entry. Refreshes are single-flight: a burst of callers makes one request.
	///After a definite rejection the entry is marked NeedsReauthorization: every later call fails at once and the user sees one message per session.</summary>
	public sealed class TokenCache {
		///<summary>Refresh this long before expiry. 5 minutes by default; PostcardMania's 15-minute tokens use 60 seconds.</summary>
		public static readonly TimeSpan DefaultRefreshMargin=TimeSpan.FromMinutes(5);
		private static readonly ConcurrentDictionary<string,TokenCache> _dictCaches=new ConcurrentDictionary<string,TokenCache>();

		private readonly SemaphoreSlim _semaphore=new SemaphoreSlim(1,1);
		private readonly Func<DateTime> _funcUtcNow;
		private OAuthAccessToken _token;
		private volatile bool _needsReauthorization;
		private int _hasShownReauthorizationMessage;

		public TokenCache(TimeSpan? refreshMargin=null,Func<DateTime> funcUtcNow=null) {
			RefreshMargin=refreshMargin ?? DefaultRefreshMargin;
			_funcUtcNow=funcUtcNow ?? (() => DateTime.UtcNow);
		}

		///<summary>How long before expiry a token is refreshed. Halved for tokens that live less than twice this long.</summary>
		public TimeSpan RefreshMargin { get; }

		///<summary>True after a definite rejection. Cleared by ClearReauthorization after a successful sign-in.</summary>
		public bool NeedsReauthorization {
			get {
				return _needsReauthorization;
			}
		}

		///<summary>The process-wide cache for vendorName and configFingerprint, created on first use.</summary>
		public static TokenCache For(string vendorName,string configFingerprint,TimeSpan? refreshMargin=null) {
			return _dictCaches.GetOrAdd(vendorName+"|"+configFingerprint,x => new TokenCache(refreshMargin));
		}

		///<summary>Forgets every cache, for tests.</summary>
		public static void ClearAll() {
			_dictCaches.Clear();
		}

		///<summary>A SHA-256 hex fingerprint of the configuration values. Each part is length-prefixed, so ("ab","c") and ("a","bc") differ.
		///Pass the encrypted (osd1$) secret or refresh token, never the plain one.</summary>
		public static string Fingerprint(params string[] arrayParts) {
			StringBuilder stringBuilder=new StringBuilder();
			for(int i=0;i<arrayParts.Length;i++) {
				string part=arrayParts[i]??"";
				stringBuilder.Append(part.Length).Append(':').Append(part).Append('|');
			}
			using(SHA256 sha=SHA256.Create()) {
				byte[] hash=sha.ComputeHash(Encoding.UTF8.GetBytes(stringBuilder.ToString()));
				StringBuilder stringBuilderHex=new StringBuilder(hash.Length*2);
				for(int i=0;i<hash.Length;i++) {
					stringBuilderHex.Append(hash[i].ToString("x2"));
				}
				return stringBuilderHex.ToString();
			}
		}

		///<summary>Returns the cached token while it has more than the refresh margin left; otherwise calls fetch, once, however many callers are
		///waiting. fetch throws TokenRejectedException for a definite rejection, which marks NeedsReauthorization; any other exception (network)
		///leaves the cache usable for the next try. Throws ReauthorizationRequiredException at once when NeedsReauthorization is set.</summary>
		public async Task<OAuthAccessToken> GetAsync(Func<CancellationToken,Task<OAuthAccessToken>> fetch,
			CancellationToken cancellationToken=default(CancellationToken))
		{
			ThrowIfNeedsReauthorization();
			OAuthAccessToken token=_token;
			if(IsUsable(token)) {
				return token;
			}
			await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
			try {
				ThrowIfNeedsReauthorization();
				token=_token;
				if(IsUsable(token)) {
					return token;//Another caller refreshed while this one waited.
				}
				try {
					token=await fetch(cancellationToken).ConfigureAwait(false);
				}
				catch(TokenRejectedException) {
					_token=null;
					_needsReauthorization=true;
					throw;
				}
				if(token==null || string.IsNullOrEmpty(token.Value)) {
					throw new InvalidOperationException("The vendor returned no access token.");
				}
				if(token.IssuedUtc==default(DateTime)) {
					token.IssuedUtc=_funcUtcNow();
				}
				_token=token;
				return token;
			}
			finally {
				_semaphore.Release();
			}
		}

		///<summary>Drops the cached token so the next GetAsync fetches a new one.</summary>
		public void Invalidate() {
			_token=null;
		}

		///<summary>Drops the cached token only if it is still tokenStale, so several callers that all got a 401 with the same token cause one refresh.</summary>
		public void InvalidateIfCurrent(OAuthAccessToken tokenStale) {
			Interlocked.CompareExchange(ref _token,null,tokenStale);
		}

		///<summary>Marks the configuration as needing a new sign-in, e.g. after MSAL's "UI required".</summary>
		public void MarkNeedsReauthorization() {
			_token=null;
			_needsReauthorization=true;
		}

		///<summary>Call after a successful interactive sign-in.</summary>
		public void ClearReauthorization() {
			_needsReauthorization=false;
			Interlocked.Exchange(ref _hasShownReauthorizationMessage,0);
		}

		///<summary>True the first time it is called after NeedsReauthorization was set, false after that, so the user sees one message per session.</summary>
		public bool ShouldShowReauthorizationMessage() {
			if(!_needsReauthorization) {
				return false;
			}
			return Interlocked.Exchange(ref _hasShownReauthorizationMessage,1)==0;
		}

		///<summary>Sends the request built by createRequest with "Authorization: Bearer {token}". On HTTP 401 it invalidates the token, refreshes
		///once and sends a new request once. A second 401 is returned to the caller as an authorization failure. createRequest is called once per
		///attempt, because a request can't be sent twice.</summary>
		public async Task<HttpResponseMessage> SendWithAuthRetryAsync(HttpClient httpClient,Func<HttpRequestMessage> createRequest,
			Func<CancellationToken,Task<OAuthAccessToken>> fetch,CancellationToken cancellationToken=default(CancellationToken))
		{
			OAuthAccessToken token=await GetAsync(fetch,cancellationToken).ConfigureAwait(false);
			HttpResponseMessage response=await SendWithTokenAsync(httpClient,createRequest,token,cancellationToken).ConfigureAwait(false);
			if(response.StatusCode!=HttpStatusCode.Unauthorized) {
				return response;
			}
			response.Dispose();
			InvalidateIfCurrent(token);
			token=await GetAsync(fetch,cancellationToken).ConfigureAwait(false);
			return await SendWithTokenAsync(httpClient,createRequest,token,cancellationToken).ConfigureAwait(false);
		}

		private static Task<HttpResponseMessage> SendWithTokenAsync(HttpClient httpClient,Func<HttpRequestMessage> createRequest,OAuthAccessToken token,
			CancellationToken cancellationToken)
		{
			HttpRequestMessage request=createRequest();
			request.Headers.Authorization=new AuthenticationHeaderValue(string.IsNullOrEmpty(token.TokenType) ? "Bearer" : token.TokenType,token.Value);
			return httpClient.SendAsync(request,cancellationToken);
		}

		private bool IsUsable(OAuthAccessToken token) {
			if(token==null) {
				return false;
			}
			TimeSpan margin=RefreshMargin;
			TimeSpan lifetime=token.ExpiresUtc-token.IssuedUtc;
			if(token.IssuedUtc!=default(DateTime) && lifetime>TimeSpan.Zero && lifetime<TimeSpan.FromTicks(margin.Ticks*2)) {
				margin=TimeSpan.FromTicks(lifetime.Ticks/2);
			}
			return token.ExpiresUtc-margin>_funcUtcNow();
		}

		private void ThrowIfNeedsReauthorization() {
			if(_needsReauthorization) {
				throw new ReauthorizationRequiredException("The vendor refused the saved sign-in. Sign in again from the integration's setup window.");
			}
		}
	}

	///<summary>Where a refresh token is kept (programproperty, or an emailaddress column), as plain text to the caller. The implementation encrypts
	///with SecretValue. TryReplace is a compare-and-swap: it writes newValue only if the stored value is still expectedCurrent.</summary>
	public interface IRefreshTokenStore {
		///<summary>The current refresh token, read from the database (not a cache), or "" if none or unreadable.</summary>
		string ReadCurrent();
		///<summary>Replaces expectedCurrent with newValue. Returns false if another workstation changed it first.</summary>
		bool TryReplace(string expectedCurrent,string newValue);
	}

	///<summary>How each vendor keeps its refresh token when it refreshes.
	///Rotating (QuickBooks Online): read the current value before refreshing, write the new one with compare-and-swap, and on a rejection re-read
	///once in case another workstation rotated it.
	///Non-rotating (Gmail): never write on refresh.
	///MSAL cache (Microsoft 365): write at most once a day per process, or when the account changes (WriteThrottle).
	///Client credentials (USPS, PostcardMania, DoseSpot, CareCredit): "refresh" just requests a new token; nothing is stored.</summary>
	public static class RefreshTokenStrategy {
		///<summary>The rotating strategy. redeem exchanges a refresh token for an access token (and usually a new refresh token).</summary>
		public static async Task<OAuthAccessToken> RefreshRotatingAsync(IRefreshTokenStore store,
			Func<string,CancellationToken,Task<OAuthAccessToken>> redeem,CancellationToken cancellationToken=default(CancellationToken))
		{
			string refreshToken=store.ReadCurrent();
			if(string.IsNullOrEmpty(refreshToken)) {
				throw new TokenRejectedException("no_refresh_token","Not signed in.");
			}
			OAuthAccessToken token;
			try {
				token=await redeem(refreshToken,cancellationToken).ConfigureAwait(false);
			}
			catch(TokenRejectedException) {
				string refreshTokenReread=store.ReadCurrent();
				if(string.IsNullOrEmpty(refreshTokenReread) || refreshTokenReread==refreshToken) {
					throw;
				}
				refreshToken=refreshTokenReread;//Another workstation rotated it. Try once with the new one.
				token=await redeem(refreshToken,cancellationToken).ConfigureAwait(false);
			}
			if(!string.IsNullOrEmpty(token.RefreshToken) && token.RefreshToken!=refreshToken) {
				//If this fails, another workstation stored a newer token first. Ours still works for this access token; theirs is kept.
				store.TryReplace(refreshToken,token.RefreshToken);
			}
			return token;
		}

		///<summary>The non-rotating strategy: reads the stored refresh token and redeems it. Never writes.</summary>
		public static async Task<OAuthAccessToken> RefreshNonRotatingAsync(IRefreshTokenStore store,
			Func<string,CancellationToken,Task<OAuthAccessToken>> redeem,CancellationToken cancellationToken=default(CancellationToken))
		{
			string refreshToken=store.ReadCurrent();
			if(string.IsNullOrEmpty(refreshToken)) {
				throw new TokenRejectedException("no_refresh_token","Not signed in.");
			}
			return await redeem(refreshToken,cancellationToken).ConfigureAwait(false);
		}
	}

	///<summary>Limits how often a process writes something back to the database (the Microsoft 365 MSAL cache): at most once per interval per key,
	///or at once when the value's identity changes (another account).</summary>
	public sealed class WriteThrottle {
		private readonly ConcurrentDictionary<string,Tuple<DateTime,string>> _dictLastWrites=new ConcurrentDictionary<string,Tuple<DateTime,string>>();
		private readonly TimeSpan _interval;
		private readonly Func<DateTime> _funcUtcNow;

		public WriteThrottle(TimeSpan interval,Func<DateTime> funcUtcNow=null) {
			_interval=interval;
			_funcUtcNow=funcUtcNow ?? (() => DateTime.UtcNow);
		}

		///<summary>True if the caller should write now; records the write. identity is e.g. the account ID.</summary>
		public bool ShouldWrite(string key,string identity) {
			DateTime now=_funcUtcNow();
			Tuple<DateTime,string> last;
			if(_dictLastWrites.TryGetValue(key,out last) && last.Item2==identity && now-last.Item1<_interval) {
				return false;
			}
			_dictLastWrites[key]=Tuple.Create(now,identity);
			return true;
		}
	}
}
