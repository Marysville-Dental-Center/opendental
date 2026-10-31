// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImagingCapture.Camera;
using ImagingCapture.UI;
using ImagingCapture.Uvc;
using ImagingDeviceManager.Config;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingCapture.Tests {
	///<summary>CaptureLearnModel, the page flow behind FrmCaptureLearn (design 8.5): when Next, Back, Record, Stop, Skip and Save are
	///enabled, how recordings end, what the test page counts, the video category warning, the button test, and the tile states.
	///Readings are scripted as in LearnAnalyzerTests: a generic black box with made-up controls, a key bit and a bit that changes
	///when it is put down. None of it describes a real camera.</summary>
	[TestClass]
	public class CaptureLearnModelTests {
		private static readonly ControlId _controlPan=new ControlId(UvcControlInterface.CameraControl,0);
		private static readonly ControlId _controlTilt=new ControlId(UvcControlInterface.CameraControl,1);
		private static readonly ControlId _controlGain=new ControlId(UvcControlInterface.VideoProcAmp,9);
		///<summary>The key bit of _controlTilt in these scripts.</summary>
		private const int KeyBit=0x1;
		///<summary>The bit of _controlTilt that is set while the handpiece rests.</summary>
		private const int RestBit=0x4;
		///<summary>A second key bit, on _controlGain, for the tests with two key candidates.</summary>
		private const int GainKeyBit=0x2;

		private ImageCategoryRule _imageCategoryRule;
		private CaptureLearnModel _model;
		private Script _script;

		[TestInitialize]
		public void Init() {
			List<ImageCategoryItem> listCategories=new List<ImageCategoryItem>() {
				new ImageCategoryItem(20,"Photos","",false),
				new ImageCategoryItem(21,"Patient pictures","P",false),
			};
			_imageCategoryRule=new ImageCategoryRule(() => listCategories);
			_model=MakeModel(CaptureLearnMode.Learn,new CameraConfig());
			_script=new Script(_model);
			_script.Set(_controlPan,0).Set(_controlTilt,0x20).Set(_controlGain,0x40);
		}

		private CaptureLearnModel MakeModel(CaptureLearnMode mode,CameraConfig cameraConfig,bool isCategoryEditable=true,bool hasStillPin=false,
			bool isStillPinConnected=false)
		{
			return new CaptureLearnModel(mode,cameraConfig,_imageCategoryRule,20,isCategoryEditable,hasStillPin,isStillPinConnected);
		}

		private static readonly ControlId[] _arrayControls={ _controlPan,_controlTilt,_controlGain };

		///<summary>Records Baseline A (held) and goes to Baseline B.</summary>
		private void DoBaselineA() {
			_model.SetControls(_arrayControls);
			Assert.IsTrue(_model.StartRecording(_script.TimeMs));
			_script.Hold(LearnAnalyzer.BaselineMs+100);
			Assert.IsFalse(_model.IsRecording,"A baseline stops by itself after BaselineMs.");
			Assert.IsTrue(_model.GoNext());
		}

		///<summary>Records Baseline B (resting, RestBit set) and goes to the key page.</summary>
		private void DoBaselineB() {
			_script.Toggle(_controlTilt,RestBit);
			Assert.IsTrue(_model.StartRecording(_script.TimeMs));
			_script.Hold(LearnAnalyzer.BaselineMs+100);
			_script.Toggle(_controlTilt,RestBit).Hold(500);
			Assert.IsTrue(_model.GoNext());
		}

		///<summary>Three presses of KeyBit; the recording stops by itself once they are released.</summary>
		private void DoKey() {
			Assert.IsTrue(_model.StartRecording(_script.TimeMs));
			_script.Hold(300).Press(_controlTilt,KeyBit,150).Press(_controlTilt,KeyBit,200).Press(_controlTilt,KeyBit,150);
			_script.Hold(CaptureLearnModel.KeySettleMs+100);
			Assert.IsFalse(_model.IsRecording,"The key recording stops by itself after the third press.");
		}

		private void LearnToTestPage(bool withLevel) {
			DoBaselineA();
			DoBaselineB();
			DoKey();
			Assert.IsTrue(_model.GoNext());
			if(withLevel) {
				Assert.IsTrue(_model.StartRecording(_script.TimeMs));
				_script.Hold(300).Toggle(_controlTilt,RestBit).Hold(1000).Toggle(_controlTilt,RestBit).Hold(300);
				Assert.IsTrue(_model.StopRecording());
				Assert.IsTrue(_model.GoNext());
			}
			else {
				Assert.IsTrue(_model.Skip());
			}
			Assert.AreEqual(LearnPage.LiveTest,_model.Page);
		}

		#region Page flow
		[TestMethod]
		public void Flow_EachPageNeedsItsStep() {
			Assert.AreEqual(LearnPage.BaselineA,_model.Page);
			Assert.IsFalse(_model.IsReady);
			Assert.IsFalse(_model.CanRecord,"Recording waits until the sampler has found the controls.");
			Assert.IsFalse(_model.CanGoNext);
			Assert.IsFalse(_model.CanGoBack);
			Assert.IsFalse(_model.CanSkip);
			Assert.IsFalse(_model.CanSave);
			Assert.AreEqual("Reading the camera's controls...",_model.GetProgressText(0));
			_model.SetControls(_arrayControls);
			Assert.IsTrue(_model.CanRecord);
			Assert.IsFalse(_model.GoNext(),"Baseline A must be recorded first.");
			Assert.IsTrue(_model.StartRecording(_script.TimeMs));
			Assert.IsTrue(_model.IsRecording);
			Assert.IsFalse(_model.CanRecord);
			Assert.IsFalse(_model.CanStop,"A baseline runs its fixed time.");
			Assert.IsFalse(_model.CanGoNext);
			_script.Hold(1500);
			StringAssert.StartsWith(_model.GetProgressText(_script.TimeMs),"Recording: 1.5 of 3.0 s");
			_script.Hold(1600);
			Assert.IsFalse(_model.IsRecording);
			Assert.IsTrue(_model.Analyzer.BaselineA.IsUsable);
			StringAssert.Contains(_model.GetResultText(),"Steady controls: CameraControl 0 (Pan); CameraControl 1 (Tilt); VideoProcAmp 9 (Gain).");
			Assert.IsTrue(_model.GoNext());
			Assert.AreEqual(LearnPage.BaselineB,_model.Page);
			Assert.AreEqual("Step 2 of 5: Baseline B, resting (optional)",_model.GetStepTitle());
			Assert.IsTrue(_model.CanSkip);
			Assert.IsTrue(_model.CanGoBack);
			Assert.IsFalse(_model.CanGoNext,"Baseline B must be recorded or skipped.");
			DoBaselineBOnPage();
			Assert.IsTrue(_model.GoNext());
			Assert.AreEqual(LearnPage.KeyCapture,_model.Page);
			Assert.IsFalse(_model.CanSkip,"The key step cannot be skipped.");
			Assert.IsFalse(_model.CanGoNext);
			DoKey();
			Assert.IsTrue(_model.CanGoNext);
			Assert.IsTrue(_model.GoNext());
			Assert.AreEqual(LearnPage.Level,_model.Page);
			Assert.IsTrue(_model.CanGoNext,"The level step is optional.");
			Assert.IsTrue(_model.CanSkip);
			Assert.IsFalse(_model.CanSave);
			Assert.IsTrue(_model.GoNext());
			Assert.AreEqual(LearnPage.LiveTest,_model.Page);
			Assert.IsFalse(_model.CanGoNext);
			Assert.IsFalse(_model.CanRecord);
			Assert.IsTrue(_model.CanSave);
		}

		private void DoBaselineBOnPage() {
			_script.Toggle(_controlTilt,RestBit);
			Assert.IsTrue(_model.StartRecording(_script.TimeMs));
			_script.Hold(LearnAnalyzer.BaselineMs+100);
			_script.Toggle(_controlTilt,RestBit).Hold(500);
			StringAssert.Contains(_model.GetResultText(),"Differ between held and resting: CameraControl 1 (Tilt) bits 0x4.");
		}

		[TestMethod]
		public void Flow_BackKeepsResults_RecordingAgainForgetsLaterSteps() {
			DoBaselineA();
			DoBaselineB();
			DoKey();
			Assert.IsTrue(_model.GoBack());
			Assert.AreEqual(LearnPage.BaselineB,_model.Page);
			Assert.IsTrue(_model.CanGoNext,"Going back keeps Baseline B.");
			Assert.IsTrue(_model.GoBack());
			Assert.IsTrue(_model.GoNext());
			Assert.IsTrue(_model.GoNext());
			Assert.IsNotNull(_model.Analyzer.KeyResult,"Going back and forth keeps the key result.");
			Assert.IsTrue(_model.GoBack());
			Assert.IsTrue(_model.GoBack());
			Assert.IsTrue(_model.StartRecording(_script.TimeMs));
			_script.Hold(LearnAnalyzer.BaselineMs+100);
			Assert.IsNull(_model.Analyzer.KeyResult,"A new Baseline A forgets the key step.");
			Assert.IsTrue(_model.GoNext());
			Assert.IsFalse(_model.CanGoNext,"...and Baseline B.");
		}

		[TestMethod]
		public void BaselineB_UnusableRecordingBlocksNext_SkipGoesOn() {
			DoBaselineA();
			Assert.IsTrue(_model.StartRecording(_script.TimeMs));
			_model.Tick(_script.TimeMs+LearnAnalyzer.BaselineMs);//no reading came in
			Assert.IsFalse(_model.IsRecording);
			Assert.IsFalse(_model.CanGoNext);
			StringAssert.Contains(_model.GetResultText(),"at least two readings");
			Assert.IsTrue(_model.Skip());
			Assert.AreEqual(LearnPage.KeyCapture,_model.Page);
			Assert.IsTrue(_model.GoBack());
			Assert.AreEqual("Skipped: no resting position was recorded.",_model.GetResultText());
		}

		[TestMethod]
		public void Recording_IgnoresReadingsFromBeforeRecord() {
			_model.SetControls(_arrayControls);
			Assert.IsTrue(_model.StartRecording(10000));
			_model.OnSample(new ControlSample(9990,new Dictionary<ControlId,int>() { { _controlPan,1 } }));
			_script.TimeMs=10000;
			_script.Hold(LearnAnalyzer.BaselineMs+100);
			Assert.IsTrue(_model.Analyzer.BaselineA.StableValues.ContainsKey(_controlPan),"The early reading with another value was left out.");
			Assert.AreEqual(0,_model.Analyzer.BaselineA.StableValues[_controlPan]);
		}

		[TestMethod]
		public void Key_WaitsForTheReleaseAndTheSettleTime() {
			DoBaselineA();
			Assert.IsTrue(_model.Skip());
			Assert.IsTrue(_model.StartRecording(_script.TimeMs));
			Assert.IsTrue(_model.CanStop);
			_script.Hold(300).Press(_controlTilt,KeyBit,150).Press(_controlTilt,KeyBit,150);
			_script.Toggle(_controlTilt,KeyBit).Hold(2000);
			Assert.IsTrue(_model.IsRecording,"The third press is still held.");
			Assert.AreEqual(2,_model.KeyProgress.PressesSeen);
			Assert.IsTrue(_model.KeyProgress.IsKeyDown);
			StringAssert.StartsWith(_model.GetProgressText(_script.TimeMs),"Presses seen: 2 of 3 (key held).");
			_script.Toggle(_controlTilt,KeyBit).Hold(CaptureLearnModel.KeySettleMs-100);
			Assert.IsTrue(_model.IsRecording,"Released, but the settle time has not passed.");
			_script.Hold(300);
			Assert.IsFalse(_model.IsRecording);
			Assert.AreEqual(_controlTilt,_model.KeyChosen.Binding.Control);
			Assert.AreEqual(KeyBit,_model.KeyChosen.Binding.Mask);
			Assert.AreEqual(ControlLevel.High,_model.KeyChosen.Binding.ActiveLevel);
			StringAssert.Contains(_model.GetResultText(),"Key found: CameraControl 1 (Tilt), mask 0x1, High.");
		}

		[TestMethod]
		public void Key_FourthPressInTheSettleTimeIsRecordedAndRefused() {
			DoBaselineA();
			Assert.IsTrue(_model.Skip());
			Assert.IsTrue(_model.StartRecording(_script.TimeMs));
			_script.Hold(300).Press(_controlTilt,KeyBit,150).Press(_controlTilt,KeyBit,150).Press(_controlTilt,KeyBit,150,200);
			_script.Press(_controlTilt,KeyBit,150).Hold(CaptureLearnModel.KeySettleMs+100);
			Assert.IsFalse(_model.IsRecording);
			Assert.IsNull(_model.KeyChosen);
			StringAssert.Contains(_model.GetResultText(),"changed 4 times");
			Assert.IsTrue(_model.CanRecord,"Record again.");
		}

		[TestMethod]
		public void Key_TimeoutWithoutAKey_CanStillGoOnAndSavesNoTrigger() {
			DoBaselineA();
			Assert.IsTrue(_model.Skip());
			Assert.IsTrue(_model.StartRecording(_script.TimeMs));
			_script.Hold(CaptureLearnModel.KeyTimeoutMs-100);
			Assert.IsTrue(_model.IsRecording);
			_script.Hold(200);
			Assert.IsFalse(_model.IsRecording,"The key recording stops after KeyTimeoutMs.");
			Assert.IsFalse(_model.Analyzer.KeyResult.IsKeyFound);
			StringAssert.Contains(_model.GetResultText(),"No key was found");
			Assert.IsTrue(_model.GoNext());
			Assert.IsTrue(_model.Skip());
			Assert.IsTrue(_model.CanSave,"Saving turns the triggers off, which clears values another camera may have left.");
			CameraConfig cameraConfig=_model.GetConfigToSave();
			Assert.IsFalse(cameraConfig.StillEnabled);
			Assert.IsFalse(cameraConfig.ControlPoll.Enabled);
			StringAssert.Contains(_model.GetResultText(),"No camera trigger is set up");
		}

		[TestMethod]
		public void Key_StopByHand() {
			DoBaselineA();
			Assert.IsTrue(_model.Skip());
			Assert.IsFalse(_model.StopRecording(),"Nothing is recording.");
			Assert.IsTrue(_model.StartRecording(_script.TimeMs));
			_script.Hold(300).Press(_controlTilt,KeyBit,150);
			Assert.IsTrue(_model.StopRecording());
			Assert.IsFalse(_model.IsRecording);
			StringAssert.Contains(_model.GetResultText(),"changed 1 times");
		}

		[TestMethod]
		public void Key_StillPinOnly() {
			_model=MakeModel(CaptureLearnMode.Learn,new CameraConfig(),hasStillPin:true,isStillPinConnected:true);
			_script=new Script(_model);
			_model.SetControls(new ControlId[0]);
			StringAssert.Contains(_model.GetResultText(),"no readable control");
			Assert.IsTrue(_model.StartRecording(_script.TimeMs));
			_script.Hold(LearnAnalyzer.BaselineMs+100);
			Assert.IsTrue(_model.GoNext(),"A baseline with no control is usable: only a still-pin key can be learned.");
			Assert.IsTrue(_model.Skip());
			Assert.IsTrue(_model.StartRecording(_script.TimeMs));
			for(int i=0;i<LearnAnalyzer.PressCount;i++) {
				_script.Hold(400);
				_model.OnStillSample(_script.TimeMs);
			}
			StringAssert.Contains(_model.GetProgressText(_script.TimeMs),"Still pin samples: 3.");
			_script.Hold(CaptureLearnModel.KeySettleMs+100);
			Assert.IsFalse(_model.IsRecording);
			Assert.IsTrue(_model.Analyzer.KeyResult.IsStillPinKey);
			Assert.IsNull(_model.KeyChosen);
			Assert.IsTrue(_model.GoNext());
			Assert.IsTrue(_model.Skip());
			Assert.IsTrue(_model.GetConfigToSave().StillEnabled);
		}

		[TestMethod]
		public void Key_StillPinNotConnected_IsExplained() {
			_model=MakeModel(CaptureLearnMode.Learn,new CameraConfig(),hasStillPin:true,isStillPinConnected:false);
			_script=new Script(_model);
			_script.Set(_controlPan,0).Set(_controlTilt,0x20).Set(_controlGain,0x40);
			DoBaselineA();
			Assert.IsTrue(_model.Skip());
			StringAssert.Contains(_model.GetInstructions(),"still pin, but it is not connected");
		}
		#endregion Page flow

		#region Choices
		[TestMethod]
		public void KeyChoice_TwoCandidates_LevelNeverSharesTheKeyBits() {
			DoBaselineA();
			Assert.IsTrue(_model.Skip());
			Assert.IsTrue(_model.StartRecording(_script.TimeMs));
			_script.Hold(300);
			for(int i=0;i<LearnAnalyzer.PressCount;i++) {
				_script.Toggle(_controlTilt,KeyBit).Toggle(_controlGain,GainKeyBit).Hold(150);
				_script.Toggle(_controlTilt,KeyBit).Toggle(_controlGain,GainKeyBit).Hold(300);
			}
			_script.Hold(CaptureLearnModel.KeySettleMs+100);
			Assert.IsTrue(_model.IsKeyPickerVisible);
			IReadOnlyList<KeyCandidate> listKeys=_model.GetKeyCandidates();
			Assert.AreEqual(2,listKeys.Count);
			Assert.AreEqual(_controlTilt,_model.KeyChosen.Binding.Control);
			Assert.IsTrue(_model.ChooseKey(listKeys[1]));
			Assert.AreEqual(_controlGain,_model.KeyChosen.Binding.Control);
			Assert.IsTrue(_model.GoNext());
			Assert.IsFalse(_model.ChooseKey(listKeys[0]),"The key is chosen on the key page only.");
			//The level step sees the gain key bit change once (a put-down that moves the same bit is made up for this test).
			Assert.IsTrue(_model.StartRecording(_script.TimeMs));
			_script.Hold(300).Toggle(_controlGain,GainKeyBit).Toggle(_controlPan,0x8).Hold(800).Toggle(_controlGain,GainKeyBit).Toggle(_controlPan,0x8).Hold(300);
			Assert.IsTrue(_model.StopRecording());
			List<LevelCandidate> listLevels=_model.GetLevelCandidates();
			Assert.IsTrue(listLevels.Count>0);
			Assert.IsFalse(listLevels.Any(x => x.Binding.Control==_controlGain && (x.Binding.Mask & GainKeyBit)!=0),"The key's bits are never a level.");
			Assert.AreEqual(_controlPan,_model.LevelChosen.Binding.Control,"The level step's best candidate is preselected.");
			Assert.IsTrue(_model.ChooseLevel(null));
			Assert.IsNull(_model.LevelChosen);
			Assert.IsFalse(_model.ChooseLevel(new LevelCandidate(new ControlBinding(_controlPan,0x10,ControlLevel.High),LevelCandidateSource.LevelStep)),
				"Only an offered candidate can be chosen.");
			Assert.IsTrue(_model.ChooseLevel(listLevels[0]));
			Assert.IsTrue(_model.GoNext());
			CameraConfig cameraConfig=_model.GetConfigToSave();
			Assert.AreEqual(_controlGain.Property,cameraConfig.ControlPoll.Property);
			Assert.AreEqual(1,cameraConfig.Levels.Count);
			Assert.AreEqual(_controlPan.Property,cameraConfig.Levels[0].Property);
		}

		[TestMethod]
		public void LevelChoice_BaselineBCandidateWithoutRecording() {
			DoBaselineA();
			DoBaselineB();
			DoKey();
			Assert.IsTrue(_model.GoNext());
			Assert.IsNull(_model.LevelChosen,"Level inputs are off unless chosen.");
			List<LevelCandidate> listLevels=_model.GetLevelCandidates();
			Assert.AreEqual(1,listLevels.Count);
			Assert.AreEqual(LevelCandidateSource.BaselineB,listLevels[0].Source);
			Assert.AreEqual(RestBit,listLevels[0].Binding.Mask);
			Assert.AreEqual(ControlLevel.High,listLevels[0].Binding.ActiveLevel,"Active while resting.");
			StringAssert.Contains(_model.GetResultText(),"Level input: none.");
			Assert.IsTrue(_model.ChooseLevel(listLevels[0]));
			StringAssert.Contains(_model.GetResultText(),"Level input: CameraControl 1 (Tilt), mask 0x4, High");
			Assert.IsTrue(_model.GoNext());
			Assert.AreEqual(RestBit,_model.GetConfigToSave().Levels[0].Mask);
		}

		[TestMethod]
		public void LevelChoice_SkipMeansNone() {
			DoBaselineA();
			DoBaselineB();
			DoKey();
			Assert.IsTrue(_model.GoNext());
			Assert.IsTrue(_model.ChooseLevel(_model.GetLevelCandidates()[0]));
			Assert.IsTrue(_model.Skip());
			Assert.AreEqual(0,_model.GetConfigToSave().Levels.Count);
		}
		#endregion Choices

		#region Test page and Save
		[TestMethod]
		public void Save_OnlyOnTheTestPage() {
			Assert.IsNull(_model.GetConfigToSave());
			LearnToTestPage(withLevel:true);
			Assert.IsTrue(_model.CanSave);
			Assert.IsNotNull(_model.GetConfigToSave());
			Assert.IsTrue(_model.GoBack());
			Assert.IsFalse(_model.CanSave);
			Assert.IsNull(_model.ConfigUnderTest,"Leaving the test page drops its configuration.");
			Assert.IsNull(_model.LiveTest);
			Assert.IsNull(_model.GetConfigToSave());
			Assert.IsTrue(_model.StartRecording(_script.TimeMs));
			Assert.IsFalse(_model.CanSave);
			Assert.IsFalse(_model.CanGoNext,"Nothing moves while recording.");
			Assert.IsFalse(_model.CanGoBack);
			Assert.IsTrue(_model.StopRecording());
			Assert.IsTrue(_model.GoNext());
			Assert.IsTrue(_model.CanSave);
		}

		[TestMethod]
		public void TestPage_CountsPhotos_AndPickUpPutDownGivesNone() {
			LearnToTestPage(withLevel:true);
			CameraConfig cameraConfig=_model.ConfigUnderTest;
			Assert.IsTrue(cameraConfig.ControlPoll.IsConfigured);
			Assert.AreEqual(1,cameraConfig.Levels.Count);
			Assert.IsTrue(cameraConfig.Levels[0].IsLevelConfigured);
			LiveTestModel liveTestModel=_model.LiveTest;
			Assert.AreEqual(3,liveTestModel.Tiles.Count);
			Assert.AreEqual(3,liveTestModel.Rows.Count,"Every control found is a row.");
			_script.Hold(200);
			for(int i=0;i<20;i++) {
				_script.Toggle(_controlTilt,RestBit).Hold(400);
				Assert.IsTrue(liveTestModel.Tiles[2].IsLit,"The level tile lights while resting.");
				_script.Toggle(_controlTilt,RestBit).Hold(400);
				Assert.IsFalse(liveTestModel.Tiles[2].IsLit);
			}
			Assert.AreEqual(0,liveTestModel.PhotoCount,"Picking up and putting down 20 times gives zero photos.");
			for(int i=0;i<5;i++) {
				_script.Press(_controlTilt,KeyBit,150,cameraConfig.EffectiveDebounceMs+100);
			}
			Assert.AreEqual(5,liveTestModel.PhotoCount);
			Assert.AreEqual(5,liveTestModel.Tiles[1].PressCount);
			Assert.AreEqual("Photos Capture would take: 5",_model.GetProgressText(_script.TimeMs));
			_model.ResetLiveTest();
			Assert.AreNotSame(liveTestModel,_model.LiveTest);
			Assert.AreEqual(0,_model.LiveTest.PhotoCount);
		}

		[TestMethod]
		public void TestPage_StillSamplesLightTheStillTile() {
			LearnToTestPage(withLevel:false);
			_script.Hold(100);
			_model.OnStillSample(_script.TimeMs);
			LiveTile liveTileStill=_model.LiveTest.Tiles[0];
			Assert.IsTrue(liveTileStill.IsLit,"An unconfigured still tile still shows that the pin responds.");
			Assert.AreEqual(0,_model.LiveTest.PhotoCount,"The still trigger was not learned, so no photo.");
			Assert.AreEqual(LiveTileState.Lit,LiveTileView.GetState(liveTileStill));
			_script.Hold(_model.LiveTest.DebounceMs+100);
			Assert.IsFalse(liveTileStill.IsLit);
			Assert.AreEqual(LiveTileState.NotSetUp,LiveTileView.GetState(liveTileStill));
		}

		[TestMethod]
		public void Save_WritesWhatTheTestPageShowed() {
			string folder=Path.Combine(Path.GetTempPath(),"ImagingCaptureTests_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(folder);
			try {
				string path=Path.Combine(folder,"capture.ini");
				File.WriteAllText(path,"[Camera]\r\nName=Camera 1\r\n[Trigger.ControlPoll]\r\nPollMs=40\r\n");
				LocalCaptureConfig localCaptureConfig=LocalCaptureConfig.Load(path);
				_model=MakeModel(CaptureLearnMode.Learn,CameraConfig.Read(localCaptureConfig));
				_script=new Script(_model);
				_script.Set(_controlPan,0).Set(_controlTilt,0x20).Set(_controlGain,0x40);
				LearnToTestPage(withLevel:true);
				string error;
				Assert.IsTrue(_model.GetConfigToSave().Write(localCaptureConfig,out error),error);
				Assert.IsTrue(localCaptureConfig.Save(),localCaptureConfig.LastError);
				CameraConfig cameraConfigRead=CameraConfig.Read(LocalCaptureConfig.Load(path));
				Assert.AreEqual("Camera 1",cameraConfigRead.Name,"[Camera] is kept.");
				Assert.AreEqual(40,cameraConfigRead.PollMs,"PollMs is kept.");
				Assert.IsTrue(cameraConfigRead.ControlPoll.IsConfigured);
				Assert.AreEqual(_model.ConfigUnderTest.ControlPoll.GetBinding().ToString(),cameraConfigRead.ControlPoll.GetBinding().ToString());
				Assert.AreEqual(_model.ConfigUnderTest.DebounceMs,cameraConfigRead.DebounceMs);
				Assert.AreEqual(1,cameraConfigRead.Levels.Count(x => x.IsLevelConfigured));
				Assert.AreEqual(LevelAction.PausePreview,cameraConfigRead.GetLevel(1).Action);
			}
			finally {
				try {
					Directory.Delete(folder,true);
				}
				catch {
				}
			}
		}
		#endregion Test page and Save

		#region Video image category
		[TestMethod]
		public void Category_OfferedAfterTheFirstPage_WarnsForP() {
			Assert.IsFalse(_model.IsCategoryVisible,"Not on the first page.");
			DoBaselineA();
			Assert.IsTrue(_model.IsCategoryVisible);
			Assert.AreEqual("",_model.GetCategoryWarning());
			Assert.IsFalse(_model.IsCategoryChanged);
			int version=_model.Version;
			Assert.IsTrue(_model.SetCategory(21));
			Assert.IsTrue(_model.Version>version,"The window refreshes on a category change.");
			Assert.IsTrue(_model.IsCategoryChanged);
			StringAssert.Contains(_model.GetCategoryWarning(),"patient picture category");
			Assert.IsTrue(_model.SetCategory(0));
			Assert.AreEqual("",_model.GetCategoryWarning());
			Assert.IsTrue(_model.SetCategory(20));
			Assert.IsFalse(_model.IsCategoryChanged);
			Assert.IsFalse(_model.SetCategory(-1));
			DoBaselineB();
			Assert.IsTrue(_model.IsCategoryVisible);
			DoKey();
			Assert.IsTrue(_model.GoNext());
			Assert.IsTrue(_model.IsCategoryVisible);
			Assert.IsTrue(_model.Skip());
			Assert.IsTrue(_model.IsCategoryVisible,"Every page after the first.");
		}

		[TestMethod]
		public void Category_NotEditableWithoutPermission() {
			_model=MakeModel(CaptureLearnMode.Learn,new CameraConfig(),isCategoryEditable:false);
			Assert.IsFalse(_model.SetCategory(21));
			Assert.AreEqual(20,_model.CategoryDefNum);
			Assert.IsFalse(_model.IsCategoryChanged);
		}
		#endregion Video image category

		#region Button test
		[TestMethod]
		public void ButtonTest_TilesOfTheSavedConfigurationOnly() {
			CameraConfig cameraConfig=new CameraConfig();
			cameraConfig.DebounceMs=250;
			cameraConfig.ControlPoll.Enabled=true;
			cameraConfig.ControlPoll.SetBinding(new ControlBinding(_controlTilt,KeyBit,ControlLevel.High));
			LevelInputConfig levelInputConfig=new LevelInputConfig(1) { Enabled=true };
			levelInputConfig.SetBinding(new ControlBinding(_controlPan,0x8,ControlLevel.Low));
			cameraConfig.Levels.Add(levelInputConfig);
			_model=MakeModel(CaptureLearnMode.ButtonTest,cameraConfig);
			_script=new Script(_model);
			_script.Set(_controlPan,0x8).Set(_controlTilt,0x20).Set(_controlGain,0x40);
			Assert.AreEqual(LearnPage.LiveTest,_model.Page);
			Assert.AreEqual("Button test",_model.GetStepTitle());
			Assert.IsTrue(_model.IsReady);
			CollectionAssert.AreEquivalent(new[] { _controlPan,_controlTilt },_model.GetControlsToRead(),"Only the configured controls are read.");
			Assert.IsFalse(_model.CanSave);
			Assert.IsFalse(_model.CanGoNext);
			Assert.IsFalse(_model.CanGoBack);
			Assert.IsFalse(_model.CanRecord);
			Assert.IsFalse(_model.CanSkip);
			Assert.IsFalse(_model.IsCategoryVisible);
			Assert.IsFalse(_model.IsValueGridVisible,"Tiles only.");
			Assert.IsTrue(_model.IsTilesVisible);
			Assert.IsNull(_model.GetConfigToSave());
			_model.SetControls(_arrayControls);
			Assert.AreEqual(2,_model.Controls.Count,"The button test keeps its own controls.");
			_script.Hold(100).Press(_controlTilt,KeyBit,100,400).Press(_controlTilt,KeyBit,100,400);
			Assert.AreEqual(2,_model.LiveTest.PhotoCount);
			LiveTile liveTileLevel=_model.LiveTest.Tiles[2];
			Assert.IsFalse(liveTileLevel.IsLit);
			_script.Toggle(_controlPan,0x8).Hold(100);
			Assert.IsTrue(liveTileLevel.IsLit,"Active Low: the bit cleared.");
			Assert.AreEqual(2,_model.LiveTest.PhotoCount);
			StringAssert.Contains(_model.GetResultText(),"Control trigger: CameraControl 1 (Tilt), mask 0x1, High");
			StringAssert.Contains(_model.GetResultText(),"Lockout after a press: 250 ms.");
			StringAssert.Contains(_model.GetResultText(),"Level 1: CameraControl 0 (Pan), mask 0x8, Low");
		}

		[TestMethod]
		public void ButtonTest_NothingSetUp() {
			_model=MakeModel(CaptureLearnMode.ButtonTest,new CameraConfig());
			Assert.AreEqual(0,_model.GetControlsToRead().Count);
			Assert.AreEqual(2,_model.LiveTest.Tiles.Count);
			Assert.IsTrue(_model.LiveTest.Tiles.All(x => LiveTileView.GetState(x)==LiveTileState.NotSetUp));
			StringAssert.Contains(_model.GetResultText(),"No camera trigger is set up");
			StringAssert.Contains(_model.GetResultText(),"Lockout after a press: 300 ms (default).");
		}
		#endregion Button test

		#region Tiles
		[TestMethod]
		public void Tiles_StateAndText() {
			CameraConfig cameraConfig=new CameraConfig();
			cameraConfig.StillEnabled=true;
			cameraConfig.ControlPoll.Enabled=true;
			cameraConfig.ControlPoll.SetBinding(new ControlBinding(_controlTilt,KeyBit,ControlLevel.High));
			LevelInputConfig levelInputConfig=new LevelInputConfig(1) { Enabled=true };
			levelInputConfig.SetBinding(new ControlBinding(_controlTilt,RestBit,ControlLevel.High));
			cameraConfig.Levels.Add(levelInputConfig);
			LiveTestModel liveTestModel=new LiveTestModel(cameraConfig,new[] { _controlTilt });
			LiveTile liveTileStill=liveTestModel.Tiles[0];
			LiveTile liveTileControl=liveTestModel.Tiles[1];
			LiveTile liveTileLevel=liveTestModel.Tiles[2];
			Assert.AreEqual(LiveTileState.Waiting,LiveTileView.GetState(liveTileStill));
			Assert.AreEqual("Capture: control\r\nPresses: 0",LiveTileView.GetText(liveTileControl));
			Assert.AreEqual("Level 1: pause preview\r\nInactive",LiveTileView.GetText(liveTileLevel));
			liveTestModel.Update(Sample(1000,0x20));
			liveTestModel.Update(Sample(1050,0x21));
			Assert.AreEqual(LiveTileState.Lit,LiveTileView.GetState(liveTileControl));
			Assert.AreEqual("Capture: control\r\nPresses: 1",LiveTileView.GetText(liveTileControl));
			liveTestModel.Update(Sample(1100,0x24));
			Assert.AreEqual(LiveTileState.Waiting,LiveTileView.GetState(liveTileControl));
			Assert.AreEqual(LiveTileState.Lit,LiveTileView.GetState(liveTileLevel));
			Assert.AreEqual("Level 1: pause preview\r\nActive",LiveTileView.GetText(liveTileLevel));
			liveTestModel.OnStillSample(1200);
			Assert.AreEqual(LiveTileState.Lit,LiveTileView.GetState(liveTileStill));
			Assert.AreEqual("Capture: still pin\r\nPresses: 0",LiveTileView.GetText(liveTileStill),"Inside the control press's lockout: no photo.");
			LiveTestModel liveTestModelEmpty=new LiveTestModel(new CameraConfig(),null);
			Assert.AreEqual("Capture: control\r\nNot set up",LiveTileView.GetText(liveTestModelEmpty.Tiles[1]));
			Assert.AreEqual(LiveTileState.NotSetUp,LiveTileView.GetState(liveTestModelEmpty.Tiles[1]));
			Assert.AreEqual(LiveTileState.NotSetUp,LiveTileView.GetState(null));
			Assert.AreEqual("",LiveTileView.GetText(null));
		}

		private static ControlSample Sample(long timeMs,int valueTilt) {
			return new ControlSample(timeMs,new Dictionary<ControlId,int>() { { _controlTilt,valueTilt } });
		}
		#endregion Tiles

		///<summary>Feeds scripted readings to the model every StepMs, and gives it the clock after each.</summary>
		private sealed class Script {
			private readonly CaptureLearnModel _model;
			private readonly Dictionary<ControlId,int> _dictValues=new Dictionary<ControlId,int>();
			public long TimeMs=1000;
			public int StepMs=50;

			public Script(CaptureLearnModel model) {
				_model=model;
			}

			public Script Set(ControlId control,int value) {
				_dictValues[control]=value;
				return this;
			}

			public Script Toggle(ControlId control,int bits) {
				_dictValues[control]=_dictValues[control]^bits;
				return this;
			}

			public Script Hold(int ms) {
				for(int i=0;i<ms/StepMs;i++) {
					TimeMs+=StepMs;
					_model.OnSample(new ControlSample(TimeMs,_dictValues));
					_model.Tick(TimeMs);
				}
				return this;
			}

			///<summary>Flips the bits for pressMs, then back for releaseMs.</summary>
			public Script Press(ControlId control,int bits,int pressMs,int releaseMs=300) {
				return Toggle(control,bits).Hold(pressMs).Toggle(control,bits).Hold(releaseMs);
			}
		}
	}
}
