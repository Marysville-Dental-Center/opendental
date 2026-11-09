// The on-screen render times of TC-3 (docs/specs/wpf-tooth-chart-plan.md, TC-3's row and gallery G1): a refill plus the screen's update, with
// the Patient Dashboard visible (the refill also makes the dashboard's image, ControlChart's GetBitmap) and hidden, and a selection-only change,
// for today's DirectX chart, the WPF chart's live view and the WPF chart drawn in software; and the live view's frame, which WPF's render thread
// draws after the UI thread's work, in software as the program draws it (O-34).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.Windows.Threading;
using OpenPmsDental;
using SparksToothChart;
using SparksToothChart.Wpf3D;

namespace ToothChartImageTests {
	///<summary>Each engine on a shown, off-screen form, the way the Chart module uses its chart. "Refill" is ChartScenes.Prepare through the relay
	///(as ControlChart.FillToothChart), then the screen's update: DirectX paints at once (Refresh, which renders and presents); the WPF chart runs
	///its posted rebuild (the scene, then the live view's frozen models and layout, or the software render of the image host until its image
	///arrives). "Dashboard" adds the refill's GetBitmap for the Patient Dashboard (ControlChart makes it only while a dashboard is shown). Warm values
	///are medians of runs alternating two scenes, so the 3D layer changes every time, after CountWarmUp unmeasured runs of each kind for each engine
	///and size (TC-3's first DirectX medians were warm-up noise: 47 ms at 410x307, measured first, against 32 ms at 615x461). DirectX draws with the
	///device format the program picks on a computer with none saved (ToothChartWrapper.GetPreferredDirectXFormat: the first standard format that
	///works, multisampled where the driver allows), not TC-0's unsmoothed recording format. Those live-view times are the UI thread's work only.
	///The live view's frame is drawn afterwards by WPF's own render thread: in the program in software at 1x, because OpenPmsDental.exe sets
	///RenderOptions.ProcessRenderMode to SoftwareOnly for the whole process (O-34), and the gallery's times process sets it the same way.
	///MeasureLiveViewFrames measures it on a window that is on a display, as the Chart module's is: the time until WPF has drawn and presented the
	///frame (MediaContext.CompleteRender, as WPF itself waits for a frame when a window is resized), with the 3D layer cached and without the cache,
	///and one full software frame of the view at 1x (ToothChartWpfView.MeasureSoftwareFrameForTests); the same wait on the off-screen window shows
	///that WPF draws that one too, so the UI-thread times above leave the frame out rather than skip it. No test waits for a frame (section 7):
	///only the gallery's times process does.</summary>
	public static class WpfOnScreenTimings {
		///<summary>The pen stroke of the pen-move times, in 410x307 pixels: along the bottom edge, below the mandibular teeth.</summary>
		private static readonly Point _pointPenStart=new Point(30,284);
		///<summary>Unmeasured runs of each kind (refill, refill with the dashboard's image, selection) before an engine's measured runs at a size.</summary>
		public const int CountWarmUp=5;

		///<summary>Adds the on-screen times at 410x307 and 615x461 to the report, on an STA thread of its own. A note says why when there is no
		///desktop or DirectX does not start.</summary>
		public static void Measure(WpfTimingReport report,int countRepeats) {
			if(!Environment.UserInteractive) {
				report.ListNotes.Add("No on-screen times: no interactive desktop session.");
				return;
			}
			StaRunner.Run(() => {
				string directXFormat=null;
				using(Form form=WpfCharts.CreateOffscreenForm(ChartScenes.SizeModule.Size)) {
					form.Show();
					Application.DoEvents();
					directXFormat=DirectXCharts.FindProgramFormat(form);
					form.Close();
				}
				report.ListNotes.Add(directXFormat==null?"No DirectX times: no standard DirectX format starts here."
					:"DirectX device format: "+directXFormat+" (the program's own pick on a computer with none saved, ToothChartWrapper.GetPreferredDirectXFormat).");
				report.ListNotes.Add("Each engine and size had "+CountWarmUp+" unmeasured runs of each kind before its measured runs.");
				foreach(Size size in new[] { ChartScenes.SizeModule.Size,WpfTimings.Size150 }) {
					string sizeName=size.Width+"x"+size.Height;
					using(Form form=WpfCharts.CreateOffscreenForm(size)) {
						form.Show();
						Application.DoEvents();
						if(directXFormat!=null) {
							MeasureDirectX(report,form,size,sizeName,countRepeats,directXFormat);
						}
						MeasureWpf(report,form,size,sizeName,countRepeats,false);
						MeasureWpf(report,form,size,sizeName,countRepeats,true);
						form.Close();
					}
					MeasureLiveViewFrames(report,size,sizeName,countRepeats);
				}
			});
		}

