// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ImagingCapture.Dicom;
using ImagingCapture.Save;
using ImagingCapture.Session;
using ImagingDeviceManager.Capture;
using OpenDentBusiness;

namespace ImagingCapture.UI {
	///<summary>How the indicator is coloured (the window picks the colours).</summary>
	public enum CaptureIndicatorTone {
		///<summary>Ready, disabled, finished.</summary>
		Neutral,
		///<summary>Connecting, starting, receiving: wait.</summary>
		Busy,
		///<summary>"Sensor software open, waiting for exposure".</summary>
		Waiting,
		///<summary>Saved.</summary>
		Good,
		///<summary>Sensor software closed, image not received, the older-mount question.</summary>
		Warning,
		Error,
	}

	///<summary>How a slot of the mini-map is drawn.</summary>
	public enum CaptureSlotTileState {
		Empty,
		///<summary>The target of the next image.</summary>
		Target,
		///<summary>Holds an image.</summary>
		Filled,
		///<summary>Skipped: empty, taken after the slots that were not skipped.</summary>
		Skipped,
	}

	///<summary>One slot of the mini-map, placed in the map's area (design 7.3 step 3).</summary>
	public sealed class CaptureSlotTile {
		///<summary>The index in MountSlots.ListMountItems, for SelectSlot.</summary>
		public int Idx { get; set; }
		///<summary>The slot's ItemOrder, as text.</summary>
		public string Number { get; set; }="";
		///<summary>"#6" or "#1,2,3", or "".</summary>
		public string TeethText { get; set; }="";
		///<summary>"3 of 18: Max R, #1,2,3", for a tooltip.</summary>
		public string ToolTip { get; set; }="";
		public double Left { get; set; }
		public double Top { get; set; }
		public double Width { get; set; }
		public double Height { get; set; }
		public CaptureSlotTileState State { get; set; }
		///<summary>The running enable's slot: its images go here, whatever the target is now.</summary>
		public bool IsEnableTarget { get; set; }
		///<summary>An empty slot that is not the target: a tap makes it the target.</summary>
		public bool IsClickable { get; set; }
	}

	///<summary>One tooth of the single-target tooth picker (Universal 1-32): row 0 is the maxillary arch 1 to 16, row 1 the mandibular arch 32 to
	///17, each from the patient's right, as a chart shows them.</summary>
	public sealed class CaptureToothTile {
		public string Universal { get; set; }="";
		public int Row { get; set; }
		public int Column { get; set; }
		public bool IsSelected { get; set; }
	}

	///<summary>The thin view model of the capture session window (FrmCaptureSession): what each part of the window shows and whether each
	///command is offered, read from CaptureSessionController, plus the little state that is the window's own (the hotkeys, the sounds, the tooth
	///picker's error, how long the preview is fresh and when the summary closes). No WPF types, so that it is tested like the controller. UI
	///thread only.
	///
	///Keys: a bound [Keys] chord pressed while the window has the focus goes to OnKeyDown and OnKeyUp (and the window marks it handled, so it
	///never reaches a focused control). While the source is enabled the session keys are hotkeys too (CaptureHotkeys), which reach OnHotkey
	///when the sensor software has the foreground. Confirm is never a hotkey. No message boxes: everything shows in the window's indicator area.</summary>
	public sealed class CaptureSessionViewModel {
		#region Fields
		///<summary>How long a new image's preview counts as fresh (the window highlights it).</summary>
		public const int PreviewFreshMs=4000;
		///<summary>How long the summary shows before the window closes by itself (design 7.3 step 6, 10.2 step 4).</summary>
		public const int SummaryCloseMs=10000;
		private readonly CaptureSessionController _controller;
		private readonly CaptureKeyBindings _keyBindings;
		private readonly CaptureHotkeys _hotkeys;
		private readonly CaptureSoundPlayer _soundPlayer;
		private int _version;
		private int _versionControllerSeen=-1;
		private bool _isClosing;
		private string _toothErrorText="";
		private Document _documentPreviewTracked;
		private long _msPreviewShown=long.MinValue/2;
		private bool _isPreviewFreshShown;
		private long? _msFinished;
		private int _secondsToCloseShown=-1;
		private AcquiredImage _imagePreviewCached;
		private bool _isInvertedCached;
		private CapturePreviewPixels _previewPixelsCached;
		private string _previewErrorCached="";
		#endregion Fields

