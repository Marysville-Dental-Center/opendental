// Mouse tests of the WPF chart (package TC-3, docs/specs/wpf-tooth-chart-plan.md section 7 item 7): the shared ToothChartMouse with synthesised
// input gives the selection, the drag fill-in and the SegmentDrawn strings that ToothChartData's own functions give for the same points, and the
// DirectX chart gives the same for the same input.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;
using OpenPmsDentalBusiness;
using SparksToothChart;
using SparksToothChart.Wpf3D;

namespace ToothChartImageTests {
	[TestClass]
	public class ToothChartMouseTests {
		///<summary>What a mouse raised and asked for.</summary>
		private class MouseLog {
			public List<string> ListSegments=new List<string>();
			public int CountSelectionsChanged;
			public int CountChartChanged;
			public int CountStrokeChanged;
		}

		private static ToothChartMouse CreateMouse(MouseLog mouseLog) {
			ToothChartMouse toothChartMouse=new ToothChartMouse();
			toothChartMouse.ActionChartChanged=() => mouseLog.CountChartChanged++;
			toothChartMouse.ActionPenStrokeChanged=() => mouseLog.CountStrokeChanged++;
			toothChartMouse.SegmentDrawn+=(sender,e) => mouseLog.ListSegments.Add(e.DrawingSegement);
			toothChartMouse.ToothSelectionsChanged+=sender => mouseLog.CountSelectionsChanged++;
			return toothChartMouse;
		}

		///<summary>A 2D wrapper of the size with the scene applied through the relay: its TcData is the chart's data in every engine.</summary>
		private static ToothChartWrapper CreatePrepared(string sceneName,ChartSize chartSize) {
			ToothChartWrapper wrapper=new ToothChartWrapper();
			wrapper.Size=chartSize.Size;
			ChartScenes.Prepare(WpfCharts.CreateRelay(wrapper),ChartScenes.Get(sceneName),chartSize.ColorSet);
			return wrapper;
		}

		[TestMethod]
		public void Pointer_Click_TogglesTheToothGetToothAtPointGives_AndCommitsOnMouseUp() {
			StaRunner.Run(() => {
				foreach(ChartSize chartSize in new[] { ChartScenes.SizeModule,ChartScenes.SizePrintout,ChartScenes.SizeSheetTall }) {
					using ToothChartWrapper wrapper=CreatePrepared("select-primary-letter",chartSize);
					ToothChartData tcData=wrapper.TcData;
					int countPoints=0;
					for(int y=0;y<chartSize.Size.Height;y+=7) {
						for(int x=0;x<chartSize.Size.Width;x+=9) {
							tcData.SelectedTeeth.Clear();
							MouseLog mouseLog=new MouseLog();
							ToothChartMouse toothChartMouse=CreateMouse(mouseLog);
							Point point=new Point(x,y);
							string toothExpected=tcData.GetToothAtPoint(point);
							toothChartMouse.MouseDown(tcData,point,chartSize.Size);
							CollectionAssert.AreEqual(new[] { toothExpected },tcData.SelectedTeeth,"Down at "+point+" ("+chartSize.FolderName+")");
							Assert.AreEqual(1,mouseLog.CountChartChanged,"The chart is drawn again on mouse down.");
							Assert.AreEqual(0,mouseLog.CountSelectionsChanged,"Not committed before mouse up.");
							toothChartMouse.MouseUp(tcData,point,chartSize.Size);
							Assert.AreEqual(1,mouseLog.CountSelectionsChanged,"Committed on mouse up.");
							//A second click on the same tooth takes it away again, and that is a change too.
							toothChartMouse.MouseDown(tcData,point,chartSize.Size);
							toothChartMouse.MouseUp(tcData,point,chartSize.Size);
							Assert.AreEqual(0,tcData.SelectedTeeth.Count);
							Assert.AreEqual(2,mouseLog.CountSelectionsChanged);
							countPoints++;
						}
					}
					Assert.IsTrue(countPoints>1000,"Points: "+countPoints);
				}
			});
		}