		///<summary>True when this process draws WPF in software, as the program does (RenderOptions.ProcessRenderMode SoftwareOnly).</summary>
		public static bool IsProcessSoftware() {
			return System.Windows.Media.RenderOptions.ProcessRenderMode==System.Windows.Interop.RenderMode.SoftwareOnly;
		}

		private static void MeasureDirectX(WpfTimingReport report,Form form,Size size,string sizeName,int countRepeats,string directXFormat) {
			ToothChartWrapper wrapper=DirectXCharts.CreateWrapper(form,size,directXFormat);
			try {
				if(wrapper.Engine!=ToothChartEngine.DirectX) {
					report.ListNotes.Add("No DirectX times at "+sizeName+": the chart fell back to "+wrapper.Engine+".");
					return;
				}
				ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
				MeasureEngine(report,sizeName+".directx","DirectX (today's chart)",relay,() => wrapper.Refresh(),countRepeats);
			}
			finally {
				form.Controls.Remove(wrapper);
				wrapper.Dispose();
				Application.DoEvents();
			}
		}

		private static void MeasureWpf(WpfTimingReport report,Form form,Size size,string sizeName,int countRepeats,bool isSoftware) {
			form.ClientSize=size;
			Stopwatch stopwatch=Stopwatch.StartNew();
			ToothChartWrapper wrapper=new ToothChartWrapper();
			wrapper.Size=size;
			wrapper.Engine=ToothChartEngine.Wpf3D;
			wrapper.WpfDrawInSoftware=isSoftware;
			form.Controls.Add(wrapper);
			try {
				ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
				string key=sizeName+(isSoftware?".software":".live");
				string name=isSoftware?"WPF drawn in software (the image host)":"WPF live view, the UI thread's work";
				Action actionShow=isSoftware?GetWaitForImage(wrapper):GetWaitForLiveView(wrapper);
				ChartScenes.Prepare(relay,ChartScenes.Get("restored-adult"),ChartColorSet.Module);
				if(isSoftware) {
					actionShow();
				}
				else if(WpfCharts.WaitForLiveView(wrapper,30000)==null) {//the first rebuild makes the live view and shows the refill in it
					report.ListNotes.Add("No live view times at "+sizeName+": the live view did not start.");
					return;
				}
				else {
					wrapper.LiveViewWpf.Child.UpdateLayout();
				}
				report.Add(key+".first","Cold for this size: "+name+" made on a shown wrapper and its first refill shown",stopwatch.Elapsed.TotalMilliseconds);
				MeasureEngine(report,key,name,relay,actionShow,countRepeats);
			}
			finally {
				form.Controls.Remove(wrapper);
				wrapper.Dispose();
				Application.DoEvents();
			}
		}

		///<summary>Runs the wrapper's posted rebuild and waits until the live view has shown a new scene, then lays it out (the 2D layer's drawing).</summary>
		private static Action GetWaitForLiveView(ToothChartWrapper wrapper) {
			return () => {
				ToothChartWpfView toothChartWpfView=wrapper.LiveViewWpf;
				int countBefore=toothChartWpfView.CountScenesShown;
				if(!WpfRendererTests.PumpUntil(() => toothChartWpfView.CountScenesShown>countBefore && !wrapper.IsWpfSceneDirty,30000)) {
					throw new TimeoutException("The live view showed no new scene.");
				}
				toothChartWpfView.Child.UpdateLayout();
			};
		}