		#region Constructor
		///<summary>actionPlaySound: plays a system sound (null: CaptureSoundPlayer.PlaySystemSound). The key bindings and the sounds come from the
		///controller's [Keys] and [Sounds]; their warnings go to the session log.</summary>
		public CaptureSessionViewModel(CaptureSessionController controller,Action<CaptureSystemSound> actionPlaySound=null) {
			_controller=controller??throw new ArgumentNullException(nameof(controller));
			_keyBindings=CaptureKeyBindings.FromConfig(controller.SessionConfig);
			foreach(string warning in _keyBindings.ListWarnings) {
				_controller.LogFromWindow(warning);
			}
			_hotkeys=new CaptureHotkeys(_keyBindings,_controller.LogFromWindow);
			_soundPlayer=new CaptureSoundPlayer(controller.SessionConfig,actionPlaySound,_controller.LogFromWindow);
			_controller.Changed+=controller_Changed;
			_controller.SoundRequested+=controller_SoundRequested;
		}
		#endregion Constructor

		#region Events
		///<summary>Something the window shows changed (the controller's Changed, or the view model's own state). UI thread.</summary>
		public event Action Changed;
		#endregion Events

		#region Properties - general
		public CaptureSessionController Controller => _controller;
		public CaptureKeyBindings KeyBindings => _keyBindings;
		public CaptureHotkeys Hotkeys => _hotkeys;
		///<summary>Changes whenever something the window shows changed.</summary>
		public int Version {
			get {
				SyncControllerVersion();
				return _version;
			}
		}
		public CaptureSessionPhase Phase => _controller.Phase;
		///<summary>Opening, enabled or stopping: the source is busy, so the quarantine links are not offered.</summary>
		public bool IsSourceBusy => Phase==CaptureSessionPhase.Opening || Phase==CaptureSessionPhase.Enabled || Phase==CaptureSessionPhase.Stopping;
		public bool IsClosing => _isClosing;
		#endregion Properties - general

		#region Properties - banner
		///<summary>The window's title.</summary>
		public string WindowTitle => "X-ray capture for "+_controller.PatientName;
		public string PatientName => _controller.PatientName;
		///<summary>"Born 1/2/1980, age 45" (design 7.3 step 2), or "Date of birth not entered".</summary>
		public string PatientDetailsText {
			get {
				Patient patient=_controller.Patient;
				if(patient==null || patient.Birthdate.Year<1880) {
					return "Date of birth not entered";
				}
				string age=PatientLogic.DateToAgeString(patient.Birthdate,_controller.Now);
				return "Born "+patient.Birthdate.ToShortDateString()+(age=="" ? "" : ", age "+age);
			}
		}
		///<summary>The provider can be changed (before Start, design 10.8).</summary>
		public bool CanEditProvider => _controller.CanEditProvider && !_isClosing;
		public long ProvNum => _controller.ProvNum;
		public string ProviderText => "Provider: "+(_controller.ProviderAbbr=="" ? "none" : _controller.ProviderAbbr);
		public bool IsMountVisible => !_controller.IsSingleTarget;
		///<summary>"Mount: FMX 18, 10/31/2026".</summary>
		public string MountText => _controller.IsSingleTarget ? "" : "Mount: "+_controller.MountText;
		///<summary>Design 7.3 step 1: the mount is dated before today, and the operator confirms it or cancels.</summary>
		public bool IsOlderMountQuestionVisible => Phase==CaptureSessionPhase.ConfirmingOlderMount && !_isClosing;
		public string OlderMountText => _controller.OlderMountText;
		#endregion Properties - banner

		#region Properties - start
		///<summary>The Start button shows while the banner waits for a deliberate start (design 7.3 step 2); its text says what it does.</summary>
		public bool IsStartVisible => Phase==CaptureSessionPhase.Ready && _controller.StartKind!=CaptureStartKind.None && !_isClosing;
		///<summary>Armed: 2 s after the banner appeared (CaptureSessionController.CanStart).</summary>
		public bool IsStartEnabled => IsStartVisible && _controller.CanStart;
		///<summary>"Start X-rays for Pat Test", "Re-enable for Pat Test", "Retry", "Take another".</summary>
		public string StartText => _controller.StartText;
		///<summary>"Or hold F12 for 1 s" when a Confirm key is bound, while the Start button shows.</summary>
		public string StartHintText {
			get {
				CaptureKeyChord chord=_keyBindings.GetChord(CaptureSessionKey.Confirm);
				if(chord==null || !IsStartVisible) {
					return "";
				}
				return "Or hold "+chord.Text+" for 1 s";
			}
		}
		public bool IsRequestRedeliveryVisible => _controller.IsRedeliveryOffered && !_isClosing;
		public bool IsRequestRedeliveryEnabled => IsRequestRedeliveryVisible && _controller.CanRequestRedelivery;
		public string RequestRedeliveryText => _controller.RedeliveryText;
		#endregion Properties - start

