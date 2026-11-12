// Group I's scripts for the tooth chart's other hosts, in process: the Treatment Plan's print previews (classic and sheet, I3), the referral
// letter (I4), the patient dashboard widget (I5), graphical perio and its 2D message (I6), and the sheet editor's preview with the undo of I5 and
// I6 (I10). The previews run through the program's own print path (a Debug build previews instead of printing) into its preview windows, which
// are printed with PrintWindow; the chart images the hosts draw are taken from the hosts themselves (GetBitmap, the sheet's parameter, the
// dashboard's picture box). The DirectX on-screen surface prints blank while the screen is locked, so its look stays with a person.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using OpenPmsDental;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;

namespace WindowChecksTests.InProc {
	public static partial class InProcChecks {
		public const string HEADING_SAVED="Gatetest saved";
		public const string DESCRIPTION_LETTER="Gatetest referral letter";

		#region Helpers
		///<summary>Selects the row of the grid as a click does (SetSelected, then its CellClick handler).</summary>
		private static void ClickRowOnThread(Control owner,string gridField,string handler,int row) {
			Control grid=(Control)Ui.GetField(owner,gridField);
			Ui.Call(grid,"SetAll",false);
			Ui.Call(grid,"SetSelected",row,true);
			Ui.Call(owner,handler,grid,new ODGridClickEventArgs(0,row,MouseButtons.Left));
		}

		///<summary>Answers the InputBox (a Frm in a FormFrame) with the text and OK.</summary>
		private static void AnswerInputBox(Ui ui,IntPtr hwnd,string text) {
			System.Windows.FrameworkElement frm=ui.GetFrm(hwnd);
			ui.Invoke(() => {
				System.Collections.IList listControls=(System.Collections.IList)Member(frm,"ListControls");
				object textBox=listControls.Cast<object>().First(x => x.GetType().Name=="TextBox");
				textBox.GetType().GetProperty("Text").SetValue(textBox,text);
			});
			ui.Post("InputBox OK",() => ClickFrm(ui,frm,"butOK_Click"));
			ui.WaitGone(hwnd);
		}

		///<summary>Saves a bitmap the program made (a chart image) and measures it; null is a failure.</summary>
		private static void AddBitmapShot(Ui ui,CheckResult checkResult,Func<Image> funcImage,string fileName,string whatItShows) {
			ShotInfo shotInfo=ui.Invoke(() => {
				Image image=funcImage();
				if(image==null) {
					return new ShotInfo { FileName=fileName,Error="no image" };
				}
				using Bitmap bitmap=new Bitmap(image);
				return WindowChecksTests.Shot.Bitmap(bitmap,fileName);
			});
			checkResult.AddShot(shotInfo,whatItShows);
		}

		///<summary>The pages the classic preview window (FormPrintPreview, WinForms) laid out, read from its PrintPreviewControl.</summary>
		private static int CountPreviewPages(Ui ui,IntPtr hwnd) {
			return ui.Invoke(() => {
				PrintPreviewControl printPreviewControl=Ui.AllControls(Control.FromHandle(hwnd)).OfType<PrintPreviewControl>().FirstOrDefault();
				if(printPreviewControl==null) {
					return -1;
				}
				Array arrayPages=Ui.GetField(printPreviewControl,"pageInfo") as Array;
				return arrayPages?.Length??0;
			});
		}

		///<summary>Treatment Plan > Print TP (the toolbar's Print, which BeginInvokes its handler) and the preview it opens; returns its window.</summary>
		private static Ui.Opened PrintTreatPlan(Ui ui,Control controlTreat) {
			return ui.Open("Treatment Plan > Print TP",() => {
				Control toolBar=(Control)Ui.GetField(controlTreat,"ToolBarMain");
				ClickToolbarButtonOnThread(toolBar,"Print");
			},timeoutMs:90000);
		}

		private static int RowOfPlan(Control controlTreat,Func<TreatPlan,bool> predicate) {
			System.Collections.IList listTreatPlans=(System.Collections.IList)Ui.GetField(controlTreat,"_listTreatPlans");
			for(int i=0;i<listTreatPlans.Count;i++) {
				if(predicate((TreatPlan)listTreatPlans[i])) {
					return i;
				}
			}
			return -1;
		}
		#endregion Helpers

