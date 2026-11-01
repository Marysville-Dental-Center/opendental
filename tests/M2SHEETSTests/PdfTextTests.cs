// Sheet PDFs (SheetDrawingJob.CreatePdf) with text in several scripts: Latin text keeps the old output byte for byte, Cyrillic and Greek are
// written as Unicode text with the font embedded, and text the PDF library cannot lay out becomes a transparent 300 dpi image.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace M2SHEETSTests {
	[TestClass]
	public class PdfTextTests {
		[TestInitialize]
		public void TestInitialize() {
			Samples.UseUsEnglish();
			Samples.RequireFonts("Arial");
		}

		[TestMethod]
		public void Pdf_LatinTextIsUnchanged() {
			//Same page drawn by the old helper and by the new one: identical content and the same standard-encoded font.
			byte[] arrayOld=DrawOnePage((xg,xfont,rect) => GraphicsHelper.DrawStringX(xg,Samples.LatinWinAnsi,xfont,XBrushes.Black,rect,HorizontalAlignment.Left));
			byte[] arrayNew=DrawOnePage((xg,xfont,rect) => SheetTextDrawing.DrawStringX(xg,Samples.LatinWinAnsi,xfont,XBrushes.Black,rect,HorizontalAlignment.Left));
			CollectionAssert.AreEqual(arrayOld,arrayNew);
			PdfPageInfo pdfPageInfo=PdfInspect.ReadFirstPage(PdfInspect.CreateSheetPdf(Samples.TextField(Samples.LatinWinAnsi)));
			Assert.AreEqual(0,pdfPageInfo.ListImages.Count);
			Assert.AreEqual(1,pdfPageInfo.ListFonts.Count);
			Assert.AreEqual("/TrueType",pdfPageInfo.ListFonts[0].Subtype);
			Assert.AreEqual("/WinAnsiEncoding",pdfPageInfo.ListFonts[0].Encoding);
		}

		[TestMethod]
		public void Pdf_CyrillicAndGreekAreUnicodeText() {
			PdfPageInfo pdfPageInfo=PdfInspect.ReadFirstPage(PdfInspect.CreateSheetPdf(Samples.TextField(Samples.Russian+" "+Samples.Greek)));
			Assert.AreEqual(0,pdfPageInfo.ListImages.Count,"Written as text, not as an image.");
			PdfFontInfo pdfFontInfo=pdfPageInfo.ListFonts.Single();
			Assert.AreEqual("/Type0",pdfFontInfo.Subtype);
			Assert.AreEqual("/Identity-H",pdfFontInfo.Encoding);
			StringAssert.Contains(pdfFontInfo.BaseFont,"Arial","The field's own font has the glyphs.");
			foreach(char c in Samples.Russian+Samples.Greek.Replace(" ","")) {
				StringAssert.Contains(pdfFontInfo.ToUnicode.ToUpperInvariant(),PdfInspect.Hex(c),"Copy and search work for "+c);
			}
		}

		[TestMethod]
		public void Pdf_KoreanUsesAnEmbeddedFallbackFont() {
			Samples.RequireFonts("Malgun Gothic");
			PdfPageInfo pdfPageInfo=PdfInspect.ReadFirstPage(PdfInspect.CreateSheetPdf(Samples.TextField(Samples.Korean)));
			Assert.AreEqual(0,pdfPageInfo.ListImages.Count);
			PdfFontInfo pdfFontInfo=pdfPageInfo.ListFonts.Single();
			Assert.AreEqual("/Type0",pdfFontInfo.Subtype);
			StringAssert.Contains(pdfFontInfo.BaseFont.Replace(" ",""),"MalgunGothic");
		}

		[TestMethod]
		public void Pdf_RightToLeftAndComplexScriptsAreImages() {
			foreach(string text in new[] { Samples.Hebrew,Samples.Arabic,Samples.Hindi,Samples.Thai,"Name: "+Samples.Hebrew }) {
				SheetField sheetField=Samples.TextField(text,width:300,height:40);
				PdfPageInfo pdfPageInfo=PdfInspect.ReadFirstPage(PdfInspect.CreateSheetPdf(sheetField));
				Assert.AreEqual(1,pdfPageInfo.ListImages.Count,text);
				Assert.AreEqual(0,pdfPageInfo.ListFonts.Count,text);
				PdfImageInfo pdfImageInfo=pdfPageInfo.ListImages[0];
				Assert.AreEqual((int)Math.Ceiling(300*1.04f*SheetTextDrawing.RASTER_SCALE),pdfImageInfo.Width,"300 dpi, as wide as the printed layout box.");
				Assert.IsTrue(pdfImageInfo.Height>0 && pdfImageInfo.Height<=Math.Ceiling(40*1.04f*SheetTextDrawing.RASTER_SCALE),text);
				Assert.IsTrue(pdfImageInfo.HasSoftMask,"Transparent, so lines and boxes under the text still show.");
			}
		}

		[TestMethod]
		public void Pdf_HugeFieldGetsALowerResolution() {
			//A long right-to-left paragraph in a field taller than a page: the image stays within MAX_RASTER_PIXELS.
			string text=string.Join(" ",Enumerable.Repeat(Samples.Hebrew,3000));
			PdfDocument pdfDocument=new PdfDocument();
			PdfPage pdfPage=pdfDocument.AddPage();
			using(XGraphics xg=XGraphics.FromPdfPage(pdfPage)) {
				SheetTextDrawing.DrawStringX(xg,text,new XFont("Arial",11,XFontStyle.Regular),XBrushes.Black,new RectangleF(10,10,800,4000),HorizontalAlignment.Right);
			}
			PdfImageInfo pdfImageInfo=PdfInspect.ReadFirstPage(pdfDocument).ListImages.Single();
			Assert.IsTrue((float)pdfImageInfo.Width*pdfImageInfo.Height<=SheetTextDrawing.MAX_RASTER_PIXELS*1.01f,pdfImageInfo.Width+" x "+pdfImageInfo.Height);
			Assert.IsTrue(pdfImageInfo.Width<Math.Ceiling(800*1.04f*SheetTextDrawing.RASTER_SCALE),"Lower than 300 dpi.");
			Assert.IsTrue(pdfImageInfo.Width>=800,"At least 100 dpi.");
		}

		[TestMethod]
		public void Pdf_FontsThatCannotBeEmbeddedAreImages() {
			Samples.RequireFonts("Microsoft YaHei");
			PdfPageInfo pdfPageInfo=PdfInspect.ReadFirstPage(PdfInspect.CreateSheetPdf(Samples.TextField(Samples.Chinese)));
			Assert.AreEqual(1,pdfPageInfo.ListImages.Count,"Microsoft YaHei is in a font collection, which the PDF library cannot embed.");
		}

		[TestMethod]
		public void Pdf_MixedSheetInEveryScript() {
			//One sheet with a field per script, plus the field kinds that draw text: input, output and static text, a combo box and a bold field.
			SheetField sheetFieldCombo=Samples.TextField("",sheetFieldType:SheetFieldType.ComboBox,yPos:560,height:19);
			sheetFieldCombo.FieldValue=Samples.Russian+";"+Samples.Russian+"|Other";
			SheetField sheetFieldBold=Samples.TextField(Samples.Greek,yPos:600);
			sheetFieldBold.FontIsBold=true;
			PdfDocument pdfDocument=PdfInspect.CreateSheetPdf(
				Samples.TextField(Samples.LatinWinAnsi,yPos:40),
				Samples.TextField(Samples.Russian,sheetFieldType:SheetFieldType.InputField,yPos:80),
				Samples.TextField(Samples.Greek,sheetFieldType:SheetFieldType.OutputText,yPos:120),
				Samples.TextField(Samples.Chinese+" "+Samples.Japanese,yPos:160),
				Samples.TextField(Samples.Korean,yPos:200),
				Samples.TextField(Samples.Hebrew+" 123.",yPos:240),
				Samples.TextField(Samples.Hebrew+" 123.",textAlign:HorizontalAlignment.Right,yPos:280),
				Samples.TextField(Samples.Arabic,yPos:320),
				Samples.TextField(Samples.Hindi,yPos:360),
				Samples.TextField(Samples.Thai,yPos:400),
				Samples.TextField(Samples.Georgian,yPos:440),
				Samples.TextField(Samples.Russian+" "+Samples.Russian+" "+Samples.Russian+" "+Samples.Hebrew+" "+Samples.Hebrew,width:120,yPos:480,height:70),
				sheetFieldCombo,
				sheetFieldBold,
				//Right-aligned and centered text written as text and drawn as an image line up.
				Samples.TextField(Samples.Russian,textAlign:HorizontalAlignment.Right,yPos:640),
				Samples.TextField(Samples.Hebrew,textAlign:HorizontalAlignment.Right,yPos:670),
				Samples.TextField(Samples.Russian,textAlign:HorizontalAlignment.Center,yPos:700),
				Samples.TextField(Samples.Hebrew,textAlign:HorizontalAlignment.Center,yPos:730));
			PdfPageInfo pdfPageInfo=PdfInspect.ReadFirstPage(pdfDocument,"M2SHEETS-every-script.pdf");
			Assert.IsTrue(pdfPageInfo.ListFonts.Any(x => x.Encoding=="/WinAnsiEncoding"),"The Latin field.");
			Assert.IsTrue(pdfPageInfo.ListFonts.Any(x => x.Subtype=="/Type0"),"Cyrillic, Greek, Korean.");
			Assert.IsTrue(pdfPageInfo.ListImages.Count>=6,"Hebrew twice, Arabic, Hindi, Thai and the mixed field at least.");
		}

		[TestMethod]
		public void Pdf_SignatureByLineInNonLatinScript() {
			//The "Signed by" line can hold a user name in any script (25.2 feature); it goes through the same helper.
			byte[] arrayContent=DrawOnePage((xg,xfont,rect) => SheetTextDrawing.DrawStringX(xg,"By: "+Samples.Russian,xfont,Brushes.Black,rect,HorizontalAlignment.Left));
			Assert.IsTrue(arrayContent.Length>0);
		}

		///<summary>Draws on the first page of a new PDF with an Arial 11 point font and a 300 by 40 pixel field; returns the page's content stream.
		///</summary>
		private static byte[] DrawOnePage(Action<XGraphics,XFont,RectangleF> actionDraw) {
			PdfDocument pdfDocument=new PdfDocument();
			PdfPage pdfPage=pdfDocument.AddPage();
			using(XGraphics xg=XGraphics.FromPdfPage(pdfPage)) {
				actionDraw(xg,new XFont("Arial",11,XFontStyle.Regular),new RectangleF(50,50,300,40));
			}
			return PdfInspect.ReadFirstPage(pdfDocument).Content;
		}
	}
}
