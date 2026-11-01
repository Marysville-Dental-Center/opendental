// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using ImagingCapture.Metadata;
using ImagingCapture.Save;
using ImagingCapture.UI;
using ImagingDeviceManager.Capture;
using OpenDentBusiness;

namespace ImagingCapture.Session {
	///<summary>Redelivery (W15, design 7.6, 7.12): when the device profile names a RedeliverCap and an enable ended with a failed transfer, or
	///with the source possibly still holding images (a stop after a refused disable, for example), the session offers "Request undelivered
	///images again". The request is a deliberate start like any re-enable (the 2 s rule, getPatNumCur()) and asks TwainCaptureSession for a
	///redelivery enable for the same target; the slot did not advance.
	///
	///The source has no patient context, so a redelivered image is never placed on arrival: it waits, with its acquiredAt and captureId shown,
	///until the operator confirms it (on screen only, 2 s after it is shown, with the main window still on the bound patient) or declines it.
	///Confirmed, it is saved into the target as it is then; declined, it goes to the quarantine marked "patient unknown". While one waits,
	///nothing is enabled. A patient change and the end of the session put the waiting ones in the quarantine the same way.</summary>
	public sealed partial class CaptureSessionController {
		#region Fields - redelivery
		private bool _isRedeliveryOffered;
		private readonly List<CapturePendingRedelivered> _listRedeliveredPending=new List<CapturePendingRedelivered>();
		///<summary>When the first waiting image was shown (ICaptureClock.NowMs): Confirm is armed 2 s later.</summary>
		private long _msRedeliveredShown;
		private bool _canConfirmRedeliveredShown;
		private bool _canRequestRedeliveryShown;
		#endregion Fields - redelivery

		#region Properties - redelivery
		///<summary>Redelivered images wait for the operator's Confirm or Decline; nothing is enabled until they are handled.</summary>
		public bool HasRedeliveredPending => _listRedeliveredPending.Count>0;

		///<summary>"Request undelivered images again" is offered (design 7.12): the device profile names a RedeliverCap, the last enable left
		///images undelivered, the source is open and not enabled, and placement goes on.</summary>
		public bool IsRedeliveryOffered => _isRedeliveryOffered && _setup.Options.RedeliverCap!=null && Phase==CaptureSessionPhase.Ready && _isSessionOpen
			&& !_isPlacementStopped && !HasRedeliveredPending && StartKind!=CaptureStartKind.None;

		///<summary>The redelivery button may be pressed now: offered, and 2 s since the banner appeared (a deliberate start).</summary>
		public bool CanRequestRedelivery => IsRedeliveryOffered && IsBannerArmed(_clock.NowMs);

		///<summary>The button's text: "Request undelivered images again".</summary>
		public string RedeliveryText => CaptureSessionText.RequestRedelivery;

		///<summary>The first redelivered image that waits, or null.</summary>
		public CaptureRedeliveredView RedeliveredView {
			get {
				if(!HasRedeliveredPending) {
					return null;
				}
				CapturePendingRedelivered pending=_listRedeliveredPending[0];
				string targetText=GetRedeliveredTargetText();
				return new CaptureRedeliveredView {
					Image=pending.Image,
					AcquiredAtText=pending.AcquiredAtText,
					CaptureId=pending.CaptureId,
					TargetText=targetText,
					Text=CaptureSessionText.GetRedeliveredText(pending.AcquiredAtText,pending.CaptureId,targetText,PatientName),
					CountPending=_listRedeliveredPending.Count,
				};
			}
		}

		///<summary>Confirm may be pressed: an image waits and was shown 2 s ago, a target is free, placement goes on, and no normal enable runs.</summary>
		public bool CanConfirmRedelivered {
			get {
				if(!HasRedeliveredPending || Phase==CaptureSessionPhase.Finished || _isPlacementStopped) {
					return false;
				}
				if(_enableRunning!=null && !_enableRunning.IsRedelivery) {
					return false;
				}
				if(!IsSingleTarget && _mountSlots.IdxTarget==-1) {
					return false;
				}
				return _clock.NowMs-_msRedeliveredShown>=KeyIgnoreMs;
			}
		}

		public bool CanDeclineRedelivered => HasRedeliveredPending && Phase!=CaptureSessionPhase.Finished;
		#endregion Properties - redelivery