		#region I3
		public static void CheckI3(Ui ui,CheckResult checkResult) {
			int countDocsBefore=Count("SELECT COUNT(*) FROM document WHERE PatNum=1");
			GoToModule(ui,EnumModuleType.TreatPlan);
			Control controlTreat=ModuleControl("controlTreat");
			bool isPromptSave=ui.Invoke(() => PrefC.GetBool(PrefName.TreatPlanPromptSave));
			//The active plan > Save TP (the heading box when TreatPlanPromptSave is on).
			ui.Invoke(() => ClickRowOnThread(controlTreat,"gridPlans","gridPlans_CellClick",RowOfPlan(controlTreat,x => x.TPStatus==TreatPlanStatus.Active)));
			string heading=HEADING_SAVED;
			List<IntPtr> listBefore=ui.GetWindows();
			Ui.Posted postedSave=ui.Post("Save TP",() => Ui.Call(controlTreat,"butSaveTP_Click",null,EventArgs.Empty));
			IntPtr hwndBox=ui.WaitForNewWindow(listBefore,isPromptSave ? 20000 : 3000);
			if(hwndBox!=IntPtr.Zero && ui.GetWindowTypeName(hwndBox).EndsWith("/InputBox")) {
				AnswerInputBox(ui,hwndBox,HEADING_SAVED);
				checkResult.Verified("Save TP: the heading box (TreatPlanPromptSave) answered '"+HEADING_SAVED+"' > OK");
			}
			else if(hwndBox!=IntPtr.Zero) {
				checkResult.Fail("Save TP showed: "+ui.DescribeWindow(hwndBox));
				ui.Close(hwndBox);
			}
			else {
				heading=ui.Invoke(() => ((TreatPlan)((System.Collections.IList)Ui.GetField(controlTreat,"_listTreatPlans"))[RowOfPlan(controlTreat,x => x.TPStatus==TreatPlanStatus.Active)]).Heading);
				checkResult.Note("No heading box (TreatPlanPromptSave off): the saved plan keeps the active plan's heading '"+heading+"'");
			}
			postedSave.Wait(30000);
			ui.CloseStrayWindows(checkResult);
			long treatPlanNum=PIn.Long(DataCore.GetScalar("SELECT MAX(TreatPlanNum) FROM treatplan WHERE PatNum=1 AND TPStatus=0 AND Heading='"+POut.String(heading)+"'"));
			if(!checkResult.Expect(treatPlanNum>0,"The saved plan '"+heading+"' is made (TreatPlanNum "+treatPlanNum+")")) {
				return;
			}
			//The User Query of the checklist, so the plan is dated before 17.1's update date.
			DataCore.NonQ("UPDATE treatplan SET DateTP='2025-12-31' WHERE TreatPlanNum="+treatPlanNum);
			checkResult.Note("User Query's UPDATE treatplan SET DateTP='2025-12-31' run on the saved plan (TreatPlanNum "+treatPlanNum+"); 17.1's update date here: "
				+ui.Invoke(() => UpdateHistories.GetDateForVersion(new Version(17,1,0,0)).ToShortDateString()));
			//Leave Treatment Plan and come back; select the saved plan: the Printing tab.
			GoToModule(ui,EnumModuleType.Account);
			GoToModule(ui,EnumModuleType.TreatPlan);
			try {
				int rowSaved=ui.Invoke(() => RowOfPlan(controlTreat,x => x.TreatPlanNum==treatPlanNum));
				ui.Invoke(() => ClickRowOnThread(controlTreat,"gridPlans","gridPlans_CellClick",rowSaved));
				bool hasPrintTab=ui.Invoke(() => ((OpenPmsDental.UI.TabControl)Ui.GetField(controlTreat,"tabControlShowSort")).TabPages.Contains((OpenPmsDental.UI.TabPage)Ui.GetField(controlTreat,"tabPagePrint")));
				checkResult.Expect(hasPrintTab,"Select '"+heading+"': the Printing tab shows");
				//Tick "Print using classic view" > Print TP: the classic preview with the chart.
				ui.Invoke(() => ((OpenPmsDental.UI.CheckBox)Ui.GetField(controlTreat,"checkPrintClassic")).Checked=true);
				Ui.Opened openedClassic=PrintTreatPlan(ui,controlTreat);
				if(InspectWindow(ui,checkResult,openedClassic,"FormRpPrintPreview",null,close:false)) {
					System.Threading.Thread.Sleep(2500);
					checkResult.AddShot(ui.Shot(openedClassic.Hwnd,"I3-classic.png"),"The classic preview");
					//The classic printout draws its chart into the MigraDoc document while it is made; that chart is not kept, so its look is read in the picture.
					checkResult.ForPerson("the chart on the classic preview's page (I3-classic.png)");
					checkResult.Expect(ui.Close(openedClassic.Hwnd),"The classic preview closes");
				}
				openedClassic.Posted.Wait();
				//Untick > Print TP: the sheet preview with the chart.
				ui.Invoke(() => ((OpenPmsDental.UI.CheckBox)Ui.GetField(controlTreat,"checkPrintClassic")).Checked=false);
				CheckSheetPreview(ui,checkResult,controlTreat,treatPlanNum,"I3-sheet.png","I3-sheet-chart.png","The sheet preview of the saved plan");
				//The active plan: no Printing tab; Print TP: the sheet preview.
				int rowActive=ui.Invoke(() => RowOfPlan(controlTreat,x => x.TPStatus==TreatPlanStatus.Active));
				ui.Invoke(() => ClickRowOnThread(controlTreat,"gridPlans","gridPlans_CellClick",rowActive));
				hasPrintTab=ui.Invoke(() => ((OpenPmsDental.UI.TabControl)Ui.GetField(controlTreat,"tabControlShowSort")).TabPages.Contains((OpenPmsDental.UI.TabPage)Ui.GetField(controlTreat,"tabPagePrint")));
				checkResult.Expect(!hasPrintTab,"Select the active plan: no Printing tab");
				CheckSheetPreview(ui,checkResult,controlTreat,0,"I3-sheet-active.png",null,"The sheet preview of the active plan");
				checkResult.Expect(Count("SELECT COUNT(*) FROM document WHERE PatNum=1")==countDocsBefore,"No new document in Images ("+countDocsBefore+" before)");
			}
			finally {
				//Undo: the saved plan deleted (its ProcTPs with it), as Delete in its window does, by the business layer.
				ui.Invoke(() => {
					TreatPlan treatPlan=TreatPlans.GetOne(treatPlanNum);
					if(treatPlan!=null) {
						ProcTPs.DeleteForTP(treatPlanNum);
						TreatPlans.Delete(treatPlan);
					}
				});
				GoToModule(ui,EnumModuleType.Account);
				GoToModule(ui,EnumModuleType.TreatPlan);
			}
			checkResult.Expect(Count("SELECT COUNT(*) FROM treatplan WHERE TreatPlanNum="+treatPlanNum)==0,"Undo: the saved plan is deleted (business layer)");
		}

