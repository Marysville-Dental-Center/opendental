// V-BRIDGE1: puts the Overjet, DTX Studio, DTX Studio Acquire and Insurance Answers Plus program links in Setup > Program Links. Each one inserts its
// rows (disabled) when Program Links opens and is edited in the standard Program Link window. Found by ProgramLinkSetupRegistry.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Windows.Forms;
using OpenDentBusiness;
using OpenDentBusiness.VendorDirect.ProgramLinks;

namespace OpenDental.VendorDirect {
	///<summary>A program link that uses the standard Program Link window (FormProgramLinkEdit) for its setup.</summary>
	public abstract class StandardProgramLinkSetup:IProgramLinkSetup {
		public abstract ProgramName ProgName { get; }

		public abstract bool EnsureProgram();

		public DialogResult OpenSetup(IWin32Window owner) {
			Program program=Programs.GetCur(ProgName);
			if(program==null) {
				Programs.RefreshCache();
				program=Programs.GetCur(ProgName);
				if(program==null) {
					return DialogResult.Cancel;
				}
			}
			using(FormProgramLinkEdit formProgramLinkEdit=new FormProgramLinkEdit()) {
				if(Programs.IsStatic(program)) {
					formProgramLinkEdit.AllowToolbarChanges=false;
				}
				formProgramLinkEdit.ProgramCur=program.Copy();
				return formProgramLinkEdit.ShowDialog(owner);
			}
		}
	}

	public class OverjetProgramLinkSetup:StandardProgramLinkSetup {
		public override ProgramName ProgName {
			get {
				return ProgramName.Overjet;
			}
		}

		public override bool EnsureProgram() {
			return OverjetLink.EnsureProgram();
		}
	}

	public class DtxStudioProgramLinkSetup:StandardProgramLinkSetup {
		public override ProgramName ProgName {
			get {
				return ProgramName.DTXStudio;
			}
		}

		public override bool EnsureProgram() {
			return DtxStudioLink.EnsureProgram(ProgramName.DTXStudio);
		}
	}

	public class DtxStudioAcquireProgramLinkSetup:StandardProgramLinkSetup {
		public override ProgramName ProgName {
			get {
				return ProgramName.DTXStudioAcquire;
			}
		}

		public override bool EnsureProgram() {
			return DtxStudioLink.EnsureProgram(ProgramName.DTXStudioAcquire);
		}
	}

	///<summary>The IAP row exists in every database. EnsureProgram clears a path left over from the IAP desktop program (IapLink).</summary>
	public class IapProgramLinkSetup:StandardProgramLinkSetup {
		public override ProgramName ProgName {
			get {
				return ProgramName.IAP;
			}
		}

		public override bool EnsureProgram() {
			return IapLink.EnsureProgram();
		}
	}
}
