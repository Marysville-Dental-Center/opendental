// Groups I and J's in-process scripts (the parts this harness reaches): the tooth chart (the filling entered as Enter Treatment does it, the
// refill, Show Big, Save to Images, Graphics > DirectX) and EHR-0 and EHR-1 (Show Features with its confirmation and restart messages, the
// SNOMED CT window, the e-mail Send drop-down and the Direct certificate window's import buttons). The rest of I and J is in InProcChecks.I.cs
// and InProcChecks.J.cs.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using OpenPmsDental;
using OpenPmsDentalBusiness;

namespace WindowChecksTests.InProc {
	public static partial class InProcChecks {
		#region I
		///<summary>I's prep: Chart > tooth 30 > surface O > TreatPlan > Type Proc Code D2391 > OK, through the chart's own handlers.</summary>
		public static void CheckIPREP(Ui ui,CheckResult checkResult) {
			GoToModule(ui,EnumModuleType.Chart);
			Control controlChart=ModuleControl("controlChart");
			int countBefore=Count("SELECT COUNT(*) FROM procedurelog WHERE PatNum=1 AND ToothNum='30' AND ProcStatus=1");
			string surf=ui.Invoke(() => {
				object relay=Ui.GetField(controlChart,"_toothChartRelay");
				Ui.Call(relay,"SetSelected","30",true);
				Ui.Call(controlChart,"toothChart_ToothSelectionsChanged",Ui.GetField(controlChart,"toothChartWrapper"));
				Ui.Call(controlChart,"butOI_Click",controlChart,EventArgs.Empty);
				return ((Control)Ui.GetField(controlChart,"textSurf")).Text;
			});
			checkResult.Expect(surf=="O","Tooth 30 selected and surface O clicked: the surface box shows "+surf,surf);
			Ui.Posted posted=ui.Post("TreatPlan > Type Proc Code D2391 > OK",() => {
				((RadioButton)Ui.GetField(controlChart,"radioEntryTP")).Checked=true;
				((Control)Ui.GetField(controlChart,"textProcCode")).Text="D2391";
				Ui.Call(controlChart,"butOK_Click",controlChart,EventArgs.Empty);
			});
			System.Threading.Thread.Sleep(2000);
			List<IntPtr> listWindows=ui.GetWindows();
			if(listWindows.Count>0) {
				checkResult.Fail("A window opened instead of adding the filling directly: "+string.Join("; ",listWindows.Select(x => ui.DescribeWindow(x))));
				ui.CloseStrayWindows(checkResult);
			}
			posted.Wait();
			checkResult.Expect(Count("SELECT COUNT(*) FROM procedurelog WHERE PatNum=1 AND ToothNum='30' AND ProcStatus=1")==countBefore+1,
				"The D2391 on 30 O is added (treatment planned) without the Procedure Edit window");
		}

		public static void CheckI1(Ui ui,CheckResult checkResult) {
			GoToModule(ui,EnumModuleType.Account);
			GoToModule(ui,EnumModuleType.Chart);
			Control controlChart=ModuleControl("controlChart");
			Control toothChartWrapper=(Control)Ui.GetField(controlChart,"toothChartWrapper");
			ShotInfo shotInfo=ui.Invoke(() => {
				using Bitmap bitmap=(Bitmap)Ui.Call(toothChartWrapper,"GetBitmap");
				return WindowChecksTests.Shot.Bitmap(bitmap,"I1-chart.png");
			});
			checkResult.AddShot(shotInfo,"The chart after leaving the Chart and coming back (its DirectX picture)");
			checkResult.ForPerson("the filling on 30 in I1-chart.png, and the date slider below the chart (moving it is mouse work)");
			Ui.Opened opened=ui.Open("Tooth Chart > Show Big",() => Ui.Call(controlChart,"menuItemChartBig_Click",controlChart,EventArgs.Empty));
			if(InspectWindow(ui,checkResult,opened,"FormToothChartingBig",null,close:false)) {
				System.Threading.Thread.Sleep(2000);
				checkResult.AddShot(ui.Shot(opened.Hwnd,"I1-big.png"),"The big chart (PrintWindow)");
				checkResult.ForPerson("the big chart is the same picture (a DirectX window printed while the screen is locked may be blank)");
				checkResult.Expect(ui.Close(opened.Hwnd),"The big chart closes");
			}
		}

