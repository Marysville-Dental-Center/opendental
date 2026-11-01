// The character rules of the B-DBM text tools (OpenDentBusiness/Misc/DbmTextCleaner.cs) and the base64 note format. No database needed.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace BDBMTests {
	[TestClass]
	public class TextCleanerTests {
		[TestMethod]
		public void ControlChars_TabCrLfStay_OtherC0Removed() {
			for(int c=0;c<0x20;c++) {
				bool isKept=c==9 || c==10 || c==13;
				Assert.AreEqual(!isKept,DbmTextCleaner.IsControlCharToRemove((char)c),"U+"+c.ToString("X4"));
			}
			Assert.IsFalse(DbmTextCleaner.IsControlCharToRemove(' '));
			Assert.IsFalse(DbmTextCleaner.IsControlCharToRemove('\u007F'),"DEL is not in the published set.");
			Assert.AreEqual("a\tb\r\nc",DbmTextCleaner.RemoveEmailBodyInvalidChars("a\u0000\t\u0001b\r\n\u001Fc\u000B\u000C"));
			Assert.AreEqual(5,DbmTextCleaner.CountEmailBodyInvalidChars("a\u0000\t\u0001b\r\n\u001Fc\u000B\u000C"));
		}

		[TestMethod]
		public void Surrogates_PairsStay_UnpairedRemoved() {
			string emoji="\uD83D\uDE00";//A valid pair.
			Assert.AreEqual("x"+emoji+"y",DbmTextCleaner.RemoveEmailBodyInvalidChars("x"+emoji+"y"));
			Assert.AreEqual("xy",DbmTextCleaner.RemoveEmailBodyInvalidChars("x\uD83Dy"),"Lone high surrogate.");
			Assert.AreEqual("xy",DbmTextCleaner.RemoveEmailBodyInvalidChars("x\uDE00y"),"Lone low surrogate.");
			Assert.AreEqual("x",DbmTextCleaner.RemoveEmailBodyInvalidChars("x\uD83D"),"High surrogate at the end.");
			Assert.AreEqual(emoji,DbmTextCleaner.RemoveEmailBodyInvalidChars("\uDE00"+emoji+"\uD83D"));
		}

		[TestMethod]
		public void Gdi_AlsoRemovesFffeAndFfff_EmailRuleKeepsThem() {
			string text="a\uFFFEb\uFFFFc\uFFFDd\u0002e";
			Assert.AreEqual("abc\uFFFDde",DbmTextCleaner.RemoveGdiInvalidChars(text),"U+FFFD (replacement character) can be drawn and stays.");
			Assert.AreEqual(3,DbmTextCleaner.CountGdiInvalidChars(text));
			Assert.AreEqual("a\uFFFEb\uFFFFc\uFFFDde",DbmTextCleaner.RemoveEmailBodyInvalidChars(text));
			Assert.AreEqual("",DbmTextCleaner.RemoveGdiInvalidChars(null));
			Assert.AreEqual(0,DbmTextCleaner.CountGdiInvalidChars(null));
			Assert.AreEqual("Plain text, accents \u00E9\u00FC and \u20AC.",DbmTextCleaner.RemoveGdiInvalidChars("Plain text, accents \u00E9\u00FC and \u20AC."));
		}

		[TestMethod]
		public void UnicodeWhiteSpace_SpacesBecomeAsciiAndZeroWidthGoes_LineBreaksStay() {
			string text="A\u00A0B\u2000C\u2005D\u200AE\u3000F\u202FG\u205FH\u1680I";
			Assert.AreEqual("A B C D E F G H I",DbmTextCleaner.NormalizeUnicodeWhiteSpace(text));
			Assert.AreEqual("ZeroWidth",DbmTextCleaner.NormalizeUnicodeWhiteSpace("\uFEFFZero\u200BWi\u200Cd\u200Dth"));
			Assert.AreEqual("line\r\nbreak\nand\u2028separator\u2029\ttab",DbmTextCleaner.NormalizeUnicodeWhiteSpace("line\r\nbreak\nand\u2028separator\u2029\ttab"));
			Assert.AreEqual("",DbmTextCleaner.NormalizeUnicodeWhiteSpace(null));
			string plain="Nothing to change.";
			Assert.AreSame(plain,DbmTextCleaner.NormalizeUnicodeWhiteSpace(plain),"Unchanged text is returned as is.");
		}

		[TestMethod]
		public void Show_MarksEachBadCharAndCutsLongText() {
			Assert.AreEqual("ab[U+0001]c[U+FFFF] d",DbmTextCleaner.ShowGdiInvalidChars("ab\u0001c\uFFFF\r\nd".Replace("\r\n","\n")));
			string longText=new string('x',100)+"\u0007"+new string('y',200);
			string shown=DbmTextCleaner.ShowGdiInvalidChars(longText,contextBefore:10,maxChars:30);
			Assert.AreEqual("..."+new string('x',10)+"[U+0007]"+new string('y',19)+"...",shown);
			Assert.AreEqual("[U+D83D]x",DbmTextCleaner.ShowGdiInvalidChars("\uD83Dx"));
			Assert.AreEqual("",DbmTextCleaner.ShowGdiInvalidChars(""));
		}

		[TestMethod]
		public void Sql_MatchesStoredBytes() {
			string sql=DbmTextCleaner.GetSqlHasGdiInvalidChars("t.Col");
			Assert.AreEqual(@"CAST(t.Col AS BINARY) REGEXP '[\\x00-\\x08\\x0B\\x0C\\x0E-\\x1F]|\\xEF\\xBF[\\xBE\\xBF]'",sql);
			Assert.AreEqual(@"CAST(BodyText AS BINARY) REGEXP '[\\x00-\\x08\\x0B\\x0C\\x0E-\\x1F]'",DbmTextCleaner.GetSqlHasControlChars("BodyText"));
		}

		[TestMethod]
		public void Base64Note_ParsesExtensionAndBytes() {
			byte[] arrayBytes=Encoding.UTF8.GetBytes("synthetic file");
			string base64=Convert.ToBase64String(arrayBytes);
			Assert.IsTrue(DatabaseMaintenances.TryParseBase64DocumentNote("_rawBase64_:.TXT_"+base64,out string ext,out byte[] arrayRead,out string error),error);
			Assert.AreEqual(".txt",ext);
			CollectionAssert.AreEqual(arrayBytes,arrayRead);
			Assert.IsTrue(DatabaseMaintenances.TryParseBase64DocumentNote("_rawBase64_:pdf_"+base64.Substring(0,8)+"\r\n"+base64.Substring(8),out ext,out arrayRead,out error),
				"No dot and a line break in the data: "+error);
			Assert.AreEqual(".pdf",ext);
			CollectionAssert.AreEqual(arrayBytes,arrayRead);
		}

		[TestMethod]
		public void Base64Note_BadNotesAreRejectedWithAReason() {
			string[] arrayBad=new string[] {
				null,
				"",
				"_download_:https://example.test/file.jpg",
				"_RAWBASE64_:.jpg_AAAA",//The prefix is case-sensitive, as in the Images module.
				"_rawBase64_:AAAA",//No extension.
				"_rawBase64_:.j/pg_AAAA",
				"_rawBase64_:.jpg_not*base64",
				"_rawBase64_:.jpg_",
			};
			for(int i=0;i<arrayBad.Length;i++) {
				Assert.IsFalse(DatabaseMaintenances.TryParseBase64DocumentNote(arrayBad[i],out string ext,out byte[] arrayBytes,out string error),"Case "+i);
				Assert.IsNull(arrayBytes,"Case "+i);
				Assert.AreNotEqual("",error,"Case "+i);
			}
		}
	}
}
