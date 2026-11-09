// The WPF chart's render times for owner question O-19 (package TC-1, launch check L-2): refill plus render at 410x307 and 615x461, a selection-only
// change, and the big chart's time and memory, cold and warm.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Text;
using Microsoft.Win32;
using OpenPmsDental;
using SparksToothChart;
using SparksToothChart.Wpf3D;

namespace ToothChartImageTests {
	///<summary>One measured value.</summary>
	public class WpfTiming {
		public string Key;
		public string Text;
		public double Value;
		public string Unit;
	}

	///<summary>The measured values and the machine they were measured on.</summary>
	public class WpfTimingReport {
		public List<string> ListMachine=new List<string>();
		public List<WpfTiming> ListTimings=new List<WpfTiming>();
		public List<string> ListNotes=new List<string>();

		public void Add(string key,string text,double value,string unit="ms") {
			WpfTiming wpfTiming=new WpfTiming();
			wpfTiming.Key=key;
			wpfTiming.Text=text;
			wpfTiming.Value=value;
			wpfTiming.Unit=unit;
			ListTimings.Add(wpfTiming);
		}

		public double Get(string key) {
			WpfTiming wpfTiming=ListTimings.FirstOrDefault(x => x.Key==key);
			return wpfTiming==null?double.NaN:wpfTiming.Value;
		}

		public string GetReport() {
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.AppendLine("WPF tooth chart render times (TC-1, for O-19; TC-3, on screen against DirectX, for G1)");
			foreach(string line in ListMachine) {
				stringBuilder.AppendLine("Machine: "+line);
			}
			foreach(WpfTiming wpfTiming in ListTimings) {
				stringBuilder.AppendLine(wpfTiming.Key+"\t"+wpfTiming.Value.ToString("0.0",CultureInfo.InvariantCulture)+" "+wpfTiming.Unit+"\t"+wpfTiming.Text);
			}
			foreach(string note in ListNotes) {
				stringBuilder.AppendLine("Note: "+note);
			}
			return stringBuilder.ToString();
		}
	}

	///<summary>Measures the WPF chart as the Chart module uses it: a refill (ChartScenes.Prepare through the relay, as ControlChart.FillToothChart
	///sets the chart) followed by GetBitmap, which builds the scene and waits for the renderer. "Cold" values are the first of a fresh process: the
	///render thread, WPF and the meshes start then. Warm values are the median and the slowest of repeated runs, alternating two scenes so the 3D
	///layer changes every time. A selection-only change toggles one tooth's selection, which keeps the 3D layer (its image is reused).</summary>
	public static class WpfTimings {
		///<summary>The repeats of the gallery's measurement (a fresh process); the in-process test uses fewer.</summary>
		public const int CountRepeats=21;
		public static readonly Size Size150=new Size(615,461);

		///<summary>isQuick: the module size only (the in-process test); otherwise every size, the factor variants, the big chart's memory and TC-3's
		///on-screen times against DirectX (WpfOnScreenTimings).</summary>
		public static WpfTimingReport Measure(bool isFreshProcess,int countRepeats=CountRepeats,bool isQuick=false) {
			WpfTimingReport report=new WpfTimingReport();
			ReadMachine(report);
			report.ListMachine.Add("Warm values: median and slowest of "+countRepeats+" runs");
			if(!isFreshProcess) {
				report.ListNotes.Add("Measured in a process that had drawn charts before: the cold values are not cold.");
			}
			ChartScene sceneA=ChartScenes.Get("restored-adult");
			ChartScene sceneB=ChartScenes.Get("treatment-plan");
			Stopwatch stopwatch=Stopwatch.StartNew();
			ToothChartWrapper wrapper410=WpfCharts.CreateWrapper(ChartScenes.SizeModule.Size);
			report.Add("start","Cold: a new wrapper set to Wpf3D (the render thread, WPF and a 1x1 check image)",stopwatch.Elapsed.TotalMilliseconds);
			if(wrapper410.Engine!=ToothChartEngine.Wpf3D) {
				report.ListNotes.Add("The chart fell back to "+wrapper410.Engine+"; no times.");
				return report;
			}
			MeasureSize(report,"410x307",wrapper410,sceneA,sceneB,countRepeats,true);
			wrapper410.Dispose();
			if(isQuick) {
				return report;
			}
			ToothChartWrapper wrapper615=WpfCharts.CreateWrapper(Size150);
			MeasureSize(report,"615x461",wrapper615,sceneA,sceneB,countRepeats,true);
			//The same at 150% with less supersampling, for O-19's choice: 2x and 1x (no smoothing of the 3D layer's edges).
			try {
				foreach(int factor in new[] { 2,1 }) {
					ToothChartRenderer.SupersamplingForMeasurement=factor;
					MeasureSize(report,"615x461.factor"+factor,wrapper615,sceneA,sceneB,countRepeats,false);
				}
			}
			finally {
				ToothChartRenderer.SupersamplingForMeasurement=0;
			}
			wrapper615.Dispose();
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
			long bytesBefore=Process.GetCurrentProcess().PrivateMemorySize64;
			ToothChartWrapper wrapperBig=WpfCharts.CreateWrapper(ChartScenes.SizeBigWindow.Size);
			MeasureSize(report,"926x858",wrapperBig,sceneA,sceneB,countRepeats,true);
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
			long bytesAfter=Process.GetCurrentProcess().PrivateMemorySize64;
			report.Add("926x858.memory","The big chart: private bytes added by its wrapper, its render targets (3x and 1x) and its 3D layer image",
				(bytesAfter-bytesBefore)/1048576.0,"MB");
			report.Add("process.memory","Private bytes of the process after the big chart",bytesAfter/1048576.0,"MB");
			report.Add("926x858.factor","Supersampling factor of the big chart's 3D layer",ToothChartRenderer.GetSupersamplingFactor(ChartScenes.SizeBigWindow.Size),"x");
			wrapperBig.Dispose();
			WpfOnScreenTimings.Measure(report,countRepeats);
			return report;
		}

