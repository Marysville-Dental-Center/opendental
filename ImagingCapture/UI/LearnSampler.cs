// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using ImagingCapture.Camera;
using ImagingCapture.Uvc;

namespace ImagingCapture.UI {
	///<summary>The controls the sampler reads, known after its first poll.</summary>
	public sealed class ControlsFoundEventArgs:EventArgs {
		public IReadOnlyList<ControlId> Controls { get; }

		public ControlsFoundEventArgs(IReadOnlyList<ControlId> listControls) {
			Controls=listControls??new List<ControlId>();
		}
	}

	///<summary>One reading of the sampled controls.</summary>
	public sealed class ControlSampleEventArgs:EventArgs {
		public ControlSample Sample { get; }

		public ControlSampleEventArgs(ControlSample sample) {
			Sample=sample;
		}
	}

	///<summary>A still-pin sample arrived. Only its time is kept; the frame is not used.</summary>
	public sealed class StillSampleEventArgs:EventArgs {
		///<summary>On the sampler's clock (NowMs).</summary>
		public long TimeMs { get; }

		public StillSampleEventArgs(long timeMs) {
			TimeMs=timeMs;
		}
	}

	///<summary>Feeds the Learn window and the button test: on a background thread it reads camera controls with Get only
	///(ControlSampler) every PollMs, and it notes the time of every still-pin sample. The controls are either given, or found on
	///the first poll (every standard property whose Get succeeds, ControlSampler.FindReadable). Each poll gives one
	///ControlSample, also when no control is read, so a recording can be timed on any camera.
	///
	///Threading. Reads happen on the sampler's own thread, never on the UI thread (a camera can take several milliseconds per
	///read) and never in a frame source callback. Events are posted to the SynchronizationContext that was current at
	///construction (the window's UI thread) and are dropped once Stop has been called. With no context, they are raised on the
	///thread that saw them. All times come from one monotonic clock, NowMs.</summary>
	public sealed class LearnSampler:IDisposable {
		#region Fields
		private readonly IFrameSource _frameSource;
		private readonly Func<long> _funcNowMs;
		private readonly SynchronizationContext _synchronizationContext;
		private readonly bool _usePollThread;
		///<summary>Guards every field below.</summary>
		private readonly object _lock=new object();
		///<summary>Serializes polls.</summary>
		private readonly object _lockPoll=new object();
		///<summary>Null until found on the first poll, unless given.</summary>
		private List<ControlId> _listControls;
		private bool _isControlsAnnounced;
		private bool _isRunning;
		private bool _isDisposed;
		///<summary>Incremented by Start and Stop. A posted event runs only if it is unchanged since the event was seen.</summary>
		private int _generation;
		private Thread _threadPoll;
		private ManualResetEvent _manualResetEventStop;
		#endregion Fields

		#region Events
		///<summary>Raised once, after the first poll, with the controls that are read.</summary>
		public event EventHandler<ControlsFoundEventArgs> ControlsFound;
		///<summary>Raised for every poll.</summary>
		public event EventHandler<ControlSampleEventArgs> SampleTaken;
		public event EventHandler<StillSampleEventArgs> StillSampleSeen;
		///<summary>The frame source reported an error (for example the camera was unplugged).</summary>
		public event EventHandler<UvcErrorEventArgs> CameraError;
		#endregion Events

		#region Constructors
		///<summary>A sampler for an opened camera. controls null means: find the readable ones on the first poll. pollMs is held to
		///CameraConfig.PollMsMin-PollMsMax. Captures the current SynchronizationContext; create it on the UI thread.</summary>
		public LearnSampler(IFrameSource frameSource,int pollMs,IEnumerable<ControlId> controls)
			: this(frameSource,pollMs,controls,null,SynchronizationContext.Current,true)
		{
		}

		///<summary>As above, with events posted to synchronizationContext (the window's UI thread).</summary>
		public LearnSampler(IFrameSource frameSource,int pollMs,IEnumerable<ControlId> controls,SynchronizationContext synchronizationContext)
			: this(frameSource,pollMs,controls,null,synchronizationContext,true)
		{
		}

		///<summary>For tests: funcNowMs is the clock (null: a Stopwatch), synchronizationContext may be null, and with usePollThread
		///false nothing polls until PollOnce is called.</summary>
		internal LearnSampler(IFrameSource frameSource,int pollMs,IEnumerable<ControlId> controls,Func<long> funcNowMs,
			SynchronizationContext synchronizationContext,bool usePollThread)
		{
			_frameSource=frameSource??throw new ArgumentNullException(nameof(frameSource));
			PollMs=Math.Min(CameraConfig.PollMsMax,Math.Max(CameraConfig.PollMsMin,pollMs));
			if(controls!=null) {
				_listControls=controls.Distinct().ToList();
			}
			if(funcNowMs==null) {
				Stopwatch stopwatch=Stopwatch.StartNew();
				funcNowMs=() => stopwatch.ElapsedMilliseconds;
			}
			_funcNowMs=funcNowMs;
			_synchronizationContext=synchronizationContext;
			_usePollThread=usePollThread;
		}
		#endregion Constructors

		#region Properties
		public int PollMs { get; }

		///<summary>The sampler's clock in milliseconds. Sample and still times use it.</summary>
		public long NowMs {
			get {
				return _funcNowMs();
			}
		}