		[TestMethod]
		public void Pointer_HoverAndAnUnchangedMouseUp_ChangeAndCommitNothing() {
			StaRunner.Run(() => {
				using ToothChartWrapper wrapper=CreatePrepared("empty-universal",ChartScenes.SizeModule);
				ToothChartData tcData=wrapper.TcData;
				MouseLog mouseLog=new MouseLog();
				ToothChartMouse toothChartMouse=CreateMouse(mouseLog);
				//No visible hover, as today: moving over the teeth with no button changes nothing and asks for no image.
				foreach(string toothId in new[] { "3","8","14","19","30" }) {
					toothChartMouse.MouseMove(tcData,GetNumberCentre(tcData,toothId),ChartScenes.SizeModule.Size);
				}
				Assert.AreEqual(0,tcData.SelectedTeeth.Count);
				Assert.AreEqual(0,mouseLog.CountChartChanged);
				//A mouse up whose selection is the one the last mouse down saw commits nothing.
				toothChartMouse.MouseUp(tcData,GetNumberCentre(tcData,"8"),ChartScenes.SizeModule.Size);
				Assert.AreEqual(0,mouseLog.CountSelectionsChanged);
				Assert.IsFalse(toothChartMouse.IsMouseDown);
			});
		}

		[TestMethod]
		public void Pointer_Drag_FillsInTheSkippedTeeth_AsGetAffectedTeethGives() {
			StaRunner.Run(() => {
				//Across the maxillary numbers from 3 to 12 in steps that skip teeth: every tooth from 3 to 12 ends up selected.
				using(ToothChartWrapper wrapper=CreatePrepared("empty-universal",ChartScenes.SizeModule)) {
					ToothChartData tcData=wrapper.TcData;
					List<Point> listPoints=GetDragPoints(GetNumberCentre(tcData,"3"),GetNumberCentre(tcData,"12"),5);
					MouseLog mouseLog=new MouseLog();
					List<string> listExpected=ReplayDirectXDrag(tcData,listPoints);
					DriveDrag(CreateMouse(mouseLog),tcData,listPoints,ChartScenes.SizeModule.Size);
					CollectionAssert.AreEquivalent(listExpected,tcData.SelectedTeeth,"As DirectX's MouseMove with ToothChartData's functions.");
					CollectionAssert.AreEquivalent(Enumerable.Range(3,10).Select(x => x.ToString()).ToList(),tcData.SelectedTeeth,"3 to 12, the skipped ones filled in.");
					Assert.AreEqual(1,mouseLog.CountSelectionsChanged);
				}
				//The primary band of a mixed dentition: a drag over the letters takes the primary teeth.
				using(ToothChartWrapper wrapper=CreatePrepared("primary-dentition",ChartScenes.SizeModule)) {
					ToothChartData tcData=wrapper.TcData;
					List<Point> listPoints=GetDragPoints(GetNumberCentre(tcData,"A"),GetNumberCentre(tcData,"J"),4);
					List<string> listExpected=ReplayDirectXDrag(tcData,listPoints);
					DriveDrag(CreateMouse(new MouseLog()),tcData,listPoints,ChartScenes.SizeModule.Size);
					CollectionAssert.AreEquivalent(listExpected,tcData.SelectedTeeth);
					CollectionAssert.AreEquivalent(new[] { "A","B","C","D","E","F","G","H","I","J" },tcData.SelectedTeeth,"A to J.");
				}
			});
		}

