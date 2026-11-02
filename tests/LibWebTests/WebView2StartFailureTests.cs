// A WebView2 Runtime that is installed but will not start (damaged, blocked by antivirus or AppLocker, a profile folder that cannot be used) is
// reported: ODWebView2.Init logs it, tells the user once per program session what an administrator can do, and throws ODException, so the windows
// that only call NavigateToHtml (wiki, email, CCD) never stay blank without a word. The environment is made to fail through ODWebView2's test
// seam (FuncCreateEnvironment); the message is recorded instead of shown (ActionShowMessage). No browser starts and no window is shown.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CodeBase;
using CodeBase.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Web.WebView2.Core;

namespace LibWebTests {
	[TestClass]
	public class WebView2StartFailureTests {
		///<summary>The HRESULT the failing environment gives (ERROR_ACCESS_DENIED), as a blocked msedgewebview2.exe or a read-only profile folder might.</summary>
		private const int HRESULT_ACCESS_DENIED=unchecked((int)0x80070005);

		///<summary>Runs action on an STA thread, as a window's thread is, with ODWebView2's seams set: the runtime counts as installed, every
		///environment fails with exceptionEnvironment, and messages go to listMessages. Restores the seams and the once-per-session flag afterwards.</summary>
		private static void RunWithFailingEnvironment(Exception exceptionEnvironment,List<string> listMessages,Action action) {
			Exception exceptionThread=null;
			Thread thread=new Thread(() => {
				Func<bool> funcIsRuntimeAvailableOld=ODWebView2.FuncIsRuntimeAvailable;
				Func<Task<CoreWebView2Environment>> funcCreateEnvironmentOld=ODWebView2.FuncCreateEnvironment;
				Action<string> actionShowMessageOld=ODWebView2.ActionShowMessage;
				bool isUnitTestOld=ODBuild.IsUnitTest;
				try {
					ODBuild.IsUnitTest=true;//The logger writes nothing.
					ODWebView2.ResetStartFailureShown();
					ODWebView2.FuncIsRuntimeAvailable=() => true;
					ODWebView2.FuncCreateEnvironment=() => {
						TaskCompletionSource<CoreWebView2Environment> taskCompletionSource=new TaskCompletionSource<CoreWebView2Environment>();
						taskCompletionSource.SetException(exceptionEnvironment);
						return taskCompletionSource.Task;
					};
					ODWebView2.ActionShowMessage=x => listMessages.Add(x);
					action();
				}
				catch(Exception ex) {
					exceptionThread=ex;
				}
				finally {
					ODWebView2.FuncIsRuntimeAvailable=funcIsRuntimeAvailableOld;
					ODWebView2.FuncCreateEnvironment=funcCreateEnvironmentOld;
					ODWebView2.ActionShowMessage=actionShowMessageOld;
					ODWebView2.ResetStartFailureShown();
					ODBuild.IsUnitTest=isUnitTestOld;
				}
			});
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
			Assert.IsTrue(thread.Join(TimeSpan.FromMinutes(1)),"The test thread finished.");
			if(exceptionThread!=null) {
				Assert.Fail(exceptionThread.ToString());
			}
		}

