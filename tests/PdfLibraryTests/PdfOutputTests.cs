// Representative PDFs on PDFsharp/MigraDoc 6.5, each saved, opened again with PdfReader and checked for page count, page size and text: a sheet,
// MigraDoc documents with a table, an image and a chart (the code paths of treatment plans, statements and receipts), a merged and a split PDF,
// images, fonts that are and are not installed, and MigraDoc printing in a print preview.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Shapes;
using MigraDoc.DocumentObjectModel.Shapes.Charts;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;
using OpenPmsDentalBusiness;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Color=System.Drawing.Color;
using HorizontalAlignment=System.Windows.Forms.HorizontalAlignment;
using Orientation=MigraDoc.DocumentObjectModel.Orientation;

namespace PdfLibraryTests {
	[TestClass]
	public class PdfOutputTests {
		[AssemblyInitialize]
		public static void AssemblyInitialize(TestContext testContext) {
			//As every program does at start, before any text is drawn (the font fallback must come first).
			PdfSharpSetup.Init();
		}

		[TestInitialize]
		public void TestInitialize() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			Thread.CurrentThread.CurrentUICulture=new CultureInfo("en-US");
		}

		#region Sheets
		[TestMethod]
		public void Sheet_PatientForm_OneLetterPageWithItsText() {
			RequireFonts("Arial");
			Sheet sheet=NewSheet(850,1100,isLandscape:false,
				TextField("Patient form LIBPDF",50,50),TextField("Second line, right aligned",50,120,HorizontalAlignment.Right),
				new SheetField { FieldType=SheetFieldType.Rectangle,XPos=40,YPos=40,Width=500,Height=200,ItemColor=Color.Black,FieldValue="",
					FontName="",RadioButtonValue="",RadioButtonGroup="",ReportableName="",UiLabelMobile="",UiLabelMobileRadioButton="" });
			PdfDocument pdfDocument=PdfRead.Open(PdfRead.Save(new SheetDrawingJob().CreatePdf(sheet)));
			Assert.AreEqual(1,pdfDocument.PageCount);
			Assert.AreEqual("612 x 792",PdfRead.Size(pdfDocument.Pages[0]),"850 x 1100 sheet pixels (1/100 inch) are a letter page.");
			string text=PdfRead.ExtractText(pdfDocument.Pages[0]);
			Assert.IsTrue(PdfRead.ContainsText(text,"Patient form LIBPDF"),text);
			Assert.IsTrue(PdfRead.ContainsText(text,"Second line, right aligned"),text);
			Assert.IsTrue(PdfRead.GetOperatorNames(pdfDocument.Pages[0]).Contains("re"),"The rectangle is drawn.");
		}

		[TestMethod]
		public void Sheet_Landscape_IsALandscapePage() {
			RequireFonts("Arial");
			//A sheet keeps the portrait size in Width and Height; IsLandscape turns it (Sheet.WidthPage).
			Sheet sheet=NewSheet(850,1100,isLandscape:true,TextField("Landscape LIBPDF",50,50));
			PdfDocument pdfDocument=PdfRead.Open(PdfRead.Save(new SheetDrawingJob().CreatePdf(sheet)));
			Assert.AreEqual(1,pdfDocument.PageCount);
			Assert.AreEqual("792 x 612",PdfRead.Size(pdfDocument.Pages[0]));
			Assert.IsTrue(PdfRead.ContainsText(PdfRead.ExtractText(pdfDocument.Pages[0]),"Landscape LIBPDF"),PdfRead.ExtractText(pdfDocument.Pages[0]));
		}

		[TestMethod]
		public void Sheet_FontThatIsNotInstalled_FallsBackToMicrosoftSansSerif() {
			//PDFsharp 1.2 drew such text in the font GDI+ substitutes, Microsoft Sans Serif; PDFsharp 6.5 would throw without PdfSharpSetup's
			//fallback.
			RequireFonts(PdfSharpSetup.FALLBACK_FONT_FAMILY);
			Sheet sheet=NewSheet(850,1100,isLandscape:false,TextField("Missing font LIBPDF",50,50,fontName:"No Such Font LIBPDF"));
			PdfDocument pdfDocument=PdfRead.Open(PdfRead.Save(new SheetDrawingJob().CreatePdf(sheet)));
			Assert.IsTrue(PdfRead.ContainsText(PdfRead.ExtractText(pdfDocument.Pages[0]),"Missing font LIBPDF"),PdfRead.ExtractText(pdfDocument.Pages[0]));
			Assert.IsTrue(PdfRead.GetFonts(pdfDocument.Pages[0]).Values.Any(x => x.Replace(" ","").Contains("MicrosoftSansSerif")),
				string.Join(", ",PdfRead.GetFonts(pdfDocument.Pages[0]).Values));
		}
		#endregion Sheets

