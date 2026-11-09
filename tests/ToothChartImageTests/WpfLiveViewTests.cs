// Tests of the WPF chart's live view (package TC-3, O-19 (b), docs/specs/wpf-tooth-chart-plan.md sections 5.4 and 7): made only by the first rebuild
// on screen of a wrapper shown on an STA thread; a hidden or MTA wrapper uses the renderer; the "Draw in software" switch; the view draws the
// renderer's scene; a selection keeps its 3D layer; its mouse; and fix round 3's: no view for a print wrapper disposed before the message loop runs,
// the fallbacks when the view cannot start or show a scene, cursors that cannot be made, a DPI change without a new scene, the bitmap cache at 150%,
// the background before the first scene, the switch's cleared image, and the 5.5 refresh cases of TC-1 on the live view.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;
using SparksToothChart;
using SparksToothChart.Wpf3D;

namespace ToothChartImageTests {
	///<summary>The tests that show a wrapper need an interactive desktop session, as TC-1's did; the live view draws in software there
	///(TestAssemblySetup), and no test waits for a frame on screen: the view's content is read back with its own software render.</summary>
	[TestClass]
	public class WpfLiveViewTests {
		public static void RequireDesktop() {
			if(!Environment.UserInteractive) {
				Assert.Inconclusive("Needs an interactive desktop session: the wrapper must be shown.");
			}
		}

		///<summary>The live view of a shown wrapper, once its first rebuild on screen has made it and shown the current data; fails when none comes.</summary>
		public static ToothChartWpfView WaitForLiveView(ToothChartWrapper wrapper) {
			ToothChartWpfView toothChartWpfView=WpfCharts.WaitForLiveView(wrapper);
			Assert.IsNotNull(toothChartWpfView,"The shown wrapper's rebuild made no live view: "+string.Join("; ",ToothChartWrapper.GetFallbackMessages().Skip(
				Math.Max(0,ToothChartWrapper.GetFallbackMessages().Count-3))));
			return toothChartWpfView;
		}

		///<summary>The window's root visual of a live view's content (the HwndSource's root, the host's adapter): VisualTreeHelper.SetRootDpi works on it.</summary>
		public static System.Windows.Media.Visual GetRootVisual(ToothChartWpfView toothChartWpfView) {
			System.Windows.PresentationSource presentationSource=System.Windows.PresentationSource.FromVisual(toothChartWpfView.Child);
			Assert.IsNotNull(presentationSource,"The view's content is in a window.");
			return presentationSource.RootVisual;
		}

		///<summary>The content's size in device pixels: its bounds in the window's DIPs times the window's DPI scale.</summary>
		public static SizeF GetContentDeviceSize(ToothChartWpfView toothChartWpfView) {
			System.Windows.FrameworkElement frameworkElement=(System.Windows.FrameworkElement)toothChartWpfView.Child;
			System.Windows.Media.Visual visualRoot=GetRootVisual(toothChartWpfView);
			((System.Windows.UIElement)visualRoot).UpdateLayout();
			System.Windows.Rect rect=frameworkElement.TransformToAncestor(visualRoot).TransformBounds(new System.Windows.Rect(0,0,frameworkElement.ActualWidth,
				frameworkElement.ActualHeight));
			double dpiScale=System.Windows.Media.VisualTreeHelper.GetDpi(visualRoot).DpiScaleX;
			return new SizeF((float)(rect.Width*dpiScale),(float)(rect.Height*dpiScale));
		}

		[TestMethod]
		public void Assembly_DrawsTheLiveViewInSoftware() {
			Assert.AreEqual(System.Windows.Interop.RenderMode.SoftwareOnly,System.Windows.Media.RenderOptions.ProcessRenderMode,
				"Section 7: the tests that host the live view draw it in software (AssemblyInitialize).");
		}

		[TestMethod]
		public void Program_DrawsEveryWpfWindowInSoftware_AsTheseTestsDo() {
			//O-34: OpenPmsDental.exe sets the process's render mode to SoftwareOnly as the first statement of Main (a line from upstream), and a
			//window's own HwndTarget.RenderMode cannot turn hardware back on, so in the program the live view is WPF's software rasterizer at 1x, as in
			//these tests. If an owner-approved change removes or narrows that line, the live view is drawn by the graphics card where it can: this
			//test, TestAssemblySetup, the live view's documentation, the plan's notes and the times of the gallery change with it.
			string main=SourceText.GetMethod(SourceText.ReadCode(@"OpenPmsDental\Main Modules\ProgramEntry.cs"),"static void Main(string[] args) {");
			string body=main.Substring(main.IndexOf('{')+1).TrimStart();
			StringAssert.StartsWith(body,"System.Windows.Media.RenderOptions.ProcessRenderMode=System.Windows.Interop.RenderMode.SoftwareOnly;",
				"The program's first statement draws every WPF window in software.");
			List<string> listOthers=new List<string>();
			foreach(string folder in new[] { "OpenPmsDental","WpfControlsOD","SparksToothChart","CodeBase","OpenPmsDentalBusiness" }) {
				foreach(string relative in SourceText.GetCsFiles(folder)) {
					if(relative.EndsWith("ProgramEntry.cs",StringComparison.OrdinalIgnoreCase) || !SourceText.Read(relative).Contains("RenderMode")) {
						continue;
					}
					string code=SourceText.ReadCode(relative);
					if(code.Contains("ProcessRenderMode") || code.Contains("RenderMode.")) {
						listOthers.Add(relative);
					}
				}
			}
			Assert.AreEqual(0,listOthers.Count,"The program has one render-mode setting, ProgramEntry's; another one changes what O-34 says: "
				+string.Join(", ",listOthers));
			Assert.AreEqual(System.Windows.Interop.RenderMode.SoftwareOnly,System.Windows.Media.RenderOptions.ProcessRenderMode,
				"These tests draw as the program does (TestAssemblySetup).");
		}

		[TestMethod]
		public void SoftwareFormat_IsTheStoredValueOfTheSwitch() {
			Assert.AreEqual("WpfSoftware",ToothChartWrapper.DirectXFormatWpfSoftware);
			Assert.IsTrue(ToothChartWrapper.IsWpfSoftwareFormat("WpfSoftware"));
			Assert.IsTrue(ToothChartWrapper.IsWpfSoftwareFormat(" WpfSoftware "));
			Assert.IsFalse(ToothChartWrapper.IsWpfSoftwareFormat(null));
			Assert.IsFalse(ToothChartWrapper.IsWpfSoftwareFormat(""));
			Assert.IsFalse(ToothChartWrapper.IsWpfSoftwareFormat("0;Hardware;64;D16;X8R8G8B8;None"),"A DirectX format means the live view.");
			//An older build reads it as a DirectX format: with no ';' it keeps its default format (O-19 (b)).
			ToothChartDirectX.DirectXDeviceFormat directXDeviceFormat=new ToothChartDirectX.DirectXDeviceFormat(ToothChartWrapper.DirectXFormatWpfSoftware);
			Assert.AreEqual(new ToothChartDirectX.DirectXDeviceFormat().ToString(),directXDeviceFormat.ToString());
		}

