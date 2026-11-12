// The spike's in-process script: Help > About through the real menu item, the About window printed two ways, and its Diagnostics window
// read and closed.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Windows.Forms;

namespace WindowChecksTests.InProc {
	public static partial class InProcChecks {
		public static void CheckSPIKE(Ui ui,CheckResult checkResult) {
			checkResult.AddShot(ui.Shot(ui.Invoke(() => ui.FormMain.Handle),"spike-inproc-main.png"),"The main window (PrintWindow)");
			Ui.Opened opened=OpenMenu(ui,"Help","About");
			if(!opened.IsOpen) {
				return;
			}
			checkResult.Expect(ui.GetWindowTypeName(opened.Hwnd)=="FormAbout","Help > About opens FormAbout",ui.DescribeWindow(opened.Hwnd));
			checkResult.AddShot(ui.Shot(opened.Hwnd,"spike-inproc-about-printwindow.png"),"About (PrintWindow)");
			Form form=ui.GetForm(opened.Hwnd);
			checkResult.AddShot(ui.ShotDraw(form,"spike-inproc-about-drawtobitmap.png"),"About (DrawToBitmap)");
			IntPtr hwndAbout=opened.Hwnd;
			Ui.Opened openedDiagnostics=ui.Open("Diagnostics",() => ((Button)FindControl(ui,hwndAbout,"butDiagnostics",isOnThread:true)).PerformClick());
			if(openedDiagnostics.IsOpen) {
				string text=ui.Invoke(() => {
					Control control=Control.FromHandle(openedDiagnostics.Hwnd);
					foreach(Control controlChild in Ui.AllControls(control)) {
						if(controlChild is TextBoxBase textBoxBase && textBoxBase.Text.Length>20) {
							return textBoxBase.Text;
						}
					}
					return "";
				});
				checkResult.Note("Diagnostics text: "+text.Replace("\r\n"," / "));
				checkResult.AddShot(ui.Shot(openedDiagnostics.Hwnd,"spike-inproc-diagnostics.png"),"Diagnostics (PrintWindow)");
				checkResult.Expect(ui.Close(openedDiagnostics.Hwnd),"Diagnostics closes");
			}
			checkResult.Expect(ui.Close(hwndAbout),"About closes");
			checkResult.Expect(opened.Posted.Wait(),"The About handler returned");
		}
	}
}