		[TestMethod]
		public void Pen_Stroke_GivesTheSegmentDrawnStringOfGetPointMouseScaled() {
			StaRunner.Run(() => {
				CultureInfo cultureOld=Thread.CurrentThread.CurrentCulture;
				try {
					Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
					foreach(ChartSize chartSize in new[] { ChartScenes.SizeModule,ChartScenes.SizePrintout }) {
						using ToothChartWrapper wrapper=CreatePrepared("empty-universal",chartSize);
						ToothChartData tcData=wrapper.TcData;
						tcData.CursorTool=CursorTool.Pen;
						MouseLog mouseLog=new MouseLog();
						ToothChartMouse toothChartMouse=CreateMouse(mouseLog);
						Point[] arrayPoints={ new Point(40,60),new Point(77,61),new Point(123,58),new Point(250,185) };
						toothChartMouse.MouseMove(tcData,new Point(1,1),chartSize.Size);
						Assert.AreEqual(0,tcData.PointList.Count,"No point without the button down.");
						toothChartMouse.MouseDown(tcData,arrayPoints[0],chartSize.Size);
						for(int i=1;i<arrayPoints.Length;i++) {
							toothChartMouse.MouseMove(tcData,arrayPoints[i],chartSize.Size);
						}
						Assert.AreEqual(arrayPoints.Length,tcData.PointList.Count,"The stroke in progress.");
						Assert.AreEqual(0,mouseLog.ListSegments.Count,"Nothing is raised before mouse up.");
						toothChartMouse.MouseUp(tcData,arrayPoints[arrayPoints.Length-1],chartSize.Size);
						string expected=string.Join(";",arrayPoints.Select(x => tcData.GetPointMouseScaled(x.X,x.Y,chartSize.Size))
							.Select(x => x.X+","+x.Y));
						CollectionAssert.AreEqual(new[] { expected },mouseLog.ListSegments);
						Assert.AreEqual(0,tcData.PointList.Count,"The stroke is cleared on mouse up.");
						Assert.AreEqual(arrayPoints.Length+1,mouseLog.CountStrokeChanged,"The stroke is drawn again for each point and once when it ends.");
						if(chartSize==ChartScenes.SizeModule) {
							Assert.AreEqual("40,60;77,61;123,58;250,185",expected,"At 410x307 the points are the drawing space's own.");
						}
						else {
							Assert.AreEqual("32.8,49.8;63.1,50.6;100.9,48.1;205,153.5",expected,"At 500x370 they are scaled into 410x307, to tenths.");
						}
					}
				}
				finally {
					Thread.CurrentThread.CurrentCulture=cultureOld;
				}
			});
		}

		[TestMethod]
		public void Eraser_RemovesTheFirstDrawingWithinEightOfTheCursorPlusOffset_OnlyWhileTheButtonIsDown() {
			StaRunner.Run(() => {
				using ToothChartWrapper wrapper=CreatePrepared("drawing-segments",ChartScenes.SizeModule);
				ToothChartData tcData=wrapper.TcData;
				tcData.CursorTool=CursorTool.Eraser;
				List<string> listSegmentsBefore=tcData.DrawingSegmentList.Select(x => x.DrawingSegment).ToList();
				Assert.AreEqual(3,listSegmentsBefore.Count);
				MouseLog mouseLog=new MouseLog();
				ToothChartMouse toothChartMouse=CreateMouse(mouseLog);
				//The red zig-zag's point 90,220: the eraser's centre is the cursor plus 8.49, so the cursor at 81.51,211.51 hits it dead on.
				Point pointNear=new Point(82,212);//the centre at 90.49,220.49, within 8 of 90,220
				Point pointFar=new Point(82+8,212+8);//the centre about 11 from 90,220 and far from every other point
				toothChartMouse.MouseMove(tcData,pointNear,ChartScenes.SizeModule.Size);
				Assert.AreEqual(3,tcData.DrawingSegmentList.Count,"No erasing without the button down.");
				toothChartMouse.MouseDown(tcData,pointFar,ChartScenes.SizeModule.Size);
				Assert.AreEqual(3,tcData.DrawingSegmentList.Count,"Mouse down alone erases nothing, as in DirectX.");
				toothChartMouse.MouseMove(tcData,pointFar,ChartScenes.SizeModule.Size);
				Assert.AreEqual(3,tcData.DrawingSegmentList.Count,"Outside the radius.");
				Assert.AreEqual(-1,ToothChartMouse.FindSegmentNear(tcData,tcData.GetPointMouseScaled(pointFar.X+ToothChartMouse.EraserOffset,
					pointFar.Y+ToothChartMouse.EraserOffset,ChartScenes.SizeModule.Size),ToothChartMouse.EraserRadius));
				toothChartMouse.MouseMove(tcData,pointNear,ChartScenes.SizeModule.Size);
				CollectionAssert.AreEqual(new[] { listSegmentsBefore[1] },mouseLog.ListSegments,"SegmentDrawn with the erased drawing's string.");
				CollectionAssert.AreEqual(new[] { listSegmentsBefore[0],listSegmentsBefore[2] },tcData.DrawingSegmentList.Select(x => x.DrawingSegment).ToList());
				Assert.AreEqual(1,mouseLog.CountChartChanged);
				toothChartMouse.MouseUp(tcData,pointNear,ChartScenes.SizeModule.Size);
				Assert.AreEqual(1,mouseLog.ListSegments.Count,"Mouse up raises nothing for the eraser.");
			});
		}

