// Procedures and materials: completing records the procedure as awaiting materials and deducts nothing; confirming deducts once (and only the
// difference when confirmed again); un-completing and deleting restore; several procedures in one confirmation; the implant lot on the
// procedure; nothing while tracking is off.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace InventoryTests {
	[TestClass]
	public class ProcedureMaterialsTests {
		[TestInitialize]
		public void TestInitialize() {
			InvDb.Begin();
		}

		private static bool IsPending(long procNum) {
			return SupplyProcPendings.GetForProcs(new List<long> { procNum }).Count>0;
		}

		[TestMethod]
		public void Complete_RecordsAwaitingMaterials_AndDeductsNothing() {
			Supply supply=InvData.Supply(onHand:10);
			long codeNum=InvData.Code((supply,2));
			long patNum=InvData.Patient();
			List<long> listProcNumsRaised=new List<long>();
			Action<long> action=x => listProcNumsRaised.Add(x);
			InventoryTracking.ProcSetComplete+=action;
			Procedure procedure;
			Procedure procedureInserted;
			try {
				procedure=InvData.SetStatus(InvData.Proc(patNum,codeNum),ProcStat.C);//Chart module Set Complete, procedure edit window.
				procedureInserted=InvData.Proc(patNum,codeNum,ProcStat.C);//Entered as complete.
			}
			finally {
				InventoryTracking.ProcSetComplete-=action;
			}
			CollectionAssert.AreEqual(new[] { procedure.ProcNum,procedureInserted.ProcNum },listProcNumsRaised);
			Assert.IsTrue(IsPending(procedure.ProcNum));
			Assert.IsTrue(IsPending(procedureInserted.ProcNum));
			SupplyProcPending supplyProcPending=SupplyProcPendings.GetForProcs(new List<long> { procedure.ProcNum }).Single();
			Assert.AreEqual(patNum,supplyProcPending.PatNum);
			Assert.AreEqual(Security.CurUser.UserNum,supplyProcPending.UserNum);
			Assert.AreEqual(10,InvData.OnHand(supply.SupplyNum),"Completing never deducts.");
			Assert.AreEqual(1,SupplyLedgers.GetForSupply(supply.SupplyNum).Count);
			Assert.IsTrue(SupplyProcPendings.GetTableAll().Select().Any(x => PIn.Long(x["ProcNum"].ToString())==procedure.ProcNum),"On the Awaiting Materials list.");
		}

		[TestMethod]
		public void Complete_CodeWithoutMaterials_IsNotAwaiting() {
			long codeNum=InvData.Code();
			Procedure procedure=InvData.SetStatus(InvData.Proc(InvData.Patient(),codeNum),ProcStat.C);
			Assert.IsFalse(IsPending(procedure.ProcNum));
		}

		[TestMethod]
		public void Confirm_DeductsOnce_AndAgainOnlyTheDifference() {
			Supply supply=InvData.Supply(onHand:10,price:4);
			Supply supplyExtra=InvData.Supply(onHand:5);
			long codeNum=InvData.Code((supply,2));
			Procedure procedure=InvData.SetStatus(InvData.Proc(InvData.Patient(),codeNum),ProcStat.C);
			List<SupplyUsageLine> listLines=InventoryTracking.GetDefaultMaterials(new List<Procedure> { procedure });
			Assert.AreEqual(1,listLines.Count);
			Assert.AreEqual(2,listLines[0].Quantity);
			listLines.Add(new SupplyUsageLine(procedure.ProcNum,supplyExtra.SupplyNum,0,1));//Added by the assistant.
			InventoryTracking.ConfirmMaterials(new List<long> { procedure.ProcNum },listLines);
			Assert.AreEqual(8,InvData.OnHand(supply.SupplyNum));
			Assert.AreEqual(4,InvData.OnHand(supplyExtra.SupplyNum));
			Assert.IsFalse(IsPending(procedure.ProcNum));
			SupplyLedger supplyLedgerUse=SupplyLedgers.GetForProcs(new List<long> { procedure.ProcNum }).First(x => x.SupplyNum==supply.SupplyNum);
			Assert.AreEqual(SupplyLedgerType.Use,supplyLedgerUse.LedgerType);
			Assert.AreEqual(-2,supplyLedgerUse.Quantity);
			Assert.AreEqual(4,supplyLedgerUse.UnitCost);
			Assert.AreEqual(procedure.PatNum,supplyLedgerUse.PatNum);
			//Confirmed again with the same lines (a retry after a failure part-way): nothing more.
			InventoryTracking.ConfirmMaterials(new List<long> { procedure.ProcNum },listLines);
			Assert.AreEqual(8,InvData.OnHand(supply.SupplyNum));
			Assert.AreEqual(2,SupplyLedgers.GetForProcs(new List<long> { procedure.ProcNum }).Count);
			//Corrected: 1 of the supply instead of 2, and the extra item removed.
			InventoryTracking.ConfirmMaterials(new List<long> { procedure.ProcNum },
				new List<SupplyUsageLine> { new SupplyUsageLine(procedure.ProcNum,supply.SupplyNum,0,1) });
			Assert.AreEqual(9,InvData.OnHand(supply.SupplyNum));
			Assert.AreEqual(5,InvData.OnHand(supplyExtra.SupplyNum));
			List<SupplyLedger> listRestores=SupplyLedgers.GetForProcs(new List<long> { procedure.ProcNum }).FindAll(x => x.LedgerType==SupplyLedgerType.Restore);
			Assert.AreEqual(2,listRestores.Count);
			Assert.IsTrue(listRestores.All(x => x.Reason==InventoryTracking.REASON_CORRECTED));
			Assert.AreEqual(4,listRestores.Single(x => x.SupplyNum==supply.SupplyNum).UnitCost,"Put back at the cost it was taken at.");
		}

		[TestMethod]
		public void Confirm_WithNoLines_RemovesTheProcedureFromTheAwaitingList() {
			Supply supply=InvData.Supply(onHand:3);
			Procedure procedure=InvData.SetStatus(InvData.Proc(InvData.Patient(),InvData.Code((supply,1))),ProcStat.C);
			InventoryTracking.ConfirmMaterials(new List<long> { procedure.ProcNum },new List<SupplyUsageLine>());
			Assert.IsFalse(IsPending(procedure.ProcNum));
			Assert.AreEqual(3,InvData.OnHand(supply.SupplyNum));
		}

		[TestMethod]
		public void Uncomplete_RestoresWhatTheProcedureUsed() {
			Supply supply=InvData.Supply(onHand:10);
			Procedure procedure=InvData.SetStatus(InvData.Proc(InvData.Patient(),InvData.Code((supply,3))),ProcStat.C);
			InventoryTracking.ConfirmMaterials(new List<long> { procedure.ProcNum },InventoryTracking.GetDefaultMaterials(new List<Procedure> { procedure }));
			Assert.AreEqual(7,InvData.OnHand(supply.SupplyNum));
			InvData.SetStatus(procedure,ProcStat.TP);
			Assert.AreEqual(10,InvData.OnHand(supply.SupplyNum));
			SupplyLedger supplyLedgerRestore=SupplyLedgers.GetForProcs(new List<long> { procedure.ProcNum }).Last();
			Assert.AreEqual(SupplyLedgerType.Restore,supplyLedgerRestore.LedgerType);
			Assert.AreEqual(3,supplyLedgerRestore.Quantity);
			Assert.AreEqual(InventoryTracking.REASON_UNCOMPLETED,supplyLedgerRestore.Reason);
			Assert.AreEqual(0,InventoryTracking.GetProcMaterials(new List<long> { procedure.ProcNum }).Count);
			//Completed again: awaiting again, and confirming deducts again.
			procedure=InvData.SetStatus(procedure,ProcStat.C);
			Assert.IsTrue(IsPending(procedure.ProcNum));
			InventoryTracking.ConfirmMaterials(new List<long> { procedure.ProcNum },InventoryTracking.GetDefaultMaterials(new List<Procedure> { procedure }));
			Assert.AreEqual(7,InvData.OnHand(supply.SupplyNum));
		}

		[TestMethod]
		public void Uncomplete_WhileAwaiting_RemovesItFromTheList() {
			Supply supply=InvData.Supply(onHand:4);
			Procedure procedure=InvData.SetStatus(InvData.Proc(InvData.Patient(),InvData.Code((supply,1))),ProcStat.C);
			Assert.IsTrue(IsPending(procedure.ProcNum));
			InvData.SetStatus(procedure,ProcStat.TP);
			Assert.IsFalse(IsPending(procedure.ProcNum));
			Assert.AreEqual(4,InvData.OnHand(supply.SupplyNum));
		}

		[TestMethod]
		public void Delete_RestoresWhatTheProcedureUsed() {
			Supply supply=InvData.Supply(onHand:6);
			Procedure procedure=InvData.SetStatus(InvData.Proc(InvData.Patient(),InvData.Code((supply,2))),ProcStat.C);
			InventoryTracking.ConfirmMaterials(new List<long> { procedure.ProcNum },InventoryTracking.GetDefaultMaterials(new List<Procedure> { procedure }));
			Assert.AreEqual(4,InvData.OnHand(supply.SupplyNum));
			Procedures.Delete(procedure.ProcNum,forceDelete:true);
			Assert.AreEqual(ProcStat.D,Procedures.GetOneProc(procedure.ProcNum,false).ProcStatus);
			Assert.AreEqual(6,InvData.OnHand(supply.SupplyNum));
			Assert.AreEqual(InventoryTracking.REASON_DELETED,SupplyLedgers.GetForProcs(new List<long> { procedure.ProcNum }).Last().Reason);
			Assert.IsFalse(IsPending(procedure.ProcNum));
		}

		[TestMethod]
		public void Batch_OneConfirmationForSeveralProcedures() {
			Supply supplyA=InvData.Supply(onHand:10);
			Supply supplyB=InvData.Supply(onHand:10);
			long patNum=InvData.Patient();
			Procedure procedure1=InvData.SetStatus(InvData.Proc(patNum,InvData.Code((supplyA,1),(supplyB,2))),ProcStat.C);
			Procedure procedure2=InvData.SetStatus(InvData.Proc(patNum,InvData.Code((supplyA,3))),ProcStat.C);
			Procedure procedureNoMaterials=InvData.SetStatus(InvData.Proc(patNum,InvData.Code()),ProcStat.C);
			List<Procedure> listProcedures=new List<Procedure> { procedure1,procedure2,procedureNoMaterials };
			List<SupplyUsageLine> listLines=InventoryTracking.GetDefaultMaterials(listProcedures);
			Assert.AreEqual(3,listLines.Count);
			List<long> listSkipped=InventoryTracking.ConfirmMaterials(listProcedures.Select(x => x.ProcNum).ToList(),listLines);
			Assert.AreEqual(0,listSkipped.Count);
			Assert.AreEqual(6,InvData.OnHand(supplyA.SupplyNum));
			Assert.AreEqual(8,InvData.OnHand(supplyB.SupplyNum));
			Assert.IsFalse(IsPending(procedure1.ProcNum));
			Assert.IsFalse(IsPending(procedure2.ProcNum));
			Assert.AreEqual(2,InventoryTracking.GetProcMaterials(new List<long> { procedure1.ProcNum }).Count);
			Assert.AreEqual(3,InventoryTracking.GetProcMaterials(new List<long> { procedure2.ProcNum }).Single().Quantity);
		}

		[TestMethod]
		public void Confirm_SkipsAProcedureThatIsNoLongerComplete() {
			Supply supply=InvData.Supply(onHand:5);
			long codeNum=InvData.Code((supply,1));
			Procedure procedure=InvData.SetStatus(InvData.Proc(InvData.Patient(),codeNum),ProcStat.C);
			List<SupplyUsageLine> listLines=InventoryTracking.GetDefaultMaterials(new List<Procedure> { procedure });
			InvData.SetStatus(procedure,ProcStat.TP);//Another workstation, while the panel was open.
			List<long> listSkipped=InventoryTracking.ConfirmMaterials(new List<long> { procedure.ProcNum },listLines);
			CollectionAssert.AreEqual(new[] { procedure.ProcNum },listSkipped);
			Assert.AreEqual(5,InvData.OnHand(supply.SupplyNum));
		}

		[TestMethod]
		public void Implant_TheLotIsRecordedOnTheProcedure_AndPutBackToTheSameLot() {
			Supply supplyImplant=InvData.Supply(isImplant:true);
			Assert.IsTrue(SupplySettings.GetForSupply(supplyImplant.SupplyNum).IsLotTracked,"An implant is tracked by lot.");
			int n=InvData.Next();
			SupplyLot supplyLotLater=InvData.Lot(supplyImplant,"IMP-LATE-"+n,DateTime.Today.AddYears(2),1);
			SupplyLot supplyLotSooner=InvData.Lot(supplyImplant,"IMP-SOON-"+n,DateTime.Today.AddYears(1),1);
			Procedure procedure=InvData.SetStatus(InvData.Proc(InvData.Patient(),InvData.Code((supplyImplant,1))),ProcStat.C);
			List<SupplyUsageLine> listLines=InventoryTracking.GetDefaultMaterials(new List<Procedure> { procedure });
			Assert.AreEqual(supplyLotSooner.SupplyLotNum,listLines.Single().SupplyLotNum,"The panel proposes the soonest-expiring lot.");
			//The assistant reads the label: the later lot was used.
			listLines[0].SupplyLotNum=supplyLotLater.SupplyLotNum;
			InventoryTracking.ConfirmMaterials(new List<long> { procedure.ProcNum },listLines);
			SupplyUsageLine supplyUsageLine=InventoryTracking.GetProcMaterials(new List<long> { procedure.ProcNum }).Single();
			Assert.AreEqual(supplyLotLater.SupplyLotNum,supplyUsageLine.SupplyLotNum,"Visible from the procedure.");
			Assert.AreEqual("IMP-LATE-"+n,SupplyLots.GetMany(new List<long> { supplyUsageLine.SupplyLotNum }).Single().LotNumber);
			Assert.AreEqual(0,InvData.LotQty(supplyLotLater.SupplyLotNum));
			Assert.AreEqual(1,InvData.LotQty(supplyLotSooner.SupplyLotNum));
			Procedures.Delete(procedure.ProcNum,forceDelete:true);
			Assert.AreEqual(1,InvData.LotQty(supplyLotLater.SupplyLotNum));
			Assert.AreEqual(2,InvData.OnHand(supplyImplant.SupplyNum));
		}

		///<summary>A procedure left not complete while it still holds materials (the program stopped between its status write and its restore):
		///the repair puts back what it holds, removes its awaiting row, and counts it.</summary>
		[TestMethod]
		public void Repair_PutsBackWhatAProcedureNoLongerCompleteHolds() {
			Supply supply=InvData.Supply(onHand:10);
			long codeNum=InvData.Code((supply,3));
			Procedure procedure=InvData.SetStatus(InvData.Proc(InvData.Patient(),codeNum),ProcStat.C);
			InventoryTracking.ConfirmMaterials(new List<long> { procedure.ProcNum },InventoryTracking.GetDefaultMaterials(new List<Procedure> { procedure }));
			Assert.AreEqual(7,InvData.OnHand(supply.SupplyNum));
			Procedure procedureAwaiting=InvData.SetStatus(InvData.Proc(InvData.Patient(),codeNum),ProcStat.C);
			//Only the status writes of Procedures.Delete and Procedures.Update ran.
			DataCore.NonQ("UPDATE procedurelog SET ProcStatus="+(int)ProcStat.D+" WHERE ProcNum="+procedure.ProcNum);
			DataCore.NonQ("UPDATE procedurelog SET ProcStatus="+(int)ProcStat.TP+" WHERE ProcNum="+procedureAwaiting.ProcNum);
			CollectionAssert.Contains(SupplyLedgers.GetProcNumsHeldNotComplete(),procedure.ProcNum);
			SupplyRepairResult supplyRepairResult=InventoryTracking.Repair();
			Assert.IsTrue(supplyRepairResult.CountProcs>=1);
			Assert.IsTrue(supplyRepairResult.CountAwaiting>=1);
			Assert.AreEqual(10,InvData.OnHand(supply.SupplyNum));
			Assert.AreEqual(0,InventoryTracking.GetProcMaterials(new List<long> { procedure.ProcNum }).Count);
			Assert.AreEqual(InventoryTracking.REASON_REPAIRED,SupplyLedgers.GetForProcs(new List<long> { procedure.ProcNum }).Last().Reason);
			Assert.IsFalse(IsPending(procedureAwaiting.ProcNum));
			CollectionAssert.DoesNotContain(SupplyLedgers.GetProcNumsHeldNotComplete(),procedure.ProcNum);
			Assert.AreEqual(0,InventoryTracking.Repair().CountProcs,"Nothing left to put back.");
			Assert.IsTrue(InvData.CountLogs(EnumPermType.InventoryAdjust,"procedure(s) no longer complete put back")>=2);
		}

		///<summary>Another workstation un-completes the procedure after ConfirmMaterials read its status and before it wrote the Use rows: that
		///workstation's restore finds nothing, so ConfirmMaterials reads the status again and puts back what it took.</summary>
		[TestMethod]
		public void Confirm_ProcedureUncompletedWhileConfirming_IsPutBack() {
			Supply supply=InvData.Supply(onHand:6);
			Procedure procedure=InvData.SetStatus(InvData.Proc(InvData.Patient(),InvData.Code((supply,2))),ProcStat.C);
			List<SupplyUsageLine> listLines=InventoryTracking.GetDefaultMaterials(new List<Procedure> { procedure },isLotAuto:true);
			List<long> listSkipped;
			InventoryTracking.ActionAfterStatusesRead=listProcNums => InvData.SetStatus(procedure,ProcStat.TP);
			try {
				listSkipped=InventoryTracking.ConfirmMaterials(new List<long> { procedure.ProcNum },listLines);
			}
			finally {
				InventoryTracking.ActionAfterStatusesRead=null;
			}
			CollectionAssert.AreEqual(new[] { procedure.ProcNum },listSkipped,"Reported as not deducted.");
			Assert.AreEqual(6,InvData.OnHand(supply.SupplyNum));
			Assert.AreEqual(0,InventoryTracking.GetProcMaterials(new List<long> { procedure.ProcNum }).Count);
			Assert.AreEqual(InventoryTracking.REASON_UNCOMPLETED,SupplyLedgers.GetForProcs(new List<long> { procedure.ProcNum }).Last().Reason);
			Assert.IsFalse(IsPending(procedure.ProcNum));
			CollectionAssert.DoesNotContain(SupplyLedgers.GetProcNumsHeldNotComplete(),procedure.ProcNum);
		}

		///<summary>A Restore row is written only while the procedure still holds exactly what it puts back: the same rows written twice (two
		///workstations restoring at once) put back once.</summary>
		[TestMethod]
		public void Restore_WrittenTwice_PutsBackOnce() {
			Supply supply=InvData.Supply(onHand:5,price:3);
			Procedure procedure=InvData.SetStatus(InvData.Proc(InvData.Patient(),InvData.Code((supply,2))),ProcStat.C);
			InventoryTracking.ConfirmMaterials(new List<long> { procedure.ProcNum },InventoryTracking.GetDefaultMaterials(new List<Procedure> { procedure }));
			Assert.AreEqual(3,InvData.OnHand(supply.SupplyNum));
			Func<List<SupplyLedger>> funcRows=() => new List<SupplyLedger> { new SupplyLedger { SupplyNum=supply.SupplyNum,SupplyLotNum=0,
				LedgerType=SupplyLedgerType.Restore,Quantity=2,UnitCost=3,ProcNum=procedure.ProcNum,PatNum=procedure.PatNum,Reason="Twice" } };
			Assert.AreEqual(1,SupplyLedgers.InsertRestoresIfStillHeld(funcRows()).Count);
			Assert.AreEqual(0,SupplyLedgers.InsertRestoresIfStillHeld(funcRows()).Count,"Held 0 now, not 2.");
			Assert.AreEqual(5,InvData.OnHand(supply.SupplyNum));
			InventoryTracking.RestoreForProc(procedure.ProcNum,"Again");
			Assert.AreEqual(5,InvData.OnHand(supply.SupplyNum));
			Assert.AreEqual(2,SupplyLedgers.GetForProcs(new List<long> { procedure.ProcNum }).Count);
		}

		///<summary>An implant that came in as an opening balance has no lot. The assistant enters the lot number from the package (SupplyLots.GetOrInsert
		///creates it); confirming moves the quantity from the stock without a lot into the lot, then takes it from the lot. Confirming again moves
		///nothing more.</summary>
		[TestMethod]
		public void Implant_LotEnteredForStockWithoutALot_IsMovedIntoTheLot() {
			Supply supplyImplant=InvData.Supply(onHand:2,isImplant:true);
			Procedure procedure=InvData.SetStatus(InvData.Proc(InvData.Patient(),InvData.Code((supplyImplant,1))),ProcStat.C);
			string lotNumber="PKG-"+InvData.Next();
			SupplyLot supplyLot=SupplyLots.GetOrInsert(supplyImplant.SupplyNum,lotNumber,DateTime.Today.AddYears(3),0);
			List<SupplyUsageLine> listLines=new List<SupplyUsageLine> { new SupplyUsageLine(procedure.ProcNum,supplyImplant.SupplyNum,supplyLot.SupplyLotNum,1) };
			InventoryTracking.ConfirmMaterials(new List<long> { procedure.ProcNum },listLines);
			Assert.AreEqual(0,InvData.LotQty(supplyLot.SupplyLotNum));
			Assert.AreEqual(1,SupplyLedgers.GetSumWithoutLot(supplyImplant.SupplyNum));
			Assert.AreEqual(1,InvData.OnHand(supplyImplant.SupplyNum));
			Assert.AreEqual(supplyLot.SupplyLotNum,InventoryTracking.GetProcMaterials(new List<long> { procedure.ProcNum }).Single().SupplyLotNum);
			List<SupplyLedger> listMoves=SupplyLedgers.GetForSupply(supplyImplant.SupplyNum).FindAll(x => x.LedgerType==SupplyLedgerType.Adjust);
			Assert.AreEqual(2,listMoves.Count);
			Assert.IsTrue(listMoves.All(x => x.Reason==InventoryTracking.REASON_MOVED_TO_LOT));
			Assert.AreEqual(0,Math.Round(listMoves.Sum(x => x.Quantity),2),"A move, not a change of stock.");
			InventoryTracking.ConfirmMaterials(new List<long> { procedure.ProcNum },listLines);
			Assert.AreEqual(2,SupplyLedgers.GetForSupply(supplyImplant.SupplyNum).Count(x => x.LedgerType==SupplyLedgerType.Adjust));
			Assert.AreEqual(1,InvData.CountLogs(EnumPermType.InventoryAdjust,"Stock without a lot moved into lot "+lotNumber));
		}

		///<summary>An implant deducted without a lot (no lot has stock) needs a reason, which goes to the audit trail; without one nothing is written.
		///</summary>
		[TestMethod]
		public void Implant_WithoutALot_NeedsAReason() {
			Supply supplyImplant=InvData.Supply(onHand:1,isImplant:true);
			Procedure procedure=InvData.SetStatus(InvData.Proc(InvData.Patient(),InvData.Code((supplyImplant,1))),ProcStat.C);
			List<SupplyUsageLine> listLines=InventoryTracking.GetDefaultMaterials(new List<Procedure> { procedure },isLotAuto:true);
			Assert.AreEqual(0,listLines.Single().SupplyLotNum,"No lot has stock.");
			InvData.Throws<ApplicationException>(() => InventoryTracking.ConfirmMaterials(new List<long> { procedure.ProcNum },listLines));
			Assert.AreEqual(1,InvData.OnHand(supplyImplant.SupplyNum));
			Assert.IsTrue(IsPending(procedure.ProcNum));
			string reason="Package discarded "+InvData.Next();
			InventoryTracking.ConfirmMaterials(new List<long> { procedure.ProcNum },listLines,reason);
			Assert.AreEqual(0,InvData.OnHand(supplyImplant.SupplyNum));
			Assert.IsFalse(IsPending(procedure.ProcNum));
			Assert.AreEqual(1,InvData.CountLogs(EnumPermType.InventoryAdjust,"Implant deducted without a lot number: "+supplyImplant.Descript));
			Assert.AreEqual(1,InvData.CountLogs(EnumPermType.InventoryAdjust,"Reason: "+reason));
			//Opened again unchanged: nothing new is taken without a lot, so no reason is needed.
			InventoryTracking.ConfirmMaterials(new List<long> { procedure.ProcNum },listLines);
		}

		///<summary>Procedures.SetCompleteInApptInList and Appointments.CompleteClick complete procedures on the Middle Tier server; the program raises
		///ProcSetComplete itself for those that are complete now and were not.</summary>
		[TestMethod]
		public void NotifyProcsNowComplete_RaisesForTheNewlyCompleted() {
			Procedure procedureWasTp=new Procedure { ProcNum=900001,ProcStatus=ProcStat.TP };
			Procedure procedureWasC=new Procedure { ProcNum=900002,ProcStatus=ProcStat.C };
			Procedure procedureStillTp=new Procedure { ProcNum=900003,ProcStatus=ProcStat.TP };
			List<Procedure> listAfter=new List<Procedure> {
				new Procedure { ProcNum=900001,ProcStatus=ProcStat.C },new Procedure { ProcNum=900002,ProcStatus=ProcStat.C },
				new Procedure { ProcNum=900003,ProcStatus=ProcStat.TP },new Procedure { ProcNum=900004,ProcStatus=ProcStat.C },//Added on the server.
			};
			List<long> listProcNumsRaised=new List<long>();
			Action<long> action=x => listProcNumsRaised.Add(x);
			InventoryTracking.ProcSetComplete+=action;
			try {
				InventoryTracking.NotifyProcsNowComplete(new List<Procedure> { procedureWasTp,procedureWasC,procedureStillTp },listAfter);
			}
			finally {
				InventoryTracking.ProcSetComplete-=action;
			}
			CollectionAssert.AreEqual(new long[] { 900001,900004 },listProcNumsRaised);
		}

		///<summary>Merging two patients moves the procedures to the patient kept; the ledger rows and the awaiting rows follow them, so the stock
		///window and a recall by lot name the chart that is kept, not the archived one.</summary>
		[TestMethod]
		public void PatientMerge_LedgerAndAwaitingRowsFollowTheProcedures() {
			Supply supply=InvData.Supply(onHand:5);
			long codeNum=InvData.Code((supply,2));
			long patNumFrom=InvData.Patient();
			long patNumTo=InvData.Patient();
			Procedure procedureUsed=InvData.SetStatus(InvData.Proc(patNumFrom,codeNum),ProcStat.C);
			InventoryTracking.ConfirmMaterials(new List<long> { procedureUsed.ProcNum },InventoryTracking.GetDefaultMaterials(new List<Procedure> { procedureUsed }));
			Procedure procedureAwaiting=InvData.SetStatus(InvData.Proc(patNumFrom,codeNum),ProcStat.C);
			Assert.IsTrue(IsPending(procedureAwaiting.ProcNum));
			Patients.MergeTwoPatientPointOfNoReturn(patNumTo,patNumFrom,new List<PatField>(),new List<PatField>());
			Assert.AreEqual(patNumTo,Procedures.GetOneProc(procedureUsed.ProcNum,false).PatNum);
			List<SupplyLedger> listSupplyLedgers=SupplyLedgers.GetForProcs(new List<long> { procedureUsed.ProcNum });
			Assert.AreEqual(1,listSupplyLedgers.Count);
			Assert.AreEqual(patNumTo,listSupplyLedgers[0].PatNum,"The Use row names the patient kept.");
			Assert.AreEqual(patNumTo,SupplyProcPendings.GetForProcs(new List<long> { procedureAwaiting.ProcNum }).Single().PatNum);
			//Un-completing after the merge puts back under the patient kept.
			InvData.SetStatus(procedureUsed,ProcStat.TP);
			Assert.AreEqual(5,InvData.OnHand(supply.SupplyNum));
			Assert.IsTrue(SupplyLedgers.GetForProcs(new List<long> { procedureUsed.ProcNum }).All(x => x.PatNum==patNumTo));
			//Every PatNum column of the inventory tables is moved by a merge.
			List<string> listColumns=DataCore.GetList<string>("SELECT CONCAT(TABLE_NAME,'.',COLUMN_NAME) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() "
				+"AND COLUMN_NAME LIKE '%PatNum%' AND TABLE_NAME IN('"+string.Join("','",InvDb.ArrayInventoryTables)+"')",
				x => x.GetString(0));
			CollectionAssert.AreEquivalent(new[] { "supplyledger.PatNum","supplyprocpending.PatNum" },listColumns);
			foreach(string column in listColumns) {
				Assert.IsTrue(Patients.StringArrayPatNumForeignKeys.Contains(column),column+" is moved by a patient merge.");
			}
		}

		[TestMethod]
		public void TrackingOff_CompletingRecordsNothing_ButUncompletingStillRestores() {
			Supply supply=InvData.Supply(onHand:10);
			long codeNum=InvData.Code((supply,2));
			Procedure procedureUsed=InvData.SetStatus(InvData.Proc(InvData.Patient(),codeNum),ProcStat.C);
			InventoryTracking.ConfirmMaterials(new List<long> { procedureUsed.ProcNum },InventoryTracking.GetDefaultMaterials(new List<Procedure> { procedureUsed }));
			List<long> listProcNumsRaised=new List<long>();
			Action<long> action=x => listProcNumsRaised.Add(x);
			Prefs.UpdateBool(PrefName.InventoryTrackingEnabled,false);
			Cache.Refresh(InvalidType.Prefs);
			InventoryTracking.ProcSetComplete+=action;
			try {
				Assert.IsFalse(InventoryTracking.IsEnabled());
				Procedure procedure=InvData.SetStatus(InvData.Proc(InvData.Patient(),codeNum),ProcStat.C);
				Assert.IsFalse(IsPending(procedure.ProcNum));
				Assert.AreEqual(0,listProcNumsRaised.Count,"The panel never opens.");
				InvData.SetStatus(procedureUsed,ProcStat.TP);
				Assert.AreEqual(10,InvData.OnHand(supply.SupplyNum),"Stock stays right.");
			}
			finally {
				InventoryTracking.ProcSetComplete-=action;
				Prefs.UpdateBool(PrefName.InventoryTrackingEnabled,true);
				Cache.Refresh(InvalidType.Prefs);
			}
		}
	}
}