		///<summary>Runs the wrapper's posted rebuild and waits until the image host has its new image from the render thread.</summary>
		private static Action GetWaitForImage(ToothChartWrapper wrapper) {
			return () => {
				ToothChartImageHost toothChartImageHost=wrapper.ImageHostWpf;
				int countBefore=toothChartImageHost.CountImagesReceived;
				if(!WpfRendererTests.PumpUntil(() => toothChartImageHost.CountImagesReceived>countBefore && !toothChartImageHost.IsRenderInFlight
					&& !wrapper.IsWpfSceneDirty,30000))
				{
					throw new TimeoutException("The image host got no new image.");
				}
			};
		}

		///<summary>Refill plus the screen's update (actionShow), with the dashboard hidden and visible, and a selection-only change, warm: CountWarmUp
		///unmeasured runs of each kind first.</summary>
		private static void MeasureEngine(WpfTimingReport report,string key,string name,ToothChartRelay relay,Action actionShow,int countRepeats) {
			ChartScene sceneA=ChartScenes.Get("restored-adult");
			ChartScene sceneB=ChartScenes.Get("treatment-plan");
			ChartScenes.Prepare(relay,sceneA,ChartColorSet.Module);
			actionShow();
			for(int i=0;i<CountWarmUp;i++) {
				ChartScenes.Prepare(relay,i%2==0?sceneB:sceneA,ChartColorSet.Module);
				actionShow();
				ChartScenes.Prepare(relay,i%2==0?sceneA:sceneB,ChartColorSet.Module);
				using(Bitmap bitmap=relay.GetBitmap()) {
				}
				actionShow();
				relay.SetSelected("8",i%2==0);
				actionShow();
			}
			ChartScenes.Prepare(relay,sceneA,ChartColorSet.Module);
			actionShow();
			List<double> listHidden=new List<double>();
			List<double> listVisible=new List<double>();
			Stopwatch stopwatch=new Stopwatch();
			for(int i=0;i<countRepeats;i++) {
				stopwatch.Restart();
				ChartScenes.Prepare(relay,i%2==0?sceneB:sceneA,ChartColorSet.Module);
				actionShow();
				listHidden.Add(stopwatch.Elapsed.TotalMilliseconds);
			}
			for(int i=0;i<countRepeats;i++) {
				stopwatch.Restart();
				ChartScenes.Prepare(relay,i%2==0?sceneB:sceneA,ChartColorSet.Module);
				using(Bitmap bitmap=relay.GetBitmap()) {//the dashboard's image (ControlChart, while a Patient Dashboard is shown)
				}
				actionShow();
				listVisible.Add(stopwatch.Elapsed.TotalMilliseconds);
			}
			List<double> listSelection=new List<double>();
			for(int i=0;i<countRepeats;i++) {
				stopwatch.Restart();
				relay.SetSelected("8",i%2==0);
				actionShow();
				listSelection.Add(stopwatch.Elapsed.TotalMilliseconds);
			}
			report.Add(key+".refill",name+": refill plus the screen's update, dashboard hidden, median",WpfTimings.Median(listHidden));
			report.Add(key+".refill.max",name+": the same, slowest",listHidden.Max());
			report.Add(key+".refill.dashboard",name+": refill, the dashboard's image (GetBitmap) and the screen's update, dashboard visible, median",
				WpfTimings.Median(listVisible));
			report.Add(key+".selection",name+": a selection-only change and the screen's update, median",WpfTimings.Median(listSelection));
			report.Add(key+".selection.max",name+": the same, slowest",listSelection.Max());
		}

