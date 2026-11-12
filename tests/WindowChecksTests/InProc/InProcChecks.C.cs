// Group C's in-process scripts: the forms whose initializer order changed. Each is opened through the handler its click runs (a grid row's
// double-click, a toolbar or module button, a menu item), every tab is selected once, the window is printed, and it is cancelled or closed.
// Nothing is saved; C8's employee and pay period are made by the business layer and deleted again.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Forms;
using OpenPmsDental;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;

namespace WindowChecksTests.InProc {
	public static partial class InProcChecks {
		#region Helpers
		///<summary>Clicks a ToolBarOD's button (by its string tag or its text) as its mouse-up does: OnButtonClicked raises ButtonClick.</summary>
		public static void ClickToolbarButtonOnThread(Control toolBarOD,string tagOrText) {
			System.Collections.IEnumerable buttons=(System.Collections.IEnumerable)toolBarOD.GetType().GetProperty("Buttons").GetValue(toolBarOD);
			object button=buttons.Cast<object>().FirstOrDefault(x => {
				object tag=Member(x,"Tag");
				string text=Member(x,"Text") as string;
				return (tag is string && (string)tag==tagOrText) || (text??"").Replace("&","")==tagOrText;
			});
			if(button==null) {
				throw new Exception("No toolbar button "+tagOrText+" on "+toolBarOD.Name);
			}
			Ui.Call(toolBarOD,"OnButtonClicked",button);
		}

		///<summary>A property's or field's value (any visibility), or null.</summary>
		public static object Member(object obj,string name) {
			System.Reflection.BindingFlags bindingFlags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public
				|System.Reflection.BindingFlags.NonPublic;
			System.Reflection.PropertyInfo propertyInfo=obj.GetType().GetProperty(name,bindingFlags);
			if(propertyInfo!=null) {
				return propertyInfo.GetValue(obj);
			}
			return obj.GetType().GetField(name,bindingFlags)?.GetValue(obj);
		}

		///<summary>Selects the grid's row and raises its CellDoubleClick, as a double-click on the row does (GridOD and GridOld).</summary>
		public static void DoubleClickRowOnThread(Control grid,int row,int col=0) {
			try {
				Ui.Call(grid,"SetAll",false);
			}
			catch(Exception) {
				//No SetAll, or a grid without selection (SelectionMode None).
			}
			try {
				Ui.Call(grid,"SetSelected",row,true);
			}
			catch(Exception) {
				//A grid without selection (SelectionMode None) is double-clicked without selecting.
			}
			Delegate handler=(Delegate)Ui.GetField(grid,"CellDoubleClick");
			if(handler==null) {
				throw new Exception(grid.Name+" has no CellDoubleClick handler");
			}
			handler.DynamicInvoke(grid,new ODGridClickEventArgs(col,row,MouseButtons.Left));
		}

		///<summary>The index of the first row whose cells (joined with |) satisfy the predicate, or -1.</summary>
		public static int FindRow(Control grid,Func<string,bool> predicate) {
			List<string> listRows=GridRows(grid);
			return listRows.FindIndex(x => predicate(x));
		}

		///<summary>Selects every tab of every tab control of the window once (UI.TabControl and WinForms TabControl), waiting for each to draw, and
		///notes the tabs; the first tab is selected again at the end.</summary>
		public static void VisitTabs(Ui ui,IntPtr hwnd,CheckResult checkResult) {
			List<string> listTabs=ui.Invoke(() => {
				List<string> listNames=new List<string>();
				foreach(Control control in Ui.AllControls(Control.FromHandle(hwnd)).Where(x => x.Visible)) {
					if(control is System.Windows.Forms.TabControl tabControl) {
						for(int i=0;i<tabControl.TabCount;i++) {
							tabControl.SelectedIndex=i;
							tabControl.Refresh();
							listNames.Add(tabControl.TabPages[i].Text);
						}
						tabControl.SelectedIndex=0;
					}
					else if(control is OpenPmsDental.UI.TabControl tabControlOD) {
						for(int i=0;i<tabControlOD.TabPages.Count;i++) {
							tabControlOD.SelectedIndex=i;
							tabControlOD.Refresh();
							listNames.Add(tabControlOD.TabPages[i].Text);
						}
						tabControlOD.SelectedIndex=0;
					}
				}
				return listNames;
			});
			if(listTabs.Count>0) {
				checkResult.Verified("Every tab selected once without an error: "+string.Join(", ",listTabs));
			}
		}

