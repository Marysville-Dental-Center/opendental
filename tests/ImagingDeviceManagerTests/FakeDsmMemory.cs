// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ImagingDeviceManager.TwainProtocol;

namespace ImagingDeviceManager.Tests {
	///<summary>The memory functions a simulated version 2 DSM hands out through DAT_ENTRYPOINT (DSM_MemAllocate, DSM_MemFree,
	///DSM_MemLock, DSM_MemUnlock), as function pointers in a TW_ENTRYPOINT, which the session turns into delegates as it does a real
	///DSM's. It counts every call and remembers which handles it allocated, so a test can see that each handle went back through
	///DSM_MemFree, and that nothing else was passed to it. Underneath it uses GlobalAlloc, as TWAINDSM does on Windows.</summary>
	internal sealed class FakeDsmMemory {
		//The delegates must stay alive as long as their function pointers can be called. They have the library's delegate types,
		//because the runtime turns a pointer to a managed delegate back only into a delegate of the same type.
		private readonly DsmMemAllocateProc _allocate;
		private readonly DsmMemFreeProc _free;
		private readonly DsmMemLockProc _lock;
		private readonly DsmMemUnlockProc _unlock;
		private readonly HashSet<IntPtr> _hashSetOutstanding=new HashSet<IntPtr>();

		public int CountAllocate;
		public int CountFree;
		///<summary>DSM_MemFree calls with a handle this DSM did not allocate (or already freed). Each one is a bug in the caller.</summary>
		public int CountForeignFree;
		public int CountLock;
		public int CountUnlock;
		public readonly List<IntPtr> ListFreed=new List<IntPtr>();

		public FakeDsmMemory() {
			_allocate=Allocate;
			_free=Free;
			_lock=Lock;
			_unlock=Unlock;
		}

		///<summary>Handles allocated and not yet freed through DSM_MemFree.</summary>
		public int CountOutstanding => _hashSetOutstanding.Count;

		public bool IsOutstanding(IntPtr handle) {
			return _hashSetOutstanding.Contains(handle);
		}

		///<summary>Fills a TW_ENTRYPOINT, as DG_CONTROL / DAT_ENTRYPOINT / MSG_GET does.</summary>
		public void WriteEntryPoint(IntPtr data) {
			TW_ENTRYPOINT entryPoint=(TW_ENTRYPOINT)Marshal.PtrToStructure(data,typeof(TW_ENTRYPOINT));
			entryPoint.DSM_MemAllocate=Marshal.GetFunctionPointerForDelegate(_allocate);
			entryPoint.DSM_MemFree=Marshal.GetFunctionPointerForDelegate(_free);
			entryPoint.DSM_MemLock=Marshal.GetFunctionPointerForDelegate(_lock);
			entryPoint.DSM_MemUnlock=Marshal.GetFunctionPointerForDelegate(_unlock);
			Marshal.StructureToPtr(entryPoint,data,false);
		}

		///<summary>DSM_MemAllocate. Also what the simulated sources call for the handles they hand to the application.</summary>
		public IntPtr Allocate(uint size) {
			IntPtr handle=NativeMethods.GlobalAlloc(NativeMethods.GHND,(UIntPtr)size);
			_hashSetOutstanding.Add(handle);
			CountAllocate++;
			return handle;
		}

		private void Free(IntPtr handle) {
			if(!_hashSetOutstanding.Remove(handle)) {
				CountForeignFree++;//Never passed on: freeing a handle from another allocator here could corrupt memory.
				return;
			}
			CountFree++;
			ListFreed.Add(handle);
			NativeMethods.GlobalFree(handle);
		}

		public IntPtr Lock(IntPtr handle) {
			CountLock++;
			return NativeMethods.GlobalLock(handle);
		}

		public void Unlock(IntPtr handle) {
			CountUnlock++;
			NativeMethods.GlobalUnlock(handle);
		}
	}

	///<summary>The Windows Global* functions, recording every handle freed through them. Tests put it in TwainSession.GlobalMemory to
	///see which handles the session frees with GlobalFree.</summary>
	internal sealed class RecordingGlobalMemory:ITwainMemory {
		public readonly List<IntPtr> ListFreed=new List<IntPtr>();
		public int CountAllocate;

		public string Name => GlobalTwainMemory.Instance.Name;

		public IntPtr Allocate(uint size) {
			CountAllocate++;
			return GlobalTwainMemory.Instance.Allocate(size);
		}

		public void Free(IntPtr handle) {
			ListFreed.Add(handle);
			GlobalTwainMemory.Instance.Free(handle);
		}

		public IntPtr Lock(IntPtr handle) {
			return GlobalTwainMemory.Instance.Lock(handle);
		}

		public void Unlock(IntPtr handle) {
			GlobalTwainMemory.Instance.Unlock(handle);
		}

		public long GetSize(IntPtr handle) {
			return GlobalTwainMemory.Instance.GetSize(handle);
		}
	}
}
