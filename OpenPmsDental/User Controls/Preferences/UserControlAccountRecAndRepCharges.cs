using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CodeBase;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations.Seeds26_1;

namespace OpenPmsDental {
	public partial class UserControlAccountRecAndRepCharges:UserControl {

		#region Fields - Private
		///<summary>The failure e-mail template being edited, as the preferences store it (blank uses the default text). Saved with the other
		///preferences.</summary>
		private string _failEmailSubject;
		private string _failEmailBody;
		#endregion Fields - Private

		#region Fields - Public
		public bool Changed;
		#endregion Fields - Public

		#region Constructors
		public UserControlAccountRecAndRepCharges() {
			InitializeComponent();
			Font=LayoutManagerForms.FontInitial;
		}
		#endregion Constructors

		#region Methods - Event Handlers
		private void checkRecurringChargesAutomated_Click(object sender,EventArgs e) {
			if(checkRecurringChargesAutomated.Checked) {
				labelRecurringChargesAutomatedTime.ForeColor=Color.Black;
				textRecurringChargesTime.Enabled=true;
				return;
			}
			//labelRecurringChargesAutomatedTime.Enabled=false;//Can't do this because we want hover info to still work
			labelRecurringChargesAutomatedTime.ForeColor=ColorOD.DisabledText;
			textRecurringChargesTime.Enabled=false;
		}

		private void butRecurringChargesFailEmailTemplate_Click(object sender,EventArgs e) {
			//A blank preference shows the default text the e-mail would use.
			string subject=string.IsNullOrWhiteSpace(_failEmailSubject) ? Seed26_1.RECURRING_FAIL_EMAIL_SUBJECT_DEFAULT : _failEmailSubject;
			string body=string.IsNullOrWhiteSpace(_failEmailBody) ? Seed26_1.RECURRING_FAIL_EMAIL_BODY_DEFAULT : _failEmailBody;
			using FormRecurringChargeFailEmailTemplate formRecurringChargeFailEmailTemplate=new FormRecurringChargeFailEmailTemplate(subject,body);
			formRecurringChargeFailEmailTemplate.ShowDialog();
			if(formRecurringChargeFailEmailTemplate.DialogResult!=DialogResult.OK) {
				return;
			}
			_failEmailSubject=formRecurringChargeFailEmailTemplate.SubjectTemplate;
			_failEmailBody=formRecurringChargeFailEmailTemplate.BodyTemplate;
		}

		private void checkRepeatingChargesAutomated_Click(object sender,EventArgs e) {
			if(checkRepeatingChargesAutomated.Checked) {
				labelRepeatingChargesAutomatedTime.ForeColor=Color.Black;
				textRepeatingChargesAutomatedTime.Enabled=true;
				return;
			}
			//labelRepeatingChargesAutomatedTime.Enabled=false;//Can't do this because we want hover info to still work
			labelRepeatingChargesAutomatedTime.ForeColor=ColorOD.DisabledText;
			textRepeatingChargesAutomatedTime.Enabled=false;
		}

		///<summary>Turning on automated repeating charges, but recurring charges are also enabled and set to run before auto repeating charges.  Prompt user that this is unadvisable.</summary>
		private void PromptRecurringRepeatingChargesTimes(object sender,EventArgs e) {
			if(checkRepeatingChargesAutomated.Checked && checkRecurringChargesAutomated.Checked
				&& PIn.DateT(textRepeatingChargesAutomatedTime.Text).TimeOfDay>=PIn.DateT(textRecurringChargesTime.Text).TimeOfDay)
			{
				MsgBox.Show(this,"Recurring charges run time is currently set before Repeating charges run time.\r\nConsider setting repeating charges to "
					+"automatically run before recurring charges.");
			}
		}
		#endregion Methods - Event Handlers

		#region Methods - Private
		#endregion Methods - Private

