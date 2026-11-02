// Routine maintenance (docs/specs/routine-maintenance.md): the checklist steps of maintenance items.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OpenPmsDentalBusiness {
	///<summary>Checklist steps of maintenance items. Not cached.</summary>
	public class MaintSteps {
		///<summary>The item's steps in checklist order.</summary>
		public static List<MaintStep> GetForItem(long maintItemNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<MaintStep>>(MethodBase.GetCurrentMethod(),maintItemNum);
			}
			string command="SELECT * FROM maintstep WHERE MaintItemNum="+POut.Long(maintItemNum)+" ORDER BY ItemOrder,MaintStepNum";
			return Crud.MaintStepCrud.SelectMany(command);
		}

		///<summary>Makes the item's steps equal to listMaintSteps, in that order: ItemOrder is set from the position, steps not in the list are deleted,
		///new ones (MaintStepNum 0) inserted and changed ones updated. Recorded completions keep their own copy of each step's text.</summary>
		public static void Sync(long maintItemNum,List<MaintStep> listMaintSteps) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),maintItemNum,listMaintSteps);
				return;
			}
			List<MaintStep> listMaintStepsDb=GetForItem(maintItemNum);
			List<long> listStepNumsKept=listMaintSteps.Where(x => x.MaintStepNum!=0).Select(x => x.MaintStepNum).ToList();
			List<long> listStepNumsDeleted=listMaintStepsDb.Where(x => !listStepNumsKept.Contains(x.MaintStepNum)).Select(x => x.MaintStepNum).ToList();
			Crud.MaintStepCrud.DeleteMany(listStepNumsDeleted);
			for(int i=0;i<listMaintSteps.Count;i++) {
				MaintStep maintStep=listMaintSteps[i];
				maintStep.MaintItemNum=maintItemNum;
				maintStep.ItemOrder=i;
				MaintStep maintStepDb=listMaintStepsDb.FirstOrDefault(x => x.MaintStepNum==maintStep.MaintStepNum && maintStep.MaintStepNum!=0);
				if(maintStepDb==null) {
					Crud.MaintStepCrud.Insert(maintStep);
					continue;
				}
				Crud.MaintStepCrud.Update(maintStep,maintStepDb);
			}
		}
	}
}
