// V-GP: the Global Payments HTTP client against a fake handler. Checks the requests against Global Payments' public API documentation (access token,
// Cloud Integration device commands, Transactions API), the hosts it may reach, the no-retry rule for money, and that nothing secret is logged.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using CodeBase.VendorDirect;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using OpenDentBusiness.VendorDirect.GlobalPayments;

namespace VGPTests {
	[TestClass]
	public class GpV2ClientTests {
		private const string APP_ID="app-id-synthetic";
		private const string APP_KEY="app-key-synthetic-0001";
		private List<string> _listLogLines;

		private GlobalPaymentsV2Config MakeConfig(bool isSandbox=true,string accountName="") {
			GlobalPaymentsV2Config config=new GlobalPaymentsV2Config();
			config.AppId=APP_ID;
			config.AppKey=APP_KEY;
			config.UseSandbox=isSandbox;
			config.AccountName=accountName;
			config.Fingerprint="test-"+Guid.NewGuid().ToString("N");
			return config;
		}

		private GpV2Client MakeClient(GpFakeHttp gpFakeHttp,Func<DateTime> funcUtcNow=null) {
			_listLogLines=new List<string>();
			return new GpV2Client(gpFakeHttp,funcUtcNow,x => { lock(_listLogLines) { _listLogLines.Add(x); } });
		}

		private static T Run<T>(Func<System.Threading.Tasks.Task<T>> func) {
			return VendorHttp.RunSync(func);
		}

		[TestMethod]
		public void ComputeSecret_IsLowercaseHexSha512OfNonceThenKey() {
			//FIPS 180-2 test vector: SHA-512("abc").
			Assert.AreEqual("ddaf35a193617abacc417349ae20413112e6fa4e89a97ea20a9eeee64b55d39a2192992a274fc1a836ba3c23a3feebbd454d4423643ce80e2a9ac94fa54ca49f",
				GpV2Client.ComputeSecret("ab","c"));
			string expected;
			using(SHA512 sha512=SHA512.Create()) {
				expected=string.Concat(sha512.ComputeHash(Encoding.UTF8.GetBytes("nonce-1"+APP_KEY)).Select(x => x.ToString("x2")));
			}
			Assert.AreEqual(expected,GpV2Client.ComputeSecret("nonce-1",APP_KEY));
			Assert.AreEqual(128,expected.Length);
		}

		[TestMethod]
		public void AccessToken_RequestFollowsThePublicDocumentation_AndNeverSendsTheKey() {
			GpFakeHttp gpFakeHttp=new GpFakeHttp();
			GpV2Client gpV2Client=MakeClient(gpFakeHttp);
			OAuthAccessToken token=Run(() => gpV2Client.GetAccessTokenAsync(MakeConfig(),CancellationToken.None));
			Assert.AreEqual("TOKEN-1",token.Value);
			GpRequest gpRequest=gpFakeHttp.Requests.Single();
			Assert.AreEqual("POST",gpRequest.Method.Method);
			Assert.AreEqual("https://apis.sandbox.globalpay.com/ucp/accesstoken",gpRequest.Uri.ToString());
			Assert.AreEqual("2021-03-22",gpRequest.GetHeader("X-GP-Version"));
			StringAssert.StartsWith(gpRequest.GetHeader("Content-Type"),"application/json");
			JObject jObject=gpRequest.GetJson();
			Assert.AreEqual(APP_ID,(string)jObject["app_id"]);
			Assert.AreEqual("client_credentials",(string)jObject["grant_type"]);
			string nonce=(string)jObject["nonce"];
			Assert.IsTrue(nonce.Length>=1 && nonce.Length<=50,"nonce is 1 to 50 characters.");
			Assert.AreEqual(GpV2Client.ComputeSecret(nonce,APP_KEY),(string)jObject["secret"]);
			Assert.IsFalse(gpRequest.Body.Contains(APP_KEY),"The app key itself is never sent.");
			Assert.IsNull(gpRequest.GetHeader("Authorization"));
		}

