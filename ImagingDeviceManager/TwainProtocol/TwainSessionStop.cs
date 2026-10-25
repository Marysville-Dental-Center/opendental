// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ImagingDeviceManager.Imaging;

namespace ImagingDeviceManager.TwainProtocol {
	//The nested message loop, and every stop the library starts itself: Cancel, the safe order of design 7.1, and the wait after a
	//refused disable.
	internal sealed partial class TwainSession {
		///<summary>Wakes the message loop regularly so that stop requests are noticed even when no other message arrives.</summary>
		private const int TimerIdTick=0x7A12;
		///<summary>Posted to the proxy window by Cancel to wake the message loop. Private to this library (WM_APP range).</summary>
		private const int WM_APP_CANCEL=0x8000+0x0A11;
		///<summary>A source that keeps offering images cannot keep a stop going forever.</summary>
		private const int MaxDrainImages=500;
		private readonly object _lockCancel=new object();
		///<summary>The proxy window while an acquisition runs; Cancel posts to it. Guarded by _lockCancel.</summary>
		private IntPtr _hwndCancel;
		private volatile bool _isCancelRequested;
		///<summary>Why the message loop stopped without a notification, for the code that runs after it.</summary>
		private TwainStopReason _stopReason;
		private readonly Stopwatch _stopwatch=Stopwatch.StartNew();
		///<summary>WM_QUIT arrived while the session had to keep its message loop running; it is posted again afterwards.</summary>
		private bool _isQuitPending;
		private int _quitExitCode;
		///<summary>On the by-name path, the window passed to Acquire when the source was enabled. Once it is destroyed (an automatic logoff
		///closes the hidden parent form, for example), the acquisition stops and nothing more goes back to the caller.</summary>
		private IntPtr _hwndWatched;
		///<summary>When the [Twain] AcquireTimeoutMinutes stop is due (NowMs), while the loop waits for the notification. 0: none.</summary>
		private long _acquireDeadlineMs;

		///<summary>How often the message loop wakes up to check for stop requests, in milliseconds.</summary>
		public int TickMilliseconds { get; set; }=200;
		///<summary>Why the library itself stopped the last acquisition (or the last close with images pending). None when it did not.</summary>
		public TwainStopReason LastStopReason { get; private set; }
		///<summary>Images handed to DrainSink since the last Acquire started.</summary>
		public int DrainedImageCount { get; private set; }
		///<summary>True while the session waits for the source after a refused MSG_DISABLEDS.</summary>
		public bool IsWaitingForSource { get; private set; }
		///<summary>True when, since the last Acquire started, the source may still hold images of this series that the library did not
		///receive: a wait after a refused disable ended without a notification and the second consecutive MSG_DISABLEDS was sent, or, on
		///the by-name path with a sink, a pending image could not be transferred or was discarded with MSG_RESET.</summary>
		public bool SourceMayHoldImages { get; private set; }
		///<summary>Raised with true when the session starts waiting for the source after a refused MSG_DISABLEDS (a status window then
		///says "Waiting for the sensor software to finish."), and with false when the wait ends. Handlers must not throw.</summary>
		public event Action<bool> WaitingForSourceChanged;

		private long NowMs => _stopwatch.ElapsedMilliseconds;

		///<summary>True when images the caller cannot take go to DrainSink instead of being discarded with MSG_RESET: only on the by-name
		///path, and only with a sink. Scans keep discarding them.</summary>
		private bool CanDrain => IsOpenedByName && DrainSink!=null;

		#region Cancel
		///<summary>Asks the running acquisition to stop in the safe order (design 7.1). Safe to call from any thread: from the Cancel
		///button of a status window, which runs inside the acquisition's message loop, or from another thread. Ignored when no
		///acquisition is running. The acquisition then returns null with EZTEC_USER_CANCEL, unless its image had already arrived.</summary>
		public void Cancel() {
			lock(_lockCancel) {
				if(_hwndCancel==IntPtr.Zero) {
					return;
				}
				_isCancelRequested=true;
				NativeMethods.PostMessage(_hwndCancel,WM_APP_CANCEL,IntPtr.Zero,IntPtr.Zero);
			}
		}

