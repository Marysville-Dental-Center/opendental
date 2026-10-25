// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using ImagingDeviceManager.Config;
using ImagingDeviceManager.TwainProtocol;
using ImagingDeviceManager.UI;

namespace ImagingDeviceManager.Acquire {
	///<summary>What the EZTwain facade needs from the computer around a device acquisition: capture.ini and the operator's windows.
	///Production uses the real ones (CreateDefault); tests replace each part with a stand-in.</summary>
	internal sealed class CaptureHost {
		///<summary>Reads capture.ini. Called before every open, so an administrator's change applies to the next acquisition.</summary>
		public Func<LocalCaptureConfig> LoadConfig;
		public ICaptureUi Ui;
		///<summary>Tests only: changes the session settings read from capture.ini, for example to shorten the wait after a refused
		///disable, which capture.ini does not set. Null in production.</summary>
		public Action<TwainCaptureSettings> AdjustSettings;

		///<summary>The real capture.ini (ProgramData, else the per-user file) and WinForms windows.</summary>
		public static CaptureHost CreateDefault() {
			return new CaptureHost {
				LoadConfig=LocalCaptureConfig.Load,
				Ui=new WinFormsCaptureUi(),
			};
		}

		///<summary>For tests that do not look at capture: an empty capture.ini (every feature at its default) and no windows.</summary>
		public static CaptureHost CreateInert() {
			return new CaptureHost {
				LoadConfig=() => LocalCaptureConfig.Load((string)null),
				Ui=new NullCaptureUi(),
			};
		}

		///<summary>LoadConfig, or an empty configuration if it fails. Never throws.</summary>
		public LocalCaptureConfig LoadConfigSafe() {
			try {
				LocalCaptureConfig config=LoadConfig?.Invoke();
				if(config!=null) {
					return config;
				}
			}
			catch {
				//LocalCaptureConfig.Load never throws; a stand-in might.
			}
			return LocalCaptureConfig.Load((string)null);
		}
	}

	///<summary>Shows nothing.</summary>
	internal sealed class NullCaptureUi:ICaptureUi {
		public void ShowNotice(string text) {
		}

		public bool IsLibraryWindow(IntPtr hwnd) {
			return false;
		}
	}
}
