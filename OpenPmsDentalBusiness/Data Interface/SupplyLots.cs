// Inventory tracking (docs/specs/inventory-tracking.md): lots and the soonest-expiring-first order.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>Lots of supplies (supplylot). Not cached. QtyOnHand is set only by SupplyLedgers from the ledger.</summary>
	public class SupplyLots {
		#region Get Methods
		///<summary>The supply's lots, soonest-expiring first (SortFifo). Lots without stock are included only when isEmptyIncluded.</summary>
		public static List<SupplyLot> GetForSupply(long supplyNum,bool isEmptyIncluded) {
			return GetForSupplies(new List<long> { supplyNum },isEmptyIncluded);
		}

		///<summary>The lots of the supplies, soonest-expiring first within each supply. Lots without stock are included only when isEmptyIncluded.
		///</summary>
		public static List<SupplyLot> GetForSupplies(List<long> listSupplyNums,bool isEmptyIncluded) {
			if(listSupplyNums.IsNullOrEmpty()) {
				return new List<SupplyLot>();
			}
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<SupplyLot>>(MethodBase.GetCurrentMethod(),listSupplyNums,isEmptyIncluded);
			}
			string command="SELECT * FROM supplylot WHERE SupplyNum IN("+string.Join(",",listSupplyNums.Select(x => POut.Long(x)))+")";
			if(!isEmptyIncluded) {
				command+=" AND ROUND(QtyOnHand,2)>0";
			}
			return SortFifo(Crud.SupplyLotCrud.SelectMany(command));
		}

		///<summary>Lots by key.</summary>
		public static List<SupplyLot> GetMany(List<long> listSupplyLotNums) {
			if(listSupplyLotNums.IsNullOrEmpty()) {
				return new List<SupplyLot>();
			}
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<SupplyLot>>(MethodBase.GetCurrentMethod(),listSupplyLotNums);
			}
			return Crud.SupplyLotCrud.SelectMany("SELECT * FROM supplylot WHERE SupplyLotNum IN("
				+string.Join(",",listSupplyLotNums.Select(x => POut.Long(x)))+")");
		}

		///<summary>Lots with stock whose expiry date is on or before dateLimit (expired lots included). For the expiring alert.</summary>
		public static List<SupplyLot> GetExpiringWithStock(DateTime dateLimit) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<SupplyLot>>(MethodBase.GetCurrentMethod(),dateLimit);
			}
			string command="SELECT * FROM supplylot WHERE ROUND(QtyOnHand,2)>0 AND DateExpiry>"+POut.Date(new DateTime(1880,1,1))+" "
				+"AND DateExpiry<="+POut.Date(dateLimit);
			return Crud.SupplyLotCrud.SelectMany(command);
		}
		#endregion Get Methods

		#region Insert/Update
		///<summary>The supply's lot with this lot number (ignoring case and blanks around it), or a new lot with the expiry date. An existing lot whose
		///expiry date is blank gets the date. Throws when the lot number is blank.</summary>
		public static SupplyLot GetOrInsert(long supplyNum,string lotNumber,DateTime dateExpiry,long supplyOrderItemNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<SupplyLot>(MethodBase.GetCurrentMethod(),supplyNum,lotNumber,dateExpiry,supplyOrderItemNum);
			}
			lotNumber=(lotNumber??"").Trim();
			if(lotNumber=="") {
				throw new ApplicationException(Lans.g("SupplyLots","A lot number is required."));
			}
			string command="SELECT * FROM supplylot WHERE SupplyNum="+POut.Long(supplyNum)+" AND LotNumber='"+POut.String(lotNumber)+"' "
				+"ORDER BY SupplyLotNum LIMIT 1";
			SupplyLot supplyLot=Crud.SupplyLotCrud.SelectOne(command);
			if(supplyLot!=null) {
				if(!supplyLot.HasExpiry() && dateExpiry.Year>1880) {
					SupplyLot supplyLotOld=supplyLot.Copy();
					supplyLot.DateExpiry=dateExpiry.Date;
					Crud.SupplyLotCrud.Update(supplyLot,supplyLotOld);
				}
				return supplyLot;
			}
			supplyLot=new SupplyLot();
			supplyLot.SupplyNum=supplyNum;
			supplyLot.LotNumber=lotNumber;
			supplyLot.DateExpiry=dateExpiry.Year>1880 ? dateExpiry.Date : DateTime.MinValue;
			supplyLot.SupplyOrderItemNum=supplyOrderItemNum;
			Crud.SupplyLotCrud.Insert(supplyLot);
			return supplyLot;
		}

		///<summary>Saves a changed lot number or expiry date. Never writes QtyOnHand, which only the ledger sets.</summary>
		public static void UpdateNumberAndExpiry(SupplyLot supplyLot) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),supplyLot);
				return;
			}
			Db.NonQ("UPDATE supplylot SET LotNumber='"+POut.String(supplyLot.LotNumber)+"',DateExpiry="+POut.Date(supplyLot.DateExpiry)+" "
				+"WHERE SupplyLotNum="+POut.Long(supplyLot.SupplyLotNum));
		}

		///<summary>Deletes the lots of a supply that is being deleted.</summary>
		public static void DeleteForSupply(long supplyNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),supplyNum);
				return;
			}
			Db.NonQ("DELETE FROM supplylot WHERE SupplyNum="+POut.Long(supplyNum));
		}
		#endregion Insert/Update

		#region Soonest-expiring first
		///<summary>Soonest-expiring first: lots with an expiry date, earliest first, then lots without one; by SupplyLotNum when equal.</summary>
		public static List<SupplyLot> SortFifo(List<SupplyLot> listSupplyLots) {
			Meth.NoCheckMiddleTierRole();
			return listSupplyLots
				.OrderBy(x => x.HasExpiry() ? 0 : 1)
				.ThenBy(x => x.HasExpiry() ? x.DateExpiry : DateTime.MaxValue)
				.ThenBy(x => x.SupplyLotNum)
				.ToList();
		}

		///<summary>Splits quantity over the lots soonest-expiring first: each lot with stock gives what it has, in SortFifo order, and the rest is
		///taken from the stock without a lot (opening balances; below zero when there is not enough). Returns one line per lot used, lot 0 for stock
		///without a lot. A quantity of 0 or less gives no lines.</summary>
		public static List<SupplyUsageLine> AllocateFifo(long supplyNum,double quantity,List<SupplyLot> listSupplyLots) {
			Meth.NoCheckMiddleTierRole();
			List<SupplyUsageLine> listSupplyUsageLines=new List<SupplyUsageLine>();
			double qtyLeft=Math.Round(quantity,2);
			if(qtyLeft<=0) {
				return listSupplyUsageLines;
			}
			foreach(SupplyLot supplyLot in SortFifo(listSupplyLots.FindAll(x => x.SupplyNum==supplyNum && Math.Round(x.QtyOnHand,2)>0))) {
				if(qtyLeft<=0) {
					break;
				}
				double qtyTake=Math.Min(qtyLeft,Math.Round(supplyLot.QtyOnHand,2));
				listSupplyUsageLines.Add(new SupplyUsageLine(0,supplyNum,supplyLot.SupplyLotNum,qtyTake));
				qtyLeft=Math.Round(qtyLeft-qtyTake,2);
			}
			if(qtyLeft>0) {
				listSupplyUsageLines.Add(new SupplyUsageLine(0,supplyNum,0,qtyLeft));
			}
			return listSupplyUsageLines;
		}

		///<summary>AllocateFifo with the supply's lots as they are in the database now.</summary>
		public static List<SupplyUsageLine> AllocateFifo(long supplyNum,double quantity) {
			Meth.NoCheckMiddleTierRole();
			return AllocateFifo(supplyNum,quantity,GetForSupply(supplyNum,isEmptyIncluded:false));
		}
		#endregion Soonest-expiring first
	}
}
