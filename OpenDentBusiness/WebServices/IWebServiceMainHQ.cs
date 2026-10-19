using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenDentBusiness {
	///<summary>The methods of Open Dental's HQ web service (WebServiceMainHQ.asmx) that the vendor integrations Open Dental brokered through HQ still
	///call. They get direct vendor integrations later. The methods of Open Dental's own hosted services were removed. This build never contacts
	///HQ: WebServiceMainHQProxy.GetWebServiceMainHQInstance() returns WebServiceMainHQNoHq unless a unit test sets a mock.</summary>
	public interface IWebServiceMainHQ {
		string SmsSend(string officeData);
		string BuildOAuthUrl(string registrationKey,string appName);
		string GetDropboxAccessToken(string accessCode);
		string GetDropboxAuthorizationUrl(long registrationKeyNum);
		string CanadaCarrierUpdate(string officeData);
		string SendPushNotification(string officeData,string pushPayloadJson);
		string InsertPaySimpleACHId(string officeData);
		string GetPaySimpleWebHookUrl();
		string SetSmsPatientPhoneOptIn(string officeData);
		string GetGoogleAccessToken(string officeData);
		string GetGoogleAuthorizationUrl(long registrationKeyNum);
		string GetGoogleAuthorizationUrlLoopbackIpAddressFlow(long registrationKeyNum);
		string GetCareCreditOAuthToken(string officeData);
		string LogCareCreditTransaction(string officeData);
		string GetAdvertisingPostcardsAccounts(string officeData);
		string GetPostcardManiaSSO(string officeData);
		string ManageAdvertisingPostcardsAccount(string officeData);
		string UploadPostcardManiaPatientList(string officeData);
		string GetCareCreditWebToken(string officeData);
		string ProvisionTenDlcBrand(string officeData);
		string ProcessUSPSAddressValidationRequest(string payload);
		string GetCareCreditBatchTimes(string officeData);
	}
}
