// Group B's in-process part: each main module, reached by its module button's click, read from the module's own grids and controls (the
// out-of-process start of each module is the picture the checklist names).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using OpenPmsDental;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;

namespace WindowChecksTests.InProc {
	public static partial class InProcChecks {
		///<summary>The rows of a grid (GridOD or GridOld) as "cell|cell|..." lines.</summary>
		public static List<string> GridRows(Control grid) {
			object listRows=grid.GetType().GetProperty("ListGridRows")?.GetValue(grid);
			List<string> listLines=new List<string>();
			if(listRows is System.Collections.IEnumerable enumerable) {
				foreach(object row in enumerable) {
					System.Collections.IEnumerable cells=(System.Collections.IEnumerable)row.GetType().GetProperty("Cells").GetValue(row);
					listLines.Add(string.Join("|",cells.Cast<object>().Select(x => (string)x.GetType().GetProperty("Text").GetValue(x))));
				}
			}
			return listLines;
		}

		///<summary>The row's value in the column, or "" when the table has no such column.</summary>
		public static string Col(DataRow dataRow,string column) {
			return dataRow.Table.Columns.Contains(column) ? dataRow[column].ToString() : "";
		}

		private static Control ModuleControl(string fieldName) {
			return MainField<Control>(fieldName);
		}

		private static IntPtr MainHwnd(Ui ui) {
			return ui.Invoke(() => FormMain.Handle);
		}

		public static void CheckB1(Ui ui,CheckResult checkResult) {
			GoToModule(ui,EnumModuleType.Appointments);
			checkResult.Expect(GetModule(ui)==EnumModuleType.Appointments,"The Appts module button opens Appointments");
			Control controlAppt=ModuleControl("controlAppt");
			ControlApptPanel controlApptPanel=(ControlApptPanel)Ui.GetField(controlAppt,"contrApptPanel");
			DateTime dateSelected=ui.Invoke(() => controlApptPanel.DateSelected);
			checkResult.Note("Date shown: "+dateSelected.ToString("yyyy-MM-dd")+" (the program opens on today)");
			List<string> listAppts=ui.Invoke(() => controlApptPanel.TableAppointments.Rows.Cast<DataRow>()
				.Select(x => Col(x,"AptDateTime")+" op "+Col(x,"Op")+" "+Col(x,"ProcDescript")).ToList());
			checkResult.Note("Appointments in view: "+string.Join("; ",listAppts));
			string op1=ScratchOpNum("Op 1");
			bool hasAppt=ui.Invoke(() => controlApptPanel.TableAppointments.Rows.Cast<DataRow>().Any(x => Col(x,"Op")==op1
				&& DateTime.TryParse(Col(x,"AptDateTime"),out DateTime dateAppt) && dateAppt==new DateTime(2026,11,9,9,0,0)));
			checkResult.Expect(dateSelected.Date==new DateTime(2026,11,9) && hasAppt,"The day view of 2026-11-09 holds the 9:00 Op 1 (OperatoryNum "+op1+") appointment",
				string.Join("; ",listAppts));
			checkResult.AddShot(ui.Shot(MainHwnd(ui),"B1-appts-day-inproc.png"),"Appointments, day (in process)");
			ui.Invoke(() => Ui.Call(controlAppt,"toggleDayWeek_WeekClick",controlAppt,EventArgs.Empty));
			System.Threading.Thread.Sleep(1500);
			bool isWeekly=ui.Invoke(() => controlApptPanel.IsWeeklyView);
			checkResult.Expect(isWeekly,"Week view (the day/week toggle's Week handler)");
			checkResult.AddShot(ui.Shot(MainHwnd(ui),"B1-appts-week.png"),"Appointments, week");
			ui.Invoke(() => Ui.Call(controlAppt,"toggleDayWeek_DayClick",controlAppt,EventArgs.Empty));
			System.Threading.Thread.Sleep(800);
			checkResult.ForPerson("compare B1-appts-day.png and B1-appts-week.png with stage 0's 03b and 03c");
		}