		#region Properties - indicator
		public string IndicatorText => _controller.IndicatorText;

		public CaptureIndicatorTone IndicatorTone {
			get {
				if(Phase==CaptureSessionPhase.ConfirmingOlderMount) {
					return CaptureIndicatorTone.Warning;
				}
				switch(_controller.IndicatorState) {
					case CaptureIndicatorState.Connecting:
					case CaptureIndicatorState.Receiving:
						return CaptureIndicatorTone.Busy;
					case CaptureIndicatorState.WaitingForExposure:
						return CaptureIndicatorTone.Waiting;
					case CaptureIndicatorState.Saved:
						return CaptureIndicatorTone.Good;
					case CaptureIndicatorState.SensorSoftwareClosed:
					case CaptureIndicatorState.ImageNotReceived:
						return CaptureIndicatorTone.Warning;
					case CaptureIndicatorState.Error:
						return CaptureIndicatorTone.Error;
					default:
						return CaptureIndicatorTone.Neutral;
				}
			}
		}

		///<summary>"Target: 1 of 18: Max R, #6" (design 10.2), "Target: Single image, #8,9", or "" once the session finished.</summary>
		public string TargetText => Phase==CaptureSessionPhase.Finished || Phase==CaptureSessionPhase.Created ? "" : "Target: "+_controller.TargetText;
		///<summary>Design 7.11: "Next image: ..., then ..." while the running enable is for another slot.</summary>
		public string NextTargetText => _controller.NextTargetText;
		///<summary>Design 7.6: the red bar.</summary>
		public string PatientChangedText => _controller.PatientChangedText;
		///<summary>Design 7.12: the red "Could not save" strip.</summary>
		public string RedStripText => _controller.RedStripText;
		///<summary>The extra-images notice and other notices that do not stop the session.</summary>
		public string NoticeText => _controller.NoticeText;
		///<summary>Design 7.6: the Cancel warning before an unsafe stop.</summary>
		public string WarningText => _controller.WarningText;
		///<summary>"N unassigned captures on this computer", or "".</summary>
		public string UnassignedText => _controller.UnassignedText??"";
		///<summary>The quarantine and Recover links may be opened now: the source is not busy and the session has not finished.</summary>
		public bool CanOpenCaptureLinks => !IsSourceBusy && Phase!=CaptureSessionPhase.Finished && !_isClosing;
		#endregion Properties - indicator

		#region Properties - commands
		///<summary>Whether the command's button shows: Retake, Skip, Next and Previous only with a mount; none once the session finished.</summary>
		public bool IsCommandVisible(CaptureSessionAction action) {
			if(Phase==CaptureSessionPhase.Finished || _isClosing) {
				return false;
			}
			switch(action) {
				case CaptureSessionAction.Retake:
				case CaptureSessionAction.Skip:
				case CaptureSessionAction.NextSlot:
				case CaptureSessionAction.PreviousSlot:
					return !_controller.IsSingleTarget;
				default:
					return true;
			}
		}

		public bool IsCommandEnabled(CaptureSessionAction action) {
			return IsCommandVisible(action) && _controller.CanExecute(action);
		}

		///<summary>"Retake (F9)": the command's name and its [Keys] chord when one is bound.</summary>
		public string GetCommandText(CaptureSessionAction action) {
			string text;
			switch(action) {
				case CaptureSessionAction.Retake: text="Retake"; break;
				case CaptureSessionAction.Skip: text="Skip"; break;
				case CaptureSessionAction.NextSlot: text="Next slot"; break;
				case CaptureSessionAction.PreviousSlot: text="Previous slot"; break;
				case CaptureSessionAction.Rotate: text="Rotate"; break;
				case CaptureSessionAction.Disable: text="Disable"; break;
				default: text="End"; break;
			}
			CaptureKeyChord chord=_keyBindings.GetChord(ToKey(action));
			return chord==null ? text : text+" ("+chord.Text+")";
		}

		///<summary>"Always rotate this slot" (design 7.3 step 5): shown only with a mount definition from this call and the Setup permission,
		///offered after a correction.</summary>
		public bool IsAlwaysRotateVisible => _controller.MountDef!=null && !_controller.IsSingleTarget && Phase!=CaptureSessionPhase.Finished && !_isClosing
			&& _controller.IsSetupAuthorized;
		public bool IsAlwaysRotateEnabled => IsAlwaysRotateVisible && _controller.CanAlwaysRotateSlot;
		#endregion Properties - commands