		[TestMethod]
		public void ColorChanger_RecoloursTheFirstDrawingWithinTwo_AndRaisesItsOldString() {
			StaRunner.Run(() => {
				using ToothChartWrapper wrapper=CreatePrepared("drawing-segments",ChartScenes.SizeModule);
				ToothChartData tcData=wrapper.TcData;
				tcData.CursorTool=CursorTool.ColorChanger;
				tcData.ColorDrawing=Color.Orange;
				MouseLog mouseLog=new MouseLog();
				ToothChartMouse toothChartMouse=CreateMouse(mouseLog);
				string segmentBlue=tcData.DrawingSegmentList[2].DrawingSegment;
				toothChartMouse.MouseDown(tcData,new Point(205,163),ChartScenes.SizeModule.Size);//3 from 205,160: outside the radius of 2
				toothChartMouse.MouseUp(tcData,new Point(205,163),ChartScenes.SizeModule.Size);
				Assert.AreEqual(0,mouseLog.ListSegments.Count);
				Assert.AreEqual(Color.Blue.ToArgb(),tcData.DrawingSegmentList[2].ColorDraw.ToArgb());
				toothChartMouse.MouseDown(tcData,new Point(205,161),ChartScenes.SizeModule.Size);//1 from 205,160
				CollectionAssert.AreEqual(new[] { segmentBlue },mouseLog.ListSegments);
				Assert.AreEqual(Color.Orange.ToArgb(),tcData.DrawingSegmentList[2].ColorDraw.ToArgb());
				Assert.AreEqual(1,mouseLog.CountChartChanged);
			});
		}

		[TestMethod]
		public void Cursors_ComeFromTheEmbeddedFiles_ForTheWrapperAndItsSubclasses() {
			StaRunner.Run(() => {
				foreach(CursorTool cursorTool in new[] { CursorTool.Pen,CursorTool.Eraser,CursorTool.ColorChanger }) {
					string resourceName=ToothChartMouse.GetCursorResourceName(cursorTool);
					Assert.IsTrue(typeof(ToothChartWrapper).Assembly.GetManifestResourceNames().Contains(resourceName),resourceName);
					using(System.IO.Stream stream=ToothChartMouse.GetCursorStream(cursorTool)) {
						Assert.IsNotNull(stream);
					}
					Cursor cursor=ToothChartMouse.GetCursor(cursorTool);
					Assert.AreNotSame(Cursors.Default,cursor,cursorTool.ToString());
				}
				Assert.IsNull(ToothChartMouse.GetCursorResourceName(CursorTool.Pointer));
				Assert.AreSame(Cursors.Default,ToothChartMouse.GetCursor(CursorTool.Pointer));
				//A subclass of the wrapper (here the tests' own) gets the cursors too: they are loaded from the chart's assembly, not the runtime type's.
				using(TestWrapper wrapper=new TestWrapper()) {
					wrapper.CursorTool=CursorTool.Pen;
					Assert.AreNotSame(Cursors.Default,wrapper.Cursor);
					wrapper.CursorTool=CursorTool.Pointer;
					Assert.AreSame(Cursors.Default,wrapper.Cursor);
				}
			});
		}

