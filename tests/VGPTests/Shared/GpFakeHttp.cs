// Test helpers for V-GP: a fake HTTP handler that answers like Global Payments' API from a script, never touching the network, plus answers shaped
// like the public samples, and the repository root for source checks.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace VGPTests {
	///<summary>A request as it reached the fake handler. The body is read at send time.</summary>
	public sealed class GpRequest {
		public HttpMethod Method;
		public Uri Uri;
		public Dictionary<string,string> DictHeaders=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
		public string Body="";

		public string GetHeader(string name) {
			string value;
			return DictHeaders.TryGetValue(name,out value) ? value : null;
		}

		public JObject GetJson() {
			return JObject.Parse(Body);
		}

		///<summary>The path after /ucp/, e.g. "accesstoken" or "transactions/TRN_1/reversal".</summary>
		public string Endpoint {
			get {
				string path=Uri.AbsolutePath;
				int index=path.IndexOf("/ucp/",StringComparison.Ordinal);
				return (index<0) ? path : path.Substring(index+5);
			}
		}
	}

	///<summary>Answers requests by endpoint: the access token endpoint gets a token unless told otherwise; other endpoints are answered from a queue of
	///responders in order. Records every request.</summary>
	public sealed class GpFakeHttp:HttpMessageHandler {
		private readonly object _lock=new object();
		private readonly List<GpRequest> _listRequests=new List<GpRequest>();
		private readonly Queue<Func<GpRequest,HttpResponseMessage>> _queueResponders=new Queue<Func<GpRequest,HttpResponseMessage>>();
		///<summary>The answer to the token endpoint. Null gives a standard token.</summary>
		public Func<GpRequest,HttpResponseMessage> TokenResponder;
		public int CountTokens;

		public List<GpRequest> Requests {
			get {
				lock(_lock) {
					return _listRequests.ToList();
				}
			}
		}

		///<summary>Every request except token requests.</summary>
		public List<GpRequest> ApiRequests {
			get {
				return Requests.Where(x => x.Endpoint!="accesstoken").ToList();
			}
		}

		public GpFakeHttp Enqueue(HttpStatusCode statusCode,string body) {
			return Enqueue(x => Make(statusCode,body));
		}

		public GpFakeHttp Enqueue(Func<GpRequest,HttpResponseMessage> responder) {
			lock(_lock) {
				_queueResponders.Enqueue(responder);
			}
			return this;
		}

		public GpFakeHttp EnqueueNetworkFailure() {
			return Enqueue(x => throw new HttpRequestException("Simulated network failure."));
		}

		public static HttpResponseMessage Make(HttpStatusCode statusCode,string body) {
			HttpResponseMessage response=new HttpResponseMessage(statusCode);
			response.Content=new StringContent(body??"",Encoding.UTF8,"application/json");
			return response;
		}

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken) {
			GpRequest gpRequest=new GpRequest();
			gpRequest.Method=request.Method;
			gpRequest.Uri=request.RequestUri;
			foreach(KeyValuePair<string,IEnumerable<string>> pair in request.Headers) {
				gpRequest.DictHeaders[pair.Key]=string.Join(",",pair.Value);
			}
			if(request.Content!=null) {
				gpRequest.Body=await request.Content.ReadAsStringAsync().ConfigureAwait(false);
				foreach(KeyValuePair<string,IEnumerable<string>> pair in request.Content.Headers) {
					gpRequest.DictHeaders[pair.Key]=string.Join(",",pair.Value);
				}
			}
			lock(_lock) {
				_listRequests.Add(gpRequest);
			}
			if(gpRequest.Endpoint=="accesstoken") {
				Interlocked.Increment(ref CountTokens);
				if(TokenResponder!=null) {
					return TokenResponder(gpRequest);
				}
				return Make(HttpStatusCode.OK,GpSamples.Token("TOKEN-"+CountTokens));
			}
			Func<GpRequest,HttpResponseMessage> responder=null;
			lock(_lock) {
				if(_queueResponders.Count>0) {
					responder=_queueResponders.Dequeue();
				}
			}
			if(responder==null) {
				return Make(HttpStatusCode.NotFound,"{\"error_code\":\"NOT_FOUND\"}");
			}
			return responder(gpRequest);
		}
	}

	///<summary>Answers shaped like Global Payments' public samples (developer.globalpayments.com). Values are made up.</summary>
	public static class GpSamples {
		public static string Token(string token,int secondsToExpire=86399,params string[] arrayAccountNames) {
			JObject jObject=new JObject();
			jObject["token"]=token;
			jObject["type"]="Bearer";
			jObject["app_id"]="APP-SYNTHETIC";
			jObject["seconds_to_expire"]=secondsToExpire.ToString(CultureInfo.InvariantCulture);
			JArray jArrayAccounts=new JArray();
			string[] arrayNames=(arrayAccountNames==null || arrayAccountNames.Length==0) ? new[] { "transaction_processing" } : arrayAccountNames;
			for(int i=0;i<arrayNames.Length;i++) {
				JObject jObjectAccount=new JObject();
				jObjectAccount["id"]="TRA_"+i;
				jObjectAccount["name"]=arrayNames[i];
				jObjectAccount["permissions"]=new JArray("TRN_POST_Authorize","TRN_POST_Refund","TRN_POST_Reverse");
				jArrayAccounts.Add(jObjectAccount);
			}
			JObject jObjectScope=new JObject();
			jObjectScope["merchant_id"]="MER_SYNTHETIC";
			jObjectScope["accounts"]=jArrayAccounts;
			jObject["scope"]=jObjectScope;
			return jObject.ToString();
		}

		///<summary>The interim answer to a terminal command.</summary>
		public static string DeviceInitiated(string commandId) {
			return "{\"id\":\""+commandId+"\",\"device_reference\":\"T-1\",\"action_type\":\"AUTHORIZE\",\"status\":\"INITIATED\","
				+"\"action\":{\"id\":\"ACT_1\",\"type\":\"DEVICE_COMMAND_CREATE\",\"result_code\":\"SUCCESS\"}}";
		}

		///<summary>A finished terminal command with one transaction (a list, as the public sample shows).</summary>
		public static string DeviceCompleted(string commandId,string transactionId,string amountMinorUnits,string paymentMethodResult="00",
			string transactionStatus="",string paymentMethodId="")
		{
			JObject jObjectCard=new JObject();
			jObjectCard["brand"]="VISA";
			jObjectCard["masked_number_last4"]="XXXXXXXXXXXX4242";
			jObjectCard["auth_code"]="A1B2C3";
			JObject jObjectPaymentMethod=new JObject();
			jObjectPaymentMethod["id"]=paymentMethodId;
			jObjectPaymentMethod["result"]=paymentMethodResult;
			jObjectPaymentMethod["message"]=(paymentMethodResult=="00") ? "APPROVAL" : "DECLINED";
			jObjectPaymentMethod["entry_mode"]="CONTACTLESS";
			jObjectPaymentMethod["card"]=jObjectCard;
			JObject jObjectTransaction=new JObject();
			jObjectTransaction["id"]=transactionId;
			if(transactionStatus!="") {
				jObjectTransaction["status"]=transactionStatus;
			}
			jObjectTransaction["amount"]=amountMinorUnits;
			jObjectTransaction["currency"]="USD";
			jObjectTransaction["payment_method"]=jObjectPaymentMethod;
			JObject jObject=new JObject();
			jObject["id"]=commandId;
			jObject["status"]="COMPLETED";
			jObject["action"]=new JObject(new JProperty("result_code","SUCCESS"));
			jObject["transaction"]=new JArray(jObjectTransaction);
			return jObject.ToString();
		}

		///<summary>A Transactions API answer.</summary>
		public static string Transaction(string transactionId,string status,string amountMinorUnits,string result="00",string paymentMethodId="") {
			JObject jObjectCard=new JObject();
			jObjectCard["brand"]="MASTERCARD";
			jObjectCard["masked_number_last4"]="XXXXXXXXXXXX5454";
			jObjectCard["authcode"]="12345";
			JObject jObjectPaymentMethod=new JObject();
			jObjectPaymentMethod["id"]=paymentMethodId;
			jObjectPaymentMethod["result"]=result;
			jObjectPaymentMethod["message"]=(result=="00") ? "[ test system ] AUTHORISED" : "DECLINED";
			jObjectPaymentMethod["card"]=jObjectCard;
			JObject jObject=new JObject();
			jObject["id"]=transactionId;
			jObject["status"]=status;
			jObject["amount"]=amountMinorUnits;
			jObject["currency"]="USD";
			jObject["payment_method"]=jObjectPaymentMethod;
			jObject["action"]=new JObject(new JProperty("result_code",(status=="DECLINED") ? "DECLINED" : "SUCCESS"));
			return jObject.ToString();
		}

		public static string Error(string code,string description) {
			return "{\"error_code\":\""+code+"\",\"detailed_error_code\":\"40001\",\"detailed_error_description\":\""+description+"\"}";
		}
	}

	public static class RepoFiles {
		public static string GetRepositoryRoot() {
			string path=AppDomain.CurrentDomain.BaseDirectory;
			while(path!=null) {
				if(File.Exists(Path.Combine(path,"OpenDental.sln"))) {
					return path;
				}
				path=Path.GetDirectoryName(path);
			}
			throw new InvalidOperationException("Repository root not found.");
		}

		public static string Read(string relativePath) {
			return File.ReadAllText(Path.Combine(GetRepositoryRoot(),relativePath));
		}
	}

	[TestClass]
	public class CultureSetup {
		///<summary>Lans.g only skips its database lookup for en-US, and notes show amounts in the current culture.</summary>
		[AssemblyInitialize]
		public static void AssemblyInitialize(TestContext testContext) {
			CultureInfo cultureInfo=new CultureInfo("en-US");
			CultureInfo.DefaultThreadCurrentCulture=cultureInfo;
			CultureInfo.DefaultThreadCurrentUICulture=cultureInfo;
			Thread.CurrentThread.CurrentCulture=cultureInfo;
			Thread.CurrentThread.CurrentUICulture=cultureInfo;
			GpDb.AssemblyInitialize();
		}

		[AssemblyCleanup]
		public static void AssemblyCleanup() {
			GpDb.AssemblyCleanup();
		}
	}
}
