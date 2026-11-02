// Inventory tracking (docs/specs/inventory-tracking.md): the stock ledger. Every change of stock goes through InsertMany, which writes the ledger
// rows first and then recomputes the counters from the ledger.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>The stock ledger (supplyledger). Not cached: stock is always read from the database, so no workstation works from a stale copy.
	///</summary>
	public class SupplyLedgers {
		#region Get Methods
		///<summary>Every ledger row of the supply, oldest first.</summary>
		public static List<SupplyLedger> GetForSupply(long supplyNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<SupplyLedger>>(MethodBase.GetCurrentMethod(),supplyNum);
			}
			string command="SELECT * FROM supplyledger WHERE SupplyNum="+POut.Long(supplyNum)+" ORDER BY DateTimeEntry,SupplyLedgerNum";
			return Crud.SupplyLedgerCrud.SelectMany(command);
		}

		///<summary>The Use and Restore rows of the procedures, oldest first.</summary>
		public static List<SupplyLedger> GetForProcs(List<long> listProcNums) {
			if(listProcNums.IsNullOrEmpty()) {
				return new List<SupplyLedger>();
			}
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<SupplyLedger>>(MethodBase.GetCurrentMethod(),listProcNums);
			}
			string command="SELECT * FROM supplyledger WHERE ProcNum IN("+string.Join(",",listProcNums.Select(x => POut.Long(x)))+") "
				+"AND LedgerType IN("+(int)SupplyLedgerType.Use+","+(int)SupplyLedgerType.Restore+") ORDER BY SupplyLedgerNum";
			return Crud.SupplyLedgerCrud.SelectMany(command);
		}

		///<summary>The procedures that still hold materials (their Use and Restore rows of some supply and lot sum to less than 0) although they are
		///no longer complete, or no longer exist. Left by a failure between a procedure's status write and its restore, or by another workstation
		///un-completing a procedure while its materials were being confirmed. For the repair.</summary>
		public static List<long> GetProcNumsHeldNotComplete() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<long>>(MethodBase.GetCurrentMethod());
			}
			string command="SELECT DISTINCT held.ProcNum FROM ("
					+"SELECT supplyledger.ProcNum FROM supplyledger "
					+"WHERE supplyledger.ProcNum>0 AND supplyledger.LedgerType IN("+(int)SupplyLedgerType.Use+","+(int)SupplyLedgerType.Restore+") "
					+"GROUP BY supplyledger.ProcNum,supplyledger.SupplyNum,supplyledger.SupplyLotNum "
					+"HAVING ROUND(SUM(supplyledger.Quantity),2)<0"
				+") held "
				+"LEFT JOIN procedurelog ON procedurelog.ProcNum=held.ProcNum "
				+"WHERE procedurelog.ProcNum IS NULL OR procedurelog.ProcStatus<>"+POut.Int((int)ProcStat.C)+" "
				+"ORDER BY held.ProcNum";
			return Db.GetListLong(command);
		}

		///<summary>The Receive rows of the order items.</summary>
		public static List<SupplyLedger> GetReceiptsForOrderItems(List<long> listSupplyOrderItemNums) {
			if(listSupplyOrderItemNums.IsNullOrEmpty()) {
				return new List<SupplyLedger>();
			}
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<SupplyLedger>>(MethodBase.GetCurrentMethod(),listSupplyOrderItemNums);
			}
			string command="SELECT * FROM supplyledger WHERE SupplyOrderItemNum IN("+string.Join(",",listSupplyOrderItemNums.Select(x => POut.Long(x)))+") "
				+"AND LedgerType="+(int)SupplyLedgerType.Receive+" ORDER BY SupplyLedgerNum";
			return Crud.SupplyLedgerCrud.SelectMany(command);
		}

		///<summary>The sum of the supply's ledger rows: what its LevelOnHand must be.</summary>
		public static double GetSumForSupply(long supplyNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetDouble(MethodBase.GetCurrentMethod(),supplyNum);
			}
			return PIn.Double(Db.GetScalar("SELECT COALESCE(SUM(Quantity),0) FROM supplyledger WHERE SupplyNum="+POut.Long(supplyNum)));
		}

		///<summary>The supply's stock that has no lot: the sum of its ledger rows with SupplyLotNum 0.</summary>
		public static double GetSumWithoutLot(long supplyNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetDouble(MethodBase.GetCurrentMethod(),supplyNum);
			}
			return PIn.Double(Db.GetScalar("SELECT COALESCE(SUM(Quantity),0) FROM supplyledger WHERE SupplyNum="+POut.Long(supplyNum)
				+" AND SupplyLotNum=0"));
		}

		///<summary>True when the supply has ledger rows other than opening balances and manual adjustments: it has a history that must be kept.
		///</summary>
		public static bool HasHistory(long supplyNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetBool(MethodBase.GetCurrentMethod(),supplyNum);
			}
			string command="SELECT COUNT(*) FROM supplyledger WHERE SupplyNum="+POut.Long(supplyNum)+" "
				+"AND LedgerType NOT IN("+(int)SupplyLedgerType.Opening+","+(int)SupplyLedgerType.Adjust+")";
			return PIn.Long(Db.GetCount(command))>0;
		}

		///<summary>Usage by item for the date range (ledger dates, both days included): Use, Restore and Dispense rows. Columns SupplyNum, Descript,
		///CatalogNumber, QtyUsed, Cost. Sorted by description.</summary>
		public static DataTable GetUsageByItem(DateTime dateFrom,DateTime dateTo) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetTable(MethodBase.GetCurrentMethod(),dateFrom,dateTo);
			}
			string command="SELECT supply.SupplyNum,supply.Descript,supply.CatalogNumber,-SUM(supplyledger.Quantity) QtyUsed,"
				+"-SUM(supplyledger.Quantity*supplyledger.UnitCost) Cost "
				+"FROM supplyledger INNER JOIN supply ON supply.SupplyNum=supplyledger.SupplyNum "
				+"WHERE supplyledger.LedgerType IN("+(int)SupplyLedgerType.Use+","+(int)SupplyLedgerType.Restore+","+(int)SupplyLedgerType.Dispense+") "
				+"AND supplyledger.DateTimeEntry>="+POut.Date(dateFrom)+" AND supplyledger.DateTimeEntry<"+POut.Date(dateTo.AddDays(1))+" "
				+"GROUP BY supply.SupplyNum,supply.Descript,supply.CatalogNumber "
				+"HAVING ROUND(SUM(supplyledger.Quantity),2)<>0 "
				+"ORDER BY supply.Descript,supply.SupplyNum";
			return Db.GetTable(command);
		}

		///<summary>Usage by procedure code for the date range (ledger dates, both days included): Use and Restore rows. Columns CodeNum, ProcCode,
		///CodeDescript, SupplyNum, Descript, CountProcs, QtyUsed, Cost. Sorted by code, then supply.</summary>
		public static DataTable GetUsageByProcCode(DateTime dateFrom,DateTime dateTo) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetTable(MethodBase.GetCurrentMethod(),dateFrom,dateTo);
			}
			string command="SELECT procedurecode.CodeNum,procedurecode.ProcCode,procedurecode.Descript CodeDescript,supply.SupplyNum,supply.Descript,"
				+"COUNT(DISTINCT supplyledger.ProcNum) CountProcs,-SUM(supplyledger.Quantity) QtyUsed,-SUM(supplyledger.Quantity*supplyledger.UnitCost) Cost "
				+"FROM supplyledger "
				+"INNER JOIN supply ON supply.SupplyNum=supplyledger.SupplyNum "
				+"INNER JOIN procedurelog ON procedurelog.ProcNum=supplyledger.ProcNum "
				+"INNER JOIN procedurecode ON procedurecode.CodeNum=procedurelog.CodeNum "
				+"WHERE supplyledger.LedgerType IN("+(int)SupplyLedgerType.Use+","+(int)SupplyLedgerType.Restore+") "
				+"AND supplyledger.DateTimeEntry>="+POut.Date(dateFrom)+" AND supplyledger.DateTimeEntry<"+POut.Date(dateTo.AddDays(1))+" "
				+"GROUP BY procedurecode.CodeNum,procedurecode.ProcCode,procedurecode.Descript,supply.SupplyNum,supply.Descript "
				+"HAVING ROUND(SUM(supplyledger.Quantity),2)<>0 "
				+"ORDER BY procedurecode.ProcCode,supply.Descript,supply.SupplyNum";
			return Db.GetTable(command);
		}
		#endregion Get Methods

		#region Insert
		///<summary>Writes the ledger rows and then sets LevelOnHand of their supplies and QtyOnHand of their lots to the sums of their ledger rows.
		///This is the only way stock changes. The ledger is written first: each row is one INSERT, which MyISAM applies whole or not at all. The
		///counters are recomputed from the ledger, never incremented from a value read earlier, so two workstations writing at the same time both end
		///with the right total, and a recompute that did not run is corrected by the next movement of that supply or by RecomputeAll. Sets UserNum to
		///the current user when it is 0. Returns the rows with their keys.</summary>
		public static List<SupplyLedger> InsertMany(List<SupplyLedger> listSupplyLedgers) {
			if(listSupplyLedgers.IsNullOrEmpty()) {
				return new List<SupplyLedger>();
			}
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<SupplyLedger>>(MethodBase.GetCurrentMethod(),listSupplyLedgers);
			}
			for(int i=0;i<listSupplyLedgers.Count;i++) {
				SupplyLedger supplyLedger=listSupplyLedgers[i];
				PrepareRow(supplyLedger);
				Crud.SupplyLedgerCrud.Insert(supplyLedger);
			}
			RecomputeForSupplies(listSupplyLedgers.Select(x => x.SupplyNum).Distinct().ToList());
			RecomputeForLots(listSupplyLedgers.Select(x => x.SupplyLotNum).Where(x => x!=0).Distinct().ToList());
			return listSupplyLedgers;
		}

		///<summary>Writes Restore rows of procedures, each one only while what its procedure holds of the row's supply and lot is still exactly what
		///the row puts back: one INSERT ... SELECT ... WHERE statement per row, which MyISAM runs under one lock of supplyledger. So two workstations
		///putting back the same procedure at the same time never put it back twice: the second one finds the first one's row and writes nothing.
		///Whatever is left held after that (another workstation took more in between) is put back by the next restore of that procedure or by the
		///repair. Then recomputes the counters as InsertMany does. Returns the rows written; their keys are not read back.</summary>
		public static List<SupplyLedger> InsertRestoresIfStillHeld(List<SupplyLedger> listSupplyLedgers) {
			if(listSupplyLedgers.IsNullOrEmpty()) {
				return new List<SupplyLedger>();
			}
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<SupplyLedger>>(MethodBase.GetCurrentMethod(),listSupplyLedgers);
			}
			List<SupplyLedger> listSupplyLedgersWritten=new List<SupplyLedger>();
			foreach(SupplyLedger supplyLedger in listSupplyLedgers) {
				if(supplyLedger.LedgerType!=SupplyLedgerType.Restore || supplyLedger.ProcNum==0 || Math.Round(supplyLedger.Quantity,2)<=0) {
					throw new ApplicationException("Only Restore rows of a procedure are written this way.");
				}
				PrepareRow(supplyLedger);
				string command="INSERT INTO supplyledger ("+(PrefC.RandomKeys ? "SupplyLedgerNum," : "")
					+"SupplyNum,SupplyLotNum,LedgerType,Quantity,UnitCost,ProcNum,PatNum,SupplyOrderItemNum,DispSupplyNum,UserNum,DateTimeEntry,Reason) "
					+"SELECT "+(PrefC.RandomKeys ? POut.Long(ReplicationServers.GetKey("supplyledger","SupplyLedgerNum"))+"," : "")
					+POut.Long(supplyLedger.SupplyNum)+","+POut.Long(supplyLedger.SupplyLotNum)+","+(int)supplyLedger.LedgerType+","
					+POut.Double(supplyLedger.Quantity)+","+POut.Double(supplyLedger.UnitCost)+","+POut.Long(supplyLedger.ProcNum)+","
					+POut.Long(supplyLedger.PatNum)+","+POut.Long(supplyLedger.SupplyOrderItemNum)+","+POut.Long(supplyLedger.DispSupplyNum)+","
					+POut.Long(supplyLedger.UserNum)+","+DbHelper.Now()+",'"+POut.String(supplyLedger.Reason)+"' "
					+"FROM DUAL WHERE ABS((SELECT COALESCE(SUM(supplyledger.Quantity),0) FROM supplyledger "
						+"WHERE supplyledger.ProcNum="+POut.Long(supplyLedger.ProcNum)+" AND supplyledger.SupplyNum="+POut.Long(supplyLedger.SupplyNum)+" "
						+"AND supplyledger.SupplyLotNum="+POut.Long(supplyLedger.SupplyLotNum)+" "
						+"AND supplyledger.LedgerType IN("+(int)SupplyLedgerType.Use+","+(int)SupplyLedgerType.Restore+"))"
					+"+"+POut.Double(supplyLedger.Quantity)+")<0.005";
				if(Db.NonQ(command)>0) {
					listSupplyLedgersWritten.Add(supplyLedger);
				}
			}
			RecomputeForSupplies(listSupplyLedgersWritten.Select(x => x.SupplyNum).Distinct().ToList());
			RecomputeForLots(listSupplyLedgersWritten.Select(x => x.SupplyLotNum).Where(x => x!=0).Distinct().ToList());
			return listSupplyLedgersWritten;
		}

		///<summary>Checks and completes a row before it is written: a supply, 2 decimals, the current user when UserNum is 0, a reason of at most 255
		///characters.</summary>
		private static void PrepareRow(SupplyLedger supplyLedger) {
			if(supplyLedger.SupplyNum==0) {
				throw new ApplicationException("A stock ledger row needs a supply.");
			}
			supplyLedger.Quantity=Math.Round(supplyLedger.Quantity,2);
			if(supplyLedger.UserNum==0 && Security.CurUser!=null) {
				supplyLedger.UserNum=Security.CurUser.UserNum;
			}
			if(supplyLedger.Reason==null) {
				supplyLedger.Reason="";
			}
			if(supplyLedger.Reason.Length>255) {
				supplyLedger.Reason=supplyLedger.Reason.Substring(0,255);
			}
		}
		#endregion Insert

		#region Recompute
		///<summary>Sets LevelOnHand of the supplies to the sums of their ledger rows, in one statement.</summary>
		public static void RecomputeForSupplies(List<long> listSupplyNums) {
			if(listSupplyNums.IsNullOrEmpty()) {
				return;
			}
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),listSupplyNums);
				return;
			}
			Db.NonQ("UPDATE supply SET LevelOnHand="+SUM_SUPPLY+" WHERE SupplyNum IN("+string.Join(",",listSupplyNums.Select(x => POut.Long(x)))+")");
		}

		///<summary>Sets QtyOnHand of the lots to the sums of their ledger rows, in one statement.</summary>
		public static void RecomputeForLots(List<long> listSupplyLotNums) {
			if(listSupplyLotNums.IsNullOrEmpty()) {
				return;
			}
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),listSupplyLotNums);
				return;
			}
			Db.NonQ("UPDATE supplylot SET QtyOnHand="+SUM_LOT+" WHERE SupplyLotNum IN("+string.Join(",",listSupplyLotNums.Select(x => POut.Long(x)))+")");
		}

		///<summary>The repair: sets every supply's LevelOnHand and every lot's QtyOnHand that differs from the sum of its ledger rows to that sum.
		///Safe at any time. Returns how many supplies and how many lots it changed.</summary>
		public static SupplyRepairResult RecomputeAll() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<SupplyRepairResult>(MethodBase.GetCurrentMethod());
			}
			SupplyRepairResult supplyRepairResult=new SupplyRepairResult();
			//The WHERE makes the matched rows the changed rows, so the count is right whether the connection reports found or affected rows.
			supplyRepairResult.CountSupplies=Db.NonQ("UPDATE supply SET LevelOnHand="+SUM_SUPPLY+" WHERE ROUND(LevelOnHand,2)<>ROUND("+SUM_SUPPLY+",2)");
			supplyRepairResult.CountLots=Db.NonQ("UPDATE supplylot SET QtyOnHand="+SUM_LOT+" WHERE ROUND(QtyOnHand,2)<>ROUND("+SUM_LOT+",2)");
			return supplyRepairResult;
		}

		private const string SUM_SUPPLY="(SELECT COALESCE(SUM(supplyledger.Quantity),0) FROM supplyledger WHERE supplyledger.SupplyNum=supply.SupplyNum)";
		private const string SUM_LOT="(SELECT COALESCE(SUM(supplyledger.Quantity),0) FROM supplyledger WHERE supplyledger.SupplyLotNum=supplylot.SupplyLotNum)";
		#endregion Recompute

		#region Delete
		///<summary>Deletes the opening and manual adjustment rows of a supply that is being deleted. Throws if the supply has any other row (a history
		///that must be kept).</summary>
		public static void DeleteForSupplyWithoutHistory(long supplyNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),supplyNum);
				return;
			}
			if(HasHistory(supplyNum)) {
				throw new ApplicationException(Lans.g("Supplies","Supply has stock history (receipts, uses, counts or dispensing). Hide it instead."));
			}
			Db.NonQ("DELETE FROM supplyledger WHERE SupplyNum="+POut.Long(supplyNum));
		}
		#endregion Delete

		#region Misc
		///<summary>The net quantity each procedure holds of each supply and lot: the sum of its Use and Restore rows, negated, so a positive value is
		///what it used. Entries that net to 0 are left out.</summary>
		public static List<SupplyUsageLine> GetNetUsage(List<SupplyLedger> listSupplyLedgers) {
			Meth.NoCheckMiddleTierRole();
			return listSupplyLedgers
				.Where(x => x.LedgerType==SupplyLedgerType.Use || x.LedgerType==SupplyLedgerType.Restore)
				.GroupBy(x => new { x.ProcNum,x.SupplyNum,x.SupplyLotNum })
				.Select(x => new SupplyUsageLine(x.Key.ProcNum,x.Key.SupplyNum,x.Key.SupplyLotNum,Math.Round(-x.Sum(y => y.Quantity),2)))
				.Where(x => x.Quantity!=0)
				.ToList();
		}
		#endregion Misc
	}

	///<summary>What the repair changed (SupplyLedgers.RecomputeAll, InventoryTracking.Repair).</summary>
	[Serializable]
	public class SupplyRepairResult {
		public long CountSupplies;
		public long CountLots;
		///<summary>Procedures no longer complete whose materials the repair put back (InventoryTracking.Repair only).</summary>
		public long CountProcs;
		///<summary>Awaiting rows of procedures no longer complete that the repair removed (InventoryTracking.Repair only).</summary>
		public long CountAwaiting;
	}
}
