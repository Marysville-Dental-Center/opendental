// Vendor-direct Microsoft 365 email: the MSAL public client per mailbox, its token cache bound to emailaddress.RefreshToken, and sign-in.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CodeBase;
using CodeBase.VendorDirect;
using Microsoft.Identity.Client;
using Microsoft.IdentityModel.Abstractions;
using MimeKit;
using OpenDentBusiness.VendorDirect;

namespace OpenDentBusiness.Microsoft365 {
	//OpenDentBusiness.Task (the task list table type) would hide System.Threading.Tasks.Task otherwise.
	using Task=System.Threading.Tasks.Task;

	///<summary>A mailbox's stored sign-in: the encrypted MSAL cache (emailaddress.RefreshToken) and the account marker (emailaddress.AccessToken).</summary>
	public sealed class Microsoft365StoredSignIn {
		public string StoredCache="";
		public string AccountMarker="";
	}

	///<summary>Where a session keeps its mailbox's encrypted MSAL cache.</summary>
	public interface IMicrosoft365CacheStore {
		///<summary>The stored values. Blank if none.</summary>
		Microsoft365StoredSignIn Read();
		///<summary>Stores a new encrypted (osd1$) cache and account marker.</summary>
		void Write(string storedCache,string accountMarker);
		///<summary>Blanks the stored cache and marker (sign-out, or a cache issued under Open Dental's registration).</summary>
		void Clear();
		///<summary>True if silent refreshes write at most once a day per process (the database). False writes every change (memory).</summary>
		bool ThrottleWrites { get; }
	}

	///<summary>A cache kept in memory only: interactive sign-in before the address is saved, MicrosoftApiConnector's (username, cache) calls,
	///and tests.</summary>
	public sealed class MemoryMicrosoft365CacheStore : IMicrosoft365CacheStore {
		private readonly object _lock=new object();
		private Microsoft365StoredSignIn _storedSignIn;

		public MemoryMicrosoft365CacheStore(string storedCache="",string accountMarker="") {
			_storedSignIn=new Microsoft365StoredSignIn { StoredCache=storedCache??"",AccountMarker=accountMarker??"" };
		}

		public bool ThrottleWrites {
			get {
				return false;
			}
		}

		public int CountWrites { get; private set; }

		public Microsoft365StoredSignIn Read() {
			lock(_lock) {
				return new Microsoft365StoredSignIn { StoredCache=_storedSignIn.StoredCache,AccountMarker=_storedSignIn.AccountMarker };
			}
		}

		public void Write(string storedCache,string accountMarker) {
			lock(_lock) {
				_storedSignIn=new Microsoft365StoredSignIn { StoredCache=storedCache??"",AccountMarker=accountMarker??"" };
				CountWrites++;
			}
		}

		public void Clear() {
			Write("","");
		}
	}

	///<summary>The cache in an emailaddress row: RefreshToken holds the osd1$ cache, AccessToken the account marker. Reads the database, not the
	///cache, so a new process gets the latest refresh token another workstation wrote. Writes only those two columns of a fresh copy of the row.
	///No Signalods on refresh: each workstation keeps its own rotating chain in memory, and Entra doesn't revoke the old one.</summary>
	public sealed class EmailAddressMicrosoft365CacheStore : IMicrosoft365CacheStore {
		private readonly long _emailAddressNum;

		public EmailAddressMicrosoft365CacheStore(long emailAddressNum) {
			_emailAddressNum=emailAddressNum;
		}

		public bool ThrottleWrites {
			get {
				return true;
			}
		}

		public Microsoft365StoredSignIn Read() {
			EmailAddress emailAddress=EmailAddresses.GetOneFromDb(_emailAddressNum);
			if(emailAddress==null) {
				return new Microsoft365StoredSignIn();
			}
			return new Microsoft365StoredSignIn { StoredCache=emailAddress.RefreshToken??"",AccountMarker=emailAddress.AccessToken??"" };
		}

		public void Write(string storedCache,string accountMarker) {
			EmailAddress emailAddress=EmailAddresses.GetOneFromDb(_emailAddressNum);
			if(emailAddress==null || emailAddress.AuthenticationType!=OAuthType.Microsoft) {
				return;//Deleted, or switched to another sign-in method meanwhile. Never resurrect a token.
			}
			emailAddress.RefreshToken=storedCache??"";
			emailAddress.AccessToken=accountMarker??"";
			EmailAddresses.Update(emailAddress);
		}

		public void Clear() {
			EmailAddress emailAddress=EmailAddresses.GetOneFromDb(_emailAddressNum);
			if(emailAddress==null || (string.IsNullOrEmpty(emailAddress.RefreshToken) && string.IsNullOrEmpty(emailAddress.AccessToken))) {
				return;
			}
			emailAddress.RefreshToken="";
			emailAddress.AccessToken="";
			EmailAddresses.Update(emailAddress);
			Signalods.SetInvalid(InvalidType.Email);
		}
	}

