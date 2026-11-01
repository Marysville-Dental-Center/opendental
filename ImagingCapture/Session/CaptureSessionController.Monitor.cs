// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Threading;
using CodeBase;
using ImagingCapture.UI;
using ImagingCapture.Video;
using ImagingDeviceManager.Capture;
using OpenPmsDentalBusiness;

namespace ImagingCapture.Session {
	///<summary>The patient-change monitor, idle disable and the stops the controller starts on its own (design 7.3 step 7, 7.6, 7.12, 10.6).
	///
	///Patient change: ODEvent Patient with a long Tag that differs from the bound PatNum, 0 included, or a Patient Tag whose PatNum differs
	///(PatientBinding, as FormVideo uses it). ODEvent can fire on any thread: the handler only counts the change (Interlocked) and posts the
	///handling to the UI thread, and an image the UI thread routes while a change is counted but not yet handled handles it first, so placing
	///stops at once. Then everything that arrives goes to the quarantine, tagged with the bound patient; the red bar shows; with safe disable in
	///effect the running enable is disabled at once (also when that becomes known only from a later status), and without it the session starts
	///no disable and offers End. Only a deliberate re-enable, which checks getPatNumCur(), places images again.
	///
	///Idle: after [Session] IdleDisableMinutes with no exposure, the running enable is disabled, only with safe disable in effect for it; the
	///re-enable needs the deliberate start.
	///
	///The controller starts no disable of its own without safe disable in effect; the one exception is a forced close (OnClosing: BeginClose,
	///in the safe order, on the worker).</summary>
	public sealed partial class CaptureSessionController {
		#region Fields - monitor
		private readonly PatientBinding _patientBinding;
		private ICapturePatientEvents _patientEvents;
		private ODEventHandler _odEventHandler;
		///<summary>Patient changes ODEvent reported (any thread, Interlocked), and how many of them the UI thread has handled.</summary>
		private long _countPatientChangesSignalled;
		private long _countPatientChangesHandled;
		///<summary>The PatNum the main window changed to last (0: none), for the log.</summary>
		private long _patNumChangedTo;
		///<summary>Since when the running enable waits for an exposure (ICaptureClock.NowMs), or null.</summary>
		private long? _msIdleSince;
		#endregion Fields - monitor

		#region Properties - monitor
		///<summary>[Session] IdleDisableMinutes, 0 when idle disable is off.</summary>
		public int IdleDisableMinutes => Math.Max(0,_setup.SessionConfig?.IdleDisableMinutes??0);
		#endregion Properties - monitor

		#region Methods - patient-change monitor
		private void SubscribePatientEvents() {
			if(_patientEvents!=null) {
				return;
			}
			ICapturePatientEvents patientEvents=_services.PatientEvents??new ODEventCapturePatientEvents();
			_odEventHandler=OnODEventFired;
			try {
				patientEvents.Subscribe(_odEventHandler);
				_patientEvents=patientEvents;
			}
			catch(Exception ex) {
				Log("The patient-change monitor could not start: "+ex.Message);
			}
		}

		private void UnsubscribePatientEvents() {
			ICapturePatientEvents patientEvents=_patientEvents;
			if(patientEvents==null) {
				return;
			}
			_patientEvents=null;
			try {
				patientEvents.Unsubscribe(_odEventHandler);
			}
			catch(Exception ex) {
				Log("The patient-change monitor could not be stopped: "+ex.Message);
			}
		}

		///<summary>ODEvent.Fired, on any thread. Counts a patient change and posts its handling; never touches the controller's state otherwise.</summary>
		private void OnODEventFired(ODEventArgs odEventArgs) {
			if(!_patientBinding.IsPatientChange(odEventArgs)) {
				return;
			}
			long patNumTo=0;
			if(odEventArgs.Tag is long) {
				patNumTo=(long)odEventArgs.Tag;
			}
			else if(odEventArgs.Tag is Patient) {
				patNumTo=((Patient)odEventArgs.Tag).PatNum;
			}
			Interlocked.Exchange(ref _patNumChangedTo,patNumTo);
			Interlocked.Increment(ref _countPatientChangesSignalled);
			try {
				_dispatcher.Post(HandlePatientChange);
			}
			catch(Exception ex) {
				ex.DoNothing();//the window is closing; an image routed later still sees the count
			}
		}

		///<summary>A patient change was counted that the UI thread has not handled yet.</summary>
		private bool IsPatientChangePending => Interlocked.Read(ref _countPatientChangesSignalled)!=_countPatientChangesHandled;

