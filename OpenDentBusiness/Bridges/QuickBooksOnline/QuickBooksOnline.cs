// QuickBooks Online (direct): the static entry points the setup and deposit windows call. Replaces the QuickBooksOnline type of the prebuilt
// Bridges.dll, re-implemented from how this program calls it and from Intuit's public documentation. Talks only to Intuit.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CodeBase;
using CodeBase.VendorDirect;

namespace OpenDentBusiness.Qbo {
	///<summary>A connection Intuit approved but that is not saved yet: the setup window asks first when it belongs to a different company.</summary>
	public sealed class QboPendingConnection {
		public QboTokenResult TokenResult;
		public string RealmId="";
		public string CompanyName="";
		///<summary>True when a readable realm ID is stored and differs: the saved account and class lists belong to the other company.</summary>
		public bool IsDifferentCompany;
	}

	///<summary>QuickBooks Online deposits, directly with Intuit using the office's own Intuit app (client ID and secret in the setup window). There is
	///no Open Dental step anywhere: the consent page opens in the system browser, the token exchange and every Accounting API call go from this
	///workstation to Intuit. Results keep the RequestException style the callers rely on: failures come back on the result, not as exceptions.
	///Every call runs on the client (never on the middle tier server).</summary>
	public static class QuickBooksOnline {
		///<summary>Vendor name for VendorHttp and the log folder.</summary>
		public const string VendorName="QuickBooksOnline";

		#region Connecting
		///<summary>Starts a connection attempt with the saved settings: validates them and builds the consent URL with a new state. Throws
		///QboAuthException with a message for the user when the settings can't be used. Start the request's listener (when UsesLoopback) before
		///opening Url in the browser.</summary>
		public static QboAuthRequest BeginAuthorization() {
			Meth.NoCheckMiddleTierRole();
			QboSettings settings=QboSettings.Load();
			if(settings.ClientId=="") {
				throw new QboAuthException("Enter the Client ID of your Intuit app.",needsReconnect:false);
			}
			if(settings.ClientSecret=="") {
				throw new QboAuthException("Enter the Client Secret of your Intuit app.",needsReconnect:false);
			}
			string error=QboRedirectRules.Validate(settings.Environment,settings.RedirectUri);
			if(error!="") {
				throw new QboAuthException(error,needsReconnect:false);
			}
			return QboAuthRequest.Create(settings.ClientId,settings.RedirectUri,settings.Environment,settings.RelayPort);
		}

