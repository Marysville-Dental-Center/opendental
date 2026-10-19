using CodeBase;
using CodeBase.VendorDirect;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using OpenDentBusiness.VendorDirect;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace OpenDentBusiness {
	///<summary>DentalXChange PayConnect 2 (XConnect Payment API 2.12). Every call goes from this workstation straight to DentalXChange with two static
	///header keys: the office's own integrator API-Key (practice-wide programproperty "API Key") and the merchant Secret (per clinic, "API Secret").
	///Both are stored as osd1$ values through SecretValue; a Secret Open Dental left in plain text still works until the setup window saves it again.
	///No Open Dental host is ever contacted. See docs/specs/vendor-direct/payconnect.md.</summary>
	public class PayConnect2 {
		#region Constants
		///<summary>Name used for the HTTP client and in log lines.</summary>
		public const string VendorName="PayConnect";
		///<summary>The production base address. Every route starts with /payments.</summary>
		public const string UrlBaseProduction="https://api.dentalxchange.com/payments";
		///<summary>The sandbox: the same routes behind a /sandbox prefix.</summary>
		public const string UrlBaseSandbox="https://api.dentalxchange.com/sandbox/payments";
		///<summary>Prefix of the correlation ID sent as invoiceNumber on every create, so a transaction whose response was lost can be found again.</summary>
		public const string CorrelationIdPrefix="OPMS-";
		///<summary>The registrable domain of every DentalXChange host this integration talks to or shows: the API and the hosted card-entry page.
		///Confirm the hosted page's host in the sandbox (spec 7.2 risk 8); if it is elsewhere, add it here and to the relay script.</summary>
		public const string AllowedDomain="dentalxchange.com";
		private const int LIST_PAGE_SIZE=100;
		private const int LIST_MAX_PAGES=20;
		private const string MSG_API_KEY_REJECTED="The PayConnect API key was not accepted. Check the key in PayConnect Setup, or whether it is enabled for the Payment API.";
		private const string MSG_SECRET_REJECTED="The PayConnect Secret for this clinic was not accepted.";
		private const string MSG_NOT_SENT="Could not reach PayConnect. Nothing was sent, so no charge was made.";
		private const string MSG_NO_ANSWER="PayConnect did not answer. The request may or may not have been processed.";
		private const string MSG_UNREADABLE="PayConnect answered, but its answer could not be read. The request may or may not have been processed.";
		private static readonly ReadOnlyCollection<string> _listAllowedHostPatterns=new ReadOnlyCollection<string>(new[] { "*."+AllowedDomain });
		///<summary>Default timeout for a request.</summary>
		public static readonly TimeSpan TimeoutDefault=TimeSpan.FromSeconds(30);
		///<summary>Timeout for a counter-terminal sale, which waits for the customer. Longer than the "wait up to 2 minutes" message the window shows.</summary>
		public static readonly TimeSpan TimeoutTerminal=TimeSpan.FromSeconds(190);
		#endregion Constants

		#region Test seams
		///<summary>Tests only: when set, requests go through this handler, behind the same VendorHttp policy, instead of the network.</summary>
		public static System.Net.Http.HttpMessageHandler HttpHandlerForTests;
		///<summary>Tests only: the clock used for the reconciliation date range.</summary>
		public static Func<DateTime> FuncGetNow=() => DateTime.Now;
		#endregion Test seams

		#region Settings and credentials
		///<summary>The host patterns this integration may reach or show: DentalXChange and its subdomains.</summary>
		public static IReadOnlyList<string> GetAllowedHostPatterns() {
			return _listAllowedHostPatterns;
		}

		///<summary>The office's integrator API key: the developer override (Introspection PayConnect2Key) if one is set, otherwise the practice-wide
		///"API Key" row (ClinicNum 0), decrypted. Read from the cache on every call, so a key changed in Setup applies at the next cache refresh.
		///Throws PayConnectNotConfiguredException when there is none.</summary>
		public static string GetApiKey() {
			string apiKey=GetIntrospectionOverride(Introspection.IntrospectionEntity.PayConnect2Key);
			if(string.IsNullOrWhiteSpace(apiKey)) {
				long programNum=GetProgramNum();
				if(programNum>0) {
					apiKey=VendorCredentials.ReadSecret(programNum,PayConnect.ProgramProperties.ApiKey,0,LegacyPolicy.AcceptPlaintext).PlainText;
				}
			}
			if(string.IsNullOrWhiteSpace(apiKey)) {
				throw new PayConnectNotConfiguredException("PayConnect API key is not set. Enter it in Setup, Program Links, PayConnect.");
			}
			return apiKey.Trim();
		}

		///<summary>The merchant Secret for the clinic (the clinic's row, else the ClinicNum 0 row), decrypted. A value Open Dental stored in plain text
		///is returned as-is. Returns "" when there is none or it can't be read. Never throws.</summary>
		public static string GetApiSecretForClinic(long clinicNum) {
			long programNum=GetProgramNum();
			if(programNum==0) {
				return "";
			}
			return VendorCredentials.ReadSecret(programNum,PayConnect.ProgramProperties.ApiSecret,clinicNum,LegacyPolicy.AcceptPlaintext).PlainText.Trim();
		}

		///<summary>True if the practice-wide "PayConnect2 Use Sandbox" box is ticked. Never throws.</summary>
		public static bool GetUseSandbox() {
			long programNum=GetProgramNum();
			if(programNum==0) {
				return false;
			}
			return PIn.Bool(ProgramProperties.GetValOrDefault(programNum,PayConnect.ProgramProperties.UseSandbox,0));
		}

		///<summary>True when PayConnect is enabled, the clinic uses version 2, and both the API key and the clinic's Secret are set. The payment windows
		///show their PayConnect 2 buttons only when this is true. Never throws.</summary>
		public static bool IsConfigured(long clinicNum) {
			try {
				Program program=Programs.GetCur(ProgramName.PayConnect);
				if(program==null || !program.Enabled) {
					return false;
				}
				if(ProgramProperties.GetValOrDefault(program.ProgramNum,PayConnect.ProgramProperties.ProgramVersion,clinicNum)!="2") {
					return false;
				}
				return HasCredentials(clinicNum);
			}
			catch(Exception ex) {
				ex.DoNothing();
				return false;
			}
		}

		///<summary>True if both the API key and the clinic's Secret are set. Doesn't check that they work. Never throws.</summary>
		public static bool HasCredentials(long clinicNum) {
			try {
				GetCredentials(clinicNum);
				return true;
			}
			catch(Exception ex) {
				ex.DoNothing();
				return false;
			}
		}

		///<summary>The base address: the developer override (Introspection PayConnectRestURL) if one is set, otherwise the sandbox or production address
		///per the "Use sandbox" setting.</summary>
		public static string GetApiBaseUrl() {
			string urlOverride=GetIntrospectionOverride(Introspection.IntrospectionEntity.PayConnectRestURL);
			if(!string.IsNullOrWhiteSpace(urlOverride)) {
				return urlOverride.Trim().TrimEnd('/');
			}
			return GetApiBaseUrl(GetUseSandbox());
		}

		///<summary>The sandbox or production base address. Never a staging or Open Dental host.</summary>
		public static string GetApiBaseUrl(bool useSandbox) {
			if(useSandbox) {
				return UrlBaseSandbox;
			}
			return UrlBaseProduction;
		}

		///<summary>The values one request needs, from the stored settings. secret, when given, replaces the clinic's stored Secret.
		///Throws PayConnectNotConfiguredException if the API key or the Secret is missing.</summary>
		public static ApiCredentials GetCredentials(long clinicNum,string secret="") {
			if(string.IsNullOrWhiteSpace(secret)) {
				secret=GetApiSecretForClinic(clinicNum);
			}
			ApiCredentials apiCredentials=MakeCredentials(GetApiKey(),secret,GetUseSandbox());
			return apiCredentials;
		}

		///<summary>Credentials from values typed in the setup window, before they are saved (Test connection). A blank apiKey falls back to the
		///developer override. Throws PayConnectNotConfiguredException if either value is missing.</summary>
		public static ApiCredentials MakeCredentials(string apiKey,string secret,bool useSandbox) {
			if(string.IsNullOrWhiteSpace(apiKey)) {
				apiKey=GetIntrospectionOverride(Introspection.IntrospectionEntity.PayConnect2Key);
			}
			if(string.IsNullOrWhiteSpace(apiKey)) {
				throw new PayConnectNotConfiguredException("PayConnect API key is not set. Enter it in Setup, Program Links, PayConnect.");
			}
			if(string.IsNullOrWhiteSpace(secret)) {
				throw new PayConnectNotConfiguredException("PayConnect is not set up for this clinic: the Secret is missing. Enter it in Setup, Program Links, PayConnect.");
			}
			ApiCredentials apiCredentials=new ApiCredentials();
			apiCredentials.ApiKey=apiKey.Trim();
			apiCredentials.Secret=secret.Trim();
			string urlOverride=GetIntrospectionOverride(Introspection.IntrospectionEntity.PayConnectRestURL);
			if(string.IsNullOrWhiteSpace(urlOverride)) {
				apiCredentials.BaseUrl=GetApiBaseUrl(useSandbox);
			}
			else {
				apiCredentials.BaseUrl=urlOverride.Trim().TrimEnd('/');
				apiCredentials.IsDeveloperOverride=true;
			}
			apiCredentials.IsSandbox=useSandbox;
			return apiCredentials;
		}

		///<summary>The two auth headers as "Name: value" lines, from the stored settings. Throws PayConnectNotConfiguredException if a value is
		///missing. The values are secrets: never log them or put them in an exception.</summary>
		public static List<string> GetHeadersForApi(long clinicNum,string secret="") {
			ApiCredentials apiCredentials=GetCredentials(clinicNum,secret);
			return GetHeadersForApi(apiCredentials.ApiKey,apiCredentials.Secret);
		}

		///<summary>The two auth headers as "Name: value" lines, for explicit values (Test connection before saving).</summary>
		public static List<string> GetHeadersForApi(string apiKey,string secret) {
			List<string> listHeaders=new List<string>();
			listHeaders.Add("API-Key: "+apiKey);
			listHeaders.Add("Secret: "+secret);
			return listHeaders;
		}

		///<summary>Returns value of Integration Type program property. 0= normal account, 1= Surcharge account.</summary>
		public static long GetIntegrationType(long clinicNum) {
			long programNum=GetProgramNum();
			if(programNum==0) {
				return 0;
			}
			return PIn.Long(ProgramProperties.GetValOrDefault(programNum,PayConnect.ProgramProperties.IntegrationType,clinicNum));
		}

		///<summary>Adds, in memory, the rows the PayConnect 2 setup needs and doesn't have yet: the practice-wide "API Key" and "PayConnect2 Use
		///Sandbox" rows (ClinicNum 0), and an "API Secret" row for ClinicNum 0 and each clinic in listClinicNums. New rows have ProgramPropertyNum 0, so
		///ProgramProperties.Sync inserts them on Save; nothing is written here. Also marks every API Key and API Secret row masked and high security,
		///because rows copied to a new clinic lost those flags. Idempotent. Returns the number of rows added.</summary>
		public static int AddMissingProperties(List<ProgramProperty> listProgramProperties,long programNum,List<long> listClinicNums) {
			int countAdded=0;
			if(!listProgramProperties.Exists(x => x.PropertyDesc==PayConnect.ProgramProperties.ApiKey && x.ClinicNum==0)) {
				listProgramProperties.Add(MakeProperty(programNum,PayConnect.ProgramProperties.ApiKey,0,"",isSecret:true));
				countAdded++;
			}
			if(!listProgramProperties.Exists(x => x.PropertyDesc==PayConnect.ProgramProperties.UseSandbox && x.ClinicNum==0)) {
				listProgramProperties.Add(MakeProperty(programNum,PayConnect.ProgramProperties.UseSandbox,0,"0",isSecret:false));
				countAdded++;
			}
			List<long> listClinicNumsAll=new List<long>() { 0 };
			if(listClinicNums!=null) {
				listClinicNumsAll.AddRange(listClinicNums.Where(x => x!=0));
			}
			listClinicNumsAll=listClinicNumsAll.Distinct().ToList();
			for(int i=0;i<listClinicNumsAll.Count;i++) {
				long clinicNum=listClinicNumsAll[i];
				if(listProgramProperties.Exists(x => x.PropertyDesc==PayConnect.ProgramProperties.ApiSecret && x.ClinicNum==clinicNum)) {
					continue;
				}
				listProgramProperties.Add(MakeProperty(programNum,PayConnect.ProgramProperties.ApiSecret,clinicNum,"",isSecret:true));
				countAdded++;
			}
			List<ProgramProperty> listSecrets=listProgramProperties.FindAll(x => x.PropertyDesc==PayConnect.ProgramProperties.ApiKey
				|| x.PropertyDesc==PayConnect.ProgramProperties.ApiSecret);
			for(int i=0;i<listSecrets.Count;i++) {
				listSecrets[i].IsMasked=true;
				listSecrets[i].IsHighSecurity=true;
			}
			return countAdded;
		}

		private static ProgramProperty MakeProperty(long programNum,string desc,long clinicNum,string value,bool isSecret) {
			ProgramProperty programProperty=new ProgramProperty();
			programProperty.ProgramNum=programNum;
			programProperty.PropertyDesc=desc;
			programProperty.PropertyValue=value;
			programProperty.ComputerName="";
			programProperty.ClinicNum=clinicNum;
			programProperty.IsMasked=isSecret;
			programProperty.IsHighSecurity=isSecret;
			return programProperty;
		}

		private static long GetProgramNum() {
			try {
				return Programs.GetProgramNum(ProgramName.PayConnect);
			}
			catch(Exception ex) {
				ex.DoNothing();
				return 0;
			}
		}

		///<summary>The developer override for entity, or "". Unlike Introspection.GetOverride, never throws.</summary>
		private static string GetIntrospectionOverride(Introspection.IntrospectionEntity entity) {
			try {
				string value;
				if(Introspection.TryGetOverride(entity,out value)) {
					return value??"";
				}
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
			return "";
		}
		#endregion Settings and credentials

		#region API calls
		///<summary>Throws exceptions.  Will purposefully throw ODExceptions that are already translated and formatted.</summary>
		public static PayConnect2Response PostCreateTransactionByToken(Patient pat,CreditCard cc,int payAmtInCents,long clinicNum,TransactionType transactionType=TransactionType.Sale,TransactionFrequency transactionFreq=TransactionFrequency.OneTime) {
			if(cc==null || string.IsNullOrWhiteSpace(cc.PayConnectToken) || pat==null) {
				throw new ODException("Error making payment by token");
			}
			return PostCreateTransactionByToken(pat,cc.CCExpiration.ToString("MMyy"),cc.PayConnectToken,cc.Zip,payAmtInCents,clinicNum,transactionType,transactionFreq);
		}

		///<summary>Charges or authorizes a saved card token. Sends a new correlation ID as invoiceNumber; if the response is lost, the transaction list is
		///searched for it (see PostCreateTransaction). The required cardHolder field carries the patient's name, because creditcard rows have no
		///cardholder name.</summary>
		public static PayConnect2Response PostCreateTransactionByToken(Patient pat,string ccExpiration,string token,string zipCode,int payAmtInCents,long clinicNum,TransactionType transactionType=TransactionType.Sale, TransactionFrequency transactionFreq=TransactionFrequency.OneTime) {
			long integrationType=GetIntegrationType(clinicNum);
			PayConnect2Response response;
			if(integrationType==1) {//Surcharge account
				CreateSurchargeTransactionRequest req=new CreateSurchargeTransactionRequest();
				req.Frequency=transactionFreq;
				req.TransType=transactionType;
				req.Amount=payAmtInCents;
				req.CardToken=token;
				req.Expiry=ccExpiration;
				req.CardHolder=pat.GetNameFLnoPref();
				req.ZipCode=zipCode;
				response=PostCreateSurchargeTransaction(req,clinicNum);
			}
			else {
				CreateTransactionRequest req=new CreateTransactionRequest();
				req.Frequency=transactionFreq;
				req.TransType=transactionType;
				req.Amount=payAmtInCents;
				req.CardToken=token;
				req.Expiry=ccExpiration;
				req.CardHolder=pat.GetNameFLnoPref();
				if(!zipCode.IsNullOrEmpty()) {
					req.ZipCode=zipCode;
				}
				response=PostCreateTransaction(req,clinicNum);
			}
			return response;
		}

		///<summary>Creates a token transaction. Sets InvoiceNumber to a new correlation ID if blank. Never retried automatically: if the request was sent
		///but no answer came, the transaction list is searched for the correlation ID, and a match comes back as a GetStatus response with Outcome
		///Reconciled.</summary>
		public static PayConnect2Response PostCreateTransaction(CreateTransactionRequest requestBody,long clinicNum) {
			if(string.IsNullOrWhiteSpace(requestBody.InvoiceNumber)) {
				requestBody.InvoiceNumber=NewCorrelationId();
			}
			return SendCreate(ApiRoute.CreateTransaction,clinicNum,requestBody.InvoiceNumber,SerializeBody(requestBody));
		}

		///<summary>As PostCreateTransaction, for surcharge accounts.</summary>
		public static PayConnect2Response PostCreateSurchargeTransaction(CreateSurchargeTransactionRequest requestBody,long clinicNum) {
			if(string.IsNullOrWhiteSpace(requestBody.InvoiceNumber)) {
				requestBody.InvoiceNumber=NewCorrelationId();
			}
			return SendCreate(ApiRoute.CreateTransactionSurcharge,clinicNum,requestBody.InvoiceNumber,SerializeBody(requestBody));
		}

		///<summary>Sale or authorization on a counter terminal. Waits up to TimeoutTerminal. Correlation and reconciliation as PostCreateTransaction.</summary>
		public static PayConnect2Response PostCreateTerminalTransaction(CreateTerminalTransactionRequest requestBody,long clinicNum) {
			if(string.IsNullOrWhiteSpace(requestBody.InvoiceNumber)) {
				requestBody.InvoiceNumber=NewCorrelationId();
			}
			return SendCreate(ApiRoute.CreateTerminalTransaction,clinicNum,requestBody.InvoiceNumber,SerializeBody(requestBody));
		}

		///<summary>Voids a transaction before its batch settles. The 2.12 schema accepts only referenceId.</summary>
		public static PayConnect2Response PutVoidWithReferenceID(VoidReferenceIDRequest requestBody,long clinicNum) {
			return SendForClinic(ApiRoute.VoidTransaction,HttpMethod.Put,clinicNum,SerializeBody(requestBody));
		}

		//Deprecated May 1st 2023 per DXC: refunds without a reference. Voids and refunds by invoice number are gone from the 2.12 schema too.

		///<summary>Refunds a settled transaction, in full or (with Amount) in part.</summary>
		public static PayConnect2Response PostRefundWithReferenceID(RefundReferenceIDRequest requestBody,long clinicNum) {
			return SendForClinic(ApiRoute.RefundTransaction,HttpMethod.Post,clinicNum,SerializeBody(requestBody));
		}

		///<summary>Attaches a signature image to a transaction.</summary>
		public static PayConnect2Response PutSignatureWithReferenceID(AddSignatureReferenceIDRequest requestBody,long clinicNum) {
			return SendForClinic(ApiRoute.AddSignature,HttpMethod.Put,clinicNum,SerializeBody(requestBody));
		}

		///<summary>Looks up one transaction by reference ID, transaction ID or embed-session order ID (any combination).</summary>
		public static PayConnect2Response GetTransactionStatus(long clinicNum,string refNumber,int transactionId=0,string orderId="") {
			return SendForClinic(ApiRoute.GetStatus,HttpMethod.Get,clinicNum,"",BuildStatusQuery(refNumber,transactionId,orderId));
		}

		///<summary>The query string for the status route, each value URL-encoded. "" if no value is given.</summary>
		public static string BuildStatusQuery(string refNumber,int transactionId=0,string orderId="") {
			List<string> listParams=new List<string>();
			if(!string.IsNullOrEmpty(refNumber)) {
				listParams.Add("referenceId="+Uri.EscapeDataString(refNumber));
			}
			if(transactionId>0) {
				listParams.Add("transactionId="+transactionId.ToString(CultureInfo.InvariantCulture));
			}
			if(!string.IsNullOrEmpty(orderId)) {
				listParams.Add("orderId="+Uri.EscapeDataString(orderId));
			}
			if(listParams.Count==0) {
				return "";
			}
			return "?"+string.Join("&",listParams);
		}

		///<summary>One page of the merchant's transactions between two dates (the API allows the last 30 days, at most 30 days apart).</summary>
		public static PayConnect2Response GetTransactionList(long clinicNum,DateTime dateFrom,DateTime dateTo,int page,int limit) {
			ApiCredentials apiCredentials;
			try {
				apiCredentials=GetCredentials(clinicNum);
			}
			catch(PayConnectNotConfiguredException ex) {
				return MakeNotConfiguredResponse(ex.Message);
			}
			return GetTransactionList(apiCredentials,dateFrom,dateTo,page,limit);
		}

		private static PayConnect2Response GetTransactionList(ApiCredentials apiCredentials,DateTime dateFrom,DateTime dateTo,int page,int limit) {
			if(Mock!=null) {
				return MockRequestResponse();
			}
			string queryStr="?dateFrom="+dateFrom.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)
				+"&dateTo="+dateTo.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)
				+"&page="+page.ToString(CultureInfo.InvariantCulture)
				+"&limit="+limit.ToString(CultureInfo.InvariantCulture);
			return Send(ApiRoute.GetTransactionList,HttpMethod.Get,apiCredentials,"",queryStr);
		}

		///<summary>Searches yesterday's and today's transactions for invoiceNumber, page by page. The API doesn't say which time zone its dates use, so
		///the extra day covers a transaction near midnight. Returns the transaction, or null. didLookupFail is true if a page couldn't be read, in
		///which case null proves nothing.</summary>
		public static GetStatusResponse FindTransactionByInvoiceNumber(long clinicNum,string invoiceNumber,out bool didLookupFail) {
			ApiCredentials apiCredentials;
			try {
				apiCredentials=GetCredentials(clinicNum);
			}
			catch(PayConnectNotConfiguredException ex) {
				ex.DoNothing();
				didLookupFail=true;
				return null;
			}
			return FindTransactionByInvoiceNumber(apiCredentials,invoiceNumber,out didLookupFail);
		}

		///<summary>As FindTransactionByInvoiceNumber, with explicit credentials.</summary>
		public static GetStatusResponse FindTransactionByInvoiceNumber(ApiCredentials apiCredentials,string invoiceNumber,out bool didLookupFail) {
			didLookupFail=false;
			if(string.IsNullOrWhiteSpace(invoiceNumber)) {
				return null;
			}
			DateTime dateTo=FuncGetNow().Date;
			DateTime dateFrom=dateTo.AddDays(-1);
			for(int page=1;page<=LIST_MAX_PAGES;page++) {
				PayConnect2Response response=GetTransactionList(apiCredentials,dateFrom,dateTo,page,LIST_PAGE_SIZE);
				if(response.ResponseType!=ResponseType.TransactionList || response.TransactionListResponse==null) {
					didLookupFail=true;
					return null;
				}
				List<GetStatusResponse> listTransactions=response.TransactionListResponse.ListTransactions;
				GetStatusResponse transaction=listTransactions.FirstOrDefault(x => x!=null && x.InvoiceNumber==invoiceNumber);
				if(transaction!=null) {
					return transaction;
				}
				if(listTransactions.Count<LIST_PAGE_SIZE) {
					return null;//Last page.
				}
				int totalPages=response.TransactionListResponse.TotalPages;
				if(totalPages>0 && page>=totalPages) {
					return null;
				}
			}
			return null;
		}

		///<summary>The merchant's terminals, for the setup window's import button.</summary>
		public static PayConnect2Response GetTerminals(ApiCredentials apiCredentials) {
			if(Mock!=null) {
				return MockRequestResponse();
			}
			return Send(ApiRoute.GetTerminals,HttpMethod.Get,apiCredentials,"");
		}

		///<summary>Gets a hosted card-entry URL (and its orderId). The API key and Secret stay in this process; only the URL goes to the browser control.</summary>
		public static PayConnect2Response PostEmbedSession(EmbedSessionRequest requestBody,long clinicNum) {
			return SendForClinic(ApiRoute.EmbedSession,HttpMethod.Post,clinicNum,SerializeBody(requestBody));
		}

		/// <summary>Merchant details for the clinic, using merchantSecret in place of the stored Secret when given.</summary>
		public static PayConnect2Response GetMerchantInfo(long clinicNum,string merchantSecret) {
			return SendForClinic(ApiRoute.GetMerchantInfo,HttpMethod.Get,clinicNum,"","",merchantSecret);
		}

		///<summary>Test connection from the setup window, with the typed values before they are saved: GET /merchants. On success the response carries
		///the merchant details; on failure ErrorResponse.Message says what was wrong.</summary>
		public static PayConnect2Response TestConnection(string apiKey,string secret,bool useSandbox) {
			ApiCredentials apiCredentials;
			try {
				apiCredentials=MakeCredentials(apiKey,secret,useSandbox);
			}
			catch(PayConnectNotConfiguredException ex) {
				return MakeNotConfiguredResponse(ex.Message);
			}
			if(Mock!=null) {
				return MockRequestResponse();
			}
			return Send(ApiRoute.GetMerchantInfo,HttpMethod.Get,apiCredentials,"");
		}

		///<summary>A new correlation ID: "OPMS-" and 12 hex characters of a new Guid. Opaque; never carries patient data.</summary>
		public static string NewCorrelationId() {
			return CorrelationIdPrefix+Guid.NewGuid().ToString("N").Substring(0,12);
		}
		#endregion API calls

		#region Transport
		private static string SerializeBody(object requestBody) {
			return JsonConvert.SerializeObject(requestBody,new JsonSerializerSettings { NullValueHandling=NullValueHandling.Ignore });
		}

		///<summary>Sends with the clinic's stored credentials. Missing credentials give an Error response (Outcome NotSent) rather than an exception, so a
		///recurring-charge batch marks that card failed and carries on.</summary>
		private static PayConnect2Response SendForClinic(ApiRoute route,HttpMethod method,long clinicNum,string body,string queryStr="",string secret="") {
			if(Mock!=null) {
				return MockRequestResponse();
			}
			ApiCredentials apiCredentials;
			try {
				apiCredentials=GetCredentials(clinicNum,secret);
			}
			catch(PayConnectNotConfiguredException ex) {
				return MakeNotConfiguredResponse(ex.Message);
			}
			return Send(route,method,apiCredentials,body,queryStr);
		}

		///<summary>Sends a create (POST) and reconciles a lost or unreadable response by invoiceNumber.</summary>
		private static PayConnect2Response SendCreate(ApiRoute route,long clinicNum,string invoiceNumber,string body) {
			if(Mock!=null) {
				return MockRequestResponse();
			}
			ApiCredentials apiCredentials;
			try {
				apiCredentials=GetCredentials(clinicNum);
			}
			catch(PayConnectNotConfiguredException ex) {
				return MakeNotConfiguredResponse(ex.Message);
			}
			return SendCreate(route,apiCredentials,invoiceNumber,body);
		}

		///<summary>As PostCreateTransaction, with explicit credentials. Used by tests and by callers that already hold the credentials.</summary>
		public static PayConnect2Response PostCreateTransaction(CreateTransactionRequest requestBody,ApiCredentials apiCredentials) {
			if(string.IsNullOrWhiteSpace(requestBody.InvoiceNumber)) {
				requestBody.InvoiceNumber=NewCorrelationId();
			}
			if(Mock!=null) {
				return MockRequestResponse();
			}
			return SendCreate(ApiRoute.CreateTransaction,apiCredentials,requestBody.InvoiceNumber,SerializeBody(requestBody));
		}

		private static PayConnect2Response SendCreate(ApiRoute route,ApiCredentials apiCredentials,string invoiceNumber,string body) {
			PayConnect2Response response=Send(route,HttpMethod.Post,apiCredentials,body);
			response.CorrelationId=invoiceNumber;
			//A 2xx whose body couldn't be read (an enum value this model doesn't know, say) may well be an approval: it is never a plain error.
			if(response.Outcome!=PayConnectRequestOutcome.SentNoResponse && response.Outcome!=PayConnectRequestOutcome.ResponseUnreadable) {
				return response;
			}
			return Reconcile(apiCredentials,invoiceNumber,response);
		}

		///<summary>After a create was sent but no answer came, or a 2xx answer came that couldn't be read: looks for the transaction by its
		///correlation ID. A list entry is only a pointer: an
		///entry whose status is not an approval (IsApprovedStatus) gives an Error response (Outcome Reconciled) that carries the vendor's status, as a
		///decline that arrived normally would. An approved entry is re-read with the status route so the result has the full model (amount, surcharge,
		///card); the re-read must also show an approval. Found and approved: a GetStatus response with Outcome Reconciled, as if the answer had
		///arrived. Not found, or the re-read failed: the original Error response, its message naming the correlation ID so staff can look for it in
		///the PayConnect portal. Never a success built from a list entry alone.</summary>
		private static PayConnect2Response Reconcile(ApiCredentials apiCredentials,string invoiceNumber,PayConnect2Response responseLost) {
			bool didLookupFail;
			GetStatusResponse transaction=FindTransactionByInvoiceNumber(apiCredentials,invoiceNumber,out didLookupFail);
			if(transaction!=null && !IsApprovedStatus(transaction.Status)) {
				return MakeReconciledDeclineResponse(invoiceNumber,transaction.Status);
			}
			if(transaction!=null) {
				GetStatusResponse transactionFull=ReadReconciledTransaction(apiCredentials,transaction);
				if(transactionFull==null) {
					didLookupFail=true;//The list had it, but its details couldn't be read: say "may or may not", never a $0 success.
				}
				else if(!IsApprovedStatus(transactionFull.Status)) {
					return MakeReconciledDeclineResponse(invoiceNumber,transactionFull.Status);
				}
				else {
					PayConnect2Response responseFound=new PayConnect2Response();
					responseFound.ResponseType=ResponseType.GetStatus;
					responseFound.GetStatusResponse=transactionFull;
					responseFound.Outcome=PayConnectRequestOutcome.Reconciled;
					responseFound.CorrelationId=invoiceNumber;
					responseFound.httpStatusCode=HttpStatusCode.OK;
					return responseFound;
				}
			}
			string message;
			if(responseLost.Outcome==PayConnectRequestOutcome.ResponseUnreadable) {
				message="PayConnect answered, but its answer could not be read";
			}
			else {
				message="PayConnect did not answer";
			}
			if(didLookupFail) {
				message+=", and its transaction list could not be checked. The payment may or may not have gone through. "
					+"Look for invoice number "+invoiceNumber+" in the PayConnect portal before trying again.";
			}
			else {
				message+=", and the transaction was not found in PayConnect's transaction list. The payment may or may not have "
					+"gone through. Look for invoice number "+invoiceNumber+" in the PayConnect portal before trying again.";
			}
			responseLost.ErrorResponse.Error=message;
			responseLost.ErrorResponse.Message=message;
			return responseLost;
		}

		///<summary>True if a transaction status shows the money was approved: Processed, Authorized or Approved, ignoring case. Anything else
		///(Declined, Error, Voided, blank, a status this code doesn't know) is not an approval.</summary>
		public static bool IsApprovedStatus(string status) {
			string statusTrimmed=(status??"").Trim();
			return statusTrimmed.Equals("Processed",StringComparison.OrdinalIgnoreCase)
				|| statusTrimmed.Equals("Authorized",StringComparison.OrdinalIgnoreCase)
				|| statusTrimmed.Equals("Approved",StringComparison.OrdinalIgnoreCase);
		}

		///<summary>Re-reads a transaction found in the list with GET /transactions/status, by reference ID, or by transaction ID when the entry has no
		///reference ID. Returns the full model, or null if it couldn't be read (or the entry has neither ID).</summary>
		private static GetStatusResponse ReadReconciledTransaction(ApiCredentials apiCredentials,GetStatusResponse transactionListed) {
			string queryStr;
			if(!string.IsNullOrWhiteSpace(transactionListed.ReferenceId)) {
				queryStr=BuildStatusQuery(transactionListed.ReferenceId);
			}
			else {
				queryStr=BuildStatusQuery("",transactionListed.TransactionId);
			}
			if(queryStr=="") {
				return null;
			}
			PayConnect2Response response=Send(ApiRoute.GetStatus,HttpMethod.Get,apiCredentials,"",queryStr);
			if(response.ResponseType!=ResponseType.GetStatus || response.GetStatusResponse==null) {
				return null;
			}
			return response.GetStatusResponse;
		}

		///<summary>The answer for a lost create that PayConnect's records show was not approved: an Error response, as a decline that arrived normally
		///would be, whose message carries the vendor's status.</summary>
		private static PayConnect2Response MakeReconciledDeclineResponse(string invoiceNumber,string status) {
			string statusShown=string.IsNullOrWhiteSpace(status) ? "unknown" : status.Trim();
			PayConnect2Response response=new PayConnect2Response();
			response.ResponseType=ResponseType.Error;
			response.Outcome=PayConnectRequestOutcome.Reconciled;//The answer came from the transaction list, not from the create itself.
			response.CorrelationId=invoiceNumber;
			response.httpStatusCode=HttpStatusCode.OK;
			response.ErrorResponse=MakeErrorResponse(PayConnectErrorKind.Vendor,"PayConnect declined the transaction (status: "+statusShown+").");
			return response;
		}

		///<summary>Sends one request through the vendor HTTP policy (DentalXChange hosts only, HTTPS, never an Open Dental host). Only GET status and list
		///calls are retried, once. Never throws for network or HTTP errors: those come back as ResponseType.Error with an Outcome that says whether the
		///request left this computer.</summary>
		private static PayConnect2Response Send(ApiRoute route,HttpMethod method,ApiCredentials apiCredentials,string body,string queryStr="") {
			if(Mock!=null) {
				return MockRequestResponse();
			}
			HttpStatusCode statusCode;
			string responseBody;
			try {
				HttpClient httpClient=GetHttpClient(apiCredentials.IsDeveloperOverride);
				Tuple<HttpStatusCode,string> tupleResult=VendorHttp.RunSync(async () => {
					using(HttpRequestMessage request=new HttpRequestMessage(method,GetApiUrl(apiCredentials.BaseUrl,route)+queryStr)) {
						request.Headers.TryAddWithoutValidation("API-Key",apiCredentials.ApiKey);
						request.Headers.TryAddWithoutValidation("Secret",apiCredentials.Secret);
						request.Headers.Accept.ParseAdd("application/json");
						if(method!=HttpMethod.Get) {
							request.Content=new StringContent(body??"",Encoding.UTF8,"application/json");
						}
						VendorHttp.SetEndpointName(request,route.ToString());
						VendorHttp.SetTimeout(request,(route==ApiRoute.CreateTerminalTransaction) ? TimeoutTerminal : TimeoutDefault);
						if(route!=ApiRoute.GetStatus && route!=ApiRoute.GetTransactionList) {
							VendorHttp.SetNoRetry(request);
						}
						using(HttpResponseMessage httpResponse=await httpClient.SendAsync(request).ConfigureAwait(false)) {
							string text="";
							if(httpResponse.Content!=null) {
								text=await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
							}
							return Tuple.Create(httpResponse.StatusCode,text);
						}
					}
				});
				statusCode=tupleResult.Item1;
				responseBody=tupleResult.Item2;
			}
			catch(Exception ex) {
				return MakeTransportErrorResponse(ClassifyFailure(ex));
			}
			return ParseHttpResponse(route,statusCode,responseBody);
		}

		///<summary>Turns an HTTP status and body into a response. 2xx: the route's model, or, if the body can't be read, an Error response with Outcome
		///ResponseUnreadable (the request may have been processed). Anything else: an Error response from ParseErrorResponse. Public for tests.</summary>
		public static PayConnect2Response ParseHttpResponse(ApiRoute route,HttpStatusCode statusCode,string responseBody) {
			int code=(int)statusCode;
			if(code>=200 && code<300) {
				try {
					PayConnect2Response responseParsed=DeserializeRawResponse(responseBody,route);
					responseParsed.httpStatusCode=statusCode;
					responseParsed.Outcome=PayConnectRequestOutcome.Response;
					return responseParsed;
				}
				catch(Exception ex) {
					ex.DoNothing();
					//The body is never put in the message: it can carry card or patient details. A 2xx means PayConnect accepted the request, so this
					//is not a plain failure: creates are reconciled by their correlation ID, and the user is warned that it may have gone through.
					PayConnect2Response responseBad=new PayConnect2Response();
					responseBad.ResponseType=ResponseType.Error;
					responseBad.httpStatusCode=statusCode;
					responseBad.Outcome=PayConnectRequestOutcome.ResponseUnreadable;
					responseBad.ErrorResponse=MakeErrorResponse(PayConnectErrorKind.NoAnswer,MSG_UNREADABLE);
					return responseBad;
				}
			}
			PayConnect2Response response=new PayConnect2Response();
			response.ResponseType=ResponseType.Error;
			response.httpStatusCode=statusCode;
			response.Outcome=PayConnectRequestOutcome.Response;
			response.ErrorResponse=ParseErrorResponse(statusCode,responseBody);
			return response;
		}

		///<summary>Maps an error body to an ErrorResponse whose Message (and Error) is a sentence for the user. Tells the key and Secret 401s apart by
		///the body's shape, never by its text: a plain-string "error" (or any 403) means the API key; an object means the Secret. 400 VALIDATION lists
		///each "param: message". Other errors keep the vendor's message, which may say "declined".</summary>
		public static ErrorResponse ParseErrorResponse(HttpStatusCode statusCode,string responseBody) {
			int code=(int)statusCode;
			JObject jObject=null;
			try {
				if(!string.IsNullOrWhiteSpace(responseBody)) {
					jObject=JToken.Parse(responseBody) as JObject;
				}
			}
			catch(JsonException ex) {
				ex.DoNothing();
			}
			JToken jTokenError=jObject?["error"];
			JToken jTokenErrorType=jObject?["errorType"];
			string errorType=(jTokenErrorType!=null && jTokenErrorType.Type==JTokenType.String) ? (string)jTokenErrorType : "";
			ErrorResponse errorResponse;
			if(errorType.Equals("VALIDATION",StringComparison.OrdinalIgnoreCase) || (code==400 && jTokenError is JArray)) {
				List<ValidationErrorBody> listValidationErrors=ParseValidationErrors(jTokenError);
				List<string> listLines=listValidationErrors
					.Select(x => string.IsNullOrWhiteSpace(x.Param) ? (x.Message??"") : x.Param+": "+(x.Message??""))
					.Where(x => !string.IsNullOrWhiteSpace(x))
					.ToList();
				string message="PayConnect did not accept the request.";
				if(listLines.Count>0) {
					message+=" "+string.Join("; ",listLines);
				}
				errorResponse=MakeErrorResponse(PayConnectErrorKind.Validation,message);
				errorResponse.ErrorType=ErrorType.Validation;
				errorResponse.ListValidationErrors=listValidationErrors;
			}
			else if(code==403 || (code==401 && jTokenError!=null && jTokenError.Type==JTokenType.String)) {
				errorResponse=MakeErrorResponse(PayConnectErrorKind.ApiKeyRejected,MSG_API_KEY_REJECTED);
			}
			else if(code==401) {
				errorResponse=MakeErrorResponse(PayConnectErrorKind.SecretRejected,MSG_SECRET_REJECTED);
			}
			else {
				string vendorMessage="";
				if(jTokenError!=null && jTokenError.Type==JTokenType.String) {
					vendorMessage=(string)jTokenError;
				}
				else if(jTokenError is JObject && jTokenError["message"]!=null && jTokenError["message"].Type==JTokenType.String) {
					vendorMessage=(string)jTokenError["message"];
				}
				if(string.IsNullOrWhiteSpace(vendorMessage)) {
					vendorMessage="PayConnect returned an error (HTTP "+code.ToString(CultureInfo.InvariantCulture)+").";
				}
				errorResponse=MakeErrorResponse(PayConnectErrorKind.Vendor,vendorMessage);
			}
			errorResponse.HttpStatus=code;
			return errorResponse;
		}

		private static List<ValidationErrorBody> ParseValidationErrors(JToken jTokenError) {
			List<ValidationErrorBody> listValidationErrors=new List<ValidationErrorBody>();
			JArray jArray=jTokenError as JArray;
			if(jArray==null && jTokenError is JObject) {
				jArray=jTokenError["properties"] as JArray ?? jTokenError["errors"] as JArray;
			}
			if(jArray==null) {
				return listValidationErrors;
			}
			foreach(JToken jToken in jArray) {
				JObject jObjectItem=jToken as JObject;
				if(jObjectItem==null) {
					continue;
				}
				ValidationErrorBody validationErrorBody=new ValidationErrorBody();
				validationErrorBody.Message=GetString(jObjectItem,"message");
				validationErrorBody.Param=GetString(jObjectItem,"param");
				validationErrorBody.Location=GetString(jObjectItem,"location");
				//The rejected value is left out on purpose: it can be card or patient data.
				listValidationErrors.Add(validationErrorBody);
			}
			return listValidationErrors;
		}

		private static string GetString(JObject jObject,string name) {
			JToken jToken=jObject[name];
			if(jToken==null || jToken.Type==JTokenType.Null) {
				return "";
			}
			if(jToken.Type==JTokenType.String || jToken.Type==JTokenType.Integer || jToken.Type==JTokenType.Float || jToken.Type==JTokenType.Boolean) {
				return jToken.ToString();
			}
			return "";
		}

		private static ErrorResponse MakeErrorResponse(PayConnectErrorKind errorKind,string message) {
			ErrorResponse errorResponse=new ErrorResponse();
			errorResponse.ErrorType=ErrorType.Response;
			errorResponse.Kind=errorKind;
			errorResponse.Error=message;
			errorResponse.Message=message;
			return errorResponse;
		}

		private static PayConnect2Response MakeNotConfiguredResponse(string message) {
			PayConnect2Response response=new PayConnect2Response();
			response.ResponseType=ResponseType.Error;
			response.Outcome=PayConnectRequestOutcome.NotSent;
			response.ErrorResponse=MakeErrorResponse(PayConnectErrorKind.NotConfigured,message);
			return response;
		}

		private static PayConnect2Response MakeTransportErrorResponse(PayConnectRequestOutcome outcome) {
			PayConnect2Response response=new PayConnect2Response();
			response.ResponseType=ResponseType.Error;
			response.Outcome=outcome;
			if(outcome==PayConnectRequestOutcome.NotSent) {
				response.ErrorResponse=MakeErrorResponse(PayConnectErrorKind.Network,MSG_NOT_SENT);
			}
			else {
				response.ErrorResponse=MakeErrorResponse(PayConnectErrorKind.NoAnswer,MSG_NO_ANSWER);
			}
			return response;
		}

		///<summary>Whether a failed call left this computer. NotSent only when it certainly didn't: the URL was refused, the vendor was marked
		///unreachable, or the connection, DNS lookup or TLS handshake failed. A timeout or a dropped connection after sending is SentNoResponse, and so
		///is anything unrecognized, so a create gets reconciled rather than repeated.</summary>
		public static PayConnectRequestOutcome ClassifyFailure(Exception ex) {
			//Walk the chain: RunSync unwraps the task, but HttpClient nests the WebException and SocketException that say what happened.
			for(Exception exCur=ex;exCur!=null;exCur=exCur.InnerException) {
				if(exCur is VendorHttpException vendorHttpException) {
					if(vendorHttpException.Failure==VendorHttpFailure.Timeout) {
						return PayConnectRequestOutcome.SentNoResponse;
					}
					return PayConnectRequestOutcome.NotSent;//URL refused, circuit open, or too many redirects (GET only).
				}
				if(exCur is WebException webException) {
					switch(webException.Status) {
						case WebExceptionStatus.NameResolutionFailure:
						case WebExceptionStatus.ProxyNameResolutionFailure:
						case WebExceptionStatus.ConnectFailure:
						case WebExceptionStatus.SecureChannelFailure:
						case WebExceptionStatus.TrustFailure:
							return PayConnectRequestOutcome.NotSent;
						default:
							return PayConnectRequestOutcome.SentNoResponse;
					}
				}
				if(exCur is SocketException socketException) {
					switch(socketException.SocketErrorCode) {
						case SocketError.HostNotFound:
						case SocketError.TryAgain:
						case SocketError.NoData:
						case SocketError.ConnectionRefused:
						case SocketError.NetworkUnreachable:
						case SocketError.HostUnreachable:
						case SocketError.NetworkDown:
							return PayConnectRequestOutcome.NotSent;
						default:
							return PayConnectRequestOutcome.SentNoResponse;
					}
				}
			}
			return PayConnectRequestOutcome.SentNoResponse;
		}

		private static HttpClient GetHttpClient(bool isDeveloperOverride) {
			VendorHttpOptions vendorHttpOptions=new VendorHttpOptions();
			vendorHttpOptions.VendorName=VendorName;
			if(isDeveloperOverride) {
				//A developer's mock server: still never an Open Dental host, still HTTPS unless it is this computer.
				vendorHttpOptions.VendorName=VendorName+" (developer override)";
			}
			else {
				vendorHttpOptions.ListAllowedHosts.AddRange(_listAllowedHostPatterns);
			}
			vendorHttpOptions.Timeout=TimeoutDefault;
			vendorHttpOptions.MaxGetRetries=1;
			vendorHttpOptions.RetryBaseDelay=TimeSpan.FromSeconds(2);
			vendorHttpOptions.MaxRetryWait=TimeSpan.FromSeconds(10);
			if(HttpHandlerForTests!=null) {
				return VendorHttp.CreateClient(vendorHttpOptions,HttpHandlerForTests);
			}
			return VendorHttp.GetClient(vendorHttpOptions);
		}

		///<summary>Returns the full URL for the route under baseUrl.</summary>
		private static string GetApiUrl(string baseUrl,ApiRoute route) {
			string apiUrl=baseUrl;
			switch(route) {
				case ApiRoute.Root:
					//Do nothing.  This is to allow someone to quickly grab the URL without having to make a copy+paste reference.
					break;
				case ApiRoute.CreateTransaction:
				case ApiRoute.CreateTransactionSurcharge:
					apiUrl+="/transactions";
					break;
				case ApiRoute.VoidTransaction:
					apiUrl+="/transactions/voids";
					break;
				case ApiRoute.RefundTransaction:
					apiUrl+="/transactions/refunds";
					break;
				case ApiRoute.AddSignature:
					apiUrl+="/transactions/signature";
					break;
				case ApiRoute.EmbedSession:
					apiUrl+="/embeds/sessions";
					break;
				case ApiRoute.GetStatus:
					apiUrl+="/transactions/status";
					break;
				case ApiRoute.CreateTerminalTransaction:
					apiUrl+="/terminals/transactions";
					break;
				case ApiRoute.GetMerchantInfo:
					apiUrl+="/merchants";
					break;
				case ApiRoute.GetTransactionList:
					apiUrl+="/transactions/list";
					break;
				case ApiRoute.GetTerminals:
					apiUrl+="/terminals";
					break;
				default:
					break;
			}
			return apiUrl;
		}

		public enum ApiRoute {
			Root,
			AddSignature,
			EmbedSession,
			CreateTransaction,
			CreateTransactionSurcharge,//Same route as CreateTransaction but helps us to differentiate for serializing/deserializing
			RefundTransaction,
			VoidTransaction,
			GetStatus,
			CreateTerminalTransaction,
			GetMerchantInfo,
			GetTransactionList,
			GetTerminals,
		}
		#endregion Transport

		#region Hosted card-entry page
		///<summary>True if url is an https address on a DentalXChange host (AllowedDomain or a subdomain), and not an Open Dental host. The WebView2
		///windows refuse to load anything else as their top-level page.</summary>
		public static bool IsAllowedHostedFormUrl(string url) {
			Uri uri;
			if(string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(),UriKind.Absolute,out uri)) {
				return false;
			}
			if(uri.Scheme!=Uri.UriSchemeHttps) {
				return false;
			}
			return VendorUrlPolicy.Check(uri,_listAllowedHostPatterns)==VendorUrlProblem.None && !VendorUrlPolicy.IsLoopbackHost(uri.Host);
		}

		///<summary>True if origin (a postMessage event.origin such as "https://pay.dentalxchange.com") is https on a DentalXChange host. Look-alikes such
		///as "https://dentalxchange.com.example.test" or "https://evildentalxchange.com" are refused.</summary>
		public static bool IsAllowedHostedFormOrigin(string origin) {
			Uri uri;
			if(string.IsNullOrWhiteSpace(origin) || !Uri.TryCreate(origin.Trim(),UriKind.Absolute,out uri)) {
				return false;
			}
			if(uri.AbsolutePath!="/" || uri.Query!="" || uri.Fragment!="" || uri.UserInfo!="") {
				return false;//An origin is scheme, host and port only.
			}
			return IsAllowedHostedFormUrl(origin);
		}

		///<summary>The script injected into the hosted card-entry page. It relays a window message to this program only when the message's origin is
		///https on a DentalXChange host, and passes that origin along so the C# side checks it again. Messages from any other frame or origin are
		///dropped, so a page or frame that isn't DentalXChange's can't post a fake result.</summary>
		public static string GetHostedFormRelayScript() {
			return "(function(){"
				+"var domain='"+AllowedDomain+"';"
				+"function isAllowed(origin){"
					+"try{"
						+"var u=new URL(origin);"
						+"if(u.protocol!=='https:'){return false;}"
						+"var h=u.hostname.toLowerCase();"
						+"return h===domain||h.endsWith('.'+domain);"
					+"}catch(e){return false;}"
				+"}"
				+"window.addEventListener('message',function(e){"
					+"if(!isAllowed(e.origin)){return;}"
					+"window.chrome.webview.postMessage({origin:e.origin,data:e.data});"
				+"});"
			+"})();";
		}

		///<summary>Parses a message from the relay script: {origin, data}, where data is the hosted page's {status, response}, as an object or as JSON
		///text. Returns null for anything else. Check IsFromAllowedOrigin before acting on it.</summary>
		public static HostedFormMessage ParseHostedFormMessage(string webMessageAsJson) {
			if(string.IsNullOrWhiteSpace(webMessageAsJson)) {
				return null;
			}
			try {
				JObject jObjectEnvelope=JToken.Parse(webMessageAsJson) as JObject;
				if(jObjectEnvelope==null) {
					return null;
				}
				JToken jTokenOrigin=jObjectEnvelope["origin"];
				JToken jTokenData=jObjectEnvelope["data"];
				if(jTokenOrigin==null || jTokenOrigin.Type!=JTokenType.String || jTokenData==null) {
					return null;
				}
				if(jTokenData.Type==JTokenType.String) {
					jTokenData=JToken.Parse((string)jTokenData);
				}
				JObject jObjectData=jTokenData as JObject;
				if(jObjectData==null) {
					return null;
				}
				iFrameResponse response=jObjectData.ToObject<iFrameResponse>();
				if(response==null || string.IsNullOrEmpty(response.IFrameStatus)) {
					return null;
				}
				HostedFormMessage hostedFormMessage=new HostedFormMessage();
				hostedFormMessage.Origin=(string)jTokenOrigin;
				hostedFormMessage.IsFromAllowedOrigin=IsAllowedHostedFormOrigin(hostedFormMessage.Origin);
				hostedFormMessage.Response=response;
				return hostedFormMessage;
			}
			catch(Exception ex) {
				ex.DoNothing();
				return null;
			}
		}

		///<summary>True if value looks like magnetic-stripe track 2 data (";account=...?") rather than a token.</summary>
		public static bool IsTrackTwoData(string value) {
			return !string.IsNullOrEmpty(value) && value.StartsWith(";") && value.EndsWith("?") && value.Contains("=");
		}

		///<summary>True if value is 13 to 19 digits that pass the Luhn check: a card number, which must never be stored as a token.</summary>
		public static bool LooksLikeCardNumber(string value) {
			if(string.IsNullOrEmpty(value) || value.Length<13 || value.Length>19 || !value.All(x => x>='0' && x<='9')) {
				return false;
			}
			int sum=0;
			bool isDouble=false;
			for(int i=value.Length-1;i>=0;i--) {
				int digit=value[i]-'0';
				if(isDouble) {
					digit*=2;
					if(digit>9) {
						digit-=9;
					}
				}
				sum+=digit;
				isDouble=!isDouble;
			}
			return sum%10==0;
		}
		#endregion Hosted card-entry page

		#region Converters

		/// <summary>Can throw exceptions if deserialization fails.</summary>
		public static PayConnect2Response DeserializeRawResponse(string rawResponse,ApiRoute route) {
			PayConnect2Response payConnect2Response=new PayConnect2Response();
			JsonSerializerSettings settings=new JsonSerializerSettings();
        	settings.NullValueHandling=NullValueHandling.Ignore;
        	settings.MissingMemberHandling=MissingMemberHandling.Ignore;
			switch(route) {
				case ApiRoute.CreateTransaction:
					payConnect2Response.TransactionResponse=JsonConvert.DeserializeObject<CreateTransactionResponse>(rawResponse,settings);
					payConnect2Response.ResponseType=ResponseType.CreateTransaction;
						break;
				case ApiRoute.CreateTransactionSurcharge:
					payConnect2Response.TransactionSurchargeResponse=JsonConvert.DeserializeObject<CreateSurchargeTransactionResponse>(rawResponse,settings);
					payConnect2Response.ResponseType=ResponseType.CreateSurchargeTransaction;
						break;
				case ApiRoute.VoidTransaction:
					payConnect2Response.VoidResponse=JsonConvert.DeserializeObject<VoidResponse>(rawResponse,settings);
					payConnect2Response.ResponseType=ResponseType.Void;
						break;
				case ApiRoute.RefundTransaction:
					payConnect2Response.RefundResponse=JsonConvert.DeserializeObject<RefundResponse>(rawResponse,settings);
					payConnect2Response.ResponseType=ResponseType.Refund;
						break;
				case ApiRoute.AddSignature:
					payConnect2Response.SignatureResponse=JsonConvert.DeserializeObject<AddSignatureResponse>(rawResponse,settings);
					payConnect2Response.ResponseType=ResponseType.AddSignature;
						break;
				case ApiRoute.EmbedSession:
					payConnect2Response.EmbedSessionResponse=JsonConvert.DeserializeObject<EmbedSessionResponse>(rawResponse,settings);
					payConnect2Response.ResponseType=ResponseType.EmbedSession;
						break;
				case ApiRoute.GetStatus:
					payConnect2Response.GetStatusResponse=JsonConvert.DeserializeObject<GetStatusResponse>(rawResponse,settings);
					payConnect2Response.ResponseType=ResponseType.GetStatus;
					break;
				case ApiRoute.CreateTerminalTransaction:
					payConnect2Response.TerminalTransactionResponse=JsonConvert.DeserializeObject<TerminalTransactionResponse>(rawResponse,settings);
					payConnect2Response.ResponseType=ResponseType.TerminalTransaction;
					break;
				case ApiRoute.GetMerchantInfo:
					payConnect2Response.GetMerchantInfoResponse=JsonConvert.DeserializeObject<GetMerchantInfoResponse>(rawResponse,settings);
					payConnect2Response.ResponseType=ResponseType.MerchantInfo;
					break;
				case ApiRoute.GetTransactionList:
					payConnect2Response.TransactionListResponse=ParseTransactionList(rawResponse,settings);
					payConnect2Response.ResponseType=ResponseType.TransactionList;
					break;
				case ApiRoute.GetTerminals:
					payConnect2Response.ListTerminals=ParseTerminals(rawResponse);
					payConnect2Response.ResponseType=ResponseType.Terminals;
					break;
				default:
						break;
			}
			return payConnect2Response;
		}

		///<summary>The transactions in a /transactions/list page. The 2.12 file's paging envelope is read loosely: the body may be an array, or an object
		///whose first array property ("transactions", "data", "items" or "results") holds them. Throws if the body isn't JSON.</summary>
		public static TransactionListResponse ParseTransactionList(string rawResponse,JsonSerializerSettings settings=null) {
			TransactionListResponse transactionListResponse=new TransactionListResponse();
			JToken jTokenRoot=JToken.Parse(rawResponse??"");
			JArray jArray=FindArray(jTokenRoot,"transactions","data","items","results");
			if(jTokenRoot is JObject jObjectRoot) {
				transactionListResponse.TotalPages=GetInt(jObjectRoot,"totalPages","pages");
			}
			if(jArray==null) {
				return transactionListResponse;
			}
			JsonSerializer jsonSerializer=JsonSerializer.Create(settings??new JsonSerializerSettings {
				NullValueHandling=NullValueHandling.Ignore,MissingMemberHandling=MissingMemberHandling.Ignore });
			foreach(JToken jToken in jArray) {
				JObject jObject=jToken as JObject;
				if(jObject==null) {
					continue;
				}
				GetStatusResponse transaction;
				try {
					transaction=jObject.ToObject<GetStatusResponse>(jsonSerializer);
				}
				catch(Exception ex) {
					ex.DoNothing();
					//An entry with a value this model doesn't know (a new type or source) still counts for matching.
					transaction=new GetStatusResponse();
					transaction.TransactionId=GetInt(jObject,"transactionId");
					transaction.ReferenceId=GetString(jObject,"referenceId");
					transaction.InvoiceNumber=GetString(jObject,"invoiceNumber");
					transaction.DocumentNumber=GetString(jObject,"documentNumber");
					transaction.OrderId=GetString(jObject,"orderId");
					transaction.Status=GetString(jObject,"status");
					transaction.AuthCode=GetString(jObject,"authCode");
				}
				transactionListResponse.ListTransactions.Add(transaction);
			}
			return transactionListResponse;
		}

		///<summary>The terminals in a /terminals body: an array, or an object holding one ("terminals", "data" or "items"). Throws if the body isn't JSON.</summary>
		public static List<PayConnectTerminalInfo> ParseTerminals(string rawResponse) {
			List<PayConnectTerminalInfo> listTerminals=new List<PayConnectTerminalInfo>();
			JArray jArray=FindArray(JToken.Parse(rawResponse??""),"terminals","data","items");
			if(jArray==null) {
				return listTerminals;
			}
			foreach(JToken jToken in jArray) {
				JObject jObject=jToken as JObject;
				if(jObject==null) {
					continue;
				}
				PayConnectTerminalInfo payConnectTerminalInfo=new PayConnectTerminalInfo();
				payConnectTerminalInfo.TerminalId=GetString(jObject,"terminalId");
				payConnectTerminalInfo.Serial=GetString(jObject,"terminal");
				payConnectTerminalInfo.Name=GetString(jObject,"name");
				payConnectTerminalInfo.TerminalType=GetString(jObject,"type");
				listTerminals.Add(payConnectTerminalInfo);
			}
			return listTerminals;
		}

		private static JArray FindArray(JToken jTokenRoot,params string[] arrayNames) {
			if(jTokenRoot is JArray jArrayRoot) {
				return jArrayRoot;
			}
			JObject jObject=jTokenRoot as JObject;
			if(jObject==null) {
				return null;
			}
			for(int i=0;i<arrayNames.Length;i++) {
				if(jObject[arrayNames[i]] is JArray jArray) {
					return jArray;
				}
			}
			return jObject.Properties().Select(x => x.Value).OfType<JArray>().FirstOrDefault();
		}

		private static int GetInt(JObject jObject,params string[] arrayNames) {
			for(int i=0;i<arrayNames.Length;i++) {
				JToken jToken=jObject[arrayNames[i]];
				if(jToken!=null && jToken.Type==JTokenType.Integer) {
					return (int)jToken;
				}
			}
			return 0;
		}

		/// <summary>Throws exceptions. The PayConnect2 API accepts an integer amount field in total cents i.e. $13.62 should be sent as 1362. This method takes in a double as that is what Open Dental generally uses to store dollar amounts and multiplies it by 100 to move the decimal 2 places and then casts to an int.</summary>
		public static int FormatAmountForApi(double amount) {
			try {
				return Convert.ToInt32(amount*100);
			}
			catch(Exception ex) {
				throw new ODException("Unable to convert transaction amount for PayConnect.",ex);
			}
		}

		/// <summary>Open Dental stores a double for monetary amounts. This method takes in an int as PayConnect2 represents money in cents and divides it by 100 to move the decimal 2 places and then casts to a double.</summary>
		public static double FormatAmountForOpenDental(int amount) {
			return ((double)amount)/100;
		}

		///<summary>Converts an any response we may have gotten from the API to the old PayConnectResponse class.</summary>
		public static PayConnectResponse ApiResponseToPayConnectResponse(PayConnect2Response response) {
			PayConnectResponse payConnectResponse=new PayConnectResponse();
			switch(response.ResponseType) {
				case ResponseType.CreateTransaction:
					CreateTransactionResponse transactionResponse=response.TransactionResponse;
					payConnectResponse.Description=transactionResponse.Status;
					payConnectResponse.StatusCode="0";//0 indicates success
					payConnectResponse.RefNumber=transactionResponse.ReferenceId;
					payConnectResponse.Amount=((decimal)transactionResponse.Amount)/100;//PayConnect sends amount as total cents, convert back to OD decimal amounts.
					payConnectResponse.AuthCode=transactionResponse.AuthCode;
					payConnectResponse.MerchantId=transactionResponse.MerchantId.ToString();
					payConnectResponse.PaymentToken=GetCard(transactionResponse.PaymentMethod).CardToken;
					payConnectResponse.CardType=GetCard(transactionResponse.PaymentMethod).Network.ToString();
					payConnectResponse.CardNumber=GetCard(transactionResponse.PaymentMethod).CardLast4Digits;
					payConnectResponse.TransType=PayConnectResponse.TransactionType.Sale;
					payConnectResponse.CardHolder=GetCard(transactionResponse.PaymentMethod).CardHolder;
					break;
				case ResponseType.CreateSurchargeTransaction:
					CreateSurchargeTransactionResponse transactionSurchargeResponse=response.TransactionSurchargeResponse;
					payConnectResponse.Description=transactionSurchargeResponse.Status;
					payConnectResponse.StatusCode="0";//0 indicates success
					payConnectResponse.RefNumber=transactionSurchargeResponse.ReferenceId;
					payConnectResponse.Amount=((decimal)transactionSurchargeResponse.Amount)/100;//PayConnect sends amount as total cents, convert back to OD decimal amounts.
					payConnectResponse.SurchargePercent=transactionSurchargeResponse.SurchargePercent;
					payConnectResponse.AmountSurcharged=((decimal)transactionSurchargeResponse.AmountSurcharged)/100;//PayConnect sends amount as total cents, convert back to OD decimal amounts.
					payConnectResponse.AuthCode=transactionSurchargeResponse.AuthCode;
					payConnectResponse.MerchantId=transactionSurchargeResponse.MerchantId.ToString();
					payConnectResponse.PaymentToken=GetCard(transactionSurchargeResponse.PaymentMethod).CardToken;
					payConnectResponse.CardType=GetCard(transactionSurchargeResponse.PaymentMethod).Network.ToString();
					payConnectResponse.CardNumber=GetCard(transactionSurchargeResponse.PaymentMethod).CardLast4Digits;
					payConnectResponse.TransType=PayConnectResponse.TransactionType.Sale;
					payConnectResponse.CardHolder=GetCard(transactionSurchargeResponse.PaymentMethod).CardHolder;
					break;
				case ResponseType.Error:
					//Happens when user tries to process a void past the allowed time limit.
					if(response.ErrorResponse==null) {
						payConnectResponse.TransType=PayConnectResponse.TransactionType.Unknown;
						payConnectResponse.Description=response.httpStatusCode.ToString();
						//Just needs to be not 0 to represent an error occured.
						payConnectResponse.StatusCode="";
						break;
					}
					ErrorResponse errorResponse=response.ErrorResponse;
					payConnectResponse.TransType=PayConnectResponse.TransactionType.Unknown;
					payConnectResponse.Description=string.IsNullOrEmpty(errorResponse.Message) ? (errorResponse.Error?.ToString()??"") : errorResponse.Message;
					payConnectResponse.StatusCode="";
					break;
				case ResponseType.AddSignature:
					AddSignatureResponse addSignatureResponse=response.SignatureResponse;
					payConnectResponse.Description=addSignatureResponse.Status;
					payConnectResponse.StatusCode="0";//0 indicates success
					payConnectResponse.RefNumber=addSignatureResponse.ReferenceId;
					payConnectResponse.Amount=((decimal)addSignatureResponse.Amount)/100;//PayConnect sends amount as total cents, convert back to OD decimal amounts.
					payConnectResponse.AuthCode=addSignatureResponse.AuthCode;
					payConnectResponse.MerchantId=addSignatureResponse.MerchantId.ToString();
					payConnectResponse.PaymentToken=GetCard(addSignatureResponse.PaymentMethod).CardToken;
					payConnectResponse.CardType=GetCard(addSignatureResponse.PaymentMethod).Network.ToString();
					payConnectResponse.CardNumber=GetCard(addSignatureResponse.PaymentMethod).CardLast4Digits;
					payConnectResponse.TransType=PayConnectResponse.TransactionType.Unknown;
					payConnectResponse.CardHolder=GetCard(addSignatureResponse.PaymentMethod).CardHolder;
					break;
				case ResponseType.Void:
					VoidResponse voidResponse=response.VoidResponse;
					payConnectResponse.Description=voidResponse.Status;
					payConnectResponse.StatusCode="0";//0 indicates success
					payConnectResponse.RefNumber=voidResponse.ReferenceId;
					payConnectResponse.Amount=((decimal)voidResponse.Amount)/100;//PayConnect sends amount as total cents, convert back to OD decimal amounts.
					payConnectResponse.AuthCode=voidResponse.AuthCode;
					payConnectResponse.MerchantId=voidResponse.MerchantId.ToString();
					payConnectResponse.PaymentToken=GetCard(voidResponse.PaymentMethod).CardToken;
					payConnectResponse.CardType=GetCard(voidResponse.PaymentMethod).Network.ToString();
					payConnectResponse.CardNumber=GetCard(voidResponse.PaymentMethod).CardLast4Digits;
					payConnectResponse.TransType=PayConnectResponse.TransactionType.Void;
					payConnectResponse.AmountSurcharged=((decimal)voidResponse.AmountSurcharged)/100;//PayConnect sends amount as total cents, convert back to OD decimal amounts.
					payConnectResponse.CardHolder=GetCard(voidResponse.PaymentMethod).CardHolder;
					break;
				case ResponseType.Refund:
					RefundResponse refundResponse=response.RefundResponse;
					payConnectResponse.Description=refundResponse.Status;
					payConnectResponse.StatusCode="0";//0 indicates success
					payConnectResponse.RefNumber=refundResponse.ReferenceId;
					payConnectResponse.Amount=((decimal)refundResponse.AmountRefunded)/100;//PayConnect sends amount as total cents, convert back to OD decimal amounts.
					payConnectResponse.AmountSurcharged=((decimal)refundResponse.AmountAuthorized-refundResponse.Amount)/100;//This response type doesn't include a line item for surcharge, so we have to subtract the difference to get it.
					payConnectResponse.AuthCode=refundResponse.AuthCode;
					payConnectResponse.MerchantId=refundResponse.MerchantId.ToString();
					if(refundResponse.PaymentMethod!=null) {
						payConnectResponse.PaymentToken=GetCard(refundResponse.PaymentMethod).CardToken;
						payConnectResponse.CardType=GetCard(refundResponse.PaymentMethod).Network.ToString();
						payConnectResponse.CardNumber=GetCard(refundResponse.PaymentMethod).CardLast4Digits;
					}
					payConnectResponse.TransType=PayConnectResponse.TransactionType.Refund;
					payConnectResponse.CardHolder=GetCard(refundResponse.PaymentMethod).CardHolder;
					break;
				case ResponseType.IFrame:
					iFrameResponse iFrameResponse=response.iFrameResponse;
					//Use IFrameStatus if we did not get a 'success' back. This would mean that they did not complete the transation in the iFrame.
					payConnectResponse.Description=iFrameResponse.IFrameStatus;
					if(iFrameResponse.IFrameStatus.ToLower()!="success") {
						payConnectResponse.StatusCode="";
						break;
					}
					payConnectResponse.Description=iFrameResponse.Response.TransactionStatus;
					payConnectResponse.PaymentToken=iFrameResponse.Response.CardToken;
					payConnectResponse.RefNumber=iFrameResponse.Response.ReferenceId;
					int year=int.Parse(iFrameResponse.Response.ExpiryYear);
					int month=int.Parse(iFrameResponse.Response.ExpiryMonth);
					payConnectResponse.TokenExpiration=new DateTime(year,month,1);
					payConnectResponse.Amount=((decimal)iFrameResponse.Response.Amount)/100;//PayConnect sends amount as total cents, convert back to OD decimal amounts.
					payConnectResponse.StatusCode="0";//0 indicates success
					payConnectResponse.SurchargePercent=iFrameResponse.Response.SurchargePercent;
					payConnectResponse.AmountSurcharged=((decimal)iFrameResponse.Response.AmountSurcharged)/100;//PayConnect sends amount as total cents, convert back to OD decimal amounts.
					payConnectResponse.CardHolder=iFrameResponse.Response.CardHolder;
					break;
				case ResponseType.TerminalTransaction:
					TerminalTransactionResponse terminalTransactionResponse=response.TerminalTransactionResponse;
					payConnectResponse.Description=terminalTransactionResponse.Status;
					payConnectResponse.StatusCode="0";//0 indicates success
					payConnectResponse.RefNumber=terminalTransactionResponse.ReferenceId;
					payConnectResponse.Amount=((decimal)terminalTransactionResponse.Amount)/100;//PayConnect sends amount as total cents, convert back to OD decimal amounts.
					payConnectResponse.AuthCode=terminalTransactionResponse.AuthCode;
					payConnectResponse.MerchantId=terminalTransactionResponse.MerchantId.ToString();
					payConnectResponse.PaymentToken=GetCard(terminalTransactionResponse.PaymentMethod).CardToken;
					payConnectResponse.CardType=GetCard(terminalTransactionResponse.PaymentMethod).Network.ToString();
					payConnectResponse.CardNumber=GetCard(terminalTransactionResponse.PaymentMethod).CardLast4Digits;
					payConnectResponse.TransType=PayConnectResponse.TransactionType.Authorize;
					if(terminalTransactionResponse.TransType==TransactionType.Sale) {
						payConnectResponse.TransType=PayConnectResponse.TransactionType.Sale;
					}
					payConnectResponse.CardHolder=GetCard(terminalTransactionResponse.PaymentMethod).CardHolder;
					break;
				case ResponseType.GetStatus:
					GetStatusResponse statusResponse=response.GetStatusResponse;
					payConnectResponse.Description=statusResponse.Status;
					payConnectResponse.StatusCode="0";//0 indicates success
					payConnectResponse.RefNumber=statusResponse.ReferenceId;
					payConnectResponse.Amount=((decimal)statusResponse.Amount)/100;//PayConnect sends amount as total cents, convert back to OD decimal amounts.
					payConnectResponse.SurchargePercent=statusResponse.SurchargePercent;
					payConnectResponse.AmountSurcharged=((decimal)statusResponse.AmountSurcharged)/100;//PayConnect sends amount as total cents, convert back to OD decimal amounts.
					payConnectResponse.AuthCode=statusResponse.AuthCode;
					payConnectResponse.MerchantId=statusResponse.MerchantId.ToString();
					payConnectResponse.PaymentToken=GetCard(statusResponse.PaymentMethod).CardToken;
					payConnectResponse.CardType=GetCard(statusResponse.PaymentMethod).Network.ToString();
					payConnectResponse.CardNumber=GetCard(statusResponse.PaymentMethod).CardLast4Digits;
					if(statusResponse.TransType==TransactionType.Sale) {
						payConnectResponse.TransType=PayConnectResponse.TransactionType.Sale;
					}
					else if(statusResponse.TransType==TransactionType.AuthorizeOnly) {
						payConnectResponse.TransType=PayConnectResponse.TransactionType.Authorize;
					}
					else if(statusResponse.TransType==TransactionType.Refund) {
						payConnectResponse.TransType=PayConnectResponse.TransactionType.Refund;
					}
					payConnectResponse.CardHolder=GetCard(statusResponse.PaymentMethod).CardHolder;
					break;
				default:
					break;
			}
			return payConnectResponse;
		}

		///<summary>The card details of a payment method, or an empty one when the vendor left them out (a list entry, an ACH method).</summary>
		private static CardPaymentMethod GetCard(PaymentMethod paymentMethod) {
			return paymentMethod?.CardPaymentMethod??new CardPaymentMethod();
		}
		#endregion Converters


		#region Request/Response Objects
		#region Request Objects

		//No request below has a patient object: no patient details are sent beyond the cardHolder name (spec 2.6). Several 2.12 schemas set
		//additionalProperties false, so only fields the schema lists are sent.

		[DataContract]
		public class CreateTransactionRequest {
			///<summary>Identify if transaction is Recurring or OneTime</summary>
			[DataMember(Name = "frequency"),JsonConverter(typeof(StringEnumConverter))]
			public TransactionFrequency Frequency;
			///<summary>Type of transaction</summary>
			[DataMember(Name = "type"),JsonConverter(typeof(StringEnumConverter))]
			public TransactionType TransType;
			///<summary>Amount in cents</summary>
			[DataMember(Name = "amount")]
			public int Amount;
			///<summary>Tokenized card number</summary>
			[DataMember(Name = "cardToken")]
			public string CardToken;
			///<summary>Expiry in MMYY or YYYYMM format</summary>
			[DataMember(Name = "expiry")]
			public string Expiry;
			///<summary>Security code for the card</summary>
			[DataMember(Name = "cvv",IsRequired=false)]
			public string Cvv;
			///<summary>Full name of the card holder</summary>
			[DataMember(Name = "cardHolder")]
			public string CardHolder;
			///<summary>The correlation ID (NewCorrelationId), set by PostCreateTransaction if blank. Opaque: never a PatNum or a name.</summary>
			[DataMember(Name = "invoiceNumber",IsRequired=false)]
			public string InvoiceNumber;
			///<summary>Optional document number. Opaque, like InvoiceNumber.</summary>
			[DataMember(Name = "documentNumber",IsRequired=false)]
			public string DocumentNumber;
			///<summary>ZipCode of the card holder</summary>
			[DataMember(Name = "zipCode",IsRequired=false)]
			public string ZipCode;
		}

		[DataContract]
		public class CreateSurchargeTransactionRequest {
			///<summary>Identify if transaction is Recurring or OneTime</summary>
			[DataMember(Name = "frequency")]
			public TransactionFrequency Frequency;
			///<summary>Type of transaction</summary>
			[DataMember(Name = "type")]
			public TransactionType TransType;
			///<summary>Amount in cents. NOTE: the PayConnect API accepts decimals but it behaves in a very strange manner when a decimal is sent.</summary>
			[DataMember(Name = "amount")]
			public int Amount;
			///<summary>Tokenized card number</summary>
			[DataMember(Name = "cardToken")]
			public string CardToken;
			///<summary>Expiry in MMYY or YYYYMM format</summary>
			[DataMember(Name = "expiry")]
			public string Expiry;
			///<summary>Security code for the card</summary>
			[DataMember(Name = "cvv",IsRequired=false)]
			public string Cvv;
			///<summary>Full name of the card holder</summary>
			[DataMember(Name = "cardHolder")]
			public string CardHolder;
			///<summary>The correlation ID (NewCorrelationId), set by PostCreateSurchargeTransaction if blank.</summary>
			[DataMember(Name = "invoiceNumber",IsRequired=false)]
			public string InvoiceNumber;
			///<summary>Optional document number. Opaque, like InvoiceNumber.</summary>
			[DataMember(Name = "documentNumber",IsRequired=false)]
			public string DocumentNumber;
			///<summary>ZipCode of the card holder. Required for surcharge accounts.</summary>
			[DataMember(Name = "zipCode")]
			public string ZipCode;
		}

		[DataContract]
		public class VoidReferenceIDRequest {
			///<summary>ReferenceID</summary>
			[DataMember(Name = "referenceId")]
			public string ReferenceId;
		}

		[DataContract]
		public class RefundReferenceIDRequest {
			///<summary>ReferenceID</summary>
			[DataMember(Name = "referenceId")]
			public string ReferenceId;
			///<summary>Amount in cents</summary>
			[DataMember(Name = "amount",IsRequired=false)]
			public int? Amount;
		}

		[DataContract]
		public class AddSignatureReferenceIDRequest {
			///<summary>Reference ID of the transaction to assign the signature</summary>
			[DataMember(Name = "referenceId")]
			public string ReferenceId;
			///<summary>Base64 string representation of the signature image</summary>
			[DataMember(Name = "signature")]
			public string Signature;
		}

		[DataContract]
		public class EmbedSessionRequest {
			///<summary>Tokenizer (save a card, no charge) or Payment. Required.</summary>
			[DataMember(Name = "type")]
			public IframeType Type;
			///<summary>Show swiper button. False by default.</summary>
			[DataMember(Name = "swiper")]
			public bool Swiper;
			///<summary>Amount in cents. If the amount is provided, Payment iFrame will disable the amount field. Null (left out) for a Tokenizer
			///session; a non-nullable 0 would still be sent.</summary>
			[DataMember(Name = "amount",IsRequired=false)]
			public int? Amount;
			///<summary>Provide fields for patient information. Defaults to false; kept false so no patient details go to the vendor.</summary>
			[DataMember(Name = "usePatient",IsRequired=false)]
			public bool UsePatient;
			///<summary>Provide a field for invoice number. Defaults to false.</summary>
			[DataMember(Name = "useInvoiceNumber",IsRequired=false)]
			public bool UseInvoiceNumber;
			///<summary>Custom submit button label for the iFrame. Default: "Process"</summary>
			[DataMember(Name = "buttonLabel",IsRequired=false)]
			public string ButtonLabel;
		}

		[DataContract]
		public class TransactionStatusRequest {
			///<summary>Reference ID of the transaction</summary>
			[DataMember(Name = "referenceId",IsRequired=false)]
			public string ReferenceId;
			///<summary>ID of the transaction</summary>
			[DataMember(Name = "transactionId",IsRequired=false)]
			public string TransactionId;
		}

		[DataContract]
		public class CreateTerminalTransactionRequest {
			///<summary>Identifies if transaction is Recurring or OneTime</summary>
			[DataMember(Name = "frequency"),JsonConverter(typeof(StringEnumConverter))]
			public TransactionFrequency Frequency;
			///<summary>Type of transaction</summary>
			[DataMember(Name = "type"),JsonConverter(typeof(StringEnumConverter))]
			public TransactionType TransType;
			///<summary>Amount in cents</summary>
			[DataMember(Name = "amount")]
			public int Amount;
			///<summary>Terminal ID or serial for processing the transaction.</summary>
			[DataMember(Name = "terminal")]
			public string Terminal;
			///<summary>Ask for a signature on the terminal.</summary>
			[DataMember(Name = "signature")]
			public bool Signature;
			///<summary>The correlation ID (NewCorrelationId), set by PostCreateTerminalTransaction if blank.</summary>
			[DataMember(Name = "invoiceNumber",IsRequired=false)]
			public string InvoiceNumber;
			///<summary>Optional document number. Opaque, like InvoiceNumber.</summary>
			[DataMember(Name = "documentNumber",IsRequired=false)]
			public string DocumentNumber;
		}

		#endregion Request Objects
		#region Response Objects

		///<summary>Wrapper response class for passing API response on the Open Dental side. Check ResponseType to see if it contains your expected type or an error.</summary>
		public class PayConnect2Response {
			public ResponseType ResponseType;
			public AddSignatureResponse SignatureResponse;
			public CreateTransactionResponse TransactionResponse;
			public CreateSurchargeTransactionResponse TransactionSurchargeResponse;
			public ErrorResponse ErrorResponse;
			public RefundResponse RefundResponse;
			public VoidResponse VoidResponse;
			public EmbedSessionResponse EmbedSessionResponse;
			public GetStatusResponse GetStatusResponse;
			public iFrameResponse iFrameResponse;
			public TerminalTransactionResponse TerminalTransactionResponse;
			public HttpStatusCode httpStatusCode;
			public GetMerchantInfoResponse GetMerchantInfoResponse;
			public TransactionListResponse TransactionListResponse;
			public List<PayConnectTerminalInfo> ListTerminals;
			///<summary>Whether the request reached PayConnect. NotSent means nothing was charged; SentNoResponse means it may have been.</summary>
			public PayConnectRequestOutcome Outcome;
			///<summary>The invoiceNumber sent with a create, for the security log and for finding it in the PayConnect portal. "" otherwise.</summary>
			public string CorrelationId="";
		}

		///<summary>One page of /transactions/list.</summary>
		public class TransactionListResponse {
			public List<GetStatusResponse> ListTransactions=new List<GetStatusResponse>();
			///<summary>0 when the body doesn't say.</summary>
			public int TotalPages;
		}

		///<summary>One terminal from /terminals.</summary>
		public class PayConnectTerminalInfo {
			///<summary>The vendor's terminal ID.</summary>
			public string TerminalId="";
			///<summary>The terminal's serial number, sent as "terminal" in a terminal transaction.</summary>
			public string Serial="";
			public string Name="";
			///<summary>Clover, Ingenico or blank.</summary>
			public string TerminalType="";
		}

		///<summary>A message relayed from the hosted card-entry page.</summary>
		public class HostedFormMessage {
			///<summary>The event.origin the relay script saw.</summary>
			public string Origin="";
			///<summary>True if Origin is https on a DentalXChange host. Ignore the message otherwise.</summary>
			public bool IsFromAllowedOrigin;
			public iFrameResponse Response;

			///<summary>ONLOAD, SUCCESS, ERROR or SESSION_EXPIRED, upper case.</summary>
			public string Status {
				get {
					return (Response?.IFrameStatus??"").Trim().ToUpperInvariant();
				}
			}
		}

		///<summary>The values one request needs. ToString hides them; never log them.</summary>
		public sealed class ApiCredentials {
			public string ApiKey="";
			public string Secret="";
			public string BaseUrl="";
			///<summary>True if the base address came from the developer override (Introspection), which lifts the DentalXChange host allow-list.</summary>
			public bool IsDeveloperOverride;
			public bool IsSandbox;

			public override string ToString() {
				return "PayConnect credentials";
			}
		}

		///<summary>Thrown when the API key or the clinic's Secret is missing.</summary>
		public class PayConnectNotConfiguredException:ODException {
			public PayConnectNotConfiguredException(string message):base(message) {
			}
		}

		[DataContract]
		public class CreateTransactionResponse {
			///<summary>Amount authorized in cents</summary>
			[DataMember(Name = "amountAuthorized")]
			public int AmountAuthorized;
			///<summary>Amount captured in cents</summary>
			[DataMember(Name = "amountCaptured")]
			public int AmountCaptured;
			///<summary>Amount in cents</summary>
			[DataMember(Name = "amount")]
			public int Amount;
			///<summary>Internal transaction id</summary>
			[DataMember(Name = "transactionId")]
			public int TransactionId;
			///<summary>The user who ran the transaction</summary>
			[DataMember(Name = "userId")]
			public int UserId;
			///<summary>Transaction reference number</summary>
			[DataMember(Name = "referenceId")]
			public string ReferenceId;
			///<summary>Type of transaction</summary>
			[DataMember(Name = "type")]
			public TransactionType TransType;
			///<summary>Authorization code send by the gateway</summary>
			[DataMember(Name = "authCode")]
			public string AuthCode;
			///<summary>Ternimal ID, if the transaction was run fron a terminal</summary>
			[DataMember(Name = "terminal")]
			public string Terminal;
			///<summary>Invoice number to identify the transaction</summary>
			[DataMember(Name = "invoiceNumber")]
			public string InvoiceNumber;
			///<summary>Identidy if transaction is Recurring or OneTime</summary>
			[DataMember(Name = "frequency")]
			public TransactionFrequency Frequency;
			///<summary>Status</summary>
			[DataMember(Name = "status")]
			public string Status;
			///<summary>Transaction origination point</summary>
			[DataMember(Name = "source")]
			public TransactionSource Source;
			///<summary>Raw response form the gateway</summary>
			[DataMember(Name = "gatewayResponse")]
			public object GatewayResponse;
			///<summary>Merchant identification number</summary>
			[DataMember(Name = "merchantId")]
			public int MerchantId;
			///<summary>Patient identification number. From testing can be null.</summary>
			[DataMember(Name = "patientId")]
			public int? PatientId;
			///<summary>Returned if the transaction is refund or void</summary>
			[DataMember(Name = "parentTransactionId")]
			public int? ParentTransactionId;
			///<summary>Batch identification number the transaction belongs to</summary>
			[DataMember(Name = "batchId")]
			public int BatchId;
			///<summary>Patient for the payment.</summary>
			[DataMember(Name = "patient")]
			PayConnectPatient Patient;
			///<summary></summary>
			[DataMember(Name = "paymentMethod")]
			public PaymentMethod PaymentMethod;
			///<summary>Date the transaction was created</summary>
			[DataMember(Name = "createdAt")]
			public string CreatedAt;
			///<summary>Date the transaction was updated</summary>
			[DataMember(Name = "updatedAt")]
			public string UpdatedAt;
		}

		[DataContract]
		public class CreateSurchargeTransactionResponse {
			///<summary>Amount authorized in cents</summary>
			[DataMember(Name = "amountAuthorized")]
			public int AmountAuthorized;
			///<summary>Amount captured in cents</summary>
			[DataMember(Name = "amountCaptured")]
			public int AmountCaptured;
			///<summary>Amount surcharged in cents</summary>
			[DataMember(Name = "amountSurcharged")]
			public int AmountSurcharged;
			///<summary>Percentage of the amount surcharged</summary>
			[DataMember(Name = "surchargePercent")]
			public int SurchargePercent;
			///<summary>Amount in cents</summary>
			[DataMember(Name = "amount")]
			public int Amount;
			///<summary>Internal transaction id</summary>
			[DataMember(Name = "transactionId")]
			public int TransactionId;
			///<summary>The user who ran the transaction</summary>
			[DataMember(Name = "userId")]
			public int UserId;
			///<summary>Transaction reference number</summary>
			[DataMember(Name = "referenceId")]
			public string ReferenceId;
			///<summary>Type of transaction</summary>
			[DataMember(Name = "type")]
			public TransactionType TransType;
			///<summary>Authorization code send by the gateway</summary>
			[DataMember(Name = "authCode")]
			public string AuthCode;
			///<summary>Ternimal ID, if the transaction was run fron a terminal</summary>
			[DataMember(Name = "terminal")]
			public string Terminal;
			///<summary>Invoice number to identify the transaction</summary>
			[DataMember(Name = "invoiceNumber")]
			public string InvoiceNumber;
			///<summary>Identidy if transaction is Recurring or OneTime</summary>
			[DataMember(Name = "frequency")]
			public TransactionFrequency Frequency;
			///<summary>Status</summary>
			[DataMember(Name = "status")]
			public string Status;
			///<summary>Transaction origination point</summary>
			[DataMember(Name = "source")]
			public TransactionSource Source;
			///<summary>Raw response form the gateway</summary>
			[DataMember(Name = "gatewayResponse")]
			public object GatewayResponse;
			///<summary>Merchant identification number</summary>
			[DataMember(Name = "merchantId")]
			public int MerchantId;
			///<summary>Patient identification number</summary>
			[DataMember(Name = "patientId")]
			public int PatientId;
			///<summary>Returned if the transaction is refund or void</summary>
			[DataMember(Name = "parentTransactionId")]
			public int ParentTransactionId;
			///<summary>Batch identification number the transaction belongs to</summary>
			[DataMember(Name = "batchId")]
			public int BatchId;
			///<summary>Patient for the payment.</summary>
			[DataMember(Name = "patient")]
			PayConnectPatient Patient;
			///<summary></summary>
			[DataMember(Name = "paymentMethod")]
			public PaymentMethod PaymentMethod;
			///<summary>Date the transaction was created</summary>
			[DataMember(Name = "createdAt")]
			public string CreatedAt;
			///<summary>Date the transaction was updated</summary>
			[DataMember(Name = "updatedAt")]
			public string UpdatedAt;
		}

		[DataContract]
		public class VoidResponse {
			///<summary>Amount authorized in cents</summary>
			[DataMember(Name = "amountAuthorized")]
			public int AmountAuthorized;
			///<summary>Amount captured in cents</summary>
			[DataMember(Name = "amountCaptured")]
			public int AmountCaptured;
			///<summary>Amount voided in cents</summary>
			[DataMember(Name = "amountVoided")]
			public int AmountVoided;
			///<summary>Amount in cents</summary>
			[DataMember(Name = "amount")]
			public int Amount;
			///<summary>Internal transaction id</summary>
			[DataMember(Name = "transactionId")]
			public int TransactionId;
			///<summary>The user who ran the transaction</summary>
			[DataMember(Name = "userId")]
			public int UserId;
			///<summary>Transaction reference number</summary>
			[DataMember(Name = "referenceId")]
			public string ReferenceId;
			///<summary>Type of transaction</summary>
			[DataMember(Name = "type")]
			public TransactionType TransType;
			///<summary>Authorization code send by the gateway</summary>
			[DataMember(Name = "authCode")]
			public string AuthCode;
			///<summary>Ternimal ID, if the transaction was run fron a terminal</summary>
			[DataMember(Name = "terminal")]
			public string Terminal;
			///<summary>Invoice number to identify the transaction</summary>
			[DataMember(Name = "invoiceNumber")]
			public string InvoiceNumber;
			///<summary>Identidy if transaction is Recurring or OneTime</summary>
			[DataMember(Name = "frequency")]
			public TransactionFrequency Frequency;
			///<summary>Status</summary>
			[DataMember(Name = "status")]
			public string Status;
			///<summary>Transaction origination point</summary>
			[DataMember(Name = "source")]
			public TransactionSource Source;
			///<summary>Raw response form the gateway</summary>
			[DataMember(Name = "gatewayResponse")]
			public object GatewayResponse;
			///<summary>Merchant identification number</summary>
			[DataMember(Name = "merchantId")]
			public int MerchantId;
			///<summary>Patient identification number</summary>
			[DataMember(Name = "patientId")]
			public int PatientId;
			///<summary>Returned if the transaction is refund or void</summary>
			[DataMember(Name = "parentTransactionId")]
			public int ParentTransactionId;
			///<summary>Batch identification number the transaction belongs to</summary>
			[DataMember(Name = "batchId")]
			public int BatchId;
			///<summary>Patient for the payment.</summary>
			[DataMember(Name = "patient")]
			PayConnectPatient Patient;
			///<summary></summary>
			[DataMember(Name = "paymentMethod")]
			public PaymentMethod PaymentMethod;
			///<summary>Date the transaction was created</summary>
			[DataMember(Name = "createdAt")]
			public string CreatedAt;
			///<summary>Date the transaction was updated</summary>
			[DataMember(Name = "updatedAt")]
			public string UpdatedAt;
			///<summary>Amount surcharged in cents</summary>
			[DataMember(Name = "amountSurcharged")]
			public int AmountSurcharged;
		}

		[DataContract]
		public class RefundResponse {
			///<summary>Amount authorized in cents</summary>
			[DataMember(Name = "amountAuthorized")]
			public int AmountAuthorized;
			///<summary>Amount captured in cents</summary>
			[DataMember(Name = "amountCaptured")]
			public int AmountCaptured;
			///<summary>Amount refunded in cents</summary>
			[DataMember(Name = "amountRefunded")]
			public int AmountRefunded;
			///<summary>Amount in cents</summary>
			[DataMember(Name = "amount")]
			public int Amount;
			///<summary>Internal transaction id</summary>
			[DataMember(Name = "transactionId")]
			public int TransactionId;
			///<summary>The user who ran the transaction</summary>
			[DataMember(Name = "userId")]
			public int UserId;
			///<summary>Transaction reference number</summary>
			[DataMember(Name = "referenceId")]
			public string ReferenceId;
			///<summary>Type of transaction</summary>
			[DataMember(Name = "type")]
			public TransactionType TransType;
			///<summary>Authorization code send by the gateway</summary>
			[DataMember(Name = "authCode")]
			public string AuthCode;
			///<summary>Ternimal ID, if the transaction was run fron a terminal</summary>
			[DataMember(Name = "terminal")]
			public string Terminal;
			///<summary>Invoice number to identify the transaction</summary>
			[DataMember(Name = "invoiceNumber")]
			public string InvoiceNumber;
			///<summary>Identidy if transaction is Recurring or OneTime</summary>
			[DataMember(Name = "frequency")]
			public TransactionFrequency Frequency;
			///<summary>Status</summary>
			[DataMember(Name = "status")]
			public string Status;
			///<summary>Transaction origination point</summary>
			[DataMember(Name = "source")]
			public TransactionSource Source;
			///<summary>Raw response form the gateway</summary>
			[DataMember(Name = "gatewayResponse")]
			public object GatewayResponse;
			///<summary>Merchant identification number</summary>
			[DataMember(Name = "merchantId")]
			public int MerchantId;
			///<summary>Patient identification number</summary>
			[DataMember(Name = "patientId")]
			public int PatientId;
			///<summary>Returned if the transaction is refund or void</summary>
			[DataMember(Name = "parentTransactionId")]
			public int ParentTransactionId;
			///<summary>Batch identification number the transaction belongs to</summary>
			[DataMember(Name = "batchId")]
			public int BatchId;
			///<summary>Patient for the payment.</summary>
			[DataMember(Name = "patient")]
			PayConnectPatient Patient;
			///<summary></summary>
			[DataMember(Name = "paymentMethod")]
			public PaymentMethod PaymentMethod;
			///<summary>Date the transaction was created</summary>
			[DataMember(Name = "createdAt")]
			public string CreatedAt;
			///<summary>Date the transaction was updated</summary>
			[DataMember(Name = "updatedAt")]
			public string UpdatedAt;
		}

		public class AddSignatureResponse {
			///<summary>Amount in cents</summary>
			[DataMember(Name = "amount")]
			public int Amount;
			///<summary>Internal transaction id</summary>
			[DataMember(Name = "transactionId")]
			public int TransactionId;
			///<summary>The user who ran the transaction</summary>
			[DataMember(Name = "userId")]
			public int UserId;
			///<summary>Transaction reference number</summary>
			[DataMember(Name = "referenceId")]
			public string ReferenceId;
			///<summary>Type of transaction</summary>
			[DataMember(Name = "type")]
			public TransactionType TransType;
			///<summary>Authorization code send by the gateway</summary>
			[DataMember(Name = "authCode")]
			public string AuthCode;
			///<summary>Ternimal ID, if the transaction was run fron a terminal</summary>
			[DataMember(Name = "terminal")]
			public string Terminal;
			///<summary>Invoice number to identify the transaction</summary>
			[DataMember(Name = "invoiceNumber")]
			public string InvoiceNumber;
			///<summary>Identidy if transaction is Recurring or OneTime</summary>
			[DataMember(Name = "frequency")]
			public TransactionFrequency Frequency;
			///<summary>Status</summary>
			[DataMember(Name = "status")]
			public string Status;
			///<summary>Transaction origination point</summary>
			[DataMember(Name = "source")]
			public TransactionSource Source;
			///<summary>Raw response form the gateway</summary>
			[DataMember(Name = "gatewayResponse")]
			public object GatewayResponse;
			///<summary>Merchant identification number</summary>
			[DataMember(Name = "merchantId")]
			public int MerchantId;
			///<summary>Patient identification number</summary>
			[DataMember(Name = "patientId")]
			public int PatientId;
			///<summary>Returned if the transaction is refund or void</summary>
			[DataMember(Name = "parentTransactionId")]
			public int ParentTransactionId;
			///<summary>Batch identification number the transaction belongs to</summary>
			[DataMember(Name = "batchId")]
			public int BatchId;
			///<summary>Patient for the payment.</summary>
			[DataMember(Name = "patient")]
			PayConnectPatient Patient;
			///<summary></summary>
			[DataMember(Name = "paymentMethod")]
			public PaymentMethod PaymentMethod;
			///<summary>Date the transaction was created</summary>
			[DataMember(Name = "createdAt")]
			public string CreatedAt;
			///<summary>Date the transaction was updated</summary>
			[DataMember(Name = "updatedAt")]
			public string UpdatedAt;
		}

		[DataContract]
		public class GetStatusResponse {
			///<summary>Amount in cents</summary>
			[DataMember(Name = "amountAuthorized")]
			public int AmountAuthorized;
			///<summary>Amount in cents</summary>
			[DataMember(Name = "amount")]
			public int Amount;
			///<summary>Amount captured in cents</summary>
			[DataMember(Name = "amountCaptured")]
			public int AmountCaptured;
			///<summary>Amount surcharged in cents</summary>
			[DataMember(Name = "amountSurcharged")]
			public int AmountSurcharged;
			///<summary>Percentage of the amount surcharged</summary>
			[DataMember(Name = "surchargePercent")]
			public int SurchargePercent;
			///<summary>Internal transaction id</summary>
			[DataMember(Name = "transactionId")]
			public int TransactionId;
			///<summary>The user who ran the transaction</summary>
			[DataMember(Name = "userId")]
			public int UserId;
			///<summary>Transaction reference number</summary>
			[DataMember(Name = "referenceId")]
			public string ReferenceId;
			///<summary>Type of transaction</summary>
			[DataMember(Name = "type")]
			public TransactionType TransType;
			///<summary>Authorization code send by the gateway</summary>
			[DataMember(Name = "authCode")]
			public string AuthCode;
			///<summary>Ternimal ID, if the transaction was run fron a terminal</summary>
			[DataMember(Name = "terminal")]
			public string Terminal;
			///<summary>Invoice number to identify the transaction</summary>
			[DataMember(Name = "invoiceNumber")]
			public string InvoiceNumber;
			///<summary>Document number, if one was sent</summary>
			[DataMember(Name = "documentNumber")]
			public string DocumentNumber;
			///<summary>The embed session's order ID, for a hosted-page transaction</summary>
			[DataMember(Name = "orderId")]
			public string OrderId;
			///<summary>Identidy if transaction is Recurring or OneTime</summary>
			[DataMember(Name = "frequency")]
			public TransactionFrequency Frequency;
			///<summary>Status</summary>
			[DataMember(Name = "status")]
			public string Status;
			///<summary>Transaction origination point</summary>
			[DataMember(Name = "source")]
			public TransactionSource Source;
			///<summary>Raw response from the gateway</summary>
			[DataMember(Name = "gatewayResponse")]
			public object GatewayResponse;
			///<summary>Merchant identification number</summary>
			[DataMember(Name = "merchantId")]
			public int MerchantId;
			///<summary>Patient identification number</summary>
			[DataMember(Name = "patientId")]
			public int PatientId;
			///<summary>Returned if the transaction is refund or void</summary>
			[DataMember(Name = "parentTransactionId")]
			public int ParentTransactionId;
			///<summary>Batch identification number the transaction belongs to</summary>
			[DataMember(Name = "batchId")]
			public int BatchId;
			///<summary>Patient for the payment.</summary>
			[DataMember(Name = "patient")]
			PayConnectPatient Patient;
			///<summary></summary>
			[DataMember(Name = "paymentMethod")]
			public PaymentMethod PaymentMethod;
			///<summary>Date the transaction was created</summary>
			[DataMember(Name = "createdAt")]
			public string CreatedAt;
			///<summary>Date the transaction was updated</summary>
			[DataMember(Name = "updatedAt")]
			public string UpdatedAt;
		}

		[DataContract]
		public class EmbedSessionResponse {
			///<summary>Secure session URL to be added in the iFrame</summary>
			[DataMember(Name = "url")]
			public string Url;
			///<summary>The session's order ID. If the window closes after SUCCESS but before the status call returns, GetTransactionStatus with this
			///orderId recovers the result.</summary>
			[DataMember(Name = "orderId")]
			public string OrderId;
		}

		[DataContract]
		public class TerminalTransactionResponse {
			///<summary>Amount authorized in cents</summary>
			[DataMember(Name = "amountAuthorized")]
			public int AmountAuthorized;
			///<summary>Amount captured in cents</summary>
			[DataMember(Name = "amountCaptured")]
			public int AmountCaptured;
			///<summary>Amount in cents</summary>
			[DataMember(Name = "amount")]
			public int Amount;
			///<summary>Internal transaction id</summary>
			[DataMember(Name = "transactionId")]
			public int TransactionId;
			///<summary>The user who ran the transaction</summary>
			[DataMember(Name = "userId")]
			public int UserId;
			///<summary>Transaction reference number</summary>
			[DataMember(Name = "referenceId")]
			public string ReferenceId;
			///<summary>Type of transaction</summary>
			[DataMember(Name = "type")]
			public TransactionType TransType;
			///<summary>Authorization code send by the gateway</summary>
			[DataMember(Name = "authCode")]
			public string AuthCode;
			///<summary>Ternimal ID, if the transaction was run fron a terminal</summary>
			[DataMember(Name = "terminal")]
			public string Terminal;
			///<summary>Invoice number to identify the transaction</summary>
			[DataMember(Name = "invoiceNumber")]
			public string InvoiceNumber;
			///<summary>Identidy if transaction is Recurring or OneTime</summary>
			[DataMember(Name = "frequency")]
			public TransactionFrequency Frequency;
			///<summary>Status</summary>
			[DataMember(Name = "status")]
			public string Status;
			///<summary>Base64 encoded signature image. Can be null.</summary>
			[DataMember(Name = "signature")]
			public string Signature;
			///<summary>Transaction origination point</summary>
			[DataMember(Name = "source")]
			public TransactionSource Source;
			///<summary>Raw response form the gateway</summary>
			[DataMember(Name = "gatewayResponse")]
			public object GatewayResponse;
			///<summary>Merchant identification number</summary>
			[DataMember(Name = "merchantId")]
			public int MerchantId;
			///<summary>Patient identification number. From testing can be null.</summary>
			[DataMember(Name = "patientId")]
			public int? PatientId;
			///<summary>Returned if the transaction is refund or void</summary>
			[DataMember(Name = "parentTransactionId")]
			public int? ParentTransactionId;
			///<summary>Batch identification number the transaction belongs to</summary>
			[DataMember(Name = "batchId")]
			public int BatchId;
			///<summary>Patient for the payment.</summary>
			[DataMember(Name = "patient")]
			PayConnectPatient Patient;
			///<summary></summary>
			[DataMember(Name = "paymentMethod")]
			public PaymentMethod PaymentMethod;
			///<summary>Date the transaction was created</summary>
			[DataMember(Name = "createdAt")]
			public string CreatedAt;
			///<summary>Date the transaction was updated</summary>
			[DataMember(Name = "updatedAt")]
			public string UpdatedAt;
		}

		[DataContract]
		public class PaymentMethod {
			public int paymentMethodId;
			[DataMember(Name = "type")]
			public PaymentMethodType MethodType;
			[DataMember(Name = "patientId")]
			public int? PatientId;
			[DataMember(Name = "createdAt")]
			public string CreatedAt;
			[DataMember(Name = "updatedAt")]
			public string UpdatedAt;
			[DataMember(Name = "cardPaymentMethod")]
			public CardPaymentMethod CardPaymentMethod;

			public enum PaymentMethodType {
				Card,
				ACH,
			}
		}

		[DataContract]
		public class CardPaymentMethod {
			[DataMember(Name = "cardPaymentMethodId")]
			public int CardPaymentMethodId;
			[DataMember(Name = "paymentMethodId")]
			public int PaymentMethodId;
			[DataMember(Name = "cardHolder")]
			public string CardHolder;
			[DataMember(Name = "cardLast4Digits")]
			public string CardLast4Digits;
			[DataMember(Name = "expiry")]
			public string Expiry;
			[DataMember(Name = "cardToken")]
			public string CardToken;
			[DataMember(Name = "network")]
			public CardNetwork Network;
			[DataMember(Name = "zipCode")]
			public string ZipCode;
			[DataMember(Name = "createdAt")]
			public string CreatedAt;
			[DataMember(Name = "updatedAt")]
			public string UpdatedAt;
		}

		public class PayConnectPatient {
			///<summary>Firstname of patient. Required.</summary>
			[DataMember(Name = "firstName")]
			public string FirstName;
			///<summary>LastName of patient. Required.</summary>
			[DataMember(Name = "lastName")]
			public string LastName;
			///<summary>Street line 1 in address</summary>
			[DataMember(Name = "address1")]
			public string Address1;
			///<summary>Street line 2 in address</summary>
			[DataMember(Name = "address2")]
			public string Address2;
			///<summary>City in address</summary>
			[DataMember(Name = "city")]
			public string City;
			///<summary>State in address</summary>
			[DataMember(Name = "state")]
			public string State;
			///<summary>ZipCode in address</summary>
			[DataMember(Name = "zipCode")]
			public string ZipCode;
			///<summary>Phone number of patient</summary>
			[DataMember(Name = "phoneNumber")]
			public string PhoneNumber;
			///<summary>Email of patient</summary>
			[DataMember(Name = "email")]
			public string Email;
		}

		[DataContract]
		public class ErrorResponse {
			[DataMember(Name = "errorType")]
			public ErrorType ErrorType;
			///<summary>The message for the user (PayConnect2.ParseErrorResponse sets it to the same text as Message). From older code paths it can be
			///the vendor's raw ValidationError or ResponseError.</summary>
			[DataMember(Name = "error")]
			public object Error;
			///<summary>A sentence for the user. Never contains the request, the keys or card data.</summary>
			public string Message="";
			///<summary>What went wrong, for callers that react differently (a rejected key, a validation error, no answer).</summary>
			public PayConnectErrorKind Kind;
			///<summary>Each rejected field of a 400 VALIDATION error, without the rejected value.</summary>
			public List<ValidationErrorBody> ListValidationErrors=new List<ValidationErrorBody>();
			///<summary>The HTTP status, or 0 when no response arrived.</summary>
			public int HttpStatus;
		}

		[DataContract]
		public class ResponseError {
			[DataMember(Name = "message")]
			public string Message;
			[DataMember(Name = "code")]
			public int Code;
		}
		/// <summary>any type of request body error will return a VALIDATION error - PayConnect Dev Team.</summary>
		[DataContract]
		public class ValidationError {
			[DataMember(Name = "properties")]
			public string ErrorProperties;
		}

		public class ValidationErrorBody {
			[DataMember(Name = "message")]
			public string Message;
			[DataMember(Name = "param")]
			public string Param;
			[DataMember(Name = "location")]
			public string Location;
			[DataMember(Name = "value")]
			public string Value;
		}

		[DataContract]
		public class iFrameResponse {
			///<summary>This status field contains the last known 'state' of the iFrame window. Known statuses: 
			///ONLOAD - the iFrame has loaded. 
			///SUCCESS - a request has been submitted successfully via the iFrame window. This does NOT correspond to the result of said request.</summary>
			[DataMember(Name="status")]
			public string IFrameStatus;
			[DataMember(Name="response")]
			public iFrameListenerResponse Response;
		}

		[DataContract]
		public class iFrameListenerResponse {
			[DataMember(Name="cardToken")]
			public string CardToken;
			[DataMember(Name="cardHolder")]
			public string CardHolder;
			[DataMember(Name="expiryMonth")]
			public string ExpiryMonth;
			[DataMember(Name="expiryYear")]
			public string ExpiryYear;
			[DataMember(Name="zipCode")]
			public string ZipCode;
			[DataMember(Name="invoiceNumber")]
			public string InvoiceNumber;
			[DataMember(Name="patientFirstName")]
			public string PatientFirstName;
			[DataMember(Name="patientLastName")]
			public string PatientLastName;
			[DataMember(Name="amount")]
			public int Amount;
			[DataMember(Name="referenceId")]
			public string ReferenceId;
			///<summary>Amount surcharged in cents</summary>
			[DataMember(Name="amountSurcharged")]
			public int AmountSurcharged;
			///<summary>Percentage of the amount surcharged</summary>
			[DataMember(Name="surchargePercent")]
			public int SurchargePercent;
			///<summary>Status - The actual status of the transaction being ran by PayConnect.</summary>
			[DataMember(Name="status")]
			public string TransactionStatus;
			///<summary>ONLOAD and ERROR will provide a message field value.</summary>
			[DataMember(Name="message")]
			public string Message;
		}

		///<summary>https://developer.dentalxchange.com/payment-api (Merchant, merchant-info)</summary>
		[DataContract]
		public class GetMerchantInfoResponse {
			///<summary>Merchant identification number</summary>
			[DataMember(Name="merchantId")]
			public int MerchantId;
			///<summary>Name of the merchant</summary>
			[DataMember(Name="name")]
			public string Name;
			///<summary>Street line 1 in merchant address</summary>
			[DataMember(Name="address1")]
			public string Address1;
			///<summary>Street line 2 in merchant address</summary>
			[DataMember(Name="address2")]
			public string Address2;
			///<summary>City in merchant address</summary>
			[DataMember(Name="city")]
			public string City;
			///<summary>State in merchant address</summary>
			[DataMember(Name="state")]
			public string State;
			///<summary>ZipCode in merchant address</summary>
			[DataMember(Name="zipCode")]
			public string ZipCode;
			///<summary>Phone number of merchant</summary>
			[DataMember(Name="phoneNumber")]
			public string PhoneNumber;
			///<summary>DentalXChange group identifier</summary>
			[DataMember(Name="dxcGroupId")]
			public int DxcGroupId;
			///<summary>Email address of merchant</summary>
			[DataMember(Name="email")]
			public string Email;
			///<summary>Reporting emails to send the reports</summary>
			[DataMember(Name="reportingEmails")]
			public string[] ReportingEmails;
			///<summary>Refund without reference is enabled if true</summary>
			[DataMember(Name="refundWithoutReferenceEnabled")]
			public bool RefundWithoutReferenceEnabled;
			///<summary>Text-to-Pay feature is enabled if true</summary>
			[DataMember(Name="text2PayEnabled")]
			public bool Text2PayEnabled;
			///<summary>Enum: Active,Inactive</summary>
			[DataMember(Name="status")]
			public string Status;
			///<summary>Date the merchant was created</summary>
			[DataMember(Name="createdAt")]
			public string CreatedAt;
			///<summary>Date the merchant was updated</summary>
			[DataMember(Name="updatedAt")]
			public string UpdatedAt;
		}
		#endregion Response Objects

		[JsonConverter(typeof(StringEnumConverter))]
		public enum TransactionType {
			Sale,
			AuthorizeOnly,
			Refund,
		}

		[JsonConverter(typeof(StringEnumConverter))]
		public enum TransactionFrequency {
			OneTime,
			Recurring,
		}

		[JsonConverter(typeof(StringEnumConverter))]
		public enum TransactionSource {
			ThirdParty,
			Portal,
			Terminal,
			Integration,
			Text2Pay,
		}

		[JsonConverter(typeof(StringEnumConverterWithDefault<CardNetwork>),CardNetwork.Unrecognized)]
		public enum CardNetwork {
			Unrecognized,//Used when the json string value doesn't match any value in this enum.
			Amex,
			Discover,
			Mastercard,
			[Description("Non-co-branded debit card")]
			NonCoBrandedDebitCard,
			Visa,
			Unknown
		}

		[JsonConverter(typeof(StringEnumConverter))]
		public enum IframeType {
			Tokenizer,
			Payment,
		}

		[JsonConverter(typeof(StringEnumConverter))]
		public enum ErrorType {
			[Description("RESPONSE")]
			Response,
			[Description("VALIDATION")]
			Validation,
		}

		[JsonConverter(typeof(StringEnumConverter))]
		public enum ResponseType {
			[Description("Add Signature")]
			AddSignature,
			[Description("Transaction")]
			CreateTransaction,
			[Description("Transaction")]
			CreateSurchargeTransaction,
			[Description("Error")]
			Error,
			[Description("Refund")]
			Refund,
			[Description("Void")]
			Void,
			[Description("Embed Session")]
			EmbedSession,
			[Description("iFrame")]
			IFrame,
			[Description("Get Status")]
			GetStatus,
			TerminalTransaction,
			[Description("Get Merchant Info")]
			MerchantInfo,
			[Description("Transaction List")]
			TransactionList,
			[Description("Terminals")]
			Terminals,
		}

		///<summary>Whether a request reached PayConnect.</summary>
		public enum PayConnectRequestOutcome {
			///<summary>A response arrived (success or an HTTP error).</summary>
			Response,
			///<summary>The request never left this computer (not set up, refused URL, DNS, connect or TLS failure). Nothing was charged.</summary>
			NotSent,
			///<summary>The request was sent but no answer came (timeout, dropped connection). It may have been processed. Creates are reconciled.</summary>
			SentNoResponse,
			///<summary>The answer was lost or unreadable, but the transaction was found in the transaction list by its correlation ID. A GetStatus
			///response if PayConnect approved it, an Error response (the vendor's status in its message) if not.</summary>
			Reconciled,
			///<summary>A 2xx answer arrived, but its body couldn't be read (malformed, or a value this model doesn't know). PayConnect accepted the
			///request, so it may have been processed. Creates are reconciled, as for SentNoResponse.</summary>
			ResponseUnreadable,
		}

		///<summary>The kind of error in an ErrorResponse.</summary>
		public enum PayConnectErrorKind {
			///<summary>The vendor's own error message (including declines).</summary>
			Vendor,
			///<summary>401 with a string error, or 403: the integrator API key is missing, unknown or not entitled to the Payment API.</summary>
			ApiKeyRejected,
			///<summary>401 with an error object: the merchant Secret was not accepted.</summary>
			SecretRejected,
			///<summary>400 VALIDATION.</summary>
			Validation,
			///<summary>The API key or Secret isn't set in this program.</summary>
			NotConfigured,
			///<summary>The request couldn't be sent.</summary>
			Network,
			///<summary>The request was sent but no answer came, or the answer couldn't be read.</summary>
			NoAnswer,
		}
		#endregion Request/Response Objects

		#region Mocks for UnitTesting
		public static MockPayConnect2 Mock;
		public class MockPayConnect2 {
			public bool IsPostCreateTransactionRequest;
			public PayConnect2Response Response;
		}

		private static PayConnect2Response MockRequestResponse() {
			return Mock.Response;
		}
		#endregion

		///<summary>Custom string to enum json converter that uses a default enum value when the string doesn't match any values in enum type T.
		///To use, apply a JsonConverter attribute to an enum type like this:
		///[JsonConverter(typeof(StringEnumConverterWithDefault<CardNetwork>),CardNetwork.Unknown)]
		///The type of this class with the enum type assigned to generic type T is the first argument, and the desired default value is the second argument. 
		///We require type T to be a struct and Enum because using just Enum references the abstract Enum class. Including struct requires it to also be a value
		///type which is what specific enum types are.</summary>
		private class StringEnumConverterWithDefault<T> : StringEnumConverter where T : struct,Enum {
			///<summary>The default enum value to use when the string value doesn't match any values in enum type T</summary>
			private T _enumValueDefault;

			///<summary>Parameterless constructor throws because failing to provide the T defaultValue argument
			///when applying the attribute will not cause syntax or build errors and deserialization will fail silently.</summary>
			public StringEnumConverterWithDefault() {
				throw new InvalidOperationException("Default value must be provided for StringEnumConverterWithDefault.");
			}

			///<summary>NewtonSoft.Json.JsonConverter calls this constructor when we provide an array of arguments
			///to the attribute.</summary>
			public StringEnumConverterWithDefault(T defaultEnumValue) {
				_enumValueDefault=defaultEnumValue;
			}

			///<summary>NewtonSoft.Json.JsonConverter calls this to convert string values to Enum type T.</summary>
			public override object ReadJson(JsonReader jsonReader,Type type,object enumValueCurrent,JsonSerializer jsonSerializer) {
				string jsonValue=null;
				if(jsonReader.Value!=null) {
					jsonValue=jsonReader.Value.ToString().Trim();
				}
				bool didFindMatch=Enum.TryParse(jsonValue,ignoreCase:true,out T enumValueMatch);
				if(didFindMatch) {
					return enumValueMatch;
				}
				return _enumValueDefault;
			}
		}

	}
}
