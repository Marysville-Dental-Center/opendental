// Image tests of the WPF chart (package TC-1, docs/specs/wpf-tooth-chart-plan.md sections 7 and 8): every chart scene against the WPF baselines,
// the recorder of those baselines, the owner gallery and the render times for O-19.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;
using OpenPmsDental;
using SparksToothChart;
using SparksToothChart.Wpf3D;

namespace ToothChartImageTests {
	///<summary>Every chart scene at every one of its sizes, drawn by the WPF chart through the relay and the wrapper's GetBitmap, against
	///Baselines\wpf-net48\&lt;size&gt;\&lt;scene&gt;.png with the tolerances of section 7 (ChartImageCompare). A failing scene writes its expected,
	///actual and diff images to %TEMP%\ToothChartImageTests\wpf-failures. The perio scenes come with TC-7. A picture the WPF chart did not draw
	///(the engine did not start, or a render failed and GetBitmap returned the Simple 2D fallback) fails and is never recorded or compared.
	///
	///Record the baselines (after a deliberate change of the drawing, in a commit that says why): set OPMS_RECORD_WPF_CHART=1 for a run of this
	///assembly (optionally OPMS_RECORD_WPF_CHART_OUT=&lt;folder&gt;). Each PNG gets a .txt with its facts (the framework and bitness of the process,
	///the screen DPI) and pixel SHA-256, and two more renders of the same state, a second GetBitmap and a fresh render of both layers, must give
	///the same pixels. The gate counts the pictures whose pixels are exactly their .txt's.
	///
	///Make a gallery (tools\New-ToothChartGallery.ps1 does this and zips it): set OPMS_CHART_GALLERY=&lt;folder&gt; (new, empty, or a gallery: any other
	///folder is refused) for a run of this assembly. WriteGallery draws every scene again, marks each picture equal or not to this branch's baseline,
	///compares it with the approved picture (from G1 on, in OPMS_CHART_GALLERY_APPROVED, which the script takes from main), measures the render times
	///in a fresh second process, copies TC-0's legacy pictures, and writes index.html. OPMS_CHART_GALLERY_NAME names it (default G0).</summary>
	[TestClass]
	public class WpfImageTests {
		public const string EnvRecord="OPMS_RECORD_WPF_CHART";
		public const string EnvRecordOut="OPMS_RECORD_WPF_CHART_OUT";
		public const string EnvGallery="OPMS_CHART_GALLERY";
		public const string EnvGalleryName="OPMS_CHART_GALLERY_NAME";
		///<summary>From G1 on: the folder of the approved WPF baselines (the &lt;size&gt;\&lt;scene&gt;.png of Baselines\wpf-net48 as main has
		///them), and words for where they came from.</summary>
		public const string EnvGalleryApproved="OPMS_CHART_GALLERY_APPROVED";
		public const string EnvGalleryApprovedFrom="OPMS_CHART_GALLERY_APPROVED_FROM";

		[TestMethod]
		public void ChartScenes_MatchTheWpfBaselines() {
			bool isRecording=Environment.GetEnvironmentVariable(EnvRecord)=="1";
			string folderBaseline=WpfCharts.BaselinesWpf;
			string folderOut=Environment.GetEnvironmentVariable(EnvRecordOut);
			if(isRecording && !string.IsNullOrWhiteSpace(folderOut)) {
				folderBaseline=folderOut;
			}
			string folderFailures=Path.Combine(Path.GetTempPath(),"ToothChartImageTests","wpf-failures");
			if(!isRecording && Directory.Exists(folderFailures)) {
				Directory.Delete(folderFailures,true);
			}
			List<string> listFailures=new List<string>();
			List<string> listWritten=new List<string>();
			List<string> listIdentical=new List<string>();
			List<string> listCompared=new List<string>();
			string windows=GetWindowsBuild();
			StaRunner.Run(() => {
				foreach(ChartSize chartSize in WpfCharts.GetChartSizes()) {
					using ToothChartWrapper wrapper=WpfCharts.CreateWrapper(chartSize.Size);
					ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
					foreach(ChartScene scene in WpfCharts.GetChartScenes().Where(x => ChartScenes.GetSizes(x).Contains(chartSize))) {
						WpfSceneOutcome wpfSceneOutcome=CheckScene(wrapper,relay,scene,chartSize,isRecording,folderBaseline,folderFailures,windows,listFailures,
							listIdentical);
						if(wpfSceneOutcome==WpfSceneOutcome.Recorded) {
							listWritten.Add(chartSize.FolderName+"/"+scene.Name);
						}
						else if(wpfSceneOutcome==WpfSceneOutcome.Compared) {
							listCompared.Add(chartSize.FolderName+"/"+scene.Name);
						}
					}
				}
			});
			if(isRecording) {
				WriteManifest(folderBaseline);
				Console.WriteLine(listWritten.Count+" WPF baselines written to "+folderBaseline+".");
				Assert.AreEqual(0,listFailures.Count,string.Join("\r\n",listFailures));
				return;
			}
			int countCompared=listCompared.Count;
			//Section 7: identical bytes before any tolerance is trusted. Every run counts the pictures whose pixels are exactly their .txt's.
			List<string> listNotIdentical=listCompared.Except(listIdentical).ToList();
			WpfCharts.WriteEvidence("wpf-gate.txt",countCompared+" WPF pictures compared with "+folderBaseline+"; "+listIdentical.Count+" of them have exactly the pixels of their "
				+".txt's Pixels SHA-256"+(listNotIdentical.Count==0?".":", these not: "+string.Join(", ",listNotIdentical.Take(40))+(listNotIdentical.Count>40?", ...":"")+"."));
			Assert.AreEqual(0,listFailures.Count,listFailures.Count+" scenes differ from the WPF baselines:\r\n"+string.Join("\r\n",listFailures));
			Assert.IsTrue(countCompared>=250,"Compared "+countCompared);
		}