		///<summary>Checks the window that opened is of the expected type, visits its tabs, prints it, and (unless close is false) closes it as Cancel
		///does. Returns false when nothing opened.</summary>
		public static bool InspectWindow(Ui ui,CheckResult checkResult,Ui.Opened opened,string typeName,string shot,bool close=true) {
			if(!opened.IsOpen) {
				return false;
			}
			string typeNameOpened=ui.GetWindowTypeName(opened.Hwnd);
			checkResult.Expect(typeNameOpened==typeName || typeNameOpened.EndsWith("/"+typeName),"The window "+typeName+" opens ("+ui.DescribeWindow(opened.Hwnd)+")",
				typeNameOpened);
			VisitTabs(ui,opened.Hwnd,checkResult);
			if(shot!=null) {
				checkResult.AddShot(ui.Shot(opened.Hwnd,shot),typeName);
			}
			if(close) {
				checkResult.Expect(ui.Close(opened.Hwnd),typeName+" closes (Cancel)");
			}
			return true;
		}

		private static int Count(string sql) {
			return PIn.Int(DataCore.GetScalar(sql));
		}
		#endregion Helpers

		public static void CheckC1(Ui ui,CheckResult checkResult) {
			GoToModule(ui,EnumModuleType.Family);
			Control controlFamily=ModuleControl("controlFamily");
			Ui.Opened opened=ui.Open("Family > double-click the patient information",() => {
				Control gridPat=(Control)Ui.GetField(controlFamily,"gridPat");
				DoubleClickRowOnThread(gridPat,0);
			});
			InspectWindow(ui,checkResult,opened,"FormPatientEdit","C1.png");
		}

		public static void CheckC2(Ui ui,CheckResult checkResult) {
			GoToModule(ui,EnumModuleType.Chart);
			Control controlChart=ModuleControl("controlChart");
			Ui.Opened opened=ui.Open("Chart > progress notes > double-click the D1110",() => {
				Control gridProg=(Control)Ui.GetField(controlChart,"gridProg");
				int row=FindRow(gridProg,x => x.Contains("D1110"));
				DoubleClickRowOnThread(gridProg,row);
			});
			if(InspectWindow(ui,checkResult,opened,"FormProcEdit","C2.png",close:false)) {
				string icons=ui.Invoke(() => {
					Control signatureBoxWrapper=FindControl(ui,opened.Hwnd,"signatureBoxWrapper",true);
					Control butTopazSign=Ui.AllControls(signatureBoxWrapper).FirstOrDefault(x => x.Name=="butTopazSign");
					Control butClearSig=Ui.AllControls(signatureBoxWrapper).FirstOrDefault(x => x.Name=="butClearSig");
					return (butTopazSign is System.Windows.Forms.Button b1 && b1.Image!=null ? "pen" : "")+","
						+(butClearSig is System.Windows.Forms.Button b2 && b2.Image!=null ? "X" : "");
				});
				checkResult.Expect(icons=="pen,X","The signature box has its pen (Topaz) and clear (X) buttons with their images",icons);
				Control signature=ui.Invoke(() => FindControl(ui,opened.Hwnd,"signatureBoxWrapper",true));
				ShotInfo shotInfoSignature=ui.ShotDraw(signature,"C2-signature.png");
				checkResult.ListShots.Add(shotInfoSignature.FileName);//an empty signature box is mostly white: kept for a person, not measured
				checkResult.Note("Signature box picture: "+shotInfoSignature);
				checkResult.Expect(ui.Close(opened.Hwnd),"FormProcEdit closes (Cancel)");
			}
		}