		[TestMethod]
		public void StartFailure_NavigateToHtmlDoesNotThrow_AndTheUserIsToldOncePerSession() {
			List<string> listMessages=new List<string>();
			RunWithFailingEnvironment(new COMException("Access is denied.",HRESULT_ACCESS_DENIED),listMessages,() => {
				//The inbox's two previews: the first tells the user, the second does not tell again.
				using ODWebView2 odWebView2First=new ODWebView2();
				odWebView2First.NavigateToHtml(HtmlPage.BuildText("first"));//An environment that already failed: Init ran to its end here.
				Assert.AreEqual(1,listMessages.Count,"The first control reported the failure.");
				using ODWebView2 odWebView2Second=new ODWebView2();
				odWebView2Second.NavigateToHtml(HtmlPage.BuildText("second"));
				odWebView2First.NavigateToHtml(HtmlPage.BuildText("first again"));
				Assert.IsNull(odWebView2First.CoreWebView2);
				//A window that calls Init itself gets ODException, so its "Init told the user" catch is true.
				using ODWebView2 odWebView2Third=new ODWebView2();
				Exception exceptionInit=null;
				try {
					odWebView2Third.Init().GetAwaiter().GetResult();
				}
				catch(Exception ex) {
					exceptionInit=ex;
				}
				Assert.IsInstanceOfType(exceptionInit,typeof(ODException));
				Assert.IsInstanceOfType(exceptionInit.InnerException,typeof(COMException));
			});
			Assert.AreEqual(1,listMessages.Count,"Once per program session: "+string.Join(" | ",listMessages));
			string message=listMessages[0];
			StringAssert.Contains(message,"The Microsoft Edge WebView2 Runtime could not start");
			StringAssert.Contains(message,"Access is denied.");
			StringAssert.Contains(message,"HRESULT 0x80070005");
			StringAssert.Contains(message,"Apps and features");
			StringAssert.Contains(message,"Repair");
			StringAssert.Contains(message,"msedgewebview2.exe");
			StringAssert.Contains(message,WebView2Policy.GetUserDataFolder());
			StringAssert.Contains(message,WebView2Policy.RuntimeDownloadUrl);
		}

		[TestMethod]
		public void StartFailure_InTheInitializationEvent_IsReportedTheSameWay_ButNotForAClosedControl() {
			List<string> listMessages=new List<string>();
			RunWithFailingEnvironment(new COMException("unused",HRESULT_ACCESS_DENIED),listMessages,() => {
				//A control that was closed while it started: no failure of the runtime.
				ODWebView2 odWebView2Closed=new ODWebView2();
				odWebView2Closed.Dispose();
				odWebView2Closed.CoreWebView2_InitializationCompleted(odWebView2Closed,
					new CoreWebView2InitializationCompletedEventArgs(new COMException("closed",HRESULT_ACCESS_DENIED)));
				using ODWebView2 odWebView2Aborted=new ODWebView2();
				odWebView2Aborted.CoreWebView2_InitializationCompleted(odWebView2Aborted,
					new CoreWebView2InitializationCompletedEventArgs(new COMException("aborted",unchecked((int)0x80004004))));
				Assert.AreEqual(0,listMessages.Count,"Closing a window while its browser starts is no failure: "+string.Join(" | ",listMessages));
				//The event of a control whose browser process could not be created.
				using ODWebView2 odWebView2=new ODWebView2();
				odWebView2.CoreWebView2_InitializationCompleted(odWebView2,
					new CoreWebView2InitializationCompletedEventArgs(new COMException("The browser process exited.",unchecked((int)0x8007139F))));
				odWebView2.CoreWebView2_InitializationCompleted(odWebView2,
					new CoreWebView2InitializationCompletedEventArgs(new COMException("again",unchecked((int)0x8007139F))));
			});
			Assert.AreEqual(1,listMessages.Count,string.Join(" | ",listMessages));
			StringAssert.Contains(listMessages[0],"The browser process exited.");
			StringAssert.Contains(listMessages[0],"HRESULT 0x8007139F");
		}

		[TestMethod]
		public void MissingRuntime_IsStillToldAsBefore() {
			List<string> listMessages=new List<string>();
			RunWithFailingEnvironment(new COMException("unused",HRESULT_ACCESS_DENIED),listMessages,() => {
				ODWebView2.FuncIsRuntimeAvailable=() => false;
				using ODWebView2 odWebView2=new ODWebView2();
				odWebView2.NavigateToHtml(HtmlPage.BuildText("page"));
			});
			Assert.AreEqual(1,listMessages.Count,string.Join(" | ",listMessages));
			StringAssert.Contains(listMessages[0],"The Microsoft Edge WebView2 Runtime is not installed on this computer");
		}
	}
}
