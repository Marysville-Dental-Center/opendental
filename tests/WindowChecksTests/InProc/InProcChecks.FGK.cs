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
			checkResult.Note("The window's title is '"+title+"' (the checklist calls it the Dashboard window)");
			int countCells=ui.Invoke(() => Ui.AllControls(Control.FromHandle(opened.Hwnd)).Count(x => x.GetType().Name.Contains("Cell")));
			checkResult.Expect(countCells>0,"The dashboard has its graph cells ("+countCells+")");
			checkResult.AddShot(ui.Shot(opened.Hwnd,"F1.png"),"Dashboard");
			checkResult.ForPerson("the graphs (they may be empty on synthetic data) in F1.png");
			checkResult.Expect(ui.Close(opened.Hwnd),"Dashboard closes");
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
