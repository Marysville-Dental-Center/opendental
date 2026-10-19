// Tests for the QuickBooks Online Accounting API client: the deposit JSON, the idempotent retries, Fault parsing, 401 handling and paging.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using CodeBase.VendorDirect;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using OpenDentBusiness.Qbo;

namespace VendorDirectTests.Qbo {
	[TestClass]
	public class QboApiClientTests {
		private static T Run<T>(Func<Task<T>> funcAsync) {
			return Task.Run(funcAsync).GetAwaiter().GetResult();
		}

		private static QboTestKit CreateKitWithToken() {
			QboTestKit kit=new QboTestKit("rt-1");
			kit.HandlerOAuth.Enqueue(HttpStatusCode.OK,QboTestKit.TokenJson("at-1","rt-1"));
			return kit;
		}

		#region Deposit JSON
		[TestMethod]
		public void DepositJson_OneLinePerAmount_RoundedToCents_WithClass() {
			QboDepositRequest depositRequest=QboTestKit.SampleDeposit("5000");
			depositRequest.ListAmounts=new List<decimal> { 12.345m,0.005m,-3.5m };
			JObject jObject=QboApiClient.BuildDepositJson(depositRequest);
			Assert.AreEqual("2026-10-19",jObject.Value<string>("TxnDate"));
			Assert.AreEqual("Batch 12",jObject.Value<string>("PrivateNote"));
			Assert.AreEqual("35",jObject["DepositToAccountRef"].Value<string>("value"));
			JArray jArrayLines=(JArray)jObject["Line"];
			Assert.AreEqual(3,jArrayLines.Count);
			CollectionAssert.AreEqual(new[] { 12.35m,0.01m,-3.5m },jArrayLines.Select(x => x.Value<decimal>("Amount")).ToArray());
			foreach(JToken jTokenLine in jArrayLines) {
				Assert.AreEqual("DepositLineDetail",jTokenLine.Value<string>("DetailType"));
				Assert.AreEqual("79",jTokenLine["DepositLineDetail"]["AccountRef"].Value<string>("value"));
				Assert.AreEqual("5000",jTokenLine["DepositLineDetail"]["ClassRef"].Value<string>("value"));
			}
		}

		[TestMethod]
		public void DepositJson_NoClassRefWithoutAClass_MemoTruncated_BlankMemoLeftOut() {
			QboDepositRequest depositRequest=QboTestKit.SampleDeposit("");
			depositRequest.Memo=new string('m',4100);
			JObject jObject=QboApiClient.BuildDepositJson(depositRequest);
			Assert.AreEqual(4000,jObject.Value<string>("PrivateNote").Length);
			Assert.IsNull(jObject["Line"][0]["DepositLineDetail"]["ClassRef"]);
			depositRequest.Memo="";
			Assert.IsNull(QboApiClient.BuildDepositJson(depositRequest)["PrivateNote"]);
		}

		[TestMethod]
		public void DepositJson_CarriesNoPatientData() {
			string json=QboApiClient.BuildDepositJson(QboTestKit.SampleDeposit("5000")).ToString();
			JObject jObject=JObject.Parse(json);
			CollectionAssert.AreEquivalent(new[] { "TxnDate","PrivateNote","DepositToAccountRef","Line" },jObject.Properties().Select(x => x.Name).ToArray());
		}
		#endregion

