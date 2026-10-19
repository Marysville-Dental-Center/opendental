// Google OAuth 2.0 for the Gmail API: the installed-app flow (system browser, loopback redirect, PKCE S256) straight to Google, with the office's
// own OAuth client. No Open Dental host is involved.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CodeBase;
using CodeBase.VendorDirect;
using Newtonsoft.Json.Linq;
using OpenDentBusiness.VendorDirect;

namespace OpenDentBusiness {
	///<summary>Google's OAuth endpoints for the Gmail API (docs/specs/vendor-direct/google.md, sections 2.3 and 3.2). Every call goes from this
	///workstation to accounts.google.com (browser only), oauth2.googleapis.com (token and revoke) or gmail.googleapis.com. The office's own Desktop
	///client ID and secret come from GoogleOAuthSettings. Tokens, codes, verifiers and id_tokens are never logged or written anywhere except the
	///encrypted refresh token, which the caller stores in emailaddress.RefreshToken.</summary>
	public class Google {
		///<summary>The consent page, opened in the system browser. Google refuses embedded web views.</summary>
		public const string AuthorizeEndpoint="https://accounts.google.com/o/oauth2/v2/auth";
		///<summary>Code exchange and refresh.</summary>
		public const string TokenEndpoint="https://oauth2.googleapis.com/token";
		///<summary>Ends the Google user's whole grant to the Cloud project, for every client in it.</summary>
		public const string RevokeEndpoint="https://oauth2.googleapis.com/revoke";
		///<summary>The loopback redirect path: http://127.0.0.1:{random port}/oauth2. A Desktop client needs no registered redirect URI.</summary>
		public const string RedirectPath="/oauth2";
		///<summary>Token and revoke calls give up after 15 seconds.</summary>
		public static readonly TimeSpan TokenTimeout=TimeSpan.FromSeconds(15);
		///<summary>The only hosts the OAuth client may reach.</summary>
		private static readonly string[] _arrayAllowedHosts={ "oauth2.googleapis.com","accounts.google.com","gmail.googleapis.com" };

		///<summary>The HTTP policy for Google's OAuth endpoints: Google hosts only, HTTPS, 15-second timeout, never retried POSTs, redacted logging.</summary>
		public static VendorHttpOptions GetHttpOptions() {
			VendorHttpOptions options=new VendorHttpOptions();
			options.VendorName=GoogleOAuthSettings.VendorName;
			options.ListAllowedHosts=_arrayAllowedHosts.ToList();
			options.Timeout=TokenTimeout;
			options.Log=x => Logger.WriteLine(x,"VendorDirect");
			return options;
		}

		///<summary>The shared HttpClient for Google's OAuth endpoints.</summary>
		public static HttpClient GetHttpClient() {
			return VendorHttp.GetClient(GetHttpOptions());
		}

		///<summary>The consent page URL (google.md 2.3, step 4). Every value is escaped, login_hint included. access_type=offline asks for a refresh
		///token, and prompt=consent makes Google issue one again on re-authorization. The code challenge is always S256.</summary>
		public static string BuildAuthorizationUrl(string clientId,string redirectUri,IEnumerable<string> listScopes,string state,string codeChallenge,
			string loginHint)
		{
			if(string.IsNullOrWhiteSpace(clientId)) {
				throw new ArgumentException("A client ID is required.",nameof(clientId));
			}
			List<KeyValuePair<string,string>> listParameters=new List<KeyValuePair<string,string>>();
			listParameters.Add(new KeyValuePair<string,string>("client_id",clientId.Trim()));
			listParameters.Add(new KeyValuePair<string,string>("response_type","code"));
			listParameters.Add(new KeyValuePair<string,string>("redirect_uri",redirectUri));
			listParameters.Add(new KeyValuePair<string,string>("scope",string.Join(" ",listScopes)));
			listParameters.Add(new KeyValuePair<string,string>("code_challenge",codeChallenge));
			listParameters.Add(new KeyValuePair<string,string>("code_challenge_method",Pkce.ChallengeMethod));
			listParameters.Add(new KeyValuePair<string,string>("state",state));
			listParameters.Add(new KeyValuePair<string,string>("access_type","offline"));
			listParameters.Add(new KeyValuePair<string,string>("prompt","consent"));
			if(!string.IsNullOrWhiteSpace(loginHint)) {
				listParameters.Add(new KeyValuePair<string,string>("login_hint",loginHint.Trim()));
			}
			return OAuthTokenClient.BuildAuthorizeUrl(new Uri(AuthorizeEndpoint),listParameters);
		}

		///<summary>Exchanges an authorization code for tokens (google.md 2.3, step 6). The client secret is sent in the body whenever one is configured.
		///Returns the tokens with the plain refresh token, the granted scopes and the account's email from the id_token. Throws GoogleAuthException.</summary>
		public static async Task<GoogleToken> ExchangeCodeAsync(string clientId,string clientSecret,string code,string codeVerifier,string redirectUri,
			CancellationToken cancellationToken=default(CancellationToken),HttpClient httpClient=null)
		{
			Meth.NoCheckMiddleTierRole();
			Dictionary<string,string> dictForm=OAuthTokenClient.GetAuthorizationCodeForm(code,redirectUri,clientId,codeVerifier,
				string.IsNullOrEmpty(clientSecret) ? null : clientSecret);
			OAuthAccessToken oAuthAccessToken;
			try {
				oAuthAccessToken=await PostTokenAsync(dictForm,"token (code)",httpClient,cancellationToken).ConfigureAwait(false);
			}
			catch(GoogleTokenRejectedException ex) {
				throw new GoogleAuthException(ex.Reason,GoogleAuthException.GetRejectionMessage(ex.Reason,ex.Error,""),ex);
			}
			catch(Exception ex) when(IsNetworkFailure(ex,cancellationToken)) {
				throw GoogleAuthException.Unreachable(ex);
			}
			return GoogleToken.FromAccessToken(oAuthAccessToken);
		}

