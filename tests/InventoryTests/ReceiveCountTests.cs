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
