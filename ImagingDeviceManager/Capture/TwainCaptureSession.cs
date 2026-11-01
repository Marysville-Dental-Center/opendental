// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using ImagingDeviceManager.Journal;
using ImagingDeviceManager.TwainProtocol;

namespace ImagingDeviceManager.Capture {
	///<summary>The capture session's TWAIN side (design 7.1 CAP-3, 7.3, 7.4, 7.6): one source opened by ProductName and kept in state 4
	///across slots, driven on a dedicated STA worker thread. See ITwainCaptureSession for the threading contract.
	///
	///Per enable (PerSlot): CAP_XFERCOUNT=1, then the source's notification. MSG_XFERREADY: every image is transferred (DAT_IMAGEINFO per
	///image, the raw 16-bit memory path, DAT_EXTIMAGEINFO in state 7), journaled and raised; at Count=0 the source is disabled and Ended
	///reports Completed. MSG_CLOSEDSREQ: DAT_STATUS (and DAT_STATUSUTF8) is read before the disable, and Ended reports the code and text.
	///The session stays in state 5 after Count=0 only with SessionMode=Continuous and the device profile's ContinuousCap accepted. Every stop
	///it starts uses the safe order of CAP-1, with the bounded wait after a refused disable.</summary>
	public sealed class TwainCaptureSession:ITwainCaptureSession {
		#region Fields
		///<summary>Every instance of this process whose worker has not ended, with the ProductName it was opened for: two workers must never
		///drive one source at once (each loads its own DSM identity, and most sources accept one MSG_OPENDS, while the other may still be
		///stopping it). Taken before an instance's _lock, never after it.</summary>
		private static readonly object _lockLiveSessions=new object();
		private static readonly List<TwainCaptureSession> _listLiveSessions=new List<TwainCaptureSession>();
		private readonly DsmLoader _dsmLoader;
		private readonly CaptureFolders _folders;
		private readonly Func<DateTime> _funcNow;
		private readonly Stopwatch _stopwatch=Stopwatch.StartNew();
		private readonly object _lock=new object();
		///<summary>Held by the worker while it decides to raise ImageAcquired and raises it, and by BeginClose while it sets _isClosing (before
		///_lock, never after it), so that no ImageAcquired is raised once BeginClose has returned. Enable, Disable and the other commands do
		///not take it, so an ImageAcquired handler may still call them, or wait for another thread that does.</summary>
		private readonly object _lockRaiseImage=new object();
		private readonly LinkedList<Command> _listCommands=new LinkedList<Command>();
		private readonly AutoResetEvent _eventCommand=new AutoResetEvent(false);
		private readonly ManualResetEvent _eventClosed=new ManualResetEvent(true);
		private readonly CaptureLog _captureLog;
		private readonly CaptureJournal _journal;
		private readonly QuarantineWriter _quarantine;
		private Thread _thread;
		///<summary>The proxy window of the open source, which the commands post to so that the message loop wakes up.</summary>
		private volatile IntPtr _hwndWake;
		private volatile bool _isClosing;
		private volatile int _state=(int)TwainCaptureState.Closed;
		private int _enableIdLast;
		private TwainCaptureOptions _options=new TwainCaptureOptions();
		private TwainCaptureQuarantineContext _quarantineContext=new TwainCaptureQuarantineContext();
		private volatile string _productName="";
		///<summary>The ProductName Open was called with, trimmed, while this instance is in _listLiveSessions. Guarded by _lockLiveSessions.</summary>
		private string _productNameLive="";
		private volatile TwainCaptureSourceInfo _sourceInfo;
		//Worker thread only:
		private TwainSession _session;
		private EnableContext _enable;
		///<summary>Why the enable is being stopped, while StopCapture runs: the tag of images drained during the stop.</summary>
		private TwainCaptureStopReason _stopReasonCurrent;
		///<summary>RedeliverCap was set TRUE and has not been set FALSE again.</summary>
		private bool _isRedeliverMaybeSet;
		#endregion Fields

		#region Construction and properties
		///<summary>A session on the computer's TWAIN DSM, with the capture folders under %ProgramData%\OpenPMS-Dental\Imaging.</summary>
		public TwainCaptureSession():this(LoadNativeDsm,CaptureFolders.CreateDefault(),() => DateTime.Now) {
		}

		///<summary>For tests: a simulated DSM, other capture folders and a clock.</summary>
		internal TwainCaptureSession(DsmLoader dsmLoader,CaptureFolders folders,Func<DateTime> funcNow) {
			_dsmLoader=dsmLoader;
			_folders=folders;
			_funcNow=funcNow??(() => DateTime.Now);
			_captureLog=new CaptureLog(_folders,_funcNow);
			_journal=new CaptureJournal(_folders);
			_quarantine=new QuarantineWriter(_folders);
			SessionGuid=Guid.NewGuid();
		}

		private static ITwainDsm LoadNativeDsm(out string errorText) {
			return NativeTwainDsm.Load(out errorText);
		}

		public TwainCaptureState State => (TwainCaptureState)_state;
		public Guid SessionGuid { get; set; }
		public string ProductName => _productName;
		public TwainCaptureOptions Options {
			get {
				lock(_lock) {
					return _options;
				}
			}
		}
		public TwainCaptureSourceInfo SourceInfo => _sourceInfo;

		public TwainCaptureQuarantineContext QuarantineContext {
			get {
				lock(_lock) {
					return _quarantineContext;
				}
			}
			set {
				lock(_lock) {
					_quarantineContext=value??new TwainCaptureQuarantineContext();
				}
			}
		}

		public Func<string,CaptureMetadataKeys> FuncReadMetadataKeys { get; set; }

		public event Action<AcquiredImage> ImageAcquired;
		public event Action<TwainCaptureEnded> Ended;
		public event Action<TwainCaptureStatus> StatusChanged;
		public event Action<AcquiredImage> ImageQuarantined;
		public event Action<string> Logged;

		private bool IsWorkerThread => _thread!=null && Thread.CurrentThread==_thread;

		private long NowMs => _stopwatch.ElapsedMilliseconds;
		#endregion Construction and properties

		#region Commands (any thread)
		private enum CommandKind {
			Open,
			Enable,
			Redeliver,
			Disable,
			Close,
		}

		private sealed class Command {
			public CommandKind Kind;
			public int EnableId;
			public TwainEnableRequest Request;
			public TwainCaptureStopReason StopReason;
			public string ProductName;
			public TwainCaptureOpenResult OpenResult;
			public ManualResetEvent EventDone;
		}

