// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ImagingDeviceManager.Imaging;
using ImagingDeviceManager.TwainProtocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingDeviceManager.Tests {
	///<summary>What the session negotiates in state 4 before MSG_ENABLEDS (design 7.1): honest CAP_UICONTROLLABLE, no forced pixel
	///type on the by-name path.</summary>
	[TestClass]
	public class TwainNegotiationTests {
		private SessionHarness _harness;

		[TestInitialize]
		public void Setup() {
			_harness=new SessionHarness();
			_harness.Fake.ListPages.Add(FakePage.CreateRgb(6,4,300,(x,y) => Color.Gray));
		}

		[TestCleanup]
		public void Cleanup() {
			_harness.Dispose();
		}

		private List<ushort> CapsSet() {
			return _harness.Fake.CallsNamed("CAP/SET").Select(x => x.Cap).ToList();
		}

		#region CAP_UICONTROLLABLE
		[TestMethod]
		public void ByName_UiControllableFalse_AlwaysShowUIAndLogged() {
			_harness.Helper.ListExposures.Add(SessionHarness.Exposure(8));
			_harness.Session.HideUI=true;
			Assert.IsNotNull(_harness.AcquireByName(),_harness.Session.ErrorText);
			Assert.AreEqual(1,_harness.Helper.LastShowUI,"A source that cannot hide its window gets ShowUI=1.");
			Assert.AreEqual(false,_harness.Session.IsUiControllable);
			Assert.IsTrue(_harness.HasLog("CAP_UICONTROLLABLE FALSE"),_harness.LogText);
		}

		[TestMethod]
		public void ByName_UiControllableTrue_HonoursHideUI() {
			_harness.Fake.UiControllable=true;
			_harness.Session.HideUI=true;
			Assert.IsTrue(_harness.Session.OpenSource("Fake Sensor"));
			Assert.IsNotNull(_harness.Session.Acquire(IntPtr.Zero));
			Assert.AreEqual(0,_harness.Fake.LastShowUI);
			Assert.AreEqual(true,_harness.Session.IsUiControllable);
		}

		[TestMethod]
		public void ByName_UiControllableNotSupported_HonoursHideUI() {
			_harness.Session.HideUI=true;
			Assert.IsTrue(_harness.Session.OpenSource("Fake Sensor"));
			Assert.IsNotNull(_harness.Session.Acquire(IntPtr.Zero));
			Assert.AreEqual(0,_harness.Fake.LastShowUI);
			Assert.IsNull(_harness.Session.IsUiControllable);
		}

		[TestMethod]
		public void DefaultSource_UiControllableFalse_ScanUnchanged() {
			_harness.Fake.UiControllable=false;
			_harness.Session.HideUI=true;
			Assert.IsTrue(_harness.Session.OpenSource(null));
			Assert.IsNotNull(_harness.Session.Acquire(IntPtr.Zero));
			Assert.AreEqual(0,_harness.Fake.LastShowUI,"Scans keep sending what the caller asked for.");
			Assert.IsFalse(_harness.Fake.ListCalls.Any(x => x.Cap==Tw.CAP_UICONTROLLABLE),"Scans do not read it.");
		}
		#endregion CAP_UICONTROLLABLE

		#region Pixel type
		[TestMethod]
		public void ByName_SingleAndMulti_NeverSetPixelTypeOrAnyFixedCapability() {
			//The sensor source rejects a pixel type other than its own (twain-contract 1.1 section 4), and the Mount/Acquire path
			//never asks for one.
			_harness.Helper.ListExposures.Add(SessionHarness.Exposure(8));
			_harness.Helper.ListExposures.Add(SessionHarness.Exposure(9));
			Assert.IsNotNull(_harness.AcquireByName(),_harness.Session.ErrorText);
			Assert.IsTrue(_harness.Session.OpenSource(_harness.Helper.ProductName));
			_harness.Session.MultiTransfer=true;
			DibImage dib=_harness.Session.Acquire(IntPtr.Zero);
			Assert.IsNotNull(dib,_harness.Session.ErrorText);
			Assert.AreEqual(8,dib.BitCount,"8-bit gray as delivered.");
			Assert.IsNotNull(_harness.Session.Acquire(IntPtr.Zero));
			_harness.Session.CloseSource();
			Assert.IsFalse(CapsSet().Contains(Tw.ICAP_PIXELTYPE));
			CollectionAssert.AreEquivalent(new[] { Tw.CAP_XFERCOUNT,Tw.ICAP_XFERMECH },CapsSet().Distinct().ToArray(),
				"Only the transfer count and mechanism are set: "+_harness.CallText);
			Assert.IsFalse(_harness.Fake.ListCalls.Any(x => x.Condition==Tw.TWCC_BADVALUE),"No value was refused.");
		}

		[TestMethod]
		public void ByName_ThroughTheFacade_NeverSetsPixelType() {
			FakeTwainDsm fake=_harness.Fake;
			_harness.Helper.ListExposures.Add(SessionHarness.Exposure(8));
			EZTwain.Reset((out string errorText) => {
				errorText="";
				return fake;
			});
			try {
				EZTwain.Session.LoopTimeoutMilliseconds=10000;
				EZTwain.SetHideUI(false);
				Assert.IsTrue(EZTwain.OpenSource(_harness.Helper.ProductName));
				IntPtr hdib=EZTwain.Acquire(IntPtr.Zero);
				Assert.AreNotEqual(IntPtr.Zero,hdib,EZTwain.LastErrorText());
				Assert.AreEqual(8,EZTwain.DIB_Depth(hdib));
				Assert.AreEqual(FakeHelperSource.Dpi,EZTwain.DIB_XResolution(hdib));
				EZTwain.DIB_Free(hdib);
				Assert.IsFalse(CapsSet().Contains(Tw.ICAP_PIXELTYPE));
			}
			finally {
				EZTwain.Reset(null);
			}
		}
		#endregion Pixel type
	}
}
