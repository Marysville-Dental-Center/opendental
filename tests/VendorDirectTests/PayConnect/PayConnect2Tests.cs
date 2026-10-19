// Tests for the PayConnect 2 library (docs/specs/vendor-direct/payconnect.md, section 6.4). No network: requests go to a fake handler.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using CodeBase;
using CodeBase.VendorDirect;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using OpenDentBusiness;

namespace VendorDirectTests.PayConnectV2 {
	[TestClass]
	public class PayConnect2Tests {
		private const string API_KEY="test-integrator-key-0001";
		private const string SECRET="test-merchant-secret-0001";

		[TestInitialize]
		public void TestInitialize() {
			PayConnect2.Mock=null;
			PayConnect2.HttpHandlerForTests=null;
			PayConnect2.FuncGetNow=() => new DateTime(2026,10,19,9,30,0);
		}

		[TestCleanup]
		public void TestCleanup() {
			PayConnect2.HttpHandlerForTests=null;
			PayConnect2.FuncGetNow=() => DateTime.Now;
		}

		private static FakeHttpHandler UseFakeHandler() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler();
			PayConnect2.HttpHandlerForTests=fakeHttpHandler;
			return fakeHttpHandler;
		}

		private static string Serialize(object obj) {
			return JsonConvert.SerializeObject(obj,new JsonSerializerSettings { NullValueHandling=NullValueHandling.Ignore });
		}

		private static HttpRequestException MakeNetworkException(WebExceptionStatus webExceptionStatus) {
			return new HttpRequestException("An error occurred while sending the request.",new WebException("Simulated.",webExceptionStatus));
		}

		#region Base address and headers
		[TestMethod]
		public void GetApiBaseUrl_ProductionAndSandbox() {
			Assert.AreEqual("https://api.dentalxchange.com/payments",PayConnect2.GetApiBaseUrl(false));
			Assert.AreEqual("https://api.dentalxchange.com/sandbox/payments",PayConnect2.GetApiBaseUrl(true));
			Assert.IsFalse(PayConnect2.GetApiBaseUrl(false).Contains("staging"),"The non-public staging host is gone.");
		}

		[TestMethod]
		public void GetHeadersForApi_BuildsBothHeaders() {
			List<string> listHeaders=PayConnect2.GetHeadersForApi(API_KEY,SECRET);
			CollectionAssert.AreEqual(new[] { "API-Key: "+API_KEY,"Secret: "+SECRET },listHeaders);
		}

		[TestMethod]
		public void MakeCredentials_MissingValuesThrowNotConfigured() {
			AssertEx.Throws<PayConnect2.PayConnectNotConfiguredException>(() => PayConnect2.MakeCredentials("",SECRET,false));
			AssertEx.Throws<PayConnect2.PayConnectNotConfiguredException>(() => PayConnect2.MakeCredentials(API_KEY," ",false));
			PayConnect2.ApiCredentials apiCredentials=PayConnect2.MakeCredentials(" "+API_KEY+" ",SECRET,true);
			Assert.AreEqual(API_KEY,apiCredentials.ApiKey,"Typed values are trimmed.");
			Assert.AreEqual(PayConnect2.UrlBaseSandbox,apiCredentials.BaseUrl);
			Assert.IsFalse(apiCredentials.ToString().Contains(API_KEY),"ToString never shows a key.");
		}

		[TestMethod]
		public void BuildStatusQuery_AppendsEachValue() {
			Assert.AreEqual("",PayConnect2.BuildStatusQuery(""));
			Assert.AreEqual("?referenceId=R1",PayConnect2.BuildStatusQuery("R1"));
			Assert.AreEqual("?transactionId=42",PayConnect2.BuildStatusQuery("",42));
			Assert.AreEqual("?orderId=O%201",PayConnect2.BuildStatusQuery("",0,"O 1"));
			Assert.AreEqual("?referenceId=R1&transactionId=42&orderId=O9",PayConnect2.BuildStatusQuery("R1",42,"O9"),
				"Both values are kept; the old code overwrote the first.");
		}
		#endregion Base address and headers

		#region Request bodies
		[TestMethod]
		public void EmbedSessionRequest_LeavesOutAmountWhenNull() {
			PayConnect2.EmbedSessionRequest embedSessionRequest=new PayConnect2.EmbedSessionRequest();
			embedSessionRequest.Type=PayConnect2.IframeType.Tokenizer;
			string json=Serialize(embedSessionRequest);
			Assert.IsFalse(json.Contains("\"amount\""),json);
			StringAssert.Contains(json,"\"type\":\"Tokenizer\"");
			StringAssert.Contains(json,"\"usePatient\":false");
			embedSessionRequest.Type=PayConnect2.IframeType.Payment;
			embedSessionRequest.Amount=1362;
			StringAssert.Contains(Serialize(embedSessionRequest),"\"amount\":1362");
		}

		[TestMethod]
		public void CreateRequests_SendNoPatientObject() {
			PayConnect2.CreateTransactionRequest createTransactionRequest=new PayConnect2.CreateTransactionRequest();
			createTransactionRequest.CardToken="tok";
			createTransactionRequest.InvoiceNumber="OPMS-0123456789ab";
			string json=Serialize(createTransactionRequest);
			Assert.IsFalse(json.Contains("patient"),json);
			StringAssert.Contains(json,"\"invoiceNumber\":\"OPMS-0123456789ab\"");
			Assert.IsFalse(Serialize(new PayConnect2.CreateSurchargeTransactionRequest()).Contains("patient"));
			Assert.IsFalse(Serialize(new PayConnect2.CreateTerminalTransactionRequest()).Contains("patient"));
		}

		[TestMethod]
		public void NewCorrelationId_IsOpaqueAndUnique() {
			string id=PayConnect2.NewCorrelationId();
			StringAssert.StartsWith(id,"OPMS-");
			Assert.AreEqual(17,id.Length);
			Assert.IsTrue(id.Substring(5).All(x => Uri.IsHexDigit(x)));
			Assert.AreNotEqual(id,PayConnect2.NewCorrelationId());
		}
		#endregion Request bodies

