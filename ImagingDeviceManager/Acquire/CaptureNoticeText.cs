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
		public const string MayStillHoldImages="The sensor software may still hold images from this series. Do not re-expose. See the runbook.";
		///<summary>Unassigned captures older than this turn the status window's count into a warning (design 7.12 "Visibility").</summary>
		public const int UnassignedWarningDays=30;

		///<summary>The quarantine notice (design 7.1): "N image(s) received after the series stopped were saved to the capture quarantine
		///folder (receive time). ..." With images received at different times, the first and the last.</summary>
		public static string Quarantined(int count,DateTime receivedFirst,DateTime receivedLast) {
			return count.ToString(CultureInfo.InvariantCulture)+" image(s) received after the series stopped were saved to the capture quarantine folder ("
				+FormatTimes(receivedFirst,receivedLast)+"). Do not re-expose. Ask an administrator to recover them (see the runbook).";
		}

		///<summary>Images received after the series stopped that could not be written to the quarantine. hasJournalCopy: each of them has a
		///journal copy, which is then the only one.</summary>
		public static string QuarantineFailed(int count,DateTime receivedFirst,DateTime receivedLast,string error,bool hasJournalCopy) {
			return count.ToString(CultureInfo.InvariantCulture)+" image(s) received after the series stopped could not be saved to the capture"
				+" quarantine folder ("+FormatTimes(receivedFirst,receivedLast)+"): "+(error??"").Trim().TrimEnd('.')+"."
				+(hasJournalCopy ? " A copy is in the capture journal folder." : "")
				+" Do not re-expose. Ask an administrator to recover them (see the runbook).";
		}

		///<summary>The status window's line about the quarantine (design 7.12 "Visibility"), or null when it is empty. After
		///UnassignedWarningDays it asks for recovery.</summary>
		public static string UnassignedCaptures(int count,DateTime? oldest,DateTime now) {
			if(count<=0) {
				return null;
			}
			string text=count.ToString(CultureInfo.InvariantCulture)+" unassigned capture(s) on this computer.";
			if(oldest!=null && (now-oldest.Value).TotalDays>UnassignedWarningDays) {
				text+=" The oldest is from "+oldest.Value.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)
					+". Ask an administrator to recover them now (see the runbook).";
			}
			return text;
		}

		private static string FormatTimes(DateTime first,DateTime last) {
			string textFirst=FormatTime(first);
			string textLast=FormatTime(last);
			return textFirst==textLast ? textFirst : textFirst+" to "+textLast;
		}

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
