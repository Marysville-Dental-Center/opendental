using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Data;
using System.Text;
using System.Windows.Forms;
using CodeBase;
using OpenPmsDentalBusiness;
using SparksToothChart.Wpf3D;

namespace SparksToothChart {
	///<summary>This is the old tooth chart control.  It "wraps" three different old tooth charts: OpenGL, DirectX9, and 2D, and the WPF 3D chart
	///(Engine, docs/specs/wpf-tooth-chart-plan.md), which no caller chooses yet.</summary>
	public partial class ToothChartWrapper:UserControl {
		private string[] selectedTeeth;
		///<summary>True for hardware graphics, false for software graphics.</summary>
		private bool hardwareMode=false;
		private ToothChartOpenGL toothChartOpenGL;
		private ToothChartDirectX toothChartDirectX;
		private int preferredPixelFormatNum;
		private ToothChartDirectX.DirectXDeviceFormat deviceFormat=null;
		private DrawingMode drawMode;
		private ToothChartData tcData;
		///<summary>The engine that draws: the three DrawingMode engines, or Wpf3D. drawMode is its stored view (Wpf3D reads as DirectX).</summary>
		private ToothChartEngine _engine;
		///<summary>Wpf3D: the engine control that shows the renderer's image: the on-screen chart until the live view is made, when the wrapper is drawn
		///in software, and when the live view could not start.</summary>
		private ToothChartImageHost toothChartImageHost;
		///<summary>Wpf3D: the live view on screen (O-19 (b), TC-3), made when the wrapper is shown on an STA thread and not drawn in software; null
		///until then, and on every hidden, MTA or software wrapper.</summary>
		private ToothChartWpfView toothChartWpfView;
		///<summary>Wpf3D: the per-computer "Draw in software" switch (WpfDrawInSoftware).</summary>
		private bool _isWpfDrawnInSoftware;
		///<summary>Wpf3D: set when the live view failed to start on this wrapper, which then shows the software render; cleared when the engine is
		///released.</summary>
		private bool _isWpfLiveViewFailed;
		///<summary>Wpf3D: this wrapper's last 3D layer image, released on Dispose and when the engine changes.</summary>
		private ToothChartLayerCache _layerCacheWpf;
		///<summary>Wpf3D: set by every Invalidate (OnInvalidated), cleared when a scene is built (section 5.5), GetBitmap's included.</summary>
		private bool _isWpfSceneDirty;
		///<summary>Wpf3D: set by every mark, like _isWpfSceneDirty, but cleared only by the on-screen rebuild: a GetBitmap builds a scene without
		///updating the screen, so a wrapper refilled while hidden and then asked for its bitmap still rebuilds its screen image when it is shown.</summary>
		private bool _isWpfScreenStale;
		///<summary>Wpf3D: set when the first mark of a burst posts the on-screen rebuild; cleared by that rebuild, and when the handle goes (which can
		///take the posted rebuild with it).</summary>
		private bool _isWpfRebuildPosted;
		private static List<string> _listFallbackMessages=new List<string>();
		///<summary>Every fallback message of this process, counted on past the 1000 that _listFallbackMessages keeps.</summary>
		private static int _countFallbackMessages;
		///<summary>The fallback messages of this process handed to the ToothChart log.</summary>
		private static int _countFallbackLinesWritten;
		private static readonly object _lockFallback=new object();

		///<summary></summary>
		[Category("Action"),Description("Occurs when the mouse goes up ending a drawing segment.")]
		public event ToothChartDrawEventHandler SegmentDrawn=null;
		///<summary></summary>
		[Category("Action"),Description("Occurs when the mouse goes up committing tooth selection.")]
		public event ToothChartSelectionEventHandler ToothSelectionsChanged=null;
		///<summary>A free-text label (ToothInitialType.Text) was dragged with CursorTool.MoveText to a new point in the chart's 410x307 space. No engine
		///raises it yet: the WPF chart's free text does from TC-5 (OnTextMoved). The relay passes it on, and the Chart module saves the move.</summary>
		[Category("Action"),Description("Occurs when the mouse goes up after moving a text label.")]
		public event ToothChartTextMovedEventHandler TextMoved=null;

		public ToothChartWrapper() {
			drawMode=DrawingMode.Simple2D;
			_engine=ToothChartEngine.Simple2D;
			tcData=new ToothChartData();
			InitializeComponent();
			ResetControls();
			Disposed+=ToothChartWrapper_Disposed;
		}

		#region Properties

		///<summary>The stored kind of the engine (computerpref.GraphicsSimple). Setting it starts that engine, falling back as GetFallbackDrawMode
		///says. While Engine is Wpf3D this reads DirectX: the stored value that the WPF chart replaces (TC-8 maps 0 and 2 to it), so a caller that
		///saves DrawMode never turns a computer to the 2D chart.</summary>
		public DrawingMode DrawMode{
			get{
				return drawMode;
			}
			set{
				if(Environment.OSVersion.Platform==PlatformID.Unix) {
					return;//disallow changing from simpleMode if platform is Unix
				}
				//do not break out if not changing mode.  ContrChart.InitializeOnStartup assumes this code will always run.
				if(_engine==ToothChartEngine.DirectX && value!=DrawingMode.DirectX){
					//If switching from from DirectX to another drawing mode,
					//then we need to cleanup DirectX resources in case the
					//chart is never switched back to DirectX mode.
					DisposeDirectX();//Calls CleanupDirectX() and device.Dispose().
				}
				if(_engine==ToothChartEngine.Wpf3D) {
					DisposeWpf3D();
				}
				//A mode that cannot start falls back to the next one (DirectX, OpenGL, Simple 2D) without an error dialog, logged once per process.
				//Read DrawMode afterwards to learn which mode started.
				drawMode=StartFirstWorkingMode(value,IsDirectXRuntimeAvailable(),TryStartDrawMode,LogFallbackOnce);
				_engine=(ToothChartEngine)drawMode;
			}
		}

		///<summary>The engine that draws the chart. Setting DirectX, Simple2D or OpenGL is the same as setting DrawMode. Setting Wpf3D starts the WPF
		///chart (docs/specs/wpf-tooth-chart-plan.md, section 5): its render thread and a check image the first time in the process, then its engine
		///control, the image host; the live view follows when the wrapper is shown on an STA thread (WpfDrawInSoftware, TC-3). When it cannot start,
		///the chart falls back to Simple2D with one log line (LogFallbackOnce). Read Engine afterwards to learn which engine started. In memory only:
		///no caller chooses Wpf3D yet (TC-8 maps the stored values to it).</summary>
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public ToothChartEngine Engine {
			get {
				return _engine;
			}
			set {
				if(Environment.OSVersion.Platform==PlatformID.Unix) {
					return;//as DrawMode: Unix keeps the 2D chart
				}
				if(value!=ToothChartEngine.Wpf3D) {
					DrawMode=(DrawingMode)value;
					return;
				}
				if(_engine==ToothChartEngine.DirectX) {
					DisposeDirectX();
				}
				if(_engine==ToothChartEngine.Wpf3D) {
					DisposeWpf3D();
				}
				try {
					ToothChartRenderer.Instance.EnsureStarted();
					_engine=ToothChartEngine.Wpf3D;
					drawMode=DrawingMode.DirectX;
					ResetControls();
				}
				catch(Exception ex) {
					DisposeWpf3D();
					LogFallbackOnce(ToothChartFallbackKind.Wpf3DStart,"The "+ToothChartEngine.Wpf3D+" tooth chart could not start ("+ex.GetType().Name+": "
						+ex.Message+"). Using "+ToothChartEngine.Simple2D+".");
					_engine=ToothChartEngine.Simple2D;
					drawMode=DrawingMode.Simple2D;
					ResetControls();
				}
			}
		}