		[TestMethod]
		public void ImageHost_DrivesTheSharedMouse_AndTheWrapperRaisesItsEvents() {
			StaRunner.Run(() => {
				using ToothChartWrapper wrapper=WpfCharts.CreateWrapper(ChartScenes.SizeModule.Size);
				ToothChartRelay relay=WpfCharts.CreateRelay(wrapper);
				ChartScenes.Prepare(relay,ChartScenes.Get("drawing-segments"),ChartColorSet.Module);
				int countSelections=0;
				List<string> listSegments=new List<string>();
				wrapper.ToothSelectionsChanged+=sender => countSelections++;
				wrapper.SegmentDrawn+=(sender,e) => listSegments.Add(e.DrawingSegement);
				ToothChartImageHost toothChartImageHost=wrapper.ImageHostWpf;
				toothChartImageHost.Size=wrapper.Size;
				Point point=GetNumberCentre(wrapper.TcData,"14");
				RaiseMouse(toothChartImageHost,"OnMouseDown",point);
				RaiseMouse(toothChartImageHost,"OnMouseUp",point);
				CollectionAssert.AreEqual(new[] { "14" },wrapper.SelectedTeeth);
				Assert.AreEqual(1,countSelections);
				Assert.IsTrue(wrapper.IsWpfSceneDirty,"The selection asks for a new scene.");
				relay.CursorTool=CursorTool.Pen;
				RaiseMouse(toothChartImageHost,"OnMouseDown",new Point(10,10));
				RaiseMouse(toothChartImageHost,"OnMouseMove",new Point(20,12));
				RaiseMouse(toothChartImageHost,"OnMouseUp",new Point(20,12));
				CollectionAssert.AreEqual(new[] { ToothChartMouse.GetDrawingSegment(wrapper.TcData,new List<PointF> { new PointF(10,10),new PointF(20,12) },
					ChartScenes.SizeModule.Size) },listSegments);
			});
		}

