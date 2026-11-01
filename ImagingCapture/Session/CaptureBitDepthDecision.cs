// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ImagingCapture.Dicom;
using ImagingCapture.Settings;
using ImagingDeviceManager.Capture;
using OpenDentBusiness;

namespace ImagingCapture.Session {
	///<summary>What the bit-depth decision for one enable looks at.</summary>
	public sealed class CaptureBitDepthInput {
		///<summary>The [Device.N] and [Twain] settings in effect (Bits, Allow16Bit, ByteOrder16, Polarity).</summary>
		public TwainCaptureOptions Options { get; set; }
		///<summary>The latest Test capture verification record for this device row, on any computer (CaptureVerificationRecords.FindLatest), or
		///null.</summary>
		public CaptureVerificationRecord Record { get; set; }
		public long ImagingDeviceNum { get; set; }
		///<summary>This computer (ODEnvironment.MachineName), for the log. A record is not tied to a computer.</summary>
		public string ComputerName { get; set; }
		///<summary>The device row's TWAIN source (ImagingDevice.TwainName).</summary>
		public string TwainName { get; set; }
		///<summary>The driver build the open source reported (TwainCaptureSourceInfo.DriverIdentity), read at every open; the recovery filer
		///passes the one the capture's sidecar recorded. Null: not known, which no record matches.</summary>
		public TwainDriverIdentity DriverIdentity { get; set; }
		///<summary>GatedSettingsHash.Compute of the capture.ini the session runs with, for the settings check (design 9.3).</summary>
		public string GatedSettingsHash { get; set; }
		///<summary>Whether those gated settings are the ones last saved in the Capture settings screen for this device on this computer (design
		///9.3). Null means not checked, which counts as not verified.</summary>
		public CaptureSettingsCheck SettingsCheck { get; set; }
		///<summary>The enable's target, as the DICOM writer sees it (RadiographTarget.ToIntraOralTarget).</summary>
		public IntraOralTarget Target { get; set; }
		///<summary>16 or 14 to decide for that depth instead of [Device.N] Bits (the recovery filer: the depth a capture was taken at); null for
		///the device's Bits.</summary>
		public int? BitsWanted { get; set; }
		///<summary>The session saw a 14- or 16-bit image it could not read ("14-bit layout not usable: ...", TwainCaptureEnded.LayoutUnusableText):
		///8 bits for the rest of the session on that device. Null when it did not.</summary>
		public string ReasonLayoutUnusable { get; set; }
	}

	///<summary>What one enable asks for.</summary>
	public sealed class CaptureBitDepthChoice {
		///<summary>16, 14 or 8.</summary>
		public int Bits { get; set; }=TwainBitDepth.Bits8;
		///<summary>Why 8 bits, as one sentence for the capture log (logged once per reason by the controller), or null when Bits is above 8:
		///"8 bits: [Device.12] Bits is 8." for a configuration, "8 bits (fallback): ..." when a gate failed.</summary>
		public string Reason8Bit { get; set; }
		///<summary>8 bits because a gate failed (not because the device or the computer is set to 8): ReasonFallback says which.</summary>
		public bool IsFallback { get; set; }
		///<summary>The failing gate in words without the prefix ("no Test capture record for this sensor's driver version and image settings"),
		///for "8-bit (fallback: ...)" and the sidecars; null when Bits is above 8 or 8 is the configuration.</summary>
		public string ReasonFallback { get; set; }
		///<summary>Bits is above 8 and [Device.N] Polarity=Invert: the matching, passed verification record covers the inversion (design 9.3: the
		///session refuses an inverted capture without one). Goes to RadiographSaveRequest.IsInversionVerified.</summary>
		public bool IsInversionVerified { get; set; }
		///<summary>Bits is 14 although [Device.N] Bits is 16: the matching record's Test capture asked this driver for 16 and it ran at 14, so the
		///session asks for the 14 bits the record passed at (owner decision 2026-11-01: 16 by default, falling back to 14). The reason in words
		///without a prefix, for the capture log ("14 bits: ...", logged once by the controller) and the sidecars; null otherwise.</summary>
		public string ReasonBelowBits { get; set; }

