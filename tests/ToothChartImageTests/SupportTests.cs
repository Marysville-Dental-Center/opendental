// Tests of the image comparer, the bitmap facts the recordings write, the STA runner and the recorder's manifest (package TC-0), and of the WPF
// gate, recorder and gallery with a forced render failure (TC-1).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;
using OpenPmsDentalBusiness;
using SparksToothChart;

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
		public void Compare_ClusterAcrossBlockLines_FailsTheBlockRule() {
			//A cluster of 25 different pixels on the corner of four 16x16 grid blocks is split into four small parts, and on a second grid offset by
			//half a block (8,8) it crosses a line there too: the rule must look at every 16x16 window.
			using Bitmap bitmapExpected=NewBitmap(410,307,Color.White);
			foreach(Point pointCorner in new[] { new Point(126,94),new Point(28,20),new Point(395,290) }) {
				using Bitmap bitmapCluster=NewBitmap(410,307,Color.White);
				for(int y=pointCorner.Y;y<pointCorner.Y+5;y++) {
					for(int x=pointCorner.X;x<pointCorner.X+5;x++) {
						bitmapCluster.SetPixel(x,y,Color.Black);
					}
				}
				ChartImageCompareResult result=ChartImageCompare.Compare(bitmapExpected,bitmapCluster);
				Assert.AreEqual(25,result.CountDifferent);
				Assert.AreEqual(25,result.CountDifferentInWorstBlock,"Every window: at "+pointCorner);
				Assert.IsFalse(result.IsPass,"A cluster of 25 pixels across block lines at "+pointCorner+" fails: "+result);
			}
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
				Assert.IsFalse(html.Contains("DirectX, smoothed"),"No smoothed column without smoothed pictures.");
			}
			finally {
				if(Directory.Exists(folder)) {
					Directory.Delete(folder,true);
				}
			}
		}

		[TestMethod]
		public void Recorder_IndexShowsTheSmoothedPictures_NextToDirectX_AndTheAdapter() {
			string folder=Path.Combine(Path.GetTempPath(),"tc0-smoothed-"+Guid.NewGuid().ToString("N"));
			string folderSmoothed=LegacyRecorder.GetSmoothedFolder(folder);
			Assert.AreEqual(folder+"-smoothed",folderSmoothed);
			Assert.AreEqual(Path.GetDirectoryName(folder),Path.GetDirectoryName(folderSmoothed),"Beside the recording, never inside it.");
			try {
				using Bitmap bitmap=NewBitmap(410,307,Color.Gray);
				WritePicture(folder,"DirectX/410x307/restored-adult",bitmap,"DirectX adapter: 0 (Hardware device): Example adapter, driver 1.2.3.4");
				WritePicture(folderSmoothed,"410x307/restored-adult",bitmap,"Device or pixel format: DirectX device format 0;Hardware;64;D16;X8R8G8B8;FourSamples");
				LegacyRecording.WriteManifestAndIndex(folder);
				string html=File.ReadAllText(Path.Combine(folder,"index.html"));
				int indexDirectX=html.IndexOf("<th>DirectX</th>",StringComparison.Ordinal);
				Assert.AreEqual(indexDirectX+"<th>DirectX</th>".Length,html.IndexOf("<th>DirectX, smoothed</th>",StringComparison.Ordinal),
					"The smoothed column comes right after DirectX.");
				StringAssert.Contains(html,"src=\"DirectX/410x307/restored-adult.png\"");
				string srcSmoothed="src=\"../"+Path.GetFileName(folderSmoothed)+"/410x307/restored-adult.png\"";
				StringAssert.Contains(html,srcSmoothed);
				Assert.IsTrue(html.IndexOf(srcSmoothed,StringComparison.Ordinal)>html.IndexOf("src=\"DirectX/410x307/restored-adult.png\"",StringComparison.Ordinal),
					"The smoothed picture follows the unsmoothed one of the same scene and size.");
				StringAssert.Contains(html,"smoothed, for the gallery only (never compared): DirectX device format 0;Hardware;64;D16;X8R8G8B8;FourSamples");
				StringAssert.Contains(html,"adapter 0 (Hardware device): Example adapter, driver 1.2.3.4","A DirectX caption names the adapter.");
				string[] arrayManifest=File.ReadAllLines(Path.Combine(folder,"manifest.txt"));
				Assert.AreEqual(4,arrayManifest.Length,"The manifest lists the compared pictures only.");
				StringAssert.Contains(arrayManifest[2],"\tDevice or pixel format\tDirectX adapter\tFailed");
				StringAssert.Contains(arrayManifest[3],"\t0 (Hardware device): Example adapter, driver 1.2.3.4\t");
			}
			finally {
				foreach(string folderDelete in new[] { folder,folderSmoothed }) {
					if(Directory.Exists(folderDelete)) {
						Directory.Delete(folderDelete,true);
					}
				}
			}
		}

		[TestMethod]
		public void Recorder_ParsesTheSceneNamesAndTheSmoothedMode() {
			Assert.IsNull(LegacyRecorder.ParseSceneNames(null));
			Assert.IsNull(LegacyRecorder.ParseSceneNames(" "));
			CollectionAssert.AreEqual(new[] { "a","b" },LegacyRecorder.ParseSceneNames(" a, b,,"));
			Assert.AreEqual(SmoothedMode.Also,LegacyRecorder.ParseSmoothed(null));
			Assert.AreEqual(SmoothedMode.Also,LegacyRecorder.ParseSmoothed("1"));
			Assert.AreEqual(SmoothedMode.None,LegacyRecorder.ParseSmoothed("0"));
			Assert.AreEqual(SmoothedMode.None,LegacyRecorder.ParseSmoothed("No"));
			Assert.AreEqual(SmoothedMode.Only,LegacyRecorder.ParseSmoothed("ONLY"));
		}

		[TestMethod]
		public void Recorder_DrawsTheSmoothedPictures_OnlyWithTheSearchedMultisampledFormat() {
			string formatSearched="0;Hardware;64;D16;X8R8G8B8;FourSamples";
			string formatBaseline="0;Hardware;64;D16;X8R8G8B8;None";
			//The default path of the build PC: the search found a multisampled format, and the baseline is drawn without multisampling.
			Assert.IsTrue(LegacyRecording.CanRecordSmoothed(false,formatSearched,formatBaseline,out string reasonSkip),reasonSkip);
			Assert.IsNull(reasonSkip);
			//DirectX cannot start.
			Assert.IsFalse(LegacyRecording.CanRecordSmoothed(false,"none (d3dx9_43.dll is missing)",null,out reasonSkip));
			Assert.AreEqual("DirectX cannot start.",reasonSkip);
			Assert.IsFalse(LegacyRecording.CanRecordSmoothed(false,"none (no standard format starts)",null,out reasonSkip));
			//A given format: there was no search, so there is no format to draw them with (FindDirectXFormat's given branch), whatever the strings are.
			Assert.IsFalse(LegacyRecording.CanRecordSmoothed(true,"not searched (given)",formatBaseline,out reasonSkip));
			StringAssert.Contains(reasonSkip,LegacyRecorder.EnvDirectXFormat+"="+formatBaseline);
			Assert.IsFalse(LegacyRecording.CanRecordSmoothed(true,"not searched (given)",formatSearched,out reasonSkip),"A given multisampled format too.");
			Assert.IsFalse(LegacyRecording.CanRecordSmoothed(true,formatSearched,formatBaseline,out reasonSkip),"Given wins over a format-like search value.");
			//A search value that names no device format, given or not: DirectXDeviceFormat would read it as invalid and DirectX would not start.
			Assert.IsFalse(LegacyRecording.CanRecordSmoothed(false,"not searched (given)",formatBaseline,out reasonSkip));
			StringAssert.Contains(reasonSkip,"no device format");
			Assert.IsFalse(LegacyRecording.CanRecordSmoothed(false,null,formatBaseline,out reasonSkip));
			//The search's format is the baseline's (it does not multisample, or does not start without multisampling): the pictures would be the baseline's.
			Assert.IsFalse(LegacyRecording.CanRecordSmoothed(false,formatBaseline,formatBaseline,out reasonSkip));
			StringAssert.Contains(reasonSkip,"would be the baseline's");
			Assert.IsFalse(LegacyRecording.CanRecordSmoothed(false,formatSearched,formatSearched,out reasonSkip));
		}

		[TestMethod]
		public void Recorder_SmoothedList_NamesSizesOfItsScenes_AndDirectXNeedsAControlForTheAdapter() {
			Assert.IsTrue(LegacyRecorder.ListSmoothed.Count>=8 && LegacyRecorder.ListSmoothed.Count<=12,"About ten smoothed pictures.");
			Assert.AreEqual(LegacyRecorder.ListSmoothed.Count,LegacyRecorder.ListSmoothed.Select(x => x.PathRelative).Distinct().Count());
			foreach(SmoothedPicture smoothedPicture in LegacyRecorder.ListSmoothed) {
				Assert.IsTrue(ChartScenes.GetSizes(ChartScenes.Get(smoothedPicture.SceneName)).Contains(smoothedPicture.ChartSize),smoothedPicture.PathRelative);
			}
			CollectionAssert.IsSubsetOf(new[] { "410x307","500x370","649x696" },LegacyRecorder.ListSmoothed.Select(x => x.ChartSize.FolderName).Distinct().ToList());
			StaRunner.Run(() => {
				using SparksToothChart.ToothChartWrapper wrapper=new SparksToothChart.ToothChartWrapper();
				Assert.AreEqual("unknown (no DirectX control)",LegacyRecording.GetDirectXAdapter(wrapper));
			});
		}

		[TestMethod]
		public void SmoothedGalleryPictures_ExistAndAreNotBlank() {
			//The only test that reads Baselines\legacy-smoothed: the pictures are for the gallery and are never compared.
			string folder=TestPaths.BaselinesLegacySmoothed;
			foreach(SmoothedPicture smoothedPicture in LegacyRecorder.ListSmoothed) {
				string pathPng=Path.Combine(folder,smoothedPicture.ChartSize.FolderName,smoothedPicture.SceneName+".png");
				Assert.IsTrue(File.Exists(pathPng),"Missing: "+pathPng);
				Assert.IsTrue(File.Exists(Path.ChangeExtension(pathPng,".txt")),"Missing: "+Path.ChangeExtension(pathPng,".txt"));
				using Bitmap bitmap=new Bitmap(pathPng);
				Assert.AreEqual(smoothedPicture.ChartSize.Size,bitmap.Size,pathPng);
				Assert.IsFalse(ChartImageCompare.DescribeBitmap(bitmap).IsBlank,"Blank: "+pathPng);
			}
			Assert.AreEqual(LegacyRecorder.ListSmoothed.Count,Directory.GetFiles(folder,"*.png",SearchOption.AllDirectories).Length,
				"The folder holds the listed pictures only.");
		}

		[TestMethod]
		public void WpfRecorderGateAndGallery_ReportAForcedRenderFailure_AndNeverUseTheFallbackPicture() {
			//A render that fails makes GetBitmap return the Simple 2D chart's image while Engine stays Wpf3D. The recorder must not write that image
			//as the WPF baseline, the gate must not compare it (a 2D baseline would match it), and the gallery must not show it as the WPF chart's.
			ChartScene scene=ChartScenes.Get("treatment-plan");
			ChartSize chartSize=ChartScenes.SizeModule;
			string prefixFailure=chartSize.FolderName+"/"+scene.Name+": the WPF render failed and GetBitmap returned the Simple 2D fallback: ";
			string folder=NewTempFolder("tc1-forced-failure-");
			string folderRecorded=Path.Combine(folder,"recorded");
			string folderBaseline2D=Path.Combine(folder,"baseline-2d");
			string folderGallery=Path.Combine(folder,"gallery");
			string folderFailures=Path.Combine(folder,"failures");
			try {
				StaRunner.Run(() => {
					using ToothChartWrapper wrapper=WpfCharts.CreateWrapper(chartSize.Size);
					Assert.AreEqual(ToothChartEngine.Wpf3D,wrapper.Engine,string.Join("; ",ToothChartWrapper.GetFallbackMessages()));
					ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
					ChartScenes.Prepare(relay,scene,chartSize.ColorSet);
					List<string> listFailures=new List<string>();
					using(WpfCharts.HoldRenderThread(WpfCharts.BuildScene(wrapper),250)) {
						//The recorder: the failure is reported and nothing is written.
						Assert.AreEqual(WpfSceneOutcome.Skipped,WpfImageTests.CheckScene(wrapper,relay,scene,chartSize,true,folderRecorded,folderFailures,"test",
							listFailures));
						Assert.AreEqual(1,listFailures.Count,string.Join("\r\n",listFailures));
						StringAssert.StartsWith(listFailures[0],prefixFailure);
						StringAssert.Contains(listFailures[0],"TimeoutException");
						Assert.AreEqual(ToothChartEngine.Wpf3D,wrapper.Engine,"A failed render leaves the engine at Wpf3D: only the message tells.");
						Assert.IsFalse(Directory.Exists(folderRecorded),"Nothing was recorded.");
						//The gate, with the fallback's own image as the baseline: compared, it would pass.
						string pathBaseline2D=WpfCharts.GetPicturePath(folderBaseline2D,chartSize,scene);
						Directory.CreateDirectory(Path.GetDirectoryName(pathBaseline2D));
						using(Bitmap bitmap2D=relay.GetBitmap()) {
							PngWriter.Save(bitmap2D,pathBaseline2D);
						}
						listFailures.Clear();
						Assert.AreEqual(WpfSceneOutcome.Skipped,WpfImageTests.CheckScene(wrapper,relay,scene,chartSize,false,folderBaseline2D,folderFailures,"test",
							listFailures));
						Assert.AreEqual(1,listFailures.Count,string.Join("\r\n",listFailures));
						StringAssert.StartsWith(listFailures[0],prefixFailure);
						//The gallery: the cell is marked as failed, and no picture is saved.
						WpfGalleryPicture picture=WpfImageTests.DrawGalleryPicture(wrapper,relay,scene,chartSize,folderGallery,null);
						Assert.IsNotNull(picture.Failure,"The gallery cell is failed.");
						StringAssert.StartsWith(picture.Failure,prefixFailure);
						Assert.IsFalse(Directory.Exists(folderGallery),"No picture in the gallery.");
					}
					//The render thread answers again: the same scene is recorded without a failure, and it does not match the 2D picture.
					listFailures.Clear();
					Assert.AreEqual(WpfSceneOutcome.Recorded,WpfImageTests.CheckScene(wrapper,relay,scene,chartSize,true,folderRecorded,folderFailures,"test",
						listFailures),string.Join("\r\n",listFailures));
					Assert.AreEqual(0,listFailures.Count,string.Join("\r\n",listFailures));
					string pathRecorded=WpfCharts.GetPicturePath(folderRecorded,chartSize,scene);
					StringAssert.Contains(File.ReadAllText(Path.ChangeExtension(pathRecorded,".txt")),"Repeatable: yes");
					Assert.AreEqual(WpfSceneOutcome.Compared,WpfImageTests.CheckScene(wrapper,relay,scene,chartSize,false,folderBaseline2D,folderFailures,"test",
						listFailures));
					Assert.AreEqual(1,listFailures.Count,"The WPF picture differs from the 2D one: "+string.Join("\r\n",listFailures));
					Assert.IsNull(WpfImageTests.DrawGalleryPicture(wrapper,relay,scene,chartSize,folderGallery,null).Failure);
				});
			}
			finally {
				Directory.Delete(folder,true);
			}
		}

		[TestMethod]
		public void Gallery_EmptiesOnlyAGalleryFolder() {
			//The gallery folder is whatever -Out or OPMS_CHART_GALLERY names. Before this check WriteGallery deleted it with no question, so
			//"-Out ." in a checkout would have deleted the checkout.
			string folder=NewTempFolder("tc1-gallery-folder-");
			try {
				string folderCheckout=Path.Combine(folder,"checkout");
				Directory.CreateDirectory(Path.Combine(folderCheckout,".git"));
				File.WriteAllText(Path.Combine(folderCheckout,"README.md"),"Not a gallery.");
				File.WriteAllText(Path.Combine(folderCheckout,"index.html"),"<!doctype html><title>Another site</title>");
				InvalidOperationException ex=Expect.Throws<InvalidOperationException>(() => WpfGallery.PrepareFolder(folderCheckout));
				StringAssert.Contains(ex.Message,"Not a gallery folder");
				Assert.IsTrue(File.Exists(Path.Combine(folderCheckout,"README.md")) && Directory.Exists(Path.Combine(folderCheckout,".git"))
					&& File.Exists(Path.Combine(folderCheckout,"index.html")),"Nothing was deleted.");
				//A new folder, and an empty one, become galleries.
				string folderNew=Path.Combine(folder,"new");
				WpfGallery.PrepareFolder(folderNew);
				Assert.IsTrue(WpfGallery.IsGalleryFolder(folderNew));
				string folderEmpty=Path.Combine(folder,"empty");
				Directory.CreateDirectory(folderEmpty);
				WpfGallery.PrepareFolder(folderEmpty);
				Assert.IsTrue(WpfGallery.IsGalleryFolder(folderEmpty));
				//A gallery is emptied and stays one.
				File.WriteAllText(Path.Combine(folderNew,"index.html"),"old");
				Directory.CreateDirectory(Path.Combine(folderNew,"wpf","410x307"));
				WpfGallery.PrepareFolder(folderNew);
				Assert.IsFalse(File.Exists(Path.Combine(folderNew,"index.html")),"The old gallery was emptied.");
				Assert.IsFalse(Directory.Exists(Path.Combine(folderNew,"wpf")));
				Assert.IsTrue(WpfGallery.IsGalleryFolder(folderNew));
				//A gallery made before the marker file: its index.html says it is one.
				string folderOld=Path.Combine(folder,"gallery-before-the-marker");
				Directory.CreateDirectory(Path.Combine(folderOld,"wpf"));
				File.WriteAllText(Path.Combine(folderOld,"index.html"),"<!doctype html>\r\n<html lang=\"en\">\r\n<head>\r\n<meta charset=\"utf-8\">\r\n"
					+"<title>Tooth chart gallery G0</title>\r\n");
				WpfGallery.PrepareFolder(folderOld);
				Assert.IsFalse(Directory.Exists(Path.Combine(folderOld,"wpf")));
				Assert.IsTrue(WpfGallery.IsGalleryFolder(folderOld));
			}
			finally {
				Directory.Delete(folder,true);
			}
		}

		[TestMethod]
		public void Gallery_ComparesWithTheApprovedSet_AndMarksThePixelsOfTheBranchBaseline() {
			//From G1 on the approved pictures come from main, not from this branch's Baselines\wpf-net48: a picture this branch re-recorded must show
			//its difference from the approved one, and each picture says whether it is exactly the file the branch merges.
			ChartScene scene=ChartScenes.Get("restored-adult");
			ChartSize chartSize=ChartScenes.SizeModule;
			string folder=NewTempFolder("tc1-gallery-approved-");
			try {
				string folderApproved=Path.Combine(folder,"approved");
				string folderGallery=Path.Combine(folder,"gallery");
				//The approved picture: the branch's baseline with a block painted over, as an older approved picture would differ.
				string pathApproved=WpfCharts.GetPicturePath(folderApproved,chartSize,scene);
				Directory.CreateDirectory(Path.GetDirectoryName(pathApproved));
				using(Bitmap bitmapApproved=WpfImageTests.LoadBitmap(WpfCharts.GetPicturePath(WpfCharts.BaselinesWpf,chartSize,scene))) {
					using(Graphics g=Graphics.FromImage(bitmapApproved)) {
						g.FillRectangle(Brushes.Magenta,100,100,20,20);
					}
					PngWriter.Save(bitmapApproved,pathApproved);
				}
				List<WpfGalleryPicture> listPictures=new List<WpfGalleryPicture>();
				StaRunner.Run(() => {
					using ToothChartWrapper wrapper=WpfCharts.CreateWrapper(chartSize.Size);
					ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
					listPictures.Add(WpfImageTests.DrawGalleryPicture(wrapper,relay,scene,chartSize,folderGallery,folderApproved));
					listPictures.Add(WpfImageTests.DrawGalleryPicture(wrapper,relay,ChartScenes.Get("treatment-plan"),chartSize,folderGallery,folderApproved));
				});
				WpfGalleryPicture picture=listPictures[0];
				Assert.IsNull(picture.Failure,picture.Failure);
				Assert.IsTrue(picture.HasBaseline);
				Assert.IsFalse(picture.Comparison.IsPass,"Compared with the approved picture, not with this branch's baseline: "+picture.Comparison);
				Assert.AreEqual(400,picture.Comparison.CountDifferent);
				Assert.IsNotNull(picture.PathDiff);
				Assert.AreEqual(picture.PixelsSha256Merged,picture.PixelsSha256,"The fresh picture has exactly the pixels of the baseline this branch merges.");
				Assert.IsFalse(listPictures[1].HasBaseline,"The approved set has no treatment-plan.");
				Assert.AreEqual(listPictures[1].PixelsSha256Merged,listPictures[1].PixelsSha256);
				string html=WpfGallery.GetIndexHtml(folderGallery,"G1","A test gallery.",listPictures,null,null,"main at abc1234");
				StringAssert.Contains(html,"Approved WPF pictures: main at abc1234.");
				StringAssert.Contains(html,"2 of the 2 WPF pictures have exactly the pixels of this branch's baselines");
				StringAssert.Contains(html,"the pixels of this branch's baseline</div>");
				StringAssert.Contains(html,"src=\""+picture.PathDiff+"\"");
				//A picture with other pixels than the branch's baseline says so.
				listPictures[1].PixelsSha256Merged=new string('0',64);
				html=WpfGallery.GetIndexHtml(folderGallery,"G1","A test gallery.",listPictures,null,null,"main at abc1234");
				StringAssert.Contains(html,"1 of the 2 WPF pictures have exactly the pixels");
				StringAssert.Contains(html,"other pixels than this branch's baseline");
			}
			finally {
				Directory.Delete(folder,true);
			}
		}

		[TestMethod]
		public void Compare_PassesForTheSamePictures_AndHonoursTheModeAndSceneFilters() {
			string folderBaseline=NewTempFolder("tc0-cmp-base-");
			string folderNew=NewTempFolder("tc0-cmp-new-");
			try {
				WriteSyntheticRecording(folderBaseline);
				WriteSyntheticRecording(folderNew);
				LegacyComparison legacyComparison=LegacyComparison.Compare(folderBaseline,folderNew,LegacyRecorder.ArrayEngines.ToList(),null,false);
				Assert.IsTrue(legacyComparison.IsPass,legacyComparison.GetSummary());
				Assert.AreEqual(4,legacyComparison.ListMatching.Count);
				//The summary names the baseline folder it was given, not the repository's legacy recording.
				StringAssert.Contains(legacyComparison.GetSummary(),") with "+folderBaseline.TrimEnd('\\','/')+", ");
				Assert.IsFalse(legacyComparison.GetSummary().Contains("Baselines\\legacy"),legacyComparison.GetSummary());
				//A new recording of DirectX only, or of one scene only, passes when only those are compared.
				Directory.Delete(Path.Combine(folderNew,"Simple2D"),true);
				File.Delete(Path.Combine(folderNew,"DirectX","410x307","select-one.png"));
				File.Delete(Path.Combine(folderNew,"DirectX","410x307","select-one.txt"));
				Assert.IsFalse(LegacyComparison.Compare(folderBaseline,folderNew,LegacyRecorder.ArrayEngines.ToList(),null,false).IsPass);
				legacyComparison=LegacyComparison.Compare(folderBaseline,folderNew,new List<DrawingMode> { DrawingMode.DirectX },
					new List<string> { "empty-universal" },false);
				Assert.IsTrue(legacyComparison.IsPass,legacyComparison.GetSummary());
				CollectionAssert.AreEqual(new[] { "DirectX/410x307/empty-universal.png","DirectX/500x370/empty-universal.png" },legacyComparison.ListMatching);
				//Nothing to compare is no pass.
				Assert.IsFalse(LegacyComparison.Compare(folderBaseline,folderNew,new List<DrawingMode> { DrawingMode.OpenGL },null,false).IsPass);
			}
			finally {
				Directory.Delete(folderBaseline,true);
				Directory.Delete(folderNew,true);
			}
		}

		[TestMethod]
		public void Compare_FailsWhenABaselinePictureIsMissing_OrANewOneHasNoBaseline() {
			string folderBaseline=NewTempFolder("tc0-cmp-base-");
			string folderNew=NewTempFolder("tc0-cmp-new-");
			try {
				WriteSyntheticRecording(folderBaseline);
				WriteSyntheticRecording(folderNew);
				File.Delete(Path.Combine(folderNew,"DirectX","500x370","empty-universal.png"));
				LegacyComparison legacyComparison=LegacyComparison.Compare(folderBaseline,folderNew,LegacyRecorder.ArrayEngines.ToList(),null,false);
				Assert.IsFalse(legacyComparison.IsPass,"A baseline picture missing from the new recording fails the check.");
				CollectionAssert.AreEqual(new[] { "DirectX/500x370/empty-universal.png" },legacyComparison.ListMissing);
				Assert.AreEqual(3,legacyComparison.ListMatching.Count);
				StringAssert.Contains(legacyComparison.GetSummary(),"Missing: DirectX/500x370/empty-universal.png");
				//With the scene filter on another scene, the missing picture is not asked for.
				Assert.IsTrue(LegacyComparison.Compare(folderBaseline,folderNew,LegacyRecorder.ArrayEngines.ToList(),new List<string> { "select-one" },false).IsPass);
				//A picture the baseline does not have fails too.
				using(Bitmap bitmap=NewBitmap(410,307,Color.Navy)) {
					WritePicture(folderNew,"OpenGL/410x307/empty-universal",bitmap,null);
				}
				legacyComparison=LegacyComparison.Compare(folderBaseline,folderNew,LegacyRecorder.ArrayEngines.ToList(),new List<string> { "empty-universal" },false);
				Assert.IsFalse(legacyComparison.IsPass);
				CollectionAssert.AreEqual(new[] { "OpenGL/410x307/empty-universal.png" },legacyComparison.ListNoBaseline);
			}
			finally {
				Directory.Delete(folderBaseline,true);
				Directory.Delete(folderNew,true);
			}
		}

		[TestMethod]
		public void Compare_FailsWhenThePixelHashOfATextFileChanged_EvenWithTheSameBytes() {
			string folderBaseline=NewTempFolder("tc0-cmp-base-");
			string folderNew=NewTempFolder("tc0-cmp-new-");
			try {
				WriteSyntheticRecording(folderBaseline);
				WriteSyntheticRecording(folderNew);
				string pathText=Path.Combine(folderNew,"Simple2D","410x307","empty-universal.txt");
				string hash=LegacyComparison.ReadPixelsSha256(pathText);
				File.WriteAllText(pathText,File.ReadAllText(pathText).Replace(hash,new string('0',64)));
				Assert.IsTrue(ChartImageCompare.AreFilesIdentical(Path.Combine(folderBaseline,"Simple2D","410x307","empty-universal.png"),
					Path.ChangeExtension(pathText,".png")),"The PNG bytes are the same.");
				foreach(bool isPixelsOnly in new[] { false,true }) {
					LegacyComparison legacyComparison=LegacyComparison.Compare(folderBaseline,folderNew,LegacyRecorder.ArrayEngines.ToList(),null,isPixelsOnly);
					Assert.IsFalse(legacyComparison.IsPass,"pixels only: "+isPixelsOnly);
					Assert.AreEqual(1,legacyComparison.ListDifferent.Count);
					StringAssert.StartsWith(legacyComparison.ListDifferent[0],"Simple2D/410x307/empty-universal.png: the .txt files' pixel SHA-256 differ");
				}
				//A .txt with no hash line fails as well.
				File.WriteAllText(pathText,"Scene: empty-universal\r\n");
				StringAssert.Contains(LegacyComparison.Compare(folderBaseline,folderNew,LegacyRecorder.ArrayEngines.ToList(),null,false).ListDifferent[0],
					"no \"Pixels SHA-256\" in the new .txt");
			}
			finally {
				Directory.Delete(folderBaseline,true);
				Directory.Delete(folderNew,true);
			}
		}

		[TestMethod]
		public void Compare_PixelsOnly_IgnoresTheFileBytes_ButNotAChangedPixel() {
			string folderBaseline=NewTempFolder("tc0-cmp-base-");
			string folderNew=NewTempFolder("tc0-cmp-new-");
			try {
				WriteSyntheticRecording(folderBaseline);
				WriteSyntheticRecording(folderNew);
				//The same pixels written by another encoder (GDI+), as another framework's DeflateStream gives other bytes for the same pixels.
				string pathPng=Path.Combine(folderNew,"DirectX","410x307","empty-universal.png");
				using(Bitmap bitmap=new Bitmap(Path.Combine(folderBaseline,"DirectX","410x307","empty-universal.png"))) {
					using Bitmap bitmapCopy=new Bitmap(bitmap);
					bitmapCopy.Save(pathPng,ImageFormat.Png);
				}
				Assert.IsFalse(ChartImageCompare.AreFilesIdentical(pathPng,Path.Combine(folderBaseline,"DirectX","410x307","empty-universal.png")));
				LegacyComparison legacyComparison=LegacyComparison.Compare(folderBaseline,folderNew,LegacyRecorder.ArrayEngines.ToList(),null,false);
				Assert.IsFalse(legacyComparison.IsPass,"Byte for byte, other bytes fail.");
				StringAssert.Contains(legacyComparison.ListDifferent[0],"the bytes differ. 0 of 125870 pixels differ");
				legacyComparison=LegacyComparison.Compare(folderBaseline,folderNew,LegacyRecorder.ArrayEngines.ToList(),null,true);
				Assert.IsTrue(legacyComparison.IsPass,legacyComparison.GetSummary());
				StringAssert.Contains(legacyComparison.GetSummary(),"by their decoded pixels");
				//One changed pixel fails in pixels-only mode, even with the .txt hash left as it was.
				using(Bitmap bitmap=NewBitmap(410,307,Color.Gray)) {
					bitmap.SetPixel(7,9,Color.FromArgb(255,129,128,128));
					PngWriter.Save(bitmap,pathPng);
				}
				legacyComparison=LegacyComparison.Compare(folderBaseline,folderNew,LegacyRecorder.ArrayEngines.ToList(),null,true);
				Assert.IsFalse(legacyComparison.IsPass);
				StringAssert.Contains(legacyComparison.ListDifferent[0],"the pixels differ.");
			}
			finally {
				Directory.Delete(folderBaseline,true);
				Directory.Delete(folderNew,true);
			}
		}

		[TestMethod]
		public void Compare_ReadsTheModeFoldersOnly_NeverTheSmoothedPictures() {
			string folderBaseline=NewTempFolder("tc0-cmp-base-");
			string folderNew=NewTempFolder("tc0-cmp-new-");
			try {
				WriteSyntheticRecording(folderBaseline);
				WriteSyntheticRecording(folderNew);
				using(Bitmap bitmap=NewBitmap(410,307,Color.Olive)) {
					WritePicture(LegacyRecorder.GetSmoothedFolder(folderBaseline),"410x307/empty-universal",bitmap,null);
					WritePicture(Path.Combine(folderBaseline,"elsewhere"),"410x307/empty-universal",bitmap,null);
				}
				LegacyComparison legacyComparison=LegacyComparison.Compare(folderBaseline,folderNew,LegacyRecorder.ArrayEngines.ToList(),null,false);
				Assert.IsTrue(legacyComparison.IsPass,legacyComparison.GetSummary());
				Assert.IsTrue(legacyComparison.ListMatching.All(x => x.StartsWith("DirectX/") || x.StartsWith("OpenGL/") || x.StartsWith("Simple2D/")));
			}
			finally {
				Directory.Delete(folderBaseline,true);
				Directory.Delete(folderNew,true);
				string folderSmoothed=LegacyRecorder.GetSmoothedFolder(folderBaseline);
				if(Directory.Exists(folderSmoothed)) {
					Directory.Delete(folderSmoothed,true);
				}
			}
		}

		private static string NewTempFolder(string prefix) {
			string folder=Path.Combine(Path.GetTempPath(),prefix+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(folder);
			return folder;
		}

		///<summary>A small recording: DirectX empty-universal at 410x307 and 500x370 and select-one at 410x307, and Simple2D empty-universal at
		///410x307, each a PNG of PngWriter with a .txt that holds its pixel SHA-256. The same call gives the same files.</summary>
		private static void WriteSyntheticRecording(string folder) {
			using Bitmap bitmapSmall=NewBitmap(410,307,Color.Gray);
			using Bitmap bitmapLarge=NewBitmap(500,370,Color.White);
			using Bitmap bitmapSelected=NewBitmap(410,307,Color.Gray);
			bitmapSelected.SetPixel(20,30,Color.Red);
			WritePicture(folder,"DirectX/410x307/empty-universal",bitmapSmall,null);
			WritePicture(folder,"DirectX/500x370/empty-universal",bitmapLarge,null);
			WritePicture(folder,"DirectX/410x307/select-one",bitmapSelected,null);
			WritePicture(folder,"Simple2D/410x307/empty-universal",bitmapSmall,null);
		}

		///<summary>Writes folder\pathRelative.png with PngWriter and a .txt beside it with the scene, the pixel SHA-256 and an optional extra line.</summary>
		private static void WritePicture(string folder,string pathRelative,Bitmap bitmap,string lineExtra) {
			string pathPng=Path.Combine(folder,pathRelative.Replace('/','\\')+".png");
			Directory.CreateDirectory(Path.GetDirectoryName(pathPng));
			PngWriter.Save(bitmap,pathPng);
			List<string> listLines=new List<string> { "Scene: "+Path.GetFileNameWithoutExtension(pathPng),"Started mode: DirectX" };
			if(lineExtra!=null) {
				listLines.Add(lineExtra);
			}
			listLines.Add("Pixels SHA-256: "+ChartImageCompare.DescribeBitmap(bitmap).PixelsSha256);
			File.WriteAllLines(Path.ChangeExtension(pathPng,".txt"),listLines);
		}
	}
}
