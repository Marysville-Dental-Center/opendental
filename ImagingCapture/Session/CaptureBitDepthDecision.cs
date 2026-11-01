// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using ImagingCapture.Dicom;
using ImagingCapture.Settings;
using ImagingDeviceManager.Capture;
using OpenDentBusiness;

namespace ImagingCapture.Session {
	///<summary>What the 8-or-16-bit decision for one enable looks at.</summary>
	public sealed class CaptureBitDepthInput {
		///<summary>The [Device.N] and [Twain] settings in effect (Bits, Allow16Bit, Polarity).</summary>
		public TwainCaptureOptions Options { get; set; }
		///<summary>The latest Test capture verification record for this device on this computer (CaptureVerificationRecords.FindLatest), or null.</summary>
		public CaptureVerificationRecord Record { get; set; }
		public long ImagingDeviceNum { get; set; }
		///<summary>This computer (ODEnvironment.MachineName).</summary>
		public string ComputerName { get; set; }
		///<summary>The device row's TWAIN source (ImagingDevice.TwainName).</summary>
		public string TwainName { get; set; }
		///<summary>GatedSettingsHash.Compute of the capture.ini the session runs with.</summary>
		public string GatedSettingsHash { get; set; }
		///<summary>Whether those gated settings are the ones last saved in the Capture settings screen for this device on this computer (design
		///9.3). Null means not checked, which counts as not verified.</summary>
		public CaptureSettingsCheck SettingsCheck { get; set; }
		///<summary>The enable's target, as the DICOM writer sees it (RadiographTarget.ToIntraOralTarget).</summary>
		public IntraOralTarget Target { get; set; }
	}

	///<summary>What one enable asks for.</summary>
	public sealed class CaptureBitDepthChoice {
		///<summary>8 or 16.</summary>
		public int Bits { get; set; }=8;
		///<summary>Why 8 bits, or null for 16. One sentence, for the capture log (logged once per reason by the controller).</summary>
		public string Reason8Bit { get; set; }
		///<summary>Bits is 16 and [Device.N] Polarity=Invert: the matching, passed verification record covers the inversion (design 9.3: the
		///session refuses an inverted capture without one). Goes to RadiographSaveRequest.IsInversionVerified.</summary>
		public bool IsInversionVerified { get; set; }
	}

	///<summary>The capture session's decision, per enable, whether to ask the source for 16 bits (design 7.8 "When 16-bit is enabled", 7.10,
	///9.3): only when [Device.N] Bits=16, [Twain] Allow16Bit=1, the gated settings are the ones last saved in the Capture settings screen for this
	///device on this computer (CaptureSettingsCheck: not changed outside Open Dental, and saved there at least once), the latest Test capture
	///verification record for this device and this computer matches the device, the computer, the device row's TWAIN source and the current
	///gated settings hash and passed for 16 bits, and the enable's target can be written as DICOM (a mount slot with tooth numbers of one view).
	///Otherwise 8 bits, with the first condition that failed as the reason. The library then does what the request asks; a source that refuses
	///16 still gives 8 (TwainCaptureEnded.Reason8Bit).
	///
	///The checks run in a fixed order, each in its own method, so that another condition can be added in one place.</summary>
	public static class CaptureBitDepthDecision {
		public static CaptureBitDepthChoice Decide(CaptureBitDepthInput input) {
			string reason=Get8BitReason(input);
			if(reason!=null) {
				return new CaptureBitDepthChoice { Bits=8,Reason8Bit=reason };
			}
			return new CaptureBitDepthChoice { Bits=16,IsInversionVerified=input.Options.Polarity==TwainPolarity.Invert };
		}

		///<summary>The first condition for 16 bits that does not hold, as a sentence, or null when they all hold.</summary>
		private static string Get8BitReason(CaptureBitDepthInput input) {
			if(input==null || input.Options==null) {
				return "8 bits: the device settings are not known.";
			}
			if(input.Options.Bits!=16) {
				return "8 bits: [Device."+input.ImagingDeviceNum+"] Bits is not 16.";
			}
			if(!input.Options.Allow16Bit) {
				return "8 bits: [Twain] Allow16Bit is 0 (the 16-bit switch is off on this computer).";
			}
			string reason=GetSettingsReason(input);
			if(reason!=null) {
				return reason;
			}
			reason=GetRecordReason(input);
			if(reason!=null) {
				return reason;
			}
			return GetTargetReason(input.Target);
		}