		///<summary>TC-3's on-screen times alone (WpfOnScreenTimings), with the machine they were measured on: for a fresh process (SecondProcess
		///"onscreen-times").</summary>
		public static WpfTimingReport MeasureOnScreen(int countRepeats=CountRepeats) {
			WpfTimingReport report=new WpfTimingReport();
			ReadMachine(report);
			report.ListMachine.Add("Warm values: median and slowest of "+countRepeats+" runs");
			WpfOnScreenTimings.Measure(report,countRepeats);
			return report;
		}

		private static void MeasureSize(WpfTimingReport report,string sizeName,ToothChartWrapper wrapper,ChartScene sceneA,ChartScene sceneB,int countRepeats,
			bool isFull)
		{
			ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
			Stopwatch stopwatch=Stopwatch.StartNew();
			using(Bitmap bitmap=WpfCharts.Draw(relay,sceneA,ChartColorSet.Module)) {
			}
			if(isFull) {
				report.Add(sizeName+".first","Cold for this size: the first refill and image (new render targets"+(sizeName=="410x307"?"; the meshes are built":"")+")",
					stopwatch.Elapsed.TotalMilliseconds);
			}
			List<double> listRefill=new List<double>();
			List<double> listTotal=new List<double>();
			for(int i=0;i<countRepeats;i++) {
				ChartScene scene=i%2==0?sceneB:sceneA;
				stopwatch.Restart();
				ChartScenes.Prepare(relay,scene,ChartColorSet.Module);
				double millisecondsRefill=stopwatch.Elapsed.TotalMilliseconds;
				using(Bitmap bitmap=relay.GetBitmap()) {
				}
				listTotal.Add(stopwatch.Elapsed.TotalMilliseconds);
				listRefill.Add(millisecondsRefill);
			}
			report.Add(sizeName+".refill","Warm: refill plus image (GetBitmap), median",Median(listTotal));
			report.Add(sizeName+".refill.max","Warm: refill plus image, slowest",listTotal.Max());
			if(!isFull) {
				return;
			}
			report.Add(sizeName+".refill.only","Warm: the refill alone (the relay's Set calls, the same for every engine), median",Median(listRefill));
			//The parts of one image: the scene on the caller's thread, then the render thread's work.
			List<double> listBuild=new List<double>();
			List<ToothChartRenderResult> listResults=new List<ToothChartRenderResult>();
			using(ToothChartLayerCache layerCache=new ToothChartLayerCache()) {
				for(int i=0;i<countRepeats;i++) {
					ChartScenes.Prepare(relay,i%2==0?sceneB:sceneA,ChartColorSet.Module);
					stopwatch.Restart();
					ToothScene toothScene=ToothSceneBuilder.Build(wrapper.TcData);
					listBuild.Add(stopwatch.Elapsed.TotalMilliseconds);
					listResults.Add(ToothChartRenderer.Instance.Render(toothScene,layerCache));
				}
			}
			report.Add(sizeName+".scene","Warm: building the scene on the caller's thread, median",Median(listBuild));
			report.Add(sizeName+".render","Warm: the render thread's work for a new 3D layer, median",Median(listResults.Select(x => x.MillisecondsRender).ToList()));
			report.Add(sizeName+".render.3d.model","  of that: the 3D layer's frozen models, median",Median(listResults.Select(x => x.Milliseconds3DModel).ToList()));
			report.Add(sizeName+".render.3d.rasterize","  of that: rasterizing the 3D layer at "+listResults[0].Factor3D+"x in software, median",
				Median(listResults.Select(x => x.Milliseconds3DRasterize).ToList()));
			report.Add(sizeName+".render.3d.reduce","  of that: the Fant reduction and the copy, median",Median(listResults.Select(x => x.Milliseconds3DReduce).ToList()));
			report.Add(sizeName+".render.composite","  of that: the 2D layer over the 3D image, rasterized and copied out, median",
				Median(listResults.Select(x => x.MillisecondsComposite).ToList()));
			//Selection only: the 3D layer is reused.
			ChartScenes.Prepare(relay,sceneA,ChartColorSet.Module);
			using(Bitmap bitmap=relay.GetBitmap()) {
			}
			List<double> listSelection=new List<double>();
			for(int i=0;i<countRepeats;i++) {
				stopwatch.Restart();
				relay.SetSelected("8",i%2==0);
				using(Bitmap bitmap=relay.GetBitmap()) {
				}
				listSelection.Add(stopwatch.Elapsed.TotalMilliseconds);
			}
			report.Add(sizeName+".selection","Warm: a selection-only change plus image (the 3D layer reused), median",Median(listSelection));
			report.Add(sizeName+".selection.max","Warm: a selection-only change plus image, slowest",listSelection.Max());
		}

