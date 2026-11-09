// The in-process check scripts' common part: the program's start as ProgramEntry.Main does it, and the ways a person's clicks reach the
// program's code (a menu item's PerformClick, a toolbar button's handler, a module button's click, a button's PerformClick). Each check's own
// script is in a file of its group (InProcChecks.B.cs and on). Runs only in the program's AppDomain.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using CodeBase;
using OpenPmsDental;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;

namespace WindowChecksTests.InProc {
	public static partial class InProcChecks {
		private static FormOpenDental _formOpenDental;

		///<summary>What ProgramEntry.Main does before the main window exists, on the window's (STA) thread. MouseWatcher (a low-level mouse hook
		///for an auto-hidden taskbar) and the per-process DPI calls are left out: they act on the whole test process.</summary>
		public static void InitializeAsProgramEntry() {
			System.Windows.Media.RenderOptions.ProcessRenderMode=System.Windows.Interop.RenderMode.SoftwareOnly;
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			ODInitialize.Initialize();
			PdfSharpSetup.Init();
			Security.CurComputerName=ODEnvironment.MachineName;
			//As ProgramEntry: the program's own borders off when the flag file is beside the exe (A6).
			if(System.IO.File.Exists(System.IO.Path.Combine(Application.StartupPath,"NoCustomBorders.txt"))) {
				FormODBase.AreBordersMS=true;
				FormFrame.AreBordersMS=true;
			}
		}

		public static Form CreateMainWindow(string[] args,Action<Exception,string> actionUnhandled) {
			_formOpenDental=new FormOpenDental(args);
			ODThread.RegisterForUnhandledExceptions(_formOpenDental,actionUnhandled);
			Application.AddMessageFilter(new ODGlobalUserActiveHandler());
			ODInitialize.FixPackageAssembly("Newtonsoft.Json",ODFileUtils.CombinePaths(AppDomain.CurrentDomain.BaseDirectory,"Newtonsoft.Json.dll"));
			return _formOpenDental;
		}

		///<summary>WPF's unhandled exceptions on the window's thread (the Frm windows run on it).</summary>
		public static void HookDispatcher(Action<Exception> actionUnhandled) {
			System.Windows.Threading.Dispatcher.CurrentDispatcher.UnhandledException+=(sender,e) => {
				actionUnhandled(e.Exception);
				e.Handled=true;
			};
		}

		public static bool IsLoggedOnWithPatient() {
			return Security.CurUser!=null && FormOpenDental.PatNumCur==1;
		}

