// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using ImagingDeviceManager.TwainProtocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingDeviceManager.Tests {
	///<summary>The optional acquire timeout (design 7.1, [Twain] AcquireTimeoutMinutes): honoured only for XFERCOUNT=1 by-name
	///acquisitions of a source in DisableIsSafeSources whose safe-disable MSG_SET succeeded; on expiry, the safe stop.</summary>
	[TestClass]
	public class TwainTimeoutTests {
		private SessionHarness _harness;

		[TestInitialize]
		public void Setup() {
			_harness=new SessionHarness();
			_harness.Session.Settings=new TwainCaptureSettings {
				SafeDisableCap=FakeHelperSource.CapSafeDisable,
				ListDisableIsSafeSources=new List<string> { _harness.Helper.ProductName },
				ListRefusedDisableCodes=new List<ushort> { FakeHelperSource.CodeDisableImagesPending,FakeHelperSource.CodeDisableClosing },
				AcquireTimeout=TimeSpan.FromMilliseconds(300),//Minutes in capture.ini; short here.
				RefusedDisableWait=TimeSpan.FromMilliseconds(2000),
			};
		}

		[TestCleanup]
		public void Cleanup() {
			_harness.Dispose();
		}

		private FakeHelperSource Helper => _harness.Helper;
		private TwainSession Session => _harness.Session;

		[TestMethod]
		public void Timeout_Honoured_StopsInTheSafeOrder_NothingLost() {
			Helper.ListExposures.Add(SessionHarness.Exposure(8));
			Helper.EndAfterMs=60000;//The helper still reads out the image when the timeout expires.
			Stopwatch stopwatch=Stopwatch.StartNew();
			Assert.IsNull(_harness.AcquireByName());
			Assert.IsTrue(stopwatch.ElapsedMilliseconds>=290,"Not before the timeout: "+stopwatch.ElapsedMilliseconds+" ms");
			Assert.AreEqual(EZTwainErrorCode.EZTEC_USER_CANCEL,Session.ErrorCode,"The series ends silently.");
			StringAssert.Contains(Session.ErrorText,"AcquireTimeoutMinutes");
			Assert.AreEqual(TwainStopReason.Timeout,Session.LastStopReason);
			Assert.AreEqual(1,_harness.Sink.ListDrained.Count,"The source refused the disable, then offered the image.");
			Assert.AreEqual(TwainStopReason.Timeout,_harness.Sink.ListDrained[0].StopReason);
			Assert.AreEqual(0,Helper.CountLostForHost);
			_harness.AssertDisabledOnlyInState5();
		}

		[TestMethod]
		public void Timeout_ImageInTime_GoesToTheCaller() {
			Helper.ListExposures.Add(SessionHarness.Exposure(8));
			Helper.EndAfterMs=50;
			Assert.IsNotNull(_harness.AcquireByName(),Session.ErrorText);
			Assert.AreEqual(TwainStopReason.None,Session.LastStopReason);
			System.Threading.Thread.Sleep(350);
			Helper.EndAfterMs=0;
			Assert.IsNotNull(_harness.AcquireByName(),"Each enable starts its own timeout.");
		}

		[TestMethod]
		public void Timeout_NotHonoured_WhenTheSafeDisableCapabilityIsUnsupported() {
			Helper.SupportsSafeDisable=false;
			Helper.ListExposures.Add(SessionHarness.Exposure(8));
			Helper.EndAfterMs=700;
			Assert.IsNotNull(_harness.AcquireByName(),"No stop at 300 ms: the image arrives at 700 ms.");
			Assert.IsTrue(_harness.HasLog("AcquireTimeoutMinutes is not honoured"),_harness.LogText);
			Assert.IsTrue(_harness.HasLog("did not accept the safe-disable capability"),_harness.LogText);
		}

		[TestMethod]
		public void Timeout_NotHonoured_InBatchMode() {
			Helper.ListExposures.Add(SessionHarness.Exposure(8));
			Helper.EndAfterMs=700;
			Assert.IsTrue(Session.OpenSource(Helper.ProductName));
			Session.MultiTransfer=true;
			Assert.IsNotNull(Session.Acquire(IntPtr.Zero),Session.ErrorText);
			Assert.IsTrue(_harness.HasLog("single acquisitions"),_harness.LogText);
			Session.CloseSource();
		}

		[TestMethod]
		public void Timeout_NotHonoured_WhenTheSourceRefusesXferCountOne() {
			Helper.RefusesXferCount=true;//The count stays -1: the operator ends the session, with any number of images.
			Helper.ListExposures.Add(SessionHarness.Exposure(8));
			Helper.ListExposures.Add(SessionHarness.Exposure(9));
			Helper.EndAfterMs=700;
			Assert.IsNotNull(_harness.AcquireByName(),"No stop at 300 ms: the session ends at 700 ms. "+Session.ErrorText);
			Assert.AreEqual(TwainStopReason.None,Session.LastStopReason);
			Assert.IsTrue(_harness.HasLog("did not accept CAP_XFERCOUNT=1"),_harness.LogText);
		}

		[TestMethod]
		public void Timeout_NotHonoured_ForASourceNotListed() {
			Session.Settings.ListDisableIsSafeSources=new List<string> { "Other Source" };
			Helper.ListExposures.Add(SessionHarness.Exposure(8));
			Helper.EndAfterMs=700;
			Assert.IsNotNull(_harness.AcquireByName());
			Assert.IsTrue(_harness.HasLog("not listed in [Twain] DisableIsSafeSources"),_harness.LogText);
		}

		[TestMethod]
		public void Timeout_Zero_IsNone() {
			Session.Settings.AcquireTimeout=TimeSpan.Zero;
			Helper.ListExposures.Add(SessionHarness.Exposure(8));
			Helper.EndAfterMs=500;
			Assert.IsNotNull(_harness.AcquireByName());
			Assert.IsFalse(_harness.HasLog("AcquireTimeoutMinutes"),_harness.LogText);
		}
	}
}
