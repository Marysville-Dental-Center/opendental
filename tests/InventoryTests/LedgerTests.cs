// The stock ledger and LevelOnHand: every movement is a ledger row, LevelOnHand and lot quantities are the ledger sums, a stale copy of a supply
// never overwrites stock, a recompute that did not run is corrected by the next movement, and the repair.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace InventoryTests {
	[TestClass]
	public class LedgerTests {
		[TestInitialize]
		public void TestInitialize() {
			InvDb.Begin();
		}

		[TestMethod]
		public void Insert_TypedOnHandBecomesAnOpeningRow() {
			Supply supply=InvData.Supply(onHand:12.5,price:3);
			List<SupplyLedger> listSupplyLedgers=SupplyLedgers.GetForSupply(supply.SupplyNum);
			Assert.AreEqual(1,listSupplyLedgers.Count);
			Assert.AreEqual(SupplyLedgerType.Opening,listSupplyLedgers[0].LedgerType);
			Assert.AreEqual(12.5,listSupplyLedgers[0].Quantity);
			Assert.AreEqual(3,listSupplyLedgers[0].UnitCost);
			Assert.AreEqual(Security.CurUser.UserNum,listSupplyLedgers[0].UserNum);
			Assert.AreEqual(12.5,InvData.OnHand(supply.SupplyNum));
			Assert.AreEqual(0,InvData.OnHand(InvData.Supply(onHand:0).SupplyNum));
			Assert.AreEqual(0,SupplyLedgers.GetForSupply(InvData.Supply().SupplyNum).Count,"No row for a supply without stock.");
		}

		[TestMethod]
		public void EveryMovement_KeepsLevelOnHandEqualToTheLedgerSum() {
			Supply supply=InvData.Supply(onHand:10);
			InventoryTracking.Adjust(supply.SupplyNum,0,-2.25,"Broken");
			InventoryTracking.Adjust(supply.SupplyNum,0,5,"Found");
			SupplyLot supplyLot=InvData.Lot(supply,"L-"+InvData.Next(),DateTime.Today.AddYears(1),4);
			InventoryTracking.Adjust(supply.SupplyNum,supplyLot.SupplyLotNum,-1,"Dropped");
			Assert.AreEqual(15.75,InvData.OnHand(supply.SupplyNum));
			Assert.AreEqual(Math.Round(SupplyLedgers.GetSumForSupply(supply.SupplyNum),2),InvData.OnHand(supply.SupplyNum));
			Assert.AreEqual(3,InvData.LotQty(supplyLot.SupplyLotNum));
			Assert.AreEqual(12.75,Math.Round(SupplyLedgers.GetSumWithoutLot(supply.SupplyNum),2));
			Assert.AreEqual(5,SupplyLedgers.GetForSupply(supply.SupplyNum).Count);
		}

		[TestMethod]
		public void Adjust_NeedsAReasonAndWritesAnAuditEntry() {
			Supply supply=InvData.Supply(onHand:3);
			InvData.Throws<ApplicationException>(() => InventoryTracking.Adjust(supply.SupplyNum,0,1," "));
			Assert.AreEqual(3,InvData.OnHand(supply.SupplyNum));
			InventoryTracking.Adjust(supply.SupplyNum,0,-1,"Expired in the drawer");
			Assert.AreEqual(1,InvData.CountLogs(EnumPermType.InventoryAdjust,supply.Descript+" by -1. Reason: Expired in the drawer"));
			SupplyLedger supplyLedger=SupplyLedgers.GetForSupply(supply.SupplyNum).Last();
			Assert.AreEqual(SupplyLedgerType.Adjust,supplyLedger.LedgerType);
			Assert.AreEqual("Expired in the drawer",supplyLedger.Reason);
		}

		[TestMethod]
		public void SetOnHand_WritesTheDifferenceAsAnAdjustment() {
			Supply supply=InvData.Supply(onHand:4);
			InventoryTracking.SetOnHand(supply.SupplyNum,9);
			Assert.AreEqual(9,InvData.OnHand(supply.SupplyNum));
			SupplyLedger supplyLedger=SupplyLedgers.GetForSupply(supply.SupplyNum).Last();
			Assert.AreEqual(5,supplyLedger.Quantity);
			Assert.AreEqual(InventoryTracking.REASON_ON_HAND_EDITED,supplyLedger.Reason);
			InventoryTracking.SetOnHand(supply.SupplyNum,9);
			Assert.AreEqual(2,SupplyLedgers.GetForSupply(supply.SupplyNum).Count,"No row when nothing changed.");
		}

		///<summary>A window that loaded the supply before another workstation used some, then saves an edit: LevelOnHand is never written by Update.
		///</summary>
		[TestMethod]
		public void Update_FromAStaleCopy_NeverOverwritesStock() {
			Supply supply=InvData.Supply(onHand:10);
			Supply supplyStale=Supplies.GetSupply(supply.SupplyNum);
			InventoryTracking.Adjust(supply.SupplyNum,0,-3,"Used elsewhere");
			supplyStale.Descript+=" (renamed)";
			supplyStale.LevelOnHand=10;
			Supplies.Update(supplyStale);
			Supply supplyDb=Supplies.GetSupply(supply.SupplyNum);
			Assert.AreEqual(supplyStale.Descript,supplyDb.Descript);
			Assert.AreEqual(7,InvData.OnHand(supply.SupplyNum));
		}

		///<summary>A ledger row whose recompute never ran (the connection was lost between the two statements): the next movement of the supply sets
		///the right total, because counters are recomputed from the ledger, not incremented.</summary>
		[TestMethod]
		public void MissedRecompute_IsCorrectedByTheNextMovement() {
			Supply supply=InvData.Supply(onHand:10);
			DataCore.NonQ("INSERT INTO supplyledger (SupplyNum,SupplyLotNum,LedgerType,Quantity,UnitCost,ProcNum,PatNum,SupplyOrderItemNum,DispSupplyNum,"
				+"UserNum,DateTimeEntry,Reason) VALUES ("+supply.SupplyNum+",0,"+(int)SupplyLedgerType.Adjust+",-4,0,0,0,0,0,1,NOW(),'Half written')");
			Assert.AreEqual(10,InvData.OnHand(supply.SupplyNum),"The counter missed the row.");
			InventoryTracking.Adjust(supply.SupplyNum,0,-1,"Next movement");
			Assert.AreEqual(5,InvData.OnHand(supply.SupplyNum));
		}

		[TestMethod]
		public void Repair_SetsEveryCounterToItsLedgerSum() {
			Supply supply=InvData.Supply(onHand:8);
			SupplyLot supplyLot=InvData.Lot(supply,"R-"+InvData.Next(),DateTime.Today.AddMonths(6),2);
			Supply supplyOk=InvData.Supply(onHand:1);
			SupplyLedgers.RecomputeAll();//Other tests' rows are right already; start from a clean state.
			DataCore.NonQ("UPDATE supply SET LevelOnHand=99 WHERE SupplyNum="+supply.SupplyNum);
			DataCore.NonQ("UPDATE supplylot SET QtyOnHand=-5 WHERE SupplyLotNum="+supplyLot.SupplyLotNum);
			SupplyRepairResult supplyRepairResult=InventoryTracking.Repair();
			Assert.AreEqual(1,supplyRepairResult.CountSupplies);
			Assert.AreEqual(1,supplyRepairResult.CountLots);
			Assert.AreEqual(10,InvData.OnHand(supply.SupplyNum));
			Assert.AreEqual(2,InvData.LotQty(supplyLot.SupplyLotNum));
			Assert.AreEqual(1,InvData.OnHand(supplyOk.SupplyNum));
			Assert.IsTrue(InvData.CountLogs(EnumPermType.InventoryAdjust,"Stock repaired from the ledger: 1 supply(ies) and 1 lot(s) changed, ")>=1);
			Assert.AreEqual(0,SupplyLedgers.RecomputeAll().CountSupplies,"Nothing left to repair.");
		}

		[TestMethod]
		public void Delete_SupplyWithHistoryIsRefused_WithoutHistoryRemovesItsRows() {
			Supply supplyUsed=InvData.Supply(onHand:5);
			long codeNum=InvData.Code((supplyUsed,1));
			Procedure procedure=InvData.SetStatus(InvData.Proc(InvData.Patient(),codeNum),ProcStat.C);
			InventoryTracking.ConfirmMaterials(new List<long> { procedure.ProcNum },InventoryTracking.GetDefaultMaterials(new List<Procedure> { procedure }));
			ApplicationException ex=InvData.Throws<ApplicationException>(() => Supplies.DeleteObject(Supplies.GetSupply(supplyUsed.SupplyNum)));
			StringAssert.Contains(ex.Message,"Hide it instead");
			Assert.IsNotNull(Supplies.GetSupply(supplyUsed.SupplyNum));
			Supply supplyNew=InvData.Supply(onHand:2,isLotTracked:true,reorderLevel:1);
			long codeNum2=InvData.Code((supplyNew,1));
			Supplies.DeleteObject(Supplies.GetSupply(supplyNew.SupplyNum));
			Assert.IsNull(Supplies.GetSupply(supplyNew.SupplyNum));
			Assert.AreEqual(0,SupplyLedgers.GetForSupply(supplyNew.SupplyNum).Count);
			Assert.AreEqual(0,SupplySettings.GetForSupplies(new List<long> { supplyNew.SupplyNum }).Count);
			Assert.AreEqual(0,SupplyProcCodes.GetForCodes(new List<long> { codeNum2 }).Count);
		}

		[TestMethod]
		public void Dispense_WritesADispenseRowWhileTrackingIsOn() {
			Supply supply=InvData.Supply(onHand:20);
			DispSupply dispSupply=new DispSupply { SupplyNum=supply.SupplyNum,ProvNum=1,DateDispensed=DateTime.Today,DispQuantity=3,Note="" };
			DispSupplies.Insert(dispSupply);
			SupplyLedger supplyLedger=SupplyLedgers.GetForSupply(supply.SupplyNum).Last();
			Assert.AreEqual(SupplyLedgerType.Dispense,supplyLedger.LedgerType);
			Assert.AreEqual(dispSupply.DispSupplyNum,supplyLedger.DispSupplyNum);
			Assert.AreEqual(17,InvData.OnHand(supply.SupplyNum));
		}
	}
}
