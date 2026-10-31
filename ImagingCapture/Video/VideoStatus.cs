// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using ImagingCapture.Uvc;

namespace ImagingCapture.Video {
	///<summary>The state of the Video window's camera, as the user sees it (design 8.9).</summary>
	public enum VideoStatusKind {
		///<summary>The preview is running.</summary>
		Streaming=0,
		///<summary>Opening the camera.</summary>
		Starting,
		///<summary>No video capture device is present.</summary>
		NoCamera,
		///<summary>Another program holds the camera.</summary>
		InUse,
		///<summary>Windows denied access to the camera (privacy settings).</summary>
		AccessDenied,
		///<summary>The chosen camera is no longer in the device list.</summary>
		NotFound,
		///<summary>The camera was unplugged while streaming.</summary>
		Disconnected,
		///<summary>Streaming stopped with an error.</summary>
		StreamError,
		///<summary>The device has no video output this window can show.</summary>
		NoVideoOutput,
		///<summary>A Windows component the camera layer needs is missing.</summary>
		NotAvailable,
		///<summary>Listing the cameras failed.</summary>
		ListFailed,
		///<summary>Opening or starting the camera failed for another reason.</summary>
		Failed,
	}

	///<summary>The button the window offers next to a status.</summary>
	public enum VideoStatusAction {
		None=0,
		///<summary>"Refresh": list the cameras again and open the chosen one.</summary>
		Refresh=1,
		///<summary>"Reconnect": the same, after the camera was lost while streaming.</summary>
		Reconnect=2,
	}

	///<summary>What the window shows about its camera: a sentence and the button to offer. Immutable.</summary>
	public sealed class VideoStatus {
		public VideoStatusKind Kind { get; }
		///<summary>The sentence to show, in English; the window translates it. "" while streaming.</summary>
		public string Text { get; }
		///<summary>Text that is not translated and follows Text, for example an error code "(0x800705AA)". Often "".</summary>
		public string Detail { get; }
		public VideoStatusAction Action { get; }

		public VideoStatus(VideoStatusKind kind,string text,string detail,VideoStatusAction action) {
			Kind=kind;
			Text=text??"";
			Detail=detail??"";
			Action=action;
		}

		///<summary>True while the preview is running.</summary>
		public bool IsStreaming {
			get {
				return Kind==VideoStatusKind.Streaming;
			}
		}

		///<summary>True for every kind except Streaming and Starting.</summary>
		public bool IsProblem {
			get {
				return Kind!=VideoStatusKind.Streaming && Kind!=VideoStatusKind.Starting;
			}
		}

		public override string ToString() {
			return Detail=="" ? Text : Text+" "+Detail;
		}
	}

	///<summary>Maps camera conditions to what the Video window shows (design 8.9). Every sentence is generic: the condition comes
	///from standard Windows error codes (UvcErrors.Classify) and no text names a camera, a driver or a camera program.</summary>
	public static class VideoStatusText {
		///<summary>The sentence the design gives for a camera held by another program.</summary>
		public const string TextInUse="Camera is in use by another program. Close other camera software and click Refresh.";
		public const string TextNoCamera="No camera found.";
		public const string TextDisconnected="Camera disconnected.";

		public static VideoStatus Streaming() {
			return new VideoStatus(VideoStatusKind.Streaming,"","",VideoStatusAction.None);
		}

		public static VideoStatus Starting() {
			return new VideoStatus(VideoStatusKind.Starting,"Starting the camera...","",VideoStatusAction.None);
		}

		///<summary>No video capture device is present.</summary>
		public static VideoStatus NoCamera() {
			return new VideoStatus(VideoStatusKind.NoCamera,TextNoCamera,"",VideoStatusAction.Refresh);
		}

		///<summary>Listing the devices threw.</summary>
		public static VideoStatus ForListFailure(UvcErrorKind uvcErrorKind,int hResult) {
			if(uvcErrorKind==UvcErrorKind.NotAvailable) {
				return NotAvailable(hResult);
			}
			return new VideoStatus(VideoStatusKind.ListFailed,"The list of cameras could not be read.",FormatDetail(hResult),VideoStatusAction.Refresh);
		}

		///<summary>Opening or starting a device threw a UvcException of this kind (or another exception, as Failed).</summary>
		public static VideoStatus ForOpenFailure(UvcErrorKind uvcErrorKind,int hResult) {
			switch(uvcErrorKind) {
				case UvcErrorKind.InUse:
					return new VideoStatus(VideoStatusKind.InUse,TextInUse,"",VideoStatusAction.Refresh);
				case UvcErrorKind.AccessDenied:
					return AccessDenied();
				case UvcErrorKind.NoDevice:
					return new VideoStatus(VideoStatusKind.NotFound,"The camera was not found. Connect it and click Refresh.","",VideoStatusAction.Refresh);
				case UvcErrorKind.DeviceLost:
					return new VideoStatus(VideoStatusKind.Disconnected,TextDisconnected,"",VideoStatusAction.Reconnect);
				case UvcErrorKind.ErrorAbort:
					return new VideoStatus(VideoStatusKind.StreamError,"The camera stopped with an error.",FormatDetail(hResult),VideoStatusAction.Reconnect);
				case UvcErrorKind.NoCapturePin:
					return new VideoStatus(VideoStatusKind.NoVideoOutput,"This device has no video output that this window can show.","",VideoStatusAction.Refresh);
				case UvcErrorKind.NotAvailable:
					return NotAvailable(hResult);
			}
			return new VideoStatus(VideoStatusKind.Failed,"The camera could not be started.",FormatDetail(hResult),VideoStatusAction.Refresh);
		}

		///<summary>The frame source raised Error while streaming (design 8.2: EC_DEVICE_LOST and EC_ERRORABORT).</summary>
		public static VideoStatus ForStreamError(UvcErrorKind uvcErrorKind,int hResult) {
			switch(uvcErrorKind) {
				case UvcErrorKind.DeviceLost:
				case UvcErrorKind.NoDevice:
					return new VideoStatus(VideoStatusKind.Disconnected,TextDisconnected,"",VideoStatusAction.Reconnect);
				case UvcErrorKind.InUse:
					return new VideoStatus(VideoStatusKind.InUse,TextInUse,"",VideoStatusAction.Refresh);
				case UvcErrorKind.AccessDenied:
					return AccessDenied();
			}
			return new VideoStatus(VideoStatusKind.StreamError,"The camera stopped with an error.",FormatDetail(hResult),VideoStatusAction.Reconnect);
		}

		private static VideoStatus AccessDenied() {
			return new VideoStatus(VideoStatusKind.AccessDenied,
				"Windows denied access to the camera. Allow desktop apps to use the camera in the Windows privacy settings, then click Refresh.",
				"",VideoStatusAction.Refresh);
		}

		private static VideoStatus NotAvailable(int hResult) {
			return new VideoStatus(VideoStatusKind.NotAvailable,
				"A Windows video component that the camera needs is not available on this computer (DirectShow Sample Grabber, qedit.dll).",
				FormatDetail(hResult),VideoStatusAction.Refresh);
		}

		///<summary>"(0x800705AA)" for a failure code, "" for 0 and success codes.</summary>
		public static string FormatDetail(int hResult) {
			if(hResult>=0) {
				return "";
			}
			return "("+UvcErrors.FormatHResult(hResult)+")";
		}
	}
}
