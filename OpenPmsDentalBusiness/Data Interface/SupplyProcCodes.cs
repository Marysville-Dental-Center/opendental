// Inventory tracking (docs/specs/inventory-tracking.md): default materials of procedure codes.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>Default materials of procedure codes (supplyproccode). Not cached: the table is small and read when a procedure is completed.</summary>
	public class SupplyProcCodes {
		///<summary>Every default material, by code and ItemOrder.</summary>
		public static List<SupplyProcCode> GetAll() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<SupplyProcCode>>(MethodBase.GetCurrentMethod());
			}
			return Crud.SupplyProcCodeCrud.SelectMany("SELECT * FROM supplyproccode ORDER BY CodeNum,ItemOrder,SupplyProcCodeNum");
		}

		///<summary>The default materials of the codes, by code and ItemOrder.</summary>
		public static List<SupplyProcCode> GetForCodes(List<long> listCodeNums) {
			if(listCodeNums.IsNullOrEmpty()) {
				return new List<SupplyProcCode>();
			}
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<SupplyProcCode>>(MethodBase.GetCurrentMethod(),listCodeNums);
			}
			return Crud.SupplyProcCodeCrud.SelectMany("SELECT * FROM supplyproccode WHERE CodeNum IN("
				+string.Join(",",listCodeNums.Select(x => POut.Long(x)))+") ORDER BY CodeNum,ItemOrder,SupplyProcCodeNum");
		}

		///<summary>True when the code has at least one default material.</summary>
		public static bool HasMaterials(long codeNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetBool(MethodBase.GetCurrentMethod(),codeNum);
			}
			return PIn.Long(Db.GetCount("SELECT COUNT(*) FROM supplyproccode WHERE CodeNum="+POut.Long(codeNum)))>0;
		}

		///<summary>Replaces the default materials of one code with the list (in its order). Rows of other codes are not touched. The new rows are
		///written before the old ones are deleted, so a failure part-way leaves extra rows, never a code without its materials.</summary>
		public static void SyncForCode(long codeNum,List<SupplyProcCode> listSupplyProcCodes) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),codeNum,listSupplyProcCodes);
				return;
			}
			List<long> listSupplyProcCodeNumsOld=GetForCodes(new List<long> { codeNum }).Select(x => x.SupplyProcCodeNum).ToList();
			for(int i=0;i<listSupplyProcCodes.Count;i++) {
				SupplyProcCode supplyProcCode=listSupplyProcCodes[i].Copy();
				supplyProcCode.CodeNum=codeNum;
				supplyProcCode.ItemOrder=i;
				supplyProcCode.Quantity=Math.Round(supplyProcCode.Quantity,2);
				Crud.SupplyProcCodeCrud.Insert(supplyProcCode);
			}
			if(listSupplyProcCodeNumsOld.Count>0) {
				Crud.SupplyProcCodeCrud.DeleteMany(listSupplyProcCodeNumsOld);
			}
		}

		///<summary>Deletes the default materials that name the supply, for a supply that is being deleted.</summary>
		public static void DeleteForSupply(long supplyNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),supplyNum);
				return;
			}
			Db.NonQ("DELETE FROM supplyproccode WHERE SupplyNum="+POut.Long(supplyNum));
		}
	}
}
