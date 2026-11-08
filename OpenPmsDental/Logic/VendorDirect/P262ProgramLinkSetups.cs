// P262-LINKS (Open Dental 26.2, items 44 and 45): puts the Allied Star Connect and OrthoComposite program links into Setup > Program Links. Each one
// inserts its rows (disabled) the first time Program Links opens and is edited in the standard Program Link window. Found by
// ProgramLinkSetupRegistry.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.VendorDirect.ProgramLinks;

namespace OpenPmsDental.VendorDirect {
	///<summary>Allied Star Connect: an ordinary local program link with no credentials (StandardProgramLinkSetup).</summary>
	public class AlliedstarConnectProgramLinkSetup:StandardProgramLinkSetup {
		public override ProgramName ProgName {
			get {
				return ProgramName.AlliedstarConnect;
			}
		}

		public override bool EnsureProgram() {
			return AlliedstarConnectLink.EnsureProgram();
		}
	}

	///<summary>OrthoComposite: an ordinary local program link with no credentials (StandardProgramLinkSetup).</summary>
	public class OrthoCompositeProgramLinkSetup:StandardProgramLinkSetup {
		public override ProgramName ProgName {
			get {
				return ProgramName.OrthoComposite;
			}
		}

		public override bool EnsureProgram() {
			return OrthoCompositeLink.EnsureProgram();
		}
	}
}