		///<summary>Print TP with sheets: the sheet preview (FormPrintPreview) opens with its pages, and the chart image the module gives the sheet
		///(ControlTreat.TreatPlanToSheet's toothChartImg parameter) is drawn.</summary>
		private static void CheckSheetPreview(Ui ui,CheckResult checkResult,Control controlTreat,long treatPlanNum,string shot,string shotChart,string whatItShows) {
			Ui.Opened opened=PrintTreatPlan(ui,controlTreat);
			if(InspectWindow(ui,checkResult,opened,"FormPrintPreview",null,close:false)) {
				System.Threading.Thread.Sleep(2500);
				int countPages=CountPreviewPages(ui,opened.Hwnd);
				checkResult.Expect(countPages>0,whatItShows+": "+countPages+" page(s) laid out");
				checkResult.AddShot(ui.Shot(opened.Hwnd,shot),whatItShows);
				checkResult.Expect(ui.Close(opened.Hwnd),whatItShows+" closes");
			}
			opened.Posted.Wait();
			if(shotChart!=null) {
				AddBitmapShot(ui,checkResult,() => {
					TreatPlan treatPlan=TreatPlans.GetOne(treatPlanNum).Copy();
					treatPlan.ListProcTPs=ProcTPs.RefreshForTP(treatPlanNum);
					Sheet sheet=(Sheet)Ui.Call(controlTreat,"TreatPlanToSheet",treatPlan);
					return SheetParameter.GetParamByName(sheet.Parameters,"toothChartImg")?.ParamValue as Image;
				},shotChart,"The chart image the sheet prints (its toothChartImg parameter)");
			}
		}
		#endregion I3

