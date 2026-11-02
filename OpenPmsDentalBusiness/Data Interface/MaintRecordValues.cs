// Routine maintenance (docs/specs/routine-maintenance.md): the checklist ticks and results of recorded completions.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>Checklist ticks and results of recorded completions. Written only by MaintRecords.Insert; never edited or deleted.</summary>
	public class MaintRecordValues {
		///<summary>The completion's values: checklist lines first, then results.</summary>
		public static List<MaintRecordValue> GetForRecord(long maintRecordNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<MaintRecordValue>>(MethodBase.GetCurrentMethod(),maintRecordNum);
			}
			string command="SELECT * FROM maintrecordvalue WHERE MaintRecordNum="+POut.Long(maintRecordNum)+" ORDER BY ItemOrder,MaintRecordValueNum";
			return Crud.MaintRecordValueCrud.SelectMany(command);
		}

		///<summary>The values of several completions, in order within each.</summary>
		public static List<MaintRecordValue> GetForRecords(List<long> listMaintRecordNums) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<MaintRecordValue>>(MethodBase.GetCurrentMethod(),listMaintRecordNums);
			}
			if(listMaintRecordNums.IsNullOrEmpty()) {
				return new List<MaintRecordValue>();
			}
			string command="SELECT * FROM maintrecordvalue WHERE MaintRecordNum IN("+string.Join(",",listMaintRecordNums.Select(x => POut.Long(x)))+") "
				+"ORDER BY MaintRecordNum,ItemOrder,MaintRecordValueNum";
			return Crud.MaintRecordValueCrud.SelectMany(command);
		}
	}
}
