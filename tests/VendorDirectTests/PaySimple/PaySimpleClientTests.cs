// Tests for the PaySimple API 4 client: the basic auth header, environments, the new status, list, order-lookup, credential-test and webhook calls,
// and error handling. Every request goes to a fake HttpMessageHandler; nothing touches the network. The credentials are made-up test values.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.VendorDirect;

namespace VendorDirectTests.PaySimple {
	[TestClass]
	public class PaySimpleClientTests {
		private const string USER="APIUserTest01";
		private const string KEY="fake-key-for-tests-only";
		private FakeHttpHandler _fakeHttpHandler;

		[TestInitialize]
		public void TestInitialize() {
			_fakeHttpHandler=new FakeHttpHandler();
			OpenDentBusiness.PaySimple.SetTestHttpHandler(_fakeHttpHandler,TimeSpan.FromMilliseconds(1));
		}

		[TestCleanup]
		public void TestCleanup() {
			OpenDentBusiness.PaySimple.SetTestHttpHandler(null);
		}

		private static OpenDentBusiness.PaySimple.PaySimpleCredentials Credentials(string environment=OpenDentBusiness.PaySimple.Environments.Production) {
			return new OpenDentBusiness.PaySimple.PaySimpleCredentials(USER,KEY,environment);
		}

		internal static string PaymentJson(long id,string status,string orderId="",string failureCode=null,string failureDescription=null,
			string actualSettledDate=null,string returnDate=null,string providerAuthCode="Approved")
		{
			string failureData=(failureCode==null) ? "null"
				: "{\"Code\":\""+failureCode+"\",\"Description\":\""+failureDescription+"\",\"MerchantActionText\":\"Contact the customer\",\"IsDecline\":false}";
			return "{\"Id\":"+id+",\"CustomerId\":8051100,\"AccountId\":7320843,\"ReferenceId\":0,\"Status\":\""+status+"\","
				+"\"PaymentType\":\"ACH\",\"PaymentSubType\":\"Ppd\",\"ProviderAuthCode\":\""+providerAuthCode+"\",\"Amount\":125.50,"
				+"\"OrderId\":"+(orderId=="" ? "null" : "\""+orderId+"\"")+","
				+"\"PaymentDate\":\"2026-10-01T06:00:00Z\","
				+"\"ActualSettledDate\":"+(actualSettledDate==null ? "null" : "\""+actualSettledDate+"\"")+","
				+"\"ReturnDate\":"+(returnDate==null ? "null" : "\""+returnDate+"\"")+","
				+"\"FailureData\":"+failureData+"}";
		}

		internal static string Envelope(string responseJson,string pagingDetails="null",int httpStatusCode=200) {
			return "{\"Meta\":{\"Errors\":null,\"HttpStatus\":\"OK\",\"HttpStatusCode\":"+httpStatusCode+",\"PagingDetails\":"+pagingDetails+"},"
				+"\"Response\":"+responseJson+"}";
		}

		[TestMethod]
		public void AuthHeader_IsBasicUserColonKey_WithoutBase64() {
			Assert.AreEqual("basic "+USER+":"+KEY,OpenDentBusiness.PaySimple.GetAuthHeaderValue(USER,KEY));
			_fakeHttpHandler.Enqueue(HttpStatusCode.OK,Envelope("[]"));
			Assert.IsTrue(OpenDentBusiness.PaySimple.TestCredentials(Credentials()));
			RecordedRequest recordedRequest=_fakeHttpHandler.Requests.Single();
			Assert.AreEqual("basic "+USER+":"+KEY,recordedRequest.GetHeader("Authorization"));
			Assert.IsFalse(recordedRequest.GetHeader("Authorization").Contains("PSSERVER"));
		}

		[TestMethod]
		public void TestCredentials_GetsOneCustomer_TrueOn200_FalseOn401() {
			_fakeHttpHandler.Enqueue(HttpStatusCode.OK,Envelope("[]"));
			_fakeHttpHandler.Enqueue(HttpStatusCode.Unauthorized,"");
			Assert.IsTrue(OpenDentBusiness.PaySimple.TestCredentials(Credentials()));
			Assert.IsFalse(OpenDentBusiness.PaySimple.TestCredentials(Credentials()));
			List<RecordedRequest> listRequests=_fakeHttpHandler.Requests;
			Assert.AreEqual(2,listRequests.Count);
			Assert.AreEqual(HttpMethod.Get,listRequests[0].Method);
			Assert.AreEqual("https://api.paysimple.com/v4/customer?page=1&pagesize=1",listRequests[0].RequestUri.AbsoluteUri);
		}

