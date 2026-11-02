// Stock counts saved while they are counted (owner decision INV 11; docs/specs/inventory-tracking.md, 2.7 and 3.5): lines saved at once and shown
// again after the window is closed, resumed by another user, one open count only, a recount replacing its line with a new snapshot, two
// workstations saving into the same count at the same moment, a post after a resume giving the same results as an uninterrupted count, a failed
// post and a post that did not finish, a discard with its audit entry, and Supply Inventory's line about the open count.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;

namespace InventoryTests {
	[TestClass]
	public class CountTests {
		[TestInitialize]
		public void TestInitialize() {
			InvDb.Begin();
			InvData.CloseOpenCounts();
		}

		[TestCleanup]
		public void TestCleanup() {
			if(InvDb.SkipReason!=null) {
				return;
			}
			InventoryTracking.ActionAfterCountPosted=null;
			InvData.CloseOpenCounts();
			Security.CurUser=Userods.GetUser(1)??new Userod { UserNum=1,UserName="Admin" };
		}

		private static OpenPmsDental.UI.GridOD GetGrid(Form form) {
			return WindowTests.GetPrivate<OpenPmsDental.UI.GridOD>(form,"gridMain");
		}

		///<summary>The Stock Count window built and loaded in memory, with a window handle but never shown. Each scan and the typed quantity are in
		///the database the moment they are entered; the window is closed without posting (no warning: nothing is lost), and a new window, as after
		///closing the program, shows the same count with the same lines.</summary>
		[TestMethod]
		public void SavedLines_AreInTheDatabaseAtOnce_AndTheWindowShowsThemAgainAfterItIsClosed() {
			string barcode="CNTWIN"+InvData.Next();
			Supply supplyScan=InvData.Supply(onHand:5,barcode:barcode);
			Supply supplyTyped=InvData.Supply(onHand:12);
			long supplyCountNum=0;
			List<SupplyCountLine> listAfterFirstScan=null;
			DialogResult dialogResult=DialogResult.None;
			WindowTests.RunSta(() => {
				using FormSupplyCount form=new FormSupplyCount();
				Assert.AreNotEqual(IntPtr.Zero,form.Handle);
				WindowTests.InvokePrivate(form,"FormSupplyCount_Load",form,EventArgs.Empty);
				OpenPmsDental.UI.GridOD grid=GetGrid(form);
				TextBox textScan=WindowTests.GetPrivate<TextBox>(form,"textScan");
				Assert.AreEqual(0,grid.ListGridRows.Count,"No count is open.");
				textScan.Text=barcode;
				WindowTests.InvokePrivate(form,"textScan_KeyDown",textScan,new KeyEventArgs(Keys.Enter));
				SupplyCount supplyCount=InventoryTracking.GetOpenCount();
				Assert.IsNotNull(supplyCount,"The first scan started a count.");
				supplyCountNum=supplyCount.SupplyCountNum;
				listAfterFirstScan=SupplyCountLines.GetForCount(supplyCountNum);
				textScan.Text=barcode;
				WindowTests.InvokePrivate(form,"textScan_KeyDown",textScan,new KeyEventArgs(Keys.Enter));
				WindowTests.InvokePrivate(form,"AddRowsForSupply",supplyTyped);
				WindowTests.InvokePrivate(form,"FillGrid");
				int indexTyped=grid.ListGridRows.Count-1;
				Assert.AreEqual("",grid.ListGridRows[indexTyped].Cells[4].Text,"Added, not counted yet.");
				Assert.AreEqual(1,SupplyCountLines.GetForCount(supplyCountNum).Count,"A row not counted yet is not saved.");
				grid.ListGridRows[indexTyped].Cells[4].Text="11";
				WindowTests.InvokePrivate(form,"gridMain_CellLeave",grid,new ODGridClickEventArgs(4,indexTyped,MouseButtons.Left));
				WindowTests.InvokePrivate(form,"butClose_Click",form,EventArgs.Empty);
				dialogResult=form.DialogResult;
			});
			Assert.AreEqual(DialogResult.OK,dialogResult,"Closed without a warning.");
			SupplyCountLine supplyCountLineFirst=listAfterFirstScan.Single();
			Assert.AreEqual(supplyScan.SupplyNum,supplyCountLineFirst.SupplyNum);
			Assert.AreEqual(1,supplyCountLineFirst.QtyCounted,"Saved with the first scan.");
			Assert.AreEqual(5,supplyCountLineFirst.QtyProgram);
			List<SupplyCountLine> listSaved=SupplyCountLines.GetForCount(supplyCountNum);
			CollectionAssert.AreEqual(new[] { supplyScan.SupplyNum,supplyTyped.SupplyNum },listSaved.Select(x => x.SupplyNum).ToArray());
			CollectionAssert.AreEqual(new[] { 2.0,11.0 },listSaved.Select(x => x.QtyCounted).ToArray());
			Assert.AreEqual(5,InvData.OnHand(supplyScan.SupplyNum),"Nothing is posted.");
			//Opened again, as after closing the window or the program: the same count and lines.
			List<string> listRowTexts=null;
			RunStaLoaded(form => {
				listRowTexts=GetGrid(form).ListGridRows.Select(x => string.Join("|",x.Cells.Select(y => y.Text))).ToList();
			});
			CollectionAssert.AreEqual(new[] { supplyScan.Descript+"|(no lot)||5|2|-3",supplyTyped.Descript+"|(no lot)||12|11|-1" },listRowTexts);
			Assert.AreEqual(supplyCountNum,InventoryTracking.GetOpenCount().SupplyCountNum);
		}

