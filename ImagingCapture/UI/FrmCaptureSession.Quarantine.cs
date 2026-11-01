// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using ImagingCapture.Recovery;
using ImagingCapture.Session;

namespace ImagingCapture.UI {
	///<summary>The session window's links to the quarantine window (design 7.1, 7.12): "N unassigned captures on this computer" opens the
	///unassigned captures, and "Recover recent captures" opens the journal copies of the session's patient that have no document yet. Both open
	///modal to this window, only while the source is not enabled (CaptureSessionViewModel.CanOpenCaptureLinks); each action in them checks its
	///own permission. What they filed into this session's mount updates the mini-map and the target (CaptureSessionController.OnCaptureFiled).</summary>
	public partial class FrmCaptureSession {
		partial void CanShowUnassignedCapturesExt(ref bool canShow) {
			canShow=true;
		}

		partial void ShowUnassignedCapturesExt(CaptureSessionController controller,ref bool isShown) {
			isShown=ShowCaptureRecovery(controller,CaptureRecoveryView.Unassigned);
		}

		partial void CanShowRecoverCapturesExt(ref bool canShow) {
			canShow=true;
		}

		partial void ShowRecoverCapturesExt(CaptureSessionController controller,ref bool isShown) {
			isShown=ShowCaptureRecovery(controller,CaptureRecoveryView.Journal);
		}

		///<summary>Opens the quarantine window for the session's patient and hands its filings to the controller. No message box: a failure goes
		///to the session's log.</summary>
		private static bool ShowCaptureRecovery(CaptureSessionController controller,CaptureRecoveryView view) {
			if(controller==null) {
				return false;
			}
			try {
				CaptureRecoveryModel model=FrmCaptureQuarantine.ShowModal(new CaptureRecoveryContext { View=view,SessionPatient=controller.Patient });
				foreach(CaptureFilingResult result in model.ListResults) {
					controller.OnCaptureFiled(result);
					controller.LogFromWindow(result.Action+" from "+(view==CaptureRecoveryView.Unassigned ? "unassigned captures" : "recent captures")+": "
						+(result.Document==null ? "" : "DocNum "+result.Document.DocNum+" for PatNum "+result.PatNum+", ")+result.Entry?.FileName+".");
				}
			}
			catch(Exception ex) {
				controller.LogFromWindow("The unassigned captures window failed: "+ex.Message);
			}
			return true;
		}
	}
}