		public static void CheckC3(Ui ui,CheckResult checkResult) {
			GoToModule(ui,EnumModuleType.Account);
			int countBefore=Count("SELECT COUNT(*) FROM payment");
			Control controlAccount=ModuleControl("controlAccount");
			Ui.Opened opened=ui.Open("Account > Payment",() => ClickToolbarButtonOnThread((Control)Ui.GetField(controlAccount,"ToolBarMain"),"Payment"));
			//The program asks for the amount first (InputBox); OK with the amount it offers opens the payment.
			if(opened.IsOpen && ui.GetWindowTypeName(opened.Hwnd)=="FormFrame/InputBox") {
				checkResult.AddShot(ui.Shot(opened.Hwnd,"C3-amount.png"),"The amount prompt");
				System.Windows.FrameworkElement frmInput=ui.GetFrm(opened.Hwnd);
				//The amount: OK with an empty box says "Please enter a value.", so 1.00 is typed into the prompt's text box.
				string typed=ui.Invoke(() => {
					object textBox=WpfTree(frmInput).FirstOrDefault(x => x.GetType().Name.StartsWith("Text") && x.GetType().GetProperty("Text")!=null
						&& x.GetType().Namespace!=null && x.GetType().Namespace.StartsWith("WpfControls"));
					if(textBox==null) {
						return "no text box";
					}
					textBox.GetType().GetProperty("Text").SetValue(textBox,"1.00");
					return textBox.GetType().Name;
				});
				checkResult.Note("Amount 1.00 typed into the prompt ("+typed+")");
				List<IntPtr> listBefore=ui.GetWindows();
				ui.Post("Amount prompt OK",() => {
					System.Reflection.MethodInfo methodInfo=frmInput.GetType().GetMethod("butOK_Click",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic
						|System.Reflection.BindingFlags.Public);
					methodInfo.Invoke(frmInput,new object[] { frmInput,new System.Windows.RoutedEventArgs() });
				});
				IntPtr hwndPayment=ui.WaitForNewWindow(listBefore.Where(x => x!=opened.Hwnd).ToList());
				opened.Hwnd=hwndPayment;
				if(hwndPayment==IntPtr.Zero) {
					checkResult.Fail("No payment window after the amount prompt's OK");
				}
			}
			if(opened.IsOpen) {
				checkResult.Expect(Count("SELECT COUNT(*) FROM payment")==countBefore+1,"The payment is inserted on open (payments "+countBefore+" before)");
			}
			InspectWindow(ui,checkResult,opened,"FormPayment","C3.png");
			opened.Posted?.Wait();
			checkResult.Expect(Count("SELECT COUNT(*) FROM payment")==countBefore,"Cancel deletes it again: no new payment");
			List<string> listRows=ui.Invoke(() => GridRows((Control)Ui.GetField(controlAccount,"gridAccount")));
			checkResult.Expect(listRows.Count(x => x.Contains("|Pay|") || x.Contains("Patient Payment"))==0,"The Account grid shows no new payment",
				string.Join("; ",listRows));
		}

