using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using CodeBase;

namespace SparksToothChart.Wpf3D {
	///<summary>The WPF chart's engine control: a plain WinForms control that shows the renderer's image (docs/specs/wpf-tooth-chart-plan.md,
	///section 5.4, O-19 (a)). It makes no WPF object. On-screen renders run asynchronously and the newest scene wins: while one render is in flight,
	///a newer scene replaces any waiting one, so slider ticks never queue up. A result reaches this control only through a non-blocking BeginInvoke,
	///skipped when the control is disposed or has no handle; the race where it goes away during the call is caught and ignored. A render whose
	///result cannot arrive (the handle went) is given up, and the newest scene is rendered again when a handle comes back, so the control never stays
	///"in flight" for good; a render a shutdown of the render thread aborted comes back as a failed result (ToothChartRenderer.RenderAsync).
	///It raises SegmentDrawn and ToothSelectionsChanged like the other engines, from the mouse class it shares with the live view (ToothChartMouse,
	///the DirectX chart's mouse logic), and draws the pen stroke in progress with GDI+ over the image until mouse up, as DirectX draws it over the
	///teeth without redrawing them. Since TC-3 (O-19 (b)) it is the on-screen chart of a wrapper drawn in software (ToothChartWrapper.WpfDrawInSoftware)
	///and of a wrapper whose live view could not start, and the wrapper's engine control until the live view is made.</summary>
	public class ToothChartImageHost:Control {
		#region Fields
		///<summary>The wrapper's data object.</summary>
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public ToothChartData TcData;
		///<summary>Called after the mouse changed the chart's data (selection, drawings), so the wrapper renders the chart again.</summary>
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public Action ActionChartChanged;
		///<summary>The 2D chart's image of the current data, for a render that failed twice.</summary>
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public Func<Bitmap> FuncGetFallbackBitmap;
		private ToothChartLayerCache _layerCache;
		private Bitmap _bitmapShown;
		private ToothScene _sceneShown;
		private ToothScene _sceneWaiting;
		///<summary>The newest scene ShowScene was given: rendered again when a render's result was lost with the handle.</summary>
		private ToothScene _sceneRequested;
		private bool _isRenderInFlight;
		///<summary>Counts the renders started. A result of an older render (one given up when the handle went) is dropped.</summary>
		private int _generationRender;
		private int _countImagesReceived;
		private int _countReceiveCalls;
		private int _countRendersStarted;
		///<summary>The pointer, pen, eraser and colour changer, shared with the live view.</summary>
		private readonly ToothChartMouse _toothChartMouse=new ToothChartMouse();
		#endregion Fields

		public ToothChartImageHost() {
			SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Opaque,true);
			_toothChartMouse.ActionChartChanged=() => ActionChartChanged?.Invoke();
			_toothChartMouse.ActionPenStrokeChanged=Invalidate;//only the stroke is drawn again, over the last image
			_toothChartMouse.SegmentDrawn+=(sender,e) => OnSegmentDrawn(e.DrawingSegement);
			_toothChartMouse.ToothSelectionsChanged+=sender => OnToothSelectionsChanged();
		}

		#region Events
		[Category("OD"),Description("Occurs on mouse up that ends a drawing segment.")]
		public event ToothChartDrawEventHandler SegmentDrawn=null;

		[Category("OD"),Description("Occurs on mouse up that commits tooth selection.")]
		public event ToothChartSelectionEventHandler ToothSelectionsChanged=null;

		protected void OnSegmentDrawn(string drawingSegment) {
			SegmentDrawn?.Invoke(this,new ToothChartDrawEventArgs(drawingSegment));
		}

		protected void OnToothSelectionsChanged() {
			ToothSelectionsChanged?.Invoke(this);
		}
		#endregion Events

		#region Properties
		///<summary>The wrapper's 3D layer cache, which this control's renders use and keep.</summary>
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public ToothChartLayerCache LayerCache {
			get {
				return _layerCache;
			}
			set {
				_layerCache=value;
			}
		}

		///<summary>How many rendered images reached this control, for tests.</summary>
		[Browsable(false)]
		public int CountImagesReceived {
			get {
				return _countImagesReceived;
			}
		}

		///<summary>How many times a render's result reached this control's UI thread at all, counted before anything else is checked (a disposed
		///control included), for tests: a test that a disposed control gets no call reads this, not CountImagesReceived.</summary>
		[Browsable(false)]
		public int CountReceiveCalls {
			get {
				return _countReceiveCalls;
			}
		}

