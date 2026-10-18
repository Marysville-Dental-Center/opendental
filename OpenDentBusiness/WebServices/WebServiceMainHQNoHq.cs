using System;
using System.Collections.Generic;
using CodeBase;

namespace OpenDentBusiness {
	///<summary>Thrown by the "no HQ" web service implementations. This build never contacts Open Dental Software, Inc. servers, so features that
	///used to call them report themselves as not available instead.</summary>
	public class HqNotAvailableException:ODException {
		///<summary>Shown for features that only existed as an Open Dental hosted service (eServices, support, updates, telemetry, etc).</summary>
		public const string MessageDefault="This feature used Open Dental's hosted services, which are not available in this build.";

		public HqNotAvailableException() : base(MessageDefault) {
		}

		public HqNotAvailableException(string message) : base(message) {
		}

		///<summary>For vendor integrations that Open Dental used to broker through its own servers (credentials, OAuth app IDs, tokens, proxying).
		///Those integrations are kept and will talk to the vendor directly once our own vendor credentials are configured locally.</summary>
		public static HqNotAvailableException ForVendor(string vendorName) {
			return new HqNotAvailableException(GetVendorMessage(vendorName));
		}

		///<summary>Returns the user-facing message for a vendor integration whose Open Dental hosted step is not available yet.</summary>
		public static string GetVendorMessage(string vendorName) {
			return vendorName+" is not available until direct vendor integration is configured.";
		}
	}

	///<summary>The only IWebServiceMainHQ implementation used outside of unit tests. It never makes a network call. Methods that return data throw
	///HqNotAvailableException, methods that ask whether something is allowed answer "no", and fire-and-forget uploads do nothing.</summary>
	public class WebServiceMainHQNoHq:IWebServiceMainHQ {
		///<summary>Kept only to satisfy the interface. Nothing ever connects to it.</summary>
		public string Url { get; set; }="";

		private static string NotAvailable() {
			throw new HqNotAvailableException();
		}

		private static string VendorNotAvailable(string vendorName) {
			throw HqNotAvailableException.ForVendor(vendorName);
		}

		#region Questions answered locally
		///<summary>No clinic is signed up for any Open Dental hosted eService.</summary>
		public List<long> GetEServiceClinicsAllowed(List<long> listClinicNums,eServiceCode eService) {
			return new List<long>();
		}
		#endregion Questions answered locally

		#region Uploads that are dropped
		///<summary>eConnector statistics are no longer uploaded.</summary>
		public void SetEConnectorStatsAsync(string payload) {
		}

		///<summary>eConnector failover is no longer reported.</summary>
		public void ProcessEConnectorFailoverAsync(string officeData) {
		}
		#endregion Uploads that are dropped

		#region HQ-brokered vendor integrations
		public string BuildOAuthUrl(string registrationKey,string appName) {
			throw new HqNotAvailableException("The "+appName+" credentials are not available until direct vendor integration is configured.");
		}
		public string GetDropboxAccessToken(string accessCode) { return VendorNotAvailable("Dropbox"); }
		public string GetDropboxAuthorizationUrl(long registrationKeyNum) { return VendorNotAvailable("Dropbox"); }
		public string GetGoogleAccessToken(string officeData) { return VendorNotAvailable("Google"); }
		public string GetGoogleAuthorizationUrl(long registrationKeyNum) { return VendorNotAvailable("Google"); }
		public string GetGoogleAuthorizationUrlLoopbackIpAddressFlow(long registrationKeyNum) { return VendorNotAvailable("Google"); }
		public string GetCareCreditOAuthToken(string officeData) { return VendorNotAvailable("CareCredit"); }
		public string GetCareCreditWebToken(string officeData) { return VendorNotAvailable("CareCredit"); }
		public string LogCareCreditTransaction(string officeData) { return VendorNotAvailable("CareCredit"); }
		public string GetCareCreditBatchTimes(string officeData) { return VendorNotAvailable("CareCredit"); }
		public string GetAdvertisingPostcardsAccounts(string officeData) { return VendorNotAvailable("PostcardMania"); }
		public string GetPostcardManiaSSO(string officeData) { return VendorNotAvailable("PostcardMania"); }
		public string ManageAdvertisingPostcardsAccount(string officeData) { return VendorNotAvailable("PostcardMania"); }
		public string UploadPostcardManiaPatientList(string officeData) { return VendorNotAvailable("PostcardMania"); }
		public string InsertPaySimpleACHId(string officeData) { return VendorNotAvailable("PaySimple"); }
		public string GetPaySimpleWebHookUrl() { return VendorNotAvailable("PaySimple"); }
		public string ProcessUSPSAddressValidationRequest(string payload) { return VendorNotAvailable("USPS address validation"); }
		public string CanadaCarrierUpdate(string officeData) { return VendorNotAvailable("Canadian carrier updates"); }
		public string SendPushNotification(string officeData,string pushPayloadJson) { return VendorNotAvailable("Push notifications"); }
		public string SmsSend(string officeData) { return VendorNotAvailable("Texting"); }
		public string SetSmsPatientPhoneOptIn(string officeData) { return VendorNotAvailable("Texting"); }
		public string ProvisionTenDlcBrand(string officeData) { return VendorNotAvailable("Texting"); }
		#endregion HQ-brokered vendor integrations

