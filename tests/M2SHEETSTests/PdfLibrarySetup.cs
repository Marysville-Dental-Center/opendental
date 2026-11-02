// Sets up the PDF library for this test assembly as every program does at start (PdfSharpSetup.Init): PDFsharp 6.5 makes no PDF before its
// licence is selected, and the font fallback must be in place before any text is drawn.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2SHEETSTests {
	[TestClass]
	public class PdfLibrarySetup {
		[AssemblyInitialize]
		public static void AssemblyInitialize(TestContext testContext) {
			PdfSharpSetup.Init();
		}
	}
}
