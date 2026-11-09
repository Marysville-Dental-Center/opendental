// Groups D and E's in-process scripts: the WPF windows of WpfControlsOD (D) and the capture windows of ImagingCapture (E). A Frm runs in a
// FormFrame; its title bar buttons are the FormFrame's, its controls and images are read from the WPF tree, and it is printed both with
// PrintWindow and with RenderTargetBitmap.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using OpenPmsDental;
using OpenPmsDentalBusiness;

namespace WindowChecksTests.InProc {
	public static partial class InProcChecks {
		#region WPF helpers
		///<summary>Every element of the WPF tree under the root (logical children first, then visual), depth first.</summary>
		public static List<System.Windows.DependencyObject> WpfTree(System.Windows.DependencyObject root) {
			List<System.Windows.DependencyObject> list=new List<System.Windows.DependencyObject>();
			Stack<System.Windows.DependencyObject> stack=new Stack<System.Windows.DependencyObject>();
			stack.Push(root);
			HashSet<System.Windows.DependencyObject> hashSetSeen=new HashSet<System.Windows.DependencyObject>();
			while(stack.Count>0) {
				System.Windows.DependencyObject dependencyObject=stack.Pop();
				if(!hashSetSeen.Add(dependencyObject)) {
					continue;
				}
				list.Add(dependencyObject);
				if(dependencyObject is System.Windows.Media.Visual || dependencyObject is System.Windows.Media.Media3D.Visual3D) {
					for(int i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(dependencyObject);i++) {
						stack.Push(System.Windows.Media.VisualTreeHelper.GetChild(dependencyObject,i));
					}
				}
			}
			return list;
		}

		///<summary>The Frm's button and toolbar images (an Image inside a button or a toolbar: Resources/*.gif): how many, and how many have no
		///picture (blank). Other images (a preview, a histogram) are empty until something is captured, and are left out.</summary>
		public static string WpfImages(System.Windows.FrameworkElement frm,out int countBlank) {
			List<System.Windows.Controls.Image> listImages=WpfTree(frm).OfType<System.Windows.Controls.Image>().Where(x => x.IsVisible && IsInButtonOrToolBar(x,frm))
				.ToList();
			countBlank=listImages.Count(x => x.Source==null || x.ActualWidth<=0);
			int countOther=WpfTree(frm).OfType<System.Windows.Controls.Image>().Count(x => x.IsVisible)-listImages.Count;
			return listImages.Count+" button or toolbar images, "+countOther+" other images";
		}

		private static bool IsInButtonOrToolBar(System.Windows.DependencyObject dependencyObject,System.Windows.FrameworkElement frm) {
			for(System.Windows.DependencyObject parent=System.Windows.Media.VisualTreeHelper.GetParent(dependencyObject);parent!=null && parent!=frm;
				parent=System.Windows.Media.VisualTreeHelper.GetParent(parent))
			{
				string typeName=parent.GetType().Name;
				if(typeName.Contains("Button") || typeName.Contains("ToolBar")) {
					return true;
				}
			}
			return false;
		}

		///<summary>Checks a Frm window: its FormFrame's title bar buttons (min/max), its images drawn, prints it both ways.</summary>
		///<summary>hasMinMax null: the title bar buttons are noted, not checked (the checklist names them only for group D).</summary>
		public static void InspectFrm(Ui ui,CheckResult checkResult,IntPtr hwnd,bool? hasMinMax,string shot) {
			string boxes=ui.Invoke(() => {
				Form form=(Form)Control.FromHandle(hwnd);
				return (form.MinimizeBox ? "min " : "")+(form.MaximizeBox ? "max " : "")+(form.ControlBox ? "close" : "");
			});
			bool isMinMax=boxes.Contains("min") && boxes.Contains("max");
			if(hasMinMax==null) {
				checkResult.Note("Title bar buttons: "+boxes);
			}
			else {
				checkResult.Expect(isMinMax==hasMinMax,"Title bar buttons: "+boxes+(hasMinMax==true ? " (minimize, maximize and close)" : " (Close only: MinMaxBoxes False)"),
					boxes);
			}
			System.Windows.FrameworkElement frm=ui.GetFrm(hwnd);
			int countBlank=0;
			string images=ui.Invoke(() => WpfImages(frm,out countBlank));
			checkResult.Expect(countBlank==0,"No blank image ("+images+")","blank: "+countBlank);
			if(shot!=null) {
				checkResult.AddShot(ui.Shot(hwnd,shot),"The window (PrintWindow)");
				ShotInfo shotInfoWpf=ui.ShotElement(frm,shot.Replace(".png","-wpf.png"));
				checkResult.AddShot(shotInfoWpf,"The Frm (RenderTargetBitmap)");
			}
		}

