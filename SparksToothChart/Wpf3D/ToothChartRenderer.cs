using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using CodeBase;

namespace SparksToothChart.Wpf3D {
	///<summary>The last 3D layer image of one wrapper (docs/specs/wpf-tooth-chart-plan.md, section 5.4): frozen, held by the wrapper and released on
	///its Dispose, not by the render thread, so the wrappers that prints and sheets make for one image and throw away leave nothing behind there. A
	///scene whose 3D layer is the same as the cached one (ToothScene.IsSame3DLayer: only selection, drawings or text changed) reuses the image.</summary>
	public sealed class ToothChartLayerCache:IDisposable {
		internal readonly object Lock=new object();
		internal ToothScene Scene3D;
		internal BitmapSource Image3D;
		private bool _isDisposed;

		///<summary>True while an image is held.</summary>
		public bool HasImage {
			get {
				lock(Lock) {
					return Image3D!=null;
				}
			}
		}

		public bool IsDisposed {
			get {
				lock(Lock) {
					return _isDisposed;
				}
			}
		}

		///<summary>Keeps the image for the scene, unless the cache has been disposed.</summary>
		internal void Store(ToothScene scene,BitmapSource image3D) {
			lock(Lock) {
				if(_isDisposed) {
					return;
				}
				Scene3D=scene;
				Image3D=image3D;
			}
		}

		///<summary>The kept image when its scene has the same 3D layer as scene, otherwise null.</summary>
		internal BitmapSource GetReusable(ToothScene scene) {
			lock(Lock) {
				if(_isDisposed || Image3D==null || !scene.IsSame3DLayer(Scene3D)) {
					return null;
				}
				return Image3D;
			}
		}

		public void Dispose() {
			lock(Lock) {
				_isDisposed=true;
				Scene3D=null;
				Image3D=null;
			}
		}
	}

	///<summary>One image of the renderer, and what it did. The pixels are 32-bit BGRA, opaque, row by row with no padding.</summary>
	public sealed class ToothChartRenderResult {
		public System.Drawing.Size Size;
		public byte[] PixelsBgra;
		///<summary>Set when the render failed (twice); the other fields are then empty.</summary>
		public Exception Exception;
		///<summary>True when the 3D layer image of the layer cache was reused (a change of the 2D layer only).</summary>
		public bool IsLayer3DReused;
		///<summary>The supersampling factor of the 3D layer: 3, or less for a big image (8 megapixels at most). 0 when the layer was reused.</summary>
		public int Factor3D;
		///<summary>The DPI scale of the roots that were rendered (VisualTreeHelper.GetDpi): both must be 1.0 (section 5.6).</summary>
		public double RootDpiScaleX2D;
		public double RootDpiScaleY2D;
		public double RootDpiScaleX3D;
		public double RootDpiScaleY3D;
		public List<ChartTextReport> ListTexts=new List<ChartTextReport>();
		///<summary>How many tries it took: 1, or 2 after a failed first try.</summary>
		public int CountTries;
		///<summary>Milliseconds on the render thread: the whole job, and the 3D layer's part (0 when reused).</summary>
		public double MillisecondsRender;
		public double Milliseconds3D;
		///<summary>The parts of the 3D layer's time: building the frozen models, rasterizing at the supersampling factor, and the Fant reduction with
		///its copy. And the composite's time: drawing the 2D layer over the 3D layer image, rasterizing it and copying the pixels out.</summary>
		public double Milliseconds3DModel;
		public double Milliseconds3DRasterize;
		public double Milliseconds3DReduce;
		public double MillisecondsComposite;
		///<summary>The managed thread ID of the render thread, for the thread tests.</summary>
		public int ThreadIdRender;

		///<summary>A new bitmap of the pixels in the format TC-0 recorded for the DirectX chart's GetBitmap: Format24bppRgb (every pixel opaque).</summary>
		public System.Drawing.Bitmap ToBitmap() {
			int width=Size.Width;
			int height=Size.Height;
			System.Drawing.Bitmap bitmap=new System.Drawing.Bitmap(width,height,System.Drawing.Imaging.PixelFormat.Format24bppRgb);
			System.Drawing.Imaging.BitmapData bitmapData=bitmap.LockBits(new System.Drawing.Rectangle(0,0,width,height),
				System.Drawing.Imaging.ImageLockMode.WriteOnly,System.Drawing.Imaging.PixelFormat.Format24bppRgb);
			try {
				byte[] arrayRow=new byte[bitmapData.Stride];
				for(int y=0;y<height;y++) {
					int indexSource=y*width*4;
					for(int x=0;x<width;x++) {
						arrayRow[x*3]=PixelsBgra[indexSource];
						arrayRow[x*3+1]=PixelsBgra[indexSource+1];
						arrayRow[x*3+2]=PixelsBgra[indexSource+2];
						indexSource+=4;
					}
					Marshal.Copy(arrayRow,0,bitmapData.Scan0+y*bitmapData.Stride,width*3);
				}
			}
			finally {
				bitmap.UnlockBits(bitmapData);
			}
			return bitmap;
		}
	}

