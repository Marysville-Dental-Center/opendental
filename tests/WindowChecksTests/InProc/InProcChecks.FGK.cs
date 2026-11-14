// Groups F, G and K10's in-process scripts: the dashboard and graphs (OpenPmsDentalGraph), the reports (FormReportComplex, the RDL viewer), and
// Help > Update with Update Setup (nothing in it is pressed; it is closed as its X does).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using OpenPmsDental;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;

namespace WindowChecksTests.InProc {
	public static partial class InProcChecks {
		///<summary>The MenuOD of a window (its own menu bar), or null.</summary>
		private static MenuOD FindMenuOD(Ui ui,IntPtr hwnd) {
			return ui.Invoke(() => Ui.AllControls(Control.FromHandle(hwnd)).OfType<MenuOD>().FirstOrDefault());
		}

		public static void CheckF1(Ui ui,CheckResult checkResult) {
			Ui.Opened opened=OpenMenu(ui,"Reports","Graphic");
			if(!InspectWindow(ui,checkResult,opened,"FormDashboardEditTab",null,close:false)) {
				return;
			}
			System.Threading.Thread.Sleep(3000);//the graphs fill in the background
			string title=Native.GetText(opened.Hwnd);
			checkResult.Expect(title=="Graphic Reports - Default","The window's title is 'Graphic Reports - Default' ('"+title+"')");
			int countCells=ui.Invoke(() => Ui.AllControls(Control.FromHandle(opened.Hwnd)).Count(x => x.GetType().Name.Contains("Cell")));
			checkResult.Expect(countCells>0,"The dashboard has its graph cells ("+countCells+")");
			checkResult.AddShot(ui.Shot(opened.Hwnd,"F1.png"),"Dashboard");
			checkResult.ForPerson("the graphs (they may be empty on synthetic data) in F1.png");
			checkResult.Expect(ui.Close(opened.Hwnd),"Dashboard closes");
		}

		///<summary>Reports > Graphic, waited for its graphs; returns the window or zero.</summary>
		private static IntPtr OpenDashboard(Ui ui,CheckResult checkResult) {
			Ui.Opened opened=OpenMenu(ui,"Reports","Graphic");
			if(!InspectWindow(ui,checkResult,opened,"FormDashboardEditTab",null,close:false)) {
				return IntPtr.Zero;
			}
			System.Threading.Thread.Sleep(3000);//the graphs fill in the background
			return opened.Hwnd;
		}

		///<summary>The dashboard's first cell (DashboardCellCtrl) that holds a graph, on the window's thread.</summary>
		private static Control GetGraphCellOnThread(IntPtr hwndDashboard) {
			return Ui.AllControls(Control.FromHandle(hwndDashboard)).FirstOrDefault(x => x.GetType().Name=="DashboardCellCtrl" && x.Visible
				&& (bool)Member(x,"HasDockedControl"));
		}

		///<summary>Closes the dashboard; a "Discard Changes?" question (unsaved layout changes) is answered OK, which discards them (never Save
		///Changes).</summary>
		private static void CloseDashboard(Ui ui,CheckResult checkResult,IntPtr hwndDashboard) {
			List<IntPtr> listBefore=ui.GetWindows();
			ui.Post("Close the dashboard",() => ((Form)Control.FromHandle(hwndDashboard)).Close());
			System.Diagnostics.Stopwatch stopwatch=System.Diagnostics.Stopwatch.StartNew();
			while(Native.IsWindow(hwndDashboard) && Native.IsWindowVisible(hwndDashboard) && stopwatch.ElapsedMilliseconds<20000) {
				foreach(IntPtr hwndAsk in ui.GetWindows().Where(x => !listBefore.Contains(x) && Native.IsDialogClass(x))) {
					string text=Native.GetDialogMessage(hwndAsk);
					checkResult.Note("Closing the dashboard asked: '"+text.Trim()+"'; answered OK (discard)");
					if(text.Contains("discard")) {
						Native.ClickDialogButton(hwndAsk,"OK");
					}
					else {
						Native.CancelDialog(hwndAsk);
					}
					ui.WaitGone(hwndAsk);
				}
				System.Threading.Thread.Sleep(200);
			}
			checkResult.Expect(ui.WaitGone(hwndDashboard,5000),"The dashboard closes (nothing saved)");
		}

