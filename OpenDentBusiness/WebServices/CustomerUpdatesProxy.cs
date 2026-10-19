using System;
using System.Collections.Generic;
using CodeBase;

namespace OpenDentBusiness {
	///<summary>The methods of Open Dental's "Customer Updates" web service (Service1.asmx) that this program still calls. Kept as an interface so
	///that call sites compile and unit tests can supply a fake. The only real implementation is CustomerUpdatesNoHq. The Canadian network
	///certificate, eRx access and customer ID (OID root) members were deleted once nothing called them.</summary>
	public interface ICustomerUpdates {
		string RequestEmailVerificationCode(string officeData);
		string PostEmailCertificate(string officeData);
	}

	///<summary>Stand-in for Open Dental's "Customer Updates" web service. It never makes a network call; every method throws
	///HqNotAvailableException.</summary>
	public class CustomerUpdatesNoHq:ICustomerUpdates {
		public string RequestEmailVerificationCode(string officeData) { throw new HqNotAvailableException(); }
		public string PostEmailCertificate(string officeData) { throw new HqNotAvailableException(); }
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
