// QuickBooks Online (direct): the QuickBooks Online Accounting API v3 calls this integration makes (account, class and company lookups, deposits).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CodeBase.VendorDirect;
using Newtonsoft.Json.Linq;

namespace OpenDentBusiness.Qbo {
	///<summary>An account or class as QuickBooks Online lists it.</summary>
	public sealed class QboEntity {
		public string Id="";
		public string Name="";
		///<summary>"Parent:Child". Equal to Name for top-level entities.</summary>
		public string FullyQualifiedName="";

		///<summary>The name this program shows and stores: the fully qualified name, or Name if Intuit sent none.</summary>
		public string DisplayName {
			get {
				return (FullyQualifiedName!="") ? FullyQualifiedName : Name;
			}
		}
	}

	///<summary>The deposit to create. Only amounts, the date, the memo and account and class IDs: never patient names, check numbers or payment notes
	///(Intuit signs no BAA).</summary>
	public sealed class QboDepositRequest {
		public string DepositToAccountId="";
		public string IncomeAccountId="";
		///<summary>Blank for no class.</summary>
		public string ClassId="";
		///<summary>One line per payment.</summary>
		public List<decimal> ListAmounts=new List<decimal>();
		public string Memo="";
		public DateTime Date;
	}

	///<summary>The deposit Intuit created (or, for a repeated request ID, the one it created the first time).</summary>
	public sealed class QboDepositResult {
		public string DepositId="";
		///<summary>True when a class was sent and at least one line came back without it.</summary>
		public bool IsClassRefMissing;
	}

	///<summary>Calls the Accounting API for one company (realm). Every call sends the bearer token, Accept: application/json and minorversion=75.
	///A 401 refreshes the token once and retries once (QboTokenManager). GETs are retried by VendorHttp on network errors, 429 and 5xx. The deposit
	///POST is retried here, only with the same requestid, so a lost response never creates a second deposit.</summary>
	public sealed class QboApiClient {
		public const string MinorVersion="75";
		///<summary>Intuit returns at most 1000 rows per query.</summary>
		public const int PageSize=1000;
		///<summary>Safety stop for paging: 50,000 rows.</summary>
		private const int MAX_PAGES=50;
		///<summary>Intuit allows at most 50 characters.</summary>
		public const int MaxRequestIdLength=50;
		///<summary>PrivateNote holds at most 4000 characters.</summary>
		public const int MaxMemoLength=4000;
		public static readonly Uri ProductionBaseUri=new Uri("https://quickbooks.api.intuit.com/");
		public static readonly Uri SandboxBaseUri=new Uri("https://sandbox-quickbooks.api.intuit.com/");
		///<summary>Accounts a deposit may go to. Other Current Asset makes Undeposited Funds possible.</summary>
		public static readonly string[] ArrayDepositAccountTypes={ "Bank","Other Current Asset" };
		public static readonly string[] ArrayIncomeAccountTypes={ "Income","Other Income" };

		private readonly HttpClient _httpClient;
		private readonly Uri _uriBase;
		private readonly string _realmId;
		private readonly QboTokenManager _tokenManager;
		///<summary>Waits before the deposit POST is retried after a 5xx, a timeout or a network failure. Two retries: 1 s, then 3 s. Tests shorten them.</summary>
		public TimeSpan[] ArrayPostRetryDelays={ TimeSpan.FromSeconds(1),TimeSpan.FromSeconds(3) };

		public QboApiClient(HttpClient httpClient,Uri uriBase,string realmId,QboTokenManager tokenManager) {
			_httpClient=httpClient ?? throw new ArgumentNullException(nameof(httpClient));
			_uriBase=uriBase ?? throw new ArgumentNullException(nameof(uriBase));
			if(!QboRedirectRules.IsValidRealmId(realmId)) {
				throw new QboAuthException(QboAuthException.MessageNotConnected,needsReconnect:true);
			}
			_realmId=realmId;
			_tokenManager=tokenManager ?? throw new ArgumentNullException(nameof(tokenManager));
		}