	///<summary>The WPF chart's renderer (docs/specs/wpf-tooth-chart-plan.md, section 5.4): one render thread per process at a time. The thread is STA,
	///a background thread with its own Dispatcher, and makes every image: the on-screen image, GetBitmap, prints, sheets and tests. It builds and
	///freezes the meshes once (ToothMeshCache) and keeps the 3D layer's visual tree. It never reads ToothChartData and never waits for another
	///thread, so a caller waiting for it cannot deadlock, and no WPF object is ever made on a caller's thread: the dashboard's MTA worker gets the
	///WPF chart.
	///An image: the 3D layer is rendered into a RenderTargetBitmap at 3x (capped at 8 megapixels) over the opaque background and reduced with WIC's
	///Fant scaler (TransformedBitmap); it is drawn with the 2D layer into a second RenderTargetBitmap at the exact pixel size, and the pixels are
	///copied out with CopyPixels. One RenderTargetBitmap per pixel size is reused with Clear(), for at most three chart sizes (the least recently
	///used is freed), so a loop never makes bitmaps fast enough to hit dotnet/wpf#3067. RenderTargetBitmap always renders in software, so no graphics
	///driver changes an image.
	///Each render thread keeps its own meshes, render targets, materials and visual tree (RenderThread), made on that thread and used only by its
	///jobs: a thread started after a Shutdown whose wait ran out never touches those of the old thread, which may still be in its last job. No lock
	///is held while a caller waits for the render thread. The start check (EnsureStarted) waits behind a gate of its own, so WPF's cold start on one
	///caller's behalf never blocks another caller's render, ShowScene or a read of the thread. A job that a shutdown aborts before it ran completes
	///its caller at once: Render throws, and RenderAsync's callback gets a failed result.
	///Each job catches its own exceptions and is tried once more after a failure. After FailuresInRowBeforeReset failed jobs in a row the thread
	///drops its visual tree, materials and render targets, and after FailuresInRowBeforeRestart the renderer retires the thread, so the next job
	///starts a new one (at most MaxRestartsAfterFailures times in a process). An unhandled exception on any thread ends a .NET Framework process,
	///so the render thread has two last guards: its Dispatcher.UnhandledException handler sets Handled for what a job lets out, and a catch around
	///Dispatcher.Run takes what the Dispatcher's own shutdown raises outside every job. A waiter's Aborted handler cannot throw there (JobWait). A
	///thread ends with its Dispatcher's shutdown, never Thread.Abort.</summary>
	public sealed class ToothChartRenderer {
		#region Fields
		///<summary>A render job waits at most this long for the render thread before the caller gives up (and falls back to the 2D chart).</summary>
		public static int MillisecondsTimeout=60000;
		///<summary>The 3D layer's supersampling factor and pixel cap.</summary>
		public const int Supersampling=3;
		public const int MaxPixels3D=8000000;
		///<summary>0: the chart's factor (Supersampling). Set only by the render-time measurement (TC-1, O-19), which also times the factors 2 and 1
		///so the owner's choice can weigh them; never set by the program.</summary>
		public static int SupersamplingForMeasurement=0;
		///<summary>How many chart sizes keep their render targets.</summary>
		public const int MaxSizesKept=3;
		///<summary>After this many render jobs in a row failed (each tried twice), the render thread drops its visual tree, materials and render
		///targets, so the next job makes them again; and again after each further run of as many.</summary>
		public const int FailuresInRowBeforeReset=3;
		///<summary>After this many render jobs in a row failed, the renderer retires the render thread (a broken render channel outlives a new visual
		///tree): it ends after its job, and the next job starts a new thread.</summary>
		public const int FailuresInRowBeforeRestart=6;
		///<summary>How many times in a process failed jobs may retire the render thread. After that the thread stays, and only resets.</summary>
		public const int MaxRestartsAfterFailures=3;
		///<summary>For tests only, null in the program: runs on the render thread at the start of the start check's job (with JobCheck) and of
		///each try of a render job (with JobRender), so a test can hold the render thread inside a job or make a job fail.</summary>
		public static Action<string> ActionJobStartForTests;
		public const string JobCheck="check";
		public const string JobRender="render";
		private static readonly ToothChartRenderer _instance=new ToothChartRenderer();
		private static int _countUnhandledExceptions;
		///<summary>Guards _renderThread, _exceptionStart, _isExitHooked and the counts. Held only to read or set them, never while waiting.</summary>
		private readonly object _lockState=new object();
		///<summary>Held by the one caller that starts a render thread, for as long as the thread takes to make its Dispatcher; never across a job.</summary>
		private readonly object _lockThreadStart=new object();
		///<summary>The start gate: only EnsureStarted takes it, so one start check runs at a time and no render or read waits for it.</summary>
		private readonly object _lockCheck=new object();
		///<summary>The render thread that takes new jobs, or null before the first job and after a Shutdown or a retirement.</summary>
		private RenderThread _renderThread;
		private Exception _exceptionStart;
		private bool _isExitHooked;
		private int _countResets;
		private int _countRestarts;
		#endregion Fields

		private ToothChartRenderer() {
		}

		///<summary>The renderer of this process. Its thread starts with EnsureStarted or the first render.</summary>
		public static ToothChartRenderer Instance {
			get {
				return _instance;
			}
		}

		///<summary>How many exceptions reached a render thread's last guards in this process: Dispatcher.UnhandledException, and the catch around
		///Dispatcher.Run (which also ends that thread). Every job catches its own, and nothing the renderer adds to the Dispatcher's shutdown throws,
		///so this stays 0.</summary>
		public static int CountUnhandledExceptions {
			get {
				return _countUnhandledExceptions;
			}
		}

		///<summary>The managed thread ID of the render thread that takes new jobs, or 0 when there is none.</summary>
		public int ThreadId {
			get {
				RenderThread renderThread=GetRenderThreadCurrent();
				return renderThread==null?0:renderThread.Thread.ManagedThreadId;
			}
		}

		///<summary>The render thread's apartment (STA), for the thread tests.</summary>
		public ApartmentState ThreadApartment {
			get {
				RenderThread renderThread=GetRenderThreadCurrent();
				return renderThread==null?ApartmentState.Unknown:renderThread.Thread.GetApartmentState();
			}
		}

		public bool IsThreadAlive {
			get {
				RenderThread renderThread=GetRenderThreadCurrent();
				return renderThread!=null && renderThread.Thread.IsAlive;
			}
		}

