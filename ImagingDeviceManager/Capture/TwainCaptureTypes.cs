// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace ImagingDeviceManager.Capture {
	///<summary>[Device.N] SessionMode (design 7.4).</summary>
	public enum TwainSessionMode {
		///<summary>The default: CAP_XFERCOUNT=1 and one enable/disable cycle per slot, the DS staying open in state 4.</summary>
		PerSlot,
		///<summary>Explicit opt-in on both sides: with the device profile's ContinuousCap accepted, the session stays in state 5 after
		///Count=0 and waits for the next MSG_XFERREADY. This is the only case in which it waits there.</summary>
		Continuous,
	}

	///<summary>[Device.N] Polarity (review flag, design 7.8). The library never inverts pixels; the saver does, only when verified.</summary>
	public enum TwainPolarity {
		AsDelivered,
		Invert,
	}

	///<summary>Why the session window (or the library itself) stops an enable. Carried on the images transferred during the stop
	///(AcquiredImage.StopReason) and on TwainCaptureEnded.</summary>
	public enum TwainCaptureStopReason {
		None,
		///<summary>The operator cancelled (TwainCaptureSession.Cancel).</summary>
		Cancel,
		///<summary>The operator ended the session.</summary>
		End,
		///<summary>[Session] IdleDisableMinutes passed (design 7.6; only with DisableIsSafe).</summary>
		Idle,
		///<summary>The patient changed in the main window (design 7.6).</summary>
		PatientChanged,
		///<summary>The target changed while enabled (Retake, Skip, a tapped slot) and the window disables before it enables for the new
		///target.</summary>
		TargetChanged,
		///<summary>[Device.N] SessionTimeoutMinutes passed (honoured only with DisableIsSafe in effect).</summary>
		SessionTimeout,
		///<summary>BeginClose: the window is closing (FormClosing, a forced logoff).</summary>
		Close,
		///<summary>The worker failed (an exception, a broken message loop, a source that stopped responding).</summary>
		Error,
		///<summary>Any other reason the window has.</summary>
		Other,
	}

	///<summary>How an enable ended.</summary>
	public enum TwainCaptureEndReason {
		///<summary>The images were transferred and the source was disabled after Count=0 (PerSlot).</summary>
		Completed,
		///<summary>The source sent MSG_CLOSEDSREQ (the sensor software closed, timed out or failed). ConditionCode is what DAT_STATUS returned
		///before the disable: TWCC_SUCCESS (0) for a plain cancel, otherwise the source's code, with ConditionText from DAT_STATUSUTF8.</summary>
		CloseRequested,
		///<summary>Disable, Cancel, the session timeout or BeginClose stopped it in the safe order; StopReason says which.</summary>
		Stopped,
		///<summary>A transfer failed (DAT_IMAGEMEMXFER or DAT_IMAGENATIVEXFER): "Image not received. Do not re-expose yet." (design 7.12).
		///The images that did arrive were raised.</summary>
		TransferFailed,
		///<summary>MSG_ENABLEDS failed (TWCC_OPERATIONERROR: the sensor software could not be started) or was cancelled.</summary>
		EnableFailed,
		///<summary>CAP_DEVICEONLINE reported FALSE before the enable: "Sensor not connected". Nothing was enabled.</summary>
		DeviceOffline,
		///<summary>RequestRedelivery with no redelivery capability in the device profile, or the source did not accept it. Nothing was
		///enabled, so no capture started.</summary>
		RedeliveryUnavailable,
		///<summary>The command was not carried out: the session is not open, is closing, or is already enabled.</summary>
		Refused,
		///<summary>The worker failed; the source was stopped in the safe order. ErrorText says why.</summary>
		Error,
	}

	///<summary>Where the worker is, for the indicator (design 7.3).</summary>
	public enum TwainCaptureState {
		Closed,
		Opening,
		///<summary>State 4: the source is open and not enabled.</summary>
		Open,
		Enabling,
		///<summary>State 5: "Sensor software open, waiting for exposure".</summary>
		WaitingForImage,
		///<summary>States 6 and 7.</summary>
		Transferring,
		///<summary>A stop in the safe order is under way.</summary>
		Stopping,
		///<summary>A disable was refused (W13): "Waiting for the sensor software to finish" (at most the wait bound, design 7.12).</summary>
		WaitingForSource,
		Closing,
	}

	///<summary>The bit depths a capture enable can ask for and run at ([Device.N] Bits, owner decision 2026-11-01: 16 by default, falling back to
	///14 or 8): 16 and 14 are negotiated over memory transfer and delivered as raw samples in 16-bit containers (AcquiredImage.Gray16); 8 is
	///everything else.</summary>
	public static class TwainBitDepth {
		public const int Bits8=8;
		public const int Bits14=14;
		public const int Bits16=16;

		///<summary>8, 14 and 16.</summary>
		public static bool IsAllowed(int bits) {
			return bits==Bits8 || bits==Bits14 || bits==Bits16;
		}

		///<summary>bits when it is 8, 14 or 16, otherwise 8.</summary>
		public static int Normalize(int bits) {
			return IsAllowed(bits) ? bits : Bits8;
		}

		///<summary>The ICAP_BITDEPTH values tried, in order, when bitsWanted is asked for: 16 then 14 for 16, 14 for 14, none for 8 (or any other
		///value). When none of them is listed and accepted, the enable runs at 8 bits.</summary>
		public static List<int> GetCandidates(int bitsWanted) {
			switch(Normalize(bitsWanted)) {
				case Bits16: return new List<int> { Bits16,Bits14 };
				case Bits14: return new List<int> { Bits14 };
				default: return new List<int>();
			}
		}
	}

	///<summary>What one Enable or RequestRedelivery asks for. The library does what it is asked; whether more than 8 bits may be asked ([Device.N]
	///Bits, Allow16Bit, a verification record, a target that can be written as DICOM) is the caller's decision (design 7.1 CAP-3, 7.8, 7.10).</summary>
	public sealed class TwainEnableRequest {
		///<summary>16, 14 or 8 (TwainBitDepth; any other value counts as 8). More than 8 bits are negotiated only when ICAP_PIXELTYPE is GRAY:
		///ICAP_XFERMECH=TWSX_MEMORY first, then for 16 ICAP_BITDEPTH=16 if MSG_GET lists 16, else 14 if it lists 14; for 14 only 14. When none is
		///listed and accepted the enable runs at 8 bits. TwainCaptureEnded.BitDepthInEffect and ReasonFallback report what was used.</summary>
		public int Bits=8;
		///<summary>Why the caller asks for fewer bits than the device is set to ([Device.N] Bits), for example a gate of the capture session's
		///decision that failed; null when it asks for what the device is set to. Recorded in the sidecars of the enable's images
		///(CaptureSidecarKeys.BitDepthReason) unless the library's own fallback (TwainCaptureEnded.ReasonFallback) replaces it.</summary>
		public string ReasonFallback;
		///<summary>Null: TwainCaptureOptions.ByteOrder16.</summary>
		public TwainByteOrder? ByteOrder16;
		///<summary>Null: !TwainCaptureOptions.Headless. A source that reports CAP_UICONTROLLABLE FALSE always gets ShowUI=1.</summary>
		public bool? ShowUI;
		///<summary>Carried to every image of the enable and to its TwainCaptureEnded, for the caller's own bookkeeping (for example the target
		///slot).</summary>
		public object Tag;
		///<summary>The target, in words, for the journal and quarantine sidecars ("Slot 3 (teeth 2,3)").</summary>
		public string TargetLabel="";
		///<summary>Extra Key=Value lines the caller adds to the journal and quarantine sidecars of every image of the enable, so that a later
		///recovery can file the image where it was meant to go (CaptureSidecarKeys: the mount, the slot, the category, the provider, the
		///device). Null or empty: none. Keys and values are written as given, cleaned to one line each.</summary>
		public List<KeyValuePair<string,string>> SidecarFields;
	}

	///<summary>The end of one enable, raised once per Enable or RequestRedelivery (also when it never reached MSG_ENABLEDS).</summary>
	public sealed class TwainCaptureEnded {
		public int EnableId { get; set; }
		public object Tag { get; set; }
		public bool IsRedelivery { get; set; }
		public TwainCaptureEndReason Reason { get; set; }
		///<summary>Why the enable was stopped (Reason Stopped), else None.</summary>
		public TwainCaptureStopReason StopReason { get; set; }
		///<summary>Reason CloseRequested: what DAT_STATUS returned after MSG_CLOSEDSREQ and before the disable (null when it could not be read).
		///Reason EnableFailed and TransferFailed: the condition code of the failure.</summary>
		public ushort? ConditionCode { get; set; }
		///<summary>The source's text for ConditionCode from DAT_STATUSUTF8, when it gave one (null for TWCC_SUCCESS, and when the source or the
		///DSM does not pass DAT_STATUSUTF8 through: the code alone is then the reason, shown in hex).</summary>
		public string ConditionText { get; set; }
		///<summary>Images raised as ImageAcquired (or, after BeginClose, put in the quarantine) from this enable.</summary>
		public int ImageCount { get; set; }
		public int ExtraImageCount { get; set; }
		public int DrainedImageCount { get; set; }
		public int TransferFailureCount { get; set; }
		///<summary>The source may still hold images of this enable that the library did not receive: a transfer failed, or after a refused
		///disable the source was disabled without having offered what it held (the way-out MSG_DISABLEDS succeeded, or after a second bound the
		///last one succeeded or the source was treated as disabled). Not when the way-out was refused and the source then sent its notification
		///and its images were received. The window offers redelivery where the device profile names a redelivery capability, and says not to
		///re-expose (design 7.12, 7.13).</summary>
		public bool SourceMayHoldImages { get; set; }
		///<summary>The safe-disable capability was accepted before this enable ([Device.N] DisableIsSafe=1 and SafeDisableCap set).</summary>
		public bool IsSafeDisableInEffect { get; set; }
		///<summary>The library started a disable of a running enable while safe disable was not in effect: a capture event (design 7.13).</summary>
		public bool WasDisableStartedUnsafe { get; set; }
		///<summary>MSG_DISABLEDS was refused at least once (W13).</summary>
		public bool WasDisableRefused { get; set; }
		///<summary>A refused disable got no notification within the bound, and the way-out MSG_DISABLEDS (a second one with no MSG_ENDXFER
		///since the refusal) was sent: a capture event (design 7.13). It may have been refused by a source still finishing, the session then
		///waiting a second bound (the log says so); SourceMayHoldImages, not this, says whether images may still be held.</summary>
		public bool WasSecondDisableAfterWait { get; set; }
		///<summary>16, 14 or 8: what the enable asked for (TwainEnableRequest.Bits).</summary>
		public int BitsRequested { get; set; }=8;
		///<summary>16, 14 or 8: the ICAP_BITDEPTH the enable ran at (16 or 14 over memory transfer), as negotiated in state 4.</summary>
		public int BitDepthInEffect { get; set; }=8;
		///<summary>Why BitDepthInEffect is less than BitsRequested (16 asked and 14 or 8 used, 14 asked and 8 used), for Test capture, the
		///session and the log, else null.</summary>
		public string ReasonFallback { get; set; }
		///<summary>A transfer of the enable failed because an image of more than 8 bits came in a layout the library cannot read (DAT_IMAGEINFO
		///BitsPerPixel not 16 for a 14- or 16-bit sample, or packed samples): "14-bit layout not usable: ..." (or 16-bit), else null. Reason is
		///TransferFailed; the image was not read and nothing of it was kept. The session asks for 8 bits for the rest of the session.</summary>
		public string LayoutUnusableText { get; set; }
		///<summary>Reason Error, Refused, EnableFailed, RedeliveryUnavailable: a sentence for the indicator area.</summary>
		public string ErrorText { get; set; }
		public DateTime EndedAt { get; set; }
	}

	///<summary>A status change of the worker, for the indicator.</summary>
	public sealed class TwainCaptureStatus {
		public TwainCaptureState State { get; set; }
		///<summary>The enable the state belongs to, or 0.</summary>
		public int EnableId { get; set; }
		///<summary>A short English description ("Sensor software open, waiting for exposure").</summary>
		public string Text { get; set; }
		///<summary>The enable the state belongs to has the safe-disable capability accepted (TwainCaptureEnded.IsSafeDisableInEffect), known
		///from WaitingForImage on. The session window uses it to decide, while the enable runs, whether it may disable on its own (design 7.6,
		///7.11): without it every disable it starts may lose an exposure.</summary>
		public bool IsSafeDisableInEffect { get; set; }
	}

	public enum TwainCaptureOpenError {
		None,
		///<summary>No TWAIN Data Source Manager could be loaded or opened: "TWAIN is not installed on this computer".</summary>
		NoDsm,
		///<summary>The source gate ([Twain] RestrictSources) refused it: "This TWAIN source is not enabled on this computer."</summary>
		SourceNotEnabledHere,
		///<summary>No installed source has that ProductName, or MSG_OPENDS failed: "Imaging device '...' not found."</summary>
		SourceNotFound,
		///<summary>Open was called while the session was not closed.</summary>
		AlreadyOpen,
		///<summary>The worker did not finish the open within TwainCaptureOptions.OpenTimeout; the session closes when it does.</summary>
		Timeout,
		///<summary>Another capture session of this program has the source open, or did not finish closing it within
		///TwainCaptureOptions.OpenTimeout: two sessions never drive one source at once.</summary>
		SourceBusy,
		Other,
	}

	public sealed class TwainCaptureOpenResult {
		public bool IsOpen { get; set; }
		public TwainCaptureOpenError Error { get; set; }
		public string ErrorText { get; set; }
		///<summary>The condition code of the failed open, when there was one.</summary>
		public ushort? ConditionCode { get; set; }
		public TwainCaptureSourceInfo SourceInfo { get; set; }
	}

	///<summary>One custom capability of the device profile, as the open source answered MSG_QUERYSUPPORT.</summary>
	public sealed class TwainCustomCapabilityInfo {
		///<summary>The [Device.N] key: SafeDisableCap, RedeliverCap, NoActivateCap or ContinuousCap.</summary>
		public string Key { get; set; }
		public ushort Number { get; set; }
		///<summary>The TWQC_ bits, or null when the source did not answer.</summary>
		public int? QuerySupport { get; set; }
		///<summary>The condition code when the source did not answer MSG_QUERYSUPPORT (TWCC_CAPUNSUPPORTED for one it does not have).</summary>
		public ushort? ConditionCode { get; set; }
		public bool IsSettable => QuerySupport!=null && (QuerySupport.Value & 0x0002)!=0;
	}

	///<summary>Which build of the TWAIN source delivered the images: TW_IDENTITY ProductName and Version (MajorNum, MinorNum and Info) as the
	///source reports them at open. A Test capture verification record names it, and the capture session compares it with the source it opened
	///(owner decision 2026-11-01: the record covers every computer that runs the same driver version with the same image settings).
	///Immutable; Info is the version text, trimmed.</summary>
	public sealed class TwainDriverIdentity {
		public string ProductName { get; }
		public int MajorNum { get; }
		public int MinorNum { get; }
		public string Info { get; }

		public TwainDriverIdentity(string productName,int majorNum,int minorNum,string info) {
			ProductName=(productName??"").Trim();
			MajorNum=majorNum;
			MinorNum=minorNum;
			Info=(info??"").Trim();
		}

		///<summary>"1.2".</summary>
		public string VersionText => MajorNum.ToString(CultureInfo.InvariantCulture)+"."+MinorNum.ToString(CultureInfo.InvariantCulture);

		///<summary>The same source build: ProductName ignoring case (as the source gate compares names), MajorNum, MinorNum, and Info exactly.
		///False for null.</summary>
		public bool IsSameAs(TwainDriverIdentity other) {
			return other!=null
				&& string.Equals(ProductName,other.ProductName,StringComparison.OrdinalIgnoreCase)
				&& MajorNum==other.MajorNum
				&& MinorNum==other.MinorNum
				&& string.Equals(Info,other.Info,StringComparison.Ordinal);
		}

		///<summary>"Example Sensor DS, version 1.2 (1.2.0 build 7)", for the texts and the log.</summary>
		public string Describe() {
			return (ProductName=="" ? "(no product name)" : ProductName)+", version "+VersionText+(Info=="" ? "" : " ("+Info+")");
		}

		///<summary>The identity from "major.minor" text and the other parts, or null when versionText is not two whole numbers.</summary>
		public static TwainDriverIdentity TryParse(string productName,string versionText,string info) {
			string[] arrayParts=(versionText??"").Trim().Split('.');
			int major;
			int minor;
			if(arrayParts.Length!=2 || !int.TryParse(arrayParts[0],NumberStyles.None,CultureInfo.InvariantCulture,out major)
				|| !int.TryParse(arrayParts[1],NumberStyles.None,CultureInfo.InvariantCulture,out minor))
			{
				return null;
			}
			return new TwainDriverIdentity(productName,major,minor,info);
		}
	}

	///<summary>What the open source reported in state 4: its identity and the capabilities the capture session reads, never sets (design 7.1
	///CAP-3). For Test capture and the log. A value is null when the source did not answer.</summary>
	public sealed class TwainCaptureSourceInfo {
		public string ProductName { get; set; }
		public string Manufacturer { get; set; }
		public string ProductFamily { get; set; }
		///<summary>TW_IDENTITY Version.MajorNum and MinorNum.</summary>
		public int VersionMajor { get; set; }
		public int VersionMinor { get; set; }
		///<summary>TW_IDENTITY Version.Info.</summary>
		public string VersionInfo { get; set; }
		///<summary>ProductName and Version as one identity, or null when the source's identity could not be read.</summary>
		public TwainDriverIdentity DriverIdentity { get; set; }
		public int ProtocolMajor { get; set; }
		public int ProtocolMinor { get; set; }
		public uint SupportedGroups { get; set; }
		///<summary>The DSM library (TWAINDSM.dll or twain_32.dll).</summary>
		public string DsmName { get; set; }
		public bool? UiControllable { get; set; }
		public bool? DeviceOnline { get; set; }
		public bool? Indicators { get; set; }
		public bool? UndefinedImageSize { get; set; }
		public int? PixelType { get; set; }
		public int? PixelFlavor { get; set; }
		public int? BitOrder { get; set; }
		public int? PlanarChunky { get; set; }
		public int? SupportedSizes { get; set; }
		///<summary>ICAP_BITDEPTH MSG_GET at open, with the mechanism the source opened with (usually native); a source may list 16 only
		///after ICAP_XFERMECH=TWSX_MEMORY.</summary>
		public List<int> ListBitDepths { get; set; }
		///<summary>ICAP_EXTIMAGEINFO can be set TRUE (MSG_QUERYSUPPORT).</summary>
		public bool IsExtImageInfoSettable { get; set; }
		///<summary>ICAP_SUPPORTEDEXTIMAGEINFO, or null when it cannot be read.</summary>
		public List<int> ListSupportedExtImageInfo { get; set; }
		///<summary>ICAP_SUPPORTEDEXTIMAGEINFO lists TWEI_TWAINDIRECTMETADATA (0x124B): the capture metadata can be read.</summary>
		public bool IsMetadataListed { get; set; }
		///<summary>The custom capabilities the device profile names, in the order SafeDisableCap, RedeliverCap, NoActivateCap, ContinuousCap.</summary>
		public List<TwainCustomCapabilityInfo> ListCustomCapabilities { get; set; }=new List<TwainCustomCapabilityInfo>();
	}

	///<summary>Who the session's images belong to, for the sidecars of the quarantine (design 7.12: session captures record the originating
	///PatNum, the patient's name, the session GUID and the slot). The library knows no patient of its own. Immutable, so that PatNum and
	///PatientName always name the same patient: the session reads it once per image, and a change replaces it (QuarantineContext).</summary>
	public sealed class TwainCaptureQuarantineContext {
		public long PatNum { get; }
		public string PatientName { get; }

		///<summary>No patient.</summary>
		public TwainCaptureQuarantineContext():this(0,"") {
		}

		public TwainCaptureQuarantineContext(long patNum,string patientName) {
			PatNum=patNum;
			PatientName=patientName??"";
		}
	}

	///<summary>What the caller's metadata parser found in AcquiredImage.MetadataJson, for the journal and quarantine sidecars (design 7.9:
	///sidecars record captureId and sequence when present).</summary>
	public sealed class CaptureMetadataKeys {
		public string CaptureId { get; set; }
		public int? Sequence { get; set; }
	}
}
