// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImagingDeviceManager.TwainProtocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingDeviceManager.Tests {
	///<summary>The source gate (design 7.1): with [Twain] RestrictSources=1, OpenSource and OpenDefaultSource refuse any ProductName not
	///listed in [Twain] AllowedSources, before MSG_OPENDS, and say "This TWAIN source is not enabled on this computer."</summary>
	[TestClass]
	public class SourceGateTests {
		private FacadeHarness _harness;

		[TestInitialize]
		public void Setup() {
			_harness=new FacadeHarness();
		}

		[TestCleanup]
		public void Cleanup() {
			_harness.Dispose();
		}

		private void AssertRefused(EZTwainErrorCode errorCodeExpected) {
			Assert.AreEqual((int)errorCodeExpected,EZTwain.LastErrorCode());
			Assert.AreEqual(TwainSourceGate.RefusedText,EZTwain.LastErrorText());
			CollectionAssert.AreEqual(new List<string> { TwainSourceGate.RefusedText },_harness.Ui.ListNotices,"The callers show only their own text.");
			Assert.IsFalse(_harness.Fake.ListLog.Any(x => x.StartsWith("OPENDS ")),"MSG_OPENDS is never sent to a refused source.");
			Assert.IsFalse(_harness.Fake.IsDsmOpen);
			Assert.AreEqual(2,EZTwain.Session.State);
		}

		[TestMethod]
		public void NoConfig_EverySourceOpens() {
			Assert.IsTrue(EZTwain.OpenSource("Fake Sensor"),EZTwain.LastErrorText());
			EZTwain.CloseSource();
			Assert.IsTrue(EZTwain.OpenDefaultSource(),EZTwain.LastErrorText());
			EZTwain.CloseSource();
			Assert.AreEqual(0,_harness.Ui.ListNotices.Count);
		}

		[TestMethod]
		public void RestrictSourcesZero_ListIgnored() {
			_harness.WriteConfig("[Twain]","RestrictSources=0","AllowedSources=Something Else");
			Assert.IsTrue(EZTwain.OpenSource("Fake Sensor"),EZTwain.LastErrorText());
			EZTwain.CloseSource();
		}

		[TestMethod]
		public void OpenSource_NotListed_Refused() {
			_harness.WriteConfig("[Twain]","RestrictSources=1","AllowedSources=Fake Scanner");
			Assert.IsFalse(EZTwain.OpenSource("Fake Sensor"));
			AssertRefused(EZTwainErrorCode.EZTEC_OPEN_DS);
			StringAssert.Contains(_harness.LogText,"\"Fake Sensor\"");
		}

		[TestMethod]
		public void OpenSource_Listed_OpensIgnoringCaseAndSpaces() {
			_harness.WriteConfig("[Twain]","RestrictSources=1","AllowedSources=Fake Scanner|  fake SENSOR  ");
			Assert.IsTrue(EZTwain.OpenSource("Fake Sensor"),EZTwain.LastErrorText());
			Assert.AreEqual("Fake Sensor",_harness.Fake.OpenSourceName);
			EZTwain.CloseSource();
			Assert.AreEqual(0,_harness.Ui.ListNotices.Count);
		}

		[TestMethod]
		public void OpenSource_GateChecksTheProductNameFound_NotTheNameAsked() {
			//OpenSource matches a name prefix; the gate checks the source that would really open.
			_harness.WriteConfig("[Twain]","RestrictSources=1","AllowedSources=Fake");
			Assert.IsFalse(EZTwain.OpenSource("Fake"));
			AssertRefused(EZTwainErrorCode.EZTEC_OPEN_DS);
		}

		[TestMethod]
		public void OpenDefaultSource_NotListed_Refused() {
			_harness.Fake.DefaultSource="Fake Sensor";
			_harness.WriteConfig("[Twain]","RestrictSources=1","AllowedSources=Fake Scanner");
			Assert.IsFalse(EZTwain.OpenDefaultSource());
			AssertRefused(EZTwainErrorCode.EZTEC_OPEN_DEFAULT_DS);
		}

		[TestMethod]
		public void OpenDefaultSource_Listed_ScanUnchanged() {
			_harness.WriteConfig("[Twain]","RestrictSources=1","AllowedSources=Fake Scanner");
			_harness.Fake.ListPages.Add(FakePage.CreateGray8(8,4,300,(x,y) => (byte)x));
			Assert.IsTrue(EZTwain.OpenDefaultSource(),EZTwain.LastErrorText());
			IntPtr hdib=EZTwain.Acquire(IntPtr.Zero);
			Assert.AreNotEqual(IntPtr.Zero,hdib,EZTwain.LastErrorText());
			EZTwain.DIB_Free(hdib);
			Assert.AreEqual(0,_harness.Ui.ListNotices.Count);
		}

		[TestMethod]
		public void Acquire_OpensTheDefaultSource_GateApplies() {
			_harness.Fake.DefaultSource="Fake Sensor";
			_harness.WriteConfig("[Twain]","RestrictSources=1","AllowedSources=Fake Scanner");
			Assert.AreEqual(IntPtr.Zero,EZTwain.Acquire(IntPtr.Zero));
			AssertRefused(EZTwainErrorCode.EZTEC_OPEN_DEFAULT_DS);
		}

		[TestMethod]
		public void AcquireMultipageFile_OpensTheDefaultSource_GateApplies() {
			_harness.Fake.DefaultSource="Fake Sensor";
			_harness.WriteConfig("[Twain]","RestrictSources=1","AllowedSources=Fake Scanner");
			string path=Path.Combine(_harness.FolderImaging,"scan.pdf");
			Assert.AreEqual(0,EZTwain.AcquireMultipageFile(IntPtr.Zero,path));
			AssertRefused(EZTwainErrorCode.EZTEC_OPEN_DEFAULT_DS);
			Assert.IsFalse(File.Exists(path));
		}

		[TestMethod]
		public void ConfigIsReadAgainBeforeEveryOpen() {
			_harness.WriteConfig("[Twain]","RestrictSources=1","AllowedSources=Fake Scanner");
			Assert.IsFalse(EZTwain.OpenSource("Fake Sensor"));
			_harness.WriteConfig("[Twain]","RestrictSources=1","AllowedSources=Fake Scanner|Fake Sensor");
			Assert.IsTrue(EZTwain.OpenSource("Fake Sensor"),EZTwain.LastErrorText());
			EZTwain.CloseSource();
		}

		[TestMethod]
		public void Gate_IsAllowed() {
			TwainSourceGate gate=new TwainSourceGate();
			Assert.IsTrue(gate.IsAllowed("Anything"),"Not restricted by default.");
			Assert.IsTrue(gate.IsAllowed(""));
			gate.IsRestricted=true;
			Assert.IsFalse(gate.IsAllowed("Anything"),"Restricted with an empty list: nothing opens.");
			gate.ListAllowedSources=new List<string> { "Scanner A"," Sensor B ","" };
			Assert.IsTrue(gate.IsAllowed("scanner a"));
			Assert.IsTrue(gate.IsAllowed("Sensor B"));
			Assert.IsFalse(gate.IsAllowed("Scanner"),"Whole names only.");
			Assert.IsFalse(gate.IsAllowed(""),"A source without a name is never listed.");
			Assert.IsFalse(gate.IsAllowed(null));
		}
	}
}
