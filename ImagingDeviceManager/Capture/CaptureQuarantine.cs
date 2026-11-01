// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using ImagingDeviceManager.Acquire;
using ImagingDeviceManager.Journal;

namespace ImagingDeviceManager.Capture {
	///<summary>The capture quarantine as the windows outside this library see it (design 7.12 "Visibility"): the capture session window,
	///Test capture and FormVideo show "N unassigned captures on this computer" when they open while the folder is not empty, and after 30
	///days the count turns into a warning.</summary>
	public static class CaptureQuarantine {
		///<summary>The line about %ProgramData%\OpenPMS-Dental\Imaging\Capture\quarantine, or null when it holds no image. Never throws, and never
		///creates the folder.</summary>
		public static string GetUnassignedText() {
			return GetUnassignedText(CaptureFolders.CreateDefault(),DateTime.Now);
		}

		internal static string GetUnassignedText(CaptureFolders folders,DateTime now) {
			try {
				DateTime? oldest;
				int count=QuarantineWriter.CountImages(folders,out oldest);
				return CaptureNoticeText.UnassignedCaptures(count,oldest,now);
			}
			catch {
				return null;
			}
		}
	}
}