	///<summary>Test seams and overrides for a session. Production leaves everything null.</summary>
	public sealed class Microsoft365SessionOptions {
		///<summary>The handler under the vendor HTTP policy for MSAL (and for Graph when GraphClient is null). Null = the real network.</summary>
		public HttpMessageHandler HttpMessageHandler;
		///<summary>The Graph client. Null = Microsoft365GraphClient.Default, or a client over HttpMessageHandler.</summary>
		public Microsoft365GraphClient GraphClient;
		///<summary>Called once per mailbox per day when the mailbox needs to sign in again. Null = no alert.</summary>
		public Action<string> RaiseReauthenticationAlert;
		///<summary>Shows a device code to the user. Null = Microsoft365Session.DeviceCodeHandler.</summary>
		public Func<Microsoft365DeviceCode,Task> DeviceCodeHandler;
		///<summary>An interactive sign-in gives up after this long. MSAL can't tell whether the user closed the browser.</summary>
		public TimeSpan InteractiveTimeout=TimeSpan.FromMinutes(5);
		///<summary>Optional instance-discovery JSON for MSAL (tests). Null = MSAL's own discovery.</summary>
		public string InstanceDiscoveryJson;
		///<summary>Write throttle for database stores. Null = the process-wide one (once per 24 hours per mailbox).</summary>
		public WriteThrottle WriteThrottle;
	}

	///<summary>One MSAL public client per process and mailbox, keyed by (client ID, tenant, EmailAddressNum). Sessions get access tokens silently
	///(MSAL returns the cached token or redeems the refresh token) and never prompt; interactive sign-in is SignInInteractiveAsync on a fresh
	///session. The mailbox's MSAL v3 cache is encrypted with SecretValue (osd1$) into emailaddress.RefreshToken. A value without the prefix, or one
	///that doesn't decrypt (a cache issued to Open Dental's app registration), counts as "not signed in": it is blanked on first use and the
	///re-authentication alert is raised. See docs/specs/vendor-direct/microsoft-365.md, sections 3.2-3.5 and 3.9.</summary>
	public sealed class Microsoft365Session {
		private static readonly ConcurrentDictionary<string,Microsoft365Session> _dictSessions=new ConcurrentDictionary<string,Microsoft365Session>();
		private static readonly WriteThrottle _writeThrottleDefault=new WriteThrottle(TimeSpan.FromHours(24));
		private static readonly ConcurrentDictionary<string,DateTime> _dictAlertTimes=new ConcurrentDictionary<string,DateTime>();
		private static readonly Lazy<HttpClient> _lazyHttpClientIdentity=new Lazy<HttpClient>(() => VendorHttp.GetClient(CreateIdentityHttpOptions()));
		///<summary>The hosts MSAL may call: the authority, its instance-discovery aliases. The system browser loads Microsoft's own sign-in pages.</summary>
		public static readonly IReadOnlyList<string> ListIdentityHosts=new[] { "login.microsoftonline.com","login.microsoft.com","login.windows.net",
			"sts.windows.net" };

		private readonly Microsoft365Config _config;
		private readonly IMicrosoft365CacheStore _store;
		private readonly string _key;
		private readonly string _mailboxLabel;
		private readonly Microsoft365SessionOptions _options;
		private readonly Microsoft365GraphClient _graphClient;
		private readonly SemaphoreSlim _semaphore=new SemaphoreSlim(1,1);
		private readonly object _lockKnownValues=new object();
		private readonly HashSet<string> _setKnownStoredValues=new HashSet<string>(StringComparer.Ordinal);
		private IPublicClientApplication _publicClientApplication;
		private byte[] _arrayCacheToLoad;
		private bool _isCacheLoaded;
		private byte[] _arrayCachePending;
		private string _storedCacheCurrent="";
		private string _accountMarker="";
		private string _accountUsername="";
		private bool _isStoreLoaded;
		private volatile bool _needsReauthentication;

		///<summary>Shows a device code (Sign-in Method = DeviceCode). The UI may replace it. The default opens Microsoft's verification page in the
		///browser and shows the code in a message box, on its own thread, so MSAL starts polling at once.</summary>
		public static Func<Microsoft365DeviceCode,Task> DeviceCodeHandler=ShowDeviceCodeDefault;

		///<summary>A session over any store. Production code uses Get(EmailAddress) or the static sign-in methods.</summary>
		public Microsoft365Session(Microsoft365Config config,IMicrosoft365CacheStore store,string key,string mailboxLabel="",
			Microsoft365SessionOptions options=null)
		{
			_config=config?.Copy()??throw new ArgumentNullException(nameof(config));
			_store=store??throw new ArgumentNullException(nameof(store));
			_key=key??"";
			_mailboxLabel=mailboxLabel??"";
			_options=options??new Microsoft365SessionOptions();
			if(_options.GraphClient!=null) {
				_graphClient=_options.GraphClient;
			}
			else if(_options.HttpMessageHandler!=null) {
				_graphClient=new Microsoft365GraphClient(new Microsoft365GraphOptions { HttpMessageHandler=_options.HttpMessageHandler });
			}
			else {
				_graphClient=Microsoft365GraphClient.Default;
			}
		}