		///<summary>Checks what came back against the request, redeems the code and reads the company's name. Saves nothing. Throws QboAuthException
		///or QboApiException with a message for the user.</summary>
		public static QboPendingConnection ExchangeAuthorization(QboAuthRequest request,QboAuthorizationResult result) {
			Meth.NoCheckMiddleTierRole();
			if(request==null || result==null) {
				throw new QboAuthException("No authorization was received. Click Connect again.",needsReconnect:false);
			}
			result.Validate(request);
			QboSettings settings=QboSettings.Load();
			if(settings.ClientId!=request.ClientId || settings.Environment!=request.Environment || settings.RedirectUri!=request.RedirectUri) {
				throw new QboAuthException("The settings changed during the connection attempt. Click Connect again.",needsReconnect:false);
			}
			QboOAuth qboOAuth=new QboOAuth(GetOAuthHttpClient(),settings.ClientId,settings.ClientSecret);
			QboTokenResult tokenResult;
			try {
				tokenResult=VendorHttp.RunSync(() => qboOAuth.ExchangeCodeAsync(result.Code,request.RedirectUri));
			}
			catch(TokenRejectedException ex) {
				if(ex.Error=="invalid_client") {
					throw new QboAuthException(QboAuthException.MessageInvalidClient,needsReconnect:false,oAuthError:ex.Error,innerException:ex);
				}
				throw new QboAuthException("Intuit did not accept the authorization ("+(ex.Error=="" ? "HTTP "+ex.StatusCode : ex.Error)+"). "
					+"The code may have expired or been used already. Click Connect again.",needsReconnect:false,oAuthError:ex.Error,innerException:ex);
			}
			catch(HttpRequestException ex) {
				throw new QboApiException(QboApiException.MessageUnreachable,innerException:ex);
			}
			if(tokenResult.RefreshToken=="") {
				throw new QboAuthException("Intuit did not return a refresh token. Click Connect again.",needsReconnect:false);
			}
			//A manager just for this attempt: the new company has no saved token yet, so it hands out the token just issued.
			QboTokenManager tokenManagerPending=new QboTokenManager(new TokenCache(),new PendingStore(tokenResult.RefreshToken),
				(refreshToken,ct) => qboOAuth.RefreshAsync(refreshToken,ct));
			tokenManagerPending.Prime(tokenResult);
			QboApiClient qboApiClient=new QboApiClient(GetApiHttpClient(settings.Environment),GetApiBaseUri(settings.Environment),result.RealmId,
				tokenManagerPending);
			QboPendingConnection pending=new QboPendingConnection();
			pending.TokenResult=tokenResult;
			pending.RealmId=result.RealmId;
			try {
				pending.CompanyName=VendorHttp.RunSync(() => qboApiClient.GetCompanyNameAsync());
			}
			catch(QboApiException ex) {
				ex.DoNothing();//Display only. The connection works without it.
			}
			pending.IsDifferentCompany=(settings.RealmIdStatus==OpenDentBusiness.VendorDirect.SecretReadStatus.Ok
				|| settings.RealmIdStatus==OpenDentBusiness.VendorDirect.SecretReadStatus.Legacy)
				&& settings.RealmId!="" && settings.RealmId!=result.RealmId;
			return pending;
		}

		///<summary>Saves an approved connection: the encrypted refresh token and realm ID, the expiry and the company name; blanks the account and class
		///lists when clearEntityLists; primes this process's token cache. Returns false if the values could not be encrypted (nothing saved). The caller
		///signals InvalidType.Programs and writes the security log entry.</summary>
		public static bool SaveConnection(QboPendingConnection pending,bool clearEntityLists) {
			Meth.NoCheckMiddleTierRole();
			if(!QboSettings.SaveConnection(pending.TokenResult.RefreshToken,pending.TokenResult.RefreshTokenExpiresUtc,pending.RealmId,
				pending.CompanyName,clearEntityLists))
			{
				return false;
			}
			ProgramProperties.RefreshCache();
			QboSettings settings=QboSettings.Load();
			GetTokenManager(settings).Prime(pending.TokenResult);
			return true;
		}

		///<summary>Revokes an approved connection the user decided not to keep. Errors are logged and ignored.</summary>
		public static void DiscardPendingConnection(QboPendingConnection pending) {
			Meth.NoCheckMiddleTierRole();
			if(pending?.TokenResult==null || pending.TokenResult.RefreshToken=="") {
				return;
			}
			QboSettings settings=QboSettings.Load();
			TryRevoke(settings,pending.TokenResult.RefreshToken);
		}

		///<summary>The whole connect step in one call, in the old style: checks, exchanges, asks funcConfirmCompanyChange when another company was
		///connected (no answer means yes), and saves. The token's RequestException is set on failure; a declined company change returns a token with
		///both values blank and no exception.</summary>
		public static QuickBooksOnlineToken CompleteAuthorization(QboAuthRequest request,QboAuthorizationResult result,
			Func<QboPendingConnection,bool> funcConfirmCompanyChange=null)
		{
			Meth.NoCheckMiddleTierRole();
			try {
				QboPendingConnection pending=ExchangeAuthorization(request,result);
				if(pending.IsDifferentCompany && funcConfirmCompanyChange!=null && !funcConfirmCompanyChange(pending)) {
					DiscardPendingConnection(pending);
					return new QuickBooksOnlineToken("","");
				}
				if(!SaveConnection(pending,pending.IsDifferentCompany)) {
					return QuickBooksOnlineToken.FromException(new ODException("The connection could not be saved because encryption failed."));
				}
				return new QuickBooksOnlineToken(pending.TokenResult.AccessToken,"");
			}
			catch(Exception ex) {
				return QuickBooksOnlineToken.FromException(ex);
			}
		}