		public static void CheckF2(Ui ui,CheckResult checkResult) {
			IntPtr hwnd=OpenDashboard(ui,checkResult);
			if(hwnd==IntPtr.Zero) {
				return;
			}
			Control cell=ui.Invoke(() => GetGraphCellOnThread(hwnd));
			if(checkResult.Expect(cell!=null,"The dashboard has a cell with a graph")) {
				//Right-click a cell > Edit: the cell's context menu item, clicked by its PerformClick (the menu itself is not shown).
				string itemText=ui.Invoke(() => ((ToolStripMenuItem)Ui.GetField(cell,"editToolStripMenuItem")).Text);
				checkResult.Expect(itemText=="Edit","The cell's context menu has Edit ('"+itemText+"')");
				Ui.Opened openedEdit=ui.Open("The cell's context menu > Edit",() => ((ToolStripMenuItem)Ui.GetField(cell,"editToolStripMenuItem")).PerformClick());
				if(InspectWindow(ui,checkResult,openedEdit,"FormDashboardEditCell",null,close:false)) {
					string title=Native.GetText(openedEdit.Hwnd);
					checkResult.Expect(title.StartsWith("Edit Cell"),"'Edit Cell' opens ('"+title+"')");
					checkResult.AddShot(ui.Shot(openedEdit.Hwnd,"F2.png"),"Edit Cell");
					checkResult.Expect(ui.Close(openedEdit.Hwnd),"Edit Cell closes (Cancel)");
				}
				openedEdit.Posted.Wait();
			}
			CloseDashboard(ui,checkResult,hwnd);
		}

		public static void CheckF3(Ui ui,CheckResult checkResult) {
			IntPtr hwnd=OpenDashboard(ui,checkResult);
			if(hwnd==IntPtr.Zero) {
				return;
			}
			Form form=ui.GetForm(hwnd);
			//Setup > Setup: its menu item's handler (GraphicalReportSetup; Admin has it).
			ui.Post("Setup > Setup",() => ((ToolStripMenuItem)Ui.GetField(form,"setupToolStripMenuItem")).PerformClick()).Wait();
			string title=Native.GetText(hwnd);
			checkResult.Expect(title.EndsWith("(Edit)"),"Setup > Setup: the title gets '(Edit)' ('"+title+"')");
			string setupText=ui.Invoke(() => ((ToolStripMenuItem)Ui.GetField(form,"setupToolStripMenuItem")).Text);
			checkResult.Expect(setupText=="Exit Setup","The Setup item now reads 'Exit Setup'");
			//Double-click a tab name: the tab control's MouseDoubleClick handler at the middle of the first tab's header.
			Control dashboardTabCtrl=(Control)Ui.GetField(form,"dashboardTabControl");
			Ui.Opened openedName=ui.Open("Double-click the first tab's name",() => {
				System.Windows.Forms.TabControl tabControl=(System.Windows.Forms.TabControl)Ui.GetField(dashboardTabCtrl,"tabControl");
				System.Drawing.Rectangle rectangle=tabControl.GetTabRect(0);
				MouseEventArgs mouseEventArgs=new MouseEventArgs(MouseButtons.Left,2,rectangle.X+rectangle.Width/2,rectangle.Y+rectangle.Height/2,0);
				Ui.Call(dashboardTabCtrl,"tabControl_MouseDoubleClick",tabControl,mouseEventArgs);
			});
			if(InspectWindow(ui,checkResult,openedName,"FormDashboardNamePrompt",null,close:false)) {
				checkResult.Note("The name prompt: '"+Native.GetText(openedName.Hwnd)+"'");
				checkResult.AddShot(ui.Shot(openedName.Hwnd,"F3.png"),"The tab name prompt");
				checkResult.AddShot(ui.Shot(hwnd,"F3-edit.png"),"The dashboard in Setup mode");
				checkResult.Expect(ui.Close(openedName.Hwnd),"The name prompt closes (Cancel)");
			}
			openedName.Posted.Wait();
			//Setup > Exit Setup.
			List<IntPtr> listBeforeExit=ui.GetWindows();
			ui.Post("Setup > Exit Setup",() => ((ToolStripMenuItem)Ui.GetField(form,"setupToolStripMenuItem")).PerformClick()).Wait();
			foreach(IntPtr hwndNew in ui.GetWindows().Where(x => !listBeforeExit.Contains(x))) {
				checkResult.Note("Exit Setup showed: "+ui.DescribeWindow(hwndNew));
				ui.Close(hwndNew);
			}
			title=Native.GetText(hwnd);
			checkResult.Expect(Native.IsWindowVisible(hwnd),"The dashboard is still open after Exit Setup");
			checkResult.Expect(!title.Contains("(Edit)"),"Setup > Exit Setup: the title loses '(Edit)' ('"+title+"'); Save Changes is never pressed");
			int countLayouts=Count("SELECT COUNT(*) FROM dashboardlayout");
			CloseDashboard(ui,checkResult,hwnd);
			checkResult.Expect(Count("SELECT COUNT(*) FROM dashboardlayout")==countLayouts,"No layout saved ("+countLayouts+" dashboardlayout rows)");
		}

