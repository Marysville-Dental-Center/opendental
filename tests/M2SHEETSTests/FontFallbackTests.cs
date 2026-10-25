// SheetFonts: which font sheet text is drawn with when the field's font lacks glyphs. The rule is tested with made-up fonts; a few tests check
// the real Windows fonts of this computer and are inconclusive when a font is missing.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using PdfSharp.Drawing;

namespace M2SHEETSTests {
	[TestClass]
	public class FontFallbackTests {
		///<summary>Made-up fonts: name to the scripts it covers. Every font covers basic Latin.</summary>
		private static readonly Dictionary<string,SheetScriptGroup[]> _dictFakeFonts=new Dictionary<string,SheetScriptGroup[]>(StringComparer.OrdinalIgnoreCase) {
			{ "Arial",new[] { SheetScriptGroup.Cyrillic,SheetScriptGroup.Greek,SheetScriptGroup.Hebrew,SheetScriptGroup.Arabic } },
			{ "Calibri",new[] { SheetScriptGroup.Cyrillic,SheetScriptGroup.Greek } },
			{ "Courier New",new SheetScriptGroup[0] },
			{ "Segoe UI",new[] { SheetScriptGroup.Cyrillic,SheetScriptGroup.Greek,SheetScriptGroup.Hebrew,SheetScriptGroup.Arabic,SheetScriptGroup.Georgian } },
			{ "Microsoft YaHei",new[] { SheetScriptGroup.Han,SheetScriptGroup.Kana } },
			{ "Microsoft JhengHei",new[] { SheetScriptGroup.Han } },
			{ "Yu Gothic",new[] { SheetScriptGroup.Han,SheetScriptGroup.Kana } },
			{ "Malgun Gothic",new[] { SheetScriptGroup.Hangul } },
			{ "Nirmala UI",new[] { SheetScriptGroup.Indic } },
		};

		private static Func<string,string,bool[]> FakeHasGlyphs(HashSet<string> hashSetInstalled) {
			return (fontName,chars) => chars.Select(c => hashSetInstalled.Contains(fontName) && _dictFakeFonts.ContainsKey(fontName)
				&& (c<0x80 || _dictFakeFonts[fontName].Contains(SheetScripts.GetScriptGroup(c)))).ToArray();
		}

		private static string Resolve(string fontName,string text,params string[] arrayInstalled) {
			HashSet<string> hashSetInstalled=new HashSet<string>(arrayInstalled,StringComparer.OrdinalIgnoreCase);
			return SheetFonts.GetFontNameForText(fontName,text,FakeHasGlyphs(hashSetInstalled),x => hashSetInstalled.Contains(x));
		}

		private static readonly string[] _arrayAllFakeFonts=_dictFakeFonts.Keys.ToArray();

		[TestMethod]
		public void Fallback_LatinTextKeepsItsFont() {
			Assert.AreEqual("Courier New",Resolve("Courier New",Samples.LatinWinAnsi,_arrayAllFakeFonts));
			Assert.AreEqual("Wingdings",Resolve("Wingdings","abc",_arrayAllFakeFonts),"Symbol fonts with Latin text are never touched.");
			Assert.AreEqual("Not Installed",Resolve("Not Installed","abc Ā"),"Not even when the font is missing.");
		}

		[TestMethod]
		public void Fallback_FontThatCoversTheTextIsKept() {
			Assert.AreEqual("Arial",Resolve("Arial",Samples.Russian+" "+Samples.Hebrew,_arrayAllFakeFonts));
			Assert.AreEqual("Calibri",Resolve("Calibri","Name: "+Samples.Greek,_arrayAllFakeFonts));
		}

		[TestMethod]
		public void Fallback_ScriptFontsInOrder() {
			Assert.AreEqual("Microsoft YaHei",Resolve("Arial",Samples.Chinese,_arrayAllFakeFonts));
			Assert.AreEqual("Microsoft JhengHei",Resolve("Arial",Samples.Chinese,"Arial","Microsoft JhengHei","Yu Gothic"),"The next font when YaHei is missing.");
			Assert.AreEqual("Malgun Gothic",Resolve("Arial",Samples.Korean,_arrayAllFakeFonts));
			Assert.AreEqual("Nirmala UI",Resolve("Arial",Samples.Hindi,_arrayAllFakeFonts));
			Assert.AreEqual("Segoe UI",Resolve("Calibri",Samples.Hebrew,_arrayAllFakeFonts),"Calibri has no Hebrew.");
			Assert.AreEqual("Segoe UI",Resolve("Courier New",Samples.Georgian,_arrayAllFakeFonts));
		}

		[TestMethod]
		public void Fallback_KanaMeansJapanese() {
			//Chinese characters come first in the text, but the kana make it Japanese.
			Assert.AreEqual("Yu Gothic",Resolve("Arial",Samples.Chinese+Samples.Japanese,_arrayAllFakeFonts));
			List<string> listCandidates=SheetFonts.GetFallbackCandidates(Samples.Chinese+Samples.Korean);
			Assert.AreEqual("Malgun Gothic",listCandidates[0],"Hangul means Korean.");
		}