		#region Error parsing
		[TestMethod]
		public void ParseErrorResponse_StringErrorOn401IsTheApiKey() {
			PayConnect2.ErrorResponse errorResponse=PayConnect2.ParseErrorResponse(HttpStatusCode.Unauthorized,"{\"error\":\"Authorization field missing\"}");
			Assert.AreEqual(PayConnect2.PayConnectErrorKind.ApiKeyRejected,errorResponse.Kind);
			StringAssert.Contains(errorResponse.Message,"API key");
			Assert.AreEqual(errorResponse.Message,errorResponse.Error.ToString());
		}

		[TestMethod]
		public void ParseErrorResponse_403IsTheApiKey() {
			PayConnect2.ErrorResponse errorResponse=PayConnect2.ParseErrorResponse(HttpStatusCode.Forbidden,
				"{\"error\":\"Access to this API has been disallowed\"}");
			Assert.AreEqual(PayConnect2.PayConnectErrorKind.ApiKeyRejected,errorResponse.Kind);
		}

		[TestMethod]
		public void ParseErrorResponse_ObjectErrorOn401IsTheSecret() {
			//The vendor's sample text says "Invalid API key" even for the Secret case: the shape decides, not the text.
			PayConnect2.ErrorResponse errorResponse=PayConnect2.ParseErrorResponse(HttpStatusCode.Unauthorized,
				"{\"errorType\":\"RESPONSE\",\"error\":{\"message\":\"Invalid API key\"}}");
			Assert.AreEqual(PayConnect2.PayConnectErrorKind.SecretRejected,errorResponse.Kind);
			StringAssert.Contains(errorResponse.Message,"Secret");
		}

		[TestMethod]
		public void ParseErrorResponse_ValidationListsEachParamWithoutTheValue() {
			string body="{\"errorType\":\"VALIDATION\",\"error\":[{\"message\":\"must be a number\",\"param\":\"amount\",\"location\":\"body\","
				+"\"value\":\"4111111111111111\"},{\"message\":\"is required\",\"param\":\"expiry\",\"location\":\"body\"}]}";
			PayConnect2.ErrorResponse errorResponse=PayConnect2.ParseErrorResponse(HttpStatusCode.BadRequest,body);
			Assert.AreEqual(PayConnect2.PayConnectErrorKind.Validation,errorResponse.Kind);
			Assert.AreEqual(PayConnect2.ErrorType.Validation,errorResponse.ErrorType);
			Assert.AreEqual(2,errorResponse.ListValidationErrors.Count);
			StringAssert.Contains(errorResponse.Message,"amount: must be a number");
			StringAssert.Contains(errorResponse.Message,"expiry: is required");
			Assert.IsFalse(errorResponse.Message.Contains("4111"),"Rejected values can be card data and are never shown.");
			Assert.IsTrue(errorResponse.ListValidationErrors.All(x => string.IsNullOrEmpty(x.Value)));
		}

		[TestMethod]
		public void ParseErrorResponse_ResponseErrorKeepsTheVendorMessage() {
			PayConnect2.ErrorResponse errorResponse=PayConnect2.ParseErrorResponse(HttpStatusCode.NotFound,
				"{\"errorType\":\"RESPONSE\",\"error\":{\"message\":\"Transaction declined\",\"code\":5}}");
			Assert.AreEqual(PayConnect2.PayConnectErrorKind.Vendor,errorResponse.Kind);
			Assert.AreEqual("Transaction declined",errorResponse.Message);
			Assert.AreEqual(404,errorResponse.HttpStatus);
		}

		[TestMethod]
		public void ParseErrorResponse_NonJsonBodyGivesAGenericMessage() {
			PayConnect2.ErrorResponse errorResponse=PayConnect2.ParseErrorResponse(HttpStatusCode.BadGateway,"<html>Bad gateway</html>");
			Assert.AreEqual(PayConnect2.PayConnectErrorKind.Vendor,errorResponse.Kind);
			StringAssert.Contains(errorResponse.Message,"502");
		}

		[TestMethod]
		public void ApiResponseToPayConnectResponse_ErrorUsesTheMessage() {
			PayConnect2.PayConnect2Response response=PayConnect2.ParseHttpResponse(PayConnect2.ApiRoute.CreateTransaction,HttpStatusCode.Unauthorized,
				"{\"error\":\"Authorization field missing\"}");
			PayConnectResponse payConnectResponse=PayConnect2.ApiResponseToPayConnectResponse(response);
			Assert.AreEqual(PayConnectResponse.TransactionType.Unknown,payConnectResponse.TransType);
			StringAssert.Contains(payConnectResponse.Description,"API key");
		}

		[TestMethod]
		public void ApiResponseToPayConnectResponse_ToleratesAMissingPaymentMethod() {
			PayConnect2.PayConnect2Response response=PayConnect2.ParseHttpResponse(PayConnect2.ApiRoute.GetStatus,HttpStatusCode.OK,
				"{\"referenceId\":\"R7\",\"status\":\"Processed\",\"type\":\"Sale\",\"amount\":500}");
			PayConnectResponse payConnectResponse=PayConnect2.ApiResponseToPayConnectResponse(response);
			Assert.AreEqual("R7",payConnectResponse.RefNumber);
			Assert.AreEqual(5m,payConnectResponse.Amount);
		}
		#endregion Error parsing

		#region HTTP calls
		[TestMethod]
		public void TestConnection_SendsBothKeysToTheSandbox() {
			FakeHttpHandler fakeHttpHandler=UseFakeHandler();
			fakeHttpHandler.Enqueue(HttpStatusCode.OK,"{\"merchantId\":77,\"name\":\"Test Dental\",\"status\":\"Active\"}");
			PayConnect2.PayConnect2Response response=PayConnect2.TestConnection(API_KEY,SECRET,true);
			Assert.AreEqual(PayConnect2.ResponseType.MerchantInfo,response.ResponseType);
			Assert.AreEqual(77,response.GetMerchantInfoResponse.MerchantId);
			Assert.AreEqual(1,fakeHttpHandler.Requests.Count);
			RecordedRequest recordedRequest=fakeHttpHandler.Requests[0];
			Assert.AreEqual(HttpMethod.Get,recordedRequest.Method);
			Assert.AreEqual("https://api.dentalxchange.com/sandbox/payments/merchants",recordedRequest.RequestUri.AbsoluteUri);
			Assert.AreEqual(API_KEY,recordedRequest.GetHeader("API-Key"));
			Assert.AreEqual(SECRET,recordedRequest.GetHeader("Secret"));
		}

