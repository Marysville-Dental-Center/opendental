// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using ImagingCapture.Save;
using OpenPmsDentalBusiness;

namespace ImagingCapture.Session {
	///<summary>Retake, Rotate and "Always rotate this slot" (design 7.3 step 5, 7.10, 7.11, 9.4, 10.7). UI thread only, like the rest of the
	///controller.
	///
	///- Retake works on the image the window shows (LastSavedDocument, while it is still in its slot): the document moves to a new unmounted
	///  item of the mount (ItemOrder -1, 100x100, as ControlImages does when no slot is free), labelled "Retake: &lt;slot&gt;" in its
	///  Description, with an ImageEdit entry (FKey=DocNum), and the same slot becomes the target again through ChangeTarget, which never moves
	///  the image of the enable that runs. Exposures are never deleted here.
	///- Rotate turns the shown image 90 degrees clockwise: Document.DegreesRotated, the display transform the Imaging module uses (stored
	///  pixels never change), saved as ControlImages' Rotate Right does.
	///- "Always rotate this slot", after a Rotate, writes the shown image's rotation to MountItemDef.RotateOnAcquire, found by MountDefNum and
	///  ItemOrder, and to the slot of this mount, so that its retakes come in turned too. It is offered only when the mount was created from a
	///  mount definition in this call (MountDef not null), needs Setup and writes a Setup entry with the old and new values.</summary>
	public sealed partial class CaptureSessionController {
		#region Properties - retake and rotation
		///<summary>The shown image's rotation in degrees (0, 90, 180, 270): the display transform the window applies to LastSavedImage.</summary>
		public int DegreesRotatedShown => LastSavedDocument==null ? 0 : NormalizeDegrees(LastSavedDocument.DegreesRotated);

		///<summary>"Always rotate this slot" is offered: the mount was created from a definition in this call, and the shown image's rotation
		///differs from its slot's RotateOnAcquire (a correction was made). Setup is checked when it is pressed.</summary>
		public bool CanAlwaysRotateSlot {
			get {
				if(_setup.MountDef==null || IsSingleTarget || Phase==CaptureSessionPhase.Finished || LastSavedDocument==null || LastSavedMountItem==null) {
					return false;
				}
				if(!MountPlacement.IsSlot(LastSavedMountItem)) {
					return false;
				}
				return DegreesRotatedShown!=NormalizeDegrees(LastSavedMountItem.RotateOnAcquire);
			}
		}
		#endregion Properties - retake and rotation

		#region Methods - retake and rotation
		///<summary>"Always rotate this slot" (design 7.3 step 5, 9.4): Setup permission, then MountItemDef.RotateOnAcquire and this mount's slot get
		///the shown image's rotation, with a Setup entry naming the old and new values. False when it is not offered, not authorized or failed
		///(NoticeText says why).</summary>
		public bool AlwaysRotateSlot() {
			if(!CanAlwaysRotateSlot) {
				return false;
			}
			bool isDone=SetSlotRotation();
			NotifyChanged();
			return isDone;
		}

