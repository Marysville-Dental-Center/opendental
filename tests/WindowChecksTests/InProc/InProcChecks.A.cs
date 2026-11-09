// Group A's in-process parts: the splash (a start with a command line never shows it, so it is shown here as the program shows it) and
// Help > About with its Diagnostics and License windows, through the real menu item.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Linq;
using System.Windows.Forms;
using OpenPmsDental;

namespace WindowChecksTests.InProc {
	public static partial class InProcChecks {
		public static void CheckA1SPLASH(Ui ui,CheckResult checkResult) {
			IntPtr hwnd=ui.Invoke(() => {
				FormSplash formSplash=new FormSplash();
				formSplash.Show();
				formSplash.Refresh();
				return formSplash.Handle;
			});
			System.Threading.Thread.Sleep(500);
			bool hasImage=ui.Invoke(() => ((Form)Control.FromHandle(hwnd)).BackgroundImage!=null);
			checkResult.Expect(hasImage,"The splash (FormSplash, shown as the program shows it without a command line) has its product image");
			checkResult.AddShot(ui.Shot(hwnd,"A1-splash.png"),"The splash");
			ui.Invoke(() => ((Form)Control.FromHandle(hwnd)).Close());
		}

		///<summary>A6's dialog: Help > About while NoCustomBorders.txt is beside the exe has the standard Windows title bar.</summary>
		public static void CheckA6ABOUT(Ui ui,CheckResult checkResult) {
			Ui.Opened opened=OpenMenu(ui,"Help","About");
			if(!opened.IsOpen) {
				return;
			}
			int style=Native.GetWindowLong(opened.Hwnd,Native.GWL_STYLE);
			checkResult.Expect((style&Native.WS_CAPTION)==Native.WS_CAPTION,"Help > About (in process, with the flag file) has the standard Windows title bar",
				"style 0x"+style.ToString("X8"));
			checkResult.AddShot(ui.Shot(opened.Hwnd,"A6-nocustomborders-about.png"),"About with standard borders");
			checkResult.Expect(ui.Close(opened.Hwnd),"About closes");
		}

		///<summary>A7's in-process part, in the Release build: the File menu has no Choose Database, Chart and Imaging open, Help > About.</summary>
		public static void CheckA7RELEASE(Ui ui,CheckResult checkResult) {
			string items=ui.Invoke(() => string.Join(", ",GetMenuItem("File").DropDownItems.OfType<ToolStripItem>().Where(x => x.Available && !(x is ToolStripSeparator))
				.Select(x => x.Text.Replace("&",""))));
			checkResult.Expect(!items.Contains("Choose Database"),"The Release build's File menu has no Choose Database (items: "+items+")");
			GoToModule(ui,OpenPmsDentalBusiness.EnumModuleType.Chart);
			checkResult.Expect(GetModule(ui)==OpenPmsDentalBusiness.EnumModuleType.Chart,"Chart opens (Release)");
			GoToModule(ui,OpenPmsDentalBusiness.EnumModuleType.Imaging);
			checkResult.Expect(GetModule(ui)==OpenPmsDentalBusiness.EnumModuleType.Imaging,"Imaging opens (Release)");
			Ui.Opened opened=OpenMenu(ui,"Help","About");
			if(opened.IsOpen) {
				checkResult.AddShot(ui.Shot(opened.Hwnd,"A7-release-about.png"),"About in the Release build");
				string version=ui.Invoke(() => FindControl(ui,opened.Hwnd,"labelVersion",true).Text);
				checkResult.Note("Release About: "+version);
				checkResult.Expect(ui.Close(opened.Hwnd),"About closes");
			}
		}

