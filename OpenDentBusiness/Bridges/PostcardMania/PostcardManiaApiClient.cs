// PostcardMania (PCM Integrations) DirectMail API v3 client. Talks to the vendor directly with the office's own API key and secret.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CodeBase;
using CodeBase.VendorDirect;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenDentBusiness.PostcardMania {
	//OpenDentBusiness.Task (the task table) would hide System.Threading.Tasks.Task inside this namespace.
	using Task=System.Threading.Tasks.Task;

	///<summary>The DirectMail API v3 over VendorHttp (HTTPS only, never an Open Dental host, redacted logging).
	///Auth: POST /auth/login with the key and secret returns a bearer token that lasts about 15 minutes. There is no refresh endpoint, so the client
	///logs in again when the token is within 60 seconds of expiry, or once after a 401. Tokens live in memory only, per key, child reference and
	///secret. 429s are retried up to 3 tries, waiting for Retry-After (at most 30 seconds); nothing is processed or charged on a 429. Placing an
	///order is never retried after a timeout or a 5xx: the caller looks it up by its extRefNbr (AdvertisingPostcards.PlaceOrder).</summary>
	public class PostcardManiaApiClient:IPostcardManiaApiClient {
		public const string VendorName="PostcardMania";
		///<summary>Production and sandbox share this host; the API key decides which one a call reaches.</summary>
		public const string DefaultBaseUrl="https://v3.pcmintegrations.com/";
		///<summary>Tokens are renewed this long before they expire.</summary>
		public static readonly TimeSpan TokenRefreshMargin=TimeSpan.FromSeconds(60);
		///<summary>Tries for a request that keeps getting 429, the first one included.</summary>
		public const int MaxRateLimitTries=3;
		///<summary>A Retry-After longer than this is not waited for; the 429 is reported instead.</summary>
		public static readonly TimeSpan MaxRetryAfter=TimeSpan.FromSeconds(30);
		///<summary>Orders and estimates can carry up to 50,000 recipients.</summary>
		public static readonly TimeSpan OrderTimeout=TimeSpan.FromSeconds(120);
		private const int MAX_PROOF_BYTES=15*1024*1024;

		private static readonly object _lockInst=new object();
		private static IPostcardManiaApiClient _inst;
		private static readonly Random _random=new Random();

		private readonly HttpClient _httpClient;
		private readonly HttpClient _httpClientProof;
		private readonly Func<DateTime> _funcUtcNow;
		private readonly Func<TimeSpan,CancellationToken,Task> _funcDelay;
		private readonly ConcurrentDictionary<string,TokenCache> _dictTokenCaches=new ConcurrentDictionary<string,TokenCache>();

		///<summary>The client the business layer uses. Created on first use (so the base URL override is read after the database is open).
		///Tests set it to a PostcardManiaApiClientMock or to a client over a fake handler.</summary>
		public static IPostcardManiaApiClient Inst {
			get {
				lock(_lockInst) {
					if(_inst==null) {
						_inst=new PostcardManiaApiClient();
					}
					return _inst;
				}
			}
			set {
				lock(_lockInst) {
					_inst=value;
				}
			}
		}

		///<summary>The production client: the shared VendorHttp client for the API host (or the PostcardManiaApiUrl introspection override).</summary>
		public PostcardManiaApiClient() : this(null) {
		}

		///<summary>innerHandler null uses the real network. Tests pass a fake handler, a base URL, a clock, a delay function (to skip real waits)
		///and may adjust the VendorHttp options (e.g. a short GET retry delay).</summary>
		public PostcardManiaApiClient(HttpMessageHandler innerHandler,string baseUrl=null,Func<DateTime> funcUtcNow=null,
			Func<TimeSpan,CancellationToken,Task> funcDelay=null,Action<VendorHttpOptions> actionConfigure=null)
		{
			_funcUtcNow=funcUtcNow??(() => DateTime.UtcNow);
			_funcDelay=funcDelay??new Func<TimeSpan,CancellationToken,Task>((timeSpan,cancellationToken) => Task.Delay(timeSpan,cancellationToken));
			Uri uriBase=new Uri(NormalizeBaseUrl(baseUrl??GetConfiguredBaseUrl()));
			VendorHttpOptions options=new VendorHttpOptions();
			options.VendorName=VendorName;
			options.BaseAddress=uriBase;
			options.ListAllowedHosts=new List<string> { uriBase.Host };
			options.MaxGetRetries=MaxRateLimitTries-1;
			options.MaxRetryWait=MaxRetryAfter;
			actionConfigure?.Invoke(options);
			VendorHttpOptions optionsProof=new VendorHttpOptions();
			optionsProof.VendorName=VendorName+" proofs";
			optionsProof.MaxGetRetries=1;
			actionConfigure?.Invoke(optionsProof);
			optionsProof.BaseAddress=null;
			optionsProof.ListAllowedHosts=new List<string>();
			if(innerHandler==null) {
				_httpClient=VendorHttp.GetClient(options);
				_httpClientProof=VendorHttp.GetClient(optionsProof);
			}
			else {
				_httpClient=VendorHttp.CreateClient(options,innerHandler);
				_httpClientProof=VendorHttp.CreateClient(optionsProof,innerHandler);
			}
		}

		///<summary>The API host this client talks to, for messages.</summary>
		public string Host {
			get {
				return _httpClient.BaseAddress.Host;
			}
		}

		///<summary>DefaultBaseUrl unless the PostcardManiaApiUrl introspection override points somewhere else (a mock server for tests).</summary>
		public static string GetConfiguredBaseUrl() {
			try {
				string overrideUrl;
				if(Introspection.TryGetOverride(Introspection.IntrospectionEntity.PostcardManiaApiUrl,out overrideUrl) && !string.IsNullOrWhiteSpace(overrideUrl)) {
					return overrideUrl.Trim();
				}
			}
			catch(Exception ex) {
				ex.DoNothing();//No database yet (tests): use the default.
			}
			return DefaultBaseUrl;
		}

		private static string NormalizeBaseUrl(string baseUrl) {
			return baseUrl.EndsWith("/") ? baseUrl : baseUrl+"/";
		}

		#region Public calls
		public PcmConnectionResult TestConnection(PostcardManiaAccount account) {
			return VendorHttp.RunSync(() => TestConnectionAsync(account,CancellationToken.None));
		}

		public PcmPagedResult<PcmDesign> GetDesigns(PostcardManiaAccount account,string productType,int page,int perPage) {
			string url="design?productType="+Uri.EscapeDataString(productType??"postcard")+"&page="+Math.Max(1,page)+"&perPage="+Math.Max(1,perPage);
			return VendorHttp.RunSync(() => GetPagedAsync<PcmDesign>(account,url,"design list",CancellationToken.None));
		}

		public PcmDesignEditorResponse GetDesignEditUrl(PostcardManiaAccount account,long designID,bool duplicate) {
			string url="design/"+designID.ToString(CultureInfo.InvariantCulture)+"/edit?mode=embed"+(duplicate ? "&duplicate=true" : "");
			return VendorHttp.RunSync(() => SendAndReadAsync<PcmDesignEditorResponse>(account,HttpMethod.Get,url,null,"design edit",null,
				CancellationToken.None));
		}

		public PcmDesignEditorResponse CreateDesign(PostcardManiaAccount account,string name,string size) {
			PcmNewDesignRequest request=new PcmNewDesignRequest { Name=name,Size=size };
			return VendorHttp.RunSync(() => SendAndReadAsync<PcmDesignEditorResponse>(account,HttpMethod.Post,"design/custom",request,"design create",null,
				CancellationToken.None));
		}

		public PcmAddressVerificationResponse VerifyAddresses(PostcardManiaAccount account,List<PcmAddress> listAddresses) {
			return VendorHttp.RunSync(() => SendAndReadAsync<PcmAddressVerificationResponse>(account,HttpMethod.Post,"recipient/verify",
				listAddresses??new List<PcmAddress>(),"recipient verify",OrderTimeout,CancellationToken.None));
		}

		public PcmPricingResponse EstimatePostcardOrder(PostcardManiaAccount account,PcmPostcardOrderRequest request) {
			return VendorHttp.RunSync(() => SendAndReadAsync<PcmPricingResponse>(account,HttpMethod.Post,"order/postcard/pricing",request,"order pricing",
				OrderTimeout,CancellationToken.None));
		}

		public PcmOrderResponse PlacePostcardOrder(PostcardManiaAccount account,PcmPostcardOrderRequest request) {
			if(request==null || string.IsNullOrWhiteSpace(request.ExtRefNbr)) {
				//The reference is what makes a retry safe (409) and lets a timed-out order be found again.
				throw new ArgumentException("An order needs an extRefNbr.",nameof(request));
			}
			return VendorHttp.RunSync(() => SendAndReadAsync<PcmOrderResponse>(account,HttpMethod.Post,"order/postcard",request,"order place",
				OrderTimeout,CancellationToken.None));
		}

		public PcmPagedResult<PcmOrder> GetOrders(PostcardManiaAccount account,string extRefNbr,int page,int perPage) {
			string url="order?page="+Math.Max(1,page)+"&perPage="+Math.Max(1,perPage);
			if(!string.IsNullOrEmpty(extRefNbr)) {
				url+="&extRefNbr="+Uri.EscapeDataString(extRefNbr);
			}
			return VendorHttp.RunSync(() => GetPagedAsync<PcmOrder>(account,url,"order list",CancellationToken.None));
		}

		public void CancelOrder(PostcardManiaAccount account,long orderID) {
			VendorHttp.RunSync(() => SendAndReadAsync<object>(account,HttpMethod.Delete,"order/"+orderID.ToString(CultureInfo.InvariantCulture),null,
				"order cancel",null,CancellationToken.None));
		}

		public PcmBatch GetBatch(PostcardManiaAccount account,long batchID) {
			return VendorHttp.RunSync(() => SendAndReadAsync<PcmBatch>(account,HttpMethod.Get,"batch/"+batchID.ToString(CultureInfo.InvariantCulture),null,
				"batch",null,CancellationToken.None));
		}

		public byte[] GetProofImage(string url) {
			Uri uri;
			if(string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url,UriKind.Absolute,out uri) || VendorUrlPolicy.Check(uri)!=VendorUrlProblem.None) {
				return null;
			}
			return VendorHttp.RunSync(() => GetProofImageAsync(uri,CancellationToken.None));
		}
		#endregion Public calls

		#region Token handling
		///<summary>The token cache for this account's key, child reference and secret. A changed setting gives a new cache entry.</summary>
		private TokenCache GetTokenCache(PostcardManiaAccount account) {
			return _dictTokenCaches.GetOrAdd(account.GetCacheKey(),x => new TokenCache(TokenRefreshMargin,_funcUtcNow));
		}

		private async Task<OAuthAccessToken> LoginAsync(PostcardManiaAccount account,CancellationToken cancellationToken) {
			PcmAuthRequest pcmAuthRequest=new PcmAuthRequest();
			pcmAuthRequest.ApiKey=account.ApiKey;
			pcmAuthRequest.ApiSecret=account.ApiSecret;
			pcmAuthRequest.ChildRefNbr=string.IsNullOrWhiteSpace(account.ChildRefNbr) ? null : account.ChildRefNbr;
			string json=PcmJson.Serialize(pcmAuthRequest);
			HttpResponseMessage response=await SendWithRateLimitAsync(HttpMethod.Post,() => {
				HttpRequestMessage request=new HttpRequestMessage(HttpMethod.Post,"auth/login");
				request.Content=new StringContent(json,Encoding.UTF8,"application/json");
				VendorHttp.SetEndpointName(request,"auth/login");
				return _httpClient.SendAsync(request,cancellationToken);
			},cancellationToken).ConfigureAwait(false);
			using(response) {
				string body=await ReadBodyAsync(response).ConfigureAwait(false);
				if(response.StatusCode==HttpStatusCode.Unauthorized || response.StatusCode==HttpStatusCode.NotFound) {
					//A definite rejection: the token cache remembers it for this key and secret, so nothing is sent again until they change.
					throw new TokenRejectedException("invalid_client","The API key or secret was rejected.",(int)response.StatusCode);
				}
				if(!response.IsSuccessStatusCode) {
					throw ToException(response,body,"auth/login");
				}
				PcmAuthResponse pcmAuthResponse=DeserializeOrThrow<PcmAuthResponse>(body,"auth/login");
				if(pcmAuthResponse==null || string.IsNullOrEmpty(pcmAuthResponse.Token)) {
					throw new PostcardManiaException(PostcardManiaErrorKind.Other,"The postcard service did not return a sign-in token.");
				}
				DateTime dateTimeNow=_funcUtcNow();
				OAuthAccessToken token=new OAuthAccessToken();
				token.Value=pcmAuthResponse.Token;
				token.IssuedUtc=dateTimeNow;
				token.ExpiresUtc=ParseExpires(pcmAuthResponse.Expires,dateTimeNow);
				return token;
			}
		}

		///<summary>The token's expiry from the response, in UTC. If the vendor leaves it out or it can't be read, 14 minutes from now (the documented
		///lifetime is 15 minutes).</summary>
		public static DateTime ParseExpires(string expires,DateTime dateTimeNowUtc) {
			DateTimeOffset dateTimeOffset;
			if(!string.IsNullOrWhiteSpace(expires)
				&& DateTimeOffset.TryParse(expires,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AllowWhiteSpaces,out dateTimeOffset))
			{
				return dateTimeOffset.UtcDateTime;
			}
			return dateTimeNowUtc.AddMinutes(14);
		}
		#endregion Token handling

		#region Sending
		private async Task<PcmConnectionResult> TestConnectionAsync(PostcardManiaAccount account,CancellationToken cancellationToken) {
			RequireConfigured(account);
			TokenCache tokenCache=GetTokenCache(account);
			//A test always logs in afresh, even after an earlier rejection of the same key (the office may have fixed it in the portal).
			tokenCache.ClearReauthorization();
			tokenCache.Invalidate();
			PcmBalanceResponse pcmBalanceResponse=await SendAndReadAsync<PcmBalanceResponse>(account,HttpMethod.Get,"integration/balance",null,"balance",
				null,cancellationToken).ConfigureAwait(false);
			PcmConnectionResult pcmConnectionResult=new PcmConnectionResult();
			pcmConnectionResult.Environment=account.IsSandbox ? PostcardManiaProgram.EnvironmentSandbox : PostcardManiaProgram.EnvironmentProduction;
			pcmConnectionResult.MoneyOnAccount=pcmBalanceResponse?.MoneyOnAccount;
			return pcmConnectionResult;
		}

		private async Task<PcmPagedResult<T>> GetPagedAsync<T>(PostcardManiaAccount account,string url,string endpointName,
			CancellationToken cancellationToken)
		{
			string body=await SendAsync(account,HttpMethod.Get,url,null,endpointName,null,cancellationToken).ConfigureAwait(false);
			return ParsePaged<T>(body,endpointName);
		}

		private async Task<T> SendAndReadAsync<T>(PostcardManiaAccount account,HttpMethod httpMethod,string url,object bodyObject,string endpointName,
			TimeSpan? timeout,CancellationToken cancellationToken)
		{
			string body=await SendAsync(account,httpMethod,url,bodyObject,endpointName,timeout,cancellationToken).ConfigureAwait(false);
			if(typeof(T)==typeof(object) || string.IsNullOrWhiteSpace(body)) {
				return default(T);
			}
			return DeserializeOrThrow<T>(body,endpointName);
		}

		///<summary>Sends an authorized request and returns the body of a successful response. Logs in as needed; after a 401, logs in once more and
		///repeats the call once. Throws PostcardManiaException for everything else.</summary>
		private async Task<string> SendAsync(PostcardManiaAccount account,HttpMethod httpMethod,string url,object bodyObject,string endpointName,
			TimeSpan? timeout,CancellationToken cancellationToken)
		{
			RequireConfigured(account);
			string json=(bodyObject==null) ? null : PcmJson.Serialize(bodyObject);
			TokenCache tokenCache=GetTokenCache(account);
			Func<HttpRequestMessage> funcCreateRequest=() => {
				HttpRequestMessage request=new HttpRequestMessage(httpMethod,url);
				if(json!=null) {
					request.Content=new StringContent(json,Encoding.UTF8,"application/json");
				}
				VendorHttp.SetEndpointName(request,endpointName);
				if(timeout.HasValue) {
					VendorHttp.SetTimeout(request,timeout.Value);
				}
				return request;
			};
			try {
				HttpResponseMessage response=await SendWithRateLimitAsync(httpMethod,
					() => tokenCache.SendWithAuthRetryAsync(_httpClient,funcCreateRequest,x => LoginAsync(account,x),cancellationToken),
					cancellationToken).ConfigureAwait(false);
				using(response) {
					string body=await ReadBodyAsync(response).ConfigureAwait(false);
					if(response.IsSuccessStatusCode) {
						return body;
					}
					throw ToException(response,body,endpointName);
				}
			}
			catch(TokenRejectedException) {
				throw CredentialsRejected(account);
			}
			catch(ReauthorizationRequiredException) {
				throw CredentialsRejected(account);
			}
			catch(VendorHttpException ex) {
				if(ex.Failure==VendorHttpFailure.Timeout) {
					throw new PostcardManiaException(PostcardManiaErrorKind.Timeout,
						"The postcard service ("+Host+") did not answer in time. Check the internet connection and try again.");
				}
				throw Unreachable();
			}
			catch(HttpRequestException) {
				throw Unreachable();
			}
			catch(TaskCanceledException) when(!cancellationToken.IsCancellationRequested) {
				throw new PostcardManiaException(PostcardManiaErrorKind.Timeout,
					"The postcard service ("+Host+") did not answer in time. Check the internet connection and try again.");
			}
		}

		///<summary>Sends with send(); on a 429 waits for Retry-After (plus a little jitter) and tries again, up to MaxRateLimitTries in all. A
		///Retry-After longer than MaxRetryAfter returns the 429 at once. GETs are already retried this way inside VendorHttp, so they pass through.</summary>
		private async Task<HttpResponseMessage> SendWithRateLimitAsync(HttpMethod httpMethod,Func<Task<HttpResponseMessage>> send,
			CancellationToken cancellationToken)
		{
			bool isRetriedByVendorHttp=(httpMethod==HttpMethod.Get || httpMethod==HttpMethod.Head);
			for(int attempt=1;;attempt++) {
				HttpResponseMessage response=await send().ConfigureAwait(false);
				if((int)response.StatusCode!=429 || isRetriedByVendorHttp || attempt>=MaxRateLimitTries) {
					return response;
				}
				TimeSpan delay=GetRetryAfter(response)??TimeSpan.FromSeconds(Math.Pow(2,attempt-1));
				if(delay>MaxRetryAfter) {
					return response;
				}
				double msJitter;
				lock(_random) {
					msJitter=_random.NextDouble()*250;
				}
				response.Dispose();
				await _funcDelay(delay+TimeSpan.FromMilliseconds(msJitter),cancellationToken).ConfigureAwait(false);
			}
		}

		private async Task<byte[]> GetProofImageAsync(Uri uri,CancellationToken cancellationToken) {
			try {
				HttpRequestMessage request=new HttpRequestMessage(HttpMethod.Get,uri);
				VendorHttp.SetEndpointName(request,"proof image");
				using(HttpResponseMessage response=await _httpClientProof.SendAsync(request,cancellationToken).ConfigureAwait(false)) {
					if(!response.IsSuccessStatusCode) {
						return null;
					}
					if(response.Content.Headers.ContentLength>MAX_PROOF_BYTES) {
						return null;
					}
					byte[] arrayBytes=await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
					return arrayBytes.Length>MAX_PROOF_BYTES ? null : arrayBytes;
				}
			}
			catch(HttpRequestException) {
				return null;//The preview is optional.
			}
			catch(TaskCanceledException) when(!cancellationToken.IsCancellationRequested) {
				return null;
			}
		}
		#endregion Sending

		#region Parsing and errors
		private static async Task<string> ReadBodyAsync(HttpResponseMessage response) {
			if(response.Content==null) {
				return "";
			}
			return await response.Content.ReadAsStringAsync().ConfigureAwait(false)??"";
		}

		private static T DeserializeOrThrow<T>(string body,string endpointName) {
			try {
				return PcmJson.Deserialize<T>(body);
			}
			catch(JsonException) {
				throw new PostcardManiaException(PostcardManiaErrorKind.Other,"The postcard service sent an answer this program could not read ("+endpointName+").");
			}
		}

		///<summary>Reads a list endpoint's page. Accepts a bare array, or an object with the list under results, data or items and the paging under
		///pagination or meta, because the v3 reference still changes.</summary>
		public static PcmPagedResult<T> ParsePaged<T>(string body,string endpointName) {
			PcmPagedResult<T> pcmPagedResult=new PcmPagedResult<T>();
			if(string.IsNullOrWhiteSpace(body)) {
				return pcmPagedResult;
			}
			JsonSerializer jsonSerializer=JsonSerializer.Create(PcmJson.Settings);
			try {
				JToken jToken=JToken.Parse(body);
				JArray jArray=jToken as JArray;
				JObject jObject=jToken as JObject;
				if(jObject!=null) {
					foreach(string name in new[] { "results","data","items" }) {
						JToken jTokenList=GetPropertyIgnoreCase(jObject,name);
						if(jTokenList is JArray) {
							jArray=(JArray)jTokenList;
							break;
						}
					}
					JToken jTokenPaging=GetPropertyIgnoreCase(jObject,"pagination")??GetPropertyIgnoreCase(jObject,"meta");
					if(jTokenPaging is JObject) {
						pcmPagedResult.Pagination=jTokenPaging.ToObject<PcmPagination>(jsonSerializer);
					}
				}
				if(jArray!=null) {
					pcmPagedResult.Results=jArray.Where(x => x.Type==JTokenType.Object).Select(x => x.ToObject<T>(jsonSerializer)).ToList();
				}
			}
			catch(JsonException) {
				throw new PostcardManiaException(PostcardManiaErrorKind.Other,"The postcard service sent an answer this program could not read ("+endpointName+").");
			}
			return pcmPagedResult;
		}

		private static JToken GetPropertyIgnoreCase(JObject jObject,string name) {
			JProperty jProperty=jObject.Properties().FirstOrDefault(x => string.Equals(x.Name,name,StringComparison.OrdinalIgnoreCase));
			return jProperty?.Value;
		}

		///<summary>The vendor's error.data[] messages ("property: message" when a property is named), or the error message if there are none and it
		///says more than the generic text.</summary>
		public static List<string> ParseErrorMessages(string body) {
			List<string> listMessages=new List<string>();
			if(string.IsNullOrWhiteSpace(body)) {
				return listMessages;
			}
			PcmApiError pcmApiError=null;
			try {
				pcmApiError=PcmJson.Deserialize<PcmApiError>(body);
			}
			catch(JsonException) {
				return listMessages;
			}
			if(pcmApiError?.Error==null) {
				return listMessages;
			}
			if(pcmApiError.Error.Data!=null) {
				for(int i=0;i<pcmApiError.Error.Data.Count;i++) {
					PcmApiErrorData data=pcmApiError.Error.Data[i];
					if(data==null || string.IsNullOrWhiteSpace(data.Message)) {
						continue;
					}
					listMessages.Add(string.IsNullOrWhiteSpace(data.Property) ? data.Message : (data.Property+": "+data.Message));
				}
			}
			if(listMessages.Count==0 && !string.IsNullOrWhiteSpace(pcmApiError.Error.Message)
				&& pcmApiError.Error.Message!="One or more errors occurred")
			{
				listMessages.Add(pcmApiError.Error.Message);
			}
			return listMessages;
		}

		private PostcardManiaException ToException(HttpResponseMessage response,string body,string endpointName) {
			int statusCode=(int)response.StatusCode;
			List<string> listMessages=ParseErrorMessages(body);
			if(statusCode==400 || statusCode==422) {
				string message="The postcard service refused the request.";
				if(statusCode==422 && endpointName=="order place") {
					message+=" Use Check addresses to find the recipients that failed verification.";
				}
				return new PostcardManiaException(PostcardManiaErrorKind.Validation,message,response.StatusCode,listMessages);
			}
			if(statusCode==401 || statusCode==403) {
				return new PostcardManiaException(PostcardManiaErrorKind.CredentialsRejected,
					"The postcard service rejected the API key or secret. Check them in Setup.",response.StatusCode,listMessages);
			}
			if(statusCode==404) {
				return new PostcardManiaException(PostcardManiaErrorKind.NotFound,"The postcard service could not find that item ("+endpointName+").",
					response.StatusCode,listMessages);
			}
			if(statusCode==409) {
				return new PostcardManiaException(PostcardManiaErrorKind.Conflict,"An order with this reference was already placed.",response.StatusCode,
					listMessages);
			}
			if(statusCode==429) {
				TimeSpan? retryAfter=GetRetryAfter(response);
				int retryAfterSeconds=retryAfter.HasValue ? (int)Math.Ceiling(retryAfter.Value.TotalSeconds) : 0;
				return new PostcardManiaException(PostcardManiaErrorKind.RateLimited,"The postcard service is busy. Nothing was processed. Try again shortly.",
					response.StatusCode,listMessages,retryAfterSeconds);
			}
			if(statusCode>=500) {
				return new PostcardManiaException(PostcardManiaErrorKind.Server,"The postcard service had an error ("+statusCode+"). Try again later.",
					response.StatusCode,listMessages);
			}
			return new PostcardManiaException(PostcardManiaErrorKind.Other,"The postcard service answered "+statusCode+".",response.StatusCode,listMessages);
		}

		private static TimeSpan? GetRetryAfter(HttpResponseMessage response) {
			if(response.Headers.RetryAfter==null) {
				return null;
			}
			if(response.Headers.RetryAfter.Delta.HasValue) {
				return response.Headers.RetryAfter.Delta.Value;
			}
			if(response.Headers.RetryAfter.Date.HasValue) {
				TimeSpan delta=response.Headers.RetryAfter.Date.Value.UtcDateTime-DateTime.UtcNow;
				return delta<TimeSpan.Zero ? TimeSpan.Zero : delta;
			}
			return null;
		}

		private PostcardManiaException Unreachable() {
			return new PostcardManiaException(PostcardManiaErrorKind.Network,
				"Could not reach the postcard service ("+Host+"). Check the internet connection and try again.");
		}

		private static PostcardManiaException CredentialsRejected(PostcardManiaAccount account) {
			return new PostcardManiaException(PostcardManiaErrorKind.CredentialsRejected,
				"The API key or secret for "+DescribeAccount(account)+" was rejected. Check them in Setup.",HttpStatusCode.Unauthorized);
		}

		private static void RequireConfigured(PostcardManiaAccount account) {
			if(account==null || !account.IsConfigured) {
				string message="No PostcardMania API key and secret are set up";
				if(account!=null && account.IsSecretUnreadable) {
					message="The stored API secret could not be read. Enter it again";
				}
				throw new PostcardManiaException(PostcardManiaErrorKind.NotConfigured,message+" for "+DescribeAccount(account)+". Use Setup.");
			}
		}

		///<summary>"the default account" or "clinic {abbr}". Never the key or secret.</summary>
		public static string DescribeAccount(PostcardManiaAccount account) {
			if(account==null || account.ClinicNumSource==0) {
				return "the default account";
			}
			string abbr="";
			try {
				abbr=Clinics.GetAbbr(account.ClinicNumSource);
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
			return "clinic "+(string.IsNullOrWhiteSpace(abbr) ? account.ClinicNumSource.ToString() : abbr);
		}
		#endregion Parsing and errors
	}
}