		#region I4
		public static void CheckI4(Ui ui,CheckResult checkResult) {
			int countSheetDefs=Count("SELECT COUNT(*) FROM sheetdef");
			//Prep: a copy of the internal Referral Letter (as Setup > Sheets > Copy makes it), by the business layer.
			long sheetDefNum=ui.Invoke(() => {
				SheetDef sheetDef=SheetsInternal.GetSheetDef(SheetInternalType.ReferralLetter);
				checkResult.Expect(!sheetDef.SheetFieldDefs.Any(x => x.FieldType==SheetFieldType.Special && x.FieldName=="toothChart"),
					"The internal Referral Letter has no tooth chart field");
				sheetDef.Description=DESCRIPTION_LETTER;
				sheetDef.IsNew=true;
				SheetDefs.InsertOrUpdate(sheetDef);
				DataValid.SetInvalid(InvalidType.Sheets);
				return sheetDef.SheetDefNum;
			});
			long referralNum=0;
			try {
				//The sheet editor: Special > toothChart > Save, and the editor draws the chart field.
				Ui.Opened openedEditor=ui.Open("Setup > Sheets > the copy (its editor)",() => {
					SheetDef sheetDef=SheetDefs.GetSheetDef(sheetDefNum);
					SheetDefs.GetFieldsAndParameters(sheetDef);
					FormSheetDefEdit formSheetDefEdit=new FormSheetDefEdit(sheetDef);
					formSheetDefEdit.ShowDialog();
				});
				if(InspectWindow(ui,checkResult,openedEditor,"FormSheetDefEdit",null,close:false)) {
					IntPtr hwndEditor=openedEditor.Hwnd;
					Ui.Opened openedSpecial=OpenByButton(ui,hwndEditor,"butSpecial");
					if(InspectWindow(ui,checkResult,openedSpecial,"FormSheetFieldSpecial",null,close:false)) {
						string listed=ui.Invoke(() => {
							Form form=(Form)Control.FromHandle(openedSpecial.Hwnd);
							OpenPmsDental.UI.ListBox listBox=(OpenPmsDental.UI.ListBox)Ui.GetField(form,"listBoxAvailable");
							List<SheetFieldDef> listAvailable=listBox.Items.GetAll<SheetFieldDef>();
							int index=listAvailable.FindIndex(x => x.FieldName=="toothChart");
							if(index>=0) {
								listBox.SetSelected(index,true);
								Ui.Call(form,"listBoxAvailable_SelectedIndexChanged",listBox,EventArgs.Empty);
							}
							return string.Join(", ",listAvailable.Select(x => x.FieldName));
						});
						checkResult.Expect(listed.Contains("toothChart"),"Special lists toothChart ("+listed+")");
						ui.Post("Special: Save",() => Ui.Call(Control.FromHandle(openedSpecial.Hwnd),"butSave_Click",null,EventArgs.Empty));
						checkResult.Expect(ui.WaitGone(openedSpecial.Hwnd),"toothChart > Save: the field is placed");
						openedSpecial.Posted.Wait();
					}
					System.Threading.Thread.Sleep(1500);
					AddBitmapShot(ui,checkResult,() => (Image)Ui.GetField(Control.FromHandle(hwndEditor),"_imageToothChart"),"I4-editor-chart.png",
						"The chart the editor draws in the field (FormSheetDefEdit's tooth chart image)");
					checkResult.AddShot(ui.Shot(hwndEditor,"I4-editor.png"),"The sheet editor with the toothChart field");
					List<IntPtr> listBeforeSave=ui.GetWindows();
					ui.Post("Editor: Save",() => Ui.Call(Control.FromHandle(hwndEditor),"butSave_Click",null,EventArgs.Empty));
					if(!ui.WaitGone(hwndEditor,20000)) {
						foreach(IntPtr hwnd in ui.GetWindows().Where(x => !listBeforeSave.Contains(x))) {
							checkResult.Fail("The editor's Save showed: "+ui.DescribeWindow(hwnd));
							ui.Close(hwnd);
						}
					}
					openedEditor.Posted.Wait();
				}
				int countFields=Count("SELECT COUNT(*) FROM sheetfielddef WHERE SheetDefNum="+sheetDefNum+" AND FieldName='toothChart'");
				checkResult.Expect(countFields==1,"The copy is saved with its toothChart field");
				ui.Invoke(() => DataValid.SetInvalid(InvalidType.Sheets));
				//A referral "Gatetest, Referral", attached as From to patient #1 (the referral windows' result, by the business layer).
				referralNum=ui.Invoke(() => {
					Referral referral=new Referral { LName="Gatetest",FName="Referral",IsDoctor=true };
					Referrals.Insert(referral);
					RefAttach refAttach=new RefAttach { PatNum=1,ReferralNum=referral.ReferralNum,RefType=ReferralType.RefFrom,RefDate=DateTime.Today,ItemOrder=0 };
					RefAttaches.Insert(refAttach);
					DataValid.SetInvalid(InvalidType.Referral);
					return referral.ReferralNum;
				});
				checkResult.Note("Referral 'Gatetest, Referral' (ReferralNum "+referralNum+") attached as From to patient #1 by the business layer");
				//Main toolbar Letter (arrow) > the item naming the referral > pick the copy.
				MenuItem menuItemReferral=ui.Invoke(() => {
					ContextMenu menuLetter=(ContextMenu)Ui.GetField(FormMain,"menuLetter");
					Ui.Call(FormMain,"menuLetter_Popup",menuLetter,EventArgs.Empty);
					return menuLetter.MenuItems.Cast<MenuItem>().FirstOrDefault(x => x.Tag is Referral && ((Referral)x.Tag).ReferralNum==referralNum);
				});
				string items=ui.Invoke(() => string.Join(", ",((ContextMenu)Ui.GetField(FormMain,"menuLetter")).MenuItems.Cast<MenuItem>().Select(x => x.Text)));
				if(checkResult.Expect(menuItemReferral!=null,"The Letter drop-down names the referral ("+items+")")) {
					Ui.Opened openedPick=ui.Open("Letter > "+menuItemReferral.Text,() => menuItemReferral.PerformClick());
					if(InspectWindow(ui,checkResult,openedPick,"FrmSheetPicker",null,close:false)) {
						System.Windows.FrameworkElement frmPick=ui.GetFrm(openedPick.Hwnd);
						List<string> listLetters=ui.Invoke(() => {
							object listMain=Ui.GetField(frmPick,"listMain");
							System.Collections.IList listSheetDefs=(System.Collections.IList)Ui.GetField(frmPick,"ListSheetDefs");
							List<string> listNames=listSheetDefs.Cast<SheetDef>().Select(x => x.Description).ToList();
							int index=listNames.IndexOf(DESCRIPTION_LETTER);
							if(index>=0) {
								Ui.Call(listMain,"SetSelected",index,true);
							}
							return listNames;
						});
						checkResult.Note("The letter picker lists: "+string.Join(", ",listLetters));
						checkResult.Expect(listLetters.Contains(DESCRIPTION_LETTER) && !listLetters.Contains("Referral Letter"),
							"The picker lists only custom referral letters (the copy, not the internal 'Referral Letter')",string.Join(", ",listLetters));
						Ui.Opened openedFill=ui.Open("Pick the copy, OK",() => ClickFrm(ui,frmPick,"butOK_Click"));
						if(InspectWindow(ui,checkResult,openedFill,"FormSheetFillEdit",null,close:false)) {
							System.Threading.Thread.Sleep(2000);
							checkResult.AddShot(ui.Shot(openedFill.Hwnd,"I4.png"),"The referral letter's fill window");
							AddBitmapShot(ui,checkResult,() => {
								Sheet sheet=(Sheet)Ui.GetField(Control.FromHandle(openedFill.Hwnd),"SheetCur");
								return sheet==null ? null : SheetParameter.GetParamByName(sheet.Parameters,"toothChartImg")?.ParamValue as Image;
							},"I4-chart.png","The tooth chart the letter shows (its toothChartImg parameter: plain teeth, no procedure completed today)");
							checkResult.Expect(ui.Close(openedFill.Hwnd),"The fill window closes (Cancel)");
						}
						openedPick.Posted.Wait();
					}
				}
			}
			finally {
				//Undo: the attachment, the referral and the copy.
				ui.Invoke(() => {
					if(referralNum!=0) {
						DataCore.NonQ("DELETE FROM refattach WHERE ReferralNum="+referralNum);
						Referrals.Delete(Referrals.GetReferral(referralNum));
						DataValid.SetInvalid(InvalidType.Referral);
					}
					SheetDefs.DeleteObject(sheetDefNum);
					DataValid.SetInvalid(InvalidType.Sheets);
				});
				ui.CloseStrayWindows(checkResult);
			}
			checkResult.Expect(Count("SELECT COUNT(*) FROM sheetdef")==countSheetDefs && Count("SELECT COUNT(*) FROM referral WHERE ReferralNum="+referralNum)==0
				&& Count("SELECT COUNT(*) FROM refattach WHERE PatNum=1")==0,"Undo: the attachment, the referral and the copy are deleted");
			checkResult.Note("The copy, its toothChart field, the referral and its attachment were made by the business layer (the designer's mouse placement and the "
				+"referral windows are not driven); the editor's Special > toothChart > Save, the Letter drop-down, the picker and the fill window ran as clicks run them");
		}
		#endregion I4

