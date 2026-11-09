// Mouse input for the live view's tests (package TC-3, fix round 3): WPF's own input path, from a raw report through the MouseDevice to the content's
// MouseMove, MouseDown and MouseUp, with GetPosition, hit testing and capture as for a real mouse.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace ToothChartImageTests {
	///<summary>Raw mouse reports for one PresentationSource (a live view's HwndSource), as WPF's own mouse provider (HwndMouseInputProvider) makes them
	///from window messages, handed to InputManager.ProcessInput. The MouseDevice then moves to the point, hit-tests, raises PreviewMouseMove and
	///MouseMove (only when the point changed, as for a real mouse), MouseDown and MouseUp on the element under the point or the one that has the
	///capture, and GetPosition gives the reported point in any element's units (through the content's 1/DPI layout transform). The points are in the
	///source's client pixels: for a live view, the wrapper's pixels. The first report activates the mouse on the source, as the provider does.
	///Why reports and not window messages: WPF's provider drops a message for a window the cursor is not over (the tests' windows are off every
	///screen), and no test moves the real cursor. The report types are WPF's internals (RawMouseInputReport, RawMouseActions, InputReportEventArgs,
	///InputManager.PreviewInputReportEvent), reached by reflection; they are WPF's internals, so S3b-5 checks them again when the program moves to
	///.NET 10 (stage 3b). WPF still moves its mouse when the capture changes (the window's mouse provider reports the real cursor's position when it
	///takes the capture; MouseDevice.Synchronize runs on each capture change): the view must not hand that move on (ToothChartWpfView). It also
	///re-synchronises after a layout pass, at the position it already has, and raises MouseMove then only when the content moved under the mouse.</summary>
	public sealed class WpfMouseInput {
		private static readonly Assembly _assemblyCore=typeof(InputManager).Assembly;
		private static readonly Type _typeReport=_assemblyCore.GetType("System.Windows.Input.RawMouseInputReport",true);
		private static readonly Type _typeActions=_assemblyCore.GetType("System.Windows.Input.RawMouseActions",true);
		private static readonly Type _typeReportArgs=_assemblyCore.GetType("System.Windows.Input.InputReportEventArgs",true);
		private readonly PresentationSource _presentationSource;
		private bool _isActive;

		public WpfMouseInput(PresentationSource presentationSource) {
			_presentationSource=presentationSource??throw new ArgumentNullException(nameof(presentationSource));
		}

		///<summary>The input of a visual's window: its PresentationSource, which a live view's content has once its host has a handle.</summary>
		public static WpfMouseInput ForVisual(System.Windows.Media.Visual visual) {
			PresentationSource presentationSource=PresentationSource.FromVisual(visual);
			if(presentationSource==null) {
				throw new InvalidOperationException("The visual is in no window.");
			}
			return new WpfMouseInput(presentationSource);
		}

		///<summary>The mouse moves to the point (WPF raises MouseMove only when its mouse was elsewhere).</summary>
		public void Move(System.Drawing.Point point) {
			Report(_isActive?"AbsoluteMove":"Activate, AbsoluteMove",point);
			_isActive=true;
		}

		///<summary>WPF moving its own mouse to the point, as it does when the capture changes (to wherever the real cursor is): for a
		///test, from a GotMouseCapture or LostMouseCapture handler. The same report as Move; no window message makes it.</summary>
		public void SynchroniseTo(System.Drawing.Point point) {
			Move(point);
		}

		///<summary>The left button goes down where WPF's mouse is (a press report carries no move of its own: move to the point first).</summary>
		public void Press(System.Drawing.Point point) {
			Report("Button1Press",point);
		}

		///<summary>The left button goes up where WPF's mouse is.</summary>
		public void Release(System.Drawing.Point point) {
			Report("Button1Release",point);
		}

		///<summary>The mouse leaves the source, as when the window goes.</summary>
		public void Deactivate() {
			if(!_isActive) {
				return;
			}
			Report("Deactivate",new System.Drawing.Point(0,0));
			_isActive=false;
		}

		private void Report(string actions,System.Drawing.Point point) {
			object rawMouseActions=Enum.Parse(_typeActions,actions);
			ConstructorInfo constructorInfoReport=_typeReport.GetConstructor(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,
				new[] { typeof(InputMode),typeof(int),typeof(PresentationSource),_typeActions,typeof(int),typeof(int),typeof(int),typeof(IntPtr) },null);
			object report=constructorInfoReport.Invoke(new object[] { InputMode.Foreground,Environment.TickCount,_presentationSource,rawMouseActions,point.X,point.Y,0,
				IntPtr.Zero });
			ConstructorInfo constructorInfoArgs=_typeReportArgs.GetConstructor(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,
				new[] { typeof(InputDevice),_assemblyCore.GetType("System.Windows.Input.InputReport",true) },null);
			InputEventArgs inputEventArgs=(InputEventArgs)constructorInfoArgs.Invoke(new object[] { Mouse.PrimaryDevice,report });
			FieldInfo fieldInfoEvent=typeof(InputManager).GetField("PreviewInputReportEvent",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
			inputEventArgs.RoutedEvent=(RoutedEvent)fieldInfoEvent.GetValue(null);
			InputManager.Current.ProcessInput(inputEventArgs);
		}
	}
}