		///<summary>The API host for the environment.</summary>
		public static Uri GetBaseUri(QboEnvironment environment) {
			return (environment==QboEnvironment.Sandbox) ? SandboxBaseUri : ProductionBaseUri;
		}

		///<summary>Active accounts of the given types (QuickBooks Online AccountType values), all pages.</summary>
		public Task<List<QboEntity>> GetAccountsAsync(string[] arrayAccountTypes,CancellationToken cancellationToken=default(CancellationToken)) {
			string where="AccountType in ("+string.Join(",",arrayAccountTypes.Select(x => "'"+x.Replace("'","\\'")+"'"))+") and Active = true";
			return QueryAsync("Account",where,cancellationToken);
		}

		///<summary>Active classes, all pages.</summary>
		public Task<List<QboEntity>> GetClassesAsync(CancellationToken cancellationToken=default(CancellationToken)) {
			return QueryAsync("Class","Active = true",cancellationToken);
		}

		///<summary>Runs "select * from {entity} where {where}" page by page (STARTPOSITION is 1-based, 1000 rows a page) until a page comes back short.</summary>
		public async Task<List<QboEntity>> QueryAsync(string entity,string where,CancellationToken cancellationToken=default(CancellationToken)) {
			List<QboEntity> listEntities=new List<QboEntity>();
			for(int page=0;page<MAX_PAGES;page++) {
				int startPosition=page*PageSize+1;
				string query="select * from "+entity+(string.IsNullOrEmpty(where) ? "" : " where "+where)
					+" STARTPOSITION "+startPosition.ToString(CultureInfo.InvariantCulture)+" MAXRESULTS "+PageSize.ToString(CultureInfo.InvariantCulture);
				Uri uri=BuildUri("query","query="+Uri.EscapeDataString(query));
				JObject jObject=await GetJsonAsync(uri,"query "+entity,cancellationToken).ConfigureAwait(false);
				JArray jArray=jObject["QueryResponse"]?[entity] as JArray;
				int count=0;
				if(jArray!=null) {
					foreach(JToken jToken in jArray) {
						count++;
						QboEntity qboEntity=new QboEntity();
						qboEntity.Id=jToken.Value<string>("Id")??"";
						qboEntity.Name=jToken.Value<string>("Name")??"";
						qboEntity.FullyQualifiedName=jToken.Value<string>("FullyQualifiedName")??"";
						if(qboEntity.Id!="") {
							listEntities.Add(qboEntity);
						}
					}
				}
				if(count<PageSize) {
					break;
				}
			}
			return listEntities;
		}

		///<summary>The connected company's name, for display.</summary>
		public async Task<string> GetCompanyNameAsync(CancellationToken cancellationToken=default(CancellationToken)) {
			Uri uri=BuildUri("companyinfo/"+Uri.EscapeDataString(_realmId),"");
			JObject jObject=await GetJsonAsync(uri,"companyinfo",cancellationToken).ConfigureAwait(false);
			return jObject["CompanyInfo"]?.Value<string>("CompanyName")??"";
		}

