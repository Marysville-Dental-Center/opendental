// Tests for the CareCredit HTTP client against a fake handler: token grant, bearer token reuse, the 401 retry rule, error mapping, the URL
// policy and no automatic retry of POSTs. Never touches the network.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Linq;
using System.Net;
using System.Text;
using CodeBase.VendorDirect;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using OpenDentBusiness;
using OpenDentBusiness.Bridges;

namespace VendorDirectTests.CareCreditTests {
	[TestClass]
	public class CareCreditApiClientTests {
		private const string TOKEN_JSON="{\"access_token\":\"tok-1\",\"token_type\":\"bearer\",\"expires_in\":3600}";
		private const string TOKEN_JSON_2="{\"access_token\":\"tok-2\",\"token_type\":\"bearer\",\"expires_in\":3600}";
		private const string PREFILL_JSON="{\"referenceId\":\"REF-9\",\"url\":\"https://pages.carecredit-sandbox.test/landing?sid=S-77\"}";

		[TestInitialize]
		public void TestInitialize() {
			TokenCache.ClearAll();
		}

		private static CareCreditPrefillRequest CreatePrefillRequest() {
			CareCreditPrefillRequest request=new CareCreditPrefillRequest();
			request.LandingPage=CareCreditLandingPage.PR;
			request.MerchantNumber=CareCreditTestData.MERCHANT;
			request.PartnerCode="OFFICE42";
			request.Consumer=CareCreditConsumer.FromPatient(CareCreditTestData.CreateAdult(5),includeDateOfBirth:false,includeSsnLast4:false);
			request.PurchaseAmount=125.5;
			request.InvoiceNumber="77";
			return request;
		}

		[TestMethod]
		public void Token_BasicHeaderByDefault() {
			FakeHttpHandler handler=new FakeHttpHandler().Enqueue(HttpStatusCode.OK,TOKEN_JSON);
			CareCreditApiClient client=new CareCreditApiClient(handler);
			OAuthAccessToken token=VendorHttp.RunSync(() => client.RequestTokenAsync(CareCreditTestData.CreateConfig()));
			Assert.AreEqual("tok-1",token.Value);
			RecordedRequest request=handler.Requests.Single();
			Assert.AreEqual("https://api.carecredit-sandbox.test/v1/oauth2/token",request.RequestUri.AbsoluteUri);
			Assert.AreEqual(HttpMethodPost(),request.Method.Method);
			string expected="Basic "+Convert.ToBase64String(Encoding.UTF8.GetBytes("office-client-id:office-client-secret"));
			Assert.AreEqual(expected,request.GetHeader("Authorization"));
			StringAssert.Contains(request.Body,"grant_type=client_credentials");
			Assert.IsFalse(request.Body.Contains("client_secret"));
		}

		private static string HttpMethodPost() {
			return "POST";
		}

		[TestMethod]
		public void Token_FormBodyWhenConfigured() {
			FakeHttpHandler handler=new FakeHttpHandler().Enqueue(HttpStatusCode.OK,TOKEN_JSON);
			CareCreditConfig config=CareCreditTestData.CreateConfig();
			config.ClientAuth=CareCreditClientAuth.FormBody;
			config.Scope="patient-data";
			CareCreditApiClient client=new CareCreditApiClient(handler);
			VendorHttp.RunSync(() => client.RequestTokenAsync(config));
			RecordedRequest request=handler.Requests.Single();
			Assert.IsNull(request.GetHeader("Authorization"));
			StringAssert.Contains(request.Body,"client_id=office-client-id");
			StringAssert.Contains(request.Body,"client_secret=office-client-secret");
			StringAssert.Contains(request.Body,"scope=patient-data");
		}

