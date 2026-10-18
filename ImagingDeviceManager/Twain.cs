// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Runtime.InteropServices;

namespace ImagingDeviceManager {
	///<summary>Helpers kept for existing callers.</summary>
	public static class Twain {
		///<summary>Formerly activated the commercial EZTwain license. Scanning no longer needs a license, so this does nothing and
		///always succeeds. Kept so existing callers compile unchanged.</summary>
		public static bool ActivateEZTwain() {
			return true;
		}

		///<summary>Deletes a GDI object, such as the HBITMAP returned by EZTwain.DIB_ToDibSection.</summary>
		[DllImport("gdi32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool DeleteObject(IntPtr hObject);
	}
}
