// V-BRIDGE2 (Open Dental 25.4, v25_4-bridges-cleverone): puts the Clever One by VATECH program link into Setup > Program Links.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using OpenDentBusiness;
using OpenDentBusiness.VendorDirect.ProgramLinks;

namespace OpenDental.VendorDirect {
	///<summary>Inserts the CleverOne row (Enabled=0, default path, Chart module button, patient id property) the first time Program Links opens, and
	///opens it in the standard program link window (StandardProgramLinkSetup, shared with the V-BRIDGE1 links): Clever One is an ordinary local
	///program link with no credentials.</summary>
	public class CleverOneProgramLinkSetup:StandardProgramLinkSetup {
		public override ProgramName ProgName {
			get {
				return ProgramName.CleverOne;
			}
		}

		public override bool EnsureProgram() {
			return CleverOneProgram.EnsureProgram();
		}
	}
}
