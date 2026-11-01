// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenDentBusiness;

namespace ImagingCapture.Save {
	///<summary>The mount placement rules of the Imaging module, copied onto OpenDentBusiness types so that the capture session can use them
	///(design 6.4: ImagingCapture cannot reference the OpenDental assembly, where ControlImageDisplay lives). Every function is pure: it
	///works on the mount items and documents it is given and calls no database, so the rules can be tested without one.
	///
	///The rules are those of ControlImageDisplay and ControlImages (OpenDental), which stay the reference:
	///- GetAvailSlots: ControlImageDisplay.GetAvailSlots.
	///- GetFirstAvailIdx: ControlImageDisplay.PreselectFirstItem.
	///- GetPlacementIdx and GetNextTargetIdx: the slot choice of ControlImages.PlaceAcquiredBitmapInUI.
	///- ApplyToDocument: the document fields ControlImageDisplay.DocumentAcquiredForMount sets.
	///- CreateUnmountedItem: the unmounted item PlaceAcquiredBitmapInUI inserts when no slot is free (ItemOrder -1, 100x100).
	///
	///The lists are parallel, as ControlImageDisplay keeps them: listMountItems in the order MountItems.GetItemsForMount returns them
	///(ORDER BY ItemOrder, so unmounted items, ItemOrder -1, come first and text items, ItemOrder 0, next), with unmounted items added during
	///the session appended at the end, and listDocuments[i] the document in listMountItems[i] or null when that item is empty
	///(Documents.GetDocumentsForMountItems).</summary>
	public static class MountPlacement {
		///<summary>MountItem.ItemOrder of a text item. Text items never take an image.</summary>
		public const int ItemOrderText=0;
		///<summary>MountItem.ItemOrder of an unmounted item: an image that belongs to the mount but has no slot (a retake, or an image that
		///arrived when every slot was full).</summary>
		public const int ItemOrderUnmounted=-1;
		///<summary>The width and height of a new unmounted item, as ControlImages uses: arbitrary, because the unmounted bar scales it.</summary>
		public const int UnmountedItemSize=100;

		///<summary>Copy of ControlImageDisplay.GetAvailSlots: the empty items that can take an image, starting with idxSelected (-1 counts as 0)
		///and going to the end of the list, then from the start of the list up to idxSelected. An item is available when it holds no document
		///and is not a text item (ItemOrder 0). Returns null when fewer than countNeed are available; countNeed -1 returns all of them.
		///
		///As in the original, an unmounted item (ItemOrder -1) with no document counts as available. Unmounted items are created with their
		///document, so this happens only after that document was deleted or moved. GetCaptureSlots leaves them out.</summary>
		public static List<MountItem> GetAvailSlots(IList<MountItem> listMountItems,IList<Document> listDocuments,int idxSelected,int countNeed=-1) {
			CheckLists(listMountItems,listDocuments);
			CheckIdx(listMountItems,idxSelected);
			List<MountItem> listMountItemsAvail=new List<MountItem>();
			int idxItem=idxSelected;
			if(idxItem==-1) {
				idxItem=0;
			}
			while(idxItem<listMountItems.Count) {
				if(IsAvailable(listMountItems,listDocuments,idxItem)) {
					listMountItemsAvail.Add(listMountItems[idxItem]);
				}
				idxItem++;
			}
			if(idxSelected>0) {//second loop, for items before the first loop's start
				idxItem=0;
				while(idxItem<idxSelected) {
					if(IsAvailable(listMountItems,listDocuments,idxItem)) {
						listMountItemsAvail.Add(listMountItems[idxItem]);
					}
					idxItem++;
				}
			}
			if(countNeed!=-1 && listMountItemsAvail.Count<countNeed) {
				return null;
			}
			return listMountItemsAvail;
		}