		[TestMethod]
		public void Environment_SandboxUsesSandboxHost_BlankIsProduction() {
			Assert.AreEqual(OpenDentBusiness.PaySimple.Environments.Production,OpenDentBusiness.PaySimple.NormalizeEnvironment(""));
			Assert.AreEqual(OpenDentBusiness.PaySimple.Environments.Production,OpenDentBusiness.PaySimple.NormalizeEnvironment(null));
			Assert.AreEqual(OpenDentBusiness.PaySimple.Environments.Sandbox,OpenDentBusiness.PaySimple.NormalizeEnvironment(" sandbox "));
			Assert.AreEqual(OpenDentBusiness.PaySimple.Environments.Production,OpenDentBusiness.PaySimple.NormalizeEnvironment("Live"));
			_fakeHttpHandler.Enqueue(HttpStatusCode.OK,Envelope("[]"));
			OpenDentBusiness.PaySimple.TestCredentials(Credentials(OpenDentBusiness.PaySimple.Environments.Sandbox));
			Assert.AreEqual("sandbox-api.paysimple.com",_fakeHttpHandler.Requests.Single().RequestUri.Host);
		}

		[TestMethod]
		public void Credentials_IncompleteAreRefusedWithoutACall() {
			Assert.IsFalse(new OpenDentBusiness.PaySimple.PaySimpleCredentials(USER,"  ","Production").IsComplete);
			Assert.IsFalse(new OpenDentBusiness.PaySimple.PaySimpleCredentials("",KEY,"Production").IsComplete);
			AssertEx.Throws<CodeBase.ODException>(() => OpenDentBusiness.PaySimple.TestCredentials(new OpenDentBusiness.PaySimple.PaySimpleCredentials(USER,"","")));
			Assert.AreEqual(0,_fakeHttpHandler.Requests.Count);
		}

		[TestMethod]
		public void GetPaymentStatus_ParsesStatusDatesAndFailureData() {
			_fakeHttpHandler.Enqueue(HttpStatusCode.OK,Envelope(PaymentJson(29124043,"Returned","OPMS-abc","R01","Insufficient Funds",
				actualSettledDate:"2026-10-03T06:00:00Z",returnDate:"2026-10-06T06:00:00Z")));
			OpenDentBusiness.PaySimple.PaySimplePaymentStatus paySimplePaymentStatus=OpenDentBusiness.PaySimple.GetPaymentStatus(Credentials(),"29124043");
			Assert.AreEqual("https://api.paysimple.com/v4/payment/29124043",_fakeHttpHandler.Requests.Single().RequestUri.AbsoluteUri);
			Assert.AreEqual(29124043,paySimplePaymentStatus.Id);
			Assert.AreEqual("Returned",paySimplePaymentStatus.Status);
			Assert.AreEqual("OPMS-abc",paySimplePaymentStatus.OrderId);
			Assert.AreEqual("R01",paySimplePaymentStatus.FailureCode);
			Assert.AreEqual("Insufficient Funds",paySimplePaymentStatus.FailureDescription);
			Assert.AreEqual(125.50m,paySimplePaymentStatus.Amount);
			Assert.AreEqual(new DateTime(2026,10,6),paySimplePaymentStatus.ReturnDate.Value.ToUniversalTime().Date);
			Assert.AreEqual(new DateTime(2026,10,3),paySimplePaymentStatus.ActualSettledDate.Value.ToUniversalTime().Date);
		}

