// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using ImagingDeviceManager.TwainProtocol;

namespace ImagingDeviceManager.Acquire {
	///<summary>The window functions ForegroundRestorer uses. Tests replace them.</summary>
	internal interface IForegroundApi {
		IntPtr GetForegroundWindow();
		void SetForegroundWindow(IntPtr hwnd);
		bool IsWindow(IntPtr hwnd);
		bool IsWindowVisible(IntPtr hwnd);
		bool IsWindowEnabled(IntPtr hwnd);
		///<summary>True when hwnd belongs to this process.</summary>
		bool IsOwnProcessWindow(IntPtr hwnd);
	}

	///<summary>Runs an action once, later, on the calling thread. Tests run it by hand.</summary>
	internal interface IDelayScheduler {
		///<summary>Runs action once after delayMs on this thread, unless Cancel is called first. Replaces an action still pending.</summary>
		void Schedule(int delayMs,Action action);
		void Cancel();
	}

	///<summary>Activation around thread-modal acquisitions (design 7.1 "Activation"). The foreground window is recorded before anything is
	///disabled. Nothing forces the foreground between the acquisitions of a series, so that each new sensor-software window can come to
	///the front. When an acquisition ends, the recorded window gets the foreground back only if no new acquisition starts within about
	///1 s, and only if no other program has the foreground then (the operator may have switched on purpose; Windows would not allow it
	///anyway). All members run on the UI thread.</summary>
	internal sealed class ForegroundRestorer {
		public const int RestoreDelayMs=1000;
		private readonly IForegroundApi _api;
		private readonly IDelayScheduler _scheduler;
		private IntPtr _hwndRecorded;
		private bool _isAcquiring;
		private bool _isRestorePending;

		public ForegroundRestorer(IForegroundApi api,IDelayScheduler scheduler) {
			_api=api;
			_scheduler=scheduler;
		}

		///<summary>The window to restore, or IntPtr.Zero.</summary>
		public IntPtr HwndRecorded => _hwndRecorded;

		///<summary>Before an acquisition disables anything. Records the foreground window, unless the restore after the previous
		///acquisition is still pending: then a series is running, its new acquisition cancels that restore, and the window recorded when the
		///series started is kept.</summary>
		public void BeginAcquire() {
			_isAcquiring=true;
			if(_isRestorePending) {
				_isRestorePending=false;
				_scheduler.Cancel();
				return;
			}
			_hwndRecorded=_api.GetForegroundWindow();
		}

		///<summary>After the acquisition re-enabled the windows: the restore is checked RestoreDelayMs later.</summary>
		public void EndAcquire() {
			_isAcquiring=false;
			if(_hwndRecorded==IntPtr.Zero) {
				return;
			}
			_isRestorePending=true;
			_scheduler.Schedule(RestoreDelayMs,OnDelayElapsed);
		}

		private void OnDelayElapsed() {
			if(!_isRestorePending || _isAcquiring) {
				return;//A new acquisition started; it keeps the recorded window for the end of the series.
			}
			_isRestorePending=false;
			IntPtr hwnd=_hwndRecorded;
			_hwndRecorded=IntPtr.Zero;
			if(ShouldRestore(_api,hwnd,_api.GetForegroundWindow())) {
				_api.SetForegroundWindow(hwnd);
			}
		}

		///<summary>True when hwndRecorded should get the foreground back: it is a visible, enabled window of this process that does not have
		///it, and the foreground is either nobody's or one of this process's windows. Not when another program has the foreground, and not
		///when the recorded window is disabled (a message box is open over it) or gone.</summary>
		public static bool ShouldRestore(IForegroundApi api,IntPtr hwndRecorded,IntPtr hwndForeground) {
			if(hwndRecorded==IntPtr.Zero || hwndForeground==hwndRecorded) {
				return false;
			}
			if(!api.IsWindow(hwndRecorded) || !api.IsWindowVisible(hwndRecorded) || !api.IsWindowEnabled(hwndRecorded)
				|| !api.IsOwnProcessWindow(hwndRecorded))
			{
				return false;
			}
			return hwndForeground==IntPtr.Zero || api.IsOwnProcessWindow(hwndForeground);
		}
	}

	///<summary>The real window functions.</summary>
	internal sealed class Win32ForegroundApi:IForegroundApi {
		public IntPtr GetForegroundWindow() {
			return NativeMethods.GetForegroundWindow();
		}

		public void SetForegroundWindow(IntPtr hwnd) {
			NativeMethods.SetForegroundWindow(hwnd);
		}

		public bool IsWindow(IntPtr hwnd) {
			return NativeMethods.IsWindow(hwnd);
		}

		public bool IsWindowVisible(IntPtr hwnd) {
			return NativeMethods.IsWindowVisible(hwnd);
		}

		public bool IsWindowEnabled(IntPtr hwnd) {
			return NativeMethods.IsWindowEnabled(hwnd);
		}

		public bool IsOwnProcessWindow(IntPtr hwnd) {
			if(hwnd==IntPtr.Zero) {
				return false;
			}
			uint processId;
			NativeMethods.GetWindowThreadProcessId(hwnd,out processId);
			return processId!=0 && processId==NativeMethods.GetCurrentProcessId();
		}
	}

	///<summary>A WinForms timer on the UI thread. Its tick is a WM_TIMER message, so it also fires inside an acquisition's message loop;
	///ForegroundRestorer ignores it then.</summary>
	internal sealed class WinFormsDelayScheduler:IDelayScheduler {
		private System.Windows.Forms.Timer _timer;
		private Action _action;

		public void Schedule(int delayMs,Action action) {
			Cancel();
			if(_timer==null) {
				_timer=new System.Windows.Forms.Timer();
				_timer.Tick+=OnTick;
			}
			_action=action;
			_timer.Interval=Math.Max(1,delayMs);
			_timer.Start();
		}

		public void Cancel() {
			_timer?.Stop();
			_action=null;
		}

		private void OnTick(object sender,EventArgs e) {
			_timer.Stop();
			Action action=_action;
			_action=null;
			try {
				action?.Invoke();
			}
			catch {
				//Restoring the foreground is a convenience; it never stops the program.
			}
		}
	}
}
