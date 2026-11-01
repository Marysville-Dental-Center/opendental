// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ImagingCapture.Settings;
using ImagingDeviceManager.Config;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace ImagingCapture.Tests {
	///<summary>A security log and the imaging device rows for the Capture settings tests.</summary>
	internal sealed class FakeCaptureSettingsStore:ICaptureSettingsStore {
		public bool IsAuthorizedResult=true;
		public readonly List<EnumPermType> ListPermsChecked=new List<EnumPermType>();
		public readonly List<SecurityLog> ListLogs=new List<SecurityLog>();
		public readonly List<ImagingDevice> ListImagingDevices=new List<ImagingDevice>();
		public string ThrowOnMakeLogEntry;
		private DateTime _dateTimeNext=new DateTime(2026,10,31,9,0,0);

		public bool IsAuthorized(EnumPermType permType) {
			ListPermsChecked.Add(permType);
			return IsAuthorizedResult;
		}

		public List<SecurityLog> GetSetupLogEntries(long imagingDeviceNum) {
			return ListLogs.Where(x => x.PermType==EnumPermType.Setup && x.FKey==imagingDeviceNum).ToList();
		}

		public void MakeLogEntry(EnumPermType permType,long patNum,string logText,long fKey) {
			if(ThrowOnMakeLogEntry!=null) {
				throw new Exception(ThrowOnMakeLogEntry);
			}
			_dateTimeNext=_dateTimeNext.AddMinutes(1);
			ListLogs.Add(new SecurityLog { SecurityLogNum=ListLogs.Count+1,PermType=permType,PatNum=patNum,LogText=logText,FKey=fKey,LogDateTime=_dateTimeNext });
		}

		public List<ImagingDevice> GetImagingDevices() {
			return ListImagingDevices.ToList();
		}

		///<summary>The entries of the Capture settings screen for imagingDeviceNum, oldest first.</summary>
		public List<CaptureSettingsLogEntry> GetEntries(long imagingDeviceNum) {
			List<CaptureSettingsLogEntry> listEntries=new List<CaptureSettingsLogEntry>();
			foreach(SecurityLog securityLog in GetSetupLogEntries(imagingDeviceNum)) {
				CaptureSettingsLogEntry entry;
				if(CaptureSettingsLogEntry.TryParse(securityLog.LogText,out entry)) {
					listEntries.Add(entry);
				}
			}
			return listEntries;
		}
	}

	///<summary>CaptureSettingsModel (design 9.3, 10.7): the Capture settings screen's values, checks, save and security-log entry, on temporary
	///capture.ini files and a fake security log.</summary>
	[TestClass]
	public class CaptureSettingsModelTests {
		private const string Ini="; capture settings of this computer\r\n[Session]\r\nEnabled=0\r\n[Twain]\r\nAllow16Bit=0\r\nRestrictSources=1\r\n"
			+"AllowedSources=Example Sensor DS\r\n[Device.12]\r\nBits=8 ; gated\r\n[Device.13]\r\nBits=16\r\n[Camera]\r\nName=Not touched\r\n";
		private string _tempDir;
		private string _path;
		private FakeCaptureSettingsStore _store;
		///<summary>When set, the next loads return this config (once), to simulate a file that changes between reads.</summary>
		private Queue<Func<LocalCaptureConfig>> _queueLoads;

		[TestInitialize]
		public void Init() {
			_tempDir=Path.Combine(Path.GetTempPath(),"CaptureSettingsModelTests_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_tempDir);
			_path=Path.Combine(_tempDir,"capture.ini");
			Write(Ini);
			_store=new FakeCaptureSettingsStore();
			_store.ListImagingDevices.Add(new ImagingDevice { ImagingDeviceNum=12,Description="Intraoral sensor" });
			_store.ListImagingDevices.Add(new ImagingDevice { ImagingDeviceNum=13,Description="Second sensor" });
			_store.ListImagingDevices.Add(new ImagingDevice { ImagingDeviceNum=14,Description="Scanner" });
			_queueLoads=new Queue<Func<LocalCaptureConfig>>();
		}

		[TestCleanup]
		public void Cleanup() {
			try {
				Directory.Delete(_tempDir,true);
			}
			catch {
			}
		}

		private void Write(string iniText) {
			File.WriteAllBytes(_path,new UTF8Encoding(false).GetBytes(iniText));
		}

		private LocalCaptureConfig LoadFile() {
			return LocalCaptureConfig.Load(_path,Path.Combine(_tempDir,"fallback.ini"));
		}

		private CaptureSettingsModel Model(long imagingDeviceNum=12) {
			CaptureSettingsModel model=new CaptureSettingsModel(new CaptureSettingsDevice { ImagingDeviceNum=imagingDeviceNum,Description="Intraoral sensor",
				TwainName="Example Sensor DS" },new CaptureSettingsServices {
					Store=_store,
					FuncLoadConfig=() => _queueLoads.Count>0 ? _queueLoads.Dequeue()() : LoadFile(),
					FuncGetComputerName=() => "OPERATORY1",
					FuncNow=() => new DateTime(2026,10,31,9,30,0),
				});
			model.Load();
			return model;
		}

		private static int Idx(CaptureSettingsModel model,string section,string key) {
			int idx=model.IndexOf(section,key);
			Assert.IsTrue(idx>=0,section+"."+key);
			return idx;
		}

		private static CaptureSettingRow Row(CaptureSettingsModel model,string section,string key) {
			return model.ListRows[Idx(model,section,key)];
		}

		#region Load
		[TestMethod]
		public void Load_EveryKeyOfTheDevice_ValuesFromTheFile_NeverSavedSaidPlainly() {
			CaptureSettingsModel model=Model();
			Assert.AreEqual(CaptureSettingsCatalog.ListSettings.Count,model.ListRows.Count);
			Assert.AreEqual("8",Row(model,"Device","Bits").Text,"[Device.12], not [Device.13]; the comment is not part of the value.");
			Assert.AreEqual("[Device.12] Bits",Row(model,"Device","Bits").DisplayName);
			Assert.IsTrue(Row(model,"Device","Bits").IsPresent);
			Assert.AreEqual("",Row(model,"Device","Polarity").Text);
			Assert.IsFalse(Row(model,"Device","Polarity").IsPresent);
			Assert.AreEqual("Example Sensor DS",Row(model,"Twain","AllowedSources").Text);
			Assert.IsTrue(Row(model,"Twain","Allow16Bit").Def.IsGated);
			Assert.IsFalse(Row(model,"Keys","Retake").Def.IsGated);
			Assert.IsFalse(model.HasChanges);
			Assert.IsFalse(model.HasProblems);
			StringAssert.Contains(model.FileText,_path);
			Assert.AreEqual(CaptureSettingsStatus.NeverSaved,model.SettingsCheck.Status);
			StringAssert.StartsWith(model.SettingsCheck.Text,CaptureSettingsCheck.TextNeverSaved);
			Assert.IsNull(Row(model,"Twain","Allow16Bit").SavedText);
		}

		[TestMethod]
		public void Load_MissingFile_DefaultsAndTheFileThatWillBeCreated() {
			File.Delete(_path);
			CaptureSettingsModel model=Model();
			Assert.AreEqual("",Row(model,"Session","Enabled").Text);
			StringAssert.Contains(model.FileText,"not created yet");
		}

		[TestMethod]
		public void Constructor_UnsavedDevice_Refused() {
			try {
				new CaptureSettingsModel(new CaptureSettingsDevice { ImagingDeviceNum=0 },new CaptureSettingsServices { Store=_store });
				Assert.Fail("A device that is not saved has no [Device.N].");
			}
			catch(ArgumentException) {
			}
		}
		#endregion Load

		#region Values
		[TestMethod]
		public void Values_CheckedWithTheLibrarysParsers() {
			AssertProblem("Session","Enabled","2","is not 0 or 1");
			AssertProblem("Session","IdleDisableMinutes","-1","is negative");
			AssertProblem("Session","IdleDisableMinutes","ten","is not a whole number");
			AssertProblem("Twain","JournalKeep","1.5","is not a whole number");
			AssertProblem("Device","Bits","12","[Device.12] Bits=12 is not 8 or 16");
			AssertProblem("Device","SessionMode","Batch","is not one of PerSlot, Continuous");
			AssertProblem("Device","Polarity","1","is not one of");
			AssertProblem("Device","SafeDisableCap","0x10000","is not a capability number");
			AssertProblem("Twain","SafeDisableCap","zz","is not a capability number");
			AssertProblem("Device","RefusedDisableCodes","0x8006|zz","is not a list of condition codes");
			AssertProblem("Twain","AllowedSources","Scanner;Other","cannot contain ';'");
			AssertProblem("Keys","Retake","Hyper+F9","is not a modifier");
			AssertProblem("Sounds","Saved","Asterisk","is not a sound name");
			foreach(string[] arrayValid in new[] {
				new[] { "Session","Enabled","yes" },new[] { "Session","IdleDisableMinutes","0x0A" },new[] { "Device","Bits","16" },
				new[] { "Device","SessionMode","continuous" },new[] { "Device","ContinuousCap","8102" },new[] { "Device","RefusedDisableCodes","0x8006 | 0x8007" },
				new[] { "Keys","Confirm","Ctrl+F12" },new[] { "Sounds","Error","" },new[] { "Sounds","Disabled","System Exclamation" },
				new[] { "Twain","AllowedSources","Scanner|Other DS" } })
			{
				CaptureSettingsModel model=Model();
				model.SetText(Idx(model,arrayValid[0],arrayValid[1]),arrayValid[2]);
				Assert.AreEqual("",Row(model,arrayValid[0],arrayValid[1]).Problem,string.Join(".",arrayValid));
			}
		}

		private void AssertProblem(string section,string key,string text,string fragment) {
			CaptureSettingsModel model=Model();
			model.SetText(Idx(model,section,key),text);
			Assert.IsTrue(model.HasProblems,section+"."+key+"="+text);
			StringAssert.Contains(Row(model,section,key).Problem,fragment);
			Assert.AreEqual(1,model.GetProblemTexts().Count);
		}

		[TestMethod]
		public void Keys_ARepeatedChordIsAProblem_ConfirmNeverASessionKey() {
			CaptureSettingsModel model=Model();
			model.SetText(Idx(model,"Keys","Retake"),"F9");
			model.SetText(Idx(model,"Keys","Skip"),"f9");
			StringAssert.Contains(Row(model,"Keys","Skip").Problem,"F9 is already Retake");
			Assert.AreEqual("",Row(model,"Keys","Retake").Problem);
			model.SetText(Idx(model,"Keys","Skip"),"F10");
			model.SetText(Idx(model,"Keys","Confirm"),"F10");
			StringAssert.Contains(Row(model,"Keys","Confirm").Problem,"a session key never confirms");
			model.SetText(Idx(model,"Keys","Confirm"),"F12");
			Assert.IsFalse(model.HasProblems);
		}

		[TestMethod]
		public void Edits_CountedAndUndone() {
			CaptureSettingsModel model=Model();
			int version=model.Version;
			model.SetText(Idx(model,"Twain","Allow16Bit")," 1 ");
			Assert.AreEqual("1",Row(model,"Twain","Allow16Bit").Text);
			Assert.IsTrue(model.Version>version);
			Assert.IsTrue(model.HasChanges);
			Assert.AreEqual(1,model.CountChanges);
			CaptureSettingChange change=model.GetPendingChanges().Single();
			Assert.AreEqual("[Twain] Allow16Bit: 0 -> 1",change.GetText());
			Assert.IsTrue(change.IsGated);
			model.UndoChanges();
			Assert.IsFalse(model.HasChanges);
			Assert.IsFalse(model.SetText(-1,"x"));
		}
		#endregion Values

		#region Save
		[TestMethod]
		public void Save_WritesOnlyTheEdits_LogsOldNewValuesAndTheNewHash_ThenVerified() {
			CaptureSettingsModel model=Model();
			string hashBefore=GatedSettingsHash.Compute(LoadFile(),12);
			model.SetText(Idx(model,"Session","Enabled"),"1");
			model.SetText(Idx(model,"Device","Polarity"),"Invert");
			model.SetText(Idx(model,"Keys","Retake"),"F9");
			CaptureSettingsSaveResult result=model.Save();
			Assert.IsTrue(result.IsSaved,result.Error);
			Assert.IsTrue(result.IsLogged,result.Error);
			Assert.AreEqual("",result.Error);
			Assert.IsTrue(_store.ListPermsChecked.Contains(EnumPermType.Setup));
			string text=File.ReadAllText(_path);
			StringAssert.Contains(text,"; capture settings of this computer","Comments stay.");
			StringAssert.Contains(text,"Bits=8 ; gated");
			StringAssert.Contains(text,"Name=Not touched");
			LocalCaptureConfig configSaved=LoadFile();
			Assert.AreEqual("1",configSaved.GetString("Session","Enabled"));
			Assert.AreEqual("Invert",configSaved.GetString("Device.12","Polarity"));
			Assert.AreEqual("F9",configSaved.GetString("Keys","Retake"));
			SecurityLog securityLog=_store.ListLogs.Single();
			Assert.AreEqual(EnumPermType.Setup,securityLog.PermType);
			Assert.AreEqual(0,securityLog.PatNum);
			Assert.AreEqual(12,securityLog.FKey);
			CaptureSettingsLogEntry entry;
			Assert.IsTrue(CaptureSettingsLogEntry.TryParse(securityLog.LogText,out entry),securityLog.LogText);
			Assert.AreEqual(GatedSettingsHash.Compute(configSaved,12),entry.GatedSettingsHash,"The hash of the file as saved.");
			Assert.AreEqual(hashBefore,entry.PreviousGatedSettingsHash);
			Assert.AreEqual("OPERATORY1",entry.ComputerName);
			Assert.AreEqual(_path,entry.FilePath);
			Assert.AreEqual(new DateTime(2026,10,31,9,30,0),entry.DateTimeSaved);
			CollectionAssert.AreEqual(new[] { "Session.Enabled","Device.12.Polarity","Keys.Retake" },entry.ListChanges.Select(x => x.Label).ToList());
			Assert.AreEqual("0",entry.ListChanges[0].OldValue);
			Assert.AreEqual("1",entry.ListChanges[0].NewValue);
			Assert.AreEqual("",entry.ListChanges[1].OldValue);
			Assert.IsTrue(entry.ListChanges[1].IsGated);
			Assert.IsFalse(entry.ListChanges[2].IsGated);
			Assert.AreEqual("Invert",entry.GetGatedValue("Device.Polarity"));
			Assert.AreEqual("Example Sensor DS",entry.GetGatedValue("Twain.AllowedSources"));
			Assert.AreEqual(GatedSettingsHash.ListSessionKeys.Count+GatedSettingsHash.ListTwainKeys.Count+GatedSettingsHash.ListDeviceKeys.Count,
				entry.DictGatedValues.Count,"Every gated value, as saved.");
			Assert.AreEqual(CaptureSettingsStatus.Verified,model.SettingsCheck.Status,"The screen shows the file as saved.");
			Assert.IsFalse(model.HasChanges);
			Assert.AreEqual("1",Row(model,"Session","Enabled").SavedText);
			Assert.AreEqual(CaptureSettingsStatus.Verified,CaptureSettingsCheck.Check(LoadFile(),12,"OPERATORY1",_store.GetSetupLogEntries(12)).Status);
		}

		[TestMethod]
		public void Save_WithoutChanges_AcceptsTheFile_NeverSavedBecomesVerified() {
			CaptureSettingsModel model=Model();
			Assert.AreEqual(CaptureSettingsStatus.NeverSaved,model.SettingsCheck.Status);
			string textBefore=File.ReadAllText(_path);
			CaptureSettingsSaveResult result=model.Save();
			Assert.IsTrue(result.IsLogged,result.Error);
			Assert.AreEqual(textBefore,File.ReadAllText(_path),"Nothing is written.");
			Assert.AreEqual(0,result.Entry.ListChanges.Count);
			StringAssert.Contains(result.Entry.Note,"without changes");
			Assert.AreEqual(CaptureSettingsStatus.Verified,model.SettingsCheck.Status);
		}

		[TestMethod]
		public void Save_AfterAHandEdit_ShowsWhatChangedOutside_ThenAcceptsIt() {
			Model().Save();
			Write(Ini.Replace("Allow16Bit=0","Allow16Bit=1"));
			CaptureSettingsModel model=Model();
			Assert.AreEqual(CaptureSettingsStatus.ChangedOutside,model.SettingsCheck.Status);
			StringAssert.StartsWith(model.SettingsCheck.Text,"Capture settings were changed outside Open Dental; review them in Setup.");
			CaptureSettingRow row=Row(model,"Twain","Allow16Bit");
			Assert.IsTrue(row.IsChangedOutside);
			Assert.AreEqual("0",row.SavedText);
			Assert.AreEqual("1",row.Text);
			Assert.IsFalse(Row(model,"Device","Bits").IsChangedOutside);
			Assert.IsTrue(model.Save().IsLogged);
			Assert.AreEqual(CaptureSettingsStatus.Verified,model.SettingsCheck.Status);
			Assert.IsFalse(Row(model,"Twain","Allow16Bit").IsChangedOutside);
		}

		[TestMethod]
		public void Save_EditingAnotherKey_AfterAHandEdit_ListsTheGatedValuesChangedOutside_AsOldAndNew() {
			Model().Save();
			Write(Ini.Replace("Allow16Bit=0","Allow16Bit=1").Replace("Bits=8 ; gated","Bits=16 ; gated"));
			CaptureSettingsModel model=Model();
			Assert.AreEqual(CaptureSettingsStatus.ChangedOutside,model.SettingsCheck.Status);
			List<CaptureSettingChange> listOutside=model.GetChangesOutside();
			CollectionAssert.AreEquivalent(new[] { "Twain.Allow16Bit","Device.12.Bits" },listOutside.Select(x => x.Label).ToArray());
			CaptureSettingChange changeAllow=listOutside.Single(x => x.Key=="Allow16Bit");
			Assert.AreEqual("0",changeAllow.OldValue,"The value last saved.");
			Assert.AreEqual("1",changeAllow.NewValue,"The value in the file.");
			Assert.IsTrue(changeAllow.IsChangedOutside);
			StringAssert.Contains(changeAllow.GetText(),"changed outside Open Dental");
			Assert.IsTrue(model.SetText(Idx(model,"Sounds","Error"),"Beep"),"A key that is not gated.");
			Assert.AreEqual(1,model.GetPendingChanges().Count,"Only the non-gated key was edited.");
			Assert.AreEqual(2,model.GetChangesOutside().Count,"The confirmation lists the values changed outside as well.");
			CaptureSettingsSaveResult result=model.Save();
			Assert.IsTrue(result.IsLogged,result.Error);
			Assert.AreEqual(3,result.Entry.ListChanges.Count);
			Assert.AreEqual(2,result.Entry.ListChanges.Count(x => x.IsChangedOutside));
			StringAssert.Contains(result.Entry.Note,"changed outside Open Dental");
			CaptureSettingsLogEntry entryRead=_store.GetEntries(12).Last();
			CaptureSettingChange changeRead=entryRead.ListChanges.Single(x => x.Label=="Twain.Allow16Bit");
			Assert.IsTrue(changeRead.IsChangedOutside,"Read back from the security log.");
			Assert.AreEqual("0",changeRead.OldValue);
			Assert.AreEqual("1",changeRead.NewValue);
			Assert.IsFalse(entryRead.ListChanges.Single(x => !x.IsGated).IsChangedOutside);
			Assert.AreEqual(CaptureSettingsStatus.Verified,model.SettingsCheck.Status);
			Assert.AreEqual(0,model.GetChangesOutside().Count);
		}

		[TestMethod]
		public void Save_Verified_NothingListedAsChangedOutside() {
			Model().Save();
			CaptureSettingsModel model=Model();
			Assert.AreEqual(CaptureSettingsStatus.Verified,model.SettingsCheck.Status);
			Assert.AreEqual(0,model.GetChangesOutside().Count);
			Assert.AreEqual(0,model.Save().Entry.ListChanges.Count);
		}

		[TestMethod]
		public void Save_WithAProblem_NothingWritten() {
			CaptureSettingsModel model=Model();
			model.SetText(Idx(model,"Device","Bits"),"12");
			string textBefore=File.ReadAllText(_path);
			CaptureSettingsSaveResult result=model.Save();
			Assert.IsFalse(result.IsSaved);
			StringAssert.Contains(result.Error,"Fix these values first");
			Assert.AreEqual(textBefore,File.ReadAllText(_path));
			Assert.AreEqual(0,_store.ListLogs.Count);
		}

		[TestMethod]
		public void Save_WithoutSetup_NothingWritten() {
			CaptureSettingsModel model=Model();
			model.SetText(Idx(model,"Session","Enabled"),"1");
			_store.IsAuthorizedResult=false;
			CaptureSettingsSaveResult result=model.Save();
			Assert.IsFalse(result.IsSaved);
			StringAssert.Contains(result.Error,"Not authorized for Setup");
			Assert.AreEqual("0",LoadFile().GetString("Session","Enabled"));
			Assert.AreEqual(0,_store.ListLogs.Count);
		}

		[TestMethod]
		public void Save_FileChangedSinceLoad_Refused_SoAHandEditIsNeverAcceptedUnseen() {
			CaptureSettingsModel model=Model();
			model.SetText(Idx(model,"Session","Enabled"),"1");
			Write(Ini.Replace("Bits=8 ; gated","Bits=16"));//edited by hand while the screen is open
			CaptureSettingsSaveResult result=model.Save();
			Assert.IsFalse(result.IsSaved);
			Assert.IsTrue(result.IsFileChangedSinceLoad);
			StringAssert.Contains(result.Error,"[Device.12] Bits");
			Assert.AreEqual("0",LoadFile().GetString("Session","Enabled"));
			Assert.AreEqual(0,_store.ListLogs.Count);
			Assert.IsTrue(model.HasChanges,"The edits stay until the screen reads the file again.");
		}

		[TestMethod]
		public void Save_DoesNotReadBack_NoEntry_NotVerified() {
			CaptureSettingsModel model=Model();
			model.SetText(Idx(model,"Session","Enabled"),"1");
			string pathOther=Path.Combine(_tempDir,"other.ini");
			File.WriteAllBytes(pathOther,new UTF8Encoding(false).GetBytes(Ini));
			//The check before the save reads the file; the read back finds another file (as when the save went to the fallback path while the
			//ProgramData file exists and is read).
			_queueLoads.Enqueue(() => LoadFile());
			_queueLoads.Enqueue(() => LocalCaptureConfig.Load(pathOther));
			CaptureSettingsSaveResult result=model.Save();
			Assert.IsFalse(result.IsSaved);
			Assert.IsFalse(result.IsLogged);
			StringAssert.Contains(result.Error,"does not read back as saved");
			StringAssert.Contains(result.Error,"[Session] Enabled");
			Assert.AreEqual(0,_store.ListLogs.Count);
		}

		[TestMethod]
		public void Save_LogEntryFails_SavedButNotVerified() {
			CaptureSettingsModel model=Model();
			model.SetText(Idx(model,"Session","Enabled"),"1");
			_store.ThrowOnMakeLogEntry="The database is not reachable.";
			CaptureSettingsSaveResult result=model.Save();
			Assert.IsTrue(result.IsSaved);
			Assert.IsFalse(result.IsLogged);
			StringAssert.Contains(result.Error,"The database is not reachable.");
			StringAssert.Contains(result.Error,"not verified");
			Assert.AreEqual("1",LoadFile().GetString("Session","Enabled"));
			Assert.IsFalse(model.SettingsCheck.IsVerified);
		}

		[TestMethod]
		public void Save_SharedKeys_OtherVerifiedDevicesGetTheirNewHash() {
			Model(12).Save();
			Model(13).Save();
			//Device 14 was never saved: it stays never saved.
			Assert.AreEqual(2,_store.ListLogs.Count);
			CaptureSettingsModel model=Model(12);
			model.SetText(Idx(model,"Twain","Allow16Bit"),"1");
			CaptureSettingsSaveResult result=model.Save();
			Assert.IsTrue(result.IsLogged,result.Error);
			Assert.AreEqual(13,result.ListEntriesShared.Single().ImagingDeviceNum);
			CaptureSettingsLogEntry entryShared=_store.GetEntries(13).Last();
			Assert.AreEqual(GatedSettingsHash.Compute(LoadFile(),13),entryShared.GatedSettingsHash);
			Assert.AreEqual("Second sensor",entryShared.DeviceDescription);
			StringAssert.Contains(entryShared.Note,"Shared [Session] or [Twain] keys changed in Capture settings for device 12");
			Assert.AreEqual("Twain.Allow16Bit",entryShared.ListChanges.Single().Label);
			Assert.AreEqual("16",entryShared.GetGatedValue("Device.Bits"),"Device 13's own values.");
			Assert.AreEqual(CaptureSettingsStatus.Verified,CaptureSettingsCheck.Check(LoadFile(),13,"OPERATORY1",_store.GetSetupLogEntries(13)).Status);
			Assert.AreEqual(0,_store.GetEntries(14).Count);
			//A change of the device's own section touches no other device.
			model=Model(12);
			model.SetText(Idx(model,"Device","Bits"),"16");
			Assert.AreEqual(0,model.Save().ListEntriesShared.Count);
		}

		[TestMethod]
		public void Save_SharedKeys_AHandEditedDeviceIsNotAccepted() {
			Model(13).Save();
			Write(Ini.Replace("[Device.13]\r\nBits=16","[Device.13]\r\nBits=8"));//device 13 edited by hand
			CaptureSettingsModel model=Model(12);
			model.SetText(Idx(model,"Twain","Allow16Bit"),"1");
			CaptureSettingsSaveResult result=model.Save();
			Assert.IsTrue(result.IsLogged,result.Error);
			Assert.AreEqual(0,result.ListEntriesShared.Count,"Saving device 12 does not verify device 13's hand edit.");
			Assert.AreEqual(CaptureSettingsStatus.ChangedOutside,CaptureSettingsCheck.Check(LoadFile(),13,"OPERATORY1",_store.GetSetupLogEntries(13)).Status);
		}
		#endregion Save
	}
}
