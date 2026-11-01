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

		private TestCaptureModel Model(long imagingDeviceNum=12,List<MountTemplateCategory> listMountTemplates=null) {
			TestCaptureDevice device=new TestCaptureDevice { ImagingDeviceNum=imagingDeviceNum,Description="Intraoral sensor",TwainName="Example Sensor DS" };
			return new TestCaptureModel(device,Load(Ini),"OPPC1",new ImageCategoryRule(Categories),
				listMountTemplates??new List<MountTemplateCategory> { new MountTemplateCategory { Description="FMX",DefaultCat=10 } },null);
		}

		private static TwainCaptureOpenResult Opened() {
			return new TwainCaptureOpenResult { IsOpen=true,SourceInfo=new TwainCaptureSourceInfo { ProductName="Example Sensor DS",ListBitDepths=new List<int> { 8 } } };
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
			Assert.IsTrue(model.Options.Is16BitConfigured);
			string text=model.GetSettingsText();
			StringAssert.Contains(text,"Bits=16");
			StringAssert.Contains(text,"SafeDisableCap=0x8101");
			StringAssert.Contains(text,"16-bit capture configured: yes");
			StringAssert.Contains(text,"SHA-256 "+model.GatedSettingsHashValue);
			StringAssert.Contains(text,"Twain.AllowedSources=example sensor ds","The gated values as hashed.");
			Assert.IsFalse(text.Contains("Not gated"),"Hardware bindings are not shown as gated values.");
			model.OnOpenResult(Opened());
			StringAssert.Contains(model.GetSettingsText(),"Bit depths at open: 8");
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
			Assert.AreEqual(16,request.Bits,"Test capture asks for 16 bits to measure.");
			Assert.AreEqual(TestCaptureModel.TargetLabel,request.TargetLabel);
			Assert.IsNull(request.ShowUI,"The [Device.N] Headless setting decides, as in the session.");
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
			model.OnEnded(new TwainCaptureEnded { EnableId=1,Reason=TwainCaptureEndReason.Completed,ImageCount=1,IsBitDepth16InEffect=true });
			Assert.AreEqual(TestCaptureState.Open,model.State,"The source stays open in state 4.");
			Assert.AreEqual("Test image received.",model.MessageText);
			StringAssert.Contains(model.GetResultText(),"Delivered: 16-bit grey over memory transfer (asked for 16).");
			model.BitsToRequest=8;
			Assert.AreEqual(8,model.CreateEnableRequest().Bits);
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
				Reason=TwainCaptureEndReason.Completed,Reason8Bit="ICAP_BITDEPTH does not list 16" }));
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
			Assert.AreEqual("OPPC1",record.ComputerName);
			Assert.AreEqual(model.GatedSettingsHashValue,record.GatedSettingsHash);
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
			Assert.IsTrue(record.IsPassedFor16Bit);
			Assert.AreEqual("",TestCaptureModel.GetRecordWarning(record));
			Assert.IsTrue(record.Matches(12,"oppc1","Example Sensor DS",model.GatedSettingsHashValue.ToUpperInvariant()));
			model.OnRecordWritten(record);
			StringAssert.Contains(model.GetResultText(),"verifies 16-bit capture with these settings");
			//A new image needs new confirmations.
			model.OnImage(Image16());
			Assert.IsFalse(model.IsByteOrderConfirmed);
			Assert.IsFalse(model.IsPolarityConfirmed);
			record=model.BuildRecord(now,out error);
			Assert.IsFalse(record.IsPassedFor16Bit);
			StringAssert.Contains(TestCaptureModel.GetRecordWarning(record),"the byte order is not confirmed");
			StringAssert.Contains(TestCaptureModel.GetRecordWarning(record),"the polarity is not confirmed");
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
			Assert.IsFalse(record.IsPassedFor16Bit);
			Assert.AreEqual("This record does not verify 16-bit capture: the image is not 16-bit.",TestCaptureModel.GetRecordWarning(record));
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
			TestCaptureModel model=new TestCaptureModel(device,Load(Ini),"OPPC1",new ImageCategoryRule(Categories),null,"2 unassigned capture(s) on this computer.");
			StringAssert.Contains(model.GetDeviceText(),"set up for computer OPPC2");
			Assert.AreEqual("2 unassigned capture(s) on this computer.",model.UnassignedText);
			device.ComputerName="oppc1";
			Assert.IsFalse(model.GetDeviceText().Contains("set up for computer"));
		}

		[TestMethod]
		public void UnassignedNotice_OpensTheWindowOnlyWhileNotTaking_ReadAgainAfterwards() {
			TestCaptureDevice device=new TestCaptureDevice { ImagingDeviceNum=12,Description="Sensor",TwainName="Example Sensor DS",ComputerName="OPPC1" };
			TestCaptureModel model=new TestCaptureModel(device,Load(Ini),"OPPC1",new ImageCategoryRule(Categories),null,"2 unassigned capture(s) on this computer.");
			Assert.IsTrue(model.CanOpenUnassignedCaptures);
			int version=model.Version;
			model.SetUnassignedText(null);
			Assert.IsNull(model.UnassignedText);
			Assert.IsTrue(model.Version>version);
			Assert.IsFalse(model.CanOpenUnassignedCaptures,"No notice, no link.");
		}

		#region CaptureVerificationRecord
		private static CaptureVerificationRecord FullRecord() {
			return new CaptureVerificationRecord {
				ImagingDeviceNum=12,DeviceDescription="Sensor; room 2 = left %",SourceName="Example Sensor DS",
				SourceProductName="Example Sensor DS 1.1",ComputerName="OPPC1",
				GatedSettingsHash=new string('a',64),DateTimeVerified=new DateTime(2026,10,31,15,4,5),BitsRequested=16,BitsDelivered=16,
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
			Assert.IsTrue(recordRead.IsPassedFor16Bit);
		}

		[TestMethod]
		public void RecordText_OtherTextsRefused() {
			CaptureVerificationRecord record;
			Assert.IsFalse(CaptureVerificationRecord.TryParse(null,out record));
			Assert.IsFalse(CaptureVerificationRecord.TryParse("Capture settings changed: ...",out record));
			Assert.IsFalse(CaptureVerificationRecord.TryParse(CaptureVerificationRecord.LogPrefix+"Device=12; Computer=PC",out record),"No hash.");
			Assert.IsFalse(CaptureVerificationRecord.TryParse(CaptureVerificationRecord.LogPrefix+"Device=0; Computer=PC; GatedSettingsHash="+new string('a',64),
				out record),"No device.");
			Assert.IsTrue(CaptureVerificationRecord.TryParse(CaptureVerificationRecord.LogPrefix+"Device=3; Computer=PC; GatedSettingsHash="+new string('B',64),
				out record),"Unknown and missing keys are ignored.");
			Assert.AreEqual(new string('b',64),record.GatedSettingsHash);
			Assert.IsFalse(record.IsPassedFor16Bit);
		}

		[TestMethod]
		public void Record_MatchesAndPasses() {
			CaptureVerificationRecord record=FullRecord();
			Assert.IsTrue(record.Matches(12,"oppc1 "," example sensor ds",new string('A',64)));
			Assert.IsFalse(record.Matches(13,"OPPC1","Example Sensor DS",new string('a',64)),"Another device.");
			Assert.IsFalse(record.Matches(12,"OPPC2","Example Sensor DS",new string('a',64)),"Another computer.");
			Assert.IsFalse(record.Matches(12,"OPPC1","Example Sensor DS",new string('b',64)),"Other settings.");
			//The hash covers capture.ini only; with RestrictSources=0 pointing the device row at another source leaves it unchanged.
			Assert.IsFalse(record.Matches(12,"OPPC1","Other Sensor DS",new string('a',64)),"Another TWAIN source.");
			Assert.IsFalse(record.Matches(12,"OPPC1","",new string('a',64)));
			record.SourceName="";
			Assert.IsFalse(record.Matches(12,"OPPC1","",new string('a',64)),"A record without its source matches none.");
			record=FullRecord();
			record.GatedSettingsHash="";
			Assert.IsFalse(record.Matches(12,"OPPC1","Example Sensor DS",""));
			record=FullRecord();
			record.ByteOrderCheck="LooksSwapped";
			Assert.IsFalse(record.IsPassedFor16Bit);
			StringAssert.Contains(TestCaptureModel.GetRecordWarning(record),"the byte order looks swapped");
			record=FullRecord();
			record.ByteOrderCheck="Inconclusive";
			Assert.IsTrue(record.IsPassedFor16Bit,"Inconclusive passes when the operator confirmed it on the ramp.");
			//The record never claims what the saver refuses: 16-bit samples only from a 16-bit enable over memory transfer...
			record=FullRecord();
			record.TransferMechanism="Native";
			Assert.IsFalse(record.IsPassedFor16Bit,"A 16bpp native DIB is never written as DICOM.");
			StringAssert.Contains(TestCaptureModel.GetRecordWarning(record),"over memory transfer");
			record=FullRecord();
			record.BitsRequested=8;
			Assert.IsFalse(record.IsPassedFor16Bit,"Nor 16-bit samples in an 8-bit enable.");
			//...and TWPF_VANILLA samples only inverted; inverted CHOCOLATE ones are written.
			record=FullRecord();
			record.PixelFlavor="Vanilla";
			Assert.IsFalse(record.IsPassedFor16Bit);
			StringAssert.Contains(TestCaptureModel.GetRecordWarning(record),"only with Polarity=Invert");
			record.Polarity="Invert";
			Assert.IsTrue(record.IsPassedFor16Bit);
			record.PixelFlavor="Chocolate";
			Assert.IsTrue(record.IsPassedFor16Bit,"A verified inversion of a chocolate source is the clinic's fix (twain-contract 1.1 section 6.2).");
		}
		#endregion CaptureVerificationRecord
	}
}
