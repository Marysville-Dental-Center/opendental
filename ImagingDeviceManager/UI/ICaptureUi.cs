// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace ImagingDeviceManager.UI {
	///<summary>The windows the library shows the operator around a device acquisition. Every member returns at once: nothing here may
	///run a modal loop, because it is called from inside the acquisition's message loop, where only that loop hands messages to the
	///source. Production uses WinFormsCaptureUi; tests record what would be shown.</summary>
	internal interface ICaptureUi {
		///<summary>Shows a non-modal, topmost notice that the operator dismisses. Never throws.</summary>
		void ShowNotice(string text);

		///<summary>True for a window this UI shows. Thread-modal acquisitions leave them enabled.</summary>
		bool IsLibraryWindow(IntPtr hwnd);
	}
}