		#region Properties - slots and teeth
		///<summary>The mini-map shows with a mount; the tooth picker without one.</summary>
		public bool IsSlotMapVisible => !_controller.IsSingleTarget;
		public bool IsToothPickerVisible => _controller.IsSingleTarget;
		///<summary>The teeth can be changed between images (CaptureSessionController.CanEditToothNumbers).</summary>
		public bool IsToothPickerEnabled => _controller.CanEditToothNumbers && !_isClosing;
		public string ToothNumbersText => _controller.ToothNumbers;
		///<summary>Why the last tooth entry was refused, or "".</summary>
		public string ToothErrorText => _toothErrorText;
		#endregion Properties - slots and teeth

		#region Properties - redelivered image
		///<summary>A redelivered image waits for the operator (design 7.12): the confirm and decline panel shows.</summary>
		public bool IsRedeliveredVisible => _controller.HasRedeliveredPending && !_isClosing;
		public string RedeliveredText => _controller.RedeliveredView?.Text??"";
		///<summary>"Acquired: ...", "Capture ID: ..." on two lines.</summary>
		public string RedeliveredDetailsText {
			get {
				CaptureRedeliveredView view=_controller.RedeliveredView;
				if(view==null) {
					return "";
				}
				return "Acquired: "+view.AcquiredAtText+"\r\nCapture ID: "+view.CaptureId;
			}
		}
		///<summary>"2 redelivered images wait." when more than one waits, else "".</summary>
		public string RedeliveredCountText {
			get {
				CaptureRedeliveredView view=_controller.RedeliveredView;
				if(view==null || view.CountPending<2) {
					return "";
				}
				return view.CountPending.ToString(CultureInfo.InvariantCulture)+" redelivered images wait.";
			}
		}
		public bool IsConfirmRedeliveredEnabled => IsRedeliveredVisible && _controller.CanConfirmRedelivered;
		public bool IsDeclineRedeliveredEnabled => IsRedeliveredVisible && _controller.CanDeclineRedelivered;
		#endregion Properties - redelivered image

		#region Properties - preview
		///<summary>The image the preview shows: a redelivered image that waits, else the image saved last (null after it was retaken).</summary>
		public AcquiredImage PreviewImage {
			get {
				if(_controller.HasRedeliveredPending) {
					return _controller.RedeliveredView?.Image;
				}
				return _controller.LastSavedDocument==null ? null : _controller.LastSavedImage;
			}
		}

		///<summary>The display rotation of the preview (design 7.3 step 4, 7.10): the slot's RotateOnAcquire and the operator's Rotate, as the
		///document stores them in DegreesRotated. A redelivered image is shown as delivered.</summary>
		public int PreviewDegrees => _controller.HasRedeliveredPending ? 0 : _controller.DegreesRotatedShown;
		///<summary>The mount's FlipOnAcquire, as the document stores it (mirrored first, then turned, as ImageHelper draws it).</summary>
		public bool IsPreviewFlipped => !_controller.HasRedeliveredPending && _controller.LastSavedDocument!=null && _controller.LastSavedDocument.IsFlipped;

		///<summary>"Last image: 3 of 18: Max R, #6", "Last image: Single image, #8", "Redelivered image, not placed yet", or "".</summary>
		public string PreviewCaption {
			get {
				if(_controller.HasRedeliveredPending) {
					return "Redelivered image, not placed yet";
				}
				Document document=_controller.LastSavedDocument;
				if(document==null || _controller.LastSavedImage==null) {
					return "";
				}
				MountItem mountItem=_controller.LastSavedMountItem;
				if(mountItem==null || _controller.MountSlots==null) {
					return "Last image: "+CaptureTargetText.GetSingleText(document.ToothNumbers);
				}
				return "Last image: "+CaptureTargetText.GetSlotText(_controller.MountSlots.ListMountItems,mountItem);
			}
		}

		///<summary>The image was saved less than PreviewFreshMs ago: the window highlights it.</summary>
		public bool IsPreviewFresh => !_controller.HasRedeliveredPending && _controller.LastSavedDocument!=null
			&& _controller.NowMs-_msPreviewShown<PreviewFreshMs;

