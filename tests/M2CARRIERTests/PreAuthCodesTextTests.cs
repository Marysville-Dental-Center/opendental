// carrier.PreAuthCodes as typed in Edit Carrier: display with non-breaking hyphens, the save checks, and the Carrier Combine merge. No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2CARRIERTests {
	[TestClass]
	public class PreAuthCodesTextTests {
		///<summary>Synthetic code list for the "does this code exist" lookups.</summary>
		private static readonly HashSet<string> _hashSetCodesKnown=new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "D0120","D0140","D2740","D2950" };

		[TestInitialize]
		public void TestInitialize() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");//Lans.g returns the text without a database.
		}

		private static PreAuthCodesCheck Check(string text) {
			return CarrierPreAuths.Check(text,x => _hashSetCodesKnown.Contains(x));
		}

		[TestMethod]
		public void Display_RangesUseNonBreakingHyphens() {
			Assert.AreEqual("D0100‑D0999,D2740",CarrierPreAuths.ToDisplay("D0100-D0999,D2740"));
			Assert.AreEqual("",CarrierPreAuths.ToDisplay(null));
			Assert.AreEqual('‑',CarrierPreAuths.NonBreakingHyphen);
		}

		[TestMethod]
		public void Display_ConvertsBackToPlainHyphens() {
			Assert.AreEqual("D0100-D0999,D2740",CarrierPreAuths.FromDisplay("D0100‑D0999,D2740"));
			//Dashes a paste can bring: en dash, em dash, minus sign, hyphen U+2010.
			Assert.AreEqual("D1-D2,D3-D4,D5-D6,D7-D8",CarrierPreAuths.FromDisplay("D1–D2,D3—D4,D5−D6,D7‐D8"));
			string stored="D0100-D0999,D2140-D2161,D2750";
			Assert.AreEqual(stored,CarrierPreAuths.FromDisplay(CarrierPreAuths.ToDisplay(stored)));
		}

		[TestMethod]
		public void Check_EmptyListIsValid() {
			Assert.IsTrue(Check("").IsValid);
			Assert.AreEqual("",Check("   ").Codes);
			Assert.AreEqual("",Check(null).Codes);
		}

		[TestMethod]
		public void Check_RefusesSpaces() {
			PreAuthCodesCheck preAuthCodesCheck=Check("D0120, D0140");
			Assert.IsFalse(preAuthCodesCheck.IsValid);
			StringAssert.Contains(preAuthCodesCheck.Error,"Spaces");
			Assert.IsFalse(Check("D0120,D01\t40").IsValid);
			//Whitespace around the whole text is not an entry: it is ignored.
			Assert.IsTrue(Check("  D0120,D0140\r\n").IsValid);
		}

		[TestMethod]
		public void Check_RefusesEmptyEntries() {
			foreach(string text in new[] { ",D0120","D0120,","D0120,,D0140","," }) {
				PreAuthCodesCheck preAuthCodesCheck=Check(text);
				Assert.IsFalse(preAuthCodesCheck.IsValid,text);
				StringAssert.Contains(preAuthCodesCheck.Error,"empty entry",text);
			}
		}

		[TestMethod]
		public void Check_RefusesRangesThatRunBackwards() {
			PreAuthCodesCheck preAuthCodesCheck=Check("D0120,D2999-D2000");
			Assert.IsFalse(preAuthCodesCheck.IsValid);
			StringAssert.Contains(preAuthCodesCheck.Error,"D2999‑D2000");
			Assert.IsTrue(Check("D2000-D2000").IsValid,"A range of one code is fine.");
		}

		[TestMethod]
		public void Check_StoresPlainHyphensAndDropsRepeats() {
			PreAuthCodesCheck preAuthCodesCheck=Check("D0100‑D0999,D2740,d2740,D0100-D0999,D2950");
			Assert.IsTrue(preAuthCodesCheck.IsValid,preAuthCodesCheck.Error);
			Assert.AreEqual("D0100-D0999,D2740,D2950",preAuthCodesCheck.Codes);
		}

		[TestMethod]
		public void Check_WarnsButAllowsUnknownCodes() {
			PreAuthCodesCheck preAuthCodesCheck=Check("D0120,X9999,D7000-D7999,X9999");
			Assert.IsTrue(preAuthCodesCheck.IsValid);
			CollectionAssert.AreEqual(new[] { "X9999" },preAuthCodesCheck.ListUnknownCodes,"Ranges are not looked up; an unknown code is listed once.");
			Assert.AreEqual("D0120,X9999,D7000-D7999",preAuthCodesCheck.Codes);
		}

		[TestMethod]
		public void Check_CodeWithDashThatIsNotARangeIsOneCode() {
			//Ends of different lengths: not a range. It is kept as a single code, and warned about when it does not exist.
			PreAuthCodesCheck preAuthCodesCheck=Check("D1110-2");
			Assert.IsTrue(preAuthCodesCheck.IsValid);
			CollectionAssert.AreEqual(new[] { "D1110-2" },preAuthCodesCheck.ListUnknownCodes);
		}

		[TestMethod]
		public void Merge_KeepsTheKeptListFirstThenAddsNewEntries() {
			Assert.AreEqual("D0100-D0999,D2740,D2950,D3310",
				CarrierPreAuths.Merge("D0100-D0999,D2740",new List<string> { "D2740,D2950","","d0100-d0999,D3310" }));
			Assert.AreEqual("D2950",CarrierPreAuths.Merge("",new List<string> { "D2950" }));
			Assert.AreEqual("D2950",CarrierPreAuths.Merge("D2950",null));
			Assert.AreEqual("",CarrierPreAuths.Merge(null,new List<string> { null,"" }));
		}
	}
}
