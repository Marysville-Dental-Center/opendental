// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using ImagingDeviceManager.Capture;
using ImagingDeviceManager.Journal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingDeviceManager.Tests {
	///<summary>CaptureQuarantine.GetUnassignedText, the quarantine line Test capture and the session window show (design 7.12 "Visibility").</summary>
	[TestClass]
	public class CaptureQuarantineTests {
		private string _folderImaging;

		[TestInitialize]
		public void Setup() {
			_folderImaging=Path.Combine(Path.GetTempPath(),"ImagingDeviceManagerTests_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_folderImaging);
		}

		[TestCleanup]
		public void Cleanup() {
			try {
				Directory.Delete(_folderImaging,true);
			}
			catch {
			}
		}

		[TestMethod]
		public void UnassignedText_NullWhenEmpty_CountOtherwise() {
			CaptureFolders captureFolders=CaptureFolders.CreateForFolder(_folderImaging);
			DateTime now=new DateTime(2026,10,31,12,0,0);
			Assert.IsNull(CaptureQuarantine.GetUnassignedText(captureFolders,now));
			Assert.IsFalse(Directory.Exists(Path.Combine(_folderImaging,@"Capture\quarantine")),"Nothing is created.");
			string folder=Path.Combine(_folderImaging,@"Capture\quarantine\session-1");
			Directory.CreateDirectory(folder);
			File.WriteAllBytes(Path.Combine(folder,"a.png"),new byte[] { 1 });
			File.SetLastWriteTime(Path.Combine(folder,"a.png"),new DateTime(2026,10,30));
			Assert.AreEqual("1 unassigned capture(s) on this computer.",CaptureQuarantine.GetUnassignedText(captureFolders,now));
			File.SetLastWriteTime(Path.Combine(folder,"a.png"),new DateTime(2026,9,1));
			StringAssert.Contains(CaptureQuarantine.GetUnassignedText(captureFolders,now),"The oldest is from 2026-09-01.");
			Assert.IsNull(CaptureQuarantine.GetUnassignedText(CaptureFolders.CreateForFolder(null),now),"An unknown folder counts nothing.");
		}
	}
}