		///<summary>The live view's frames on a window that is on a display (FormOnDisplay), in this process's render mode: "live" when the process draws
		///in software (as the program does), "live.hardware" when it leaves hardware rendering on. For a refill, a selection-only change and one pen
		///move: ".shown.*" is the UI thread's work plus the frame (until WPF has drawn and presented it), ".frame.*" the frame alone, with the 3D
		///layer cached (the view's default) and, in software, ".nocache" without the cache (TC-3 as first written). ".frame.idle" is the same wait
		///with nothing changed, ".fullframe" one full software frame of the view at 1x (a RenderTargetBitmap of its content, without the cache), and
		///".offscreen.frame.refill" the refill's frame on an off-screen window, which shows whether WPF draws such a window at all.</summary>
		public static void MeasureLiveViewFrames(WpfTimingReport report,Size size,string sizeName,int countRepeats) {
			bool isProcessSoftware=IsProcessSoftware();
			string key=sizeName+(isProcessSoftware?".live":".live.hardware");
			string name="WPF live view"+(isProcessSoftware?", drawn in software as in the program":", with hardware rendering (not the program today)");
			Action actionCompleteRender=GetCompleteRender();
			if(actionCompleteRender==null) {
				report.ListNotes.Add("No live-view frame times at "+sizeName+": this framework has no MediaContext.CompleteRender to wait for a frame.");
			}
			using(FormOnDisplay form=new FormOnDisplay(size)) {
				form.Show();
				Application.DoEvents();
				ToothChartWrapper wrapper=WpfCharts.CreateWrapper(size);
				form.Controls.Add(wrapper);
				try {
					ToothChartWpfView toothChartWpfView=WpfCharts.WaitForLiveView(wrapper,30000);//made by the wrapper's first rebuild on screen
					if(toothChartWpfView==null) {
						report.ListNotes.Add("No live-view frame times at "+sizeName+": the live view did not start.");
						return;
					}
					ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
					Action actionShow=GetWaitForLiveView(wrapper);
					ChartScenes.Prepare(relay,ChartScenes.Get("restored-adult"),ChartColorSet.Module);
					actionShow();
					if(isProcessSoftware) {
						toothChartWpfView.Is3DLayerCached=false;
						List<double> listFull=new List<double>();
						for(int i=0;i<countRepeats;i++) {
							ChartScenes.Prepare(relay,ChartScenes.Get(i%2==0?"treatment-plan":"restored-adult"),ChartColorSet.Module);
							actionShow();
							listFull.Add(toothChartWpfView.MeasureSoftwareFrameForTests());
						}
						report.Add(key+".fullframe",name+": one full frame of the view by WPF's software rasterizer at 1x (the 3D layer drawn again; a "
							+"RenderTargetBitmap of its content), median",WpfTimings.Median(listFull));
						toothChartWpfView.Is3DLayerCached=true;
					}
					if(actionCompleteRender==null) {
						return;
					}
					WaitForFrame(actionCompleteRender);
					List<double> listIdle=new List<double>();
					Stopwatch stopwatch=new Stopwatch();
					for(int i=0;i<countRepeats;i++) {
						stopwatch.Restart();
						WaitForFrame(actionCompleteRender);
						listIdle.Add(stopwatch.Elapsed.TotalMilliseconds);
					}
					report.Add(key+".frame.idle",name+": waiting for a frame with nothing changed, median",WpfTimings.Median(listIdle));
					foreach(bool isCached in isProcessSoftware?new[] { true,false }:new[] { true }) {
						toothChartWpfView.Is3DLayerCached=isCached;
						MeasureFrames(report,key+(isCached?"":".nocache"),name+(isCached?", 3D layer cached":", 3D layer not cached"),wrapper,relay,
							actionShow,actionCompleteRender,countRepeats);
					}
					toothChartWpfView.Is3DLayerCached=true;
				}
				finally {
					form.Controls.Remove(wrapper);
					wrapper.Dispose();
					Application.DoEvents();
					form.Close();
				}
			}
			if(actionCompleteRender!=null && isProcessSoftware) {
				MeasureOffscreenRefillFrame(report,size,key,name,actionCompleteRender,countRepeats);
			}
		}