		///<summary>The 8-bit picture of PreviewImage (made once per image), or null with error.</summary>
		public CapturePreviewPixels GetPreviewPixels(out string error) {
			AcquiredImage image=PreviewImage;
			bool isInverted=image!=null && image.IsGray16 && !_controller.HasRedeliveredPending && _controller.Options!=null
				&& _controller.Options.Polarity==TwainPolarity.Invert;
			if(image!=_imagePreviewCached || isInverted!=_isInvertedCached) {
				_imagePreviewCached=image;
				_isInvertedCached=isInverted;
				_previewErrorCached="";
				_previewPixelsCached=image==null ? null : CapturePreview.FromImage(image,isInverted,out _previewErrorCached);
			}
			error=_previewErrorCached;
			return _previewPixelsCached;
		}
		#endregion Properties - preview

		#region Properties - summary
		///<summary>The session finished: the summary shows (design 7.3 step 6).</summary>
		public bool IsSummaryVisible => Phase==CaptureSessionPhase.Finished && _controller.Summary!=null;

		///<summary>How the session ended, and when the window closes.</summary>
		public string SummaryText {
			get {
				CaptureSessionSummary summary=_controller.Summary;
				if(!IsSummaryVisible || summary==null) {
					return "";
				}
				string text;
				switch(summary.EndReason) {
					case CaptureSessionEndReason.MountFull:
						text="Every slot of the mount has an image.";
						break;
					case CaptureSessionEndReason.Ended:
						text="The session was ended.";
						break;
					case CaptureSessionEndReason.CancelledBeforeStart:
						text="Nothing was taken.";
						break;
					default:
						text="The window was closed before the session finished.";
						break;
				}
				int seconds=SecondsToClose;
				if(seconds>0) {
					text+=" This window closes in "+seconds.ToString(CultureInfo.InvariantCulture)+" s.";
				}
				return text;
			}
		}

		///<summary>Seconds until the summary closes the window by itself, 0 when it is due or the session has not finished.</summary>
		public int SecondsToClose {
			get {
				if(!IsSummaryVisible || _msFinished==null || _controller.Summary.EndReason==CaptureSessionEndReason.CancelledBeforeStart) {
					return 0;
				}
				long msLeft=SummaryCloseMs-(_controller.NowMs-_msFinished.Value);
				if(msLeft<=0) {
					return 0;
				}
				return (int)((msLeft+999)/1000);
			}
		}

		///<summary>The window should close now: the summary has shown SummaryCloseMs, or the session ended before anything was started.</summary>
		public bool IsCloseDue {
			get {
				if(!IsSummaryVisible || _isClosing) {
					return false;
				}
				if(_controller.Summary.EndReason==CaptureSessionEndReason.CancelledBeforeStart) {
					return true;
				}
				return _msFinished!=null && _controller.NowMs-_msFinished.Value>=SummaryCloseMs;
			}
		}
		#endregion Properties - summary

		#region Methods - commands
		///<summary>The window has loaded: the banner shows.</summary>
		public void Begin() {
			_controller.Begin();
			Track();
		}

		///<summary>The window's timer, about every 100 ms: the controller's timers, the hotkeys, the preview's freshness and the summary's countdown.</summary>
		public void OnTick() {
			if(_isClosing) {
				return;
			}
			_controller.OnTick();
			Track();
			bool isPreviewFresh=IsPreviewFresh;
			int secondsToClose=SecondsToClose;
			if(isPreviewFresh!=_isPreviewFreshShown || secondsToClose!=_secondsToCloseShown) {
				_isPreviewFreshShown=isPreviewFresh;
				_secondsToCloseShown=secondsToClose;
				NotifyOwnChange();
			}
		}

		public bool RequestStart() {
			return !_isClosing && _controller.RequestStart();
		}

		public void ConfirmOlderMount() {
			_controller.ConfirmOlderMount();
		}

		public void DeclineOlderMount() {
			_controller.DeclineOlderMount();
		}

		public bool SetProvider(long provNum) {
			return _controller.SetProvider(provNum);
		}

		public bool Execute(CaptureSessionAction action) {
			return !_isClosing && _controller.Execute(action);
		}

		public bool AlwaysRotateSlot() {
			return !_isClosing && _controller.AlwaysRotateSlot();
		}

		///<summary>A tap on a slot of the mini-map.</summary>
		public bool SelectSlot(int idx) {
			return !_isClosing && _controller.SelectSlot(idx);
		}

		public bool RequestRedelivery() {
			return !_isClosing && _controller.RequestRedelivery();
		}

		public bool ConfirmRedelivered() {
			return !_isClosing && _controller.ConfirmRedelivered();
		}

		public bool DeclineRedelivered() {
			return !_isClosing && _controller.DeclineRedelivered();
		}

		///<summary>After the quarantine or Recover window closed: the count again.</summary>
		public void RefreshUnassignedText() {
			_controller.RefreshUnassignedText();
		}
		#endregion Methods - commands

