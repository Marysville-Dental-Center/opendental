// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Runtime.InteropServices;
using IBindCtx=System.Runtime.InteropServices.ComTypes.IBindCtx;

namespace ImagingCapture.Uvc.Interop {
	//COM interfaces for device enumeration, declared by hand from the Windows SDK documentation (strmif.h, objidl.h, ocidl.h).
	//Rules for every interface in this folder:
	//- Methods are declared in vtable order. Only the members this layer calls are declared with their real signature. A member
	//  that comes before one of those in the vtable is declared as a parameterless placeholder named Unused_<SDK name>, so the
	//  later members land in the right slots. Placeholders are never called. Members after the last one used are left out.
	//- Every method keeps its HRESULT (PreserveSig). Callers check it.
	//Unit tests pin each used member to its vtable slot.

	///<summary>ICreateDevEnum, implemented by the System Device Enumerator.</summary>
	[ComImport,Guid("29840822-5B84-11D0-BD3B-00A0C911CE86"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface ICreateDevEnum {
		///<summary>Returns S_FALSE and a null enumerator when the category is empty or does not exist.</summary>
		[PreserveSig]
		int CreateClassEnumerator([In] ref Guid clsidDeviceClass,out IEnumMoniker enumMoniker,int flags);
	}

	///<summary>IEnumMoniker.</summary>
	[ComImport,Guid("00000102-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IEnumMoniker {
		[PreserveSig]
		int Next(int celt,[Out,MarshalAs(UnmanagedType.LPArray,SizeParamIndex=0)] IMoniker[] rgelt,IntPtr pceltFetched);
	}

	///<summary>IMoniker. A device moniker of the video input category binds to the capture source filter (BindToObject) and to
	///a property bag that holds FriendlyName and DevicePath (BindToStorage).</summary>
	[ComImport,Guid("0000000F-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IMoniker {
		//IPersist
		[PreserveSig]
		int Unused_GetClassID();
		//IPersistStream
		[PreserveSig]
		int Unused_IsDirty();
		[PreserveSig]
		int Unused_Load();
		[PreserveSig]
		int Unused_Save();
		[PreserveSig]
		int Unused_GetSizeMax();
		//IMoniker
		[PreserveSig]
		int BindToObject(IBindCtx pbc,IMoniker pmkToLeft,[In] ref Guid riidResult,[MarshalAs(UnmanagedType.IUnknown)] out object ppvResult);
		[PreserveSig]
		int BindToStorage(IBindCtx pbc,IMoniker pmkToLeft,[In] ref Guid riid,[MarshalAs(UnmanagedType.IUnknown)] out object ppvObj);
		[PreserveSig]
		int Unused_Reduce();
		[PreserveSig]
		int Unused_ComposeWith();
		[PreserveSig]
		int Unused_Enum();
		[PreserveSig]
		int Unused_IsEqual();
		[PreserveSig]
		int Unused_Hash();
		[PreserveSig]
		int Unused_IsRunning();
		[PreserveSig]
		int Unused_GetTimeOfLastChange();
		[PreserveSig]
		int Unused_Inverse();
		[PreserveSig]
		int Unused_CommonPrefixWith();
		[PreserveSig]
		int Unused_RelativePathTo();
		///<summary>The display name is allocated with CoTaskMemAlloc. The caller frees it.</summary>
		[PreserveSig]
		int GetDisplayName(IBindCtx pbc,IMoniker pmkToLeft,out IntPtr ppszDisplayName);
	}

	///<summary>IPropertyBag.</summary>
	[ComImport,Guid("55272A00-42CB-11CE-8135-00AA004BB851"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IPropertyBag {
		[PreserveSig]
		int Read([MarshalAs(UnmanagedType.LPWStr)] string pszPropName,[MarshalAs(UnmanagedType.Struct)] ref object pVar,IntPtr pErrorLog);
	}
}
