// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Threading;
using ImagingCapture.Uvc.Interop;
using Microsoft.Win32.SafeHandles;

namespace ImagingCapture.Uvc.DirectShow {
	///<summary>One camera opened through DirectShow (design 8.2 and 8.3).
	///
	///Graph: the capture source filter is bound from the device moniker. IAMStreamConfig on its capture pin (PIN_CATEGORY_CAPTURE)
	///selects the stream format, and RenderStream renders that same pin through a Sample Grabber that accepts only RGB32 into a
	///Null Renderer; DirectShow inserts whatever decoder the format needs (for MJPG, the MJPEG decompressor). A preview pin, if
	///the device has one, is not used, so the selected format is the one shown and saved. When asked, the still pin
	///(PIN_CATEGORY_STILL) is rendered the same way to a second grabber; the preview never depends on it.
	///
	///Camera writes: IAMStreamConfig::SetFormat, once, before the capture pin is connected. Camera controls are only read (Get
	///and GetRange). No property page is opened and IAMVideoControl is not used.
	///
	///Threading: every COM object is created, called and released on this source's ComWorker thread. Sample Grabber callbacks
	///run on the graph's streaming threads and touch no COM object: they copy the buffer and raise FrameReady or StillCaptured.
	///Graph events (EC_DEVICE_LOST, EC_ERRORABORT) are read on the worker thread and raised as Error on a thread-pool thread.</summary>
	internal sealed class DirectShowFrameSource:IFrameSource {
		///<summary>How long Start waits for the graph to report that it is running. Bounds the wait so a driver that never
		///finishes the transition cannot hang the caller; streaming starts whenever the transition finishes.</summary>
		private const int RunTransitionTimeoutMs=5000;

		[ThreadStatic]
		private static bool _isInStreamingCallback;

		private readonly ComWorker _comWorker;
		private readonly FrameExchange _frameExchange=new FrameExchange();
		private readonly object _lockDispose=new object();
		private bool _isDisposeStarted;
		private volatile bool _isDisposed;
		private volatile bool _isStopping;
		private volatile bool _isRunning;
		private long _framesReceived;
		private long _framesDropped;
		private long _sequenceStill;

		#region COM objects, worker thread only
		private IGraphBuilder _graphBuilder;
		///<summary>Same wrapper as _graphBuilder. Not released separately.</summary>
		private IMediaControl _mediaControl;
		///<summary>Same wrapper as _graphBuilder. Not released separately.</summary>
		private IMediaEventEx _mediaEventEx;
		private ICaptureGraphBuilder2 _captureGraphBuilder;
		private IBaseFilter _filterSource;
		private IPin _pinCapture;
		///<summary>Same wrapper as _pinCapture, or null when the pin has no IAMStreamConfig. Not released separately.</summary>
		private IAMStreamConfig _streamConfig;
		private IPin _pinStill;
		private object _objCameraControl;
		///<summary>Same wrapper as _objCameraControl. Not released separately.</summary>
		private IAMCameraControl _cameraControl;
		private object _objVideoProcAmp;
		///<summary>Same wrapper as _objVideoProcAmp. Not released separately.</summary>
		private IAMVideoProcAmp _videoProcAmp;
		private IBaseFilter _filterGrabber;
		///<summary>Same wrapper as _filterGrabber. Not released separately.</summary>
		private ISampleGrabber _sampleGrabber;
		private IBaseFilter _filterNullRenderer;
		private IBaseFilter _filterGrabberStill;
		///<summary>Same wrapper as _filterGrabberStill. Not released separately.</summary>
		private ISampleGrabber _sampleGrabberStill;
		private IBaseFilter _filterNullRendererStill;
		private GrabberCallback _grabberCallbackPreview;
		private GrabberCallback _grabberCallbackStill;
		private GraphEventWaitHandle _waitHandleGraphEvent;
		#endregion COM objects, worker thread only

		#region State set on the worker thread
		private ReadOnlyCollection<UvcMode> _listModes=new ReadOnlyCollection<UvcMode>(new List<UvcMode>());
		private bool _isGraphRendered;
		private bool _isRenderFailed;
		private bool _isStillRequested;
		private volatile UvcMode _modeStarted;
		private volatile bool _isStillConnected;
		///<summary>Written before the graph runs, read by the streaming threads.</summary>
		private FrameGeometry _geometryPreview;
		private FrameGeometry _geometryStill;
		#endregion State set on the worker thread

		public UvcDeviceInfo Device { get; }
		public bool HasStillPin { get; private set; }
		///<summary>Why the still pin could not be connected, or "".</summary>
		public string StillPinError { get; private set; }="";

		public event EventHandler FrameReady;
		public event EventHandler<UvcStillEventArgs> StillCaptured;
		public event EventHandler<UvcErrorEventArgs> Error;

