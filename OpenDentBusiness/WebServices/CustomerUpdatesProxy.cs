using System;
using System.Collections.Generic;
using CodeBase;

namespace OpenDentBusiness {
	///<summary>The methods of Open Dental's "Customer Updates" web service (Service1.asmx) that this program used to call. Kept as an interface so
	///that call sites compile and unit tests can supply a fake. The only real implementation is CustomerUpdatesNoHq.</summary>
	public interface ICustomerUpdates {
		string RequestCustomerID(string officeData);
		string RequestEmailVerificationCode(string officeData);
		string PostEmailCertificate(string officeData);
		string RequestCertCanadaABC(string officeData);
		string RequestCertCanadaTelusAandB(string officeData);
		string RequestCertCanadaTelusAandBtest(string officeData);
		string GetErxAccess(string officeData);
		string GetClinicErxAccess(string officeData);
	}

	///<summary>Stand-in for Open Dental's "Customer Updates" web service. It never makes a network call; every method throws
	///HqNotAvailableException.</summary>
	public class CustomerUpdatesNoHq:ICustomerUpdates {
		public string RequestCustomerID(string officeData) { throw new HqNotAvailableException(); }
		public string RequestEmailVerificationCode(string officeData) { throw new HqNotAvailableException(); }
		public string PostEmailCertificate(string officeData) { throw new HqNotAvailableException(); }
		//Canadian claim network vendor certificates. Placing our own certificate file in the network's folder skips this download entirely.
		public string RequestCertCanadaABC(string officeData) { throw HqNotAvailableException.ForVendor("Downloading the Alberta Blue Cross certificate"); }
		public string RequestCertCanadaTelusAandB(string officeData) { throw HqNotAvailableException.ForVendor("Downloading the TELUS certificate"); }
		public string RequestCertCanadaTelusAandBtest(string officeData) { throw HqNotAvailableException.ForVendor("Downloading the TELUS certificate"); }
		public string GetErxAccess(string officeData) { throw HqNotAvailableException.ForVendor("eRx access"); }
		public string GetClinicErxAccess(string officeData) { throw HqNotAvailableException.ForVendor("eRx access"); }
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

	}
}