		///<summary>Called by Acquire and AcquireAll once the source is open: a Cancel from before this call does not count.</summary>
		private void BeginAcquire() {
			lock(_lockCancel) {
				_isCancelRequested=false;
				_hwndCancel=_proxyWindow==null ? IntPtr.Zero : _proxyWindow.Handle;
			}
			_stopReason=TwainStopReason.None;
			LastStopReason=TwainStopReason.None;
			DrainedImageCount=0;
			EndConditionCode=null;//The next image of a batch comes without an enable; its Acquire must not report an earlier end.
			EndConditionText=null;
			SourceMayHoldImages=false;
		}

		private void EndAcquire() {
			lock(_lockCancel) {
				_hwndCancel=IntPtr.Zero;
				_isCancelRequested=false;
			}
		}

		///<summary>The stop the session must start now, or None. Checked on every pass of the message loop and before every image goes
		///back to the caller.</summary>
		private TwainStopReason GetStopRequest() {
			if(_hwndWatched!=IntPtr.Zero && !NativeMethods.IsWindow(_hwndWatched)) {
				return TwainStopReason.ParentDestroyed;
			}
			if(_isCancelRequested) {
				return TwainStopReason.Cancel;
			}
			if(_acquireDeadlineMs>0 && NowMs>=_acquireDeadlineMs) {
				return TwainStopReason.Timeout;
			}
			return TwainStopReason.None;
		}

		///<summary>Before an enable: arms the optional acquire timeout (design 7.1, [Twain] AcquireTimeoutMinutes). It is honoured only on
		///the by-name path, for single acquisitions (CAP_XFERCOUNT=1, and the source accepted that count: isXferCountSet), of a source
		///listed in [Twain] DisableIsSafeSources whose safe-disable MSG_SET succeeded, because only then can the library stop the source
		///without losing an image. Otherwise the log says why it is not.</summary>
		private void ArmAcquireTimeout(int xferCount,bool isXferCountSet) {
			_acquireDeadlineMs=0;
			TwainCaptureSettings settings=Settings??new TwainCaptureSettings();
			if(settings.AcquireTimeout<=TimeSpan.Zero || !IsOpenedByName) {
				return;
			}
			string reasonNotHonoured=null;
			if(xferCount!=1) {
				reasonNotHonoured="it applies to single acquisitions (CAP_XFERCOUNT=1) only";
			}
			else if(!isXferCountSet) {
				reasonNotHonoured="the source did not accept CAP_XFERCOUNT=1, so the acquisition may not be a single one";
			}
			else if(!settings.IsDisableSafeSource(SourceName)) {
				reasonNotHonoured="the source is not listed in [Twain] DisableIsSafeSources";
			}
			else if(!IsSafeDisableInEffect) {
				reasonNotHonoured="the source did not accept the safe-disable capability";
			}
			if(reasonNotHonoured!=null) {
				Log("[Twain] AcquireTimeoutMinutes is not honoured for \""+SourceName+"\": "+reasonNotHonoured+".");
				return;
			}
			_acquireDeadlineMs=NowMs+(long)settings.AcquireTimeout.TotalMilliseconds;
		}

		///<summary>Before an enable: on the by-name path, watch the window the caller passed (design 7.1 "Parent destroyed or logoff").
		///Scans keep their behaviour.</summary>
		private void WatchParent(IntPtr hwnd) {
			_hwndWatched=IsOpenedByName && hwnd!=IntPtr.Zero && NativeMethods.IsWindow(hwnd) ? hwnd : IntPtr.Zero;
		}