		[TestMethod]
		public void ListPayments_SendsStatusDateLitePaging_AndReadsPagingDetails() {
			string json=Envelope("["+PaymentJson(1,"Returned")+","+PaymentJson(2,"ChargeBack")+"]",
				"{\"TotalItems\":201,\"Page\":1,\"ItemsPerPage\":200}");
			_fakeHttpHandler.Enqueue(HttpStatusCode.OK,json);
			OpenDentBusiness.PaySimple.PaySimplePaymentPage paySimplePaymentPage=OpenDentBusiness.PaySimple.ListPayments(Credentials(),
				PaySimpleAchStatusSync.ArrayLateReturnStatuses,new DateTime(2026,8,5),1);
			Uri uri=_fakeHttpHandler.Requests.Single().RequestUri;
			Assert.AreEqual("/v4/payment",uri.AbsolutePath);
			string query=Uri.UnescapeDataString(uri.Query);
			StringAssert.Contains(query,"status=Returned,ReverseNSF,ChargeBack");
			StringAssert.Contains(query,"startdate=2026-08-05");
			StringAssert.Contains(query,"lite=true");
			StringAssert.Contains(query,"page=1");
			StringAssert.Contains(query,"pagesize=200");
			Assert.AreEqual(2,paySimplePaymentPage.ListPayments.Count);
			Assert.AreEqual(201,paySimplePaymentPage.TotalItems);
			Assert.IsTrue(paySimplePaymentPage.HasMorePages);
		}

		[TestMethod]
		public void FindPaymentByOrderId_PagesThroughTheCustomersPayments() {
			_fakeHttpHandler.Enqueue(HttpStatusCode.OK,Envelope("["+PaymentJson(1,"Posted","OPMS-other")+"]","{\"TotalItems\":2,\"Page\":1,\"ItemsPerPage\":1}"));
			_fakeHttpHandler.Enqueue(HttpStatusCode.OK,Envelope("["+PaymentJson(2,"Posted","OPMS-wanted")+"]","{\"TotalItems\":2,\"Page\":2,\"ItemsPerPage\":1}"));
			OpenDentBusiness.PaySimple.PaySimplePaymentStatus paySimplePaymentStatus=OpenDentBusiness.PaySimple.FindPaymentByOrderId(Credentials(),8051100,
				"OPMS-wanted");
			Assert.IsNotNull(paySimplePaymentStatus);
			Assert.AreEqual(2,paySimplePaymentStatus.Id);
			List<RecordedRequest> listRequests=_fakeHttpHandler.Requests;
			Assert.AreEqual(2,listRequests.Count);
			Assert.AreEqual("/v4/customer/8051100/payments",listRequests[0].RequestUri.AbsolutePath);
			StringAssert.Contains(listRequests[1].RequestUri.Query,"page=2");
		}

		[TestMethod]
		public void FindPaymentByOrderId_ReturnsNullWhenAbsent() {
			_fakeHttpHandler.Enqueue(HttpStatusCode.OK,Envelope("["+PaymentJson(1,"Posted","OPMS-other")+"]","{\"TotalItems\":1,\"Page\":1,\"ItemsPerPage\":200}"));
			Assert.IsNull(OpenDentBusiness.PaySimple.FindPaymentByOrderId(Credentials(),8051100,"OPMS-wanted"));
			Assert.AreEqual(1,_fakeHttpHandler.Requests.Count);
		}

		[TestMethod]
		public void Webhooks_ListAndDelete_NeverCreate() {
			string json="{\"total_item_count\":2,\"data\":[{\"id\":\"wh_1\",\"url\":\"https://example.invalid/hook\",\"event_types\":[\"payment_settled\","
				+"\"payment_failed\"],\"is_active\":true},{\"id\":\"wh_2\",\"url\":\"https://office.example/ps\",\"event_types\":null,\"is_active\":false}]}";
			_fakeHttpHandler.Enqueue(HttpStatusCode.OK,json);
			_fakeHttpHandler.Enqueue(HttpStatusCode.NoContent,"");
			List<OpenDentBusiness.PaySimple.ApiWebhookResponse> listWebhooks=OpenDentBusiness.PaySimple.GetWebhooks(Credentials());
			Assert.AreEqual(2,listWebhooks.Count);
			Assert.AreEqual("wh_1",listWebhooks[0].WebhookId);
			Assert.AreEqual("https://example.invalid/hook",listWebhooks[0].WebhookUrl);
			CollectionAssert.AreEqual(new[] { "payment_settled","payment_failed" },listWebhooks[0].WebhookTypes.ToArray());
			Assert.AreEqual(0,listWebhooks[1].WebhookTypes.Count);
			OpenDentBusiness.PaySimple.DeleteWebhook(Credentials(),"wh_1");
			List<RecordedRequest> listRequests=_fakeHttpHandler.Requests;
			Assert.AreEqual("https://api.paysimple.com/ps/webhook/subscriptions",listRequests[0].RequestUri.AbsoluteUri);
			Assert.AreEqual(HttpMethod.Delete,listRequests[1].Method);
			Assert.AreEqual("https://api.paysimple.com/ps/webhook/subscription/wh_1",listRequests[1].RequestUri.AbsoluteUri);
			Assert.AreEqual("basic "+USER+":"+KEY,listRequests[1].GetHeader("Authorization"));
			Assert.IsFalse(listRequests.Any(x => x.Method==HttpMethod.Post));
		}

