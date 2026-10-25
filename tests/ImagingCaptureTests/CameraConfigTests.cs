// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImagingCapture.Camera;
using ImagingCapture.Uvc;
using ImagingDeviceManager.Config;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingCapture.Tests {
	///<summary>The camera sections of capture.ini (design 9.3) through CameraConfig over LocalCaptureConfig.</summary>
	[TestClass]
	public class CameraConfigTests {
		private string _folder;
		private string _path;

		[TestInitialize]
		public void Init() {
			_folder=Path.Combine(Path.GetTempPath(),"ImagingCaptureTests_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_folder);
			_path=Path.Combine(_folder,"capture.ini");
		}

		[TestCleanup]
		public void Cleanup() {
			try {
				Directory.Delete(_folder,true);
			}
			catch {
			}
		}

		private CameraConfig ReadText(string text) {
			File.WriteAllText(_path,text);
			return CameraConfig.Read(LocalCaptureConfig.Load(_path));
		}

		private static void AssertNoTriggerValues(CameraConfig cameraConfig) {
			Assert.IsNull(cameraConfig.DebounceMs);
			Assert.AreEqual(300,cameraConfig.EffectiveDebounceMs);
			Assert.IsFalse(cameraConfig.StillEnabled);
			Assert.IsFalse(cameraConfig.ControlPoll.Enabled);
			Assert.IsNull(cameraConfig.ControlPoll.Interface);
			Assert.IsNull(cameraConfig.ControlPoll.Property);
			Assert.IsNull(cameraConfig.ControlPoll.Mask);
			Assert.IsNull(cameraConfig.ControlPoll.ActiveLevel);
			Assert.IsNull(cameraConfig.ControlPoll.GetBinding());
			Assert.AreEqual(50,cameraConfig.PollMs);
			foreach(LevelInputConfig levelInputConfig in cameraConfig.Levels) {
				Assert.IsFalse(levelInputConfig.Enabled);
				Assert.IsNull(levelInputConfig.GetBinding());
			}
		}

		[TestMethod]
		public void MissingFile_DefaultsHaveNoCameraTriggerOrLevel() {
			CameraConfig cameraConfig=CameraConfig.Read(LocalCaptureConfig.Load(_path));
			Assert.AreEqual("",cameraConfig.Moniker);
			Assert.AreEqual("",cameraConfig.Name);
			Assert.AreEqual(0,cameraConfig.Width);
			Assert.AreEqual(0,cameraConfig.Height);
			Assert.AreEqual("auto",cameraConfig.Subtype);
			Assert.AreEqual(CameraPhotoSource.Preview,cameraConfig.PhotoSource);
			Assert.IsTrue(cameraConfig.MainWindowSpace);
			Assert.AreEqual(0,cameraConfig.Levels.Count);
			AssertNoTriggerValues(cameraConfig);
			Assert.AreEqual(300,new CameraConfig().EffectiveDebounceMs,"A new CameraConfig has the same defaults.");
			AssertNoTriggerValues(new CameraConfig());
			AssertNoTriggerValues(CameraConfig.Read(null));
		}

		[TestMethod]
		public void DesignSampleBlock_ReadsAsNotConfigured() {
			CameraConfig cameraConfig=ReadText(
				"[Camera]\r\nMoniker=\r\nName=\r\nWidth=0\r\nHeight=0\r\nSubtype=auto              ; auto | MJPG | YUY2 | RGB24\r\n"
				+"PhotoSource=Preview       ; Preview | Still\r\nMainWindowSpace=1\r\n\r\n"
				+"[Trigger]\r\nDebounceMs=               ; empty = learned value, else 300\r\n"
				+"[Trigger.Still]\r\nEnabled=0\r\n"
				+"[Trigger.ControlPoll]\r\nEnabled=0\r\nInterface=                ; CameraControl | VideoProcAmp\r\nProperty=\r\nMask=\r\n"
				+"ActiveLevel=              ; High | Low\r\nPollMs=50\r\n\r\n"
				+"[Level.1]\r\nEnabled=0\r\nInterface=\r\nProperty=\r\nMask=\r\nActiveLevel=\r\nAction=PausePreview\r\n");
			AssertNoTriggerValues(cameraConfig);
			Assert.AreEqual(1,cameraConfig.Levels.Count);
			Assert.AreEqual(1,cameraConfig.Levels[0].Number);
			Assert.AreEqual(LevelAction.PausePreview,cameraConfig.Levels[0].Action);
			Assert.IsFalse(cameraConfig.Levels[0].IsLevelConfigured);
			Assert.AreEqual("",cameraConfig.Levels[0].GetProblem(),"A level that is off has no problem.");
		}

		[TestMethod]
		public void FullValues_AreRead() {
			CameraConfig cameraConfig=ReadText(
				"[Camera]\nMoniker=@device:test:1\nName=Test camera\nWidth=1280\nHeight=720\nSubtype=yuy2\nPhotoSource=still\nMainWindowSpace=0\n"
				+"[Trigger]\nDebounceMs=360\n[Trigger.Still]\nEnabled=1\n"
				+"[trigger.controlpoll]\nEnabled=1\nInterface=videoprocamp\nProperty=9\nMask=0x10\nActiveLevel=low\nPollMs=40\n"
				+"[Level.2]\nEnabled=1\nInterface=CameraControl\nProperty=6\nMask=8\nActiveLevel=High\nAction=PausePreview\n");
			Assert.AreEqual("@device:test:1",cameraConfig.Moniker);
			Assert.AreEqual("Test camera",cameraConfig.Name);
			Assert.AreEqual(1280,cameraConfig.Width);
			Assert.AreEqual(720,cameraConfig.Height);
			Assert.AreEqual("yuy2",cameraConfig.Subtype);
			Assert.AreEqual(CameraPhotoSource.Still,cameraConfig.PhotoSource);
			Assert.IsFalse(cameraConfig.MainWindowSpace);
			Assert.AreEqual(360,cameraConfig.DebounceMs);
			Assert.AreEqual(360,cameraConfig.EffectiveDebounceMs);
			Assert.IsTrue(cameraConfig.StillEnabled);
			Assert.AreEqual(40,cameraConfig.PollMs);
			ControlBinding binding=cameraConfig.ControlPoll.GetBinding();
			Assert.IsTrue(cameraConfig.ControlPoll.IsConfigured);
			Assert.AreEqual(new ControlId(UvcControlInterface.VideoProcAmp,9),binding.Control);
			Assert.AreEqual(0x10,binding.Mask);
			Assert.AreEqual(ControlLevel.Low,binding.ActiveLevel);
			Assert.AreEqual(1,cameraConfig.Levels.Count);
			LevelInputConfig level=cameraConfig.GetLevel(2);
			Assert.IsTrue(level.IsLevelConfigured);
			Assert.AreEqual(8,level.Mask,"A mask without 0x is decimal.");
			Assert.AreEqual(new ControlId(UvcControlInterface.CameraControl,6),level.GetBinding().Control);
		}

		[TestMethod]
		public void InvalidValues_ReadAsNotConfigured() {
			CameraConfig cameraConfig=ReadText(
				"[Camera]\nWidth=-5\nSubtype=\nPhotoSource=Somewhere\n"
				+"[Trigger]\nDebounceMs=0\n"
				+"[Trigger.ControlPoll]\nEnabled=1\nInterface=Elsewhere\nProperty=x\nMask=0\nActiveLevel=Up\nPollMs=-5\n");
			Assert.AreEqual(0,cameraConfig.Width);
			Assert.AreEqual("auto",cameraConfig.Subtype);
			Assert.AreEqual(CameraPhotoSource.Preview,cameraConfig.PhotoSource);
			Assert.IsNull(cameraConfig.DebounceMs);
			Assert.AreEqual(50,cameraConfig.PollMs);
			Assert.IsTrue(cameraConfig.ControlPoll.Enabled);
			Assert.IsFalse(cameraConfig.ControlPoll.IsConfigured);
			Assert.IsNull(cameraConfig.ControlPoll.GetBinding());
			string problem=cameraConfig.ControlPoll.GetProblem();
			StringAssert.Contains(problem,"Interface, Property, Mask, ActiveLevel are empty or invalid");
			Assert.IsNull(ReadText("[Trigger]\nDebounceMs=abc\n").DebounceMs);
			Assert.IsNull(ReadText("[Trigger]\nDebounceMs=60001\n").DebounceMs);
			Assert.AreEqual(60000,ReadText("[Trigger]\nDebounceMs=60000\n").DebounceMs);
		}

		[TestMethod]
		public void PropertyOutsideTheInterfaceRange_IsNotConfigured() {
			CameraConfig cameraConfig=ReadText("[Trigger.ControlPoll]\nEnabled=1\nInterface=CameraControl\nProperty=7\nMask=0x1\nActiveLevel=High\n");
			Assert.AreEqual(7,cameraConfig.ControlPoll.Property);
			Assert.IsNull(cameraConfig.ControlPoll.GetBinding(),"CameraControl has properties 0-6.");
			StringAssert.Contains(cameraConfig.ControlPoll.GetProblem(),"Property is empty or invalid");
			cameraConfig=ReadText("[Trigger.ControlPoll]\nEnabled=1\nInterface=VideoProcAmp\nProperty=9\nMask=0x1\nActiveLevel=High\n");
			Assert.IsNotNull(cameraConfig.ControlPoll.GetBinding(),"VideoProcAmp has properties 0-9.");
			cameraConfig=ReadText("[Trigger.ControlPoll]\nEnabled=1\nInterface=VideoProcAmp\nProperty=10\nMask=0x1\nActiveLevel=High\n");
			Assert.IsNull(cameraConfig.ControlPoll.GetBinding());
		}

		[TestMethod]
		public void PollMs_IsHeldToItsRange() {
			Assert.AreEqual(10,ReadText("[Trigger.ControlPoll]\nPollMs=1\n").EffectivePollMs);
			Assert.AreEqual(1000,ReadText("[Trigger.ControlPoll]\nPollMs=5000\n").EffectivePollMs);
			Assert.AreEqual(50,ReadText("[Trigger.ControlPoll]\nPollMs=\n").EffectivePollMs);
			Assert.AreEqual(75,ReadText("[Trigger.ControlPoll]\nPollMs=75\n").EffectivePollMs);
		}

		[TestMethod]
		public void Levels_NumbersAndActions() {
			CameraConfig cameraConfig=ReadText(
				"[Level.3]\nEnabled=1\nInterface=CameraControl\nProperty=0\nMask=0x2\nActiveLevel=High\n"
				+"[Level.1]\nEnabled=1\nInterface=CameraControl\nProperty=0\nMask=0x4\nActiveLevel=Low\nAction=\n"
				+"[Level.2]\nAction=Dance\n[Level.01]\nEnabled=1\n[Level.0]\nEnabled=1\n[Level.x]\nEnabled=1\n");
			CollectionAssert.AreEqual(new[] { 1,2,3 },cameraConfig.Levels.Select(x => x.Number).ToArray(),"Level.01, Level.0 and Level.x are not level sections.");
			Assert.IsNull(cameraConfig.GetLevel(1).Action,"An empty Action means none.");
			Assert.IsFalse(cameraConfig.GetLevel(1).IsLevelConfigured);
			StringAssert.Contains(cameraConfig.GetLevel(1).GetProblem(),"Action is empty or unknown");
			Assert.IsNull(cameraConfig.GetLevel(2).Action,"An unknown Action means none.");
			Assert.AreEqual(LevelAction.PausePreview,cameraConfig.GetLevel(3).Action,"A missing Action is the design's default.");
			Assert.IsTrue(cameraConfig.GetLevel(3).IsLevelConfigured);
		}

		[TestMethod]
		public void RoundTrip_WriteSaveRead() {
			LocalCaptureConfig config=LocalCaptureConfig.Load(_path);
			CameraConfig cameraConfig=CameraConfig.Read(config);
			cameraConfig.Moniker="@device:test:7";
			cameraConfig.Name="Round trip";
			cameraConfig.Width=1920;
			cameraConfig.Height=1080;
			cameraConfig.Subtype="MJPG";
			cameraConfig.PhotoSource=CameraPhotoSource.Still;
			cameraConfig.MainWindowSpace=false;
			cameraConfig.DebounceMs=250;
			cameraConfig.StillEnabled=true;
			cameraConfig.ControlPoll.Enabled=true;
			cameraConfig.ControlPoll.SetBinding(new ControlBinding(new ControlId(UvcControlInterface.CameraControl,3),unchecked((int)0x80000000),ControlLevel.High));
			cameraConfig.PollMs=30;
			LevelInputConfig level=new LevelInputConfig(1) { Enabled=true };
			level.SetBinding(new ControlBinding(new ControlId(UvcControlInterface.VideoProcAmp,0),0x100,ControlLevel.Low));
			cameraConfig.Levels.Add(level);
			string error;
			Assert.IsTrue(cameraConfig.Write(config,out error),error);
			Assert.IsTrue(config.Save(),config.LastError);
			string text=File.ReadAllText(_path);
			StringAssert.Contains(text,"Mask=0x80000000");
			StringAssert.Contains(text,"Action=PausePreview");
			CameraConfig cameraConfigRead=CameraConfig.Read(LocalCaptureConfig.Load(_path));
			Assert.AreEqual("@device:test:7",cameraConfigRead.Moniker);
			Assert.AreEqual("Round trip",cameraConfigRead.Name);
			Assert.AreEqual(1920,cameraConfigRead.Width);
			Assert.AreEqual(1080,cameraConfigRead.Height);
			Assert.AreEqual("MJPG",cameraConfigRead.Subtype);
			Assert.AreEqual(CameraPhotoSource.Still,cameraConfigRead.PhotoSource);
			Assert.IsFalse(cameraConfigRead.MainWindowSpace);
			Assert.AreEqual(250,cameraConfigRead.DebounceMs);
			Assert.IsTrue(cameraConfigRead.StillEnabled);
			Assert.AreEqual(30,cameraConfigRead.PollMs);
			Assert.AreEqual(cameraConfig.ControlPoll.GetBinding().ToString(),cameraConfigRead.ControlPoll.GetBinding().ToString());
			Assert.AreEqual(unchecked((int)0x80000000),cameraConfigRead.ControlPoll.Mask);
			Assert.AreEqual(1,cameraConfigRead.Levels.Count);
			Assert.AreEqual(level.GetBinding().ToString(),cameraConfigRead.GetLevel(1).GetBinding().ToString());
			Assert.AreEqual(LevelAction.PausePreview,cameraConfigRead.GetLevel(1).Action);
			//Writing what was read changes nothing.
			LocalCaptureConfig config2=LocalCaptureConfig.Load(_path);
			Assert.IsTrue(CameraConfig.Read(config2).Write(config2,out error),error);
			Assert.IsFalse(config2.HasUnsavedChanges);
		}

		[TestMethod]
		public void Write_SetsOnlyChangedKeys() {
			string text="; site notes\r\n[Camera]\r\nWidth=640   ; kept comment\r\n[Trigger.ControlPoll]\r\nEnabled=0\r\nMask=zz   ; hand edit\r\n";
			File.WriteAllText(_path,text);
			LocalCaptureConfig config=LocalCaptureConfig.Load(_path);
			CameraConfig cameraConfig=CameraConfig.Read(config);
			string error;
			Assert.IsTrue(cameraConfig.Write(config,out error),error);
			Assert.IsFalse(config.HasUnsavedChanges,"Nothing changed, so nothing is set; the invalid Mask keeps its text.");
			cameraConfig.Width=800;
			Assert.IsTrue(cameraConfig.Write(config,out error),error);
			Assert.IsTrue(config.Save(),config.LastError);
			Assert.AreEqual(text.Replace("Width=640   ;","Width=800   ;"),File.ReadAllText(_path));
		}

		[TestMethod]
		public void Write_RemovedLevelIsDisabledAndEmptied() {
			File.WriteAllText(_path,"[Level.1]\r\nEnabled=1\r\nInterface=CameraControl\r\nProperty=1\r\nMask=0x1\r\nActiveLevel=High\r\nAction=PausePreview\r\n");
			LocalCaptureConfig config=LocalCaptureConfig.Load(_path);
			CameraConfig cameraConfig=CameraConfig.Read(config);
			cameraConfig.Levels.Clear();
			string error;
			Assert.IsTrue(cameraConfig.Write(config,out error),error);
			Assert.IsTrue(config.Save(),config.LastError);
			CameraConfig cameraConfigRead=CameraConfig.Read(LocalCaptureConfig.Load(_path));
			Assert.AreEqual(1,cameraConfigRead.Levels.Count);
			Assert.IsFalse(cameraConfigRead.GetLevel(1).Enabled);
			Assert.IsNull(cameraConfigRead.GetLevel(1).GetBinding());
			Assert.IsNull(cameraConfigRead.GetLevel(1).Interface);
			Assert.IsNull(cameraConfigRead.GetLevel(1).Mask);
			Assert.AreEqual(LevelAction.PausePreview,cameraConfigRead.GetLevel(1).Action);
		}

		[TestMethod]
		public void SetDevice_CutsNativeStringsAtNul() {
			CameraConfig cameraConfig=new CameraConfig();
			cameraConfig.SetDevice(new UvcDeviceInfo("Lab camera;HD\0garbage\r\n",@"\\?\fake#1","@device:fake:1\0more"));
			Assert.AreEqual("@device:fake:1",cameraConfig.Moniker);
			Assert.AreEqual("Lab camera HD",cameraConfig.Name,"';' would start an INI comment, so it becomes a space.");
			LocalCaptureConfig config=LocalCaptureConfig.Load(_path);
			string error;
			Assert.IsTrue(cameraConfig.Write(config,out error),error);
			Assert.IsTrue(config.Save(),config.LastError);
			CameraConfig cameraConfigRead=CameraConfig.Read(LocalCaptureConfig.Load(_path));
			Assert.AreEqual("@device:fake:1",cameraConfigRead.Moniker);
			Assert.AreEqual("Lab camera HD",cameraConfigRead.Name);
			Assert.AreEqual("abc",CameraConfig.CutAtNul("abc\0def"));
			Assert.AreEqual("",CameraConfig.CutAtNul(null));
		}

		[TestMethod]
		public void Write_UnwritableValueSetsNothing() {
			LocalCaptureConfig config=LocalCaptureConfig.Load(_path);
			CameraConfig cameraConfig=new CameraConfig();
			cameraConfig.Width=640;
			cameraConfig.Moniker="@device;bad";
			string error;
			Assert.IsFalse(cameraConfig.Write(config,out error));
			StringAssert.Contains(error,"[Camera] Moniker");
			Assert.IsFalse(config.HasUnsavedChanges,"Width was not set either.");
		}

		[TestMethod]
		public void FindSavedDevice_MonikerThenName() {
			List<UvcDeviceInfo> listDevices=new List<UvcDeviceInfo> {
				new UvcDeviceInfo("Camera A","","@device:fake:a"),
				new UvcDeviceInfo("Camera B","","@device:fake:b"),
				new UvcDeviceInfo("Camera B","","@device:fake:b2"),
			};
			CameraConfig cameraConfig=new CameraConfig { Moniker="@DEVICE:FAKE:B2",Name="Camera B" };
			Assert.AreSame(listDevices[2],cameraConfig.FindSavedDevice(listDevices),"The moniker wins, ignoring case.");
			cameraConfig.Moniker="@device:fake:gone";
			Assert.AreSame(listDevices[1],cameraConfig.FindSavedDevice(listDevices),"Moved to another port: the first with the saved name.");
			cameraConfig.Name="Camera C";
			Assert.IsNull(cameraConfig.FindSavedDevice(listDevices));
			Assert.IsNull(new CameraConfig().FindSavedDevice(listDevices),"Nothing saved: the caller takes the first device.");
			Assert.IsNull(cameraConfig.FindSavedDevice(null));
		}

		[TestMethod]
		public void LearnedDebounce_TwiceTheLongestPressAtLeast200() {
			Assert.AreEqual(200,CameraConfig.GetLearnedDebounceMs(0));
			Assert.AreEqual(200,CameraConfig.GetLearnedDebounceMs(60));
			Assert.AreEqual(200,CameraConfig.GetLearnedDebounceMs(100));
			Assert.AreEqual(300,CameraConfig.GetLearnedDebounceMs(150));
			Assert.AreEqual(1000,CameraConfig.GetLearnedDebounceMs(500));
			Assert.AreEqual(60000,CameraConfig.GetLearnedDebounceMs(int.MaxValue),"Held to the largest valid DebounceMs.");
		}

		[TestMethod]
		public void Clone_IsDeep() {
			CameraConfig cameraConfig=new CameraConfig();
			cameraConfig.ControlPoll.Enabled=true;
			cameraConfig.Levels.Add(new LevelInputConfig(1) { Enabled=true });
			CameraConfig cameraConfigClone=cameraConfig.Clone();
			cameraConfigClone.ControlPoll.Enabled=false;
			cameraConfigClone.Levels[0].Enabled=false;
			cameraConfigClone.Levels.Add(new LevelInputConfig(2));
			Assert.IsTrue(cameraConfig.ControlPoll.Enabled);
			Assert.IsTrue(cameraConfig.Levels[0].Enabled);
			Assert.AreEqual(1,cameraConfig.Levels.Count);
		}

		[TestMethod]
		public void ControlValueFormat_TextForms() {
			Assert.AreEqual("0x10",ControlValueFormat.ToHex(16));
			Assert.AreEqual("0xFFFFFFFF",ControlValueFormat.ToHex(-1));
			Assert.AreEqual("0x0010",ControlValueFormat.ToHexPadded(16));
			Assert.AreEqual("0x00012345",ControlValueFormat.ToHexPadded(0x12345));
			Assert.AreEqual("0000 0000 0001 0000",ControlValueFormat.ToBinary(16));
			Assert.AreEqual("1111 1111 1111 1111 1111 1111 1111 1110",ControlValueFormat.ToBinary(-2));
			Assert.AreEqual("-5",ControlValueFormat.ToDecimal(-5));
			int value;
			Assert.IsTrue(ControlValueFormat.TryParse(" 0x80000000 ",out value));
			Assert.AreEqual(int.MinValue,value);
			Assert.IsTrue(ControlValueFormat.TryParse("4294967295",out value));
			Assert.AreEqual(-1,value);
			Assert.IsTrue(ControlValueFormat.TryParse("16",out value));
			Assert.AreEqual(16,value);
			Assert.IsFalse(ControlValueFormat.TryParse("0x",out value));
			Assert.IsFalse(ControlValueFormat.TryParse("0x123456789",out value));
			Assert.IsFalse(ControlValueFormat.TryParse("ten",out value));
			Assert.IsFalse(ControlValueFormat.TryParse(null,out value));
		}

		[TestMethod]
		public void ControlBinding_ActiveLevels() {
			ControlId control=new ControlId(UvcControlInterface.CameraControl,0);
			ControlBinding bindingHigh=new ControlBinding(control,0x6,ControlLevel.High);
			Assert.IsTrue(bindingHigh.IsActive(0x6));
			Assert.IsTrue(bindingHigh.IsActive(0xF6));
			Assert.IsFalse(bindingHigh.IsActive(0x2),"High needs every mask bit set.");
			ControlBinding bindingLow=new ControlBinding(control,0x6,ControlLevel.Low);
			Assert.IsTrue(bindingLow.IsActive(0xF1));
			Assert.IsFalse(bindingLow.IsActive(0x4),"Low needs every mask bit clear.");
			Assert.IsFalse(bindingHigh.IsSingleBit);
			Assert.IsTrue(new ControlBinding(control,0x40,ControlLevel.High).IsSingleBit);
			TestUtil.AssertThrows<ArgumentException>(() => new ControlBinding(control,0,ControlLevel.High));
			TestUtil.AssertThrows<ArgumentException>(() => new ControlBinding(new ControlId(UvcControlInterface.CameraControl,7),1,ControlLevel.High));
			Assert.AreEqual(17,ControlId.GetAll().Count);
		}
	}
}
