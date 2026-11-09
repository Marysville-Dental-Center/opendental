using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace SparksToothChart {
	///<summary>The mouse of the WPF chart (docs/specs/wpf-tooth-chart-plan.md, TC-3), shared by its two engine controls: the image host that shows the
	///renderer's image and the live view. It is the DirectX chart's mouse logic (ToothChartDirectX MouseDown, MouseMove and MouseUp) with no drawing
	///in it: the pointer selects with the same GetToothAtPoint and fills in a drag with the same GetAffectedTeeth; the pen collects its points and
	///raises SegmentDrawn on mouse up with the same string (GetPointMouseScaled into the 410x307 drawing space, "x,y" joined by ';'); the eraser
	///(radius 8 at the cursor plus 8.49) and the colour changer (radius 2) raise SegmentDrawn with the string of the drawing they touch, then remove
	///it or give it the pen colour. ToothSelectionsChanged is raised on mouse up when the selection changed. Every button acts, as in DirectX.
	///The control asks for a new image through ActionChartChanged (selection or drawings changed) and redraws only the stroke in progress through
	///ActionPenStrokeChanged, as DirectX draws the stroke over the teeth without drawing them again. No visible hover, as today. The 2D chart keeps
	///its own mouse code: it toggles only the tooth under the cursor, a difference that stays. The DirectX chart keeps its own handlers until TC-9a
	///deletes it; ToothChartImageTests drives both with the same input.</summary>
	public class ToothChartMouse {
		///<summary>The eraser's radius in the 410x307 drawing space, and the offset of its centre from the cursor in pixels (the centre of
		///EraseCircle.cur), both by trial and error for the best feel (DirectX).</summary>
		public const float EraserRadius=8f;
		public const float EraserOffset=8.49f;
		///<summary>The colour changer's radius in the 410x307 drawing space (DirectX).</summary>
		public const float ColorChangerRadius=2f;
		///<summary>Mouse move sets this to the tooth under the cursor.</summary>
		private string _hotTooth;
		///<summary>The previous hot tooth. When it differs from _hotTooth, the mouse has just moved to another tooth.</summary>
		private string _hotToothOld;
		private bool _isMouseDown;
		private List<string> _listSelectedTeethOld=new List<string>();
		///<summary>Called after the mouse changed the selection or the drawings, so the control shows a new image.</summary>
		public Action ActionChartChanged;
		///<summary>Called after the pen added a point to the stroke in progress (TcData.PointList), or the stroke ended, so the control draws the stroke
		///again over its image.</summary>
		public Action ActionPenStrokeChanged;
		///<summary>On mouse up that ends a drawing segment, and when the eraser or the colour changer touches one (the drawing's old string).</summary>
		public event ToothChartDrawEventHandler SegmentDrawn=null;
		///<summary>On mouse up that commits a changed tooth selection.</summary>
		public event ToothChartSelectionEventHandler ToothSelectionsChanged=null;

		///<summary>True between mouse down and mouse up.</summary>
		public bool IsMouseDown {
			get {
				return _isMouseDown;
			}
		}

		///<summary>DirectX's MouseDown at location (control pixels) on a chart of sizeControl pixels.</summary>
		public void MouseDown(ToothChartData tcData,Point location,Size sizeControl) {
			_isMouseDown=true;
			if(tcData==null) {
				return;
			}
			if(tcData.CursorTool==CursorTool.Pointer) {
				_listSelectedTeethOld=tcData.SelectedTeeth.FindAll(x => x!=null);//Make a copy of the list.  No elements should ever be null (copy all).
				string toothClicked=tcData.GetToothAtPoint(location);
				tcData.SetSelected(toothClicked,!tcData.SelectedTeeth.Contains(toothClicked));
				ActionChartChanged?.Invoke();
			}
			else if(tcData.CursorTool==CursorTool.Pen) {
				tcData.PointList.Add(new PointF(location.X,location.Y));
				ActionPenStrokeChanged?.Invoke();
			}
			else if(tcData.CursorTool==CursorTool.ColorChanger) {
				//look for any lines near the "wand". Since the line segments are so short, it's sufficient to check end points.
				PointF pointMouseScaled=tcData.GetPointMouseScaled(location.X,location.Y,sizeControl);
				int indexSegment=FindSegmentNear(tcData,pointMouseScaled,ColorChangerRadius);
				if(indexSegment>=0) {
					OnSegmentDrawn(tcData.DrawingSegmentList[indexSegment].DrawingSegment);
					tcData.DrawingSegmentList[indexSegment].ColorDraw=tcData.ColorDrawing;
					ActionChartChanged?.Invoke();
				}
			}
			//Eraser: nothing until the mouse moves. MoveText: TC-5.
		}

		///<summary>DirectX's MouseMove.</summary>
		public void MouseMove(ToothChartData tcData,Point location,Size sizeControl) {
			if(tcData==null || tcData.ListToothGraphics.Count==0) {
				return;//can happen during forced close of perio chart.
			}
			if(tcData.CursorTool==CursorTool.Pointer) {
				_hotTooth=tcData.GetToothAtPoint(location);
				if(_hotTooth==_hotToothOld) {//mouse has not moved to another tooth
					return;
				}
				if(_isMouseDown) {//drag action
					List<string> listAffectedTeeth=tcData.GetAffectedTeeth(_hotToothOld,_hotTooth,tcData.PointPixToMm(location).Y);
					for(int i=0;i<listAffectedTeeth.Count;i++) {
						tcData.SetSelected(listAffectedTeeth[i],!tcData.SelectedTeeth.Contains(listAffectedTeeth[i]));
					}
					_hotToothOld=_hotTooth;
					ActionChartChanged?.Invoke();
				}
				else {
					_hotToothOld=_hotTooth;
				}
			}
			else if(tcData.CursorTool==CursorTool.Pen) {
				if(!_isMouseDown) {
					return;
				}
				tcData.PointList.Add(new PointF(location.X,location.Y));
				ActionPenStrokeChanged?.Invoke();
			}
			else if(tcData.CursorTool==CursorTool.Eraser) {
				if(!_isMouseDown) {
					return;
				}
				//look for any lines that intersect the "eraser". Since the line segments are so short, it's sufficient to check end points.
				PointF pointEraser=tcData.GetPointMouseScaled(location.X+EraserOffset,location.Y+EraserOffset,sizeControl);
				int indexSegment=FindSegmentNear(tcData,pointEraser,EraserRadius);
				if(indexSegment>=0) {
					OnSegmentDrawn(tcData.DrawingSegmentList[indexSegment].DrawingSegment);//triggers a deletion from db.
					tcData.DrawingSegmentList.RemoveAt(indexSegment);
					ActionChartChanged?.Invoke();
				}
			}
		}

		///<summary>DirectX's MouseUp.</summary>
		public void MouseUp(ToothChartData tcData,Point location,Size sizeControl) {
			_isMouseDown=false;
			if(tcData==null) {
				return;
			}
			if(tcData.CursorTool==CursorTool.Pointer) {
				if(tcData.HasSelectedTeethChanged(_listSelectedTeethOld)) {
					OnToothSelectionsChanged();
				}
			}
			else if(tcData.CursorTool==CursorTool.Pen) {
				string drawingSegment=GetDrawingSegment(tcData,tcData.PointList,sizeControl);
				OnSegmentDrawn(drawingSegment);
				tcData.PointList=new List<PointF>();
				ActionPenStrokeChanged?.Invoke();
				ActionChartChanged?.Invoke();
			}
		}

		private void OnSegmentDrawn(string drawingSegment) {
			SegmentDrawn?.Invoke(this,new ToothChartDrawEventArgs(drawingSegment));
		}

		private void OnToothSelectionsChanged() {
			ToothSelectionsChanged?.Invoke(this);
		}

		///<summary>The string DirectX's MouseUp gives a pen stroke: each point scaled into the 410x307 drawing space (GetPointMouseScaled, rounded to
		///tenths), "x,y" in the workstation's number format as DirectX writes it, joined by ';'.</summary>
		public static string GetDrawingSegment(ToothChartData tcData,List<PointF> listPoints,Size sizeControl) {
			string drawingSegment="";
			for(int i=0;i<listPoints.Count;i++) {
				if(i>0) {
					drawingSegment+=";";
				}
				PointF pointMouseScaled=tcData.GetPointMouseScaled(listPoints[i].X,listPoints[i].Y,sizeControl);
				drawingSegment+=pointMouseScaled.X+","+pointMouseScaled.Y;
			}
			return drawingSegment;
		}

		///<summary>The index of the first drawing with an end point within radius of point (in the 410x307 drawing space), or -1, as DirectX tests a
		///circle against the drawings' points.</summary>
		public static int FindSegmentNear(ToothChartData tcData,PointF point,float radius) {
			for(int i=0;i<tcData.DrawingSegmentList.Count;i++) {
				string[] arrayPointStr=tcData.DrawingSegmentList[i].DrawingSegment.Split(';');
				for(int p=0;p<arrayPointStr.Length;p++) {
					string[] arrayXy=arrayPointStr[p].Split(',');
					float x;
					float y;
					if(arrayXy.Length==2 && float.TryParse(arrayXy[0],out x) && float.TryParse(arrayXy[1],out y)) {
						float dist=(float)Math.Sqrt(Math.Pow(Math.Abs(x-point.X),2)+Math.Pow(Math.Abs(y-point.Y),2));
						if(dist<=radius) {//testing circle intersection here
							return i;
						}
					}
				}
			}
			return -1;
		}

		///<summary>The tools with a cursor file of their own: the pen, the eraser and the colour changer.</summary>
		public static readonly CursorTool[] ArrayCursorTools={ CursorTool.Pen,CursorTool.Eraser,CursorTool.ColorChanger };
		///<summary>For tests only, null in the program: runs on this thread before a tool's embedded cursor file is opened (GetCursorStream, for the
		///WinForms cursors of GetCursor and the live view's WPF cursors), so a test can make the cursors fail to load. Thread-static.</summary>
		[ThreadStatic]
		public static Action<CursorTool> ActionOpenCursorForTests;
		///<summary>The WinForms cursors of the tools on this thread (GetCursor); null until a tool with a cursor file is first chosen.</summary>
		[ThreadStatic]
		private static ToothChartCursorCache<Cursor> _cursorCacheThread;

		///<summary>The manifest name of the embedded cursor of a tool, as the wrapper's CursorTool loads it ("SparksToothChart.Pen.cur" and so on), or
		///null for the pointer and the text mover, which use the arrow.</summary>
		public static string GetCursorResourceName(CursorTool cursorTool) {
			switch(cursorTool) {
				case CursorTool.Pen:
					return "SparksToothChart.Pen.cur";
				case CursorTool.Eraser:
					return "SparksToothChart.EraseCircle.cur";
				case CursorTool.ColorChanger:
					return "SparksToothChart.ColorChanger.cur";
				default:
					return null;
			}
		}

		///<summary>The embedded .cur file of a tool, or null for the arrow. The caller disposes it.</summary>
		public static Stream GetCursorStream(CursorTool cursorTool) {
			string resourceName=GetCursorResourceName(cursorTool);
			if(resourceName==null) {
				return null;
			}
			ActionOpenCursorForTests?.Invoke(cursorTool);
			return typeof(ToothChartMouse).Assembly.GetManifestResourceStream(resourceName);
		}

		///<summary>The WinForms cursor of a tool, from its embedded .cur file (the arrow for the pointer and the text mover). The three cursors are made
		///together on this thread the first time one is asked for, and kept, so a tool change makes no new cursor handle. A cursor that cannot be made is
		///the arrow until a later choice of its tool makes it, and the failure is logged once (ToothChartCursorCache). Never throws.</summary>
		public static Cursor GetCursor(CursorTool cursorTool) {
			if(_cursorCacheThread==null) {
				_cursorCacheThread=new ToothChartCursorCache<Cursor>(stream => new Cursor(stream),"The tooth chart");
			}
			return _cursorCacheThread.Get(cursorTool)??Cursors.Default;
		}
	}

	///<summary>The cursors of the tools with a cursor file (ToothChartMouse.ArrayCursorTools) on one thread, of one kind: the wrapper's WinForms cursors
	///(ToothChartMouse.GetCursor) or the live view's WPF cursors (ToothChartWpfView.GetCursorWpf), which share this loop. The three are made from their
	///embedded .cur files the first time one is asked for, and kept, so a tool change makes no new cursor: WPF's Cursor(Stream) writes the stream to a
	///temporary file each time it makes a cursor (Path.GetTempFileName, which fails once the user's temp folder holds 65,535 .tmp files). A cursor that
	///cannot be made is null (the caller's arrow) for now and is tried again, alone, the next time its tool is asked for, so a fault that passes (a temp
	///folder that is cleared) does not leave the arrow for the rest of the thread's life, which on the UI thread is the whole session. Only the failures
	///of the first try that fails are logged (ToothChartFallbackKind.Cursor): one line for this thread's cursors of this kind, however often they are
	///tried. Never throws.</summary>
	internal sealed class ToothChartCursorCache<TCursor> where TCursor:class {
		private readonly Dictionary<CursorTool,TCursor> _dictCursorsMade=new Dictionary<CursorTool,TCursor>();
		private readonly Func<Stream,TCursor> _funcMakeCursor;
		///<summary>The start of the log line, naming whose cursors these are ("The tooth chart").</summary>
		private readonly string _textWhose;
		///<summary>True once the three cursors were first tried; after that only a cursor that failed is tried again, alone.</summary>
		private bool _isTried;
		private bool _isFailureLogged;

		///<summary>funcMakeCursor makes a cursor of this kind from a .cur file's stream, or throws.</summary>
		public ToothChartCursorCache(Func<Stream,TCursor> funcMakeCursor,string textWhose) {
			_funcMakeCursor=funcMakeCursor;
			_textWhose=textWhose;
		}

		///<summary>The cursor of the tool, or null: for the pointer and the text mover (the arrow), and for a tool whose cursor could not be made, which is
		///tried again at each such call until it is made.</summary>
		public TCursor Get(CursorTool cursorTool) {
			if(ToothChartMouse.GetCursorResourceName(cursorTool)==null) {
				return null;
			}
			TCursor cursor;
			if(_dictCursorsMade.TryGetValue(cursorTool,out cursor)) {
				return cursor;
			}
			CursorTool[] arrayCursorToolsToMake=_isTried?new[] { cursorTool }:ToothChartMouse.ArrayCursorTools;
			_isTried=true;
			List<string> listFailures=new List<string>();
			foreach(CursorTool cursorToolToMake in arrayCursorToolsToMake) {
				try {
					using(Stream stream=ToothChartMouse.GetCursorStream(cursorToolToMake)) {
						if(stream==null) {
							throw new FileNotFoundException("The embedded cursor file "+ToothChartMouse.GetCursorResourceName(cursorToolToMake)+" is missing.");
						}
						_dictCursorsMade[cursorToolToMake]=_funcMakeCursor(stream);
					}
				}
				catch(Exception ex) {
					listFailures.Add(cursorToolToMake+" ("+ex.GetType().Name+": "+ex.Message+")");
				}
			}
			if(listFailures.Count>0 && !_isFailureLogged) {
				_isFailureLogged=true;
				ToothChartWrapper.LogFallbackOnce(ToothChartFallbackKind.Cursor,_textWhose+" could not make the cursor of "+string.Join(", ",listFailures)
					+". It shows the arrow for "+(listFailures.Count==1?"that tool":"those tools")+" until a later choice of the tool makes the cursor.");
			}
			_dictCursorsMade.TryGetValue(cursorTool,out cursor);
			return cursor;
		}
	}
}
