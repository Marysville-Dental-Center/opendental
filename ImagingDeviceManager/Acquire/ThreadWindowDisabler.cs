// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using ImagingDeviceManager.TwainProtocol;

namespace ImagingDeviceManager.Acquire {
	///<summary>The window functions ThreadWindowDisabler uses. Tests replace them with a scripted set of windows.</summary>
	internal interface IThreadWindowApi {
		///<summary>The top-level windows of the calling thread (EnumThreadWindows).</summary>
		List<IntPtr> GetThreadWindows();
		bool IsWindow(IntPtr hwnd);
		bool IsWindowVisible(IntPtr hwnd);
		bool IsWindowEnabled(IntPtr hwnd);
		void EnableWindow(IntPtr hwnd,bool isEnabled);
	}

	///<summary>The thread-modal part of a device acquisition (design 7.1): disables every visible, enabled top-level window of the calling
	///thread, the way a modal dialog disables its owner, and later re-enables exactly those. Windows that were hidden or already disabled
	///are left alone and stay as they were; windows created afterwards (the source's own, or the library's status window and notices)
	///are never touched. Windows of other processes, such as an out-of-process sensor helper, are never touched either.</summary>
	internal sealed class ThreadWindowDisabler {
		private readonly IThreadWindowApi _api;
		private readonly List<IntPtr> _listDisabled=new List<IntPtr>();

		public ThreadWindowDisabler(IThreadWindowApi api) {
			_api=api;
		}

		///<summary>The windows DisableAll disabled and ReenableAll has not re-enabled yet.</summary>
		public List<IntPtr> ListDisabled => new List<IntPtr>(_listDisabled);

		///<summary>Disables every visible, enabled top-level window of the calling thread for which isExcluded (may be null) returns false,
		///and remembers exactly those. Returns how many it disabled.</summary>
		public int DisableAll(Func<IntPtr,bool> isExcluded) {
			int count=0;
			foreach(IntPtr hwnd in _api.GetThreadWindows()) {
				if(hwnd==IntPtr.Zero || _listDisabled.Contains(hwnd)) {
					continue;
				}
				if(isExcluded!=null && isExcluded(hwnd)) {
					continue;
				}
				if(!_api.IsWindowVisible(hwnd) || !_api.IsWindowEnabled(hwnd)) {
					continue;
				}
				_api.EnableWindow(hwnd,false);
				_listDisabled.Add(hwnd);
				count++;
			}
			return count;
		}

		///<summary>Re-enables exactly the windows DisableAll disabled that still exist, in the reverse order, and forgets them. Harmless to
		///call again.</summary>
		public void ReenableAll() {
			for(int i=_listDisabled.Count-1;i>=0;i--) {
				IntPtr hwnd=_listDisabled[i];
				if(_api.IsWindow(hwnd)) {
					_api.EnableWindow(hwnd,true);
				}
			}
			_listDisabled.Clear();
		}
	}

	///<summary>The real window functions.</summary>
	internal sealed class Win32ThreadWindowApi:IThreadWindowApi {
		public List<IntPtr> GetThreadWindows() {
			List<IntPtr> listWindows=new List<IntPtr>();
			NativeMethods.EnumWindowsProc enumWindowsProc=(hwnd,lParam) => {
				listWindows.Add(hwnd);
				return true;
			};
			NativeMethods.EnumThreadWindows(NativeMethods.GetCurrentThreadId(),enumWindowsProc,IntPtr.Zero);
			GC.KeepAlive(enumWindowsProc);
			return listWindows;
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

		public void EnableWindow(IntPtr hwnd,bool isEnabled) {
			NativeMethods.EnableWindow(hwnd,isEnabled);
		}
	}
}
