// A statement made into a PDF by the sheet code on PDFsharp 6.5, from a scratch database with one synthetic family; and the statement batch
// merging statement PDFs as BillingL does. The database (127.0.0.1:3306, root, blank password, name starting test_libpdf_) is built from the
// seed dump and the frozen convert scripts, and dropped afterwards; the tests are inconclusive when the server does not answer or when
// LIBPDF_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using PdfSharp.Pdf;

namespace PdfLibraryTests {
	[TestClass]
	public class StatementPdfDbTests {
		private static readonly string DB_NAME=TestDb.ScratchName("stmt");
		private static string _skipReason;
		private static Patient _patient;
		private static long _provNum;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=TestDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			TestDb.DropStaleScratchDatabases();
			TestDb.CreateMigrated(DB_NAME);
			Security.CurUser=Security.CurUser??new Userod();
			_provNum=OpenPmsDentalBusiness.Crud.ProviderCrud.Insert(new Provider { Abbr="LIBDOC",FName="Libpdf",LName="Doctor" });
			long codeNum=OpenPmsDentalBusiness.Crud.ProcedureCodeCrud.Insert(new ProcedureCode { ProcCode="Z9301",Descript="Synthetic cleaning",
				AbbrDesc="SynCl",TreatArea=TreatmentArea.Mouth,ProcCat=Defs.GetDefsForCategory(DefCat.ProcCodeCats,true).First().DefNum });
			Cache.Refresh(InvalidType.ProcCodes,InvalidType.Providers);
			Patient patient=new Patient();
			patient.LName="Libpdftest";
			patient.FName="Ann";
			patient.PatStatus=PatientStatus.Patient;
			patient.Address="1 Test Street";
			patient.City="Testville";
			patient.State="OR";
			patient.Zip="97000";
			patient.BillingType=PrefC.GetLong(PrefName.PracticeDefaultBillType);
			patient.Birthdate=new DateTime(1980,1,1);
			patient.PriProv=_provNum;
			patient.PatNum=OpenPmsDentalBusiness.Crud.PatientCrud.Insert(patient);
			Patient patientOld=patient.Copy();
			patient.Guarantor=patient.PatNum;
			OpenPmsDentalBusiness.Crud.PatientCrud.Update(patient,patientOld);
			_patient=patient;
			for(int i=0;i<3;i++) {
				Procedure procedure=new Procedure();
				procedure.PatNum=patient.PatNum;
				procedure.CodeNum=codeNum;
				procedure.ProcDate=DateTime.Today.AddDays(-30+i);
				procedure.DateEntryC=procedure.ProcDate;
				procedure.ProcFee=100+i;
				procedure.UnitQty=1;
				procedure.ProcStatus=ProcStat.C;
				procedure.ProvNum=_provNum;
				procedure.ToothNum="";
				procedure.Surf="";
				procedure.ToothRange="";
				OpenPmsDentalBusiness.Crud.ProcedureCrud.Insert(procedure);
			}
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null || Environment.GetEnvironmentVariable("LIBPDF_KEEP_DB")=="1") {
				return;
			}
			try {
				TestDb.Drop(DB_NAME);
			}
			catch(Exception) {
				//A later run drops it once it is stale.
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			TestDb.Connect(DB_NAME);
			Security.CurUser=Security.CurUser??new Userod();
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			Thread.CurrentThread.CurrentUICulture=new CultureInfo("en-US");
		}

		private static Statement NewStatement() {
			Statement statement=new Statement();
			statement.PatNum=_patient.PatNum;
			statement.DateSent=DateTime.Today;
			statement.DateRangeFrom=DateTime.MinValue;
			statement.DateRangeTo=new DateTime(2200,1,1);
			statement.Mode_=StatementMode.InPerson;
			statement.StatementType=StmtType.NotSet;
			statement.Note="Statement note LIBPDF";
			statement.NoteBold="";
			statement.IsBalValid=true;
			statement.Intermingled=true;
			return statement;
		}

