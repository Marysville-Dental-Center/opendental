// Tests for the PostcardMania (PCM Integrations) DirectMail API v3 client against a fake HTTP handler: login and token reuse, 401, 429, errors.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using CodeBase.VendorDirect;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using OpenDentBusiness.PostcardMania;

namespace VendorDirectTests.PostcardMania {
	[TestClass]
	public class PostcardManiaApiClientTests {
		private const string BALANCE_JSON="{\"moneyOnAccount\":42.5}";

		[TestMethod]
		public void TestConnection_LogsInWithTheOfficesKeyThenReadsTheBalance() {
			PostcardManiaFixture fixture=new PostcardManiaFixture();
			fixture.EnqueueLogin();
			fixture.Handler.Enqueue(HttpStatusCode.OK,BALANCE_JSON);
			PcmConnectionResult result=fixture.Client.TestConnection(PostcardManiaFixture.MakeAccount(childRefNbr:"clinic-3"));
			Assert.AreEqual(42.5m,result.MoneyOnAccount);
			Assert.AreEqual(PostcardManiaProgram.EnvironmentSandbox,result.Environment);
			List<RecordedRequest> listRequests=fixture.Handler.Requests;
			Assert.AreEqual(2,listRequests.Count);
			Assert.AreEqual("https://v3.pcmintegrations.com/auth/login",listRequests[0].RequestUri.AbsoluteUri);
			JObject jObjectLogin=JObject.Parse(listRequests[0].Body);
			Assert.AreEqual("key-1",(string)jObjectLogin["apiKey"]);
			Assert.AreEqual("secret-1",(string)jObjectLogin["apiSecret"]);
			Assert.AreEqual("clinic-3",(string)jObjectLogin["childRefNbr"]);
			Assert.IsNull(listRequests[0].GetHeader("Authorization"),"The login itself carries no token.");
			Assert.AreEqual("https://v3.pcmintegrations.com/integration/balance",listRequests[1].RequestUri.AbsoluteUri);
			Assert.AreEqual("Bearer tok-1",listRequests[1].GetHeader("Authorization"));
		}

		[TestMethod]
		public void Token_IsReusedUntilSixtySecondsBeforeExpiryThenRenewed() {
			PostcardManiaFixture fixture=new PostcardManiaFixture();
			PostcardManiaAccount account=PostcardManiaFixture.MakeAccount();
			fixture.EnqueueLogin("tok-1");
			fixture.Handler.SetDefault((request,ct) => Task.FromResult(FakeHttpHandler.MakeResponse(HttpStatusCode.OK,"[]")));
			fixture.Client.GetOrders(account,null,1,10);
			fixture.UtcNow=fixture.UtcNow.AddMinutes(13);//2 minutes left: still reused.
			fixture.Client.GetOrders(account,null,1,10);
			Assert.AreEqual(1,fixture.RequestsTo("POST auth/login").Count);
			fixture.UtcNow=fixture.UtcNow.AddSeconds(61);//59 seconds left: renewed.
			fixture.Handler.Enqueue(HttpStatusCode.OK,fixture.LoginJson("tok-2"));
			fixture.Client.GetOrders(account,null,1,10);
			Assert.AreEqual(2,fixture.RequestsTo("POST auth/login").Count);
			Assert.AreEqual("Bearer tok-2",fixture.RequestsTo("GET order").Last().GetHeader("Authorization"));
		}

		[TestMethod]
		public void Token_IsPerKeyChildAndSecret() {
			PostcardManiaFixture fixture=new PostcardManiaFixture();
			fixture.Handler.SetDefault((request,ct) => Task.FromResult(request.RequestUri.AbsolutePath=="/auth/login"
				? FakeHttpHandler.MakeResponse(HttpStatusCode.OK,fixture.LoginJson("tok-"+fixture.Handler.Requests.Count))
				: FakeHttpHandler.MakeResponse(HttpStatusCode.OK,"[]")));
			fixture.Client.GetOrders(PostcardManiaFixture.MakeAccount(),null,1,10);
			fixture.Client.GetOrders(PostcardManiaFixture.MakeAccount(childRefNbr:"clinic-2"),null,1,10);
			fixture.Client.GetOrders(PostcardManiaFixture.MakeAccount(apiSecret:"secret-new"),null,1,10);
			fixture.Client.GetOrders(PostcardManiaFixture.MakeAccount(),null,1,10);
			Assert.AreEqual(3,fixture.RequestsTo("POST auth/login").Count,"A changed child reference or secret logs in again; the same account reuses its token.");
		}

