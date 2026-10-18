// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using System.IO;
using System.Text;
using ImagingDeviceManager.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace ImagingDeviceManager.Tests {
	[TestClass]
	public class PdfPageWriterTests {
		private string _tempDir;

		[TestInitialize]
		public void Setup() {
			_tempDir=Path.Combine(Path.GetTempPath(),"ImagingDeviceManagerTests_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_tempDir);
		}

		[TestCleanup]
		public void Cleanup() {
			try {
				Directory.Delete(_tempDir,true);
			}
			catch {
			}
		}

		internal static int GetPageCount(string path) {
			using(PdfDocument pdfDocument=PdfReader.Open(path,PdfDocumentOpenMode.Import)) {
				return pdfDocument.PageCount;
			}
		}

		private static string ReadLatin1(string path) {
			return Encoding.GetEncoding(28591).GetString(File.ReadAllBytes(path));
		}

		[TestMethod]
		public void Save_WritesOnePagePerImageSizedByResolution() {
			string path=Path.Combine(_tempDir,"scan.pdf");
			using(PdfPageWriter pdfPageWriter=new PdfPageWriter(60)) {
				pdfPageWriter.AddPage(DibImage.FromPackedDib(FakePage.CreateRgb(300,150,150,(x,y) => Color.FromArgb(x%256,y%256,128)).NativeDib));
				pdfPageWriter.AddPage(DibImage.FromPackedDib(FakePage.CreateBw(600,300,300,(x,y) => (x/10+y/10)%2==0).NativeDib));
				Assert.AreEqual(2,pdfPageWriter.PageCount);
				pdfPageWriter.Save(path);
			}
			Assert.AreEqual(2,GetPageCount(path));
			using(PdfDocument pdfDocument=PdfReader.Open(path,PdfDocumentOpenMode.Import)) {
				//300 x 150 pixels at 150 dpi is 2 x 1 inches, 144 x 72 points.
				Assert.AreEqual(144,pdfDocument.Pages[0].Width.Point,0.5);
				Assert.AreEqual(72,pdfDocument.Pages[0].Height.Point,0.5);
				Assert.AreEqual(144,pdfDocument.Pages[1].Width.Point,0.5);
			}
			string content=ReadLatin1(path);
			StringAssert.Contains(content,"/DCTDecode","The color page should be stored as JPEG.");
		}

		[TestMethod]
		public void Quality100_StoresColorLosslessly() {
			string path=Path.Combine(_tempDir,"lossless.pdf");
			using(PdfPageWriter pdfPageWriter=new PdfPageWriter(100)) {
				pdfPageWriter.AddPage(DibImage.FromPackedDib(FakePage.CreateRgb(40,30,100,(x,y) => Color.FromArgb(x,y,0)).NativeDib));
				pdfPageWriter.Save(path);
			}
			Assert.AreEqual(1,GetPageCount(path));
			Assert.IsFalse(ReadLatin1(path).Contains("/DCTDecode"));
		}

		[TestMethod]
		public void JpegQuality_ChangesFileSize() {
			FakePage page=FakePage.CreateRgb(400,300,200,(x,y) => Color.FromArgb((x*y)%256,(x+y)%256,(x^y)%256));
			string pathLow=Path.Combine(_tempDir,"low.pdf");
			string pathHigh=Path.Combine(_tempDir,"high.pdf");
			using(PdfPageWriter pdfPageWriter=new PdfPageWriter(10)) {
				pdfPageWriter.AddPage(DibImage.FromPackedDib(page.NativeDib));
				pdfPageWriter.Save(pathLow);
			}
			using(PdfPageWriter pdfPageWriter=new PdfPageWriter(95)) {
				pdfPageWriter.AddPage(DibImage.FromPackedDib(page.NativeDib));
				pdfPageWriter.Save(pathHigh);
			}
			Assert.IsTrue(new FileInfo(pathLow).Length<new FileInfo(pathHigh).Length,"Lower JPEG quality must give a smaller file.");
		}

		[TestMethod]
		public void Save_WithoutPagesThrowsNoPages() {
			using(PdfPageWriter pdfPageWriter=new PdfPageWriter(75)) {
				try {
					pdfPageWriter.Save(Path.Combine(_tempDir,"empty.pdf"));
					Assert.Fail("Expected an exception.");
				}
				catch(ImagingDeviceException ex) {
					Assert.AreEqual(EZTwainErrorCode.EZTEC_0_PAGES,ex.ErrorCode);
				}
			}
		}
	}
}