		#region Methods - redelivery
		///<summary>"Request undelivered images again" (design 7.12): a redelivery enable for the target, after the patient check. False when it is
		///not offered or not armed, or the enable was refused.</summary>
		public bool RequestRedelivery() {
			if(!CanRequestRedelivery) {
				Log("Redelivery request ignored: "+(IsRedeliveryOffered ? "the banner appeared less than 2 s ago." : "it is not offered now."));
				return false;
			}
			ClearStopWarning();
			RedStripText="";
			NoticeText="";
			IsDeviceOffline=false;
			_isPausedBySaveFailure=false;
			Log("The operator asked for the undelivered images again.");
			bool isPosted=PostEnable(true);
			if(isPosted) {
				SetIndicator(CaptureIndicatorState.Connecting,CaptureSessionText.RedeliveryStarting);
			}
			NotifyChanged();
			return isPosted;
		}

		///<summary>The operator confirms the first waiting redelivered image: it is saved for the bound patient into the target as it is now, the one
		///the panel shows (in single-target mode with the banner's tooth numbers as they are now).
		///Refused while the main window shows another patient.</summary>
		public bool ConfirmRedelivered() {
			if(!CanConfirmRedelivered) {
				return false;
			}
			long patNumCur=ReadPatNumCur();
			if(patNumCur!=PatNum) {
				Log("Redelivered image not placed: the main window shows patient "+patNumCur+", the session is for "+PatNum+".");
				PatientChangedText=CaptureSessionText.GetPatientChangedText(PatientName);
				PlaySound(CaptureSound.Error);
				NotifyChanged();
				return false;
			}
			CapturePendingRedelivered pending=_listRedeliveredPending[0];
			_listRedeliveredPending.RemoveAt(0);
			_msRedeliveredShown=_clock.NowMs;
			int idxTarget=-1;
			MountItem mountItemTarget=null;
			if(!IsSingleTarget) {
				idxTarget=_mountSlots.IdxTarget;
				mountItemTarget=_mountSlots.Target;
			}
			Log("The operator confirmed the redelivered image (acquired "+pending.AcquiredAtText+", capture ID "+pending.CaptureId+") for "
				+(IsSingleTarget ? "a single image" : MountPlacement.GetSlotLabel(mountItemTarget))+".");
			//Confirmed for the bound patient: should its save fail, the quarantine file is the bound patient's and may be assigned (design 7.12).
			SetImageSidecarField(pending.Image,CaptureSidecarKeys.RedeliveryConfirmed,"1");
			//Into the target the panel showed (GetRedeliveredTargetText): in single-target mode the banner's teeth now, which the operator may have
			//changed while the image waited, not the ones of the redelivery enable.
			SaveImage(pending.Image,pending.Record,idxTarget,mountItemTarget,IsSingleTarget ? _toothNumbersSingle : null);
			if(Phase==CaptureSessionPhase.Ready && !HasRedeliveredPending && !IsSingleTarget && _mountSlots.IdxTarget==-1) {
				Finish(CaptureSessionEndReason.MountFull);
				return true;
			}
			NotifyChanged();
			return true;
		}

		///<summary>The operator declines the first waiting redelivered image: it goes to the quarantine marked "patient unknown".</summary>
		public bool DeclineRedelivered() {
			if(!CanDeclineRedelivered) {
				return false;
			}
			CapturePendingRedelivered pending=_listRedeliveredPending[0];
			_listRedeliveredPending.RemoveAt(0);
			_msRedeliveredShown=_clock.NowMs;
			QuarantineRedelivered(pending,"declined by the operator");
			NotifyChanged();
			return true;
		}

		///<summary>OnImageReceivedExt: a redelivered image waits for the operator, or, once placement stopped, goes to the quarantine at once.
		///True when it was such an image.</summary>
		private bool HoldRedelivered(AcquiredImage image,CaptureEnableRecord record) {
			if(!image.IsRedelivery && !record.IsRedelivery) {
				return false;
			}
			CapturePendingRedelivered pending=new CapturePendingRedelivered(image,record);
			if(_isPlacementStopped) {
				QuarantineRedelivered(pending,"not placed: the patient changed in the main window");
				return true;
			}
			if(!HasRedeliveredPending) {
				_msRedeliveredShown=_clock.NowMs;
			}
			_listRedeliveredPending.Add(pending);
			Log("Redelivered image received (acquired "+pending.AcquiredAtText+", capture ID "+pending.CaptureId+"); it is placed only when the operator "
				+"confirms it.");
			return true;
		}

		///<summary>Every waiting redelivered image to the quarantine (a patient change, the end of the session).</summary>
		private void DeclineAllRedelivered(string why) {
			while(HasRedeliveredPending) {
				CapturePendingRedelivered pending=_listRedeliveredPending[0];
				_listRedeliveredPending.RemoveAt(0);
				QuarantineRedelivered(pending,why);
			}
		}

		private void QuarantineRedelivered(CapturePendingRedelivered pending,string why) {
			QuarantineImage(_session,pending.Image,pending.Record,
				CaptureSessionText.GetRedeliveredQuarantineReason(why,pending.AcquiredAtText,pending.CaptureId),true);
		}