		[TestMethod]
		public void AccessToken_NoncesAreUnique() {
			GpV2Client gpV2Client=MakeClient(new GpFakeHttp());
			HashSet<string> hashSetNonces=new HashSet<string>();
			for(int i=0;i<50;i++) {
				Assert.IsTrue(hashSetNonces.Add(gpV2Client.NewNonce()));
			}
		}

		[TestMethod]
		public void AccessToken_IsCachedAndRefreshedBeforeItExpires() {
			DateTime dateTimeNow=new DateTime(2026,10,25,12,0,0,DateTimeKind.Utc);
			GpFakeHttp gpFakeHttp=new GpFakeHttp();
			gpFakeHttp.TokenResponder=x => GpFakeHttp.Make(HttpStatusCode.OK,GpSamples.Token("TOKEN-"+gpFakeHttp.CountTokens,3600));
			GpV2Client gpV2Client=MakeClient(gpFakeHttp,() => dateTimeNow);
			GlobalPaymentsV2Config config=MakeConfig();
			Assert.AreEqual("TOKEN-1",Run(() => gpV2Client.GetAccessTokenAsync(config,CancellationToken.None)).Value);
			dateTimeNow=dateTimeNow.AddMinutes(50);
			Assert.AreEqual("TOKEN-1",Run(() => gpV2Client.GetAccessTokenAsync(config,CancellationToken.None)).Value,"Still well before expiry.");
			dateTimeNow=dateTimeNow.AddMinutes(6);
			Assert.AreEqual("TOKEN-2",Run(() => gpV2Client.GetAccessTokenAsync(config,CancellationToken.None)).Value,"Within 5 minutes of expiry.");
			Assert.AreEqual(2,gpFakeHttp.CountTokens);
		}

		[TestMethod]
		public void AccessToken_RejectedKeyIsCredentialsAndFailsFastAfterwards() {
			GpFakeHttp gpFakeHttp=new GpFakeHttp();
			gpFakeHttp.TokenResponder=x => GpFakeHttp.Make(HttpStatusCode.Forbidden,GpSamples.Error("ACTION_NOT_AUTHORIZED","Invalid app credentials"));
			GpV2Client gpV2Client=MakeClient(gpFakeHttp);
			GlobalPaymentsV2Config config=MakeConfig();
			GpV2Exception gpV2Exception=AssertThrows<GpV2Exception>(() => Run(() => gpV2Client.GetAccessTokenAsync(config,CancellationToken.None)));
			Assert.AreEqual(GpV2ErrorKind.Credentials,gpV2Exception.Kind);
			StringAssert.Contains(gpV2Exception.Message,"Invalid app credentials");
			gpV2Exception=AssertThrows<GpV2Exception>(() => Run(() => gpV2Client.GetAccessTokenAsync(config,CancellationToken.None)));
			Assert.AreEqual(GpV2ErrorKind.Credentials,gpV2Exception.Kind);
			Assert.AreEqual(1,gpFakeHttp.CountTokens,"A rejected configuration isn't sent again until it changes or Test connection resets it.");
			gpV2Client.Reset(config);
			gpFakeHttp.TokenResponder=null;
			Assert.IsNotNull(Run(() => gpV2Client.GetAccessTokenAsync(config,CancellationToken.None)));
		}

		[TestMethod]
		public void AccessToken_MissingCredentialsMakeNoCall() {
			GpFakeHttp gpFakeHttp=new GpFakeHttp();
			GpV2Client gpV2Client=MakeClient(gpFakeHttp);
			GlobalPaymentsV2Config config=MakeConfig();
			config.AppKey="";
			GpV2Exception gpV2Exception=AssertThrows<GpV2Exception>(() => Run(() => gpV2Client.GetAccessTokenAsync(config,CancellationToken.None)));
			Assert.AreEqual(GpV2ErrorKind.NotConfigured,gpV2Exception.Kind);
			Assert.AreEqual(0,gpFakeHttp.Requests.Count);
		}

