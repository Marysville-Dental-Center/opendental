// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Runtime.InteropServices;

namespace ImagingDeviceManager.TwainProtocol {
	///<summary>Allocates, locks, unlocks and frees the memory handles exchanged with the Data Source Manager and data sources:
	///capability containers, native DIBs and DAT_STATUSUTF8 text. When the DSM reports DF_DSM2 and hands out its memory functions
	///(DAT_ENTRYPOINT), both sides use them; otherwise both sides use the Windows Global* functions (TWAIN 2.4 pp.3-15, 3-32,
	///7-40 to 7-41). A handle a source gives the application is freed the same way the source allocated it (twain-contract 1.1
	///section 6.4).</summary>
	internal interface ITwainMemory {
		///<summary>"DSM_MemFree" or "GlobalFree", for the log.</summary>
		string Name { get; }

		IntPtr Allocate(uint size);

		void Free(IntPtr handle);

		IntPtr Lock(IntPtr handle);

		void Unlock(IntPtr handle);

		///<summary>Size of the block in bytes, used only as an upper bound when reading it. 0 when this allocator cannot tell.</summary>
		long GetSize(IntPtr handle);
	}

	///<summary>The Windows Global* functions, used with the legacy twain_32.dll and with any DSM that does not hand out its own.</summary>
	internal sealed class GlobalTwainMemory:ITwainMemory {
		public static readonly GlobalTwainMemory Instance=new GlobalTwainMemory();

		private GlobalTwainMemory() {
		}

		public string Name => "GlobalFree";

		public IntPtr Allocate(uint size) {
			return NativeMethods.GlobalAlloc(NativeMethods.GHND,(UIntPtr)size);
		}

		public void Free(IntPtr handle) {
			if(handle!=IntPtr.Zero) {
				NativeMethods.GlobalFree(handle);
			}
		}

		public IntPtr Lock(IntPtr handle) {
			return NativeMethods.GlobalLock(handle);
		}

		public void Unlock(IntPtr handle) {
			NativeMethods.GlobalUnlock(handle);
		}

		public long GetSize(IntPtr handle) {
			return (long)NativeMethods.GlobalSize(handle).ToUInt64();
		}
	}

	//The DSM memory functions of TW_ENTRYPOINT (twain.h). Simulated DSMs in the unit tests hand out pointers to delegates of these
	//types, because the runtime turns such a pointer back only into a delegate of the type it was made from.
	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	internal delegate IntPtr DsmMemAllocateProc(uint size);
	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	internal delegate void DsmMemFreeProc(IntPtr handle);
	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	internal delegate IntPtr DsmMemLockProc(IntPtr handle);
	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	internal delegate void DsmMemUnlockProc(IntPtr handle);

	///<summary>The memory functions a version 2 DSM hands out through DAT_ENTRYPOINT. Its handles are opaque: nothing says they are
	///Global handles, so GlobalSize is never called on them.</summary>
	internal sealed class DsmTwainMemory:ITwainMemory {
		private readonly DsmMemAllocateProc _memAllocate;
		private readonly DsmMemFreeProc _memFree;
		private readonly DsmMemLockProc _memLock;
		private readonly DsmMemUnlockProc _memUnlock;

		private DsmTwainMemory(TW_ENTRYPOINT entryPoint) {
			_memAllocate=(DsmMemAllocateProc)Marshal.GetDelegateForFunctionPointer(entryPoint.DSM_MemAllocate,typeof(DsmMemAllocateProc));
			_memFree=(DsmMemFreeProc)Marshal.GetDelegateForFunctionPointer(entryPoint.DSM_MemFree,typeof(DsmMemFreeProc));
			_memLock=(DsmMemLockProc)Marshal.GetDelegateForFunctionPointer(entryPoint.DSM_MemLock,typeof(DsmMemLockProc));
			_memUnlock=(DsmMemUnlockProc)Marshal.GetDelegateForFunctionPointer(entryPoint.DSM_MemUnlock,typeof(DsmMemUnlockProc));
		}

		///<summary>Null unless all four functions are present; a DSM that hands out only some is treated as handing out none, as
		///sources do (twain-contract 1.1 section 2).</summary>
		public static DsmTwainMemory TryCreate(TW_ENTRYPOINT entryPoint) {
			if(entryPoint.DSM_MemAllocate==IntPtr.Zero || entryPoint.DSM_MemFree==IntPtr.Zero
				|| entryPoint.DSM_MemLock==IntPtr.Zero || entryPoint.DSM_MemUnlock==IntPtr.Zero)
			{
				return null;
			}
			return new DsmTwainMemory(entryPoint);
		}

		public string Name => "DSM_MemFree";

		public IntPtr Allocate(uint size) {
			return _memAllocate(size);
		}

		public void Free(IntPtr handle) {
			if(handle!=IntPtr.Zero) {
				_memFree(handle);
			}
		}

		public IntPtr Lock(IntPtr handle) {
			return _memLock(handle);
		}

		public void Unlock(IntPtr handle) {
			_memUnlock(handle);
		}

		public long GetSize(IntPtr handle) {
			return 0;
		}
	}
}
