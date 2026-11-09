// The live view's real WPF input path (package TC-3, fix round 3; docs/specs/wpf-tooth-chart-plan.md section 7 item 7): raw mouse reports through
// WPF's MouseDevice to the content's MouseMove, MouseDown and MouseUp, so GetPosition (under the content's 1/DPI transform) and the capture are
// exercised, against the shared ToothChartMouse given the same points; the capture released before the handlers of SegmentDrawn and
// ToothSelectionsChanged run; WPF's own MouseMove on a capture change adds no point; and a held-still pen adds none.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;
using SparksToothChart;
using SparksToothChart.Wpf3D;

namespace ToothChartImageTests {
	[TestClass]
	public class WpfLiveViewInputTests {
		///<summary>Each input goes to the live view through WPF (WpfMouseInput) and to a shared ToothChartMouse on a copy of the chart's data, as
		///DirectX's handlers would get it: a move only where the mouse moved, the press and release at their points. Capture changes inside the view
		///also make WPF move its mouse to a point (simulated here at pointCaptureSync, as MouseDevice.Synchronize moves it to the real cursor), which no
		///window message made and the shared mouse never gets.</summary>
		private sealed class InputPair {
			public WpfMouseInput WpfMouseInput;
			public ToothChartMouse ToothChartMouseShared;
			public ToothChartData TcDataShared;
			public Size SizeChart;
			public Point PointLast=new Point(int.MinValue,int.MinValue);

			///<summary>A move of the mouse to the point; none when it is already there (Windows sends no WM_MOUSEMOVE for a mouse that does not move).</summary>
			public void Move(Point point) {
				if(point==PointLast) {
					return;
				}
				WpfMouseInput.Move(point);
				ToothChartMouseShared.MouseMove(TcDataShared,point,SizeChart);
				PointLast=point;
			}

			public void Press(Point point) {
				Move(point);
				WpfMouseInput.Press(point);
				ToothChartMouseShared.MouseDown(TcDataShared,point,SizeChart);
			}

			public void Release(Point point) {
				Move(point);
				WpfMouseInput.Release(point);
				ToothChartMouseShared.MouseUp(TcDataShared,point,SizeChart);
			}
		}

		[TestMethod]
		public void RealInput_ClickDragPenAndAHeldStillPen_GiveTheSharedMousesSelectionAndStrings() {
			WpfLiveViewTests.RequireDesktop();
			StaRunner.Run(() => {
				CultureInfo cultureOld=Thread.CurrentThread.CurrentCulture;
				try {
					Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
					foreach(ChartSize chartSize in new[] { ChartScenes.SizeModule,new ChartSize(615,461,ChartColorSet.Module) }) {
						CheckSize(chartSize);
					}
				}
				finally {
					Thread.CurrentThread.CurrentCulture=cultureOld;
				}
			});
		}

