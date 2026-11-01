// Other Procedure Codes (v25_4-canada-other-proc-codes, -cdha, -dac, -ramq; v26_1-lists-canada-othercodes-password): reading the office's code
// file and working out the import, without a database. Synthetic codes and descriptions only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2CANTests {
	[TestClass]
	public class OtherCodesTests {
		private static List<OtherCodeRow> Parse(string text,out List<string> listErrors) {
			using(new CultureScope("en-US")) {
				return ProcCodesOther.Parse(text,out listErrors);
			}
		}

		private static OtherCodeImportPlan Plan(EnumOtherCodeSystem codeSystem,EnumOtherCodeUpdates updates,string text,
			List<ProcedureCode> listProcedureCodesExisting=null)
		{
			using(new CultureScope("en-US")) {
				List<OtherCodeRow> listRows=ProcCodesOther.Parse(text,out List<string> listErrors);
				return ProcCodesOther.CreatePlan(codeSystem,updates,listRows,listProcedureCodesExisting??new List<ProcedureCode>(),listErrors);
			}
		}

		#region Code systems
		[TestMethod]
		public void CodeSystems_AreTheFivePublicOnes() {
			CollectionAssert.AreEqual(new[] { "CDHA","DAC (English)","DAC (French)","RAMQ","CDA" },
				Enum.GetValues(typeof(EnumOtherCodeSystem)).Cast<EnumOtherCodeSystem>().Select(x => ProcCodesOther.GetCodeSystemName(x)).ToArray());
			//ACDQ is not offered: it needs member verification by Open Dental's staff.
			Assert.IsFalse(Enum.GetNames(typeof(EnumOtherCodeSystem)).Any(x => x.ToUpperInvariant().Contains("ACDQ")));
		}

		[TestMethod]
		public void Categories_AreNamedAfterTheSystem_DacSharesOne() {
			Assert.AreEqual("CDHA",ProcCodesOther.GetCategoryName(EnumOtherCodeSystem.Cdha));
			Assert.AreEqual("DAC",ProcCodesOther.GetCategoryName(EnumOtherCodeSystem.DacEnglish));
			Assert.AreEqual("DAC",ProcCodesOther.GetCategoryName(EnumOtherCodeSystem.DacFrench));
			Assert.AreEqual("RAMQ",ProcCodesOther.GetCategoryName(EnumOtherCodeSystem.Ramq));
			Assert.AreEqual("",ProcCodesOther.GetCategoryName(EnumOtherCodeSystem.Cda));
		}

		[TestMethod]
		public void Cda_OnlyWithAdvancedOptions() {
			using(new CultureScope("en-US")) {
				Assert.AreNotEqual("",ProcCodesOther.GetChoiceError(EnumOtherCodeSystem.Cda,EnumOtherCodeUpdates.None));
				Assert.AreEqual("",ProcCodesOther.GetChoiceError(EnumOtherCodeSystem.Cda,EnumOtherCodeUpdates.PaintType));
				foreach(EnumOtherCodeSystem codeSystem in new[] { EnumOtherCodeSystem.Cdha,EnumOtherCodeSystem.DacEnglish,EnumOtherCodeSystem.DacFrench,
					EnumOtherCodeSystem.Ramq })
				{
					Assert.AreEqual("",ProcCodesOther.GetChoiceError(codeSystem,EnumOtherCodeUpdates.None),codeSystem.ToString());
					Assert.IsTrue(ProcCodesOther.CanAddCodes(codeSystem));
				}
				Assert.IsFalse(ProcCodesOther.CanAddCodes(EnumOtherCodeSystem.Cda));
			}
		}

		[TestMethod]
		public void Ramq_StoredWithTrailingR() {
			Assert.AreEqual("01234R",ProcCodesOther.GetStoredCode(EnumOtherCodeSystem.Ramq,"01234"));
			Assert.AreEqual("01234R",ProcCodesOther.GetStoredCode(EnumOtherCodeSystem.Ramq," 01234 "));
			Assert.AreEqual("01234R",ProcCodesOther.GetStoredCode(EnumOtherCodeSystem.Ramq,"01234R"),"A list exported from this program already has it.");
			Assert.AreEqual("01234",ProcCodesOther.GetStoredCode(EnumOtherCodeSystem.Cdha,"01234"));
			Assert.AreEqual("01234",ProcCodesOther.GetStoredCode(EnumOtherCodeSystem.DacFrench,"01234"));
			Assert.AreEqual("01234",ProcCodesOther.GetStoredCode(EnumOtherCodeSystem.Cda,"01234"));
		}

		[TestMethod]
		public void Ramq_ClaimsLeaveOutTheR_OnlyForRamqCategoryCodes() {
			List<Def> listDefs=new List<Def> {
				new Def { DefNum=10,Category=DefCat.ProcCodeCats,ItemName="Diagnostic" },
				new Def { DefNum=11,Category=DefCat.ProcCodeCats,ItemName="RAMQ" },
				new Def { DefNum=12,Category=DefCat.ProcCodeCats,ItemName=" ramq " },
			};
			ProcedureCode procedureCodeRamq=new ProcedureCode { ProcCode="0123R",ProcCat=11 };
			Assert.IsTrue(ProcCodesOther.IsRamqCode(procedureCodeRamq,listDefs));
			Assert.AreEqual("0123",ProcCodesOther.GetCodeForClaim("0123R",procedureCodeRamq,listDefs));
			Assert.AreEqual("0123",ProcCodesOther.GetCodeForClaim("0123R",new ProcedureCode { ProcCode="0123R",ProcCat=12 },listDefs),"Any case, spaces.");
			//Not in the RAMQ category: unchanged, even with a trailing R.
			Assert.AreEqual("4567R",ProcCodesOther.GetCodeForClaim("4567R",new ProcedureCode { ProcCode="4567R",ProcCat=10 },listDefs));
			//A code sent that is not the procedure's own code (alternate code, typed by the user, or already cut to five characters): unchanged.
			Assert.AreEqual("ALT1R",ProcCodesOther.GetCodeForClaim("ALT1R",procedureCodeRamq,listDefs));
			Assert.AreEqual("12345",ProcCodesOther.GetCodeForClaim("12345",new ProcedureCode { ProcCode="12345R",ProcCat=11 },listDefs));
			//Edge cases.
			Assert.AreEqual("R",ProcCodesOther.GetCodeForClaim("R",new ProcedureCode { ProcCode="R",ProcCat=11 },listDefs));
			Assert.AreEqual("",ProcCodesOther.GetCodeForClaim("",procedureCodeRamq,listDefs));
			Assert.AreEqual("0123R",ProcCodesOther.GetCodeForClaim("0123R",null,listDefs));
			Assert.IsFalse(ProcCodesOther.IsRamqCode(new ProcedureCode { ProcCode="0123R",ProcCat=0 },listDefs));
		}

		[TestMethod]
		public void PrintedClaims_UseTheRamqRule() {
			string code=TestPaths.ReadRepoFile("OpenDental","Forms","FormClaimPrint.cs");
			StringAssert.Contains(code,"case \"Code\":\r\n\t\t\t\t\treturn ProcCodesOther.GetCodeForClaim(_listClaimProcs[procIndex].CodeSent,procedureCode);"
				.Replace("\r\n",code.Contains("\r\n") ? "\r\n" : "\n"));
		}
		#endregion Code systems

		#region Confirmation phrase (26.1)
		[TestMethod]
		public void Confirmation_OnlyForAdvancedOptions() {
			Assert.IsFalse(ProcCodesOther.RequiresConfirmation(EnumOtherCodeUpdates.None));
			foreach(EnumOtherCodeUpdates updates in new[] { EnumOtherCodeUpdates.Descriptions,EnumOtherCodeUpdates.Abbreviations,EnumOtherCodeUpdates.TreatArea,
				EnumOtherCodeUpdates.PaintType,EnumOtherCodeUpdates.Descriptions|EnumOtherCodeUpdates.PaintType })
			{
				Assert.IsTrue(ProcCodesOther.RequiresConfirmation(updates),updates.ToString());
			}
		}

		[TestMethod]
		public void Confirmation_PhraseMustMatch() {
			Assert.IsTrue(ProcCodesOther.IsConfirmationPhrase(ProcCodesOther.CONFIRMATION_PHRASE));
			Assert.IsTrue(ProcCodesOther.IsConfirmationPhrase("  update codes "));
			Assert.IsFalse(ProcCodesOther.IsConfirmationPhrase(""));
			Assert.IsFalse(ProcCodesOther.IsConfirmationPhrase(null));
			Assert.IsFalse(ProcCodesOther.IsConfirmationPhrase("UPDATE"));
			Assert.IsFalse(ProcCodesOther.IsConfirmationPhrase("UPDATE  CODES"));
		}

		[TestMethod]
		public void Confirmation_WindowAsksBeforeReadingTheFile() {
			string code=TestPaths.ReadRepoFile("OpenDental","Forms","FormProcCodesOther.cs");
			int indexOk=code.IndexOf("private void butOK_Click(");
			int indexSecurityAdmin=code.IndexOf("!Security.IsAuthorized(EnumPermType.SecurityAdmin) || !Security.IsAuthorized(EnumPermType.ProcCodeEdit)",
				indexOk);
			int indexConfirm=code.IndexOf("if(!ConfirmAdvancedOptions(updates))",indexOk);
			int indexApply=code.IndexOf("ProcCodesOther.Apply(otherCodeImportPlan)",indexOk);
			Assert.IsTrue(indexOk>0 && indexSecurityAdmin>indexOk,"OK checks Security Admin and Procedure Code Edit.");
			Assert.IsTrue(indexConfirm>indexSecurityAdmin && indexApply>indexConfirm,"The phrase is asked before anything is saved.");
			StringAssert.Contains(code,"ProcCodesOther.IsConfirmationPhrase(inputBox.StringResult)");
			StringAssert.Contains(code,"ProcCodesOther.RequiresConfirmation(updates)");
		}
		#endregion Confirmation phrase (26.1)

		#region Parse
		[TestMethod]
		public void Parse_NoHeader_ColumnsInTheDocumentedOrder() {
			List<OtherCodeRow> listRows=Parse("90001,Synthetic scaling one unit,Scale1,Mouth,None,1\r\n"
				+"# a comment\r\n"
				+"\r\n"
				+"90002,Synthetic sealant,Seal,Tooth,Sealant,0.5\r\n"
				+"90003,Synthetic exam\r\n",out List<string> listErrors);
			Assert.AreEqual(0,listErrors.Count,string.Join("; ",listErrors));
			Assert.AreEqual(3,listRows.Count);
			Assert.AreEqual("90001",listRows[0].ProcCode);
			Assert.AreEqual("Synthetic scaling one unit",listRows[0].Descript);
			Assert.AreEqual("Scale1",listRows[0].AbbrDesc);
			Assert.AreEqual(TreatmentArea.Mouth,listRows[0].TreatArea);
			Assert.AreEqual(ToothPaintingType.None,listRows[0].PaintType);
			Assert.AreEqual(1d,listRows[0].CanadaTimeUnits);
			Assert.AreEqual(1,listRows[0].LineNumber);
			Assert.AreEqual(TreatmentArea.Tooth,listRows[1].TreatArea);
			Assert.AreEqual(ToothPaintingType.Sealant,listRows[1].PaintType);
			Assert.AreEqual(0.5d,listRows[1].CanadaTimeUnits);
			Assert.AreEqual(4,listRows[1].LineNumber);
			//Blank cells stay "not given".
			Assert.AreEqual("",listRows[2].AbbrDesc);
			Assert.IsNull(listRows[2].TreatArea);
			Assert.IsNull(listRows[2].PaintType);
			Assert.IsNull(listRows[2].CanadaTimeUnits);
		}

		[TestMethod]
		public void Parse_HeaderRow_ColumnsByNameInAnyOrder() {
			List<OtherCodeRow> listRows=Parse("Time Units\tPaint Type\tCode\tExtra\tDescription\tTreatment Area\tAbbreviation\r\n"
				+"2\tFillingLight\t90010\tignored\tSynthetic filling\tSurface\tFill\r\n",out List<string> listErrors);
			//"Time Units" first: the header is only recognised when the first cell names the code column.
			Assert.AreEqual(0,listRows.Count(x => x.ProcCode=="90010"),"Not a header when the code is not the first column.");
			listRows=Parse("Code\tDescription\tAbbreviation\tTreatment Area\tPaint Type\tTime Units\tExtra\r\n"
				+"90010\tSynthetic filling\tFill\tSurface\tFilling Light\t2\tignored\r\n",out listErrors);
			Assert.AreEqual(0,listErrors.Count,string.Join("; ",listErrors));
			Assert.AreEqual(1,listRows.Count);
			Assert.AreEqual("90010",listRows[0].ProcCode);
			Assert.AreEqual(TreatmentArea.Surf,listRows[0].TreatArea);
			Assert.AreEqual(ToothPaintingType.FillingLight,listRows[0].PaintType);
			Assert.AreEqual(2d,listRows[0].CanadaTimeUnits);
			listRows=Parse("ProcCode,Descript,AbbrDesc\r\n90011,Synthetic one,One\r\n",out listErrors);
			Assert.AreEqual(1,listRows.Count);
			Assert.AreEqual("One",listRows[0].AbbrDesc);
			Assert.IsNull(listRows[0].TreatArea);
		}

		[TestMethod]
		public void Parse_QuotedCellsAsSpreadsheetsWriteThem() {
			List<OtherCodeRow> listRows=Parse("90020,\"Synthetic, with a comma\",\"Say \"\"hi\"\"\",3,0,\"1,5\"\r\n",out List<string> listErrors);
			Assert.AreEqual(0,listErrors.Count,string.Join("; ",listErrors));
			Assert.AreEqual("Synthetic, with a comma",listRows[0].Descript);
			Assert.AreEqual("Say \"hi\"",listRows[0].AbbrDesc);
			Assert.AreEqual(TreatmentArea.Mouth,listRows[0].TreatArea,"Numbers are accepted.");
			Assert.AreEqual(1.5d,listRows[0].CanadaTimeUnits,"A French Canadian decimal comma.");
			CollectionAssert.AreEqual(new[] { "a","b c","5\" wide","" },ProcCodesOther.SplitLine("a, \"b c\",5\" wide,",',').ToArray());
		}

		[TestMethod]
		public void Parse_BadRowsAreListed_GoodRowsKept() {
			List<OtherCodeRow> listRows=Parse("90030,Synthetic ok\r\n"
				+",No code\r\n"
				+"90031,Synthetic,Abbr,Elbow\r\n"
				+"90032,Synthetic,Abbr,Mouth,Glitter\r\n"
				+"90033,Synthetic,Abbr,Mouth,None,lots\r\n"
				+"90034,Synthetic,Abbr,Mouth,None,-1\r\n"
				+"90035,"+new string('x',256)+"\r\n"
				+"90036,Synthetic,"+new string('y',51)+"\r\n"
				+"90037,Synthetic,Abbr,99\r\n",out List<string> listErrors);
			Assert.AreEqual(1,listRows.Count);
			Assert.AreEqual("90030",listRows[0].ProcCode);
			Assert.AreEqual(8,listErrors.Count,string.Join("; ",listErrors));
			StringAssert.StartsWith(listErrors[0],"Line 2:");
			StringAssert.Contains(listErrors[1],"treatment area");
			StringAssert.Contains(listErrors[2],"paint type");
			StringAssert.Contains(listErrors[3],"time units");
			StringAssert.Contains(listErrors[4],"time units");
			StringAssert.Contains(listErrors[5],"description");
			StringAssert.Contains(listErrors[6],"abbreviation");
			StringAssert.Contains(listErrors[7],"treatment area");
		}

		[TestMethod]
		public void Parse_HeaderWithoutCodeColumn_ReadsNothing() {
			List<OtherCodeRow> listRows=Parse("Code\r\n",out List<string> listErrors);
			Assert.AreEqual(0,listRows.Count);
			Assert.AreEqual(0,listErrors.Count,"A header alone is not an error; the window says no codes were found.");
		}

		[TestMethod]
		public void DecodeFileBytes_Utf8Utf16AndAnsi() {
			string text="90040,Détartrage synthétique\r\n";
			Assert.AreEqual(text,ProcCodesOther.DecodeFileBytes(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text)).ToArray()));
			Assert.AreEqual(text,ProcCodesOther.DecodeFileBytes(Encoding.UTF8.GetBytes(text)));
			Assert.AreEqual(text,ProcCodesOther.DecodeFileBytes(Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(text)).ToArray()));
			Assert.AreEqual(text,ProcCodesOther.DecodeFileBytes(Encoding.BigEndianUnicode.GetPreamble().Concat(Encoding.BigEndianUnicode.GetBytes(text))
				.ToArray()));
			if(Encoding.Default.CodePage==1252) {//The ANSI fallback is the computer's code page.
				Assert.AreEqual(text,ProcCodesOther.DecodeFileBytes(Encoding.GetEncoding(1252).GetBytes(text)),"Windows-1252 is not valid UTF-8.");
			}
			Assert.AreEqual("",ProcCodesOther.DecodeFileBytes(null));
		}
		#endregion Parse

		#region Plan
		private static List<ProcedureCode> GetExisting() {
			return new List<ProcedureCode> {
				new ProcedureCode { CodeNum=1,ProcCode="90001",Descript="Synthetic existing one",AbbrDesc="Ex1",ProcCat=5,TreatArea=TreatmentArea.Mouth,
					PaintType=ToothPaintingType.None },
				new ProcedureCode { CodeNum=2,ProcCode="90002",Descript="Synthetic existing two",AbbrDesc="Ex2",ProcCat=5,TreatArea=TreatmentArea.Tooth,
					PaintType=ToothPaintingType.Sealant },
				new ProcedureCode { CodeNum=3,ProcCode="0500R",Descript="Synthetic RAMQ",AbbrDesc="RQ",ProcCat=6 },
			};
		}

		[TestMethod]
		public void Plan_Cdha_AddsMissingSkipsExisting() {
			OtherCodeImportPlan plan=Plan(EnumOtherCodeSystem.Cdha,EnumOtherCodeUpdates.None,
				"90001,Synthetic hygiene one,H1,Mouth\r\n"
				+"90003,Synthetic hygiene three,,Quad,None,2\r\n"
				+"90004,Synthetic hygiene four,H4\r\n",GetExisting());
			Assert.AreEqual("CDHA",plan.CategoryName);
			CollectionAssert.AreEqual(new[] { "90003","90004" },plan.ListAdds.Select(x => x.ProcCode).ToArray());
			Assert.AreEqual(0,plan.ListChanges.Count);
			Assert.AreEqual(1,plan.CountSkipped);
			Assert.AreEqual(0,plan.ListErrors.Count);
			ProcedureCode procedureCode=plan.ListAdds[0];
			Assert.AreEqual("Synthetic hygiene three",procedureCode.Descript);
			Assert.AreEqual("90003",procedureCode.AbbrDesc,"A blank abbreviation becomes the code.");
			Assert.AreEqual(TreatmentArea.Quad,procedureCode.TreatArea);
			Assert.AreEqual(2d,procedureCode.CanadaTimeUnits);
			Assert.AreEqual(0,procedureCode.ProvNumDefault);
			Assert.AreEqual(TreatmentArea.None,plan.ListAdds[1].TreatArea);
			Assert.IsTrue(plan.HasWork());
		}

		[TestMethod]
		public void Plan_RefusesSeparatorsDuplicatesAndBlankDescriptions() {
			OtherCodeImportPlan plan=Plan(EnumOtherCodeSystem.DacEnglish,EnumOtherCodeUpdates.None,
				"90050,Synthetic denture one\r\n"
				+"900-51,Synthetic hyphen\r\n"
				+"\"90,52\",Synthetic comma\r\n"
				+"90053‑1,Synthetic non-breaking hyphen\r\n"
				+"90050,Synthetic denture one again\r\n"
				+"90054\r\n"
				+"ABCDEFGHIJKLMNOP,Synthetic too long\r\n",GetExisting());
			CollectionAssert.AreEqual(new[] { "90050" },plan.ListAdds.Select(x => x.ProcCode).ToArray());
			Assert.AreEqual(6,plan.ListErrors.Count,string.Join("; ",plan.ListErrors));
			Assert.IsTrue(plan.ListErrors.Count(x => x.Contains("commas or hyphens"))==3);
			Assert.IsTrue(plan.ListErrors.Any(x => x.Contains("more than once")));
			Assert.IsTrue(plan.ListErrors.Any(x => x.Contains("no description")));
			Assert.IsTrue(plan.ListErrors.Any(x => x.Contains("longer than 15")));
			Assert.AreEqual("DAC",plan.CategoryName);
		}

		[TestMethod]
		public void Plan_Ramq_AddsWithRAndChecksTheLengthWithTheR() {
			OtherCodeImportPlan plan=Plan(EnumOtherCodeSystem.Ramq,EnumOtherCodeUpdates.None,
				"0500,Synthetic RAMQ exists\r\n"
				+"0600,Synthetic RAMQ new\r\n"
				+"90001,Synthetic same number as a CDA-style code\r\n"
				+"ABCDEFGHIJKLMNO,Synthetic fifteen plus R\r\n",GetExisting());
			CollectionAssert.AreEqual(new[] { "0600R","90001R" },plan.ListAdds.Select(x => x.ProcCode).ToArray(),"Never clashes with the existing 90001.");
			Assert.AreEqual(1,plan.CountSkipped);
			Assert.AreEqual(1,plan.ListErrors.Count);
			StringAssert.Contains(plan.ListErrors[0],"ABCDEFGHIJKLMNOR");
		}

		[TestMethod]
		public void Plan_AdvancedOptions_ChangeOnlyTheCheckedFields() {
			string text="90001,Synthetic new wording,New1,Quad,Implant\r\n"
				+"90002,,,,\r\n";//Blank cells never clear a field.
			OtherCodeImportPlan plan=Plan(EnumOtherCodeSystem.Cdha,EnumOtherCodeUpdates.Descriptions,text,GetExisting());
			Assert.AreEqual(1,plan.ListChanges.Count);
			Assert.AreEqual(1,plan.CountSkipped);
			OtherCodeChange change=plan.ListChanges[0];
			Assert.AreEqual("Synthetic existing one",change.ProcedureCodeOld.Descript);
			Assert.AreEqual("Synthetic new wording",change.ProcedureCodeNew.Descript);
			Assert.AreEqual("Ex1",change.ProcedureCodeNew.AbbrDesc);
			Assert.AreEqual(TreatmentArea.Mouth,change.ProcedureCodeNew.TreatArea);
			Assert.AreEqual(ToothPaintingType.None,change.ProcedureCodeNew.PaintType);
			plan=Plan(EnumOtherCodeSystem.Cdha,EnumOtherCodeUpdates.Abbreviations|EnumOtherCodeUpdates.TreatArea|EnumOtherCodeUpdates.PaintType,text,
				GetExisting());
			change=plan.ListChanges.Single();
			Assert.AreEqual("Synthetic existing one",change.ProcedureCodeNew.Descript);
			Assert.AreEqual("New1",change.ProcedureCodeNew.AbbrDesc);
			Assert.AreEqual(TreatmentArea.Quad,change.ProcedureCodeNew.TreatArea);
			Assert.AreEqual(ToothPaintingType.Implant,change.ProcedureCodeNew.PaintType);
			Assert.AreEqual(1L,change.ProcedureCodeNew.CodeNum);
			//The existing list is not changed by planning.
			Assert.AreEqual("Ex1",GetExisting()[0].AbbrDesc);
			using(new CultureScope("en-US")) {
				string log=ProcCodesOther.GetChangeLogText(change,"CDHA");
				StringAssert.Contains(log,"Code 90001");
				StringAssert.Contains(log,"abbreviation changed from 'Ex1' to 'New1'");
				StringAssert.Contains(log,"treatment area changed from 'Mouth' to 'Quad'");
				StringAssert.Contains(log,"paint type changed from 'None' to 'Implant'");
				Assert.IsFalse(log.Contains("description changed"));
			}
		}

		[TestMethod]
		public void Plan_Cda_UpdatesExistingNeverAdds() {
			OtherCodeImportPlan plan=Plan(EnumOtherCodeSystem.Cda,EnumOtherCodeUpdates.PaintType,
				"90002,Synthetic,,,Veneer\r\n"
				+"90099,Synthetic not in the database,,,Veneer\r\n",GetExisting());
			Assert.AreEqual(0,plan.ListAdds.Count);
			Assert.AreEqual(1,plan.CountNotFound);
			Assert.AreEqual(ToothPaintingType.Veneer,plan.ListChanges.Single().ProcedureCodeNew.PaintType);
			Assert.AreEqual("",plan.CategoryName);
		}

		[TestMethod]
		public void Plan_DacFrenchAfterEnglish_KeepsTheFirstLanguageUnlessAsked() {
			List<ProcedureCode> listExisting=new List<ProcedureCode> {
				new ProcedureCode { CodeNum=7,ProcCode="91001",Descript="Synthetic denture (English)",AbbrDesc="Den",ProcCat=8 },
			};
			string textFrench="91001,Prothèse synthétique (français)\r\n91002,Rebasage synthétique\r\n";
			OtherCodeImportPlan plan=Plan(EnumOtherCodeSystem.DacFrench,EnumOtherCodeUpdates.None,textFrench,listExisting);
			Assert.AreEqual(0,plan.ListChanges.Count);
			Assert.AreEqual(1,plan.CountSkipped);
			Assert.AreEqual("91002",plan.ListAdds.Single().ProcCode);
			plan=Plan(EnumOtherCodeSystem.DacFrench,EnumOtherCodeUpdates.Descriptions,textFrench,listExisting);
			Assert.AreEqual("Prothèse synthétique (français)",plan.ListChanges.Single().ProcedureCodeNew.Descript);
		}

		[TestMethod]
		public void Plan_SummaryAndLogText() {
			OtherCodeImportPlan plan=Plan(EnumOtherCodeSystem.Ramq,EnumOtherCodeUpdates.Descriptions,
				"0500,Synthetic RAMQ renamed\r\n0700,Synthetic RAMQ seven\r\n07-1,Synthetic bad\r\n",GetExisting());
			using(new CultureScope("en-US")) {
				string summary=ProcCodesOther.GetPlanSummary(plan);
				StringAssert.Contains(summary,"Code system: RAMQ");
				StringAssert.Contains(summary,"Codes to add: 1 (category RAMQ)");
				StringAssert.Contains(summary,"Codes to update: 1");
				StringAssert.Contains(summary,"Advanced Options: Update descriptions");
				StringAssert.Contains(summary,"Rows refused: 1");
				OtherCodeImportResult result=new OtherCodeImportResult { CountAdded=1,CountUpdated=1,CountSkipped=0,CountRefused=1,IsCategoryCreated=true };
				string log=ProcCodesOther.GetLogText(plan,result);
				Assert.AreEqual("Other Procedure Codes import, RAMQ: 1 added, 1 updated, 0 skipped, 1 refused. Advanced Options: Update descriptions. "
					+"New category: RAMQ.",log);
			}
		}
		#endregion Plan
	}
}
