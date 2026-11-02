// Inventory tracking (docs/specs/inventory-tracking.md): the tracking settings of one supply. A fork table; Open Dental has no such table, and
// the settings are kept out of the supply table so that its schema stays Open Dental's.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace OpenPmsDentalBusiness {
	///<summary>The inventory tracking settings of one supply. At most one row per supply; a supply without a row has the defaults (no reorder level,
	///not tracked by lot, not an implant).</summary>
	[Serializable]
	[CrudTable]
	public class SupplySetting:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long SupplySettingNum;
		///<summary>FK to supply.SupplyNum. Unique.</summary>
		public long SupplyNum;
		///<summary>A low-stock alert is raised when the supply's on hand is at or below this level. 0 means no low-stock alert.</summary>
		public double ReorderLevel;
		///<summary>True when the supply's stock is kept per lot, with lot numbers and expiry dates.</summary>
		public bool IsLotTracked;
		///<summary>True for implant-type items: the lot used is recorded with the patient's procedure for traceability. Implies IsLotTracked.</summary>
		public bool IsImplant;

		///<summary></summary>
		public SupplySetting Copy() {
			return (SupplySetting)this.MemberwiseClone();
		}
	}
}