		///<summary>Builds and loads the Stock Count window on an STA thread, runs the action on it, and disposes it.</summary>
		private static void RunStaLoaded(Action<FormSupplyCount> action,Userod userod=null) {
			WindowTests.RunSta(() => {
				if(userod!=null) {
					Security.CurUser=userod;
				}
				using FormSupplyCount form=new FormSupplyCount();
				Assert.AreNotEqual(IntPtr.Zero,form.Handle);
				WindowTests.InvokePrivate(form,"FormSupplyCount_Load",form,EventArgs.Empty);
				action(form);
			});
		}

		///<summary>A count started by one user is resumed by another on another workstation: the window shows every line, the second user's lines
		///go into the same count, and the count still names who started it.</summary>
		[TestMethod]
		public void Count_IsResumedByAnotherUser() {
			Userod userodA=Security.CurUser;
			Userod userodB=InvData.User();
			Supply supplyA=InvData.Supply(onHand:3);
			Supply supplyB=InvData.Supply(onHand:4);
			string barcode="CNTRES"+InvData.Next();
			Supply supplyC=InvData.Supply(onHand:6,barcode:barcode);
			SupplyCountLine supplyCountLine1=InvData.CountLine(supplyA,2);
			SupplyCountLine supplyCountLine2=InvData.CountLine(supplyB,4);
			long supplyCountNum=supplyCountLine1.SupplyCountNum;
			Assert.AreEqual(supplyCountNum,supplyCountLine2.SupplyCountNum);
			Security.CurUser=userodB;
			SupplyCount supplyCount=InventoryTracking.GetOpenCount();
			Assert.AreEqual(supplyCountNum,supplyCount.SupplyCountNum);
			Assert.AreEqual(userodA.UserNum,supplyCount.UserNum);
			string description=InventoryTracking.GetOpenCountDescription();
			StringAssert.Contains(description,"Stock count open: started by "+userodA.UserName+" on ");
			StringAssert.Contains(description,", 2 line(s).");
			int countRowsLoaded=0;
			RunStaLoaded(form => {
				countRowsLoaded=GetGrid(form).ListGridRows.Count;
				TextBox textScan=WindowTests.GetPrivate<TextBox>(form,"textScan");
				textScan.Text=barcode;
				WindowTests.InvokePrivate(form,"textScan_KeyDown",textScan,new KeyEventArgs(Keys.Enter));
			},userodB);
			Assert.AreEqual(2,countRowsLoaded,"The other user sees both lines.");
			List<SupplyCountLine> listSupplyCountLines=InventoryTracking.GetSavedCountLines(supplyCountNum);
			CollectionAssert.AreEqual(new[] { supplyA.SupplyNum,supplyB.SupplyNum,supplyC.SupplyNum },listSupplyCountLines.Select(x => x.SupplyNum).ToArray());
			CollectionAssert.AreEqual(new[] { userodA.UserNum,userodA.UserNum,userodB.UserNum },listSupplyCountLines.Select(x => x.UserNum).ToArray());
			Assert.AreEqual(userodA.UserNum,SupplyCounts.GetOne(supplyCountNum).UserNum,"Still started by the first user.");
			Assert.AreEqual(1,InvData.CountOpenCounts());
		}

		///<summary>The office has at most one open count: every line, from any workstation, joins it; while its post runs nothing can start another
		///or save a line; once it is closed the next line starts a new one.</summary>
		[TestMethod]
		public void OneOpenCountOnly() {
			Supply supply1=InvData.Supply(onHand:2);
			Supply supply2=InvData.Supply(onHand:2);
			SupplyCountLine supplyCountLine1=InvData.CountLine(supply1,1);
			SupplyCountLine supplyCountLine2=InvData.CountLine(supply2,1);
			long supplyCountNum=supplyCountLine1.SupplyCountNum;
			Assert.AreEqual(supplyCountNum,supplyCountLine2.SupplyCountNum,"One count.");
			Assert.AreEqual(supplyCountNum,SupplyCounts.InsertOpenIfNone(1).SupplyCountNum,"Starting again joins the open count.");
			Assert.AreEqual(1,InvData.CountOpenCounts());
			//While its post runs (Posting), no count can start and no line is saved into it.
			Assert.IsTrue(SupplyCounts.SetStatus(supplyCountNum,SupplyCountStatus.Open,SupplyCountStatus.Posting,1,"Test"));
			Assert.IsFalse(SupplyCounts.SetStatus(supplyCountNum,SupplyCountStatus.Open,SupplyCountStatus.Posting,1,"Test"),"Claimed once only.");
			Assert.IsNull(SupplyCounts.InsertOpenIfNone(1));
			InvData.Throws<ApplicationException>(() => InvData.CountLine(supply1,5));
			InvData.Throws<ApplicationException>(() => InvData.CountLine(supply1,5,supplyCountNum:supplyCountNum));
			Assert.AreEqual(1,InvData.CountOpenCounts());
			Assert.AreEqual(1,SupplyCountLines.GetForCount(supplyCountNum).Single(x => x.SupplyNum==supply1.SupplyNum).QtyCounted,"Not changed.");
			Assert.IsTrue(SupplyCounts.SetStatus(supplyCountNum,SupplyCountStatus.Posting,SupplyCountStatus.Open,0,""));
			Assert.AreEqual(2,InventoryTracking.PostOpenCount(supplyCountNum,"Count"));
			Assert.AreEqual(SupplyCountStatus.Posted,SupplyCounts.GetOne(supplyCountNum).CountStatus);
			Assert.IsNull(InventoryTracking.GetOpenCount(),"Posting closed it.");
			long supplyCountNumNext=InvData.CountLine(supply1,1).SupplyCountNum;
			Assert.AreNotEqual(supplyCountNum,supplyCountNumNext,"The next line starts a new count.");
			Assert.AreEqual(1,InvData.CountOpenCounts());
			Assert.AreEqual(SupplyCountStatus.Open,SupplyCounts.GetOne(supplyCountNumNext).CountStatus);
		}

