// Integration of M2-CAN's 26.1 procedure code rule (no comma or hyphen) with B-PROC's D Codes and CDA Codes imports (ProcCodeImports): a row
// whose code breaks the rule is refused and listed before anything is saved, like the other code imports. Synthetic codes only; no database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace M2CANTests {
	[TestClass]
	public class ProcCodeImportsRuleTests {
		[TestInitialize]
		public void TestInitialize() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
		}

		[TestMethod]
		public void Parse_RefusesCodesWithCommasOrHyphens_AndListsThem() {
			string text="ProcCode\tDescript\n"
				+"D9931\tSynthetic allowed\n"
				+"D99-32\tSynthetic hyphen-minus\n"
				+"\"D99,33\"\tSynthetic comma\n"
				+"D99‑34\tSynthetic non-breaking hyphen\n"
				+"99135\tSynthetic allowed CDA-style\n";
			List<ProcCodeImportRow> listRows=ProcCodeImports.Parse(text,out List<string> listErrors);
			CollectionAssert.AreEqual(new[] { "D9931","99135" },listRows.Select(x => x.ProcCode).ToArray());
			Assert.AreEqual(3,listErrors.Count,string.Join("; ",listErrors));
			Assert.IsTrue(listErrors.All(x => x.Contains(ProcedureCodes.GetProcCodeTextError("D99-32").Split(':')[0])),string.Join("; ",listErrors));
			Assert.IsTrue(listErrors[0].StartsWith("Line 3:"),listErrors[0]);
		}

		[TestMethod]
		public void Parse_Xml_RefusesCodesWithHyphens() {
			string xml="<?xml version=\"1.0\" encoding=\"utf-16\"?><ArrayOfProcedureCode xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" "
				+"xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">"
				+"<ProcedureCode><ProcCode>D9941</ProcCode><Descript>Synthetic xml allowed</Descript></ProcedureCode>"
				+"<ProcedureCode><ProcCode>D99-42</ProcCode><Descript>Synthetic xml hyphen</Descript></ProcedureCode>"
				+"</ArrayOfProcedureCode>";
			List<ProcCodeImportRow> listRows=ProcCodeImports.Parse(xml,out List<string> listErrors);
			CollectionAssert.AreEqual(new[] { "D9941" },listRows.Select(x => x.ProcCode).ToArray());
			Assert.AreEqual(1,listErrors.Count,string.Join("; ",listErrors));
			StringAssert.Contains(listErrors[0],"D99-42");
		}
	}
}