		///<summary>Gets a new access token with a refresh token (google.md 2.3, step 8). Google doesn't normally issue a new refresh token here, and
		///nothing is written to the database. Throws GoogleTokenRejectedException (a TokenRejectedException) when Google refuses it, which marks the
		///address as needing authorization; a network failure throws HttpRequestException, which never does. Used by GoogleTokenCache.</summary>
		public static Task<OAuthAccessToken> RefreshAsync(string clientId,string clientSecret,string refreshToken,
			CancellationToken cancellationToken=default(CancellationToken),HttpClient httpClient=null)
		{
			Meth.NoCheckMiddleTierRole();
			Dictionary<string,string> dictForm=OAuthTokenClient.GetRefreshForm(refreshToken,clientId,string.IsNullOrEmpty(clientSecret) ? null : clientSecret);
			return PostTokenAsync(dictForm,"token (refresh)",httpClient,cancellationToken);
		}

		///<summary>Revokes a refresh (or access) token, best effort (google.md 2.3, step 9). Returns true if Google accepted it. A 400 (already revoked)
		///or a network failure returns false. Revoking ends the Google user's whole grant to the project, so callers check ShouldRevoke first.</summary>
		public static async Task<bool> RevokeAsync(string token,CancellationToken cancellationToken=default(CancellationToken),HttpClient httpClient=null) {
			Meth.NoCheckMiddleTierRole();
			if(string.IsNullOrEmpty(token)) {
				return false;
			}
			try {
				using(HttpRequestMessage request=new HttpRequestMessage(HttpMethod.Post,RevokeEndpoint)) {
					request.Content=new FormUrlEncodedContent(new[] { new KeyValuePair<string,string>("token",token) });
					VendorHttp.SetEndpointName(request,"revoke");
					using(HttpResponseMessage response=await (httpClient??GetHttpClient()).SendAsync(request,cancellationToken).ConfigureAwait(false)) {
						return response.IsSuccessStatusCode;
					}
				}
			}
			catch(Exception ex) when(IsNetworkFailure(ex,cancellationToken)) {
				return false;
			}
		}

		///<summary>Posts a token request and reads Google's answer. A 400 or 401 (or a 403 that names an OAuth error) throws GoogleTokenRejectedException
		///with Google's error and error_subtype; other failures throw HttpRequestException. Never puts a body or token in an exception message.</summary>
		private static async Task<OAuthAccessToken> PostTokenAsync(Dictionary<string,string> dictForm,string endpointName,HttpClient httpClient,
			CancellationToken cancellationToken)
		{
			using(HttpRequestMessage request=new HttpRequestMessage(HttpMethod.Post,TokenEndpoint)) {
				request.Content=new FormUrlEncodedContent(dictForm.Where(x => x.Value!=null));
				request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
				VendorHttp.SetEndpointName(request,endpointName);
				using(HttpResponseMessage response=await (httpClient??GetHttpClient()).SendAsync(request,cancellationToken).ConfigureAwait(false)) {
					string body=(response.Content==null) ? "" : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
					int statusCode=(int)response.StatusCode;
					GoogleOAuthError googleOAuthError=GoogleOAuthError.Parse(body);
					if(statusCode==400 || statusCode==401 || (statusCode==403 && googleOAuthError.Error!="")) {
						GoogleAuthReason reason=GoogleAuthException.GetReasonForTokenError(googleOAuthError.Error);
						Logger.WriteLine("Google "+endpointName+": HTTP "+statusCode+" "+googleOAuthError.Error
							+(googleOAuthError.ErrorSubtype=="" ? "" : "/"+googleOAuthError.ErrorSubtype),"VendorDirect");
						throw new GoogleTokenRejectedException(googleOAuthError.Error,googleOAuthError.ErrorSubtype,reason,
							GoogleAuthException.GetRejectionMessage(reason,googleOAuthError.Error,""),statusCode);
					}
					return OAuthTokenClient.ParseResponse(statusCode,body,DateTime.UtcNow);
				}
			}
		}

		///<summary>True for a failure to reach Google (DNS, refused connection, TLS, timeout), as opposed to the caller cancelling.</summary>
		internal static bool IsNetworkFailure(Exception ex,CancellationToken cancellationToken) {
			if(ex is HttpRequestException) {
				return true;
			}
			if(ex is TaskCanceledException && !cancellationToken.IsCancellationRequested) {
				return true;//HttpClient reports its own timeout as a cancellation.
			}
			return false;
		}

