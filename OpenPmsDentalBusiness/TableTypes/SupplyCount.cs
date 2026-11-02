// Inventory tracking (docs/specs/inventory-tracking.md, 2.7): one stock count, saved while it is counted. A fork table; Open Dental has no such
// table.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.ComponentModel;

namespace OpenPmsDentalBusiness {
	///<summary>One stock count (Supply Inventory, Inventory, Stock Count). Its counted lines are supplycountline rows, each saved as soon as it is
	///entered, so a closed window, a crash or a power cut loses nothing. The office has at most one count that is Open or Posting (one storage
	///location): the first counted line starts it, any user can resume it, and posting or discarding closes it. Closed counts are kept with their
	///lines as the record of what was counted. Written only through SupplyCounts and InventoryTracking, whose statements keep "at most one open
	///count" without transactions.</summary>
	[Serializable]
	[CrudTable]
	public class SupplyCount:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long SupplyCountNum;
		///<summary>Enum:SupplyCountStatus Open while it is counted, Posting while its post runs (or after a post that did not finish), then Posted or
		///Discarded.</summary>
		public SupplyCountStatus CountStatus;
		///<summary>FK to userod.UserNum. The user who started the count (counted its first line).</summary>
		public long UserNum;
		///<summary>When the count was started. Set by the database server on insert.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.DateTEntry)]
		public DateTime DateTimeEntry;
		///<summary>FK to userod.UserNum. The user who posted or discarded the count (while Posting: who started the post). 0 while it is open.</summary>
		public long UserNumClosed;
		///<summary>When the count was posted or discarded (while Posting: when the post began), server time. 0001-01-01 while it is open.</summary>
		public DateTime DateTimeClosed;
		///<summary>The reason the count was posted with, which its CountAdjust ledger rows also carry. Blank while it is open and for a discarded
		///count.</summary>
		public string Reason;

		///<summary></summary>
		public SupplyCount Copy() {
			return (SupplyCount)this.MemberwiseClone();
		}

		///<summary>True for the office's open count: Open, or Posting (a post that runs or did not finish).</summary>
		public bool IsOpenOrPosting() {
			return CountStatus==SupplyCountStatus.Open || CountStatus==SupplyCountStatus.Posting;
		}
	}

	///<summary>The state of a stock count (supplycount.CountStatus). A fork-only enum: Open Dental has no such column, so the values start at 0.
	///Stored in the database; never renumber.</summary>
	public enum SupplyCountStatus {
		///<summary>0 - Being counted: lines can be saved, removed, posted or discarded.</summary>
		[Description("Open")]
		Open,
		///<summary>1 - Its post runs: no line can be saved. A post that did not finish (a crash, a lost connection) stays here until it is
		///resumed (InventoryTracking.ResumeCount).</summary>
		[Description("Posting")]
		Posting,
		///<summary>2 - Posted: its differences are CountAdjust ledger rows.</summary>
		[Description("Posted")]
		Posted,
		///<summary>3 - Discarded without changing stock.</summary>
		[Description("Discarded")]
		Discarded,
	}
}