		public static void CheckI2(Ui ui,CheckResult checkResult) {
			GoToModule(ui,EnumModuleType.Chart);
			Control controlChart=ModuleControl("controlChart");
			int countBefore=Count("SELECT COUNT(*) FROM document WHERE PatNum=1");
			ui.Post("Tooth Chart > Save to Images",() => Ui.Call(controlChart,"menuItemChartSave_Click",controlChart,EventArgs.Empty)).Wait();
			ui.CloseStrayWindows(checkResult);
			int countAfter=Count("SELECT COUNT(*) FROM document WHERE PatNum=1");
			checkResult.Expect(countAfter==countBefore+1,"Save to Images adds a document (the Tooth Charts category)");
			long docNum=PIn.Long(DataCore.GetScalar("SELECT MAX(DocNum) FROM document WHERE PatNum=1"));
			string fileName=DataCore.GetScalar("SELECT FileName FROM document WHERE DocNum="+docNum);
			string folder=ImageStore.GetPatientFolder(Patients.GetPat(1),ImageStore.GetPreferredAtoZpath());
			string path=System.IO.Path.Combine(folder,fileName);
			if(checkResult.Expect(System.IO.File.Exists(path),"Its file is in the patient's folder ("+fileName+")")) {
				using Bitmap bitmap=new Bitmap(path);
				checkResult.AddShot(WindowChecksTests.Shot.Bitmap(bitmap,"I2-images.png"),"The saved image");
			}
			checkResult.ForPerson("the image shows the chart with the filling (I2-images.png); the second save with the per-provider colours (a user "
				+"setting) is not scripted in this run");
			//Undo: the document and its file (and a thumbnail if one was made), by the business layer.
			ui.Invoke(() => {
				Document document=Documents.GetByNum(docNum);
				Documents.Delete(document);
			});
			try {
				System.IO.File.Delete(path);
				string thumbnail=System.IO.Path.Combine(folder,"Thumbnails",fileName);
				if(System.IO.File.Exists(thumbnail)) {
					System.IO.File.Delete(thumbnail);
				}
			}
			catch(Exception ex) {
				checkResult.Note("File not deleted: "+ex.Message);
			}
			checkResult.Expect(Count("SELECT COUNT(*) FROM document WHERE PatNum=1")==countBefore,"Undo: the image is deleted (business layer, in the scratch copy)");
		}

		public static void CheckI7(Ui ui,CheckResult checkResult) {
			Ui.Opened opened=OpenMenu(ui,"File","Graphics");
			if(!InspectWindow(ui,checkResult,opened,"FormGraphics",null,close:false)) {
				return;
			}
			checkResult.AddShot(ui.Shot(opened.Hwnd,"I7.png"),"Graphics");
			IntPtr hwnd=opened.Hwnd;
			ui.Invoke(() => ((RadioButton)FindControl(ui,hwnd,"radioDirectXChart",true)).Checked=true);
			List<IntPtr> listBefore=ui.GetWindows();
			ui.Post("Save",() => ((System.Windows.Forms.Button)FindControl(ui,hwnd,"butSave",true)).PerformClick());
			System.Threading.Thread.Sleep(3000);
			List<IntPtr> listNew=ui.GetWindows().Where(x => !listBefore.Contains(x)).ToList();
			foreach(IntPtr hwndNew in listNew) {
				checkResult.Fail("Save showed: "+ui.DescribeWindow(hwndNew));
				ui.Close(hwndNew);
			}
			checkResult.Expect(ui.WaitGone(hwnd,30000),"DirectX 9 > Save: no error, the window closes (Save ran ToothChartWrapper.TestDirectXFormat)");
			opened.Posted.Wait();
			GoToModule(ui,EnumModuleType.Chart);
			Control toothChartWrapper=(Control)Ui.GetField(ModuleControl("controlChart"),"toothChartWrapper");
			string engine=ui.Invoke(() => toothChartWrapper.GetType().GetProperty("Engine").GetValue(toothChartWrapper).ToString());
			checkResult.Expect(engine=="DirectX","The Chart draws in DirectX again ("+engine+")");
			checkResult.Note("computerpref: "+DataCore.GetScalar("SELECT CONCAT(GraphicsSimple,' ',DirectXFormat) FROM computerpref WHERE ComputerName='"
				+POut.String(CodeBase.ODEnvironment.MachineName)+"'"));
		}