		///<summary>GetAvailSlots without unmounted items: the mount slots (ItemOrder 1 and up) that are empty, in the same order. The capture
		///session targets only these, so that an X-ray never goes to an empty unmounted item, which has no position, rotation or tooth numbers
		///(design 7.3 "the target advances to the next empty slot in ItemOrder"). Returns null when fewer than countNeed are available.</summary>
		public static List<MountItem> GetCaptureSlots(IList<MountItem> listMountItems,IList<Document> listDocuments,int idxSelected,int countNeed=-1) {
			List<MountItem> listMountItemsAvail=GetAvailSlots(listMountItems,listDocuments,idxSelected,-1)
				.Where(x => x.ItemOrder!=ItemOrderUnmounted)
				.ToList();
			if(countNeed!=-1 && listMountItemsAvail.Count<countNeed) {
				return null;
			}
			return listMountItemsAvail;
		}

		///<summary>Copy of ControlImageDisplay.PreselectFirstItem, for a mount with nothing selected: the index of the first item, in list order,
		///that holds no document and is not a text item. -1 when there is none (the mount is full).</summary>
		public static int GetFirstAvailIdx(IList<MountItem> listMountItems,IList<Document> listDocuments) {
			CheckLists(listMountItems,listDocuments);
			for(int i=0;i<listDocuments.Count;i++) {
				if(listMountItems[i].ItemOrder==ItemOrderText) {
					continue;
				}
				if(listDocuments[i]==null) {
					return i;
				}
			}
			return -1;
		}

		///<summary>Where ControlImages.PlaceAcquiredBitmapInUI puts an acquired image, given the selected index: the selected item when it is
		///selected and empty; otherwise (an occupied item, or nothing selected) the first item GetAvailSlots(1) gives from idxSelected. -1 means
		///that no item is available, so the image goes to a new unmounted item (CreateUnmountedItem).
		///
		///As in the original, an empty selected item is taken whatever its ItemOrder; the caller selects only mount slots.</summary>
		public static int GetPlacementIdx(IList<MountItem> listMountItems,IList<Document> listDocuments,int idxSelected) {
			CheckLists(listMountItems,listDocuments);
			CheckIdx(listMountItems,idxSelected);
			if(idxSelected!=-1 && listDocuments[idxSelected]==null) {
				return idxSelected;
			}
			List<MountItem> listMountItemsAvail=GetAvailSlots(listMountItems,listDocuments,idxSelected,1);
			if(listMountItemsAvail==null) {
				return -1;
			}
			return IndexOfMountItem(listMountItems,listMountItemsAvail[0]);
		}

		///<summary>The next target after an image was placed in idxFilled, as PlaceAcquiredBitmapInUI decides whether it is done: the first item
		///GetAvailSlots(1) gives from idxFilled, which then holds the new document. -1 when the mount is full (the legacy loop then stops and
		///selects nothing).</summary>
		public static int GetNextTargetIdx(IList<MountItem> listMountItems,IList<Document> listDocuments,int idxFilled) {
			List<MountItem> listMountItemsAvail=GetAvailSlots(listMountItems,listDocuments,idxFilled,1);
			if(listMountItemsAvail==null) {
				return -1;
			}
			return IndexOfMountItem(listMountItems,listMountItemsAvail[0]);
		}

		///<summary>Sets the fields ControlImageDisplay.DocumentAcquiredForMount sets on an acquired document: MountItemNum, IsFlipped (set true
		///when the mount flips on acquire, never cleared), DegreesRotated from the item's RotateOnAcquire and ToothNumbers from the item. The
		///caller then saves it with Documents.Update(document,documentOld).</summary>
		public static void ApplyToDocument(Document document,MountItem mountItem,bool flipOnAcquire) {
			if(document==null) {
				throw new ArgumentNullException(nameof(document));
			}
			if(mountItem==null) {
				throw new ArgumentNullException(nameof(mountItem));
			}
			document.MountItemNum=mountItem.MountItemNum;
			if(flipOnAcquire) {
				document.IsFlipped=true;
			}
			document.DegreesRotated=mountItem.RotateOnAcquire;
			document.ToothNumbers=mountItem.ToothNumbers;
		}