		public static void ClickFrm(Ui ui,System.Windows.FrameworkElement frm,string handler) {
			Ui.Call(frm,handler,frm,new System.Windows.RoutedEventArgs());
		}
		#endregion WPF helpers

		public static void CheckD1(Ui ui,CheckResult checkResult) {
			Ui.Opened opened=OpenToolbar(ui,"Patient");
			if(!InspectWindow(ui,checkResult,opened,"FrmPatientSelect",null,close:false)) {
				return;
			}
			System.Windows.FrameworkElement frm=ui.GetFrm(opened.Hwnd);
			InspectFrm(ui,checkResult,opened.Hwnd,hasMinMax:false,shot:null);
			int countRows=ui.Invoke(() => {
				object textLName=Ui.GetField(frm,"textLName");
				textLName.GetType().GetProperty("Text").SetValue(textLName,"Gate");
				ClickFrm(ui,frm,"butSearch_Click");
				object gridMain=Ui.GetField(frm,"gridMain");
				return ((System.Collections.ICollection)gridMain.GetType().GetProperty("ListGridRows").GetValue(gridMain)).Count;
			});
			System.Threading.Thread.Sleep(500);
			checkResult.Expect(countRows>=1,"Search 'Gate' lists patient #1 ("+countRows+" rows)");
			checkResult.AddShot(ui.Shot(opened.Hwnd,"D1.png"),"Select Patient (PrintWindow)");
			checkResult.AddShot(ui.ShotElement(frm,"D1-wpf.png"),"Select Patient (RenderTargetBitmap)");
			ui.Invoke(() => {
				object gridMain=Ui.GetField(frm,"gridMain");
				Ui.Call(gridMain,"SetSelected",0,true);
			});
			ui.Post("OK",() => ClickFrm(ui,frm,"butOK_Click"));
			checkResult.Expect(ui.WaitGone(opened.Hwnd),"OK closes the window");
			opened.Posted.Wait();
			checkResult.Expect(FormOpenDental.PatNumCur==1,"Patient #1 stays selected");
		}

		public static void CheckD2(Ui ui,CheckResult checkResult) {
			GoToModule(ui,EnumModuleType.Manage);
			Control controlManage=ModuleControl("controlManage");
			Ui.Opened openedAccounting=ui.Open("Manage > Accounting",() => Ui.Call(controlManage,"butAccounting_Click",controlManage,EventArgs.Empty));
			if(!InspectWindow(ui,checkResult,openedAccounting,"FormAccounting",null,close:false)) {
				return;
			}
			IntPtr hwndAccounting=openedAccounting.Hwnd;
			Ui.Opened opened=ui.Open("Add",() => Ui.Call(Control.FromHandle(hwndAccounting),"Add_Click"));
			if(InspectWindow(ui,checkResult,opened,"FrmAccountEdit",null,close:false)) {
				InspectFrm(ui,checkResult,opened.Hwnd,hasMinMax:false,shot:"D2.png");
				checkResult.Expect(ui.Close(opened.Hwnd),"Edit Account closes (Cancel)");
			}
			checkResult.Expect(ui.Close(hwndAccounting),"Accounting closes");
		}

