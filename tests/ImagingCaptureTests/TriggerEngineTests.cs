// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ImagingCapture.Camera;
using ImagingCapture.Uvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingCapture.Tests {
	///<summary>TriggerEngine with FakeUvcBackend: scripted control values and still samples, a manual clock, and polls driven by the
	///test. The controls, bits and levels are made up; they describe no real camera.</summary>
	[TestClass]
	public class TriggerEngineTests {
		private static readonly ControlId _controlKey=new ControlId(UvcControlInterface.CameraControl,3);
		private static readonly ControlId _controlOther=new ControlId(UvcControlInterface.VideoProcAmp,8);
		private FakeUvcBackend _fakeUvcBackend;
		private FakeFrameSource _fakeFrameSource;
		private TriggerEngine _triggerEngine;
		private long _nowMs;
		private List<TriggerPressEventArgs> _listPresses;
		private List<TriggerPressEventArgs> _listIgnored;
		private List<LevelChangedEventArgs> _listLevelChanges;
		private List<string> _listWarnings;

		[TestInitialize]
		public void Init() {
			_fakeUvcBackend=new FakeUvcBackend();
			_fakeUvcBackend.HasStillPin=true;
			UvcDeviceInfo device=_fakeUvcBackend.AddDevice();
			_fakeFrameSource=(FakeFrameSource)_fakeUvcBackend.Open(device);
			_fakeFrameSource.Start(null,true);
			_nowMs=1000;
			_listPresses=new List<TriggerPressEventArgs>();
			_listIgnored=new List<TriggerPressEventArgs>();
			_listLevelChanges=new List<LevelChangedEventArgs>();
			_listWarnings=new List<string>();
		}

		[TestCleanup]
		public void Cleanup() {
			_triggerEngine?.Dispose();
		}

		///<summary>A configuration with the control trigger on _controlKey.</summary>
		private static CameraConfig MakeConfig(int mask=0x1,ControlLevel activeLevel=ControlLevel.High,bool isStillEnabled=false,int? debounceMs=300) {
			CameraConfig cameraConfig=new CameraConfig();
			cameraConfig.DebounceMs=debounceMs;
			cameraConfig.StillEnabled=isStillEnabled;
			cameraConfig.ControlPoll.Enabled=true;
			cameraConfig.ControlPoll.SetBinding(new ControlBinding(_controlKey,mask,activeLevel));
			return cameraConfig;
		}

		private static LevelInputConfig MakeLevel(int number,ControlId control,int mask,ControlLevel activeLevel) {
			LevelInputConfig levelInputConfig=new LevelInputConfig(number) { Enabled=true };
			levelInputConfig.SetBinding(new ControlBinding(control,mask,activeLevel));
			return levelInputConfig;
		}

		private void StartEngine(CameraConfig cameraConfig,SynchronizationContext synchronizationContext=null) {
			_triggerEngine=new TriggerEngine(_fakeFrameSource,cameraConfig,() => _nowMs,synchronizationContext,false);
			_triggerEngine.Pressed+=(sender,e) => _listPresses.Add(e);
			_triggerEngine.PressIgnored+=(sender,e) => _listIgnored.Add(e);
			_triggerEngine.LevelChanged+=(sender,e) => _listLevelChanges.Add(e);
			_triggerEngine.Warning+=(sender,e) => _listWarnings.Add(e.Message);
			_triggerEngine.Start();
		}

		///<summary>Sets the key control's value, advances the clock and polls once.</summary>
		private void Step(int value,int elapsedMs=50) {
			_fakeFrameSource.SetControlValue(_controlKey.Interface,_controlKey.Property,value);
			_nowMs+=elapsedMs;
			_triggerEngine.PollOnce();
		}

		private void Steps(params int[] arrayValues) {
			foreach(int value in arrayValues) {
				Step(value);
			}
		}

		private void PushStill() {
			_fakeFrameSource.PushStill(2,2,(x,y) => 0x808080);
		}

		[TestMethod]
		public void ActiveHigh_OneEdgeIsOnePress() {
			StartEngine(MakeConfig());
			Steps(0,0,1,1,1,1,1,1,1,1,0,0);
			Assert.AreEqual(1,_listPresses.Count,"Holding the key does not repeat.");
			Assert.AreEqual(TriggerSource.ControlPoll,_listPresses[0].Source);
			Assert.AreEqual(1150,_listPresses[0].TimeMs);
			Assert.IsNull(_listPresses[0].StillFrame);
			Assert.AreEqual(TriggerInputState.Armed,_triggerEngine.ControlPollState);
			Assert.AreEqual(0,_listWarnings.Count);
		}

		[TestMethod]
		public void ActiveLow_PressIsTheClearingEdge() {
			StartEngine(MakeConfig(0x10,ControlLevel.Low));
			Steps(0x13,0x13,0x03);
			Assert.AreEqual(1,_listPresses.Count);
			Assert.IsTrue(_triggerEngine.IsControlPollActive);
			Steps(0x03,0x13);
			Assert.AreEqual(1,_listPresses.Count,"Setting the bit again is the release.");
			Assert.IsFalse(_triggerEngine.IsControlPollActive);
		}

		[TestMethod]
		public void FirstReading_NeverFires() {
			StartEngine(MakeConfig());
			Steps(1,1);
			Assert.AreEqual(0,_listPresses.Count,"A key held at start is not a press.");
			Steps(0);
			Assert.AreEqual(0,_listPresses.Count,"Nor is its release.");
			Steps(1);
			Assert.AreEqual(1,_listPresses.Count);
		}

		[TestMethod]
		public void Mask_OtherBitsAreIgnored() {
			StartEngine(MakeConfig(0x4));
			Steps(0x0,0x1,0x3,0x8,0xB);
			Assert.AreEqual(0,_listPresses.Count);
			Steps(0xF,0xC,0x8);
			Step(0x4,300);
			Assert.AreEqual(2,_listPresses.Count,"0xF and, after the lockout, 0x4.");
		}

		[TestMethod]
		public void MultiBitMask_HighNeedsEveryBit() {
			StartEngine(MakeConfig(0x6));
			Steps(0x0,0x2,0x4,0x2);
			Assert.AreEqual(0,_listPresses.Count,"Some mask bits set is not the active level.");
			Steps(0x6);
			Assert.AreEqual(1,_listPresses.Count);
		}

		[TestMethod]
		public void Debounce_OnePressPerLockout() {
			StartEngine(MakeConfig(debounceMs:300));
			Assert.AreEqual(300,_triggerEngine.DebounceMs);
			Step(0);
			Step(1);//t=1100: accepted
			Step(0);
			Step(1);//t=1200: bounce, 100 ms after
			Step(0);
			Step(1);//t=1300: 200 ms after
			Assert.AreEqual(1,_listPresses.Count);
			Step(0,100);//t=1400
			Step(1,50);//t=1450: 350 ms after the accepted press
			Assert.AreEqual(2,_listPresses.Count);
			Assert.AreEqual(1450,_listPresses[1].TimeMs);
		}

		[TestMethod]
		public void Debounce_EmptyValueUses300() {
			CameraConfig cameraConfig=MakeConfig(debounceMs:null);
			StartEngine(cameraConfig);
			Assert.AreEqual(300,_triggerEngine.DebounceMs);
		}

		[TestMethod]
		public void StillSampleAndPollEdgeTogether_OnePhoto() {
			StartEngine(MakeConfig(isStillEnabled:true));
			Assert.AreEqual(TriggerInputState.Armed,_triggerEngine.StillState);
			Step(0);
			_nowMs+=50;
			PushStill();
			Step(1,0);
			Assert.AreEqual(1,_listPresses.Count);
			Assert.AreEqual(TriggerSource.StillPin,_listPresses[0].Source);
			Assert.IsNotNull(_listPresses[0].StillFrame);
			Step(0,500);
			Step(1,0);
			PushStill();
			Assert.AreEqual(2,_listPresses.Count,"The other order: the poll edge first, the still sample dropped.");
			Assert.AreEqual(TriggerSource.ControlPoll,_listPresses[1].Source);
			_nowMs+=500;
			PushStill();
			Assert.AreEqual(3,_listPresses.Count,"A still sample alone after the lockout.");
			Assert.AreEqual(TriggerSource.StillPin,_listPresses[2].Source);
		}

		[TestMethod]
		public void StillPin_EachSampleIsAPress() {
			CameraConfig cameraConfig=new CameraConfig { StillEnabled=true };
			StartEngine(cameraConfig);
			Assert.AreEqual(TriggerInputState.Off,_triggerEngine.ControlPollState);
			for(int i=0;i<5;i++) {
				_nowMs+=1000;
				PushStill();
			}
			Assert.AreEqual(5,_listPresses.Count);
			Assert.IsTrue(_listPresses.All(x => x.Source==TriggerSource.StillPin && x.StillFrame!=null));
			_nowMs+=100;
			PushStill();
			Assert.AreEqual(5,_listPresses.Count,"Within the lockout.");
		}

		[TestMethod]
		public void ConsumerDisabled_PressIsIgnored() {
			bool isEnabled=false;
			StartEngine(MakeConfig());
			_triggerEngine.FuncIsConsumerEnabled=() => isEnabled;
			Steps(0,1);
			Assert.AreEqual(0,_listPresses.Count);
			Assert.AreEqual(1,_listIgnored.Count);
			isEnabled=true;
			Steps(0,1);
			Assert.AreEqual(0,_listPresses.Count,"Bounce right after the window is enabled again is still inside the lockout.");
			Step(0,300);
			Step(1);
			Assert.AreEqual(1,_listPresses.Count);
			_triggerEngine.FuncIsConsumerEnabled=() => { throw new InvalidOperationException(); };
			Step(0,300);
			Step(1);
			Assert.AreEqual(1,_listPresses.Count,"A consumer check that throws counts as disabled.");
			Assert.AreEqual(2,_listIgnored.Count);
		}

		[TestMethod]
		public void UnsupportedProperty_DisablesTheTriggerWithOneWarning() {
			StartEngine(MakeConfig(isStillEnabled:true));
			for(int i=0;i<5;i++) {
				_nowMs+=50;
				_triggerEngine.PollOnce();
			}
			Assert.AreEqual(TriggerInputState.Failed,_triggerEngine.ControlPollState);
			StringAssert.Contains(_triggerEngine.ControlPollProblem,"does not support");
			Assert.AreEqual(1,_listWarnings.Count,string.Join(" | ",_listWarnings));
			StringAssert.Contains(_listWarnings[0],"[Trigger.ControlPoll]");
			Assert.AreEqual(1,_fakeFrameSource.GetReadCount(_controlKey.Interface,_controlKey.Property),"No further reads.");
			_fakeFrameSource.SetControlValue(_controlKey.Interface,_controlKey.Property,1);
			_nowMs+=50;
			_triggerEngine.PollOnce();
			Assert.AreEqual(0,_listPresses.Count);
			_nowMs+=50;
			PushStill();
			Assert.AreEqual(1,_listPresses.Count,"The still trigger keeps working.");
		}

		[TestMethod]
		public void FailedRead_DisablesTheTriggerWithOneWarning() {
			StartEngine(MakeConfig());
			Steps(0,0);
			_fakeFrameSource.SetControlStatus(_controlKey.Interface,_controlKey.Property,UvcControlStatus.Failed);
			for(int i=0;i<3;i++) {
				_nowMs+=50;
				_triggerEngine.PollOnce();
			}
			Assert.AreEqual(TriggerInputState.Failed,_triggerEngine.ControlPollState);
			Assert.AreEqual(1,_listWarnings.Count);
			StringAssert.Contains(_listWarnings[0],"failed");
		}

		[TestMethod]
		public void IncompleteInputs_WarnOnceAtStart() {
			CameraConfig cameraConfig=new CameraConfig { StillEnabled=true };
			cameraConfig.ControlPoll.Enabled=true;
			cameraConfig.ControlPoll.Interface=UvcControlInterface.CameraControl;
			cameraConfig.Levels.Add(new LevelInputConfig(4) { Enabled=true });
			_fakeFrameSource.Dispose();
			_fakeUvcBackend.HasStillPin=false;
			_fakeFrameSource=(FakeFrameSource)_fakeUvcBackend.Open(_fakeUvcBackend.ListDevices[0]);
			_fakeFrameSource.Start(null,true);
			StartEngine(cameraConfig);
			Assert.AreEqual(TriggerInputState.Incomplete,_triggerEngine.StillState);
			StringAssert.Contains(_triggerEngine.StillProblem,"no still pin");
			Assert.AreEqual(TriggerInputState.Incomplete,_triggerEngine.ControlPollState);
			StringAssert.Contains(_triggerEngine.ControlPollProblem,"Property, Mask, ActiveLevel");
			Assert.AreEqual(TriggerInputState.Incomplete,_triggerEngine.GetLevelState(4));
			Assert.AreEqual(3,_listWarnings.Count,string.Join(" | ",_listWarnings));
			_triggerEngine.Stop();
			_triggerEngine.Start();
			Assert.AreEqual(3,_listWarnings.Count,"Not again on a restart.");
			_triggerEngine.PollOnce();
			PushStill();
			Assert.AreEqual(0,_listPresses.Count);
			Assert.AreEqual(TriggerInputState.Off,_triggerEngine.GetLevelState(9));
		}

		[TestMethod]
		public void NothingEnabled_NothingIsRead() {
			StartEngine(new CameraConfig());
			_triggerEngine.PollOnce();
			PushStill();
			Assert.AreEqual(TriggerInputState.Off,_triggerEngine.StillState);
			Assert.AreEqual(TriggerInputState.Off,_triggerEngine.ControlPollState);
			Assert.AreEqual(0,_fakeFrameSource.GetReadCount(_controlKey.Interface,_controlKey.Property));
			Assert.AreEqual(0,_listPresses.Count);
			Assert.AreEqual(0,_listWarnings.Count);
		}

		[TestMethod]
		public void Level_PausePreviewWhileActive_SharedControlReadOnce() {
			CameraConfig cameraConfig=MakeConfig(0x1);
			cameraConfig.Levels.Add(MakeLevel(1,_controlKey,0x8,ControlLevel.High));
			StartEngine(cameraConfig);
			Assert.AreEqual(TriggerInputState.Armed,_triggerEngine.GetLevelState(1));
			Steps(0x0,0x8);
			Assert.AreEqual(1,_listLevelChanges.Count);
			Assert.IsTrue(_listLevelChanges[0].IsActive);
			Assert.IsTrue(_listLevelChanges[0].IsPausePreviewActive);
			Assert.AreEqual(LevelAction.PausePreview,_listLevelChanges[0].Action);
			Assert.IsTrue(_triggerEngine.IsPausePreviewActive);
			Assert.IsTrue(_triggerEngine.IsLevelActive(1));
			Steps(0x8,0x0);
			Assert.AreEqual(2,_listLevelChanges.Count);
			Assert.IsFalse(_listLevelChanges[1].IsActive);
			Assert.IsFalse(_triggerEngine.IsPausePreviewActive);
			Assert.AreEqual(0,_listPresses.Count,"The level bit is not the key.");
			Steps(0x1);
			Assert.AreEqual(1,_listPresses.Count);
			Assert.AreEqual(5,_fakeFrameSource.GetReadCount(_controlKey.Interface,_controlKey.Property),"One read per poll for both inputs.");
		}

		[TestMethod]
		public void Level_ActiveAtStartPausesAtOnce() {
			CameraConfig cameraConfig=new CameraConfig();
			cameraConfig.Levels.Add(MakeLevel(2,_controlOther,0x2,ControlLevel.Low));
			StartEngine(cameraConfig);
			_fakeFrameSource.SetControlValue(_controlOther.Interface,_controlOther.Property,0x0);
			_triggerEngine.PollOnce();
			Assert.AreEqual(1,_listLevelChanges.Count);
			Assert.AreEqual(2,_listLevelChanges[0].Number);
			Assert.IsTrue(_triggerEngine.IsPausePreviewActive);
		}

		[TestMethod]
		public void Level_FailedReadResumesPreviewWithOneWarning() {
			CameraConfig cameraConfig=new CameraConfig();
			cameraConfig.Levels.Add(MakeLevel(1,_controlOther,0x2,ControlLevel.High));
			StartEngine(cameraConfig);
			_fakeFrameSource.SetControlValue(_controlOther.Interface,_controlOther.Property,0x2);
			_triggerEngine.PollOnce();
			Assert.IsTrue(_triggerEngine.IsPausePreviewActive);
			_fakeFrameSource.SetControlStatus(_controlOther.Interface,_controlOther.Property,UvcControlStatus.DeviceLost);
			_triggerEngine.PollOnce();
			_triggerEngine.PollOnce();
			Assert.IsFalse(_triggerEngine.IsPausePreviewActive);
			Assert.AreEqual(TriggerInputState.Failed,_triggerEngine.GetLevelState(1));
			Assert.AreEqual(2,_listLevelChanges.Count);
			Assert.IsFalse(_listLevelChanges[1].IsActive);
			Assert.AreEqual(1,_listWarnings.Count);
			StringAssert.Contains(_listWarnings[0],"[Level.1]");
		}

		[TestMethod]
		public void Restart_KeyHeldAcrossStopDoesNotFire() {
			StartEngine(MakeConfig());
			Steps(0,1);
			Assert.AreEqual(1,_listPresses.Count);
			_triggerEngine.Stop();
			Assert.IsFalse(_triggerEngine.IsRunning);
			Step(0,1000);
			_triggerEngine.Start();
			Step(1);
			Step(1);
			Assert.AreEqual(1,_listPresses.Count,"The first reading after a restart only sets the state.");
			Steps(0,1);
			Assert.AreEqual(2,_listPresses.Count);
		}

		[TestMethod]
		public void PostedEvents_DroppedAfterStop() {
			QueueSynchronizationContext queueSynchronizationContext=new QueueSynchronizationContext();
			StartEngine(MakeConfig(),queueSynchronizationContext);
			Steps(0,1);
			Assert.AreEqual(0,_listPresses.Count,"Posted, not yet run.");
			queueSynchronizationContext.RunAll();
			Assert.AreEqual(1,_listPresses.Count);
			Step(0,500);
			Step(1);
			_triggerEngine.Stop();
			queueSynchronizationContext.RunAll();
			Assert.AreEqual(1,_listPresses.Count,"A press seen before Stop but delivered after it is dropped.");
		}

		[TestMethod]
		public void PollThread_ReadsAtPollMs() {
			CameraConfig cameraConfig=MakeConfig();
			cameraConfig.PollMs=10;
			_fakeFrameSource.SetControlValue(_controlKey.Interface,_controlKey.Property,0);
			using(ManualResetEvent manualResetEventPressed=new ManualResetEvent(false))
			using(TriggerEngine triggerEngine=new TriggerEngine(_fakeFrameSource,cameraConfig,null,null,true)) {
				triggerEngine.Pressed+=(sender,e) => manualResetEventPressed.Set();
				Assert.AreEqual(10,triggerEngine.PollMs);
				triggerEngine.Start();
				DateTime dateTimeEnd=DateTime.Now.AddSeconds(5);
				while(_fakeFrameSource.GetReadCount(_controlKey.Interface,_controlKey.Property)<3 && DateTime.Now<dateTimeEnd) {
					Thread.Sleep(5);
				}
				_fakeFrameSource.SetControlValue(_controlKey.Interface,_controlKey.Property,1);
				Assert.IsTrue(manualResetEventPressed.WaitOne(5000),"The poll thread saw the edge.");
				triggerEngine.Stop();
				int countReads=_fakeFrameSource.GetReadCount(_controlKey.Interface,_controlKey.Property);
				Thread.Sleep(50);
				Assert.AreEqual(countReads,_fakeFrameSource.GetReadCount(_controlKey.Interface,_controlKey.Property),"No reads after Stop.");
			}
		}

		[TestMethod]
		public void DebounceGate_SharedAcrossThreads() {
			DebounceGate debounceGate=new DebounceGate(300);
			Assert.IsTrue(debounceGate.TryAccept(0));
			Assert.IsTrue(debounceGate.IsLockedOut(299));
			Assert.IsFalse(debounceGate.TryAccept(299));
			Assert.IsFalse(debounceGate.TryAccept(150),"A rejected press does not extend the lockout.");
			Assert.IsTrue(debounceGate.TryAccept(300));
			debounceGate.Reset();
			Assert.IsTrue(debounceGate.TryAccept(301));
			int countAccepted=0;
			DebounceGate debounceGateRace=new DebounceGate(1000);
			Thread[] arrayThreads=Enumerable.Range(0,8).Select(x => new Thread(() => {
				if(debounceGateRace.TryAccept(5)) {
					Interlocked.Increment(ref countAccepted);
				}
			})).ToArray();
			foreach(Thread thread in arrayThreads) {
				thread.Start();
			}
			foreach(Thread thread in arrayThreads) {
				thread.Join();
			}
			Assert.AreEqual(1,countAccepted);
		}

		///<summary>Queues posted callbacks until the test runs them, like a UI thread that is busy.</summary>
		private sealed class QueueSynchronizationContext:SynchronizationContext {
			private readonly Queue<Tuple<SendOrPostCallback,object>> _queue=new Queue<Tuple<SendOrPostCallback,object>>();

			public override void Post(SendOrPostCallback d,object state) {
				_queue.Enqueue(Tuple.Create(d,state));
			}

			public void RunAll() {
				while(_queue.Count>0) {
					Tuple<SendOrPostCallback,object> tuple=_queue.Dequeue();
					tuple.Item1(tuple.Item2);
				}
			}
		}
	}
}