		public static CaptureBitDepthChoice Configured8(string reason) {
			return new CaptureBitDepthChoice { Bits=TwainBitDepth.Bits8,Reason8Bit=CaptureBitDepthText.PrefixConfigured+reason.Trim().TrimEnd('.')+"." };
		}

		public static CaptureBitDepthChoice Fallback8(string reason) {
			string reasonTrimmed=(reason??"").Trim().TrimEnd('.');
			return new CaptureBitDepthChoice { Bits=TwainBitDepth.Bits8,IsFallback=true,ReasonFallback=reasonTrimmed,
				Reason8Bit=CaptureBitDepthText.PrefixFallback+reasonTrimmed+"." };
		}
	}

	///<summary>The capture session's decision, per enable, whether to ask the source for more than 8 bits (design 7.8 "When 16-bit is enabled",
	///7.10, 9.3; owner decisions 2026-11-01: 16 by default, falling back to 14 or 8, with every gate kept, and a Test capture record that covers
	///the sensor's driver version and image settings on any computer). The depth asked for is [Device.N] Bits (16 by default), and only when all
	///of these hold, checked in this order, each in its own method:
	///1. the settings are known; [Device.N] Bits is 16 or 14, and [Twain] Allow16Bit=1 (both are configuration: "8 bits: ...");
	///2. the session has not met a 14- or 16-bit layout it cannot read (TwainCaptureEnded.LayoutUnusableText);
	///3. the capture settings check of design 9.3: the gated settings are the ones last saved in Capture settings for this device on this
	///   computer, and the check was made for the file in effect;
	///4. the latest Test capture verification record for this device row, made on any computer, matches the row's TWAIN source, the driver
	///   build the open source reports and the image settings (CaptureImageSettingsHash), and passed at the depth asked for;
	///5. the target can be written as DICOM (a mount slot with tooth numbers of one view).
	///Otherwise 8 bits, with the first gate that failed as a fallback reason ("8 bits (fallback): ..."). The library then falls back from 16 to
	///14 or 8 by what the source lists and reports the depth used (TwainCaptureEnded.BitDepthInEffect). With Bits=16, a matching record whose
	///Test capture asked for 16 and passed at 14 (the driver lists 14 but not 16) gives 14: the session asks for the depth the record passed at,
	///which that driver falls back to anyway (GetBitsAsked).</summary>
	public static class CaptureBitDepthDecision {
		public static CaptureBitDepthChoice Decide(CaptureBitDepthInput input) {
			if(input==null || input.Options==null) {
				return CaptureBitDepthChoice.Fallback8("the device settings are not known");
			}
			int bits=TwainBitDepth.Normalize(input.BitsWanted??input.Options.Bits);
			if(bits<=8) {
				return CaptureBitDepthChoice.Configured8("[Device."+input.ImagingDeviceNum.ToString(CultureInfo.InvariantCulture)+"] Bits is 8");
			}
			if(!input.Options.Allow16Bit) {
				return CaptureBitDepthChoice.Configured8("[Twain] Allow16Bit is 0 (more than 8 bits are switched off on this computer)");
			}
			int bitsAsked=GetBitsAsked(input,bits);
			string reason=GetLayoutReason(input)??GetSettingsReason(input)??GetRecordReason(input,bitsAsked)??GetTargetReason(input.Target);
			if(reason!=null) {
				return CaptureBitDepthChoice.Fallback8(reason);
			}
			CaptureBitDepthChoice choice=new CaptureBitDepthChoice { Bits=bitsAsked,IsInversionVerified=input.Options.Polarity==TwainPolarity.Invert };
			if(bitsAsked<bits) {
				choice.ReasonBelowBits="[Device."+input.ImagingDeviceNum.ToString(CultureInfo.InvariantCulture)+"] Bits is "
					+bits.ToString(CultureInfo.InvariantCulture)+", and the latest Test capture record ("+input.Record.DateText+") asked this driver for 16 "
					+"bits and passed at 14, so the session asks for 14";
			}
			return choice;
		}

