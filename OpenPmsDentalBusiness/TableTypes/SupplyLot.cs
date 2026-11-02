// Inventory tracking (docs/specs/inventory-tracking.md): one lot of a supply. A fork table; Open Dental has no such table.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace OpenPmsDentalBusiness {
	///<summary>One lot of a supply that is tracked by lot (supplysetting.IsLotTracked): its lot number, expiry date and quantity on hand. Created
	///when a lot is received or counted. QtyOnHand is always the sum of the lot's supplyledger rows (SupplyLedgers); never set it directly.</summary>
	[Serializable]
	[CrudTable]
	public class SupplyLot:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long SupplyLotNum;
		///<summary>FK to supply.SupplyNum.</summary>
		public long SupplyNum;
		///<summary>The manufacturer's lot (batch) number, as printed on the package.</summary>
		public string LotNumber;
		///<summary>The expiry date printed on the package. 0001-01-01 when the lot has none.</summary>
		public DateTime DateExpiry;
		///<summary>Quantity on hand of this lot, kept equal to the sum of its ledger rows.</summary>
		public double QtyOnHand;
		///<summary>FK to supplyorderitem.SupplyOrderItemNum. The order item whose receipt created the lot, or 0.</summary>
		public long SupplyOrderItemNum;
		///<summary>When the lot was created. Set by the database server on insert.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.DateTEntry)]
		public DateTime DateTimeEntry;

		///<summary></summary>
		public SupplyLot Copy() {
			return (SupplyLot)this.MemberwiseClone();
		}

		///<summary>True when the lot has an expiry date.</summary>
		public bool HasExpiry() {
			return DateExpiry.Year>1880;
		}
	}
}