		#region Methods - teeth
		///<summary>The 32 permanent teeth of the picker, maxillary 1-16 then mandibular 32-17, with the banner's selection.</summary>
		public List<CaptureToothTile> GetToothTiles() {
			HashSet<string> hashSetSelected=GetSelectedTeeth();
			List<CaptureToothTile> listTiles=new List<CaptureToothTile>();
			for(int column=0;column<16;column++) {
				string upper=(column+1).ToString(CultureInfo.InvariantCulture);
				string lower=(32-column).ToString(CultureInfo.InvariantCulture);
				listTiles.Add(new CaptureToothTile { Universal=upper,Row=0,Column=column,IsSelected=hashSetSelected.Contains(upper) });
				listTiles.Add(new CaptureToothTile { Universal=lower,Row=1,Column=column,IsSelected=hashSetSelected.Contains(lower) });
			}
			return listTiles.OrderBy(x => x.Row).ThenBy(x => x.Column).ToList();
		}

		///<summary>A tap on a tooth of the picker: it is added to the banner's teeth, or removed. False, with ToothErrorText, when they cannot be
		///changed now.</summary>
		public bool ToggleTooth(string universal) {
			List<ToothInfo> listTeeth;
			string error;
			if(!Dicom.ToothNumbers.TryParseList(_controller.ToothNumbers,out listTeeth,out error)) {
				listTeeth=new List<ToothInfo>();
			}
			List<string> listUniversal=listTeeth.Select(x => x.Universal).ToList();
			if(listUniversal.Contains(universal)) {
				listUniversal.Remove(universal);
			}
			else {
				listUniversal.Add(universal);
			}
			return SetToothNumbersText(string.Join(",",listUniversal.OrderBy(x => GetToothSortKey(x))));
		}

		///<summary>The banner's teeth as typed (Universal, separated by commas; primary teeth A-T too). False, with ToothErrorText, when they do
		///not read or cannot be changed now.</summary>
		public bool SetToothNumbersText(string text) {
			string error;
			bool isSet=_controller.SetToothNumbers(text??"",out error);
			string errorText=isSet ? "" : (error??"");
			if(errorText!=_toothErrorText) {
				_toothErrorText=errorText;
				NotifyOwnChange();
			}
			return isSet;
		}

		private HashSet<string> GetSelectedTeeth() {
			List<ToothInfo> listTeeth;
			string error;
			if(!Dicom.ToothNumbers.TryParseList(_controller.ToothNumbers,out listTeeth,out error)) {
				return new HashSet<string>();
			}
			return new HashSet<string>(listTeeth.Select(x => x.Universal));
		}

		///<summary>Permanent teeth by number, then primary teeth A-T.</summary>
		private static int GetToothSortKey(string universal) {
			int number;
			if(int.TryParse(universal,NumberStyles.None,CultureInfo.InvariantCulture,out number)) {
				return number;
			}
			return 100+(string.IsNullOrEmpty(universal) ? 0 : char.ToUpperInvariant(universal[0])-'A');
		}
		#endregion Methods - teeth

		#region Methods - slots
		///<summary>The mini-map's tiles in an area of the given size: every capture slot of the mount in ItemOrder (CaptureSessionController.
		///GetSlotViews), placed where the mount places it when the slots have a size, otherwise in a grid in ItemOrder.</summary>
		public List<CaptureSlotTile> GetSlotTiles(double widthArea,double heightArea) {
			List<CaptureSlotTile> listTiles=new List<CaptureSlotTile>();
			List<CaptureSlotView> listViews=_controller.GetSlotViews();
			if(listViews.Count==0 || widthArea<1 || heightArea<1) {
				return listTiles;
			}
			bool isFinished=Phase==CaptureSessionPhase.Finished || _isClosing;
			foreach(CaptureSlotView view in listViews) {
				CaptureSlotTileState state;
				if(view.IsFilled) {
					state=CaptureSlotTileState.Filled;
				}
				else if(view.IsTarget) {
					state=CaptureSlotTileState.Target;
				}
				else if(view.IsSkipped) {
					state=CaptureSlotTileState.Skipped;
				}
				else {
					state=CaptureSlotTileState.Empty;
				}
				string teeth=(view.MountItem.ToothNumbers??"").Trim();
				listTiles.Add(new CaptureSlotTile {
					Idx=view.Idx,
					Number=view.MountItem.ItemOrder.ToString(CultureInfo.InvariantCulture),
					TeethText=teeth=="" ? "" : "#"+teeth,
					ToolTip=CaptureTargetText.GetSlotText(_controller.MountSlots.ListMountItems,view.MountItem),
					State=state,
					IsEnableTarget=view.IsEnableTarget,
					IsClickable=!isFinished && !view.IsFilled && !view.IsTarget,
				});
			}
			List<MountItem> listMountItems=listViews.Select(x => x.MountItem).ToList();
			if(HasMountLayout(listMountItems)) {
				PlaceAsMounted(listTiles,listMountItems,widthArea,heightArea);
			}
			else {
				PlaceInGrid(listTiles,widthArea,heightArea);
			}
			return listTiles;
		}

