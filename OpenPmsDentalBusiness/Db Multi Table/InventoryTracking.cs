// Inventory tracking (docs/specs/inventory-tracking.md): what completing, un-completing and deleting procedures, confirming materials, receiving,
// counting, adjusting, dispensing and the repair do to stock, and the low-stock and expiring alerts.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>Inventory tracking. Every stock change is written through SupplyLedgers.InsertMany (ledger first, then the counters from the ledger).
	///Completing a procedure never deducts by itself: it records the procedure as awaiting materials, and only ConfirmMaterials (the Materials used
	///panel, the Awaiting Materials list) deducts what a user confirmed.</summary>
	public class InventoryTracking {
		public const string REASON_UNCOMPLETED="Procedure no longer complete";
		public const string REASON_DELETED="Procedure deleted";
		public const string REASON_CORRECTED="Materials corrected";
		public const string REASON_ON_HAND_EDITED="On hand edited";
		public const string REASON_OPENING="Opening balance";
		public const string REASON_DISPENSED="Dispensed";
		///<summary>The full alert refresh runs at most this often per program (RefreshAlertsIfDue).</summary>
		public static readonly TimeSpan TimeSpanAlertRefresh=TimeSpan.FromMinutes(10);
		private static DateTime _dateTimeAlertsRefreshed=DateTime.MinValue;
		private static readonly object _lockAlertsRefreshed=new object();

		///<summary>Raised in the program, on the thread that completed it, for each procedure that became complete (Procedures.Insert of a complete
		///procedure, Procedures.Update to complete) while inventory tracking is on, also when the work ran on the Middle Tier. The main window
		///collects them from its own thread only and opens one Materials used panel per user action. Never raised on the Middle Tier server's side
		///for a client.</summary>
		public static event Action<long> ProcSetComplete;

		#region Settings
		///<summary>Databases (server and name) known to have the inventory tables. Only a positive answer is remembered: a database gets the tables
		///from the fork migration while the program runs, never loses them.</summary>
		private static readonly HashSet<string> _hashSetDatabasesInstalled=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static readonly object _lockInstalled=new object();

		///<summary>True when the connected database has the inventory tables (the fork migration ran on it). Every hook that reads or writes them
		///checks this first, so a database without them (a scratch database built from the Open Dental reference) is never asked for them.</summary>
		public static bool IsInstalled() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetBool(MethodBase.GetCurrentMethod());
			}
			string key=DataConnectionBase.DataConnection.GetServerName()+"/"+DataConnectionBase.DataConnection.GetDatabaseName();
			lock(_lockInstalled) {
				if(_hashSetDatabasesInstalled.Contains(key)) {
					return true;
				}
			}
			bool isInstalled=PIn.Long(Db.GetCount("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() "
				+"AND TABLE_NAME='supplyledger'"))>0;
			if(isInstalled) {
				lock(_lockInstalled) {
					_hashSetDatabasesInstalled.Add(key);
				}
			}
			return isInstalled;
		}

		///<summary>True when the office turned inventory tracking on (PrefName.InventoryTrackingEnabled). The preference row exists only once the
		///office saved the settings (SaveSettings); without it tracking is off.</summary>
		public static bool IsEnabled() {
			Meth.NoCheckMiddleTierRole();
			return PIn.Bool(PrefC.GetRawOrDefault(nameof(PrefName.InventoryTrackingEnabled),"0"));
		}

		///<summary>The lead days of the expiring alert (PrefName.InventoryExpiryAlertDays); 30 when the row is missing.</summary>
		public static int GetExpiryAlertDays() {
			Meth.NoCheckMiddleTierRole();
			return PIn.Int(PrefC.GetRawOrDefault(nameof(PrefName.InventoryExpiryAlertDays),"30"));
		}

		///<summary>Supply Inventory, Inventory, Setup: saves the two preferences, writing their rows when missing (a database gets them the first time
		///the office saves the settings, so adopting a database adds none). Turning tracking on also links the two alert types to the "All" alert
		///category where missing, and the first time ever (no preference row yet) grants Inventory Adjust to every user group that holds Supplier
		///Edit, unless a group has it already. The caller refreshes the preference, security and alert category caches. Returns the number of user
		///groups granted Inventory Adjust.</summary>
		public static int SaveSettings(bool isEnabled,int expiryAlertDays) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetInt(MethodBase.GetCurrentMethod(),isEnabled,expiryAlertDays);
			}
			bool isFirstTime=Db.GetScalar("SELECT COUNT(*) FROM preference WHERE PrefName='"+nameof(PrefName.InventoryTrackingEnabled)+"'")=="0";
			int countGroupsGranted=0;
			if(isEnabled) {
				//Links and grant first, the preference that turns tracking on last.
				AddAlertLinksIfMissing();
				if(isFirstTime) {
					countGroupsGranted=GrantAdjustToSupplierGroups();
				}
			}
			SetPrefRaw(nameof(PrefName.InventoryExpiryAlertDays),POut.Int(Math.Max(0,expiryAlertDays)));
			SetPrefRaw(nameof(PrefName.InventoryTrackingEnabled),POut.Bool(isEnabled));
			return countGroupsGranted;
		}

		private static void SetPrefRaw(string prefName,string valueString) {
			string where="WHERE PrefName='"+POut.String(prefName)+"'";
			if(Db.GetScalar("SELECT COUNT(*) FROM preference "+where)=="0") {
				Db.NonQ("INSERT INTO preference (PrefName,ValueString) VALUES('"+POut.String(prefName)+"','"+POut.String(valueString)+"')");
				return;
			}
			Db.NonQ("UPDATE preference SET ValueString='"+POut.String(valueString)+"' "+where);
		}

		///<summary>Links the two inventory alert types to the "All" alert category (InternalName OdAllTypes), as every alert type is linked
		///(AlertType's summary), so users subscribed to All see them. Only missing links are added.</summary>
		private static void AddAlertLinksIfMissing() {
			long alertCategoryNum=PIn.Long(Db.GetScalar("SELECT AlertCategoryNum FROM alertcategory WHERE InternalName='OdAllTypes' AND IsHQCategory=1 "
				+"ORDER BY AlertCategoryNum LIMIT 1"));
			if(alertCategoryNum==0) {
				return;
			}
			foreach(AlertType alertType in new[] { AlertType.InventoryLowStock,AlertType.InventoryExpiring }) {
				string where="WHERE AlertCategoryNum="+POut.Long(alertCategoryNum)+" AND AlertType="+(int)alertType;
				if(PIn.Long(Db.GetCount("SELECT COUNT(*) FROM alertcategorylink "+where))>0) {
					continue;
				}
				Crud.AlertCategoryLinkCrud.Insert(new AlertCategoryLink { AlertCategoryNum=alertCategoryNum,AlertType=alertType });
			}
		}

		///<summary>Grants Inventory Adjust to every user group that holds Supplier Edit, the people who look after supplies today, unless some group
		///has Inventory Adjust already. Writes a Security Admin audit entry. Returns the number of groups granted.</summary>
		private static int GrantAdjustToSupplierGroups() {
			if(PIn.Long(Db.GetCount("SELECT COUNT(*) FROM grouppermission WHERE PermType="+(int)EnumPermType.InventoryAdjust))>0) {
				return 0;
			}
			List<long> listUserGroupNums=Db.GetTable("SELECT DISTINCT UserGroupNum FROM grouppermission WHERE PermType="+(int)EnumPermType.SupplierEdit
				+" ORDER BY UserGroupNum").Select().Select(x => PIn.Long(x[0].ToString())).ToList();
			foreach(long userGroupNum in listUserGroupNums) {
				Crud.GroupPermissionCrud.Insert(new GroupPermission { UserGroupNum=userGroupNum,PermType=EnumPermType.InventoryAdjust,FKey=0,
					NewerDate=DateTime.MinValue,NewerDays=0 });
			}
			if(listUserGroupNums.Count>0) {
				SecurityLogs.MakeLogEntry(EnumPermType.SecurityAdmin,0,"Inventory tracking turned on: Inventory Adjust granted to the "+listUserGroupNums.Count
					+" user group(s) with Supplier Edit (UserGroupNum "+string.Join(", ",listUserGroupNums)+").");
			}
			return listUserGroupNums.Count;
		}
		#endregion Settings

		#region Procedure hooks
		///<summary>Called by Procedures.Insert and Procedures.Update (on the server side) after a procedure became complete. While tracking is on and
		///the code has default materials, records the procedure as awaiting materials. Never deducts.</summary>
		public static void OnProcSetComplete(Procedure procedure) {
			Meth.NoCheckMiddleTierRole();
			if(!IsEnabled() || procedure==null || procedure.ProcNum==0) {
				return;
			}
			if(!SupplyProcCodes.HasMaterials(procedure.CodeNum)) {
				return;
			}
			SupplyProcPendings.InsertIfMissing(procedure.ProcNum,procedure.PatNum,Security.CurUser?.UserNum??0);
		}

		///<summary>Called by Procedures.Insert and Procedures.Update in the program, on the calling thread, after a procedure became complete. Raises
		///ProcSetComplete while tracking is on.</summary>
		public static void NotifyProcSetComplete(long procNum) {
			Meth.NoCheckMiddleTierRole();
			if(procNum==0 || !IsEnabled()) {
				return;
			}
			ProcSetComplete?.Invoke(procNum);
		}

		///<summary>Called by Procedures.Update when a complete procedure gets another status, and by Procedures.Delete: puts back what the procedure
		///still holds (RestoreForProc) and removes it from the awaiting list. Runs also while tracking is off, so stock stays right.</summary>
		public static void OnProcNoLongerComplete(long procNum,bool isDeleted) {
			Meth.NoCheckMiddleTierRole();
			if(procNum==0 || !IsInstalled()) {
				return;
			}
			RestoreForProc(procNum,isDeleted ? REASON_DELETED : REASON_UNCOMPLETED);
			SupplyProcPendings.DeleteForProcs(new List<long> { procNum });
		}
		#endregion Procedure hooks

		#region Materials of procedures
		///<summary>What the procedures hold now, per supply and lot (Use minus Restore rows). Quantities are positive.</summary>
		public static List<SupplyUsageLine> GetProcMaterials(List<long> listProcNums) {
			Meth.NoCheckMiddleTierRole();
			return SupplyLedgers.GetNetUsage(SupplyLedgers.GetForProcs(listProcNums));
		}

		///<summary>The default materials of each procedure's code, for the Materials used panel: one line per material, with the lots filled in
		///soonest-expiring first (split over lots when one lot does not hold enough; lot 0 when there is no lot with stock). Lots already planned for
		///an earlier procedure of the list are taken into account.</summary>
		public static List<SupplyUsageLine> GetDefaultMaterials(List<Procedure> listProcedures) {
			Meth.NoCheckMiddleTierRole();
			List<SupplyUsageLine> listSupplyUsageLines=new List<SupplyUsageLine>();
			List<SupplyProcCode> listSupplyProcCodes=SupplyProcCodes.GetForCodes(listProcedures.Select(x => x.CodeNum).Distinct().ToList());
			List<long> listSupplyNums=listSupplyProcCodes.Select(x => x.SupplyNum).Distinct().ToList();
			List<SupplyLot> listSupplyLots=SupplyLots.GetForSupplies(listSupplyNums,isEmptyIncluded:false);
			foreach(Procedure procedure in listProcedures) {
				foreach(SupplyProcCode supplyProcCode in listSupplyProcCodes.FindAll(x => x.CodeNum==procedure.CodeNum)) {
					List<SupplyUsageLine> listAllocated=SupplyLots.AllocateFifo(supplyProcCode.SupplyNum,supplyProcCode.Quantity,listSupplyLots);
					foreach(SupplyUsageLine supplyUsageLine in listAllocated) {
						supplyUsageLine.ProcNum=procedure.ProcNum;
						SupplyLot supplyLot=listSupplyLots.Find(x => x.SupplyLotNum==supplyUsageLine.SupplyLotNum);
						if(supplyLot!=null) {
							supplyLot.QtyOnHand=Math.Round(supplyLot.QtyOnHand-supplyUsageLine.Quantity,2);//Planned for this procedure.
						}
					}
					listSupplyUsageLines.AddRange(listAllocated);
				}
			}
			return listSupplyUsageLines;
		}

		///<summary>Deducts what the user confirmed for the procedures and removes them from the awaiting list. listSupplyUsageLines holds what each
		///procedure should hold in the end, per supply and lot (SupplyLotNum -1: soonest-expiring first now). For each procedure, the difference to
		///what the ledger already holds for it is written: Use rows for more, Restore rows for less. So confirming again after a failure part-way, or
		///opening the panel again for a procedure, never deducts twice. A procedure with no lines is confirmed with no materials. Procedures that are
		///not complete any more (another workstation changed them) are skipped and returned.</summary>
		public static List<long> ConfirmMaterials(List<long> listProcNums,List<SupplyUsageLine> listSupplyUsageLines) {
			if(listProcNums.IsNullOrEmpty()) {
				return new List<long>();
			}
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<long>>(MethodBase.GetCurrentMethod(),listProcNums,listSupplyUsageLines);
			}
			List<long> listProcNumsSkipped=new List<long>();
			List<Procedure> listProcedures=Procedures.GetManyProc(listProcNums,false);
			List<SupplyLedger> listSupplyLedgersProcs=SupplyLedgers.GetForProcs(listProcNums);
			List<long> listSupplyNums=listSupplyUsageLines.Select(x => x.SupplyNum).Concat(listSupplyLedgersProcs.Select(x => x.SupplyNum)).Distinct().ToList();
			List<Supply> listSupplies=Supplies.GetMany(listSupplyNums);
			List<SupplyLedger> listSupplyLedgersNew=new List<SupplyLedger>();
			List<long> listProcNumsDone=new List<long>();
			foreach(long procNum in listProcNums.Distinct()) {
				Procedure procedure=listProcedures.Find(x => x.ProcNum==procNum);
				if(procedure==null || procedure.ProcStatus!=ProcStat.C) {
					listProcNumsSkipped.Add(procNum);
					continue;
				}
				List<SupplyLedger> listSupplyLedgersProc=listSupplyLedgersProcs.FindAll(x => x.ProcNum==procNum);
				List<SupplyUsageLine> listHeld=SupplyLedgers.GetNetUsage(listSupplyLedgersProc);
				List<SupplyUsageLine> listWanted=ResolveWanted(listSupplyUsageLines.FindAll(x => x.ProcNum==procNum && x.Quantity>0),listHeld);
				List<(long SupplyNum,long SupplyLotNum)> listKeys=listWanted.Select(x => (x.SupplyNum,x.SupplyLotNum))
					.Concat(listHeld.Select(x => (x.SupplyNum,x.SupplyLotNum))).Distinct().ToList();
				foreach((long supplyNum,long supplyLotNum) in listKeys) {
					double qtyWanted=listWanted.Where(x => x.SupplyNum==supplyNum && x.SupplyLotNum==supplyLotNum).Sum(x => x.Quantity);
					double qtyHeld=listHeld.Where(x => x.SupplyNum==supplyNum && x.SupplyLotNum==supplyLotNum).Sum(x => x.Quantity);
					double qtyDiff=Math.Round(qtyWanted-qtyHeld,2);
					if(qtyDiff==0) {
						continue;
					}
					SupplyLedger supplyLedger=new SupplyLedger();
					supplyLedger.SupplyNum=supplyNum;
					supplyLedger.SupplyLotNum=supplyLotNum;
					supplyLedger.ProcNum=procNum;
					supplyLedger.PatNum=procedure.PatNum;
					supplyLedger.Quantity=-qtyDiff;
					if(qtyDiff>0) {
						supplyLedger.LedgerType=SupplyLedgerType.Use;
						supplyLedger.UnitCost=listSupplies.Find(x => x.SupplyNum==supplyNum)?.Price??0;
						supplyLedger.Reason="";
					}
					else {
						supplyLedger.LedgerType=SupplyLedgerType.Restore;
						supplyLedger.UnitCost=GetUseCost(listSupplyLedgersProc,supplyNum,supplyLotNum,listSupplies);
						supplyLedger.Reason=REASON_CORRECTED;
					}
					listSupplyLedgersNew.Add(supplyLedger);
				}
				listProcNumsDone.Add(procNum);
			}
			//Ledger first (it recomputes the counters), the awaiting rows last.
			SupplyLedgers.InsertMany(listSupplyLedgersNew);
			SupplyProcPendings.DeleteForProcs(listProcNumsDone.Concat(listProcNumsSkipped).ToList());
			if(IsEnabled()) {
				RefreshLowStockAlerts(listSupplyLedgersNew.Select(x => x.SupplyNum).Distinct().ToList());
			}
			return listProcNumsSkipped;
		}

		///<summary>Turns the lines of one procedure into explicit lots. A supply with any line of lot -1 is taken as a whole: what the procedure already
		///holds of it is kept as it is, more is allocated soonest-expiring first from the lots in the database now, and less is given back from stock
		///without a lot first, then from the lots that expire last.</summary>
		private static List<SupplyUsageLine> ResolveWanted(List<SupplyUsageLine> listLines,List<SupplyUsageLine> listHeld) {
			List<SupplyUsageLine> listWanted=new List<SupplyUsageLine>();
			foreach(IGrouping<long,SupplyUsageLine> grouping in listLines.GroupBy(x => x.SupplyNum)) {
				if(!grouping.Any(x => x.SupplyLotNum==-1)) {
					listWanted.AddRange(grouping.Select(x => x.Copy()));
					continue;
				}
				long supplyNum=grouping.Key;
				double qtyWanted=Math.Round(grouping.Sum(x => x.Quantity),2);
				List<SupplyUsageLine> listHeldSupply=listHeld.FindAll(x => x.SupplyNum==supplyNum && x.Quantity>0).Select(x => x.Copy()).ToList();
				double qtyHeld=Math.Round(listHeldSupply.Sum(x => x.Quantity),2);
				if(qtyWanted>=qtyHeld) {
					listWanted.AddRange(listHeldSupply);
					listWanted.AddRange(SupplyLots.AllocateFifo(supplyNum,Math.Round(qtyWanted-qtyHeld,2)));
					continue;
				}
				double qtyGiveBack=Math.Round(qtyHeld-qtyWanted,2);
				List<SupplyLot> listSupplyLots=SupplyLots.GetMany(listHeldSupply.Select(x => x.SupplyLotNum).Where(x => x!=0).ToList());
				List<long> listLotNumsOrder=new List<long> { 0 };
				listLotNumsOrder.AddRange(SupplyLots.SortFifo(listSupplyLots).Select(x => x.SupplyLotNum).Reverse());
				foreach(long supplyLotNum in listLotNumsOrder) {
					foreach(SupplyUsageLine supplyUsageLine in listHeldSupply.FindAll(x => x.SupplyLotNum==supplyLotNum)) {
						double qtyTake=Math.Min(qtyGiveBack,supplyUsageLine.Quantity);
						supplyUsageLine.Quantity=Math.Round(supplyUsageLine.Quantity-qtyTake,2);
						qtyGiveBack=Math.Round(qtyGiveBack-qtyTake,2);
					}
				}
				listWanted.AddRange(listHeldSupply.FindAll(x => x.Quantity>0));
			}
			return listWanted;
		}

		///<summary>The unit cost of the procedure's latest Use row of the supply and lot, or the supply's price.</summary>
		private static double GetUseCost(List<SupplyLedger> listSupplyLedgersProc,long supplyNum,long supplyLotNum,List<Supply> listSupplies) {
			SupplyLedger supplyLedgerUse=listSupplyLedgersProc.LastOrDefault(x => x.LedgerType==SupplyLedgerType.Use && x.SupplyNum==supplyNum
				&& x.SupplyLotNum==supplyLotNum);
			if(supplyLedgerUse!=null) {
				return supplyLedgerUse.UnitCost;
			}
			return listSupplies.Find(x => x.SupplyNum==supplyNum)?.Price??0;
		}

		///<summary>Writes a Restore row for every supply and lot the procedure still holds (the sum of its Use and Restore rows), with the lot it was
		///taken from and its cost. Writes nothing when it holds nothing.</summary>
		public static void RestoreForProc(long procNum,string reason) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),procNum,reason);
				return;
			}
			List<SupplyLedger> listSupplyLedgersProc=SupplyLedgers.GetForProcs(new List<long> { procNum });
			List<SupplyUsageLine> listHeld=SupplyLedgers.GetNetUsage(listSupplyLedgersProc).FindAll(x => x.Quantity>0);
			if(listHeld.Count==0) {
				return;
			}
			List<Supply> listSupplies=Supplies.GetMany(listHeld.Select(x => x.SupplyNum).Distinct().ToList());
			List<SupplyLedger> listSupplyLedgersNew=new List<SupplyLedger>();
			foreach(SupplyUsageLine supplyUsageLine in listHeld) {
				SupplyLedger supplyLedger=new SupplyLedger();
				supplyLedger.SupplyNum=supplyUsageLine.SupplyNum;
				supplyLedger.SupplyLotNum=supplyUsageLine.SupplyLotNum;
				supplyLedger.LedgerType=SupplyLedgerType.Restore;
				supplyLedger.Quantity=supplyUsageLine.Quantity;
				supplyLedger.UnitCost=GetUseCost(listSupplyLedgersProc,supplyUsageLine.SupplyNum,supplyUsageLine.SupplyLotNum,listSupplies);
				supplyLedger.ProcNum=procNum;
				supplyLedger.PatNum=listSupplyLedgersProc.First(x => x.SupplyNum==supplyUsageLine.SupplyNum).PatNum;
				supplyLedger.Reason=reason;
				listSupplyLedgersNew.Add(supplyLedger);
			}
			SupplyLedgers.InsertMany(listSupplyLedgersNew);
			if(IsEnabled()) {
				RefreshLowStockAlerts(listSupplyLedgersNew.Select(x => x.SupplyNum).Distinct().ToList());
			}
		}
		#endregion Materials of procedures

		#region Receiving, counts, adjustments, dispensing, repair
		///<summary>Receives order items: one Receive row per line at the order item's price, in the lot of the line's lot number (found or created)
		///or without a lot. Then sets an order item's received date when it is fully received, and the order's when every item is. Writes a Supply
		///Purchases audit entry per order. Returns the ledger rows.</summary>
		public static List<SupplyLedger> Receive(List<SupplyReceiveLine> listSupplyReceiveLines) {
			if(listSupplyReceiveLines.IsNullOrEmpty()) {
				return new List<SupplyLedger>();
			}
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<SupplyLedger>>(MethodBase.GetCurrentMethod(),listSupplyReceiveLines);
			}
			List<SupplyLedger> listSupplyLedgers=new List<SupplyLedger>();
			List<SupplyOrderItem> listSupplyOrderItems=new List<SupplyOrderItem>();
			foreach(SupplyReceiveLine supplyReceiveLine in listSupplyReceiveLines.Where(x => Math.Round(x.Quantity,2)!=0)) {
				SupplyOrderItem supplyOrderItem=listSupplyOrderItems.Find(x => x.SupplyOrderItemNum==supplyReceiveLine.SupplyOrderItemNum);
				if(supplyOrderItem==null) {
					supplyOrderItem=SupplyOrderItems.SelectOne(supplyReceiveLine.SupplyOrderItemNum);
					if(supplyOrderItem==null) {
						throw new ApplicationException(Lans.g("InventoryTracking","An order item was deleted by another user."));
					}
					listSupplyOrderItems.Add(supplyOrderItem);
				}
				long supplyLotNum=0;
				if(!string.IsNullOrWhiteSpace(supplyReceiveLine.LotNumber)) {
					supplyLotNum=SupplyLots.GetOrInsert(supplyOrderItem.SupplyNum,supplyReceiveLine.LotNumber,supplyReceiveLine.DateExpiry,
						supplyOrderItem.SupplyOrderItemNum).SupplyLotNum;
				}
				SupplyLedger supplyLedger=new SupplyLedger();
				supplyLedger.SupplyNum=supplyOrderItem.SupplyNum;
				supplyLedger.SupplyLotNum=supplyLotNum;
				supplyLedger.LedgerType=SupplyLedgerType.Receive;
				supplyLedger.Quantity=supplyReceiveLine.Quantity;
				supplyLedger.UnitCost=supplyOrderItem.Price;
				supplyLedger.SupplyOrderItemNum=supplyOrderItem.SupplyOrderItemNum;
				supplyLedger.Reason="";
				listSupplyLedgers.Add(supplyLedger);
			}
			SupplyLedgers.InsertMany(listSupplyLedgers);
			//Received dates, from every receipt of the items (other workstations' too).
			List<SupplyLedger> listSupplyLedgersReceived=SupplyLedgers.GetReceiptsForOrderItems(listSupplyOrderItems.Select(x => x.SupplyOrderItemNum).ToList());
			foreach(SupplyOrderItem supplyOrderItem in listSupplyOrderItems) {
				double qtyReceived=listSupplyLedgersReceived.Where(x => x.SupplyOrderItemNum==supplyOrderItem.SupplyOrderItemNum).Sum(x => x.Quantity);
				if(supplyOrderItem.DateReceived.Year<1880 && Math.Round(qtyReceived,2)>=supplyOrderItem.Qty) {
					supplyOrderItem.DateReceived=DateTime.Today;
					SupplyOrderItems.Update(supplyOrderItem);
				}
			}
			foreach(long supplyOrderNum in listSupplyOrderItems.Select(x => x.SupplyOrderNum).Distinct()) {
				SupplyOrder supplyOrder=SupplyOrders.GetOne(supplyOrderNum);
				if(supplyOrder==null) {
					continue;
				}
				List<SupplyOrderItem> listItemsOrder=SupplyOrderItems.GetSupplyItemsForOrder(supplyOrderNum);
				int countLines=listSupplyLedgers.Count(x => listItemsOrder.Any(y => y.SupplyOrderItemNum==x.SupplyOrderItemNum));
				SecurityLogs.MakeLogEntry(EnumPermType.SupplyPurchases,0,"Order received: "+countLines+" line(s), supplier "
					+Suppliers.GetOne(supplyOrder.SupplierNum)?.Name+", order of "+(supplyOrder.IsPending() ? "(not placed)" : supplyOrder.DatePlaced.ToShortDateString()));
				if(supplyOrder.DateReceived.Year<1880 && listItemsOrder.All(x => x.DateReceived.Year>1880)) {
					supplyOrder.DateReceived=DateTime.Today;
					SupplyOrders.Update(supplyOrder);
				}
			}
			if(IsEnabled()) {
				RefreshLowStockAlerts(listSupplyLedgers.Select(x => x.SupplyNum).Distinct().ToList());
			}
			return listSupplyLedgers;
		}

		///<summary>Posts a stock count: for every line whose counted quantity differs from the quantity the program had when the line was counted, a
		///CountAdjust row of the difference, so that procedures completed during the count are not counted twice. A line with a lot number and no
		///SupplyLotNum is a lot found in the count: it is found or created. Writes an Inventory Adjust audit entry per adjusted line. The reason is
		///required. Returns the number of adjusted lines.</summary>
		public static int PostCount(List<SupplyCountLine> listSupplyCountLines,string reason) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetInt(MethodBase.GetCurrentMethod(),listSupplyCountLines,reason);
			}
			if(string.IsNullOrWhiteSpace(reason)) {
				throw new ApplicationException(Lans.g("InventoryTracking","A reason is required."));
			}
			List<SupplyLedger> listSupplyLedgers=new List<SupplyLedger>();
			List<string> listLogTexts=new List<string>();
			List<Supply> listSupplies=Supplies.GetMany(listSupplyCountLines.Select(x => x.SupplyNum).Distinct().ToList());
			foreach(SupplyCountLine supplyCountLine in listSupplyCountLines) {
				double qtyDiff=Math.Round(supplyCountLine.QtyCounted-supplyCountLine.QtyProgram,2);
				if(qtyDiff==0) {
					continue;
				}
				Supply supply=listSupplies.Find(x => x.SupplyNum==supplyCountLine.SupplyNum);
				if(supply==null) {
					continue;//Deleted by another user since it was counted.
				}
				long supplyLotNum=supplyCountLine.SupplyLotNum;
				string lotNumber=supplyCountLine.LotNumber??"";
				if(supplyLotNum==0 && !string.IsNullOrWhiteSpace(lotNumber)) {
					supplyLotNum=SupplyLots.GetOrInsert(supply.SupplyNum,lotNumber,supplyCountLine.DateExpiry,0).SupplyLotNum;
				}
				SupplyLedger supplyLedger=new SupplyLedger();
				supplyLedger.SupplyNum=supply.SupplyNum;
				supplyLedger.SupplyLotNum=supplyLotNum;
				supplyLedger.LedgerType=SupplyLedgerType.CountAdjust;
				supplyLedger.Quantity=qtyDiff;
				supplyLedger.UnitCost=supply.Price;
				supplyLedger.Reason=reason.Trim();
				listSupplyLedgers.Add(supplyLedger);
				listLogTexts.Add("Stock count: "+supply.Descript+(lotNumber=="" ? "" : ", lot "+lotNumber)+": program had "+FormatQty(supplyCountLine.QtyProgram)
					+", counted "+FormatQty(supplyCountLine.QtyCounted)+", adjusted by "+FormatQtySigned(qtyDiff)+". Reason: "+reason.Trim());
			}
			SupplyLedgers.InsertMany(listSupplyLedgers);
			foreach(string logText in listLogTexts) {
				SecurityLogs.MakeLogEntry(EnumPermType.InventoryAdjust,0,logText);
			}
			if(IsEnabled()) {
				RefreshLowStockAlerts(listSupplyLedgers.Select(x => x.SupplyNum).Distinct().ToList());
			}
			return listSupplyLedgers.Count;
		}

		///<summary>A manual adjustment of the supply (and lot) by qtyChange, with a reason (required), and an Inventory Adjust audit entry.</summary>
		public static void Adjust(long supplyNum,long supplyLotNum,double qtyChange,string reason) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),supplyNum,supplyLotNum,qtyChange,reason);
				return;
			}
			if(string.IsNullOrWhiteSpace(reason)) {
				throw new ApplicationException(Lans.g("InventoryTracking","A reason is required."));
			}
			qtyChange=Math.Round(qtyChange,2);
			if(qtyChange==0) {
				return;
			}
			Supply supply=Supplies.GetSupply(supplyNum);
			if(supply==null) {
				throw new ApplicationException(Lans.g("InventoryTracking","The supply was deleted by another user."));
			}
			string lotNumber="";
			if(supplyLotNum!=0) {
				lotNumber=SupplyLots.GetMany(new List<long> { supplyLotNum }).FirstOrDefault()?.LotNumber??"";
			}
			SupplyLedger supplyLedger=new SupplyLedger();
			supplyLedger.SupplyNum=supplyNum;
			supplyLedger.SupplyLotNum=supplyLotNum;
			supplyLedger.LedgerType=SupplyLedgerType.Adjust;
			supplyLedger.Quantity=qtyChange;
			supplyLedger.UnitCost=supply.Price;
			supplyLedger.Reason=reason.Trim();
			SupplyLedgers.InsertMany(new List<SupplyLedger> { supplyLedger });
			SecurityLogs.MakeLogEntry(EnumPermType.InventoryAdjust,0,"Stock adjusted: "+supply.Descript+(lotNumber=="" ? "" : ", lot "+lotNumber)+" by "
				+FormatQtySigned(qtyChange)+". Reason: "+reason.Trim());
			if(IsEnabled()) {
				RefreshLowStockAlerts(new List<long> { supplyNum });
			}
		}

		///<summary>The On Hand field of the supply windows, edited while tracking is off: an Adjust row of the difference between the new value and
		///the ledger now (stock without a lot), and an Inventory Adjust audit entry. Does nothing when there is no difference.</summary>
		public static void SetOnHand(long supplyNum,double qtyOnHandNew) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),supplyNum,qtyOnHandNew);
				return;
			}
			if(!IsInstalled()) {//A database without the inventory tables: as before.
				Db.NonQ("UPDATE supply SET LevelOnHand="+POut.Float((float)qtyOnHandNew)+" WHERE SupplyNum="+POut.Long(supplyNum));
				return;
			}
			double qtyChange=Math.Round(qtyOnHandNew-SupplyLedgers.GetSumForSupply(supplyNum),2);
			if(qtyChange==0) {
				return;
			}
			Adjust(supplyNum,0,qtyChange,REASON_ON_HAND_EDITED);
		}

		///<summary>Called by DispSupplies.Insert: while tracking is on, a Dispense row for the dispensed quantity.</summary>
		public static void OnDispensed(DispSupply dispSupply) {
			Meth.NoCheckMiddleTierRole();
			if(!IsEnabled() || dispSupply.SupplyNum==0 || Math.Round(dispSupply.DispQuantity,2)==0) {
				return;
			}
			Supply supply=Supplies.GetSupply(dispSupply.SupplyNum);
			SupplyLedger supplyLedger=new SupplyLedger();
			supplyLedger.SupplyNum=dispSupply.SupplyNum;
			supplyLedger.LedgerType=SupplyLedgerType.Dispense;
			supplyLedger.Quantity=-dispSupply.DispQuantity;
			supplyLedger.UnitCost=supply?.Price??0;
			supplyLedger.DispSupplyNum=dispSupply.DispSupplyNum;
			supplyLedger.Reason=REASON_DISPENSED;
			SupplyLedgers.InsertMany(new List<SupplyLedger> { supplyLedger });
			RefreshLowStockAlerts(new List<long> { dispSupply.SupplyNum });
		}

		///<summary>The repair: recomputes every supply's and lot's stock from the ledger (SupplyLedgers.RecomputeAll), writes an Inventory Adjust audit
		///entry and refreshes the alerts. Safe at any time.</summary>
		public static SupplyRepairResult Repair() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<SupplyRepairResult>(MethodBase.GetCurrentMethod());
			}
			SupplyRepairResult supplyRepairResult=SupplyLedgers.RecomputeAll();
			SecurityLogs.MakeLogEntry(EnumPermType.InventoryAdjust,0,"Stock repaired from the ledger: "+supplyRepairResult.CountSupplies+" supply(ies) and "
				+supplyRepairResult.CountLots+" lot(s) changed.");
			if(IsEnabled()) {
				RefreshAllAlerts();
			}
			return supplyRepairResult;
		}

		///<summary>A quantity for display and audit text: up to 2 decimals, no trailing zeros.</summary>
		public static string FormatQty(double qty) {
			Meth.NoCheckMiddleTierRole();
			return Math.Round(qty,2).ToString("0.##");
		}

		private static string FormatQtySigned(double qty) {
			return (qty>0 ? "+" : "")+FormatQty(qty);
		}
		#endregion Receiving, counts, adjustments, dispensing, repair

		#region Alerts
		///<summary>Brings the Inventory Low Stock alerts of the supplies up to date: adds the missing ones, updates changed texts and removes the ones
		///that no longer apply.</summary>
		public static void RefreshLowStockAlerts(List<long> listSupplyNums) {
			if(listSupplyNums.IsNullOrEmpty()) {
				return;
			}
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),listSupplyNums);
				return;
			}
			SyncAlerts(AlertType.InventoryLowStock,GetLowStockWanted(listSupplyNums),listSupplyNums);
		}

		///<summary>Brings every inventory alert up to date: low stock for every supply, expiring for every lot.</summary>
		public static void RefreshAllAlerts() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod());
				return;
			}
			SyncAlerts(AlertType.InventoryLowStock,GetLowStockWanted(null),null);
			SyncAlerts(AlertType.InventoryExpiring,GetExpiringWanted(),null);
		}

		///<summary>For the program's alert check: RefreshAllAlerts while tracking is on, at most every TimeSpanAlertRefresh per program. Never throws.
		///</summary>
		public static void RefreshAlertsIfDue() {
			Meth.NoCheckMiddleTierRole();
			if(!IsEnabled()) {
				return;
			}
			lock(_lockAlertsRefreshed) {
				if(DateTime.Now-_dateTimeAlertsRefreshed<TimeSpanAlertRefresh) {
					return;
				}
				_dateTimeAlertsRefreshed=DateTime.Now;
			}
			ODException.SwallowAnyException(() => RefreshAllAlerts());
		}

		///<summary>Removes every inventory alert, for when tracking is turned off.</summary>
		public static void DeleteAllAlerts() {
			Meth.NoCheckMiddleTierRole();
			AlertItems.DeleteFor(AlertType.InventoryLowStock);
			AlertItems.DeleteFor(AlertType.InventoryExpiring);
		}

		///<summary>The wanted low-stock alert texts by SupplyNum: supplies not hidden whose reorder level is above 0 and whose on hand is at or below
		///it. Null listSupplyNums means every supply.</summary>
		private static Dictionary<long,string> GetLowStockWanted(List<long> listSupplyNums) {
			string command="SELECT supply.SupplyNum,supply.Descript,supply.LevelOnHand,supplysetting.ReorderLevel "
				+"FROM supply INNER JOIN supplysetting ON supplysetting.SupplyNum=supply.SupplyNum "
				+"WHERE supply.IsHidden=0 AND supplysetting.ReorderLevel>0 AND ROUND(supply.LevelOnHand,2)<=ROUND(supplysetting.ReorderLevel,2)";
			if(listSupplyNums!=null) {
				command+=" AND supply.SupplyNum IN("+string.Join(",",listSupplyNums.Select(x => POut.Long(x)))+")";
			}
			Dictionary<long,string> dict=new Dictionary<long,string>();
			foreach(DataRow dataRow in Db.GetTable(command).Rows) {
				dict[PIn.Long(dataRow["SupplyNum"].ToString())]=Lans.g("InventoryTracking","Low stock")+": "+dataRow["Descript"].ToString()+" ("
					+Lans.g("InventoryTracking","reorder level")+" "+FormatQty(PIn.Double(dataRow["ReorderLevel"].ToString()))+")";
			}
			return dict;
		}

		///<summary>The wanted expiring alert texts by SupplyLotNum: lots with stock that expire within the lead days, or have expired.</summary>
		private static Dictionary<long,string> GetExpiringWanted() {
			Dictionary<long,string> dict=new Dictionary<long,string>();
			int days=GetExpiryAlertDays();
			if(days<=0) {
				return dict;
			}
			DateTime dateToday=MiscData.GetNowDateTime().Date;
			List<SupplyLot> listSupplyLots=SupplyLots.GetExpiringWithStock(dateToday.AddDays(days));
			List<Supply> listSupplies=Supplies.GetMany(listSupplyLots.Select(x => x.SupplyNum).Distinct().ToList());
			foreach(SupplyLot supplyLot in listSupplyLots) {
				Supply supply=listSupplies.Find(x => x.SupplyNum==supplyLot.SupplyNum);
				if(supply==null || supply.IsHidden) {
					continue;
				}
				string verb=supplyLot.DateExpiry<dateToday ? Lans.g("InventoryTracking","Expired") : Lans.g("InventoryTracking","Expires");
				dict[supplyLot.SupplyLotNum]=verb+" "+supplyLot.DateExpiry.ToShortDateString()+": "+supply.Descript+", "
					+Lans.g("InventoryTracking","lot")+" "+supplyLot.LotNumber;
			}
			return dict;
		}

		///<summary>Makes the alerts of the type match dictWanted (FKey to text). listFKeysScope limits the change to those keys; null means every alert
		///of the type. A missing alert is added with one INSERT ... SELECT ... WHERE NOT EXISTS, which MyISAM runs under one lock of alertitem, so two
		///workstations refreshing at the same time never add the same alert twice. Extra copies of an alert are removed.</summary>
		private static void SyncAlerts(AlertType alertType,Dictionary<long,string> dictWanted,List<long> listFKeysScope) {
			List<AlertItem> listAlertItems=AlertItems.RefreshForType(alertType);
			if(listFKeysScope!=null) {
				listAlertItems=listAlertItems.FindAll(x => listFKeysScope.Contains(x.FKey));
			}
			List<long> listAlertItemNumsDelete=new List<long>();
			foreach(IGrouping<long,AlertItem> grouping in listAlertItems.GroupBy(x => x.FKey)) {
				List<AlertItem> listForKey=grouping.OrderBy(x => x.AlertItemNum).ToList();
				if(!dictWanted.ContainsKey(grouping.Key)) {
					listAlertItemNumsDelete.AddRange(listForKey.Select(x => x.AlertItemNum));
					continue;
				}
				listAlertItemNumsDelete.AddRange(listForKey.Skip(1).Select(x => x.AlertItemNum));
				if(listForKey[0].Description!=dictWanted[grouping.Key]) {
					Db.NonQ("UPDATE alertitem SET Description='"+POut.String(dictWanted[grouping.Key])+"' WHERE AlertItemNum="
						+POut.Long(listForKey[0].AlertItemNum));
				}
			}
			AlertItems.Delete(listAlertItemNumsDelete);
			foreach(KeyValuePair<long,string> pair in dictWanted) {
				if(listAlertItems.Any(x => x.FKey==pair.Key)) {
					continue;
				}
				InsertAlertIfMissing(alertType,pair.Key,pair.Value);
			}
		}

		///<summary>Adds the inventory alert of the type for fKey (all clinics, Medium, Mark as Read and Open Form, opening the Supply Inventory window)
		///unless one exists. One INSERT ... SELECT ... WHERE NOT EXISTS statement, which MyISAM runs under one lock of alertitem: of two workstations
		///that both saw no alert, only the first adds one. Returns true when it added one.</summary>
		public static bool InsertAlertIfMissing(AlertType alertType,long fKey,string description) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetBool(MethodBase.GetCurrentMethod(),alertType,fKey,description);
			}
			string where="Type="+(int)alertType+" AND FKey="+POut.Long(fKey);
			long countAdded=Db.NonQ("INSERT INTO alertitem (ClinicNum,Description,Type,Severity,Actions,FormToOpen,FKey,ItemValue,UserNum,SecDateTEntry) "
				+"SELECT -1,'"+POut.String(description)+"',"+(int)alertType+","+(int)SeverityType.Medium+","
				+(int)(ActionType.MarkAsRead|ActionType.OpenForm)+","+(int)FormType.FormSupplyInventory+","+POut.Long(fKey)+",'',0,"+DbHelper.Now()+" "
				+"FROM DUAL WHERE NOT EXISTS(SELECT 1 FROM alertitem WHERE "+where+")");
			return countAdded>0;
		}
		#endregion Alerts
	}

	///<summary>One material of one procedure: a supply, the lot (0 for stock without a lot, -1 for soonest-expiring first when it is deducted) and
	///a quantity.</summary>
	[Serializable]
	public class SupplyUsageLine {
		///<summary>FK to procedurelog.ProcNum.</summary>
		public long ProcNum;
		///<summary>FK to supply.SupplyNum.</summary>
		public long SupplyNum;
		///<summary>FK to supplylot.SupplyLotNum. 0: stock without a lot. -1: soonest-expiring first, chosen when it is deducted.</summary>
		public long SupplyLotNum;
		///<summary>Positive quantity used.</summary>
		public double Quantity;

		///<summary>For serialization.</summary>
		public SupplyUsageLine() {
		}

		public SupplyUsageLine(long procNum,long supplyNum,long supplyLotNum,double quantity) {
			ProcNum=procNum;
			SupplyNum=supplyNum;
			SupplyLotNum=supplyLotNum;
			Quantity=quantity;
		}

		public SupplyUsageLine Copy() {
			return (SupplyUsageLine)MemberwiseClone();
		}
	}

	///<summary>One receipt of an order item: quantity, and lot number and expiry date for supplies tracked by lot.</summary>
	[Serializable]
	public class SupplyReceiveLine {
		///<summary>FK to supplyorderitem.SupplyOrderItemNum.</summary>
		public long SupplyOrderItemNum;
		public double Quantity;
		///<summary>Blank for stock without a lot.</summary>
		public string LotNumber="";
		///<summary>MinValue when the lot has no expiry date.</summary>
		public DateTime DateExpiry;
	}

	///<summary>One counted line of a stock count: the supply and lot, the quantity the program had when the line was counted, and the counted
	///quantity.</summary>
	[Serializable]
	public class SupplyCountLine {
		///<summary>FK to supply.SupplyNum.</summary>
		public long SupplyNum;
		///<summary>FK to supplylot.SupplyLotNum. 0 for stock without a lot, or for a lot found in the count (LotNumber set).</summary>
		public long SupplyLotNum;
		///<summary>The lot number, also for a lot found in the count. Blank for stock without a lot.</summary>
		public string LotNumber="";
		///<summary>The expiry date of a lot found in the count. MinValue for none.</summary>
		public DateTime DateExpiry;
		///<summary>What the program had when the line was counted.</summary>
		public double QtyProgram;
		public double QtyCounted;
	}
}
