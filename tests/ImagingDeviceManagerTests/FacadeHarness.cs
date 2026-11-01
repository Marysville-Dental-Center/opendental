// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImagingDeviceManager.Acquire;
using ImagingDeviceManager.Config;
using ImagingDeviceManager.Journal;
using ImagingDeviceManager.TwainProtocol;
using ImagingDeviceManager.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingDeviceManager.Tests {
	///<summary>Records what the library would show the operator.</summary>
	internal sealed class RecordingCaptureUi:ICaptureUi {
		public readonly List<string> ListNotices=new List<string>();
		public readonly List<RecordedStatus> ListStatus=new List<RecordedStatus>();

		///<summary>A status window that was shown.</summary>
		public sealed class RecordedStatus:IAcquireStatusView {
			public AcquireStatusController Controller;
			public bool IsClosed;

			public void Close() {
				IsClosed=true;
			}
		}

		///<summary>The last status window shown, or null.</summary>
		public RecordedStatus StatusLast => ListStatus.LastOrDefault();

		public void ShowNotice(string text) {
			ListNotices.Add(text);
		}

		public IAcquireStatusView ShowStatus(AcquireStatusController controller) {
			RecordedStatus recordedStatus=new RecordedStatus { Controller=controller };
			ListStatus.Add(recordedStatus);
			return recordedStatus;
		}

		public bool IsLibraryWindow(IntPtr hwnd) {
			return false;
		}
	}

	///<summary>Scripted windows for ForegroundRestorer: each window belongs to this process or to another one.</summary>
	internal sealed class FakeForegroundApi:IForegroundApi {
		public IntPtr HwndForeground;
		public readonly HashSet<IntPtr> HashSetOwnWindows=new HashSet<IntPtr>();
		public readonly HashSet<IntPtr> HashSetOtherWindows=new HashSet<IntPtr>();
		public readonly HashSet<IntPtr> HashSetDisabled=new HashSet<IntPtr>();
		public readonly HashSet<IntPtr> HashSetHidden=new HashSet<IntPtr>();
		public readonly List<IntPtr> ListSetForeground=new List<IntPtr>();

		public IntPtr GetForegroundWindow() {
			return HwndForeground;
		}

		public void SetForegroundWindow(IntPtr hwnd) {
			ListSetForeground.Add(hwnd);
			HwndForeground=hwnd;
		}

		public bool IsWindow(IntPtr hwnd) {
			return HashSetOwnWindows.Contains(hwnd) || HashSetOtherWindows.Contains(hwnd);
		}

		public bool IsWindowVisible(IntPtr hwnd) {
			return IsWindow(hwnd) && !HashSetHidden.Contains(hwnd);
		}

		public bool IsWindowEnabled(IntPtr hwnd) {
			return IsWindow(hwnd) && !HashSetDisabled.Contains(hwnd);
		}

		public bool IsOwnProcessWindow(IntPtr hwnd) {
			return HashSetOwnWindows.Contains(hwnd);
		}
	}

	///<summary>Holds the scheduled action until the test runs it.</summary>
	internal sealed class ManualDelayScheduler:IDelayScheduler {
		public Action ActionPending;
		public int DelayMsPending;
		public int CountCancel;

		public void Schedule(int delayMs,Action action) {
			ActionPending=action;
			DelayMsPending=delayMs;
		}

		public void Cancel() {
			ActionPending=null;
			CountCancel++;
		}

		///<summary>Runs the pending action, as the timer would.</summary>
		public void Elapse() {
			Action action=ActionPending;
			ActionPending=null;
			action?.Invoke();
		}
	}

	///<summary>The EZTwain facade on a FakeTwainDsm that also offers a FakeHelperSource (the contract 1.1 profile), with capture.ini and
	///the capture folders in a temporary folder and the windows recorded. Each test class creates one in its TestInitialize and disposes
	///it in TestCleanup.</summary>
	internal sealed class FacadeHarness:IDisposable {
		public readonly FakeTwainDsm Fake=new FakeTwainDsm();
		public readonly FakeHelperSource Helper=new FakeHelperSource();
		public readonly RecordingCaptureUi Ui=new RecordingCaptureUi();
		public readonly FakeForegroundApi Foreground=new FakeForegroundApi();
		public readonly ManualDelayScheduler Scheduler=new ManualDelayScheduler();
		///<summary>Stands in for %ProgramData%\OpenPMS-Dental\Imaging.</summary>
		public readonly string FolderImaging;
		public readonly string PathConfig;
		public readonly CaptureHost Host;
		public readonly List<string> ListSessionLog=new List<string>();
		///<summary>The time the host reports next; each call moves it on by a second, so receive times and file names differ.</summary>
		public DateTime Now=new DateTime(2026,10,25,9,0,0);

		private DateTime NowNext() {
			DateTime now=Now;
			Now=Now.AddSeconds(1);
			return now;
		}

		public string FolderJournal => Path.Combine(FolderImaging,CaptureFolders.SubfolderJournal);
		public string FolderQuarantine => Path.Combine(FolderImaging,CaptureFolders.SubfolderQuarantine);
		public string PathCaptureLog => Path.Combine(FolderImaging,CaptureFolders.SubfolderCapture,CaptureLog.FileName);

		///<summary>The image files in a capture folder, oldest first. Empty when the folder does not exist.</summary>
		public static List<string> ListImages(string folder) {
			if(!Directory.Exists(folder)) {
				return new List<string>();
			}
			return Directory.GetFiles(folder,"*"+CaptureFiles.ImageExtension).OrderBy(x => x,StringComparer.Ordinal).ToList();
		}

		public static string SidecarOf(string pathImage) {
			return Path.ChangeExtension(pathImage,CaptureFiles.SidecarExtension);
		}

		public FacadeHarness() {
			Fake.AddHelperSource(Helper);
			FolderImaging=Path.Combine(Path.GetTempPath(),"ImagingDeviceManagerTests_"+Guid.NewGuid().ToString("N"),"Imaging");
			Directory.CreateDirectory(FolderImaging);
			PathConfig=Path.Combine(FolderImaging,LocalCaptureConfig.FileName);
			Host=new CaptureHost {
				LoadConfig=() => LocalCaptureConfig.Load(PathConfig),
				Ui=Ui,
				ThreadWindows=new Win32ThreadWindowApi(),
				Foreground=Foreground,
				Scheduler=Scheduler,
				Folders=CaptureFolders.CreateForFolder(FolderImaging),
				Now=() => NowNext(),
				AdjustSettings=settings => settings.RefusedDisableWait=TimeSpan.FromMilliseconds(2000),
			};
			EZTwain.Reset(Load,Host);
			EZTwain.Session.LoopTimeoutMilliseconds=10000;//A broken test fails instead of hanging.
			EZTwain.Session.TickMilliseconds=20;
			EZTwain.Session.Logged+=ListSessionLog.Add;
			TestWin32.FinalizePending();
		}

		private ITwainDsm Load(out string errorText) {
			errorText="";
			return Fake;
		}

		///<summary>Writes capture.ini. It is read again before every open.</summary>
		public void WriteConfig(params string[] lines) {
			File.WriteAllLines(PathConfig,lines);
		}

		///<summary>[Twain] keys that make the helper's disable safe, with the contract 1.1 numbers entered as data.</summary>
		public void WriteSafeDisableConfig(params string[] linesMore) {
			List<string> listLines=new List<string> {
				"[Twain]",
				"DisableIsSafeSources="+Helper.ProductName,
				"SafeDisableCap=0x"+FakeHelperSource.CapSafeDisable.ToString("X4"),
				"RefusedDisableCodes=0x"+FakeHelperSource.CodeDisableImagesPending.ToString("X4")+"|0x"+FakeHelperSource.CodeDisableClosing.ToString("X4"),
			};
			listLines.AddRange(linesMore);
			WriteConfig(listLines.ToArray());
		}

		public string LogText => string.Join("\r\n",ListSessionLog);

		public bool HasNotice(string fragment) {
			return Ui.ListNotices.Any(x => x.IndexOf(fragment,StringComparison.Ordinal)>=0);
		}

		public void Dispose() {
			EZTwain.Reset(null);
			//These tests run Win32 message loops on the test adapter's STA thread; see EZTwainTests.Cleanup.
			TestWin32.FinalizePending();
			try {
				Directory.Delete(Path.GetDirectoryName(FolderImaging),true);
			}
			catch {
			}
		}
	}
}
