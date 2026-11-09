// Scene-builder tests of TC-3's parity work (docs/specs/wpf-tooth-chart-plan.md section 7 item 1): no WPF object and no thread.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Drawing;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SparksToothChart;
using SparksToothChart.Wpf3D;

namespace ToothChartImageTests {
	[TestClass]
	public class WpfParitySceneTests {
		[TestMethod]
		public void AxisAlignedRectangles_SnapToThePixelsDirectXFills() {
			//The centre line at 410x307: 0.32 mm is 1.008 pixels around y 153.5 in DirectX (154 in the image's coordinates), so DirectX fills the rows
			//whose centres 153.5 and 154.5 lie inside [153.496, 154.504): rows 153 and 154.
			PointF[] arrayLine={ new PointF(0.5f,153.496f),new PointF(410.5f,153.496f),new PointF(410.5f,154.504f),new PointF(0.5f,154.504f) };
			Assert.IsTrue(ToothSceneItem2D.IsAxisAlignedRectangle(arrayLine));
			PointF[] arraySnapped=ToothSceneItem2D.SnapToDirectXPixels(arrayLine);
			Assert.AreEqual(153f,arraySnapped.Min(x => x.Y));
			Assert.AreEqual(155f,arraySnapped.Max(x => x.Y));
			Assert.AreEqual(0f,arraySnapped.Min(x => x.X),"The pixel whose centre 0.5 is on the left edge is filled (the top-left rule).");
			Assert.AreEqual(410f,arraySnapped.Max(x => x.X),"The pixel whose centre 410.5 is on the right edge is not.");
			//A line too thin to hold a pixel centre: DirectX draws nothing, and so does the 2D layer.
			Assert.IsNull(ToothSceneItem2D.SnapToDirectXPixels(new[] { new PointF(10,100.6f),new PointF(50,100.6f),new PointF(50,101.4f),new PointF(10,101.4f) }));
			ToothSceneItem2D itemEmpty=ToothSceneItem2D.CreatePolygons(Color.White,new System.Collections.Generic.List<PointF[]> {
				new[] { new PointF(10,100.6f),new PointF(50,100.6f),new PointF(50,101.4f),new PointF(10,101.4f) } },"thin");
			Assert.AreEqual(0,itemEmpty.Polygons.Count);
			//A slanted stroke keeps its points and is smoothed.
			PointF[] arraySlanted={ new PointF(10,10),new PointF(50,12),new PointF(49.9f,14),new PointF(9.9f,12) };
			Assert.IsFalse(ToothSceneItem2D.IsAxisAlignedRectangle(arraySlanted));
			ToothSceneItem2D itemSlanted=ToothSceneItem2D.CreatePolygons(Color.White,new System.Collections.Generic.List<PointF[]> { arraySlanted },"slanted");
			CollectionAssert.AreEquivalent(arraySlanted,itemSlanted.Polygons[0]);
		}

		[TestMethod]
		public void CentreLineAndHighlightBoxes_LieOnWholePixels_AtEveryChartSize() {
			foreach(ChartSize chartSize in WpfCharts.GetChartSizes()) {
				ToothChartData tcData=WpfSceneTests.CreateData(chartSize.Size);
				tcData.SetSelected("3",true);
				tcData.SetSelected("19",true);
				ToothScene toothScene=ToothSceneBuilder.Build(tcData);
				foreach(ToothSceneItem2D item in toothScene.Items2D.Where(x => x.Source=="centre line" || x.Source.StartsWith("highlight of "))) {
					Assert.AreEqual(1,item.Polygons.Count,item.Source);
					Assert.IsTrue(item.Polygons[0].All(x => x.X==(float)System.Math.Round(x.X) && x.Y==(float)System.Math.Round(x.Y)),
						chartSize.FolderName+" "+item+": "+string.Join(" ",item.Polygons[0]));
				}
				Assert.AreEqual(3,toothScene.Items2D.Count(x => x.Source=="centre line" || x.Source.StartsWith("highlight of ")),chartSize.FolderName);
			}
		}

		[TestMethod]
		public void Mobility_DrawsNothingInTheNormalChart_AsDirectXDrawsNone() {
			//DirectX's DrawScene has no mobility: only the perio chart draws it (DrawPerioRow, TC-7).
			ToothChartData tcData=WpfSceneTests.CreateData(ChartScenes.SizeModule.Size);
			string describeBefore=ToothSceneBuilder.Build(tcData).Describe();
			foreach(string toothId in new[] { "3","8","14","19","24","30" }) {
				tcData.ListToothGraphics[toothId].Mobility="2";
				tcData.ListToothGraphics[toothId].colorMobility=Color.Red;
			}
			Assert.AreEqual(describeBefore,ToothSceneBuilder.Build(tcData).Describe());
		}
	}
}