		[TestMethod]
		public void TestConnection_ProductionHost() {
			FakeHttpHandler fakeHttpHandler=UseFakeHandler();
			fakeHttpHandler.Enqueue(HttpStatusCode.OK,"{\"merchantId\":1}");
			PayConnect2.TestConnection(API_KEY,SECRET,false);
			Assert.AreEqual("https://api.dentalxchange.com/payments/merchants",fakeHttpHandler.Requests[0].RequestUri.AbsoluteUri);
		}

		[TestMethod]
		public void TestConnection_MissingSecretSendsNothing() {
			FakeHttpHandler fakeHttpHandler=UseFakeHandler();
			PayConnect2.PayConnect2Response response=PayConnect2.TestConnection(API_KEY,"",true);
			Assert.AreEqual(PayConnect2.ResponseType.Error,response.ResponseType);
			Assert.AreEqual(PayConnect2.PayConnectRequestOutcome.NotSent,response.Outcome);
			Assert.AreEqual(PayConnect2.PayConnectErrorKind.NotConfigured,response.ErrorResponse.Kind);
			Assert.AreEqual(0,fakeHttpHandler.Requests.Count);
		}

		[TestMethod]
		public void TestConnection_RejectedKeyGivesTheKeyMessage() {
			FakeHttpHandler fakeHttpHandler=UseFakeHandler();
			fakeHttpHandler.Enqueue(HttpStatusCode.Unauthorized,"{\"error\":\"Authorization field missing\"}");
			PayConnect2.PayConnect2Response response=PayConnect2.TestConnection(API_KEY,SECRET,true);
			Assert.AreEqual(PayConnect2.ResponseType.Error,response.ResponseType);
			Assert.AreEqual(PayConnect2.PayConnectRequestOutcome.Response,response.Outcome);
			Assert.AreEqual(PayConnect2.PayConnectErrorKind.ApiKeyRejected,response.ErrorResponse.Kind);
		}

		[TestMethod]
		public void ConnectFailure_IsNotSentAndSaysNoCharge() {
			FakeHttpHandler fakeHttpHandler=UseFakeHandler();
			fakeHttpHandler.Enqueue((request,ct) => throw MakeNetworkException(WebExceptionStatus.ConnectFailure));
			PayConnect2.PayConnect2Response response=PayConnect2.TestConnection(API_KEY,SECRET,true);
			Assert.AreEqual(PayConnect2.PayConnectRequestOutcome.NotSent,response.Outcome);
			StringAssert.Contains(response.ErrorResponse.Message,"no charge was made");
		}

		[TestMethod]
		public void ErrorMessages_NeverContainTheKeys() {
			FakeHttpHandler fakeHttpHandler=UseFakeHandler();
			fakeHttpHandler.Enqueue((request,ct) => throw new HttpRequestException("failed for "+API_KEY+" "+SECRET));
			fakeHttpHandler.Enqueue(HttpStatusCode.Unauthorized,"{\"errorType\":\"RESPONSE\",\"error\":{\"message\":\"bad\"}}");
			fakeHttpHandler.Enqueue(HttpStatusCode.OK,"not json");
			for(int i=0;i<3;i++) {
				PayConnect2.PayConnect2Response response=PayConnect2.TestConnection(API_KEY,SECRET,true);
				Assert.AreEqual(PayConnect2.ResponseType.Error,response.ResponseType);
				string text=response.ErrorResponse.Message+" "+response.ErrorResponse.Error;
				Assert.IsFalse(text.Contains(API_KEY),text);
				Assert.IsFalse(text.Contains(SECRET),text);
			}
		}

		[TestMethod]
		public void ClassifyFailure_OnlyCertainFailuresAreNotSent() {
			Assert.AreEqual(PayConnect2.PayConnectRequestOutcome.NotSent,PayConnect2.ClassifyFailure(MakeNetworkException(WebExceptionStatus.ConnectFailure)));
			Assert.AreEqual(PayConnect2.PayConnectRequestOutcome.NotSent,
				PayConnect2.ClassifyFailure(MakeNetworkException(WebExceptionStatus.NameResolutionFailure)));
			Assert.AreEqual(PayConnect2.PayConnectRequestOutcome.NotSent,PayConnect2.ClassifyFailure(new VendorHttpException("PayConnect",
				VendorHttpFailure.UrlRefused,"refused",VendorUrlProblem.HostNotAllowed)));
			Assert.AreEqual(PayConnect2.PayConnectRequestOutcome.SentNoResponse,PayConnect2.ClassifyFailure(new VendorHttpException("PayConnect",
				VendorHttpFailure.Timeout,"timed out")));
			Assert.AreEqual(PayConnect2.PayConnectRequestOutcome.SentNoResponse,
				PayConnect2.ClassifyFailure(MakeNetworkException(WebExceptionStatus.ReceiveFailure)));
			Assert.AreEqual(PayConnect2.PayConnectRequestOutcome.SentNoResponse,PayConnect2.ClassifyFailure(new HttpRequestException("unknown")),
				"Unknown failures are treated as maybe-sent, so a create is reconciled rather than repeated.");
			Assert.AreEqual(PayConnect2.PayConnectRequestOutcome.SentNoResponse,PayConnect2.ClassifyFailure(new TaskCanceledException()));
		}

		[TestMethod]
		public void GetTerminals_ParsesTheList() {
			FakeHttpHandler fakeHttpHandler=UseFakeHandler();
			fakeHttpHandler.Enqueue(HttpStatusCode.OK,"[{\"terminalId\":12,\"terminal\":\"SN-001\",\"name\":\"Front desk\",\"type\":\"Clover\"},"
				+"{\"terminalId\":13,\"terminal\":null,\"name\":\"Back\",\"type\":null}]");
			PayConnect2.PayConnect2Response response=PayConnect2.GetTerminals(PayConnect2.MakeCredentials(API_KEY,SECRET,true));
			Assert.AreEqual(PayConnect2.ResponseType.Terminals,response.ResponseType);
			Assert.AreEqual(2,response.ListTerminals.Count);
			Assert.AreEqual("SN-001",response.ListTerminals[0].Serial);
			Assert.AreEqual("12",response.ListTerminals[0].TerminalId);
			Assert.AreEqual("",response.ListTerminals[1].Serial);
			Assert.AreEqual("https://api.dentalxchange.com/sandbox/payments/terminals",fakeHttpHandler.Requests[0].RequestUri.AbsoluteUri);
		}
		#endregion HTTP calls

