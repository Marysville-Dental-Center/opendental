// B-PRINT: e-mailed statements carry the patient's last name (Open Dental 25.3).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenPmsDentalBusiness {
	public partial class Statements {
		///<summary>The file name prefix of an e-mailed statement's PDF. Kept from 24.3, where every attachment was named "Statement.pdf".</summary>
		public const string EMAIL_PDF_NAME_PREFIX="Statement";
		///<summary>The longest last name kept in the attachment name. Longer names are cut.</summary>
		public const int EMAIL_PDF_LNAME_MAX_LENGTH=40;

		///<summary>The name the recipient sees for an e-mailed statement's PDF: "Statement_LastName.pdf", for example "Statement_Smith.pdf", so someone
		///who receives the statements of several families can tell them apart. Pass the statement's patient (the guarantor for a family statement).
		///Only the last name is used: never the first name, birth date or account number. Characters Windows does not allow in file names, and
		///control characters, are dropped; runs of spaces become one. "Statement.pdf" when there is no patient or no usable last name.</summary>
		public static string GetEmailPdfDisplayName(Patient patient) {
			Meth.NoCheckMiddleTierRole();
			string lastName=GetFileNameSafeLastName(patient?.LName);
			if(lastName=="") {
				return EMAIL_PDF_NAME_PREFIX+".pdf";
			}
			return EMAIL_PDF_NAME_PREFIX+"_"+lastName+".pdf";
		}

		///<summary>The last name with characters that are not allowed in a file name removed, spaces collapsed, trimmed, and cut to
		///EMAIL_PDF_LNAME_MAX_LENGTH characters. Dots at the ends are removed too, so the name never looks like a second extension.</summary>
		public static string GetFileNameSafeLastName(string lastName) {
			Meth.NoCheckMiddleTierRole();
			if(string.IsNullOrWhiteSpace(lastName)) {
				return "";
			}
			char[] arrayCharsInvalid=Path.GetInvalidFileNameChars();
			StringBuilder stringBuilder=new StringBuilder();
			bool isLastSpace=false;
			foreach(char c in lastName) {
				if(char.IsWhiteSpace(c)) {//Before the invalid characters: a tab or line break between two names still separates them.
					if(!isLastSpace && stringBuilder.Length>0) {
						stringBuilder.Append(' ');
					}
					isLastSpace=true;
					continue;
				}
				if(arrayCharsInvalid.Contains(c) || char.IsControl(c)) {
					continue;
				}
				isLastSpace=false;
				stringBuilder.Append(c);
			}
			string result=stringBuilder.ToString().Trim().Trim('.').Trim();
			if(result.Length>EMAIL_PDF_LNAME_MAX_LENGTH) {
				result=result.Substring(0,EMAIL_PDF_LNAME_MAX_LENGTH).Trim();
			}
			return result;
		}
	}
}
