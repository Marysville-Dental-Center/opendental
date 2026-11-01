// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using ImagingCapture.Save;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace ImagingCapture.Tests {
	///<summary>MountPlacement and MountSlots (design 6.4, 7.3): the placement rules of ControlImageDisplay.GetAvailSlots,
	///PreselectFirstItem and DocumentAcquiredForMount, and the slot choice of ControlImages.PlaceAcquiredBitmapInUI, pinned on representative
	///mounts. No database: the mount items and documents are built here, in the order MountItems.GetItemsForMount returns them.</summary>
	[TestClass]
	public class MountPlacementTests {
		private static MountItem Item(long mountItemNum,int itemOrder,string toothNumbers="",int rotateOnAcquire=0) {
			return new MountItem { MountItemNum=mountItemNum,MountNum=7,ItemOrder=itemOrder,ToothNumbers=toothNumbers,RotateOnAcquire=rotateOnAcquire,
				Width=200,Height=300 };
		}

		private static Document Doc(long docNum,long mountItemNum=0) {
			return new Document { DocNum=docNum,MountItemNum=mountItemNum };
		}

		private static List<long> Nums(List<MountItem> listMountItems) {
			return listMountItems?.Select(x => x.MountItemNum).ToList();
		}

		///<summary>A bitewing-4 mount as it comes from the database: four slots, ItemOrder 1 to 4.</summary>
		private static List<MountItem> Bitewing4() {
			return new List<MountItem> { Item(101,1,"2,3",90),Item(102,2,"14,15",270),Item(103,3,"30,31",90),Item(104,4,"18,19",270) };
		}

		[TestMethod]
		public void EmptyMount_AllSlotsInOrder() {
			List<MountItem> listMountItems=Bitewing4();
			Document[] arrayDocuments=new Document[4];
			CollectionAssert.AreEqual(new List<long> { 101,102,103,104 },Nums(MountPlacement.GetAvailSlots(listMountItems,arrayDocuments,-1)));
			CollectionAssert.AreEqual(new List<long> { 101,102,103,104 },Nums(MountPlacement.GetAvailSlots(listMountItems,arrayDocuments,0)));
			Assert.AreEqual(0,MountPlacement.GetFirstAvailIdx(listMountItems,arrayDocuments));
			Assert.AreEqual(0,MountPlacement.GetPlacementIdx(listMountItems,arrayDocuments,-1),"Nothing selected: the first available slot.");
			Assert.AreEqual(2,MountPlacement.GetPlacementIdx(listMountItems,arrayDocuments,2),"An empty selected slot is used as it is.");
		}

		[TestMethod]
		public void StartInMiddle_WrapsAround() {
			List<MountItem> listMountItems=Bitewing4();
			Document[] arrayDocuments=new Document[4];
			CollectionAssert.AreEqual(new List<long> { 103,104,101,102 },Nums(MountPlacement.GetAvailSlots(listMountItems,arrayDocuments,2)));
		}

		[TestMethod]
		public void PartiallyFilled_SkipsOccupied() {
			List<MountItem> listMountItems=Bitewing4();
			Document[] arrayDocuments={ Doc(1,101),null,Doc(3,103),null };
			CollectionAssert.AreEqual(new List<long> { 104,102 },Nums(MountPlacement.GetAvailSlots(listMountItems,arrayDocuments,2)));
			CollectionAssert.AreEqual(new List<long> { 102,104 },Nums(MountPlacement.GetAvailSlots(listMountItems,arrayDocuments,-1)));
			Assert.AreEqual(1,MountPlacement.GetFirstAvailIdx(listMountItems,arrayDocuments));
			Assert.AreEqual(3,MountPlacement.GetPlacementIdx(listMountItems,arrayDocuments,2),"An occupied selection moves to the next free slot.");
			Assert.AreEqual(1,MountPlacement.GetPlacementIdx(listMountItems,arrayDocuments,0));
			Assert.AreEqual(3,MountPlacement.GetNextTargetIdx(listMountItems,arrayDocuments,2));
		}

		[TestMethod]
		public void CountNeed_NullWhenTooFew() {
			List<MountItem> listMountItems=Bitewing4();
			Document[] arrayDocuments={ Doc(1),Doc(2),null,Doc(4) };
			Assert.AreEqual(1,MountPlacement.GetAvailSlots(listMountItems,arrayDocuments,0,1).Count);
			Assert.IsNull(MountPlacement.GetAvailSlots(listMountItems,arrayDocuments,0,2));
			Assert.AreEqual(1,MountPlacement.GetAvailSlots(listMountItems,arrayDocuments,0,-1).Count);
		}

		[TestMethod]
		public void FullMount_NoSlotGoesUnmounted() {
			List<MountItem> listMountItems=Bitewing4();
			Document[] arrayDocuments={ Doc(1),Doc(2),Doc(3),Doc(4) };
			Assert.IsNull(MountPlacement.GetAvailSlots(listMountItems,arrayDocuments,3,1));
			Assert.AreEqual(0,MountPlacement.GetAvailSlots(listMountItems,arrayDocuments,3).Count);
			Assert.AreEqual(-1,MountPlacement.GetFirstAvailIdx(listMountItems,arrayDocuments));
			Assert.AreEqual(-1,MountPlacement.GetPlacementIdx(listMountItems,arrayDocuments,3),"-1: a new unmounted item takes the image.");
			Assert.AreEqual(-1,MountPlacement.GetPlacementIdx(listMountItems,arrayDocuments,-1));
			Assert.AreEqual(-1,MountPlacement.GetNextTargetIdx(listMountItems,arrayDocuments,3));
		}

		[TestMethod]
		public void TextItems_NeverAvailable() {
			//Text items (ItemOrder 0) sort before the slots.
			List<MountItem> listMountItems=new List<MountItem> { Item(200,0),Item(201,1),Item(202,2) };
			Document[] arrayDocuments=new Document[3];
			CollectionAssert.AreEqual(new List<long> { 201,202 },Nums(MountPlacement.GetAvailSlots(listMountItems,arrayDocuments,-1)));
			Assert.AreEqual(1,MountPlacement.GetFirstAvailIdx(listMountItems,arrayDocuments));
			Assert.AreEqual(1,MountPlacement.GetPlacementIdx(listMountItems,arrayDocuments,-1));
			CollectionAssert.AreEqual(new List<long> { 202,201 },Nums(MountPlacement.GetAvailSlots(listMountItems,arrayDocuments,2)),
				"The second loop also skips the text item.");
			Assert.IsFalse(MountPlacement.IsSlot(listMountItems[0]));
		}

		[TestMethod]
		public void RetakeUnmountedItems_WithTheirDocuments_AreOccupied() {
			//A retake moved the rejected image to an unmounted item (ItemOrder -1), which the database returns first.
			List<MountItem> listMountItems=new List<MountItem> { Item(300,-1),Item(301,1,"8,9"),Item(302,2,"7") };
			Document[] arrayDocuments={ Doc(30,300),null,null };
			CollectionAssert.AreEqual(new List<long> { 301,302 },Nums(MountPlacement.GetAvailSlots(listMountItems,arrayDocuments,-1)));
			CollectionAssert.AreEqual(new List<long> { 301,302 },Nums(MountPlacement.GetCaptureSlots(listMountItems,arrayDocuments,-1)));
			Assert.AreEqual(1,MountPlacement.GetFirstAvailIdx(listMountItems,arrayDocuments));
			Assert.AreEqual(1,MountPlacement.GetPlacementIdx(listMountItems,arrayDocuments,-1));
			Assert.IsFalse(MountPlacement.IsSlot(listMountItems[0]));
		}

		[TestMethod]
		public void EmptyUnmountedItem_AvailableInLegacyRules_NotACaptureSlot() {
			//An unmounted item whose document was deleted: the legacy rules count it as available, the capture session does not.
			List<MountItem> listMountItems=new List<MountItem> { Item(300,-1),Item(301,1,"8,9"),Item(302,2,"7") };
			Document[] arrayDocuments=new Document[3];
			CollectionAssert.AreEqual(new List<long> { 300,301,302 },Nums(MountPlacement.GetAvailSlots(listMountItems,arrayDocuments,-1)));
			Assert.AreEqual(0,MountPlacement.GetFirstAvailIdx(listMountItems,arrayDocuments),"PreselectFirstItem skips only text items.");
			CollectionAssert.AreEqual(new List<long> { 301,302 },Nums(MountPlacement.GetCaptureSlots(listMountItems,arrayDocuments,-1)));
			Assert.IsNull(MountPlacement.GetCaptureSlots(listMountItems,arrayDocuments,-1,3));
			MountSlots mountSlots=new MountSlots(new Mount { MountNum=7 },listMountItems,arrayDocuments);
			Assert.AreEqual(1,mountSlots.IdxTarget);
			Assert.IsFalse(mountSlots.SetTarget(0),"An unmounted item is never a target.");
		}

		[TestMethod]
		public void UnmountedAddedDuringSession_AppendedAtEnd() {
			List<MountItem> listMountItems=Bitewing4();
			Document[] arrayDocuments={ Doc(1),Doc(2),Doc(3),Doc(4) };
			MountSlots mountSlots=new MountSlots(new Mount { MountNum=7 },listMountItems,arrayDocuments);
			Assert.AreEqual(-1,mountSlots.IdxTarget);
			Assert.IsNull(mountSlots.Target);
			MountItem mountItemUnmounted=MountPlacement.CreateUnmountedItem(7);
			mountItemUnmounted.MountItemNum=500;
			mountSlots.AddUnmounted(mountItemUnmounted,Doc(5,500));
			Assert.AreEqual(5,mountSlots.ListMountItems.Count);
			Assert.AreEqual(500,mountSlots.ListMountItems[4].MountItemNum);
			Assert.AreEqual(4,mountSlots.IndexOf(mountItemUnmounted));
			Assert.AreEqual(-1,mountSlots.IdxTarget);
			Assert.AreEqual(-1,MountPlacement.GetPlacementIdx(mountSlots.ListMountItems.ToList(),mountSlots.ListDocuments.ToList(),4));
		}

		[TestMethod]
		public void ApplyToDocument_SetsTheFieldsOfDocumentAcquiredForMount() {
			Document document=new Document { DocNum=9,ToothNumbers="",DegreesRotated=0,IsFlipped=false };
			MountItem mountItem=Item(103,3,"30,31",90);
			MountPlacement.ApplyToDocument(document,mountItem,flipOnAcquire:false);
			Assert.AreEqual(103,document.MountItemNum);
			Assert.AreEqual(90f,document.DegreesRotated);
			Assert.AreEqual("30,31",document.ToothNumbers);
			Assert.IsFalse(document.IsFlipped);
			MountPlacement.ApplyToDocument(document,mountItem,flipOnAcquire:true);
			Assert.IsTrue(document.IsFlipped);
			MountPlacement.ApplyToDocument(document,Item(104,4,"18,19",270),flipOnAcquire:false);
			Assert.IsTrue(document.IsFlipped,"As in DocumentAcquiredForMount, IsFlipped is set but never cleared.");
			Assert.AreEqual(270f,document.DegreesRotated);
		}

		[TestMethod]
		public void CreateUnmountedItem_IsTheLegacyConvention() {
			MountItem mountItem=MountPlacement.CreateUnmountedItem(42);
			Assert.AreEqual(42,mountItem.MountNum);
			Assert.AreEqual(-1,mountItem.ItemOrder);
			Assert.AreEqual(100,mountItem.Width);
			Assert.AreEqual(100,mountItem.Height);
			Assert.AreEqual(0,mountItem.RotateOnAcquire);
			Assert.AreEqual("",mountItem.ToothNumbers);
			Assert.AreEqual(0,mountItem.MountItemNum);
		}

		[TestMethod]
		public void SlotLabel() {
			Assert.AreEqual("Slot 3 (teeth 30,31)",MountPlacement.GetSlotLabel(Item(103,3,"30,31")));
			Assert.AreEqual("Slot 2",MountPlacement.GetSlotLabel(Item(102,2,"")));
			Assert.AreEqual("Unmounted",MountPlacement.GetSlotLabel(Item(300,-1)));
			Assert.AreEqual("No mount",MountPlacement.GetSlotLabel(null));
		}

		[TestMethod]
		public void BadInput_Throws() {
			List<MountItem> listMountItems=Bitewing4();
			TestUtil.AssertThrows<ArgumentException>(() => MountPlacement.GetAvailSlots(listMountItems,new Document[3],-1));
			TestUtil.AssertThrows<ArgumentOutOfRangeException>(() => MountPlacement.GetAvailSlots(listMountItems,new Document[4],4));
			TestUtil.AssertThrows<ArgumentOutOfRangeException>(() => MountPlacement.GetPlacementIdx(listMountItems,new Document[4],-2));
		}

		///<summary>The legacy Mount/Acquire loop (PreselectFirstItem, then PlaceAcquiredBitmapInUI per image until GetAvailSlots(1) is null) and
		///MountSlots fill a partly filled FMX-like mount with a text item in the same order.</summary>
		[TestMethod]
		public void LegacyLoopAndMountSlots_FillInTheSameOrder() {
			List<MountItem> listMountItems=new List<MountItem> { Item(900,0) };
			for(int i=1;i<=8;i++) {
				listMountItems.Add(Item(900+i,i,i.ToString()));
			}
			Document[] arrayDocumentsStart=new Document[listMountItems.Count];
			arrayDocumentsStart[2]=Doc(2);//slot 2 already taken
			arrayDocumentsStart[5]=Doc(5);//slot 5 already taken
			//Legacy loop.
			Document[] arrayDocumentsLegacy=(Document[])arrayDocumentsStart.Clone();
			List<long> listLegacy=new List<long>();
			int idxSelected=MountPlacement.GetFirstAvailIdx(listMountItems,arrayDocumentsLegacy);
			for(int image=0;image<20;image++) {
				int idx=MountPlacement.GetPlacementIdx(listMountItems,arrayDocumentsLegacy,idxSelected);
				Assert.AreNotEqual(-1,idx);
				arrayDocumentsLegacy[idx]=Doc(100+image);
				listLegacy.Add(listMountItems[idx].MountItemNum);
				idxSelected=idx;
				if(MountPlacement.GetAvailSlots(listMountItems,arrayDocumentsLegacy,idxSelected,1)==null) {
					break;
				}
			}
			//MountSlots.
			MountSlots mountSlots=new MountSlots(new Mount { MountNum=7 },listMountItems,arrayDocumentsStart);
			List<long> listSession=new List<long>();
			int count=0;
			while(mountSlots.Target!=null && count++<20) {
				listSession.Add(mountSlots.Target.MountItemNum);
				mountSlots.SetPlaced(mountSlots.IdxTarget,Doc(200+count));
			}
			CollectionAssert.AreEqual(new List<long> { 901,903,904,906,907,908 },listLegacy);
			CollectionAssert.AreEqual(listLegacy,listSession);
		}

		[TestMethod]
		public void MountSlots_TargetMovesAndWraps() {
			List<MountItem> listMountItems=Bitewing4();
			MountSlots mountSlots=new MountSlots(new Mount { MountNum=7 },listMountItems,new Document[4]);
			Assert.AreEqual(0,mountSlots.IdxTarget);
			Assert.IsTrue(mountSlots.SetTarget(2),"Tap an empty slot.");
			mountSlots.SetPlaced(2,Doc(3));
			Assert.AreEqual(3,mountSlots.IdxTarget);
			mountSlots.SetPlaced(3,Doc(4));
			Assert.AreEqual(0,mountSlots.IdxTarget,"Wraps to the first empty slot.");
			Assert.IsFalse(mountSlots.SetTarget(3),"An occupied slot is not a target.");
			Assert.AreEqual(0,mountSlots.IdxTarget);
			CollectionAssert.AreEqual(new List<long> { 101,102 },Nums(mountSlots.GetEmptySlots()));
			mountSlots.SetPlaced(0,Doc(1));
			mountSlots.SetPlaced(1,Doc(2));
			Assert.AreEqual(-1,mountSlots.IdxTarget);
			Assert.AreEqual(0,mountSlots.GetEmptySlots().Count);
		}
	}
}