		///<summary>How many times failed jobs in a row made a render thread drop its visual tree, materials and render targets, for tests.</summary>
		public int CountResetsAfterFailures {
			get {
				lock(_lockState) {
					return _countResets;
				}
			}
		}

		///<summary>How many times failed jobs in a row retired the render thread, for tests.</summary>
		public int CountRestartsAfterFailures {
			get {
				lock(_lockState) {
					return _countRestarts;
				}
			}
		}

		#region Start and stop
		///<summary>Starts the render thread if there is none and, the first time on each thread, makes a 1x1 3D image there, so a computer where WPF
		///cannot draw is found when the wrapper starts the engine (which then falls back to the 2D chart). Throws when the thread or the check fails;
		///the check's failure is kept, so later calls throw it again at once. A caller's own ThreadAbortException or ThreadInterruptedException
		///(an ODThread's finite Join aborts the thread it waits for) is the caller's matter: it is thrown on and not kept, and the next call checks
		///again. Only this method waits behind the start gate, so the cold check holds no lock that a render waits for.</summary>
		public void EnsureStarted() {
			ThrowIfStartFailed();
			lock(_lockCheck) {
				ThrowIfStartFailed();
				RenderThread renderThread=GetRenderThread();
				if(renderThread.IsChecked) {
					return;
				}
				try {
					RunAndWait(renderThread,renderThreadJob => {
						ActionJobStartForTests?.Invoke(JobCheck);
						ToothScene sceneCheck=new ToothScene(new System.Drawing.Size(1,1),System.Drawing.Color.Black,false,1,1,new List<ToothSceneItem3D>(),
							new List<ToothSceneItem2D>());
						renderThreadJob.Render3DLayer(sceneCheck,null);
						renderThreadJob.ListRenderTargets.Clear();//the check's 1x1 targets take no place of a chart size
						return true;
					},DispatcherPriority.Normal);
				}
				catch(ThreadAbortException) {
					throw;
				}
				catch(ThreadInterruptedException) {
					throw;
				}
				catch(Exception ex) {
					lock(_lockState) {
						_exceptionStart=ex;
					}
					throw new InvalidOperationException("The WPF tooth chart's renderer could not start: "+ex.Message,ex);
				}
				renderThread.IsChecked=true;
			}
		}

		private void ThrowIfStartFailed() {
			Exception exceptionStart;
			lock(_lockState) {
				exceptionStart=_exceptionStart;
			}
			if(exceptionStart!=null) {
				throw new InvalidOperationException("The WPF tooth chart's renderer could not start: "+exceptionStart.Message,exceptionStart);
			}
		}

		///<summary>The render thread that takes new jobs, started when there is none or it was retired or ended. Only the start of a thread waits,
		///behind its own lock, for the moment the thread takes to make its Dispatcher.</summary>
		private RenderThread GetRenderThread() {
			RenderThread renderThread=GetRenderThreadUsable();
			if(renderThread!=null) {
				return renderThread;
			}
			lock(_lockThreadStart) {
				renderThread=GetRenderThreadUsable();
				if(renderThread!=null) {
					return renderThread;//another caller started one meanwhile
				}
				renderThread=RenderThread.Start(this);
				lock(_lockState) {
					_renderThread=renderThread;
					if(!_isExitHooked) {
						_isExitHooked=true;
						AppDomain.CurrentDomain.ProcessExit+=AppDomain_Exit;
						AppDomain.CurrentDomain.DomainUnload+=AppDomain_Exit;
					}
				}
				return renderThread;
			}
		}

		private RenderThread GetRenderThreadUsable() {
			lock(_lockState) {
				if(_renderThread!=null && _renderThread.IsUsable) {
					return _renderThread;
				}
				return null;
			}
		}

		private RenderThread GetRenderThreadCurrent() {
			lock(_lockState) {
				return _renderThread;
			}
		}

		///<summary>At process exit or when the test AppDomain unloads: the Dispatcher shuts down on its own thread (posted, so this never blocks on a
		///busy render), and the thread gets a second to end. A background thread never keeps the process alive either way.</summary>
		private void AppDomain_Exit(object sender,EventArgs e) {
			Shutdown(1000);
		}

		///<summary>Ends the render thread through its Dispatcher's shutdown (never Thread.Abort) and waits up to millisecondsWait for it. The jobs
		///still queued on it are aborted, and their callers hear of it at once. The next job starts a new thread, with its own targets and visual
		///tree, even while the old one finishes its last job.</summary>
		public void Shutdown(int millisecondsWait) {
			RenderThread renderThread;
			lock(_lockState) {
				renderThread=_renderThread;
				_renderThread=null;
			}
			renderThread?.Stop(millisecondsWait);
		}

		///<summary>On the render thread, after FailuresInRowBeforeRestart failed jobs in a row: the thread takes no new jobs and ends after its current
		///one (the jobs still queued on it are aborted, and their callers hear of it at once), and the next job starts a new thread. False, with
		///nothing done, when the process has used its MaxRestartsAfterFailures.</summary>
		private bool RetireAfterFailures(RenderThread renderThread) {
			lock(_lockState) {
				if(_countRestarts>=MaxRestartsAfterFailures) {
					return false;
				}
				_countRestarts++;
				if(_renderThread==renderThread) {
					_renderThread=null;
				}
			}
			renderThread.Stop(0);
			return true;
		}

		private void CountResetAfterFailures() {
			lock(_lockState) {
				_countResets++;
			}
		}

		private static void Dispatcher_UnhandledException(object sender,DispatcherUnhandledExceptionEventArgs e) {
			e.Handled=true;
			Interlocked.Increment(ref _countUnhandledExceptions);
			ToothChartWrapper.LogFallbackOnce(ToothChartFallbackKind.Wpf3DRenderThread,"The WPF tooth chart's render thread caught an unexpected error ("
				+e.Exception.GetType().Name+": "+e.Exception.Message+").");
		}
		#endregion Start and stop

