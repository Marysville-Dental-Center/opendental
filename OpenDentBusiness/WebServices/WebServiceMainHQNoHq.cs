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

	///<summary>The only IWebServiceMainHQ implementation used outside of unit tests. It never makes a network call. Every method throws
	///HqNotAvailableException; the vendor integrations Open Dental brokered through HQ say that a direct vendor integration is needed.</summary>
	public class WebServiceMainHQNoHq:IWebServiceMainHQ {
		private static string VendorNotAvailable(string vendorName) {
			throw HqNotAvailableException.ForVendor(vendorName);
		}

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
	}
}
