// What a check script uses to drive the program's windows from its own thread: post work to the window's thread (as a click would run it),
// wait for the windows that work opens, describe and print them, find their controls, and close them. Waiting is by polling the window
// thread's top-level windows (EnumThreadWindows), which works the same for WinForms forms, WPF windows and the standard message box, and
// while the screen is locked.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace WindowChecksTests.InProc {
	public class Ui {
		private readonly ProgramDriver _programDriver;
		public readonly Form FormMain;
		public readonly int ThreadIdUi;
		public CheckResult CheckResultCur;
		public const int TimeoutWindowMs=45000;

		public Ui(ProgramDriver programDriver,Form formMain,int threadIdUi) {
			_programDriver=programDriver;
			FormMain=formMain;
			ThreadIdUi=threadIdUi;
		}

		#region Running on the window's thread
		///<summary>Runs the function on the window's thread and returns its result. Works while a modal window is open (its loop runs the call).
		///Throws TimeoutException when the thread does not answer.</summary>
		public T Invoke<T>(Func<T> func,int timeoutMs=60000) {
			T result=default(T);
			Exception exception=null;
			ManualResetEvent manualResetEvent=new ManualResetEvent(false);
			FormMain.BeginInvoke((Action)(() => {
				try {
					result=func();
				}
				catch(Exception ex) {
					exception=ex;
				}
				finally {
					manualResetEvent.Set();
				}
			}));
			if(!manualResetEvent.WaitOne(timeoutMs)) {
				throw new TimeoutException("The window's thread did not answer within "+timeoutMs/1000+" s.");
			}
			if(exception!=null) {
				throw new Exception("On the window's thread: "+exception.GetType().Name+": "+exception.Message,exception);
			}
			return result;
		}

		public void Invoke(Action action,int timeoutMs=60000) {
			Invoke(() => {
				action();
				return true;
			},timeoutMs);
		}

		///<summary>Posts the action to the window's thread and returns at once; the action may open a modal window (its ShowDialog returns when
		///the check closes that window). Done is set when the action returns; an exception it throws is a failure of the check.</summary>
		public Posted Post(string description,Action action) {
			Posted posted=new Posted { Description=description };
			FormMain.BeginInvoke((Action)(() => {
				try {
					action();
				}
				catch(Exception ex) {
					posted.Exception=ex;
					CheckResultCur?.Fail(description+" threw "+ex.GetType().Name+": "+ex.Message+" "+Frames(ex));
				}
				finally {
					posted.ManualResetEventDone.Set();
				}
			}));
			return posted;
		}

		///<summary>The first frames of the exception's stack, for a failure's evidence.</summary>
		public static string Frames(Exception ex) {
			string stack=(ex.GetBaseException().StackTrace??"").Replace("\r","");
			return "["+string.Join(" / ",stack.Split('\n').Select(x => x.Trim()).Where(x => x!="").Take(5))+"]";
		}

		public class Posted {
			public string Description;
			public Exception Exception;
			public ManualResetEvent ManualResetEventDone=new ManualResetEvent(false);

			public bool Wait(int timeoutMs=TimeoutWindowMs) {
				return ManualResetEventDone.WaitOne(timeoutMs);
			}
		}
		#endregion Running on the window's thread

		#region Windows
		///<summary>The visible top-level windows of the window's thread, the main window left out.</summary>
		public List<IntPtr> GetWindows() {
			IntPtr hwndMain=Invoke(() => FormMain.Handle);
			return Native.GetThreadWindows(ThreadIdUi).Where(x => x!=hwndMain && IsRealWindow(x)).ToList();
		}

		///<summary>A window a person would see as a window: a form, a WPF window or a standard dialog; not a tooltip, drop-down or menu.</summary>
		private bool IsRealWindow(IntPtr hwnd) {
			string className=Native.GetClass(hwnd);
			if(className=="#32770") {
				return true;
			}
			if(className.StartsWith("tooltips_class") || className.StartsWith("WindowsForms10.tooltips") || className=="#32768") {
				return false;
			}
			return Invoke(() => {
				Control control=Control.FromHandle(hwnd);
				if(control is Form) {
					return true;//the program's forms draw their own borders (FormBorderStyle None), so every form counts
				}
				if(control!=null) {
					return false;
				}
				System.Windows.Interop.HwndSource hwndSource=System.Windows.Interop.HwndSource.FromHwnd(hwnd);
				return hwndSource?.RootVisual is System.Windows.Window;
			});
		}

		///<summary>Waits for a window that was not open before (not in listBefore). Message boxes count too: the caller decides.</summary>
		public IntPtr WaitForNewWindow(List<IntPtr> listBefore,int timeoutMs=TimeoutWindowMs) {
			Stopwatch stopwatch=Stopwatch.StartNew();
			while(stopwatch.ElapsedMilliseconds<timeoutMs) {
				IntPtr hwnd=GetWindows().FirstOrDefault(x => !listBefore.Contains(x));
				if(hwnd!=IntPtr.Zero) {
					Thread.Sleep(400);//let it finish its Load and first paint
					return hwnd;
				}
				Thread.Sleep(150);
			}
			return IntPtr.Zero;
		}

		///<summary>Opens a window by posting the action, and returns it; a message box instead of a window is read and recorded as a failure (and
		///closed), unless allowMessage. Returns zero when nothing opened.</summary>
		public Opened Open(string description,Action action,bool allowMessage=false,int timeoutMs=TimeoutWindowMs) {
			List<IntPtr> listBefore=GetWindows();
			Opened opened=new Opened { Posted=Post(description,action) };
			Stopwatch stopwatch=Stopwatch.StartNew();
			while(stopwatch.ElapsedMilliseconds<timeoutMs) {
				opened.Hwnd=WaitForNewWindow(listBefore,Math.Max(100,timeoutMs-(int)stopwatch.ElapsedMilliseconds));
				if(opened.Hwnd==IntPtr.Zero) {
					break;
				}
				if(Native.IsDialogClass(opened.Hwnd) && !allowMessage) {
					string text=DescribeWindow(opened.Hwnd);
					CheckResultCur?.Fail(description+": unexpected message box: "+text);
					Native.CancelDialog(opened.Hwnd);
					WaitGone(opened.Hwnd);
					listBefore=GetWindows();
					continue;
				}
				return opened;
			}
			if(opened.Posted.ManualResetEventDone.WaitOne(0)) {
				CheckResultCur?.Fail(description+": no window opened (the handler returned without one)");
			}
			else {
				CheckResultCur?.Fail(description+": no window opened within "+timeoutMs/1000+" s");
			}
			return opened;
		}

		public class Opened {
			public Posted Posted;
			public IntPtr Hwnd;

			public bool IsOpen {
				get {
					return Hwnd!=IntPtr.Zero;
				}
			}
		}

		///<summary>"Title (class): message" for a standard dialog, "Title (FormType)" for a form, "Title (WPF FrmType)" for a WPF window.</summary>
		public string DescribeWindow(IntPtr hwnd) {
			string title=Native.GetText(hwnd);
			if(Native.IsDialogClass(hwnd)) {
				return "\""+title+"\": "+Native.GetDialogMessage(hwnd)+" ["+string.Join("/",Native.GetDialogButtons(hwnd))+"]";
			}
			string typeName=Invoke(() => {
				Control control=Control.FromHandle(hwnd);
				if(control!=null) {
					return control.GetType().Name;
				}
				System.Windows.Interop.HwndSource hwndSource=System.Windows.Interop.HwndSource.FromHwnd(hwnd);
				if(hwndSource?.RootVisual is System.Windows.Window window) {
					return "WPF "+window.GetType().Name+(window.Content==null ? "" : " / "+window.Content.GetType().Name);
				}
				return Native.GetClass(hwnd);
			});
			return "\""+title+"\" ("+typeName+")";
		}

		///<summary>The WPF Frm (FrmODBase) a FormFrame hosts in its ElementHost, or null. On the window's thread.</summary>
		public static System.Windows.FrameworkElement GetFrmOnThread(Control control) {
			if(control==null || control.GetType().Name!="FormFrame") {
				return null;
			}
			System.Windows.Forms.Integration.ElementHost elementHost=AllControls(control).OfType<System.Windows.Forms.Integration.ElementHost>().FirstOrDefault();
			return elementHost?.Child as System.Windows.FrameworkElement;
		}

		///<summary>The WPF Frm of the window (a FormFrame), or null.</summary>
		public System.Windows.FrameworkElement GetFrm(IntPtr hwnd) {
			return Invoke(() => GetFrmOnThread(Control.FromHandle(hwnd)));
		}

		///<summary>Renders a WPF element (a Frm) with RenderTargetBitmap.</summary>
		public ShotInfo ShotElement(System.Windows.FrameworkElement frameworkElement,string fileName) {
			return Invoke(() => {
				int width=(int)Math.Ceiling(frameworkElement.ActualWidth);
				int height=(int)Math.Ceiling(frameworkElement.ActualHeight);
				if(width<=0 || height<=0) {
					return new ShotInfo { FileName=fileName,Error="the WPF element has no size" };
				}
				System.Windows.Media.Imaging.RenderTargetBitmap renderTargetBitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(width,height,96,96,
					System.Windows.Media.PixelFormats.Pbgra32);
				System.Windows.Media.DrawingVisual drawingVisual=new System.Windows.Media.DrawingVisual();
				using(System.Windows.Media.DrawingContext drawingContext=drawingVisual.RenderOpen()) {
					drawingContext.DrawRectangle(System.Windows.Media.Brushes.White,null,new System.Windows.Rect(0,0,width,height));
					drawingContext.DrawRectangle(new System.Windows.Media.VisualBrush(frameworkElement),null,new System.Windows.Rect(0,0,width,height));
				}
				renderTargetBitmap.Render(drawingVisual);
				System.Windows.Media.Imaging.PngBitmapEncoder pngBitmapEncoder=new System.Windows.Media.Imaging.PngBitmapEncoder();
				pngBitmapEncoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(renderTargetBitmap));
				using System.IO.MemoryStream memoryStream=new System.IO.MemoryStream();
				pngBitmapEncoder.Save(memoryStream);
				memoryStream.Position=0;
				using Bitmap bitmap=new Bitmap(memoryStream);
				return WindowChecksTests.Shot.Bitmap(bitmap,fileName);
			});
		}

		public Form GetForm(IntPtr hwnd) {
			return Invoke(() => Control.FromHandle(hwnd) as Form);
		}

		///<summary>The WPF window of the handle, or null.</summary>
		public System.Windows.Window GetWpfWindow(IntPtr hwnd) {
			return Invoke(() => System.Windows.Interop.HwndSource.FromHwnd(hwnd)?.RootVisual as System.Windows.Window);
		}

		///<summary>The type name of the window: a Form's class, or a WPF window's class (and its content's, for the Frm windows hosted in one).</summary>
		public string GetWindowTypeName(IntPtr hwnd) {
			return Invoke(() => {
				Control control=Control.FromHandle(hwnd);
				if(control!=null) {
					//A WPF Frm runs in a FormFrame (a WinForms form with an ElementHost): name both.
					System.Windows.FrameworkElement frm=GetFrmOnThread(control);
					return control.GetType().Name+(frm==null ? "" : "/"+frm.GetType().Name);
				}
				System.Windows.Window window=System.Windows.Interop.HwndSource.FromHwnd(hwnd)?.RootVisual as System.Windows.Window;
				if(window==null) {
					return Native.GetClass(hwnd);
				}
				return window.GetType().Name+(window.Content is System.Windows.FrameworkElement frameworkElement ? "/"+frameworkElement.GetType().Name : "");
			});
		}

		public bool WaitGone(IntPtr hwnd,int timeoutMs=15000) {
			Stopwatch stopwatch=Stopwatch.StartNew();
			while(stopwatch.ElapsedMilliseconds<timeoutMs) {
				if(!Native.IsWindow(hwnd) || !Native.IsWindowVisible(hwnd)) {
					return true;
				}
				Thread.Sleep(100);
			}
			return false;
		}

		///<summary>Closes the window as its Cancel or X would: a form gets Close (a modal one ends its ShowDialog with Cancel), a WPF window Close, a
		///standard dialog its Cancel button. Waits for it to go.</summary>
		public bool Close(IntPtr hwnd) {
			if(!Native.IsWindow(hwnd)) {
				return true;
			}
			if(Native.IsDialogClass(hwnd)) {
				Native.CancelDialog(hwnd);
				return WaitGone(hwnd);
			}
			//Posted, because closing may itself open a message ("Save changes?"), which must not block this thread.
			FormMain.BeginInvoke((Action)(() => {
				try {
					Control control=Control.FromHandle(hwnd);
					if(control is Form form) {
						form.Close();
						return;
					}
					System.Windows.Window window=System.Windows.Interop.HwndSource.FromHwnd(hwnd)?.RootVisual as System.Windows.Window;
					window?.Close();
				}
				catch(Exception ex) {
					CheckResultCur?.Fail("Closing "+Native.GetText(hwnd)+" threw "+ex.GetType().Name+": "+ex.Message);
				}
			}));
			if(WaitGone(hwnd)) {
				return true;
			}
			//A question on closing: answer No (nothing saved).
			foreach(IntPtr hwndDialog in GetWindows().Where(x => Native.IsDialogClass(x))) {
				CheckResultCur?.Note("Closing asked: "+DescribeWindow(hwndDialog)+"; answered No/Cancel");
				if(!Native.ClickDialogButton(hwndDialog,"No")) {
					Native.CancelDialog(hwndDialog);
				}
			}
			return WaitGone(hwnd);
		}

		///<summary>Closes every window of the thread but the main window, last opened first; each one left open is noted.</summary>
		public void CloseStrayWindows(CheckResult checkResult) {
			for(int i=0;i<6;i++) {
				List<IntPtr> listWindows;
				try {
					listWindows=GetWindows();
				}
				catch(Exception) {
					return;
				}
				if(listWindows.Count==0) {
					return;
				}
				foreach(IntPtr hwnd in listWindows) {
					checkResult?.Note("Closed a window left open: "+DescribeWindow(hwnd));
					Close(hwnd);
				}
			}
		}
		#endregion Windows

		#region Pictures
		///<summary>Prints the window (PrintWindow, PW_RENDERFULLCONTENT) into the results folder.</summary>
		public ShotInfo Shot(IntPtr hwnd,string fileName) {
			return WindowChecksTests.Shot.Window(hwnd,fileName);
		}

		///<summary>Draws a WinForms control with DrawToBitmap (the control's own painting, children included).</summary>
		public ShotInfo ShotDraw(Control control,string fileName) {
			return Invoke(() => {
				using Bitmap bitmap=new Bitmap(Math.Max(1,control.Width),Math.Max(1,control.Height));
				control.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));
				return WindowChecksTests.Shot.Bitmap(bitmap,fileName);
			});
		}

		///<summary>Renders a WPF window's content with RenderTargetBitmap (WPF's own rendering, independent of the screen).</summary>
		public ShotInfo ShotWpf(System.Windows.Window window,string fileName) {
			return Invoke(() => {
				System.Windows.FrameworkElement frameworkElement=(window.Content as System.Windows.FrameworkElement)??window;
				int width=(int)Math.Ceiling(frameworkElement.ActualWidth);
				int height=(int)Math.Ceiling(frameworkElement.ActualHeight);
				if(width<=0 || height<=0) {
					return new ShotInfo { FileName=fileName,Error="the WPF content has no size" };
				}
				System.Windows.Media.Imaging.RenderTargetBitmap renderTargetBitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(width,height,96,96,
					System.Windows.Media.PixelFormats.Pbgra32);
				System.Windows.Media.DrawingVisual drawingVisual=new System.Windows.Media.DrawingVisual();
				using(System.Windows.Media.DrawingContext drawingContext=drawingVisual.RenderOpen()) {
					drawingContext.DrawRectangle(System.Windows.Media.Brushes.White,null,new System.Windows.Rect(0,0,width,height));
					drawingContext.DrawRectangle(new System.Windows.Media.VisualBrush(frameworkElement),null,new System.Windows.Rect(0,0,width,height));
				}
				renderTargetBitmap.Render(drawingVisual);
				System.Windows.Media.Imaging.PngBitmapEncoder pngBitmapEncoder=new System.Windows.Media.Imaging.PngBitmapEncoder();
				pngBitmapEncoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(renderTargetBitmap));
				using System.IO.MemoryStream memoryStream=new System.IO.MemoryStream();
				pngBitmapEncoder.Save(memoryStream);
				memoryStream.Position=0;
				using Bitmap bitmap=new Bitmap(memoryStream);
				return WindowChecksTests.Shot.Bitmap(bitmap,fileName);
			});
		}
		#endregion Pictures

		#region Controls
		///<summary>Every control under the root, depth first.</summary>
		public static IEnumerable<Control> AllControls(Control root) {
			foreach(Control control in root.Controls) {
				yield return control;
				foreach(Control controlChild in AllControls(control)) {
					yield return controlChild;
				}
			}
		}

		///<summary>The value of an instance field (any visibility, up the class chain), or null.</summary>
		public static object GetField(object obj,string name) {
			for(Type type=obj.GetType();type!=null;type=type.BaseType) {
				FieldInfo fieldInfo=type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.DeclaredOnly);
				if(fieldInfo!=null) {
					return fieldInfo.GetValue(obj);
				}
			}
			return null;
		}

		public static void SetField(object obj,string name,object value) {
			for(Type type=obj.GetType();type!=null;type=type.BaseType) {
				FieldInfo fieldInfo=type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.DeclaredOnly);
				if(fieldInfo!=null) {
					fieldInfo.SetValue(obj,value);
					return;
				}
			}
			throw new MissingFieldException(obj.GetType().Name,name);
		}

		///<summary>Calls an instance method (any visibility, up the class chain) by name and argument count.</summary>
		public static object Call(object obj,string name,params object[] args) {
			for(Type type=obj.GetType();type!=null;type=type.BaseType) {
				MethodInfo methodInfo=type.GetMethods(BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.DeclaredOnly)
					.FirstOrDefault(x => x.Name==name && x.GetParameters().Length==args.Length);
				if(methodInfo!=null) {
					try {
						return methodInfo.Invoke(obj,args);
					}
					catch(TargetInvocationException ex) {
						System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
						throw;
					}
				}
			}
			throw new MissingMethodException(obj.GetType().Name,name);
		}

		public static object CallStatic(Type type,string name,params object[] args) {
			MethodInfo methodInfo=type.GetMethods(BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public)
				.FirstOrDefault(x => x.Name==name && x.GetParameters().Length==args.Length);
			if(methodInfo==null) {
				throw new MissingMethodException(type.Name,name);
			}
			try {
				return methodInfo.Invoke(null,args);
			}
			catch(TargetInvocationException ex) {
				System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
				throw;
			}
		}
		#endregion Controls
	}
}
