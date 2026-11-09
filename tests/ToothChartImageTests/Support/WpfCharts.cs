// Helpers for the WPF chart's tests (package TC-1): wrappers on the Wpf3D engine, scenes drawn through the relay, the folders of the WPF baselines,
// the failure of a GetBitmap that fell back to the 2D chart, and a forced render failure.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using OpenPmsDental;
using SparksToothChart;
using SparksToothChart.Wpf3D;

namespace ToothChartImageTests {
	public static class WpfCharts {
		///<summary>The folder of the WPF chart's baselines on .NET Framework 4.8 x86: tests\ToothChartImageTests\Baselines\wpf-net48.</summary>
		public static string BaselinesWpf {
			get {
				return Path.Combine(TestPaths.ProjectFolder,"Baselines","wpf-net48");
			}
		}

		///<summary>A wrapper of the given size on the Wpf3D engine, as a caller would make one: size first, then the engine. Not shown.</summary>
		public static ToothChartWrapper CreateWrapper(Size size) {
			ToothChartWrapper wrapper=new ToothChartWrapper();
			wrapper.Size=size;
			wrapper.Engine=ToothChartEngine.Wpf3D;
			return wrapper;
		}

		public static ToothChartRelay CreateRelay(ToothChartWrapper wrapper) {
			ToothChartRelay relay=new ToothChartRelay();
			relay.SetToothChartWrapper(wrapper);
			return relay;
		}

		///<summary>The chart scenes the WPF chart draws in TC-1: every scene of kind Chart. The perio scenes need perio mode, which the wrapper allows
		///only in DirectX until TC-7.</summary>
		public static List<ChartScene> GetChartScenes() {
			return ChartScenes.GetAll().Where(x => x.Kind==ChartSceneKind.Chart).ToList();
		}

		///<summary>Every chart size of the chart scenes, standard sizes first, then the extra sizes in scene order (the recorder's order).</summary>
		public static List<ChartSize> GetChartSizes() {
			List<ChartSize> listSizes=new List<ChartSize>();
			foreach(ChartSize chartSize in ChartScenes.GetStandardSizes(ChartSceneKind.Chart).Concat(GetChartScenes().SelectMany(x => x.ListSizesExtra))) {
				if(!listSizes.Contains(chartSize)) {
					listSizes.Add(chartSize);
				}
			}
			return listSizes;
		}

		///<summary>Prepares the scene through the relay (ChartScenes.Prepare, as the program's callers do) and returns the wrapper's GetBitmap.</summary>
		public static Bitmap Draw(ToothChartRelay relay,ChartScene scene,ChartColorSet chartColorSet) {
			ChartScenes.Prepare(relay,scene,chartColorSet);
			return relay.GetBitmap();
		}

		///<summary>The scene the wrapper's data gives now (what GetBitmap renders).</summary>
		public static ToothScene BuildScene(ToothChartWrapper wrapper) {
			return ToothSceneBuilder.Build(wrapper.TcData);
		}

		///<summary>The path of a scene's WPF picture under a folder: &lt;folder&gt;\&lt;size&gt;\&lt;scene&gt;.png.</summary>
		public static string GetPicturePath(string folder,ChartSize chartSize,ChartScene scene) {
			return Path.Combine(folder,chartSize.FolderName,scene.Name+".png");
		}

		///<summary>Null when no fallback message was added since countBefore (ToothChartWrapper.GetCountFallbackMessages, taken before a GetBitmap);
		///otherwise the failure "&lt;name&gt;: the WPF render failed and GetBitmap returned the Simple 2D fallback: &lt;messages&gt;". A render that fails
		///leaves the wrapper's Engine at Wpf3D and returns the 2D chart's image, so only the messages tell that the picture is not the WPF chart's.</summary>
		public static string GetFallbackFailure(string name,int countBefore) {
			List<string> listMessages=ToothChartWrapper.GetFallbackMessagesSince(countBefore);
			if(listMessages.Count==0) {
				return null;
			}
			return name+": the WPF render failed and GetBitmap returned the Simple 2D fallback: "+string.Join("; ",listMessages);
		}

		///<summary>A forced render failure, for tests: holds the render thread in a job and sets ToothChartRenderer.MillisecondsTimeout to
		///millisecondsTimeout, so every render until Dispose times out and the wrapper's GetBitmap returns the Simple 2D chart's image. Make the
		///wrappers first (starting the engine renders a check image the first time in the process). scene is the holding job's scene.</summary>
		public static RenderThreadHold HoldRenderThread(ToothScene scene,int millisecondsTimeout) {
			return new RenderThreadHold(scene,millisecondsTimeout);
		}

		///<summary>The scene of a chart scene at a size and colour set, prepared through the relay on a 2D wrapper (the data is the same in every
		///engine), on an STA thread of its own.</summary>
		public static ToothScene BuildScene(string sceneName,ChartSize chartSize) {
			return StaRunner.Run(() => {
				using(ToothChartWrapper wrapper=new ToothChartWrapper()) {
					wrapper.Size=chartSize.Size;
					ChartScenes.Prepare(CreateRelay(wrapper),ChartScenes.Get(sceneName),chartSize.ColorSet);
					return ToothSceneBuilder.Build(wrapper.TcData);
				}
			});
		}

		///<summary>Prints a test's evidence line and keeps it in %TEMP%\ToothChartImageTests\&lt;fileName&gt; (as render-times-in-process.txt), because a
		///test runner shows a passing test's output only at a detailed verbosity. L-2's 150% run compares its determinism.txt with the 100% run's.</summary>
		public static void WriteEvidence(string fileName,string text) {
			Console.WriteLine(text);
			try {
				string folder=Path.Combine(Path.GetTempPath(),"ToothChartImageTests");
				Directory.CreateDirectory(folder);
				File.WriteAllText(Path.Combine(folder,fileName),text+"\r\n",new System.Text.UTF8Encoding(false));
			}
			catch(Exception ex) {
				Console.WriteLine("Not kept in a file: "+ex.Message);
			}
		}

