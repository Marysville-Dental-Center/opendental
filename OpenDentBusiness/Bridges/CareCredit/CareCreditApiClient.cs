// CareCredit (Synchrony) patient financing: HTTP client for Synchrony's Patient Data Service, called from this workstation with the office's own
// credentials. Never contacts any other host.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CodeBase;
using CodeBase.VendorDirect;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenDentBusiness.Bridges {
	///<summary>Talks to the office's configured Synchrony API host through VendorHttp: HTTPS only, never an Open Dental host, only the API host on the
	///allow-list, GET retries only, redacted logs. Access tokens come from the OAuth client-credentials grant and live in memory only (TokenCache,
	///keyed by the configuration); a 401 refreshes the token once and retries once. Endpoint paths, the session parameter and the client
	///authentication style are settings from Synchrony's implementation guide. Request field names are this program's; adjust ToJson in
	///CareCreditDtos.cs if the gated reference names them differently. POSTs are never retried automatically.</summary>
	public sealed class CareCreditApiClient : ICareCreditApiClient {
		public const string VENDOR_NAME="CareCredit";
		private readonly HttpMessageHandler _innerHandler;
		private readonly Func<DateTime> _funcUtcNow;
		private readonly ConcurrentDictionary<string,HttpClient> _dictClients=new ConcurrentDictionary<string,HttpClient>();

		///<summary>innerHandler is for tests (a fake handler); production passes null and gets VendorHttp's shared client.</summary>
		public CareCreditApiClient(HttpMessageHandler innerHandler=null,Func<DateTime> funcUtcNow=null) {
			_innerHandler=innerHandler;
			_funcUtcNow=funcUtcNow ?? (() => DateTime.UtcNow);
		}

		private HttpClient GetClient(CareCreditConfig config) {
			string urlProblem=CareCreditConfig.GetUrlProblem(config.ApiBaseUrl);
			if(urlProblem!="") {
				throw new CareCredit.CareCreditException(0,"CareCredit API base URL: "+urlProblem);
			}
			Uri uriBase=new Uri(config.ApiBaseUrl.Trim());
			VendorHttpOptions options=new VendorHttpOptions();
			options.VendorName=VENDOR_NAME;
			options.BaseAddress=new Uri(uriBase.GetLeftPart(UriPartial.Authority)+"/");
			options.ListAllowedHosts=new List<string> { uriBase.Host };
			options.Log=x => Logger.WriteLine(x,"CareCredit");
			if(_innerHandler==null) {
				return VendorHttp.GetClient(options);
			}
			return _dictClients.GetOrAdd(options.BaseAddress.AbsoluteUri,x => VendorHttp.CreateClient(options,_innerHandler));
		}

		public async Task<OAuthAccessToken> RequestTokenAsync(CareCreditConfig config,CancellationToken cancellationToken=default(CancellationToken)) {
			HttpClient httpClient=GetClient(config);
			Uri uriToken=GetEndpointOrThrow(config,config.TokenPath);
			string scope=string.IsNullOrWhiteSpace(config.Scope) ? null : config.Scope.Trim();
			bool isBody=(config.ClientAuth==CareCreditClientAuth.FormBody);
			Dictionary<string,string> dictForm=OAuthTokenClient.GetClientCredentialsForm(scope,
				isBody ? config.ClientId : null,isBody ? config.ClientSecret : null);
			return await OAuthTokenClient.RequestAsync(httpClient,uriToken,dictForm,isBody ? null : config.ClientId,isBody ? null : config.ClientSecret,
				cancellationToken,_funcUtcNow).ConfigureAwait(false);
		}

		public async Task<CareCreditPrefillResult> PrefillAsync(CareCreditConfig config,CareCreditPrefillRequest request,
			CancellationToken cancellationToken=default(CancellationToken))
		{
			Uri uri=GetEndpointOrThrow(config,config.PrefillPath);
			string body=request.ToJson().ToString(Formatting.None);
			string json=await SendAsync(config,() => CreateJsonPost(uri,body,"prefill"),cancellationToken).ConfigureAwait(false);
			JObject jObject=CareCreditJson.TryParse(json);
			if(jObject==null) {
				throw new CareCredit.CareCreditException(0,"CareCredit sent an answer this program couldn't read.");
			}
			CareCreditPrefillResult result=new CareCreditPrefillResult();
			result.RawJson=CareCreditJson.Scrub(json);
			result.ReferenceId=CareCreditJson.GetString(jObject,"referenceId","refId","referenceNumber","refNumber");
			result.PageUrl=CareCreditJson.GetString(jObject,"url","pageUrl","landingPageUrl","hostedPageUrl","redirectUrl");
			result.SessionId=CareCreditJson.GetString(jObject,"sessionId","session_id");
			if(result.SessionId=="") {
				result.SessionId=CareCredit.GetSessionIdFromUrl(result.PageUrl,config.SessionParam);
			}
			return result;
		}

		public async Task<CareCreditPullbackResult> PullbackAsync(CareCreditConfig config,string referenceId,string merchantNumber,bool isQuickScreen,
			CancellationToken cancellationToken=default(CancellationToken))
		{
			if(string.IsNullOrWhiteSpace(referenceId)) {
				throw new ArgumentException("A reference ID is required.",nameof(referenceId));
			}
			Func<HttpRequestMessage> createRequest;
			if(config.PullbackPath.Contains(CareCreditSettings.REF_ID_PLACEHOLDER)) {
				//The reference ID goes in the path: a GET, which VendorHttp may retry.
				Uri uri=GetEndpointOrThrow(config,config.PullbackPath.Replace(CareCreditSettings.REF_ID_PLACEHOLDER,Uri.EscapeDataString(referenceId.Trim())));
				createRequest=() => {
					HttpRequestMessage requestMessage=new HttpRequestMessage(HttpMethod.Get,uri);
					requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
					VendorHttp.SetEndpointName(requestMessage,"pullback");
					return requestMessage;
				};
			}
			else {
				Uri uri=GetEndpointOrThrow(config,config.PullbackPath);
				JObject jObjectBody=new JObject();
				jObjectBody["partnerCode"]=config.PartnerCode;
				jObjectBody["referenceId"]=referenceId.Trim();
				CareCreditPrefillRequest.AddIfNotBlank(jObjectBody,"merchantNumber",merchantNumber);
				string body=jObjectBody.ToString(Formatting.None);
				createRequest=() => CreateJsonPost(uri,body,"pullback");
			}
			string json=await SendAsync(config,createRequest,cancellationToken).ConfigureAwait(false);
			if(CareCreditJson.TryParse(json)==null) {
				throw new CareCredit.CareCreditException(0,"CareCredit sent an answer this program couldn't read.");
			}
			return CareCreditJson.ReadPullback(json,isQuickScreen);
		}

		public async Task<string> SubmitBatchAsync(CareCreditConfig config,CareCreditBatchRequest request,
			CancellationToken cancellationToken=default(CancellationToken))
		{
			Uri uri=GetEndpointOrThrow(config,config.BatchPath);
			string body=request.ToJson().ToString(Formatting.None);
			string json=await SendAsync(config,() => CreateJsonPost(uri,body,"batch"),cancellationToken).ConfigureAwait(false);
			JObject jObject=CareCreditJson.TryParse(json);
			if(jObject==null) {
				throw new CareCredit.CareCreditException(0,"CareCredit sent an answer this program couldn't read.");
			}
			return CareCreditJson.GetString(jObject,"referenceId","refId","referenceNumber","refNumber");
		}

		private static Uri GetEndpointOrThrow(CareCreditConfig config,string path) {
			try {
				return config.GetEndpoint(path);
			}
			catch(ArgumentException ex) {
				throw new CareCredit.CareCreditException(0,ex.Message);
			}
		}

		private static HttpRequestMessage CreateJsonPost(Uri uri,string body,string endpointName) {
			HttpRequestMessage requestMessage=new HttpRequestMessage(HttpMethod.Post,uri);
			requestMessage.Content=new StringContent(body,Encoding.UTF8,"application/json");
			requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
			VendorHttp.SetEndpointName(requestMessage,endpointName);
			VendorHttp.SetNoRetry(requestMessage);
			return requestMessage;
		}

		///<summary>Sends with the cached bearer token and returns the body of a 2xx answer. Refused credentials (at the token endpoint, or a second 401)
		///throw CareCreditException with IsAuthFailure. Other HTTP errors throw CareCreditException with the status and the vendor's error code
		///(never the body).</summary>
		private async Task<string> SendAsync(CareCreditConfig config,Func<HttpRequestMessage> createRequest,CancellationToken cancellationToken) {
			HttpClient httpClient=GetClient(config);
			TokenCache tokenCache=TokenCache.For(VENDOR_NAME,config.GetTokenFingerprint());
			HttpResponseMessage response;
			try {
				response=await tokenCache.SendWithAuthRetryAsync(httpClient,createRequest,ct => RequestTokenAsync(config,ct),cancellationToken)
					.ConfigureAwait(false);
			}
			catch(TokenRejectedException ex) {
				throw new CareCredit.CareCreditException(401,"CareCredit rejected the configured credentials."+(ex.Error=="" ? "" : " ("+ex.Error+")"),
					isAuthFailure:true);
			}
			catch(ReauthorizationRequiredException) {
				throw new CareCredit.CareCreditException(401,"CareCredit rejected the configured credentials. Check them in CareCredit Setup.",
					isAuthFailure:true);
			}
			using(response) {
				string body=(response.Content==null) ? "" : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
				int statusCode=(int)response.StatusCode;
				if(response.StatusCode==HttpStatusCode.Unauthorized) {
					throw new CareCredit.CareCreditException(401,"CareCredit rejected the configured credentials.",isAuthFailure:true);
				}
				if(statusCode<200 || statusCode>299) {
					JObject jObjectError=CareCreditJson.TryParse(body);
					string errorCode=CareCreditJson.GetString(jObjectError,"errorCode","code","error");
					string message="CareCredit answered with HTTP "+statusCode+(errorCode=="" ? "" : " ("+errorCode+")")+".";
					CareCredit.CareCreditException careCreditException=new CareCredit.CareCreditException(statusCode,message);
					careCreditException.ResponseJson=CareCreditJson.Scrub(body);
					if(response.Headers.RetryAfter?.Delta!=null) {
						careCreditException.RetryAfter=response.Headers.RetryAfter.Delta;
					}
					throw careCreditException;
				}
				return body;
			}
		}
	}
}
