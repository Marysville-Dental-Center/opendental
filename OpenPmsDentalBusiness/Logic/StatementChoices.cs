// The Statement Options window (Open Dental 26.2 release notes: "Moved Statement dropdown options to a new window"; package P262-BILLING,
// docs/port-notes/P262-BILLING.md). The descriptions are this program's own wording.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;

namespace OpenPmsDentalBusiness {
	///<summary>The kinds of statement the Account module's Statement button offers. Before 26.2 all but Statement were entries of the button's
	///dropdown menu; since 26.2 the dropdown arrow opens the Statement Options window, which lists them with a description.</summary>
	public enum StatementChoice {
		///<summary>The button itself: the default statement, printed at once.</summary>
		Statement,
		Walkout,
		Email,
		Receipt,
		Invoice,
		Limited,
		LimitedCustom,
		///<summary>The Statement window, every option set by hand ("More Options").</summary>
		MoreOptions,
		///<summary>The Service Date View window (Open Dental 26.1, M2-SDV).</summary>
		ServiceDateView,
	}

	///<summary>The descriptions the Statement Options window shows next to each choice.</summary>
	public class StatementChoices {
		///<summary>The choices in the order the window lists them: the order of the former dropdown menu, after the default statement.</summary>
		public static List<StatementChoice> GetAllInOrder() {
			Meth.NoCheckMiddleTierRole();
			return new List<StatementChoice>() {
				StatementChoice.Statement,
				StatementChoice.Walkout,
				StatementChoice.Email,
				StatementChoice.Receipt,
				StatementChoice.Invoice,
				StatementChoice.Limited,
				StatementChoice.LimitedCustom,
				StatementChoice.MoreOptions,
				StatementChoice.ServiceDateView,
			};
		}

		///<summary>The English title of a choice (the window shows the translated text of the menu entry instead when it has one).</summary>
		public static string GetTitle(StatementChoice statementChoice) {
			Meth.NoCheckMiddleTierRole();
			switch(statementChoice) {
				case StatementChoice.Statement:
					return "Statement";
				case StatementChoice.Walkout:
					return "Walkout";
				case StatementChoice.Email:
					return "Email";
				case StatementChoice.Receipt:
					return "Receipt";
				case StatementChoice.Invoice:
					return "Invoice";
				case StatementChoice.Limited:
					return "Limited";
				case StatementChoice.LimitedCustom:
					return "Limited (Custom)";
				case StatementChoice.MoreOptions:
					return "More Options";
				case StatementChoice.ServiceDateView:
					return "Service Date View Window";
				default:
					return statementChoice.ToString();
			}
		}

		///<summary>The English description of a choice, translated by the window.</summary>
		public static string GetDescription(StatementChoice statementChoice) {
			Meth.NoCheckMiddleTierRole();
			switch(statementChoice) {
				case StatementChoice.Statement:
					return "Prints the family's statement now, for the dates in the Account module or the days set in Billing Defaults.";
				case StatementChoice.Walkout:
					return "Prints today's activity of this patient, without payment options.";
				case StatementChoice.Email:
					return "Emails the family's statement as a PDF, with the subject and body set in Billing Defaults.";
				case StatementChoice.Receipt:
					return "Prints a receipt of today's payments.";
				case StatementChoice.Invoice:
					return "Makes an invoice of the procedures and adjustments selected in the account.";
				case StatementChoice.Limited:
					return "Makes a limited statement of the transactions selected in the account.";
				case StatementChoice.LimitedCustom:
					return "Opens a window to choose the transactions of a limited statement.";
				case StatementChoice.MoreOptions:
					return "Opens the Statement window to set every option before printing, emailing or viewing.";
				case StatementChoice.ServiceDateView:
					return "Opens the Service Date View window of this patient or family.";
				default:
					return "";
			}
		}
	}
}