		///<summary>A supply counted again replaces its line: one line, the new counted quantity and a new snapshot of the program's quantity at that
		///moment (a use during the count is in it). A scan adds one to the line, also with a new snapshot. The post compares with the last snapshot.
		///</summary>
		[TestMethod]
		public void Recount_ReplacesTheLine_WithANewSnapshot() {
			Supply supply=InvData.Supply(onHand:10);
			SupplyCountLine supplyCountLineFirst=InvData.CountLine(supply,8);
			Assert.AreEqual(10,supplyCountLineFirst.QtyProgram);
			Assert.AreEqual(8,supplyCountLineFirst.QtyCounted);
			Assert.AreEqual(Security.CurUser.UserNum,supplyCountLineFirst.UserNum);
			Assert.IsTrue(supplyCountLineFirst.DateTimeEntry.Year>1880,"Server time.");
			InventoryTracking.Adjust(supply.SupplyNum,0,-1,"Used during the count");
			SupplyCountLine supplyCountLineAgain=InvData.CountLine(supply,7);
			Assert.AreEqual(supplyCountLineFirst.SupplyCountLineNum,supplyCountLineAgain.SupplyCountLineNum,"The same line, replaced.");
			Assert.AreEqual(9,supplyCountLineAgain.QtyProgram,"A new snapshot.");
			Assert.AreEqual(7,supplyCountLineAgain.QtyCounted);
			Assert.IsTrue(supplyCountLineAgain.DateTimeEntry>=supplyCountLineFirst.DateTimeEntry);
			InventoryTracking.Adjust(supply.SupplyNum,0,-1,"Used during the count");
			SupplyCountLine supplyCountLineScan=InvData.CountLine(supply,1,isAdded:true);
			Assert.AreEqual(8,supplyCountLineScan.QtyCounted,"A scan adds one.");
			Assert.AreEqual(8,supplyCountLineScan.QtyProgram);
			long supplyCountNum=supplyCountLineFirst.SupplyCountNum;
			Assert.AreEqual(1,SupplyCountLines.GetForCount(supplyCountNum).Count);
			Assert.AreEqual(0,InventoryTracking.PostOpenCount(supplyCountNum,"Recount"),"Counted 8 with the program at 8.");
			Assert.AreEqual(8,InvData.OnHand(supply.SupplyNum));
			//Lots each have their own line; a recount of one lot leaves the other.
			Supply supplyLots=InvData.Supply(isLotTracked:true);
			SupplyLot supplyLotA=InvData.Lot(supplyLots,"RA"+InvData.Next(),DateTime.Today.AddMonths(2),4);
			SupplyLot supplyLotB=InvData.Lot(supplyLots,"RB"+InvData.Next(),DateTime.Today.AddMonths(4),5);
			InvData.CountLine(supplyLots,3,supplyLotNum:supplyLotA.SupplyLotNum);
			InvData.CountLine(supplyLots,5,supplyLotNum:supplyLotB.SupplyLotNum);
			SupplyCountLine supplyCountLineLotA=InvData.CountLine(supplyLots,4,supplyLotNum:supplyLotA.SupplyLotNum);
			List<SupplyCountLine> listLines=InventoryTracking.GetSavedCountLines(supplyCountLineLotA.SupplyCountNum);
			Assert.AreEqual(2,listLines.Count);
			Assert.AreEqual(4,listLines.Single(x => x.SupplyLotNum==supplyLotA.SupplyLotNum).QtyCounted);
			Assert.AreEqual(supplyLotA.LotNumber,listLines.Single(x => x.SupplyLotNum==supplyLotA.SupplyLotNum).LotNumber,"Filled in from the lot.");
			Assert.AreEqual("",SupplyCountLines.GetForCount(supplyCountLineLotA.SupplyCountNum).Single(x => x.SupplyLotNum==supplyLotA.SupplyLotNum).LotNumber,
				"Saved blank for an existing lot.");
		}

