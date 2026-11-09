// Renderer tests of the WPF chart (package TC-1, docs/specs/wpf-tooth-chart-plan.md sections 5.4 to 5.6 and 7): an MTA caller, a render loop with
// bounded memory, process exit with the render thread alive, the DPI settings, the on-screen refresh after a GetBitmap, a host disposed during its
// render, and determinism in one process, across two and against the committed baseline; and the review's latent cases: a hidden refill, a handle
// that goes, a pixel-exact paint, a restart after a shutdown, aborted jobs, the cold start's locks, failures in a row, and the start and log lines.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
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
			int countMessages=ToothChartWrapper.GetCountFallbackMessages();
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
			Assert.AreEqual(0,ToothChartWrapper.GetFallbackMessagesSince(countMessages).Count,"Nothing was logged: "
				+string.Join("; ",ToothChartWrapper.GetFallbackMessagesSince(countMessages)));
			Assert.AreEqual(countUnhandled,ToothChartRenderer.CountUnhandledExceptions);
			Assert.IsNull(System.Windows.Threading.Dispatcher.FromThread(Thread.CurrentThread),"No WPF object was made on the caller's thread.");
			Assert.AreEqual(ApartmentState.STA,ToothChartRenderer.Instance.ThreadApartment,"The render thread is STA.");
			Assert.AreNotEqual(Thread.CurrentThread.ManagedThreadId,ToothChartRenderer.Instance.ThreadId);
		}

		[TestMethod]
		public void RenderLoop_FiveHundredImages_WithoutAFailure() {
			int countMessages=ToothChartWrapper.GetCountFallbackMessages();
			int countUnhandled=ToothChartRenderer.CountUnhandledExceptions;
			ChartSize[] arraySizes={ ChartScenes.SizeModule,ChartScenes.SizePrintout,ChartScenes.SizeSheetWide,ChartScenes.SizeSheetTall };
			List<ToothChartWrapper> listWrappers=arraySizes.Select(x => WpfCharts.CreateWrapper(x.Size)).ToList();
			try {
				List<ToothChartRelay> listRelays=listWrappers.Select(x => WpfCharts.CreateRelay(x)).ToList();
				for(int w=0;w<listRelays.Count;w++) {
					ChartScenes.Prepare(listRelays[w],ChartScenes.Get("treatment-plan"),arraySizes[w].ColorSet);
				}
				Stopwatch stopwatch=Stopwatch.StartNew();
				long bytesPrivateWarm=0;
				long bytesManagedWarm=0;
				for(int i=0;i<500;i++) {
					if(i==50) {
						//Warm: every size has been drawn (the sheet sizes at 24 and 49), and every glyph and mesh is made.
						bytesManagedWarm=GetMemoryAfterCollect(out bytesPrivateWarm);
					}
					//Mostly the module and printout sizes, now and then the two sheet sizes: four sizes, one more than the renderer keeps, so the
					//sheet sizes' targets are freed and made again about twenty times.
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
				//Bounded memory over the 450 warm images (an x86 process): a leak of the freed sizes' targets (about 8 MB a time at 640x320) or of a
				//composite per image (0.5 MB) would pass these limits several times over.
				long bytesPrivateEnd;
				long bytesManagedEnd=GetMemoryAfterCollect(out bytesPrivateEnd);
				double mbPrivateGrowth=(bytesPrivateEnd-bytesPrivateWarm)/1048576.0;
				double mbManagedGrowth=(bytesManagedEnd-bytesManagedWarm)/1048576.0;
				WpfCharts.WriteEvidence("render-loop-memory.txt","Memory after image 50 and after image 500: private bytes "+(bytesPrivateWarm/1048576.0).ToString("0.0")+" MB and "
					+(bytesPrivateEnd/1048576.0).ToString("0.0")+" MB ("+mbPrivateGrowth.ToString("+0.0;-0.0")+" MB), managed heap "
					+(bytesManagedWarm/1048576.0).ToString("0.0")+" MB and "+(bytesManagedEnd/1048576.0).ToString("0.0")+" MB ("+mbManagedGrowth.ToString("+0.0;-0.0")+" MB).");
				Assert.IsTrue(mbPrivateGrowth<48,"Private bytes grew by "+mbPrivateGrowth.ToString("0.0")+" MB over 450 warm images.");
				Assert.IsTrue(mbManagedGrowth<16,"The managed heap grew by "+mbManagedGrowth.ToString("0.0")+" MB over 450 warm images.");
			}
			finally {
				listWrappers.ForEach(x => x.Dispose());
			}
			Assert.AreEqual(0,ToothChartWrapper.GetFallbackMessagesSince(countMessages).Count,"No render failed: "
				+string.Join("; ",ToothChartWrapper.GetFallbackMessagesSince(countMessages)));
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
			int countMessages=ToothChartWrapper.GetCountFallbackMessages();
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
				Assert.AreEqual(0,toothChartImageHost.CountReceiveCalls,"The disposed host got no call: nothing reached its UI thread.");
				Assert.AreEqual(0,toothChartImageHost.CountImagesReceived,"The disposed host got no image.");
			});
			Assert.AreEqual(0,ToothChartWrapper.GetFallbackMessagesSince(countMessages).Count,"Nothing was logged: "
				+string.Join("; ",ToothChartWrapper.GetFallbackMessagesSince(countMessages)));
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
			//And the bytes of the committed baseline (the same scene, size and colours, the same hash of GetPixelsArgb), whose .txt names the screen
			//DPI it was recorded at (100%): a run with the desktop at 150% (launch check L-2) that passes has the bytes of the 100% run.
			string pathBaselineText=Path.ChangeExtension(WpfCharts.GetPicturePath(WpfCharts.BaselinesWpf,ChartScenes.SizeModule,ChartScenes.Get("restored-adult")),".txt");
			string hashBaseline=LegacyComparison.ReadPixelsSha256(pathBaselineText);
			int dpiScreen=GetScreenDpi();
			WpfCharts.WriteEvidence("determinism.txt","restored-adult at "+ChartScenes.SizeModule.FolderName+", module colours: pixels SHA-256 "+hashFirst
				+" in this process and in a second one; the committed baseline's is "+hashBaseline+"; the screen DPI seen by the test host is "+dpiScreen+" ("
				+(dpiScreen*100/96)+"%).");
			Assert.IsNotNull(hashBaseline,"No Pixels SHA-256 in "+pathBaselineText);
			Assert.AreEqual(hashBaseline,hashFirst,"The same bytes as the committed baseline, here at "+dpiScreen+" dpi.");
		}

		///<summary>The screen DPI the test host sees, as the recordings write it.</summary>
		public static int GetScreenDpi() {
			using(Graphics g=Graphics.FromHwnd(IntPtr.Zero)) {
				return (int)Math.Round(g.DpiX);
			}
		}

		[TestMethod]
		public void HiddenWrapper_RefillThenGetBitmap_ShowsTheRefillWhenShownAgain() {
			//A GetBitmap clears the scene's dirty mark, but not the screen's: a wrapper refilled while hidden, then asked for its bitmap (as the
			//dashboard does), must still rebuild its screen image when it is shown again, or it keeps the previous chart.
			if(!Environment.UserInteractive) {
				Assert.Inconclusive("Needs an interactive desktop session: the wrapper must be shown.");
			}
			StaRunner.Run(() => {
				using(Form form=WpfCharts.CreateOffscreenForm(ChartScenes.SizeModule.Size)) {
					ToothChartWrapper wrapper=WpfCharts.CreateWrapper(ChartScenes.SizeModule.Size);
					form.Controls.Add(wrapper);
					form.Show();
					ToothChartImageHost toothChartImageHost=wrapper.ImageHostWpf;
					ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
					ChartScenes.Prepare(relay,ChartScenes.Get("restored-adult"),ChartColorSet.Module);
					Assert.IsTrue(PumpUntil(() => toothChartImageHost.CountImagesReceived>=1 && !toothChartImageHost.IsRenderInFlight,20000),"A first image arrived.");
					form.Hide();
					Assert.IsFalse(wrapper.Visible);
					int countRenders=toothChartImageHost.CountRendersStarted;
					int countImages=toothChartImageHost.CountImagesReceived;
					ChartScenes.Prepare(relay,ChartScenes.Get("treatment-plan"),ChartColorSet.Module);
					PumpUntil(() => false,200);
					Assert.AreEqual(countRenders,toothChartImageHost.CountRendersStarted,"A hidden wrapper posts no rebuild.");
					using(Bitmap bitmap=relay.GetBitmap()) {
						Assert.IsFalse(wrapper.IsWpfSceneDirty,"GetBitmap built the scene.");
						form.Show();
						Assert.IsTrue(PumpUntil(() => toothChartImageHost.CountImagesReceived>countImages && !toothChartImageHost.IsRenderInFlight,20000),
							"Shown again, the wrapper rebuilt its screen image.");
						using(Bitmap bitmapShown=toothChartImageHost.GetBitmapShown()) {
							Assert.IsTrue(ChartImageCompare.GetPixelsArgb(bitmap).SequenceEqual(ChartImageCompare.GetPixelsArgb(bitmapShown)),
								"The screen shows the refill, the same image as GetBitmap's.");
						}
					}
					form.Close();
				}
			});
		}

		[TestMethod]
		public void Wrapper_WhosePostedRebuildWentWithItsHandle_RebuildsWhenTheHandleComesBack() {
			//DestroyHandle (not RecreateHandle) drops the BeginInvoke of a posted rebuild; the wrapper must not then believe a rebuild is still
			//posted, or it never posts one again when it is shown again (a reused window: destroyed, not disposed, then shown).
			if(!Environment.UserInteractive) {
				Assert.Inconclusive("Needs an interactive desktop session: the wrapper must be shown.");
			}
			StaRunner.Run(() => {
				using(Form form=WpfCharts.CreateOffscreenForm(ChartScenes.SizeModule.Size)) {
					TestWrapper wrapper=new TestWrapper();
					wrapper.Size=ChartScenes.SizeModule.Size;
					wrapper.Engine=ToothChartEngine.Wpf3D;
					form.Controls.Add(wrapper);
					form.Show();
					ToothChartImageHost toothChartImageHost=wrapper.ImageHostWpf;
					ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
					ChartScenes.Prepare(relay,ChartScenes.Get("restored-adult"),ChartColorSet.Module);
					Assert.IsTrue(PumpUntil(() => toothChartImageHost.CountImagesReceived>=1 && !toothChartImageHost.IsRenderInFlight,20000),"A first image arrived.");
					relay.SetSelected("8",true);//posts a rebuild; nothing pumps before the handle goes
					wrapper.DestroyHandleForTests();
					Assert.IsFalse(wrapper.IsHandleCreated);
					wrapper.CreateHandleForTests();
					Assert.IsTrue(wrapper.IsHandleCreated && toothChartImageHost.IsHandleCreated);
					//DestroyWindow hid the wrapper, so WinForms reads it as not visible: a window destroyed and not disposed is shown again.
					wrapper.Visible=true;
					toothChartImageHost.Visible=true;
					int countImages=toothChartImageHost.CountImagesReceived;
					int countRenders=toothChartImageHost.CountRendersStarted;
					relay.SetSelected("9",true);
					Assert.IsTrue(PumpUntil(() => toothChartImageHost.CountImagesReceived>countImages && !toothChartImageHost.IsRenderInFlight,20000),
						"With its handle back, the wrapper rebuilds its screen image again. Renders started "+countRenders+" then "
						+toothChartImageHost.CountRendersStarted+", results received "+toothChartImageHost.CountReceiveCalls+", images "+countImages+" then "
						+toothChartImageHost.CountImagesReceived+", in flight "+toothChartImageHost.IsRenderInFlight+", scene dirty "+wrapper.IsWpfSceneDirty
						+", wrapper visible "+wrapper.Visible+", handles "+wrapper.IsHandleCreated+" and "+toothChartImageHost.IsHandleCreated+".");
					using(Bitmap bitmap=relay.GetBitmap())
					using(Bitmap bitmapShown=toothChartImageHost.GetBitmapShown()) {
						Assert.IsTrue(ChartImageCompare.GetPixelsArgb(bitmap).SequenceEqual(ChartImageCompare.GetPixelsArgb(bitmapShown)),"The screen shows both selections.");
					}
					form.Close();
				}
			});
		}

		[TestMethod]
		public void Host_HandleDestroyedDuringItsRender_RendersAgainWhenTheHandleComesBack() {
			//The render's result finds no handle and is dropped; the host must not stay "in flight" for good.
			ToothScene toothScene=WpfCharts.BuildScene("restored-adult",ChartScenes.SizeModule);
			ToothScene toothSceneNext=WpfCharts.BuildScene("treatment-plan",ChartScenes.SizeModule);
			StaRunner.Run(() => {
				using(TestImageHost toothChartImageHost=new TestImageHost()) {
					toothChartImageHost.Size=toothScene.Size;
					toothChartImageHost.CreateControl();
					using(RenderJobHold renderJobHold=new RenderJobHold(ToothChartRenderer.JobRender)) {
						toothChartImageHost.ShowScene(toothScene);
						Assert.IsTrue(renderJobHold.WaitEntered(30000),"The host's render is running.");
						toothChartImageHost.DestroyHandleForTests();
						Assert.IsFalse(toothChartImageHost.IsHandleCreated);
					}
					ToothChartRenderer.Instance.WaitIdle();//the render has run and found no handle
					PumpUntil(() => false,200);
					Assert.AreEqual(0,toothChartImageHost.CountReceiveCalls,"The result had nowhere to go.");
					toothChartImageHost.CreateHandleForTests();
					Assert.IsTrue(PumpUntil(() => toothChartImageHost.CountImagesReceived>=1 && !toothChartImageHost.IsRenderInFlight,20000),
						"With its handle back, the host renders its scene again.");
					Assert.AreSame(toothScene,toothChartImageHost.SceneShown);
					toothChartImageHost.ShowScene(toothSceneNext);
					Assert.IsTrue(PumpUntil(() => toothChartImageHost.SceneShown==toothSceneNext && !toothChartImageHost.IsRenderInFlight,20000),
						"The next scene is shown too.");
				}
			});
		}

		[TestMethod]
		public void Host_PaintsItsImagePixelForPixel_WhateverTheDpiOfTheGraphics() {
			//DrawImageUnscaled draws at the image's physical size, which differs from its pixel size when the bitmap's DPI is not the Graphics' DPI
			//(a drag between monitors, L-2).
			ToothScene toothScene=WpfCharts.BuildScene("restored-adult",ChartScenes.SizeModule);
			StaRunner.Run(() => {
				using(TestImageHost toothChartImageHost=new TestImageHost()) {
					toothChartImageHost.Size=toothScene.Size;
					toothChartImageHost.CreateControl();
					toothChartImageHost.ShowScene(toothScene);
					Assert.IsTrue(PumpUntil(() => toothChartImageHost.CountImagesReceived>=1 && !toothChartImageHost.IsRenderInFlight,20000),"An image arrived.");
					using(Bitmap bitmapShown=toothChartImageHost.GetBitmapShown()) {
						byte[] arrayPixelsShown=ChartImageCompare.GetPixelsArgb(bitmapShown);
						foreach(float dpi in new[] { 96f,144f,192f,72f }) {
							using(Bitmap bitmapTarget=new Bitmap(toothScene.Size.Width,toothScene.Size.Height,PixelFormat.Format24bppRgb)) {
								bitmapTarget.SetResolution(dpi,dpi);
								using(Graphics g=Graphics.FromImage(bitmapTarget)) {
									toothChartImageHost.PaintForTests(g);
								}
								Assert.IsTrue(arrayPixelsShown.SequenceEqual(ChartImageCompare.GetPixelsArgb(bitmapTarget)),"Painted at "+dpi+" dpi, pixel for pixel.");
							}
						}
					}
				}
			});
		}

		[TestMethod]
		public void Restart_AfterAShutdownWhoseJoinTimedOut_LeavesTheOldThreadsJobAlone() {
			//Shutdown waits only so long; a new render thread may start while the old one is still in its job. Each must keep its own targets and
			//visual tree, or the old thread draws with the new thread's objects (or the new one with the old one's) and fails.
			ToothScene toothScene=WpfCharts.BuildScene("restored-adult",ChartScenes.SizeModule);
			ToothChartRenderer toothChartRenderer=ToothChartRenderer.Instance;
			int threadIdOld=toothChartRenderer.Render(toothScene,null).ThreadIdRender;//the old thread, with its visual tree
			ToothChartRenderResult resultOld=null;
			ToothChartRenderResult resultNew;
			using(ManualResetEvent manualResetEventOldDone=new ManualResetEvent(false)) {
				using(RenderJobHold renderJobHold=new RenderJobHold(ToothChartRenderer.JobRender)) {
					toothChartRenderer.RenderAsync(toothScene,null,result => {
						resultOld=result;
						manualResetEventOldDone.Set();
					});
					Assert.IsTrue(renderJobHold.WaitEntered(30000),"The old thread is in its render job.");
					toothChartRenderer.Shutdown(0);//its Join times out at once
					resultNew=toothChartRenderer.Render(toothScene,null);//a new thread starts and renders while the old one is still in its job
					Assert.AreNotEqual(threadIdOld,resultNew.ThreadIdRender,"A new render thread drew.");
					renderJobHold.Release();
					Assert.IsTrue(manualResetEventOldDone.WaitOne(30000),"The old thread finished its job.");
				}
			}
			Assert.IsNull(resultOld.Exception,"The old thread drew its job with its own objects: "+resultOld.Exception);
			Assert.AreEqual(threadIdOld,resultOld.ThreadIdRender);
			Assert.IsTrue(resultOld.PixelsBgra.SequenceEqual(resultNew.PixelsBgra),"Both threads drew the same image.");
			ToothChartRenderResult resultAfter=toothChartRenderer.Render(toothScene,null);
			Assert.AreEqual(resultNew.ThreadIdRender,resultAfter.ThreadIdRender);
			Assert.IsTrue(resultAfter.PixelsBgra.SequenceEqual(resultNew.PixelsBgra),"The new thread still draws.");
		}

		[TestMethod]
		public void Shutdown_CompletesTheWaitersOfTheJobsItAborts_AtOnce() {
			//Shutdown aborts the queued jobs. A caller waiting in Render must hear of it at once, not after MillisecondsTimeout, and RenderAsync's
			//callback must still run, or an image host stays "in flight" for good.
			ToothScene toothScene=WpfCharts.BuildScene("restored-adult",ChartScenes.SizeModule);
			ToothChartRenderer toothChartRenderer=ToothChartRenderer.Instance;
			toothChartRenderer.Render(toothScene,null);
			int millisecondsTimeoutOld=ToothChartRenderer.MillisecondsTimeout;
			ToothChartRenderer.MillisecondsTimeout=20000;
			ToothChartRenderResult resultAsync=null;
			Exception exceptionWaiter=null;
			long millisecondsWaiter=-1;
			bool isAsyncDone;
			Thread threadWaiter;
			try {
				using(ManualResetEvent manualResetEventAsyncDone=new ManualResetEvent(false)) {
					using(RenderJobHold renderJobHold=new RenderJobHold(ToothChartRenderer.JobRender)) {
						toothChartRenderer.RenderAsync(toothScene,null,null);//the job that holds the thread
						Assert.IsTrue(renderJobHold.WaitEntered(30000));
						toothChartRenderer.RenderAsync(toothScene,null,result => {
							resultAsync=result;
							manualResetEventAsyncDone.Set();
						});
						threadWaiter=new Thread(() => {
							Stopwatch stopwatch=Stopwatch.StartNew();
							try {
								toothChartRenderer.Render(toothScene,null);
							}
							catch(Exception ex) {
								exceptionWaiter=ex;
							}
							millisecondsWaiter=stopwatch.ElapsedMilliseconds;
						});
						threadWaiter.IsBackground=true;
						threadWaiter.Start();
						Assert.IsTrue(WaitUntil(() => (threadWaiter.ThreadState & System.Threading.ThreadState.WaitSleepJoin)!=0,10000),"The waiter queued its job and waits.");
						toothChartRenderer.Shutdown(0);
						renderJobHold.Release();
					}
					isAsyncDone=manualResetEventAsyncDone.WaitOne(10000);
				}
				Assert.IsTrue(threadWaiter.Join(60000));
			}
			finally {
				ToothChartRenderer.MillisecondsTimeout=millisecondsTimeoutOld;
			}
			Assert.IsTrue(isAsyncDone,"RenderAsync's callback ran for the aborted job.");
			Assert.IsNotNull(resultAsync.Exception,"With a failed result.");
			Assert.IsNotNull(exceptionWaiter,"The waiter's Render threw.");
			Assert.IsTrue(millisecondsWaiter<10000,"The waiter heard of the abort at once, not after the timeout: "+millisecondsWaiter+" ms, "+exceptionWaiter);
			Assert.IsFalse(exceptionWaiter is TimeoutException,exceptionWaiter.ToString());
			Assert.AreEqual(toothScene.Size,toothChartRenderer.Render(toothScene,null).Size,"The next render starts a new thread.");
		}

		[TestMethod]
		public void Host_WhoseRenderWasAbortedByAShutdown_ShowsTheNextScene() {
			ToothScene toothScene=WpfCharts.BuildScene("restored-adult",ChartScenes.SizeModule);
			ToothScene toothSceneNext=WpfCharts.BuildScene("treatment-plan",ChartScenes.SizeModule);
			ToothChartRenderer.Instance.Render(toothScene,null);
			StaRunner.Run(() => {
				using(TestImageHost toothChartImageHost=new TestImageHost()) {
					toothChartImageHost.Size=toothScene.Size;
					toothChartImageHost.CreateControl();
					using(RenderJobHold renderJobHold=new RenderJobHold(ToothChartRenderer.JobRender)) {
						ToothChartRenderer.Instance.RenderAsync(toothScene,null,null);//the job that holds the thread
						Assert.IsTrue(renderJobHold.WaitEntered(30000));
						toothChartImageHost.ShowScene(toothScene);//queued behind it
						Assert.IsTrue(toothChartImageHost.IsRenderInFlight);
						ToothChartRenderer.Instance.Shutdown(0);
						renderJobHold.Release();
					}
					Assert.IsTrue(PumpUntil(() => !toothChartImageHost.IsRenderInFlight,15000),"The aborted render came back, so the host is not stuck.");
					toothChartImageHost.ShowScene(toothSceneNext);
					Assert.IsTrue(PumpUntil(() => toothChartImageHost.SceneShown==toothSceneNext && !toothChartImageHost.IsRenderInFlight,20000),
						"The next scene is shown, drawn by a new render thread.");
				}
			});
		}

		[TestMethod]
		public void ColdStart_HoldsNoLockThatAnotherCallerWaitsFor() {
			//The start check waits for WPF's cold start. A caller that only queues a render (the UI thread's ShowScene) or reads the thread must
			//not wait for it.
			ToothScene toothScene=WpfCharts.BuildScene("restored-adult",ChartScenes.SizeModule);
			ToothChartRenderer.Instance.Shutdown(30000);//the next EnsureStarted checks again, on a new thread
			Exception exceptionStart=null;
			bool isOtherDone;
			bool isRendered;
			using(ManualResetEvent manualResetEventRendered=new ManualResetEvent(false)) {
				using(RenderJobHold renderJobHold=new RenderJobHold(ToothChartRenderer.JobCheck)) {
					Thread threadStart=new Thread(() => {
						try {
							ToothChartRenderer.Instance.EnsureStarted();
						}
						catch(Exception ex) {
							exceptionStart=ex;
						}
					});
					threadStart.IsBackground=true;
					threadStart.Start();
					Assert.IsTrue(renderJobHold.WaitEntered(30000),"The start check is running.");
					Thread threadOther=new Thread(() => {
						int threadId=ToothChartRenderer.Instance.ThreadId;
						ToothChartRenderer.Instance.RenderAsync(toothScene,null,result => manualResetEventRendered.Set());
					});
					threadOther.IsBackground=true;
					threadOther.Start();
					isOtherDone=threadOther.Join(5000);
					renderJobHold.Release();
					Assert.IsTrue(threadStart.Join(60000));
					Assert.IsTrue(threadOther.Join(60000));
				}
				isRendered=manualResetEventRendered.WaitOne(30000);
			}
			Assert.IsTrue(isOtherDone,"The other caller waited for the start check.");
			Assert.IsNull(exceptionStart,"The start check passed: "+exceptionStart);
			Assert.IsTrue(isRendered,"The queued render ran after the start check.");
		}

		[TestMethod]
		public void RepeatedRenderFailures_StartANewRenderThread_AndTheNextRenderWorks() {
			ToothScene toothScene=WpfCharts.BuildScene("restored-adult",ChartScenes.SizeModule);
			ToothChartRenderer toothChartRenderer=ToothChartRenderer.Instance;
			ToothChartRenderResult resultBefore=toothChartRenderer.Render(toothScene,null);
			int countUnhandled=ToothChartRenderer.CountUnhandledExceptions;
			int countResets=toothChartRenderer.CountResetsAfterFailures;
			int countRestarts=toothChartRenderer.CountRestartsAfterFailures;
			Assert.IsTrue(countRestarts<ToothChartRenderer.MaxRestartsAfterFailures,"This process may still restart its render thread.");
			ToothChartRenderer.ActionJobStartForTests=kind => {
				if(kind==ToothChartRenderer.JobRender) {
					throw new InvalidOperationException("A forced render failure.");
				}
			};
			try {
				for(int i=1;i<=ToothChartRenderer.FailuresInRowBeforeRestart;i++) {
					Expect.Throws<InvalidOperationException>(() => toothChartRenderer.Render(toothScene,null));
					if(i==ToothChartRenderer.FailuresInRowBeforeReset) {
						Assert.AreEqual(countResets+1,toothChartRenderer.CountResetsAfterFailures,"After three failures in a row, the visual tree is made again.");
						Assert.AreEqual(resultBefore.ThreadIdRender,toothChartRenderer.ThreadId,"The same thread so far.");
					}
				}
			}
			finally {
				ToothChartRenderer.ActionJobStartForTests=null;
			}
			Assert.AreEqual(countRestarts+1,toothChartRenderer.CountRestartsAfterFailures);
			ToothChartRenderResult resultAfter=toothChartRenderer.Render(toothScene,null);
			Assert.AreNotEqual(resultBefore.ThreadIdRender,resultAfter.ThreadIdRender,"After six failed renders in a row, a new render thread draws.");
			Assert.IsTrue(resultAfter.PixelsBgra.SequenceEqual(resultBefore.PixelsBgra),"And it draws the same image.");
			Assert.AreEqual(countUnhandled,ToothChartRenderer.CountUnhandledExceptions);
		}

		[TestMethod]
		public void Start_InterruptedCaller_IsNoPermanentStartFailure() {
			SecondProcessResult result=SecondProcess.Run(120000,"start-interrupted");
			Console.WriteLine(result.Output);
			Assert.AreEqual(0,result.ExitCode,result.Output);
			StringAssert.Contains(result.Output,"caller interrupted: True");
			StringAssert.Contains(result.Output,"started afterwards: True","A caller's ThreadInterruptedException is not kept as the start failure.");
			StringAssert.Contains(result.Output,"engine: Wpf3D");
		}

		[TestMethod]
		public void Start_Failure_FallsBackToSimple2D_WithOneLogLine() {
			SecondProcessResult result=SecondProcess.Run(120000,"start-fails");
			Console.WriteLine(result.Output);
			Assert.AreEqual(0,result.ExitCode,result.Output);
			StringAssert.Contains(result.Output,"engine 1: Simple2D");
			StringAssert.Contains(result.Output,"engine 2: Simple2D");
			StringAssert.Contains(result.Output,"bitmap: 410x307, blank False");
			StringAssert.Contains(result.Output,"start checks run: 1","The start failure is kept: the second wrapper does not check again.");
			StringAssert.Contains(result.Output,"fallback messages: 2");
			StringAssert.Contains(result.Output,"log lines: 1","Two wrappers fell back, and one line was written.");
			StringAssert.Contains(result.Output,"message: The Wpf3D tooth chart could not start (InvalidOperationException: ");
			StringAssert.Contains(result.Output,"A forced start failure.");
			StringAssert.Contains(result.Output,"). Using Simple2D.");
		}

		[TestMethod]
		public void LogLines_OfWpfRenderFailures_AreWrittenEvenAfterADirectXFallbackLine() {
			SecondProcessResult result=SecondProcess.Run(120000,"log-kinds");
			Console.WriteLine(result.Output);
			Assert.AreEqual(0,result.ExitCode,result.Output);
			StringAssert.Contains(result.Output,"log lines after a DirectX fallback: 1");
			StringAssert.Contains(result.Output,"log lines after a WPF render failure: 2","The WPF render failure has its own logged-once line.");
			StringAssert.Contains(result.Output,"log lines after a second WPF render failure: 2","And only one.");
		}

		///<summary>The managed heap after a full collection with the finalizers run (WPF's render targets free their native memory in theirs), and
		///the process's private bytes then.</summary>
		private static long GetMemoryAfterCollect(out long bytesPrivate) {
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
			long bytesManaged=GC.GetTotalMemory(true);
			using(Process process=Process.GetCurrentProcess()) {
				bytesPrivate=process.PrivateMemorySize64;
			}
			return bytesManaged;
		}

		///<summary>Waits on the calling thread, without pumping, until the condition holds or the time is up.</summary>
		public static bool WaitUntil(Func<bool> funcCondition,int millisecondsTimeout) {
			Stopwatch stopwatch=Stopwatch.StartNew();
			while(stopwatch.ElapsedMilliseconds<millisecondsTimeout) {
				if(funcCondition()) {
					return true;
				}
				Thread.Sleep(5);
			}
			return funcCondition();
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