		private DirectShowFrameSource(UvcDeviceInfo device) {
			Device=device;
			_comWorker=new ComWorker("UVC camera");
		}

		///<summary>Binds the device and reads its modes, still pin and control interfaces. Throws UvcException.</summary>
		public static DirectShowFrameSource Open(UvcDeviceInfo device) {
			DirectShowFrameSource frameSource=new DirectShowFrameSource(device);
			try {
				frameSource._comWorker.Invoke(frameSource.OpenOnWorker);
			}
			catch {
				frameSource.Dispose();
				throw;
			}
			return frameSource;
		}

		#region IFrameSource
		public IReadOnlyList<UvcMode> Modes {
			get {
				return _listModes;
			}
		}

		public bool IsRunning {
			get {
				return _isRunning;
			}
		}

		public UvcMode ModeStarted {
			get {
				return _modeStarted;
			}
		}

		public bool IsStillPinConnected {
			get {
				return _isStillConnected;
			}
		}

		public long FramesReceived {
			get {
				return Interlocked.Read(ref _framesReceived);
			}
		}

		///<summary>Samples that could not be copied (shorter than their format says).</summary>
		public long FramesDropped {
			get {
				return Interlocked.Read(ref _framesDropped);
			}
		}

		public void Start(UvcMode mode,bool connectStillPin) {
			ThrowIfInStreamingCallback(nameof(Start));
			ThrowIfDisposed();
			_comWorker.Invoke(() => StartOnWorker(mode,connectStillPin));
		}

		public void Stop() {
			ThrowIfInStreamingCallback(nameof(Stop));
			if(_isDisposed) {
				return;
			}
			try {
				_comWorker.Invoke(StopOnWorker);
			}
			catch(ObjectDisposedException) {
			}
		}

		public UvcFrame TakeLatestFrame() {
			return _frameExchange.TakeLatest();
		}

		public UvcControlValue ReadControl(UvcControlInterface controlInterface,int property) {
			ThrowIfInStreamingCallback(nameof(ReadControl));
			if(!UvcControls.IsValidProperty(controlInterface,property)) {
				return new UvcControlValue(UvcControlStatus.Unsupported,0,0,Ds.E_PROP_ID_UNSUPPORTED);
			}
			try {
				return _comWorker.Invoke(() => ReadControlOnWorker(controlInterface,property));
			}
			catch(ObjectDisposedException) {
				return new UvcControlValue(UvcControlStatus.Closed,0,0,0);
			}
		}

		public UvcControlRange ReadControlRange(UvcControlInterface controlInterface,int property) {
			ThrowIfInStreamingCallback(nameof(ReadControlRange));
			if(!UvcControls.IsValidProperty(controlInterface,property)) {
				return new UvcControlRange(UvcControlStatus.Unsupported,0,0,0,0,0,Ds.E_PROP_ID_UNSUPPORTED);
			}
			try {
				return _comWorker.Invoke(() => ReadControlRangeOnWorker(controlInterface,property));
			}
			catch(ObjectDisposedException) {
				return new UvcControlRange(UvcControlStatus.Closed,0,0,0,0,0,0);
			}
		}

		///<summary>Stops the graph, releases every COM object on the worker thread, then ends the worker thread.</summary>
		public void Dispose() {
			ThrowIfInStreamingCallback(nameof(Dispose));
			lock(_lockDispose) {
				if(_isDisposeStarted) {
					return;
				}
				_isDisposeStarted=true;
			}
			try {
				_comWorker.Invoke(ReleaseOnWorker);
			}
			catch(ObjectDisposedException) {
			}
			_comWorker.Dispose();
		}
		#endregion IFrameSource

