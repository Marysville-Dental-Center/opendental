// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace ImagingDeviceManager.TwainProtocol {
	///<summary>A loaded TWAIN Data Source Manager. The production implementation is NativeTwainDsm. Unit tests supply a
	///simulated data source manager so that the protocol logic can be tested without a scanner.</summary>
	internal interface ITwainDsm:IDisposable {
		///<summary>Name of the library, for error messages. Example: "TWAINDSM.dll".</summary>
		string LibraryName { get; }

		///<summary>Calls DSM_Entry. origin points to the application's TW_IDENTITY. dest points to the data source's
		///TW_IDENTITY, or is IntPtr.Zero for triplets addressed to the DSM itself.</summary>
		ushort Entry(IntPtr origin,IntPtr dest,uint dg,ushort dat,ushort msg,IntPtr data);

		///<summary>Called right after MSG_OPENDSM succeeds. A version 2 DSM hands out its memory functions here.</summary>
		void OnDsmOpened(IntPtr appIdentity);

		///<summary>Allocates movable memory that can be exchanged with a data source (capability containers).</summary>
		IntPtr MemAllocate(uint size);

		void MemFree(IntPtr handle);

		IntPtr MemLock(IntPtr handle);

		void MemUnlock(IntPtr handle);
	}
}
