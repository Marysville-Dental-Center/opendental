// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace ImagingCapture.Session {
	///<summary>The extension points of CaptureSessionController, implemented for the session rules of CAP-4 implementer A2. Each one only
	///dispatches to the rule's own file: Retake and Rotate (CaptureSessionController.Retake.cs).</summary>
	public sealed partial class CaptureSessionController {
		partial void ExecuteExt(CaptureSessionAction action,ref bool isHandled) {
			switch(action) {
				case CaptureSessionAction.Retake:
					isHandled=Retake();
					break;
				case CaptureSessionAction.Rotate:
					isHandled=Rotate();
					break;
			}
		}

		partial void CanExecuteExt(CaptureSessionAction action,ref bool canExecute) {
			switch(action) {
				case CaptureSessionAction.Retake:
					canExecute=CanRetake();
					break;
				case CaptureSessionAction.Rotate:
					canExecute=Phase!=CaptureSessionPhase.Finished && LastSavedDocument!=null;
					break;
			}
		}
	}
}