		[TestMethod]
		public void HiddenWrapper_HasNoLiveView_AndItsImagesComeFromTheRenderer() {
			StaRunner.Run(() => {
				using ToothChartWrapper wrapper=WpfCharts.CreateWrapper(ChartScenes.SizeModule.Size);
				ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
				using Bitmap bitmap=WpfCharts.Draw(relay,ChartScenes.Get("restored-adult"),ChartColorSet.Module);
				Assert.IsNull(wrapper.LiveViewWpf,"A wrapper that is not shown makes no live view.");
				Assert.AreSame(wrapper.ImageHostWpf,wrapper.Controls.Cast<Control>().Single());
				Assert.AreEqual("Arial",wrapper.TcData.Font.Name,"TcData.Font as DirectX sets it.");
				Assert.AreEqual(9f,wrapper.TcData.Font.Size);
				using Bitmap bitmapRenderer=ToothChartRenderer.Instance.Render(ToothSceneBuilder.Build(wrapper.TcData),null).ToBitmap();
				Assert.IsTrue(ChartImageCompare.GetPixelsArgb(bitmap).SequenceEqual(ChartImageCompare.GetPixelsArgb(bitmapRenderer)));
			});
		}

		[TestMethod]
		public void ShownStaWrapper_MakesTheLiveView_WhichShowsTheScene_AndGetBitmapStaysTheRenderers() {
			RequireDesktop();
			StaRunner.Run(() => {
				using Form form=WpfCharts.CreateOffscreenForm(ChartScenes.SizeModule.Size);
				ToothChartWrapper wrapper=WpfCharts.CreateWrapper(ChartScenes.SizeModule.Size);
				form.Controls.Add(wrapper);
				ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
				ChartScenes.Prepare(relay,ChartScenes.Get("restored-adult"),ChartColorSet.Module);
				Assert.IsNull(wrapper.LiveViewWpf,"Not before it is shown.");
				form.Show();
				Assert.IsNull(wrapper.LiveViewWpf,"Not before the message loop runs its rebuild (fix round 3).");
				ToothChartWpfView toothChartWpfView=WaitForLiveView(wrapper);
				Assert.IsFalse(wrapper.ImageHostWpf.Visible,"The image host is hidden under it.");
				ToothScene toothScene=WpfCharts.BuildScene(wrapper);
				Assert.AreEqual(toothScene.Items3D.Count,toothChartWpfView.CountModelsShown,"Every 3D item is a model of the view.");
				Assert.AreEqual(0,wrapper.ImageHostWpf.CountRendersStarted,"The hidden image host rendered nothing.");
				//The content is laid out at 1/DPI of its window, whatever the desktop's scale (100% on the build PC, 1.5 on a desktop at 150%), so the
				//scene's pixels are the window's device pixels.
				Assert.AreEqual(System.Windows.Media.VisualTreeHelper.GetDpi(GetRootVisual(toothChartWpfView)).DpiScaleX,toothChartWpfView.DpiScaleShown,1e-9,
					"The DPI scale of the view's window.");
				SizeF sizeDevice=GetContentDeviceSize(toothChartWpfView);
				Assert.AreEqual(toothScene.Size.Width,sizeDevice.Width,0.01,"The content's width in device pixels is the scene's.");
				Assert.AreEqual(toothScene.Size.Height,sizeDevice.Height,0.01,"The content's height in device pixels is the scene's.");
				int countBefore=ToothChartWrapper.GetCountFallbackMessages();
				using(Bitmap bitmap=relay.GetBitmap())
				using(Bitmap bitmapRenderer=ToothChartRenderer.Instance.Render(toothScene,null).ToBitmap()) {
					Assert.IsTrue(ChartImageCompare.GetPixelsArgb(bitmap).SequenceEqual(ChartImageCompare.GetPixelsArgb(bitmapRenderer)),
						"GetBitmap of a wrapper with a live view is the renderer's image.");
				}
				Assert.AreEqual(0,ToothChartWrapper.GetFallbackMessagesSince(countBefore).Count,"Nothing was logged.");
				System.Windows.UIElement content=toothChartWpfView.Child;
				wrapper.Dispose();
				Assert.IsTrue(toothChartWpfView.IsDisposed,"The wrapper's Dispose releases the live view.");
				Assert.IsNull(toothChartWpfView.Child,"Its content was detached before its Dispose (ElementHost.Dispose leaves it attached).");
				Assert.IsNull(System.Windows.Media.VisualTreeHelper.GetParent(content),"The content left the host's adapter.");
				form.Close();
			});
		}

		[TestMethod]
		public void ShownMtaWrapper_HasNoLiveView_AndShowsTheRenderersImage() {
			RequireDesktop();
			StaRunner.RunMta(() => {
				using Form form=WpfCharts.CreateOffscreenForm(ChartScenes.SizeModule.Size);
				ToothChartWrapper wrapper=WpfCharts.CreateWrapper(ChartScenes.SizeModule.Size);
				form.Controls.Add(wrapper);
				ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
				ChartScenes.Prepare(relay,ChartScenes.Get("restored-adult"),ChartColorSet.Module);
				form.Show();
				ToothChartImageHost toothChartImageHost=wrapper.ImageHostWpf;
				Assert.IsTrue(WpfRendererTests.PumpUntil(() => toothChartImageHost.CountImagesReceived>=1 && !toothChartImageHost.IsRenderInFlight,20000),
					"The image host shows the renderer's image.");
				Assert.IsNull(wrapper.LiveViewWpf,"An MTA thread cannot host WPF: its rebuild made no live view.");
				Assert.IsTrue(toothChartImageHost.Visible);
				using(Bitmap bitmapShown=toothChartImageHost.GetBitmapShown())
				using(Bitmap bitmap=relay.GetBitmap()) {
					Assert.IsTrue(ChartImageCompare.GetPixelsArgb(bitmap).SequenceEqual(ChartImageCompare.GetPixelsArgb(bitmapShown)));
				}
				wrapper.Dispose();
				form.Close();
			});
		}

