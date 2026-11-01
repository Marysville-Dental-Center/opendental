// Which writing systems a piece of sheet text uses (Open Dental 25.4: sheets support non-Latin languages; backlog package M2-SHEETS).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using CodeBase;

namespace OpenDentBusiness {
	///<summary>Classifies the characters of sheet text by writing system, so screen, print and PDF can pick a font that has the glyphs, lay out
	///right-to-left text right-to-left, and know when the PDF library cannot lay the text out by itself. Pure functions, no database.</summary>
	public class SheetScripts {
		///<summary>The characters outside Latin-1 that the Windows-1252 ("WinAnsi") code page holds: the euro sign, typographic quotes, dashes and
		///the like. Text made only of these and Latin-1 prints in PDFs exactly as it always has.</summary>
		private static readonly HashSet<char> _hashSetWinAnsiExtra=new HashSet<char> {
			'€','‚','ƒ','„','…','†','‡','ˆ','‰','Š','‹','Œ','Ž','‘','’',
			'“','”','•','–','—','˜','™','š','›','œ','ž','Ÿ'
		};

		///<summary>True when every character is Latin-1 or one of the Windows-1252 extras. The PDF library writes such text with its standard
		///encoding, so sheets in English, Spanish, French and the like keep the exact PDF output they had before 25.4.</summary>
		public static bool IsWinAnsiEncodable(string text) {
			if(string.IsNullOrEmpty(text)) {
				return true;
			}
			for(int i=0;i<text.Length;i++) {
				if(text[i]>'ÿ' && !_hashSetWinAnsiExtra.Contains(text[i])) {
					return false;
				}
			}
			return true;
		}

		///<summary>True when the text only needs the Latin fonts every sheet font already covers: Latin-1, the Windows-1252 extras, Latin
		///Extended-A and -B, and characters that draw nothing (spaces, controls, format marks). Font fallback is never considered for such text, so
		///existing sheets (including those that use symbol fonts) look exactly as before.</summary>
		public static bool IsLatinText(string text) {
			if(string.IsNullOrEmpty(text)) {
				return true;
			}
			for(int i=0;i<text.Length;i++) {
				char c=text[i];
				if(c<='ɏ' || _hashSetWinAnsiExtra.Contains(c) || IsIgnorableForFonts(c)) {
					continue;
				}
				return false;
			}
			return true;
		}

		///<summary>Characters that draw no glyph of their own or that fonts do not cover on purpose: controls, white space, format marks (zero-width
		///joiners, direction marks), variation selectors, surrogate halves (characters beyond the Basic Multilingual Plane, such as emoji, are out of
		///scope) and private-use characters. They never make a font look like it lacks glyphs.</summary>
		public static bool IsIgnorableForFonts(char c) {
			if(char.IsControl(c) || char.IsWhiteSpace(c) || char.IsSurrogate(c)) {
				return true;
			}
			if(c>='︀' && c<='️') {//variation selectors
				return true;
			}
			UnicodeCategory unicodeCategory=char.GetUnicodeCategory(c);
			return unicodeCategory==UnicodeCategory.Format || unicodeCategory==UnicodeCategory.PrivateUse;
		}

		///<summary>Hebrew, Arabic, Syriac, Thaana, N'Ko, Samaritan and Mandaic characters and their presentation forms, plus the explicit
		///right-to-left marks and embeddings.</summary>
		public static bool IsRightToLeftChar(char c) {
			int codePoint=c;
			return (codePoint>=0x0590 && codePoint<=0x08FF)
				|| (codePoint>=0xFB1D && codePoint<=0xFDFF)
				|| (codePoint>=0xFE70 && codePoint<=0xFEFE)
				|| c=='‏' || c=='‫' || c=='‮' || c=='⁧';
		}

		///<summary>True when the text has at least one right-to-left character.</summary>
		public static bool HasRightToLeft(string text) {
			if(string.IsNullOrEmpty(text)) {
				return false;
			}
			for(int i=0;i<text.Length;i++) {
				if(IsRightToLeftChar(text[i])) {
					return true;
				}
			}
			return false;
		}

		///<summary>True when the text reads right-to-left: its first strongly directional character (a letter, or a direction mark) is Hebrew,
		///Arabic or another right-to-left script, as in step P2 of the Unicode bidirectional algorithm. Digits and punctuation do not decide. Text
		///that starts with a Latin, Cyrillic, Chinese or other left-to-right letter reads left-to-right even if it holds right-to-left words.</summary>
		public static bool IsRightToLeft(string text) {
			if(string.IsNullOrEmpty(text)) {
				return false;
			}
			for(int i=0;i<text.Length;i++) {
				char c=text[i];
				if(c=='‏') {//right-to-left mark
					return true;
				}
				if(c=='‎') {//left-to-right mark
					return false;
				}
				if(!char.IsLetter(c)) {
					continue;
				}
				return IsRightToLeftChar(c);
			}
			return false;
		}