		#region I5, I6, I10
		private static long _sheetDefNumWidget;
		private static long _perioExamNum;

		public static void CheckI5(Ui ui,CheckResult checkResult) {
			Ui.Opened openedSetup=OpenMenu(ui,"Tools","Patient Dashboards","Dashboard Setup");
			if(!InspectWindow(ui,checkResult,openedSetup,"FormDashboardWidgetSetup",null,close:false)) {
				return;
			}
			IntPtr hwndSetup=openedSetup.Hwnd;
			string group=ui.Invoke(() => ((OpenPmsDental.UI.ComboBox)FindControl(ui,hwndSetup,"comboUserGroup",true)).GetSelected<UserGroup>()?.Description??"");
			checkResult.Expect(group=="Admin Group" || group.StartsWith("Admin"),"User Group = '"+group+"' (Admin's)");
			int countCustomBefore=ui.Invoke(() => GridRows(FindControl(ui,hwndSetup,"gridCustom",true)).Count);
			int rowInternal=ui.Invoke(() => FindRow(FindControl(ui,hwndSetup,"gridInternal",true),x => x.Contains("Patient Dashboard ToothChart")));
			if(!checkResult.Expect(rowInternal>=0,"The internal grid lists 'Patient Dashboard ToothChart'")) {
				ui.Close(hwndSetup);
				return;
			}
			ui.Invoke(() => {
				Control gridInternal=FindControl(ui,hwndSetup,"gridInternal",true);
				Ui.Call(gridInternal,"SetSelected",rowInternal,true);
			});
			ui.Post("Copy",() => Ui.Call(Control.FromHandle(hwndSetup),"butCopy_Click",null,EventArgs.Empty)).Wait();
			List<string> listCustom=ui.Invoke(() => GridRows(FindControl(ui,hwndSetup,"gridCustom",true)));
			checkResult.Expect(listCustom.Count==countCustomBefore+1 && listCustom.Any(x => x.Contains("Patient Dashboard ToothChart") && x.EndsWith("X")),
				"Copy: the copy appears in the custom grid with Allowed X ("+string.Join("; ",listCustom)+")");
			_sheetDefNumWidget=PIn.Long(DataCore.GetScalar("SELECT MAX(SheetDefNum) FROM sheetdef WHERE SheetType="+(int)SheetTypeEnum.PatientDashboardWidget));
			ui.Post("Save",() => Ui.Call(Control.FromHandle(hwndSetup),"butSave_Click",null,EventArgs.Empty));
			checkResult.Expect(ui.WaitGone(hwndSetup),"Save closes Dashboard Setup");
			openedSetup.Posted.Wait();
			//Tools > Patient Dashboards > "Patient Dashboard ToothChart": the dashboard opens with the chart.
			OpenWidget(ui,checkResult,"I5-open.png","I5-chart.png","The dashboard opens with the chart");
			//Close it (click the checked item).
			ui.Post("Patient Dashboards > the checked item (close)",() => GetDashboardMenuItem().PerformClick()).Wait();
			ui.CloseStrayWindows(checkResult);
			checkResult.Expect(!ui.Invoke(() => (bool)Member(MainField<Control>("userControlDashboard"),"IsInitialized"))
				|| ui.Invoke(() => ((System.Collections.ICollection)Member(MainField<Control>("userControlDashboard"),"ListOpenWidgets")).Count==0),
				"Clicking the checked item closes the dashboard");
			//Another module and back to Chart (a refill without the dashboard), then the widget again: its chart with the filling.
			GoToModule(ui,EnumModuleType.Account);
			GoToModule(ui,EnumModuleType.Chart);
			OpenWidget(ui,checkResult,"I5-reopened.png","I5-reopened-chart.png","The widget opened again shows its chart");
			checkResult.ForPerson("the filling on 30 in I5-reopened-chart.png");
		}

