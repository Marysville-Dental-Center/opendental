// DoseSpot eRx: the HTTP client for DoseSpot's REST API v2, with the office's own subscription key and clinic credentials.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CodeBase;
using CodeBase.VendorDirect;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenDentBusiness {
	///<summary>Why a DoseSpot call failed. Each kind gets its own wording in the Chart.</summary>
	public enum DoseSpotErrorKind {
		///<summary>A subscription key, clinic ID, clinic key or user ID is missing or unreadable. Nothing was sent.</summary>
		NotConfigured,
		///<summary>DoseSpot refused the clinic ID/key, the user ID or the subscription key (token 400/401, or 401 twice on a call).</summary>
		Rejected,
		///<summary>DoseSpot says the user may not do this (403, or a "not authorized" result for a proxy user).</summary>
		NotAuthorized,
		///<summary>HTTP 429. Background polls pause for RetryAfter.</summary>
		RateLimited,
		///<summary>No answer: no network, DNS failure, timeout.</summary>
		Unreachable,
		///<summary>Any other failure: a 4xx/5xx, an unreadable answer, or Result.ResultCode=ERROR (the message then carries ResultDescription).</summary>
		VendorError,
	}

	///<summary>A DoseSpot call that failed. The message is safe to show: it never holds a token, key or SSO code.</summary>
	[Serializable]
	public class DoseSpotException:ODException {
		public DoseSpotErrorKind Kind { get; private set; }
		///<summary>The HTTP status, or 0 if there was no answer.</summary>
		public int HttpStatus { get; private set; }
		///<summary>DoseSpot's Retry-After on a 429, if it sent one.</summary>
		public TimeSpan? RetryAfter { get; set; }

		public DoseSpotException(DoseSpotErrorKind kind,string message,int httpStatus=0)
			:base(message,kind==DoseSpotErrorKind.NotAuthorized ? (int)ErrorCodes.DoseSpotNotAuthorized : 0)
		{
			Kind=kind;
			HttpStatus=httpStatus;
		}

		protected DoseSpotException(SerializationInfo info,StreamingContext context) : base(info,context) {
		}
	}

	///<summary>Everything one DoseSpot call needs: where (API base URL), the practice-wide subscription key, and the clinic and user it acts as.
	///Plain values in memory only; build it with FromConfig right before the call and let it go.</summary>
	public sealed class DoseSpotCredentials {
		public string ApiBaseUrl="";
		public string SubscriptionKey="";
		public string ClinicId="";
		public string ClinicKey="";
		public string UserId="";

		///<summary>Fills the base URL and subscription key from eRx Setup. Throws DoseSpotException(NotConfigured) if the key is missing.</summary>
		public static DoseSpotCredentials FromConfig(string clinicId,string clinicKey,string userId) {
			Meth.NoCheckMiddleTierRole();
			DoseSpotCredentials doseSpotCredentials=new DoseSpotCredentials();
			doseSpotCredentials.ApiBaseUrl=DoseSpotConfig.GetApiBaseUrl(DoseSpotConfig.GetEnvironment());
			doseSpotCredentials.SubscriptionKey=DoseSpotConfig.ReadSubscriptionKey().PlainText;
			doseSpotCredentials.ClinicId=(clinicId??"").Trim();
			doseSpotCredentials.ClinicKey=(clinicKey??"").Trim();
			doseSpotCredentials.UserId=(userId??"").Trim();
			doseSpotCredentials.ThrowIfIncomplete();
			return doseSpotCredentials;
		}

		///<summary>Throws DoseSpotException(NotConfigured), naming what is missing, if any value is blank.</summary>
		public void ThrowIfIncomplete() {
			List<string> listMissing=new List<string>();
			if(string.IsNullOrWhiteSpace(SubscriptionKey)) {
				listMissing.Add(Lans.g("DoseSpot","subscription key"));
			}
			if(string.IsNullOrWhiteSpace(ClinicId)) {
				listMissing.Add(Lans.g("DoseSpot","clinic ID"));
			}
			if(string.IsNullOrWhiteSpace(ClinicKey)) {
				listMissing.Add(Lans.g("DoseSpot","clinic key"));
			}
			if(string.IsNullOrWhiteSpace(UserId)) {
				listMissing.Add(Lans.g("DoseSpot","DoseSpot user ID"));
			}
			if(string.IsNullOrWhiteSpace(ApiBaseUrl)) {
				listMissing.Add(Lans.g("DoseSpot","API address"));
			}
			if(listMissing.Count>0) {
				throw new DoseSpotException(DoseSpotErrorKind.NotConfigured,
					Lans.g("DoseSpot","DoseSpot is not configured. Missing:")+" "+string.Join(", ",listMissing)+".");
			}
		}

		///<summary>The token cache key. Secrets go in only as SHA-256 fingerprints, which stay in memory.</summary>
		public string GetFingerprint() {
			return TokenCache.Fingerprint(ApiBaseUrl,ClinicId,UserId,TokenCache.Fingerprint(ClinicKey),TokenCache.Fingerprint(SubscriptionKey));
		}
	}

	///<summary>DoseSpot's result envelope. ResultCode contains "ERROR" for business errors, with the reason in ResultDescription.</summary>
	public class DoseSpotResult {
		public string ResultCode="";
		public string ResultDescription="";

		public bool IsError {
			get {
				return (ResultCode??"").ToUpperInvariant().Contains("ERROR");
			}
		}
	}

	///<summary>The counts shown on the Chart's eRx button.</summary>
	public class DoseSpotNotificationCounts {
		public int RefillRequestsCount;
		public int TransactionErrorsCount;
		public int PendingPrescriptionsCount;
		public DoseSpotResult Result;
	}

	///<summary>Talks to DoseSpot's REST API v2 directly with the office's own subscription key: POST /connect/token (a password-style grant from
	///the clinic ID, clinic key and user ID) and the /api resources DoseSpot's public JumpstartV2 contract describes. Access tokens live in memory
	///only, per environment, clinic and user, until expires_in minus 60 seconds (10 minutes assumed when DoseSpot sends none). A 401 gets one new
	///token and one retry. GETs get one retry after a network error or a 5xx; POSTs and PUTs are never retried (POST /api/patients could create a
	///duplicate patient). A 429 pauses the Chart's background polls for Retry-After. Only the configured DoseSpot host is ever contacted, and only
	///the endpoint name, status and request ID are logged: never a token, key, SSO code, URL or body.</summary>
	public class DoseSpotApiClient {
		public const string VendorName="DoseSpot";
		///<summary>The token lifetime assumed when the token answer has no expires_in.</summary>
		public static readonly TimeSpan DefaultTokenLifetime=TimeSpan.FromMinutes(10);
		///<summary>Refresh this long before a token expires.</summary>
		public static readonly TimeSpan TokenRefreshMargin=TimeSpan.FromSeconds(60);
		///<summary>A runaway "HasNext" never makes more than this many page requests.</summary>
		public const int MaxPages=100;
		private static DoseSpotApiClient _default;
		private static readonly object _lockDefault=new object();
		private static readonly JsonSerializerSettings _jsonSerializerSettings=new JsonSerializerSettings {
			MissingMemberHandling=MissingMemberHandling.Ignore,
			//A field whose type changed in a later API release is skipped rather than failing the whole answer.
			Error=(sender,args) => args.ErrorContext.Handled=true,
		};

		private readonly HttpMessageHandler _httpMessageHandler;
		private readonly Func<DateTime> _funcUtcNow;
		private readonly Action<string> _actionLog;
		private readonly ErxPollThrottle _erxPollThrottle;
		private readonly ConcurrentDictionary<string,HttpClient> _dictHttpClients=new ConcurrentDictionary<string,HttpClient>();
		private readonly ConcurrentDictionary<string,TokenCache> _dictTokenCaches=new ConcurrentDictionary<string,TokenCache>();

		///<summary>The process-wide client used by the program. Tests may replace it.</summary>
		public static DoseSpotApiClient Default {
			get {
				lock(_lockDefault) {
					if(_default==null) {
						_default=new DoseSpotApiClient();
					}
					return _default;
				}
			}
			set {
				lock(_lockDefault) {
					_default=value;
				}
			}
		}

		///<summary>httpMessageHandler is for tests (a fake handler); null uses the real network through VendorHttp. funcUtcNow is the clock for token
		///lifetimes. actionLog receives redacted lines; null writes them to the program's "DoseSpot" log. erxPollThrottle receives 429 pauses;
		///null uses ErxPollThrottle.Default.</summary>
		public DoseSpotApiClient(HttpMessageHandler httpMessageHandler=null,Func<DateTime> funcUtcNow=null,Action<string> actionLog=null,
			ErxPollThrottle erxPollThrottle=null)
		{
			_httpMessageHandler=httpMessageHandler;
			_funcUtcNow=funcUtcNow??(() => DateTime.UtcNow);
			_actionLog=actionLog??(x => Logger.WriteLine(x,"DoseSpot"));
			_erxPollThrottle=erxPollThrottle??ErxPollThrottle.Default;
		}

		///<summary>Forgets every cached token and rejection, e.g. after eRx Setup saves, so a problem the office fixed at DoseSpot is retried at once.</summary>
		public void ResetAll() {
			_dictTokenCaches.Clear();
		}

		///<summary>This instance's client for one API base URL, allowed to reach only that URL's host. Each base URL gets its own client, so
		///Production, Staging and an Introspection override can all be used in one session. VendorHttp.GetClient is not used: it caches by vendor
		///and BaseAddress, and with no BaseAddress every DoseSpot URL would share the allow-list of whichever one was used first.</summary>
		private HttpClient GetHttpClient(string apiBaseUrl) {
			return _dictHttpClients.GetOrAdd(apiBaseUrl,x => {
				VendorHttpOptions options=new VendorHttpOptions();
				options.VendorName=VendorName;
				options.ListAllowedHosts=new List<string> { new Uri(x).Host };
				options.Timeout=TimeSpan.FromSeconds(30);
				options.MaxGetRetries=1;
				options.Log=_actionLog;
				//Null uses the real network: an HttpClientHandler behind the same policy handler the tests go through.
				return VendorHttp.CreateClient(options,_httpMessageHandler);
			});
		}

		private TokenCache GetTokenCache(DoseSpotCredentials doseSpotCredentials) {
			return _dictTokenCaches.GetOrAdd(doseSpotCredentials.GetFingerprint(),x => new TokenCache(TokenRefreshMargin,_funcUtcNow));
		}

		private static Uri MakeUri(DoseSpotCredentials doseSpotCredentials,string relativePath) {
			Uri uri;
			if(!Uri.TryCreate(doseSpotCredentials.ApiBaseUrl.TrimEnd('/')+relativePath,UriKind.Absolute,out uri)) {
				throw new DoseSpotException(DoseSpotErrorKind.NotConfigured,Lans.g("DoseSpot","The DoseSpot API address is not valid."));
			}
			return uri;
		}

		#region Token
		///<summary>The cached access token, or a new one from DoseSpot. isInteractive forgets an earlier rejection first (the user clicked eRx after
		///fixing something), so only background calls fail fast after a rejection. Throws DoseSpotException.</summary>
		public async Task<OAuthAccessToken> GetAccessTokenAsync(DoseSpotCredentials doseSpotCredentials,bool isInteractive,
			CancellationToken cancellationToken=default(CancellationToken))
		{
			doseSpotCredentials.ThrowIfIncomplete();
			TokenCache tokenCache=GetTokenCache(doseSpotCredentials);
			if(isInteractive) {
				tokenCache.ClearReauthorization();
			}
			try {
				return await tokenCache.GetAsync(ct => RequestTokenAsync(doseSpotCredentials,ct),cancellationToken).ConfigureAwait(false);
			}
			catch(Exception ex) {
				throw MapException(ex);
			}
		}

		///<summary>The form DoseSpot's token endpoint takes. Every value is form-encoded by FormUrlEncodedContent, so a key with + / = survives.</summary>
		public static Dictionary<string,string> GetTokenForm(DoseSpotCredentials doseSpotCredentials) {
			Dictionary<string,string> dictForm=new Dictionary<string,string>();
			dictForm["grant_type"]="password";
			dictForm["client_id"]=doseSpotCredentials.ClinicId;
			dictForm["client_secret"]=doseSpotCredentials.ClinicKey;
			dictForm["username"]=doseSpotCredentials.UserId;
			dictForm["password"]=doseSpotCredentials.ClinicKey;
			dictForm["scope"]="api";
			return dictForm;
		}

		private async Task<OAuthAccessToken> RequestTokenAsync(DoseSpotCredentials doseSpotCredentials,CancellationToken cancellationToken) {
			Uri uri=MakeUri(doseSpotCredentials,"/connect/token");
			HttpClient httpClient=GetHttpClient(doseSpotCredentials.ApiBaseUrl);
			using(HttpRequestMessage request=new HttpRequestMessage(HttpMethod.Post,uri)) {
				request.Content=new FormUrlEncodedContent(GetTokenForm(doseSpotCredentials));
				request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
				request.Headers.TryAddWithoutValidation("Subscription-Key",doseSpotCredentials.SubscriptionKey);
				VendorHttp.SetEndpointName(request,"token");
				using(HttpResponseMessage response=await httpClient.SendAsync(request,cancellationToken).ConfigureAwait(false)) {
					string body=(response.Content==null) ? "" : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
					int status=(int)response.StatusCode;
					if(status==429) {
						throw MakeRateLimited(response);
					}
					JObject jObject=TryParseJson(body);
					if(status==400 || status==401) {
						string error=jObject?.Value<string>("error")??"";
						throw new TokenRejectedException(error,"DoseSpot refused the sign-in"+(error=="" ? "" : " ("+error+")")+".",status);
					}
					if(status<200 || status>299) {
						throw new DoseSpotException(DoseSpotErrorKind.VendorError,Lans.g("DoseSpot","DoseSpot answered the sign-in with HTTP")+" "+status+".",status);
					}
					string accessToken=jObject?.Value<string>("access_token");
					if(string.IsNullOrEmpty(accessToken)) {
						throw new DoseSpotException(DoseSpotErrorKind.VendorError,Lans.g("DoseSpot","DoseSpot's sign-in answer had no access token."),status);
					}
					DateTime utcNow=_funcUtcNow();
					OAuthAccessToken token=new OAuthAccessToken();
					token.Value=accessToken;
					token.IssuedUtc=utcNow;
					long expiresIn=0;
					long.TryParse(jObject["expires_in"]?.ToString()??"",out expiresIn);
					token.ExpiresUtc=(expiresIn>0) ? utcNow.AddSeconds(expiresIn) : utcNow.Add(DefaultTokenLifetime);
					return token;
				}
			}
		}
		#endregion Token

		#region Calls
		///<summary>GET /api/notifications/counts.</summary>
		public async Task<DoseSpotNotificationCounts> GetNotificationCountsAsync(DoseSpotCredentials doseSpotCredentials,bool isInteractive,
			CancellationToken cancellationToken=default(CancellationToken))
		{
			DoseSpotNotificationCounts counts=await SendAsync<DoseSpotNotificationCounts>(doseSpotCredentials,HttpMethod.Get,"/api/notifications/counts",
				null,"notification counts",isInteractive,cancellationToken).ConfigureAwait(false);
			ThrowIfResultError(counts.Result,"");
			return counts;
		}

		///<summary>GET /api/patients/{id}/prescriptions, every page.</summary>
		public Task<List<DoseSpotPrescription>> GetPrescriptionsAsync(DoseSpotCredentials doseSpotCredentials,string patientId,bool isInteractive,
			CancellationToken cancellationToken=default(CancellationToken))
		{
			return GetAllPagesAsync<DoseSpotPrescription>(doseSpotCredentials,"/api/patients/"+Escape(patientId)+"/prescriptions","prescriptions",
				Lans.g("DoseSpot","Error getting prescriptions:"),isInteractive,cancellationToken);
		}

		///<summary>GET /api/patients/{id}/selfReportedMedications, every page.</summary>
		public Task<List<DoseSpotSelfReported>> GetSelfReportedAsync(DoseSpotCredentials doseSpotCredentials,string patientId,bool isInteractive,
			CancellationToken cancellationToken=default(CancellationToken))
		{
			return GetAllPagesAsync<DoseSpotSelfReported>(doseSpotCredentials,"/api/patients/"+Escape(patientId)+"/selfReportedMedications",
				"self-reported medications",Lans.g("DoseSpot","Error getting self reported medications:"),isInteractive,cancellationToken);
		}

		///<summary>GET /api/pharmacies/{id}: the pharmacy's StoreName.</summary>
		public async Task<string> GetPharmacyNameAsync(DoseSpotCredentials doseSpotCredentials,int pharmacyId,bool isInteractive,
			CancellationToken cancellationToken=default(CancellationToken))
		{
			JObject jObject=await SendAsync<JObject>(doseSpotCredentials,HttpMethod.Get,"/api/pharmacies/"+pharmacyId,null,"pharmacy",isInteractive,
				cancellationToken).ConfigureAwait(false);
			ThrowIfResultError(jObject?["Result"]?.ToObject<DoseSpotResult>(),Lans.g("DoseSpot","Error getting pharmacy:"));
			string storeName=jObject?["Item"]?.Value<string>("StoreName");
			if(storeName==null) {
				throw new DoseSpotException(DoseSpotErrorKind.VendorError,Lans.g("DoseSpot","Error getting pharmacy: malformed response from DoseSpot."));
			}
			return storeName;
		}

		///<summary>POST /api/patients. Never retried automatically: a retry after a lost answer could create a duplicate patient. Returns the new ID.</summary>
		public async Task<string> AddPatientAsync(DoseSpotCredentials doseSpotCredentials,object patientBody,bool isInteractive,
			CancellationToken cancellationToken=default(CancellationToken))
		{
			JObject jObject=await SendAsync<JObject>(doseSpotCredentials,HttpMethod.Post,"/api/patients",patientBody,"add patient",isInteractive,
				cancellationToken).ConfigureAwait(false);
			ThrowIfResultError(jObject?["Result"]?.ToObject<DoseSpotResult>(),Lans.g("DoseSpot","Error adding patient:"));
			string id=jObject?["Id"]?.ToString()??"";
			if(id=="" || id=="-1" || id=="0") {
				throw new DoseSpotException(DoseSpotErrorKind.VendorError,Lans.g("DoseSpot","Error adding patient: DoseSpot returned no patient ID."));
			}
			return id;
		}

		///<summary>PUT /api/patients/{id}: overwrites DoseSpot's copy of the demographics.</summary>
		public async System.Threading.Tasks.Task EditPatientAsync(DoseSpotCredentials doseSpotCredentials,string patientId,object patientBody,bool isInteractive,
			CancellationToken cancellationToken=default(CancellationToken))
		{
			JObject jObject=await SendAsync<JObject>(doseSpotCredentials,HttpMethod.Put,"/api/patients/"+Escape(patientId),patientBody,"edit patient",
				isInteractive,cancellationToken).ConfigureAwait(false);
			ThrowIfResultError(jObject?["Result"]?.ToObject<DoseSpotResult>(),Lans.g("DoseSpot","Error editing patient:"));
		}

		///<summary>POST /api/patients/{id}/medications/history/consent.</summary>
		public async System.Threading.Tasks.Task PostMedicationHistoryConsentAsync(DoseSpotCredentials doseSpotCredentials,string patientId,bool isInteractive,
			CancellationToken cancellationToken=default(CancellationToken))
		{
			JObject jObject=await SendAsync<JObject>(doseSpotCredentials,HttpMethod.Post,"/api/patients/"+Escape(patientId)+"/medications/history/consent",
				new JObject(),"medication history consent",isInteractive,cancellationToken).ConfigureAwait(false);
			ThrowIfResultError(jObject?["Result"]?.ToObject<DoseSpotResult>(),Lans.g("DoseSpot","Error posting medication history consent:"));
		}

		///<summary>POST /api/clinics: registers another location under the same DoseSpot account. Returns DoseSpot's new clinic ID and key.</summary>
		public async Task<KeyValuePair<string,string>> PostClinicAsync(DoseSpotCredentials doseSpotCredentials,object clinicBody,
			CancellationToken cancellationToken=default(CancellationToken))
		{
			JObject jObject=await SendAsync<JObject>(doseSpotCredentials,HttpMethod.Post,"/api/clinics",clinicBody,"add clinic",true,
				cancellationToken).ConfigureAwait(false);
			ThrowIfResultError(jObject?["Result"]?.ToObject<DoseSpotResult>(),Lans.g("DoseSpot","Error registering clinic:"));
			string clinicId=jObject?["ClinicId"]?.ToString()??"";
			string clinicKey=jObject?["ClinicKey"]?.ToString()??"";
			if(clinicId=="" || clinicKey=="") {
				throw new DoseSpotException(DoseSpotErrorKind.VendorError,Lans.g("DoseSpot","Error registering clinic: DoseSpot returned no clinic ID or key."));
			}
			return new KeyValuePair<string,string>(clinicId,clinicKey);
		}

		///<summary>POST (medicationId 0) or PUT a free-text self-reported medication. Returns DoseSpot's ID for it.</summary>
		public async Task<int> SaveSelfReportedAsync(DoseSpotCredentials doseSpotCredentials,string patientId,int medicationId,object medicationBody,
			bool isInteractive,CancellationToken cancellationToken=default(CancellationToken))
		{
			string path="/api/patients/"+Escape(patientId)+"/selfReportedMedications/freetext";
			HttpMethod httpMethod=HttpMethod.Post;
			if(medicationId>0) {
				path+="/"+medicationId;
				httpMethod=HttpMethod.Put;
			}
			JObject jObject=await SendAsync<JObject>(doseSpotCredentials,httpMethod,path,medicationBody,"self-reported medication",isInteractive,
				cancellationToken).ConfigureAwait(false);
			ThrowIfResultError(jObject?["Result"]?.ToObject<DoseSpotResult>(),Lans.g("DoseSpot","Error saving medication:"));
			int id;
			if(!int.TryParse(jObject?["Id"]?.ToString()??"",out id) || id<=0) {
				id=medicationId;
			}
			return id;
		}
		#endregion Calls

		#region Plumbing
		private async Task<List<T>> GetAllPagesAsync<T>(DoseSpotCredentials doseSpotCredentials,string path,string endpointName,string errorPrefix,
			bool isInteractive,CancellationToken cancellationToken)
		{
			List<T> listItems=new List<T>();
			string pathCur=path;
			for(int page=1;page<=MaxPages;page++) {
				JObject jObject=await SendAsync<JObject>(doseSpotCredentials,HttpMethod.Get,pathCur,null,endpointName,isInteractive,cancellationToken)
					.ConfigureAwait(false);
				ThrowIfResultError(jObject?["Result"]?.ToObject<DoseSpotResult>(),errorPrefix);
				JArray jArrayItems=jObject?["Items"] as JArray;
				if(jArrayItems!=null) {
					JsonSerializer jsonSerializer=JsonSerializer.Create(_jsonSerializerSettings);
					for(int i=0;i<jArrayItems.Count;i++) {
						T item=jArrayItems[i].ToObject<T>(jsonSerializer);
						if(item!=null) {
							listItems.Add(item);
						}
					}
				}
				JToken jTokenPage=jObject?["PageResult"];
				bool hasNext=jTokenPage!=null && (jTokenPage.Value<bool?>("HasNext")??false);
				if(!hasNext) {
					break;
				}
				int currentPage=jTokenPage.Value<int?>("CurrentPage")??page;
				pathCur=path+"?pageNumber="+(currentPage+1);
			}
			return listItems;
		}

		///<summary>Sends one call with the bearer token and the subscription key, refreshing the token once on a 401. Throws DoseSpotException for
		///everything but a 2xx.</summary>
		private async Task<T> SendAsync<T>(DoseSpotCredentials doseSpotCredentials,HttpMethod httpMethod,string path,object body,string endpointName,
			bool isInteractive,CancellationToken cancellationToken)
		{
			doseSpotCredentials.ThrowIfIncomplete();
			Uri uri=MakeUri(doseSpotCredentials,path);//Before GetHttpClient, so an address that won't parse is reported as NotConfigured.
			HttpClient httpClient=GetHttpClient(doseSpotCredentials.ApiBaseUrl);
			TokenCache tokenCache=GetTokenCache(doseSpotCredentials);
			if(isInteractive) {
				tokenCache.ClearReauthorization();
			}
			string json=(body==null) ? null : JsonConvert.SerializeObject(body);
			HttpResponseMessage response;
			try {
				response=await tokenCache.SendWithAuthRetryAsync(httpClient,
					() => {
						HttpRequestMessage request=new HttpRequestMessage(httpMethod,uri);
						request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
						request.Headers.TryAddWithoutValidation("Subscription-Key",doseSpotCredentials.SubscriptionKey);
						if(json!=null) {
							request.Content=new StringContent(json,Encoding.UTF8,"application/json");
						}
						VendorHttp.SetEndpointName(request,endpointName);
						return request;
					},
					ct => RequestTokenAsync(doseSpotCredentials,ct),cancellationToken).ConfigureAwait(false);
			}
			catch(Exception ex) {
				throw MapException(ex);
			}
			using(response) {
				string responseBody=(response.Content==null) ? "" : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
				int status=(int)response.StatusCode;
				if(status==429) {
					throw MakeRateLimited(response);
				}
				if(status==401) {
					throw new DoseSpotException(DoseSpotErrorKind.Rejected,GetRejectedMessage(),status);
				}
				if(status==403) {
					throw new DoseSpotException(DoseSpotErrorKind.NotAuthorized,
						Lans.g("DoseSpot","DoseSpot did not allow this user to do that.")+GetResultSuffix(responseBody),status);
				}
				if(status<200 || status>299) {
					throw new DoseSpotException(DoseSpotErrorKind.VendorError,
						Lans.g("DoseSpot","DoseSpot answered with HTTP")+" "+status+"."+GetResultSuffix(responseBody),status);
				}
				if(typeof(T)==typeof(JObject)) {
					return (T)(object)(TryParseJson(responseBody)??new JObject());
				}
				T result=JsonConvert.DeserializeObject<T>(responseBody??"",_jsonSerializerSettings);
				if(result==null) {
					throw new DoseSpotException(DoseSpotErrorKind.VendorError,Lans.g("DoseSpot","DoseSpot's answer could not be read."),status);
				}
				return result;
			}
		}

		private DoseSpotException MakeRateLimited(HttpResponseMessage response) {
			TimeSpan? retryAfter=null;
			RetryConditionHeaderValue retryConditionHeaderValue=response.Headers.RetryAfter;
			if(retryConditionHeaderValue?.Delta!=null) {
				retryAfter=retryConditionHeaderValue.Delta.Value;
			}
			else if(retryConditionHeaderValue?.Date!=null) {
				TimeSpan delta=retryConditionHeaderValue.Date.Value.UtcDateTime-_funcUtcNow();
				retryAfter=(delta<TimeSpan.Zero) ? TimeSpan.Zero : delta;
			}
			_erxPollThrottle.Pause(VendorName,retryAfter);
			int seconds=(int)Math.Ceiling((retryAfter??ErxPollThrottle.DefaultRetryAfter).TotalSeconds);
			DoseSpotException doseSpotException=new DoseSpotException(DoseSpotErrorKind.RateLimited,
				Lans.g("DoseSpot","DoseSpot is busy. Try again in")+" "+seconds+" "+Lans.g("DoseSpot","seconds."),429);
			doseSpotException.RetryAfter=retryAfter;
			return doseSpotException;
		}

		private static string GetRejectedMessage() {
			return Lans.g("DoseSpot","DoseSpot rejected the clinic ID and key, the subscription key or the user's DoseSpot user ID. "
				+"Check them in Setup > Program Links > eRx and Security > User Edit.");
		}

		///<summary>Turns whatever the HTTP stack threw into a DoseSpotException with a message safe to show.</summary>
		private static Exception MapException(Exception ex) {
			if(ex is DoseSpotException) {
				return ex;
			}
			if(ex is TokenRejectedException || ex is ReauthorizationRequiredException) {
				return new DoseSpotException(DoseSpotErrorKind.Rejected,GetRejectedMessage(),(ex as TokenRejectedException)?.StatusCode??0);
			}
			if(ex is VendorHttpException vendorHttpException && vendorHttpException.Failure==VendorHttpFailure.UrlRefused) {
				return new DoseSpotException(DoseSpotErrorKind.NotConfigured,vendorHttpException.Message);
			}
			if(ex is HttpRequestException || ex is OperationCanceledException || ex is WebException) {
				return new DoseSpotException(DoseSpotErrorKind.Unreachable,Lans.g("DoseSpot","DoseSpot could not be reached."));
			}
			return new DoseSpotException(DoseSpotErrorKind.VendorError,Lans.g("DoseSpot","The DoseSpot call failed:")+" "+ex.GetType().Name);
		}

		private static void ThrowIfResultError(DoseSpotResult doseSpotResult,string errorPrefix) {
			if(doseSpotResult==null || !doseSpotResult.IsError) {
				return;
			}
			string message=((errorPrefix??"")=="" ? "" : errorPrefix+" ")+(doseSpotResult.ResultDescription??"");
			DoseSpotErrorKind kind=DoseSpotErrorKind.VendorError;
			string description=(doseSpotResult.ResultDescription??"").ToLowerInvariant();
			if(description.Contains("not authorized") || description.Contains("unauthorized")) {
				kind=DoseSpotErrorKind.NotAuthorized;
			}
			throw new DoseSpotException(kind,message.Trim());
		}

		private static string GetResultSuffix(string responseBody) {
			JObject jObject=TryParseJson(responseBody);
			string description=jObject?["Result"]?.Value<string>("ResultDescription")??"";
			return (description=="") ? "" : " "+description;
		}

		private static JObject TryParseJson(string body) {
			if(string.IsNullOrWhiteSpace(body)) {
				return null;
			}
			try {
				return JObject.Parse(body);
			}
			catch(Exception) {
				return null;
			}
		}

		private static string Escape(string value) {
			return Uri.EscapeDataString((value??"").Trim());
		}
		#endregion Plumbing
	}
}
