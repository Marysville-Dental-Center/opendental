// Inventory tracking (docs/specs/inventory-tracking.md): a default material of a procedure code. A fork table; Open Dental has no such table.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace OpenPmsDentalBusiness {
	///<summary>One default material of a procedure code: the supply and the quantity a procedure with this code usually uses. The Materials used
	///panel opens filled with these when a procedure with the code is completed.</summary>
	[Serializable]
	[CrudTable]
	public class SupplyProcCode:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long SupplyProcCodeNum;
		///<summary>FK to procedurecode.CodeNum.</summary>
		public long CodeNum;
		///<summary>FK to supply.SupplyNum.</summary>
		public long SupplyNum;
		///<summary>The default quantity, in the supply's units.</summary>
		public double Quantity;
		///<summary>Zero-based order of the material within the code.</summary>
		public int ItemOrder;

		///<summary></summary>
		public SupplyProcCode Copy() {
			return (SupplyProcCode)this.MemberwiseClone();
		}
	}
}