		public static void CheckC4(Ui ui,CheckResult checkResult) {
			int countBefore=Count("SELECT COUNT(*) FROM sheet WHERE PatNum=1");
			Ui.Opened openedForms=OpenToolbar(ui,"Form");
			if(!InspectWindow(ui,checkResult,openedForms,"FormPatientForms",null,close:false)) {
				return;
			}
			IntPtr hwndForms=openedForms.Hwnd;
			Ui.Opened openedPicker=ui.Open("Add Sheet",() => Ui.Call(Control.FromHandle(hwndForms),"butAddSheet_Click",null,EventArgs.Empty));
			if(InspectWindow(ui,checkResult,openedPicker,"FrmSheetPicker",null,close:false)) {
				System.Windows.FrameworkElement frm=ui.GetFrm(openedPicker.Hwnd);
				string first=ui.Invoke(() => {
					object listMain=Ui.GetField(frm,"listMain");
					Ui.Call(listMain,"SetSelected",0,true);
					object items=listMain.GetType().GetProperty("Items").GetValue(listMain);
					return items.GetType().GetProperty("Count").GetValue(items).ToString()+" items";
				});
				checkResult.Note("Pick Sheet lists "+first+"; the first is selected");
				Ui.Opened openedFill=ui.Open("Pick the first patient form, OK",() => Ui.Call(frm,"butOK_Click",frm,new System.Windows.RoutedEventArgs()));
				InspectWindow(ui,checkResult,openedFill,"FormSheetFillEdit","C4.png");
				openedPicker.Posted.Wait();
			}
			checkResult.Expect(ui.Close(hwndForms),"The Forms list closes");
			checkResult.Expect(Count("SELECT COUNT(*) FROM sheet WHERE PatNum=1")==countBefore,"No new sheet ("+countBefore+" before)");
		}

		public static void CheckC5(Ui ui,CheckResult checkResult) {
			GoToModule(ui,EnumModuleType.Family);
			Control controlFamily=ModuleControl("controlFamily");
			Ui.Opened openedPlan=ui.Open("Family > double-click the plan",() => DoubleClickRowOnThread((Control)Ui.GetField(controlFamily,"gridIns"),1,1));
			if(!InspectWindow(ui,checkResult,openedPlan,"FormInsPlan",null,close:false)) {
				return;
			}
			IntPtr hwndPlan=openedPlan.Hwnd;
			Ui.Opened openedBenefits=ui.Open("Edit Insurance Plan > double-click the benefit grid",
				() => Ui.Call(Control.FromHandle(hwndPlan),"gridBenefits_DoubleClick",FindControl(ui,hwndPlan,"gridBenefits",true),EventArgs.Empty));
			InspectWindow(ui,checkResult,openedBenefits,"FormInsBenefits","C5.png");
			openedBenefits.Posted?.Wait();
			checkResult.Expect(ui.Close(hwndPlan),"Edit Insurance Plan closes (Cancel)");
		}

		public static void CheckC6(Ui ui,CheckResult checkResult) {
			Ui.Opened openedReports=OpenMenu(ui,"Reports","Standard");
			if(!InspectWindow(ui,checkResult,openedReports,"FormReportsMore",null,close:false)) {
				return;
			}
			IntPtr hwndReports=openedReports.Hwnd;
			Ui.Opened openedRawPop=ui.Open("Public Health > Raw Population Data",() => {
				Form formReportsMore=(Form)Control.FromHandle(hwndReports);
				System.Collections.IList listReports=(System.Collections.IList)Ui.GetField(formReportsMore,"_listDisplayReportsPublicHealth");
				object displayReport=listReports.Cast<object>().First(x => (string)x.GetType().GetField("InternalName").GetValue(x)=="ODRawPopulationData");
				Ui.Call(formReportsMore,"OpenReportLocalHelper",displayReport,Ui.GetField(formReportsMore,"_listGroupPermissions"),true);
			});
			if(InspectWindow(ui,checkResult,openedRawPop,"FormRpPHRawPop",null,close:false)) {
				IntPtr hwndRawPop=openedRawPop.Hwnd;
				ui.Invoke(() => {
					((MonthCalendar)FindControl(ui,hwndRawPop,"date1",true)).SetDate(new DateTime(2026,11,1));
					((MonthCalendar)FindControl(ui,hwndRawPop,"date2",true)).SetDate(new DateTime(2026,11,30));
				});
				Ui.Opened openedMessage=ui.Open("OK without an adjustment type",() => ((System.Windows.Forms.Button)FindControl(ui,hwndRawPop,"butOK",true)).PerformClick(),
					allowMessage:true);
				if(openedMessage.IsOpen) {
					string text=Native.GetDialogMessage(openedMessage.Hwnd);
					checkResult.Expect(text.Contains("At least one adjustment type must be selected."),"Without a type: '"+text+"'",text);
					ui.Close(openedMessage.Hwnd);
				}
				int countTypes=ui.Invoke(() => {
					OpenPmsDental.UI.ListBox listAdjType=(OpenPmsDental.UI.ListBox)FindControl(ui,hwndRawPop,"listAdjType",true);
					if(listAdjType.Items.Count>0) {
						listAdjType.SetSelected(0,true);
					}
					return listAdjType.Items.Count;
				});
				checkResult.Expect(countTypes>0,"Adjustment types are listed ("+countTypes+")");
				Ui.Opened openedQuery=ui.Open("OK with a type",() => ((System.Windows.Forms.Button)FindControl(ui,hwndRawPop,"butOK",true)).PerformClick());
				InspectWindow(ui,checkResult,openedQuery,"FormQuery","C6.png");
				openedQuery.Posted?.Wait();
				ui.Close(hwndRawPop);
			}
			checkResult.Expect(ui.Close(hwndReports),"Standard reports closes");
		}

