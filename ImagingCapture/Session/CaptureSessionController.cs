// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using ImagingCapture.Dicom;
using ImagingCapture.Metadata;
using ImagingCapture.Save;
using ImagingCapture.UI;
using ImagingCapture.Video;
using ImagingDeviceManager.Capture;
using OpenDentBusiness;

namespace ImagingCapture.Session {
	///<summary>The logic of the capture session window (design 7.3-7.7, 7.12, 10.2, 10.3, 10.8), with no window types, so that it can be tested
	///with a stand-in TWAIN session, store, clock and dispatcher. The window (FrmCaptureSession) shows its properties and calls its commands;
	///this class drives the ITwainCaptureSession and saves through RadiographImport.
	///
	///Threading: create it, call it and read it on the UI thread only. ITwainCaptureSession raises its events on its worker thread; the
	///controller posts each one to the UI thread through ICaptureDispatcher and handles it there, so every save and database write runs on
	///the UI thread. Open blocks until the source is open, so it runs through ICaptureDispatcher.RunInBackground and posts its result back.
	///Changed is raised (UI thread) after anything the window shows changed; Version changes with it.
	///
	///Safety rules kept here:
	///- Starting needs a deliberate action: RequestStart (the on-screen button) or the Confirm key held about 1 s (ConfirmHoldMs). Session keys
	///  never start anything. Key presses in the first 2 s after the banner appears (KeyIgnoreMs) are ignored, and the Start button is not armed
	///  in those 2 s either (CanStart), because a key press can activate a focused button. The same holds for every re-enable after a disable.
	///- getPatNumCur() is checked before every enable; a mismatch refuses the enable.
	///- Every image is saved for the bound patient, into the target its enable was for (CaptureEnableRecord), never one chosen later.
	///- Images that cannot be placed go to the quarantine (TwainCaptureSession.QuarantineImage), tagged with the bound patient.
	///- No message boxes: everything is a property the window shows in its indicator area.
	///
	///Extension points (partial methods, implemented in other files of this partial class by CAP-4 implementer A2): ExecuteExt and
	///CanExecuteExt (Retake, Rotate), OnImageReceivedExt (redelivered images, the patient-change monitor), OnImageSavedExt, OnImageQuarantinedExt,
	///OnEnableEndedExt (capture events of design 7.13, redelivery, idle and patient-change stops), OnEnablePostedExt and OnTickExt (idle
	///disable), OnStatusExt and OnFinishingExt (the session's audit entry), OnBeginExt, OnDeliberateStartExt and OnFinishStartingExt (the
	///patient-change monitor, the forced close). StopPlacement stops placing images at once (a patient change). The rules themselves are in
	///CaptureSessionController.Retake.cs, .Monitor.cs, .Redelivery.cs and .Audit.cs.</summary>
	public sealed partial class CaptureSessionController {
		#region Fields
		///<summary>Key presses in the first 2 s after the banner appears are ignored (design 7.3 step 2).</summary>
		public const int KeyIgnoreMs=2000;
		///<summary>The Confirm key starts when held about 1 s (design 7.3 step 2).</summary>
		public const int ConfirmHoldMs=1000;
		///<summary>The second press of End or Disable after the unsafe-stop warning counts within this time.</summary>
		public const int StopConfirmMs=10000;
		private readonly CaptureSessionSetup _setup;
		private readonly CaptureSessionServices _services;
		private readonly ICaptureDispatcher _dispatcher;
		private readonly ICaptureClock _clock;
		private readonly IRadiographStore _radiographStore;
		private readonly RadiographImport _radiographImport;
		private readonly MountSlots _mountSlots;
		private ITwainCaptureSession _session;
		private bool _isSessionOpen;
		private CaptureEnableRecord _enableRunning;
		private long _msReadyShown;
		///<summary>When the Confirm key went down in this press, or null.</summary>
		private long? _msConfirmDown;
		///<summary>The Confirm key went down while presses were ignored: this press never confirms, until it is released.</summary>
		private bool _isConfirmPressIgnored;
		private bool _isStarted;
		private bool _isEndRequested;
		private bool _isPausedBySaveFailure;
		///<summary>The target the operator chose while an enable ran for another one; the target after that enable's image is placed.</summary>
		private int? _idxTargetChosen;
		private readonly HashSet<int> _hashSetSkipped=new HashSet<int>();
		private readonly HashSet<string> _hashSetReasonsLogged=new HashSet<string>();
		///<summary>The command whose unsafe-stop warning shows, and when; the second press within StopConfirmMs goes ahead.</summary>
		private CaptureSessionAction? _actionStopPending;
		private long _msStopPending;
		private bool _isPlacementStopped;
		private string _placementStoppedReason="";
		private string _toothNumbersSingle="";
		private readonly List<Document> _listDocumentsSingle=new List<Document>();
		private readonly List<long> _listDocNums=new List<long>();
		private readonly List<string> _listQuarantinePaths=new List<string>();
		private readonly List<string> _listLog=new List<string>();
		private DateTime _dateTimeStarted;
		private string _indicatorText="";
		///<summary>Every enable posted, for the effective settings of the summary (design 9.3).</summary>
		private readonly List<CaptureEnableRecord> _listEnablesPosted=new List<CaptureEnableRecord>();
		///<summary>The first reason the source gave 8 bits where 16 were asked (TwainCaptureEnded.Reason8Bit), or "".</summary>
		private string _reason8BitDelivered="";
		#endregion Fields

		#region Constructor
		///<summary>setup: what the session is bound to (CaptureSessionLauncher resolves it). services: the TWAIN session factory, the stores, the
		///clock and the log. dispatcher: the UI thread of the window.</summary>
		public CaptureSessionController(CaptureSessionSetup setup,CaptureSessionServices services,ICaptureDispatcher dispatcher) {
			_setup=setup??throw new ArgumentNullException(nameof(setup));
			_services=services??throw new ArgumentNullException(nameof(services));
			_dispatcher=dispatcher??throw new ArgumentNullException(nameof(dispatcher));
			if(setup.Patient==null || setup.Patient.PatNum==0) {
				throw new ArgumentException("The capture session needs a patient.",nameof(setup));
			}
			if(setup.ImagingDevice==null) {
				throw new ArgumentException("The capture session needs an imaging device.",nameof(setup));
			}
			if(setup.Mount!=null && setup.MountSlots==null) {
				throw new ArgumentException("A mount needs its slots.",nameof(setup));
			}
			_clock=services.Clock??new SystemCaptureClock();
			_radiographStore=services.RadiographStore??new OdRadiographStore();
			_radiographImport=new RadiographImport(_radiographStore,() => _clock.Now);
			_mountSlots=setup.Mount==null ? null : setup.MountSlots;
			_patientBinding=new PatientBinding(setup.PatNum);
			ProvNum=setup.ProvNumDefault;
			ProviderAbbr=GetProviderAbbr(ProvNum);
			UnassignedText=setup.UnassignedText;
			IsUnassignedOverdue=setup.IsUnassignedOverdue;
			_indicatorText=CaptureSessionText.ReadyStart;
		}
		#endregion Constructor

		#region Events
		///<summary>Something the window shows changed (UI thread).</summary>
		public event Action Changed;
		///<summary>Play this [Sounds] sound (UI thread).</summary>
		public event Action<CaptureSound> SoundRequested;
		///<summary>A line of the session's log (UI thread). The controller's own lines also go to Capture\capture-log.txt.</summary>
		public event Action<string> Logged;
		///<summary>The session finished: show the summary, then close (UI thread). Raised once.</summary>
		public event Action<CaptureSessionSummary> SessionFinished;
		#endregion Events

