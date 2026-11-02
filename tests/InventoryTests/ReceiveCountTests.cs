// Receiving orders (with lots and expiry dates, partial and full receipts) and stock counts (differences against the program's quantity when the
// line was counted, lots found in the count, the reason and the audit entries). Also the barcode lookup.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace InventoryTests {
	[TestClass]
	public class ReceiveCountTests {
		[TestInitialize]
		public void TestInitialize() {
			InvDb.Begin();
		}

		[TestMethod]
		public void Receive_RaisesStockInLots_AtTheOrderPrice_AndDatesTheOrderWhenComplete() {
			Supply supplyLot=InvData.Supply(onHand:1,isLotTracked:true);
			Supply supplyPlain=InvData.Supply(onHand:0);
			List<SupplyOrderItem> listItems=InvData.Order((supplyLot,4,7.5),(supplyPlain,10,1.25));
			string lotNumber="RCV"+InvData.Next();
			//Part of the lot-tracked item arrives first.
			InventoryTracking.Receive(new List<SupplyReceiveLine> {
				new SupplyReceiveLine { SupplyOrderItemNum=listItems[0].SupplyOrderItemNum,Quantity=3,LotNumber=lotNumber,DateExpiry=new DateTime(2028,2,1) } });
			SupplyLot supplyLotReceived=SupplyLots.GetForSupply(supplyLot.SupplyNum,isEmptyIncluded:false).Single();
			Assert.AreEqual(lotNumber,supplyLotReceived.LotNumber);
			Assert.AreEqual(new DateTime(2028,2,1),supplyLotReceived.DateExpiry);
			Assert.AreEqual(listItems[0].SupplyOrderItemNum,supplyLotReceived.SupplyOrderItemNum);
			Assert.AreEqual(3,InvData.LotQty(supplyLotReceived.SupplyLotNum));
			Assert.AreEqual(4,InvData.OnHand(supplyLot.SupplyNum));
			SupplyLedger supplyLedger=SupplyLedgers.GetForSupply(supplyLot.SupplyNum).Last();
			Assert.AreEqual(SupplyLedgerType.Receive,supplyLedger.LedgerType);
			Assert.AreEqual(7.5,supplyLedger.UnitCost);
			Assert.AreEqual(listItems[0].SupplyOrderItemNum,supplyLedger.SupplyOrderItemNum);
			Assert.IsTrue(SupplyOrderItems.SelectOne(listItems[0].SupplyOrderItemNum).DateReceived.Year<1880,"Not fully received yet.");
			SupplyOrder supplyOrder=SupplyOrders.GetOne(listItems[0].SupplyOrderNum);
			Assert.IsTrue(supplyOrder.DateReceived.Year<1880);
			//The rest: same lot number again adds to the lot; the plain item has no lot.
			InventoryTracking.Receive(new List<SupplyReceiveLine> {
				new SupplyReceiveLine { SupplyOrderItemNum=listItems[0].SupplyOrderItemNum,Quantity=1,LotNumber=lotNumber,DateExpiry=new DateTime(2028,2,1) },
				new SupplyReceiveLine { SupplyOrderItemNum=listItems[1].SupplyOrderItemNum,Quantity=10 } });
			Assert.AreEqual(4,InvData.LotQty(supplyLotReceived.SupplyLotNum));
			Assert.AreEqual(5,InvData.OnHand(supplyLot.SupplyNum));
			Assert.AreEqual(10,InvData.OnHand(supplyPlain.SupplyNum));
			Assert.AreEqual(0,SupplyLedgers.GetForSupply(supplyPlain.SupplyNum).Single().SupplyLotNum);
			Assert.AreEqual(DateTime.Today,SupplyOrderItems.SelectOne(listItems[0].SupplyOrderItemNum).DateReceived);
			Assert.AreEqual(DateTime.Today,SupplyOrderItems.SelectOne(listItems[1].SupplyOrderItemNum).DateReceived);
			Assert.AreEqual(DateTime.Today,SupplyOrders.GetOne(supplyOrder.SupplyOrderNum).DateReceived);
			Assert.AreEqual(3,SupplyLedgers.GetReceiptsForOrderItems(listItems.Select(x => x.SupplyOrderItemNum).ToList()).Count);
			Assert.IsTrue(InvData.CountLogs(EnumPermType.SupplyPurchases,"Order received: ")>=2);
		}

		[TestMethod]
		public void PostCount_WritesTheDifferenceToTheProgramQuantityWhenCounted() {
			Supply supply=InvData.Supply(onHand:10);
			//The line is counted with the program at 10; then a procedure uses 1 before the count is posted.
			SupplyCountLine supplyCountLine=new SupplyCountLine { SupplyNum=supply.SupplyNum,QtyProgram=10,QtyCounted=8 };
			InventoryTracking.Adjust(supply.SupplyNum,0,-1,"Used during the count");
			int countAdjusted=InventoryTracking.PostCount(new List<SupplyCountLine> { supplyCountLine },"Quarterly count");
			Assert.AreEqual(1,countAdjusted);
			Assert.AreEqual(7,InvData.OnHand(supply.SupplyNum),"Counted 8 before the use of 1: 7 now, not 8.");
			SupplyLedger supplyLedger=SupplyLedgers.GetForSupply(supply.SupplyNum).Last();
			Assert.AreEqual(SupplyLedgerType.CountAdjust,supplyLedger.LedgerType);
			Assert.AreEqual(-2,supplyLedger.Quantity);
			Assert.AreEqual("Quarterly count",supplyLedger.Reason);
			Assert.AreEqual(1,InvData.CountLogs(EnumPermType.InventoryAdjust,"Stock count: "+supply.Descript+": program had 10, counted 8, adjusted by -2. Reason: Quarterly count"));
		}

		[TestMethod]
		public void PostCount_NeedsAReason_SkipsEqualLines_AndCreatesLotsFoundInTheCount() {
			Supply supply=InvData.Supply(onHand:5,isLotTracked:true);
			Supply supplyEqual=InvData.Supply(onHand:3);
			string lotNumber="CNT"+InvData.Next();
			List<SupplyCountLine> listLines=new List<SupplyCountLine> {
				new SupplyCountLine { SupplyNum=supply.SupplyNum,SupplyLotNum=0,LotNumber=lotNumber,DateExpiry=new DateTime(2027,9,1),QtyProgram=0,QtyCounted=2 },
				new SupplyCountLine { SupplyNum=supplyEqual.SupplyNum,QtyProgram=3,QtyCounted=3 },
			};
			InvData.Throws<ApplicationException>(() => InventoryTracking.PostCount(listLines,""));
			Assert.AreEqual(5,InvData.OnHand(supply.SupplyNum));
			Assert.AreEqual(1,InventoryTracking.PostCount(listLines,"Found a box"));
			SupplyLot supplyLot=SupplyLots.GetForSupply(supply.SupplyNum,isEmptyIncluded:false).Single();
			Assert.AreEqual(lotNumber,supplyLot.LotNumber);
			Assert.AreEqual(new DateTime(2027,9,1),supplyLot.DateExpiry);
			Assert.AreEqual(2,InvData.LotQty(supplyLot.SupplyLotNum));
			Assert.AreEqual(7,InvData.OnHand(supply.SupplyNum));
			Assert.AreEqual(1,SupplyLedgers.GetForSupply(supplyEqual.SupplyNum).Count,"Only the opening row.");
		}

		///<summary>Once the ledger is written, a failure of what follows (here the audit entry: the securitylog table is renamed away for the moment)
		///is logged, not reported: the windows would otherwise keep what was entered, and posting it again would write it twice.</summary>
		[TestMethod]
		public void ReceiveCountAdjust_FailureAfterTheLedgerWrite_IsNotReportedAsAFailure() {
			Supply supply=InvData.Supply(onHand:4);
			List<SupplyOrderItem> listItems=InvData.Order((supply,3,2.0));
			string loggerDirOld=CodeBase.Logger.LoggerDirOverride;
			CodeBase.Logger.LoggerDirOverride=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"TestLogs");
			DataCore.NonQ("RENAME TABLE securitylog TO securitylog_inv_off");
			try {
				InventoryTracking.Receive(new List<SupplyReceiveLine> { new SupplyReceiveLine { SupplyOrderItemNum=listItems[0].SupplyOrderItemNum,Quantity=3 } });
				Assert.AreEqual(7,InvData.OnHand(supply.SupplyNum));
				Assert.AreEqual(1,InventoryTracking.PostCount(new List<SupplyCountLine> { new SupplyCountLine { SupplyNum=supply.SupplyNum,QtyProgram=7,QtyCounted=6 } },
					"Count"));
				Assert.AreEqual(6,InvData.OnHand(supply.SupplyNum));
				InventoryTracking.Adjust(supply.SupplyNum,0,-1,"Broken");
				Assert.AreEqual(5,InvData.OnHand(supply.SupplyNum));
			}
			finally {
				DataCore.NonQ("RENAME TABLE securitylog_inv_off TO securitylog");
				CodeBase.Logger.LoggerDirOverride=loggerDirOld;
			}
			Assert.AreEqual(DateTime.Today,SupplyOrderItems.SelectOne(listItems[0].SupplyOrderItemNum).DateReceived,"The other follow-ups still ran.");
			Assert.AreEqual(4,SupplyLedgers.GetForSupply(supply.SupplyNum).Count,"Opening, receipt, count, adjustment: each once.");
		}

		///<summary>A post that failed inside SupplyLedgers.InsertMany can leave a CountAdjust row written and the lot counter at its old value (the
		///recompute comes last). The count's program quantity is read from the ledger, so posting the same count line again writes nothing more.</summary>
		[TestMethod]
		public void CountProgramQuantity_IsTheLedgerSum_SoARepostAfterAMissedRecomputeWritesNothingMore() {
			Supply supply=InvData.Supply(isLotTracked:true);
			SupplyLot supplyLot=InvData.Lot(supply,"CQ"+InvData.Next(),DateTime.Today.AddMonths(6),5);
			Assert.AreEqual(5,InventoryTracking.GetQtyProgram(supply.SupplyNum,supplyLot.SupplyLotNum));
			//The count said 3 (program 5): the CountAdjust row of -2 was written, then the post failed before the counters were recomputed.
			DataCore.NonQ("INSERT INTO supplyledger (SupplyNum,SupplyLotNum,LedgerType,Quantity,UnitCost,ProcNum,PatNum,SupplyOrderItemNum,DispSupplyNum,"
				+"UserNum,DateTimeEntry,Reason) VALUES ("+supply.SupplyNum+","+supplyLot.SupplyLotNum+","+(int)SupplyLedgerType.CountAdjust+",-2,0,0,0,0,0,1,NOW(),"
				+"'Quarterly count')");
			Assert.AreEqual(5,InvData.LotQty(supplyLot.SupplyLotNum),"The counter missed the row.");
			double qtyProgram=InventoryTracking.GetQtyProgram(supply.SupplyNum,supplyLot.SupplyLotNum);
			Assert.AreEqual(3,qtyProgram,"The ledger sum, not the counter.");
			Assert.AreEqual(3,InventoryTracking.GetCountLines(supply.SupplyNum).Single(x => x.SupplyLotNum==supplyLot.SupplyLotNum).QtyProgram);
			//The window reloads the program quantity (3) and the same count (3) is posted again: nothing more is adjusted.
			int countAdjusted=InventoryTracking.PostCount(new List<SupplyCountLine> { new SupplyCountLine { SupplyNum=supply.SupplyNum,
				SupplyLotNum=supplyLot.SupplyLotNum,LotNumber=supplyLot.LotNumber,QtyProgram=qtyProgram,QtyCounted=3 } },"Quarterly count");
			Assert.AreEqual(0,countAdjusted);
			Assert.AreEqual(3,SupplyLedgers.GetSumForLot(supplyLot.SupplyLotNum));
			Assert.AreEqual(1,SupplyLedgers.GetForSupply(supply.SupplyNum).Count(x => x.LedgerType==SupplyLedgerType.CountAdjust),"Adjusted once.");
			//The window reads the ledger through these, never the counters, and so does every saved line's snapshot. Opening a count again after a
			//failed post never reads a snapshot again (today's quantity has the stock used since the line was counted in it): each line takes its own
			//adjustments into its snapshot, from the ledger rows that name it.
			string source=System.IO.File.ReadAllText(InvDb.FindRepoFile(System.IO.Path.Combine("OpenPmsDental","Forms","FormSupplyCount.cs")));
			Assert.IsFalse(source.Contains("QtyOnHand"),"FormSupplyCount reads no lot counter.");
			StringAssert.Contains(source,"InventoryTracking.GetCountLines(supply.SupplyNum)");
			Assert.AreEqual(1,source.Split(new[] { "InventoryTracking.GetQtyProgram(" },StringSplitOptions.None).Length-1,"A lot found by its number.");
			string sourceBusiness=System.IO.File.ReadAllText(InvDb.FindRepoFile(System.IO.Path.Combine("OpenPmsDentalBusiness","Db Multi Table","InventoryTracking.cs")));
			int indexReopen=sourceBusiness.IndexOf("private static void ReopenAfterFailedPost(");
			int indexSave=sourceBusiness.IndexOf("public static SupplyCountLine SaveCountLine(");
			int indexResume=sourceBusiness.IndexOf("public static SupplyCount ResumeCount(");
			Assert.IsTrue(indexReopen>0 && indexSave>0 && indexResume>indexReopen);
			string sourceReopen=sourceBusiness.Substring(indexReopen,indexResume-indexReopen);
			Assert.IsFalse(sourceReopen.Contains("GetQtyProgram("),"No snapshot is read again after a failed post.");
			StringAssert.Contains(sourceReopen,"SupplyCountLines.TakeAdjustmentsIntoSnapshotsWhilePosting(supplyCountNum)");
			StringAssert.Contains(sourceBusiness.Substring(indexSave,indexReopen>indexSave ? indexReopen-indexSave : sourceBusiness.Length-indexSave),"GetQtyProgram(");
			SupplyLedgers.RecomputeForSupplies(new List<long> { supply.SupplyNum });
			SupplyLedgers.RecomputeForLots(new List<long> { supplyLot.SupplyLotNum });
		}

		///<summary>A lot below zero gets a count row (a lot taken past its stock), so a full count corrects the supply: lot A at -1 and lot B at 2,
		///with 1 on the shelf (in lot B). Counting A as 0 and B as 1 leaves On Hand 1 and each lot at its count.</summary>
		[TestMethod]
		public void CountLines_ListLotsBelowZero_SoAFullCountCorrectsTheSupply() {
			Supply supply=InvData.Supply(isLotTracked:true);
			int n=InvData.Next();
			SupplyLot supplyLotA=InvData.Lot(supply,"AB21-"+n,DateTime.Today.AddMonths(2),0);
			InventoryTracking.Adjust(supply.SupplyNum,supplyLotA.SupplyLotNum,-1,"Taken past its stock");
			SupplyLot supplyLotB=InvData.Lot(supply,"AB12-"+n,DateTime.Today.AddMonths(5),2);
			SupplyLot supplyLotEmpty=InvData.Lot(supply,"EMPTY-"+n,DateTime.Today.AddMonths(1),0);
			Assert.AreEqual(1,InvData.OnHand(supply.SupplyNum));
			List<SupplyCountLine> listLines=InventoryTracking.GetCountLines(supply.SupplyNum);
			CollectionAssert.AreEqual(new[] { supplyLotA.SupplyLotNum,supplyLotB.SupplyLotNum },listLines.Select(x => x.SupplyLotNum).ToList(),
				"Both lots with stock other than 0, soonest-expiring first; no line for the empty lot or for stock without a lot (there is none).");
			Assert.AreEqual(-1,listLines[0].QtyProgram);
			Assert.AreEqual(2,listLines[1].QtyProgram);
			Assert.AreEqual("AB21-"+n,listLines[0].LotNumber);
			listLines[0].QtyCounted=0;
			listLines[1].QtyCounted=1;
			Assert.AreEqual(2,InventoryTracking.PostCount(listLines,"Full count"));
			Assert.AreEqual(1,InvData.OnHand(supply.SupplyNum),"On Hand matches the shelf.");
			Assert.AreEqual(0,InvData.LotQty(supplyLotA.SupplyLotNum));
			Assert.AreEqual(1,InvData.LotQty(supplyLotB.SupplyLotNum));
			Assert.AreEqual(0,InvData.LotQty(supplyLotEmpty.SupplyLotNum));
			//A supply not tracked by lot always has its line of stock without a lot, also at 0.
			Supply supplyPlain=InvData.Supply(onHand:0);
			SupplyCountLine supplyCountLinePlain=InventoryTracking.GetCountLines(supplyPlain.SupplyNum).Single();
			Assert.AreEqual(0,supplyCountLinePlain.SupplyLotNum);
			Assert.AreEqual(0,supplyCountLinePlain.QtyProgram);
		}

		[TestMethod]
		public void GetByScan_MatchesTheBarcode_ThenTheCatalogNumber() {
			string barcode="0"+InvData.Next()+"77123";
			Supply supply=InvData.Supply(barcode:barcode);
			Assert.AreEqual(supply.SupplyNum,Supplies.GetByScan(" "+barcode+" ").Single().SupplyNum);
			Supply supplyCatalog=InvData.Supply();
			Assert.AreEqual(supplyCatalog.SupplyNum,Supplies.GetByScan(supplyCatalog.CatalogNumber).Single().SupplyNum);
			Assert.AreEqual(0,Supplies.GetByScan("").Count);
			Assert.AreEqual(0,Supplies.GetByScan("NO-SUCH-CODE-"+InvData.Next()).Count);
		}
	}
}