		#region Properties
		///<summary>True after Microsoft refused the refresh token (revoked, expired, password reset) or the stored cache was unusable. Every later
		///call fails at once, with no network call, until the mailbox's stored sign-in changes.</summary>
		public bool NeedsReauthentication {
			get {
				return _needsReauthentication;
			}
		}

		///<summary>MSAL's HomeAccountId.Identifier for the signed-in account, or "".</summary>
		public string AccountMarker {
			get {
				return _accountMarker;
			}
		}

		///<summary>The signed-in account's user name as MSAL knows it, or "".</summary>
		public string AccountUsername {
			get {
				return _accountUsername;
			}
		}

		///<summary>The latest encrypted cache this session holds (osd1$), which may be newer than the stored one when writes are throttled.</summary>
		public string CurrentStoredCache {
			get {
				return _storedCacheCurrent;
			}
		}

		///<summary>Supplies tokens to Microsoft365GraphClient: silent, with a forced refresh after a 401.</summary>
		public Microsoft365TokenProvider TokenProvider {
			get {
				return (listScopes,forceRefresh,cancellationToken) => GetAccessTokenAsync(listScopes,forceRefresh,cancellationToken);
			}
		}

		///<summary>The key the Graph client limits calls and sends by: the mailbox.</summary>
		private string MailboxKey {
			get {
				return string.IsNullOrEmpty(_mailboxLabel) ? _key : _mailboxLabel;
			}
		}
		#endregion Properties

		#region Registry
		///<summary>The session for an emailaddress row, created on first use. Throws Microsoft365Exception (NotConfigured) if the program is off or
		///not set up; no network call is made then. If the row's stored sign-in changed since this session last saw it (another workstation signed
		///in again), the session starts over from the new value.</summary>
		public static Microsoft365Session Get(EmailAddress emailAddress) {
			Meth.NoCheckMiddleTierRole();
			if(emailAddress==null) {
				throw new ArgumentNullException(nameof(emailAddress));
			}
			Microsoft365Config config=Microsoft365Program.GetConfigOrThrow();
			string key=config.GetKey()+"|"+POut.Long(emailAddress.EmailAddressNum);
			if(emailAddress.EmailAddressNum==0) {
				//Not saved yet: nothing to write back to.
				return new Microsoft365Session(config,new MemoryMicrosoft365CacheStore(emailAddress.RefreshToken,emailAddress.AccessToken),key,
					emailAddress.EmailUsername);
			}
			Microsoft365Session session=_dictSessions.GetOrAdd(key,x => {
				Microsoft365SessionOptions options=new Microsoft365SessionOptions();
				options.RaiseReauthenticationAlert=RaiseReauthenticationAlertDefault;
				return new Microsoft365Session(config,new EmailAddressMicrosoft365CacheStore(emailAddress.EmailAddressNum),x,emailAddress.EmailUsername,options);
			});
			session.NoteStoredValue(emailAddress.RefreshToken);
			return session;
		}

		///<summary>A bearer token for the row's mailbox, from a pool thread (never blocks the UI thread's context). scopes defaults to sending.
		///Throws Microsoft365AuthException when the mailbox has to sign in again.</summary>
		public static string GetAccessToken(EmailAddress emailAddress,bool forceRefresh=false,IReadOnlyList<string> listScopes=null) {
			Meth.NoCheckMiddleTierRole();
			Microsoft365Session session=Get(emailAddress);
			return VendorHttp.RunSync(() => session.GetAccessTokenAsync(listScopes??Microsoft365Scopes.ListSend,forceRefresh));
		}

		///<summary>Drops the session for a row, e.g. after sign-out or deleting the address. The next Get starts over from the database.</summary>
		public static void Forget(long emailAddressNum) {
			string suffix="|"+POut.Long(emailAddressNum);
			foreach(string key in _dictSessions.Keys.Where(x => x.EndsWith(suffix,StringComparison.Ordinal)).ToList()) {
				Microsoft365Session session;
				_dictSessions.TryRemove(key,out session);
			}
		}

		///<summary>Drops every session, e.g. after the app registration settings changed.</summary>
		public static void ForgetAll() {
			_dictSessions.Clear();
		}

		///<summary>Drops sessions whose stored cache is storedCache (sign-out by value, when the row number isn't known).</summary>
		internal static void ForgetByStoredCache(string storedCache) {
			if(string.IsNullOrEmpty(storedCache)) {
				return;
			}
			foreach(KeyValuePair<string,Microsoft365Session> pair in _dictSessions.ToList()) {
				if(pair.Value.IsKnownStoredValue(storedCache)) {
					Microsoft365Session session;
					_dictSessions.TryRemove(pair.Key,out session);
				}
			}
		}
		#endregion Registry

