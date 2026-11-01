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
		private IntPtr _hModule;
		private DsmEntryProc _dsmEntry;

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

		public void Dispose() {
			_dsmEntry=null;
			if(_hModule!=IntPtr.Zero) {
				NativeMethods.FreeLibrary(_hModule);
				_hModule=IntPtr.Zero;
			}
		}
	}
}