		///<summary>One scene of the gate (ChartScenes_MatchTheWpfBaselines) or the recorder, at the wrapper's size: drawn through the relay, then
		///recorded into folderBaseline (isRecording) or compared with its baseline there; what went wrong is added to listFailures. A picture the
		///WPF chart did not draw is a failure and is neither recorded nor compared: the engine did not start (Engine is not Wpf3D), or a render
		///failed and GetBitmap returned the Simple 2D chart's image (Engine stays Wpf3D; a fallback message tells). Otherwise the recorder would
		///write a 2D picture as the WPF baseline, and every later 2D picture would match it. listIdentical, when given, gets the name of a compared
		///scene whose pixels are exactly those of its baseline .txt's Pixels SHA-256.</summary>
		public static WpfSceneOutcome CheckScene(ToothChartWrapper wrapper,ToothChartRelay relay,ChartScene scene,ChartSize chartSize,bool isRecording,
			string folderBaseline,string folderFailures,string windows,List<string> listFailures,List<string> listIdentical=null)
		{
			string pathPng=WpfCharts.GetPicturePath(folderBaseline,chartSize,scene);
			string name=chartSize.FolderName+"/"+scene.Name;
			int countFallbacks=ToothChartWrapper.GetCountFallbackMessages();
			using Bitmap bitmap=WpfCharts.Draw(relay,scene,chartSize.ColorSet);
			if(wrapper.Engine!=ToothChartEngine.Wpf3D) {
				listFailures.Add(name+": the chart fell back to "+wrapper.Engine+".");
				return WpfSceneOutcome.Skipped;
			}
			string failureFallback=WpfCharts.GetFallbackFailure(name,countFallbacks);
			if(failureFallback!=null) {
				listFailures.Add(failureFallback);
				return WpfSceneOutcome.Skipped;
			}
			if(isRecording) {
				if(!WriteBaseline(wrapper,relay,bitmap,scene,chartSize,pathPng,windows,listFailures)) {
					return WpfSceneOutcome.Skipped;
				}
				return WpfSceneOutcome.Recorded;
			}
			if(!File.Exists(pathPng)) {
				listFailures.Add(name+": no baseline (record with "+EnvRecord+"=1).");
				return WpfSceneOutcome.Skipped;
			}
			string hashBaseline=LegacyComparison.ReadPixelsSha256(Path.ChangeExtension(pathPng,".txt"));
			if(listIdentical!=null && hashBaseline!=null && hashBaseline==ChartImageCompare.Sha256(ChartImageCompare.GetPixelsArgb(bitmap))) {
				listIdentical.Add(name);
			}
			using Bitmap bitmapBaseline=LoadBitmap(pathPng);
			ChartImageCompareResult result=ChartImageCompare.Compare(bitmapBaseline,bitmap);
			if(!result.IsPass) {
				string pathDiff=ChartImageCompare.WriteFailureImages(Path.Combine(folderFailures,chartSize.FolderName),scene.Name,bitmapBaseline,bitmap);
				listFailures.Add(name+": "+result+" Images: "+pathDiff);
			}
			return WpfSceneOutcome.Compared;
		}

