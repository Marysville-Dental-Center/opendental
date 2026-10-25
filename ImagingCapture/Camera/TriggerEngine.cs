// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using ImagingCapture.Uvc;

namespace ImagingCapture.Camera {
	///<summary>Which trigger saw a press.</summary>
	public enum TriggerSource {
		///<summary>A still-pin sample ([Trigger.Still]).</summary>
		StillPin=0,
		///<summary>An edge of a polled control value ([Trigger.ControlPoll]).</summary>
		ControlPoll=1,
	}

	///<summary>The state of one trigger or level input.</summary>
	public enum TriggerInputState {
		///<summary>Not enabled in capture.ini.</summary>
		Off=0,
		///<summary>Enabled, but it cannot work: values are missing or invalid, or the camera has no connected still pin. Problem says
		///why. Logged once.</summary>
		Incomplete,
		///<summary>Configured and working (while the engine runs).</summary>
		Armed,
		///<summary>Turned off after a control read failed, for example because the camera does not support the property. Problem says
		///why. Logged once.</summary>
		Failed,
	}

	///<summary>A press that passed the debounce gate.</summary>
	public sealed class TriggerPressEventArgs:EventArgs {
		public TriggerSource Source { get; }
		///<summary>When the press was seen, on the engine's monotonic clock in milliseconds.</summary>
		public long TimeMs { get; }
		///<summary>For a still-pin press, the still sample; it belongs to the handler. Null for a control-poll press.</summary>
		public UvcFrame StillFrame { get; }

		public TriggerPressEventArgs(TriggerSource source,long timeMs,UvcFrame stillFrame) {
			Source=source;
			TimeMs=timeMs;
			StillFrame=stillFrame;
		}
	}

	///<summary>A level input changed state.</summary>
	public sealed class LevelChangedEventArgs:EventArgs {
		///<summary>The N of [Level.N].</summary>
		public int Number { get; }
		public bool IsActive { get; }
		public LevelAction Action { get; }
		///<summary>Whether any PausePreview level is active after this change.</summary>
		public bool IsPausePreviewActive { get; }

		public LevelChangedEventArgs(int number,bool isActive,LevelAction action,bool isPausePreviewActive) {
			Number=number;
			IsActive=isActive;
			Action=action;
			IsPausePreviewActive=isPausePreviewActive;
		}
	}

	///<summary>A problem to log once: an incomplete or failed trigger or level input.</summary>
	public sealed class TriggerWarningEventArgs:EventArgs {
		public string Message { get; }

		public TriggerWarningEventArgs(string message) {
			Message=message??"";
		}
	}

	///<summary>Turns a camera's still pin and polled control values into presses, and tracks level inputs (design 8.4, 8.6).
	///
	///Triggers. StillPin: each still sample is one press. ControlPoll: the configured property is read with Get only (never Set),
	///every PollMs, on the capture source filter; a press is the edge of (value &amp; Mask) toward the active level, that is from
	///inactive to active. The first reading after Start sets the state and never fires. Both triggers pass through one
	///DebounceGate of DebounceMs, so one photo is taken per lockout window, whichever trigger fires first.
	///
	///Levels. Each configured [Level.N] property is read at the same PollMs (a property shared with the trigger is read once per
	///poll). Its state is tracked from the first reading, and LevelChanged is raised when it changes. The only action is
	///PausePreview, active while the level is active (IsPausePreviewActive).
	///
	///Consumer. A press is first put through the debounce gate; one that passes is raised as Pressed when FuncIsConsumerEnabled
	///returns true (or is null), otherwise as PressIgnored (design 8.4: presses are ignored while the window is disabled). An ignored
	///press still starts the lockout, so key bounce right after a message box closes cannot take a photo.
	///
	///Failures. When a control read fails (E_PROP_ID_UNSUPPORTED or any other failure), every input on that control is turned off
	///for the rest of this engine's life and one Warning is raised for each. An enabled input with missing values, or a still
	///trigger without a connected still pin, raises one Warning at Start.
	///
	///Threading. Events are posted to the SynchronizationContext that was current when the engine was created (the window's UI
	///thread), and are dropped when the engine was stopped before they ran. With no context, they are raised on the thread that saw
	///them: the poll thread, or the still pin's streaming thread. FuncIsConsumerEnabled is called where Pressed would be raised.
	///Start the frame source before the engine, so the still pin's state is known.</summary>
	public sealed class TriggerEngine:IDisposable {
		#region Fields
		private readonly IFrameSource _frameSource;
		private readonly Func<long> _funcNowMs;
		private readonly SynchronizationContext _synchronizationContext;
		private readonly bool _usePollThread;
		private readonly DebounceGate _debounceGate;
		private readonly bool _isStillEnabled;
		///<summary>Guards every field below and the inputs' state.</summary>
		private readonly object _lock=new object();
		///<summary>Serializes polls, so the poll thread and a test never read at the same time.</summary>
		private readonly object _lockPoll=new object();
		///<summary>The control-poll trigger, or null when [Trigger.ControlPoll] is not enabled.</summary>
		private readonly PolledInput _inputControlPoll;
		///<summary>One per enabled [Level.N], by number.</summary>
		private readonly List<PolledInput> _listInputLevels=new List<PolledInput>();
		private TriggerInputState _stillState=TriggerInputState.Off;
		private string _stillProblem="";
		private bool _isRunning;
		private bool _hasStarted;
		private bool _isDisposed;
		private bool _isStillSubscribed;
		///<summary>Incremented by Start and Stop. A posted event runs only if it is unchanged since the event was seen.</summary>
		private int _generation;
		private Thread _threadPoll;
		private ManualResetEvent _manualResetEventStop;
		#endregion Fields

