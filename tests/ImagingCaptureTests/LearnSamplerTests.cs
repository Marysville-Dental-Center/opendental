// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ImagingCapture.Camera;
using ImagingCapture.UI;
using ImagingCapture.Uvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingCapture.Tests {
	///<summary>LearnSampler with FakeUvcBackend: which controls it reads, the samples and still times it raises, and that nothing is
	///raised after Stop. Polls are driven by the test with a manual clock, except in the one thread test. Controls are made up.</summary>
	[TestClass]
	public class LearnSamplerTests {
		private static readonly ControlId _controlZoom=new ControlId(UvcControlInterface.CameraControl,3);
		private static readonly ControlId _controlHue=new ControlId(UvcControlInterface.VideoProcAmp,2);
		private FakeFrameSource _fakeFrameSource;
		private LearnSampler _learnSampler;
		private long _nowMs;
		private List<IReadOnlyList<ControlId>> _listControlsFound;
		private List<ControlSample> _listSamples;
		private List<long> _listStillTimesMs;
		private List<UvcErrorEventArgs> _listErrors;

		[TestInitialize]
		public void Init() {
			FakeUvcBackend fakeUvcBackend=new FakeUvcBackend();
			fakeUvcBackend.HasStillPin=true;
			_fakeFrameSource=(FakeFrameSource)fakeUvcBackend.Open(fakeUvcBackend.AddDevice());
			_fakeFrameSource.Start(null,true);
			_fakeFrameSource.SetControlValue(_controlZoom.Interface,_controlZoom.Property,7);
			_fakeFrameSource.SetControlValue(_controlHue.Interface,_controlHue.Property,-3);
			_nowMs=5000;
			_listControlsFound=new List<IReadOnlyList<ControlId>>();
			_listSamples=new List<ControlSample>();
			_listStillTimesMs=new List<long>();
			_listErrors=new List<UvcErrorEventArgs>();
		}

		[TestCleanup]
		public void Cleanup() {
			_learnSampler?.Dispose();
		}

		private LearnSampler MakeSampler(IEnumerable<ControlId> controls,bool usePollThread=false) {
			_learnSampler=new LearnSampler(_fakeFrameSource,50,controls,() => _nowMs,null,usePollThread);
			_learnSampler.ControlsFound+=(sender,e) => _listControlsFound.Add(e.Controls);
			_learnSampler.SampleTaken+=(sender,e) => _listSamples.Add(e.Sample);
			_learnSampler.StillSampleSeen+=(sender,e) => _listStillTimesMs.Add(e.TimeMs);
			_learnSampler.CameraError+=(sender,e) => _listErrors.Add(e);
			return _learnSampler;
		}

		[TestMethod]
		public void FirstPoll_FindsReadableControlsOnce() {
			MakeSampler(null).Start();
			Assert.IsNull(_learnSampler.Controls);
			_learnSampler.PollOnce();
			_nowMs+=50;
			_learnSampler.PollOnce();
			Assert.AreEqual(1,_listControlsFound.Count);
			CollectionAssert.AreEqual(new[] { _controlZoom,_controlHue },_listControlsFound[0].ToArray(),"Every standard property is tried; only these read.");
			CollectionAssert.AreEqual(new[] { _controlZoom,_controlHue },_learnSampler.Controls.ToArray());
			Assert.AreEqual(2,_listSamples.Count);
			Assert.AreEqual(5000,_listSamples[0].TimeMs);
			Assert.AreEqual(5050,_listSamples[1].TimeMs);
			int value;
			Assert.IsTrue(_listSamples[1].TryGetValue(_controlHue,out value));
			Assert.AreEqual(-3,value);
			//An unsupported property is tried once (by FindReadable), then never again.
			Assert.AreEqual(1,_fakeFrameSource.GetReadCount(UvcControlInterface.CameraControl,0));
			Assert.AreEqual(3,_fakeFrameSource.GetReadCount(_controlZoom.Interface,_controlZoom.Property));
		}

		[TestMethod]
		public void GivenControls_OnlyThoseAreRead() {
			MakeSampler(new[] { _controlHue }).Start();
			_learnSampler.PollOnce();
			Assert.AreEqual(1,_listControlsFound.Count);
			CollectionAssert.AreEqual(new[] { _controlHue },_listControlsFound[0].ToArray());
			Assert.AreEqual(0,_fakeFrameSource.GetReadCount(_controlZoom.Interface,_controlZoom.Property));
			Assert.AreEqual(1,_fakeFrameSource.GetReadCount(_controlHue.Interface,_controlHue.Property));
		}

		[TestMethod]
		public void NoReadableControl_StillGivesTimedSamples() {
			_fakeFrameSource.SetControlStatus(_controlZoom.Interface,_controlZoom.Property,UvcControlStatus.Unsupported);
			_fakeFrameSource.SetControlStatus(_controlHue.Interface,_controlHue.Property,UvcControlStatus.Unsupported);
			MakeSampler(null).Start();
			_learnSampler.PollOnce();
			_nowMs+=50;
			_learnSampler.PollOnce();
			Assert.AreEqual(0,_listControlsFound[0].Count);
			Assert.AreEqual(2,_listSamples.Count);
			Assert.AreEqual(0,_listSamples[0].Values.Count);
		}

		[TestMethod]
		public void FailedRead_LeavesTheControlOutOfTheSample() {
			MakeSampler(null).Start();
			_learnSampler.PollOnce();
			_fakeFrameSource.SetControlStatus(_controlZoom.Interface,_controlZoom.Property,UvcControlStatus.DeviceLost);
			_learnSampler.PollOnce();
			int value;
			Assert.IsFalse(_listSamples[1].TryGetValue(_controlZoom,out value));
			Assert.IsTrue(_listSamples[1].TryGetValue(_controlHue,out value));
		}

		[TestMethod]
		public void StillSamplesAndErrors_AreRaisedWithTheClock() {
			MakeSampler(null).Start();
			_nowMs=6100;
			_fakeFrameSource.PushStill(4,4,(x,y) => 0);
			_fakeFrameSource.RaiseError(UvcErrorKind.DeviceLost);
			CollectionAssert.AreEqual(new long[] { 6100 },_listStillTimesMs);
			Assert.AreEqual(1,_listErrors.Count);
			Assert.AreEqual(UvcErrorKind.DeviceLost,_listErrors[0].Kind);
		}

		[TestMethod]
		public void AfterStop_NothingIsReadOrRaised() {
			MakeSampler(null).Start();
			_learnSampler.PollOnce();
			_learnSampler.Stop();
			int countReads=_fakeFrameSource.GetReadCount(_controlZoom.Interface,_controlZoom.Property);
			_learnSampler.PollOnce();
			_fakeFrameSource.PushStill(4,4,(x,y) => 0);
			_fakeFrameSource.RaiseError(UvcErrorKind.ErrorAbort);
			Assert.AreEqual(countReads,_fakeFrameSource.GetReadCount(_controlZoom.Interface,_controlZoom.Property));
			Assert.AreEqual(1,_listSamples.Count);
			Assert.AreEqual(0,_listStillTimesMs.Count);
			Assert.AreEqual(0,_listErrors.Count);
			_learnSampler.Dispose();
			TestUtil.AssertThrows<ObjectDisposedException>(() => _learnSampler.Start());
		}

		[TestMethod]
		public void PollMs_HeldToTheConfigRange() {
			Assert.AreEqual(CameraConfig.PollMsMin,new LearnSampler(_fakeFrameSource,1,null,() => 0,null,false).PollMs);
			Assert.AreEqual(CameraConfig.PollMsMax,new LearnSampler(_fakeFrameSource,100000,null,() => 0,null,false).PollMs);
			Assert.AreEqual(50,new LearnSampler(_fakeFrameSource,50,null,() => 0,null,false).PollMs);
		}

		[TestMethod]
		public void PollThread_DeliversSamples() {
			MakeSampler(null,usePollThread:true);
			using(ManualResetEvent manualResetEvent=new ManualResetEvent(false)) {
				int countSamples=0;
				_learnSampler.SampleTaken+=(sender,e) => {
					if(Interlocked.Increment(ref countSamples)>=3) {
						manualResetEvent.Set();
					}
				};
				_learnSampler.Start();
				Assert.IsTrue(manualResetEvent.WaitOne(5000),"Three samples within 5 s at a 50 ms poll.");
				_learnSampler.Stop();
			}
			Assert.IsFalse(_learnSampler.IsRunning);
		}
	}
}
