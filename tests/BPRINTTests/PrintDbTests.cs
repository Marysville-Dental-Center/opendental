// B-PRINT database tests: the inserter barcode setting (a fork preference row written the first time it is turned on), the barcode on printed
// statement pages and on the Billing window's print document, and the "Copy to Imaging when saved" option of a sheet layout. Synthetic data.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using PdfSharp.Pdf;

namespace BPRINTTests {
	[TestClass]
	public class PrintDbTests {
		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			BPrintDb.EnsureBuilt();
		}

		[TestInitialize]
		public void TestInitialize() {
			BPrintDb.Begin();
			DeleteBarcodeRow();
		}

		[TestCleanup]
		public void TestCleanup() {
			try {
				DeleteBarcodeRow();//Leaves the barcode off for the tests without a database that run later in this process.
				Prefs.UpdateString(PrefName.SheetsDefaultStatement,"0");
				BPrintDb.RefreshCaches();
			}
			catch(Exception) {
				//The database was not built (inconclusive test).
			}
		}

		private static void DeleteBarcodeRow() {
			DataCore.NonQ("DELETE FROM preference WHERE PrefName='"+StatementInserterBarcode.PREF_NAME+"'");
			Cache.Refresh(InvalidType.Prefs);
		}

		private static string BarcodeRowValue() {
			DataTable table=DataCore.GetTable("SELECT ValueString FROM preference WHERE PrefName='"+StatementInserterBarcode.PREF_NAME+"'");
			if(table.Rows.Count==0) {
				return null;
			}
			Assert.AreEqual(1,table.Rows.Count,"One row only.");
			return table.Rows[0][0].ToString();
		}

		[TestMethod]
		public void Setting_IsOffWithoutTheRow_AndTheRowIsWrittenWhenTurnedOn() {
			Assert.IsFalse(StatementInserterBarcode.IsEnabled(),"Off by default: an adopted or fresh database has no row.");
			Assert.IsFalse(StatementInserterBarcode.SetEnabled(false),"Turning it off again changes nothing...");
			Assert.IsNull(BarcodeRowValue(),"...and writes no row.");
			Assert.IsTrue(StatementInserterBarcode.SetEnabled(true));
			Assert.AreEqual("1",BarcodeRowValue());
			Assert.IsTrue(StatementInserterBarcode.IsEnabled(),"This workstation's cache follows at once.");
			Cache.Refresh(InvalidType.Prefs);
			Assert.IsTrue(StatementInserterBarcode.IsEnabled(),"Other workstations read it after the Prefs signal.");
			Assert.IsFalse(StatementInserterBarcode.SetEnabled(true));
			Assert.IsTrue(StatementInserterBarcode.SetEnabled(false));
			Assert.AreEqual("0",BarcodeRowValue());
			Cache.Refresh(InvalidType.Prefs);
			Assert.IsFalse(StatementInserterBarcode.IsEnabled());
		}

		private static int CountBlack(Bitmap bitmap,Rectangle rectangle) {
			int count=0;
			for(int x=rectangle.Left;x<rectangle.Right;x++) {
				if(bitmap.GetPixel(x,rectangle.Top+rectangle.Height/2).ToArgb()==Color.Black.ToArgb()) {
					count++;
				}
			}
			return count;
		}

		[TestMethod]
		public void PrintedPage_GetsTheBarcode_OnlyWhenOn_AndOnlyOnStatements() {
			Sheet sheetStatement=new Sheet { SheetType=SheetTypeEnum.Statement,Width=850,Height=1100,SheetFields=new List<SheetField>() };
			Rectangle rectangleDefault=StatementInserterBarcode.GetDefaultBounds(850);
			using(Bitmap bitmap=new Bitmap(850,1100))
			using(Graphics g=Graphics.FromImage(bitmap)) {
				g.Clear(Color.White);
				Assert.IsFalse(StatementInserterBarcode.DrawOnPrintedPage(g,sheetStatement,1,1,2),"Off.");
				Assert.AreEqual(0,CountBlack(bitmap,rectangleDefault));
				StatementInserterBarcode.SetEnabled(true);
				Assert.IsFalse(StatementInserterBarcode.DrawOnPrintedPage(g,new Sheet { SheetType=SheetTypeEnum.PatientForm,Width=850,Height=1100,
					SheetFields=new List<SheetField>() },1,1,1),"Only statements.");
				Assert.IsTrue(StatementInserterBarcode.DrawOnPrintedPage(g,sheetStatement,1,1,2));
				Assert.IsTrue(CountBlack(bitmap,rectangleDefault)>40,"Bars in the top-right margin.");
			}
			//A layout field moves it.
			sheetStatement.SheetFields.Add(new SheetField { FieldType=SheetFieldType.Special,FieldName=StatementInserterBarcode.FIELD_NAME,
				XPos=100,YPos=600,Width=200,Height=25 });
			using(Bitmap bitmap=new Bitmap(850,1100))
			using(Graphics g=Graphics.FromImage(bitmap)) {
				g.Clear(Color.White);
				Assert.IsTrue(StatementInserterBarcode.DrawOnPrintedPage(g,sheetStatement,3,2,2));
				Assert.AreEqual(0,CountBlack(bitmap,rectangleDefault));
				Assert.IsTrue(CountBlack(bitmap,new Rectangle(100,600,200,25))>40);
			}
		}

		private static PdfDocument NewDocument(int pageCount) {
			PdfDocument pdfDocument=new PdfDocument();
			for(int i=0;i<pageCount;i++) {
				pdfDocument.AddPage();
			}
			return pdfDocument;
		}