		///<summary>Design 9.3: no 16 bits from gated settings that were changed outside Open Dental, or never saved in the Capture settings screen
		///(not verified), or whose check was made for another file than the one in effect.</summary>
		private static string GetSettingsReason(CaptureBitDepthInput input) {
			CaptureSettingsCheck check=input.SettingsCheck;
			if(check==null) {
				return "8 bits: the capture settings were not checked against the ones saved in Capture settings (not verified).";
			}
			if(!check.IsVerified) {
				return check.Reason8Bit;
			}
			if(!string.Equals(check.CurrentHash,(input.GatedSettingsHash??"").Trim(),StringComparison.OrdinalIgnoreCase)) {
				return "8 bits: the capture settings checked are not the ones in effect (not verified).";
			}
			return null;
		}

		///<summary>Design 7.8 condition 2 and 9.3: a verification record that matches the current settings and passed.</summary>
		private static string GetRecordReason(CaptureBitDepthInput input) {
			CaptureVerificationRecord record=input.Record;
			if(record==null) {
				return "8 bits: no Test capture verification record exists for this device on this computer.";
			}
			if(!record.Matches(input.ImagingDeviceNum,input.ComputerName,input.TwainName,input.GatedSettingsHash)) {
				return "8 bits: the latest Test capture verification record ("+record.DateTimeVerified.ToString("yyyy-MM-dd HH:mm")+") does not "
					+"match the current settings: the device's TWAIN source or its gated capture settings changed since. Run Test capture again.";
			}
			if(!record.IsPassedFor16Bit) {
				return "8 bits: the latest Test capture verification record ("+record.DateTimeVerified.ToString("yyyy-MM-dd HH:mm")+") did not "
					+"pass for 16 bits (a 16-bit image over memory transfer, byte order and polarity confirmed).";
			}
			return null;
		}

		///<summary>Design 7.8 "Anatomy" and 7.10: a target with no mount slot, or whose slot has no tooth numbers of one view, is captured at 8
		///bits and never written as DICOM.</summary>
		private static string GetTargetReason(IntraOralTarget target) {
			if(target==null) {
				return "8 bits: the target is not known.";
			}
			string reason;
			if(!DicomIntraOralWriter.CanWrite(target,out reason)) {
				return "8 bits: the target cannot be stored as DICOM: "+(reason??"").Trim().TrimEnd('.')+".";
			}
			return null;
		}
	}

	///<summary>Finds the Test capture verification record the capture session uses (design 9.3): among the Setup security-log entries with
	///FKey=ImagingDeviceNum whose text is a record, the latest one for this device on this computer. A newer record that does not match the
	///current settings wins over an older one that does: the latest test is what counts.</summary>
	public static class CaptureVerificationRecords {
		///<summary>The latest record for imagingDeviceNum and computerName (compared trimmed and ignoring case), by LogDateTime and then
		///SecurityLogNum, or null.</summary>
		public static CaptureVerificationRecord FindLatest(IEnumerable<SecurityLog> listSecurityLogs,long imagingDeviceNum,string computerName) {
			string computer=(computerName??"").Trim();
			CaptureVerificationRecord recordLatest=null;
			SecurityLog securityLogLatest=null;
			foreach(SecurityLog securityLog in (listSecurityLogs??Enumerable.Empty<SecurityLog>()).Where(x => x!=null)) {
				CaptureVerificationRecord record;
				if(!CaptureVerificationRecord.TryParse(securityLog.LogText,out record)) {
					continue;
				}
				if(record.ImagingDeviceNum!=imagingDeviceNum
					|| !string.Equals((record.ComputerName??"").Trim(),computer,StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				if(securityLogLatest!=null && (securityLog.LogDateTime<securityLogLatest.LogDateTime
					|| (securityLog.LogDateTime==securityLogLatest.LogDateTime && securityLog.SecurityLogNum<securityLogLatest.SecurityLogNum)))
				{
					continue;
				}
				securityLogLatest=securityLog;
				recordLatest=record;
			}
			return recordLatest;
		}
	}
}
