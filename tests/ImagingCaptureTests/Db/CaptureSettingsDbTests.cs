// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImagingCapture.Session;
using ImagingCapture.Settings;
using ImagingDeviceManager.Config;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace ImagingCapture.Tests.Db {
	///<summary>The Capture settings screen's save log on the scratch database (CaptureDb): CaptureSettingsModel with OdCaptureSettingsStore on a
	///temporary capture.ini writes the Setup entry (design 9.3, 10.7), and the hash check the session runs reads it back through
	///OdCaptureSessionStore.</summary>
	[TestClass]
	public class CaptureSettingsDbTests {
		private string _folder;

		[TestInitialize]
		public void Init() {
			CaptureDb.Begin();
			_folder=Path.Combine(Path.GetTempPath(),"capdb_settings_"+Guid.NewGuid().ToString("N"));
		}

		[TestCleanup]
		public void Cleanup() {
			try {
				Directory.Delete(_folder,true);
			}
			catch {
			}
		}

		///<summary>The check the launcher runs for the device on this computer, with the entries read from the database.</summary>
		private CaptureSettingsCheck Check(ImagingDevice imagingDevice) {
			OdCaptureSessionStore store=new OdCaptureSessionStore();
			return CaptureSettingsCheck.Run(CaptureDb.LoadCaptureIni(_folder),imagingDevice.ImagingDeviceNum,CaptureDb.ComputerName,
				x => store.GetSetupLogEntries(x));
		}

		[TestMethod]
		public void Save_WritesTheSetupEntry_OldAndNewValuesAndTheHash_TheSessionsCheckReadsItBack() {
			ImagingDevice imagingDevice=CaptureDb.CreateImagingDevice("settings save");
			CaptureDb.WriteCaptureIni(_folder,"; capture settings of this computer","[Session]","Enabled=0","[Camera]","Name=Not touched");
			Assert.AreEqual(CaptureSettingsStatus.NeverSaved,Check(imagingDevice).Status);
			CaptureSettingsSaveResult result=CaptureDb.SaveSettings(imagingDevice,_folder,"Session.Enabled=1","Device.Headless=1");
			SecurityLog securityLog=CaptureDb.GetLogsByFKey(EnumPermType.Setup,imagingDevice.ImagingDeviceNum,CaptureSettingsLogEntry.LogPrefix).Single();
			Assert.AreEqual(0,securityLog.PatNum);
			Assert.AreEqual(Security.CurUser.UserNum,securityLog.UserNum);
			Assert.IsTrue(CaptureDb.HasLogHash(securityLog.SecurityLogNum));
			StringAssert.StartsWith(securityLog.LogText,CaptureSettingsLogEntry.LogPrefix);
			CaptureSettingsLogEntry entry;
			Assert.IsTrue(CaptureSettingsLogEntry.TryParse(securityLog.LogText,out entry),"The entry reads back from the database as written.");
			Assert.AreEqual(result.Entry.ToLogText(),securityLog.LogText);
			Assert.AreEqual(imagingDevice.ImagingDeviceNum,entry.ImagingDeviceNum);
			Assert.AreEqual(CaptureDb.ComputerName,entry.ComputerName);
			LocalCaptureConfig config=CaptureDb.LoadCaptureIni(_folder);
			Assert.AreEqual(GatedSettingsHash.Compute(config,imagingDevice.ImagingDeviceNum),entry.GatedSettingsHash);
			CollectionAssert.AreEquivalent(new[] { "[Session] Enabled: 0 -> 1","[Device."+imagingDevice.ImagingDeviceNum+"] Headless: (empty) -> 1" },
				entry.ListChanges.Select(x => x.GetText()).ToList());
			Assert.AreEqual("1",entry.GetGatedValue("Session.Enabled"));
			Assert.AreEqual("Not touched",config.GetString("Camera","Name",""),"Other lines stay.");
			CaptureSettingsCheck check=Check(imagingDevice);
			Assert.AreEqual(CaptureSettingsStatus.Verified,check.Status,check.Text);
		}

		[TestMethod]
		public void HandEdit_ChangedOutside_ASaveWithoutChangesAcceptsIt_ListingTheValue() {
			ImagingDevice imagingDevice=CaptureDb.CreateImagingDevice("settings outside");
			CaptureDb.WriteCaptureIni(_folder,"[Session]","Enabled=1","[Twain]","Allow16Bit=0");
			CaptureDb.SaveSettings(imagingDevice,_folder);
			Assert.AreEqual(CaptureSettingsStatus.Verified,Check(imagingDevice).Status,"Saved without changes: the file's values were accepted.");
			//A hand edit of a gated key.
			string path=Path.Combine(_folder,"capture.ini");
			File.WriteAllText(path,File.ReadAllText(path).Replace("Allow16Bit=0","Allow16Bit=1"));
			CaptureSettingsCheck check=Check(imagingDevice);
			Assert.AreEqual(CaptureSettingsStatus.ChangedOutside,check.Status);
			StringAssert.StartsWith(check.Text,CaptureSettingsCheck.TextChangedOutside);
			CollectionAssert.AreEqual(new[] { "Twain.Allow16Bit" },check.ListChangedLabels);
			//A Setup user reviews it and saves without changes: logged with the value it accepts.
			CaptureDb.SaveSettings(imagingDevice,_folder);
			List<SecurityLog> listSecurityLogs=CaptureDb.GetLogsByFKey(EnumPermType.Setup,imagingDevice.ImagingDeviceNum,CaptureSettingsLogEntry.LogPrefix);
			Assert.AreEqual(2,listSecurityLogs.Count);
			CaptureSettingsLogEntry entry;
			Assert.IsTrue(CaptureSettingsLogEntry.TryParse(listSecurityLogs[1].LogText,out entry));
			CaptureSettingChange change=entry.ListChanges.Single();
			Assert.IsTrue(change.IsChangedOutside);
			Assert.AreEqual("[Twain] Allow16Bit: 0 -> 1 "+CaptureSettingChange.TextChangedOutside,change.GetText());
			StringAssert.Contains(entry.Note,"without changes");
			Assert.AreEqual(CaptureSettingsStatus.Verified,Check(imagingDevice).Status,
				"The latest entry wins, also when both saves have the same LogDateTime second (SecurityLogNum breaks the tie).");
		}

		[TestMethod]
		public void SharedKeyChanged_TheOtherVerifiedDeviceGetsAnEntryWithItsNewHash_StaysVerified() {
			ImagingDevice imagingDeviceA=CaptureDb.CreateImagingDevice("settings shared A");
			ImagingDevice imagingDeviceB=CaptureDb.CreateImagingDevice("settings shared B");
			CaptureDb.WriteCaptureIni(_folder,"[Session]","Enabled=1","[Twain]","RestrictSources=0");
			CaptureDb.SaveSettings(imagingDeviceA,_folder);
			CaptureDb.SaveSettings(imagingDeviceB,_folder);
			Assert.AreEqual(CaptureSettingsStatus.Verified,Check(imagingDeviceB).Status);
			//[Twain] is in every device's hash: device A's screen changes it.
			CaptureSettingsSaveResult result=CaptureDb.SaveSettings(imagingDeviceA,_folder,"Twain.RestrictSources=1","Twain.AllowedSources="+CaptureDb.TwainName);
			CaptureSettingsLogEntry entryB=result.ListEntriesShared.Single(x => x.ImagingDeviceNum==imagingDeviceB.ImagingDeviceNum);
			StringAssert.Contains(entryB.Note,"Shared [Session] or [Twain] keys changed");
			SecurityLog securityLogB=CaptureDb.GetLogsByFKey(EnumPermType.Setup,imagingDeviceB.ImagingDeviceNum,CaptureSettingsLogEntry.LogPrefix).Last();
			Assert.AreEqual(entryB.ToLogText(),securityLogB.LogText,"Written with device B's number as its FKey.");
			Assert.AreEqual(CaptureSettingsStatus.Verified,Check(imagingDeviceA).Status);
			Assert.AreEqual(CaptureSettingsStatus.Verified,Check(imagingDeviceB).Status,"The change made here is no change outside Open PMS - Dental for B.");
		}

		[TestMethod]
		public void Save_WithoutSetup_NothingSavedOrLogged() {
			ImagingDevice imagingDevice=CaptureDb.CreateImagingDevice("settings no setup");
			CaptureDb.WriteCaptureIni(_folder,"[Session]","Enabled=0");
			CaptureDb.LogInWith(EnumPermType.ImageCreate);
			CaptureSettingsModel model=CaptureDb.SettingsModel(imagingDevice,_folder);
			Assert.IsTrue(model.SetText(model.IndexOf("Session","Enabled"),"1"));
			CaptureSettingsSaveResult result=model.Save();
			Assert.IsFalse(result.IsSaved);
			Assert.IsFalse(result.IsLogged);
			StringAssert.Contains(result.Error,"Not authorized for Setup");
			Assert.AreEqual(0,CaptureDb.GetLogsByFKey(EnumPermType.Setup,imagingDevice.ImagingDeviceNum,CaptureSettingsLogEntry.LogPrefix).Count);
			Assert.AreEqual("0",CaptureDb.LoadCaptureIni(_folder).GetString("Session","Enabled",""),"The file is unchanged.");
		}

		[TestMethod]
		public void SettingsStore_TheImagingDeviceRowsAndTheSetupEntries() {
			OdCaptureSettingsStore store=new OdCaptureSettingsStore();
			ImagingDevice imagingDevice=CaptureDb.CreateImagingDevice("settings store");
			Assert.IsTrue(store.GetImagingDevices().Any(x => x.ImagingDeviceNum==imagingDevice.ImagingDeviceNum && x.TwainName==CaptureDb.TwainName));
			Assert.IsTrue(store.IsAuthorized(EnumPermType.Setup));
			store.MakeLogEntry(EnumPermType.Setup,0,"Capdb settings store entry",imagingDevice.ImagingDeviceNum);
			Assert.AreEqual("Capdb settings store entry",store.GetSetupLogEntries(imagingDevice.ImagingDeviceNum).Single().LogText);
			CaptureDb.LogInWith(EnumPermType.ImageCreate);
			Assert.IsFalse(store.IsAuthorized(EnumPermType.Setup));
		}
	}
}