		[TestMethod]
		public void SoftwareSwitch_ShowsTheRenderersImage_AndSwitchesAShownWrapperAtOnce() {
			RequireDesktop();
			StaRunner.Run(() => {
				using Form form=WpfCharts.CreateOffscreenForm(ChartScenes.SizeModule.Size);
				ToothChartWrapper wrapper=WpfCharts.CreateWrapper(ChartScenes.SizeModule.Size);
				wrapper.WpfDrawInSoftware=true;
				form.Controls.Add(wrapper);
				ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
				ChartScenes.Prepare(relay,ChartScenes.Get("restored-adult"),ChartColorSet.Module);
				form.Show();
				ToothChartImageHost toothChartImageHost=wrapper.ImageHostWpf;
				Assert.IsTrue(WpfRendererTests.PumpUntil(() => toothChartImageHost.CountImagesReceived>=1 && !toothChartImageHost.IsRenderInFlight,20000));
				Assert.IsNull(wrapper.LiveViewWpf,"Drawn in software: its rebuild made no live view.");
				//Off: the posted rebuild makes the live view, over the image host.
				wrapper.WpfDrawInSoftware=false;
				ToothChartWpfView toothChartWpfView=WaitForLiveView(wrapper);
				Assert.IsFalse(toothChartImageHost.Visible);
				Assert.IsNotNull(toothChartImageHost.GetBitmapShown(),"Hidden under the live view, the image host still holds its image from before it.");
				//On again, after a change made while the live view was on: the image host shows the current chart.
				relay.SetSelected("8",true);
				Assert.IsTrue(WpfRendererTests.PumpUntil(() => toothChartWpfView.SceneShown.Items2D.Any(x => x.Source=="highlight of 8"),20000));
				int countImages=toothChartImageHost.CountImagesReceived;
				wrapper.WpfDrawInSoftware=true;
				Assert.IsNull(wrapper.LiveViewWpf);
				Assert.IsTrue(toothChartWpfView.IsDisposed,"The live view is released.");
				Assert.IsNull(toothChartWpfView.Child,"Its content was detached before its Dispose.");
				Assert.IsTrue(toothChartImageHost.Visible);
				//Until the software render arrives (about 0.3 s), the image host shows the chart's background, not its image from before the live view,
				//which lacks the selection (and in the Chart module could be another patient's chart).
				Assert.IsNull(toothChartImageHost.GetBitmapShown(),"The image host's old image was dropped when it came back on screen.");
				Assert.IsNull(toothChartImageHost.SceneShown);
				Assert.IsTrue(WpfRendererTests.PumpUntil(() => toothChartImageHost.CountImagesReceived>countImages && !toothChartImageHost.IsRenderInFlight,20000),
					"The image host renders the chart again.");
				Assert.IsTrue(toothChartImageHost.SceneShown.Items2D.Any(x => x.Source=="highlight of 8"),"With the selection made meanwhile.");
				using(Bitmap bitmap=relay.GetBitmap())
				using(Bitmap bitmapShown=toothChartImageHost.GetBitmapShown()) {
					Assert.IsTrue(ChartImageCompare.GetPixelsArgb(bitmap).SequenceEqual(ChartImageCompare.GetPixelsArgb(bitmapShown)),"The current chart.");
				}
				wrapper.Dispose();
				form.Close();
			});
		}

		[TestMethod]
		public void LiveView_DrawsWhatTheRendererDraws_ByTheParityProbes() {
			RequireDesktop();
			List<ParityProbeOutcome> listOutcomes=new List<ParityProbeOutcome>();
			StaRunner.Run(() => {
				foreach(ChartSize chartSize in new[] { ChartScenes.SizeModule,ChartScenes.SizePrintout }) {
					using Form form=WpfCharts.CreateOffscreenForm(chartSize.Size);
					ToothChartWrapper wrapper=WpfCharts.CreateWrapper(chartSize.Size);
					form.Controls.Add(wrapper);
					ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
					form.Show();
					ToothChartWpfView toothChartWpfView=WaitForLiveView(wrapper);
					foreach(string sceneName in new[] { "restored-adult","treatment-plan","select-several","drawing-segments","text-watch","primary-dentition",
						"movements-combined","sealant-statuses" })
					{
						ChartScene scene=ChartScenes.Get(sceneName);
						ChartScenes.Prepare(relay,scene,chartSize.ColorSet);
						ToothScene toothScene=WpfCharts.BuildScene(wrapper);
						Assert.IsTrue(WpfRendererTests.PumpUntil(() => toothChartWpfView.SceneShown!=null && !wrapper.IsWpfSceneDirty,20000),sceneName);
						using Bitmap bitmapView=toothChartWpfView.GetBitmapForTests();
						using Bitmap bitmapRenderer=relay.GetBitmap();
						Assert.AreEqual(bitmapRenderer.Size,bitmapView.Size);
						List<ParityProbePoint> listPoints=ParityProbes.GetPoints(wrapper.TcData);
						listOutcomes.AddRange(ParityProbes.Probe(scene,chartSize,listPoints,ChartImageCompare.GetPixelsArgb(bitmapRenderer),
							ChartImageCompare.GetPixelsArgb(bitmapView),wrapper.TcData.ColorBackground));
					}
					wrapper.Dispose();
					form.Close();
				}
			});
			List<ParityProbeOutcome> listFailed=listOutcomes.Where(x => x.Failure!=null).ToList();
			WpfCharts.WriteEvidence("liveview-probes.txt","The live view against the renderer's image, by the parity probes: "+listOutcomes.Count+" probes, "
				+listFailed.Count+" fail.\r\n"+string.Join("\r\n",listFailed.Take(60)));
			Assert.IsTrue(listOutcomes.Count>1000,"Probes: "+listOutcomes.Count);
			Assert.AreEqual(0,listFailed.Count,"The live view differs from the renderer's image:\r\n"+string.Join("\r\n",listFailed.Take(40)));
		}

		[TestMethod]
		public void LiveView_ASelectionKeepsThe3DLayer_AndARefillBuildsItOnce() {
			RequireDesktop();
			StaRunner.Run(() => {
				using Form form=WpfCharts.CreateOffscreenForm(ChartScenes.SizeModule.Size);
				ToothChartWrapper wrapper=WpfCharts.CreateWrapper(ChartScenes.SizeModule.Size);
				form.Controls.Add(wrapper);
				ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
				ChartScenes.Prepare(relay,ChartScenes.Get("restored-adult"),ChartColorSet.Module);
				form.Show();
				ToothChartWpfView toothChartWpfView=WaitForLiveView(wrapper);
				int countBuilt=toothChartWpfView.Count3DLayersBuilt;
				int countShown=toothChartWpfView.CountScenesShown;
				relay.SetSelected("8",true);
				relay.SetSelected("9",true);
				Assert.IsTrue(WpfRendererTests.PumpUntil(() => toothChartWpfView.CountScenesShown>countShown,20000));
				WpfRendererTests.PumpUntil(() => false,100);
				Assert.AreEqual(countShown+1,toothChartWpfView.CountScenesShown,"Two Set calls post one rebuild.");
				Assert.AreEqual(countBuilt,toothChartWpfView.Count3DLayersBuilt,"A selection keeps the 3D layer: only the 2D layer is drawn again.");
				ChartScenes.Prepare(relay,ChartScenes.Get("treatment-plan"),ChartColorSet.Module);
				Assert.IsTrue(WpfRendererTests.PumpUntil(() => toothChartWpfView.CountScenesShown>countShown+1,20000));
				WpfRendererTests.PumpUntil(() => false,100);
				Assert.AreEqual(countBuilt+1,toothChartWpfView.Count3DLayersBuilt,"A refill of hundreds of Set calls builds the 3D layer once.");
				Assert.AreEqual(WpfCharts.BuildScene(wrapper).Items3D.Count,toothChartWpfView.CountModelsShown);
				wrapper.Dispose();
				form.Close();
			});
		}

