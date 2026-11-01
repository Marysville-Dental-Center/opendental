// B-PRINT: inserter barcode on printed statements (Open Dental 25.3): Code 39, page content, placement, clear zone and the Billing window's
// print document. Synthetic PDFs only; the setting itself is tested in InserterBarcodeDbTests.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using PdfSharp.Pdf;

namespace BPRINTTests {
	[TestClass]
	public class InserterBarcodeTests {
		///<summary>MSTest v1 has no Assert.ThrowsException.</summary>
		private static void AssertThrows<T>(Action action) where T:Exception {
			try {
				action();
			}
			catch(T) {
				return;
			}
			Assert.Fail("Expected "+typeof(T).Name+".");
		}

		#region Code 39
		[TestMethod]
		public void Code39_Patterns_MatchThePublishedSymbology() {
			//Bar/space modules as Code 39 references print them: 1 = narrow bar, 11 = wide bar, 0 = narrow space, 00 = wide space.
			Dictionary<char,string> dictExpected=new Dictionary<char,string> {
				{'*',"100101101101"},{'0',"101001101101"},{'1',"110100101011"},{'2',"101100101011"},{'3',"110110010101"},
				{'4',"101001101011"},{'5',"110100110101"},{'6',"101100110101"},{'7',"101001011011"},{'8',"110100101101"},
				{'9',"101100101101"},{'A',"110101001011"},{'-',"100101011011"},{'.',"110010101101"},{' ',"100110101101"},
			};
			foreach(KeyValuePair<char,string> pair in dictExpected) {
				Assert.AreEqual(pair.Value,ModulesOfOneChar(pair.Key),"Character '"+pair.Key+"'.");
			}
		}

		///<summary>The modules of one character, cut out of the framed barcode "*c*".</summary>
		private static string ModulesOfOneChar(char c) {
			if(c==Code39Barcode.START_STOP) {
				return Code39Barcode.GetModules("0").Substring(0,12);
			}
			string modules=Code39Barcode.GetModules(c.ToString());
			return modules.Substring(13,12);//start character (12) and the narrow gap (1) come first
		}

		[TestMethod]
		public void Code39_EveryCharacter_HasNineElementsThreeWide() {
			foreach(char c in "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ-. *") {
				string pattern=Code39Barcode.GetPattern(c);
				Assert.AreEqual(9,pattern.Length,"Character '"+c+"'.");
				Assert.AreEqual(3,pattern.Count(x => x=='w'),"Character '"+c+"'.");
			}
		}

		[TestMethod]
		public void Code39_Validity() {
			Assert.IsTrue(Code39Barcode.IsValid("000010010011"));
			Assert.IsFalse(Code39Barcode.IsValid(""));
			Assert.IsFalse(Code39Barcode.IsValid(null));
			Assert.IsFalse(Code39Barcode.IsValid("abc"),"Lower case is not in the symbology.");
			Assert.IsFalse(Code39Barcode.IsValid("1*2"),"The start/stop character cannot be inside the text.");
			AssertThrows<ArgumentException>(() => Code39Barcode.GetModules("a"));
		}

		[TestMethod]
		public void Code39_Bars_FillTheWidthWithQuietZones() {
			List<Tuple<double,double>> listBars=Code39Barcode.GetBars("000010010011",out double totalWidth);
			//14 characters (12 + start + stop) of 6 narrow and 3 wide elements (wide = 3), 13 gaps, and 10 narrow units of quiet zone on each side.
			Assert.AreEqual(14*(6+3*3)+13+20,totalWidth,0.0001);
			Assert.AreEqual(14*5,listBars.Count,"Five bars per character.");
			Assert.AreEqual(Code39Barcode.QUIET_ZONE,listBars[0].Item1,0.0001,"The first bar starts after the quiet zone.");
			Tuple<double,double> tupleLast=listBars[listBars.Count-1];
			Assert.AreEqual(totalWidth-Code39Barcode.QUIET_ZONE,tupleLast.Item1+tupleLast.Item2,0.0001,"The last bar ends before the quiet zone.");
		}
		#endregion Code 39

