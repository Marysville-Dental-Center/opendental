// Routine maintenance (docs/specs/routine-maintenance.md): one checklist step of a maintenance item. Fork-only table, not Open Dental schema.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace OpenPmsDentalBusiness {
	///<summary>One step of a maintenance item's ordered checklist. Recording a completion lists the steps to tick (maintrecordvalue keeps a copy of
	///each step's text, so the history does not change when a step is edited or deleted).</summary>
	[Serializable]
	public class MaintStep:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long MaintStepNum;
		///<summary>FK to maintitem.MaintItemNum.</summary>
		public long MaintItemNum;
		///<summary>0-based position in the checklist.</summary>
		public int ItemOrder;
		///<summary>The step, for example "Drain and refill the reservoir".</summary>
		public string Description;

		///<summary></summary>
		public MaintStep Copy() {
			return (MaintStep)this.MemberwiseClone();
		}
	}
}