		#region Open
		private void OpenOnWorker() {
			IMoniker moniker=DeviceEnumerator.FindMoniker(Device);
			if(moniker==null) {
				throw new UvcException(UvcErrorKind.NoDevice,"The camera is not connected.");
			}
			try {
				_graphBuilder=(IGraphBuilder)CreateComObject(Ds.CLSID_FilterGraph,typeof(IGraphBuilder),"The filter graph");
				_mediaControl=(IMediaControl)_graphBuilder;
				_mediaEventEx=(IMediaEventEx)_graphBuilder;
				_captureGraphBuilder=(ICaptureGraphBuilder2)CreateComObject(Ds.CLSID_CaptureGraphBuilder2,typeof(ICaptureGraphBuilder2),
					"The capture graph builder");
				Check(_captureGraphBuilder.SetFiltergraph(_graphBuilder),"ICaptureGraphBuilder2::SetFiltergraph");
				Guid iidBaseFilter=typeof(IBaseFilter).GUID;
				object objFilter;
				Check(moniker.BindToObject(null,null,ref iidBaseFilter,out objFilter),"Binding the camera");
				_filterSource=objFilter as IBaseFilter;
				if(_filterSource==null) {
					DsUtil.Release(ref objFilter);
					throw new UvcException(UvcErrorKind.Failed,"The camera's filter does not implement IBaseFilter.");
				}
				Check(_graphBuilder.AddFilter(_filterSource,"Video capture source"),"IFilterGraph::AddFilter (source)");
			}
			finally {
				DsUtil.Release(ref moniker);
			}
			Guid guidStill=Ds.PIN_CATEGORY_STILL;
			Guid guidVideo=Ds.MEDIATYPE_Video;
			IPin pinStill;
			if(_captureGraphBuilder.FindPin(_filterSource,PIN_DIRECTION.PINDIR_OUTPUT,ref guidStill,ref guidVideo,false,0,out pinStill)==Ds.S_OK
				&& pinStill!=null)
			{
				_pinStill=pinStill;
				HasStillPin=true;
			}
			Guid guidCapture=Ds.PIN_CATEGORY_CAPTURE;
			IPin pinCapture;
			if(_captureGraphBuilder.FindPin(_filterSource,PIN_DIRECTION.PINDIR_OUTPUT,ref guidCapture,ref guidVideo,false,0,out pinCapture)==Ds.S_OK
				&& pinCapture!=null)
			{
				_pinCapture=pinCapture;
			}
			else {
				_pinCapture=FindFirstConfigurableOutputPin();
			}
			if(_pinCapture==null) {
				throw new UvcException(UvcErrorKind.NoCapturePin,"The camera has no video capture pin.");
			}
			_streamConfig=_pinCapture as IAMStreamConfig;
			_listModes=new ReadOnlyCollection<UvcMode>(ReadModes());
			_objCameraControl=FindUpstreamInterface(typeof(IAMCameraControl).GUID);
			_cameraControl=_objCameraControl as IAMCameraControl;
			_objVideoProcAmp=FindUpstreamInterface(typeof(IAMVideoProcAmp).GUID);
			_videoProcAmp=_objVideoProcAmp as IAMVideoProcAmp;
			IntPtr hEvent;
			if(_mediaEventEx.GetEventHandle(out hEvent)==Ds.S_OK && hEvent!=IntPtr.Zero) {
				_waitHandleGraphEvent=new GraphEventWaitHandle(hEvent);
				_comWorker.SetSignalHandler(_waitHandleGraphEvent,DrainGraphEvents);
			}
		}

		///<summary>For a source whose pins report no category: the first output pin, other than the still pin, that has
		///IAMStreamConfig. Null if none.</summary>
		private IPin FindFirstConfigurableOutputPin() {
			IEnumPins enumPins=null;
			try {
				if(_filterSource.EnumPins(out enumPins)!=Ds.S_OK || enumPins==null) {
					return null;
				}
				IPin[] arrayPins=new IPin[1];
				while(enumPins.Next(1,arrayPins,IntPtr.Zero)==Ds.S_OK) {
					IPin pin=arrayPins[0];
					arrayPins[0]=null;
					if(pin==null) {
						break;
					}
					PIN_DIRECTION pinDirection;
					bool isMatch=pin.QueryDirection(out pinDirection)==Ds.S_OK
						&& pinDirection==PIN_DIRECTION.PINDIR_OUTPUT
						&& !ReferenceEquals(pin,_pinStill)
						&& pin is IAMStreamConfig;
					if(isMatch) {
						return pin;
					}
					DsUtil.Release(ref pin);
				}
				return null;
			}
			finally {
				DsUtil.Release(ref enumPins);
			}
		}

		///<summary>An interface on the source filter or upstream of it, or null.</summary>
		private object FindUpstreamInterface(Guid iid) {
			Guid guidLookUpstream=Ds.LOOK_UPSTREAM_ONLY;
			object obj;
			if(_captureGraphBuilder.FindInterface(ref guidLookUpstream,IntPtr.Zero,_filterSource,ref iid,out obj)!=Ds.S_OK) {
				DsUtil.Release(ref obj);
				return null;
			}
			return obj;
		}