		[TestMethod]
		public void Unauthorized_LogsInOnceMoreAndRetriesOnce() {
			PostcardManiaFixture fixture=new PostcardManiaFixture();
			PostcardManiaAccount account=PostcardManiaFixture.MakeAccount();
			fixture.EnqueueLogin("tok-1");
			fixture.Handler.Enqueue(HttpStatusCode.Unauthorized,"");
			fixture.EnqueueLogin("tok-2");
			fixture.Handler.Enqueue(HttpStatusCode.OK,"{\"batchID\":3,\"status\":\"Pending Payment\"}");
			PcmBatch batch=fixture.Client.GetBatch(account,3);
			Assert.IsTrue(batch.NeedsPayment);
			Assert.AreEqual(2,fixture.RequestsTo("POST auth/login").Count);
			List<RecordedRequest> listBatchRequests=fixture.RequestsTo("GET batch/3");
			Assert.AreEqual(2,listBatchRequests.Count);
			Assert.AreEqual("Bearer tok-2",listBatchRequests[1].GetHeader("Authorization"));
		}

		[TestMethod]
		public void Unauthorized_TwiceIsReportedAsRejectedCredentials() {
			PostcardManiaFixture fixture=new PostcardManiaFixture();
			fixture.EnqueueLogin("tok-1");
			fixture.Handler.Enqueue(HttpStatusCode.Unauthorized,"");
			fixture.EnqueueLogin("tok-2");
			fixture.Handler.Enqueue(HttpStatusCode.Unauthorized,"");
			PostcardManiaException ex=AssertThrows(() => fixture.Client.GetBatch(PostcardManiaFixture.MakeAccount(),3));
			Assert.AreEqual(PostcardManiaErrorKind.CredentialsRejected,ex.Kind);
			Assert.AreEqual(4,fixture.Handler.Requests.Count,"One re-login and one retry, no more.");
		}

		[TestMethod]
		public void LoginRejected_IsRememberedUntilTestConnection() {
			PostcardManiaFixture fixture=new PostcardManiaFixture();
			PostcardManiaAccount account=PostcardManiaFixture.MakeAccount();
			fixture.Handler.Enqueue(HttpStatusCode.Unauthorized,"{\"error\":{\"code\":401,\"message\":\"Invalid credentials\"}}");
			PostcardManiaException ex=AssertThrows(() => fixture.Client.GetOrders(account,null,1,10));
			Assert.AreEqual(PostcardManiaErrorKind.CredentialsRejected,ex.Kind);
			StringAssert.Contains(ex.Message,"was rejected");
			Assert.IsFalse(ex.Message.Contains("secret-1"),"Never the secret in a message.");
			ex=AssertThrows(() => fixture.Client.GetOrders(account,null,1,10));
			Assert.AreEqual(PostcardManiaErrorKind.CredentialsRejected,ex.Kind);
			Assert.AreEqual(1,fixture.Handler.Requests.Count,"A rejected key isn't sent again in this session.");
			fixture.Handler.Enqueue(HttpStatusCode.NotFound,"");//404 on login also means a bad key.
			ex=AssertThrows(() => fixture.Client.TestConnection(account));
			Assert.AreEqual(PostcardManiaErrorKind.CredentialsRejected,ex.Kind);
			Assert.AreEqual(2,fixture.Handler.Requests.Count,"Test connection always tries again.");
		}

