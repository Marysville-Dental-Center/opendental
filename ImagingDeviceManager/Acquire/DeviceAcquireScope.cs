// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using ImagingDeviceManager.TwainProtocol;
using ImagingDeviceManager.UI;

namespace ImagingDeviceManager.Acquire {
	///<summary>What surrounds a by-name acquisition while the source may be enabled (design 7.1 "Thread-modal device acquisitions" and
	///"Status and Cancel window"): from just before MSG_ENABLEDS until the acquisition returns, after any wait for the source, every
	///visible, enabled top-level window of the calling thread is disabled, not only the root of the window the caller passed (on the
	///Mount/Acquire path that is a hidden form, so the main window would stay live), and the library's status and Cancel window is shown.
	///The foreground window is recorded first (design 7.1 "Activation"). Dispose re-enables exactly those windows and closes the status
	///window, on every exit path, and leaves the foreground restore to ForegroundRestorer.</summary>
	internal sealed class DeviceAcquireScope:IDisposable {
		private readonly ThreadWindowDisabler _disabler;
		private readonly ForegroundRestorer _foregroundRestorer;
		private readonly TwainSession _session;
		private readonly Action<string> _log;
		private AcquireStatusController _controller;
		private IAcquireStatusView _view;
		private bool _isDisposed;

		private DeviceAcquireScope(CaptureHost host,ForegroundRestorer foregroundRestorer,TwainSession session,Action<string> log) {
			_disabler=new ThreadWindowDisabler(host.ThreadWindows);
			_foregroundRestorer=foregroundRestorer;
			_session=session;
			_log=log??(text => { });
		}

		///<summary>The status window's logic, or null when it is not shown.</summary>
		public AcquireStatusController Controller => _controller;

		///<summary>Records the foreground window, disables the calling thread's windows, leaving the library's own windows enabled, then
		///shows the status window, which is created afterwards and so stays enabled. unassignedText is the status window's quarantine line,
		///or null. A failure is logged, and the windows disabled so far are enabled again; the acquisition then runs as before. Never
		///throws.</summary>
		public static DeviceAcquireScope Begin(CaptureHost host,ForegroundRestorer foregroundRestorer,TwainSession session,string unassignedText,
			Action<string> log)
		{
			DeviceAcquireScope scope=new DeviceAcquireScope(host,foregroundRestorer,session,log);
			try {
				scope._foregroundRestorer?.BeginAcquire();
				scope._disabler.DisableAll(host.Ui.IsLibraryWindow);
			}
			catch(Exception ex) {
				scope._log("The program's windows could not be disabled for the acquisition: "+ex.Message);
				scope.ReenableSafely();
				return scope;
			}
			try {
				scope._controller=new AcquireStatusController(() => session.IsSafeDisableInEffect,session.MultiTransfer,session.Cancel,unassignedText);
				session.WaitingForSourceChanged+=scope._controller.SetWaiting;
				scope._view=host.Ui.ShowStatus(scope._controller);
			}
			catch(Exception ex) {
				scope._log("The acquisition status window could not be shown: "+ex.Message);
			}
			return scope;
		}

		private void ReenableSafely() {
			try {
				_disabler.ReenableAll();
			}
			catch(Exception ex) {
				_log("The program's windows could not be enabled again after the acquisition: "+ex.Message);
			}
		}

		///<summary>Re-enables the windows first and then closes the status window, so that Windows hands activation back to the program
		///rather than to another one.</summary>
		public void Dispose() {
			if(_isDisposed) {
				return;
			}
			_isDisposed=true;
			if(_controller!=null) {
				_session.WaitingForSourceChanged-=_controller.SetWaiting;
			}
			ReenableSafely();
			try {
				_view?.Close();
			}
			catch(Exception ex) {
				_log("The acquisition status window could not be closed: "+ex.Message);
			}
			try {
				_foregroundRestorer?.EndAcquire();
			}
			catch(Exception ex) {
				_log("The foreground window could not be scheduled for restoring: "+ex.Message);
			}
		}
	}
}