		[TestMethod]
		public void LiveView_Caches3DLayer_AndAPenMoveDrawsOnlyTheStroke() {
			RequireDesktop();
			StaRunner.Run(() => {
				using Form form=WpfCharts.CreateOffscreenForm(ChartScenes.SizeModule.Size);
				ToothChartWrapper wrapper=WpfCharts.CreateWrapper(ChartScenes.SizeModule.Size);
				form.Controls.Add(wrapper);
				ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
				ChartScenes.Prepare(relay,ChartScenes.Get("restored-adult"),ChartColorSet.Module);
				form.Show();
				ToothChartWpfView toothChartWpfView=WaitForLiveView(wrapper);
				//The 3D layer (the Viewport3D over the background, the size of the chart) is kept in a bitmap cache at 1x, snapped to the pixels, so a
				//frame that changes only the 2D layer composites it.
				Assert.IsTrue(toothChartWpfView.Is3DLayerCached,"The default.");
				System.Windows.Controls.Grid grid=(System.Windows.Controls.Grid)toothChartWpfView.Child;
				System.Windows.Controls.Border border3D=grid.Children.OfType<System.Windows.Controls.Border>().Single();
				Assert.IsInstanceOfType(border3D.Child,typeof(System.Windows.Controls.Viewport3D));
				Assert.AreEqual(ChartScenes.SizeModule.Size.Width,border3D.ActualWidth,"The cached element is the size of the chart.");
				Assert.AreEqual(ChartScenes.SizeModule.Size.Height,border3D.ActualHeight);
				System.Windows.Media.BitmapCache bitmapCache=border3D.CacheMode as System.Windows.Media.BitmapCache;
				Assert.IsNotNull(bitmapCache,"The 3D layer is kept in a bitmap cache.");
				Assert.AreEqual(1.0,bitmapCache.RenderAtScale,"At 1x: the cached image has the 3D layer's own pixels.");
				Assert.IsTrue(bitmapCache.SnapsToDevicePixels);
				using(Bitmap bitmapCached=toothChartWpfView.GetBitmapForTests()) {
					toothChartWpfView.Is3DLayerCached=false;
					Assert.IsNull(border3D.CacheMode);
					double millisecondsFrame=toothChartWpfView.MeasureSoftwareFrameForTests();
					Assert.IsTrue(millisecondsFrame>0 && millisecondsFrame<10000,"One full software frame: "+millisecondsFrame);
					using(Bitmap bitmapUncached=toothChartWpfView.GetBitmapForTests()) {
						if(!ChartImageCompare.GetPixelsArgb(bitmapCached).SequenceEqual(ChartImageCompare.GetPixelsArgb(bitmapUncached))) {
							string folder=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"ToothChartImageTests","liveview-cache");
							System.IO.Directory.CreateDirectory(folder);
							PngWriter.Save(bitmapCached,System.IO.Path.Combine(folder,"cached.png"));
							PngWriter.Save(bitmapUncached,System.IO.Path.Combine(folder,"uncached.png"));
							Assert.Fail("The cache changes pixels of the view ("+ChartImageCompare.Compare(bitmapUncached,bitmapCached)+"); both pictures are in "
								+folder+".");
						}
					}
					toothChartWpfView.Is3DLayerCached=true;
					Assert.IsInstanceOfType(border3D.CacheMode,typeof(System.Windows.Media.BitmapCache));
				}
				//A pen move draws the stroke's own element, not the 2D layer, and builds no 3D layer.
				grid.UpdateLayout();
				int count2D=toothChartWpfView.CountLayer2DRenders;
				int countPen=toothChartWpfView.CountPenLayerRenders;
				int count3D=toothChartWpfView.Count3DLayersBuilt;
				relay.CursorTool=CursorTool.Pen;
				List<PointF> listPoints=new List<PointF> { new PointF(30,280),new PointF(45,284),new PointF(63,290) };
				toothChartWpfView.MouseDownForTests(Point.Round(listPoints[0]));
				toothChartWpfView.MouseMoveForTests(Point.Round(listPoints[1]));
				grid.UpdateLayout();
				toothChartWpfView.MouseMoveForTests(Point.Round(listPoints[2]));
				grid.UpdateLayout();
				Assert.AreEqual(count2D,toothChartWpfView.CountLayer2DRenders,"A pen move does not draw the 2D layer again.");
				Assert.IsTrue(toothChartWpfView.CountPenLayerRenders>=countPen+2,"Each pen move draws the stroke: "+(toothChartWpfView.CountPenLayerRenders-countPen));
				Assert.AreEqual(count3D,toothChartWpfView.Count3DLayersBuilt);
				using(Bitmap bitmapStroke=toothChartWpfView.GetBitmapForTests()) {
					Color color=bitmapStroke.GetPixel(45,284);
					Assert.IsTrue(color.R<60 && color.G<60 && color.B<60,"The stroke in progress shows over the 2D layer: "+color);
				}
				toothChartWpfView.MouseUpForTests(Point.Round(listPoints[2]));
				relay.CursorTool=CursorTool.Pointer;
				Assert.IsTrue(WpfRendererTests.PumpUntil(() => !wrapper.IsWpfSceneDirty,20000));
				//A selection draws the 2D layer again and keeps the 3D layer.
				grid.UpdateLayout();
				count2D=toothChartWpfView.CountLayer2DRenders;
				count3D=toothChartWpfView.Count3DLayersBuilt;
				relay.SetSelected("8",true);
				Assert.IsTrue(WpfRendererTests.PumpUntil(() => toothChartWpfView.SceneShown.Items2D.Any(x => x.Source=="highlight of 8"),20000));
				grid.UpdateLayout();
				Assert.IsTrue(toothChartWpfView.CountLayer2DRenders>count2D,"The selection draws the 2D layer.");
				Assert.AreEqual(count3D,toothChartWpfView.Count3DLayersBuilt,"It keeps the 3D layer.");
				wrapper.Dispose();
				form.Close();
			});
		}

		[TestMethod]
		public void LiveView_Mouse_SelectsDrawsAndRaisesTheWrappersEvents() {
			RequireDesktop();
			StaRunner.Run(() => {
				using Form form=WpfCharts.CreateOffscreenForm(ChartScenes.SizeModule.Size);
				ToothChartWrapper wrapper=WpfCharts.CreateWrapper(ChartScenes.SizeModule.Size);
				form.Controls.Add(wrapper);
				ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
				ChartScenes.Prepare(relay,ChartScenes.Get("empty-universal"),ChartColorSet.Module);
				form.Show();
				ToothChartWpfView toothChartWpfView=WaitForLiveView(wrapper);
				int countSelections=0;
				List<string> listSegments=new List<string>();
				wrapper.ToothSelectionsChanged+=sender => countSelections++;
				wrapper.SegmentDrawn+=(sender,e) => listSegments.Add(e.DrawingSegement);
				int countBuilt=toothChartWpfView.Count3DLayersBuilt;
				Point point8=ToothChartMouseTests.GetNumberCentre(wrapper.TcData,"8");
				toothChartWpfView.MouseMoveForTests(point8);
				toothChartWpfView.MouseDownForTests(point8);
				toothChartWpfView.MouseUpForTests(point8);
				CollectionAssert.AreEqual(new[] { "8" },wrapper.SelectedTeeth);
				Assert.AreEqual(1,countSelections);
				Assert.IsTrue(WpfRendererTests.PumpUntil(() => toothChartWpfView.SceneShown.Items2D.Any(x => x.Source=="highlight of 8"),20000),
					"The view shows the selection.");
				Assert.AreEqual(countBuilt,toothChartWpfView.Count3DLayersBuilt,"Only the 2D layer was drawn again.");
				Assert.IsNull(toothChartWpfView.CursorWpf,"The pointer's arrow.");
				relay.CursorTool=CursorTool.Pen;
				Assert.IsNotNull(toothChartWpfView.CursorWpf,"The pen's cursor from Pen.cur.");
				List<PointF> listPoints=new List<PointF> { new PointF(30,280),new PointF(45,284),new PointF(63,290) };
				toothChartWpfView.MouseDownForTests(Point.Round(listPoints[0]));
				toothChartWpfView.MouseMoveForTests(Point.Round(listPoints[1]));
				toothChartWpfView.MouseMoveForTests(Point.Round(listPoints[2]));
				Assert.AreEqual(3,wrapper.TcData.PointList.Count,"The stroke in progress, drawn by the view's 2D layer.");
				using(Bitmap bitmapStroke=toothChartWpfView.GetBitmapForTests()) {
					//The stroke's middle, in the pen colour (black) over the background.
					Color color=bitmapStroke.GetPixel(45,284);
					Assert.IsTrue(color.R<60 && color.G<60 && color.B<60,"The stroke in progress shows: "+color);
				}
				toothChartWpfView.MouseUpForTests(Point.Round(listPoints[2]));
				CollectionAssert.AreEqual(new[] { ToothChartMouse.GetDrawingSegment(wrapper.TcData,listPoints,ChartScenes.SizeModule.Size) },listSegments);
				Assert.AreEqual(0,wrapper.TcData.PointList.Count);
				wrapper.Dispose();
				form.Close();
			});
		}

