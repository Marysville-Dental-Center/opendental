// USPS address validation: the HTTP client for the USPS OAuth 2.0 and Addresses v3 APIs (client credentials; no browser, no refresh token).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CodeBase;
using CodeBase.VendorDirect;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenDentBusiness {
	///<summary>Why an address check didn't get an answer from USPS. Each kind has its own prompt in PatientL.</summary>
	public enum UspsErrorKind {
		///<summary>No network, DNS failure, timeout, 5xx or an unreadable answer.</summary>
		Offline,
		///<summary>USPS refused the consumer key or secret (token 400/401, or 401 twice on the address call).</summary>
		Credentials,
		///<summary>403: the app has no access to the Addresses API (licence not linked or access not granted yet).</summary>
		Forbidden,
		///<summary>429: over the hourly quota or a spike arrest.</summary>
		Quota,
		///<summary>400 on the address call: USPS couldn't read the request (e.g. a state or ZIP it rejects).</summary>
		BadRequest,
		///<summary>The program link isn't enabled or has no key or secret. No call was made.</summary>
		NotConfigured,
	}

	///<summary>An address check that got no usable answer from USPS. The message is safe to show: it never holds the address, token or secret.</summary>
	[Serializable]
	public class UspsApiException:ODException {
		///<summary>Why the check failed.</summary>
		public UspsErrorKind Kind { get; private set; }
		///<summary>The HTTP status, or 0 if there was no answer.</summary>
		public int HttpStatus { get; private set; }
		///<summary>USPS's own error message, or "".</summary>
		public string UspsMessage { get; private set; }
		///<summary>USPS's Retry-After on a 429, if it sent one.</summary>
		public TimeSpan? RetryAfter { get; set; }

		public UspsApiException(UspsErrorKind kind,int httpStatus,string uspsMessage,string message,Exception innerException=null)
			:base(message,innerException)
		{
			Kind=kind;
			HttpStatus=httpStatus;
			UspsMessage=uspsMessage??"";
		}

		protected UspsApiException(SerializationInfo info,StreamingContext context) : base(info,context) {
		}
	}

	///<summary>The query for GET /addresses/v3/address, built from an OpenDentBusiness.Address under the rules of the USPS OpenAPI spec. Values that
	///don't match USPS's patterns are left out, not sent. The firm is never sent, and neither is anything that names the patient.</summary>
	public sealed class UspsAddressQuery {
		///<summary>The longest streetAddress USPS returns; longer input is cut to this.</summary>
		public const int MaxStreetLength=50;
		///<summary>USPS state codes: the states, DC, the territories and the military codes (the spec's state pattern).</summary>
		private static readonly HashSet<string> _hashSetStates=new HashSet<string>(new[] { "AA","AE","AL","AK","AP","AS","AZ","AR","CA","CO","CT",
			"DE","DC","FM","FL","GA","GU","HI","ID","IL","IN","IA","KS","KY","LA","ME","MH","MD","MA","MI","MN","MS","MO","MP","MT","NE","NV","NH","NJ",
			"NM","NY","NC","ND","OH","OK","OR","PW","PA","PR","RI","SC","SD","TN","TX","UT","VT","VI","VA","WA","WV","WI","WY" });
		private static readonly Regex _regexZip=new Regex(@"^(\d{5})(?:-?(\d{4}))?$");

		public string StreetAddress="";
		public string SecondaryAddress="";
		public string City="";
		public string State="";
		public string ZIPCode="";
		public string ZIPPlus4="";

		///<summary>Builds the query. Returns null, with errorMessage set, if there is nothing USPS could check: no street, or neither city and state nor a
		///ZIP Code. Then no call is made (and no lookup is billed).</summary>
		public static UspsAddressQuery FromAddress(Address address,out string errorMessage) {
			errorMessage="";
			UspsAddressQuery query=new UspsAddressQuery();
			if(address!=null) {
				query.StreetAddress=(address.Address1??"").Trim();
				if(query.StreetAddress.Length>MaxStreetLength) {
					query.StreetAddress=query.StreetAddress.Substring(0,MaxStreetLength).TrimEnd();
				}
				query.SecondaryAddress=(address.Address2??"").Trim();
				query.City=(address.City??"").Trim();
				string state=(address.State??"").Trim().ToUpperInvariant();
				if(_hashSetStates.Contains(state)) {
					query.State=state;
				}
				Match match=_regexZip.Match((address.Zip??"").Trim());
				if(match.Success) {
					query.ZIPCode=match.Groups[1].Value;
					query.ZIPPlus4=match.Groups[2].Success ? match.Groups[2].Value : "";
				}
			}
			if(query.StreetAddress=="") {
				errorMessage="A street address is needed to check the address.";
				return null;
			}
			if((query.City=="" || query.State=="") && query.ZIPCode=="") {
				errorMessage="City and state, or a ZIP Code, are needed to check the address.";
				return null;
			}
			return query;
		}

		///<summary>The query string, without the leading '?'. Every value is percent-encoded; empty values are left out.</summary>
		public string ToQueryString() {
			List<KeyValuePair<string,string>> listParams=new List<KeyValuePair<string,string>>();
			listParams.Add(new KeyValuePair<string,string>("streetAddress",StreetAddress));
			listParams.Add(new KeyValuePair<string,string>("secondaryAddress",SecondaryAddress));
			listParams.Add(new KeyValuePair<string,string>("city",City));
			listParams.Add(new KeyValuePair<string,string>("state",State));
			listParams.Add(new KeyValuePair<string,string>("ZIPCode",ZIPCode));
			listParams.Add(new KeyValuePair<string,string>("ZIPPlus4",ZIPPlus4));
			return string.Join("&",listParams.Where(x => !string.IsNullOrEmpty(x.Value)).Select(x => x.Key+"="+Escape(x.Value)));
		}

		///<summary>RFC 3986 percent-encoding, including the characters older frameworks leave alone (! * ' ( )).</summary>
		private static string Escape(string value) {
			StringBuilder stringBuilder=new StringBuilder(Uri.EscapeDataString(value));
			stringBuilder.Replace("!","%21").Replace("*","%2A").Replace("'","%27").Replace("(","%28").Replace(")","%29");
			return stringBuilder.ToString();
		}
	}

	///<summary>Talks to USPS directly, with the office's own app: POST /oauth2/v3/token (client credentials) and GET /addresses/v3/address.
	///The access token (about 8 hours) is kept in memory only, per process and per configuration fingerprint, and requested again 5 minutes before
	///it expires. A 401 on the address call gets one new token and one retry. Nothing is retried automatically otherwise: every lookup is billed.
	///Only apis.usps.com and apis-tem.usps.com are ever contacted. Never logs the URL (it carries the address), the token or the secret.</summary>
	public class UspsApiClient {
		public const string HostProduction="apis.usps.com";
		public const string HostTesting="apis-tem.usps.com";
		///<summary>The scope asked for: least privilege.</summary>
		public const string Scope="addresses";
		///<summary>Timeout for the token call and for the address call, each.</summary>
		public static readonly TimeSpan CallTimeout=TimeSpan.FromSeconds(15);
		private const string VENDOR_NAME="USPS";
		private static readonly Lazy<UspsApiClient> _lazyDefault=new Lazy<UspsApiClient>(() => new UspsApiClient());
		private static readonly JsonSerializerSettings _jsonSerializerSettings=new JsonSerializerSettings {
			MissingMemberHandling=MissingMemberHandling.Ignore,
			//A field whose type changed in a later API release is skipped rather than failing the whole answer.
			Error=(sender,args) => args.ErrorContext.Handled=true,
		};

		private readonly HttpMessageHandler _httpMessageHandler;
		private readonly Func<DateTime> _funcUtcNow;
		private readonly Action<string> _actionLog;
		private readonly ConcurrentDictionary<UspsEnvironment,HttpClient> _dictHttpClients=new ConcurrentDictionary<UspsEnvironment,HttpClient>();
		private readonly ConcurrentDictionary<string,TokenCache> _dictTokenCaches=new ConcurrentDictionary<string,TokenCache>();

		///<summary>The process-wide client used by the program.</summary>
		public static UspsApiClient Default {
			get {
				return _lazyDefault.Value;
			}
		}

		///<summary>httpMessageHandler is for tests (a fake handler); null uses the real network through VendorHttp. funcUtcNow is the clock for token
		///lifetimes and the quota cool-down. actionLog receives redacted failure lines; null writes them to the program's "USPS" log.</summary>
		public UspsApiClient(HttpMessageHandler httpMessageHandler=null,Func<DateTime> funcUtcNow=null,Action<string> actionLog=null) {
			_httpMessageHandler=httpMessageHandler;
			_funcUtcNow=funcUtcNow??(() => DateTime.UtcNow);
			_actionLog=actionLog??(x => Logger.WriteLine(x,"USPS"));
		}

		///<summary>https://apis.usps.com/ or https://apis-tem.usps.com/.</summary>
		public static Uri GetBaseUri(UspsEnvironment uspsEnvironment) {
			return new Uri("https://"+(uspsEnvironment==UspsEnvironment.Testing ? HostTesting : HostProduction)+"/");
		}

		///<summary>The token endpoint for the environment.</summary>
		public static Uri GetTokenUri(UspsEnvironment uspsEnvironment) {
			return new Uri(GetBaseUri(uspsEnvironment),"oauth2/v3/token");
		}

		///<summary>The address endpoint with the query for the environment.</summary>
		public static Uri GetAddressUri(UspsEnvironment uspsEnvironment,UspsAddressQuery uspsAddressQuery) {
			return new Uri(GetBaseUri(uspsEnvironment),"addresses/v3/address?"+uspsAddressQuery.ToQueryString());
		}

		private HttpClient GetHttpClient(UspsEnvironment uspsEnvironment) {
			return _dictHttpClients.GetOrAdd(uspsEnvironment,x => {
				VendorHttpOptions options=new VendorHttpOptions();
				options.VendorName=VENDOR_NAME;
				options.BaseAddress=GetBaseUri(x);
				options.ListAllowedHosts=new List<string> { GetBaseUri(x).Host };
				options.Timeout=CallTimeout;
				//No automatic retries: every address lookup is billed, and a 429 must not be repeated.
				options.MaxGetRetries=0;
				if(_httpMessageHandler!=null) {
					return VendorHttp.CreateClient(options,_httpMessageHandler);
				}
				return VendorHttp.GetClient(options);
			});
		}

		private TokenCache GetTokenCache(UspsConfig uspsConfig) {
			string key=uspsConfig.Fingerprint;
			if(string.IsNullOrEmpty(key)) {
				//Callers that build a config by hand (Test connection, tests) get a fingerprint of the plain values. It stays in memory only.
				key=TokenCache.Fingerprint(uspsConfig.ConsumerKey,uspsConfig.ConsumerSecret,uspsConfig.Environment.ToString());
			}
			return _dictTokenCaches.GetOrAdd(uspsConfig.Environment+"|"+key,x => new TokenCache(TokenCache.DefaultRefreshMargin,_funcUtcNow));
		}

		///<summary>Forgets the cached token and any earlier rejection for this configuration, so the next call asks USPS again (Test connection).</summary>
		public void Reset(UspsConfig uspsConfig) {
			TokenCache tokenCache=GetTokenCache(uspsConfig);
			tokenCache.Invalidate();
			tokenCache.ClearReauthorization();
		}

		///<summary>Forgets every cached token and rejection in this process, e.g. after the setup window saves, so a problem the office fixed at USPS
		///(a licence linked, access granted) is retried without restarting the program.</summary>
		public void ResetAll() {
			_dictTokenCaches.Clear();
		}

		///<summary>The cached access token, or a new one from USPS. Throws UspsApiException.</summary>
		public async Task<OAuthAccessToken> GetAccessTokenAsync(UspsConfig uspsConfig,CancellationToken cancellationToken) {
			ThrowIfIncomplete(uspsConfig);
			try {
				return await GetTokenCache(uspsConfig).GetAsync(ct => RequestTokenAsync(uspsConfig,ct),cancellationToken).ConfigureAwait(false);
			}
			catch(Exception ex) {
				throw MapException(ex);
			}
		}

		///<summary>Checks one address. Returns USPS's answer; on a 404 (no match) a response with address==null and ErrorMessage set to USPS's message.
		///Throws UspsApiException for everything else that isn't a 200.</summary>
		public async Task<USPSAddressValidationResponse> GetAddressAsync(UspsConfig uspsConfig,UspsAddressQuery uspsAddressQuery,
			CancellationToken cancellationToken)
		{
			ThrowIfIncomplete(uspsConfig);
			if(uspsAddressQuery==null) {
				throw new ArgumentNullException(nameof(uspsAddressQuery));
			}
			HttpClient httpClient=GetHttpClient(uspsConfig.Environment);
			TokenCache tokenCache=GetTokenCache(uspsConfig);
			Uri uriAddress=GetAddressUri(uspsConfig.Environment,uspsAddressQuery);
			Stopwatch stopwatch=Stopwatch.StartNew();
			HttpResponseMessage response;
			try {
				response=await tokenCache.SendWithAuthRetryAsync(httpClient,
					() => {
						HttpRequestMessage request=new HttpRequestMessage(HttpMethod.Get,uriAddress);
						request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
						VendorHttp.SetEndpointName(request,"address");
						VendorHttp.SetTimeout(request,CallTimeout);
						VendorHttp.SetNoRetry(request);
						return request;
					},
					ct => RequestTokenAsync(uspsConfig,ct),cancellationToken).ConfigureAwait(false);
			}
			catch(Exception ex) {
				UspsApiException uspsApiException=MapException(ex);
				Log("address",uspsApiException.HttpStatus,uspsApiException.Kind.ToString(),stopwatch);
				throw uspsApiException;
			}
			using(response) {
				string body=(response.Content==null) ? "" : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
				int status=(int)response.StatusCode;
				if(status==200) {
					USPSAddressValidationResponse uspsAddressValidationResponse=Deserialize(body);
					if(uspsAddressValidationResponse==null) {
						Log("address",status,"unreadable",stopwatch);
						throw new UspsApiException(UspsErrorKind.Offline,status,"","USPS sent an answer this program could not read.");
					}
					if(uspsAddressValidationResponse.address==null) {
						uspsAddressValidationResponse.ErrorMessage="USPS did not return an address.";
					}
					return uspsAddressValidationResponse;
				}
				string uspsMessage=ReadErrorMessage(body);
				Log("address",status,ReadErrorCode(body),stopwatch);
				if(status==404) {
					USPSAddressValidationResponse uspsAddressValidationResponse=new USPSAddressValidationResponse();
					uspsAddressValidationResponse.ErrorMessage=(uspsMessage=="") ? "USPS could not find this address." : uspsMessage;
					return uspsAddressValidationResponse;
				}
				throw MapStatus(status,uspsMessage,response);
			}
		}

		///<summary>POST /oauth2/v3/token with grant_type=client_credentials and scope=addresses. The key and secret go in the form body, as USPS's own
		///examples send them.</summary>
		private async Task<OAuthAccessToken> RequestTokenAsync(UspsConfig uspsConfig,CancellationToken cancellationToken) {
			HttpClient httpClient=GetHttpClient(uspsConfig.Environment);
			Stopwatch stopwatch=Stopwatch.StartNew();
			using(HttpRequestMessage request=new HttpRequestMessage(HttpMethod.Post,GetTokenUri(uspsConfig.Environment))) {
				request.Content=new FormUrlEncodedContent(OAuthTokenClient.GetClientCredentialsForm(Scope,uspsConfig.ConsumerKey.Trim(),uspsConfig.ConsumerSecret));
				request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
				VendorHttp.SetEndpointName(request,"token");
				VendorHttp.SetTimeout(request,CallTimeout);
				using(HttpResponseMessage response=await httpClient.SendAsync(request,cancellationToken).ConfigureAwait(false)) {
					string body=(response.Content==null) ? "" : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
					int status=(int)response.StatusCode;
					if(status==400 || status==401) {
						Log("token",status,ReadErrorCode(body),stopwatch);
						//Throws TokenRejectedException, which marks this configuration as rejected until its settings change or Reset is called.
						return OAuthTokenClient.ParseResponse(status,body,_funcUtcNow());
					}
					if(status<200 || status>299) {
						Log("token",status,ReadErrorCode(body),stopwatch);
						throw MapStatus(status,ReadErrorMessage(body),response);
					}
					return OAuthTokenClient.ParseResponse(status,body,_funcUtcNow());
				}
			}
		}

		private static void ThrowIfIncomplete(UspsConfig uspsConfig) {
			if(uspsConfig==null || !uspsConfig.IsComplete) {
				throw new UspsApiException(UspsErrorKind.NotConfigured,0,"","USPS address validation needs a consumer key and secret.");
			}
		}

		///<summary>Turns a non-success status into the exception for its kind. Starts the quota cool-down on a 429.</summary>
		private UspsApiException MapStatus(int status,string uspsMessage,HttpResponseMessage response) {
			string detail=(uspsMessage=="") ? "" : " "+uspsMessage;
			switch(status) {
				case 400:
					return new UspsApiException(UspsErrorKind.BadRequest,status,uspsMessage,"USPS could not check this address."+detail);
				case 401:
					return new UspsApiException(UspsErrorKind.Credentials,status,uspsMessage,"USPS rejected the app credentials."+detail);
				case 403:
					return new UspsApiException(UspsErrorKind.Forbidden,status,uspsMessage,"Your USPS app does not have access to the Addresses API."+detail);
				case 429:
					UspsAddresses.StartQuotaCooldown(_funcUtcNow());
					UspsApiException uspsApiException=new UspsApiException(UspsErrorKind.Quota,status,uspsMessage,
						"USPS address checks are over the limit right now.");
					uspsApiException.RetryAfter=GetRetryAfter(response);
					return uspsApiException;
				default:
					return new UspsApiException(UspsErrorKind.Offline,status,uspsMessage,"USPS is not available right now (HTTP "+status+").");
			}
		}

		private TimeSpan? GetRetryAfter(HttpResponseMessage response) {
			RetryConditionHeaderValue retryAfter=response?.Headers.RetryAfter;
			if(retryAfter==null) {
				return null;
			}
			if(retryAfter.Delta.HasValue) {
				return retryAfter.Delta.Value;
			}
			if(retryAfter.Date.HasValue) {
				TimeSpan delta=retryAfter.Date.Value.UtcDateTime-_funcUtcNow();
				return (delta<TimeSpan.Zero) ? TimeSpan.Zero : delta;
			}
			return null;
		}

		///<summary>Maps whatever the token cache or HTTP stack threw to a UspsApiException.</summary>
		private static UspsApiException MapException(Exception ex) {
			if(ex is AggregateException aggregateException && aggregateException.InnerExceptions.Count==1) {
				ex=aggregateException.InnerExceptions[0];
			}
			if(ex is UspsApiException uspsApiException) {
				return uspsApiException;
			}
			if(ex is TokenRejectedException tokenRejectedException) {
				return new UspsApiException(UspsErrorKind.Credentials,tokenRejectedException.StatusCode,tokenRejectedException.Error,
					"USPS rejected the app credentials"+(tokenRejectedException.Error=="" ? "" : " ("+tokenRejectedException.Error+")")+".",ex);
			}
			if(ex is ReauthorizationRequiredException) {
				return new UspsApiException(UspsErrorKind.Credentials,0,"",
					"USPS rejected the app credentials earlier in this session. Check them in the setup window and use Test connection.",ex);
			}
			//VendorHttpException (refused URL, timeout), HttpRequestException (network), TaskCanceledException, an unreadable token answer.
			return new UspsApiException(UspsErrorKind.Offline,0,"","Could not reach USPS to check the address.",ex);
		}

		///<summary>Parses a 200 answer. Null if it isn't JSON.</summary>
		public static USPSAddressValidationResponse Deserialize(string body) {
			if(string.IsNullOrWhiteSpace(body)) {
				return null;
			}
			JObject jObject;
			try {
				jObject=JObject.Parse(body);
			}
			catch(Exception ex) {
				ex.DoNothing();
				return null;
			}
			//Each part is read on its own, so a part whose shape changed in a later API release is skipped without losing the rest.
			USPSAddressValidationResponse response=new USPSAddressValidationResponse();
			response.firm=ReadPart<string>(jObject["firm"]);
			response.address=ReadPart<USPSAddressValidationAddress>(jObject["address"]);
			response.additionalInfo=ReadPart<USPSAddressValidationAdditionalInfo>(jObject["additionalInfo"]);
			response.corrections=ReadPart<List<USPSAddressValidationCodeText>>(jObject["corrections"]);
			response.matches=ReadPart<List<USPSAddressValidationCodeText>>(jObject["matches"]);
			response.warnings=ReadPart<List<string>>(jObject["warnings"]);
			return response;
		}

		private static T ReadPart<T>(JToken jToken) where T:class {
			if(jToken==null || jToken.Type==JTokenType.Null) {
				return null;
			}
			try {
				return jToken.ToObject<T>(JsonSerializer.Create(_jsonSerializerSettings));
			}
			catch(Exception ex) {
				ex.DoNothing();
				return null;
			}
		}

		///<summary>error.message from USPS's ErrorMessage body, else the first error's detail or title, else "".</summary>
		public static string ReadErrorMessage(string body) {
			JObject jObjectError=ReadErrorObject(body);
			if(jObjectError==null) {
				return "";
			}
			string message=jObjectError.Value<string>("message");
			if(string.IsNullOrWhiteSpace(message) && jObjectError["errors"] is JArray jArrayErrors && jArrayErrors.Count>0
				&& jArrayErrors[0] is JObject jObjectFirst)
			{
				message=jObjectFirst.Value<string>("detail");
				if(string.IsNullOrWhiteSpace(message)) {
					message=jObjectFirst.Value<string>("title");
				}
			}
			return (message??"").Trim();
		}

		///<summary>error.code from USPS's ErrorMessage body (or the OAuth "error" field), for log lines. "" if none.</summary>
		public static string ReadErrorCode(string body) {
			JObject jObjectError=ReadErrorObject(body);
			string code=jObjectError?["code"]?.ToString();
			return (code??"").Trim();
		}

		private static JObject ReadErrorObject(string body) {
			if(string.IsNullOrWhiteSpace(body)) {
				return null;
			}
			try {
				JObject jObject=JObject.Parse(body);
				if(jObject["error"] is JObject jObjectError) {
					return jObjectError;
				}
				if(jObject["error"]!=null) {//OAuth style: { "error": "invalid_client", "error_description": "..." }
					JObject jObjectOAuth=new JObject();
					jObjectOAuth["code"]=jObject["error"].ToString();
					jObjectOAuth["message"]=jObject.Value<string>("error_description");
					return jObjectOAuth;
				}
				return null;
			}
			catch(Exception ex) {
				ex.DoNothing();
				return null;
			}
		}

		///<summary>One redacted line: endpoint, status, USPS error code and elapsed time. Never the URL, address, token or secret.</summary>
		private void Log(string endpoint,int status,string code,Stopwatch stopwatch) {
			try {
				_actionLog(VENDOR_NAME+" "+endpoint+": HTTP "+status+(string.IsNullOrEmpty(code) ? "" : " ("+code+")")+", "+stopwatch.ElapsedMilliseconds+" ms");
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
		}
	}
}
