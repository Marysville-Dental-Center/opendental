// PDFsharp 6.5 under the AGPL: the program selects the licence with no key, and the library then draws nothing of its own on the pages, opens
// no network connection and writes no message; only the Producer and Creator properties of a PDF name the licence. The licence texts that ship
// are the FSF's, word for word. The licence check is this project's open PdfSharpLicense.cs (StandInTests), not empira's closed library.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace PdfLibraryTests {
	[TestClass]
	public class LicenceTests {
		///<summary>SHA-256 of LICENSE-AGPL-3.0.txt with LF line ends: gnu.org's agpl-3.0.txt, byte for byte.</summary>
		private const string AGPL_SHA256_LF="8486a10c4393cee1c25392769ddd3b2d6c242d6ec7928e1414efff7dfb2f07ef";
		///<summary>SHA-256 of LICENSE-GPL-3.0.txt with LF line ends: gnu.org's gpl-3.0.txt, byte for byte (the copy mintty and xz ship).</summary>
		private const string GPL3_SHA256_LF="3972dc9744f6499f0f9b2dbf76696f2ae7ad8af9b23dde66d6af86c9dfb36986";
		private static IsolatedPdfResult _isolatedPdfResult;
		private static readonly object _lock=new object();

		///<summary>The isolated run, done once for the tests of this class.</summary>
		private static IsolatedPdfResult GetIsolatedResult() {
			lock(_lock) {
				if(_isolatedPdfResult==null) {
					_isolatedPdfResult=IsolatedPdfRun.Run();
				}
				return _isolatedPdfResult;
			}
		}

		[TestMethod]
		public void WithoutTheLicence_TheLibraryRefuses_TheAgplLicenceNeedsNoKey() {
			IsolatedPdfResult isolatedPdfResult=GetIsolatedResult();
			StringAssert.Contains(isolatedPdfResult.ErrorWithoutLicence,"PdfSharpLicenseException",
				"PDFsharp 6.5 refuses to make a PDF until a licence is selected, which is why every program calls PdfSharpSetup.Init.");
			StringAssert.Contains(isolatedPdfResult.ErrorWithoutLicence,"license is not valid");
			//PdfSharpSetup.Init calls PdfSharpLicense.SetAgplLicense(), which takes no key.
			Assert.AreEqual(2,isolatedPdfResult.PageCountPdfSharp);
			Assert.IsTrue(isolatedPdfResult.PageCountMigraDoc>=1);
		}

		[TestMethod]
		public void WithTheLicence_NoNetworkConnection_NoMessage() {
			IsolatedPdfResult isolatedPdfResult=GetIsolatedResult();
			Assert.AreEqual(0,isolatedPdfResult.ListNetworkLinesDuringPdf.Count,"Selecting the licence and making, reading and rendering PDFs "
				+"used the network:\r\n"+string.Join("\r\n",isolatedPdfResult.ListNetworkLinesDuringPdf.Take(20)));
			Assert.IsTrue(isolatedPdfResult.ListNetworkLinesControl.Count>0,"The network tracing is on: creating a socket was recorded.");
			Assert.AreEqual("",isolatedPdfResult.ConsoleAndTraceOutput.Trim(),"The library wrote to the console or to Trace.");
			CollectionAssert.DoesNotContain(isolatedPdfResult.ListAssemblies,"System.Net.Http");
			//The licence check is part of PdfSharp-gdi built here: empira's closed PdfSharp.License is never loaded, and the rest of the package stays out.
			CollectionAssert.Contains(isolatedPdfResult.ListAssemblies,"PdfSharp-gdi");
			foreach(string name in new[] { "PdfSharp.License","PdfSharp.Quality-gdi","PdfSharp.Snippets-gdi","PdfSharp.Cryptography","PdfSharp.WPFonts",
				"PdfSharp.BarCodes-gdi" })
			{
				CollectionAssert.DoesNotContain(isolatedPdfResult.ListAssemblies,name);
			}
		}

		[TestMethod]
		public void Pdfs_CarryNoMarkOfTheLicence_ExceptInTheirProperties() {
			//A blank page: the library adds nothing to it.
			PdfDocument pdfDocument=new PdfDocument();
			pdfDocument.AddPage();
			PdfDocument pdfDocumentBlank=PdfRead.Open(PdfRead.Save(pdfDocument));
			PdfPage pdfPageBlank=pdfDocumentBlank.Pages[0];
			Assert.AreEqual("",PdfRead.ExtractText(pdfPageBlank));
			Assert.AreEqual(0,PdfRead.GetFonts(pdfPageBlank).Count);
			Assert.AreEqual(0,PdfRead.GetImages(pdfPageBlank).Count);
			Assert.IsFalse(pdfPageBlank.Elements.ContainsKey("/Annots"),"No annotation.");
			Assert.AreEqual(1,pdfDocumentBlank.PageCount);
			//A page with one line of text shows exactly that line.
			pdfDocument=new PdfDocument();
			using(XGraphics xGraphics=XGraphics.FromPdfPage(pdfDocument.AddPage())) {
				xGraphics.DrawString("Only this line",new XFont("Arial",12),XBrushes.Black,72,72);
			}
			PdfDocument pdfDocumentText=PdfRead.Open(PdfRead.Save(pdfDocument));
			Assert.AreEqual("Only this line\n",PdfRead.ExtractText(pdfDocumentText.Pages[0]));
			Assert.AreEqual(1,PdfRead.GetFonts(pdfDocumentText.Pages[0]).Count);
			Assert.IsFalse(pdfDocumentText.Pages[0].Elements.ContainsKey("/Annots"));
			//The document properties name the library and its licence, as 1.2 named the library.
			StringAssert.Contains(pdfDocumentText.Info.Producer,"PDFsharp 6.5.0");
			StringAssert.Contains(pdfDocumentText.Info.Producer,"(AGPL license)");
		}

		[TestMethod]
		public void LicenceTexts_AreTheFsfTextsVerbatim() {
			Assert.AreEqual(AGPL_SHA256_LF,Sha256Lf(TestDb.FindRepoFile("LICENSE-AGPL-3.0.txt")),"The AGPL 3.0 text, unchanged.");
			Assert.AreEqual(GPL3_SHA256_LF,Sha256Lf(TestDb.FindRepoFile("LICENSE-GPL-3.0.txt")),"The GPL 3.0 text, unchanged.");
			StringAssert.StartsWith(File.ReadAllText(TestDb.FindRepoFile("LICENSE-AGPL-3.0.txt")).TrimStart(),"GNU AFFERO GENERAL PUBLIC LICENSE");
		}

		[TestMethod]
		public void ProgramLicenseText_SaysHowTheProgramIsDistributed_AndShowsTheTexts() {
			string text=OpenPmsDental.LicenseList.GetProgramLicenseText();
			StringAssert.Contains(text,"either version 2 of the License, or (at your option) any later version");
			StringAssert.Contains(text,"GNU Affero General Public License, version 3");
			StringAssert.Contains(text,"requirements on interaction through a network apply to the combination as a whole",
				"GPL 3.0 section 13: the AGPL's network clause covers the whole program.");
			StringAssert.Contains(text,"Version 3, 29 June 2007","The GPL 3.0 text follows the 2.0 text.");
			Dictionary<string,string> dictVendorTexts=OpenPmsDental.LicenseList.GetVendorTexts();
			foreach(string component in new[] { "PDFsharp","MigraDoc" }) {
				StringAssert.Contains(dictVendorTexts[component],"AGPL Dual License",component+": empira's notice for 6.5.");
				StringAssert.Contains(dictVendorTexts[component],"GNU AFFERO GENERAL PUBLIC LICENSE",component+": the AGPL text.");
				Assert.IsFalse(dictVendorTexts[component].Contains("MIT License"),component+": no longer MIT.");
			}
		}

		private static string Sha256Lf(string path) {
			string text=File.ReadAllText(path).Replace("\r\n","\n");
			using(SHA256 sha256=SHA256.Create()) {
				return string.Concat(sha256.ComputeHash(Encoding.UTF8.GetBytes(text)).Select(x => x.ToString("x2")));
			}
		}
	}
}
