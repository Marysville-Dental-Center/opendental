// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using ImagingDeviceManager.Journal;

namespace ImagingDeviceManager.Capture {
	///<summary>Capture\capture-log.txt for callers outside this library: the capture session window writes its own decisions there (the bit
	///depth it chose and why, quarantine moves, refusals), next to the lines of its TwainCaptureSession worker. Writing never throws.</summary>
	public static class CaptureLogFile {
		private static readonly CaptureLog _captureLog=new CaptureLog(CaptureFolders.CreateDefault(),() => DateTime.Now);

		///<summary>Appends one line, with the local time and the Windows user. Any thread.</summary>
		public static void Write(string text) {
			try {
				_captureLog.Write(text??"");
			}
			catch {
				//CaptureLog already never fails an acquisition; this guards the static construction too.
			}
		}
	}
}