		///<summary>Creates the deposit. requestId is Intuit's idempotency key: one per Send click, reused for every retry of that Send, so a repeat
		///returns the original deposit instead of a second one. Retries twice after a 5xx, a timeout or a network failure.</summary>
		public async Task<QboDepositResult> CreateDepositAsync(QboDepositRequest depositRequest,string requestId,
			CancellationToken cancellationToken=default(CancellationToken))
		{
			if(string.IsNullOrEmpty(requestId) || requestId.Length>MaxRequestIdLength) {
				throw new ArgumentException("A request ID of 1 to 50 characters is required.",nameof(requestId));
			}
			string json=BuildDepositJson(depositRequest).ToString(Newtonsoft.Json.Formatting.None);
			Uri uri=BuildUri("deposit","requestid="+Uri.EscapeDataString(requestId));
			Func<HttpRequestMessage> funcCreateRequest=() => {
				HttpRequestMessage request=new HttpRequestMessage(HttpMethod.Post,uri);
				request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
				request.Content=new StringContent(json,Encoding.UTF8,"application/json");
				VendorHttp.SetEndpointName(request,"deposit");
				return request;
			};
			for(int attempt=0;;attempt++) {
				HttpResponseMessage response=null;
				HttpRequestException exNetwork=null;
				try {
					response=await _tokenManager.SendAsync(_httpClient,funcCreateRequest,cancellationToken).ConfigureAwait(false);
				}
				catch(HttpRequestException ex) {
					exNetwork=ex;
				}
				bool isRetryable=(exNetwork!=null && IsRetryableNetworkFailure(exNetwork)) || (response!=null && IsRetryableStatus(response.StatusCode));
				if(isRetryable && attempt<ArrayPostRetryDelays.Length) {
					response?.Dispose();
					await System.Threading.Tasks.Task.Delay(ArrayPostRetryDelays[attempt],cancellationToken).ConfigureAwait(false);
					continue;
				}
				if(exNetwork!=null) {
					throw new QboApiException(QboApiException.MessageUnreachable,innerException:exNetwork);
				}
				using(response) {
					JObject jObject=await ReadJsonAsync(response).ConfigureAwait(false);
					return ParseDepositResponse(jObject,!string.IsNullOrEmpty(depositRequest.ClassId));
				}
			}
		}

		///<summary>The Deposit JSON: TxnDate, PrivateNote (memo, at most 4000 characters), DepositToAccountRef and one line per amount (rounded to cents)
		///with the income account and, when given, the class. Public for tests.</summary>
		public static JObject BuildDepositJson(QboDepositRequest depositRequest) {
			JObject jObject=new JObject();
			jObject["TxnDate"]=depositRequest.Date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
			string memo=depositRequest.Memo??"";
			if(memo.Length>MaxMemoLength) {
				memo=memo.Substring(0,MaxMemoLength);
			}
			if(memo!="") {
				jObject["PrivateNote"]=memo;
			}
			jObject["DepositToAccountRef"]=new JObject(new JProperty("value",depositRequest.DepositToAccountId));
			JArray jArrayLines=new JArray();
			for(int i=0;i<depositRequest.ListAmounts.Count;i++) {
				JObject jObjectDetail=new JObject();
				jObjectDetail["AccountRef"]=new JObject(new JProperty("value",depositRequest.IncomeAccountId));
				if(!string.IsNullOrEmpty(depositRequest.ClassId)) {
					jObjectDetail["ClassRef"]=new JObject(new JProperty("value",depositRequest.ClassId));
				}
				JObject jObjectLine=new JObject();
				jObjectLine["Amount"]=Math.Round(depositRequest.ListAmounts[i],2,MidpointRounding.AwayFromZero);
				jObjectLine["DetailType"]="DepositLineDetail";
				jObjectLine["DepositLineDetail"]=jObjectDetail;
				jArrayLines.Add(jObjectLine);
			}
			jObject["Line"]=jArrayLines;
			return jObject;
		}

		///<summary>Reads Deposit.Id, and whether any deposit line came back without a ClassRef when one was sent.</summary>
		public static QboDepositResult ParseDepositResponse(JObject jObject,bool wasClassSent) {
			JObject jObjectDeposit=jObject["Deposit"] as JObject;
			if(jObjectDeposit==null) {
				throw new QboApiException("QuickBooks Online did not return the deposit.");
			}
			QboDepositResult result=new QboDepositResult();
			result.DepositId=jObjectDeposit.Value<string>("Id")??"";
			if(wasClassSent) {
				JArray jArrayLines=jObjectDeposit["Line"] as JArray;
				List<JToken> listDetails=(jArrayLines==null) ? new List<JToken>()
					: jArrayLines.Select(x => x["DepositLineDetail"]).Where(x => x!=null && x.Type==JTokenType.Object).ToList();
				result.IsClassRefMissing=listDetails.Count==0 || listDetails.Any(x => string.IsNullOrEmpty(x["ClassRef"]?.Value<string>("value")));
			}
			return result;
		}

