// Inventory tracking (docs/specs/inventory-tracking.md): one movement of stock. A fork table; Open Dental has no such table.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.ComponentModel;

namespace OpenPmsDentalBusiness {
	///<summary>One movement of stock of one supply, and lot when the supply is tracked by lot: a signed quantity with its type, user, time, the
	///procedure, order item or dispensary row it belongs to, and a reason. Every change of stock is a row here, and supply.LevelOnHand and
	///supplylot.QtyOnHand are always the sums of these rows (SupplyLedgers). Rows are never edited; a correction is a new row.</summary>
	[Serializable]
	[CrudTable(IsDeleteForbidden=true)]
	public class SupplyLedger:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long SupplyLedgerNum;
		///<summary>FK to supply.SupplyNum.</summary>
		public long SupplyNum;
		///<summary>FK to supplylot.SupplyLotNum. 0 for stock without a lot.</summary>
		public long SupplyLotNum;
		///<summary>Enum:SupplyLedgerType What kind of movement this is.</summary>
		public SupplyLedgerType LedgerType;
		///<summary>Signed quantity: positive adds to stock (receipts, restores, positive adjustments), negative takes from it (uses, dispenses,
		///negative adjustments).</summary>
		public double Quantity;
		///<summary>The cost of one unit at the time of the movement: the order item's price for a receipt, otherwise the supply's price.</summary>
		public double UnitCost;
		///<summary>FK to procedurelog.ProcNum. The procedure for Use and Restore rows, otherwise 0.</summary>
		public long ProcNum;
		///<summary>FK to patient.PatNum. The patient of the procedure for Use and Restore rows, otherwise 0.</summary>
		public long PatNum;
		///<summary>FK to supplyorderitem.SupplyOrderItemNum. The order item for Receive rows, otherwise 0.</summary>
		public long SupplyOrderItemNum;
		///<summary>FK to dispsupply.DispSupplyNum. The dispensary row for Dispense rows, otherwise 0.</summary>
		public long DispSupplyNum;
		///<summary>FK to userod.UserNum. The user who made the movement. 0 for rows the migration wrote.</summary>
		public long UserNum;
		///<summary>When the movement was recorded. Set by the database server on insert.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.DateTEntry)]
		public DateTime DateTimeEntry;
		///<summary>Why. Required for count adjustments and manual adjustments.</summary>
		public string Reason;
		///<summary>FK to supplycountline.SupplyCountLineNum. For a CountAdjust row, the saved stock count line it was posted from
		///(InventoryTracking.PostCount), written in the row's own INSERT: a post that failed or did not finish is opened again knowing which lines it
		///adjusted (InventoryTracking.ReopenAfterFailedPost). 0 for every other row, and for a count posted from lines that are not saved.</summary>
		public long SupplyCountLineNum;

		///<summary></summary>
		public SupplyLedger Copy() {
			return (SupplyLedger)this.MemberwiseClone();
		}
	}

	///<summary>The kind of a stock movement (supplyledger.LedgerType). A fork-only enum: Open Dental has no such column, so the values start at 0.
	///Stored in the database; never renumber.</summary>
	public enum SupplyLedgerType {
		///<summary>0 - The stock a supply had when inventory tracking was installed, or that was typed for a new supply.</summary>
		[Description("Opening")]
		Opening,
		///<summary>1 - Received from a supply order.</summary>
		[Description("Receive")]
		Receive,
		///<summary>2 - Used on a completed procedure (negative).</summary>
		[Description("Use")]
		Use,
		///<summary>3 - Put back from a procedure that was un-completed, deleted or whose materials were corrected (positive).</summary>
		[Description("Restore")]
		Restore,
		///<summary>4 - The difference a stock count found.</summary>
		[Description("Count")]
		CountAdjust,
		///<summary>5 - A manual adjustment with a reason.</summary>
		[Description("Adjust")]
		Adjust,
		///<summary>6 - Given out at the dental school dispensary (negative).</summary>
		[Description("Dispense")]
		Dispense,
	}
}