		///<summary>Reads the claims of an id_token that came straight from Google's token endpoint on our own TLS connection. The signature isn't
		///checked: the token is used once, locally, to catch the wrong account being chosen, not as proof of identity (google.md 2.3, step 7).
		///Returns null if the value isn't a readable JWT.</summary>
		public static GoogleIdToken ParseIdToken(string idToken) {
			if(string.IsNullOrWhiteSpace(idToken)) {
				return null;
			}
			string[] arrayParts=idToken.Trim().Split('.');
			if(arrayParts.Length<2 || arrayParts[1]=="") {
				return null;
			}
			JObject jObject;
			try {
				string json=Encoding.UTF8.GetString(Base64Url.Decode(arrayParts[1]));
				jObject=JObject.Parse(json);
			}
			catch(Exception ex) {
				ex.DoNothing();
				return null;
			}
			GoogleIdToken googleIdToken=new GoogleIdToken();
			googleIdToken.Email=jObject.Value<string>("email")??"";
			JToken jTokenVerified=jObject["email_verified"];
			//Google sends a boolean; some older tokens carried the string "true".
			googleIdToken.IsEmailVerified=jTokenVerified!=null && string.Equals(jTokenVerified.ToString(),"true",StringComparison.OrdinalIgnoreCase);
			googleIdToken.HostedDomain=jObject.Value<string>("hd")??"";
			return googleIdToken;
		}

		///<summary>The verified email address in an id_token, or "" if there is none or Google says it isn't verified.</summary>
		public static string ParseIdTokenEmail(string idToken) {
			GoogleIdToken googleIdToken=ParseIdToken(idToken);
			if(googleIdToken==null || !googleIdToken.IsEmailVerified) {
				return "";
			}
			return googleIdToken.Email;
		}

		///<summary>True if two mailbox names are the same address, ignoring case and surrounding spaces.</summary>
		public static bool IsSameMailbox(string mailbox1,string mailbox2) {
			if(string.IsNullOrWhiteSpace(mailbox1) || string.IsNullOrWhiteSpace(mailbox2)) {
				return false;
			}
			return string.Equals(mailbox1.Trim(),mailbox2.Trim(),StringComparison.OrdinalIgnoreCase);
		}

		///<summary>False if another Google address in listEmailAddresses (other than emailAddressNumExclude) is the same mailbox. Revoking a token
		///ends that Google user's whole grant to the project, so it would also sign out a mailbox that was authorized correctly (google.md 3.2).</summary>
		public static bool ShouldRevoke(string mailbox,IEnumerable<EmailAddress> listEmailAddresses,long emailAddressNumExclude) {
			if(string.IsNullOrWhiteSpace(mailbox)) {
				return false;
			}
			return !listEmailAddresses.Any(x => x.EmailAddressNum!=emailAddressNumExclude && x.AuthenticationType==OAuthType.Google
				&& IsSameMailbox(x.EmailUsername,mailbox));
		}

		///<summary>ShouldRevoke against every email address in the cache.</summary>
		public static bool ShouldRevoke(string mailbox,long emailAddressNumExclude) {
			Meth.NoCheckMiddleTierRole();
			return ShouldRevoke(mailbox,EmailAddresses.GetDeepCopy(),emailAddressNumExclude);
		}

		///<summary>Signs a Google address out at Google, best effort (google.md 3.2, "Sign-out"): revokes its stored refresh token unless another Google
		///address is the same mailbox, and drops its cached access token. The caller then blanks the address's token fields. Returns true if Google
		///confirmed the revoke. A legacy or unreadable token is never sent.</summary>
		public static async Task<bool> SignOutAsync(long emailAddressNum,string mailbox,string storedRefreshToken,
			CancellationToken cancellationToken=default(CancellationToken))
		{
			Meth.NoCheckMiddleTierRole();
			GoogleTokenCache.Invalidate(emailAddressNum);
			if(!ShouldRevoke(mailbox,emailAddressNum)) {
				return false;
			}
			return await RevokeStoredAsync(storedRefreshToken,null,cancellationToken).ConfigureAwait(false);
		}

		///<summary>Revokes an osd1$ refresh token, best effort. Plain-text (legacy) values were issued to another client and are never sent.</summary>
		public static async Task<bool> RevokeStoredAsync(string storedRefreshToken,HttpClient httpClient=null,
			CancellationToken cancellationToken=default(CancellationToken))
		{
			SecretReadResult secretReadResult=GoogleOAuthSettings.ReadRefreshToken(storedRefreshToken);
			if(secretReadResult.Status!=SecretReadStatus.Ok) {
				return false;
			}
			return await RevokeAsync(secretReadResult.PlainText,cancellationToken,httpClient).ConfigureAwait(false);
		}

		///<summary>Kept until the EMAIL-UI package switches EmailMessages.RefreshGmailToken to GoogleTokenCache. Takes the stored
		///emailaddress.RefreshToken (osd1$) and returns a fresh access token from Google, dropping any cached one first. A blank, plain-text (legacy
		///Open Dental) or unreadable token throws without any network call. Throws GoogleAuthException (an ODException) with a message for the user.</summary>
		public static string MakeRefreshAccessTokenRequest(string storedRefreshToken) {
			Meth.NoCheckMiddleTierRole();
			GoogleClientCredentials googleClientCredentials=GoogleOAuthSettings.GetClientCredentials();
			GoogleTokenCache.InvalidateStored(storedRefreshToken);
			return VendorHttp.RunSync(() => GoogleTokenCache.GetAccessTokenAsync(0,"",storedRefreshToken,googleClientCredentials,null,CancellationToken.None));
		}

