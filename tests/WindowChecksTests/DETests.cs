// Groups D and E of the checklist: the WPF windows of WpfControlsOD and the capture windows of ImagingCapture, in process.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WindowChecksTests {
	[TestClass]
	public class DETests {
		[TestMethod]
		public void D1toD5_WpfWindows() {
			WinChkFixture.Begin("D");
			CTests.RunInProcess(new[] {
				new[] { "D1","Patient select (FrmPatientSelect)" },
				new[] { "D2","Account edit (FrmAccountEdit)" },
				new[] { "D3","Spell check in TextRich (FrmCommItem)" },
				new[] { "D4","eForm editor (FrmEFormDefEdit)" },
				new[] { "D5","eForm fill and print preview (FrmEFormFillEdit, FrmPrintPreview)" },
			},"chart");
		}

		[TestMethod]
		public void E1toE4_CaptureWindows() {
			WinChkFixture.Begin("E");
			CTests.RunInProcess(new[] {
				new[] { "E1","Unassigned captures (FrmCaptureQuarantine)" },
				new[] { "E2","Standard templates (FrmStandardTemplates)" },
				new[] { "E3","Capture settings (FrmCaptureSettings)" },
				new[] { "E4","Test capture (FrmTestCapture)" },
			},"chart");
		}
	}
}