		///<summary>The statement as the program makes it: the statement sheet, filled, made into a PDF (SheetDrawingJob.CreatePdf).</summary>
		private static PdfDocument CreateStatementPdf(Statement statement) {
			DataSet dataSet=AccountModules.GetAccount(Statements.GetPatNumForGetAccount(statement),statement,doShowHiddenPaySplits:statement.IsReceipt);
			SheetDef sheetDef=SheetUtil.GetStatementSheetDef(statement);
			Sheet sheet=SheetUtil.CreateSheet(sheetDef,statement.PatNum,statement.HidePayment);
			sheet.Parameters.Add(new SheetParameter(true,"Statement") { ParamValue=statement });
			SheetFiller.FillFields(sheet,dataSet,statement);
			SheetUtil.CalculateHeights(sheet,dataSet,statement);
			return new SheetDrawingJob().CreatePdf(sheet,statement,null,dataSet,null,null);
		}

		[TestMethod]
		public void Statement_IsALetterPdf_WithThePatientAndTheProcedures() {
			Statement statement=NewStatement();
			statement.StatementNum=Statements.Insert(statement);
			try {
				PdfDocument pdfDocument=PdfRead.Open(PdfRead.Save(CreateStatementPdf(statement)));
				Assert.IsTrue(pdfDocument.PageCount>=1);
				Assert.AreEqual("612 x 792",PdfRead.Size(pdfDocument.Pages[0]));
				string text=string.Concat(Enumerable.Range(0,pdfDocument.PageCount).Select(x => PdfRead.ExtractText(pdfDocument.Pages[x])));
				StringAssert.Contains(text,"Libpdftest");
				StringAssert.Contains(text,"Synthetic cleaning");
				StringAssert.Contains(text,"Statement note LIBPDF");
				StringAssert.Contains(pdfDocument.Info.Producer,"PDFsharp 6.5.0");
			}
			finally {
				DataCore.NonQ("DELETE FROM statement WHERE StatementNum="+POut.Long(statement.StatementNum));
			}
		}

		[TestMethod]
		public void StatementBatch_MergesStatementPdfs_ReadBackFromTheirFiles() {
			Statement statementA=NewStatement();
			statementA.StatementNum=Statements.Insert(statementA);
			Statement statementB=NewStatement();
			statementB.StatementNum=Statements.Insert(statementB);
			try {
				byte[] arrayBytesA=PdfRead.Save(CreateStatementPdf(statementA));
				byte[] arrayBytesB=PdfRead.Save(CreateStatementPdf(statementB));
				int pageCountA=PdfRead.Open(arrayBytesA).PageCount;
				int pageCountB=PdfRead.Open(arrayBytesB).PageCount;
				//As BillingL.PrintBatch: the master document gets every page of each statement, read for import from its saved bytes.
				PdfDocument pdfDocumentMaster=new PdfDocument();
				foreach(byte[] arrayBytes in new[] { arrayBytesA,arrayBytesB }) {
					PdfDocument pdfDocumentInput=PdfRead.Open(arrayBytes,PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
					for(int i=0;i<pdfDocumentInput.PageCount;i++) {
						pdfDocumentMaster.AddPage(pdfDocumentInput.Pages[i]);
					}
				}
				PdfDocument pdfDocumentRead=PdfRead.Open(PdfRead.Save(pdfDocumentMaster));
				Assert.AreEqual(pageCountA+pageCountB,pdfDocumentRead.PageCount);
				StringAssert.Contains(PdfRead.ExtractText(pdfDocumentRead.Pages[0]),"Libpdftest");
				StringAssert.Contains(PdfRead.ExtractText(pdfDocumentRead.Pages[pageCountA]),"Libpdftest");
			}
			finally {
				DataCore.NonQ("DELETE FROM statement WHERE StatementNum IN ("+POut.Long(statementA.StatementNum)+","+POut.Long(statementB.StatementNum)+")");
			}
		}
	}
}
