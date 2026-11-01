// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using ImagingDeviceManager.TwainProtocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingDeviceManager.Tests {
	///<summary>The capability reads and custom MSG_SETs of the capture path (design 7.1 CAP-3), against the contract 1.1 profile.</summary>
	[TestClass]
	public class TwainCaptureCapabilityTests {
		private SessionHarness _harness;

		[TestInitialize]
		public void Setup() {
			_harness=new SessionHarness();
		}

		[TestCleanup]
		public void Cleanup() {
			_harness.Dispose();
		}

		private TwainSession Session => _harness.Session;
		private FakeHelperSource Helper => _harness.Helper;

		private void OpenHelper() {
			Assert.IsTrue(Session.OpenSource(Helper.ProductName),Session.ErrorText);
		}

		[TestMethod]
		public void ExtImageInfoStructures_HaveTwainSizes() {
			Assert.AreEqual(12,Marshal.SizeOf(typeof(TW_INFO)),"TW_INFO: four UINT16 and a 4-byte TW_UINTPTR.");
			Assert.AreEqual(4+3*12,TwExtImageInfo.GetSize(3));
			Assert.AreEqual(4+12,TwExtImageInfo.GetInfoOffset(1));
			Assert.IsTrue(TwExtImageInfo.IsItemHandle(Tw.TWTY_HANDLE,1),"A TWTY_HANDLE item is a handle.");
			Assert.IsFalse(TwExtImageInfo.IsItemHandle(Tw.TWTY_UINT16,1),"2 bytes fit in Item.");
			Assert.IsFalse(TwExtImageInfo.IsItemHandle(Tw.TWTY_UINT32,1),"4 bytes fit in Item.");
			Assert.IsTrue(TwExtImageInfo.IsItemHandle(Tw.TWTY_UINT32,2),"8 bytes do not.");
			Assert.IsTrue(TwExtImageInfo.IsItemHandle(Tw.TWTY_STR255,1));
			Assert.IsTrue(TwExtImageInfo.IsItemHandle(Tw.TWTY_FRAME,1));
		}

		[TestMethod]
		public void BitDepthGet_IsAnEnumerationOf8And16() {
			OpenHelper();
			TwainCapabilityValues values;
			Assert.IsTrue(Session.TryGetCapabilityValues(Tw.ICAP_BITDEPTH,Tw.MSG_GET,out values));
			Assert.AreEqual(Tw.TWON_ENUMERATION,values.ConType);
			CollectionAssert.AreEqual(new List<int> { 8,16 },values.ListItems);
			Assert.AreEqual(8,values.Current);
			Assert.IsTrue(values.Contains(16));
			Assert.IsFalse(values.Contains(12));
			Assert.AreEqual("8|16",values.Describe(false));
		}

		[TestMethod]
		public void BitDepthGet_Contract10Source_Lists8Only() {
			Helper.Supports16Bit=false;
			OpenHelper();
			TwainCapabilityValues values;
			Assert.IsTrue(Session.TryGetCapabilityValues(Tw.ICAP_BITDEPTH,Tw.MSG_GET,out values));
			CollectionAssert.AreEqual(new List<int> { 8 },values.ListItems);
			Assert.IsFalse(values.Contains(16));
		}

		[TestMethod]
		public void SupportedExtImageInfo_IsAnArrayOfTheSevenItems_HandlesFreedThroughTheDsm() {
			_harness.Fake.SupplyEntryPoints=true;
			OpenHelper();
			TwainCapabilityValues values;
			Assert.IsTrue(Session.TryGetCapabilityValues(Tw.ICAP_SUPPORTEDEXTIMAGEINFO,Tw.MSG_GET,out values));
			Assert.AreEqual(Tw.TWON_ARRAY,values.ConType);
			Assert.AreEqual(7,values.ListItems.Count);
			Assert.IsTrue(values.Contains(Tw.TWEI_TWAINDIRECTMETADATA));
			Assert.IsTrue(values.Contains(Tw.TWEI_PIXELFLAVOR));
			Assert.IsNull(values.Current,"An array has no current value.");
			Assert.AreEqual(0,_harness.Fake.DsmMemory.CountOutstanding,"Every container went back through DSM_MemFree.");
			Assert.AreEqual(0,_harness.Fake.DsmMemory.CountForeignFree);
		}

		[TestMethod]
		public void SupportedExtImageInfo_Contract10Source_NotReadable() {
			Helper.SupportsExtImageInfo=false;
			OpenHelper();
			TwainCapabilityValues values;
			Assert.IsFalse(Session.TryGetCapabilityValues(Tw.ICAP_SUPPORTEDEXTIMAGEINFO,Tw.MSG_GET,out values));
			Assert.AreEqual(Tw.TWCC_CAPUNSUPPORTED,Session.LastConditionCode);
		}

		[TestMethod]
		public void OneValueAndFixedCapabilities_AreRead() {
			OpenHelper();
			int value;
			Assert.IsTrue(Session.TryGetCapabilityValue(Tw.CAP_INDICATORS,out value));
			Assert.AreEqual(1,value);
			Assert.IsTrue(Session.TryGetCapabilityValue(Tw.ICAP_PIXELFLAVOR,out value));
			Assert.AreEqual(Tw.TWPF_CHOCOLATE,value);
			Assert.IsTrue(Session.TryGetCapabilityValue(Tw.CAP_DEVICEONLINE,out value));
			Assert.AreEqual(1,value);
			TwainCapabilityValues values;
			Assert.IsTrue(Session.TryGetCapabilityValues(Tw.CAP_XFERCOUNT,Tw.MSG_GETCURRENT,out values));
			Assert.AreEqual(Tw.TWON_ONEVALUE,values.ConType);
			Assert.AreEqual(-1,values.Current);
			Assert.AreEqual(0,Helper.CountFixedCapSet,"Read, never set.");
		}

		[TestMethod]
		public void RangeContainer_ContainsHonoursTheStep() {
			IntPtr p=Marshal.AllocHGlobal(32);
			try {
				Marshal.WriteInt16(p,0,(short)Tw.TWTY_UINT16);
				Marshal.WriteInt32(p,TwContainer.RangeMinValueOffset,8);
				Marshal.WriteInt32(p,TwContainer.RangeMaxValueOffset,16);
				Marshal.WriteInt32(p,TwContainer.RangeStepSizeOffset,4);
				Marshal.WriteInt32(p,TwContainer.RangeDefaultValueOffset,8);
				Marshal.WriteInt32(p,TwContainer.RangeCurrentValueOffset,12);
				TwainCapabilityValues values=TwainSession.ReadContainer(p,Tw.TWON_RANGE,0);
				Assert.IsTrue(values.Contains(16));
				Assert.IsTrue(values.Contains(12));
				Assert.IsFalse(values.Contains(14),"Not a step of the range.");
				Assert.IsFalse(values.Contains(20));
				Assert.AreEqual(12,values.Current);
				Assert.AreEqual("8..16 step 4",values.Describe(false));
			}
			finally {
				Marshal.FreeHGlobal(p);
			}
		}

		[TestMethod]
		public void EnumerationContainer_ListBoundedByTheBlockSize() {
			IntPtr p=Marshal.AllocHGlobal(64);
			try {
				Marshal.WriteInt16(p,0,(short)Tw.TWTY_UINT16);
				Marshal.WriteInt32(p,TwContainer.EnumNumItemsOffset,1000000);//A broken source.
				Marshal.WriteInt32(p,TwContainer.EnumCurrentIndexOffset,0);
				Marshal.WriteInt32(p,TwContainer.EnumDefaultIndexOffset,0);
				TwainCapabilityValues values=TwainSession.ReadContainer(p,Tw.TWON_ENUMERATION,TwContainer.EnumItemListOffset+2*3);
				Assert.AreEqual(3,values.ListItems.Count,"Never read past the block.");
			}
			finally {
				Marshal.FreeHGlobal(p);
			}
		}

		[TestMethod]
		public void QuerySupport_AskedOncePerOpenSource() {
			OpenHelper();
			ushort conditionCode;
			Assert.AreEqual(0x1F,Session.GetQuerySupport(FakeHelperSource.CapRedeliver,out conditionCode));
			Assert.AreEqual(0x1F,Session.GetQuerySupport(FakeHelperSource.CapRedeliver,out conditionCode));
			Assert.AreEqual(1,_harness.Fake.ListCalls.Count(x => x.Name=="CAP/QUERYSUPPORT" && x.Cap==FakeHelperSource.CapRedeliver));
			Session.CloseSource();
			OpenHelper();
			Session.GetQuerySupport(FakeHelperSource.CapRedeliver,out conditionCode);
			Assert.AreEqual(2,_harness.Fake.ListCalls.Count(x => x.Name=="CAP/QUERYSUPPORT" && x.Cap==FakeHelperSource.CapRedeliver),
				"Asked again after MSG_OPENDS.");
		}

		[TestMethod]
		public void CustomCapability_SetInState4() {
			OpenHelper();
			Assert.AreEqual(TwainCustomCapOutcome.Set,Session.SetCustomCapability(FakeHelperSource.CapNoActivate,true,"NoActivateCap"));
			Assert.AreEqual(1,Helper.DictCaps[FakeHelperSource.CapNoActivate]);
			int idxQuery=_harness.IndexOfCall(x => x.Name=="CAP/QUERYSUPPORT" && x.Cap==FakeHelperSource.CapNoActivate);
			int idxSet=_harness.IndexOfCall(x => x.Name=="CAP/SET" && x.Cap==FakeHelperSource.CapNoActivate);
			Assert.IsTrue(idxQuery>=0 && idxSet>idxQuery,"MSG_QUERYSUPPORT first.\r\n"+_harness.CallText);
			Assert.AreEqual(4,_harness.Fake.ListCalls[idxSet].StateBefore);
		}

		[TestMethod]
		public void CustomCapability_ReservedNumber_UnsupportedAndLogged() {
			OpenHelper();
			const ushort capContinuousReserved=0x8001;//Reserved by the contract (section 10.1), not implemented in 1.1.
			Assert.AreEqual(TwainCustomCapOutcome.Unsupported,Session.SetCustomCapability(capContinuousReserved,true,"ContinuousCap"));
			Assert.IsFalse(_harness.Fake.ListCalls.Any(x => x.Name=="CAP/SET" && x.Cap==capContinuousReserved),"Nothing is set.");
			Assert.IsTrue(_harness.HasLog("does not support the ContinuousCap capability 0x8001 (TRUE) (TWCC_CAPUNSUPPORTED)"),_harness.LogText);
		}

		[TestMethod]
		public void CustomCapability_Contract10Source_Unsupported() {
			Helper.SupportsSafeDisable=false;
			OpenHelper();
			Assert.AreEqual(TwainCustomCapOutcome.Unsupported,Session.SetCustomCapability(FakeHelperSource.CapRedeliver,true,"RedeliverCap"));
		}

		[TestMethod]
		public void CustomCapability_NotSettable_NotSent() {
			OpenHelper();
			//CAP_INDICATORS stands in for a capability whose MSG_QUERYSUPPORT lacks TWQC_SET.
			Assert.AreEqual(TwainCustomCapOutcome.NotSettable,Session.SetCustomCapability(FakeHelperSource.CapIndicators,false,"SomeCap"));
			Assert.AreEqual(0,Helper.CountFixedCapSet);
		}

		[TestMethod]
		public void CustomCapability_OutsideState4_NotSent() {
			Assert.AreEqual(TwainCustomCapOutcome.NotSent,Session.SetCustomCapability(FakeHelperSource.CapNoActivate,true,"NoActivateCap"));
		}
	}
}