		///<summary>One sign-in of one email address: start the loopback listener, open the consent page in the system browser, wait for the redirect,
		///check state, redeem the code with the PKCE verifier and check that the right account was authorized. Use one instance per attempt and
		///Dispose it (or call CloseListener) afterwards; the port is always released.</summary>
		public class AuthorizationRequest : IDisposable {
			private LoopbackListener _loopbackListener;

			///<summary>The office's OAuth client. Null reads it from the GmailApi program link when the request starts.</summary>
			public GoogleClientCredentials ClientCredentials;
			///<summary>The client for the token and revoke calls. Null uses Google.GetHttpClient. Tests pass a fake.</summary>
			public HttpClient HttpClient;
			///<summary>Opens the consent page. Defaults to the system browser. Tests replace it.</summary>
			public Action<string> ActionOpenBrowser=SystemBrowser.Open;
			///<summary>How long to wait for the browser. Five minutes by default.</summary>
			public TimeSpan Timeout=LoopbackListener.DefaultTimeout;
			///<summary>The emailaddress row being authorized, or 0 for a new one. It is left out of the revoke check.</summary>
			public long EmailAddressNum;
			///<summary>Answers "should a stray grant for this mailbox be revoked?" for the wrong-account case. Null uses Google.ShouldRevoke against the
			///email address cache.</summary>
			public Func<string,bool> FuncShouldRevoke;

			///<summary>The redirect_uri sent to Google: http://127.0.0.1:{port}/oauth2. "" before StartListener.</summary>
			public string RedirectUri {
				get {
					if(_loopbackListener==null || _loopbackListener.Port==0) {
						return "";
					}
					return _loopbackListener.RedirectUri;
				}
			}

			///<summary>Binds a free loopback port picked by the operating system (127.0.0.1, port 0). Does nothing if already listening.</summary>
			public void StartListener() {
				if(_loopbackListener!=null) {
					return;
				}
				LoopbackListener loopbackListener=new LoopbackListener(0,RedirectPath,"127.0.0.1");
				try {
					loopbackListener.Start();
				}
				catch(Exception) {
					loopbackListener.Dispose();
					throw;
				}
				_loopbackListener=loopbackListener;
			}

			///<summary>Runs the whole flow for emailAddress and returns its tokens: the plain refresh token (encrypt it with
			///GoogleOAuthSettings.ProtectRefreshToken before saving), the access token, its lifetime, the granted scopes and the authorized account.
			///Throws GoogleAuthException for every expected failure (not configured, cancelled, timed out, declined, wrong account, Google refused,
			///Google unreachable). The caller checks GoogleOAuthSettings.HasScope(token.Scope,ScopeGmailReadOnly) when downloading mail.</summary>
			public async Task<GoogleToken> MakeAccessTokenRequestAsync(string emailAddress,IEnumerable<string> listScopes,
				CancellationToken cancellationToken=default(CancellationToken))
			{
				Meth.NoCheckMiddleTierRole();
				GoogleClientCredentials googleClientCredentials=ClientCredentials??GoogleOAuthSettings.GetClientCredentials();
				if(!googleClientCredentials.IsConfigured) {
					throw new GoogleAuthException(GoogleAuthReason.NotConfigured,GoogleOAuthSettings.MessageNotConfiguredAuthorize);
				}
				try {
					StartListener();
					string redirectUri=_loopbackListener.RedirectUri;
					Pkce pkce=Pkce.Create();
					string state=OAuthState.New();
					string url=BuildAuthorizationUrl(googleClientCredentials.ClientId,redirectUri,listScopes,state,pkce.Challenge,emailAddress);
					ActionOpenBrowser(url);
					OAuthRedirectResult oAuthRedirectResult;
					try {
						oAuthRedirectResult=await _loopbackListener.WaitForRedirectAsync(Timeout,cancellationToken).ConfigureAwait(false);
					}
					catch(OperationCanceledException ex) {
						throw new GoogleAuthException(GoogleAuthReason.Cancelled,"Authorization was cancelled.",ex);
					}
					catch(TimeoutException ex) {
						throw new GoogleAuthException(GoogleAuthReason.TimedOut,"Authorization was cancelled. The browser did not finish within "
							+Math.Round(Timeout.TotalMinutes)+" minutes.",ex);
					}
					if(!oAuthRedirectResult.VerifyState(state)) {
						throw new GoogleAuthException(GoogleAuthReason.StateMismatch,"The browser's answer did not belong to this authorization request. Try again.");
					}
					if(!string.IsNullOrEmpty(oAuthRedirectResult.Error)) {
						GoogleAuthReason reason=GoogleAuthException.GetReasonForAuthorizeError(oAuthRedirectResult.Error);
						throw new GoogleAuthException(reason,GoogleAuthException.GetRejectionMessage(reason,oAuthRedirectResult.Error,emailAddress));
					}
					if(!oAuthRedirectResult.HasCode) {
						throw new GoogleAuthException(GoogleAuthReason.Other,"Google's answer had no authorization code. Try again.");
					}
					GoogleToken googleToken=await ExchangeCodeAsync(googleClientCredentials.ClientId,googleClientCredentials.ClientSecret,oAuthRedirectResult.Code,
						pkce.Verifier,redirectUri,cancellationToken,HttpClient).ConfigureAwait(false);
					await CheckAccountAsync(googleToken,emailAddress,cancellationToken).ConfigureAwait(false);
					return googleToken;
				}
				finally {
					CloseListener();
				}
			}