		#region Open Dental hosted services
		public string HandleConfirmationsApptChanged(string officeData) { return NotAvailable(); }
		public string WebSchedAsapSend(string officeData) { return NotAvailable(); }
		public string TestConnection(string officeData) { return NotAvailable(); }
		public string TestConnectionDb(string officeData) { return NotAvailable(); }
		public string SetEConnectorType(string officeData,bool isListening) { return NotAvailable(); }
		public string BuildWebSchedNewPatApptURLs(string registrationKey,string clinicNums) { return NotAvailable(); }
		public string EServiceSetup(string officeData) { return NotAvailable(); }
		public string EnableAdditionalFeatures(string officeData) { return NotAvailable(); }
		public string GetFHIRAPIKeysForOffice(string officeData) { return NotAvailable(); }
		public string GenerateWebAppUrl(string officeData) { return NotAvailable(); }
		public string GenerateFHIRAPIKey(string officeData) { return NotAvailable(); }
		public string AssignFHIRAPIKey(string officeData) { return NotAvailable(); }
		public string UpdateFHIRKeyStatus(string officeData) { return NotAvailable(); }
		public string UpdateFHIRAPIKeys(string officeData) { return NotAvailable(); }
		public string ConfirmationRequestSend(string officeData) { return NotAvailable(); }
		public string WebSchedRecallNotificationsSend(string officeData) { return NotAvailable(); }
		public string GetEConnectorType(string officeData) { return NotAvailable(); }
		public string RequestListenerProxyPrefs(string officeData) { return NotAvailable(); }
		public string ValidateWebAppUrl(string officeData) { return NotAvailable(); }
		public string GetFeaturesForCustomer(string registrationKey) { return NotAvailable(); }
		public string RecordCloudSessionsInUse(string officeData) { return NotAvailable(); }
		public string RecordCloudStorageSizeUsed(string officeData) { return NotAvailable(); }
		public string CheckFHIRAPIKey(string officeData) { return NotAvailable(); }
		public string PerformRefreshCache(string officeData) { return NotAvailable(); }
		public string ValidateVersion(string officeData) { return NotAvailable(); }
		public string BuildConfirmationRequestUrl(string shortGuid) { return NotAvailable(); }
		public string BuildWebSchedUrl(string shortGuid) { return NotAvailable(); }
		public string BuildWebSchedASAPUrl(string shortGuid) { return NotAvailable(); }
		public string SubmitUnhandledException(string officeData) { return NotAvailable(); }
		public string ValidateEService(string registrationKey,string eService) { return NotAvailable(); }
		public string GenerateShortGUIDs(string officeData) { return NotAvailable(); }
		public string BuildPatientPortalStatementUrl(string shortGuid) { return NotAvailable(); }
		public string BuildConfirmationRequestUrlQuick(string shortGuid) { return NotAvailable(); }
		public string UploadRedactedDataPatientPortalInvites(string officeData) { return NotAvailable(); }
		public string ODXamValidateLogin(string loginData) { return NotAvailable(); }
		public string ODXamTwoFactorAuth(string twoFactorAuthRequestJSON) { return NotAvailable(); }
		public string EServiceLogUpload(string officeData) { return NotAvailable(); }
		public string CreateNewHelpKey(string officeData) { return NotAvailable(); }
		public string GetManualPage(string formName,string programVersion) { return NotAvailable(); }
		public string GetStableManualVersion() { return NotAvailable(); }
		public string CustomerUpdateCommitted(string officeData) { return NotAvailable(); }
		public string LicenseAgreementAccepted(string officeData) { return NotAvailable(); }
		public string SupplementalBackupHandshake(string officeData) { return NotAvailable(); }
		public string SetSupplementalBackupStatus(string officeData) { return NotAvailable(); }
		public string ThankYouSend(string officeData) { return NotAvailable(); }
		public string NewPatThankYouSend(string officeData) { return NotAvailable(); }
		public string RetrieveWebFormURL(string officeData) { return NotAvailable(); }
		public string BuildUrlRedirect(string officeData) { return NotAvailable(); }
		public string RegisterCalendarEvent(string officeData) { return NotAvailable(); }
		public string BuildApptThankYouIcs(string shortGuid) { return NotAvailable(); }
		public string EmailHostingSignup(string officeData) { return NotAvailable(); }
		public string EmailHostingChangeClinicStatus(string officeData) { return NotAvailable(); }
		public string GetCloudMaxSessions(string officeData) { return NotAvailable(); }
		public string GetThinfinityLicenseUsers(string officeData) { return NotAvailable(); }
		public string GetMaxAtoZSizeGB(string officeData) { return NotAvailable(); }
		public string UpsertCloudMaxSessions(string officeData) { return NotAvailable(); }
		public string GetMobileSettings(string officeData) { return NotAvailable(); }
		public string GetMobileSettings2FA(string officeData) { return NotAvailable(); }
		public string UpsertMobileSettings(string officeData) { return NotAvailable(); }
		public string UploadPatientPortalXWebResponses(string officeData) { return NotAvailable(); }
		public string IsBetaAvailableForThisCustomer(string registrationKey,string proposedBetaVersion) { return NotAvailable(); }
		public string GetLatestCloudClientVersion() { return NotAvailable(); }
		public string ProcessOCRIIDRequest(string officeData) { return NotAvailable(); }
		public string BuildPaymentPortalRedirectUrl(string shortGuid) { return NotAvailable(); }
		public string MsgToPaySend(string officeData) { return NotAvailable(); }
		public string GetMsgToPayShortUrl(string officeData) { return NotAvailable(); }
		#endregion Open Dental hosted services
	}
}