		///<summary>Two workstations start counting at the same moment and each saves its own lines while both scan the same item: one count, every
		///line once, every scan of the shared item counted, each line with its own user.</summary>
		[TestMethod]
		public void TwoWorkstations_SaveLinesIntoTheSameOpenCount_AtTheSameMoment() {
			const int countPerWorkstation=12;
			Userod userodA=Security.CurUser;
			Userod userodB=InvData.User();
			List<Supply> listSuppliesA=Enumerable.Range(0,countPerWorkstation).Select(x => InvData.Supply(onHand:5)).ToList();
			List<Supply> listSuppliesB=Enumerable.Range(0,countPerWorkstation).Select(x => InvData.Supply(onHand:5)).ToList();
			Supply supplyShared=InvData.Supply(onHand:30);
			Barrier barrier=new Barrier(2);
			Exception exceptionA=null;
			Exception exceptionB=null;
			Thread threadA=new Thread(() => exceptionA=RunWorkstation(barrier,userodA,listSuppliesA,supplyShared));
			Thread threadB=new Thread(() => exceptionB=RunWorkstation(barrier,userodB,listSuppliesB,supplyShared));
			threadA.Start();
			threadB.Start();
			threadA.Join();
			threadB.Join();
			Assert.IsNull(exceptionA,exceptionA?.ToString());
			Assert.IsNull(exceptionB,exceptionB?.ToString());
			Assert.AreEqual(1,InvData.CountOpenCounts(),"Both started at once: one count.");
			SupplyCount supplyCount=InventoryTracking.GetOpenCount();
			List<SupplyCountLine> listSupplyCountLines=InventoryTracking.GetSavedCountLines(supplyCount.SupplyCountNum);
			Assert.AreEqual(2*countPerWorkstation+1,listSupplyCountLines.Count,"Every line once.");
			Assert.AreEqual(listSupplyCountLines.Count,listSupplyCountLines.Select(x => x.SupplyNum).Distinct().Count());
			Assert.AreEqual(2*countPerWorkstation,listSupplyCountLines.Single(x => x.SupplyNum==supplyShared.SupplyNum).QtyCounted,"Every scan of the shared item.");
			for(int i=0;i<countPerWorkstation;i++) {
				SupplyCountLine supplyCountLineA=listSupplyCountLines.Single(x => x.SupplyNum==listSuppliesA[i].SupplyNum);
				SupplyCountLine supplyCountLineB=listSupplyCountLines.Single(x => x.SupplyNum==listSuppliesB[i].SupplyNum);
				Assert.AreEqual(i+1,supplyCountLineA.QtyCounted);
				Assert.AreEqual(i+1,supplyCountLineB.QtyCounted);
				Assert.AreEqual(userodA.UserNum,supplyCountLineA.UserNum);
				Assert.AreEqual(userodB.UserNum,supplyCountLineB.UserNum);
				Assert.AreEqual(5,supplyCountLineA.QtyProgram);
			}
		}

		///<summary>One workstation: its user, then a start at the same moment as the other, then its lines and a scan of the shared item after
		///each. Returns the exception it ended with, or null.</summary>
		private static Exception RunWorkstation(Barrier barrier,Userod userod,List<Supply> listSupplies,Supply supplyShared) {
			try {
				Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
				Security.CurUser=userod;
				barrier.SignalAndWait();
				for(int i=0;i<listSupplies.Count;i++) {
					InvData.CountLine(listSupplies[i],i+1);
					InvData.CountLine(supplyShared,1,isAdded:true);
				}
				return null;
			}
			catch(Exception ex) {
				return ex;
			}
		}

		///<summary>A shelf: a plain supply with 10, and a supply tracked by lot with lots of 4 and 6.</summary>
		private class Shelf {
			public Supply SupplyPlain;
			public Supply SupplyLotted;
			public SupplyLot SupplyLotSoon;
			public SupplyLot SupplyLotLate;
			public string LotNumberFound;

			public static Shelf Make() {
				Shelf shelf=new Shelf();
				int n=InvData.Next();
				shelf.SupplyPlain=InvData.Supply(onHand:10);
				shelf.SupplyLotted=InvData.Supply(isLotTracked:true);
				shelf.SupplyLotSoon=InvData.Lot(shelf.SupplyLotted,"SOON"+n,DateTime.Today.AddMonths(2),4);
				shelf.SupplyLotLate=InvData.Lot(shelf.SupplyLotted,"LATE"+n,DateTime.Today.AddMonths(8),6);
				shelf.LotNumberFound="FOUND"+n;
				return shelf;
			}