			///<summary>Refuses tokens for another account than emailAddress, or tokens without send access or a refresh token. A stray grant for the wrong
			///account is revoked only if no other Google address is that mailbox.</summary>
			private async System.Threading.Tasks.Task CheckAccountAsync(GoogleToken googleToken,string emailAddress,CancellationToken cancellationToken) {
				if(googleToken.Email=="") {
					throw new GoogleAuthException(GoogleAuthReason.WrongAccount,"Google did not say which account was authorized, so the tokens were discarded. "
						+"Try again, and allow access to the account's email address.");
				}
				if(!IsSameMailbox(googleToken.Email,emailAddress)) {
					bool shouldRevoke=(FuncShouldRevoke==null) ? ShouldRevoke(googleToken.Email,EmailAddressNum) : FuncShouldRevoke(googleToken.Email);
					if(shouldRevoke) {
						await RevokeAsync(string.IsNullOrEmpty(googleToken.RefreshToken) ? googleToken.AccessToken : googleToken.RefreshToken,
							cancellationToken,HttpClient).ConfigureAwait(false);
					}
					throw new GoogleAuthException(GoogleAuthReason.WrongAccount,"You authorized "+googleToken.Email+", but this address is "+emailAddress+". "
						+"Sign in to Google as "+emailAddress+" and try again.");
				}
				if(!GoogleOAuthSettings.HasScope(googleToken.Scope,GoogleOAuthSettings.ScopeGmailSend)) {
					throw new GoogleAuthException(GoogleAuthReason.ScopeNotGranted,"Send access was not granted, so this address can't send email. "
						+"Try again, and allow sending email on Google's consent page.");
				}
				if(string.IsNullOrEmpty(googleToken.RefreshToken)) {
					throw new GoogleAuthException(GoogleAuthReason.Other,"Google did not issue a refresh token. Remove the app's access in the Google Account's "
						+"third-party connections, then try again.");
				}
			}

			///<summary>Kept until the EMAIL-UI package rewrites the Email Address window's Gmail group. Runs the flow with send and read access and blocks
			///until it ends. Returns the access token and the refresh token already encrypted (osd1$), ready for emailaddress.RefreshToken, and primes
			///the access token cache. Every expected failure comes back in ErrorMessage (with ErrorCode set), as the window expects.</summary>
			public GoogleToken MakeAccessTokenRequest(string emailAddress) {
				Meth.NoCheckMiddleTierRole();
				GoogleToken googleToken;
				try {
					googleToken=VendorHttp.RunSync(() => MakeAccessTokenRequestAsync(emailAddress,GoogleOAuthSettings.GetScopes(isDownloadingMail:true)));
				}
				catch(GoogleAuthException ex) {
					GoogleToken googleTokenError=new GoogleToken("","",ex.Message);
					googleTokenError.ErrorCode=ex.Reason.ToString();
					return googleTokenError;
				}
				string storedRefreshToken;
				if(!GoogleOAuthSettings.ProtectRefreshToken(googleToken.RefreshToken,out storedRefreshToken)) {
					GoogleToken googleTokenError=new GoogleToken("","","The refresh token could not be encrypted, so it was not kept. Try again.");
					googleTokenError.ErrorCode=GoogleAuthReason.Other.ToString();
					return googleTokenError;
				}
				GoogleTokenCache.Prime(EmailAddressNum,storedRefreshToken,googleToken);
				googleToken.RefreshToken=storedRefreshToken;
				return googleToken;
			}

			///<summary>Releases the loopback port. Safe to call more than once.</summary>
			public void CloseListener() {
				LoopbackListener loopbackListener=_loopbackListener;
				_loopbackListener=null;
				loopbackListener?.Dispose();
			}

			public void Dispose() {
				CloseListener();
			}
		}//End AuthorizationRequest class.
	}//End Google class.

	///<summary>Tokens from Google for one email address. The refresh token is plain text when it comes from Google.ExchangeCodeAsync; encrypt it with
	///GoogleOAuthSettings.ProtectRefreshToken before it goes into emailaddress.RefreshToken. Access tokens are kept in memory only.</summary>
	[Serializable()]
	public class GoogleToken {
		public string AccessToken;
		public string RefreshToken;
		///<summary>A message for the user when the request failed, or "".</summary>
		public string ErrorMessage;
		///<summary>Seconds the access token lasts (typically 3599), or 0 if unknown.</summary>
		public long ExpiresInSeconds;
		///<summary>The scopes actually granted, space-separated. The user can untick some on Google's consent page.</summary>
		public string Scope;
		///<summary>The authorized account's verified email address, from the id_token, or "".</summary>
		public string Email;
		///<summary>The GoogleAuthReason name when ErrorMessage is set, or "".</summary>
		public string ErrorCode;

		public GoogleToken(string access,string refresh,string error="") {
			AccessToken=access;
			RefreshToken=refresh;
			ErrorMessage=error;
			Scope="";
			Email="";
			ErrorCode="";
		}

		///<summary>Required for serialization.</summary>
		public GoogleToken() {
			AccessToken="";
			RefreshToken="";
			ErrorMessage="";
			Scope="";
			Email="";
			ErrorCode="";
		}