		public static void CheckD3(Ui ui,CheckResult checkResult) {
			int countBefore=Count("SELECT COUNT(*) FROM commlog WHERE PatNum=1");
			checkResult.Note("CommLogAutoSave is "+PrefC.GetBool(PrefName.CommLogAutoSave));
			Ui.Opened opened=OpenToolbar(ui,"Commlog");
			if(!InspectWindow(ui,checkResult,opened,"FrmCommItem",null,close:false)) {
				return;
			}
			System.Windows.FrameworkElement frm=ui.GetFrm(opened.Hwnd);
			InspectFrm(ui,checkResult,opened.Hwnd,hasMinMax:true,shot:null);
			ui.Invoke(() => {
				object textNote=Ui.GetField(frm,"textNote");
				textNote.GetType().GetProperty("Text").SetValue(textNote,"teh speling");
			});
			System.Threading.Thread.Sleep(1500);
			List<string> listMisspelled=ui.Invoke(() => {
				object textNote=Ui.GetField(frm,"textNote");
				Ui.Call(textNote,"SpellCheck");
				System.Collections.IEnumerable ranges=(System.Collections.IEnumerable)Ui.GetField(textNote,"_listTextRangesMisspelled");
				return ranges==null ? new List<string>() : ranges.Cast<System.Windows.Documents.TextRange>().Select(x => x.Text).ToList();
			});
			checkResult.Expect(listMisspelled.Contains("teh") && listMisspelled.Contains("speling"),"Both words are marked misspelled (the red wavy underline's ranges: "
				+string.Join(", ",listMisspelled)+")");
			checkResult.AddShot(ui.Shot(opened.Hwnd,"D3.png"),"Commlog (PrintWindow)");
			checkResult.AddShot(ui.ShotElement(frm,"D3-wpf.png"),"Commlog (RenderTargetBitmap)");
			checkResult.ForPerson("the red underline's look in D3.png");
			checkResult.Expect(ui.Close(opened.Hwnd),"Commlog closes (Cancel)");
			opened.Posted.Wait();
			checkResult.Expect(Count("SELECT COUNT(*) FROM commlog WHERE PatNum=1")==countBefore,"Nothing saved ("+countBefore+" commlogs before)");
		}

		///<summary>D4 makes an eForm def (kept for D5, which deletes it).</summary>
		private static long _eFormDefNumD4;

		public static void CheckD4(Ui ui,CheckResult checkResult) {
			Ui.Opened openedDefs=OpenMenu(ui,"Setup","eForms");
			if(!InspectWindow(ui,checkResult,openedDefs,"FrmEFormDefs",null,close:false)) {
				return;
			}
			System.Windows.FrameworkElement frmDefs=ui.GetFrm(openedDefs.Hwnd);
			int countBefore=Count("SELECT COUNT(*) FROM eformdef");
			Ui.Opened openedPick=ui.Open("eForms > Add",() => ClickFrm(ui,frmDefs,"butAdd_Click"));
			if(InspectWindow(ui,checkResult,openedPick,"FrmEFormPicker",null,close:false)) {
				System.Windows.FrameworkElement frmPick=ui.GetFrm(openedPick.Hwnd);
				ui.Invoke(() => Ui.Call(Ui.GetField(frmPick,"listMain"),"SetSelected",0,true));
				Ui.Opened openedEdit=ui.Open("Pick the first internal eForm, OK",() => ClickFrm(ui,frmPick,"butOK_Click"));
				if(InspectWindow(ui,checkResult,openedEdit,"FrmEFormDefEdit",null,close:false)) {
					System.Windows.FrameworkElement frmEdit=ui.GetFrm(openedEdit.Hwnd);
					InspectFrm(ui,checkResult,openedEdit.Hwnd,hasMinMax:true,shot:"D4.png");
					string description=ui.Invoke(() => (string)Member(Ui.GetField(frmEdit,"textDescription"),"Text"));
					int countFields=ui.Invoke(() => WpfTree(frmEdit).Count(x => x.GetType().Name.StartsWith("Ctrl")||x.GetType().Name.Contains("Field")));
					checkResult.Expect(countFields>0,"The editor shows the eForm's fields ("+countFields+" field elements)");
					checkResult.Note("eForm name: "+description);
					ui.Post("Save",() => ClickFrm(ui,frmEdit,"butSave_Click"));
					checkResult.Expect(ui.WaitGone(openedEdit.Hwnd),"Save closes the editor");
				}
				openedPick.Posted.Wait();
				ui.CloseStrayWindows(checkResult);
			}
			_eFormDefNumD4=PIn.Long(DataCore.GetScalar("SELECT MAX(EFormDefNum) FROM eformdef"));
			checkResult.Expect(Count("SELECT COUNT(*) FROM eformdef")==countBefore+1,"The eForm def is saved (EFormDefNum "+_eFormDefNumD4+"; D5 deletes it)");
			if(Native.IsWindow(openedDefs.Hwnd)) {
				ui.Close(openedDefs.Hwnd);
			}
		}

