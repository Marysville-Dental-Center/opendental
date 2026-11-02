// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using ImagingCapture.Session;
using ImagingDeviceManager.Capture;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace ImagingCapture.Tests {
	///<summary>The capture session's security-log entries (design 7.13, 10.7) through ICaptureSessionStore: the ImageCreate session summary, the
	///"Capture event:" entries under the bound patient for every event that may stand for an exposure with no stored image, and every
	///quarantine move. Each entry's text is pinned here.</summary>
	[TestClass]
	public class CaptureSessionAuditTests {
		private const string SessionGuidText="11111111-2222-3333-4444-555555555555";
		private const string Tail=" Device: Intraoral sensor (ImagingDeviceNum 12). Target: Slot 1 (teeth 1,2,3). Session: "+SessionGuidText+".";
		private CaptureControllerHarness _harness;

		[TestInitialize]
		public void Init() {
			_harness=new CaptureControllerHarness();
		}

		private CaptureSessionController Controller => _harness.Controller;

		///<summary>The ImageCreate entries' "patNum|fKey|logText" that are capture events.</summary>
		private List<string> CaptureEvents => _harness.Store.GetLogs(EnumPermType.ImageCreate).Where(x => x.Contains("|Capture event: ")).ToList();

		private string QuarantinePath(int number) {
			return @"C:\Quarantine\"+new Guid(SessionGuidText).ToString("N")+@"\image-"+number+".bmp";
		}

		#region Session summary
		[TestMethod]
		public void SessionSummary_OneImageCreateEntry_DeviceImagesRetakesQuarantinedProvider() {
			_harness.Started();
			_harness.Expose();
			_harness.Execute(CaptureSessionAction.Retake);//slot 1 again after slot 2
			_harness.Image();
			_harness.Ended(TwainCaptureEndReason.Completed);
			_harness.Expose();
			_harness.Status(TwainCaptureState.WaitingForImage);
			_harness.Image();
			_harness.Image(AcquiredImageKind.Extra);
			_harness.Ended(TwainCaptureEndReason.Completed,ended => ended.ExtraImageCount=1);
			Assert.AreEqual(CaptureSessionEndReason.MountFull,Controller.Summary.EndReason);
			List<string> listSummaries=_harness.Store.GetLogs(EnumPermType.ImageCreate).Where(x => x.Contains("Capture session summary")).ToList();
			Assert.AreEqual("55|0|Capture session summary: device Intraoral sensor (ImagingDeviceNum 12); 4 images, 1 retake, 1 quarantined; provider "
				+"DOC4 (ProvNum 4); mount 7; ended: MountFull; in effect: PerSlot, 8-bit (fallback: no Test capture record for this sensor's driver version and image settings), safe disable not in effect, "
				+"polarity AsDelivered, headless off. Session: "+SessionGuidText+".",listSummaries.Single(),"Design 9.3: the effective values too.");
			string tailSlot3=" Device: Intraoral sensor (ImagingDeviceNum 12). Target: Slot 3 (teeth 6). Session: "+SessionGuidText+".";
			CollectionAssert.AreEqual(new[] {
				"55|0|Capture event: an image was moved to unassigned captures on this computer. Reason: The sensor software sent more images than "
					+"expected. File: "+QuarantinePath(1)+"."+tailSlot3,
				"55|0|Capture event: the sensor software sent 1 more image than expected; it is in unassigned captures."+tailSlot3,
			},CaptureEvents);
			Assert.AreEqual(1,_harness.Store.GetLogs(EnumPermType.ImageEdit).Count,"The Retake.");
		}

		[TestMethod]
		public void SessionSummary_EffectiveValues_SafeDisablePerEnable_SourceGave8Bits_SettingsNotVerified() {
			_harness.Setup.Options.SessionMode=TwainSessionMode.Continuous;
			_harness.Setup.Options.Polarity=TwainPolarity.Invert;
			_harness.Setup.Options.Headless=true;
			//Every gate holds, so 16 bits are asked; the source gives 8 (the library's fallback is on the image).
			_harness.Setup.GatedSettingsHash=new string('a',64);
			_harness.Setup.SettingsCheck=CaptureSettingsTestKit.VerifiedHash(new string('a',64));
			_harness.Setup.VerificationRecord=CaptureControllerHarness.PassedRecord(new string('a',64),16,
				new TwainCaptureOptions { ImagingDeviceNum=12,Bits=16,Polarity=TwainPolarity.Invert });
			_harness.Started();
			Assert.AreEqual(16,_harness.Twain.LastRequest.Bits);
			_harness.Status(TwainCaptureState.WaitingForImage,isSafeDisableInEffect:true);
			_harness.Image(actionConfigure:image => image.BitDepthReason="The source sent 8-bit images.");
			_harness.Ended(TwainCaptureEndReason.Completed,ended => { ended.BitsRequested=16; ended.ReasonFallback="The source sent 8-bit images."; });
			_harness.Execute(CaptureSessionAction.End);//the next slot's enable runs: the first press warns
			_harness.Execute(CaptureSessionAction.End);
			_harness.Ended(TwainCaptureEndReason.Stopped,ended => ended.StopReason=TwainCaptureStopReason.End);
			CaptureEffectiveSettings settings=Controller.Summary.EffectiveSettings;
			Assert.AreEqual(2,settings.CountEnables);
			Assert.AreEqual(1,settings.CountSafeDisable);
			Assert.AreEqual("Continuous, 8-bit (fallback: The source sent 8-bit images), safe disable in effect for 1 of 2 enables, polarity Invert, "
				+"headless on",settings.GetText(isWithReasons:false),"The depth actually used, with the source's reason.");
			StringAssert.Contains(_harness.Store.GetLogs(EnumPermType.ImageCreate).Single(x => x.Contains("Capture session summary")),
				"in effect: Continuous, 8-bit (fallback: The source sent 8-bit images)");
			settings.IsSettingsVerified=false;
			settings.SettingsNotVerifiedText="Capture settings were changed outside Open PMS - Dental.";
			StringAssert.StartsWith(settings.GetText(isWithReasons:true),
				"capture settings not verified, so the safe values (Capture settings were changed outside Open PMS - Dental), Continuous");
		}

		[TestMethod]
		public void SessionSummary_SingleTargetMode_AndNoneWhenNeverStarted() {
			CaptureControllerHarness harness=new CaptureControllerHarness(0);
			harness.Started();
			harness.Expose();
			harness.Execute(CaptureSessionAction.End);
			Assert.AreEqual("55|0|Capture session summary: device Intraoral sensor (ImagingDeviceNum 12); 1 image, 0 retakes, 0 quarantined; provider "
				+"DOC4 (ProvNum 4); single images, no mount; ended: Ended; in effect: PerSlot, 8-bit (fallback: no Test capture record for this sensor's driver version and image settings), safe disable not "
				+"in effect, polarity AsDelivered, headless off. Session: "+SessionGuidText+".",
				harness.Store.GetLogs(EnumPermType.ImageCreate).Single());
			_harness.BeginArmed();
			_harness.Execute(CaptureSessionAction.End);
			Assert.AreEqual(CaptureSessionEndReason.CancelledBeforeStart,Controller.Summary.EndReason);
			Assert.AreEqual(0,_harness.Store.ListLogTexts.Count,"No capture session ran.");
		}

		[TestMethod]
		public void SessionSummary_NoProvider() {
			_harness.Store.DefaultProvNum=0;
			_harness.Setup.ProvNumDefault=0;
			_harness.Started();
			_harness.Execute(CaptureSessionAction.End);
			_harness.Execute(CaptureSessionAction.End);
			_harness.Ended(TwainCaptureEndReason.Stopped,ended => ended.StopReason=TwainCaptureStopReason.End);
			StringAssert.Contains(_harness.Store.GetLogs(EnumPermType.ImageCreate).Last(),"0 images, 0 retakes, 0 quarantined; provider none; mount 7; ended: Ended; in effect: ");
		}
		#endregion Session summary

		#region Capture events
		[TestMethod]
		public void CloseRequestedWithoutAnImage_CaptureEvent_WithTheReasonAndCode() {
			_harness.Started();
			_harness.Ended(TwainCaptureEndReason.CloseRequested,ended => {
				ended.ConditionCode=0x8010;
				ended.ConditionText="No calibration.";
			});
			Assert.AreEqual("55|0|Capture event: the sensor software closed after an enable without delivering an image: No calibration, condition "
				+"code 0x8010."+Tail,CaptureEvents.Single());
			_harness.Arm();
			_harness.Start();
			_harness.Ended(TwainCaptureEndReason.CloseRequested,ended => ended.ConditionCode=0);
			Assert.AreEqual("55|0|Capture event: the sensor software closed after an enable without delivering an image (a plain close, condition "
				+"code 0x0000)."+Tail,CaptureEvents.Last());
			_harness.Arm();
			_harness.Start();
			_harness.Ended(TwainCaptureEndReason.CloseRequested);
			StringAssert.Contains(CaptureEvents.Last(),"without delivering an image (its status could not be read).");
		}

		[TestMethod]
		public void CloseRequestedAfterAnImage_NoCaptureEvent() {
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage);
			_harness.Image();
			_harness.Ended(TwainCaptureEndReason.CloseRequested,ended => ended.ConditionCode=0);
			Assert.AreEqual(0,CaptureEvents.Count);
		}

		[TestMethod]
		public void TransferFailure_CaptureEvent() {
			_harness.Started();
			_harness.Ended(TwainCaptureEndReason.TransferFailed,ended => {
				ended.TransferFailureCount=1;
				ended.ConditionCode=0x0004;
			});
			Assert.AreEqual("55|0|Capture event: an image transfer failed (condition code 0x0004); the sensor software may still hold the image."+Tail,
				CaptureEvents.Single());
			_harness.Arm();
			_harness.Start();
			_harness.Ended(TwainCaptureEndReason.Completed,ended => ended.TransferFailureCount=2);
			StringAssert.StartsWith(CaptureEvents.Last(),"55|0|Capture event: 2 image transfers failed; the sensor software may still hold the image.");
		}

		[TestMethod]
		public void DisableWithoutSafeDisable_CaptureEvent() {
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage,isSafeDisableInEffect:false);
			_harness.Execute(CaptureSessionAction.Disable);
			_harness.Execute(CaptureSessionAction.Disable);
			_harness.Ended(TwainCaptureEndReason.Stopped,ended => {
				ended.StopReason=TwainCaptureStopReason.Cancel;
				ended.WasDisableStartedUnsafe=true;
			});
			Assert.AreEqual("55|0|Capture event: the source was disabled while safe disable was not in effect (cancelled); an exposure in progress may "
				+"not have been delivered."+Tail,CaptureEvents.Single());
		}

		[TestMethod]
		public void SecondDisableAfterTheWaitBound_CaptureEvent() {
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage,isSafeDisableInEffect:true);
			_harness.Execute(CaptureSessionAction.Disable);
			_harness.Ended(TwainCaptureEndReason.Stopped,ended => {
				ended.StopReason=TwainCaptureStopReason.Cancel;
				ended.IsSafeDisableInEffect=true;
				ended.WasDisableRefused=true;
				ended.WasSecondDisableAfterWait=true;
			});
			Assert.AreEqual("55|0|Capture event: a refused disable got no notification within the wait bound, and the disable was sent again; the "
				+"sensor software may still hold images."+Tail,CaptureEvents.Single());
		}

		[TestMethod]
		public void ForcedClose_WithoutSafeDisable_OneCaptureEvent_TheLateEndedAddsNone_LateQuarantineLogged() {
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage,isSafeDisableInEffect:false);
			FakeTwainCaptureSession twain=_harness.Twain;
			Controller.OnClosing();
			Assert.AreEqual("55|0|Capture event: the capture window closed while the sensor software was enabled without safe disable in effect; the "
				+"source is stopped in the safe order, and an exposure in progress may not be delivered."+Tail,CaptureEvents.Single());
			//The worker's own end of that enable, and an image it quarantined while closing.
			twain.RaiseEnded(TwainCaptureEndReason.Stopped,ended => {
				ended.StopReason=TwainCaptureStopReason.Close;
				ended.WasDisableStartedUnsafe=true;
			});
			AcquiredImage image=TestImages.Image8(4,4,(x,y) => 1);
			image.TargetLabel="Slot 1 (teeth 1,2,3)";
			twain.RaiseQuarantinedByLibrary(image);
			_harness.Dispatcher.RunPending();
			Assert.AreEqual(2,CaptureEvents.Count,"No second entry for the same stop.");
			Assert.AreEqual("55|0|Capture event: an image was moved to unassigned captures on this computer. Reason: The image arrived after the capture "
				+@"session ended. File: C:\Quarantine\late.bmp."+Tail,CaptureEvents.Last());
			Assert.AreEqual(1,_harness.Store.GetLogs(EnumPermType.ImageCreate).Count(x => x.Contains("Capture session summary:")),"One summary...");
			string update=_harness.Store.GetLogs(EnumPermType.ImageCreate).Single(x => x.Contains("Capture session summary updated:"));
			StringAssert.Contains(update,"1 quarantined in all","...and the update for the image quarantined after it (design 10.7).");
		}

		[TestMethod]
		public void ForcedClose_WithSafeDisable_NoCaptureEvent() {
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage,isSafeDisableInEffect:true);
			Controller.OnClosing();
			Assert.AreEqual(0,CaptureEvents.Count);
		}

		[TestMethod]
		public void QuarantineMove_PatientChange_CaptureEvent() {
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage,isSafeDisableInEffect:false);
			_harness.ChangePatient(77);
			_harness.Image();
			Assert.AreEqual("55|0|Capture event: an image was moved to unassigned captures on this computer. Reason: The patient changed in the main "
				+"window (to PatNum 77) during the capture session for Pat Test (PatNum 55). File: "+QuarantinePath(1)+"."+Tail,CaptureEvents.Single());
		}

		[TestMethod]
		public void QuarantineMoveFailed_CaptureEvent_NamesTheJournalCopy() {
			_harness.RadiographStore.ThrowOn="Import";
			_harness.ActionOnNewSession=session => session.QuarantineError="the disk is full";
			_harness.Started();
			AcquiredImage image=TestImages.Image8(4,4,(x,y) => 1);
			image.JournalPath=@"C:\Journal\j1.bmp";
			_harness.Twain.RaiseImage(image);
			_harness.Dispatcher.RunPending();
			Assert.AreEqual(@"55|0|Capture event: an image could not be moved to unassigned captures. Reason: Could not save: Import failed. Its journal "
				+@"copy is C:\Journal\j1.bmp."+Tail,CaptureEvents.Single());
		}

		[TestMethod]
		public void RedeliveredImageDeclined_CaptureEvent() {
			_harness.Setup.Options.RedeliverCap=0x9001;
			_harness.Started();
			_harness.Ended(TwainCaptureEndReason.TransferFailed,ended => ended.TransferFailureCount=1);
			_harness.Arm();
			Controller.RequestRedelivery();
			AcquiredImage image=TestImages.Image8(4,4,(x,y) => 1,TestImages.Json8Bit);
			image.IsRedelivery=true;
			_harness.Twain.RaiseImage(image);
			_harness.Dispatcher.RunPending();
			Controller.DeclineRedelivered();
			Assert.AreEqual("55|0|Capture event: an image was moved to unassigned captures on this computer. Reason: Redelivered image declined by the "
				+"operator; patient unknown (acquired 2026-10-31T09:15:30.123+00:00, capture ID c-7f3a91). File: "+QuarantinePath(1)+"."+Tail,
				CaptureEvents.Last());
		}

		[TestMethod]
		public void AnEntryThatCannotBeWritten_NeverStopsTheSession() {
			_harness.Store.ThrowOn="MakeLogEntry";
			_harness.Started();
			_harness.Ended(TwainCaptureEndReason.CloseRequested,ended => ended.ConditionCode=0);
			Assert.AreEqual(CaptureSessionPhase.Ready,Controller.Phase);
			Assert.IsTrue(_harness.ListLogFile.Any(x => x.Contains("The security-log entry (ImageCreate) could not be written: MakeLogEntry failed")
				&& x.Contains("Capture event: ")),string.Join("\r\n",_harness.ListLogFile));
			_harness.Arm();
			Assert.IsTrue(_harness.Start());
			_harness.Expose();
			Assert.AreEqual(1,_harness.RadiographStore.ListDocumentsUpdated.Count);
		}
		#endregion Capture events
	}
}
