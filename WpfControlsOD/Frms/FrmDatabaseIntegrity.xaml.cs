using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenDentBusiness;
using WpfControls.UI;

namespace OpenDental {
	///<summary>Message box shown when a record's tamper-detection hash does not match, meaning the record was changed outside of this program.
	///Set MessageToShow to describe which entries are affected. </summary>
	public partial class FrmDatabaseIntegrity:FrmODBase {
		///<summary>This can be quite long.</summary>
		public string MessageToShow;

		public FrmDatabaseIntegrity() {
			InitializeComponent();
			Load+=FormDatabaseIntegrity_Load;
		}

		private void FormDatabaseIntegrity_Load(object sender,EventArgs e) {
			Lang.F(this);
			labelMessage.Text=MessageToShow;
		}

	}
}