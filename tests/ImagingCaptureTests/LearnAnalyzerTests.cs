// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using ImagingCapture.Camera;
using ImagingCapture.Uvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingCapture.Tests {
	///<summary>LearnAnalyzer (design 8.5) on scripted control recordings, and the Learn result driving TriggerEngine and the live test
	///model. Every control, value and bit here is made up: a generic black box that has some steady controls, one that drifts, a
	///key bit and a bit that changes when it is put down. None of it describes a real camera.</summary>
	[TestClass]
	public class LearnAnalyzerTests {
		private static readonly ControlId _controlPan=new ControlId(UvcControlInterface.CameraControl,0);
		private static readonly ControlId _controlTilt=new ControlId(UvcControlInterface.CameraControl,1);
		private static readonly ControlId _controlExposure=new ControlId(UvcControlInterface.CameraControl,4);
		private static readonly ControlId _controlBrightness=new ControlId(UvcControlInterface.VideoProcAmp,0);
		private static readonly ControlId _controlGain=new ControlId(UvcControlInterface.VideoProcAmp,9);
		///<summary>The key bit of _controlTilt in these scripts.</summary>
		private const int KeyBit=0x1;
		///<summary>The bit of _controlTilt that is set while the handpiece rests, in these scripts.</summary>
		private const int RestBit=0x4;

		private ControlRecorder _recorder;
		private LearnAnalyzer _learnAnalyzer;

		[TestInitialize]
		public void Init() {
			_recorder=new ControlRecorder();
			_recorder.Set(_controlPan,0).Set(_controlTilt,0x20).Set(_controlBrightness,128).Set(_controlGain,0x40);
			_recorder.Drift(_controlExposure,timeMs => 300+(int)(timeMs/100)%7);
			_learnAnalyzer=new LearnAnalyzer();
		}

		///<summary>Baseline A held (no keys) and, when withBaselineB, Baseline B resting (RestBit set).</summary>
		private void RecordBaselines(bool withBaselineB) {
			BaselineResult baselineA=_learnAnalyzer.SetBaselineA(_recorder.Hold(LearnAnalyzer.BaselineMs).Take());
			Assert.IsTrue(baselineA.IsUsable,baselineA.Problem);
			if(!withBaselineB) {
				_learnAnalyzer.SkipBaselineB();
				return;
			}
			_recorder.Toggle(_controlTilt,RestBit);
			BaselineResult baselineB=_learnAnalyzer.SetBaselineB(_recorder.Hold(LearnAnalyzer.BaselineMs).Take());
			Assert.IsTrue(baselineB.IsUsable,baselineB.Problem);
			_recorder.Toggle(_controlTilt,RestBit);
			_recorder.Hold(500).Take();
		}

		///<summary>Three key presses of the given lengths on _controlTilt.</summary>
		private KeyLearnResult RecordKeyPresses(params int[] arrayPressMs) {
			_recorder.Hold(300);
			foreach(int pressMs in arrayPressMs) {
				_recorder.Press(_controlTilt,KeyBit,pressMs);
			}
			return _learnAnalyzer.SetKeyCapture(_recorder.Take(),null);
		}

		#region Baselines
		[TestMethod]
		public void Baseline_StableDriftingAndUnreadable() {
			_recorder.Set(_controlGain,0x40);
			List<ControlSample> listSamples=_recorder.Hold(1000).Take();
			listSamples[5]=new ControlSample(listSamples[5].TimeMs,listSamples[5].Values.Where(x => x.Key!=_controlBrightness).ToDictionary(x => x.Key,x => x.Value));
			BaselineResult baselineResult=_learnAnalyzer.SetBaselineA(listSamples);
			Assert.IsTrue(baselineResult.IsUsable);
			Assert.AreEqual(20,baselineResult.SampleCount);
			CollectionAssert.AreEquivalent(new[] { _controlPan,_controlTilt,_controlGain },baselineResult.StableValues.Keys.ToArray());
			Assert.AreEqual(0x20,baselineResult.StableValues[_controlTilt]);
			CollectionAssert.AreEqual(new[] { _controlExposure },baselineResult.Drifting.ToArray());
			CollectionAssert.AreEqual(new[] { _controlBrightness },baselineResult.Unreadable.ToArray());
			CollectionAssert.AreEqual(new[] { _controlPan,_controlTilt,_controlGain },_learnAnalyzer.GetUsableControls().ToArray());
		}

		[TestMethod]
		public void Baseline_TooFewReadings() {
			BaselineResult baselineResult=_learnAnalyzer.SetBaselineA(_recorder.Hold(50).Take());
			Assert.IsFalse(baselineResult.IsUsable);
			StringAssert.Contains(baselineResult.Problem,"at least two readings");
			TestUtil.AssertThrows<InvalidOperationException>(() => _learnAnalyzer.SetBaselineB(_recorder.Hold(1000).Take()));
			TestUtil.AssertThrows<InvalidOperationException>(() => _learnAnalyzer.SetKeyCapture(_recorder.Hold(1000).Take(),null));
		}

		[TestMethod]
		public void BaselineB_DifferingBitsBecomeLevelCandidates() {
			_recorder.Set(_controlGain,0x41);
			Assert.IsTrue(_learnAnalyzer.SetBaselineA(_recorder.Hold(LearnAnalyzer.BaselineMs).Take()).IsUsable);
			_recorder.Toggle(_controlTilt,RestBit|0x2).Toggle(_controlGain,0x1);
			_recorder.Drift(_controlPan,timeMs => (int)(timeMs/50)%2);
			BaselineResult baselineB=_learnAnalyzer.SetBaselineB(_recorder.Hold(LearnAnalyzer.BaselineMs).Take());
			Assert.IsTrue(baselineB.IsUsable);
			Assert.AreEqual(RestBit|0x2,_learnAnalyzer.LevelBits[_controlTilt]);
			Assert.AreEqual(0x1,_learnAnalyzer.LevelBits[_controlGain]);
			string[] arrayCandidates=_learnAnalyzer.BaselineLevelCandidates.Select(x => x.ToString()).ToArray();
			CollectionAssert.AreEqual(new[] {
				new ControlBinding(_controlTilt,0x2,ControlLevel.High).ToString(),
				new ControlBinding(_controlTilt,RestBit,ControlLevel.High).ToString(),
				new ControlBinding(_controlGain,0x1,ControlLevel.Low).ToString(),
			},arrayCandidates,"One per differing bit, active in the resting state.");
			Assert.IsTrue(_learnAnalyzer.BaselineLevelCandidates.All(x => x.Source==LevelCandidateSource.BaselineB));
			Assert.IsFalse(_learnAnalyzer.GetUsableControls().Contains(_controlPan),"A control that drifts while resting is not used.");
		}
		#endregion Baselines

		#region Key step
		[TestMethod]
		public void Key_SingleBitActiveHigh_DebounceIsTwiceTheLongestPress() {
			RecordBaselines(true);
			KeyLearnResult keyLearnResult=RecordKeyPresses(150,100,200);
			Assert.IsTrue(keyLearnResult.IsKeyFound);
			Assert.AreEqual(1,keyLearnResult.Candidates.Count,string.Join(" | ",keyLearnResult.Notes));
			ControlBinding binding=keyLearnResult.Chosen.Binding;
			Assert.AreEqual(_controlTilt,binding.Control);
			Assert.AreEqual(KeyBit,binding.Mask);
			Assert.AreEqual(ControlLevel.High,binding.ActiveLevel);
			CollectionAssert.AreEqual(new[] { 150,100,200 },keyLearnResult.Chosen.Presses.Select(x => x.DurationMs).ToArray());
			Assert.AreEqual(200,keyLearnResult.Chosen.LongestPressMs);
			Assert.AreEqual(400,keyLearnResult.DebounceMs);
			Assert.IsFalse(keyLearnResult.IsStillPinKey);
		}

		[TestMethod]
		public void Key_ShortPressesGiveTheDebounceFloor() {
			RecordBaselines(false);
			KeyLearnResult keyLearnResult=RecordKeyPresses(50,50,100);
			Assert.AreEqual(100,keyLearnResult.Chosen.LongestPressMs);
			Assert.AreEqual(200,keyLearnResult.DebounceMs,"Twice 100 ms is 200 ms, the floor.");
			_recorder.Hold(1000).Take();
			keyLearnResult=RecordKeyPresses(50,50,50);
			Assert.AreEqual(200,keyLearnResult.DebounceMs,"Twice 50 ms is below the floor.");
		}

		[TestMethod]
		public void Key_ActiveLow() {
			_recorder.Set(_controlGain,0x50);
			RecordBaselines(false);
			_recorder.Hold(300);
			for(int i=0;i<3;i++) {
				_recorder.Press(_controlGain,0x10,150);
			}
			KeyLearnResult keyLearnResult=_learnAnalyzer.SetKeyCapture(_recorder.Take(),null);
			Assert.AreEqual(new ControlBinding(_controlGain,0x10,ControlLevel.Low).ToString(),keyLearnResult.Chosen.Binding.ToString());
		}

		[TestMethod]
		public void Key_DriftingControlIsExcluded() {
			RecordBaselines(false);
			Assert.IsTrue(_learnAnalyzer.BaselineA.Drifting.Contains(_controlExposure));
			KeyLearnResult keyLearnResult=RecordKeyPresses(150,150,150);
			Assert.AreEqual(1,keyLearnResult.Candidates.Count);
			Assert.AreEqual(_controlTilt,keyLearnResult.Chosen.Binding.Control,"The drifting control changed throughout, and is never a candidate.");
			_recorder.Hold(1000).Take();
			_recorder.Drift(_controlExposure,timeMs => (timeMs/150)%2==0 ? 300 : 301);
			keyLearnResult=_learnAnalyzer.SetKeyCapture(_recorder.Hold(1500).Take(),null);
			Assert.IsFalse(keyLearnResult.IsKeyFound,"Only a drifting control changed: no key.");
			Assert.IsTrue(keyLearnResult.Notes.Any(x => x.Contains("No key was found")));
		}

		[TestMethod]
		public void Key_BaselineBBitsAreNeverPartOfTheMask() {
			RecordBaselines(true);
			Assert.AreEqual(RestBit,_learnAnalyzer.LevelBits[_controlTilt]);
			_recorder.Hold(300);
			_recorder.Press(_controlTilt,KeyBit,150);
			_recorder.Toggle(_controlTilt,KeyBit|RestBit).Hold(150).Toggle(_controlTilt,KeyBit|RestBit).Hold(300);//pressed while putting it down
			_recorder.Toggle(_controlTilt,RestBit).Hold(600).Toggle(_controlTilt,RestBit).Hold(300);//put down and picked up, no key
			_recorder.Press(_controlTilt,KeyBit,150);
			KeyLearnResult keyLearnResult=_learnAnalyzer.SetKeyCapture(_recorder.Take(),null);
			Assert.AreEqual(new ControlBinding(_controlTilt,KeyBit,ControlLevel.High).ToString(),keyLearnResult.Chosen?.Binding.ToString(),
				string.Join(" | ",keyLearnResult.Notes));
			Assert.IsTrue(keyLearnResult.Notes.Any(x => x.Contains("never used as a key")));
		}

		[TestMethod]
		public void Key_LevelToggledInsteadOfKey_IsNotAKeyWithBaselineB() {
			RecordBaselines(true);
			_recorder.Hold(300);
			for(int i=0;i<3;i++) {
				_recorder.Press(_controlTilt,RestBit,800);
			}
			List<ControlSample> listSamples=_recorder.Take();
			KeyLearnResult keyLearnResult=_learnAnalyzer.SetKeyCapture(listSamples,null);
			Assert.IsFalse(keyLearnResult.IsKeyFound,"Putting the handpiece down three times is not a key once Baseline B is recorded.");
			Assert.AreEqual(0,keyLearnResult.Candidates.Count);
			//Without Baseline B the same recording would teach the rest bit as a key: that is what Baseline B is for.
			LearnAnalyzer learnAnalyzerWithoutB=new LearnAnalyzer();
			learnAnalyzerWithoutB.SetBaselineA(RecordHeldBaseline());
			learnAnalyzerWithoutB.SkipBaselineB();
			Assert.AreEqual(RestBit,learnAnalyzerWithoutB.SetKeyCapture(listSamples,null).Chosen.Binding.Mask);
		}

		///<summary>A fresh held baseline of the current values.</summary>
		private List<ControlSample> RecordHeldBaseline() {
			return _recorder.Hold(LearnAnalyzer.BaselineMs).Take();
		}

		[TestMethod]
		public void MaskRule_SameSetOnEveryPress_MultiBitAccepted() {
			RecordBaselines(false);
			_recorder.Hold(300);
			for(int i=0;i<3;i++) {
				_recorder.Press(_controlPan,0x3,150);
			}
			KeyLearnResult keyLearnResult=_learnAnalyzer.SetKeyCapture(_recorder.Take(),null);
			Assert.AreEqual(new ControlBinding(_controlPan,0x3,ControlLevel.High).ToString(),keyLearnResult.Chosen.Binding.ToString());
		}

		[TestMethod]
		public void MaskRule_DifferentSetsWithOneCommonBit_ThatBit() {
			RecordBaselines(false);
			_recorder.Hold(300);
			_recorder.Press(_controlPan,0x3,150);
			_recorder.Press(_controlPan,0x1,150);
			_recorder.Press(_controlPan,0x5,150);
			KeyLearnResult keyLearnResult=_learnAnalyzer.SetKeyCapture(_recorder.Take(),null);
			Assert.AreEqual(new ControlBinding(_controlPan,0x1,ControlLevel.High).ToString(),keyLearnResult.Chosen.Binding.ToString());
		}

		[TestMethod]
		public void MaskRule_NoCommonBit_Refused() {
			RecordBaselines(false);
			_recorder.Hold(300);
			_recorder.Press(_controlPan,0x1,150);
			_recorder.Press(_controlPan,0x2,150);
			_recorder.Press(_controlPan,0x1,150);
			KeyLearnResult keyLearnResult=_learnAnalyzer.SetKeyCapture(_recorder.Take(),null);
			Assert.IsNull(keyLearnResult.Chosen);
			Assert.IsTrue(keyLearnResult.Notes.Any(x => x.Contains("no bit changed every time")),string.Join(" | ",keyLearnResult.Notes));
		}

		[TestMethod]
		public void MaskRule_DifferentSetsWithTwoCommonBits_Refused() {
			RecordBaselines(false);
			_recorder.Hold(300);
			_recorder.Press(_controlPan,0x7,150);
			_recorder.Press(_controlPan,0x3,150);
			_recorder.Press(_controlPan,0xB,150);
			KeyLearnResult keyLearnResult=_learnAnalyzer.SetKeyCapture(_recorder.Take(),null);
			Assert.IsNull(keyLearnResult.Chosen);
			Assert.IsTrue(keyLearnResult.Notes.Any(x => x.Contains("more than one bit changed every time")),string.Join(" | ",keyLearnResult.Notes));
		}

		[TestMethod]
		public void MaskRule_BitsInOppositeDirections_Refused() {
			_recorder.Set(_controlPan,0x1);
			RecordBaselines(false);
			_recorder.Hold(300);
			for(int i=0;i<3;i++) {
				_recorder.Press(_controlPan,0x3,150);//0x1 becomes 0x2
			}
			KeyLearnResult keyLearnResult=_learnAnalyzer.SetKeyCapture(_recorder.Take(),null);
			Assert.IsNull(keyLearnResult.Chosen);
			Assert.IsTrue(keyLearnResult.Notes.Any(x => x.Contains("opposite directions")),string.Join(" | ",keyLearnResult.Notes));
		}

		[TestMethod]
		public void MaskRule_BitsThatNeverChangeTogether_Refused() {
			RecordBaselines(false);
			_recorder.Hold(300);
			for(int i=0;i<3;i++) {
				_recorder.Toggle(_controlPan,0x1).Hold(100).Toggle(_controlPan,0x3).Hold(100).Toggle(_controlPan,0x2).Hold(300);
			}
			KeyLearnResult keyLearnResult=_learnAnalyzer.SetKeyCapture(_recorder.Take(),null);
			Assert.IsNull(keyLearnResult.Chosen);
			Assert.IsTrue(keyLearnResult.Notes.Any(x => x.Contains("did not change together")),string.Join(" | ",keyLearnResult.Notes));
		}

		[TestMethod]
		public void Key_WrongNumberOfPressesOrKeyStillHeld() {
			RecordBaselines(false);
			KeyLearnResult keyLearnResult=RecordKeyPresses(150,150);
			Assert.IsNull(keyLearnResult.Chosen);
			Assert.IsTrue(keyLearnResult.Notes.Any(x => x.Contains("changed 2 times")));
			keyLearnResult=RecordKeyPresses(150,150,150,150);
			Assert.IsTrue(keyLearnResult.Notes.Any(x => x.Contains("changed 4 times")));
			_recorder.Hold(300);
			_recorder.Press(_controlTilt,KeyBit,150);
			_recorder.Press(_controlTilt,KeyBit,150);
			_recorder.Toggle(_controlTilt,KeyBit).Hold(300);
			keyLearnResult=_learnAnalyzer.SetKeyCapture(_recorder.Take(),null);
			Assert.IsNull(keyLearnResult.Chosen);
			Assert.IsTrue(keyLearnResult.Notes.Any(x => x.Contains("had not returned")));
		}

		[TestMethod]
		public void Key_StillPinOnePerPress() {
			RecordBaselines(false);
			KeyLearnResult keyLearnResult=_learnAnalyzer.SetKeyCapture(_recorder.Hold(2000).Take(),new long[] { 100,700,1300 });
			Assert.IsTrue(keyLearnResult.IsKeyFound);
			Assert.IsTrue(keyLearnResult.IsStillPinKey);
			Assert.IsNull(keyLearnResult.Chosen);
			Assert.IsNull(keyLearnResult.DebounceMs,"Only a control shows how long a press lasts.");
			CameraConfig cameraConfig=_learnAnalyzer.BuildConfig(new CameraConfig(),null);
			Assert.IsTrue(cameraConfig.StillEnabled);
			Assert.IsFalse(cameraConfig.ControlPoll.Enabled);
			Assert.IsNull(cameraConfig.DebounceMs);
			Assert.AreEqual(300,cameraConfig.EffectiveDebounceMs);
			keyLearnResult=_learnAnalyzer.SetKeyCapture(_recorder.Hold(2000).Take(),new long[] { 100,700 });
			Assert.IsFalse(keyLearnResult.IsStillPinKey);
			Assert.AreEqual(2,keyLearnResult.StillSampleCount);
			Assert.IsTrue(keyLearnResult.Notes.Any(x => x.Contains("still pin sent 2 samples")));
		}

		[TestMethod]
		public void Key_StillPinAndControlTogether_BothTurnedOn() {
			RecordBaselines(false);
			_recorder.Hold(300);
			List<long> listStillTimes=new List<long>();
			for(int i=0;i<3;i++) {
				listStillTimes.Add(_recorder.TimeMs+50);
				_recorder.Press(_controlTilt,KeyBit,150);
			}
			KeyLearnResult keyLearnResult=_learnAnalyzer.SetKeyCapture(_recorder.Take(),listStillTimes);
			Assert.IsTrue(keyLearnResult.IsStillPinKey);
			Assert.IsNotNull(keyLearnResult.Chosen);
			CameraConfig cameraConfig=_learnAnalyzer.BuildConfig(new CameraConfig(),null);
			Assert.IsTrue(cameraConfig.StillEnabled);
			Assert.IsTrue(cameraConfig.ControlPoll.IsConfigured,"The shared debounce gate makes one photo of each press.");
		}

		[TestMethod]
		public void Key_SeveralControls_SingleBitFirstAndChoosable() {
			RecordBaselines(false);
			_recorder.Hold(300);
			for(int i=0;i<3;i++) {
				_recorder.Toggle(_controlPan,0x3).Toggle(_controlGain,0x2).Hold(150).Toggle(_controlPan,0x3).Toggle(_controlGain,0x2).Hold(300);
			}
			KeyLearnResult keyLearnResult=_learnAnalyzer.SetKeyCapture(_recorder.Take(),null);
			Assert.AreEqual(2,keyLearnResult.Candidates.Count);
			Assert.AreEqual(_controlGain,keyLearnResult.Chosen.Binding.Control,"Single-bit masks come first.");
			Assert.IsTrue(keyLearnResult.Notes.Any(x => x.Contains("Several controls")));
			Assert.IsTrue(_learnAnalyzer.ChooseKey(keyLearnResult.Candidates[1]));
			Assert.AreEqual(_controlPan,_learnAnalyzer.KeyResult.Chosen.Binding.Control);
			Assert.IsFalse(_learnAnalyzer.ChooseKey(new KeyCandidate(new ControlBinding(_controlTilt,1,ControlLevel.High),null)));
		}

		[TestMethod]
		public void KeyCaptureProgress_CountsPressesLive() {
			RecordBaselines(true);
			_recorder.Hold(300);
			Assert.AreEqual(0,_learnAnalyzer.GetKeyCaptureProgress(_recorder.Peek()).PressesSeen);
			_recorder.Toggle(_controlTilt,KeyBit).Hold(100);
			KeyCaptureProgress keyCaptureProgress=_learnAnalyzer.GetKeyCaptureProgress(_recorder.Peek());
			Assert.AreEqual(0,keyCaptureProgress.PressesSeen);
			Assert.IsTrue(keyCaptureProgress.IsKeyDown);
			_recorder.Toggle(_controlTilt,KeyBit).Hold(300);
			_recorder.Toggle(_controlTilt,RestBit).Hold(300).Toggle(_controlTilt,RestBit).Hold(300);
			keyCaptureProgress=_learnAnalyzer.GetKeyCaptureProgress(_recorder.Peek());
			Assert.AreEqual(1,keyCaptureProgress.PressesSeen,"The rest bit is not counted.");
			Assert.IsFalse(keyCaptureProgress.IsKeyDown);
			_recorder.Toggle(_controlBrightness,0x1).Hold(300);
			keyCaptureProgress=_learnAnalyzer.GetKeyCaptureProgress(_recorder.Peek());
			Assert.IsFalse(keyCaptureProgress.IsKeyDown,"A control that changed once and stayed changed does not hold the step.");
			Assert.AreEqual(0,new LearnAnalyzer().GetKeyCaptureProgress(_recorder.Peek()).PressesSeen,"Nothing without Baseline A.");
		}
		#endregion Key step

		#region Level step and result
		[TestMethod]
		public void LevelStep_OffersTheBitThatChangesWhenPutDown() {
			RecordBaselines(false);
			RecordKeyPresses(150,150,150);
			_recorder.Hold(300);
			_recorder.Toggle(_controlTilt,RestBit).Hold(1000);
			_recorder.Press(_controlTilt,KeyBit,150);//a key press while it rests is not the level
			_recorder.Toggle(_controlTilt,RestBit).Hold(500);
			LevelLearnResult levelLearnResult=_learnAnalyzer.SetLevelCapture(_recorder.Take());
			Assert.AreEqual(1,levelLearnResult.Candidates.Count,string.Join(" | ",levelLearnResult.Notes));
			Assert.AreEqual(new ControlBinding(_controlTilt,RestBit,ControlLevel.High).ToString(),levelLearnResult.Chosen.Binding.ToString());
			Assert.AreEqual(LevelCandidateSource.LevelStep,levelLearnResult.Chosen.Source);
			LevelLearnResult levelLearnResultNone=_learnAnalyzer.SetLevelCapture(_recorder.Hold(1000).Take());
			Assert.IsNull(levelLearnResultNone.Chosen);
			Assert.IsTrue(levelLearnResultNone.Notes.Any(x => x.Contains("no level input is offered")));
		}

		[TestMethod]
		public void BuildConfig_ReplacesTriggerAndLevelValuesOnly() {
			RecordBaselines(true);
			RecordKeyPresses(100,150,120);
			CameraConfig cameraConfigCurrent=new CameraConfig { Moniker="@device:fake:1",Name="Camera 1",Width=640,Height=480,PollMs=40,DebounceMs=999 };
			cameraConfigCurrent.ControlPoll.Enabled=true;
			cameraConfigCurrent.ControlPoll.SetBinding(new ControlBinding(_controlGain,0x80,ControlLevel.Low));
			cameraConfigCurrent.Levels.Add(new LevelInputConfig(2) { Enabled=true });
			CameraConfig cameraConfig=_learnAnalyzer.BuildConfig(cameraConfigCurrent,_learnAnalyzer.BaselineLevelCandidates[0]);
			Assert.AreEqual("@device:fake:1",cameraConfig.Moniker);
			Assert.AreEqual(640,cameraConfig.Width);
			Assert.AreEqual(40,cameraConfig.PollMs);
			Assert.IsFalse(cameraConfig.StillEnabled);
			Assert.IsTrue(cameraConfig.ControlPoll.IsConfigured);
			Assert.AreEqual(new ControlBinding(_controlTilt,KeyBit,ControlLevel.High).ToString(),cameraConfig.ControlPoll.GetBinding().ToString());
			Assert.AreEqual(300,cameraConfig.DebounceMs,"Twice 150 ms.");
			Assert.AreEqual(1,cameraConfig.Levels.Count);
			LevelInputConfig level=cameraConfig.GetLevel(1);
			Assert.IsTrue(level.IsLevelConfigured);
			Assert.AreEqual(new ControlBinding(_controlTilt,RestBit,ControlLevel.High).ToString(),level.GetBinding().ToString());
			Assert.AreEqual(LevelAction.PausePreview,level.Action);
			Assert.AreEqual(999,cameraConfigCurrent.DebounceMs,"The current configuration is not changed.");
			Assert.AreEqual(1,cameraConfigCurrent.Levels.Count);
			CameraConfig cameraConfigNoLevel=_learnAnalyzer.BuildConfig(cameraConfigCurrent,null);
			Assert.AreEqual(0,cameraConfigNoLevel.Levels.Count,"Level.2 is dropped, so Write disables it.");
			TestUtil.AssertThrows<InvalidOperationException>(() => new LearnAnalyzer().BuildConfig(cameraConfigCurrent,null));
			LearnAnalyzer learnAnalyzer=new LearnAnalyzer();
			learnAnalyzer.SetBaselineA(_recorder.Hold(1000).Take());
			TestUtil.AssertThrows<InvalidOperationException>(() => learnAnalyzer.BuildConfig(cameraConfigCurrent,null));
		}

		[TestMethod]
		public void PickUpAndPutDown20Times_GivesZeroPhotos() {
			RecordBaselines(true);
			KeyLearnResult keyLearnResult=RecordKeyPresses(150,150,150);
			Assert.AreEqual(KeyBit,keyLearnResult.Chosen.Binding.Mask);
			foreach(LevelCandidate levelAccepted in new[] { _learnAnalyzer.BaselineLevelCandidates.Single(),null }) {
				CameraConfig cameraConfig=_learnAnalyzer.BuildConfig(new CameraConfig(),levelAccepted);
				FakeUvcBackend fakeUvcBackend=new FakeUvcBackend();
				FakeFrameSource fakeFrameSource=(FakeFrameSource)fakeUvcBackend.Open(fakeUvcBackend.AddDevice());
				fakeFrameSource.Start(null,false);
				long nowMs=0;
				int countPhotos=0;
				int countLevelChanges=0;
				using(TriggerEngine triggerEngine=new TriggerEngine(fakeFrameSource,cameraConfig,() => nowMs,null,false)) {
					triggerEngine.Pressed+=(sender,e) => countPhotos++;
					triggerEngine.LevelChanged+=(sender,e) => countLevelChanges++;
					triggerEngine.Start();
					int value=0x20;
					Action<int> actionRun=ms => {
						for(int elapsed=0;elapsed<ms;elapsed+=50) {
							fakeFrameSource.SetControlValue(_controlTilt.Interface,_controlTilt.Property,value);
							nowMs+=50;
							triggerEngine.PollOnce();
						}
					};
					actionRun(500);
					for(int i=0;i<20;i++) {
						value^=RestBit;
						actionRun(1000);
						Assert.AreEqual(levelAccepted!=null,triggerEngine.IsPausePreviewActive);
						value^=RestBit;
						actionRun(1000);
						Assert.IsFalse(triggerEngine.IsPausePreviewActive);
					}
					Assert.AreEqual(0,countPhotos,"Picking up and putting down 20 times takes no photo.");
					Assert.AreEqual(levelAccepted==null ? 0 : 40,countLevelChanges);
					for(int i=0;i<5;i++) {
						value^=KeyBit;
						actionRun(150);
						value^=KeyBit;
						actionRun(1000);
					}
					Assert.AreEqual(5,countPhotos,"The key still works.");
				}
			}
		}

		[TestMethod]
		public void LiveTest_RawValuesTilesAndPhotoCount() {
			RecordBaselines(true);
			RecordKeyPresses(150,150,150);
			CameraConfig cameraConfig=_learnAnalyzer.BuildConfig(new CameraConfig(),_learnAnalyzer.BaselineLevelCandidates[0]);
			LiveTestModel liveTestModel=new LiveTestModel(cameraConfig,new[] { _controlGain,_controlPan });
			Assert.AreEqual(300,liveTestModel.DebounceMs);
			CollectionAssert.AreEqual(new[] { _controlPan,_controlTilt,_controlGain },liveTestModel.GetControls().ToArray(),"The used control is added; ControlId order.");
			Assert.IsTrue(liveTestModel.Rows.Single(x => x.Control==_controlTilt).IsUsed);
			Assert.IsFalse(liveTestModel.Rows.Single(x => x.Control==_controlPan).IsUsed);
			CollectionAssert.AreEqual(new[] { LiveTileKind.StillTrigger,LiveTileKind.ControlTrigger,LiveTileKind.Level },liveTestModel.Tiles.Select(x => x.Kind).ToArray());
			LiveTile tileStill=liveTestModel.Tiles[0];
			LiveTile tileControl=liveTestModel.Tiles[1];
			LiveTile tileLevel=liveTestModel.Tiles[2];
			Assert.IsFalse(tileStill.IsConfigured);
			Assert.IsTrue(tileControl.IsConfigured);
			Assert.IsTrue(tileLevel.IsConfigured);
			Assert.AreEqual(1,tileLevel.LevelNumber);
			_recorder.Hold(300);
			foreach(ControlSample sample in _recorder.Take()) {
				liveTestModel.Update(sample);
			}
			LiveValueRow rowTilt=liveTestModel.Rows.Single(x => x.Control==_controlTilt);
			Assert.IsTrue(rowTilt.IsReadable);
			Assert.AreEqual("32",rowTilt.ValueDecimal);
			Assert.AreEqual("0x0020",rowTilt.ValueHex);
			Assert.AreEqual("0000 0000 0010 0000",rowTilt.ValueBinary);
			Assert.IsFalse(tileControl.IsLit);
			for(int i=0;i<50;i++) {
				_recorder.Toggle(_controlTilt,KeyBit).Hold(150);
				foreach(ControlSample sample in _recorder.Take()) {
					liveTestModel.Update(sample);
				}
				Assert.IsTrue(tileControl.IsLit);
				_recorder.Toggle(_controlTilt,KeyBit).Hold(300);
				foreach(ControlSample sample in _recorder.Take()) {
					liveTestModel.Update(sample);
				}
				Assert.IsFalse(tileControl.IsLit);
			}
			Assert.AreEqual(50,liveTestModel.PhotoCount,"50 presses, 50 photos: none doubled, none missed.");
			Assert.AreEqual(50,tileControl.PressCount);
			for(int i=0;i<20;i++) {
				_recorder.Toggle(_controlTilt,RestBit).Hold(500);
				foreach(ControlSample sample in _recorder.Take()) {
					liveTestModel.Update(sample);
				}
				Assert.IsTrue(tileLevel.IsLit);
				_recorder.Toggle(_controlTilt,RestBit).Hold(500);
				foreach(ControlSample sample in _recorder.Take()) {
					liveTestModel.Update(sample);
				}
				Assert.IsFalse(tileLevel.IsLit);
			}
			Assert.AreEqual(50,liveTestModel.PhotoCount,"Putting it down 20 times adds none.");
			liveTestModel.OnStillSample(_recorder.TimeMs);
			Assert.IsTrue(tileStill.IsLit,"An unconfigured still pin still shows that it fires.");
			Assert.AreEqual(50,liveTestModel.PhotoCount);
			liveTestModel.Update(_recorder.Hold(400).Take().Last());
			Assert.IsFalse(tileStill.IsLit);
			liveTestModel.Update(new ControlSample(_recorder.TimeMs+50,null));
			Assert.IsFalse(rowTilt.IsReadable);
			Assert.AreEqual("",rowTilt.ValueHex);
		}
		#endregion Level step and result

		#region ControlSampler
		[TestMethod]
		public void ControlSampler_ReadsWithGetOnly() {
			FakeUvcBackend fakeUvcBackend=new FakeUvcBackend();
			FakeFrameSource fakeFrameSource=(FakeFrameSource)fakeUvcBackend.Open(fakeUvcBackend.AddDevice());
			fakeFrameSource.SetControlValue(_controlTilt.Interface,_controlTilt.Property,7);
			fakeFrameSource.SetControlValue(_controlGain.Interface,_controlGain.Property,-3);
			fakeFrameSource.SetControlStatus(_controlPan.Interface,_controlPan.Property,UvcControlStatus.Failed);
			List<ControlId> listReadable=ControlSampler.FindReadable(fakeFrameSource);
			CollectionAssert.AreEqual(new[] { _controlTilt,_controlGain },listReadable.ToArray());
			Assert.AreEqual(1,fakeFrameSource.GetReadCount(_controlPan.Interface,_controlPan.Property),"Every standard property is tried once.");
			ControlSample sample=ControlSampler.Read(fakeFrameSource,new[] { _controlTilt,_controlGain,_controlPan },1234);
			Assert.AreEqual(1234,sample.TimeMs);
			Assert.AreEqual(2,sample.Values.Count);
			int value;
			Assert.IsTrue(sample.TryGetValue(_controlGain,out value));
			Assert.AreEqual(-3,value);
			Assert.IsFalse(sample.TryGetValue(_controlPan,out value));
			Assert.AreEqual(0,ControlSampler.FindReadable(null).Count);
		}
		#endregion ControlSampler

		///<summary>Builds scripted readings: the current value of each control, sampled every StepMs. Drift functions override a value
		///with one computed from the time.</summary>
		private sealed class ControlRecorder {
			private readonly Dictionary<ControlId,int> _dictValues=new Dictionary<ControlId,int>();
			private readonly Dictionary<ControlId,Func<long,int>> _dictDrift=new Dictionary<ControlId,Func<long,int>>();
			private List<ControlSample> _listSamples=new List<ControlSample>();
			public long TimeMs;
			public int StepMs=50;

			public ControlRecorder Set(ControlId control,int value) {
				_dictDrift.Remove(control);
				_dictValues[control]=value;
				return this;
			}

			public ControlRecorder Drift(ControlId control,Func<long,int> funcValue) {
				_dictDrift[control]=funcValue;
				return this;
			}

			///<summary>Flips the bits.</summary>
			public ControlRecorder Toggle(ControlId control,int bits) {
				_dictValues[control]=_dictValues[control]^bits;
				return this;
			}

			///<summary>Records ms/StepMs readings of the current values.</summary>
			public ControlRecorder Hold(int ms) {
				for(int i=0;i<ms/StepMs;i++) {
					TimeMs+=StepMs;
					Dictionary<ControlId,int> dictValues=new Dictionary<ControlId,int>(_dictValues);
					foreach(KeyValuePair<ControlId,Func<long,int>> keyValuePair in _dictDrift) {
						dictValues[keyValuePair.Key]=keyValuePair.Value(TimeMs);
					}
					_listSamples.Add(new ControlSample(TimeMs,dictValues));
				}
				return this;
			}

			///<summary>Flips the bits for pressMs, then back for releaseMs.</summary>
			public ControlRecorder Press(ControlId control,int bits,int pressMs,int releaseMs=300) {
				return Toggle(control,bits).Hold(pressMs).Toggle(control,bits).Hold(releaseMs);
			}

			///<summary>The readings so far, which are then forgotten.</summary>
			public List<ControlSample> Take() {
				List<ControlSample> listSamples=_listSamples;
				_listSamples=new List<ControlSample>();
				return listSamples;
			}

			///<summary>The readings so far, kept.</summary>
			public List<ControlSample> Peek() {
				return new List<ControlSample>(_listSamples);
			}
		}
	}
}