		///<summary>The baseline PNG and its .txt. The state is drawn twice more first, because a baseline is only worth comparing with if the same
		///state gives the same pixels: by a second GetBitmap (which reuses the wrapper's 3D layer: only the 2D composite is drawn again), and by a
		///fresh render of both layers without the layer cache, so a recording on another platform (x64, .NET 10) checks the 3D rasterization of every
		///scene too. False, with nothing written, when the second GetBitmap fell back to the 2D chart.</summary>
		private static bool WriteBaseline(ToothChartWrapper wrapper,ToothChartRelay relay,Bitmap bitmap,ChartScene scene,ChartSize chartSize,string pathPng,
			string windows,List<string> listFailures)
		{
			string name=chartSize.FolderName+"/"+scene.Name;
			BitmapFacts bitmapFacts=ChartImageCompare.DescribeBitmap(bitmap);
			string pixelsSecond;
			int countFallbacks=ToothChartWrapper.GetCountFallbackMessages();
			using(Bitmap bitmapSecond=relay.GetBitmap()) {
				pixelsSecond=ChartImageCompare.DescribeBitmap(bitmapSecond).PixelsSha256;
			}
			string failureFallback=WpfCharts.GetFallbackFailure(name,countFallbacks);
			if(failureFallback!=null) {
				listFailures.Add(failureFallback);
				return false;
			}
			string pixelsFresh=null;
			try {
				using(Bitmap bitmapFresh=ToothChartRenderer.Instance.Render(ToothSceneBuilder.Build(wrapper.TcData),null).ToBitmap()) {
					pixelsFresh=ChartImageCompare.DescribeBitmap(bitmapFresh).PixelsSha256;
				}
			}
			catch(Exception ex) {
				listFailures.Add(name+": the fresh render of both layers failed ("+ex.GetType().Name+": "+ex.Message+").");
			}
			Directory.CreateDirectory(Path.GetDirectoryName(pathPng));
			if(pixelsSecond!=bitmapFacts.PixelsSha256) {
				listFailures.Add(name+": a second render of the same state gave other pixels.");
			}
			if(pixelsFresh!=null && pixelsFresh!=bitmapFacts.PixelsSha256) {
				listFailures.Add(name+": a fresh render of both layers (no layer cache) gave other pixels.");
			}
			if(bitmapFacts.IsBlank) {
				listFailures.Add(name+": blank.");
			}
			PngWriter.Save(bitmap,pathPng);
			List<string> listLines=new List<string>();
			listLines.Add("Scene: "+scene.Name);
			listLines.Add("Group: "+scene.Group);
			listLines.Add("Description: "+scene.Description);
			listLines.Add("Size: "+chartSize.FolderName);
			listLines.Add("Colours: "+chartSize.ColorSet);
			listLines.Add("Engine: "+ToothChartEngine.Wpf3D+" (RenderTargetBitmap in software, 3D layer at "+ToothChartRenderer.GetSupersamplingFactor(chartSize.Size)
				+"x reduced with WIC's Fant scaler)");
			listLines.Add("Bitmap: "+bitmapFacts.Size.Width+"x"+bitmapFacts.Size.Height+" "+bitmapFacts.PixelFormat);
			listLines.Add("Alpha: "+bitmapFacts.GetAlphaText());
			listLines.Add("Colours in the picture: "+bitmapFacts.CountColors);
			listLines.Add("Pixels SHA-256: "+bitmapFacts.PixelsSha256);
			listLines.Add("Repeatable: "+(pixelsSecond==bitmapFacts.PixelsSha256?"yes (a second GetBitmap gave the same pixels)":"no"));
			listLines.Add("Framework: "+GetFramework()+", "+GetBitness());
			listLines.Add("Windows: "+windows);
			listLines.Add("Fresh render: "+(pixelsFresh==bitmapFacts.PixelsSha256?"yes (both layers drawn again without the layer cache gave the same pixels)":"no"));
			listLines.Add("Screen DPI seen by the test host: "+WpfRendererTests.GetScreenDpi());
			File.WriteAllLines(Path.ChangeExtension(pathPng,".txt"),listLines,new UTF8Encoding(false));
			return true;
		}