		///<summary>Every slot has a size and no two slots overlap: the mini-map can look like the mount.</summary>
		private static bool HasMountLayout(List<MountItem> listMountItems) {
			if(listMountItems.Any(x => x.Width<=0 || x.Height<=0)) {
				return false;
			}
			for(int i=0;i<listMountItems.Count;i++) {
				for(int j=i+1;j<listMountItems.Count;j++) {
					MountItem a=listMountItems[i];
					MountItem b=listMountItems[j];
					bool isOverlap=a.Xpos<b.Xpos+b.Width && b.Xpos<a.Xpos+a.Width && a.Ypos<b.Ypos+b.Height && b.Ypos<a.Ypos+a.Height;
					if(isOverlap) {
						return false;
					}
				}
			}
			return true;
		}

		///<summary>Scales the slots' own rectangles (their bounding box) into the area, centred, with a small gap.</summary>
		private static void PlaceAsMounted(List<CaptureSlotTile> listTiles,List<MountItem> listMountItems,double widthArea,double heightArea) {
			double xMin=listMountItems.Min(x => x.Xpos);
			double yMin=listMountItems.Min(x => x.Ypos);
			double xMax=listMountItems.Max(x => x.Xpos+x.Width);
			double yMax=listMountItems.Max(x => x.Ypos+x.Height);
			double scale=Math.Min(widthArea/(xMax-xMin),heightArea/(yMax-yMin));
			double xOffset=(widthArea-(xMax-xMin)*scale)/2;
			double yOffset=(heightArea-(yMax-yMin)*scale)/2;
			const double gap=1;
			for(int i=0;i<listTiles.Count;i++) {
				MountItem mountItem=listMountItems[i];
				listTiles[i].Left=xOffset+(mountItem.Xpos-xMin)*scale+gap;
				listTiles[i].Top=yOffset+(mountItem.Ypos-yMin)*scale+gap;
				listTiles[i].Width=Math.Max(4,mountItem.Width*scale-2*gap);
				listTiles[i].Height=Math.Max(4,mountItem.Height*scale-2*gap);
			}
		}

		///<summary>Square tiles in ItemOrder, row by row, as large as fit.</summary>
		private static void PlaceInGrid(List<CaptureSlotTile> listTiles,double widthArea,double heightArea) {
			int count=listTiles.Count;
			int columnsBest=1;
			double sizeBest=0;
			for(int columns=1;columns<=count;columns++) {
				int rows=(count+columns-1)/columns;
				double size=Math.Min(widthArea/columns,heightArea/rows);
				if(size>sizeBest) {
					sizeBest=size;
					columnsBest=columns;
				}
			}
			const double gap=2;
			for(int i=0;i<count;i++) {
				listTiles[i].Left=(i%columnsBest)*sizeBest+gap;
				listTiles[i].Top=(i/columnsBest)*sizeBest+gap;
				listTiles[i].Width=Math.Max(4,sizeBest-2*gap);
				listTiles[i].Height=Math.Max(4,sizeBest-2*gap);
			}
		}
		#endregion Methods - slots

		#region Methods - keys and hotkeys
		///<summary>A key went down while the window has the focus (vk: the Windows virtual-key code; isRepeat: an auto-repeat). True when it is a
		///bound [Keys] chord, which the window then marks handled so that it never reaches a focused control, whether or not the controller did
		///anything with it.</summary>
		public bool OnKeyDown(int virtualKey,CaptureKeyModifiers modifiers,bool isRepeat) {
			if(_isClosing || CaptureKeyParser.IsModifierKey(virtualKey)) {
				return false;
			}
			CaptureSessionKey? key=_keyBindings.Find(virtualKey,modifiers);
			if(key==null) {
				return false;
			}
			_controller.OnKeyDown(key.Value,_controller.NowMs,isRepeat);
			Track();
			return true;
		}

