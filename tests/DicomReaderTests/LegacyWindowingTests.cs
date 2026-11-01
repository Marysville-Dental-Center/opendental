// CAP-W2 (design 7.8): stored windowing values above 255 are legacy values on the 12 bit scale and are divided by 16 for display, without
// being written back. M2-IMG's own tests (tests/M2IMGTests/WindowingTests.cs) cover the BitsStored scaling and the 16 bit import window.
// The pixel values are made up. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace DicomReaderTests {
	[TestClass]
	public class LegacyWindowingTests {
		private static BitmapDicom MakeImage(ushort bitsStored,ushort[] arrayRaw) {
			BitmapDicom bitmapDicom=new BitmapDicom();
			bitmapDicom.ArrayDataRaw=arrayRaw;
			bitmapDicom.Width=arrayRaw.Length;
			bitmapDicom.Height=1;
			bitmapDicom.BitsAllocated=16;
			bitmapDicom.BitsStored=bitsStored;
			bitmapDicom.PhotometricInterp=EnumDicomPhotometricInterp.MONOCHROME2;
			return bitmapDicom;
		}

		private static byte[] Window(BitmapDicom bitmapDicom,int windowingMin,int windowingMax) {
			using Bitmap bitmap=DicomHelper.ApplyWindowing(bitmapDicom,windowingMin,windowingMax);
			byte[] arrayGray=new byte[bitmap.Width];
			for(int x=0;x<bitmap.Width;x++) {
				Color color=bitmap.GetPixel(x,0);
				Assert.AreEqual(color.R,color.G);
				Assert.AreEqual(color.R,color.B);
				arrayGray[x]=color.R;
			}
			return arrayGray;
		}

		///<summary>What Open Dental 24.3 showed: every 16 bit word divided by 16 against the stored values as they were.</summary>
		private static byte Window24_3(ushort raw,int windowingMin,int windowingMax) {
			float floatDataRaw=raw/16f;
			if(floatDataRaw<windowingMin) {
				return 0;
			}
			if(floatDataRaw>windowingMax) {
				return 255;
			}
			return (byte)Math.Round(255f*(floatDataRaw-windowingMin)/(windowingMax-windowingMin));
		}

		private static ushort[] Sweep16() {
			List<ushort> listRaw=new List<ushort>();
			for(int raw=0;raw<=65535;raw+=61) {
				listRaw.Add((ushort)raw);
			}
			listRaw.Add(65535);
			return listRaw.ToArray();
		}

		[TestMethod]
		public void DisplayScale_Table() {
			Assert.AreEqual(0,DicomHelper.GetWindowingOnDisplayScale(-7));
			Assert.AreEqual(0,DicomHelper.GetWindowingOnDisplayScale(0));
			Assert.AreEqual(100,DicomHelper.GetWindowingOnDisplayScale(100));
			Assert.AreEqual(255,DicomHelper.GetWindowingOnDisplayScale(255),"0-255 is used as it is.");
			Assert.AreEqual(16,DicomHelper.GetWindowingOnDisplayScale(256),"Above 255: the 12 bit scale, divided by 16.");
			Assert.AreEqual(100,DicomHelper.GetWindowingOnDisplayScale(1600));
			Assert.AreEqual(150,DicomHelper.GetWindowingOnDisplayScale(2400));
			Assert.AreEqual(255,DicomHelper.GetWindowingOnDisplayScale(4095),"The old 16 bit import value and the 12 bit fallback.");
			Assert.AreEqual(255,DicomHelper.GetWindowingOnDisplayScale(4096));
			Assert.AreEqual(255,DicomHelper.GetWindowingOnDisplayScale(65535));
			Assert.AreEqual(255,DicomHelper.GetWindowingOnDisplayScale(int.MaxValue));
		}

		[TestMethod]
		public void SixteenBit_ImportWindow0To4095_RendersAsBefore() {
			BitmapDicom bitmapDicom=MakeImage(16,Sweep16());
			byte[] arrayGray=Window(bitmapDicom,0,4095);
			for(int i=0;i<bitmapDicom.ArrayDataRaw.Length;i++) {
				int gray24_3=Window24_3(bitmapDicom.ArrayDataRaw[i],0,4095);
				Assert.IsTrue(Math.Abs(gray24_3-arrayGray[i])<=1,"raw "+bitmapDicom.ArrayDataRaw[i]+": "+arrayGray[i]+" now, "+gray24_3+" before");
			}
		}

		[TestMethod]
		public void SixteenBit_Other12BitScaleWindows_RenderAsBefore() {
			//A window stored on the 12 bit scale (both ends above 255, or a min of 0) shows what the old divide-by-16 showed: raw/16 against the
			//stored values is raw/256 against the stored values divided by 16. Within one gray level when both ends are multiples of 16 (and for
			//the old import's 0-4095); within two otherwise, because a stored value is divided by 16 in whole numbers (4095 reads as 255, not 255.9).
			BitmapDicom bitmapDicom=MakeImage(16,Sweep16());
			foreach((int min,int max) window in new[] { (1600,2400),(800,3008),(256,1024),(0,2048),(512,4095),(3008,4000),(0,4095),(1000,3333) }) {
				byte[] arrayGray=Window(bitmapDicom,window.min,window.max);
				int tolerance=(window.min%16==0 && window.max%16==0) || (window.min==0 && window.max==4095) ? 1 : 2;
				for(int i=0;i<bitmapDicom.ArrayDataRaw.Length;i++) {
					int gray24_3=Window24_3(bitmapDicom.ArrayDataRaw[i],window.min,window.max);
					Assert.IsTrue(Math.Abs(gray24_3-arrayGray[i])<=tolerance,
						"window "+window.min+"-"+window.max+", raw "+bitmapDicom.ArrayDataRaw[i]+": "+arrayGray[i]+" now, "+gray24_3+" before");
				}
			}
		}

		[TestMethod]
		public void TwelveBit_12BitScaleWindow_NowShowsTheWindowItMeant() {
			ushort[] arrayRaw=Enumerable.Range(0,4096).Where(x => x%5==0).Select(x => (ushort)x).ToArray();
			BitmapDicom bitmapDicom=MakeImage(12,arrayRaw);
			CollectionAssert.AreEqual(Window(bitmapDicom,100,150),Window(bitmapDicom,1600,2400),"1600-2400 on the 12 bit scale is 100-150.");
			//The mixed window of the 12 bit import's fallback (a min on the 0-255 scale, 4095 for the max), nearly black before CAP-W.
			CollectionAssert.AreEqual(Window(bitmapDicom,100,255),Window(bitmapDicom,100,4095));
			byte[] arrayGrayBefore=arrayRaw.Select(x => Window24_3(x,100,4095)).ToArray();
			Assert.IsTrue(arrayGrayBefore.Max()<20,"It used to render nearly black.");
			Assert.AreEqual(255,Window(bitmapDicom,100,4095).Max());
		}

		[TestMethod]
		public void ZeroZero_ShowsTheFullRange() {
			BitmapDicom bitmapDicom16=MakeImage(16,new ushort[] { 0,1000,32768,65535 });
			CollectionAssert.AreEqual(Window(bitmapDicom16,0,255),Window(bitmapDicom16,0,0));
			BitmapDicom bitmapDicom12=MakeImage(12,new ushort[] { 0,1000,2048,4095 });
			CollectionAssert.AreEqual(Window(bitmapDicom12,0,255),Window(bitmapDicom12,0,0));
		}

		[TestMethod]
		public void SingleValueWindow_DoesNotThrow() {
			//OpenDentBusiness uses checked arithmetic: 0/0 would be NaN, and a checked cast of NaN to a byte throws.
			BitmapDicom bitmapDicom=MakeImage(16,new ushort[] { 0,25600,25700,65535 });
			CollectionAssert.AreEqual(new byte[] { 0,255,255,255 },Window(bitmapDicom,100,100));
			BitmapDicom bitmapDicom12=MakeImage(12,new ushort[] { 0,4080,4095 });
			CollectionAssert.AreEqual(new byte[] { 0,255,255 },Window(bitmapDicom12,4095,4095),"4095 and 4095 both read as 255.");
		}
	}
}