		private static ToolStripMenuItem GetDashboardMenuItem() {
			ToolStripMenuItem menuItemDashboards=GetMenuItem("Tools","Patient Dashboards");
			return menuItemDashboards.DropDownItems.OfType<ToolStripMenuItem>().FirstOrDefault(x => x.Tag is SheetDef && ((SheetDef)x.Tag).SheetDefNum==_sheetDefNumWidget);
		}

		private static void OpenWidget(Ui ui,CheckResult checkResult,string shot,string shotChart,string whatItShows) {
			ToolStripMenuItem menuItem=ui.Invoke(() => GetDashboardMenuItem());
			if(!checkResult.Expect(menuItem!=null,"Tools > Patient Dashboards lists the copy")) {
				return;
			}
			Ui.Opened openedMsg=ui.Open("Patient Dashboards > "+menuItem.Text,() => menuItem.PerformClick(),allowMessage:true,timeoutMs:20000);
			string text=openedMsg.IsOpen && Native.IsDialogClass(openedMsg.Hwnd) ? Native.GetDialogMessage(openedMsg.Hwnd) : "";
			checkResult.Note("Opening the widget said: '"+text.Trim()+"'");
			if(openedMsg.IsOpen) {
				ui.Close(openedMsg.Hwnd);
			}
			openedMsg.Posted.Wait();
			System.Threading.Thread.Sleep(3000);
			Control controlChartPicture=ui.Invoke(() => Ui.AllControls(MainField<Control>("userControlDashboard")).FirstOrDefault(x => x.GetType().Name=="DashToothChart"));
			if(checkResult.Expect(controlChartPicture!=null,"The dashboard holds the tooth chart widget field (DashToothChart)")) {
				AddBitmapShot(ui,checkResult,() => ((PictureBox)controlChartPicture).Image,shotChart,whatItShows+" (its picture)");
			}
			checkResult.AddShot(ui.Shot(ui.Invoke(() => FormMain.Handle),shot),"The main window with the dashboard");
		}