		#region Fonts
		[TestMethod]
		public void Fonts_InstalledFontsResolveToThemselves_MissingOnesToTheFallback() {
			RequireFonts("Arial","Times New Roman",PdfSharpSetup.FALLBACK_FONT_FAMILY);
			Assert.AreEqual("Arial",new XFont("Arial",10).FontFamily.Name);
			XFont xFontBold=new XFont("Arial",10,XFontStyleEx.Bold);
			Assert.IsTrue(xFontBold.Bold);
			Assert.AreEqual("Times New Roman",new XFont("Times New Roman",10,XFontStyleEx.Italic).FontFamily.Name);
			Assert.AreEqual(PdfSharpSetup.FALLBACK_FONT_FAMILY,new XFont("No Such Font LIBPDF",10).FontFamily.Name);
			Assert.AreEqual(PdfSharpSetup.FALLBACK_FONT_FAMILY,new XFont("No Such Font LIBPDF",10,XFontStyleEx.BoldItalic).FontFamily.Name);
			Assert.IsTrue(SheetFonts.CanEmbedInPdf("Arial",XFontStyleEx.Regular));
		}

		[TestMethod]
		public void Fonts_AreEmbeddedAsSubsets() {
			//PDFsharp 1.2 embedded a subset of each font; PdfFontEmbedding.Always now means the whole font file, which the program does not ask for.
			RequireFonts("Arial");
			PdfDocument pdfDocument=new PdfDocument();
			using(XGraphics xGraphics=XGraphics.FromPdfPage(pdfDocument.AddPage())) {
				xGraphics.DrawString("Subset",new XFont("Arial",12,XFontStyleEx.Regular,SheetFonts.GetPdfFontOptionsUnicode()),XBrushes.Black,72,72);
			}
			byte[] arrayBytes=PdfRead.Save(pdfDocument);
			Assert.IsTrue(arrayBytes.Length<200000,"A few glyphs of Arial, not the whole font (about 1 MB): "+arrayBytes.Length+" bytes.");
			PdfDocument pdfDocumentRead=PdfRead.Open(arrayBytes);
			Assert.AreEqual("Subset\n",PdfRead.ExtractText(pdfDocumentRead.Pages[0]));
		}
		#endregion Fonts

		#region MigraDoc
		[TestMethod]
		public void MigraDoc_DocumentWithTableAndImage() {
			RequireFonts("Arial");
			string pathImage=Path.Combine(Path.GetTempPath(),"libpdf_"+Guid.NewGuid().ToString("N")+".png");
			try {
				using(Bitmap bitmap=new Bitmap(120,60)) {
					using(Graphics graphics=Graphics.FromImage(bitmap)) {
						graphics.Clear(Color.White);
						graphics.FillEllipse(Brushes.DarkRed,10,10,100,40);
					}
					bitmap.Save(pathImage,ImageFormat.Png);
				}
				MigraDoc.DocumentObjectModel.Document document=MigraDocHelper.CreateDocument();
				PageSetup pageSetup=MigraDocHelper.GetPageSetup(document);
				pageSetup.PageWidth=Unit.FromInch(8.5);
				pageSetup.PageHeight=Unit.FromInch(11);
				pageSetup.TopMargin=Unit.FromInch(.5);
				pageSetup.LeftMargin=Unit.FromInch(.5);
				pageSetup.RightMargin=Unit.FromInch(.5);
				Section section=MigraDocHelper.AddSection(document);
				Paragraph paragraph=section.AddParagraph();
				paragraph.AddFormattedText("Treatment plan LIBPDF",MigraDocHelper.CreateFont(13,true));
				Table table=section.AddTable();
				table.Borders.Width=0.5;
				for(int i=0;i<3;i++) {
					table.AddColumn(Unit.FromInch(1.5));
				}
				for(int r=0;r<4;r++) {
					Row row=table.AddRow();
					for(int c=0;c<3;c++) {
						row.Cells[c].AddParagraph("Cell "+r+"-"+c);
					}
				}
				MigraDoc.DocumentObjectModel.Shapes.Image image=section.AddImage(pathImage);
				image.Width=Unit.FromInch(1.2);
				PdfDocument pdfDocument=PdfRead.Open(Render(document));
				Assert.AreEqual(1,pdfDocument.PageCount);
				Assert.AreEqual("612 x 792",PdfRead.Size(pdfDocument.Pages[0]));
				string text=PdfRead.ExtractText(pdfDocument.Pages[0]);
				Assert.IsTrue(PdfRead.ContainsText(text,"Treatment plan LIBPDF"),text);
				Assert.IsTrue(PdfRead.ContainsText(text,"Cell 0-0"),text);
				Assert.IsTrue(PdfRead.ContainsText(text,"Cell 3-2"),text);
				List<PdfDictionary> listImages=PdfRead.GetImages(pdfDocument.Pages[0]);
				Assert.AreEqual(1,listImages.Count);
				Assert.AreEqual(120,listImages[0].Elements.GetInteger("/Width"));
				Assert.AreEqual(60,listImages[0].Elements.GetInteger("/Height"));
			}
			finally {
				File.Delete(pathImage);
			}
		}

