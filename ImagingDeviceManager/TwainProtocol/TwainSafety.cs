// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using ImagingDeviceManager.Config;
using ImagingDeviceManager.Imaging;

namespace ImagingDeviceManager.TwainProtocol {
	///<summary>The administrator-set [Twain] keys of capture.ini that change how the session runs and stops a by-name acquisition
	///(design 7.1 and 9.3). Device-specific numbers (the safe-disable capability, the refusal codes) come only from here, as data
	///copied from the source's published contract; the library has none of its own. The facade fills it from LocalCaptureConfig
	///before each acquisition. The defaults turn every feature off.</summary>
	internal sealed class TwainCaptureSettings {
		///<summary>The library's own bound for the wait after a refused disable. The sensor contract asks hosts to allow at least 20 s.</summary>
		public static readonly TimeSpan RefusedDisableWaitDefault=TimeSpan.FromSeconds(30);

		///<summary>[Twain] DisableIsSafeSources: ProductNames of the sources whose disable may be made safe. Matched ignoring case and
		///surrounding spaces, because the legacy loop gives the library only the device's TwainName.</summary>
		public List<string> ListDisableIsSafeSources=new List<string>();
		///<summary>[Twain] SafeDisableCap: the TWTY_BOOL capability that, set TRUE in state 4, makes the source refuse a disable that would
		///lose images and offer them first. Null: not used.</summary>
		public ushort? SafeDisableCap;
		///<summary>[Twain] RefusedDisableCodes: the condition codes with which the source refuses MSG_DISABLEDS and then sends
		///MSG_XFERREADY or MSG_CLOSEDSREQ.</summary>
		public List<ushort> ListRefusedDisableCodes=new List<ushort>();
		///<summary>[Twain] AcquireTimeoutMinutes. Zero: none. Honoured only for XFERCOUNT=1 by-name acquisitions of a source in
		///ListDisableIsSafeSources whose safe-disable MSG_SET succeeded.</summary>
		public TimeSpan AcquireTimeout=TimeSpan.Zero;
		///<summary>How long to keep the message loop running after a refused disable. Not in capture.ini; tests shorten it.</summary>
		public TimeSpan RefusedDisableWait=RefusedDisableWaitDefault;

		///<summary>The [Twain] keys of config. A capability number or condition code outside 0x0001-0xFFFF is ignored.</summary>
		public static TwainCaptureSettings FromConfig(LocalCaptureConfig config) {
			TwainCaptureSettings settings=new TwainCaptureSettings();
			if(config==null) {
				return settings;
			}
			settings.ListDisableIsSafeSources=config.DisableIsSafeSources;
			int? safeDisableCap=config.SafeDisableCap;
			if(safeDisableCap!=null && safeDisableCap.Value>0 && safeDisableCap.Value<=ushort.MaxValue) {
				settings.SafeDisableCap=(ushort)safeDisableCap.Value;
			}
			foreach(int code in config.RefusedDisableCodes) {
				if(code>0 && code<=ushort.MaxValue && !settings.ListRefusedDisableCodes.Contains((ushort)code)) {
					settings.ListRefusedDisableCodes.Add((ushort)code);
				}
			}
			settings.AcquireTimeout=TimeSpan.FromMinutes(config.AcquireTimeoutMinutes);
			return settings;
		}

		///<summary>True when productName is listed in DisableIsSafeSources.</summary>
		public bool IsDisableSafeSource(string productName) {
			string name=(productName??"").Trim();
			if(name=="") {
				return false;
			}
			foreach(string nameListed in ListDisableIsSafeSources) {
				if(string.Equals((nameListed??"").Trim(),name,StringComparison.OrdinalIgnoreCase)) {
					return true;
				}
			}
			return false;
		}
	}

	///<summary>Why the library itself stopped an acquisition.</summary>
	internal enum TwainStopReason {
		None,
		///<summary>Cancel was requested (TwainSession.Cancel).</summary>
		Cancel,
		///<summary>[Twain] AcquireTimeoutMinutes passed.</summary>
		Timeout,
		///<summary>The window passed to Acquire was destroyed, for example by an automatic logoff.</summary>
		ParentDestroyed,
		///<summary>WM_QUIT arrived: the program is closing. It is posted again once the source is stopped.</summary>
		Quit,
		///<summary>The caller closed the source while images were still pending (CloseSource or Dispose).</summary>
		Closed,
		///<summary>An unexpected error, a failed transfer, or a source that stopped responding.</summary>
		Error,
	}

	///<summary>Why an image went to the drain sink instead of to the caller.</summary>
	internal enum TwainDrainReason {
		///<summary>It was pending (state 6) when the library stopped the acquisition; StopReason says why. Transferring it, instead of
		///discarding it with MSG_RESET, keeps an image the source already offered.</summary>
		Stop,
		///<summary>The source offered it after refusing MSG_DISABLEDS (safe disable).</summary>
		RefusedDisable,
		///<summary>The source delivered more images than the one asked for in single mode (XFERCOUNT=1), for example because one of its
		///own settings overrides the count.</summary>
		ExtraImage,
	}

	///<summary>An image the session transferred after the caller could no longer take it.</summary>
	internal sealed class TwainDrainedImage {
		public DibImage Image;
		public TwainDrainReason Reason;
		public TwainStopReason StopReason;
		public string SourceName;
		///<summary>Local time of the transfer.</summary>
		public DateTime ReceivedAt;
	}

	///<summary>What the session hands over that the caller cannot take. The facade implements it with the journal and the quarantine
	///(design 7.1 and 7.12). Called on the thread that runs the acquisition, from inside its message loop; a member that throws is
	///logged and otherwise ignored. With no sink set, pending images are discarded with MSG_RESET as before, which leaves them
	///undelivered in the source (a source that offers redelivery can still offer them).</summary>
	internal interface ITwainDrainSink {
		///<summary>One image, transferred and ended with MSG_ENDXFER. The sink owns it from now on.</summary>
		void ImageDrained(TwainDrainedImage drained);

		///<summary>The wait after a refused disable ended without a notification, and the second consecutive MSG_DISABLEDS was sent. The
		///source may still hold images of this series: the facade tells the operator not to re-expose.</summary>
		void MayStillHoldImages(string sourceName,TwainStopReason stopReason);
	}
}
