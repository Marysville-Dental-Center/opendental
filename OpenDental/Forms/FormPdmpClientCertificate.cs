// Vendor-direct integrations: placeholder for choosing the office's PDMP client certificate.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Windows.Forms;
using OpenDentBusiness;

namespace OpenDental {
	///<summary>Placeholder registered in OpenDental.csproj by the foundation package (F0). The PDMP package builds this window.
	///See docs/specs/vendor-direct/README.md, section 3.2.</summary>
	public partial class FormPdmpClientCertificate:FormODBase {
		public FormPdmpClientCertificate() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}
	}
}