		[TestMethod]
		public void MigraDoc_TextWithoutAFontOfItsOwn_IsVerdana_AsIn12() {
			RequireFonts("Verdana");
			MigraDoc.DocumentObjectModel.Document document=MigraDocHelper.CreateDocument();
			MigraDocHelper.AddSection(document).AddParagraph("Default font LIBPDF");
			PdfDocument pdfDocument=PdfRead.Open(Render(document));
			Assert.IsTrue(PdfRead.ContainsText(PdfRead.ExtractText(pdfDocument.Pages[0]),"Default font LIBPDF"),PdfRead.ExtractText(pdfDocument.Pages[0]));
			Assert.IsTrue(PdfRead.GetFonts(pdfDocument.Pages[0]).Values.Any(x => x.Contains("Verdana")),
				string.Join(", ",PdfRead.GetFonts(pdfDocument.Pages[0]).Values));
		}

		[TestMethod]
		public void MigraDoc_PageSetupOfTheDocument_ReachesEverySection_AndLandscapeTurnsThePage() {
			//MigraDoc 1.2 took the page size and margins from Document.DefaultPageSetup; MigraDoc 6 throws when it is changed.
			MigraDoc.DocumentObjectModel.Document documentThrows=new MigraDoc.DocumentObjectModel.Document();
			try {
				documentThrows.DefaultPageSetup.PageWidth=Unit.FromInch(8.5);
				Assert.Fail("MigraDoc 6 lets DefaultPageSetup be changed again; MigraDocHelper.GetPageSetup may no longer be needed.");
			}
			catch(InvalidOperationException) {
			}
			MigraDoc.DocumentObjectModel.Document document=MigraDocHelper.CreateDocument();
			PageSetup pageSetup=MigraDocHelper.GetPageSetup(document);
			pageSetup.PageWidth=Unit.FromInch(8.5);
			pageSetup.PageHeight=Unit.FromInch(11);
			pageSetup.LeftMargin=Unit.FromInch(.5);
			MigraDocHelper.AddSection(document).AddParagraph("Portrait section");
			Section sectionLandscape=MigraDocHelper.AddSection(document);
			sectionLandscape.AddParagraph("Landscape section");
			//The service date view: 8.5 x 11 in the document, landscape for its section; 1.2 printed 11 x 8.5.
			MigraDocHelper.SetLandscape(sectionLandscape);
			Assert.AreEqual(Orientation.Landscape,sectionLandscape.PageSetup.Orientation);
			PdfDocument pdfDocument=PdfRead.Open(Render(document));
			Assert.AreEqual(2,pdfDocument.PageCount);
			Assert.AreEqual("612 x 792",PdfRead.Size(pdfDocument.Pages[0]));
			Assert.AreEqual("792 x 612",PdfRead.Size(pdfDocument.Pages[1]));
			Assert.AreEqual(50,(int)Math.Round(MigraDocHelper.GetPageSetup(document).LeftMargin.Inch*100),"DrawGrid's margin.");
			//Receipts: a narrow page as tall as the text.
			MigraDoc.DocumentObjectModel.Document documentReceipt=MigraDocHelper.CreateDocument();
			PageSetup pageSetupReceipt=MigraDocHelper.GetPageSetup(documentReceipt);
			pageSetupReceipt.PageWidth=Unit.FromInch(3.0);
			pageSetupReceipt.PageHeight=Unit.FromInch(2.5);
			pageSetupReceipt.TopMargin=Unit.FromInch(0.25);
			MigraDocHelper.AddSection(documentReceipt).AddParagraph("Receipt LIBPDF");
			PdfDocument pdfDocumentReceipt=PdfRead.Open(Render(documentReceipt));
			Assert.AreEqual("216 x 180",PdfRead.Size(pdfDocumentReceipt.Pages[0]));
		}

