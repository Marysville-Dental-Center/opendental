// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using CodeBase;
using ImagingCapture.Session;
using ImagingDeviceManager.Capture;
using ImagingDeviceManager.Config;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace ImagingCapture.Tests {
	///<summary>The patient-change monitor, idle disable, the cancel warning and the forced close of CaptureSessionController (design 7.3 step 7,
	///7.6, 7.12, 10.6), on the fakes of CaptureSessionTestKit. The rule they share: no disable of the controller's own without safe disable in
	///effect, except the forced close.</summary>
	[TestClass]
	public class CaptureSessionMonitorTests {
		private CaptureControllerHarness _harness;

		[TestInitialize]
		public void Init() {
			_harness=new CaptureControllerHarness();
		}

		private CaptureSessionController Controller => _harness.Controller;

		#region Patient change
		[TestMethod]
		public void PatientChange_MidEnable_WithSafeDisable_StopsPlacing_DisablesAtOnce_ReEnableNeedsThePatientBack() {
			_harness.Started();
			Assert.AreEqual(1,_harness.PatientEvents.CountHandlers,"The monitor runs from Begin.");
			_harness.Status(TwainCaptureState.WaitingForImage,isSafeDisableInEffect:true);
			_harness.ChangePatient(77);
			Assert.IsTrue(Controller.IsPlacementStopped);
			Assert.AreEqual("Patient changed in the main window. This session is still for Pat Test.",Controller.PatientChangedText);
			CollectionAssert.AreEqual(new[] { TwainCaptureStopReason.PatientChanged },_harness.Twain.ListDisables,"Disabled at once.");
			Assert.AreEqual(CaptureSessionPhase.Stopping,Controller.Phase);
			Assert.AreEqual(CaptureSessionText.PatientChangedDisabling,Controller.NoticeText);
			CollectionAssert.Contains(_harness.ListSounds,CaptureSound.Error);
			//What the source still delivers goes to the quarantine, tagged with the bound patient.
			_harness.Image(AcquiredImageKind.Drained,TwainCaptureStopReason.PatientChanged);
			Assert.AreEqual(0,_harness.RadiographStore.ListDocumentsUpdated.Count);
			FakeQuarantineCall call=_harness.Twain.ListQuarantined.Single();
			Assert.AreEqual("The patient changed in the main window (to PatNum 77) during the capture session for Pat Test (PatNum 55).",call.Reason);
			Assert.IsFalse(call.IsPatientUnknown);
			Assert.AreEqual(55,call.Context.PatNum);
			_harness.Ended(TwainCaptureEndReason.Stopped,ended => {
				ended.StopReason=TwainCaptureStopReason.PatientChanged;
				ended.IsSafeDisableInEffect=true;
			});
			Assert.AreEqual(CaptureSessionPhase.Ready,Controller.Phase);
			Assert.AreEqual(CaptureStartKind.ReEnable,Controller.StartKind);
			Assert.AreEqual("Re-enable for Pat Test",Controller.StartText);
			Assert.AreEqual(CaptureIndicatorState.Disabled,Controller.IndicatorState);
			Assert.AreNotEqual("",Controller.PatientChangedText,"The red bar stays.");
			//Re-enable is refused while the main window shows another patient (getPatNumCur()).
			_harness.Arm();
			Assert.IsFalse(_harness.Start());
			Assert.AreEqual(1,_harness.Twain.ListEnables.Count);
			Assert.IsTrue(Controller.IsPlacementStopped);
			//The patient is selected again (a Tag for the bound patient is no change); the deliberate re-enable places images again.
			_harness.ChangePatient(55);
			_harness.Arm();
			Assert.IsTrue(_harness.Start());
			Assert.IsFalse(Controller.IsPlacementStopped);
			Assert.AreEqual("",Controller.PatientChangedText);
			_harness.Expose();
			Assert.AreEqual(101,_harness.RadiographStore.ListDocumentsUpdated.Single().MountItemNum,"Placed in the slot the session was at.");
		}

		[TestMethod]
		public void PatientChange_MidEnable_WithoutSafeDisable_NoDisable_OffersEnd_ImagesQuarantined_NoAutomaticEnable() {
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage,isSafeDisableInEffect:false);
			_harness.ChangePatient(77);
			Assert.IsTrue(Controller.IsPlacementStopped);
			Assert.AreEqual(0,_harness.Twain.ListDisables.Count,"Design 7.6: no disable without safe disable.");
			Assert.AreEqual(CaptureSessionPhase.Enabled,Controller.Phase);
			Assert.AreEqual("Images that arrive now go to unassigned captures. Press End to stop, or select Pat Test in the main window and re-enable "
				+"for Pat Test when the sensor software has finished.",Controller.NoticeText);
			Assert.IsTrue(Controller.CanExecute(CaptureSessionAction.End));
			_harness.Image();
			Assert.AreEqual(0,_harness.RadiographStore.ListDocumentsUpdated.Count);
			Assert.AreEqual(55,_harness.Twain.ListQuarantined.Single().Context.PatNum);
			//The main window shows the bound patient again before the enable ends: still no automatic enable.
			_harness.ChangePatient(55);
			_harness.Ended(TwainCaptureEndReason.Completed);
			Assert.AreEqual(1,_harness.Twain.ListEnables.Count,"Only a deliberate re-enable goes on.");
			Assert.AreEqual(CaptureSessionPhase.Ready,Controller.Phase);
			Assert.AreEqual(CaptureStartKind.ReEnable,Controller.StartKind);
			Assert.AreEqual(CaptureSessionText.NotEnabledAfterPatientChange,Controller.IndicatorText);
			_harness.Arm();
			Assert.IsTrue(_harness.Start());
			Assert.AreEqual(2,_harness.Twain.ListEnables.Count);
			Assert.AreEqual(101,_harness.LastRecord.MountItemTarget.MountItemNum,"The quarantined image did not fill the slot.");
		}

		[TestMethod]
		public void PatientChange_MainWindowStillOnTheOtherPatient_NextEnableRefused() {
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage,isSafeDisableInEffect:false);
			_harness.ChangePatient(77);
			_harness.Ended(TwainCaptureEndReason.Completed,ended => ended.ImageCount=0);
			Assert.AreEqual(1,_harness.Twain.ListEnables.Count);
			Assert.AreEqual(CaptureStartKind.ReEnable,Controller.StartKind);
		}

		[TestMethod]
		public void PatientChange_Tag0_CountsAsAChange() {
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage,isSafeDisableInEffect:true);
			_harness.ChangePatient(0);
			Assert.IsTrue(Controller.IsPlacementStopped,"0 means no patient: a change.");
			CollectionAssert.AreEqual(new[] { TwainCaptureStopReason.PatientChanged },_harness.Twain.ListDisables);
		}

		[TestMethod]
		public void PatientTag_SamePatNum_Ignored_OtherEventsIgnored() {
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage,isSafeDisableInEffect:true);
			_harness.PatientEvents.FirePatient(new Patient { PatNum=55,FName="Pat",LName="Edited" });
			_harness.PatientEvents.FirePatient(55L);
			_harness.PatientEvents.Fire(ODEventType.AppointmentEdited,77L);
			_harness.PatientEvents.FirePatient(77);//an int Tag carries no PatNum
			_harness.Dispatcher.RunPending();
			Assert.IsFalse(Controller.IsPlacementStopped);
			Assert.AreEqual("",Controller.PatientChangedText);
			_harness.Image();
			Assert.AreEqual(1,_harness.RadiographStore.ListDocumentsUpdated.Count);
			_harness.PatientEvents.FirePatient(new Patient { PatNum=77 });
			_harness.Dispatcher.RunPending();
			Assert.IsTrue(Controller.IsPlacementStopped,"A Patient Tag of another patient is a change.");
		}

		[TestMethod]
		public void PatientChange_FiredOnAnotherThread_AnImageRoutedBeforeThePostedHandling_GoesToTheQuarantine() {
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage,isSafeDisableInEffect:false);
			_harness.Twain.RaiseImage(TestImages.Image8(4,4,(x,y) => 1));//posted before the change
			_harness.PatNumCur=77;
			Thread thread=new Thread(() => _harness.PatientEvents.FirePatient(77L));
			thread.Start();
			Assert.IsTrue(thread.Join(5000));
			Assert.IsFalse(Controller.IsPlacementStopped,"The event thread only counts and posts.");
			Assert.AreEqual(2,_harness.Dispatcher.CountPending);
			_harness.Dispatcher.RunPending();
			Assert.AreEqual(0,_harness.RadiographStore.ListDocumentsUpdated.Count,"Placing stopped at once, before the posted handling ran.");
			Assert.AreEqual(1,_harness.Twain.ListQuarantined.Count);
			Assert.AreEqual("Patient changed in the main window. This session is still for Pat Test.",Controller.PatientChangedText);
			Assert.AreEqual(1,_harness.ListSounds.Count(x => x==CaptureSound.Error),"Handled once.");
		}

		[TestMethod]
		public void PatientChange_BeforeSafeDisableIsKnown_DisablesOnTheFirstSafeStatus() {
			_harness.Started();//the enable is posted; no status yet
			_harness.ChangePatient(77);
			Assert.AreEqual(0,_harness.Twain.ListDisables.Count,"Not known to be safe yet.");
			_harness.Status(TwainCaptureState.WaitingForImage,isSafeDisableInEffect:true);
			CollectionAssert.AreEqual(new[] { TwainCaptureStopReason.PatientChanged },_harness.Twain.ListDisables);
			Assert.AreEqual(CaptureSessionText.PatientChangedDisabling,Controller.NoticeText);
		}

		[TestMethod]
		public void PatientChange_BeforeStart_RedBar_StartRefusedUntilThePatientIsBack() {
			_harness.BeginArmed();
			_harness.ChangePatient(77);
			Assert.AreEqual("Patient changed in the main window. This session is still for Pat Test.",Controller.PatientChangedText);
			Assert.IsFalse(_harness.Start());
			Assert.AreEqual(0,_harness.ListSessions.Count);
			_harness.ChangePatient(55);
			_harness.Arm();
			Assert.IsTrue(_harness.Start());
			_harness.Expose();
			Assert.AreEqual(1,_harness.RadiographStore.ListDocumentsUpdated.Count);
		}
		#endregion Patient change

		#region Idle disable
		[TestMethod]
		public void Idle_WithSafeDisable_DisablesAfterTheMinutes_ReEnableNeedsTheDeliberateStart() {
			Assert.AreEqual(10,Controller.IdleDisableMinutes,"The design default.");
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage,isSafeDisableInEffect:true);
			_harness.Clock.Advance(10*60000-1);
			Controller.OnTick();
			Assert.AreEqual(0,_harness.Twain.ListDisables.Count);
			_harness.Clock.Advance(1);
			Controller.OnTick();
			CollectionAssert.AreEqual(new[] { TwainCaptureStopReason.Idle },_harness.Twain.ListDisables);
			Assert.AreEqual(CaptureSessionPhase.Stopping,Controller.Phase);
			Controller.OnTick();
			Assert.AreEqual(1,_harness.Twain.ListDisables.Count,"Once.");
			_harness.Ended(TwainCaptureEndReason.Stopped,ended => {
				ended.StopReason=TwainCaptureStopReason.Idle;
				ended.IsSafeDisableInEffect=true;
			});
			Assert.AreEqual(CaptureSessionPhase.Ready,Controller.Phase);
			Assert.AreEqual(CaptureIndicatorState.Disabled,Controller.IndicatorState);
			Assert.AreEqual("Sensor software closed after 10 minutes without an exposure.",Controller.IndicatorText);
			Assert.AreEqual(CaptureStartKind.ReEnable,Controller.StartKind);
			CollectionAssert.Contains(_harness.ListSounds,CaptureSound.Disabled);
			Assert.IsFalse(Controller.CanStart,"The deliberate start, 2 s after the banner.");
			_harness.Arm();
			Assert.IsTrue(_harness.Start());
			Assert.AreEqual(2,_harness.Twain.ListEnables.Count);
			Assert.AreEqual(101,_harness.LastRecord.MountItemTarget.MountItemNum);
		}

		[TestMethod]
		public void Idle_WithoutSafeDisable_NeverDisables() {
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage,isSafeDisableInEffect:false);
			for(int i=0;i<30;i++) {
				_harness.Clock.Advance(60000);
				Controller.OnTick();
			}
			Assert.AreEqual(0,_harness.Twain.ListDisables.Count);
			Assert.AreEqual(CaptureSessionPhase.Enabled,Controller.Phase);
		}

		[TestMethod]
		public void Idle_AnExposureStartsTheClockAgain() {
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage,isSafeDisableInEffect:true);
			_harness.Clock.Advance(9*60000);
			Controller.OnTick();
			_harness.Image();
			_harness.Ended(TwainCaptureEndReason.Completed);
			_harness.Status(TwainCaptureState.WaitingForImage,isSafeDisableInEffect:true);
			_harness.Clock.Advance(9*60000);
			Controller.OnTick();
			Assert.AreEqual(0,_harness.Twain.ListDisables.Count,"9 minutes since the last exposure.");
			_harness.Clock.Advance(60000);
			Controller.OnTick();
			CollectionAssert.AreEqual(new[] { TwainCaptureStopReason.Idle },_harness.Twain.ListDisables);
		}

		[TestMethod]
		public void Idle_MinutesFromCaptureIni_ZeroTurnsItOff() {
			_harness.Setup.SessionConfig=ReadSessionConfig("[Session]\r\nIdleDisableMinutes=0\r\n");
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage,isSafeDisableInEffect:true);
			_harness.Clock.Advance(120*60000);
			Controller.OnTick();
			Assert.AreEqual(0,_harness.Twain.ListDisables.Count);
			CaptureControllerHarness harness=new CaptureControllerHarness();
			harness.Setup.SessionConfig=ReadSessionConfig("[Session]\r\nIdleDisableMinutes=1\r\n");
			harness.Started();
			harness.Status(TwainCaptureState.WaitingForImage,isSafeDisableInEffect:true);
			harness.Clock.Advance(60000);
			harness.Controller.OnTick();
			CollectionAssert.AreEqual(new[] { TwainCaptureStopReason.Idle },harness.Twain.ListDisables);
			harness.Ended(TwainCaptureEndReason.Stopped,ended => ended.StopReason=TwainCaptureStopReason.Idle);
			Assert.AreEqual("Sensor software closed after 1 minute without an exposure.",harness.Controller.IndicatorText);
		}

		private static CaptureSessionConfig ReadSessionConfig(string text) {
			string path=Path.Combine(Path.GetTempPath(),"ImagingCaptureTests_"+Guid.NewGuid().ToString("N")+".ini");
			try {
				File.WriteAllText(path,text);
				return CaptureSessionConfig.Read(LocalCaptureConfig.Load(path,path));
			}
			finally {
				File.Delete(path);
			}
		}
		#endregion Idle disable

		#region Cancel, End and the forced close
		[TestMethod]
		public void Cancel_WithoutSafeDisable_WarnsInTheIndicatorArea_ThenDisablesOnTheSecondPress() {
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage,isSafeDisableInEffect:false);
			Assert.IsTrue(_harness.Execute(CaptureSessionAction.Disable));
			Assert.AreEqual("If an exposure was just taken, wait until it appears before cancelling. Press Disable again to stop now.",Controller.WarningText);
			Assert.AreEqual(0,_harness.Twain.ListDisables.Count);
			Assert.IsTrue(_harness.Execute(CaptureSessionAction.Disable));
			CollectionAssert.AreEqual(new[] { TwainCaptureStopReason.Cancel },_harness.Twain.ListDisables,"The operator's stop, in the safe order.");
			//End warns the same way.
			CaptureControllerHarness harness=new CaptureControllerHarness();
			harness.Started();
			harness.Execute(CaptureSessionAction.End);
			Assert.AreEqual("If an exposure was just taken, wait until it appears before cancelling. Press End again to stop now.",
				harness.Controller.WarningText);
			Assert.AreEqual(0,harness.Twain.ListDisables.Count);
		}

		[TestMethod]
		public void Cancel_WithSafeDisable_NoWarning() {
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage,isSafeDisableInEffect:true);
			Assert.IsTrue(_harness.Execute(CaptureSessionAction.Disable));
			Assert.AreEqual("",Controller.WarningText);
			CollectionAssert.AreEqual(new[] { TwainCaptureStopReason.Cancel },_harness.Twain.ListDisables);
		}

		[TestMethod]
		public void ForcedClose_OneSecondIntoASafeStop_ALateImageGoesToTheQuarantine() {
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage,isSafeDisableInEffect:true);
			_harness.Execute(CaptureSessionAction.End);
			CollectionAssert.AreEqual(new[] { TwainCaptureStopReason.End },_harness.Twain.ListDisables);
			FakeTwainCaptureSession twain=_harness.Twain;
			_harness.Clock.Advance(1000);
			Controller.OnClosing();//FormClosing: does not wait for the worker
			Assert.AreEqual(CaptureSessionPhase.Finished,Controller.Phase);
			Assert.AreEqual(1,twain.CountBeginClose);
			Assert.AreEqual(CaptureSessionEndReason.Closed,Controller.Summary.EndReason);
			Assert.AreEqual(0,_harness.PatientEvents.CountHandlers,"The monitor stops.");
			//10 s later the worker, still stopping in the safe order, gets the image: it goes to the quarantine.
			_harness.Clock.Advance(10000);
			twain.RaiseQuarantinedByLibrary(TestImages.Image8(4,4,(x,y) => 1));
			_harness.Dispatcher.RunPending();
			Assert.AreEqual(1,Controller.Summary.CountQuarantined);
			Assert.AreEqual(0,_harness.RadiographStore.ListDocumentsUpdated.Count);
		}

		[TestMethod]
		public void ForcedClose_Tag0ThenFormClosing_AnImageHandledLaterIsQuarantined() {
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage,isSafeDisableInEffect:false);
			FakeTwainCaptureSession twain=_harness.Twain;
			_harness.ChangePatient(0);//a logoff
			Assert.IsTrue(Controller.IsPlacementStopped);
			twain.RaiseImage(TestImages.Image8(4,4,(x,y) => 1));
			Controller.OnClosing();
			Assert.AreEqual(0,twain.ListDisables.Count,"The forced close stops the source through BeginClose, on the worker.");
			Assert.AreEqual(1,twain.CountBeginClose);
			_harness.Dispatcher.RunPending();
			StringAssert.Contains(twain.ListQuarantined.Single().Reason,"after the capture session ended");
			Assert.AreEqual(1,Controller.Summary.CountQuarantined);
		}

		[TestMethod]
		public void WithoutSafeDisable_NoDisableOfTheControllersOwn_ExceptTheForcedClose() {
			_harness=new CaptureControllerHarness(5);
			_harness.Setup.Options.SessionMode=TwainSessionMode.PerSlot;
			_harness.Started();
			_harness.Status(TwainCaptureState.WaitingForImage,isSafeDisableInEffect:false);
			_harness.Execute(CaptureSessionAction.Skip);
			_harness.Execute(CaptureSessionAction.NextSlot);
			_harness.Execute(CaptureSessionAction.PreviousSlot);
			Controller.SelectSlot(Controller.MountSlots.IndexOf(Controller.MountSlots.ListMountItems.First(x => x.ItemOrder==5)));
			_harness.Image();
			_harness.Ended(TwainCaptureEndReason.Completed);
			_harness.Status(TwainCaptureState.WaitingForImage,isSafeDisableInEffect:false);
			_harness.Execute(CaptureSessionAction.Retake);
			_harness.Clock.Advance(60*60000);
			Controller.OnTick();
			_harness.ChangePatient(77);
			Assert.AreEqual(0,_harness.ListSessions.Sum(x => x.ListDisables.Count),"Target changes, Retake, idle and a patient change.");
			Controller.OnClosing();
			Assert.AreEqual(1,_harness.Twain.CountBeginClose);
		}
		#endregion Cancel, End and the forced close
	}
}
