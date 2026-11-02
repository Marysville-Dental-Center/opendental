// Inventory tracking (docs/specs/inventory-tracking.md): the tracking settings of supplies.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>Tracking settings of supplies (supplysetting), at most one row per supply. Not cached.</summary>
	public class SupplySettings {
		///<summary>The settings of the supply, or the defaults (SupplySettingNum 0) when it has no row.</summary>
		public static SupplySetting GetForSupply(long supplyNum) {
			Meth.NoCheckMiddleTierRole();
			return GetForSupplies(new List<long> { supplyNum }).FirstOrDefault()??CreateDefault(supplyNum);
		}

		///<summary>The rows of the supplies that have one. Supplies without a row have the defaults (CreateDefault).</summary>
		public static List<SupplySetting> GetForSupplies(List<long> listSupplyNums) {
			if(listSupplyNums.IsNullOrEmpty()) {
				return new List<SupplySetting>();
			}
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<SupplySetting>>(MethodBase.GetCurrentMethod(),listSupplyNums);
			}
			return Crud.SupplySettingCrud.SelectMany("SELECT * FROM supplysetting WHERE SupplyNum IN("
				+string.Join(",",listSupplyNums.Select(x => POut.Long(x)))+")");
		}

		///<summary>Every row.</summary>
		public static List<SupplySetting> GetAll() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<SupplySetting>>(MethodBase.GetCurrentMethod());
			}
			return Crud.SupplySettingCrud.SelectMany("SELECT * FROM supplysetting");
		}

		///<summary>The defaults of a supply without a row: no reorder level, not tracked by lot, not an implant.</summary>
		public static SupplySetting CreateDefault(long supplyNum) {
			Meth.NoCheckMiddleTierRole();
			return new SupplySetting { SupplyNum=supplyNum };
		}

		///<summary>Saves the settings of supplySetting.SupplyNum: updates its row, or inserts one when it has none. An implant is always tracked by
		///lot.</summary>
		public static void Upsert(SupplySetting supplySetting) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),supplySetting);
				return;
			}
			if(supplySetting.IsImplant) {
				supplySetting.IsLotTracked=true;
			}
			supplySetting.ReorderLevel=Math.Max(0,Math.Round(supplySetting.ReorderLevel,2));
			SupplySetting supplySettingDb=GetForSupplies(new List<long> { supplySetting.SupplyNum }).FirstOrDefault();
			if(supplySettingDb==null) {
				Crud.SupplySettingCrud.Insert(supplySetting);
				return;
			}
			supplySetting.SupplySettingNum=supplySettingDb.SupplySettingNum;
			Crud.SupplySettingCrud.Update(supplySetting,supplySettingDb);
		}

		///<summary>Deletes the row of a supply that is being deleted.</summary>
		public static void DeleteForSupply(long supplyNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),supplyNum);
				return;
			}
			Db.NonQ("DELETE FROM supplysetting WHERE SupplyNum="+POut.Long(supplyNum));
		}
	}
}