		#region Create deposit
		[TestMethod]
		public void CreateDeposit_PostsWithRequestIdAndMinorVersion_AndReadsTheId() {
			QboTestKit kit=CreateKitWithToken();
			kit.HandlerApi.Enqueue(HttpStatusCode.OK,QboTestKit.DepositJson("148",true));
			QboDepositResult result=Run(() => kit.ApiClient.CreateDepositAsync(QboTestKit.SampleDeposit("5000"),"req-1"));
			Assert.AreEqual("148",result.DepositId);
			Assert.IsFalse(result.IsClassRefMissing);
			RecordedRequest request=kit.HandlerApi.Requests.Single();
			Assert.AreEqual("POST",request.Method.Method);
			Assert.AreEqual("/v3/company/"+QboTestKit.RealmId+"/deposit",request.RequestUri.AbsolutePath);
			Dictionary<string,string> dictQuery=OAuthRedirectParser.ParseQuery(request.RequestUri.Query);
			Assert.AreEqual("req-1",dictQuery["requestid"]);
			Assert.AreEqual("75",dictQuery["minorversion"]);
			Assert.AreEqual("Bearer at-1",request.GetHeader("Authorization"));
			Assert.AreEqual("application/json",request.ContentType);
			Assert.IsFalse(string.Join("\n",kit.ListLogLines).Contains("Batch 12"),"Memos are never logged.");
			Assert.IsFalse(string.Join("\n",kit.ListLogLines).Contains("at-1"),"Tokens are never logged.");
		}

		[TestMethod]
		public void CreateDeposit_ClassDroppedByQuickBooksOnline_IsReported() {
			QboTestKit kit=CreateKitWithToken();
			kit.HandlerApi.Enqueue(HttpStatusCode.OK,QboTestKit.DepositJson("149",false));
			QboDepositResult result=Run(() => kit.ApiClient.CreateDepositAsync(QboTestKit.SampleDeposit("5000"),"req-1"));
			Assert.IsTrue(result.IsClassRefMissing);
			kit.HandlerApi.Enqueue(HttpStatusCode.OK,QboTestKit.DepositJson("150",false));
			result=Run(() => kit.ApiClient.CreateDepositAsync(QboTestKit.SampleDeposit(""),"req-2"));
			Assert.IsFalse(result.IsClassRefMissing,"No class sent, nothing missing.");
		}

		[TestMethod]
		public void CreateDeposit_RetriesA5xxWithTheSameRequestId() {
			QboTestKit kit=CreateKitWithToken();
			kit.HandlerApi.Enqueue(HttpStatusCode.ServiceUnavailable,"");
			kit.HandlerApi.Enqueue(HttpStatusCode.BadGateway,"");
			kit.HandlerApi.Enqueue(HttpStatusCode.OK,QboTestKit.DepositJson("151",false));
			QboDepositResult result=Run(() => kit.ApiClient.CreateDepositAsync(QboTestKit.SampleDeposit(),"req-same"));
			Assert.AreEqual("151",result.DepositId);
			Assert.AreEqual(3,kit.HandlerApi.Requests.Count);
			Assert.IsTrue(kit.HandlerApi.Requests.All(x => OAuthRedirectParser.ParseQuery(x.RequestUri.Query)["requestid"]=="req-same"));
			Assert.IsTrue(kit.HandlerApi.Requests.All(x => x.Body==kit.HandlerApi.Requests[0].Body));
		}

		[TestMethod]
		public void CreateDeposit_LostResponse_RetriesWithTheSameRequestId() {
			QboTestKit kit=CreateKitWithToken();
			kit.HandlerApi.EnqueueNetworkFailure();
			kit.HandlerApi.Enqueue(HttpStatusCode.OK,QboTestKit.DepositJson("152",false));
			QboDepositResult result=Run(() => kit.ApiClient.CreateDepositAsync(QboTestKit.SampleDeposit(),"req-lost"));
			Assert.AreEqual("152",result.DepositId);
			Assert.AreEqual(2,kit.HandlerApi.Requests.Count);
			Assert.AreEqual("req-lost",OAuthRedirectParser.ParseQuery(kit.HandlerApi.Requests[1].RequestUri.Query)["requestid"]);
		}

		[TestMethod]
		public void CreateDeposit_GivesUpAfterTwoRetries_AsUnreachable() {
			QboTestKit kit=CreateKitWithToken();
			kit.HandlerApi.EnqueueNetworkFailure().EnqueueNetworkFailure().EnqueueNetworkFailure();
			QboApiException ex=AssertEx.Throws<QboApiException>(() => Run(() => kit.ApiClient.CreateDepositAsync(QboTestKit.SampleDeposit(),"req-x")));
			Assert.AreEqual(QboApiException.MessageUnreachable,ex.Message);
			Assert.IsTrue(ex.IsNetworkFailure);
			Assert.AreEqual(3,kit.HandlerApi.Requests.Count);
		}

