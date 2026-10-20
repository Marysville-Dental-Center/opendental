// Vendor-direct integrations: the startup check for a database that still names Dropbox as its A to Z storage.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using OpenDentBusiness;

namespace OpenDental.VendorDirect {
	///<summary>Owned by the DROPBOX package (docs/specs/vendor-direct/README.md, 3.2). Dropbox A to Z storage was removed (owner decision
	///2026-10-18), so this no longer contacts Dropbox. FormOpenDental calls it only when the A to Z storage preference is DropboxAtoZ.</summary>
	public static class DropboxStartupCheck {
		///<summary>Always returns false, with storageErrMsg set to the message that tells the administrator to choose another storage location.</summary>
		public static bool Run(out string storageErrMsg) {
			storageErrMsg=Lans.g("FormOpenDental",Dropbox.StorageRemovedMessage);
			return false;
		}
	}
}
