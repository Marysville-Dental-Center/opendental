// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace ImagingDeviceManager.TwainProtocol {
	///<summary>The TWAIN Data Source Manager installed on this computer. Prefers the version 2 manager (TWAINDSM.dll, from the
	///TWAIN Working Group, installed by most current scanner drivers) and falls back to the legacy twain_32.dll that ships
	///with Windows. Libraries are loaded by full path so that a stray DLL in the current directory is never picked up.</summary>
	internal sealed class NativeTwainDsm:ITwainDsm {
		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		private delegate ushort DsmEntryProc(IntPtr origin,IntPtr dest,uint dg,ushort dat,ushort msg,IntPtr data);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		private delegate IntPtr DsmMemAllocateProc(uint size);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		private delegate void DsmMemFreeProc(IntPtr handle);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		private delegate IntPtr DsmMemLockProc(IntPtr handle);
		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		private delegate void DsmMemUnlockProc(IntPtr handle);

		private IntPtr _hModule;
		private DsmEntryProc _dsmEntry;
		private DsmMemAllocateProc _memAllocate;
		private DsmMemFreeProc _memFree;
		private DsmMemLockProc _memLock;
		private DsmMemUnlockProc _memUnlock;

		public string LibraryName { get; private set; }

		private NativeTwainDsm() {
		}

		///<summary>The full paths tried, in order.</summary>
		public static List<string> GetCandidatePaths() {
			List<string> listPaths=new List<string>();
			listPaths.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"TWAINDSM.dll"));
			listPaths.Add(Path.Combine(Environment.SystemDirectory,"TWAINDSM.dll"));//SysWOW64 for a 32-bit process on 64-bit Windows
			string windowsDir=Environment.GetEnvironmentVariable("windir");
			if(string.IsNullOrEmpty(windowsDir)) {
				windowsDir=Path.GetDirectoryName(Environment.SystemDirectory);
			}
			listPaths.Add(Path.Combine(windowsDir,"twain_32.dll"));
			return listPaths;
		}

		///<summary>Loads the first data source manager found. Returns null and sets errorText if none can be loaded.</summary>
		public static NativeTwainDsm Load(out string errorText) {
			errorText="";
			if(IntPtr.Size!=4) {
				errorText="TWAIN scanning requires a 32-bit (x86) process.";
				return null;
			}
			foreach(string path in GetCandidatePaths()) {
				if(!File.Exists(path)) {
					continue;
				}
				IntPtr hModule=NativeMethods.LoadLibrary(path);
				if(hModule==IntPtr.Zero) {
					errorText="Could not load "+path+" (Windows error "+Marshal.GetLastWin32Error()+").";
					continue;
				}
				IntPtr procAddress=NativeMethods.GetProcAddress(hModule,"DSM_Entry");
				if(procAddress==IntPtr.Zero) {
					NativeMethods.FreeLibrary(hModule);
					errorText=path+" does not export DSM_Entry.";
					continue;
				}
				NativeTwainDsm dsm=new NativeTwainDsm();
				dsm._hModule=hModule;
				dsm._dsmEntry=(DsmEntryProc)Marshal.GetDelegateForFunctionPointer(procAddress,typeof(DsmEntryProc));
				dsm.LibraryName=Path.GetFileName(path);
				return dsm;
			}
			if(errorText=="") {
				errorText="No TWAIN Data Source Manager is installed (TWAINDSM.dll or twain_32.dll). Install the scanner's TWAIN driver.";
			}
			return null;
		}

		public ushort Entry(IntPtr origin,IntPtr dest,uint dg,ushort dat,ushort msg,IntPtr data) {
			if(_dsmEntry==null) {
				throw new ObjectDisposedException(nameof(NativeTwainDsm));
			}
			return _dsmEntry(origin,dest,dg,dat,msg,data);
		}

		public void OnDsmOpened(IntPtr appIdentity) {
			TW_IDENTITY identity=(TW_IDENTITY)Marshal.PtrToStructure(appIdentity,typeof(TW_IDENTITY));
			if((identity.SupportedGroups & Tw.DF_DSM2)==0) {
				return;//Legacy DSM. Applications use the Windows Global* memory functions.
			}
			IntPtr pEntryPoint=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TW_ENTRYPOINT)));
			try {
				TW_ENTRYPOINT entryPoint=new TW_ENTRYPOINT();
				entryPoint.Size=(uint)Marshal.SizeOf(typeof(TW_ENTRYPOINT));
				Marshal.StructureToPtr(entryPoint,pEntryPoint,false);
				ushort rc=Entry(appIdentity,IntPtr.Zero,Tw.DG_CONTROL,Tw.DAT_ENTRYPOINT,Tw.MSG_GET,pEntryPoint);
				if(rc!=Tw.TWRC_SUCCESS) {
					return;
				}
				entryPoint=(TW_ENTRYPOINT)Marshal.PtrToStructure(pEntryPoint,typeof(TW_ENTRYPOINT));
				if(entryPoint.DSM_MemAllocate==IntPtr.Zero || entryPoint.DSM_MemFree==IntPtr.Zero
					|| entryPoint.DSM_MemLock==IntPtr.Zero || entryPoint.DSM_MemUnlock==IntPtr.Zero)
				{
					return;
				}
				_memAllocate=(DsmMemAllocateProc)Marshal.GetDelegateForFunctionPointer(entryPoint.DSM_MemAllocate,typeof(DsmMemAllocateProc));
				_memFree=(DsmMemFreeProc)Marshal.GetDelegateForFunctionPointer(entryPoint.DSM_MemFree,typeof(DsmMemFreeProc));
				_memLock=(DsmMemLockProc)Marshal.GetDelegateForFunctionPointer(entryPoint.DSM_MemLock,typeof(DsmMemLockProc));
				_memUnlock=(DsmMemUnlockProc)Marshal.GetDelegateForFunctionPointer(entryPoint.DSM_MemUnlock,typeof(DsmMemUnlockProc));
			}
			finally {
				Marshal.FreeHGlobal(pEntryPoint);
			}
		}

		public IntPtr MemAllocate(uint size) {
			if(_memAllocate!=null) {
				return _memAllocate(size);
			}
			return NativeMethods.GlobalAlloc(NativeMethods.GHND,(UIntPtr)size);
		}

		public void MemFree(IntPtr handle) {
			if(handle==IntPtr.Zero) {
				return;
			}
			if(_memFree!=null) {
				_memFree(handle);
				return;
			}
			NativeMethods.GlobalFree(handle);
		}

		public IntPtr MemLock(IntPtr handle) {
			if(_memLock!=null) {
				return _memLock(handle);
			}
			return NativeMethods.GlobalLock(handle);
		}

		public void MemUnlock(IntPtr handle) {
			if(_memUnlock!=null) {
				_memUnlock(handle);
				return;
			}
			NativeMethods.GlobalUnlock(handle);
		}

		public void Dispose() {
			_memAllocate=null;
			_memFree=null;
			_memLock=null;
			_memUnlock=null;
			_dsmEntry=null;
			if(_hModule!=IntPtr.Zero) {
				NativeMethods.FreeLibrary(_hModule);
				_hModule=IntPtr.Zero;
			}
		}
	}
}