		[TestMethod]
		public void Hosts_ProductionAndSandboxOnly() {
			Assert.AreEqual("https://apis.globalpay.com/ucp/",GpV2Client.GetBaseUri(false).ToString());
			Assert.AreEqual("https://apis.sandbox.globalpay.com/ucp/",GpV2Client.GetBaseUri(true).ToString());
			GpFakeHttp gpFakeHttp=new GpFakeHttp();
			GpV2Client gpV2Client=MakeClient(gpFakeHttp);
			Run(() => gpV2Client.GetAccessTokenAsync(MakeConfig(isSandbox:false),CancellationToken.None));
			Assert.AreEqual("apis.globalpay.com",gpFakeHttp.Requests.Single().Uri.Host);
		}

		[TestMethod]
		public void DeviceSale_SendsTheTerminalAndMinorUnitsWithTheToken() {
			GpFakeHttp gpFakeHttp=new GpFakeHttp();
			gpFakeHttp.Enqueue(HttpStatusCode.OK,GpSamples.DeviceInitiated("DVC_1"));
			GpV2Client gpV2Client=MakeClient(gpFakeHttp);
			GpV2DeviceAnswer answer=Run(() => gpV2Client.StartDeviceTransactionAsync(MakeConfig(),"T-1234",GpV2TransType.Sale,12.34m,CancellationToken.None));
			Assert.AreEqual("DVC_1",answer.CommandId);
			Assert.IsTrue(answer.IsPending);
			GpRequest gpRequest=gpFakeHttp.ApiRequests.Single();
			Assert.AreEqual("device_commands",gpRequest.Endpoint);
			Assert.AreEqual("Bearer TOKEN-1",gpRequest.GetHeader("Authorization"));
			Assert.AreEqual("2021-03-22",gpRequest.GetHeader("X-GP-Version"));
			JObject jObject=gpRequest.GetJson();
			Assert.AreEqual("T-1234",(string)jObject["device_reference"]);
			Assert.AreEqual("AUTHORIZE",(string)jObject["action_type"]);
			Assert.AreEqual("1234",(string)jObject["transaction"]["amount"]);
		}

		[TestMethod]
		public void DeviceRefund_And_ResultQuery() {
			GpFakeHttp gpFakeHttp=new GpFakeHttp();
			gpFakeHttp.Enqueue(HttpStatusCode.OK,GpSamples.DeviceInitiated("DVC_2"));
			gpFakeHttp.Enqueue(HttpStatusCode.OK,GpSamples.DeviceCompleted("DVC_2","TRN_2","500"));
			GpV2Client gpV2Client=MakeClient(gpFakeHttp);
			GlobalPaymentsV2Config config=MakeConfig();
			Run(() => gpV2Client.StartDeviceTransactionAsync(config,"T-1",GpV2TransType.Refund,5m,CancellationToken.None));
			GpV2DeviceAnswer answer=Run(() => gpV2Client.GetDeviceCommandResultAsync(config,"T-1","DVC_2",CancellationToken.None));
			Assert.IsFalse(answer.IsPending);
			List<GpRequest> listRequests=gpFakeHttp.ApiRequests;
			Assert.AreEqual("REFUND",(string)listRequests[0].GetJson()["action_type"]);
			JObject jObjectQuery=listRequests[1].GetJson();
			Assert.AreEqual("TRANSACTION_LIST",(string)jObjectQuery["action_type"]);
			Assert.AreEqual("DVC_2",(string)jObjectQuery["id"]);
			Assert.AreEqual("T-1",(string)jObjectQuery["device_reference"]);
			Assert.AreEqual(1,gpFakeHttp.CountTokens,"One token for both calls.");
		}

		[TestMethod]
		public void Device_Expired401GetsOneNewTokenAndOneResend() {
			GpFakeHttp gpFakeHttp=new GpFakeHttp();
			gpFakeHttp.Enqueue(HttpStatusCode.Unauthorized,GpSamples.Error("NOT_AUTHENTICATED","Invalid access token"));
			gpFakeHttp.Enqueue(HttpStatusCode.OK,GpSamples.DeviceInitiated("DVC_3"));
			GpV2Client gpV2Client=MakeClient(gpFakeHttp);
			GpV2DeviceAnswer answer=Run(() => gpV2Client.StartDeviceTransactionAsync(MakeConfig(),"T-1",GpV2TransType.Sale,1m,CancellationToken.None));
			Assert.AreEqual("DVC_3",answer.CommandId);
			Assert.AreEqual(2,gpFakeHttp.CountTokens);
			Assert.AreEqual("Bearer TOKEN-2",gpFakeHttp.ApiRequests[1].GetHeader("Authorization"));
		}