		#region Jobs
		///<summary>Renders the scene on the render thread and waits for it. layerCache is the wrapper's cache of its last 3D layer, or null. Throws when
		///the render fails twice, the render thread does not answer in time, or it shut down before it ran the job; the wrapper then returns the 2D
		///chart's image.</summary>
		public ToothChartRenderResult Render(ToothScene scene,ToothChartLayerCache layerCache) {
			ToothChartRenderResult result=RunAndWait(renderThread => renderThread.RunJob(scene,layerCache));
			if(result.Exception!=null) {
				throw new InvalidOperationException("The WPF tooth chart could not draw its image: "+result.Exception.Message,result.Exception);
			}
			return result;
		}

		///<summary>Queues the scene and returns at once. actionDone runs once: on the render thread with the result (its Exception set when the render
		///failed twice), or with a failed result when the render thread shut down before it ran the job (then on that thread during its shutdown, or
		///at once on this thread). It must not block, and what it throws is caught. Jobs run in the order queued.</summary>
		public void RenderAsync(ToothScene scene,ToothChartLayerCache layerCache,Action<ToothChartRenderResult> actionDone) {
			RenderThread renderThread=GetRenderThread();
			int isDone=0;
			Action<ToothChartRenderResult> actionDoneOnce=result => {
				if(Interlocked.Exchange(ref isDone,1)==1) {
					return;
				}
				try {
					actionDone?.Invoke(result);
				}
				catch(Exception ex) {
					ToothChartWrapper.LogFallbackOnce(ToothChartFallbackKind.Wpf3DRenderThread,"The WPF tooth chart's image could not be handed on ("
						+ex.GetType().Name+": "+ex.Message+").");
				}
			};
			DispatcherOperation dispatcherOperation=renderThread.DispatcherThread.BeginInvoke(DispatcherPriority.Normal,new Action(() => {
				actionDoneOnce(renderThread.RunJob(scene,layerCache));
			}));
			dispatcherOperation.Aborted+=(sender,e) => actionDoneOnce(CreateAbortedResult(scene));
			if(dispatcherOperation.Status==DispatcherOperationStatus.Aborted) {
				actionDoneOnce(CreateAbortedResult(scene));//the thread had shut down: BeginInvoke aborts without raising Aborted
			}
		}

		///<summary>Waits until every job queued before this call has run, for tests.</summary>
		public void WaitIdle() {
			RunAndWait(renderThread => true,DispatcherPriority.ApplicationIdle);
		}

		///<summary>The number of models whose meshes are built, for tests.</summary>
		public int GetCountMeshSets() {
			return RunAndWait(renderThread => renderThread.ToothMeshCache.CountMeshSets);
		}

		///<summary>The number of chart sizes whose render targets are kept, for tests.</summary>
		public int GetCountSizesKept() {
			return RunAndWait(renderThread => renderThread.ListRenderTargets.Count);
		}

		private T RunAndWait<T>(Func<RenderThread,T> func,DispatcherPriority dispatcherPriority=DispatcherPriority.Normal) {
			return RunAndWait(GetRenderThread(),func,dispatcherPriority);
		}

		///<summary>Runs func on the thread and waits for it. Throws when func throws, when the thread does not answer within MillisecondsTimeout, and
		///at once when the thread shut down before it ran func.</summary>
		private static T RunAndWait<T>(RenderThread renderThread,Func<RenderThread,T> func,DispatcherPriority dispatcherPriority) {
			if(renderThread.DispatcherThread.CheckAccess()) {
				return func(renderThread);//on the render thread already (a check from inside a job)
			}
			JobWait<T> jobWait=new JobWait<T>();
			DispatcherOperation dispatcherOperation=renderThread.DispatcherThread.BeginInvoke(dispatcherPriority,new Action(() => {
				try {
					jobWait.Result=func(renderThread);
				}
				catch(Exception ex) {
					jobWait.Exception=ex;
				}
				finally {
					jobWait.IsRun=true;
					jobWait.Signal();
				}
			}));
			//A shutdown can abort the job before this handler is added and raise Aborted only after this caller saw the status below, returned
			//and closed the wait; Signal then does nothing (JobWait).
			dispatcherOperation.Aborted+=(sender,e) => jobWait.Signal();
			if(dispatcherOperation.Status==DispatcherOperationStatus.Aborted) {
				jobWait.Signal();//aborted already: by a shutdown under way, or by BeginInvoke itself when the thread had shut down (no Aborted then)
			}
			if(!jobWait.Wait(MillisecondsTimeout)) {
				throw new TimeoutException("The WPF tooth chart's render thread did not answer within "+MillisecondsTimeout+" ms.");
			}
			jobWait.Close();
			if(!jobWait.IsRun) {
				throw new InvalidOperationException("The WPF tooth chart's render thread shut down before it ran the job.");
			}
			if(jobWait.Exception!=null) {
				throw new InvalidOperationException(jobWait.Exception.Message,jobWait.Exception);
			}
			return jobWait.Result;
		}

		private static ToothChartRenderResult CreateAbortedResult(ToothScene scene) {
			ToothChartRenderResult result=new ToothChartRenderResult();
			result.Size=scene.Size;
			result.Exception=new InvalidOperationException("The WPF tooth chart's render thread shut down before it ran the job.");
			return result;
		}

