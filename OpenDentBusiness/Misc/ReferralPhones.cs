// M2-MISC (Open Dental 25.4): the referral's primary phone (referral.Telephone) is no longer limited to exactly 10 digits. It holds up to 30
// characters, with formatting and extensions, like the other phone fields. The column was widened to varchar(30) by M2-01 (level 2026102401).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Text.RegularExpressions;

namespace OpenDentBusiness {
	///<summary>Rules for the referral's primary phone. A plain 10-digit number keeps being stored as 10 digits, as before 25.4, so letters, sheets,
	///exports and bridges that format or send 10-digit values work as they did. Anything else is stored as typed.</summary>
	public static class ReferralPhones {
		///<summary>The width of referral.Telephone since Open Dental 25.4.</summary>
		public const int MAX_LENGTH=30;

		///<summary>The text shown in the Referral window for a stored value: 10-digit values in the office phone format (for example (503)555-1234
		///in the US), anything else as stored.</summary>
		public static string GetDisplay(string telephone) {
			if(string.IsNullOrEmpty(telephone)) {
				return "";
			}
			if(Regex.IsMatch(telephone,@"^\d{10}$")) {
				return TelephoneNumbers.ReFormat(telephone);
			}
			return telephone;
		}

		///<summary>The value stored for what the user typed. Trimmed. A number that is exactly 10 digits once the usual punctuation (parentheses,
		///dashes, dots, spaces) is removed is stored as the 10 digits; anything else (11 digits, extensions, international numbers) is stored as
		///typed.</summary>
		public static string GetStorage(string text) {
			if(text==null) {
				return "";
			}
			string trimmed=text.Trim();
			string digitsOnly=Regex.Replace(trimmed,@"[\s().\-]","");
			if(Regex.IsMatch(digitsOnly,@"^\d{10}$")) {
				return digitsOnly;
			}
			return trimmed;
		}

		///<summary>An error message for the user, or "" when the text can be saved. The only limit left is the column width.</summary>
		public static string GetError(string text) {
			if(GetStorage(text).Length>MAX_LENGTH) {
				return "Phone must be "+MAX_LENGTH+" characters or less.";
			}
			return "";
		}

		///<summary>The referral phone copied from a patient's home phone when a patient is added as a referral: the stored form of the home phone,
		///or "" when it would not fit.</summary>
		public static string FromPatientPhone(string hmPhone) {
			string telephone=GetStorage(hmPhone);
			if(telephone.Length>MAX_LENGTH) {
				return "";
			}
			return telephone;
		}
	}
}