		public static void CheckI6(Ui ui,CheckResult checkResult) {
			GoToModule(ui,EnumModuleType.Chart);
			Control controlChart=ModuleControl("controlChart");
			Ui.Opened openedPerio=OpenChartPerio(ui,controlChart);
			if(!InspectWindow(ui,checkResult,openedPerio,"FormPerio",null,close:false)) {
				return;
			}
			IntPtr hwndPerio=openedPerio.Hwnd;
			int countBefore=Count("SELECT COUNT(*) FROM perioexam WHERE PatNum=1");
			List<IntPtr> listBeforeAdd=ui.GetWindows();
			ui.Post("Perio > Add",() => Ui.Call(Control.FromHandle(hwndPerio),"butAdd_Click",null,EventArgs.Empty)).Wait();
			foreach(IntPtr hwndNew in ui.GetWindows().Where(x => !listBeforeAdd.Contains(x))) {
				checkResult.Fail("Add showed: "+ui.DescribeWindow(hwndNew));
				ui.Close(hwndNew);
			}
			_perioExamNum=PIn.Long(DataCore.GetScalar("SELECT MAX(PerioExamNum) FROM perioexam WHERE PatNum=1"));
			checkResult.Expect(Count("SELECT COUNT(*) FROM perioexam WHERE PatNum=1")==countBefore+1,"Add: a new exam (PerioExamNum "+_perioExamNum+"), selected");
			Ui.Opened openedGraphical=OpenByButton(ui,hwndPerio,"butGraphical");
			if(InspectWindow(ui,checkResult,openedGraphical,"FormPerioGraphical",null,close:false)) {
				System.Threading.Thread.Sleep(2500);
				AddBitmapShot(ui,checkResult,() => {
					object toothChartWrapper=Ui.GetField(Control.FromHandle(openedGraphical.Hwnd),"toothChartWrapper");
					return (Image)Ui.Call(toothChartWrapper,"GetBitmap");
				},"I6-perio.png","The graphical perio chart (its DirectX picture)");
				checkResult.AddShot(ui.Shot(openedGraphical.Hwnd,"I6-perio-window.png"),"The graphical perio window (PrintWindow)");
				checkResult.ForPerson("the graphical perio chart on screen (its DirectX surface prints blank while the screen is locked; I6-perio.png is its own picture)");
				checkResult.Expect(ui.Close(openedGraphical.Hwnd),"The graphical perio window closes");
			}
			openedGraphical.Posted.Wait();
			ui.Close(hwndPerio);
			openedPerio.Posted.Wait();
			//File > Graphics > Simple 2D > Save.
			Ui.Opened openedGraphics=OpenMenu(ui,"File","Graphics");
			if(InspectWindow(ui,checkResult,openedGraphics,"FormGraphics",null,close:false)) {
				IntPtr hwndGraphics=openedGraphics.Hwnd;
				ui.Invoke(() => ((RadioButton)FindControl(ui,hwndGraphics,"radioSimpleChart",true)).Checked=true);
				ui.Post("Save",() => ((System.Windows.Forms.Button)FindControl(ui,hwndGraphics,"butSave",true)).PerformClick());
				checkResult.Expect(ui.WaitGone(hwndGraphics,30000),"File > Graphics > Simple 2D (pie shapes) > Save");
				openedGraphics.Posted.Wait();
				ui.CloseStrayWindows(checkResult);
			}
			//Perio Chart > the exam > Graphical: the message.
			GoToModule(ui,EnumModuleType.Chart);
			Ui.Opened openedPerio2=OpenChartPerio(ui,ModuleControl("controlChart"));
			if(InspectWindow(ui,checkResult,openedPerio2,"FormPerio",null,close:false)) {
				Ui.Opened openedMsg=OpenByButton(ui,openedPerio2.Hwnd,"butGraphical",allowMessage:true);
				string text=openedMsg.IsOpen && Native.IsDialogClass(openedMsg.Hwnd) ? Native.GetDialogMessage(openedMsg.Hwnd).Trim() : "";
				if(openedMsg.IsOpen) {
					checkResult.AddShot(ui.Shot(openedMsg.Hwnd,"I6-2d-message.png"),"The 2D message");
				}
				ReadAndCloseMessage(ui,openedMsg);
				openedMsg.Posted.Wait();
				checkResult.Expect(text=="In the Graphics setup window, you must first select DirectX.","Graphical with Simple 2D: exactly 'In the Graphics setup window, you "
					+"must first select DirectX.'",text);
				ui.CloseStrayWindows(checkResult);
			}
			checkResult.Note("The exam stays until I10, the 2D setting until I7");
		}

		private static Ui.Opened OpenChartPerio(Ui ui,Control controlChart) {
			return ui.Open("Chart > Perio Chart",() => {
				Control toolBar=Ui.AllControls(controlChart).FirstOrDefault(x => x.GetType().Name=="ToolBarOD" && x.Name=="ToolBarMain")
					??Ui.AllControls(controlChart).First(x => x.GetType().Name=="ToolBarOD");
				ClickToolbarButtonOnThread(toolBar,"Perio");
			});
		}

