using CodeBase;
using CodeBase.VendorDirect;
using Newtonsoft.Json;
using OpenDentBusiness.VendorDirect;
using OpenDentBusiness.WebTypes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace OpenDentBusiness {
	///<summary>PaySimple API 4 (cards and ACH), called directly from this workstation with the office's own merchant API user name and key. Every
	///request carries "Authorization: basic {user}:{key}" (PaySimple's scheme, not Base64). The key is stored as an osd1$ value (plain text from
	///Open Dental is still accepted until the next save). Only api.paysimple.com, sandbox-api.paysimple.com or the PaySimpleApiURL introspection
	///override are contacted. See docs/specs/vendor-direct/paysimple.md.</summary>
	public class PaySimple:WebBase {
		public static MockPaySimple Mock;
		///<summary>Tests only: the handler every PaySimple request goes through instead of the network. Set with SetTestHttpHandler.</summary>
		private static HttpMessageHandler _httpMessageHandlerTest;
		///<summary>Tests only: the first retry delay for GETs. Null for the production 2 seconds.</summary>
		private static TimeSpan? _timeSpanRetryBaseDelayTest;
		///<summary>Tests only: the request timeout. Null for the production 30 seconds.</summary>
		private static TimeSpan? _timeSpanTimeoutTest;
		private static HttpClient _httpClientTest;
		private static string _baseUrlHttpClientTest;
		private static readonly object _lockHttpClientTest=new object();
		public class MockPaySimple {
			public bool IsCheckoutTokenRequest;
			public bool IsExistingCustomerTokenRequest;
			public bool IsSucessPostPaymentRequest;
			public bool IsSuccessDeleteRequest;
			public bool IsFailDeleteRequest;
			public double PayAmt;
		}

		public class PropertyDescs {
			public const string PaySimpleApiUserName="PaySimple API User Name";
			public const string PaySimpleApiKey="PaySimple API Key";
			public const string PaySimplePayTypeCC="PaySimple Payment Type CC";
			public const string PaySimplePayTypeACH="PaySimple Payment Type ACH";
			public const string PaySimplePreventSavingNewCC="PaySimplePreventSavingNewCC";
			public const string PaySimplePrintReceipt="PaySimplePrintReceipt";
			public const string PaySimpleIsOnlinePaymentsEnabled="PaySimpleIsOnlinePaymentsEnabled";
			///<summary>Per clinic: "Production" (the default when blank) or "Sandbox". See PaySimple.Environments.</summary>
			public const string PaySimpleEnvironment="PaySimple Environment";
			///<summary>ClinicNum 0 only: "1" (the default when blank) runs the hourly ACH status check, "0" turns it off. Manual checks always run.
			///The job's lease lives in the "VendorJobLease.PaySimpleAchStatus" row written by the vendor job host.</summary>
			public const string PaySimpleAchStatusCheck="PaySimpleAchStatusCheck";
		}

		///<summary>Values of the "PaySimple Environment" property.</summary>
		public static class Environments {
			public const string Production="Production";
			public const string Sandbox="Sandbox";
			public const string UrlProduction="https://api.paysimple.com";
			public const string UrlSandbox="https://sandbox-api.paysimple.com";
		}

		///<summary>One clinic's PaySimple merchant credentials, with the key already decrypted. Never log or display ApiKey.</summary>
		public class PaySimpleCredentials {
			public string ApiUserName="";
			public string ApiKey="";
			///<summary>Environments.Production or Environments.Sandbox.</summary>
			public string Environment=Environments.Production;

			public PaySimpleCredentials() {
			}

			public PaySimpleCredentials(string apiUserName,string apiKey,string environment) {
				ApiUserName=(apiUserName??"").Trim();
				ApiKey=(apiKey??"").Trim();
				Environment=NormalizeEnvironment(environment);
			}

			///<summary>True if both the user name and the key are present.</summary>
			public bool IsComplete {
				get {
					return !string.IsNullOrWhiteSpace(ApiUserName) && !string.IsNullOrWhiteSpace(ApiKey);
				}
			}

			///<summary>A value that is equal for equal credentials, for grouping clinics that share one merchant account. Holds the key: keep it in
			///memory only.</summary>
			public string GetGroupingKey() {
				return Environment+"\n"+ApiUserName+"\n"+ApiKey;
			}
		}

		///<summary>Sandbox for any spelling of "Sandbox"; Production for everything else, blank included.</summary>
		public static string NormalizeEnvironment(string environment) {
			if(string.Equals((environment??"").Trim(),Environments.Sandbox,StringComparison.OrdinalIgnoreCase)) {
				return Environments.Sandbox;
			}
			return Environments.Production;
		}

		///<summary>The credentials stored for clinicNum (the clinic's rows, else the ClinicNum 0 rows), read from the cache. The key is decrypted; an
		///unreadable key counts as blank. Never throws and never calls PaySimple. Pass -1 for the current clinic.</summary>
		public static PaySimpleCredentials GetCredentials(long clinicNum) {
			Meth.NoCheckMiddleTierRole();
			if(clinicNum==-1) {
				clinicNum=Clinics.ClinicNum;
			}
			Program program=Programs.GetCur(ProgramName.PaySimple);
			if(program==null) {
				return new PaySimpleCredentials();
			}
			string apiUserName=ProgramProperties.GetValOrDefault(program.ProgramNum,PropertyDescs.PaySimpleApiUserName,clinicNum);
			string apiKey=SecretValue.ReadOrBlank(ProgramProperties.GetValOrDefault(program.ProgramNum,PropertyDescs.PaySimpleApiKey,clinicNum),
				LegacyPolicy.AcceptPlaintext);
			string environment=ProgramProperties.GetValOrDefault(program.ProgramNum,PropertyDescs.PaySimpleEnvironment,clinicNum);
			return new PaySimpleCredentials(apiUserName,apiKey,environment);
		}

		///<summary>True if clinicNum has a user name and a readable key. Doesn't check the program's Enabled flag or call PaySimple.</summary>
		public static bool HasCredentials(long clinicNum) {
			return GetCredentials(clinicNum).IsComplete;
		}

		///<summary>True unless the hourly ACH status check was turned off in PaySimple Setup. Reads the cache.</summary>
		public static bool IsAchStatusCheckOn() {
			Meth.NoCheckMiddleTierRole();
			long programNum=Programs.GetProgramNum(ProgramName.PaySimple);
			if(programNum==0) {
				return false;
			}
			return ProgramProperties.GetValOrDefault(programNum,PropertyDescs.PaySimpleAchStatusCheck,0)!="0";
		}

		///<summary>Inserts the practice-wide (ClinicNum 0) rows that older databases lack: "PaySimple Environment" (Production) and
		///"PaySimpleAchStatusCheck" (1). Also marks every "PaySimple API Key" row masked and high security. Returns true if it changed anything; the
		///caller then refreshes the program property cache. No vendor call. Safe to call often.</summary>
		public static bool EnsureProperties() {
			Meth.NoCheckMiddleTierRole();
			long programNum=Programs.GetProgramNum(ProgramName.PaySimple);
			if(programNum==0) {
				return false;
			}
			bool isChanged=false;
			if(ProgramProperties.GetOneFromDb(programNum,PropertyDescs.PaySimpleEnvironment,0)==null) {
				VendorCredentials.EnsureSetting(programNum,PropertyDescs.PaySimpleEnvironment,0,Environments.Production);
				isChanged=true;
			}
			if(ProgramProperties.GetOneFromDb(programNum,PropertyDescs.PaySimpleAchStatusCheck,0)==null) {
				VendorCredentials.EnsureSetting(programNum,PropertyDescs.PaySimpleAchStatusCheck,0,"1");
				isChanged=true;
			}
			List<ProgramProperty> listKeyRows=ProgramProperties.GetForProgram(programNum)
				.FindAll(x => x.PropertyDesc==PropertyDescs.PaySimpleApiKey && (!x.IsMasked || !x.IsHighSecurity));
			for(int i=0;i<listKeyRows.Count;i++) {
				ProgramProperties.InsertIfMissing(programNum,PropertyDescs.PaySimpleApiKey,listKeyRows[i].ClinicNum,"",true,true);
				isChanged=true;
			}
			return isChanged;
		}

		///<summary>Tests only. Sends every PaySimple request through httpMessageHandler instead of the network (null restores the network), optionally
		///with a short GET retry delay and request timeout so retry and timeout tests run quickly.</summary>
		public static void SetTestHttpHandler(HttpMessageHandler httpMessageHandler,TimeSpan? timeSpanRetryBaseDelay=null,TimeSpan? timeSpanTimeout=null) {
			lock(_lockHttpClientTest) {
				_httpMessageHandlerTest=httpMessageHandler;
				_timeSpanRetryBaseDelayTest=timeSpanRetryBaseDelay;
				_timeSpanTimeoutTest=timeSpanTimeout;
				_httpClientTest=null;
				_baseUrlHttpClientTest=null;
			}
		}

		///<summary>The base URL (no trailing slash) for the environment: the PaySimpleApiURL introspection override when one is set, else
		///api.paysimple.com or sandbox-api.paysimple.com.</summary>
		public static string GetApiBaseUrl(string environment) {
			string urlOverride="";
			bool hasOverride;
			try {
				hasOverride=Introspection.TryGetOverride(Introspection.IntrospectionEntity.PaySimpleApiURL,out urlOverride);
			}
			catch(Exception ex) {
				//No preference cache (unit tests without a database) or a malformed IntrospectionItems preference: use the real hosts.
				ex.DoNothing();
				hasOverride=false;
			}
			if(hasOverride && !string.IsNullOrWhiteSpace(urlOverride)) {
				return urlOverride.Trim().TrimEnd('/');
			}
			if(NormalizeEnvironment(environment)==Environments.Sandbox) {
				return Environments.UrlSandbox;
			}
			return Environments.UrlProduction;
		}

		///<summary>The Authorization header value: "basic {user}:{key}". PaySimple specifies no Base64 encoding.</summary>
		public static string GetAuthHeaderValue(string apiUserName,string apiKey) {
			return "basic "+apiUserName+":"+apiKey;
		}

		///<summary>A new correlation ID for one charge, sent as OrderId (PaySimple allows 50 characters). Used to find the payment after a timeout.</summary>
		public static string NewOrderId() {
			return "OPMS-"+Guid.NewGuid().ToString("N");
		}

		///<summary>The HttpClient for baseUrl: one per environment, behind the vendor-direct policy (never an Open Dental host, HTTPS only except to
		///this computer, only the base URL's host, 30 s timeout, GET-only retries after 2, 4 and 8 s on 429/5xx, POSTs never retried).</summary>
		private static HttpClient GetHttpClient(string baseUrl) {
			Uri uriBase=new Uri(baseUrl+"/");
			VendorHttpOptions vendorHttpOptions=new VendorHttpOptions();
			vendorHttpOptions.VendorName="PaySimple";
			vendorHttpOptions.BaseAddress=uriBase;
			vendorHttpOptions.ListAllowedHosts=new List<string> { uriBase.Host };
			vendorHttpOptions.Timeout=TimeSpan.FromSeconds(30);
			vendorHttpOptions.MaxGetRetries=3;
			vendorHttpOptions.RetryBaseDelay=TimeSpan.FromSeconds(2);
			vendorHttpOptions.MaxRetryWait=TimeSpan.FromSeconds(10);
			lock(_lockHttpClientTest) {
				if(_httpMessageHandlerTest!=null) {
					if(_httpClientTest==null || _baseUrlHttpClientTest!=baseUrl) {
						if(_timeSpanRetryBaseDelayTest.HasValue) {
							vendorHttpOptions.RetryBaseDelay=_timeSpanRetryBaseDelayTest.Value;
						}
						if(_timeSpanTimeoutTest.HasValue) {
							vendorHttpOptions.Timeout=_timeSpanTimeoutTest.Value;
						}
						_httpClientTest=VendorHttp.CreateClient(vendorHttpOptions,_httpMessageHandlerTest);
						_baseUrlHttpClientTest=baseUrl;
					}
					return _httpClientTest;
				}
			}
			return VendorHttp.GetClient(vendorHttpOptions);
		}

		public class WebPaymentProperties {
			public bool IsOnlinePaymentAllowed;
			///<summary>The value of the "PaySimple API Key" property.</summary>
			public string Token;
		}

		///<summary>Throws exceptions.  Will purposefully throw ODExceptions that are already translated and formatted.</summary>
		public static long AddCustomer(string fname,string lname,string idInDb="",long clinicNum=-1) {
			ValidateProgram(clinicNum);
			return PaySimpleApi.PostCustomer(GetCredentialsForCall(clinicNum),PaySimpleApi.MakeNewCustomerData(fname,lname,idInDb));
		}

		///<summary>Throws exceptions.  Will purposefully throw ODExceptions that are already translated and and formatted.</summary>
		public static ApiResponse AddCreditCard(long customerId,string ccNum,DateTime ccExpDate,string billingZipCode="",long clinicNum=-1) {
			ValidateProgram(clinicNum);
			if(customerId==0) {
				throw new ODException(Lans.g("PaySimple","Invalid PaySimple Customer ID provided: ")+customerId.ToString());
			}
			return PaySimpleApi.PostAccountCreditCard(GetCredentialsForCall(clinicNum),PaySimpleApi.MakeNewAccountCreditCardData(customerId,ccNum,ccExpDate,PaySimpleApi.GetCardType(ccNum),billingZipCode));
		}

		///<summary>Throws exceptions.  Will purposefully throw ODExceptions that are already translated and formatted.</summary>
		public static ApiResponse AddACHAccount(Patient pat,string routingNumber,string acctNumber,string bankName,bool isCheckings,
			long clinicNum)
		{
			ValidateProgram(clinicNum);
			if(pat==null || pat.PatNum==0) {
				throw new ODException(Lans.g("PaySimple","Error adding account. Patient required."));
			}
			long psCustomerId=GetCustomerIdForPat(pat.PatNum,pat.FName,pat.LName,clinicNum);
			try {
				return AddACHAccount(psCustomerId,routingNumber,acctNumber,bankName,isCheckings,clinicNum);
			}
			catch(PaySimpleException ex) {
				HandlePaySimpleException(ex,psCustomerId);
				throw;
			}
		}

		///<summary>Throws exceptions.  Will purposefully throw ODExceptions that are already translated and formatted.</summary>
		private static ApiResponse AddACHAccount(long customerId,string routingNumber,string acctNumber,string bankName,bool isCheckings,
			long clinicNum)
		{
			ValidateProgram(clinicNum);
			if(customerId==0) {
				throw new ODException(Lans.g("PaySimple","Invalid PaySimple Customer ID provided: ")+customerId.ToString());
			}
			return PaySimpleApi.PostAccountACH(GetCredentialsForCall(clinicNum),
				PaySimpleApi.MakeNewAccountACHData(customerId,routingNumber,acctNumber,bankName,isCheckings));
		}

		public static PaySimpleApiGetACHResponse GetACHAccount(long clinicNum,string accountId) {
			ValidateProgram(clinicNum);
			return PaySimpleApi.GetACHAccount(GetCredentialsForCall(clinicNum),accountId);
		}

		public static PaySimpleApiGetCCResponse GetCreditCard(long clinicNum,string accountId) {
			ValidateProgram(clinicNum);
			return PaySimpleApi.GetCreditCard(GetCredentialsForCall(clinicNum),accountId);
		}

		public static ApiResponse GetPayment(long clinicNum,long externalId=0) {
			ValidateProgram(clinicNum);
			return PaySimpleApi.GetPayment(GetCredentialsForCall(clinicNum),externalId);
		}

		///<summary>Returns the customer token for the customerId passed in.</summary>
		public static string GetCustomerToken(long paySimpleCustomerId,long clinicNum=-1) {
			ValidateProgram(clinicNum);
			return PaySimpleApi.GetCustomerToken(GetCredentialsForCall(clinicNum),paySimpleCustomerId);
		}

		///<summary>Returns the checkout token.</summary>
		public static string GetCheckoutToken(long clinicNum=-1) {
			ValidateProgram(clinicNum);
			return PaySimpleApi.GetCheckoutToken(GetCredentialsForCall(clinicNum));
		}

		///<summary>The webhook subscriptions in the PaySimple account for clinicNum's stored credentials. Throws exceptions.</summary>
		public static List<ApiWebhookResponse> GetAchWebhooks(long clinicNum) {
			ValidateProgram(clinicNum);
			return PaySimpleApi.GetWebhooks(GetCredentialsForCall(clinicNum));
		}

		///<summary>The webhook subscriptions in the PaySimple account for the given credentials, which the setup window may not have saved yet. Lists
		///only; nothing is created. An office coming from Open Dental may find subscriptions that still send its payment events to Open Dental.
		///Throws PaySimpleCredentialsException for rejected credentials, other exceptions for other failures.</summary>
		public static List<ApiWebhookResponse> GetWebhooks(PaySimpleCredentials paySimpleCredentials) {
			ValidateCredentials(paySimpleCredentials);
			return PaySimpleApi.GetWebhooks(paySimpleCredentials);
		}

		///<summary>Removes one webhook subscription from the PaySimple account. Only after the user confirmed it, never automatically: another
		///product could own a subscription. Throws exceptions.</summary>
		public static void DeleteWebhook(PaySimpleCredentials paySimpleCredentials,string webhookId) {
			ValidateCredentials(paySimpleCredentials);
			if(string.IsNullOrWhiteSpace(webhookId)) {
				throw new ODException(Lans.g("PaySimple","Invalid webhook subscription ID."));
			}
			PaySimpleApi.DeleteWebhook(paySimpleCredentials,webhookId);
		}

		///<summary>True if PaySimple accepts the credentials (GET /v4/customer?page=1&amp;pagesize=1 answers 200), false if it rejects them (401).
		///Throws for anything else (no network, PaySimple error). Doesn't need the program to be enabled, so Setup can test before saving.</summary>
		public static bool TestCredentials(PaySimpleCredentials paySimpleCredentials) {
			ValidateCredentials(paySimpleCredentials);
			try {
				PaySimpleApi.GetFirstCustomer(paySimpleCredentials);
			}
			catch(PaySimpleCredentialsException ex) {
				ex.DoNothing();
				return false;
			}
			return true;
		}

		///<summary>TestCredentials for clinicNum's stored credentials.</summary>
		public static bool TestCredentials(long clinicNum) {
			return TestCredentials(GetCredentials(clinicNum));
		}

		///<summary>Status details of one payment (GET /v4/payment/{id}), for the ACH status check. Throws exceptions; PaySimpleCredentialsException
		///for rejected credentials, PaySimpleException for a PaySimple error such as an unknown payment ID.</summary>
		public static PaySimplePaymentStatus GetPaymentStatus(PaySimpleCredentials paySimpleCredentials,string paymentId) {
			ValidateCredentials(paySimpleCredentials);
			if(string.IsNullOrWhiteSpace(paymentId)) {
				throw new ODException(Lans.g("PaySimple","Invalid PaySimple Payment ID."));
			}
			return PaySimpleApi.GetPaymentStatus(paySimpleCredentials,paymentId.Trim());
		}

		///<summary>GetPaymentStatus for clinicNum's stored credentials. Needs the program enabled.</summary>
		public static PaySimplePaymentStatus GetPaymentStatus(long clinicNum,string paymentId) {
			ValidateProgram(clinicNum);
			return GetPaymentStatus(GetCredentialsForCall(clinicNum),paymentId);
		}

		///<summary>One page (1-based) of the account's payments with any of the given statuses, created on or after dateStart (lite objects, up to
		///PaySimpleApi page size per page). Used to catch late ACH returns. Throws exceptions.</summary>
		public static PaySimplePaymentPage ListPayments(PaySimpleCredentials paySimpleCredentials,string[] arrayStatuses,DateTime dateStart,int page) {
			ValidateCredentials(paySimpleCredentials);
			return PaySimpleApi.ListPayments(paySimpleCredentials,arrayStatuses,dateStart,Math.Max(1,page));
		}

		///<summary>ListPayments for clinicNum's stored credentials. Needs the program enabled.</summary>
		public static PaySimplePaymentPage ListPayments(long clinicNum,string[] arrayStatuses,DateTime dateStart,int page) {
			ValidateProgram(clinicNum);
			return ListPayments(GetCredentialsForCall(clinicNum),arrayStatuses,dateStart,page);
		}

		///<summary>The customer's payment whose OrderId is orderId, or null. Used after a charge timed out, to learn whether PaySimple took it.
		///Throws exceptions.</summary>
		public static PaySimplePaymentStatus FindPaymentByOrderId(PaySimpleCredentials paySimpleCredentials,long customerId,string orderId) {
			ValidateCredentials(paySimpleCredentials);
			if(customerId<=0 || string.IsNullOrWhiteSpace(orderId)) {
				return null;
			}
			return PaySimpleApi.FindPaymentByOrderId(paySimpleCredentials,customerId,orderId);
		}

		///<summary>FindPaymentByOrderId for clinicNum's stored credentials. Needs the program enabled.</summary>
		public static PaySimplePaymentStatus FindPaymentByOrderId(long clinicNum,long customerId,string orderId) {
			ValidateProgram(clinicNum);
			return FindPaymentByOrderId(GetCredentialsForCall(clinicNum),customerId,orderId);
		}

		///<summary>Throws exceptions.  Will purposefully throw ODExceptions that are already translated and and formatted.
		///If PatNum is 0, we will make a one time payment for an UNKNOWN patient.  This is currently only intended for prepaid insurance cards.
		///Returns the PaymentId given by PaySimple. Paramater lname can be set to carrier name sometimes.</summary>
		public static ApiResponse MakePayment(long patNum,CreditCard cc,decimal payAmt,string ccNum,DateTime ccExpDate,bool isOneTimePayment
			,string billingZipCode="",string cvv="",long clinicNum=-1,string lname="") 
		{
			ValidateProgram(clinicNum);
			if(patNum==0) {
				//MakePaymentNoPat will validate its credentials.
				return MakePaymentNoPat(payAmt,ccNum,ccExpDate,billingZipCode,cvv,clinicNum,lname);
			}
			if((cc==null || string.IsNullOrWhiteSpace(cc.PaySimpleToken)) && (string.IsNullOrWhiteSpace(ccNum) || ccExpDate.Year<DateTime.Today.Year)) {
				throw new ODException(Lans.g("PaySimple","Error making payment"));
			}
			if(cc==null) {
				cc=new CreditCard() {
					PatNum=patNum,
					PaySimpleToken="",
					CCSource=CreditCardSource.PaySimple,//Payment made from ODProper. PaymentPortal should never pass a null CreditCard object.
				};
			}
			if(string.IsNullOrWhiteSpace(cc.PaySimpleToken)) {
				Patient patCur=Patients.GetPat(cc.PatNum);
				if(patCur==null) {
					patCur=new Patient() {
						PatNum=patNum,
						FName="",
						LName="",
					};
				}
				long psCustomerId=GetCustomerIdForPat(patCur.PatNum,patCur.FName,patCur.LName,clinicNum);
				ApiResponse apiResponse;
				try {
					apiResponse=AddCreditCard(psCustomerId,ccNum,ccExpDate,billingZipCode,clinicNum);
				}
				catch(PaySimpleException ex) {
					HandlePaySimpleException(ex,psCustomerId);
					throw;
				}
				cc.PaySimpleToken=apiResponse.PaySimpleToken;
				//If the user doesn't want Open Dental to store their account id, we will let them continue entering their CC info.
				if(!isOneTimePayment && cc.CreditCardNum>0) {
					CreditCards.Update(cc);
				}
			}
			string orderId=NewOrderId();
			return PostPaymentReconciled(GetCredentialsForCall(clinicNum),
				PaySimpleApi.MakeNewPaymentData(PIn.Long(cc.PaySimpleToken),payAmt,cvv,orderId),orderId,PIn.Long(cc.PaySimpleToken),
				0,patNum,false,cc.CCSource);
		}

		///<summary>Posts a charge. If the result is unknown (a timeout or a dropped connection after sending), looks for the charge by its OrderId
		///among the customer's payments: found, it is returned like a normal response (or throws like a decline); not found, or the lookup fails,
		///throws PaySimpleResultUnknownException. Never posts the charge twice. customerId may be 0; it is then taken from the patient's PaySimple
		///link (patNum), else read from the stored account.</summary>
		private static ApiResponse PostPaymentReconciled(PaySimpleCredentials paySimpleCredentials,string postData,string orderId,long accountId,
			long customerId,long patNum,bool isAch,CreditCardSource ccSource)
		{
			try {
				return PaySimpleApi.PostPayment(paySimpleCredentials,postData,ccSource);
			}
			catch(PaySimpleResultUnknownException ex) {
				ex.DoNothing();
				PaySimplePaymentStatus paySimplePaymentStatus=null;
				try {
					if(customerId<=0) {
						customerId=GetLinkedCustomerId(patNum);
					}
					if(customerId<=0 && accountId>0) {
						customerId=isAch
							? PaySimpleApi.GetACHAccount(paySimpleCredentials,POut.Long(accountId)).CustomerId
							: PaySimpleApi.GetCreditCard(paySimpleCredentials,POut.Long(accountId)).CustomerId;
					}
					if(customerId>0) {
						paySimplePaymentStatus=PaySimpleApi.FindPaymentByOrderId(paySimpleCredentials,customerId,orderId);
					}
				}
				catch(Exception exLookup) {
					exLookup.DoNothing();//The original "result unknown" is what the user needs to see.
				}
				if(paySimplePaymentStatus==null) {
					throw;
				}
				return PaySimpleApi.ToSaleResponse(paySimplePaymentStatus,ccSource);
			}
		}

		///<summary>The PaySimple customer ID linked to patNum, or 0. No vendor call.</summary>
		private static long GetLinkedCustomerId(long patNum) {
			if(patNum<=0) {
				return 0;
			}
			return PatientLinks.GetPatNumsLinkedFrom(patNum,PatientLinkType.PaySimple).FirstOrDefault();
		}

		///<summary>Throws exceptions.  Will purposefully throw ODExceptions that are already translated and and formatted.</summary>
		public static ApiResponse MakePaymentByToken(Patient pat,CreditCard cc,decimal payAmt,long clinicNum=-1,bool isWeb=false) {
			ValidateProgram(clinicNum);
			if(cc==null || string.IsNullOrWhiteSpace(cc.PaySimpleToken)) {
				throw new ODException(Lans.g("PaySimple","Error making payment by token"));
			}
			if(isWeb) {
				if(cc.CCSource.In(CreditCardSource.PaySimplePaymentPortal,CreditCardSource.PaySimplePaymentPortalGuest)) {
					return MakePayment(cc.PatNum,cc,payAmt,"",DateTime.MinValue,false,"","",clinicNum);
				}
				else if(cc.CCSource==CreditCardSource.PaySimplePaymentPortalACH) {
					return MakePaymentACH(pat,cc,payAmt,"","","",false,false,clinicNum);
				}
				else {
					throw new ODException(Lans.g("PaySimple","Error making payment by token. Credit card source not supported."));
				}
			}
			if(cc.CCSource==CreditCardSource.PaySimple) {
				return MakePayment(cc.PatNum,cc,payAmt,"",DateTime.MinValue,false,"","",clinicNum);
			}
			return MakePaymentACH(pat,cc,payAmt,"","","",false,false,clinicNum);
		}

		///<summary>Throws exceptions.  Will purposefully throw ODExceptions that are already translated and and formatted.</summary>
		public static ApiResponse VoidPayment(string paySimplePaymentId,long clinicNum=-1) {
			ValidateProgram(clinicNum);
			if(string.IsNullOrWhiteSpace(paySimplePaymentId)) {
				throw new Exception(Lans.g("PaySimple","Invalid PaySimple Payment ID to void."));
			}
			return PaySimpleApi.PutPaymentVoided(GetCredentialsForCall(clinicNum),paySimplePaymentId);
		}

		///<summary>Throws exceptions.  Will purposefully throw ODExceptions that are already translated and and formatted.</summary>
		public static ApiResponse ReversePayment(string paySimplePaymentId,long clinicNum=-1) {
			ValidateProgram(clinicNum);
			if(string.IsNullOrWhiteSpace(paySimplePaymentId)) {
				throw new Exception(Lans.g("PaySimple","Invalid PaySimple Payment ID to reverse."));
			}
			return PaySimpleApi.PutPaymentReversed(GetCredentialsForCall(clinicNum),paySimplePaymentId);
		}

		///<summary>Throws exceptions.  Will purposefully throw ODExceptions that are already translated and and formatted.
		///Returns the PaymentId given by PaySimple.</summary>
		private static ApiResponse MakePaymentNoPat(decimal payAmt,string ccNum,DateTime ccExpDate,string billingZipCode="",string cvv=""
			,long clinicNum=-1,string lname="") 
		{
			ValidateProgram(clinicNum);
			if(string.IsNullOrWhiteSpace(ccNum) || ccExpDate.Year<DateTime.Today.Year) {
				throw new ODException(Lans.g("PaySimple","Error making payment"));
			}
			long psCustomerId=AddCustomer("UNKNOWN",lname,"",clinicNum);
			ApiResponse apiResponse=AddCreditCard(psCustomerId,ccNum,ccExpDate,billingZipCode,clinicNum);
			string accountId=apiResponse.PaySimpleToken;
			string orderId=NewOrderId();
			return PostPaymentReconciled(GetCredentialsForCall(clinicNum),PaySimpleApi.MakeNewPaymentData(PIn.Long(accountId),payAmt,cvv,orderId),
				orderId,PIn.Long(accountId),psCustomerId,0,false,CreditCardSource.PaySimple);
		}

		///<summary>Will throw ODExceptions that are already translated and and formatted.
		///If pat is null, we will make a one time payment for an UNKNOWN patient.  This is currently only intended for prepaid insurance cards.
		///Returns the ApiResponse based on what is given us by PaySimple.</summary>
		public static ApiResponse MakePaymentACH(Patient pat,CreditCard cc,decimal payAmt,string routingNumber,string acctNumber,string bankName,
			bool isCheckings,bool isOneTimePayment,long clinicNum=-1)
		{
			ValidateProgram(clinicNum);
			if(pat==null || pat.PatNum==0) {
				throw new ODException(Lans.g("PaySimple","Error making payment. Patient required."));//No patient is allowed only for prepaid credit cards
			}
			if((cc==null || string.IsNullOrWhiteSpace(cc.PaySimpleToken)) 
				&& (string.IsNullOrWhiteSpace(routingNumber) || string.IsNullOrWhiteSpace(acctNumber) || string.IsNullOrWhiteSpace(bankName)))
			{
				throw new ODException(Lans.g("PaySimple","Error making payment."));
			}
			if(cc==null) {
				cc=new CreditCard() {
					PatNum=pat.PatNum,
					PaySimpleToken="",
					CCSource=CreditCardSource.PaySimpleACH,
				};
			}
			if(string.IsNullOrWhiteSpace(cc.PaySimpleToken)) {
				long psCustomerId=GetCustomerIdForPat(pat.PatNum,pat.FName,pat.LName,clinicNum);
				try {
					ApiResponse apiResponse=AddACHAccount(psCustomerId,routingNumber,acctNumber,bankName,isCheckings,clinicNum);
					cc.PaySimpleToken=apiResponse.PaySimpleToken;
				}
				catch(PaySimpleException ex) {
					HandlePaySimpleException(ex,psCustomerId);
				}
			}
			string orderId=NewOrderId();
			return PostPaymentReconciled(GetCredentialsForCall(clinicNum),PaySimpleApi.MakeNewPaymentACHData(PIn.Long(cc.PaySimpleToken),payAmt,orderId),
				orderId,PIn.Long(cc.PaySimpleToken),0,pat.PatNum,true,cc.CCSource);
		}

		///<summary>Deletes the credit card.</summary>
		public static ApiResponse DeleteCreditCard(CreditCard creditCardCur) {
			ValidateProgram(creditCardCur.ClinicNum);
			if(string.IsNullOrWhiteSpace(creditCardCur.PaySimpleToken)) {
				throw new Exception(Lans.g("PaySimple","Invalid PaySimple Credit Card ID to void."));
			}
			return PaySimpleApi.DeleteAccountCreditCard(GetCredentialsForCall(creditCardCur.ClinicNum),creditCardCur.PaySimpleToken);
		}

		///<summary>Deletes the ACH account.</summary>
		public static ApiResponse DeleteACHAccount(CreditCard creditCardCur) {
			ValidateProgram(creditCardCur.ClinicNum);
			if(string.IsNullOrWhiteSpace(creditCardCur.PaySimpleToken)) {
				throw new Exception(Lans.g("PaySimple","Invalid PaySimple ACN Account ID to void."));
			}
			return PaySimpleApi.DeleteAccountACH(GetCredentialsForCall(creditCardCur.ClinicNum),creditCardCur.PaySimpleToken);
		}
		
		///<summary>The stored credentials for the API call: clinicNum's, or the current clinic's for -1. ValidateProgram has checked them.</summary>
		private static PaySimpleCredentials GetCredentialsForCall(long clinicNum=-1) {
			return GetCredentials(clinicNum);
		}

		///<summary>Throws an ODException if the credentials are missing a user name or key.</summary>
		private static void ValidateCredentials(PaySimpleCredentials paySimpleCredentials) {
			if(paySimpleCredentials==null || !paySimpleCredentials.IsComplete) {
				throw new ODException(Lans.g("PaySimple","PaySimple Username or Key is empty."));
			}
		}

		///<summary>Throws exceptions if the PaySimple program or program properties are not valid.
		///If this method doesn't throw an exception, everything is assumed to be valid.</summary>
		private static void ValidateProgram(long clinicNum=-1) {
			if(clinicNum==-1) {
				clinicNum=Clinics.ClinicNum;
			}
			Program progPaySimple=Programs.GetCur(ProgramName.PaySimple);
			if(progPaySimple==null) {
				throw new ODException(Lans.g("PaySimple","PaySimple program does not exist in the database.  Please call support."));
			}
			if(!progPaySimple.Enabled) {
				throw new ODException(Lans.g("PaySimple","PaySimple is not enabled."));
			}
			//The key is decrypted here: an unreadable osd1$ value counts as empty, so it is never sent as if it were the key.
			PaySimpleCredentials paySimpleCredentials=GetCredentials(clinicNum);
			string payType=ProgramProperties.GetValOrDefault(progPaySimple.ProgramNum,PropertyDescs.PaySimplePayTypeCC,clinicNum);
			if(!paySimpleCredentials.IsComplete || string.IsNullOrWhiteSpace(payType)) {
				throw new ODException(Lans.g("PaySimple","PaySimple Username, Key, or PayType is empty."));
			}
		}

		///<summary>Return true if PaySimple is enabled for the passed in clinicNum.</summary>
		public static bool IsEnabledForClinic(long clinicNum) {
			try {
				ValidateProgram(clinicNum);
			} 
			catch {
				return false;
			}
			return true;
		}

		///<summary>Returns the CustomerId that PaySimple gave us for the given patNum.
		///If addToPaySimpleIfMissing, the PaySimple API will add the given patNum if a link isn't in our database and return the new CustomerId.
		///Otherwise, it will return 0.</summary>
		public static long GetCustomerIdForPat(long patNum,string fname,string lname,long clinicNum=-1) {
			long psCustomerId=PatientLinks.GetPatNumsLinkedFrom(patNum,PatientLinkType.PaySimple).FirstOrDefault();
			if(psCustomerId==0) {//Patient doesn't have a PaySimpleCustomerId
				psCustomerId=AddCustomer(fname,lname,(patNum>0 ? "PatNum: "+patNum.ToString() : ""),clinicNum);
				PatientLinks.Insert(new PatientLink() {
					PatNumFrom=patNum,
					PatNumTo=psCustomerId,
					LinkType=PatientLinkType.PaySimple,
				});
			}
			return psCustomerId;
		}

		public static void HandlePaySimpleException(PaySimpleException exception,long customerId) {
			if(exception.ErrorCode=="InvalidInput" && exception.ErrorMessages.Count==1
				&& exception.ErrorMessages[0]==$"Customer id {customerId} does not exist") 
			{
				exception.ErrorType=PaySimpleError.CustomerDoesNotExist;
				exception.CustomerId=customerId;
			}
			throw exception;
		}

		///<summary>OD response object for PaySimple API method responses.</summary>
		public class ApiResponse {
			///<summary>The transaction that was just processed.</summary>
			public TransType TransType;
			///<summary>The status of the PaySimple response.</summary>
			public string Status;
			///<summary>The returned ProviderAuthCode from PaySimple.</summary>
			public string AuthCode;
			///<summary>The PaySimple Payment ID of the payment that was handled.</summary>
			public string RefNumber;
			///<summary>The PaySimple Account ID of the credit card used to make the payment.</summary>
			public string PaySimpleToken;
			//Commented out CardType.  There isn't a consistent way to get the card type without making extra API calls to PaySimple.
			/////<summary>The issuer of the credit card (E.g. VISA or MASTERCARD).</summary>
			//public string CardType;
			///<summary>Not given from the API.  This is filled after returning back to Open Dental 
			///and uses Open Dental specific things to generate this (ie. clinic info)</summary>
			public string TransactionReceipt;
			public decimal Amount;
			///<summary>Should be either PaySimple or PaySimpleACH.</summary>
			public CreditCardSource CCSource=CreditCardSource.PaySimple;
			/// <summary>Includes information about why a payment failed. </summary>
			public string FailureDescription;
			public bool IsDeclined;
			public string MerchantActionText;

			///<summary>Builds the receipt string for a web service transaction.
			///This method assumes ccExpYear is a 4 digit integer.</summary>
			public void BuildReceiptString(string ccNum,int ccExpMonth,int ccExpYear,string nameOnCard,long clinicNum,bool wasSwiped=false,
				bool isACH=false,bool isVoidingRefund=false) 
			{
				string result="";
				int xleft=0;
				int xright=15;
				result+=Environment.NewLine;
				result+=CreditCardUtils.AddClinicToReceipt(clinicNum);
				//Print body
				result+="Date".PadRight(xright-xleft,'.')+DateTime.Now.ToString()+Environment.NewLine;
				result+=Environment.NewLine;
				result+="Trans Type".PadRight(xright-xleft,'.')+this.TransType.ToString()+Environment.NewLine;
				result+=Environment.NewLine;
				result+="Transaction #".PadRight(xright-xleft,'.')+this.RefNumber+Environment.NewLine;
				if(!string.IsNullOrWhiteSpace(nameOnCard)) {
					result+="Name".PadRight(xright-xleft,'.')+nameOnCard+Environment.NewLine;
				}
				result+="Account".PadRight(xright-xleft,'.');
				for(int i = 0;i<ccNum.Length-4;i++) {
					result+="*";
				}
				if(!string.IsNullOrWhiteSpace(ccNum)) {
					result+=ccNum.Substring(ccNum.Length-4)+Environment.NewLine;//last 4 digits of card number only.
				}
				if(ccExpMonth>=0 && ccExpYear>=0) {
					result+="Exp Date".PadRight(xright-xleft,'.')+ccExpMonth.ToString().PadLeft(2,'0')+(ccExpYear%100)+Environment.NewLine;
				}
				result+="Entry".PadRight(xright-xleft,'.')+(wasSwiped ? "Swiped" : "Manual")+Environment.NewLine;
				result+="Auth Code".PadRight(xright-xleft,'.')+this.AuthCode+Environment.NewLine;
				result+="Result".PadRight(xright-xleft,'.')+this.Status+Environment.NewLine;
				result+=Environment.NewLine+Environment.NewLine+Environment.NewLine;
				if(this.TransType.In(PaySimple.TransType.RETURN,PaySimple.TransType.VOID) && !isVoidingRefund) {
					result+="Total Amt".PadRight(xright-xleft,'.')+(this.Amount*-1)+Environment.NewLine;
				}
				else {
					result+="Total Amt".PadRight(xright-xleft,'.')+this.Amount+Environment.NewLine;
				}
				if(this.TransType==TransType.SALE) {
					result+=Environment.NewLine+Environment.NewLine+Environment.NewLine;
					if(isACH) {
						result+="I agree to pay the above total amount according to my bank agreement.";
					}
					else {
						result+="I agree to pay the above total amount according to my card issuer/bank agreement.";
					}
				}
				this.TransactionReceipt=result;
			}

			///<summary>Returns the translated and note-formatted string that represents the result of the API call.</summary>
			public string ToNoteString(string clinicDesc="",string entry="",string curUserName="",string expDateStr="",string cardType="") {
				string retVal="";
				if(!string.IsNullOrWhiteSpace(clinicDesc)) {
					retVal+=Lans.g("PaySimple","Clinic")+": "+clinicDesc+Environment.NewLine;
				}
				retVal+=Lans.g("PaySimple","Transaction Type")+": "+Enum.GetName(typeof(TransType),this.TransType)+Environment.NewLine+
					Lans.g("PaySimple","Status")+": "+this.Status+Environment.NewLine+
					Lans.g("PaySimple","Auth Code")+": "+this.AuthCode+Environment.NewLine+
					Lans.g("PaySimple","Amount")+": "+this.Amount+Environment.NewLine+
					Lans.g("PaySimple","PaySimple Account ID")+": "+this.PaySimpleToken+Environment.NewLine+
					Lans.g("PaySimple","PaySimple Transaction Number")+": "+this.RefNumber+Environment.NewLine;
				if(!string.IsNullOrWhiteSpace(entry)) {
					retVal+=Lans.g("PaySimple","Entry")+": "+entry+Environment.NewLine;
				}
				if(!string.IsNullOrWhiteSpace(curUserName)) {
					retVal+=Lans.g("PaySimple","Clerk")+": "+curUserName+Environment.NewLine;
				}
				if(!string.IsNullOrWhiteSpace(expDateStr)) {
					retVal+=Lans.g("PaySimple","Expiration")+": "+expDateStr+Environment.NewLine;
				}
				if(!string.IsNullOrWhiteSpace(cardType)) {
					retVal+=Lans.g("PaySimple","Card Type")+": "+cardType+Environment.NewLine;
				}
				return retVal;
			}
		}

		public class ApiWebhookResponse {
			public string WebhookId;
			public string WebhookUrl;
			public List<string> WebhookTypes;
			public bool IsActive;
		}

		public class PaySimpleApiGetCCResponse {
			public string CCNumberMasked;
			public string CCExpiration;
			public string Issuer;
			public string BillingZipCode;
			public long CustomerId;
			public bool IsDefault;
			public bool RequiresValidation;
			public long Id;
			public string LastModified;
			public string CreatedOn;
		}

		public class PaySimpleApiGetACHResponse {
			public bool IsCheckingAccount;
			public string RoutingNumber;
			public string AccountNumber;
			public string BankName;
			public long CustomerId;
			public bool IsDefault;
			public bool RequiresValidation;
			public long Id;
			public string LastModified;
			public string CreatedOn;
		}

		private class PaySimpleApi {

			#region SDK Calls
			///<summary>Throws exceptions for http codes of 300 or more from API call.</summary>
			public static string GetCheckoutToken(PaySimpleCredentials creds) {
				var response=Request(ApiRoute.Token,HttpMethod.Post,creds,"{}",
					#region ResponseType Object
					new {
						Meta=new {
							Errors=new {
								ErrorCode="InvalidInput",
								ErrorMessages=new [] { new {
										Field="",
										Message="",
									}
								}
							},
							HttpStatus="",
							HttpStatusCode="",
							PagingDetails="",
						},
						Response=new {
							JwtToken="",
							Expiration="",
						}
					}
					#endregion
				);
				if(response==null || response.FullResponse==null || response.FullResponse.Response==null) {
					throw new Exception("Unexpected response from PaySimple"+(response!=null ? ": "+response.RawResponse : ""));
				}
				return response.FullResponse.Response.JwtToken;
			}

			///<summary>Throws exceptions for http codes of 300 or more from API call.</summary>
			public static string GetCustomerToken(PaySimpleCredentials creds,long paySimpleCustID) {
				var response=Request(ApiRoute.CustomerToken,HttpMethod.Get,creds,"",
					#region ResponseType Object
					new {
						Meta=new {
							Errors=new {
								ErrorCode="InvalidInput",
								ErrorMessages=new [] { new {
										Field="",
										Message="",
									}
								}
							},
							HttpStatus="",
							HttpStatusCode="",
							PagingDetails="",
						},
						Response=new {
							JwtToken="",
						}
					}
					#endregion
					,paySimpleCustID.ToString()
				);
				if(response==null || response.FullResponse==null || response.FullResponse.Response==null) {
					throw new Exception("Unexpected response from PaySimple"+(response!=null ? ": "+response.RawResponse : ""));
				}
				return response.FullResponse.Response.JwtToken;
			}
			#endregion

			///<summary>Throws exceptions for http codes of 300 or more from API call.</summary>
			public static long PostCustomer(PaySimpleCredentials creds,string postData) {
				var response=Request(ApiRoute.Customer,HttpMethod.Post,creds,postData,
					#region ResponseType Object
					new {
						Meta=new {
							Errors=new {
								ErrorCode="InvalidInput",
								ErrorMessages=new [] { new {
										Field="",
										Message="",
									}
								}
							},
							HttpStatus="",
							HttpStatusCode="",
							PagingDetails="",
						},
						Response=new {
							MiddleName="",
							AltEmail="",
							AltPhone="",
							MobilePhone="",
							Fax="",
							Website="",
							BillingAddress="",
							ShippingSameAsBilling=true,
							ShippingAddress="",
							Company="",
							Notes="",
							CustomerAccount="",
							FirstName="",
							LastName="",
							Email="",
							Phone="",
							Id=(long)0,
							LastModified="",
							CreatedOn="",
						}
					}
					#endregion
				);
				if(response==null || response.FullResponse==null || response.FullResponse.Response==null) {
					throw new Exception("Unexpected response from PaySimple"+(response!=null ? ": "+response.RawResponse : ""));
				}
				return response.FullResponse.Response.Id;
			}

			///<summary>Throws exceptions for http codes of 300 or more from API call.</summary>
			public static ApiResponse PostAccountCreditCard(PaySimpleCredentials creds,string postData) {
				var response=Request(ApiRoute.AccountCreditCard,HttpMethod.Post,creds,postData,
					#region ResponseType Object
					new {
						Meta=new {
							Errors=new {
								ErrorCode="InvalidInput",
								ErrorMessages=new [] { new {
										Field="",
										Message="",
									}
								}
							},
							HttpStatus="",
							HttpStatusCode="",
							PagingDetails="",
						},
						Response=new {
							CreditCardNumber="",
							ExpirationDate="",
							Issuer="",
							BillingZipCode="",
							CustomerId=(long)0,
							IsDefault=true,
							Id=395560,
							LastModified="",
							CreatedOn=""
						}
					}
					#endregion
				);
				if(response==null || response.FullResponse==null || response.FullResponse.Response==null) {
					throw new Exception("Unexpected response from PaySimple"+(response!=null ? ": "+response.RawResponse : ""));
				}
				return new ApiResponse() {
					Status="",
					AuthCode="",
					PaySimpleToken=response.FullResponse.Response.Id.ToString(),
					RefNumber="",
					TransType=TransType.AUTH,
				};
			}

			///<summary>Throws exceptions for http codes of 300 or more from API call. Throws PaySimpleResultUnknownException (carrying the OrderId in
			///postData) when the request timed out or the connection dropped, so the caller can look the payment up instead of charging again.
			///A decline or any result that isn't approved throws an Exception with PaySimple's text.</summary>
			public static ApiResponse PostPayment(PaySimpleCredentials creds,string postData,CreditCardSource ccSource) {
				var response=Request(ApiRoute.Payment,HttpMethod.Post,creds,postData,new PaymentEnvelope());
				if(response==null || response.FullResponse==null || response.FullResponse.Response==null) {
					throw new Exception("Unexpected response from PaySimple"+(response!=null ? ": "+response.RawResponse : ""));
				}
				return ToSaleResponse(response.FullResponse.Response.ToStatus(),ccSource);
			}

			///<summary>The ApiResponse for a charge PaySimple took, or throws like a decline if it wasn't approved.</summary>
			public static ApiResponse ToSaleResponse(PaySimplePaymentStatus paySimplePaymentStatus,CreditCardSource ccSource) {
				string providerAuthCode=paySimplePaymentStatus.ProviderAuthCode??"";
				if(!providerAuthCode.ToLower().Contains("approved")) {
					string errorMsg;
					if(paySimplePaymentStatus.IsDecline) {
						errorMsg="Payment was declined by PaySimple.\r\n"
							+"Message From PaySimple: "+paySimplePaymentStatus.FailureDescription+"\r\n"
							+"Merchant Action Text from PaySimple: "+paySimplePaymentStatus.MerchantActionText;
					}
					else {
						errorMsg="Payment was not approved by PaySimple.  Auth Code from PaySimple:\r\n"+providerAuthCode;
					}
					throw new Exception(errorMsg);
				}
				return new ApiResponse() {
					Status=paySimplePaymentStatus.Status,
					AuthCode=providerAuthCode,
					PaySimpleToken=paySimplePaymentStatus.AccountId.ToString(),
					RefNumber=paySimplePaymentStatus.Id.ToString(),
					TransType=TransType.SALE,
					Amount=paySimplePaymentStatus.Amount,
					CCSource=ccSource,
				};
			}

			///<summary>GET /v4/payment/{id}. Throws exceptions.</summary>
			public static PaySimplePaymentStatus GetPaymentStatus(PaySimpleCredentials creds,string paymentId) {
				var response=Request(ApiRoute.Payment,HttpMethod.Get,creds,"",new PaymentEnvelope(),Uri.EscapeDataString(paymentId));
				if(response==null || response.FullResponse==null || response.FullResponse.Response==null) {
					throw new Exception("Unexpected response from PaySimple"+(response!=null ? ": "+response.RawResponse : ""));
				}
				return response.FullResponse.Response.ToStatus();
			}

			///<summary>GET /v4/payment?status=...&amp;startdate=yyyy-MM-dd&amp;lite=true&amp;page=n&amp;pagesize=200. Throws exceptions.</summary>
			public static PaySimplePaymentPage ListPayments(PaySimpleCredentials creds,string[] arrayStatuses,DateTime dateStart,int page) {
				string query="?status="+Uri.EscapeDataString(string.Join(",",(arrayStatuses??new string[0]).Where(x => !string.IsNullOrWhiteSpace(x))))
					+"&startdate="+dateStart.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)
					+"&lite=true"
					+"&page="+page.ToString(CultureInfo.InvariantCulture)
					+"&pagesize="+PAGE_SIZE.ToString(CultureInfo.InvariantCulture);
				var response=Request(ApiRoute.Payment,HttpMethod.Get,creds,"",new PaymentListEnvelope(),"",query);
				if(response==null || response.FullResponse==null) {
					throw new Exception("Unexpected response from PaySimple"+(response!=null ? ": "+response.RawResponse : ""));
				}
				return response.FullResponse.ToPage(page);
			}

			///<summary>Pages through GET /v4/customer/{customerId}/payments (at most MAX_PAGES_ORDER_LOOKUP pages) for the payment with orderId.
			///Returns null if none. Throws exceptions.</summary>
			public static PaySimplePaymentStatus FindPaymentByOrderId(PaySimpleCredentials creds,long customerId,string orderId) {
				for(int page=1;page<=MAX_PAGES_ORDER_LOOKUP;page++) {
					string query="?page="+page.ToString(CultureInfo.InvariantCulture)+"&pagesize="+PAGE_SIZE.ToString(CultureInfo.InvariantCulture);
					var response=Request(ApiRoute.CustomerPayments,HttpMethod.Get,creds,"",new PaymentListEnvelope(),
						customerId.ToString(CultureInfo.InvariantCulture),query);
					if(response==null || response.FullResponse==null) {
						throw new Exception("Unexpected response from PaySimple"+(response!=null ? ": "+response.RawResponse : ""));
					}
					PaySimplePaymentPage paySimplePaymentPage=response.FullResponse.ToPage(page);
					PaySimplePaymentStatus paySimplePaymentStatus=paySimplePaymentPage.ListPayments
						.FirstOrDefault(x => string.Equals(x.OrderId,orderId,StringComparison.Ordinal));
					if(paySimplePaymentStatus!=null) {
						return paySimplePaymentStatus;
					}
					if(!paySimplePaymentPage.HasMorePages) {
						return null;
					}
				}
				return null;
			}

			///<summary>GET /v4/customer?page=1&amp;pagesize=1, the cheapest call that needs valid credentials. Throws PaySimpleCredentialsException on
			///401, other exceptions for other failures.</summary>
			public static void GetFirstCustomer(PaySimpleCredentials creds) {
				Request(ApiRoute.Customer,HttpMethod.Get,creds,"","","","?page=1&pagesize=1");
			}

			///<summary>DELETE /ps/webhook/subscription/{id}. Throws exceptions.</summary>
			public static void DeleteWebhook(PaySimpleCredentials creds,string webhookId) {
				Request(ApiRoute.Webhook,HttpMethod.Delete,creds,"","",Uri.EscapeDataString(webhookId));
			}

			public static PaySimpleApiGetACHResponse GetACHAccount(PaySimpleCredentials creds, string externalId) {
				#region Response
				var response=Request(ApiRoute.AccountACH,HttpMethod.Get,creds,"",
					new {
						Meta=new {
							Errors=new {
								ErrorCode="InvalidInput",
								ErrorMessages=new [] {new {
										Field="",
										Message="",
									}
								}
							},
							HttpStatus="",
							HttpStatusCode="",
							PagingDetails="",
						},
						Response=new {
							IsCheckingAccount=false,
							RoutingNumber="",
							AccountNumber="",
							BankName="",
							CustomerId=(long)0,
							IsDefault=false,
							RequiresValidation=false,
							Id=(long)0,
							LastModified="",
							CreatedOn=""
						}
					},externalId==""?"":externalId
				);
				#endregion
				if(response==null || response.FullResponse==null || response.FullResponse.Response==null) {
					throw new Exception("Unexpected response from PaySimple"+(response!=null ? ": "+response.RawResponse : ""));
				}
				return new PaySimpleApiGetACHResponse() {
					IsCheckingAccount=response.FullResponse.Response.IsCheckingAccount,
					RoutingNumber=response.FullResponse.Response.RoutingNumber,
					AccountNumber=response.FullResponse.Response.AccountNumber,
					BankName=response.FullResponse.Response.BankName,
					CustomerId=response.FullResponse.Response.CustomerId,
					IsDefault=response.FullResponse.Response.IsDefault,
					RequiresValidation=response.FullResponse.Response.RequiresValidation,
					Id=response.FullResponse.Response.Id,
					LastModified=response.FullResponse.Response.LastModified,
					CreatedOn=response.FullResponse.Response.CreatedOn,
				};
				
			}

			public static PaySimpleApiGetCCResponse GetCreditCard(PaySimpleCredentials creds,string externalId) {
				#region Response
				var response =Request(ApiRoute.AccountCreditCard,HttpMethod.Get,creds,"",
					new {
						Meta=new {
							Errors=new {
								ErrorCode="InvalidInput",
								ErrorMessages=new [] {new {
										Field="",
										Message="",
									}
								}
							},
							HttpStatus="",
							HttpStatusCode="",
							PagingDetails="",
						},
						Response=new {
							CreditCardNumber="",
							ExpirationDate="",
							Issuer="",
							BillingZipCode="",
							CustomerId=(long)0,
							IsDefault=false,
							RequiresValidation=false,
							Id=(long)0,
							LastModified="",
							CreatedOn=""
						}
					},externalId==""?"":externalId
				);
				#endregion
				if(response==null || response.FullResponse==null || response.FullResponse.Response==null) {
					throw new Exception("Unexpected response from PaySimple"+(response!=null ? ": "+response.RawResponse : ""));
				}
				return new PaySimpleApiGetCCResponse() {
					CCNumberMasked=response.FullResponse.Response.CreditCardNumber,
					CCExpiration=response.FullResponse.Response.ExpirationDate,
					Issuer=response.FullResponse.Response.Issuer,
					BillingZipCode=response.FullResponse.Response.BillingZipCode,
					CustomerId=response.FullResponse.Response.CustomerId,
					IsDefault=response.FullResponse.Response.IsDefault,
					RequiresValidation=response.FullResponse.Response.RequiresValidation,
					Id=response.FullResponse.Response.Id,
					LastModified=response.FullResponse.Response.LastModified,
					CreatedOn=response.FullResponse.Response.CreatedOn,
				};
			}

			public static ApiResponse GetPayment(PaySimpleCredentials creds,long externalId) {
				#region Response
				var response =Request(ApiRoute.Payment,HttpMethod.Get,creds,"",
					new {
						Meta=new {
							Errors=new {
								ErrorCode="InvalidInput",
								ErrorMessages=new [] {new {
										Field="",
										Message="",
									}
								}
							},
							HttpStatus="",
							HttpStatusCode="",
							PagingDetails="",
						},
						Response=new {
							CustomerId=(long)0,
							CustomerFirstName="",
							CustomerLastName="",
							CustomerCompany="",
							ReferenceId=(long)0,
							Status="Authorized",
							RecurringScheduleId=(long)0,
							PaymentType="CC",
							PaymentSubType="MOTO",
							ProviderAuthCode="Approved",
							TraceNumber="",
							PaymentDate="",
							ReturnDate="",
							EstimatedSettleDate="",
							EstimatedDepositDate="",
							ActualSettledDate="",
							CanVoidUntil="",
							FailureData=new {
								Code="",
								Description="",
								MerchantActionText="",
								IsDecline=false
							},
							AccountId=(long)0,
							InvoiceId="",
							Amount=(decimal)0.0,
							IsDebit=false,
							InvoiceNumber="123AB",//Can have alpha characters
							PurchaseOrderNumber="",
							OrderId="",
							Description="",
							Latitude="",
							Longitude="",
							SuccessReceiptOptions="",
							FailureReceiptOptions="",
							Id=(long)0,
							LastModified="",
							CreatedOn="",
						}
					},externalId==0?"":externalId.ToString()
				);
				#endregion
				if(response==null || response.FullResponse==null || response.FullResponse.Response==null) {
					throw new Exception("Unexpected response from PaySimple"+(response!=null ? ": "+response.RawResponse : ""));
				}
				ApiResponse paySimpleResponse=new ApiResponse() {
					Status=response.FullResponse.Response.Status,
					AuthCode=response.FullResponse.Response.ProviderAuthCode,
					PaySimpleToken=response.FullResponse.Response.AccountId.ToString(),
					RefNumber=response.FullResponse.Response.Id.ToString(),
					Amount=response.FullResponse.Response.Amount
				};
				if(response.FullResponse.Response.FailureData!=null) {
					paySimpleResponse.FailureDescription=response.FullResponse.Response.FailureData.Description;
					paySimpleResponse.IsDeclined=response.FullResponse.Response.FailureData.IsDecline;
					paySimpleResponse.MerchantActionText=response.FullResponse.Response.FailureData.MerchantActionText;
				}
				return paySimpleResponse;
			}

			///<summary>Gets all webhook subscriptions</summary>
			public static List<ApiWebhookResponse> GetWebhooks(PaySimpleCredentials creds) {
				var response=Request(ApiRoute.AllWebhooks,HttpMethod.Get,creds,"",
				#region Response
					new {
						total_item_count=(long)0,
						data=new [] { new {
								id="",
								url="",
								event_types=new [] {""},
								is_active=false,								
							}
						}
					}
				);
				#endregion
				if(response==null || response.FullResponse==null) {
					throw new Exception("Unexpected response from PaySimple"+(response!=null ? ": "+response.RawResponse : ""));
				}
				List<ApiWebhookResponse> listWebhooks=new List<ApiWebhookResponse>();
				if(response.FullResponse.data==null) {
					//No subscriptions.
					return listWebhooks;
				}
				for(int i=0;i<response.FullResponse.data.Length;i++) {
					if(response.FullResponse.data[i]==null) {
						continue;
					}
					ApiWebhookResponse webhookData=new ApiWebhookResponse();
					webhookData.WebhookId=response.FullResponse.data[i].id;
					webhookData.WebhookUrl=response.FullResponse.data[i].url;
					webhookData.IsActive=response.FullResponse.data[i].is_active;
					webhookData.WebhookTypes=new List<string>();
					if(response.FullResponse.data[i].event_types!=null) {
						webhookData.WebhookTypes.AddRange(response.FullResponse.data[i].event_types.Where(x => x!=null));
					}
					listWebhooks.Add(webhookData);
				}
				return listWebhooks;
			}

			///<summary>The following mock results are per PaySimple's Declined result documentation</summary>
			private static RequestResponse<T> MockDeclinedPaymentResponse<T>(T resType) {
				string resStr=@"
					{
						""Meta"": {
							""Errors"": null,
							""HttpStatus"": ""Created"",
							""HttpStatusCode"": 201,
							""PagingDetails"": null
						},
						""Response"": {
							""CustomerId"": 8051100,
							""CustomerFirstName"": ""John"",
							""CustomerLastName"": ""Doe"",
							""CustomerCompany"": ""Great Plumbing"",
							""ReferenceId"": 0,
							""Status"": ""Failed"",
							""RecurringScheduleId"": 0,
							""PaymentType"": ""CC"",
							""PaymentSubType"": ""Moto"",
							""ProviderAuthCode"": ""    DECLINE     "",
							""TraceNumber"": ""05"",
							""PaymentDate"": ""2018-05-09T06:00:00Z"",
							""ReturnDate"": null,
							""EstimatedSettleDate"": null,
							""EstimatedDepositDate"": null,
							""ActualSettledDate"": ""2018-05-09T06:00:00Z"",
							""CanVoidUntil"": ""2018-05-10T02:45:00Z"",
							""FailureData"": {
								""Code"": ""4005"",
								""Description"": ""Do Not Honor"",
								""MerchantActionText"": ""Try a different payment method"",
								""IsDecline"": true
							},
							""RequiresReceipt"": false,
							""CVV"": null,
							""AccountId"": 7320843,
							""InvoiceId"": null,
							""Amount"": 9999.01,
							""IsDebit"": false,
							""InvoiceNumber"": null,
							""PurchaseOrderNumber"": null,
							""OrderId"": null,
							""Description"": null,
							""Latitude"": null,
							""Longitude"": null,
							""SuccessReceiptOptions"": null,
							""FailureReceiptOptions"": null,
							""Id"": 29124043,
							""LastModified"": ""2018-05-09T21:03:45Z"",
							""CreatedOn"": ""2018-05-09T21:03:45Z""
						}
					}";
				return new RequestResponse<T>(JsonConvert.DeserializeAnonymousType(resStr,resType),resStr);
			}

			///<summary>The following mock results are per PaySimple's Success result documentation</summary>
			private static RequestResponse<T> MockSuccessPaymentResponse<T>(T resType,double payAmt) {
				string resStr=@"
					{
						""Meta"": {
							""Errors"": null,
							""HttpStatus"": ""Created"",
							""HttpStatusCode"": 201,
							""PagingDetails"": null
						},
						""Response"": {
							""CustomerId"": 8051100,
							""CustomerFirstName"": ""John"",
							""CustomerLastName"": ""Doe"",
							""CustomerCompany"": ""Great Plumbing"",
							""ReferenceId"": 0,
							""Status"": ""Authorized"",
							""RecurringScheduleId"": 0,
							""PaymentType"": ""CC"",
							""PaymentSubType"": ""Moto"",
							""ProviderAuthCode"": ""    approved     "",
							""TraceNumber"": ""05"",
							""PaymentDate"": ""2018-05-09T06:00:00Z"",
							""ReturnDate"": null,
							""EstimatedSettleDate"": null,
							""EstimatedDepositDate"": null,
							""ActualSettledDate"": ""2018-05-09T06:00:00Z"",
							""CanVoidUntil"": ""2018-05-10T02:45:00Z"",
							""FailureData"":null,
							""RequiresReceipt"": false,
							""CVV"": null,
							""AccountId"": 7320843,
							""InvoiceId"": null,
							""Amount"": "+payAmt+@",
							""IsDebit"": false,
							""InvoiceNumber"": null,
							""PurchaseOrderNumber"": null,
							""OrderId"": null,
							""Description"": null,
							""Latitude"": null,
							""Longitude"": null,
							""SuccessReceiptOptions"": null,
							""FailureReceiptOptions"": null,
							""Id"": 29124043,
							""LastModified"": ""2018-05-09T21:03:45Z"",
							""CreatedOn"": ""2018-05-09T21:03:45Z""
						}
					}";
				return new RequestResponse<T>(JsonConvert.DeserializeAnonymousType(resStr,resType),resStr);
			}

			private static RequestResponse<T> MockSuccessGetCreditCardResponse<T>(T resType) {
				string resStr=@"
					{
						""Meta"": {
							""Errors"": null,
							""HttpStatus"": ""Created"",
							""HttpStatusCode"": 201,
							""PagingDetails"": null
						},
						""Response"": {
							""CreditCardNumber"": ""************1111"",
							""ExpirationDate"": ""12/2020"",
							""Issuer"": ""Visa"",
							""BillingZipCode"": ""80202"",
							""CustomerId"": 9695143,
							""IsDefault"": true,
							""RequiresValidation"": false,
							""Id"": 8989970 ,
							""LastModified"": ""2018-05-09T21:03:45Z"",
							""CreatedOn"": ""2018-05-09T21:03:45Z""
						}
					}";
				return new RequestResponse<T>(JsonConvert.DeserializeAnonymousType(resStr,resType),resStr);

			}

			///<summary>The following mock return a successful Checkout Token per PaySimple's documentation.</summary>
			private static RequestResponse<T> MockCheckoutTokenResponse<T>(T resType) {
				string resStr=@"
					{
						""Meta"": {
							""Errors"": null,
							""HttpStatus"": ""Success"",
							""HttpStatusCode"": 200,
							""PagingDetails"": null
						},
						""Response"": {
							""JwtToken"":""TEST.JWT.TOKEN"",
							""Expiration"":""2050-12-01"",
						}
					}";
				return new RequestResponse<T>(JsonConvert.DeserializeAnonymousType(resStr,resType),resStr);
			}

			///<summary>The following mock return a successful Customer Token per PaySimple's documentation.</summary>
			private static RequestResponse<T> MockCustomerTokenResponse<T>(T resType) {
				string resStr=@"
					{
						""Meta"": {
							""Errors"": null,
							""HttpStatus"": ""Success"",
							""HttpStatusCode"": 200,
							""PagingDetails"": null
						},
						""Response"": {
							""JwtToken"":""EXISTING.CUSTOMER.TOKEN"",
						}
					}";
				return new RequestResponse<T>(JsonConvert.DeserializeAnonymousType(resStr,resType),resStr);
			}

			private static RequestResponse<T> MockDeleteResponseFail<T>(T resType) {
				//No need to try and deserialize a response here because the DELETE method works different than the other actions. Also our logic is set up that if the response code is not 204 No Content, then we throw an error similar to the one below.
				throw new Exception("Error deleting: 500 Some test error");
			}

			private static RequestResponse<T> MockDeleteResponseSuccess<T>(T resType) {
				//For this case, we will assume everything was successful. Our DELETE method doesn't actually use the response object so we can just have an empty object returned.
				return new RequestResponse<T>(JsonConvert.DeserializeAnonymousType("",resType),"");
			}

			///<summary>Throws exceptions for http codes of 300 or more from API call.</summary>
			public static ApiResponse PutPaymentVoided(PaySimpleCredentials creds,string paymentId) {
				var response=Request(ApiRoute.PaymentVoided,HttpMethod.Put,creds,"",
					#region ResponseType Object
					new {
						Meta=new {
							Errors=new {
								ErrorCode="InvalidInput",
								ErrorMessages=new [] { new {
										Field="",
										Message="",
									}
								}
							},
							HttpStatus="",
							HttpStatusCode="",
							PagingDetails="",
						},
						Response=new {
							CustomerId=(long)0,
							CustomerFirstName="",
							CustomerLastName="",
							CustomerCompany="",
							ReferenceId=(long)0,
							Status="Authorized",
							RecurringScheduleId=(long)0,
							PaymentType="CC",
							PaymentSubType="MOTO",
							ProviderAuthCode="Approved",
							TraceNumber="",
							PaymentDate="",
							ReturnDate="",
							EstimatedSettleDate="",
							ActualSettledDate="",
							CanVoidUntil="",
							FailureData=new {
								Code="",
								Description="",
								MerchantActionText=""
							},
							AccountId=(long)0,
							InvoiceId="",
							Amount=(decimal)0.0,
							IsDebit=false,
							InvoiceNumber="123AB",//Can have alpha characters
							PurchaseOrderNumber="",
							OrderId="",
							Description="",
							Latitude="",
							Longitude="",
							SuccessReceiptOptions="",
							FailureReceiptOptions="",
							Id=(long)0,
							LastModified="",
							CreatedOn="",
						}
					}
					#endregion
					,paymentId
				);
				if(response==null || response.FullResponse==null || response.FullResponse.Response==null) {
					throw new Exception("Unexpected response from PaySimple"+(response!=null ? ": "+response.RawResponse : ""));
				}
				if(response.FullResponse.Response.Status!="Voided") {
					throw new ODException(Lans.g("PaySimple","Payment could not be voided.  Please try again."));
				}
				return new ApiResponse() {
					Status=response.FullResponse.Response.Status,
					AuthCode=response.FullResponse.Response.ProviderAuthCode,
					PaySimpleToken=response.FullResponse.Response.AccountId.ToString(),
					RefNumber=response.FullResponse.Response.Id.ToString(),
					TransType=TransType.VOID,
					Amount=response.FullResponse.Response.Amount,
				};
			}

			///<summary>Throws exceptions for http codes of 300 or more from API call.</summary>
			public static ApiResponse PutPaymentReversed(PaySimpleCredentials creds,string paymentId) {
				var response=Request(ApiRoute.PaymentReversed,HttpMethod.Put,creds,"",
					#region ResponseType Object
					new {
						Meta=new {
							Errors=new {
								ErrorCode="InvalidInput",
								ErrorMessages=new [] { new {
										Field="",
										Message="",
									}
								}
							},
							HttpStatus="",
							HttpStatusCode="",
							PagingDetails="",
						},
						Response=new {
							CustomerId=(long)0,
							CustomerFirstName="",
							CustomerLastName="",
							CustomerCompany="",
							ReferenceId=(long)0,
							Status="Authorized",
							RecurringScheduleId=(long)0,
							PaymentType="CC",
							PaymentSubType="MOTO",
							ProviderAuthCode="Approved",
							TraceNumber="",
							PaymentDate="",
							ReturnDate="",
							EstimatedSettleDate="",
							ActualSettledDate="",
							CanVoidUntil="",
							FailureData=new {
								Code="",
								Description="",
								MerchantActionText=""
							},
							AccountId=(long)0,
							InvoiceId="",
							Amount=(decimal)0.0,
							IsDebit=false,
							InvoiceNumber="123AB",//Can have alpha characters
							PurchaseOrderNumber="",
							OrderId="",
							Description="",
							Latitude="",
							Longitude="",
							SuccessReceiptOptions="",
							FailureReceiptOptions="",
							Id=(long)0,
							LastModified="",
							CreatedOn="",
						}
					}
					#endregion
					,paymentId
				);
				if(response==null || response.FullResponse==null || response.FullResponse.Response==null) {
					throw new Exception("Unexpected response from PaySimple"+(response!=null ? ": "+response.RawResponse : ""));
				}
				if(response.FullResponse.Response.Status!="ReversePosted") {
					throw new ODException(Lans.g("PaySimple","Payment could not be reversed.  Please try again."));
				}
				return new ApiResponse() {
					Status=response.FullResponse.Response.Status,
					AuthCode=response.FullResponse.Response.ProviderAuthCode,
					PaySimpleToken=response.FullResponse.Response.AccountId.ToString(),
					RefNumber=response.FullResponse.Response.ReferenceId.ToString(),//TODO:  Check that this ID is actually the same as paymentID.  I would prefer to get it from PaySimple instead of my parameter,
					TransType=TransType.RETURN,
					Amount=response.FullResponse.Response.Amount,
				};
			}

			///<summary>Throws exceptions for http codes of 300 or more from API call.</summary>
			public static ApiResponse PostAccountACH(PaySimpleCredentials creds,string postData) {
				var response=Request(ApiRoute.AccountACH,HttpMethod.Post,creds,postData,
					#region ResponseType Object
					new {
						Meta=new {
							Errors=new {
								ErrorCode="InvalidInput",
								ErrorMessages=new [] { new {
										Field="",
										Message="",
									}
								}
							},
							HttpStatus="",
							HttpStatusCode="",
							PagingDetails="",
						},
						Response=new {
							IsCheckingAccount=true,
							RoutingNumber="",
							AccountNumber="",
							BankName="",
							CustomerId=(long)0,
							IsDefault=true,
							Id=395560,
							LastModified="",
							CreatedOn=""
						}
					}
					#endregion
				);
				if(response==null || response.FullResponse==null || response.FullResponse.Response==null) {
					throw new Exception("Unexpected response from PaySimple"+(response!=null ? ": "+response.RawResponse : ""));
				}
				return new ApiResponse() {
					Status="",
					AuthCode="",
					PaySimpleToken=response.FullResponse.Response.Id.ToString(),
					RefNumber="",
					TransType=TransType.AUTH,
					CCSource=CreditCardSource.PaySimpleACH,
				};
			}

			///<summary>Throws exceptions if http code from API call is not 204.</summary>
			public static ApiResponse DeleteAccountCreditCard(PaySimpleCredentials creds,string accountId) {
				return DeleteAccount(creds,accountId,ApiRoute.AccountCreditCard);
			}

			///<summary>Throws exceptions if http code from API call is not 204.</summary>
			public static ApiResponse DeleteAccountACH(PaySimpleCredentials creds,string accountId) {
				return DeleteAccount(creds,accountId,ApiRoute.AccountACH);
			}

			///<summary>Throws exceptions if http code from API call is not 204.</summary>
			private static ApiResponse DeleteAccount(PaySimpleCredentials creds,string accountId,ApiRoute route) {
				var response=Request(route,HttpMethod.Delete,creds,"","",accountId);
				return new ApiResponse() {
					Status="",
					AuthCode="",
					PaySimpleToken="",
					RefNumber="",
					TransType=TransType.DELETE,
				};
			}
			///<summary>Sends one request to PaySimple and deserializes the body into the shape of responseType. Throws for an http code of 300 or more:
			///PaySimpleCredentialsException for 401, PaySimpleException when PaySimple names the error, Exception otherwise. A charge (POST to the
			///payment route) that times out or loses its connection throws PaySimpleResultUnknownException, because PaySimple may have taken it.
			///query, if any, starts with "?". Every call runs on this workstation, never the middle tier server.</summary>
			private static RequestResponse<T> Request<T>(ApiRoute route,HttpMethod method,PaySimpleCredentials creds,string body,T responseType,
				string routeId="",string query="")
			{
				if(Mock!=null) {
					if(Mock.IsCheckoutTokenRequest && route==ApiRoute.Token) {
						return MockCheckoutTokenResponse(responseType);
					}
					else if(Mock.IsExistingCustomerTokenRequest && route==ApiRoute.CustomerToken) {
						return MockCustomerTokenResponse(responseType);
					}
					else if(Mock.IsSucessPostPaymentRequest && route==ApiRoute.Payment) {
						return MockSuccessPaymentResponse(responseType,Mock.PayAmt);
					}
					else if(Mock.IsSucessPostPaymentRequest && route==ApiRoute.AccountCreditCard) {
						return MockSuccessGetCreditCardResponse(responseType);
					}
					else if(Mock.IsSuccessDeleteRequest && (route==ApiRoute.AccountACH || route==ApiRoute.AccountCreditCard)) {
						return MockDeleteResponseSuccess(responseType);
					}
					else if(Mock.IsFailDeleteRequest && (route==ApiRoute.AccountACH || route==ApiRoute.AccountCreditCard)) {
						return MockDeleteResponseFail(responseType);
					}
				}
				Meth.NoCheckMiddleTierRole();
				if(creds==null || !creds.IsComplete) {
					throw new ODException(Lans.g("PaySimple","PaySimple Username or Key is empty."));
				}
				string baseUrl=GetApiBaseUrl(creds.Environment);
				Uri uri=new Uri(GetApiUrl(baseUrl,route,routeId)+(query??""));
				bool isCharge=(route==ApiRoute.Payment && method==HttpMethod.Post);
				HttpClient httpClient=GetHttpClient(baseUrl);
				int statusCode;
				string res;
				using(HttpRequestMessage httpRequestMessage=new HttpRequestMessage(method,uri)) {
					httpRequestMessage.Headers.TryAddWithoutValidation("Authorization",GetAuthHeaderValue(creds.ApiUserName,creds.ApiKey));
					httpRequestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
					if(method==HttpMethod.Post || method==HttpMethod.Put) {
						httpRequestMessage.Content=new StringContent(body??"",Encoding.UTF8,"application/json");
					}
					VendorHttp.SetEndpointName(httpRequestMessage,method.Method+" "+route.ToString());//Never the URL: IDs stay out of log lines.
					try {
						using(HttpResponseMessage httpResponseMessage=VendorHttp.RunSync(() => httpClient.SendAsync(httpRequestMessage))) {
							statusCode=(int)httpResponseMessage.StatusCode;
							res=VendorHttp.RunSync(() => httpResponseMessage.Content.ReadAsStringAsync())??"";
						}
					}
					catch(VendorHttpException ex) when(isCharge && ex.Failure==VendorHttpFailure.Timeout) {
						throw new PaySimpleResultUnknownException(GetOrderId(body),ex);
					}
					catch(VendorHttpException ex) {
						throw new Exception(ex.Message,ex);
					}
					catch(HttpRequestException ex) when(isCharge) {
						//The connection failed or dropped. If it dropped after the request went out, PaySimple may have taken the charge.
						throw new PaySimpleResultUnknownException(GetOrderId(body),ex);
					}
					catch(HttpRequestException ex) {
						throw new Exception(Lans.g("PaySimple","Could not reach PaySimple.")+" "+ex.GetBaseException().Message,ex);
					}
				}
				if(statusCode<200 || statusCode>=300) {
					if(statusCode==(int)HttpStatusCode.Unauthorized) {
						throw new PaySimpleCredentialsException();
					}
					if(!string.IsNullOrWhiteSpace(res)) {
						HandleWebException(res);//Throws a PaySimpleException if the body is PaySimple's error object.
					}
					throw new PaySimpleHttpException(statusCode,"PaySimple returned HTTP "+statusCode
						+(string.IsNullOrWhiteSpace(res) ? "" : "\r\nRaw response:\r\n"+res));
				}
				if(typeof(T)==typeof(string)) {//The caller wants the raw response, or has no use for one (DELETE answers 204 with no body).
					return new RequestResponse<T>((T)(object)res,res);
				}
				return new RequestResponse<T>(JsonConvert.DeserializeAnonymousType(res,responseType),res);
			}

			///<summary>The OrderId in a payment body, or "".</summary>
			private static string GetOrderId(string body) {
				try {
					var payment=JsonConvert.DeserializeAnonymousType(body??"",new { OrderId="" });
					return payment?.OrderId??"";
				}
				catch(Exception ex) {
					ex.DoNothing();
					return "";
				}
			}

			private static Type MockGetMethodReturnTypeV<T>(Func<T,RequestResponse<T>> mockCustomerTokenResponse) {
				return ((System.Reflection.MethodInfo)mockCustomerTokenResponse.Method).ReturnType;
			}

			///<summary>Includes the deserialized request object along with the raw response from PaySimple.</summary>
			public class RequestResponse<T> {
				public T FullResponse;
				public string RawResponse;

				public RequestResponse(T response,string rawResponse) {
					FullResponse=response;
					RawResponse=rawResponse;
				}
			}			

#region MakePostData

			///<summary>orderId is our correlation ID (NewOrderId), used to find the charge after a timeout. CardOnFileType and NetworkTransactionId
			///are not sent (open question 3 in docs/specs/vendor-direct/paysimple.md).</summary>
			public static string MakeNewPaymentData(long accountId,decimal amt,string cvv="",string orderId="") {
				return JsonConvert.SerializeObject(new {
					//Required fields:
					AccountId=accountId,
					Amount=amt,
					//Optional fields:
					//IsDebit=true,
					CVV=cvv,
					//PaymentSubType="MOTO",
					//InvoiceId="",
					//InvoiceNumber="",
					//PurchaseOrderNumber = "",
					OrderId=orderId??"",
					//Description = payment.PayNote,
					//Latitude="",
					//Longitude="",
					//SuccessReceiptOptions="",
					//SendToCustomer=false,//Dictates if a receipt gets emailed to the customer after the payment is made
					//SendToOtherAddresses="",//A specific email (or emails, surrounded in brackets separated by commas) to send the receipt to.
					//FailureReceiptOptions="",
					//SendToCustomer=false,
					//SendToOtherAddresses="",
				});
			}

			///<summary>orderId is our correlation ID (NewOrderId), used to find the charge after a timeout.</summary>
			public static string MakeNewPaymentACHData(long accountId,decimal amt,string orderId="") {
				return JsonConvert.SerializeObject(new {
					//Required fields:
					AccountId=accountId,
					Amount=amt,
					//Optional fields:
					//IsDebit=false,
					//CVV="",
					PaymentSubType="PPD",//Prearranged Payment and Deposit
					//InvoiceId="",
					//InvoiceNumber="",
					//PurchaseOrderNumber = "",
					OrderId=orderId??"",
					//Description = payment.PayNote,
					//Latitude="",
					//Longitude="",
					//SuccessReceiptOptions="",
					//SendToCustomer=false,//Dictates if a receipt gets emailed to the customer after the payment is made
					//SendToOtherAddresses="",//A specific email (or emails, surrounded in brackets separated by commas) to send the receipt to.
					//FailureReceiptOptions="",
					//SendToCustomer=false,
					//SendToOtherAddresses="",
				});
			}

			public static string MakeNewCustomerData(string fname,string lname,string idInDb="") {
				return JsonConvert.SerializeObject(new {
					//Required fields:
					FirstName=fname,
					LastName=lname,
					ShippingSameAsBilling=true,//Hardcoded because we don't support this
					//Optional fields:
					//BillingAddress=new {
					//	StreetAddress1="",
					//	StreetAddress2="",
					//	City="",
					//	StateCode="",
					//	ZipCode="",
					//	Country="",
					//},
					//ShippingAddress="",
					//Company="",
					//Notes="",
					CustomerAccount=idInDb,
					//Email="",
					//Phone="",
				});
			}

			public static string MakeNewAccountCreditCardData(long customerId,string ccNum,DateTime ccExpDate,int issuer,string billingZipCode="") {
				return JsonConvert.SerializeObject(new {
					//Required fields:
					CustomerId=customerId,
					CreditCardNumber=ccNum,
					ExpirationDate=ccExpDate.ToString("MM/yyyy"),
					Issuer=issuer,
					IsDefault=false,
					//Optional fields:
					BillingZipCode=billingZipCode,
				});
			}

			public static string MakeNewAccountACHData(long customerId,string routingNumber,string acctNumber,string bankName,bool isCheckings) {
				return JsonConvert.SerializeObject(new {
					//Required fields:
					CustomerId=customerId,
					RoutingNumber=routingNumber,
					AccountNumber=acctNumber,
					BankName=bankName,
					IsCheckingAccount=isCheckings,
					IsDefault=false,
				});
			}

			#endregion

			///<summary>Takes a credit card number and returns the issuer id from it to match PaySimple's expected values</summary>
			public static int GetCardType(string ccNum) {
				int retVal=0;
				string cardTypeName=CreditCardUtils.GetCardType(ccNum);
				if(cardTypeName=="VISA") {
					retVal=12;
				}
				else if(cardTypeName=="MASTERCARD") {
					retVal=13;
				}
				else if(cardTypeName=="AMEX") {
					retVal=14;
				}
				else if(cardTypeName=="DISCOVER") {
					retVal=15;
				}
				else {
					throw new Exception("Unsupported Card Type");
				}
				return retVal;
			}

			///<summary>Returns the full URL for the route and route id under baseUrl (GetApiBaseUrl).</summary>
			private static string GetApiUrl(string baseUrl,ApiRoute route,string routeId="") {
				string apiUrl=baseUrl;
				if(!(route==ApiRoute.Webhook || route==ApiRoute.AllWebhooks)) {
					apiUrl+="/v4";
				}
				switch(route) {
					case ApiRoute.Root:
						//Do nothing.  This is to allow someone to quickly grab the URL without having to make a copy+paste reference.
						break;
					case ApiRoute.Token:
						apiUrl+="/checkouttoken";
						break;
					case ApiRoute.Customer:
						apiUrl+="/customer";
						break;
					case ApiRoute.AccountCreditCard:
						apiUrl+="/account/creditcard"+(routeId!="" ? "/"+routeId : "");
						break;
					case ApiRoute.Payment:
						apiUrl+="/payment"+(routeId!=""?"/"+routeId:"");
						break;
					case ApiRoute.PaymentReversed:
						apiUrl+="/payment/"+routeId+"/reverse";
						break;
					case ApiRoute.PaymentVoided:
						apiUrl+="/payment/"+routeId+"/void";
						break;
					case ApiRoute.CustomerToken:
						apiUrl+="/customer/"+routeId+"/token";
						break;
					case ApiRoute.CustomerPayments:
						apiUrl+="/customer/"+routeId+"/payments";
						break;
					case ApiRoute.AccountACH:
						apiUrl+="/account/ach"+(routeId!="" ? "/"+routeId : "");
						break;
					case ApiRoute.AllWebhooks://To get all webhooks
						apiUrl+="/ps/webhook/subscriptions";
						break;
					case ApiRoute.Webhook://One webhook subscription (DELETE)
						apiUrl+="/ps/webhook/subscription"+(routeId!="" ? "/"+routeId : "");
						break;
					default:
						break;
				}
				return apiUrl;
			}

			private static void HandleWebException(string paySimpleErrorJson) {
				try {
					var errorObj=JsonConvert.DeserializeAnonymousType(paySimpleErrorJson,new {
						Meta=new {
							Errors=new {
								ErrorCode="InvalidInput",
								ErrorMessages=new[] {new {
										Field="",
										Message="",
									}
								}
							},
							HttpStatus="",
							HttpStatusCode=HttpStatusCode.NotFound,
							PagingDetails=(object)null,
						}
					});
					//A body that isn't PaySimple's error object (an HTML error page, for example) ends in the last catch; the caller then throws its own.
					//Make assumptions that every response from PaySimple matches their API documentation schema.
					//If something fails we should default to the exception that was originally thrown in the calling method.
					var metaObj=errorObj.Meta;
					var errors=metaObj.Errors;
					StringBuilder strbError=new StringBuilder();
					strbError.AppendLine("PaySimple ErrorCode:  "+errors.ErrorCode);
					List<string> listErrorMessages=new List<string>();
					if(errors.ErrorMessages.Length>0) {
						strbError.AppendLine("PaySimple Error Message(s):");
						errors.ErrorMessages.ToList().ForEach(x => {
							strbError.AppendLine(x.Message);
							listErrorMessages.Add(x.Message);
						});
					}
					//Purposefully not Lans.g and throwing PaySimpleException.  I don't want this to look like a generic exception being caught.
					throw new PaySimpleException(strbError.ToString(),errors.ErrorCode,listErrorMessages);
				}
				catch(PaySimpleException ex) {
					ex.DoNothing();
					throw;//Re-throw the stringbuilder above.
				}
				catch(Exception e) {
					e.DoNothing();//The calling method should throw if this doesn't.
				}
			}

			private enum ApiRoute {
				///<summary>Base URL, no route</summary>
				Root,
				///<summary>CheckoutToken</summary>
				Token,
				///<summary>Customer</summary>
				Customer,
				///<summary>Account/CreditCard</summary>
				AccountCreditCard,
				///<summary>Payment</summary>
				Payment,
				///<summary>Payment/PaymentId/Reverse</summary>
				PaymentReversed,
				///<summary>Payment/PaymentId/Void</summary>
				PaymentVoided,
				///<summary>Customer/Token</summary>
				CustomerToken,
				///<summary>Account/ACH</summary>
				AccountACH,
				///<summary>/ps/webhook/subscriptions</summary>
				AllWebhooks,
				///<summary>/ps/webhook/subscription</summary>
				Webhook,
				///<summary>Customer/CustomerId/Payments</summary>
				CustomerPayments,
			}

			///<summary>Page size for list calls (PaySimple's default; no documented maximum).</summary>
			private const int PAGE_SIZE=200;
			///<summary>How many pages of a customer's payments FindPaymentByOrderId reads at most.</summary>
			private const int MAX_PAGES_ORDER_LOOKUP=10;

			///<summary>PaySimple's Meta object. Errors and PagingDetails are null when absent.</summary>
			private class MetaObject {
				public object Errors { get; set; }
				public string HttpStatus { get; set; }
				public int? HttpStatusCode { get; set; }
				public PagingDetailsObject PagingDetails { get; set; }
			}

			private class PagingDetailsObject {
				public int? TotalItems { get; set; }
				public int? Page { get; set; }
				public int? ItemsPerPage { get; set; }
			}

			private class FailureDataObject {
				public string Code { get; set; }
				public string Description { get; set; }
				public string MerchantActionText { get; set; }
				public bool IsDecline { get; set; }
			}

			///<summary>PaySimple's Payment object, the fields we use. Lite list results leave most of them null.</summary>
			private class PaymentObject {
				public long? Id { get; set; }
				public long? CustomerId { get; set; }
				public long? AccountId { get; set; }
				public long? ReferenceId { get; set; }
				public string Status { get; set; }
				public string PaymentType { get; set; }
				public string PaymentSubType { get; set; }
				public string ProviderAuthCode { get; set; }
				public decimal? Amount { get; set; }
				public string OrderId { get; set; }
				public DateTime? PaymentDate { get; set; }
				public DateTime? ActualSettledDate { get; set; }
				public DateTime? ReturnDate { get; set; }
				public DateTime? CanVoidUntil { get; set; }
				public FailureDataObject FailureData { get; set; }

				public PaySimplePaymentStatus ToStatus() {
					PaySimplePaymentStatus paySimplePaymentStatus=new PaySimplePaymentStatus();
					paySimplePaymentStatus.Id=Id??0;
					paySimplePaymentStatus.CustomerId=CustomerId??0;
					paySimplePaymentStatus.AccountId=AccountId??0;
					paySimplePaymentStatus.ReferenceId=ReferenceId??0;
					paySimplePaymentStatus.Status=Status??"";
					paySimplePaymentStatus.PaymentType=PaymentType??"";
					paySimplePaymentStatus.PaymentSubType=PaymentSubType??"";
					paySimplePaymentStatus.ProviderAuthCode=ProviderAuthCode??"";
					paySimplePaymentStatus.Amount=Amount??0;
					paySimplePaymentStatus.OrderId=OrderId??"";
					paySimplePaymentStatus.PaymentDate=PaymentDate;
					paySimplePaymentStatus.ActualSettledDate=ActualSettledDate;
					paySimplePaymentStatus.ReturnDate=ReturnDate;
					paySimplePaymentStatus.CanVoidUntil=CanVoidUntil;
					if(FailureData!=null) {
						paySimplePaymentStatus.FailureCode=FailureData.Code??"";
						paySimplePaymentStatus.FailureDescription=FailureData.Description??"";
						paySimplePaymentStatus.MerchantActionText=FailureData.MerchantActionText??"";
						paySimplePaymentStatus.IsDecline=FailureData.IsDecline;
					}
					return paySimplePaymentStatus;
				}
			}

			private class PaymentEnvelope {
				public MetaObject Meta { get; set; }
				public PaymentObject Response { get; set; }
			}

			private class PaymentListEnvelope {
				public MetaObject Meta { get; set; }
				public List<PaymentObject> Response { get; set; }

				public PaySimplePaymentPage ToPage(int page) {
					PaySimplePaymentPage paySimplePaymentPage=new PaySimplePaymentPage();
					paySimplePaymentPage.Page=page;
					paySimplePaymentPage.ListPayments=(Response??new List<PaymentObject>()).Where(x => x!=null).Select(x => x.ToStatus()).ToList();
					int? totalItems=Meta?.PagingDetails?.TotalItems;
					int itemsPerPage=Meta?.PagingDetails?.ItemsPerPage??PAGE_SIZE;
					if(totalItems.HasValue) {
						paySimplePaymentPage.TotalItems=totalItems.Value;
						paySimplePaymentPage.HasMorePages=(long)page*itemsPerPage<totalItems.Value;
					}
					else {
						paySimplePaymentPage.TotalItems=-1;
						paySimplePaymentPage.HasMorePages=paySimplePaymentPage.ListPayments.Count>=itemsPerPage;
					}
					return paySimplePaymentPage;
				}
			}
		}

		///<summary>What PaySimple says about one payment. Dates are as PaySimple sent them (UTC), null when absent.</summary>
		public class PaySimplePaymentStatus {
			public long Id;
			public long CustomerId;
			public long AccountId;
			///<summary>For a reversal, the payment it reverses; for a reversed payment, its reversal.</summary>
			public long ReferenceId;
			///<summary>Pending, Posted, Settled, Failed, Voided, Reversed, ReversePosted, Returned, ReverseNSF, ChargeBack, Authorized,
			///RequiresAction, RefundSettled, ...</summary>
			public string Status="";
			public string PaymentType="";
			public string PaymentSubType="";
			public string ProviderAuthCode="";
			public decimal Amount;
			public string OrderId="";
			public DateTime? PaymentDate;
			public DateTime? ActualSettledDate;
			public DateTime? ReturnDate;
			public DateTime? CanVoidUntil;
			public string FailureCode="";
			public string FailureDescription="";
			public string MerchantActionText="";
			public bool IsDecline;
		}

		///<summary>One page of a payment list.</summary>
		public class PaySimplePaymentPage {
			public int Page;
			///<summary>-1 if PaySimple didn't say.</summary>
			public int TotalItems;
			public bool HasMorePages;
			public List<PaySimplePaymentStatus> ListPayments=new List<PaySimplePaymentStatus>();
		}

		public enum TransType {
			///<summary>Used to make a payment.</summary>
			SALE,
			///<summary>Used to add a credit card.</summary>
			AUTH,
			///<summary>Used to reverse a payment.</summary>
			RETURN,
			///<summary>Used to cancel a payment.</summary>
			VOID,
			///<summary>Used to cancel a credit card or ACH account.</summary>
			DELETE,
		}
	}

	///<summary>Exception class for errors returned from PayConnect. The Message field is a human-readable error that can be shown to the user.
	///</summary>
	public class PaySimpleException:ApplicationException {
		///<summary>Error code returned from PaySimple.</summary>
		public string ErrorCode;
		///<summary>Error messages returned from PaySimple.</summary>
		public List<string> ErrorMessages;
		///<summary>The type of error.</summary>
		public PaySimpleError ErrorType;
		///<summary>The PaySimple customer id sent in this request.</summary>
		public long CustomerId;

		public PaySimpleException(string message,string errorCode,List<string> errorMessages) : base(message) {
			ErrorCode=errorCode;
			ErrorMessages=errorMessages;
		}
	}

	///<summary>PaySimple answered with an HTTP status of 300 or more and a body that isn't its error object. 429 and 5xx mean "try later".</summary>
	public class PaySimpleHttpException:Exception {
		public int StatusCode { get; }

		public PaySimpleHttpException(int statusCode,string message) : base(message) {
			StatusCode=statusCode;
		}

		///<summary>True for 408, 429 and 5xx: PaySimple is busy or down, not refusing this request.</summary>
		public bool IsTransient {
			get {
				return StatusCode==408 || StatusCode==429 || StatusCode>=500;
			}
		}
	}

	///<summary>PaySimple rejected the API user name or key (HTTP 401). The message is translated and can be shown to the user.</summary>
	public class PaySimpleCredentialsException:ODException {
		public PaySimpleCredentialsException()
			: base(Lans.g("PaySimple","Invalid PaySimple credentials.  Check your Username and Key and try again."))
		{
		}
	}

	///<summary>A charge timed out or lost its connection, and PaySimple may or may not have taken it. Never charge again automatically: look the
	///payment up by OrderId, or have the user check PaySimple's web portal first. The message is translated and can be shown to the user.</summary>
	public class PaySimpleResultUnknownException:ODException {
		///<summary>The OrderId sent with the charge, or "".</summary>
		public string OrderId { get; }

		public PaySimpleResultUnknownException(string orderId,Exception innerException=null)
			: base(Lans.g("PaySimple","The result of this PaySimple transaction is unknown: PaySimple did not answer in time.")+"\r\n"
				+Lans.g("PaySimple","Check the payment in PaySimple's web portal before charging again.")
				+(string.IsNullOrEmpty(orderId) ? "" : "\r\n"+Lans.g("PaySimple","Order ID")+": "+orderId),innerException)
		{
			OrderId=orderId??"";
		}
	}

	///<summary>The PaySimple errors that we handle differently.</summary>
	public enum PaySimpleError {
		///<summary>We have not classified this error.</summary>
		NotSpecified,
		///<summary>A Customer object does not exist in PaySimple with the Customer id we provided.</summary>
		CustomerDoesNotExist,
	}
}
