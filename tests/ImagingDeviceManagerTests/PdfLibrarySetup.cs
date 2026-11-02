// Selects the PDF library's AGPL licence for this test assembly, as OpenPmsDental does at start (OpenPmsDentalBusiness.PdfSharpSetup.Init,
// which this assembly does not reference): PDFsharp 6.5 makes no PDF before its licence is selected.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PdfSharp;

namespace ImagingDeviceManager.Tests {
	[TestClass]
	public class PdfLibrarySetup {
		[AssemblyInitialize]
		public static void AssemblyInitialize(TestContext testContext) {
			PdfSharpLicense.SetAgplLicense();
		}
	}
}