		public TwainCaptureOpenResult Open(string productName,TwainCaptureOptions options) {
			if(IsWorkerThread) {
				throw new InvalidOperationException("TwainCaptureSession.Open cannot be called from one of its event handlers.");
			}
			Thread threadOld=_thread;
			if(threadOld!=null && threadOld.IsAlive && State==TwainCaptureState.Closed) {
				threadOld.Join(5000);//A worker that is just ending.
			}
			lock(_lock) {
				if(State!=TwainCaptureState.Closed || (_thread!=null && _thread.IsAlive)) {
					return new TwainCaptureOpenResult { Error=TwainCaptureOpenError.AlreadyOpen,ErrorText="The capture session is already open." };
				}
			}
			string productNameLive=(productName??"").Trim();
			string errorBusy;
			if(!WaitForOtherSessions(productNameLive,GetOpenTimeout(options),out errorBusy)) {
				Log(errorBusy);
				return new TwainCaptureOpenResult { Error=TwainCaptureOpenError.SourceBusy,ErrorText=errorBusy };
			}
			Command command;
			lock(_lockLiveSessions) {
				if(FindOtherLiveSession(productNameLive)!=null) {
					errorBusy="Another capture session of this program has just opened the TWAIN source \""+productNameLive+"\".";
					Log(errorBusy);
					return new TwainCaptureOpenResult { Error=TwainCaptureOpenError.SourceBusy,ErrorText=errorBusy };
				}
				lock(_lock) {
					if(State!=TwainCaptureState.Closed || (_thread!=null && _thread.IsAlive)) {
						return new TwainCaptureOpenResult { Error=TwainCaptureOpenError.AlreadyOpen,ErrorText="The capture session is already open." };
					}
					_options=options==null ? new TwainCaptureOptions() : options.Clone();
					_isClosing=false;
					_hwndWake=IntPtr.Zero;
					_sourceInfo=null;
					_productName="";
					_listCommands.Clear();
					_state=(int)TwainCaptureState.Opening;
					_eventClosed.Reset();
					command=new Command { Kind=CommandKind.Open,ProductName=productName??"",EventDone=new ManualResetEvent(false) };
					_listCommands.AddLast(command);
					_thread=new Thread(WorkerMain) { IsBackground=true,Name="TWAIN capture "+SessionGuid.ToString("N").Substring(0,8) };
					_thread.SetApartmentState(ApartmentState.STA);
					_productNameLive=productNameLive;
					_listLiveSessions.Add(this);
				}
			}
			_thread.Start();
			_eventCommand.Set();
			TimeSpan timeout=GetOpenTimeout(_options);
			if(!command.EventDone.WaitOne(timeout)) {
				Log("The source did not finish opening within "+timeout.TotalSeconds.ToString("0",CultureInfo.InvariantCulture)
					+" s; the session closes when it does.");
				BeginClose();
				return new TwainCaptureOpenResult {
					Error=TwainCaptureOpenError.Timeout,
					ErrorText="The TWAIN source \""+productName+"\" did not answer.",
				};
			}
			if(command.OpenResult==null || !command.OpenResult.IsOpen) {
				WaitForClose(TimeSpan.FromSeconds(10));//The worker ends after a failed open; State is Closed afterwards.
			}
			return command.OpenResult;
		}

		private static TimeSpan GetOpenTimeout(TwainCaptureOptions options) {
			return options!=null && options.OpenTimeout>TimeSpan.Zero ? options.OpenTimeout : TimeSpan.FromSeconds(60);
		}

		///<summary>Another instance of this process whose worker drives productName (ignoring case), or null. The caller holds
		///_lockLiveSessions.</summary>
		private TwainCaptureSession FindOtherLiveSession(string productName) {
			foreach(TwainCaptureSession session in _listLiveSessions) {
				if(session!=this && string.Equals(session._productNameLive,productName,StringComparison.OrdinalIgnoreCase)) {
					return session;
				}
			}
			return null;
		}

		///<summary>Before an Open: waits, at most timeout, while another instance of this process is closing productName (after a timed-out
		///Open, or a window closed while MSG_DISABLEDS blocks), so that its MSG_CLOSEDS comes before this MSG_OPENDS. False with errorText when
		///another instance has the source open and is not closing, or did not finish closing in time.</summary>
		private bool WaitForOtherSessions(string productName,TimeSpan timeout,out string errorText) {
			errorText="";
			Stopwatch stopwatch=Stopwatch.StartNew();
			while(true) {
				TwainCaptureSession sessionOther;
				lock(_lockLiveSessions) {
					sessionOther=FindOtherLiveSession(productName);
				}
				if(sessionOther==null) {
					return true;
				}
				if(!sessionOther._isClosing) {
					errorText="The TWAIN source \""+productName+"\" is open in another capture window of this program. Close that window first.";
					return false;
				}
				TimeSpan timeLeft=timeout-stopwatch.Elapsed;
				if(timeLeft<=TimeSpan.Zero || !sessionOther._eventClosed.WaitOne(timeLeft)) {
					errorText="The previous capture session of the TWAIN source \""+productName+"\" is still closing. Try again in a moment.";
					return false;
				}
			}
		}

		public int Enable(TwainEnableRequest request) {
			return PostEnable(CommandKind.Enable,request);
		}

		public int RequestRedelivery(TwainEnableRequest request) {
			return PostEnable(CommandKind.Redeliver,request);
		}

		private int PostEnable(CommandKind kind,TwainEnableRequest request) {
			lock(_lock) {
				if(!IsWorkerRunning() || _isClosing) {
					return 0;
				}
				int enableId=++_enableIdLast;
				_listCommands.AddLast(new Command { Kind=kind,EnableId=enableId,Request=request??new TwainEnableRequest() });
				Wake();
				return enableId;
			}
		}

		public void Disable(TwainCaptureStopReason reason) {
			lock(_lock) {
				if(!IsWorkerRunning()) {
					return;
				}
				_listCommands.AddLast(new Command { Kind=CommandKind.Disable,StopReason=reason==TwainCaptureStopReason.None ? TwainCaptureStopReason.Other : reason });
				Wake();
			}
		}

		public void Cancel() {
			Disable(TwainCaptureStopReason.Cancel);
		}

		public void BeginClose() {
			lock(_lockRaiseImage) {//Waits for an ImageAcquired that is being raised; none is raised after this.
				lock(_lock) {
					if(!IsWorkerRunning()) {
						return;
					}
					if(!_isClosing) {
						_isClosing=true;//At once: images drained from now on go to the quarantine.
						_listCommands.AddLast(new Command { Kind=CommandKind.Close });
					}
					Wake();
				}
			}
		}

		public bool WaitForClose(TimeSpan timeout) {
			return _eventClosed.WaitOne(timeout);
		}

		public void Dispose() {
			BeginClose();
		}

		private bool IsWorkerRunning() {
			return _thread!=null && _thread.IsAlive && !_eventClosed.WaitOne(0);
		}

		///<summary>Wakes the worker: the event for its idle wait, and a message to the proxy window for the message loop of an enable.</summary>
		private void Wake() {
			_eventCommand.Set();
			IntPtr hwnd=_hwndWake;
			if(hwnd!=IntPtr.Zero) {
				NativeMethods.PostMessage(hwnd,TwainSession.WM_APP_WAKE,IntPtr.Zero,IntPtr.Zero);
			}
		}
		#endregion Commands (any thread)

		#region Worker
		private void WorkerMain() {
			try {
				while(true) {
					Command command=WaitForCommand();
					if(command==null) {
						continue;
					}
					if(!Execute(command)) {
						break;
					}
				}
			}
			catch(Exception ex) {
				Log("The capture worker failed: "+ex);
			}
			finally {
				ShutDownWorker();
			}
		}