		///<summary>This data object holds nearly all information about what to draw.  It is normally acted on by public methods of the wrapper instead of being accessed directly.</summary>
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public ToothChartData TcData {
			get {
				return tcData;
			}
			set {
				if(_engine==ToothChartEngine.Simple2D) {
					toothChart2D.TcData=value;
				}
				else if(_engine==ToothChartEngine.DirectX) {
					toothChartDirectX.SetTcData(value);//Frees the old models' buffers in video memory and makes the new ones'.
				}
				else if(_engine==ToothChartEngine.OpenGL) {
					toothChartOpenGL.TcData=value;
				}
				else if(_engine==ToothChartEngine.Wpf3D) {
					toothChartImageHost.TcData=value;
					if(toothChartWpfView!=null) {
						toothChartWpfView.TcData=value;
					}
				}
				tcData=value;//Must set last so old tcdata can be cleaned up first.
				if(_engine==ToothChartEngine.Wpf3D) {
					MarkWpfSceneDirty();
				}
			}
		}

		///<summary>Valid values are 1-32 and A-Z.</summary>
		public List<string> SelectedTeeth {
			get {
				return tcData.SelectedTeeth;
			}
		}

		///<summary></summary>
		[Browsable(false)]
		public Color ColorBackground {
			get {
				return tcData.ColorBackground;
			}
			set {
				tcData.ColorBackground=value;
				Invalidate();
			}
		}

		///<summary></summary>
		[Browsable(false)]
		public Color ColorText{
			set {
				tcData.ColorText=value;
				Invalidate();
			}
		}

		///<summary></summary>
		[Browsable(false)]
		public Color ColorTextHighlight {
			set {
				tcData.ColorTextHighlight=value;
				Invalidate();
			}
		}

		///<summary></summary>
		[Browsable(false)]
		public Color ColorBackHighlight {
			set {
				tcData.ColorBackHighlight=value;
				Invalidate();
			}
		}

		///<summary>Set to true when using hardware rendering in OpenGL, and false otherwise. This will have no effect when in simple 2D graphics mode.</summary>
		[Browsable(false)]
		public bool UseHardware{
			get{
				return hardwareMode;
			}
			set{
				hardwareMode=value;
			}
		}

		[Browsable(false)]
		public bool AutoFinish{
			get{
				if(drawMode==DrawingMode.OpenGL) {
					return toothChartOpenGL.autoFinish;
				}
				else {
					return false;
				}
			}
			set{
				if(drawMode==DrawingMode.OpenGL) {
					toothChartOpenGL.autoFinish=value;
				}
			}
		}

		[Browsable(false)]
		public int PreferredPixelFormatNumber{
			get{
				return preferredPixelFormatNum;
			}
			set{
				preferredPixelFormatNum=value;
			}
		}

		[Browsable(false)]
		public ToothChartDirectX.DirectXDeviceFormat DeviceFormat {
			get {
				return deviceFormat;
			}
			set {
				deviceFormat=value;
			}
		}

		[Browsable(false)]
		public CursorTool CursorTool{
			get{
				return tcData.CursorTool;
			}
			set{
				tcData.CursorTool=value;
				//The embedded .cur files (Pen.cur, EraseCircle.cur, ColorChanger.cur), the arrow for the pointer; MoveText keeps the cursor it had, as
				//before. Loaded from this assembly whatever the runtime type, so a subclass of the wrapper gets them too.
				if(tcData.CursorTool==CursorTool.Pointer) {
					this.Cursor=Cursors.Default;
				}
				else if(tcData.CursorTool!=CursorTool.MoveText) {
					this.Cursor=ToothChartMouse.GetCursor(tcData.CursorTool);
				}
				if(toothChartWpfView!=null) {
					toothChartWpfView.SetCursorTool(tcData.CursorTool);//the live view's WPF content has a cursor of its own
				}
				//if(drawMode!=DrawingMode.Simple2D) {
				//	toothChartOpenGL.CursorTool=value;
				//}
			}
		}

		///<summary>For the freehand drawing tool.</summary>
		[Browsable(false)]
		public Color ColorDrawing{
			set{
				tcData.ColorDrawing=value;
			}
		}
		
		[Browsable(false)]
		public bool PerioMode {
			get {
				return tcData.PerioMode;
			}
			set {
				if(_engine!=ToothChartEngine.DirectX && value==true) {
					throw new Exception("Only allowed in DirectX");//the WPF chart draws perio from TC-7
				}
				tcData.PerioMode=value;
				Invalidate();
			}
		}

		///<summary></summary>
		[Browsable(false)]
		public Color ColorBleeding {
			set {
				tcData.ColorBleeding=value;
			}
		}

		///<summary></summary>
		[Browsable(false)]
		public Color ColorSuppuration {
			set {
				tcData.ColorSuppuration=value;
			}
		}

		///<summary></summary>
		[Browsable(false)]
		public Color ColorFurcations {
			set {
				tcData.ColorFurcations=value;
			}
		}

		///<summary></summary>
		[Browsable(false)]
		public Color ColorFurcationsRed {
			set {
				tcData.ColorFurcationsRed=value;
			}
		}

		///<summary></summary>
		[Browsable(false)]
		public Color ColorGingivalMargin {
			set {
				tcData.ColorGingivalMargin=value;
			}
		}

		///<summary></summary>
		[Browsable(false)]
		public Color ColorCAL {
			set {
				tcData.ColorCAL=value;
			}
		}

		///<summary></summary>
		[Browsable(false)]
		public Color ColorMGJ {
			set {
				tcData.ColorMGJ=value;
			}
		}

		///<summary></summary>
		[Browsable(false)]
		public Color ColorProbing {
			set {
				tcData.ColorProbing=value;
			}
		}

		///<summary></summary>
		[Browsable(false)]
		public Color ColorProbingRed {
			set {
				tcData.ColorProbingRed=value;
			}
		}

		///<summary></summary>
		[Browsable(false)]
		public int RedLimitProbing {
			set {
				tcData.RedLimitProbing=value;
			}
		}

		///<summary></summary>
		[Browsable(false)]
		public int RedLimitFurcations {
			set {
				tcData.RedLimitFurcations=value;
			}
		}

		#endregion Properties

		protected override void OnInvalidated(InvalidateEventArgs e) {
			base.OnInvalidated(e);
			if(_engine==ToothChartEngine.Simple2D) {
				toothChart2D.Invalidate();
			}
			else if(_engine==ToothChartEngine.DirectX) {
				toothChartDirectX.Invalidate();
			}
			else if(_engine==ToothChartEngine.OpenGL) {
				toothChartOpenGL.Invalidate();
				//toothChartOpenGL.TaoDraw();
			}
			else if(_engine==ToothChartEngine.Wpf3D) {
				MarkWpfSceneDirty();
			}
		}

