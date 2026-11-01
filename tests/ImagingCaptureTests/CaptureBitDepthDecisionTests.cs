// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ImagingCapture.Dicom;
using ImagingCapture.Session;
using ImagingCapture.Settings;
using ImagingDeviceManager.Capture;
using ImagingDeviceManager.Config;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

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
				DriverIdentity=CaptureControllerHarness.Driver,
				GatedSettingsHash=_hash,
				SettingsCheck=CaptureSettingsTestKit.VerifiedHash(_hash),
				Target=new IntraOralTarget { HasMountSlot=true,ToothNumbers="2,3" },
			};
		}

		///<summary>The text of a record as CAP-3 and CAP-4 wrote it (capture-verification/1: per computer, the whole-file hash, no driver version).</summary>
		internal const string LegacyRecordText="Capture verification record (capture-verification/1): Device=12; DeviceDescription=Intraoral sensor; "
			+"Source=Example Sensor DS; SourceProductName=Example Sensor DS; Computer=OPERATORY1; "
			+"GatedSettingsHash=bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb; Date=2026-10-30T09:00:00; BitsRequested=16; BitsDelivered=16; "
			+"Transfer=Memory; Width=600; Height=400; XDpi=1270; YDpi=1270; ByteOrder16=LE; ByteOrderCheck=LooksRight; ByteOrderConfirmed=1; "
			+"Polarity=AsDelivered; PixelFlavor=Chocolate; PolarityConfirmed=1; Min=12; Max=4000; EffectiveBitsMeasured=12; EffectiveBitsReported=; "
			+"BitsStoredRule=16; WindowLow=100; WindowHigh=3900; Serial=; Model=; DsVersion=; Metadata=0";

		#region Decision
		[TestMethod]
		public void EveryConditionHolds_16Bits() {
			CaptureBitDepthChoice choice=CaptureBitDepthDecision.Decide(Input());
			Assert.AreEqual(16,choice.Bits);
			Assert.IsNull(choice.Reason8Bit);
			Assert.IsNull(choice.ReasonFallback);
			Assert.IsFalse(choice.IsFallback);
			Assert.IsFalse(choice.IsInversionVerified,"Polarity=AsDelivered.");
		}

		[TestMethod]
		public void Bits14_RecordPassedAt14_14Bits_AndTheOtherWayRound8() {
			CaptureBitDepthInput input=Input();
			input.Options.Bits=14;
			input.Record=CaptureControllerHarness.PassedRecord(_hash,14);
			CaptureBitDepthChoice choice14=CaptureBitDepthDecision.Decide(input);
			Assert.AreEqual(14,choice14.Bits,"A record that passed at 14 covers 14.");
			Assert.IsNull(choice14.ReasonBelowBits,"14 is the device's Bits.");
			//Bits=16, and a record made with Bits=16 whose operator chose to test 14: it passed at 14, not at the 16 asked for.
			input=Input();
			input.Record=CaptureControllerHarness.PassedRecord(_hash,16);
			input.Record.BitsRequested=14;
			input.Record.BitDepthInEffect=14;
			CaptureBitDepthChoice choice=CaptureBitDepthDecision.Decide(input);
			Assert.AreEqual(8,choice.Bits);
			Assert.IsTrue(choice.IsFallback);
			StringAssert.Contains(choice.ReasonFallback,"passed at 14 bits, not at the 16 bits asked for");
			//Bits=14 and a record that passed at 16 with Bits=14 written in the file.
			input=Input();
			input.Options.Bits=14;
			input.Record=CaptureControllerHarness.PassedRecord(_hash,14);
			input.Record.BitDepthInEffect=16;
			StringAssert.Contains(CaptureBitDepthDecision.Decide(input).ReasonFallback,"passed at 16 bits, not at the 14 bits asked for");
		}

		[TestMethod]
		public void Bits16_RecordAsked16AndPassedAt14_Asks14_TheDepthItVerified() {
			//Owner decision 2026-11-01, "default to 16, and can fall back to 14 or 8": the driver lists 14 but not 16, so Test capture asked it for
			//16 and it ran at 14. With the same driver and image settings (Bits=16), the session asks for those 14 bits, never for 16.
			CaptureBitDepthInput input=Input();
			input.Record=CaptureControllerHarness.PassedRecord(_hash,16);
			input.Record.BitDepthInEffect=14;
			Assert.IsTrue(input.Record.IsPassedFor(14));
			CaptureBitDepthChoice choice=CaptureBitDepthDecision.Decide(input);
			Assert.AreEqual(14,choice.Bits);
			Assert.IsFalse(choice.IsFallback);
			Assert.IsNull(choice.Reason8Bit);
			Assert.AreEqual("[Device.12] Bits is 16, and the latest Test capture record (2026-10-30 09:00) asked this driver for 16 bits and passed at 14, "
				+"so the session asks for 14",choice.ReasonBelowBits);
			Assert.AreEqual(14,CaptureBitDepthDecision.GetBitsCovered(input.Record,16));
			Assert.AreEqual(8,CaptureBitDepthDecision.GetBitsCovered(input.Record,8));
			//Every gate still holds for the 14: another driver version, other image settings, or a target that is not DICOM give 8.
			input.DriverIdentity=new TwainDriverIdentity("Example Sensor DS",1,2,"1.2.0");
			Assert.AreEqual(8,CaptureBitDepthDecision.Decide(input).Bits);
			input=Input();
			input.Record=CaptureControllerHarness.PassedRecord(_hash,16);
			input.Record.BitDepthInEffect=14;
			input.Options.ByteOrder16=TwainByteOrder.BE;
			StringAssert.Contains(CaptureBitDepthDecision.Decide(input).ReasonFallback,"other image settings");
			input=Input();
			input.Record=CaptureControllerHarness.PassedRecord(_hash,16);
			input.Record.BitDepthInEffect=14;
			input.Target=new IntraOralTarget { HasMountSlot=false,ToothNumbers="2" };
			Assert.AreEqual(8,CaptureBitDepthDecision.Decide(input).Bits);
			//A record of that test that did not pass covers nothing.
			input=Input();
			input.Record=CaptureControllerHarness.PassedRecord(_hash,16);
			input.Record.BitDepthInEffect=14;
			input.Record.IsPolarityConfirmed=false;
			Assert.AreEqual(8,CaptureBitDepthDecision.Decide(input).Bits);
			StringAssert.Contains(CaptureBitDepthDecision.Decide(input).ReasonFallback,"did not pass");
			//The recovery filer decides for the depth a capture's enable asked for (BitsWanted), never a lower one.
			input=Input();
			input.Record=CaptureControllerHarness.PassedRecord(_hash,16);
			input.Record.BitDepthInEffect=14;
			input.BitsWanted=16;
			Assert.AreEqual(8,CaptureBitDepthDecision.Decide(input).Bits,"A 16-bit enable's capture is not covered by a record that passed at 14.");
			input.BitsWanted=14;
			Assert.AreEqual(14,CaptureBitDepthDecision.Decide(input).Bits,"The capture of a session that asked for 14.");
		}

		[TestMethod]
		public void BitsWanted_DecidesForThatDepth() {
			CaptureBitDepthInput input=Input();
			input.Options.Bits=14;
			input.Record=CaptureControllerHarness.PassedRecord(_hash,14);
			input.BitsWanted=16;
			StringAssert.Contains(CaptureBitDepthDecision.Decide(input).ReasonFallback,"not at the 16 bits asked for","The filer's depth, not Bits.");
			input.BitsWanted=14;
			Assert.AreEqual(14,CaptureBitDepthDecision.Decide(input).Bits);
		}

		[TestMethod]
		public void RecordPassedAtAnotherDepth_TheReasonNeverAsksToSetTheBitsAlreadySet() {
			//Bits=16 and a record that passed at 16; the filer decides for a capture whose enable asked for 14.
			CaptureBitDepthInput input=Input();
			input.BitsWanted=14;
			string reason=CaptureBitDepthDecision.Decide(input).ReasonFallback;
			StringAssert.EndsWith(reason,"passed at 16 bits, not at the 14 bits asked for; run Test capture again at 14 bits");
			//Bits=16 and a record whose test asked for 14 and passed at 14: setting Bits=14 is the other way.
			input=Input();
			input.Record=CaptureControllerHarness.PassedRecord(_hash,16);
			input.Record.BitsRequested=14;
			input.Record.BitDepthInEffect=14;
			StringAssert.EndsWith(CaptureBitDepthDecision.Decide(input).ReasonFallback,"run Test capture again at 16 bits, or set Bits=14 and run it then");
		}

		[TestMethod]
		public void PolarityInvert_AboveEightBits_IsInversionVerified() {
			CaptureBitDepthInput input=Input();
			input.Options.Polarity=TwainPolarity.Invert;
			input.Record=CaptureControllerHarness.PassedRecord(_hash,16,new TwainCaptureOptions { ImagingDeviceNum=12,Bits=16,Polarity=TwainPolarity.Invert });
			Assert.IsTrue(CaptureBitDepthDecision.Decide(input).IsInversionVerified);
			input.Options.Allow16Bit=false;
			CaptureBitDepthChoice choice=CaptureBitDepthDecision.Decide(input);
			Assert.AreEqual(8,choice.Bits);
			Assert.IsFalse(choice.IsInversionVerified,"An inverted capture needs the matching record too (design 9.3).");
		}

		[TestMethod]
		public void Configured8_NotAFallback() {
			CaptureBitDepthInput input=Input();
			input.Options.Bits=8;
			CaptureBitDepthChoice choice=CaptureBitDepthDecision.Decide(input);
			Assert.AreEqual(8,choice.Bits);
			Assert.AreEqual("8 bits: [Device.12] Bits is 8.",choice.Reason8Bit);
			Assert.IsFalse(choice.IsFallback);
			Assert.IsNull(choice.ReasonFallback);
			input=Input();
			input.Options.Allow16Bit=false;
			choice=CaptureBitDepthDecision.Decide(input);
			StringAssert.StartsWith(choice.Reason8Bit,"8 bits: [Twain] Allow16Bit is 0");
			Assert.IsFalse(choice.IsFallback,"0 is this computer's kill switch: a configuration.");
		}

		[TestMethod]
		public void EachGate_Gives8BitsAsAFallback_WithItsReason() {
			AssertFallback(x => x.Record=null,"no Test capture record for this sensor's driver version and image settings");
			AssertFallback(x => x.DriverIdentity=new TwainDriverIdentity("Example Sensor DS",1,2,"1.1.0"),"is for driver Example Sensor DS, version 1.1");
			AssertFallback(x => x.DriverIdentity=new TwainDriverIdentity("Example Sensor DS",2,1,"1.1.0"),"this source reports Example Sensor DS, version 2.1");
			AssertFallback(x => x.DriverIdentity=new TwainDriverIdentity("Example Sensor DS",1,1,"1.1.1"),"run Test capture again");
			AssertFallback(x => x.DriverIdentity=null,"did not report its driver version");
			AssertFallback(x => x.Options.ByteOrder16=TwainByteOrder.BE,"other image settings (Bits, ByteOrder16 or Polarity)");
			AssertFallback(x => x.Options.Polarity=TwainPolarity.Invert,"other image settings");
			AssertFallback(x => x.Options.Bits=14,"other image settings");
			AssertFallback(x => x.TwainName="Other DS","another device row or TWAIN source");
			AssertFallback(x => x.ImagingDeviceNum=13,"another device row or TWAIN source");
			AssertFallback(x => x.Record.IsPolarityConfirmed=false,"did not pass");
			AssertFallback(x => x.Record.TransferMechanism="Native","did not pass");
			AssertFallback(x => x.Target=new IntraOralTarget { HasMountSlot=false,ToothNumbers="8" },"cannot be stored as DICOM");
			AssertFallback(x => x.Target=new IntraOralTarget { HasMountSlot=true,ToothNumbers="" },"cannot be stored as DICOM");
			AssertFallback(x => x.Target=new IntraOralTarget { HasMountSlot=true,ToothNumbers="5,6" },"cannot be stored as DICOM");
			AssertFallback(x => x.ReasonLayoutUnusable="14-bit layout not usable: DAT_IMAGEINFO BitsPerPixel is 14","14-bit layout not usable");
			AssertFallback(x => x.Options=null,"not known");
		}

		[TestMethod]
		public void RecordFromAnotherComputer_SameDriverAndImageSettings_Matches() {
			CaptureBitDepthInput input=Input();
			input.Record.ComputerName="OPERATORY7";
			input.Record.GatedSettingsHash=new string('c',64);//that computer's whole capture.ini: never compared
			input.ComputerName="OPERATORY1";
			Assert.AreNotEqual(input.GatedSettingsHash,input.Record.GatedSettingsHash);
			Assert.AreEqual(16,CaptureBitDepthDecision.Decide(input).Bits,"The record covers every computer with the same driver and image settings.");
			Assert.IsTrue(input.Record.Matches(12," example sensor ds",new TwainDriverIdentity("EXAMPLE SENSOR DS",1,1,"1.1.0"),
				input.Record.ImageSettingsHash.ToUpperInvariant()));
		}

		[TestMethod]
		public void SettingsOutsideTheImageSettings_StillMatch() {
			//The record was written on a computer whose capture.ini differs from this one's only in the allowed sources and the journal size: the
			//whole-file hashes differ, the image settings do not, and the record matches (each computer's own settings check is a separate gate).
			LocalCaptureConfig configA=CaptureSessionTestConfig.Load(new[] { "[Twain]","Allow16Bit=1","RestrictSources=1","AllowedSources=Example Sensor DS",
				"JournalKeep=200","[Device.12]","Bits=16" });
			LocalCaptureConfig configB=CaptureSessionTestConfig.Load(new[] { "[Twain]","Allow16Bit=1","RestrictSources=1",
				"AllowedSources=Example Sensor DS|Scanner","JournalKeep=50","[Device.12]","Bits=16" });
			string hashA=GatedSettingsHash.Compute(configA,12);
			string hashB=GatedSettingsHash.Compute(configB,12);
			Assert.AreNotEqual(hashA,hashB,"The whole-file hashes differ.");
			CaptureBitDepthInput inputB=Input();
			inputB.Record=CaptureControllerHarness.PassedRecord(hashA,16,TwainCaptureOptions.FromConfig(configA,12));
			inputB.Options=TwainCaptureOptions.FromConfig(configB,12);
			inputB.GatedSettingsHash=hashB;
			inputB.SettingsCheck=CaptureSettingsTestKit.VerifiedHash(hashB);
			CaptureBitDepthChoice choiceB=CaptureBitDepthDecision.Decide(inputB);
			Assert.AreEqual(16,choiceB.Bits,choiceB.Reason8Bit);
			//Every other gated key outside the image settings too.
			CaptureBitDepthInput input=Input();
			input.Options.RestrictSources=true;
			input.Options.AllowedSources=new List<string> { "Example Sensor DS","Scanner" };
			input.Options.JournalKeep=7;
			input.Options.SessionMode=TwainSessionMode.Continuous;
			input.Options.Headless=true;
			input.Options.DisableIsSafe=true;
			input.Options.RedeliverCap=0x8003;
			Assert.AreEqual(16,CaptureBitDepthDecision.Decide(input).Bits);
			string hashBefore=CaptureBitDepthDecision.GetImageSettingsHash(new TwainCaptureOptions { ImagingDeviceNum=12 },12);
			Assert.AreEqual(hashBefore,CaptureBitDepthDecision.GetImageSettingsHash(input.Options,12));
			Assert.AreEqual(hashBefore,CaptureImageSettingsHash.Compute(new TwainCaptureOptions { ImagingDeviceNum=12,Bits=16 }),
				"The defaults and the same values written out hash alike.");
		}

		[TestMethod]
		public void ImageSettingsHash_BitsByteOrderPolarity_EachChangesIt() {
			string hashBase=CaptureImageSettingsHash.Compute(new TwainCaptureOptions { ImagingDeviceNum=12 });
			Assert.AreEqual(64,hashBase.Length);
			HashSet<string> hashSetHashes=new HashSet<string> { hashBase };
			Assert.IsTrue(hashSetHashes.Add(CaptureImageSettingsHash.Compute(new TwainCaptureOptions { ImagingDeviceNum=12,Bits=14 })));
			Assert.IsTrue(hashSetHashes.Add(CaptureImageSettingsHash.Compute(new TwainCaptureOptions { ImagingDeviceNum=12,Bits=8 })));
			Assert.IsTrue(hashSetHashes.Add(CaptureImageSettingsHash.Compute(new TwainCaptureOptions { ImagingDeviceNum=12,ByteOrder16=TwainByteOrder.BE })));
			Assert.IsTrue(hashSetHashes.Add(CaptureImageSettingsHash.Compute(new TwainCaptureOptions { ImagingDeviceNum=12,Polarity=TwainPolarity.Invert })));
			Assert.IsTrue(hashSetHashes.Add(CaptureImageSettingsHash.Compute(new TwainCaptureOptions { ImagingDeviceNum=13 })),"The device row too.");
			Assert.AreEqual("capture-image-settings/1\nDevice=12\nBits=16\nByteOrder16=LE\nPolarity=AsDelivered\n",
				CaptureImageSettingsHash.GetCanonicalText(12,16,TwainByteOrder.LE,TwainPolarity.AsDelivered));
			Assert.AreEqual(CaptureImageSettingsHash.GetCanonicalText(12,8,TwainByteOrder.LE,TwainPolarity.AsDelivered),
				CaptureImageSettingsHash.GetCanonicalText(12,12,TwainByteOrder.LE,TwainPolarity.AsDelivered),"A depth not allowed counts as 8, as the library reads it.");
			TestUtil.AssertThrows<ArgumentOutOfRangeException>(() => CaptureImageSettingsHash.Compute(new TwainCaptureOptions()));
		}

		[TestMethod]
		public void LegacyRecord_ReadButNeverMatches_RunTestCaptureAgain() {
			CaptureVerificationRecord record;
			Assert.IsTrue(CaptureVerificationRecord.TryParse(LegacyRecordText,out record));
			Assert.IsTrue(record.IsLegacyFormat);
			Assert.AreEqual(16,record.BitDepthVerified,"A legacy record that says 16 verified 16.");
			Assert.IsTrue(record.IsPassed);
			Assert.IsNull(record.DriverIdentity);
			Assert.AreEqual("OPERATORY1",record.ComputerName);
			Assert.IsFalse(record.Matches(12,"Example Sensor DS",CaptureControllerHarness.Driver,CaptureControllerHarness.PassedRecord(_hash).ImageSettingsHash));
			CaptureBitDepthInput input=Input();
			input.Record=record;
			input.ComputerName="OPERATORY1";
			CaptureBitDepthChoice choice=CaptureBitDepthDecision.Decide(input);
			Assert.AreEqual(8,choice.Bits);
			StringAssert.Contains(choice.ReasonFallback,"was made before records named the driver version and the image settings; run Test capture again");
			//A legacy record without its computer or hash is not a record.
			Assert.IsFalse(CaptureVerificationRecord.TryParse(LegacyRecordText.Replace("Computer=OPERATORY1","Computer="),out record));
			//A new record without its driver version or image-settings hash is not one either.
			string textNew=CaptureControllerHarness.PassedRecord(_hash).ToLogText();
			Assert.IsTrue(CaptureVerificationRecord.TryParse(textNew,out record));
			Assert.IsFalse(record.IsLegacyFormat);
			Assert.IsFalse(CaptureVerificationRecord.TryParse(textNew.Replace("DriverVersion=1.1;","DriverVersion=;"),out record));
			Assert.IsFalse(CaptureVerificationRecord.TryParse(textNew.Replace("ImageSettingsHash=","ImageSettingsHash=x"),out record));
		}

		[TestMethod]
		public void Record_RoundTrip_DriverImageSettingsAndDepth() {
			CaptureVerificationRecord recordWritten=CaptureControllerHarness.PassedRecord(_hash,14);
			recordWritten.DriverIdentity=new TwainDriverIdentity("Example Sensor DS",3,4,"3.4; build=7");
			CaptureVerificationRecord recordRead;
			Assert.IsTrue(CaptureVerificationRecord.TryParse(recordWritten.ToLogText(),out recordRead));
			StringAssert.StartsWith(recordWritten.ToLogText(),"Capture verification record (capture-verification/2): ");
			Assert.IsTrue(recordRead.DriverIdentity.IsSameAs(recordWritten.DriverIdentity),"Escaped ';' and '=' come back.");
			Assert.AreEqual(recordWritten.ImageSettingsHash,recordRead.ImageSettingsHash);
			Assert.AreEqual(14,recordRead.BitDepthInEffect);
			Assert.AreEqual(14,recordRead.BitDepthVerified);
			Assert.IsTrue(recordRead.IsPassedFor(14));
			Assert.IsFalse(recordRead.IsPassedFor(16));
			Assert.AreEqual("OPERATORY1",recordRead.ComputerName,"Kept for the audit trail.");
			Assert.AreEqual(_hash,recordRead.GatedSettingsHash,"Kept for the audit trail.");
		}

		[TestMethod]
		public void FallbackReasons_InTheirOrder() {
			//Configuration first, then the layout, the settings check, the record and the target.
			CaptureBitDepthInput input=Input();
			input.Options.Bits=8;
			input.ReasonLayoutUnusable="14-bit layout not usable: packed samples";
			input.SettingsCheck=null;
			input.Record=null;
			input.Target=null;
			Assert.AreEqual("8 bits: [Device.12] Bits is 8.",CaptureBitDepthDecision.Decide(input).Reason8Bit);
			input.Options.Bits=16;
			StringAssert.Contains(CaptureBitDepthDecision.Decide(input).ReasonFallback,"layout not usable");
			input.ReasonLayoutUnusable=null;
			StringAssert.Contains(CaptureBitDepthDecision.Decide(input).ReasonFallback,"not checked");
			input.SettingsCheck=CaptureSettingsTestKit.VerifiedHash(_hash);
			StringAssert.Contains(CaptureBitDepthDecision.Decide(input).ReasonFallback,"no Test capture record");
			input.Record=CaptureControllerHarness.PassedRecord(_hash);
			StringAssert.Contains(CaptureBitDepthDecision.Decide(input).ReasonFallback,"target is not known");
			input.Target=new IntraOralTarget { HasMountSlot=true,ToothNumbers="2,3" };
			Assert.AreEqual(16,CaptureBitDepthDecision.Decide(input).Bits);
		}

		[TestMethod]
		public void SettingsNotVerified_8Bits_EvenWithAMatchingRecord() {
			//Design 9.3: a hand edit after the last save in Capture settings; Test capture's record may even match the edited file.
			AssertFallback(x => x.SettingsCheck=CaptureSettingsCheck.CheckHash(_hash,12,"OPERATORY1",
				new List<SecurityLog> { CaptureSettingsTestKit.SavedLog(new string('c',64),12,"OPERATORY1") }),"changed outside Open Dental");
			//Never saved: "not verified". Saved on another computer only: still never saved here (the settings check stays per computer).
			AssertFallback(x => x.SettingsCheck=CaptureSettingsCheck.CheckHash(_hash,12,"OPERATORY1",new List<SecurityLog>()),"never been saved");
			AssertFallback(x => x.SettingsCheck=CaptureSettingsCheck.CheckHash(_hash,12,"OPERATORY1",
				new List<SecurityLog> { CaptureSettingsTestKit.SavedLog(_hash,12,"OPERATORY2") }),"never been saved");
			AssertFallback(x => x.SettingsCheck=CaptureSettingsCheck.Unknown(12,"OPERATORY1","the security log could not be read"),"could not be checked");
			AssertFallback(x => x.SettingsCheck=null,"not checked");
			//A check made for another file than the one in effect.
			AssertFallback(x => x.SettingsCheck=CaptureSettingsTestKit.VerifiedHash(new string('d',64)),"not the ones in effect");
			CaptureBitDepthInput input=Input();
			input.SettingsCheck=CaptureSettingsCheck.CheckHash(_hash,12,"OPERATORY1",new List<SecurityLog>());
			StringAssert.Contains(CaptureBitDepthDecision.Decide(input).Reason8Bit,"(not verified)");
		}

		[TestMethod]
		public void SettingsNotVerified_ComesAfterTheSwitches() {
			CaptureBitDepthInput input=Input();
			input.SettingsCheck=null;
			input.Options.Bits=8;
			StringAssert.Contains(CaptureBitDepthDecision.Decide(input).Reason8Bit,"Bits is 8","Bits=8 says so first.");
		}

		[TestMethod]
		public void BitDepthText_TheThreeForms() {
			Assert.AreEqual("16-bit",CaptureBitDepthText.Describe(16));
			Assert.AreEqual("14-bit in a 16-bit file",CaptureBitDepthText.Describe(14));
			Assert.AreEqual("8-bit",CaptureBitDepthText.Describe(8));
			Assert.AreEqual("8-bit (fallback: no Test capture record for this sensor's driver version and image settings)",
				CaptureBitDepthText.Describe(8,"no Test capture record for this sensor's driver version and image settings."));
			Assert.AreEqual("16-bit",CaptureBitDepthText.Describe(16,"ignored above 8"));
		}

		private static void AssertFallback(Action<CaptureBitDepthInput> actionChange,string fragment) {
			CaptureBitDepthInput input=Input();
			actionChange(input);
			CaptureBitDepthChoice choice=CaptureBitDepthDecision.Decide(input);
			Assert.AreEqual(8,choice.Bits,fragment);
			Assert.IsTrue(choice.IsFallback,fragment);
			StringAssert.Contains(choice.ReasonFallback,fragment);
			StringAssert.Contains(choice.Reason8Bit,fragment);
			StringAssert.StartsWith(choice.Reason8Bit,"8 bits (fallback): ");
		}
		#endregion Decision

		#region FindLatest
		private static SecurityLog Log(long securityLogNum,DateTime dateTime,CaptureVerificationRecord record) {
			return new SecurityLog { SecurityLogNum=securityLogNum,LogDateTime=dateTime,FKey=record.ImagingDeviceNum,PermType=EnumPermType.Setup,
				LogText=record.ToLogText() };
		}

		[TestMethod]
		public void FindLatest_LatestForTheDeviceOnAnyComputer_EvenWhenItNoLongerMatches() {
			CaptureVerificationRecord recordOld=CaptureControllerHarness.PassedRecord(_hash);
			CaptureVerificationRecord recordNew=CaptureControllerHarness.PassedRecord(new string('d',64));
			recordNew.DriverIdentity=new TwainDriverIdentity("Example Sensor DS",9,9,"9.9");
			CaptureVerificationRecord recordOtherPc=CaptureControllerHarness.PassedRecord(new string('5',64));
			recordOtherPc.ComputerName="OPERATORY2";
			CaptureVerificationRecord recordOtherDevice=CaptureControllerHarness.PassedRecord(_hash);
			recordOtherDevice.ImagingDeviceNum=13;
			List<SecurityLog> listLogs=new List<SecurityLog> {
				Log(5,new DateTime(2026,10,29,9,0,0),recordOld),
				Log(6,new DateTime(2026,10,30,9,0,0),recordNew),
				Log(8,new DateTime(2026,10,31,9,0,0),recordOtherDevice),
				new SecurityLog { SecurityLogNum=9,LogDateTime=new DateTime(2026,10,31,10,0,0),FKey=12,LogText="Imaging device edited." },
				new SecurityLog { SecurityLogNum=10,LogDateTime=new DateTime(2026,10,28,9,0,0),FKey=12,LogText=LegacyRecordText },
			};
			CaptureVerificationRecord recordFound=CaptureVerificationRecords.FindLatest(listLogs,12);
			Assert.IsNotNull(recordFound);
			Assert.AreEqual(new string('d',64),recordFound.GatedSettingsHash,"The newest one counts, though its driver version differs.");
			listLogs.Add(Log(7,new DateTime(2026,10,31,9,0,0),recordOtherPc));
			recordFound=CaptureVerificationRecords.FindLatest(listLogs,12);
			Assert.AreEqual("OPERATORY2",recordFound.ComputerName,"Made on another computer: found too (owner decision 2026-11-01).");
			Assert.IsNull(CaptureVerificationRecords.FindLatest(listLogs,14));
			Assert.IsNull(CaptureVerificationRecords.FindLatest(null,12));
			CaptureVerificationRecord recordLegacy=CaptureVerificationRecords.FindLatest(listLogs.Where(x => x.SecurityLogNum==10).ToList(),12);
			Assert.IsTrue(recordLegacy.IsLegacyFormat,"A legacy record is found, so the session can say to run Test capture again.");
		}

		[TestMethod]
		public void FindLatest_SameTime_HigherSecurityLogNumWins() {
			CaptureVerificationRecord recordA=CaptureControllerHarness.PassedRecord(new string('a',64));
			CaptureVerificationRecord recordB=CaptureControllerHarness.PassedRecord(new string('e',64));
			recordB.ComputerName="OPERATORY2";
			DateTime dateTime=new DateTime(2026,10,30,9,0,0);
			CaptureVerificationRecord recordFound=CaptureVerificationRecords.FindLatest(new List<SecurityLog> { Log(11,dateTime,recordB),Log(10,dateTime,recordA) },12);
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
