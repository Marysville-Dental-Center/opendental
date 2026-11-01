// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using ImagingCapture.Session;
using ImagingCapture.UI;
using ImagingDeviceManager.Capture;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace ImagingCapture.Tests {
	///<summary>Disable refused, transfer failures and redelivery (W13, W15; design 7.6, 7.12) in CaptureSessionController: the offer of "Request
	///undelivered images again", the redelivery enable, and the operator's confirmation of each redelivered image (placed) or its decline (the
	///quarantine, "patient unknown"). The capability number is made up: the device profile supplies the real one as data.</summary>
	[TestClass]
	public class CaptureSessionRedeliveryTests {
		private const ushort RedeliverCapMadeUp=0x9001;
		private CaptureControllerHarness _harness;

		[TestInitialize]
		public void Init() {
			_harness=new CaptureControllerHarness();
			_harness.Setup.Options.RedeliverCap=RedeliverCapMadeUp;
		}

		private CaptureSessionController Controller => _harness.Controller;

		///<summary>An image of the last enable with IsRedelivery and the 8-bit metadata (acquiredAt 2026-10-31T09:15:30.123+00:00, captureId
		///c-7f3a91).</summary>
		private AcquiredImage RaiseRedelivered(string metadataJson=TestImages.Json8Bit) {
			AcquiredImage image=TestImages.Image8(6,4,(x,y) => (byte)(x*40+y),metadataJson);
			image.IsRedelivery=true;
			_harness.Twain.RaiseImage(image);
			_harness.Dispatcher.RunPending();
			return image;
		}

		///<summary>Started, then the first enable ends with a failed transfer: the slot does not advance, and redelivery is offered.</summary>
		private void TransferFailed() {
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage);
			_harness.Ended(TwainCaptureEndReason.TransferFailed,ended => {
				ended.TransferFailureCount=1;
				ended.SourceMayHoldImages=true;
			});
		}

		[TestMethod]
		public void TransferFailed_SlotDoesNotAdvance_RedeliveryOffered_ConfirmedImagePlaced() {
			TransferFailed();
			Assert.AreEqual("Image not received. Do not re-expose yet.",Controller.IndicatorText);
			Assert.AreEqual(CaptureIndicatorState.ImageNotReceived,Controller.IndicatorState);
			Assert.AreEqual(1,Controller.MountSlots.Target.ItemOrder,"The slot does not advance.");
			Assert.IsTrue(Controller.IsRedeliveryOffered);
			Assert.AreEqual("Request undelivered images again",Controller.RedeliveryText);
			Assert.IsFalse(Controller.CanRequestRedelivery,"A deliberate start: 2 s after the banner.");
			Assert.IsFalse(Controller.RequestRedelivery());
			_harness.Arm();
			Assert.IsTrue(Controller.CanRequestRedelivery);
			Assert.IsTrue(Controller.RequestRedelivery());
			_harness.Dispatcher.RunPending();
			FakeTwainCaptureSession twain=_harness.Twain;
			Assert.AreEqual(twain.LastEnableId,twain.ListRedeliveryEnableIds.Single(),"RequestRedelivery, not Enable.");
			Assert.IsTrue(_harness.LastRecord.IsRedelivery);
			Assert.AreEqual(101,_harness.LastRecord.MountItemTarget.MountItemNum,"For the same target.");
			Assert.AreEqual(CaptureSessionPhase.Enabled,Controller.Phase);
			Assert.IsFalse(Controller.IsRedeliveryOffered);
			//The redelivered image waits, with its acquiredAt and captureId shown.
			_harness.Status(TwainCaptureState.WaitingForImage);
			RaiseRedelivered();
			Assert.AreEqual(0,_harness.RadiographStore.ListDocumentsUpdated.Count,"Never placed on arrival.");
			Assert.AreEqual(0,twain.ListQuarantined.Count);
			CaptureRedeliveredView view=Controller.RedeliveredView;
			Assert.AreEqual("2026-10-31T09:15:30.123+00:00",view.AcquiredAtText);
			Assert.AreEqual("c-7f3a91",view.CaptureId);
			Assert.AreEqual("1 of 3: Max R, #1,2,3",view.TargetText);
			Assert.AreEqual("Redelivered image, acquired 2026-10-31T09:15:30.123+00:00, capture ID c-7f3a91. The sensor software does not know the "
				+"patient. Place it in 1 of 3: Max R, #1,2,3 only if it is Pat Test's image.",view.Text);
			Assert.AreEqual(1,view.CountPending);
			_harness.Ended(TwainCaptureEndReason.Completed);
			Assert.AreEqual(2,twain.ListEnables.Count,"No automatic enable after a redelivery.");
			Assert.AreEqual(CaptureSessionPhase.Ready,Controller.Phase);
			Assert.AreEqual(CaptureSessionText.RedeliveredConfirm,Controller.IndicatorText);
			_harness.Arm();
			Assert.IsFalse(Controller.CanStart,"Nothing is enabled while a redelivered image waits.");
			//Confirm is on screen only, 2 s after the image is shown.
			Assert.IsTrue(Controller.CanConfirmRedelivered);
			Assert.IsTrue(Controller.ConfirmRedelivered());
			Document document=_harness.RadiographStore.ListDocumentsUpdated.Single();
			Assert.AreEqual(101,document.MountItemNum);
			Assert.AreEqual(55,document.PatNum,"The bound patient.");
			Assert.AreEqual(2,Controller.MountSlots.Target.ItemOrder,"Now the target advances.");
			Assert.IsFalse(Controller.HasRedeliveredPending);
			Assert.IsTrue(Controller.CanStart);
			Assert.IsTrue(_harness.Start());
			Assert.AreEqual(102,_harness.LastRecord.MountItemTarget.MountItemNum);
			Assert.IsFalse(_harness.LastRecord.IsRedelivery);
		}

		[TestMethod]
		public void Redelivered_ConfirmArmed2SecondsAfterItShows_EachImageInTurn() {
			TransferFailed();
			_harness.Arm();
			Controller.RequestRedelivery();
			RaiseRedelivered();
			RaiseRedelivered(TestImages.Json8BitRotated);
			Assert.IsFalse(Controller.CanConfirmRedelivered);
			Assert.IsFalse(Controller.ConfirmRedelivered());
			_harness.Clock.Advance(CaptureSessionController.KeyIgnoreMs);
			int version=Controller.Version;
			Controller.OnTick();
			Assert.AreNotEqual(version,Controller.Version,"The window hears that Confirm is armed.");
			Assert.IsTrue(Controller.ConfirmRedelivered());
			Assert.AreEqual("c-1",Controller.RedeliveredView.CaptureId,"The next one.");
			Assert.AreEqual("2 of 3: Max R, #4,5",Controller.RedeliveredView.TargetText);
			Assert.IsFalse(Controller.CanConfirmRedelivered,"2 s again for the next image.");
			_harness.Clock.Advance(CaptureSessionController.KeyIgnoreMs);
			Assert.IsTrue(Controller.ConfirmRedelivered());
			CollectionAssert.AreEqual(new long[] { 101,102 },_harness.RadiographStore.ListDocumentsUpdated.Select(x => x.MountItemNum).ToList());
			//Keys never confirm: they are session keys.
			Assert.IsFalse(Controller.HasRedeliveredPending);
		}

		[TestMethod]
		public void Redelivered_Declined_QuarantinedAsPatientUnknown_WithAcquiredAtAndCaptureId() {
			TransferFailed();
			_harness.Arm();
			Controller.RequestRedelivery();
			RaiseRedelivered();
			Assert.IsTrue(Controller.CanDeclineRedelivered);
			Assert.IsTrue(Controller.DeclineRedelivered());
			FakeQuarantineCall call=_harness.Twain.ListQuarantined.Single();
			Assert.IsTrue(call.IsPatientUnknown);
			Assert.AreEqual("Redelivered image declined by the operator; patient unknown (acquired 2026-10-31T09:15:30.123+00:00, capture ID c-7f3a91).",
				call.Reason);
			Assert.AreEqual(0,_harness.RadiographStore.ListDocumentsUpdated.Count);
			Assert.AreEqual(1,Controller.CountQuarantined);
			Assert.AreEqual(1,Controller.MountSlots.Target.ItemOrder,"The slot is still to take.");
		}

		[TestMethod]
		public void Redelivered_ConfirmedThenTheSaveFails_QuarantinedForTheBoundPatient_MarkedConfirmed() {
			TransferFailed();
			_harness.Arm();
			Controller.RequestRedelivery();
			AcquiredImage image=RaiseRedelivered();
			Assert.IsFalse((image.SidecarFields??new List<KeyValuePair<string,string>>()).Any(x => x.Key==CaptureSidecarKeys.RedeliveryConfirmed),
				"Not confirmed yet.");
			_harness.Ended(TwainCaptureEndReason.Completed);
			_harness.Clock.Advance(CaptureSessionController.KeyIgnoreMs);
			_harness.RadiographStore.ThrowOn="Import";
			Assert.IsTrue(Controller.ConfirmRedelivered());
			FakeQuarantineCall call=_harness.Twain.ListQuarantined.Single();
			Assert.IsFalse(call.IsPatientUnknown,"The operator confirmed it for the bound patient.");
			Assert.AreEqual("1",call.Image.SidecarFields.Single(x => x.Key==CaptureSidecarKeys.RedeliveryConfirmed).Value,
				"Its sidecar says so, so Assign may file it (CaptureRecoveryFiler.IsAssignOffered).");
			Assert.AreEqual(0,_harness.Twain.ListJournalConfirmed.Count,"Its journal copy is rewritten by the quarantine move, not as a saved image.");
		}

		[TestMethod]
		public void Redelivered_ConfirmedAndSaved_ItsJournalCopyNowNamesTheBoundPatient_OnlyAfterASuccessfulSave() {
			TransferFailed();
			_harness.Arm();
			Controller.RequestRedelivery();
			AcquiredImage image=RaiseRedelivered();
			AcquiredImage imageSecond=RaiseRedelivered(TestImages.Json8BitRotated);
			AcquiredImage imageThird=RaiseRedelivered();
			_harness.Ended(TwainCaptureEndReason.Completed);
			FakeTwainCaptureSession twain=_harness.Twain;
			Assert.AreEqual(0,twain.ListJournalConfirmed.Count,"Not on arrival: the source knows no patient.");
			_harness.Clock.Advance(CaptureSessionController.KeyIgnoreMs);
			Assert.IsTrue(Controller.ConfirmRedelivered());
			Assert.AreEqual(1,_harness.RadiographStore.ListDocumentsUpdated.Count);
			Assert.AreSame(image,twain.ListJournalConfirmed.Single(),"After its save.");
			List<KeyValuePair<string,string>> listFields=twain.ListJournalConfirmedFields.Single();
			Assert.AreEqual("1",listFields.Single(x => x.Key==CaptureSidecarKeys.RedeliveryConfirmed).Value);
			Assert.AreEqual("101",listFields.Single(x => x.Key==CaptureSidecarKeys.MountItemNum).Value,"With the slot it was saved into.");
			//The second one's save fails: it goes to the quarantine for the bound patient instead (QuarantineImage rewrites its copy then).
			_harness.Clock.Advance(CaptureSessionController.KeyIgnoreMs);
			_harness.RadiographStore.ThrowOn="Import";
			Assert.IsTrue(Controller.ConfirmRedelivered());
			Assert.AreSame(imageSecond,twain.ListQuarantined.Single().Image);
			Assert.AreEqual(1,twain.ListJournalConfirmed.Count,"Only after a successful save.");
			//The third one is declined: "patient unknown" in the quarantine, its copy not confirmed.
			Assert.IsTrue(Controller.DeclineRedelivered());
			Assert.AreSame(imageThird,twain.ListQuarantined.Last().Image);
			Assert.AreEqual(1,twain.ListJournalConfirmed.Count);
		}

		[TestMethod]
		public void Redelivered_ConfirmedAndSaved_TheJournalCopyCannotBeRewritten_Logged_TheDocumentStays() {
			TransferFailed();
			_harness.Arm();
			Controller.RequestRedelivery();
			RaiseRedelivered();
			_harness.Ended(TwainCaptureEndReason.Completed);
			_harness.Twain.JournalConfirmError="The disk is full.";
			_harness.Clock.Advance(CaptureSessionController.KeyIgnoreMs);
			Assert.IsTrue(Controller.ConfirmRedelivered());
			Assert.AreEqual(1,_harness.RadiographStore.ListDocumentsUpdated.Count);
			Assert.AreEqual(0,_harness.Twain.ListQuarantined.Count);
			Assert.IsTrue(Controller.ListLog.Any(x => x.Contains("could not be marked with patient 55") && x.Contains("The disk is full.")),
				string.Join("\r\n",Controller.ListLog));
		}

		[TestMethod]
		public void Redelivered_WithoutMetadata_ShownAsUnknown() {
			TransferFailed();
			_harness.Arm();
			Controller.RequestRedelivery();
			RaiseRedelivered(null);
			Assert.AreEqual("unknown",Controller.RedeliveredView.AcquiredAtText);
			Assert.AreEqual("unknown",Controller.RedeliveredView.CaptureId);
		}

		[TestMethod]
		public void Redelivered_ConfirmRefused_WhileTheMainWindowShowsAnotherPatient() {
			TransferFailed();
			_harness.Arm();
			Controller.RequestRedelivery();
			RaiseRedelivered();
			_harness.Clock.Advance(CaptureSessionController.KeyIgnoreMs);
			_harness.PatNumCur=77;//no ODEvent: the check itself refuses
			Assert.IsFalse(Controller.ConfirmRedelivered());
			Assert.IsTrue(Controller.HasRedeliveredPending);
			Assert.AreEqual(0,_harness.RadiographStore.ListDocumentsUpdated.Count);
			Assert.AreEqual("Patient changed in the main window. This session is still for Pat Test.",Controller.PatientChangedText);
		}

		[TestMethod]
		public void Redelivered_PatientChange_WaitingImagesGoToTheQuarantineAsPatientUnknown() {
			TransferFailed();
			_harness.Arm();
			Controller.RequestRedelivery();
			RaiseRedelivered();
			_harness.ChangePatient(77);
			Assert.IsFalse(Controller.HasRedeliveredPending);
			FakeQuarantineCall call=_harness.Twain.ListQuarantined.Single();
			Assert.IsTrue(call.IsPatientUnknown);
			StringAssert.StartsWith(call.Reason,"Redelivered image not placed: the patient changed in the main window; patient unknown");
			RaiseRedelivered();
			Assert.IsTrue(_harness.Twain.ListQuarantined.Last().IsPatientUnknown,"A later one too, at once.");
			Assert.AreEqual(2,_harness.Twain.ListQuarantined.Count);
		}

		[TestMethod]
		public void Redelivered_SessionEnds_WaitingImagesGoToTheQuarantine() {
			TransferFailed();
			_harness.Arm();
			Controller.RequestRedelivery();
			RaiseRedelivered();
			_harness.Ended(TwainCaptureEndReason.Completed);
			Assert.IsTrue(_harness.Execute(CaptureSessionAction.End));
			Assert.AreEqual(CaptureSessionPhase.Finished,Controller.Phase);
			Assert.IsTrue(_harness.Twain.ListQuarantined.Single().IsPatientUnknown);
			Assert.AreEqual(1,Controller.Summary.CountQuarantined);
		}

		[TestMethod]
		public void Redelivered_HandledOnlyAfterTheSessionFinished_QuarantinedAsPatientUnknown() {
			//Design 7.12: a redelivered image that the operator did not confirm has no patient. Its ImageAcquired is still queued when a logoff
			//closes the window.
			TransferFailed();
			_harness.Arm();
			Assert.IsTrue(Controller.RequestRedelivery());
			AcquiredImage image=TestImages.Image8(6,4,(x,y) => (byte)(x*40+y),TestImages.Json8Bit);
			image.IsRedelivery=true;
			_harness.Twain.RaiseImage(image);
			Controller.OnClosing();
			_harness.Dispatcher.RunPending();
			FakeQuarantineCall call=_harness.Twain.ListQuarantined.Single();
			Assert.IsTrue(call.IsPatientUnknown,"Never the bound patient for an image nobody confirmed.");
			StringAssert.Contains(call.Reason,"after the capture session ended");
			Assert.AreEqual(0,_harness.RadiographStore.ListDocumentsUpdated.Count);
		}

		[TestMethod]
		public void Redelivered_OnlyTheEnableSaysSo_HandledAfterTheSessionFinished_QuarantinedAsPatientUnknown() {
			TransferFailed();
			_harness.Arm();
			Assert.IsTrue(Controller.RequestRedelivery());
			Assert.IsTrue(_harness.LastRecord.IsRedelivery);
			AcquiredImage image=TestImages.Image8(6,4,(x,y) => (byte)(x*40+y),TestImages.Json8Bit);//the image itself is not marked
			_harness.Twain.RaiseImage(image);
			Controller.OnClosing();
			_harness.Dispatcher.RunPending();
			Assert.IsTrue(_harness.Twain.ListQuarantined.Single().IsPatientUnknown,"The redelivery enable's image has no patient either.");
			Assert.AreEqual(0,_harness.RadiographStore.ListDocumentsUpdated.Count);
		}

		[TestMethod]
		public void SourceClosedByTheLibrary_MayHoldImages_Retry_OpensWithoutAnEnable_TheRedeliveryStaysOffered() {
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage);
			//WM_QUIT reached the worker: the library closed the source on its own, and the source may still hold images.
			_harness.Ended(TwainCaptureEndReason.Stopped,ended => {
				ended.StopReason=TwainCaptureStopReason.Close;
				ended.SourceMayHoldImages=true;
			});
			Assert.AreEqual(CaptureStartKind.Retry,Controller.StartKind);
			Assert.IsFalse(Controller.IsRedeliveryOffered,"Not while the source is closed.");
			_harness.Arm();
			Assert.IsTrue(_harness.Start(),"Retry opens the source again.");
			Assert.AreEqual(2,_harness.ListSessions.Count);
			Assert.AreEqual(0,_harness.Twain.ListEnables.Count,"No enable: a fresh source session would lose the images it may still hold.");
			Assert.AreEqual(CaptureSessionPhase.Ready,Controller.Phase);
			Assert.AreEqual(CaptureStartKind.ReEnable,Controller.StartKind);
			StringAssert.Contains(Controller.IndicatorText,CaptureEndText.MayStillHoldImages);
			Assert.IsTrue(Controller.IsRedeliveryOffered,"\"Request undelivered images again\" is offered now that the source is open.");
			_harness.Arm();
			Assert.IsTrue(Controller.RequestRedelivery());
			Assert.IsTrue(_harness.LastRecord.IsRedelivery);
		}

		[TestMethod]
		public void NoRedeliverCap_NoOffer() {
			_harness.Setup.Options.RedeliverCap=null;
			TransferFailed();
			_harness.Arm();
			Assert.IsFalse(Controller.IsRedeliveryOffered);
			Assert.IsFalse(Controller.RequestRedelivery());
			Assert.AreEqual(0,_harness.Twain.ListRedeliveryEnableIds.Count);
			Assert.AreEqual(CaptureEndText.ImageNotReceived,Controller.IndicatorText);
		}

		[TestMethod]
		public void DisableRefused_WaitingForTheSensorSoftware_ThenAStopThatMayHoldImages_OffersRedelivery() {
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage,isSafeDisableInEffect:true);
			_harness.Execute(CaptureSessionAction.Disable);
			_harness.Status(TwainCaptureState.WaitingForSource,isSafeDisableInEffect:true);
			Assert.AreEqual("Waiting for the sensor software to finish",Controller.IndicatorText);
			Assert.AreEqual(CaptureIndicatorState.Receiving,Controller.IndicatorState);
			_harness.Ended(TwainCaptureEndReason.Stopped,ended => {
				ended.StopReason=TwainCaptureStopReason.Cancel;
				ended.IsSafeDisableInEffect=true;
				ended.WasDisableRefused=true;
				ended.WasSecondDisableAfterWait=true;
				ended.SourceMayHoldImages=true;
			});
			StringAssert.Contains(Controller.IndicatorText,CaptureEndText.MayStillHoldImages);
			Assert.AreEqual(Controller.IndicatorText.IndexOf(CaptureEndText.MayStillHoldImages),Controller.IndicatorText.LastIndexOf(CaptureEndText.MayStillHoldImages),
				"Said once.");
			Assert.IsTrue(Controller.IsRedeliveryOffered);
		}

		[TestMethod]
		public void CompletedWithImagesMaybeHeld_Offers_ANormalReEnableClearsTheOffer() {
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage);
			_harness.Image();
			_harness.Ended(TwainCaptureEndReason.Completed,ended => ended.SourceMayHoldImages=true);
			Assert.AreEqual(1,_harness.Twain.ListEnables.Count,"No automatic enable.");
			Assert.IsTrue(Controller.IsRedeliveryOffered);
			_harness.Arm();
			Assert.IsTrue(_harness.Start());
			Assert.IsFalse(_harness.LastRecord.IsRedelivery);
			_harness.Ended(TwainCaptureEndReason.CloseRequested,ended => ended.ConditionCode=0);
			Assert.AreEqual(CaptureSessionPhase.Ready,Controller.Phase);
			Assert.IsFalse(Controller.IsRedeliveryOffered,"The source offers only its most recent session's images.");
		}

		[TestMethod]
		public void RedeliveryUnavailable_ShownWithReEnable_OfferWithdrawn() {
			TransferFailed();
			_harness.Arm();
			Controller.RequestRedelivery();
			_harness.Ended(TwainCaptureEndReason.RedeliveryUnavailable,ended => ended.IsRedelivery=true);
			Assert.AreEqual(CaptureIndicatorState.Error,Controller.IndicatorState);
			Assert.AreEqual("The sensor software cannot deliver images again.",Controller.IndicatorText);
			Assert.AreEqual(CaptureStartKind.ReEnable,Controller.StartKind);
			Assert.IsFalse(Controller.IsRedeliveryOffered);
			Assert.AreEqual(2,_harness.Twain.ListEnables.Count);
		}

		[TestMethod]
		public void Redelivery_NothingToRedeliver_CloseRequestedWithACode_ShownLikeAnyReason() {
			TransferFailed();
			_harness.Arm();
			Controller.RequestRedelivery();
			_harness.Ended(TwainCaptureEndReason.CloseRequested,ended => {
				ended.IsRedelivery=true;
				ended.ConditionCode=0x8050;
				ended.ConditionText="Nothing to deliver";
			});
			StringAssert.StartsWith(Controller.IndicatorText,"Sensor software closed without an image: Nothing to deliver.");
			Assert.IsFalse(Controller.IsRedeliveryOffered);
			Assert.AreEqual(CaptureStartKind.ReEnable,Controller.StartKind);
		}

		[TestMethod]
		public void Redelivery_SingleTargetMode_ConfirmedImageIsADocumentWithTheBannersTeeth() {
			_harness=new CaptureControllerHarness(0);
			_harness.Setup.Options.RedeliverCap=RedeliverCapMadeUp;
			string error;
			Controller.Begin();
			Controller.SetToothNumbers("8",out error);
			_harness.Arm();
			_harness.Start();
			_harness.Ended(TwainCaptureEndReason.TransferFailed,ended => ended.TransferFailureCount=1);
			_harness.Arm();
			Assert.IsTrue(Controller.RequestRedelivery());
			RaiseRedelivered();
			Assert.AreEqual("Single image, Max R anterior, #8",Controller.RedeliveredView.TargetText);
			_harness.Ended(TwainCaptureEndReason.Completed);
			_harness.Clock.Advance(CaptureSessionController.KeyIgnoreMs);
			Assert.IsTrue(Controller.ConfirmRedelivered());
			Document document=_harness.RadiographStore.ListDocumentsUpdated.Single();
			Assert.AreEqual("8",document.ToothNumbers);
			Assert.AreEqual(0,document.MountItemNum);
		}

		[TestMethod]
		public void Redelivery_SingleTargetMode_TeethChangedWhileTheImageWaits_SavedWithTheTeethThePanelShows() {
			_harness=new CaptureControllerHarness(0);
			_harness.Setup.Options.RedeliverCap=RedeliverCapMadeUp;
			string error;
			Controller.Begin();
			Controller.SetToothNumbers("8,9",out error);
			_harness.Arm();
			_harness.Start();
			_harness.Ended(TwainCaptureEndReason.TransferFailed,ended => ended.TransferFailureCount=1);
			_harness.Arm();
			Assert.IsTrue(Controller.RequestRedelivery());
			RaiseRedelivered();
			_harness.Ended(TwainCaptureEndReason.Completed);
			Assert.IsTrue(Controller.CanEditToothNumbers,"Between images.");
			Assert.IsTrue(Controller.SetToothNumbers("24,25",out error),error);
			Assert.AreEqual("Single image, Mand anterior, #24,25",Controller.RedeliveredView.TargetText);
			_harness.Clock.Advance(CaptureSessionController.KeyIgnoreMs);
			Assert.IsTrue(Controller.ConfirmRedelivered());
			Document document=_harness.RadiographStore.ListDocumentsUpdated.Single();
			Assert.AreEqual("24,25",document.ToothNumbers,"Design 7.12: what the operator confirmed.");
		}
	}
}
