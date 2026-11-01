// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ImagingCapture.Dicom;
using ImagingCapture.Session;
using ImagingCapture.Settings;
using ImagingDeviceManager.Capture;
using ImagingDeviceManager.Config;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace ImagingCapture.Tests {
	///<summary>CaptureBitDepthDecision (design 7.8 "When 16-bit is enabled", 7.10, 9.3) and CaptureVerificationRecords.FindLatest; also the
	///capture session's own capture.ini keys (CaptureSessionConfig) and the target texts (CaptureTargetText).</summary>
	[TestClass]
	public class CaptureBitDepthDecisionTests {
		private static readonly string _hash=new string('b',64);

		private static CaptureBitDepthInput Input() {
			return new CaptureBitDepthInput {
				Options=new TwainCaptureOptions { ImagingDeviceNum=12,Bits=16,Allow16Bit=true },
				Record=CaptureControllerHarness.PassedRecord(_hash),
				ImagingDeviceNum=12,
				ComputerName="operatory1 ",
				TwainName="Example Sensor DS",
				GatedSettingsHash=_hash,
				Target=new IntraOralTarget { HasMountSlot=true,ToothNumbers="2,3" },
			};
		}

		#region Decision
		[TestMethod]
		public void EveryConditionHolds_16Bits() {
			CaptureBitDepthChoice choice=CaptureBitDepthDecision.Decide(Input());
			Assert.AreEqual(16,choice.Bits);
			Assert.IsNull(choice.Reason8Bit);
			Assert.IsFalse(choice.IsInversionVerified,"Polarity=AsDelivered.");
		}

		[TestMethod]
		public void PolarityInvert_With16Bits_IsInversionVerified() {
			CaptureBitDepthInput input=Input();
			input.Options.Polarity=TwainPolarity.Invert;
			Assert.IsTrue(CaptureBitDepthDecision.Decide(input).IsInversionVerified);
			input.Options.Allow16Bit=false;
			CaptureBitDepthChoice choice=CaptureBitDepthDecision.Decide(input);
			Assert.AreEqual(8,choice.Bits);
			Assert.IsFalse(choice.IsInversionVerified,"An inverted capture needs the matching record too (design 9.3).");
		}

		[TestMethod]
		public void EachCondition_Gives8Bits_WithItsReason() {
			AssertReason(x => x.Options.Bits=8,"Bits is not 16");
			AssertReason(x => x.Options.Allow16Bit=false,"Allow16Bit is 0");
			AssertReason(x => x.Record=null,"no Test capture verification record");
			AssertReason(x => x.GatedSettingsHash=new string('c',64),"does not match the current settings");
			AssertReason(x => x.TwainName="Other DS","does not match the current settings");
			AssertReason(x => x.ComputerName="OPERATORY2","does not match the current settings");
			AssertReason(x => x.ImagingDeviceNum=13,"does not match the current settings");
			AssertReason(x => x.Record.IsPolarityConfirmed=false,"did not pass for 16 bits");
			AssertReason(x => x.Record.TransferMechanism="Native","did not pass for 16 bits");
			AssertReason(x => x.Target=new IntraOralTarget { HasMountSlot=false,ToothNumbers="8" },"cannot be stored as DICOM");
			AssertReason(x => x.Target=new IntraOralTarget { HasMountSlot=true,ToothNumbers="" },"cannot be stored as DICOM");
			AssertReason(x => x.Target=new IntraOralTarget { HasMountSlot=true,ToothNumbers="5,6" },"cannot be stored as DICOM");
			AssertReason(x => x.Options=null,"not known");
		}

		private static void AssertReason(Action<CaptureBitDepthInput> actionChange,string fragment) {
			CaptureBitDepthInput input=Input();
			actionChange(input);
			CaptureBitDepthChoice choice=CaptureBitDepthDecision.Decide(input);
			Assert.AreEqual(8,choice.Bits,fragment);
			StringAssert.Contains(choice.Reason8Bit,fragment);
			StringAssert.StartsWith(choice.Reason8Bit,"8 bits: ");
		}
		#endregion Decision

		#region FindLatest
		private static SecurityLog Log(long securityLogNum,DateTime dateTime,CaptureVerificationRecord record) {
			return new SecurityLog { SecurityLogNum=securityLogNum,LogDateTime=dateTime,FKey=record.ImagingDeviceNum,PermType=EnumPermType.Setup,
				LogText=record.ToLogText() };
		}

		[TestMethod]
		public void FindLatest_LatestForTheDeviceAndComputer_EvenWhenItNoLongerMatches() {
			CaptureVerificationRecord recordOld=CaptureControllerHarness.PassedRecord(_hash);
			CaptureVerificationRecord recordNew=CaptureControllerHarness.PassedRecord(new string('d',64));
			CaptureVerificationRecord recordOtherPc=CaptureControllerHarness.PassedRecord(_hash);
			recordOtherPc.ComputerName="OPERATORY2";
			CaptureVerificationRecord recordOtherDevice=CaptureControllerHarness.PassedRecord(_hash);
			recordOtherDevice.ImagingDeviceNum=13;
			List<SecurityLog> listLogs=new List<SecurityLog> {
				Log(5,new DateTime(2026,10,29,9,0,0),recordOld),
				Log(6,new DateTime(2026,10,30,9,0,0),recordNew),
				Log(7,new DateTime(2026,10,31,9,0,0),recordOtherPc),
				Log(8,new DateTime(2026,10,31,9,0,0),recordOtherDevice),
				new SecurityLog { SecurityLogNum=9,LogDateTime=new DateTime(2026,10,31,10,0,0),FKey=12,LogText="Imaging device edited." },
			};
			CaptureVerificationRecord recordFound=CaptureVerificationRecords.FindLatest(listLogs,12," Operatory1");
			Assert.IsNotNull(recordFound);
			Assert.AreEqual(new string('d',64),recordFound.GatedSettingsHash,"The newest one counts, though its hash differs from today's file.");
			Assert.IsNull(CaptureVerificationRecords.FindLatest(listLogs,12,"OPERATORY3"));
			Assert.IsNull(CaptureVerificationRecords.FindLatest(null,12,"OPERATORY1"));
		}

		[TestMethod]
		public void FindLatest_SameTime_HigherSecurityLogNumWins() {
			CaptureVerificationRecord recordA=CaptureControllerHarness.PassedRecord(new string('a',64));
			CaptureVerificationRecord recordB=CaptureControllerHarness.PassedRecord(new string('e',64));
			DateTime dateTime=new DateTime(2026,10,30,9,0,0);
			CaptureVerificationRecord recordFound=CaptureVerificationRecords.FindLatest(new List<SecurityLog> { Log(11,dateTime,recordB),Log(10,dateTime,recordA) },
				12,"OPERATORY1");
			Assert.AreEqual(new string('e',64),recordFound.GatedSettingsHash);
		}
		#endregion FindLatest

		#region CaptureSessionConfig
		[TestMethod]
		public void SessionConfig_Defaults() {
			CaptureSessionConfig config=CaptureSessionConfig.Read(null);
			Assert.IsFalse(config.IsEnabled,"Design 9.3: Enabled=0, the legacy loop.");
			Assert.AreEqual(10,config.IdleDisableMinutes);
			Assert.AreEqual("",config.GetKeyText(CaptureSessionKey.Confirm),"Empty: the on-screen button only.");
			Assert.AreEqual("SystemAsterisk",config.GetSoundName(CaptureSound.Saved));
			Assert.AreEqual("SystemHand",config.GetSoundName(CaptureSound.NotReceived));
			Assert.AreEqual("SystemExclamation",config.GetSoundName(CaptureSound.Disabled));
			Assert.AreEqual("SystemHand",config.GetSoundName(CaptureSound.Error));
		}

		[TestMethod]
		public void SessionConfig_ReadFromTheFile() {
			string folder=Path.Combine(Path.GetTempPath(),"CaptureSessionConfig_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(folder);
			try {
				string path=Path.Combine(folder,"capture.ini");
				File.WriteAllText(path,string.Join("\r\n","[session]","enabled=1","IdleDisableMinutes=-3","[Keys]","Confirm= F8 ","Retake=Ctrl+F9","End=F12",
					"[Sounds]","Saved=","NotReceived=SystemQuestion"),new UTF8Encoding(false));
				CaptureSessionConfig config=CaptureSessionConfig.Read(LocalCaptureConfig.Load(path,Path.Combine(folder,"fallback.ini")));
				Assert.IsTrue(config.IsEnabled);
				Assert.AreEqual(0,config.IdleDisableMinutes,"Negative reads as off.");
				Assert.AreEqual("F8",config.GetKeyText(CaptureSessionKey.Confirm));
				Assert.AreEqual("Ctrl+F9",config.GetKeyText(CaptureSessionKey.Retake));
				Assert.AreEqual("F12",config.GetKeyText(CaptureSessionKey.End));
				Assert.AreEqual("",config.GetKeyText(CaptureSessionKey.Skip));
				Assert.AreEqual("",config.GetSoundName(CaptureSound.Saved),"Empty turns the sound off.");
				Assert.AreEqual("SystemQuestion",config.GetSoundName(CaptureSound.Error),"Error follows NotReceived without its own key.");
				Assert.AreEqual("SystemExclamation",config.GetSoundName(CaptureSound.Disabled));
			}
			finally {
				try {
					Directory.Delete(folder,true);
				}
				catch {
				}
			}
		}
		#endregion CaptureSessionConfig

		#region CaptureTargetText
		[TestMethod]
		public void TargetText_PositionAmongTheSlots_AndTheTeeth() {
			List<MountItem> listMountItems=CaptureControllerHarness.Slots(7);
			Assert.AreEqual("1 of 7: Max R, #1,2,3",CaptureTargetText.GetSlotText(listMountItems,listMountItems[1]));
			Assert.AreEqual("3 of 7: Max R anterior, #6",CaptureTargetText.GetSlotText(listMountItems,listMountItems[3]));
			Assert.AreEqual("4 of 7: Max anterior, #7,8,9,10",CaptureTargetText.GetSlotText(listMountItems,listMountItems[4]));
			Assert.AreEqual("7 of 7: Max L, #14,15,16",CaptureTargetText.GetSlotText(listMountItems,listMountItems[7]));
			Assert.AreEqual("",CaptureTargetText.GetSlotText(listMountItems,listMountItems[0]),"A text item.");
			Assert.AreEqual("R bitewing, #2,3,30,31",CaptureTargetText.GetTeethText("2,3,30,31"));
			Assert.AreEqual("Mand anterior, #24,25",CaptureTargetText.GetTeethText("24, 25"));
			Assert.AreEqual("#2-5",CaptureTargetText.GetTeethText("2-5"),"Not readable: as written.");
			Assert.AreEqual("Slot 4",CaptureTargetText.GetDescription(new MountItem { ItemOrder=4,ToothNumbers="" }));
			Assert.AreEqual("Single image",CaptureTargetText.GetSingleText(""));
			Assert.AreEqual("Single image, Max anterior, #8,9",CaptureTargetText.GetSingleText("8,9"));
		}
		#endregion CaptureTargetText
	}
}