		#region Events
		///<summary>A press passed the debounce gate while the consumer was enabled: take one photo.</summary>
		public event EventHandler<TriggerPressEventArgs> Pressed;
		///<summary>A press passed the debounce gate while the consumer was disabled. No photo.</summary>
		public event EventHandler<TriggerPressEventArgs> PressIgnored;
		public event EventHandler<LevelChangedEventArgs> LevelChanged;
		///<summary>Something to log once. Never raised twice for the same input.</summary>
		public event EventHandler<TriggerWarningEventArgs> Warning;
		#endregion Events

		#region Constructors
		///<summary>An engine for one opened camera, with the triggers and levels of cameraConfig. Captures the current
		///SynchronizationContext; create it on the UI thread. Nothing runs until Start.</summary>
		public TriggerEngine(IFrameSource frameSource,CameraConfig cameraConfig) : this(frameSource,cameraConfig,null,SynchronizationContext.Current,true) {
		}

		///<summary>For tests: funcNowMs is the clock (null: a Stopwatch), synchronizationContext may be null, and with usePollThread
		///false nothing polls until PollOnce is called.</summary>
		internal TriggerEngine(IFrameSource frameSource,CameraConfig cameraConfig,Func<long> funcNowMs,SynchronizationContext synchronizationContext,
			bool usePollThread)
		{
			_frameSource=frameSource??throw new ArgumentNullException(nameof(frameSource));
			cameraConfig=cameraConfig??new CameraConfig();
			if(funcNowMs==null) {
				Stopwatch stopwatch=Stopwatch.StartNew();
				funcNowMs=() => stopwatch.ElapsedMilliseconds;
			}
			_funcNowMs=funcNowMs;
			_synchronizationContext=synchronizationContext;
			_usePollThread=usePollThread;
			DebounceMs=cameraConfig.EffectiveDebounceMs;
			PollMs=cameraConfig.EffectivePollMs;
			_debounceGate=new DebounceGate(DebounceMs);
			_isStillEnabled=cameraConfig.StillEnabled;
			ControlInputConfig controlPoll=cameraConfig.ControlPoll??new ControlInputConfig();
			if(controlPoll.Enabled) {
				_inputControlPoll=new PolledInput("["+CameraConfig.SectionTriggerControlPoll+"]",controlPoll.GetBinding(),null);
				_inputControlPoll.SetIncompleteIfNeeded(controlPoll.GetProblem());
			}
			foreach(LevelInputConfig levelInputConfig in (cameraConfig.Levels??new List<LevelInputConfig>()).Where(x => x!=null && x.Enabled).OrderBy(x => x.Number)) {
				PolledInput input=new PolledInput("["+CameraConfig.GetLevelSection(levelInputConfig.Number)+"]",levelInputConfig.GetBinding(),levelInputConfig);
				input.SetIncompleteIfNeeded(levelInputConfig.GetProblem());
				_listInputLevels.Add(input);
			}
		}
		#endregion Constructors