		///<summary>UI thread: the patient changed in the main window (design 7.6).</summary>
		private void HandlePatientChange() {
			long countSignalled=Interlocked.Read(ref _countPatientChangesSignalled);
			if(countSignalled==_countPatientChangesHandled) {
				return;
			}
			_countPatientChangesHandled=countSignalled;
			if(Phase==CaptureSessionPhase.Finished) {
				return;
			}
			long patNumTo=Interlocked.Read(ref _patNumChangedTo);
			StopPlacement("The patient changed in the main window (to PatNum "+patNumTo+") during the capture session for "+PatientName
				+" (PatNum "+PatNum+").");
			PatientChangedText=CaptureSessionText.GetPatientChangedText(PatientName);
			PlaySound(CaptureSound.Error);
			DeclineAllRedelivered("not placed: the patient changed in the main window");
			CaptureEnableRecord record=_enableRunning;
			if(record!=null && (Phase==CaptureSessionPhase.Enabled || Phase==CaptureSessionPhase.Stopping)) {
				if(Phase==CaptureSessionPhase.Enabled && record.StopRequested==TwainCaptureStopReason.None && record.IsSafeDisableInEffect) {
					StopEnableSafely(record,TwainCaptureStopReason.PatientChanged);
					NoticeText=CaptureSessionText.PatientChangedDisabling;
					Log("Patient changed: the source is disabled (safe disable is in effect).");
				}
				else {
					//Design 7.6, 10.6: without safe disable the fork starts no disable of its own; it offers End.
					NoticeText=CaptureSessionText.GetPatientChangedOfferEnd(PatientName);
					Log("Patient changed: the source stays enabled (safe disable is not in effect); End is offered.");
				}
			}
			NotifyChanged();
		}

		///<summary>A deliberate start or re-enable passed the patient check: the changes counted until now are over (the main window shows the bound
		///patient again), and images are placed again.</summary>
		private void ResumePlacement() {
			_countPatientChangesHandled=Interlocked.Read(ref _countPatientChangesSignalled);
			if(!_isPlacementStopped) {
				return;
			}
			_isPlacementStopped=false;
			_placementStoppedReason="";
			Log("Placement resumed: the deliberate re-enable for "+PatientName+" passed the patient check.");
		}

		///<summary>A status of the running enable: a disable that waited until safe disable was known to be in effect (a patient change, or a target
		///change made before the status said so) now goes ahead.</summary>
		private void TryDeferredSafeStop(CaptureEnableRecord record) {
			if(Phase!=CaptureSessionPhase.Enabled || record!=_enableRunning || record.StopRequested!=TwainCaptureStopReason.None
				|| !record.IsSafeDisableInEffect)
			{
				return;
			}
			if(_isPlacementStopped) {
				StopEnableSafely(record,TwainCaptureStopReason.PatientChanged);
				NoticeText=CaptureSessionText.PatientChangedDisabling;
				Log("Patient changed: the source is disabled now that safe disable is known to be in effect.");
				return;
			}
			if(_idxTargetChosen!=null && _setup.Options.SessionMode==TwainSessionMode.PerSlot && record.CountSaved==0 && !record.IsRedelivery) {
				StopEnableSafely(record,TwainCaptureStopReason.TargetChanged);
				Log("The target changed: the source is disabled now that safe disable is known to be in effect.");
			}
		}
		#endregion Methods - patient-change monitor

		#region Methods - idle disable
		///<summary>The running enable's status: the idle clock runs while it waits for an exposure.</summary>
		private void UpdateIdleClock(TwainCaptureStatus status) {
			switch(status.State) {
				case TwainCaptureState.WaitingForImage:
					_msIdleSince=_clock.NowMs;
					break;
				case TwainCaptureState.Enabling:
				case TwainCaptureState.Transferring:
				case TwainCaptureState.Stopping:
				case TwainCaptureState.WaitingForSource:
					_msIdleSince=null;
					break;
			}
		}

		///<summary>OnTick: design 7.6 idle disable, only with safe disable in effect for the running enable.</summary>
		private void CheckIdle(long nowMs) {
			int minutes=IdleDisableMinutes;
			CaptureEnableRecord record=_enableRunning;
			if(minutes==0 || record==null || Phase!=CaptureSessionPhase.Enabled || record.StopRequested!=TwainCaptureStopReason.None
				|| !record.IsSafeDisableInEffect || _msIdleSince==null)
			{
				return;
			}
			if(nowMs-_msIdleSince.Value<minutes*60000L) {
				return;
			}
			_msIdleSince=null;
			StopEnableSafely(record,TwainCaptureStopReason.Idle);
			Log("No exposure for "+minutes+" minutes: the source is disabled (safe disable is in effect).");
			NotifyChanged();
		}

		///<summary>The end of an idle disable: the banner waits for the deliberate re-enable (design 7.6, 7.12 "Idle").</summary>
		private bool HandleIdleEnded(TwainCaptureEnded ended,CaptureEnableRecord record) {
			if(record==null || record.StopRequested!=TwainCaptureStopReason.Idle || ended.Reason!=TwainCaptureEndReason.Stopped) {
				return false;
			}
			string textHold=ended.SourceMayHoldImages ? " "+CaptureEndText.MayStillHoldImages : "";
			PlaySound(CaptureSound.Disabled);
			ShowReady(GetReEnableKind(),CaptureIndicatorState.Disabled,CaptureSessionText.GetIdleText(IdleDisableMinutes)+textHold);
			return true;
		}
		#endregion Methods - idle disable
	}
}
