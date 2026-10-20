using System;

namespace OpenDentBusiness {
	/// <summary>Per Sam - This class is usually used for web service calls dealing with billing, account management, and synch.
	/// This build never contacts HQ. The vendor helpers that used to live here (advertising postcard accounts, CareCredit batch times and merchant
	/// numbers, USPS address validation) were deleted once each integration talked to its vendor directly or was removed.</summary>
	public static class WebServiceMainHQProxy {

		///<summary>Unit tests may set this to a fake implementation. When null, GetWebServiceMainHQInstance() returns WebServiceMainHQNoHq.</summary>
		public static IWebServiceMainHQ MockWebServiceMainHQ {
			private get;//Use GetWebServiceMainHQInstance()
			set;
		}

		///<summary>Always false outside of unit tests. This build never connects to Open Dental's HQ web service, so callers can use this to skip
		///work that only made sense with HQ (building payloads, starting threads, showing signup UI).</summary>
		public static bool IsHqAvailable {
			get {
				return MockWebServiceMainHQ!=null;
			}
		}

		///<summary>Returns the "no HQ" implementation of the WebServiceMainHQ web service. Its methods never make a network call; they throw
		///HqNotAvailableException instead. The URL parameter is ignored and only kept so existing callers compile.</summary>
		public static IWebServiceMainHQ GetWebServiceMainHQInstance(string webServiceHqUrl="") {
			if(MockWebServiceMainHQ!=null) {
				return MockWebServiceMainHQ;
			}
			return new WebServiceMainHQNoHq();
		}
	}

}