		#region Properties
		///<summary>The debounce lockout in milliseconds ([Trigger] DebounceMs, else 300).</summary>
		public int DebounceMs { get; }

		///<summary>The poll interval in milliseconds ([Trigger.ControlPoll] PollMs).</summary>
		public int PollMs { get; }

		///<summary>Called before Pressed is raised; false means the consumer is disabled and the press is ignored. Null means always
		///enabled. An exception counts as disabled.</summary>
		public Func<bool> FuncIsConsumerEnabled { get; set; }

		public bool IsRunning {
			get {
				lock(_lock) {
					return _isRunning;
				}
			}
		}

		public TriggerInputState StillState {
			get {
				lock(_lock) {
					return _stillState;
				}
			}
		}

		///<summary>"" or why the still trigger is Incomplete, for the settings note.</summary>
		public string StillProblem {
			get {
				lock(_lock) {
					return _stillProblem;
				}
			}
		}

		public TriggerInputState ControlPollState {
			get {
				lock(_lock) {
					return _inputControlPoll?.State??TriggerInputState.Off;
				}
			}
		}

		///<summary>"" or why the control-poll trigger is Incomplete or Failed, for the settings note.</summary>
		public string ControlPollProblem {
			get {
				lock(_lock) {
					return _inputControlPoll?.Problem??"";
				}
			}
		}

		///<summary>True while the polled trigger value is at its active level (the key is held). False before the first reading.</summary>
		public bool IsControlPollActive {
			get {
				lock(_lock) {
					return _inputControlPoll!=null && _inputControlPoll.IsActive;
				}
			}
		}

		///<summary>True while any armed PausePreview level is active.</summary>
		public bool IsPausePreviewActive {
			get {
				lock(_lock) {
					return GetIsPausePreviewActive();
				}
			}
		}
		#endregion Properties

		#region Levels
		///<summary>The state of [Level.N]: Off when it is not enabled.</summary>
		public TriggerInputState GetLevelState(int number) {
			lock(_lock) {
				return FindLevel(number)?.State??TriggerInputState.Off;
			}
		}

		///<summary>"" or why [Level.N] is Incomplete or Failed.</summary>
		public string GetLevelProblem(int number) {
			lock(_lock) {
				return FindLevel(number)?.Problem??"";
			}
		}

		///<summary>True while [Level.N] is armed and active.</summary>
		public bool IsLevelActive(int number) {
			lock(_lock) {
				PolledInput input=FindLevel(number);
				return input!=null && input.State==TriggerInputState.Armed && input.IsActive;
			}
		}

		private PolledInput FindLevel(int number) {
			return _listInputLevels.FirstOrDefault(x => x.Level.Number==number);
		}

		private bool GetIsPausePreviewActive() {
			return _listInputLevels.Any(x => x.State==TriggerInputState.Armed && x.IsActive && x.Level.Action==LevelAction.PausePreview);
		}
		#endregion Levels

		#region Start and Stop
		///<summary>Starts the triggers and levels: subscribes to the still pin when the still trigger is enabled and the still pin is
		///connected, and starts polling when the control-poll trigger or a level is armed. Raises one Warning for each enabled input
		///that cannot work. Does nothing when already running. Throws ObjectDisposedException after Dispose.</summary>
		public void Start() {
			List<string> listWarnings=new List<string>();
			int generation;
			bool startPollThread;
			lock(_lock) {
				if(_isDisposed) {
					throw new ObjectDisposedException(nameof(TriggerEngine));
				}
				if(_isRunning) {
					return;
				}
				_isRunning=true;
				_generation++;
				generation=_generation;
				if(!_hasStarted) {
					_hasStarted=true;
					if(_isStillEnabled) {
						if(_frameSource.IsStillPinConnected) {
							_stillState=TriggerInputState.Armed;
						}
						else {
							_stillState=TriggerInputState.Incomplete;
							_stillProblem=_frameSource.HasStillPin
								? "it is enabled, but the camera's still pin is not connected."
								: "it is enabled, but the camera has no still pin.";
							listWarnings.Add("["+CameraConfig.SectionTriggerStill+"] "+_stillProblem+" The still trigger is off.");
						}
					}
					foreach(PolledInput input in GetPolledInputs()) {
						if(input.State==TriggerInputState.Incomplete) {
							listWarnings.Add(input.Label+" "+input.Problem+" "+GetOffText(input));
						}
					}
				}
				foreach(PolledInput input in GetPolledInputs()) {
					//A restart reads afresh: a key held across Stop and Start must not fire.
					input.HasReading=false;
				}
				if(_stillState==TriggerInputState.Armed && !_isStillSubscribed) {
					_frameSource.StillCaptured+=FrameSource_StillCaptured;
					_isStillSubscribed=true;
				}
				startPollThread=_usePollThread && GetPolledInputs().Any(x => x.State==TriggerInputState.Armed);
				if(startPollThread) {
					_manualResetEventStop=new ManualResetEvent(false);
					_threadPoll=new Thread(PollLoop);
					_threadPoll.IsBackground=true;
					_threadPoll.Name="Camera trigger poll";
				}
			}
			if(startPollThread) {
				_threadPoll.Start(_manualResetEventStop);
			}
			foreach(string warning in listWarnings) {
				PostWarning(generation,warning);
			}
		}

