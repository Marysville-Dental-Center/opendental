// Renderer tests of the WPF chart (package TC-1, docs/specs/wpf-tooth-chart-plan.md sections 5.4 to 5.6 and 7): an MTA caller, a render loop,
// process exit with the render thread alive, the DPI settings, the on-screen refresh after a GetBitmap, a host disposed during its render, and
// determinism in one process and across two.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;
using OpenPmsDentalBusiness;
using SparksToothChart;
using SparksToothChart.Wpf3D;

namespace ToothChartImageTests {
	[TestClass]
	public class WpfRendererTests {
		[TestMethod]
		public void MtaCaller_GetsTheWpfImage_WithoutAFallback() {
			StaRunner.RunMta(() => CheckMtaCaller());
		}

		///<summary>On an MTA thread, as the dashboard's ODThread worker makes its chart (UserControlDashboard, DashToothChart).</summary>
		private static void CheckMtaCaller() {
			Assert.AreEqual(ApartmentState.MTA,Thread.CurrentThread.GetApartmentState());
			int countMessages=ToothChartWrapper.GetFallbackMessages().Count;
			int countUnhandled=ToothChartRenderer.CountUnhandledExceptions;
			using(ToothChartWrapper wrapper=WpfCharts.CreateWrapper(ChartScenes.SizePrintout.Size)) {
				Assert.AreEqual(ToothChartEngine.Wpf3D,wrapper.Engine,string.Join("; ",ToothChartWrapper.GetFallbackMessages()));
				ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
				using(Bitmap bitmap=WpfCharts.Draw(relay,ChartScenes.Get("restored-adult"),ChartColorSet.Printout)) {
					Assert.AreEqual(ChartScenes.SizePrintout.Size,bitmap.Size);
					Assert.AreEqual(PixelFormat.Format24bppRgb,bitmap.PixelFormat,"The pixel format TC-0 recorded for DirectX.");
					BitmapFacts bitmapFacts=ChartImageCompare.DescribeBitmap(bitmap);
					Assert.IsFalse(bitmapFacts.IsBlank);
					Assert.IsTrue(bitmapFacts.CountColors>200,"A lit, antialiased chart has many colours: "+bitmapFacts.CountColors);
				}
				Assert.AreEqual(ToothChartEngine.Wpf3D,wrapper.Engine,"Still the WPF chart.");
				Assert.AreEqual(DrawingMode.DirectX,wrapper.DrawMode,"DrawMode reads the stored value the WPF chart replaces.");
			}
			Assert.AreEqual(countMessages,ToothChartWrapper.GetFallbackMessages().Count,"Nothing was logged: "
				+string.Join("; ",ToothChartWrapper.GetFallbackMessages().Skip(countMessages)));
			Assert.AreEqual(countUnhandled,ToothChartRenderer.CountUnhandledExceptions);
			Assert.IsNull(System.Windows.Threading.Dispatcher.FromThread(Thread.CurrentThread),"No WPF object was made on the caller's thread.");
			Assert.AreEqual(ApartmentState.STA,ToothChartRenderer.Instance.ThreadApartment,"The render thread is STA.");
			Assert.AreNotEqual(Thread.CurrentThread.ManagedThreadId,ToothChartRenderer.Instance.ThreadId);
		}