		[TestMethod]
		public void CreateDeposit_ValidationFault_ShowsMessageAndDetail_WithoutRetry() {
			QboTestKit kit=CreateKitWithToken();
			kit.HandlerApi.Enqueue((request,ct) => Task.FromResult(QboTestKit.WithTid(FakeHttpHandler.MakeResponse(HttpStatusCode.BadRequest,
				QboTestKit.FaultJson("ValidationFault","Invalid Reference Id","Accounts element id 99 not found","2500")),"tid-7")));
			QboApiException ex=AssertEx.Throws<QboApiException>(() => Run(() => kit.ApiClient.CreateDepositAsync(QboTestKit.SampleDeposit(),"req-v")));
			StringAssert.Contains(ex.Message,"Invalid Reference Id");
			StringAssert.Contains(ex.Message,"Accounts element id 99 not found");
			Assert.AreEqual("ValidationFault",ex.FaultType);
			Assert.AreEqual("2500",ex.Code);
			Assert.AreEqual("tid-7",ex.IntuitTid);
			Assert.AreEqual(400,ex.HttpStatus);
			Assert.AreEqual(1,kit.HandlerApi.Requests.Count);
		}

		[TestMethod]
		public void CreateDeposit_RejectsAMissingOrLongRequestId() {
			QboTestKit kit=CreateKitWithToken();
			AssertEx.Throws<ArgumentException>(() => Run(() => kit.ApiClient.CreateDepositAsync(QboTestKit.SampleDeposit(),"")));
			AssertEx.Throws<ArgumentException>(() => Run(() => kit.ApiClient.CreateDepositAsync(QboTestKit.SampleDeposit(),new string('a',51))));
			Assert.AreEqual(32,QuickBooksOnline.NewRequestId().Length);
		}
		#endregion

		#region 401, faults, throttling
		[TestMethod]
		public void Api401_RefreshesOnceAndRetriesOnce() {
			QboTestKit kit=CreateKitWithToken();
			kit.HandlerOAuth.Enqueue(HttpStatusCode.OK,QboTestKit.TokenJson("at-2","rt-1"));
			kit.HandlerApi.Enqueue(HttpStatusCode.Unauthorized,QboTestKit.FaultJson("AuthenticationFault","Token expired","","3200"));
			kit.HandlerApi.Enqueue(HttpStatusCode.OK,"{\"CompanyInfo\":{\"CompanyName\":\"Sandbox Company_US_1\"}}");
			Assert.AreEqual("Sandbox Company_US_1",Run(() => kit.ApiClient.GetCompanyNameAsync()));
			Assert.AreEqual(2,kit.HandlerOAuth.Requests.Count);
			Assert.AreEqual("Bearer at-2",kit.HandlerApi.Requests[1].GetHeader("Authorization"));
			Assert.AreEqual("/v3/company/"+QboTestKit.RealmId+"/companyinfo/"+QboTestKit.RealmId,kit.HandlerApi.Requests[1].RequestUri.AbsolutePath);
		}

		[TestMethod]
		public void Api401Twice_NeedsReconnect_WithoutLooping() {
			QboTestKit kit=CreateKitWithToken();
			kit.HandlerOAuth.Enqueue(HttpStatusCode.OK,QboTestKit.TokenJson("at-2","rt-1"));
			kit.HandlerApi.SetDefault((request,ct) => Task.FromResult(FakeHttpHandler.MakeResponse(HttpStatusCode.Unauthorized,"")));
			QboAuthException ex=AssertEx.Throws<QboAuthException>(() => Run(() => kit.ApiClient.GetCompanyNameAsync()));
			Assert.AreEqual(QboAuthException.MessageReconnect,ex.Message);
			Assert.AreEqual(2,kit.HandlerApi.Requests.Count);
			Assert.AreEqual(2,kit.HandlerOAuth.Requests.Count);
		}