			///<summary>Stock and the count adjustments as the post left them: on hand, each lot, the found lot, and every CountAdjust quantity.</summary>
			public string Result() {
				List<SupplyLedger> listSupplyLedgers=SupplyLedgers.GetForSupply(SupplyPlain.SupplyNum).Concat(SupplyLedgers.GetForSupply(SupplyLotted.SupplyNum))
					.Where(x => x.LedgerType==SupplyLedgerType.CountAdjust).ToList();
				SupplyLot supplyLotFound=SupplyLots.GetForSupply(SupplyLotted.SupplyNum,isEmptyIncluded:true).Single(x => x.LotNumber==LotNumberFound);
				return InvData.OnHand(SupplyPlain.SupplyNum)+";"+InvData.OnHand(SupplyLotted.SupplyNum)+";"+InvData.LotQty(SupplyLotSoon.SupplyLotNum)+";"
					+InvData.LotQty(SupplyLotLate.SupplyLotNum)+";"+InvData.LotQty(supplyLotFound.SupplyLotNum)+";"+supplyLotFound.DateExpiry.ToString("yyyy-MM-dd")+";"
					+string.Join(",",listSupplyLedgers.Select(x => (x.SupplyNum==SupplyPlain.SupplyNum ? "plain" : x.SupplyLotNum==SupplyLotSoon.SupplyLotNum ? "soon"
						: x.SupplyLotNum==SupplyLotLate.SupplyLotNum ? "late" : "found")+":"+InventoryTracking.FormatQty(x.Quantity)+":"+x.Reason));
			}
		}

		///<summary>The same shelf counted twice: once without a break, once by one user who stops (the window and the program closed) and another
		///who resumes and posts. A procedure uses one of the plain supply during each count, after it was counted. The stock, the lots, the lot found
		///in the count and the count adjustments come out the same.</summary>
		[TestMethod]
		public void PostAfterResume_GivesTheSameResultsAsAnUninterruptedCount() {
			Userod userodB=InvData.User();
			DateTime dateExpiryFound=DateTime.Today.AddMonths(10);
			//Without a break.
			Shelf shelfOnce=Shelf.Make();
			long supplyCountNumOnce=InvData.CountLine(shelfOnce.SupplyPlain,8).SupplyCountNum;
			InvData.CountLine(shelfOnce.SupplyLotted,3,supplyLotNum:shelfOnce.SupplyLotSoon.SupplyLotNum,supplyCountNum:supplyCountNumOnce);
			InventoryTracking.Adjust(shelfOnce.SupplyPlain.SupplyNum,0,-1,"Used during the count");
			InvData.CountLine(shelfOnce.SupplyLotted,7,supplyLotNum:shelfOnce.SupplyLotLate.SupplyLotNum,supplyCountNum:supplyCountNumOnce);
			InvData.CountLine(shelfOnce.SupplyLotted,2,supplyCountNum:supplyCountNumOnce,lotNumber:shelfOnce.LotNumberFound,dateExpiry:dateExpiryFound);
			int countAdjustedOnce=InventoryTracking.PostOpenCount(supplyCountNumOnce,"Quarterly count");
			//Stopped half-way and resumed by another user.
			Shelf shelfResumed=Shelf.Make();
			long supplyCountNumResumed=InvData.CountLine(shelfResumed.SupplyPlain,8).SupplyCountNum;
			Assert.AreNotEqual(supplyCountNumOnce,supplyCountNumResumed);
			InvData.CountLine(shelfResumed.SupplyLotted,3,supplyLotNum:shelfResumed.SupplyLotSoon.SupplyLotNum,supplyCountNum:supplyCountNumResumed);
			InventoryTracking.Adjust(shelfResumed.SupplyPlain.SupplyNum,0,-1,"Used during the count");
			Security.CurUser=userodB;
			SupplyCount supplyCountResumed=InventoryTracking.ResumeCount(InventoryTracking.GetOpenCount().SupplyCountNum);
			Assert.AreEqual(supplyCountNumResumed,supplyCountResumed.SupplyCountNum);
			InvData.CountLine(shelfResumed.SupplyLotted,7,supplyLotNum:shelfResumed.SupplyLotLate.SupplyLotNum,supplyCountNum:supplyCountNumResumed);
			InvData.CountLine(shelfResumed.SupplyLotted,2,supplyCountNum:supplyCountNumResumed,lotNumber:shelfResumed.LotNumberFound,dateExpiry:dateExpiryFound);
			int countAdjustedResumed=InventoryTracking.PostOpenCount(supplyCountNumResumed,"Quarterly count");
			Assert.AreEqual(4,countAdjustedOnce);
			Assert.AreEqual(countAdjustedOnce,countAdjustedResumed);
			string resultOnce=shelfOnce.Result();
			Assert.AreEqual("7;12;3;7;2;"+dateExpiryFound.ToString("yyyy-MM-dd")+";plain:-2:Quarterly count,soon:-1:Quarterly count,late:1:Quarterly count,"
				+"found:2:Quarterly count",resultOnce,"Counted 8 of 10 before the use of 1: 7 on hand.");
			Assert.AreEqual(resultOnce,shelfResumed.Result());
			SupplyCount supplyCountPosted=SupplyCounts.GetOne(supplyCountNumResumed);
			Assert.AreEqual(SupplyCountStatus.Posted,supplyCountPosted.CountStatus);
			Assert.AreEqual(userodB.UserNum,supplyCountPosted.UserNumClosed);
			Assert.AreEqual("Quarterly count",supplyCountPosted.Reason);
			Assert.AreEqual(1,InvData.CountLogs(EnumPermType.InventoryAdjust,"Stock count: "+shelfResumed.SupplyPlain.Descript
				+": program had 10, counted 8, adjusted by -2. Reason: Quarterly count"));
			Assert.AreEqual(1,InvData.CountLogs(EnumPermType.InventoryAdjust,"Stock count: "+shelfOnce.SupplyPlain.Descript
				+": program had 10, counted 8, adjusted by -2. Reason: Quarterly count"));
		}

