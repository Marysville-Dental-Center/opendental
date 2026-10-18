using System;
using System.Collections.Generic;
using CodeBase;

namespace OpenDentBusiness {
	///<summary>The methods of Open Dental's "Customer Updates" web service (Service1.asmx) that this program used to call. Kept as an interface so
	///that call sites compile and unit tests can supply a fake. The only real implementation is CustomerUpdatesNoHq.</summary>
	public interface ICustomerUpdates {
		string RequestUpdate(string officeData);
		string RequestCustomerID(string officeData);
		void PracticeTitleReset(string officeData);
		string RequestRegKeyStatus(string officeData);
		string RequestEmailVerificationCode(string officeData);
		string PostEmailCertificate(string officeData);
		string RequestCertCanadaABC(string officeData);
		string RequestCertCanadaTelusAandB(string officeData);
		string RequestCertCanadaTelusAandBtest(string officeData);
		string RequestCodeSystems(string officeData);
		string RequestCodeSystemDownload(string officeData);
		string FeatureRequestGetList(string officeData);
		string FeatureRequestGetOne(string officeData);
		string FeatureRequestSubmitChanges(string officeData);
		string FeatureRequestDiscussSubmit(string officeData);
		string FeatureRequestDiscussGetList(string officeData);
		string RequestFeeSched(string officeData);
		string GetErxAccess(string officeData);
		string GetClinicErxAccess(string officeData);
		string RequestIsDevKey(string regKeyString);
		string DatabaseIntegrityGetList();
		string DatabaseIntegrityGetList2(string registrationKey);
	}

	///<summary>Stand-in for Open Dental's "Customer Updates" web service. It never makes a network call; every method throws
	///HqNotAvailableException, except the database integrity list, which is simply empty.</summary>
	public class CustomerUpdatesNoHq:ICustomerUpdates {
		public string RequestUpdate(string officeData) { throw new HqNotAvailableException(); }
		public string RequestCustomerID(string officeData) { throw new HqNotAvailableException(); }
		public void PracticeTitleReset(string officeData) { throw new HqNotAvailableException(); }
		public string RequestRegKeyStatus(string officeData) { throw new HqNotAvailableException(); }
		public string RequestEmailVerificationCode(string officeData) { throw new HqNotAvailableException(); }
		public string PostEmailCertificate(string officeData) { throw new HqNotAvailableException(); }
		//Canadian claim network vendor certificates. Placing our own certificate file in the network's folder skips this download entirely.
		public string RequestCertCanadaABC(string officeData) { throw HqNotAvailableException.ForVendor("Downloading the Alberta Blue Cross certificate"); }
		public string RequestCertCanadaTelusAandB(string officeData) { throw HqNotAvailableException.ForVendor("Downloading the TELUS certificate"); }
		public string RequestCertCanadaTelusAandBtest(string officeData) { throw HqNotAvailableException.ForVendor("Downloading the TELUS certificate"); }
		public string RequestCodeSystems(string officeData) { throw new HqNotAvailableException(); }
		public string RequestCodeSystemDownload(string officeData) { throw new HqNotAvailableException(); }
		public string FeatureRequestGetList(string officeData) { throw new HqNotAvailableException(); }
		public string FeatureRequestGetOne(string officeData) { throw new HqNotAvailableException(); }
		public string FeatureRequestSubmitChanges(string officeData) { throw new HqNotAvailableException(); }
		public string FeatureRequestDiscussSubmit(string officeData) { throw new HqNotAvailableException(); }
		public string FeatureRequestDiscussGetList(string officeData) { throw new HqNotAvailableException(); }
		public string RequestFeeSched(string officeData) { throw new HqNotAvailableException(); }
		public string GetErxAccess(string officeData) { throw HqNotAvailableException.ForVendor("eRx access"); }
		public string GetClinicErxAccess(string officeData) { throw HqNotAvailableException.ForVendor("eRx access"); }
		public string RequestIsDevKey(string regKeyString) { throw new HqNotAvailableException(); }
		public string DatabaseIntegrityGetList() { throw new HqNotAvailableException(); }
		public string DatabaseIntegrityGetList2(string registrationKey) { throw new HqNotAvailableException(); }
	}

	public class CustomerUpdatesProxy {
		///<summary>Unit tests may set this to a fake implementation. When null, GetWebServiceInstance() returns CustomerUpdatesNoHq.</summary>
		public static ICustomerUpdates MockCustomerUpdates {
			private get;//Use GetWebServiceInstance()
			set;
		}

		///<summary>Returns the "no HQ" stand-in for Open Dental's 'Customer Updates Web Service'. It never makes a network call.</summary>
		public static ICustomerUpdates GetWebServiceInstance() {
			if(MockCustomerUpdates!=null) {
				return MockCustomerUpdates;
			}
			return new CustomerUpdatesNoHq();
		}

		///<summary>Used to send the registration key, practice title, phone, version, clinic count and list of enabled programs to HQ and get back
		///the list of available updates. None of that is sent anymore. Always throws HqNotAvailableException.</summary>
		public static string SendAndReceiveUpdateRequestXml() {
			throw new HqNotAvailableException();
		}
	}
}