		///<summary>The framework this test assembly runs on, from its TargetFrameworkAttribute (".NET Framework 4.8"; ".NET 10.0" after S3b), or the
		///runtime's own description.</summary>
		public static string GetFramework() {
			System.Runtime.Versioning.TargetFrameworkAttribute targetFrameworkAttribute=(System.Runtime.Versioning.TargetFrameworkAttribute)Attribute.GetCustomAttribute(
				typeof(WpfImageTests).Assembly,typeof(System.Runtime.Versioning.TargetFrameworkAttribute));
			if(targetFrameworkAttribute!=null && !string.IsNullOrWhiteSpace(targetFrameworkAttribute.FrameworkDisplayName)) {
				return targetFrameworkAttribute.FrameworkDisplayName;
			}
			return System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
		}

		///<summary>"x64" or "x86", from the process.</summary>
		public static string GetBitness() {
			return Environment.Is64BitProcess?"x64":"x86";
		}

		///<summary>manifest.txt: one tab-separated line per baseline, from the .txt files. Its header names the framework and bitness of the process
		///that wrote it.</summary>
		private static void WriteManifest(string folder) {
			string[] arrayColumns={ "Scene","Group","Size","Colours","Bitmap","Alpha","Colours in the picture","Pixels SHA-256","Repeatable" };
			List<string> listLines=new List<string>();
			listLines.Add("# The WPF tooth chart's baselines on "+GetFramework()+" "+GetBitness()+", recorded by ToothChartImageTests.WpfImageTests (TC-1 and "
				+"later; docs/specs/wpf-tooth-chart-plan.md, section 7).");
			listLines.Add("File\t"+string.Join("\t",arrayColumns));
			foreach(string pathText in Directory.GetFiles(folder,"*.txt",SearchOption.AllDirectories).OrderBy(x => x,StringComparer.Ordinal)) {
				if(Path.GetFileName(pathText)=="manifest.txt") {
					continue;
				}
				Dictionary<string,string> dict=LegacyRecording.ReadSidecar(pathText);
				string pathRelative=pathText.Substring(folder.TrimEnd('\\').Length+1).Replace('\\','/');
				pathRelative=pathRelative.Substring(0,pathRelative.Length-4)+".png";
				listLines.Add(pathRelative+"\t"+string.Join("\t",arrayColumns.Select(x => dict.TryGetValue(x,out string value)?value:"")));
			}
			File.WriteAllLines(Path.Combine(folder,"manifest.txt"),listLines,new UTF8Encoding(false));
		}

		[TestMethod]
		public void WriteGallery() {
			string folder=Environment.GetEnvironmentVariable(EnvGallery);
			if(string.IsNullOrWhiteSpace(folder)) {
				Assert.Inconclusive("Set "+EnvGallery+" to a folder to write the WPF chart's gallery (tools\\New-ToothChartGallery.ps1).");
			}
			string galleryName=Environment.GetEnvironmentVariable(EnvGalleryName);
			if(string.IsNullOrWhiteSpace(galleryName)) {
				galleryName="G0";
			}
			//Old against new: from G1 on, the approved pictures come from outside this branch (the script takes them from main), because a branch that
			//has re-recorded would show its changed scenes as the same as its own baselines.
			string folderApproved=null;
			string approvedFrom="none yet: G0's WPF pictures are the first WPF baselines";
			if(galleryName!="G0") {
				folderApproved=Environment.GetEnvironmentVariable(EnvGalleryApproved);
				if(string.IsNullOrWhiteSpace(folderApproved) || !Directory.Exists(folderApproved)) {
					Assert.Fail("Set "+EnvGalleryApproved+" to a folder of the approved WPF baselines (tools\\New-ToothChartGallery.ps1 takes them from main): "
						+"gallery "+galleryName+" shows old against new, and this branch's own Baselines\\wpf-net48 would show a re-recorded scene as unchanged.");
				}
				approvedFrom=Environment.GetEnvironmentVariable(EnvGalleryApprovedFrom);
				if(string.IsNullOrWhiteSpace(approvedFrom)) {
					approvedFrom=folderApproved;
				}
			}
			WpfGallery.PrepareFolder(folder);//refuses a folder that is not a gallery
			//The render times first, in a fresh process, so the cold values are cold.
			string pathTimes=Path.Combine(folder,"timings-second-process.txt");
			SecondProcessResult resultTimes=SecondProcess.Run(600000,"times",pathTimes);
			Assert.AreEqual(0,resultTimes.ExitCode,resultTimes.Output);
			WpfTimingReport timingReport=ParseTimings(File.ReadAllText(pathTimes));
			File.Delete(pathTimes);
			List<WpfGalleryPicture> listPictures=new List<WpfGalleryPicture>();
			StaRunner.Run(() => {
				foreach(ChartSize chartSize in WpfCharts.GetChartSizes()) {
					using ToothChartWrapper wrapper=WpfCharts.CreateWrapper(chartSize.Size);
					ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
					foreach(ChartScene scene in WpfCharts.GetChartScenes().Where(x => ChartScenes.GetSizes(x).Contains(chartSize))) {
						listPictures.Add(DrawGalleryPicture(wrapper,relay,scene,chartSize,folder,folderApproved));
					}
				}
			});
			string note=GetGalleryNote(galleryName);
			WpfGallery.Write(folder,galleryName,note,listPictures,timingReport,Environment.GetEnvironmentVariable("OPMS_CHART_GALLERY_COMMIT"),approvedFrom);
			List<string> listFailures=listPictures.Where(x => x.Failure!=null).Select(x => x.Failure).ToList();
			Console.WriteLine("Gallery "+galleryName+" written to "+folder+": "+(listPictures.Count-listFailures.Count)+" WPF pictures"
				+(listFailures.Count>0?", "+listFailures.Count+" failed":"")+".");
			Console.WriteLine(timingReport.GetReport());
			Assert.AreEqual(0,listFailures.Count,listFailures.Count+" cells of the gallery are marked as failed, because the WPF chart did not draw "
				+"them (the folder holds the gallery; tools\\New-ToothChartGallery.ps1 -NoTestRun zips it):\r\n"+string.Join("\r\n",listFailures));
		}