		///<summary>A post that fails after its adjustments were written (a lost connection) opens the count again with each line's program quantity
		///read again from the ledger, so posting again writes nothing more.</summary>
		[TestMethod]
		public void FailedPost_OpensTheCountAgain_AndARepostWritesNothingMore() {
			Supply supply=InvData.Supply(onHand:10);
			Supply supply2=InvData.Supply(onHand:4);
			long supplyCountNum=InvData.CountLine(supply,8).SupplyCountNum;
			InvData.CountLine(supply2,6,supplyCountNum:supplyCountNum);
			InventoryTracking.ActionAfterCountPosted=x => throw new ApplicationException("Connection lost (test)");
			InvData.Throws<ApplicationException>(() => InventoryTracking.PostOpenCount(supplyCountNum,"Count"));
			InventoryTracking.ActionAfterCountPosted=null;
			Assert.AreEqual(SupplyCountStatus.Open,SupplyCounts.GetOne(supplyCountNum).CountStatus,"Opened again.");
			Assert.AreEqual(8,InvData.OnHand(supply.SupplyNum),"The adjustments were written.");
			Assert.AreEqual(6,InvData.OnHand(supply2.SupplyNum));
			List<SupplyCountLine> listSupplyCountLines=SupplyCountLines.GetForCount(supplyCountNum);
			CollectionAssert.AreEqual(new[] { 8.0,6.0 },listSupplyCountLines.Select(x => x.QtyProgram).ToArray(),"Read again from the ledger.");
			Assert.AreEqual(0,InventoryTracking.PostOpenCount(supplyCountNum,"Count"),"Posting again writes nothing more.");
			Assert.AreEqual(SupplyCountStatus.Posted,SupplyCounts.GetOne(supplyCountNum).CountStatus);
			Assert.AreEqual(1,SupplyLedgers.GetForSupply(supply.SupplyNum).Count(x => x.LedgerType==SupplyLedgerType.CountAdjust));
			Assert.AreEqual(1,SupplyLedgers.GetForSupply(supply2.SupplyNum).Count(x => x.LedgerType==SupplyLedgerType.CountAdjust));
		}

		///<summary>A post cut off after its adjustments were written and before the count could be opened again (a crash, a power cut: here the
		///lines table is gone for the moment) leaves the count Posting. Nothing can be counted then, and it cannot be resumed while the post may
		///still run; 5 minutes after the post began it is resumed, with each line's program quantity read again from the ledger and an audit entry,
		///and posting it again writes nothing more.</summary>
		[TestMethod]
		public void PostThatDidNotFinish_StaysPosting_AndIsResumedAfterFiveMinutes() {
			Supply supply=InvData.Supply(onHand:5);
			Supply supplyOther=InvData.Supply(onHand:1);
			long supplyCountNum=InvData.CountLine(supply,3).SupplyCountNum;
			string loggerDirOld=CodeBase.Logger.LoggerDirOverride;
			CodeBase.Logger.LoggerDirOverride=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"TestLogs");
			InventoryTracking.ActionAfterCountPosted=x => {
				DataCore.NonQ("RENAME TABLE supplycountline TO supplycountline_inv_off");
				throw new ApplicationException("Power cut (test)");
			};
			try {
				InvData.Throws<ApplicationException>(() => InventoryTracking.PostOpenCount(supplyCountNum,"Count"));
			}
			finally {
				InventoryTracking.ActionAfterCountPosted=null;
				DataCore.NonQ("RENAME TABLE supplycountline_inv_off TO supplycountline");
				CodeBase.Logger.LoggerDirOverride=loggerDirOld;
			}
			Assert.AreEqual(SupplyCountStatus.Posting,SupplyCounts.GetOne(supplyCountNum).CountStatus,"It could not be opened again.");
			Assert.AreEqual(3,InvData.OnHand(supply.SupplyNum),"Posted.");
			StringAssert.Contains(InventoryTracking.GetOpenCountDescription(),"It is being posted now.");
			InvData.Throws<ApplicationException>(() => InvData.CountLine(supplyOther,1));
			Assert.AreEqual(1,InvData.CountOpenCounts(),"No second count.");
			InvData.Throws<ApplicationException>(() => InventoryTracking.ResumeCount(supplyCountNum));
			Assert.AreEqual(SupplyCountStatus.Posting,SupplyCounts.GetOne(supplyCountNum).CountStatus);
			DataCore.NonQ("UPDATE supplycount SET DateTimeClosed=DATE_SUB(NOW(),INTERVAL 10 MINUTE) WHERE SupplyCountNum="+supplyCountNum);
			StringAssert.Contains(InventoryTracking.GetOpenCountDescription(),"Its post did not finish");
			SupplyCount supplyCount=InventoryTracking.ResumeCount(supplyCountNum);
			Assert.AreEqual(SupplyCountStatus.Open,supplyCount.CountStatus);
			Assert.AreEqual(0,supplyCount.UserNumClosed);
			Assert.AreEqual(3,SupplyCountLines.GetForCount(supplyCountNum).Single().QtyProgram,"Read again from the ledger.");
			Assert.AreEqual(1,InvData.CountLogs(EnumPermType.InventoryAdjust,"Stock count resumed after a post that did not finish (begun by "));
			Assert.AreEqual(1,InvData.CountLogs(EnumPermType.InventoryAdjust,"from the ledger (SupplyCountNum "+supplyCountNum+")."));
			Assert.AreEqual(0,InventoryTracking.PostOpenCount(supplyCountNum,"Count"));
			Assert.AreEqual(3,InvData.OnHand(supply.SupplyNum));
			Assert.AreEqual(1,SupplyLedgers.GetForSupply(supply.SupplyNum).Count(x => x.LedgerType==SupplyLedgerType.CountAdjust));
		}