		#region Reconciliation
		private static string MakeTransactionJson(string referenceId,string invoiceNumber,string status="Processed") {
			return "{\"referenceId\":\""+referenceId+"\",\"invoiceNumber\":\""+invoiceNumber+"\",\"status\":\""+status+"\",\"type\":\"Sale\",\"amount\":2500,"
				+"\"amountSurcharged\":75,\"paymentMethod\":{\"cardPaymentMethod\":{\"cardLast4Digits\":\"1111\",\"network\":\"Visa\"}}}";
		}

		private static PayConnect2.PayConnect2Response PostLostCreate(string invoiceNumber) {
			PayConnect2.CreateTransactionRequest createTransactionRequest=new PayConnect2.CreateTransactionRequest();
			createTransactionRequest.CardToken="tok";
			createTransactionRequest.Amount=2500;
			createTransactionRequest.InvoiceNumber=invoiceNumber;
			return PayConnect2.PostCreateTransaction(createTransactionRequest,PayConnect2.MakeCredentials(API_KEY,SECRET,true));
		}

		[TestMethod]
		public void LostCreateResponse_DeclinedListMatchIsADecline() {
			FakeHttpHandler fakeHttpHandler=UseFakeHandler();
			fakeHttpHandler.Enqueue((request,ct) => throw MakeNetworkException(WebExceptionStatus.ReceiveFailure));
			fakeHttpHandler.Enqueue(HttpStatusCode.OK,"["+MakeTransactionJson("R-DECL","OPMS-decl00000001","Declined")+"]");
			PayConnect2.PayConnect2Response response=PostLostCreate("OPMS-decl00000001");
			Assert.AreEqual(PayConnect2.ResponseType.Error,response.ResponseType);
			Assert.AreEqual(PayConnect2.PayConnectRequestOutcome.Reconciled,response.Outcome);
			Assert.AreEqual("OPMS-decl00000001",response.CorrelationId);
			Assert.AreEqual(PayConnect2.PayConnectErrorKind.Vendor,response.ErrorResponse.Kind);
			Assert.AreEqual("PayConnect declined the transaction (status: Declined).",response.ErrorResponse.Message);
			Assert.AreEqual(response.ErrorResponse.Message,response.ErrorResponse.Error.ToString());
			Assert.AreEqual(2,fakeHttpHandler.Requests.Count,"A declined entry needs no status read.");
			PayConnectResponse payConnectResponse=PayConnect2.ApiResponseToPayConnectResponse(response);
			Assert.AreNotEqual("0",payConnectResponse.StatusCode);
			Assert.AreEqual(PayConnectResponse.TransactionType.Unknown,payConnectResponse.TransType);
		}

		[TestMethod]
		public void LostCreateResponse_StatusReadThatShowsADeclineIsADecline() {
			FakeHttpHandler fakeHttpHandler=UseFakeHandler();
			fakeHttpHandler.Enqueue((request,ct) => throw MakeNetworkException(WebExceptionStatus.ReceiveFailure));
			fakeHttpHandler.Enqueue(HttpStatusCode.OK,"["+MakeTransactionJson("R-X","OPMS-err000000001","Processed")+"]");
			fakeHttpHandler.Enqueue(HttpStatusCode.OK,MakeTransactionJson("R-X","OPMS-err000000001","Error"));
			PayConnect2.PayConnect2Response response=PostLostCreate("OPMS-err000000001");
			Assert.AreEqual(PayConnect2.ResponseType.Error,response.ResponseType);
			StringAssert.Contains(response.ErrorResponse.Message,"(status: Error)");
		}

		[TestMethod]
		public void LostCreateResponse_FallbackParsedMatchIsReReadForItsAmount() {
			FakeHttpHandler fakeHttpHandler=UseFakeHandler();
			fakeHttpHandler.Enqueue((request,ct) => throw MakeNetworkException(WebExceptionStatus.ReceiveFailure));
			//A type this model doesn't know makes ParseTransactionList fall back to a bare entry: no amount, type, card or surcharge.
			fakeHttpHandler.Enqueue(HttpStatusCode.OK,"[{\"referenceId\":\"R-NEW\",\"invoiceNumber\":\"OPMS-new000000001\",\"status\":\"Processed\","
				+"\"type\":\"SomethingNew\",\"amount\":2500}]");
			fakeHttpHandler.Enqueue(HttpStatusCode.OK,MakeTransactionJson("R-NEW","OPMS-new000000001"));
			PayConnect2.PayConnect2Response response=PostLostCreate("OPMS-new000000001");
			Assert.AreEqual(PayConnect2.ResponseType.GetStatus,response.ResponseType);
			Assert.AreEqual(PayConnect2.PayConnectRequestOutcome.Reconciled,response.Outcome);
			Assert.AreEqual(3,fakeHttpHandler.Requests.Count);
			Assert.AreEqual("/sandbox/payments/transactions/status",fakeHttpHandler.Requests[2].RequestUri.AbsolutePath);
			Assert.AreEqual("?referenceId=R-NEW",fakeHttpHandler.Requests[2].RequestUri.Query);
			PayConnectResponse payConnectResponse=PayConnect2.ApiResponseToPayConnectResponse(response);
			Assert.AreEqual("0",payConnectResponse.StatusCode);
			Assert.AreEqual(25m,payConnectResponse.Amount);
			Assert.AreEqual(0.75m,payConnectResponse.AmountSurcharged);
			Assert.AreEqual("1111",payConnectResponse.CardNumber);
			Assert.AreEqual(PayConnectResponse.TransactionType.Sale,payConnectResponse.TransType);
		}

		[TestMethod]
		public void LostCreateResponse_FallbackParsedMatchWithoutReferenceIdIsReReadByTransactionId() {
			FakeHttpHandler fakeHttpHandler=UseFakeHandler();
			fakeHttpHandler.Enqueue((request,ct) => throw MakeNetworkException(WebExceptionStatus.ReceiveFailure));
			fakeHttpHandler.Enqueue(HttpStatusCode.OK,"[{\"transactionId\":4321,\"invoiceNumber\":\"OPMS-tid000000001\",\"status\":\"approved\","
				+"\"type\":\"SomethingNew\"}]");
			fakeHttpHandler.Enqueue(HttpStatusCode.OK,MakeTransactionJson("R-TID","OPMS-tid000000001","Approved"));
			PayConnect2.PayConnect2Response response=PostLostCreate("OPMS-tid000000001");
			Assert.AreEqual(PayConnect2.ResponseType.GetStatus,response.ResponseType);
			Assert.AreEqual("?transactionId=4321",fakeHttpHandler.Requests[2].RequestUri.Query);
			Assert.AreEqual("R-TID",response.GetStatusResponse.ReferenceId);
		}

