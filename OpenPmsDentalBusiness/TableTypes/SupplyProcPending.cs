// Inventory tracking (docs/specs/inventory-tracking.md): a completed procedure whose materials nobody has confirmed yet. A fork table; Open Dental
// has no such table.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace OpenPmsDentalBusiness {
	///<summary>A procedure that became complete while inventory tracking was on, whose code has default materials, and whose materials have not been
	///confirmed. Completing a procedure never deducts stock by itself: it writes this row, and the Materials used panel (or the Awaiting Materials
	///list) deducts what the user confirms and removes the row. Un-completing or deleting the procedure removes the row too.</summary>
	[Serializable]
	[CrudTable]
	public class SupplyProcPending:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long SupplyProcPendingNum;
		///<summary>FK to procedurelog.ProcNum. Unique.</summary>
		public long ProcNum;
		///<summary>FK to patient.PatNum.</summary>
		public long PatNum;
		///<summary>FK to userod.UserNum. The user who completed the procedure, or 0 when it was completed without a user.</summary>
		public long UserNum;
		///<summary>When the procedure was completed. Set by the database server on insert.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.DateTEntry)]
		public DateTime DateTimeEntry;

		///<summary></summary>
		public SupplyProcPending Copy() {
			return (SupplyProcPending)this.MemberwiseClone();
		}
	}
}