		///<summary>Discarding the open count closes it without changing stock, keeps its lines with it, writes an Inventory Adjust audit entry with
		///who started it, when and how many lines it had, and cannot be done twice; the next line starts a new count.</summary>
		[TestMethod]
		public void Discard_ClosesTheCount_WithItsAuditEntry_AndChangesNoStock() {
			Supply supply=InvData.Supply(onHand:5);
			Supply supply2=InvData.Supply(onHand:2);
			SupplyCountLine supplyCountLine=InvData.CountLine(supply,1);
			long supplyCountNum=supplyCountLine.SupplyCountNum;
			InvData.CountLine(supply2,9,supplyCountNum:supplyCountNum);
			SupplyCount supplyCountOpen=SupplyCounts.GetOne(supplyCountNum);
			int countLedgerRows=SupplyLedgers.GetForSupply(supply.SupplyNum).Count;
			InventoryTracking.DiscardOpenCount(supplyCountNum);
			SupplyCount supplyCount=SupplyCounts.GetOne(supplyCountNum);
			Assert.AreEqual(SupplyCountStatus.Discarded,supplyCount.CountStatus);
			Assert.AreEqual(Security.CurUser.UserNum,supplyCount.UserNumClosed);
			Assert.IsTrue(supplyCount.DateTimeClosed.Year>1880);
			Assert.IsNull(InventoryTracking.GetOpenCount());
			Assert.AreEqual(5,InvData.OnHand(supply.SupplyNum));
			Assert.AreEqual(2,InvData.OnHand(supply2.SupplyNum));
			Assert.AreEqual(countLedgerRows,SupplyLedgers.GetForSupply(supply.SupplyNum).Count,"No ledger row.");
			Assert.AreEqual(2,SupplyCountLines.GetForCount(supplyCountNum).Count,"The lines stay with the closed count.");
			Assert.AreEqual(1,InvData.CountLogs(EnumPermType.InventoryAdjust,"Stock count discarded: started by "+Userods.GetName(supplyCountOpen.UserNum)+" on "
				+supplyCountOpen.DateTimeEntry.ToShortDateString()+" "+supplyCountOpen.DateTimeEntry.ToShortTimeString()+", 2 line(s) counted; stock not changed "
				+"(SupplyCountNum "+supplyCountNum+")."));
			InvData.Throws<ApplicationException>(() => InventoryTracking.DiscardOpenCount(supplyCountNum));
			InvData.Throws<ApplicationException>(() => InventoryTracking.PostOpenCount(supplyCountNum,"Count"));
			//A line saved from a window that still shows the discarded count is refused, never moved into a new count without a word.
			InvData.Throws<ApplicationException>(() => InvData.CountLine(supply,4,supplyCountNum:supplyCountNum));
			Assert.IsNull(InventoryTracking.GetOpenCount());
			InvData.Throws<ApplicationException>(() => InventoryTracking.DeleteCountLine(supplyCountLine.SupplyCountLineNum));
			Assert.AreEqual(2,SupplyCountLines.GetForCount(supplyCountNum).Count);
			Assert.AreNotEqual(supplyCountNum,InvData.CountLine(supply,4).SupplyCountNum,"The next line starts a new count.");
		}

		///<summary>Removing a line deletes it from the open count; removing it again (another workstation was first) changes nothing.</summary>
		[TestMethod]
		public void RemovedLine_IsDeletedFromTheOpenCount() {
			Supply supply=InvData.Supply(onHand:5);
			Supply supply2=InvData.Supply(onHand:5);
			SupplyCountLine supplyCountLine=InvData.CountLine(supply,1);
			InvData.CountLine(supply2,2,supplyCountNum:supplyCountLine.SupplyCountNum);
			InventoryTracking.DeleteCountLine(supplyCountLine.SupplyCountLineNum);
			InventoryTracking.DeleteCountLine(supplyCountLine.SupplyCountLineNum);
			Assert.AreEqual(supply2.SupplyNum,SupplyCountLines.GetForCount(supplyCountLine.SupplyCountNum).Single().SupplyNum);
			Assert.AreEqual(1,InventoryTracking.PostOpenCount(supplyCountLine.SupplyCountNum,"Count"));
			Assert.AreEqual(5,InvData.OnHand(supply.SupplyNum),"The removed line is not posted.");
			Assert.AreEqual(2,InvData.OnHand(supply2.SupplyNum));
		}