		///<summary>The capture pin's FORMAT_VideoInfo stream formats, without duplicates (the faster entry of two equal formats
		///is kept). Empty when the pin has no IAMStreamConfig.</summary>
		private List<UvcMode> ReadModes() {
			List<UvcMode> listModes=new List<UvcMode>();
			if(_streamConfig==null) {
				return listModes;
			}
			int count;
			int sizeCaps;
			if(_streamConfig.GetNumberOfCapabilities(out count,out sizeCaps)!=Ds.S_OK || count<=0) {
				return listModes;
			}
			int sizeVideoCaps=Marshal.SizeOf(typeof(VIDEO_STREAM_CONFIG_CAPS));
			IntPtr pCaps=Marshal.AllocCoTaskMem(Math.Max(sizeCaps,sizeVideoCaps));
			try {
				for(int i=0;i<count;i++) {
					IntPtr pmt;
					if(_streamConfig.GetStreamCaps(i,out pmt,pCaps)!=Ds.S_OK || pmt==IntPtr.Zero) {
						continue;
					}
					try {
						AM_MEDIA_TYPE mediaType=(AM_MEDIA_TYPE)Marshal.PtrToStructure(pmt,typeof(AM_MEDIA_TYPE));
						VIDEOINFOHEADER videoInfoHeader;
						if(mediaType.majortype!=Ds.MEDIATYPE_Video || !DsUtil.TryGetVideoInfoHeader(mediaType,out videoInfoHeader)) {
							continue;
						}
						long frameIntervalCaps=0;
						if(sizeCaps>=sizeVideoCaps) {
							VIDEO_STREAM_CONFIG_CAPS caps=(VIDEO_STREAM_CONFIG_CAPS)Marshal.PtrToStructure(pCaps,typeof(VIDEO_STREAM_CONFIG_CAPS));
							frameIntervalCaps=caps.MinFrameInterval;
						}
						UvcMode mode=CreateMode(i,mediaType.subtype,videoInfoHeader,frameIntervalCaps);
						if(mode==null) {
							continue;
						}
						int indexSame=listModes.FindIndex(x => x.IsSameFormat(mode));
						if(indexSame<0) {
							listModes.Add(mode);
						}
						else if(mode.MaxFramesPerSecond>listModes[indexSame].MaxFramesPerSecond) {
							listModes[indexSame]=mode;
						}
					}
					finally {
						DsUtil.DeleteMediaType(pmt);
					}
				}
			}
			finally {
				Marshal.FreeCoTaskMem(pCaps);
			}
			return listModes;
		}

		///<summary>A mode from one stream-caps entry, or null for an empty size. The frame interval is the caps' MinFrameInterval
		///(the fastest the format allows) when set, otherwise the format's AvgTimePerFrame. Height is positive whatever the row
		///order.</summary>
		internal static UvcMode CreateMode(int index,Guid subtype,VIDEOINFOHEADER videoInfoHeader,long frameIntervalCaps) {
			int width=videoInfoHeader.bmiHeader.biWidth;
			int height=Math.Abs(videoInfoHeader.bmiHeader.biHeight);
			if(width<=0 || height<=0) {
				return null;
			}
			long frameInterval=frameIntervalCaps>0 ? frameIntervalCaps : videoInfoHeader.AvgTimePerFrame;
			return new UvcMode(index,width,height,subtype,videoInfoHeader.bmiHeader.biBitCount,frameInterval);
		}
		#endregion Open

		#region Start and stop
		private void StartOnWorker(UvcMode mode,bool connectStillPin) {
			if(_isDisposed) {
				throw new ObjectDisposedException(nameof(DirectShowFrameSource));
			}
			if(_isRenderFailed) {
				throw new InvalidOperationException("Starting this camera failed earlier. Dispose it and open the device again.");
			}
			if(_isGraphRendered) {
				if(!IsSameMode(mode,_modeStarted) || connectStillPin!=_isStillRequested) {
					throw new InvalidOperationException("The mode and still choice are fixed by the first Start. Open the device again to change them.");
				}
				RunGraph();
				return;
			}
			try {
				if(mode!=null) {
					SetFormat(mode);
				}
				RenderPreview();
				if(connectStillPin && _pinStill!=null) {
					TryRenderStill();
				}
			}
			catch {
				_isRenderFailed=true;
				throw;
			}
			_isGraphRendered=true;
			_modeStarted=mode;
			_isStillRequested=connectStillPin;
			RunGraph();
		}

		private static bool IsSameMode(UvcMode mode1,UvcMode mode2) {
			if(mode1==null || mode2==null) {
				return mode1==mode2;
			}
			return mode1.Index==mode2.Index && mode1.IsSameFormat(mode2);
		}

