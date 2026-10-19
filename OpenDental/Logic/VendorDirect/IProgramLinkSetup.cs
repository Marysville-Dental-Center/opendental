// Vendor-direct integrations: how a vendor package plugs its setup window into Setup > Program Links.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Windows.Forms;
using OpenDentBusiness;

namespace OpenDental.VendorDirect {
	///<summary>Implemented once per vendor-direct program link, in the vendor package's own file, by a class with a public parameterless constructor.
	///ProgramLinkSetupRegistry finds it by reflection, so no package edits FormProgramLinks.</summary>
	public interface IProgramLinkSetup {
		///<summary>The program link this setup belongs to.</summary>
		ProgramName ProgName { get; }

		///<summary>Inserts the program row (Programs.InsertIfMissing, Enabled=0) and any missing property rows (ProgramProperties.InsertIfMissing).
		///Returns true if it inserted anything. Called each time Program Links opens, before its cache refresh. Must not call the vendor.</summary>
		bool EnsureProgram();

		///<summary>Shows the setup window. Returns OK if anything was saved.</summary>
		DialogResult OpenSetup(IWin32Window owner);
	}
}