		///<summary>A lot found in the count (not a lot yet) is saved with its number and expiry date and a program quantity of 0. When the lot is
		///received meanwhile and then counted under its lot, the found lot's line becomes the lot's line (the scan adds to it) with a new snapshot.
		///</summary>
		[TestMethod]
		public void LotFoundInTheCount_FollowsTheLotWhenItIsReceivedMeanwhile() {
			Supply supply=InvData.Supply(isLotTracked:true);
			string lotNumber="FND"+InvData.Next();
			SupplyCountLine supplyCountLineFound=InvData.CountLine(supply,2,lotNumber:lotNumber,dateExpiry:new DateTime(2028,5,1));
			Assert.AreEqual(0,supplyCountLineFound.SupplyLotNum);
			Assert.AreEqual(lotNumber,supplyCountLineFound.LotNumber);
			Assert.AreEqual(new DateTime(2028,5,1),supplyCountLineFound.DateExpiry);
			Assert.AreEqual(0,supplyCountLineFound.QtyProgram,"Not a lot yet.");
			SupplyLot supplyLot=InvData.Lot(supply,lotNumber,new DateTime(2028,5,1),3);//Received on another workstation.
			SupplyCountLine supplyCountLineLot=InvData.CountLine(supply,1,supplyLotNum:supplyLot.SupplyLotNum,isAdded:true,
				supplyCountNum:supplyCountLineFound.SupplyCountNum);
			Assert.AreEqual(supplyLot.SupplyLotNum,supplyCountLineLot.SupplyLotNum);
			Assert.AreEqual(supplyCountLineFound.SupplyCountLineNum,supplyCountLineLot.SupplyCountLineNum,"The same line.");
			Assert.AreEqual(3,supplyCountLineLot.QtyCounted,"2 found plus one scan.");
			Assert.AreEqual(3,supplyCountLineLot.QtyProgram);
			Assert.AreEqual(lotNumber,supplyCountLineLot.LotNumber);
			Assert.AreEqual(1,SupplyCountLines.GetForCount(supplyCountLineFound.SupplyCountNum).Count);
			//Saved as a lot found in the count by its number once the lot exists: saved as that lot.
			Assert.AreEqual(supplyLot.SupplyLotNum,InvData.CountLine(supply,3,lotNumber:lotNumber.ToLowerInvariant()).SupplyLotNum);
			Assert.AreEqual(0,InventoryTracking.PostOpenCount(supplyCountLineFound.SupplyCountNum,"Count"));
			Assert.AreEqual(3,InvData.LotQty(supplyLot.SupplyLotNum));
		}

		///<summary>Supply Inventory says whether a count is open: who started it, when and how many lines, and its Stock Count button then reads
		///Resume Count.</summary>
		[TestMethod]
		public void SupplyInventory_ShowsTheOpenCount_AndOffersToResumeIt() {
			Assert.IsNull(InventoryTracking.GetOpenCountDescription());
			string labelNone=null;
			string buttonNone=null;
			WindowTests.RunSta(() => {
				using FormSupplyInventory form=new FormSupplyInventory();
				Assert.AreNotEqual(IntPtr.Zero,form.Handle);
				WindowTests.InvokePrivate(form,"FillStockCount");
				labelNone=WindowTests.GetPrivate<Label>(form,"labelStockCount").Text;
				buttonNone=WindowTests.GetPrivate<OpenPmsDental.UI.Button>(form,"butStockCount").Text;
			});
			Assert.AreEqual("No stock count is open.",labelNone);
			Assert.AreEqual("Stock Count",buttonNone);
			SupplyCountLine supplyCountLine=InvData.CountLine(InvData.Supply(onHand:1),1);
			InvData.CountLine(InvData.Supply(onHand:1),1,supplyCountNum:supplyCountLine.SupplyCountNum);
			SupplyCount supplyCount=InventoryTracking.GetOpenCount();
			string description=InventoryTracking.GetOpenCountDescription();
			Assert.AreEqual("Stock count open: started by "+Userods.GetName(supplyCount.UserNum)+" on "+supplyCount.DateTimeEntry.ToShortDateString()+" "
				+supplyCount.DateTimeEntry.ToShortTimeString()+", 2 line(s).",description);
			string labelOpen=null;
			string buttonOpen=null;
			WindowTests.RunSta(() => {
				using FormSupplyInventory form=new FormSupplyInventory();
				Assert.AreNotEqual(IntPtr.Zero,form.Handle);
				WindowTests.InvokePrivate(form,"FillStockCount");
				labelOpen=WindowTests.GetPrivate<Label>(form,"labelStockCount").Text;
				buttonOpen=WindowTests.GetPrivate<OpenPmsDental.UI.Button>(form,"butStockCount").Text;
			});
			Assert.AreEqual(description,labelOpen);
			Assert.AreEqual("Resume Count",buttonOpen);
		}
	}
}
