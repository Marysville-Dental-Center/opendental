// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;

namespace ImagingDeviceManager.Imaging {
	///<summary>Hands out the opaque image handles ("HDIB") that EZTwain.Acquire returns. A handle is a number that is never
	///reused, so freeing a handle twice, or freeing it after DIB_ToDibSection already released it, is harmless.</summary>
	internal static class DibHandleTable {
		private static readonly object _lock=new object();
		private static readonly Dictionary<long,DibImage> _dictImages=new Dictionary<long,DibImage>();
		///<summary>Starts well above zero and steps by 4 so handles look like ordinary aligned handles.</summary>
		private static long _nextHandle=0x10000;

		public static IntPtr Add(DibImage dib) {
			if(dib==null) {
				return IntPtr.Zero;
			}
			lock(_lock) {
				long handle=_nextHandle;
				_nextHandle+=4;
				if(_nextHandle>int.MaxValue-4) {
					_nextHandle=0x10000;//Practically unreachable: half a billion scans in one session.
				}
				_dictImages[handle]=dib;
				return new IntPtr(handle);
			}
		}

		public static DibImage Get(IntPtr handle) {
			lock(_lock) {
				DibImage dib;
				_dictImages.TryGetValue(handle.ToInt64(),out dib);
				return dib;
			}
		}

		///<summary>Returns true if the handle was live.</summary>
		public static bool Free(IntPtr handle) {
			lock(_lock) {
				return _dictImages.Remove(handle.ToInt64());
			}
		}

		public static int Count {
			get {
				lock(_lock) {
					return _dictImages.Count;
				}
			}
		}
	}
}
