using System;

namespace OpenPmsDentalBusiness {
	///<summary>A Glidewell laboratory billing account that lab cases can be sent to electronically. Table added in Open Dental 26.2
	///(OpenDentalDiffDocumentation26-2.xml); names, order and types as published. This class gives the schema and the stored data a home; sending
	///lab cases to Glidewell is a separate feature package, off until the office enters its own account.</summary>
	[Serializable]
	public class GlidewellAcct:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long GlidewellAcctNum;
		///<summary>The account number Glidewell uses to track the billing account, for example 10-2913770.</summary>
		public string AccountNumber;
		///<summary>The API key from Glidewell, stored encrypted.</summary>
		public string ApiKey;
		///<summary>Date and time of the last successful log-in to a scanner software for authentication.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.DateT)]
		public DateTime DateTimeExpires;

		public GlidewellAcct() {
			AccountNumber="";
			ApiKey="";
		}

		///<summary>Returns a copy of this GlidewellAcct.</summary>
		public GlidewellAcct Copy() {
			return (GlidewellAcct)MemberwiseClone();
		}
	}
}
