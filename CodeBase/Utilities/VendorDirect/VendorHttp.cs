// Vendor-direct integrations: the HttpClient policy every vendor call goes through.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBase.VendorDirect {
	///<summary>Settings for one vendor's HttpClient. Built once per vendor and base URL.</summary>
	public sealed class VendorHttpOptions {
		///<summary>Short vendor name used in log lines and exceptions, e.g. "USPS". Required.</summary>
		public string VendorName;
		///<summary>The vendor's base URL, or null to use absolute request URLs only. Must pass VendorUrlPolicy.</summary>
		public Uri BaseAddress;
		///<summary>Host patterns this client may reach ("api.example.com", "*.example.com"). Empty means any host VendorUrlPolicy allows.
		///Redirects are checked against the same list.</summary>
		public List<string> ListAllowedHosts=new List<string>();
		///<summary>Per-request timeout unless the request sets its own with VendorHttp.SetTimeout. Default 30 seconds.</summary>
		public TimeSpan Timeout=TimeSpan.FromSeconds(30);
		///<summary>Retries for GET and HEAD after a network error, a timeout or a 408/429/500/502/503/504. Never for other methods. Default 2.</summary>
		public int MaxGetRetries=2;
		///<summary>First retry delay before jitter. Doubles each retry. Default 500 ms.</summary>
		public TimeSpan RetryBaseDelay=TimeSpan.FromMilliseconds(500);
		///<summary>The longest a retry may wait, Retry-After included. A longer Retry-After returns the response instead. Default 10 seconds.</summary>
		public TimeSpan MaxRetryWait=TimeSpan.FromSeconds(10);
		///<summary>After a network-level failure, fail every call at once for CircuitBreakDuration. Off by default.</summary>
		public bool UseCircuitBreaker;
		///<summary>Default 30 seconds.</summary>
		public TimeSpan CircuitBreakDuration=TimeSpan.FromSeconds(30);
		///<summary>Redirects followed for GET and HEAD, each one checked against the policy. Other methods get the 3xx back. Default 5.</summary>
		public int MaxRedirects=5;
		///<summary>A client certificate for mutual TLS (PDMP). Load it with VendorHttp.LoadClientCertificate.</summary>
		public X509Certificate2 ClientCertificate;
		///<summary>Receives one redacted line per call: vendor, endpoint name, status, vendor request ID. Never headers, bodies, tokens or query
		///strings. Null to log nothing (the default).</summary>
		public Action<string> Log;
		///<summary>Clock for the circuit breaker. Tests replace it.</summary>
		public Func<DateTime> GetUtcNow=() => DateTime.UtcNow;
	}

	///<summary>Why VendorHttp refused or stopped a call.</summary>
	public enum VendorHttpFailure {
		///<summary>The URL failed VendorUrlPolicy (an Open Dental host, plain http, a host not on the allow-list, or not absolute).</summary>
		UrlRefused,
		///<summary>The vendor was marked unreachable after a recent network failure.</summary>
		CircuitOpen,
		///<summary>No response within the timeout.</summary>
		Timeout,
		///<summary>More redirects than MaxRedirects.</summary>
		TooManyRedirects,
	}

	///<summary>A call VendorHttp refused or gave up on. Derives from HttpRequestException so existing network error handling catches it.</summary>
	public class VendorHttpException : HttpRequestException {
		public string VendorName { get; }
		public VendorHttpFailure Failure { get; }
		///<summary>Set when Failure is UrlRefused.</summary>
		public VendorUrlProblem UrlProblem { get; }

		public VendorHttpException(string vendorName,VendorHttpFailure failure,string message,VendorUrlProblem urlProblem=VendorUrlProblem.None,
			Exception innerException=null) : base(message,innerException)
		{
			VendorName=vendorName;
			Failure=failure;
			UrlProblem=urlProblem;
		}
	}

	///<summary>One HttpClient per vendor and base URL, behind VendorPolicyHandler: never an Open Dental host, HTTPS only (except to this computer),
	///an optional host allow-list, per-route timeouts, retries for idempotent GETs only, an optional circuit breaker and redacted logging.
	///POSTs that move money or send messages are never retried here: use the vendor's idempotency key or a correlation ID and reconcile.
	///Every vendor call runs on the client workstation (Meth.NoCheckMiddleTierRole in the business layer); the middle tier server never calls a vendor.
	///Synchronous callers use RunSync, which blocks a pool thread rather than the UI thread's context.</summary>
	public static class VendorHttp {
		private const string PROPERTY_TIMEOUT="VendorHttp.Timeout";
		private const string PROPERTY_ENDPOINT="VendorHttp.Endpoint";
		private const string PROPERTY_NO_RETRY="VendorHttp.NoRetry";
		private static readonly ConcurrentDictionary<string,HttpClient> _dictClients=new ConcurrentDictionary<string,HttpClient>();
		private static readonly string[] _arraySensitiveHeaders={ "Authorization","Proxy-Authorization","API-Key","Secret","Subscription-Key",
			"Ocp-Apim-Subscription-Key","Cookie","Set-Cookie" };
		private static readonly string[] _arrayRequestIdHeaders={ "x-request-id","request-id","x-ms-request-id","client-request-id","intuit_tid",
			"x-correlation-id","x-amzn-requestid" };

		///<summary>The shared client for options.VendorName and options.BaseAddress, created on first use. Later calls with the same vendor and base URL
		///get the same client and ignore their other options.</summary>
		public static HttpClient GetClient(VendorHttpOptions options) {
			ValidateOptions(options);
			string key=options.VendorName+"|"+(options.BaseAddress?.AbsoluteUri??"");
			return _dictClients.GetOrAdd(key,x => CreateClient(options));
		}

		///<summary>A new client with the policy handler over innerHandler. Tests pass a fake handler; production code uses GetClient, which passes
		///null for an HttpClientHandler with automatic redirects off (the policy handler follows them itself, checking each hop).</summary>
		public static HttpClient CreateClient(VendorHttpOptions options,HttpMessageHandler innerHandler=null) {
			ValidateOptions(options);
			if(innerHandler==null) {
				HttpClientHandler httpClientHandler=new HttpClientHandler();
				httpClientHandler.AllowAutoRedirect=false;
				httpClientHandler.UseCookies=false;
				httpClientHandler.AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate;
				if(options.ClientCertificate!=null) {
					httpClientHandler.ClientCertificateOptions=ClientCertificateOption.Manual;
					httpClientHandler.ClientCertificates.Add(options.ClientCertificate);
				}
				innerHandler=httpClientHandler;
			}
			HttpClient httpClient=new HttpClient(new VendorPolicyHandler(options,innerHandler));
			//The policy handler applies the per-request timeout to the headers. This outer limit only bounds reading a body that stalls.
			httpClient.Timeout=TimeSpan.FromMinutes(10);
			httpClient.BaseAddress=options.BaseAddress;
			httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(ProductIdentity.UserAgent);
			return httpClient;
		}

		private static void ValidateOptions(VendorHttpOptions options) {
			if(options==null) {
				throw new ArgumentNullException(nameof(options));
			}
			if(string.IsNullOrWhiteSpace(options.VendorName)) {
				throw new ArgumentException("VendorName is required.",nameof(options));
			}
			if(options.BaseAddress!=null) {
				VendorUrlProblem problem=VendorUrlPolicy.Check(options.BaseAddress,options.ListAllowedHosts);
				if(problem!=VendorUrlProblem.None) {
					throw new VendorHttpException(options.VendorName,VendorHttpFailure.UrlRefused,
						options.VendorName+": base address refused. "+VendorUrlPolicy.Describe(problem),problem);
				}
			}
		}

		///<summary>Overrides the client's timeout for this request (Graph send 100 s, PayConnect terminals 190 s).</summary>
		public static void SetTimeout(HttpRequestMessage request,TimeSpan timeout) {
			request.Properties[PROPERTY_TIMEOUT]=timeout;
		}

		///<summary>Names the endpoint for log lines ("addresses", "deposit"), so URLs that carry PHI are never logged.</summary>
		public static void SetEndpointName(HttpRequestMessage request,string endpointName) {
			request.Properties[PROPERTY_ENDPOINT]=endpointName;
		}

		///<summary>Turns off the GET retries for this request, for a GET with side effects or an interactive call that must fail fast.</summary>
		public static void SetNoRetry(HttpRequestMessage request) {
			request.Properties[PROPERTY_NO_RETRY]=true;
		}

		internal static TimeSpan GetTimeout(HttpRequestMessage request,TimeSpan timeSpanDefault) {
			object value;
			if(request.Properties.TryGetValue(PROPERTY_TIMEOUT,out value) && value is TimeSpan) {
				return (TimeSpan)value;
			}
			return timeSpanDefault;
		}

		internal static string GetEndpointName(HttpRequestMessage request) {
			object value;
			if(request.Properties.TryGetValue(PROPERTY_ENDPOINT,out value) && value is string) {
				return (string)value;
			}
			return request.Method.Method+" "+(request.RequestUri?.Host??"");//The host only: paths and queries can carry PHI.
		}

		internal static bool IsNoRetry(HttpRequestMessage request) {
			object value;
			return request.Properties.TryGetValue(PROPERTY_NO_RETRY,out value) && value is bool && (bool)value;
		}

		///<summary>True for headers that carry credentials: Authorization, API-Key, Secret, Subscription-Key, any X-Auth-* and cookies.
		///Never log these.</summary>
		public static bool IsSensitiveHeader(string name) {
			if(string.IsNullOrEmpty(name)) {
				return false;
			}
			if(name.StartsWith("X-Auth",StringComparison.OrdinalIgnoreCase)) {
				return true;
			}
			return _arraySensitiveHeaders.Any(x => x.Equals(name,StringComparison.OrdinalIgnoreCase));
		}

		///<summary>The vendor's request ID from a response, for log lines and support calls, or "".</summary>
		public static string GetVendorRequestId(HttpResponseMessage response) {
			if(response==null) {
				return "";
			}
			for(int i=0;i<_arrayRequestIdHeaders.Length;i++) {
				IEnumerable<string> listValues;
				if(response.Headers.TryGetValues(_arrayRequestIdHeaders[i],out listValues)) {
					string value=listValues.FirstOrDefault();
					if(!string.IsNullOrEmpty(value)) {
						return value;
					}
				}
			}
			return "";
		}

		///<summary>Loads a .pfx/.p12 client certificate. EphemeralKeySet (the default) keeps the private key in memory, never in the user's key store.
		///Pass isEphemeral false only if the TLS stack in use can't present an ephemeral key.</summary>
		public static X509Certificate2 LoadClientCertificate(byte[] pfxBytes,string password,bool isEphemeral=true) {
			X509KeyStorageFlags flags=isEphemeral ? X509KeyStorageFlags.EphemeralKeySet : X509KeyStorageFlags.UserKeySet;
			return new X509Certificate2(pfxBytes,password,flags);
		}

		///<summary>Runs an async vendor call from synchronous code on a pool thread, so it never deadlocks on the UI thread's context.</summary>
		public static T RunSync<T>(Func<Task<T>> funcAsync) {
			return Task.Run(funcAsync).GetAwaiter().GetResult();
		}

		///<summary>RunSync for calls without a result.</summary>
		public static void RunSync(Func<Task> funcAsync) {
			Task.Run(funcAsync).GetAwaiter().GetResult();
		}
	}

	///<summary>The DelegatingHandler behind every VendorHttp client. See VendorHttp.</summary>
	public sealed class VendorPolicyHandler : DelegatingHandler {
		private static readonly Random _random=new Random();
		private readonly VendorHttpOptions _options;
		private long _ticksCircuitOpenUntil;

		public VendorPolicyHandler(VendorHttpOptions options,HttpMessageHandler innerHandler) : base(innerHandler) {
			_options=options;
		}

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken) {
			CheckUrl(request.RequestUri);
			if(_options.UseCircuitBreaker && _options.GetUtcNow().Ticks<Interlocked.Read(ref _ticksCircuitOpenUntil)) {
				Log(request,"refused: vendor marked unreachable");
				throw new VendorHttpException(_options.VendorName,VendorHttpFailure.CircuitOpen,
					_options.VendorName+" could not be reached a moment ago. Try again shortly.");
			}
			bool isIdempotent=(request.Method==HttpMethod.Get || request.Method==HttpMethod.Head);
			bool canRetry=isIdempotent && !VendorHttp.IsNoRetry(request);
			int maxAttempts=canRetry ? Math.Max(0,_options.MaxGetRetries)+1 : 1;
			TimeSpan timeout=VendorHttp.GetTimeout(request,_options.Timeout);
			for(int attempt=1;;attempt++) {
				HttpResponseMessage response;
				using(CancellationTokenSource ctsTimeout=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)) {
					if(timeout!=System.Threading.Timeout.InfiniteTimeSpan) {
						ctsTimeout.CancelAfter(timeout);
					}
					try {
						response=await SendFollowingRedirectsAsync(request,isIdempotent,ctsTimeout.Token).ConfigureAwait(false);
					}
					catch(OperationCanceledException ex) when(!cancellationToken.IsCancellationRequested) {
						OpenCircuit();
						if(attempt<maxAttempts) {
							await Task.Delay(GetBackoff(attempt),cancellationToken).ConfigureAwait(false);
							continue;
						}
						Log(request,"timed out");
						throw new VendorHttpException(_options.VendorName,VendorHttpFailure.Timeout,_options.VendorName+" did not answer in time.",
							innerException:ex);
					}
					catch(HttpRequestException ex) when(!(ex is VendorHttpException)) {
						OpenCircuit();
						if(attempt<maxAttempts) {
							await Task.Delay(GetBackoff(attempt),cancellationToken).ConfigureAwait(false);
							continue;
						}
						Log(request,"failed: "+ex.GetType().Name);
						throw;
					}
				}
				Interlocked.Exchange(ref _ticksCircuitOpenUntil,0);
				if(attempt<maxAttempts && IsRetryableStatus(response.StatusCode)) {
					TimeSpan delay=GetRetryDelay(attempt,response);
					if(delay<=_options.MaxRetryWait) {
						response.Dispose();
						await Task.Delay(delay,cancellationToken).ConfigureAwait(false);
						continue;
					}
				}
				string requestId=VendorHttp.GetVendorRequestId(response);
				Log(request,((int)response.StatusCode).ToString()+(requestId=="" ? "" : " request "+requestId));
				return response;
			}
		}

		private async Task<HttpResponseMessage> SendFollowingRedirectsAsync(HttpRequestMessage request,bool isIdempotent,
			CancellationToken cancellationToken)
		{
			HttpResponseMessage response=await base.SendAsync(request,cancellationToken).ConfigureAwait(false);
			if(!isIdempotent) {
				return response;//A redirected POST goes back to the caller unchanged.
			}
			HttpRequestMessage requestCur=request;
			for(int countRedirects=0;IsRedirect(response.StatusCode);countRedirects++) {
				Uri uriLocation=response.Headers.Location;
				if(uriLocation==null) {
					return response;
				}
				if(!uriLocation.IsAbsoluteUri) {
					uriLocation=new Uri(requestCur.RequestUri,uriLocation);
				}
				if(countRedirects>=_options.MaxRedirects) {
					response.Dispose();
					throw new VendorHttpException(_options.VendorName,VendorHttpFailure.TooManyRedirects,_options.VendorName+" redirected too many times.");
				}
				CheckUrl(uriLocation);
				HttpRequestMessage requestNext=new HttpRequestMessage(requestCur.Method,uriLocation);
				foreach(KeyValuePair<string,IEnumerable<string>> header in requestCur.Headers) {
					//Credentials never follow a redirect to another host.
					if(header.Key.Equals("Authorization",StringComparison.OrdinalIgnoreCase)
						&& !uriLocation.Host.Equals(requestCur.RequestUri.Host,StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}
					requestNext.Headers.TryAddWithoutValidation(header.Key,header.Value);
				}
				foreach(KeyValuePair<string,object> property in requestCur.Properties) {
					requestNext.Properties[property.Key]=property.Value;
				}
				response.Dispose();
				if(requestCur!=request) {
					requestCur.Dispose();
				}
				requestCur=requestNext;
				response=await base.SendAsync(requestCur,cancellationToken).ConfigureAwait(false);
			}
			return response;
		}

		private void CheckUrl(Uri uri) {
			VendorUrlProblem problem=VendorUrlPolicy.Check(uri,_options.ListAllowedHosts);
			if(problem!=VendorUrlProblem.None) {
				_options.Log?.Invoke(_options.VendorName+": refused a request ("+problem+").");
				throw new VendorHttpException(_options.VendorName,VendorHttpFailure.UrlRefused,
					_options.VendorName+": request refused. "+VendorUrlPolicy.Describe(problem),problem);
			}
		}

		private void OpenCircuit() {
			if(!_options.UseCircuitBreaker) {
				return;
			}
			Interlocked.Exchange(ref _ticksCircuitOpenUntil,_options.GetUtcNow().Add(_options.CircuitBreakDuration).Ticks);
		}

		private void Log(HttpRequestMessage request,string outcome) {
			_options.Log?.Invoke(_options.VendorName+" "+VendorHttp.GetEndpointName(request)+": "+outcome);
		}

		internal static bool IsRedirect(HttpStatusCode statusCode) {
			int code=(int)statusCode;
			return code==301 || code==302 || code==303 || code==307 || code==308;
		}

		internal static bool IsRetryableStatus(HttpStatusCode statusCode) {
			int code=(int)statusCode;
			return code==408 || code==429 || code==500 || code==502 || code==503 || code==504;
		}

		private TimeSpan GetRetryDelay(int attempt,HttpResponseMessage response) {
			RetryConditionHeaderValue retryAfter=response.Headers.RetryAfter;
			if(retryAfter!=null) {
				if(retryAfter.Delta.HasValue) {
					return retryAfter.Delta.Value;
				}
				if(retryAfter.Date.HasValue) {
					TimeSpan delta=retryAfter.Date.Value.UtcDateTime-DateTime.UtcNow;
					return (delta<TimeSpan.Zero) ? TimeSpan.Zero : delta;
				}
			}
			return GetBackoff(attempt);
		}

		///<summary>RetryBaseDelay * 2^(attempt-1), plus up to half of that again at random, capped at MaxRetryWait.</summary>
		private TimeSpan GetBackoff(int attempt) {
			double msBase=_options.RetryBaseDelay.TotalMilliseconds*Math.Pow(2,attempt-1);
			double jitter;
			lock(_random) {
				jitter=_random.NextDouble();
			}
			double ms=msBase+msBase*0.5*jitter;
			return TimeSpan.FromMilliseconds(Math.Min(ms,_options.MaxRetryWait.TotalMilliseconds));
		}
	}
}
