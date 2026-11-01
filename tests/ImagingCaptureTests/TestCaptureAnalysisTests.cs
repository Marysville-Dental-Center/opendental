// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Linq;
using ImagingCapture.Save;
using ImagingCapture.UI;
using ImagingDeviceManager.Capture;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingCapture.Tests {
	///<summary>TestCaptureAnalysis and HistogramPicture (design 7.8, 13 step 8): the measurements Test capture shows, on made-up images.</summary>
	[TestClass]
	public class TestCaptureAnalysisTests {
		///<summary>A 12-bit horizontal ramp with a little row-to-row variation and two masked corners, as delivered little-endian.</summary>
		private static ushort Ramp(int x,int y) {
			if(x<2 && y<2) {
				return 0;
			}
			return (ushort)(200+x*50+y%3);
		}

		private static AcquiredImage Image16Ramp(string json=TestImages.Json16Bit12) {
			return TestImages.Image16(64,16,Ramp,json);
		}

		[TestMethod]
		public void Ramp16_RangeBitsWindowAndByteOrder() {
			TestCaptureAnalysis analysis=TestCaptureAnalysis.Analyze(Image16Ramp(),TwainPolarity.AsDelivered);
			Assert.IsTrue(analysis.IsValid,analysis.Error);
			Assert.IsTrue(analysis.IsGray16);
			Assert.AreEqual(16,analysis.BitsDelivered);
			Assert.AreEqual(16,analysis.BitsRequested);
			Assert.AreEqual(64*16,analysis.CountPixels);
			Assert.AreEqual(4,analysis.CountZero);
			Assert.AreEqual(200,analysis.MinValue,"The smallest non-zero sample (x=0 on row 3).");
			Assert.AreEqual(200+63*50+2,analysis.MaxValue);
			Assert.AreEqual(12,analysis.EffectiveBitsMeasured,"3352 needs 12 bits.");
			Assert.AreEqual(12,analysis.BitsStoredRule,"Fits 12 bits and the metadata reports 12.");
			Assert.AreEqual(ByteOrderVerdict.LooksRight,analysis.ByteOrderVerdict);
			Assert.IsTrue(analysis.RoughnessSwapped>analysis.RoughnessAsRead*100);
			Assert.AreEqual(4,analysis.HistogramShift);
			Assert.AreEqual(64*16-4,analysis.Histogram.Sum(),"Every non-zero sample is counted once.");
			int windowLow;
			int windowHigh;
			ImagingCapture.Dicom.DicomIntraOralWriter.GetWindow(Image16Ramp().Gray16,out windowLow,out windowHigh);
			Assert.AreEqual(windowLow,analysis.WindowLow);
			Assert.AreEqual(windowHigh,analysis.WindowHigh);
			Assert.AreEqual(64*16,analysis.Preview.Length);
			Assert.AreEqual(0,analysis.Preview[0],"A masked corner is black.");
			Assert.IsTrue(analysis.Preview[63]>=250,"The far end of the ramp is white.");
			Assert.IsNotNull(analysis.Metadata);
		}

		[TestMethod]
		public void Ramp16_NoMetadata_BitsStored16() {
			TestCaptureAnalysis analysis=TestCaptureAnalysis.Analyze(Image16Ramp(json:null),TwainPolarity.AsDelivered);
			Assert.AreEqual(16,analysis.BitsStoredRule,"Unknown effective bits: 16 (the rule never assumes 12).");
			Assert.IsNull(analysis.Metadata);
			StringAssert.Contains(string.Join("\n",analysis.GetResultLines()),"Capture metadata: none.");
		}

		[TestMethod]
		public void Swapped16_LooksSwapped() {
			AcquiredImage image=Image16Ramp();
			image.Gray16=Gray16Samples.SwapBytes(image.Gray16);//what ByteOrder16=LE gives when the source sends big-endian
			TestCaptureAnalysis analysis=TestCaptureAnalysis.Analyze(image,TwainPolarity.AsDelivered);
			Assert.AreEqual(ByteOrderVerdict.LooksSwapped,analysis.ByteOrderVerdict);
			StringAssert.Contains(analysis.GetByteOrderText(),"LOOKS SWAPPED");
		}

		[TestMethod]
		public void Flat16_Inconclusive() {
			TestCaptureAnalysis analysis=TestCaptureAnalysis.Analyze(TestImages.Image16(20,20,(x,y) => 1000),TwainPolarity.AsDelivered);
			Assert.AreEqual(ByteOrderVerdict.Inconclusive,analysis.ByteOrderVerdict);
			analysis=TestCaptureAnalysis.Analyze(TestImages.Image16(3,2,Ramp),TwainPolarity.AsDelivered);
			Assert.AreEqual(ByteOrderVerdict.Inconclusive,analysis.ByteOrderVerdict,"Too few pairs.");
			analysis=TestCaptureAnalysis.Analyze(TestImages.Image16(8,8,(x,y) => 0),TwainPolarity.AsDelivered);
			Assert.IsTrue(analysis.IsValid);
			Assert.AreEqual(0,analysis.MaxValue);
			Assert.AreEqual(0,analysis.EffectiveBitsMeasured);
			Assert.AreEqual(ByteOrderVerdict.Inconclusive,analysis.ByteOrderVerdict);
		}

		[TestMethod]
		public void Invert16_WindowAndPreviewAfterInversion() {
			AcquiredImage image=TestImages.Image16(64,16,Ramp,TestImages.Json16Bit12,AcquiredImage.PixelFlavorVanilla);
			TestCaptureAnalysis analysis=TestCaptureAnalysis.Analyze(image,TwainPolarity.Invert);
			Assert.IsTrue(analysis.IsInverted);
			Assert.AreEqual(200+63*50+2,analysis.MaxValue,"The range is measured as delivered.");
			int windowLow;
			int windowHigh;
			ImagingCapture.Dicom.DicomIntraOralWriter.GetWindow(Gray16Samples.Invert(image.Gray16,12),out windowLow,out windowHigh);
			Assert.AreEqual(windowLow,analysis.WindowLow);
			Assert.AreEqual(windowHigh,analysis.WindowHigh);
			Assert.AreEqual(255,analysis.Preview[3*64],"The ramp's dark start is white after the inversion.");
			Assert.AreEqual(0,analysis.Preview[0],"Masked corners stay black.");
			StringAssert.Contains(string.Join("\n",analysis.GetResultLines()),"vanilla (0 is white)");
		}

		[TestMethod]
		public void Image8_GreyRange() {
			AcquiredImage image=TestImages.Image8(10,4,(x,y) => (byte)(20+x*10+y));
			TestCaptureAnalysis analysis=TestCaptureAnalysis.Analyze(image,TwainPolarity.Invert);
			Assert.IsTrue(analysis.IsValid,analysis.Error);
			Assert.IsFalse(analysis.IsGray16);
			Assert.IsFalse(analysis.IsInverted,"Polarity applies to 16-bit samples only.");
			Assert.AreEqual(8,analysis.BitsDelivered);
			Assert.AreEqual(20,analysis.MinValue);
			Assert.AreEqual(20+90+3,analysis.MaxValue);
			Assert.AreEqual(8,analysis.EffectiveBitsMeasured);
			Assert.AreEqual(ByteOrderVerdict.NotApplicable,analysis.ByteOrderVerdict);
			Assert.AreEqual(40,analysis.Histogram.Sum());
			Assert.AreEqual(1,analysis.Histogram[20]);
			Assert.AreEqual(20+3*10+2,analysis.Preview[2*10+3],"Top-down rows.");
			StringAssert.Contains(string.Join("\n",analysis.GetResultLines()),"Delivered: 8-bit grey over native transfer (asked for 8).");
		}

		[TestMethod]
		public void Image8_In16BitEnable_Noted() {
			AcquiredImage image=TestImages.Image8(4,4,(x,y) => 9);
			image.RequestedBits=16;
			image.IsDeliveredAs8BitIn16BitEnable=true;
			TestCaptureAnalysis analysis=TestCaptureAnalysis.Analyze(image,TwainPolarity.AsDelivered);
			StringAssert.Contains(string.Join("\n",analysis.GetResultLines()),"no 16-bit original");
		}

		[TestMethod]
		public void BadImages_ErrorNotThrow() {
			Assert.IsFalse(TestCaptureAnalysis.Analyze(null,TwainPolarity.AsDelivered).IsValid);
			AcquiredImage image=TestImages.Image16(4,4,(x,y) => 5);
			image.Width=5;
			Assert.IsFalse(TestCaptureAnalysis.Analyze(image,TwainPolarity.AsDelivered).IsValid);
			image=TestImages.Image8(4,4,(x,y) => 5);
			image.Dib=new byte[3];
			TestCaptureAnalysis analysis=TestCaptureAnalysis.Analyze(image,TwainPolarity.AsDelivered);
			Assert.IsFalse(analysis.IsValid);
			Assert.AreEqual(analysis.Error,analysis.GetResultLines().Single());
		}

		[TestMethod]
		public void BitsFor() {
			Assert.AreEqual(0,TestCaptureAnalysis.GetBitsFor(0));
			Assert.AreEqual(1,TestCaptureAnalysis.GetBitsFor(1));
			Assert.AreEqual(12,TestCaptureAnalysis.GetBitsFor(4095));
			Assert.AreEqual(13,TestCaptureAnalysis.GetBitsFor(4096));
			Assert.AreEqual(16,TestCaptureAnalysis.GetBitsFor(65535));
		}

		[TestMethod]
		public void Histogram_BarsAndMarkers() {
			int[] arrayBins=new int[256];
			arrayBins[0]=10;
			arrayBins[128]=5;
			byte[] arrayPixels=HistogramPicture.Render(arrayBins,256,100,new[] { 64 });
			Assert.AreEqual(256*100,arrayPixels.Length);
			Assert.AreEqual(HistogramPicture.Bar,arrayPixels[0],"The tallest bin reaches the top.");
			Assert.AreEqual(HistogramPicture.Bar,arrayPixels[99*256]);
			Assert.AreEqual(HistogramPicture.Background,arrayPixels[49*256+128],"Half as tall.");
			Assert.AreEqual(HistogramPicture.Bar,arrayPixels[50*256+128]);
			Assert.AreEqual(HistogramPicture.Marker,arrayPixels[10*256+64]);
			Assert.AreEqual(HistogramPicture.Background,arrayPixels[10*256+200]);
			Assert.IsTrue(HistogramPicture.Render(null,10,10).All(x => x==HistogramPicture.Background));
			Assert.IsTrue(HistogramPicture.Render(new int[256],10,10).All(x => x==HistogramPicture.Background));
			byte[] arrayNarrow=HistogramPicture.Render(arrayBins,64,10);
			Assert.AreEqual(HistogramPicture.Bar,arrayNarrow[0],"Four bins per column: the tallest of them is drawn.");
			Assert.AreEqual(HistogramPicture.Bar,arrayNarrow[9*64+32]);
			Assert.AreEqual(HistogramPicture.Background,arrayNarrow[4*64+32],"Bins 128 to 131 hold half of the tallest.");
		}
	}
}
