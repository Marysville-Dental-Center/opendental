// The on-screen render times of TC-3 (docs/specs/wpf-tooth-chart-plan.md, TC-3's row and gallery G1): a refill plus the screen's update, with
// the Patient Dashboard visible (the refill also makes the dashboard's image, ControlChart's GetBitmap) and hidden, and a selection-only change,
// for today's DirectX chart, the WPF chart's live view and the WPF chart drawn in software.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using OpenPmsDental;
using SparksToothChart;
using SparksToothChart.Wpf3D;

namespace ToothChartImageTests {
	///<summary>Each engine on a shown, off-screen form, the way the Chart module uses its chart. "Refill" is ChartScenes.Prepare through the relay
	///(as ControlChart.FillToothChart), then the screen's update: DirectX paints at once (Refresh, which renders and presents); the WPF chart runs
	///its posted rebuild (the scene, then the live view's frozen models and layout, or the software render of the image host until its image
	///arrives). "Dashboard" adds the refill's GetBitmap for the Patient Dashboard (ControlChart makes it only while a dashboard is shown). Warm values
	///are medians of runs alternating two scenes, so the 3D layer changes every time. The live view's frame itself is drawn by WPF's own render
	///thread on the graphics card after the UI thread's work, and no measurement waits for a frame (section 7); its times are the UI thread's.</summary>
	public static class WpfOnScreenTimings {
		///<summary>Adds the on-screen times at 410x307 and 615x461 to the report, on an STA thread of its own. A note says why when there is no
		///desktop or DirectX does not start.</summary>
		public static void Measure(WpfTimingReport report,int countRepeats) {
			if(!Environment.UserInteractive) {
				report.ListNotes.Add("No on-screen times: no interactive desktop session.");
				return;
			}
			StaRunner.Run(() => {
				foreach(Size size in new[] { ChartScenes.SizeModule.Size,WpfTimings.Size150 }) {
					string sizeName=size.Width+"x"+size.Height;
					using(Form form=WpfCharts.CreateOffscreenForm(size)) {
						form.Show();
						Application.DoEvents();
						MeasureDirectX(report,form,size,sizeName,countRepeats);
						MeasureWpf(report,form,size,sizeName,countRepeats,false);
						MeasureWpf(report,form,size,sizeName,countRepeats,true);
						form.Close();
					}
				}
			});
		}

		private static void MeasureDirectX(WpfTimingReport report,Form form,Size size,string sizeName,int countRepeats) {
			string directXFormat=DirectXCharts.FindFormat(form);
			if(directXFormat==null) {
				report.ListNotes.Add("No DirectX times at "+sizeName+": no standard DirectX format starts here.");
				return;
			}
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
				string name=isSoftware?"WPF drawn in software (the image host)":"WPF live view";
				if(!isSoftware && wrapper.LiveViewWpf==null) {
					report.ListNotes.Add("No live view times at "+sizeName+": the live view did not start.");
					return;
				}
				Action actionShow=isSoftware?GetWaitForImage(wrapper):GetWaitForLiveView(wrapper);
				ChartScenes.Prepare(relay,ChartScenes.Get("restored-adult"),ChartColorSet.Module);
				actionShow();
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

		///<summary>Refill plus the screen's update (actionShow), with the dashboard hidden and visible, and a selection-only change, warm.</summary>
		private static void MeasureEngine(WpfTimingReport report,string key,string name,ToothChartRelay relay,Action actionShow,int countRepeats) {
			ChartScene sceneA=ChartScenes.Get("restored-adult");
			ChartScene sceneB=ChartScenes.Get("treatment-plan");
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
	}
}