		[TestMethod]
		public void EngineChange_ReleasesTheLiveView_AndWpf3DMakesItAgainWhenShown() {
			RequireDesktop();
			StaRunner.Run(() => {
				using Form form=WpfCharts.CreateOffscreenForm(ChartScenes.SizeModule.Size);
				ToothChartWrapper wrapper=WpfCharts.CreateWrapper(ChartScenes.SizeModule.Size);
				form.Controls.Add(wrapper);
				form.Show();
				ToothChartWpfView toothChartWpfView=WaitForLiveView(wrapper);
				wrapper.Engine=ToothChartEngine.Simple2D;
				Assert.IsNull(wrapper.LiveViewWpf);
				Assert.IsTrue(toothChartWpfView.IsDisposed,"Leaving Wpf3D releases the live view.");
				wrapper.Engine=ToothChartEngine.Wpf3D;
				Assert.IsNotNull(WpfCharts.WaitForLiveView(wrapper),"A shown wrapper set to Wpf3D gets its live view again, from its posted rebuild.");
				wrapper.Dispose();
				form.Close();
			});
		}

		#region Fix round 3
		[TestMethod]
		public void PrintWrapper_ShownAndDisposedBeforeTheMessageLoopRuns_MakesNoLiveView() {
			//ControlTreat.PrepImageForPrinting adds its print wrapper to the shown Treatment Plan module, takes GetBitmap and disposes it, with no message
			//loop in between. The live view is made by the wrapper's first rebuild on screen, which the message loop runs, so the print makes none.
			RequireDesktop();
			StaRunner.Run(() => {
				int countMade=0;
				ToothChartWpfView.ActionStepForTests=(toothChartWpfView,step) => {
					if(step==ToothChartWpfView.StepCreate) {
						countMade++;
					}
				};
				try {
					using Form form=WpfCharts.CreateOffscreenForm(ChartScenes.SizePrintout.Size);
					form.Show();
					Application.DoEvents();
					ToothChartWrapper wrapper=WpfCharts.CreateWrapper(ChartScenes.SizePrintout.Size);
					form.Controls.Add(wrapper);
					wrapper.BringToFront();
					Assert.IsTrue(wrapper.IsHandleCreated && wrapper.Visible,"The print wrapper is on a shown control, as on the Treatment Plan module.");
					ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
					using(Bitmap bitmap=WpfCharts.Draw(relay,ChartScenes.Get("treatment-plan"),ChartColorSet.Printout)) {
						Assert.AreEqual(ChartScenes.SizePrintout.Size,bitmap.Size);
					}
					Assert.IsNull(wrapper.LiveViewWpf,"No live view before the message loop runs.");
					form.Controls.Remove(wrapper);
					wrapper.Dispose();
					WpfRendererTests.PumpUntil(() => false,300);//the message loop runs; the posted rebuild went with the wrapper
					Assert.AreEqual(0,countMade,"The print made no live view.");
					//A wrapper that stays shown makes its live view when the loop runs its rebuild.
					ToothChartWrapper wrapperShown=WpfCharts.CreateWrapper(ChartScenes.SizePrintout.Size);
					form.Controls.Add(wrapperShown);
					WaitForLiveView(wrapperShown);
					Assert.AreEqual(1,countMade);
					wrapperShown.Dispose();
					form.Close();
				}
				finally {
					ToothChartWpfView.ActionStepForTests=null;
				}
			});
		}

		[TestMethod]
		public void LiveView_ThatCannotStart_LeavesTheImageHostOnScreen_AndIsNotTriedAgain() {
			RequireDesktop();
			StaRunner.Run(() => {
				int countMade=0;
				ToothChartWpfView.ActionStepForTests=(toothChartWpfView,step) => {
					if(step==ToothChartWpfView.StepCreate) {
						countMade++;
						throw new InvalidOperationException("A test: the live view cannot start.");
					}
				};
				try {
					using Form form=WpfCharts.CreateOffscreenForm(ChartScenes.SizeModule.Size);
					ToothChartWrapper wrapper=WpfCharts.CreateWrapper(ChartScenes.SizeModule.Size);
					form.Controls.Add(wrapper);
					ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
					ChartScenes.Prepare(relay,ChartScenes.Get("restored-adult"),ChartColorSet.Module);
					int countBefore=ToothChartWrapper.GetCountFallbackMessages();
					form.Show();
					ToothChartImageHost toothChartImageHost=wrapper.ImageHostWpf;
					Assert.IsTrue(WpfRendererTests.PumpUntil(() => toothChartImageHost.CountImagesReceived>=1 && !toothChartImageHost.IsRenderInFlight,20000),
						"The image host shows the software render instead.");
					Assert.AreEqual(1,countMade,"The rebuild tried to make the live view.");
					Assert.IsNull(wrapper.LiveViewWpf);
					Assert.IsTrue(toothChartImageHost.Visible);
					Assert.AreSame(toothChartImageHost,wrapper.Controls.Cast<Control>().Single(),"Nothing of the failed view is left on the wrapper.");
					List<string> listMessages=ToothChartWrapper.GetFallbackMessagesSince(countBefore);
					Assert.AreEqual(1,listMessages.Count,string.Join("; ",listMessages));
					StringAssert.Contains(listMessages[0],"live view could not start");
					//It is not tried again: the next change goes to the image host.
					int countImages=toothChartImageHost.CountImagesReceived;
					relay.SetSelected("8",true);
					Assert.IsTrue(WpfRendererTests.PumpUntil(() => toothChartImageHost.CountImagesReceived>countImages && !toothChartImageHost.IsRenderInFlight,20000));
					Assert.AreEqual(1,countMade,"No second try.");
					using(Bitmap bitmap=relay.GetBitmap())
					using(Bitmap bitmapShown=toothChartImageHost.GetBitmapShown()) {
						Assert.IsTrue(ChartImageCompare.GetPixelsArgb(bitmap).SequenceEqual(ChartImageCompare.GetPixelsArgb(bitmapShown)),"The current chart.");
					}
					wrapper.Dispose();
					form.Close();
				}
				finally {
					ToothChartWpfView.ActionStepForTests=null;
				}
			});
		}