		[TestMethod]
		public void LostCreateResponse_FallbackParsedMatchWhoseStatusReadFailsIsNeverASuccess() {
			FakeHttpHandler fakeHttpHandler=UseFakeHandler();
			fakeHttpHandler.Enqueue((request,ct) => throw MakeNetworkException(WebExceptionStatus.ReceiveFailure));
			fakeHttpHandler.Enqueue(HttpStatusCode.OK,"[{\"referenceId\":\"R-NEW\",\"invoiceNumber\":\"OPMS-new000000002\",\"status\":\"Processed\","
				+"\"type\":\"SomethingNew\"}]");
			fakeHttpHandler.Enqueue(HttpStatusCode.NotFound,"{\"errorType\":\"RESPONSE\",\"error\":{\"message\":\"Not found\"}}");
			PayConnect2.PayConnect2Response response=PostLostCreate("OPMS-new000000002");
			Assert.AreEqual(PayConnect2.ResponseType.Error,response.ResponseType);
			Assert.AreEqual(PayConnect2.PayConnectRequestOutcome.SentNoResponse,response.Outcome);
			StringAssert.Contains(response.ErrorResponse.Message,"OPMS-new000000002");
			StringAssert.Contains(response.ErrorResponse.Message,"may or may not");
			Assert.AreNotEqual("0",PayConnect2.ApiResponseToPayConnectResponse(response).StatusCode);
		}

		[TestMethod]
		public void IsApprovedStatus_OnlyApprovals() {
			Assert.IsTrue(PayConnect2.IsApprovedStatus("Processed"));
			Assert.IsTrue(PayConnect2.IsApprovedStatus("AUTHORIZED"));
			Assert.IsTrue(PayConnect2.IsApprovedStatus(" approved "));
			Assert.IsFalse(PayConnect2.IsApprovedStatus("Declined"));
			Assert.IsFalse(PayConnect2.IsApprovedStatus("Error"));
			Assert.IsFalse(PayConnect2.IsApprovedStatus("Voided"));
			Assert.IsFalse(PayConnect2.IsApprovedStatus(""));
			Assert.IsFalse(PayConnect2.IsApprovedStatus(null));
		}

		[TestMethod]
		public void LostCreateResponse_IsFoundByInvoiceNumberAndNotResent() {
			FakeHttpHandler fakeHttpHandler=UseFakeHandler();
			fakeHttpHandler.Enqueue((request,ct) => throw MakeNetworkException(WebExceptionStatus.ReceiveFailure));
			fakeHttpHandler.Enqueue(HttpStatusCode.OK,"{\"transactions\":["+MakeTransactionJson("R-OTHER","OPMS-000000000000")+","
				+MakeTransactionJson("R-FOUND","OPMS-abcdef012345")+"],\"totalPages\":1}");
			fakeHttpHandler.Enqueue(HttpStatusCode.OK,MakeTransactionJson("R-FOUND","OPMS-abcdef012345"));
			PayConnect2.CreateTransactionRequest createTransactionRequest=new PayConnect2.CreateTransactionRequest();
			createTransactionRequest.CardToken="tok";
			createTransactionRequest.Amount=2500;
			createTransactionRequest.InvoiceNumber="OPMS-abcdef012345";
			PayConnect2.PayConnect2Response response=PayConnect2.PostCreateTransaction(createTransactionRequest,PayConnect2.MakeCredentials(API_KEY,SECRET,true));
			Assert.AreEqual(PayConnect2.PayConnectRequestOutcome.Reconciled,response.Outcome);
			Assert.AreEqual(PayConnect2.ResponseType.GetStatus,response.ResponseType);
			Assert.AreEqual("R-FOUND",response.GetStatusResponse.ReferenceId);
			Assert.AreEqual("OPMS-abcdef012345",response.CorrelationId);
			List<RecordedRequest> listRequests=fakeHttpHandler.Requests;
			Assert.AreEqual(3,listRequests.Count);
			Assert.AreEqual(1,listRequests.Count(x => x.Method==HttpMethod.Post),"A POST that moves money is never retried.");
			Assert.AreEqual("/sandbox/payments/transactions/status",listRequests[2].RequestUri.AbsolutePath);
			Assert.AreEqual("?referenceId=R-FOUND",listRequests[2].RequestUri.Query);
			StringAssert.Contains(listRequests[0].Body,"\"invoiceNumber\":\"OPMS-abcdef012345\"");
			Assert.AreEqual("/sandbox/payments/transactions/list",listRequests[1].RequestUri.AbsolutePath);
			StringAssert.Contains(listRequests[1].RequestUri.Query,"dateFrom=2026-10-18");
			StringAssert.Contains(listRequests[1].RequestUri.Query,"dateTo=2026-10-19");
			StringAssert.Contains(listRequests[1].RequestUri.Query,"page=1");
			PayConnectResponse payConnectResponse=PayConnect2.ApiResponseToPayConnectResponse(response);
			Assert.AreEqual("0",payConnectResponse.StatusCode);
			Assert.AreEqual("R-FOUND",payConnectResponse.RefNumber);
		}

		[TestMethod]
		public void LostCreateResponse_NotFoundNamesTheInvoiceNumber() {
			FakeHttpHandler fakeHttpHandler=UseFakeHandler();
			fakeHttpHandler.Enqueue((request,ct) => throw MakeNetworkException(WebExceptionStatus.ReceiveFailure));
			fakeHttpHandler.Enqueue(HttpStatusCode.OK,"[]");
			PayConnect2.CreateTransactionRequest createTransactionRequest=new PayConnect2.CreateTransactionRequest();
			createTransactionRequest.InvoiceNumber="OPMS-111111111111";
			PayConnect2.PayConnect2Response response=PayConnect2.PostCreateTransaction(createTransactionRequest,PayConnect2.MakeCredentials(API_KEY,SECRET,true));
			Assert.AreEqual(PayConnect2.ResponseType.Error,response.ResponseType);
			Assert.AreEqual(PayConnect2.PayConnectRequestOutcome.SentNoResponse,response.Outcome);
			StringAssert.Contains(response.ErrorResponse.Message,"OPMS-111111111111");
			StringAssert.Contains(response.ErrorResponse.Message,"may or may not");
			Assert.AreEqual(1,fakeHttpHandler.Requests.Count(x => x.Method==HttpMethod.Post));
		}