		#region Tokens
		///<summary>Ensures this session reflects storedCache, the value the caller's emailaddress row holds. A value this session loaded or wrote
		///itself changes nothing; anything else (a new sign-in elsewhere, a sign-out) starts the session over and clears NeedsReauthentication.</summary>
		public void NoteStoredValue(string storedCache) {
			storedCache=storedCache??"";
			if(IsKnownStoredValue(storedCache) || (!_isStoreLoaded && !_needsReauthentication)) {
				return;//Nothing loaded yet (the first call reads the database), or a value this session already knows.
			}
			if(!_semaphore.Wait(TimeSpan.Zero)) {
				return;//A token call is running. Never block the caller; the next call notices the change.
			}
			try {
				if(IsKnownStoredValue(storedCache)) {
					return;
				}
				ResetLocked();
			}
			finally {
				_semaphore.Release();
			}
		}

		///<summary>A bearer token with listScopes, silently: MSAL's cached token while it is valid (it refreshes about 5 minutes before expiry),
		///otherwise the refresh token is redeemed. forceRefresh skips the cached token (after a Graph 401). Never prompts.
		///Throws Microsoft365AuthException when the mailbox has to sign in again (and sets NeedsReauthentication), Microsoft365Exception otherwise.</summary>
		public async Task<string> GetAccessTokenAsync(IReadOnlyList<string> listScopes=null,bool forceRefresh=false,
			CancellationToken cancellationToken=default(CancellationToken))
		{
			if(_needsReauthentication) {
				throw new Microsoft365AuthException(Microsoft365ErrorKind.NeedsSignIn,Microsoft365Errors.MessageNeedsSignIn);
			}
			listScopes=listScopes??Microsoft365Scopes.ListSend;
			await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
			try {
				if(_needsReauthentication) {
					throw new Microsoft365AuthException(Microsoft365ErrorKind.NeedsSignIn,Microsoft365Errors.MessageNeedsSignIn);
				}
				LoadStoreLocked();
				EnsureClientLocked();
				IAccount account=await FindAccountAsync().ConfigureAwait(false);
				if(account==null) {
					MarkNeedsReauthentication(raiseAlert:true);
					throw new Microsoft365AuthException(Microsoft365ErrorKind.NeedsSignIn,Microsoft365Errors.MessageNeedsSignIn);
				}
				AuthenticationResult authenticationResult;
				try {
					authenticationResult=await _publicClientApplication.AcquireTokenSilent(listScopes,account).WithForceRefresh(forceRefresh)
						.ExecuteAsync(cancellationToken).ConfigureAwait(false);
				}
				catch(MsalUiRequiredException ex) {
					MarkNeedsReauthentication(raiseAlert:true);
					Microsoft365Log.Write("Microsoft 365 token: sign-in required ("+ex.ErrorCode+" "+Microsoft365Errors.GetAadstsCode(ex.Message)+")");
					throw new Microsoft365AuthException(Microsoft365ErrorKind.NeedsSignIn,Microsoft365Errors.MessageNeedsSignIn,ex.StatusCode,ex.ErrorCode);
				}
				catch(Exception ex) when(!(ex is Microsoft365Exception) && !IsCallerCancellation(ex,cancellationToken)) {
					throw MapIdentityException(ex);
				}
				_accountUsername=authenticationResult.Account?.Username??_accountUsername;
				PersistPendingLocked(authenticationResult.Account?.HomeAccountId?.Identifier??_accountMarker,isAccountChange:false);
				return authenticationResult.AccessToken;
			}
			finally {
				_semaphore.Release();
			}
		}

		///<summary>Gets a fresh access token now (replaces EmailMessages.RefreshMicrosoftToken). Blocks a pool thread.</summary>
		public void ForceRefresh() {
			VendorHttp.RunSync(() => GetAccessTokenAsync(Microsoft365Scopes.ListSend,true));
		}
		#endregion Tokens

		#region Graph calls for this mailbox
		///<summary>GET /me for this mailbox.</summary>
		public Task<MicrosoftProfile> GetProfileAsync(CancellationToken cancellationToken=default(CancellationToken)) {
			return _graphClient.GetProfileAsync(MailboxKey,TokenProvider,cancellationToken);
		}

		///<summary>Sends a MIME message from this mailbox (see Microsoft365GraphClient.SendMimeAsync).</summary>
		public Task SendMimeAsync(MimeMessage mimeMessage,CancellationToken cancellationToken=default(CancellationToken),bool isBackground=false) {
			return _graphClient.SendMimeAsync(MailboxKey,TokenProvider,mimeMessage,cancellationToken,isBackground);
		}

		///<summary>Lists this mailbox's inbox since sinceUtc (immutable IDs, newest first).</summary>
		public Task<List<MicrosoftMessageRef>> ListInboxAsync(DateTime sinceUtc,CancellationToken cancellationToken=default(CancellationToken),
			bool isBackground=false)
		{
			return _graphClient.ListInboxAsync(MailboxKey,TokenProvider,sinceUtc,cancellationToken,isBackground);
		}

