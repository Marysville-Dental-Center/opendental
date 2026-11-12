// Runs test code on its own STA thread, as WPF needs. The test host's own thread is not to be relied on.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace EFormPrintTests {
	///<summary>Runs an action on a new STA thread and waits for it, the pattern of tests/ToothChartImageTests/Support/StaRunner.cs. What the action
	///throws is thrown again on the test thread with its own type and stack trace, so Assert.Inconclusive and Assert.Fail inside the action keep their
	///meaning.</summary>
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
				finally {
					ShutDownDispatcherOfThread();
				}
			});
			thread.SetApartmentState(ApartmentState.STA);
			thread.IsBackground=true;
			thread.Start();
			thread.Join();
			exceptionDispatchInfo?.Throw();
			return result;
		}

		///<summary>Shuts down this thread's WPF Dispatcher on the thread itself before it ends, so WPF releases its per-thread objects in their own
		///apartment.</summary>
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
