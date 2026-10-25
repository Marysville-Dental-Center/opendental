// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using ImagingDeviceManager.Imaging;
using ImagingDeviceManager.TwainProtocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingDeviceManager.Tests {
	///<summary>Stops the library starts itself (design 7.1): Cancel, the safe stop order (state 7 MSG_ENDXFER, state 6 drain to the
	///sink instead of MSG_RESET, state 5 MSG_DISABLEDS), and extra images in single mode.</summary>
	[TestClass]
	public class TwainStopTests {
		private SessionHarness _harness;

		[TestInitialize]
		public void Setup() {
			_harness=new SessionHarness();
		}

		[TestCleanup]
		public void Cleanup() {
			_harness.Dispose();
		}

		private FakeHelperSource Helper => _harness.Helper;
		private TwainSession Session => _harness.Session;
		private RecordingDrainSink Sink => _harness.Sink;

		#region Cancel
		[TestMethod]
		public void Cancel_FromAnotherThread_StopsTheWaitAndDisablesInState5() {
			Helper.End=FakeHelperEnd.Never;//Nobody exposes and nobody closes the helper.
			Thread thread=new Thread(() => {
				Thread.Sleep(100);
				Session.Cancel();
			});
			thread.Start();
			Assert.IsNull(_harness.AcquireByName());
			thread.Join();
			Assert.AreEqual(EZTwainErrorCode.EZTEC_USER_CANCEL,Session.ErrorCode,"Callers end the series silently.");
			Assert.AreEqual(TwainStopReason.Cancel,Session.LastStopReason);
			_harness.AssertDisabledOnlyInState5();
			Assert.AreEqual(3,Helper.State,"Disabled and closed.");
		}

		[TestMethod]
		public void Cancel_FromTheLoopsOwnThread_LikeAStatusWindowButton() {
			Helper.End=FakeHelperEnd.Never;
			using(System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer()) {
				timer.Interval=50;
				timer.Tick+=(sender,e) => {
					timer.Stop();
					Session.Cancel();//Runs inside the session's nested message loop.
				};
				timer.Start();
				Assert.IsNull(_harness.AcquireByName());
			}
			Assert.AreEqual(TwainStopReason.Cancel,Session.LastStopReason);
			_harness.AssertDisabledOnlyInState5();
		}

		[TestMethod]
		public void Cancel_SourceThatNeverAnswers_Works() {
			_harness.Fake.EnableBehavior=FakeEnableBehavior.Nothing;
			_harness.Fake.ListPages.Add(FakePage.CreateRgb(4,4,300,(x,y) => Color.Gray));
			_harness.Fake.ActionOnEnable=() => new Thread(() => {
				Thread.Sleep(50);
				Session.Cancel();
			}).Start();
			Assert.IsTrue(Session.OpenSource("Fake Sensor"));
			Assert.IsNull(Session.Acquire(IntPtr.Zero));
			Assert.AreEqual(EZTwainErrorCode.EZTEC_USER_CANCEL,Session.ErrorCode);
			Assert.IsFalse(_harness.Fake.IsEnabled);
		}

		[TestMethod]
		public void Cancel_WhenNothingRuns_IsIgnored() {
			Session.Cancel();
			Helper.ListExposures.Add(SessionHarness.Exposure(8));
			Assert.IsNotNull(_harness.AcquireByName(),"A Cancel from before the acquisition does not count.");
			Assert.AreEqual(TwainStopReason.None,Session.LastStopReason);
		}

		[TestMethod]
		public void Cancel_ImageAlreadyReady_ByName_ImageGoesToTheSinkNotLost() {
			Helper.ListExposures.Add(SessionHarness.Exposure(8));
			Helper.ActionOnEnable=() => Session.Cancel();//The image is ready before the loop sees the Cancel.
			Assert.IsNull(_harness.AcquireByName(),"After a Cancel nothing goes back to the caller.");
			Assert.AreEqual(1,Sink.ListDrained.Count,"The image the source already had is kept.");
			Assert.AreEqual(TwainDrainReason.Stop,Sink.ListDrained[0].Reason);
			Assert.AreEqual(TwainStopReason.Cancel,Sink.ListDrained[0].StopReason);
			Assert.AreEqual(Helper.ProductName,Sink.ListDrained[0].SourceName);
			Assert.AreEqual(8,Sink.ListDrained[0].Image.Width);
			Assert.AreEqual(1,Helper.CountDelivered);
			Assert.AreEqual(0,Helper.CountLostForHost);
			Assert.AreEqual(0,_harness.Fake.CallsNamed("RESET").Count);
			_harness.AssertDisabledOnlyInState5();
		}

		[TestMethod]
		public void Cancel_WithoutSafeDisable_DisableBlocksWhileTheHelperCloses_ImageNotDelivered() {
			//Why the library starts no disable of its own without safe disable (design 7.6): the source closes its helper, blocking the
			//call, and an image it was still reading out is not delivered to the host.
			Helper.ListExposures.Add(SessionHarness.Exposure(8));
			Helper.EndAfterMs=60000;
			Helper.BlockingCloseMs=150;
			Helper.ActionOnEnable=() => Session.Cancel();
			System.Diagnostics.Stopwatch stopwatch=System.Diagnostics.Stopwatch.StartNew();
			Assert.IsNull(_harness.AcquireByName());
			Assert.IsTrue(stopwatch.ElapsedMilliseconds>=140,"MSG_DISABLEDS blocked while the helper closed.");
			Assert.IsFalse(Session.IsSafeDisableInEffect);
			Assert.AreEqual(1,Helper.CountLostForHost);
			Assert.AreEqual(0,Sink.ListDrained.Count);
			Assert.AreEqual(1,_harness.Fake.ListDisableStates.Count,"Accepted at once: no wait.");
		}

		[TestMethod]
		public void Cancel_ImageAlreadyReady_NoSink_DisabledAsBefore() {
			Session.DrainSink=null;
			Helper.ListExposures.Add(SessionHarness.Exposure(8));
			Helper.ActionOnEnable=() => Session.Cancel();
			Assert.IsNull(_harness.AcquireByName());
			Assert.AreEqual(1,Helper.CountLostForHost,"Without a sink the library does not transfer what it cannot keep.");
		}
		#endregion Cancel

		#region Safe stop order
		[TestMethod]
		public void CloseSource_ByNameWithPendingImages_DrainedInsteadOfReset() {
			for(int i=0;i<3;i++) {
				Helper.ListExposures.Add(SessionHarness.Exposure(8+i));
			}
			Assert.IsTrue(Session.OpenSource(Helper.ProductName));
			Session.MultiTransfer=true;
			DibImage dib=Session.Acquire(IntPtr.Zero);
			Assert.AreEqual(8,dib.Width);
			Assert.IsFalse(Session.IsDone(),"Two images are pending (state 6).");
			Session.CloseSource();
			Assert.AreEqual(0,_harness.Fake.CallsNamed("RESET").Count,"No MSG_RESET.");
			CollectionAssert.AreEqual(new[] { 9,10 },Sink.ListDrained.Select(x => x.Image.Width).ToArray());
			Assert.IsTrue(Sink.ListDrained.All(x => x.Reason==TwainDrainReason.Stop && x.StopReason==TwainStopReason.Closed));
			Assert.AreEqual(TwainStopReason.Closed,Session.LastStopReason);
			Assert.AreEqual(3,Helper.CountDelivered);
			_harness.AssertDisabledOnlyInState5();
		}

		[TestMethod]
		public void CloseSource_DefaultSourceWithPendingImages_ResetAsBefore() {
			_harness.Fake.DefaultSource=Helper.ProductName;
			for(int i=0;i<3;i++) {
				Helper.ListExposures.Add(SessionHarness.Exposure(8+i));
			}
			Assert.IsTrue(Session.OpenSource(null));
			Session.MultiTransfer=true;
			Assert.IsNotNull(Session.Acquire(IntPtr.Zero));
			Session.CloseSource();
			Assert.AreEqual(1,_harness.Fake.CallsNamed("RESET").Count,"Scans keep MSG_RESET.");
			Assert.AreEqual(0,Sink.ListDrained.Count);
			Assert.AreEqual(2,Helper.CountKeptForRedelivery);
		}

		[TestMethod]
		public void ExceptionInState7_EndXferThenDrainThenDisable() {
			FakeTwainDsm fake=_harness.Fake;
			for(int i=0;i<3;i++) {
				fake.ListPages.Add(FakePage.CreateRgb(10+i,20,300,(x,y) => Color.Gray));
			}
			bool hasThrown=false;
			fake.ActionOnMemoryStrip=row => {
				if(row>0 && !hasThrown) {
					hasThrown=true;
					throw new InvalidOperationException("Simulated failure in the middle of a transfer.");
				}
			};
			Assert.IsTrue(Session.OpenSource("Fake Sensor"));
			Assert.IsTrue(Session.SetTransferMechanism(Tw.TWSX_MEMORY));
			Assert.IsNull(Session.Acquire(IntPtr.Zero));
			Assert.AreEqual(EZTwainErrorCode.EZTEC_INTERNAL,Session.ErrorCode);
			Assert.AreEqual(TwainStopReason.Error,Session.LastStopReason);
			int idxEnd=_harness.IndexOfCall(x => x.Name=="ENDXFER");
			Assert.AreEqual(7,fake.ListCalls[idxEnd].StateBefore,"State 7: the transfer is ended with MSG_ENDXFER.\r\n"+_harness.CallText);
			Assert.IsTrue(fake.ListCalls.Take(idxEnd).All(x => x.Name!="DISABLEDS" && x.Name!="RESET"));
			CollectionAssert.AreEqual(new[] { 11,12 },Sink.ListDrained.Select(x => x.Image.Width).ToArray(),"State 6: the others are drained.");
			Assert.AreEqual(0,fake.CallsNamed("RESET").Count);
			_harness.AssertDisabledOnlyInState5();
		}

		[TestMethod]
		public void StrictGenericSource_NeverDisabledOutsideState5() {
			FakeTwainDsm fake=_harness.Fake;
			fake.IsStrict=true;
			for(int i=0;i<3;i++) {
				fake.ListPages.Add(FakePage.CreateRgb(6+i,4,300,(x,y) => Color.Gray));
			}
			//A scan with extra pages: MSG_RESET, then the disable.
			Assert.IsTrue(Session.OpenSource(null));
			Assert.IsNotNull(Session.Acquire(IntPtr.Zero));
			//A by-name batch closed with images pending: drained, then the disable.
			Assert.IsTrue(Session.OpenSource("Fake Sensor"));
			Session.MultiTransfer=true;
			Assert.IsNotNull(Session.Acquire(IntPtr.Zero));
			Session.CloseSource();
			Assert.AreEqual(2,Sink.ListDrained.Count);
			//A by-name single acquisition with extra images.
			Assert.IsTrue(Session.OpenSource("Fake Sensor"));
			Assert.IsNotNull(Session.Acquire(IntPtr.Zero));
			Assert.AreEqual(4,Sink.ListDrained.Count);
			//A Cancel while the source says nothing.
			fake.EnableBehavior=FakeEnableBehavior.Nothing;
			fake.ActionOnEnable=() => Session.Cancel();
			Assert.IsTrue(Session.OpenSource("Fake Sensor"));
			Assert.IsNull(Session.Acquire(IntPtr.Zero));
			Assert.AreEqual(4,fake.ListDisableStates.Count);
			_harness.AssertDisabledOnlyInState5();
			Assert.IsFalse(fake.ListLog.Contains("DISABLEDS refused"));
		}

		[TestMethod]
		public void TransferFailure_ByName_RemainingImagesDrained() {
			for(int i=0;i<3;i++) {
				Helper.ListExposures.Add(SessionHarness.Exposure(8+i));
			}
			Helper.ListExposures[1].FailCondition=Tw.TWCC_LOWMEMORY;
			Assert.IsTrue(Session.OpenSource(Helper.ProductName));
			Session.MultiTransfer=true;
			Assert.IsNotNull(Session.Acquire(IntPtr.Zero));
			Assert.IsNull(Session.Acquire(IntPtr.Zero));
			Assert.AreEqual(EZTwainErrorCode.EZTEC_XFER_FAILURE,Session.ErrorCode);
			Assert.AreEqual(1,Sink.ListDrained.Count,"The image after the failed one is kept.");
			Assert.AreEqual(10,Sink.ListDrained[0].Image.Width);
			Assert.AreEqual(1,Helper.CountKeptForRedelivery,"The failed image stays undelivered in the source.");
			_harness.AssertDisabledOnlyInState5();
		}
		#endregion Safe stop order

		#region Extra images in single mode
		[TestMethod]
		public void SingleMode_ByName_SourceIgnoresXferCount_ExtrasDrainedWithWarning() {
			Helper.Mode=FakeHelperMode.Multi;//An installation setting turns CAP_XFERCOUNT=1 into a batch.
			for(int i=0;i<3;i++) {
				Helper.ListExposures.Add(SessionHarness.Exposure(8+i));
			}
			DibImage dib=_harness.AcquireByName();
			Assert.IsNotNull(dib,Session.ErrorText);
			Assert.AreEqual(8,dib.Width,"The first image goes to the caller.");
			Assert.AreEqual(EZTwainErrorCode.EZTEC_NONE,Session.ErrorCode);
			Assert.AreEqual(0,_harness.Fake.CallsNamed("RESET").Count,"The batch is not reset.");
			CollectionAssert.AreEqual(new[] { 9,10 },Sink.ListDrained.Select(x => x.Image.Width).ToArray());
			Assert.IsTrue(Sink.ListDrained.All(x => x.Reason==TwainDrainReason.ExtraImage && x.StopReason==TwainStopReason.None));
			Assert.IsTrue(_harness.HasLog("Warning"),_harness.LogText);
			Assert.AreEqual(2,Session.DrainedImageCount);
			_harness.AssertDisabledOnlyInState5();
		}

		[TestMethod]
		public void SingleMode_ByName_NoSink_ExtrasResetAsBefore() {
			Session.DrainSink=null;
			Helper.Mode=FakeHelperMode.Multi;
			Helper.ListExposures.Add(SessionHarness.Exposure(8));
			Helper.ListExposures.Add(SessionHarness.Exposure(9));
			Assert.IsNotNull(_harness.AcquireByName());
			Assert.AreEqual(1,_harness.Fake.CallsNamed("RESET").Count);
			Assert.AreEqual(1,Helper.CountKeptForRedelivery,"MSG_RESET leaves the extra image undelivered in the source.");
		}

		[TestMethod]
		public void SinkThatThrows_IsLogged_AndTheStopGoesOn() {
			Sink.IsThrowing=true;
			Helper.Mode=FakeHelperMode.Multi;
			Helper.ListExposures.Add(SessionHarness.Exposure(8));
			Helper.ListExposures.Add(SessionHarness.Exposure(9));
			Helper.ListExposures.Add(SessionHarness.Exposure(10));
			Assert.IsNotNull(_harness.AcquireByName());
			Assert.AreEqual(2,Sink.ListDrained.Count,"Every extra image is still offered to the sink.");
			Assert.IsTrue(_harness.HasLog("The drain sink failed"),_harness.LogText);
			_harness.AssertDisabledOnlyInState5();
		}
		#endregion Extra images in single mode
	}
}