		///<summary>The next command, waiting for it while this thread's messages are dispatched (the source's windows, if it has any on this
		///thread, keep working in state 4).</summary>
		private Command WaitForCommand() {
			IntPtr pMsg=Marshal.AllocHGlobal(NativeMethods.MsgBufferSize);
			try {
				IntPtr[] arrayHandles={ _eventCommand.SafeWaitHandle.DangerousGetHandle() };
				while(true) {
					lock(_lock) {
						if(_listCommands.Count>0) {
							Command command=_listCommands.First.Value;
							_listCommands.RemoveFirst();
							return command;
						}
					}
					uint tickMs=(uint)Math.Max(10,_options.TickMilliseconds);
					NativeMethods.MsgWaitForMultipleObjectsEx(1,arrayHandles,tickMs,NativeMethods.QS_ALLINPUT,NativeMethods.MWMO_INPUTAVAILABLE);
					while(NativeMethods.PeekMessage(pMsg,IntPtr.Zero,0,0,NativeMethods.PM_REMOVE)) {
						if(Marshal.ReadInt32(pMsg,NativeMethods.MsgOffsetMessage)==NativeMethods.WM_QUIT) {
							Log("WM_QUIT reached the capture worker; the session closes.");
							BeginClose();
							continue;
						}
						NativeMethods.TranslateMessage(pMsg);
						NativeMethods.DispatchMessage(pMsg);
					}
				}
			}
			finally {
				Marshal.FreeHGlobal(pMsg);
			}
		}

		///<summary>Runs a command. False ends the worker.</summary>
		private bool Execute(Command command) {
			switch(command.Kind) {
				case CommandKind.Open:
					return DoOpen(command);
				case CommandKind.Enable:
				case CommandKind.Redeliver:
					DoEnable(command);
					return true;
				case CommandKind.Disable:
					Log("Disable ("+command.StopReason+") with nothing enabled; ignored.");
					return true;
				default:
					return false;//Close: ShutDownWorker closes the source.
			}
		}

		///<summary>Closes the source and the DSM, answers every command still queued, and marks the session closed. Never throws.</summary>
		private void ShutDownWorker() {
			_isClosing=true;
			try {
				if(_session!=null) {
					SetState(TwainCaptureState.Closing,0);
					if(_session.State>=5) {
						_stopReasonCurrent=TwainCaptureStopReason.Close;
						_session.StopCapture(TwainStopReason.Closed);
						_stopReasonCurrent=TwainCaptureStopReason.None;
					}
					if(_session.State>=4) {
						Log("Closing \""+_session.SourceName+"\".");
					}
					_session.CloseSource();
				}
			}
			catch(Exception ex) {
				Log("Closing the source failed: "+ex.Message);
			}
			try {
				_session?.Dispose();
			}
			catch {
			}
			_session=null;
			_hwndWake=IntPtr.Zero;
			List<Command> listLeft;
			lock(_lock) {
				listLeft=new List<Command>(_listCommands);
				_listCommands.Clear();
			}
			foreach(Command command in listLeft) {
				if(command.Kind==CommandKind.Enable || command.Kind==CommandKind.Redeliver) {
					RaiseEnded(new EnableContext(command),TwainCaptureEndReason.Refused,"The capture session is closed.");
				}
				else if(command.Kind==CommandKind.Open) {
					command.OpenResult=new TwainCaptureOpenResult { Error=TwainCaptureOpenError.Other,ErrorText="The capture session closed." };
					command.EventDone?.Set();
				}
			}
			SetState(TwainCaptureState.Closed,0);
			lock(_lockLiveSessions) {
				_listLiveSessions.Remove(this);//The source and the DSM are closed: another session may open the source now.
				_productNameLive="";
			}
			_eventClosed.Set();
		}

		private void SetState(TwainCaptureState state,int enableId) {
			_state=(int)state;
			EnableContext context=_enable;
			bool isSafeDisableInEffect=enableId!=0 && context!=null && context.Id==enableId && context.IsSafeDisableInEffect;
			TwainCaptureStatus status=new TwainCaptureStatus { State=state,EnableId=enableId,Text=GetStateText(state),
				IsSafeDisableInEffect=isSafeDisableInEffect };
			Raise(StatusChanged,status,"StatusChanged");
		}

		public static string GetStateText(TwainCaptureState state) {
			switch(state) {
				case TwainCaptureState.Opening: return "Opening the TWAIN source";
				case TwainCaptureState.Open: return "Sensor software not running";
				case TwainCaptureState.Enabling: return "Starting the sensor software";
				case TwainCaptureState.WaitingForImage: return "Sensor software open, waiting for exposure";
				case TwainCaptureState.Transferring: return "Receiving the image";
				case TwainCaptureState.Stopping: return "Stopping the sensor software";
				case TwainCaptureState.WaitingForSource: return "Waiting for the sensor software to finish";
				case TwainCaptureState.Closing: return "Closing the TWAIN source";
				default: return "Closed";
			}
		}

		private void Raise<T>(Action<T> handler,T value,string name) {
			if(handler==null) {
				return;
			}
			try {
				handler(value);
			}
			catch(Exception ex) {
				Log("A "+name+" handler failed: "+ex.Message);
			}
		}

		///<summary>A line for the capture log file, and for Logged when this is the worker (every event is raised on the worker; a line
		///written on another thread, by Open or QuarantineImage, goes to the file only).</summary>
		private void Log(string text) {
			string line="Capture session "+SessionGuid.ToString("N").Substring(0,8)+": "+text;
			try {
				_captureLog.Write(line);
			}
			catch {
			}
			if(!IsWorkerThread) {
				return;
			}
			try {
				Logged?.Invoke(line);
			}
			catch {
			}
		}
		#endregion Worker

		#region Open
		private bool DoOpen(Command command) {
			TwainCaptureOpenResult result=new TwainCaptureOpenResult();
			try {
				bool isGateRefused=false;
				_session=new TwainSession(_dsmLoader) {
					IsCaptureMode=true,
					Settings=_options.ToSessionSettings(command.ProductName),
					SourceGate=_options.ToSourceGate(),
					DrainSink=new WorkerDrainSink(this),
					FuncNow=_funcNow,
					TickMilliseconds=Math.Max(10,_options.TickMilliseconds),
					LoopTimeoutMilliseconds=0,
					FuncIsInterruptRequested=IsInterruptRequested,
				};
				_session.Logged+=Log;
				_session.WaitingForSourceChanged+=OnWaitingForSourceChanged;
				_session.SourceRefusedByGate+=x => isGateRefused=true;
				Log("Opening \""+command.ProductName+"\" (device "+_options.ImagingDeviceNum+", "+_options.SessionMode+", Bits="+_options.Bits
					+", DisableIsSafe="+(_options.DisableIsSafe ? "1" : "0")+").");
				foreach(string warning in _options.ListWarnings) {
					Log("capture.ini: "+warning);
				}
				if(!_session.OpenSource(command.ProductName)) {
					result.Error=MapOpenError(_session.ErrorCode,isGateRefused);
					result.ErrorText=_session.ErrorText;
					result.ConditionCode=_session.LastConditionCode==Tw.TWCC_SUCCESS ? (ushort?)null : _session.LastConditionCode;
					Log("The source could not be opened: "+_session.ErrorText);
					return false;
				}
				_productName=_session.SourceName;
				_session.Settings=_options.ToSessionSettings(_session.SourceName);
				_hwndWake=_session.ProxyWindowHandle;
				_sourceInfo=ReadSourceInfo();
				result.IsOpen=true;
				result.SourceInfo=_sourceInfo;
				SetState(TwainCaptureState.Open,0);
				return true;
			}
			catch(Exception ex) {
				result.IsOpen=false;
				result.Error=TwainCaptureOpenError.Other;
				result.ErrorText=ex.Message;
				Log("Opening the source failed: "+ex);
				return false;
			}
			finally {
				command.OpenResult=result;
				command.EventDone?.Set();
			}
		}

