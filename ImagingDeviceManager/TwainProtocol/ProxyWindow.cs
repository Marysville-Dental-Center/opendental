// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Runtime.InteropServices;

namespace ImagingDeviceManager.TwainProtocol {
	///<summary>An invisible top-level window that the Data Source Manager and data sources use as their parent window.
	///Scanner dialogs are positioned relative to it, and it is temporarily owned by the caller's window during an acquire so
	///that the dialogs stay in front of the program and focus returns to the program afterwards. It is a plain Win32 window
	///of the system "STATIC" class, so it needs no window class registration and no WinForms machinery.</summary>
	internal sealed class ProxyWindow:IDisposable {
		private const uint WS_POPUP=0x80000000;
		private const uint WS_EX_TOOLWINDOW=0x00000080;
		private const uint MONITOR_DEFAULTTONEAREST=2;
		private const uint MONITOR_DEFAULTTOPRIMARY=1;
		private const uint SWP_NOSIZE=0x0001;
		private const uint SWP_NOZORDER=0x0004;
		private const uint SWP_NOACTIVATE=0x0010;
		private IntPtr _hwndOwner=IntPtr.Zero;

		public IntPtr Handle { get; private set; }

		public ProxyWindow() {
			RECT rectWork=GetWorkArea(IntPtr.Zero);
			Handle=CreateWindowEx(WS_EX_TOOLWINDOW,"STATIC","TWAIN",WS_POPUP,
				(rectWork.Left+rectWork.Right)/2,(rectWork.Top+rectWork.Bottom)/2,1,1,
				IntPtr.Zero,IntPtr.Zero,GetModuleHandle(null),IntPtr.Zero);
			if(Handle==IntPtr.Zero) {
				throw new InvalidOperationException("CreateWindowEx failed with Windows error "+Marshal.GetLastWin32Error()+".");
			}
		}

		///<summary>Makes the proxy owned by the top-level window that contains hwnd (if that window is visible and not
		///minimized), and centers the proxy over that window. Pass IntPtr.Zero to make the proxy unowned again.</summary>
		public void AttachTo(IntPtr hwnd) {
			if(Handle==IntPtr.Zero) {
				return;
			}
			IntPtr hwndRoot=GetUsableRoot(hwnd);
			if(hwndRoot!=_hwndOwner) {
				NativeMethods.SetWindowLong(Handle,NativeMethods.GWL_HWNDPARENT,hwndRoot.ToInt32());
				_hwndOwner=hwndRoot;
			}
			RECT rect;
			if(hwndRoot==IntPtr.Zero || !GetWindowRect(hwndRoot,out rect)) {
				rect=GetWorkArea(hwnd!=IntPtr.Zero && NativeMethods.IsWindow(hwnd) ? hwnd : IntPtr.Zero);
			}
			SetWindowPos(Handle,IntPtr.Zero,(rect.Left+rect.Right)/2,(rect.Top+rect.Bottom)/2,0,0,SWP_NOSIZE|SWP_NOZORDER|SWP_NOACTIVATE);
		}

		///<summary>The top-level window that contains hwnd, if it is a visible, non-minimized window. Otherwise IntPtr.Zero.</summary>
		public static IntPtr GetUsableRoot(IntPtr hwnd) {
			if(hwnd==IntPtr.Zero || !NativeMethods.IsWindow(hwnd)) {
				return IntPtr.Zero;
			}
			IntPtr hwndRoot=NativeMethods.GetAncestor(hwnd,NativeMethods.GA_ROOT);
			if(hwndRoot==IntPtr.Zero || !NativeMethods.IsWindowVisible(hwndRoot) || NativeMethods.IsIconic(hwndRoot)) {
				return IntPtr.Zero;
			}
			return hwndRoot;
		}

		///<summary>Work area of the monitor showing hwnd, or of the primary monitor when hwnd is IntPtr.Zero.</summary>
		private static RECT GetWorkArea(IntPtr hwnd) {
			IntPtr hMonitor=hwnd==IntPtr.Zero
				? MonitorFromPoint(new POINT(),MONITOR_DEFAULTTOPRIMARY)
				: MonitorFromWindow(hwnd,MONITOR_DEFAULTTONEAREST);
			MONITORINFO monitorInfo=new MONITORINFO();
			monitorInfo.cbSize=Marshal.SizeOf(typeof(MONITORINFO));
			if(hMonitor!=IntPtr.Zero && GetMonitorInfo(hMonitor,ref monitorInfo)) {
				return monitorInfo.rcWork;
			}
			return new RECT { Left=0,Top=0,Right=800,Bottom=600 };
		}

		public void Dispose() {
			if(Handle!=IntPtr.Zero) {
				DestroyWindow(Handle);
				Handle=IntPtr.Zero;
			}
		}

		#region Win32
		[StructLayout(LayoutKind.Sequential)]
		private struct RECT {
			public int Left;
			public int Top;
			public int Right;
			public int Bottom;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct POINT {
			public int X;
			public int Y;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct MONITORINFO {
			public int cbSize;
			public RECT rcMonitor;
			public RECT rcWork;
			public uint dwFlags;
		}

		[DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
		private static extern IntPtr CreateWindowEx(uint dwExStyle,string lpClassName,string lpWindowName,uint dwStyle,int x,int y,
			int nWidth,int nHeight,IntPtr hWndParent,IntPtr hMenu,IntPtr hInstance,IntPtr lpParam);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool DestroyWindow(IntPtr hWnd);

		[DllImport("kernel32.dll",CharSet=CharSet.Unicode)]
		private static extern IntPtr GetModuleHandle(string lpModuleName);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool GetWindowRect(IntPtr hWnd,out RECT lpRect);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool SetWindowPos(IntPtr hWnd,IntPtr hWndInsertAfter,int x,int y,int cx,int cy,uint uFlags);

		[DllImport("user32.dll")]
		private static extern IntPtr MonitorFromWindow(IntPtr hwnd,uint dwFlags);

		[DllImport("user32.dll")]
		private static extern IntPtr MonitorFromPoint(POINT pt,uint dwFlags);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool GetMonitorInfo(IntPtr hMonitor,ref MONITORINFO lpmi);
		#endregion Win32
	}
}