		///<summary>IAMStreamConfig::SetFormat with the mode's stream-caps media type and its fastest frame interval. The one write
		///this layer makes to a camera (design 8.7). Called once, before the capture pin is connected.</summary>
		private void SetFormat(UvcMode mode) {
			if(_streamConfig==null) {
				throw new UvcException(UvcErrorKind.Failed,"The camera's capture pin does not allow choosing a format.");
			}
			int count;
			int sizeCaps;
			Check(_streamConfig.GetNumberOfCapabilities(out count,out sizeCaps),"IAMStreamConfig::GetNumberOfCapabilities");
			if(mode.Index<0 || mode.Index>=count) {
				throw new UvcException(UvcErrorKind.Failed,"The camera no longer offers "+mode+".");
			}
			IntPtr pCaps=Marshal.AllocCoTaskMem(Math.Max(sizeCaps,Marshal.SizeOf(typeof(VIDEO_STREAM_CONFIG_CAPS))));
			IntPtr pmt=IntPtr.Zero;
			try {
				Check(_streamConfig.GetStreamCaps(mode.Index,out pmt,pCaps),"IAMStreamConfig::GetStreamCaps");
				AM_MEDIA_TYPE mediaType=(AM_MEDIA_TYPE)Marshal.PtrToStructure(pmt,typeof(AM_MEDIA_TYPE));
				VIDEOINFOHEADER videoInfoHeader;
				if(!DsUtil.TryGetVideoInfoHeader(mediaType,out videoInfoHeader)
					|| mediaType.subtype!=mode.Subtype
					|| videoInfoHeader.bmiHeader.biWidth!=mode.Width
					|| Math.Abs(videoInfoHeader.bmiHeader.biHeight)!=mode.Height)
				{
					throw new UvcException(UvcErrorKind.Failed,"The camera no longer offers "+mode+".");
				}
				if(mode.FrameIntervalMin>0) {
					Marshal.WriteInt64(mediaType.pbFormat,Marshal.OffsetOf(typeof(VIDEOINFOHEADER),"AvgTimePerFrame").ToInt32(),mode.FrameIntervalMin);
				}
				Check(_streamConfig.SetFormat(pmt),"IAMStreamConfig::SetFormat ("+mode+")");
			}
			finally {
				DsUtil.DeleteMediaType(pmt);
				Marshal.FreeCoTaskMem(pCaps);
			}
		}

		private void RenderPreview() {
			_grabberCallbackPreview=new GrabberCallback(this,false);
			CreateGrabberBranch(_grabberCallbackPreview,"Preview",out _filterGrabber,out _sampleGrabber,out _filterNullRenderer);
			Check(_captureGraphBuilder.RenderStream(IntPtr.Zero,IntPtr.Zero,_pinCapture,_filterGrabber,_filterNullRenderer),
				"Connecting the camera's capture pin");
			_geometryPreview=ReadGeometry(_sampleGrabber);
		}

		///<summary>Renders the still pin to its own grabber. On failure, removes what it added and records why: the preview does
		///not depend on the still pin.</summary>
		private void TryRenderStill() {
			try {
				_grabberCallbackStill=new GrabberCallback(this,true);
				CreateGrabberBranch(_grabberCallbackStill,"Still",out _filterGrabberStill,out _sampleGrabberStill,out _filterNullRendererStill);
				Check(_captureGraphBuilder.RenderStream(IntPtr.Zero,IntPtr.Zero,_pinStill,_filterGrabberStill,_filterNullRendererStill),
					"Connecting the camera's still pin");
				_geometryStill=ReadGeometry(_sampleGrabberStill);
				_isStillConnected=true;
			}
			catch(Exception ex) {
				StillPinError=ex.Message;
				_isStillConnected=false;
				DisconnectPin(_pinStill);
				if(_sampleGrabberStill!=null) {
					_sampleGrabberStill.SetCallback(null,Ds.SampleGrabberCallBufferCB);
				}
				if(_filterGrabberStill!=null) {
					_graphBuilder.RemoveFilter(_filterGrabberStill);
				}
				if(_filterNullRendererStill!=null) {
					_graphBuilder.RemoveFilter(_filterNullRendererStill);
				}
				_sampleGrabberStill=null;
				DsUtil.Release(ref _filterGrabberStill);
				DsUtil.Release(ref _filterNullRendererStill);
				_grabberCallbackStill=null;
			}
		}

		///<summary>Disconnects a pin and its peer, if connected.</summary>
		private void DisconnectPin(IPin pin) {
			IPin pinPeer;
			if(pin.ConnectedTo(out pinPeer)!=Ds.S_OK || pinPeer==null) {
				return;
			}
			try {
				_graphBuilder.Disconnect(pinPeer);
				_graphBuilder.Disconnect(pin);
			}
			finally {
				DsUtil.Release(ref pinPeer);
			}
		}