		///<summary>Downloads one message as MIME.</summary>
		public Task<MimeMessage> GetMimeAsync(string messageId,CancellationToken cancellationToken=default(CancellationToken),bool isBackground=false) {
			return _graphClient.GetMimeAsync(MailboxKey,TokenProvider,messageId,cancellationToken,isBackground);
		}
		#endregion Graph calls for this mailbox

		#region Sign-in
		///<summary>Signs a mailbox in interactively: the system browser with MSAL's loopback listener and PKCE (or device code, per the setup), then
		///GET /me to check the account. If emailUsername is not blank and neither the account's mail nor its user principal name match it, nothing is
		///kept and WrongAccount is thrown. Returns the account marker and the encrypted cache to store in the emailaddress row; writes nothing to
		///the database itself. Throws Microsoft365Exception (Cancelled when the user cancelled or after the 5-minute timeout).</summary>
		public static Task<Microsoft365SignInResult> SignInInteractiveAsync(Microsoft365Config config,string emailUsername,IEnumerable<string> scopes,
			CancellationToken cancellationToken=default(CancellationToken),Microsoft365SessionOptions options=null)
		{
			Microsoft365Session session=new Microsoft365Session(config,new MemoryMicrosoft365CacheStore(),"sign-in",emailUsername,options);
			return session.SignInInteractiveInternalAsync(emailUsername,scopes,cancellationToken);
		}

		private async Task<Microsoft365SignInResult> SignInInteractiveInternalAsync(string emailUsername,IEnumerable<string> scopes,
			CancellationToken cancellationToken)
		{
			List<string> listScopes=(scopes??Microsoft365Scopes.ListSend).ToList();
			await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
			try {
				_isStoreLoaded=true;//A sign-in starts from an empty cache.
				_isCacheLoaded=true;
				EnsureClientLocked();
				AuthenticationResult authenticationResult;
				using(CancellationTokenSource cancellationTokenSource=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)) {
					cancellationTokenSource.CancelAfter(_options.InteractiveTimeout);
					try {
						if(_config.SignInMethod==Microsoft365SignInMethod.DeviceCode) {
							Func<Microsoft365DeviceCode,Task> funcHandler=_options.DeviceCodeHandler??DeviceCodeHandler;
							authenticationResult=await _publicClientApplication.AcquireTokenWithDeviceCode(listScopes,deviceCodeResult => {
								Microsoft365DeviceCode microsoft365DeviceCode=new Microsoft365DeviceCode();
								microsoft365DeviceCode.UserCode=deviceCodeResult.UserCode;
								microsoft365DeviceCode.VerificationUrl=deviceCodeResult.VerificationUrl;
								microsoft365DeviceCode.Message=deviceCodeResult.Message;
								microsoft365DeviceCode.ExpiresOn=deviceCodeResult.ExpiresOn;
								return (funcHandler==null) ? Task.CompletedTask : funcHandler(microsoft365DeviceCode);
							}).ExecuteAsync(cancellationTokenSource.Token).ConfigureAwait(false);
						}
						else {
							SystemWebViewOptions systemWebViewOptions=new SystemWebViewOptions();
							//MSAL formats the error page with string.Format, so neither page may contain braces.
							systemWebViewOptions.HtmlMessageSuccess=GetBrowserPageHtml(false);
							systemWebViewOptions.HtmlMessageError=GetBrowserPageHtml(true);
							systemWebViewOptions.OpenBrowserAsync=uri => {
								SystemBrowser.Open(uri.AbsoluteUri);
								return Task.CompletedTask;
							};
							AcquireTokenInteractiveParameterBuilder builder=_publicClientApplication.AcquireTokenInteractive(listScopes)
								.WithUseEmbeddedWebView(false)
								.WithPrompt(Prompt.SelectAccount)
								.WithSystemWebViewOptions(systemWebViewOptions);
							if(!string.IsNullOrWhiteSpace(emailUsername)) {
								builder=builder.WithLoginHint(emailUsername.Trim());
							}
							authenticationResult=await builder.ExecuteAsync(cancellationTokenSource.Token).ConfigureAwait(false);
						}
					}
					catch(OperationCanceledException ex) {
						throw new Microsoft365Exception(Microsoft365ErrorKind.Cancelled,Microsoft365Errors.MessageCancelled,0,"authentication_canceled","",ex);
					}
					catch(Exception ex) when(!(ex is Microsoft365Exception)) {
						throw MapIdentityException(ex);
					}
				}
				//Check the account before keeping anything.
				string accessToken=authenticationResult.AccessToken;
				Microsoft365TokenProvider getTokenOnce=(listScopesProfile,forceRefresh,ct) => {
					if(forceRefresh) {
						throw new Microsoft365AuthException(Microsoft365ErrorKind.NeedsSignIn,Microsoft365Errors.MessageNeedsSignIn,401,"InvalidAuthenticationToken");
					}
					return Task.FromResult(accessToken);
				};
				MicrosoftProfile microsoftProfile=await _graphClient.GetProfileAsync(MailboxKey,getTokenOnce,cancellationToken).ConfigureAwait(false);
				if(!microsoftProfile.Matches(emailUsername)) {
					throw new Microsoft365Exception(Microsoft365ErrorKind.WrongAccount,"Signed in as "+microsoftProfile.GetAddress()+", but this email address "
						+"is "+emailUsername.Trim()+". Sign in with that account, or change the address's user name first.");
				}
				string accountMarker=authenticationResult.Account?.HomeAccountId?.Identifier??"";
				_accountUsername=authenticationResult.Account?.Username??"";
				PersistPendingLocked(accountMarker,isAccountChange:true);
				if(string.IsNullOrEmpty(_storedCacheCurrent) || accountMarker=="") {
					throw new Microsoft365Exception(Microsoft365ErrorKind.General,"The sign-in could not be stored securely. Nothing was saved.");
				}
				Microsoft365SignInResult microsoft365SignInResult=new Microsoft365SignInResult();
				microsoft365SignInResult.AccountMarker=accountMarker;
				microsoft365SignInResult.StoredCache=_storedCacheCurrent;
				microsoft365SignInResult.Profile=microsoftProfile;
				return microsoft365SignInResult;
			}
			finally {
				_semaphore.Release();
			}
		}