		#region Properties
		public CaptureSessionPhase Phase { get; private set; }=CaptureSessionPhase.Created;
		public CaptureIndicatorState IndicatorState { get; private set; }=CaptureIndicatorState.Ready;
		///<summary>The indicator's text (design 7.5, 7.12).</summary>
		public string IndicatorText => _indicatorText;
		///<summary>The target in words: "3 of 18: Max R, #1,2,3", "Single image, #8,9", or "Every slot has an image".</summary>
		public string TargetText {
			get {
				if(IsSingleTarget) {
					return CaptureTargetText.GetSingleText(_toothNumbersSingle);
				}
				MountItem mountItemTarget=_mountSlots.Target;
				return mountItemTarget==null ? "Every slot has an image" : CaptureTargetText.GetSlotText(_mountSlots.ListMountItems,mountItemTarget);
			}
		}
		///<summary>Design 7.11, while an enable runs for a target other than the one chosen: "Next image: 4 of 18: ..., then 7 of 18: ...". "" otherwise.</summary>
		public string NextTargetText { get; private set; }="";
		///<summary>Design 7.12 "Save fails": the red strip "Could not save: &lt;message&gt;". "" when none.</summary>
		public string RedStripText { get; private set; }="";
		///<summary>Design 7.6: the red bar "Patient changed in the main window. This session is still for &lt;name&gt;." "" when none.</summary>
		public string PatientChangedText { get; private set; }="";
		///<summary>A notice that does not stop the session: the extra-images notice of design 7.12, a saver's warning. "" when none.</summary>
		public string NoticeText { get; private set; }="";
		///<summary>Design 7.6: the warning before an unsafe stop, until the second press or the next command. "" when none.</summary>
		public string WarningText { get; private set; }="";
		///<summary>"N unassigned captures on this computer" (CaptureQuarantine.GetUnassignedText), or null when the quarantine is empty.</summary>
		public string UnassignedText { get; private set; }
		///<summary>UnassignedText has turned into the warning (the oldest is more than 30 days old, design 7.12 "Visibility"): the window shows it
		///in a warning colour.</summary>
		public bool IsUnassignedOverdue { get; private set; }
		///<summary>The gated capture settings are the ones last saved in the Capture settings screen (design 9.3 hash check). The launcher starts a
		///session only then; a session begun without it never enables (the safe value of [Session] Enabled is off).</summary>
		public bool IsSettingsVerified => _setup.SettingsCheck!=null && _setup.SettingsCheck.IsVerified;
		///<summary>Why the settings are not verified ("Capture settings were changed outside Open Dental; review them in Setup. ..."), or "".</summary>
		public string SettingsNoticeText => IsSettingsVerified ? ""
			: (_setup.SettingsCheck==null ? "The capture settings were not checked against the ones saved in Capture settings; review them in Setup."
				: _setup.SettingsCheck.Text);
		///<summary>The target mount: "FMX 18, 10/31/2026". "" in single-target mode.</summary>
		public string MountText => _setup.Mount==null ? "" : CaptureSessionText.GetMountText(_setup.Mount.Description,_setup.Mount.DateCreated);
		///<summary>Phase ConfirmingOlderMount: the question to show.</summary>
		public string OlderMountText => _setup.Mount==null ? "" : CaptureSessionText.GetOlderMountText(_setup.Mount.Description,_setup.Mount.DateCreated);
		///<summary>The mount is dated before today (design 7.3 step 1).</summary>
		public bool IsMountOlderThanToday => _setup.Mount!=null && _setup.Mount.DateCreated.Date<_clock.Now.Date;
		///<summary>What a deliberate start does (Phase Ready).</summary>
		public CaptureStartKind StartKind { get; private set; }=CaptureStartKind.Start;
		///<summary>The Start button's text: "Start X-rays for Pat Test", "Re-enable for Pat Test", "Retry", "Take another".</summary>
		public string StartText => CaptureSessionText.GetStartText(StartKind,PatientName);
		///<summary>The Start button may be pressed now: Phase Ready, something to start, and 2 s since the banner appeared. The window calls OnTick
		///so that it notices when the 2 s are over.</summary>
		public bool CanStart => Phase==CaptureSessionPhase.Ready && StartKind!=CaptureStartKind.None && IsBannerArmed(_clock.NowMs)
			&& !HasRedeliveredPending;
		///<summary>The provider can be changed (design 10.8: editable before Start).</summary>
		public bool CanEditProvider => !_isStarted && Phase!=CaptureSessionPhase.Finished;
		///<summary>The single-target tooth numbers can be changed (between images).</summary>
		public bool CanEditToothNumbers => IsSingleTarget && (Phase==CaptureSessionPhase.Ready || Phase==CaptureSessionPhase.Created);
		///<summary>The provider for Document.ProvNum and Mount.ProvNum.</summary>
		public long ProvNum { get; private set; }
		public string ProviderAbbr { get; private set; }="";
		///<summary>Single-target mode: the banner's tooth numbers for the next image.</summary>
		public string ToothNumbers => _toothNumbersSingle;
		///<summary>No mount: each image is a separate document (design 7.3 step 2, 10.3).</summary>
		public bool IsSingleTarget => _mountSlots==null;
		public Patient Patient => _setup.Patient;
		public long PatNum => _setup.PatNum;
		public string PatientName => _setup.PatientName;
		public ImagingDevice ImagingDevice => _setup.ImagingDevice;
		public Mount Mount => _setup.Mount;
		public MountDef MountDef => _setup.MountDef;
		public Guid SessionGuid => _setup.SessionGuid;
		public TwainCaptureOptions Options => _setup.Options;
		public CaptureSessionConfig SessionConfig => _setup.SessionConfig;
		///<summary>The mount's items, documents and target; null in single-target mode.</summary>
		public MountSlots MountSlots => _mountSlots;
		///<summary>Images saved as documents.</summary>
		public int CountImages => _listDocNums.Count;
		///<summary>Retakes; CAP-4 implementer A2 counts them.</summary>
		public int CountRetakes { get; private set; }
		///<summary>Images moved to the quarantine in this session.</summary>
		public int CountQuarantined => _listQuarantinePaths.Count;
		///<summary>The image saved last, its document and its slot (null in single-target mode), for the window's brief preview.</summary>
		public AcquiredImage LastSavedImage { get; private set; }
		public Document LastSavedDocument { get; private set; }
		public MountItem LastSavedMountItem { get; private set; }
		///<summary>The enable that runs, or null.</summary>
		public CaptureEnableRecord EnableRunning => _enableRunning;
		///<summary>The source is enabled (an enable is posted or running): the window registers the [Keys] hotkeys now and releases them when it
		///turns false (design 7.3 step 3).</summary>
		public bool AreSessionKeysActive => _enableRunning!=null;
		///<summary>The last enable ended with CAP_DEVICEONLINE FALSE: "Sensor not connected" and Retry (design 7.7).</summary>
		public bool IsDeviceOffline { get; private set; }
		///<summary>Images are no longer placed (a patient change); everything that arrives goes to the quarantine.</summary>
		public bool IsPlacementStopped => _isPlacementStopped;
		///<summary>Set when Phase is Finished.</summary>
		public CaptureSessionSummary Summary { get; private set; }
		///<summary>Changes whenever something the window shows changed.</summary>
		public int Version { get; private set; }
		public IReadOnlyList<string> ListLog => _listLog;
		#endregion Properties

		#region Methods - commands (UI thread)
		///<summary>The window has loaded and shows the banner. With a mount dated before today the session first asks to confirm it.</summary>
		public void Begin() {
			if(Phase!=CaptureSessionPhase.Created) {
				return;
			}
			_dateTimeStarted=_clock.Now;
			_msReadyShown=_clock.NowMs;//the banner appears now: keys are ignored for 2 s, also while the older mount is asked about
			Log("Capture session "+SessionGuid+" for patient "+PatNum+", device "+_setup.ImagingDevice.ImagingDeviceNum+" ("
				+(_setup.ImagingDevice.Description??"")+"), "+(IsSingleTarget ? "single-target mode" : "mount "+_setup.Mount.MountNum)+".");
			OnBeginExt();
			if(!IsSettingsVerified) {
				//Design 9.3: settings that are not verified mean the safe values, and the safe value of [Session] Enabled is off. The launcher does
				//not open the window then; this holds for any other way into the session too. The indicator says why, and nothing can start.
				Log("Not started: "+SettingsNoticeText);
				ShowReady(CaptureStartKind.None,CaptureIndicatorState.Error,SettingsNoticeText);
			}
			else if(IsMountOlderThanToday) {
				Phase=CaptureSessionPhase.ConfirmingOlderMount;
				SetIndicator(CaptureIndicatorState.Ready,OlderMountText);
			}
			else {
				ShowReady(CaptureStartKind.Start,CaptureIndicatorState.Ready,CaptureSessionText.ReadyStart);
			}
			NotifyChanged();
		}

		///<summary>The operator confirmed the older mount as the target.</summary>
		public void ConfirmOlderMount() {
			if(Phase!=CaptureSessionPhase.ConfirmingOlderMount) {
				return;
			}
			Log("The operator confirmed mount "+_setup.Mount.MountNum+", dated "+_setup.Mount.DateCreated.ToString("yyyy-MM-dd")+", as the target.");
			ShowReady(CaptureStartKind.Start,CaptureIndicatorState.Ready,CaptureSessionText.ReadyStart);
			NotifyChanged();
		}

		///<summary>The operator declined the older mount: the session ends with nothing captured.</summary>
		public void DeclineOlderMount() {
			if(Phase!=CaptureSessionPhase.ConfirmingOlderMount) {
				return;
			}
			Log("The operator declined mount "+_setup.Mount.MountNum+" as the target.");
			Finish(CaptureSessionEndReason.CancelledBeforeStart);
		}

		///<summary>Design 10.8: the banner's provider, before Start. False when it can no longer be changed.</summary>
		public bool SetProvider(long provNum) {
			if(!CanEditProvider || provNum<0) {
				return false;
			}
			ProvNum=provNum;
			ProviderAbbr=GetProviderAbbr(provNum);
			NotifyChanged();
			return true;
		}

		///<summary>Single-target mode: the banner's tooth numbers (Universal, separated by commas) for the next image. False, with error, when
		///they cannot be read or cannot be changed now.</summary>
		public bool SetToothNumbers(string toothNumbers,out string error) {
			error="";
			if(!CanEditToothNumbers) {
				error="The tooth numbers can be changed only between images, without a mount.";
				return false;
			}
			List<ToothInfo> listTeeth;
			if(!Dicom.ToothNumbers.TryParseList(toothNumbers,out listTeeth,out error)) {
				return false;
			}
			_toothNumbersSingle=string.Join(",",listTeeth.Select(x => x.Universal));
			NotifyChanged();
			return true;
		}

		///<summary>The on-screen Start button (whatever StartText says). False when it is not armed (CanStart) or the start was refused.</summary>
		public bool RequestStart() {
			if(!CanStart) {
				Log("Start ignored: "+(Phase!=CaptureSessionPhase.Ready ? "the session is "+Phase+"." : "the banner appeared less than 2 s ago."));
				return false;
			}
			bool isStarted=StartDeliberate();
			NotifyChanged();
			return isStarted;
		}

		///<summary>A key or pedal went down; timestampMs on ICaptureClock.NowMs. isRepeat: an auto-repeat key-down while the key is held (WPF
		///KeyEventArgs.IsRepeat). The Confirm key starts when held ConfirmHoldMs from a key-down this controller saw; a press whose first key-down
		///came while keys were ignored, or before the banner showed (only repeats arrive), never starts. A session key runs its command once per
		///press. Every key is ignored in the first KeyIgnoreMs after the banner appeared. True when it did something.</summary>
		public bool OnKeyDown(CaptureSessionKey key,long timestampMs,bool isRepeat) {
			NoteActivity();//a pedal or a hotkey is activity Open Dental does not see (design 7.1)
			bool isBannerShowing=Phase==CaptureSessionPhase.Ready || Phase==CaptureSessionPhase.ConfirmingOlderMount;
			if(isBannerShowing && !IsBannerArmed(timestampMs)) {
				if(key==CaptureSessionKey.Confirm) {
					_isConfirmPressIgnored=true;
					_msConfirmDown=null;
				}
				return false;//design 7.3 step 2: ignored, not queued
			}
			if(key==CaptureSessionKey.Confirm) {
				if(Phase!=CaptureSessionPhase.Ready || _isConfirmPressIgnored) {
					return false;
				}
				if(_msConfirmDown==null) {
					if(isRepeat) {
						_isConfirmPressIgnored=true;//held since before the banner was armed: this press never confirms
						return false;
					}
					_msConfirmDown=timestampMs;
					return false;
				}
				return TryConfirmByKey(timestampMs);
			}
			if(isRepeat) {
				return false;
			}
			bool isDone=Execute(ToAction(key));
			return isDone;
		}

