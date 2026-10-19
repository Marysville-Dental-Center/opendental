// Test helpers for the QuickBooks Online package: a fake connection store, Intuit-shaped responses and wired-up clients over fake HTTP handlers.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Net.Http;
using CodeBase.VendorDirect;
using Newtonsoft.Json.Linq;
using OpenDentBusiness.Qbo;

namespace VendorDirectTests.Qbo {
	///<summary>The shared refresh token row, in memory. TryReplace is a real compare-and-swap.</summary>
	public sealed class FakeQboStore : IQboConnectionStore {
		public string Current="";
		public int CountReads;
		public List<string> ListReplacements=new List<string>();
		///<summary>Set to make the next TryReplace lose, as if another workstation stored a newer token a moment before.</summary>
		public string ValueStoredByOtherWorkstation;
		public DateTime? RefreshTokenExpiresUtc;
		public int CountExpiryWrites;
		public int CountSignals;

		public string ReadCurrent() {
			CountReads++;
			return Current;
		}

		public bool TryReplace(string expectedCurrent,string newValue) {
			if(ValueStoredByOtherWorkstation!=null) {
				Current=ValueStoredByOtherWorkstation;
				ValueStoredByOtherWorkstation=null;
			}
			if(Current!=expectedCurrent) {
				return false;
			}
			Current=newValue;
			ListReplacements.Add(newValue);
			return true;
		}

		public DateTime? ReadRefreshTokenExpiresUtc() {
			return RefreshTokenExpiresUtc;
		}

		public void WriteRefreshTokenExpiresUtc(DateTime utc) {
			RefreshTokenExpiresUtc=utc;
			CountExpiryWrites++;
		}

		public void SignalChanged() {
			CountSignals++;
		}
	}

	///<summary>A token manager and API client for one fake company, with separate fake handlers for Intuit's OAuth and API hosts.</summary>
	public sealed class QboTestKit {
		public const string ClientId="test-client-id";
		public const string ClientSecret="test-client-secret";
		public const string RealmId="4620816365165566530";
		public static readonly Uri UriApiBase=new Uri("https://sandbox-quickbooks.api.intuit.com/");

		public FakeHttpHandler HandlerOAuth=new FakeHttpHandler();
		public FakeHttpHandler HandlerApi=new FakeHttpHandler();
		public FakeQboStore Store=new FakeQboStore();
		public TokenCache TokenCache=new TokenCache();
		public QboOAuth QboOAuth;
		public QboTokenManager TokenManager;
		public QboApiClient ApiClient;
		public List<string> ListLogLines=new List<string>();

		public QboTestKit(string refreshTokenStored="rt-1") {
			Store.Current=refreshTokenStored;
			VendorHttpOptions optionsOAuth=new VendorHttpOptions();
			optionsOAuth.VendorName="QuickBooksOnline OAuth test";
			optionsOAuth.ListAllowedHosts=new List<string>(QboOAuth.ArrayAllowedHosts);
			optionsOAuth.MaxGetRetries=0;
			optionsOAuth.Log=x => ListLogLines.Add(x);
			QboOAuth=new QboOAuth(VendorHttp.CreateClient(optionsOAuth,HandlerOAuth),ClientId,ClientSecret);
			TokenManager=new QboTokenManager(TokenCache,Store,(refreshToken,ct) => QboOAuth.RefreshAsync(refreshToken,ct));
			VendorHttpOptions optionsApi=new VendorHttpOptions();
			optionsApi.VendorName="QuickBooksOnline test";
			optionsApi.BaseAddress=UriApiBase;
			optionsApi.ListAllowedHosts=new List<string> { UriApiBase.Host };
			optionsApi.MaxGetRetries=0;
			optionsApi.Log=x => ListLogLines.Add(x);
			ApiClient=new QboApiClient(VendorHttp.CreateClient(optionsApi,HandlerApi),UriApiBase,RealmId,TokenManager);
			ApiClient.ArrayPostRetryDelays=new[] { TimeSpan.Zero,TimeSpan.Zero };
		}

		///<summary>An Intuit token endpoint answer.</summary>
		public static string TokenJson(string accessToken,string refreshToken,long? refreshExpiresInSeconds=8726400,string extraField=null) {
			JObject jObject=new JObject();
			jObject["token_type"]="bearer";
			jObject["access_token"]=accessToken;
			jObject["expires_in"]=3600;
			jObject["refresh_token"]=refreshToken;
			if(refreshExpiresInSeconds.HasValue) {
				jObject["x_refresh_token_expires_in"]=refreshExpiresInSeconds.Value;
			}
			if(extraField!=null) {
				jObject[extraField]=157680000;
			}
			return jObject.ToString();
		}

		public static string OAuthErrorJson(string error) {
			return new JObject(new JProperty("error",error)).ToString();
		}

		public static string FaultJson(string type,string message,string detail,string code) {
			return "{\"Fault\":{\"Error\":[{\"Message\":\""+message+"\",\"Detail\":\""+detail+"\",\"code\":\""+code+"\"}],\"type\":\""+type+"\"},"
				+"\"time\":\"2026-10-19T10:00:00.000-07:00\"}";
		}

		public static string DepositJson(string id,bool withClass) {
			string classRef=withClass ? ",\"ClassRef\":{\"value\":\"5000\",\"name\":\"Hygiene\"}" : "";
			return "{\"Deposit\":{\"Id\":\""+id+"\",\"TxnDate\":\"2026-10-19\",\"Line\":["
				+"{\"Amount\":10.0,\"DetailType\":\"DepositLineDetail\",\"DepositLineDetail\":{\"AccountRef\":{\"value\":\"79\"}"+classRef+"}},"
				+"{\"Amount\":5.25,\"DetailType\":\"DepositLineDetail\",\"DepositLineDetail\":{\"AccountRef\":{\"value\":\"79\"}"+classRef+"}}"
				+"]},\"time\":\"2026-10-19T10:00:00.000-07:00\"}";
		}

		public static string QueryJson(string entity,int count,int idStart=1) {
			JArray jArray=new JArray();
			for(int i=0;i<count;i++) {
				JObject jObject=new JObject();
				jObject["Id"]=(idStart+i).ToString();
				jObject["Name"]="Child "+(idStart+i);
				jObject["FullyQualifiedName"]="Parent:Child "+(idStart+i);
				jArray.Add(jObject);
			}
			JObject jObjectResponse=new JObject();
			JObject jObjectQuery=new JObject();
			if(count>0) {
				jObjectQuery[entity]=jArray;
				jObjectQuery["startPosition"]=idStart;
				jObjectQuery["maxResults"]=count;
			}
			jObjectResponse["QueryResponse"]=jObjectQuery;
			return jObjectResponse.ToString();
		}

		public static QboDepositRequest SampleDeposit(string classId="") {
			QboDepositRequest depositRequest=new QboDepositRequest();
			depositRequest.DepositToAccountId="35";
			depositRequest.IncomeAccountId="79";
			depositRequest.ClassId=classId;
			depositRequest.ListAmounts=new List<decimal> { 10m,5.25m };
			depositRequest.Memo="Batch 12";
			depositRequest.Date=new DateTime(2026,10,19);
			return depositRequest;
		}

		public static HttpResponseMessage WithTid(HttpResponseMessage response,string tid="1-abc") {
			response.Headers.TryAddWithoutValidation("intuit_tid",tid);
			return response;
		}
	}
}