		///<summary>The page MSAL shows in the browser after the redirect: the shared vendor-direct text, self-contained, no braces or assets.</summary>
		public static string GetBrowserPageHtml(bool isError) {
			string title=isError ? OAuthResponsePage.TitleError : OAuthResponsePage.TitleSuccess;
			string message=isError ? OAuthResponsePage.MessageError : OAuthResponsePage.MessageSuccess;
			return "<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>"+title+"</title></head><body><p>"+message+"</p></body></html>";
		}

		private static Task ShowDeviceCodeDefault(Microsoft365DeviceCode microsoft365DeviceCode) {
			try {
				SystemBrowser.Open(microsoft365DeviceCode.VerificationUrl);
			}
			catch(Exception ex) {
				ex.DoNothing();//The message box still names the page.
			}
			Thread thread=new Thread(() => {
				try {
					System.Windows.Forms.MessageBox.Show("To sign in, open "+microsoft365DeviceCode.VerificationUrl+" and enter the code "
						+microsoft365DeviceCode.UserCode+".\r\n\r\nPress Ctrl+C to copy this text.","Microsoft 365 sign-in");
				}
				catch(Exception ex) {
					ex.DoNothing();
				}
			});
			thread.IsBackground=true;
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
			return Task.CompletedTask;
		}
		#endregion Sign-in

		#region MSAL plumbing
		private static VendorHttpOptions CreateIdentityHttpOptions() {
			VendorHttpOptions vendorHttpOptions=new VendorHttpOptions();
			vendorHttpOptions.VendorName="Microsoft identity platform";
			vendorHttpOptions.ListAllowedHosts=ListIdentityHosts.ToList();
			vendorHttpOptions.Timeout=TimeSpan.FromSeconds(30);
			vendorHttpOptions.UseCircuitBreaker=true;
			vendorHttpOptions.Log=Microsoft365Log.Write;
			return vendorHttpOptions;
		}

		///<summary>Reads the store once: decrypts the cache, or handles a blank or legacy value. Caller holds _semaphore.</summary>
		private void LoadStoreLocked() {
			if(_isStoreLoaded) {
				return;
			}
			Microsoft365StoredSignIn storedSignIn=_store.Read();
			AddKnownStoredValue(storedSignIn.StoredCache);//Whatever happens below, only a different value starts the session over.
			SecretReadResult secretReadResult=SecretValue.Read(storedSignIn.StoredCache,LegacyPolicy.RejectPlaintext);
			if(secretReadResult.NeedsReentry) {
				AddKnownStoredValue("");
				//A cache issued to Open Dental's app registration (MiscUtils-encrypted, or odv2e$), or a corrupt value. Never decrypted with
				//Open Dental's key, never sent. Blank it, once, and ask for a new sign-in.
				try {
					_store.Clear();
				}
				catch(Exception ex) {
					ex.DoNothing();
				}
				Microsoft365Log.Write("Microsoft 365 token: the stored sign-in is not usable here; cleared.");
				MarkNeedsReauthentication(raiseAlert:true);
				throw new Microsoft365AuthException(Microsoft365ErrorKind.NeedsSignIn,Microsoft365Errors.MessageNeedsSignIn,0,"LegacyCache");
			}
			if(secretReadResult.Status!=SecretReadStatus.Ok || !secretReadResult.HasValue) {
				MarkNeedsReauthentication(raiseAlert:false);
				throw new Microsoft365AuthException(Microsoft365ErrorKind.NeedsSignIn,Microsoft365Errors.MessageNeedsSignIn,0,"NotSignedIn");
			}
			_arrayCacheToLoad=Encoding.UTF8.GetBytes(secretReadResult.PlainText);
			_isCacheLoaded=false;
			_accountMarker=storedSignIn.AccountMarker??"";
			_storedCacheCurrent=storedSignIn.StoredCache;
			AddKnownStoredValue(storedSignIn.StoredCache);
			_isStoreLoaded=true;
		}

