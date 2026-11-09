// Helpers for the WPF chart's tests (package TC-1): wrappers on the Wpf3D engine, scenes drawn through the relay, and the folders of the WPF baselines.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
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
	}
}
