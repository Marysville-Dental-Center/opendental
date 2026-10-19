// CareCredit (Synchrony) patient financing: the facade this program calls, with the same public surface the prebuilt Bridges.dll had (re-implemented
// from how this repository calls it; the binary was never decompiled), talking to Synchrony directly with the office's own credentials.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using CodeBase;
using CodeBase.VendorDirect;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenDentBusiness.Bridges {
	///<summary>Where carecreditwebresponse rows are read and written. The database by default; tests use an in-memory store.</summary>
	public interface ICareCreditResponseStore {
		long Insert(CareCreditWebResponse careCreditWebResponse);
		void Update(CareCreditWebResponse careCreditWebResponse);
		bool Update(CareCreditWebResponse careCreditWebResponse,CareCreditWebResponse careCreditWebResponseOld);
	}

	///<summary>The database store, through CareCreditWebResponses.</summary>
	public sealed class CareCreditResponseStoreDb : ICareCreditResponseStore {
		public long Insert(CareCreditWebResponse careCreditWebResponse) {
			return CareCreditWebResponses.Insert(careCreditWebResponse);
		}

		public void Update(CareCreditWebResponse careCreditWebResponse) {
			CareCreditWebResponses.Update(careCreditWebResponse);
		}

		public bool Update(CareCreditWebResponse careCreditWebResponse,CareCreditWebResponse careCreditWebResponseOld) {
			return CareCreditWebResponses.Update(careCreditWebResponse,careCreditWebResponseOld);
		}
	}

	///<summary>CareCredit: prefill hosted pages, pull results back, merchant numbers. Everything runs on the client workstation, never on the middle
	///tier server. With the program link disabled or the connection settings incomplete, nothing here contacts Synchrony. Opening windows
	///(ShowPage) lives in OpenDental's CareCreditL.</summary>
	public static class CareCredit {
		///<summary>A CareCredit HTTP error answer, or a configuration the request can't be sent with (ErrorCode 0).</summary>
		public class CareCreditException:Exception {
			///<summary>The HTTP status, or 0 when nothing came back from Synchrony.</summary>
			public int ErrorCode;
			///<summary>True when Synchrony refused the configured credentials.</summary>
			public bool IsAuthFailure;
			///<summary>The error answer with SSN and date-of-birth fields removed, or "".</summary>
			public string ResponseJson="";
			///<summary>Synchrony's Retry-After, if it sent one with a 429 or 503.</summary>
			public TimeSpan? RetryAfter;

			public CareCreditException(int errorCode,string message,bool isAuthFailure=false) : base(message) {
				ErrorCode=errorCode;
				IsAuthFailure=isAuthFailure;
			}
		}

		#region Dependencies (tests replace these)
		///<summary>The Synchrony client.</summary>
		public static ICareCreditApiClient ApiClient=new CareCreditApiClient();
		///<summary>Where carecreditwebresponse rows go.</summary>
		public static ICareCreditResponseStore ResponseStore=new CareCreditResponseStoreDb();
		///<summary>The saved connection settings.</summary>
		public static Func<CareCreditConfig> FuncGetConfig=CareCreditSettings.Load;
		///<summary>True while the CareCredit program link is enabled.</summary>
		public static Func<bool> FuncIsEnabled=() => Programs.IsEnabled(ProgramName.CareCredit);
		///<summary>The database server's clock.</summary>
		public static Func<DateTime> FuncNow=() => MiscData.GetNowDateTime();
		///<summary>Records (or clears, with "") the last refused-credentials message for the setup window.</summary>
		public static Action<string> ActionSetLastAuthError=CareCreditSettings.SetLastAuthError;

		///<summary>Puts the production dependencies back.</summary>
		public static void ResetDependencies() {
			ApiClient=new CareCreditApiClient();
			ResponseStore=new CareCreditResponseStoreDb();
			FuncGetConfig=CareCreditSettings.Load;
			FuncIsEnabled=() => Programs.IsEnabled(ProgramName.CareCredit);
			FuncNow=() => MiscData.GetNowDateTime();
			ActionSetLastAuthError=CareCreditSettings.SetLastAuthError;
			_dictSessionIdsByUrl.Clear();
		}
		#endregion Dependencies (tests replace these)

		///<summary>Session IDs from prefill answers, by page URL, for GetSessionIDFromUrl when the URL doesn't carry a known parameter.</summary>
		private static readonly ConcurrentDictionary<string,string> _dictSessionIdsByUrl=new ConcurrentDictionary<string,string>();
		///<summary>Message shown when the integration can't be used yet.</summary>
		public const string NOT_CONFIGURED_MESSAGE="CareCredit is not configured. Open Setup > Program Links > CareCredit.";

		#region Merchant numbers
		///<summary>True when clinics are on and merchant numbers are kept per provider.</summary>
		public static bool IsMerchantNumberByProv {
			get {
				if(!PrefC.HasClinicsEnabled) {
					return false;
				}
				return PIn.Bool(CareCreditSettings.GetPracticeValue(ProgramProperties.PropertyDescs.CareCredit.CareCreditIsMerchantNumberByProv));
			}
		}

		///<summary>The merchant number to use. By provider: the provider's row for the clinic, else the provider's default row. Otherwise: the clinic's
		///row, else the practice row. Throws ODException when it is missing, closed or not 16 digits; the message names the provider or clinic.</summary>
		public static string GetMerchantNumber(long clinicNum,long provNum,bool isProvOverride) {
			Meth.NoCheckMiddleTierRole();
			string merchantNum;
			string owner;
			if(isProvOverride) {
				ProviderClinic providerClinic=ProviderClinics.GetOneOrDefault(provNum,clinicNum);
				merchantNum=(providerClinic?.CareCreditMerchantId??"").Trim();
				owner=Lans.g("CareCredit","Provider")+" '"+Providers.GetAbbr(provNum)+"'";
			}
			else {
				merchantNum=GetMerchantNumberByClinic(clinicNum);
				owner=(clinicNum==0 || !PrefC.HasClinicsEnabled) ? Lans.g("CareCredit","The practice")
					: Lans.g("CareCredit","Clinic")+" '"+Clinics.GetAbbr(clinicNum)+"'";
			}
			if(merchantNum=="") {
				throw new ODException(owner+" "+Lans.g("CareCredit","does not have a CareCredit merchant number."));
			}
			if(IsMerchantNumClosed(merchantNum)) {
				throw new ODException(owner+" "+Lans.g("CareCredit","has a closed CareCredit merchant number. Enter a new one in CareCredit Setup."));
			}
			if(!IsMerchantNumValid(merchantNum,allowClosedMerchant:false)) {
				throw new ODException(owner+" "+Lans.g("CareCredit","has a CareCredit merchant number that is not a valid 16-digit number."));
			}
			return merchantNum;
		}

		///<summary>The clinic's merchant number, else the practice's (ClinicNum 0). "" if neither is set.</summary>
		public static string GetMerchantNumberByClinic(long clinicNum) {
			Meth.NoCheckMiddleTierRole();
			long programNum=CareCreditSettings.GetProgramNum();
			if(programNum==0) {
				return "";
			}
			string desc=ProgramProperties.PropertyDescs.CareCredit.CareCreditMerchantNumber;
			List<ProgramProperty> listProgramProperties=ProgramProperties.GetWhere(x => x.ProgramNum==programNum && x.PropertyDesc==desc);
			string merchantNum=(listProgramProperties.FirstOrDefault(x => x.ClinicNum==clinicNum)?.PropertyValue??"").Trim();
			if(merchantNum=="" && clinicNum!=0) {
				merchantNum=(listProgramProperties.FirstOrDefault(x => x.ClinicNum==0)?.PropertyValue??"").Trim();
			}
			return merchantNum;
		}

		///<summary>Every distinct merchant number in the program properties and providerclinic rows, closed ones included.</summary>
		public static List<string> GetMerchantNumsAll() {
			Meth.NoCheckMiddleTierRole();
			List<string> listMerchantNums=new List<string>();
			long programNum=CareCreditSettings.GetProgramNum();
			if(programNum!=0) {
				listMerchantNums.AddRange(ProgramProperties.GetWhere(x => x.ProgramNum==programNum
					&& x.PropertyDesc==ProgramProperties.PropertyDescs.CareCredit.CareCreditMerchantNumber)
					.Select(x => (x.PropertyValue??"").Trim()));
			}
			listMerchantNums.AddRange(ProviderClinics.GetCareCreditMerchantNums().Select(x => (x??"").Trim()));
			return listMerchantNums.Where(x => x!="").Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		}

		///<summary>A closed merchant number starts with 'x'.</summary>
		public static bool IsMerchantNumClosed(string merchantNum) {
			string num=(merchantNum??"").Trim();
			return num.Length>0 && (num[0]=='x' || num[0]=='X');
		}

		///<summary>True if merchantNum is closed, or listMerchantNumsAll holds its closed form ("x"+number).</summary>
		public static bool IsMerchantNumClosed(string merchantNum,List<string> listMerchantNumsAll) {
			if(IsMerchantNumClosed(merchantNum)) {
				return true;
			}
			string num=(merchantNum??"").Trim();
			if(num=="" || listMerchantNumsAll==null) {
				return false;
			}
			return listMerchantNumsAll.Any(x => string.Equals((x??"").Trim(),"x"+num,StringComparison.OrdinalIgnoreCase));
		}

		///<summary>16 digits; with allowClosedMerchant, an 'x' in front is also accepted.</summary>
		public static bool IsMerchantNumValid(string merchantNum,bool allowClosedMerchant) {
			string num=(merchantNum??"").Trim();
			if(allowClosedMerchant && IsMerchantNumClosed(num)) {
				num=num.Substring(1);
			}
			return num.Length==16 && num.All(x => x>='0' && x<='9');
		}
		#endregion Merchant numbers

		#region Hosted pages
		///<summary>Sends a prefill for the landing page and returns the hosted-page URL. Inserts a carecreditwebresponse row (Created, then Pending with
		///the reference and session IDs; CreatedError on failure). Throws ODException when the integration is disabled or not configured (no call
		///is made), CareCreditException for an HTTP error answer, and ODException when Synchrony can't be reached.</summary>
		public static string SendPrefillReturnPageUrl(CareCreditLandingPage careCreditLandingPage,Patient patient,string invoiceNumber,
			double purchaseAmt,double refundAmt,string noteMemo,string promoCode,string userName,double estimatedFeeAmt,string oldRefId,
			string merchantNum,EnumCareCreditPrefillServiceType serviceType,long payNum,long clinicNum)
		{
			Meth.NoCheckMiddleTierRole();
			CareCreditConfig config=GetUsableConfigOrThrow();
			if(!IsMerchantNumValid(merchantNum,allowClosedMerchant:false)) {
				throw new ODException(Lans.g("CareCredit","The CareCredit merchant number is closed or is not a valid 16-digit number."));
			}
			CareCreditPrefillRequest request=new CareCreditPrefillRequest();
			request.LandingPage=careCreditLandingPage;
			request.ServiceType=serviceType;
			request.MerchantNumber=merchantNum.Trim();
			request.PartnerCode=config.PartnerCode.Trim();
			if(patient!=null) {
				request.Consumer=CareCreditConsumer.FromPatient(patient,includeDateOfBirth:careCreditLandingPage==CareCreditLandingPage.AP,
					includeSsnLast4:false);
			}
			request.InvoiceNumber=invoiceNumber??"";
			request.PurchaseAmount=purchaseAmt;
			request.RefundAmount=refundAmt;
			request.EstimatedFeeAmount=estimatedFeeAmt;
			request.NoteMemo=noteMemo??"";
			request.PromoCode=promoCode??"";
			request.UserName=string.IsNullOrWhiteSpace(userName) ? GetCurrentUserName() : userName;
			request.OriginalReferenceId=oldRefId??"";
			CareCreditWebResponse careCreditWebResponse=new CareCreditWebResponse();
			careCreditWebResponse.PatNum=patient?.PatNum??0;
			careCreditWebResponse.PayNum=payNum;
			careCreditWebResponse.ClinicNum=clinicNum;
			careCreditWebResponse.MerchantNumber=request.MerchantNumber;
			careCreditWebResponse.Amount=(refundAmt>0) ? refundAmt : purchaseAmt;
			careCreditWebResponse.RefNumber="";
			careCreditWebResponse.WebToken="";
			careCreditWebResponse.ServiceType=(careCreditLandingPage==CareCreditLandingPage.QS) ? CareCreditServiceType.IndividualQS
				: CareCreditServiceType.Prefill;
			careCreditWebResponse.TransType=GetTransType(careCreditLandingPage);
			careCreditWebResponse.ProcessingStatus=CareCreditWebStatus.Created;
			careCreditWebResponse.LastResponseStr=GetLocalContextJson(careCreditLandingPage,request.UserName);
			ResponseStore.Insert(careCreditWebResponse);
			CareCreditPrefillResult result;
			try {
				result=VendorHttp.RunSync(() => ApiClient.PrefillAsync(config,request));
			}
			catch(CareCreditException ex) {
				RecordFailure(careCreditWebResponse,ex.Message,ex.ResponseJson);
				if(ex.IsAuthFailure) {
					ActionSetLastAuthError(ex.Message);
				}
				throw;
			}
			catch(HttpRequestException ex) {
				RecordFailure(careCreditWebResponse,"Network error: "+ex.Message,"");
				throw new ODException(Lans.g("CareCredit","Can't reach CareCredit. Check the internet connection and try again."),ex);
			}
			if(!config.IsHostedPageUrl(result.PageUrl)) {
				RecordFailure(careCreditWebResponse,"The page address CareCredit returned is not on the configured hosted-page host.",result.RawJson);
				throw new ODException(Lans.g("CareCredit","CareCredit returned a page address that is not on the hosted-page host in CareCredit Setup."));
			}
			CareCreditWebResponse careCreditWebResponseOld=careCreditWebResponse.Copy();
			careCreditWebResponse.RefNumber=result.ReferenceId;
			careCreditWebResponse.WebToken=result.SessionId;
			ApplyStatus(careCreditWebResponse,CareCreditWebStatus.Pending,FuncNow());
			ResponseStore.Update(careCreditWebResponse,careCreditWebResponseOld);
			//With no job computer set, this session pulls the row back itself (CareCreditJobs.PollSessionRows).
			CareCreditJobs.TrackSessionRow(careCreditWebResponse.CareCreditWebResponseNum,careCreditWebResponse.DateTimePending);
			if(result.SessionId!="") {
				_dictSessionIdsByUrl[result.PageUrl]=result.SessionId;
			}
			ActionSetLastAuthError("");
			return result.PageUrl;
		}

		private static CareCreditTransType GetTransType(CareCreditLandingPage careCreditLandingPage) {
			if(careCreditLandingPage==CareCreditLandingPage.PR) {
				return CareCreditTransType.Purchase;
			}
			if(careCreditLandingPage==CareCreditLandingPage.RF) {
				return CareCreditTransType.Refund;
			}
			return CareCreditTransType.None;
		}

		///<summary>What a new row records before Synchrony answers: the landing page and the staff user (no patient data).</summary>
		private static string GetLocalContextJson(CareCreditLandingPage careCreditLandingPage,string userName) {
			JObject jObject=new JObject();
			jObject["landingPage"]=careCreditLandingPage.ToString();
			jObject["userName"]=userName??"";
			jObject["source"]="local";
			return jObject.ToString(Formatting.None);
		}

		private static void RecordFailure(CareCreditWebResponse careCreditWebResponse,string message,string responseJson) {
			try {
				string text=string.IsNullOrEmpty(responseJson) ? message : message+" "+responseJson;
				CareCreditWebResponses.HandleResponseErrorAt(careCreditWebResponse,text,FuncNow());
				ResponseStore.Update(careCreditWebResponse);
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
		}

		///<summary>The session ID in a hosted-page URL: the configured parameter, else a parameter named sessionId, session_id or sid, else the one
		///the prefill answer gave for that URL. "" if none.</summary>
		public static string GetSessionIDFromUrl(string url) {
			string sessionParam="";
			try {
				sessionParam=FuncGetConfig().SessionParam;
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
			string sessionId=GetSessionIdFromUrl(url,sessionParam);
			if(sessionId=="" && !string.IsNullOrEmpty(url)) {
				_dictSessionIdsByUrl.TryGetValue(url,out sessionId);
			}
			return sessionId??"";
		}

		///<summary>Reads the session ID parameter from the query or fragment of url. paramName first, then sessionId, session_id, sid.</summary>
		public static string GetSessionIdFromUrl(string url,string paramName) {
			Uri uri;
			if(string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(),UriKind.Absolute,out uri)) {
				return "";
			}
			Dictionary<string,string> dictParams=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
			foreach(string part in (uri.Query.TrimStart('?')+"&"+uri.Fragment.TrimStart('#')).Split('&')) {
				int indexEquals=part.IndexOf('=');
				if(indexEquals<=0) {
					continue;
				}
				string key=Uri.UnescapeDataString(part.Substring(0,indexEquals));
				if(!dictParams.ContainsKey(key)) {
					dictParams[key]=Uri.UnescapeDataString(part.Substring(indexEquals+1).Replace('+',' '));
				}
			}
			List<string> listNames=new List<string>();
			if(!string.IsNullOrWhiteSpace(paramName)) {
				listNames.Add(paramName.Trim());
			}
			listNames.AddRange(new[] { "sessionId","session_id","sid" });
			foreach(string name in listNames) {
				string value;
				if(dictParams.TryGetValue(name,out value) && !string.IsNullOrWhiteSpace(value)) {
					return value.Trim();
				}
			}
			return "";
		}

		///<summary>True if url may be shown as a CareCredit hosted page (the configured hosted-page host, HTTPS, never an Open Dental host).</summary>
		public static bool IsHostedPageUrl(string url) {
			try {
				return FuncGetConfig().IsHostedPageUrl(url);
			}
			catch(Exception ex) {
				ex.DoNothing();
				return false;
			}
		}
		#endregion Hosted pages

		#region Pullback
		///<summary>Pulls back the outcome for the row's reference ID and updates the row in place and in the database: status, transaction type, amount
		///and the response (SSN and birth date removed). Rows without a reference ID are left alone. Doesn't create or change payments; the caller
		///(FormPayment, or the background poller through CareCreditJobs) does that. Throws like SendPrefillReturnPageUrl.</summary>
		public static void UpdateWebResponse(CareCreditWebResponse careCreditWebResponse) {
			Meth.NoCheckMiddleTierRole();
			if(careCreditWebResponse==null || string.IsNullOrWhiteSpace(careCreditWebResponse.RefNumber)) {
				return;
			}
			CareCreditConfig config=GetUsableConfigOrThrow();
			CareCreditPullbackResult result=Pullback(config,careCreditWebResponse);
			CareCreditWebResponse careCreditWebResponseOld=careCreditWebResponse.Copy();
			ApplyPullbackToRow(careCreditWebResponse,result,FuncNow());
			ResponseStore.Update(careCreditWebResponse,careCreditWebResponseOld);
		}

		///<summary>Calls pullback for the row. Records refused credentials for the setup window. Network failures become ODException. The OpenDental
		///program reaches it through CareCreditJobs.PullbackNow, which also applies the result.</summary>
		internal static CareCreditPullbackResult Pullback(CareCreditConfig config,CareCreditWebResponse careCreditWebResponse) {
			bool isQuickScreen=careCreditWebResponse.ServiceType.In(CareCreditServiceType.Batch,CareCreditServiceType.IndividualQS);
			try {
				CareCreditPullbackResult result=VendorHttp.RunSync(() => ApiClient.PullbackAsync(config,careCreditWebResponse.RefNumber,
					careCreditWebResponse.MerchantNumber,isQuickScreen));
				return result;
			}
			catch(CareCreditException ex) {
				if(ex.IsAuthFailure) {
					ActionSetLastAuthError(ex.Message);
				}
				throw;
			}
			catch(HttpRequestException ex) {
				throw new ODException(Lans.g("CareCredit","Can't reach CareCredit. Check the internet connection and try again."),ex);
			}
		}

		///<summary>Copies a pullback result onto the row without saving it. A Pending answer only refreshes the stored response. Timestamps follow the
		///status lists in CareCreditWebResponses.</summary>
		public static void ApplyPullbackToRow(CareCreditWebResponse careCreditWebResponse,CareCreditPullbackResult result,DateTime dateTimeNow) {
			if(result.ScrubbedJson!="") {
				careCreditWebResponse.LastResponseStr=result.ScrubbedJson;
			}
			if(result.TransType!=CareCreditTransType.None) {
				careCreditWebResponse.TransType=result.TransType;
			}
			if(result.Amount>0) {
				careCreditWebResponse.Amount=result.Amount;
			}
			if(result.Status==CareCreditWebStatus.Pending || result.Status==CareCreditWebStatus.Created) {
				return;//Still open in Synchrony. The status and its timestamp stay.
			}
			if(careCreditWebResponse.ServiceType==CareCreditServiceType.Batch && result.Status.In(CareCreditWebStatus.UnknownError,CareCreditWebStatus.Unknown)
				&& (result.IsInvalidInput || result.IsMerchantClosed || result.ErrorDescription!=""))
			{
				ApplyStatus(careCreditWebResponse,CareCreditWebStatus.BatchError,dateTimeNow);
				return;
			}
			ApplyStatus(careCreditWebResponse,result.Status,dateTimeNow);
		}

		///<summary>Sets the status and the timestamp that goes with it, as CareCreditWebResponses.UpdateProcessingWebStatus does, without saving.</summary>
		public static void ApplyStatus(CareCreditWebResponse careCreditWebResponse,CareCreditWebStatus careCreditWebStatus,DateTime dateTimeNow) {
			careCreditWebResponse.ProcessingStatus=careCreditWebStatus;
			if(CareCreditWebResponses.ListWebStatusAcknowledge.Contains(careCreditWebStatus)
				|| CareCreditWebResponses.ListWebStatusCompleted.Contains(careCreditWebStatus))
			{
				careCreditWebResponse.DateTimeCompleted=dateTimeNow;
			}
			else if(CareCreditWebResponses.ListWebStatusError.Contains(careCreditWebStatus)) {
				careCreditWebResponse.DateTimeLastError=dateTimeNow;
			}
			else if(CareCreditWebResponses.ListWebStatusExpired.Contains(careCreditWebStatus)) {
				careCreditWebResponse.DateTimeExpired=dateTimeNow;
			}
			else {
				careCreditWebResponse.DateTimePending=dateTimeNow;
			}
		}

		///<summary>A short payment note from the stored pullback: transaction type, reference ID, promotion and amount.</summary>
		public static string GetFormattedNote(CareCreditWebResponse careCreditWebResponse) {
			if(careCreditWebResponse==null) {
				return "";
			}
			CareCreditPullbackResult result=CareCreditJson.ReadPullback(careCreditWebResponse.LastResponseStr,isQuickScreen:false);
			CareCreditTransType transType=(careCreditWebResponse.TransType!=CareCreditTransType.None) ? careCreditWebResponse.TransType : result.TransType;
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.Append("CareCredit");
			if(transType!=CareCreditTransType.None) {
				stringBuilder.Append(" "+transType.GetDescription());
			}
			string refNumber=string.IsNullOrWhiteSpace(careCreditWebResponse.RefNumber) ? result.ReferenceId : careCreditWebResponse.RefNumber;
			if(!string.IsNullOrWhiteSpace(refNumber)) {
				stringBuilder.Append(" - Ref ID: "+refNumber.Trim());
			}
			if(result.PromoCode!="") {
				stringBuilder.Append(" - Promotion: "+result.PromoCode);
			}
			double amount=(careCreditWebResponse.Amount>0) ? careCreditWebResponse.Amount : result.Amount;
			if(amount>0) {
				stringBuilder.Append(" - Amount: "+amount.ToString("c"));
			}
			return stringBuilder.ToString();
		}

		///<summary>The merchant fee from the stored pullback, as a positive number. FormRpPaySheet shows it negated and subtracts it from the amount.</summary>
		public static double GetMerchantFee(CareCreditWebResponse careCreditWebResponse) {
			if(careCreditWebResponse==null) {
				return 0;
			}
			return Math.Abs(CareCreditJson.ReadPullback(careCreditWebResponse.LastResponseStr,isQuickScreen:false).MerchantFee);
		}

		///<summary>The staff user recorded on the original transaction, else the current user's name.</summary>
		public static string GetUserName(CareCreditWebResponse careCreditWebResponse) {
			string userName="";
			if(careCreditWebResponse!=null) {
				userName=CareCreditJson.ReadPullback(careCreditWebResponse.LastResponseStr,isQuickScreen:false).UserName;
			}
			if(userName=="") {
				userName=GetCurrentUserName();
			}
			return userName;
		}

		private static string GetCurrentUserName() {
			try {
				return Security.CurUser?.UserName??"";
			}
			catch(Exception ex) {
				ex.DoNothing();
				return "";
			}
		}

		///<summary>The batch result stored in the row, or null.</summary>
		public static CareCreditQSBatchResponse GetBatchResponse(CareCreditWebResponse careCreditWebResponse) {
			return CareCreditQSBatchResponse.From(careCreditWebResponse);
		}
		#endregion Pullback

		#region Patients and locations
		///<summary>True for a patient with a real birth date (year after 1880) under 18. Matches the minor rule in CareCreditWebResponses.</summary>
		public static bool IsPatAMinor(Patient patient) {
			if(patient==null || patient.Birthdate.Year<=1880) {
				return false;
			}
			return patient.Age<18;
		}

		///<summary>True if staff may not submit credit applications for patients at this clinic (or the practice): California locations, under the
		///state law in force since 2020-07-01. The patient applies directly with CareCredit instead; purchases still work.</summary>
		public static bool IsStaffApplicationRestricted(long clinicNum) {
			Meth.NoCheckMiddleTierRole();
			string state="";
			try {
				if(PrefC.HasClinicsEnabled && clinicNum>0) {
					state=Clinics.GetClinic(clinicNum)?.State??"";
				}
				if(string.IsNullOrWhiteSpace(state)) {
					state=PrefC.GetString(PrefName.PracticeST);
				}
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
			return IsCaliforniaState(state);
		}

		///<summary>True for "CA" or "California", in any letter case.</summary>
		public static bool IsCaliforniaState(string state) {
			string stateTrim=(state??"").Trim();
			return stateTrim.Equals("CA",StringComparison.OrdinalIgnoreCase) || stateTrim.Equals("California",StringComparison.OrdinalIgnoreCase);
		}
		#endregion Patients and locations

		#region Configuration
		///<summary>The saved settings, if the program link is enabled and they are complete. Otherwise ODException with NOT_CONFIGURED_MESSAGE, and
		///nothing is sent.</summary>
		public static CareCreditConfig GetUsableConfigOrThrow() {
			if(!FuncIsEnabled()) {
				throw new ODException(Lans.g("CareCredit",NOT_CONFIGURED_MESSAGE));
			}
			CareCreditConfig config=FuncGetConfig();
			if(config==null || !config.IsComplete) {
				throw new ODException(Lans.g("CareCredit",NOT_CONFIGURED_MESSAGE));
			}
			return config;
		}

		///<summary>True if the program link is enabled and the settings are complete. Never calls Synchrony.</summary>
		public static bool IsUsable() {
			try {
				GetUsableConfigOrThrow();
				return true;
			}
			catch(Exception ex) {
				ex.DoNothing();
				return false;
			}
		}

		///<summary>"" if the setup window may use configNew (Test connection, Save); otherwise why not. The settings that decide where the client
		///secret and patient data go (CareCreditConfig.GetDestinationChanges) are high security, like the secret itself: without
		///ManageHighSecurityProgProperties they can't differ from configSaved. Otherwise a user who may not see or edit the secret could point the
		///API host at a server of their own and have Test connection send the saved secret there.</summary>
		public static string GetConnectionChangeProblem(CareCreditConfig configSaved,CareCreditConfig configNew,bool hasHighSecurityPermission) {
			if(hasHighSecurityPermission) {
				return "";
			}
			List<string> listChanges=CareCreditConfig.GetDestinationChanges(configSaved,configNew);
			if(listChanges.Count==0) {
				return "";
			}
			return Lans.g("CareCredit","Changing where CareCredit sends the client secret and patient data needs the 'Manage High Security Program "
				+"Properties' permission. Changed")+":\r\n"+string.Join("\r\n",listChanges);
		}

		///<summary>Test connection from the setup window: refuses, without calling anyone, when the user lacks the high-security permission and a
		///destination setting differs from the saved one (GetConnectionChangeProblem); otherwise TestConnection(config).</summary>
		public static string TestConnection(CareCreditConfig config,CareCreditConfig configSaved,bool hasHighSecurityPermission) {
			Meth.NoCheckMiddleTierRole();
			string problem=GetConnectionChangeProblem(configSaved,config,hasHighSecurityPermission);
			if(problem!="") {
				return problem;
			}
			return TestConnection(config);
		}

		///<summary>Asks Synchrony for an access token with config (the values in the setup window, saved or not). Returns "" on success, else a message
		///for the user (never a secret). Refuses incomplete settings without calling Synchrony. The setup window calls the overload that also checks
		///the user may change the destination.</summary>
		public static string TestConnection(CareCreditConfig config) {
			Meth.NoCheckMiddleTierRole();
			List<string> listProblems=config.GetProblems();
			if(listProblems.Count>0) {
				return string.Join("\r\n",listProblems);
			}
			try {
				OAuthAccessToken token=VendorHttp.RunSync(() => ApiClient.RequestTokenAsync(config));
				if(token==null || string.IsNullOrEmpty(token.Value)) {
					return "CareCredit returned no access token.";
				}
				return "";
			}
			catch(TokenRejectedException ex) {
				return "CareCredit rejected the credentials. "+ex.Message;
			}
			catch(CareCreditException ex) {
				return ex.Message;
			}
			catch(VendorHttpException ex) {
				return "The request was refused or failed: "+ex.Message;
			}
			catch(HttpRequestException ex) {
				string message="Can't reach CareCredit: "+ex.GetBaseException().Message;
				if(ex.GetBaseException() is System.Security.Authentication.AuthenticationException) {
					message+=" The certificate chain may be missing; install the certificate bundle Synchrony's developer portal offers.";
				}
				return message;
			}
			catch(Exception ex) {
				return "The connection test failed: "+ex.GetBaseException().Message;
			}
		}
		#endregion Configuration
	}
}