		#region Content
		[TestMethod]
		public void Content_IsSequencePagePageCountAndEndOfSet() {
			Assert.AreEqual("000070010020",StatementInserterBarcode.GetContent(7,1,2));
			Assert.AreEqual("000070020021",StatementInserterBarcode.GetContent(7,2,2));
			Assert.AreEqual("000010010011",StatementInserterBarcode.GetContent(1,1,1));
			Assert.AreEqual("999991231231",StatementInserterBarcode.GetContent(99999,123,123));
			Assert.AreEqual(12,StatementInserterBarcode.GetContent(12345,3,9).Length);
			Assert.IsTrue(Code39Barcode.IsValid(StatementInserterBarcode.GetContent(5,1,1)));
		}

		[TestMethod]
		public void Content_WrapsTheSequenceAndCapsPages() {
			Assert.AreEqual("000000010011",StatementInserterBarcode.GetContent(100000,1,1));
			Assert.AreEqual("000019999991",StatementInserterBarcode.GetContent(1,1000,1000));
		}

		[TestMethod]
		public void Content_RefusesPagesOutsideTheStatement() {
			AssertThrows<ArgumentOutOfRangeException>(() => StatementInserterBarcode.GetContent(1,0,2));
			AssertThrows<ArgumentOutOfRangeException>(() => StatementInserterBarcode.GetContent(1,3,2));
			AssertThrows<ArgumentOutOfRangeException>(() => StatementInserterBarcode.GetContent(0,1,1));
		}
		#endregion Content

		#region Placement
		private static SheetFieldDef BarcodeDef(int x,int y,int width=200,int height=25) {
			return SheetFieldDef.NewSpecial(StatementInserterBarcode.FIELD_NAME,x,y,width,height);
		}

		private static SheetFieldDef Text(string text,int x,int y,int width,int height) {
			return SheetFieldDef.NewStaticText(text,9,"Arial",false,x,y,width,height);
		}

		[TestMethod]
		public void DefaultSpot_IsTheTopRightMargin() {
			Rectangle rectangle=StatementInserterBarcode.GetDefaultBounds(850);
			Assert.AreEqual(new Rectangle(850-25-200,16,200,23),rectangle);
			Assert.IsTrue(rectangle.Bottom<40,"Inside the statement's 0.4 inch top margin.");
		}

		[TestMethod]
		public void LayoutField_SetsThePlace_WhenItFitsOnThePage() {
			List<SheetFieldDef> listDefs=new List<SheetFieldDef> { Text("Hello",50,50,100,20),BarcodeDef(400,1000) };
			Rectangle? rectangleField=StatementInserterBarcode.GetFieldBounds(listDefs);
			Assert.AreEqual(new Rectangle(400,1000,200,25),rectangleField);
			Assert.AreEqual(new Rectangle(400,1000,200,25),StatementInserterBarcode.GetPageBounds(rectangleField,850,1100));
			Assert.AreEqual(StatementInserterBarcode.GetDefaultBounds(850),StatementInserterBarcode.GetPageBounds(new Rectangle(400,1090,200,25),850,1100),
				"A field past the first page falls back to the default spot.");
			Assert.AreEqual(StatementInserterBarcode.GetDefaultBounds(850),StatementInserterBarcode.GetPageBounds(null,850,1100));
			Assert.IsNull(StatementInserterBarcode.GetFieldBounds(new List<SheetFieldDef> { SheetFieldDef.NewSpecial("toothChart",0,0,500,370) }));
			Assert.IsNull(StatementInserterBarcode.GetFieldBounds(new List<SheetFieldDef> { BarcodeDef(10,10,0,25) }),"A field without area is ignored.");
		}

		[TestMethod]
		public void SpecialField_IsOfferedOnStatementLayouts() {
			List<SheetFieldDef> listSpecial=SheetFieldsAvailable.GetSpecial(SheetTypeEnum.Statement,SheetFieldLayoutMode.Default);
			SheetFieldDef sheetFieldDef=listSpecial.Single(x => x.FieldName==StatementInserterBarcode.FIELD_NAME);
			Assert.AreEqual(SheetFieldType.Special,sheetFieldDef.FieldType);
			Assert.AreEqual(StatementInserterBarcode.DEFAULT_WIDTH,sheetFieldDef.Width);
			Assert.AreEqual(StatementInserterBarcode.DEFAULT_HEIGHT,sheetFieldDef.Height);
			Assert.IsTrue(SheetDefs.GetVisibleButtons(SheetTypeEnum.Statement).Contains(SheetFieldType.Special),"The sheet editor shows the Special button.");
			Assert.IsFalse(SheetFieldsAvailable.GetSpecial(SheetTypeEnum.PatientForm,SheetFieldLayoutMode.Default).Any(x => x.FieldName==StatementInserterBarcode.FIELD_NAME));
			Assert.IsFalse(SheetDefs.GetVisibleButtons(SheetTypeEnum.PatientForm).Contains(SheetFieldType.Special),"Other sheet types are unchanged.");
		}