		///<summary>A first key-down (not a repeat) at timestampMs.</summary>
		public bool OnKeyDown(CaptureSessionKey key,long timestampMs) {
			return OnKeyDown(key,timestampMs,false);
		}

		public bool OnKeyDown(CaptureSessionKey key) {
			return OnKeyDown(key,_clock.NowMs,false);
		}

		///<summary>A key or pedal went up. A Confirm key held ConfirmHoldMs starts on release too, when no later key-down or tick came first.</summary>
		public bool OnKeyUp(CaptureSessionKey key,long timestampMs) {
			if(key!=CaptureSessionKey.Confirm) {
				return false;
			}
			bool isDone=_msConfirmDown!=null && !_isConfirmPressIgnored && TryConfirmByKey(timestampMs);
			_msConfirmDown=null;
			_isConfirmPressIgnored=false;
			return isDone;
		}

		public bool OnKeyUp(CaptureSessionKey key) {
			return OnKeyUp(key,_clock.NowMs);
		}

		///<summary>The window's timer (about every 100 ms while it is open): the held Confirm key, the end of the 2 s in which the Start button is not
		///armed, and the timers of the extensions.</summary>
		public void OnTick() {
			long nowMs=_clock.NowMs;
			bool canStartBefore=_canStartShown;
			if(_msConfirmDown!=null && !_isConfirmPressIgnored && Phase==CaptureSessionPhase.Ready) {
				TryConfirmByKey(nowMs);
			}
			OnTickExt(nowMs);
			if(CanStart!=canStartBefore) {
				NotifyChanged();
			}
		}

		///<summary>Runs an operator command (a button, a key or a pedal). False when it is not available now.</summary>
		public bool Execute(CaptureSessionAction action) {
			if(Phase==CaptureSessionPhase.Finished) {
				return false;
			}
			bool isHandled=false;
			ExecuteExt(action,ref isHandled);
			if(isHandled) {
				NotifyChanged();
				return true;
			}
			if(action!=CaptureSessionAction.End && action!=CaptureSessionAction.Disable) {
				ClearStopWarning();
			}
			bool isDone;
			switch(action) {
				case CaptureSessionAction.Skip:
					isDone=SkipTarget();
					break;
				case CaptureSessionAction.NextSlot:
					isDone=MoveTarget(1);
					break;
				case CaptureSessionAction.PreviousSlot:
					isDone=MoveTarget(-1);
					break;
				case CaptureSessionAction.Disable:
					isDone=DisableByOperator();
					break;
				case CaptureSessionAction.End:
					isDone=EndByOperator();
					break;
				default:
					isDone=false;//Retake and Rotate come with ExecuteExt
					break;
			}
			NotifyChanged();
			return isDone;
		}

		///<summary>The command is available now, for the window's buttons.</summary>
		public bool CanExecute(CaptureSessionAction action) {
			if(Phase==CaptureSessionPhase.Finished) {
				return false;
			}
			bool canExecute;
			switch(action) {
				case CaptureSessionAction.Skip:
				case CaptureSessionAction.NextSlot:
				case CaptureSessionAction.PreviousSlot:
					canExecute=!IsSingleTarget && _mountSlots.Target!=null && _mountSlots.GetEmptySlots().Count>1;
					break;
				case CaptureSessionAction.Disable:
					canExecute=Phase==CaptureSessionPhase.Enabled && _enableRunning!=null;
					break;
				case CaptureSessionAction.End:
					canExecute=true;
					break;
				default:
					canExecute=false;
					break;
			}
			CanExecuteExt(action,ref canExecute);
			return canExecute;
		}

		///<summary>Tap a slot of the mini-map: an empty slot becomes the target. False for a filled slot, a text item or in single-target mode.</summary>
		public bool SelectSlot(int idx) {
			if(IsSingleTarget || Phase==CaptureSessionPhase.Finished) {
				return false;
			}
			ClearStopWarning();
			bool isDone=ChangeTarget(idx,isSkip:false);
			NotifyChanged();
			return isDone;
		}

		///<summary>Every capture slot of the mount in ItemOrder, for the mini-map. Empty in single-target mode.</summary>
		public List<CaptureSlotView> GetSlotViews() {
			List<CaptureSlotView> listViews=new List<CaptureSlotView>();
			if(IsSingleTarget) {
				return listViews;
			}
			for(int i=0;i<_mountSlots.ListMountItems.Count;i++) {
				MountItem mountItem=_mountSlots.ListMountItems[i];
				if(!MountPlacement.IsSlot(mountItem)) {
					continue;
				}
				listViews.Add(new CaptureSlotView {
					Idx=i,
					MountItem=mountItem,
					Label=MountPlacement.GetSlotLabel(mountItem),
					Document=_mountSlots.ListDocuments[i],
					IsTarget=i==_mountSlots.IdxTarget,
					IsEnableTarget=_enableRunning!=null && GetNextImageIdx(_enableRunning)==i,
					IsSkipped=_hashSetSkipped.Contains(i),
				});
			}
			return listViews.OrderBy(x => x.MountItem.ItemOrder).ToList();
		}

		///<summary>The window is closing (after the summary, or forced: FormClosing, a logoff). A session that has not finished stops the source in
		///the safe order on its worker (BeginClose; later images go to the quarantine) and finishes. Never waits for the worker.</summary>
		public void OnClosing() {
			if(Phase==CaptureSessionPhase.Finished) {
				return;
			}
			Log("The window closed before the session finished.");
			Finish(CaptureSessionEndReason.Closed);
		}

		///<summary>Stops placing images at once: everything that arrives from now on goes to the quarantine with reason (design 7.6, a patient
		///change). For CAP-4 implementer A2's patient monitor.</summary>
		private void StopPlacement(string reason) {
			_isPlacementStopped=true;
			_placementStoppedReason=reason??"";
			Log("Placement stopped: "+_placementStoppedReason);
		}
		#endregion Methods - commands (UI thread)

		#region Methods - start and enable
		private bool _canStartShown;

		private bool IsBannerArmed(long nowMs) {
			return nowMs-_msReadyShown>=KeyIgnoreMs;
		}

		private bool TryConfirmByKey(long nowMs) {
			if(_msConfirmDown==null || nowMs-_msConfirmDown.Value<ConfirmHoldMs || !CanStart) {
				return false;
			}
			_msConfirmDown=null;
			_isConfirmPressIgnored=true;//the same press never starts twice; released by key-up
			Log("Started with the Confirm key held "+ConfirmHoldMs+" ms.");
			bool isStarted=StartDeliberate();
			NotifyChanged();
			return isStarted;
		}

		///<summary>A deliberate start or re-enable: the patient check, then Open (first time, or after a failed open) or Enable.</summary>
		private bool StartDeliberate() {
			if(!CheckPatient("start")) {
				return false;
			}
			OnDeliberateStartExt();
			if(StartKind==CaptureStartKind.TakeSkipped) {
				_hashSetSkipped.Clear();
			}
			PatientChangedText="";
			_isStarted=true;
			_isPausedBySaveFailure=false;
			IsDeviceOffline=false;
			RedStripText="";
			NoticeText="";
			ClearStopWarning();
			if(!_isSessionOpen) {
				OpenSession();
				return true;
			}
			return EnableNext();
		}

		///<summary>design 6.4, 10.6: the main window still shows the bound patient. Otherwise nothing is enabled, and the red bar shows.</summary>
		private bool CheckPatient(string what) {
			long patNumCur;
			try {
				patNumCur=_setup.FuncGetPatNumCur==null ? 0 : _setup.FuncGetPatNumCur();
			}
			catch(Exception ex) {
				Log("The main window's patient could not be read: "+ex.Message);
				patNumCur=0;
			}
			if(patNumCur==PatNum) {
				return true;
			}
			Log("Refused to "+what+": the main window shows patient "+patNumCur+", the session is for "+PatNum+".");
			PatientChangedText=CaptureSessionText.GetPatientChangedText(PatientName);
			PlaySound(CaptureSound.Error);
			ShowReady(_isStarted ? GetReEnableKind() : CaptureStartKind.Start,CaptureIndicatorState.Error,"The sensor software was not started.");
			return false;
		}

		private void OpenSession() {
			if(_session!=null) {//a failed open: start over with a new worker; the old one's events are ignored (each handler checks _session)
				_session.BeginClose();
			}
			ITwainCaptureSession session=_services.FuncCreateSession();
			session.SessionGuid=SessionGuid;
			session.QuarantineContext=new TwainCaptureQuarantineContext(PatNum,PatientName);//always the bound patient (design 7.12)
			session.FuncReadMetadataKeys=CaptureMetadataParser.ReadKeys;
			SubscribeSession(session);
			_session=session;
			Phase=CaptureSessionPhase.Opening;
			SetIndicator(CaptureIndicatorState.Connecting,CaptureSessionText.Opening);
			string twainName=_setup.ImagingDevice.TwainName??"";
			TwainCaptureOptions options=_setup.Options.Clone();
			Log("Opening \""+twainName+"\".");
			_dispatcher.RunInBackground(() => {
				TwainCaptureOpenResult result;
				try {
					result=session.Open(twainName,options)??new TwainCaptureOpenResult { Error=TwainCaptureOpenError.Other };
				}
				catch(Exception ex) {
					result=new TwainCaptureOpenResult { Error=TwainCaptureOpenError.Other,ErrorText=ex.Message };
				}
				_dispatcher.Post(() => OnOpenFinished(session,result));
			});
		}