		[TestMethod]
		public void Prefill_SendsBearerTokenAndReadsReferenceAndPage() {
			FakeHttpHandler handler=new FakeHttpHandler()
				.Enqueue(HttpStatusCode.OK,TOKEN_JSON)
				.Enqueue(HttpStatusCode.OK,PREFILL_JSON);
			CareCreditApiClient client=new CareCreditApiClient(handler);
			CareCreditPrefillResult result=VendorHttp.RunSync(() => client.PrefillAsync(CareCreditTestData.CreateConfig(),CreatePrefillRequest()));
			Assert.AreEqual("REF-9",result.ReferenceId);
			Assert.AreEqual("https://pages.carecredit-sandbox.test/landing?sid=S-77",result.PageUrl);
			Assert.AreEqual("S-77",result.SessionId,"Read from the configured session parameter.");
			RecordedRequest request=handler.Requests[1];
			Assert.AreEqual("https://api.carecredit-sandbox.test/v1/patient-data/prefill",request.RequestUri.AbsoluteUri);
			Assert.AreEqual("Bearer tok-1",request.GetHeader("Authorization"));
			JObject body=JObject.Parse(request.Body);
			Assert.AreEqual("OFFICE42",(string)body["partnerCode"]);
			Assert.AreEqual(CareCreditTestData.MERCHANT,(string)body["merchantNumber"]);
			Assert.AreEqual("PR",(string)body["landingPage"]);
			Assert.AreEqual("125.50",(string)body["purchaseAmount"]);
			Assert.AreEqual("Ada",(string)body["consumer"]["firstName"]);
			Assert.IsNull(body["consumer"]["ssnLast4"],"No SSN digits on a prefill.");
			Assert.IsNull(body["consumer"]["dateOfBirth"],"No birth date except for an application.");
		}

		[TestMethod]
		public void Token_IsReusedAcrossCalls() {
			FakeHttpHandler handler=new FakeHttpHandler()
				.Enqueue(HttpStatusCode.OK,TOKEN_JSON)
				.Enqueue(HttpStatusCode.OK,PREFILL_JSON)
				.Enqueue(HttpStatusCode.OK,PREFILL_JSON);
			CareCreditApiClient client=new CareCreditApiClient(handler);
			CareCreditConfig config=CareCreditTestData.CreateConfig();
			VendorHttp.RunSync(() => client.PrefillAsync(config,CreatePrefillRequest()));
			VendorHttp.RunSync(() => client.PrefillAsync(config,CreatePrefillRequest()));
			Assert.AreEqual(1,handler.Requests.Count(x => x.RequestUri.AbsolutePath.EndsWith("/oauth2/token")));
			Assert.AreEqual(3,handler.Requests.Count);
		}

		[TestMethod]
		public void Unauthorized_RefreshesOnceAndRetriesOnce() {
			FakeHttpHandler handler=new FakeHttpHandler()
				.Enqueue(HttpStatusCode.OK,TOKEN_JSON)
				.Enqueue(HttpStatusCode.Unauthorized,"")
				.Enqueue(HttpStatusCode.OK,TOKEN_JSON_2)
				.Enqueue(HttpStatusCode.OK,PREFILL_JSON);
			CareCreditApiClient client=new CareCreditApiClient(handler);
			CareCreditPrefillResult result=VendorHttp.RunSync(() => client.PrefillAsync(CareCreditTestData.CreateConfig(),CreatePrefillRequest()));
			Assert.AreEqual("REF-9",result.ReferenceId);
			Assert.AreEqual(4,handler.Requests.Count);
			Assert.AreEqual("Bearer tok-2",handler.Requests[3].GetHeader("Authorization"));
		}

		[TestMethod]
		public void Unauthorized_TwiceIsAnAuthFailure() {
			FakeHttpHandler handler=new FakeHttpHandler()
				.Enqueue(HttpStatusCode.OK,TOKEN_JSON)
				.Enqueue(HttpStatusCode.Unauthorized,"")
				.Enqueue(HttpStatusCode.OK,TOKEN_JSON_2)
				.Enqueue(HttpStatusCode.Unauthorized,"");
			CareCreditApiClient client=new CareCreditApiClient(handler);
			CareCredit.CareCreditException ex=AssertEx.Throws<CareCredit.CareCreditException>(
				() => VendorHttp.RunSync(() => client.PrefillAsync(CareCreditTestData.CreateConfig(),CreatePrefillRequest())));
			Assert.AreEqual(401,ex.ErrorCode);
			Assert.IsTrue(ex.IsAuthFailure);
			Assert.AreEqual(4,handler.Requests.Count,"One refresh and one retry, no more.");
		}

