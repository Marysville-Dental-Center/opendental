// Vendor-direct integrations: Tools > Advertising > Postcards, moved out of FormOpenDental unchanged.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using OpenDentBusiness;

namespace OpenDental.VendorDirect {
	///<summary>Owned by the POSTCARD package (docs/specs/vendor-direct/README.md, 3.2). The foundation package moved the click handler here without
	///changing what it does. The Advertising menu only shows while the PostcardMania program link is enabled.</summary>
	public static class PostcardsMenu {
		///<summary>Runs when the user clicks Tools > Advertising > Postcards.</summary>
		public static void Open() {
			if(!Security.IsAuthorized(EnumPermType.Advertising)) {
				return;
			}
			if(PrefC.GetString(PrefName.AdvertisingPostCardGuid)=="") {
				using FormAdvertisingPostcardsSetup formMassPostcardSetup=new FormAdvertisingPostcardsSetup();
				formMassPostcardSetup.ShowDialog();
			}
			using FormAdvertisingPostcardsSend formMassPostcardSend=new FormAdvertisingPostcardsSend();
			formMassPostcardSend.ShowDialog();
		}
	}
}
