// D Codes and CDA Codes imports without a database: reading the code file the office supplies (XML, tab and comma separated text, with or
// without a header row) and working out what the import does. Synthetic codes and descriptions only: no ADA or CDA code text.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace BPROCTests {
	[TestClass]
	public class ProcCodeImportTests {
		[TestInitialize]
		public void TestInitialize() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
		}

		#region Parse
		[TestMethod]
		public void Parse_PositionalTabs_OldCanadianColumnOrder() {
			//ProcCode, Descript, TreatArea, NoBillIns, IsProsth, IsHygiene, PaintType, ProcCatDescript, ProcTime, AbbrDesc, CanadaTimeUnits
			string text="# synthetic test file\r\n"
				+"99111\tSynthetic exam one\t3\t0\t0\t1\t0\tExams\t/X/\tSynEx1\t1.5\r\n"
				+"\r\n"
				+"99112\tSynthetic filling two\t2\t1\t1\t0\t1\tRestorative\t/XX/\tSynFil2\t2\r\n";
			List<ProcCodeImportRow> listRows=ProcCodeImports.Parse(text,out List<string> listErrors);
			Assert.AreEqual(0,listErrors.Count,string.Join("; ",listErrors));
			Assert.AreEqual(2,listRows.Count);
			ProcCodeImportRow procCodeImportRow=listRows[1];
			Assert.AreEqual("99112",procCodeImportRow.ProcCode);
			Assert.AreEqual("Synthetic filling two",procCodeImportRow.Descript);
			Assert.AreEqual(TreatmentArea.Tooth,procCodeImportRow.TreatArea);
			Assert.IsTrue(procCodeImportRow.NoBillIns);
			Assert.IsTrue(procCodeImportRow.IsProsth);
			Assert.IsFalse(procCodeImportRow.IsHygiene);
			Assert.AreEqual((ToothPaintingType)1,procCodeImportRow.PaintType);
			Assert.AreEqual("Restorative",procCodeImportRow.ProcCatDescript);
			Assert.AreEqual("/XX/",procCodeImportRow.ProcTime);
			Assert.AreEqual("SynFil2",procCodeImportRow.AbbrDesc);
			Assert.AreEqual(2.0,procCodeImportRow.CanadaTimeUnits);
			Assert.AreEqual(4,procCodeImportRow.LineNumber);
			Assert.IsTrue(listRows[0].IsHygiene);
			Assert.AreEqual(1.5,listRows[0].CanadaTimeUnits);
		}

		[TestMethod]
		public void Parse_CommaHeader_ColumnsByNameAndQuotes() {
			string text="Code,Nomenclature,Abbreviation,Category,Treatment Area,Deleted\n"
				+"D9901,\"Synthetic code, with a comma\",SynA,\"Synthetic \"\"quoted\"\" cat\",Mouth,\n"
				+"D9902,Synthetic code removed,,,,yes\n"
				+"D9903,Synthetic code three,SynC,Obsolete,,\n";
			List<ProcCodeImportRow> listRows=ProcCodeImports.Parse(text,out List<string> listErrors);
			Assert.AreEqual(0,listErrors.Count,string.Join("; ",listErrors));
			Assert.AreEqual(3,listRows.Count);
			Assert.AreEqual("Synthetic code, with a comma",listRows[0].Descript);
			Assert.AreEqual("Synthetic \"quoted\" cat",listRows[0].ProcCatDescript);
			Assert.AreEqual(TreatmentArea.Mouth,listRows[0].TreatArea);
			Assert.IsFalse(listRows[0].IsDeleted);
			Assert.IsTrue(listRows[1].IsDeleted,"Deleted column.");
			Assert.IsTrue(listRows[2].IsDeleted,"Obsolete category means deleted.");
		}

		[TestMethod]
		public void Parse_Errors_AndDuplicates() {
			string text="ProcCode\tDescript\tTreatArea\tProcTime\n"
				+"D9911\tSynthetic ok\tTooth\t/X/\n"
				+"D9912\tSynthetic bad area\tElbow\t\n"
				+"\tSynthetic no code\t\t\n"
				+"D9913\tSynthetic bad time\t\t/X-X/\n"
				+"D99140000000000000\tSynthetic too long\t\t\n"
				+"D9911\tSynthetic duplicate\t\t\n";
			List<ProcCodeImportRow> listRows=ProcCodeImports.Parse(text,out List<string> listErrors);
			CollectionAssert.AreEqual(new[] { "D9911" },listRows.Select(x => x.ProcCode).ToArray());
			Assert.AreEqual("Synthetic ok",listRows[0].Descript,"The first of a duplicate code wins.");
			Assert.AreEqual(5,listErrors.Count,string.Join("; ",listErrors));
			Assert.IsTrue(listErrors[0].StartsWith("Line 3:"),listErrors[0]);
			Assert.IsTrue(listErrors.Any(x => x.Contains("twice")));
		}

		[TestMethod]
		public void Parse_Xml_ExportFormatWithCategories() {
			List<ProcedureCode> listProcedureCodes=new List<ProcedureCode> {
				new ProcedureCode { ProcCode="D9921",Descript="Synthetic xml one",AbbrDesc="SynX1",TreatArea=TreatmentArea.Quad,ProcTime="/XX/" },
				new ProcedureCode { ProcCode="D9922",Descript="Synthetic xml two",AbbrDesc="SynX2" },
			};
			XmlSerializer xmlSerializer=new XmlSerializer(typeof(List<ProcedureCode>));
			string xml;
			using(StringWriter stringWriter=new StringWriter()) {
				xmlSerializer.Serialize(stringWriter,listProcedureCodes);
				xml=stringWriter.ToString();
			}
			//ProcCatDescript is not serialized; files may carry it as an element, the way the N Codes resource does.
			xml=xml.Replace("<ProcCode>D9922</ProcCode>","<ProcCode>D9922</ProcCode><ProcCatDescript>Obsolete</ProcCatDescript>");
			List<ProcCodeImportRow> listRows=ProcCodeImports.Parse(xml,out List<string> listErrors);
			Assert.AreEqual(0,listErrors.Count,string.Join("; ",listErrors));
			Assert.AreEqual(2,listRows.Count);
			Assert.AreEqual(TreatmentArea.Quad,listRows[0].TreatArea);
			Assert.AreEqual("/XX/",listRows[0].ProcTime);
			Assert.AreEqual("",listRows[0].ProcCatDescript);
			Assert.IsFalse(listRows[0].IsDeleted);
			Assert.IsTrue(listRows[1].IsDeleted);
		}

		[TestMethod]
		public void Parse_BadXml_IsAnError() {
			List<ProcCodeImportRow> listRows=ProcCodeImports.Parse("<ArrayOfProcedureCode><ProcedureCode>",out List<string> listErrors);
			Assert.AreEqual(0,listRows.Count);
			Assert.AreEqual(1,listErrors.Count);
		}

		[TestMethod]
		public void DecodeFileBytes_Utf8AndAnsi() {
			byte[] arrayUtf8Bom=new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes("D9931\tSynth\u00e9tique")).ToArray();
			Assert.AreEqual("D9931\tSynth\u00e9tique",ProcCodeImports.DecodeFileBytes(arrayUtf8Bom));
			byte[] arrayAnsi=Encoding.Default.GetBytes("D9932\tSynth\u00e9tique");
			Assert.AreEqual(Encoding.Default.GetString(arrayAnsi),ProcCodeImports.DecodeFileBytes(arrayAnsi),"Invalid UTF-8 falls back to the ANSI code page.");
			Assert.AreEqual("",ProcCodeImports.DecodeFileBytes(new byte[0]));
		}

		[TestMethod]
		public void SplitLine_And_Headers() {
			CollectionAssert.AreEqual(new[] { "a","b,c","" },ProcCodeImports.SplitLine("a,\"b,c\",").ToArray());
			CollectionAssert.AreEqual(new[] { "a","b,c" },ProcCodeImports.SplitLine("a\tb,c").ToArray(),"Tabs win over commas.");
			Assert.AreEqual("ProcCode",ProcCodeImports.GetColumnForHeader(" CDT Code "));
			Assert.AreEqual("Descript",ProcCodeImports.GetColumnForHeader("Description"));
			Assert.AreEqual("ProcCatDescript",ProcCodeImports.GetColumnForHeader("Category"));
			Assert.AreEqual("Deleted",ProcCodeImports.GetColumnForHeader("Is_Deleted"));
			Assert.AreEqual("",ProcCodeImports.GetColumnForHeader("Descriptor"),"Columns this import does not read are ignored.");
			Assert.IsTrue(ProcCodeImports.ParseBool("Yes"));
			Assert.IsFalse(ProcCodeImports.ParseBool(""));
		}
		#endregion Parse

		#region Plan
		private static ProcedureCode Existing(long codeNum,string procCode,string descript,string abbrDesc,long procCat) {
			return new ProcedureCode { CodeNum=codeNum,ProcCode=procCode,Descript=descript,AbbrDesc=abbrDesc,ProcCat=procCat };
		}

		private static ProcCodeImportRow Row(string procCode,string descript,string abbrDesc="",string category="",bool isDeleted=false) {
			return new ProcCodeImportRow { ProcCode=procCode,Descript=descript,AbbrDesc=abbrDesc,ProcCatDescript=category,IsDeleted=isDeleted };
		}

		private static List<Def> Cats() {
			return new List<Def> {
				new Def { DefNum=1,Category=DefCat.ProcCodeCats,ItemName="Synthetic Restorative",ItemOrder=0 },
				new Def { DefNum=2,Category=DefCat.ProcCodeCats,ItemName="Synthetic Other",ItemOrder=1 },
				new Def { DefNum=3,Category=DefCat.ProcCodeCats,ItemName="Obsolete",ItemOrder=2,IsHidden=true },
				new Def { DefNum=4,Category=DefCat.ProcCodeCats,ItemName="Synthetic Hidden",ItemOrder=3,IsHidden=true },
			};
		}

		[TestMethod]
		public void CreatePlan_Cdt() {
			List<ProcedureCode> listExisting=new List<ProcedureCode> {
				Existing(101,"D9951","Old synthetic text","",1),
				Existing(102,"D9952","Same synthetic text","Keep",1),
				Existing(103,"D9953","Synthetic soon gone","",2),
				Existing(104,"D9954","Synthetic already obsolete","",3),
				Existing(105,"D9955","Synthetic block mate","",1),
			};
			List<ProcCodeImportRow> listRows=new List<ProcCodeImportRow> {
				Row("D9951","New synthetic text","NewAbbr"),//description and blank abbreviation
				Row("D9952","Same synthetic text","Other"),//abbreviation already set: unchanged
				Row("D9953","Synthetic soon gone",isDeleted:true),//moved to Obsolete
				Row("D9954","Synthetic already obsolete",isDeleted:true),//already there: unchanged
				Row("D9956","Synthetic new code"),//added, category from the D99xx block
				Row("D9957","Synthetic new code with category",category:"Synthetic Other"),
				Row("D9958","Synthetic deleted, never here",isDeleted:true),
				Row("99999","Synthetic not a D code"),
				Row("D99A1","Synthetic not a D code either"),
			};
			ProcCodeImportPlan procCodeImportPlan=ProcCodeImports.CreatePlan(listRows,listExisting,Cats(),EnumDentalCodeSystem.CDT);
			CollectionAssert.AreEqual(new[] { "D9956","D9957" },procCodeImportPlan.ListAdds.Select(x => x.ProcedureCode.ProcCode).ToArray());
			Assert.AreEqual("Synthetic Restorative",procCodeImportPlan.ListAdds[0].CategoryName,"Most D99xx codes are in this category.");
			Assert.AreEqual("Synthetic Other",procCodeImportPlan.ListAdds[1].CategoryName);
			Assert.AreEqual(0,procCodeImportPlan.ListAdds[0].ProcedureCode.ProvNumDefault);
			Assert.AreEqual(2,procCodeImportPlan.ListChanges.Count);
			ProcCodeImportChange procCodeImportChange=procCodeImportPlan.ListChanges.Single(x => x.CodeNum==101);
			Assert.AreEqual("New synthetic text",procCodeImportChange.DescriptNew);
			Assert.AreEqual("Old synthetic text",procCodeImportChange.DescriptOld);
			Assert.AreEqual("NewAbbr",procCodeImportChange.AbbrDescNew);
			Assert.IsFalse(procCodeImportChange.IsMoveToObsolete);
			Assert.IsTrue(procCodeImportPlan.ListChanges.Single(x => x.CodeNum==103).IsMoveToObsolete);
			Assert.AreEqual(2,procCodeImportPlan.CountUnchanged);
			Assert.AreEqual(2,procCodeImportPlan.CountNotInCodeSystem);
			Assert.AreEqual(1,procCodeImportPlan.CountDeletedNotPresent);
			Assert.AreEqual(1,procCodeImportPlan.GetCountDescriptions());
			Assert.AreEqual(1,procCodeImportPlan.GetCountAbbrs());
			Assert.AreEqual(1,procCodeImportPlan.GetCountObsolete());
			Assert.IsTrue(procCodeImportPlan.HasWork());
			string summary=ProcCodeImports.GetPlanSummary(procCodeImportPlan,new List<string> { "Line 9: synthetic error" });
			StringAssert.Contains(summary,"Codes to add: 2");
			StringAssert.Contains(summary,"Line 9: synthetic error");
		}

		[TestMethod]
		public void CreatePlan_Cda_AddsOnlyMissingCodes() {
			List<ProcedureCode> listExisting=new List<ProcedureCode> {
				Existing(201,"99961","Synthetic Canadian old text","",1),
			};
			List<ProcCodeImportRow> listRows=new List<ProcCodeImportRow> {
				Row("99961","Synthetic Canadian new text","Abbr"),
				Row("99962","Synthetic Canadian new code"),
				Row("99963","Synthetic Canadian removed",isDeleted:true),
				Row("99961X","Synthetic code with letters"),
			};
			ProcCodeImportPlan procCodeImportPlan=ProcCodeImports.CreatePlan(listRows,listExisting,Cats(),EnumDentalCodeSystem.CDA);
			CollectionAssert.AreEqual(new[] { "99962","99961X" },procCodeImportPlan.ListAdds.Select(x => x.ProcedureCode.ProcCode).ToArray());
			Assert.AreEqual(0,procCodeImportPlan.ListChanges.Count,"Existing CDA codes are never changed.");
			Assert.AreEqual(1,procCodeImportPlan.CountUnchanged);
			Assert.AreEqual(1,procCodeImportPlan.CountDeletedNotPresent);
			Assert.AreEqual(0,procCodeImportPlan.CountNotInCodeSystem);
		}

		[TestMethod]
		public void CreatePlan_NothingToDo() {
			List<ProcedureCode> listExisting=new List<ProcedureCode> { Existing(301,"D9971","Synthetic same","Abbr",1) };
			ProcCodeImportPlan procCodeImportPlan=ProcCodeImports.CreatePlan(new List<ProcCodeImportRow> { Row("D9971","Synthetic same","Other") },
				listExisting,Cats(),EnumDentalCodeSystem.CDT);
			Assert.IsFalse(procCodeImportPlan.HasWork());
			Assert.AreEqual(1,procCodeImportPlan.CountUnchanged);
		}

		[TestMethod]
		public void GetCategoryForNewCode_BlockMajorityElseImported() {
			List<ProcedureCode> listExisting=new List<ProcedureCode> {
				Existing(1,"D9810","a","",2),
				Existing(2,"D9811","b","",2),
				Existing(3,"D9812","c","",1),
				Existing(4,"D9813","d","",4),//hidden categories never count
				Existing(5,"D9814","e","",4),
				Existing(6,"D9814A","f","",1),//other length
			};
			Assert.AreEqual("Synthetic Other",ProcCodeImports.GetCategoryForNewCode("D9819",listExisting,Cats()));
			Assert.AreEqual(ProcCodeImports.CATEGORY_IMPORTED,ProcCodeImports.GetCategoryForNewCode("D9701",listExisting,Cats()));
			Assert.AreEqual(ProcCodeImports.CATEGORY_IMPORTED,ProcCodeImports.GetCategoryForNewCode("D9",listExisting,Cats()));
		}

		[TestMethod]
		public void IsCdtCode_DPlusFourDigits() {
			Assert.IsTrue(ProcCodeImports.IsCdtCode("D0120"));
			Assert.IsFalse(ProcCodeImports.IsCdtCode("d0120"));
			Assert.IsFalse(ProcCodeImports.IsCdtCode("D012"));
			Assert.IsFalse(ProcCodeImports.IsCdtCode("D01201"));
			Assert.IsFalse(ProcCodeImports.IsCdtCode("01202"));
			Assert.IsFalse(ProcCodeImports.IsCdtCode(null));
		}

		[TestMethod]
		public void IsYearEndWindow_October30ThroughDecember31() {
			Assert.IsFalse(ProcCodeImports.IsYearEndWindow(new DateTime(2026,10,29)));
			Assert.IsTrue(ProcCodeImports.IsYearEndWindow(new DateTime(2026,10,30)));
			Assert.IsTrue(ProcCodeImports.IsYearEndWindow(new DateTime(2027,12,31,23,59,0)));
			Assert.IsFalse(ProcCodeImports.IsYearEndWindow(new DateTime(2027,1,1)));
			Assert.IsFalse(ProcCodeImports.IsYearEndWindow(new DateTime(2027,6,15)));
		}

		[TestMethod]
		public void GetLogText_Counts() {
			ProcCodeImportResult procCodeImportResult=new ProcCodeImportResult { CountAdded=3,CountDescriptionsUpdated=2,CountAbbrsFilled=1,CountMovedToObsolete=4 };
			string logText=ProcCodeImports.GetLogText(EnumDentalCodeSystem.CDA,procCodeImportResult);
			StringAssert.StartsWith(logText,"CDA");
			StringAssert.Contains(logText,"3 added");
			StringAssert.Contains(logText,"4 moved to Obsolete");
		}
		#endregion Plan
	}
}