		[TestMethod]
		public void LiveView_WhoseShowSceneThrows_IsReleased_AndTheImageHostShowsTheScene() {
			//RebuildWpfOnScreen called the view's ShowScene with no guard: an exception there (a WPF failure on the UI thread) went up the message loop.
			RequireDesktop();
			StaRunner.Run(() => {
				List<ToothChartWpfView> listViewsFailed=new List<ToothChartWpfView>();
				ToothChartWpfView.ActionStepForTests=(toothChartWpfView,step) => {
					if(step==ToothChartWpfView.StepShowScene) {
						listViewsFailed.Add(toothChartWpfView);
						throw new InvalidOperationException("A test: the live view cannot show the scene.");
					}
				};
				try {
					using Form form=WpfCharts.CreateOffscreenForm(ChartScenes.SizeModule.Size);
					ToothChartWrapper wrapper=WpfCharts.CreateWrapper(ChartScenes.SizeModule.Size);
					form.Controls.Add(wrapper);
					ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
					ChartScenes.Prepare(relay,ChartScenes.Get("restored-adult"),ChartColorSet.Module);
					int countBefore=ToothChartWrapper.GetCountFallbackMessages();
					form.Show();
					ToothChartImageHost toothChartImageHost=wrapper.ImageHostWpf;
					Assert.IsTrue(WpfRendererTests.PumpUntil(() => toothChartImageHost.CountImagesReceived>=1 && !toothChartImageHost.IsRenderInFlight,20000),
						"The image host shows the scene the live view could not.");
					Assert.AreEqual(1,listViewsFailed.Count,"The live view was made and asked to show the scene once.");
					ToothChartWpfView toothChartWpfViewFailed=listViewsFailed[0];
					Assert.IsTrue(toothChartWpfViewFailed.IsDisposed,"The failed view is released.");
					Assert.IsNull(toothChartWpfViewFailed.Child,"Its content was detached first.");
					Assert.IsNull(wrapper.LiveViewWpf);
					Assert.IsTrue(toothChartImageHost.Visible);
					Assert.AreSame(toothChartImageHost,wrapper.Controls.Cast<Control>().Single());
					List<string> listMessages=ToothChartWrapper.GetFallbackMessagesSince(countBefore);
					Assert.AreEqual(1,listMessages.Count,string.Join("; ",listMessages));
					StringAssert.Contains(listMessages[0],"live view could not show the chart");
					using(Bitmap bitmap=relay.GetBitmap())
					using(Bitmap bitmapShown=toothChartImageHost.GetBitmapShown()) {
						Assert.IsTrue(ChartImageCompare.GetPixelsArgb(bitmap).SequenceEqual(ChartImageCompare.GetPixelsArgb(bitmapShown)),"The scene of the failed rebuild.");
					}
					//The wrapper stays on the image host.
					int countImages=toothChartImageHost.CountImagesReceived;
					relay.SetSelected("8",true);
					Assert.IsTrue(WpfRendererTests.PumpUntil(() => toothChartImageHost.CountImagesReceived>countImages && !toothChartImageHost.IsRenderInFlight,20000));
					Assert.AreEqual(1,listViewsFailed.Count,"No new live view.");
					Assert.IsNull(wrapper.LiveViewWpf);
					wrapper.Dispose();
					form.Close();
				}
				finally {
					ToothChartWpfView.ActionStepForTests=null;
				}
			});
		}

		[TestMethod]
		public void Cursors_ThatCannotBeMade_AreTheArrow_AndChoosingAToolNeverThrows() {
			//WPF's Cursor(Stream) writes a temporary file for each cursor it makes, and Path.GetTempFileName fails once the temp folder holds 65,535 .tmp
			//files. The cursors are made once per thread, and one that cannot be made is the arrow, logged once.
			RequireDesktop();
			StaRunner.Run(() => {
				int countOpened=0;
				ToothChartMouse.ActionOpenCursorForTests=cursorTool => {
					countOpened++;
					throw new System.IO.IOException("A test: the temporary folder is full.");
				};
				try {
					using Form form=WpfCharts.CreateOffscreenForm(ChartScenes.SizeModule.Size);
					ToothChartWrapper wrapper=WpfCharts.CreateWrapper(ChartScenes.SizeModule.Size);
					form.Controls.Add(wrapper);
					ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
					form.Show();
					ToothChartWpfView toothChartWpfView=WaitForLiveView(wrapper);
					int countBefore=ToothChartWrapper.GetCountFallbackMessages();
					foreach(CursorTool cursorTool in new[] { CursorTool.Pen,CursorTool.Eraser,CursorTool.ColorChanger,CursorTool.Pen,CursorTool.Pointer,CursorTool.Eraser }) {
						relay.CursorTool=cursorTool;//never throws
						Assert.AreEqual(cursorTool,wrapper.CursorTool);
						Assert.IsNull(toothChartWpfView.CursorWpf,"The live view's arrow for "+cursorTool+".");
						Assert.AreSame(Cursors.Default,wrapper.Cursor,"The wrapper's arrow for "+cursorTool+".");
					}
					List<string> listMessages=ToothChartWrapper.GetFallbackMessagesSince(countBefore);
					Assert.AreEqual(2,listMessages.Count,"Logged once for the wrapper's cursors and once for the live view's: "+string.Join("; ",listMessages));
					Assert.IsTrue(listMessages.All(x => x.Contains("could not make the cursor of Pen") && x.Contains("ColorChanger")),string.Join("; ",listMessages));
					Assert.AreEqual(6,countOpened,"Each cursor file was tried once on this thread (three for the wrapper, three for the live view), not on every "
						+"tool change.");
					//The drawing tools still work with the arrow.
					List<string> listSegments=new List<string>();
					wrapper.SegmentDrawn+=(sender,e) => listSegments.Add(e.DrawingSegement);
					relay.CursorTool=CursorTool.Pen;
					toothChartWpfView.MouseDownForTests(new Point(30,280));
					toothChartWpfView.MouseMoveForTests(new Point(45,284));
					toothChartWpfView.MouseUpForTests(new Point(45,284));
					Assert.AreEqual(1,listSegments.Count);
					wrapper.Dispose();
					form.Close();
				}
				finally {
					ToothChartMouse.ActionOpenCursorForTests=null;
				}
			});
			//On a thread whose cursor files open, the cursors are made once and kept: the same objects on every tool change.
			StaRunner.Run(() => {
				int countOpened=0;
				ToothChartMouse.ActionOpenCursorForTests=cursorTool => countOpened++;
				try {
					System.Windows.Input.Cursor cursorPen=ToothChartWpfView.GetCursorWpf(CursorTool.Pen);
					Assert.IsNotNull(cursorPen);
					Assert.IsNotNull(ToothChartWpfView.GetCursorWpf(CursorTool.Eraser));
					Assert.AreSame(cursorPen,ToothChartWpfView.GetCursorWpf(CursorTool.Pen));
					Assert.IsNull(ToothChartWpfView.GetCursorWpf(CursorTool.Pointer),"The arrow.");
					Cursor cursorEraser=ToothChartMouse.GetCursor(CursorTool.Eraser);
					Assert.AreNotSame(Cursors.Default,cursorEraser);
					Assert.AreSame(cursorEraser,ToothChartMouse.GetCursor(CursorTool.Eraser));
					Assert.AreEqual(6,countOpened,"Three WPF cursors and three WinForms cursors, once each.");
				}
				finally {
					ToothChartMouse.ActionOpenCursorForTests=null;
				}
			});
		}