		[TestMethod]
		public void Device_ServerErrorIsOfflineAndNotRetried() {
			GpFakeHttp gpFakeHttp=new GpFakeHttp();
			gpFakeHttp.Enqueue(HttpStatusCode.BadGateway,"");
			GpV2Client gpV2Client=MakeClient(gpFakeHttp);
			GpV2Exception gpV2Exception=AssertThrows<GpV2Exception>(() => Run(() => gpV2Client.StartDeviceTransactionAsync(MakeConfig(),"T-1",
				GpV2TransType.Sale,1m,CancellationToken.None)));
			Assert.AreEqual(GpV2ErrorKind.Offline,gpV2Exception.Kind);
			Assert.AreEqual(1,gpFakeHttp.ApiRequests.Count,"A request that moves money is sent once.");
		}

		[TestMethod]
		public void Device_NetworkFailureIsOfflineAndNotRetried() {
			GpFakeHttp gpFakeHttp=new GpFakeHttp();
			gpFakeHttp.EnqueueNetworkFailure();
			GpV2Client gpV2Client=MakeClient(gpFakeHttp);
			GpV2Exception gpV2Exception=AssertThrows<GpV2Exception>(() => Run(() => gpV2Client.StartDeviceTransactionAsync(MakeConfig(),"T-1",
				GpV2TransType.Sale,1m,CancellationToken.None)));
			Assert.AreEqual(GpV2ErrorKind.Offline,gpV2Exception.Kind);
			StringAssert.Contains(gpV2Exception.Message,"may or may not");
			Assert.AreEqual(1,gpFakeHttp.ApiRequests.Count);
		}

		[TestMethod]
		public void Device_BadRequestIsRejectedWithTheVendorsMessage() {
			GpFakeHttp gpFakeHttp=new GpFakeHttp();
			gpFakeHttp.Enqueue(HttpStatusCode.BadRequest,GpSamples.Error("INVALID_REQUEST_DATA","Unknown device_reference"));
			GpV2Client gpV2Client=MakeClient(gpFakeHttp);
			GpV2Exception gpV2Exception=AssertThrows<GpV2Exception>(() => Run(() => gpV2Client.StartDeviceTransactionAsync(MakeConfig(),"T-9",
				GpV2TransType.Sale,1m,CancellationToken.None)));
			Assert.AreEqual(GpV2ErrorKind.Rejected,gpV2Exception.Kind);
			StringAssert.Contains(gpV2Exception.Message,"Unknown device_reference");
			Assert.AreEqual("INVALID_REQUEST_DATA",gpV2Exception.ErrorCode);
		}

		[TestMethod]
		public void Device_PingMovesNoMoney() {
			GpFakeHttp gpFakeHttp=new GpFakeHttp();
			gpFakeHttp.Enqueue(HttpStatusCode.OK,"{\"id\":\"DVC_9\",\"status\":\"COMPLETED\"}");
			GpV2Client gpV2Client=MakeClient(gpFakeHttp);
			Assert.AreEqual("COMPLETED",Run(() => gpV2Client.PingDeviceAsync(MakeConfig(),"T-1",CancellationToken.None)));
			JObject jObject=gpFakeHttp.ApiRequests.Single().GetJson();
			Assert.AreEqual("PING",(string)jObject["action_type"]);
			Assert.IsNull(jObject["transaction"]);
		}