		private void ResetControls(){
			selectedTeeth=new string[0];
			this.Controls.Clear();
			if(_engine==ToothChartEngine.Wpf3D) {
				//The image host is a plain WinForms control, and no WPF object is made here: a wrapper made on any thread (the dashboard's MTA worker
				//included) gets the WPF chart. The renderer makes the images on its own thread. The live view comes when the wrapper is shown on an STA
				//thread (StartWpfLiveViewIfShown).
				//The font DirectX's PrepareForDirectX sets, which callers may read to measure text as the DirectX chart did.
				tcData.Font=new System.Drawing.Font("Arial",9f);
				_layerCacheWpf=new ToothChartLayerCache();
				toothChartImageHost=new ToothChartImageHost();
				toothChartImageHost.Dock=System.Windows.Forms.DockStyle.Fill;
				toothChartImageHost.Location=new System.Drawing.Point(0,0);
				toothChartImageHost.Name="toothChartImageHost";
				toothChartImageHost.SegmentDrawn+=new ToothChartDrawEventHandler(toothChart_SegmentDrawn);
				toothChartImageHost.ToothSelectionsChanged+=new ToothChartSelectionEventHandler(toothChart_ToothSelectionsChanged);
				toothChartImageHost.TcData=tcData;
				toothChartImageHost.LayerCache=_layerCacheWpf;
				toothChartImageHost.ActionChartChanged=Invalidate;
				toothChartImageHost.FuncGetFallbackBitmap=GetSimple2DBitmap;
				toothChartImageHost.SuspendLayout();
				this.Controls.Add(toothChartImageHost);
				ResetTeeth();
				toothChartImageHost.ResumeLayout();
				_isWpfSceneDirty=true;
				_isWpfScreenStale=true;
				StartWpfLiveViewIfShown();//a wrapper that is already shown when it starts Wpf3D
				return;
			}
			if(drawMode==DrawingMode.Simple2D){
				//this.Invalidate();
				toothChart2D=new ToothChart2D();
				toothChart2D.Dock = System.Windows.Forms.DockStyle.Fill;
				toothChart2D.Location = new System.Drawing.Point(0,0);
				toothChart2D.Name = "toothChart2D";
				//toothChart2D.Size = new System.Drawing.Size(719,564);//unnecessary?
				toothChart2D.SegmentDrawn+=new ToothChartDrawEventHandler(toothChart_SegmentDrawn);
				toothChart2D.ToothSelectionsChanged+=new ToothChartSelectionEventHandler(toothChart_ToothSelectionsChanged);
				toothChart2D.TcData=tcData;
				toothChart2D.SuspendLayout();
				this.Controls.Add(toothChart2D);
				ResetTeeth();
				toothChart2D.InitializeGraphics();
				toothChart2D.ResumeLayout();
			}
			else if(drawMode==DrawingMode.DirectX){
				//I noticed that this code executes when the program starts, then also when the Chart module is selected for the first time.
				//Thus the Chart graphic was loading twice before the user could see it.
				bool isInitialized=true;
				if(toothChartDirectX==null) {
					isInitialized=false;
				}
				if(isInitialized) {
					//Since the control is already initialized, reuse it.  This helps the load time of the Chart module and helps to prevent an issue.
					//This flag helps prevent a red X on the tooth chart when the Chart module is left open and the Windows user is switched then switched back.
				}
				else {
					toothChartDirectX=new ToothChartDirectX();//(hardwareMode,preferredPixelFormatNum);
				}
				//preferredPixelFormatNum=toothChart.SelectedPixelFormatNumber;
				//toothChartDirectX.ColorText=colorText;
				toothChartDirectX.Dock = System.Windows.Forms.DockStyle.Fill;
				toothChartDirectX.Location = new System.Drawing.Point(0,0);
				toothChartDirectX.Name = "toothChartDirectX";
				//toothChartDirectX.Size = new System.Drawing.Size(719,564);//unnecessary?
				toothChartDirectX.SegmentDrawn+=new ToothChartDrawEventHandler(toothChart_SegmentDrawn);
				toothChartDirectX.ToothSelectionsChanged+=new ToothChartSelectionEventHandler(toothChart_ToothSelectionsChanged);
				toothChartDirectX.TcData=tcData;
				toothChartDirectX.SuspendLayout();//Might help with the MDA debug error we used to get (if the option wasn't disabled in our compilers).
				this.Controls.Add(toothChartDirectX);
				ResetTeeth();
				toothChartDirectX.deviceFormat=deviceFormat;
				if(!isInitialized) {
					toothChartDirectX.InitializeGraphics();
				}
				toothChartDirectX.ResumeLayout();//Might help with the MDA debug error we used to get (if the option wasn't disabled in our compilers).
			}
			else if(drawMode==DrawingMode.OpenGL){
				toothChartOpenGL=new ToothChartOpenGL(hardwareMode,preferredPixelFormatNum);
				preferredPixelFormatNum=toothChartOpenGL.SelectedPixelFormatNumber;
				//toothChartOpenGL.ColorText=colorText;
				toothChartOpenGL.Dock = System.Windows.Forms.DockStyle.Fill;
				toothChartOpenGL.Location = new System.Drawing.Point(0,0);
				toothChartOpenGL.Name = "toothChartOpenGL";
				//toothChartOpenGL.Size = new System.Drawing.Size(719,564);//unnecessary?
				toothChartOpenGL.TcData=tcData;
				toothChartOpenGL.SegmentDrawn+=new ToothChartDrawEventHandler(toothChart_SegmentDrawn);
				toothChartOpenGL.ToothSelectionsChanged+=new ToothChartSelectionEventHandler(toothChart_ToothSelectionsChanged);
				toothChartOpenGL.SuspendLayout();
				this.Controls.Add(toothChartOpenGL);
				ResetTeeth();
				toothChartOpenGL.InitializeGraphics();//MakeDisplayLists();
				toothChartOpenGL.ResumeLayout();
			}
		}

		#region Draw mode fallback
		///<summary>The D3DX 9 helper library from Microsoft's legacy DirectX End-User Runtime. The DirectX chart needs it for its fonts and mesh
		///optimisation (SharpDX.Direct3D9.Font, D3DX.OptimizeFaces). Windows itself ships d3d9.dll but not this file.</summary>
		public const string D3DX_DLL="d3dx9_43.dll";
		private static bool? _isDirectXRuntimeAvailable;
		///<summary>The fallback kinds whose first message has been written to the log in this process.</summary>
		private static readonly HashSet<ToothChartFallbackKind> _hashSetKindsLogged=new HashSet<ToothChartFallbackKind>();

		///<summary>The mode the chart tries next when drawingMode cannot start: DirectX falls back to OpenGL, OpenGL to Simple 2D. Simple 2D has no
		///fallback and returns itself.</summary>
		public static DrawingMode GetFallbackDrawMode(DrawingMode drawingMode) {
			if(drawingMode==DrawingMode.DirectX) {
				return DrawingMode.OpenGL;
			}
			return DrawingMode.Simple2D;
		}

		///<summary>True when D3DX_DLL can be loaded in this process, so the DirectX chart can start. A clean Windows install lacks it. Checked once
		///per process.</summary>
		public static bool IsDirectXRuntimeAvailable() {
			if(_isDirectXRuntimeAvailable==null) {
				_isDirectXRuntimeAvailable=CanLoadLibrary(D3DX_DLL);
			}
			return _isDirectXRuntimeAvailable.Value;
		}

		///<summary>Starts the first mode that works, beginning with drawingModeRequested and following GetFallbackDrawMode: DirectX, then OpenGL,
		///then Simple 2D. DirectX is not tried when isDirectXRuntimeAvailable is false. funcTryStart starts one mode and returns true, or returns false
		///or throws when that mode cannot start. Simple 2D is the last resort and is not caught, as before. actionLog gets one line for each mode
		///that was skipped or failed. Returns the mode that started.</summary>
		public static DrawingMode StartFirstWorkingMode(DrawingMode drawingModeRequested,bool isDirectXRuntimeAvailable,Func<DrawingMode,bool> funcTryStart,
			Action<string> actionLog)
		{
			DrawingMode drawingMode=drawingModeRequested;
			if(drawingMode==DrawingMode.DirectX && !isDirectXRuntimeAvailable) {
				actionLog?.Invoke("The DirectX tooth chart cannot start because "+D3DX_DLL+" (Microsoft's DirectX End-User Runtime) is not installed. "
					+"Using "+GetFallbackDrawMode(drawingMode)+".");
				drawingMode=GetFallbackDrawMode(drawingMode);
			}
			while(drawingMode!=DrawingMode.Simple2D) {
				string reason="";
				bool isStarted=false;
				try {
					isStarted=funcTryStart(drawingMode);
				}
				catch(Exception ex) {
					reason=" ("+ex.GetType().Name+": "+ex.Message+")";
				}
				if(isStarted) {
					return drawingMode;
				}
				actionLog?.Invoke("The "+drawingMode+" tooth chart could not start"+reason+". Using "+GetFallbackDrawMode(drawingMode)+".");
				drawingMode=GetFallbackDrawMode(drawingMode);
			}
			funcTryStart(DrawingMode.Simple2D);
			return DrawingMode.Simple2D;
		}

