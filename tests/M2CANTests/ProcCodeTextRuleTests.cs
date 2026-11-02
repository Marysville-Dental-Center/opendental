// v26_1-lists-proccode-no-hyphens: procedure codes may not contain commas or hyphens, and are at most 15 characters. Enforced on manual add
// (Procedure Codes, New), on the XML/N-code import (FormProcCodes.ImportProcCodes) and on the Other Procedure Codes import; existing codes are
// listed by Database Maintenance, never changed.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2CANTests {
	[TestClass]
	public class ProcCodeTextRuleTests {
		[TestMethod]
		public void Allowed_PlainCodes() {
			using(new CultureScope("en-US")) {
				foreach(string code in new[] { "D0120","N4101","11101","99111","Z","T1234","ABCDEFGHIJKLMNO","01234R","D 1" }) {
					Assert.AreEqual("",ProcedureCodes.GetProcCodeTextError(code),code);
					Assert.IsTrue(ProcedureCodes.IsProcCodeTextAllowed(code),code);
				}
			}
		}

		[TestMethod]
		public void Refused_CommasHyphensLengthAndBlank() {
			using(new CultureScope("en-US")) {
				Dictionary<string,string> dictExpected=new Dictionary<string,string> {
					{ "D01-20","commas or hyphens" },
					{ "D0120,","commas or hyphens" },
					{ ",","commas or hyphens" },
					{ "-","commas or hyphens" },
					{ "D01‑20","commas or hyphens" },//Non-breaking hyphen, named by the 26.1 document.
					{ "ABCDEFGHIJKLMNOP","longer than 15" },
					{ "","blank" },
				};
				foreach(KeyValuePair<string,string> pair in dictExpected) {
					string error=ProcedureCodes.GetProcCodeTextError(pair.Key);
					StringAssert.Contains(error,pair.Value,"Code '"+pair.Key+"'");
					Assert.IsFalse(ProcedureCodes.IsProcCodeTextAllowed(pair.Key));
				}
				Assert.IsFalse(ProcedureCodes.IsProcCodeTextAllowed(null));
			}
		}

		[TestMethod]
		public void TheSeparatorsAreTheOnesCodeListsUse() {
			CollectionAssert.AreEquivalent(new[] { ',','-','‑' },ProcedureCodes.ArrayProcCodeCharsNotAllowed);
			Assert.AreEqual(15,ProcedureCodes.PROC_CODE_MAX_LENGTH,"procedurecode.ProcCode is varchar(15).");
		}

		[TestMethod]
		public void GetCodesWithAllowedText_KeepsOrderAndReportsEachRefusal() {
			using(new CultureScope("en-US")) {
				List<ProcedureCode> listProcedureCodes=new List<ProcedureCode> {
					new ProcedureCode { ProcCode="N4101" },
					new ProcedureCode { ProcCode="X-1" },
					new ProcedureCode { ProcCode="N4102" },
					new ProcedureCode { ProcCode="A,B" },
				};
				List<ProcedureCode> listAllowed=ProcedureCodes.GetCodesWithAllowedText(listProcedureCodes,out List<string> listErrors);
				CollectionAssert.AreEqual(new[] { "N4101","N4102" },listAllowed.Select(x => x.ProcCode).ToArray());
				Assert.AreEqual(2,listErrors.Count);
				StringAssert.Contains(listErrors[0],"X-1");
				StringAssert.Contains(listErrors[1],"A,B");
				Assert.AreEqual(0,ProcedureCodes.GetCodesWithAllowedText(null,out listErrors).Count);
			}
		}

		[TestMethod]
		public void GetCodesWithSeparators_FindsOnlyTheSeparators() {
			List<ProcedureCode> listProcedureCodes=new List<ProcedureCode> {
				new ProcedureCode { ProcCode="D0120" },
				new ProcedureCode { ProcCode="D01-20" },
				new ProcedureCode { ProcCode="D0,1" },
				new ProcedureCode { ProcCode="D‑1" },
				new ProcedureCode { ProcCode="D‐1" },//U+2010 HYPHEN is not named by the document; not refused.
			};
			CollectionAssert.AreEqual(new[] { "D01-20","D0,1","D‑1" },
				ProcedureCodes.GetCodesWithSeparators(listProcedureCodes).Select(x => x.ProcCode).ToArray());
		}

		[TestMethod]
		public void NoFeeCodesThatShipWithTheProgram_FollowTheRule() {
			string text=TestPaths.ReadRepoFile("OpenPmsDental","Resources","NoFeeProcCodes.txt");
			List<string> listCodes=System.Text.RegularExpressions.Regex.Matches(text,"<ProcCode>([^<]*)</ProcCode>").Cast<System.Text.RegularExpressions.Match>()
				.Select(x => x.Groups[1].Value).ToList();
			Assert.IsTrue(listCodes.Count>0);
			foreach(string code in listCodes) {
				Assert.IsTrue(ProcedureCodes.IsProcCodeTextAllowed(code),"N Codes must import under the 26.1 rule: "+code);
			}
		}

		[TestMethod]
		public void Dbm_CheckIsListedInTheWindow() {
			System.Reflection.MethodInfo methodInfo=typeof(DatabaseMaintenances).GetMethod("ProcedurecodeCodeHasSeparator");
			Assert.IsNotNull(methodInfo);
			DbmMethodAttr dbmMethodAttr=(DbmMethodAttr)methodInfo.GetCustomAttributes(typeof(DbmMethodAttr),false).Single();
			Assert.IsTrue(dbmMethodAttr.HasBreakDown);
			Assert.IsFalse(dbmMethodAttr.IsCanada,"The rule is for every office.");
		}

		[TestMethod]
		public void EveryImportPath_ChecksTheRule() {
			string codeNew=TestPaths.ReadRepoFile("OpenPmsDental","Forms","FormProcCodeNew.cs");
			StringAssert.Contains(codeNew,"ProcedureCodes.IsProcCodeTextAllowed(textNewCode.Text)");
			//The check comes before the code is inserted.
			Assert.IsTrue(codeNew.IndexOf("ProcedureCodes.IsProcCodeTextAllowed(textNewCode.Text)")<codeNew.IndexOf("ProcedureCodes.Insert(procedureCode)"));
			string codes2=TestPaths.ReadRepoFile("OpenPmsDental","Forms","FormProcCodes2.cs");
			int indexImport=codes2.IndexOf("public static int ImportProcCodes(");
			int indexFilter=codes2.IndexOf("ProcedureCodes.GetCodesWithAllowedText(listProcedureCodes,out List<string> listErrorsCodeText)");
			int indexInsert=codes2.IndexOf("ProcedureCodes.Insert(listProcedureCodes[i])");
			Assert.IsTrue(indexImport>0 && indexFilter>indexImport && indexInsert>indexFilter,"ImportProcCodes filters the codes before inserting them.");
			//Edit Procedure Code can't change the code text (read-only box), so it needs no check.
			string designerEdit=TestPaths.ReadRepoFile("OpenPmsDental","Forms","FormProcCodeEdit.designer.cs");
			StringAssert.Contains(designerEdit,"this.textProcCode.ReadOnly = true;");
			string other=TestPaths.ReadRepoFile("OpenPmsDentalBusiness","Data Interface","ProcCodesOther.cs");
			StringAssert.Contains(other,"ProcedureCodes.GetProcCodeTextError(code)");
		}
	}
}
