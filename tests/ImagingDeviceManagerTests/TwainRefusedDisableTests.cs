// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using ImagingDeviceManager.TwainProtocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingDeviceManager.Tests {
	///<summary>The wait after a refused disable (design 7.1 "Refused disable", twain-contract 1.1 section 10.2), against the contract
	///1.1 profile with its published capability and condition codes entered as [Twain] data.</summary>
	[TestClass]
	public class TwainRefusedDisableTests {
		private SessionHarness _harness;
		private List<bool> _listWaiting;

		[TestInitialize]
		public void Setup() {
			_harness=new SessionHarness();
			_harness.Session.Settings=new TwainCaptureSettings {
				SafeDisableCap=FakeHelperSource.CapSafeDisable,
				ListDisableIsSafeSources=new List<string> { _harness.Helper.ProductName },
				ListRefusedDisableCodes=new List<ushort> { FakeHelperSource.CodeDisableImagesPending,FakeHelperSource.CodeDisableClosing },
				RefusedDisableWait=TimeSpan.FromMilliseconds(2000),
			};
			_listWaiting=new List<bool>();
			_harness.Session.WaitingForSourceChanged+=_listWaiting.Add;
		}

		[TestCleanup]
		public void Cleanup() {
			_harness.Dispose();
		}

		private FakeHelperSource Helper => _harness.Helper;
		private TwainSession Session => _harness.Session;
		private RecordingDrainSink Sink => _harness.Sink;

		///<summary>The operator took an image and the helper is still reading it out when the library cancels.</summary>
		private void HelperBusyWithOneImage() {
			Helper.ListExposures.Add(SessionHarness.Exposure(8));
			Helper.EndAfterMs=60000;
			Helper.ActionOnEnable=() => Session.Cancel();
		}

		private int IndexOfDisable(int occurrence) {
			List<int> list=Enumerable.Range(0,_harness.Fake.ListCalls.Count).Where(i => _harness.Fake.ListCalls[i].Name=="DISABLEDS").ToList();
			return occurrence<list.Count ? list[occurrence] : -1;
		}

		[TestMethod]
		public void RefusedWhileClosing_ThenXferReady_ImagesDrained_ThenDisabledAgain() {
			HelperBusyWithOneImage();
			Assert.IsNull(_harness.AcquireByName(),"Nothing goes back to the caller after a Cancel.");
			Assert.AreEqual(EZTwainErrorCode.EZTEC_USER_CANCEL,Session.ErrorCode);
			Assert.IsTrue(Session.IsSafeDisableInEffect);
			List<FakeCall> listCalls=_harness.Fake.ListCalls;
			int idxRefused=IndexOfDisable(0);
			Assert.AreEqual(Tw.TWRC_FAILURE,listCalls[idxRefused].Rc);
			Assert.AreEqual(FakeHelperSource.CodeDisableClosing,listCalls[idxRefused].Condition);
			int idxReady=_harness.IndexOfCall(x => x.Name=="PROCESSEVENT" && x.Value==Tw.MSG_XFERREADY,idxRefused);
			int idxTransfer=_harness.IndexOfCall(x => x.Name=="NATIVEXFER",idxReady);
			int idxEnd=_harness.IndexOfCall(x => x.Name=="ENDXFER",idxTransfer);
			int idxDisabled=IndexOfDisable(1);
			Assert.IsTrue(idxReady>idxRefused && idxTransfer>idxReady && idxEnd>idxTransfer && idxDisabled>idxEnd,_harness.CallText);
			Assert.AreEqual(Tw.TWRC_SUCCESS,listCalls[idxDisabled].Rc);
			Assert.AreEqual(-1,IndexOfDisable(2));
			Assert.AreEqual(1,Sink.ListDrained.Count);
			Assert.AreEqual(TwainDrainReason.RefusedDisable,Sink.ListDrained[0].Reason);
			Assert.AreEqual(TwainStopReason.Cancel,Sink.ListDrained[0].StopReason);
			Assert.AreEqual(1,Helper.CountDelivered,"Nothing is lost.");
			Assert.AreEqual(0,Sink.ListMayStillHold.Count);
			Assert.IsFalse(Session.SourceMayHoldImages);
			CollectionAssert.AreEqual(new List<bool> { true,false },_listWaiting,"The status window can say it is waiting.");
			Assert.IsTrue(_harness.HasLog("Stopping \""+Helper.ProductName+"\" (Cancel); safe disable in effect: yes."),_harness.LogText);
			Assert.IsTrue(_harness.HasLog("\""+Helper.ProductName+"\" was disabled after MSG_XFERREADY, "),_harness.LogText);
			Assert.IsFalse(listCalls.Any(x => x.Condition==FakeHelperSource.CodeDisableImagesPending),"Never disabled in state 6.");
			_harness.AssertDisabledOnlyInState5();
			_harness.AssertStatusReadAfterEveryFailure();
		}

		[TestMethod]
		public void RefusedWhileClosing_NoImage_ThenCloseRequest_StatusReadThenDisabledAgain() {
			Helper.End=FakeHelperEnd.Never;//The helper is idle: no image was taken.
			Helper.ActionOnEnable=() => Session.Cancel();
			Assert.IsNull(_harness.AcquireByName());
			int idxRefused=IndexOfDisable(0);
			int idxCloseRequest=_harness.IndexOfCall(x => x.Name=="PROCESSEVENT" && x.Value==Tw.MSG_CLOSEDSREQ,idxRefused);
			int idxStatus=_harness.IndexOfCall(x => x.Name=="STATUS",idxCloseRequest);
			int idxDisabled=IndexOfDisable(1);
			Assert.IsTrue(idxRefused>=0 && idxCloseRequest>idxRefused && idxStatus>idxCloseRequest && idxDisabled>idxStatus,_harness.CallText);
			Assert.AreEqual(Tw.TWRC_SUCCESS,_harness.Fake.ListCalls[idxDisabled].Rc);
			Assert.AreEqual(Tw.TWCC_SUCCESS,Session.EndConditionCode);
			Assert.AreEqual(0,Sink.ListDrained.Count);
			Assert.AreEqual(0,Sink.ListMayStillHold.Count);
			Assert.IsTrue(_harness.HasLog("was disabled after MSG_CLOSEDSREQ, "),_harness.LogText);
			Assert.IsFalse(_harness.HasLog("No notification"),_harness.LogText);
		}

		[TestMethod]
		public void WayOutRefusedWhileFinishing_NotificationInTheSecondBound_ImageDrained_NothingHeld() {
			Session.Settings.RefusedDisableWait=TimeSpan.FromMilliseconds(400);
			HelperBusyWithOneImage();
			Helper.IgnoresCloseRequest=true;
			Helper.ForcedEndMs=600;//Ended by the source after the first bound, within the second.
			Assert.IsNull(_harness.AcquireByName());
			Assert.AreEqual(EZTwainErrorCode.EZTEC_USER_CANCEL,Session.ErrorCode);
			List<FakeCall> listCalls=_harness.Fake.ListCalls;
			int idxRefused=IndexOfDisable(0);
			int idxWayOut=IndexOfDisable(1);
			Assert.AreEqual(FakeHelperSource.CodeDisableClosing,listCalls[idxRefused].Condition);
			Assert.AreEqual(Tw.TWRC_FAILURE,listCalls[idxWayOut].Rc,"The helper still runs: the way-out is refused too.");
			Assert.AreEqual(FakeHelperSource.CodeDisableClosing,listCalls[idxWayOut].Condition);
			int idxReady=_harness.IndexOfCall(x => x.Name=="PROCESSEVENT" && x.Value==Tw.MSG_XFERREADY,idxWayOut);
			int idxTransfer=_harness.IndexOfCall(x => x.Name=="NATIVEXFER",idxReady);
			int idxEnd=_harness.IndexOfCall(x => x.Name=="ENDXFER",idxTransfer);
			int idxDisabled=IndexOfDisable(2);
			Assert.IsTrue(idxReady>idxWayOut && idxTransfer>idxReady && idxEnd>idxTransfer && idxDisabled>idxEnd,_harness.CallText);
			Assert.AreEqual(Tw.TWRC_SUCCESS,listCalls[idxDisabled].Rc);
			Assert.AreEqual(-1,IndexOfDisable(3));
			Assert.AreEqual(1,Sink.ListDrained.Count);
			Assert.AreEqual(TwainDrainReason.RefusedDisable,Sink.ListDrained[0].Reason);
			Assert.AreEqual(1,Helper.CountDelivered,"Nothing is lost.");
			Assert.AreEqual(0,Sink.ListMayStillHold.Count,"The source offered what it held.");
			Assert.IsFalse(Session.SourceMayHoldImages);
			CollectionAssert.AreEqual(new List<bool> { true,false },_listWaiting,"One wait for the status window.");
			Assert.IsTrue(_harness.HasLog("the way-out MSG_DISABLEDS was refused too (condition code 0x8007)"),_harness.LogText);
			Assert.IsTrue(_harness.HasLog("\""+Helper.ProductName+"\" was disabled after MSG_XFERREADY, "),_harness.LogText);
			Assert.IsFalse(_harness.HasLog("treated as disabled"),_harness.LogText);
			Assert.IsFalse(_harness.HasLog("may still hold images"),_harness.LogText);
			_harness.AssertDisabledOnlyInState5();
			_harness.AssertStatusReadAfterEveryFailure();
		}

		[TestMethod]
		public void NoNotificationInEitherBound_LastDisableRefused_TreatedAsDisabled_MayStillHoldImages() {
			Session.Settings.RefusedDisableWait=TimeSpan.FromMilliseconds(300);
			HelperBusyWithOneImage();
			Helper.IgnoresCloseRequest=true;
			Helper.ForcedEndMs=30000;//Long after both bounds: a source that never notifies.
			Stopwatch stopwatch=Stopwatch.StartNew();
			Assert.IsNull(_harness.AcquireByName());
			long elapsed=stopwatch.ElapsedMilliseconds;
			Assert.IsTrue(elapsed>=560 && elapsed<5000,"Waited about both bounds: "+elapsed+" ms");
			List<FakeCall> listCalls=_harness.Fake.ListCalls;
			for(int i=0;i<3;i++) {
				Assert.AreEqual(Tw.TWRC_FAILURE,listCalls[IndexOfDisable(i)].Rc,"disable "+i);
				Assert.AreEqual(FakeHelperSource.CodeDisableClosing,listCalls[IndexOfDisable(i)].Condition,"disable "+i);
			}
			Assert.AreEqual(-1,IndexOfDisable(3),"The refused one, the way-out after the first bound, the last after the second; no others.");
			Assert.IsFalse(listCalls.Any(x => x.Name=="ENDXFER"),"Consecutive.");
			CollectionAssert.AreEqual(new List<TwainStopReason> { TwainStopReason.Cancel },Sink.ListMayStillHold);
			Assert.IsTrue(Session.SourceMayHoldImages);
			CollectionAssert.AreEqual(new List<bool> { true,false },_listWaiting);
			Assert.IsTrue(_harness.HasLog("the way-out MSG_DISABLEDS was refused too"),_harness.LogText);
			Assert.IsTrue(_harness.HasLog("Contract deviation: no notification from"),_harness.LogText);
			Assert.IsTrue(_harness.HasLog("treated as disabled"),_harness.LogText);
			Assert.IsTrue(_harness.HasLog("may still hold images"),_harness.LogText);
			Assert.AreEqual(0,Helper.CountDelivered);
			Assert.AreEqual(1,Helper.CountRedeliverable,"The image stays with the source.");
			_harness.AssertDisabledOnlyInState5();
			_harness.AssertStatusReadAfterEveryFailure();
		}

		[TestMethod]
		public void RefusedAndHelperNeverExits_EndedAtForcedEnd_ImagesStillOfferedAndDrained() {
			HelperBusyWithOneImage();
			Helper.IgnoresCloseRequest=true;
			Helper.ForcedEndMs=150;
			Assert.IsNull(_harness.AcquireByName());
			Assert.AreEqual(1,Sink.ListDrained.Count);
			Assert.AreEqual(0,Sink.ListMayStillHold.Count);
			Assert.AreEqual(1,Helper.CountDelivered);
		}

		[TestMethod]
		public void RefusedWithUnlistedCode_WhileSafeDisableSet_LoggedAsDeviation_HandledTheSame() {
			Session.Settings.ListRefusedDisableCodes=new List<ushort>();
			HelperBusyWithOneImage();
			Assert.IsNull(_harness.AcquireByName());
			Assert.IsTrue(_harness.HasLog("Contract deviation"),_harness.LogText);
			Assert.AreEqual(1,Sink.ListDrained.Count);
			Assert.AreEqual(TwainDrainReason.RefusedDisable,Sink.ListDrained[0].Reason);
		}

		[TestMethod]
		public void RefusedWithListedCode_WithoutSafeDisable_Waits() {
			//A source refusing with a listed code is waited for even when no safe-disable capability is configured.
			Session.Settings.SafeDisableCap=null;
			Session.Settings.RefusedDisableWait=TimeSpan.FromMilliseconds(200);
			FakeTwainDsm fake=_harness.Fake;
			fake.ListPages.Add(FakePage.CreateRgb(4,4,300,(x,y) => Color.Gray));
			fake.EnableBehavior=FakeEnableBehavior.Nothing;
			fake.QueueDisableFailures.Enqueue(FakeHelperSource.CodeDisableClosing);
			fake.ActionOnEnable=() => Session.Cancel();
			Assert.IsTrue(Session.OpenSource("Fake Sensor"));
			Assert.IsNull(Session.Acquire(IntPtr.Zero));
			Assert.IsFalse(Session.IsSafeDisableInEffect);
			CollectionAssert.AreEqual(new List<bool> { true,false },_listWaiting);
			Assert.AreEqual(2,fake.ListDisableStates.Count,"Refused, then the way-out one after the bound, which succeeds.");
			Assert.IsFalse(fake.IsEnabled);
			Assert.AreEqual(1,Sink.ListMayStillHold.Count);
			Assert.IsTrue(Session.SourceMayHoldImages);
			Assert.IsTrue(_harness.HasLog("The way-out MSG_DISABLEDS succeeded. The source may still hold images"),_harness.LogText);
		}

		[TestMethod]
		public void WayOutRefusedWithListedCode_NoNotificationInTheSecondBound_LastDisableSucceeds_MayStillHoldImages() {
			Session.Settings.SafeDisableCap=null;
			Session.Settings.RefusedDisableWait=TimeSpan.FromMilliseconds(200);
			FakeTwainDsm fake=_harness.Fake;
			fake.ListPages.Add(FakePage.CreateRgb(4,4,300,(x,y) => Color.Gray));
			fake.EnableBehavior=FakeEnableBehavior.Nothing;
			fake.QueueDisableFailures.Enqueue(FakeHelperSource.CodeDisableClosing);
			fake.QueueDisableFailures.Enqueue(FakeHelperSource.CodeDisableClosing);
			fake.ActionOnEnable=() => Session.Cancel();
			Assert.IsTrue(Session.OpenSource("Fake Sensor"));
			Stopwatch stopwatch=Stopwatch.StartNew();
			Assert.IsNull(Session.Acquire(IntPtr.Zero));
			Assert.IsTrue(stopwatch.ElapsedMilliseconds>=370,"Waited both bounds: "+stopwatch.ElapsedMilliseconds+" ms");
			Assert.AreEqual(3,fake.ListDisableStates.Count,"Refused, the way-out refused after the first bound, the last after the second.");
			CollectionAssert.AreEqual(new List<bool> { true,false },_listWaiting);
			Assert.AreEqual(1,Sink.ListMayStillHold.Count);
			Assert.IsTrue(Session.SourceMayHoldImages);
			Assert.IsTrue(_harness.HasLog("Waiting up to 0.2 s more"),_harness.LogText);
			Assert.IsTrue(_harness.HasLog("The last MSG_DISABLEDS succeeded. The source may still hold images"),_harness.LogText);
			Assert.IsFalse(_harness.HasLog("treated as disabled"),_harness.LogText);
		}

		[TestMethod]
		public void WayOutRefusedWithUnlistedCode_WithoutSafeDisable_TreatedAsDisabledWithoutASecondBound() {
			Session.Settings.SafeDisableCap=null;
			Session.Settings.RefusedDisableWait=TimeSpan.FromMilliseconds(200);
			FakeTwainDsm fake=_harness.Fake;
			fake.ListPages.Add(FakePage.CreateRgb(4,4,300,(x,y) => Color.Gray));
			fake.EnableBehavior=FakeEnableBehavior.Nothing;
			fake.QueueDisableFailures.Enqueue(FakeHelperSource.CodeDisableClosing);
			fake.QueueDisableFailures.Enqueue(Tw.TWCC_BUMMER);
			fake.ActionOnEnable=() => Session.Cancel();
			Assert.IsTrue(Session.OpenSource("Fake Sensor"));
			Assert.IsNull(Session.Acquire(IntPtr.Zero));
			Assert.AreEqual(2,fake.ListDisableStates.Count);
			Assert.AreEqual(1,Sink.ListMayStillHold.Count);
			Assert.IsTrue(_harness.HasLog("The way-out MSG_DISABLEDS was refused (condition code "+TwainSession.ToHex(Tw.TWCC_BUMMER)
				+"); the source is treated as disabled."),_harness.LogText);
			Assert.IsFalse(_harness.HasLog("s more"),"No second bound: "+_harness.LogText);
		}

		[TestMethod]
		public void RefusedWithUnlistedCode_WithoutSafeDisable_TreatedAsDisabledAsBefore() {
			Session.Settings.SafeDisableCap=null;
			Session.Settings.ListRefusedDisableCodes=new List<ushort>();
			FakeTwainDsm fake=_harness.Fake;
			fake.ListPages.Add(FakePage.CreateRgb(4,4,300,(x,y) => Color.Gray));
			fake.EnableBehavior=FakeEnableBehavior.Nothing;
			fake.QueueDisableFailures.Enqueue(Tw.TWCC_BUMMER);
			fake.ActionOnEnable=() => Session.Cancel();
			Assert.IsTrue(Session.OpenSource("Fake Sensor"));
			Assert.IsNull(Session.Acquire(IntPtr.Zero));
			Assert.AreEqual(0,_listWaiting.Count,"No wait.");
			Assert.AreEqual(1,fake.ListDisableStates.Count);
			Assert.IsTrue(_harness.HasLog("treated as disabled"),_harness.LogText);
			Assert.AreEqual(0,Sink.ListMayStillHold.Count);
			Assert.IsFalse(fake.IsSourceOpen,"The source is still closed afterwards.");
		}

		[TestMethod]
		public void RepeatedXferReadyWithNothingPending_IsIgnored() {
			Helper.End=FakeHelperEnd.Never;
			Helper.SpuriousXferReadyAfterRefusal=true;
			Helper.ActionOnEnable=() => Session.Cancel();
			Assert.IsNull(_harness.AcquireByName());
			int idxSpurious=_harness.IndexOfCall(x => x.Name=="PROCESSEVENT" && x.Value==Tw.MSG_XFERREADY);
			Assert.IsTrue(idxSpurious>=0,_harness.CallText);
			Assert.AreEqual("PENDINGXFERS/GET",_harness.Fake.ListCalls[idxSpurious+1].Name);
			Assert.IsFalse(_harness.Fake.ListCalls.Any(x => x.Name=="IMAGEINFO" || x.Name=="NATIVEXFER"),"No transfer is tried.");
			Assert.IsTrue(_harness.HasLog("nothing pending"),_harness.LogText);
			Assert.IsTrue(_harness.Fake.ListCalls.Any(x => x.Name=="PROCESSEVENT" && x.Value==Tw.MSG_CLOSEDSREQ),"The real notification still ends the wait.");
			Assert.AreEqual(Tw.TWRC_SUCCESS,_harness.Fake.ListCalls[IndexOfDisable(1)].Rc);
			Assert.AreEqual(0,Sink.ListMayStillHold.Count);
		}

		[TestMethod]
		public void SafeDisable_NormalEndOfASingleAcquisition_DisablesAtOnce() {
			Helper.ListExposures.Add(SessionHarness.Exposure(8));
			Assert.IsNotNull(_harness.AcquireByName(),Session.ErrorText);
			Assert.AreEqual(1,_harness.Fake.ListDisableStates.Count);
			Assert.AreEqual(0,_listWaiting.Count);
			Assert.AreEqual(TwainStopReason.None,Session.LastStopReason);
		}

		[TestMethod]
		public void RefusedWithoutSink_OfferedImagesResetAndKeptBySource() {
			Session.DrainSink=null;
			HelperBusyWithOneImage();
			Assert.IsNull(_harness.AcquireByName());
			Assert.AreEqual(1,_harness.Fake.CallsNamed("RESET").Count);
			Assert.AreEqual(1,Helper.CountKeptForRedelivery,"Not transferred, so a redelivery can still offer it.");
			Assert.AreEqual(0,Helper.CountLostForHost);
		}
	}
}