		[TestMethod]
		public void TokenRefused_IsAnAuthFailureAndFailsFastAfterwards() {
			FakeHttpHandler handler=new FakeHttpHandler()
				.Enqueue(HttpStatusCode.BadRequest,"{\"error\":\"invalid_client\"}");
			CareCreditApiClient client=new CareCreditApiClient(handler);
			CareCreditConfig config=CareCreditTestData.CreateConfig();
			CareCredit.CareCreditException ex=AssertEx.Throws<CareCredit.CareCreditException>(
				() => VendorHttp.RunSync(() => client.PrefillAsync(config,CreatePrefillRequest())));
			Assert.IsTrue(ex.IsAuthFailure);
			StringAssert.Contains(ex.Message,"invalid_client");
			AssertEx.Throws<CareCredit.CareCreditException>(() => VendorHttp.RunSync(() => client.PrefillAsync(config,CreatePrefillRequest())));
			Assert.AreEqual(1,handler.Requests.Count,"After a rejection the same configuration makes no further calls this session.");
		}

		[TestMethod]
		public void HttpError_KeepsStatusAndVendorCodeButNotTheBody() {
			FakeHttpHandler handler=new FakeHttpHandler()
				.Enqueue(HttpStatusCode.OK,TOKEN_JSON)
				.Enqueue((HttpStatusCode)422,"{\"errorCode\":\"TRAINING_SUSPENDED\",\"consumer\":{\"ssn\":\"123456789\"}}");
			CareCreditApiClient client=new CareCreditApiClient(handler);
			CareCredit.CareCreditException ex=AssertEx.Throws<CareCredit.CareCreditException>(
				() => VendorHttp.RunSync(() => client.PrefillAsync(CareCreditTestData.CreateConfig(),CreatePrefillRequest())));
			Assert.AreEqual(422,ex.ErrorCode);
			StringAssert.Contains(ex.Message,"TRAINING_SUSPENDED");
			Assert.IsFalse(ex.Message.Contains("123456789"));
			Assert.IsFalse(ex.ResponseJson.Contains("123456789"),"The stored error answer is scrubbed.");
		}

		[TestMethod]
		public void Prefill_IsNeverRetriedAutomatically() {
			FakeHttpHandler handler=new FakeHttpHandler()
				.Enqueue(HttpStatusCode.OK,TOKEN_JSON)
				.Enqueue(HttpStatusCode.ServiceUnavailable,"")
				.Enqueue(HttpStatusCode.OK,PREFILL_JSON);
			CareCreditApiClient client=new CareCreditApiClient(handler);
			CareCredit.CareCreditException ex=AssertEx.Throws<CareCredit.CareCreditException>(
				() => VendorHttp.RunSync(() => client.PrefillAsync(CareCreditTestData.CreateConfig(),CreatePrefillRequest())));
			Assert.AreEqual(503,ex.ErrorCode);
			Assert.AreEqual(2,handler.Requests.Count);
		}

		[TestMethod]
		public void Pullback_GetWithEscapedReferenceInThePath() {
			FakeHttpHandler handler=new FakeHttpHandler()
				.Enqueue(HttpStatusCode.OK,TOKEN_JSON)
				.Enqueue(HttpStatusCode.OK,"{\"status\":\"PRE-APPROVED\",\"preApprovalAmount\":\"2500\"}");
			CareCreditApiClient client=new CareCreditApiClient(handler);
			CareCreditPullbackResult result=VendorHttp.RunSync(() => client.PullbackAsync(CareCreditTestData.CreateConfig(),"A/B 1",CareCreditTestData.MERCHANT,true));
			Assert.AreEqual(CareCreditWebStatus.PreApproved,result.Status);
			Assert.AreEqual(2500,result.PreApprovalAmount,0.001);
			RecordedRequest request=handler.Requests[1];
			Assert.AreEqual("GET",request.Method.Method);
			Assert.AreEqual("/v1/patient-data/pullback/A%2FB%201",request.RequestUri.AbsolutePath);
		}

