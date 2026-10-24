using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using OpenDentBusiness;

namespace OpenDental {
	///<summary>An empty window that is entirely filled by a copy of the old Images module.  Used from FormClaimEdit to view EOB.  Also, from FormClaimPayBatch to view EOB.  Also from FormClaimPayPreAuth and FormClaimEdit for the EOBs of a preauthorization.  Finally, from FormEhrAmendmentEdit to scan.</summary>
	public partial class FormImages:FormODBase {
		///<summary>Right now, this form supports claimpayment, preauthorization and amendment mode. Set only one of ClaimPaymentNum and ClaimNumPreAuth.</summary>
		public long ClaimPaymentNum;
		///<summary>Open Dental 25.2: the EOBs of this preauthorization (claim.ClaimNum of a PreAuth claim), stored with eobattach.ClaimNumPreAuth.</summary>
		public long ClaimNumPreAuth;
		public EhrAmendment EhrAmendmentCur;

		public FormImages() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormImages_Shown(object sender,EventArgs e) {
			if(ClaimNumPreAuth!=0) {
				contrImagesMain.ModuleSelectedPreAuth(ClaimNumPreAuth);
			}
			else if(ClaimPaymentNum!=0) {
				contrImagesMain.ModuleSelectedClaimPayment(ClaimPaymentNum);
			}
			else if(EhrAmendmentCur!=null) {
				contrImagesMain.ModuleSelectedAmendment(EhrAmendmentCur);
			}
			contrImagesMain.CloseClick+=ContrImagesMain_CloseClick;
		}

		private void ContrImagesMain_CloseClick(object sender,EventArgs e) {
			DialogResult=DialogResult.OK;
		}
	}
}