		private bool SetSlotRotation() {
			ICaptureSessionStore store=_services.Store;
			if(store==null || !store.IsAuthorized(EnumPermType.Setup)) {
				NoticeText=CaptureSessionText.AlwaysRotateNotAuthorized;
				Log("Always rotate this slot refused: not authorized for Setup.");
				return false;
			}
			MountItem mountItem=LastSavedMountItem;
			int degreesNew=DegreesRotatedShown;
			MountItemDef mountItemDef;
			try {
				List<MountItemDef> listMountItemDefs=store.GetMountItemDefs(_setup.MountDef.MountDefNum)??new List<MountItemDef>();
				mountItemDef=listMountItemDefs.FirstOrDefault(x => x.ItemOrder==mountItem.ItemOrder);
			}
			catch(Exception ex) {
				return FailSlotRotation("The mount definition could not be read: "+ex.Message);
			}
			if(mountItemDef==null) {
				return FailSlotRotation("The mount definition \""+(_setup.MountDef.Description??"")+"\" has no slot "+mountItem.ItemOrder+".");
			}
			int degreesOld=NormalizeDegrees(mountItemDef.RotateOnAcquire);
			mountItemDef.RotateOnAcquire=degreesNew;
			try {
				store.UpdateMountItemDef(mountItemDef);
			}
			catch(Exception ex) {
				mountItemDef.RotateOnAcquire=degreesOld;
				return FailSlotRotation("The mount definition could not be saved: "+ex.Message);
			}
			int degreesItemOld=mountItem.RotateOnAcquire;
			mountItem.RotateOnAcquire=degreesNew;
			bool isMountItemChanged=true;
			string errorMountItem="";
			try {
				store.UpdateMountItem(mountItem);
			}
			catch(Exception ex) {
				mountItem.RotateOnAcquire=degreesItemOld;
				isMountItemChanged=false;
				errorMountItem=ex.Message;
				Log("The slot of mount "+_setup.Mount.MountNum+" could not be saved with the new rotation: "+ex.Message);
			}
			//Design 10.7: a Setup change with its old and new values, FKey=MountDefNum as the standard templates' entries.
			string logText=CaptureAuditText.GetAlwaysRotateText(_setup.MountDef.Description,_setup.MountDef.MountDefNum,mountItem.ItemOrder,
				mountItem.ToothNumbers,degreesOld,degreesNew,_setup.Mount.MountNum,degreesItemOld,isMountItemChanged,_setup.ComputerName);
			if(!WriteAudit(EnumPermType.Setup,0,logText,_setup.MountDef.MountDefNum)) {
				return UndoSlotRotation(store,mountItemDef,degreesOld,mountItem,isMountItemChanged ? (int?)degreesItemOld : null);
			}
			NoticeText=CaptureSessionText.GetAlwaysRotateText(mountItem.ItemOrder,_setup.MountDef.Description,degreesNew)
				+(isMountItemChanged ? "" : " The slot of this mount could not be changed: "+errorMountItem);
			Log("Always rotate this slot: MountDefNum "+_setup.MountDef.MountDefNum+", slot "+mountItem.ItemOrder+", "+degreesOld+" -> "+degreesNew+".");
			return true;
		}

		///<summary>The Setup entry of "Always rotate this slot" could not be written: a Setup change is not kept without its entry, so the mount
		///definition's slot (and this mount's slot, when degreesItemOld is set) get their old rotation back. NoticeText says what is left.</summary>
		private bool UndoSlotRotation(ICaptureSessionStore store,MountItemDef mountItemDef,int degreesOld,MountItem mountItem,int? degreesItemOld) {
			List<string> listErrors=new List<string>();
			mountItemDef.RotateOnAcquire=degreesOld;
			try {
				store.UpdateMountItemDef(mountItemDef);
			}
			catch(Exception ex) {
				listErrors.Add(ex.Message);
			}
			if(degreesItemOld!=null) {
				int degreesNew=mountItem.RotateOnAcquire;
				mountItem.RotateOnAcquire=degreesItemOld.Value;
				try {
					store.UpdateMountItem(mountItem);
				}
				catch(Exception ex) {
					mountItem.RotateOnAcquire=degreesNew;
					listErrors.Add(ex.Message);
				}
			}
			if(listErrors.Count==0) {
				return FailSlotRotation("the change could not be written to the security log, so it was undone.");
			}
			return FailSlotRotation("the change could not be written to the security log, and it could not be undone ("+string.Join(" ",listErrors)
				+"): the new rotation is saved but not logged. Tell an administrator.");
		}

		private bool FailSlotRotation(string error) {
			NoticeText="Always rotate this slot: "+error;
			Log("Always rotate this slot failed: "+error);
			PlaySound(CaptureSound.Error);
			return false;
		}

		///<summary>The index of the shown image's slot, when it is still there: what a Retake moves. -1 when there is none.</summary>
		private int GetRetakeIdx() {
			if(IsSingleTarget || LastSavedDocument==null || LastSavedMountItem==null) {
				return -1;
			}
			int idx=_mountSlots.IndexOf(LastSavedMountItem);
			if(idx==-1 || !MountPlacement.IsSlot(LastSavedMountItem) || !ReferenceEquals(_mountSlots.ListDocuments[idx],LastSavedDocument)) {
				return -1;
			}
			return idx;
		}

		private bool CanRetake() {
			return Phase!=CaptureSessionPhase.Finished && !_isEndRequested && GetRetakeIdx()!=-1;
		}