		[TestMethod]
		public void RenderLoop_FiveHundredImages_WithoutAFailure() {
			int countMessages=ToothChartWrapper.GetFallbackMessages().Count;
			int countUnhandled=ToothChartRenderer.CountUnhandledExceptions;
			ChartSize[] arraySizes={ ChartScenes.SizeModule,ChartScenes.SizePrintout,ChartScenes.SizeSheetWide,ChartScenes.SizeSheetTall };
			List<ToothChartWrapper> listWrappers=arraySizes.Select(x => WpfCharts.CreateWrapper(x.Size)).ToList();
			try {
				List<ToothChartRelay> listRelays=listWrappers.Select(x => WpfCharts.CreateRelay(x)).ToList();
				for(int w=0;w<listRelays.Count;w++) {
					ChartScenes.Prepare(listRelays[w],ChartScenes.Get("treatment-plan"),arraySizes[w].ColorSet);
				}
				Stopwatch stopwatch=Stopwatch.StartNew();
				for(int i=0;i<500;i++) {
					//Mostly the module and printout sizes, now and then the two sheet sizes: four sizes, one more than the renderer keeps.
					int w=i%25==24?2+(i/25)%2:i%2;
					ToothChartRelay relay=listRelays[w];
					if(i%50==0) {
						relay.SetSurfaceColors("14","MOD",i%100==0?ChartScenes.TP.Dark:ChartScenes.C.Dark);//a 3D change, now and then
					}
					else {
						relay.SetSelected((1+i%32).ToString(),i%3==0);//a 2D change
					}
					using(Bitmap bitmap=relay.GetBitmap()) {
						Assert.AreEqual(arraySizes[w].Size,bitmap.Size,"Image "+i);
					}
					Assert.AreEqual(ToothChartEngine.Wpf3D,listWrappers[w].Engine,"Image "+i);
				}
				Console.WriteLine("500 images in "+stopwatch.ElapsedMilliseconds+" ms.");
				Assert.IsTrue(ToothChartRenderer.Instance.GetCountSizesKept()<=ToothChartRenderer.MaxSizesKept,"At most three sizes keep render targets.");
			}
			finally {
				listWrappers.ForEach(x => x.Dispose());
			}
			Assert.AreEqual(countMessages,ToothChartWrapper.GetFallbackMessages().Count,"No render failed: "
				+string.Join("; ",ToothChartWrapper.GetFallbackMessages().Skip(countMessages)));
			Assert.AreEqual(countUnhandled,ToothChartRenderer.CountUnhandledExceptions);
		}

		[TestMethod]
		public void Process_Exits_WhileTheRenderThreadIsAlive() {
			SecondProcessResult result=SecondProcess.Run(120000,"exit");
			Console.WriteLine(result.Output);
			Assert.IsFalse(result.IsTimedOut,"The process did not end: the render thread kept it alive.");
			Assert.AreEqual(0,result.ExitCode,result.Output);
			StringAssert.Contains(result.Output,"render thread alive: True","The process ended with its render thread still running.");
		}

		[TestMethod]
		public void Text_UsesPixelsPerDipOne_AndEveryRootHasDpiScaleOne() {
			ToothScene toothScene=StaRunner.Run(() => {
				using(ToothChartWrapper wrapper=new ToothChartWrapper()) {
					wrapper.Size=ChartScenes.SizePrintout.Size;
					ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
					ChartScenes.Prepare(relay,ChartScenes.Get("treatment-plan"),ChartColorSet.Printout);
					relay.SetSealant("19",Color.Purple);
					relay.SetText("3",Color.Red,"W");
					relay.SetSelected("8",true);
					return ToothSceneBuilder.Build(wrapper.TcData);
				}
			});
			List<ToothSceneItem2D> listTexts=toothScene.Items2D.Where(x => x.Kind==ChartItem2DKind.Text).ToList();
			Assert.IsTrue(listTexts.Count>=32+3,"The numbers, the sealant and the watch: "+listTexts.Count);
			ToothChartRenderResult result=ToothChartRenderer.Instance.Render(toothScene,null);
			Assert.AreEqual(listTexts.Count,result.ListTexts.Count,"Every text item was drawn.");
			foreach(ChartTextReport chartTextReport in result.ListTexts) {
				Assert.AreEqual(1.0,chartTextReport.PixelsPerDip,chartTextReport.Text);
				Assert.AreEqual(System.Windows.Media.TextFormattingMode.Display,chartTextReport.TextFormattingMode);
				Assert.IsTrue(chartTextReport.ScaleX>0.5 && chartTextReport.ScaleX<2,chartTextReport.Text+" scale "+chartTextReport.ScaleX);
			}
			Assert.AreEqual(1.0,result.RootDpiScaleX2D);
			Assert.AreEqual(1.0,result.RootDpiScaleY2D);
			Assert.AreEqual(1.0,result.RootDpiScaleX3D);
			Assert.AreEqual(1.0,result.RootDpiScaleY3D);
			Assert.AreEqual(ToothChartRenderer.Supersampling,result.Factor3D);
			Assert.IsFalse(result.IsLayer3DReused);
		}

