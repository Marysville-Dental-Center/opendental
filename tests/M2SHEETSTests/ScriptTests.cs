// SheetScripts: which characters keep the old PDF encoding, which read right-to-left or need shaping, script groups and language names.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2SHEETSTests {
	[TestClass]
	public class ScriptTests {
		[TestInitialize]
		public void TestInitialize() {
			Samples.UseUsEnglish();
		}

		[TestMethod]
		public void WinAnsi_LatinAndWindows1252Only() {
			Assert.IsTrue(SheetScripts.IsWinAnsiEncodable(""));
			Assert.IsTrue(SheetScripts.IsWinAnsiEncodable("Hello, World 123\r\n\t"));
			Assert.IsTrue(SheetScripts.IsWinAnsiEncodable(Samples.LatinWinAnsi));
			Assert.IsFalse(SheetScripts.IsWinAnsiEncodable("Ā"),"Latin Extended-A is not in Windows-1252.");
			Assert.IsFalse(SheetScripts.IsWinAnsiEncodable("Name "+Samples.Russian));
			Assert.IsFalse(SheetScripts.IsWinAnsiEncodable(Samples.Chinese));
		}

		[TestMethod]
		public void LatinText_NeverNeedsFallback() {
			Assert.IsTrue(SheetScripts.IsLatinText(Samples.LatinWinAnsi));
			Assert.IsTrue(SheetScripts.IsLatinText("ĀŐǿ"),"Latin Extended-A and -B.");
			Assert.IsTrue(SheetScripts.IsLatinText("abc‎‍ "),"Direction marks, joiners and spaces draw nothing.");
			Assert.IsFalse(SheetScripts.IsLatinText("abc "+Samples.Greek));
			Assert.IsFalse(SheetScripts.IsLatinText(Samples.Korean));
		}

		[TestMethod]
		public void RightToLeft_FirstStrongLetterDecides() {
			Assert.IsTrue(SheetScripts.IsRightToLeft(Samples.Hebrew));
			Assert.IsTrue(SheetScripts.IsRightToLeft(Samples.Arabic));
			Assert.IsTrue(SheetScripts.IsRightToLeft("123. "+Samples.Hebrew),"Digits and punctuation are weak.");
			Assert.IsTrue(SheetScripts.IsRightToLeft(Samples.Hebrew+" Name"));
			Assert.IsTrue(SheetScripts.IsRightToLeft("‏123"),"A right-to-left mark is strong.");
			Assert.IsFalse(SheetScripts.IsRightToLeft("Name: "+Samples.Hebrew));
			Assert.IsFalse(SheetScripts.IsRightToLeft(Samples.Russian));
			Assert.IsFalse(SheetScripts.IsRightToLeft(Samples.Chinese));
			Assert.IsFalse(SheetScripts.IsRightToLeft("123"));
			Assert.IsFalse(SheetScripts.IsRightToLeft(""));
			Assert.IsFalse(SheetScripts.IsRightToLeft(null));
		}

		[TestMethod]
		public void HasRightToLeft_AnywhereInTheText() {
			Assert.IsTrue(SheetScripts.HasRightToLeft("Name: "+Samples.Hebrew));
			Assert.IsTrue(SheetScripts.HasRightToLeft("١٢"),"Arabic-Indic digits.");
			Assert.IsFalse(SheetScripts.HasRightToLeft(Samples.Russian+Samples.Chinese));
		}

		[TestMethod]
		public void NeedsShaping_RightToLeftAndComplexScripts() {
			foreach(string text in new[] { Samples.Hindi,Samples.Thai,Samples.Arabic,Samples.Hebrew,"가","កេ" }) {
				Assert.IsTrue(SheetScripts.NeedsShaping("x "+text),text);
			}
			foreach(string text in new[] { Samples.Russian,Samples.Greek,Samples.Chinese,Samples.Japanese,Samples.Korean,Samples.Georgian,Samples.LatinWinAnsi }) {
				Assert.IsFalse(SheetScripts.NeedsShaping(text),text);
			}
		}

		[TestMethod]
		public void ScriptGroups() {
			Assert.AreEqual(SheetScriptGroup.Latin,SheetScripts.GetScriptGroup('e'));
			Assert.AreEqual(SheetScriptGroup.LatinExtended,SheetScripts.GetScriptGroup('Ạ'));
			Assert.AreEqual(SheetScriptGroup.Cyrillic,SheetScripts.GetScriptGroup(Samples.Russian[0]));
			Assert.AreEqual(SheetScriptGroup.Greek,SheetScripts.GetScriptGroup(Samples.Greek[0]));
			Assert.AreEqual(SheetScriptGroup.Han,SheetScripts.GetScriptGroup(Samples.Chinese[0]));
			Assert.AreEqual(SheetScriptGroup.Kana,SheetScripts.GetScriptGroup(Samples.Japanese[0]));
			Assert.AreEqual(SheetScriptGroup.Hangul,SheetScripts.GetScriptGroup(Samples.Korean[0]));
			Assert.AreEqual(SheetScriptGroup.Hebrew,SheetScripts.GetScriptGroup(Samples.Hebrew[0]));
			Assert.AreEqual(SheetScriptGroup.Arabic,SheetScripts.GetScriptGroup(Samples.Arabic[0]));
			Assert.AreEqual(SheetScriptGroup.Indic,SheetScripts.GetScriptGroup(Samples.Hindi[0]));
			Assert.AreEqual(SheetScriptGroup.Thai,SheetScripts.GetScriptGroup(Samples.Thai[0]));
			Assert.AreEqual(SheetScriptGroup.Georgian,SheetScripts.GetScriptGroup(Samples.Georgian[0]));
			Assert.AreEqual(SheetScriptGroup.Symbols,SheetScripts.GetScriptGroup('→'));
			Assert.AreEqual(SheetScriptGroup.Han,SheetScripts.GetScriptGroup('！'),"Fullwidth forms go with Chinese.");
			Assert.AreEqual(SheetScriptGroup.Kana,SheetScripts.GetScriptGroup('ｶ'),"Halfwidth katakana.");
		}

		[TestMethod]
		public void LanguageNames_NonLatinShowTheirOwnScript() {
			string russian=SheetScripts.GetLanguageDisplayName("rus");
			StringAssert.StartsWith(russian,new CultureInfo("ru").DisplayName);
			StringAssert.Contains(russian,"(русский)");
			Assert.AreEqual(new CultureInfo("es").DisplayName,SheetScripts.GetLanguageDisplayName("spa"),"Latin native names are not repeated.");
			string chinese=SheetScripts.GetLanguageDisplayName("zho");
			StringAssert.Contains(chinese,"(");
			Assert.IsFalse(SheetScripts.IsLatinText(chinese));
			Assert.AreEqual("Farsi",SheetScripts.GetLanguageDisplayName("Farsi"),"Custom languages show as typed.");
			Assert.AreEqual("xqz",SheetScripts.GetLanguageDisplayName("xqz"),"Unknown codes show as typed.");
		}
	}
}