		[TestMethod]
		public void SavedCardSale_FollowsTheTransactionsApi_WithAnIdempotencyKey() {
			GpFakeHttp gpFakeHttp=new GpFakeHttp();
			gpFakeHttp.Enqueue(HttpStatusCode.OK,GpSamples.Transaction("TRN_10","CAPTURED","2550"));
			GpV2Client gpV2Client=MakeClient(gpFakeHttp);
			GlobalPaymentsV2Result result=Run(() => gpV2Client.ChargeSavedCardAsync(MakeConfig(accountName:"office_account"),GpV2TransType.Sale,"PMT_abc",
				25.50m,"RC12-20261025",isRecurring:true,CancellationToken.None));
			Assert.AreEqual(GpV2Outcome.Approved,result.Outcome);
			Assert.AreEqual("TRN_10",result.TransactionId);
			Assert.AreEqual(25.50m,result.Amount);
			GpRequest gpRequest=gpFakeHttp.ApiRequests.Single();
			Assert.AreEqual("transactions",gpRequest.Endpoint);
			Assert.AreEqual("RC12-20261025",gpRequest.GetHeader("X-GP-Idempotency"));
			JObject jObject=gpRequest.GetJson();
			Assert.AreEqual("office_account",(string)jObject["account_name"]);
			Assert.AreEqual("SALE",(string)jObject["type"]);
			Assert.AreEqual("CNP",(string)jObject["channel"]);
			Assert.AreEqual("2550",(string)jObject["amount"]);
			Assert.AreEqual("USD",(string)jObject["currency"]);
			Assert.AreEqual("US",(string)jObject["country"]);
			Assert.AreEqual("RC12-20261025",(string)jObject["reference"]);
			Assert.AreEqual("PMT_abc",(string)jObject["payment_method"]["id"]);
			Assert.AreEqual("RECURRING",(string)jObject["stored_credential"]["model"]);
		}

		[TestMethod]
		public void SavedCardRefund_UsesTheTokensAccountWhenNoneIsSet() {
			GpFakeHttp gpFakeHttp=new GpFakeHttp();
			gpFakeHttp.TokenResponder=x => GpFakeHttp.Make(HttpStatusCode.OK,GpSamples.Token("TOKEN-X",86399,"first_account","second_account"));
			gpFakeHttp.Enqueue(HttpStatusCode.OK,GpSamples.Transaction("TRN_11","CAPTURED","1000"));
			GpV2Client gpV2Client=MakeClient(gpFakeHttp);
			GlobalPaymentsV2Result result=Run(() => gpV2Client.ChargeSavedCardAsync(MakeConfig(),GpV2TransType.Refund,"PMT_abc",10m,"RF-0000001",
				isRecurring:false,CancellationToken.None));
			Assert.IsTrue(result.IsApproved);
			JObject jObject=gpFakeHttp.ApiRequests.Single().GetJson();
			Assert.AreEqual("first_account",(string)jObject["account_name"]);
			Assert.AreEqual("REFUND",(string)jObject["type"]);
			Assert.IsNull(jObject["stored_credential"]);
		}

		[TestMethod]
		public void SavedCard_DeclineIsAResultNotAnError() {
			GpFakeHttp gpFakeHttp=new GpFakeHttp();
			gpFakeHttp.Enqueue(HttpStatusCode.OK,GpSamples.Transaction("TRN_12","DECLINED","1000","05"));
			gpFakeHttp.Enqueue(HttpStatusCode.BadRequest,"{\"status\":\"DECLINED\",\"id\":\"TRN_13\",\"error_code\":\"DECLINED\","
				+"\"detailed_error_description\":\"Card declined by issuer\"}");
			GpV2Client gpV2Client=MakeClient(gpFakeHttp);
			GlobalPaymentsV2Config config=MakeConfig(accountName:"a");
			GlobalPaymentsV2Result result=Run(() => gpV2Client.ChargeSavedCardAsync(config,GpV2TransType.Sale,"PMT_abc",10m,"SL-00000001",false,
				CancellationToken.None));
			Assert.AreEqual(GpV2Outcome.Declined,result.Outcome);
			Assert.AreEqual("05",result.ResultCode);
			result=Run(() => gpV2Client.ChargeSavedCardAsync(config,GpV2TransType.Sale,"PMT_abc",10m,"SL-00000002",false,CancellationToken.None));
			Assert.AreEqual(GpV2Outcome.Declined,result.Outcome);
			StringAssert.Contains(result.Message,"Card declined by issuer");
		}