		///<summary>Copies a token endpoint answer. The id_token itself is not kept; only its verified email.</summary>
		public static GoogleToken FromAccessToken(OAuthAccessToken oAuthAccessToken) {
			GoogleToken googleToken=new GoogleToken(oAuthAccessToken.Value??"",oAuthAccessToken.RefreshToken??"");
			googleToken.Scope=oAuthAccessToken.Scope??"";
			googleToken.Email=Google.ParseIdTokenEmail(oAuthAccessToken.IdToken);
			if(oAuthAccessToken.ExpiresUtc>oAuthAccessToken.IssuedUtc && oAuthAccessToken.IssuedUtc!=default(DateTime)) {
				googleToken.ExpiresInSeconds=(long)(oAuthAccessToken.ExpiresUtc-oAuthAccessToken.IssuedUtc).TotalSeconds;
			}
			return googleToken;
		}

		///<summary>The access token as the token cache holds it, expiring ExpiresInSeconds (3600 if unknown) after utcNow.</summary>
		public OAuthAccessToken ToAccessToken(DateTime utcNow) {
			OAuthAccessToken oAuthAccessToken=new OAuthAccessToken();
			oAuthAccessToken.Value=AccessToken;
			oAuthAccessToken.IssuedUtc=utcNow;
			oAuthAccessToken.ExpiresUtc=utcNow.AddSeconds(ExpiresInSeconds>0 ? ExpiresInSeconds : 3600);
			oAuthAccessToken.Scope=Scope;
			return oAuthAccessToken;
		}
	}

	///<summary>The claims this program reads from a Google id_token.</summary>
	public class GoogleIdToken {
		public string Email="";
		public bool IsEmailVerified;
		///<summary>The Google Workspace domain ("hd"), or "" for a consumer account.</summary>
		public string HostedDomain="";
	}

	///<summary>The error fields of a Google OAuth error body.</summary>
	public class GoogleOAuthError {
		public string Error="";
		public string ErrorDescription="";
		///<summary>Google's refinement of invalid_grant, such as "invalid_rapt" (reauthentication required by a Workspace policy).</summary>
		public string ErrorSubtype="";

		///<summary>Reads error, error_description and error_subtype. Anything unreadable gives blank fields.</summary>
		public static GoogleOAuthError Parse(string body) {
			GoogleOAuthError googleOAuthError=new GoogleOAuthError();
			if(string.IsNullOrWhiteSpace(body)) {
				return googleOAuthError;
			}
			try {
				JObject jObject=JObject.Parse(body);
				JToken jTokenError=jObject["error"];
				if(jTokenError!=null && jTokenError.Type==JTokenType.String) {
					googleOAuthError.Error=jTokenError.ToString();
				}
				googleOAuthError.ErrorDescription=jObject.Value<string>("error_description")??"";
				googleOAuthError.ErrorSubtype=jObject.Value<string>("error_subtype")??"";
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
			return googleOAuthError;
		}
	}

	///<summary>Why a Gmail authorization or token call failed (google.md 3.5).</summary>
	public enum GoogleAuthReason {
		///<summary>Anything not listed below.</summary>
		Other,
		///<summary>The GmailApi program link has no client ID. No network call was made.</summary>
		NotConfigured,
		///<summary>The address has no usable refresh token: blank, legacy (issued to Open Dental's client) or unreadable. No network call was made.</summary>
		NotAuthorized,
		///<summary>Google refused the refresh token (invalid_grant, including invalid_rapt), or it was refused earlier in this session.</summary>
		NeedsReauthorization,
		///<summary>Google refused the OAuth client (invalid_client, unauthorized_client): check the client ID and secret.</summary>
		InvalidClient,
		///<summary>A Workspace administrator restricted the app (admin_policy_enforced).</summary>
		AdminPolicy,
		///<summary>Google couldn't be reached (DNS, connection, TLS, timeout). Never marks the address as needing authorization.</summary>
		Unreachable,
		///<summary>The user cancelled the authorization.</summary>
		Cancelled,
		///<summary>The browser didn't finish within the timeout.</summary>
		TimedOut,
		///<summary>The user declined on Google's consent page (access_denied).</summary>
		AccessDenied,
		///<summary>The redirect's state didn't match the request.</summary>
		StateMismatch,
		///<summary>Another account than the address being set up was authorized, or Google didn't say which.</summary>
		WrongAccount,
		///<summary>The user didn't grant send access.</summary>
		ScopeNotGranted,
	}

	///<summary>A Gmail authorization or token failure, with a message meant for the user. Never carries a token, code or response body.</summary>
	public class GoogleAuthException : ODException {
		public GoogleAuthReason Reason { get; }

		public GoogleAuthException(GoogleAuthReason reason,string message,Exception innerException=null) : base(message,innerException) {
			Reason=reason;
		}

		///<summary>True for reasons that mean "authorize this address again" (as opposed to a setup or network problem).</summary>
		public bool NeedsAuthorization {
			get {
				return Reason==GoogleAuthReason.NotAuthorized || Reason==GoogleAuthReason.NeedsReauthorization;
			}
		}

		///<summary>"Gmail could not be reached."</summary>
		public static GoogleAuthException Unreachable(Exception innerException) {
			return new GoogleAuthException(GoogleAuthReason.Unreachable,"Gmail could not be reached.",innerException);
		}

