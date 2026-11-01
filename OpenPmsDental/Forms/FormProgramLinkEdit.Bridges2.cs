// V-BRIDGE2 (Open Dental 25.4, v25_4-bridges-trophy-enhanced-tags): help on the Command line box for program links whose bridge reads it.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Windows.Forms;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.VendorDirect.ProgramLinks;

namespace OpenPmsDental {
	public partial class FormProgramLinkEdit {
		///<summary>Shows what the Command line does for the Trophy (Enhanced) link as a tool tip on the box and its label. Other links are unchanged.
		///</summary>
		private void SetCommandLineHint() {
			if(ProgramCur==null || ProgramCur.ProgName!=ProgramName.TrophyEnhanced.ToString()) {
				return;
			}
			if(components==null) {
				components=new System.ComponentModel.Container();
			}
			ToolTip toolTip=new ToolTip(components);
			toolTip.AutoPopDelay=30000;
			string hint=Lan.g(this,TrophyEnhancedCommandLine.CommandLineHint);
			toolTip.SetToolTip(textCommandLine,hint);
			toolTip.SetToolTip(label4,hint);
		}
	}
}