		[TestMethod]
		public void Throttled_SaysBusy() {
			QboTestKit kit=CreateKitWithToken();
			kit.HandlerApi.Enqueue(HttpStatusCode.OK,QboTestKit.QueryJson("Class",0));
			kit.HandlerApi.Enqueue((HttpStatusCode)429,QboTestKit.FaultJson("ThrottleExceeded","Too many requests","","003001"));
			Run(() => kit.ApiClient.GetClassesAsync());
			QboApiException ex=AssertEx.Throws<QboApiException>(() => Run(() => kit.ApiClient.GetClassesAsync()));
			Assert.AreEqual(QboApiException.MessageThrottled,ex.Message);
		}

		[TestMethod]
		public void ParseFailure_NonJsonBody_UsesTheStatus() {
			Exception ex=QboApiClient.ParseFailure(502,"<html>Bad gateway</html>","");
			Assert.IsInstanceOfType(ex,typeof(QboApiException));
			StringAssert.Contains(ex.Message,"HTTP 502");
			Assert.IsInstanceOfType(QboApiClient.ParseFailure(403,QboTestKit.FaultJson("AuthorizationFault","Not authorized","","003100"),""),
				typeof(QboAuthException));
		}
		#endregion

		#region Queries
		[TestMethod]
		public void Query_PagesUntilAShortPage_AndUsesFullyQualifiedNames() {
			QboTestKit kit=CreateKitWithToken();
			kit.HandlerApi.Enqueue(HttpStatusCode.OK,QboTestKit.QueryJson("Account",1000,1));
			kit.HandlerApi.Enqueue(HttpStatusCode.OK,QboTestKit.QueryJson("Account",3,1001));
			List<QboEntity> listEntities=Run(() => kit.ApiClient.GetAccountsAsync(QboApiClient.ArrayDepositAccountTypes));
			Assert.AreEqual(1003,listEntities.Count);
			Assert.AreEqual("Parent:Child 1003",listEntities.Last().DisplayName);
			Assert.AreEqual(2,kit.HandlerApi.Requests.Count);
			string query1=OAuthRedirectParser.ParseQuery(kit.HandlerApi.Requests[0].RequestUri.Query)["query"];
			string query2=OAuthRedirectParser.ParseQuery(kit.HandlerApi.Requests[1].RequestUri.Query)["query"];
			Assert.AreEqual("select * from Account where AccountType in ('Bank','Other Current Asset') and Active = true STARTPOSITION 1 MAXRESULTS 1000",
				query1);
			StringAssert.Contains(query2,"STARTPOSITION 1001 MAXRESULTS 1000");
			Assert.AreEqual("75",OAuthRedirectParser.ParseQuery(kit.HandlerApi.Requests[0].RequestUri.Query)["minorversion"]);
			Assert.AreEqual("GET",kit.HandlerApi.Requests[0].Method.Method);
		}

		[TestMethod]
		public void Query_EmptyResponse_IsAnEmptyList() {
			QboTestKit kit=CreateKitWithToken();
			kit.HandlerApi.Enqueue(HttpStatusCode.OK,QboTestKit.QueryJson("Class",0));
			Assert.AreEqual(0,Run(() => kit.ApiClient.GetClassesAsync()).Count);
			StringAssert.Contains(OAuthRedirectParser.ParseQuery(kit.HandlerApi.Requests[0].RequestUri.Query)["query"],"select * from Class where Active = true");
		}

		[TestMethod]
		public void ApiClient_RefusesAnUnsafeRealmId() {
			QboTestKit kit=new QboTestKit();
			AssertEx.Throws<QboAuthException>(() => new QboApiClient(new System.Net.Http.HttpClient(new FakeHttpHandler()),QboTestKit.UriApiBase,
				"123/../456",kit.TokenManager));
		}
		#endregion
	}
}