		///<summary>The controls read, or null before the first poll found them.</summary>
		public IReadOnlyList<ControlId> Controls {
			get {
				lock(_lock) {
					return _listControls?.ToList();
				}
			}
		}

		public bool IsRunning {
			get {
				lock(_lock) {
					return _isRunning;
				}
			}
		}
		#endregion Properties

		#region Start and Stop
		///<summary>Subscribes to still samples and errors and starts polling. Does nothing when running. Throws ObjectDisposedException
		///after Dispose.</summary>
		public void Start() {
			lock(_lock) {
				if(_isDisposed) {
					throw new ObjectDisposedException(nameof(LearnSampler));
				}
				if(_isRunning) {
					return;
				}
				_isRunning=true;
				_generation++;
				_frameSource.StillCaptured+=FrameSource_StillCaptured;
				_frameSource.Error+=FrameSource_Error;
				if(!_usePollThread) {
					return;
				}
				_manualResetEventStop=new ManualResetEvent(false);
				_threadPoll=new Thread(PollLoop);
				_threadPoll.IsBackground=true;
				_threadPoll.Name="Camera learn sampler";
				_threadPoll.Start(_manualResetEventStop);
			}
		}

		///<summary>Stops polling, unsubscribes, and drops events not yet delivered. Waits up to 2 s for a poll in progress. Safe to
		///call more than once and from an event handler.</summary>
		public void Stop() {
			Thread threadPoll;
			ManualResetEvent manualResetEventStop;
			lock(_lock) {
				if(!_isRunning) {
					return;
				}
				_isRunning=false;
				_generation++;
				_frameSource.StillCaptured-=FrameSource_StillCaptured;
				_frameSource.Error-=FrameSource_Error;
				threadPoll=_threadPoll;
				manualResetEventStop=_manualResetEventStop;
				_threadPoll=null;
				_manualResetEventStop=null;
			}
			if(threadPoll==null) {
				return;
			}
			manualResetEventStop.Set();
			if(threadPoll!=Thread.CurrentThread) {
				threadPoll.Join(2000);
			}
		}

		public void Dispose() {
			Stop();
			lock(_lock) {
				_isDisposed=true;
			}
		}
		#endregion Start and Stop

		#region Polling
		private void PollLoop(object state) {
			ManualResetEvent manualResetEventStop=(ManualResetEvent)state;
			try {
				while(true) {
					try {
						PollOnce();
					}
					catch {
						//A failed read is reported as a missing value in the sample, so this is unexpected; the next poll tries again.
					}
					if(manualResetEventStop.WaitOne(PollMs)) {
						return;
					}
				}
			}
			finally {
				manualResetEventStop.Dispose();
			}
		}

		///<summary>One poll: finds the readable controls if they are not known yet, then reads them once and posts the sample. Does
		///nothing when not running. Called by the poll thread every PollMs; tests call it directly.</summary>
		internal void PollOnce() {
			lock(_lockPoll) {
				int generation;
				List<ControlId> listControls;
				lock(_lock) {
					if(!_isRunning) {
						return;
					}
					generation=_generation;
					listControls=_listControls;
				}
				if(listControls==null) {
					listControls=ControlSampler.FindReadable(_frameSource);
				}
				bool isAnnounce;
				lock(_lock) {
					if(!_isRunning || generation!=_generation) {
						return;
					}
					_listControls=listControls;
					isAnnounce=!_isControlsAnnounced;
					_isControlsAnnounced=true;
				}
				if(isAnnounce) {
					ControlsFoundEventArgs argsFound=new ControlsFoundEventArgs(listControls.ToList());
					Post(generation,() => ControlsFound?.Invoke(this,argsFound));
				}
				ControlSample sample=ControlSampler.Read(_frameSource,listControls,_funcNowMs());
				ControlSampleEventArgs argsSample=new ControlSampleEventArgs(sample);
				Post(generation,() => SampleTaken?.Invoke(this,argsSample));
			}
		}
		#endregion Polling

		#region Frame source events
		private void FrameSource_StillCaptured(object sender,UvcStillEventArgs e) {
			long nowMs=_funcNowMs();
			int generation;
			lock(_lock) {
				if(!_isRunning) {
					return;
				}
				generation=_generation;
			}
			StillSampleEventArgs args=new StillSampleEventArgs(nowMs);
			Post(generation,() => StillSampleSeen?.Invoke(this,args));
		}

		private void FrameSource_Error(object sender,UvcErrorEventArgs e) {
			int generation;
			lock(_lock) {
				if(!_isRunning) {
					return;
				}
				generation=_generation;
			}
			UvcErrorEventArgs args=e??new UvcErrorEventArgs(UvcErrorKind.Failed,0,"");
			Post(generation,() => CameraError?.Invoke(this,args));
		}
		#endregion Frame source events

		#region Dispatch
		///<summary>Runs action on the captured context, or inline without one, unless the sampler stopped or restarted since generation.</summary>
		private void Post(int generation,Action action) {
			SendOrPostCallback sendOrPostCallback=state => {
				lock(_lock) {
					if(generation!=_generation || !_isRunning) {
						return;
					}
				}
				action();
			};
			if(_synchronizationContext==null) {
				sendOrPostCallback(null);
			}
			else {
				_synchronizationContext.Post(sendOrPostCallback,null);
			}
		}
		#endregion Dispatch
	}
}