		///<summary>The depth the enable asks for: bits, except 14 for the device's Bits=16 (not a depth the recovery filer passes, BitsWanted) when
		///the latest record's Test capture asked for 16 and passed at 14. That driver lists 14 but not 16, so asking it for 16 gives 14 anyway;
		///asking for 14 keeps the depth to the one the record verified. GetRecordReason still checks that the record matches this source, driver
		///version and image settings (whose Bits is the 16 it was taken with).</summary>
		private static int GetBitsAsked(CaptureBitDepthInput input,int bits) {
			if(input.BitsWanted!=null || bits!=TwainBitDepth.Bits16) {
				return bits;
			}
			return GetBitsCovered(input.Record,bits)==TwainBitDepth.Bits14 ? TwainBitDepth.Bits14 : bits;
		}

		///<summary>The depth above 8 that a passed record lets a session with [Device.N] Bits=bitsDevice ask for, or 8 when it lets none: bitsDevice
		///when the record passed at it; 14 for bitsDevice 16 when the record's Test capture asked for 16 and passed at 14 (that driver lists 14 but
		///not 16). Whether the record matches the source, driver version and image settings is not checked here (GetRecordReason does).</summary>
		public static int GetBitsCovered(CaptureVerificationRecord record,int bitsDevice) {
			int bits=TwainBitDepth.Normalize(bitsDevice);
			if(record==null || bits<=8) {
				return TwainBitDepth.Bits8;
			}
			if(record.IsPassedFor(bits)) {
				return bits;
			}
			if(bits==TwainBitDepth.Bits16 && record.BitsRequested==TwainBitDepth.Bits16 && record.IsPassedFor(TwainBitDepth.Bits14)) {
				return TwainBitDepth.Bits14;
			}
			return TwainBitDepth.Bits8;
		}

		///<summary>A 14- or 16-bit image of this session came in a layout the library cannot read: 8 bits for the rest of the session.</summary>
		private static string GetLayoutReason(CaptureBitDepthInput input) {
			return string.IsNullOrWhiteSpace(input.ReasonLayoutUnusable) ? null : input.ReasonLayoutUnusable.Trim().TrimEnd('.');
		}

		///<summary>Design 9.3: no more than 8 bits from gated settings that were changed outside Open Dental, or never saved in the Capture settings
		///screen on this computer (not verified), or whose check was made for another file than the one in effect.</summary>
		private static string GetSettingsReason(CaptureBitDepthInput input) {
			CaptureSettingsCheck check=input.SettingsCheck;
			if(check==null) {
				return "the capture settings were not checked against the ones saved in Capture settings (not verified)";
			}
			if(!check.IsVerified) {
				return check.ReasonFallback;
			}
			if(!string.Equals(check.CurrentHash,(input.GatedSettingsHash??"").Trim(),StringComparison.OrdinalIgnoreCase)) {
				return "the capture settings checked are not the ones in effect (not verified)";
			}
			return null;
		}

