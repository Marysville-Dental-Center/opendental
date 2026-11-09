// Win32 calls of the headless window checks: finding a process's or a thread's windows, reading their texts, pressing a button by message
// (BM_CLICK), closing by WM_CLOSE and printing a window into a bitmap (PrintWindow). Nothing here sends input (no SendInput, no mouse or
// keyboard): every step works while the session's screen is locked.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace WindowChecksTests {
	public static class Native {
		public const uint PW_RENDERFULLCONTENT=2;
		public const int WM_CLOSE=0x0010;
		public const int WM_GETTEXT=0x000D;
		public const int WM_GETTEXTLENGTH=0x000E;
		public const int WM_COMMAND=0x0111;
		public const int BM_CLICK=0x00F5;
		public const int IDOK=1;
		public const int IDCANCEL=2;
		public const int IDNO=7;
		public const int GW_OWNER=4;
		public const int GWL_STYLE=-16;
		public const int GWL_EXSTYLE=-20;
		public const int WS_CAPTION=0x00C00000;
		public const int WS_THICKFRAME=0x00040000;
		public const int WS_MINIMIZEBOX=0x00020000;
		public const int WS_MAXIMIZEBOX=0x00010000;
		public const int WS_SYSMENU=0x00080000;

		public delegate bool EnumWindowsProc(IntPtr hwnd,IntPtr lParam);

		[StructLayout(LayoutKind.Sequential)]
		public struct RECT {
			public int Left;
			public int Top;
			public int Right;
			public int Bottom;
		}

		[DllImport("user32.dll")]
		public static extern bool PrintWindow(IntPtr hwnd,IntPtr hdc,uint flags);
		[DllImport("user32.dll")]
		public static extern bool EnumWindows(EnumWindowsProc enumProc,IntPtr lParam);
		[DllImport("user32.dll")]
		public static extern bool EnumThreadWindows(int threadId,EnumWindowsProc enumProc,IntPtr lParam);
		[DllImport("user32.dll")]
		public static extern bool EnumChildWindows(IntPtr hwndParent,EnumWindowsProc enumProc,IntPtr lParam);
		[DllImport("user32.dll")]
		public static extern int GetWindowThreadProcessId(IntPtr hwnd,out int processId);
		[DllImport("user32.dll")]
		public static extern bool IsWindowVisible(IntPtr hwnd);
		[DllImport("user32.dll")]
		public static extern bool IsWindow(IntPtr hwnd);
		[DllImport("user32.dll")]
		public static extern bool IsWindowEnabled(IntPtr hwnd);
		[DllImport("user32.dll")]
		public static extern bool GetWindowRect(IntPtr hwnd,out RECT rect);
		[DllImport("user32.dll",CharSet=CharSet.Unicode)]
		public static extern int GetClassName(IntPtr hwnd,StringBuilder stringBuilder,int maxCount);
		[DllImport("user32.dll",CharSet=CharSet.Unicode,EntryPoint="SendMessageW")]
		public static extern IntPtr SendMessage(IntPtr hwnd,int msg,IntPtr wParam,IntPtr lParam);
		[DllImport("user32.dll",CharSet=CharSet.Unicode,EntryPoint="SendMessageW")]
		public static extern IntPtr SendMessageText(IntPtr hwnd,int msg,IntPtr wParam,StringBuilder lParam);
		[DllImport("user32.dll",CharSet=CharSet.Unicode,EntryPoint="SendMessageTimeoutW")]
		public static extern IntPtr SendMessageTimeoutText(IntPtr hwnd,int msg,IntPtr wParam,StringBuilder lParam,int flags,int timeout,out IntPtr result);
		[DllImport("user32.dll",CharSet=CharSet.Unicode,EntryPoint="SendMessageTimeoutW")]
		public static extern IntPtr SendMessageTimeout(IntPtr hwnd,int msg,IntPtr wParam,IntPtr lParam,int flags,int timeout,out IntPtr result);
		[DllImport("user32.dll",CharSet=CharSet.Unicode,EntryPoint="PostMessageW")]
		public static extern bool PostMessage(IntPtr hwnd,int msg,IntPtr wParam,IntPtr lParam);
		[DllImport("user32.dll")]
		public static extern IntPtr GetWindow(IntPtr hwnd,int cmd);
		[DllImport("user32.dll")]
		public static extern int GetDlgCtrlID(IntPtr hwnd);
		[DllImport("user32.dll",EntryPoint="GetWindowLong")]
		public static extern int GetWindowLong(IntPtr hwnd,int index);
		[DllImport("user32.dll")]
		public static extern IntPtr GetParent(IntPtr hwnd);
		[DllImport("kernel32.dll")]
		public static extern int GetCurrentThreadId();
		[DllImport("user32.dll")]
		public static extern bool IsIconic(IntPtr hwnd);
		[DllImport("user32.dll")]
		public static extern IntPtr GetForegroundWindow();

		///<summary>SMTO_ABORTIFHUNG: a hung window's text reads as empty instead of blocking the test.</summary>
		private const int SMTO_ABORTIFHUNG=0x0002;

		public static string GetClass(IntPtr hwnd) {
			StringBuilder stringBuilder=new StringBuilder(256);
			GetClassName(hwnd,stringBuilder,stringBuilder.Capacity);
			return stringBuilder.ToString();
		}

		///<summary>The window's text by WM_GETTEXT with a timeout (works across processes, also for edit controls).</summary>
		public static string GetText(IntPtr hwnd) {
			IntPtr result;
			IntPtr ok=SendMessageTimeout(hwnd,WM_GETTEXTLENGTH,IntPtr.Zero,IntPtr.Zero,SMTO_ABORTIFHUNG,2000,out result);
			if(ok==IntPtr.Zero) {
				return "";
			}
			int length=result.ToInt32();
			if(length<=0) {
				return "";
			}
			StringBuilder stringBuilder=new StringBuilder(length+2);
			SendMessageTimeoutText(hwnd,WM_GETTEXT,(IntPtr)stringBuilder.Capacity,stringBuilder,SMTO_ABORTIFHUNG,2000,out result);
			return stringBuilder.ToString();
		}

		public static Rectangle GetRect(IntPtr hwnd) {
			RECT rect;
			GetWindowRect(hwnd,out rect);
			return Rectangle.FromLTRB(rect.Left,rect.Top,rect.Right,rect.Bottom);
		}

		///<summary>The visible top-level windows of the process, in z-order (topmost first).</summary>
		public static List<IntPtr> GetProcessWindows(int processId,bool includeHidden=false) {
			List<IntPtr> listHwnds=new List<IntPtr>();
			EnumWindows((hwnd,lParam) => {
				int pid;
				GetWindowThreadProcessId(hwnd,out pid);
				if(pid==processId && (includeHidden || IsWindowVisible(hwnd))) {
					listHwnds.Add(hwnd);
				}
				return true;
			},IntPtr.Zero);
			return listHwnds;
		}

		///<summary>The visible top-level windows of a thread of this process.</summary>
		public static List<IntPtr> GetThreadWindows(int threadId) {
			List<IntPtr> listHwnds=new List<IntPtr>();
			EnumThreadWindows(threadId,(hwnd,lParam) => {
				if(IsWindowVisible(hwnd)) {
					listHwnds.Add(hwnd);
				}
				return true;
			},IntPtr.Zero);
			return listHwnds;
		}

		public static List<IntPtr> GetChildren(IntPtr hwnd) {
			List<IntPtr> listHwnds=new List<IntPtr>();
			EnumChildWindows(hwnd,(hwndChild,lParam) => {
				listHwnds.Add(hwndChild);
				return true;
			},IntPtr.Zero);
			return listHwnds;
		}

		///<summary>True for the standard dialog class, which MessageBox.Show (WinForms and WPF) creates.</summary>
		public static bool IsDialogClass(IntPtr hwnd) {
			return GetClass(hwnd)=="#32770";
		}

		///<summary>The message of a standard message box: its static texts, joined. Buttons are left out.</summary>
		public static string GetDialogMessage(IntPtr hwnd) {
			List<string> listTexts=new List<string>();
			foreach(IntPtr hwndChild in GetChildren(hwnd)) {
				string className=GetClass(hwndChild);
				if(className=="Static" || className.StartsWith("DirectUIHWND")) {
					string text=GetText(hwndChild);
					if(!string.IsNullOrWhiteSpace(text)) {
						listTexts.Add(text);
					}
				}
			}
			return string.Join(" | ",listTexts);
		}

		///<summary>The texts of the dialog's buttons.</summary>
		public static List<string> GetDialogButtons(IntPtr hwnd) {
			List<string> listTexts=new List<string>();
			foreach(IntPtr hwndChild in GetChildren(hwnd)) {
				if(GetClass(hwndChild)=="Button") {
					listTexts.Add(GetText(hwndChild).Replace("&",""));
				}
			}
			return listTexts;
		}

		///<summary>Presses the dialog's button with this text (no ampersands) by BM_CLICK. False when there is none.</summary>
		public static bool ClickDialogButton(IntPtr hwnd,string text) {
			foreach(IntPtr hwndChild in GetChildren(hwnd)) {
				if(GetClass(hwndChild)=="Button" && string.Equals(GetText(hwndChild).Replace("&",""),text,StringComparison.OrdinalIgnoreCase)) {
					//Posted: a sent BM_CLICK would run the dialog's whole close inside this call.
					PostMessage(hwndChild,BM_CLICK,IntPtr.Zero,IntPtr.Zero);
					return true;
				}
			}
			return false;
		}

		///<summary>Closes a standard dialog the way its Cancel (or Esc) does, or with its only button.</summary>
		public static void CancelDialog(IntPtr hwnd) {
			List<string> listButtons=GetDialogButtons(hwnd);
			foreach(string text in new[] { "Cancel","No","OK","Close" }) {
				if(listButtons.Exists(x => string.Equals(x,text,StringComparison.OrdinalIgnoreCase))) {
					ClickDialogButton(hwnd,text);
					return;
				}
			}
			PostMessage(hwnd,WM_COMMAND,(IntPtr)IDCANCEL,IntPtr.Zero);
		}
	}
}
