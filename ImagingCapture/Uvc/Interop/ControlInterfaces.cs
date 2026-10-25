// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Runtime.InteropServices;

namespace ImagingCapture.Uvc.Interop {
	//Camera control interfaces, declared by hand from the Windows SDK documentation (strmif.h). This layer only reads camera
	//controls (design 8.7): it calls GetRange and Get, never Set. Set sits between them in the vtable, so it is declared as a
	//parameterless placeholder purely to keep Get in its slot. It must never be called, and a unit test checks that no code
	//in ImagingCapture calls it. The rules are in DeviceEnumInterfaces.cs.

	///<summary>IAMCameraControl, exposed by the capture source filter. Properties 0-6 (CameraControlProperty).</summary>
	[ComImport,Guid("C6E13370-30AC-11D0-A18C-00A0C9118956"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IAMCameraControl {
		[PreserveSig]
		int GetRange(int Property,out int pMin,out int pMax,out int pSteppingDelta,out int pDefault,out int pCapsFlags);
		///<summary>Vtable placeholder for Set. Never called: the fork writes no camera controls.</summary>
		[PreserveSig]
		int Unused_Set();
		[PreserveSig]
		int Get(int Property,out int lValue,out int Flags);
	}

	///<summary>IAMVideoProcAmp, exposed by the capture source filter. Properties 0-9 (VideoProcAmpProperty).</summary>
	[ComImport,Guid("C6E13360-30AC-11D0-A18C-00A0C9118956"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IAMVideoProcAmp {
		[PreserveSig]
		int GetRange(int Property,out int pMin,out int pMax,out int pSteppingDelta,out int pDefault,out int pCapsFlags);
		///<summary>Vtable placeholder for Set. Never called: the fork writes no camera controls.</summary>
		[PreserveSig]
		int Unused_Set();
		[PreserveSig]
		int Get(int Property,out int lValue,out int Flags);
	}
}