		///<summary>A 2xx create answer whose source this model doesn't know: the card may well have been charged.</summary>
		private const string UNREADABLE_CREATE_BODY="{\"referenceId\":\"R-UNR\",\"status\":\"Processed\",\"source\":\"NewValue\",\"type\":\"Sale\","
			+"\"amount\":2500,\"invoiceNumber\":\"OPMS-unr000000001\"}";

		[TestMethod]
		public void ParseHttpResponse_Unreadable2xxIsNotAPlainError() {
			PayConnect2.PayConnect2Response response=PayConnect2.ParseHttpResponse(PayConnect2.ApiRoute.CreateTransaction,HttpStatusCode.OK,
				UNREADABLE_CREATE_BODY);
			Assert.AreEqual(PayConnect2.ResponseType.Error,response.ResponseType);
			Assert.AreEqual(PayConnect2.PayConnectRequestOutcome.ResponseUnreadable,response.Outcome);
			Assert.AreEqual(PayConnect2.PayConnectErrorKind.NoAnswer,response.ErrorResponse.Kind);
			StringAssert.Contains(response.ErrorResponse.Message,"may or may not");
			Assert.IsFalse(response.ErrorResponse.Message.Contains("R-UNR"),"The body is never put in the message.");
		}

		[TestMethod]
		public void UnreadableCreateResponse_NotFoundWarnsAndNamesTheInvoiceNumber() {
			FakeHttpHandler fakeHttpHandler=UseFakeHandler();
			fakeHttpHandler.Enqueue(HttpStatusCode.OK,UNREADABLE_CREATE_BODY);
			fakeHttpHandler.Enqueue(HttpStatusCode.OK,"[]");
			PayConnect2.PayConnect2Response response=PostLostCreate("OPMS-unr000000001");
			Assert.AreEqual(PayConnect2.ResponseType.Error,response.ResponseType);
			Assert.AreNotEqual(PayConnect2.PayConnectRequestOutcome.Response,response.Outcome,"Never an ordinary decline.");
			Assert.AreEqual(PayConnect2.PayConnectRequestOutcome.ResponseUnreadable,response.Outcome);
			Assert.AreEqual("OPMS-unr000000001",response.CorrelationId);
			StringAssert.Contains(response.ErrorResponse.Message,"OPMS-unr000000001");
			StringAssert.Contains(response.ErrorResponse.Message,"may or may not");
			StringAssert.StartsWith(response.ErrorResponse.Message,"PayConnect answered, but its answer could not be read");
			Assert.AreEqual(2,fakeHttpHandler.Requests.Count);
			Assert.AreEqual(1,fakeHttpHandler.Requests.Count(x => x.Method==HttpMethod.Post),"The create is never resent.");
			Assert.AreEqual("/sandbox/payments/transactions",fakeHttpHandler.Requests[0].RequestUri.AbsolutePath);
			Assert.AreEqual("/sandbox/payments/transactions/list",fakeHttpHandler.Requests[1].RequestUri.AbsolutePath);
			Assert.AreNotEqual("0",PayConnect2.ApiResponseToPayConnectResponse(response).StatusCode);
		}

		[TestMethod]
		public void UnreadableCreateResponse_ListLookupFailureStillWarns() {
			FakeHttpHandler fakeHttpHandler=UseFakeHandler();
			fakeHttpHandler.Enqueue(HttpStatusCode.OK,UNREADABLE_CREATE_BODY);
			fakeHttpHandler.Enqueue(HttpStatusCode.NotFound,"{\"errorType\":\"RESPONSE\",\"error\":{\"message\":\"Not found\"}}");
			PayConnect2.PayConnect2Response response=PostLostCreate("OPMS-unr000000002");
			Assert.AreEqual(PayConnect2.PayConnectRequestOutcome.ResponseUnreadable,response.Outcome);
			StringAssert.Contains(response.ErrorResponse.Message,"could not be checked");
			StringAssert.Contains(response.ErrorResponse.Message,"OPMS-unr000000002");
			Assert.AreEqual(1,fakeHttpHandler.Requests.Count(x => x.Method==HttpMethod.Post),"The create is never resent.");
		}

		[TestMethod]
		public void UnreadableCreateResponse_ApprovedListMatchIsReconciled() {
			FakeHttpHandler fakeHttpHandler=UseFakeHandler();
			fakeHttpHandler.Enqueue(HttpStatusCode.OK,UNREADABLE_CREATE_BODY);
			fakeHttpHandler.Enqueue(HttpStatusCode.OK,"["+MakeTransactionJson("R-UNR","OPMS-unr000000003")+"]");
			fakeHttpHandler.Enqueue(HttpStatusCode.OK,MakeTransactionJson("R-UNR","OPMS-unr000000003"));
			PayConnect2.PayConnect2Response response=PostLostCreate("OPMS-unr000000003");
			Assert.AreEqual(PayConnect2.ResponseType.GetStatus,response.ResponseType);
			Assert.AreEqual(PayConnect2.PayConnectRequestOutcome.Reconciled,response.Outcome);
			Assert.AreEqual(1,fakeHttpHandler.Requests.Count(x => x.Method==HttpMethod.Post),"The create is never resent.");
			PayConnectResponse payConnectResponse=PayConnect2.ApiResponseToPayConnectResponse(response);
			Assert.AreEqual("0",payConnectResponse.StatusCode);
			Assert.AreEqual("R-UNR",payConnectResponse.RefNumber);
			Assert.AreEqual(25m,payConnectResponse.Amount);
		}

		[TestMethod]
		public void UnreadableRefundResponse_IsNotAPlainError() {
			PayConnect2.PayConnect2Response response=PayConnect2.ParseHttpResponse(PayConnect2.ApiRoute.RefundTransaction,HttpStatusCode.OK,"not json");
			Assert.AreEqual(PayConnect2.PayConnectRequestOutcome.ResponseUnreadable,response.Outcome);
			StringAssert.Contains(response.ErrorResponse.Message,"may or may not");
		}