		///<summary>Starts drawingMode on this wrapper. Throws when it cannot start, after releasing a half-made DirectX control so the next attempt at
		///DirectX starts clean.</summary>
		private bool TryStartDrawMode(DrawingMode drawingMode) {
			drawMode=drawingMode;
			_engine=(ToothChartEngine)drawingMode;
			try {
				ResetControls();
			}
			catch {
				if(drawingMode==DrawingMode.DirectX) {
					DisposeDirectX();
				}
				throw;
			}
			return true;
		}

		private void DisposeDirectX() {
			if(toothChartDirectX==null) {
				return;
			}
			try {
				Controls.Remove(toothChartDirectX);
				toothChartDirectX.Dispose();
			}
			catch(Exception ex) {
				ex.DoNothing();//A device that never started can fail to clean up; it is dropped either way.
			}
			toothChartDirectX=null;
		}

		///<summary>A fallback of a DrawingMode engine (ToothChartFallbackKind.DrawMode), as StartFirstWorkingMode reports it.</summary>
		internal static void LogFallbackOnce(string message) {
			LogFallbackOnce(ToothChartFallbackKind.DrawMode,message);
		}

		///<summary>Writes the first fallback of each kind in this process to the ToothChart log and ignores the rest of that kind, so a computer
		///without DirectX does not fill the log every time a chart is drawn, and a computer that has written one kind still writes the first of
		///another (a DirectX fallback line does not hide the WPF chart's first failure). Every message, written or not, is kept for
		///GetFallbackMessages. The WPF chart's renderer and image host log through here too.</summary>
		internal static void LogFallbackOnce(ToothChartFallbackKind kind,string message) {
			lock(_lockFallback) {
				if(_listFallbackMessages.Count>=1000) {
					_listFallbackMessages.RemoveAt(0);//a computer without DirectX adds one for every chart; the list stays small
				}
				_listFallbackMessages.Add(message);
				_countFallbackMessages++;
				if(!_hashSetKindsLogged.Add(kind)) {
					return;
				}
				_countFallbackLinesWritten++;
			}
			try {
				CodeBase.Logger.WriteLine(message,"ToothChart");
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
		}

		///<summary>The fallback messages of this process so far, in order, including those LogFallbackOnce did not write because one was written
		///already: the last 1000. For tests that check a chart started or drew without falling back, GetCountFallbackMessages and
		///GetFallbackMessagesSince, because once 1000 are kept the count of this list no longer grows.</summary>
		public static List<string> GetFallbackMessages() {
			lock(_lockFallback) {
				return new List<string>(_listFallbackMessages);
			}
		}

		///<summary>How many fallback messages this process has handed to the ToothChart log (CodeBase.Logger, which writes nothing in a unit test
		///process), for tests.</summary>
		public static int GetCountFallbackLinesWritten() {
			lock(_lockFallback) {
				return _countFallbackLinesWritten;
			}
		}

		///<summary>How many fallback messages this process has had so far, counting those GetFallbackMessages no longer keeps. Taken before a call
		///and passed to GetFallbackMessagesSince afterwards, it tells whether that call fell back: a WPF render that fails leaves Engine at Wpf3D and
		///returns the 2D chart's image, so only a new message tells.</summary>
		public static int GetCountFallbackMessages() {
			lock(_lockFallback) {
				return _countFallbackMessages;
			}
		}

		///<summary>The fallback messages added since GetCountFallbackMessages returned countBefore, in order, or an empty list. When more were added
		///than GetFallbackMessages keeps, the kept ones.</summary>
		public static List<string> GetFallbackMessagesSince(int countBefore) {
			lock(_lockFallback) {
				int countNew=Math.Min(_countFallbackMessages-countBefore,_listFallbackMessages.Count);
				if(countNew<=0) {
					return new List<string>();
				}
				return _listFallbackMessages.GetRange(_listFallbackMessages.Count-countNew,countNew);
			}
		}

		///<summary>True when ex is an error of the chart's graphics device: today SharpDX's exception (or a subclass), thrown by the DirectX chart when
		///its device is lost or a picture cannot be read back. Lets callers catch it without naming SharpDX:
		///catch(Exception ex) when(ToothChartWrapper.IsGraphicsDeviceException(ex)). False for null and for every other exception.</summary>
		public static bool IsGraphicsDeviceException(Exception ex) {
			return ex is SharpDX.SharpDXException;
		}

		///<summary>CAUTION: Runs slowly. May take minutes. Returns the string "invalid" if no suitable Direct X format can be found. Each format is
		///tried on an invisible chart on controlHost (TestDirectXFormat). Moved from FormGraphics (TC-2).</summary>
		public static string GetPreferredDirectXFormat(Control controlHost) {
			if(!ToothChartWrapper.IsDirectXRuntimeAvailable()) {
				return "invalid";//No format can work without d3dx9_43.dll, so don't spend minutes trying each one.
			}
			ToothChartDirectX.DirectXDeviceFormat[] directXDeviceFormatArray=ToothChartDirectX.GetStandardDeviceFormats();
			for(int i=0;i<directXDeviceFormatArray.Length;i++) {
				if(TestDirectXFormat(controlHost,directXDeviceFormatArray[i].ToString())) {
					return directXDeviceFormatArray[i].ToString();
				}
			}
			return "invalid";
		}

		///<summary>Returns true if the given directXFormat works for a DirectX tooth chart on the local computer: an invisible chart on controlHost
		///starts DirectX with it and gives a picture. Moved from FormGraphics (TC-2).</summary>
		public static bool TestDirectXFormat(Control controlHost,string directXFormat) {
			using(ToothChartWrapper toothChartWrapperTest=new ToothChartWrapper()) {//using blocks: this project is C# 7.3
				toothChartWrapperTest.Visible=false;
				//We add the invisible tooth chart to our form so that the device context will initialize properly
				//and our device creation test will then be accurate.
				controlHost.Controls.Add(toothChartWrapperTest);
				toothChartWrapperTest.DeviceFormat=new ToothChartDirectX.DirectXDeviceFormat(directXFormat);
				toothChartWrapperTest.DrawMode=DrawingMode.DirectX;//Creates the device.
				if(toothChartWrapperTest.DrawMode!=DrawingMode.DirectX) {
					//The chart falls back to OpenGL, then Simple 2D, when DirectX cannot start.
					controlHost.Controls.Remove(toothChartWrapperTest);
					return false;
				}
				//Now we check to be sure that one can draw and retrieve a screen shot from a DirectX control
				//using the specified device format.
				try {
					using(Bitmap bitmap=toothChartWrapperTest.GetBitmap()) {
					}
				}
				catch {
					controlHost.Controls.Remove(toothChartWrapperTest);
					return false;
				}
				controlHost.Controls.Remove(toothChartWrapperTest);
				return true;
			}
		}

		private static bool CanLoadLibrary(string fileName) {
			try {
				IntPtr intPtrModule=LoadLibrary(fileName);
				if(intPtrModule==IntPtr.Zero) {
					return false;
				}
				FreeLibrary(intPtrModule);
				return true;
			}
			catch(Exception ex) {
				ex.DoNothing();
				return false;
			}
		}

		[System.Runtime.InteropServices.DllImport("kernel32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode,SetLastError=true)]
		private static extern IntPtr LoadLibrary(string lpFileName);

		[System.Runtime.InteropServices.DllImport("kernel32.dll",SetLastError=true)]
		private static extern bool FreeLibrary(IntPtr hModule);
		#endregion Draw mode fallback

		#region Wpf3D
		///<summary>Marks the WPF chart's scene dirty (every Invalidate comes here, section 5.5). On a wrapper that is shown, the first mark of a burst
		///posts one rebuild to the UI thread's message queue, and later marks post nothing; a refill's hundreds of Set calls give one image. A wrapper
		///that is not shown (the hidden forms of prints, sheets and the dashboard) posts nothing: GetBitmap builds its scene. The rebuild is posted
		///rather than tied to the next paint, because the engine control covers the whole wrapper, which may then get no paint of its own. Every mark
		///also marks the screen stale (_isWpfScreenStale), which only the rebuild clears.</summary>
		private void MarkWpfSceneDirty() {
			_isWpfSceneDirty=true;
			_isWpfScreenStale=true;
			PostWpfRebuildIfShown();
		}

		///<summary>On a wrapper that is shown, posts the on-screen rebuild unless one is posted already.</summary>
		private void PostWpfRebuildIfShown() {
			if(_isWpfRebuildPosted || toothChartImageHost==null || !IsShownForWpf()) {
				return;
			}
			_isWpfRebuildPosted=true;
			try {
				BeginInvoke(new Action(RebuildWpfOnScreen));
			}
			catch(InvalidOperationException ex) {
				ex.DoNothing();//the handle went away; nothing is shown, so nothing needs posting
				_isWpfRebuildPosted=false;
			}
		}

		///<summary>Wpf3D: a wrapper that comes on screen makes its live view (on an STA thread, unless drawn in software), and one with a stale screen
		///image (data set while it was hidden, even when a GetBitmap has built its scene since) posts its rebuild now, so it does not wait for the next
		///Invalidate to show the chart.</summary>
		protected override void OnVisibleChanged(EventArgs e) {
			base.OnVisibleChanged(e);
			if(_engine==ToothChartEngine.Wpf3D) {
				StartWpfLiveViewIfShown();
				if(_isWpfScreenStale) {
					PostWpfRebuildIfShown();
				}
			}
		}

		protected override void OnHandleCreated(EventArgs e) {
			base.OnHandleCreated(e);
			if(_engine==ToothChartEngine.Wpf3D) {
				StartWpfLiveViewIfShown();
				if(_isWpfScreenStale) {
					PostWpfRebuildIfShown();
				}
			}
		}

		#region Wpf3D live view and the software switch
		///<summary>The value of computerpref.DirectXFormat that turns the WPF chart's on-screen view to software on a computer (O-19 (b)): empty, or a
		///DirectX device format, means the live hardware view. TC-8 stores and reads it; an older build finds no ';' in it and keeps its default DirectX
		///format (ToothChartDirectX.DirectXDeviceFormat).</summary>
		public const string DirectXFormatWpfSoftware="WpfSoftware";

		///<summary>True when a stored computerpref.DirectXFormat value is DirectXFormatWpfSoftware: the computer draws the WPF chart in software.</summary>
		public static bool IsWpfSoftwareFormat(string directXFormat) {
			return directXFormat!=null && directXFormat.Trim()==DirectXFormatWpfSoftware;
		}

		///<summary>The per-computer "Draw in software" switch of the WPF chart (O-19 (b); TC-8 sets it from computerpref.DirectXFormat and shows it in
		///Setup > Graphics). Off (the default): a shown wrapper on an STA thread shows the live view, drawn by the graphics card. On: it shows the
		///software render of the scene, the same image as GetBitmap's, which no graphics driver can change (for a PC whose driver draws no WPF 3D,
		///dotnet/wpf#7076). Images (GetBitmap, prints, sheets, the dashboard) are the software render either way. Changing it on a shown wrapper
		///switches the view at once.</summary>
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public bool WpfDrawInSoftware {
			get {
				return _isWpfDrawnInSoftware;
			}
			set {
				if(_isWpfDrawnInSoftware==value) {
					return;
				}
				_isWpfDrawnInSoftware=value;
				if(_engine!=ToothChartEngine.Wpf3D) {
					return;
				}
				if(value) {
					DisposeWpfLiveView();
					if(toothChartImageHost!=null) {
						toothChartImageHost.Visible=true;
					}
				}
				else {
					StartWpfLiveViewIfShown();
				}
				_isWpfScreenStale=true;
				PostWpfRebuildIfShown();
			}
		}

		///<summary>The WPF chart's live view, or null (not shown yet, hidden, on an MTA thread, drawn in software, or in another engine), for tests.</summary>
		[Browsable(false)]
		public ToothChartWpfView LiveViewWpf {
			get {
				return toothChartWpfView;
			}
		}

		///<summary>Makes the live view when the wrapper is shown, on an STA thread, in Wpf3D and not drawn in software, and has none yet. It takes the
		///image host's place on screen (the image host stays, hidden, for the software switch), and the screen is rebuilt. When it cannot start, the
		///wrapper keeps the image host and logs once (Wpf3DLiveView); it does not try again until the engine is started again.</summary>
		private void StartWpfLiveViewIfShown() {
			if(_engine!=ToothChartEngine.Wpf3D || _isWpfDrawnInSoftware || _isWpfLiveViewFailed || toothChartWpfView!=null || toothChartImageHost==null
				|| !IsShownForWpf() || System.Threading.Thread.CurrentThread.GetApartmentState()!=System.Threading.ApartmentState.STA)
			{
				return;
			}
			ToothChartWpfView toothChartWpfViewNew=null;
			try {
				toothChartWpfViewNew=new ToothChartWpfView();
				toothChartWpfViewNew.Dock=System.Windows.Forms.DockStyle.Fill;
				toothChartWpfViewNew.Location=new System.Drawing.Point(0,0);
				toothChartWpfViewNew.Name="toothChartWpfView";
				toothChartWpfViewNew.TcData=tcData;
				toothChartWpfViewNew.ActionChartChanged=Invalidate;
				toothChartWpfViewNew.SegmentDrawn+=new ToothChartDrawEventHandler(toothChart_SegmentDrawn);
				toothChartWpfViewNew.ToothSelectionsChanged+=new ToothChartSelectionEventHandler(toothChart_ToothSelectionsChanged);
				toothChartWpfViewNew.SetCursorTool(tcData.CursorTool);
				this.Controls.Add(toothChartWpfViewNew);
				toothChartWpfViewNew.BringToFront();
				toothChartWpfView=toothChartWpfViewNew;
				toothChartImageHost.Visible=false;
			}
			catch(Exception ex) {
				_isWpfLiveViewFailed=true;
				if(toothChartWpfViewNew!=null) {
					try {
						Controls.Remove(toothChartWpfViewNew);
						toothChartWpfViewNew.Dispose();
					}
					catch(Exception exDispose) {
						exDispose.DoNothing();
					}
				}
				toothChartWpfView=null;
				toothChartImageHost.Visible=true;
				LogFallbackOnce(ToothChartFallbackKind.Wpf3DLiveView,"The "+ToothChartEngine.Wpf3D+" tooth chart's live view could not start ("+ex.GetType().Name+": "
					+ex.Message+"). Showing its software render on screen.");
			}
			_isWpfScreenStale=true;
			PostWpfRebuildIfShown();
		}

		///<summary>Releases the live view, if there is one.</summary>
		private void DisposeWpfLiveView() {
			if(toothChartWpfView==null) {
				return;
			}
			try {
				Controls.Remove(toothChartWpfView);
				toothChartWpfView.Dispose();
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
			toothChartWpfView=null;
		}
		#endregion Wpf3D live view and the software switch

		///<summary>Wpf3D: DestroyHandle drops what BeginInvoke posted (RecreateHandle keeps it), so a posted rebuild may never run. The posted mark is
		///cleared, and the screen stays stale until a rebuild runs: the next mark, or the new handle, posts again. A kept rebuild that still runs
		///only renders once more.</summary>
		protected override void OnHandleDestroyed(EventArgs e) {
			_isWpfRebuildPosted=false;
			base.OnHandleDestroyed(e);
		}

		///<summary>True when the wrapper is on screen: it has a handle and it and its parents are visible.</summary>
		private bool IsShownForWpf() {
			return !IsDisposed && !Disposing && IsHandleCreated && Visible;
		}

		///<summary>The posted rebuild: always renders the on-screen image from the current data, even when a GetBitmap in between has built the scene
		///and cleared the dirty mark (a refill that ends in the dashboard's GetBitmap still updates the screen). Only this clears the posted mark
		///(besides the loss of the handle) and the stale mark.</summary>
		private void RebuildWpfOnScreen() {
			_isWpfRebuildPosted=false;
			if(_engine!=ToothChartEngine.Wpf3D || toothChartImageHost==null || IsDisposed) {
				return;
			}
			_isWpfScreenStale=false;
			ToothScene toothScene=BuildWpfScene();
			if(toothChartWpfView!=null) {
				toothChartWpfView.ShowScene(toothScene);//the live view, drawn by the graphics card (O-19 (b))
				return;
			}
			toothChartImageHost.ShowScene(toothScene);
		}

		///<summary>The scene of the current data; clears the dirty mark.</summary>
		private ToothScene BuildWpfScene() {
			ToothScene toothScene=ToothSceneBuilder.Build(tcData);
			_isWpfSceneDirty=false;
			return toothScene;
		}

		///<summary>True while the WPF chart's data has changed since its last scene, for tests.</summary>
		[Browsable(false)]
		public bool IsWpfSceneDirty {
			get {
				return _isWpfSceneDirty;
			}
		}

		///<summary>The WPF chart's engine control, or null in the other engines, for tests.</summary>
		[Browsable(false)]
		public ToothChartImageHost ImageHostWpf {
			get {
				return toothChartImageHost;
			}
		}

		///<summary>GetBitmap of the WPF chart: the renderer's image of a scene built now from TcData, as the DirectX chart's GetBitmap draws TcData
		///as it is (so a change that skipped Invalidate is in it too). On any thread: the wrapper builds only the scene. A render that fails twice
		///gives the 2D chart's image of the same data, logged once, so a print never comes out without a chart.</summary>
		private Bitmap GetWpf3DBitmap() {
			ToothScene toothScene=BuildWpfScene();
			try {
				return ToothChartRenderer.Instance.Render(toothScene,_layerCacheWpf).ToBitmap();
			}
			catch(Exception ex) {
				LogFallbackOnce(ToothChartFallbackKind.Wpf3DRender,"The "+ToothChartEngine.Wpf3D+" tooth chart could not draw its image ("+ex.GetType().Name
					+": "+ex.Message+"). Using "+ToothChartEngine.Simple2D+" for this image.");
				return GetSimple2DBitmap();
			}
		}

		///<summary>The 2D chart's image of TcData at the wrapper's size, in the WPF chart's pixel format (Format24bppRgb).</summary>
		private Bitmap GetSimple2DBitmap() {
			using(ToothChart2D toothChart2DFallback=new ToothChart2D()) {
				toothChart2DFallback.Size=Size;
				toothChart2DFallback.TcData=tcData;
				toothChart2DFallback.InitializeGraphics();
				using(Bitmap bitmap2D=toothChart2DFallback.GetBitmap()) {
					Bitmap bitmap=new Bitmap(bitmap2D.Width,bitmap2D.Height,System.Drawing.Imaging.PixelFormat.Format24bppRgb);
					using(Graphics g=Graphics.FromImage(bitmap)) {
						g.DrawImageUnscaled(bitmap2D,0,0);
					}
					return bitmap;
				}
			}
		}

		///<summary>Releases the WPF chart's engine controls (the image host and the live view) and its 3D layer image.</summary>
		private void DisposeWpf3D() {
			if(_layerCacheWpf!=null) {
				_layerCacheWpf.Dispose();
				_layerCacheWpf=null;
			}
			DisposeWpfLiveView();
			_isWpfLiveViewFailed=false;
			if(toothChartImageHost!=null) {
				try {
					Controls.Remove(toothChartImageHost);
					toothChartImageHost.Dispose();
				}
				catch(Exception ex) {
					ex.DoNothing();
				}
				toothChartImageHost=null;
			}
			_isWpfSceneDirty=false;
			_isWpfScreenStale=false;
			_isWpfRebuildPosted=false;
		}

		private void ToothChartWrapper_Disposed(object sender,EventArgs e) {
			DisposeWpf3D();
		}
		#endregion Wpf3D

		//This step usually happens when the DrawMode property is set on the tooth chart wrapper, by way of the ResetControls() function.
		///<summary>Not normally used unless we are just trying to make a copy of an existing directX control.</summary>
		//public void InitializeDirectXGraphics() {
		//  toothChartDirectX.InitializeGraphics();
		//}

		#region Public Methods

		///<summary>If ListToothGraphics is empty, then this fills it, including the complex process of loading all drawing points from local resources.  Or if not empty, then this resets all 32+20 teeth to default postitions, no restorations, etc. Primary teeth set to visible false.  Also clears selected.  Should surround with SuspendLayout / ResumeLayout.</summary>
		public void ResetTeeth() {
			//selectedTeeth=new string[0];
			//this will only happen once when program first loads.  Unfortunately, there is no way to tell what the drawMode is going to be when loading the graphics from the file.  So any other initialization must happen in resetControls.
			if(tcData.ListToothGraphics.Count==0) {
				tcData.ListToothGraphics.Clear();
				ToothGraphic tooth;
				for(int i=1;i<=32;i++) {
					tooth=new ToothGraphic(i.ToString());
					tooth.Visible=true;
					tcData.ListToothGraphics.Add(tooth);
					//primary
					if(Tooth.PermToPri(i.ToString())!="") {
						tooth=new ToothGraphic(Tooth.PermToPri(i.ToString()));
						tooth.Visible=false;
						tcData.ListToothGraphics.Add(tooth);
					}
				}
				tooth=new ToothGraphic("implant");
				tcData.ListToothGraphics.Add(tooth);
			}
			else {//list was already initially filled, but now user needs to reset it.
				for(int i=0;i<tcData.ListToothGraphics.Count;i++) {//loop through all perm and pri teeth.
					tcData.ListToothGraphics[i].Reset();
				}
			}
			tcData.SelectedTeeth.Clear();
			tcData.DrawingSegmentList=new List<ToothInitial>();
			tcData.PointList=new List<PointF>();
			Invalidate();
		}

		///<summary>Moves position of tooth.  Rotations first in order listed, then translations.  Tooth doesn't get moved immediately, just when painting.  All changes are cumulative and are in addition to any previous translations and rotations.</summary>
		public void MoveTooth(string toothID,float rotate,float tipM,float tipB,float shiftM,float shiftO,float shiftB) {
			if(!ToothGraphic.IsValidToothID(toothID)) {
				return;
			}
			tcData.ListToothGraphics[toothID].ShiftM+=shiftM;
			tcData.ListToothGraphics[toothID].ShiftO+=shiftO;
			tcData.ListToothGraphics[toothID].ShiftB+=shiftB;
			tcData.ListToothGraphics[toothID].Rotate+=rotate;
			tcData.ListToothGraphics[toothID].TipM+=tipM;
			tcData.ListToothGraphics[toothID].TipB+=tipB;
			Invalidate();
		}

		///<summary>Sets the specified permanent tooth to primary. Works as follows: Sets ShowPrimaryLetter to true for the perm tooth.  Makes pri tooth visible=true.  Also repositions perm tooth by translating -Y.  Moves primary tooth slightly to M or D sometimes for better alignment.  And if 2nd primary molar, then because of the larger size, it must move all perm molars to distal.</summary>
		public void SetPrimary(string toothID) {
			if(!ToothGraphic.IsValidToothID(toothID)) {
				return;
			}
			if(Tooth.IsPrimary(toothID)) {
				return;
			}
			tcData.ListToothGraphics[toothID].ShiftO-=12;
			if(ToothGraphic.IsValidToothID(Tooth.PermToPri(toothID))) {//if there's a primary tooth at this location
				tcData.ListToothGraphics[Tooth.PermToPri(toothID)].Visible=true;//show the primary.
				tcData.ListToothGraphics[toothID].ShowPrimaryLetter=true;
			}		
			//first pri mand molars, shift slightly to M
			if(toothID=="21") {
				tcData.ListToothGraphics["J"].ShiftM+=0.5f;
			}
			if(toothID=="28") {
				tcData.ListToothGraphics["S"].ShiftM+=0.5f;
			}
			//second pri molars are huge, so shift distally for space
			//and move all the perm molars distally too
			if(toothID=="4") {
				tcData.ListToothGraphics["A"].ShiftM-=0.5f;
				tcData.ListToothGraphics["1"].ShiftM-=1;
				tcData.ListToothGraphics["2"].ShiftM-=1;
				tcData.ListToothGraphics["3"].ShiftM-=1;
			}
			if(toothID=="13") {
				tcData.ListToothGraphics["J"].ShiftM-=0.5f;
				tcData.ListToothGraphics["14"].ShiftM-=1;
				tcData.ListToothGraphics["15"].ShiftM-=1;
				tcData.ListToothGraphics["16"].ShiftM-=1;
			}
			if(toothID=="20") {
				tcData.ListToothGraphics["K"].ShiftM-=1.2f;
				tcData.ListToothGraphics["17"].ShiftM-=2.3f;
				tcData.ListToothGraphics["18"].ShiftM-=2.3f;
				tcData.ListToothGraphics["19"].ShiftM-=2.3f;
			}
			if(toothID=="29") {
				tcData.ListToothGraphics["T"].ShiftM-=1.2f;
				tcData.ListToothGraphics["30"].ShiftM-=2.3f;
				tcData.ListToothGraphics["31"].ShiftM-=2.3f;
				tcData.ListToothGraphics["32"].ShiftM-=2.3f;
			}
			Invalidate();
		}

		///<summary>This is used for crowns and for retainers.  Crowns will be visible on missing teeth with implants.  Crowns are visible on F and O views, unlike ponics which are only visible on F view.  If the tooth is not visible, that should be set before this call, because then, this will set the root invisible. Tooth numbers 1-32 or A-T.  Supernumeraries not supported here yet.</summary>
		public void SetCrown(string toothID,Color color) {
			if(!ToothGraphic.IsValidToothID(toothID)) {
				return;
			}
			tcData.ListToothGraphics[toothID].IsCrown=true;
			if(!tcData.ListToothGraphics[toothID].Visible) {//tooth not visible, so set root invisible.
				tcData.ListToothGraphics[toothID].SetGroupVisibility(ToothGroupType.Cementum,false);
			}
			tcData.ListToothGraphics[toothID].SetSurfaceColors("MODBLFIV",color);
			tcData.ListToothGraphics[toothID].SetGroupColor(ToothGroupType.Enamel,color);
			tcData.ListToothGraphics[toothID].SetGroupColor(ToothGroupType.EnamelF,color);
			this.Invalidate();
		}

		///<summary>A series of color settings will result in the last ones entered overriding earlier entries.</summary>
		public void SetSurfaceColors(string toothID,string surfaces,Color color) {
			if(!ToothGraphic.IsValidToothID(toothID)) {
				return;
			}
			tcData.ListToothGraphics[toothID].SetSurfaceColors(surfaces,color);
			Invalidate();
		}

		///<summary>Used for missing teeth.  This should always be done before setting restorations, because a pontic will cause the tooth to become visible again except for the root.  So if setMissing after a pontic, then the pontic can't show.</summary>
		public void SetMissing(string toothID) {
			if(!ToothGraphic.IsValidToothID(toothID)) {
				return;
			}
			tcData.ListToothGraphics[toothID].Visible=false;
			Invalidate();
		}

		///<summary>This is just the same as SetMissing, except that it also hides the number from showing.  This is used, for example, if premolars are missing, and ortho has completely closed the space.  User will not be able to select this tooth because the number is hidden.</summary>
		public void SetHidden(string toothID) {
			if(!ToothGraphic.IsValidToothID(toothID)) {
				return;
			}
			tcData.ListToothGraphics[toothID].Visible=false;
			tcData.ListToothGraphics[toothID].HideNumber=true;
			Invalidate();
		}

		///<summary>This is used for any pontic, including bridges, full dentures, and partials.  It is usually used on a tooth that has already been set invisible.  This routine cuases the tooth to show again, but the root needs to be invisible.  Then, it sets the entire crown to the specified color.  If the tooth is already visible, then it does not set the root invisible.
		///Tooth numbers 1-32 or A-J or T-K.  Supernumeraries not supported here yet.</summary>
		public void SetPontic(string toothID,Color color) {
			if(!ToothGraphic.IsValidToothID(toothID)) {
				return;
			}
			tcData.ListToothGraphics[toothID].IsPontic=true;
			if(!tcData.ListToothGraphics[toothID].Visible) {
				//tooth not visible, but since IsPontic changes the visibility behavior of the tooth, we need to set the root invisible.
				tcData.ListToothGraphics[toothID].SetGroupVisibility(ToothGroupType.Cementum,false);
			}
			tcData.ListToothGraphics[toothID].SetSurfaceColors("MODBLFIV",color);
			tcData.ListToothGraphics[toothID].SetGroupColor(ToothGroupType.Enamel,color);
			tcData.ListToothGraphics[toothID].SetGroupColor(ToothGroupType.EnamelF,color);
			Invalidate();
		}

		///<summary>Root canals are initially not visible.  This routine sets the canals visible, changes the color to the one specified, and also sets the cementum for the tooth to be semitransparent so that the canals can be seen.  Also sets the IsRCT flag for the tooth to true.</summary>
		public void SetRCT(string toothID,Color color) {
			if(!ToothGraphic.IsValidToothID(toothID)) {
				return;
			}
			tcData.ListToothGraphics[toothID].IsRCT=true;
			tcData.ListToothGraphics[toothID].colorRCT=color;
			Invalidate();
		}

		///<summary>This draws a big red extraction X right on top of the tooth.  It's up to the calling application to figure out when it's appropriate to do this.  Even if the tooth has been marked invisible, there's a good chance that this will still get drawn because a tooth can be set visible again for the drawing the pontic.  So the calling application needs to figure out when it's appropriate to draw the X, and not set this otherwise.</summary>
		public void SetBigX(string toothID,Color color) {
			if(!ToothGraphic.IsValidToothID(toothID)) {
				return;
			}
			tcData.ListToothGraphics[toothID].DrawBigX=true;
			tcData.ListToothGraphics[toothID].colorX=color;
			Invalidate();
		}

		///<summary>Set this tooth to show a BU or post.</summary>
		public void SetBU(string toothID,Color color) {
			if(!ToothGraphic.IsValidToothID(toothID)) {
				return;
			}
			//TcData.ListToothGraphics[toothID].IsBU=true;
			//TcData.ListToothGraphics[toothID].colorBU=color;
			//Buildups are now just another group, so
			tcData.ListToothGraphics[toothID].SetGroupVisibility(ToothGroupType.Buildup,true);
			tcData.ListToothGraphics[toothID].SetGroupColor(ToothGroupType.Buildup,color);
			Invalidate();
		}

		///<summary>Set this tooth to show an implant</summary>
		public void SetImplant(string toothID,Color color) {
			if(!ToothGraphic.IsValidToothID(toothID)) {
				return;
			}
			tcData.ListToothGraphics[toothID].IsImplant=true;
			tcData.ListToothGraphics[toothID].colorImplant=color;
			Invalidate();
		}

		///<summary>Set this tooth to show a sealant</summary>
		public void SetSealant(string toothID,Color color) {
			if(!ToothGraphic.IsValidToothID(toothID)) {
				return;
			}
			tcData.ListToothGraphics[toothID].IsSealant=true;
			tcData.ListToothGraphics[toothID].colorSealant=color;
			Invalidate();
		}

		///<summary>This will mostly only be successful on certain anterior teeth.   For others, it will just show F coloring.</summary>
		public void SetVeneer(string toothID,Color color) {
			if(!ToothGraphic.IsValidToothID(toothID)) {
				return;
			}
			tcData.ListToothGraphics[toothID].SetSurfaceColors("BFV",color);
			tcData.ListToothGraphics[toothID].SetGroupColor(ToothGroupType.EnamelF,color);
			tcData.ListToothGraphics[toothID].SetGroupColor(ToothGroupType.DF,color);
			tcData.ListToothGraphics[toothID].SetGroupColor(ToothGroupType.MF,color);
			tcData.ListToothGraphics[toothID].SetGroupColor(ToothGroupType.IF,color);
			Invalidate();
		}

		///<summary>Set this tooth to show a 'W' to indicate that the tooth is being watched.</summary>
		public void SetWatch(string toothID,Color color) {
			if(!ToothGraphic.IsValidToothID(toothID)) {
				return;
			}
			tcData.ListToothGraphics[toothID].Watch=true;
			tcData.ListToothGraphics[toothID].colorWatch=color;
			Invalidate();
		}

		///<summary></summary>
		public void AddDrawingSegment(ToothInitial drawingSegment) {
			bool alreadyAdded=false;
			for(int i=0;i<tcData.DrawingSegmentList.Count;i++) {
				if(tcData.DrawingSegmentList[i].DrawingSegment==drawingSegment.DrawingSegment) {
					alreadyAdded=true;
					break;
				}
			}
			if(!alreadyAdded){
				tcData.DrawingSegmentList.Add(drawingSegment);
			}
			Invalidate();
			//toothChartOpenGL.AddDrawingSegment(drawingSegment);
		}

		///<summary>Returns a bitmap of what is showing in the control.  Used for printing.</summary>
		public Bitmap GetBitmap() {
			/*
			Bitmap bmp=new Bitmap(this.Width,this.Height);
			Graphics g=Graphics.FromImage(dummy);
			PaintEventArgs e=new PaintEventArgs(g,new Rectangle(0,0,dummy.Width,dummy.Height));
			if(simpleMode) {
				OnPaint(e);
				return dummy;
			}
			toothChartOpenGL.Render(e);
			Bitmap result=toothChartOpenGL.ReadFrontBuffer();
			g.Dispose();
			return result;
			return null;*/
			if(_engine==ToothChartEngine.Wpf3D) {
				return GetWpf3DBitmap();
			}
			if(drawMode==DrawingMode.Simple2D) {
				return toothChart2D.GetBitmap();
			}
			if(drawMode==DrawingMode.OpenGL) {
				return toothChartOpenGL.GetBitmap();
			}
			if(drawMode==DrawingMode.DirectX) {
				return toothChartDirectX.GetBitmap();
			}
			return null;
		}

		public void SetToothNumberingNomenclature(ToothNumberingNomenclature nomenclature) {
			tcData.ToothNumberingNomenclature=nomenclature;
			Invalidate();
		}

		public void SetMobility(string toothID,string mobility,Color color) {
			if(!ToothGraphic.IsValidToothID(toothID)) {
				return;
			}
			tcData.ListToothGraphics[toothID].Mobility=mobility;
			tcData.ListToothGraphics[toothID].colorMobility=color;
			Invalidate();
		}

		public void AddPerioMeasure(int intTooth,PerioSequenceType sequenceType,int mb,int b,int db,int ml,int l, int dl) {
			PerioMeasure pm=new PerioMeasure();
			pm.MBvalue=mb;
			pm.Bvalue=b;
			pm.DBvalue=db;
			pm.MLvalue=ml;
			pm.Lvalue=l;
			pm.DLvalue=dl;
			pm.IntTooth=intTooth;
			pm.SequenceType=sequenceType;
			tcData.ListPerioMeasure.Add(pm);
		}

		public void AddPerioMeasure(PerioMeasure pm) {
			tcData.ListPerioMeasure.Add(pm);
		}

		#endregion

		protected override void OnResize(EventArgs e) {
			base.OnResize(e);
			tcData.SizeControl=this.Size;
			Invalidate();//Wpf3D: marks the scene dirty, and the next image has the new size
			if(_engine==ToothChartEngine.DirectX){
				//Fire the resize event for the DirectX tooth chart.
				//For some reason the Resize() and Size() events don't fire on the DirectX control
				//if you create them through the designer. Perhaps there is something wrong, but this
				//works for now.
				toothChartDirectX.SetSize(this.Size);
			}
		}

		public void SetSelected(string tooth_id,bool setValue) {
			tcData.SetSelected(tooth_id,setValue);
			Invalidate();
		}

		///<summary></summary>
		protected void OnSegmentDrawn(string drawingSegment){
			ToothChartDrawEventArgs tArgs=new ToothChartDrawEventArgs(drawingSegment);
			if(SegmentDrawn!=null){
				SegmentDrawn(this,tArgs);
			}
		}

		///<summary></summary>
		protected void OnToothSelectionsChanged(){
			if(ToothSelectionsChanged!=null){
				ToothSelectionsChanged(this);
			}
		}

		private void toothChart_SegmentDrawn(object sender,ToothChartDrawEventArgs e) {
			OnSegmentDrawn(e.DrawingSegement);
		}

		private void toothChart_ToothSelectionsChanged(object sender) {
			OnToothSelectionsChanged();
		}

		///<summary>Raises TextMoved: the free-text label toothInitialNum was dragged to pointF, in the chart's 410x307 space. For the engines (the WPF
		///chart's free text, TC-5); none calls it yet.</summary>
		internal void OnTextMoved(long toothInitialNum,PointF pointF) {
			TextMoved?.Invoke(this,new ToothChartTextMovedEventArgs(toothInitialNum,pointF));
		}

		//public void SimulateMouseDown(MouseEventArgs e) {

		//}
		

	}

	public enum CursorTool{
		Pointer,
		Pen,
		Eraser,
		ColorChanger,
		///<summary>Only supported in Sparks3D</summary>
		MoveText
	}

	///<summary></summary>
	public delegate void ToothChartDrawEventHandler(object sender,ToothChartDrawEventArgs e);
	///<summary></summary>
	public delegate void ToothChartSelectionEventHandler(object sender);
	///<summary>TextMoved: a free-text label was moved.</summary>
	public delegate void ToothChartTextMovedEventHandler(object sender,ToothChartTextMovedEventArgs e);

	///<summary>The label a TextMoved moved (toothinitial.ToothInitialNum) and where it was dropped, in the chart's 410x307 space, as the mouse gave
	///it: not clamped or rounded (ToothChartRelay.MoveTextLabel does that before saving).</summary>
	public class ToothChartTextMovedEventArgs {
		public long ToothInitialNum { get; }
		public PointF Location { get; }

		public ToothChartTextMovedEventArgs(long toothInitialNum,PointF location) {
			ToothInitialNum=toothInitialNum;
			Location=location;
		}
	}
	
	///<summary></summary>
	public class ToothChartDrawEventArgs{
		private string drawingSegment;
		//private bool isInsert;

		///<summary></summary>
		public ToothChartDrawEventArgs(string drawingSeg){//,bool isInsert){
			this.drawingSegment=drawingSeg;
			//this.isInsert=isInsert;
		}

		///<summary></summary>
		public string DrawingSegement{
			get{ 
				return drawingSegment;
			}
		}

		/*//<summary></summary>
		public bool IsInsert{
			get{ 
				return isInsert;
			}
		}*/
	}

	
}