		///<summary>One cell of the gallery: the scene drawn through the relay and saved under folder\wpf, its pixel hash checked against this
		///branch's baseline .txt (Baselines\wpf-net48, the file being merged), and compared with the approved picture in folderApproved when that is
		///given and has one. A picture the WPF chart did not draw (as CheckScene) is not saved and not compared: the cell's Failure says why, and the
		///gallery marks the cell as failed instead of showing the 2D chart's image as the WPF chart's.</summary>
		public static WpfGalleryPicture DrawGalleryPicture(ToothChartWrapper wrapper,ToothChartRelay relay,ChartScene scene,ChartSize chartSize,string folder,
			string folderApproved)
		{
			string name=chartSize.FolderName+"/"+scene.Name;
			WpfGalleryPicture picture=new WpfGalleryPicture();
			picture.Scene=scene;
			picture.ChartSize=chartSize;
			picture.PathWpf="wpf/"+chartSize.FolderName+"/"+scene.Name+".png";
			int countFallbacks=ToothChartWrapper.GetCountFallbackMessages();
			using Bitmap bitmap=WpfCharts.Draw(relay,scene,chartSize.ColorSet);
			if(wrapper.Engine!=ToothChartEngine.Wpf3D) {
				picture.Failure=name+": the chart fell back to "+wrapper.Engine+".";
				return picture;
			}
			picture.Failure=WpfCharts.GetFallbackFailure(name,countFallbacks);
			if(picture.Failure!=null) {
				return picture;
			}
			string pathPng=Path.Combine(folder,"wpf",chartSize.FolderName,scene.Name+".png");
			Directory.CreateDirectory(Path.GetDirectoryName(pathPng));
			PngWriter.Save(bitmap,pathPng);
			picture.PixelsSha256=ChartImageCompare.Sha256(ChartImageCompare.GetPixelsArgb(bitmap));
			picture.PixelsSha256Merged=LegacyComparison.ReadPixelsSha256(Path.ChangeExtension(WpfCharts.GetPicturePath(WpfCharts.BaselinesWpf,chartSize,scene),".txt"));
			if(folderApproved==null) {
				return picture;
			}
			string pathBaseline=WpfCharts.GetPicturePath(folderApproved,chartSize,scene);
			picture.HasBaseline=File.Exists(pathBaseline);
			if(picture.HasBaseline) {
				using Bitmap bitmapBaseline=LoadBitmap(pathBaseline);
				picture.Comparison=ChartImageCompare.Compare(bitmapBaseline,bitmap);
				if(picture.Comparison.CountDifferent>0 && bitmapBaseline.Size==bitmap.Size) {
					picture.PathDiff="diff/"+chartSize.FolderName+"/"+scene.Name+".png";
					string pathDiff=Path.Combine(folder,"diff",chartSize.FolderName,scene.Name+".png");
					Directory.CreateDirectory(Path.GetDirectoryName(pathDiff));
					using Bitmap bitmapDiff=ChartImageCompare.MakeDiffImage(bitmapBaseline,bitmap);
					PngWriter.Save(bitmapDiff,pathDiff);
				}
			}
			return picture;
		}