		///<summary>The unmounted item ControlImages.PlaceAcquiredBitmapInUI inserts when no slot is free: ItemOrder -1, 100 by 100 (arbitrary;
		///the unmounted bar scales it), no rotation and no tooth numbers. Not inserted; MountItemNum is 0.</summary>
		public static MountItem CreateUnmountedItem(long mountNum) {
			MountItem mountItem=new MountItem();
			mountItem.MountNum=mountNum;
			mountItem.ItemOrder=ItemOrderUnmounted;
			mountItem.Width=UnmountedItemSize;
			mountItem.Height=UnmountedItemSize;
			mountItem.ToothNumbers="";
			mountItem.TextShowing="";
			return mountItem;
		}

		///<summary>True for an item that can hold a captured image as a mount slot: not a text item and not unmounted.</summary>
		public static bool IsSlot(MountItem mountItem) {
			return mountItem!=null && mountItem.ItemOrder!=ItemOrderText && mountItem.ItemOrder!=ItemOrderUnmounted;
		}

		///<summary>The slot in words, for the indicator, logs and sidecars: "Slot 3 (teeth 2,3)", "Slot 3", "Unmounted", or "No mount" for null.</summary>
		public static string GetSlotLabel(MountItem mountItem) {
			if(mountItem==null) {
				return "No mount";
			}
			if(mountItem.ItemOrder==ItemOrderUnmounted) {
				return "Unmounted";
			}
			if(mountItem.ItemOrder==ItemOrderText) {
				return "Text item";
			}
			string label="Slot "+mountItem.ItemOrder.ToString(CultureInfo.InvariantCulture);
			string toothNumbers=(mountItem.ToothNumbers??"").Trim();
			if(toothNumbers!="") {
				label+=" (teeth "+toothNumbers+")";
			}
			return label;
		}

		///<summary>The index of mountItem in the list: the same object, else the same MountItemNum when it is not 0. -1 when absent.</summary>
		public static int IndexOfMountItem(IList<MountItem> listMountItems,MountItem mountItem) {
			if(listMountItems==null || mountItem==null) {
				return -1;
			}
			for(int i=0;i<listMountItems.Count;i++) {
				if(ReferenceEquals(listMountItems[i],mountItem)) {
					return i;
				}
			}
			if(mountItem.MountItemNum==0) {
				return -1;
			}
			for(int i=0;i<listMountItems.Count;i++) {
				if(listMountItems[i]!=null && listMountItems[i].MountItemNum==mountItem.MountItemNum) {
					return i;
				}
			}
			return -1;
		}

		private static bool IsAvailable(IList<MountItem> listMountItems,IList<Document> listDocuments,int idx) {
			if(listDocuments[idx]!=null) {
				return false;//occupied
			}
			if(listMountItems[idx].ItemOrder==ItemOrderText) {
				return false;//text items are not available
			}
			return true;
		}

		private static void CheckLists(IList<MountItem> listMountItems,IList<Document> listDocuments) {
			if(listMountItems==null) {
				throw new ArgumentNullException(nameof(listMountItems));
			}
			if(listDocuments==null) {
				throw new ArgumentNullException(nameof(listDocuments));
			}
			if(listMountItems.Count!=listDocuments.Count) {
				throw new ArgumentException("The mount items and documents must be parallel lists of the same length.");
			}
			if(listMountItems.Any(x => x==null)) {
				throw new ArgumentException("A mount item is null.");
			}
		}

		///<summary>idxSelected is -1 (nothing selected) or an index of the list, as ControlImageDisplay keeps _idxSelectedInMount.</summary>
		private static void CheckIdx(IList<MountItem> listMountItems,int idxSelected) {
			if(idxSelected<-1 || (idxSelected>-1 && idxSelected>=listMountItems.Count)) {
				throw new ArgumentOutOfRangeException(nameof(idxSelected));
			}
		}
	}

	///<summary>A mount as the capture session holds it while it fills it: the mount, its items and their documents as parallel lists (as
	///ControlImageDisplay keeps them), and the target. Placement follows MountPlacement; the session window updates it after each save.</summary>
	public sealed class MountSlots {
		private readonly List<MountItem> _listMountItems;
		private readonly List<Document> _listDocuments;

