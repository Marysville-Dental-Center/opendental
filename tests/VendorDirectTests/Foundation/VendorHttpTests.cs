// Tests for the vendor HttpClient policy, against a fake handler. Nothing is sent over the network.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using CodeBase.VendorDirect;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VendorDirectTests.Foundation {
	[TestClass]
	public class VendorHttpTests {
		private static VendorHttpOptions MakeOptions(List<string> listLog=null) {
			VendorHttpOptions options=new VendorHttpOptions();
			options.VendorName="TestVendor";
			options.BaseAddress=new Uri("https://api.vendor.example/v1/");
			options.RetryBaseDelay=TimeSpan.FromMilliseconds(1);
			if(listLog!=null) {
				options.Log=x => { lock(listLog) { listLog.Add(x); } };
			}
			return options;
		}

		[TestMethod]
		public void Client_SendsNeutralUserAgentAndRelativeUrls() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler().Enqueue(HttpStatusCode.OK,"{}");
				HttpClient httpClient=VendorHttp.CreateClient(MakeOptions(),fake);
				HttpResponseMessage response=await httpClient.GetAsync("things/1");
				Assert.AreEqual(HttpStatusCode.OK,response.StatusCode);
				RecordedRequest recorded=fake.Requests.Single();
				Assert.AreEqual("https://api.vendor.example/v1/things/1",recorded.RequestUri.AbsoluteUri);
				Assert.AreEqual(ProductIdentity.UserAgent,recorded.GetHeader("User-Agent"));
				StringAssert.StartsWith(ProductIdentity.UserAgent,"OpenPMS-Dental/");
				Assert.IsFalse(ProductIdentity.UserAgent.ToLowerInvariant().Contains("open dental"));
			});
		}

		[TestMethod]
		public void Client_RefusesBlockedHostsWithoutSending() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler().SetDefault((r,ct) => Task.FromResult(FakeHttpHandler.MakeResponse(HttpStatusCode.OK)));
				HttpClient httpClient=VendorHttp.CreateClient(MakeOptions(),fake);
				string urlBlocked="https://www."+HqHosts.BlockedDomains[0]+"/api";
				VendorHttpException ex=await AssertEx.ThrowsAsync<VendorHttpException>(() => httpClient.GetAsync(urlBlocked));
				Assert.AreEqual(VendorHttpFailure.UrlRefused,ex.Failure);
				Assert.AreEqual(VendorUrlProblem.BlockedHost,ex.UrlProblem);
				ex=await AssertEx.ThrowsAsync<VendorHttpException>(() => httpClient.GetAsync("http://api.vendor.example/plain"));
				Assert.AreEqual(VendorUrlProblem.NotHttps,ex.UrlProblem);
				Assert.AreEqual(0,fake.Requests.Count,"Nothing may reach the wire.");
			});
		}

		[TestMethod]
		public void Client_BaseAddressOnBlockedHostIsRefused() {
			VendorHttpOptions options=MakeOptions();
			options.BaseAddress=new Uri("https://api."+HqHosts.BlockedDomains[1]+"/");
			AssertEx.Throws<VendorHttpException>(() => VendorHttp.CreateClient(options,new FakeHttpHandler()));
		}

		[TestMethod]
		public void Client_AllowListIsEnforced() {
			AssertEx.Run(async () => {
				VendorHttpOptions options=MakeOptions();
				options.ListAllowedHosts.Add("*.vendor.example");
				FakeHttpHandler fake=new FakeHttpHandler().SetDefault((r,ct) => Task.FromResult(FakeHttpHandler.MakeResponse(HttpStatusCode.OK)));
				HttpClient httpClient=VendorHttp.CreateClient(options,fake);
				await httpClient.GetAsync("https://sandbox.vendor.example/x");
				VendorHttpException ex=await AssertEx.ThrowsAsync<VendorHttpException>(() => httpClient.GetAsync("https://other.example/x"));
				Assert.AreEqual(VendorUrlProblem.HostNotAllowed,ex.UrlProblem);
				Assert.AreEqual(1,fake.Requests.Count);
			});
		}

		[TestMethod]
		public void Client_RedirectsAreCheckedAndCredentialsDropped() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler()
					.Enqueue(HttpStatusCode.Found,"",actionCustomize:x => x.Headers.Location=new Uri("https://cdn.vendor.example/file"))
					.Enqueue(HttpStatusCode.OK,"done");
				HttpClient httpClient=VendorHttp.CreateClient(MakeOptions(),fake);
				HttpRequestMessage request=new HttpRequestMessage(HttpMethod.Get,"files/1");
				request.Headers.Authorization=new AuthenticationHeaderValue("Bearer","secret-token");
				HttpResponseMessage response=await httpClient.SendAsync(request);
				Assert.AreEqual("done",await response.Content.ReadAsStringAsync());
				Assert.AreEqual(2,fake.Requests.Count);
				Assert.IsNotNull(fake.Requests[0].GetHeader("Authorization"));
				Assert.IsNull(fake.Requests[1].GetHeader("Authorization"),"A token must not follow a redirect to another host.");
				//A redirect to a blocked host is refused.
				fake.Enqueue(HttpStatusCode.MovedPermanently,"",actionCustomize:x => x.Headers.Location=new Uri("https://"+HqHosts.BlockedDomains[0]+"/"));
				VendorHttpException ex=await AssertEx.ThrowsAsync<VendorHttpException>(() => httpClient.GetAsync("files/2"));
				Assert.AreEqual(VendorUrlProblem.BlockedHost,ex.UrlProblem);
				Assert.AreEqual(3,fake.Requests.Count);
			});
		}

		private static readonly string[] _arrayCredentialHeaders={ "Authorization","Proxy-Authorization","API-Key","Secret","Subscription-Key",
			"Ocp-Apim-Subscription-Key","X-Auth-Token","Cookie" };

		///<summary>A client whose default headers carry one credential, and a GET that carries the rest of them plus a harmless header.</summary>
		private static async Task<FakeHttpHandler> SendRedirectedGetWithCredentials(string urlFirst,string urlLocation) {
			FakeHttpHandler fake=new FakeHttpHandler()
				.Enqueue(HttpStatusCode.Found,"",actionCustomize:x => x.Headers.Location=new Uri(urlLocation,UriKind.RelativeOrAbsolute))
				.Enqueue(HttpStatusCode.OK,"done");
			HttpClient httpClient=VendorHttp.CreateClient(MakeOptions(),fake);
			httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Ocp-Apim-Subscription-Key","default-subscription-key");
			HttpRequestMessage request=new HttpRequestMessage(HttpMethod.Get,urlFirst);
			request.Headers.Authorization=new AuthenticationHeaderValue("Bearer","secret-token");
			request.Headers.TryAddWithoutValidation("Proxy-Authorization","Basic cHJveHk6cHc=");
			request.Headers.TryAddWithoutValidation("API-Key","api-key-value");
			request.Headers.TryAddWithoutValidation("Secret","secret-value");
			request.Headers.TryAddWithoutValidation("Subscription-Key","subscription-key-value");
			request.Headers.TryAddWithoutValidation("X-Auth-Token","x-auth-value");
			request.Headers.TryAddWithoutValidation("Cookie","session=abc");
			request.Headers.TryAddWithoutValidation("X-Trace","keep-me");
			HttpResponseMessage response=await httpClient.SendAsync(request);
			Assert.AreEqual("done",await response.Content.ReadAsStringAsync());
			Assert.AreEqual(2,fake.Requests.Count);
			foreach(string header in _arrayCredentialHeaders) {
				Assert.IsNotNull(fake.Requests[0].GetHeader(header),header+" should reach the first host.");
			}
			Assert.AreEqual("keep-me",fake.Requests[1].GetHeader("X-Trace"),"Ordinary headers follow every redirect.");
			return fake;
		}

		[TestMethod]
		public void Client_RedirectToAnotherHostDropsEveryCredentialHeader() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=await SendRedirectedGetWithCredentials("https://api.vendor.example/v1/files/1","https://cdn.other.example/file");
				foreach(string header in _arrayCredentialHeaders) {
					Assert.IsNull(fake.Requests[1].GetHeader(header),header+" must not follow a redirect to another host.");
				}
			});
		}

		[TestMethod]
		public void Client_RedirectToAnotherPortOrSchemeDropsEveryCredentialHeader() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=await SendRedirectedGetWithCredentials("https://api.vendor.example/v1/files/1","https://api.vendor.example:8443/file");
				foreach(string header in _arrayCredentialHeaders) {
					Assert.IsNull(fake.Requests[1].GetHeader(header),header+" must not follow a redirect to another port.");
				}
				fake=await SendRedirectedGetWithCredentials("http://localhost:5000/files/1","https://localhost:5000/file");
				foreach(string header in _arrayCredentialHeaders) {
					Assert.IsNull(fake.Requests[1].GetHeader(header),header+" must not follow a redirect to another scheme.");
				}
			});
		}

		[TestMethod]
		public void Client_RedirectOnSameOriginKeepsCredentialHeaders() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=await SendRedirectedGetWithCredentials("https://api.vendor.example/v1/files/1","/v1/files/1/content");
				Assert.AreEqual("https://api.vendor.example/v1/files/1/content",fake.Requests[1].RequestUri.AbsoluteUri);
				foreach(string header in _arrayCredentialHeaders) {
					Assert.AreEqual(fake.Requests[0].GetHeader(header),fake.Requests[1].GetHeader(header),header+" should follow a same-origin redirect.");
				}
			});
		}

		[TestMethod]
		public void Client_RedirectFromRemoteHostToThisComputerIsRefused() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler()
					.Enqueue(HttpStatusCode.Found,"",actionCustomize:x => x.Headers.Location=new Uri("http://127.0.0.1:8080/steal"));
				VendorHttpOptions options=MakeOptions();
				options.ListAllowedHosts.Add("*.vendor.example");
				HttpClient httpClient=VendorHttp.CreateClient(options,fake);
				VendorHttpException ex=await AssertEx.ThrowsAsync<VendorHttpException>(() => httpClient.GetAsync("files/1"));
				Assert.AreEqual(VendorHttpFailure.UrlRefused,ex.Failure);
				Assert.AreEqual(VendorUrlProblem.HostNotAllowed,ex.UrlProblem);
				Assert.AreEqual(1,fake.Requests.Count,"The loopback hop must not be sent.");
			});
		}

		[TestMethod]
		public void Client_PostRedirectIsReturnedNotFollowed() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler()
					.Enqueue(HttpStatusCode.TemporaryRedirect,"",actionCustomize:x => x.Headers.Location=new Uri("https://api.vendor.example/elsewhere"));
				HttpClient httpClient=VendorHttp.CreateClient(MakeOptions(),fake);
				HttpResponseMessage response=await httpClient.PostAsync("pay",new StringContent("{}"));
				Assert.AreEqual(HttpStatusCode.TemporaryRedirect,response.StatusCode);
				Assert.AreEqual(1,fake.Requests.Count);
			});
		}

		[TestMethod]
		public void Client_GetIsRetriedOnTransientFailures() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler()
					.EnqueueNetworkFailure()
					.Enqueue(HttpStatusCode.ServiceUnavailable,"")
					.Enqueue(HttpStatusCode.OK,"third time");
				HttpClient httpClient=VendorHttp.CreateClient(MakeOptions(),fake);
				HttpResponseMessage response=await httpClient.GetAsync("status");
				Assert.AreEqual("third time",await response.Content.ReadAsStringAsync());
				Assert.AreEqual(3,fake.Requests.Count);
			});
		}

		[TestMethod]
		public void Client_GetGivesUpAfterTwoRetries() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler().SetDefault((r,ct) => Task.FromResult(FakeHttpHandler.MakeResponse(HttpStatusCode.BadGateway)));
				HttpClient httpClient=VendorHttp.CreateClient(MakeOptions(),fake);
				HttpResponseMessage response=await httpClient.GetAsync("status");
				Assert.AreEqual(HttpStatusCode.BadGateway,response.StatusCode);
				Assert.AreEqual(3,fake.Requests.Count);
			});
		}

		[TestMethod]
		public void Client_PostIsNeverRetried() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler().EnqueueNetworkFailure();
				HttpClient httpClient=VendorHttp.CreateClient(MakeOptions(),fake);
				await AssertEx.ThrowsAsync<HttpRequestException>(() => httpClient.PostAsync("charge",new StringContent("{\"amount\":1}")));
				Assert.AreEqual(1,fake.Requests.Count,"A payment must never be sent twice by the client.");
				fake.Enqueue(HttpStatusCode.ServiceUnavailable).Enqueue(HttpStatusCode.OK);
				HttpResponseMessage response=await httpClient.PostAsync("charge",new StringContent("{}"));
				Assert.AreEqual(HttpStatusCode.ServiceUnavailable,response.StatusCode);
				Assert.AreEqual(2,fake.Requests.Count);
			});
		}

		[TestMethod]
		public void Client_NoRetryRequestIsSentOnce() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler().Enqueue(HttpStatusCode.ServiceUnavailable).Enqueue(HttpStatusCode.OK);
				HttpClient httpClient=VendorHttp.CreateClient(MakeOptions(),fake);
				HttpRequestMessage request=new HttpRequestMessage(HttpMethod.Get,"interactive");
				VendorHttp.SetNoRetry(request);
				HttpResponseMessage response=await httpClient.SendAsync(request);
				Assert.AreEqual(HttpStatusCode.ServiceUnavailable,response.StatusCode);
				Assert.AreEqual(1,fake.Requests.Count);
			});
		}

		[TestMethod]
		public void Client_RetryAfterLongerThanCapIsReturned() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler()
					.Enqueue(HttpStatusCode.ServiceUnavailable,"",actionCustomize:x => x.Headers.RetryAfter=new RetryConditionHeaderValue(TimeSpan.FromMinutes(5)))
					.Enqueue(HttpStatusCode.OK);
				HttpClient httpClient=VendorHttp.CreateClient(MakeOptions(),fake);
				HttpResponseMessage response=await httpClient.GetAsync("busy");
				Assert.AreEqual(HttpStatusCode.ServiceUnavailable,response.StatusCode,"Waiting 5 minutes is not allowed; the caller decides.");
				Assert.AreEqual(1,fake.Requests.Count);
				//A short Retry-After is honoured.
				fake=new FakeHttpHandler()
					.Enqueue((HttpStatusCode)429,"",actionCustomize:x => x.Headers.RetryAfter=new RetryConditionHeaderValue(TimeSpan.FromMilliseconds(1)))
					.Enqueue(HttpStatusCode.OK);
				httpClient=VendorHttp.CreateClient(MakeOptions(),fake);
				response=await httpClient.GetAsync("busy");
				Assert.AreEqual(HttpStatusCode.OK,response.StatusCode);
			});
		}

		[TestMethod]
		public void Client_TimeoutPerRequest() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler().SetDefault(async (r,ct) => {
					await Task.Delay(TimeSpan.FromSeconds(10),ct);
					return FakeHttpHandler.MakeResponse(HttpStatusCode.OK);
				});
				VendorHttpOptions options=MakeOptions();
				options.MaxGetRetries=0;
				HttpClient httpClient=VendorHttp.CreateClient(options,fake);
				HttpRequestMessage request=new HttpRequestMessage(HttpMethod.Get,"slow");
				VendorHttp.SetTimeout(request,TimeSpan.FromMilliseconds(100));
				VendorHttpException ex=await AssertEx.ThrowsAsync<VendorHttpException>(() => httpClient.SendAsync(request));
				Assert.AreEqual(VendorHttpFailure.Timeout,ex.Failure);
				//The caller's own cancellation is not turned into a timeout.
				using(CancellationTokenSource cts=new CancellationTokenSource(TimeSpan.FromMilliseconds(100))) {
					await AssertEx.ThrowsAsync<OperationCanceledException>(() => httpClient.GetAsync("slow",cts.Token));
				}
			});
		}

		[TestMethod]
		public void Client_CircuitBreakerFailsFastThenRecovers() {
			AssertEx.Run(async () => {
				DateTime nowUtc=new DateTime(2026,10,19,12,0,0,DateTimeKind.Utc);
				VendorHttpOptions options=MakeOptions();
				options.UseCircuitBreaker=true;
				options.MaxGetRetries=0;
				options.GetUtcNow=() => nowUtc;
				FakeHttpHandler fake=new FakeHttpHandler().EnqueueNetworkFailure().Enqueue(HttpStatusCode.OK);
				HttpClient httpClient=VendorHttp.CreateClient(options,fake);
				await AssertEx.ThrowsAsync<HttpRequestException>(() => httpClient.GetAsync("a"));
				VendorHttpException ex=await AssertEx.ThrowsAsync<VendorHttpException>(() => httpClient.GetAsync("b"));
				Assert.AreEqual(VendorHttpFailure.CircuitOpen,ex.Failure);
				Assert.AreEqual(1,fake.Requests.Count,"The open circuit sends nothing.");
				nowUtc=nowUtc.AddSeconds(31);
				HttpResponseMessage response=await httpClient.GetAsync("c");
				Assert.AreEqual(HttpStatusCode.OK,response.StatusCode);
			});
		}

		[TestMethod]
		public void Client_LogLinesAreRedacted() {
			AssertEx.Run(async () => {
				List<string> listLog=new List<string>();
				FakeHttpHandler fake=new FakeHttpHandler().Enqueue(HttpStatusCode.OK,"{\"token\":\"t\"}",
					actionCustomize:x => x.Headers.Add("x-request-id","req-42"));
				HttpClient httpClient=VendorHttp.CreateClient(MakeOptions(listLog),fake);
				HttpRequestMessage request=new HttpRequestMessage(HttpMethod.Get,"addresses?streetAddress=123+Main+St&ZIPCode=98270");
				request.Headers.Authorization=new AuthenticationHeaderValue("Bearer","secret-token");
				request.Headers.Add("API-Key","key-123");
				await httpClient.SendAsync(request);
				request=new HttpRequestMessage(HttpMethod.Get,"addresses?streetAddress=456+Oak");
				VendorHttp.SetEndpointName(request,"address");
				fake.Enqueue(HttpStatusCode.OK);
				await httpClient.SendAsync(request);
				Assert.AreEqual(2,listLog.Count);
				string allLines=string.Join("\n",listLog);
				StringAssert.Contains(listLog[0],"TestVendor GET api.vendor.example: 200 request req-42");
				StringAssert.Contains(listLog[1],"TestVendor address: 200");
				foreach(string forbidden in new[] { "Main","98270","Oak","secret-token","key-123","token","/v1/" }) {
					Assert.IsFalse(allLines.Contains(forbidden),"Log leaked: "+forbidden);
				}
			});
		}

		[TestMethod]
		public void SensitiveHeaders() {
			foreach(string name in new[] { "Authorization","API-Key","Secret","Subscription-Key","X-Auth-Token","x-auth-key","cookie" }) {
				Assert.IsTrue(VendorHttp.IsSensitiveHeader(name),name);
			}
			Assert.IsFalse(VendorHttp.IsSensitiveHeader("Content-Type"));
			Assert.IsFalse(VendorHttp.IsSensitiveHeader("x-request-id"));
		}

		[TestMethod]
		public void GetClient_IsSharedPerVendorAndBase() {
			VendorHttpOptions options=MakeOptions();
			options.VendorName="SharedClientTest";
			HttpClient client1=VendorHttp.GetClient(options);
			HttpClient client2=VendorHttp.GetClient(options);
			Assert.AreSame(client1,client2);
			VendorHttpOptions optionsOther=MakeOptions();
			optionsOther.VendorName="SharedClientTest";
			optionsOther.BaseAddress=new Uri("https://sandbox.vendor.example/");
			Assert.AreNotSame(client1,VendorHttp.GetClient(optionsOther));
		}
	}
}