		///<summary>What a caller of RunAndWait waits for. Written on the render thread before Signal. Signal and Close take the same lock, and a
		///Signal after Close does nothing. A shutdown aborts a queued job (its status turns Aborted under the Dispatcher's lock) before it raises the
		///job's Aborted event (after that lock is released), so a caller that adds its handler in between sees the status, returns and closes the
		///wait before the render thread runs the handler. Set on the closed event would throw ObjectDisposedException in the Dispatcher's shutdown,
		///outside every job, where Dispatcher.UnhandledException never sees it.</summary>
		private sealed class JobWait<T> {
			private readonly object _lock=new object();
			private readonly ManualResetEvent _manualResetEventDone=new ManualResetEvent(false);
			private bool _isClosed;
			public T Result;
			public Exception Exception;
			public bool IsRun;

			///<summary>Wakes the caller; nothing once the caller has closed the wait. Never throws.</summary>
			public void Signal() {
				lock(_lock) {
					if(_isClosed) {
						return;
					}
					_manualResetEventDone.Set();
				}
			}

			///<summary>On the caller's thread: true when signalled within millisecondsTimeout.</summary>
			public bool Wait(int millisecondsTimeout) {
				return _manualResetEventDone.WaitOne(millisecondsTimeout);
			}

			///<summary>On the caller's thread, after Wait returned true.</summary>
			public void Close() {
				lock(_lock) {
					_isClosed=true;
					_manualResetEventDone.Close();
				}
			}
		}
		#endregion Jobs

		#region Static parts of the image
		///<summary>3, or the largest whole factor that keeps the 3D layer at or below 8 megapixels (at least 1).</summary>
		public static int GetSupersamplingFactor(System.Drawing.Size size) {
			long pixels=(long)size.Width*size.Height;
			int factor=Supersampling;
			if(SupersamplingForMeasurement>0) {
				factor=SupersamplingForMeasurement;
			}
			while(factor>1 && pixels*factor*factor>MaxPixels3D) {
				factor--;
			}
			return factor;
		}

		///<summary>The three-part material of section 5.3, frozen.</summary>
		public static MaterialGroup CreateMaterial(System.Drawing.Color color,float specular,float diffuseFactor,float emissiveFactor) {
			MaterialGroup materialGroup=new MaterialGroup();
			materialGroup.Children.Add(new DiffuseMaterial(CreateBrush(color,diffuseFactor)));
			byte specularByte=ToByte(specular*255.0);
			SolidColorBrush brushSpecular=new SolidColorBrush(Color.FromRgb(specularByte,specularByte,specularByte));
			brushSpecular.Freeze();
			materialGroup.Children.Add(new SpecularMaterial(brushSpecular,ToothScene.SpecularPower));
			materialGroup.Children.Add(new EmissiveMaterial(CreateBrush(color,emissiveFactor)));
			materialGroup.Freeze();
			return materialGroup;
		}

		private static SolidColorBrush CreateBrush(System.Drawing.Color color,float factor) {
			SolidColorBrush brush=new SolidColorBrush(Color.FromRgb(ToByte(color.R*(double)factor),ToByte(color.G*(double)factor),ToByte(color.B*(double)factor)));
			brush.Freeze();
			return brush;
		}

		private static byte ToByte(double value) {
			return (byte)Math.Max(0,Math.Min(255,Math.Round(value,MidpointRounding.AwayFromZero)));
		}

		///<summary>DirectX's one directional light, specular part 0.8, in WPF's right-handed space: DirectX's direction (0.5,-0.2,1) with Z negated.</summary>
		public static DirectionalLight CreateLight() {
			byte intensity=ToByte(ToothScene.LightIntensity*255.0);
			DirectionalLight directionalLight=new DirectionalLight(Color.FromRgb(intensity,intensity,intensity),new Vector3D(0.5,-0.2,-1));
			directionalLight.Freeze();
			return directionalLight;
		}

		///<summary>The camera of a scene (section 5.3): orthographic, as wide as the scene's projection, 400 mm in front of the teeth looking down -Z
		///with +Y up, centred half a pixel off the origin (ToothScene.CameraCenterX/Y) so DirectX 9's pixel centres line up. Near 0 and far 1000 mm,
		///DirectX's depth range. Not frozen; made on the calling thread.</summary>
		public static OrthographicCamera CreateCamera(ToothScene scene) {
			OrthographicCamera orthographicCamera=new OrthographicCamera();
			orthographicCamera.Position=new Point3D(scene.CameraCenterX,scene.CameraCenterY,ToothScene.CameraDistance);
			orthographicCamera.LookDirection=new Vector3D(0,0,-1);
			orthographicCamera.UpDirection=new Vector3D(0,1,0);
			orthographicCamera.Width=scene.WidthProjection;
			orthographicCamera.NearPlaneDistance=0;
			orthographicCamera.FarPlaneDistance=1000;
			return orthographicCamera;
		}

		///<summary>The WPF matrix of a model transform. Both are for row vectors, so the elements map one for one.</summary>
		public static Matrix3D ToMatrix3D(ChartMatrix m) {
			return new Matrix3D(m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44);
		}

		///<summary>The scene's 3D models with the one light, frozen: each item's frozen mesh from meshCache (reversed when its transform mirrors) with its
		///material from materialCache and its transform. A model the cache does not have (a group index past the model's groups) is left out. Shared by
		///the render thread and the live view (ToothChartWpfView), each with the caches of its own thread.</summary>
		public static Model3DGroup BuildModel(ToothScene scene,ToothMeshCache meshCache,ToothMaterialCache materialCache) {
			Model3DGroup model3DGroup=new Model3DGroup();
			model3DGroup.Children.Add(CreateLight());
			foreach(ToothSceneItem3D item in scene.Items3D) {
				MeshGeometry3D mesh=meshCache.GetMesh(item.ToothId,item.GroupIndex,item.IsReversed);
				if(mesh==null) {
					continue;
				}
				GeometryModel3D geometryModel3D=new GeometryModel3D(mesh,materialCache.GetMaterial(item.Color,item.Specular,scene));
				MatrixTransform3D matrixTransform3D=new MatrixTransform3D(ToMatrix3D(item.Transform));
				matrixTransform3D.Freeze();
				geometryModel3D.Transform=matrixTransform3D;
				model3DGroup.Children.Add(geometryModel3D);
			}
			model3DGroup.Freeze();
			return model3DGroup;
		}
		#endregion Static parts of the image