		[TestMethod]
		public void ClearZone_ListsFieldsThatTouchTheBarcode() {
			SheetFieldDef sheetFieldDefBarcode=BarcodeDef(600,10);
			SheetFieldDef sheetFieldDefOverlap=Text("Title",650,20,100,20);
			SheetFieldDef sheetFieldDefClear=Text("Far away",50,500,100,20);
			SheetFieldDef sheetFieldDefLine=SheetFieldDef.NewLine(500,22,300,0);//horizontal line through the barcode, no height
			SheetFieldDef sheetFieldDefParameter=SheetFieldDef.NewSpecial("unused",0,0,0,0);
			sheetFieldDefParameter.FieldType=SheetFieldType.Parameter;
			sheetFieldDefParameter.XPos=600;
			sheetFieldDefParameter.YPos=10;
			sheetFieldDefParameter.Width=50;
			sheetFieldDefParameter.Height=50;
			SheetFieldDef sheetFieldDefOtherLanguage=Text("Titulo",650,20,100,20);
			sheetFieldDefOtherLanguage.Language="spa";
			SheetDef sheetDef=new SheetDef { SheetType=SheetTypeEnum.Statement,Width=850,Height=1100,
				SheetFieldDefs=new List<SheetFieldDef> { sheetFieldDefBarcode,sheetFieldDefOverlap,sheetFieldDefClear,sheetFieldDefLine,sheetFieldDefParameter,
					sheetFieldDefOtherLanguage } };
			List<SheetFieldDef> listOverlaps=StatementInserterBarcode.GetOverlappingFields(sheetDef);
			CollectionAssert.AreEquivalent(new List<SheetFieldDef> { sheetFieldDefOverlap,sheetFieldDefLine },listOverlaps);
			sheetDef.SheetFieldDefs.Remove(sheetFieldDefBarcode);
			Assert.AreEqual(0,StatementInserterBarcode.GetOverlappingFields(sheetDef).Count,"No barcode field, nothing to warn about.");
			Assert.AreEqual(0,StatementInserterBarcode.GetOverlappingFields(null).Count);
		}
		#endregion Placement

		#region Drawing and the print document
		[TestMethod]
		public void Draw_PaintsBarsInsideTheRectangleOnly() {
			using(Bitmap bitmap=new Bitmap(300,60))
			using(Graphics g=Graphics.FromImage(bitmap)) {
				g.Clear(Color.Red);
				StatementInserterBarcode.Draw(g,new RectangleF(20,10,243,30),StatementInserterBarcode.GetContent(1,1,1));
				Assert.AreEqual(Color.Red.ToArgb(),bitmap.GetPixel(5,5).ToArgb(),"Outside is untouched.");
				Assert.AreEqual(Color.White.ToArgb(),bitmap.GetPixel(25,25).ToArgb(),"The quiet zone is white.");
				int countBlack=0;
				for(int x=20;x<263;x++) {
					if(bitmap.GetPixel(x,25).ToArgb()==Color.Black.ToArgb()) {
						countBlack++;
					}
				}
				Assert.IsTrue(countBlack>60,"Bars are drawn across the middle row.");
			}
		}

		private static PdfDocument NewDocument(int pageCount) {
			PdfDocument pdfDocument=new PdfDocument();
			for(int i=0;i<pageCount;i++) {
				pdfDocument.AddPage();
			}
			return pdfDocument;
		}

		private static int ContentLength(PdfPage pdfPage) {
			return pdfPage.Contents.Elements.Count;
		}

		[TestMethod]
		public void PrintDocument_EveryPageOfEveryCountedStatementIsStamped() {
			PdfDocument pdfDocument=NewDocument(5);
			int[] arrayBefore=Enumerable.Range(0,5).Select(i => ContentLength(pdfDocument.Pages[i])).ToArray();
			int countStamped=StatementInserterBarcode.StampPrintDocument(pdfDocument,new List<int> { 2,3 });
			Assert.AreEqual(5,countStamped);
			for(int i=0;i<5;i++) {
				Assert.IsTrue(ContentLength(pdfDocument.Pages[i])>arrayBefore[i],"Page "+(i+1)+" got the barcode drawing.");
			}
		}

