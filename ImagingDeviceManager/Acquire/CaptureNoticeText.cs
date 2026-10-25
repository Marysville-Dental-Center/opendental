// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Globalization;
using ImagingDeviceManager.TwainProtocol;

namespace ImagingDeviceManager.Acquire {
	///<summary>Every text the library shows the operator around a device acquisition (design 7.1 CAP-1). There are no device or vendor
	///names: "sensor software" is the role of the source's own window.</summary>
	internal static class CaptureNoticeText {
		public const string SourceNotEnabled=TwainSourceGate.RefusedText;
		public const string StatusOpen="Sensor software is open. Cancel ends this series.";
		public const string CancelWarningSingle="If an exposure was just taken, wait until it appears before cancelling.";
		public const string CancelWarningBatch="Cancel here may lose images already taken in this series; use Done in the sensor software to keep them.";
		public const string Waiting="Waiting for the sensor software to finish.";

		///<summary>The warning the status window shows before a Cancel, or null when the cancel needs none: only when the source's disable
		///is safe (the safe-disable capability was accepted), because only then can a stop not lose an image the source holds. A batch
		///(CAP_XFERCOUNT -1) delivers every image taken when the operator ends it in the sensor software, and a disable does not.</summary>
		public static string GetCancelWarning(bool isSafeDisableInEffect,bool isBatch) {
			if(isSafeDisableInEffect) {
				return null;
			}
			return isBatch ? CancelWarningBatch : CancelWarningSingle;
		}

		///<summary>"2026-10-25 14:30:12": receive times in notices and sidecars, local time.</summary>
		public static string FormatTime(DateTime dateTime) {
			return dateTime.ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture);
		}
	}
}