		[TestMethod]
		public void SelectionOnlyChange_ReusesThe3DLayer_AndAColourChangeDoesNot() {
			StaRunner.Run(() => {
				using(ToothChartWrapper wrapper=new ToothChartWrapper())
				using(ToothChartLayerCache layerCache=new ToothChartLayerCache()) {
					wrapper.Size=ChartScenes.SizeModule.Size;
					ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
					ChartScenes.Prepare(relay,ChartScenes.Get("restored-adult"),ChartColorSet.Module);
					ToothChartRenderResult result1=ToothChartRenderer.Instance.Render(ToothSceneBuilder.Build(wrapper.TcData),layerCache);
					Assert.IsFalse(result1.IsLayer3DReused);
					Assert.IsTrue(layerCache.HasImage);
					relay.SetSelected("8",true);
					ToothChartRenderResult result2=ToothChartRenderer.Instance.Render(ToothSceneBuilder.Build(wrapper.TcData),layerCache);
					Assert.IsTrue(result2.IsLayer3DReused,"A selection is 2D only.");
					Assert.IsFalse(result2.PixelsBgra.SequenceEqual(result1.PixelsBgra),"The selection shows.");
					relay.SetSurfaceColors("8","F",Color.Blue);
					ToothChartRenderResult result3=ToothChartRenderer.Instance.Render(ToothSceneBuilder.Build(wrapper.TcData),layerCache);
					Assert.IsFalse(result3.IsLayer3DReused,"A colour is 3D.");
					//Reusing the layer gives the same pixels as rendering it again.
					relay.SetSelected("9",true);
					ToothScene toothScene=ToothSceneBuilder.Build(wrapper.TcData);
					ToothChartRenderResult resultReused=ToothChartRenderer.Instance.Render(toothScene,layerCache);
					ToothChartRenderResult resultFresh=ToothChartRenderer.Instance.Render(toothScene,null);
					Assert.IsTrue(resultReused.IsLayer3DReused);
					Assert.IsTrue(resultReused.PixelsBgra.SequenceEqual(resultFresh.PixelsBgra),"The reused layer is the layer.");
					layerCache.Dispose();
					Assert.IsFalse(layerCache.HasImage,"Dispose releases the image.");
				}
			});
		}

		[TestMethod]
		public void Wrapper_HoldsItsLayerImage_AndReleasesItOnDispose() {
			StaRunner.Run(() => {
				ToothChartWrapper wrapper=WpfCharts.CreateWrapper(ChartScenes.SizeModule.Size);
				ToothChartLayerCache layerCache=wrapper.ImageHostWpf.LayerCache;
				Assert.IsFalse(layerCache.HasImage);
				using(Bitmap bitmap=wrapper.GetBitmap()) {
				}
				Assert.IsTrue(layerCache.HasImage,"GetBitmap keeps the wrapper's 3D layer.");
				wrapper.Dispose();
				Assert.IsTrue(layerCache.IsDisposed && !layerCache.HasImage,"Dispose releases it.");
			});
		}

