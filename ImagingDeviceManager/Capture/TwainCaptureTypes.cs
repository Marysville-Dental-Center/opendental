// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;

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

	///<summary>What one Enable or RequestRedelivery asks for. The library does what it is asked; whether 16 bits may be asked (Bits=16,
	///Allow16Bit, a verification record, a target that can be written as DICOM) is the caller's decision (design 7.1 CAP-3, 7.8, 7.10).</summary>
	public sealed class TwainEnableRequest {
		///<summary>8 or 16. 16 is negotiated only when ICAP_PIXELTYPE is GRAY: ICAP_XFERMECH=TWSX_MEMORY first, then ICAP_BITDEPTH=16 if MSG_GET
		///lists it; otherwise the enable runs at 8 bits (logged).</summary>
		public int Bits=8;
		///<summary>Null: TwainCaptureOptions.ByteOrder16.</summary>
		public TwainByteOrder? ByteOrder16;
		///<summary>Null: !TwainCaptureOptions.Headless. A source that reports CAP_UICONTROLLABLE FALSE always gets ShowUI=1.</summary>
		public bool? ShowUI;
		///<summary>Carried to every image of the enable and to its TwainCaptureEnded, for the caller's own bookkeeping (for example the target
		///slot).</summary>
		public object Tag;
		///<summary>The target, in words, for the journal and quarantine sidecars ("Slot 3 (teeth 2,3)").</summary>
		public string TargetLabel="";
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
		///<summary>The source may still hold images of this enable that the library did not receive: a transfer failed, or a refused disable
		///got no notification within the bound and the second consecutive MSG_DISABLEDS was sent. The window offers redelivery where the device
		///profile names a redelivery capability, and says not to re-expose (design 7.12, 7.13).</summary>
		public bool SourceMayHoldImages { get; set; }
		///<summary>The safe-disable capability was accepted before this enable ([Device.N] DisableIsSafe=1 and SafeDisableCap set).</summary>
		public bool IsSafeDisableInEffect { get; set; }
		///<summary>The library started a disable of a running enable while safe disable was not in effect: a capture event (design 7.13).</summary>
		public bool WasDisableStartedUnsafe { get; set; }
		///<summary>MSG_DISABLEDS was refused at least once (W13).</summary>
		public bool WasDisableRefused { get; set; }
		///<summary>A refused disable got no notification within the bound, and the second consecutive MSG_DISABLEDS was sent: a capture event
		///(design 7.13).</summary>
		public bool WasSecondDisableAfterWait { get; set; }
		///<summary>The enable negotiated 16 bits.</summary>
		public bool IsBitDepth16InEffect { get; set; }
		///<summary>Why 16 bits were asked and the enable ran at 8 (for Test capture and the log), else null.</summary>
		public string Reason8Bit { get; set; }
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

	///<summary>What the open source reported in state 4: its identity and the capabilities the capture session reads, never sets (design 7.1
	///CAP-3). For Test capture and the log. A value is null when the source did not answer.</summary>
	public sealed class TwainCaptureSourceInfo {
		public string ProductName { get; set; }
		public string Manufacturer { get; set; }
		public string ProductFamily { get; set; }
		public string VersionInfo { get; set; }
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