		///<summary>I8's in-process part, in a session started with OPMS_TOOTHCHART_WPF=1: the WPF chart, Show Big (timed), the big window resized.</summary>
		public static void CheckI8BIG(Ui ui,CheckResult checkResult) {
			GoToModule(ui,EnumModuleType.Chart);
			Control controlChart=ModuleControl("controlChart");
			Control toothChartWrapper=(Control)Ui.GetField(controlChart,"toothChartWrapper");
			string engine=ui.Invoke(() => toothChartWrapper.GetType().GetProperty("Engine").GetValue(toothChartWrapper).ToString());
			checkResult.Expect(engine=="Wpf3D","With OPMS_TOOTHCHART_WPF=1 the Chart runs the WPF chart ("+engine+")");
			System.Diagnostics.Stopwatch stopwatch=System.Diagnostics.Stopwatch.StartNew();
			Ui.Opened opened=ui.Open("Tooth Chart > Show Big",() => Ui.Call(controlChart,"menuItemChartBig_Click",controlChart,EventArgs.Empty));
			if(!InspectWindow(ui,checkResult,opened,"FormToothChartingBig",null,close:false)) {
				return;
			}
			checkResult.Note("Show Big: the window after "+stopwatch.Elapsed.TotalSeconds.ToString("0.0")+" s (in process; the test host's memory is not the program's)");
			System.Threading.Thread.Sleep(2500);
			checkResult.AddShot(ui.Shot(opened.Hwnd,"I8-wpf-big.png"),"The big WPF chart");
			Size sizeBefore=ui.Invoke(() => Control.FromHandle(opened.Hwnd).Size);
			//The big window opens maximized: restore it first, then give it a clearly different size (the end of a corner drag).
			ui.Invoke(() => {
				Form form=(Form)Control.FromHandle(opened.Hwnd);
				form.WindowState=FormWindowState.Normal;
				form.Size=new Size(sizeBefore.Width*3/4,sizeBefore.Height*3/4);
			});
			System.Threading.Thread.Sleep(3000);
			ShotInfo shotInfo=ui.Shot(opened.Hwnd,"I8-wpf-big-resized.png");
			checkResult.AddShot(shotInfo,"The big WPF chart after a resize to 3/4 (set by the window's Size, not a corner drag)");
			checkResult.Note("Big window "+sizeBefore.Width+"x"+sizeBefore.Height+" resized to "+shotInfo.Width+"x"+shotInfo.Height);
			checkResult.ForPerson("sharp, not stretched or blurred after the resize (I8-wpf-big-resized.png); a corner drag is mouse work");
			checkResult.Expect(ui.Close(opened.Hwnd),"The big chart closes");
		}

		///<summary>Undo of I's prep: the D2391 deleted (status deleted), as progress notes > Delete does, by the business layer.</summary>
		public static void CheckIUNDO(Ui ui,CheckResult checkResult) {
			long procNum=PIn.Long(DataCore.GetScalar("SELECT MAX(ProcNum) FROM procedurelog WHERE PatNum=1 AND ToothNum='30' AND ProcStatus=1"));
			if(procNum==0) {
				checkResult.Note("No D2391 to delete");
				return;
			}
			ui.Invoke(() => Procedures.Delete(procNum));
			checkResult.Expect(Count("SELECT COUNT(*) FROM procedurelog WHERE ProcNum="+procNum+" AND ProcStatus=6")==1,"The D2391 is deleted (the row stays with status deleted)");
		}
		#endregion I