		[TestMethod]
		public void MigraDoc_Chart() {
			RequireFonts("Arial");
			MigraDoc.DocumentObjectModel.Document document=MigraDocHelper.CreateDocument();
			Section section=MigraDocHelper.AddSection(document);
			section.AddParagraph("Chart LIBPDF");
			Chart chart=section.AddChart(ChartType.Column2D);
			chart.Width=Unit.FromInch(5);
			chart.Height=Unit.FromInch(3);
			chart.SeriesCollection.AddSeries().Add(new double[] { 3,5,2 });
			chart.XValues.AddXSeries().Add(new[] { "Jan","Feb","Mar" });
			chart.XAxis.Title.Caption="Month LIBPDF";
			chart.YAxis.Title.Caption="Visits";
			PdfDocument pdfDocument=PdfRead.Open(Render(document));
			Assert.AreEqual(1,pdfDocument.PageCount);
			string text=PdfRead.ExtractText(pdfDocument.Pages[0]);
			Assert.IsTrue(PdfRead.ContainsText(text,"Chart LIBPDF"),text);
			Assert.IsTrue(PdfRead.ContainsText(text,"Month LIBPDF"),text);
			Assert.IsTrue(PdfRead.ContainsText(text,"Feb"),text);
			List<string> listOperators=PdfRead.GetOperatorNames(pdfDocument.Pages[0]);
			Assert.IsTrue(listOperators.Count(x => x=="re" || x=="f" || x=="l")>3,"The columns are drawn.");
			Assert.IsTrue(AppDomain.CurrentDomain.GetAssemblies().Any(x => x.GetName().Name=="PdfSharp.Charting-gdi"),"MigraDoc draws it with PdfSharp.Charting.");
		}

		[TestMethod]
		public void MigraDoc_PrintDocument_PrintsEveryPageInPreview() {
			string printerName=PrinterSettings.InstalledPrinters.Cast<string>().FirstOrDefault(x => x=="Microsoft Print to PDF");
			if(printerName==null) {
				Assert.Inconclusive("No \"Microsoft Print to PDF\" printer on this computer; the preview needs one to measure the page.");
			}
			MigraDoc.DocumentObjectModel.Document document=MigraDocHelper.CreateDocument();
			PageSetup pageSetup=MigraDocHelper.GetPageSetup(document);
			pageSetup.PageWidth=Unit.FromInch(8.5);
			pageSetup.PageHeight=Unit.FromInch(11);
			Section section=MigraDocHelper.AddSection(document);
			section.AddParagraph("Page one");
			section.AddPageBreak();
			section.AddParagraph("Page two");
			DocumentRenderer documentRenderer=new DocumentRenderer(document);
			documentRenderer.PrepareDocument();
			Assert.AreEqual(2,documentRenderer.FormattedDocument.PageCount);
			using(MigraDocPrintDocument migraDocPrintDocument=new MigraDocPrintDocument(documentRenderer)) {
				migraDocPrintDocument.PrinterSettings.PrinterName=printerName;
				PreviewPrintController previewPrintController=new PreviewPrintController();
				migraDocPrintDocument.PrintController=previewPrintController;
				migraDocPrintDocument.Print();//Into memory: the preview controller sends nothing to the printer.
				PreviewPageInfo[] arrayPages=previewPrintController.GetPreviewPageInfo();
				Assert.AreEqual(2,arrayPages.Length);
				foreach(PreviewPageInfo previewPageInfo in arrayPages) {
					previewPageInfo.Image.Dispose();
				}
			}
		}
		#endregion MigraDoc