		///<summary>A refill, a selection-only change and a pen move, each until WPF has presented its frame.</summary>
		private static void MeasureFrames(WpfTimingReport report,string key,string name,ToothChartWrapper wrapper,ToothChartRelay relay,Action actionShow,
			Action actionCompleteRender,int countRepeats)
		{
			ToothChartWpfView toothChartWpfView=wrapper.LiveViewWpf;
			ChartScenes.Prepare(relay,ChartScenes.Get("restored-adult"),ChartColorSet.Module);
			actionShow();
			WaitForFrame(actionCompleteRender);
			Stopwatch stopwatch=new Stopwatch();
			List<double> listRefillShown=new List<double>();
			List<double> listRefillFrame=new List<double>();
			for(int i=0;i<countRepeats;i++) {
				stopwatch.Restart();
				ChartScenes.Prepare(relay,ChartScenes.Get(i%2==0?"treatment-plan":"restored-adult"),ChartColorSet.Module);
				actionShow();
				double millisecondsUi=stopwatch.Elapsed.TotalMilliseconds;
				WaitForFrame(actionCompleteRender);
				listRefillShown.Add(stopwatch.Elapsed.TotalMilliseconds);
				listRefillFrame.Add(stopwatch.Elapsed.TotalMilliseconds-millisecondsUi);
			}
			List<double> listSelectionShown=new List<double>();
			List<double> listSelectionFrame=new List<double>();
			for(int i=0;i<countRepeats;i++) {
				stopwatch.Restart();
				relay.SetSelected("8",i%2==0);
				actionShow();
				double millisecondsUi=stopwatch.Elapsed.TotalMilliseconds;
				WaitForFrame(actionCompleteRender);
				listSelectionShown.Add(stopwatch.Elapsed.TotalMilliseconds);
				listSelectionFrame.Add(stopwatch.Elapsed.TotalMilliseconds-millisecondsUi);
			}
			//A pen stroke along the bottom edge, one move at a time, as the mouse moves.
			double scale=wrapper.Width/410.0;
			relay.CursorTool=CursorTool.Pen;
			toothChartWpfView.MouseDownForTests(ScalePoint(_pointPenStart,0,scale));
			toothChartWpfView.Child.UpdateLayout();
			WaitForFrame(actionCompleteRender);
			List<double> listPenShown=new List<double>();
			List<double> listPenFrame=new List<double>();
			for(int i=0;i<countRepeats;i++) {
				stopwatch.Restart();
				toothChartWpfView.MouseMoveForTests(ScalePoint(_pointPenStart,4*(i+1),scale));
				toothChartWpfView.Child.UpdateLayout();
				double millisecondsUi=stopwatch.Elapsed.TotalMilliseconds;
				WaitForFrame(actionCompleteRender);
				listPenShown.Add(stopwatch.Elapsed.TotalMilliseconds);
				listPenFrame.Add(stopwatch.Elapsed.TotalMilliseconds-millisecondsUi);
			}
			toothChartWpfView.MouseUpForTests(ScalePoint(_pointPenStart,4*countRepeats,scale));
			relay.CursorTool=CursorTool.Pointer;
			actionShow();
			WaitForFrame(actionCompleteRender);
			report.Add(key+".shown.refill",name+": refill, the UI thread's work and WPF's frame until it is presented, median",WpfTimings.Median(listRefillShown));
			report.Add(key+".frame.refill",name+": of that, WPF's frame, median",WpfTimings.Median(listRefillFrame));
			report.Add(key+".shown.selection",name+": a selection-only change, the UI thread's work and WPF's frame, median",WpfTimings.Median(listSelectionShown));
			report.Add(key+".frame.selection",name+": of that, WPF's frame, median",WpfTimings.Median(listSelectionFrame));
			report.Add(key+".shown.pen",name+": one pen move (4 pixels of stroke), the UI thread's work and WPF's frame, median",WpfTimings.Median(listPenShown));
			report.Add(key+".frame.pen",name+": of that, WPF's frame, median",WpfTimings.Median(listPenFrame));
		}