		public static void CheckD5(Ui ui,CheckResult checkResult) {
			if(_eFormDefNumD4==0) {
				checkResult.Fail("D4 made no eForm def");
				return;
			}
			int countEFormsBefore=Count("SELECT COUNT(*) FROM eform WHERE PatNum=1");
			Ui.Opened openedForms=OpenToolbar(ui,"Form");
			if(!InspectWindow(ui,checkResult,openedForms,"FormPatientForms",null,close:false)) {
				return;
			}
			IntPtr hwndForms=openedForms.Hwnd;
			Ui.Opened openedPick=ui.Open("Add eForm",() => Ui.Call(Control.FromHandle(hwndForms),"butAddEForm_Click",null,EventArgs.Empty));
			if(InspectWindow(ui,checkResult,openedPick,"FrmEFormPicker",null,close:false)) {
				System.Windows.FrameworkElement frmPick=ui.GetFrm(openedPick.Hwnd);
				string picked=ui.Invoke(() => {
					object listMain=Ui.GetField(frmPick,"listMain");
					object items=Member(listMain,"Items");
					int count=(int)Member(items,"Count");
					//The D4 eForm: the custom one, listed after the internal ones.
					Ui.Call(listMain,"SetSelected",count-1,true);
					return count+" listed, the last selected";
				});
				checkResult.Note("Add eForm picker: "+picked);
				Ui.Opened openedFill=ui.Open("Pick the D4 eForm, OK",() => ClickFrm(ui,frmPick,"butOK_Click"));
				if(InspectWindow(ui,checkResult,openedFill,"FrmEFormFillEdit",null,close:false)) {
					System.Windows.FrameworkElement frmFill=ui.GetFrm(openedFill.Hwnd);
					InspectFrm(ui,checkResult,openedFill.Hwnd,hasMinMax:false,shot:"D5-fill.png");
					Ui.Opened openedPreview=ui.Open("Print",() => ClickFrm(ui,frmFill,"butPrint_Click"),allowMessage:true);
					if(openedPreview.IsOpen && Native.IsDialogClass(openedPreview.Hwnd)) {
						checkResult.Fail("Print showed a message instead of the preview: "+ui.DescribeWindow(openedPreview.Hwnd));
						ui.Close(openedPreview.Hwnd);
					}
					else if(InspectWindow(ui,checkResult,openedPreview,"FrmPrintPreview",null,close:false)) {
						InspectFrm(ui,checkResult,openedPreview.Hwnd,hasMinMax:true,shot:"D5-preview.png");
						checkResult.Expect(ui.Close(openedPreview.Hwnd),"The preview closes");
					}
					checkResult.Expect(ui.Close(openedFill.Hwnd),"The fill window closes (Cancel)");
				}
				openedPick.Posted.Wait();
				ui.CloseStrayWindows(checkResult);
			}
			if(Native.IsWindow(hwndForms)) {
				ui.Close(hwndForms);
			}
			checkResult.Expect(Count("SELECT COUNT(*) FROM eform WHERE PatNum=1")==countEFormsBefore,"Nothing attached to the patient");
			//Undo D4.
			ui.Invoke(() => {
				EFormDefs.Delete(_eFormDefNumD4);
				DataValid.SetInvalid(InvalidType.Sheets);
			});
			checkResult.Expect(Count("SELECT COUNT(*) FROM eformdef WHERE EFormDefNum="+_eFormDefNumD4)==0,"Undo of D4: the eForm def is deleted (by the business layer)");
		}

		#region E
		private static long _imagingDeviceNumMade;