		[TestMethod]
		public void DirectX_GivesTheSameSelectionsAndStrings_ForTheSameInput() {
			if(!Environment.UserInteractive) {
				Assert.Inconclusive("Needs an interactive desktop session: the DirectX chart draws on a shown window.");
			}
			if(!ToothChartWrapper.IsDirectXRuntimeAvailable()) {
				Assert.Inconclusive(ToothChartWrapper.D3DX_DLL+" is not installed: no DirectX chart to compare with.");
			}
			StaRunner.Run(() => {
				using Form form=WpfCharts.CreateOffscreenForm(ChartScenes.SizeModule.Size);
				form.Show();
				string directXFormat=DirectXCharts.FindFormat(form);
				if(directXFormat==null) {
					Assert.Inconclusive("No standard DirectX format starts on this computer.");
				}
				using ToothChartWrapper wrapperDirectX=DirectXCharts.CreateWrapper(form,ChartScenes.SizeModule.Size,directXFormat);
				Assert.AreEqual(ToothChartEngine.DirectX,wrapperDirectX.Engine,"DirectX started.");
				Control controlDirectX=wrapperDirectX.Controls.Cast<Control>().Single();
				using ToothChartWrapper wrapperShared=CreatePrepared("drawing-segments",ChartScenes.SizeModule);
				ToothChartRelay relayDirectX=WpfCharts.CreateRelay(wrapperDirectX);
				ChartScenes.Prepare(relayDirectX,ChartScenes.Get("drawing-segments"),ChartColorSet.Module);
				List<string> listSegmentsDirectX=new List<string>();
				int countSelectionsDirectX=0;
				wrapperDirectX.SegmentDrawn+=(sender,e) => listSegmentsDirectX.Add(e.DrawingSegement);
				wrapperDirectX.ToothSelectionsChanged+=sender => countSelectionsDirectX++;
				MouseLog mouseLog=new MouseLog();
				ToothChartMouse toothChartMouse=CreateMouse(mouseLog);
				ToothChartData tcDataShared=wrapperShared.TcData;
				Size size=ChartScenes.SizeModule.Size;
				//The same input to both: hovers, clicks, a drag, a pen stroke, the colour changer and the eraser.
				List<Tuple<CursorTool,string,Point>> listInput=new List<Tuple<CursorTool,string,Point>>();
				Point point3=GetNumberCentre(tcDataShared,"3");
				Point point12=GetNumberCentre(tcDataShared,"12");
				Point point30=GetNumberCentre(tcDataShared,"30");
				listInput.Add(Tuple.Create(CursorTool.Pointer,"move",point30));
				listInput.Add(Tuple.Create(CursorTool.Pointer,"down",point30));
				listInput.Add(Tuple.Create(CursorTool.Pointer,"up",point30));
				listInput.Add(Tuple.Create(CursorTool.Pointer,"move",point3));
				listInput.Add(Tuple.Create(CursorTool.Pointer,"down",point3));
				foreach(Point point in GetDragPoints(point3,point12,5).Skip(1)) {
					listInput.Add(Tuple.Create(CursorTool.Pointer,"move",point));
				}
				listInput.Add(Tuple.Create(CursorTool.Pointer,"up",point12));
				listInput.Add(Tuple.Create(CursorTool.Pen,"down",new Point(30,280)));
				listInput.Add(Tuple.Create(CursorTool.Pen,"move",new Point(45,284)));
				listInput.Add(Tuple.Create(CursorTool.Pen,"move",new Point(63,290)));
				listInput.Add(Tuple.Create(CursorTool.Pen,"up",new Point(63,290)));
				listInput.Add(Tuple.Create(CursorTool.ColorChanger,"down",new Point(205,161)));
				listInput.Add(Tuple.Create(CursorTool.ColorChanger,"up",new Point(205,161)));
				listInput.Add(Tuple.Create(CursorTool.Eraser,"down",new Point(82,212)));
				listInput.Add(Tuple.Create(CursorTool.Eraser,"move",new Point(82,212)));
				listInput.Add(Tuple.Create(CursorTool.Eraser,"up",new Point(82,212)));
				foreach(Tuple<CursorTool,string,Point> input in listInput) {
					wrapperDirectX.CursorTool=input.Item1;
					tcDataShared.CursorTool=input.Item1;
					tcDataShared.ColorDrawing=Color.Orange;
					wrapperDirectX.ColorDrawing=Color.Orange;
					string method=input.Item2=="down"?"OnMouseDown":input.Item2=="up"?"OnMouseUp":"OnMouseMove";
					RaiseMouse(controlDirectX,method,input.Item3);
					Application.DoEvents();
					if(input.Item2=="down") {
						toothChartMouse.MouseDown(tcDataShared,input.Item3,size);
					}
					else if(input.Item2=="up") {
						toothChartMouse.MouseUp(tcDataShared,input.Item3,size);
					}
					else {
						toothChartMouse.MouseMove(tcDataShared,input.Item3,size);
					}
					CollectionAssert.AreEquivalent(wrapperDirectX.TcData.SelectedTeeth,tcDataShared.SelectedTeeth,input.Item1+" "+input.Item2+" at "+input.Item3);
				}
				CollectionAssert.AreEqual(listSegmentsDirectX,mouseLog.ListSegments,"The same SegmentDrawn strings, in order.");
				Assert.AreEqual(countSelectionsDirectX,mouseLog.CountSelectionsChanged,"The same commits of the selection.");
				CollectionAssert.AreEqual(wrapperDirectX.TcData.DrawingSegmentList.Select(x => x.DrawingSegment+"|"+x.ColorDraw.ToArgb()).ToList(),
					tcDataShared.DrawingSegmentList.Select(x => x.DrawingSegment+"|"+x.ColorDraw.ToArgb()).ToList(),"The same drawings, recoloured and erased alike.");
				Assert.IsTrue(listSegmentsDirectX.Count==3,"The pen stroke, the recoloured drawing and the erased one: "+string.Join(" / ",listSegmentsDirectX));
				Assert.IsTrue(countSelectionsDirectX==2 && tcDataShared.SelectedTeeth.Count==11,"30, then 3 to 12: "+string.Join(",",tcDataShared.SelectedTeeth));
				form.Close();
			});
		}