		[TestMethod]
		public void LiveView_DpiChangeWithoutANewScene_SetsItsTransformAgain() {
			//A window moved to a monitor of another scale under the program's PerMonitorV2 brings WPF a new DPI and the view no new scene. The test changes
			//the DPI of the view's window root (VisualTreeHelper.SetRootDpi), as WPF does on such a move, and puts it back.
			RequireDesktop();
			StaRunner.Run(() => {
				using Form form=WpfCharts.CreateOffscreenForm(ChartScenes.SizeModule.Size);
				ToothChartWrapper wrapper=WpfCharts.CreateWrapper(ChartScenes.SizeModule.Size);
				form.Controls.Add(wrapper);
				ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
				ChartScenes.Prepare(relay,ChartScenes.Get("restored-adult"),ChartColorSet.Module);
				form.Show();
				ToothChartWpfView toothChartWpfView=WaitForLiveView(wrapper);
				System.Windows.Media.Visual visualRoot=GetRootVisual(toothChartWpfView);
				System.Windows.DpiScale dpiScaleOld=System.Windows.Media.VisualTreeHelper.GetDpi(visualRoot);
				Assert.AreEqual(dpiScaleOld.DpiScaleX,toothChartWpfView.DpiScaleShown,1e-9);
				int countShown=toothChartWpfView.CountScenesShown;
				double dpiScaleNew=dpiScaleOld.DpiScaleX==1.5?2.0:1.5;
				try {
					System.Windows.Media.VisualTreeHelper.SetRootDpi(visualRoot,new System.Windows.DpiScale(dpiScaleNew,dpiScaleNew));
					Assert.AreEqual(dpiScaleNew,toothChartWpfView.DpiScaleShown,1e-9,"The transform follows the new DPI at once.");
					Assert.AreEqual(countShown,toothChartWpfView.CountScenesShown,"Without a new scene.");
					SizeF sizeDevice=GetContentDeviceSize(toothChartWpfView);
					Assert.AreEqual(ChartScenes.SizeModule.Size.Width,sizeDevice.Width,0.01,"Still one scene unit on one device pixel.");
					Assert.AreEqual(ChartScenes.SizeModule.Size.Height,sizeDevice.Height,0.01);
				}
				finally {
					System.Windows.Media.VisualTreeHelper.SetRootDpi(visualRoot,dpiScaleOld);
				}
				Assert.AreEqual(dpiScaleOld.DpiScaleX,toothChartWpfView.DpiScaleShown,1e-9,"And back.");
				wrapper.Dispose();
				form.Close();
			});
		}

		[TestMethod]
		public void LiveView_At150Percent_ItsCachedLayerIsRasterizedAtTheDevicePixels() {
			//The 3D layer's BitmapCache (RenderAtScale 1.0) sits under the 1/DPI layout transform. WPF rasterizes a cache in the element's own units and
			//composites it through the transforms above it, so at 150% the cache of a 615-pixel chart has 615 pixels and lands 1:1 on the device
			//pixels, not 410 resampled or 922 reduced. Drawn at 144 dpi (RenderTargetBitmap, WPF's software rasterizer, as the program draws the view)
			//with the view's own 1/1.5 transform, the cached view equals the uncached one exactly, and a cache rasterized at 1.5x does not.
			//Here the window stays 615 DIPs wide (only its visual DPI is forced), so the content is smaller than its host, as in a resize before the
			//rebuild: it must stay at 0,0. ElementHost sets its child's alignment to Stretch, which centred it at a fractional device offset where the cached
			//image was resampled (24,496 pixels differed); the view sets top-left again after it hosts the content.
			RequireDesktop();
			StaRunner.Run(() => {
				Size size=WpfTimings.Size150;
				using Form form=WpfCharts.CreateOffscreenForm(size);
				ToothChartWrapper wrapper=WpfCharts.CreateWrapper(size);
				form.Controls.Add(wrapper);
				ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
				ChartScenes.Prepare(relay,ChartScenes.Get("restored-adult"),ChartColorSet.Module);
				form.Show();
				ToothChartWpfView toothChartWpfView=WaitForLiveView(wrapper);
				System.Windows.Controls.Grid grid=(System.Windows.Controls.Grid)toothChartWpfView.Child;
				System.Windows.Controls.Border border3D=grid.Children.OfType<System.Windows.Controls.Border>().Single();
				System.Windows.Media.Visual visualRoot=GetRootVisual(toothChartWpfView);
				System.Windows.DpiScale dpiScaleOld=System.Windows.Media.VisualTreeHelper.GetDpi(visualRoot);
				try {
					System.Windows.Media.VisualTreeHelper.SetRootDpi(visualRoot,new System.Windows.DpiScale(1.5,1.5));
					Assert.AreEqual(1.5,toothChartWpfView.DpiScaleShown,1e-9);
					((System.Windows.UIElement)visualRoot).UpdateLayout();
					Assert.AreEqual(new System.Windows.Point(0,0),grid.TransformToAncestor(visualRoot).Transform(new System.Windows.Point(0,0)),
						"The content stays at the host's top-left when the host is larger than it.");
					System.Windows.Media.BitmapCache bitmapCache=(System.Windows.Media.BitmapCache)border3D.CacheMode;
					Assert.AreEqual(1.0,bitmapCache.RenderAtScale);
					byte[] arrayCached=RenderAt(grid,size,144);
					toothChartWpfView.Is3DLayerCached=false;
					byte[] arrayUncached=RenderAt(grid,size,144);
					if(!arrayCached.SequenceEqual(arrayUncached)) {
						Assert.Fail("At 150% the cache changes "+CountPixelsDiffering(arrayCached,arrayUncached)+" pixels of the view: it is resampled.");
					}
					System.Windows.Media.BitmapCache bitmapCacheAt15=new System.Windows.Media.BitmapCache(1.5);
					bitmapCacheAt15.SnapsToDevicePixels=true;
					border3D.CacheMode=bitmapCacheAt15;
					byte[] arrayCachedAt15=RenderAt(grid,size,144);
					Assert.IsTrue(CountPixelsDiffering(arrayCachedAt15,arrayUncached)>100,"A cache rasterized at 1.5x, which a DPI fix might set, would be resampled "
						+"(the test sees it): "+CountPixelsDiffering(arrayCachedAt15,arrayUncached)+" pixels.");
					//The content drawn at 144 dpi covers the scene's pixels exactly: its last row and column are the chart's, and nothing lies beyond.
					SizeF sizeDevice=GetContentDeviceSize(toothChartWpfView);
					Assert.AreEqual(size.Width,sizeDevice.Width,0.01);
					Assert.AreEqual(size.Height,sizeDevice.Height,0.01);
				}
				finally {
					toothChartWpfView.Is3DLayerCached=true;
					System.Windows.Media.VisualTreeHelper.SetRootDpi(visualRoot,dpiScaleOld);
				}
				wrapper.Dispose();
				form.Close();
			});
		}