		public static void CheckC7(Ui ui,CheckResult checkResult) {
			GoToModule(ui,EnumModuleType.Manage);
			Control controlManage=ModuleControl("controlManage");
			Ui.Opened openedDeposits=ui.Open("Manage > Deposits",() => Ui.Call(controlManage,"butDeposit_Click",controlManage,EventArgs.Empty));
			if(!InspectWindow(ui,checkResult,openedDeposits,"FormDeposits",null,close:false)) {
				return;
			}
			IntPtr hwndDeposits=openedDeposits.Hwnd;
			int countBefore=Count("SELECT COUNT(*) FROM deposit");
			Ui.Opened openedEdit=ui.Open("Add",() => ((System.Windows.Forms.Button)FindControl(ui,hwndDeposits,"butAdd",true)).PerformClick());
			InspectWindow(ui,checkResult,openedEdit,"FormDepositEdit","C7.png");
			openedEdit.Posted?.Wait();
			checkResult.Expect(Count("SELECT COUNT(*) FROM deposit")==countBefore,"Cancel leaves no deposit ("+countBefore+" before)");
			checkResult.Expect(ui.Close(hwndDeposits),"Deposits closes");
		}

		public static void CheckC8(Ui ui,CheckResult checkResult) {
			//The seed has no employee and no pay period. Made by the business layer (the Lists > Employees and Time Cards windows are not part of this
			//check), deleted again at the end.
			Employee employee=new Employee { LName="Gatetest",FName="Employee",MiddleI="",ClockStatus="",PhoneExt=0,PayrollID="",WirelessPhone="",EmailWork="",
				EmailPersonal="" };
			PayPeriod payPeriod=new PayPeriod { DateStart=new DateTime(2026,11,1),DateStop=new DateTime(2026,11,14),DatePaycheck=new DateTime(2026,11,15) };
			ui.Invoke(() => {
				Employees.Insert(employee);
				PayPeriods.Insert(payPeriod);
				DataValid.SetInvalid(InvalidType.Employees);
				Cache.Refresh(InvalidType.Employees);
			});
			checkResult.Note("Employee "+employee.EmployeeNum+" and pay period "+payPeriod.PayPeriodNum+" made by the business layer");
			try {
				GoToModule(ui,EnumModuleType.Appointments);
				GoToModule(ui,EnumModuleType.Manage);
				Control controlManage=ModuleControl("controlManage");
				Ui.Opened opened=ui.Open("Manage > select Gatetest, Employee > View Time Card",() => {
					Control gridEmp=(Control)Ui.GetField(controlManage,"gridEmp");
					int row=FindRow(gridEmp,x => x.Contains("Gatetest"));
					if(row<0) {
						throw new Exception("Gatetest, Employee is not in the employee list: "+string.Join("; ",GridRows(gridEmp)));
					}
					Ui.Call(gridEmp,"SetSelected",row,true);
					Ui.Call(controlManage,"gridEmp_CellClick",gridEmp,new ODGridClickEventArgs(0,row,MouseButtons.Left));
					Ui.Call(controlManage,"butTimeCard_Click",controlManage,EventArgs.Empty);
				});
				InspectWindow(ui,checkResult,opened,"FormTimeCard","C8.png");
				opened.Posted?.Wait();
			}
			finally {
				ui.Invoke(() => {
					PayPeriods.Delete(payPeriod);
					Employees.Delete(employee.EmployeeNum);
					DataValid.SetInvalid(InvalidType.Employees);
					Cache.Refresh(InvalidType.Employees);
				});
			}
			checkResult.Expect(Count("SELECT COUNT(*) FROM employee")==0 && Count("SELECT COUNT(*) FROM payperiod")==0,"Undo: the employee and the pay period are gone");
		}

