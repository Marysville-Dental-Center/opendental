// Wording of the "Write-Offs for Non-Covered Services Overrides" in Edit Insurance Plan (backlog item v25_4-family-writeoff-override-wording,
// package M2-INSPLAN). Text only: the values the window stores (insplan.ExclusionFeeRule 0/1/2 and the two YN overrides 0/1/2) and the logic
// that reads them are unchanged.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	///<summary>Help text for the write-off overrides of an insurance plan. "UCR" is called the standard provider fee. The Exclusions options
	///themselves come from ExclusionRule's descriptions (M2-TERM renamed UseUcrFee's to "Use Standard Provider Fee").</summary>
	public static class InsPlanWriteOffL {
		///<summary>Help text for the Exclusions override.</summary>
		public static string GetExclusionsHelp() {
			return Lan.g("FormInsPlan","Practice Default follows the exclusions setting in Preferences, Family Insurance.")+"\r\n"
				+Lan.g("FormInsPlan","Do Nothing applies the plan's normal fee and write-off.")+"\r\n"
				+Lan.g("FormInsPlan","Use Standard Provider Fee bills the full standard fee with no write-off.");
		}

		///<summary>Help text for the Annual Max override.</summary>
		public static string GetAnnualMaxHelp() {
			return Lan.g("FormInsPlan","Yes bills the full standard provider fee with no write-off once the annual max is reached.")+"\r\n"
				+Lan.g("FormInsPlan","No keeps the normal fee and write-off.")+"\r\n"
				+Lan.g("FormInsPlan","Default follows the annual max setting in Preferences, Family Insurance.");
		}

		///<summary>Help text for the Age or Frequency Limit override.</summary>
		public static string GetFreqOrAgingHelp() {
			return Lan.g("FormInsPlan","Yes bills the full standard provider fee with no write-off once an age or frequency limit is reached.")+"\r\n"
				+Lan.g("FormInsPlan","No keeps the normal fee and write-off.")+"\r\n"
				+Lan.g("FormInsPlan","Default follows the age and frequency setting in Preferences, Family Insurance.");
		}

		///<summary>Help text for the plan's "Claims show standard fee, not billed fee" check box (insplan.ClaimsUseUCR).</summary>
		public static string GetClaimsUseStandardFeeHelp() {
			return Lan.g("FormInsPlan","Claims for this plan show the standard provider fee instead of the billed fee.");
		}
	}
}