		///<summary>listMountItems from MountItems.GetItemsForMount (ordered by ItemOrder), and listDocuments from
		///Documents.GetDocumentsForMountItems for the same items. The target starts at the first empty slot (GetCaptureSlots), or -1.</summary>
		public MountSlots(Mount mount,IEnumerable<MountItem> listMountItems,IEnumerable<Document> listDocuments) {
			Mount=mount??throw new ArgumentNullException(nameof(mount));
			_listMountItems=(listMountItems??throw new ArgumentNullException(nameof(listMountItems))).ToList();
			_listDocuments=(listDocuments??throw new ArgumentNullException(nameof(listDocuments))).ToList();
			if(_listMountItems.Count!=_listDocuments.Count) {
				throw new ArgumentException("The mount items and documents must be parallel lists of the same length.");
			}
			List<MountItem> listSlots=MountPlacement.GetCaptureSlots(_listMountItems,_listDocuments,-1,1);
			IdxTarget=listSlots==null ? -1 : MountPlacement.IndexOfMountItem(_listMountItems,listSlots[0]);
		}

		public Mount Mount { get; }
		public IReadOnlyList<MountItem> ListMountItems => _listMountItems;
		public IReadOnlyList<Document> ListDocuments => _listDocuments;
		///<summary>The index of the target slot in ListMountItems, or -1 when no slot is free (the mount is full).</summary>
		public int IdxTarget { get; private set; }

		///<summary>The target slot, or null when the mount is full.</summary>
		public MountItem Target => IdxTarget==-1 ? null : _listMountItems[IdxTarget];

		///<summary>The empty slots, starting with the target and wrapping around (GetCaptureSlots).</summary>
		public List<MountItem> GetEmptySlots() {
			return MountPlacement.GetCaptureSlots(_listMountItems,_listDocuments,IdxTarget,-1);
		}

		///<summary>Makes the slot at idx the target, when it is an empty slot. Returns false otherwise (the target is unchanged).</summary>
		public bool SetTarget(int idx) {
			if(idx<0 || idx>=_listMountItems.Count || _listDocuments[idx]!=null || !MountPlacement.IsSlot(_listMountItems[idx])) {
				return false;
			}
			IdxTarget=idx;
			return true;
		}

		///<summary>Records that document was saved into the slot at idx (a document placed with MountPlacement.ApplyToDocument), and moves the
		///target to the next empty slot after it, wrapping around (-1 when the mount is now full).</summary>
		public void SetPlaced(int idx,Document document) {
			if(idx<0 || idx>=_listMountItems.Count) {
				throw new ArgumentOutOfRangeException(nameof(idx));
			}
			_listDocuments[idx]=document??throw new ArgumentNullException(nameof(document));
			List<MountItem> listSlots=MountPlacement.GetCaptureSlots(_listMountItems,_listDocuments,idx,1);
			IdxTarget=listSlots==null ? -1 : MountPlacement.IndexOfMountItem(_listMountItems,listSlots[0]);
		}

		///<summary>Records that the document in the slot at idx left it (a Retake moved it to an unmounted item, design 7.11), and returns that
		///document, or null when the slot was empty. The slot is empty afterwards; the target is unchanged.</summary>
		public Document RemoveFromSlot(int idx) {
			if(idx<0 || idx>=_listMountItems.Count) {
				throw new ArgumentOutOfRangeException(nameof(idx));
			}
			Document document=_listDocuments[idx];
			_listDocuments[idx]=null;
			return document;
		}

		///<summary>Records a new unmounted item and its document, appended at the end as ControlImageDisplay.DocumentAcquiredForMount does. The
		///target is unchanged.</summary>
		public void AddUnmounted(MountItem mountItem,Document document) {
			if(mountItem==null) {
				throw new ArgumentNullException(nameof(mountItem));
			}
			_listMountItems.Add(mountItem);
			_listDocuments.Add(document);
		}

		///<summary>The index of mountItem in ListMountItems, or -1.</summary>
		public int IndexOf(MountItem mountItem) {
			return MountPlacement.IndexOfMountItem(_listMountItems,mountItem);
		}
	}
}