		[TestMethod]
		public void Reversal_PostsToTheTransactionsReversalEndpoint() {
			GpFakeHttp gpFakeHttp=new GpFakeHttp();
			gpFakeHttp.Enqueue(HttpStatusCode.OK,GpSamples.Transaction("TRN_20","REVERSED","1999"));
			GpV2Client gpV2Client=MakeClient(gpFakeHttp);
			GlobalPaymentsV2Result result=Run(() => gpV2Client.ReverseAsync(MakeConfig(),"TRN_20",null,CancellationToken.None));
			Assert.AreEqual(GpV2Outcome.Approved,result.Outcome);
			Assert.AreEqual(GpV2TransType.Void,result.TransType);
			GpRequest gpRequest=gpFakeHttp.ApiRequests.Single();
			Assert.AreEqual("transactions/TRN_20/reversal",gpRequest.Endpoint);
			Assert.AreEqual("VOID-TRN_20",gpRequest.GetHeader("X-GP-Idempotency"));
			Assert.IsNull(gpRequest.GetJson()["amount"],"No amount reverses the whole transaction.");
		}

		[TestMethod]
		public void Logs_NeverHoldTheKeyTokenOrCardToken() {
			GpFakeHttp gpFakeHttp=new GpFakeHttp();
			gpFakeHttp.Enqueue(HttpStatusCode.OK,GpSamples.Transaction("TRN_30","CAPTURED","100","00","PMT_secret_token"));
			gpFakeHttp.Enqueue(HttpStatusCode.InternalServerError,"");
			GpV2Client gpV2Client=MakeClient(gpFakeHttp);
			GlobalPaymentsV2Config config=MakeConfig(accountName:"a");
			Run(() => gpV2Client.ChargeSavedCardAsync(config,GpV2TransType.Sale,"PMT_secret_token",1m,"SL-00000003",false,CancellationToken.None));
			AssertThrows<GpV2Exception>(() => Run(() => gpV2Client.ChargeSavedCardAsync(config,GpV2TransType.Sale,"PMT_secret_token",1m,"SL-00000004",false,
				CancellationToken.None)));
			string allLog=string.Join("\n",_listLogLines);
			Assert.IsFalse(allLog.Contains(APP_KEY));
			Assert.IsFalse(allLog.Contains("TOKEN-1"));
			Assert.IsFalse(allLog.Contains("PMT_secret_token"));
		}

		[TestMethod]
		public void Requests_OnlyEverGoToGlobalPayments() {
			GpFakeHttp gpFakeHttp=new GpFakeHttp();
			gpFakeHttp.Enqueue(HttpStatusCode.OK,GpSamples.DeviceInitiated("DVC_40"));
			gpFakeHttp.Enqueue(HttpStatusCode.OK,GpSamples.Transaction("TRN_40","REVERSED","100"));
			GpV2Client gpV2Client=MakeClient(gpFakeHttp);
			GlobalPaymentsV2Config config=MakeConfig(isSandbox:false);
			Run(() => gpV2Client.StartDeviceTransactionAsync(config,"T-1",GpV2TransType.Sale,1m,CancellationToken.None));
			Run(() => gpV2Client.ReverseAsync(config,"TRN_40",null,CancellationToken.None));
			foreach(GpRequest gpRequest in gpFakeHttp.Requests) {
				Assert.AreEqual("https",gpRequest.Uri.Scheme);
				Assert.AreEqual("apis.globalpay.com",gpRequest.Uri.Host);
				Assert.IsFalse(HqHosts.IsBlocked(gpRequest.Uri));
			}
		}

		public static T AssertThrows<T>(Action action) where T:Exception {
			try {
				action();
			}
			catch(Exception ex) {
				Exception exInner=ex;
				if(exInner is AggregateException aggregateException && aggregateException.InnerExceptions.Count==1) {
					exInner=aggregateException.InnerExceptions[0];
				}
				if(exInner is T exExpected) {
					return exExpected;
				}
				Assert.Fail("Expected "+typeof(T).Name+" but got "+exInner.GetType().Name+": "+exInner.Message);
			}
			Assert.Fail("Expected "+typeof(T).Name+".");
			return null;
		}
	}
}
