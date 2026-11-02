// Inventory tracking (docs/specs/inventory-tracking.md, 2.7): one counted line of a stock count. A fork table; Open Dental has no such table.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace OpenPmsDentalBusiness {
	///<summary>One counted line of a stock count: a supply and lot, the quantity the program had of it when it was last counted (its snapshot)
	///and the counted quantity. Saved as soon as it is counted (InventoryTracking.SaveCountLine); a supply and lot counted again replaces its line
	///(the unique key SupplyCountNum, SupplyNum, SupplyLotNum, LotNumber). Posting writes the difference QtyCounted-QtyProgram as a CountAdjust
	///ledger row (InventoryTracking.PostCount, which also takes lines that are not saved, SupplyCountLineNum 0: the starting lines of a supply,
	///InventoryTracking.GetCountLines).</summary>
	[Serializable]
	[CrudTable]
	public class SupplyCountLine:TableBase {
		///<summary>Primary key. 0 for a line that is not saved.</summary>
		[CrudColumn(IsPriKey=true)]
		public long SupplyCountLineNum;
		///<summary>FK to supplycount.SupplyCountNum.</summary>
		public long SupplyCountNum;
		///<summary>FK to supply.SupplyNum.</summary>
		public long SupplyNum;
		///<summary>FK to supplylot.SupplyLotNum. 0 for stock without a lot, or for a lot found in the count (LotNumber set), which the post creates.
		///</summary>
		public long SupplyLotNum;
		///<summary>The lot number of a lot found in the count (SupplyLotNum 0). Saved blank for an existing lot: InventoryTracking fills it in from
		///supplylot when it reads the lines, so a lot number corrected during the count never splits a lot into two lines. Blank for stock without a
		///lot.</summary>
		public string LotNumber="";
		///<summary>The expiry date of a lot found in the count. MinValue for none; saved as MinValue for an existing lot and filled in from supplylot,
		///like LotNumber.</summary>
		public DateTime DateExpiry;
		///<summary>What the program had of the supply and lot, from the ledger (InventoryTracking.GetQtyProgram), when the line was last counted.
		///</summary>
		public double QtyProgram;
		///<summary>The counted quantity.</summary>
		public double QtyCounted;
		///<summary>FK to userod.UserNum. The user who counted the line last.</summary>
		public long UserNum;
		///<summary>When the line was last counted, server time (set by the statement that saves it).</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.DateTEntry)]
		public DateTime DateTimeEntry;

		///<summary></summary>
		public SupplyCountLine Copy() {
			return (SupplyCountLine)this.MemberwiseClone();
		}

		///<summary>True when the line is a lot found in the count: no lot yet, a lot number.</summary>
		public bool IsLotFound() {
			return SupplyLotNum==0 && !string.IsNullOrWhiteSpace(LotNumber);
		}
	}
}
