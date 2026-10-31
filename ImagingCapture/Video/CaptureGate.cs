// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace ImagingCapture.Video {
	///<summary>What asked the Video window for a photo.</summary>
	public enum CaptureRequestSource {
		///<summary>The window's Capture button.</summary>
		Button=0,
		///<summary>Space typed while the Video window has the focus.</summary>
		SpaceInWindow=1,
		///<summary>Space typed in the main window and passed on through FormVideo.Parent_KeyDown (design 8.10).</summary>
		SpaceInMainWindow=2,
		///<summary>A camera trigger (still pin or polled control) that passed the debounce gate (design 8.4).</summary>
		Trigger=3,
	}

	///<summary>Why a photo request was not carried out. None means it was accepted.</summary>
	public enum CaptureDropReason {
		None=0,
		///<summary>The window is closing: the patient changed or the user closed it (design 8.10).</summary>
		Closing,
		///<summary>Space from the main window while [Camera] MainWindowSpace=0 (design 8.10).</summary>
		MainWindowSpaceOff,
		///<summary>The handler of the previous photo is still running (design 8.4, hand-off safety).</summary>
		Busy,
		///<summary>The window is disabled, for example under a modal window (design 8.4).</summary>
		WindowDisabled,
		///<summary>No patient was selected when the window opened.</summary>
		NoPatient,
		///<summary>No camera is streaming.</summary>
		NoCamera,
		///<summary>The camera has not delivered a picture yet.</summary>
		NoPicture,
		///<summary>Copying the picture failed, for example for lack of memory.</summary>
		Failed,
	}

	///<summary>The Video window's photo hand-off rules (design 8.4 and 8.10), apart from the window so they can be tested.
	///
	///A request is dropped, in this order of checks, when the window is closing, when it is Space from the main window and
	///[Camera] MainWindowSpace is off, when the previous photo's handler is still running (no new BitmapCaptured while the
	///previous handler runs, even when that handler shows a message box whose message loop delivers more presses), when the
	///window is disabled, when no patient is bound, and when no camera is streaming. An accepted request holds the gate until
	///End. Once closing, the gate never accepts again.
	///
	///Thread-safe: BeginClosing may be called from any thread (ODEvent can be fired from one); the other members are called on
	///the UI thread.</summary>
	public sealed class CaptureGate {
		private readonly object _lock=new object();
		private bool _isClosing;
		private bool _isHandlerRunning;

		///<summary>True after BeginClosing.</summary>
		public bool IsClosing {
			get {
				lock(_lock) {
					return _isClosing;
				}
			}
		}

		///<summary>True between an accepted TryBegin and its End.</summary>
		public bool IsHandlerRunning {
			get {
				lock(_lock) {
					return _isHandlerRunning;
				}
			}
		}

		///<summary>Decides one request. None means accepted: the gate is held until End, and every request until then is Busy.</summary>
		public CaptureDropReason TryBegin(CaptureRequestSource captureRequestSource,bool isWindowEnabled,bool isMainWindowSpaceOn,bool hasPatient,
			bool isCameraStreaming)
		{
			lock(_lock) {
				CaptureDropReason captureDropReason=Decide(captureRequestSource,isWindowEnabled,isMainWindowSpaceOn,hasPatient,isCameraStreaming);
				if(captureDropReason==CaptureDropReason.None) {
					_isHandlerRunning=true;
				}
				return captureDropReason;
			}
		}

		///<summary>Releases the gate after the handler returned (or after the accepted request found no picture). Safe to call more
		///than once.</summary>
		public void End() {
			lock(_lock) {
				_isHandlerRunning=false;
			}
		}

		///<summary>From now on every request is dropped as Closing.</summary>
		public void BeginClosing() {
			lock(_lock) {
				_isClosing=true;
			}
		}

		private CaptureDropReason Decide(CaptureRequestSource captureRequestSource,bool isWindowEnabled,bool isMainWindowSpaceOn,bool hasPatient,
			bool isCameraStreaming)
		{
			if(_isClosing) {
				return CaptureDropReason.Closing;
			}
			if(captureRequestSource==CaptureRequestSource.SpaceInMainWindow && !isMainWindowSpaceOn) {
				return CaptureDropReason.MainWindowSpaceOff;
			}
			if(_isHandlerRunning) {
				return CaptureDropReason.Busy;
			}
			if(!isWindowEnabled) {
				return CaptureDropReason.WindowDisabled;
			}
			if(!hasPatient) {
				return CaptureDropReason.NoPatient;
			}
			if(!isCameraStreaming) {
				return CaptureDropReason.NoCamera;
			}
			return CaptureDropReason.None;
		}

		///<summary>The note the window shows for a dropped request. "" for None and for drops that need no note: Closing (the window
		///is going away) and MainWindowSpaceOff (Space in the main window is then simply not a capture key).</summary>
		public static string GetDropText(CaptureDropReason captureDropReason) {
			switch(captureDropReason) {
				case CaptureDropReason.Busy:
					return "Ignored: the previous photo is still being saved.";
				case CaptureDropReason.WindowDisabled:
					return "Ignored while another window or message is open.";
				case CaptureDropReason.NoPatient:
					return "No patient is selected. Close this window and open it again from Imaging.";
				case CaptureDropReason.NoCamera:
					return "No camera is running.";
				case CaptureDropReason.NoPicture:
					return "The camera has not sent a picture yet.";
				case CaptureDropReason.Failed:
					return "The photo could not be taken.";
			}
			return "";
		}

		///<summary>True for drops worth a log line: a request the user or a camera button made that did nothing for a reason other
		///than the window state the user can see.</summary>
		public static bool IsLogged(CaptureDropReason captureDropReason) {
			return captureDropReason==CaptureDropReason.Busy
				|| captureDropReason==CaptureDropReason.WindowDisabled
				|| captureDropReason==CaptureDropReason.NoPicture
				|| captureDropReason==CaptureDropReason.Failed;
		}
	}
}
