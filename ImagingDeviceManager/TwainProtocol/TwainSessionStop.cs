// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Runtime.InteropServices;
using ImagingDeviceManager.Imaging;

namespace ImagingDeviceManager.TwainProtocol {
	//The nested message loop, and every stop the library starts itself: Cancel, and the safe order of design 7.1.
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

		///<summary>How often the message loop wakes up to check for stop requests, in milliseconds.</summary>
		public int TickMilliseconds { get; set; }=200;
		///<summary>Why the library itself stopped the last acquisition (or the last close with images pending). None when it did not.</summary>
		public TwainStopReason LastStopReason { get; private set; }
		///<summary>Images handed to DrainSink since the last Acquire started.</summary>
		public int DrainedImageCount { get; private set; }

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
		}

		private void EndAcquire() {
			lock(_lockCancel) {
				_hwndCancel=IntPtr.Zero;
				_isCancelRequested=false;
			}
		}

		///<summary>The stop the message loop must start now, or None.</summary>
		private TwainStopReason GetStopRequest() {
			if(_isCancelRequested) {
				return TwainStopReason.Cancel;
			}
			return TwainStopReason.None;
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
			IntPtr pMsg=Marshal.AllocHGlobal(NativeMethods.MsgBufferSize);
			IntPtr pEvent=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TW_EVENT)));
			IntPtr hwndProxy=_proxyWindow.Handle;
			bool hasTimeoutTimer=false;
			int countFailures=0;
			try {
				NativeMethods.SetTimer(hwndProxy,(UIntPtr)TimerIdTick,(uint)Math.Max(10,TickMilliseconds),IntPtr.Zero);
				if(LoopTimeoutMilliseconds>0) {
					NativeMethods.SetTimer(hwndProxy,(UIntPtr)TimerIdTimeout,(uint)LoopTimeoutMilliseconds,IntPtr.Zero);
					hasTimeoutTimer=true;
				}
				while(true) {
					int result=NativeMethods.GetMessage(pMsg,IntPtr.Zero,0,0);
					if(result==-1) {
						SetError(EZTwainErrorCode.EZTEC_INTERNAL,"Windows message loop error "+Marshal.GetLastWin32Error()+".");
						_stopReason=TwainStopReason.Error;
						return Tw.MSG_NULL;
					}
					if(result==0) {
						//WM_QUIT: the program is shutting down. Stop scanning and pass the quit message on to the outer loop.
						int exitCode=Marshal.ReadInt32(pMsg,NativeMethods.MsgOffsetWParam);
						SetError(EZTwainErrorCode.EZTEC_USER_CANCEL,"The program is closing.");
						NativeMethods.PostQuitMessage(exitCode);
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
					TwainStopReason stopReason=GetStopRequest();
					if(stopReason!=TwainStopReason.None) {
						_stopReason=stopReason;
						return Tw.MSG_NULL;
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
							SetError(EZTwainErrorCode.EZTEC_DS_FAILURE,"The scanner stopped responding. "+GetConditionText(conditionCode));
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
		///Never throws.</summary>
		private void StopToState4(TwainStopReason reason) {
			if(reason!=TwainStopReason.None) {
				LastStopReason=reason;
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
				Log("Stopping \""+SourceName+"\" failed: "+ex.Message);
			}
		}

		///<summary>Brings the session back to state 4 after an unexpected exception, in the safe order.</summary>
		private void AbortToState4() {
			StopToState4(TwainStopReason.Error);
		}

		///<summary>From state 6: transfers every pending image to the drain sink, ending each with MSG_ENDXFER, until the source reports
		///none left (state 5). Draining never changes the caller's error.</summary>
		private void DrainPending(TwainDrainReason drainReason,TwainStopReason stopReason) {
			EZTwainErrorCode errorCodeOld=ErrorCode;
			string errorTextOld=ErrorText;
			try {
				int count=0;
				while(State==6) {
					if(count>=MaxDrainImages) {
						Log("\""+SourceName+"\" still reports pending images after "+count+"; the rest are discarded with MSG_RESET.");
						ResetPendingTransfers();
						break;
					}
					count++;
					DibImage dib;
					TransferOutcome outcome=TransferOne(out dib);
					if(outcome==TransferOutcome.Done) {
						DeliverToSink(dib,drainReason,stopReason);
					}
					else {
						Log("A pending image from \""+SourceName+"\" could not be transferred ("+drainReason+"): "+ErrorText);
					}
				}
			}
			finally {
				SetError(errorCodeOld,errorTextOld);
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

		///<summary>MSG_DISABLEDS from state 5. A source that refuses is treated as disabled, as before.</summary>
		private void DisableSourceSafely(TwainStopReason reason) {
			if(SendDisable()) {
				return;
			}
			if(IsOpenedByName) {
				Log("\""+SourceName+"\" refused MSG_DISABLEDS (condition code "+ToHex(LastConditionCode)+"); it is treated as disabled.");
			}
			SetDisabledState();
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