		///<summary>Revokes the stored refresh token at Intuit (errors are logged and ignored: the office may already have disconnected the app in
		///QuickBooks Online), then blanks the refresh token, realm ID, expiry and company name. The account and class lists stay. The caller signals
		///InvalidType.Programs.</summary>
		public static void Disconnect() {
			Meth.NoCheckMiddleTierRole();
			QboSettings settings=QboSettings.Load();
			if(settings.ProgramNum==0) {
				return;
			}
			string refreshToken=new QboProgramPropertyStore(settings.ProgramNum).ReadCurrent();
			if(refreshToken!="" && settings.ClientId!="" && settings.ClientSecret!="") {
				TryRevoke(settings,refreshToken);
			}
			QboSettings.ClearConnection();
			QboTokenManager.ClearAll();
		}

		private static void TryRevoke(QboSettings settings,string refreshToken) {
			try {
				QboOAuth qboOAuth=new QboOAuth(GetOAuthHttpClient(),settings.ClientId,settings.ClientSecret);
				VendorHttp.RunSync(() => qboOAuth.RevokeAsync(refreshToken));
			}
			catch(Exception ex) {
				Log("revoke failed: "+ex.GetType().Name);
			}
		}
		#endregion

		#region Tokens (kept for the callers' sake)
		///<summary>Makes sure a fresh access token is available, refreshing (and storing a rotated refresh token) when needed. The RefreshToken passed in
		///is not used: the stored one is read from the database. RequestException is set on failure.</summary>
		public static QuickBooksOnlineToken RefreshTokens(QuickBooksOnlineToken token) {
			Meth.NoCheckMiddleTierRole();
			try {
				QboSettings settings=GetConnectedSettings();
				QboTokenManager tokenManager=GetTokenManager(settings);
				tokenManager.Invalidate();
				OAuthAccessToken accessToken=VendorHttp.RunSync(() => tokenManager.GetAccessTokenAsync());
				return new QuickBooksOnlineToken(accessToken.Value,token?.RefreshToken??"");
			}
			catch(HttpRequestException ex) {
				return QuickBooksOnlineToken.FromException(new QboApiException(QboApiException.MessageUnreachable,innerException:ex));
			}
			catch(Exception ex) {
				return QuickBooksOnlineToken.FromException(ex);
			}
		}

		///<summary>True, after refreshing, when ex is an expired-token failure worth one more try. The API methods already refresh and retry once on 401,
		///so callers rarely see true.</summary>
		public static bool RefreshTokensIfNeeded(Exception ex,QuickBooksOnlineToken token) {
			Meth.NoCheckMiddleTierRole();
			QboAuthException exAuth=ex as QboAuthException;
			if(exAuth==null || exAuth.OAuthError!="" || !exAuth.NeedsReconnect) {
				return false;//Only a plain 401 from the API qualifies; a refused refresh token or wrong keys don't.
			}
			QuickBooksOnlineToken tokenNew=RefreshTokens(token);
			if(tokenNew.RequestException!=null) {
				return false;
			}
			if(token!=null) {
				token.AccessToken=tokenNew.AccessToken;
			}
			return true;
		}

		///<summary>True when a connection is saved but Intuit refused its refresh token earlier in this session. No network call.</summary>
		public static bool IsReconnectNeeded() {
			Meth.NoCheckMiddleTierRole();
			QboSettings settings=QboSettings.Load();
			if(!settings.IsConnected) {
				return false;
			}
			return GetTokenManager(settings).NeedsReconnect;
		}

		///<summary>A new idempotency key for one Send: 32 hex characters.</summary>
		public static string NewRequestId() {
			return Guid.NewGuid().ToString("N");
		}
		#endregion

