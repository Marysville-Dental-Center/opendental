using System;

namespace OpenDentBusiness {
	///<summary>One insurer payment (cheque or direct deposit) received through PaySuite, a Canadian claim-payment service. Table added in Open Dental
	///25.4. This class gives the schema and the stored data a home; the PaySuite integration itself is a separate vendor package that talks to
	///PaySuite directly and is off by default.</summary>
	[Serializable]
	public class PaySuitePayment:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long PaySuitePaymentNum;
		///<summary>PaySuite's identifier for the payment.</summary>
		public string PaymentId;
		///<summary>The provider number the payment was issued to (provider.NationalProvID).</summary>
		public string ProviderId;
		///<summary>"C" for a cheque, "D" for a direct deposit.</summary>
		public string PaymentMethod;
		///<summary>The cheque number, or the direct deposit reference.</summary>
		public string PaymentReference;
		///<summary>The total amount of the payment.</summary>
		public double PaymentAmount;
		///<summary>The date of the payment (no time).</summary>
		public DateTime PaymentDate;
		///<summary>PaySuite's status code for the payment, for example issued, paid, reversed or under threshold.</summary>
		public string PaymentStatus;
		///<summary>Why a reversed payment was reversed. Empty unless the payment was reversed.</summary>
		public string ReversalReasonCode;
		///<summary>For a reversed or under-threshold payment, the PaymentId of the later payment it was rolled into. Empty otherwise.</summary>
		public string AssociatedPaymentId;
		///<summary>FK to paysuitepaymentdetail.PaySuitePaymentDetailNum.</summary>
		public long PaySuitePaymentDetailNum;
		///<summary>True for a paid payment whose linked claim payment or claimprocs do not add up to it.</summary>
		public bool HasUnresolvedClaimPayment;
		///<summary>Enum:EnumReconciliationStatus DoNotProcess (0) by default. The reconciliation stage of this payment.</summary>
		public EnumReconciliationStatus ReconciliationStatus;
		///<summary>FK to claimpayment.ClaimPaymentNum. The claim payment this payment was posted to.</summary>
		public long ClaimPaymentNum;

		public PaySuitePayment() {
			PaymentId="";
			ProviderId="";
			PaymentMethod="";
			PaymentReference="";
			PaymentStatus="";
			ReversalReasonCode="";
			AssociatedPaymentId="";
		}

		public PaySuitePayment Copy() {
			return (PaySuitePayment)MemberwiseClone();
		}
	}

	///<summary>Stored in paysuitepayment.ReconciliationStatus. Numbers as published in the Open Dental 25.4 database documentation.</summary>
	public enum EnumReconciliationStatus {
		///<summary>0 - Not processed.</summary>
		DoNotProcess=0,
		///<summary>1 - Waiting to be processed.</summary>
		ReadyToProcess=1,
		///<summary>2 - More than one claim matched.</summary>
		MultiClaimMatches=2,
		///<summary>3 - Processed, with discrepancies.</summary>
		DiscrepanciesFound=3,
		///<summary>4 - Processed, no discrepancies.</summary>
		NoDiscrepanciesFound=4,
	}
}
