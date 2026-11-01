// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using ImagingCapture.Save;
using ImagingDeviceManager.Capture;
using OpenDentBusiness;

namespace ImagingCapture.Session {
	///<summary>The capture session's security-log entries (design 7.13, 10.7), written through ICaptureSessionStore on the UI thread, every one
	///under the bound patient except the Setup entry of "Always rotate this slot":
	///- one ImageCreate session summary when the session finishes (device, N images, R retakes, the quarantined count, the provider), once it
	///  was started;
	///- an ImageCreate "Capture event:" entry for each event that may stand for an exposure with no stored image: MSG_CLOSEDSREQ after an enable
	///  without an image, a transfer failure, a disable started without safe disable (TwainCaptureEnded.WasDisableStartedUnsafe, or the forced
	///  close itself), a second consecutive MSG_DISABLEDS after the wait bound, extra images, and every quarantine move (also a failed one);
	///- the Retake's ImageEdit entry (CaptureSessionController.Retake.cs).
	///An entry that cannot be written never stops the session: the failure and the text go to the capture log instead.</summary>
	public sealed partial class CaptureSessionController {
		#region Methods - audit
		///<summary>One security-log entry (SecurityLogs.MakeLogEntry). Never throws: false when it could not be written (the capture log has the
		///text then).</summary>
		private bool WriteAudit(EnumPermType permType,long patNum,string logText,long fKey) {
			ICaptureSessionStore store=_services.Store;
			try {
				if(store==null) {
					throw new InvalidOperationException("No store.");
				}
				store.MakeLogEntry(permType,patNum,logText,fKey);
				Log("Security log ("+permType+"): "+logText);
				return true;
			}
			catch(Exception ex) {
				Log("The security-log entry ("+permType+") could not be written: "+ex.Message+" Its text: "+logText);
				return false;
			}
		}

		///<summary>An ImageCreate "Capture event:" entry under the bound patient (design 7.13).</summary>
		private void WriteCaptureEvent(string what,string targetLabel) {
			WriteAudit(EnumPermType.ImageCreate,PatNum,CaptureAuditText.GetCaptureEventText(what,_setup.ImagingDevice.Description,
				_setup.ImagingDevice.ImagingDeviceNum,targetLabel,SessionGuid),0);
		}

		///<summary>The end of an enable of this session (running or not, also after the finish): its capture events.</summary>
		private void WriteCaptureEvents(TwainCaptureEnded ended,CaptureEnableRecord record) {
			string targetLabel=GetRecordTargetLabel(record);
			if(ended.Reason==TwainCaptureEndReason.CloseRequested && ended.ImageCount==0) {
				WriteCaptureEvent(CaptureAuditText.GetClosedWithoutImageWhat(ended.ConditionCode,ended.ConditionText),targetLabel);
			}
			if(ended.Reason==TwainCaptureEndReason.TransferFailed || ended.TransferFailureCount>0) {
				ushort? conditionCode=ended.Reason==TwainCaptureEndReason.TransferFailed ? ended.ConditionCode : null;
				WriteCaptureEvent(CaptureAuditText.GetTransferFailedWhat(Math.Max(1,ended.TransferFailureCount),conditionCode),targetLabel);
			}
			if(ended.WasDisableStartedUnsafe && (record==null || !record.IsUnsafeStopLogged)) {
				if(record!=null) {
					record.IsUnsafeStopLogged=true;
				}
				WriteCaptureEvent(CaptureAuditText.GetDisableStartedUnsafeWhat(ended.StopReason),targetLabel);
			}
			if(ended.WasSecondDisableAfterWait) {
				WriteCaptureEvent(CaptureAuditText.GetSecondDisableAfterWaitWhat(),targetLabel);
			}
			if(ended.ExtraImageCount>0) {
				WriteCaptureEvent(CaptureAuditText.GetExtraImagesWhat(ended.ExtraImageCount),targetLabel);
			}
		}

		///<summary>Every quarantine move, path null when it failed (design 7.13).</summary>
		private void WriteQuarantineEvent(AcquiredImage image,string reason,string path) {
			string what=path==null ? CaptureAuditText.GetQuarantineFailedWhat(reason,image?.JournalPath)
				: CaptureAuditText.GetQuarantinedWhat(reason,path);
			WriteCaptureEvent(what,image?.TargetLabel);
		}

		///<summary>Finish has begun with the source still enabled and no safe disable in effect: the forced close (design 7.3 step 7, 10.6) is a
		///stop the fork starts, so it is a capture event; the enable's own Ended then writes no second one for it.</summary>
		private void WriteForcedCloseEvent(CaptureSessionEndReason endReason) {
			CaptureEnableRecord record=_enableRunning;
			if(endReason!=CaptureSessionEndReason.Closed || record==null || record.IsSafeDisableInEffect || record.IsUnsafeStopLogged) {
				return;
			}
			record.IsUnsafeStopLogged=true;
			WriteCaptureEvent(CaptureAuditText.GetForcedCloseWhat(),GetRecordTargetLabel(record));
		}

		///<summary>The session's ImageCreate summary (design 10.7), once the operator started it.</summary>
		private void WriteSessionSummary(CaptureSessionSummary summary) {
			if(summary==null || (!_isStarted && summary.CountImages==0 && summary.CountQuarantined==0)) {
				return;
			}
			WriteAudit(EnumPermType.ImageCreate,PatNum,CaptureAuditText.GetSessionSummaryText(summary),0);
		}

		///<summary>An image of the session was quarantined after its summary entry was written (the closing source delivered it): an ImageCreate
		///entry with the quarantined count as it is now (design 10.7).</summary>
		private void WriteSessionSummaryUpdate(CaptureSessionSummary summary) {
			if(summary==null || !_isStarted) {
				return;
			}
			WriteAudit(EnumPermType.ImageCreate,PatNum,CaptureAuditText.GetSessionSummaryUpdateText(summary),0);
		}

		private string GetRecordTargetLabel(CaptureEnableRecord record) {
			if(record==null) {
				return "";
			}
			return GetRadiographTarget(record.MountItemTarget,record.ToothNumbers).GetLabel();
		}
		#endregion Methods - audit
	}
}