		///<summary>Design 7.8 condition 2 and 9.3, with the owner's scope of 2026-11-01: the latest record for the device row matches the row's
		///source, the open source's driver build and the image settings, and passed at bits.</summary>
		private static string GetRecordReason(CaptureBitDepthInput input,int bits) {
			CaptureVerificationRecord record=input.Record;
			if(record==null) {
				return CaptureBitDepthText.NoRecord;
			}
			string imageSettingsHash;
			try {
				imageSettingsHash=GetImageSettingsHash(input.Options,input.ImagingDeviceNum);
			}
			catch(Exception ex) {
				return "the image settings could not be hashed ("+ex.Message.Trim().TrimEnd('.')+")";
			}
			string latest="the latest Test capture record ("+record.DateText+(string.IsNullOrWhiteSpace(record.ComputerName) ? ""
				: ", made on "+record.ComputerName.Trim())+")";
			switch(record.GetMismatch(input.ImagingDeviceNum,input.TwainName,input.DriverIdentity,imageSettingsHash)) {
				case CaptureRecordMismatch.LegacyRecord:
					return latest+" was made before records named the driver version and the image settings; run Test capture again";
				case CaptureRecordMismatch.Source:
					return latest+" is for another device row or TWAIN source; run Test capture again";
				case CaptureRecordMismatch.DriverUnknown:
					return "the TWAIN source did not report its driver version, so no Test capture record can match it";
				case CaptureRecordMismatch.Driver:
					return CaptureBitDepthText.NoRecord+": "+latest+" is for driver "+record.DriverIdentity.Describe()+", and this source reports "
						+input.DriverIdentity.Describe()+"; run Test capture again";
				case CaptureRecordMismatch.ImageSettings:
					return CaptureBitDepthText.NoRecord+": "+latest+" was made with other image settings (Bits, ByteOrder16 or Polarity); run Test "
						+"capture again";
			}
			if(!record.IsPassed) {
				return latest+" did not pass (an image of more than 8 bits over memory transfer, byte order and polarity confirmed)";
			}
			if(record.BitDepthVerified!=bits) {
				//"Set Bits=N" only when the device is not set to N already (the recovery filer decides for the depth a capture asked for).
				bool isVerifiedConfigured=record.BitDepthVerified==TwainBitDepth.Normalize(input.Options.Bits);
				return latest+" passed at "+record.BitDepthVerified.ToString(CultureInfo.InvariantCulture)+" bits, not at the "
					+bits.ToString(CultureInfo.InvariantCulture)+" bits asked for; run Test capture again at "+bits.ToString(CultureInfo.InvariantCulture)
					+" bits"+(isVerifiedConfigured ? "" : ", or set Bits="+record.BitDepthVerified.ToString(CultureInfo.InvariantCulture)+" and run it then");
			}
			return null;
		}

		///<summary>Design 7.8 "Anatomy" and 7.10: a target with no mount slot, or whose slot has no tooth numbers of one view, is captured at 8
		///bits and never written as DICOM.</summary>
		private static string GetTargetReason(IntraOralTarget target) {
			if(target==null) {
				return "the target is not known";
			}
			string reason;
			if(!DicomIntraOralWriter.CanWrite(target,out reason)) {
				return "the target cannot be stored as DICOM: "+(reason??"").Trim().TrimEnd('.');
			}
			return null;
		}

		///<summary>CaptureImageSettingsHash of options for imagingDeviceNum, or for options.ImagingDeviceNum when imagingDeviceNum is not positive.</summary>
		public static string GetImageSettingsHash(TwainCaptureOptions options,long imagingDeviceNum) {
			TwainCaptureOptions optionsHashed=options.Clone();
			if(imagingDeviceNum>0) {
				optionsHashed.ImagingDeviceNum=imagingDeviceNum;
			}
			return CaptureImageSettingsHash.Compute(optionsHashed);
		}
	}

	///<summary>The bit depth in the words the operator sees (owner decision 2026-11-01): "16-bit", "14-bit in a 16-bit file", "8-bit", or
	///"8-bit (fallback: &lt;reason&gt;)", for the session window's summary and "In effect" line, Test capture and the Unassigned captures window.</summary>
	public static class CaptureBitDepthText {
		public const string Bits16="16-bit";
		public const string Bits14="14-bit in a 16-bit file";
		public const string Bits8="8-bit";
		///<summary>The start of a configuration's 8-bit reason in the capture log.</summary>
		public const string PrefixConfigured="8 bits: ";
		///<summary>The start of a fallback's 8-bit reason in the capture log.</summary>
		public const string PrefixFallback="8 bits (fallback): ";
		///<summary>The start of the log line when the session asks for 14 bits with Bits=16 (CaptureBitDepthChoice.ReasonBelowBits).</summary>
		public const string Prefix14="14 bits: ";
		///<summary>The fallback reason when no record exists for the device.</summary>
		public const string NoRecord="no Test capture record for this sensor's driver version and image settings";

