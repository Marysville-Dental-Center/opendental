// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Runtime.InteropServices;
using IBindCtx=System.Runtime.InteropServices.ComTypes.IBindCtx;

namespace ImagingCapture.Uvc.Interop {
	///<summary>Small helpers for the DirectShow interop: media type memory, COM object creation and deterministic release.</summary>
	internal static class DsUtil {
		[DllImport("ole32.dll")]
		public static extern int CreateBindCtx(int reserved,out IBindCtx ppbc);

		///<summary>Creates a COM object from its class identifier. Throws COMException (for example REGDB_E_CLASSNOTREG) if the
		///class is not available.</summary>
		public static object CreateComObject(Guid clsid) {
			Type type=Type.GetTypeFromCLSID(clsid,true);
			return Activator.CreateInstance(type);
		}

		///<summary>Releases this reference to a COM object, if it is one, and clears the variable. Each interface pointer received
		///from COM is released exactly once. A cast of a runtime callable wrapper to another interface is the same wrapper, so only
		///one of the variables that hold it is released.</summary>
		public static void Release<T>(ref T comObject) where T:class {
			T obj=comObject;
			comObject=null;
			if(obj==null || !Marshal.IsComObject(obj)) {
				return;
			}
			try {
				Marshal.ReleaseComObject(obj);
			}
			catch(Exception) {
				//The wrapper was already detached from its COM object. Nothing left to release.
			}
		}

		///<summary>FreeMediaType: frees the format block and releases pUnk, then clears both. The structure itself is not freed.</summary>
		public static void FreeMediaType(ref AM_MEDIA_TYPE mediaType) {
			if(mediaType.pbFormat!=IntPtr.Zero) {
				Marshal.FreeCoTaskMem(mediaType.pbFormat);
			}
			mediaType.pbFormat=IntPtr.Zero;
			mediaType.cbFormat=0;
			if(mediaType.pUnk!=IntPtr.Zero) {
				Marshal.Release(mediaType.pUnk);
				mediaType.pUnk=IntPtr.Zero;
			}
		}

		///<summary>DeleteMediaType: FreeMediaType on a structure allocated with CoTaskMemAlloc (as returned by
		///IAMStreamConfig::GetStreamCaps), then frees the structure.</summary>
		public static void DeleteMediaType(IntPtr pmt) {
			if(pmt==IntPtr.Zero) {
				return;
			}
			AM_MEDIA_TYPE mediaType=(AM_MEDIA_TYPE)Marshal.PtrToStructure(pmt,typeof(AM_MEDIA_TYPE));
			FreeMediaType(ref mediaType);
			Marshal.FreeCoTaskMem(pmt);
		}

		///<summary>Reads the VIDEOINFOHEADER of a FORMAT_VideoInfo media type. Returns false for any other format, or a format
		///block that is too small.</summary>
		public static bool TryGetVideoInfoHeader(AM_MEDIA_TYPE mediaType,out VIDEOINFOHEADER videoInfoHeader) {
			videoInfoHeader=new VIDEOINFOHEADER();
			if(mediaType.formattype!=Ds.FORMAT_VideoInfo
				|| mediaType.pbFormat==IntPtr.Zero
				|| mediaType.cbFormat<Marshal.SizeOf(typeof(VIDEOINFOHEADER)))
			{
				return false;
			}
			videoInfoHeader=(VIDEOINFOHEADER)Marshal.PtrToStructure(mediaType.pbFormat,typeof(VIDEOINFOHEADER));
			return true;
		}

		///<summary>The moniker's display name, or "" if it cannot be read.</summary>
		public static string GetDisplayName(IMoniker moniker) {
			IBindCtx bindCtx=null;
			IntPtr pName=IntPtr.Zero;
			try {
				if(CreateBindCtx(0,out bindCtx)!=Ds.S_OK) {
					return "";
				}
				if(moniker.GetDisplayName(bindCtx,null,out pName)!=Ds.S_OK || pName==IntPtr.Zero) {
					return "";
				}
				return Marshal.PtrToStringUni(pName)??"";
			}
			finally {
				if(pName!=IntPtr.Zero) {
					Marshal.FreeCoTaskMem(pName);
				}
				Release(ref bindCtx);
			}
		}

		///<summary>Reads string properties from a device moniker's property bag. Each entry is "" when that property is absent.</summary>
		public static string[] ReadPropertyBagStrings(IMoniker moniker,params string[] propertyNames) {
			string[] values=new string[propertyNames.Length];
			for(int i=0;i<values.Length;i++) {
				values[i]="";
			}
			object objBag=null;
			try {
				Guid iidPropertyBag=typeof(IPropertyBag).GUID;
				if(moniker.BindToStorage(null,null,ref iidPropertyBag,out objBag)!=Ds.S_OK || objBag==null) {
					return values;
				}
				IPropertyBag propertyBag=(IPropertyBag)objBag;
				for(int i=0;i<propertyNames.Length;i++) {
					object value=null;
					if(propertyBag.Read(propertyNames[i],ref value,IntPtr.Zero)==Ds.S_OK) {
						values[i]=value as string??"";
					}
				}
				return values;
			}
			finally {
				Release(ref objBag);
			}
		}
	}
}