		#region J
		///<summary>Setup > Advanced Setup > Show Features: EHR on (on is true) or off, with its messages; Save.</summary>
		private static void ShowFeaturesEhr(Ui ui,CheckResult checkResult,bool on,string shotBefore,string shotConfirm) {
			Ui.Opened opened=OpenMenu(ui,"Setup","Advanced Setup","Show Features");
			if(!InspectWindow(ui,checkResult,opened,"FormShowFeatures",null,close:false)) {
				return;
			}
			IntPtr hwnd=opened.Hwnd;
			if(shotBefore!=null) {
				checkResult.AddShot(ui.Shot(hwnd,shotBefore),"Show Features before");
			}
			OpenPmsDental.UI.CheckBox checkEhr=(OpenPmsDental.UI.CheckBox)FindControl(ui,hwnd,"checkEhr");
			checkResult.Expect(ui.Invoke(() => checkEhr.Checked)!=on,"EHR is "+(on ? "unticked" : "ticked")+" before");
			if(on) {
				//Tick, Cancel: unticked, no restart message.
				Ui.Opened openedConfirm=ui.Open("Tick EHR",() => { checkEhr.Checked=true; Ui.Call(Control.FromHandle(hwnd),"checkEhr_Click",checkEhr,EventArgs.Empty); },allowMessage:true);
				if(openedConfirm.IsOpen) {
					string text=Native.GetDialogMessage(openedConfirm.Hwnd);
					checkResult.Note("Confirmation: "+text.Replace("\r"," ").Replace("\n"," "));
					string expected=FormShowFeatures.GetEhrOnConfirmationText();
					checkResult.Expect(Clean2(text)==Clean2(expected),"One confirmation with the program's text",text);
					foreach(string sentence in new[] { "Turning EHR on changes how prescriptions, patient race, required fields and the Chart work, and it shows the EHR tools.",
						"This program is not certified EHR technology. Its measures cannot be used to attest or submit to any program.",
						"A restart is needed for the change to take effect.","Turn EHR on?" })
					{
						checkResult.Expect(Clean2(text).Contains(Clean2(sentence)),"It says '"+sentence+"'",text);
					}
					checkResult.Expect(!Native.GetDialogMessage(openedConfirm.Hwnd).ToLower().Contains("password"),"No password");
					if(shotConfirm!=null) {
						checkResult.AddShot(ui.Shot(openedConfirm.Hwnd,shotConfirm),"The confirmation");
					}
					Native.ClickDialogButton(openedConfirm.Hwnd,"Cancel");
					ui.WaitGone(openedConfirm.Hwnd);
					System.Threading.Thread.Sleep(800);
					List<IntPtr> listAfterCancel=ui.GetWindows().Where(x => Native.IsDialogClass(x)).ToList();
					checkResult.Expect(listAfterCancel.Count==0,"Cancel: no restart message");
					listAfterCancel.ForEach(x => ui.Close(x));
					openedConfirm.Posted.Wait();
					checkResult.Expect(!ui.Invoke(() => checkEhr.Checked),"Cancel: the box unchecks");
				}
				//Tick, OK: the restart message.
				Ui.Opened openedConfirm2=ui.Open("Tick EHR again",() => { checkEhr.Checked=true; Ui.Call(Control.FromHandle(hwnd),"checkEhr_Click",checkEhr,EventArgs.Empty); },allowMessage:true);
				if(openedConfirm2.IsOpen) {
					List<IntPtr> listBefore=ui.GetWindows();
					Native.ClickDialogButton(openedConfirm2.Hwnd,"OK");
					IntPtr hwndRestart=ui.WaitForNewWindow(listBefore.Where(x => x!=openedConfirm2.Hwnd).ToList(),15000);
					string textRestart=hwndRestart==IntPtr.Zero ? "" : Native.GetDialogMessage(hwndRestart);
					checkResult.Expect(textRestart.Contains("You will need to restart the program for the change to take effect."),"OK: 'You will need to restart the program for "
						+"the change to take effect.'",textRestart);
					if(hwndRestart!=IntPtr.Zero) {
						ui.Close(hwndRestart);
					}
					openedConfirm2.Posted.Wait();
				}
			}
			else {
				Ui.Opened openedRestart=ui.Open("Untick EHR",() => { checkEhr.Checked=false; Ui.Call(Control.FromHandle(hwnd),"checkEhr_Click",checkEhr,EventArgs.Empty); },allowMessage:true);
				string text=openedRestart.IsOpen ? Native.GetDialogMessage(openedRestart.Hwnd) : "";
				checkResult.Expect(text.Contains("You will need to restart the program for the change to take effect."),"Untick: the restart message, no confirmation",text);
				if(openedRestart.IsOpen) {
					ui.Close(openedRestart.Hwnd);
				}
				openedRestart.Posted.Wait();
			}
			checkResult.Expect(ui.Invoke(() => checkEhr.Checked)==on,"EHR is "+(on ? "ticked" : "unticked"));
			ui.Post("Save",() => ((System.Windows.Forms.Button)FindControl(ui,hwnd,"butSave",true)).PerformClick());
			checkResult.Expect(ui.WaitGone(hwnd,20000),"Save closes Show Features");
			opened.Posted.Wait();
			ui.CloseStrayWindows(checkResult);
			checkResult.Expect(DataCore.GetScalar("SELECT ValueString FROM preference WHERE PrefName='ShowFeatureEhr'")==(on ? "1" : "0"),"ShowFeatureEhr is "+(on ? "1" : "0"));
		}

