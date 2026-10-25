// V-BRIDGE2 (Open Dental 25.4, v25_4-bridges-cleverone): puts the Clever One by VATECH program link into Setup > Program Links.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Windows.Forms;
using OpenDentBusiness;
using OpenDentBusiness.VendorDirect.ProgramLinks;

namespace OpenDental.VendorDirect {
	///<summary>Inserts the CleverOne row (Enabled=0, default path, Chart module button, patient id property) the first time Program Links opens, and
	///opens it in the standard program link window: Clever One is an ordinary local program link with no credentials.</summary>
	public class CleverOneProgramLinkSetup:IProgramLinkSetup {
		public ProgramName ProgName {
			get {
				return ProgramName.CleverOne;
			}
		}

		public bool EnsureProgram() {
			return CleverOneProgram.EnsureProgram();
		}

		public DialogResult OpenSetup(IWin32Window owner) {
			Programs.RefreshCache();
			Program program=Programs.GetCur(ProgramName.CleverOne);
			if(program==null) {
				return DialogResult.Cancel;
			}
			using FormProgramLinkEdit formProgramLinkEdit=new FormProgramLinkEdit();
			formProgramLinkEdit.ProgramCur=program.Copy();
			return formProgramLinkEdit.ShowDialog(owner);
		}
	}
}