		public static void CheckC9(Ui ui,CheckResult checkResult) {
			GoToModule(ui,EnumModuleType.Appointments);
			Control controlAppt=ModuleControl("controlAppt");
			ControlApptPanel controlApptPanel=(ControlApptPanel)Ui.GetField(controlAppt,"contrApptPanel");
			long aptNum=PIn.Long(DataCore.GetScalar("SELECT AptNum FROM appointment WHERE PatNum=1 AND AptDateTime='2026-11-09 09:00:00'"));
			checkResult.Expect(aptNum>0,"The 2026-11-09 9:00 appointment is AptNum "+aptNum);
			Ui.Opened opened=ui.Open("Appointments > double-click the 9:00 appointment",() => {
				controlApptPanel.SelectedAptNum=aptNum;
				Ui.Call(controlAppt,"contrApptPanel_ApptDoubleClicked",controlApptPanel,new ApptEventArgs { Appt=Appointments.GetOneApt(aptNum) });
			});
			InspectWindow(ui,checkResult,opened,"FormApptEdit","C9.png");
		}

		public static void CheckC10(Ui ui,CheckResult checkResult) {
			Ui.Opened opened=OpenMenu(ui,"Tools","Database Maintenance");
			if(!InspectWindow(ui,checkResult,opened,"FormDatabaseMaintenance",null,close:false)) {
				return;
			}
			List<string> listNames=ui.Invoke(() => {
				GridOD gridMain=(GridOD)FindControl(ui,opened.Hwnd,"gridMain",true);
				int col=Enumerable.Range(0,gridMain.Columns.Count).FirstOrDefault(i => gridMain.Columns[i].Heading=="Name");
				return gridMain.ListGridRows.Select(x => x.Cells[col].Text).ToList();
			});
			List<string> listSorted=listNames.OrderBy(x => x,StringComparer.CurrentCulture).ToList();
			checkResult.Expect(listNames.Count>50 && listNames.SequenceEqual(listSorted),"The checks are listed sorted by name ("+listNames.Count+")",
				"first out of order: "+listNames.Where((x,i) => x!=listSorted[i]).FirstOrDefault());
			checkResult.AddShot(ui.Shot(opened.Hwnd,"C10.png"),"Database Maintenance");
			checkResult.Expect(ui.Close(opened.Hwnd),"Database Maintenance closes (no Check or Fix)");
		}

		public static void CheckC11(Ui ui,CheckResult checkResult) {
			Ui.Opened opened=OpenMenu(ui,"Setup","Family / Insurance","Claim Forms");
			if(!InspectWindow(ui,checkResult,opened,"FormClaimForms",null,close:false)) {
				return;
			}
			List<string> listInternal=ui.Invoke(() => GridRows(FindControl(ui,opened.Hwnd,"gridInternal",true)));
			checkResult.Note("Internal claim forms: "+string.Join("; ",listInternal));
			checkResult.Expect(listInternal.Any(x => x.Contains("ADA")),"The internal list includes the ADA forms");
			checkResult.Expect(listInternal.Any(x => x.Contains("Denti-Cal DC217_2019")),"The internal list includes Denti-Cal DC217_2019");
			checkResult.AddShot(ui.Shot(opened.Hwnd,"C11.png"),"Claim Forms");
			checkResult.Expect(ui.Close(opened.Hwnd),"Claim Forms closes");
		}

