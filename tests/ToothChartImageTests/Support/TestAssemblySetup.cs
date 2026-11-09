// The test assembly's start (package TC-3, docs/specs/wpf-tooth-chart-plan.md section 7): the live view's tests draw in software.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ToothChartImageTests {
	[TestClass]
	public class TestAssemblySetup {
		///<summary>Section 7: the tests that host the live view (O-19 (b)) set RenderOptions.ProcessRenderMode to SoftwareOnly, so the window WPF draws on
		///screen does not depend on the graphics card. It changes nothing else: RenderTargetBitmap renders in software anyway, and no test waits for a
		///frame on screen.</summary>
		[AssemblyInitialize]
		public static void AssemblyInitialize(TestContext testContext) {
			RenderOptions.ProcessRenderMode=RenderMode.SoftwareOnly;
		}
	}
}