		///<summary>Opens Setup > Imaging > Devices and returns it; makes the "Gatetest device" through Add and Save when none is listed.</summary>
		private static IntPtr OpenDevices(Ui ui,CheckResult checkResult) {
			Ui.Opened opened=OpenMenu(ui,"Setup","Imaging","Devices");
			if(!InspectWindow(ui,checkResult,opened,"FormImagingDevices",null,close:false)) {
				return IntPtr.Zero;
			}
			IntPtr hwnd=opened.Hwnd;
			int countRows=ui.Invoke(() => GridRows(FindControl(ui,hwnd,"gridMain",true)).Count);
			if(countRows==0 && _imagingDeviceNumMade==0) {
				Ui.Opened openedAdd=ui.Open("Devices > Add",() => ((System.Windows.Forms.Button)FindControl(ui,hwnd,"butAdd",true)).PerformClick(),allowMessage:true);
				if(openedAdd.IsOpen && !Native.IsDialogClass(openedAdd.Hwnd)) {
					ui.Invoke(() => {
						FindControl(ui,openedAdd.Hwnd,"textDescription",true).Text="Gatetest device";
					});
					ui.Post("Save",() => ((System.Windows.Forms.Button)FindControl(ui,openedAdd.Hwnd,"butSave",true)).PerformClick());
					checkResult.Expect(ui.WaitGone(openedAdd.Hwnd),"The device 'Gatetest device' is added (Add, Save) [DATA]");
					openedAdd.Posted.Wait();
				}
				else if(openedAdd.IsOpen) {
					checkResult.Note("Add showed: "+ui.DescribeWindow(openedAdd.Hwnd));
					ui.Close(openedAdd.Hwnd);
					openedAdd.Posted.Wait();
					ui.CloseStrayWindows(checkResult);
				}
				_imagingDeviceNumMade=PIn.Long(DataCore.GetScalar("SELECT MAX(ImagingDeviceNum) FROM imagingdevice"));
			}
			return hwnd;
		}

		public static void CheckE1(Ui ui,CheckResult checkResult) {
			IntPtr hwnd=OpenDevices(ui,checkResult);
			if(hwnd==IntPtr.Zero) {
				return;
			}
			Ui.Opened opened=ui.Open("Unassigned captures",() => ((System.Windows.Forms.Button)FindControl(ui,hwnd,"butUnassigned",true)).PerformClick());
			if(InspectWindow(ui,checkResult,opened,"FrmCaptureQuarantine",null,close:false)) {
				InspectFrm(ui,checkResult,opened.Hwnd,hasMinMax:null,shot:"E1.png");
				checkResult.ForPerson("bold, wrapped and multi-line labels in E1.png");
				checkResult.Expect(ui.Close(opened.Hwnd),"Unassigned captures closes");
			}
			ui.Close(hwnd);
		}

		public static void CheckE2(Ui ui,CheckResult checkResult) {
			IntPtr hwnd=OpenDevices(ui,checkResult);
			if(hwnd==IntPtr.Zero) {
				return;
			}
			int countMounts=Count("SELECT COUNT(*) FROM mountdef");
			Ui.Opened opened=ui.Open("Add standard templates",() => ((System.Windows.Forms.Button)FindControl(ui,hwnd,"butStandardTemplates",true)).PerformClick());
			if(InspectWindow(ui,checkResult,opened,"FrmStandardTemplates",null,close:false)) {
				InspectFrm(ui,checkResult,opened.Hwnd,hasMinMax:null,shot:"E2.png");
				checkResult.ForPerson("bold, wrapped and multi-line labels in E2.png");
				checkResult.Expect(ui.Close(opened.Hwnd),"Standard templates closes (Cancel; Add is never pressed)");
			}
			checkResult.Expect(Count("SELECT COUNT(*) FROM mountdef")==countMounts,"No mount made");
			ui.Close(hwnd);
		}

		///<summary>Opens the device's edit window (double-click its row) from Devices.</summary>
		private static IntPtr OpenDevice(Ui ui,CheckResult checkResult,IntPtr hwndDevices) {
			Ui.Opened opened=ui.Open("Devices > the device",() => {
				Control gridMain=FindControl(ui,hwndDevices,"gridMain",true);
				int row=Math.Max(0,FindRow(gridMain,x => x.Contains("Gatetest device")));
				DoubleClickRowOnThread(gridMain,row);
			});
			return InspectWindow(ui,checkResult,opened,"FormImagingDeviceEdit",null,close:false) ? opened.Hwnd : IntPtr.Zero;
		}

