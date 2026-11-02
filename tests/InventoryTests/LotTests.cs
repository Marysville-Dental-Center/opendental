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

		///<summary>The lots of lines of lot -1 are chosen procedure by procedure: a later procedure does not get what an earlier one takes, explicit
		///lines of the supply are kept and their draws count, what a procedure holds is kept, and less gives back from the lot that expires last.
		///</summary>
		[TestMethod]
		public void ResolveAutoLines_ProcedureByProcedure_KeepsExplicitAndHeldLines() {
			List<SupplyLot> listSupplyLots=new List<SupplyLot> {
				NewLot(1,new DateTime(2027,1,1),1),//SOON
				NewLot(2,new DateTime(2028,1,1),10),//LATE
			};
			//Two procedures each want 1, soonest-expiring first: the first gets SOON, the second LATE.
			List<SupplyUsageLine> listLines=new List<SupplyUsageLine> { new SupplyUsageLine(100,7,-1,1),new SupplyUsageLine(200,7,-1,1) };
			List<SupplyUsageLine> listAuto=InventoryTracking.ResolveAutoLines(new List<long> { 100,200 },listLines,new List<SupplyUsageLine>(),listSupplyLots);
			Assert.AreEqual(1,listAuto.Single(x => x.ProcNum==100).SupplyLotNum);
			Assert.AreEqual(2,listAuto.Single(x => x.ProcNum==200).SupplyLotNum);
			Assert.AreEqual(0,listSupplyLots[0].QtyOnHand,"What the procedures take is taken off the lots.");
			Assert.AreEqual(9,listSupplyLots[1].QtyOnHand);
			//A quantity above what the first lot holds splits over the lots.
			listSupplyLots=new List<SupplyLot> { NewLot(1,new DateTime(2027,1,1),1),NewLot(2,new DateTime(2028,1,1),10) };
			listAuto=InventoryTracking.ResolveAutoLines(new List<long> { 100 },new List<SupplyUsageLine> { new SupplyUsageLine(100,7,-1,2) },
				new List<SupplyUsageLine>(),listSupplyLots);
			CollectionAssert.AreEqual(new long[] { 1,2 },listAuto.Select(x => x.SupplyLotNum).ToArray());
			CollectionAssert.AreEqual(new[] { 1.0,1.0 },listAuto.Select(x => x.Quantity).ToArray());
			//An explicit line of the same supply is kept (not returned) and takes from its lot first.
			listSupplyLots=new List<SupplyLot> { NewLot(1,new DateTime(2027,1,1),1),NewLot(2,new DateTime(2028,1,1),10) };
			listLines=new List<SupplyUsageLine> { new SupplyUsageLine(100,7,1,1),new SupplyUsageLine(100,7,-1,1) };
			listAuto=InventoryTracking.ResolveAutoLines(new List<long> { 100 },listLines,new List<SupplyUsageLine>(),listSupplyLots);
			Assert.AreEqual(2,listAuto.Single().SupplyLotNum,"SOON is taken by the explicit line.");
			Assert.AreEqual(0,listSupplyLots[0].QtyOnHand);
			Assert.AreEqual(9,listSupplyLots[1].QtyOnHand);
			//Held: what the procedure holds is kept; asking for less gives back from the lot that expires last, then nothing more is taken.
			listSupplyLots=new List<SupplyLot> { NewLot(1,new DateTime(2027,1,1),0),NewLot(2,new DateTime(2028,1,1),5) };
			List<SupplyUsageLine> listHeld=new List<SupplyUsageLine> { new SupplyUsageLine(100,7,1,1),new SupplyUsageLine(100,7,2,1) };
			listAuto=InventoryTracking.ResolveAutoLines(new List<long> { 100 },new List<SupplyUsageLine> { new SupplyUsageLine(100,7,-1,2) },listHeld,listSupplyLots);
			CollectionAssert.AreEquivalent(new long[] { 1,2 },listAuto.Select(x => x.SupplyLotNum).ToArray(),"Unchanged: what it holds.");
			Assert.AreEqual(5,listSupplyLots[1].QtyOnHand,"Nothing new is taken.");
			listAuto=InventoryTracking.ResolveAutoLines(new List<long> { 100 },new List<SupplyUsageLine> { new SupplyUsageLine(100,7,-1,1) },listHeld,listSupplyLots);
			Assert.AreEqual(1,listAuto.Single().SupplyLotNum,"The lot that expires last is given back.");
			Assert.AreEqual(6,listSupplyLots[1].QtyOnHand);
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

		///<summary>The Materials used panel sends lot -1 for every material but implants. A quantity changed above what the soonest-expiring lot holds
		///splits over the lots when it is deducted (the finding's example: SOON holds 1, LATE 10, the assistant changes 1 to 2).</summary>
		[TestMethod]
		public void AutoLot_QuantityEditedAboveTheFirstLot_SplitsOverLots() {
			Supply supply=InvData.Supply(isLotTracked:true);
			int n=InvData.Next();
			SupplyLot supplyLotSoon=InvData.Lot(supply,"SOON"+n,DateTime.Today.AddMonths(1),1);
			SupplyLot supplyLotLate=InvData.Lot(supply,"LATE"+n,DateTime.Today.AddMonths(8),10);
			Procedure procedure=InvData.SetStatus(InvData.Proc(InvData.Patient(),InvData.Code((supply,1))),ProcStat.C);
			List<SupplyUsageLine> listLines=InventoryTracking.GetDefaultMaterials(new List<Procedure> { procedure },isLotAuto:true);
			Assert.AreEqual(-1,listLines.Single().SupplyLotNum,"Chosen when deducted.");
			listLines[0].Quantity=2;
			InventoryTracking.ConfirmMaterials(new List<long> { procedure.ProcNum },listLines);
			Assert.AreEqual(0,InvData.LotQty(supplyLotSoon.SupplyLotNum));
			Assert.AreEqual(9,InvData.LotQty(supplyLotLate.SupplyLotNum));
			Assert.AreEqual(9,InvData.OnHand(supply.SupplyNum));
			List<SupplyUsageLine> listHeld=InventoryTracking.GetProcMaterials(new List<long> { procedure.ProcNum });
			Assert.AreEqual(1,listHeld.Single(x => x.SupplyLotNum==supplyLotSoon.SupplyLotNum).Quantity);
			Assert.AreEqual(1,listHeld.Single(x => x.SupplyLotNum==supplyLotLate.SupplyLotNum).Quantity);
		}

		///<summary>Two panels opened at the same time both plan the last unit of the soonest-expiring lot. Each confirmation chooses from the lots as
		///they are then, so the second one takes the next lot; the same holds within one confirmation of several procedures.</summary>
		[TestMethod]
		public void AutoLot_TwoConfirmsAgainstTheSameLastUnit_Split() {
			Supply supply=InvData.Supply(isLotTracked:true);
			int n=InvData.Next();
			SupplyLot supplyLotSoon=InvData.Lot(supply,"SOON"+n,DateTime.Today.AddMonths(1),1);
			SupplyLot supplyLotLate=InvData.Lot(supply,"LATE"+n,DateTime.Today.AddMonths(8),10);
			long codeNum=InvData.Code((supply,1));
			Procedure procedureA=InvData.SetStatus(InvData.Proc(InvData.Patient(),codeNum),ProcStat.C);
			Procedure procedureB=InvData.SetStatus(InvData.Proc(InvData.Patient(),codeNum),ProcStat.C);
			//Both panels are filled before either is confirmed.
			List<SupplyUsageLine> listLinesA=InventoryTracking.GetDefaultMaterials(new List<Procedure> { procedureA },isLotAuto:true);
			List<SupplyUsageLine> listLinesB=InventoryTracking.GetDefaultMaterials(new List<Procedure> { procedureB },isLotAuto:true);
			InventoryTracking.ConfirmMaterials(new List<long> { procedureA.ProcNum },listLinesA);
			InventoryTracking.ConfirmMaterials(new List<long> { procedureB.ProcNum },listLinesB);
			Assert.AreEqual(supplyLotSoon.SupplyLotNum,InventoryTracking.GetProcMaterials(new List<long> { procedureA.ProcNum }).Single().SupplyLotNum);
			Assert.AreEqual(supplyLotLate.SupplyLotNum,InventoryTracking.GetProcMaterials(new List<long> { procedureB.ProcNum }).Single().SupplyLotNum);
			Assert.AreEqual(0,InvData.LotQty(supplyLotSoon.SupplyLotNum),"Never below zero.");
			Assert.AreEqual(9,InvData.LotQty(supplyLotLate.SupplyLotNum));
			//One confirmation of two procedures.
			SupplyLot supplyLotSoon2=InvData.Lot(supply,"SOON2-"+n,DateTime.Today.AddDays(10),1);
			Procedure procedureC=InvData.SetStatus(InvData.Proc(InvData.Patient(),codeNum),ProcStat.C);
			Procedure procedureD=InvData.SetStatus(InvData.Proc(InvData.Patient(),codeNum),ProcStat.C);
			List<Procedure> listProcedures=new List<Procedure> { procedureC,procedureD };
			InventoryTracking.ConfirmMaterials(listProcedures.Select(x => x.ProcNum).ToList(),InventoryTracking.GetDefaultMaterials(listProcedures,isLotAuto:true));
			Assert.AreEqual(supplyLotSoon2.SupplyLotNum,InventoryTracking.GetProcMaterials(new List<long> { procedureC.ProcNum }).Single().SupplyLotNum);
			Assert.AreEqual(supplyLotLate.SupplyLotNum,InventoryTracking.GetProcMaterials(new List<long> { procedureD.ProcNum }).Single().SupplyLotNum);
			Assert.AreEqual(0,InvData.LotQty(supplyLotSoon2.SupplyLotNum));
			Assert.AreEqual(8,InvData.LotQty(supplyLotLate.SupplyLotNum));
		}

		///<summary>An explicit line (a lot the assistant picked) and a line of lot -1 of the same supply: the explicit lot is kept, the rest is chosen
		///soonest-expiring first, and a later correction gives back only from what the automatic part holds.</summary>
		[TestMethod]
		public void AutoLot_ExplicitLinesOfTheSameSupplyAreKept() {
			Supply supply=InvData.Supply(isLotTracked:true);
			int n=InvData.Next();
			SupplyLot supplyLotSoon=InvData.Lot(supply,"SOON"+n,DateTime.Today.AddMonths(1),2);
			SupplyLot supplyLotLate=InvData.Lot(supply,"LATE"+n,DateTime.Today.AddMonths(8),5);
			Procedure procedure=InvData.SetStatus(InvData.Proc(InvData.Patient(),InvData.Code((supply,1))),ProcStat.C);
			InventoryTracking.ConfirmMaterials(new List<long> { procedure.ProcNum },new List<SupplyUsageLine> {
				new SupplyUsageLine(procedure.ProcNum,supply.SupplyNum,supplyLotLate.SupplyLotNum,1),
				new SupplyUsageLine(procedure.ProcNum,supply.SupplyNum,-1,2) });
			List<SupplyUsageLine> listHeld=InventoryTracking.GetProcMaterials(new List<long> { procedure.ProcNum });
			Assert.AreEqual(1,listHeld.Single(x => x.SupplyLotNum==supplyLotLate.SupplyLotNum).Quantity,"The picked lot is kept.");
			Assert.AreEqual(2,listHeld.Single(x => x.SupplyLotNum==supplyLotSoon.SupplyLotNum).Quantity);
			//Opened again: the picked lot stays, the automatic part drops to 1.
			InventoryTracking.ConfirmMaterials(new List<long> { procedure.ProcNum },new List<SupplyUsageLine> {
				new SupplyUsageLine(procedure.ProcNum,supply.SupplyNum,supplyLotLate.SupplyLotNum,1),
				new SupplyUsageLine(procedure.ProcNum,supply.SupplyNum,-1,1) });
			listHeld=InventoryTracking.GetProcMaterials(new List<long> { procedure.ProcNum });
			Assert.AreEqual(1,listHeld.Single(x => x.SupplyLotNum==supplyLotLate.SupplyLotNum).Quantity);
			Assert.AreEqual(1,listHeld.Single(x => x.SupplyLotNum==supplyLotSoon.SupplyLotNum).Quantity);
			Assert.AreEqual(1,InvData.LotQty(supplyLotSoon.SupplyLotNum));
			Assert.AreEqual(4,InvData.LotQty(supplyLotLate.SupplyLotNum));
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