		///<summary>A Sample Grabber that accepts only RGB32 video and calls BufferCB, and a Null Renderer, both added to the graph.</summary>
		private void CreateGrabberBranch(GrabberCallback grabberCallback,string name,out IBaseFilter filterGrabber,
			out ISampleGrabber sampleGrabber,out IBaseFilter filterNullRenderer)
		{
			filterGrabber=null;
			sampleGrabber=null;
			filterNullRenderer=null;
			filterGrabber=(IBaseFilter)CreateComObject(Ds.CLSID_SampleGrabber,typeof(IBaseFilter),"The Sample Grabber (qedit.dll)");
			sampleGrabber=filterGrabber as ISampleGrabber;
			if(sampleGrabber==null) {
				throw new UvcException(UvcErrorKind.NotAvailable,"The Sample Grabber does not implement ISampleGrabber.");
			}
			AM_MEDIA_TYPE mediaType=new AM_MEDIA_TYPE();
			mediaType.majortype=Ds.MEDIATYPE_Video;
			mediaType.subtype=Ds.MEDIASUBTYPE_RGB32;
			Check(sampleGrabber.SetMediaType(ref mediaType),"ISampleGrabber::SetMediaType");
			Check(sampleGrabber.SetBufferSamples(false),"ISampleGrabber::SetBufferSamples");
			Check(sampleGrabber.SetOneShot(false),"ISampleGrabber::SetOneShot");
			Check(sampleGrabber.SetCallback(grabberCallback,Ds.SampleGrabberCallBufferCB),"ISampleGrabber::SetCallback");
			Check(_graphBuilder.AddFilter(filterGrabber,name+" grabber"),"IFilterGraph::AddFilter ("+name+" grabber)");
			filterNullRenderer=(IBaseFilter)CreateComObject(Ds.CLSID_NullRenderer,typeof(IBaseFilter),"The Null Renderer (qedit.dll)");
			Check(_graphBuilder.AddFilter(filterNullRenderer,name+" null renderer"),"IFilterGraph::AddFilter ("+name+" null renderer)");
		}

		///<summary>The geometry of the RGB format the grabber connected with.</summary>
		private static FrameGeometry ReadGeometry(ISampleGrabber sampleGrabber) {
			AM_MEDIA_TYPE mediaType=new AM_MEDIA_TYPE();
			Check(sampleGrabber.GetConnectedMediaType(ref mediaType),"ISampleGrabber::GetConnectedMediaType");
			try {
				VIDEOINFOHEADER videoInfoHeader;
				if(!DsUtil.TryGetVideoInfoHeader(mediaType,out videoInfoHeader)) {
					throw new UvcException(UvcErrorKind.Failed,"The grabber connected with a format other than VIDEOINFOHEADER.");
				}
				FrameGeometry geometry;
				string error;
				if(!FrameGeometry.TryCreate(videoInfoHeader,out geometry,out error)) {
					throw new UvcException(UvcErrorKind.Failed,"The grabber connected with an unusable format. "+error);
				}
				return geometry;
			}
			finally {
				DsUtil.FreeMediaType(ref mediaType);
			}
		}

		private void RunGraph() {
			if(_isRunning) {
				return;
			}
			int hr=_mediaControl.Run();
			if(hr==Ds.S_FALSE) {
				int state;
				hr=_mediaControl.GetState(RunTransitionTimeoutMs,out state);
				//VFW_S_STATE_INTERMEDIATE (not finished yet) and VFW_S_CANT_CUE are success codes; streaming starts when it finishes.
			}
			if(hr<0) {
				_mediaControl.Stop();
				UvcErrorKind kind=UvcErrors.Classify(hr);
				throw new UvcException(kind==UvcErrorKind.None ? UvcErrorKind.Failed : kind,
					"Starting the camera failed ("+UvcErrors.FormatHResult(hr)+").",hr);
			}
			_isRunning=true;
		}

		private void StopOnWorker() {
			if(_isDisposed || !_isRunning || _mediaControl==null) {
				return;
			}
			_isStopping=true;
			try {
				_mediaControl.Stop();
			}
			finally {
				_isRunning=false;
				_isStopping=false;
			}
		}
		#endregion Start and stop

		#region Controls
		private UvcControlValue ReadControlOnWorker(UvcControlInterface controlInterface,int property) {
			if(_isDisposed) {
				return new UvcControlValue(UvcControlStatus.Closed,0,0,0);
			}
			int value;
			int flags;
			int hr;
			if(controlInterface==UvcControlInterface.CameraControl) {
				if(_cameraControl==null) {
					return new UvcControlValue(UvcControlStatus.NoInterface,0,0,Ds.E_NOINTERFACE);
				}
				hr=_cameraControl.Get(property,out value,out flags);
			}
			else {
				if(_videoProcAmp==null) {
					return new UvcControlValue(UvcControlStatus.NoInterface,0,0,Ds.E_NOINTERFACE);
				}
				hr=_videoProcAmp.Get(property,out value,out flags);
			}
			UvcControlStatus status=UvcControls.GetStatusFromHResult(hr);
			if(status!=UvcControlStatus.Ok) {
				return new UvcControlValue(status,0,0,hr);
			}
			return new UvcControlValue(status,value,flags,hr);
		}