		[TestMethod]
		public void Pullback_PostWhenThePathHasNoPlaceholder() {
			FakeHttpHandler handler=new FakeHttpHandler()
				.Enqueue(HttpStatusCode.OK,TOKEN_JSON)
				.Enqueue(HttpStatusCode.OK,"{\"status\":\"COMPLETED\",\"transactionType\":\"REFUND\",\"amount\":40}");
			CareCreditConfig config=CareCreditTestData.CreateConfig();
			config.PullbackPath="/patient-data/pullback";
			CareCreditApiClient client=new CareCreditApiClient(handler);
			CareCreditPullbackResult result=VendorHttp.RunSync(() => client.PullbackAsync(config,"REF-1",CareCreditTestData.MERCHANT,false));
			Assert.AreEqual(CareCreditWebStatus.Completed,result.Status);
			Assert.AreEqual(CareCreditTransType.Refund,result.TransType);
			RecordedRequest request=handler.Requests[1];
			Assert.AreEqual("POST",request.Method.Method);
			JObject body=JObject.Parse(request.Body);
			Assert.AreEqual("REF-1",(string)body["referenceId"]);
			Assert.AreEqual("OFFICE42",(string)body["partnerCode"]);
		}

		[TestMethod]
		public void BlockedApiHost_IsRefusedWithoutAnyRequest() {
			FakeHttpHandler handler=new FakeHttpHandler().Enqueue(HttpStatusCode.OK,TOKEN_JSON);
			CareCreditConfig config=CareCreditTestData.CreateConfig();
			config.ApiBaseUrl="https://api."+HqHosts.BlockedDomains.First()+"/v1";
			CareCreditApiClient client=new CareCreditApiClient(handler);
			AssertEx.Throws<CareCredit.CareCreditException>(() => VendorHttp.RunSync(() => client.PrefillAsync(config,CreatePrefillRequest())));
			Assert.AreEqual(0,handler.Requests.Count);
		}

		[TestMethod]
		public void BatchSubmit_SendsSsnLast4OnlyForLookupOnly() {
			FakeHttpHandler handler=new FakeHttpHandler()
				.Enqueue(HttpStatusCode.OK,TOKEN_JSON)
				.Enqueue(HttpStatusCode.OK,"{\"referenceId\":\"B-1\"}")
				.Enqueue(HttpStatusCode.OK,"{\"referenceId\":\"B-2\"}");
			CareCreditApiClient client=new CareCreditApiClient(handler);
			CareCreditConfig config=CareCreditTestData.CreateConfig();
			Patient patient=CareCreditTestData.CreateAdult(8);
			CareCreditBatchRequest requestQs=new CareCreditBatchRequest();
			requestQs.MerchantNumber=CareCreditTestData.MERCHANT;
			requestQs.PartnerCode="OFFICE42";
			requestQs.EstimatedFeeAmount=300;
			requestQs.Consumer=CareCreditConsumer.FromPatient(patient,includeDateOfBirth:false,includeSsnLast4:false);
			Assert.AreEqual("B-1",VendorHttp.RunSync(() => client.SubmitBatchAsync(config,requestQs)));
			CareCreditBatchRequest requestLookup=new CareCreditBatchRequest();
			requestLookup.MerchantNumber=CareCreditTestData.MERCHANT;
			requestLookup.PartnerCode="OFFICE42";
			requestLookup.IsLookupOnly=true;
			requestLookup.Consumer=CareCreditConsumer.FromPatient(patient,includeDateOfBirth:false,includeSsnLast4:true);
			Assert.AreEqual("B-2",VendorHttp.RunSync(() => client.SubmitBatchAsync(config,requestLookup)));
			JObject bodyQs=JObject.Parse(handler.Requests[1].Body);
			JObject bodyLookup=JObject.Parse(handler.Requests[2].Body);
			Assert.IsNull(bodyQs["consumer"]["ssnLast4"]);
			Assert.AreEqual("300",(string)bodyQs["estimatedFeeAmount"]);
			Assert.AreEqual("6789",(string)bodyLookup["consumer"]["ssnLast4"]);
			Assert.AreEqual("LOOKUP",(string)bodyLookup["requestType"]);
			Assert.IsFalse(handler.Requests.Any(x => x.Body.Contains("123456789")),"The full SSN is never sent.");
		}
	}
}
