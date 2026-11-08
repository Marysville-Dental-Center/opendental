// Tests of the image comparer, the bitmap facts the recordings write, the STA runner and the recorder's manifest (package TC-0).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace ToothChartImageTests {
	[TestClass]
	public class SupportTests {
		private static Bitmap NewBitmap(int width,int height,Color color,PixelFormat pixelFormat=PixelFormat.Format32bppArgb) {
			Bitmap bitmap=new Bitmap(width,height,pixelFormat);
			using Graphics g=Graphics.FromImage(bitmap);
			g.Clear(color);
			return bitmap;
		}

		[TestMethod]
		public void Compare_IdenticalImages_Pass() {
			using Bitmap bitmap1=NewBitmap(64,48,Color.Gray);
			using Bitmap bitmap2=NewBitmap(64,48,Color.Gray);
			ChartImageCompareResult result=ChartImageCompare.Compare(bitmap1,bitmap2);
			Assert.IsTrue(result.IsPass);
			Assert.AreEqual(0,result.CountDifferent);
			Assert.AreEqual(64*48,result.CountPixels);
		}

		[TestMethod]
		public void Compare_ChannelDifferenceOfEight_IsNotADifference_ButNineIs() {
			using Bitmap bitmap1=NewBitmap(20,20,Color.FromArgb(255,100,100,100));
			using Bitmap bitmap2=NewBitmap(20,20,Color.FromArgb(255,108,92,100));
			Assert.AreEqual(0,ChartImageCompare.Compare(bitmap1,bitmap2).CountDifferent,"8 of 255 is within tolerance.");
			using Bitmap bitmap3=NewBitmap(20,20,Color.FromArgb(255,100,100,109));
			Assert.AreEqual(400,ChartImageCompare.Compare(bitmap1,bitmap3).CountDifferent,"9 of 255 is a difference.");
			using Bitmap bitmap4=NewBitmap(20,20,Color.FromArgb(240,100,100,100));
			Assert.AreEqual(400,ChartImageCompare.Compare(bitmap1,bitmap4).CountDifferent,"Alpha counts too.");
		}

		[TestMethod]
		public void Compare_SmallLocalError_FailsTheBlockRule_AndScatteredOnesPass() {
			//410x307 = 125,870 pixels, so 0.1% allows 125 different pixels.
			using Bitmap bitmapExpected=NewBitmap(410,307,Color.White);
			using Bitmap bitmapLocal=NewBitmap(410,307,Color.White);
			for(int y=100;y<105;y++) {
				for(int x=200;x<205;x++) {
					bitmapLocal.SetPixel(x,y,Color.Black);//25 pixels in one block, like a missing "S"
				}
			}
			ChartImageCompareResult resultLocal=ChartImageCompare.Compare(bitmapExpected,bitmapLocal);
			Assert.AreEqual(25,resultLocal.CountDifferent);
			Assert.AreEqual(25,resultLocal.CountDifferentInWorstBlock);
			Assert.AreEqual(new Point(192,96),resultLocal.PointWorstBlock);
			Assert.IsFalse(resultLocal.IsPass,"More than 24 different pixels in one 16x16 block fail.");
			using Bitmap bitmapScattered=NewBitmap(410,307,Color.White);
			for(int i=0;i<120;i++) {
				bitmapScattered.SetPixel((i*37)%410,(i*53)%307,Color.Black);
			}
			ChartImageCompareResult resultScattered=ChartImageCompare.Compare(bitmapExpected,bitmapScattered);
			Assert.AreEqual(120,resultScattered.CountDifferent);
			Assert.IsTrue(resultScattered.IsPass,resultScattered.ToString());
			using Bitmap bitmapMany=NewBitmap(410,307,Color.White);
			for(int i=0;i<130;i++) {
				bitmapMany.SetPixel((i*37)%410,(i*53)%307,Color.Black);
			}
			Assert.IsFalse(ChartImageCompare.Compare(bitmapExpected,bitmapMany).IsPass,"More than 0.1% of the pixels fail.");
		}

		[TestMethod]
		public void Compare_DifferentSizes_Fail() {
			using Bitmap bitmap1=NewBitmap(410,307,Color.White);
			using Bitmap bitmap2=NewBitmap(500,370,Color.White);
			ChartImageCompareResult result=ChartImageCompare.Compare(bitmap1,bitmap2);
			Assert.IsFalse(result.IsSameSize);
			Assert.IsFalse(result.IsPass);
		}

		[TestMethod]
		public void WriteFailureImages_WritesExpectedActualAndARedDiff() {
			string folder=Path.Combine(Path.GetTempPath(),"tc0-compare-"+Guid.NewGuid().ToString("N"));
			try {
				using Bitmap bitmapExpected=NewBitmap(32,32,Color.White);
				using Bitmap bitmapActual=NewBitmap(32,32,Color.White);
				bitmapActual.SetPixel(3,4,Color.Blue);
				string pathDiff=ChartImageCompare.WriteFailureImages(folder,"scene",bitmapExpected,bitmapActual);
				Assert.IsTrue(File.Exists(Path.Combine(folder,"scene.expected.png")));
				Assert.IsTrue(File.Exists(Path.Combine(folder,"scene.actual.png")));
				using Bitmap bitmapDiff=new Bitmap(pathDiff);
				Assert.AreEqual(Color.FromArgb(255,255,0,0),bitmapDiff.GetPixel(3,4));
				Color colorGrey=bitmapDiff.GetPixel(0,0);
				Assert.IsTrue(colorGrey.R==colorGrey.G && colorGrey.G==colorGrey.B && colorGrey.R>128,"The rest is grey.");
			}
			finally {
				if(Directory.Exists(folder)) {
					Directory.Delete(folder,true);
				}
			}
		}

		[TestMethod]
		public void DescribeBitmap_GivesPixelFormatAlphaAndColours() {
			using Bitmap bitmap24=NewBitmap(10,10,Color.Red,PixelFormat.Format24bppRgb);
			BitmapFacts bitmapFacts24=ChartImageCompare.DescribeBitmap(bitmap24);
			Assert.AreEqual(PixelFormat.Format24bppRgb,bitmapFacts24.PixelFormat);
			Assert.IsFalse(bitmapFacts24.HasAlphaChannel);
			Assert.IsTrue(bitmapFacts24.IsOpaque);
			Assert.AreEqual("every alpha is 255 (format without an alpha channel)",bitmapFacts24.GetAlphaText());
			Assert.IsTrue(bitmapFacts24.IsBlank);
			using Bitmap bitmap32=NewBitmap(10,10,Color.Red);
			bitmap32.SetPixel(1,1,Color.FromArgb(0,0,0,0));
			bitmap32.SetPixel(2,2,Color.FromArgb(128,0,0,255));
			BitmapFacts bitmapFacts32=ChartImageCompare.DescribeBitmap(bitmap32);
			Assert.AreEqual(PixelFormat.Format32bppArgb,bitmapFacts32.PixelFormat);
			Assert.IsTrue(bitmapFacts32.HasAlphaChannel);
			Assert.IsFalse(bitmapFacts32.IsOpaque);
			Assert.AreEqual(2,bitmapFacts32.CountAlphaBelow255);
			Assert.AreEqual("2 pixels have alpha below 255 (lowest 0)",bitmapFacts32.GetAlphaText());
			Assert.AreEqual(3,bitmapFacts32.CountColors);
			Assert.IsFalse(bitmapFacts32.IsBlank);
			Assert.AreEqual(64,bitmapFacts32.PixelsSha256.Length);
			using Bitmap bitmap32Copy=(Bitmap)bitmap32.Clone();
			Assert.AreEqual(bitmapFacts32.PixelsSha256,ChartImageCompare.DescribeBitmap(bitmap32Copy).PixelsSha256);
		}

		[TestMethod]
		public void PngWriter_RoundTripsThePixels_AndGivesTheSameBytesEveryTime() {
			string folder=Path.Combine(Path.GetTempPath(),"tc0-png-"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(folder);
			try {
				//A picture with gradients, edges and every filter's case: opaque, so it is written as RGB.
				using Bitmap bitmapOpaque=new Bitmap(67,41,PixelFormat.Format32bppArgb);
				for(int y=0;y<bitmapOpaque.Height;y++) {
					for(int x=0;x<bitmapOpaque.Width;x++) {
						bitmapOpaque.SetPixel(x,y,Color.FromArgb(255,(x*7+y*3)%256,(x*x+y)%256,(x+y*y*5)%256));
					}
				}
				byte[] arrayPng=PngWriter.Encode(bitmapOpaque);
				CollectionAssert.AreEqual(arrayPng,PngWriter.Encode(bitmapOpaque),"The same pixels give the same bytes.");
				Assert.AreEqual(2,arrayPng[25],"Colour type 2 (RGB) when every alpha is 255.");
				string pathOpaque=Path.Combine(folder,"opaque.png");
				File.WriteAllBytes(pathOpaque,arrayPng);
				using(Bitmap bitmapRead=new Bitmap(pathOpaque)) {
					Assert.AreEqual(bitmapOpaque.Size,bitmapRead.Size);
					CollectionAssert.AreEqual(ChartImageCompare.GetPixelsArgb(bitmapOpaque),ChartImageCompare.GetPixelsArgb(bitmapRead));
				}
				//With alpha below 255 somewhere, RGBA.
				using Bitmap bitmapAlpha=new Bitmap(9,5,PixelFormat.Format32bppArgb);
				for(int y=0;y<bitmapAlpha.Height;y++) {
					for(int x=0;x<bitmapAlpha.Width;x++) {
						bitmapAlpha.SetPixel(x,y,Color.FromArgb(x==4 && y==2?7:255,x*20,y*40,200));
					}
				}
				byte[] arrayPngAlpha=PngWriter.Encode(bitmapAlpha);
				Assert.AreEqual(6,arrayPngAlpha[25],"Colour type 6 (RGBA) when an alpha is below 255.");
				string pathAlpha=Path.Combine(folder,"alpha.png");
				File.WriteAllBytes(pathAlpha,arrayPngAlpha);
				using(Bitmap bitmapRead=new Bitmap(pathAlpha)) {
					Assert.AreEqual(7,bitmapRead.GetPixel(4,2).A);
					Assert.AreEqual(Color.FromArgb(255,60,80,200),bitmapRead.GetPixel(3,2));
				}
			}
			finally {
				Directory.Delete(folder,true);
			}
		}

		[TestMethod]
		public void StaRunner_RunsOnAnStaThread_AndKeepsTheExceptionType() {
			Assert.AreEqual(ApartmentState.STA,StaRunner.Run(() => Thread.CurrentThread.GetApartmentState()));
			Expect.Throws<AssertInconclusiveException>(() => StaRunner.Run(() => Assert.Inconclusive("inside")));
			InvalidOperationException ex=Expect.Throws<InvalidOperationException>(() => StaRunner.Run(() => {
				throw new InvalidOperationException("boom");
			}));
			Assert.AreEqual("boom",ex.Message);
		}

		[TestMethod]
		public void Recorder_ParsesTheModesToRecord() {
			CollectionAssert.AreEqual(LegacyRecorder.ArrayEngines,LegacyRecorder.ParseEngines(null));
			CollectionAssert.AreEqual(LegacyRecorder.ArrayEngines,LegacyRecorder.ParseEngines(""));
			CollectionAssert.AreEqual(new[] { DrawingMode.DirectX },LegacyRecorder.ParseEngines("directx"));
			CollectionAssert.AreEqual(new[] { DrawingMode.DirectX,DrawingMode.Simple2D },LegacyRecorder.ParseEngines("Simple2D, DirectX"),
				"Always in recording order.");
		}

		[TestMethod]
		public void Recorder_WritesTheManifestAndIndexFromTheTextFiles() {
			string folder=Path.Combine(Path.GetTempPath(),"tc0-manifest-"+Guid.NewGuid().ToString("N"));
			try {
				string folderPng=Path.Combine(folder,"DirectX","410x307");
				Directory.CreateDirectory(folderPng);
				File.WriteAllLines(Path.Combine(folderPng,"empty-universal.txt"),new[] { "Scene: empty-universal","Group: empty","Requested mode: DirectX",
					"Started mode: OpenGL","Size: 410x307","Bitmap: 410x307 Format32bppArgb","Alpha: every alpha is 255","Adapter: A","Adapter: B" });
				using(Bitmap bitmap=NewBitmap(410,307,Color.Gray)) {
					bitmap.Save(Path.Combine(folderPng,"empty-universal.png"),ImageFormat.Png);
				}
				Dictionary<string,string> dict=LegacyRecording.ReadSidecar(Path.Combine(folderPng,"empty-universal.txt"));
				Assert.AreEqual("A; B",dict["Adapter"],"Repeated keys are joined.");
				LegacyRecording.WriteManifestAndIndex(folder);
				string[] arrayManifest=File.ReadAllLines(Path.Combine(folder,"manifest.txt"));
				Assert.IsTrue(arrayManifest[2].StartsWith("File\tScene\tGroup\tRequested mode\tStarted mode"));
				Assert.AreEqual(4,arrayManifest.Length);
				StringAssert.StartsWith(arrayManifest[3],"DirectX/410x307/empty-universal.png\tempty-universal\tempty\tDirectX\tOpenGL\t410x307");
				string html=File.ReadAllText(Path.Combine(folder,"index.html"));
				StringAssert.Contains(html,"src=\"DirectX/410x307/empty-universal.png\"");
				StringAssert.Contains(html,"drawn by OpenGL","A picture drawn by a fallback mode says so.");
				StringAssert.Contains(html,"<option>perio</option>");
				Assert.AreEqual(ChartScenes.GetAll().Count,html.Split(new[] { "<tbody data-group=" },StringSplitOptions.None).Length-1,"One row group per scene.");
			}
			finally {
				if(Directory.Exists(folder)) {
					Directory.Delete(folder,true);
				}
			}
		}
	}
}