		///<summary>Runs the shown wrapper's posted rebuild, which makes its live view (TC-3 fix round 3: never before the message loop runs) and shows the
		///scene in it, and returns the view once it shows a scene with the current data; null when no live view came within millisecondsTimeout.</summary>
		public static ToothChartWpfView WaitForLiveView(ToothChartWrapper wrapper,int millisecondsTimeout=20000) {
			WpfRendererTests.PumpUntil(() => wrapper.LiveViewWpf!=null && wrapper.LiveViewWpf.SceneShown!=null && !wrapper.IsWpfSceneDirty,millisecondsTimeout);
			ToothChartWpfView toothChartWpfView=wrapper.LiveViewWpf;
			if(toothChartWpfView==null || toothChartWpfView.SceneShown==null) {
				return null;
			}
			return toothChartWpfView;
		}

		///<summary>A borderless form off every screen but shown, for the tests that need a wrapper on screen (an interactive desktop session).</summary>
		public static System.Windows.Forms.Form CreateOffscreenForm(Size sizeClient) {
			System.Windows.Forms.Form form=new System.Windows.Forms.Form();
			form.FormBorderStyle=System.Windows.Forms.FormBorderStyle.None;
			form.ShowInTaskbar=false;
			form.StartPosition=System.Windows.Forms.FormStartPosition.Manual;
			form.Location=new Point(-32000,-32000);
			form.ClientSize=sizeClient;
			return form;
		}
	}

	///<summary>Holds the render thread inside the first job of one kind (ToothChartRenderer.JobCheck or JobRender) that starts after it is made, through
	///ToothChartRenderer.ActionJobStartForTests: WaitEntered says when the job is held, and Release lets it go on. Later jobs pass. Dispose removes
	///the hook and releases the job.</summary>
	public sealed class RenderJobHold:IDisposable {
		private readonly ManualResetEvent _manualResetEventEntered=new ManualResetEvent(false);
		private readonly ManualResetEvent _manualResetEventRelease=new ManualResetEvent(false);
		private int _isTaken;

		public RenderJobHold(string jobKind) {
			ToothChartRenderer.ActionJobStartForTests=kind => {
				if(kind!=jobKind || Interlocked.Exchange(ref _isTaken,1)==1) {
					return;
				}
				_manualResetEventEntered.Set();
				_manualResetEventRelease.WaitOne();
			};
		}

		public bool WaitEntered(int millisecondsTimeout) {
			return _manualResetEventEntered.WaitOne(millisecondsTimeout);
		}

		public void Release() {
			_manualResetEventRelease.Set();
		}

		public void Dispose() {
			ToothChartRenderer.ActionJobStartForTests=null;
			_manualResetEventRelease.Set();
		}
	}

	///<summary>The image host with its protected handle and paint calls opened for the tests.</summary>
	public class TestImageHost:ToothChartImageHost {
		public void DestroyHandleForTests() {
			DestroyHandle();
		}

		///<summary>Makes the handle again after DestroyHandleForTests.</summary>
		public void CreateHandleForTests() {
			if(!IsHandleCreated) {
				CreateHandle();
			}
		}

		///<summary>Paints the control into g as WM_PAINT would.</summary>
		public void PaintForTests(Graphics g) {
			OnPaint(new System.Windows.Forms.PaintEventArgs(g,ClientRectangle));
		}
	}

	///<summary>The wrapper with its protected handle calls opened for the tests.</summary>
	public class TestWrapper:ToothChartWrapper {
		public void DestroyHandleForTests() {
			DestroyHandle();
		}

		///<summary>Makes the handles of the wrapper and its engine control again after DestroyHandleForTests.</summary>
		public void CreateHandleForTests() {
			if(!IsHandleCreated) {
				CreateHandle();
			}
			foreach(System.Windows.Forms.Control control in Controls) {
				if(!control.IsHandleCreated) {
					control.CreateControl();
				}
				if(!control.IsHandleCreated) {
					IntPtr intPtrHandle=control.Handle;
				}
			}
		}
	}

	///<summary>See WpfCharts.HoldRenderThread. Dispose restores the timeout, frees the render thread and waits until the jobs queued in between
	///(the renders that timed out) have run.</summary>
	public sealed class RenderThreadHold:IDisposable {
		private readonly ManualResetEvent _manualResetEventRelease=new ManualResetEvent(false);
		private readonly int _millisecondsTimeoutOld;
		private bool _isDisposed;

		public RenderThreadHold(ToothScene scene,int millisecondsTimeout) {
			using(ManualResetEvent manualResetEventHeld=new ManualResetEvent(false)) {
				ToothChartRenderer.Instance.RenderAsync(scene,null,result => {
					manualResetEventHeld.Set();
					_manualResetEventRelease.WaitOne();
				});
				if(!manualResetEventHeld.WaitOne(ToothChartRenderer.MillisecondsTimeout)) {
					_manualResetEventRelease.Set();
					throw new TimeoutException("The render thread did not start the holding job.");
				}
			}
			_millisecondsTimeoutOld=ToothChartRenderer.MillisecondsTimeout;
			ToothChartRenderer.MillisecondsTimeout=millisecondsTimeout;
		}

		public void Dispose() {
			if(_isDisposed) {
				return;
			}
			_isDisposed=true;
			ToothChartRenderer.MillisecondsTimeout=_millisecondsTimeoutOld;
			_manualResetEventRelease.Set();
			ToothChartRenderer.Instance.WaitIdle();
			_manualResetEventRelease.Close();
		}
	}
}
