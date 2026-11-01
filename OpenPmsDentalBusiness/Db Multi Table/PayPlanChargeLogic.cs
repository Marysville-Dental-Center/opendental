// Open Dental 25.1 retired the "Pay Plan charge logic" choice (preference PayPlansVersion). Backlog package B-PAY (docs/specs/port-backlog.md);
// behavior in docs/specs/features-25.1.md (v25_1-account-payplan-charge-logic-deprecated).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>The Pay Plan charge logic is no longer something an office chooses. PayPlansVersion is still read everywhere it was read, so stored data
	///comes out exactly as before, but Preferences no longer offers the choice. A database that still holds a legacy mode (anything but Age Credits
	///and Debits) shows that mode as a read-only line. Nothing converts the value: changing it would change balances and aging.</summary>
	public class PayPlanChargeLogic {
		///<summary>The stored PayPlansVersion value.</summary>
		public static int GetStoredValue() {
			Meth.NoCheckMiddleTierRole();
			return PrefC.GetInt(PrefName.PayPlansVersion);
		}

		///<summary>True for any value but Age Credits and Debits (the default, and the only mode 25.1 and later offer), including unknown values.</summary>
		public static bool IsLegacyMode(int payPlansVersion) {
			return payPlansVersion!=(int)PayPlanVersions.AgeCreditsAndDebits;
		}

		///<summary>The read-only line Preferences shows for a legacy mode, or "" for Age Credits and Debits.</summary>
		public static string GetReadOnlyText(int payPlansVersion) {
			if(!IsLegacyMode(payPlansVersion)) {
				return "";
			}
			string modeName;
			if(Enum.IsDefined(typeof(PayPlanVersions),payPlansVersion)) {
				modeName=((PayPlanVersions)payPlansVersion).GetDescription();
			}
			else {
				modeName=Lans.g("PayPlanChargeLogic","Unknown")+" ("+payPlansVersion+")";
			}
			return Lans.g("PayPlanChargeLogic","Pay Plan charge logic (legacy, no longer selectable)")+": "+modeName;
		}
	}
}
