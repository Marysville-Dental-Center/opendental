// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using ImagingDeviceManager.Config;
using ImagingDeviceManager.Imaging;
using ImagingDeviceManager.TwainProtocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingDeviceManager.Tests {
	///<summary>What the session negotiates in state 4 before MSG_ENABLEDS (design 7.1): honest CAP_UICONTROLLABLE, the safe-disable
	///capability, and no forced pixel type on the by-name path.</summary>
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

		///<summary>[Twain] keys that make the helper source's disable safe, with the published numbers as data.</summary>
		private void ConfigureSafeDisable() {
			_harness.Session.Settings=new TwainCaptureSettings {
				SafeDisableCap=FakeHelperSource.CapSafeDisable,
				ListDisableIsSafeSources=new List<string> { "Other Source"," "+_harness.Helper.ProductName.ToUpper()+" " },
				ListRefusedDisableCodes=new List<ushort> { FakeHelperSource.CodeDisableImagesPending,FakeHelperSource.CodeDisableClosing },
			};
		}

		private bool AnyCallFor(ushort capability) {
			return _harness.Fake.ListCalls.Any(x => x.Dat==Tw.DAT_CAPABILITY && x.Cap==capability);
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
		public void ByName_UiControllableFalse_OnlyMsgGet_StillForcesShowUI() {
			_harness.Fake.UiControllable=false;
			_harness.Fake.UiControllableOnlyMsg=Tw.MSG_GET;//TWAIN requires only MSG_GET for it (p.5-13).
			_harness.Session.HideUI=true;
			Assert.IsTrue(_harness.Session.OpenSource("Fake Sensor"));
			Assert.IsNotNull(_harness.Session.Acquire(IntPtr.Zero));
			Assert.AreEqual(1,_harness.Fake.LastShowUI);
			Assert.AreEqual(false,_harness.Session.IsUiControllable);
			Assert.IsTrue(_harness.Fake.ListCalls.Any(x => x.Name=="CAP/GET" && x.Cap==Tw.CAP_UICONTROLLABLE && x.Rc==Tw.TWRC_SUCCESS),
				_harness.CallText);
		}

		[TestMethod]
		public void ByName_UiControllableFalse_OnlyMsgGetCurrent_FallbackForcesShowUI() {
			_harness.Fake.UiControllable=false;
			_harness.Fake.UiControllableOnlyMsg=Tw.MSG_GETCURRENT;
			_harness.Session.HideUI=true;
			Assert.IsTrue(_harness.Session.OpenSource("Fake Sensor"));
			Assert.IsNotNull(_harness.Session.Acquire(IntPtr.Zero));
			Assert.AreEqual(1,_harness.Fake.LastShowUI);
			Assert.AreEqual(false,_harness.Session.IsUiControllable);
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

		#region Safe-disable capability
		[TestMethod]
		public void SafeDisable_ListedSource_QuerySupportThenSetTrueInState4BeforeEachEnable() {
			ConfigureSafeDisable();
			_harness.Helper.ListExposures.Add(SessionHarness.Exposure(8));
			Assert.IsTrue(_harness.Session.OpenSource(_harness.Helper.ProductName));
			_harness.Session.MultiTransfer=true;//The source stays open in state 4 between the two enables.
			Assert.IsNotNull(_harness.Session.Acquire(IntPtr.Zero),_harness.Session.ErrorText);
			Assert.IsTrue(_harness.Session.IsDone());
			Assert.IsNotNull(_harness.Session.Acquire(IntPtr.Zero),_harness.Session.ErrorText);
			Assert.IsTrue(_harness.Session.IsSafeDisableInEffect);
			List<FakeCall> listCalls=_harness.Fake.ListCalls;
			List<int> listEnables=Enumerable.Range(0,listCalls.Count).Where(i => listCalls[i].Name=="ENABLEDS").ToList();
			Assert.AreEqual(2,listEnables.Count);
			int start=0;
			foreach(int idxEnable in listEnables) {
				int idxQuery=_harness.IndexOfCall(x => x.Name=="CAP/QUERYSUPPORT" && x.Cap==FakeHelperSource.CapSafeDisable,start);
				int idxSet=_harness.IndexOfCall(x => x.Name=="CAP/SET" && x.Cap==FakeHelperSource.CapSafeDisable,start);
				Assert.IsTrue(idxQuery>=0 && idxQuery<idxSet && idxSet<idxEnable,_harness.CallText);
				Assert.AreEqual(1,listCalls[idxSet].Value,"TRUE");
				Assert.AreEqual(4,listCalls[idxSet].StateBefore);
				Assert.AreEqual(Tw.TWRC_SUCCESS,listCalls[idxSet].Rc);
				start=idxEnable+1;
			}
			Assert.AreEqual(1,_harness.Helper.DictCaps[FakeHelperSource.CapSafeDisable]);
			Assert.IsTrue(_harness.HasLog("Safe disable is in effect"),_harness.LogText);
		}

		[TestMethod]
		public void SafeDisable_SourceNotListed_NothingSent() {
			ConfigureSafeDisable();
			_harness.Session.Settings.ListDisableIsSafeSources=new List<string> { "Other Source" };
			_harness.Helper.ListExposures.Add(SessionHarness.Exposure(8));
			Assert.IsNotNull(_harness.AcquireByName());
			Assert.IsFalse(AnyCallFor(FakeHelperSource.CapSafeDisable));
			Assert.IsFalse(_harness.Session.IsSafeDisableInEffect);
		}

		[TestMethod]
		public void SafeDisable_NoCapabilityConfigured_NothingSent() {
			ConfigureSafeDisable();
			_harness.Session.Settings.SafeDisableCap=null;
			_harness.Helper.ListExposures.Add(SessionHarness.Exposure(8));
			Assert.IsNotNull(_harness.AcquireByName());
			Assert.IsFalse(AnyCallFor(FakeHelperSource.CapSafeDisable));
			Assert.IsFalse(_harness.Session.IsSafeDisableInEffect);
		}

		[TestMethod]
		public void SafeDisable_CapabilityUnsupported_ToleratedLoggedAndNotInEffect() {
			ConfigureSafeDisable();
			_harness.Helper.SupportsSafeDisable=false;//The contract 1.0 build.
			_harness.Helper.ListExposures.Add(SessionHarness.Exposure(8));
			Assert.IsNotNull(_harness.AcquireByName(),"The acquisition goes on without the feature.");
			Assert.AreEqual(EZTwainErrorCode.EZTEC_NONE,_harness.Session.ErrorCode);
			Assert.IsFalse(_harness.Session.IsSafeDisableInEffect);
			Assert.AreEqual(0,_harness.Fake.CallsNamed("CAP/SET").Count(x => x.Cap==FakeHelperSource.CapSafeDisable),"No MSG_SET after TWCC_CAPUNSUPPORTED.");
			Assert.IsTrue(_harness.HasLog("does not support the safe-disable capability 0x8002"),_harness.LogText);
		}

		[TestMethod]
		public void SafeDisable_CapabilityNotSettable_NotInEffect() {
			ConfigureSafeDisable();
			_harness.Session.Settings.SafeDisableCap=FakeHelperSource.CapIndicators;//A BOOL the source reports as read-only (0x0D).
			_harness.Helper.ListExposures.Add(SessionHarness.Exposure(8));
			Assert.IsNotNull(_harness.AcquireByName());
			Assert.IsFalse(_harness.Session.IsSafeDisableInEffect);
			Assert.IsFalse(_harness.Fake.CallsNamed("CAP/SET").Any(x => x.Cap==FakeHelperSource.CapIndicators));
			Assert.IsTrue(_harness.HasLog("does not allow setting"),_harness.LogText);
		}

		[TestMethod]
		public void SafeDisable_DefaultSource_NeverSent() {
			ConfigureSafeDisable();
			_harness.Fake.DefaultSource=_harness.Helper.ProductName;
			_harness.Helper.ListExposures.Add(SessionHarness.Exposure(8));
			Assert.IsTrue(_harness.Session.OpenSource(null));
			Assert.IsNotNull(_harness.Session.Acquire(IntPtr.Zero));
			Assert.IsFalse(AnyCallFor(FakeHelperSource.CapSafeDisable),"Scans keep their behaviour.");
		}

		[TestMethod]
		public void Settings_FromCaptureIni() {
			string path=Path.Combine(Path.GetTempPath(),"capture_"+Guid.NewGuid().ToString("N")+".ini");
			try {
				File.WriteAllText(path,"[Twain]\r\nSafeDisableCap=0x8002\r\nRefusedDisableCodes=8006 | 0x8007 | zz | 0x18000\r\n"
					+"DisableIsSafeSources= Sensor A | Sensor B \r\nAcquireTimeoutMinutes=3\r\n");
				TwainCaptureSettings settings=TwainCaptureSettings.FromConfig(LocalCaptureConfig.Load(path));
				Assert.AreEqual((ushort)0x8002,settings.SafeDisableCap);
				CollectionAssert.AreEqual(new List<ushort> { 0x8006,0x8007 },settings.ListRefusedDisableCodes);
				Assert.IsTrue(settings.IsDisableSafeSource("sensor b"));
				Assert.IsFalse(settings.IsDisableSafeSource("Sensor"));
				Assert.AreEqual(TimeSpan.FromMinutes(3),settings.AcquireTimeout);
				Assert.AreEqual(TimeSpan.FromSeconds(30),settings.RefusedDisableWait);
				File.WriteAllText(path,"[Twain]\r\nSafeDisableCap=0x18002\r\n");
				Assert.IsNull(TwainCaptureSettings.FromConfig(LocalCaptureConfig.Load(path)).SafeDisableCap,"Not a capability number.");
			}
			finally {
				File.Delete(path);
			}
			TwainCaptureSettings settingsDefault=TwainCaptureSettings.FromConfig(null);
			Assert.IsNull(settingsDefault.SafeDisableCap);
			Assert.AreEqual(0,settingsDefault.ListRefusedDisableCodes.Count);
			Assert.AreEqual(TimeSpan.Zero,settingsDefault.AcquireTimeout);
			Assert.IsFalse(settingsDefault.IsDisableSafeSource(""));
		}
		#endregion Safe-disable capability

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
