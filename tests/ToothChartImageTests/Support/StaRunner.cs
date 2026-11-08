// Runs test code on its own STA thread, as WinForms charts and WPF need. MSTest v1 runs tests on an MTA thread.
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
			thread.SetApartmentState(ApartmentState.STA);
			thread.IsBackground=true;
			thread.Start();
			thread.Join();
			exceptionDispatchInfo?.Throw();
			return result;
		}
	}
}
