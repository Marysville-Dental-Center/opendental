using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenDentBusiness {
	///<summary>What is left of Open Dental's HQ web service (WebServiceMainHQ.asmx). The vendor integrations Open Dental brokered through HQ
	///(Google, Dropbox, CareCredit, PostcardMania, USPS, Canadian carrier updates, PaySimple, QuickBooks Online, PayConnect) now talk to the vendor
	///directly or were removed, so their members were deleted. The methods of Open Dental's own hosted services were removed earlier. Only the
	///hosted texting members remain; nothing calls them, and they go with the Phase 3 HQ removal. This build never contacts HQ:
	///WebServiceMainHQProxy.GetWebServiceMainHQInstance() returns WebServiceMainHQNoHq unless a unit test sets a mock.</summary>
	public interface IWebServiceMainHQ {
		string SmsSend(string officeData);
		string SendPushNotification(string officeData,string pushPayloadJson);
		string SetSmsPatientPhoneOptIn(string officeData);
		string ProvisionTenDlcBrand(string officeData);
	}
}
