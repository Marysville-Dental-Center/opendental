// The window the program stores when it imports a gray DICOM image (DicomHelper.CalculateWindowingOnImport, saved as document.WindowingMin
// and WindowingMax by ImageStore.SetDicomImportFields and Documents.InsertMissing) for 9 to 11 bits stored. The full reader moves those
// samples up to 12 bits, so only every 2nd, 4th or 8th value of the 4096-entry histogram has pixels; before the fix the hunt from the peak
// stopped at the empty bin next to it and stored a window of at most one step, which ApplyWindowing draws as a black-and-white threshold
// image. The same made-up image is stored at 12, 11, 10 and 9 bits, as MONOCHROME2, MONOCHROME1 and signed, and each window must match
// the 12-bit one. The 12-bit windows must be exactly what the algorithm gave before the fix (LegacyImportWindow below). Everything is
// synthetic; no database or file is used.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace DicomReaderTests {
	[TestClass]
	public class ImportWindowBitDepthTests {
		private const int WIDTH=1600;
		private const int HEIGHT=1200;
		private const string MONO2="MONOCHROME2";
		private const string MONO1="MONOCHROME1";
		private const string SIGNED="signed MONOCHROME2";
		///<summary>The 12-bit windows of the image, on the 0-255 scale, as the algorithm gave them before the fix: soft tissue and enamel
		///between 50 and 220. MONOCHROME1 hunts the other way, so its minimum is the larger value.</summary>
		private static readonly Dictionary<string,(int min,int max)> _dictWindow12=new Dictionary<string,(int min,int max)> {
			{ MONO2,(50,220) },
			{ MONO1,(205,35) },
			{ SIGNED,(50,220) },
		};
		private static ushort[] _arraySamples12;

		///<summary>The 12-bit values of a made-up radiograph with a smooth histogram: a 10% black background (0), then soft tissue (70% of the
		///rest, centered at 0.40 of the range) and enamel (30%, centered at 0.75), each value as often as the two-part density says, so the
		///histogram has no noise. The pixels are in ascending order: a black band at the top, then a smooth gradient from dark to bright.</summary>
		private static ushort[] GetSamples12() {
			if(_arraySamples12!=null) {
				return _arraySamples12;
			}
			double[] arrayDensity=new double[4096];
			for(int v=0;v<4096;v++) {
				double x=v/4095.0;
				arrayDensity[v]=0.7*Math.Exp(-(x-0.40)*(x-0.40)/(2*0.10*0.10))+0.3*(0.10/0.06)*Math.Exp(-(x-0.75)*(x-0.75)/(2*0.06*0.06));
			}
			double total=arrayDensity.Sum();
			int countContent=(int)(WIDTH*HEIGHT*0.9);
			int[] arrayCounts=new int[4096];
			for(int v=1;v<4096;v++) {
				arrayCounts[v]=(int)Math.Round(countContent*arrayDensity[v]/total);
			}
			ushort[] arraySamples=new ushort[WIDTH*HEIGHT];
			int idx=WIDTH*HEIGHT-arrayCounts.Sum();//the black background: 0, as the array starts
			Assert.IsTrue(idx>=WIDTH*HEIGHT/20,"Some black background: "+idx);
			for(int v=1;v<4096;v++) {
				for(int n=0;n<arrayCounts[v];n++) {
					arraySamples[idx++]=(ushort)v;
				}
			}
			_arraySamples12=arraySamples;
			return arraySamples;
		}

		///<summary>The image at bitsStored bits (the low 12-bitsStored bits of each 12-bit value dropped), in 16-bit words with High Bit
		///bitsStored-1. MONOCHROME1 stores the image inverted, with the background still at 0. Signed stores each value minus half the range as
		///two's complement, sign-extended to 16 bits, so the most negative value is the background; the reader moves it back up by half the
		///range, which gives the same values as the unsigned image.</summary>
		private static byte[] MakeFile(ushort[] arraySamples12,int bitsStored,string variant) {
			int shiftDown=12-bitsStored;
			int top=(1<<bitsStored)-1;
			int half=1<<(bitsStored-1);
			ushort[] arrayWords=new ushort[arraySamples12.Length];
			for(int i=0;i<arraySamples12.Length;i++) {
				int value=arraySamples12[i]>>shiftDown;
				if(variant==MONO1 && arraySamples12[i]!=0) {
					value=top-value;
				}
				if(variant==SIGNED) {
					arrayWords[i]=unchecked((ushort)(short)(value-half));
				}
				else {
					arrayWords[i]=(ushort)value;
				}
			}
			string photometric=variant==MONO1 ? MONO1 : MONO2;
			ushort pixelRepresentation=variant==SIGNED ? (ushort)1 : (ushort)0;
			List<SynElement> listElements=SyntheticDicom.ImageDataset(HEIGHT,WIDTH,16,(ushort)bitsStored,(ushort)(bitsStored-1),pixelRepresentation,
				photometric,SyntheticDicom.Words(arrayWords));
			return SyntheticDicom.MakeFile(listElements);
		}

		private static BitmapDicom Read(byte[] arrayFile,string what) {
			DicomImageReadResult result=DicomHelper.ReadStreamResult(new MemoryStream(arrayFile));
			Assert.AreEqual(DicomImageReadStatus.Read,result.Status,what+": "+result);
			BitmapDicom bitmapDicom=result.BitmapDicom;
			Assert.AreEqual(WIDTH,bitmapDicom.Width,what);
			Assert.AreEqual(HEIGHT,bitmapDicom.Height,what);
			Assert.AreEqual(16,bitmapDicom.BitsAllocated,what);
			Assert.AreEqual(12,bitmapDicom.BitsStored,what+": 9 to 12 bits stored are shown on the 12 bit scale.");
			return bitmapDicom;
		}

		///<summary>CalculateWindowingOnImport as it was before the data step was added (one bin at a time, a threshold of 150 pixels), kept
		///here to prove the 12-bit windows did not change and to show what the 9 to 11 bit windows were.</summary>
		private static (int min,int max) LegacyImportWindow(BitmapDicom bitmapDicom) {
			int[] histogram=new int[4096];
			for(int i=0;i<bitmapDicom.ArrayDataRaw.Length;i++) {
				histogram[bitmapDicom.ArrayDataRaw[i]]++;
			}
			int idxBlackPixels=Array.IndexOf(histogram,histogram.Max());
			if(idxBlackPixels<100) {
				histogram[idxBlackPixels]=0;
			}
			int idxPeak=Array.IndexOf(histogram,histogram.Max());
			int windowLow=0;
			for(int i=idxPeak;i>=0;i--) {
				if(histogram[i]<150) {
					windowLow=i/16;
					break;
				}
			}
			int windowHigh=4095;
			for(int i=idxPeak;i<histogram.Length;i++) {
				if(histogram[i]<150) {
					windowHigh=i/16;
					break;
				}
			}
			if(bitmapDicom.PhotometricInterp==EnumDicomPhotometricInterp.MONOCHROME1) {
				return (windowHigh==4095 ? 0 : windowHigh,windowLow==0 ? 4095 : windowLow);
			}
			return (windowLow,windowHigh);
		}

		///<summary>How many different gray levels ApplyWindowing draws with this window.</summary>
		private static int CountGrayLevels(BitmapDicom bitmapDicom,int windowingMin,int windowingMax) {
			using(Bitmap bitmap=DicomHelper.ApplyWindowing(bitmapDicom,windowingMin,windowingMax)) {
				BitmapData bitmapData=bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
				try {
					byte[] arrayBytes=new byte[bitmapData.Stride*bitmap.Height];
					Marshal.Copy(bitmapData.Scan0,arrayBytes,0,arrayBytes.Length);
					bool[] arrayIsUsed=new bool[256];
					for(int y=0;y<bitmap.Height;y++) {
						for(int x=0;x<bitmap.Width;x++) {
							arrayIsUsed[arrayBytes[y*bitmapData.Stride+x*4+2]]=true;
						}
					}
					return arrayIsUsed.Count(x => x);
				}
				finally {
					bitmap.UnlockBits(bitmapData);
				}
			}
		}

		[TestMethod]
		public void ImportWindow_9To11BitsStored_MatchesThe12BitWindow_InEveryVersion() {
			ushort[] arraySamples12=GetSamples12();
			foreach(string variant in new[] { MONO2,MONO1,SIGNED }) {
				//12 bits: the data step is 1, and the window is exactly what it was before the fix.
				BitmapDicom bitmapDicom12=Read(MakeFile(arraySamples12,12,variant),variant+" 12 bits");
				Assert.AreEqual(1,DicomHelper.GetHistogramStep(bitmapDicom12.ArrayDataRaw),variant+" 12 bits");
				DicomHelper.CalculateWindowingOnImport(bitmapDicom12);
				(int min,int max) window12=(bitmapDicom12.WindowingMin,bitmapDicom12.WindowingMax);
				Assert.AreEqual(LegacyImportWindow(bitmapDicom12),window12,variant+" 12 bits: the window did not change.");
				Assert.AreEqual(_dictWindow12[variant],window12,variant+" 12 bits: the window did not change.");
				Assert.IsTrue(Math.Abs(window12.max-window12.min)>=20,variant+" 12 bits: "+window12);
				for(int bitsStored=11;bitsStored>=9;bitsStored--) {
					string what=variant+" "+bitsStored+" bits";
					int step=1<<(12-bitsStored);
					BitmapDicom bitmapDicom=Read(MakeFile(arraySamples12,bitsStored,variant),what);
					Assert.AreEqual(step,DicomHelper.GetHistogramStep(bitmapDicom.ArrayDataRaw),what);
					//What the import stored before the fix: a window of at most one step, a threshold.
					(int min,int max) windowLegacy=LegacyImportWindow(bitmapDicom);
					Assert.IsTrue(Math.Abs(windowLegacy.max-windowLegacy.min)<=1,what+": the legacy window was "+windowLegacy);
					DicomHelper.CalculateWindowingOnImport(bitmapDicom);
					(int min,int max) window=(bitmapDicom.WindowingMin,bitmapDicom.WindowingMax);
					string summary=what+": window "+window+", 12-bit window "+window12;
					Assert.IsTrue(Math.Abs(window.min-window12.min)<=2,summary);
					Assert.IsTrue(Math.Abs(window.max-window12.max)<=2,summary);
					Assert.IsTrue(Math.Abs(window.max-window.min)>=20,summary);
					if(variant!=MONO1) {
						//ApplyWindowing draws the image in grays, not black and white. (A MONOCHROME1 window has its minimum above its maximum, which
						//ApplyWindowing draws in two levels at every bit depth, 12 included: not part of this fix.)
						int countLevels=CountGrayLevels(bitmapDicom,window.min,window.max);
						Assert.IsTrue(countLevels>=200,summary+", "+countLevels+" gray levels");
						Assert.IsTrue(CountGrayLevels(bitmapDicom,windowLegacy.min,windowLegacy.max)<=3,what+": the legacy window drew a threshold image.");
					}
				}
			}
		}

		[TestMethod]
		public void ImportWindow_12BitsAllEven_MatchesTheWindowWithOddValues() {
			ushort[] arraySamples12=GetSamples12();
			BitmapDicom bitmapDicom12=Read(MakeFile(arraySamples12,12,MONO2),"12 bits");
			DicomHelper.CalculateWindowingOnImport(bitmapDicom12);
			//The same image with the lowest bit of every value cleared, still 12 bits stored (the subset reader reads it).
			ushort[] arrayEven=arraySamples12.Select(x => (ushort)(x & ~1)).ToArray();
			BitmapDicom bitmapDicom=Read(MakeFile(arrayEven,12,MONO2),"12 bits, all even");
			Assert.AreEqual(2,DicomHelper.GetHistogramStep(bitmapDicom.ArrayDataRaw));
			(int min,int max) windowLegacy=LegacyImportWindow(bitmapDicom);
			Assert.IsTrue(windowLegacy.max-windowLegacy.min<=1,"The legacy window was "+windowLegacy);
			DicomHelper.CalculateWindowingOnImport(bitmapDicom);
			string summary="window "+(bitmapDicom.WindowingMin,bitmapDicom.WindowingMax)+", with odd values "+(bitmapDicom12.WindowingMin,bitmapDicom12.WindowingMax);
			Assert.IsTrue(Math.Abs(bitmapDicom.WindowingMin-bitmapDicom12.WindowingMin)<=2,summary);
			Assert.IsTrue(Math.Abs(bitmapDicom.WindowingMax-bitmapDicom12.WindowingMax)<=2,summary);
			Assert.IsTrue(bitmapDicom.WindowingMax-bitmapDicom.WindowingMin>=20,summary);
		}

		[TestMethod]
		public void GetHistogramStep_IsTheLowestBitSetInAnyValue_AtMost8() {
			Assert.AreEqual(1,DicomHelper.GetHistogramStep(new ushort[0]),"no values");
			Assert.AreEqual(1,DicomHelper.GetHistogramStep(new ushort[] { 0,0,0 }),"only black");
			Assert.AreEqual(1,DicomHelper.GetHistogramStep(new ushort[] { 0,4,8,3 }),"one odd value");
			Assert.AreEqual(2,DicomHelper.GetHistogramStep(new ushort[] { 0,4,6,4094 }));
			Assert.AreEqual(4,DicomHelper.GetHistogramStep(new ushort[] { 0,4,8,4092 }));
			Assert.AreEqual(8,DicomHelper.GetHistogramStep(new ushort[] { 0,8,4088 }));
			Assert.AreEqual(8,DicomHelper.GetHistogramStep(new ushort[] { 0,16,32,4080 }),"at most 8");
		}
	}
}