		[TestMethod]
		public void Errors_PaySimpleErrorBody_GivesPaySimpleException() {
			string json="{\"Meta\":{\"Errors\":{\"ErrorCode\":\"InvalidInput\",\"ErrorMessages\":[{\"Field\":\"Id\",\"Message\":\"Payment 5 not found\"}]},"
				+"\"HttpStatus\":\"BadRequest\",\"HttpStatusCode\":400,\"PagingDetails\":null},\"Response\":null}";
			_fakeHttpHandler.Enqueue(HttpStatusCode.BadRequest,json);
			PaySimpleException paySimpleException=AssertEx.Throws<PaySimpleException>(
				() => OpenDentBusiness.PaySimple.GetPaymentStatus(Credentials(),"5"));
			Assert.AreEqual("InvalidInput",paySimpleException.ErrorCode);
			CollectionAssert.AreEqual(new[] { "Payment 5 not found" },paySimpleException.ErrorMessages);
		}

		[TestMethod]
		public void Errors_401_GivesCredentialsException() {
			_fakeHttpHandler.Enqueue(HttpStatusCode.Unauthorized,"{\"Message\":\"Authorization has been denied for this request.\"}");
			AssertEx.Throws<PaySimpleCredentialsException>(() => OpenDentBusiness.PaySimple.GetPaymentStatus(Credentials(),"5"));
		}

		[TestMethod]
		public void Errors_503OnGet_IsRetriedThenTransient() {
			_fakeHttpHandler.SetDefault((request,ct) => System.Threading.Tasks.Task.FromResult(
				FakeHttpHandler.MakeResponse(HttpStatusCode.ServiceUnavailable,"<html>busy</html>","text/html")));
			PaySimpleHttpException paySimpleHttpException=AssertEx.Throws<PaySimpleHttpException>(
				() => OpenDentBusiness.PaySimple.GetPaymentStatus(Credentials(),"5"));
			Assert.AreEqual(503,paySimpleHttpException.StatusCode);
			Assert.IsTrue(paySimpleHttpException.IsTransient);
			Assert.AreEqual(4,_fakeHttpHandler.Requests.Count,"One request plus three retries.");
		}

		[TestMethod]
		public void OrderId_HasOurPrefixAndFitsPaySimplesLimit() {
			string orderId=OpenDentBusiness.PaySimple.NewOrderId();
			StringAssert.StartsWith(orderId,"OPMS-");
			Assert.IsTrue(orderId.Length<=50);
			Assert.AreNotEqual(orderId,OpenDentBusiness.PaySimple.NewOrderId());
		}

		[TestMethod]
		public void ApiKey_StoredEncrypted_PlainTextFromOpenDentalStillAccepted() {
			string stored=SecretsTestHelper.Protect(KEY);
			SecretsTestHelper.AssertRoundTrip(KEY,stored);
			Assert.AreEqual(KEY,SecretValue.ReadOrBlank(stored,LegacyPolicy.AcceptPlaintext));
			//Open Dental 24.3 stored the key as typed. It keeps working until the next save encrypts it.
			SecretReadResult secretReadResult=SecretValue.Read(KEY,LegacyPolicy.AcceptPlaintext);
			Assert.AreEqual(KEY,secretReadResult.PlainText);
			Assert.IsTrue(secretReadResult.NeedsReEncrypt);
			//A damaged osd1$ value is never sent as if it were the key.
			string corrupted=stored.Substring(0,stored.Length-4)+(stored.EndsWith("AAAA") ? "BBBB" : "AAAA");
			Assert.AreEqual("",SecretValue.ReadOrBlank(corrupted,LegacyPolicy.AcceptPlaintext));
		}
	}
}