		///<summary>The depth image was actually stored at, in words: its samples' depth for an image of more than 8 bits ("16-bit", "14-bit in a
		///16-bit file"), otherwise "8-bit", with the reason when the enable fell back: the library's or the request's (AcquiredImage.BitDepthReason),
		///else reasonFallbackEnable (the session's decision for the enable), and for an 8-bit image of a 14- or 16-bit enable that the source had
		///no original of more than 8 bits for it. "" for null.</summary>
		public static string DescribeImage(AcquiredImage image,string reasonFallbackEnable=null) {
			if(image==null) {
				return "";
			}
			if(image.IsGray16) {
				return image.BitDepthInEffect==TwainBitDepth.Bits14 ? Bits14 : Bits16;
			}
			string reason=!string.IsNullOrWhiteSpace(image.BitDepthReason) ? image.BitDepthReason : reasonFallbackEnable;
			if(image.IsDeliveredAs8BitIn16BitEnable) {
				reason="the source had no original of more than 8 bits for this image";
			}
			return Describe(TwainBitDepth.Bits8,reason);
		}

		///<summary>The depth a capture file of the quarantine or the journal was taken at, as its sidecar records it (CaptureFileEntry
		///BitDepthInEffect, BitDepthReason), in the words of DescribeImage. "" for null.</summary>
		public static string DescribeEntry(CaptureFileEntry entry) {
			if(entry==null) {
				return "";
			}
			if(entry.IsGray16) {
				return entry.BitDepthInEffect==TwainBitDepth.Bits14 ? Bits14 : Bits16;
			}
			string reason=entry.Get("DeliveredAs8BitIn16BitEnable")=="1" ? "the source had no original of more than 8 bits for this image"
				: entry.BitDepthReason;
			return Describe(TwainBitDepth.Bits8,reason);
		}

		///<summary>"16-bit", "14-bit in a 16-bit file" or "8-bit"; "8-bit (fallback: reason)" for 8 with a reason.</summary>
		public static string Describe(int bitDepth,string reasonFallback=null) {
			switch(TwainBitDepth.Normalize(bitDepth)) {
				case TwainBitDepth.Bits16:
					return Bits16;
				case TwainBitDepth.Bits14:
					return Bits14;
				default:
					string reason=(reasonFallback??"").Trim().TrimEnd('.');
					return reason=="" ? Bits8 : Bits8+" (fallback: "+reason+")";
			}
		}
	}

	///<summary>Finds the Test capture verification record the capture session uses (design 9.3, owner decision 2026-11-01): among the Setup
	///security-log entries with FKey=ImagingDeviceNum whose text is a record, the latest one for this device row, made on any computer. A newer
	///record that does not match the current source, driver version or image settings wins over an older one that does: the latest test is what
	///counts.</summary>
	public static class CaptureVerificationRecords {
		///<summary>The latest record for imagingDeviceNum on any computer, by LogDateTime and then SecurityLogNum, or null.</summary>
		public static CaptureVerificationRecord FindLatest(IEnumerable<SecurityLog> listSecurityLogs,long imagingDeviceNum) {
			CaptureVerificationRecord recordLatest=null;
			SecurityLog securityLogLatest=null;
			foreach(SecurityLog securityLog in (listSecurityLogs??Enumerable.Empty<SecurityLog>()).Where(x => x!=null)) {
				CaptureVerificationRecord record;
				if(!CaptureVerificationRecord.TryParse(securityLog.LogText,out record)) {
					continue;
				}
				if(record.ImagingDeviceNum!=imagingDeviceNum) {
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