		private void OnOpenFinished(ITwainCaptureSession session,TwainCaptureOpenResult result) {
			if(session!=_session || Phase==CaptureSessionPhase.Finished) {
				if(result!=null && result.IsOpen) {
					//The session finished (a forced close) or started over while Open still ran, for example while it waited for the previous
					//window's source to finish closing. Its BeginClose found no worker yet, so this source would stay open in state 4 for good, and
					//every later session of the device would find it open in another window (design 7.3 step 7: a forced close closes the source).
					Log("The sensor software opened after the session "+(session!=_session ? "started over" : "ended")+"; it is closed now.");
					try {
						session.BeginClose();
					}
					catch(Exception ex) {
						Log("The sensor software could not be closed: "+ex.Message);
					}
				}
				return;
			}
			if(result.IsOpen) {
				_isSessionOpen=true;
				TwainCaptureSourceInfo sourceInfo=result.SourceInfo;
				Log("Open: \""+(sourceInfo?.ProductName??"")+"\""+(sourceInfo?.DeviceOnline==false ? ", CAP_DEVICEONLINE FALSE" : "")+".");
				if(_isEndRequested) {
					Finish(CaptureSessionEndReason.Ended);
					return;
				}
				EnableNext();
				NotifyChanged();
				return;
			}
			string text=CaptureEndText.GetOpenText(result,_setup.ImagingDevice.TwainName,_setup.Options.OpenTimeout);
			Log("Open failed ("+result.Error+"): "+text);
			if(_isEndRequested) {
				Finish(CaptureSessionEndReason.Ended);
				return;
			}
			PlaySound(CaptureSound.Error);
			//Design 7.12: the source gate's refusal is final here; everything else may be retried by the operator.
			CaptureStartKind startKind=result.Error==TwainCaptureOpenError.SourceNotEnabledHere ? CaptureStartKind.None : CaptureStartKind.Retry;
			ShowReady(startKind,CaptureIndicatorState.Error,text);
			NotifyChanged();
		}

		///<summary>Enables for the target: the patient check, the bit depth, then TwainCaptureSession.Enable. Finishes when the mount is full.</summary>
		private bool EnableNext() {
			return PostEnable(false);
		}

		///<summary>EnableNext, or with isRedelivery a redelivery enable (W15, design 7.12: TwainCaptureSession.RequestRedelivery) for the same
		///target. Neither goes ahead while placement is stopped (a patient change) or redelivered images wait for the operator.</summary>
		private bool PostEnable(bool isRedelivery) {
			if(_isEndRequested) {
				Finish(CaptureSessionEndReason.Ended);
				return false;
			}
			if(!IsSingleTarget && _mountSlots.IdxTarget==-1) {
				Finish(CaptureSessionEndReason.MountFull);
				return false;
			}
			if(!CheckPatient(isRedelivery ? "request the undelivered images" : "enable")) {
				return false;
			}
			if(_isPlacementStopped) {
				//Design 7.6: after a patient change only a deliberate re-enable (which passed the patient check) resumes the session.
				Log("Not enabled again: placement is stopped ("+_placementStoppedReason+").");
				ShowReady(GetReEnableKind(),CaptureIndicatorState.Disabled,CaptureSessionText.NotEnabledAfterPatientChange);
				return false;
			}
			if(HasRedeliveredPending) {
				//Design 7.12: the redelivered images are placed (or not) before anything else; their target is the slot an enable would take.
				Log("Not enabled: redelivered images wait for the operator.");
				ShowReady(GetReEnableKind(),CaptureIndicatorState.Ready,CaptureSessionText.RedeliveredConfirmFirst);
				return false;
			}
			CaptureEnableRecord record=new CaptureEnableRecord();
			record.PostedAt=_clock.Now;
			record.IsRedelivery=isRedelivery;
			if(IsSingleTarget) {
				record.IdxTarget=-1;
				record.ToothNumbers=_toothNumbersSingle;
			}
			else {
				record.IdxTarget=_mountSlots.IdxTarget;
				record.MountItemTarget=_mountSlots.Target;
			}
			RadiographTarget radiographTarget=GetRadiographTarget(record.MountItemTarget,record.ToothNumbers);
			record.BitDepth=CaptureBitDepthDecision.Decide(new CaptureBitDepthInput {
				Options=_setup.Options,
				Record=_setup.VerificationRecord,
				ImagingDeviceNum=_setup.ImagingDevice.ImagingDeviceNum,
				ComputerName=_setup.ComputerName,
				TwainName=_setup.ImagingDevice.TwainName,
				GatedSettingsHash=_setup.GatedSettingsHash,
				SettingsCheck=_setup.SettingsCheck,
				Target=radiographTarget.ToIntraOralTarget(),
			});
			if(record.BitDepth.Reason8Bit!=null) {
				LogOnce(record.BitDepth.Reason8Bit);
			}
			TwainEnableRequest request=new TwainEnableRequest {
				Bits=record.BitDepth.Bits,
				Tag=record,
				TargetLabel=radiographTarget.GetLabel(),
				SidecarFields=GetSidecarFields(record),
			};
			int enableId=isRedelivery ? _session.RequestRedelivery(request) : _session.Enable(request);
			if(enableId==0) {
				_isSessionOpen=false;
				Log("Enable refused: the session is not open.");
				PlaySound(CaptureSound.Error);
				ShowReady(CaptureStartKind.Retry,CaptureIndicatorState.Error,CaptureSessionText.NotOpen);
				return false;
			}
			record.EnableId=enableId;
			_enableRunning=record;
			_listEnablesPosted.Add(record);
			_idxTargetChosen=null;
			NextTargetText="";
			Phase=CaptureSessionPhase.Enabled;
			SetIndicator(CaptureIndicatorState.Connecting,CaptureSessionText.Starting);
			NoteActivity();
			OnEnablePostedExt(record);
			return true;
		}

		private RadiographTarget GetRadiographTarget(MountItem mountItem,string toothNumbers) {
			if(IsSingleTarget) {
				return new RadiographTarget { ToothNumbers=toothNumbers??"" };
			}
			return new RadiographTarget { Mount=_setup.Mount,MountItem=mountItem };
		}

		///<summary>The banner shows again and waits for a deliberate start (startKind), with the indicator as given.</summary>
		private void ShowReady(CaptureStartKind startKind,CaptureIndicatorState indicatorState,string indicatorText) {
			Phase=CaptureSessionPhase.Ready;
			StartKind=startKind;
			_msReadyShown=_clock.NowMs;
			_msConfirmDown=null;
			_isConfirmPressIgnored=false;
			NextTargetText="";
			SetIndicator(indicatorState,indicatorText);
		}
		#endregion Methods - start and enable

		#region Methods - targets
		///<summary>Skip (design 7.3 step 5): the target is left empty for now and the next empty slot that was not skipped becomes the target.</summary>
		private bool SkipTarget() {
			if(IsSingleTarget || _mountSlots.IdxTarget==-1) {
				return false;
			}
			int idxCurrent=_mountSlots.IdxTarget;
			List<int> listIdxEmpty=GetEmptyIdxs().Where(x => x!=idxCurrent).ToList();
			if(listIdxEmpty.Count==0) {
				return false;
			}
			int idxNew=listIdxEmpty[0];
			foreach(int idx in listIdxEmpty) {
				if(!_hashSetSkipped.Contains(idx)) {
					idxNew=idx;
					break;
				}
			}
			return ChangeTarget(idxNew,isSkip:true);
		}

		///<summary>Next (+1) or Previous (-1) empty slot in ItemOrder, wrapping around.</summary>
		private bool MoveTarget(int direction) {
			if(IsSingleTarget || _mountSlots.IdxTarget==-1) {
				return false;
			}
			List<int> listIdxEmpty=GetEmptyIdxs();//starts with the target
			if(listIdxEmpty.Count<2) {
				return false;
			}
			int idxNew=direction>0 ? listIdxEmpty[1] : listIdxEmpty[listIdxEmpty.Count-1];
			return ChangeTarget(idxNew,isSkip:false);
		}

		///<summary>The empty slots in ItemOrder, starting with the target and wrapping around.</summary>
		private List<int> GetEmptyIdxs() {
			return _mountSlots.GetEmptySlots().Select(x => _mountSlots.IndexOf(x)).Where(x => x!=-1).ToList();
		}

		///<summary>Makes the empty slot idx the target. A target change while the source is enabled never moves the image of the current enable
		///(design 7.11): its images go to the slot it was started for. With safe disable in effect (PerSlot), the session disables in the safe
		///order and enables for the new target when that enable has ended; without it, the change waits for the enable to deliver or end, and
		///NextTargetText says so ("then retake ..." for a Retake, isRetake). In Continuous mode, once the running enable has delivered an image,
		///its next images go to the target as it is now (ReceiveImage), so only the target changes.</summary>
		private bool ChangeTarget(int idx,bool isSkip,bool isRetake=false) {
			int idxBefore=_mountSlots.IdxTarget;
			if(idx==idxBefore) {
				return false;
			}
			if(!_mountSlots.SetTarget(idx)) {
				return false;
			}
			if(isSkip && idxBefore!=-1) {
				_hashSetSkipped.Add(idxBefore);
			}
			_hashSetSkipped.Remove(idx);
			Log((isSkip ? "Skipped " : (isRetake ? "Retake: target changed from " : "Target changed from "))
				+(idxBefore==-1 ? "none" : MountPlacement.GetSlotLabel(_mountSlots.ListMountItems[idxBefore]))
				+(isSkip ? "; the target is " : " to ")+MountPlacement.GetSlotLabel(_mountSlots.Target)+".");
			if(_enableRunning==null || _enableRunning.IdxTarget==idx) {
				_idxTargetChosen=null;
				NextTargetText="";
				return true;
			}
			if(_setup.Options.SessionMode==TwainSessionMode.Continuous && _enableRunning.CountReceived>0) {
				_idxTargetChosen=null;//design 7.11: in continuous mode only the target changes; nothing is disabled
				NextTargetText="";
				if(IndicatorState==CaptureIndicatorState.WaitingForExposure) {
					//The next image goes to the new target (ReceiveImage): the indicator says so at once (design 7.3 step 3).
					SetIndicator(CaptureIndicatorState.WaitingForExposure,CaptureSessionText.WaitingForExposure+": "+GetEnableTargetText(_enableRunning));
				}
				return true;
			}
			_idxTargetChosen=idx;
			if(Phase==CaptureSessionPhase.Enabled && _enableRunning.IsSafeDisableInEffect && _setup.Options.SessionMode==TwainSessionMode.PerSlot
				&& _enableRunning.CountSaved==0 && !_enableRunning.IsRedelivery)
			{
				StopEnableSafely(_enableRunning,TwainCaptureStopReason.TargetChanged);
			}
			SetNextTargetText(isRetake);
			return true;
		}

