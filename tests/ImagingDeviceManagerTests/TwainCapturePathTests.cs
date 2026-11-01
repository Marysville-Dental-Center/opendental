// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using ImagingDeviceManager.Capture;
using ImagingDeviceManager.Imaging;
using ImagingDeviceManager.TwainProtocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingDeviceManager.Tests {
	///<summary>The capture path of TwainSession (design 7.1 CAP-3): the 16-bit negotiation order, the raw 16-bit memory path, DAT_IMAGEINFO
	///per image, and DAT_EXTIMAGEINFO in state 7, against the contract 1.1 profile.</summary>
	[TestClass]
	public class TwainCapturePathTests {
		private SessionHarness _harness;

		[TestInitialize]
		public void Setup() {
			_harness=new SessionHarness();
			_harness.Session.IsCaptureMode=true;
		}

		[TestCleanup]
		public void Cleanup() {
			_harness.Dispose();
		}

		private TwainSession Session => _harness.Session;
		private FakeHelperSource Helper => _harness.Helper;
		private List<FakeCall> Calls => _harness.Fake.ListCalls;

		///<summary>An 8-bit rendering with a 16-bit original whose samples use all 16 bits (no shift would survive this).</summary>
		internal static FakePage ExposureWith16(int width,int height) {
			FakePage page=FakePage.CreateGray8(width,height,FakeHelperSource.Dpi,(x,y) => (byte)(x*11+y));
			page.Original16=FakePage.CreateGray16(width,height,FakeHelperSource.Dpi,(x,y) => Sample16(x,y));
			return page;
		}

		public static int Sample16(int x,int y) {
			return (x*4099+y*257+0x0101)&0xFFFF;
		}

		private void OpenHelper() {
			Assert.IsTrue(Session.OpenSource(Helper.ProductName),Session.ErrorText);
		}

		private TwainCaptureNegotiationResult Negotiate(bool want16) {
			return Negotiate(want16 ? 16 : 8);
		}

		private TwainCaptureNegotiationResult Negotiate(int bitsWanted) {
			return Session.NegotiateCapture(new TwainCaptureNegotiation { BitsWanted=bitsWanted });
		}

		///<summary>Enables, waits for the notification and transfers one image; the source is left as the transfer leaves it.</summary>
		private AcquiredImage EnableAndTransfer() {
			Assert.AreEqual(Tw.TWRC_SUCCESS,Session.EnableForCapture(true));
			Assert.AreEqual(Tw.MSG_XFERREADY,Session.WaitForCaptureNotification(false),_harness.CallText);
			AcquiredImage image;
			Assert.AreEqual(TransferOutcome.Done,Session.TransferCaptureImage(out image),Session.ErrorText);
			return image;
		}

		private int IndexOfSet(ushort cap,int value,int start=0) {
			return _harness.IndexOfCall(x => x.Name=="CAP/SET" && x.Cap==cap && x.Value==value,start);
		}

		private static void AssertGray16(FakePage original,AcquiredImage image) {
			Assert.AreEqual(original.Width,image.Width);
			Assert.AreEqual(original.Height,image.Height);
			Assert.IsNotNull(image.Gray16);
			Assert.AreEqual(original.Width*original.Height,image.Gray16.Length);
			for(int y=0;y<original.Height;y++) {
				for(int x=0;x<original.Width;x++) {
					int expected=original.TwainRows[y][x*2]|(original.TwainRows[y][x*2+1]<<8);
					Assert.AreEqual(expected,image.Gray16[y*original.Width+x],"Sample ("+x+","+y+")");
				}
			}
		}

		private static void AssertGray8Dib(FakePage page,AcquiredImage image) {
			Assert.IsNotNull(image.Dib);
			DibImage dib=DibImage.FromPackedDib(image.Dib);
			Assert.AreEqual(8,dib.BitCount);
			for(int y=0;y<page.Height;y++) {
				for(int x=0;x<page.Width;x++) {
					int row=dib.IsTopDown ? y : page.Height-1-y;
					Assert.AreEqual(page.TwainRows[y][x],dib.Data[dib.PixelOffset+row*dib.Stride+x],"Pixel ("+x+","+y+")");
				}
			}
		}

		#region 16-bit negotiation
		[TestMethod]
		public void Want16_MemorySetFirst_ThenBitDepth16_OnlyBecauseGetLists16() {
			FakePage page=ExposureWith16(9,6);
			Helper.ListExposures.Add(page);
			OpenHelper();
			TwainCaptureNegotiationResult result=Negotiate(true);
			Assert.AreEqual(16,result.Bits,result.ReasonFallback);
			Assert.IsNull(result.ReasonFallback);
			Assert.AreEqual(Tw.TWSX_MEMORY,result.XferMech);
			int idxMemory=IndexOfSet(Tw.ICAP_XFERMECH,Tw.TWSX_MEMORY);
			int idxGet=_harness.IndexOfCall(x => x.Name=="CAP/GET" && x.Cap==Tw.ICAP_BITDEPTH);
			int idxDepth=IndexOfSet(Tw.ICAP_BITDEPTH,16);
			Assert.IsTrue(idxMemory>=0 && idxGet>idxMemory && idxDepth>idxGet,"MEMORY, then MSG_GET, then 16.\r\n"+_harness.CallText);
			Assert.AreEqual(Tw.TWRC_SUCCESS,Calls[idxDepth].Rc);
			Assert.IsFalse(Calls.Any(x => x.Name=="CAP/SET" && x.Cap==Tw.ICAP_PIXELTYPE),"The pixel type is never set.");
			Assert.IsTrue(Calls.Take(idxDepth).Any(x => x.Name=="CAP/GETCURRENT" && x.Cap==Tw.ICAP_PIXELTYPE),"It is read.");
			Assert.AreEqual(16,Helper.DictCaps[FakeHelperSource.IcapBitDepth]);
			AcquiredImage image=EnableAndTransfer();
			AssertGray16(page.Original16,image);
			Assert.IsNull(image.Dib);
			Assert.AreEqual(16,image.BitsPerPixel);
			Assert.AreEqual(16,image.BitsPerSample);
			Assert.AreEqual(AcquiredImage.PixelTypeGray,image.PixelType);
			Assert.AreEqual(16,image.BitDepthInEffect);
			Assert.IsTrue(image.IsAbove8BitInEffect);
			Assert.IsFalse(image.IsDeliveredAs8BitIn16BitEnable);
			Assert.AreEqual(TwainTransferMechanism.Memory,image.TransferMechanism);
			Assert.AreEqual(TwainByteOrder.LE,image.ByteOrder16);
			Assert.AreEqual(FakeHelperSource.Dpi,image.XDpi,0.001);
			Assert.AreEqual(1,Helper.CountDelivered);
		}

		[TestMethod]
		public void Want16_ByteOrderBE_SwapsEachSample() {
			FakePage page=ExposureWith16(4,3);
			Helper.ListExposures.Add(page);
			OpenHelper();
			Session.NegotiateCapture(new TwainCaptureNegotiation { BitsWanted=16,IsByteOrderBigEndian=true });
			AcquiredImage image=EnableAndTransfer();
			Assert.AreEqual(TwainByteOrder.BE,image.ByteOrder16);
			int sample=Sample16(1,2);
			Assert.AreEqual((ushort)(((sample&0xFF)<<8)|(sample>>8)),image.Gray16[2*4+1]);
		}

		[TestMethod]
		public void Want16_PaddedRowsAndTiles_PixelExact() {
			FakePage page=ExposureWith16(11,7);
			Helper.ListExposures.Add(page);
			Helper.MemoryRowPadding=6;
			Helper.MemoryColumnsPerStrip=4;
			OpenHelper();
			Negotiate(true);
			AcquiredImage image=EnableAndTransfer();
			AssertGray16(page.Original16,image);
			Assert.IsTrue(_harness.Fake.CallsNamed("MEMXFER").Count>=3,"Tiles: several strips.");
		}

		[TestMethod]
		public void Want16_ImageWithout16BitOriginal_ReturnedAs8BitAndFlagged() {
			FakePage page=FakePage.CreateGray8(10,4,FakeHelperSource.Dpi,(x,y) => (byte)(x*20+y));
			Helper.ListExposures.Add(page);
			Helper.MemoryRowPadding=2;
			OpenHelper();
			Assert.AreEqual(16,Negotiate(true).Bits);
			AcquiredImage image=EnableAndTransfer();
			Assert.IsNull(image.Gray16);
			Assert.AreEqual(8,image.BitsPerPixel);
			Assert.IsTrue(image.IsDeliveredAs8BitIn16BitEnable);
			Assert.AreEqual(16,image.BitDepthInEffect);
			AssertGray8Dib(page,image);
			Assert.IsTrue(_harness.HasLog("delivered an 8-bit image in a 16-bit enable"),_harness.LogText);
		}

		[TestMethod]
		public void Want16_ImageInfoReadForEveryImage() {
			FakePage first=ExposureWith16(5,3);
			FakePage second=FakePage.CreateGray8(6,3,FakeHelperSource.Dpi,(x,y) => (byte)x);
			Helper.ListExposures.Add(first);
			Helper.ListExposures.Add(second);
			Helper.Mode=FakeHelperMode.Multi;//Two images in one enable.
			Helper.EndAfterMs=0;
			OpenHelper();
			Negotiate(true);
			AcquiredImage image1=EnableAndTransfer();
			Assert.AreEqual(6,Session.State,"The second image is pending.");
			AcquiredImage image2;
			Assert.AreEqual(TransferOutcome.Done,Session.TransferCaptureImage(out image2));
			Assert.IsTrue(image1.IsGray16);
			Assert.IsFalse(image2.IsGray16);
			Assert.IsTrue(image2.IsDeliveredAs8BitIn16BitEnable);
			Assert.AreEqual(2,_harness.Fake.CallsNamed("IMAGEINFO").Count,"DAT_IMAGEINFO per image, not once per enable.");
		}

		[TestMethod]
		public void Want8_After16OnTheSameOpenSource_BitDepth8SetBeforeTheEnable() {
			Helper.ListExposures.Add(ExposureWith16(5,3));
			OpenHelper();
			Negotiate(true);
			EnableAndTransfer();
			Session.DisableCapture();
			Assert.AreEqual(4,Session.State);
			int start=Calls.Count;
			TwainCaptureNegotiationResult result=Negotiate(false);
			Assert.AreEqual(8,result.Bits);
			Assert.AreEqual(Tw.TWSX_NATIVE,result.XferMech);
			int idxDepth8=IndexOfSet(Tw.ICAP_BITDEPTH,8,start);
			int idxNative=IndexOfSet(Tw.ICAP_XFERMECH,Tw.TWSX_NATIVE,start);
			Assert.IsTrue(idxDepth8>=start && idxNative>idxDepth8,"ICAP_BITDEPTH 8 first, then native.\r\n"+_harness.CallText);
			Assert.AreEqual(Tw.TWRC_SUCCESS,Calls[idxNative].Rc,"No CHECKSTATUS: the depth was already 8.");
			AcquiredImage image=EnableAndTransfer();
			Assert.AreEqual(8,image.BitsPerPixel);
			Assert.AreEqual(TwainTransferMechanism.Native,image.TransferMechanism);
			Assert.IsFalse(image.IsDeliveredAs8BitIn16BitEnable);
			int idxEnable=_harness.IndexOfCall(x => x.Name=="ENABLEDS",start);
			Assert.IsTrue(idxEnable>idxNative);
		}

		[TestMethod]
		public void Want8_FirstEnable_NoBitDepthSet() {
			Helper.ListExposures.Add(ExposureWith16(5,3));
			OpenHelper();
			Negotiate(false);
			Assert.IsFalse(Calls.Any(x => x.Name=="CAP/SET" && x.Cap==Tw.ICAP_BITDEPTH),"The depth is left as the source delivers it.");
			AcquiredImage image=EnableAndTransfer();
			Assert.AreEqual(8,image.BitsPerPixel,"Native: the 8-bit rendering.");
		}

		[TestMethod]
		public void Contract10Source_Lists8Only_StaysAt8WithoutSetting16() {
			Helper.Supports16Bit=false;
			Helper.ListExposures.Add(ExposureWith16(5,3));
			OpenHelper();
			TwainCaptureNegotiationResult result=Negotiate(true);
			Assert.AreEqual(8,result.Bits);
			StringAssert.Contains(result.ReasonFallback,"does not list 16");
			Assert.AreEqual(-1,IndexOfSet(Tw.ICAP_BITDEPTH,16));
			Assert.AreEqual(Tw.TWSX_NATIVE,result.XferMech,"Back to native for 8 bits.");
			Assert.AreEqual(8,EnableAndTransfer().BitsPerPixel);
		}

		[TestMethod]
		public void MemoryRefused_16NeverAskedOverNative() {
			Helper.RefusesMemoryTransfer=true;
			Helper.ListExposures.Add(ExposureWith16(5,3));
			OpenHelper();
			TwainCaptureNegotiationResult result=Negotiate(true);
			Assert.AreEqual(8,result.Bits);
			StringAssert.Contains(result.ReasonFallback,"never asked over native transfers");
			Assert.IsFalse(Calls.Any(x => x.Name=="CAP/SET" && x.Cap==Tw.ICAP_BITDEPTH),_harness.CallText);
			Assert.IsTrue(_harness.HasLog("the enable runs at 8 bits"),_harness.LogText);
		}

		[TestMethod]
		public void Contract_Refuses16WhileNative_AndNativeResets16To8WithCheckStatus() {
			using(FakeRawClient client=new FakeRawClient(_harness.Fake,Helper.ProductName)) {
				Assert.AreEqual(Tw.TWRC_FAILURE,client.SetCap(FakeHelperSource.IcapBitDepth,Tw.TWTY_UINT16,16),"16 while native.");
				Assert.AreEqual(Tw.TWCC_BADVALUE,client.Status());
				Assert.AreEqual(Tw.TWRC_SUCCESS,client.SetCap(Tw.ICAP_XFERMECH,Tw.TWTY_UINT16,Tw.TWSX_MEMORY));
				Assert.AreEqual(Tw.TWRC_SUCCESS,client.SetCap(FakeHelperSource.IcapBitDepth,Tw.TWTY_UINT16,16));
				Assert.AreEqual(Tw.TWRC_CHECKSTATUS,client.SetCap(Tw.ICAP_XFERMECH,Tw.TWTY_UINT16,Tw.TWSX_NATIVE));
				Assert.AreEqual(8,client.GetCapValue(FakeHelperSource.IcapBitDepth));
			}
		}

		///<summary>An 8-bit rendering with a 14-bit original: values 0 to 16383, right-justified in 16-bit containers.</summary>
		internal static FakePage ExposureWith14(int width,int height) {
			FakePage page=FakePage.CreateGray8(width,height,FakeHelperSource.Dpi,(x,y) => (byte)(x*11+y));
			page.Original16=FakePage.CreateGray16(width,height,FakeHelperSource.Dpi,(x,y) => Sample14(x,y));
			return page;
		}

		public static int Sample14(int x,int y) {
			return (x*4099+y*257+0x0101)&0x3FFF;
		}

		[TestMethod]
		public void Lists8And14_Want16_FallsBackTo14_SamplesUnchanged() {
			Helper.ListBitDepths=new List<int> { 8,14 };
			FakePage page=ExposureWith14(9,6);
			Helper.ListExposures.Add(page);
			OpenHelper();
			TwainCaptureNegotiationResult result=Negotiate(16);
			Assert.AreEqual(14,result.Bits,result.ReasonFallback);
			Assert.AreEqual(Tw.TWSX_MEMORY,result.XferMech);
			StringAssert.Contains(result.ReasonFallback,"does not list 16");
			Assert.AreEqual(-1,IndexOfSet(Tw.ICAP_BITDEPTH,16),"16 is not listed, so it is never set.");
			int idxMemory=IndexOfSet(Tw.ICAP_XFERMECH,Tw.TWSX_MEMORY);
			int idxGet=_harness.IndexOfCall(x => x.Name=="CAP/GET" && x.Cap==Tw.ICAP_BITDEPTH);
			int idxDepth=IndexOfSet(Tw.ICAP_BITDEPTH,14);
			Assert.IsTrue(idxMemory>=0 && idxGet>idxMemory && idxDepth>idxGet,"MEMORY, then MSG_GET, then 14.\r\n"+_harness.CallText);
			Assert.AreEqual(14,Session.CaptureBits);
			Assert.IsTrue(_harness.HasLog("the enable runs at 14 bits"),_harness.LogText);
			AcquiredImage image=EnableAndTransfer();
			AssertGray16(page.Original16,image);
			Assert.AreEqual(14,image.BitDepthInEffect);
			Assert.IsTrue(image.IsAbove8BitInEffect);
			Assert.AreEqual(14,image.BitsPerSample,"As DAT_IMAGEINFO reports it.");
			Assert.AreEqual(16,image.BitsPerPixel,"16-bit containers.");
			Assert.IsFalse(_harness.HasLog("not right-justified"),"Every sample fits 14 bits.");
		}

		[TestMethod]
		public void Lists8_12_14_16_Want16Gets16_Want14Gets14_12NeverSet() {
			Helper.ListBitDepths=new List<int> { 8,12,14,16 };
			Helper.ListExposures.Add(ExposureWith16(5,3));
			OpenHelper();
			TwainCaptureNegotiationResult result16=Negotiate(16);
			Assert.AreEqual(16,result16.Bits);
			Assert.IsNull(result16.ReasonFallback);
			Assert.AreEqual(-1,IndexOfSet(Tw.ICAP_BITDEPTH,14),"16 was listed and accepted: 14 is not tried.");
			TwainCaptureNegotiationResult result14=Negotiate(14);
			Assert.AreEqual(14,result14.Bits);
			Assert.IsNull(result14.ReasonFallback);
			Assert.AreEqual(14,Helper.DictCaps[FakeHelperSource.IcapBitDepth]);
			Assert.AreEqual(-1,IndexOfSet(Tw.ICAP_BITDEPTH,12),"12 is never asked.");
		}

		[TestMethod]
		public void Lists8Only_Want14_StaysAt8_NativeAgain() {
			Helper.Supports16Bit=false;
			Helper.ListExposures.Add(ExposureWith14(5,3));
			OpenHelper();
			TwainCaptureNegotiationResult result=Negotiate(14);
			Assert.AreEqual(8,result.Bits);
			StringAssert.Contains(result.ReasonFallback,"does not list 14");
			Assert.IsFalse(result.ReasonFallback.Contains("16"),"14 asked: 16 is never tried.");
			Assert.IsFalse(Calls.Any(x => x.Name=="CAP/SET" && x.Cap==Tw.ICAP_BITDEPTH),_harness.CallText);
			Assert.AreEqual(Tw.TWSX_NATIVE,result.XferMech,"Back to native for 8 bits.");
			Assert.AreEqual(8,EnableAndTransfer().BitsPerPixel);
		}

		[TestMethod]
		public void Lists8_14_16_Set16Refused_FallsBackTo14() {
			Helper.ListBitDepths=new List<int> { 8,14,16 };
			Helper.HashSetRefusedBitDepths.Add(16);
			Helper.ListExposures.Add(ExposureWith14(5,3));
			OpenHelper();
			TwainCaptureNegotiationResult result=Negotiate(16);
			Assert.AreEqual(14,result.Bits);
			StringAssert.Contains(result.ReasonFallback,"did not accept ICAP_BITDEPTH 16");
			int idx16=IndexOfSet(Tw.ICAP_BITDEPTH,16);
			int idx14=IndexOfSet(Tw.ICAP_BITDEPTH,14);
			Assert.AreEqual(Tw.TWRC_FAILURE,Calls[idx16].Rc);
			Assert.IsTrue(idx14>idx16,_harness.CallText);
			Assert.AreEqual(14,EnableAndTransfer().BitDepthInEffect);
		}

		[TestMethod]
		public void Lists8And14_Set14Refused_BackTo8_ConfirmedBeforeNative() {
			Helper.ListBitDepths=new List<int> { 8,14 };
			Helper.HashSetRefusedBitDepths.Add(14);
			Helper.ListExposures.Add(ExposureWith14(5,3));
			OpenHelper();
			TwainCaptureNegotiationResult result=Negotiate(16);
			Assert.AreEqual(8,result.Bits);
			StringAssert.Contains(result.ReasonFallback,"does not list 16");
			StringAssert.Contains(result.ReasonFallback,"did not accept ICAP_BITDEPTH 14");
			Assert.AreEqual(Tw.TWSX_NATIVE,result.XferMech);
			Assert.AreEqual(8,Helper.DictCaps[FakeHelperSource.IcapBitDepth]);
			Assert.IsTrue(_harness.HasLog("the enable runs at 8 bits"),_harness.LogText);
			AcquiredImage image=EnableAndTransfer();
			Assert.AreEqual(8,image.BitsPerPixel);
			Assert.AreEqual(8,image.BitDepthInEffect);
		}

		[TestMethod]
		public void Want8_After14OnTheSameOpenSource_BitDepth8SetBeforeNative() {
			Helper.ListBitDepths=new List<int> { 8,14 };
			Helper.ListExposures.Add(ExposureWith14(5,3));
			OpenHelper();
			Assert.AreEqual(14,Negotiate(16).Bits);
			EnableAndTransfer();
			Session.DisableCapture();
			int start=Calls.Count;
			TwainCaptureNegotiationResult result=Negotiate(8);
			Assert.AreEqual(8,result.Bits);
			int idxDepth8=IndexOfSet(Tw.ICAP_BITDEPTH,8,start);
			int idxNative=IndexOfSet(Tw.ICAP_XFERMECH,Tw.TWSX_NATIVE,start);
			Assert.IsTrue(idxDepth8>=start && idxNative>idxDepth8,"ICAP_BITDEPTH 8 first, then native.\r\n"+_harness.CallText);
		}

		[TestMethod]
		public void Bits14_PackedSamples_TransferFailsUnread_LayoutNotUsable() {
			Helper.ListBitDepths=new List<int> { 8,14 };
			Helper.BitsPerPixelAt14=14;//packed 14-bit samples
			Helper.ListExposures.Add(ExposureWith14(5,3));
			OpenHelper();
			Assert.AreEqual(14,Negotiate(14).Bits);
			Assert.AreEqual(Tw.TWRC_SUCCESS,Session.EnableForCapture(true));
			Assert.AreEqual(Tw.MSG_XFERREADY,Session.WaitForCaptureNotification(false));
			AcquiredImage image;
			Assert.AreEqual(TransferOutcome.Failed,Session.TransferCaptureImage(out image));
			Assert.IsNull(image);
			StringAssert.StartsWith(Session.LayoutUnusableText,"14-bit layout not usable: ");
			StringAssert.Contains(Session.LayoutUnusableText,"BitsPerPixel is 14");
			StringAssert.Contains(Session.LayoutUnusableText,"packed samples");
			Assert.AreEqual(0,_harness.Fake.CallsNamed("MEMXFER").Count,"Nothing is read of an image that cannot be decoded.");
			FakeCall callEnd=_harness.Fake.CallsNamed("ENDXFER").Single();
			Assert.AreEqual(6,callEnd.StateBefore,"Ended in state 6 without a transfer.");
			Assert.IsTrue(_harness.HasLog("in a layout that cannot be read"),_harness.LogText);
		}

		[TestMethod]
		public void Bits14_BitsPerSample16InContainers_Accepted() {
			Helper.ListBitDepths=new List<int> { 8,14 };
			Helper.BitsPerSampleAt14=16;
			FakePage page=ExposureWith14(4,3);
			Helper.ListExposures.Add(page);
			OpenHelper();
			Negotiate(14);
			AcquiredImage image=EnableAndTransfer();
			AssertGray16(page.Original16,image);
			Assert.AreEqual(14,image.BitDepthInEffect);
			Assert.IsNull(Session.LayoutUnusableText);
		}

		[TestMethod]
		public void Bits14_SamplesAbove14Bits_KeptAsDeliveredAndLogged() {
			Helper.ListBitDepths=new List<int> { 8,14 };
			FakePage page=ExposureWith16(5,3);//values above 16383 in a 14-bit enable
			Helper.ListExposures.Add(page);
			OpenHelper();
			Negotiate(14);
			AcquiredImage image=EnableAndTransfer();
			AssertGray16(page.Original16,image);
			Assert.IsTrue(_harness.HasLog("not right-justified 14-bit values"),_harness.LogText);
		}

		[TestMethod]
		public void PixelTypeNotReadable_16NotNegotiated_NothingForced() {
			//The built-in source answers no ICAP_PIXELTYPE.
			_harness.Fake.ListPages.Add(FakePage.CreateGray8(4,3,300,(x,y) => (byte)x));
			Assert.IsTrue(Session.OpenSource("Fake Sensor"),Session.ErrorText);
			TwainCaptureNegotiationResult result=Negotiate(true);
			Assert.AreEqual(8,result.Bits);
			StringAssert.Contains(result.ReasonFallback,"ICAP_PIXELTYPE");
			Assert.IsFalse(_harness.Fake.DictCapsSet.ContainsKey(Tw.ICAP_PIXELTYPE));
			Assert.IsFalse(_harness.Fake.DictCapsSet.ContainsKey(Tw.ICAP_BITDEPTH));
			Assert.AreNotEqual(Tw.TWSX_MEMORY,_harness.Fake.DictCapsSet.ContainsKey(Tw.ICAP_XFERMECH) ? _harness.Fake.DictCapsSet[Tw.ICAP_XFERMECH] : 0);
		}
		#endregion 16-bit negotiation

		#region Extended image info
		[TestMethod]
		public void ExtImageInfo_SetInState4_ReadInState7BeforeEndXfer_HandlesFreedThroughTheDsm() {
			_harness.Fake.SupplyEntryPoints=true;
			FakePage page=ExposureWith16(5,3);
			Helper.ListExposures.Add(page);
			OpenHelper();
			TwainCaptureNegotiationResult result=Negotiate(true);
			Assert.IsTrue(result.IsExtImageInfoSet);
			Assert.IsTrue(result.IsMetadataListed);
			int idxQuery=_harness.IndexOfCall(x => x.Name=="CAP/QUERYSUPPORT" && x.Cap==Tw.ICAP_EXTIMAGEINFO);
			int idxSet=IndexOfSet(Tw.ICAP_EXTIMAGEINFO,1);
			Assert.IsTrue(idxQuery>=0 && idxSet>idxQuery);
			Assert.AreEqual(4,Calls[idxSet].StateBefore);
			AcquiredImage image=EnableAndTransfer();
			int idxXferDone=_harness.IndexOfCall(x => x.Name=="MEMXFER" && x.Rc==Tw.TWRC_XFERDONE);
			int idxExt=_harness.IndexOfCall(x => x.Name=="EXTIMAGEINFO");
			int idxEnd=_harness.IndexOfCall(x => x.Name=="ENDXFER");
			Assert.IsTrue(idxXferDone>=0 && idxExt>idxXferDone && idxEnd>idxExt,"XFERDONE, EXTIMAGEINFO, ENDXFER.\r\n"+_harness.CallText);
			Assert.AreEqual(7,Calls[idxExt].StateBefore);
			Assert.AreEqual(Tw.TWRC_SUCCESS,Calls[idxExt].Rc);
			Assert.AreEqual(Helper.GetMetadataJson(page),image.MetadataJson,"Raw, as sent.");
			StringAssert.Contains(image.MetadataJson,"\"schema\":\"capture-metadata/1\"");
			StringAssert.Contains(image.MetadataJson,"Fake 1.1 é","UTF-8.");
			Assert.AreEqual(Tw.TWPF_CHOCOLATE,image.PixelFlavor);
			Assert.AreEqual(0,_harness.Fake.DsmMemory.CountOutstanding,"Every handle went back through DSM_MemFree.");
			Assert.AreEqual(0,_harness.Fake.DsmMemory.CountForeignFree);
		}

		[TestMethod]
		public void ExtImageInfo_GlobalHandles_FreedWithGlobalFree() {
			RecordingGlobalMemory globalMemory=new RecordingGlobalMemory();
			Session.GlobalMemory=globalMemory;
			Helper.ListExposures.Add(SessionHarness.Exposure(6));
			OpenHelper();
			Negotiate(false);
			AcquiredImage image=EnableAndTransfer();
			Assert.IsNotNull(image.MetadataJson);
			CollectionAssert.IsSubsetOf(_harness.Fake.ListSourceHandles,globalMemory.ListFreed,"Every handle from the source is freed.");
		}

		[TestMethod]
		public void ExtImageInfo_OnlyPixelFlavorWhenMetadataNotListed() {
			Helper.ListsTwainDirectMetadata=false;
			Helper.ListExposures.Add(SessionHarness.Exposure(6));
			OpenHelper();
			TwainCaptureNegotiationResult result=Negotiate(false);
			Assert.IsTrue(result.IsExtImageInfoSet);
			Assert.IsFalse(result.IsMetadataListed);
			AcquiredImage image=EnableAndTransfer();
			Assert.IsNull(image.MetadataJson,"0x124B is read only when ICAP_SUPPORTEDEXTIMAGEINFO lists it.");
			Assert.AreEqual(1,Helper.CountExtImageInfo,"TWEI_PIXELFLAVOR is still asked.");
		}

		[TestMethod]
		public void ExtImageInfo_Contract10Source_NoMetadata() {
			Helper.SupportsExtImageInfo=false;
			Helper.ListExposures.Add(SessionHarness.Exposure(6));
			OpenHelper();
			TwainCaptureNegotiationResult result=Negotiate(false);
			Assert.IsFalse(result.IsExtImageInfoSet);
			AcquiredImage image=EnableAndTransfer();
			Assert.IsNull(image.MetadataJson);
			Assert.AreEqual(0,_harness.Fake.CallsNamed("EXTIMAGEINFO").Count);
			Assert.IsTrue(_harness.HasLog("does not support ICAP_EXTIMAGEINFO"),_harness.LogText);
		}

		[TestMethod]
		public void ExtImageInfo_BadProtocol_TreatedAsNoMetadata() {
			Helper.ListExposures.Add(SessionHarness.Exposure(6));
			OpenHelper();
			Negotiate(false);
			Helper.SupportsExtImageInfo=false;//Now DAT_EXTIMAGEINFO returns TWCC_BADPROTOCOL, as in contract 1.0.
			AcquiredImage image=EnableAndTransfer();
			Assert.IsNull(image.MetadataJson);
			FakeCall callExt=_harness.Fake.CallsNamed("EXTIMAGEINFO").Single();
			Assert.AreEqual(Tw.TWCC_BADPROTOCOL,callExt.Condition);
			Assert.AreEqual(1,Helper.CountDelivered,"The image is still delivered.");
		}

		[TestMethod]
		public void ExtImageInfo_FakeFailsItAfterEndXfer() {
			Helper.ListExposures.Add(SessionHarness.Exposure(6));
			Helper.ListExposures.Add(SessionHarness.Exposure(6));
			Helper.EndAfterMs=0;
			using(FakeRawClient client=new FakeRawClient(_harness.Fake,Helper.ProductName)) {
				client.SetCap(FakeHelperSource.IcapExtImageInfo,Tw.TWTY_BOOL,1);
				client.Enable();
				Assert.AreEqual(Tw.MSG_XFERREADY,client.WaitForNotification(5000));
				byte[] dib;
				IntPtr hDib;
				Assert.AreEqual(Tw.TWRC_XFERDONE,client.NativeTransfer(out dib,out hDib));
				Assert.AreEqual(Tw.TWRC_SUCCESS,CallExtImageInfo(client),"State 7, after XFERDONE.");
				client.EndTransfer();
				Assert.AreEqual(Tw.TWRC_FAILURE,CallExtImageInfo(client),"After MSG_ENDXFER (state 6).");
				Assert.AreEqual(Tw.TWCC_SEQERROR,client.Status());
			}
		}

		private static ushort CallExtImageInfo(FakeRawClient client) {
			IntPtr p=Marshal.AllocHGlobal(TwExtImageInfo.GetSize(1));
			try {
				Marshal.WriteInt32(p,0,1);
				Marshal.StructureToPtr(new TW_INFO { InfoID=Tw.TWEI_PIXELFLAVOR },new IntPtr(p.ToInt64()+TwExtImageInfo.GetInfoOffset(0)),false);
				return client.Call(Tw.DG_IMAGE,Tw.DAT_EXTIMAGEINFO,Tw.MSG_GET,p);
			}
			finally {
				Marshal.FreeHGlobal(p);
			}
		}

		[TestMethod]
		public void ReadableBytes_BoundedByCommittedMemory() {
			IntPtr p=Marshal.AllocHGlobal(100);
			try {
				Assert.IsTrue(NativeMethods.GetReadableBytes(p,100)==100);
				Assert.AreEqual(0,NativeMethods.GetReadableBytes(IntPtr.Zero,100),"Page 0 is never committed.");
			}
			finally {
				Marshal.FreeHGlobal(p);
			}
		}
		#endregion Extended image info

		#region Drains on the capture path
		[TestMethod]
		public void StopWithImagesPending_DrainedAsAcquiredImages() {
			Helper.ListExposures.Add(ExposureWith16(5,3));
			Helper.ListExposures.Add(ExposureWith16(5,3));
			Helper.Mode=FakeHelperMode.Multi;
			Helper.EndAfterMs=0;
			OpenHelper();
			Negotiate(true);
			Assert.AreEqual(Tw.TWRC_SUCCESS,Session.EnableForCapture(true));
			Assert.AreEqual(Tw.MSG_XFERREADY,Session.WaitForCaptureNotification(false));
			Session.StopCapture(TwainStopReason.Cancel);
			Assert.AreEqual(4,Session.State);
			Assert.AreEqual(2,_harness.Sink.ListDrained.Count);
			Assert.IsTrue(_harness.Sink.ListDrained.All(x => x.Image==null && x.Acquired!=null && x.Acquired.IsGray16));
			Assert.IsTrue(_harness.Sink.ListDrained.All(x => x.Acquired.MetadataJson!=null),"Drained images keep their metadata.");
			Assert.AreEqual(TwainStopReason.Cancel,_harness.Sink.ListDrained[0].StopReason);
			_harness.AssertDisabledOnlyInState5();
		}
		#endregion Drains on the capture path
	}
}
