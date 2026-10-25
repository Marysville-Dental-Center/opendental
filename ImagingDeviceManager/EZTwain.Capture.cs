// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using ImagingDeviceManager.Acquire;
using ImagingDeviceManager.Config;
using ImagingDeviceManager.TwainProtocol;

namespace ImagingDeviceManager {
	//Device acquisitions (design 7.1 CAP-1): capture.ini, the source gate, and what the facade adds around a source opened by name
	//(the Mount/Acquire path). Scans (the default source) keep their behaviour, apart from the source gate.
	public static partial class EZTwain {
		///<summary>Tests replace the host; null uses CaptureHost.CreateDefault.</summary>
		internal static CaptureHost HostOverride;
		private static CaptureHost _hostDefault;
		///<summary>capture.ini as read before the last open.</summary>
		private static LocalCaptureConfig _configInEffect;

		internal static CaptureHost Host {
			get {
				if(HostOverride!=null) {
					return HostOverride;
				}
				if(_hostDefault==null) {
					_hostDefault=CaptureHost.CreateDefault();
				}
				return _hostDefault;
			}
		}

		///<summary>Once, when the session is created.</summary>
		private static void ConfigureSession(TwainSession session) {
			session.SourceRefusedByGate+=OnSourceRefusedByGate;
		}

		private static void ResetCapture(CaptureHost host) {
			HostOverride=host;
			_configInEffect=null;
		}

		///<summary>Before anything that opens a source: reads capture.ini and gives the session the [Twain] settings for by-name
		///acquisitions and the source gate. Never while an acquisition runs.</summary>
		private static void ApplyCaptureConfig() {
			if(Session.IsAcquiring) {
				return;
			}
			LocalCaptureConfig config=Host.LoadConfigSafe();
			TwainCaptureSettings settings=TwainCaptureSettings.FromConfig(config);
			Host.AdjustSettings?.Invoke(settings);
			Session.Settings=settings;
			Session.SourceGate=TwainSourceGate.FromConfig(config);
			_configInEffect=config;
		}

		///<summary>The callers show only their own text when an open fails ("Imaging Device unavailable.", "Default scanner could not be
		///opened..."), so the gate's reason is shown as a notice of its own.</summary>
		private static void OnSourceRefusedByGate(string productName) {
			Host.Ui.ShowNotice(TwainSourceGate.RefusedText);
		}
	}
}
