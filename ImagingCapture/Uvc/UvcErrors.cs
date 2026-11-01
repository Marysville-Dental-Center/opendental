// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using ImagingCapture.Uvc.Interop;

namespace ImagingCapture.Uvc {
	///<summary>What went wrong with a camera, in terms the window can act on (design 8.9). Vendor-neutral by construction: the
	///kind comes only from standard Windows error codes.</summary>
	public enum UvcErrorKind {
		None=0,
		///<summary>The device is not (or no longer) in the list of video capture devices.</summary>
		NoDevice,
		///<summary>Another program holds the camera.</summary>
		InUse,
		///<summary>Windows denied access, for example through the camera privacy settings.</summary>
		AccessDenied,
		///<summary>The camera was unplugged while open.</summary>
		DeviceLost,
		///<summary>The graph stopped because of a streaming error (EC_ERRORABORT).</summary>
		ErrorAbort,
		///<summary>The device has no output pin this layer can stream from.</summary>
		NoCapturePin,
		///<summary>A Windows component this layer needs (for example the Sample Grabber in qedit.dll) is not registered.</summary>
		NotAvailable,
		///<summary>Any other failure. The HRESULT says which.</summary>
		Failed,
	}

	///<summary>A camera operation failed. Kind says how; HResult holds the Windows error code, or 0.</summary>
	public class UvcException:Exception {
		public UvcErrorKind Kind { get; }

		public UvcException(UvcErrorKind kind,string message,int hResult=0) : base(message) {
			Kind=kind;
			HResult=hResult;
		}
	}

	///<summary>An error the frame source reports after Start: device lost or a streaming error.</summary>
	public sealed class UvcErrorEventArgs:EventArgs {
		public UvcErrorKind Kind { get; }
		public int HResult { get; }
		///<summary>Technical text for the log. Not for display: the window shows generic text chosen by Kind.</summary>
		public string Message { get; }

		public UvcErrorEventArgs(UvcErrorKind kind,int hResult,string message) {
			Kind=kind;
			HResult=hResult;
			Message=message??"";
		}
	}

	///<summary>Classifies Windows error codes into UvcErrorKind.</summary>
	public static class UvcErrors {
		//HRESULT_FROM_WIN32 and Media Foundation codes that capture stacks return. From winerror.h and mferror.h.
		///<summary>ERROR_SHARING_VIOLATION.</summary>
		internal const int HR_ERROR_SHARING_VIOLATION=unchecked((int)0x80070020);
		///<summary>ERROR_BUSY.</summary>
		internal const int HR_ERROR_BUSY=unchecked((int)0x800700AA);
		///<summary>ERROR_NO_SYSTEM_RESOURCES. What a capture driver returns when the camera is already streaming to another program.</summary>
		internal const int HR_ERROR_NO_SYSTEM_RESOURCES=unchecked((int)0x800705AA);
		///<summary>MF_E_HW_MFT_FAILED_START_STREAMING.</summary>
		internal const int HR_MF_E_HW_MFT_FAILED_START_STREAMING=unchecked((int)0xC00D3704);
		///<summary>MF_E_VIDEO_RECORDING_DEVICE_PREEMPTED.</summary>
		internal const int HR_MF_E_VIDEO_RECORDING_DEVICE_PREEMPTED=unchecked((int)0xC00DABE5);
		///<summary>MF_E_VIDEO_RECORDING_DEVICE_INVALIDATED.</summary>
		internal const int HR_MF_E_VIDEO_RECORDING_DEVICE_INVALIDATED=unchecked((int)0xC00DABE4);
		///<summary>ERROR_DEVICE_NOT_CONNECTED.</summary>
		internal const int HR_ERROR_DEVICE_NOT_CONNECTED=unchecked((int)0x8007048F);
		///<summary>ERROR_NO_SUCH_DEVICE.</summary>
		internal const int HR_ERROR_NO_SUCH_DEVICE=unchecked((int)0x800701B1);
		///<summary>ERROR_DEV_NOT_EXIST.</summary>
		internal const int HR_ERROR_DEV_NOT_EXIST=unchecked((int)0x80070037);
		///<summary>ERROR_SET_NOT_FOUND: a kernel streaming property set the device does not support.</summary>
		internal const int HR_ERROR_SET_NOT_FOUND=unchecked((int)0x80070492);

		///<summary>The kind of a failed HRESULT. Success codes give None, unrecognized failures give Failed.</summary>
		public static UvcErrorKind Classify(int hResult) {
			if(hResult>=0) {
				return UvcErrorKind.None;
			}
			switch(hResult) {
				case HR_ERROR_SHARING_VIOLATION:
				case HR_ERROR_BUSY:
				case HR_ERROR_NO_SYSTEM_RESOURCES:
				case HR_MF_E_HW_MFT_FAILED_START_STREAMING:
				case HR_MF_E_VIDEO_RECORDING_DEVICE_PREEMPTED:
					return UvcErrorKind.InUse;
				case Ds.E_ACCESSDENIED:
					return UvcErrorKind.AccessDenied;
				case HR_ERROR_DEVICE_NOT_CONNECTED:
				case HR_ERROR_NO_SUCH_DEVICE:
				case HR_ERROR_DEV_NOT_EXIST:
				case HR_MF_E_VIDEO_RECORDING_DEVICE_INVALIDATED:
					return UvcErrorKind.DeviceLost;
				case Ds.REGDB_E_CLASSNOTREG:
					return UvcErrorKind.NotAvailable;
			}
			return UvcErrorKind.Failed;
		}

		///<summary>"0x800705AA" style text for logs.</summary>
		public static string FormatHResult(int hResult) {
			return "0x"+hResult.ToString("X8");
		}
	}
}