		public static void CheckF4(Ui ui,CheckResult checkResult) {
			IntPtr hwnd=OpenDashboard(ui,checkResult);
			if(hwnd==IntPtr.Zero) {
				return;
			}
			Control cell=ui.Invoke(() => GetGraphCellOnThread(hwnd));
			if(checkResult.Expect(cell!=null,"The dashboard has a cell with a graph")) {
				string itemText=ui.Invoke(() => ((ToolStripMenuItem)Ui.GetField(cell,"printToolStripMenuItem")).Text);
				checkResult.Expect(itemText=="Print","The cell's context menu has Print ('"+itemText+"')");
				Ui.Opened openedPrint=ui.Open("The cell's context menu > Print",() => ((ToolStripMenuItem)Ui.GetField(cell,"printToolStripMenuItem")).PerformClick());
				if(InspectWindow(ui,checkResult,openedPrint,"FormPrintSettings",null,close:false)) {
					System.Threading.Thread.Sleep(1500);
					string title=Native.GetText(openedPrint.Hwnd);
					checkResult.Expect(title=="Print Settings","'Print Settings' opens ('"+title+"')");
					checkResult.AddShot(ui.Shot(openedPrint.Hwnd,"F4.png"),"Print Settings");
					//The graph image it previews: the page bitmap it drew the chart and legend into.
					ShotInfo shotInfoPage=ui.Invoke(() => {
						System.Drawing.Bitmap bitmap=Ui.GetField(Control.FromHandle(openedPrint.Hwnd),"_bmpSheet") as System.Drawing.Bitmap;
						return bitmap==null ? new ShotInfo { FileName="F4-image.png",Error="no page bitmap" } : WindowChecksTests.Shot.Bitmap(bitmap,"F4-image.png");
					});
					checkResult.AddShot(shotInfoPage,"The graph image on the page");
					checkResult.Expect(ui.Close(openedPrint.Hwnd),"Print Settings closes (Close; Print... never pressed)");
				}
				openedPrint.Posted.Wait();
			}
			CloseDashboard(ui,checkResult,hwnd);
		}