		///<summary>How many renders this control started, for tests.</summary>
		[Browsable(false)]
		public int CountRendersStarted {
			get {
				return _countRendersStarted;
			}
		}

		///<summary>The scene of the image shown, or null before the first.</summary>
		[Browsable(false)]
		public ToothScene SceneShown {
			get {
				return _sceneShown;
			}
		}

		///<summary>True while a render of this control is in flight.</summary>
		[Browsable(false)]
		public bool IsRenderInFlight {
			get {
				return _isRenderInFlight;
			}
		}

		///<summary>A copy of the image shown, or null.</summary>
		public Bitmap GetBitmapShown() {
			if(_bitmapShown==null) {
				return null;
			}
			return new Bitmap(_bitmapShown);
		}
		#endregion Properties

		#region Rendering
		///<summary>Renders the scene for the screen, on the UI thread of this control. When a render is in flight, the scene waits, replacing any
		///scene that was waiting.</summary>
		public void ShowScene(ToothScene scene) {
			if(IsDisposed || scene==null) {
				return;
			}
			_sceneRequested=scene;
			_sceneWaiting=scene;
			if(!_isRenderInFlight) {
				StartRender();
			}
		}

		private void StartRender() {
			ToothScene scene=_sceneWaiting;
			_sceneWaiting=null;
			if(scene==null) {
				return;
			}
			if(scene.Size.Width<1 || scene.Size.Height<1) {
				return;
			}
			_isRenderInFlight=true;
			_countRendersStarted++;
			int generation=++_generationRender;
			try {
				ToothChartRenderer.Instance.RenderAsync(scene,_layerCache,result => OnRenderDone(scene,result,generation));
			}
			catch(Exception ex) {
				ToothChartRenderResult resultFailed=new ToothChartRenderResult();
				resultFailed.Size=scene.Size;
				resultFailed.Exception=ex;
				ReceiveImage(scene,resultFailed,generation);
			}
		}

		///<summary>On the render thread (or, for a job a shutdown aborted, the thread that ended it): hands the result to the UI thread without
		///waiting, or drops it when this control is gone or has no handle. A result dropped for want of a handle is not lost for good:
		///OnHandleCreated renders the newest scene again.</summary>
		private void OnRenderDone(ToothScene scene,ToothChartRenderResult result,int generation) {
			if(IsDisposed || Disposing || !IsHandleCreated) {
				return;
			}
			try {
				BeginInvoke(new Action(() => ReceiveImage(scene,result,generation)));
			}
			catch(InvalidOperationException ex) {
				ex.DoNothing();//the handle went away between the check and the call (ObjectDisposedException included); OnHandleCreated renders again
			}
		}

		///<summary>A render whose result can no longer arrive (the handle went, and DestroyHandle drops what BeginInvoke posted) or may not have
		///arrived (it ended while there was no handle) is given up: the control is no longer "in flight", the newest scene waits to be rendered
		///again, and a late result of the given-up render is dropped.</summary>
		private void AbandonRenderInFlight() {
			_generationRender++;
			_isRenderInFlight=false;
			if(_sceneWaiting==null && !IsDisposed && !Disposing) {
				_sceneWaiting=_sceneRequested;
			}
		}

		protected override void OnHandleCreated(EventArgs e) {
			base.OnHandleCreated(e);
			if(_isRenderInFlight) {
				AbandonRenderInFlight();//its result may have found no handle
			}
			if(_sceneWaiting!=null) {
				StartRender();
			}
		}

		protected override void OnHandleDestroyed(EventArgs e) {
			if(_isRenderInFlight) {
				AbandonRenderInFlight();
			}
			base.OnHandleDestroyed(e);
		}