		///<summary>"{mailbox} needs to be authorized. ..."</summary>
		public static string GetNeedsAuthorizationMessage(string mailbox) {
			string name=string.IsNullOrWhiteSpace(mailbox) ? "This Gmail address" : mailbox.Trim();
			return name+" needs to be authorized. Open Setup > Email, edit the address, and click Authorize.";
		}

		///<summary>Maps a token endpoint "error" to a reason.</summary>
		public static GoogleAuthReason GetReasonForTokenError(string error) {
			switch(error??"") {
				case "invalid_grant":
					return GoogleAuthReason.NeedsReauthorization;
				case "invalid_client":
				case "unauthorized_client":
					return GoogleAuthReason.InvalidClient;
				case "admin_policy_enforced":
					return GoogleAuthReason.AdminPolicy;
				default:
					return GoogleAuthReason.Other;
			}
		}

		///<summary>Maps an "error" on the authorization redirect to a reason.</summary>
		public static GoogleAuthReason GetReasonForAuthorizeError(string error) {
			switch(error??"") {
				case "access_denied":
					return GoogleAuthReason.AccessDenied;
				case "admin_policy_enforced":
					return GoogleAuthReason.AdminPolicy;
				case "invalid_client":
				case "unauthorized_client":
					return GoogleAuthReason.InvalidClient;
				default:
					return GoogleAuthReason.Other;
			}
		}

		///<summary>The user's message for a refusal. error is Google's OAuth error code, shown only for unlisted ones.</summary>
		public static string GetRejectionMessage(GoogleAuthReason reason,string error,string mailbox) {
			switch(reason) {
				case GoogleAuthReason.NotAuthorized:
				case GoogleAuthReason.NeedsReauthorization:
					return GetNeedsAuthorizationMessage(mailbox);
				case GoogleAuthReason.InvalidClient:
					return "Google refused the OAuth client. Check the client ID and secret in Setup > Program Links > Gmail API.";
				case GoogleAuthReason.AdminPolicy:
					return "Your Google Workspace administrator has restricted this app.";
				case GoogleAuthReason.AccessDenied:
					return "Access was not granted on Google's consent page.";
				case GoogleAuthReason.Unreachable:
					return "Gmail could not be reached.";
				default:
					return "Google refused the request"+(string.IsNullOrEmpty(error) ? "" : " ("+error+")")+".";
			}
		}
	}

	///<summary>Google refused a token request (a 400 or 401 from the token endpoint). A TokenRejectedException, so TokenCache marks the configuration
	///as needing a new sign-in.</summary>
	public class GoogleTokenRejectedException : TokenRejectedException {
		///<summary>Google's error_subtype, e.g. "invalid_rapt", or "".</summary>
		public string ErrorSubtype { get; }
		public GoogleAuthReason Reason { get; }

		public GoogleTokenRejectedException(string error,string errorSubtype,GoogleAuthReason reason,string message,int statusCode)
			: base(error,message,statusCode)
		{
			ErrorSubtype=errorSubtype??"";
			Reason=reason;
		}
	}

	///<summary>What a Gmail API error means for the caller (google.md 3.5).</summary>
	public enum GmailErrorKind {
		///<summary>Not a recognized Gmail error.</summary>
		Other,
		///<summary>401: refresh the access token once and retry once.</summary>
		Unauthorized,
		///<summary>403 insufficientPermissions: the grant lacks the scope (send-only address asked to download).</summary>
		InsufficientScope,
		///<summary>403 domainPolicy: the Workspace domain blocks Gmail apps.</summary>
		DomainPolicy,
		///<summary>403 rateLimitExceeded/userRateLimitExceeded, or a concurrency or bandwidth 429: back off.</summary>
		RateLimited,
		///<summary>403 dailyLimitExceeded: the Cloud project's quota is used up. Don't retry.</summary>
		DailyQuota,
		///<summary>429 on send with a retry time: the mailbox hit Gmail's sending limit. Skip the rest of a batch for this address.</summary>
		SendingLimit,
		///<summary>500 to 504.</summary>
		ServerError,
	}

	///<summary>Classifies Gmail API errors and supplies the messages and backoff that google.md section 3.5 asks for.</summary>
	public static class GmailApiErrors {
		///<summary>The most backoff attempts for rate limits and 5xx (1, 2, 4 and 8 seconds, plus jitter).</summary>
		public const int MaxBackoffAttempts=4;
		///<summary>No backoff wait is longer than this.</summary>
		public static readonly TimeSpan MaxBackoffDelay=TimeSpan.FromSeconds(32);
		private static readonly Random _random=new Random();

		///<summary>Classifies an HTTP status plus Gmail's first error "reason" and the message. isSend is true for users.messages.send, where a 429
		///with a "Retry after" time is the mail sending limit rather than a concurrency limit.</summary>
		public static GmailErrorKind Classify(int httpStatus,string reason,string message,bool isSend) {
			reason=reason??"";
			message=message??"";
			if(httpStatus==401) {
				return GmailErrorKind.Unauthorized;
			}
			if(httpStatus==403) {
				switch(reason) {
					case "insufficientPermissions":
					case "ACCESS_TOKEN_SCOPE_INSUFFICIENT":
						return GmailErrorKind.InsufficientScope;
					case "domainPolicy":
						return GmailErrorKind.DomainPolicy;
					case "dailyLimitExceeded":
						return GmailErrorKind.DailyQuota;
					case "rateLimitExceeded":
					case "userRateLimitExceeded":
						return GmailErrorKind.RateLimited;
				}
				if(message.IndexOf("insufficient",StringComparison.OrdinalIgnoreCase)>=0 && message.IndexOf("scope",StringComparison.OrdinalIgnoreCase)>=0) {
					return GmailErrorKind.InsufficientScope;
				}
				return GmailErrorKind.Other;
			}
			if(httpStatus==429) {
				if(isSend && message.IndexOf("Retry after",StringComparison.OrdinalIgnoreCase)>=0) {
					return GmailErrorKind.SendingLimit;
				}
				return GmailErrorKind.RateLimited;
			}
			if(httpStatus>=500 && httpStatus<=504) {
				return GmailErrorKind.ServerError;
			}
			return GmailErrorKind.Other;
		}