		private static TwainCaptureOpenError MapOpenError(EZTwainErrorCode errorCode,bool isGateRefused) {
			if(isGateRefused) {
				return TwainCaptureOpenError.SourceNotEnabledHere;
			}
			switch(errorCode) {
				case EZTwainErrorCode.EZTEC_LOAD_DSM:
				case EZTwainErrorCode.EZTEC_OPEN_DSM:
					return TwainCaptureOpenError.NoDsm;
				case EZTwainErrorCode.EZTEC_NO_SUCH_DS:
				case EZTwainErrorCode.EZTEC_OPEN_DS:
					return TwainCaptureOpenError.SourceNotFound;
				default:
					return TwainCaptureOpenError.Other;
			}
		}

		///<summary>The identity and the capabilities the session reads and never sets (design 7.1 CAP-3), for Test capture and the log.</summary>
		private TwainCaptureSourceInfo ReadSourceInfo() {
			TwainCaptureSourceInfo info=new TwainCaptureSourceInfo { DsmName=_session.DsmLibraryName };
			TW_IDENTITY? identity=_session.GetOpenSourceIdentity();
			if(identity!=null) {
				info.ProductName=identity.Value.ProductName??"";
				info.Manufacturer=identity.Value.Manufacturer??"";
				info.ProductFamily=identity.Value.ProductFamily??"";
				info.VersionInfo=identity.Value.Version.Info??"";
				info.ProtocolMajor=identity.Value.ProtocolMajor;
				info.ProtocolMinor=identity.Value.ProtocolMinor;
				info.SupportedGroups=identity.Value.SupportedGroups;
			}
			info.UiControllable=ReadBool(Tw.CAP_UICONTROLLABLE);
			info.DeviceOnline=ReadBool(Tw.CAP_DEVICEONLINE);
			info.Indicators=ReadBool(Tw.CAP_INDICATORS);
			info.UndefinedImageSize=ReadBool(Tw.ICAP_UNDEFINEDIMAGESIZE);
			info.PixelType=ReadInt(Tw.ICAP_PIXELTYPE);
			info.PixelFlavor=ReadInt(Tw.ICAP_PIXELFLAVOR);
			info.BitOrder=ReadInt(Tw.ICAP_BITORDER);
			info.PlanarChunky=ReadInt(Tw.ICAP_PLANARCHUNKY);
			info.SupportedSizes=ReadInt(Tw.ICAP_SUPPORTEDSIZES);
			TwainCapabilityValues values;
			if(_session.TryGetCapabilityValues(Tw.ICAP_BITDEPTH,Tw.MSG_GET,out values)) {
				info.ListBitDepths=values.ConType==Tw.TWON_RANGE ? new List<int> { values.RangeMin,values.RangeMax } : values.ListItems;
			}
			ushort conditionCode;
			int? support=_session.GetQuerySupport(Tw.ICAP_EXTIMAGEINFO,out conditionCode);
			info.IsExtImageInfoSettable=support!=null && (support.Value & Tw.TWQC_SET)!=0;
			if(_session.TryGetCapabilityValues(Tw.ICAP_SUPPORTEDEXTIMAGEINFO,Tw.MSG_GET,out values)) {
				info.ListSupportedExtImageInfo=values.ListItems;
				info.IsMetadataListed=values.Contains(Tw.TWEI_TWAINDIRECTMETADATA);
			}
			AddCustomCapabilityInfo(info,"SafeDisableCap",_options.SafeDisableCap);
			AddCustomCapabilityInfo(info,"RedeliverCap",_options.RedeliverCap);
			AddCustomCapabilityInfo(info,"NoActivateCap",_options.NoActivateCap);
			AddCustomCapabilityInfo(info,"ContinuousCap",_options.ContinuousCap);
			Log("Opened \""+info.ProductName+"\" ("+info.Manufacturer+", "+info.VersionInfo+", protocol "+info.ProtocolMajor+"."+info.ProtocolMinor
				+") through "+info.DsmName+"; CAP_UICONTROLLABLE "+Describe(info.UiControllable)+", CAP_DEVICEONLINE "+Describe(info.DeviceOnline)
				+", ICAP_BITDEPTH "+(info.ListBitDepths==null ? "not readable" : string.Join("|",info.ListBitDepths))
				+", capture metadata "+(info.IsMetadataListed ? "listed" : "not listed")+".");
			return info;
		}

		private void AddCustomCapabilityInfo(TwainCaptureSourceInfo info,string key,ushort? capability) {
			if(capability==null) {
				return;
			}
			ushort conditionCode;
			int? support=_session.GetQuerySupport(capability.Value,out conditionCode);
			info.ListCustomCapabilities.Add(new TwainCustomCapabilityInfo {
				Key=key,
				Number=capability.Value,
				QuerySupport=support,
				ConditionCode=support==null ? conditionCode : (ushort?)null,
			});
		}

		private bool? ReadBool(ushort capability) {
			int value;
			return _session.TryGetCapabilityValue(capability,out value) ? value!=0 : (bool?)null;
		}

		private int? ReadInt(ushort capability) {
			int value;
			return _session.TryGetCapabilityValue(capability,out value) ? value : (int?)null;
		}

		private static string Describe(bool? value) {
			return value==null ? "not reported" : (value.Value ? "TRUE" : "FALSE");
		}
		#endregion Open

		#region Enable
		///<summary>One enable, from its command to its Ended.</summary>
		private sealed class EnableContext {
			public readonly int Id;
			public readonly TwainEnableRequest Request;
			public readonly bool IsRedelivery;
			public TwainCaptureEndReason Reason=TwainCaptureEndReason.Completed;
			public TwainCaptureStopReason StopReason;
			public ushort? ConditionCode;
			public string ConditionText;
			public string ErrorText;
			public string Reason8Bit;
			public bool IsBitDepth16InEffect;
			public bool IsContinuous;
			public bool IsSafeDisableInEffect;
			public bool WasDisableStartedUnsafe;
			public bool IsSourceMayHoldImages;
			public int CountImages;
			public int CountNormal;
			public int CountExtra;
			public int CountDrained;
			public int CountTransferFailures;
			///<summary>NowMs at which [Device.N] SessionTimeoutMinutes stops the enable, or 0.</summary>
			public long DeadlineMs;

			public EnableContext(Command command) {
				Id=command.EnableId;
				Request=command.Request??new TwainEnableRequest();
				IsRedelivery=command.Kind==CommandKind.Redeliver;
			}
		}