		///<summary>On the UI thread: shows the image (or, after a failed render, the 2D chart's image of the data) and starts the waiting scene. A
		///result of a given-up render (AbandonRenderInFlight) is dropped.</summary>
		private void ReceiveImage(ToothScene scene,ToothChartRenderResult result,int generation) {
			_countReceiveCalls++;
			if(generation!=_generationRender) {
				return;
			}
			_isRenderInFlight=false;
			if(IsDisposed) {
				return;
			}
			Bitmap bitmapNew=null;
			if(result.Exception==null && result.PixelsBgra!=null) {
				bitmapNew=result.ToBitmap();
			}
			else {
				ToothChartWrapper.LogFallbackOnce(ToothChartFallbackKind.Wpf3DRender,"The Wpf3D tooth chart could not draw its image on screen ("
					+result.Exception?.GetType().Name+": "+result.Exception?.Message+"). Using Simple2D for this image.");
				try {
					bitmapNew=FuncGetFallbackBitmap?.Invoke();
				}
				catch(Exception ex) {
					ex.DoNothing();
				}
			}
			if(bitmapNew!=null) {
				Bitmap bitmapOld=_bitmapShown;
				_bitmapShown=bitmapNew;
				_sceneShown=scene;
				bitmapOld?.Dispose();
				_countImagesReceived++;
				Invalidate();
			}
			if(_sceneWaiting!=null) {
				StartRender();
			}
		}

		protected override void OnPaintBackground(PaintEventArgs pevent) {
			//Do nothing: OnPaint covers the whole control, so there is no flicker.
		}

		protected override void OnPaint(PaintEventArgs e) {
			Color colorBackground=TcData==null?Color.Gray:TcData.ColorBackground;
			if(_bitmapShown==null) {
				using(SolidBrush brush=new SolidBrush(colorBackground)) {
					e.Graphics.FillRectangle(brush,ClientRectangle);
				}
			}
			else {
				//Pixel for pixel, with an explicit destination: DrawImageUnscaled draws at the image's physical size, which is not its pixel size when
				//the bitmap's DPI differs from the Graphics' (a drag to a monitor of another scale under PerMonitorV2).
				e.Graphics.InterpolationMode=InterpolationMode.NearestNeighbor;
				Rectangle rectangleImage=new Rectangle(0,0,_bitmapShown.Width,_bitmapShown.Height);
				e.Graphics.DrawImage(_bitmapShown,rectangleImage,rectangleImage,GraphicsUnit.Pixel);
				if(_bitmapShown.Width<Width || _bitmapShown.Height<Height) {
					//The control grew and the new image is not there yet: the background fills the rest.
					using(SolidBrush brush=new SolidBrush(colorBackground)) {
						e.Graphics.FillRectangle(brush,_bitmapShown.Width,0,Width-_bitmapShown.Width,Height);
						e.Graphics.FillRectangle(brush,0,_bitmapShown.Height,_bitmapShown.Width,Height-_bitmapShown.Height);
					}
				}
			}
			DrawPenStroke(e.Graphics);
		}

		///<summary>The stroke in progress (TcData.PointList, in control pixels), 0.71 mm wide in the pen colour, as the DirectX chart draws it.</summary>
		private void DrawPenStroke(Graphics g) {
			if(TcData==null || TcData.PointList==null || TcData.PointList.Count<2) {
				return;
			}
			float widthPixels=Math.Max(1f,0.71f*TcData.ScaleMmToPix);
			using(Pen pen=new Pen(TcData.ColorDrawing,widthPixels)) {
				pen.StartCap=LineCap.Round;
				pen.EndCap=LineCap.Round;
				pen.LineJoin=LineJoin.Round;
				g.SmoothingMode=SmoothingMode.AntiAlias;
				g.DrawLines(pen,TcData.PointList.ToArray());
			}
		}

		protected override void Dispose(bool disposing) {
			if(disposing) {
				_bitmapShown?.Dispose();
				_bitmapShown=null;
				_sceneWaiting=null;
				_sceneRequested=null;
			}
			base.Dispose(disposing);
		}
		#endregion Rendering

		#region Mouse
		//The shared mouse class (ToothChartMouse): the DirectX chart's GetToothAtPoint, drag fill-in through GetAffectedTeeth and SegmentDrawn strings. A
		//change of the data asks the wrapper for a new image instead of drawing at once; the pen's stroke in progress is drawn over the last image.
		protected override void OnMouseDown(MouseEventArgs e) {
			base.OnMouseDown(e);
			_toothChartMouse.MouseDown(TcData,e.Location,Size);
		}

		protected override void OnMouseMove(MouseEventArgs e) {
			base.OnMouseMove(e);
			_toothChartMouse.MouseMove(TcData,e.Location,Size);
		}

		protected override void OnMouseUp(MouseEventArgs e) {
			base.OnMouseUp(e);
			_toothChartMouse.MouseUp(TcData,e.Location,Size);
		}
		#endregion Mouse
	}
}
