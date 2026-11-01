// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ImagingCapture.Settings;
using ImagingCapture.UI;
using ImagingDeviceManager.Capture;
using ImagingDeviceManager.Config;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace ImagingCapture.Tests {
	///<summary>TestCaptureModel, CaptureVerificationRecord and CaptureEndText (design 7.8, 7.12, 9.3, 13 step 8): Test capture's state, texts
	///and verification record, driven with made-up session events. No sensor, no window and no database.</summary>
	[TestClass]
	public class TestCaptureModelTests {
		private readonly List<string> _listPaths=new List<string>();
		private const string Ini=@"[Twain]
Allow16Bit=1
RestrictSources=1
AllowedSources=Example Sensor DS
[Device.12]
Bits=16
ByteOrder16=LE
Polarity=AsDelivered
SafeDisableCap=0x8101
[Camera]
Name=Not gated
";

		[TestCleanup]
		public void Cleanup() {
			foreach(string path in _listPaths) {
				try {
					File.Delete(path);
				}
				catch {
				}
			}
		}

		private LocalCaptureConfig Load(string iniText) {
			string path=Path.Combine(Path.GetTempPath(),"ImagingCaptureTests_"+Guid.NewGuid().ToString("N")+".ini");
			_listPaths.Add(path);
			File.WriteAllText(path,iniText,new UTF8Encoding(false));
			return LocalCaptureConfig.Load(path);
		}

		private static List<ImageCategoryItem> Categories() {
			return new List<ImageCategoryItem> {
				new ImageCategoryItem(10,"X-rays","X",false),
				new ImageCategoryItem(12,"Patient pictures","MP",false),
				new ImageCategoryItem(14,"Photos","",false),
			};
		}

		///<summary>isSettingsSaved: the Capture settings screen saved exactly this file for the device on OPPC1 (design 9.3), else never.</summary>
		private TestCaptureModel Model(long imagingDeviceNum=12,List<MountTemplateCategory> listMountTemplates=null,bool isSettingsSaved=true,
			string iniText=Ini)
		{
			TestCaptureDevice device=new TestCaptureDevice { ImagingDeviceNum=imagingDeviceNum,Description="Intraoral sensor",TwainName="Example Sensor DS" };
			LocalCaptureConfig config=Load(iniText);
			CaptureSettingsCheck settingsCheck=isSettingsSaved ? CaptureSettingsTestKit.Verified(config,imagingDeviceNum,"OPPC1")
				: CaptureSettingsCheck.Check(config,imagingDeviceNum,"OPPC1",new List<SecurityLog>());
			return new TestCaptureModel(device,config,"OPPC1",new ImageCategoryRule(Categories),
				listMountTemplates??new List<MountTemplateCategory> { new MountTemplateCategory { Description="FMX",DefaultCat=10 } },null,settingsCheck);
		}

		private static readonly TwainDriverIdentity _driver=new TwainDriverIdentity("Example Sensor DS",1,1,"1.1.0");

		private static TwainCaptureOpenResult Opened(TwainDriverIdentity driverIdentity=null) {
			TwainDriverIdentity driver=driverIdentity??_driver;
			return new TwainCaptureOpenResult { IsOpen=true,SourceInfo=new TwainCaptureSourceInfo { ProductName="Example Sensor DS",ListBitDepths=new List<int> { 8 },
				VersionMajor=driver.MajorNum,VersionMinor=driver.MinorNum,VersionInfo=driver.Info,DriverIdentity=driver } };
		}

		private static AcquiredImage Image16() {
			return TestImages.Image16(64,16,(x,y) => (ushort)(200+x*50+y%3),TestImages.Json16Bit12);
		}

		[TestMethod]
		public void Settings_EffectiveValuesAndGatedHash() {
			TestCaptureModel model=Model();
			LocalCaptureConfig config=Load(Ini);
			Assert.AreEqual(GatedSettingsHash.Compute(config,12),model.GatedSettingsHashValue);
			Assert.AreEqual(16,model.Options.Bits);
			Assert.IsTrue(model.Options.IsAbove8BitConfigured);
			string text=model.GetSettingsText();
			StringAssert.Contains(text,"Bits=16");
			StringAssert.Contains(text,"SafeDisableCap=0x8101");
			StringAssert.Contains(text,"More than 8 bits configured: yes, 16 bits");
			StringAssert.Contains(text,"SHA-256 "+model.GatedSettingsHashValue);
			StringAssert.Contains(text,"Twain.AllowedSources=example sensor ds","The gated values as hashed.");
			Assert.IsFalse(text.Contains("Not gated"),"Hardware bindings are not shown as gated values.");
			model.OnOpenResult(Opened());
			StringAssert.Contains(model.GetSettingsText(),"Bit depths at open: 8");
		}

		[TestMethod]
		public void SettingsNeverSaved_SafeValuesInEffect_NoRecord_SaidPlainly() {
			TestCaptureModel model=Model(isSettingsSaved:false);
			Assert.AreEqual(CaptureSettingsStatus.NeverSaved,model.SettingsCheck.Status);
			StringAssert.StartsWith(model.SettingsCheckText,CaptureSettingsCheck.TextNeverSaved);
			StringAssert.Contains(model.SettingsCheckText,"capture of more than 8 bits is not verified");
			Assert.AreEqual(8,model.BitsToRequest,"The safe values force 8 bits.");
			CollectionAssert.AreEqual(new[] { 8 },model.ListBitsChoices);
			Assert.AreEqual(8,model.Options.Bits,"Design 9.3 safe values.");
			Assert.IsFalse(model.Options.Allow16Bit);
			Assert.IsNull(model.Options.SafeDisableCap);
			Assert.IsTrue(model.Options.RestrictSources,"Never saved: the source gate keeps the file's values.");
			CollectionAssert.AreEqual(new[] { "Example Sensor DS" },model.Options.AllowedSources);
			StringAssert.Contains(model.GetSettingsText(),"Safe values in effect");
			StringAssert.Contains(model.GetSettingsText(),"Twain.Allow16Bit=1","The file's gated values and hash are still shown.");
			model.OnOpenResult(Opened());
			model.OnEnablePosted(1);
			model.OnImage(Image16());
			Assert.IsFalse(model.CanWriteRecord,"A record would claim the test ran with the file's settings.");
			string error;
			Assert.IsNull(model.BuildRecord(DateTime.Now,out error));
			StringAssert.Contains(error,"safe values");
		}

		[TestMethod]
		public void SettingsChangedOutside_RestrictSourcesAsLastSaved() {
			LocalCaptureConfig configSaved=Load("[Twain]\r\nRestrictSources=1\r\nAllowedSources=Scanner\r\n[Device.12]\r\nBits=8\r\n");
			LocalCaptureConfig config=Load(Ini);
			CaptureSettingsCheck settingsCheck=CaptureSettingsCheck.Check(config,12,"OPPC1",
				new List<SecurityLog> { CaptureSettingsTestKit.SavedLog(configSaved,12,"OPPC1") });
			Assert.AreEqual(CaptureSettingsStatus.ChangedOutside,settingsCheck.Status);
			TestCaptureDevice device=new TestCaptureDevice { ImagingDeviceNum=12,Description="Intraoral sensor",TwainName="Example Sensor DS" };
			TestCaptureModel model=new TestCaptureModel(device,config,"OPPC1",new ImageCategoryRule(Categories),null,null,settingsCheck);
			StringAssert.StartsWith(model.SettingsCheckText,"Capture settings were changed outside Open Dental; review them in Setup.");
			CollectionAssert.AreEqual(new[] { "Scanner" },model.Options.AllowedSources,"RestrictSources and AllowedSources as last saved.");
			Assert.IsTrue(model.Options.RestrictSources);
			Assert.AreEqual(8,model.Options.Bits);
			Assert.IsFalse(model.CanWriteRecord);
		}

		[TestMethod]
		public void SettingsNotChecked_CountAsNotVerified() {
			TestCaptureDevice device=new TestCaptureDevice { ImagingDeviceNum=12,Description="Intraoral sensor",TwainName="Example Sensor DS" };
			TestCaptureModel model=new TestCaptureModel(device,Load(Ini),"OPPC1",new ImageCategoryRule(Categories),null,null,null);
			Assert.AreEqual(CaptureSettingsStatus.Unknown,model.SettingsCheck.Status);
			Assert.AreEqual(8,model.Options.Bits);
			StringAssert.Contains(model.SettingsCheckText,"not verified");
		}

		[TestMethod]
		public void SettingsVerified_NoText_FileValuesInEffect() {
			TestCaptureModel model=Model();
			Assert.IsTrue(model.SettingsCheck.IsVerified);
			Assert.AreEqual("",model.SettingsCheckText);
			Assert.AreEqual(16,model.Options.Bits);
			Assert.IsFalse(model.GetSettingsText().Contains("Safe values"));
		}

		[TestMethod]
		public void Flow_OpenEnableImageEnded() {
			TestCaptureModel model=Model();
			Assert.AreEqual(TestCaptureState.Closed,model.State);
			Assert.IsTrue(model.CanStart);
			Assert.IsFalse(model.CanStop);
			Assert.IsFalse(model.CanWriteRecord);
			model.OnOpening();
			Assert.AreEqual(TestCaptureState.Opening,model.State);
			Assert.IsFalse(model.CanStart);
			model.OnOpenResult(Opened());
			Assert.AreEqual(TestCaptureState.Open,model.State);
			TwainEnableRequest request=model.CreateEnableRequest();
			Assert.AreEqual(16,request.Bits,"The device's Bits, 16 by default: what the session will ask for.");
			Assert.AreEqual(TestCaptureModel.TargetLabel,request.TargetLabel);
			Assert.IsNull(request.ShowUI,"The [Device.N] Headless setting decides, as in the session.");
			Assert.AreEqual("1",request.SidecarFields.Single(x => x.Key==CaptureSidecarKeys.TestCapture).Value,
				"Its images' sidecars say so: a test image is never filed to a patient.");
			model.OnEnablePosted(1);
			Assert.AreEqual(TestCaptureState.Enabled,model.State);
			Assert.IsTrue(model.CanStop);
			model.OnStatus(new TwainCaptureStatus { State=TwainCaptureState.WaitingForImage,EnableId=1,Text="Sensor software open, waiting for exposure" });
			Assert.AreEqual("Sensor software open, waiting for exposure",model.StatusText);
			model.OnImage(Image16());
			Assert.AreEqual(1,model.ImageCount);
			Assert.IsNotNull(model.Analysis);
			Assert.IsTrue(model.CanWriteRecord);
			Assert.IsTrue(model.IsByteOrderCheckable);
			model.OnEnded(new TwainCaptureEnded { EnableId=1,Reason=TwainCaptureEndReason.Completed,ImageCount=1,BitsRequested=16,BitDepthInEffect=16 });
			Assert.AreEqual(TestCaptureState.Open,model.State,"The source stays open in state 4.");
			Assert.AreEqual("Test image received.",model.MessageText);
			StringAssert.Contains(model.GetResultText(),"Delivered: 16-bit grey over memory transfer (asked for 16).");
			model.BitsToRequest=8;
			Assert.AreEqual(8,model.CreateEnableRequest().Bits);
			model.BitsToRequest=14;
			Assert.AreEqual(14,model.CreateEnableRequest().Bits);
			model.BitsToRequest=12;
			Assert.AreEqual(8,model.BitsToRequest,"A depth that is not 8, 14 or 16 is 8.");
			CollectionAssert.AreEqual(new[] { 16,14,8 },model.ListBitsChoices);
			model.OnStatus(new TwainCaptureStatus { State=TwainCaptureState.Closed });
			Assert.AreEqual(TestCaptureState.Closed,model.State);
		}

		[TestMethod]
		public void OpenFailed_TextsOfDesign712() {
			TestCaptureModel model=Model();
			model.OnOpening();
			model.OnOpenResult(new TwainCaptureOpenResult { Error=TwainCaptureOpenError.SourceNotFound });
			Assert.AreEqual(TestCaptureState.Closed,model.State);
			Assert.AreEqual("Imaging device 'Example Sensor DS' not found. Check Setup > Imaging > Devices.",model.MessageText);
			Assert.AreEqual(CaptureEndText.NoDsm,CaptureEndText.GetOpenText(new TwainCaptureOpenResult { Error=TwainCaptureOpenError.NoDsm },"",TimeSpan.Zero));
			Assert.AreEqual(CaptureEndText.SourceNotEnabledHere,
				CaptureEndText.GetOpenText(new TwainCaptureOpenResult { Error=TwainCaptureOpenError.SourceNotEnabledHere },"",TimeSpan.Zero));
			StringAssert.Contains(CaptureEndText.GetOpenText(new TwainCaptureOpenResult { Error=TwainCaptureOpenError.Timeout },"",TimeSpan.FromSeconds(60)),
				"within 60 seconds");
			StringAssert.Contains(CaptureEndText.GetOpenText(new TwainCaptureOpenResult { Error=TwainCaptureOpenError.Other,ConditionCode=0x0001 },"",TimeSpan.Zero),
				"0x0001");
			model.OnEnablePosted(0);
			Assert.AreEqual(TestCaptureState.Closed,model.State);
		}

		[TestMethod]
		public void EndedTexts() {
			Assert.AreEqual("",CaptureEndText.GetEndedText(new TwainCaptureEnded { Reason=TwainCaptureEndReason.Completed,ImageCount=1 }));
			Assert.AreEqual(CaptureEndText.SensorSoftwareClosed,
				CaptureEndText.GetEndedText(new TwainCaptureEnded { Reason=TwainCaptureEndReason.CloseRequested,ConditionCode=0 }));
			Assert.AreEqual("Sensor software closed without an image: 0x8123. Do not re-expose until you have checked the sensor software.",
				CaptureEndText.GetEndedText(new TwainCaptureEnded { Reason=TwainCaptureEndReason.CloseRequested,ConditionCode=0x8123 }));
			StringAssert.StartsWith(CaptureEndText.GetEndedText(new TwainCaptureEnded { Reason=TwainCaptureEndReason.CloseRequested,ConditionCode=0x8123,
				ConditionText="Not calibrated." }),"Sensor software closed without an image: Not calibrated.");
			Assert.AreEqual(CaptureEndText.ImageNotReceived,
				CaptureEndText.GetEndedText(new TwainCaptureEnded { Reason=TwainCaptureEndReason.TransferFailed,SourceMayHoldImages=true }));
			Assert.AreEqual(CaptureEndText.SensorNotConnected,CaptureEndText.GetEndedText(new TwainCaptureEnded { Reason=TwainCaptureEndReason.DeviceOffline }));
			Assert.AreEqual("Stopped (cancelled).",
				CaptureEndText.GetEndedText(new TwainCaptureEnded { Reason=TwainCaptureEndReason.Stopped,StopReason=TwainCaptureStopReason.Cancel }));
			StringAssert.Contains(CaptureEndText.GetEndedText(new TwainCaptureEnded { Reason=TwainCaptureEndReason.Stopped,
				StopReason=TwainCaptureStopReason.Cancel,WasSecondDisableAfterWait=true,SourceMayHoldImages=true }),CaptureEndText.MayStillHoldImages);
			//The way-out disable was refused and the source then offered everything in the second wait: nothing is held.
			Assert.AreEqual("Stopped (cancelled).",CaptureEndText.GetEndedText(new TwainCaptureEnded { Reason=TwainCaptureEndReason.Stopped,
				StopReason=TwainCaptureStopReason.Cancel,WasSecondDisableAfterWait=true }));
			StringAssert.Contains(CaptureEndText.GetEndedText(new TwainCaptureEnded { Reason=TwainCaptureEndReason.EnableFailed,ConditionCode=0x0011 }),
				"could not be started: 0x0011.");
			StringAssert.Contains(CaptureEndText.GetEndedText(new TwainCaptureEnded { Reason=TwainCaptureEndReason.Completed,ExtraImageCount=1 }),
				CaptureEndText.ExtraImages);
			Assert.AreEqual("Delivered at 8 bits: ICAP_BITDEPTH does not list 16.",CaptureEndText.GetEndedText(new TwainCaptureEnded {
				Reason=TwainCaptureEndReason.Completed,BitsRequested=16,ReasonFallback="ICAP_BITDEPTH does not list 16" }));
		}

		[TestMethod]
		public void Record_16Bit_ConfirmedPasses() {
			TestCaptureModel model=Model();
			string error;
			Assert.IsNull(model.BuildRecord(DateTime.Now,out error));
			Assert.AreEqual("Take a test image first.",error);
			model.OnOpenResult(Opened());
			model.OnEnablePosted(1);
			model.OnImage(Image16());
			model.IsByteOrderConfirmed=true;
			model.IsPolarityConfirmed=true;
			DateTime now=new DateTime(2026,10,31,15,4,5);
			CaptureVerificationRecord record=model.BuildRecord(now,out error);
			Assert.IsNotNull(record,error);
			Assert.AreEqual(12,record.ImagingDeviceNum);
			Assert.AreEqual("OPPC1",record.ComputerName,"For the audit trail.");
			Assert.AreEqual(model.GatedSettingsHashValue,record.GatedSettingsHash,"For the audit trail.");
			Assert.IsTrue(record.DriverIdentity.IsSameAs(_driver),"The driver the source reported at open.");
			Assert.AreEqual(CaptureImageSettingsHash.Compute(model.Options),record.ImageSettingsHash);
			Assert.AreEqual(model.ImageSettingsHashValue,record.ImageSettingsHash);
			Assert.AreEqual(16,record.BitDepthInEffect);
			Assert.AreEqual(now,record.DateTimeVerified);
			Assert.AreEqual("Example Sensor DS",record.SourceName);
			Assert.AreEqual("Example Sensor DS",record.SourceProductName);
			Assert.AreEqual(16,record.BitsRequested);
			Assert.AreEqual(16,record.BitsDelivered);
			Assert.AreEqual("Memory",record.TransferMechanism);
			Assert.AreEqual("LE",record.ByteOrder16);
			Assert.AreEqual("LooksRight",record.ByteOrderCheck);
			Assert.AreEqual("AsDelivered",record.Polarity);
			Assert.AreEqual("Chocolate",record.PixelFlavor);
			Assert.AreEqual(12,record.EffectiveBitsMeasured);
			Assert.AreEqual(12,record.EffectiveBitsReported);
			Assert.AreEqual(12,record.BitsStoredRule);
			Assert.AreEqual("SN-000123",record.Serial);
			Assert.IsTrue(record.IsMetadataPresent);
			Assert.IsTrue(record.IsPassedFor(16));
			Assert.AreEqual("",model.GetRecordWarning(record));
			Assert.IsTrue(record.Matches(12,"Example Sensor DS",_driver,model.ImageSettingsHashValue.ToUpperInvariant()));
			model.OnRecordWritten(record);
			StringAssert.Contains(model.GetResultText(),"verifies 16-bit capture. It covers 16-bit capture for driver Example Sensor DS, version 1.1 (1.1.0)");
			StringAssert.Contains(model.GetResultText(),"on every computer that runs them");
			//A new image needs new confirmations.
			model.OnImage(Image16());
			Assert.IsFalse(model.IsByteOrderConfirmed);
			Assert.IsFalse(model.IsPolarityConfirmed);
			record=model.BuildRecord(now,out error);
			Assert.IsFalse(record.IsPassedFor(16));
			StringAssert.Contains(model.GetRecordWarning(record),"the byte order is not confirmed");
			StringAssert.Contains(model.GetRecordWarning(record),"the polarity is not confirmed");
		}

		[TestMethod]
		public void Record_8Bit_DoesNotVerify16() {
			TestCaptureModel model=Model();
			model.OnOpenResult(Opened());
			model.OnEnablePosted(1);
			AcquiredImage image=TestImages.Image8(8,8,(x,y) => (byte)(x*y),TestImages.Json8Bit);
			image.RequestedBits=16;
			image.IsDeliveredAs8BitIn16BitEnable=true;
			model.OnImage(image);
			Assert.IsFalse(model.IsByteOrderCheckable);
			Assert.IsTrue(model.IsPolarityCheckable);
			model.IsPolarityConfirmed=true;
			string error;
			CaptureVerificationRecord record=model.BuildRecord(DateTime.Now,out error);
			Assert.AreEqual(8,record.BitsDelivered);
			Assert.AreEqual(CaptureVerificationRecord.ByteOrderCheckNotApplicable,record.ByteOrderCheck);
			Assert.IsNull(record.BitsStoredRule);
			Assert.IsFalse(record.IsPassed);
			Assert.AreEqual(8,record.BitDepthVerified);
			Assert.AreEqual("This record does not verify capture of more than 8 bits: the image is 8-bit.",model.GetRecordWarning(record));
		}

		[TestMethod]
		public void Record_UnsavedDevice_NoHash() {
			TestCaptureModel model=Model(imagingDeviceNum:0);
			Assert.AreEqual("",model.GatedSettingsHashValue);
			StringAssert.Contains(model.HashError,"could not be hashed");
			StringAssert.Contains(model.GetSettingsText(),"could not be hashed");
			model.OnImage(Image16());
			Assert.IsFalse(model.CanWriteRecord);
			string error;
			Assert.IsNull(model.BuildRecord(DateTime.Now,out error));
		}

		[TestMethod]
		public void Closing_IgnoresLateImages() {
			TestCaptureModel model=Model();
			model.OnClosing();
			model.OnImage(Image16());
			Assert.IsNull(model.Analysis);
			model.OnStatus(new TwainCaptureStatus { State=TwainCaptureState.Open });
			Assert.AreEqual(TestCaptureState.Closing,model.State);
			Assert.IsFalse(model.CanStart);
		}

		[TestMethod]
		public void CancelWarning_OnlyWithoutSafeDisable() {
			TestCaptureModel model=Model();
			Assert.AreEqual(CaptureEndText.CancelWarning,model.GetCancelWarning());
			model.OnEnded(new TwainCaptureEnded { Reason=TwainCaptureEndReason.Completed,IsSafeDisableInEffect=true });
			Assert.IsNull(model.GetCancelWarning());
		}

		[TestMethod]
		public void XrayCategory_PWarningAndTemplates() {
			List<MountTemplateCategory> listMountTemplates=new List<MountTemplateCategory> {
				new MountTemplateCategory { Description="BWX",DefaultCat=12 },
				new MountTemplateCategory { Description="FMX",DefaultCat=10 },
				new MountTemplateCategory { Description="PA",DefaultCat=10 },
				new MountTemplateCategory { Description="Any",DefaultCat=0 },
			};
			Assert.AreEqual(10,TestCaptureModel.GetDefaultCategory(listMountTemplates),"The category most templates use.");
			Assert.AreEqual(12,TestCaptureModel.GetDefaultCategory(listMountTemplates.Take(2)),"Equal counts: the first.");
			Assert.AreEqual(0,TestCaptureModel.GetDefaultCategory(null));
			TestCaptureModel model=Model(listMountTemplates:listMountTemplates);
			Assert.AreEqual(10,model.CategoryDefNum);
			Assert.AreEqual("Mount template \"BWX\" saves its X-rays into \"Patient pictures\" (usage P).",model.GetCategoryWarning());
			model.CategoryDefNum=12;
			Assert.AreEqual("\"Patient pictures\" is the patient picture category (usage P). Every X-ray saved in it becomes the patient's picture. "
				+"Choose a category without P.",model.GetCategoryWarning(),"The chosen category's warning; BWX's is the same category.");
			model.CategoryDefNum=14;
			StringAssert.Contains(model.GetCategoryWarning(),"BWX");
			Assert.AreEqual(3,model.GetCategoryPickList().Count);
		}

		[TestMethod]
		public void DeviceText_OtherComputerNoted() {
			TestCaptureDevice device=new TestCaptureDevice { ImagingDeviceNum=12,Description="Sensor",TwainName="Example Sensor DS",ComputerName="OPPC2" };
			LocalCaptureConfig config=Load(Ini);
			TestCaptureModel model=new TestCaptureModel(device,config,"OPPC1",new ImageCategoryRule(Categories),null,"2 unassigned capture(s) on this computer.",
				CaptureSettingsTestKit.Verified(config,12,"OPPC1"));
			StringAssert.Contains(model.GetDeviceText(),"set up for computer OPPC2");
			Assert.AreEqual("2 unassigned capture(s) on this computer.",model.UnassignedText);
			device.ComputerName="oppc1";
			Assert.IsFalse(model.GetDeviceText().Contains("set up for computer"));
		}

		[TestMethod]
		public void UnassignedNotice_OpensTheWindowOnlyWhileNotTaking_ReadAgainAfterwards() {
			TestCaptureDevice device=new TestCaptureDevice { ImagingDeviceNum=12,Description="Sensor",TwainName="Example Sensor DS",ComputerName="OPPC1" };
			LocalCaptureConfig config=Load(Ini);
			TestCaptureModel model=new TestCaptureModel(device,config,"OPPC1",new ImageCategoryRule(Categories),null,"2 unassigned capture(s) on this computer.",
				CaptureSettingsTestKit.Verified(config,12,"OPPC1"));
			Assert.IsTrue(model.CanOpenUnassignedCaptures);
			int version=model.Version;
			model.SetUnassignedText(null);
			Assert.IsNull(model.UnassignedText);
			Assert.IsTrue(model.Version>version);
			Assert.IsFalse(model.CanOpenUnassignedCaptures,"No notice, no link.");
		}

		///<summary>A 14-bit image in 16-bit containers from a 14-bit enable (the source listed 14, not 16).</summary>
		private static AcquiredImage Image14(int requestedBits=14) {
			AcquiredImage image=TestImages.Image16(64,16,(x,y) => (ushort)(200+x*200+y%3),TestImages.Json16Bit12);
			image.BitsPerSample=14;
			image.RequestedBits=requestedBits;
			image.BitDepthInEffect=14;
			image.BitDepthReason=requestedBits==16 ? "ICAP_BITDEPTH MSG_GET does not list 16 (8, 14)" : null;
			return image;
		}

		[TestMethod]
		public void BitsToRequest_TheDevicesBits_And8OnlyWhenThisComputerForcesIt() {
			TestCaptureModel model14=Model(iniText:Ini.Replace("Bits=16","Bits=14"));
			Assert.AreEqual(14,model14.BitsToRequest,"The device's Bits: what a record must have passed at.");
			Assert.AreEqual(14,model14.CreateEnableRequest().Bits);
			TestCaptureModel modelDefault=Model(iniText:Ini.Replace("Bits=16\r\n","").Replace("Bits=16\n",""));
			Assert.AreEqual(16,modelDefault.Options.Bits,"No Bits in the file: 16 by default.");
			Assert.AreEqual(16,modelDefault.BitsToRequest);
			TestCaptureModel modelOff=Model(iniText:Ini.Replace("Allow16Bit=1","Allow16Bit=0"));
			Assert.AreEqual(8,modelOff.BitsToRequest,"Allow16Bit=0 forces 8 everywhere, Test capture too.");
			modelOff.BitsToRequest=16;
			Assert.AreEqual(8,modelOff.BitsToRequest);
			CollectionAssert.AreEqual(new[] { 8 },modelOff.ListBitsChoices);
			StringAssert.Contains(modelOff.GetSettingsText(),"More than 8 bits configured: no");
		}

		[TestMethod]
		public void Record_14Bit_ConfirmedPassesAt14_AndSaysWhatItCovers() {
			TestCaptureModel model=Model(iniText:Ini.Replace("Bits=16","Bits=14"));
			model.OnOpenResult(Opened());
			model.OnEnablePosted(1);
			model.OnImage(Image14());
			StringAssert.Contains(model.GetResultText(),"Delivered: 14-bit in a 16-bit file, grey over memory transfer (asked for 14).");
			model.IsByteOrderConfirmed=true;
			model.IsPolarityConfirmed=true;
			string error;
			CaptureVerificationRecord record=model.BuildRecord(new DateTime(2026,11,1,9,0,0),out error);
			Assert.IsNotNull(record,error);
			Assert.AreEqual(14,record.BitDepthInEffect);
			Assert.AreEqual(14,record.BitDepthVerified);
			Assert.IsTrue(record.IsPassedFor(14));
			Assert.AreEqual("",model.GetRecordWarning(record));
			model.OnRecordWritten(record);
			StringAssert.Contains(model.GetResultText(),"verifies 14-bit capture. It covers 14-bit capture (stored in a 16-bit file) for driver");
		}

		[TestMethod]
		public void Record_Asked16AndTheSourceRanAt14_CoversTheSessionAt14_NoWarning() {
			TestCaptureModel model=Model();//Bits=16
			model.OnOpenResult(Opened());
			model.OnEnablePosted(1);
			model.OnImage(Image14(requestedBits:16));
			StringAssert.Contains(model.GetResultText(),"Delivered: 14-bit in a 16-bit file, grey over memory transfer (asked for 16). ICAP_BITDEPTH MSG_GET "
				+"does not list 16 (8, 14).");
			model.IsByteOrderConfirmed=true;
			model.IsPolarityConfirmed=true;
			string error;
			CaptureVerificationRecord record=model.BuildRecord(DateTime.Now,out error);
			Assert.IsTrue(record.IsPassedFor(14));
			Assert.AreEqual(16,record.BitsRequested);
			Assert.AreEqual("",model.GetRecordWarning(record),"Owner decision 2026-11-01: with Bits=16 the session falls back to the 14 it passed at.");
			model.OnRecordWritten(record);
			StringAssert.Contains(model.GetResultText(),"verifies 14-bit capture. It covers 14-bit capture (stored in a 16-bit file) for driver");
			StringAssert.Contains(model.GetResultText(),"This driver gave 14 bits when asked for 16, so the capture session asks it for 14.");
		}

		[TestMethod]
		public void Record_TheOperatorTested14WithBits16_WarnsThatTestCaptureMustRunAgainAfterSettingBits_AndCoversNothing() {
			TestCaptureModel model=Model();//Bits=16
			model.BitsToRequest=14;
			model.OnOpenResult(Opened());
			model.OnEnablePosted(1);
			model.OnImage(Image14(requestedBits:14));
			model.IsByteOrderConfirmed=true;
			model.IsPolarityConfirmed=true;
			string error;
			CaptureVerificationRecord record=model.BuildRecord(DateTime.Now,out error);
			Assert.IsTrue(record.IsPassedFor(14));
			Assert.AreEqual("This record verifies 14-bit capture, but this device's Bits is 16, so the capture session will not ask for more than 8 bits "
				+"with it. Set Bits=14 in Capture settings and run Test capture again (this record does not match once Bits changes), or take the test "
				+"image at 16 bits.",model.GetRecordWarning(record));
			Assert.AreEqual("",TestCaptureModel.GetRecordWarningPassed(record),"It did pass, at 14.");
			Assert.AreEqual("",TestCaptureModel.GetRecordScopeText(record,16),"With Bits=16 in its image settings no session can use it.");
			model.OnRecordWritten(record);
			string text=model.GetResultText();
			StringAssert.Contains(text,"verifies 14-bit capture. This record verifies 14-bit capture, but this device's Bits is 16");
			Assert.IsFalse(text.Contains("It covers"),text);
		}

		[TestMethod]
		public void Record_SourceReportedNoDriverVersion_NotWritten() {
			TestCaptureModel model=Model();
			TwainCaptureOpenResult opened=Opened();
			opened.SourceInfo.DriverIdentity=null;
			model.OnOpenResult(opened);
			model.OnEnablePosted(1);
			model.OnImage(Image16());
			string error;
			Assert.IsNull(model.BuildRecord(DateTime.Now,out error));
			StringAssert.Contains(error,"did not report its driver version");
		}

		[TestMethod]
		public void Record_8BitImageWithFallback_DeliveredLineSaysWhy() {
			TestCaptureModel model=Model();
			model.OnOpenResult(Opened());
			model.OnEnablePosted(1);
			AcquiredImage image=TestImages.Image8(8,8,(x,y) => (byte)(x*y),TestImages.Json8Bit);
			image.RequestedBits=16;
			image.BitDepthReason="ICAP_BITDEPTH MSG_GET does not list 16 (8)";
			model.OnImage(image);
			StringAssert.Contains(model.GetResultText(),"Delivered: 8-bit (fallback: ICAP_BITDEPTH MSG_GET does not list 16 (8)) grey over");
		}

		#region CaptureVerificationRecord
		private static CaptureVerificationRecord FullRecord() {
			return new CaptureVerificationRecord {
				ImagingDeviceNum=12,DeviceDescription="Sensor; room 2 = left %",SourceName="Example Sensor DS",
				SourceProductName="Example Sensor DS 1.1",ComputerName="OPPC1",DriverIdentity=_driver,ImageSettingsHash=new string('c',64),
				GatedSettingsHash=new string('a',64),DateTimeVerified=new DateTime(2026,10,31,15,4,5),BitsRequested=16,BitDepthInEffect=16,BitsDelivered=16,
				TransferMechanism="Memory",Width=1000,Height=1500,XDpi=1270,YDpi=1270,ByteOrder16="LE",ByteOrderCheck="LooksRight",
				IsByteOrderConfirmed=true,Polarity="AsDelivered",PixelFlavor="Chocolate",IsPolarityConfirmed=true,MinValue=12,MaxValue=4000,
				EffectiveBitsMeasured=12,EffectiveBitsReported=null,BitsStoredRule=16,WindowLow=100,WindowHigh=3900,Serial="SN\r\n1",Model="",
				DsVersion="1.1.0",IsMetadataPresent=true,
			};
		}

		[TestMethod]
		public void RecordText_RoundTrip() {
			CaptureVerificationRecord record=FullRecord();
			string logText=record.ToLogText();
			StringAssert.StartsWith(logText,CaptureVerificationRecord.LogPrefix);
			StringAssert.Contains(logText,"Device=12; ");
			StringAssert.Contains(logText,"DeviceDescription=Sensor%3B room 2 %3D left %25; ");
			Assert.IsFalse(logText.Contains("\n"));
			CaptureVerificationRecord recordRead;
			Assert.IsTrue(CaptureVerificationRecord.TryParse(logText,out recordRead));
			Assert.AreEqual(record.ToLogText(),recordRead.ToLogText());
			Assert.AreEqual("Sensor; room 2 = left %",recordRead.DeviceDescription);
			Assert.AreEqual("SN\r\n1",recordRead.Serial);
			Assert.AreEqual(record.DateTimeVerified,recordRead.DateTimeVerified);
			Assert.IsNull(recordRead.EffectiveBitsReported);
			Assert.AreEqual(3900,recordRead.WindowHigh);
			Assert.AreEqual(1270,recordRead.XDpi);
			Assert.IsTrue(recordRead.IsPassedFor(16));
			Assert.IsTrue(recordRead.DriverIdentity.IsSameAs(_driver));
			Assert.AreEqual(new string('c',64),recordRead.ImageSettingsHash);
		}

		[TestMethod]
		public void RecordText_OtherTextsRefused() {
			CaptureVerificationRecord record;
			Assert.IsFalse(CaptureVerificationRecord.TryParse(null,out record));
			Assert.IsFalse(CaptureVerificationRecord.TryParse("Capture settings changed: ...",out record));
			string driverPairs="DriverProductName=X DS; DriverVersion=1.0; DriverVersionInfo=1.0.0; ";
			Assert.IsFalse(CaptureVerificationRecord.TryParse(CaptureVerificationRecord.LogPrefix+"Device=12; "+driverPairs+"Computer=PC",out record),"No hash.");
			Assert.IsFalse(CaptureVerificationRecord.TryParse(CaptureVerificationRecord.LogPrefix+"Device=12; Computer=PC; ImageSettingsHash="+new string('a',64),
				out record),"No driver version.");
			Assert.IsFalse(CaptureVerificationRecord.TryParse(CaptureVerificationRecord.LogPrefix+"Device=0; "+driverPairs+"ImageSettingsHash="+new string('a',64),
				out record),"No device.");
			Assert.IsTrue(CaptureVerificationRecord.TryParse(CaptureVerificationRecord.LogPrefix+"Device=3; "+driverPairs+"ImageSettingsHash="+new string('B',64),
				out record),"Unknown and missing keys are ignored; the computer is not needed.");
			Assert.AreEqual(new string('b',64),record.ImageSettingsHash);
			Assert.AreEqual("",record.ComputerName);
			Assert.IsFalse(record.IsPassed);
		}

		[TestMethod]
		public void Record_MatchesAndPasses() {
			CaptureVerificationRecord record=FullRecord();
			string hash=new string('c',64);
			Assert.IsTrue(record.Matches(12," example sensor ds",new TwainDriverIdentity("EXAMPLE SENSOR DS",1,1,"1.1.0"),hash.ToUpperInvariant()));
			Assert.IsFalse(record.Matches(13,"Example Sensor DS",_driver,hash),"Another device.");
			Assert.IsFalse(record.Matches(12,"Example Sensor DS",_driver,new string('b',64)),"Other image settings.");
			Assert.IsFalse(record.Matches(12,"Example Sensor DS",new TwainDriverIdentity("Example Sensor DS",1,2,"1.1.0"),hash),"Another driver version.");
			Assert.IsFalse(record.Matches(12,"Example Sensor DS",new TwainDriverIdentity("Example Sensor DS",1,1,"1.1.1"),hash),"Another driver build.");
			Assert.IsFalse(record.Matches(12,"Example Sensor DS",null,hash),"The open source's driver not known.");
			Assert.IsFalse(record.Matches(12,"Other Sensor DS",_driver,hash),"Another TWAIN source of the row.");
			Assert.IsFalse(record.Matches(12,"",_driver,hash));
			record.ComputerName="OPPC2";
			Assert.IsTrue(record.Matches(12,"Example Sensor DS",_driver,hash),"Another computer: the computer is not compared.");
			record.SourceName="";
			Assert.IsFalse(record.Matches(12,"",_driver,hash),"A record without its source matches none.");
			record=FullRecord();
			record.ImageSettingsHash="";
			Assert.IsFalse(record.Matches(12,"Example Sensor DS",_driver,""));
			record=FullRecord();
			record.ByteOrderCheck="LooksSwapped";
			Assert.IsFalse(record.IsPassedFor(16));
			StringAssert.Contains(TestCaptureModel.GetRecordWarningPassed(record),"the byte order looks swapped");
			record=FullRecord();
			record.ByteOrderCheck="Inconclusive";
			Assert.IsTrue(record.IsPassedFor(16),"Inconclusive passes when the operator confirmed it on the ramp.");
			Assert.IsFalse(record.IsPassedFor(14),"A record that passed at 16 covers 16 only.");
			//The record never claims what the saver refuses: samples of more than 8 bits only from such an enable over memory transfer...
			record=FullRecord();
			record.TransferMechanism="Native";
			Assert.IsFalse(record.IsPassedFor(16),"A 16bpp native DIB is never written as DICOM.");
			StringAssert.Contains(TestCaptureModel.GetRecordWarningPassed(record),"over memory transfer");
			record=FullRecord();
			record.BitsRequested=8;
			record.BitDepthInEffect=8;
			Assert.IsFalse(record.IsPassedFor(16),"Nor 16-bit samples in an 8-bit enable.");
			//...and TWPF_VANILLA samples only inverted; inverted CHOCOLATE ones are written.
			record=FullRecord();
			record.PixelFlavor="Vanilla";
			Assert.IsFalse(record.IsPassedFor(16));
			StringAssert.Contains(TestCaptureModel.GetRecordWarningPassed(record),"only with Polarity=Invert");
			record.Polarity="Invert";
			Assert.IsTrue(record.IsPassedFor(16));
			record.PixelFlavor="Chocolate";
			Assert.IsTrue(record.IsPassedFor(16),"A verified inversion of a chocolate source is the clinic's fix (twain-contract 1.1 section 6.2).");
			record=FullRecord();
			record.BitDepthInEffect=14;
			Assert.IsTrue(record.IsPassedFor(14));
			Assert.IsFalse(record.IsPassedFor(16),"A record that passed at 14 covers 14 only.");
			//A 14-bit image with a sample above 16383 is not right-justified 14-bit data: it verifies no 14-bit capture.
			record.MaxValue=16383;
			Assert.IsTrue(record.IsPassedFor(14));
			record.MaxValue=20000;
			Assert.IsFalse(record.IsPassedFor(14));
			Assert.IsTrue(record.Is14BitNotRightJustified);
			StringAssert.Contains(TestCaptureModel.GetRecordWarningPassed(record),"the 14-bit samples are not right-justified (max 20000)");
			record.BitDepthInEffect=16;
			Assert.IsTrue(record.IsPassedFor(16),"At 16 bits every value fits.");
		}
		#endregion CaptureVerificationRecord
	}
}
