// Database tests for package M2-CAN on the run's scratch MariaDB database (ScratchDb): the Other Procedure Codes import saves codes into the code
// system's category and logs them, a second run changes nothing, Advanced Options update existing codes, RAMQ codes print without their R, and
// Database Maintenance lists codes with commas or hyphens without changing them. Synthetic codes and descriptions only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Data;
using System.Linq;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2CANTests {
	[TestClass]
	public class M2CanDbTests {
		private CultureScope _cultureScope;

		[TestInitialize]
		public void TestInitialize() {
			string skipReason=ScratchDb.EnsureCreated();
			if(skipReason!=null) {
				Assert.Inconclusive(skipReason);
			}
			_cultureScope=new CultureScope("en-US");
			ScratchDb.Clear();
		}

		[TestCleanup]
		public void TestCleanup() {
			_cultureScope?.Dispose();
		}

		#region Synthetic data
		private static long AddCategory(string name,int itemOrder,bool isHidden=false) {
			Def def=new Def { Category=DefCat.ProcCodeCats,ItemName=name,ItemOrder=itemOrder,IsHidden=isHidden };
			return Defs.Insert(def);
		}

		private static long AddCode(string procCode,string descript,long procCat,ToothPaintingType paintType=ToothPaintingType.None) {
			ProcedureCode procedureCode=new ProcedureCode { ProcCode=procCode,Descript=descript,AbbrDesc=procCode,ProcCat=procCat,PaintType=paintType };
			return ProcedureCodes.Insert(procedureCode);
		}

		private static List<ProcedureCode> GetCodes() {
			return ProcedureCodes.GetAllCodes();
		}

		private static List<string> GetLogTexts(EnumPermType permType) {
			return DataCore.GetTable("SELECT LogText FROM securitylog WHERE PermType="+(int)permType+" ORDER BY SecurityLogNum").Select()
				.Select(x => x[0].ToString()).ToList();
		}

		private static OtherCodeImportResult Import(EnumOtherCodeSystem codeSystem,EnumOtherCodeUpdates updates,string text,
			out OtherCodeImportPlan otherCodeImportPlan)
		{
			List<OtherCodeRow> listRows=ProcCodesOther.Parse(text,out List<string> listErrors);
			otherCodeImportPlan=ProcCodesOther.CreatePlanFromDb(codeSystem,updates,listRows,listErrors);
			return ProcCodesOther.Apply(otherCodeImportPlan);
		}
		#endregion Synthetic data

		[TestMethod]
		public void Cdha_ImportsIntoANewCategoryAndLogs() {
			long defNumDiag=AddCategory("Synthetic Diagnostic",0);
			AddCategory("Synthetic Preventive",3);
			AddCode("90001","Synthetic existing exam",defNumDiag);
			string text="Code,Description,Abbreviation,Treatment Area,Paint Type,Time Units\r\n"
				+"90001,Synthetic hygiene exam,HExam,Mouth,None,1\r\n"
				+"90100,Synthetic hygiene scaling,HScale,Quad,None,1.5\r\n"
				+"90101,Synthetic hygiene sealant,HSeal,Tooth,Sealant,\r\n"
				+"901-02,Synthetic hyphen code,Bad,Mouth,None,1\r\n";
			OtherCodeImportResult result=Import(EnumOtherCodeSystem.Cdha,EnumOtherCodeUpdates.None,text,out OtherCodeImportPlan plan);
			Assert.AreEqual(2,result.CountAdded);
			Assert.AreEqual(0,result.CountUpdated);
			Assert.AreEqual(1,result.CountSkipped);
			Assert.AreEqual(1,result.CountRefused);
			Assert.IsTrue(result.IsCategoryCreated);
			List<Def> listDefs=OpenPmsDentalBusiness.Crud.DefCrud.SelectMany("SELECT * FROM definition WHERE Category="+(int)DefCat.ProcCodeCats+" ORDER BY ItemOrder");
			Def defCdha=listDefs.Single(x => x.ItemName=="CDHA");
			Assert.AreEqual(4,defCdha.ItemOrder,"After the last category.");
			Assert.AreEqual(result.DefNumCategory,defCdha.DefNum);
			List<ProcedureCode> listCodes=GetCodes();
			Assert.AreEqual(3,listCodes.Count);
			ProcedureCode procedureCodeScaling=listCodes.Single(x => x.ProcCode=="90100");
			Assert.AreEqual(defCdha.DefNum,procedureCodeScaling.ProcCat);
			Assert.AreEqual("Synthetic hygiene scaling",procedureCodeScaling.Descript);
			Assert.AreEqual("HScale",procedureCodeScaling.AbbrDesc);
			Assert.AreEqual(TreatmentArea.Quad,procedureCodeScaling.TreatArea);
			Assert.AreEqual(1.5d,procedureCodeScaling.CanadaTimeUnits);
			Assert.AreEqual(ToothPaintingType.Sealant,listCodes.Single(x => x.ProcCode=="90101").PaintType);
			//The existing code is skipped: not moved, not renamed.
			ProcedureCode procedureCodeExisting=listCodes.Single(x => x.ProcCode=="90001");
			Assert.AreEqual("Synthetic existing exam",procedureCodeExisting.Descript);
			Assert.AreEqual(defNumDiag,procedureCodeExisting.ProcCat);
			Assert.IsFalse(listCodes.Any(x => x.ProcCode.Contains("-")));
			//Logs: the new category, one entry per added code, one for the run.
			Assert.IsTrue(GetLogTexts(EnumPermType.DefEdit).Any(x => x.Contains("CDHA")));
			List<string> listLogs=GetLogTexts(EnumPermType.ProcCodeEdit);
			Assert.AreEqual(3,listLogs.Count,string.Join(" | ",listLogs));
			Assert.IsTrue(listLogs.Any(x => x.StartsWith("Code 90100 added by the Other Procedure Codes import (CDHA)")));
			Assert.AreEqual("Other Procedure Codes import, CDHA: 2 added, 0 updated, 1 skipped, 1 refused. New category: CDHA.",listLogs.Last());
			//A second run changes nothing and creates no second category.
			result=Import(EnumOtherCodeSystem.Cdha,EnumOtherCodeUpdates.None,text,out plan);
			Assert.IsFalse(plan.HasWork());
			Assert.AreEqual(0,result.CountAdded);
			Assert.AreEqual(3,result.CountSkipped);
			Assert.IsFalse(result.IsCategoryCreated);
			Assert.AreEqual(1,OpenPmsDentalBusiness.Crud.DefCrud.SelectMany("SELECT * FROM definition WHERE ItemName='CDHA'").Count);
			Assert.AreEqual(3,GetCodes().Count);
		}

		[TestMethod]
		public void Dac_EnglishThenFrench_OneCategory_DescriptionsOnlyWhenAsked() {
			Import(EnumOtherCodeSystem.DacEnglish,EnumOtherCodeUpdates.None,"91001,Synthetic denture,Den\r\n91002,Synthetic reline,Rel\r\n",out _);
			OtherCodeImportResult result=Import(EnumOtherCodeSystem.DacFrench,EnumOtherCodeUpdates.None,
				"91001,Prothèse synthétique,Pro\r\n91003,Regarnissage synthétique,Reg\r\n",out _);
			Assert.AreEqual(1,result.CountAdded);
			Assert.AreEqual(1,result.CountSkipped);
			Assert.IsFalse(result.IsCategoryCreated,"DAC English and French share the DAC category.");
			Assert.AreEqual("Synthetic denture",GetCodes().Single(x => x.ProcCode=="91001").Descript);
			result=Import(EnumOtherCodeSystem.DacFrench,EnumOtherCodeUpdates.Descriptions,"91001,Prothèse synthétique,Pro\r\n",out _);
			Assert.AreEqual(1,result.CountUpdated);
			ProcedureCode procedureCode=GetCodes().Single(x => x.ProcCode=="91001");
			Assert.AreEqual("Prothèse synthétique",procedureCode.Descript);
			Assert.AreEqual("Den",procedureCode.AbbrDesc,"Abbreviations were not asked for.");
			List<string> listLogs=GetLogTexts(EnumPermType.ProcCodeEdit);
			Assert.IsTrue(listLogs.Any(x => x=="Code 91001 description changed from 'Synthetic denture' to 'Prothèse synthétique' (Other Procedure Codes "
				+"import, DAC (French))."),string.Join(" | ",listLogs));
			Assert.AreEqual(1,OpenPmsDentalBusiness.Crud.DefCrud.SelectMany("SELECT * FROM definition WHERE ItemName='DAC'").Count);
			Assert.AreEqual(3,GetCodes().Count(x => x.ProcCat==GetCodes().First().ProcCat));
		}

		[TestMethod]
		public void Ramq_StoredWithR_PrintsWithout() {
			long defNumCda=AddCategory("Synthetic CDA",0);
			AddCode("02000","Synthetic CDA code",defNumCda);
			OtherCodeImportResult result=Import(EnumOtherCodeSystem.Ramq,EnumOtherCodeUpdates.None,"02000,Synthetic RAMQ code\r\n",out _);
			Assert.AreEqual(1,result.CountAdded);
			List<ProcedureCode> listCodes=GetCodes();
			Assert.AreEqual(2,listCodes.Count,"The RAMQ code does not clash with the CDA code of the same number.");
			ProcedureCode procedureCodeRamq=listCodes.Single(x => x.ProcCode=="02000R");
			Defs.RefreshCache();
			Assert.IsTrue(ProcCodesOther.IsRamqCode(procedureCodeRamq));
			Assert.AreEqual("02000",ProcCodesOther.GetCodeForClaim("02000R",procedureCodeRamq));
			ProcedureCode procedureCodeCda=listCodes.Single(x => x.ProcCode=="02000");
			Assert.IsFalse(ProcCodesOther.IsRamqCode(procedureCodeCda));
			//Moved out of the RAMQ category by the office: printed as stored.
			procedureCodeRamq.ProcCat=defNumCda;
			Assert.AreEqual("02000R",ProcCodesOther.GetCodeForClaim("02000R",procedureCodeRamq));
		}

		[TestMethod]
		public void Cda_AdvancedOptionsUpdateExistingCodesOnly() {
			long defNumCda=AddCategory("Synthetic CDA",0);
			AddCode("03000","Synthetic CDA one",defNumCda,ToothPaintingType.None);
			AddCode("03001","Synthetic CDA two",defNumCda,ToothPaintingType.Veneer);
			OtherCodeImportResult result=Import(EnumOtherCodeSystem.Cda,EnumOtherCodeUpdates.PaintType|EnumOtherCodeUpdates.TreatArea,
				"03000,Synthetic CDA one,,Tooth,CrownLight\r\n03001,Synthetic CDA two,,,Veneer\r\n03999,Synthetic not here,,Tooth,CrownLight\r\n",
				out OtherCodeImportPlan plan);
			Assert.AreEqual(0,result.CountAdded);
			Assert.AreEqual(1,result.CountUpdated);
			Assert.AreEqual(1,result.CountSkipped);
			Assert.AreEqual(1,result.CountNotFound);
			Assert.IsFalse(result.IsCategoryCreated);
			ProcedureCode procedureCode=GetCodes().Single(x => x.ProcCode=="03000");
			Assert.AreEqual(ToothPaintingType.CrownLight,procedureCode.PaintType);
			Assert.AreEqual(TreatmentArea.Tooth,procedureCode.TreatArea);
			Assert.AreEqual(defNumCda,procedureCode.ProcCat);
			Assert.AreEqual(2,GetCodes().Count);
			Assert.AreEqual(1,OpenPmsDentalBusiness.Crud.DefCrud.SelectMany("SELECT * FROM definition").Count);
			string log=GetLogTexts(EnumPermType.ProcCodeEdit).Last();
			StringAssert.StartsWith(log,"Other Procedure Codes import, CDA: 0 added, 1 updated, 1 skipped, 1 not found.");
			StringAssert.Contains(log,"Advanced Options: Update treatment area, Update paint type.");
		}

		[TestMethod]
		public void Apply_KeepsChangesMadeMeanwhileToOtherFields() {
			long defNum=AddCategory("Synthetic CDA",0);
			long codeNum=AddCode("04000","Synthetic before",defNum);
			List<OtherCodeRow> listRows=ProcCodesOther.Parse("04000,Synthetic after\r\n04001,Synthetic added\r\n",out List<string> listErrors);
			OtherCodeImportPlan plan=ProcCodesOther.CreatePlanFromDb(EnumOtherCodeSystem.Cdha,EnumOtherCodeUpdates.Descriptions,listRows,listErrors);
			//Someone edits the code, and adds 04001, between the plan and the save.
			DataCore.NonQ("UPDATE procedurecode SET AbbrDesc='Edited' WHERE CodeNum="+codeNum);
			AddCode("04001","Synthetic added by hand",defNum);
			OtherCodeImportResult result=ProcCodesOther.Apply(plan);
			Assert.AreEqual(0,result.CountAdded);
			Assert.AreEqual(1,result.CountSkipped);
			ProcedureCode procedureCode=ProcedureCodes.GetProcCodeFromDb(codeNum);
			Assert.AreEqual("Synthetic after",procedureCode.Descript);
			Assert.AreEqual("Edited",procedureCode.AbbrDesc);
			Assert.AreEqual(1,GetCodes().Count(x => x.ProcCode=="04001"));
			Assert.AreEqual("Synthetic added by hand",GetCodes().Single(x => x.ProcCode=="04001").Descript);
		}

		[TestMethod]
		public void HiddenCategoryWithTheName_IsReused() {
			long defNumHidden=AddCategory("RAMQ",0,isHidden:true);
			OtherCodeImportResult result=Import(EnumOtherCodeSystem.Ramq,EnumOtherCodeUpdates.None,"05000,Synthetic RAMQ\r\n",out _);
			Assert.IsFalse(result.IsCategoryCreated);
			Assert.AreEqual(defNumHidden,GetCodes().Single().ProcCat);
		}

		[TestMethod]
		public void Dbm_ListsCodesWithSeparatorsAndChangesNothing() {
			long defNum=AddCategory("Synthetic",0);
			AddCode("06000","Synthetic fine",defNum);
			//Codes from before 26.1, written straight to the table as an old database has them.
			DataCore.NonQ("INSERT INTO procedurecode (ProcCode,Descript,AbbrDesc,ProcCat,GraphicColor,IsCanadianLab,BaseUnits,SubstOnlyIf,IsMultiVisit,"
				+"DrugNDC,RevenueCodeDefault,ProvNumDefault,CanadaTimeUnits,IsRadiology,DefaultClaimNote,DefaultTPNote,BypassGlobalLock,TaxCode,PaintText,"
				+"AreaAlsoToothRange,DiagnosticCodes) VALUES "
				+"('06-01','Synthetic hyphen',''," +defNum+",0,0,0,0,0,'','',0,0,0,'','',0,'','',0,''),"
				+"('06,02','Synthetic comma',''," +defNum+",0,0,0,0,0,'','',0,0,0,'','',0,'','',0,''),"
				+"('06‑03','Synthetic nb hyphen','',"+defNum+",0,0,0,0,0,'','',0,0,0,'','',0,'','',0,'')");
			string log=DatabaseMaintenances.ProcedurecodeCodeHasSeparator(false,DbmMode.Check);
			StringAssert.Contains(log,"Procedure codes that contain a comma or a hyphen: 3");
			StringAssert.Contains(log,"Manual fix needed");
			string breakdown=DatabaseMaintenances.ProcedurecodeCodeHasSeparator(false,DbmMode.Breakdown);
			StringAssert.Contains(breakdown,"06-01 - Synthetic hyphen");
			StringAssert.Contains(breakdown,"06,02 - Synthetic comma");
			StringAssert.Contains(breakdown,"06‑03 - Synthetic nb hyphen");
			Assert.IsFalse(breakdown.Contains("06000"));
			DatabaseMaintenances.ProcedurecodeCodeHasSeparator(false,DbmMode.Fix);
			CollectionAssert.AreEquivalent(new[] { "06000","06-01","06,02","06‑03" },GetCodes().Select(x => x.ProcCode).ToArray(),
				"Fix never renames or deletes codes.");
			//Nothing to report: empty unless verbose.
			DataCore.NonQ("DELETE FROM procedurecode WHERE ProcCode!='06000'");
			Assert.AreEqual("",DatabaseMaintenances.ProcedurecodeCodeHasSeparator(false,DbmMode.Check));
			StringAssert.Contains(DatabaseMaintenances.ProcedurecodeCodeHasSeparator(true,DbmMode.Check),": 0");
		}
	}
}
