// Character rules shared by the Database Maintenance text tools of backlog package B-DBM: the Raw Emails clean-up (24.4), the
// EmailmessageInvalidCharactersInBodyText check (25.1) and the Invalid Characters (GDI+) tool (25.2). Open Dental publishes only the names of these
// tools, so the character sets are our own (see docs/port-notes/B-DBM.md).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Globalization;
using System.Text;

namespace OpenPmsDentalBusiness {
	///<summary>Finds, shows and removes characters that break display, printing or re-sending of text stored in the database.</summary>
	public static class DbmTextCleaner {
		///<summary>SQL (MariaDB/MySQL REGEXP on the raw bytes) for a C0 control character other than tab, line feed and carriage return. In UTF-8 the
		///bytes 0x00 to 0x1F only ever encode those characters, so the byte match is exact.</summary>
		private const string SQL_REGEX_CONTROL=@"[\\x00-\\x08\\x0B\\x0C\\x0E-\\x1F]";
		///<summary>SQL for U+FFFE or U+FFFF: their UTF-8 bytes, which no other character contains.</summary>
		private const string SQL_REGEX_NONCHARACTER=@"\\xEF\\xBF[\\xBE\\xBF]";

		#region Character classes
		///<summary>True for a C0 control character other than tab, line feed and carriage return (U+0000 to U+0008, U+000B, U+000C, U+000E to
		///U+001F).</summary>
		public static bool IsControlCharToRemove(char c) {
			return c<0x20 && c!='\t' && c!='\n' && c!='\r';
		}

		///<summary>True when the character at index is half of a UTF-16 surrogate pair whose other half is missing.</summary>
		public static bool IsUnpairedSurrogate(string text,int index) {
			char c=text[index];
			if(char.IsHighSurrogate(c)) {
				return index+1>=text.Length || !char.IsLowSurrogate(text[index+1]);
			}
			if(char.IsLowSurrogate(c)) {
				return index==0 || !char.IsHighSurrogate(text[index-1]);
			}
			return false;
		}

		///<summary>The 25.1 email body rule: a C0 control character other than tab, CR and LF, or an unpaired surrogate.</summary>
		public static bool IsEmailBodyInvalidAt(string text,int index) {
			return IsControlCharToRemove(text[index]) || IsUnpairedSurrogate(text,index);
		}

		///<summary>The 25.2 GDI+ rule: the email body rule plus the noncharacters U+FFFE and U+FFFF.</summary>
		public static bool IsGdiInvalidAt(string text,int index) {
			return IsEmailBodyInvalidAt(text,index) || text[index]=='\uFFFE' || text[index]=='\uFFFF';
		}

		///<summary>True for a Unicode space separator (category Zs) other than the ASCII space: the no-break space, U+1680, U+2000 to U+200A, U+202F,
		///U+205F and the ideographic space U+3000.</summary>
		public static bool IsUnicodeSpaceToNormalize(char c) {
			return c!=' ' && CharUnicodeInfo.GetUnicodeCategory(c)==UnicodeCategory.SpaceSeparator;
		}

		///<summary>True for the zero-width characters that are removed: U+200B to U+200D and the byte-order mark U+FEFF.</summary>
		public static bool IsZeroWidthToRemove(char c) {
			return (c>='\u200B' && c<='\u200D') || c=='\uFEFF';
		}
		#endregion Character classes

		#region Remove and count
		///<summary>Removes the characters of the email body rule (IsEmailBodyInvalidAt). Tab, CR and LF stay. Returns "" for null.</summary>
		public static string RemoveEmailBodyInvalidChars(string text) {
			return Remove(text,isGdi:false);
		}

		///<summary>Removes the characters of the GDI+ rule (IsGdiInvalidAt). Tab, CR and LF stay. Returns "" for null.</summary>
		public static string RemoveGdiInvalidChars(string text) {
			return Remove(text,isGdi:true);
		}

		///<summary>How many characters RemoveEmailBodyInvalidChars would remove.</summary>
		public static int CountEmailBodyInvalidChars(string text) {
			return Count(text,isGdi:false);
		}

		///<summary>How many characters RemoveGdiInvalidChars would remove.</summary>
		public static int CountGdiInvalidChars(string text) {
			return Count(text,isGdi:true);
		}