		///<summary>NextTargetText while the running enable is for another slot than the chosen target (design 7.11): "Next image: &lt;slot N+1&gt;,
		///then &lt;slot&gt;", or "then retake &lt;slot N&gt;" for a Retake.</summary>
		private void SetNextTargetText(bool isRetake) {
			string enableTargetText=CaptureTargetText.GetSlotText(_mountSlots.ListMountItems,_enableRunning.MountItemTarget);
			NextTargetText=isRetake ? CaptureSessionText.GetNextImageRetakeText(enableTargetText,TargetText)
				: CaptureSessionText.GetNextImageText(enableTargetText,TargetText);
		}

		///<summary>Disables the running enable in the safe order, for reason. The controller's own stops (a target change, a patient change, idle,
		///a full continuous mount) call it only when safe disable is in effect for that enable (design 7.6, 10.6). Its images still go to the slot
		///it was started for.</summary>
		private void StopEnableSafely(CaptureEnableRecord record,TwainCaptureStopReason reason) {
			record.StopRequested=reason;
			_session.Disable(reason);
			Phase=CaptureSessionPhase.Stopping;
			SetIndicator(CaptureIndicatorState.Disabled,CaptureSessionText.Stopping);
		}

		///<summary>After an image was placed in idxFilled: the target the operator chose meanwhile, if it is still empty, otherwise the next empty
		///slot that was not skipped (MountSlots.SetPlaced already moved to the next empty one).</summary>
		private void AdvanceTarget() {
			if(_idxTargetChosen!=null && _mountSlots.SetTarget(_idxTargetChosen.Value)) {
				return;
			}
			if(_mountSlots.IdxTarget==-1 || !_hashSetSkipped.Contains(_mountSlots.IdxTarget)) {
				return;
			}
			foreach(int idx in GetEmptyIdxs()) {
				if(!_hashSetSkipped.Contains(idx)) {
					_mountSlots.SetTarget(idx);
					return;
				}
			}
		}

		///<summary>Every empty slot was skipped.</summary>
		private bool IsOnlySkippedLeft() {
			List<int> listIdxEmpty=GetEmptyIdxs();
			return listIdxEmpty.Count>0 && listIdxEmpty.All(x => _hashSetSkipped.Contains(x));
		}
		#endregion Methods - targets

		#region Methods - stop and finish
		///<summary>Disable (design 7.6): the source is disabled in the safe order; re-enabling needs the deliberate start. Without safe disable in
		///effect, the first press shows the warning and the second one goes ahead.</summary>
		private bool DisableByOperator() {
			if(Phase!=CaptureSessionPhase.Enabled || _enableRunning==null) {
				return false;
			}
			if(!ConfirmUnsafeStop(CaptureSessionAction.Disable)) {
				return true;//the warning shows
			}
			_enableRunning.StopRequested=TwainCaptureStopReason.Cancel;
			_session.Disable(TwainCaptureStopReason.Cancel);
			Phase=CaptureSessionPhase.Stopping;
			SetIndicator(CaptureIndicatorState.Disabled,CaptureSessionText.Stopping);
			Log("The operator disabled the source.");
			return true;
		}

		///<summary>End (design 7.3 step 6, 7.6): with the source enabled, it is disabled in the safe order (with the warning first when that is not
		///safe), the images it still delivers are placed, and the session finishes when the enable has ended.</summary>
		private bool EndByOperator() {
			switch(Phase) {
				case CaptureSessionPhase.Created:
				case CaptureSessionPhase.ConfirmingOlderMount:
				case CaptureSessionPhase.Ready:
					Log("The operator ended the session.");
					Finish(_isStarted ? CaptureSessionEndReason.Ended : CaptureSessionEndReason.CancelledBeforeStart);
					return true;
				case CaptureSessionPhase.Opening:
					_isEndRequested=true;
					SetIndicator(CaptureIndicatorState.Connecting,"Ending when the sensor software has opened.");
					return true;
				case CaptureSessionPhase.Enabled:
					if(_enableRunning==null) {
						//Nothing is enabled any more (its end came without a phase change): nothing to disable.
						Log("The operator ended the session.");
						Finish(CaptureSessionEndReason.Ended);
						return true;
					}
					if(!ConfirmUnsafeStop(CaptureSessionAction.End)) {
						return true;
					}
					_isEndRequested=true;
					_enableRunning.StopRequested=TwainCaptureStopReason.End;
					_session.Disable(TwainCaptureStopReason.End);
					Phase=CaptureSessionPhase.Stopping;
					SetIndicator(CaptureIndicatorState.Disabled,CaptureSessionText.Stopping);
					Log("The operator ended the session; the source is being disabled.");
					return true;
				case CaptureSessionPhase.Stopping:
					_isEndRequested=true;
					Log("The operator ended the session; it ends when the source has stopped.");
					return true;
				default:
					return false;
			}
		}

		///<summary>True when the stop may go ahead: safe disable is in effect for the running enable, or this is the second press of the same
		///command within StopConfirmMs after the warning (design 7.6).</summary>
		private bool ConfirmUnsafeStop(CaptureSessionAction action) {
			if(_enableRunning!=null && _enableRunning.IsSafeDisableInEffect) {
				ClearStopWarning();
				return true;
			}
			long nowMs=_clock.NowMs;
			if(_actionStopPending==action && nowMs-_msStopPending<=StopConfirmMs) {
				ClearStopWarning();
				return true;
			}
			_actionStopPending=action;
			_msStopPending=nowMs;
			WarningText=CaptureSessionText.GetUnsafeStopWarning(action);
			return false;
		}

		private void ClearStopWarning() {
			_actionStopPending=null;
			WarningText="";
		}

		///<summary>The end: the source closes on its worker in the safe order (BeginClose: later images go to the quarantine), the summary is made
		///and SessionFinished is raised.</summary>
		private void Finish(CaptureSessionEndReason endReason) {
			if(Phase==CaptureSessionPhase.Finished) {
				return;
			}
			//The source was enabled, stopping or opening: images it still delivers reach the controller only later (posted to this thread) or go
			//to the quarantine on the closing worker, naming this session's mount and slots.
			IsSourceStillDelivering=_enableRunning!=null || Phase==CaptureSessionPhase.Stopping || Phase==CaptureSessionPhase.Opening;
			Phase=CaptureSessionPhase.Finished;
			StartKind=CaptureStartKind.None;
			ClearStopWarning();
			NextTargetText="";
			OnFinishStartingExt(endReason);
			_enableRunning=null;
			if(_session!=null) {
				_session.BeginClose();//does not wait for the worker
			}
			Summary=new CaptureSessionSummary {
				PatNum=PatNum,
				PatientName=PatientName,
				ProvNum=ProvNum,
				ProviderAbbr=ProviderAbbr,
				ImagingDeviceNum=_setup.ImagingDevice.ImagingDeviceNum,
				DeviceDescription=_setup.ImagingDevice.Description??"",
				SessionGuid=SessionGuid,
				MountNum=_setup.Mount?.MountNum??0,
				CountImages=CountImages,
				CountRetakes=CountRetakes,
				CountQuarantined=CountQuarantined,
				ListDocNums=new List<long>(_listDocNums),
				ListQuarantinePaths=new List<string>(_listQuarantinePaths),
				EndReason=endReason,
				EffectiveSettings=GetEffectiveSettings(),
				DateTimeStarted=_dateTimeStarted.Year>1880 ? _dateTimeStarted : _clock.Now,
				DateTimeEnded=_clock.Now,
			};
			//The counts only: "In effect" has a line of its own in the window, so the indicator stays at two lines.
			SetIndicator(CaptureIndicatorState.Finished,"Finished. "+Summary.GetCountsText());
			Log("Session finished ("+endReason+"): "+Summary.GetText());
			OnFinishingExt(Summary);
			NotifyChanged();
			SessionFinished?.Invoke(Summary);
		}

