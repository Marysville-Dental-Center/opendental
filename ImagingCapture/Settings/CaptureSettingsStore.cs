// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;
using ImagingDeviceManager.Config;
using OpenPmsDentalBusiness;

namespace ImagingCapture.Settings {
	///<summary>The Capture settings screen's database seam: the Setup permission, the Setup security-log entries of a device (where the screen and
	///Test capture write theirs, FKey=ImagingDeviceNum), and the imaging device rows.</summary>
	public interface ICaptureSettingsStore {
		///<summary>Security.IsAuthorized(permType,suppressMessage:true): the caller shows the refusal itself.</summary>
		bool IsAuthorized(EnumPermType permType);
		///<summary>The Setup security-log entries with FKey=imagingDeviceNum.</summary>
		List<SecurityLog> GetSetupLogEntries(long imagingDeviceNum);
		///<summary>SecurityLogs.MakeLogEntry(permType,patNum,logText,fKey,DateTime.MinValue). Throws on failure.</summary>
		void MakeLogEntry(EnumPermType permType,long patNum,string logText,long fKey);
		///<summary>Every imaging device row (ImagingDevices.GetDeepCopy): a save that changes the shared [Session] or [Twain] keys logs the new hash
		///of each other device whose settings were verified.</summary>
		List<ImagingDevice> GetImagingDevices();
	}

	///<summary>ICaptureSettingsStore through OpenDentBusiness. Not exercised by the unit tests, which have no database.</summary>
	public sealed class OdCaptureSettingsStore:ICaptureSettingsStore {
		public bool IsAuthorized(EnumPermType permType) {
			return Security.IsAuthorized(permType,suppressMessage:true);
		}

		public List<SecurityLog> GetSetupLogEntries(long imagingDeviceNum) {
			return SecurityLogs.Refresh(0,new List<EnumPermType> { EnumPermType.Setup },imagingDeviceNum).ToList();
		}

		public void MakeLogEntry(EnumPermType permType,long patNum,string logText,long fKey) {
			SecurityLogs.MakeLogEntry(permType,patNum,logText,fKey,DateTime.MinValue);
		}

		public List<ImagingDevice> GetImagingDevices() {
			return ImagingDevices.GetDeepCopy();
		}
	}

	///<summary>The Capture settings screen's connections to the rest of the program, so that it can be tested with stand-ins.</summary>
	public sealed class CaptureSettingsServices {
		public ICaptureSettingsStore Store { get; set; }
		///<summary>capture.ini, read fresh from disk on every call (LocalCaptureConfig.Load).</summary>
		public Func<LocalCaptureConfig> FuncLoadConfig { get; set; }
		///<summary>This computer (ODEnvironment.MachineName).</summary>
		public Func<string> FuncGetComputerName { get; set; }
		public Func<DateTime> FuncNow { get; set; }

		public static CaptureSettingsServices CreateDefault() {
			return new CaptureSettingsServices {
				Store=new OdCaptureSettingsStore(),
				FuncLoadConfig=() => LocalCaptureConfig.Load(),
				FuncGetComputerName=() => ODEnvironment.MachineName,
				FuncNow=() => DateTime.Now,
			};
		}
	}
}
