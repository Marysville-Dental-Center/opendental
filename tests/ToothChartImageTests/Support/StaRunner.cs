// Runs test code on its own STA thread, as WinForms charts and WPF need, or on its own MTA thread. The test host's own thread is not to be relied
// on: under vstest the MSTest v1 adapter ran the TC-1 tests on an STA thread.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace ToothChartImageTests {
	///<summary>Runs an action on a new STA thread and waits for it, the pattern of tests/BPRINTTests/PrintOptionsTests.cs (RunSta). What the
	///action throws is thrown again on the test thread with its own type and stack trace, so Assert.Inconclusive and Assert.Fail inside the action
	///keep their meaning. After S4-1 (MSTest 3.6 or later) the tests may use [STATestMethod] instead.</summary>
	public static class StaRunner {
		public static void Run(Action action) {
			Run<object>(() => {
				action();
				return null;
			});
		}

		public static T Run<T>(Func<T> func) {
			return Run(func,ApartmentState.STA);
		}

		///<summary>Runs the action on a new MTA thread, as the Patient Dashboard's ODThread worker makes its chart image (TC-1's WPF chart tests). The
		///test host's own thread may be STA.</summary>
		public static void RunMta(Action action) {
			Run<object>(() => {
				action();
				return null;
			},ApartmentState.MTA);
		}

		public static T RunMta<T>(Func<T> func) {
			return Run(func,ApartmentState.MTA);
		}

		private static T Run<T>(Func<T> func,ApartmentState apartmentState) {
			T result=default(T);
			ExceptionDispatchInfo exceptionDispatchInfo=null;
			Thread thread=new Thread(() => {
				try {
					result=func();
				}
				catch(Exception ex) {
					exceptionDispatchInfo=ExceptionDispatchInfo.Capture(ex);
				}
				finally {
					ShutDownDispatcherOfThread();
				}
			});
			thread.SetApartmentState(apartmentState);
			thread.IsBackground=true;
			thread.Start();
			thread.Join();
			exceptionDispatchInfo?.Throw();
			return result;
		}

		///<summary>Shuts down this thread's WPF Dispatcher, when the action made one (a live view's ElementHost), on the thread itself before it ends, so
		///WPF releases its per-thread COM objects (the text services of a WPF window that had the keyboard focus) in their own apartment. Left to the
		///end of the process, after the STA thread is gone, TextServicesContext's shutdown can throw InvalidComObjectException and end the test host.</summary>
		private static void ShutDownDispatcherOfThread() {
			System.Windows.Threading.Dispatcher dispatcher=System.Windows.Threading.Dispatcher.FromThread(Thread.CurrentThread);
			if(dispatcher==null || dispatcher.HasShutdownStarted) {
				return;
			}
			try {
				dispatcher.InvokeShutdown();
			}
			catch(Exception ex) {
				ex.ToString();//the thread ends anyway
			}
		}
	}
}