		public static void CheckC12(Ui ui,CheckResult checkResult) {
			Ui.Opened opened=OpenMenu(ui,"Setup","Sheets");
			if(InspectWindow(ui,checkResult,opened,"FormSheetDefs",null,close:false)) {
				int countInternal=ui.Invoke(() => GridRows(FindControl(ui,opened.Hwnd,"grid1",true)).Count);
				checkResult.Expect(countInternal>10,"The internal sheets are listed ("+countInternal+")");
				checkResult.AddShot(ui.Shot(opened.Hwnd,"C12-sheets.png"),"Sheets");
				checkResult.Expect(ui.Close(opened.Hwnd),"Sheets closes");
			}
			Ui.Opened openedEForms=OpenMenu(ui,"Setup","eForms");
			if(!InspectWindow(ui,checkResult,openedEForms,"FrmEFormDefs",null,close:false)) {
				return;
			}
			System.Windows.FrameworkElement frmEForms=ui.GetFrm(openedEForms.Hwnd);
			Ui.Opened openedPick=ui.Open("eForms > Add",() => Ui.Call(frmEForms,"butAdd_Click",frmEForms,new System.Windows.RoutedEventArgs()));
			if(InspectWindow(ui,checkResult,openedPick,"FrmEFormPicker",null,close:false)) {
				System.Windows.FrameworkElement frmPick=ui.GetFrm(openedPick.Hwnd);
				int countInternal=ui.Invoke(() => {
					object listMain=Ui.GetField(frmPick,"listMain");
					object items=listMain?.GetType().GetProperty("Items")?.GetValue(listMain);
					return items==null ? -1 : (int)items.GetType().GetProperty("Count").GetValue(items);
				});
				checkResult.Expect(countInternal>0,"Pick eForm lists the internal eForms ("+countInternal+")");
				checkResult.AddShot(ui.Shot(openedPick.Hwnd,"C12-eforms.png"),"Pick eForm");
				checkResult.AddShot(ui.ShotElement(frmPick,"C12-eforms-wpf.png"),"Pick eForm (RenderTargetBitmap)");
				checkResult.Expect(ui.Close(openedPick.Hwnd),"Pick eForm closes (Cancel)");
				openedPick.Posted.Wait();
			}
			checkResult.Expect(ui.Close(openedEForms.Hwnd),"eForms closes");
		}

		public static void CheckC13(Ui ui,CheckResult checkResult) {
			//On the test database no payment program link is enabled, so the window refuses with a message first; that is recorded, then PayConnect
			//is enabled in the copy (the flag only, by the business layer) so the window and its grid can be checked, and disabled again. Opening the
			//window charges nothing; Send is never pressed.
			Ui.Opened openedMessage=ui.Open("Tools > CC Recurring Charges (no payment link enabled)",() => GetMenuItem("Tools","CC Recurring Charges").PerformClick(),
				allowMessage:true);
			if(openedMessage.IsOpen && Native.IsDialogClass(openedMessage.Hwnd)) {
				string text=Native.GetDialogMessage(openedMessage.Hwnd);
				checkResult.Note("Without a payment program link: '"+text+"' (the checklist expects the window: a precondition of the test database)");
				ui.Close(openedMessage.Hwnd);
			}
			else if(openedMessage.IsOpen) {
				ui.Close(openedMessage.Hwnd);
			}
			openedMessage.Posted.Wait();
			ui.CloseStrayWindows(checkResult);
			Program program=Programs.GetCur(ProgramName.PayConnect);
			SetProgramEnabled(ui,program,true);
			try {
				Ui.Opened opened=OpenMenu(ui,"Tools","CC Recurring Charges");
				if(!InspectWindow(ui,checkResult,opened,"FormCreditRecurringCharges",null,close:false)) {
					return;
				}
				checkResult.Expect(ui.Invoke(() => FindControl(ui,opened.Hwnd,"gridMain",true).Visible),"With PayConnect enabled in the copy, the window and its grid show");
				checkResult.AddShot(ui.Shot(opened.Hwnd,"C13.png"),"CC Recurring Charges");
				checkResult.Expect(ui.Close(opened.Hwnd),"CC Recurring Charges closes (nothing sent)");
			}
			finally {
				SetProgramEnabled(ui,program,false);
			}
		}

