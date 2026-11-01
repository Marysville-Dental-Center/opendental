// Windowing (brightness and contrast) of DICOM radiographs, Open Dental 25.4: document.WindowingMin and WindowingMax stay on a 0-255 scale for
// every bit depth; 12 bit data is scaled by 16, as before, and true 16 bit data by 256 (its 12 most significant bits). The pixel values below
// are made up. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2IMGTests {
	[TestClass]
	public class WindowingTests {
		///<summary>A one-row image with these raw values.</summary>
		public static BitmapDicom MakeImage(ushort bitsAllocated,ushort bitsStored,params ushort[] arrayRaw) {
			BitmapDicom bitmapDicom=new BitmapDicom();
			bitmapDicom.ArrayDataRaw=arrayRaw;
			bitmapDicom.Width=arrayRaw.Length;
			bitmapDicom.Height=1;
			bitmapDicom.BitsAllocated=bitsAllocated;
			bitmapDicom.BitsStored=bitsStored;
			bitmapDicom.PhotometricInterp=EnumDicomPhotometricInterp.MONOCHROME2;
			return bitmapDicom;
		}

		///<summary>The gray value of every pixel of a one-row bitmap, checking that it is gray and opaque.</summary>
		public static byte[] Gray(Bitmap bitmap) {
			byte[] arrayGray=new byte[bitmap.Width];
			for(int x=0;x<bitmap.Width;x++) {
				Color color=bitmap.GetPixel(x,0);
				Assert.AreEqual(color.R,color.G);
				Assert.AreEqual(color.R,color.B);
				Assert.AreEqual(255,color.A);
				arrayGray[x]=color.R;
			}
			return arrayGray;
		}

		public static byte[] Window(BitmapDicom bitmapDicom,int windowingMin,int windowingMax) {
			using Bitmap bitmap=DicomHelper.ApplyWindowing(bitmapDicom,windowingMin,windowingMax);
			return Gray(bitmap);
		}

		///<summary>What 24.3 showed for one raw value: every 16 bit word was divided by 16, whatever BitsStored said. Same float arithmetic as the
		///24.3 loop, so 12 bit results must match it exactly.</summary>
		public static byte Window24_3(BitmapDicom bitmapDicom,ushort raw,int windowingMin,int windowingMax) {
			float floatDataRaw=raw;
			if(bitmapDicom.BitsAllocated==16) {
				floatDataRaw=floatDataRaw/16f;
			}
			if(floatDataRaw<windowingMin) {
				return 0;
			}
			if(floatDataRaw>windowingMax) {
				return 255;
			}
			if(bitmapDicom.BitsAllocated==8) {
				return (byte)floatDataRaw;
			}
			return (byte)Math.Round(255f*(floatDataRaw-windowingMin)/(windowingMax-windowingMin));
		}

		private static string Show(byte[] arrayGray) {
			return string.Join(",",arrayGray);
		}

		[TestMethod]
		public void RawPerWindowingStep_IsDecidedByBitsStored() {
			Assert.AreEqual(1f,DicomHelper.GetRawPerWindowingStep(MakeImage(8,8,0)),"8 bit data is already on the 0-255 scale.");
			Assert.AreEqual(16f,DicomHelper.GetRawPerWindowingStep(MakeImage(16,12,0)),"12 bit: 4095/255, as in 24.3.");
			Assert.AreEqual(256f,DicomHelper.GetRawPerWindowingStep(MakeImage(16,16,0)),"True 16 bit: the 12 most significant bits, times 16.");
			Assert.AreEqual(16f,DicomHelper.GetRawPerWindowingStep(MakeImage(16,8,0)),
				"8 bits stored in 16 bit words keep the factor they had, so their stored windowing shows as before.");
		}

		[TestMethod]
		public void TrueSixteenBit_BrightValuesNoLongerSaturate() {
			BitmapDicom bitmapDicom=MakeImage(16,16,0,4096,32768,40000,60000,65535);
			byte[] arrayGray=Window(bitmapDicom,0,255);
			//raw/256: 0, 16, 128, 156.25, 234.4, 255.99 (above 255, so white)
			CollectionAssert.AreEqual(new byte[] { 0,16,128,156,234,255 },arrayGray,Show(arrayGray));
			//In 24.3 the same image was divided by 16, so everything above raw 4080 was white.
			byte[] arrayGray24_3=bitmapDicom.ArrayDataRaw.Select(x => Window24_3(bitmapDicom,x,0,255)).ToArray();
			CollectionAssert.AreEqual(new byte[] { 0,255,255,255,255,255 },arrayGray24_3,Show(arrayGray24_3));
		}

		[TestMethod]
		public void TrueSixteenBit_WindowUsesTheTwelveMostSignificantBits() {
			//Window 100 to 200 on the 0-255 scale is raw 25600 to 51200 for true 16 bit data.
			BitmapDicom bitmapDicom=MakeImage(16,16,25599,25600,37120,46080,51200,51456);
			byte[] arrayGray=Window(bitmapDicom,100,200);
			//below, the window's bottom, 145 (255*45/100=114.75), 180 (255*80/100=204), the window's top, above
			CollectionAssert.AreEqual(new byte[] { 0,0,115,204,255,255 },arrayGray,Show(arrayGray));
		}

		[TestMethod]
		public void TwelveBit_ShowsExactlyAsIn24_3() {
			List<ushort> listRaw=new List<ushort>();
			for(int raw=0;raw<=4095;raw+=7) {
				listRaw.Add((ushort)raw);
			}
			listRaw.AddRange(new ushort[] { 1599,1600,2400,2401,4080,4081,4095 });
			BitmapDicom bitmapDicom=MakeImage(16,12,listRaw.ToArray());
			foreach((int min,int max) window in new[] { (0,255),(30,200),(99,150),(100,101),(0,1),(254,255) }) {
				byte[] arrayGray=Window(bitmapDicom,window.min,window.max);
				for(int i=0;i<listRaw.Count;i++) {
					Assert.AreEqual(Window24_3(bitmapDicom,listRaw[i],window.min,window.max),arrayGray[i],
						"12 bit raw "+listRaw[i]+", window "+window.min+"-"+window.max);
				}
			}
		}

		[TestMethod]
		public void EightBit_ShowsExactlyAsIn24_3() {
			ushort[] arrayRaw=Enumerable.Range(0,256).Select(x => (ushort)x).ToArray();
			BitmapDicom bitmapDicom=MakeImage(8,8,arrayRaw);
			foreach((int min,int max) window in new[] { (0,255),(30,200) }) {
				byte[] arrayGray=Window(bitmapDicom,window.min,window.max);
				for(int i=0;i<arrayRaw.Length;i++) {
					Assert.AreEqual(Window24_3(bitmapDicom,arrayRaw[i],window.min,window.max),arrayGray[i],"8 bit raw "+arrayRaw[i]);
				}
			}
		}

		[TestMethod]
		public void SixteenBit_Stored4095FromTheOldImport_ShowsTheFullRangeAsBefore() {
			//Before 25.4 the import stored 0-4095 for true 16 bit images, which showed the full range because raw values were divided by 16.
			List<ushort> listRaw=new List<ushort>();
			for(int raw=0;raw<=65535;raw+=97) {
				listRaw.Add((ushort)raw);
			}
			listRaw.Add(65535);
			BitmapDicom bitmapDicom=MakeImage(16,16,listRaw.ToArray());
			byte[] arrayGray=Window(bitmapDicom,0,4095);
			CollectionAssert.AreEqual(Window(bitmapDicom,0,255),arrayGray,"4095 counts as the top of the 0-255 scale.");
			for(int i=0;i<listRaw.Count;i++) {
				int gray24_3=Window24_3(bitmapDicom,listRaw[i],0,4095);
				Assert.IsTrue(Math.Abs(gray24_3-arrayGray[i])<=1,"raw "+listRaw[i]+": "+arrayGray[i]+" now, "+gray24_3+" in 24.3 (one gray level of rounding at most)");
			}
		}

		[TestMethod]
		public void WindowingNeverSet_ShowsTheFullRange() {
			//document.WindowingMax 0 means no windowing; the slider shows 0-255 for it, and so does the picture now (24.3 showed it all white).
			BitmapDicom bitmapDicom16=MakeImage(16,16,0,1000,32768,65535);
			CollectionAssert.AreEqual(Window(bitmapDicom16,0,255),Window(bitmapDicom16,0,0));
			BitmapDicom bitmapDicom12=MakeImage(16,12,0,1000,2048,4095);
			CollectionAssert.AreEqual(Window(bitmapDicom12,0,255),Window(bitmapDicom12,0,0));
		}

		[TestMethod]
		public void ClampWindowing_KeepsTheZeroTo255Scale() {
			Assert.AreEqual(0,DicomHelper.ClampWindowing(-5));
			Assert.AreEqual(0,DicomHelper.ClampWindowing(0));
			Assert.AreEqual(128,DicomHelper.ClampWindowing(128));
			Assert.AreEqual(255,DicomHelper.ClampWindowing(255));
			Assert.AreEqual(255,DicomHelper.ClampWindowing(256));
			Assert.AreEqual(255,DicomHelper.ClampWindowing(4095));
		}

		[TestMethod]
		public void Import_TrueSixteenBit_IsTheFullRangeOnTheZeroTo255Scale() {
			BitmapDicom bitmapDicom=MakeImage(16,16,0,100,30000,65535);
			DicomHelper.CalculateWindowingOnImport(bitmapDicom);
			Assert.AreEqual(0,bitmapDicom.WindowingMin);
			Assert.AreEqual(255,bitmapDicom.WindowingMax,"Was 4095 before 25.4; the stored value now stays within 0-255.");
		}

		[TestMethod]
		public void Import_EightAndTwelveBit_Unchanged() {
			BitmapDicom bitmapDicom8=MakeImage(8,8,0,100,200);
			DicomHelper.CalculateWindowingOnImport(bitmapDicom8);
			Assert.AreEqual(0,bitmapDicom8.WindowingMin);
			Assert.AreEqual(255,bitmapDicom8.WindowingMax);
			//12 bit: many black pixels (thrown out), then a peak at 2000 with values from 1600 to 2400 above the 150 pixel threshold.
			List<ushort> listRaw=new List<ushort>();
			listRaw.AddRange(Enumerable.Repeat((ushort)0,20000));
			for(int raw=1600;raw<=2400;raw++) {
				listRaw.AddRange(Enumerable.Repeat((ushort)raw,200));
			}
			listRaw.AddRange(Enumerable.Repeat((ushort)2000,300));
			BitmapDicom bitmapDicom12=MakeImage(16,12,listRaw.ToArray());
			DicomHelper.CalculateWindowingOnImport(bitmapDicom12);
			Assert.AreEqual(1599/16,bitmapDicom12.WindowingMin,"First value below the threshold under the peak, on the 0-255 scale.");
			Assert.AreEqual(2401/16,bitmapDicom12.WindowingMax,"First value below the threshold above the peak, on the 0-255 scale.");
		}

		[TestMethod]
		public void EightBitColorSettings_ClampOutOfRangeValues() {
			//Ordinary 8 bit images: a stored value above 255 used to wrap around when cast to a byte (300 became 44).
			using Bitmap bitmap=new Bitmap(1,1,PixelFormat.Format32bppArgb);
			bitmap.SetPixel(0,0,Color.FromArgb(255,200,200,200));
			ImageHelper.ApplyColorSettings(bitmap,10,300);
			Assert.AreEqual(198,bitmap.GetPixel(0,0).R,"255*(200-10)/(255-10)=197.8, with 300 counted as 255.");
			Document document=new Document();
			document.WindowingMin=10;
			document.WindowingMax=300;
			using Bitmap bitmapSource=new Bitmap(1,1,PixelFormat.Format32bppArgb);
			bitmapSource.SetPixel(0,0,Color.FromArgb(255,200,200,200));
			using Bitmap bitmapCopy=ImageHelper.CopyWithBrightContrast(document,bitmapSource);
			Assert.AreEqual(198,bitmapCopy.GetPixel(0,0).R);
		}
	}
}
