// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using ImagingCapture.Settings;
using ImagingDeviceManager.Config;
using OpenPmsDentalBusiness;

namespace ImagingCapture.Tests {
	///<summary>Security-log entries of the Capture settings screen (CaptureSettingsLogEntry) for the tests: a save of exactly the given file's
	///gated settings, and the verified hash check (design 9.3) that follows from one.</summary>
	internal static class CaptureSettingsTestKit {
		///<summary>A Setup entry with FKey=imagingDeviceNum that saved config's gated settings for the device on computerName.</summary>
		public static SecurityLog SavedLog(LocalCaptureConfig config,long imagingDeviceNum,string computerName,long securityLogNum=900,DateTime? dateTime=null) {
			return SavedLog(GatedSettingsHash.Compute(config,imagingDeviceNum),imagingDeviceNum,computerName,securityLogNum,dateTime,config);
		}

		///<summary>A Setup entry with FKey=imagingDeviceNum that saved gated settings with this hash. config, when given, fills the gated values.</summary>
		public static SecurityLog SavedLog(string hash,long imagingDeviceNum,string computerName,long securityLogNum=900,DateTime? dateTime=null,
			LocalCaptureConfig config=null)
		{
			CaptureSettingsLogEntry entry=new CaptureSettingsLogEntry {
				ImagingDeviceNum=imagingDeviceNum,
				DeviceDescription="Intraoral sensor",
				ComputerName=computerName,
				FilePath=@"C:\ProgramData\OpenPMS-Dental\Imaging\capture.ini",
				GatedSettingsHash=hash,
				DateTimeSaved=dateTime??new DateTime(2026,10,29,8,0,0),
				Note="Saved in Capture settings.",
			};
			foreach(CaptureSettingDef settingDef in CaptureSettingsCatalog.ListSettings) {
				if(settingDef.IsGated) {
					entry.DictGatedValues[settingDef.Label]=config==null ? "" : config.GetString(settingDef.GetSectionName(imagingDeviceNum),settingDef.Key,"");
				}
			}
			return new SecurityLog { SecurityLogNum=securityLogNum,FKey=imagingDeviceNum,PermType=EnumPermType.Setup,PatNum=0,
				LogDateTime=dateTime??new DateTime(2026,10,29,8,0,0),LogText=entry.ToLogText() };
		}

		///<summary>The check of config for the device on computerName after a save of exactly that file: Verified. Null when the device number
		///cannot be hashed (not positive).</summary>
		public static CaptureSettingsCheck Verified(LocalCaptureConfig config,long imagingDeviceNum,string computerName) {
			if(imagingDeviceNum<=0) {
				return null;
			}
			return CaptureSettingsCheck.Check(config,imagingDeviceNum,computerName,new List<SecurityLog> { SavedLog(config,imagingDeviceNum,computerName) });
		}

		///<summary>A Verified check of device 12 on OPERATORY1 for a file whose gated settings have this hash.</summary>
		public static CaptureSettingsCheck VerifiedHash(string hash) {
			return CaptureSettingsCheck.CheckHash(hash,12,"OPERATORY1",new List<SecurityLog> { SavedLog(hash,12,"OPERATORY1") });
		}
	}
}
