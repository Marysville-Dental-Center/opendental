// Soonest-expiring first: the order of lots, splitting a quantity over lots, and deductions that take the lot that expires first.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace InventoryTests {
	[TestClass]
	public class LotRuleTests {
		private static SupplyLot NewLot(long supplyLotNum,DateTime dateExpiry,double qty) {
			return new SupplyLot { SupplyLotNum=supplyLotNum,SupplyNum=7,LotNumber="L"+supplyLotNum,DateExpiry=dateExpiry,QtyOnHand=qty };
		}

		[TestMethod]
		public void SortFifo_DatedLotsEarliestFirst_ThenUndated_ThenByKey() {
			List<SupplyLot> listSupplyLots=new List<SupplyLot> {
				NewLot(1,DateTime.MinValue,5),
				NewLot(2,new DateTime(2027,6,1),5),
				NewLot(3,new DateTime(2027,1,1),5),
				NewLot(4,DateTime.MinValue,5),
				NewLot(5,new DateTime(2027,1,1),5),
			};
			CollectionAssert.AreEqual(new long[] { 3,5,2,1,4 },SupplyLots.SortFifo(listSupplyLots).Select(x => x.SupplyLotNum).ToArray());
		}

		[TestMethod]
		public void AllocateFifo_SplitsOverLots_AndTakesTheRestWithoutALot() {
			List<SupplyLot> listSupplyLots=new List<SupplyLot> {
				NewLot(1,new DateTime(2028,1,1),10),
				NewLot(2,new DateTime(2027,1,1),1.5),
				NewLot(3,new DateTime(2026,1,1),0),//Empty: skipped.
			};
			List<SupplyUsageLine> list=SupplyLots.AllocateFifo(7,4,listSupplyLots);
			Assert.AreEqual(2,list.Count);
			Assert.AreEqual(2,list[0].SupplyLotNum);
			Assert.AreEqual(1.5,list[0].Quantity);
			Assert.AreEqual(1,list[1].SupplyLotNum);
			Assert.AreEqual(2.5,list[1].Quantity);
			list=SupplyLots.AllocateFifo(7,13,listSupplyLots);
			CollectionAssert.AreEqual(new long[] { 2,1,0 },list.Select(x => x.SupplyLotNum).ToArray());
			Assert.AreEqual(1.5,list[2].Quantity,"Beyond what the lots hold: without a lot, below zero.");
			list=SupplyLots.AllocateFifo(7,2,new List<SupplyLot>());
			Assert.AreEqual(1,list.Count);
			Assert.AreEqual(0,list[0].SupplyLotNum);
			Assert.AreEqual(0,SupplyLots.AllocateFifo(7,0,listSupplyLots).Count);
			Assert.AreEqual(0,SupplyLots.AllocateFifo(8,1,listSupplyLots).Single().SupplyLotNum,"Lots of another supply are not used.");
		}
	}

	[TestClass]
	public class LotDbTests {
		[TestInitialize]
		public void TestInitialize() {
			InvDb.Begin();
		}

		[TestMethod]
		public void Deduction_TakesTheSoonestExpiringLotFirst() {
			Supply supply=InvData.Supply(isLotTracked:true);
			int n=InvData.Next();
			SupplyLot supplyLotLate=InvData.Lot(supply,"LATE"+n,DateTime.Today.AddMonths(9),5);
			SupplyLot supplyLotSoon=InvData.Lot(supply,"SOON"+n,DateTime.Today.AddMonths(2),2);
			SupplyLot supplyLotNone=InvData.Lot(supply,"NONE"+n,DateTime.MinValue,5);
			long codeNum=InvData.Code((supply,3));
			Procedure procedure=InvData.SetStatus(InvData.Proc(InvData.Patient(),codeNum),ProcStat.C);
			List<SupplyUsageLine> listDefaults=InventoryTracking.GetDefaultMaterials(new List<Procedure> { procedure });
			CollectionAssert.AreEqual(new[] { supplyLotSoon.SupplyLotNum,supplyLotLate.SupplyLotNum },listDefaults.Select(x => x.SupplyLotNum).ToArray());
			CollectionAssert.AreEqual(new[] { 2.0,1.0 },listDefaults.Select(x => x.Quantity).ToArray());
			InventoryTracking.ConfirmMaterials(new List<long> { procedure.ProcNum },listDefaults);
			Assert.AreEqual(0,InvData.LotQty(supplyLotSoon.SupplyLotNum));
			Assert.AreEqual(4,InvData.LotQty(supplyLotLate.SupplyLotNum));
			Assert.AreEqual(5,InvData.LotQty(supplyLotNone.SupplyLotNum));
			Assert.AreEqual(9,InvData.OnHand(supply.SupplyNum));
		}

		[TestMethod]
		public void AutoLot_IsChosenWhenDeducted() {
			Supply supply=InvData.Supply(isLotTracked:true);
			int n=InvData.Next();
			SupplyLot supplyLotA=InvData.Lot(supply,"A"+n,DateTime.Today.AddMonths(3),1);
			SupplyLot supplyLotB=InvData.Lot(supply,"B"+n,DateTime.Today.AddMonths(1),1);
			long codeNum=InvData.Code((supply,1));
			Procedure procedure=InvData.SetStatus(InvData.Proc(InvData.Patient(),codeNum),ProcStat.C);
			InventoryTracking.ConfirmMaterials(new List<long> { procedure.ProcNum },
				new List<SupplyUsageLine> { new SupplyUsageLine(procedure.ProcNum,supply.SupplyNum,-1,1.5) });
			List<SupplyUsageLine> listHeld=InventoryTracking.GetProcMaterials(new List<long> { procedure.ProcNum });
			Assert.AreEqual(1,listHeld.Single(x => x.SupplyLotNum==supplyLotB.SupplyLotNum).Quantity);
			Assert.AreEqual(0.5,listHeld.Single(x => x.SupplyLotNum==supplyLotA.SupplyLotNum).Quantity);
			//Asking for less gives back from the lot that expires last first.
			InventoryTracking.ConfirmMaterials(new List<long> { procedure.ProcNum },
				new List<SupplyUsageLine> { new SupplyUsageLine(procedure.ProcNum,supply.SupplyNum,-1,1) });
			listHeld=InventoryTracking.GetProcMaterials(new List<long> { procedure.ProcNum });
			Assert.AreEqual(1,listHeld.Count);
			Assert.AreEqual(supplyLotB.SupplyLotNum,listHeld[0].SupplyLotNum);
			Assert.AreEqual(1,InvData.LotQty(supplyLotA.SupplyLotNum));
		}

		[TestMethod]
		public void GetOrInsert_FindsTheLotByNumber_AndFillsAMissingExpiry() {
			Supply supply=InvData.Supply(isLotTracked:true);
			string lotNumber="G"+InvData.Next();
			SupplyLot supplyLot=SupplyLots.GetOrInsert(supply.SupplyNum,lotNumber,DateTime.MinValue,0);
			Assert.IsFalse(supplyLot.HasExpiry());
			SupplyLot supplyLotAgain=SupplyLots.GetOrInsert(supply.SupplyNum," "+lotNumber.ToLower()+" ",new DateTime(2027,3,1),0);
			Assert.AreEqual(supplyLot.SupplyLotNum,supplyLotAgain.SupplyLotNum,"Same lot, ignoring case and blanks.");
			Assert.AreEqual(new DateTime(2027,3,1),SupplyLots.GetMany(new List<long> { supplyLot.SupplyLotNum }).Single().DateExpiry);
			InvData.Throws<ApplicationException>(() => SupplyLots.GetOrInsert(supply.SupplyNum,"  ",DateTime.MinValue,0));
		}
	}
}