		#region Render thread
		///<summary>One render thread and what its jobs keep: the meshes, the render targets, the materials and the 3D layer's visual tree. Start makes
		///it. Everything but the flags and DispatcherThread is used only on the thread itself, by its jobs, so a thread started after a Shutdown has
		///its own and never meets the old thread's.</summary>
		private sealed class RenderThread {
			private readonly ToothChartRenderer _toothChartRenderer;
			public readonly Thread Thread;
			///<summary>The thread's Dispatcher, set on the thread before Start returns.</summary>
			public Dispatcher DispatcherThread;
			///<summary>True once the start check passed on this thread. Read and set under the start gate only.</summary>
			public bool IsChecked;
			///<summary>True once Shutdown or failed jobs retired the thread: it takes no new jobs.</summary>
			public volatile bool IsRetired;
			private readonly ManualResetEvent _manualResetEventReady=new ManualResetEvent(false);
			private Exception _exceptionThreadStart;
			//The thread's own, used only by its jobs:
			public ToothMeshCache ToothMeshCache;
			public readonly List<RenderTargets> ListRenderTargets=new List<RenderTargets>();
			private readonly ToothMaterialCache _toothMaterialCache=new ToothMaterialCache();
			private ContainerVisual _containerVisual3D;
			private DrawingVisual _drawingVisualBackground;
			private Viewport3DVisual _viewport3DVisual;
			private ModelVisual3D _modelVisual3D;
			private int _countFailuresInRow;

			private RenderThread(ToothChartRenderer toothChartRenderer) {
				_toothChartRenderer=toothChartRenderer;
				Thread=new Thread(Run);
				Thread.SetApartmentState(ApartmentState.STA);
				Thread.IsBackground=true;
				Thread.Name="Tooth chart renderer";
			}

			///<summary>Starts a render thread and waits until it has its Dispatcher. Throws when it does not start.</summary>
			public static RenderThread Start(ToothChartRenderer toothChartRenderer) {
				RenderThread renderThread=new RenderThread(toothChartRenderer);
				renderThread.Thread.Start();
				if(!renderThread._manualResetEventReady.WaitOne(MillisecondsTimeout)) {
					throw new TimeoutException("The WPF tooth chart's render thread did not start.");
				}
				renderThread._manualResetEventReady.Close();
				if(renderThread._exceptionThreadStart!=null) {
					throw new InvalidOperationException("The WPF tooth chart's render thread could not start: "+renderThread._exceptionThreadStart.Message,
						renderThread._exceptionThreadStart);
				}
				return renderThread;
			}

			private void Run() {
				try {
					DispatcherThread=Dispatcher.CurrentDispatcher;
					DispatcherThread.UnhandledException+=Dispatcher_UnhandledException;
					ToothMeshCache=new ToothMeshCache();
				}
				catch(Exception ex) {
					_exceptionThreadStart=ex;
					_manualResetEventReady.Set();
					return;
				}
				_manualResetEventReady.Set();
				try {
					Dispatcher.Run();
				}
				catch(ThreadAbortException) {
					throw;//the test AppDomain's unload aborts what is left of its threads; not an error of the chart
				}
				catch(Exception ex) {
					//The thread's last guard. What the Dispatcher's own shutdown raises outside every job (an Aborted or ShutdownFinished
					//handler; ShutdownStarted runs inside the posted shutdown job) leaves Dispatcher.Run, where Dispatcher.UnhandledException never
					//sees it, and an unhandled exception on any thread ends a .NET Framework process. The thread ends here; the next job starts a new one. A job still queued on this
					//thread then waits out MillisecondsTimeout, and its caller falls back to the 2D chart.
					IsRetired=true;
					Interlocked.Increment(ref _countUnhandledExceptions);
					ToothChartWrapper.LogFallbackOnce(ToothChartFallbackKind.Wpf3DRenderThread,"The WPF tooth chart's render thread ended on an unexpected error ("
						+ex.GetType().Name+": "+ex.Message+").");
				}
			}

			///<summary>True while the thread takes new jobs: not retired, alive, and its Dispatcher not shutting down.</summary>
			public bool IsUsable {
				get {
					return !IsRetired && Thread.IsAlive && !DispatcherThread.HasShutdownStarted;
				}
			}

			///<summary>Retires the thread and posts its Dispatcher's shutdown, which runs after the current job; waits up to millisecondsWait for the
			///thread to end (never on the thread itself).</summary>
			public void Stop(int millisecondsWait) {
				IsRetired=true;
				try {
					DispatcherThread.BeginInvokeShutdown(DispatcherPriority.Send);
					if(Thread!=Thread.CurrentThread && millisecondsWait>0) {
						Thread.Join(millisecondsWait);
					}
				}
				catch(Exception ex) {
					ex.DoNothing();
				}
			}

			///<summary>One render, on this thread, tried twice. Never throws: a failure is the result's Exception, and failures in a row reset or
			///retire the thread (FailuresInRowBeforeReset, FailuresInRowBeforeRestart).</summary>
			public ToothChartRenderResult RunJob(ToothScene scene,ToothChartLayerCache layerCache) {
				Exception exceptionFirst=null;
				for(int tryNum=1;tryNum<=2;tryNum++) {
					try {
						ActionJobStartForTests?.Invoke(JobRender);
						ToothChartRenderResult result=RenderOnThread(scene,layerCache);
						result.CountTries=tryNum;
						_countFailuresInRow=0;
						return result;
					}
					catch(Exception ex) {
						//dotnet/wpf#3067 and its kin: a COMException from a render target. The targets are dropped, and the second try makes new ones.
						exceptionFirst=exceptionFirst??ex;
						ListRenderTargets.Clear();
						if(tryNum==2) {
							OnJobFailed(ex);
							ToothChartRenderResult resultFailed=new ToothChartRenderResult();
							resultFailed.Size=scene.Size;
							resultFailed.Exception=ex;
							resultFailed.CountTries=tryNum;
							return resultFailed;
						}
					}
				}
				ToothChartRenderResult resultNone=new ToothChartRenderResult();
				resultNone.Exception=exceptionFirst??new InvalidOperationException("No render was tried.");
				return resultNone;
			}

