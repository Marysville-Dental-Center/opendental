// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using ImagingCapture.Session;

namespace ImagingCapture.UI {
	///<summary>ICaptureSessionWindowRunner with the real window: FrmCaptureSession, shown with ShowDialog on the calling UI thread. The window
	///calls the controller's Begin when it has loaded and OnClosing when it closes, and closes itself after the summary. Its owner is the window
	///that is active when it opens (FrmODBase.ShowDialog), which is the main window right after FrmMountAndAcquire closes; owner is not used.
	///The hotkeys are released again after ShowDialog returns or throws.</summary>
	public sealed class CaptureSessionWindowRunner:ICaptureSessionWindowRunner {
		public void ShowModal(CaptureSessionController controller,object owner) {
			if(controller==null) {
				throw new ArgumentNullException(nameof(controller));
			}
			FrmCaptureSession frmCaptureSession=new FrmCaptureSession(controller);
			try {
				frmCaptureSession.ShowDialog();
			}
			finally {
				frmCaptureSession.ReleaseHotkeys();
			}
		}
	}
}