		#region Helpers
		///<summary>The centre of a tooth's number box, in the chart's pixels, as the chart computes the box (DirectX's measure rule).</summary>
		public static Point GetNumberCentre(ToothChartData tcData,string toothId) {
			ToothScene toothScene=ToothSceneBuilder.Build(tcData);
			ToothSceneItem2D item=toothScene.Items2D.First(x => x.Source=="number "+toothId);
			return new Point(item.Origin.X+item.WidthPixels/2,item.Origin.Y+item.Font.Height/2);
		}

		///<summary>Points from start to end in steps of about stepTeeth teeth' width... in countSteps equal steps, the start included.</summary>
		public static List<Point> GetDragPoints(Point pointStart,Point pointEnd,int countSteps) {
			List<Point> listPoints=new List<Point>();
			for(int i=0;i<=countSteps;i++) {
				listPoints.Add(new Point(pointStart.X+(pointEnd.X-pointStart.X)*i/countSteps,pointStart.Y+(pointEnd.Y-pointStart.Y)*i/countSteps));
			}
			return listPoints;
		}

		///<summary>DirectX's pointer logic for a drag over the points (a hover at the first, then down there, moves to the others), written out with
		///ToothChartData's own functions on a copy of the selection: the selection DirectX ends with.</summary>
		private static List<string> ReplayDirectXDrag(ToothChartData tcData,List<Point> listPoints) {
			List<string> listSelected=new List<string>(tcData.SelectedTeeth);
			string hotToothOld=tcData.GetToothAtPoint(listPoints[0]);//the hover before the press
			string toothClicked=tcData.GetToothAtPoint(listPoints[0]);
			Toggle(listSelected,toothClicked);
			foreach(Point point in listPoints.Skip(1)) {
				string hotTooth=tcData.GetToothAtPoint(point);
				if(hotTooth==hotToothOld) {
					continue;
				}
				foreach(string toothId in tcData.GetAffectedTeeth(hotToothOld,hotTooth,tcData.PointPixToMm(point).Y)) {
					Toggle(listSelected,toothId);
				}
				hotToothOld=hotTooth;
			}
			return listSelected;
		}

		private static void Toggle(List<string> listSelected,string toothId) {
			if(listSelected.Contains(toothId)) {
				listSelected.Remove(toothId);
			}
			else {
				listSelected.Add(toothId);
			}
		}

		private static void DriveDrag(ToothChartMouse toothChartMouse,ToothChartData tcData,List<Point> listPoints,Size size) {
			toothChartMouse.MouseMove(tcData,listPoints[0],size);//the hover before the press
			toothChartMouse.MouseDown(tcData,listPoints[0],size);
			foreach(Point point in listPoints.Skip(1)) {
				toothChartMouse.MouseMove(tcData,point,size);
			}
			toothChartMouse.MouseUp(tcData,listPoints[listPoints.Count-1],size);
		}

		///<summary>Raises a control's protected OnMouseDown, OnMouseMove or OnMouseUp with the left button at the point: synthesised input that runs
		///the control's own handlers.</summary>
		public static void RaiseMouse(Control control,string methodName,Point point) {
			MethodInfo methodInfo=typeof(Control).GetMethod(methodName,BindingFlags.Instance|BindingFlags.NonPublic,null,new[] { typeof(MouseEventArgs) },null);
			MouseButtons mouseButtons=methodName=="OnMouseMove"?MouseButtons.None:MouseButtons.Left;
			methodInfo.Invoke(control,new object[] { new MouseEventArgs(mouseButtons,methodName=="OnMouseMove"?0:1,point.X,point.Y,0) });
		}
		#endregion Helpers
	}
}
