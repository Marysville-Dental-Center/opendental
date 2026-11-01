// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using ImagingDeviceManager.Capture;
using OpenDentBusiness;

namespace ImagingCapture.Session {
	///<summary>What the session window (FrmCaptureSession, through CaptureSessionViewModel) needs from the controller besides its commands: the
	///controller's own clock, so that key timestamps and the window's timers use one clock, a log line from the window, a fresh unassigned
	///captures count after the quarantine window closed, and whether the user has Setup. UI thread only, like the rest of the controller.</summary>
	public sealed partial class CaptureSessionController {
		private bool? _isSetupAuthorized;

		///<summary>The user has the Setup permission (ICaptureSessionStore.IsAuthorized, which shows no message; checked once per session): the
		///window shows "Always rotate this slot" only then (design 7.3 step 5). AlwaysRotateSlot checks it again when pressed.</summary>
		public bool IsSetupAuthorized {
			get {
				if(_isSetupAuthorized==null) {
					try {
						_isSetupAuthorized=_services.Store!=null && _services.Store.IsAuthorized(EnumPermType.Setup);
					}
					catch(Exception ex) {
						Log("The Setup permission could not be checked: "+ex.Message);
						_isSetupAuthorized=false;
					}
				}
				return _isSetupAuthorized.Value;
			}
		}

		///<summary>The controller's monotonic milliseconds (ICaptureClock.NowMs): the timestamps of OnKeyDown and OnKeyUp, and the window's timers.</summary>
		public long NowMs => _clock.NowMs;

		///<summary>The controller's local time (ICaptureClock.Now), for the banner's age.</summary>
		public DateTime Now => _clock.Now;

		///<summary>A line from the session window (a key binding that could not be used, a hotkey that could not be registered, an unknown sound
		///name): into the session's log, the Logged event and Capture\capture-log.txt.</summary>
		public void LogFromWindow(string text) {
			Log("Window: "+(text??""));
		}

		///<summary>Reads the unassigned-captures count again (after the quarantine window closed). Raises Changed when the text changed.</summary>
		public void RefreshUnassignedText() {
			CaptureUnassignedInfo info=CaptureSessionLauncher.GetUnassignedInfo(_services);
			if(info.Text==UnassignedText && info.IsOverdue==IsUnassignedOverdue) {
				return;
			}
			UnassignedText=info.Text;
			IsUnassignedOverdue=info.IsOverdue;
			NotifyChanged();
		}
	}
}
