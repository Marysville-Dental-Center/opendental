// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using ImagingCapture.Metadata;
using ImagingCapture.Session;
using ImagingCapture.Settings;
using ImagingCapture.UI;
using ImagingDeviceManager.Capture;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace ImagingCapture.Tests {
	///<summary>CaptureSessionController (design 7.3-7.7, 7.11, 7.12, 10.2, 10.3, 10.8) on a stand-in TWAIN session, store, clock and dispatcher:
	///the deliberate start, the targets, the receive path, the ends of an enable and the finish. No database, no worker thread.</summary>
	[TestClass]
	public class CaptureSessionControllerTests {
		private CaptureControllerHarness _harness;

		[TestInitialize]
		public void Init() {
			_harness=new CaptureControllerHarness();
		}

		private CaptureSessionController Controller => _harness.Controller;

		#region Deliberate start
		[TestMethod]
		public void Begin_BannerShows_StartNotArmedFor2Seconds() {
			Controller.Begin();
			Assert.AreEqual(CaptureSessionPhase.Ready,Controller.Phase);
			Assert.AreEqual(CaptureStartKind.Start,Controller.StartKind);
			Assert.AreEqual("Start X-rays for Pat Test",Controller.StartText);
			Assert.IsFalse(Controller.CanStart,"Not armed in the first 2 s.");
			Assert.IsFalse(Controller.RequestStart());
			_harness.Dispatcher.RunPending();
			Assert.AreEqual(0,_harness.ListSessions.Count,"Nothing opened.");
			_harness.Clock.Advance(1999);
			Assert.IsFalse(Controller.CanStart);
			_harness.Clock.Advance(1);
			int version=Controller.Version;
			Controller.OnTick();
			Assert.IsTrue(Controller.CanStart);
			Assert.AreNotEqual(version,Controller.Version,"OnTick tells the window that Start is armed now.");
			Assert.IsTrue(_harness.Start());
			Assert.AreEqual(1,_harness.Twain.CountOpen);
			Assert.AreEqual(1,_harness.Dispatcher.CountBackground,"Open runs off the UI thread.");
			Assert.AreEqual(CaptureSessionPhase.Enabled,Controller.Phase);
			Assert.AreEqual(1,_harness.Twain.ListEnables.Count);
		}

		[TestMethod]
		public void KeysInTheFirst2Seconds_Ignored_AConfirmPressMadeThenNeverStarts() {
			Controller.Begin();
			_harness.Clock.Advance(500);
			Assert.IsFalse(Controller.OnKeyDown(CaptureSessionKey.Confirm,_harness.Clock.NowMs),"Ignored in the first 2 s.");
			Assert.IsFalse(Controller.OnKeyDown(CaptureSessionKey.End,_harness.Clock.NowMs),"A session key too.");
			Assert.AreEqual(CaptureSessionPhase.Ready,Controller.Phase,"End was ignored.");
			//The same press, still held past the 2 s and for more than 1 s: it never starts.
			for(int i=0;i<30;i++) {
				_harness.Clock.Advance(100);
				Controller.OnKeyDown(CaptureSessionKey.Confirm,_harness.Clock.NowMs);//auto-repeat
				Controller.OnTick();
			}
			_harness.Dispatcher.RunPending();
			Assert.AreEqual(0,_harness.ListSessions.Count,"A press that began in the ignored 2 s never confirms.");
			Controller.OnKeyUp(CaptureSessionKey.Confirm,_harness.Clock.NowMs);
			//A new press after the 2 s, held 1 s: it starts.
			Controller.OnKeyDown(CaptureSessionKey.Confirm,_harness.Clock.NowMs);
			_harness.Clock.Advance(CaptureSessionController.ConfirmHoldMs);
			Controller.OnTick();
			_harness.Dispatcher.RunPending();
			Assert.AreEqual(1,_harness.ListSessions.Count);
			Assert.AreEqual(CaptureSessionPhase.Enabled,Controller.Phase);
		}

		[TestMethod]
		public void ConfirmKey_HeldSinceBeforeTheBanner_OnlyRepeatsSeen_NeverStarts() {
			_harness.BeginArmed();
			for(int i=0;i<20;i++) {
				Controller.OnKeyDown(CaptureSessionKey.Confirm,_harness.Clock.NowMs,isRepeat:true);
				_harness.Clock.Advance(100);
				Controller.OnTick();
			}
			_harness.Dispatcher.RunPending();
			Assert.AreEqual(0,_harness.ListSessions.Count,"The press began while the controller was not watching.");
			Controller.OnKeyUp(CaptureSessionKey.Confirm,_harness.Clock.NowMs);
			Controller.OnKeyDown(CaptureSessionKey.Confirm,_harness.Clock.NowMs,isRepeat:false);
			_harness.Clock.Advance(CaptureSessionController.ConfirmHoldMs);
			Controller.OnKeyDown(CaptureSessionKey.Confirm,_harness.Clock.NowMs,isRepeat:true);
			_harness.Dispatcher.RunPending();
			Assert.AreEqual(1,_harness.ListSessions.Count,"A press seen from its first key-down starts.");
		}

		[TestMethod]
		public void SessionKey_Repeat_RunsTheCommandOncePerPress() {
			_harness=new CaptureControllerHarness(5);
			_harness.BeginArmed();
			Controller.OnKeyDown(CaptureSessionKey.NextSlot,_harness.Clock.NowMs,isRepeat:false);
			Controller.OnKeyDown(CaptureSessionKey.NextSlot,_harness.Clock.NowMs,isRepeat:true);
			Controller.OnKeyDown(CaptureSessionKey.NextSlot,_harness.Clock.NowMs,isRepeat:true);
			Assert.AreEqual(2,Controller.MountSlots.Target.ItemOrder);
		}

		[TestMethod]
		public void ConfirmKey_HeldLessThan1Second_DoesNotStart() {
			_harness.BeginArmed();
			Controller.OnKeyDown(CaptureSessionKey.Confirm,_harness.Clock.NowMs);
			_harness.Clock.Advance(900);
			Controller.OnTick();
			Assert.IsFalse(Controller.OnKeyUp(CaptureSessionKey.Confirm,_harness.Clock.NowMs));
			_harness.Clock.Advance(200);
			Controller.OnTick();
			_harness.Dispatcher.RunPending();
			Assert.AreEqual(0,_harness.ListSessions.Count);
			Assert.AreEqual(CaptureSessionPhase.Ready,Controller.Phase);
		}

		[TestMethod]
		public void ConfirmKey_HeldOneSecond_StartsByRepeatOrRelease() {
			_harness.BeginArmed();
			Controller.OnKeyDown(CaptureSessionKey.Confirm,_harness.Clock.NowMs);
			_harness.Clock.Advance(CaptureSessionController.ConfirmHoldMs);
			Assert.IsTrue(Controller.OnKeyDown(CaptureSessionKey.Confirm,_harness.Clock.NowMs),"An auto-repeat key-down after 1 s starts.");
			_harness.Dispatcher.RunPending();
			Assert.AreEqual(1,_harness.ListSessions.Count);
			//The release of a press that already started does not start again.
			Assert.IsFalse(Controller.OnKeyUp(CaptureSessionKey.Confirm,_harness.Clock.NowMs));
			//Released after 1 s with no repeat or tick between: it starts on release.
			CaptureControllerHarness harness=new CaptureControllerHarness();
			harness.BeginArmed();
			harness.Controller.OnKeyDown(CaptureSessionKey.Confirm,harness.Clock.NowMs);
			harness.Clock.Advance(1200);
			Assert.IsTrue(harness.Controller.OnKeyUp(CaptureSessionKey.Confirm,harness.Clock.NowMs));
			harness.Dispatcher.RunPending();
			Assert.AreEqual(1,harness.ListSessions.Count);
		}

		[TestMethod]
		public void SessionKeys_NeverConfirm() {
			_harness.BeginArmed();
			foreach(CaptureSessionKey key in new[] { CaptureSessionKey.Retake,CaptureSessionKey.Skip,CaptureSessionKey.NextSlot,
				CaptureSessionKey.PreviousSlot,CaptureSessionKey.Rotate,CaptureSessionKey.Disable })
			{
				Controller.OnKeyDown(key,_harness.Clock.NowMs);
				_harness.Clock.Advance(1500);
				Controller.OnKeyDown(key,_harness.Clock.NowMs);
				Controller.OnTick();
				Controller.OnKeyUp(key,_harness.Clock.NowMs);
			}
			_harness.Dispatcher.RunPending();
			Assert.AreEqual(0,_harness.ListSessions.Count,"No session key or pedal opens or enables anything.");
			Assert.AreEqual(CaptureSessionPhase.Ready,Controller.Phase);
		}

		[TestMethod]
		public void OlderMount_NeedsExplicitConfirmation() {
			_harness=new CaptureControllerHarness(3,new DateTime(2026,10,20,9,0,0));
			Controller.Begin();
			Assert.AreEqual(CaptureSessionPhase.ConfirmingOlderMount,Controller.Phase);
			StringAssert.Contains(Controller.OlderMountText,"before today");
			StringAssert.Contains(Controller.OlderMountText,"\"FMX\"");
			_harness.Arm();
			Assert.IsFalse(Controller.CanStart);
			Assert.IsFalse(Controller.RequestStart());
			Controller.OnKeyDown(CaptureSessionKey.Confirm,_harness.Clock.NowMs);
			_harness.Clock.Advance(1500);
			Controller.OnTick();
			_harness.Dispatcher.RunPending();
			Assert.AreEqual(0,_harness.ListSessions.Count,"The Confirm key does not confirm the older mount.");
			Controller.OnKeyUp(CaptureSessionKey.Confirm,_harness.Clock.NowMs);
			Controller.ConfirmOlderMount();
			Assert.AreEqual(CaptureSessionPhase.Ready,Controller.Phase);
			Assert.IsFalse(Controller.CanStart,"The banner appears now: 2 s again.");
			_harness.Arm();
			Assert.IsTrue(_harness.Start());
			Assert.AreEqual(1,_harness.Twain.ListEnables.Count);
		}

		[TestMethod]
		public void OlderMount_Declined_FinishesWithNothing() {
			_harness=new CaptureControllerHarness(3,new DateTime(2026,10,20,9,0,0));
			CaptureSessionSummary summaryRaised=null;
			Controller.SessionFinished+=summary => summaryRaised=summary;
			Controller.Begin();
			Controller.DeclineOlderMount();
			Assert.AreEqual(CaptureSessionPhase.Finished,Controller.Phase);
			Assert.AreSame(Controller.Summary,summaryRaised);
			Assert.AreEqual(CaptureSessionEndReason.CancelledBeforeStart,summaryRaised.EndReason);
			Assert.AreEqual(0,_harness.ListSessions.Count);
			Assert.IsTrue(Controller.IsMountEmpty);
		}

		[TestMethod]
		public void PatientChangedBeforeStart_Refused_NothingOpened() {
			_harness.BeginArmed();
			_harness.PatNumCur=77;
			Assert.IsFalse(_harness.Start());
			Assert.AreEqual(0,_harness.ListSessions.Count);
			Assert.AreEqual("Patient changed in the main window. This session is still for Pat Test.",Controller.PatientChangedText);
			Assert.AreEqual(CaptureIndicatorState.Error,Controller.IndicatorState);
			Assert.AreEqual(CaptureStartKind.Start,Controller.StartKind);
			CollectionAssert.Contains(_harness.ListSounds,CaptureSound.Error);
		}

		[TestMethod]
		public void PatientChangedBetweenSlots_NextEnableRefused() {
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage);
			_harness.Image();
			_harness.PatNumCur=0;//no patient in the main window
			_harness.Ended(TwainCaptureEndReason.Completed);
			Assert.AreEqual(1,_harness.Twain.ListEnables.Count,"getPatNumCur() is checked before every enable.");
			Assert.AreEqual(CaptureSessionPhase.Ready,Controller.Phase);
			Assert.AreEqual(CaptureStartKind.ReEnable,Controller.StartKind);
			Assert.AreNotEqual("",Controller.PatientChangedText);
		}

		[TestMethod]
		public void Open_SessionPreparedBeforeOpen() {
			_harness.Started();
			FakeTwainCaptureSession twain=_harness.Twain;
			Assert.AreEqual("Example Sensor DS",twain.OpenProductName);
			Assert.AreEqual(_harness.Setup.SessionGuid,twain.SessionGuidAtOpen,"SessionGuid is set before Open.");
			Assert.AreEqual(55,twain.QuarantineContextAtOpen.PatNum,"The bound patient.");
			Assert.AreEqual("Pat Test",twain.QuarantineContextAtOpen.PatientName);
			Assert.IsNotNull(twain.FuncReadMetadataKeysAtOpen);
			Assert.AreEqual("c-7f3a91",twain.FuncReadMetadataKeysAtOpen(TestImages.Json8Bit).CaptureId,"CaptureMetadataParser.ReadKeys.");
			Assert.AreNotSame(_harness.Setup.Options,twain.OpenOptions,"Open gets a copy of the options.");
		}

		[TestMethod]
		public void OpenFails_ErrorAndRetry_WithANewSession() {
			_harness.ActionOnNewSession=session => {
				if(_harness.ListSessions.Count==0) {
					session.OpenResult=new TwainCaptureOpenResult { Error=TwainCaptureOpenError.SourceNotFound };
				}
			};
			_harness.Started();
			Assert.AreEqual(CaptureSessionPhase.Ready,Controller.Phase);
			Assert.AreEqual(CaptureIndicatorState.Error,Controller.IndicatorState);
			Assert.AreEqual("Imaging device 'Example Sensor DS' not found. Check Setup > Imaging > Devices.",Controller.IndicatorText);
			Assert.AreEqual(CaptureStartKind.Retry,Controller.StartKind);
			Assert.IsFalse(Controller.CanStart,"Retry is a re-enable: 2 s again.");
			_harness.Arm();
			Assert.IsTrue(_harness.Start());
			Assert.AreEqual(2,_harness.ListSessions.Count,"A new worker after a failed open.");
			Assert.AreEqual(1,_harness.ListSessions[0].CountBeginClose);
			Assert.AreEqual(1,_harness.ListSessions[1].ListEnables.Count);
		}

		[TestMethod]
		public void EnableRefusedByTheSession_RetryOpensAgain() {
			_harness.Started();
			_harness.Twain.IsEnableRefused=true;//the worker closed under the controller
			_harness.Expose();
			Assert.AreEqual(CaptureIndicatorState.Error,Controller.IndicatorState);
			Assert.AreEqual(CaptureSessionText.NotOpen,Controller.IndicatorText);
			Assert.AreEqual(CaptureStartKind.Retry,Controller.StartKind);
			Assert.IsFalse(Controller.AreSessionKeysActive);
			_harness.Arm();
			Assert.IsTrue(_harness.Start());
			Assert.AreEqual(2,_harness.ListSessions.Count,"Retry opens a new worker.");
			Assert.AreEqual(102,_harness.LastRecord.MountItemTarget.MountItemNum,"Slot 1 has its image; slot 2 is next.");
		}

		[TestMethod]
		public void OpenFails_SourceGate_NothingToRetry() {
			_harness.ActionOnNewSession=session => session.OpenResult=new TwainCaptureOpenResult { Error=TwainCaptureOpenError.SourceNotEnabledHere };
			_harness.Started();
			Assert.AreEqual(CaptureEndText.SourceNotEnabledHere,Controller.IndicatorText);
			Assert.AreEqual(CaptureStartKind.None,Controller.StartKind);
			_harness.Arm();
			Assert.IsFalse(Controller.CanStart);
			Assert.IsTrue(Controller.CanExecute(CaptureSessionAction.End));
		}
		#endregion Deliberate start

		#region Targets and receive
		[TestMethod]
		public void PerSlot_AdvancesToAFullMount_ThenFinishes() {
			CaptureSessionSummary summaryRaised=null;
			Controller.SessionFinished+=summary => summaryRaised=summary;
			_harness.Started();
			Assert.AreEqual("1 of 3: Max R, #1,2,3",Controller.TargetText);
			Assert.IsTrue(Controller.AreSessionKeysActive,"Hotkeys are registered while enabled.");
			_harness.Status(TwainCaptureState.WaitingForImage);
			Assert.AreEqual(CaptureIndicatorState.WaitingForExposure,Controller.IndicatorState);
			Assert.AreEqual("Sensor software open, waiting for exposure: 1 of 3: Max R, #1,2,3",Controller.IndicatorText);
			_harness.Image();
			Assert.AreEqual(CaptureIndicatorState.Saved,Controller.IndicatorState);
			Assert.AreEqual("Saved to slot 1 (teeth 1,2,3)",Controller.IndicatorText);
			Assert.AreEqual("2 of 3: Max R, #4,5",Controller.TargetText,"The target advances to the next empty slot.");
			_harness.Status(TwainCaptureState.Stopping);
			Assert.AreEqual(CaptureIndicatorState.Saved,Controller.IndicatorState,"The PerSlot stop after Count=0 keeps 'Saved' showing.");
			_harness.Ended(TwainCaptureEndReason.Completed);
			Assert.AreEqual(2,_harness.Twain.ListEnables.Count,"PerSlot enables again for the next slot, with no confirmation.");
			Assert.AreEqual(102,_harness.LastRecord.MountItemTarget.MountItemNum);
			_harness.Expose();
			Assert.AreEqual(3,_harness.Twain.ListEnables.Count);
			_harness.Expose();
			Assert.AreEqual(CaptureSessionPhase.Finished,Controller.Phase);
			Assert.AreEqual(3,_harness.Twain.ListEnables.Count,"No enable once the mount is full.");
			Assert.AreEqual(1,_harness.Twain.CountBeginClose,"The source closes.");
			Assert.AreEqual(0,_harness.Twain.ListDisables.Count,"No disable of the fork's own: each enable ended by itself.");
			Assert.IsFalse(Controller.AreSessionKeysActive);
			//The documents: the bound patient, the category, the provider, one per slot in ItemOrder.
			List<Document> listDocuments=_harness.RadiographStore.ListDocumentsUpdated;
			CollectionAssert.AreEqual(new long[] { 101,102,103 },listDocuments.Select(x => x.MountItemNum).ToList());
			CollectionAssert.AreEqual(new[] { "1,2,3","4,5","6" },listDocuments.Select(x => x.ToothNumbers).ToList());
			Assert.IsTrue(listDocuments.All(x => x.ProvNum==4 && x.PatNum==55));
			Assert.IsTrue(_harness.RadiographStore.ListPatientsImported.All(x => x.PatNum==55));
			Assert.IsTrue(_harness.RadiographStore.ListCategoriesImported.All(x => x==20));
			Assert.AreEqual(4,_harness.RadiographStore.ListMountsUpdated.Last().ProvNum,"Mount.ProvNum (design 10.8).");
			Assert.AreEqual(3,_harness.ListSounds.Count(x => x==CaptureSound.Saved));
			//The summary.
			Assert.AreSame(Controller.Summary,summaryRaised);
			Assert.AreEqual(CaptureSessionEndReason.MountFull,summaryRaised.EndReason);
			Assert.AreEqual(3,summaryRaised.CountImages);
			Assert.AreEqual(0,summaryRaised.CountQuarantined);
			Assert.AreEqual(55,summaryRaised.PatNum);
			Assert.AreEqual(7,summaryRaised.MountNum);
			Assert.AreEqual(4,summaryRaised.ProvNum);
			Assert.AreEqual("3 images, 0 retakes, 0 quarantined. Patient: Pat Test. Provider: DOC4.",summaryRaised.GetText());
			Assert.IsFalse(Controller.IsMountEmpty);
		}

		[TestMethod]
		public void Provider_EditableBeforeStartOnly_AppliedToDocuments() {
			Controller.Begin();
			Assert.AreEqual(4,Controller.ProvNum);
			Assert.AreEqual("DOC4",Controller.ProviderAbbr);
			Assert.IsTrue(Controller.SetProvider(9));
			_harness.Arm();
			_harness.Start();
			Assert.IsFalse(Controller.CanEditProvider);
			Assert.IsFalse(Controller.SetProvider(5),"Not after Start (design 10.8).");
			_harness.Expose();
			Assert.AreEqual(9,_harness.RadiographStore.ListDocumentsUpdated[0].ProvNum);
		}

		[TestMethod]
		public void SingleTarget_TakeAnother_UntilEnd() {
			_harness=new CaptureControllerHarness(0);
			Controller.Begin();
			Assert.IsTrue(Controller.IsSingleTarget);
			string error;
			Assert.IsTrue(Controller.SetToothNumbers(" 8, 9 ",out error),error);
			Assert.AreEqual("Single image, Max anterior, #8,9",Controller.TargetText);
			Assert.IsFalse(Controller.SetToothNumbers("2-5",out error));
			_harness.Arm();
			_harness.Start();
			Assert.AreEqual("Single image (teeth 8,9)",_harness.Twain.LastRequest.TargetLabel);
			Assert.AreEqual(8,_harness.Twain.LastRequest.Bits,"No mount slot: 8 bits.");
			_harness.Expose();
			Assert.AreEqual(1,_harness.Twain.ListEnables.Count,"No automatic enable without a mount.");
			Assert.AreEqual(CaptureSessionPhase.Ready,Controller.Phase);
			Assert.AreEqual(CaptureStartKind.TakeAnother,Controller.StartKind);
			Assert.AreEqual("Take another",Controller.StartText);
			StringAssert.StartsWith(Controller.IndicatorText,"Saved as a single image (teeth 8,9)");
			Assert.AreEqual("8,9",_harness.RadiographStore.ListDocumentsUpdated[0].ToothNumbers);
			Assert.AreEqual(0,_harness.RadiographStore.ListDocumentsUpdated[0].MountItemNum);
			Assert.IsFalse(Controller.CanStart,"Take another is a re-enable: 2 s again.");
			Assert.IsTrue(Controller.SetToothNumbers("10",out error),"The teeth can change between images.");
			_harness.Arm();
			Assert.IsTrue(_harness.Start());
			Assert.AreEqual(2,_harness.Twain.ListEnables.Count);
			Assert.IsFalse(Controller.SetToothNumbers("11",out error),"Not while enabled.");
			_harness.Expose();
			Assert.AreEqual("10",_harness.RadiographStore.ListDocumentsUpdated[1].ToothNumbers,"Each image is its own document with the banner's teeth.");
			Assert.IsTrue(_harness.Execute(CaptureSessionAction.End));
			Assert.AreEqual(CaptureSessionPhase.Finished,Controller.Phase);
			Assert.AreEqual(2,Controller.Summary.CountImages);
			Assert.AreEqual(CaptureSessionEndReason.Ended,Controller.Summary.EndReason);
			Assert.AreEqual(0,Controller.Summary.MountNum);
		}

		[TestMethod]
		public void SkipNextPreviousAndTap_MoveTheTargetAmongEmptySlots() {
			_harness=new CaptureControllerHarness(5);
			Controller.Begin();
			Assert.AreEqual(1,Controller.MountSlots.Target.ItemOrder);
			Assert.IsTrue(_harness.Execute(CaptureSessionAction.NextSlot));
			Assert.AreEqual(2,Controller.MountSlots.Target.ItemOrder);
			Assert.IsTrue(_harness.Execute(CaptureSessionAction.PreviousSlot));
			Assert.AreEqual(1,Controller.MountSlots.Target.ItemOrder);
			Assert.IsTrue(_harness.Execute(CaptureSessionAction.PreviousSlot),"Wraps around.");
			Assert.AreEqual(5,Controller.MountSlots.Target.ItemOrder);
			int idxSlot3=Controller.MountSlots.IndexOf(Controller.MountSlots.ListMountItems.First(x => x.ItemOrder==3));
			Assert.IsTrue(Controller.SelectSlot(idxSlot3));
			Assert.AreEqual(3,Controller.MountSlots.Target.ItemOrder);
			Assert.IsFalse(Controller.SelectSlot(0),"A text item is no target.");
			Assert.IsTrue(_harness.Execute(CaptureSessionAction.Skip));
			Assert.AreEqual(4,Controller.MountSlots.Target.ItemOrder);
			List<CaptureSlotView> listViews=Controller.GetSlotViews();
			CollectionAssert.AreEqual(new[] { 1,2,3,4,5 },listViews.Select(x => x.MountItem.ItemOrder).ToList(),"Slots only, in ItemOrder.");
			Assert.IsTrue(listViews.Single(x => x.MountItem.ItemOrder==3).IsSkipped);
			Assert.IsTrue(listViews.Single(x => x.MountItem.ItemOrder==4).IsTarget);
		}

		[TestMethod]
		public void Skip_SkippedSlotsComeLast_ThenTakeTheSkippedSlots() {
			_harness.BeginArmed();
			Assert.IsTrue(_harness.Execute(CaptureSessionAction.Skip));//skip slot 1
			_harness.Start();
			Assert.AreEqual(102,_harness.LastRecord.MountItemTarget.MountItemNum);
			_harness.Expose();
			Assert.AreEqual(103,_harness.LastRecord.MountItemTarget.MountItemNum,"Slot 3 next, not the skipped slot 1.");
			_harness.Expose();
			Assert.AreEqual(CaptureSessionPhase.Ready,Controller.Phase,"Only skipped slots are left: no automatic enable.");
			Assert.AreEqual(CaptureStartKind.TakeSkipped,Controller.StartKind);
			Assert.AreEqual(1,Controller.MountSlots.Target.ItemOrder);
			_harness.Arm();
			_harness.Start();
			Assert.AreEqual(101,_harness.LastRecord.MountItemTarget.MountItemNum);
			_harness.Expose();
			Assert.AreEqual(CaptureSessionEndReason.MountFull,Controller.Summary.EndReason);
		}

		[TestMethod]
		public void TargetChangeWhileEnabled_WithoutSafeDisable_ImageGoesToTheEnablesSlot_ThenTheChosenSlot() {
			_harness=new CaptureControllerHarness(5);
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage,isSafeDisableInEffect:false);
			int idxSlot4=Controller.MountSlots.IndexOf(Controller.MountSlots.ListMountItems.First(x => x.ItemOrder==4));
			Assert.IsTrue(Controller.SelectSlot(idxSlot4));
			Assert.AreEqual(0,_harness.Twain.ListDisables.Count,"No disable without safe disable (design 7.6).");
			Assert.AreEqual("Next image: 1 of 5: Max R, #1,2,3, then 4 of 5: Max anterior, #7,8,9,10",Controller.NextTargetText);
			_harness.Image();
			Assert.AreEqual(101,_harness.RadiographStore.ListDocumentsUpdated[0].MountItemNum,"The image of the running enable keeps its slot.");
			Assert.AreEqual(4,Controller.MountSlots.Target.ItemOrder,"The chosen target stays.");
			_harness.Ended(TwainCaptureEndReason.Completed);
			Assert.AreEqual(104,_harness.LastRecord.MountItemTarget.MountItemNum);
			Assert.AreEqual("",Controller.NextTargetText);
		}

		[TestMethod]
		public void TargetChangeWhileEnabled_WithSafeDisable_DisablesPlacesDrainedImage_ThenEnablesForTheNewTarget() {
			_harness=new CaptureControllerHarness(5);
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage,isSafeDisableInEffect:true);
			Assert.IsTrue(_harness.Execute(CaptureSessionAction.Skip));
			CollectionAssert.AreEqual(new[] { TwainCaptureStopReason.TargetChanged },_harness.Twain.ListDisables);
			Assert.AreEqual(CaptureSessionPhase.Stopping,Controller.Phase);
			//The source delivered what it held during the safe stop.
			_harness.Image(AcquiredImageKind.Drained,TwainCaptureStopReason.TargetChanged);
			Assert.AreEqual(101,_harness.RadiographStore.ListDocumentsUpdated[0].MountItemNum,"Placed where its enable was for.");
			_harness.Ended(TwainCaptureEndReason.Stopped,ended => {
				ended.StopReason=TwainCaptureStopReason.TargetChanged;
				ended.IsSafeDisableInEffect=true;
			});
			Assert.AreEqual(2,_harness.Twain.ListEnables.Count,"Enabled for the new target.");
			Assert.AreEqual(102,_harness.LastRecord.MountItemTarget.MountItemNum);
			Assert.AreEqual(CaptureSessionPhase.Enabled,Controller.Phase);
		}

		[TestMethod]
		public void Continuous_ImagesFillTheCurrentTarget_FullMountEndsTheEnable_WithSafeDisable() {
			_harness.Setup.Options.SessionMode=TwainSessionMode.Continuous;
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage,isSafeDisableInEffect:true);
			_harness.Image();
			_harness.Image();
			_harness.Image();
			CollectionAssert.AreEqual(new long[] { 101,102,103 },_harness.RadiographStore.ListDocumentsUpdated.Select(x => x.MountItemNum).ToList());
			CollectionAssert.AreEqual(new[] { TwainCaptureStopReason.End },_harness.Twain.ListDisables,"The full mount disables the continuous enable.");
			Assert.AreEqual(CaptureSessionPhase.Stopping,Controller.Phase);
			_harness.Ended(TwainCaptureEndReason.Stopped,ended => ended.StopReason=TwainCaptureStopReason.End);
			Assert.AreEqual(CaptureSessionPhase.Finished,Controller.Phase);
			Assert.AreEqual(1,_harness.Twain.ListEnables.Count);
		}

		[TestMethod]
		public void Continuous_FullMountWithoutSafeDisable_NoDisableOfItsOwn_TheOperatorEnds() {
			_harness.Setup.Options.SessionMode=TwainSessionMode.Continuous;
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage,isSafeDisableInEffect:false);
			_harness.Image();
			_harness.Image();
			_harness.Image();
			Assert.AreEqual(3,_harness.RadiographStore.ListDocumentsUpdated.Count);
			Assert.AreEqual(0,_harness.Twain.ListDisables.Count,"Design 7.6, 10.6: no disable of the fork's own without safe disable.");
			Assert.AreEqual(CaptureSessionPhase.Enabled,Controller.Phase);
			Assert.AreEqual(CaptureSessionText.MountFullContinuous,Controller.NoticeText);
			_harness.Image();
			Assert.AreEqual("Every slot of the mount already holds an image.",_harness.Twain.ListQuarantined.Single().Reason);
			Assert.IsTrue(_harness.Execute(CaptureSessionAction.End));
			Assert.AreEqual(0,_harness.Twain.ListDisables.Count,"End warns first.");
			Assert.IsTrue(_harness.Execute(CaptureSessionAction.End));
			CollectionAssert.AreEqual(new[] { TwainCaptureStopReason.End },_harness.Twain.ListDisables);
			_harness.Ended(TwainCaptureEndReason.Stopped,ended => ended.StopReason=TwainCaptureStopReason.End);
			Assert.AreEqual(CaptureSessionPhase.Finished,Controller.Phase);
		}

		[TestMethod]
		public void Continuous_ASecondImageWhoseSaveFails_IsQuarantinedForItsOwnSlot_NotTheEnablesFirst() {
			_harness.Setup.Options.SessionMode=TwainSessionMode.Continuous;
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage);
			_harness.Image();
			_harness.RadiographStore.ThrowOn="Import";
			_harness.Image();
			FakeQuarantineCall call=_harness.Twain.ListQuarantined.Single();
			Assert.AreEqual(101,_harness.LastRecord.MountItemTarget.MountItemNum,"The enable started for slot 1...");
			Dictionary<string,string> dictFields=call.Image.SidecarFields.ToDictionary(x => x.Key,x => x.Value);
			Assert.AreEqual("102",dictFields[CaptureSidecarKeys.MountItemNum],"...the image was meant for slot 2.");
			Assert.AreEqual("2",dictFields[CaptureSidecarKeys.SlotItemOrder]);
			Assert.AreEqual("4,5",dictFields[CaptureSidecarKeys.ToothNumbers]);
			Assert.AreEqual("1",dictFields[CaptureSidecarKeys.SlotOfImage]);
			Assert.AreEqual("Slot 2 (teeth 4,5)",call.Image.TargetLabel);
		}

		[TestMethod]
		public void ContinuousConfigured_ButTheEnableRanPerSlot_EnablesAgain() {
			_harness.Setup.Options.SessionMode=TwainSessionMode.Continuous;
			_harness.Started();
			_harness.Expose();
			Assert.AreEqual(2,_harness.Twain.ListEnables.Count,"Completed means the source was disabled after Count=0.");
		}

		[TestMethod]
		public void DrainedImage_SlotTaken_GoesToTheQuarantine() {
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage);
			_harness.Image();
			_harness.Image(AcquiredImageKind.Drained,TwainCaptureStopReason.Cancel);
			Assert.AreEqual(1,_harness.RadiographStore.ListDocumentsUpdated.Count);
			FakeQuarantineCall call=_harness.Twain.ListQuarantined.Single();
			StringAssert.Contains(call.Reason,"already holds an image");
			Assert.IsFalse(call.IsPatientUnknown);
			Assert.AreEqual(55,call.Context.PatNum,"Tagged with the bound patient.");
			Assert.AreEqual(1,Controller.CountQuarantined);
			Assert.AreEqual("2 unassigned captures on this computer.",Controller.UnassignedText);
		}

		[TestMethod]
		public void ExtraImage_Quarantined_WithTheNotice() {
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage);
			_harness.Image();
			_harness.Image(AcquiredImageKind.Extra);
			Assert.AreEqual(1,_harness.RadiographStore.ListDocumentsUpdated.Count,"Only the first image is saved.");
			Assert.AreEqual(1,_harness.Twain.ListQuarantined.Count);
			Assert.AreEqual(CaptureSessionText.ExtraImages,Controller.NoticeText);
			Assert.AreEqual("The sensor software sent more images than expected; they are in unassigned captures.",Controller.NoticeText);
			_harness.Ended(TwainCaptureEndReason.Completed,ended => ended.ExtraImageCount=1);
			Assert.AreEqual(2,_harness.Twain.ListEnables.Count,"The series goes on.");
			Assert.AreEqual(CaptureSessionText.ExtraImages,Controller.NoticeText);
		}

		[TestMethod]
		public void SaveFails_ImageQuarantined_RedStrip_PausedUntilADeliberateReEnable() {
			_harness.RadiographStore.ThrowOn="Import";
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage);
			_harness.Image();
			Assert.AreEqual("Could not save: Import failed",Controller.RedStripText);
			FakeQuarantineCall call=_harness.Twain.ListQuarantined.Single();
			Assert.AreEqual("Could not save: Import failed",call.Reason);
			Assert.AreEqual(CaptureIndicatorState.Error,Controller.IndicatorState);
			CollectionAssert.Contains(_harness.ListSounds,CaptureSound.Error);
			Assert.AreEqual(1,Controller.MountSlots.Target.ItemOrder,"The slot does not advance.");
			_harness.Ended(TwainCaptureEndReason.Completed);
			Assert.AreEqual(1,_harness.Twain.ListEnables.Count,"Paused: no automatic enable.");
			Assert.AreEqual(CaptureSessionPhase.Ready,Controller.Phase);
			Assert.AreEqual(CaptureStartKind.ReEnable,Controller.StartKind);
			Assert.AreEqual("Could not save: Import failed",Controller.RedStripText);
			_harness.RadiographStore.ThrowOn="";
			_harness.Arm();
			Assert.IsTrue(_harness.Start());
			Assert.AreEqual(101,_harness.LastRecord.MountItemTarget.MountItemNum,"The same slot again.");
			Assert.AreEqual("",Controller.RedStripText);
		}

		[TestMethod]
		public void QuarantineFails_RedStripNamesTheJournalCopy() {
			_harness.RadiographStore.ThrowOn="Import";
			_harness.ActionOnNewSession=session => session.QuarantineError="the disk is full";
			_harness.Started();
			AcquiredImage image=TestImages.Image8(4,4,(x,y) => 1);
			image.JournalPath=@"C:\Journal\j1.bmp";
			_harness.Twain.RaiseImage(image);
			_harness.Dispatcher.RunPending();
			StringAssert.Contains(Controller.RedStripText,"the disk is full");
			StringAssert.Contains(Controller.RedStripText,@"C:\Journal\j1.bmp");
			Assert.AreEqual(0,Controller.CountQuarantined);
		}

		[TestMethod]
		public void ImageAfterTheSessionEnded_Quarantined() {
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage);
			FakeTwainCaptureSession twain=_harness.Twain;
			AcquiredImage image=TestImages.Image8(4,4,(x,y) => 1);
			twain.RaiseImage(image);//raised before End, handled after it
			Controller.OnClosing();
			_harness.Dispatcher.RunPending();
			Assert.AreEqual(CaptureSessionPhase.Finished,Controller.Phase);
			Assert.AreEqual(0,_harness.RadiographStore.ListDocumentsUpdated.Count);
			StringAssert.Contains(twain.ListQuarantined.Single().Reason,"after the capture session ended");
			Assert.AreEqual(1,Controller.Summary.CountQuarantined,"The summary counts it.");
			Assert.AreEqual(CaptureSessionEndReason.Closed,Controller.Summary.EndReason);
			twain.RaiseQuarantinedByLibrary(TestImages.Image8(4,4,(x,y) => 1));
			_harness.Dispatcher.RunPending();
			Assert.AreEqual(2,Controller.Summary.CountQuarantined,"Images the closing worker quarantined count too.");
		}

		[TestMethod]
		public void RedeliveredImage_NotPlacedWithoutConfirmation_DeclinedQuarantinedAsPatientUnknown() {
			_harness.Started();
			AcquiredImage image=TestImages.Image8(4,4,(x,y) => 1);
			image.IsRedelivery=true;
			_harness.Twain.RaiseImage(image);
			_harness.Dispatcher.RunPending();
			Assert.AreEqual(0,_harness.RadiographStore.ListDocumentsUpdated.Count);
			Assert.IsTrue(Controller.HasRedeliveredPending,"It waits for the operator.");
			Assert.AreEqual(0,_harness.Twain.ListQuarantined.Count);
			Assert.IsTrue(Controller.DeclineRedelivered());
			Assert.IsTrue(_harness.Twain.ListQuarantined.Single().IsPatientUnknown);
		}
		#endregion Targets and receive

		#region Ends of an enable
		[TestMethod]
		public void DeviceOffline_SensorNotConnected_RetryWithoutAnAutomaticLoop() {
			_harness.Started();
			_harness.Ended(TwainCaptureEndReason.DeviceOffline);
			Assert.IsTrue(Controller.IsDeviceOffline);
			Assert.AreEqual(CaptureIndicatorState.Connecting,Controller.IndicatorState,"Design 7.12: the state after is Connecting.");
			Assert.AreEqual("Sensor not connected.",Controller.IndicatorText);
			Assert.AreEqual(CaptureStartKind.Retry,Controller.StartKind);
			Assert.AreEqual("Retry",Controller.StartText);
			for(int i=0;i<50;i++) {
				_harness.Clock.Advance(1000);
				Controller.OnTick();
			}
			_harness.Dispatcher.RunPending();
			Assert.AreEqual(1,_harness.Twain.ListEnables.Count,"No automatic retry.");
			Assert.IsTrue(_harness.Start());
			Assert.AreEqual(2,_harness.Twain.ListEnables.Count);
			Assert.AreEqual(1,_harness.Twain.CountOpen,"The open source is enabled again, not reopened.");
			Assert.IsFalse(Controller.IsDeviceOffline);
		}

		[TestMethod]
		public void CloseRequested_PlainClose_SensorSoftwareClosed_ReEnableNeedsConfirmation() {
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage);
			_harness.Ended(TwainCaptureEndReason.CloseRequested,ended => ended.ConditionCode=0);
			Assert.AreEqual(CaptureIndicatorState.SensorSoftwareClosed,Controller.IndicatorState);
			Assert.AreEqual("Sensor software closed.",Controller.IndicatorText);
			Assert.AreEqual(CaptureStartKind.ReEnable,Controller.StartKind);
			Assert.AreEqual("Re-enable for Pat Test",Controller.StartText);
			Assert.IsFalse(Controller.AreSessionKeysActive);
			CollectionAssert.Contains(_harness.ListSounds,CaptureSound.Disabled);
			Assert.AreEqual(1,_harness.Twain.ListEnables.Count);
			Assert.IsFalse(Controller.CanStart);
			_harness.Arm();
			Assert.IsTrue(_harness.Start());
			Assert.AreEqual(101,_harness.LastRecord.MountItemTarget.MountItemNum);
		}

		[TestMethod]
		public void CloseRequested_WithACode_TheReasonAndDoNotReExpose() {
			_harness.Started();
			_harness.Ended(TwainCaptureEndReason.CloseRequested,ended => {
				ended.ConditionCode=0x8010;
				ended.ConditionText="No calibration";
			});
			Assert.AreEqual("Sensor software closed without an image: No calibration. Do not re-expose until you have checked the sensor software.",
				Controller.IndicatorText);
			CollectionAssert.Contains(_harness.ListSounds,CaptureSound.NotReceived);
		}

		[TestMethod]
		public void TransferFailed_ImageNotReceived_SlotDoesNotAdvance() {
			_harness.Started();
			_harness.Ended(TwainCaptureEndReason.TransferFailed,ended => ended.TransferFailureCount=1);
			Assert.AreEqual(CaptureIndicatorState.ImageNotReceived,Controller.IndicatorState);
			Assert.AreEqual(CaptureEndText.ImageNotReceived,Controller.IndicatorText);
			Assert.AreEqual(1,Controller.MountSlots.Target.ItemOrder);
			Assert.AreEqual(CaptureSessionPhase.Ready,Controller.Phase);
			CollectionAssert.Contains(_harness.ListSounds,CaptureSound.NotReceived);
		}

		[TestMethod]
		public void EnableFailed_ConditionText_Retry() {
			_harness.Started();
			_harness.Ended(TwainCaptureEndReason.EnableFailed,ended => ended.ErrorText="The sensor software could not be started (condition code 0x0001).");
			Assert.AreEqual(CaptureIndicatorState.Error,Controller.IndicatorState);
			Assert.AreEqual("The sensor software could not be started (condition code 0x0001).",Controller.IndicatorText);
			Assert.AreEqual(CaptureStartKind.Retry,Controller.StartKind);
		}

		[TestMethod]
		public void CompletedButTheSourceMayHoldImages_NoAutomaticEnable() {
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage);
			_harness.Image();
			_harness.Ended(TwainCaptureEndReason.Completed,ended => ended.SourceMayHoldImages=true);
			Assert.AreEqual(1,_harness.Twain.ListEnables.Count);
			Assert.AreEqual(CaptureEndText.MayStillHoldImages,Controller.IndicatorText);
		}

		[TestMethod]
		public void Disable_WithoutSafeDisable_WarnsFirst_SecondPressDisables_ReEnableNeedsConfirmation() {
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage,isSafeDisableInEffect:false);
			Assert.IsTrue(_harness.Execute(CaptureSessionAction.Disable));
			Assert.AreEqual(0,_harness.Twain.ListDisables.Count,"The first press only warns.");
			StringAssert.StartsWith(Controller.WarningText,"If an exposure was just taken, wait until it appears before cancelling.");
			_harness.Clock.Advance(3000);
			Assert.IsTrue(_harness.Execute(CaptureSessionAction.Disable));
			CollectionAssert.AreEqual(new[] { TwainCaptureStopReason.Cancel },_harness.Twain.ListDisables);
			Assert.AreEqual("",Controller.WarningText);
			_harness.Ended(TwainCaptureEndReason.Stopped,ended => ended.StopReason=TwainCaptureStopReason.Cancel);
			Assert.AreEqual(CaptureIndicatorState.Disabled,Controller.IndicatorState);
			Assert.AreEqual(CaptureStartKind.ReEnable,Controller.StartKind);
			CollectionAssert.Contains(_harness.ListSounds,CaptureSound.Disabled);
		}

		[TestMethod]
		public void Disable_WarningExpires() {
			_harness.Started();
			_harness.Execute(CaptureSessionAction.Disable);
			_harness.Clock.Advance(CaptureSessionController.StopConfirmMs+1);
			_harness.Execute(CaptureSessionAction.Disable);
			Assert.AreEqual(0,_harness.Twain.ListDisables.Count,"A press long after the warning warns again.");
		}

		[TestMethod]
		public void End_WithSafeDisable_DisablesAtOnce_DrainedImagePlaced_ThenFinished() {
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage,isSafeDisableInEffect:true);
			Assert.IsTrue(_harness.Execute(CaptureSessionAction.End));
			CollectionAssert.AreEqual(new[] { TwainCaptureStopReason.End },_harness.Twain.ListDisables);
			Assert.AreEqual(CaptureSessionPhase.Stopping,Controller.Phase);
			_harness.Image(AcquiredImageKind.Drained,TwainCaptureStopReason.End);
			Assert.AreEqual(1,_harness.RadiographStore.ListDocumentsUpdated.Count,"The image it still delivered is placed.");
			_harness.Ended(TwainCaptureEndReason.Stopped,ended => ended.StopReason=TwainCaptureStopReason.End);
			Assert.AreEqual(CaptureSessionPhase.Finished,Controller.Phase);
			Assert.AreEqual(1,_harness.Twain.CountBeginClose);
			Assert.AreEqual(CaptureSessionEndReason.Ended,Controller.Summary.EndReason);
			Assert.AreEqual(1,Controller.Summary.CountImages);
			CollectionAssert.AreEqual(new List<long> { 1000 },Controller.Summary.ListDocNums);
		}

		[TestMethod]
		public void End_BeforeStart_FinishesWithoutOpening() {
			_harness.BeginArmed();
			Assert.IsTrue(_harness.Execute(CaptureSessionAction.End));
			Assert.AreEqual(CaptureSessionEndReason.CancelledBeforeStart,Controller.Summary.EndReason);
			Assert.AreEqual(0,_harness.ListSessions.Count);
			Assert.IsFalse(Controller.CanExecute(CaptureSessionAction.End));
		}

		[TestMethod]
		public void RetakeAndRotate_NotAvailableBeforeAnImageIsSaved() {
			_harness.Started();
			Assert.IsFalse(Controller.CanExecute(CaptureSessionAction.Retake));
			Assert.IsFalse(Controller.CanExecute(CaptureSessionAction.Rotate));
			Assert.IsFalse(_harness.Execute(CaptureSessionAction.Retake));
			Assert.IsTrue(Controller.CanExecute(CaptureSessionAction.Disable));
		}
		#endregion Ends of an enable

		#region Bit depth
		[TestMethod]
		public void BitDepth_16WhenEveryConditionHolds_8ForASlotWithoutTeeth_ReasonLoggedOnce() {
			_harness.Setup.Options.Bits=16;
			_harness.Setup.Options.Allow16Bit=true;
			_harness.Setup.GatedSettingsHash=new string('a',64);
			_harness.Setup.SettingsCheck=CaptureSettingsTestKit.VerifiedHash(new string('a',64));
			_harness.Setup.VerificationRecord=CaptureControllerHarness.PassedRecord(new string('a',64));
			_harness.Setup.MountSlots.ListMountItems.First(x => x.ItemOrder==2).ToothNumbers="";
			_harness.Started();
			Assert.AreEqual(16,_harness.Twain.LastRequest.Bits,"Slot 1 has teeth of one view.");
			Assert.AreEqual(16,_harness.LastRecord.BitDepth.Bits);
			_harness.Expose();
			Assert.AreEqual(8,_harness.Twain.LastRequest.Bits,"Slot 2 has no teeth: it cannot be stored as DICOM.");
			StringAssert.Contains(_harness.LastRecord.BitDepth.Reason8Bit,"cannot be stored as DICOM");
			int countLines=_harness.ListLogFile.Count(x => x.Contains("cannot be stored as DICOM"));
			Assert.AreEqual(1,countLines,"Logged once.");
		}

		[TestMethod]
		public void BitDepth_SourceGave8Where16WereAsked_SilentAndLoggedOnce() {
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage);
			_harness.Image();
			_harness.Ended(TwainCaptureEndReason.Completed,ended => ended.Reason8Bit="ICAP_BITDEPTH 16 is not listed.");
			Assert.AreEqual(2,_harness.Twain.ListEnables.Count,"The series goes on.");
			Assert.IsFalse(Controller.IndicatorText.Contains("ICAP"),"Silent: the indicator does not show it (design 7.12).");
			Assert.AreEqual("",Controller.NoticeText);
			_harness.Status(TwainCaptureState.WaitingForImage);
			_harness.Image();
			_harness.Ended(TwainCaptureEndReason.Completed,ended => ended.Reason8Bit="ICAP_BITDEPTH 16 is not listed.");
			Assert.AreEqual(1,_harness.ListLogFile.Count(x => x.Contains("ICAP_BITDEPTH 16 is not listed.")),"Logged once.");
		}

		[TestMethod]
		public void BitDepth_SettingsCheckedForAnotherFile_8_ReasonLogged() {
			//Settings that are not verified never start (SettingsNotVerified_TheWindowSaysWhy_NothingStarts); a check made for another file than
			//the one in effect starts, but gives no 16 bits.
			_harness.Setup.Options.Bits=16;
			_harness.Setup.Options.Allow16Bit=true;
			_harness.Setup.GatedSettingsHash=new string('a',64);
			_harness.Setup.VerificationRecord=CaptureControllerHarness.PassedRecord(new string('a',64));
			_harness.Setup.SettingsCheck=CaptureSettingsTestKit.VerifiedHash(new string('e',64));
			_harness.Started();
			Assert.AreEqual(8,_harness.Twain.LastRequest.Bits);
			Assert.IsTrue(_harness.ListLogFile.Any(x => x.Contains("not the ones in effect")),string.Join("\r\n",_harness.ListLogFile));
		}

		[TestMethod]
		public void SettingsNotVerified_TheWindowSaysWhy_NothingStarts() {
			_harness.Setup.SettingsCheck=CaptureSettingsCheck.CheckHash(new string('a',64),12,"OPERATORY1",
				new List<SecurityLog> { CaptureSettingsTestKit.SavedLog(new string('e',64),12,"OPERATORY1") });
			Assert.IsFalse(Controller.IsSettingsVerified);
			_harness.BeginArmed();
			Assert.AreEqual(CaptureIndicatorState.Error,Controller.IndicatorState);
			StringAssert.StartsWith(Controller.IndicatorText,"Capture settings were changed outside Open Dental; review them in Setup.");
			Assert.AreEqual(Controller.SettingsNoticeText,Controller.IndicatorText);
			Assert.AreEqual(CaptureStartKind.None,Controller.StartKind);
			Assert.IsFalse(Controller.CanStart);
			Assert.IsFalse(_harness.Start());
			Assert.AreEqual(0,_harness.ListSessions.Count,"Nothing opens.");
			Assert.IsTrue(_harness.ListLogFile.Any(x => x.Contains("Not started: Capture settings were changed outside Open Dental")));
			CaptureSessionViewModel viewModel=new CaptureSessionViewModel(Controller);
			Assert.IsFalse(viewModel.IsStartVisible);
			StringAssert.StartsWith(viewModel.IndicatorText,"Capture settings were changed outside Open Dental");
			_harness.Setup.SettingsCheck=null;
			StringAssert.Contains(Controller.SettingsNoticeText,"not checked");
		}

		[TestMethod]
		public void BitDepth_DefaultSettings_8() {
			_harness.Started();
			Assert.AreEqual(8,_harness.Twain.LastRequest.Bits);
			Assert.IsTrue(_harness.ListLogFile.Any(x => x.Contains("Bits is not 16")),string.Join("\r\n",_harness.ListLogFile));
		}
		#endregion Bit depth

		#region Texts
		[TestMethod]
		public void Logged_SessionLinesGoToTheWindowOnly() {
			List<string> listLines=new List<string>();
			Controller.Logged+=line => listLines.Add(line);
			_harness.Started();
			_harness.Twain.RaiseLogged("Enable 1: 8 bits.");
			_harness.Dispatcher.RunPending();
			Assert.IsTrue(listLines.Contains("TWAIN: Enable 1: 8 bits."));
			Assert.IsFalse(_harness.ListLogFile.Any(x => x.Contains("TWAIN: ")),"The worker writes its own lines to the file.");
			Assert.IsTrue(_harness.ListLogFile.All(x => x.StartsWith("Capture session: ")));
		}
		#endregion Texts
	}
}
