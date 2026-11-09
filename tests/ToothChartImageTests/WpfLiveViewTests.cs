// Tests of the WPF chart's live view (package TC-3, O-19 (b), docs/specs/wpf-tooth-chart-plan.md sections 5.4 and 7): made only when the wrapper is
// shown on an STA thread; a hidden or MTA wrapper uses the renderer; the "Draw in software" switch; the view draws the renderer's scene; a selection
// keeps its 3D layer; its mouse.
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
		private static void RequireDesktop() {
			if(!Environment.UserInteractive) {
				Assert.Inconclusive("Needs an interactive desktop session: the wrapper must be shown.");
			}
		}

		[TestMethod]
		public void Assembly_DrawsTheLiveViewInSoftware() {
			Assert.AreEqual(System.Windows.Interop.RenderMode.SoftwareOnly,System.Windows.Media.RenderOptions.ProcessRenderMode,
				"Section 7: the tests that host the live view draw it in software (AssemblyInitialize).");
		}

		[TestMethod]
		public void SoftwareFormat_IsTheStoredValueOfTheSwitch() {
			Assert.AreEqual("WpfSoftware",ToothChartWrapper.DirectXFormatWpfSoftware);
			Assert.IsTrue(ToothChartWrapper.IsWpfSoftwareFormat("WpfSoftware"));
			Assert.IsTrue(ToothChartWrapper.IsWpfSoftwareFormat(" WpfSoftware "));
			Assert.IsFalse(ToothChartWrapper.IsWpfSoftwareFormat(null));
			Assert.IsFalse(ToothChartWrapper.IsWpfSoftwareFormat(""));
			Assert.IsFalse(ToothChartWrapper.IsWpfSoftwareFormat("0;Hardware;64;D16;X8R8G8B8;None"),"A DirectX format means the hardware view.");
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
				ToothChartWpfView toothChartWpfView=wrapper.LiveViewWpf;
				Assert.IsNotNull(toothChartWpfView,"Shown on an STA thread: the live view.");
				Assert.IsFalse(wrapper.ImageHostWpf.Visible,"The image host is hidden under it.");
				Assert.IsTrue(WpfRendererTests.PumpUntil(() => toothChartWpfView.SceneShown!=null,20000),"The posted rebuild showed the scene.");
				ToothScene toothScene=WpfCharts.BuildScene(wrapper);
				Assert.AreEqual(toothScene.Items3D.Count,toothChartWpfView.CountModelsShown,"Every 3D item is a model of the view.");
				Assert.AreEqual(0,wrapper.ImageHostWpf.CountRendersStarted,"The hidden image host rendered nothing.");
				Assert.AreEqual(1.0,toothChartWpfView.DpiScaleShown,"The test host's window at 96 dpi.");
				int countBefore=ToothChartWrapper.GetCountFallbackMessages();
				using(Bitmap bitmap=relay.GetBitmap())
				using(Bitmap bitmapRenderer=ToothChartRenderer.Instance.Render(toothScene,null).ToBitmap()) {
					Assert.IsTrue(ChartImageCompare.GetPixelsArgb(bitmap).SequenceEqual(ChartImageCompare.GetPixelsArgb(bitmapRenderer)),
						"GetBitmap of a wrapper with a live view is the renderer's image.");
				}
				Assert.AreEqual(0,ToothChartWrapper.GetFallbackMessagesSince(countBefore).Count,"Nothing was logged.");
				wrapper.Dispose();
				Assert.IsTrue(toothChartWpfView.IsDisposed,"The wrapper's Dispose releases the live view.");
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
				Assert.IsNull(wrapper.LiveViewWpf,"An MTA thread cannot host WPF: no live view.");
				ToothChartImageHost toothChartImageHost=wrapper.ImageHostWpf;
				Assert.IsTrue(toothChartImageHost.Visible);
				Assert.IsTrue(WpfRendererTests.PumpUntil(() => toothChartImageHost.CountImagesReceived>=1 && !toothChartImageHost.IsRenderInFlight,20000),
					"The image host shows the renderer's image.");
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
				Assert.IsNull(wrapper.LiveViewWpf,"Drawn in software: no live view.");
				Assert.IsTrue(WpfRendererTests.PumpUntil(() => toothChartImageHost.CountImagesReceived>=1 && !toothChartImageHost.IsRenderInFlight,20000));
				//Off: the live view at once.
				wrapper.WpfDrawInSoftware=false;
				ToothChartWpfView toothChartWpfView=wrapper.LiveViewWpf;
				Assert.IsNotNull(toothChartWpfView);
				Assert.IsFalse(toothChartImageHost.Visible);
				Assert.IsTrue(WpfRendererTests.PumpUntil(() => toothChartWpfView.SceneShown!=null,20000),"The live view shows the chart.");
				//On again, after a change made while the live view was on: the image host shows the current chart.
				relay.SetSelected("8",true);
				Assert.IsTrue(WpfRendererTests.PumpUntil(() => toothChartWpfView.SceneShown.Items2D.Any(x => x.Source=="highlight of 8"),20000));
				int countImages=toothChartImageHost.CountImagesReceived;
				wrapper.WpfDrawInSoftware=true;
				Assert.IsNull(wrapper.LiveViewWpf);
				Assert.IsTrue(toothChartWpfView.IsDisposed,"The live view is released.");
				Assert.IsTrue(toothChartImageHost.Visible);
				Assert.IsTrue(WpfRendererTests.PumpUntil(() => toothChartImageHost.CountImagesReceived>countImages && !toothChartImageHost.IsRenderInFlight,20000),
					"The image host renders the chart again.");
				Assert.IsTrue(toothChartImageHost.SceneShown.Items2D.Any(x => x.Source=="highlight of 8"),"With the selection made meanwhile.");
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
					ToothChartWpfView toothChartWpfView=wrapper.LiveViewWpf;
					Assert.IsNotNull(toothChartWpfView);
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
				ToothChartWpfView toothChartWpfView=wrapper.LiveViewWpf;
				Assert.IsTrue(WpfRendererTests.PumpUntil(() => toothChartWpfView.SceneShown!=null && !wrapper.IsWpfSceneDirty,20000));
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
		public void LiveView_Mouse_SelectsDrawsAndRaisesTheWrappersEvents() {
			RequireDesktop();
			StaRunner.Run(() => {
				using Form form=WpfCharts.CreateOffscreenForm(ChartScenes.SizeModule.Size);
				ToothChartWrapper wrapper=WpfCharts.CreateWrapper(ChartScenes.SizeModule.Size);
				form.Controls.Add(wrapper);
				ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
				ChartScenes.Prepare(relay,ChartScenes.Get("empty-universal"),ChartColorSet.Module);
				form.Show();
				ToothChartWpfView toothChartWpfView=wrapper.LiveViewWpf;
				Assert.IsTrue(WpfRendererTests.PumpUntil(() => toothChartWpfView.SceneShown!=null && !wrapper.IsWpfSceneDirty,20000));
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
				ToothChartWpfView toothChartWpfView=wrapper.LiveViewWpf;
				Assert.IsNotNull(toothChartWpfView);
				wrapper.Engine=ToothChartEngine.Simple2D;
				Assert.IsNull(wrapper.LiveViewWpf);
				Assert.IsTrue(toothChartWpfView.IsDisposed,"Leaving Wpf3D releases the live view.");
				wrapper.Engine=ToothChartEngine.Wpf3D;
				Assert.IsNotNull(wrapper.LiveViewWpf,"A shown wrapper set to Wpf3D gets its live view again.");
				wrapper.Dispose();
				form.Close();
			});
		}
	}
}