		private void EnsureClientLocked() {
			if(_publicClientApplication!=null) {
				return;
			}
			if(!_config.IsComplete) {
				throw new Microsoft365Exception(Microsoft365ErrorKind.NotConfigured,Microsoft365Errors.MessageNotConfigured);
			}
			HttpClient httpClient;
			if(_options.HttpMessageHandler!=null) {
				VendorHttpOptions vendorHttpOptions=CreateIdentityHttpOptions();
				vendorHttpOptions.MaxGetRetries=0;
				httpClient=VendorHttp.CreateClient(vendorHttpOptions,new NonDisposingHandler(_options.HttpMessageHandler));
			}
			else {
				httpClient=_lazyHttpClientIdentity.Value;
			}
			PublicClientApplicationBuilder builder=PublicClientApplicationBuilder.Create(_config.ClientId.Trim())
				.WithAuthority(AzureCloudInstance.AzurePublic,_config.TenantId.Trim())
				.WithRedirectUri(_config.GetRedirectUriOrDefault())
				.WithClientName(ProductIdentity.ProductName)
				.WithClientVersion(ProductIdentity.Version)
				.WithLegacyCacheCompatibility(false)
				.WithHttpClientFactory(new MsalHttpClientFactory(httpClient))
				.WithLogging(new MsalNoLogger(),enablePiiLogging:false);
			if(!string.IsNullOrEmpty(_options.InstanceDiscoveryJson)) {
				builder=builder.WithInstanceDiscoveryMetadata(_options.InstanceDiscoveryJson);
			}
			_publicClientApplication=builder.Build();
			_publicClientApplication.UserTokenCache.SetBeforeAccess(OnBeforeAccess);
			_publicClientApplication.UserTokenCache.SetAfterAccess(OnAfterAccess);
		}

		private void OnBeforeAccess(TokenCacheNotificationArgs args) {
			if(_isCacheLoaded) {
				return;
			}
			_isCacheLoaded=true;
			if(_arrayCacheToLoad!=null && _arrayCacheToLoad.Length>0) {
				args.TokenCache.DeserializeMsalV3(_arrayCacheToLoad,true);
			}
			_arrayCacheToLoad=null;
		}

		private void OnAfterAccess(TokenCacheNotificationArgs args) {
			if(args.HasStateChanged) {
				_arrayCachePending=args.TokenCache.SerializeMsalV3();
			}
		}

		///<summary>After an MSAL call: encrypts a changed cache, and writes it to the store when the account changed, when the store isn't
		///throttled, or when this process hasn't written this mailbox in 24 hours. Caller holds _semaphore.</summary>
		private void PersistPendingLocked(string accountMarker,bool isAccountChange) {
			accountMarker=accountMarker??"";
			bool isMarkerChange=(accountMarker!=_accountMarker);
			_accountMarker=accountMarker;
			byte[] arrayPending=_arrayCachePending;
			_arrayCachePending=null;
			if(arrayPending==null) {
				return;
			}
			string stored;
			if(!SecretValue.Protect(Encoding.UTF8.GetString(arrayPending),out stored)) {
				Microsoft365Log.Write("Microsoft 365 token: the cache could not be encrypted; not stored.");
				return;
			}
			_storedCacheCurrent=stored;
			AddKnownStoredValue(stored);
			WriteThrottle writeThrottle=_options.WriteThrottle??_writeThrottleDefault;
			if(!_store.ThrottleWrites || isAccountChange || isMarkerChange || writeThrottle.ShouldWrite(_key,accountMarker)) {
				_store.Write(stored,accountMarker);
			}
		}

		private async Task<IAccount> FindAccountAsync() {
			IAccount account=null;
			if(!string.IsNullOrEmpty(_accountMarker)) {
				account=await _publicClientApplication.GetAccountAsync(_accountMarker).ConfigureAwait(false);
			}
			if(account==null) {
				account=(await _publicClientApplication.GetAccountsAsync().ConfigureAwait(false)).FirstOrDefault();
			}
			if(account!=null) {
				_accountUsername=account.Username??"";
			}
			return account;
		}