		///<summary>Stops polling and the still trigger, and drops events not yet delivered. Waits up to 2 s for a poll in progress.
		///Safe to call more than once and from an event handler.</summary>
		public void Stop() {
			Thread threadPoll;
			ManualResetEvent manualResetEventStop;
			lock(_lock) {
				if(!_isRunning) {
					return;
				}
				_isRunning=false;
				_generation++;
				if(_isStillSubscribed) {
					_frameSource.StillCaptured-=FrameSource_StillCaptured;
					_isStillSubscribed=false;
				}
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
			bool isExceptionWarned=false;
			try {
				while(true) {
					try {
						PollOnce();
					}
					catch(Exception ex) {
						//A failure here must not end the process; the reads themselves report failures as statuses. Logged once.
						if(!isExceptionWarned) {
							isExceptionWarned=true;
							PostWarning(-1,"Camera trigger poll failed: "+ex.Message);
						}
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

		///<summary>One poll: reads each armed control once with Get and updates the trigger and the levels. Does nothing when the
		///engine is not running. Called by the poll thread every PollMs; tests call it directly.</summary>
		internal void PollOnce() {
			lock(_lockPoll) {
				List<ControlId> listControls;
				int generation;
				lock(_lock) {
					if(!_isRunning) {
						return;
					}
					generation=_generation;
					listControls=GetPolledInputs().Where(x => x.State==TriggerInputState.Armed).Select(x => x.Binding.Control).Distinct().ToList();
				}
				foreach(ControlId control in listControls) {
					UvcControlValue controlValue=_frameSource.ReadControl(control.Interface,control.Property);
					long nowMs=_funcNowMs();
					List<Action> listActions=new List<Action>();
					lock(_lock) {
						if(!_isRunning || generation!=_generation) {
							return;
						}
						foreach(PolledInput input in GetPolledInputs().Where(x => x.State==TriggerInputState.Armed && x.Binding.Control==control)) {
							if(controlValue.IsOk) {
								Evaluate(input,controlValue.Value,nowMs,listActions);
							}
							else {
								Fail(input,control,controlValue,listActions);
							}
						}
					}
					foreach(Action action in listActions) {
						action();
					}
				}
			}
		}

		///<summary>Updates one input from a reading. Called inside _lock; queues the events to raise after it.</summary>
		private void Evaluate(PolledInput input,int value,long nowMs,List<Action> listActions) {
			bool isActive=input.Binding.IsActive(value);
			bool hadReading=input.HasReading;
			bool wasActive=input.IsActive;
			input.HasReading=true;
			input.IsActive=isActive;
			if(input.Level==null) {
				if(hadReading && isActive && !wasActive && _debounceGate.TryAccept(nowMs)) {
					TriggerPressEventArgs args=new TriggerPressEventArgs(TriggerSource.ControlPoll,nowMs,null);
					int generation=_generation;
					listActions.Add(() => PostPress(generation,args));
				}
				return;
			}
			if(isActive!=wasActive) {
				LevelChangedEventArgs args=new LevelChangedEventArgs(input.Level.Number,isActive,input.Level.Action??LevelAction.PausePreview,GetIsPausePreviewActive());
				int generation=_generation;
				listActions.Add(() => PostLevelChanged(generation,args));
			}
		}

		///<summary>Turns an input off after a failed read. Called inside _lock. An active level becomes inactive, so a paused preview
		///resumes.</summary>
		private void Fail(PolledInput input,ControlId control,UvcControlValue controlValue,List<Action> listActions) {
			bool wasActive=input.IsActive;
			input.State=TriggerInputState.Failed;
			input.IsActive=false;
			string reason=controlValue.Status==UvcControlStatus.Unsupported
				? "the camera does not support "+control+"."
				: "reading "+control+" failed ("+controlValue.Status+", "+UvcErrors.FormatHResult(controlValue.HResult)+").";
			input.Problem=reason;
			int generation=_generation;
			string warning=input.Label+" "+reason+" "+GetOffText(input);
			listActions.Add(() => PostWarning(generation,warning));
			if(input.Level!=null && wasActive) {
				LevelChangedEventArgs args=new LevelChangedEventArgs(input.Level.Number,false,input.Level.Action??LevelAction.PausePreview,GetIsPausePreviewActive());
				listActions.Add(() => PostLevelChanged(generation,args));
			}
		}

		private static string GetOffText(PolledInput input) {
			return input.Level==null ? "The control trigger is off." : "The level input is off.";
		}

		private IEnumerable<PolledInput> GetPolledInputs() {
			if(_inputControlPoll!=null) {
				yield return _inputControlPoll;
			}
			foreach(PolledInput input in _listInputLevels) {
				yield return input;
			}
		}
		#endregion Polling

		#region Still pin
		private void FrameSource_StillCaptured(object sender,UvcStillEventArgs e) {
			long nowMs=_funcNowMs();
			int generation;
			lock(_lock) {
				if(!_isRunning || _stillState!=TriggerInputState.Armed) {
					return;
				}
				generation=_generation;
			}
			if(!_debounceGate.TryAccept(nowMs)) {
				return;
			}
			PostPress(generation,new TriggerPressEventArgs(TriggerSource.StillPin,nowMs,e?.Frame));
		}
		#endregion Still pin

		#region Dispatch
		private void PostPress(int generation,TriggerPressEventArgs args) {
			Post(generation,() => {
				bool isEnabled;
				try {
					Func<bool> funcIsConsumerEnabled=FuncIsConsumerEnabled;
					isEnabled=funcIsConsumerEnabled==null || funcIsConsumerEnabled();
				}
				catch {
					isEnabled=false;
				}
				if(isEnabled) {
					Pressed?.Invoke(this,args);
				}
				else {
					PressIgnored?.Invoke(this,args);
				}
			});
		}

		private void PostLevelChanged(int generation,LevelChangedEventArgs args) {
			Post(generation,() => LevelChanged?.Invoke(this,args));
		}

		///<summary>generation -1 delivers even after Stop.</summary>
		private void PostWarning(int generation,string message) {
			TriggerWarningEventArgs args=new TriggerWarningEventArgs(message);
			Post(generation,() => Warning?.Invoke(this,args));
		}

		///<summary>Runs action on the captured context, or inline without one, unless the engine stopped or restarted since generation.</summary>
		private void Post(int generation,Action action) {
			SendOrPostCallback sendOrPostCallback=state => {
				if(generation!=-1) {
					lock(_lock) {
						if(generation!=_generation || !_isRunning) {
							return;
						}
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

		///<summary>The control-poll trigger or one level input, with its state.</summary>
		private sealed class PolledInput {
			///<summary>"[Trigger.ControlPoll]" or "[Level.1]", for log text.</summary>
			public readonly string Label;
			///<summary>Null when the values are incomplete.</summary>
			public readonly ControlBinding Binding;
			///<summary>Null for the trigger.</summary>
			public readonly LevelInputConfig Level;
			public TriggerInputState State=TriggerInputState.Armed;
			public string Problem="";
			public bool HasReading;
			public bool IsActive;

			public PolledInput(string label,ControlBinding binding,LevelInputConfig level) {
				Label=label;
				Binding=binding;
				Level=level;
			}

			public void SetIncompleteIfNeeded(string problem) {
				if(Binding!=null && problem=="") {
					return;
				}
				State=TriggerInputState.Incomplete;
				Problem=problem=="" ? "it is enabled, but its values are incomplete." : problem;
			}
		}
	}
}
