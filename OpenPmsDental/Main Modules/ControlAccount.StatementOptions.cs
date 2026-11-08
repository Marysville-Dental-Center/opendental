// The Statement Options window of the Account module (Open Dental 26.2 release notes: "Moved Statement dropdown options to a new window"; package
// P262-BILLING, docs/port-notes/P262-BILLING.md). The Statement button still prints the default statement when it is clicked; its dropdown arrow,
// which showed a menu before 26.2, now opens the Statement Options window. The window lists the default statement and every entry of
// contextMenuStatement (Walkout, Email, Receipt, Invoice, Limited, Limited (Custom), More Options and the Service Date View window, plus any entry a
// plugin adds), each with a description, and runs the entry that is chosen exactly as the menu entry ran it.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	public partial class ControlAccount {
		///<summary>Called from the constructor: gives each entry of contextMenuStatement its StatementChoice (for its description in the window) and
		///connects the dropdown arrow of the Statement button.</summary>
		private void ConstructStatementOptions() {
			menuItemStatementWalkout.Tag=StatementChoice.Walkout;
			menuItemStatementEmail.Tag=StatementChoice.Email;
			menuItemReceipt.Tag=StatementChoice.Receipt;
			menuItemInvoice.Tag=StatementChoice.Invoice;
			menuItemLimited.Tag=StatementChoice.Limited;
			menuItemLimitedCustom.Tag=StatementChoice.LimitedCustom;
			menuItemStatementMore.Tag=StatementChoice.MoreOptions;
			ToolBarMain.ButtonDropDownClick+=ToolBarMain_ButtonDropDownClick;
		}

		private void ToolBarMain_ButtonDropDownClick(object sender,ODToolBarButtonClickEventArgs e) {
			if(!(e.Button.Tag is string) || e.Button.Tag.ToString()!="Statement") {
				return;
			}
			if(_patient==null) {
				return;
			}
			if(Patients.GetPat(_patient.PatNum).PatStatus==PatientStatus.Deleted) {
				MsgBox.Show(this,"Selected patient has been deleted by another workstation.");
				return;
			}
			//BeginInvoke for the same reason as the Statement button: a window opened from a toolbar click otherwise does not get the focus.
			ToolBarClick toolBarClick=ShowStatementOptions;
			this.BeginInvoke(toolBarClick);
		}

		///<summary>Opens the Statement Options window and runs the choice. The entries are read from contextMenuStatement each time, so the window
		///always offers what the dropdown menu offered.</summary>
		private void ShowStatementOptions() {
			List<MenuItem> listMenuItems=contextMenuStatement.MenuItems.Cast<MenuItem>()
				.Where(x => x.Visible && x.Enabled && x.Text!="-")
				.ToList();
			using FormStatementChoices formStatementChoices=new FormStatementChoices();
			formStatementChoices.AddChoice(Lan.g(this,StatementChoices.GetTitle(StatementChoice.Statement)),
				Lan.g("FormStatementChoices",StatementChoices.GetDescription(StatementChoice.Statement)));
			for(int i=0;i<listMenuItems.Count;i++) {
				string description="";
				if(listMenuItems[i].Tag is StatementChoice statementChoice) {
					description=Lan.g("FormStatementChoices",StatementChoices.GetDescription(statementChoice));
				}
				formStatementChoices.AddChoice(listMenuItems[i].Text.Replace("&",""),description);
			}
			formStatementChoices.ShowDialog();
			if(formStatementChoices.DialogResult!=DialogResult.OK || formStatementChoices.IndexSelected<0) {
				return;
			}
			if(formStatementChoices.IndexSelected==0) {
				toolBarButStatement_Click();
				return;
			}
			listMenuItems[formStatementChoices.IndexSelected-1].PerformClick();
		}
	}
}