		private void DoEnable(Command command) {
			EnableContext context=new EnableContext(command);
			if(_isClosing || _session==null || _session.State!=4) {
				RaiseEnded(context,TwainCaptureEndReason.Refused,_isClosing ? "The capture session is closing."
					: "The source is not open and idle (state "+(_session==null ? 1 : _session.State)+").");
				return;
			}
			_enable=context;
			try {
				SetState(TwainCaptureState.Enabling,context.Id);
				_session.ClearError();
				_session.ResetCaptureStopFlags();
				Log("Enable "+context.Id+(context.IsRedelivery ? " (redelivery)" : "")+": "+context.Request.Bits+" bits"
					+(string.IsNullOrEmpty(context.Request.TargetLabel) ? "" : ", target "+context.Request.TargetLabel)+".");
				if(!PrepareEnable(context)) {
					return;
				}
				bool isShowUI=_session.GetCaptureShowUI(context.Request.ShowUI ?? !_options.Headless);
				if(_isClosing) {
					context.Reason=TwainCaptureEndReason.Refused;
					context.ErrorText="The capture session is closing.";
					return;
				}
				TwainCaptureStopReason reasonBeforeEnable;
				if(TryTakeStopRequest(context,out reasonBeforeEnable)) {
					//A Disable or Cancel posted while the enable was prepared: MSG_ENABLEDS is not sent, so the sensor software never starts and
					//nothing is stopped without safe disable.
					context.StopReason=reasonBeforeEnable;
					context.Reason=TwainCaptureEndReason.Stopped;
					Log("Enable "+context.Id+" is stopped ("+reasonBeforeEnable+") before MSG_ENABLEDS; the sensor software was not started.");
					return;
				}
				ushort rc=_session.EnableForCapture(isShowUI);
				if(rc!=Tw.TWRC_SUCCESS && rc!=Tw.TWRC_CHECKSTATUS) {
					context.Reason=TwainCaptureEndReason.EnableFailed;
					if(rc==Tw.TWRC_FAILURE) {
						context.ConditionCode=_session.LastConditionCode;
						context.ConditionText=_session.GetSourceTextForCode(_session.LastConditionCode);
						context.ErrorText="The sensor software could not be started (condition code "+TwainSession.ToHex(_session.LastConditionCode)+")"
							+(context.ConditionText==null ? "." : ": "+context.ConditionText);
					}
					else {
						context.ErrorText="The enable was cancelled (return code "+rc+").";
					}
					Log(context.ErrorText);
					return;
				}
				RunEnabled(context);
			}
			catch(Exception ex) {
				Log("Enable "+context.Id+" failed: "+ex);
				context.Reason=TwainCaptureEndReason.Error;
				context.ErrorText=ex.Message;
				try {
					if(_session!=null && _session.State>=5) {
						context.StopReason=TwainCaptureStopReason.Error;
						StopEnable(context,TwainCaptureStopReason.Error);
						context.Reason=TwainCaptureEndReason.Error;
					}
				}
				catch(Exception exStop) {
					Log("Stopping after the failure failed: "+exStop.Message);
				}
			}
			finally {
				FinishEnable(context);
			}
		}

		///<summary>State 4 before MSG_ENABLEDS. False when the enable must not happen; context then says why.</summary>
		private bool PrepareEnable(EnableContext context) {
			if(!context.IsRedelivery && _isRedeliverMaybeSet && !ClearRedeliver()) {
				context.Reason=TwainCaptureEndReason.EnableFailed;
				context.ErrorText="The redelivery capability could not be set FALSE again, so an enable would not start a capture.";
				return false;
			}
			int deviceOnline;
			if(_session.TryGetCapabilityValue(Tw.CAP_DEVICEONLINE,out deviceOnline) && deviceOnline==0) {
				context.Reason=TwainCaptureEndReason.DeviceOffline;
				context.ErrorText="Sensor not connected.";
				Log("\""+_session.SourceName+"\" reports CAP_DEVICEONLINE FALSE; nothing was enabled.");
				return false;
			}
			TwainByteOrder byteOrder=context.Request.ByteOrder16 ?? _options.ByteOrder16;
			TwainCaptureNegotiationResult negotiation=_session.NegotiateCapture(new TwainCaptureNegotiation {
				Want16Bit=context.Request.Bits==16,
				IsByteOrderBigEndian=byteOrder==TwainByteOrder.BE,
				WantExtImageInfo=true,
				XferCount=1,
			});
			context.IsBitDepth16InEffect=negotiation.Bits==16;
			context.Reason8Bit=negotiation.Reason8Bit;
			if(negotiation.RefusalText!=null) {
				context.Reason=TwainCaptureEndReason.EnableFailed;
				context.ErrorText=negotiation.RefusalText;
				return false;
			}
			if(_options.NoActivateCap!=null) {
				_session.SetCustomCapability(_options.NoActivateCap.Value,true,"NoActivateCap");
			}
			if(_options.SessionMode==TwainSessionMode.Continuous && !context.IsRedelivery) {
				if(_options.ContinuousCap==null) {
					Log("SessionMode=Continuous, but ContinuousCap is empty: the enable runs per slot and never waits in state 5 after Count=0.");
				}
				else if(_session.SetCustomCapability(_options.ContinuousCap.Value,true,"ContinuousCap")==TwainCustomCapOutcome.Set) {
					context.IsContinuous=true;
				}
				else {
					Log("SessionMode=Continuous, but the source did not accept ContinuousCap: the enable runs per slot.");
				}
			}
			context.IsSafeDisableInEffect=_session.PrepareSafeDisable();
			if(_options.DisableIsSafe && _options.SafeDisableCap==null) {
				Log("DisableIsSafe=1, but SafeDisableCap is empty: a disable is not treated as safe.");
			}
			if(_options.SessionTimeout>TimeSpan.Zero) {
				if(_options.DisableIsSafe && context.IsSafeDisableInEffect) {
					context.DeadlineMs=NowMs+(long)_options.SessionTimeout.TotalMilliseconds;
				}
				else {
					Log("SessionTimeoutMinutes is not honoured: it needs DisableIsSafe=1 and the safe-disable capability accepted.");
				}
			}
			if(context.IsRedelivery) {
				//Last, right before MSG_ENABLEDS (design 7.1 CAP-3, W15).
				if(_options.RedeliverCap==null) {
					context.Reason=TwainCaptureEndReason.RedeliveryUnavailable;
					context.ErrorText="The device profile names no redelivery capability ([Device.N] RedeliverCap).";
					Log(context.ErrorText);
					return false;
				}
				if(_session.SetCustomCapability(_options.RedeliverCap.Value,true,"RedeliverCap")!=TwainCustomCapOutcome.Set) {
					context.Reason=TwainCaptureEndReason.RedeliveryUnavailable;
					context.ErrorText="The source did not accept the redelivery capability; nothing was enabled.";
					return false;
				}
				_isRedeliverMaybeSet=true;
			}
			return true;
		}

		///<summary>RedeliverCap FALSE in state 4. True when it is FALSE now.</summary>
		private bool ClearRedeliver() {
			if(_options.RedeliverCap==null || _session==null || _session.State!=4) {
				return !_isRedeliverMaybeSet;
			}
			TwainCustomCapOutcome outcome=_session.SetCustomCapability(_options.RedeliverCap.Value,false,"RedeliverCap");
			_isRedeliverMaybeSet=outcome==TwainCustomCapOutcome.Refused || outcome==TwainCustomCapOutcome.NotSent;
			return !_isRedeliverMaybeSet;
		}