		private static void SetProgramEnabled(Ui ui,Program program,bool isEnabled) {
			ui.Invoke(() => {
				Program programOld=program.Copy();
				program.Enabled=isEnabled;
				Programs.Update(program,programOld);
				DataValid.SetInvalid(InvalidType.Programs);
				Cache.Refresh(InvalidType.Programs);
			});
		}

		public static void CheckC14(Ui ui,CheckResult checkResult) {
			GoToModule(ui,EnumModuleType.Manage);
			Control controlManage=ModuleControl("controlManage");
			int countBefore=Count("SELECT COUNT(*) FROM statement");
			Ui.Opened opened=ui.Open("Manage > Billing",() => Ui.Call(controlManage,"butBilling_Click",controlManage,EventArgs.Empty));
			InspectWindow(ui,checkResult,opened,"FormBillingOptions","C14.png");
			opened.Posted?.Wait();
			checkResult.Expect(Count("SELECT COUNT(*) FROM statement")==countBefore,"No statement made ("+countBefore+" before)");
			ui.CloseStrayWindows(checkResult);
		}

		public static void CheckC15(Ui ui,CheckResult checkResult) {
			Ui.Opened opened=OpenMenu(ui,"Tools","Wiki");
			if(!InspectWindow(ui,checkResult,opened,"FormWiki",null,close:false)) {
				return;
			}
			//The browser pane: WebView2, filled with the Home page.
			string state="";
			for(int i=0;i<40;i++) {
				state=ui.Invoke(() => {
					Control webView=FindControl(ui,opened.Hwnd,"webViewWiki",true);
					object coreWebView2=webView?.GetType().GetProperty("CoreWebView2")?.GetValue(webView);
					string source=coreWebView2==null ? "" : (string)coreWebView2.GetType().GetProperty("Source").GetValue(coreWebView2);
					return (webView==null ? "no webViewWiki" : webView.GetType().Name+" "+webView.Width+"x"+webView.Height)
						+"; CoreWebView2 "+(coreWebView2==null ? "not ready" : "ready, source "+(source.Length>80 ? source.Substring(0,80) : source));
				});
				if(state.Contains("ready,")) {
					break;
				}
				System.Threading.Thread.Sleep(500);
			}
			System.Threading.Thread.Sleep(2000);
			checkResult.Expect(state.Contains("ready,"),"The WebView2 browser pane starts: "+state,state);
			//The page's text, read from the browser (the script's task completes on the window's thread, which keeps running its loop).
			System.Threading.Tasks.Task<string> taskText=ui.Invoke(() => {
				Control webView=FindControl(ui,opened.Hwnd,"webViewWiki",true);
				object coreWebView2=webView?.GetType().GetProperty("CoreWebView2")?.GetValue(webView);
				return coreWebView2==null ? null
					: (System.Threading.Tasks.Task<string>)Ui.Call(coreWebView2,"ExecuteScriptAsync","document.body ? document.body.innerText : ''");
			});
			string pageText=taskText!=null && taskText.Wait(15000) ? taskText.Result : "(not read)";
			checkResult.Expect(pageText.Contains("Home"),"The browser pane shows the Home page (its text: "+pageText.Replace("\n"," ")+")",pageText);
			ShotInfo shotInfo=ui.Shot(opened.Hwnd,"C15.png");
			checkResult.AddShot(shotInfo,"Wiki");
			checkResult.Expect(ui.Close(opened.Hwnd),"Wiki closes");
		}
	}
}