		///<summary>Design 9.3: the values the session ran with, for its summary.</summary>
		private CaptureEffectiveSettings GetEffectiveSettings() {
			TwainCaptureOptions options=_setup.Options??new TwainCaptureOptions();
			return new CaptureEffectiveSettings {
				SessionMode=options.SessionMode.ToString(),
				Polarity=options.Polarity.ToString(),
				IsHeadless=options.Headless,
				IsSettingsVerified=IsSettingsVerified,
				SettingsNotVerifiedText=SettingsNoticeText,
				CountEnables=_listEnablesPosted.Count,
				CountSafeDisable=_listEnablesPosted.Count(x => x.IsSafeDisableInEffect),
				ListBitsAsked=_listEnablesPosted.Where(x => x.BitDepth!=null).Select(x => x.BitDepth.Bits).Distinct().OrderBy(x => x).ToList(),
				Reason8Bit=_listEnablesPosted.Select(x => x.BitDepth?.Reason8Bit).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))??"",
				Reason8BitDelivered=_reason8BitDelivered,
			};
		}

		///<summary>The session finished while the source was enabled, stopping or opening (a forced close, the X while stopping): images it still
		///delivers are quarantined after the finish, naming this session's mount and slots, so a mount created for the session is kept
		///(CaptureSessionLauncher), and Assign can still file them into their slots.</summary>
		public bool IsSourceStillDelivering { get; private set; }

		///<summary>No document of this session, and none before it, is in the mount: a mount created for this session is still empty.</summary>
		public bool IsMountEmpty => _mountSlots!=null && _mountSlots.ListDocuments.All(x => x==null);
		#endregion Methods - stop and finish

		#region Methods - session events (UI thread, posted)
		private void SubscribeSession(ITwainCaptureSession session) {
			session.ImageAcquired+=image => _dispatcher.Post(() => OnImageAcquired(session,image));
			session.Ended+=ended => _dispatcher.Post(() => OnEnded(session,ended));
			session.StatusChanged+=status => _dispatcher.Post(() => OnStatus(session,status));
			session.ImageQuarantined+=image => _dispatcher.Post(() => OnImageQuarantinedByLibrary(session,image));
			session.Logged+=line => _dispatcher.Post(() => LogFromSession(session,line));
		}

		private void OnStatus(ITwainCaptureSession session,TwainCaptureStatus status) {
			if(session!=_session || status==null || Phase==CaptureSessionPhase.Finished) {
				return;
			}
			CaptureEnableRecord record=_enableRunning;
			if(record==null || status.EnableId!=record.EnableId) {
				return;
			}
			NoteActivity();
			if(status.IsSafeDisableInEffect) {
				record.IsSafeDisableInEffect=true;
			}
			bool isSavedShowing=record.CountSaved>0 && IndicatorState==CaptureIndicatorState.Saved;
			switch(status.State) {
				case TwainCaptureState.Enabling:
					SetIndicator(CaptureIndicatorState.Connecting,CaptureSessionText.Starting);
					break;
				case TwainCaptureState.WaitingForImage:
				case TwainCaptureState.Transferring:
					if(_isPausedBySaveFailure) {
						//A continuous enable that could not be disabled after a failed save (no safe disable in effect) still runs: design 7.12 says
						//Paused, so the indicator keeps the error and never invites an exposure that would not be placed.
						SetIndicator(CaptureIndicatorState.Error,CaptureSessionText.PausedAfterSaveFailureIndicator);
						break;
					}
					if(status.State==TwainCaptureState.WaitingForImage) {
						SetIndicator(CaptureIndicatorState.WaitingForExposure,CaptureSessionText.WaitingForExposure+": "+GetEnableTargetText(record));
					}
					else {
						SetIndicator(CaptureIndicatorState.Receiving,CaptureSessionText.Receiving);
					}
					break;
				case TwainCaptureState.Stopping:
					if(!isSavedShowing) {
						SetIndicator(CaptureIndicatorState.Disabled,CaptureSessionText.Stopping);
					}
					break;
				case TwainCaptureState.WaitingForSource:
					if(!isSavedShowing) {
						SetIndicator(CaptureIndicatorState.Receiving,CaptureSessionText.WaitingForSource);
					}
					break;
			}
			OnStatusExt(status,record);
			NotifyChanged();
		}

		///<summary>Where the running enable's next image goes, for the indicator (design 7.3 step 3): the enable's target, or, once a continuous
		///enable delivered an image, the target as it is now, which is where ReceiveImage places it.</summary>
		private string GetEnableTargetText(CaptureEnableRecord record) {
			if(IsSingleTarget) {
				return CaptureTargetText.GetSingleText(record.ToothNumbers);
			}
			int idx=GetNextImageIdx(record);
			if(idx==-1) {
				return CaptureSessionText.EverySlotFilled;
			}
			return CaptureTargetText.GetSlotText(_mountSlots.ListMountItems,_mountSlots.ListMountItems[idx]);
		}

		///<summary>The index of the slot record's next image goes to (ReceiveImage): its own target, or, after the first image of a continuous
		///enable, the target as it is now (-1 when every slot has an image).</summary>
		private int GetNextImageIdx(CaptureEnableRecord record) {
			if(record==null || IsSingleTarget) {
				return -1;
			}
			return record.IsContinuous && record.CountReceived>0 ? _mountSlots.IdxTarget : record.IdxTarget;
		}

		private void OnImageAcquired(ITwainCaptureSession session,AcquiredImage image) {
			if(image==null) {
				return;
			}
			if(session==_session && Phase!=CaptureSessionPhase.Finished) {
				NoteActivity();
			}
			CaptureEnableRecord record=image.Tag as CaptureEnableRecord;
			ReceiveImage(session,image,record);
			NotifyChanged();
		}

		///<summary>Where an image goes (design 7.3 step 4, 7.11, 7.12): the quarantine after the end, after a patient change, for an extra image
		///and when its slot is taken; otherwise it is saved into the target of its enable.</summary>
		private void ReceiveImage(ITwainCaptureSession session,AcquiredImage image,CaptureEnableRecord record) {
			if(Phase==CaptureSessionPhase.Finished) {
				QuarantineImage(session,image,record,"The image arrived after the capture session ended.",image.IsRedelivery || record?.IsRedelivery==true);
				return;
			}
			if(session!=_session || record==null) {
				QuarantineImage(session,image,record,"The image did not come from an enable of this capture session.",false);
				return;
			}
			record.CountReceived++;
			if(image.IsContinuousEnable) {
				record.IsContinuous=true;
			}
			bool isHandled=false;
			OnImageReceivedExt(image,record,ref isHandled);
			if(isHandled) {
				return;
			}
			if(_isPlacementStopped) {
				QuarantineImage(session,image,record,_placementStoppedReason,false);
				return;
			}
			if(_isPausedBySaveFailure) {
				//Design 7.12 "Save fails": the session pauses until the deliberate re-enable. A continuous enable that could not be disabled (no safe
				//disable in effect) still delivers; its images are not placed into the slot whose save failed, or any other, meanwhile.
				QuarantineImage(session,image,record,CaptureSessionText.PausedAfterSaveFailure,false);
				NoticeText=CaptureSessionText.PausedAfterSaveFailureNotice;
				PlaySound(CaptureSound.NotReceived);
				return;
			}
			if(image.IsRedelivery || record.IsRedelivery) {
				//The source knows no patient: a redelivered image is placed only after the operator confirms it (design 7.12). OnImageReceivedExt
				//holds it for that; this is the fallback.
				QuarantineImage(session,image,record,"Redelivered image that the operator did not confirm.",true);
				return;
			}
			if(image.Kind==AcquiredImageKind.Extra || (!image.IsContinuousEnable && image.IndexInEnable>1)) {
				//Also a later image of a per-slot enable (CAP_XFERCOUNT=1) that the library did not mark Extra: never saved as the enable's own image,
				//nor marked with its slot (design 7.12 "Extra images").
				QuarantineImage(session,image,record,"The sensor software sent more images than expected.",false);
				NoticeText=CaptureSessionText.ExtraImages;
				return;
			}
			int idxTarget=-1;
			MountItem mountItemTarget=null;
			if(!IsSingleTarget) {
				//An image after the first one of a continuous enable (design 7.4), also one drained during its stop (End, a full mount), was
				//exposed for the target as it is now: the target moved on after each save, so the slot its enable started for is never its own.
				//Otherwise the target its enable was started for, also for a Drained image of a per-slot enable (design 7.11).
				bool isContinuousNext=image.IsContinuousEnable && image.IndexInEnable>1;
				idxTarget=isContinuousNext ? _mountSlots.IdxTarget : record.IdxTarget;
				mountItemTarget=idxTarget==-1 ? null : _mountSlots.ListMountItems[idxTarget];
				if(idxTarget==-1) {
					QuarantineImage(session,image,record,"Every slot of the mount already holds an image.",false);
					return;
				}
				if(isContinuousNext && _hashSetSkipped.Contains(idxTarget)) {
					//Only skipped slots are left (AdvanceTarget keeps the target on one): a skipped slot is taken only after the deliberate "Take the
					//skipped slots", as in PerSlot (design 7.3).
					QuarantineImage(session,image,record,CaptureSessionText.OnlySkippedLeftReason,false);
					NoticeText=CaptureSessionText.OnlySkippedLeftContinuous;
					PlaySound(CaptureSound.NotReceived);
					return;
				}
				if(_mountSlots.ListDocuments[idxTarget]!=null) {
					SetImageTarget(image,mountItemTarget,record.ToothNumbers);
					QuarantineImage(session,image,record,"Its slot ("+MountPlacement.GetSlotLabel(mountItemTarget)+") already holds an image.",false);
					return;
				}
			}
			SaveImage(image,record,idxTarget,mountItemTarget);
		}

		///<summary>Saves image into mountItemTarget (idxTarget), or in single-target mode as a document with toothNumbersSingle, by default the
		///banner's teeth when its enable started (record.ToothNumbers).</summary>
		private void SaveImage(AcquiredImage image,CaptureEnableRecord record,int idxTarget,MountItem mountItemTarget,string toothNumbersSingle=null) {
			string toothNumbers=toothNumbersSingle??record.ToothNumbers;
			RadiographTarget target=GetRadiographTarget(mountItemTarget,toothNumbers);
			//The target this image is saved into, also in its sidecar: a failed save quarantines it for this target (design 7.12).
			SetImageTarget(image,mountItemTarget,toothNumbers);
			RadiographSaveRequest request=new RadiographSaveRequest {
				Image=image,
				Metadata=CaptureMetadataParser.Parse(image.MetadataJson),
				Patient=_setup.Patient,
				DocCategory=_setup.CategoryDefNum,
				ProvNum=ProvNum,
				Target=target,
				Is16BitAllowed=record.BitDepth!=null && record.BitDepth.Bits==16,
				Polarity=_setup.Options.Polarity,
				IsInversionVerified=record.BitDepth!=null && record.BitDepth.IsInversionVerified,
				SessionGuid=SessionGuid,
				DeviceDescription=_setup.ImagingDevice.Description??"",
				ImagingDeviceNum=_setup.ImagingDevice.ImagingDeviceNum,
			};
			RadiographSaveResult result=_radiographImport.Save(request);
			if(!result.IsSaved) {
				string error=string.IsNullOrWhiteSpace(result.Error) ? "the image was not saved." : result.Error.Trim();
				Log("Save failed for "+target.GetLabel()+": "+error);
				//Design 7.12: the red strip, the image to the quarantine, and the session pauses: the next enable needs the deliberate start.
				RedStripText=CaptureSessionText.CouldNotSavePrefix+error;
				_isPausedBySaveFailure=true;
				QuarantineImage(_session,image,record,CaptureSessionText.CouldNotSavePrefix+error,false);
				if(_enableRunning==record && Phase==CaptureSessionPhase.Enabled && !record.IsRedelivery && record.IsContinuous) {
					//A continuous enable stays enabled after an image (design 7.4): the pause disables it in the safe order, but only with safe
					//disable in effect; otherwise the fork starts no disable of its own (design 7.6, 10.6) and ReceiveImage quarantines what it sends.
					if(record.IsSafeDisableInEffect) {
						StopEnableSafely(record,TwainCaptureStopReason.Other);
						Log("The save failed: the continuous enable is being disabled.");
					}
					else {
						Log("The save failed: the continuous enable stays enabled (safe disable is not in effect); its images go to unassigned captures "
							+"until the deliberate re-enable.");
					}
				}
				//The error only: the red strip also says where the image went, or where its only copy is when the quarantine write failed.
				SetIndicator(CaptureIndicatorState.Error,CaptureSessionText.CouldNotSavePrefix+error);
				PlaySound(CaptureSound.Error);
				return;
			}
			record.CountSaved++;
			_listDocNums.Add(result.Document.DocNum);
			LastSavedImage=image;
			LastSavedDocument=result.Document;
			LastSavedMountItem=mountItemTarget;
			if(IsSingleTarget) {
				_listDocumentsSingle.Add(result.Document);
				SetIndicator(CaptureIndicatorState.Saved,CaptureSessionText.GetSavedSingleText(toothNumbers));
			}
			else {
				_mountSlots.SetPlaced(idxTarget,result.Document);
				AdvanceTarget();
				SetIndicator(CaptureIndicatorState.Saved,CaptureSessionText.GetSavedText(MountPlacement.GetSlotLabel(mountItemTarget)));
				if(_mountSlots.IdxTarget!=-1 && IsOnlySkippedLeft() && _enableRunning==record && Phase==CaptureSessionPhase.Enabled
					&& record.IsContinuous)
				{
					//Only skipped slots are left: they are taken after the deliberate "Take the skipped slots" (design 7.3), never by the running
					//continuous enable. With safe disable in effect it is disabled, and its end offers that start; otherwise it stays enabled
					//and its images go to unassigned captures (ReceiveImage).
					if(record.IsSafeDisableInEffect) {
						StopEnableSafely(record,TwainCaptureStopReason.TargetChanged);
						SetIndicator(CaptureIndicatorState.Saved,CaptureSessionText.GetSavedText(MountPlacement.GetSlotLabel(mountItemTarget)));
						Log("Only skipped slots are left; the continuous enable is being disabled.");
					}
					else {
						NoticeText=CaptureSessionText.OnlySkippedLeftContinuous;
						Log("Only skipped slots are left; the continuous enable stays enabled until the operator ends or disables it (safe disable is not "
							+"in effect).");
					}
				}
				if(_mountSlots.IdxTarget==-1 && _enableRunning==record && Phase==CaptureSessionPhase.Enabled && record.IsContinuous) {
					//A continuous enable stays in state 5 after Count=0 (design 7.4). The full mount ends it in the safe order, but only with safe
					//disable in effect: otherwise the fork starts no disable of its own (design 7.6, 10.6), and the operator ends it.
					if(record.IsSafeDisableInEffect) {
						_isEndRequested=true;
						StopEnableSafely(record,TwainCaptureStopReason.End);
						SetIndicator(CaptureIndicatorState.Saved,CaptureSessionText.GetSavedText(MountPlacement.GetSlotLabel(mountItemTarget)));
						Log("The mount is full; the continuous enable is being disabled.");
					}
					else {
						NoticeText=CaptureSessionText.MountFullContinuous;
						Log("The mount is full; the continuous enable stays enabled until the operator ends it (safe disable is not in effect).");
					}
				}
			}
			if(result.ListWarnings.Count>0) {
				NoticeText=string.Join(" ",result.ListWarnings);
				foreach(string warning in result.ListWarnings) {
					Log("Saved with a warning: "+warning);
				}
			}
			Log("Saved DocNum "+result.Document.DocNum+" ("+result.Plan?.Format+") to "+target.GetLabel()+".");
			PlaySound(CaptureSound.Saved);
			OnImageSavedExt(image,record,result);
		}

		///<summary>Moves image to the quarantine for the bound patient (or "patient unknown"), with reason in its sidecar.</summary>
		private void QuarantineImage(ITwainCaptureSession session,AcquiredImage image,CaptureEnableRecord record,string reason,bool isPatientUnknown) {
			ITwainCaptureSession sessionQuarantine=session??_session;
			string error="";
			string path=null;
			try {
				path=sessionQuarantine?.QuarantineImage(image,reason,isPatientUnknown,out error);
			}
			catch(Exception ex) {
				error=ex.Message;
			}
			if(record!=null) {
				record.CountQuarantined++;
			}
			if(path==null) {
				string journal=string.IsNullOrEmpty(image.JournalPath) ? "" : " Its journal copy is "+image.JournalPath+".";
				string text="The image could not be moved to unassigned captures: "+(error??"").Trim().TrimEnd('.')+"."+journal;
				RedStripText=RedStripText=="" ? text : RedStripText+" "+text;
				Log("Quarantine failed ("+reason+"): "+error+"."+journal);
				PlaySound(CaptureSound.Error);
				OnImageQuarantinedExt(image,reason,null);
				return;
			}
			AddQuarantined(path);
			Log("Quarantined ("+reason+"): "+path);
			OnImageQuarantinedExt(image,reason,path);
		}

		///<summary>After BeginClose: the library put an image in the quarantine itself.</summary>
		private void OnImageQuarantinedByLibrary(ITwainCaptureSession session,AcquiredImage image) {
			if(session!=_session || image==null) {
				return;
			}
			AddQuarantined(image.QuarantinePath??"");
			Log("Quarantined by the closing source: "+(image.QuarantinePath??""));
			OnImageQuarantinedExt(image,"The image arrived after the capture session ended.",image.QuarantinePath);
			NotifyChanged();
		}

		private void AddQuarantined(string path) {
			_listQuarantinePaths.Add(path);
			if(Summary!=null) {
				Summary.CountQuarantined=_listQuarantinePaths.Count;
				Summary.ListQuarantinePaths=new List<string>(_listQuarantinePaths);
			}
			CaptureUnassignedInfo info=CaptureSessionLauncher.GetUnassignedInfo(_services);//never throws: the count is a convenience
			UnassignedText=info.Text;
			IsUnassignedOverdue=info.IsOverdue;
		}

		///<summary>The end of one enable (design 7.12): what the indicator shows, and whether the session enables again (PerSlot, for the next
		///empty slot), waits for a deliberate re-enable, or finishes.</summary>
		private void OnEnded(ITwainCaptureSession session,TwainCaptureEnded ended) {
			if(session!=_session || ended==null) {
				return;
			}
			CaptureEnableRecord record=ended.Tag as CaptureEnableRecord;
			bool isRunning=record!=null && record==_enableRunning;
			if(isRunning) {
				_enableRunning=null;
				NextTargetText="";
			}
			string textEnded=CaptureEndText.GetEndedText(ended);
			if(!string.IsNullOrWhiteSpace(ended.Reason8Bit)) {
				LogOnce("16 bits were asked and the source gave 8: "+ended.Reason8Bit.Trim());//design 7.12 "16-bit refused": silent, logged
				if(_reason8BitDelivered=="") {
					_reason8BitDelivered=ended.Reason8Bit.Trim();
				}
			}
			bool isHandled=false;
			OnEnableEndedExt(ended,record,isRunning,ref isHandled);
			if(isHandled || Phase==CaptureSessionPhase.Finished) {
				NotifyChanged();
				return;
			}
			if(!isRunning) {
				Log("Enable "+ended.EnableId+" ended ("+ended.Reason+"), but it is not the running one; ignored.");
				NotifyChanged();
				return;
			}
			if(ended.ExtraImageCount>0) {
				NoticeText=CaptureSessionText.ExtraImages;
			}
			if(_isEndRequested) {
				if(textEnded!="") {
					Log("The last enable ended: "+textEnded);
				}
				Finish(CaptureSessionEndReason.Ended);
				return;
			}
			HandleEnded(ended,record,textEnded);
			NotifyChanged();
		}

		private void HandleEnded(TwainCaptureEnded ended,CaptureEnableRecord record,string textEnded) {
			//textHold decides that no automatic enable follows (ContinueAfterEnable). textEnded already says it for the other ends.
			string textHold=ended.SourceMayHoldImages && ended.Reason!=TwainCaptureEndReason.TransferFailed ? " "+CaptureEndText.MayStillHoldImages : "";
			switch(ended.Reason) {
				case TwainCaptureEndReason.Completed:
					if(record.CountSaved==0 && record.CountQuarantined==0 && ended.ImageCount==0) {
						PlaySound(CaptureSound.NotReceived);
						ShowReady(CaptureStartKind.ReEnable,CaptureIndicatorState.ImageNotReceived,CaptureEndText.ImageNotReceived);
						return;
					}
					ContinueAfterEnable(textHold);
					return;
				case TwainCaptureEndReason.Stopped:
					if(ended.StopReason==TwainCaptureStopReason.TargetChanged && record.StopRequested==TwainCaptureStopReason.TargetChanged) {
						ContinueAfterEnable(textHold);//design 7.11: enable for the new target
						return;
					}
					if(_isPausedBySaveFailure && ended.StopReason!=TwainCaptureStopReason.Close) {
						ContinueAfterEnable(textHold);//paused after a failed save: the red strip and the deliberate re-enable
						return;
					}
					if(ended.StopReason==TwainCaptureStopReason.Close) {
						//The library closed the source on its own (WM_QUIT reached its worker): the controller's own close never gets here, because
						//Finish comes first. The worker is closing, so the session is no longer open; Retry opens a new one (design 7.3 step 7, 7.6).
						_isSessionOpen=false;
						Log("The sensor software was closed while the session ran. "+textEnded);
						PlaySound(CaptureSound.Error);
						ShowReady(CaptureStartKind.Retry,CaptureIndicatorState.Error,(CaptureSessionText.SourceClosed+" "+textEnded).Trim());
						return;
					}
					PlaySound(CaptureSound.Disabled);
					ShowReady(GetReEnableKind(),CaptureIndicatorState.Disabled,(CaptureSessionText.Disabled+" "+textEnded).Trim());
					return;
				case TwainCaptureEndReason.CloseRequested:
					bool isPlainClose=ended.ConditionCode==null || ended.ConditionCode.Value==0;
					PlaySound(isPlainClose ? CaptureSound.Disabled : CaptureSound.NotReceived);
					ShowReady(GetReEnableKind(),CaptureIndicatorState.SensorSoftwareClosed,textEnded.Trim());
					return;
				case TwainCaptureEndReason.TransferFailed:
					PlaySound(CaptureSound.NotReceived);
					ShowReady(CaptureStartKind.ReEnable,CaptureIndicatorState.ImageNotReceived,textEnded);//the slot does not advance
					return;
				case TwainCaptureEndReason.DeviceOffline:
					IsDeviceOffline=true;
					PlaySound(CaptureSound.Error);
					//Design 7.7: "Sensor not connected" and a Retry button; no automatic retry. Design 7.12: the state after is Connecting.
					ShowReady(CaptureStartKind.Retry,CaptureIndicatorState.Connecting,CaptureEndText.SensorNotConnected);
					return;
				case TwainCaptureEndReason.EnableFailed:
					PlaySound(CaptureSound.Error);
					ShowReady(CaptureStartKind.Retry,CaptureIndicatorState.Error,textEnded.Trim());
					return;
				default://RedeliveryUnavailable (OnEnableEndedExt handles it for a redelivery enable), Refused, Error
					PlaySound(CaptureSound.Error);
					ShowReady(CaptureStartKind.Retry,CaptureIndicatorState.Error,textEnded.Trim());
					return;
			}
		}

		///<summary>After an enable that delivered (or was stopped for a target change): the next enable in PerSlot mode, the banner for "Take
		///another" in single-target mode, a pause after a failed save, or the finish when the mount is full.</summary>
		private void ContinueAfterEnable(string textHold) {
			if(_isPausedBySaveFailure) {
				ShowReady(CaptureStartKind.ReEnable,CaptureIndicatorState.Error,(RedStripText+textHold).Trim());
				return;
			}
			if(textHold!="") {
				//The source may still hold an exposure: no automatic enable; the operator checks the sensor software first (design 7.12).
				PlaySound(CaptureSound.NotReceived);
				ShowReady(IsSingleTarget ? CaptureStartKind.TakeAnother : CaptureStartKind.ReEnable,CaptureIndicatorState.ImageNotReceived,textHold.Trim());
				return;
			}
			if(IsSingleTarget) {
				if(IndicatorState==CaptureIndicatorState.Saved) {
					ShowReady(CaptureStartKind.TakeAnother,CaptureIndicatorState.Saved,_indicatorText+". "+CaptureSessionText.ReadyTakeAnother);
				}
				else {
					ShowReady(CaptureStartKind.TakeAnother,CaptureIndicatorState.Ready,CaptureSessionText.ReadyTakeAnother);
				}
				return;
			}
			if(_mountSlots.IdxTarget==-1) {
				Finish(CaptureSessionEndReason.MountFull);
				return;
			}
			if(IsOnlySkippedLeft()) {
				ShowReady(CaptureStartKind.TakeSkipped,CaptureIndicatorState.Ready,CaptureSessionText.ReadyTakeSkipped);
				return;
			}
			//Design 7.3 step 4: PerSlot enables again for the next empty slot. A Completed enable ran per slot (it was disabled after Count=0),
			//also when SessionMode=Continuous was configured and the source did not accept ContinuousCap.
			EnableNext();
		}

		private CaptureStartKind GetReEnableKind() {
			if(IsSingleTarget && _listDocumentsSingle.Count>0) {
				return CaptureStartKind.TakeAnother;
			}
			return CaptureStartKind.ReEnable;
		}
		#endregion Methods - session events (UI thread, posted)

		#region Methods - helpers
		private static CaptureSessionAction ToAction(CaptureSessionKey key) {
			switch(key) {
				case CaptureSessionKey.Retake: return CaptureSessionAction.Retake;
				case CaptureSessionKey.Skip: return CaptureSessionAction.Skip;
				case CaptureSessionKey.NextSlot: return CaptureSessionAction.NextSlot;
				case CaptureSessionKey.PreviousSlot: return CaptureSessionAction.PreviousSlot;
				case CaptureSessionKey.Rotate: return CaptureSessionAction.Rotate;
				case CaptureSessionKey.Disable: return CaptureSessionAction.Disable;
				default: return CaptureSessionAction.End;
			}
		}

		private void SetIndicator(CaptureIndicatorState state,string text) {
			IndicatorState=state;
			_indicatorText=text??"";
		}

		private void PlaySound(CaptureSound sound) {
			try {
				SoundRequested?.Invoke(sound);
			}
			catch(Exception ex) {
				Log("The sound could not be played: "+ex.Message);
			}
		}

		private string GetProviderAbbr(long provNum) {
			if(provNum==0) {
				return "";
			}
			try {
				return _services.Store?.GetProviderAbbr(provNum)??"";
			}
			catch {
				return "";
			}
		}

		///<summary>Design 7.1 (phase 3): activity for Open Dental's automatic logoff (CaptureSessionServices.ActionNoteActivity). Never throws.</summary>
		private void NoteActivity() {
			try {
				_services.ActionNoteActivity?.Invoke();
			}
			catch {
				//the logoff timer is a convenience here; the session goes on
			}
		}

		///<summary>A line for the session log, the Logged event and Capture\capture-log.txt.</summary>
		private void Log(string text) {
			string line="Capture session: "+(text??"");
			_listLog.Add(line);
			try {
				_services.ActionWriteLog?.Invoke(line);
			}
			catch {
				//logging never stops the session
			}
			try {
				Logged?.Invoke(line);
			}
			catch {
			}
		}

		///<summary>Log, once per text in this session (the 8-bit reason, design 7.8).</summary>
		private void LogOnce(string text) {
			if(_hashSetReasonsLogged.Add(text??"")) {
				Log(text);
			}
		}

		///<summary>The worker's own log lines: already in the capture log file, so only for the window.</summary>
		private void LogFromSession(ITwainCaptureSession session,string line) {
			if(session!=_session) {
				return;
			}
			string text="TWAIN: "+(line??"");
			_listLog.Add(text);
			try {
				Logged?.Invoke(text);
			}
			catch {
			}
		}

		private void NotifyChanged() {
			Version++;
			_canStartShown=CanStart;
			_canConfirmRedeliveredShown=CanConfirmRedelivered;
			_canRequestRedeliveryShown=CanRequestRedelivery;
			try {
				Changed?.Invoke();
			}
			catch(Exception ex) {
				_listLog.Add("Capture session: a Changed handler failed: "+ex.Message);
			}
		}
		#endregion Methods - helpers

		#region Extension points (CAP-4 implementer A2)
		///<summary>Retake and Rotate, and any command A2 handles first. Set isHandled to skip the built-in handling.</summary>
		partial void ExecuteExt(CaptureSessionAction action,ref bool isHandled);
		partial void CanExecuteExt(CaptureSessionAction action,ref bool canExecute);
		///<summary>An image before it is routed (a redelivered image to confirm, the patient-change monitor). Set isHandled when A2 took it.</summary>
		partial void OnImageReceivedExt(AcquiredImage image,CaptureEnableRecord record,ref bool isHandled);
		partial void OnImageSavedExt(AcquiredImage image,CaptureEnableRecord record,RadiographSaveResult result);
		///<summary>Every quarantine move (path null when it failed), for the "Capture event:" entry of design 7.13.</summary>
		partial void OnImageQuarantinedExt(AcquiredImage image,string reason,string path);
		///<summary>The end of an enable, before the built-in handling (capture events of design 7.13, redelivery, idle and patient-change stops).
		///isRunning: it was the running enable (EnableRunning is already null). Set isHandled to skip the built-in handling.</summary>
		partial void OnEnableEndedExt(TwainCaptureEnded ended,CaptureEnableRecord record,bool isRunning,ref bool isHandled);
		partial void OnEnablePostedExt(CaptureEnableRecord record);
		partial void OnStatusExt(TwainCaptureStatus status,CaptureEnableRecord record);
		partial void OnTickExt(long nowMs);
		///<summary>The summary is made (the session's ImageCreate audit entry of design 10.7).</summary>
		partial void OnFinishingExt(CaptureSessionSummary summary);
		///<summary>Begin: the banner shows (the patient-change monitor starts).</summary>
		partial void OnBeginExt();
		///<summary>A deliberate start or re-enable passed the patient check (placement resumes after a patient change).</summary>
		partial void OnDeliberateStartExt();
		///<summary>Finish has begun: Phase is Finished, EnableRunning is still the enable that ran (a forced close), the source is not yet closing
		///and the summary is not made.</summary>
		partial void OnFinishStartingExt(CaptureSessionEndReason endReason);
		#endregion Extension points (CAP-4 implementer A2)
	}
}