		///<summary>States 5 to 7 of one enable, until it is back in state 4.</summary>
		private void RunEnabled(EnableContext context) {
			SetState(TwainCaptureState.WaitingForImage,context.Id);
			bool isRepeat=false;
			while(_session.State>=5) {
				if(_session.State>=6) {
					if(!TransferPending(context)) {
						return;
					}
					continue;
				}
				ushort notification=_session.WaitForCaptureNotification(isRepeat && context.IsContinuous);
				if(notification==Tw.MSG_XFERREADY) {
					isRepeat=true;
					SetState(TwainCaptureState.Transferring,context.Id);
					if(!TransferPending(context)) {
						return;
					}
					if(_session.State==5) {
						if(context.IsContinuous) {
							SetState(TwainCaptureState.WaitingForImage,context.Id);
							continue;
						}
						//Count=0 in PerSlot mode, or the end of a redelivery: never wait in state 5.
						SetState(TwainCaptureState.Stopping,context.Id);
						_session.DisableCapture();
					}
					context.Reason=context.CountTransferFailures>0 ? TwainCaptureEndReason.TransferFailed : TwainCaptureEndReason.Completed;
					continue;
				}
				if(notification==Tw.MSG_CLOSEDSREQ) {
					context.Reason=TwainCaptureEndReason.CloseRequested;
					context.ConditionCode=_session.EndConditionCode;
					context.ConditionText=_session.EndConditionText;
					SetState(TwainCaptureState.Stopping,context.Id);
					_session.DisableCapture();
					continue;
				}
				if(_session.IsLoopInterrupted) {
					TwainCaptureStopReason reason;
					if(TryTakeStopRequest(context,out reason)) {
						StopEnable(context,reason);
						return;
					}
					continue;
				}
				//The message loop failed, or WM_QUIT arrived on this thread.
				TwainCaptureStopReason reasonLoop=_session.LoopStopReason==TwainStopReason.Quit ? TwainCaptureStopReason.Close : TwainCaptureStopReason.Error;
				if(reasonLoop==TwainCaptureStopReason.Close) {
					BeginClose();
				}
				context.ErrorText=_session.ErrorText;
				StopEnable(context,reasonLoop);
				return;
			}
		}

		///<summary>State 6: transfers every pending image, raising each, unless a stop was asked for in between. False when the enable was
		///stopped.</summary>
		private bool TransferPending(EnableContext context) {
			while(_session.State==6) {
				TwainCaptureStopReason reason;
				if(TryTakeStopRequest(context,out reason)) {
					StopEnable(context,reason);
					return false;
				}
				AcquiredImage image;
				TransferOutcome outcome=_session.TransferCaptureImage(out image);
				if(outcome==TransferOutcome.Done) {
					AcquiredImageKind kind=AcquiredImageKind.Normal;
					if(!context.IsContinuous && !context.IsRedelivery && context.CountNormal>=1) {
						kind=AcquiredImageKind.Extra;
						Log("Warning: \""+_session.SourceName+"\" sent more images than the one asked for (CAP_XFERCOUNT=1); this one is an extra image.");
					}
					Deliver(context,image,kind,TwainCaptureStopReason.None);
					continue;
				}
				context.CountTransferFailures++;
				context.IsSourceMayHoldImages=true;
				context.ConditionCode=_session.TransferConditionCode??context.ConditionCode;//Never the code of an earlier, unrelated failure.
				Log("Capture event: image not received from \""+_session.SourceName+"\" (enable "+context.Id+"): "+_session.ErrorText);
			}
			return true;
		}

		///<summary>Called from the message loop on each of its own wake-ups (WM_APP_WAKE, the tick), never on a message for the source: a command
		///is waiting, the session is closing, or the session timeout passed.</summary>
		private bool IsInterruptRequested() {
			lock(_lock) {
				if(_listCommands.Count>0) {
					return true;
				}
			}
			EnableContext context=_enable;
			return _isClosing || (context!=null && context.DeadlineMs>0 && NowMs>=context.DeadlineMs);
		}

		///<summary>While enabled, and once right before MSG_ENABLEDS: takes the first Disable from the queue, or sees a Close (left queued for
		///after the enable), the session timeout or BeginClose. Enable and RedeliveryRequest commands before it are refused at once: the source
		///is already enabled, or being enabled.</summary>
		private bool TryTakeStopRequest(EnableContext context,out TwainCaptureStopReason reason) {
			reason=TwainCaptureStopReason.None;
			List<Command> listRefused=new List<Command>();
			lock(_lock) {
				LinkedListNode<Command> node=_listCommands.First;
				while(node!=null) {
					LinkedListNode<Command> next=node.Next;
					Command command=node.Value;
					if(command.Kind==CommandKind.Enable || command.Kind==CommandKind.Redeliver) {
						listRefused.Add(command);
						_listCommands.Remove(node);
					}
					else if(command.Kind==CommandKind.Disable) {
						reason=command.StopReason;
						_listCommands.Remove(node);
						break;
					}
					else if(command.Kind==CommandKind.Close) {
						reason=TwainCaptureStopReason.Close;
						break;
					}
					node=next;
				}
			}
			foreach(Command command in listRefused) {
				RaiseEnded(new EnableContext(command),TwainCaptureEndReason.Refused,"The source is already enabled, or being enabled (enable "+context.Id+").");
			}
			if(reason==TwainCaptureStopReason.None && _isClosing) {
				reason=TwainCaptureStopReason.Close;
			}
			if(reason==TwainCaptureStopReason.None && context.DeadlineMs>0 && NowMs>=context.DeadlineMs) {
				reason=TwainCaptureStopReason.SessionTimeout;
				Log("[Device.N] SessionTimeoutMinutes passed for enable "+context.Id+".");
			}
			return reason!=TwainCaptureStopReason.None;
		}

		///<summary>A stop of the running enable in the safe order (CAP-1): images transferred during it are raised as Drained with reason.</summary>
		private void StopEnable(EnableContext context,TwainCaptureStopReason reason) {
			context.StopReason=reason;
			context.Reason=reason==TwainCaptureStopReason.Error ? TwainCaptureEndReason.Error : TwainCaptureEndReason.Stopped;
			if(_session.State>=5 && !context.IsSafeDisableInEffect) {
				context.WasDisableStartedUnsafe=true;
				Log("Capture event: enable "+context.Id+" is stopped ("+reason+") without safe disable; the source may hold an exposure it has"
					+" not delivered.");
			}
			else {
				Log("Stopping enable "+context.Id+" ("+reason+").");
			}
			SetState(TwainCaptureState.Stopping,context.Id);
			_stopReasonCurrent=reason;
			try {
				_session.StopCapture(MapStopReason(reason));
			}
			finally {
				_stopReasonCurrent=TwainCaptureStopReason.None;
			}
		}