		[TestMethod]
		public void CreateNotSent_IsNotReconciled() {
			FakeHttpHandler fakeHttpHandler=UseFakeHandler();
			fakeHttpHandler.Enqueue((request,ct) => throw MakeNetworkException(WebExceptionStatus.NameResolutionFailure));
			PayConnect2.CreateTransactionRequest createTransactionRequest=new PayConnect2.CreateTransactionRequest();
			PayConnect2.PayConnect2Response response=PayConnect2.PostCreateTransaction(createTransactionRequest,PayConnect2.MakeCredentials(API_KEY,SECRET,true));
			Assert.AreEqual(PayConnect2.PayConnectRequestOutcome.NotSent,response.Outcome);
			StringAssert.StartsWith(createTransactionRequest.InvoiceNumber,"OPMS-","A correlation ID is always sent.");
			Assert.AreEqual(1,fakeHttpHandler.Requests.Count,"Nothing left, so there is nothing to look up.");
		}

		[TestMethod]
		public void FindTransactionByInvoiceNumber_PagesUntilFound() {
			FakeHttpHandler fakeHttpHandler=UseFakeHandler();
			StringBuilder stringBuilderPage1=new StringBuilder("[");
			for(int i=0;i<100;i++) {
				if(i>0) {
					stringBuilderPage1.Append(",");
				}
				stringBuilderPage1.Append(MakeTransactionJson("R"+i,"OPMS-"+i.ToString("000000000000")));
			}
			stringBuilderPage1.Append("]");
			fakeHttpHandler.Enqueue(HttpStatusCode.OK,stringBuilderPage1.ToString());
			fakeHttpHandler.Enqueue(HttpStatusCode.OK,"["+MakeTransactionJson("R-PAGE2","OPMS-fedcba987654")+"]");
			bool didLookupFail;
			PayConnect2.GetStatusResponse transaction=PayConnect2.FindTransactionByInvoiceNumber(PayConnect2.MakeCredentials(API_KEY,SECRET,false),
				"OPMS-fedcba987654",out didLookupFail);
			Assert.IsFalse(didLookupFail);
			Assert.IsNotNull(transaction);
			Assert.AreEqual("R-PAGE2",transaction.ReferenceId);
			StringAssert.Contains(fakeHttpHandler.Requests[1].RequestUri.Query,"page=2");
		}

		[TestMethod]
		public void FindTransactionByInvoiceNumber_LookupFailureIsReported() {
			FakeHttpHandler fakeHttpHandler=UseFakeHandler();
			fakeHttpHandler.Enqueue(HttpStatusCode.Unauthorized,"{\"error\":\"Authorization field missing\"}");
			bool didLookupFail;
			PayConnect2.GetStatusResponse transaction=PayConnect2.FindTransactionByInvoiceNumber(PayConnect2.MakeCredentials(API_KEY,SECRET,false),
				"OPMS-fedcba987654",out didLookupFail);
			Assert.IsNull(transaction);
			Assert.IsTrue(didLookupFail);
		}

		[TestMethod]
		public void ParseTransactionList_KeepsEntriesWithUnknownValues() {
			PayConnect2.TransactionListResponse transactionListResponse=PayConnect2.ParseTransactionList(
				"{\"data\":[{\"referenceId\":\"R1\",\"invoiceNumber\":\"OPMS-1\",\"type\":\"SomethingNew\",\"source\":\"Elsewhere\"}],\"totalPages\":3}");
			Assert.AreEqual(3,transactionListResponse.TotalPages);
			Assert.AreEqual(1,transactionListResponse.ListTransactions.Count);
			Assert.AreEqual("OPMS-1",transactionListResponse.ListTransactions[0].InvoiceNumber);
		}
		#endregion Reconciliation

		#region Hosted card-entry page
		[TestMethod]
		public void IsAllowedHostedFormOrigin_AcceptsDentalXChangeOnly() {
			Assert.IsTrue(PayConnect2.IsAllowedHostedFormOrigin("https://dentalxchange.com"));
			Assert.IsTrue(PayConnect2.IsAllowedHostedFormOrigin("https://pay.dentalxchange.com"));
			Assert.IsTrue(PayConnect2.IsAllowedHostedFormOrigin("https://a.b.DentalXChange.com:443"));
			Assert.IsFalse(PayConnect2.IsAllowedHostedFormOrigin("http://pay.dentalxchange.com"),"https only");
			Assert.IsFalse(PayConnect2.IsAllowedHostedFormOrigin("https://dentalxchange.com.evil.test"));
			Assert.IsFalse(PayConnect2.IsAllowedHostedFormOrigin("https://evildentalxchange.com"));
			Assert.IsFalse(PayConnect2.IsAllowedHostedFormOrigin("https://www.example.com"));
			Assert.IsFalse(PayConnect2.IsAllowedHostedFormOrigin("null"));
			Assert.IsFalse(PayConnect2.IsAllowedHostedFormOrigin(""));
			Assert.IsFalse(PayConnect2.IsAllowedHostedFormOrigin(null));
			Assert.IsFalse(PayConnect2.IsAllowedHostedFormOrigin("https://pay.dentalxchange.com/path"),"An origin has no path.");
			Assert.IsFalse(PayConnect2.IsAllowedHostedFormOrigin("https://user@pay.dentalxchange.com"));
		}

		[TestMethod]
		public void IsAllowedHostedFormUrl_AllowsPathsOnDentalXChangeOnly() {
			Assert.IsTrue(PayConnect2.IsAllowedHostedFormUrl("https://embed.dentalxchange.com/session/abc?x=1"));
			Assert.IsFalse(PayConnect2.IsAllowedHostedFormUrl("http://embed.dentalxchange.com/session/abc"));
			Assert.IsFalse(PayConnect2.IsAllowedHostedFormUrl("https://example.test/session"));
			Assert.IsFalse(PayConnect2.IsAllowedHostedFormUrl("https://localhost/session"));
			Assert.IsFalse(PayConnect2.IsAllowedHostedFormUrl("javascript:alert(1)"));
			Assert.IsFalse(PayConnect2.IsAllowedHostedFormUrl("about:blank"));
		}