		[TestMethod]
		public void RateLimited_PostWaitsForRetryAfterAndStopsAfterThreeTries() {
			PostcardManiaFixture fixture=new PostcardManiaFixture();
			PostcardManiaAccount account=PostcardManiaFixture.MakeAccount();
			fixture.EnqueueLogin();
			for(int i=0;i<3;i++) {
				fixture.Handler.Enqueue((HttpStatusCode)429,"",actionCustomize:x => x.Headers.RetryAfter=new RetryConditionHeaderValue(TimeSpan.FromSeconds(2)));
			}
			PostcardManiaException ex=AssertThrows(() => fixture.Client.EstimatePostcardOrder(account,PostcardManiaFixture.MakeOrder("r")));
			Assert.AreEqual(PostcardManiaErrorKind.RateLimited,ex.Kind);
			Assert.AreEqual(2,ex.RetryAfterSeconds);
			Assert.AreEqual(3,fixture.RequestsTo("POST order/postcard/pricing").Count);
			Assert.AreEqual(2,fixture.ListDelays.Count);
			Assert.IsTrue(fixture.ListDelays.All(x => x>=TimeSpan.FromSeconds(2) && x<TimeSpan.FromSeconds(3)),"Retry-After plus a little jitter.");
		}

		[TestMethod]
		public void RateLimited_ThenSuccessReturnsTheAnswer() {
			PostcardManiaFixture fixture=new PostcardManiaFixture();
			fixture.EnqueueLogin();
			fixture.Handler.Enqueue((HttpStatusCode)429,"",actionCustomize:x => x.Headers.RetryAfter=new RetryConditionHeaderValue(TimeSpan.FromSeconds(1)));
			fixture.Handler.Enqueue(HttpStatusCode.OK,"{\"estimatedTotalPrice\":1.30,\"lineItems\":[{\"name\":\"Postcard\",\"price\":0.65,\"quantity\":2,\"lineTotal\":1.30}]}");
			PcmPricingResponse response=fixture.Client.EstimatePostcardOrder(PostcardManiaFixture.MakeAccount(),PostcardManiaFixture.MakeOrder("r"));
			Assert.AreEqual(1.30m,response.EstimatedTotalPrice);
			Assert.AreEqual(2,response.LineItems[0].Quantity);
		}

		[TestMethod]
		public void RateLimited_TooLongRetryAfterIsNotWaitedFor() {
			PostcardManiaFixture fixture=new PostcardManiaFixture();
			fixture.EnqueueLogin();
			fixture.Handler.Enqueue((HttpStatusCode)429,"",actionCustomize:x => x.Headers.RetryAfter=new RetryConditionHeaderValue(TimeSpan.FromSeconds(120)));
			PostcardManiaException ex=AssertThrows(() => fixture.Client.PlacePostcardOrder(PostcardManiaFixture.MakeAccount(),PostcardManiaFixture.MakeOrder("r")));
			Assert.AreEqual(PostcardManiaErrorKind.RateLimited,ex.Kind);
			Assert.AreEqual(120,ex.RetryAfterSeconds);
			Assert.AreEqual(0,fixture.ListDelays.Count);
			Assert.AreEqual(1,fixture.RequestsTo("POST order/postcard").Count);
		}

		[TestMethod]
		public void RateLimited_GetIsRetriedByTheHttpPolicy() {
			PostcardManiaFixture fixture=new PostcardManiaFixture();
			fixture.EnqueueLogin();
			fixture.Handler.Enqueue((HttpStatusCode)429,"",actionCustomize:x => x.Headers.RetryAfter=new RetryConditionHeaderValue(TimeSpan.Zero));
			fixture.Handler.Enqueue(HttpStatusCode.OK,"{\"results\":[{\"designID\":1,\"friendlyName\":\"A\"}]}");
			PcmPagedResult<PcmDesign> paged=fixture.Client.GetDesigns(PostcardManiaFixture.MakeAccount(),"postcard",1,50);
			Assert.AreEqual(1,paged.Results.Count);
			List<RecordedRequest> listRequests=fixture.RequestsTo("GET design");
			Assert.AreEqual(2,listRequests.Count);
			Assert.AreEqual("?productType=postcard&page=1&perPage=50",listRequests[0].RequestUri.Query);
		}

