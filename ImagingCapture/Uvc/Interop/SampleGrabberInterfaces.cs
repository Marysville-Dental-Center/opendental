// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Runtime.InteropServices;

namespace ImagingCapture.Uvc.Interop {
	//The Sample Grabber's interfaces, declared by hand from the Windows SDK documentation (qedit.h). The Sample Grabber and the
	//Null Renderer live in qedit.dll, which is deprecated but still ships with Windows (SysWOW64 for this 32-bit process). The
	//IUvcBackend seam leaves room for another backend if it is ever removed. The rules are in DeviceEnumInterfaces.cs.

	///<summary>ISampleGrabber.</summary>
	[ComImport,Guid("6B652FFF-11FE-4FCE-92AD-0266B5D7C78F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface ISampleGrabber {
		[PreserveSig]
		int SetOneShot([MarshalAs(UnmanagedType.Bool)] bool OneShot);
		///<summary>Restricts the input to this media type. Fields left as GUID_NULL match anything.</summary>
		[PreserveSig]
		int SetMediaType([In] ref AM_MEDIA_TYPE pType);
		///<summary>Fills the caller's structure. Its format block is released with DsUtil.FreeMediaType.</summary>
		[PreserveSig]
		int GetConnectedMediaType(ref AM_MEDIA_TYPE pType);
		[PreserveSig]
		int SetBufferSamples([MarshalAs(UnmanagedType.Bool)] bool BufferThem);
		[PreserveSig]
		int Unused_GetCurrentBuffer();
		[PreserveSig]
		int Unused_GetCurrentSample();
		///<summary>WhichMethodToCallback: 0 = SampleCB, 1 = BufferCB. A null callback removes it.</summary>
		[PreserveSig]
		int SetCallback(ISampleGrabberCB pCallback,int WhichMethodToCallback);
	}

	///<summary>ISampleGrabberCB, implemented by this layer and called on the graph's streaming thread.</summary>
	[ComImport,Guid("0579154A-2B53-4994-B0D0-E773148EFF85"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface ISampleGrabberCB {
		///<summary>Not requested (SetCallback is called with BufferCB). Declared because it comes first in the vtable.</summary>
		[PreserveSig]
		int SampleCB(double SampleTime,IntPtr pSample);
		///<summary>pBuffer is valid only during the call.</summary>
		[PreserveSig]
		int BufferCB(double SampleTime,IntPtr pBuffer,int BufferLen);
	}
}
