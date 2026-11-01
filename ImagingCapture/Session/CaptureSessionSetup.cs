// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using ImagingCapture.Save;
using ImagingCapture.Settings;
using ImagingDeviceManager.Capture;
using ImagingDeviceManager.Config;
using OpenPmsDentalBusiness;

namespace ImagingCapture.Session {
	///<summary>Everything one capture session is bound to, resolved by CaptureSessionLauncher before the window opens (design 6.4): the patient
	///and the main window's current patient, the target mount and its slots or none, the device, the category, the default provider, and the
	///capture.ini values in effect on this computer.</summary>
	public sealed class CaptureSessionSetup {
		///<summary>The bound patient. Every save uses this patient (design 10.6), never ControlImages' current patient.</summary>
		public Patient Patient { get; set; }
		///<summary>Returns FormOpenDental.PatNumCur; checked before every enable (design 6.4, 10.6).</summary>
		public Func<long> FuncGetPatNumCur { get; set; }
		///<summary>The target mount, or null for single-target mode.</summary>
		public Mount Mount { get; set; }
		///<summary>The mount definition the mount was created from in this call, or null (an existing mount, or no mount).</summary>
		public MountDef MountDef { get; set; }
		///<summary>The mount's items and documents, with the target; null without a mount.</summary>
		public MountSlots MountSlots { get; set; }
		public ImagingDevice ImagingDevice { get; set; }
		///<summary>The image category (Def of DefCat.ImageCats), already checked.</summary>
		public long CategoryDefNum { get; set; }
		///<summary>RadiographProvider.GetDefaultProvNum (design 10.8); editable before Start.</summary>
		public long ProvNumDefault { get; set; }
		///<summary>capture.ini as loaded when the session started.</summary>
		public LocalCaptureConfig Config { get; set; }
		///<summary>The [Device.N] and [Twain] values in effect.</summary>
		public TwainCaptureOptions Options { get; set; }=new TwainCaptureOptions();
		public CaptureSessionConfig SessionConfig { get; set; }=CaptureSessionConfig.Read(null);
		///<summary>This computer (ODEnvironment.MachineName).</summary>
		public string ComputerName { get; set; }="";
		///<summary>The latest Test capture verification record for the device row, made on any computer (owner decision 2026-11-01), or null. The
		///bit-depth decision matches it against the driver version the source reports at open and the image settings.</summary>
		public CaptureVerificationRecord VerificationRecord { get; set; }
		///<summary>GatedSettingsHash.Compute(Config, ImagingDeviceNum), or "" when it could not be computed (no 16 bits then).</summary>
		public string GatedSettingsHash { get; set; }="";
		///<summary>The hash check of design 9.3 for the device on this computer. The launcher starts the session only when it is Verified; the
		///16-bit decision checks it again. Null means not checked (no 16 bits).</summary>
		public CaptureSettingsCheck SettingsCheck { get; set; }
		///<summary>The quarantine line when the session started (CaptureQuarantine.GetUnassignedText), or null.</summary>
		public string UnassignedText { get; set; }
		///<summary>UnassignedText is the warning after 30 days (CaptureUnassignedInfo.IsOverdue).</summary>
		public bool IsUnassignedOverdue { get; set; }
		///<summary>The session's GUID: the quarantine folder, the sidecars, the DICOM Series and the security log.</summary>
		public Guid SessionGuid { get; set; }=Guid.NewGuid();

		public long PatNum => Patient?.PatNum??0;

		///<summary>"Pat Test" (Patient.GetNameFL), or "".</summary>
		public string PatientName => Patient==null ? "" : Patient.GetNameFL();
	}
}