		[TestMethod]
		public void RenderTimes_AreMeasured() {
			//In this process the cold values are not cold (other tests have drawn); the gallery measures in a fresh process. This test keeps the
			//measurement working and prints the warm values in the test log.
			WpfTimingReport timingReport=StaRunner.Run(() => WpfTimings.Measure(false,3,true));
			Console.WriteLine(timingReport.GetReport());
			string folder=Path.Combine(Path.GetTempPath(),"ToothChartImageTests");
			Directory.CreateDirectory(folder);
			File.WriteAllText(Path.Combine(folder,"render-times-in-process.txt"),timingReport.GetReport(),new UTF8Encoding(false));
			foreach(string key in new[] { "start","410x307.refill","410x307.selection","410x307.render.3d.rasterize","410x307.render.composite" }) {
				double value=timingReport.Get(key);
				Assert.IsFalse(double.IsNaN(value),key+" was not measured.");
				Assert.IsTrue(value>0 && value<10000,key+": "+value);
			}
		}

		private static string GetGalleryNote(string galleryName) {
			if(galleryName=="G0") {
				return "Gallery G0 (package TC-1): the first look at the WPF chart, next to today's DirectX, OpenGL and Simple 2D pictures, with the render "
					+"times that owner question O-19 waits for. TC-1 is the foundation: the 3D layer (lit tooth groups, implants), the 2D layer in DirectX's "
					+"order (X, root canal lines, build-up silhouette, sealant S, watches, drawings, centre line, numbers) and the renderer. The look is "
					+"matched to DirectX in TC-3 (gallery G1): specular and font tuning, parity probes. Free text, TextBelow, the extraction graphic, "
					+"orthodontic hardware, retained roots and space maintainers come in TC-5 and TC-6 (G2), perio in TC-7 (G3). These WPF pictures are "
					+"the first baselines, so there is no approved WPF picture to compare with yet.";
			}
			return "Gallery "+galleryName+" of the WPF chart.";
		}

		///<summary>Reads a timing report written by WpfTimingReport.GetReport.</summary>
		public static WpfTimingReport ParseTimings(string text) {
			WpfTimingReport report=new WpfTimingReport();
			foreach(string line in text.Split(new[] { "\r\n","\n" },StringSplitOptions.RemoveEmptyEntries)) {
				if(line.StartsWith("Machine: ")) {
					report.ListMachine.Add(line.Substring("Machine: ".Length));
					continue;
				}
				if(line.StartsWith("Note: ")) {
					report.ListNotes.Add(line.Substring("Note: ".Length));
					continue;
				}
				string[] arrayParts=line.Split('\t');
				if(arrayParts.Length!=3) {
					continue;
				}
				string[] arrayValue=arrayParts[1].Split(' ');
				if(arrayValue.Length==2 && double.TryParse(arrayValue[0],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,
					out double value))
				{
					report.Add(arrayParts[0],arrayParts[2],value,arrayValue[1]);
				}
			}
			return report;
		}

		///<summary>A bitmap read into memory, so the file is not held open.</summary>
		public static Bitmap LoadBitmap(string path) {
			using(MemoryStream memoryStream=new MemoryStream(File.ReadAllBytes(path)))
			using(Bitmap bitmapFile=new Bitmap(memoryStream)) {
				return new Bitmap(bitmapFile);
			}
		}

		private static string GetWindowsBuild() {
			try {
				using RegistryKey registryKeyBase=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64);
				using RegistryKey registryKey=registryKeyBase.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
				if(registryKey!=null) {
					return "build "+registryKey.GetValue("CurrentBuild")+"."+registryKey.GetValue("UBR")+" ("+registryKey.GetValue("DisplayVersion")+")";
				}
			}
			catch(Exception ex) {
				ex.ToString();
			}
			return Environment.OSVersion.VersionString;
		}
	}

	///<summary>What WpfImageTests.CheckScene did with a scene.</summary>
	public enum WpfSceneOutcome {
		///<summary>Neither recorded nor compared: the WPF chart did not draw it, or it has no baseline. The failures say which.</summary>
		Skipped,
		///<summary>Its baseline was written (the failures say when it is blank or a second render differs).</summary>
		Recorded,
		///<summary>Compared with its baseline (the failures say when it differs).</summary>
		Compared,
	}
}