		private static string Clean2(string text) {
			return string.Join(" ",(text??"").Split(new[] { ' ','\r','\n' },StringSplitOptions.RemoveEmptyEntries));
		}

		public static void CheckJ1(Ui ui,CheckResult checkResult) {
			ShowFeaturesEhr(ui,checkResult,on:true,shotBefore:"J1-before.png",shotConfirm:"J1-confirm.png");
		}

		public static void CheckJ16(Ui ui,CheckResult checkResult) {
			ShowFeaturesEhr(ui,checkResult,on:false,shotBefore:null,shotConfirm:null);
		}

		///<summary>J1's restart: the Chart, printed as J1-chart-ehr.png.</summary>
		public static void CheckJCHARTJ1(Ui ui,CheckResult checkResult) {
			ChartAfterStart(ui,checkResult,"J1-chart-ehr.png");
		}

		///<summary>J16's restart: the Chart, printed as J16-chart.png.</summary>
		public static void CheckJCHARTJ16(Ui ui,CheckResult checkResult) {
			ChartAfterStart(ui,checkResult,"J16-chart.png");
		}

		///<summary>J17's first restart (EHR saved by the wizard): the Chart, printed as J17-chart-ehr.png.</summary>
		public static void CheckJCHARTJ17ON(Ui ui,CheckResult checkResult) {
			ChartAfterStart(ui,checkResult,"J17-chart-ehr.png");
		}

		///<summary>J17's second restart (EHR off again): the Chart, printed as J17-chart-off.png.</summary>
		public static void CheckJCHARTJ17OFF(Ui ui,CheckResult checkResult) {
			ChartAfterStart(ui,checkResult,"J17-chart-off.png");
		}

		///<summary>After a start: the Chart toolbar has (or has not) the EHR button. Each restart prints its own picture (fileName), so one restart's
		///picture never stands for another's.</summary>
		private static void ChartAfterStart(Ui ui,CheckResult checkResult,string fileName) {
			GoToModule(ui,EnumModuleType.Chart);
			Control controlChart=ModuleControl("controlChart");
			List<string> listButtons=ui.Invoke(() => {
				Control toolBar=Ui.AllControls(controlChart).FirstOrDefault(x => x.GetType().Name=="ToolBarOD" && x.Name=="ToolBarMain")
					??Ui.AllControls(controlChart).First(x => x.GetType().Name=="ToolBarOD");
				System.Collections.IEnumerable buttons=(System.Collections.IEnumerable)toolBar.GetType().GetProperty("Buttons").GetValue(toolBar);
				return buttons.Cast<object>().Select(x => (Member(x,"Text") as string)??"").ToList();
			});
			checkResult.Note("Chart toolbar: "+string.Join(", ",listButtons));
			checkResult.ListVerified.Add("EHR button "+(listButtons.Contains("EHR") ? "present" : "absent"));
			checkResult.AddShot(ui.Shot(ui.Invoke(() => FormMain.Handle),fileName),"The Chart after the start (EHR button "
				+(listButtons.Contains("EHR") ? "present" : "absent")+")");
		}

