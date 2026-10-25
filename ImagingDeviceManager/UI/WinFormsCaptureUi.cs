// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ImagingDeviceManager.UI {
	///<summary>The production ICaptureUi: small WinForms windows on the calling (UI) thread. Their messages are dispatched by whichever
	///loop runs on that thread, the acquisition's own message loop included.</summary>
	internal sealed class WinFormsCaptureUi:ICaptureUi {
		private readonly HashSet<IntPtr> _hashSetWindows=new HashSet<IntPtr>();
		private int _countNotices;

		public void ShowNotice(string text) {
			try {
				CaptureNoticeWindow window=new CaptureNoticeWindow(text);
				window.PlaceOnScreen(_countNotices++);
				Track(window.Handle,window);
				window.Show();
			}
			catch(Exception ex) {
				Trace.WriteLine("ImagingDeviceManager: a capture notice could not be shown ("+ex.Message+"): "+text);
			}
		}

		public bool IsLibraryWindow(IntPtr hwnd) {
			return hwnd!=IntPtr.Zero && _hashSetWindows.Contains(hwnd);
		}

		private void Track(IntPtr hwnd,System.Windows.Forms.Form form) {
			_hashSetWindows.Add(hwnd);
			form.FormClosed+=(sender,e) => _hashSetWindows.Remove(hwnd);
		}
	}
}
