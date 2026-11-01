// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ImagingDeviceManager.Capture;
using ImagingDeviceManager.Config;
using OpenDentBusiness;

namespace ImagingCapture.Settings {
	///<summary>Whether the gated capture settings of a device on this computer are the ones last saved in the Capture settings screen.</summary>
	public enum CaptureSettingsStatus {
		///<summary>The file's GatedSettingsHash for the device equals the one the screen last logged for it on this computer.</summary>
		Verified,
		///<summary>The hash differs: a hand edit, another program, or a missing file while a saved configuration exists (design 9.3).</summary>
		ChangedOutside,
		///<summary>The screen never saved this device's settings on this computer.</summary>
		NeverSaved,
		///<summary>The security log or the file could not be read.</summary>
		Unknown,
	}

	///<summary>The hash check of design 9.3: capture.ini is user-writable, so its gated settings change only through the Capture settings screen,
	///which logs the new GatedSettingsHash (CaptureSettingsLogEntry); at start, the capture session and Test capture compare the file's hash for
	///the device with the latest one logged for the device on this computer. LocalCaptureConfig has no database access, so the check is here.
	///
	///Unless the settings are Verified, the session path ignores the gated block and uses safe values (GetOptionsInEffect): the capture session
	///stays off (CaptureSessionLauncher returns NotStarted and the legacy Mount/Acquire loop runs), Test capture runs with the safe values and
	///writes no verification record, and the 16-bit decision gives 8 bits (CaptureBitDepthDecision). The legacy path is not checked: the
	///library reads [Twain] as it is (design 9.3 "Before CAP-4, and on the legacy path").</summary>
	public sealed class CaptureSettingsCheck {
		///<summary>Design 9.3's sentence for a hash that differs from the one last saved.</summary>
		public const string TextChangedOutside="Capture settings were changed outside Open Dental; review them in Setup.";
		public const string TextNeverSaved="The capture settings of this device have never been saved in Open Dental on this computer; review and "
			+"save them in Setup (Imaging Devices, the device, Capture settings).";
		public const string TextConsequences="Until then the capture session stays off on this computer, Test capture uses safe values, and 16-bit "
			+"capture is not verified.";

		private CaptureSettingsCheck() {
		}

		public CaptureSettingsStatus Status { get; private set; }
		public long ImagingDeviceNum { get; private set; }
		public string ComputerName { get; private set; }="";
		///<summary>GatedSettingsHash.Compute of the file as checked, or "" when it could not be computed.</summary>
		public string CurrentHash { get; private set; }="";
		///<summary>The latest entry the screen logged for the device on this computer, or null (NeverSaved, or Unknown).</summary>
		public CaptureSettingsLogEntry LastSaved { get; private set; }
		///<summary>Unknown: why.</summary>
		public string ErrorText { get; private set; }="";
		///<summary>ChangedOutside: the gated keys whose value differs from the one last saved, by their GatedSettingsHash names
		///("Twain.Allow16Bit"), when the file was given to the check. Empty otherwise.</summary>
		public List<string> ListChangedLabels { get; private set; }=new List<string>();

		public bool IsVerified => Status==CaptureSettingsStatus.Verified;

