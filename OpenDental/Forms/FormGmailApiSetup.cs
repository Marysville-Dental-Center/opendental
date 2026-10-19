// Vendor-direct integrations: placeholder for the Gmail API program link's setup window.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Windows.Forms;
using OpenDentBusiness;

namespace OpenDental {
	///<summary>Placeholder registered in OpenDental.csproj by the foundation package (F0). The GMAIL-LIB package builds this window.
	///See docs/specs/vendor-direct/README.md, section 3.2.</summary>
	public partial class FormGmailApiSetup:FormODBase {
		public FormGmailApiSetup() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}
	}
}
