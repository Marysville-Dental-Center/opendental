// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImagingCapture.Session;
using ImagingCapture.Settings;
using ImagingDeviceManager.Capture;
using ImagingDeviceManager.Config;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace ImagingCapture.Tests {
	///<summary>The hash check of design 9.3 (CaptureSettingsCheck), the Capture settings screen's security-log entry (CaptureSettingsLogEntry),
	///the safe values, and the key catalog the screen edits (CaptureSettingsCatalog).</summary>
	[TestClass]
	public class CaptureSettingsCheckTests {
		private const string Ini="[Session]\r\nEnabled=1\r\n[Twain]\r\nAllow16Bit=1\r\nRestrictSources=1\r\nAllowedSources=Example Sensor DS\r\n"
			+"[Device.12]\r\nBits=16\r\nSafeDisableCap=0x8101\r\nDisableIsSafe=1\r\nHeadless=1\r\nSessionMode=Continuous\r\nContinuousCap=0x8102\r\n"
			+"RedeliverCap=0x8103\r\nNoActivateCap=0x8104\r\nRefusedDisableCodes=0x8006|0x8007\r\nSessionTimeoutMinutes=5\r\nPolarity=Invert\r\n"
			+"ByteOrder16=BE\r\n[Keys]\r\nRetake=F9\r\n";

		private static LocalCaptureConfig Config(string iniText) {
			return CaptureSessionTestConfig.Load(iniText.Split(new[] { "\r\n" },StringSplitOptions.None));
		}

		#region Log entry
		[TestMethod]
		public void LogEntry_RoundTrip_WithEscapedValues() {
			CaptureSettingsLogEntry entry=new CaptureSettingsLogEntry {
				ImagingDeviceNum=12,
				DeviceDescription="Sensor; room 1",
				ComputerName="OPERATORY1",
				FilePath=@"C:\ProgramData\OpenPMS-Dental\Imaging\capture.ini",
				GatedSettingsHash=new string('a',64),
				PreviousGatedSettingsHash=new string('b',64),
				DateTimeSaved=new DateTime(2026,10,31,9,15,30),
				Note="Saved in Capture settings.",
			};
			entry.ListChanges.Add(new CaptureSettingChange { Section="Twain",Key="AllowedSources",OldValue="A|B",NewValue="A=1|100%",IsGated=true });
			entry.ListChanges.Add(new CaptureSettingChange { Section="Device.12",Key="Bits",OldValue="",NewValue="16",IsGated=true });
			entry.ListChanges.Add(new CaptureSettingChange { Section="Keys",Key="Retake",OldValue="F9",NewValue="",IsGated=false });
			entry.DictGatedValues["Session.Enabled"]="1";
			entry.DictGatedValues["Twain.AllowedSources"]="A=1|100%";
			entry.DictGatedValues["Device.Bits"]="16";
			string logText=entry.ToLogText();
			StringAssert.StartsWith(logText,CaptureSettingsLogEntry.LogPrefix);
			StringAssert.Contains(logText,"Change2=Device.12.Bits; Change2.Old=; Change2.New=16; Change2.Gated=1");
			StringAssert.Contains(logText,"GatedSettingsHash="+new string('a',64));
			CaptureSettingsLogEntry entryRead;
			Assert.IsTrue(CaptureSettingsLogEntry.TryParse(logText,out entryRead));
			Assert.AreEqual(12,entryRead.ImagingDeviceNum);
			Assert.AreEqual("Sensor; room 1",entryRead.DeviceDescription);
			Assert.AreEqual("OPERATORY1",entryRead.ComputerName);
			Assert.AreEqual(entry.FilePath,entryRead.FilePath);
			Assert.AreEqual(new string('a',64),entryRead.GatedSettingsHash);
			Assert.AreEqual(new string('b',64),entryRead.PreviousGatedSettingsHash);
			Assert.AreEqual(entry.DateTimeSaved,entryRead.DateTimeSaved);
			Assert.AreEqual(entry.Note,entryRead.Note);
			Assert.AreEqual(3,entryRead.ListChanges.Count);
			Assert.AreEqual("Twain.AllowedSources",entryRead.ListChanges[0].Label);
			Assert.AreEqual("A=1|100%",entryRead.ListChanges[0].NewValue);
			Assert.AreEqual("Device.12",entryRead.ListChanges[1].Section);
			Assert.AreEqual("Bits",entryRead.ListChanges[1].Key);
			Assert.AreEqual("",entryRead.ListChanges[1].OldValue);
			Assert.IsTrue(entryRead.ListChanges[1].IsGated);
			Assert.IsFalse(entryRead.ListChanges[2].IsGated);
			Assert.AreEqual("[Device.12] Bits: (empty) -> 16",entryRead.ListChanges[1].GetText());
			Assert.AreEqual("A=1|100%",entryRead.GetGatedValue("Twain.AllowedSources"));
			Assert.AreEqual("16",entryRead.GetGatedValue("device.bits"),"Names ignore case.");
			Assert.IsNull(entryRead.GetGatedValue("Twain.JournalKeep"));
		}

		[TestMethod]
		public void LogEntry_TryParse_OnlyCompleteEntries() {
			CaptureSettingsLogEntry entry;
			Assert.IsFalse(CaptureSettingsLogEntry.TryParse(null,out entry));
			Assert.IsFalse(CaptureSettingsLogEntry.TryParse("Imaging device edited.",out entry));
			Assert.IsFalse(CaptureSettingsLogEntry.TryParse(CaptureControllerHarness.PassedRecord(new string('a',64)).ToLogText(),out entry),
				"A verification record is not a settings entry.");
			Assert.IsFalse(CaptureSettingsLogEntry.TryParse(CaptureSettingsLogEntry.LogPrefix+"Device=0; Computer=PC; GatedSettingsHash="+new string('a',64),out entry));
			Assert.IsFalse(CaptureSettingsLogEntry.TryParse(CaptureSettingsLogEntry.LogPrefix+"Device=3; GatedSettingsHash="+new string('a',64),out entry));
			Assert.IsFalse(CaptureSettingsLogEntry.TryParse(CaptureSettingsLogEntry.LogPrefix+"Device=3; Computer=PC; GatedSettingsHash=abc",out entry));
			Assert.IsTrue(CaptureSettingsLogEntry.TryParse(CaptureSettingsLogEntry.LogPrefix+"Device=3; Computer=PC; GatedSettingsHash="+new string('B',64),out entry));
			Assert.AreEqual(new string('b',64),entry.GatedSettingsHash);
			Assert.AreEqual(0,entry.ListChanges.Count);
		}

		[TestMethod]
		public void FindLatest_LatestForTheDeviceAndComputer() {
			string hashOld=new string('1',64);
			string hashNew=new string('2',64);
			List<SecurityLog> listLogs=new List<SecurityLog> {
				CaptureSettingsTestKit.SavedLog(hashOld,12,"OPERATORY1",5,new DateTime(2026,10,29,9,0,0)),
				CaptureSettingsTestKit.SavedLog(hashNew,12,"operatory1 ",6,new DateTime(2026,10,30,9,0,0)),
				CaptureSettingsTestKit.SavedLog(new string('3',64),12,"OPERATORY2",7,new DateTime(2026,10,31,9,0,0)),
				CaptureSettingsTestKit.SavedLog(new string('4',64),13,"OPERATORY1",8,new DateTime(2026,10,31,9,0,0)),
				new SecurityLog { SecurityLogNum=9,FKey=12,LogDateTime=new DateTime(2026,10,31,10,0,0),
					LogText=CaptureControllerHarness.PassedRecord(new string('5',64)).ToLogText() },
			};
			Assert.AreEqual(hashNew,CaptureSettingsLogEntry.FindLatest(listLogs,12," OPERATORY1").GatedSettingsHash);
			Assert.IsNull(CaptureSettingsLogEntry.FindLatest(listLogs,12,"OPERATORY3"));
			Assert.IsNull(CaptureSettingsLogEntry.FindLatest(null,12,"OPERATORY1"));
			DateTime dateTime=new DateTime(2026,11,1,9,0,0);
			listLogs.Add(CaptureSettingsTestKit.SavedLog(new string('7',64),12,"OPERATORY1",21,dateTime));
			listLogs.Add(CaptureSettingsTestKit.SavedLog(new string('6',64),12,"OPERATORY1",20,dateTime));
			Assert.AreEqual(new string('7',64),CaptureSettingsLogEntry.FindLatest(listLogs,12,"OPERATORY1").GatedSettingsHash,"Same time: the higher SecurityLogNum.");
		}
		#endregion Log entry

		#region Check
		[TestMethod]
		public void Check_SavedFile_Verified() {
			LocalCaptureConfig config=Config(Ini);
			CaptureSettingsCheck check=CaptureSettingsCheck.Check(config,12,"OPERATORY1",new[] { CaptureSettingsTestKit.SavedLog(config,12,"OPERATORY1") });
			Assert.AreEqual(CaptureSettingsStatus.Verified,check.Status);
			Assert.IsTrue(check.IsVerified);
			Assert.AreEqual("",check.Text);
			Assert.IsNull(check.Reason8Bit);
			Assert.AreEqual(GatedSettingsHash.Compute(config,12),check.CurrentHash);
			Assert.IsNotNull(check.LastSaved);
		}

		[TestMethod]
		public void Check_HandEditOfAGatedKey_ChangedOutside_NamesTheKeys() {
			LocalCaptureConfig configSaved=Config(Ini);
			LocalCaptureConfig config=Config(Ini.Replace("Bits=16","Bits=8").Replace("Allow16Bit=1","Allow16Bit=0"));
			CaptureSettingsCheck check=CaptureSettingsCheck.Check(config,12,"OPERATORY1",
				new[] { CaptureSettingsTestKit.SavedLog(configSaved,12,"OPERATORY1",dateTime:new DateTime(2026,10,29,8,0,0)) });
			Assert.AreEqual(CaptureSettingsStatus.ChangedOutside,check.Status);
			CollectionAssert.AreEqual(new[] { "Twain.Allow16Bit","Device.Bits" },check.ListChangedLabels);
			StringAssert.StartsWith(check.Text,"Capture settings were changed outside Open Dental; review them in Setup.");
			StringAssert.Contains(check.Text,"2026-10-29 08:00");
			StringAssert.Contains(check.Text,"(Twain.Allow16Bit, Device.Bits)");
			StringAssert.Contains(check.Reason8Bit,"changed outside Open Dental");
		}

		[TestMethod]
		public void Check_HandEditOfAHardwareBinding_StillVerified() {
			LocalCaptureConfig configSaved=Config(Ini);
			LocalCaptureConfig config=Config(Ini.Replace("Retake=F9","Retake=F10")+"[Camera]\r\nName=Other\r\n[Device.13]\r\nBits=16\r\n");
			CaptureSettingsCheck check=CaptureSettingsCheck.Check(config,12,"OPERATORY1",new[] { CaptureSettingsTestKit.SavedLog(configSaved,12,"OPERATORY1") });
			Assert.AreEqual(CaptureSettingsStatus.Verified,check.Status,"[Keys], [Camera] and other devices are not this device's gated block.");
		}

		[TestMethod]
		public void Check_SharedKeysChangedForAnotherDevice_ChangedOutsideForThisOne() {
			//[Session] and [Twain] are in every device's hash.
			LocalCaptureConfig configSaved=Config(Ini);
			LocalCaptureConfig config=Config(Ini.Replace("Enabled=1","Enabled=0"));
			CaptureSettingsCheck check=CaptureSettingsCheck.Check(config,12,"OPERATORY1",new[] { CaptureSettingsTestKit.SavedLog(configSaved,12,"OPERATORY1") });
			Assert.AreEqual(CaptureSettingsStatus.ChangedOutside,check.Status);
			CollectionAssert.AreEqual(new[] { "Session.Enabled" },check.ListChangedLabels);
		}

		[TestMethod]
		public void Check_MissingFileWhileASavedConfigurationExists_ChangedOutside() {
			LocalCaptureConfig configSaved=Config(Ini);
			string dir=Path.Combine(Path.GetTempPath(),"CaptureSettingsCheckTests_"+Guid.NewGuid().ToString("N"));
			LocalCaptureConfig configMissing=LocalCaptureConfig.Load(Path.Combine(dir,"capture.ini"),Path.Combine(dir,"fallback.ini"));
			CaptureSettingsCheck check=CaptureSettingsCheck.Check(configMissing,12,"OPERATORY1",
				new[] { CaptureSettingsTestKit.SavedLog(configSaved,12,"OPERATORY1") });
			Assert.AreEqual(CaptureSettingsStatus.ChangedOutside,check.Status,"Design 9.3: a missing file when a logged configuration exists.");
		}

		[TestMethod]
		public void Check_NeverSaved_SaidPlainly_NotVerifiedFor16Bit() {
			LocalCaptureConfig config=Config(Ini);
			CaptureSettingsCheck check=CaptureSettingsCheck.Check(config,12,"OPERATORY1",new SecurityLog[0]);
			Assert.AreEqual(CaptureSettingsStatus.NeverSaved,check.Status);
			Assert.IsNull(check.LastSaved);
			Assert.AreEqual(CaptureSettingsCheck.TextNeverSaved+" "+CaptureSettingsCheck.TextConsequences,check.Text);
			StringAssert.Contains(check.Text,"16-bit capture is not verified");
			StringAssert.Contains(check.Reason8Bit,"never been saved");
			StringAssert.Contains(check.Reason8Bit,"(not verified)");
		}

		[TestMethod]
		public void Check_UnsavedDeviceOrUnreadableLog_Unknown() {
			LocalCaptureConfig config=Config(Ini);
			CaptureSettingsCheck check=CaptureSettingsCheck.Check(config,0,"OPERATORY1",new SecurityLog[0]);
			Assert.AreEqual(CaptureSettingsStatus.Unknown,check.Status);
			StringAssert.Contains(check.Text,"could not be checked");
			check=CaptureSettingsCheck.Run(config,12,"OPERATORY1",x => { throw new Exception("The database is not reachable."); });
			Assert.AreEqual(CaptureSettingsStatus.Unknown,check.Status);
			StringAssert.Contains(check.Text,"The database is not reachable");
			Assert.IsFalse(check.IsVerified);
			Assert.AreEqual(CaptureSettingsStatus.Unknown,CaptureSettingsCheck.Run(config,12,"OPERATORY1",null).Status);
			Assert.AreEqual(CaptureSettingsStatus.Verified,
				CaptureSettingsCheck.Run(config,12,"OPERATORY1",x => new[] { CaptureSettingsTestKit.SavedLog(config,x,"OPERATORY1") }).Status);
		}
		#endregion Check

		#region Safe values
		[TestMethod]
		public void OptionsInEffect_Verified_TheFilesOwn() {
			LocalCaptureConfig config=Config(Ini);
			TwainCaptureOptions optionsFile=TwainCaptureOptions.FromConfig(config,12);
			CaptureSettingsCheck check=CaptureSettingsTestKit.Verified(config,12,"OPERATORY1");
			Assert.AreSame(optionsFile,check.GetOptionsInEffect(optionsFile));
		}

		[TestMethod]
		public void OptionsInEffect_ChangedOutside_SafeValues_SourceGateAsLastSaved() {
			LocalCaptureConfig configSaved=Config("[Twain]\r\nRestrictSources=1\r\nAllowedSources=Scanner|Other DS\r\n");
			LocalCaptureConfig config=Config(Ini.Replace("RestrictSources=1","RestrictSources=0"));
			CaptureSettingsCheck check=CaptureSettingsCheck.Check(config,12,"OPERATORY1",new[] { CaptureSettingsTestKit.SavedLog(configSaved,12,"OPERATORY1") });
			TwainCaptureOptions optionsFile=TwainCaptureOptions.FromConfig(config,12);
			optionsFile.OpenTimeout=TimeSpan.FromSeconds(7);
			TwainCaptureOptions options=check.GetOptionsInEffect(optionsFile);
			Assert.AreNotSame(optionsFile,options);
			Assert.AreEqual(16,optionsFile.Bits,"The file's options are not changed.");
			AssertSafe(options);
			Assert.IsTrue(options.RestrictSources,"RestrictSources as last saved.");
			CollectionAssert.AreEqual(new[] { "Scanner","Other DS" },options.AllowedSources);
			Assert.IsFalse(options.IsSourceAllowed("Example Sensor DS"));
			Assert.AreEqual(TimeSpan.FromSeconds(7),options.OpenTimeout);
			Assert.AreEqual(12,options.ImagingDeviceNum);
			Assert.IsTrue(options.ListWarnings.Any(x => x.Contains("Safe values are in effect")));
		}

		[TestMethod]
		public void OptionsInEffect_NeverSaved_SafeValues_SourceGateFromTheFile() {
			LocalCaptureConfig config=Config(Ini);
			CaptureSettingsCheck check=CaptureSettingsCheck.Check(config,12,"OPERATORY1",new SecurityLog[0]);
			TwainCaptureOptions options=check.GetOptionsInEffect(TwainCaptureOptions.FromConfig(config,12));
			AssertSafe(options);
			Assert.IsTrue(options.RestrictSources,"Design 9.3: RestrictSources and AllowedSources keep their file values.");
			CollectionAssert.AreEqual(new[] { "Example Sensor DS" },options.AllowedSources);
		}

		private static void AssertSafe(TwainCaptureOptions options) {
			Assert.AreEqual(TwainSessionMode.PerSlot,options.SessionMode);
			Assert.AreEqual(8,options.Bits);
			Assert.AreEqual(TwainByteOrder.LE,options.ByteOrder16);
			Assert.AreEqual(TwainPolarity.AsDelivered,options.Polarity);
			Assert.IsFalse(options.Headless);
			Assert.IsFalse(options.DisableIsSafe);
			Assert.AreEqual(TimeSpan.Zero,options.SessionTimeout);
			Assert.IsNull(options.SafeDisableCap);
			Assert.AreEqual(0,options.RefusedDisableCodes.Count);
			Assert.IsNull(options.ContinuousCap);
			Assert.IsNull(options.RedeliverCap);
			Assert.IsNull(options.NoActivateCap);
			Assert.IsFalse(options.Allow16Bit);
			Assert.IsFalse(options.IsAbove8BitConfigured);
			Assert.AreEqual(200,options.JournalKeep);
		}
		#endregion Safe values

		#region Catalog
		[TestMethod]
		public void Catalog_GatedKeys_AreExactlyTheHashedOnes() {
			List<string> listGated=CaptureSettingsCatalog.ListSettings.Where(x => x.IsGated).Select(x => x.Label).ToList();
			List<string> listHashed=GatedSettingsHash.ListSessionKeys.Select(x => "Session."+x)
				.Concat(GatedSettingsHash.ListTwainKeys.Select(x => "Twain."+x))
				.Concat(GatedSettingsHash.ListDeviceKeys.Select(x => "Device."+x)).ToList();
			CollectionAssert.AreEqual(listHashed,listGated,"Same keys, in hash order.");
			foreach(CaptureSettingDef settingDef in CaptureSettingsCatalog.ListSettings) {
				Assert.AreEqual(settingDef.IsGated,GatedSettingsHash.IsGatedKey(settingDef.GetSectionName(12),settingDef.Key),settingDef.Label);
				Assert.AreSame(settingDef,CaptureSettingsCatalog.Find(settingDef.Section.ToLowerInvariant(),settingDef.Key.ToUpperInvariant()));
			}
			Assert.AreEqual(8,CaptureSettingsCatalog.ListSettings.Count(x => x.Section=="Keys"));
			Assert.AreEqual(4,CaptureSettingsCatalog.ListSettings.Count(x => x.Section=="Sounds"));
			Assert.AreEqual("[Device.12] Bits",CaptureSettingsCatalog.Find("Device","Bits").GetDisplayName(12));
			Assert.IsNull(CaptureSettingsCatalog.Find("Camera","Name"),"The camera sections stay with FormVideo's settings.");
		}

		[TestMethod]
		public void Catalog_DefaultsAreTheLibrarysDefaults() {
			//Every default, written into a file, reads back as what the library uses for a missing key.
			LocalCaptureConfig configEmpty=LocalCaptureConfig.Load((string)null);
			LocalCaptureConfig configDefaults=LocalCaptureConfig.Load((string)null);
			foreach(CaptureSettingDef settingDef in CaptureSettingsCatalog.ListSettings.Where(x => x.IsGated)) {
				configDefaults.Set(settingDef.GetSectionName(12),settingDef.Key,settingDef.DefaultText);
			}
			TwainCaptureOptions optionsEmpty=TwainCaptureOptions.FromConfig(configEmpty,12);
			TwainCaptureOptions optionsDefaults=TwainCaptureOptions.FromConfig(configDefaults,12);
			Assert.AreEqual(0,optionsDefaults.ListWarnings.Count,string.Join("; ",optionsDefaults.ListWarnings));
			Assert.AreEqual(optionsEmpty.SessionMode,optionsDefaults.SessionMode);
			Assert.AreEqual(optionsEmpty.Bits,optionsDefaults.Bits);
			Assert.AreEqual(optionsEmpty.ByteOrder16,optionsDefaults.ByteOrder16);
			Assert.AreEqual(optionsEmpty.Polarity,optionsDefaults.Polarity);
			Assert.AreEqual(optionsEmpty.JournalKeep,optionsDefaults.JournalKeep);
			Assert.AreEqual(16,optionsDefaults.Bits,"Owner decision 2026-11-01.");
			Assert.AreEqual(configEmpty.Allow16Bit,configDefaults.Allow16Bit);
			Assert.IsTrue(configDefaults.Allow16Bit,"Owner decision 2026-11-01.");
			Assert.AreEqual(optionsEmpty.Allow16Bit,optionsDefaults.Allow16Bit);
			CollectionAssert.AreEqual(new[] { "16","14","8" },CaptureSettingsCatalog.Find("Device","Bits").ArrayChoices.ToArray());
			Assert.AreEqual(configEmpty.ThreadModal,configDefaults.ThreadModal);
			Assert.AreEqual(configEmpty.AcquireTimeoutMinutes,configDefaults.AcquireTimeoutMinutes);
			CaptureSessionConfig sessionEmpty=CaptureSessionConfig.Read(configEmpty);
			CaptureSessionConfig sessionDefaults=CaptureSessionConfig.Read(configDefaults);
			Assert.AreEqual(sessionEmpty.IsEnabled,sessionDefaults.IsEnabled);
			Assert.AreEqual(sessionEmpty.IdleDisableMinutes,sessionDefaults.IdleDisableMinutes);
			foreach(CaptureSound sound in new[] { CaptureSound.Saved,CaptureSound.NotReceived,CaptureSound.Disabled }) {
				Assert.AreEqual(sessionEmpty.GetSoundName(sound),CaptureSettingsCatalog.Find("Sounds",sound.ToString()).DefaultText);
			}
		}
		#endregion Catalog
	}
}
