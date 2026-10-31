// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Runtime.InteropServices;

namespace ImagingDeviceManager.TwainProtocol {
	///<summary>Win32 functions used by the TWAIN implementation.</summary>
	internal static class NativeMethods {
		public const uint GMEM_MOVEABLE=0x0002;
		public const uint GMEM_ZEROINIT=0x0040;
		public const uint GHND=GMEM_MOVEABLE|GMEM_ZEROINIT;
		public const int GWL_HWNDPARENT=-8;
		public const uint GA_ROOT=2;
		public const int WM_NULL=0x0000;
		public const int WM_TIMER=0x0113;
		public const int WM_QUIT=0x0012;
		public const uint DIB_RGB_COLORS=0;
		///<summary>Size reserved for a Windows MSG structure. 28 bytes on 32-bit Windows; extra room is harmless.</summary>
		public const int MsgBufferSize=64;
		public const int MsgOffsetHwnd=0;
		public const int MsgOffsetMessage=4;
		public const int MsgOffsetWParam=8;

		[DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
		public static extern IntPtr LoadLibrary(string lpFileName);

		[DllImport("kernel32.dll",CharSet=CharSet.Ansi,SetLastError=true,BestFitMapping=false)]
		public static extern IntPtr GetProcAddress(IntPtr hModule,string procName);

		[DllImport("kernel32.dll",SetLastError=true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool FreeLibrary(IntPtr hModule);

		[DllImport("kernel32.dll",SetLastError=true)]
		public static extern IntPtr GlobalAlloc(uint uFlags,UIntPtr dwBytes);

		[DllImport("kernel32.dll",SetLastError=true)]
		public static extern IntPtr GlobalLock(IntPtr hMem);

		[DllImport("kernel32.dll",SetLastError=true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool GlobalUnlock(IntPtr hMem);

		[DllImport("kernel32.dll",SetLastError=true)]
		public static extern IntPtr GlobalFree(IntPtr hMem);

		[DllImport("kernel32.dll",SetLastError=true)]
		public static extern UIntPtr GlobalSize(IntPtr hMem);

		[DllImport("user32.dll",SetLastError=true)]
		public static extern int GetMessage(IntPtr lpMsg,IntPtr hWnd,uint wMsgFilterMin,uint wMsgFilterMax);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool TranslateMessage(IntPtr lpMsg);

		[DllImport("user32.dll")]
		public static extern IntPtr DispatchMessage(IntPtr lpMsg);

		[DllImport("user32.dll",SetLastError=true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool PostMessage(IntPtr hWnd,int msg,IntPtr wParam,IntPtr lParam);

		[DllImport("user32.dll")]
		public static extern void PostQuitMessage(int nExitCode);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool EnableWindow(IntPtr hWnd,[MarshalAs(UnmanagedType.Bool)] bool bEnable);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool IsWindow(IntPtr hWnd);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool IsWindowEnabled(IntPtr hWnd);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool IsWindowVisible(IntPtr hWnd);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool IsIconic(IntPtr hWnd);

		[DllImport("user32.dll")]
		public static extern IntPtr GetAncestor(IntPtr hWnd,uint gaFlags);

		[DllImport("user32.dll",EntryPoint="SetWindowLong",SetLastError=true)]
		public static extern int SetWindowLong(IntPtr hWnd,int nIndex,int dwNewLong);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool SetForegroundWindow(IntPtr hWnd);

		[DllImport("user32.dll")]
		public static extern UIntPtr SetTimer(IntPtr hWnd,UIntPtr nIDEvent,uint uElapse,IntPtr lpTimerFunc);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool KillTimer(IntPtr hWnd,UIntPtr uIDEvent);

		[DllImport("gdi32.dll",SetLastError=true)]
		public static extern IntPtr CreateDIBSection(IntPtr hdc,IntPtr pbmi,uint usage,out IntPtr ppvBits,IntPtr hSection,uint offset);

		[DllImport("gdi32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool DeleteObject(IntPtr hObject);
	}
}