		///<summary>A key went up while the window has the focus. The Confirm key's release goes to the controller (it may start on release). True
		///for any bound key.</summary>
		public bool OnKeyUp(int virtualKey) {
			if(_isClosing) {
				return false;
			}
			if(_keyBindings.IsConfirmKey(virtualKey)) {
				_controller.OnKeyUp(CaptureSessionKey.Confirm,_controller.NowMs);
				Track();
				return true;
			}
			return _keyBindings.GetSessionKeysBound().Any(x => _keyBindings.GetChord(x).VirtualKey==virtualKey);
		}

		///<summary>WM_HOTKEY with wParam id (a session key while the source is enabled, also when the sensor software has the foreground). True when
		///it was one of the session's hotkeys.</summary>
		public bool OnHotkey(int id) {
			if(_isClosing) {
				return false;
			}
			CaptureSessionKey? key=_hotkeys.GetKey(id);
			if(key==null) {
				return false;
			}
			_controller.OnKeyDown(key.Value,_controller.NowMs,false);
			Track();
			return true;
		}

		///<summary>The window's handle is known: the hotkeys register on it (null: the handle is gone).</summary>
		public void SetHotkeyRegistrar(ICaptureHotkeyRegistrar registrar) {
			_hotkeys.SetRegistrar(registrar);
			SyncHotkeys();
		}

		///<summary>Releases every hotkey (the window closing, or a failure path). Safe to call more than once.</summary>
		public void ReleaseHotkeys() {
			_hotkeys.ReleaseAll();
		}
		#endregion Methods - keys and hotkeys

		#region Methods - closing
		///<summary>The operator clicked the window's X. While the source is enabled that is End (with the design 7.6 warning first when disabling is
		///not safe), and the window stays open until the session finishes: false. Otherwise the window may close: true (FormClosing then runs
		///OnClosing).</summary>
		public bool OnCloseRequestedByOperator() {
			if(_isClosing || Phase!=CaptureSessionPhase.Enabled) {
				return true;
			}
			_controller.Execute(CaptureSessionAction.End);
			Track();
			return false;
		}

		///<summary>The window is closing (FormClosing: after the summary, the operator, or forced by a logoff). The hotkeys are released and a
		///session that has not finished takes the controller's forced-close path, which never waits for the worker.</summary>
		public void OnClosing() {
			if(_isClosing) {
				return;
			}
			_isClosing=true;
			_hotkeys.ReleaseAll();
			try {
				if(_controller.Phase!=CaptureSessionPhase.Finished) {
					_controller.OnClosing();
				}
			}
			finally {
				_hotkeys.ReleaseAll();
				_controller.Changed-=controller_Changed;
				_controller.SoundRequested-=controller_SoundRequested;
			}
		}
		#endregion Methods - closing

		#region Methods - private
		private void controller_Changed() {
			Track();
			Changed?.Invoke();
		}

		private void controller_SoundRequested(CaptureSound sound) {
			if(_isClosing) {
				return;
			}
			_soundPlayer.Play(sound);
		}

		///<summary>Follows the controller: the hotkeys while the source is enabled, a new preview, the finish.</summary>
		private void Track() {
			SyncHotkeys();
			if(_controller.LastSavedDocument!=_documentPreviewTracked) {
				_documentPreviewTracked=_controller.LastSavedDocument;
				if(_documentPreviewTracked!=null) {
					_msPreviewShown=_controller.NowMs;
					_isPreviewFreshShown=true;//OnTick notices when it is no longer fresh
				}
				_version++;
			}
			if(Phase==CaptureSessionPhase.Finished && _msFinished==null) {
				_msFinished=_controller.NowMs;
				_version++;
			}
		}

		private void SyncHotkeys() {
			_hotkeys.SetActive(!_isClosing && _controller.AreSessionKeysActive);
		}

		private void SyncControllerVersion() {
			if(_controller.Version!=_versionControllerSeen) {
				_versionControllerSeen=_controller.Version;
				_version++;
			}
		}

		private void NotifyOwnChange() {
			_version++;
			Changed?.Invoke();
		}

		private static CaptureSessionKey ToKey(CaptureSessionAction action) {
			switch(action) {
				case CaptureSessionAction.Retake: return CaptureSessionKey.Retake;
				case CaptureSessionAction.Skip: return CaptureSessionKey.Skip;
				case CaptureSessionAction.NextSlot: return CaptureSessionKey.NextSlot;
				case CaptureSessionAction.PreviousSlot: return CaptureSessionKey.PreviousSlot;
				case CaptureSessionAction.Rotate: return CaptureSessionKey.Rotate;
				case CaptureSessionAction.Disable: return CaptureSessionKey.Disable;
				default: return CaptureSessionKey.End;
			}
		}
		#endregion Methods - private
	}
}