		[TestMethod]
		public void ValidationError_CarriesTheVendorMessages() {
			PostcardManiaFixture fixture=new PostcardManiaFixture();
			fixture.EnqueueLogin();
			fixture.Handler.Enqueue((HttpStatusCode)422,"{\"error\":{\"code\":422,\"message\":\"One or more errors occurred\",\"data\":"
				+"[{\"message\":\"Address could not be verified\",\"property\":\"recipients[1]\"}]}}");
			PostcardManiaException ex=AssertThrows(() => fixture.Client.PlacePostcardOrder(PostcardManiaFixture.MakeAccount(),PostcardManiaFixture.MakeOrder("r")));
			Assert.AreEqual(PostcardManiaErrorKind.Validation,ex.Kind);
			CollectionAssert.AreEqual(new[] { "recipients[1]: Address could not be verified" },ex.Messages);
			StringAssert.Contains(ex.Message,"Check addresses");
		}

		[TestMethod]
		public void PlaceOrder_IsNeverRetriedAfterATimeoutOrServerError() {
			PostcardManiaFixture fixture=new PostcardManiaFixture();
			PostcardManiaAccount account=PostcardManiaFixture.MakeAccount();
			fixture.EnqueueLogin();
			fixture.Handler.Enqueue((request,ct) => throw new TaskCanceledException("Simulated timeout."));
			PostcardManiaException ex=AssertThrows(() => fixture.Client.PlacePostcardOrder(account,PostcardManiaFixture.MakeOrder("r")));
			Assert.AreEqual(PostcardManiaErrorKind.Timeout,ex.Kind);
			Assert.IsTrue(ex.IsOutcomeUnknown);
			fixture.Handler.Enqueue(HttpStatusCode.BadGateway,"");
			ex=AssertThrows(() => fixture.Client.PlacePostcardOrder(account,PostcardManiaFixture.MakeOrder("r")));
			Assert.AreEqual(PostcardManiaErrorKind.Server,ex.Kind);
			Assert.AreEqual(2,fixture.RequestsTo("POST order/postcard").Count,"One send per call.");
			Assert.AreEqual(0,fixture.ListDelays.Count);
		}

		[TestMethod]
		public void PlaceOrder_RequiresAReference() {
			PostcardManiaFixture fixture=new PostcardManiaFixture();
			AssertEx.Throws<ArgumentException>(() => { fixture.Client.PlacePostcardOrder(PostcardManiaFixture.MakeAccount(),PostcardManiaFixture.MakeOrder("")); });
			Assert.AreEqual(0,fixture.Handler.Requests.Count);
		}

		[TestMethod]
		public void NetworkFailure_IsReportedAsUnreachable() {
			PostcardManiaFixture fixture=new PostcardManiaFixture();
			fixture.Handler.EnqueueNetworkFailure();
			PostcardManiaException ex=AssertThrows(() => fixture.Client.TestConnection(PostcardManiaFixture.MakeAccount()));
			Assert.AreEqual(PostcardManiaErrorKind.Network,ex.Kind);
			StringAssert.Contains(ex.Message,"v3.pcmintegrations.com");
		}

		[TestMethod]
		public void NotConfigured_SendsNothing() {
			PostcardManiaFixture fixture=new PostcardManiaFixture();
			PostcardManiaException ex=AssertThrows(() => fixture.Client.TestConnection(PostcardManiaFixture.MakeAccount(apiSecret:"")));
			Assert.AreEqual(PostcardManiaErrorKind.NotConfigured,ex.Kind);
			PostcardManiaAccount account=PostcardManiaFixture.MakeAccount(apiSecret:"");
			account.IsSecretUnreadable=true;
			ex=AssertThrows(() => fixture.Client.GetOrders(account,null,1,10));
			StringAssert.Contains(ex.Message,"could not be read");
			Assert.AreEqual(0,fixture.Handler.Requests.Count);
		}