		#region Merging, splitting and images
		[TestMethod]
		public void Merge_AsTheStatementBatchDoes_AndSplit() {
			RequireFonts("Arial");
			byte[] arrayBytesA=MakePdf(new[] { "Statement A page 1","Statement A page 2" },isLandscape:false);
			byte[] arrayBytesB=MakePdf(new[] { "Statement B page 1" },isLandscape:true);
			//BillingL: each statement opened for import from a stream that is closed before its pages are added to the batch.
			PdfDocument pdfDocumentMerged=new PdfDocument();
			foreach(byte[] arrayBytes in new[] { arrayBytesA,arrayBytesB }) {
				PdfDocument pdfDocumentInput=PdfRead.Open(arrayBytes,PdfDocumentOpenMode.Import);
				for(int i=0;i<pdfDocumentInput.PageCount;i++) {
					pdfDocumentMerged.AddPage(pdfDocumentInput.Pages[i]);
				}
			}
			byte[] arrayBytesMerged=PdfRead.Save(pdfDocumentMerged);
			PdfDocument pdfDocumentRead=PdfRead.Open(arrayBytesMerged);
			Assert.AreEqual(3,pdfDocumentRead.PageCount);
			Assert.AreEqual("612 x 792",PdfRead.Size(pdfDocumentRead.Pages[0]));
			Assert.AreEqual("792 x 612",PdfRead.Size(pdfDocumentRead.Pages[2]));
			Assert.AreEqual("Statement A page 1\n",PdfRead.ExtractText(pdfDocumentRead.Pages[0]));
			Assert.AreEqual("Statement A page 2\n",PdfRead.ExtractText(pdfDocumentRead.Pages[1]));
			Assert.AreEqual("Statement B page 1\n",PdfRead.ExtractText(pdfDocumentRead.Pages[2]));
			//Split: the second page of the merged file on its own.
			PdfDocument pdfDocumentImport=PdfRead.Open(arrayBytesMerged,PdfDocumentOpenMode.Import);
			PdfDocument pdfDocumentSplit=new PdfDocument();
			pdfDocumentSplit.AddPage(pdfDocumentImport.Pages[1]);
			byte[] arrayBytesSplit=PdfRead.Save(pdfDocumentSplit);
			Assert.IsTrue(PdfReader.TestPdfFile(arrayBytesSplit)>0);
			PdfDocument pdfDocumentSplitRead=PdfRead.Open(arrayBytesSplit);
			Assert.AreEqual(1,pdfDocumentSplitRead.PageCount);
			Assert.AreEqual("Statement A page 2\n",PdfRead.ExtractText(pdfDocumentSplitRead.Pages[0]));
			//A document opened for modification is saved again with its changes.
			PdfDocument pdfDocumentModify=PdfRead.Open(arrayBytesSplit,PdfDocumentOpenMode.Modify);
			using(XGraphics xGraphics=XGraphics.FromPdfPage(pdfDocumentModify.Pages[0])) {
				xGraphics.DrawString("Added",new XFont("Arial",12),XBrushes.Black,72,144);
			}
			PdfDocument pdfDocumentModifiedRead=PdfRead.Open(PdfRead.Save(pdfDocumentModify));
			Assert.AreEqual(1,pdfDocumentModifiedRead.PageCount);
			Assert.IsTrue(PdfRead.ContainsText(PdfRead.ExtractText(pdfDocumentModifiedRead.Pages[0]),"Statement A page 2 Added"));
		}

