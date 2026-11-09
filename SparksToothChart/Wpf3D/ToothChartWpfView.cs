using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace SparksToothChart.Wpf3D {
	///<summary>The WPF chart's live view on screen (docs/specs/wpf-tooth-chart-plan.md, O-19 (b), decided after TC-1's times): an ElementHost whose
	///WPF content is a Viewport3D with the scene's 3D layer and over it the 2D layer as an element of its own, which draws the scene's 2D items with
	///the renderer's own code (ChartLayer2D), and above that the pen stroke in progress on a third element. WPF's own render thread draws it on
	///screen, as it draws every WPF window of the process: in OpenPmsDental.exe that is WPF's software rasterizer, at 1x and with no edge smoothing,
	///because the program sets RenderOptions.ProcessRenderMode to SoftwareOnly for the whole process (ProgramEntry.Main, from upstream; a window's
	///own HwndTarget.RenderMode cannot turn hardware back on, O-34). Only a process that leaves hardware rendering on draws it with the graphics card.
	///The wrapper makes it at its first rebuild on screen (ToothChartWrapper.RebuildWpfOnScreen, which the message loop runs), only when it is shown
	///on an STA thread and not drawn in software (ToothChartWrapper.WpfDrawInSoftware, which shows the renderer's smoothed image instead); every image
	///(GetBitmap, prints, sheets, the dashboard) still comes from the software renderer, so a hidden wrapper, a wrapper on an MTA thread and the
	///print path (a print wrapper shown and disposed before the message loop runs) never make one.
	///A scene whose 3D layer is the one shown (ToothScene.IsSame3DLayer: a change of selection, drawings or text) only draws the 2D layer again; any
	///other scene builds its frozen models from the meshes and materials this thread keeps (a refill changes materials, visibility and transforms
	///only; the meshes are built once per thread and frozen). The 3D layer is kept in a bitmap cache (Is3DLayerCached), so a frame that redraws
	///only the 2D layer or the pen stroke composites the cached 3D image under it instead of rasterizing the 3D layer again, and the pen stroke
	///has an element of its own, so a pen move redraws only the stroke. The content is laid out in the chart's pixels and scaled by the inverse of
	///the window's DPI scale, so one scene unit is one device pixel at any DPI, as the wrapper's pixel size and the mouse expect; the scale is set
	///again when the DPI changes without a new scene (the window moved to a monitor of another scale, OnDpiChanged of the content's root).
	///The mouse is the shared ToothChartMouse, on the WPF content's mouse events (captured from button down to button up), at the points DirectX's
	///handlers would get: the press and release are taken before the capture changes, the capture is released before the shared class's MouseUp
	///(so the handlers of SegmentDrawn and ToothSelectionsChanged run without WPF's capture), and the MouseMove WPF raises on its own when the capture
	///changes (no WM_MOUSEMOVE made it) is not handed on. The cursor is the embedded .cur file of the tool, made once per thread (GetCursorWpf). The
	///content's background is the chart's from the start (TcData). The WPF content takes no keyboard focus: the chart handles no keys.</summary>
	public class ToothChartWpfView:ElementHost {
		#region Fields
		///<summary>The meshes and materials of this thread: the live views of one UI thread share them, and they never cross to another thread.</summary>
		[ThreadStatic]
		private static ToothMeshCache _toothMeshCacheThread;
		[ThreadStatic]
		private static ToothMaterialCache _toothMaterialCacheThread;
		///<summary>The WPF cursors of the pen, the eraser and the colour changer on this thread, made once (GetCursorWpf); null until a tool other than
		///the pointer is first chosen. A tool whose cursor could not be made is in it as null, the arrow.</summary>
		[ThreadStatic]
		private static Dictionary<CursorTool,System.Windows.Input.Cursor> _dictCursorsThread;
		///<summary>For tests only, null in the program: runs on this thread at the start of a live view's constructor (with StepCreate) and of its
		///ShowScene (with StepShowScene), with the view, so a test can make either fail. Thread-static: it reaches only the views of the thread that set
		///it.</summary>
		[ThreadStatic]
		public static Action<ToothChartWpfView,string> ActionStepForTests;
		public const string StepCreate="create";
		public const string StepShowScene="show scene";
		private ToothChartData _tcData;
		///<summary>Called after the mouse changed the chart's data (selection, drawings), so the wrapper builds the scene again.</summary>
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public Action ActionChartChanged;
		private readonly ToothChartMouse _toothChartMouse=new ToothChartMouse();
		private readonly LiveRoot _gridRoot;
		private readonly Viewport3D _viewport3D;
		///<summary>The 3D layer: the Viewport3D over the scene's background, the size of the chart. The bitmap cache is on this element, so the cache
		///covers whole pixels from 0,0 (the Viewport3D's own bounds are those of its teeth, which need not fall on whole pixels).</summary>
		private readonly Border _border3D;
		private readonly ModelVisual3D _modelVisual3D;
		private readonly LiveLayer2D _liveLayer2D;
		private readonly LivePenLayer _livePenLayer;
		private ToothScene _sceneShown;
		private ToothScene _scene3DShown;
		private int _countScenesShown;
		private int _count3DLayersBuilt;
		private double _millisecondsLast3DLayer;
		private bool _is3DLayerCached;
		private int _countLayer2DRenders;
		private int _countPenLayerRenders;
		///<summary>True while this view takes or releases WPF's mouse capture. WPF raises a MouseMove of its own inside those calls (MouseDevice.Synchronize,
		///at wherever the cursor is then): no WM_MOUSEMOVE made it, so DirectX never saw it, and it is not handed on (a pen stroke gets no point from it).</summary>
		private bool _isChangingCapture;
		#endregion Fields

		public ToothChartWpfView() {
			if(Thread.CurrentThread.GetApartmentState()!=ApartmentState.STA) {
				throw new InvalidOperationException("The WPF tooth chart's live view needs an STA thread.");
			}
			ActionStepForTests?.Invoke(this,StepCreate);
			_viewport3D=new Viewport3D();
			_viewport3D.ClipToBounds=true;
			_viewport3D.IsHitTestVisible=false;
			_modelVisual3D=new ModelVisual3D();
			_viewport3D.Children.Add(_modelVisual3D);
			_border3D=new Border();
			_border3D.HorizontalAlignment=HorizontalAlignment.Left;
			_border3D.VerticalAlignment=VerticalAlignment.Top;
			_border3D.SnapsToDevicePixels=true;
			_border3D.IsHitTestVisible=false;
			_border3D.Child=_viewport3D;
			_liveLayer2D=new LiveLayer2D(this);
			_liveLayer2D.IsHitTestVisible=false;
			_livePenLayer=new LivePenLayer(this);
			_livePenLayer.IsHitTestVisible=false;
			_gridRoot=new LiveRoot(this);
			_gridRoot.HorizontalAlignment=HorizontalAlignment.Left;
			_gridRoot.VerticalAlignment=VerticalAlignment.Top;
			_gridRoot.Focusable=false;
			_gridRoot.SnapsToDevicePixels=true;
			//Hit-testable from the start. TcData's background replaces it before anything is drawn (the wrapper sets TcData before it adds the view), and
			//each scene's after that: the view never shows a colour that is not the chart's.
			_gridRoot.Background=Brushes.Transparent;
			_gridRoot.Children.Add(_border3D);
			_gridRoot.Children.Add(_liveLayer2D);
			_gridRoot.Children.Add(_livePenLayer);
			_gridRoot.MouseDown+=GridRoot_MouseDown;
			_gridRoot.MouseMove+=GridRoot_MouseMove;
			_gridRoot.MouseUp+=GridRoot_MouseUp;
			Child=_gridRoot;
			Is3DLayerCached=true;
			_toothChartMouse.ActionChartChanged=() => ActionChartChanged?.Invoke();
			_toothChartMouse.ActionPenStrokeChanged=() => _livePenLayer.InvalidateVisual();//the stroke alone: the 2D layer and the 3D layer stay
			_toothChartMouse.SegmentDrawn+=(sender,e) => SegmentDrawn?.Invoke(this,e);
			_toothChartMouse.ToothSelectionsChanged+=sender => ToothSelectionsChanged?.Invoke(this);
		}

		#region Events
		[Category("OD"),Description("Occurs on mouse up that ends a drawing segment.")]
		public event ToothChartDrawEventHandler SegmentDrawn=null;

		[Category("OD"),Description("Occurs on mouse up that commits tooth selection.")]
		public event ToothChartSelectionEventHandler ToothSelectionsChanged=null;
		#endregion Events

		#region Properties
		///<summary>The wrapper's data object. Until the first scene is shown, setting it gives the content (and the host's own back colour) the chart's
		///background, the colour every scene of this data is drawn on, so the Chart module never flashes another colour while its chart first comes on
		///screen.</summary>
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public ToothChartData TcData {
			get {
				return _tcData;
			}
			set {
				_tcData=value;
				if(_sceneShown==null && value!=null) {
					BackColor=System.Drawing.Color.FromArgb(255,value.ColorBackground);
					_gridRoot.Background=CreateBrushBackground(value.ColorBackground);
				}
			}
		}

		///<summary>The scene shown, or null before the first.</summary>
		[Browsable(false)]
		public ToothScene SceneShown {
			get {
				return _sceneShown;
			}
		}

		///<summary>How many scenes ShowScene has shown, for tests.</summary>
		[Browsable(false)]
		public int CountScenesShown {
			get {
				return _countScenesShown;
			}
		}

		///<summary>How many times a scene's 3D layer was built (the others kept the shown 3D layer), for tests.</summary>
		[Browsable(false)]
		public int Count3DLayersBuilt {
			get {
				return _count3DLayersBuilt;
			}
		}

		///<summary>The milliseconds the last new 3D layer took on this thread (its frozen models), for the render times.</summary>
		[Browsable(false)]
		public double MillisecondsLast3DLayer {
			get {
				return _millisecondsLast3DLayer;
			}
		}

		///<summary>The number of GeometryModel3D in the 3D layer shown (the scene's 3D items that have a mesh), for tests.</summary>
		[Browsable(false)]
		public int CountModelsShown {
			get {
				Model3DGroup model3DGroup=_modelVisual3D.Content as Model3DGroup;
				if(model3DGroup==null) {
					return 0;
				}
				int count=0;
				foreach(Model3D model3D in model3DGroup.Children) {
					if(model3D is GeometryModel3D) {
						count++;
					}
				}
				return count;
			}
		}

		///<summary>The WPF cursor of the content, for tests.</summary>
		[Browsable(false)]
		public System.Windows.Input.Cursor CursorWpf {
			get {
				return _gridRoot.Cursor;
			}
		}

		///<summary>The DPI scale that the content is laid out for, for tests.</summary>
		[Browsable(false)]
		public double DpiScaleShown {
			get {
				return _gridRoot.LayoutTransform is ScaleTransform scaleTransform && scaleTransform.ScaleX!=0?1/scaleTransform.ScaleX:1;
			}
		}

		///<summary>True (the default) when the 3D layer is kept in a bitmap cache: a BitmapCache on the 3D layer's element (the Viewport3D over the
		///background, the size of the chart), at 1x and snapped to the device pixels, so the cached image covers whole pixels from 0,0 and lands on the
		///pixels the 3D layer itself would. RenderAtScale 1.0 holds at any DPI: WPF rasterizes a cache in the element's own units, which the 1/DPI layout
		///transform makes device pixels, and composites it through the transforms above it (1/DPI, then the window's DPI: 1 in all), so at 150% the cache
		///of a 615-pixel chart has 615 pixels and is not resampled (a test renders the view at 144 dpi with and without it). WPF then rasterizes the 3D
		///layer only when it changes (a refill), and a frame
		///that redraws only the 2D layer or the pen stroke composites the cached image under it. Without the cache every such frame rasterizes the 3D
		///layer again wherever the 2D layer changed, which in software (the program, O-34) costs about as much as a refill. False only for the
		///measurement that compares the two (G1's times).</summary>
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public bool Is3DLayerCached {
			get {
				return _is3DLayerCached;
			}
			set {
				_is3DLayerCached=value;
				if(!value) {
					_border3D.CacheMode=null;
					return;
				}
				BitmapCache bitmapCache=new BitmapCache(1.0);
				bitmapCache.SnapsToDevicePixels=true;
				bitmapCache.Freeze();
				_border3D.CacheMode=bitmapCache;
			}
		}

		///<summary>How many times WPF drew the 2D layer (its OnRender), for tests: a pen move does not draw it.</summary>
		[Browsable(false)]
		public int CountLayer2DRenders {
			get {
				return _countLayer2DRenders;
			}
		}

		///<summary>How many times WPF drew the pen stroke's element (its OnRender), for tests.</summary>
		[Browsable(false)]
		public int CountPenLayerRenders {
			get {
				return _countPenLayerRenders;
			}
		}
		#endregion Properties

		#region Showing
		///<summary>Shows the scene, on this view's thread. A scene with the shown 3D layer only draws the 2D layer again.</summary>
		public void ShowScene(ToothScene scene) {
			if(scene==null || IsDisposed) {
				return;
			}
			ActionStepForTests?.Invoke(this,StepShowScene);
			_countScenesShown++;
			_sceneShown=scene;
			_gridRoot.Width=scene.Size.Width;
			_gridRoot.Height=scene.Size.Height;
			ApplyDpiScale(GetDpiScale());
			BackColor=System.Drawing.Color.FromArgb(255,scene.ColorBackground);
			if(!scene.IsSame3DLayer(_scene3DShown)) {
				Stopwatch stopwatch=Stopwatch.StartNew();
				SolidColorBrush brushBackground=CreateBrushBackground(scene.ColorBackground);
				_gridRoot.Background=brushBackground;
				_border3D.Background=brushBackground;
				_border3D.Width=scene.Size.Width;
				_border3D.Height=scene.Size.Height;
				_viewport3D.Width=scene.Size.Width;
				_viewport3D.Height=scene.Size.Height;
				_viewport3D.Camera=ToothChartRenderer.CreateCamera(scene);
				if(_toothMeshCacheThread==null) {
					_toothMeshCacheThread=new ToothMeshCache();
					_toothMaterialCacheThread=new ToothMaterialCache();
				}
				_modelVisual3D.Content=ToothChartRenderer.BuildModel(scene,_toothMeshCacheThread,_toothMaterialCacheThread);
				_scene3DShown=scene;
				_count3DLayersBuilt++;
				_millisecondsLast3DLayer=stopwatch.Elapsed.TotalMilliseconds;
			}
			_liveLayer2D.Width=scene.Size.Width;
			_liveLayer2D.Height=scene.Size.Height;
			_liveLayer2D.InvalidateVisual();
			_livePenLayer.Width=scene.Size.Width;
			_livePenLayer.Height=scene.Size.Height;
			_livePenLayer.InvalidateVisual();//nothing to draw unless a stroke is in progress
		}

		///<summary>The DPI scale WPF gives the content: the scale of the monitor its window is on, or 1 before the content is in a window. In the program
		///it is the monitor's: WinForms makes the process PerMonitorV2 (OpenPmsDental/app.config:116) and WPF works in DIPs there
		///(WpfControlsOD/Frame/Dpi.cs:29-33, VisualTreeHelper.GetDpi), so on a 150% monitor it is 1.5, and the 1/DPI layout transform (ApplyDpiScale) is
		///what keeps one scene unit on one device pixel; it is not dead code (launch check L-4 looks at 100% and 150%).</summary>
		private double GetDpiScale() {
			try {
				return VisualTreeHelper.GetDpi(_gridRoot).DpiScaleX;
			}
			catch(Exception ex) {
				ex.ToString();//no window yet
			}
			return 1;
		}

		///<summary>Lays the content out at 1/dpiScale, so the chart's pixels (the scene's units, the wrapper's size) are device pixels: a 615-pixel scene
		///at 150% is 410 DIPs, which WPF draws on 615 pixels. ShowScene applies it, and the content's root again when WPF tells it the DPI changed without
		///a new scene (OnDpiChanged: the window moved to a monitor of another scale). The 3D layer's bitmap cache stays at RenderAtScale 1.0 under it:
		///WPF rasterizes a cache in the element's own units (here device pixels) and composites it through the transforms above it, which here come to
		///1 (Is3DLayerCached).</summary>
		private void ApplyDpiScale(double dpiScale) {
			if(!(dpiScale>0) || double.IsInfinity(dpiScale)) {
				dpiScale=1;
			}
			if(_gridRoot.LayoutTransform is ScaleTransform scaleTransform && scaleTransform.ScaleX==1/dpiScale) {
				return;
			}
			ScaleTransform scaleTransformNew=new ScaleTransform(1/dpiScale,1/dpiScale);
			scaleTransformNew.Freeze();
			_gridRoot.LayoutTransform=scaleTransformNew;
		}

		private static SolidColorBrush CreateBrushBackground(System.Drawing.Color color) {
			SolidColorBrush brush=new SolidColorBrush(Color.FromRgb(color.R,color.G,color.B));
			brush.Freeze();
			return brush;
		}

		///<summary>The cursor of the tool (GetCursorWpf; the arrow for the pointer); the text mover keeps the cursor it had, as the wrapper's own cursor
		///does. Never throws.</summary>
		public void SetCursorTool(CursorTool cursorTool) {
			if(cursorTool==CursorTool.MoveText) {
				return;
			}
			_gridRoot.Cursor=GetCursorWpf(cursorTool);
		}

		///<summary>The WPF cursor of a tool on this thread, or null (the arrow) for the pointer and the text mover, and for a tool whose cursor could not be
		///made. The pen's, the eraser's and the colour changer's are made together, once per thread, from their embedded .cur files, the first time
		///one is asked for, and kept: WPF's Cursor(Stream) writes the stream to a temporary file each time it makes a cursor (Path.GetTempFileName, which
		///fails once the user's temp folder holds 65,535 .tmp files), so a cursor made on every tool change could make choosing the pen throw. A cursor
		///that cannot be made is the arrow, and the failure is logged once (ToothChartFallbackKind.Cursor). Never throws.</summary>
		public static System.Windows.Input.Cursor GetCursorWpf(CursorTool cursorTool) {
			if(ToothChartMouse.GetCursorResourceName(cursorTool)==null) {
				return null;
			}
			if(_dictCursorsThread==null) {
				Dictionary<CursorTool,System.Windows.Input.Cursor> dictCursors=new Dictionary<CursorTool,System.Windows.Input.Cursor>();
				List<string> listFailures=new List<string>();
				foreach(CursorTool cursorToolMade in ToothChartMouse.ArrayCursorTools) {
					System.Windows.Input.Cursor cursor=null;
					try {
						using(Stream stream=ToothChartMouse.GetCursorStream(cursorToolMade)) {
							if(stream!=null) {
								cursor=new System.Windows.Input.Cursor(stream);
							}
						}
					}
					catch(Exception ex) {
						listFailures.Add(cursorToolMade+" ("+ex.GetType().Name+": "+ex.Message+")");
					}
					dictCursors[cursorToolMade]=cursor;
				}
				_dictCursorsThread=dictCursors;
				if(listFailures.Count>0) {
					ToothChartWrapper.LogFallbackOnce(ToothChartFallbackKind.Cursor,"The "+ToothChartEngine.Wpf3D+" tooth chart's live view could not make "
						+"the cursor of "+string.Join(", ",listFailures)+". It shows the arrow for "+(listFailures.Count==1?"that tool":"those tools")+".");
				}
			}
			System.Windows.Input.Cursor cursorFound;
			if(_dictCursorsThread.TryGetValue(cursorTool,out cursorFound)) {
				return cursorFound;
			}
			return null;
		}

		///<summary>The content as WPF draws it in software, at the scene's pixel size (the 3D layer at 1x with no edge smoothing, then the 2D layer and
		///the pen stroke): what the program shows on screen, since it draws every WPF window in software (O-34), for tests and gallery G1. No test waits
		///for a frame on screen.</summary>
		public System.Drawing.Bitmap GetBitmapForTests() {
			if(_sceneShown==null) {
				return null;
			}
			_gridRoot.UpdateLayout();
			int width=_sceneShown.Size.Width;
			int height=_sceneShown.Size.Height;
			RenderTargetBitmap renderTargetBitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);
			RenderContent(renderTargetBitmap);
			byte[] arrayPixels=new byte[width*height*4];
			renderTargetBitmap.CopyPixels(arrayPixels,width*4,0);
			ToothChartRenderResult result=new ToothChartRenderResult();
			result.Size=_sceneShown.Size;
			result.PixelsBgra=arrayPixels;
			return result.ToBitmap();
		}

		///<summary>The milliseconds WPF's software rasterizer takes to draw the whole content once at the scene's pixel size, as GetBitmapForTests draws
		///it (RenderTargetBitmap always draws in software): with Is3DLayerCached off, what one full frame of the view costs in a process that draws in
		///software, as the program does (O-34), when the 3D layer is drawn again. For the render times of gallery G1; the layout is brought up to date
		///first and is not counted.</summary>
		public double MeasureSoftwareFrameForTests() {
			if(_sceneShown==null) {
				return double.NaN;
			}
			_gridRoot.UpdateLayout();
			RenderTargetBitmap renderTargetBitmap=new RenderTargetBitmap(_sceneShown.Size.Width,_sceneShown.Size.Height,96,96,PixelFormats.Pbgra32);
			Stopwatch stopwatch=Stopwatch.StartNew();
			RenderContent(renderTargetBitmap);
			return stopwatch.Elapsed.TotalMilliseconds;
		}

		///<summary>Draws the content into renderTargetBitmap in its order: the background, the 3D layer (over its own copy of the background), the 2D
		///layer and the pen stroke.</summary>
		private void RenderContent(RenderTargetBitmap renderTargetBitmap) {
			DrawingVisual drawingVisualBackground=new DrawingVisual();
			using(DrawingContext drawingContext=drawingVisualBackground.RenderOpen()) {
				drawingContext.DrawRectangle(_gridRoot.Background,null,new Rect(0,0,renderTargetBitmap.PixelWidth,renderTargetBitmap.PixelHeight));
			}
			renderTargetBitmap.Render(drawingVisualBackground);
			renderTargetBitmap.Render(_border3D);
			renderTargetBitmap.Render(_liveLayer2D);
			renderTargetBitmap.Render(_livePenLayer);
		}
		#endregion Showing

		#region Mouse
		//The shared mouse class, at the position in the chart's pixels: the content is laid out in pixels (under the 1/DPI transform), so GetPosition on
		//it gives them. DirectX's handlers get each point from its own window message; here a press and a release are read before the capture changes,
		//because WPF moves its mouse to wherever the cursor is when the capture changes (MouseDevice.Synchronize) and raises a MouseMove for it.

		///<summary>Takes the capture (button down to button up, as a WinForms control has it) after reading the press's point, then the shared class's
		///MouseDown.</summary>
		private void GridRoot_MouseDown(object sender,MouseButtonEventArgs e) {
			System.Drawing.Point location=GetLocation(e);
			ChangeCapture(true);
			_toothChartMouse.MouseDown(TcData,location,Size);
		}

		///<summary>The shared class's MouseMove, for a move of the mouse: a MouseMove that WPF raises while this view takes or releases the capture is
		///WPF's own (no WM_MOUSEMOVE made it, DirectX never saw it) and is not handed on, so a pen stroke gets a point exactly when DirectX's gets one,
		///on a move of the mouse. A held-still mouse makes no WM_MOUSEMOVE, and WPF raises no MouseMove for a position it already has, so a held-still
		///pen adds no point.</summary>
		private void GridRoot_MouseMove(object sender,System.Windows.Input.MouseEventArgs e) {
			if(_isChangingCapture) {
				return;
			}
			_toothChartMouse.MouseMove(TcData,GetLocation(e),Size);
		}

		///<summary>Reads the release's point, releases the capture, then calls the shared class's MouseUp, so its SegmentDrawn and ToothSelectionsChanged
		///handlers (ControlChart saves the drawing and refills the chart) run without WPF's capture. The MouseMove WPF raises on the release is not
		///handed on, so it cannot add a point to the stroke that is still in progress until MouseUp.</summary>
		private void GridRoot_MouseUp(object sender,MouseButtonEventArgs e) {
			System.Drawing.Point location=GetLocation(e);
			ChangeCapture(false);
			_toothChartMouse.MouseUp(TcData,location,Size);
		}

		///<summary>Takes or releases WPF's mouse capture for the content, with _isChangingCapture set while WPF raises its own MouseMove inside the call.</summary>
		private void ChangeCapture(bool isCapture) {
			_isChangingCapture=true;
			try {
				if(isCapture) {
					_gridRoot.CaptureMouse();
				}
				else if(_gridRoot.IsMouseCaptured) {
					_gridRoot.ReleaseMouseCapture();
				}
			}
			finally {
				_isChangingCapture=false;
			}
		}

		private System.Drawing.Point GetLocation(System.Windows.Input.MouseEventArgs e) {
			Point point=e.GetPosition(_gridRoot);
			return new System.Drawing.Point((int)Math.Round(point.X),(int)Math.Round(point.Y));
		}

		///<summary>Mouse down at a point in the chart's pixels, as the content's MouseDown gives it, for tests (synthesised input).</summary>
		public void MouseDownForTests(System.Drawing.Point location) {
			_toothChartMouse.MouseDown(TcData,location,Size);
		}

		public void MouseMoveForTests(System.Drawing.Point location) {
			_toothChartMouse.MouseMove(TcData,location,Size);
		}

		public void MouseUpForTests(System.Drawing.Point location) {
			_toothChartMouse.MouseUp(TcData,location,Size);
		}
		#endregion Mouse

		///<summary>The content's root, which holds the 3D layer, the 2D layer and the pen stroke: a Grid that sets the 1/DPI layout transform again when
		///WPF tells it the DPI changed (a move of the window to a monitor of another scale under the program's PerMonitorV2, which brings no new
		///scene).</summary>
		private sealed class LiveRoot:Grid {
			private readonly ToothChartWpfView _toothChartWpfView;

			public LiveRoot(ToothChartWpfView toothChartWpfView) {
				_toothChartWpfView=toothChartWpfView;
			}

			protected override void OnDpiChanged(DpiScale oldDpi,DpiScale newDpi) {
				base.OnDpiChanged(oldDpi,newDpi);
				_toothChartWpfView.ApplyDpiScale(newDpi.DpiScaleX);
			}
		}

		///<summary>The 2D layer of the live view: the scene's 2D items in their order, drawn by the renderer's own code with grayscale text (section 7).</summary>
		private sealed class LiveLayer2D:FrameworkElement {
			private readonly ToothChartWpfView _toothChartWpfView;

			public LiveLayer2D(ToothChartWpfView toothChartWpfView) {
				_toothChartWpfView=toothChartWpfView;
				TextOptions.SetTextRenderingMode(this,TextRenderingMode.Grayscale);
				TextOptions.SetTextHintingMode(this,TextHintingMode.Fixed);
				TextOptions.SetTextFormattingMode(this,TextFormattingMode.Display);
				Focusable=false;
			}

			protected override void OnRender(DrawingContext drawingContext) {
				_toothChartWpfView._countLayer2DRenders++;
				ToothScene scene=_toothChartWpfView._sceneShown;
				if(scene==null) {
					return;
				}
				ChartLayer2D.Draw(drawingContext,scene);
			}
		}

		///<summary>The pen stroke in progress (TcData.PointList, in the chart's pixels, 0.71 mm wide in the pen colour) until mouse up, over the 2D layer,
		///as the image host draws it with GDI+ over its image. An element of its own, so a pen move redraws only the stroke: WPF's frame then covers the
		///stroke's bounds, and the 2D layer and the cached 3D layer under it are composited, not drawn again.</summary>
		private sealed class LivePenLayer:FrameworkElement {
			private readonly ToothChartWpfView _toothChartWpfView;

			public LivePenLayer(ToothChartWpfView toothChartWpfView) {
				_toothChartWpfView=toothChartWpfView;
				Focusable=false;
			}

			protected override void OnRender(DrawingContext drawingContext) {
				_toothChartWpfView._countPenLayerRenders++;
				ToothChartData tcData=_toothChartWpfView.TcData;
				if(tcData==null || tcData.PointList==null || tcData.PointList.Count<2) {
					return;
				}
				SolidColorBrush brush=new SolidColorBrush(Color.FromArgb(255,tcData.ColorDrawing.R,tcData.ColorDrawing.G,tcData.ColorDrawing.B));
				brush.Freeze();
				Pen pen=new Pen(brush,Math.Max(1.0,0.71*tcData.ScaleMmToPix));
				pen.StartLineCap=PenLineCap.Round;
				pen.EndLineCap=PenLineCap.Round;
				pen.LineJoin=PenLineJoin.Round;
				pen.Freeze();
				StreamGeometry streamGeometry=new StreamGeometry();
				using(StreamGeometryContext streamGeometryContext=streamGeometry.Open()) {
					streamGeometryContext.BeginFigure(new Point(tcData.PointList[0].X,tcData.PointList[0].Y),false,false);
					for(int i=1;i<tcData.PointList.Count;i++) {
						streamGeometryContext.LineTo(new Point(tcData.PointList[i].X,tcData.PointList[i].Y),true,true);
					}
				}
				streamGeometry.Freeze();
				drawingContext.DrawGeometry(null,pen,streamGeometry);
			}
		}
	}
}
