// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImagingDeviceManager.Acquire;
using ImagingDeviceManager.Config;
using ImagingDeviceManager.TwainProtocol;
using ImagingDeviceManager.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingDeviceManager.Tests {
	///<summary>Records what the library would show the operator.</summary>
	internal sealed class RecordingCaptureUi:ICaptureUi {
		public readonly List<string> ListNotices=new List<string>();

		public void ShowNotice(string text) {
			ListNotices.Add(text);
		}

		public bool IsLibraryWindow(IntPtr hwnd) {
			return false;
		}
	}

	///<summary>The EZTwain facade on a FakeTwainDsm that also offers a FakeHelperSource (the contract 1.1 profile), with capture.ini and
	///the capture folders in a temporary folder and the windows recorded. Each test class creates one in its TestInitialize and disposes
	///it in TestCleanup.</summary>
	internal sealed class FacadeHarness:IDisposable {
		public readonly FakeTwainDsm Fake=new FakeTwainDsm();
		public readonly FakeHelperSource Helper=new FakeHelperSource();
		public readonly RecordingCaptureUi Ui=new RecordingCaptureUi();
		///<summary>Stands in for %ProgramData%\OpenPMS-Dental\Imaging.</summary>
		public readonly string FolderImaging;
		public readonly string PathConfig;
		public readonly CaptureHost Host;
		public readonly List<string> ListSessionLog=new List<string>();

		public FacadeHarness() {
			Fake.AddHelperSource(Helper);
			FolderImaging=Path.Combine(Path.GetTempPath(),"ImagingDeviceManagerTests_"+Guid.NewGuid().ToString("N"),"Imaging");
			Directory.CreateDirectory(FolderImaging);
			PathConfig=Path.Combine(FolderImaging,LocalCaptureConfig.FileName);
			Host=new CaptureHost {
				LoadConfig=() => LocalCaptureConfig.Load(PathConfig),
				Ui=Ui,
				AdjustSettings=settings => settings.RefusedDisableWait=TimeSpan.FromMilliseconds(2000),
			};
			EZTwain.Reset(Load,Host);
			EZTwain.Session.LoopTimeoutMilliseconds=10000;//A broken test fails instead of hanging.
			EZTwain.Session.TickMilliseconds=20;
			EZTwain.Session.Logged+=ListSessionLog.Add;
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
			GC.Collect();
			GC.WaitForPendingFinalizers();
			try {
				Directory.Delete(Path.GetDirectoryName(FolderImaging),true);
			}
			catch {
			}
		}
	}
}
