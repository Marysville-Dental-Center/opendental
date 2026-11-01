// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using ImagingDeviceManager.Capture;

namespace ImagingCapture.Session {
	///<summary>The extension points of CaptureSessionController, implemented for the session rules of CAP-4 implementer A2. Each one only
	///dispatches to the rule's own file: Retake and Rotate (CaptureSessionController.Retake.cs), the patient-change monitor, idle disable and the
	///forced close (CaptureSessionController.Monitor.cs), redelivery (CaptureSessionController.Redelivery.cs) and the security log
	///(CaptureSessionController.Audit.cs).</summary>
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

		partial void OnBeginExt() {
			SubscribePatientEvents();
		}

		partial void OnDeliberateStartExt() {
			ResumePlacement();
		}

		partial void OnImageReceivedExt(AcquiredImage image,CaptureEnableRecord record,ref bool isHandled) {
			if(IsPatientChangePending) {
				HandlePatientChange();//design 7.6: placing stops at once, also before the posted handling has run
			}
			_msIdleSince=_clock.NowMs;
			isHandled=HoldRedelivered(image,record);
		}

		partial void OnEnablePostedExt(CaptureEnableRecord record) {
			_msIdleSince=null;
			if(!record.IsRedelivery) {
				_isRedeliveryOffered=false;//the source offers only the undelivered images of its most recent session
			}
		}

		partial void OnStatusExt(TwainCaptureStatus status,CaptureEnableRecord record) {
			UpdateIdleClock(status);
			TryDeferredSafeStop(record);
		}

		partial void OnTickExt(long nowMs) {
			CheckIdle(nowMs);
			NotifyRedeliveryArming();
		}

		partial void OnImageQuarantinedExt(AcquiredImage image,string reason,string path) {
			WriteQuarantineEvent(image,reason,path);
			if(Phase==CaptureSessionPhase.Finished && path!=null && Summary!=null) {
				WriteSessionSummaryUpdate(Summary);//the summary entry was written at the finish, before this image arrived
			}
		}

		partial void OnEnableEndedExt(TwainCaptureEnded ended,CaptureEnableRecord record,bool isRunning,ref bool isHandled) {
			WriteCaptureEvents(ended,record);
			if(!isRunning || Phase==CaptureSessionPhase.Finished) {
				return;
			}
			UpdateRedeliveryOffer(ended,record);
			if(_isEndRequested) {
				return;
			}
			isHandled=HandleRedeliveryEnded(ended,record) || HandleIdleEnded(ended,record);
		}

		partial void OnFinishStartingExt(CaptureSessionEndReason endReason) {
			UnsubscribePatientEvents();
			WriteForcedCloseEvent(endReason);
			DeclineAllRedelivered("not placed: the capture session ended first");
		}

		partial void OnFinishingExt(CaptureSessionSummary summary) {
			WriteSessionSummary(summary);
		}
	}
}
