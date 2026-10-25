// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using ImagingDeviceManager.Acquire;
using ImagingDeviceManager.Config;
using ImagingDeviceManager.Imaging;
using ImagingDeviceManager.Journal;
using ImagingDeviceManager.TwainProtocol;

namespace ImagingDeviceManager {
	//Device acquisitions (design 7.1 CAP-1): capture.ini, the source gate, and what the facade adds around a source opened by name
	//(the Mount/Acquire path). Scans (the default source) keep their behaviour, apart from the source gate.
	public static partial class EZTwain {
		///<summary>True (the default): while a source opened by name (OpenSource, the Mount/Acquire path) is enabled, every visible, enabled
		///top-level window of the calling thread is disabled, and exactly those are enabled again when the acquisition ends, including
		///any wait for the source to finish. False opts out: as for scans, only the top-level window containing the window passed to
		///Acquire is disabled. capture.ini [Twain] ThreadModal=0 opts out the same way.</summary>
		public static bool DisableThreadWindowsDuringAcquire { get; set; }=true;
		///<summary>Tests replace the host; null uses CaptureHost.CreateDefault.</summary>
		internal static CaptureHost HostOverride;
		private static CaptureHost _hostDefault;
		///<summary>capture.ini as read before the last open.</summary>
		private static LocalCaptureConfig _configInEffect;
		private static ForegroundRestorer _foregroundRestorer;
		private static CaptureLog _captureLog;
		private static CaptureJournal _journal;

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

		///<summary>One for all acquisitions, so that a series keeps the window recorded at its start.</summary>
		private static ForegroundRestorer ForegroundRestorerShared {
			get {
				if(_foregroundRestorer==null) {
					_foregroundRestorer=new ForegroundRestorer(Host.Foreground,Host.Scheduler);
				}
				return _foregroundRestorer;
			}
		}

		///<summary>Once, when the session is created.</summary>
		private static void ConfigureSession(TwainSession session) {
			session.SourceRefusedByGate+=OnSourceRefusedByGate;
			session.Logged+=LogCapture;
		}

		private static CaptureJournal Journal {
			get {
				if(_journal==null) {
					_journal=new CaptureJournal(Host.Folders);
				}
				return _journal;
			}
		}

		///<summary>A line for the capture log (Capture\capture-log.txt).</summary>
		private static void LogCapture(string text) {
			if(_captureLog==null) {
				_captureLog=new CaptureLog(Host.Folders,Host.Now);
			}
			_captureLog.Write(text);
		}

		///<summary>DisableThreadWindowsDuringAcquire and [Twain] ThreadModal as read before the last open.</summary>
		private static bool IsThreadModalInEffect() {
			return DisableThreadWindowsDuringAcquire && (_configInEffect==null || _configInEffect.ThreadModal);
		}

		///<summary>Acquire on a source opened by name (the Mount/Acquire path), thread-modal unless opted out. The next image of a batch
		///that is already offered (state 6) comes without an enable, so nothing is disabled for it.</summary>
		private static DibImage AcquireByName(IntPtr hwnd,long patNum) {
			TwainSession session=Session;
			string sourceName=session.SourceName;//Single mode closes the source before Acquire returns.
			string acquireId=Guid.NewGuid().ToString("N");
			DeviceAcquireScope scope=null;
			if(session.State<6 && IsThreadModalInEffect()) {
				scope=DeviceAcquireScope.Begin(Host,ForegroundRestorerShared,session,null,LogCapture);
			}
			DibImage dib;
			try {
				dib=session.Acquire(hwnd);
			}
			finally {
				scope?.Dispose();
			}
			if(dib!=null) {
				WriteJournalCopy(dib,sourceName,acquireId,patNum,CaptureJournal.DeliveredCaller,null);
			}
			return dib;
		}

		///<summary>The journal copy of an image (design 7.1 "Journal"), before it goes back to the caller or after it went to the quarantine.
		///Never throws; problems go to the capture log.</summary>
		private static string WriteJournalCopy(DibImage dib,string sourceName,string acquireId,long patNum,string delivered,string detail) {
			CaptureRecord record=CaptureRecord.Create(Host.Now(),sourceName,acquireId);
			record.PatNum=patNum;
			record.ListSettings=CaptureSettingsSnapshot.Build(_configInEffect,IsThreadModalInEffect(),Session.IsSafeDisableInEffect);
			int journalKeep=_configInEffect==null ? 200 : _configInEffect.JournalKeep;
			string pathImage=Journal.Write(dib,record,delivered,detail,journalKeep,LogCapture);
			if(pathImage!=null) {
				LogCapture("Journal copy "+System.IO.Path.GetFileName(pathImage)+" of an image from \""+record.SourceName+"\" (acquire "+acquireId
					+", delivered: "+delivered+").");
			}
			return pathImage;
		}

		///<summary>CloseSource. A source opened by name that is still enabled (images of a batch left) is stopped in the safe order, which
		///can wait for the source; that wait is thread-modal like an acquisition.</summary>
		private static void CloseSourceGuarded() {
			TwainSession session=Session;
			DeviceAcquireScope scope=null;
			if(session.State>=5 && session.IsOpenedByName && !session.IsAcquiring && IsThreadModalInEffect()) {
				scope=DeviceAcquireScope.Begin(Host,ForegroundRestorerShared,session,null,LogCapture);
			}
			try {
				session.CloseSource();
			}
			finally {
				scope?.Dispose();
			}
		}

		///<summary>Before an open: a source still enabled is closed first, as CloseSource does, so that the stop runs with the settings
		///it was enabled with.</summary>
		private static void CloseEnabledSourceFirst() {
			if(Session.State>=5 && !Session.IsAcquiring) {
				CloseSourceGuarded();
			}
		}

		private static void ResetCapture(CaptureHost host) {
			HostOverride=host;
			_configInEffect=null;
			_foregroundRestorer=null;
			_captureLog=null;
			_journal=null;
			DisableThreadWindowsDuringAcquire=true;
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