		[TestMethod]
		public void CancelOrder_DeletesAndReportsACancelTooLate() {
			PostcardManiaFixture fixture=new PostcardManiaFixture();
			PostcardManiaAccount account=PostcardManiaFixture.MakeAccount();
			fixture.EnqueueLogin();
			fixture.Handler.Enqueue(HttpStatusCode.NoContent,"");
			fixture.Client.CancelOrder(account,5001);
			Assert.AreEqual(1,fixture.RequestsTo("DELETE order/5001").Count);
			fixture.Handler.Enqueue(HttpStatusCode.BadRequest,"{\"error\":{\"code\":400,\"message\":\"One or more errors occurred\",\"data\":[{\"message\":\"Order can no longer be canceled\"}]}}");
			PostcardManiaException ex=AssertThrows(() => fixture.Client.CancelOrder(account,5002));
			Assert.AreEqual(PostcardManiaErrorKind.Validation,ex.Kind);
			CollectionAssert.AreEqual(new[] { "Order can no longer be canceled" },ex.Messages);
		}

		[TestMethod]
		public void DesignEditor_ReturnsTheTokenUrlForTheEmbeddedWindow() {
			PostcardManiaFixture fixture=new PostcardManiaFixture();
			PostcardManiaAccount account=PostcardManiaFixture.MakeAccount();
			fixture.EnqueueLogin();
			fixture.Handler.Enqueue(HttpStatusCode.OK,"{\"designID\":91,\"url\":\"https://portal.pcmintegrations.com/integrated/embed/editor/91?token=abc\"}");
			PcmDesignEditorResponse response=fixture.Client.GetDesignEditUrl(account,90,duplicate:true);
			Assert.AreEqual(91,response.DesignID,"Editing a template makes a copy with its own ID.");
			Assert.AreEqual("?mode=embed&duplicate=true",fixture.RequestsTo("GET design/90/edit")[0].RequestUri.Query);
			fixture.Handler.Enqueue(HttpStatusCode.Created,"{\"designID\":92,\"url\":\"https://portal.pcmintegrations.com/integrated/embed/editor/92?token=def\"}");
			response=fixture.Client.CreateDesign(account,"Spring","57");
			Assert.AreEqual(92,response.DesignID);
			JObject jObject=JObject.Parse(fixture.RequestsTo("POST design/custom")[0].Body);
			Assert.AreEqual("Spring",(string)jObject["name"]);
			Assert.AreEqual("57",(string)jObject["size"]);
		}

		[TestMethod]
		public void ProofImage_IsFetchedWithoutCredentialsAndOnlyOverHttps() {
			PostcardManiaFixture fixture=new PostcardManiaFixture();
			fixture.Handler.Enqueue((request,ct) => Task.FromResult(MakeImageResponse()));
			byte[] arrayBytes=fixture.Client.GetProofImage("https://proofs.example.test/front.png");
			CollectionAssert.AreEqual(new byte[] { 1,2,3 },arrayBytes);
			Assert.IsNull(fixture.Handler.Requests[0].GetHeader("Authorization"));
			Assert.IsNull(fixture.Client.GetProofImage("http://proofs.example.test/front.png"));
			Assert.IsNull(fixture.Client.GetProofImage("https://www."+HqHosts.BlockedDomains[0]+"/front.png"),"Never an Open Dental host.");
			Assert.IsNull(fixture.Client.GetProofImage(""));
			Assert.AreEqual(1,fixture.Handler.Requests.Count);
		}

		[TestMethod]
		public void BaseUrl_NeverAnOpenDentalHost() {
			AssertEx.Throws<VendorHttpException>(() => { new PostcardManiaApiClient(new FakeHttpHandler(),"https://api."+HqHosts.BlockedDomains[0]+"/"); });
			AssertEx.Throws<VendorHttpException>(() => { new PostcardManiaApiClient(new FakeHttpHandler(),"http://v3.pcmintegrations.com/"); });
		}

		private static HttpResponseMessage MakeImageResponse() {
			HttpResponseMessage response=new HttpResponseMessage(HttpStatusCode.OK);
			response.Content=new ByteArrayContent(new byte[] { 1,2,3 });
			response.Content.Headers.ContentType=new MediaTypeHeaderValue("image/png");
			return response;
		}

		internal static PostcardManiaException AssertThrows(Action action) {
			try {
				action();
			}
			catch(PostcardManiaException ex) {
				return ex;
			}
			Assert.Fail("Expected a PostcardManiaException.");
			return null;
		}
	}
}