		private void MarkNeedsReauthentication(bool raiseAlert) {
			_needsReauthentication=true;
			if(!raiseAlert || _options.RaiseReauthenticationAlert==null) {
				return;
			}
			DateTime dateTimeNow=DateTime.UtcNow;
			DateTime dateTimeLast;
			if(_dictAlertTimes.TryGetValue(_key,out dateTimeLast) && dateTimeNow-dateTimeLast<TimeSpan.FromDays(1)) {
				return;
			}
			_dictAlertTimes[_key]=dateTimeNow;
			try {
				_options.RaiseReauthenticationAlert(_mailboxLabel);
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
		}

		///<summary>One alert per mailbox per day: "needs to sign in again", opening Setup > Email.</summary>
		private static void RaiseReauthenticationAlertDefault(string emailUsername) {
			AlertItem alertItem=new AlertItem();
			alertItem.ClinicNum=-1;
			alertItem.Description="The following email address needs to sign in to Microsoft 365 again: "+emailUsername;
			alertItem.Type=AlertType.Generic;
			alertItem.Severity=SeverityType.Medium;
			alertItem.Actions=ActionType.Delete|ActionType.MarkAsRead|ActionType.OpenForm;
			alertItem.FormToOpen=FormType.FormEmailAddresses;
			alertItem.FKey=0;
			alertItem.ItemValue="";
			alertItem.UserNum=0;
			AlertItems.Insert(alertItem);
		}

		private void ResetLocked() {
			_publicClientApplication=null;
			_arrayCacheToLoad=null;
			_arrayCachePending=null;
			_isCacheLoaded=false;
			_isStoreLoaded=false;
			_storedCacheCurrent="";
			_accountMarker="";
			_accountUsername="";
			_needsReauthentication=false;
			lock(_lockKnownValues) {
				_setKnownStoredValues.Clear();
			}
		}

		private void AddKnownStoredValue(string storedCache) {
			lock(_lockKnownValues) {
				_setKnownStoredValues.Add(storedCache??"");
			}
		}

		private bool IsKnownStoredValue(string storedCache) {
			lock(_lockKnownValues) {
				return _setKnownStoredValues.Contains(storedCache??"");
			}
		}

		private static bool IsCallerCancellation(Exception ex,CancellationToken cancellationToken) {
			return ex is OperationCanceledException && cancellationToken.IsCancellationRequested;
		}

		///<summary>Maps an MSAL or network exception to Microsoft365Exception.</summary>
		private Microsoft365Exception MapIdentityException(Exception ex) {
			for(Exception exCur=ex;exCur!=null;exCur=exCur.InnerException) {
				if(exCur is HttpRequestException || exCur is TimeoutException) {
					return new Microsoft365Exception(Microsoft365ErrorKind.Unreachable,Microsoft365Errors.MessageUnreachable,0,"NetworkFailure","",ex);
				}
			}
			if(ex is OperationCanceledException) {
				return new Microsoft365Exception(Microsoft365ErrorKind.Unreachable,Microsoft365Errors.MessageUnreachable,0,"request_timeout","",ex);
			}
			MsalServiceException msalServiceException=ex as MsalServiceException;
			if(msalServiceException!=null) {
				Microsoft365Log.Write("Microsoft 365 token: "+msalServiceException.ErrorCode+" "+Microsoft365Errors.GetAadstsCode(msalServiceException.Message)
					+" HTTP "+msalServiceException.StatusCode);
				return Microsoft365Errors.FromIdentityError(msalServiceException.ErrorCode,msalServiceException.Message,msalServiceException.StatusCode,
					_config.GetRedirectUriOrDefault());
			}
			MsalException msalException=ex as MsalException;
			if(msalException!=null) {
				Microsoft365Log.Write("Microsoft 365 token: "+msalException.ErrorCode);
				return Microsoft365Errors.FromIdentityError(msalException.ErrorCode,msalException.Message,0,_config.GetRedirectUriOrDefault());
			}
			Microsoft365Log.Write("Microsoft 365 token: "+ex.GetType().Name);
			return new Microsoft365Exception(Microsoft365ErrorKind.General,Microsoft365Errors.GetMessage(Microsoft365ErrorKind.General),0,ex.GetType().Name,
				"",ex);
		}

		///<summary>Hands MSAL the vendor-policy HttpClient, so its calls get the same host checks, HTTPS rule and circuit breaker.</summary>
		private sealed class MsalHttpClientFactory : IMsalHttpClientFactory {
			private readonly HttpClient _httpClient;

			public MsalHttpClientFactory(HttpClient httpClient) {
				_httpClient=httpClient;
			}

			public HttpClient GetHttpClient() {
				return _httpClient;
			}
		}

		///<summary>MSAL's logger, switched off for every level, so MSAL never writes tokens, account names or other PII anywhere (the same as MSAL's
		///default, made explicit). Using MSAL's IIdentityLogger here also makes Microsoft.IdentityModel.Abstractions a reference of this assembly, so
		///every program that hosts the sign-in copies it and gets the binding redirect it needs (MSAL 4.47.1 asks for 6.22; the shipped copy is
		///6.23.1). Microsoft Graph's dependencies brought it in before Bridges.dll and the Graph SDK were removed.</summary>
		private sealed class MsalNoLogger : IIdentityLogger {
			public bool IsEnabled(EventLogLevel eventLogLevel) {
				return false;
			}

			public void Log(LogEntry entry) {
			}
		}

		///<summary>Lets the test handler outlive the clients built over it.</summary>
		private sealed class NonDisposingHandler : DelegatingHandler {
			public NonDisposingHandler(HttpMessageHandler innerHandler) : base(innerHandler) {
			}

			protected override void Dispose(bool disposing) {
			}
		}
		#endregion MSAL plumbing
	}
}
