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
	///HqNotAvailableException.</summary>
	public class WebServiceMainHQNoHq:IWebServiceMainHQ {
		private static string NotAvailable(string featureName) {
			throw HqNotAvailableException.ForVendor(featureName);
		}

		#region Hosted texting
		public string SendPushNotification(string officeData,string pushPayloadJson) { return NotAvailable("Push notifications"); }
		public string SmsSend(string officeData) { return NotAvailable("Texting"); }
		public string SetSmsPatientPhoneOptIn(string officeData) { return NotAvailable("Texting"); }
		public string ProvisionTenDlcBrand(string officeData) { return NotAvailable("Texting"); }
		#endregion Hosted texting
	}
}
