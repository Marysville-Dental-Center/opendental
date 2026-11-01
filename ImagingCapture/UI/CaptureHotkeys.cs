// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using ImagingCapture.Session;

namespace ImagingCapture.UI {
	///<summary>RegisterHotKey and UnregisterHotKey for one window, as a seam so that CaptureHotkeys can be tested without a window.</summary>
	public interface ICaptureHotkeyRegistrar {
		///<summary>Registers chord as hotkey id (no auto-repeat). False, with the Win32 error code, when Windows refused it (another program
		///holds the same chord, for example).</summary>
		bool Register(int id,CaptureKeyChord chord,out int errorCode);
		void Unregister(int id);
	}

	///<summary>The session keys as hotkeys (design 7.3 step 3): while the source is enabled, the [Keys] chords of Retake, Skip, NextSlot,
	///PreviousSlot, Rotate, Disable and End are registered with RegisterHotKey on the session window's handle, so that a pedal works while the
	///sensor software has the foreground; they are released when the source is no longer enabled and when the window closes. Confirm is never
	///a hotkey: a deliberate start is made in the session window itself. A chord Windows refuses is logged once and skipped; the others still
	///work. UI thread only.</summary>
	public sealed class CaptureHotkeys {
		///<summary>The first hotkey id; an application's ids are 0x0000 to 0xBFFF.</summary>
		public const int IdFirst=0x5C00;
		private readonly CaptureKeyBindings _bindings;
		private ICaptureHotkeyRegistrar _registrar;
		private readonly Action<string> _actionLog;
		private readonly Dictionary<int,CaptureSessionKey> _dictRegistered=new Dictionary<int,CaptureSessionKey>();
		private readonly HashSet<CaptureSessionKey> _hashSetFailureLogged=new HashSet<CaptureSessionKey>();
		private bool _isActive;

		///<summary>actionLog: a line for the session log (a refused registration).</summary>
		public CaptureHotkeys(CaptureKeyBindings bindings,Action<string> actionLog) {
			_bindings=bindings??throw new ArgumentNullException(nameof(bindings));
			_actionLog=actionLog;
		}

		///<summary>The hotkeys are wanted now (SetActive(true) was last).</summary>
		public bool IsActive => _isActive;

		///<summary>The keys registered now, in the order of CaptureSessionKey.</summary>
		public List<CaptureSessionKey> GetKeysRegistered() {
			List<CaptureSessionKey> listKeys=new List<CaptureSessionKey>(_dictRegistered.Values);
			listKeys.Sort((x,y) => ((int)x).CompareTo((int)y));
			return listKeys;
		}

		///<summary>The window's handle is known (or gone, with null). Registered hotkeys move to the new registrar when the set is active.</summary>
		public void SetRegistrar(ICaptureHotkeyRegistrar registrar) {
			bool isActive=_isActive;
			ReleaseAll();
			_registrar=registrar;
			if(isActive) {
				SetActive(true);
			}
		}

		///<summary>Registers every bound session key (isActive) or releases them all. Calling it again with the same value does nothing, so the
		///window calls it on every refresh with CaptureSessionController.AreSessionKeysActive.</summary>
		public void SetActive(bool isActive) {
			if(!isActive) {
				ReleaseAll();
				return;
			}
			if(_isActive) {
				return;
			}
			_isActive=true;
			if(_registrar==null) {
				return;//no window handle yet; SetRegistrar registers them
			}
			int id=IdFirst;
			foreach(CaptureSessionKey key in _bindings.GetSessionKeysBound()) {
				CaptureKeyChord chord=_bindings.GetChord(key);
				int idKey=id++;
				int errorCode=0;
				bool isRegistered;
				try {
					isRegistered=_registrar.Register(idKey,chord,out errorCode);
				}
				catch(Exception ex) {
					LogFailure(key,chord,ex.Message);
					continue;
				}
				if(isRegistered) {
					_dictRegistered[idKey]=key;
					continue;
				}
				LogFailure(key,chord,new Win32Exception(errorCode).Message+" (error "+errorCode+")");
			}
		}

		///<summary>Releases every registered hotkey. Safe to call at any time, also more than once (the window closing, a failure path).</summary>
		public void ReleaseAll() {
			_isActive=false;
			if(_dictRegistered.Count==0) {
				return;
			}
			List<int> listIds=new List<int>(_dictRegistered.Keys);
			_dictRegistered.Clear();
			foreach(int id in listIds) {
				try {
					_registrar?.Unregister(id);
				}
				catch(Exception ex) {
					_actionLog?.Invoke("A hotkey could not be released: "+ex.Message);
				}
			}
		}

		///<summary>WM_HOTKEY with wParam id: the session key it stands for, or null for an id that is not registered now.</summary>
		public CaptureSessionKey? GetKey(int id) {
			CaptureSessionKey key;
			if(_isActive && _dictRegistered.TryGetValue(id,out key)) {
				return key;
			}
			return null;
		}

		private void LogFailure(CaptureSessionKey key,CaptureKeyChord chord,string why) {
			if(!_hashSetFailureLogged.Add(key)) {
				return;
			}
			_actionLog?.Invoke("[Keys] "+key+" ("+chord.Text+") could not be registered as a hotkey: "+why+" It works only while the session window "
				+"has the focus.");
		}
	}

	///<summary>ICaptureHotkeyRegistrar on one window handle (user32 RegisterHotKey with MOD_NOREPEAT, so a held pedal sends one WM_HOTKEY). The
	///hotkeys belong to the window's thread; register and release them on that thread.</summary>
	public sealed class Win32HotkeyRegistrar:ICaptureHotkeyRegistrar {
		///<summary>WM_HOTKEY: wParam is the id.</summary>
		public const int WM_HOTKEY=0x0312;
		private const uint MOD_NOREPEAT=0x4000;
		private readonly IntPtr _hwnd;

		public Win32HotkeyRegistrar(IntPtr hwnd) {
			_hwnd=hwnd;
		}

		public bool Register(int id,CaptureKeyChord chord,out int errorCode) {
			errorCode=0;
			if(_hwnd==IntPtr.Zero || chord==null) {
				return false;
			}
			if(RegisterHotKey(_hwnd,id,(uint)chord.Modifiers|MOD_NOREPEAT,(uint)chord.VirtualKey)) {
				return true;
			}
			errorCode=Marshal.GetLastWin32Error();
			return false;
		}

		public void Unregister(int id) {
			if(_hwnd==IntPtr.Zero) {
				return;
			}
			UnregisterHotKey(_hwnd,id);
		}

		[DllImport("user32.dll",SetLastError=true)]
		private static extern bool RegisterHotKey(IntPtr hWnd,int id,uint fsModifiers,uint vk);

		[DllImport("user32.dll",SetLastError=true)]
		private static extern bool UnregisterHotKey(IntPtr hWnd,int id);
	}
}