		public static void CheckA2(Ui ui,CheckResult checkResult) {
			Ui.Opened opened=OpenMenu(ui,"Help","About");
			if(!opened.IsOpen) {
				return;
			}
			IntPtr hwndAbout=opened.Hwnd;
			checkResult.Expect(ui.GetWindowTypeName(hwndAbout)=="FormAbout","Help > About opens FormAbout",ui.DescribeWindow(hwndAbout));
			string productName=ui.Invoke(() => FindControl(ui,hwndAbout,"labelProductName",true).Text);
			string attribution=ui.Invoke(() => FindControl(ui,hwndAbout,"labelAttribution",true).Text);
			string version=ui.Invoke(() => FindControl(ui,hwndAbout,"labelVersion",true).Text);
			checkResult.Expect(productName=="Open PMS - Dental","Product name 'Open PMS - Dental'",productName);
			checkResult.Expect(attribution.Contains("fork of Open Dental") && attribution.Contains("GNU GPL"),"Attribution: "+attribution,attribution);
			checkResult.Expect(version.StartsWith("Version:"),"Version line: "+version,version);
			bool hasLogo=ui.Invoke(() => {
				Control controlLogo=FindControl(ui,hwndAbout,"pictureLogo",true);
				return controlLogo!=null && controlLogo.Visible && (controlLogo.GetType().GetProperty("Image")?.GetValue(controlLogo)!=null
					|| controlLogo.BackgroundImage!=null);
			});
			checkResult.Expect(hasLogo,"The logo (pictureLogo, with its image) is on the window");
			checkResult.AddShot(ui.Shot(hwndAbout,"A2-about.png"),"About");
			Ui.Opened openedDiagnostics=ui.Open("Diagnostics",() => ((Button)FindControl(ui,hwndAbout,"butDiagnostics",isOnThread:true)).PerformClick());
			if(openedDiagnostics.IsOpen) {
				string text=ui.Invoke(() => Ui.AllControls(Control.FromHandle(openedDiagnostics.Hwnd)).OfType<TextBoxBase>()
					.Select(x => x.Text).OrderByDescending(x => x.Length).FirstOrDefault()??"");
				string[] arrayLines=text.Replace("\r","").Split('\n').Select(x => x.Trim()).ToArray();
				checkResult.Expect(arrayLines.Contains("Process: 32-bit"),"Diagnostics: 'Process: 32-bit'");
				string lineRuntime=arrayLines.FirstOrDefault(x => x.StartsWith(".NET runtime:"))??"";
				checkResult.Expect(lineRuntime.StartsWith(".NET runtime: CLR 4.0.30319."),"Diagnostics: '"+lineRuntime+"'",lineRuntime);
				string lineRelease=arrayLines.FirstOrDefault(x => x.StartsWith(".NET Framework release:"))??"";
				checkResult.Expect(lineRelease.Contains("(4.8.1 or later)"),"Diagnostics: '"+lineRelease+"'",lineRelease);
				string linePath=arrayLines.FirstOrDefault(x => x.StartsWith("Program path:"))??"";
				checkResult.Expect(string.Equals(linePath.Substring(Math.Min(linePath.Length,"Program path:".Length)).Trim().TrimEnd('\\'),
					AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase),"Diagnostics: 'Program path:' is the Debug folder",linePath);
				checkResult.Note("In process, the Process and .NET lines describe the test host process (32-bit, the same CLR) and Program path comes from "
					+"Application.StartupPath, which the harness sets to the exe's folder");
				checkResult.AddShot(ui.Shot(openedDiagnostics.Hwnd,"A2-diagnostics.png"),"Diagnostics");
				checkResult.Expect(ui.Close(openedDiagnostics.Hwnd),"Diagnostics closes");
			}
			Ui.Opened openedLicense=ui.Open("View Licenses",() => ((Button)FindControl(ui,hwndAbout,"butLicense",isOnThread:true)).PerformClick());
			if(openedLicense.IsOpen) {
				int countLicenses=ui.Invoke(() => ((OpenPmsDental.UI.ListBox)FindControl(ui,openedLicense.Hwnd,"listBoxLicense",true)).Items.Count);
				checkResult.Expect(countLicenses>3,"The License window lists its components ("+countLicenses+" entries)");
				checkResult.AddShot(ui.Shot(openedLicense.Hwnd,"A2-license.png"),"License");
				checkResult.Expect(ui.Close(openedLicense.Hwnd),"License closes");
			}
			checkResult.Expect(ui.Close(hwndAbout),"About closes");
		}
	}
}