		private UvcControlRange ReadControlRangeOnWorker(UvcControlInterface controlInterface,int property) {
			if(_isDisposed) {
				return new UvcControlRange(UvcControlStatus.Closed,0,0,0,0,0,0);
			}
			int min;
			int max;
			int step;
			int defaultValue;
			int capsFlags;
			int hr;
			if(controlInterface==UvcControlInterface.CameraControl) {
				if(_cameraControl==null) {
					return new UvcControlRange(UvcControlStatus.NoInterface,0,0,0,0,0,Ds.E_NOINTERFACE);
				}
				hr=_cameraControl.GetRange(property,out min,out max,out step,out defaultValue,out capsFlags);
			}
			else {
				if(_videoProcAmp==null) {
					return new UvcControlRange(UvcControlStatus.NoInterface,0,0,0,0,0,Ds.E_NOINTERFACE);
				}
				hr=_videoProcAmp.GetRange(property,out min,out max,out step,out defaultValue,out capsFlags);
			}
			UvcControlStatus status=UvcControls.GetStatusFromHResult(hr);
			if(status!=UvcControlStatus.Ok) {
				return new UvcControlRange(status,0,0,0,0,0,hr);
			}
			return new UvcControlRange(status,min,max,step,defaultValue,capsFlags,hr);
		}
		#endregion Controls

		#region Graph events
		///<summary>Worker thread, when the graph's event handle is signaled. Each event's parameters are freed before it is
		///handled. Errors are raised on a thread-pool thread, so no handler runs on the worker thread.</summary>
		private void DrainGraphEvents() {
			for(int i=0;i<64;i++) {
				IMediaEventEx mediaEventEx=_mediaEventEx;
				if(_isDisposed || mediaEventEx==null) {
					return;
				}
				int eventCode;
				IntPtr param1;
				IntPtr param2;
				int hr=mediaEventEx.GetEvent(out eventCode,out param1,out param2,0);
				if(hr!=Ds.S_OK) {
					if(hr!=Ds.E_ABORT) {
						//E_ABORT means the queue is empty. Anything else would leave the handle signaled: stop waiting on it.
						_comWorker.SetSignalHandler(null,null);
					}
					return;
				}
				int value1=param1.ToInt32();
				int value2=param2.ToInt32();
				mediaEventEx.FreeEventParams(eventCode,param1,param2);
				if(eventCode==Ds.EC_DEVICE_LOST && value2==0) {
					RaiseErrorAsync(UvcErrorKind.DeviceLost,0,"The camera was removed (EC_DEVICE_LOST).");
				}
				else if(eventCode==Ds.EC_ERRORABORT) {
					UvcErrorKind kind=UvcErrors.Classify(value1);
					if(kind==UvcErrorKind.None || kind==UvcErrorKind.Failed) {
						kind=UvcErrorKind.ErrorAbort;
					}
					RaiseErrorAsync(kind,value1,"Streaming stopped with an error (EC_ERRORABORT, "+UvcErrors.FormatHResult(value1)+").");
				}
			}
		}

		///<summary>Waits on the graph's event handle without owning it: the graph closes it when it is released.</summary>
		private sealed class GraphEventWaitHandle:WaitHandle {
			public GraphEventWaitHandle(IntPtr hEvent) {
				SafeWaitHandle=new SafeWaitHandle(hEvent,false);
			}
		}

		private void RaiseErrorAsync(UvcErrorKind kind,int hResult,string message) {
			UvcErrorEventArgs args=new UvcErrorEventArgs(kind,hResult,message);
			ThreadPool.QueueUserWorkItem(state => {
				try {
					Error?.Invoke(this,args);
				}
				catch(Exception) {
					//A handler's exception must not end the process from a thread-pool thread.
				}
			});
		}
		#endregion Graph events

		#region Streaming callbacks
		///<summary>Streaming thread. Copies the sample and raises FrameReady or StillCaptured. Touches no COM object and never
		///throws into the graph.</summary>
		private int OnBuffer(bool isStill,double sampleTime,IntPtr pBuffer,int bufferLength) {
			if(_isDisposed || _isStopping) {
				return Ds.S_OK;
			}
			_isInStreamingCallback=true;
			try {
				if(isStill) {
					FrameGeometry geometry=_geometryStill;
					UvcFrame frame=new UvcFrame(geometry.Width,geometry.Height);
					if(!FrameCopier.Copy(pBuffer,bufferLength,geometry,frame)) {
						Interlocked.Increment(ref _framesDropped);
						return Ds.S_OK;
					}
					frame.Sequence=Interlocked.Increment(ref _sequenceStill);
					frame.SampleTime=sampleTime;
					StillCaptured?.Invoke(this,new UvcStillEventArgs(frame));
				}
				else {
					FrameGeometry geometry=_geometryPreview;
					UvcFrame frame=_frameExchange.GetBackBuffer(geometry.Width,geometry.Height);
					if(!FrameCopier.Copy(pBuffer,bufferLength,geometry,frame)) {
						Interlocked.Increment(ref _framesDropped);
						return Ds.S_OK;
					}
					_frameExchange.Publish(sampleTime);
					Interlocked.Increment(ref _framesReceived);
					FrameReady?.Invoke(this,EventArgs.Empty);
				}
			}
			catch(Exception) {
				//A handler's exception must not reach the streaming thread.
			}
			finally {
				_isInStreamingCallback=false;
			}
			return Ds.S_OK;
		}

