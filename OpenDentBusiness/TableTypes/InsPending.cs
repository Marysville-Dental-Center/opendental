using System;

namespace OpenDentBusiness {
	///<summary>Insurance details a patient entered on a form, held for staff to review before real insplan, inssub and patplan rows are made. Table
	///added in Open Dental 25.2. The eForms Ins Pick feature package builds the workflow; this class and its CRUD exist so the table's data
	///loads.</summary>
	[Serializable]
	public class InsPending:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long InsPendingNum;
		///<summary>FK to patient.PatNum. The patient the insurance is for.</summary>
		public long PatNum;
		///<summary>FK to patient.PatNum. The subscriber.</summary>
		public long PatNumSubscriber;
		///<summary>1, 2, 3 and so on for the ordinal the patient wants (primary, secondary, ...). Only a wish, not the plan's real position. Can be 0.</summary>
		public byte Ordinal;
		///<summary>Enum:Relat Relationship to the subscriber. Self when the patient is the subscriber.</summary>
		public Relat Relationship;
		///<summary>Group number as the patient entered it.</summary>
		public string GroupNum;
		///<summary>Group name as the patient entered it.</summary>
		public string GroupName;
		///<summary>Employer as the patient entered it.</summary>
		public string Employer;
		///<summary>Subscriber ID as the patient entered it.</summary>
		public string SubscriberID;
		///<summary>Carrier phone as the patient entered it.</summary>
		public string Phone;
		///<summary>Carrier name as the patient entered it.</summary>
		public string CarrierName;

		public InsPending() {
			GroupNum="";
			GroupName="";
			Employer="";
			SubscriberID="";
			Phone="";
			CarrierName="";
		}

		public InsPending Copy() {
			return (InsPending)MemberwiseClone();
		}
	}
}