		[TestMethod]
		public void Fallback_MostCoverageWhenNoFontHasAll() {
			//No made-up font has both Cyrillic and Chinese: the one with more of the characters wins.
			Assert.AreEqual("Segoe UI",Resolve("Courier New",Samples.Russian+" "+Samples.Chinese,_arrayAllFakeFonts));
			//Nothing installed covers Hindi: the field keeps its font.
			Assert.AreEqual("Arial",Resolve("Arial",Samples.Hindi,"Arial","Segoe UI"));
		}

		[TestMethod]
		public void Fallback_IgnoresCharactersThatDrawNothing() {
			//Only the Chinese character needs a glyph; the space, the zero-width joiner and the direction mark do not.
			Assert.AreEqual("Microsoft YaHei",Resolve("Arial","你 ‍‎",_arrayAllFakeFonts));
			Assert.AreEqual("Arial",Resolve("Arial","‍‎",_arrayAllFakeFonts),"Format and private-use characters alone.");
		}

		[TestMethod]
		public void FallbackCandidates_NoDuplicatesAndGeneralFontsLast() {
			List<string> listCandidates=SheetFonts.GetFallbackCandidates(Samples.Russian+Samples.Greek+Samples.Hebrew);
			Assert.AreEqual("Segoe UI",listCandidates[0]);
			Assert.AreEqual(listCandidates.Count,listCandidates.Distinct(StringComparer.OrdinalIgnoreCase).Count());
			CollectionAssert.Contains(listCandidates,"Arial Unicode MS");
			CollectionAssert.Contains(SheetFonts.GetFallbackCandidates(Samples.Thai),"Leelawadee UI");
		}

		[TestMethod]
		public void NormalizeFontName_FontFamilyToString() {
			Assert.AreEqual("Arial",SheetFonts.NormalizeFontName("[FontFamily: Name=Arial]"));
			Assert.AreEqual("Times New Roman",SheetFonts.NormalizeFontName("Times New Roman"));
			Assert.IsNull(SheetFonts.NormalizeFontName(null));
		}

		#region Real fonts of this computer
		[TestMethod]
		public void RealFonts_InstalledAndGlyphs() {
			Samples.RequireFonts("Arial");
			Assert.IsTrue(SheetFonts.IsFontInstalled("arial"),"Case does not matter.");
			Assert.IsFalse(SheetFonts.IsFontInstalled("No Such Font M2SHEETS"));
			CollectionAssert.AreEqual(new[] { true,true,false },SheetFonts.FontHasGlyphs("Arial","AЖ你"));
			CollectionAssert.AreEqual(new[] { false },SheetFonts.FontHasGlyphs("No Such Font M2SHEETS","A"));
		}

		[TestMethod]
		public void RealFonts_ArialKeepsCyrillicAndGreek() {
			Samples.RequireFonts("Arial");
			Assert.AreEqual("Arial",SheetFonts.GetFontNameForText("Arial",Samples.Russian+" "+Samples.Greek));
		}

		[TestMethod]
		public void RealFonts_ChineseInArialUsesYaHei() {
			Samples.RequireFonts("Arial","Microsoft YaHei");
			Assert.AreEqual("Microsoft YaHei",SheetFonts.GetFontNameForText("Arial","Name: "+Samples.Chinese));
		}

		[TestMethod]
		public void RealFonts_KoreanInArialUsesMalgunGothic() {
			Samples.RequireFonts("Arial","Malgun Gothic");
			Assert.AreEqual("Malgun Gothic",SheetFonts.GetFontNameForText("Arial",Samples.Korean));
		}

		[TestMethod]
		public void RealFonts_HindiInArialUsesNirmalaUI() {
			Samples.RequireFonts("Arial","Nirmala UI");
			Assert.AreEqual("Nirmala UI",SheetFonts.GetFontNameForText("Arial",Samples.Hindi));
		}

		[TestMethod]
		public void RealFonts_HebrewInAFontWithoutHebrewUsesSegoeUI() {
			Samples.RequireFonts("Segoe UI");
			//The first common Windows font here that has no Hebrew letters.
			string fontName=new[] { "Calibri","Verdana","Georgia","Trebuchet MS" }
				.FirstOrDefault(x => SheetFonts.IsFontInstalled(x) && !SheetFonts.FontHasGlyphs(x,Samples.Hebrew).Any(y => y));
			if(fontName==null) {
				Assert.Inconclusive("No installed font without Hebrew to test with.");
			}
			Assert.AreEqual("Segoe UI",SheetFonts.GetFontNameForText(fontName,Samples.Hebrew),fontName);
		}

		[TestMethod]
		public void RealFonts_PdfEmbedding() {
			Samples.RequireFonts("Arial");
			Assert.IsTrue(SheetFonts.CanEmbedInPdf("Arial",XFontStyle.Regular));
			Assert.IsTrue(SheetFonts.CanEmbedInPdf("Arial",XFontStyle.Bold));
			Assert.IsFalse(SheetFonts.CanEmbedInPdf("",XFontStyle.Regular));
		}

		[TestMethod]
		public void RealFonts_FontCollectionsCannotBeEmbedded() {
			//Windows keeps Microsoft YaHei in a font collection (.ttc), which the PDF library cannot embed. Such text becomes an image in PDFs.
			Samples.RequireFonts("Microsoft YaHei");
			Assert.IsFalse(SheetFonts.CanEmbedInPdf("Microsoft YaHei",XFontStyle.Regular));
		}
		#endregion Real fonts of this computer
	}
}