		public static void CheckE3(Ui ui,CheckResult checkResult) {
			IntPtr hwnd=OpenDevices(ui,checkResult);
			IntPtr hwndDevice=hwnd==IntPtr.Zero ? IntPtr.Zero : OpenDevice(ui,checkResult,hwnd);
			if(hwndDevice!=IntPtr.Zero) {
				Ui.Opened opened=ui.Open("Capture settings",() => ((System.Windows.Forms.Button)FindControl(ui,hwndDevice,"butCaptureSettings",true)).PerformClick());
				if(InspectWindow(ui,checkResult,opened,"FrmCaptureSettings",null,close:false)) {
					InspectFrm(ui,checkResult,opened.Hwnd,hasMinMax:null,shot:"E3.png");
					checkResult.ForPerson("bold, wrapped and multi-line labels in E3.png");
					checkResult.Expect(ui.Close(opened.Hwnd),"Capture settings closes (Cancel; Save is never pressed)");
				}
				ui.Close(hwndDevice);
			}
			if(hwnd!=IntPtr.Zero) {
				ui.Close(hwnd);
			}
		}

		public static void CheckE4(Ui ui,CheckResult checkResult) {
			IntPtr hwnd=OpenDevices(ui,checkResult);
			IntPtr hwndDevice=hwnd==IntPtr.Zero ? IntPtr.Zero : OpenDevice(ui,checkResult,hwnd);
			if(hwndDevice!=IntPtr.Zero) {
				int countRecords=Count("SELECT COUNT(*) FROM securitylog");
				Ui.Opened opened=ui.Open("Test capture",() => ((System.Windows.Forms.Button)FindControl(ui,hwndDevice,"butTestCapture",true)).PerformClick(),allowMessage:true);
				if(opened.IsOpen && Native.IsDialogClass(opened.Hwnd)) {
					checkResult.Note("Test capture asked: "+ui.DescribeWindow(opened.Hwnd));
					ui.Close(opened.Hwnd);
				}
				else if(InspectWindow(ui,checkResult,opened,"FrmTestCapture",null,close:false)) {
					System.Windows.FrameworkElement frm=ui.GetFrm(opened.Hwnd);
					string labels=ui.Invoke(() => {
						string device=Member(Ui.GetField(frm,"labelDevice"),"Text") as string;
						object labelSettings=Ui.GetField(frm,"labelSettings");
						object labelCheck=Ui.GetField(frm,"labelSettingsCheck");
						System.Windows.Controls.TextBlock textBlock=WpfTree((System.Windows.DependencyObject)labelSettings).OfType<System.Windows.Controls.TextBlock>()
							.FirstOrDefault();
						return "device: "+device+"; settings label '"+Member(labelSettings,"Text")+"' IsBold "+Member(labelSettings,"IsBold")+", drawn weight "
							+(textBlock==null ? "?" : textBlock.FontWeight.ToString())
							+"; check line '"+Member(labelCheck,"Text")+"' colour "+(Member(labelCheck,"ColorText")??Member(labelCheck,"Foreground"));
					});
					checkResult.Note(labels);
					checkResult.Expect(labels.Contains("IsBold True, drawn weight Bold"),"'Settings in effect' is bold (IsBold, and its text drawn Bold)",labels);
					checkResult.Expect(labels.Contains("colour #FFC00000") || labels.Contains("colour #FFFF0000") || labels.Contains("Red"),"The check line is red",labels);
					InspectFrm(ui,checkResult,opened.Hwnd,hasMinMax:null,shot:"E4.png");
					checkResult.ForPerson("the device line, the bold 'Settings in effect' and the wrapped red check line in E4.png");
					checkResult.Expect(ui.Close(opened.Hwnd),"Test capture closes (no test image, no verification record)");
				}
				opened.Posted?.Wait();
				ui.Close(hwndDevice);
			}
			if(hwnd!=IntPtr.Zero) {
				ui.Close(hwnd);
			}
			//Undo: the device E made.
			if(_imagingDeviceNumMade>0) {
				ui.Invoke(() => {
					ImagingDevices.Delete(_imagingDeviceNumMade);
					ImagingDevices.RefreshCache();
				});
				checkResult.Expect(Count("SELECT COUNT(*) FROM imagingdevice WHERE ImagingDeviceNum="+_imagingDeviceNumMade)==0,
					"Undo: the Gatetest device is deleted (by the business layer)");
			}
		}
		#endregion E
	}
}
