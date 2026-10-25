// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ImagingDeviceManager.TwainProtocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingDeviceManager.Tests {
	///<summary>Design 7.1 "Parent destroyed or logoff": each pass of the nested loop checks IsWindow on the window passed to Acquire and
	///watches for WM_QUIT; either stops the acquisition in the safe order, images arriving afterwards go to the drain sink, never to
	///the caller, and WM_QUIT is posted again once the source is stopped.</summary>
	[TestClass]
	public class TwainParentAndQuitTests {
		private SessionHarness _harness;
		private Form _form;

		[TestInitialize]
		public void Setup() {
			_harness=new SessionHarness();
			_harness.Session.Settings=new TwainCaptureSettings {
				SafeDisableCap=FakeHelperSource.CapSafeDisable,
				ListDisableIsSafeSources=new List<string> { _harness.Helper.ProductName },
				ListRefusedDisableCodes=new List<ushort> { FakeHelperSource.CodeDisableImagesPending,FakeHelperSource.CodeDisableClosing },
				RefusedDisableWait=TimeSpan.FromMilliseconds(2000),
			};
			_form=new Form { ShowInTaskbar=false };//Like the hidden form the Mount/Acquire loop passes.
			IntPtr hwnd=_form.Handle;
		}

		[TestCleanup]
		public void Cleanup() {
			_form.Dispose();
			TestWin32.TakeQuitMessages();//Never leave a WM_QUIT for the test runner.
			_harness.Dispose();
		}

		private FakeHelperSource Helper => _harness.Helper;
		private TwainSession Session => _harness.Session;
		private RecordingDrainSink Sink => _harness.Sink;

		private Imaging.DibImage AcquireByNameWithParent() {
			Assert.IsTrue(Session.OpenSource(Helper.ProductName),Session.ErrorText);
			return Session.Acquire(_form.Handle);
		}

		#region Parent destroyed
		[TestMethod]
		public void ParentDestroyedDuringTheWait_SafeStop_LateImageGoesToTheSink() {
			Helper.ListExposures.Add(SessionHarness.Exposure(8));
			Helper.EndAfterMs=60000;//The image is being read out.
			using(Timer timer=new Timer()) {
				timer.Interval=50;
				timer.Tick+=(sender,e) => {
					timer.Stop();
					_form.Dispose();//Inside the session's message loop, as an automatic logoff closes the form.
				};
				timer.Start();
				Assert.IsNull(AcquireByNameWithParent(),"Nothing goes back to the caller.");
			}
			Assert.AreEqual(EZTwainErrorCode.EZTEC_USER_CANCEL,Session.ErrorCode);
			Assert.AreEqual(TwainStopReason.ParentDestroyed,Session.LastStopReason);
			Assert.AreEqual(1,Sink.ListDrained.Count,"The image that arrived after the stop is kept.");
			Assert.AreEqual(TwainStopReason.ParentDestroyed,Sink.ListDrained[0].StopReason);
			Assert.AreEqual(1,Helper.CountDelivered);
			_harness.AssertDisabledOnlyInState5();
		}

		[TestMethod]
		public void ParentDestroyed_ImageReadyAtTheSameTime_GoesToTheSinkNotTheCaller() {
			Helper.ListExposures.Add(SessionHarness.Exposure(8));
			Helper.ActionOnEnable=() => _form.Dispose();
			Assert.IsNull(AcquireByNameWithParent());
			Assert.AreEqual(1,Sink.ListDrained.Count);
			Assert.AreEqual(TwainDrainReason.Stop,Sink.ListDrained[0].Reason);
			Assert.AreEqual(TwainStopReason.ParentDestroyed,Sink.ListDrained[0].StopReason);
		}

		[TestMethod]
		public void ParentDestroyedBetweenTheImagesOfABatch_RemainingImagesGoToTheSink() {
			for(int i=0;i<3;i++) {
				Helper.ListExposures.Add(SessionHarness.Exposure(8+i));
			}
			Assert.IsTrue(Session.OpenSource(Helper.ProductName));
			Session.MultiTransfer=true;
			IntPtr hwnd=_form.Handle;
			Assert.IsNotNull(Session.Acquire(_form.Handle));
			_form.Dispose();
			Assert.IsNull(Session.Acquire(hwnd));
			Assert.AreEqual(TwainStopReason.ParentDestroyed,Session.LastStopReason);
			Assert.AreEqual(2,Sink.ListDrained.Count);
			Assert.IsTrue(Session.IsDone());
			Session.CloseSource();
			_harness.AssertDisabledOnlyInState5();
		}

		[TestMethod]
		public void ParentNotWatched_OnTheScanPath() {
			FakeTwainDsm fake=_harness.Fake;
			fake.ListPages.Add(FakePage.CreateRgb(4,4,300,(x,y) => Color.Gray));
			fake.ActionOnEnable=() => _form.Dispose();
			Assert.IsTrue(Session.OpenSource(null));
			Assert.IsNotNull(Session.Acquire(_form.Handle),"Scans keep their behaviour.");
			Assert.AreEqual(0,Sink.ListDrained.Count);
		}
		#endregion Parent destroyed

		#region WM_QUIT
		[TestMethod]
		public void WmQuitDuringTheWait_SafeStop_ImageToTheSink_QuitPostedAgainAfterwards() {
			Helper.ListExposures.Add(SessionHarness.Exposure(8));
			Helper.EndAfterMs=60000;
			Helper.ActionOnEnable=() => NativeMethods.PostQuitMessage(7);
			Assert.IsNull(AcquireByNameWithParent());
			Assert.AreEqual("The program is closing.",Session.ErrorText);
			Assert.AreEqual(TwainStopReason.Quit,Session.LastStopReason);
			Assert.AreEqual(1,Sink.ListDrained.Count,"The refused disable was waited out despite WM_QUIT, and the image kept.");
			Assert.AreEqual(TwainStopReason.Quit,Sink.ListDrained[0].StopReason);
			CollectionAssert.AreEqual(new List<int> { 7 },TestWin32.TakeQuitMessages(),"WM_QUIT is posted again, once.");
			_harness.AssertDisabledOnlyInState5();
		}

		[TestMethod]
		public void WmQuit_ScanPath_StopsAndQuitPostedAgain() {
			FakeTwainDsm fake=_harness.Fake;
			fake.ListPages.Add(FakePage.CreateRgb(4,4,300,(x,y) => Color.Gray));
			fake.EnableBehavior=FakeEnableBehavior.Nothing;
			fake.ActionOnEnable=() => NativeMethods.PostQuitMessage(3);
			Assert.IsTrue(Session.OpenSource(null));
			Assert.IsNull(Session.Acquire(IntPtr.Zero));
			Assert.AreEqual(EZTwainErrorCode.EZTEC_USER_CANCEL,Session.ErrorCode);
			Assert.AreEqual("The program is closing.",Session.ErrorText);
			Assert.IsFalse(fake.IsEnabled);
			CollectionAssert.AreEqual(new List<int> { 3 },TestWin32.TakeQuitMessages());
		}
		#endregion WM_QUIT
	}
}
