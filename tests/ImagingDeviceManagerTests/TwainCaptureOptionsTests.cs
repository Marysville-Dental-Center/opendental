// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ImagingDeviceManager.Capture;
using ImagingDeviceManager.Config;
using ImagingDeviceManager.TwainProtocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingDeviceManager.Tests {
	///<summary>TwainCaptureOptions.FromConfig: the [Device.N] keys of design 9.3 with their defaults, and the [Twain] keys that apply.</summary>
	[TestClass]
	public class TwainCaptureOptionsTests {
		private string _tempDir;

		[TestInitialize]
		public void Setup() {
			_tempDir=Path.Combine(Path.GetTempPath(),"TwainCaptureOptionsTests_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_tempDir);
		}

		[TestCleanup]
		public void Cleanup() {
			try {
				Directory.Delete(_tempDir,true);
			}
			catch {
			}
		}

		private LocalCaptureConfig Config(params string[] arrayLines) {
			string path=Path.Combine(_tempDir,"capture.ini");
			File.WriteAllBytes(path,new UTF8Encoding(false).GetBytes(string.Join("\r\n",arrayLines)+"\r\n"));
			return LocalCaptureConfig.Load(path);
		}

		[TestMethod]
		public void MissingFile_Design93Defaults() {
			TwainCaptureOptions options=TwainCaptureOptions.FromConfig(LocalCaptureConfig.Load(Path.Combine(_tempDir,"none.ini")),12);
			Assert.AreEqual(12,options.ImagingDeviceNum);
			Assert.AreEqual(TwainSessionMode.PerSlot,options.SessionMode);
			Assert.AreEqual(8,options.Bits);
			Assert.AreEqual(TwainByteOrder.LE,options.ByteOrder16);
			Assert.AreEqual(TwainPolarity.AsDelivered,options.Polarity);
			Assert.IsFalse(options.Headless);
			Assert.IsFalse(options.DisableIsSafe);
			Assert.AreEqual(TimeSpan.Zero,options.SessionTimeout);
			Assert.IsNull(options.SafeDisableCap);
			Assert.IsNull(options.ContinuousCap);
			Assert.IsNull(options.RedeliverCap);
			Assert.IsNull(options.NoActivateCap);
			Assert.AreEqual(0,options.RefusedDisableCodes.Count);
			Assert.IsFalse(options.Allow16Bit);
			Assert.IsFalse(options.Is16BitConfigured);
			Assert.AreEqual(200,options.JournalKeep);
			Assert.AreEqual(0,options.ListWarnings.Count);
			Assert.AreEqual(0,TwainCaptureOptions.FromConfig(null,3).ListWarnings.Count);
		}

		[TestMethod]
		public void EveryKey_ReadFromItsDeviceSection() {
			TwainCaptureOptions options=TwainCaptureOptions.FromConfig(Config(
				"[Twain]","Allow16Bit=1","RestrictSources=1","AllowedSources=A | B","JournalKeep=50",
				"SafeDisableCap=0x1111","RefusedDisableCodes=0x2222",
				"[Device.11]","Bits=16","SessionMode=Continuous",
				"[Device.12]","SessionMode=continuous","Bits=16","ByteOrder16=BE","Polarity=Invert","Headless=1","DisableIsSafe=1",
				"SessionTimeoutMinutes=7","SafeDisableCap=0x8002","RefusedDisableCodes=0x8006|8007","ContinuousCap=8001","RedeliverCap=0x8003",
				"NoActivateCap=0x8004"),12);
			Assert.AreEqual(TwainSessionMode.Continuous,options.SessionMode);
			Assert.AreEqual(16,options.Bits);
			Assert.AreEqual(TwainByteOrder.BE,options.ByteOrder16);
			Assert.AreEqual(TwainPolarity.Invert,options.Polarity);
			Assert.IsTrue(options.Headless);
			Assert.IsTrue(options.DisableIsSafe);
			Assert.AreEqual(TimeSpan.FromMinutes(7),options.SessionTimeout);
			Assert.AreEqual((ushort)0x8002,options.SafeDisableCap,"[Device.N], not [Twain].");
			CollectionAssert.AreEqual(new List<ushort> { 0x8006,0x8007 },options.RefusedDisableCodes);
			Assert.AreEqual((ushort)0x8001,options.ContinuousCap);
			Assert.AreEqual((ushort)0x8003,options.RedeliverCap);
			Assert.AreEqual((ushort)0x8004,options.NoActivateCap);
			Assert.IsTrue(options.Allow16Bit);
			Assert.IsTrue(options.Is16BitConfigured);
			Assert.IsTrue(options.RestrictSources);
			CollectionAssert.AreEqual(new List<string> { "A","B" },options.AllowedSources);
			Assert.AreEqual(50,options.JournalKeep);
			Assert.AreEqual(0,options.ListWarnings.Count,string.Join("; ",options.ListWarnings));
		}

		[TestMethod]
		public void IsSourceAllowed_TheSourceGateOfTheOptions() {
			TwainCaptureOptions options=new TwainCaptureOptions();
			Assert.IsTrue(options.IsSourceAllowed("Any Source"),"Without RestrictSources every source may be opened.");
			options.RestrictSources=true;
			options.AllowedSources=new List<string> { "Sensor DS","Scanner" };
			Assert.IsTrue(options.IsSourceAllowed(" sensor ds "),"Ignoring case and surrounding spaces, as Open's gate.");
			Assert.IsFalse(options.IsSourceAllowed("Sensor  DS"));
			Assert.IsFalse(options.IsSourceAllowed("Other"));
			Assert.IsFalse(options.IsSourceAllowed(""));
			Assert.IsFalse(options.IsSourceAllowed(null));
		}

		[TestMethod]
		public void OtherDevicesSections_AndTwainCaps_NotUsed() {
			TwainCaptureOptions options=TwainCaptureOptions.FromConfig(Config(
				"[Twain]","SafeDisableCap=0x8002","DisableIsSafeSources=X",
				"[Device.5]","Bits=16","DisableIsSafe=1"),12);
			Assert.AreEqual(8,options.Bits);
			Assert.IsFalse(options.DisableIsSafe);
			Assert.IsNull(options.SafeDisableCap,"The session path reads the device profile only.");
		}

		[TestMethod]
		public void InvalidValues_DefaultsAndWarnings() {
			TwainCaptureOptions options=TwainCaptureOptions.FromConfig(Config(
				"[Device.12]","Bits=12","SessionMode=Batch","ByteOrder16=1","Polarity=Reverse","SessionTimeoutMinutes=-3","SafeDisableCap=zz",
				"RedeliverCap=0x10000","ContinuousCap=0","RefusedDisableCodes=0x8006|nope|0"),12);
			Assert.AreEqual(8,options.Bits);
			Assert.AreEqual(TwainSessionMode.PerSlot,options.SessionMode);
			Assert.AreEqual(TwainByteOrder.LE,options.ByteOrder16,"A number is not a name.");
			Assert.AreEqual(TwainPolarity.AsDelivered,options.Polarity);
			Assert.AreEqual(TimeSpan.Zero,options.SessionTimeout);
			Assert.IsNull(options.SafeDisableCap);
			Assert.IsNull(options.RedeliverCap);
			Assert.IsNull(options.ContinuousCap);
			CollectionAssert.AreEqual(new List<ushort> { 0x8006 },options.RefusedDisableCodes);
			Assert.AreEqual(7,options.ListWarnings.Count,string.Join("\r\n",options.ListWarnings));
			Assert.IsTrue(options.ListWarnings.Any(x => x.Contains("[Device.12] Bits=12")));
		}

		[TestMethod]
		public void SessionSettings_SafeDisableOnlyWithDisableIsSafe() {
			TwainCaptureOptions options=new TwainCaptureOptions { SafeDisableCap=0x8002,RefusedDisableCodes=new List<ushort> { 0x8006 } };
			TwainCaptureSettings settings=options.ToSessionSettings("Src");
			Assert.IsNull(settings.SafeDisableCap,"DisableIsSafe=0: not set.");
			CollectionAssert.AreEqual(new List<ushort> { 0x8006 },settings.ListRefusedDisableCodes);
			options.DisableIsSafe=true;
			settings=options.ToSessionSettings("Src");
			Assert.AreEqual((ushort)0x8002,settings.SafeDisableCap);
			Assert.IsTrue(settings.IsDisableSafeSource("src"));
			Assert.AreEqual(TimeSpan.Zero,settings.AcquireTimeout);
		}

		[TestMethod]
		public void Clone_CopiesTheLists() {
			TwainCaptureOptions options=new TwainCaptureOptions { RefusedDisableCodes=new List<ushort> { 1 } };
			TwainCaptureOptions clone=options.Clone();
			clone.RefusedDisableCodes.Add(2);
			Assert.AreEqual(1,options.RefusedDisableCodes.Count);
		}
	}
}
