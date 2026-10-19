using Bridges;
using CodeBase;
using Newtonsoft.Json;
using ODCrypt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using WebServiceSerializer;

namespace OpenDentBusiness {
	/// <summary>Per Sam - This class is usually used for web service calls dealing with billing, account management, and synch.</summary>
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
		///HqNotAvailableException (or answer "not allowed") instead. The URL parameter is ignored and only kept so existing callers compile.</summary>
		public static IWebServiceMainHQ GetWebServiceMainHQInstance(string webServiceHqUrl="") {
			if(MockWebServiceMainHQ!=null) {
				return MockWebServiceMainHQ;
			}
			return new WebServiceMainHQNoHq();
		}

		///<summary>Returns a serialized list of ClinicProgramPropertyContainer objects to pass from the office to HQ. Public for unit testing.
		///HQ used to receive these CareCredit merchant numbers with the eServices setup, which was removed. Kept for the direct CareCredit integration.</summary>
		public static string GetSerializedProgramProperties(List<Clinic> listClinics) {
			List<Provider> listProviders=new List<Provider>();
			List<ClinicProgramPropertyContainer> listToSerialize=new List<ClinicProgramPropertyContainer>();
			if(Programs.IsEnabled(ProgramName.CareCredit)) {
				//small helper method to check for blanks, remove any unwanted chars, and make sure we're not adding duplicates
				bool tryAdd(string merchID,List<string> curMerchIds) {
					if(!string.IsNullOrWhiteSpace(merchID) && !curMerchIds.Contains(merchID)) {
						//This means that the merchant number is technically inactive but that doesn't mean that web responses might not come in for older merch numbers
						//Offices can also remove the x themselves at any point so we might as well update our records with it
						if(merchID.StartsWith("x")) {
							merchID=merchID.Substring(1);
						}
						curMerchIds.Add(merchID);
						return true;
					}
					return false;
				}
				List<ProviderClinic> listMerchantIdProviderClinics=ProviderClinics.GetCareCreditRows();
				//Loop through this list and add any clinic and provider merch numbers. Whether or not they're in use doesn't feel that important but we should strip any leading x's so that we only have clean data
				for(int i=0;i<listClinics.Count();i++) {
					List<string> clinicMerchIds=new List<string>();
					Clinic clinic = listClinics[i];
					string merchIdForClinic=CareCredit.GetMerchantNumberByClinic(clinic.ClinicNum);
					string merchIdForProvider=listMerchantIdProviderClinics.FirstOrDefault(x=>x.ClinicNum==clinic.ClinicNum)?.CareCreditMerchantId??"";
					if(!tryAdd(merchIdForClinic,clinicMerchIds) && !tryAdd(merchIdForProvider,clinicMerchIds)) {
						continue;
					}
					listToSerialize.Add(new ClinicProgramPropertyContainer() {
						ClinicNum=clinic.ClinicNum,
						ProgramName=ProgramName.CareCredit.ToString(),
						ProgramProperty=CareCreditWebResponses.MERCHANT_ID_PROG_PROPERTY,
						ProgramValue=JsonConvert.SerializeObject(clinicMerchIds)
					});
				}
			}
			return JsonConvert.SerializeObject(listToSerialize);
		}

		///<summary>A CareCredit merchant number of a clinic, as sent to HQ by GetSerializedProgramProperties().</summary>
		public class ClinicProgramPropertyContainer {
			public long ClinicNum;
			public string ProgramName;
			public string ProgramProperty;
			public string ProgramValue;
		}

		#region Advertising - Postcards
		public static PostcardManiaMetaData GetAdvertisingPostcardsAccountData() {
			List<PayloadItem> payloadItems=new List<PayloadItem>();
			string officeData=PayloadHelper.CreatePayload(payloadItems,eServiceCode.Undefined);
			string result=GetWebServiceMainHQInstance().GetAdvertisingPostcardsAccounts(officeData);
			return WebSerializer.DeserializeTag<PostcardManiaMetaData>(result,AdvertisingPostcards.MassPostcardResponseNode);
		}

