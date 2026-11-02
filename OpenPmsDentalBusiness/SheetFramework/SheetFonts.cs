// Font fallback for sheet text in non-Latin scripts (Open Dental 25.4: sheets support non-Latin languages; backlog package M2-SHEETS).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace OpenPmsDentalBusiness {
	///<summary>Picks the font sheet text is drawn with when the field's own font has no glyphs for it, for example Chinese typed into a field
	///that uses Arial. The same choice is made on screen, in print and in PDFs, so the three match. Latin text is never affected.</summary>
	public class SheetFonts {
		///<summary>Fallback fonts per writing system, best first. Only installed fonts are used. Segoe UI, Microsoft YaHei, Malgun Gothic and Nirmala
		///UI ship with Windows; the others are older or optional Windows fonts that some workstations have.</summary>
		private static readonly Dictionary<SheetScriptGroup,string[]> _dictFallbackFonts=new Dictionary<SheetScriptGroup,string[]> {
			{ SheetScriptGroup.Latin,new[] { "Segoe UI","Arial","Times New Roman","Microsoft Sans Serif" } },
			{ SheetScriptGroup.LatinExtended,new[] { "Segoe UI","Arial","Times New Roman","Lucida Sans Unicode","Microsoft Sans Serif" } },
			{ SheetScriptGroup.Greek,new[] { "Segoe UI","Arial","Times New Roman","Tahoma","Microsoft Sans Serif" } },
			{ SheetScriptGroup.Cyrillic,new[] { "Segoe UI","Arial","Times New Roman","Tahoma","Microsoft Sans Serif" } },
			{ SheetScriptGroup.Armenian,new[] { "Segoe UI","Sylfaen","Arial" } },
			{ SheetScriptGroup.Georgian,new[] { "Segoe UI","Sylfaen" } },
			{ SheetScriptGroup.Hebrew,new[] { "Segoe UI","Arial","Times New Roman","Tahoma","Microsoft Sans Serif","David","Miriam" } },
			{ SheetScriptGroup.Arabic,new[] { "Segoe UI","Arial","Times New Roman","Tahoma","Microsoft Sans Serif","Traditional Arabic" } },
			{ SheetScriptGroup.Syriac,new[] { "Estrangelo Edessa","Segoe UI Historic" } },
			{ SheetScriptGroup.Thaana,new[] { "MV Boli" } },
			{ SheetScriptGroup.Indic,new[] { "Nirmala UI","Nirmala Text","Mangal","Aparajita","Kokila","Utsaah","Vrinda","Raavi","Shruti","Kalinga","Latha",
				"Gautami","Tunga","Kartika","Iskoola Pota" } },
			{ SheetScriptGroup.Thai,new[] { "Leelawadee UI","Leelawadee","Tahoma","Microsoft Sans Serif" } },
			{ SheetScriptGroup.Lao,new[] { "Leelawadee UI","Lao UI","DokChampa" } },
			{ SheetScriptGroup.Khmer,new[] { "Leelawadee UI","Khmer UI","DaunPenh" } },
			{ SheetScriptGroup.Tibetan,new[] { "Microsoft Himalaya" } },
			{ SheetScriptGroup.Myanmar,new[] { "Myanmar Text" } },
			{ SheetScriptGroup.Mongolian,new[] { "Mongolian Baiti" } },
			{ SheetScriptGroup.Ethiopic,new[] { "Ebrima","Nyala" } },
			{ SheetScriptGroup.CherokeeAndSyllabics,new[] { "Gadugi","Plantagenet Cherokee","Euphemia" } },
			{ SheetScriptGroup.Hangul,new[] { "Malgun Gothic","Gulim","Dotum","Batang" } },
			{ SheetScriptGroup.Kana,new[] { "Yu Gothic","Meiryo","MS Gothic","MS PGothic","MS UI Gothic","Microsoft YaHei" } },
			{ SheetScriptGroup.Han,new[] { "Microsoft YaHei","Microsoft JhengHei","SimSun","NSimSun","MingLiU","PMingLiU","Yu Gothic","MS Gothic",
				"Malgun Gothic" } },
			{ SheetScriptGroup.Yi,new[] { "Microsoft Yi Baiti" } },
			{ SheetScriptGroup.Symbols,new[] { "Segoe UI Symbol","Segoe UI","Arial","Cambria Math","Lucida Sans Unicode" } },
			{ SheetScriptGroup.Other,new[] { "Segoe UI Historic","Ebrima","Gadugi","Sans Serif Collection" } },
		};
		///<summary>Tried after the fonts of the text's own scripts.</summary>
		private static readonly string[] _arrayFallbackFontsLast=new[] { "Segoe UI","Arial Unicode MS","Sans Serif Collection","Segoe UI Symbol",
			"Segoe UI Historic","Microsoft Sans Serif" };
		private static readonly Regex _regexFontFamilyToString=new Regex(@"^\[FontFamily: Name=(.*)\]$");
		private static readonly object _lockInstalled=new object();
		private static HashSet<string> _hashSetInstalled;
		///<summary>Glyph coverage per font (lower-case name) and character, filled as characters are looked up.</summary>
		private static readonly ConcurrentDictionary<string,ConcurrentDictionary<char,bool>> _dictCoverage=
			new ConcurrentDictionary<string,ConcurrentDictionary<char,bool>>();
		///<summary>Resolved font per requested font and text. Cleared when it grows large; sheets repaint the same fields many times.</summary>
		private static readonly ConcurrentDictionary<string,string> _dictResolved=new ConcurrentDictionary<string,string>();
		private const int MAX_RESOLVED_CACHED=4096;
		///<summary>Whether the PDF library can embed a font for Unicode text, per font name and style.</summary>
		private static readonly ConcurrentDictionary<string,bool> _dictCanEmbed=new ConcurrentDictionary<string,bool>();

		#region Fallback
		///<summary>The font to draw the text with: fontName itself when it has glyphs for every character of the text (always, for Latin text),
		///otherwise the first installed fallback font for the text's scripts that has them all (or, if none does, the one that has the most, when it
		///has more than fontName). Uses the installed fonts of this computer.</summary>
		public static string GetFontNameForText(string fontName,string text) {
			if(SheetScripts.IsLatinText(text)) {
				return fontName;
			}
			//The choice depends only on the distinct characters in the order they first appear, which is much shorter than a long text.
			string chars=GetDistinctDrawnChars(text);
			string key=(fontName??"")+"\u0001"+chars;
			if(_dictResolved.TryGetValue(key,out string fontNameResolved)) {
				return fontNameResolved;
			}
			fontNameResolved=GetFontNameForText(fontName,chars,FontHasGlyphs,IsFontInstalled);
			if(_dictResolved.Count>MAX_RESOLVED_CACHED) {
				_dictResolved.Clear();
			}
			_dictResolved[key]=fontNameResolved;
			return fontNameResolved;
		}

		///<summary>The fallback rule itself, with the font checks passed in. funcFontHasGlyphs(fontName,chars) returns, for each character, whether
		///the font has a glyph for it; funcIsInstalled(fontName) whether the font is installed. Used by the overload above and by the tests.</summary>
		public static string GetFontNameForText(string fontName,string text,Func<string,string,bool[]> funcFontHasGlyphs,Func<string,bool> funcIsInstalled) {
			if(SheetScripts.IsLatinText(text)) {
				return fontName;
			}
			string chars=GetDistinctDrawnChars(text);
			if(chars.Length==0) {
				return fontName;
			}
			int countRequested=0;
			if(!string.IsNullOrWhiteSpace(fontName) && funcIsInstalled(fontName)) {
				countRequested=CountCovered(funcFontHasGlyphs(fontName,chars));
				if(countRequested==chars.Length) {
					return fontName;
				}
			}
			string fontNameBest=fontName;
			int countBest=countRequested;
			foreach(string fontNameCandidate in GetFallbackCandidates(text)) {
				if(string.Equals(fontNameCandidate,fontName,StringComparison.OrdinalIgnoreCase) || !funcIsInstalled(fontNameCandidate)) {
					continue;
				}
				int count=CountCovered(funcFontHasGlyphs(fontNameCandidate,chars));
				if(count==chars.Length) {
					return fontNameCandidate;
				}
				if(count>countBest) {
					fontNameBest=fontNameCandidate;
					countBest=count;
				}
			}
			return fontNameBest;
		}

		///<summary>Fallback font names for the text, best first: Japanese fonts first when the text has kana, Korean fonts first when it has Hangul,
		///then the fonts of each other script in the order the scripts first appear, then general Unicode fonts. No duplicates.</summary>
		public static List<string> GetFallbackCandidates(string text) {
			List<SheetScriptGroup> listGroups=new List<SheetScriptGroup>();
			for(int i=0;i<(text??"").Length;i++) {
				if(SheetScripts.IsIgnorableForFonts(text[i])) {
					continue;
				}
				SheetScriptGroup sheetScriptGroup=SheetScripts.GetScriptGroup(text[i]);
				if(sheetScriptGroup==SheetScriptGroup.Latin || listGroups.Contains(sheetScriptGroup)) {
					continue;
				}
				listGroups.Add(sheetScriptGroup);
			}
			//Chinese characters are shared by Japanese and Korean; kana or Hangul tell which language the text is in.
			foreach(SheetScriptGroup sheetScriptGroupFirst in new[] { SheetScriptGroup.Hangul,SheetScriptGroup.Kana }) {
				if(listGroups.Remove(sheetScriptGroupFirst)) {
					listGroups.Insert(0,sheetScriptGroupFirst);
				}
			}
			List<string> listFontNames=new List<string>();
			foreach(SheetScriptGroup sheetScriptGroup in listGroups) {
				AddDistinct(listFontNames,_dictFallbackFonts[sheetScriptGroup]);
			}
			AddDistinct(listFontNames,_arrayFallbackFontsLast);
			return listFontNames;
		}

		///<summary>Some sheet code names a font with FontFamily.ToString(), which gives "[FontFamily: Name=Arial]". Returns the plain name.</summary>
		public static string NormalizeFontName(string fontName) {
			if(string.IsNullOrEmpty(fontName)) {
				return fontName;
			}
			Match match=_regexFontFamilyToString.Match(fontName);
			if(match.Success) {
				return match.Groups[1].Value;
			}
			return fontName;
		}

		private static void AddDistinct(List<string> listFontNames,IEnumerable<string> fontNames) {
			foreach(string fontName in fontNames) {
				if(!listFontNames.Any(x => string.Equals(x,fontName,StringComparison.OrdinalIgnoreCase))) {
					listFontNames.Add(fontName);
				}
			}
		}

		private static int CountCovered(bool[] arrayHasGlyph) {
			int count=0;
			for(int i=0;i<arrayHasGlyph.Length;i++) {
				if(arrayHasGlyph[i]) {
					count++;
				}
			}
			return count;
		}

		///<summary>The distinct characters of the text that need a glyph.</summary>
		private static string GetDistinctDrawnChars(string text) {
			HashSet<char> hashSet=new HashSet<char>();
			System.Text.StringBuilder stringBuilder=new System.Text.StringBuilder();
			for(int i=0;i<text.Length;i++) {
				if(SheetScripts.IsIgnorableForFonts(text[i]) || !hashSet.Add(text[i])) {
					continue;
				}
				stringBuilder.Append(text[i]);
			}
			return stringBuilder.ToString();
		}
		#endregion Fallback

		#region Installed fonts and glyphs
		///<summary>True when a font family of this name is installed on this computer (case does not matter).</summary>
		public static bool IsFontInstalled(string fontName) {
			if(string.IsNullOrWhiteSpace(fontName)) {
				return false;
			}
			lock(_lockInstalled) {
				if(_hashSetInstalled==null) {
					HashSet<string> hashSet=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
					try {
						using(InstalledFontCollection installedFontCollection=new InstalledFontCollection()) {
							foreach(FontFamily fontFamily in installedFontCollection.Families) {
								hashSet.Add(fontFamily.Name);
							}
						}
					}
					catch(Exception) {
						//No font list (should not happen on Windows): treat every font as missing, so text keeps its own font.
					}
					_hashSetInstalled=hashSet;
				}
				return _hashSetInstalled.Contains(fontName);
			}
		}

		///<summary>For each character of chars, whether the installed font has a glyph for it, as Windows reports it (GetGlyphIndices). Fonts
		///that are not installed have none. Results are cached per font and character.</summary>
		public static bool[] FontHasGlyphs(string fontName,string chars) {
			bool[] arrayHasGlyph=new bool[chars.Length];
			if(!IsFontInstalled(fontName)) {
				return arrayHasGlyph;
			}
			ConcurrentDictionary<char,bool> dictCoverage=_dictCoverage.GetOrAdd(fontName.ToLowerInvariant(),x => new ConcurrentDictionary<char,bool>());
			System.Text.StringBuilder stringBuilderUnknown=new System.Text.StringBuilder();
			for(int i=0;i<chars.Length;i++) {
				if(!dictCoverage.ContainsKey(chars[i]) && stringBuilderUnknown.ToString().IndexOf(chars[i])<0) {
					stringBuilderUnknown.Append(chars[i]);
				}
			}
			if(stringBuilderUnknown.Length>0) {
				string unknown=stringBuilderUnknown.ToString();
				bool[] arrayLookedUp=LookUpGlyphs(fontName,unknown);
				for(int i=0;i<unknown.Length;i++) {
					dictCoverage[unknown[i]]=arrayLookedUp[i];
				}
			}
			for(int i=0;i<chars.Length;i++) {
				dictCoverage.TryGetValue(chars[i],out arrayHasGlyph[i]);
			}
			return arrayHasGlyph;
		}

		private static bool[] LookUpGlyphs(string fontName,string chars) {
			bool[] arrayHasGlyph=new bool[chars.Length];
			Font font=null;
			IntPtr hFont=IntPtr.Zero;
			IntPtr hdc=IntPtr.Zero;
			IntPtr hFontOld=IntPtr.Zero;
			try {
				font=CreateFontAnyStyle(fontName,12f,GraphicsUnit.Pixel);
				if(font==null) {
					return arrayHasGlyph;
				}
				hFont=font.ToHfont();
				hdc=CreateCompatibleDC(IntPtr.Zero);
				hFontOld=SelectObject(hdc,hFont);
				ushort[] arrayGlyphIndices=new ushort[chars.Length];
				uint result=GetGlyphIndicesW(hdc,chars,chars.Length,arrayGlyphIndices,GGI_MARK_NONEXISTING_GLYPHS);
				if(result==GDI_ERROR) {
					return arrayHasGlyph;
				}
				for(int i=0;i<chars.Length;i++) {
					arrayHasGlyph[i]=(arrayGlyphIndices[i]!=0xFFFF);
				}
				return arrayHasGlyph;
			}
			catch(Exception) {
				return arrayHasGlyph;
			}
			finally {
				if(hdc!=IntPtr.Zero) {
					if(hFontOld!=IntPtr.Zero) {
						SelectObject(hdc,hFontOld);
					}
					DeleteDC(hdc);
				}
				if(hFont!=IntPtr.Zero) {
					DeleteObject(hFont);
				}
				font?.Dispose();
			}
		}

		///<summary>A GDI+ font of the family in the first style the family has (regular, bold, italic, bold italic), or null when the family is not
		///installed. Some families have no regular style.</summary>
		public static Font CreateFontAnyStyle(string fontName,float emSize,GraphicsUnit graphicsUnit) {
			FontFamily fontFamily;
			try {
				fontFamily=new FontFamily(fontName);
			}
			catch(Exception) {
				return null;
			}
			FontStyle? fontStyleAvailable=null;
			using(fontFamily) {
				foreach(FontStyle fontStyle in new[] { FontStyle.Regular,FontStyle.Bold,FontStyle.Italic,FontStyle.Bold|FontStyle.Italic }) {
					if(fontFamily.IsStyleAvailable(fontStyle)) {
						fontStyleAvailable=fontStyle;
						break;
					}
				}
			}
			if(fontStyleAvailable==null) {
				return null;
			}
			return new Font(fontName,emSize,fontStyleAvailable.Value,graphicsUnit);
		}

		private const uint GGI_MARK_NONEXISTING_GLYPHS=0x0001;
		private const uint GDI_ERROR=0xFFFFFFFF;

		[DllImport("gdi32.dll",CharSet=CharSet.Unicode,EntryPoint="GetGlyphIndicesW")]
		private static extern uint GetGlyphIndicesW(IntPtr hdc,string lpstr,int c,[Out] ushort[] pgi,uint fl);

		[DllImport("gdi32.dll")]
		private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

		[DllImport("gdi32.dll")]
		private static extern bool DeleteDC(IntPtr hdc);

		[DllImport("gdi32.dll")]
		private static extern IntPtr SelectObject(IntPtr hdc,IntPtr hgdiobj);

		[DllImport("gdi32.dll")]
		private static extern bool DeleteObject(IntPtr hObject);
		#endregion Installed fonts and glyphs

		#region PDF
		///<summary>Font options that make the PDF library embed the font and write text as Unicode, as ImageHelper and GridOD already do for
		///non-ASCII text. The font is embedded as a subset of the characters used, as PDFsharp 1.2 embedded it ("Always" means the whole font file
		///in PDFsharp 6).</summary>
		public static XPdfFontOptions GetPdfFontOptionsUnicode() {
			return new XPdfFontOptions(PdfFontEncoding.Unicode,PdfFontEmbedding.TryComputeSubset);
		}

		///<summary>True when the PDF library can embed the font for Unicode text. It cannot use fonts that Windows stores in font collections (.ttc
		///files, which hold most Chinese and Japanese fonts and, on recent Windows, Nirmala UI and Cambria); text in such a font is drawn into the PDF
		///by the print engine instead (see SheetTextDrawing). Checked once per font and style.</summary>
		public static bool CanEmbedInPdf(string fontName,XFontStyleEx xFontStyle) {
			if(string.IsNullOrWhiteSpace(fontName)) {
				return false;
			}
			return _dictCanEmbed.GetOrAdd(fontName.ToLowerInvariant()+"|"+(int)xFontStyle,x => {
				try {
					XFont xFont=new XFont(fontName,10,xFontStyle,GetPdfFontOptionsUnicode());
					return xFont!=null;
				}
				catch(Exception) {
					return false;
				}
			});
		}
		#endregion PDF
	}
}