		public static void CheckJ3(Ui ui,CheckResult checkResult) {
			checkResult.Note("snomed rows: "+DataCore.GetScalar("SELECT COUNT(*) FROM snomed"));
			Ui.Opened openedEhr=OpenMenu(ui,"Setup","Chart","EHR");
			if(!InspectWindow(ui,checkResult,openedEhr,"FormEhrSetup",null,close:false)) {
				return;
			}
			IntPtr hwndEhr=openedEhr.Hwnd;
			Control butSnomed=ui.Invoke(() => Ui.AllControls(Control.FromHandle(hwndEhr)).FirstOrDefault(x => x is IButtonControl && x.Text.Replace("&","").StartsWith("SNOMED")));
			if(checkResult.Expect(butSnomed!=null,"Setup > Chart > EHR has the SNOMED CTs button")) {
				Ui.Opened opened=ui.Open("SNOMED CTs",() => ((IButtonControl)butSnomed).PerformClick());
				if(opened.IsOpen) {
					checkResult.Note("SNOMED window: "+ui.DescribeWindow(opened.Hwnd));
					bool hasTools=ui.Invoke(() => {
						Control control=Control.FromHandle(opened.Hwnd);
						if(control!=null) {
							return Ui.AllControls(control).Any(x => x.Visible && x.Text.Contains("One-Time Tools"));
						}
						System.Windows.FrameworkElement frm=Ui.GetFrmOnThread(control);
						return frm!=null && WpfTree(frm).OfType<System.Windows.FrameworkElement>().Any(x => x.IsVisible && (Member(x,"Text") as string??"").Contains("One-Time Tools"));
					});
					checkResult.Expect(!hasTools,"No 'One-Time Tools' group (no SNOMED rows)");
					checkResult.AddShot(ui.Shot(opened.Hwnd,"J3.png"),"SNOMED CTs");
					checkResult.Expect(ui.Close(opened.Hwnd),"SNOMED CTs closes");
				}
			}
			ui.Close(hwndEhr);
			//[DATA, undone in J13] the synthetic SNOMED row J4 to J13 use.
			DataCore.NonQ("INSERT INTO snomed (SnomedCode,Description) VALUES ('TEST0001','Synthetic gate test code')");
			checkResult.Expect(Count("SELECT COUNT(*) FROM snomed WHERE SnomedCode='TEST0001'")==1,"[DATA] The synthetic SNOMED row TEST0001 is inserted (J13 deletes it)");
		}

