using System;

namespace OpenPmsDentalBusiness {
	///<summary>One lab case sent to Glidewell: the request, Glidewell's most recent response and the order's status there. Table added in Open Dental
	///26.2 (OpenDentalDiffDocumentation26-2.xml); names, order and types as published. This class gives the schema and the stored data a home;
	///sending lab cases to Glidewell is a separate feature package.</summary>
	[Serializable]
	public class GlidewellMsg:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long GlidewellMsgNum;
		///<summary>FK to labcase.LabCaseNum.</summary>
		public long LabCaseNum;
		///<summary>Enum:EnumGlidewellOrderStatus The most recent status of the order in Glidewell's system, for example Initialized or InProgress.</summary>
		public EnumGlidewellOrderStatus OrderStatus;
		///<summary>The vendor order ID Glidewell returns when the case is registered. Open Dental's documents describe it as used only when testing
		///against a simulated scanner; in the real workflow Glidewell sends it to the office's scanner.</summary>
		public string VendorRxId;
		///<summary>FK to patient.PatNum.</summary>
		public long PatNum;
		///<summary>FK to glidewellacct.GlidewellAcctNum. The account the case was sent to; provider.GlidewellAcctNum is only the default.</summary>
		public long GlidewellAcctNum;
		///<summary>JSON: the most recent response body received from Glidewell.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.IsText)]
		public string RawJsonResponse;
		///<summary>JSON: the request sent to Glidewell.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.IsText)]
		public string RawJsonRequest;
		///<summary>True when the case was sent expecting a scan from an iTero scanner.</summary>
		public bool SentITero;
		///<summary>True when the case was sent expecting a scan from a Medit scanner.</summary>
		public bool SentMedit;

		public GlidewellMsg() {
			VendorRxId="";
			RawJsonResponse="";
			RawJsonRequest="";
		}

		///<summary>Returns a copy of this GlidewellMsg.</summary>
		public GlidewellMsg Copy() {
			return (GlidewellMsg)MemberwiseClone();
		}
	}

	///<summary>Stored in glidewellmsg.OrderStatus. Names and order as published in Open Dental's 26.2 database documentation, which prints them
	///without numbers; the numbers are their order from 0 (INFERRED, docs/port-notes/P262-SCHEMA.md).</summary>
	public enum EnumGlidewellOrderStatus {
		///<summary>0 - The lab case has been registered and is awaiting scan submission.</summary>
		Initialized=0,
		///<summary>1 - Glidewell received the lab case; it waits to be processed.</summary>
		Received=1,
		///<summary>2 - The lab is processing the lab case.</summary>
		InProgress=2,
		///<summary>3 - The lab case is paused, awaiting more information.</summary>
		OnHold=3,
		///<summary>4 - The products of the lab case were shipped; carrier and tracking information are available.</summary>
		Shipped=4,
		///<summary>5 - The lab case was cancelled and will not be processed further.</summary>
		Cancelled=5,
		///<summary>6 - The lab case was returned to the lab and waits for a payment credit.</summary>
		CreditPending=6,
		///<summary>7 - A credit was issued for the returned lab case.</summary>
		Credited=7,
	}
}