		///<summary>Posts WM_QUIT again once the source is stopped, if the session's message loop took it; the program then closes as it
		///would have. Called at the end of every public call that can run the loop.</summary>
		private void RepostQuitIfPending() {
			if(!_isQuitPending) {
				return;
			}
			_isQuitPending=false;
			NativeMethods.PostQuitMessage(_quitExitCode);
		}

		///<summary>The error an acquisition stopped by the library returns, unless the loop already set one. Every stop ends the series in
		///the callers, which treat EZTEC_USER_CANCEL as a silent end.</summary>
		private void SetStopError(TwainStopReason reason) {
			if(ErrorCode!=EZTwainErrorCode.EZTEC_NONE) {
				return;
			}
			switch(reason) {
				case TwainStopReason.Cancel:
					SetError(EZTwainErrorCode.EZTEC_USER_CANCEL,"The acquisition was cancelled.");
					return;
				case TwainStopReason.ParentDestroyed:
					SetError(EZTwainErrorCode.EZTEC_USER_CANCEL,"The window that started the acquisition was closed.");
					return;
				case TwainStopReason.Quit:
					SetError(EZTwainErrorCode.EZTEC_USER_CANCEL,"The program is closing.");
					return;
				case TwainStopReason.Timeout:
					SetError(EZTwainErrorCode.EZTEC_USER_CANCEL,"No image arrived within the acquire timeout ([Twain] AcquireTimeoutMinutes);"
						+" the acquisition was stopped.");
					return;
				default:
					SetError(EZTwainErrorCode.EZTEC_INTERNAL,"The acquisition stopped.");
					return;
			}
		}
		#endregion Cancel

		#region Message loop
		///<summary>Pumps messages while the source is enabled, handing every message to it (DAT_EVENT / MSG_PROCESSEVENT). Returns
		///MSG_XFERREADY or MSG_CLOSEDSREQ from the source, or MSG_NULL when it stopped for another reason: then _stopReason says why
		///(Error with the error set when the loop itself failed).</summary>
		private ushort RunMessageLoop() {
			return PumpMessages(false,0);
		}