		public static void CheckJ15(Ui ui,CheckResult checkResult) {
			Ui.Opened opened=OpenToolbar(ui,"Email");
			if(InspectWindow(ui,checkResult,opened,"FormEmailMessageEdit",null,close:false)) {
				List<string> listItems=ui.Invoke(() => {
					List<string> list=new List<string>();
					//The toolbars' buttons and their drop-down menus (the Send button's menu is an ODToolBarButton's DropDownMenu).
					foreach(Control toolBar in Ui.AllControls(Control.FromHandle(opened.Hwnd)).Where(x => x.GetType().Name=="ToolBarOD")) {
						foreach(object button in ((System.Collections.IEnumerable)toolBar.GetType().GetProperty("Buttons").GetValue(toolBar)).Cast<object>()) {
							list.Add(toolBar.Name+" button: "+((Member(button,"Text") as string)??""));
							object dropDownMenu=Member(button,"DropDownMenu");
							ContextMenu contextMenuButton=dropDownMenu as ContextMenu??(dropDownMenu==null ? null : Ui.Call(dropDownMenu,"GetContextMenu") as ContextMenu);
							if(contextMenuButton!=null) {
								list.AddRange(contextMenuButton.MenuItems.Cast<MenuItem>().Where(x => x.Visible).Select(x => toolBar.Name+" menu: "+x.Text.Replace("&","")));
							}
						}
					}
					foreach(Control control in Ui.AllControls(Control.FromHandle(opened.Hwnd)).Where(x => !x.Name.StartsWith("text") && !x.Name.StartsWith("grid"))) {
						object dropDown=Member(control,"ContextMenuStrip")??Member(control,"ContextMenu")??Member(control,"DropDownMenu")??Member(control,"MenuDropDown");
						if(dropDown is ContextMenuStrip contextMenuStrip) {
							list.AddRange(contextMenuStrip.Items.OfType<ToolStripItem>().Where(x => x.Available).Select(x => control.Name+": "+x.Text.Replace("&","")));
						}
						else if(dropDown is ContextMenu contextMenu) {
							list.AddRange(contextMenu.MenuItems.Cast<MenuItem>().Where(x => x.Visible).Select(x => control.Name+": "+x.Text.Replace("&","")));
						}
					}
					return list;
				});
				checkResult.Note("Drop-down items of the window's buttons: "+string.Join("; ",listItems));
				checkResult.Expect(listItems.Any(x => x.EndsWith("menu: Direct Messaging")) && listItems.Any(x => x.EndsWith("menu: Send")),
					"The Send button's drop-down lists Send and Direct Messaging (read from its menu, not opened)",string.Join("; ",listItems));
				checkResult.AddShot(ui.Shot(opened.Hwnd,"J15-send-menu.png"),"The new e-mail (the drop-down itself is not opened headless)");
				checkResult.Expect(ui.Close(opened.Hwnd),"The e-mail is cancelled (nothing sent or saved)");
			}
			int countAddresses=Count("SELECT COUNT(*) FROM emailaddress");
			Ui.Opened openedAddresses=OpenMenu(ui,"Setup","Manage","E-mail");
			if(InspectWindow(ui,checkResult,openedAddresses,"FormEmailAddresses",null,close:false)) {
				IntPtr hwndAddresses=openedAddresses.Hwnd;
				Ui.Opened openedEdit=ui.Open("Add",() => ((IButtonControl)FindControl(ui,hwndAddresses,"butAdd",true)).PerformClick());
				if(InspectWindow(ui,checkResult,openedEdit,"FormEmailAddressEdit",null,close:false)) {
					IntPtr hwndEdit=openedEdit.Hwnd;
					Control butCert=ui.Invoke(() => FindControl(ui,hwndEdit,"butRegisterCertificate",true));
					if(checkResult.Expect(butCert!=null,"The address editor has its Certificate button")) {
						//Since DIRECT-CERT (checklist revised 2026-11-09) the Certificate button needs the Username first: with it empty, a message asks for it.
						ui.Invoke(() => FindControl(ui,hwndEdit,"textUsername",true).Text="");
						Ui.Opened openedAsk=ui.Open("Certificate with the Username empty",() => ((IButtonControl)butCert).PerformClick(),allowMessage:true);
						string textAsk=openedAsk.IsOpen && Native.IsDialogClass(openedAsk.Hwnd) ? Native.GetDialogMessage(openedAsk.Hwnd) : "";
						checkResult.Expect(textAsk.Contains("Enter the email address first."),"Certificate with the Username empty asks for it first: '"+textAsk.Trim()+"'",
							openedAsk.IsOpen ? ui.DescribeWindow(openedAsk.Hwnd) : "nothing opened");
						if(openedAsk.IsOpen) {
							ui.Close(openedAsk.Hwnd);
						}
						openedAsk.Posted.Wait();
						//With the checklist's address in Username the registration window opens.
						ui.Invoke(() => FindControl(ui,hwndEdit,"textUsername",true).Text="gatetest@example.invalid");
						Ui.Opened openedReg=ui.Open("Certificate",() => ((IButtonControl)butCert).PerformClick());
						if(InspectWindow(ui,checkResult,openedReg,"FormEmailCertRegister",null,close:false)) {
							IntPtr hwndReg=openedReg.Hwnd;
							checkResult.Note("The registration window's title: '"+Native.GetText(hwndReg)+"' (the checklist calls it Email Certificate Registration)");
							List<string> listButtons=ui.Invoke(() => Ui.AllControls(Control.FromHandle(hwndReg)).Where(x => x is IButtonControl && x.Visible)
								.Select(x => x.Name+" '"+x.Text.Replace("&","")+"'"+(x.Enabled ? "" : " (disabled)")).ToList());
							checkResult.Note("Buttons: "+string.Join(", ",listButtons));
							string groupCert=ui.Invoke(() => FindControl(ui,hwndReg,"groupCertificate",true)?.Text??"");
							checkResult.Expect(groupCert.Contains(".pfx") && groupCert.Contains(".p12"),"The certificate group asks for the provider's .pfx or .p12 ('"+groupCert+"')");
							checkResult.Expect(listButtons.Any(x => x.StartsWith("butImport 'Import'")),"The .pfx/.p12 import button (Import) is there; it is never pressed",
								string.Join(", ",listButtons));
							checkResult.Expect(listButtons.Any(x => x.StartsWith("butImportAnchor ")),"The trust anchor's Import... button is there; it is never pressed",
								string.Join(", ",listButtons));
							checkResult.Expect(!listButtons.Any(x => x.Contains("Send Code")),"No Send Code button (DIRECT-CERT replaced it)",string.Join(", ",listButtons));
							checkResult.AddShot(ui.Shot(hwndReg,"J15-send-code.png"),"The Direct certificate window (nothing imported)");
							ui.Close(hwndReg);
						}
						openedReg.Posted?.Wait();
					}
					ui.Close(hwndEdit);
				}
				openedEdit.Posted?.Wait();
				ui.Close(hwndAddresses);
			}
			checkResult.Expect(Count("SELECT COUNT(*) FROM emailaddress")==countAddresses,"No address saved ("+countAddresses+" before)");
		}
		#endregion J
	}
}