		#region Accounts, classes and deposits
		///<summary>Active Bank and Other Current Asset accounts. Name is the fully qualified name. On failure, a one-element list whose RequestException is set.</summary>
		public static List<QuickBooksOnlineAccount> GetDepositAccounts(string realmId,QuickBooksOnlineToken token) {
			Meth.NoCheckMiddleTierRole();
			return GetAccounts(realmId,QboApiClient.ArrayDepositAccountTypes);
		}

		///<summary>Active Income and Other Income accounts. On failure, a one-element list whose RequestException is set.</summary>
		public static List<QuickBooksOnlineAccount> GetIncomeAccounts(string realmId,QuickBooksOnlineToken token) {
			Meth.NoCheckMiddleTierRole();
			return GetAccounts(realmId,QboApiClient.ArrayIncomeAccountTypes);
		}

		///<summary>Active classes. On failure, a one-element list whose RequestException is set.</summary>
		public static List<QuickBooksOnlineClassRef> GetClassRefs(string realmId,QuickBooksOnlineToken token) {
			Meth.NoCheckMiddleTierRole();
			try {
				QboApiClient qboApiClient=GetApiClient(realmId);
				List<QboEntity> listEntities=VendorHttp.RunSync(() => qboApiClient.GetClassesAsync());
				return listEntities.Select(x => {
					QuickBooksOnlineClassRef classRef=new QuickBooksOnlineClassRef(x.DisplayName,x.Id);
					classRef.FullyQualifiedName=x.DisplayName;
					return classRef;
				}).ToList();
			}
			catch(Exception ex) {
				return QuickBooksOnlineClassRef.FromException(ex);
			}
		}

		private static List<QuickBooksOnlineAccount> GetAccounts(string realmId,string[] arrayAccountTypes) {
			try {
				QboApiClient qboApiClient=GetApiClient(realmId);
				List<QboEntity> listEntities=VendorHttp.RunSync(() => qboApiClient.GetAccountsAsync(arrayAccountTypes));
				return listEntities.Select(x => {
					QuickBooksOnlineAccount account=new QuickBooksOnlineAccount(x.DisplayName,x.Id);
					account.FullyQualifiedName=x.DisplayName;
					return account;
				}).ToList();
			}
			catch(Exception ex) {
				return QuickBooksOnlineAccount.FromException(ex);
			}
		}

		///<summary>Creates a deposit with one line per amount (rounded to cents) to the income account, with the class when classRef is not null. memo goes
		///to PrivateNote (at most 4000 characters). requestId is the idempotency key: pass the same one for every retry of one Send; null makes a new one.
		///RequestException is set on failure.</summary>
		public static QuickBooksOnlineDepositResponse CreateDeposit(string realmId,QuickBooksOnlineToken token,QuickBooksOnlineAccount deposit,
			QuickBooksOnlineAccount income,QuickBooksOnlineClassRef classRef,List<decimal> amounts,string memo,DateTime date,string requestId=null)
		{
			Meth.NoCheckMiddleTierRole();
			try {
				if(deposit==null || string.IsNullOrEmpty(deposit.Id)) {
					throw new ODException("The deposit account was not found in the QuickBooks Online setup. Check Accounting > Setup > QuickBooks Online.");
				}
				if(income==null || string.IsNullOrEmpty(income.Id)) {
					throw new ODException("The income account was not found in the QuickBooks Online setup. Check Accounting > Setup > QuickBooks Online.");
				}
				QboDepositRequest depositRequest=new QboDepositRequest();
				depositRequest.DepositToAccountId=deposit.Id;
				depositRequest.IncomeAccountId=income.Id;
				depositRequest.ClassId=classRef?.Id??"";
				depositRequest.ListAmounts=(amounts??new List<decimal>()).ToList();
				depositRequest.Memo=memo??"";
				depositRequest.Date=date;
				string requestIdUsed=string.IsNullOrEmpty(requestId) ? NewRequestId() : requestId;
				QboApiClient qboApiClient=GetApiClient(realmId);
				QboDepositResult result=VendorHttp.RunSync(() => qboApiClient.CreateDepositAsync(depositRequest,requestIdUsed));
				QuickBooksOnlineDepositResponse response=new QuickBooksOnlineDepositResponse();
				response.DepositId=result.DepositId;
				response.IsClassRefMissing=result.IsClassRefMissing;
				return response;
			}
			catch(Exception ex) {
				return QuickBooksOnlineDepositResponse.FromException(ex);
			}
		}
		#endregion