		///<summary>The visual drawn into a bitmap of the size at the dpi by WPF's software rasterizer (RenderTargetBitmap), with its own transform.</summary>
		private static byte[] RenderAt(System.Windows.Media.Visual visual,Size size,double dpi) {
			System.Windows.Media.Imaging.RenderTargetBitmap renderTargetBitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(size.Width,size.Height,dpi,dpi,
				System.Windows.Media.PixelFormats.Pbgra32);
			renderTargetBitmap.Render(visual);
			byte[] arrayPixels=new byte[size.Width*size.Height*4];
			renderTargetBitmap.CopyPixels(arrayPixels,size.Width*4,0);
			return arrayPixels;
		}

		///<summary>The pixels whose channels differ by more than 8 of 255 (section 7).</summary>
		private static int CountPixelsDiffering(byte[] arrayA,byte[] arrayB) {
			int count=0;
			for(int i=0;i<arrayA.Length;i+=4) {
				if(Math.Abs(arrayA[i]-arrayB[i])>8 || Math.Abs(arrayA[i+1]-arrayB[i+1])>8 || Math.Abs(arrayA[i+2]-arrayB[i+2])>8) {
					count++;
				}
			}
			return count;
		}

		[TestMethod]
		public void LiveView_TakesTheChartsBackground_BeforeItsFirstScene() {
			//The content's background was gray until the first ShowScene, so a gray frame could flash when the Chart module first showed.
			StaRunner.Run(() => {
				ToothChartData tcData=new ToothChartData();
				tcData.ColorBackground=Color.FromArgb(255,12,34,56);
				using ToothChartWpfView toothChartWpfView=new ToothChartWpfView();
				toothChartWpfView.TcData=tcData;
				Assert.IsNull(toothChartWpfView.SceneShown,"No scene yet.");
				System.Windows.Media.SolidColorBrush brush=((System.Windows.Controls.Grid)toothChartWpfView.Child).Background as System.Windows.Media.SolidColorBrush;
				Assert.IsNotNull(brush);
				Assert.AreEqual(System.Windows.Media.Color.FromRgb(12,34,56),brush.Color,"The chart's background from the start.");
				Assert.AreEqual(tcData.ColorBackground.ToArgb(),toothChartWpfView.BackColor.ToArgb(),"The host's own back colour too.");
			});
		}

		[TestMethod]
		public void LiveView_RefillThenGetBitmap_StillShowsTheRefill() {
			//TC-1's 5.5 case on the live view: a refill that ends in the dashboard's GetBitmap (which builds the scene and clears the dirty mark first)
			//still updates the screen.
			RequireDesktop();
			StaRunner.Run(() => {
				using Form form=WpfCharts.CreateOffscreenForm(ChartScenes.SizeModule.Size);
				ToothChartWrapper wrapper=WpfCharts.CreateWrapper(ChartScenes.SizeModule.Size);
				form.Controls.Add(wrapper);
				ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
				ChartScenes.Prepare(relay,ChartScenes.Get("restored-adult"),ChartColorSet.Module);
				form.Show();
				ToothChartWpfView toothChartWpfView=WaitForLiveView(wrapper);
				int countShown=toothChartWpfView.CountScenesShown;
				ChartScenes.Prepare(relay,ChartScenes.Get("treatment-plan"),ChartColorSet.Module);
				Assert.IsTrue(wrapper.IsWpfSceneDirty);
				using(Bitmap bitmap=relay.GetBitmap()) {
					Assert.IsFalse(wrapper.IsWpfSceneDirty,"GetBitmap built the scene.");
					Assert.IsTrue(WpfRendererTests.PumpUntil(() => toothChartWpfView.CountScenesShown>countShown,20000),"The posted rebuild still ran.");
					using(Bitmap bitmapScene=ToothChartRenderer.Instance.Render(toothChartWpfView.SceneShown,null).ToBitmap()) {
						Assert.IsTrue(ChartImageCompare.GetPixelsArgb(bitmap).SequenceEqual(ChartImageCompare.GetPixelsArgb(bitmapScene)),
							"The view shows the refilled data: the scene GetBitmap drew.");
					}
				}
				WpfRendererTests.PumpUntil(() => false,100);
				Assert.AreEqual(countShown+1,toothChartWpfView.CountScenesShown,"One rebuild for the whole refill.");
				wrapper.Dispose();
				form.Close();
			});
		}

		[TestMethod]
		public void LiveView_HiddenRefillThenGetBitmap_ShowsTheRefillWhenShownAgain() {
			//TC-1's 5.5 case on the live view: a wrapper refilled while hidden and then asked for its bitmap (as the dashboard does) shows the refill
			//when it is shown again, in the same live view.
			RequireDesktop();
			StaRunner.Run(() => {
				using Form form=WpfCharts.CreateOffscreenForm(ChartScenes.SizeModule.Size);
				ToothChartWrapper wrapper=WpfCharts.CreateWrapper(ChartScenes.SizeModule.Size);
				form.Controls.Add(wrapper);
				ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
				ChartScenes.Prepare(relay,ChartScenes.Get("restored-adult"),ChartColorSet.Module);
				form.Show();
				ToothChartWpfView toothChartWpfView=WaitForLiveView(wrapper);
				form.Hide();
				Assert.IsFalse(wrapper.Visible);
				int countShown=toothChartWpfView.CountScenesShown;
				ChartScenes.Prepare(relay,ChartScenes.Get("treatment-plan"),ChartColorSet.Module);
				WpfRendererTests.PumpUntil(() => false,200);
				Assert.AreEqual(countShown,toothChartWpfView.CountScenesShown,"A hidden wrapper posts no rebuild.");
				using(Bitmap bitmap=relay.GetBitmap()) {
					Assert.IsFalse(wrapper.IsWpfSceneDirty,"GetBitmap built the scene.");
					form.Show();
					Assert.IsTrue(WpfRendererTests.PumpUntil(() => toothChartWpfView.CountScenesShown>countShown,20000),"Shown again, the wrapper rebuilt its screen.");
					Assert.AreSame(toothChartWpfView,wrapper.LiveViewWpf,"The same live view.");
					using(Bitmap bitmapScene=ToothChartRenderer.Instance.Render(toothChartWpfView.SceneShown,null).ToBitmap()) {
						Assert.IsTrue(ChartImageCompare.GetPixelsArgb(bitmap).SequenceEqual(ChartImageCompare.GetPixelsArgb(bitmapScene)),
							"The view shows the refill made while hidden.");
					}
				}
				wrapper.Dispose();
				form.Close();
			});
		}
		#endregion Fix round 3
	}
}
