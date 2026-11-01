using System;

namespace OpenPmsDentalBusiness {
	///<summary>The line detail behind a PaySuitePayment (rolled-up payments, claims, adjustments), as JSON. Table added in Open Dental 25.4. Schema and
	///data compatibility; the PaySuite integration is a separate vendor package.</summary>
	[Serializable]
	public class PaySuitePaymentDetail:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long PaySuitePaymentDetailNum;
		///<summary>The detail as a JSON string. mediumtext.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.IsText)]
		public string DetailsJson;

		public PaySuitePaymentDetail() {
			DetailsJson="";
		}

		public PaySuitePaymentDetail Copy() {
			return (PaySuitePaymentDetail)MemberwiseClone();
		}
	}
}
