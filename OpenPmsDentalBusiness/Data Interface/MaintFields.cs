// Routine maintenance (docs/specs/routine-maintenance.md): the result fields of maintenance items.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OpenPmsDentalBusiness {
	///<summary>Result fields of maintenance items. Not cached.</summary>
	public class MaintFields {
		///<summary>The item's fields in order.</summary>
		public static List<MaintField> GetForItem(long maintItemNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<MaintField>>(MethodBase.GetCurrentMethod(),maintItemNum);
			}
			string command="SELECT * FROM maintfield WHERE MaintItemNum="+POut.Long(maintItemNum)+" ORDER BY ItemOrder,MaintFieldNum";
			return Crud.MaintFieldCrud.SelectMany(command);
		}

		///<summary>Makes the item's fields equal to listMaintFields, in that order: ItemOrder is set from the position, fields not in the list are
		///deleted, new ones (MaintFieldNum 0) inserted and changed ones updated. Recorded values keep their own copy of each field's name and unit.
		///</summary>
		public static void Sync(long maintItemNum,List<MaintField> listMaintFields) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),maintItemNum,listMaintFields);
				return;
			}
			List<MaintField> listMaintFieldsDb=GetForItem(maintItemNum);
			List<long> listFieldNumsKept=listMaintFields.Where(x => x.MaintFieldNum!=0).Select(x => x.MaintFieldNum).ToList();
			List<long> listFieldNumsDeleted=listMaintFieldsDb.Where(x => !listFieldNumsKept.Contains(x.MaintFieldNum)).Select(x => x.MaintFieldNum).ToList();
			Crud.MaintFieldCrud.DeleteMany(listFieldNumsDeleted);
			for(int i=0;i<listMaintFields.Count;i++) {
				MaintField maintField=listMaintFields[i];
				maintField.MaintItemNum=maintItemNum;
				maintField.ItemOrder=i;
				MaintField maintFieldDb=listMaintFieldsDb.FirstOrDefault(x => x.MaintFieldNum==maintField.MaintFieldNum && maintField.MaintFieldNum!=0);
				if(maintFieldDb==null) {
					Crud.MaintFieldCrud.Insert(maintField);
					continue;
				}
				Crud.MaintFieldCrud.Update(maintField,maintFieldDb);
			}
		}
	}
}
