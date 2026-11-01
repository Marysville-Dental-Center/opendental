// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using ImagingCapture.Save;
using ImagingCapture.Session;
using ImagingDeviceManager.Capture;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace ImagingCapture.Tests {
	///<summary>Retake, Rotate and "Always rotate this slot" of CaptureSessionController (design 7.3 step 5, 7.10, 7.11, 9.4, 10.7), on the fakes
	///of CaptureSessionTestKit. A target change while the source is enabled never moves the image of the enable that runs.</summary>
	[TestClass]
	public class CaptureSessionRetakeTests {
		private const string SessionGuidText="11111111-2222-3333-4444-555555555555";
		private CaptureControllerHarness _harness;

		[TestInitialize]
		public void Init() {
			_harness=new CaptureControllerHarness();
		}

		private CaptureSessionController Controller => _harness.Controller;

		private Document DocumentInSlot(int itemOrder) {
			int idx=Controller.MountSlots.IndexOf(Controller.MountSlots.ListMountItems.First(x => x.ItemOrder==itemOrder));
			return Controller.MountSlots.ListDocuments[idx];
		}

		#region Retake
		[TestMethod]
		public void Retake_WithoutSafeDisable_QueuedUntilTheRunningEnableDelivers_ThenTheSlotAgain() {
			_harness.Started();
			_harness.Expose();//slot 1 saved, the enable for slot 2 posted
			_harness.Status(TwainCaptureState.WaitingForImage,isSafeDisableInEffect:false);
			Assert.IsTrue(Controller.CanExecute(CaptureSessionAction.Retake));
			Assert.IsTrue(_harness.Execute(CaptureSessionAction.Retake));
			//The rejected document went to a new unmounted item of the mount (ItemOrder -1, 100x100), labelled "Retake: <slot>".
			MountItem mountItemUnmounted=_harness.RadiographStore.ListMountItemsInserted.Single();
			Assert.AreEqual(MountPlacement.ItemOrderUnmounted,mountItemUnmounted.ItemOrder);
			Assert.AreEqual(100,mountItemUnmounted.Width);
			Assert.AreEqual(100,mountItemUnmounted.Height);
			Assert.AreEqual(7,mountItemUnmounted.MountNum);
			Assert.AreEqual("",mountItemUnmounted.TextShowing);
			Document documentRetaken=_harness.RadiographStore.ListDocumentsUpdated.Last();
			Assert.AreEqual(1000,documentRetaken.DocNum);
			Assert.AreEqual(mountItemUnmounted.MountItemNum,documentRetaken.MountItemNum);
			Assert.AreEqual("Retake: Slot 1 (teeth 1,2,3)",documentRetaken.Description);
			Assert.IsNull(DocumentInSlot(1),"The slot is empty again.");
			Assert.AreSame(documentRetaken,Controller.MountSlots.ListDocuments.Last(),"Recorded as an unmounted item of the mount.");
			//Its ImageEdit entry, FKey=DocNum, under the bound patient.
			Assert.AreEqual("55|1000|Retake: image DocNum 1000 moved from Slot 1 (teeth 1,2,3) of mount 7 to an unmounted item, labelled "
				+"\"Retake: Slot 1 (teeth 1,2,3)\". The slot is taken again. Session: "+SessionGuidText+".",_harness.Store.GetLogs(EnumPermType.ImageEdit).Single());
			//Without safe disable nothing is disabled: the change waits, and the indicator says so.
			Assert.AreEqual(0,_harness.Twain.ListDisables.Count);
			Assert.AreEqual(1,Controller.MountSlots.Target.ItemOrder,"The same slot is the target again.");
			Assert.AreEqual("Next image: 2 of 3: Max R, #4,5, then retake 1 of 3: Max R, #1,2,3",Controller.NextTargetText);
			Assert.AreEqual(1,Controller.CountRetakes);
			Assert.IsFalse(Controller.CanExecute(CaptureSessionAction.Retake),"The retaken image is no longer the shown one.");
			//The running enable's image lands in its own slot, then the session enables for the retake.
			_harness.Image();
			Assert.AreEqual(102,DocumentInSlot(2).MountItemNum);
			Assert.AreEqual(1,Controller.MountSlots.Target.ItemOrder);
			_harness.Ended(TwainCaptureEndReason.Completed);
			Assert.AreEqual(3,_harness.Twain.ListEnables.Count);
			Assert.AreEqual(101,_harness.LastRecord.MountItemTarget.MountItemNum);
			Assert.AreEqual("",Controller.NextTargetText);
			_harness.Expose();
			Assert.AreEqual(1002,DocumentInSlot(1).DocNum,"The retake is in the slot.");
			Assert.AreEqual(103,_harness.LastRecord.MountItemTarget.MountItemNum);
			_harness.Expose();
			Assert.AreEqual(CaptureSessionEndReason.MountFull,Controller.Summary.EndReason);
			StringAssert.StartsWith(Controller.Summary.GetText(),"4 images, 1 retake, 0 quarantined. Patient: Pat Test. Provider: DOC4. In effect: ");
			CollectionAssert.Contains(Controller.Summary.ListDocNums,1000L,"The rejected exposure is kept, never deleted.");
		}

		[TestMethod]
		public void Retake_WithSafeDisable_DisablesPlacesTheInFlightImageInItsSlot_ThenEnablesForTheRetake() {
			_harness.Started();
			_harness.Expose();
			_harness.Status(TwainCaptureState.WaitingForImage,isSafeDisableInEffect:true);
			Assert.IsTrue(_harness.Execute(CaptureSessionAction.Retake));
			CollectionAssert.AreEqual(new[] { TwainCaptureStopReason.TargetChanged },_harness.Twain.ListDisables,"Disabled in the safe order.");
			Assert.AreEqual(CaptureSessionPhase.Stopping,Controller.Phase);
			//W13: the disable is refused while the sensor software finishes; then it delivers what it held.
			_harness.Status(TwainCaptureState.WaitingForSource,isSafeDisableInEffect:true);
			Assert.AreEqual("Waiting for the sensor software to finish",Controller.IndicatorText);
			_harness.Image(AcquiredImageKind.Drained,TwainCaptureStopReason.TargetChanged);
			Assert.AreEqual(102,DocumentInSlot(2).MountItemNum,"The image of that enable goes to the slot that was its target.");
			_harness.Ended(TwainCaptureEndReason.Stopped,ended => {
				ended.StopReason=TwainCaptureStopReason.TargetChanged;
				ended.IsSafeDisableInEffect=true;
				ended.WasDisableRefused=true;
			});
			Assert.AreEqual(3,_harness.Twain.ListEnables.Count,"Enabled for the retake at once.");
			Assert.AreEqual(101,_harness.LastRecord.MountItemTarget.MountItemNum);
			Assert.AreEqual(CaptureSessionPhase.Enabled,Controller.Phase);
		}

		[TestMethod]
		public void Retake_Continuous_OnlyTheTargetChanges() {
			_harness.Setup.Options.SessionMode=TwainSessionMode.Continuous;
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage,isSafeDisableInEffect:true);
			_harness.Image();
			_harness.Image();
			Assert.AreEqual(1001,DocumentInSlot(2).DocNum);
			Assert.IsTrue(_harness.Execute(CaptureSessionAction.Retake));
			Assert.AreEqual(0,_harness.Twain.ListDisables.Count,"Nothing is disabled in continuous mode.");
			Assert.AreEqual("",Controller.NextTargetText,"The next image goes to the target as it is now.");
			Assert.AreEqual(2,Controller.MountSlots.Target.ItemOrder);
			_harness.Image();
			Assert.AreEqual(1002,DocumentInSlot(2).DocNum,"The retake fills the slot.");
			Assert.AreEqual(1001,Controller.MountSlots.ListDocuments.Last().DocNum,"The rejected image is unmounted.");
			_harness.Image();
			Assert.AreEqual(1003,DocumentInSlot(3).DocNum);
			CollectionAssert.AreEqual(new[] { TwainCaptureStopReason.End },_harness.Twain.ListDisables,"The full mount ends it, safe disable in effect.");
			Assert.AreEqual(1,_harness.Twain.ListEnables.Count);
		}

		[TestMethod]
		public void Retake_InState4_TheNextDeliberateStartEnablesForTheSlot() {
			_harness.Started();
			_harness.Expose();
			_harness.Ended(TwainCaptureEndReason.CloseRequested,ended => ended.ConditionCode=0);
			Assert.AreEqual(CaptureSessionPhase.Ready,Controller.Phase);
			Assert.IsTrue(_harness.Execute(CaptureSessionAction.Retake));
			Assert.AreEqual("",Controller.NextTargetText,"No enable runs.");
			Assert.AreEqual(1,Controller.MountSlots.Target.ItemOrder);
			Assert.AreEqual(2,_harness.Twain.ListEnables.Count,"Re-enabling still needs the deliberate start.");
			_harness.Arm();
			Assert.IsTrue(_harness.Start());
			Assert.AreEqual(101,_harness.LastRecord.MountItemTarget.MountItemNum);
		}

		[TestMethod]
		public void Retake_NotAvailable_WithoutAShownImageInASlot() {
			_harness.Started();
			Assert.IsFalse(Controller.CanExecute(CaptureSessionAction.Retake),"Nothing saved yet.");
			Assert.IsFalse(_harness.Execute(CaptureSessionAction.Retake));
			Assert.AreEqual(0,_harness.RadiographStore.ListMountItemsInserted.Count);
			CaptureControllerHarness harnessSingle=new CaptureControllerHarness(0);
			harnessSingle.Started();
			harnessSingle.Expose();
			Assert.IsFalse(harnessSingle.Controller.CanExecute(CaptureSessionAction.Retake),"Single-target mode has no slot to retake.");
			Assert.IsFalse(harnessSingle.Execute(CaptureSessionAction.Retake));
		}

		[TestMethod]
		public void Retake_ADatabaseWriteFails_NothingMoves() {
			_harness.Started();
			_harness.Expose();
			_harness.RadiographStore.ThrowOn="UpdateDocument";
			Assert.IsFalse(_harness.Execute(CaptureSessionAction.Retake));
			Assert.AreEqual("Could not retake Slot 1 (teeth 1,2,3): UpdateDocument failed",Controller.RedStripText);
			Assert.AreSame(_harness.RadiographStore.ListMountItemsInserted.Single(),_harness.RadiographStore.ListMountItemsDeleted.Single(),
				"The unmounted item made for it is deleted again.");
			Document document=DocumentInSlot(1);
			Assert.AreEqual(1000,document.DocNum,"Still in its slot.");
			Assert.AreEqual(101,document.MountItemNum);
			Assert.AreEqual("",document.Description);
			Assert.AreEqual(0,Controller.CountRetakes);
			Assert.AreEqual(0,_harness.Store.GetLogs(EnumPermType.ImageEdit).Count);
			Assert.AreEqual(2,Controller.MountSlots.Target.ItemOrder,"The target is unchanged.");
			CollectionAssert.Contains(_harness.ListSounds,CaptureSound.Error);
		}
		#endregion Retake

		#region Rotation
		[TestMethod]
		public void Rotate_TurnsTheShownImage90Degrees_AsItsDisplayTransform() {
			_harness.Started();
			Assert.IsFalse(Controller.CanExecute(CaptureSessionAction.Rotate),"No image shown yet.");
			_harness.Expose();
			Assert.IsTrue(Controller.CanExecute(CaptureSessionAction.Rotate));
			Assert.AreEqual(0,Controller.DegreesRotatedShown);
			Assert.IsTrue(_harness.Execute(CaptureSessionAction.Rotate));
			Assert.AreEqual(90,Controller.DegreesRotatedShown);
			Document document=_harness.RadiographStore.ListDocumentsUpdated.Last();
			Assert.AreEqual(1000,document.DocNum);
			Assert.AreEqual(90f,document.DegreesRotated,"Saved as DegreesRotated, as Rotate Right in the Imaging module.");
			Assert.AreEqual("Rotated to 90 degrees.",Controller.NoticeText);
			Assert.AreEqual("55|1000|Rotate: image DocNum 1000 ("+document.FileName+") in Slot 1 (teeth 1,2,3) rotated right 90 degrees: DegreesRotated "
				+"0 -> 90. Session: 11111111-2222-3333-4444-555555555555.",_harness.Store.GetLogs(EnumPermType.ImageEdit).Single(),
				"Logged under ImageEdit as the Imaging module's Rotate Right is.");
			//A key press rotates once per press.
			Controller.OnKeyDown(CaptureSessionKey.Rotate,_harness.Clock.NowMs,false);
			Controller.OnKeyDown(CaptureSessionKey.Rotate,_harness.Clock.NowMs,true);
			Assert.AreEqual(180,Controller.DegreesRotatedShown);
			_harness.Execute(CaptureSessionAction.Rotate);
			_harness.Execute(CaptureSessionAction.Rotate);
			Assert.AreEqual(0,Controller.DegreesRotatedShown,"Wraps around.");
			Assert.AreEqual(0,_harness.Store.ListMountItemDefsUpdated.Count,"Rotate alone changes no slot.");
		}

		[TestMethod]
		public void AlwaysRotateSlot_OfferedAfterACorrection_NeedsSetup_WritesTheDefinitionAndTheSlot_LogsOldAndNew() {
			_harness.Setup.MountDef=new MountDef { MountDefNum=3,Description="FMX" };
			_harness.Store.DictMountItemDefs[3]=Enumerable.Range(1,3).Select(x => new MountItemDef { MountItemDefNum=30+x,MountDefNum=3,ItemOrder=x })
				.ToList();
			_harness.Started();
			_harness.Expose();
			Assert.IsFalse(Controller.CanAlwaysRotateSlot,"Offered only after a correction.");
			_harness.Execute(CaptureSessionAction.Rotate);
			Assert.IsTrue(Controller.CanAlwaysRotateSlot);
			_harness.Store.HashSetPermsDenied.Add(EnumPermType.Setup);
			Assert.IsFalse(Controller.AlwaysRotateSlot());
			Assert.AreEqual(EnumPermType.Setup,_harness.Store.PermTypeChecked);
			Assert.AreEqual(CaptureSessionText.AlwaysRotateNotAuthorized,Controller.NoticeText);
			Assert.AreEqual(0,_harness.Store.ListMountItemDefsUpdated.Count);
			_harness.Store.HashSetPermsDenied.Clear();
			Assert.IsTrue(Controller.AlwaysRotateSlot());
			MountItemDef mountItemDef=_harness.Store.ListMountItemDefsUpdated.Single();
			Assert.AreEqual(31,mountItemDef.MountItemDefNum,"Found by MountDefNum and ItemOrder.");
			Assert.AreEqual(90,mountItemDef.RotateOnAcquire);
			MountItem mountItem=_harness.Store.ListMountItemsUpdated.Single();
			Assert.AreEqual(101,mountItem.MountItemNum,"The slot of this mount, so that its retakes come in turned.");
			Assert.AreEqual(90,mountItem.RotateOnAcquire);
			Assert.AreEqual("0|3|Always rotate this slot: mount definition \"FMX\" (MountDefNum 3), slot 1 (teeth 1,2,3): RotateOnAcquire 0 -> 90. "
				+"The slot of mount 7: RotateOnAcquire 0 -> 90. Computer: OPERATORY1.",_harness.Store.GetLogs(EnumPermType.Setup).Single(),
				"FKey=MountDefNum.");
			Assert.AreEqual("Slot 1 of \"FMX\" now turns 90 degrees on acquire.",Controller.NoticeText);
			Assert.IsFalse(Controller.CanAlwaysRotateSlot,"The slot matches the image now.");
			//The retake of that slot comes in turned.
			_harness.Execute(CaptureSessionAction.Retake);
			_harness.Image();
			_harness.Ended(TwainCaptureEndReason.Completed);
			_harness.Expose();
			Assert.AreEqual(90f,DocumentInSlot(1).DegreesRotated);
		}

		[TestMethod]
		public void AlwaysRotateSlot_TheMountsSlotCannotBeSaved_TheEntrySaysSo() {
			_harness.Setup.MountDef=new MountDef { MountDefNum=3,Description="FMX" };
			_harness.Store.DictMountItemDefs[3]=Enumerable.Range(1,3).Select(x => new MountItemDef { MountItemDefNum=30+x,MountDefNum=3,ItemOrder=x })
				.ToList();
			_harness.Started();
			_harness.Expose();
			_harness.Execute(CaptureSessionAction.Rotate);
			_harness.Store.ThrowOn="UpdateMountItem";
			Assert.IsTrue(Controller.AlwaysRotateSlot(),"The definition changed.");
			Assert.AreEqual("0|3|Always rotate this slot: mount definition \"FMX\" (MountDefNum 3), slot 1 (teeth 1,2,3): RotateOnAcquire 0 -> 90. "
				+"The slot of mount 7 could not be changed; it keeps RotateOnAcquire 0. Computer: OPERATORY1.",
				_harness.Store.GetLogs(EnumPermType.Setup).Single());
			StringAssert.Contains(Controller.NoticeText,"The slot of this mount could not be changed: UpdateMountItem failed");
		}

		[TestMethod]
		public void AlwaysRotateSlot_TheSetupEntryCannotBeWritten_TheChangeIsUndone() {
			_harness.Setup.MountDef=new MountDef { MountDefNum=3,Description="FMX" };
			_harness.Store.DictMountItemDefs[3]=Enumerable.Range(1,3).Select(x => new MountItemDef { MountItemDefNum=30+x,MountDefNum=3,ItemOrder=x })
				.ToList();
			_harness.Started();
			_harness.Expose();
			_harness.Execute(CaptureSessionAction.Rotate);
			_harness.Store.ThrowOn="MakeLogEntry";
			Assert.IsFalse(Controller.AlwaysRotateSlot());
			CollectionAssert.AreEqual(new[] { 90,0 },_harness.Store.ListMountItemDefsUpdated.Select(x => x.RotateOnAcquire).ToList(),
				"Design 10.7: no Setup change without its entry.");
			CollectionAssert.AreEqual(new[] { 90,0 },_harness.Store.ListMountItemsUpdated.Select(x => x.RotateOnAcquire).ToList());
			Assert.AreEqual("Always rotate this slot: the change could not be written to the security log, so it was undone.",Controller.NoticeText);
			Assert.IsTrue(Controller.CanAlwaysRotateSlot,"Still offered: nothing changed.");
		}

		[TestMethod]
		public void AlwaysRotateSlot_NotOfferedWithoutAMountDefinition() {
			_harness.Started();
			_harness.Expose();
			_harness.Execute(CaptureSessionAction.Rotate);
			Assert.IsNull(_harness.Setup.MountDef);
			Assert.IsFalse(Controller.CanAlwaysRotateSlot);
			Assert.IsFalse(Controller.AlwaysRotateSlot());
			Assert.AreEqual(0,_harness.Store.ListMountItemDefsUpdated.Count);
			Assert.AreEqual(0,_harness.Store.GetLogs(EnumPermType.Setup).Count);
		}
		#endregion Rotation
	}
}
