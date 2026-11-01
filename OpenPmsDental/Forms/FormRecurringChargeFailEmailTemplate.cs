// The template of the e-mail sent when a recurring charge is declined (Open Dental 25.4, item v25_4-account-recurringcharge-fail-email, package
// M2-RECUR in docs/specs/port-backlog.md). Opened from Preferences, Account, Rec and Rep Charges.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Windows.Forms;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations.Seeds26_1;

namespace OpenPmsDental {
	///<summary>Edits the subject and body of the failure e-mail. Saves nothing itself: the Preferences window saves the text with the other
	///preferences.</summary>
	public partial class FormRecurringChargeFailEmailTemplate:FormODBase {
		///<summary>The subject when the window closes with OK.</summary>
		public string SubjectTemplate;
		///<summary>The body when the window closes with OK.</summary>
		public string BodyTemplate;

		///<summary>For the designer only.</summary>
		public FormRecurringChargeFailEmailTemplate() : this("","") {
		}

		public FormRecurringChargeFailEmailTemplate(string subjectTemplate,string bodyTemplate) {
			SubjectTemplate=subjectTemplate??"";
			BodyTemplate=bodyTemplate??"";
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormRecurringChargeFailEmailTemplate_Load(object sender,EventArgs e) {
			textSubject.Text=SubjectTemplate;
			textBody.Text=BodyTemplate;
			labelTags.Text=Lan.g(this,"Replacement tags")+": "+string.Join(" ",RecurringChargeFailEmails.ListTags);
		}

		private void butDefault_Click(object sender,EventArgs e) {
			if(!MsgBox.Show(this,MsgBoxButtons.OKCancel,"Replace the subject and body with the default text?")) {
				return;
			}
			textSubject.Text=Seed26_1.RECURRING_FAIL_EMAIL_SUBJECT_DEFAULT;
			textBody.Text=Seed26_1.RECURRING_FAIL_EMAIL_BODY_DEFAULT;
		}

		private void butSave_Click(object sender,EventArgs e) {
			if(string.IsNullOrWhiteSpace(textSubject.Text) || string.IsNullOrWhiteSpace(textBody.Text)) {
				MsgBox.Show(this,"The subject and the body cannot be blank.");
				return;
			}
			SubjectTemplate=textSubject.Text;
			BodyTemplate=textBody.Text;
			DialogResult=DialogResult.OK;
		}
	}
}