		[TestMethod]
		public void Engine_Wpf3D_KeepsPerioDirectXOnly_AndSwitchesBack() {
			StaRunner.Run(() => {
				using(ToothChartWrapper wrapper=WpfCharts.CreateWrapper(ChartScenes.SizeModule.Size)) {
					Assert.AreEqual(ToothChartEngine.Wpf3D,wrapper.Engine);
					Assert.IsNotNull(wrapper.ImageHostWpf);
					Assert.AreSame(wrapper.ImageHostWpf,wrapper.Controls.Cast<Control>().Single(),"The image host is the wrapper's one engine control.");
					Exception ex=Expect.Throws<Exception>(() => wrapper.PerioMode=true);
					StringAssert.Contains(ex.Message,"DirectX","Perio on the WPF chart comes with TC-7.");
					wrapper.DrawMode=DrawingMode.Simple2D;
					Assert.AreEqual(ToothChartEngine.Simple2D,wrapper.Engine);
					Assert.IsNull(wrapper.ImageHostWpf,"Leaving Wpf3D releases its engine control.");
					wrapper.Engine=ToothChartEngine.Wpf3D;
					Assert.AreEqual(ToothChartEngine.Wpf3D,wrapper.Engine);
					using(Bitmap bitmap=wrapper.GetBitmap()) {
						Assert.AreEqual(ChartScenes.SizeModule.Size,bitmap.Size);
					}
					wrapper.Engine=ToothChartEngine.Simple2D;
					Assert.AreEqual(DrawingMode.Simple2D,wrapper.DrawMode);
				}
			});
		}

		[TestMethod]
		public void ShownWrapper_RefillThenGetBitmap_StillUpdatesTheScreen() {
			if(!Environment.UserInteractive) {
				Assert.Inconclusive("Needs an interactive desktop session: the wrapper must be shown.");
			}
			StaRunner.Run(() => {
				using(Form form=new Form()) {
					form.FormBorderStyle=FormBorderStyle.None;
					form.ShowInTaskbar=false;
					form.StartPosition=FormStartPosition.Manual;
					form.Location=new Point(-32000,-32000);//off every screen, but shown
					form.ClientSize=ChartScenes.SizeModule.Size;
					ToothChartWrapper wrapper=WpfCharts.CreateWrapper(ChartScenes.SizeModule.Size);
					form.Controls.Add(wrapper);
					form.Show();
					ToothChartImageHost toothChartImageHost=wrapper.ImageHostWpf;
					ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
					ChartScenes.Prepare(relay,ChartScenes.Get("restored-adult"),ChartColorSet.Module);
					Assert.IsTrue(PumpUntil(() => toothChartImageHost.CountImagesReceived>=1 && !toothChartImageHost.IsRenderInFlight,20000),"A first image arrived.");
					int countBefore=toothChartImageHost.CountImagesReceived;
					//A refill posts one rebuild. The dashboard's GetBitmap right after it builds the scene and clears the dirty mark first.
					ChartScenes.Prepare(relay,ChartScenes.Get("treatment-plan"),ChartColorSet.Module);
					Assert.IsTrue(wrapper.IsWpfSceneDirty);
					using(Bitmap bitmap=relay.GetBitmap()) {
						Assert.IsFalse(wrapper.IsWpfSceneDirty,"GetBitmap built the scene.");
						//The posted rebuild still renders the screen's image, from the current data.
						Assert.IsTrue(PumpUntil(() => toothChartImageHost.CountImagesReceived>countBefore && !toothChartImageHost.IsRenderInFlight,20000),
							"The screen was updated after the GetBitmap.");
						using(Bitmap bitmapShown=toothChartImageHost.GetBitmapShown()) {
							Assert.IsTrue(ChartImageCompare.GetPixelsArgb(bitmap).SequenceEqual(ChartImageCompare.GetPixelsArgb(bitmapShown)),
								"The screen shows the refilled chart, the same image as GetBitmap's.");
						}
					}
					//One burst of hundreds of Set calls posts one rebuild, so it starts one render.
					int countRenders=toothChartImageHost.CountRendersStarted;
					ChartScenes.Prepare(relay,ChartScenes.Get("restored-adult"),ChartColorSet.Module);
					Assert.IsTrue(PumpUntil(() => toothChartImageHost.CountRendersStarted>countRenders && !toothChartImageHost.IsRenderInFlight,20000));
					Application.DoEvents();
					Assert.AreEqual(countRenders+1,toothChartImageHost.CountRendersStarted,"One render for the whole refill.");
					form.Close();
				}
			});
		}