		///<summary>Maps every Unicode space separator to an ASCII space and removes the zero-width characters (U+200B to U+200D, U+FEFF). Line
		///breaks, including U+2028 and U+2029, are left alone. Returns "" for null.</summary>
		public static string NormalizeUnicodeWhiteSpace(string text) {
			if(string.IsNullOrEmpty(text)) {
				return text??"";
			}
			StringBuilder stringBuilder=null;
			for(int i=0;i<text.Length;i++) {
				char c=text[i];
				bool isSpace=IsUnicodeSpaceToNormalize(c);
				bool isZeroWidth=IsZeroWidthToRemove(c);
				if(!isSpace && !isZeroWidth) {
					stringBuilder?.Append(c);
					continue;
				}
				if(stringBuilder==null) {
					stringBuilder=new StringBuilder(text.Length);
					stringBuilder.Append(text,0,i);
				}
				if(isSpace) {
					stringBuilder.Append(' ');
				}
			}
			return stringBuilder==null ? text : stringBuilder.ToString();
		}

		private static bool IsInvalidAt(string text,int index,bool isGdi) {
			return isGdi ? IsGdiInvalidAt(text,index) : IsEmailBodyInvalidAt(text,index);
		}

		private static string Remove(string text,bool isGdi) {
			if(string.IsNullOrEmpty(text)) {
				return text??"";
			}
			StringBuilder stringBuilder=null;
			for(int i=0;i<text.Length;i++) {
				if(!IsInvalidAt(text,i,isGdi)) {
					stringBuilder?.Append(text[i]);
					continue;
				}
				if(stringBuilder==null) {
					stringBuilder=new StringBuilder(text.Length);
					stringBuilder.Append(text,0,i);
				}
			}
			return stringBuilder==null ? text : stringBuilder.ToString();
		}

		private static int Count(string text,bool isGdi) {
			if(string.IsNullOrEmpty(text)) {
				return 0;
			}
			int count=0;
			for(int i=0;i<text.Length;i++) {
				if(IsInvalidAt(text,i,isGdi)) {
					count++;
				}
			}
			return count;
		}
		#endregion Remove and count

		#region Display
		///<summary>A short piece of the text around its first GDI+ invalid character, for a preview grid: every invalid character is shown as
		///[U+XXXX], tabs and line breaks as spaces, and "..." marks a cut. At most contextBefore characters come before the first invalid one and the
		///result holds at most maxChars characters of the text. Returns "" when there is nothing to show.</summary>
		public static string ShowGdiInvalidChars(string text,int contextBefore=30,int maxChars=120) {
			if(string.IsNullOrEmpty(text)) {
				return "";
			}
			int idxFirst=0;
			for(int i=0;i<text.Length;i++) {
				if(IsGdiInvalidAt(text,i)) {
					idxFirst=i;
					break;
				}
			}
			int idxStart=System.Math.Max(0,idxFirst-contextBefore);
			int idxEnd=System.Math.Min(text.Length,idxStart+maxChars);
			StringBuilder stringBuilder=new StringBuilder();
			if(idxStart>0) {
				stringBuilder.Append("...");
			}
			for(int i=idxStart;i<idxEnd;i++) {
				char c=text[i];
				if(IsGdiInvalidAt(text,i)) {
					stringBuilder.Append("[U+"+((int)c).ToString("X4")+"]");
				}
				else if(c=='\r' || c=='\n' || c=='\t') {
					stringBuilder.Append(' ');
				}
				else {
					stringBuilder.Append(c);
				}
			}
			if(idxEnd<text.Length) {
				stringBuilder.Append("...");
			}
			return stringBuilder.ToString();
		}
		#endregion Display

		#region SQL
		///<summary>A MySQL/MariaDB condition that is true when the column holds a C0 control character other than tab, CR and LF. Matches the stored
		///bytes, so the result does not depend on the column's collation. Unpaired surrogates cannot be stored in UTF-8, so the C# rules find no more
		///than this.</summary>
		public static string GetSqlHasControlChars(string columnExpression) {
			return "CAST("+columnExpression+" AS BINARY) REGEXP '"+SQL_REGEX_CONTROL+"'";
		}

		///<summary>Like GetSqlHasControlChars, and also true for U+FFFE and U+FFFF: the stored-text side of IsGdiInvalidAt.</summary>
		public static string GetSqlHasGdiInvalidChars(string columnExpression) {
			return "CAST("+columnExpression+" AS BINARY) REGEXP '"+SQL_REGEX_CONTROL+"|"+SQL_REGEX_NONCHARACTER+"'";
		}
		#endregion SQL
	}
}