		///<summary>Turns a failed response into the exception the user sees: 401 needs a reconnect, 429 is "busy", anything else shows Intuit's Fault
		///Message and Detail (or the HTTP status). Public for tests.</summary>
		public static Exception ParseFailure(int statusCode,string body,string intuitTid) {
			if(statusCode==401) {
				return new QboAuthException(QboAuthException.MessageReconnect,needsReconnect:true);
			}
			if(statusCode==429) {
				return new QboApiException(QboApiException.MessageThrottled,statusCode,"","",intuitTid);
			}
			string faultType="";
			string code="";
			string message="";
			string detail="";
			try {
				JObject jObject=JObject.Parse(body??"");
				JToken jTokenFault=jObject["Fault"]??jObject["fault"];
				if(jTokenFault!=null) {
					faultType=jTokenFault.Value<string>("type")??"";
					JToken jTokenError=(jTokenFault["Error"]??jTokenFault["error"]) is JArray jArrayErrors ? jArrayErrors.FirstOrDefault() : null;
					if(jTokenError!=null) {
						message=jTokenError.Value<string>("Message")??"";
						detail=jTokenError.Value<string>("Detail")??"";
						code=jTokenError.Value<string>("code")??"";
					}
				}
			}
			catch(Exception) {
				//Not JSON (a gateway error page). The status says enough.
			}
			if(faultType=="AuthenticationFault" || faultType=="AuthorizationFault") {
				return new QboAuthException(QboAuthException.MessageReconnect,needsReconnect:true);
			}
			string text;
			if(message=="" && detail=="") {
				text="QuickBooks Online returned an error (HTTP "+statusCode.ToString(CultureInfo.InvariantCulture)+").";
			}
			else {
				text="QuickBooks Online returned an error: "+message;
				if(detail!="" && detail!=message) {
					text+=(message=="" ? "" : " ")+detail;
				}
			}
			return new QboApiException(text,statusCode,faultType,code,intuitTid);
		}

		private Uri BuildUri(string path,string query) {
			string relative="v3/company/"+Uri.EscapeDataString(_realmId)+"/"+path+"?"+(query=="" ? "" : query+"&")+"minorversion="+MinorVersion;
			return new Uri(_uriBase,relative);
		}

		private async Task<JObject> GetJsonAsync(Uri uri,string endpointName,CancellationToken cancellationToken) {
			Func<HttpRequestMessage> funcCreateRequest=() => {
				HttpRequestMessage request=new HttpRequestMessage(HttpMethod.Get,uri);
				request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
				VendorHttp.SetEndpointName(request,endpointName);
				return request;
			};
			HttpResponseMessage response;
			try {
				response=await _tokenManager.SendAsync(_httpClient,funcCreateRequest,cancellationToken).ConfigureAwait(false);
			}
			catch(HttpRequestException ex) {
				throw new QboApiException(QboApiException.MessageUnreachable,innerException:ex);
			}
			using(response) {
				return await ReadJsonAsync(response).ConfigureAwait(false);
			}
		}

		private static async Task<JObject> ReadJsonAsync(HttpResponseMessage response) {
			string body=(response.Content==null) ? "" : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
			if(!response.IsSuccessStatusCode) {
				throw ParseFailure((int)response.StatusCode,body,VendorHttp.GetVendorRequestId(response));
			}
			try {
				return JObject.Parse(body);
			}
			catch(Exception ex) {
				throw new QboApiException("QuickBooks Online returned an answer this program could not read.",(int)response.StatusCode,"","",
					VendorHttp.GetVendorRequestId(response),ex);
			}
		}

		private static bool IsRetryableStatus(HttpStatusCode statusCode) {
			int code=(int)statusCode;
			return code==500 || code==502 || code==503 || code==504;
		}

		///<summary>Timeouts and connection failures. Not a URL the policy refused, and not an open circuit breaker.</summary>
		private static bool IsRetryableNetworkFailure(HttpRequestException ex) {
			VendorHttpException exVendor=ex as VendorHttpException;
			if(exVendor==null) {
				return true;
			}
			return exVendor.Failure==VendorHttpFailure.Timeout;
		}
	}
}