		///<summary>Scripts whose letters change shape or order when they are joined (Indic scripts, Thai, Lao, Tibetan, Myanmar, Khmer, Mongolian,
		///the Philippine and Indonesian scripts, conjoining Hangul jamo and the like). Precomposed Hangul syllables, Chinese, Japanese, Cyrillic and
		///Greek are not complex.</summary>
		public static bool IsComplexChar(char c) {
			int codePoint=c;
			if(codePoint>=0x0900 && codePoint<=0x0FFF) {//Devanagari to Sinhala, Thai, Lao, Tibetan
				return true;
			}
			if(codePoint>=0x1000 && codePoint<=0x109F) {//Myanmar
				return true;
			}
			if(codePoint>=0x1100 && codePoint<=0x11FF) {//Hangul jamo (conjoining)
				return true;
			}
			if(codePoint>=0x1700 && codePoint<=0x1CFF) {//Philippine scripts, Khmer, Mongolian, Limbu, Tai Le, Buginese, Tai Tham, Balinese and more
				return !(codePoint>=0x1C80 && codePoint<=0x1CBF);//except Cyrillic Extended-C and Georgian Extended
			}
			if(codePoint>=0xA800 && codePoint<=0xABFF) {//Syloti Nagri to Meetei Mayek, Hangul jamo extended-A, Javanese, Cham, Tai Viet
				return !(codePoint>=0xAB00 && codePoint<=0xABBF);//except Ethiopic Extended-A, Latin Extended-E and the Cherokee supplement
			}
			return codePoint>=0xD7B0 && codePoint<=0xD7FF;//Hangul jamo extended-B
		}

		///<summary>True when the text has right-to-left or complex-script characters. The PDF library draws characters one by one, left to right,
		///without any shaping, so such text is drawn into PDFs by the same text engine that prints it (see SheetTextDrawing).</summary>
		public static bool NeedsShaping(string text) {
			if(string.IsNullOrEmpty(text)) {
				return false;
			}
			for(int i=0;i<text.Length;i++) {
				if(IsRightToLeftChar(text[i]) || IsComplexChar(text[i])) {
					return true;
				}
			}
			return false;
		}