		///<summary>I10 in a new session (the program started again): the dashboard closed if open, Dashboard Setup > the copy's Name cell: the sheet
		///editor draws the toothChart field; Delete (confirm); Save. Perio Chart > the I6 exam > Delete.</summary>
		public static void CheckI10(Ui ui,CheckResult checkResult) {
			if(_sheetDefNumWidget==0) {
				_sheetDefNumWidget=PIn.Long(DataCore.GetScalar("SELECT MAX(SheetDefNum) FROM sheetdef WHERE SheetType="+(int)SheetTypeEnum.PatientDashboardWidget));
			}
			bool isOpen=ui.Invoke(() => (bool)Member(MainField<Control>("userControlDashboard"),"IsInitialized")
				&& ((System.Collections.ICollection)Member(MainField<Control>("userControlDashboard"),"ListOpenWidgets")).Count>0);
			checkResult.Note("At the start the dashboard was "+(isOpen ? "open (the user preference I5 left)" : "closed"));
			if(isOpen) {
				ui.Post("Patient Dashboards > the checked item (close)",() => GetDashboardMenuItem().PerformClick()).Wait();
				ui.CloseStrayWindows(checkResult);
			}
			Ui.Opened openedSetup=OpenMenu(ui,"Tools","Patient Dashboards","Dashboard Setup");
			if(InspectWindow(ui,checkResult,openedSetup,"FormDashboardWidgetSetup",null,close:false)) {
				IntPtr hwndSetup=openedSetup.Hwnd;
				int row=ui.Invoke(() => {
					Control gridCustom=FindControl(ui,hwndSetup,"gridCustom",true);
					System.Collections.IList listRows=(System.Collections.IList)Member(gridCustom,"ListGridRows");
					for(int i=0;i<listRows.Count;i++) {
						if(Member(listRows[i],"Tag") is SheetDef sheetDef && sheetDef.SheetDefNum==_sheetDefNumWidget) {
							return i;
						}
					}
					return -1;
				});
				if(checkResult.Expect(row>=0,"The custom grid lists the I5 copy")) {
					Ui.Opened openedEditor=ui.Open("Double-click the copy's Name cell",() => {
						Control gridCustom=FindControl(ui,hwndSetup,"gridCustom",true);
						Ui.Call(gridCustom,"SetSelected",row,true);
						Ui.SetField(gridCustom,"_selectedCell",new Point(0,row));
						DoubleClickRowOnThread(gridCustom,row,0);
					});
					if(InspectWindow(ui,checkResult,openedEditor,"FormSheetDefEdit",null,close:false)) {
						IntPtr hwndEditor=openedEditor.Hwnd;
						System.Threading.Thread.Sleep(2000);
						AddBitmapShot(ui,checkResult,() => (Image)Ui.GetField(Control.FromHandle(hwndEditor),"_imageToothChart"),"I10-chart.png",
							"The chart the sheet editor draws in the toothChart field");
						checkResult.AddShot(ui.Shot(hwndEditor,"I10.png"),"The sheet editor with the toothChart field");
						Ui.Opened openedConfirm=OpenByButton(ui,hwndEditor,"butDelete",allowMessage:true);
						string text=openedConfirm.IsOpen && Native.IsDialogClass(openedConfirm.Hwnd) ? Native.GetDialogMessage(openedConfirm.Hwnd).Trim() : "";
						checkResult.Note("Delete asked: '"+text+"'; confirmed");
						ReadAndCloseMessage(ui,openedConfirm,Native.GetDialogButtons(openedConfirm.Hwnd).Contains("Yes") ? "Yes" : "OK");
						checkResult.Expect(ui.WaitGone(hwndEditor),"Delete (confirmed) closes the editor");
						openedEditor.Posted.Wait();
					}
				}
				ui.Post("Save",() => Ui.Call(Control.FromHandle(hwndSetup),"butSave_Click",null,EventArgs.Empty));
				checkResult.Expect(ui.WaitGone(hwndSetup),"Save closes Dashboard Setup");
				openedSetup.Posted.Wait();
				ui.CloseStrayWindows(checkResult);
			}
			checkResult.Expect(Count("SELECT COUNT(*) FROM sheetdef WHERE SheetDefNum="+_sheetDefNumWidget)==0,"The dashboard copy is deleted (undoes I5)");
			//Perio Chart > the I6 exam > Delete.
			GoToModule(ui,EnumModuleType.Chart);
			Ui.Opened openedPerio=OpenChartPerio(ui,ModuleControl("controlChart"));
			if(InspectWindow(ui,checkResult,openedPerio,"FormPerio",null,close:false)) {
				IntPtr hwndPerio=openedPerio.Hwnd;
				int rowExam=ui.Invoke(() => {
					Control gridExam=FindControl(ui,hwndPerio,"gridODExam",true);
					int count=GridRows(gridExam).Count;
					return count-1;
				});
				ui.Invoke(() => {
					Control gridExam=FindControl(ui,hwndPerio,"gridODExam",true);
					Ui.Call(gridExam,"SetSelected",rowExam,true);
				});
				Ui.Opened openedConfirm=OpenByButton(ui,hwndPerio,"butDelete",allowMessage:true);
				string text=openedConfirm.IsOpen && Native.IsDialogClass(openedConfirm.Hwnd) ? Native.GetDialogMessage(openedConfirm.Hwnd).Trim() : "";
				checkResult.Expect(text.Contains("Delete Exam?"),"Perio > the exam > Delete asks 'Delete Exam?'; OK",text);
				ReadAndCloseMessage(ui,openedConfirm,"OK");
				openedConfirm.Posted.Wait();
				ui.Close(hwndPerio);
			}
			checkResult.Expect(Count("SELECT COUNT(*) FROM perioexam WHERE PatNum=1")==0,"The I6 exam is deleted (undoes I6)");
		}
		#endregion I5, I6, I10
	}
}