		///<summary>True for errors worth retrying after GetBackoffDelay. A sending limit and a used-up project quota are never retried.</summary>
		public static bool IsBackoffRetryable(GmailErrorKind gmailErrorKind) {
			return gmailErrorKind==GmailErrorKind.RateLimited || gmailErrorKind==GmailErrorKind.ServerError;
		}

		///<summary>The wait before retry attempt number attempt (0 based): 1, 2, 4, 8 ... seconds plus up to 1 second of jitter, capped at 32 seconds.</summary>
		public static TimeSpan GetBackoffDelay(int attempt,Random random=null) {
			int exponent=Math.Max(0,Math.Min(attempt,5));
			double jitterMs;
			Random randomUse=random??_random;
			lock(randomUse) {
				jitterMs=randomUse.NextDouble()*1000;
			}
			TimeSpan timeSpan=TimeSpan.FromSeconds(1<<exponent)+TimeSpan.FromMilliseconds(jitterMs);
			return (timeSpan>MaxBackoffDelay) ? MaxBackoffDelay : timeSpan;
		}

		///<summary>The user's message for an error kind. mailbox names the address where it helps.</summary>
		public static string GetMessage(GmailErrorKind gmailErrorKind,string mailbox) {
			switch(gmailErrorKind) {
				case GmailErrorKind.Unauthorized:
					return GoogleAuthException.GetNeedsAuthorizationMessage(mailbox);
				case GmailErrorKind.InsufficientScope:
					return "This address was authorized for sending only. Re-authorize with Download incoming emails ticked.";
				case GmailErrorKind.DomainPolicy:
					return "Your Google Workspace administrator has restricted this app.";
				case GmailErrorKind.DailyQuota:
					return "The Gmail API quota for this Cloud project is used up.";
				case GmailErrorKind.SendingLimit:
					return "Gmail sending limit reached for "+(string.IsNullOrWhiteSpace(mailbox) ? "this address" : mailbox.Trim())+".";
				case GmailErrorKind.RateLimited:
					return "Gmail is limiting requests from this address. Try again in a few minutes.";
				case GmailErrorKind.ServerError:
					return "Gmail had a temporary problem. Try again in a few minutes.";
				default:
					return "Gmail refused the request.";
			}
		}
	}

	///<summary>Helpers for downloading mail from a Gmail inbox (google.md 3.8).</summary>
	public static class GmailInbox {
		///<summary>Used when the address has no query, so a poll doesn't list the whole mailbox.</summary>
		public const string DefaultQuery="in:inbox newer_than:30d";
		///<summary>Some stored message UIDs carry this prefix.</summary>
		public const string LegacyUidPrefix="GmailId";
		private const string UNREAD_FILTER="is:unread";

		///<summary>The query to send to messages.list. A blank query, or one that only says "is:unread", gets DefaultQuery in front.</summary>
		public static string GetQuery(string queryString) {
			string query=(queryString??"").Trim();
			string queryWithoutUnread=query.Replace(UNREAD_FILTER,"").Trim();
			if(queryWithoutUnread=="") {
				return (DefaultQuery+" "+query).Trim();
			}
			return query;
		}

		///<summary>A stored UID as a Gmail message ID: removes the exact "GmailId" prefix if present. Unlike TrimStart, this never strips leading hex
		///digits such as 'a' or 'd' from a real ID.</summary>
		public static string NormalizeStoredUid(string uid) {
			if(uid==null) {
				return "";
			}
			if(uid.StartsWith(LegacyUidPrefix,StringComparison.Ordinal)) {
				return uid.Substring(LegacyUidPrefix.Length);
			}
			return uid;
		}

		///<summary>The IDs from listedIds that aren't already stored, in order and without repeats. Exact, case-sensitive comparison.</summary>
		public static List<string> GetNewMessageIds(IEnumerable<string> listedIds,IEnumerable<string> storedUids) {
			HashSet<string> hashSetKnown=new HashSet<string>(storedUids.Select(NormalizeStoredUid),StringComparer.Ordinal);
			List<string> listNewIds=new List<string>();
			foreach(string id in listedIds) {
				if(string.IsNullOrEmpty(id) || hashSetKnown.Contains(id)) {
					continue;
				}
				hashSetKnown.Add(id);
				listNewIds.Add(id);
			}
			return listNewIds;
		}

		///<summary>Decodes a message's "raw" field (base64url, usually without '=' padding) to the RFC 2822 bytes.</summary>
		public static byte[] DecodeRaw(string raw) {
			if(raw==null) {
				throw new ArgumentNullException(nameof(raw));
			}
			return Base64Url.Decode(raw.Trim());
		}
	}
}
