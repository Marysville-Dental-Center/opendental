// Global Payments V2: the HTTP client for Global Payments' API, written from its public developer documentation (developer.globalpayments.com):
//  - POST /ucp/accesstoken (an access token for the office's own app: app_id, a nonce and secret = SHA-512 of nonce + app_key, never the key);
//  - POST /ucp/device_commands (Cloud Integration: a command for a card terminal, and TRANSACTION_LIST to read a command's result);
//  - POST /ucp/transactions (a sale or refund on a saved card) and POST /ucp/transactions/{id}/reversal (a void).
// Hosts: apis.globalpay.com and apis.sandbox.globalpay.com only. Requests that move money are never retried automatically; the Transactions API calls
// carry an X-GP-Idempotency key. Tokens, the app key, card tokens and bodies are never logged.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CodeBase;
using CodeBase.VendorDirect;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenPmsDentalBusiness.VendorDirect.GlobalPayments {
	///<summary>Why a call to Global Payments failed.</summary>
	public enum GpV2ErrorKind {
		///<summary>No App ID or App Key. No call was made.</summary>
		NotConfigured,
		///<summary>Global Payments refused the app credentials (the access token request answered 400, 401 or 403).</summary>
		Credentials,
		///<summary>Global Payments answered with an error status for the request itself (4xx other than 401). Nothing was processed.</summary>
		Rejected,
		///<summary>No answer, a timeout, a 5xx or an answer this program can't read. For a request that moves money the outcome is unknown.</summary>
		Offline,
	}

	///<summary>A call to Global Payments that got no usable answer. The message is safe to show and to log: it never holds a token, key or card token.
	///</summary>
	[Serializable]
	public class GpV2Exception:Exception {
		public GpV2ErrorKind Kind { get; private set; }
		///<summary>The HTTP status, or 0 without an answer.</summary>
		public int HttpStatus { get; private set; }
		///<summary>Global Payments' error code (error_code), or "".</summary>
		public string ErrorCode { get; private set; }

		public GpV2Exception(GpV2ErrorKind kind,int httpStatus,string errorCode,string message,Exception innerException=null)
			:base(message,innerException)
		{
			Kind=kind;
			HttpStatus=httpStatus;
			ErrorCode=errorCode??"";
		}

		protected GpV2Exception(System.Runtime.Serialization.SerializationInfo info,System.Runtime.Serialization.StreamingContext context)
			:base(info,context)
		{
		}
	}

	///<summary>One account the access token lists (scope.accounts).</summary>
	public sealed class GpV2Account {
		public string Id="";
		public string Name="";
		public List<string> ListPermissions=new List<string>();
	}

	///<summary>The answer to a device command or a TRANSACTION_LIST query, parsed.</summary>
	public sealed class GpV2DeviceAnswer {
		///<summary>The device command ID (DVC_...).</summary>
		public string CommandId="";
		public string Status="";
		///<summary>The whole answer, for GpV2Json.ReadTransaction.</summary>
		public JObject JObjectAnswer;
		public bool IsPending {
			get {
				return GpV2Json.IsDeviceCommandPending(JObjectAnswer);
			}
		}
	}

	///<summary>Talks to Global Payments directly with the office's own API app. The access token is kept in memory only, per configuration fingerprint,
	///and requested again 5 minutes before it expires; a 401 gets one new token and one resend (a 401 means nothing was processed).</summary>
	public class GpV2Client {
		public const string HostProduction="apis.globalpay.com";
		public const string HostSandbox="apis.sandbox.globalpay.com";
		///<summary>The API version header every endpoint requires (X-GP-Version). The access token reference documents this value; the Cloud Integration
		///page's samples show a different date format, so the same documented value is sent to every endpoint (INFERRED).</summary>
		public const string ApiVersion="2021-03-22";
		///<summary>Timeout for one call. A terminal transaction is a series of short calls (start, then a query every few seconds).</summary>
		public static readonly TimeSpan CallTimeout=TimeSpan.FromSeconds(30);
		private const string VENDOR_NAME="GlobalPaymentsV2";
		private readonly HttpMessageHandler _httpMessageHandler;
		private readonly Func<DateTime> _funcUtcNow;
		private readonly Action<string> _actionLog;
		private readonly ConcurrentDictionary<bool,HttpClient> _dictHttpClients=new ConcurrentDictionary<bool,HttpClient>();
		private readonly ConcurrentDictionary<string,TokenCache> _dictTokenCaches=new ConcurrentDictionary<string,TokenCache>();
		///<summary>The accounts each configuration's latest token listed, by token cache key.</summary>
		private readonly ConcurrentDictionary<string,List<GpV2Account>> _dictAccounts=new ConcurrentDictionary<string,List<GpV2Account>>();

		///<summary>httpMessageHandler is for tests (a fake handler); null uses the network through VendorHttp. funcUtcNow is the clock for token lifetimes
		///and nonces. actionLog receives redacted lines (endpoint, status, error code); null writes them to the program's "GlobalPaymentsV2" log.</summary>
		public GpV2Client(HttpMessageHandler httpMessageHandler=null,Func<DateTime> funcUtcNow=null,Action<string> actionLog=null) {
			_httpMessageHandler=httpMessageHandler;
			_funcUtcNow=funcUtcNow??(() => DateTime.UtcNow);
			_actionLog=actionLog??(x => ODException.SwallowAnyException(() => Logger.WriteLine(x,VENDOR_NAME)));
		}

		///<summary>True if this client sends through a test handler.</summary>
		public bool HasTestHandler {
			get {
				return _httpMessageHandler!=null;
			}
		}

		///<summary>True if this client sends through httpMessageHandler.</summary>
		public bool UsesHandler(HttpMessageHandler httpMessageHandler) {
			return _httpMessageHandler!=null && ReferenceEquals(_httpMessageHandler,httpMessageHandler);
		}

		///<summary>https://apis.globalpay.com/ucp/ or https://apis.sandbox.globalpay.com/ucp/.</summary>
		public static Uri GetBaseUri(bool isSandbox) {
			return new Uri("https://"+(isSandbox ? HostSandbox : HostProduction)+"/ucp/");
		}

		private HttpClient GetHttpClient(bool isSandbox) {
			return _dictHttpClients.GetOrAdd(isSandbox,x => {
				VendorHttpOptions options=new VendorHttpOptions();
				options.VendorName=VENDOR_NAME;
				options.BaseAddress=GetBaseUri(x);
				options.ListAllowedHosts=new List<string> { GetBaseUri(x).Host };
				options.Timeout=CallTimeout;
				//Every call here is a POST, which VendorHttp never retries. Money must not move twice.
				options.MaxGetRetries=0;
				options.Log=_actionLog;
				if(_httpMessageHandler!=null) {
					return VendorHttp.CreateClient(options,_httpMessageHandler);
				}
				return VendorHttp.GetClient(options);
			});
		}

		private static string GetCacheKey(GlobalPaymentsV2Config config) {
			string key=config.Fingerprint;
			if(string.IsNullOrEmpty(key)) {
				//A config built by hand (Test connection, tests) gets a fingerprint of its plain values. It stays in memory only.
				key=TokenCache.Fingerprint(config.AppId,config.AppKey,config.AccountName);
			}
			return (config.UseSandbox ? "sandbox|" : "production|")+key;
		}

		private TokenCache GetTokenCache(GlobalPaymentsV2Config config) {
			return _dictTokenCaches.GetOrAdd(GetCacheKey(config),x => new TokenCache(TokenCache.DefaultRefreshMargin,_funcUtcNow));
		}

		///<summary>Forgets every cached token and rejection in this process, e.g. after the setup window saves.</summary>
		public void ResetAll() {
			_dictTokenCaches.Clear();
			_dictAccounts.Clear();
		}

		///<summary>Forgets the cached token and any earlier rejection for this configuration (Test connection).</summary>
		public void Reset(GlobalPaymentsV2Config config) {
			TokenCache tokenCache=GetTokenCache(config);
			tokenCache.Invalidate();
			tokenCache.ClearReauthorization();
		}

		///<summary>The secret of an access token request: the SHA-512 of nonce followed by appKey, as lowercase hex (Global Payments' documentation; the
		///hex encoding matches its published example).</summary>
		public static string ComputeSecret(string nonce,string appKey) {
			using(SHA512 sha512=SHA512.Create()) {
				byte[] arrayHash=sha512.ComputeHash(Encoding.UTF8.GetBytes((nonce??"")+(appKey??"")));
				StringBuilder stringBuilder=new StringBuilder(arrayHash.Length*2);
				for(int i=0;i<arrayHash.Length;i++) {
					stringBuilder.Append(arrayHash[i].ToString("x2",CultureInfo.InvariantCulture));
				}
				return stringBuilder.ToString();
			}
		}

		///<summary>A nonce for one token request: the UTC time to the millisecond (the form Global Payments' documentation suggests) plus a random
		///suffix, so two workstations never send the same one. At most 50 characters.</summary>
		public string NewNonce() {
			return _funcUtcNow().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'",CultureInfo.InvariantCulture)+"-"+Guid.NewGuid().ToString("N").Substring(0,12);
		}

		///<summary>The cached access token, or a new one. Throws GpV2Exception.</summary>
		public async Task<OAuthAccessToken> GetAccessTokenAsync(GlobalPaymentsV2Config config,CancellationToken cancellationToken) {
			ThrowIfIncomplete(config);
			try {
				return await GetTokenCache(config).GetAsync(ct => RequestTokenAsync(config,ct),cancellationToken).ConfigureAwait(false);
			}
			catch(Exception ex) {
				throw MapException(ex,isMoneyRequest:false);
			}
		}

		///<summary>The accounts the latest access token for config listed (scope.accounts), or an empty list before the first token.</summary>
		public List<GpV2Account> GetAccounts(GlobalPaymentsV2Config config) {
			List<GpV2Account> listAccounts;
			if(_dictAccounts.TryGetValue(GetCacheKey(config),out listAccounts)) {
				return listAccounts.ToList();
			}
			return new List<GpV2Account>();
		}

		///<summary>The account name for charges on saved cards: the configured one, else the first account the token lists with permission to
		///authorize (TRN_POST_Authorize), else the first account. "" if the token listed none.</summary>
		public string GetAccountNameForCharges(GlobalPaymentsV2Config config) {
			if(!string.IsNullOrWhiteSpace(config.AccountName)) {
				return config.AccountName.Trim();
			}
			List<GpV2Account> listAccounts=GetAccounts(config);
			GpV2Account account=listAccounts.FirstOrDefault(x => x.ListPermissions.Any(y => y.Equals("TRN_POST_Authorize",StringComparison.OrdinalIgnoreCase)))
				??listAccounts.FirstOrDefault();
			return account?.Name??"";
		}

		///<summary>POST accesstoken. The app key itself is never sent.</summary>
		private async Task<OAuthAccessToken> RequestTokenAsync(GlobalPaymentsV2Config config,CancellationToken cancellationToken) {
			string nonce=NewNonce();
			JObject jObjectBody=new JObject();
			jObjectBody["app_id"]=config.AppId.Trim();
			jObjectBody["nonce"]=nonce;
			jObjectBody["secret"]=ComputeSecret(nonce,config.AppKey);
			jObjectBody["grant_type"]="client_credentials";
			HttpClient httpClient=GetHttpClient(config.UseSandbox);
			using(HttpRequestMessage request=CreateRequest(config.UseSandbox,"accesstoken",jObjectBody,"accesstoken")) {
				using(HttpResponseMessage response=await httpClient.SendAsync(request,cancellationToken).ConfigureAwait(false)) {
					string body=(response.Content==null) ? "" : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
					int status=(int)response.StatusCode;
					if(status==400 || status==401 || status==403) {
						//Throws TokenRejectedException, which marks this configuration as rejected until its settings change or Reset is called.
						throw new TokenRejectedException(GpV2Json.ReadErrorCode(body),"Global Payments refused the App ID or App Key. "
							+GpV2Json.ReadErrorMessage(body),status);
					}
					if(status<200 || status>299) {
						throw new GpV2Exception(GpV2ErrorKind.Offline,status,GpV2Json.ReadErrorCode(body),
							"Global Payments is not available right now (HTTP "+status+").");
					}
					JObject jObject=GpV2Json.TryParseObject(body);
					string token=GpV2Json.GetString(jObject,"token");
					if(token=="") {
						throw new GpV2Exception(GpV2ErrorKind.Offline,status,"","Global Payments sent an access token answer this program could not read.");
					}
					OAuthAccessToken accessToken=new OAuthAccessToken();
					accessToken.Value=token;
					string type=GpV2Json.GetString(jObject,"type");
					accessToken.TokenType=type.Equals("Bearer",StringComparison.OrdinalIgnoreCase) || type=="" ? "Bearer" : type;
					accessToken.IssuedUtc=_funcUtcNow();
					long secondsToExpire;
					if(!long.TryParse(GpV2Json.GetString(jObject,"seconds_to_expire"),NumberStyles.Integer,CultureInfo.InvariantCulture,out secondsToExpire)
						|| secondsToExpire<=0)
					{
						secondsToExpire=600;//No lifetime given: assume a short one, so a stale token is not kept.
					}
					accessToken.ExpiresUtc=accessToken.IssuedUtc.AddSeconds(secondsToExpire);
					_dictAccounts[GetCacheKey(config)]=ReadAccounts(jObject);
					return accessToken;
				}
			}
		}

		///<summary>scope.accounts of a token answer.</summary>
		public static List<GpV2Account> ReadAccounts(JObject jObjectToken) {
			List<GpV2Account> listAccounts=new List<GpV2Account>();
			JToken jTokenAccounts=jObjectToken?["scope"]?["accounts"];
			if(!(jTokenAccounts is JArray jArray)) {
				return listAccounts;
			}
			foreach(JObject jObjectAccount in jArray.OfType<JObject>()) {
				GpV2Account account=new GpV2Account();
				account.Id=GpV2Json.GetString(jObjectAccount,"id");
				account.Name=GpV2Json.GetString(jObjectAccount,"name");
				if(jObjectAccount["permissions"] is JArray jArrayPermissions) {
					account.ListPermissions=jArrayPermissions.Select(x => x.Type==JTokenType.String ? (string)x : "").Where(x => x!="").ToList();
				}
				listAccounts.Add(account);
			}
			return listAccounts;
		}

		private HttpRequestMessage CreateRequest(bool isSandbox,string relativePath,JObject jObjectBody,string endpointName,string idempotencyKey="") {
			HttpRequestMessage request=new HttpRequestMessage(HttpMethod.Post,new Uri(GetBaseUri(isSandbox),relativePath));
			request.Content=new StringContent(jObjectBody.ToString(Formatting.None),Encoding.UTF8,"application/json");
			request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
			request.Headers.Add("X-GP-Version",ApiVersion);
			if(!string.IsNullOrEmpty(idempotencyKey)) {
				request.Headers.Add("X-GP-Idempotency",idempotencyKey);
			}
			VendorHttp.SetEndpointName(request,endpointName);
			VendorHttp.SetTimeout(request,CallTimeout);
			VendorHttp.SetNoRetry(request);
			return request;
		}

		///<summary>Sends an authorized POST and returns the status and body. Throws GpV2Exception for no answer (Offline) or rejected credentials.</summary>
		private async Task<Tuple<int,string>> PostAsync(GlobalPaymentsV2Config config,string relativePath,JObject jObjectBody,string endpointName,
			bool isMoneyRequest,CancellationToken cancellationToken,string idempotencyKey="")
		{
			ThrowIfIncomplete(config);
			HttpClient httpClient=GetHttpClient(config.UseSandbox);
			TokenCache tokenCache=GetTokenCache(config);
			HttpResponseMessage response;
			try {
				response=await tokenCache.SendWithAuthRetryAsync(httpClient,
					() => CreateRequest(config.UseSandbox,relativePath,jObjectBody,endpointName,idempotencyKey),
					ct => RequestTokenAsync(config,ct),cancellationToken).ConfigureAwait(false);
			}
			catch(Exception ex) {
				throw MapException(ex,isMoneyRequest);
			}
			using(response) {
				string body=(response.Content==null) ? "" : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
				return Tuple.Create((int)response.StatusCode,body);
			}
		}

		///<summary>Throws GpV2Exception for a status that isn't 2xx. 5xx is Offline (the outcome of a money request is then unknown); 4xx is Rejected.
		///</summary>
		private static void ThrowIfError(int status,string body,string what) {
			if(status>=200 && status<=299) {
				return;
			}
			string message=GpV2Json.ReadErrorMessage(body);
			string code=GpV2Json.ReadErrorCode(body);
			if(status==401) {
				throw new GpV2Exception(GpV2ErrorKind.Credentials,status,code,"Global Payments refused the App ID or App Key."
					+(message=="" ? "" : " "+message));
			}
			if(status>=500 || status==0) {
				throw new GpV2Exception(GpV2ErrorKind.Offline,status,code,"Global Payments is not available right now (HTTP "+status+")."
					+(message=="" ? "" : " "+message));
			}
			throw new GpV2Exception(GpV2ErrorKind.Rejected,status,code,"Global Payments did not accept the "+what+" (HTTP "+status+")."
				+(message=="" ? "" : " "+message));
		}

		#region Device commands (card terminals)
		///<summary>The action_type of a device command for a transaction type. Upper case, as the published request sample sends it.</summary>
		public static string GetDeviceActionType(GpV2TransType transType) {
			switch(transType) {
				case GpV2TransType.Sale:
					return "AUTHORIZE";
				case GpV2TransType.Refund:
					return "REFUND";
				default:
					throw new ArgumentException("A void is not a terminal command; it goes through the Transactions API.");
			}
		}

		///<summary>Sends a sale or refund to a terminal (device_reference is the terminal's ID as entered in setup). Returns the interim answer with the
		///device command ID. Throws GpV2Exception; an Offline failure means the terminal may or may not have received it.</summary>
		public async Task<GpV2DeviceAnswer> StartDeviceTransactionAsync(GlobalPaymentsV2Config config,string deviceReference,GpV2TransType transType,
			decimal amount,CancellationToken cancellationToken)
		{
			JObject jObjectBody=new JObject();
			jObjectBody["device_reference"]=(deviceReference??"").Trim();
			jObjectBody["action_type"]=GetDeviceActionType(transType);
			JObject jObjectTransaction=new JObject();
			jObjectTransaction["amount"]=GlobalPaymentsV2.ToMinorUnits(amount).ToString(CultureInfo.InvariantCulture);
			jObjectBody["transaction"]=jObjectTransaction;
			Tuple<int,string> tupleResponse=await PostAsync(config,"device_commands",jObjectBody,"device command",isMoneyRequest:true,cancellationToken)
				.ConfigureAwait(false);
			ThrowIfError(tupleResponse.Item1,tupleResponse.Item2,"terminal command");
			return ReadDeviceAnswer(tupleResponse.Item2);
		}

		///<summary>Reads the result of a device command (action_type TRANSACTION_LIST with the command ID). Throws GpV2Exception.</summary>
		public async Task<GpV2DeviceAnswer> GetDeviceCommandResultAsync(GlobalPaymentsV2Config config,string deviceReference,string commandId,
			CancellationToken cancellationToken)
		{
			JObject jObjectBody=new JObject();
			jObjectBody["id"]=commandId??"";
			jObjectBody["device_reference"]=(deviceReference??"").Trim();
			jObjectBody["action_type"]="TRANSACTION_LIST";
			Tuple<int,string> tupleResponse=await PostAsync(config,"device_commands",jObjectBody,"device command result",isMoneyRequest:false,
				cancellationToken).ConfigureAwait(false);
			ThrowIfError(tupleResponse.Item1,tupleResponse.Item2,"result query");
			GpV2DeviceAnswer answer=ReadDeviceAnswer(tupleResponse.Item2);
			if(answer.CommandId=="") {
				answer.CommandId=commandId??"";
			}
			return answer;
		}

		///<summary>Checks that Global Payments can reach a terminal (action_type PING). Moves no money. Returns the command status. Throws GpV2Exception.
		///</summary>
		public async Task<string> PingDeviceAsync(GlobalPaymentsV2Config config,string deviceReference,CancellationToken cancellationToken) {
			JObject jObjectBody=new JObject();
			jObjectBody["device_reference"]=(deviceReference??"").Trim();
			jObjectBody["action_type"]="PING";
			Tuple<int,string> tupleResponse=await PostAsync(config,"device_commands",jObjectBody,"device ping",isMoneyRequest:false,cancellationToken)
				.ConfigureAwait(false);
			ThrowIfError(tupleResponse.Item1,tupleResponse.Item2,"terminal check");
			return ReadDeviceAnswer(tupleResponse.Item2).Status;
		}

		private static GpV2DeviceAnswer ReadDeviceAnswer(string body) {
			JObject jObject=GpV2Json.TryParseObject(body);
			if(jObject==null) {
				throw new GpV2Exception(GpV2ErrorKind.Offline,200,"","Global Payments sent a terminal answer this program could not read.");
			}
			GpV2DeviceAnswer answer=new GpV2DeviceAnswer();
			answer.JObjectAnswer=jObject;
			answer.CommandId=GpV2Json.GetString(jObject,"id");
			answer.Status=GpV2Json.GetString(jObject,"status");
			return answer;
		}
		#endregion Device commands (card terminals)

		#region Transactions API (saved cards, voids)
		///<summary>A sale or refund on a saved card (payment_method.id, a PMT_ token), card not present. reference (8 to 50 characters) is also the
		///idempotency key. Returns the finished result; a decline is a result, not an exception. Throws GpV2Exception; an Offline failure means the
		///charge may or may not have gone through.</summary>
		public async Task<GlobalPaymentsV2Result> ChargeSavedCardAsync(GlobalPaymentsV2Config config,GpV2TransType transType,string paymentMethodId,
			decimal amount,string reference,bool isRecurring,CancellationToken cancellationToken)
		{
			if(transType==GpV2TransType.Void) {
				throw new ArgumentException("Use ReverseAsync for a void.");
			}
			if(string.IsNullOrWhiteSpace(paymentMethodId)) {
				throw new GpV2Exception(GpV2ErrorKind.NotConfigured,0,"","The saved card has no Global Payments card reference.");
			}
			ThrowIfIncomplete(config);
			//The account name may come from the token's account list, so get the token first.
			await GetAccessTokenAsync(config,cancellationToken).ConfigureAwait(false);
			string accountName=GetAccountNameForCharges(config);
			if(accountName=="") {
				throw new GpV2Exception(GpV2ErrorKind.NotConfigured,0,"","No Global Payments account name. Enter it in Setup > Program Links > "
					+GlobalPaymentsV2.DisplayName+".");
			}
			JObject jObjectBody=new JObject();
			jObjectBody["account_name"]=accountName;
			jObjectBody["type"]=(transType==GpV2TransType.Refund) ? "REFUND" : "SALE";
			jObjectBody["channel"]="CNP";
			jObjectBody["capture_mode"]="AUTO";
			jObjectBody["amount"]=GlobalPaymentsV2.ToMinorUnits(amount).ToString(CultureInfo.InvariantCulture);
			jObjectBody["currency"]=GlobalPaymentsV2.Currency;
			jObjectBody["country"]=GlobalPaymentsV2.Country;
			jObjectBody["reference"]=reference;
			JObject jObjectPaymentMethod=new JObject();
			jObjectPaymentMethod["id"]=paymentMethodId.Trim();
			jObjectPaymentMethod["entry_mode"]="ECOM";
			jObjectBody["payment_method"]=jObjectPaymentMethod;
			if(isRecurring && transType==GpV2TransType.Sale) {
				//A merchant-initiated charge on a stored card (the Transactions API's stored_credential). INFERRED values.
				JObject jObjectStoredCredential=new JObject();
				jObjectStoredCredential["model"]="RECURRING";
				jObjectStoredCredential["reason"]="RECURRING";
				jObjectStoredCredential["sequence"]="SUBSEQUENT";
				jObjectBody["stored_credential"]=jObjectStoredCredential;
			}
			Tuple<int,string> tupleResponse=await PostAsync(config,"transactions",jObjectBody,"transaction",isMoneyRequest:true,cancellationToken,
				idempotencyKey:reference).ConfigureAwait(false);
			return ReadTransactionAnswer(tupleResponse.Item1,tupleResponse.Item2,transType,amount,config.UseSandbox);
		}

		///<summary>Reverses (voids) a transaction by its ID; amount null reverses the full amount. Returns the finished result. Throws GpV2Exception.
		///</summary>
		public async Task<GlobalPaymentsV2Result> ReverseAsync(GlobalPaymentsV2Config config,string transactionId,decimal? amount,
			CancellationToken cancellationToken)
		{
			if(string.IsNullOrWhiteSpace(transactionId)) {
				throw new GpV2Exception(GpV2ErrorKind.NotConfigured,0,"","There is no Global Payments transaction to void.");
			}
			JObject jObjectBody=new JObject();
			if(amount.HasValue) {
				jObjectBody["amount"]=GlobalPaymentsV2.ToMinorUnits(amount.Value).ToString(CultureInfo.InvariantCulture);
			}
			string idempotencyKey="VOID-"+transactionId.Trim();
			if(idempotencyKey.Length>50) {
				idempotencyKey=idempotencyKey.Substring(0,50);
			}
			Tuple<int,string> tupleResponse=await PostAsync(config,"transactions/"+Uri.EscapeDataString(transactionId.Trim())+"/reversal",jObjectBody,
				"reversal",isMoneyRequest:true,cancellationToken,idempotencyKey:idempotencyKey).ConfigureAwait(false);
			GlobalPaymentsV2Result result=ReadTransactionAnswer(tupleResponse.Item1,tupleResponse.Item2,GpV2TransType.Void,amount??0,config.UseSandbox);
			if(result.TransactionId=="") {
				result.TransactionId=transactionId.Trim();
			}
			return result;
		}

		///<summary>A Transactions API answer as a result. A 2xx is read with GpV2Json.ReadTransaction. A 4xx that is a decline (status DECLINED in the
		///body) is a declined result; other errors throw.</summary>
		private static GlobalPaymentsV2Result ReadTransactionAnswer(int status,string body,GpV2TransType transType,decimal amountAsked,bool isSandbox) {
			GlobalPaymentsV2Result result=new GlobalPaymentsV2Result();
			result.TransType=transType;
			result.Amount=amountAsked;
			result.IsSandbox=isSandbox;
			JObject jObject=GpV2Json.TryParseObject(body);
			if(status>=200 && status<=299) {
				if(jObject==null) {
					throw new GpV2Exception(GpV2ErrorKind.Offline,status,"","Global Payments sent an answer this program could not read.");
				}
				GpV2Json.ReadTransaction(result,jObject);
				return result;
			}
			if(jObject!=null && GpV2Json.GetString(jObject,"status").Equals("DECLINED",StringComparison.OrdinalIgnoreCase)) {
				GpV2Json.ReadTransaction(result,jObject);
				result.Outcome=GpV2Outcome.Declined;
				if(result.Message=="") {
					result.Message=GpV2Json.ReadErrorMessage(body);
				}
				return result;
			}
			ThrowIfError(status,body,"request");
			return result;//Not reached: ThrowIfError throws for every non-2xx status.
		}
		#endregion Transactions API (saved cards, voids)

		private static void ThrowIfIncomplete(GlobalPaymentsV2Config config) {
			if(config==null || !config.IsComplete) {
				throw new GpV2Exception(GpV2ErrorKind.NotConfigured,0,"",GlobalPaymentsV2.DisplayName+" needs an App ID and App Key.");
			}
		}

		///<summary>Maps whatever the token cache or HTTP stack threw to a GpV2Exception.</summary>
		private static GpV2Exception MapException(Exception ex,bool isMoneyRequest) {
			if(ex is AggregateException aggregateException && aggregateException.InnerExceptions.Count==1) {
				ex=aggregateException.InnerExceptions[0];
			}
			if(ex is GpV2Exception gpV2Exception) {
				return gpV2Exception;
			}
			if(ex is TokenRejectedException tokenRejectedException) {
				return new GpV2Exception(GpV2ErrorKind.Credentials,tokenRejectedException.StatusCode,tokenRejectedException.Error,
					tokenRejectedException.Message.Trim(),ex);
			}
			if(ex is ReauthorizationRequiredException) {
				return new GpV2Exception(GpV2ErrorKind.Credentials,0,"","Global Payments refused the App ID or App Key earlier in this session. "
					+"Check them in Setup > Program Links > "+GlobalPaymentsV2.DisplayName+" and use Test connection.",ex);
			}
			if(ex is OperationCanceledException && !(ex is TaskCanceledException)) {
				return new GpV2Exception(GpV2ErrorKind.Offline,0,"","Stopped before Global Payments answered."
					+(isMoneyRequest ? " The transaction may or may not have gone through." : ""),ex);
			}
			//VendorHttpException (refused URL, timeout), HttpRequestException (network), TaskCanceledException, an unreadable answer.
			return new GpV2Exception(GpV2ErrorKind.Offline,0,"","Could not reach Global Payments."
				+(isMoneyRequest ? " The transaction may or may not have gone through." : ""),ex);
		}
	}
}