			///<summary>After a job failed twice. A failure that comes from the visual tree or the thread's render channel repeats on every later image,
			///so a run of FailuresInRowBeforeReset drops the visual tree, the materials and the render targets, and a run of FailuresInRowBeforeRestart
			///retires the thread.</summary>
			private void OnJobFailed(Exception ex) {
				_countFailuresInRow++;
				if(_countFailuresInRow>=FailuresInRowBeforeRestart && _toothChartRenderer.RetireAfterFailures(this)) {
					ToothChartWrapper.LogFallbackOnce(ToothChartFallbackKind.Wpf3DRenderThread,"The WPF tooth chart's render thread failed "+_countFailuresInRow
						+" images in a row ("+ex.GetType().Name+": "+ex.Message+"); a new render thread takes over.");
					return;
				}
				if(_countFailuresInRow%FailuresInRowBeforeReset==0) {
					ListRenderTargets.Clear();
					_toothMaterialCache.Clear();
					_containerVisual3D=null;
					_drawingVisualBackground=null;
					_viewport3DVisual=null;
					_modelVisual3D=null;
					_toothChartRenderer.CountResetAfterFailures();
				}
			}

			private ToothChartRenderResult RenderOnThread(ToothScene scene,ToothChartLayerCache layerCache) {
				Stopwatch stopwatch=Stopwatch.StartNew();
				int width=scene.Size.Width;
				int height=scene.Size.Height;
				if(width<1 || height<1) {
					throw new ArgumentException("The chart image has no size ("+width+"x"+height+").");
				}
				ToothChartRenderResult result=new ToothChartRenderResult();
				result.Size=scene.Size;
				result.ThreadIdRender=Thread.CurrentThread.ManagedThreadId;
				BitmapSource image3D=null;
				if(layerCache!=null) {
					image3D=layerCache.GetReusable(scene);
				}
				if(image3D!=null) {
					result.IsLayer3DReused=true;
				}
				else {
					Stopwatch stopwatch3D=Stopwatch.StartNew();
					image3D=Render3DLayer(scene,result);
					result.Milliseconds3D=stopwatch3D.Elapsed.TotalMilliseconds;
					if(layerCache!=null) {
						layerCache.Store(scene,image3D);
					}
				}
				Stopwatch stopwatchComposite=Stopwatch.StartNew();
				RenderTargetBitmap renderTargetBitmap=GetRenderTargets(scene.Size).GetComposite();
				renderTargetBitmap.Clear();
				ChartDrawingVisual chartDrawingVisual=new ChartDrawingVisual();
				VisualTreeHelper.SetRootDpi(chartDrawingVisual,new DpiScale(1,1));
				using(DrawingContext drawingContext=chartDrawingVisual.RenderOpen()) {
					drawingContext.DrawImage(image3D,new Rect(0,0,width,height));
					result.ListTexts=ChartLayer2D.Draw(drawingContext,scene);
				}
				renderTargetBitmap.Render(chartDrawingVisual);
				DpiScale dpiScale2D=VisualTreeHelper.GetDpi(chartDrawingVisual);
				result.RootDpiScaleX2D=dpiScale2D.DpiScaleX;
				result.RootDpiScaleY2D=dpiScale2D.DpiScaleY;
				byte[] arrayPixels=new byte[width*height*4];
				renderTargetBitmap.CopyPixels(arrayPixels,width*4,0);
				for(int i=3;i<arrayPixels.Length;i+=4) {
					arrayPixels[i]=255;//The background is opaque, so this changes nothing but makes "every alpha 255" a guarantee.
				}
				result.PixelsBgra=arrayPixels;
				result.MillisecondsComposite=stopwatchComposite.Elapsed.TotalMilliseconds;
				result.MillisecondsRender=stopwatch.Elapsed.TotalMilliseconds;
				return result;
			}

