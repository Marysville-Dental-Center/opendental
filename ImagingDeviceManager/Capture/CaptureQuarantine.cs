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

		///<summary>The count, the oldest file and whether the count has turned into the warning (older than 30 days), for a window that shows the
		///line in a warning colour. Never throws, and never creates the folder.</summary>
		public static CaptureUnassignedInfo GetUnassignedInfo() {
			return GetUnassignedInfo(CaptureFolders.CreateDefault(),DateTime.Now);
		}

		internal static CaptureUnassignedInfo GetUnassignedInfo(CaptureFolders folders,DateTime now) {
			CaptureUnassignedInfo info=new CaptureUnassignedInfo();
			try {
				DateTime? oldest;
				info.Count=QuarantineWriter.CountImages(folders,out oldest);
				info.Oldest=oldest;
				info.IsOverdue=oldest!=null && (now-oldest.Value).TotalDays>CaptureNoticeText.UnassignedWarningDays;
				info.Text=CaptureNoticeText.UnassignedCaptures(info.Count,oldest,now);
			}
			catch {
			}
			return info;
		}
	}

	///<summary>The unassigned captures on this computer (design 7.12 "Visibility").</summary>
	public sealed class CaptureUnassignedInfo {
		///<summary>Image files under Capture\quarantine.</summary>
		public int Count { get; set; }
		///<summary>The last-write time of the oldest, or null.</summary>
		public DateTime? Oldest { get; set; }
		///<summary>The oldest is more than 30 days old: the line asks for recovery now.</summary>
		public bool IsOverdue { get; set; }
		///<summary>"N unassigned capture(s) on this computer." (with the warning when IsOverdue), or null when there are none.</summary>
		public string Text { get; set; }
	}
}
