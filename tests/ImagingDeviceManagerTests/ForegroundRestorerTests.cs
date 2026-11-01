// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using ImagingDeviceManager.Acquire;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingDeviceManager.Tests {
	///<summary>Activation (design 7.1): the foreground window is recorded before anything is disabled, never forced between acquisitions,
	///and restored after the acquire ends only if no new acquire starts within about 1 s and no other program has the foreground.</summary>
	[TestClass]
	public class ForegroundRestorerTests {
		private static readonly IntPtr _hwndMain=new IntPtr(0x100);
		private static readonly IntPtr _hwndOwnOther=new IntPtr(0x104);
		private static readonly IntPtr _hwndHelper=new IntPtr(0x200);//The sensor software, another process.
		private FakeForegroundApi _api;
		private ManualDelayScheduler _scheduler;
		private ForegroundRestorer _restorer;

		[TestInitialize]
		public void Setup() {
			_api=new FakeForegroundApi();
			_api.HashSetOwnWindows.Add(_hwndMain);
			_api.HashSetOwnWindows.Add(_hwndOwnOther);
			_api.HashSetOtherWindows.Add(_hwndHelper);
			_api.HwndForeground=_hwndMain;
			_scheduler=new ManualDelayScheduler();
			_restorer=new ForegroundRestorer(_api,_scheduler);
		}

		[TestMethod]
		public void RestoredAfterTheDelay_WhenNobodyHasTheForeground() {
			_restorer.BeginAcquire();
			Assert.AreEqual(_hwndMain,_restorer.HwndRecorded);
			_api.HwndForeground=_hwndHelper;
			_restorer.EndAcquire();
			_api.HwndForeground=IntPtr.Zero;//The sensor software closed; Windows gave the foreground to nobody.
			Assert.AreEqual(0,_api.ListSetForeground.Count,"Nothing is forced when the acquire ends.");
			Assert.AreEqual(ForegroundRestorer.RestoreDelayMs,_scheduler.DelayMsPending);
			_scheduler.Elapse();
			CollectionAssert.AreEqual(new List<IntPtr> { _hwndMain },_api.ListSetForeground);
		}

		[TestMethod]
		public void RestoredWhenAnotherWindowOfThisProgramHasTheForeground() {
			_restorer.BeginAcquire();
			_restorer.EndAcquire();
			_api.HwndForeground=_hwndOwnOther;
			_scheduler.Elapse();
			CollectionAssert.AreEqual(new List<IntPtr> { _hwndMain },_api.ListSetForeground);
		}

		[TestMethod]
		public void NeverForcedBetweenAcquisitions_SeriesKeepsTheFirstWindow() {
			_restorer.BeginAcquire();
			_restorer.EndAcquire();
			_api.HwndForeground=_hwndHelper;//The next slot's sensor software is already in front.
			_restorer.BeginAcquire();//Within the delay.
			Assert.IsNull(_scheduler.ActionPending,"The restore is cancelled.");
			Assert.AreEqual(1,_scheduler.CountCancel);
			Assert.AreEqual(_hwndMain,_restorer.HwndRecorded,"The window recorded when the series started is kept.");
			_restorer.EndAcquire();
			_api.HwndForeground=IntPtr.Zero;
			_scheduler.Elapse();
			CollectionAssert.AreEqual(new List<IntPtr> { _hwndMain },_api.ListSetForeground);
		}

		[TestMethod]
		public void NotRestored_WhenAnotherProgramHasTheForeground() {
			_restorer.BeginAcquire();
			_restorer.EndAcquire();
			_api.HwndForeground=_hwndHelper;
			_scheduler.Elapse();
			Assert.AreEqual(0,_api.ListSetForeground.Count);
			Assert.AreEqual(IntPtr.Zero,_restorer.HwndRecorded,"Forgotten: the next acquire records afresh.");
		}

		[TestMethod]
		public void NotRestored_WhenTheTimerTicksDuringAnAcquire() {
			_restorer.BeginAcquire();
			_restorer.EndAcquire();
			Action actionStale=_scheduler.ActionPending;
			_restorer.BeginAcquire();
			_api.HwndForeground=IntPtr.Zero;
			actionStale();//A tick already queued, dispatched by the next acquisition's message loop.
			Assert.AreEqual(0,_api.ListSetForeground.Count);
		}

		[TestMethod]
		public void NotRestored_WhenTheWindowIsDisabledHiddenGoneOrAlreadyInFront() {
			Assert.IsFalse(ForegroundRestorer.ShouldRestore(_api,_hwndMain,_hwndMain),"Already in front.");
			Assert.IsTrue(ForegroundRestorer.ShouldRestore(_api,_hwndMain,IntPtr.Zero));
			_api.HashSetDisabled.Add(_hwndMain);//A message box is open over it.
			Assert.IsFalse(ForegroundRestorer.ShouldRestore(_api,_hwndMain,IntPtr.Zero));
			_api.HashSetDisabled.Clear();
			_api.HashSetHidden.Add(_hwndMain);
			Assert.IsFalse(ForegroundRestorer.ShouldRestore(_api,_hwndMain,IntPtr.Zero));
			_api.HashSetHidden.Clear();
			Assert.IsFalse(ForegroundRestorer.ShouldRestore(_api,new IntPtr(0x999),IntPtr.Zero),"Gone.");
			Assert.IsFalse(ForegroundRestorer.ShouldRestore(_api,_hwndHelper,IntPtr.Zero),"Never another program's window.");
			Assert.IsFalse(ForegroundRestorer.ShouldRestore(_api,IntPtr.Zero,IntPtr.Zero));
		}

		[TestMethod]
		public void NothingRecorded_NothingScheduled() {
			_api.HwndForeground=IntPtr.Zero;
			_restorer.BeginAcquire();
			_restorer.EndAcquire();
			Assert.IsNull(_scheduler.ActionPending);
		}
	}

	///<summary>The facade records the foreground before a thread-modal acquisition and schedules the restore after it.</summary>
	[TestClass]
	public class ForegroundFacadeTests {
		private FacadeHarness _harness;

		[TestInitialize]
		public void Setup() {
			_harness=new FacadeHarness();
		}

		[TestCleanup]
		public void Cleanup() {
			_harness.Dispose();
		}

		[TestMethod]
		public void SeriesOfTwo_RestoredOnceAfterTheLast() {
			IntPtr hwndMain=new IntPtr(0x100);
			_harness.Foreground.HashSetOwnWindows.Add(hwndMain);
			_harness.Foreground.HwndForeground=hwndMain;
			for(int i=0;i<2;i++) {
				_harness.Helper.ListExposures.Clear();
				_harness.Helper.ListExposures.Add(SessionHarness.Exposure(8));
				Assert.IsTrue(EZTwain.OpenSource(_harness.Helper.ProductName));
				IntPtr hdib=EZTwain.Acquire(IntPtr.Zero);
				Assert.AreNotEqual(IntPtr.Zero,hdib,EZTwain.LastErrorText());
				EZTwain.DIB_Free(hdib);
				Assert.IsNotNull(_harness.Scheduler.ActionPending);
				_harness.Foreground.HwndForeground=IntPtr.Zero;
			}
			Assert.AreEqual(1,_harness.Scheduler.CountCancel,"The second acquire cancelled the first restore.");
			Assert.AreEqual(0,_harness.Foreground.ListSetForeground.Count);
			_harness.Scheduler.Elapse();
			CollectionAssert.AreEqual(new List<IntPtr> { hwndMain },_harness.Foreground.ListSetForeground);
		}

		[TestMethod]
		public void Scans_DoNotTouchTheForeground() {
			IntPtr hwndMain=new IntPtr(0x100);
			_harness.Foreground.HashSetOwnWindows.Add(hwndMain);
			_harness.Foreground.HwndForeground=hwndMain;
			_harness.Fake.ListPages.Add(FakePage.CreateGray8(8,4,300,(x,y) => (byte)x));
			Assert.IsTrue(EZTwain.OpenDefaultSource());
			EZTwain.DIB_Free(EZTwain.Acquire(IntPtr.Zero));
			Assert.IsNull(_harness.Scheduler.ActionPending);
		}
	}
}