		public static double Median(List<double> listValues) {
			List<double> listSorted=listValues.OrderBy(x => x).ToList();
			if(listSorted.Count==0) {
				return double.NaN;
			}
			int middle=listSorted.Count/2;
			if(listSorted.Count%2==1) {
				return listSorted[middle];
			}
			return (listSorted[middle-1]+listSorted[middle])/2;
		}

		private static void ReadMachine(WpfTimingReport report) {
			try {
				using(RegistryKey registryKeyBase=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64))
				using(RegistryKey registryKey=registryKeyBase.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion")) {
					if(registryKey!=null) {
						report.ListMachine.Add("Windows build "+registryKey.GetValue("CurrentBuild")+"."+registryKey.GetValue("UBR"));
					}
				}
				using(RegistryKey registryKeyBase=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64))
				using(RegistryKey registryKey=registryKeyBase.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0")) {
					if(registryKey!=null) {
						report.ListMachine.Add("Processor "+(registryKey.GetValue("ProcessorNameString")??"").ToString().Trim()+", "+Environment.ProcessorCount+" logical");
					}
				}
			}
			catch(Exception ex) {
				report.ListMachine.Add("Windows and processor unknown ("+ex.GetType().Name+")");
			}
			try {
				using(ManagementObjectSearcher managementObjectSearcher=new ManagementObjectSearcher("SELECT Name, DriverVersion FROM Win32_VideoController")) {
					foreach(ManagementBaseObject managementBaseObject in managementObjectSearcher.Get()) {
						report.ListMachine.Add("Adapter "+managementBaseObject["Name"]+", driver "+managementBaseObject["DriverVersion"]);
					}
				}
			}
			catch(Exception ex) {
				report.ListMachine.Add("Adapters unknown ("+ex.GetType().Name+")");
			}
			report.ListMachine.Add((Environment.Is64BitProcess?"64":"32")+"-bit process, .NET Framework "+Environment.Version);
			report.ListMachine.Add("WPF render mode of the process: "+System.Windows.Media.RenderOptions.ProcessRenderMode
				+(WpfOnScreenTimings.IsProcessSoftware()?" (as OpenPmsDental.exe sets it: every WPF window, the live view included, is drawn in software, O-34)"
				:" (WPF draws its windows with the graphics card where it can; the program does not run so, O-34)")
				+"; RenderTargetBitmap, and so every image of the renderer, draws in software either way");
			using(Graphics g=Graphics.FromHwnd(IntPtr.Zero)) {
				report.ListMachine.Add("Screen DPI seen by the process "+g.DpiX.ToString(CultureInfo.InvariantCulture));
			}
		}
	}
}