		///<summary>What the screens show, or "" when Verified. Starts with design 9.3's sentence for ChangedOutside.</summary>
		public string Text {
			get {
				switch(Status) {
					case CaptureSettingsStatus.Verified:
						return "";
					case CaptureSettingsStatus.ChangedOutside:
						string details="The gated settings of this device differ from the ones saved in Capture settings on "
							+LastSaved.DateTimeSaved.ToString("yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture)
							+(ListChangedLabels.Count==0 ? "" : " ("+string.Join(", ",ListChangedLabels)+")")+".";
						return TextChangedOutside+" "+details+" "+TextConsequences;
					case CaptureSettingsStatus.NeverSaved:
						return TextNeverSaved+" "+TextConsequences;
					default:
						return "The capture settings saved in Open Dental could not be checked: "+(ErrorText??"").Trim().TrimEnd('.')+". They count as "
							+"not verified. "+TextConsequences;
				}
			}
		}

		///<summary>The reason the 16-bit decision gives when the settings are not Verified, or null when they are.</summary>
		public string Reason8Bit {
			get {
				switch(Status) {
					case CaptureSettingsStatus.Verified:
						return null;
					case CaptureSettingsStatus.ChangedOutside:
						return "8 bits: capture settings were changed outside Open Dental since they were last saved in Capture settings (not verified).";
					case CaptureSettingsStatus.NeverSaved:
						return "8 bits: the capture settings of this device have never been saved in Open Dental on this computer (not verified).";
					default:
						return "8 bits: the capture settings saved in Open Dental could not be checked (not verified).";
				}
			}
		}

		///<summary>Checks config for imagingDeviceNum against listSetupLogs: the Setup security-log entries with FKey=ImagingDeviceNum (where the
		///screen writes its entries; others are skipped). Never throws: a hash that cannot be computed gives Unknown.</summary>
		public static CaptureSettingsCheck Check(LocalCaptureConfig config,long imagingDeviceNum,string computerName,IEnumerable<SecurityLog> listSetupLogs) {
			string hash;
			try {
				hash=GatedSettingsHash.Compute(config,imagingDeviceNum);
			}
			catch(Exception ex) {
				return Unknown(imagingDeviceNum,computerName,"the gated settings could not be hashed: "+ex.Message);
			}
			CaptureSettingsCheck check=CheckHash(hash,imagingDeviceNum,computerName,listSetupLogs);
			if(check.Status==CaptureSettingsStatus.ChangedOutside) {
				check.ListChangedLabels=GetChangedLabels(check.LastSaved,config,imagingDeviceNum);
			}
			return check;
		}

		///<summary>Check, reading the entries with funcGetSetupLogs (ICaptureSessionStore.GetSetupLogEntries and the like); a failure gives Unknown
		///with its message.</summary>
		public static CaptureSettingsCheck Run(LocalCaptureConfig config,long imagingDeviceNum,string computerName,
			Func<long,IEnumerable<SecurityLog>> funcGetSetupLogs)
		{
			IEnumerable<SecurityLog> listSetupLogs;
			try {
				listSetupLogs=funcGetSetupLogs==null ? null : funcGetSetupLogs(imagingDeviceNum)?.ToList();
			}
			catch(Exception ex) {
				return Unknown(imagingDeviceNum,computerName,"the security log could not be read: "+ex.Message);
			}
			if(listSetupLogs==null) {
				return Unknown(imagingDeviceNum,computerName,"the security log could not be read");
			}
			return Check(config,imagingDeviceNum,computerName,listSetupLogs);
		}

		///<summary>The check of a hash already computed (currentHash: GatedSettingsHash.Compute of the file) against listSetupLogs.</summary>
		public static CaptureSettingsCheck CheckHash(string currentHash,long imagingDeviceNum,string computerName,IEnumerable<SecurityLog> listSetupLogs) {
			CaptureSettingsCheck check=new CaptureSettingsCheck {
				ImagingDeviceNum=imagingDeviceNum,
				ComputerName=(computerName??"").Trim(),
				CurrentHash=(currentHash??"").Trim().ToLowerInvariant(),
			};
			if(check.CurrentHash.Length!=64) {
				check.Status=CaptureSettingsStatus.Unknown;
				check.ErrorText="the gated settings could not be hashed";
				return check;
			}
			check.LastSaved=CaptureSettingsLogEntry.FindLatest(listSetupLogs,imagingDeviceNum,computerName);
			if(check.LastSaved==null) {
				check.Status=CaptureSettingsStatus.NeverSaved;
			}
			else if(string.Equals(check.LastSaved.GatedSettingsHash,check.CurrentHash,StringComparison.OrdinalIgnoreCase)) {
				check.Status=CaptureSettingsStatus.Verified;
			}
			else {
				check.Status=CaptureSettingsStatus.ChangedOutside;
			}
			return check;
		}

		public static CaptureSettingsCheck Unknown(long imagingDeviceNum,string computerName,string errorText) {
			return new CaptureSettingsCheck {
				Status=CaptureSettingsStatus.Unknown,
				ImagingDeviceNum=imagingDeviceNum,
				ComputerName=(computerName??"").Trim(),
				ErrorText=errorText??"",
			};
		}

		///<summary>The gated keys whose value in config differs from the one saved in entry, compared the way GatedSettingsHash compares them.</summary>
		public static List<string> GetChangedLabels(CaptureSettingsLogEntry entry,LocalCaptureConfig config,long imagingDeviceNum) {
			List<string> listLabels=new List<string>();
			if(entry==null) {
				return listLabels;
			}
			foreach(CaptureSettingDef settingDef in CaptureSettingsCatalog.ListSettings.Where(x => x.IsGated)) {
				string valueSaved=entry.GetGatedValue(settingDef.Label)??"";
				string valueNow=config==null ? "" : config.GetString(settingDef.GetSectionName(imagingDeviceNum),settingDef.Key,"");
				if(GatedSettingsHash.NormalizeValue(settingDef.Key,valueSaved)!=GatedSettingsHash.NormalizeValue(settingDef.Key,valueNow)) {
					listLabels.Add(settingDef.Label);
				}
			}
			return listLabels;
		}

		///<summary>The settings the session path uses: optionsFile itself when Verified; otherwise design 9.3's safe values: PerSlot, 8 bits,
		///AsDelivered, Headless off, DisableIsSafe off, no session timeout, no custom capabilities or refusal codes, the 16-bit switch off and the
		///default JournalKeep. RestrictSources and AllowedSources are the ones last saved when the settings changed outside Open Dental, and the
		///file's own when they were never saved (or the entry cannot be read). ListWarnings says that safe values are in effect. ([Session] is
		///off too: CaptureSessionLauncher does not start the session.)</summary>
		public TwainCaptureOptions GetOptionsInEffect(TwainCaptureOptions optionsFile) {
			TwainCaptureOptions optionsFrom=optionsFile??new TwainCaptureOptions { ImagingDeviceNum=ImagingDeviceNum };
			if(IsVerified) {
				return optionsFrom;
			}
			TwainCaptureOptions options=optionsFrom.Clone();
			options.SessionMode=TwainSessionMode.PerSlot;
			options.Bits=8;
			options.ByteOrder16=TwainByteOrder.LE;
			options.Polarity=TwainPolarity.AsDelivered;
			options.Headless=false;
			options.DisableIsSafe=false;
			options.SessionTimeout=TimeSpan.Zero;
			options.SafeDisableCap=null;
			options.RefusedDisableCodes=new List<ushort>();
			options.ContinuousCap=null;
			options.RedeliverCap=null;
			options.NoActivateCap=null;
			options.Allow16Bit=false;
			options.JournalKeep=new TwainCaptureOptions().JournalKeep;
			if(Status==CaptureSettingsStatus.ChangedOutside && LastSaved!=null && LastSaved.GetGatedValue("Twain.RestrictSources")!=null) {
				//Read the saved values with the same parser as the file's.
				LocalCaptureConfig configSaved=LocalCaptureConfig.Load((string)null);
				configSaved.Set(LocalCaptureConfig.SectionTwain,"RestrictSources",LastSaved.GetGatedValue("Twain.RestrictSources"));
				configSaved.Set(LocalCaptureConfig.SectionTwain,"AllowedSources",LastSaved.GetGatedValue("Twain.AllowedSources")??"");
				options.RestrictSources=configSaved.RestrictSources;
				options.AllowedSources=configSaved.AllowedSources;
			}
			options.ListWarnings.Add("Safe values are in effect: the gated capture settings are not verified ("+Status+").");
			return options;
		}
	}
}
