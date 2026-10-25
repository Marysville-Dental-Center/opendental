// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using ImagingDeviceManager.Acquire;

namespace ImagingDeviceManager.UI {
	///<summary>What the status and Cancel window shows and does (design 7.1 "Status and Cancel window"), without the window, so that it can
	///be tested. Cancel asks the acquisition to stop in the safe order. When the source's disable is not safe, the first Cancel only
	///shows a warning, in the window itself (a message box would run a modal loop that hands nothing to the source), and a second
	///Cancel stops. While the session waits for the source after a refused disable, the window says so and Cancel is off. All members
	///run on the acquisition's thread.</summary>
	internal sealed class AcquireStatusController {
		public const string CancelButtonText="Cancel";
		public const string CancelAnywayButtonText="Cancel anyway";
		public const string CancellingButtonText="Cancelling";
		public const string BackButtonText="Keep waiting";

		private enum StatusState {
			Open,
			Warning,
			Cancelling,
		}

		private readonly Func<bool> _funcIsSafeDisableInEffect;
		private readonly bool _isBatch;
		private readonly Action _actionCancel;
		private StatusState _state=StatusState.Open;
		private string _warningText;
		private bool _isWaiting;

		///<summary>funcIsSafeDisableInEffect is asked when Cancel is clicked, because the source accepts or refuses the safe-disable
		///capability only just before the enable. unassignedText is a second line about the capture quarantine, or null.</summary>
		public AcquireStatusController(Func<bool> funcIsSafeDisableInEffect,bool isBatch,Action actionCancel,string unassignedText) {
			_funcIsSafeDisableInEffect=funcIsSafeDisableInEffect??(() => false);
			_isBatch=isBatch;
			_actionCancel=actionCancel??(() => { });
			UnassignedText=string.IsNullOrWhiteSpace(unassignedText) ? null : unassignedText;
		}

		///<summary>Raised after anything shown changed.</summary>
		public event Action Changed;

		public string MessageText {
			get {
				if(_isWaiting) {
					return CaptureNoticeText.Waiting;
				}
				if(_state==StatusState.Warning) {
					return _warningText;
				}
				return CaptureNoticeText.StatusOpen;
			}
		}

		///<summary>The capture quarantine line, or null.</summary>
		public string UnassignedText { get; }

		public string CancelText {
			get {
				switch(_state) {
					case StatusState.Warning:
						return CancelAnywayButtonText;
					case StatusState.Cancelling:
						return CancellingButtonText;
					default:
						return CancelButtonText;
				}
			}
		}

		public bool IsCancelEnabled => !_isWaiting && _state!=StatusState.Cancelling;

		///<summary>"Keep waiting", shown with the warning: back to the open state without cancelling.</summary>
		public bool IsBackVisible => _state==StatusState.Warning && !_isWaiting;

		public bool IsCancelRequested => _state==StatusState.Cancelling;

		public bool IsWarningShown => _state==StatusState.Warning;

		public void ClickCancel() {
			if(!IsCancelEnabled) {
				return;
			}
			if(_state==StatusState.Open) {
				string warning=CaptureNoticeText.GetCancelWarning(_funcIsSafeDisableInEffect(),_isBatch);
				if(warning!=null) {
					_warningText=warning;
					_state=StatusState.Warning;
					RaiseChanged();
					return;
				}
			}
			_state=StatusState.Cancelling;
			RaiseChanged();
			_actionCancel();
		}

		public void ClickBack() {
			if(_state!=StatusState.Warning) {
				return;
			}
			_state=StatusState.Open;
			RaiseChanged();
		}

		///<summary>The session started (true) or ended (false) the wait for the source after a refused disable.</summary>
		public void SetWaiting(bool isWaiting) {
			_isWaiting=isWaiting;
			RaiseChanged();
		}

		private void RaiseChanged() {
			try {
				Changed?.Invoke();
			}
			catch {
				//A window that cannot redraw must not stop the acquisition.
			}
		}
	}
}