		private static TwainStopReason MapStopReason(TwainCaptureStopReason reason) {
			switch(reason) {
				case TwainCaptureStopReason.SessionTimeout: return TwainStopReason.Timeout;
				case TwainCaptureStopReason.Close: return TwainStopReason.Closed;
				case TwainCaptureStopReason.Error: return TwainStopReason.Error;
				default: return TwainStopReason.Cancel;
			}
		}

		private void FinishEnable(EnableContext context) {
			if(_session!=null) {
				if(_isRedeliverMaybeSet && _session.State==4) {
					ClearRedeliver();//As soon as the redelivery enable ends, before any other enable.
				}
				if(_session.CountRefusedDisables>0) {
					Log("Enable "+context.Id+": MSG_DISABLEDS was refused "+_session.CountRefusedDisables+" time(s).");
				}
				context.IsSourceMayHoldImages|=_session.SourceMayHoldImages;
				if(_session.WasRefusedDisableUnanswered) {
					//Design 7.13. Whether the way-out was refused and a second bound waited is told here, not in TwainCaptureEnded.
					Log("Capture event: the way-out MSG_DISABLEDS was sent after the wait bound (enable "+context.Id+")."
						+(_session.WasWayOutDisableRefused ? " It was refused (the source was still finishing), and the session waited a second"
							+" bound." : "")
						+(context.IsSourceMayHoldImages ? " The source may still hold images." : " The source then delivered what it held."));
				}
				if(context.ConditionCode==null && _session.EndConditionCode!=null && context.Reason!=TwainCaptureEndReason.EnableFailed) {
					context.ConditionCode=_session.EndConditionCode;
					context.ConditionText=_session.EndConditionText;
				}
			}
			bool wasRefused=_session!=null && _session.CountRefusedDisables>0;
			bool wasUnanswered=_session!=null && _session.WasRefusedDisableUnanswered;
			_enable=null;
			RaiseEnded(context,context.Reason,context.ErrorText,wasRefused,wasUnanswered);
			if(_session!=null && _session.State==4) {
				SetState(TwainCaptureState.Open,0);
			}
		}

		private void RaiseEnded(EnableContext context,TwainCaptureEndReason reason,string errorText,bool wasDisableRefused=false,
			bool wasSecondDisableAfterWait=false)
		{
			TwainCaptureEnded ended=new TwainCaptureEnded {
				EnableId=context.Id,
				Tag=context.Request.Tag,
				IsRedelivery=context.IsRedelivery,
				Reason=reason,
				StopReason=context.StopReason,
				ConditionCode=context.ConditionCode,
				ConditionText=context.ConditionText,
				ImageCount=context.CountImages,
				ExtraImageCount=context.CountExtra,
				DrainedImageCount=context.CountDrained,
				TransferFailureCount=context.CountTransferFailures,
				SourceMayHoldImages=context.IsSourceMayHoldImages,
				IsSafeDisableInEffect=context.IsSafeDisableInEffect,
				WasDisableStartedUnsafe=context.WasDisableStartedUnsafe,
				WasDisableRefused=wasDisableRefused,
				WasSecondDisableAfterWait=wasSecondDisableAfterWait,
				IsBitDepth16InEffect=context.IsBitDepth16InEffect,
				Reason8Bit=context.Reason8Bit,
				ErrorText=errorText,
				EndedAt=GetNow(),
			};
			Log("Enable "+context.Id+" ended: "+reason+(context.StopReason==TwainCaptureStopReason.None ? "" : " ("+context.StopReason+")")
				+", "+context.CountImages+" image(s)"+(ended.ConditionCode==null ? "" : ", condition code "+TwainSession.ToHex(ended.ConditionCode.Value))
				+(string.IsNullOrEmpty(errorText) ? "" : ": "+errorText)+".");
			Raise(Ended,ended,"Ended");
		}

		private void OnWaitingForSourceChanged(bool isWaiting) {
			EnableContext context=_enable;
			SetState(isWaiting ? TwainCaptureState.WaitingForSource : TwainCaptureState.Stopping,context==null ? 0 : context.Id);
		}

		private DateTime GetNow() {
			try {
				return _funcNow();
			}
			catch {
				return DateTime.Now;
			}
		}
		#endregion Enable

		#region Images
		///<summary>The drain sink of the capture path: images transferred during a stop, or offered after a refused disable.</summary>
		private sealed class WorkerDrainSink:ITwainDrainSink {
			private readonly TwainCaptureSession _owner;

			public WorkerDrainSink(TwainCaptureSession owner) {
				_owner=owner;
			}

			public void ImageDrained(TwainDrainedImage drained) {
				_owner.OnDrained(drained);
			}

			public void MayStillHoldImages(string sourceName,TwainStopReason stopReason) {
				EnableContext context=_owner._enable;
				if(context!=null) {
					context.IsSourceMayHoldImages=true;
				}
			}
		}

		private void OnDrained(TwainDrainedImage drained) {
			if(drained.Acquired==null) {
				return;
			}
			EnableContext context=_enable??new EnableContext(new Command { Kind=CommandKind.Enable,EnableId=0 });
			TwainCaptureStopReason reason=_stopReasonCurrent!=TwainCaptureStopReason.None ? _stopReasonCurrent : context.StopReason;
			if(reason==TwainCaptureStopReason.None && !context.IsContinuous && !context.IsRedelivery && context.CountNormal>=1) {
				//No stop of the session's: the disable after Count=0 (or after MSG_CLOSEDSREQ) was refused and the source then offered more. The
				//one image CAP_XFERCOUNT=1 asked for has arrived, so this is an extra image (design 7.12), not one for the enable's target.
				Log("Warning: \""+_session.SourceName+"\" sent more images than the one asked for (CAP_XFERCOUNT=1) after the disable was refused;"
					+" this one is an extra image.");
				Deliver(context,drained.Acquired,AcquiredImageKind.Extra,reason);
				return;
			}
			Deliver(context,drained.Acquired,AcquiredImageKind.Drained,reason);
		}

