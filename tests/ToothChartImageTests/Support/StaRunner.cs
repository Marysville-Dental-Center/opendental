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
			});
			thread.SetApartmentState(apartmentState);
			thread.IsBackground=true;
			thread.Start();
			thread.Join();
			exceptionDispatchInfo?.Throw();
			return result;
		}
	}
}