		///<summary>The OperatoryNum of the operatory with this abbreviation, from the program's cache.</summary>
		private static string ScratchOpNum(string abbr) {
			string key=new string(abbr.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
			Operatory operatory=Operatories.GetDeepCopy().FirstOrDefault(x => new string((x.Abbrev??"").Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant()==key
				|| new string((x.OpName??"").Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant()==key);
			return operatory==null ? "?" : operatory.OperatoryNum.ToString();
		}

		public static void CheckB2(Ui ui,CheckResult checkResult) {
			GoToModule(ui,EnumModuleType.Family);
			checkResult.Expect(GetModule(ui)==EnumModuleType.Family,"The Family module button opens Family");
			Control controlFamily=ModuleControl("controlFamily");
			List<string> listPat=ui.Invoke(() => GridRows((Control)Ui.GetField(controlFamily,"gridPat")));
			List<string> listIns=ui.Invoke(() => GridRows((Control)Ui.GetField(controlFamily,"gridIns")));
			checkResult.Expect(listPat.Any(x => x.Contains("Gatetest")),"Patient information lists Gatetest ("+listPat.Count+" rows)");
			checkResult.Expect(listIns.Any(x => x.Contains("Gatetest Carrier")),"The insurance grid shows the Gatetest Carrier plan",
				string.Join("; ",listIns.Take(5)));
			checkResult.AddShot(ui.Shot(MainHwnd(ui),"B2-family-inproc.png"),"Family (in process)");
		}

		public static void CheckB3(Ui ui,CheckResult checkResult) {
			GoToModule(ui,EnumModuleType.Account);
			checkResult.Expect(GetModule(ui)==EnumModuleType.Account,"The Account module button opens Account");
			Control controlAccount=ModuleControl("controlAccount");
			List<string> listGrids=ui.Invoke(() => Ui.AllControls(controlAccount).Where(x => x.GetType().Name=="GridOD" && x.Visible)
				.Select(x => x.Name+" "+x.Width+"x"+x.Height).ToList());
			checkResult.Note("Visible grids: "+string.Join(", ",listGrids));
			ShotInfo shotInfoGrids=ui.Invoke(() => {
				//Every visible grid paints into a bitmap of its own (its OnPaint), so a grid that cannot draw shows up as blank.
				List<string> listBlank=new List<string>();
				foreach(Control grid in Ui.AllControls(controlAccount).Where(x => x.GetType().Name=="GridOD" && x.Visible && x.Width>0 && x.Height>0)) {
					using Bitmap bitmap=new Bitmap(grid.Width,grid.Height);
					grid.DrawToBitmap(bitmap,new Rectangle(0,0,grid.Width,grid.Height));
					ShotInfo shotInfo=WindowChecksTests.Shot.Bitmap(bitmap,null);
					if(shotInfo.CountColors<3) {
						listBlank.Add(grid.Name);
					}
				}
				return new ShotInfo { FileName=string.Join(",",listBlank) };
			});
			checkResult.Expect(listGrids.Count>0 && shotInfoGrids.FileName=="","Every visible grid draws ("+listGrids.Count+")","blank: "+shotInfoGrids.FileName);
			List<string> listRows=ui.Invoke(() => GridRows((Control)Ui.GetField(controlAccount,"gridAccount")));
			checkResult.Note("Account rows: "+string.Join("; ",listRows.Select(x => x.Replace("||","|"))));
			checkResult.Expect(listRows.Any(x => x.IndexOf("claim",StringComparison.OrdinalIgnoreCase)>=0),"Claim #1 is listed");
			checkResult.Expect(listRows.Any(x => x.Contains("100.00") && x.IndexOf("ins",StringComparison.OrdinalIgnoreCase)>=0),"The $100 insurance payment is listed");
			checkResult.AddShot(ui.Shot(MainHwnd(ui),"B3-account-inproc.png"),"Account (in process)");
		}

		public static void CheckB4(Ui ui,CheckResult checkResult) {
			GoToModule(ui,EnumModuleType.TreatPlan);
			checkResult.Expect(GetModule(ui)==EnumModuleType.TreatPlan,"The Tx Plan module button opens Treatment Plan");
			Control controlTreat=ModuleControl("controlTreat");
			List<string> listPlans=ui.Invoke(() => GridRows((Control)Ui.GetField(controlTreat,"gridPlans")));
			List<string> listRows=ui.Invoke(() => GridRows((Control)Ui.GetField(controlTreat,"gridMain")));
			checkResult.Note("Plans: "+string.Join("; ",listPlans)+"; rows: "+string.Join("; ",listRows.Take(8)));
			checkResult.Expect(listRows.Any(x => x.Contains("D0120")),"The active plan lists D0120");
			checkResult.AddShot(ui.Shot(MainHwnd(ui),"B4-tp-inproc.png"),"Treatment Plan (in process)");
		}

		public static void CheckB5(Ui ui,CheckResult checkResult) {
			GoToModule(ui,EnumModuleType.Chart);
			checkResult.Expect(GetModule(ui)==EnumModuleType.Chart,"The Chart module button opens Chart");
			Control controlChart=ModuleControl("controlChart");
			//SparksToothChart.ToothChartWrapper, by reflection (this project does not reference SparksToothChart).
			Control toothChartWrapper=(Control)Ui.GetField(controlChart,"toothChartWrapper");
			string engine=ui.Invoke(() => toothChartWrapper.GetType().GetProperty("Engine").GetValue(toothChartWrapper)+" (DrawMode "
				+toothChartWrapper.GetType().GetProperty("DrawMode").GetValue(toothChartWrapper)+")");
			checkResult.Expect(engine.StartsWith("DirectX"),"The tooth chart runs in DirectX: "+engine,engine);
			List<string> listFallbacks=ui.Invoke(() => (List<string>)Ui.CallStatic(toothChartWrapper.GetType(),"GetFallbackMessages"));
			checkResult.Expect(listFallbacks.Count==0,"No tooth chart fallback was logged",string.Join("; ",listFallbacks));
			List<string> listProg=ui.Invoke(() => GridRows((Control)Ui.GetField(controlChart,"gridProg")));
			checkResult.Expect(listProg.Any(x => x.Contains("D1110")) && listProg.Any(x => x.Contains("D0120")),"The progress notes list D1110 and D0120",
				string.Join("; ",listProg.Take(6)));
			Control gridProgPageNav=(Control)Ui.GetField(controlChart,"gridProgPageNav");
			string pageNav=ui.Invoke(() => gridProgPageNav.Visible+" "+gridProgPageNav.Width+"x"+gridProgPageNav.Height+", "
				+Ui.AllControls(gridProgPageNav).Count()+" child controls");
			checkResult.Expect(ui.Invoke(() => gridProgPageNav.Visible && gridProgPageNav.Height>0),"The page navigation bar (ODGridPageNav) is under the grid: "+pageNav);
			checkResult.AddShot(ui.ShotDraw(gridProgPageNav,"B5-pagenav.png"),"The page navigation bar with its arrow images");
			//The chart's own picture: the DirectX scene rendered off screen (the Save to Images path).
			ShotInfo shotInfoChart=ui.Invoke(() => {
				using Bitmap bitmap=(Bitmap)Ui.Call(toothChartWrapper,"GetBitmap");
				return WindowChecksTests.Shot.Bitmap(bitmap,"B5-chart-bitmap.png");
			});
			checkResult.AddShot(shotInfoChart,"The tooth chart's DirectX picture (GetBitmap)");
			IntPtr hwndChart=ui.Invoke(() => toothChartWrapper.Handle);
			ShotInfo shotInfoOnScreen=ui.Shot(hwndChart,"B5-chart-onscreen.png");
			checkResult.Note("The chart's window printed (PrintWindow): "+shotInfoOnScreen);
			if(!shotInfoOnScreen.IsDrawn) {
				checkResult.ForPerson("the DirectX chart on the screen: printed from its window it is blank ("+shotInfoOnScreen+"); its off-screen picture "
					+"(B5-chart-bitmap.png) is drawn. Whether the screen shows it needs a person at an unlocked screen");
			}
			checkResult.AddShot(ui.Shot(MainHwnd(ui),"B5-chart-inproc.png"),"Chart (in process)");
		}

		public static void CheckB6(Ui ui,CheckResult checkResult) {
			GoToModule(ui,EnumModuleType.Imaging);
			checkResult.Expect(GetModule(ui)==EnumModuleType.Imaging,"The Imaging module button opens Imaging");
			Control controlImages=ModuleControl("controlImages");
			string state=ui.Invoke(() => controlImages.Visible+", "+Ui.AllControls(controlImages).Count(x => x.Visible)+" visible controls, toolbars: "
				+string.Join(", ",Ui.AllControls(controlImages).Where(x => x.GetType().Name=="ToolBarOD" && x.Visible).Select(x => x.Name)));
			checkResult.Expect(ui.Invoke(() => controlImages.Visible),"Imaging is shown: "+state);
			checkResult.AddShot(ui.Shot(MainHwnd(ui),"B6-images-inproc.png"),"Imaging (in process)");
			checkResult.ForPerson("compare B6-images.png with stage 0's 01 (tree and toolbar)");
		}

		public static void CheckB7(Ui ui,CheckResult checkResult) {
			GoToModule(ui,EnumModuleType.Manage);
			checkResult.Expect(GetModule(ui)==EnumModuleType.Manage,"The Manage module button opens Manage");
			Control controlManage=ModuleControl("controlManage");
			List<string> listButtons=ui.Invoke(() => Ui.AllControls(controlManage).OfType<System.Windows.Forms.Button>().Where(x => x.Visible).Select(x => x.Text).ToList());
			checkResult.Expect(listButtons.Count>=10,"Manage shows its buttons ("+listButtons.Count+": "+string.Join(", ",listButtons)+")");
			bool hasEmpGrid=ui.Invoke(() => ((Control)Ui.GetField(controlManage,"gridEmp")).Visible);
			checkResult.Expect(hasEmpGrid,"The employee list (gridEmp) is shown ("+ui.Invoke(() => GridRows((Control)Ui.GetField(controlManage,"gridEmp")).Count)+" rows)");
			checkResult.AddShot(ui.Shot(MainHwnd(ui),"B7-manage-inproc.png"),"Manage (in process)");
		}
	}
}