		///<summary>Retake (design 7.11): the shown image moves to a new unmounted item labelled "Retake: &lt;slot&gt;", with its ImageEdit entry, and
		///its slot is the target again. A failed database write changes nothing and says so in the red strip.</summary>
		private bool Retake() {
			int idx=GetRetakeIdx();
			if(idx==-1 || Phase==CaptureSessionPhase.Finished) {
				return false;
			}
			ClearStopWarning();
			Document document=_mountSlots.ListDocuments[idx];
			MountItem mountItemSlot=_mountSlots.ListMountItems[idx];
			string slotLabel=MountPlacement.GetSlotLabel(mountItemSlot);
			MountItem mountItemUnmounted=MountPlacement.CreateUnmountedItem(_setup.Mount.MountNum);
			try {
				_radiographStore.InsertMountItem(mountItemUnmounted);
			}
			catch(Exception ex) {
				return FailRetake(slotLabel,ex.Message);
			}
			Document documentOld=document.Copy();
			document.MountItemNum=mountItemUnmounted.MountItemNum;
			document.Description=CaptureSessionText.GetRetakeLabel(slotLabel);
			try {
				_radiographStore.UpdateDocument(document,documentOld);
			}
			catch(Exception ex) {
				document.MountItemNum=documentOld.MountItemNum;
				document.Description=documentOld.Description;
				try {
					_radiographStore.DeleteMountItem(mountItemUnmounted);
				}
				catch(Exception exDelete) {
					Log("The unmounted item made for the retake could not be deleted again: "+exDelete.Message);
				}
				return FailRetake(slotLabel,ex.Message);
			}
			_mountSlots.RemoveFromSlot(idx);
			_mountSlots.AddUnmounted(mountItemUnmounted,document);
			CountRetakes++;
			LastSavedImage=null;
			LastSavedDocument=null;
			LastSavedMountItem=null;
			WriteAudit(EnumPermType.ImageEdit,PatNum,CaptureAuditText.GetRetakeText(document.DocNum,slotLabel,_setup.Mount.MountNum,SessionGuid),
				document.DocNum);
			Log("Retake: DocNum "+document.DocNum+" moved from "+slotLabel+" to unmounted item "+mountItemUnmounted.MountItemNum+".");
			ChangeTarget(idx,isSkip:false,isRetake:true);
			NoticeText=CaptureSessionText.GetRetakeNotice(slotLabel);
			return true;
		}

		private bool FailRetake(string slotLabel,string error) {
			string text="Could not retake "+slotLabel+": "+(error??"").Trim();
			RedStripText=text;
			Log(text);
			PlaySound(CaptureSound.Error);
			return false;
		}

		///<summary>Rotate (design 7.3 step 5, 7.10): the shown image turns 90 degrees clockwise, as a display transform (DegreesRotated), saved as
		///the Imaging module's Rotate Right saves it.</summary>
		private bool Rotate() {
			Document document=LastSavedDocument;
			if(document==null || Phase==CaptureSessionPhase.Finished) {
				return false;
			}
			Document documentOld=document.Copy();
			int degreesNew=(NormalizeDegrees(document.DegreesRotated)+90)%360;
			document.DegreesRotated=degreesNew;
			try {
				_radiographStore.UpdateDocument(document,documentOld);
			}
			catch(Exception ex) {
				document.DegreesRotated=documentOld.DegreesRotated;
				NoticeText="Could not rotate: "+ex.Message;
				Log("Rotate failed for DocNum "+document.DocNum+": "+ex.Message);
				PlaySound(CaptureSound.Error);
				return false;
			}
			NoticeText=CaptureSessionText.GetRotatedText(degreesNew);
			Log("Rotated DocNum "+document.DocNum+" to "+degreesNew+" degrees.");
			//As the Imaging module's Rotate Right: a stored change of the patient's document is logged under ImageEdit.
			string targetLabel=IsSingleTarget ? GetRadiographTarget(null,document.ToothNumbers).GetLabel() : MountPlacement.GetSlotLabel(LastSavedMountItem);
			WriteAudit(EnumPermType.ImageEdit,PatNum,CaptureAuditText.GetRotateText(document.DocNum,document.FileName,targetLabel,
				NormalizeDegrees(documentOld.DegreesRotated),degreesNew,SessionGuid),document.DocNum);
			return true;
		}

		///<summary>0, 90, 180 or 270 for any whole-degree value (DegreesRotated is a float, RotateOnAcquire an int).</summary>
		internal static int NormalizeDegrees(double degrees) {
			int value=(int)Math.Round(degrees)%360;
			if(value<0) {
				value+=360;
			}
			return value;
		}
		#endregion Methods - retake and rotation
	}
}