		#region Wiring
		///<summary>The saved settings, or QboAuthException when nothing usable is connected. No network call.</summary>
		private static QboSettings GetConnectedSettings() {
			QboSettings settings=QboSettings.Load();
			if(!settings.IsConnected) {
				throw new QboAuthException(settings.GetNotConnectedMessage(),needsReconnect:true);
			}
			return settings;
		}

		private static QboApiClient GetApiClient(string realmId) {
			QboSettings settings=GetConnectedSettings();
			string realmIdUsed=string.IsNullOrEmpty(realmId) ? settings.RealmId : realmId;
			return new QboApiClient(GetApiHttpClient(settings.Environment),GetApiBaseUri(settings.Environment),realmIdUsed,GetTokenManager(settings));
		}

		private static QboTokenManager GetTokenManager(QboSettings settings) {
			return QboTokenManager.For(settings,() => {
				QboOAuth qboOAuth=new QboOAuth(GetOAuthHttpClient(),settings.ClientId,settings.ClientSecret);
				return new QboTokenManager(TokenCache.For(VendorName,settings.GetFingerprint()),new QboProgramPropertyStore(settings.ProgramNum),
					(refreshToken,ct) => qboOAuth.RefreshAsync(refreshToken,ct));
			});
		}

		///<summary>The Accounting API host for the environment, or the developer override (Introspection QuickBooksOnlineSandboxUrl, for a mock server).</summary>
		internal static Uri GetApiBaseUri(QboEnvironment environment) {
			string urlOverride;
			Uri uriOverride;
			if(Introspection.TryGetOverride(Introspection.IntrospectionEntity.QuickBooksOnlineSandboxUrl,out urlOverride)
				&& !string.IsNullOrWhiteSpace(urlOverride) && Uri.TryCreate(urlOverride.Trim().TrimEnd('/')+"/",UriKind.Absolute,out uriOverride)) {
				return uriOverride;
			}
			return QboApiClient.GetBaseUri(environment);
		}

		private static HttpClient GetOAuthHttpClient() {
			VendorHttpOptions options=new VendorHttpOptions();
			options.VendorName=VendorName+" OAuth";
			options.ListAllowedHosts=QboOAuth.ArrayAllowedHosts.ToList();
			options.Log=Log;
			return VendorHttp.GetClient(options);
		}

		private static HttpClient GetApiHttpClient(QboEnvironment environment) {
			Uri uriBase=GetApiBaseUri(environment);
			VendorHttpOptions options=new VendorHttpOptions();
			options.VendorName=VendorName;
			options.BaseAddress=uriBase;
			options.ListAllowedHosts=new List<string> { uriBase.Host };
			options.Log=Log;
			return VendorHttp.GetClient(options);
		}

		///<summary>One redacted line: endpoint, status and intuit_tid. Never tokens, codes, the secret, memos or amounts.</summary>
		private static void Log(string line) {
			try {
				Logger.WriteLine(line,VendorName);
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
		}

		///<summary>Holds the refresh token of a connection that isn't saved yet. Never writes.</summary>
		private sealed class PendingStore : IQboConnectionStore {
			private readonly string _refreshToken;

			public PendingStore(string refreshToken) {
				_refreshToken=refreshToken;
			}

			public string ReadCurrent() {
				return _refreshToken;
			}

			public bool TryReplace(string expectedCurrent,string newValue) {
				return false;
			}

			public DateTime? ReadRefreshTokenExpiresUtc() {
				return null;
			}

			public void WriteRefreshTokenExpiresUtc(DateTime utc) {
			}

			public void SignalChanged() {
			}
		}
		#endregion
	}
}
