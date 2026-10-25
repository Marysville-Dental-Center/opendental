// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Runtime.InteropServices;

namespace ImagingCapture.Uvc.Interop {
	//Filter graph interfaces, declared by hand from the Windows SDK documentation (strmif.h, control.h). The rules are in
	//DeviceEnumInterfaces.cs.

	///<summary>IGraphBuilder, implemented by the Filter Graph Manager. Only IFilterGraph members are used.</summary>
	[ComImport,Guid("56A868A9-0AD4-11CE-B03A-0020AF0BA770"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IGraphBuilder {
		//IFilterGraph
		[PreserveSig]
		int AddFilter(IBaseFilter pFilter,[MarshalAs(UnmanagedType.LPWStr)] string pName);
		[PreserveSig]
		int RemoveFilter(IBaseFilter pFilter);
		[PreserveSig]
		int Unused_EnumFilters();
		[PreserveSig]
		int Unused_FindFilterByName();
		[PreserveSig]
		int Unused_ConnectDirect();
		[PreserveSig]
		int Unused_Reconnect();
		[PreserveSig]
		int Disconnect(IPin ppin);
	}

	///<summary>ICaptureGraphBuilder2, implemented by the Capture Graph Builder.</summary>
	[ComImport,Guid("93E5A4E0-2D50-11D2-ABFA-00A0C9C6E38D"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface ICaptureGraphBuilder2 {
		[PreserveSig]
		int SetFiltergraph(IGraphBuilder pfg);
		[PreserveSig]
		int Unused_GetFiltergraph();
		[PreserveSig]
		int Unused_SetOutputFileName();
		///<summary>pType is passed as IntPtr.Zero (any media type).</summary>
		[PreserveSig]
		int FindInterface([In] ref Guid pCategory,IntPtr pType,IBaseFilter pf,[In] ref Guid riid,
			[MarshalAs(UnmanagedType.IUnknown)] out object ppint);
		///<summary>pCategory and pType are passed as IntPtr.Zero, and pSource is an output pin: that pin is rendered through
		///pfCompressor (here a Sample Grabber) to pfRenderer, with any decoder DirectShow needs inserted.</summary>
		[PreserveSig]
		int RenderStream(IntPtr pCategory,IntPtr pType,[MarshalAs(UnmanagedType.IUnknown)] object pSource,IBaseFilter pfCompressor,
			IBaseFilter pfRenderer);
		[PreserveSig]
		int Unused_ControlStream();
		[PreserveSig]
		int Unused_AllocCapFile();
		[PreserveSig]
		int Unused_CopyCaptureFile();
		[PreserveSig]
		int FindPin([MarshalAs(UnmanagedType.IUnknown)] object pSource,PIN_DIRECTION pindir,[In] ref Guid pCategory,[In] ref Guid pType,
			[MarshalAs(UnmanagedType.Bool)] bool fUnconnected,int num,out IPin ppPin);
	}

	///<summary>IMediaControl, implemented by the Filter Graph Manager. A dual interface: the four IDispatch members come first in
	///the vtable.</summary>
	[ComImport,Guid("56A868B1-0AD4-11CE-B03A-0020AF0BA770"),InterfaceType(ComInterfaceType.InterfaceIsDual)]
	internal interface IMediaControl {
		[PreserveSig]
		int Run();
		[PreserveSig]
		int Unused_Pause();
		[PreserveSig]
		int Stop();
		[PreserveSig]
		int GetState(int msTimeout,out int pfs);
	}

	///<summary>IMediaEventEx, implemented by the Filter Graph Manager. A dual interface. Only the IMediaEvent members it
	///inherits are used: the graph's event handle is waited on and drained with GetEvent and FreeEventParams.</summary>
	[ComImport,Guid("56A868C0-0AD4-11CE-B03A-0020AF0BA770"),InterfaceType(ComInterfaceType.InterfaceIsDual)]
	internal interface IMediaEventEx {
		//IMediaEvent
		///<summary>A manual-reset event that is signaled while events are queued. Owned by the graph: never closed by the caller.</summary>
		[PreserveSig]
		int GetEventHandle(out IntPtr hEvent);
		///<summary>Returns E_ABORT when no event arrives within msTimeout. Every event retrieved is passed to FreeEventParams.</summary>
		[PreserveSig]
		int GetEvent(out int lEventCode,out IntPtr lParam1,out IntPtr lParam2,int msTimeout);
		[PreserveSig]
		int Unused_WaitForCompletion();
		[PreserveSig]
		int Unused_CancelDefaultHandling();
		[PreserveSig]
		int Unused_RestoreDefaultHandling();
		[PreserveSig]
		int FreeEventParams(int lEvCode,IntPtr lParam1,IntPtr lParam2);
	}
}
