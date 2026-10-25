// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace ImagingCapture.Uvc.Interop {
	///<summary>Constants of the DirectShow API, taken from the public Windows SDK documentation and headers (uuids.h, strmif.h,
	///evcode.h, vfwmsgs.h, winerror.h). Class, interface and format identifiers are public facts of the Windows API. Only the
	///values this layer uses are listed.</summary>
	internal static class Ds {
		#region Class identifiers
		///<summary>CLSID_SystemDeviceEnum: the System Device Enumerator (devenum.dll).</summary>
		public static readonly Guid CLSID_SystemDeviceEnum=new Guid("62BE5D10-60EB-11d0-BD3B-00A0C911CE86");
		///<summary>CLSID_VideoInputDeviceCategory: the device category of video capture sources.</summary>
		public static readonly Guid CLSID_VideoInputDeviceCategory=new Guid("860BB310-5D01-11d0-BD3B-00A0C911CE86");
		///<summary>CLSID_FilterGraph: the Filter Graph Manager (quartz.dll).</summary>
		public static readonly Guid CLSID_FilterGraph=new Guid("E436EBB3-524F-11CE-9F53-0020AF0BA770");
		///<summary>CLSID_CaptureGraphBuilder2: the Capture Graph Builder (qcap.dll).</summary>
		public static readonly Guid CLSID_CaptureGraphBuilder2=new Guid("BF87B6E1-8C27-11d0-B3F0-00AA003761C5");
		///<summary>CLSID_SampleGrabber: the Sample Grabber filter (qedit.dll).</summary>
		public static readonly Guid CLSID_SampleGrabber=new Guid("C1F400A0-3F08-11d3-9F0B-006008039E37");
		///<summary>CLSID_NullRenderer: the Null Renderer filter (qedit.dll).</summary>
		public static readonly Guid CLSID_NullRenderer=new Guid("C1F400A4-3F08-11d3-9F0B-006008039E37");
		#endregion Class identifiers

		#region Media types, subtypes and format types
		///<summary>MEDIATYPE_Video ('vids').</summary>
		public static readonly Guid MEDIATYPE_Video=new Guid("73646976-0000-0010-8000-00AA00389B71");
		///<summary>MEDIASUBTYPE_RGB24: uncompressed 24-bit RGB.</summary>
		public static readonly Guid MEDIASUBTYPE_RGB24=new Guid("E436EB7D-524F-11CE-9F53-0020AF0BA770");
		///<summary>MEDIASUBTYPE_RGB32: uncompressed 32-bit RGB, the fourth byte unused.</summary>
		public static readonly Guid MEDIASUBTYPE_RGB32=new Guid("E436EB7E-524F-11CE-9F53-0020AF0BA770");
		///<summary>MEDIASUBTYPE_RGB565.</summary>
		public static readonly Guid MEDIASUBTYPE_RGB565=new Guid("E436EB7B-524F-11CE-9F53-0020AF0BA770");
		///<summary>MEDIASUBTYPE_RGB555.</summary>
		public static readonly Guid MEDIASUBTYPE_RGB555=new Guid("E436EB7C-524F-11CE-9F53-0020AF0BA770");
		///<summary>MEDIASUBTYPE_ARGB32.</summary>
		public static readonly Guid MEDIASUBTYPE_ARGB32=new Guid("773C9AC0-3274-11D0-B724-00AA006C1A01");
		///<summary>MEDIASUBTYPE_MJPG: motion JPEG (FourCC 'MJPG').</summary>
		public static readonly Guid MEDIASUBTYPE_MJPG=new Guid("47504A4D-0000-0010-8000-00AA00389B71");
		///<summary>MEDIASUBTYPE_YUY2: packed 4:2:2 YUV (FourCC 'YUY2').</summary>
		public static readonly Guid MEDIASUBTYPE_YUY2=new Guid("32595559-0000-0010-8000-00AA00389B71");
		///<summary>The base of every FourCC subtype: XXXXXXXX-0000-0010-8000-00AA00389B71, where Data1 holds the FourCC.</summary>
		public static readonly Guid FourCCBase=new Guid("00000000-0000-0010-8000-00AA00389B71");
		///<summary>FORMAT_VideoInfo: the format block is a VIDEOINFOHEADER.</summary>
		public static readonly Guid FORMAT_VideoInfo=new Guid("05589F80-C356-11CE-BF01-00AA0055595A");
		#endregion Media types, subtypes and format types

		#region Pin categories and search directions
		///<summary>PIN_CATEGORY_CAPTURE: the capture (stream) pin.</summary>
		public static readonly Guid PIN_CATEGORY_CAPTURE=new Guid("FB6C4281-0353-11D1-905F-0000C0CC16BA");
		///<summary>PIN_CATEGORY_PREVIEW: the preview pin. Not used: the capture pin is rendered, so the selected format is the one
		///shown and saved.</summary>
		public static readonly Guid PIN_CATEGORY_PREVIEW=new Guid("FB6C4282-0353-11D1-905F-0000C0CC16BA");
		///<summary>PIN_CATEGORY_STILL: the still-image pin. A UVC camera sends a sample on it when its hardware still trigger fires.</summary>
		public static readonly Guid PIN_CATEGORY_STILL=new Guid("FB6C428A-0353-11D1-905F-0000C0CC16BA");
		///<summary>LOOK_UPSTREAM_ONLY: ICaptureGraphBuilder2::FindInterface searches the filter and upstream of it only.</summary>
		public static readonly Guid LOOK_UPSTREAM_ONLY=new Guid("AC798BE0-98E3-11D1-B3F1-00AA003761C5");
		#endregion Pin categories and search directions

		#region HRESULT values
		public const int S_OK=0;
		public const int S_FALSE=1;
		public const int E_NOTIMPL=unchecked((int)0x80004001);
		public const int E_NOINTERFACE=unchecked((int)0x80004002);
		public const int E_ABORT=unchecked((int)0x80004004);
		public const int E_FAIL=unchecked((int)0x80004005);
		public const int E_INVALIDARG=unchecked((int)0x80070057);
		public const int E_ACCESSDENIED=unchecked((int)0x80070005);
		public const int REGDB_E_CLASSNOTREG=unchecked((int)0x80040154);
		///<summary>E_PROP_ID_UNSUPPORTED, HRESULT_FROM_WIN32(ERROR_NOT_FOUND): the device does not support this property.</summary>
		public const int E_PROP_ID_UNSUPPORTED=unchecked((int)0x80070490);
		///<summary>E_PROP_SET_UNSUPPORTED, HRESULT_FROM_WIN32(ERROR_NOT_SUPPORTED): the device does not support this property set.</summary>
		public const int E_PROP_SET_UNSUPPORTED=unchecked((int)0x80070032);
		///<summary>VFW_S_STATE_INTERMEDIATE: a state change has not finished yet.</summary>
		public const int VFW_S_STATE_INTERMEDIATE=0x00040237;
		///<summary>VFW_S_CANT_CUE: a live source cannot cue data while paused. A success code.</summary>
		public const int VFW_S_CANT_CUE=0x00040268;
		///<summary>VFW_E_NOT_CONNECTED: the pin is not connected.</summary>
		public const int VFW_E_NOT_CONNECTED=unchecked((int)0x80040209);
		#endregion HRESULT values

		#region Event codes
		///<summary>EC_ERRORABORT: an operation was aborted because of an error. Param1 is the HRESULT.</summary>
		public const int EC_ERRORABORT=0x03;
		///<summary>EC_DEVICE_LOST: a Plug and Play device was removed (Param2 0) or became available again (Param2 1).</summary>
		public const int EC_DEVICE_LOST=0x1F;
		#endregion Event codes

		#region Other values
		///<summary>FILTER_STATE State_Running.</summary>
		public const int State_Running=2;
		///<summary>ISampleGrabber::SetCallback: call ISampleGrabberCB::BufferCB.</summary>
		public const int SampleGrabberCallBufferCB=1;
		///<summary>BITMAPINFOHEADER biCompression BI_RGB.</summary>
		public const int BI_RGB=0;
		///<summary>BITMAPINFOHEADER biCompression BI_BITFIELDS.</summary>
		public const int BI_BITFIELDS=3;
		#endregion Other values
	}

	///<summary>PIN_DIRECTION.</summary>
	internal enum PIN_DIRECTION {
		PINDIR_INPUT=0,
		PINDIR_OUTPUT=1,
	}
}