		public static void CheckG1(Ui ui,CheckResult checkResult) {
			Ui.Opened openedReports=OpenMenu(ui,"Reports","Standard");
			if(!InspectWindow(ui,checkResult,openedReports,"FormReportsMore",null,close:false)) {
				return;
			}
			IntPtr hwndReports=openedReports.Hwnd;
			Ui.Opened openedAppts=ui.Open("Lists > Appointments",() => {
				Form formReportsMore=(Form)Control.FromHandle(hwndReports);
				System.Collections.IList listReports=(System.Collections.IList)Ui.GetField(formReportsMore,"_listDisplayReportsList");
				object displayReport=listReports.Cast<object>().First(x => (string)x.GetType().GetField("InternalName").GetValue(x)=="ODAppointments");
				Ui.Call(formReportsMore,"OpenReportLocalHelper",displayReport,Ui.GetField(formReportsMore,"_listGroupPermissions"),true);
			});
			if(InspectWindow(ui,checkResult,openedAppts,"FormRpAppointments",null,close:false)) {
				IntPtr hwndAppts=openedAppts.Hwnd;
				ui.Invoke(() => {
					FindControl(ui,hwndAppts,"textDateFrom",true).Text="11/08/2026";
					FindControl(ui,hwndAppts,"textDateTo",true).Text="11/09/2026";
					((OpenPmsDental.UI.CheckBox)FindControl(ui,hwndAppts,"checkAllProvs",true)).Checked=true;
				});
				Ui.Opened openedReport=ui.Open("OK",() => ((System.Windows.Forms.Button)FindControl(ui,hwndAppts,"butOK",true)).PerformClick());
				if(InspectWindow(ui,checkResult,openedReport,"FormReportComplex",null,close:false)) {
					System.Threading.Thread.Sleep(1500);
					List<string> listRows=ui.Invoke(() => {
						List<string> listLines=new List<string>();
						object report=Ui.GetField(Control.FromHandle(openedReport.Hwnd),"_myReport");
						foreach(System.Data.DataTable table in FindTables(report,0)) {
							foreach(System.Data.DataRow row in table.Rows) {
								listLines.Add(string.Join("|",row.ItemArray.Select(x => x.ToString())));
							}
						}
						return listLines;
					});
					checkResult.Note("Report rows: "+string.Join("; ",listRows.Take(6)));
					checkResult.Expect(listRows.Count(x => x.Contains("Gatetest") || x.Contains("Stage"))>=2,"The report shows both stage 0 appointments ("+listRows.Count+" rows)");
					checkResult.AddShot(ui.Shot(openedReport.Hwnd,"G1.png"),"The report");
					checkResult.ForPerson("paging through the report's pages (one page here)");
					checkResult.Expect(ui.Close(openedReport.Hwnd),"The report closes");
				}
				openedReport.Posted?.Wait();
				if(Native.IsWindow(hwndAppts)) {
					ui.Close(hwndAppts);
				}
			}
			ui.Close(hwndReports);
		}

		///<summary>The DataTables reachable from the object through its fields (two levels of lists), for reading a report's rows.</summary>
		private static List<System.Data.DataTable> FindTables(object obj,int depth) {
			List<System.Data.DataTable> list=new List<System.Data.DataTable>();
			if(obj==null || depth>4) {
				return list;
			}
			if(obj is System.Data.DataTable dataTable) {
				list.Add(dataTable);
				return list;
			}
			if(obj is string || obj.GetType().IsPrimitive) {
				return list;
			}
			if(obj is System.Collections.IEnumerable enumerable && !(obj is System.Data.DataRow)) {
				foreach(object item in enumerable.Cast<object>().Take(50)) {
					list.AddRange(FindTables(item,depth+1));
				}
				return list;
			}
			if(!(obj.GetType().Namespace??"").StartsWith("OpenPmsDental")) {
				return list;
			}
			foreach(System.Reflection.FieldInfo fieldInfo in obj.GetType().GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public
				|System.Reflection.BindingFlags.NonPublic))
			{
				if(fieldInfo.FieldType.IsPrimitive || fieldInfo.FieldType==typeof(string) || typeof(Control).IsAssignableFrom(fieldInfo.FieldType)) {
					continue;
				}
				list.AddRange(FindTables(fieldInfo.GetValue(obj),depth+1));
			}
			return list.Distinct().ToList();
		}