		///<summary>ISampleGrabberCB for one grabber. Holds the source only while the grabber holds the callback.</summary>
		[ComVisible(true)]
		private sealed class GrabberCallback:ISampleGrabberCB {
			private readonly DirectShowFrameSource _frameSource;
			private readonly bool _isStill;

			public GrabberCallback(DirectShowFrameSource frameSource,bool isStill) {
				_frameSource=frameSource;
				_isStill=isStill;
			}

			public int SampleCB(double SampleTime,IntPtr pSample) {
				return Ds.S_OK;
			}

			public int BufferCB(double SampleTime,IntPtr pBuffer,int BufferLen) {
				return _frameSource.OnBuffer(_isStill,SampleTime,pBuffer,BufferLen);
			}
		}
		#endregion Streaming callbacks

		#region Release
		///<summary>Worker thread. Stops the graph, detaches the callbacks and releases every COM object exactly once.</summary>
		private void ReleaseOnWorker() {
			_isDisposed=true;
			_comWorker.SetSignalHandler(null,null);
			if(_mediaControl!=null) {
				_isStopping=true;
				_mediaControl.Stop();
				_isRunning=false;
			}
			if(_sampleGrabber!=null) {
				_sampleGrabber.SetCallback(null,Ds.SampleGrabberCallBufferCB);
			}
			if(_sampleGrabberStill!=null) {
				_sampleGrabberStill.SetCallback(null,Ds.SampleGrabberCallBufferCB);
			}
			_grabberCallbackPreview=null;
			_grabberCallbackStill=null;
			if(_waitHandleGraphEvent!=null) {
				_waitHandleGraphEvent.Dispose();//does not close the graph's handle
				_waitHandleGraphEvent=null;
			}
			_cameraControl=null;
			DsUtil.Release(ref _objCameraControl);
			_videoProcAmp=null;
			DsUtil.Release(ref _objVideoProcAmp);
			_streamConfig=null;
			DsUtil.Release(ref _pinStill);
			DsUtil.Release(ref _pinCapture);
			_sampleGrabberStill=null;
			DsUtil.Release(ref _filterGrabberStill);
			DsUtil.Release(ref _filterNullRendererStill);
			_sampleGrabber=null;
			DsUtil.Release(ref _filterGrabber);
			DsUtil.Release(ref _filterNullRenderer);
			DsUtil.Release(ref _filterSource);
			DsUtil.Release(ref _captureGraphBuilder);
			_mediaControl=null;
			_mediaEventEx=null;
			DsUtil.Release(ref _graphBuilder);
		}
		#endregion Release

		#region Helpers
		///<summary>Creates a COM object and checks it implements the interface the caller casts to. Throws UvcException.</summary>
		private static object CreateComObject(Guid clsid,Type typeInterface,string description) {
			object obj;
			try {
				obj=DsUtil.CreateComObject(clsid);
			}
			catch(COMException ex) {
				UvcErrorKind kind=ex.ErrorCode==Ds.REGDB_E_CLASSNOTREG ? UvcErrorKind.NotAvailable : UvcErrorKind.Failed;
				throw new UvcException(kind,description+" is not available ("+UvcErrors.FormatHResult(ex.ErrorCode)+").",ex.ErrorCode);
			}
			if(!typeInterface.IsInstanceOfType(obj)) {
				DsUtil.Release(ref obj);
				throw new UvcException(UvcErrorKind.NotAvailable,description+" does not implement "+typeInterface.Name+".");
			}
			return obj;
		}

		///<summary>Throws UvcException for a failed HRESULT, with a kind from UvcErrors.Classify (Failed when unrecognized).</summary>
		private static void Check(int hr,string operation) {
			if(hr>=0) {
				return;
			}
			UvcErrorKind kind=UvcErrors.Classify(hr);
			throw new UvcException(kind==UvcErrorKind.None ? UvcErrorKind.Failed : kind,operation+" failed ("+UvcErrors.FormatHResult(hr)+").",hr);
		}

		private void ThrowIfDisposed() {
			if(_isDisposeStarted) {
				throw new ObjectDisposedException(nameof(DirectShowFrameSource));
			}
		}

		private static void ThrowIfInStreamingCallback(string member) {
			if(_isInStreamingCallback) {
				throw new InvalidOperationException(member+" cannot be called from a FrameReady or StillCaptured handler. Use BeginInvoke.");
			}
		}
		#endregion Helpers
	}
}