		///<summary>The check scripts, by check id.</summary>
		public static void Run(string id,Ui ui,CheckResult checkResult) {
			MethodInfo methodInfo=typeof(InProcChecks).GetMethod("Check"+id,BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
			if(methodInfo==null) {
				throw new MissingMethodException("No in-process script for "+id);
			}
			try {
				methodInfo.Invoke(null,new object[] { ui,checkResult });
			}
			catch(TargetInvocationException ex) {
				System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
			}
		}

		#region Reaching the program's code as a click does
		///<summary>The main menu item at the path ("Help", "About"), matched without ampersands and trailing dots.</summary>
		public static ToolStripMenuItem GetMenuItem(params string[] arrayPath) {
			MenuOD menuOD=(MenuOD)Ui.GetField(_formOpenDental,"menuMain");
			ToolStripItemCollection toolStripItemCollection=menuOD.GetMenuItems();
			ToolStripMenuItem toolStripMenuItem=null;
			foreach(string text in arrayPath) {
				toolStripMenuItem=toolStripItemCollection.OfType<ToolStripMenuItem>().FirstOrDefault(x => Clean(x.Text)==Clean(text));
				if(toolStripMenuItem==null) {
					throw new Exception("No menu item '"+text+"' in "+string.Join(" > ",arrayPath)+" (there: "
						+string.Join(", ",toolStripItemCollection.OfType<ToolStripMenuItem>().Select(x => Clean(x.Text)))+")");
				}
				toolStripItemCollection=toolStripMenuItem.DropDownItems;
			}
			return toolStripMenuItem;
		}

		private static string Clean(string text) {
			return (text??"").Replace("&","").Trim().TrimEnd('.').Trim();
		}

		///<summary>Clicks the menu item at the path (its PerformClick raises the Click handler the program attached) and returns the window it
		///opens.</summary>
		public static Ui.Opened OpenMenu(Ui ui,params string[] arrayPath) {
			string path=string.Join(" > ",arrayPath);
			return ui.Open("Menu "+path,() => {
				ToolStripMenuItem toolStripMenuItem=GetMenuItem(arrayPath);
				if(!toolStripMenuItem.Available || !toolStripMenuItem.Enabled) {
					throw new Exception("The menu item "+path+" is not available");
				}
				toolStripMenuItem.PerformClick();
			});
		}

		///<summary>The main toolbar's button with the tag (the predefined ones: Patient, Commlog, Email, Letter, Form, Tasklist, Label, Popups).</summary>
		public static Ui.Opened OpenToolbar(Ui ui,string tag) {
			return ui.Open("Main toolbar "+tag,() => {
				ToolBarOD toolBarOD=(ToolBarOD)Ui.GetField(_formOpenDental,"ToolBarMain");
				ODToolBarButton odToolBarButton=toolBarOD.Buttons.Cast<ODToolBarButton>().FirstOrDefault(x => x.Tag is string && (string)x.Tag==tag);
				if(odToolBarButton==null) {
					throw new Exception("No main toolbar button "+tag);
				}
				Ui.Call(_formOpenDental,"toolBarMain_ButtonClick",toolBarOD,new ODToolBarButtonClickEventArgs(odToolBarButton));
			});
		}

		///<summary>Clicks the module bar's button of the module, as its OnMouseUp does (the previous selection kept for a refused click). Posted: leaving a
		///module can ask a question (the Chart's "Create Planned Appointment ...?"), which is read into the current check's notes and answered No.</summary>
		public static void GoToModule(Ui ui,EnumModuleType enumModuleType) {
			List<IntPtr> listBefore=ui.GetWindows();
			Ui.Posted posted=ui.Post("Module button "+enumModuleType,() => {
				ModuleBar moduleBar=(ModuleBar)Ui.GetField(_formOpenDental,"moduleBar");
				List<ModuleBarButton> listButtons=(List<ModuleBarButton>)Ui.GetField(moduleBar,"_listButtons");
				int index=listButtons.FindIndex(x => x.ModuleType==enumModuleType);
				Ui.SetField(moduleBar,"_selectedIndexPrevious",Ui.GetField(moduleBar,"_selectedIndex"));
				Ui.SetField(moduleBar,"_selectedIndex",index);
				moduleBar.Invalidate();
				Ui.Call(moduleBar,"OnButtonClicked",listButtons[index],false);
			});
			System.Diagnostics.Stopwatch stopwatch=System.Diagnostics.Stopwatch.StartNew();
			while(!posted.ManualResetEventDone.WaitOne(200) && stopwatch.Elapsed.TotalSeconds<90) {
				foreach(IntPtr hwnd in ui.GetWindows().Where(x => !listBefore.Contains(x) && Native.IsDialogClass(x))) {
					ui.CheckResultCur?.Note("Going to "+enumModuleType+" asked: "+ui.DescribeWindow(hwnd)+"; answered No");
					if(!Native.ClickDialogButton(hwnd,"No")) {
						Native.CancelDialog(hwnd);
					}
					ui.WaitGone(hwnd);
				}
			}
			if(!posted.ManualResetEventDone.WaitOne(0)) {
				throw new TimeoutException("The module button "+enumModuleType+" did not finish within 90 s; windows: "
					+string.Join("; ",ui.GetWindows().Select(x => ui.DescribeWindow(x))));
			}
			Thread.Sleep(500);
		}

		public static EnumModuleType GetModule(Ui ui) {
			return ui.Invoke(() => ((ModuleBar)Ui.GetField(_formOpenDental,"moduleBar")).SelectedModule);
		}

		///<summary>Presses a button of a window by its PerformClick: the control named, or the first button whose text matches.</summary>
		public static void ClickButton(Ui ui,IntPtr hwnd,string nameOrText) {
			ui.Post("Button "+nameOrText,() => {
				Control control=FindControl(ui,hwnd,nameOrText,isOnThread:true);
				if(control==null) {
					throw new Exception("No button "+nameOrText);
				}
				if(control is IButtonControl buttonControl) {
					buttonControl.PerformClick();
				}
				else {
					Ui.Call(control,"OnClick",EventArgs.Empty);
				}
			});
		}

		///<summary>The control of the window with this Name, or else the first visible one whose Text (no ampersands) is this text.</summary>
		public static Control FindControl(Ui ui,IntPtr hwnd,string nameOrText,bool isOnThread=false) {
			Func<Control> func=() => {
				Control controlRoot=Control.FromHandle(hwnd);
				if(controlRoot==null) {
					return null;
				}
				List<Control> listControls=Ui.AllControls(controlRoot).ToList();
				return listControls.FirstOrDefault(x => x.Name==nameOrText)
					??listControls.FirstOrDefault(x => x.Visible && Clean(x.Text)==Clean(nameOrText));
			};
			return isOnThread ? func() : ui.Invoke(func);
		}

		///<summary>The main window's control in the field (a module, the toolbar).</summary>
		public static T MainField<T>(string name) where T:class {
			return Ui.GetField(_formOpenDental,name) as T;
		}

		public static FormOpenDental FormMain {
			get {
				return _formOpenDental;
			}
		}
		#endregion Reaching the program's code as a click does
	}
}
