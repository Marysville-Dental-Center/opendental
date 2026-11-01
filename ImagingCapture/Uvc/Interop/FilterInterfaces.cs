// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Runtime.InteropServices;

namespace ImagingCapture.Uvc.Interop {
	//Filter and pin interfaces, declared by hand from the Windows SDK documentation (strmif.h). The rules are in
	//DeviceEnumInterfaces.cs.

	///<summary>IBaseFilter.</summary>
	[ComImport,Guid("56A86895-0AD4-11CE-B03A-0020AF0BA770"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IBaseFilter {
		//IPersist
		[PreserveSig]
		int Unused_GetClassID();
		//IMediaFilter
		[PreserveSig]
		int Unused_Stop();
		[PreserveSig]
		int Unused_Pause();
		[PreserveSig]
		int Unused_Run();
		[PreserveSig]
		int Unused_GetState();
		[PreserveSig]
		int Unused_SetSyncSource();
		[PreserveSig]
		int Unused_GetSyncSource();
		//IBaseFilter
		[PreserveSig]
		int EnumPins(out IEnumPins ppEnum);
	}

	///<summary>IEnumPins.</summary>
	[ComImport,Guid("56A86892-0AD4-11CE-B03A-0020AF0BA770"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IEnumPins {
		[PreserveSig]
		int Next(int cPins,[Out,MarshalAs(UnmanagedType.LPArray,SizeParamIndex=0)] IPin[] ppPins,IntPtr pcFetched);
	}

	///<summary>IPin.</summary>
	[ComImport,Guid("56A86891-0AD4-11CE-B03A-0020AF0BA770"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IPin {
		[PreserveSig]
		int Unused_Connect();
		[PreserveSig]
		int Unused_ReceiveConnection();
		[PreserveSig]
		int Unused_Disconnect();
		///<summary>Returns VFW_E_NOT_CONNECTED and null when the pin is not connected.</summary>
		[PreserveSig]
		int ConnectedTo(out IPin ppPin);
		[PreserveSig]
		int Unused_ConnectionMediaType();
		[PreserveSig]
		int Unused_QueryPinInfo();
		[PreserveSig]
		int QueryDirection(out PIN_DIRECTION pPinDir);
	}

	///<summary>IAMStreamConfig, exposed by a capture source's output pins. SetFormat selects the stream format (resolution,
	///subtype and frame interval). It is the only call in this layer that changes anything on the camera (design 8.7), and it is
	///made only while the pin is unconnected.</summary>
	[ComImport,Guid("C6E13340-30AC-11D0-A18C-00A0C9118956"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IAMStreamConfig {
		[PreserveSig]
		int SetFormat(IntPtr pmt);
		[PreserveSig]
		int Unused_GetFormat();
		[PreserveSig]
		int GetNumberOfCapabilities(out int piCount,out int piSize);
		///<summary>*ppmt is allocated by the pin and released with DsUtil.DeleteMediaType. pSCC points to a caller buffer of at
		///least the size GetNumberOfCapabilities returned.</summary>
		[PreserveSig]
		int GetStreamCaps(int iIndex,out IntPtr ppmt,IntPtr pSCC);
	}
}
