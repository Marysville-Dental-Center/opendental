// Vendor follow-up from public documentation (next/vendorpub-1): adds the "Partner ID" property to the Medit Link program link the first time
// Setup > Program Links opens. The link keeps the standard Program Link window. Found by ProgramLinkSetupRegistry.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.VendorDirect.ProgramLinks;

namespace OpenPmsDental.VendorDirect {
	///<summary>Medit Link: the row Open Dental's conversion created in every database, edited in the standard Program Link window as before
	///(StandardProgramLinkSetup). EnsureProgram only adds the blank Partner ID property (MeditLinkPartner.EnsureProperty); it never inserts or
	///changes the program row.</summary>
	public class MeditLinkProgramLinkSetup:StandardProgramLinkSetup {
		public override ProgramName ProgName {
			get {
				return ProgramName.MeditLink;
			}
		}

		public override bool EnsureProgram() {
			return MeditLinkPartner.EnsureProperty();
		}
	}
}