		///<summary>Tags the image with its enable, writes its journal copy, then raises ImageAcquired; after BeginClose writes it to the
		///quarantine instead and raises ImageQuarantined. The choice between the two is made under _lockRaiseImage, which BeginClose takes
		///to set _isClosing: an image is raised only if BeginClose has not started, and BeginClose returns only after a raise under way has
		///ended. So a BeginClose during the journal write (slow I/O for a 16-bit image) still sends the image to the quarantine.</summary>
		private void Deliver(EnableContext context,AcquiredImage image,AcquiredImageKind kind,TwainCaptureStopReason stopReason) {
			image.EnableId=context.Id;
			image.Tag=context.Request.Tag;
			image.TargetLabel=context.Request.TargetLabel??"";
			image.Kind=kind;
			image.StopReason=stopReason;
			image.IsRedelivery=context.IsRedelivery;
			image.RequestedBits=context.Request.Bits;
			image.SidecarFields=context.Request.SidecarFields==null ? null : new List<KeyValuePair<string,string>>(context.Request.SidecarFields);
			image.SourceName=string.IsNullOrEmpty(image.SourceName) ? _productName : image.SourceName;
			image.IndexInEnable=++context.CountImages;
			switch(kind) {
				case AcquiredImageKind.Extra: context.CountExtra++; break;
				case AcquiredImageKind.Drained: context.CountDrained++; break;
				default: context.CountNormal++; break;
			}
			bool isClosingAtArrival=_isClosing;
			TwainCaptureQuarantineContext quarantineContext=QuarantineContext;//Read once: the PatNum and the name of one patient.
			CaptureRecord record=BuildRecord(image,quarantineContext);
			CaptureImageFile file;
			try {
				file=CaptureImageFile.FromAcquiredImage(image);
			}
			catch(Exception ex) {
				Log("Image "+image.IndexInEnable+" of enable "+context.Id+" has no pixels to save: "+ex.Message);
				file=null;
			}
			if(!isClosingAtArrival) {
				image.JournalPath=file==null ? null : _journal.Write(file,record,CaptureJournal.DeliveredCaller,null,_options.JournalKeep,Log);
				if(image.JournalPath!=null) {
					Log("Journal copy "+Path.GetFileName(image.JournalPath)+" of image "+image.IndexInEnable+" of enable "+context.Id+" ("+kind
						+(stopReason==TwainCaptureStopReason.None ? "" : ", stop "+stopReason)+").");
				}
				lock(_lockRaiseImage) {
					if(!_isClosing) {
						Raise(ImageAcquired,image,"ImageAcquired");
						return;
					}
				}
			}
			//After BeginClose, also when it came while the journal copy was written: the quarantine, never ImageAcquired.
			string error="";
			string pathQuarantine=file==null ? null : _quarantine.WriteSessionCapture(file,record,SessionGuid,quarantineContext.PatNum,
				quarantineContext.PatientName,image.TargetLabel,"It arrived after the capture window began to close.",Log,out error);
			image.QuarantinePath=pathQuarantine;
			string delivered=pathQuarantine==null ? CaptureJournal.DeliveredNone : CaptureJournal.DeliveredQuarantine;
			string deliveredDetail=pathQuarantine==null ? "The quarantine could not be written: "+error : Path.GetFileName(pathQuarantine);
			if(isClosingAtArrival) {
				image.JournalPath=file==null ? null : _journal.Write(file,record,delivered,deliveredDetail,_options.JournalKeep,Log);
			}
			else if(image.JournalPath!=null) {
				//The copy was written as Delivered=Caller before BeginClose was seen; its sidecar now says where the image went.
				image.JournalPath=_journal.SetDelivered(image.JournalPath,file,record,delivered,deliveredDetail,Log);
			}
			Log("Capture event: an image of enable "+context.Id+" arrived while the session closed; "+(pathQuarantine==null
				? "it could not be quarantined ("+error+")"+(image.JournalPath==null ? "" : "; its journal copy is the only one")
				: "quarantined as "+Path.GetFileName(pathQuarantine))+".");
			Raise(ImageQuarantined,image,"ImageQuarantined");
		}

		///<summary>The sidecar record of image, with the PatNum of quarantineContext (the caller reads the context once and uses the same one
		///for the quarantine sidecar's patient name).</summary>
		private CaptureRecord BuildRecord(AcquiredImage image,TwainCaptureQuarantineContext quarantineContext) {
			CaptureRecord record=CaptureRecord.Create(image.ReceivedAt==default(DateTime) ? GetNow() : image.ReceivedAt,
				image.SourceName??_productName,SessionGuid.ToString("N").Substring(0,8)+"-"+image.EnableId.ToString(CultureInfo.InvariantCulture));
			record.PatNum=quarantineContext.PatNum;
			CaptureMetadataKeys keys=ReadMetadataKeys(image.MetadataJson);
			if(keys!=null) {
				record.CaptureId=keys.CaptureId;
				record.Sequence=keys.Sequence;
			}
			record.ListCaptureFields.Add(new SidecarField("SessionGuid",SessionGuid.ToString("D")));
			record.ListCaptureFields.Add(new SidecarField("EnableId",image.EnableId.ToString(CultureInfo.InvariantCulture)));
			record.ListCaptureFields.Add(new SidecarField("Target",image.TargetLabel??""));
			record.ListCaptureFields.Add(new SidecarField("ImageKind",image.Kind.ToString()));
			record.ListCaptureFields.Add(new SidecarField("StopReason",image.StopReason.ToString()));
			record.ListCaptureFields.Add(new SidecarField("Redelivered",image.IsRedelivery ? "1" : "0"));
			record.ListCaptureFields.Add(new SidecarField("RequestedBits",image.RequestedBits.ToString(CultureInfo.InvariantCulture)));
			record.ListCaptureFields.Add(new SidecarField("DeliveredAs8BitIn16BitEnable",image.IsDeliveredAs8BitIn16BitEnable ? "1" : "0"));
			record.ListCaptureFields.Add(new SidecarField("TransferMechanism",image.TransferMechanism.ToString()));
			record.ListCaptureFields.Add(new SidecarField("PixelFlavor",image.PixelFlavor.ToString(CultureInfo.InvariantCulture)));
			record.ListCaptureFields.Add(new SidecarField(CaptureSidecarKeys.PixelType,image.PixelType.ToString(CultureInfo.InvariantCulture)));
			record.ListCaptureFields.Add(new SidecarField(CaptureSidecarKeys.BitsPerSample,image.BitsPerSample.ToString(CultureInfo.InvariantCulture)));
			record.ListCaptureFields.Add(new SidecarField(CaptureSidecarKeys.BitDepth16InEffect,image.IsBitDepth16InEffect ? "1" : "0"));
			if(image.SidecarFields!=null) {
				foreach(KeyValuePair<string,string> pair in image.SidecarFields) {
					if(!string.IsNullOrWhiteSpace(pair.Key)) {
						record.ListCaptureFields.Add(new SidecarField(pair.Key,pair.Value??""));
					}
				}
			}
			record.ListCaptureFields.Add(new SidecarField("Metadata",image.MetadataJson??""));
			record.ListSettings=_options.GetSidecarFields();
			return record;
		}

		private CaptureMetadataKeys ReadMetadataKeys(string metadataJson) {
			Func<string,CaptureMetadataKeys> funcRead=FuncReadMetadataKeys;
			if(funcRead==null) {
				return null;
			}
			try {
				return funcRead(metadataJson);
			}
			catch(Exception ex) {
				Log("The metadata parser failed: "+ex.Message);
				return null;
			}
		}

		public string QuarantineImage(AcquiredImage image,string reason,bool isPatientUnknown,out string error) {
			error="";
			if(image==null) {
				error="No image.";
				return null;
			}
			try {
				CaptureImageFile file=CaptureImageFile.FromAcquiredImage(image);
				TwainCaptureQuarantineContext context=QuarantineContext;
				string path=_quarantine.WriteSessionCapture(file,BuildRecord(image,context),SessionGuid,isPatientUnknown ? 0 : context.PatNum,
					context.PatientName,image.TargetLabel,reason,Log,out error);
				image.QuarantinePath=path;
				Log(path==null ? "An image of enable "+image.EnableId+" could not be quarantined: "+error
					: "Quarantine image "+Path.GetFileName(path)+" of enable "+image.EnableId+": "+reason);
				return path;
			}
			catch(Exception ex) {
				error=ex.Message;
				return null;
			}
		}
		#endregion Images
	}
}