		public static void CheckG2(Ui ui,CheckResult checkResult) {
			ToolStripMenuItem toolStripMenuItem=ui.Invoke(() => GetMenuItem("Custom Reports","Unsched Treat Plans"));
			Ui.Opened opened=ui.Open("Reports > Custom Reports > Unsched Treat Plans",() => toolStripMenuItem.PerformClick(),allowMessage:true);
			for(int i=0;i<3 && opened.IsOpen;i++) {
				if(Native.IsDialogClass(opened.Hwnd)) {
					checkResult.Note("Message: "+ui.DescribeWindow(opened.Hwnd));
					ui.Close(opened.Hwnd);
					break;
				}
				string typeName=ui.GetWindowTypeName(opened.Hwnd);
				if(typeName=="FormReportCustom") {
					break;
				}
				//A parameter window: accept it.
				checkResult.Note("Parameter window: "+ui.DescribeWindow(opened.Hwnd)+", accepted");
				IntPtr hwndParam=opened.Hwnd;
				List<IntPtr> listBefore=ui.GetWindows();
				Control butOK=FindControl(ui,hwndParam,"butOK");
				ui.Post("Parameter OK",() => ((IButtonControl)butOK).PerformClick());
				opened.Hwnd=ui.WaitForNewWindow(listBefore.Where(x => x!=hwndParam).ToList(),20000);
			}
			if(InspectWindow(ui,checkResult,opened,"FormReportCustom",null,close:false)) {
				System.Threading.Thread.Sleep(3000);
				string viewer=ui.Invoke(() => string.Join(", ",Ui.AllControls(Control.FromHandle(opened.Hwnd)).Where(x => x.Visible && (x.GetType().Namespace??"").StartsWith("fyiReporting"))
					.Select(x => x.GetType().Name).Distinct()));
				checkResult.Expect(viewer!="","The RDL viewer's controls show ("+viewer+")");
				checkResult.AddShot(ui.Shot(opened.Hwnd,"G2-report.png"),"The RDL report");
				checkResult.ForPerson("the report and its page, zoom and export controls in G2-report.png");
				checkResult.Expect(ui.Close(opened.Hwnd),"The report closes");
			}
		}

		public static void CheckK10(Ui ui,CheckResult checkResult) {
			Ui.Opened opened=OpenMenu(ui,"Help","Update");
			if(!InspectWindow(ui,checkResult,opened,"FormUpdate",null,close:false)) {
				return;
			}
			string version=ui.Invoke(() => FindControl(ui,opened.Hwnd,"labelVersion",true).Text);
			string message=ui.Invoke(() => FindControl(ui,opened.Hwnd,"textConnectionMessage",true).Text);
			checkResult.Expect(version.StartsWith("Using Version:"),"'"+version+"'",version);
			checkResult.Expect(message.StartsWith("Checking for updates with Open Dental is not available in this build."),"'"+message+"' (no server contacted)",message);
			checkResult.AddShot(ui.Shot(opened.Hwnd,"K10.png"),"Update");
			MenuOD menuOD=FindMenuOD(ui,opened.Hwnd);
			Ui.Opened openedSetup=ui.Open("Update > menu Setup",() => menuOD.GetMenuItems().OfType<ToolStripMenuItem>().First(x => x.Text.Replace("&","")=="Setup").PerformClick());
			if(InspectWindow(ui,checkResult,openedSetup,"FormUpdateSetup",null,close:false)) {
				checkResult.AddShot(ui.Shot(openedSetup.Hwnd,"K10-setup.png"),"Update Setup");
				checkResult.Expect(ui.Close(openedSetup.Hwnd),"Update Setup closes as its X does (nothing pressed: no Recopy, Change or Save)");
			}
			checkResult.Expect(ui.Close(opened.Hwnd),"Update closes");
		}
	}
}