		///<summary>The refill's frame on an off-screen window (the form the UI-thread times use), for comparison with the window on a display.</summary>
		private static void MeasureOffscreenRefillFrame(WpfTimingReport report,Size size,string key,string name,Action actionCompleteRender,int countRepeats) {
			using(Form form=WpfCharts.CreateOffscreenForm(size)) {
				form.Show();
				Application.DoEvents();
				ToothChartWrapper wrapper=WpfCharts.CreateWrapper(size);
				form.Controls.Add(wrapper);
				try {
					if(WpfCharts.WaitForLiveView(wrapper,30000)==null) {
						return;
					}
					ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
					Action actionShow=GetWaitForLiveView(wrapper);
					ChartScenes.Prepare(relay,ChartScenes.Get("restored-adult"),ChartColorSet.Module);
					actionShow();
					WaitForFrame(actionCompleteRender);
					List<double> listFrame=new List<double>();
					Stopwatch stopwatch=new Stopwatch();
					for(int i=0;i<countRepeats;i++) {
						ChartScenes.Prepare(relay,ChartScenes.Get(i%2==0?"treatment-plan":"restored-adult"),ChartColorSet.Module);
						actionShow();
						stopwatch.Restart();
						WaitForFrame(actionCompleteRender);
						listFrame.Add(stopwatch.Elapsed.TotalMilliseconds);
					}
					report.Add(key+".offscreen.frame.refill",name+": a refill's frame on a window off every display (the UI-thread times' window), median",
						WpfTimings.Median(listFrame));
				}
				finally {
					form.Controls.Remove(wrapper);
					wrapper.Dispose();
					Application.DoEvents();
					form.Close();
				}
			}
		}

		private static Point ScalePoint(Point point,int dx,double scale) {
			return new Point((int)Math.Round((point.X+dx)*scale),(int)Math.Round(point.Y*scale));
		}

		///<summary>Lets WPF draw what the UI thread changed and waits until its render thread has drawn and presented the frame: the UI thread's render
		///pass (Dispatcher priority Render runs before Loaded), then CompleteRender; twice, so a frame that WPF held back until the previous one was
		///presented is drawn too.</summary>
		private static void WaitForFrame(Action actionCompleteRender) {
			for(int i=0;i<2;i++) {
				Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Loaded,new Action(() => { }));
				actionCompleteRender();
			}
		}

		///<summary>WPF's internal MediaContext.CompleteRender of this thread's Dispatcher: it commits the UI thread's frame and returns when WPF's render
		///thread has drawn and presented it (HwndTarget calls it on a resize). Null when this framework has no such method.</summary>
		private static Action GetCompleteRender() {
			Type typeMediaContext=typeof(System.Windows.Media.CompositionTarget).Assembly.GetType("System.Windows.Media.MediaContext");
			if(typeMediaContext==null) {
				return null;
			}
			MethodInfo methodInfoFrom=typeMediaContext.GetMethod("From",BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static,null,
				new[] { typeof(Dispatcher) },null);
			MethodInfo methodInfoCompleteRender=typeMediaContext.GetMethod("CompleteRender",BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance,
				null,Type.EmptyTypes,null);
			if(methodInfoFrom==null || methodInfoCompleteRender==null) {
				return null;
			}
			object mediaContext=methodInfoFrom.Invoke(null,new object[] { Dispatcher.CurrentDispatcher });
			return () => methodInfoCompleteRender.Invoke(mediaContext,null);
		}

		///<summary>A borderless window on the primary screen that the user neither sees nor clicks (1% opaque, click-through, never activated, not in the
		///task bar): the live view's frames are measured on a window that is on a display.</summary>
		private sealed class FormOnDisplay:Form {
			private const int WS_EX_TRANSPARENT=0x00000020;
			private const int WS_EX_TOOLWINDOW=0x00000080;
			private const int WS_EX_NOACTIVATE=0x08000000;

			public FormOnDisplay(Size sizeClient) {
				FormBorderStyle=FormBorderStyle.None;
				ShowInTaskbar=false;
				StartPosition=FormStartPosition.Manual;
				Location=Screen.PrimaryScreen.WorkingArea.Location;
				ClientSize=sizeClient;
				Opacity=0.01;
			}

			protected override bool ShowWithoutActivation {
				get {
					return true;
				}
			}

			protected override CreateParams CreateParams {
				get {
					CreateParams createParams=base.CreateParams;
					createParams.ExStyle|=WS_EX_TRANSPARENT|WS_EX_TOOLWINDOW|WS_EX_NOACTIVATE;
					return createParams;
				}
			}
		}
	}
}