		[TestMethod]
		public void BillingPrintDocument_IsStampedPerStatement_WithItsLayout() {
			//A custom statement layout with the barcode field, made the default statement layout.
			SheetDef sheetDef=new SheetDef { IsNew=true,Description="Synthetic statement",SheetType=SheetTypeEnum.Statement,FontName="Arial",FontSize=9,
				Width=850,Height=1100,PageCount=1,SheetFieldDefs=new List<SheetFieldDef> {
					SheetFieldDef.NewSpecial(StatementInserterBarcode.FIELD_NAME,50,500,StatementInserterBarcode.DEFAULT_WIDTH,StatementInserterBarcode.DEFAULT_HEIGHT),
				} };
			SheetDefs.InsertOrUpdate(sheetDef);
			Prefs.UpdateString(PrefName.SheetsDefaultStatement,sheetDef.SheetDefNum.ToString());
			BPrintDb.RefreshCaches();
			try {
				StampWithLayout(sheetDef);
			}
			finally {
				Prefs.UpdateString(PrefName.SheetsDefaultStatement,"0");//A default sheet cannot be deleted.
				BPrintDb.RefreshCaches();
				SheetDefs.DeleteObject(sheetDef.SheetDefNum);
			}
		}

		private static void StampWithLayout(SheetDef sheetDef) {
			Statement statementMail=new Statement { StatementNum=1001,Mode_=StatementMode.Mail };
			Assert.AreEqual(sheetDef.SheetDefNum,SheetUtil.GetStatementSheetDef(statementMail).SheetDefNum);
			Assert.AreEqual(new Rectangle(50,500,200,25),StatementInserterBarcode.GetFieldBounds(SheetUtil.GetStatementSheetDef(statementMail).SheetFieldDefs),
				"The statement's layout is where its barcode goes.");
			StatementInserterBarcode.SetEnabled(true);
			//What BillingL.PrintBatch does for printed statements: get the statement's PDF, add its pages, mark it sent. E-mail adds no pages.
			SendStatementsIO sendStatementsIO=new SendStatementsIO();
			Dictionary<long,int> dictPageCounts=new Dictionary<long,int> { { 1001,2 },{ 1003,3 } };
			long statementNumCur=0;
			sendStatementsIO.FuncGetPdfDocument=(rawBase64,savedPdfPath) => NewDocument(dictPageCounts[statementNumCur]);
			StatementBatch statementBatch=new StatementBatch();
			statementBatch.ListStatements.Add(statementMail);
			statementBatch.ListStatements.Add(new Statement { StatementNum=1002,Mode_=StatementMode.Email });
			statementBatch.ListStatements.Add(new Statement { StatementNum=1003,Mode_=StatementMode.InPerson });
			sendStatementsIO.ListStatementBatches.Add(statementBatch);
			sendStatementsIO.CurStatementBatch=statementBatch;
			StatementInserterBarcodeBatch statementInserterBarcodeBatch=StatementInserterBarcodeBatch.Attach(sendStatementsIO);
			foreach(Statement statement in statementBatch.ListStatements) {
				if(statement.Mode_.In(StatementMode.Mail,StatementMode.InPerson)) {
					statementNumCur=statement.StatementNum;
					sendStatementsIO.PdfMasterDocument=sendStatementsIO.PdfMasterDocument??new PdfDocument();
					PdfDocument pdfDocumentInput=sendStatementsIO.FuncGetPdfDocument("","");
					for(int i=0;i<pdfDocumentInput.PageCount;i++) {
						sendStatementsIO.PdfMasterDocument.AddPage();
					}
				}
				sendStatementsIO.ListStatementNumsSent.Add(statement.StatementNum);
			}
			CollectionAssert.AreEqual(new List<int> { 2,3 },statementInserterBarcodeBatch.ListPageCounts);
			Assert.AreEqual(5,statementInserterBarcodeBatch.Stamp(sendStatementsIO));
			StatementInserterBarcode.SetEnabled(false);
			Assert.AreEqual(5,statementInserterBarcodeBatch.Stamp(sendStatementsIO),"The run keeps the setting it started with.");
		}

		[TestMethod]
		public void LayoutOption_CopyToImagingWhenSaved_IsReadFromTheSheetsLayout() {
			SheetDef sheetDefOn=new SheetDef { IsNew=true,Description="Synthetic form on",SheetType=SheetTypeEnum.PatientForm,FontName="Arial",FontSize=9,
				Width=850,Height=1100,PageCount=1,AutoCheckSaveImage=true,SheetFieldDefs=new List<SheetFieldDef>() };
			SheetDef sheetDefOff=new SheetDef { IsNew=true,Description="Synthetic form off",SheetType=SheetTypeEnum.PatientForm,FontName="Arial",FontSize=9,
				Width=850,Height=1100,PageCount=1,AutoCheckSaveImage=false,SheetFieldDefs=new List<SheetFieldDef>() };
			SheetDefs.InsertOrUpdate(sheetDefOn);
			SheetDefs.InsertOrUpdate(sheetDefOff);
			BPrintDb.RefreshCaches();
			try {
				Assert.IsTrue(SheetAutoSaveImages.IsLayoutOptionOn(new Sheet { SheetDefNum=sheetDefOn.SheetDefNum }));
				Assert.IsFalse(SheetAutoSaveImages.IsLayoutOptionOn(new Sheet { SheetDefNum=sheetDefOff.SheetDefNum }));
				Assert.IsFalse(SheetAutoSaveImages.IsLayoutOptionOn(new Sheet { SheetDefNum=sheetDefOff.SheetDefNum+1000 }),"A deleted layout.");
			}
			finally {
				SheetDefs.DeleteObject(sheetDefOn.SheetDefNum);
				SheetDefs.DeleteObject(sheetDefOff.SheetDefNum);
				BPrintDb.RefreshCaches();
			}
		}
	}
}
