// Vendor-direct integrations: placeholder for the EdgeExpress hosted payment page window.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Windows.Forms;
using OpenDentBusiness;

namespace OpenDental {
	///<summary>Placeholder registered in OpenDental.csproj by the foundation package (F0). The EDGEEXPRESS-LIB package builds this window.
	///See docs/specs/vendor-direct/README.md, section 3.2.</summary>
	public partial class FormEdgeExpressPayPage:FormODBase {
		public FormEdgeExpressPayPage() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}
	}
}