		public static PostcardManiaSSOResponse GetAdvertisingPostcardsSSO(PostcardManiaSSORequest req) {
			List<PayloadItem> payloadItems=new List<PayloadItem>();
			payloadItems.Add(new PayloadItem(req,AdvertisingPostcards.MassPostcardRequestNode));
			string officeData=PayloadHelper.CreatePayload(payloadItems,eServiceCode.Undefined);
			string result=GetWebServiceMainHQInstance().GetPostcardManiaSSO(officeData);
			return WebSerializer.DeserializeTag<PostcardManiaSSOResponse>(result,AdvertisingPostcards.MassPostcardResponseNode);
		}

		public static PostcardManiaAccountCreateResponse ManageAdvertisingPostcardsAccount(PostcardManiaAccountCreateRequest req) {
			List<PayloadItem> payloadItems=new List<PayloadItem>();
			payloadItems.Add(new PayloadItem(req,AdvertisingPostcards.MassPostcardRequestNode));
			string officeData=PayloadHelper.CreatePayload(payloadItems,eServiceCode.Undefined);
			string result=GetWebServiceMainHQInstance().ManageAdvertisingPostcardsAccount(officeData);
			return WebSerializer.DeserializeTag<PostcardManiaAccountCreateResponse>(result,AdvertisingPostcards.MassPostcardResponseNode);
		}

		public static PostcardManiaUploadPatientsResponse UploadAdvertisingPostcardsPatientList(PostcardManiaUploadPatientsRequest req) {
			List<PayloadItem> payloadItems=new List<PayloadItem>();
			payloadItems.Add(new PayloadItem(req,AdvertisingPostcards.MassPostcardRequestNode));
			string officeData=PayloadHelper.CreatePayload(payloadItems,eServiceCode.Undefined);
			string result=GetWebServiceMainHQInstance().UploadPostcardManiaPatientList(officeData);
			return WebSerializer.DeserializeTag<PostcardManiaUploadPatientsResponse>(result,AdvertisingPostcards.MassPostcardResponseNode);
		}
		#endregion
		///<summary>Returns a DateTime set by HQ or minval if we failed to retrieve the value. Without HQ this is always MinValue.</summary>
		public static DateTime GetCareCreditBatchProcessTime() {
			if(!IsHqAvailable) {
				return DateTime.MinValue;
			}
			string payload=PayloadHelper.CreatePayload("",eServiceCode.Undefined);//Undefined to only check if they were ever a customer. Doesn't require support
			try {
				return WebSerializer.DeserializeTag<DateTime>(GetWebServiceMainHQInstance().GetCareCreditBatchTimes(payload),"DateTimeBatchProcess");
			}
			catch {
				return DateTime.MinValue;
			}
		}

		///<summary>Returns a DateTime set by HQ or minval if we failed to retrieve the value. Without HQ this is always MinValue.</summary>
		public static DateTime GetCareCreditBatchPullbackTime() {
			if(!IsHqAvailable) {
				return DateTime.MinValue;
			}
			string payload=PayloadHelper.CreatePayload("",eServiceCode.Undefined);//Undefined to only check if they were ever a customer. Doesn't require support
			try {
				return WebSerializer.DeserializeTag<DateTime>(GetWebServiceMainHQInstance().GetCareCreditBatchTimes(payload),"DateTimeBatchPullback");
			}
			catch {
				return DateTime.MinValue;
			}
		}

		///<summary>Used to send the address to Open Dental HQ, which forwarded it to USPS. HQ is not available, so this throws
		///HqNotAvailableException until USPS address validation talks to USPS directly with our own credentials.</summary>
		public static string ProcessUSPSAddressValidationRequest(Address address) {
			if(!IsHqAvailable) {
				throw HqNotAvailableException.ForVendor("USPS address validation");
			}
			string payload=PayloadHelper.CreatePayload(
				new List<PayloadItem> {
					new PayloadItem(address,"address"),
				},
				eServiceCode.SoftwareUpdate
			);
			return GetWebServiceMainHQInstance().ProcessUSPSAddressValidationRequest(payload);
		}
	}

}