		private static void CheckSize(ChartSize chartSize) {
			Size size=chartSize.Size;
			string sizeName=size.Width+"x"+size.Height;
			using Form form=WpfCharts.CreateOffscreenForm(size);
			ToothChartWrapper wrapper=WpfCharts.CreateWrapper(size);
			form.Controls.Add(wrapper);
			ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
			ChartScenes.Prepare(relay,ChartScenes.Get("empty-universal"),chartSize.ColorSet);
			form.Show();
			ToothChartWpfView toothChartWpfView=WpfLiveViewTests.WaitForLiveView(wrapper);
			System.Windows.FrameworkElement content=(System.Windows.FrameworkElement)toothChartWpfView.Child;
			Assert.AreEqual(System.Windows.Media.VisualTreeHelper.GetDpi(WpfLiveViewTests.GetRootVisual(toothChartWpfView)).DpiScaleX,
				toothChartWpfView.DpiScaleShown,1e-9,"The content is under its 1/DPI transform, which GetPosition goes through.");
			using ToothChartWrapper wrapperShared=new ToothChartWrapper();//the same data in a 2D wrapper, for the shared mouse
			wrapperShared.Size=size;
			ChartScenes.Prepare(WpfCharts.CreateRelay(wrapperShared),ChartScenes.Get("empty-universal"),chartSize.ColorSet);
			List<string> listSegments=new List<string>();
			List<string> listSegmentsShared=new List<string>();
			int countSelections=0;
			int countSelectionsShared=0;
			List<string> listCapturedInHandlers=new List<string>();
			wrapper.SegmentDrawn+=(sender,e) => {
				listSegments.Add(e.DrawingSegement);
				listCapturedInHandlers.Add(System.Windows.Input.Mouse.Captured==null?null:"SegmentDrawn");
			};
			wrapper.ToothSelectionsChanged+=sender => {
				countSelections++;
				listCapturedInHandlers.Add(System.Windows.Input.Mouse.Captured==null?null:"ToothSelectionsChanged");
			};
			ToothChartMouse toothChartMouseShared=new ToothChartMouse();
			toothChartMouseShared.SegmentDrawn+=(sender,e) => listSegmentsShared.Add(e.DrawingSegement);
			toothChartMouseShared.ToothSelectionsChanged+=sender => countSelectionsShared++;
			InputPair inputPair=new InputPair();
			inputPair.WpfMouseInput=WpfMouseInput.ForVisual(content);
			inputPair.ToothChartMouseShared=toothChartMouseShared;
			inputPair.TcDataShared=wrapperShared.TcData;
			inputPair.SizeChart=size;
			//WPF's own move on each capture change, to a point over the chart that no input went to (a mandibular tooth for the press, the bottom edge for
			//the release): without the view's guard, the pointer's drag would start from that tooth and the pen would get a point there.
			Point pointSyncPress=ToothChartMouseTests.GetNumberCentre(wrapper.TcData,"25");
			Point pointSyncRelease=new Point(size.Width/2,size.Height-3);
			List<bool> listCapturedAfterPress=new List<bool>();
			content.GotMouseCapture+=(sender,e) => inputPair.WpfMouseInput.SynchroniseTo(pointSyncPress);
			content.LostMouseCapture+=(sender,e) => inputPair.WpfMouseInput.SynchroniseTo(pointSyncRelease);
			try {
				//A click on 30.
				Point point30=ToothChartMouseTests.GetNumberCentre(wrapper.TcData,"30");
				inputPair.Move(point30);
				inputPair.Press(point30);
				listCapturedAfterPress.Add(content.IsMouseCaptured);
				inputPair.Release(point30);
				Assert.IsFalse(content.IsMouseCaptured,sizeName+": released on mouse up.");
				CollectionAssert.AreEqual(new[] { "30" },wrapper.SelectedTeeth,sizeName);
				//A drag from 3 to 12 in wide steps: the skipped teeth are filled in, as the shared class fills them in.
				List<Point> listDrag=ToothChartMouseTests.GetDragPoints(ToothChartMouseTests.GetNumberCentre(wrapper.TcData,"3"),
					ToothChartMouseTests.GetNumberCentre(wrapper.TcData,"12"),5);
				inputPair.Move(listDrag[0]);
				inputPair.Press(listDrag[0]);
				listCapturedAfterPress.Add(content.IsMouseCaptured);
				foreach(Point point in listDrag.Skip(1)) {
					inputPair.Move(point);
				}
				inputPair.Release(listDrag[listDrag.Count-1]);
				CollectionAssert.AreEquivalent(inputPair.TcDataShared.SelectedTeeth,wrapper.SelectedTeeth,sizeName+": the drag's selection.");
				Assert.AreEqual(11,wrapper.SelectedTeeth.Count,sizeName+": 30, then 3 to 12: "+string.Join(",",wrapper.SelectedTeeth));
				//A pen stroke along the bottom edge: the SegmentDrawn string of GetPointMouseScaled (tenths at 615x461), with no point of the capture's moves.
				relay.CursorTool=CursorTool.Pen;
				inputPair.TcDataShared.CursorTool=CursorTool.Pen;
				double scale=size.Width/410.0;
				List<Point> listPen=new[] { new Point(30,280),new Point(45,284),new Point(63,290),new Point(80,288) }
					.Select(x => new Point((int)Math.Round(x.X*scale),(int)Math.Round(x.Y*scale))).ToList();
				inputPair.Press(listPen[0]);
				listCapturedAfterPress.Add(content.IsMouseCaptured);
				foreach(Point point in listPen.Skip(1)) {
					inputPair.Move(point);
				}
				Assert.AreEqual(listPen.Count,wrapper.TcData.PointList.Count,sizeName+": one point per move, none for the capture's own move.");
				inputPair.Release(listPen[listPen.Count-1]);
				Assert.AreEqual(ToothChartMouse.GetDrawingSegment(inputPair.TcDataShared,listPen.Select(x => new PointF(x.X,x.Y)).ToList(),size),listSegments.Last(),
					sizeName+": the stroke's string.");
				//A pen held still between press and release: one point, as DirectX gets no WM_MOUSEMOVE for a mouse that does not move.
				Point pointStill=new Point((int)Math.Round(120*scale),(int)Math.Round(292*scale));
				inputPair.Press(pointStill);
				listCapturedAfterPress.Add(content.IsMouseCaptured);
				inputPair.Release(pointStill);
				Assert.AreEqual(ToothChartMouse.GetDrawingSegment(inputPair.TcDataShared,new List<PointF> { new PointF(pointStill.X,pointStill.Y) },size),
					listSegments.Last(),sizeName+": a held-still pen adds no point.");
				relay.CursorTool=CursorTool.Pointer;
			}
			finally {
				System.Windows.Input.Mouse.Capture(null);
				inputPair.WpfMouseInput.Deactivate();
			}
			CollectionAssert.AreEqual(listSegmentsShared,listSegments,sizeName+": the same SegmentDrawn strings as the shared class's, in order.");
			Assert.AreEqual(2,listSegments.Count,sizeName);
			Assert.AreEqual(countSelectionsShared,countSelections,sizeName+": the same commits of the selection.");
			Assert.AreEqual(2,countSelections,sizeName);
			Assert.IsTrue(listCapturedAfterPress.All(x => x),sizeName+": the content has the capture from each press.");
			Assert.IsTrue(listCapturedInHandlers.All(x => x==null),sizeName+": the handlers ran under WPF's capture: "+string.Join(", ",listCapturedInHandlers));
			Assert.AreEqual(4,listCapturedInHandlers.Count,sizeName);
			wrapper.Dispose();
			form.Close();
		}
	}
}