			///<summary>The 3D layer at the exact pixel size: the background and the lit teeth, rendered at the supersampling factor and reduced with
			///WIC's Fant scaler, frozen. result, when given, gets the factor, the root's DPI scale and the times of the parts.</summary>
			public BitmapSource Render3DLayer(ToothScene scene,ToothChartRenderResult result) {
				int width=scene.Size.Width;
				int height=scene.Size.Height;
				int factor=GetSupersamplingFactor(scene.Size);
				Stopwatch stopwatch=Stopwatch.StartNew();
				EnsureVisualTree3D();
				using(DrawingContext drawingContext=_drawingVisualBackground.RenderOpen()) {
					SolidColorBrush brushBackground=new SolidColorBrush(Color.FromRgb(scene.ColorBackground.R,scene.ColorBackground.G,scene.ColorBackground.B));
					brushBackground.Freeze();
					drawingContext.DrawRectangle(brushBackground,null,new Rect(0,0,width,height));
				}
				_viewport3DVisual.Viewport=new Rect(0,0,width,height);
				_viewport3DVisual.Camera=CreateCamera(scene);
				_modelVisual3D.Content=BuildModel(scene,ToothMeshCache,_toothMaterialCache);
				VisualTreeHelper.SetRootDpi(_containerVisual3D,new DpiScale(1,1));
				double millisecondsModel=stopwatch.Elapsed.TotalMilliseconds;
				stopwatch.Restart();
				RenderTargetBitmap renderTargetBitmap=GetRenderTargets(scene.Size).GetLayer3D(factor);
				renderTargetBitmap.Clear();
				renderTargetBitmap.Render(_containerVisual3D);
				DpiScale dpiScale=VisualTreeHelper.GetDpi(_containerVisual3D);
				_modelVisual3D.Content=null;//the frozen models are not kept past the render
				double millisecondsRasterize=stopwatch.Elapsed.TotalMilliseconds;
				stopwatch.Restart();
				BitmapSource bitmapSource=renderTargetBitmap;
				if(factor>1) {
					bitmapSource=new TransformedBitmap(renderTargetBitmap,new ScaleTransform(1.0/factor,1.0/factor));
				}
				if(bitmapSource.PixelWidth<width || bitmapSource.PixelHeight<height) {
					throw new InvalidOperationException("The reduced 3D layer is "+bitmapSource.PixelWidth+"x"+bitmapSource.PixelHeight+", not "+width+"x"+height+".");
				}
				byte[] arrayPixels=new byte[width*height*4];
				bitmapSource.CopyPixels(new Int32Rect(0,0,width,height),arrayPixels,width*4,0);
				BitmapSource image3D=BitmapSource.Create(width,height,96,96,PixelFormats.Pbgra32,null,arrayPixels,width*4);
				image3D.Freeze();
				if(result!=null) {
					result.Factor3D=factor;
					result.RootDpiScaleX3D=dpiScale.DpiScaleX;
					result.RootDpiScaleY3D=dpiScale.DpiScaleY;
					result.Milliseconds3DModel=millisecondsModel;
					result.Milliseconds3DRasterize=millisecondsRasterize;
					result.Milliseconds3DReduce=stopwatch.Elapsed.TotalMilliseconds;
				}
				return image3D;
			}

			private void EnsureVisualTree3D() {
				if(_containerVisual3D!=null) {
					return;
				}
				_containerVisual3D=new ContainerVisual();
				_drawingVisualBackground=new DrawingVisual();
				_viewport3DVisual=new Viewport3DVisual();
				_modelVisual3D=new ModelVisual3D();
				_viewport3DVisual.Children.Add(_modelVisual3D);
				_containerVisual3D.Children.Add(_drawingVisualBackground);
				_containerVisual3D.Children.Add(_viewport3DVisual);
			}

			///<summary>The render targets of one chart size: the 3D layer's at the supersampling factor and the composite's at the exact size, made when
			///first needed. The list keeps the most recently used size first and at most MaxSizesKept sizes.</summary>
			private RenderTargets GetRenderTargets(System.Drawing.Size size) {
				RenderTargets renderTargets=null;
				for(int i=0;i<ListRenderTargets.Count;i++) {
					if(ListRenderTargets[i].Size==size) {
						renderTargets=ListRenderTargets[i];
						ListRenderTargets.RemoveAt(i);
						break;
					}
				}
				if(renderTargets==null) {
					renderTargets=new RenderTargets();
					renderTargets.Size=size;
				}
				ListRenderTargets.Insert(0,renderTargets);
				while(ListRenderTargets.Count>MaxSizesKept) {
					ListRenderTargets.RemoveAt(ListRenderTargets.Count-1);
				}
				return renderTargets;
			}
		}

		private class RenderTargets {
			public System.Drawing.Size Size;
			private RenderTargetBitmap _layer3D;
			private int _factor3D;
			private RenderTargetBitmap _composite;

			///<summary>The 3D layer's target at the factor: Size times factor, at 96 times factor dpi, so the viewport stays in the image's pixels.</summary>
			public RenderTargetBitmap GetLayer3D(int factor) {
				if(_layer3D==null || _factor3D!=factor) {
					_layer3D=new RenderTargetBitmap(Size.Width*factor,Size.Height*factor,96*factor,96*factor,PixelFormats.Pbgra32);
					_factor3D=factor;
				}
				return _layer3D;
			}

			///<summary>The composite's target at the exact size, 96 dpi.</summary>
			public RenderTargetBitmap GetComposite() {
				if(_composite==null) {
					_composite=new RenderTargetBitmap(Size.Width,Size.Height,96,96,PixelFormats.Pbgra32);
				}
				return _composite;
			}
		}
		#endregion Render thread
	}
	///<summary>The frozen three-part materials of section 5.3 (ToothChartRenderer.CreateMaterial) by colour, specular value and the scene's light
	///factors. Not thread-safe: one cache belongs to one thread (the render thread's, or the live view's UI thread). At most 4096 are kept.</summary>
	public sealed class ToothMaterialCache {
		private readonly Dictionary<string,Material> _dictMaterials=new Dictionary<string,Material>();

		///<summary>A material of three parts: a diffuse brush of the colour times DiffuseFactor, a specular brush of the specular value with power 70,
		///and an emissive brush of the colour times EmissiveFactor, which is DirectX's ambient term. Made and frozen the first time.</summary>
		public Material GetMaterial(System.Drawing.Color color,float specular,ToothScene scene) {
			string key=color.ToArgb()+"|"+specular.ToString("R")+"|"+scene.DiffuseFactor.ToString("R")+"|"+scene.EmissiveFactor.ToString("R");
			Material material;
			if(_dictMaterials.TryGetValue(key,out material)) {
				return material;
			}
			if(_dictMaterials.Count>4096) {
				_dictMaterials.Clear();
			}
			material=ToothChartRenderer.CreateMaterial(color,specular,scene.DiffuseFactor,scene.EmissiveFactor);
			_dictMaterials[key]=material;
			return material;
		}

		public void Clear() {
			_dictMaterials.Clear();
		}
	}
}