		[TestMethod]
		public void HostDisposed_WhileItsRenderIsInFlight_GetsNoCall() {
			int countMessages=ToothChartWrapper.GetFallbackMessages().Count;
			int countUnhandled=ToothChartRenderer.CountUnhandledExceptions;
			StaRunner.Run(() => {
				ToothScene toothScene;
				using(ToothChartWrapper wrapper=new ToothChartWrapper()) {
					wrapper.Size=ChartScenes.SizeBigWindow.Size;//a big image, so the render is still running when the host goes
					ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
					ChartScenes.Prepare(relay,ChartScenes.Get("restored-adult"),ChartColorSet.Module);
					toothScene=ToothSceneBuilder.Build(wrapper.TcData);
				}
				ToothChartLayerCache layerCache=new ToothChartLayerCache();
				ToothChartImageHost toothChartImageHost=new ToothChartImageHost();
				toothChartImageHost.LayerCache=layerCache;
				toothChartImageHost.Size=toothScene.Size;
				toothChartImageHost.CreateControl();
				Assert.IsTrue(toothChartImageHost.IsHandleCreated);
				toothChartImageHost.ShowScene(toothScene);
				Assert.IsTrue(toothChartImageHost.IsRenderInFlight);
				toothChartImageHost.Dispose();
				ToothChartRenderer.Instance.WaitIdle();//the render has run
				PumpUntil(() => false,500);//and anything it posted has had its chance
				Assert.IsTrue(layerCache.HasImage,"The render did finish.");
				Assert.AreEqual(0,toothChartImageHost.CountImagesReceived,"The disposed host got no image.");
			});
			Assert.AreEqual(countMessages,ToothChartWrapper.GetFallbackMessages().Count,"Nothing was logged: "
				+string.Join("; ",ToothChartWrapper.GetFallbackMessages().Skip(countMessages)));
			Assert.AreEqual(countUnhandled,ToothChartRenderer.CountUnhandledExceptions,"Nothing was thrown on the render thread.");
		}

		[TestMethod]
		public void Determinism_TheSameBytes_TwiceInOneProcess_AndInASecondProcess() {
			string hashFirst=SecondProcessEntry.RenderHash("restored-adult");
			string hashSecond=SecondProcessEntry.RenderHash("restored-adult");
			Assert.AreEqual(hashFirst,hashSecond,"Two renders of one scene in one process.");
			//The same wrapper after other scenes: its 3D layer is rendered again, not reused.
			StaRunner.Run(() => {
				using(ToothChartWrapper wrapper=WpfCharts.CreateWrapper(ChartScenes.SizeModule.Size)) {
					ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
					using(Bitmap bitmap=WpfCharts.Draw(relay,ChartScenes.Get("treatment-plan"),ChartColorSet.Module)) {
					}
					using(Bitmap bitmap=WpfCharts.Draw(relay,ChartScenes.Get("restored-adult"),ChartColorSet.Module)) {
						Assert.AreEqual(hashFirst,ChartImageCompare.Sha256(ChartImageCompare.GetPixelsArgb(bitmap)),"On an STA caller, after another scene.");
					}
				}
			});
			SecondProcessResult result=SecondProcess.Run(120000,"hash","restored-adult");
			Assert.AreEqual(0,result.ExitCode,result.Output);
			StringAssert.Contains(result.Output,"SHA256 "+hashFirst,"A second process gives the same bytes.");
		}

		///<summary>Pumps the message loop of the calling thread until the condition holds or the time is up.</summary>
		public static bool PumpUntil(Func<bool> funcCondition,int millisecondsTimeout) {
			Stopwatch stopwatch=Stopwatch.StartNew();
			while(stopwatch.ElapsedMilliseconds<millisecondsTimeout) {
				Application.DoEvents();
				if(funcCondition()) {
					return true;
				}
				Thread.Sleep(5);
			}
			Application.DoEvents();
			return funcCondition();
		}
	}
}