		[TestMethod]
		public void PrintDocument_NeverStampsPagesItCannotAccountFor() {
			PdfDocument pdfDocument=NewDocument(4);
			int contentBefore=ContentLength(pdfDocument.Pages[3]);
			//The first statement has 3 pages, the second claims 2 but only 1 is left: the second is not stamped at all.
			Assert.AreEqual(3,StatementInserterBarcode.StampPrintDocument(pdfDocument,new List<int> { 3,2 }));
			Assert.AreEqual(contentBefore,ContentLength(pdfDocument.Pages[3]));
			Assert.AreEqual(0,StatementInserterBarcode.StampPrintDocument(null,new List<int> { 1 }));
			Assert.AreEqual(0,StatementInserterBarcode.StampPrintDocument(NewDocument(1),null));
			Assert.AreEqual(1,StatementInserterBarcode.StampPrintDocument(NewDocument(1),new List<int> { 0,1 }),"A statement without pages is skipped.");
		}

		[TestMethod]
		public void PrintDocument_UsesEachStatementsLayout() {
			PdfDocument pdfDocument=NewDocument(2);
			List<IEnumerable<SheetFieldDef>> listLayouts=new List<IEnumerable<SheetFieldDef>> { new List<SheetFieldDef> { BarcodeDef(50,1000) },null };
			Assert.AreEqual(2,StatementInserterBarcode.StampPrintDocument(pdfDocument,new List<int> { 1,1 },listLayouts));
			Size sizePage=StatementInserterBarcode.GetPageSizeSheetUnits(pdfDocument.Pages[0]);
			Assert.AreEqual(850,sizePage.Width,1,"A default PDF page is US Letter.");
			Assert.AreEqual(1100,sizePage.Height,1);
		}

		[TestMethod]
		public void Batch_PrintedStatementsComeFromTheSentList_InOrder() {
			SendStatementsIO sendStatementsIO=new SendStatementsIO();
			StatementBatch statementBatch=new StatementBatch();
			statementBatch.ListStatements.Add(new Statement { StatementNum=1,Mode_=StatementMode.Mail });
			statementBatch.ListStatements.Add(new Statement { StatementNum=2,Mode_=StatementMode.Email });
			statementBatch.ListStatements.Add(new Statement { StatementNum=3,Mode_=StatementMode.InPerson });
			sendStatementsIO.ListStatementBatches.Add(statementBatch);
			sendStatementsIO.CurStatementBatch=statementBatch;
			sendStatementsIO.ListStatementNumsSent.AddRange(new long[] { 3,1,2 });
			List<Statement> listPrinted=StatementInserterBarcodeBatch.GetPrintedStatements(sendStatementsIO);
			CollectionAssert.AreEqual(new long[] { 3,1 },listPrinted.Select(x => x.StatementNum).ToArray());
			sendStatementsIO.ListStatementNumsSent.Add(99);
			Assert.IsNull(StatementInserterBarcodeBatch.GetPrintedStatements(sendStatementsIO),"A statement the batches do not know: no matching.");
		}

		[TestMethod]
		public void Batch_DoesNothingWhileTheBarcodeIsOff() {
			//Off: without a database the preference cache is empty, and the database tests turn the barcode off again when they finish.
			SendStatementsIO sendStatementsIO=new SendStatementsIO();
			Func<string,string,PdfDocument> funcGetPdfDocument=(rawBase64,path) => NewDocument(2);
			sendStatementsIO.FuncGetPdfDocument=funcGetPdfDocument;
			StatementInserterBarcodeBatch statementInserterBarcodeBatch=StatementInserterBarcodeBatch.Attach(sendStatementsIO);
			Assert.AreSame(funcGetPdfDocument,sendStatementsIO.FuncGetPdfDocument,"The callback is left alone.");
			sendStatementsIO.PdfMasterDocument=NewDocument(2);
			Assert.AreEqual(0,statementInserterBarcodeBatch.Stamp(sendStatementsIO));
		}
		#endregion Drawing and the print document
	}
}
