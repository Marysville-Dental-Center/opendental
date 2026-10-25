// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace ImagingDeviceManager.Acquire {
	///<summary>What surrounds a by-name acquisition while the source may be enabled (design 7.1 "Thread-modal device acquisitions"): from
	///just before MSG_ENABLEDS until the acquisition returns, after any wait for the source, every visible, enabled top-level window of
	///the calling thread is disabled, not only the root of the window the caller passed (on the Mount/Acquire path that is a hidden form,
	///so the main window would stay live). Dispose re-enables exactly those windows, on every exit path.</summary>
	internal sealed class DeviceAcquireScope:IDisposable {
		private readonly ThreadWindowDisabler _disabler;
		private readonly Action<string> _log;
		private bool _isDisposed;

		private DeviceAcquireScope(CaptureHost host,Action<string> log) {
			_disabler=new ThreadWindowDisabler(host.ThreadWindows);
			_log=log??(text => { });
		}

		///<summary>Disables the calling thread's windows, leaving the library's own windows enabled. A failure is logged, and the windows
		///disabled so far are enabled again; the acquisition then runs as before. Never throws.</summary>
		public static DeviceAcquireScope Begin(CaptureHost host,Action<string> log) {
			DeviceAcquireScope scope=new DeviceAcquireScope(host,log);
			try {
				scope._disabler.DisableAll(host.Ui.IsLibraryWindow);
			}
			catch(Exception ex) {
				scope._log("The program's windows could not be disabled for the acquisition: "+ex.Message);
				scope.ReenableSafely();
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

		public void Dispose() {
			if(_isDisposed) {
				return;
			}
			_isDisposed=true;
			ReenableSafely();
		}
	}
}