		[TestMethod]
		public void Images_TransparentPngAndJpeg_KeepTheirPixelsAndTransparency() {
			PdfDocument pdfDocument=new PdfDocument();
			using(XGraphics xGraphics=XGraphics.FromPdfPage(pdfDocument.AddPage()))
			using(Bitmap bitmapArgb=new Bitmap(200,100,PixelFormat.Format32bppArgb))
			using(Bitmap bitmapRgb=new Bitmap(320,240,PixelFormat.Format24bppRgb)) {
				using(Graphics graphics=Graphics.FromImage(bitmapArgb)) {
					//Smooth edges, so the alpha has values between clear and opaque, as text drawn into a sheet image has.
					graphics.Clear(Color.Transparent);
					graphics.SmoothingMode=SmoothingMode.AntiAlias;
					graphics.FillEllipse(Brushes.Navy,20,20,100,50);
				}
				using(Graphics graphics=Graphics.FromImage(bitmapRgb))
				using(LinearGradientBrush linearGradientBrush=new LinearGradientBrush(new Rectangle(0,0,320,240),Color.White,Color.DarkGreen,45f)) {
					graphics.FillRectangle(linearGradientBrush,0,0,320,240);
				}
				//A signature or sheet image (XImage.FromGdiPlusImage), and an X-ray saved as JPEG.
				xGraphics.DrawImage(XImage.FromGdiPlusImage(bitmapArgb),72,72,200,100);
				using(MemoryStream memoryStream=new MemoryStream()) {
					bitmapRgb.Save(memoryStream,ImageFormat.Jpeg);
					memoryStream.Position=0;
					using(System.Drawing.Image imageJpeg=System.Drawing.Image.FromStream(memoryStream)) {
						xGraphics.DrawImage(XImage.FromGdiPlusImage(imageJpeg),72,200,320,240);
					}
				}
			}
			PdfDocument pdfDocumentRead=PdfRead.Open(PdfRead.Save(pdfDocument));
			List<PdfDictionary> listImages=PdfRead.GetImages(pdfDocumentRead.Pages[0]);
			Assert.AreEqual(2,listImages.Count);
			PdfDictionary dictPng=listImages.Single(x => x.Elements.GetInteger("/Width")==200);
			Assert.AreEqual(100,dictPng.Elements.GetInteger("/Height"));
			Assert.IsTrue(dictPng.Elements.ContainsKey("/SMask"),"The transparent part stays transparent. Keys: "+string.Join(" ",dictPng.Elements.Keys));
			PdfDictionary dictJpeg=listImages.Single(x => x.Elements.GetInteger("/Width")==320);
			Assert.AreEqual(240,dictJpeg.Elements.GetInteger("/Height"));
			StringAssert.Contains(dictJpeg.Elements["/Filter"].ToString(),"DCTDecode","A JPEG stays a JPEG.");
		}
		#endregion Merging, splitting and images

		#region Helpers
		private static void RequireFonts(params string[] arrayFontNames) {
			foreach(string fontName in arrayFontNames) {
				if(!SheetFonts.IsFontInstalled(fontName)) {
					Assert.Inconclusive("The font "+fontName+" is not installed on this computer.");
				}
			}
		}

		private static byte[] Render(MigraDoc.DocumentObjectModel.Document document) {
			PdfDocumentRenderer pdfDocumentRenderer=new PdfDocumentRenderer();
			pdfDocumentRenderer.Document=document;
			pdfDocumentRenderer.RenderDocument();
			return PdfRead.Save(pdfDocumentRenderer.PdfDocument);
		}

		private static byte[] MakePdf(string[] arrayPageTexts,bool isLandscape) {
			PdfDocument pdfDocument=new PdfDocument();
			foreach(string pageText in arrayPageTexts) {
				PdfPage pdfPage=pdfDocument.AddPage();
				pdfPage.Width=XUnit.FromPoint(612);
				pdfPage.Height=XUnit.FromPoint(792);
				if(isLandscape) {
					pdfPage.Orientation=PdfSharp.PageOrientation.Landscape;
				}
				using(XGraphics xGraphics=XGraphics.FromPdfPage(pdfPage)) {
					xGraphics.DrawString(pageText,new XFont("Arial",12),XBrushes.Black,72,72);
				}
			}
			return PdfRead.Save(pdfDocument);
		}

		private static Sheet NewSheet(int width,int height,bool isLandscape,params SheetField[] arraySheetFields) {
			return new Sheet {
				SheetType=SheetTypeEnum.PatientForm,
				FontName="Arial",
				FontSize=11f,
				Width=width,
				Height=height,
				IsLandscape=isLandscape,
				Description="LIBPDF synthetic",
				Parameters=new List<SheetParameter>(),
				SheetFields=new List<SheetField>(arraySheetFields),
			};
		}

		private static SheetField TextField(string text,int xPos,int yPos,HorizontalAlignment textAlign=HorizontalAlignment.Left,string fontName="Arial") {
			return new SheetField {
				FieldType=SheetFieldType.StaticText,
				FieldName="",
				FieldValue=text,
				FontName=fontName,
				FontSize=11f,
				XPos=xPos,
				YPos=yPos,
				Width=400,
				Height=40,
				TextAlign=textAlign,
				ItemColor=Color.Black,
				RadioButtonValue="",
				RadioButtonGroup="",
				ReportableName="",
				UiLabelMobile="",
				UiLabelMobileRadioButton="",
				GrowthBehavior=GrowthBehaviorEnum.None,
			};
		}
		#endregion Helpers
	}
}