		///<summary>The end of the running enable: the offer follows whether images may have been left with the source. A redelivery enable that
		///ended clears it unless it left images again.</summary>
		private void UpdateRedeliveryOffer(TwainCaptureEnded ended,CaptureEnableRecord record) {
			bool isUndelivered=ended.Reason==TwainCaptureEndReason.TransferFailed || ended.TransferFailureCount>0 || ended.SourceMayHoldImages;
			if(record!=null && record.IsRedelivery) {
				_isRedeliveryOffered=isUndelivered;
			}
			else if(isUndelivered) {
				_isRedeliveryOffered=true;
			}
			if(isUndelivered && _setup.Options.RedeliverCap!=null) {
				Log("Images may be undelivered: \""+CaptureSessionText.RequestRedelivery+"\" is offered.");
			}
		}

		///<summary>The end of a redelivery enable: never an automatic enable. Completed or stopped, the banner waits (with the redelivered images to
		///check first); RedeliveryUnavailable shows why. Every other end is shown as for any enable (no automatic enable either).</summary>
		private bool HandleRedeliveryEnded(TwainCaptureEnded ended,CaptureEnableRecord record) {
			if(record==null || !record.IsRedelivery) {
				return false;
			}
			string textEnded=CaptureEndText.GetEndedText(ended);
			switch(ended.Reason) {
				case TwainCaptureEndReason.Completed:
				case TwainCaptureEndReason.Stopped:
					if(ended.Reason==TwainCaptureEndReason.Stopped && ended.StopReason==TwainCaptureStopReason.Close) {
						return false;
					}
					if(HasRedeliveredPending) {
						ShowReady(GetReEnableKind(),CaptureIndicatorState.Ready,CaptureSessionText.RedeliveredConfirm);
					}
					else if(ended.ImageCount==0) {
						PlaySound(CaptureSound.NotReceived);
						ShowReady(GetReEnableKind(),CaptureIndicatorState.ImageNotReceived,CaptureSessionText.RedeliveryNothing);
					}
					else if(IndicatorState==CaptureIndicatorState.Saved) {
						ShowReady(GetReEnableKind(),CaptureIndicatorState.Saved,_indicatorText+". "+CaptureSessionText.ReadyReEnable);
					}
					else {
						ShowReady(GetReEnableKind(),CaptureIndicatorState.Ready,CaptureSessionText.ReadyReEnable);
					}
					return true;
				case TwainCaptureEndReason.RedeliveryUnavailable:
					PlaySound(CaptureSound.Error);
					ShowReady(GetReEnableKind(),CaptureIndicatorState.Error,textEnded);
					return true;
				default:
					return false;
			}
		}

		private string GetRedeliveredTargetText() {
			if(IsSingleTarget) {
				return CaptureTargetText.GetSingleText(_toothNumbersSingle);
			}
			MountItem mountItemTarget=_mountSlots.Target;
			return mountItemTarget==null ? "" : CaptureTargetText.GetSlotText(_mountSlots.ListMountItems,mountItemTarget);
		}

		///<summary>getPatNumCur(), 0 when it cannot be read.</summary>
		private long ReadPatNumCur() {
			try {
				return _setup.FuncGetPatNumCur==null ? 0 : _setup.FuncGetPatNumCur();
			}
			catch(Exception ex) {
				Log("The main window's patient could not be read: "+ex.Message);
				return 0;
			}
		}

		///<summary>OnTick: tell the window when Confirm or the redelivery button becomes armed.</summary>
		private void NotifyRedeliveryArming() {
			if(CanConfirmRedelivered!=_canConfirmRedeliveredShown || CanRequestRedelivery!=_canRequestRedeliveryShown) {
				NotifyChanged();
			}
		}
		#endregion Methods - redelivery

		///<summary>A redelivered image that waits for the operator, with what its metadata says.</summary>
		private sealed class CapturePendingRedelivered {
			public readonly AcquiredImage Image;
			public readonly CaptureEnableRecord Record;
			public readonly string AcquiredAtText;
			public readonly string CaptureId;

			public CapturePendingRedelivered(AcquiredImage image,CaptureEnableRecord record) {
				Image=image;
				Record=record;
				CaptureMetadata metadata=null;
				try {
					metadata=CaptureMetadataParser.Parse(image.MetadataJson);
				}
				catch {
					//no metadata: shown as unknown
				}
				string acquiredAt=(metadata?.AcquiredAtText??"").Trim();
				string captureId=(metadata?.CaptureId??"").Trim();
				AcquiredAtText=acquiredAt=="" ? "unknown" : acquiredAt;
				CaptureId=captureId=="" ? "unknown" : captureId;
			}
		}
	}
}