		#region Methods - Public
		public void FillAccountRecAndRepCharges() {
			#region Pay/Adj Group Boxes
			comboRecurringChargePayType.Items.AddDefNone("("+Lan.g(this,"default")+")");
			comboRecurringChargePayType.Items.AddDefs(Defs.GetDefsForCategory(DefCat.PaymentTypes,true));
			comboRecurringChargePayType.SetSelectedDefNum(PrefC.GetLong(PrefName.RecurringChargesPayTypeCC));
			#endregion Pay/Adj Group Boxes
			#region Misc Account
			checkRecurChargPriProv.Checked=PrefC.GetBool(PrefName.RecurringChargesUsePriProv);
			checkRecurringChargesUseTransDate.Checked=PrefC.GetBool(PrefName.RecurringChargesUseTransDate);
			checkRecurringChargesShowInactive.Checked=PrefC.GetBool(PrefName.RecurringChargesShowInactive);
			checkRecurringChargesAutomated.Checked=PrefC.GetBool(PrefName.RecurringChargesAutomatedEnabled);
			labelRecurringChargesAutomatedTime.ForeColor=ColorOD.DisabledText;
			textRecurringChargesTime.Text=PrefC.GetDateT(PrefName.RecurringChargesAutomatedTime).TimeOfDay.ToShortTimeString();
			checkRecurPatBal0.Checked=PrefC.GetBool(PrefName.RecurringChargesAllowedWhenNoPatBal);
			checkRecurringChargesInactivateDeclinedCards.Checked=PrefC.GetBool(PrefName.RecurringChargesInactivateDeclinedCards);
			checkRecurringChargesFailEmailEnabled.Checked=PrefC.GetBool(PrefName.RecurringChargesFailEmailEnabled);
			//The stored text, so that saving with an untouched blank template keeps it blank (RecurringChargeFailEmails then uses the default).
			_failEmailSubject=PrefC.GetStringSilent(PrefName.RecurringChargesFailEmailSubject);
			_failEmailBody=PrefC.GetStringSilent(PrefName.RecurringChargesFailEmailBody);
			if(PrefC.GetBool(PrefName.EasyHideRepeatCharges)) {
				//groupRepeatingCharges.Enabled=false;//Can't do this because we want hover info to still work
				checkRepeatingChargesRunAging.Enabled=false;
				checkRepeatingChargesAutomated.Enabled=false;
				checkRepeatingChargesAllowBackdating.Enabled=false;
				labelRepeatingChargesAutomatedTime.ForeColor=ColorOD.DisabledText;
				textRepeatingChargesAutomatedTime.Enabled=false;
			}
			checkRepeatingChargesRunAging.Checked=PrefC.GetBool(PrefName.RepeatingChargesRunAging);
			checkRepeatingChargesAutomated.Checked=PrefC.GetBool(PrefName.RepeatingChargesAutomated);
			textRepeatingChargesAutomatedTime.Text=PrefC.GetDateT(PrefName.RepeatingChargesAutomatedTime).TimeOfDay.ToShortTimeString();
			checkRepeatingChargesAllowBackdating.Checked=PrefC.GetBool(PrefName.RepeatingChargesAllowBackdating);
			#endregion Misc Account
		}

		public bool SaveAccountRecAndRepCharges() {
			if(checkRecurringChargesAutomated.Checked
				&& (string.IsNullOrWhiteSpace(textRecurringChargesTime.Text) || !textRecurringChargesTime.IsValid())) 
			{
				MsgBox.Show(this,"Recurring charge time must be a valid time.");
				return false;
			}
			if(checkRepeatingChargesAutomated.Checked
				&& (string.IsNullOrWhiteSpace(textRepeatingChargesAutomatedTime.Text) || !textRepeatingChargesAutomatedTime.IsValid())) 
			{
				MsgBox.Show(this,"Repeating charge time must be a valid time.");
				return false;
			}
			Changed|=Prefs.UpdateBool(PrefName.RecurringChargesUsePriProv,checkRecurChargPriProv.Checked);
			Changed|=Prefs.UpdateBool(PrefName.RecurringChargesUseTransDate,checkRecurringChargesUseTransDate.Checked);
			Changed|=Prefs.UpdateBool(PrefName.RecurringChargesShowInactive,checkRecurringChargesShowInactive.Checked);
			Changed|=Prefs.UpdateBool(PrefName.RecurringChargesAutomatedEnabled,checkRecurringChargesAutomated.Checked);
			Changed|=Prefs.UpdateDateT(PrefName.RecurringChargesAutomatedTime,PIn.DateT(textRecurringChargesTime.Text));
			Changed|=Prefs.UpdateBool(PrefName.RepeatingChargesAutomated,checkRepeatingChargesAutomated.Checked);
			Changed|=Prefs.UpdateDateT(PrefName.RepeatingChargesAutomatedTime,PIn.DateT(textRepeatingChargesAutomatedTime.Text));
			Changed|=Prefs.UpdateBool(PrefName.RepeatingChargesRunAging,checkRepeatingChargesRunAging.Checked);
			Changed|=Prefs.UpdateLong(PrefName.RecurringChargesPayTypeCC,comboRecurringChargePayType.GetSelectedDefNum());
			Changed|=Prefs.UpdateBool(PrefName.RecurringChargesAllowedWhenNoPatBal,checkRecurPatBal0.Checked);
			Changed|=Prefs.UpdateBool(PrefName.RecurringChargesInactivateDeclinedCards,checkRecurringChargesInactivateDeclinedCards.Checked);
			Changed|=Prefs.UpdateBool(PrefName.RecurringChargesFailEmailEnabled,checkRecurringChargesFailEmailEnabled.Checked);
			Changed|=Prefs.UpdateString(PrefName.RecurringChargesFailEmailSubject,_failEmailSubject);
			Changed|=Prefs.UpdateString(PrefName.RecurringChargesFailEmailBody,_failEmailBody);
			Changed|=Prefs.UpdateBool(PrefName.RepeatingChargesAllowBackdating,checkRepeatingChargesAllowBackdating.Checked);
			return true;
		}
		#endregion Methods - Public
	}
}
