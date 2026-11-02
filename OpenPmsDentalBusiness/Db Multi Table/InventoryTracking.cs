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
		public const string REASON_MOVED_TO_LOT="Lot number entered: moved from stock without a lot";
		public const string REASON_REPAIRED="Repair: procedure no longer complete";
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

		///<summary>The preference row that turns inventory tracking on (boolean, "0" or "1"). A fork-owned preference, not in PrefName (the registry of
		///the names Open Dental publishes; the "Fork" prefix keeps a later Open Dental name from clashing with it): read with PrefC.GetRawOrDefault,
		///written by SaveSettings. No database has the row until the office saves the settings, and without it tracking is off. While off the
		///Materials used panel never opens, completing a procedure records nothing and nothing is deducted; receipts, counts and manual adjustments
		///still go through the stock ledger.</summary>
		public const string PREF_ENABLED="ForkInventoryTrackingEnabled";
		///<summary>The preference row with the lead days of the Inventory Expiring alert (integer, 30 when the row is missing; 0 turns the alert off).
		///Fork-owned, not in PrefName, like PREF_ENABLED.</summary>
		public const string PREF_EXPIRY_ALERT_DAYS="ForkInventoryExpiryAlertDays";

		///<summary>True when the office turned inventory tracking on (PREF_ENABLED). The preference row exists only once the office saved the settings
		///(SaveSettings); without it tracking is off.</summary>
		public static bool IsEnabled() {
			Meth.NoCheckMiddleTierRole();
			return PIn.Bool(PrefC.GetRawOrDefault(PREF_ENABLED,"0"));
		}

		///<summary>The lead days of the expiring alert (PREF_EXPIRY_ALERT_DAYS); 30 when the row is missing.</summary>
		public static int GetExpiryAlertDays() {
			Meth.NoCheckMiddleTierRole();
			return PIn.Int(PrefC.GetRawOrDefault(PREF_EXPIRY_ALERT_DAYS,"30"));
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
			bool isFirstTime=PIn.Long(Db.GetScalar("SELECT COUNT(*) FROM preference WHERE PrefName='"+POut.String(PREF_ENABLED)+"'"))==0;
			int countGroupsGranted=0;
			if(isEnabled) {
				//Links and grant first, the preference that turns tracking on last.
				AddAlertLinksIfMissing();
				if(isFirstTime) {
					countGroupsGranted=GrantAdjustToSupplierGroups();
				}
			}
			//The shared helper for fork-owned preference rows (B-SERVER): inserts the row the first time, updates it afterwards.
			PrefC.UpsertRawNoCache(PREF_EXPIRY_ALERT_DAYS,POut.Int(Math.Max(0,expiryAlertDays)));
			PrefC.UpsertRawNoCache(PREF_ENABLED,POut.Bool(isEnabled));
			return countGroupsGranted;
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
		///<summary>Called by Procedures.Insert and Procedures.Update (on the server side) after a procedure became complete, its note included. While
		///tracking is on and the code has default materials, records the procedure as awaiting materials. Never deducts, and never throws
		///(RunProcHook): a failure is logged, and the procedure is simply not on the Awaiting Materials list, so nothing is deducted for it, which is
		///never a silent deduction.</summary>
		public static void OnProcSetComplete(Procedure procedure) {
			Meth.NoCheckMiddleTierRole();
			RunProcHook(() => {
				if(!IsEnabled() || procedure==null || procedure.ProcNum==0) {
					return;
				}
				if(!SupplyProcCodes.HasMaterials(procedure.CodeNum)) {
					return;
				}
				SupplyProcPendings.InsertIfMissing(procedure.ProcNum,procedure.PatNum,Security.CurUser?.UserNum??0);
			});
		}

		///<summary>Runs a hook of the procedure save path (OnProcSetComplete, OnProcNoLongerComplete) so that it can never stop a procedure from being
		///saved, deleted or given its note: a fork table that fails (a crashed MyISAM table, a lost connection, a deadlock) also on a database whose
		///office never turned tracking on. A failure is written to the log (folder InventoryTracking) and not thrown. A restore that did not run is
		///put back by the repair (SupplyLedgers.GetProcNumsHeldNotComplete).</summary>
		private static void RunProcHook(Action action) {
			try {
				action();
			}
			catch(Exception ex) {
				ODException.SwallowAnyException(() => Logger.WriteException(ex,"InventoryTracking"));
			}
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

		///<summary>For business methods that complete procedures entirely on the Middle Tier server (Procedures.SetCompleteInApptInList,
		///Appointments.CompleteClick): called in the program after the remote call returns, it raises ProcSetComplete (NotifyProcSetComplete) for
		///every procedure of listProceduresAfter that is complete and was not complete in listProceduresBefore (or was not in it).</summary>
		public static void NotifyProcsNowComplete(List<Procedure> listProceduresBefore,List<Procedure> listProceduresAfter) {
			Meth.NoCheckMiddleTierRole();
			if(listProceduresAfter.IsNullOrEmpty() || !IsEnabled()) {
				return;
			}
			listProceduresBefore=listProceduresBefore??new List<Procedure>();
			foreach(Procedure procedure in listProceduresAfter) {
				if(procedure==null || procedure.ProcStatus!=ProcStat.C) {
					continue;
				}
				if(listProceduresBefore.Any(x => x!=null && x.ProcNum==procedure.ProcNum && x.ProcStatus==ProcStat.C)) {
					continue;
				}
				NotifyProcSetComplete(procedure.ProcNum);
			}
		}

		///<summary>Called by Procedures.Update when a complete procedure gets another status (after its note is written), and by Procedures.Delete
		///(after the deleted status is written): puts back what the procedure still holds (RestoreForProc) and removes it from the awaiting list. Runs
		///also while tracking is off, so stock stays right. Never throws (RunProcHook): a failure is logged, and what was not put back is put back by
		///the repair, which finds procedures no longer complete that still hold materials.</summary>
		public static void OnProcNoLongerComplete(long procNum,bool isDeleted) {
			Meth.NoCheckMiddleTierRole();
			RunProcHook(() => {
				if(procNum==0 || !IsInstalled()) {
					return;
				}
				RestoreForProc(procNum,isDeleted ? REASON_DELETED : REASON_UNCOMPLETED);
				SupplyProcPendings.DeleteForProcs(new List<long> { procNum });
			});
		}
		#endregion Procedure hooks

		#region Materials of procedures
		///<summary>What the procedures hold now, per supply and lot (Use minus Restore rows). Quantities are positive.</summary>
		public static List<SupplyUsageLine> GetProcMaterials(List<long> listProcNums) {
			Meth.NoCheckMiddleTierRole();
			return SupplyLedgers.GetNetUsage(SupplyLedgers.GetForProcs(listProcNums));
		}

		///<summary>The default materials of each procedure's code, for the Materials used panel: one line per material. The lots are filled in
		///soonest-expiring first (split over lots when one lot does not hold enough; lot 0 when there is no lot with stock), taking into account what
		///an earlier procedure of the list takes. With isLotAuto, only implants get their lots that way (an implant's lot is chosen in the panel and
		///recorded with the procedure); every other material gets lot -1, so its lots are chosen soonest-expiring first from the stock as it is when
		///it is deducted (ConfirmMaterials).</summary>
		public static List<SupplyUsageLine> GetDefaultMaterials(List<Procedure> listProcedures,bool isLotAuto=false) {
			Meth.NoCheckMiddleTierRole();
			List<SupplyUsageLine> listSupplyUsageLines=new List<SupplyUsageLine>();
			List<SupplyProcCode> listSupplyProcCodes=SupplyProcCodes.GetForCodes(listProcedures.Select(x => x.CodeNum).Distinct().ToList());
			List<long> listSupplyNums=listSupplyProcCodes.Select(x => x.SupplyNum).Distinct().ToList();
			List<SupplyLot> listSupplyLots=SupplyLots.GetForSupplies(listSupplyNums,isEmptyIncluded:false);
			List<SupplySetting> listSupplySettings=isLotAuto ? SupplySettings.GetForSupplies(listSupplyNums) : new List<SupplySetting>();
			foreach(Procedure procedure in listProcedures) {
				foreach(SupplyProcCode supplyProcCode in listSupplyProcCodes.FindAll(x => x.CodeNum==procedure.CodeNum)) {
					if(isLotAuto && !(listSupplySettings.Find(x => x.SupplyNum==supplyProcCode.SupplyNum)?.IsImplant??false)) {
						listSupplyUsageLines.Add(new SupplyUsageLine(procedure.ProcNum,supplyProcCode.SupplyNum,-1,Math.Round(supplyProcCode.Quantity,2)));
						continue;
					}
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
		///procedure should hold in the end, per supply and lot. SupplyLotNum -1 means soonest-expiring first, chosen here from the lots as they are
		///in the database now (ResolveAutoLines), so a panel that was open while another workstation took the last of a lot never takes it again.
		///For each procedure, the difference to what the ledger already holds for it is written: Use rows for more, Restore rows for less. So
		///confirming again after a failure part-way, or opening the panel again for a procedure, never deducts twice. A procedure with no lines is
		///confirmed with no materials. Procedures that are not complete (another workstation changed them) are skipped and returned, also when that
		///happened while this call ran: what it took for them is put back (RestoreForProc).
		///A lot-tracked supply taken from an explicit lot that holds less: the missing quantity is first moved from the supply's stock without a lot
		///into the lot (two Adjust rows), up to what that stock holds, as when the assistant enters the lot number of an implant that came in as an
		///opening balance. An implant taken without a lot (lot 0) needs reasonImplantWithoutLot, which goes to the audit trail; without it nothing
		///is written and an ApplicationException is thrown.</summary>
		public static List<long> ConfirmMaterials(List<long> listProcNums,List<SupplyUsageLine> listSupplyUsageLines,string reasonImplantWithoutLot="") {
			if(listProcNums.IsNullOrEmpty()) {
				return new List<long>();
			}
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<long>>(MethodBase.GetCurrentMethod(),listProcNums,listSupplyUsageLines,reasonImplantWithoutLot);
			}
			listSupplyUsageLines=listSupplyUsageLines??new List<SupplyUsageLine>();
			List<long> listProcNumsSkipped=new List<long>();
			List<Procedure> listProcedures=Procedures.GetManyProc(listProcNums,false);
			ActionAfterStatusesRead?.Invoke(listProcNums);
			List<SupplyLedger> listSupplyLedgersProcs=SupplyLedgers.GetForProcs(listProcNums);
			List<SupplyUsageLine> listHeldAll=SupplyLedgers.GetNetUsage(listSupplyLedgersProcs);
			List<long> listSupplyNums=listSupplyUsageLines.Select(x => x.SupplyNum).Concat(listSupplyLedgersProcs.Select(x => x.SupplyNum)).Distinct().ToList();
			List<Supply> listSupplies=Supplies.GetMany(listSupplyNums);
			List<SupplySetting> listSupplySettings=SupplySettings.GetForSupplies(listSupplyNums);
			//The lots as they are now. ResolveAutoLines takes each procedure's draws off before the next procedure's lots are chosen.
			List<SupplyLot> listSupplyLots=SupplyLots.GetForSupplies(listSupplyNums,isEmptyIncluded:true);
			Dictionary<long,double> dictQtyWithoutLotDb=new Dictionary<long,double>();
			List<SupplyLedger> listSupplyLedgersNew=new List<SupplyLedger>();
			List<string> listLogTexts=new List<string>();
			List<long> listProcNumsDone=new List<long>();
			foreach(long procNum in listProcNums.Distinct()) {
				Procedure procedure=listProcedures.Find(x => x.ProcNum==procNum);
				if(procedure==null || procedure.ProcStatus!=ProcStat.C) {
					listProcNumsSkipped.Add(procNum);
					continue;
				}
				List<SupplyLedger> listSupplyLedgersProc=listSupplyLedgersProcs.FindAll(x => x.ProcNum==procNum);
				List<SupplyUsageLine> listHeld=listHeldAll.FindAll(x => x.ProcNum==procNum);
				List<SupplyUsageLine> listLinesProc=listSupplyUsageLines.FindAll(x => x.ProcNum==procNum && Math.Round(x.Quantity,2)>0);
				List<SupplyUsageLine> listWanted=listLinesProc.FindAll(x => x.SupplyLotNum>=0).Select(x => x.Copy()).ToList();
				listWanted.AddRange(ResolveAutoLines(new List<long> { procNum },listLinesProc,listHeld,listSupplyLots));
				List<(long SupplyNum,long SupplyLotNum)> listKeys=listWanted.Select(x => (x.SupplyNum,x.SupplyLotNum))
					.Concat(listHeld.Select(x => (x.SupplyNum,x.SupplyLotNum))).Distinct().ToList();
				List<SupplyLedger> listSupplyLedgersMoves=new List<SupplyLedger>();
				List<SupplyLedger> listSupplyLedgersUses=new List<SupplyLedger>();
				foreach((long supplyNum,long supplyLotNum) in listKeys) {
					double qtyWanted=listWanted.Where(x => x.SupplyNum==supplyNum && x.SupplyLotNum==supplyLotNum).Sum(x => x.Quantity);
					double qtyHeld=listHeld.Where(x => x.SupplyNum==supplyNum && x.SupplyLotNum==supplyLotNum).Sum(x => x.Quantity);
					double qtyDiff=Math.Round(qtyWanted-qtyHeld,2);
					if(qtyDiff==0) {
						continue;
					}
					Supply supply=listSupplies.Find(x => x.SupplyNum==supplyNum);
					SupplySetting supplySetting=listSupplySettings.Find(x => x.SupplyNum==supplyNum)??SupplySettings.CreateDefault(supplyNum);
					string textProc=ProcedureCodes.GetStringProcCode(procedure.CodeNum)+" "+procedure.ProcDate.ToShortDateString();
					if(qtyDiff>0 && supplyLotNum==0 && supplySetting.IsImplant) {
						if(string.IsNullOrWhiteSpace(reasonImplantWithoutLot)) {
							throw new ApplicationException(Lans.g("InventoryTracking","The lot number of an implant is recorded with the procedure. Please enter the lot number from the package of")
								+" "+(supply?.Descript??"")+".");
						}
						listLogTexts.Add("Implant deducted without a lot number: "+(supply?.Descript??"")+" x"+FormatQty(qtyDiff)+", procedure "+textProc
							+". Reason: "+reasonImplantWithoutLot.Trim());
					}
					SupplyLot supplyLot=listSupplyLots.Find(x => x.SupplyLotNum==supplyLotNum);
					if(qtyDiff>0 && supplyLotNum>0 && supplySetting.IsLotTracked && supplyLot!=null && Math.Round(supplyLot.QtyOnHand,2)<0) {
						//The lot holds less than is taken from it now (ResolveAutoLines took this procedure's draws off already).
						double qtyShort=Math.Min(qtyDiff,Math.Round(-supplyLot.QtyOnHand,2));
						if(!dictQtyWithoutLotDb.ContainsKey(supplyNum)) {
							dictQtyWithoutLotDb[supplyNum]=SupplyLedgers.GetSumWithoutLot(supplyNum);
						}
						double qtyWithoutLot=dictQtyWithoutLotDb[supplyNum]+listSupplyLedgersNew.Concat(listSupplyLedgersMoves).Concat(listSupplyLedgersUses)
							.Where(x => x.SupplyNum==supplyNum && x.SupplyLotNum==0).Sum(x => x.Quantity);
						double qtyMove=Math.Round(Math.Min(qtyShort,qtyWithoutLot),2);
						if(qtyMove>0) {
							listSupplyLedgersMoves.Add(NewAdjustRow(supply,supplyNum,0,-qtyMove,REASON_MOVED_TO_LOT));
							listSupplyLedgersMoves.Add(NewAdjustRow(supply,supplyNum,supplyLotNum,qtyMove,REASON_MOVED_TO_LOT));
							supplyLot.QtyOnHand=Math.Round(supplyLot.QtyOnHand+qtyMove,2);
							listLogTexts.Add("Stock without a lot moved into lot "+supplyLot.LotNumber+": "+(supply?.Descript??"")+" x"+FormatQty(qtyMove)
								+", for procedure "+textProc+".");
						}
					}
					SupplyLedger supplyLedger=new SupplyLedger();
					supplyLedger.SupplyNum=supplyNum;
					supplyLedger.SupplyLotNum=supplyLotNum;
					supplyLedger.ProcNum=procNum;
					supplyLedger.PatNum=procedure.PatNum;
					supplyLedger.Quantity=-qtyDiff;
					if(qtyDiff>0) {
						supplyLedger.LedgerType=SupplyLedgerType.Use;
						supplyLedger.UnitCost=supply?.Price??0;
						supplyLedger.Reason="";
					}
					else {
						supplyLedger.LedgerType=SupplyLedgerType.Restore;
						supplyLedger.UnitCost=GetUseCost(listSupplyLedgersProc,supplyNum,supplyLotNum,listSupplies);
						supplyLedger.Reason=REASON_CORRECTED;
					}
					listSupplyLedgersUses.Add(supplyLedger);
				}
				//A move into a lot comes before the Use row that takes from it.
				listSupplyLedgersNew.AddRange(listSupplyLedgersMoves);
				listSupplyLedgersNew.AddRange(listSupplyLedgersUses);
				listProcNumsDone.Add(procNum);
			}
			//Ledger first (it recomputes the counters), the awaiting rows last.
			SupplyLedgers.InsertMany(listSupplyLedgersNew);
			//Another workstation may have un-completed or deleted a procedure after its status was read above; its restore then found nothing of
			//this call's rows to put back. Read the statuses again and put back what this call took for such a procedure.
			List<long> listProcNumsWritten=listSupplyLedgersNew.Where(x => x.ProcNum!=0).Select(x => x.ProcNum).Distinct().ToList();
			if(listProcNumsWritten.Count>0) {
				List<Procedure> listProceduresNow=Procedures.GetManyProc(listProcNumsWritten,false);
				foreach(long procNum in listProcNumsWritten) {
					Procedure procedureNow=listProceduresNow.Find(x => x.ProcNum==procNum);
					if(procedureNow!=null && procedureNow.ProcStatus==ProcStat.C) {
						continue;
					}
					RestoreForProc(procNum,procedureNow==null || procedureNow.ProcStatus==ProcStat.D ? REASON_DELETED : REASON_UNCOMPLETED);
					listProcNumsDone.Remove(procNum);
					listProcNumsSkipped.Add(procNum);
				}
			}
			SupplyProcPendings.DeleteForProcs(listProcNumsDone.Concat(listProcNumsSkipped).ToList());
			foreach(string logText in listLogTexts) {
				RunAfterStockWritten(() => SecurityLogs.MakeLogEntry(EnumPermType.InventoryAdjust,0,logText));
			}
			if(IsEnabled()) {
				RunAfterStockWritten(() => RefreshLowStockAlerts(listSupplyLedgersNew.Select(x => x.SupplyNum).Distinct().ToList()));
			}
			return listProcNumsSkipped;
		}

		///<summary>Tests only: runs in ConfirmMaterials right after the procedures' statuses were read, to change them as another workstation would.
		///</summary>
		internal static Action<List<long>> ActionAfterStatusesRead;

		///<summary>An Adjust row of the supply and lot at the supply's price.</summary>
		private static SupplyLedger NewAdjustRow(Supply supply,long supplyNum,long supplyLotNum,double qtyChange,string reason) {
			SupplyLedger supplyLedger=new SupplyLedger();
			supplyLedger.SupplyNum=supplyNum;
			supplyLedger.SupplyLotNum=supplyLotNum;
			supplyLedger.LedgerType=SupplyLedgerType.Adjust;
			supplyLedger.Quantity=qtyChange;
			supplyLedger.UnitCost=supply?.Price??0;
			supplyLedger.Reason=reason;
			return supplyLedger;
		}

		///<summary>Runs a follow-up of a stock write that is already done (audit entries, alerts, received dates). A failure is written to the log
		///(folder InventoryTracking) and never thrown: the caller must not report a failure for stock that was written, because the window would keep
		///what was entered and posting it again would write it twice.</summary>
		private static void RunAfterStockWritten(Action action) {
			try {
				action();
			}
			catch(Exception ex) {
				ODException.SwallowAnyException(() => Logger.WriteException(ex,"InventoryTracking"));
			}
		}

		///<summary>Chooses the lots of the lines of lot -1 (soonest-expiring first), procedure by procedure in the order of listProcNums, and returns
		///the explicit lines that take their place (with ProcNum; the procedures' lines of explicit lots are not returned). Per procedure and supply,
		///the lines of explicit lots are kept as they are and claim first what the procedure holds of their lots. The quantity of the lines of lot -1
		///then keeps what the procedure holds of the supply beyond that; more is taken soonest-expiring first from what the lots hold (less what
		///explicit lines take from them), and the rest without a lot; less gives back from stock without a lot first, then from the lots that expire
		///last. listHeld: what the procedures hold now (GetProcMaterials). listSupplyLots: the supplies' lots with their stock, empty lots included;
		///it is changed as each procedure draws (its explicit lines too), so a later procedure, or a caller, sees what is left. No database access:
		///ConfirmMaterials passes the lots as they are when it runs, the Materials used panel the lots it read, to show the lots it plans.</summary>
		public static List<SupplyUsageLine> ResolveAutoLines(List<long> listProcNums,List<SupplyUsageLine> listLines,List<SupplyUsageLine> listHeld,
			List<SupplyLot> listSupplyLots)
		{
			Meth.NoCheckMiddleTierRole();
			List<SupplyUsageLine> listAuto=new List<SupplyUsageLine>();
			foreach(long procNum in listProcNums.Distinct()) {
				List<SupplyUsageLine> listLinesProc=listLines.FindAll(x => x.ProcNum==procNum && Math.Round(x.Quantity,2)>0);
				List<SupplyUsageLine> listHeldProc=listHeld.FindAll(x => x.ProcNum==procNum && Math.Round(x.Quantity,2)>0);
				List<SupplyUsageLine> listAutoProc=new List<SupplyUsageLine>();
				foreach(long supplyNum in listLinesProc.Where(x => x.SupplyLotNum==-1).Select(x => x.SupplyNum).Distinct()) {
					double qtyAuto=Math.Round(listLinesProc.Where(x => x.SupplyNum==supplyNum && x.SupplyLotNum==-1).Sum(x => x.Quantity),2);
					List<SupplyUsageLine> listExplicit=listLinesProc.FindAll(x => x.SupplyNum==supplyNum && x.SupplyLotNum>=0);
					//What the procedure holds of the supply that its explicit lines do not claim, and what is free in each lot after the explicit lines.
					List<SupplyLot> listLotsFree=listSupplyLots.FindAll(x => x.SupplyNum==supplyNum).Select(x => x.Copy()).ToList();
					List<SupplyUsageLine> listHeldFree=new List<SupplyUsageLine>();
					List<long> listLotNums=listHeldProc.Where(x => x.SupplyNum==supplyNum).Select(x => x.SupplyLotNum)
						.Concat(listExplicit.Select(x => x.SupplyLotNum)).Distinct().ToList();
					foreach(long supplyLotNum in listLotNums) {
						double qtyHeld=Math.Round(listHeldProc.Where(x => x.SupplyNum==supplyNum && x.SupplyLotNum==supplyLotNum).Sum(x => x.Quantity),2);
						double qtyExplicit=Math.Round(listExplicit.Where(x => x.SupplyLotNum==supplyLotNum).Sum(x => x.Quantity),2);
						if(qtyHeld>qtyExplicit) {
							listHeldFree.Add(new SupplyUsageLine(procNum,supplyNum,supplyLotNum,Math.Round(qtyHeld-qtyExplicit,2)));
							continue;
						}
						SupplyLot supplyLotFree=listLotsFree.Find(x => x.SupplyLotNum==supplyLotNum);
						if(supplyLotFree!=null) {
							supplyLotFree.QtyOnHand=Math.Round(supplyLotFree.QtyOnHand-(qtyExplicit-qtyHeld),2);
						}
					}
					double qtyHeldFree=Math.Round(listHeldFree.Sum(x => x.Quantity),2);
					if(qtyAuto>=qtyHeldFree) {
						listAutoProc.AddRange(listHeldFree);
						List<SupplyUsageLine> listAllocated=SupplyLots.AllocateFifo(supplyNum,Math.Round(qtyAuto-qtyHeldFree,2),listLotsFree);
						listAllocated.ForEach(x => x.ProcNum=procNum);
						listAutoProc.AddRange(listAllocated);
						continue;
					}
					//Less than it holds: give back from stock without a lot first, then from the lots that expire last.
					double qtyGiveBack=Math.Round(qtyHeldFree-qtyAuto,2);
					List<long> listLotNumsOrder=new List<long> { 0 };
					listLotNumsOrder.AddRange(SupplyLots.SortFifo(listSupplyLots.FindAll(x => x.SupplyNum==supplyNum)).Select(x => x.SupplyLotNum).Reverse());
					listLotNumsOrder.AddRange(listHeldFree.Select(x => x.SupplyLotNum).Where(x => !listLotNumsOrder.Contains(x)));//Lots not passed in.
					foreach(long supplyLotNum in listLotNumsOrder) {
						foreach(SupplyUsageLine supplyUsageLine in listHeldFree.FindAll(x => x.SupplyLotNum==supplyLotNum)) {
							double qtyTake=Math.Min(qtyGiveBack,supplyUsageLine.Quantity);
							supplyUsageLine.Quantity=Math.Round(supplyUsageLine.Quantity-qtyTake,2);
							qtyGiveBack=Math.Round(qtyGiveBack-qtyTake,2);
						}
					}
					listAutoProc.AddRange(listHeldFree.FindAll(x => x.Quantity>0));
				}
				//What the procedure takes from each lot beyond what it holds (its explicit and its chosen lines) is no longer there for the next one.
				List<SupplyUsageLine> listWantedProc=listLinesProc.FindAll(x => x.SupplyLotNum>=0).Concat(listAutoProc).ToList();
				foreach(SupplyLot supplyLot in listSupplyLots) {
					double qtyWanted=listWantedProc.Where(x => x.SupplyLotNum==supplyLot.SupplyLotNum).Sum(x => x.Quantity);
					double qtyHeld=listHeldProc.Where(x => x.SupplyLotNum==supplyLot.SupplyLotNum).Sum(x => x.Quantity);
					supplyLot.QtyOnHand=Math.Round(supplyLot.QtyOnHand-(qtyWanted-qtyHeld),2);
				}
				listAuto.AddRange(listAutoProc);
			}
			return listAuto;
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
		///taken from and its cost. Writes nothing when it holds nothing. Each row is written only while the procedure still holds exactly that
		///(SupplyLedgers.InsertRestoresIfStillHeld), so two workstations putting back the same procedure at the same time put it back once.</summary>
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
			List<SupplyLedger> listSupplyLedgersWritten=SupplyLedgers.InsertRestoresIfStillHeld(listSupplyLedgersNew);
			if(IsEnabled()) {
				//Never a failure of the caller (Procedures.Update and Delete have written the procedure already).
				RunAfterStockWritten(() => RefreshLowStockAlerts(listSupplyLedgersWritten.Select(x => x.SupplyNum).Distinct().ToList()));
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
			//Stock is written. What follows (received dates, the audit entry, the alerts) can still fail, but must never make this call report a
			//failure: the window would keep what was entered, and posting it again would receive it twice. A failure is logged; a date that was not
			//set is set by the next receipt of the order, an alert by the next alert check.
			RunAfterStockWritten(() => SetReceivedDatesOfItems(listSupplyOrderItems));
			foreach(long supplyOrderNum in listSupplyOrderItems.Select(x => x.SupplyOrderNum).Distinct()) {
				RunAfterStockWritten(() => FinishOrderReceipt(supplyOrderNum,listSupplyLedgers));
			}
			if(IsEnabled()) {
				RunAfterStockWritten(() => RefreshLowStockAlerts(listSupplyLedgers.Select(x => x.SupplyNum).Distinct().ToList()));
			}
			return listSupplyLedgers;
		}

		///<summary>Sets the received date of each order item that is now fully received, from every receipt of the items (other workstations' too).
		///</summary>
		private static void SetReceivedDatesOfItems(List<SupplyOrderItem> listSupplyOrderItems) {
			List<SupplyLedger> listSupplyLedgersReceived=SupplyLedgers.GetReceiptsForOrderItems(listSupplyOrderItems.Select(x => x.SupplyOrderItemNum).ToList());
			foreach(SupplyOrderItem supplyOrderItem in listSupplyOrderItems) {
				double qtyReceived=listSupplyLedgersReceived.Where(x => x.SupplyOrderItemNum==supplyOrderItem.SupplyOrderItemNum).Sum(x => x.Quantity);
				if(supplyOrderItem.DateReceived.Year<1880 && Math.Round(qtyReceived,2)>=supplyOrderItem.Qty) {
					supplyOrderItem.DateReceived=DateTime.Today;
					SupplyOrderItems.Update(supplyOrderItem);
				}
			}
		}

		///<summary>The Supply Purchases audit entry of a receipt of the order, then the order's received date when every item is received.</summary>
		private static void FinishOrderReceipt(long supplyOrderNum,List<SupplyLedger> listSupplyLedgers) {
			SupplyOrder supplyOrder=SupplyOrders.GetOne(supplyOrderNum);
			if(supplyOrder==null) {
				return;
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

		///<summary>Posts a stock count: for every line whose counted quantity differs from the quantity the program had when the line was counted, a
		///CountAdjust row of the difference, so that procedures completed during the count are not counted twice. A line with a lot number and no
		///SupplyLotNum is a lot found in the count: it is found or created. Writes an Inventory Adjust audit entry per adjusted line. The reason is
		///required. Returns the number of adjusted lines. The window posts the office's saved count through PostOpenCount, which calls this.</summary>
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
			//Stock is written: the audit entries and alerts never make this call report a failure (posting again would adjust twice).
			foreach(string logText in listLogTexts) {
				RunAfterStockWritten(() => SecurityLogs.MakeLogEntry(EnumPermType.InventoryAdjust,0,logText));
			}
			if(IsEnabled()) {
				RunAfterStockWritten(() => RefreshLowStockAlerts(listSupplyLedgers.Select(x => x.SupplyNum).Distinct().ToList()));
			}
			return listSupplyLedgers.Count;
		}

		///<summary>The program's quantity of a stock count line: the stock of the lot (supplyLotNum>0) or of the supply's stock without a lot (0), from
		///the ledger, never from the counters. A counter is brought up to date only at the end of a stock write (SupplyLedgers.InsertMany), so after a
		///write that failed part-way it can still hold the quantity from before; a count compared with it would write the same difference twice.
		///Rounded to 2 decimals.</summary>
		public static double GetQtyProgram(long supplyNum,long supplyLotNum) {
			Meth.NoCheckMiddleTierRole();
			if(supplyLotNum>0) {
				return Math.Round(SupplyLedgers.GetSumForLot(supplyLotNum),2);
			}
			return Math.Round(SupplyLedgers.GetSumWithoutLot(supplyNum),2);
		}

		///<summary>The lines a stock count starts with for the supply, each with the program's quantity from the ledger (GetQtyProgram's rule): one per
		///lot whose stock is not 0, negative lots too (a lot taken past its stock, a lot number entered from the package, two workstations taking the
		///last unit), so that counting it corrects it; soonest-expiring first. Then one line for the stock without a lot when there is some, or when
		///the supply is not tracked by lot, or when no lot has a line. QtyCounted is 0.</summary>
		public static List<SupplyCountLine> GetCountLines(long supplyNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<SupplyCountLine>>(MethodBase.GetCurrentMethod(),supplyNum);
			}
			bool isLotTracked=SupplySettings.GetForSupply(supplyNum).IsLotTracked;
			SerializableDictionary<long,double> dictQtyByLot=SupplyLedgers.GetSumsByLot(supplyNum);
			List<SupplyCountLine> listSupplyCountLines=new List<SupplyCountLine>();
			foreach(SupplyLot supplyLot in SupplyLots.GetForSupply(supplyNum,isEmptyIncluded:true)) {
				double qtyLot=Math.Round(dictQtyByLot.TryGetValue(supplyLot.SupplyLotNum,out double qty) ? qty : 0,2);
				if(qtyLot==0) {
					continue;
				}
				listSupplyCountLines.Add(new SupplyCountLine { SupplyNum=supplyNum,SupplyLotNum=supplyLot.SupplyLotNum,LotNumber=supplyLot.LotNumber,
					DateExpiry=supplyLot.DateExpiry,QtyProgram=qtyLot });
			}
			double qtyWithoutLot=Math.Round(dictQtyByLot.TryGetValue(0,out double qtyNoLot) ? qtyNoLot : 0,2);
			if(qtyWithoutLot!=0 || !isLotTracked || listSupplyCountLines.Count==0) {
				listSupplyCountLines.Add(new SupplyCountLine { SupplyNum=supplyNum,QtyProgram=qtyWithoutLot });
			}
			return listSupplyCountLines;
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
			//Stock is written: the audit entry and alerts never make this call report a failure (saving again would adjust twice).
			RunAfterStockWritten(() => SecurityLogs.MakeLogEntry(EnumPermType.InventoryAdjust,0,"Stock adjusted: "+supply.Descript
				+(lotNumber=="" ? "" : ", lot "+lotNumber)+" by "+FormatQtySigned(qtyChange)+". Reason: "+reason.Trim()));
			if(IsEnabled()) {
				RunAfterStockWritten(() => RefreshLowStockAlerts(new List<long> { supplyNum }));
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
			RunAfterStockWritten(() => RefreshLowStockAlerts(new List<long> { dispSupply.SupplyNum }));
		}

		///<summary>The repair. First puts back what procedures that are no longer complete (or no longer exist) still hold
		///(SupplyLedgers.GetProcNumsHeldNotComplete, RestoreForProc): left by a failure between a procedure's status write and its restore, or by
		///another workstation un-completing a procedure while its materials were confirmed. Then removes the awaiting rows of procedures that are
		///no longer complete, recomputes every supply's and lot's stock from the ledger (SupplyLedgers.RecomputeAll), writes an Inventory Adjust audit
		///entry and refreshes the alerts. Safe at any time.</summary>
		public static SupplyRepairResult Repair() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<SupplyRepairResult>(MethodBase.GetCurrentMethod());
			}
			List<long> listProcNums=SupplyLedgers.GetProcNumsHeldNotComplete();
			foreach(long procNum in listProcNums) {
				RestoreForProc(procNum,REASON_REPAIRED);
			}
			long countAwaiting=SupplyProcPendings.DeleteNotComplete();
			SupplyRepairResult supplyRepairResult=SupplyLedgers.RecomputeAll();
			supplyRepairResult.CountProcs=listProcNums.Count;
			supplyRepairResult.CountAwaiting=countAwaiting;
			SecurityLogs.MakeLogEntry(EnumPermType.InventoryAdjust,0,"Stock repaired from the ledger: "+supplyRepairResult.CountSupplies+" supply(ies) and "
				+supplyRepairResult.CountLots+" lot(s) changed, "+supplyRepairResult.CountProcs+" procedure(s) no longer complete put back"
				+(listProcNums.Count==0 ? "" : " (ProcNum "+string.Join(", ",listProcNums)+")")+", "+countAwaiting+" awaiting row(s) removed.");
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

		#region Saved stock counts
		///<summary>A count that is Posting longer than this many minutes after its post began is taken for a post that did not finish (a crash, a lost
		///connection, a power cut), and ResumeCount may open it again. Before that another workstation may still be posting it.</summary>
		public const int MINUTES_POST_STALE=5;

		///<summary>The office's open count (Open or Posting), or null when none is open (docs/specs/inventory-tracking.md, 2.7).</summary>
		public static SupplyCount GetOpenCount() {
			Meth.NoCheckMiddleTierRole();
			return SupplyCounts.GetOpen();
		}

		///<summary>What Supply Inventory shows about the open count: who started it, when, how many lines, and whether its post runs or did not
		///finish. Null when no count is open.</summary>
		public static string GetOpenCountDescription() {
			Meth.NoCheckMiddleTierRole();
			SupplyCount supplyCount=SupplyCounts.GetOpen();
			if(supplyCount==null) {
				return null;
			}
			long countLines=SupplyCountLines.GetCountForCount(supplyCount.SupplyCountNum);
			string text=Lans.g("InventoryTracking","Stock count open: started by")+" "+Userods.GetName(supplyCount.UserNum)+" "+Lans.g("InventoryTracking","on")+" "
				+supplyCount.DateTimeEntry.ToShortDateString()+" "+supplyCount.DateTimeEntry.ToShortTimeString()+", "+countLines+" "+Lans.g("InventoryTracking","line(s).");
			if(supplyCount.CountStatus==SupplyCountStatus.Posting) {
				if(SupplyCounts.IsPostingOlderThan(supplyCount.SupplyCountNum,MINUTES_POST_STALE)) {
					text+=" "+Lans.g("InventoryTracking","Its post did not finish: resume it to post it again or to discard it.");
				}
				else {
					text+=" "+Lans.g("InventoryTracking","It is being posted now.");
				}
			}
			return text;
		}

		///<summary>The count's saved lines, in the order they were first counted, with the lot number and expiry date of existing lots filled in from
		///supplylot.</summary>
		public static List<SupplyCountLine> GetSavedCountLines(long supplyCountNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<SupplyCountLine>>(MethodBase.GetCurrentMethod(),supplyCountNum);
			}
			List<SupplyCountLine> listSupplyCountLines=SupplyCountLines.GetForCount(supplyCountNum);
			FillLotsOfLines(listSupplyCountLines);
			return listSupplyCountLines;
		}

		///<summary>Saved lines keep the lot number and expiry date only of a lot found in the count; an existing lot's are read from supplylot.</summary>
		private static void FillLotsOfLines(List<SupplyCountLine> listSupplyCountLines) {
			List<SupplyLot> listSupplyLots=SupplyLots.GetMany(listSupplyCountLines.Where(x => x.SupplyLotNum>0).Select(x => x.SupplyLotNum).Distinct().ToList());
			foreach(SupplyCountLine supplyCountLine in listSupplyCountLines.Where(x => x.SupplyLotNum>0)) {
				SupplyLot supplyLot=listSupplyLots.Find(x => x.SupplyLotNum==supplyCountLine.SupplyLotNum);
				if(supplyLot!=null) {
					supplyCountLine.LotNumber=supplyLot.LotNumber;
					supplyCountLine.DateExpiry=supplyLot.DateExpiry;
				}
			}
		}

		///<summary>The supply's lot with this lot number (ignoring case and blanks around it), or null.</summary>
		private static SupplyLot FindLotByNumber(long supplyNum,string lotNumber) {
			return SupplyLots.GetForSupply(supplyNum,isEmptyIncluded:true)
				.Find(x => string.Equals((x.LotNumber??"").Trim(),(lotNumber??"").Trim(),StringComparison.OrdinalIgnoreCase));
		}

		///<summary>Saves one counted line into the office's open count as soon as it is counted, starting a count when none is open
		///(SupplyCounts.InsertOpenIfNone; two workstations starting at once share one). The line is the supply and its lot (SupplyLotNum), or a lot
		///found in the count (SupplyLotNum 0 and LotNumber, with DateExpiry), or stock without a lot. A lot found in the count whose number is a lot of
		///the supply by now is saved as that lot. The line gets a new snapshot of the program's quantity, read from the ledger now (GetQtyProgram;
		///0 for a lot found in the count), and replaces the count's earlier line of the same supply and lot; QtyCounted replaces its counted
		///quantity, or with isAdded (a scan) is added to it in the same statement (SupplyCountLines.Upsert). Throws when the count is being posted or
		///was closed by another workstation meanwhile: the line is then not saved, and the window says so. Returns the line as saved, with the lot
		///number and expiry date of an existing lot filled in.</summary>
		public static SupplyCountLine SaveCountLine(SupplyCountLine supplyCountLine,bool isAdded) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<SupplyCountLine>(MethodBase.GetCurrentMethod(),supplyCountLine,isAdded);
			}
			double qtyCounted=Math.Round(supplyCountLine.QtyCounted,2);
			if(qtyCounted<0 || (isAdded && qtyCounted==0)) {
				throw new ApplicationException(Lans.g("InventoryTracking","Please enter a quantity of 0 or more."));
			}
			Supply supply=Supplies.GetSupply(supplyCountLine.SupplyNum);
			if(supply==null) {
				throw new ApplicationException(Lans.g("InventoryTracking","The supply was deleted by another user."));
			}
			long userNum=Security.CurUser?.UserNum??0;
			SupplyCount supplyCount=SupplyCounts.InsertOpenIfNone(userNum);
			if(supplyCount==null) {
				throw new ApplicationException(Lans.g("InventoryTracking","The stock count is being posted, or its post did not finish. Open Supply Inventory to "
					+"resume it."));
			}
			SupplyCountLine supplyCountLineSave=new SupplyCountLine();
			supplyCountLineSave.SupplyCountNum=supplyCount.SupplyCountNum;
			supplyCountLineSave.SupplyNum=supply.SupplyNum;
			supplyCountLineSave.SupplyLotNum=Math.Max(0,supplyCountLine.SupplyLotNum);
			supplyCountLineSave.LotNumber="";
			supplyCountLineSave.DateExpiry=DateTime.MinValue;
			string lotNumber=(supplyCountLine.LotNumber??"").Trim();
			if(supplyCountLineSave.SupplyLotNum==0 && lotNumber!="") {
				SupplyLot supplyLotFound=FindLotByNumber(supply.SupplyNum,lotNumber);
				if(supplyLotFound==null) {
					supplyCountLineSave.LotNumber=lotNumber;
					supplyCountLineSave.DateExpiry=supplyCountLine.DateExpiry.Year>1880 ? supplyCountLine.DateExpiry.Date : DateTime.MinValue;
				}
				else {
					supplyCountLineSave.SupplyLotNum=supplyLotFound.SupplyLotNum;
				}
			}
			if(supplyCountLineSave.SupplyLotNum>0) {
				SupplyLot supplyLot=SupplyLots.GetMany(new List<long> { supplyCountLineSave.SupplyLotNum }).FirstOrDefault();
				if(supplyLot==null || supplyLot.SupplyNum!=supply.SupplyNum) {
					throw new ApplicationException(Lans.g("InventoryTracking","The lot is not a lot of this supply."));
				}
				if(!string.IsNullOrWhiteSpace(supplyLot.LotNumber)) {
					//The lot's earlier line from when it was a lot found in the count (it was received since) becomes this lot's line.
					SupplyCountLines.MoveLotFound(supplyCount.SupplyCountNum,supply.SupplyNum,supplyLot.LotNumber,supplyLot.SupplyLotNum,SupplyCountStatus.Open);
				}
			}
			supplyCountLineSave.QtyProgram=supplyCountLineSave.IsLotFound() ? 0 : GetQtyProgram(supply.SupplyNum,supplyCountLineSave.SupplyLotNum);
			supplyCountLineSave.QtyCounted=qtyCounted;
			supplyCountLineSave.UserNum=userNum;
			long countRows=SupplyCountLines.Upsert(supplyCountLineSave,isAdded);
			if(countRows==0 && SupplyCounts.GetOne(supplyCount.SupplyCountNum)?.CountStatus!=SupplyCountStatus.Open) {
				throw new ApplicationException(Lans.g("InventoryTracking","The stock count was posted or discarded by another user, or is being posted. "
					+"This line was not saved."));
			}
			SupplyCountLine supplyCountLineSaved=SupplyCountLines.GetByKey(supplyCount.SupplyCountNum,supply.SupplyNum,supplyCountLineSave.SupplyLotNum,
				supplyCountLineSave.LotNumber);
			if(supplyCountLineSaved==null) {
				throw new ApplicationException(Lans.g("InventoryTracking","Another user removed this line at the same moment. Count it again."));
			}
			FillLotsOfLines(new List<SupplyCountLine> { supplyCountLineSaved });
			return supplyCountLineSaved;
		}

		///<summary>Removes a saved line from the open count (Remove, or a counted quantity cleared). Nothing happens when another workstation removed it
		///already. Throws when its count is no longer open.</summary>
		public static void DeleteCountLine(long supplyCountLineNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),supplyCountLineNum);
				return;
			}
			if(SupplyCountLines.DeleteWhileOpen(supplyCountLineNum)>0) {
				return;
			}
			if(SupplyCountLines.GetOne(supplyCountLineNum)!=null) {
				throw new ApplicationException(Lans.g("InventoryTracking","The stock count was posted or discarded by another user, or is being posted."));
			}
		}

		///<summary>Posts the open count (Inventory Adjust; the reason is required), exactly as an unsaved count is posted (PostCount: a CountAdjust row
		///of the difference to each line's snapshot, an Inventory Adjust audit entry per adjusted line), and closes it. First marks the count Posting in
		///one UPDATE ... WHERE CountStatus=Open, so of two workstations posting it only one does, and no line can be saved into it any more; then reads
		///its lines, writes the adjustments and marks it Posted. When the adjustments fail, the count is opened again with each line's program
		///quantity read again from the ledger, so posting again writes only what still differs; if even that fails, the count stays Posting and is
		///resumed later (ResumeCount). Returns the number of adjusted lines.</summary>
		public static int PostOpenCount(long supplyCountNum,string reason) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetInt(MethodBase.GetCurrentMethod(),supplyCountNum,reason);
			}
			if(string.IsNullOrWhiteSpace(reason)) {
				throw new ApplicationException(Lans.g("InventoryTracking","A reason is required."));
			}
			if(SupplyCountLines.GetCountForCount(supplyCountNum)==0) {
				throw new ApplicationException(Lans.g("InventoryTracking","Nothing is counted yet."));
			}
			long userNum=Security.CurUser?.UserNum??0;
			if(!SupplyCounts.SetStatus(supplyCountNum,SupplyCountStatus.Open,SupplyCountStatus.Posting,userNum,reason)) {
				throw new ApplicationException(Lans.g("InventoryTracking","The stock count was posted or discarded by another user, or is being posted."));
			}
			bool isLedgerWriteStarted=false;
			int countAdjusted;
			try {
				List<SupplyCountLine> listSupplyCountLines=GetSavedCountLines(supplyCountNum);//Every line: none can be saved into it now.
				isLedgerWriteStarted=true;
				countAdjusted=PostCount(listSupplyCountLines,reason);
				ActionAfterCountPosted?.Invoke(supplyCountNum);
			}
			catch(Exception) {
				try {
					ReopenAfterFailedPost(supplyCountNum,isLedgerWriteStarted);
				}
				catch(Exception exReopen) {
					//The count stays Posting; ResumeCount opens it again later. The caller gets the post's own failure.
					ODException.SwallowAnyException(() => Logger.WriteException(exReopen,"InventoryTracking"));
				}
				throw;
			}
			//Stock is written: closing the count never makes this call report a failure (posting again would adjust twice). A count this leaves
			//Posting is resumed later, which reads every line's program quantity again, so posting it then writes nothing more.
			RunAfterStockWritten(() => SupplyCounts.SetPosted(supplyCountNum));
			return countAdjusted;
		}

		///<summary>Tests only: runs in PostOpenCount right after the count's adjustments were written, to fail there as a lost connection or a crash
		///would.</summary>
		internal static Action<long> ActionAfterCountPosted;

		///<summary>Opens a Posting count again after a post that failed or did not finish. When the post may have written ledger rows
		///(isLedgerTouched), each line's snapshot is first read again from the ledger (GetQtyProgram), never from the counters, and a lot found in the
		///count that the post created becomes that lot's line, so posting again writes only what still differs. The count stays Posting when this
		///fails part-way.</summary>
		private static void ReopenAfterFailedPost(long supplyCountNum,bool isLedgerTouched) {
			if(isLedgerTouched) {
				foreach(SupplyCountLine supplyCountLine in SupplyCountLines.GetForCount(supplyCountNum).Where(x => x.IsLotFound())) {
					SupplyLot supplyLot=FindLotByNumber(supplyCountLine.SupplyNum,supplyCountLine.LotNumber);
					if(supplyLot!=null) {
						SupplyCountLines.MoveLotFound(supplyCountNum,supplyCountLine.SupplyNum,supplyCountLine.LotNumber,supplyLot.SupplyLotNum,SupplyCountStatus.Posting);
					}
				}
				foreach(SupplyCountLine supplyCountLine in SupplyCountLines.GetForCount(supplyCountNum).Where(x => !x.IsLotFound())) {
					SupplyCountLines.UpdateQtyProgramWhilePosting(supplyCountLine.SupplyCountLineNum,GetQtyProgram(supplyCountLine.SupplyNum,supplyCountLine.SupplyLotNum));
				}
			}
			SupplyCounts.SetStatus(supplyCountNum,SupplyCountStatus.Posting,SupplyCountStatus.Open,0,"");
		}

		///<summary>Resumes the open count: returns it when it is Open. A count that is Posting longer than MINUTES_POST_STALE (a post that did not
		///finish) is opened again first, with each line's program quantity read again from the ledger (ReopenAfterFailedPost), and an Inventory Adjust
		///audit entry. Throws when the count is no longer open, or while its post may still be running.</summary>
		public static SupplyCount ResumeCount(long supplyCountNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<SupplyCount>(MethodBase.GetCurrentMethod(),supplyCountNum);
			}
			SupplyCount supplyCount=SupplyCounts.GetOne(supplyCountNum);
			if(supplyCount==null || !supplyCount.IsOpenOrPosting()) {
				throw new ApplicationException(Lans.g("InventoryTracking","The stock count was posted or discarded by another user."));
			}
			if(supplyCount.CountStatus==SupplyCountStatus.Open) {
				return supplyCount;
			}
			if(!SupplyCounts.IsPostingOlderThan(supplyCountNum,MINUTES_POST_STALE)) {
				throw new ApplicationException(Lans.g("InventoryTracking","The stock count is being posted by")+" "+Userods.GetName(supplyCount.UserNumClosed)+". "
					+Lans.g("InventoryTracking","If that post did not finish, the count can be resumed")+" "+MINUTES_POST_STALE+" "
					+Lans.g("InventoryTracking","minutes after it began."));
			}
			ReopenAfterFailedPost(supplyCountNum,isLedgerTouched:true);
			RunAfterStockWritten(() => SecurityLogs.MakeLogEntry(EnumPermType.InventoryAdjust,0,"Stock count resumed after a post that did not finish (begun by "
				+Userods.GetName(supplyCount.UserNumClosed)+" on "+supplyCount.DateTimeClosed.ToShortDateString()+" "+supplyCount.DateTimeClosed.ToShortTimeString()
				+"): each line's program quantity read again from the ledger (SupplyCountNum "+supplyCountNum+")."));
			return SupplyCounts.GetOne(supplyCountNum);
		}

		///<summary>Discards the open count (Inventory Adjust, after a confirmation): closes it as Discarded in one UPDATE ... WHERE CountStatus=Open,
		///changes no stock, and writes an Inventory Adjust audit entry with who started it, when and how many lines it had. Its lines are kept with
		///it. Throws when it is no longer Open.</summary>
		public static void DiscardOpenCount(long supplyCountNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),supplyCountNum);
				return;
			}
			SupplyCount supplyCount=SupplyCounts.GetOne(supplyCountNum);
			long countLines=SupplyCountLines.GetCountForCount(supplyCountNum);
			if(supplyCount==null
				|| !SupplyCounts.SetStatus(supplyCountNum,SupplyCountStatus.Open,SupplyCountStatus.Discarded,Security.CurUser?.UserNum??0,""))
			{
				throw new ApplicationException(Lans.g("InventoryTracking","The stock count was posted or discarded by another user, or is being posted."));
			}
			//The count is closed: the audit entry never makes this call report a failure (the window would offer to discard it again).
			RunAfterStockWritten(() => SecurityLogs.MakeLogEntry(EnumPermType.InventoryAdjust,0,"Stock count discarded: started by "+Userods.GetName(supplyCount.UserNum)
				+" on "+supplyCount.DateTimeEntry.ToShortDateString()+" "+supplyCount.DateTimeEntry.ToShortTimeString()+", "+countLines
				+" line(s) counted; stock not changed (SupplyCountNum "+supplyCountNum+")."));
		}
		#endregion Saved stock counts

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
			//With random primary keys (replication) the key is taken from ReplicationServers.GetKey, as the generated CRUD does, never from
			//AUTO_INCREMENT: two replicated servers would hand out the same next key, and the duplicate key would stop replication.
			long countAdded=Db.NonQ("INSERT INTO alertitem ("+(PrefC.RandomKeys ? "AlertItemNum," : "")
				+"ClinicNum,Description,Type,Severity,Actions,FormToOpen,FKey,ItemValue,UserNum,SecDateTEntry) "
				+"SELECT "+(PrefC.RandomKeys ? POut.Long(ReplicationServers.GetKey("alertitem","AlertItemNum"))+"," : "")
				+"-1,'"+POut.String(description)+"',"+(int)alertType+","+(int)SeverityType.Medium+","
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
}