		///<summary>The writing system of one character, in the groups font fallback uses.</summary>
		public static SheetScriptGroup GetScriptGroup(char c) {
			int cp=c;
			if(cp<=0x024F) {
				return SheetScriptGroup.Latin;
			}
			if(cp<=0x036F || (cp>=0x1D00 && cp<=0x1DFF) || (cp>=0x1E00 && cp<=0x1EFF) || (cp>=0x2C60 && cp<=0x2C7F) || (cp>=0xA720 && cp<=0xA7FF)
				|| (cp>=0xAB30 && cp<=0xAB6F))
			{
				return SheetScriptGroup.LatinExtended;//IPA, spacing modifiers, combining marks, phonetic extensions, Latin Extended Additional, C, D, E
			}
			if((cp>=0x0370 && cp<=0x03FF) || (cp>=0x1F00 && cp<=0x1FFF)) {
				return SheetScriptGroup.Greek;
			}
			if((cp>=0x0400 && cp<=0x052F) || (cp>=0x1C80 && cp<=0x1C8F) || (cp>=0x2DE0 && cp<=0x2DFF) || (cp>=0xA640 && cp<=0xA69F)) {
				return SheetScriptGroup.Cyrillic;
			}
			if((cp>=0x0530 && cp<=0x058F) || (cp>=0xFB13 && cp<=0xFB17)) {
				return SheetScriptGroup.Armenian;
			}
			if((cp>=0x0590 && cp<=0x05FF) || (cp>=0xFB1D && cp<=0xFB4F)) {
				return SheetScriptGroup.Hebrew;
			}
			if((cp>=0x0600 && cp<=0x06FF) || (cp>=0x0750 && cp<=0x077F) || (cp>=0x0870 && cp<=0x08FF) || (cp>=0xFB50 && cp<=0xFDFF)
				|| (cp>=0xFE70 && cp<=0xFEFE))
			{
				return SheetScriptGroup.Arabic;
			}
			if((cp>=0x0700 && cp<=0x074F) || (cp>=0x0860 && cp<=0x086F)) {
				return SheetScriptGroup.Syriac;
			}
			if(cp>=0x0780 && cp<=0x07BF) {
				return SheetScriptGroup.Thaana;
			}
			if(cp>=0x07C0 && cp<=0x085F) {//N'Ko, Samaritan, Mandaic
				return SheetScriptGroup.Other;
			}
			if((cp>=0x0900 && cp<=0x0DFF) || (cp>=0x1CD0 && cp<=0x1CFF) || (cp>=0xA830 && cp<=0xA83F) || (cp>=0xA8E0 && cp<=0xA8FF)) {
				return SheetScriptGroup.Indic;
			}
			if(cp>=0x0E00 && cp<=0x0E7F) {
				return SheetScriptGroup.Thai;
			}
			if(cp>=0x0E80 && cp<=0x0EFF) {
				return SheetScriptGroup.Lao;
			}
			if(cp>=0x0F00 && cp<=0x0FFF) {
				return SheetScriptGroup.Tibetan;
			}
			if((cp>=0x1000 && cp<=0x109F) || (cp>=0xA9E0 && cp<=0xA9FF) || (cp>=0xAA60 && cp<=0xAA7F)) {
				return SheetScriptGroup.Myanmar;
			}
			if((cp>=0x10A0 && cp<=0x10FF) || (cp>=0x1C90 && cp<=0x1CBF) || (cp>=0x2D00 && cp<=0x2D2F)) {
				return SheetScriptGroup.Georgian;
			}
			if((cp>=0x1100 && cp<=0x11FF) || (cp>=0x3130 && cp<=0x318F) || (cp>=0xA960 && cp<=0xA97F) || (cp>=0xAC00 && cp<=0xD7FF)
				|| (cp>=0xFFA0 && cp<=0xFFDC))
			{
				return SheetScriptGroup.Hangul;
			}
			if((cp>=0x1200 && cp<=0x139F) || (cp>=0x2D80 && cp<=0x2DDF) || (cp>=0xAB00 && cp<=0xAB2F)) {
				return SheetScriptGroup.Ethiopic;
			}
			if((cp>=0x13A0 && cp<=0x167F) || (cp>=0x18B0 && cp<=0x18FF) || (cp>=0xAB70 && cp<=0xABBF)) {
				return SheetScriptGroup.CherokeeAndSyllabics;
			}
			if((cp>=0x1780 && cp<=0x17FF) || (cp>=0x19E0 && cp<=0x19FF)) {
				return SheetScriptGroup.Khmer;
			}
			if(cp>=0x1800 && cp<=0x18AF) {
				return SheetScriptGroup.Mongolian;
			}
			if((cp>=0x3040 && cp<=0x30FF) || (cp>=0x31F0 && cp<=0x31FF) || (cp>=0xFF65 && cp<=0xFF9F)) {
				return SheetScriptGroup.Kana;
			}
			if((cp>=0x2E80 && cp<=0x2FFF) || (cp>=0x3000 && cp<=0x303F) || (cp>=0x3100 && cp<=0x312F) || (cp>=0x3190 && cp<=0x31EF)
				|| (cp>=0x3200 && cp<=0x4DBF) || (cp>=0x4E00 && cp<=0x9FFF) || (cp>=0xF900 && cp<=0xFAFF) || (cp>=0xFE30 && cp<=0xFE4F)
				|| (cp>=0xFF00 && cp<=0xFFEF))
			{
				return SheetScriptGroup.Han;
			}
			if(cp>=0xA000 && cp<=0xA4CF) {
				return SheetScriptGroup.Yi;
			}
			if(cp>=0x2000 && cp<=0x2BFF) {
				return SheetScriptGroup.Symbols;//punctuation, currency, letterlike, arrows, math, box drawing, shapes, dingbats
			}
			return SheetScriptGroup.Other;
		}

		///<summary>The name of a patient language for the Sheet Def editor's language list. A three-letter ISO 639-2 code shows the culture's
		///display name, followed by its own name in its own script when that is not Latin, for example "Russian (русский)" or "Chinese (中文)",
		///so non-Latin languages are easy to find. Codes that are not a known culture (custom languages typed into Languages Used by Patients) show
		///as typed.</summary>
		public static string GetLanguageDisplayName(string threeLetters) {
			CultureInfo cultureInfo=MiscUtils.GetCultureFromThreeLetter(threeLetters);
			if(cultureInfo==null) {
				return threeLetters;
			}
			string display=cultureInfo.DisplayName;
			string native=cultureInfo.NativeName;
			if(string.IsNullOrWhiteSpace(native) || IsLatinText(native) || native==display) {
				return display;
			}
			return display+" ("+native+")";
		}
	}

	///<summary>Groups of writing systems that share a list of fallback fonts. Not stored in the database.</summary>
	public enum SheetScriptGroup {
		Latin,
		LatinExtended,
		Greek,
		Cyrillic,
		Armenian,
		Georgian,
		Hebrew,
		Arabic,
		Syriac,
		Thaana,
		Indic,
		Thai,
		Lao,
		Khmer,
		Tibetan,
		Myanmar,
		Mongolian,
		Ethiopic,
		CherokeeAndSyllabics,
		Hangul,
		Kana,
		Han,
		Yi,
		Symbols,
		Other
	}
}
