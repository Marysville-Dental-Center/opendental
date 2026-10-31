// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using ImagingCapture.Uvc.Interop;

namespace ImagingCapture.Uvc {
	///<summary>The two standard camera control interfaces a UVC source filter exposes. The values are the [Trigger.ControlPoll]
	///and [Level.N] Interface names in capture.ini.</summary>
	public enum UvcControlInterface {
		///<summary>IAMCameraControl, properties 0-6.</summary>
		CameraControl=0,
		///<summary>IAMVideoProcAmp, properties 0-9.</summary>
		VideoProcAmp=1,
	}

	///<summary>The outcome of reading a camera control.</summary>
	public enum UvcControlStatus {
		Ok=0,
		///<summary>The device does not support this property (E_PROP_ID_UNSUPPORTED and similar). A trigger on it is disabled.</summary>
		Unsupported,
		///<summary>The device does not expose this control interface at all.</summary>
		NoInterface,
		///<summary>The device was removed.</summary>
		DeviceLost,
		///<summary>Any other failure. HResult says which.</summary>
		Failed,
		///<summary>The frame source was disposed.</summary>
		Closed,
	}

	///<summary>A control value read with Get. Value and Flags are meaningful only when Status is Ok.</summary>
	public struct UvcControlValue {
		public UvcControlStatus Status { get; }
		public int Value { get; }
		///<summary>The driver's flags: 1 = automatic, 2 = manual (CameraControl_Flags and VideoProcAmp_Flags).</summary>
		public int Flags { get; }
		public int HResult { get; }

		public UvcControlValue(UvcControlStatus status,int value,int flags,int hResult) {
			Status=status;
			Value=value;
			Flags=flags;
			HResult=hResult;
		}

		public bool IsOk {
			get {
				return Status==UvcControlStatus.Ok;
			}
		}
	}

	///<summary>A control range read with GetRange. The numbers are meaningful only when Status is Ok.</summary>
	public struct UvcControlRange {
		public UvcControlStatus Status { get; }
		public int Min { get; }
		public int Max { get; }
		public int Step { get; }
		public int Default { get; }
		public int CapsFlags { get; }
		public int HResult { get; }

		public UvcControlRange(UvcControlStatus status,int min,int max,int step,int defaultValue,int capsFlags,int hResult) {
			Status=status;
			Min=min;
			Max=max;
			Step=step;
			Default=defaultValue;
			CapsFlags=capsFlags;
			HResult=hResult;
		}

		public bool IsOk {
			get {
				return Status==UvcControlStatus.Ok;
			}
		}
	}

	///<summary>The standard property numbers of the two control interfaces and their Windows SDK names
	///(CameraControlProperty, VideoProcAmpProperty). Nothing here is specific to any camera.</summary>
	public static class UvcControls {
		private static readonly string[] _arrayCameraControlNames={ "Pan","Tilt","Roll","Zoom","Exposure","Iris","Focus" };
		private static readonly string[] _arrayVideoProcAmpNames={ "Brightness","Contrast","Hue","Saturation","Sharpness","Gamma",
			"ColorEnable","WhiteBalance","BacklightCompensation","Gain" };

		///<summary>7 for CameraControl, 10 for VideoProcAmp.</summary>
		public static int GetPropertyCount(UvcControlInterface controlInterface) {
			if(controlInterface==UvcControlInterface.CameraControl) {
				return _arrayCameraControlNames.Length;
			}
			if(controlInterface==UvcControlInterface.VideoProcAmp) {
				return _arrayVideoProcAmpNames.Length;
			}
			return 0;
		}

		public static bool IsValidProperty(UvcControlInterface controlInterface,int property) {
			return property>=0 && property<GetPropertyCount(controlInterface);
		}

		///<summary>The SDK name, for example "Focus" or "Gain", or the number for an invalid property.</summary>
		public static string GetPropertyName(UvcControlInterface controlInterface,int property) {
			if(!IsValidProperty(controlInterface,property)) {
				return property.ToString();
			}
			if(controlInterface==UvcControlInterface.CameraControl) {
				return _arrayCameraControlNames[property];
			}
			return _arrayVideoProcAmpNames[property];
		}

		///<summary>Maps the HRESULT of IAMCameraControl or IAMVideoProcAmp Get or GetRange to a status.</summary>
		public static UvcControlStatus GetStatusFromHResult(int hResult) {
			if(hResult>=0) {
				return UvcControlStatus.Ok;
			}
			switch(hResult) {
				case Ds.E_PROP_ID_UNSUPPORTED:
				case Ds.E_PROP_SET_UNSUPPORTED:
				case UvcErrors.HR_ERROR_SET_NOT_FOUND:
				case Ds.E_NOTIMPL:
				case Ds.E_INVALIDARG:
					return UvcControlStatus.Unsupported;
				case Ds.E_NOINTERFACE:
					return UvcControlStatus.NoInterface;
			}
			if(UvcErrors.Classify(hResult)==UvcErrorKind.DeviceLost) {
				return UvcControlStatus.DeviceLost;
			}
			return UvcControlStatus.Failed;
		}
	}
}