		///<summary>The nested message loop. Waiting for the transfer (isRefusalWait false) it also stops on a stop request. Waiting after
		///a refused disable (isRefusalWait true) it ignores stop requests (the stop is already under way), keeps going after WM_QUIT
		///(posted again afterwards) and returns MSG_NULL at deadlineMs or when the source stops responding; it sets no error.</summary>
		private ushort PumpMessages(bool isRefusalWait,long deadlineMs) {
			IntPtr pMsg=Marshal.AllocHGlobal(NativeMethods.MsgBufferSize);
			IntPtr pEvent=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TW_EVENT)));
			IntPtr hwndProxy=_proxyWindow.Handle;
			bool hasTimeoutTimer=false;
			int countFailures=0;
			try {
				NativeMethods.SetTimer(hwndProxy,(UIntPtr)TimerIdTick,(uint)Math.Max(10,TickMilliseconds),IntPtr.Zero);
				if(LoopTimeoutMilliseconds>0 && !isRefusalWait) {
					NativeMethods.SetTimer(hwndProxy,(UIntPtr)TimerIdTimeout,(uint)LoopTimeoutMilliseconds,IntPtr.Zero);
					hasTimeoutTimer=true;
				}
				while(true) {
					int result=NativeMethods.GetMessage(pMsg,IntPtr.Zero,0,0);
					if(result==-1) {
						string error="Windows message loop error "+Marshal.GetLastWin32Error()+".";
						if(isRefusalWait) {
							Log(error);
							return Tw.MSG_NULL;
						}
						SetError(EZTwainErrorCode.EZTEC_INTERNAL,error);
						_stopReason=TwainStopReason.Error;
						return Tw.MSG_NULL;
					}
					if(result==0 && isRefusalWait) {
						//WM_QUIT while the source finishes: keep waiting (GetMessage works again once WM_QUIT is read) and post it again
						//afterwards, so the program still closes.
						_isQuitPending=true;
						_quitExitCode=Marshal.ReadInt32(pMsg,NativeMethods.MsgOffsetWParam);
						continue;
					}
					if(result==0) {
						//WM_QUIT: the program is shutting down (for example an automatic logoff). Stop in the safe order; the quit message
						//is posted again for the outer loop once the source is stopped (RepostQuitIfPending).
						_isQuitPending=true;
						_quitExitCode=Marshal.ReadInt32(pMsg,NativeMethods.MsgOffsetWParam);
						_stopReason=TwainStopReason.Quit;
						return Tw.MSG_NULL;
					}
					IntPtr hwndMsg=Marshal.ReadIntPtr(pMsg,NativeMethods.MsgOffsetHwnd);
					int message=Marshal.ReadInt32(pMsg,NativeMethods.MsgOffsetMessage);
					IntPtr wParam=Marshal.ReadIntPtr(pMsg,NativeMethods.MsgOffsetWParam);
					if(hasTimeoutTimer && message==NativeMethods.WM_TIMER && hwndMsg==hwndProxy && wParam==(IntPtr)TimerIdTimeout) {
						SetError(EZTwainErrorCode.EZTEC_INTERNAL,"Timed out waiting for the scanner.");
						_stopReason=TwainStopReason.Error;
						return Tw.MSG_NULL;
					}
					if(isRefusalWait) {
						if(NowMs>=deadlineMs) {
							return Tw.MSG_NULL;
						}
					}
					else {
						TwainStopReason stopReason=GetStopRequest();
						if(stopReason!=TwainStopReason.None) {
							_stopReason=stopReason;
							return Tw.MSG_NULL;
						}
					}
					if(hwndMsg==hwndProxy && (message==WM_APP_CANCEL || (message==NativeMethods.WM_TIMER && wParam==(IntPtr)TimerIdTick))) {
						continue;//The library's own wake-up: not for the source.
					}
					TW_EVENT twEvent=new TW_EVENT { pEvent=pMsg,TWMessage=Tw.MSG_NULL };
					Marshal.StructureToPtr(twEvent,pEvent,false);
					ushort rc=SourceEntry(Tw.DG_CONTROL,Tw.DAT_EVENT,Tw.MSG_PROCESSEVENT,pEvent);
					twEvent=(TW_EVENT)Marshal.PtrToStructure(pEvent,typeof(TW_EVENT));
					if(twEvent.TWMessage==Tw.MSG_XFERREADY || twEvent.TWMessage==Tw.MSG_CLOSEDSREQ) {
						return twEvent.TWMessage;
					}
					if(rc==Tw.TWRC_FAILURE) {
						//Normal answers are DSEVENT or NOTDSEVENT. A source that keeps saying it is not open (or not enabled)
						//is gone; without this check the program would wait forever with its window disabled. Other failures
						//are ignored because some sources answer FAILURE for messages that are not theirs.
						ushort conditionCode=LastConditionCode;
						if(conditionCode==Tw.TWCC_BADDEST || conditionCode==Tw.TWCC_NODS || conditionCode==Tw.TWCC_SEQERROR) {
							countFailures++;
						}
						else {
							countFailures=0;
						}
						if(countFailures>=MaxConsecutiveEventFailures) {
							string error="The scanner stopped responding. "+GetConditionText(conditionCode);
							if(isRefusalWait) {
								Log("\""+SourceName+"\" stopped responding while the session waited for it.");
								return Tw.MSG_NULL;
							}
							SetError(EZTwainErrorCode.EZTEC_DS_FAILURE,error);
							_stopReason=TwainStopReason.Error;
							return Tw.MSG_NULL;
						}
					}
					else {
						countFailures=0;
					}
					if(rc==Tw.TWRC_DSEVENT) {
						continue;//The source handled the message (its own dialog).
					}
					NativeMethods.TranslateMessage(pMsg);
					NativeMethods.DispatchMessage(pMsg);
				}
			}
			finally {
				NativeMethods.KillTimer(hwndProxy,(UIntPtr)TimerIdTick);
				if(hasTimeoutTimer) {
					NativeMethods.KillTimer(hwndProxy,(UIntPtr)TimerIdTimeout);
				}
				Marshal.FreeHGlobal(pEvent);
				Marshal.FreeHGlobal(pMsg);
			}
		}

		///<summary>One MSG_PROCESSEVENT with a WM_NULL message for the proxy window, outside the loop. When the loop stops in state 5 this
		///gives the source a last chance to report a notification it already has, so that images it offered are transferred instead of
		///dropped by the disable. A source may report its notification on any message (twain-contract 1.1 section 5). Returns
		///MSG_XFERREADY, MSG_CLOSEDSREQ or MSG_NULL.</summary>
		private ushort PollNotification() {
			IntPtr pMsg=Marshal.AllocHGlobal(NativeMethods.MsgBufferSize);
			IntPtr pEvent=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TW_EVENT)));
			try {
				for(int offset=0;offset<NativeMethods.MsgBufferSize;offset+=4) {
					Marshal.WriteInt32(pMsg,offset,0);
				}
				Marshal.WriteIntPtr(pMsg,NativeMethods.MsgOffsetHwnd,_proxyWindow.Handle);
				Marshal.WriteInt32(pMsg,NativeMethods.MsgOffsetMessage,NativeMethods.WM_NULL);
				Marshal.StructureToPtr(new TW_EVENT { pEvent=pMsg,TWMessage=Tw.MSG_NULL },pEvent,false);
				ushort rc=SourceEntry(Tw.DG_CONTROL,Tw.DAT_EVENT,Tw.MSG_PROCESSEVENT,pEvent);
				TW_EVENT twEvent=(TW_EVENT)Marshal.PtrToStructure(pEvent,typeof(TW_EVENT));
				if(rc==Tw.TWRC_DSEVENT && (twEvent.TWMessage==Tw.MSG_XFERREADY || twEvent.TWMessage==Tw.MSG_CLOSEDSREQ)) {
					return twEvent.TWMessage;
				}
				return Tw.MSG_NULL;
			}
			finally {
				Marshal.FreeHGlobal(pEvent);
				Marshal.FreeHGlobal(pMsg);
			}
		}
		#endregion Message loop

		#region Safe stop order
		///<summary>After the message loop stopped without a notification (a stop request, or the loop failed): stops the source in the
		///safe order and sets the error. On the by-name path with a sink, a notification the source already has is picked up first.</summary>
		private void StopAfterLoop() {
			TwainStopReason reason=_stopReason==TwainStopReason.None ? TwainStopReason.Error : _stopReason;
			if(CanDrain && State==5) {
				ushort notification=PollNotification();
				if(notification==Tw.MSG_XFERREADY) {
					State=6;
				}
				else if(notification==Tw.MSG_CLOSEDSREQ) {
					ReadEndCondition();
				}
			}
			StopToState4(reason);
			SetStopError(reason);
		}

		///<summary>Brings the source back to state 4 in the safe order (design 7.1; TWAIN 2.4 pp.3-27 to 3-28 and p.7-103: MSG_DISABLEDS is
		///valid in state 5 only), for every stop the library starts: Cancel, timeout, a destroyed parent window, WM_QUIT, a close with
		///images pending, and errors.
		///- State 7: the transfer is over as far as it got; MSG_ENDXFER ends it.
		///- State 6: on the by-name path with a sink, each pending image is transferred to the sink (an image the source offered is
		///  kept); otherwise, and for scans, MSG_RESET discards them as before.
		///- State 5: MSG_DISABLEDS.
		///On the by-name path every such stop is a capture event and goes to the log (design 7.6, 7.13). Never throws.</summary>
		private void StopToState4(TwainStopReason reason) {
			if(reason!=TwainStopReason.None) {
				LastStopReason=reason;
				if(IsOpenedByName && State>=5) {
					Log("Stopping \""+SourceName+"\" ("+reason+"); safe disable in effect: "+(IsSafeDisableInEffect ? "yes."
						: "no; an exposure the source still held may not be delivered (see the runbook, \"may still hold images\")."));
				}
			}
			try {
				if(State==7) {
					EndTransfer();
				}
				if(State==6) {
					if(CanDrain) {
						DrainPending(TwainDrainReason.Stop,reason);
					}
					else {
						ResetPendingTransfers();
					}
				}
				if(State==5) {
					DisableSourceSafely(reason);
				}
			}
			catch(Exception ex) {
				//The source is in an unknown state. CloseSource will still try to close it.
				Log("Stopping \""+SourceName+"\" failed: "+ex.Message+(CanDrain ? " The source may still hold images of this series." : ""));
				if(CanDrain) {
					NoteMayStillHoldImages(reason);
				}
			}
		}

		///<summary>Brings the session back to state 4 after an unexpected exception, in the safe order.</summary>
		private void AbortToState4() {
			StopToState4(TwainStopReason.Error);
		}

		///<summary>From state 6: transfers every pending image to the drain sink, ending each with MSG_ENDXFER, until the source reports
		///none left (state 5). An image that could not be transferred, or that the MaxDrainImages bound discards, may still be held by the
		///source, so the operator is told (NoteMayStillHoldImages). Draining never changes the caller's error.</summary>
		private void DrainPending(TwainDrainReason drainReason,TwainStopReason stopReason) {
			EZTwainErrorCode errorCodeOld=ErrorCode;
			string errorTextOld=ErrorText;
			bool isAnyNotReceived=false;
			try {
				int count=0;
				while(State==6) {
					if(count>=MaxDrainImages) {
						Log("\""+SourceName+"\" still reports pending images after "+count+"; the rest are discarded with MSG_RESET. The source"
							+" may still hold them.");
						ResetPendingTransfers();
						isAnyNotReceived=true;
						break;
					}
					count++;
					DibImage dib;
					TransferOutcome outcome=TransferOne(out dib);
					if(outcome==TransferOutcome.Done) {
						DeliverToSink(dib,drainReason,stopReason);
					}
					else {
						Log("A pending image from \""+SourceName+"\" could not be transferred ("+drainReason+"): "+ErrorText+" The source may still"
							+" hold it.");
						isAnyNotReceived=true;
					}
				}
			}
			finally {
				SetError(errorCodeOld,errorTextOld);
				if(isAnyNotReceived) {
					NoteMayStillHoldImages(stopReason);
				}
			}
		}

		///<summary>The source may still hold images of this series that the library did not receive: sets SourceMayHoldImages, and the drain
		///sink, when there is one, tells the operator not to re-expose (design 10.6 "Needless re-exposure"). The caller logs why. Never
		///throws.</summary>
		private void NoteMayStillHoldImages(TwainStopReason stopReason) {
			SourceMayHoldImages=true;
			if(DrainSink==null) {
				return;
			}
			try {
				DrainSink.MayStillHoldImages(SourceName,stopReason);
			}
			catch(Exception ex) {
				Log("The drain sink failed: "+ex.Message);
			}
		}

		private void DeliverToSink(DibImage dib,TwainDrainReason drainReason,TwainStopReason stopReason) {
			DrainedImageCount++;
			Log("An image from \""+SourceName+"\" went to the drain sink ("+drainReason
				+(stopReason==TwainStopReason.None ? "" : ", stop: "+stopReason)+").");
			try {
				DrainSink.ImageDrained(new TwainDrainedImage {
					Image=dib,
					Reason=drainReason,
					StopReason=stopReason,
					SourceName=SourceName,
					ReceivedAt=DateTime.Now,
				});
			}
			catch(Exception ex) {
				Log("The drain sink failed: "+ex.Message);
			}
		}

		///<summary>Single mode asked for one image (CAP_XFERCOUNT=1), but the source has more, for example because one of its own settings
		///overrides the count. On the by-name path with a sink they go to the sink with a warning; otherwise MSG_RESET discards them as
		///before.</summary>
		private void HandleExtraImages() {
			if(!CanDrain) {
				ResetPendingTransfers();
				return;
			}
			Log("Warning: \""+SourceName+"\" delivered more images than the one asked for (CAP_XFERCOUNT=1). The extra images go to"
				+" the drain sink instead of being discarded.");
			DrainPending(TwainDrainReason.ExtraImage,TwainStopReason.None);
		}

		///<summary>MSG_DISABLEDS from state 5. On the by-name path a refusal with a code listed in [Twain] RefusedDisableCodes means the
		///source finishes first and then sends a notification (twain-contract 1.1 section 10.2), so the session waits for it
		///(WaitAfterRefusedDisable). Any other refusal while the safe-disable capability is set is a contract deviation, logged and
		///handled the same way, because waiting never loses an image. Otherwise, and for scans, a source that refuses is treated as
		///disabled, as before.</summary>
		private void DisableSourceSafely(TwainStopReason reason) {
			if(SendDisable()) {
				return;
			}
			ushort conditionCode=LastConditionCode;
			if(!IsOpenedByName) {
				SetDisabledState();
				return;
			}
			TwainCaptureSettings settings=Settings??new TwainCaptureSettings();
			bool isListed=settings.ListRefusedDisableCodes.Contains(conditionCode);
			if(!isListed && !IsSafeDisableInEffect) {
				Log("\""+SourceName+"\" refused MSG_DISABLEDS (condition code "+ToHex(conditionCode)+"); it is treated as disabled.");
				SetDisabledState();
				return;
			}
			if(isListed) {
				Log("\""+SourceName+"\" refused MSG_DISABLEDS (condition code "+ToHex(conditionCode)+"). Waiting up to "
					+GetWaitSeconds(settings)+" s for it to finish.");
			}
			else {
				Log("Contract deviation: \""+SourceName+"\" refused MSG_DISABLEDS with condition code "+ToHex(conditionCode)
					+", which [Twain] RefusedDisableCodes does not list, while the safe-disable capability is set. It is handled as a"
					+" refused disable: waiting up to "+GetWaitSeconds(settings)+" s for it to finish.");
			}
			WaitAfterRefusedDisable(reason,settings);
		}

		private static string GetWaitSeconds(TwainCaptureSettings settings) {
			return Math.Max(0,settings.RefusedDisableWait.TotalSeconds).ToString("0.#",System.Globalization.CultureInfo.InvariantCulture);
		}

		///<summary>After a refused MSG_DISABLEDS (design 7.1, twain-contract 1.1 section 10.2): keeps the message loop running for at most
		///Settings.RefusedDisableWait, the library's own bound (the contract asks for at least 20 s).
		///- MSG_XFERREADY: every image offered goes to the drain sink, then MSG_DISABLEDS again. One that arrives with nothing pending
		///  (MSG_GET reports 0) is ignored.
		///- MSG_CLOSEDSREQ: the end condition is read (DAT_STATUS), then MSG_DISABLEDS again.
		///- A disable refused again keeps the wait going, within the same bound.
		///- Neither in time: the second consecutive MSG_DISABLEDS, which the contract plans to succeed, leaving any images with the
		///  source for redelivery; the log and the sink hear that the source may still hold images.
		///Without a sink, offered images are discarded with MSG_RESET (they stay undelivered in the source).</summary>
		private void WaitAfterRefusedDisable(TwainStopReason reason,TwainCaptureSettings settings) {
			long deadlineMs=NowMs+(long)Math.Max(0,settings.RefusedDisableWait.TotalMilliseconds);
			SetWaitingForSource(true);
			try {
				while(true) {
					ushort notification=PumpMessages(true,deadlineMs);
					if(notification==Tw.MSG_XFERREADY) {
						if(State==5) {
							short pending;
							if(TryGetPendingCount(out pending) && pending==0) {
								Log("\""+SourceName+"\" sent MSG_XFERREADY with nothing pending; ignored.");
								continue;
							}
							State=6;
						}
						if(CanDrain) {
							DrainPending(TwainDrainReason.RefusedDisable,reason);
						}
						else {
							ResetPendingTransfers();
						}
					}
					else if(notification==Tw.MSG_CLOSEDSREQ) {
						ReadEndCondition();
					}
					else {
						break;//The bound passed, or the source stopped responding.
					}
					if(State==5 && SendDisable()) {
						return;
					}
					Log("\""+SourceName+"\" refused MSG_DISABLEDS again (condition code "+ToHex(LastConditionCode)+"); still waiting.");
				}
				bool isDisabled=State==5 && SendDisable();
				Log("No notification from \""+SourceName+"\" within "+GetWaitSeconds(settings)+" s after the refused disable. The second"
					+" consecutive MSG_DISABLEDS "+(isDisabled ? "succeeded" : "failed (condition code "+ToHex(LastConditionCode)
					+"); the source is treated as disabled")+". The source may still hold images of this series.");
				if(!isDisabled) {
					SetDisabledState();
				}
				NoteMayStillHoldImages(reason);
			}
			finally {
				SetWaitingForSource(false);
				RepostQuitIfPending();
			}
		}

		private void SetWaitingForSource(bool isWaiting) {
			IsWaitingForSource=isWaiting;
			try {
				WaitingForSourceChanged?.Invoke(isWaiting);
			}
			catch(Exception ex) {
				Log("A WaitingForSourceChanged handler failed: "+ex.Message);
			}
		}

		///<summary>DAT_PENDINGXFERS / MSG_GET. False when the source does not answer it.</summary>
		private bool TryGetPendingCount(out short count) {
			count=0;
			IntPtr pPending=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TW_PENDINGXFERS)));
			try {
				Marshal.StructureToPtr(new TW_PENDINGXFERS(),pPending,false);
				if(SourceEntry(Tw.DG_CONTROL,Tw.DAT_PENDINGXFERS,Tw.MSG_GET,pPending)!=Tw.TWRC_SUCCESS) {
					return false;
				}
				count=((TW_PENDINGXFERS)Marshal.PtrToStructure(pPending,typeof(TW_PENDINGXFERS))).Count;
				return true;
			}
			finally {
				Marshal.FreeHGlobal(pPending);
			}
		}

		///<summary>MSG_DISABLEDS. True, in state 4, when the source accepted it.</summary>
		private bool SendDisable() {
			IntPtr pUserInterface=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TW_USERINTERFACE)));
			ushort rc;
			try {
				TW_USERINTERFACE userInterface=new TW_USERINTERFACE();
				userInterface.ShowUI=(ushort)(HideUI ? 0 : 1);
				userInterface.hParent=_proxyWindow==null ? IntPtr.Zero : _proxyWindow.Handle;
				Marshal.StructureToPtr(userInterface,pUserInterface,false);
				rc=SourceEntry(Tw.DG_CONTROL,Tw.DAT_USERINTERFACE,Tw.MSG_DISABLEDS,pUserInterface);
			}
			finally {
				Marshal.FreeHGlobal(pUserInterface);
			}
			if(rc!=Tw.TWRC_SUCCESS) {
				return false;
			}
			SetDisabledState();
			return true;
		}

		private void SetDisabledState() {
			State=4;
			if(_proxyWindow!=null) {
				_proxyWindow.AttachTo(IntPtr.Zero);
			}
		}
		#endregion Safe stop order
	}
}