		[TestMethod]
		public void ParseHostedFormMessage_ReadsTheRelayEnvelope() {
			string json="{\"origin\":\"https://pay.dentalxchange.com\",\"data\":{\"status\":\"SUCCESS\",\"response\":{\"cardToken\":\"T1\","
				+"\"referenceId\":\"R1\",\"expiryMonth\":\"01\",\"expiryYear\":\"2030\"}}}";
			PayConnect2.HostedFormMessage hostedFormMessage=PayConnect2.ParseHostedFormMessage(json);
			Assert.IsNotNull(hostedFormMessage);
			Assert.IsTrue(hostedFormMessage.IsFromAllowedOrigin);
			Assert.AreEqual("SUCCESS",hostedFormMessage.Status);
			Assert.AreEqual("R1",hostedFormMessage.Response.Response.ReferenceId);
		}

		[TestMethod]
		public void ParseHostedFormMessage_AcceptsJsonTextData() {
			string json="{\"origin\":\"https://pay.dentalxchange.com\",\"data\":\"{\\\"status\\\":\\\"session_expired\\\"}\"}";
			PayConnect2.HostedFormMessage hostedFormMessage=PayConnect2.ParseHostedFormMessage(json);
			Assert.IsNotNull(hostedFormMessage);
			Assert.AreEqual("SESSION_EXPIRED",hostedFormMessage.Status);
		}

		[TestMethod]
		public void ParseHostedFormMessage_FlagsOtherOriginsAndRejectsJunk() {
			PayConnect2.HostedFormMessage hostedFormMessage=PayConnect2.ParseHostedFormMessage(
				"{\"origin\":\"https://dentalxchange.com.evil.test\",\"data\":{\"status\":\"SUCCESS\"}}");
			Assert.IsNotNull(hostedFormMessage);
			Assert.IsFalse(hostedFormMessage.IsFromAllowedOrigin,"A look-alike origin is never trusted.");
			Assert.IsNull(PayConnect2.ParseHostedFormMessage("{\"status\":\"SUCCESS\"}"),"The old, unwrapped shape carries no origin.");
			Assert.IsNull(PayConnect2.ParseHostedFormMessage("{\"origin\":\"https://pay.dentalxchange.com\",\"data\":42}"));
			Assert.IsNull(PayConnect2.ParseHostedFormMessage("not json"));
			Assert.IsNull(PayConnect2.ParseHostedFormMessage(""));
		}

		[TestMethod]
		public void RelayScript_FiltersOnOrigin() {
			string script=PayConnect2.GetHostedFormRelayScript();
			StringAssert.Contains(script,"'dentalxchange.com'");
			StringAssert.Contains(script,"e.origin");
			StringAssert.Contains(script,"origin:e.origin");
			Assert.IsFalse(script.Contains("postMessage(e.data)"),"The old relay forwarded every message from any origin.");
		}

		[TestMethod]
		public void CardNumberChecks() {
			Assert.IsTrue(PayConnect2.IsTrackTwoData(";4111111111111111=30121011000?"));
			Assert.IsFalse(PayConnect2.IsTrackTwoData("tok_123"));
			Assert.IsFalse(PayConnect2.IsTrackTwoData(null));
			Assert.IsTrue(PayConnect2.LooksLikeCardNumber("4111111111111111"));
			Assert.IsTrue(PayConnect2.LooksLikeCardNumber("378282246310005"));
			Assert.IsFalse(PayConnect2.LooksLikeCardNumber("4111111111111112"),"Fails Luhn.");
			Assert.IsFalse(PayConnect2.LooksLikeCardNumber("411111111111"),"Too short.");
			Assert.IsFalse(PayConnect2.LooksLikeCardNumber("41111111111111A1"));
			Assert.IsFalse(PayConnect2.LooksLikeCardNumber(""));
		}
		#endregion Hosted card-entry page

		#region Setup rows
		[TestMethod]
		public void AddMissingProperties_AddsOnceAndRaisesFlags() {
			List<ProgramProperty> listProgramProperties=new List<ProgramProperty>();
			ProgramProperty programPropertyCopied=new ProgramProperty();
			programPropertyCopied.ProgramPropertyNum=55;
			programPropertyCopied.ProgramNum=9;
			programPropertyCopied.PropertyDesc=OpenDentBusiness.PayConnect.ProgramProperties.ApiSecret;
			programPropertyCopied.PropertyValue="legacy-plain";
			programPropertyCopied.ClinicNum=5;
			listProgramProperties.Add(programPropertyCopied);//As InsertForClinic used to copy it: no flags.
			int countAdded=PayConnect2.AddMissingProperties(listProgramProperties,9,new List<long> { 0,5,7 });
			Assert.AreEqual(4,countAdded,"API Key, Use Sandbox, and the Secret for ClinicNum 0 and clinic 7.");
			Assert.AreEqual(0,PayConnect2.AddMissingProperties(listProgramProperties,9,new List<long> { 0,5,7 }),"Idempotent.");
			Assert.AreEqual(1,listProgramProperties.Count(x => x.PropertyDesc==OpenDentBusiness.PayConnect.ProgramProperties.ApiKey));
			Assert.AreEqual(0,listProgramProperties.Single(x => x.PropertyDesc==OpenDentBusiness.PayConnect.ProgramProperties.ApiKey).ClinicNum);
			Assert.AreEqual("0",listProgramProperties.Single(x => x.PropertyDesc==OpenDentBusiness.PayConnect.ProgramProperties.UseSandbox).PropertyValue);
			List<ProgramProperty> listSecrets=listProgramProperties.FindAll(x => x.PropertyDesc==OpenDentBusiness.PayConnect.ProgramProperties.ApiSecret);
			CollectionAssert.AreEquivalent(new List<long> { 0,5,7 },listSecrets.Select(x => x.ClinicNum).ToList());
			Assert.IsTrue(listSecrets.All(x => x.IsMasked && x.IsHighSecurity),"Copied rows get their flags back.");
			Assert.AreEqual("legacy-plain",programPropertyCopied.PropertyValue,"Existing values are never changed here.");
			Assert.IsTrue(listProgramProperties.Where(x => x.ProgramPropertyNum!=55).All(x => x.ProgramPropertyNum==0 && x.ProgramNum==9),
				"New rows are in memory only (Sync inserts them on Save).");
		}
		#endregion Setup rows
	}
}
