// The windows that cannot work without their WebView2 and show an error of their own (FormCareCreditWeb, FormPayConnect2, FormPayConnect2iFrame,
// the Images module's PDF print) show exactly one message when the browser cannot start: ODWebView2.Init's own for a missing or failing
// WebView2 Runtime, never that and the window's FriendlyException as well. Through the seams of ODWebView2 (FuncIsRuntimeAvailable,
// FuncCreateEnvironment, ActionShowMessage) and WebView2L (ActionShowError), every message is counted instead of shown: no window, no browser.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CodeBase;
using CodeBase.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Web.WebView2.Core;
using OpenPmsDental;

namespace LibWebTests {
	[TestClass]
	public class WebView2InitMessageTests {
		private const int HRESULT_ACCESS_DENIED=unchecked((int)0x80070005);
		private const int HRESULT_ABORT=unchecked((int)0x80004004);

		///<summary>Every message a window's browser setup showed, in order: "Init: ..." from ODWebView2, "Window: ..." from the window (WebView2L).</summary>
		private class Messages {
			public List<string> ListMessages=new List<string>();

			public int Count {
				get {
					return ListMessages.Count;
				}
			}

			public override string ToString() {
				return string.Join(" | ",ListMessages);
			}
		}

		///<summary>Runs action on an STA thread with the seams set: the runtime is installed when isRuntimeAvailable, every environment fails with
		///exceptionEnvironment, and both kinds of message are recorded. Restores the seams and the once-per-session flag afterwards.</summary>
		private static void Run(bool isRuntimeAvailable,Exception exceptionEnvironment,Messages messages,Action action) {
			Exception exceptionThread=null;
			Thread thread=new Thread(() => {
				Func<bool> funcIsRuntimeAvailableOld=ODWebView2.FuncIsRuntimeAvailable;
				Func<Task<CoreWebView2Environment>> funcCreateEnvironmentOld=ODWebView2.FuncCreateEnvironment;
				Action<string> actionShowMessageOld=ODWebView2.ActionShowMessage;
				Action<string,Exception> actionShowErrorOld=WebView2L.ActionShowError;
				bool isUnitTestOld=ODBuild.IsUnitTest;
				try {
					ODBuild.IsUnitTest=true;//The logger writes nothing.
					ODWebView2.ResetStartFailureShown();
					ODWebView2.FuncIsRuntimeAvailable=() => isRuntimeAvailable;
					ODWebView2.FuncCreateEnvironment=() => {
						TaskCompletionSource<CoreWebView2Environment> taskCompletionSource=new TaskCompletionSource<CoreWebView2Environment>();
						taskCompletionSource.SetException(exceptionEnvironment);
						return taskCompletionSource.Task;
					};
					ODWebView2.ActionShowMessage=x => messages.ListMessages.Add("Init: "+x);
					WebView2L.ActionShowError=(text,ex) => messages.ListMessages.Add("Window: "+text+" "+ex?.Message);
					action();
				}
				catch(Exception ex) {
					exceptionThread=ex;
				}
				finally {
					ODWebView2.FuncIsRuntimeAvailable=funcIsRuntimeAvailableOld;
					ODWebView2.FuncCreateEnvironment=funcCreateEnvironmentOld;
					ODWebView2.ActionShowMessage=actionShowMessageOld;
					WebView2L.ActionShowError=actionShowErrorOld;
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

		///<summary>What a window does (WebView2L.TryInit with the window's own setup), on a new control. The environment of these tests fails at
		///once, so this completes on the calling thread.</summary>
		private static bool OpenWindow(string textError,ref int countSetups) {
			int countSetupsRun=0;
			using ODWebView2 odWebView2=new ODWebView2();
			bool isInitialized=WebView2L.TryInit(odWebView2,textError,() => {
				countSetupsRun++;
				return Task.CompletedTask;
			}).GetAwaiter().GetResult();
			countSetups+=countSetupsRun;
			return isInitialized;
		}

		[TestMethod]
		public void MissingRuntime_OneMessage_InitsOwn() {
			Messages messages=new Messages();
			int countSetups=0;
			Run(isRuntimeAvailable:false,new COMException("unused",HRESULT_ACCESS_DENIED),messages,() => {
				Assert.IsFalse(OpenWindow("Error loading window.",ref countSetups));
				Assert.AreEqual(1,messages.Count,"One message for the first window: "+messages);
				//Every window the user opens says it again, once.
				Assert.IsFalse(OpenWindow("Error initializing window.",ref countSetups));
			});
			Assert.AreEqual(2,messages.Count,messages.ToString());
			Assert.IsTrue(messages.ListMessages.TrueForAll(x => x.StartsWith("Init: The Microsoft Edge WebView2 Runtime is not installed on this computer")),
				messages.ToString());
			Assert.AreEqual(0,countSetups,"The window's own setup never runs without its browser.");
		}

		[TestMethod]
		public void RuntimeThatWillNotStart_OneMessagePerWindow_EvenAfterInitToldTheSessionOnce() {
			Messages messages=new Messages();
			int countSetups=0;
			Run(isRuntimeAvailable:true,new COMException("Access is denied.",HRESULT_ACCESS_DENIED),messages,() => {
				//The first window: Init tells the user (once per program session), the window shows nothing more.
				Assert.IsFalse(OpenWindow("Error loading window.",ref countSetups));
				Assert.AreEqual(1,messages.Count,messages.ToString());
				StringAssert.Contains(messages.ListMessages[0],"This message shows once until the program is started again.");
				//A wiki page after that: Init holds its message back, as before.
				using(ODWebView2 odWebView2Wiki=new ODWebView2()) {
					odWebView2Wiki.NavigateToHtml(HtmlPage.BuildText("wiki"));
				}
				Assert.AreEqual(1,messages.Count,"The wiki says nothing again: "+messages);
				//A payment window the user opens later: Init holds its message back, so the window shows it, once.
				Assert.IsFalse(OpenWindow("Error initializing window.",ref countSetups));
				Assert.AreEqual(2,messages.Count,messages.ToString());
			});
			foreach(string message in messages.ListMessages) {
				StringAssert.StartsWith(message,"Init: The Microsoft Edge WebView2 Runtime could not start");
				StringAssert.Contains(message,"Access is denied.");
				StringAssert.Contains(message,"HRESULT 0x80070005");
				StringAssert.Contains(message,"msedgewebview2.exe");
			}
			Assert.IsFalse(messages.ListMessages[1].Contains("shows once"),"The window's message makes no promise about the next one.");
			Assert.AreEqual(0,countSetups);
		}

		[TestMethod]
		public void WindowClosedWhileItsBrowserStarts_NoMessage() {
			Messages messages=new Messages();
			int countSetups=0;
			Run(isRuntimeAvailable:true,new COMException("Operation aborted",HRESULT_ABORT),messages,() => {
				Assert.IsFalse(OpenWindow("Error loading window.",ref countSetups));
			});
			Assert.AreEqual(0,messages.Count,messages.ToString());
		}

		[TestMethod]
		public void InitException_SaysWhetherTheUserWasTold() {
			Messages messages=new Messages();
			Run(isRuntimeAvailable:true,new COMException("Access is denied.",HRESULT_ACCESS_DENIED),messages,() => {
				using ODWebView2 odWebView2=new ODWebView2();
				ODWebView2InitException odWebView2InitException=null;
				try {
					odWebView2.Init().GetAwaiter().GetResult();
				}
				catch(ODWebView2InitException ex) {
					odWebView2InitException=ex;
				}
				Assert.IsNotNull(odWebView2InitException);
				Assert.IsInstanceOfType(odWebView2InitException,typeof(ODException),"The windows' \"Init told the user\" catches still hold.");
				Assert.IsTrue(odWebView2InitException.IsUserTold);
				Assert.IsFalse(odWebView2InitException.IsClosed);
				Assert.IsInstanceOfType(odWebView2InitException.InnerException,typeof(COMException));
				Assert.IsTrue(ODWebView2.TryShowInitFailure(odWebView2InitException),"Init's failure: nothing more to show.");
				Assert.AreEqual(1,messages.Count);
				//Any other error is the window's to show.
				Assert.IsFalse(ODWebView2.TryShowInitFailure(new InvalidOperationException("setup")));
				Assert.IsFalse(ODWebView2.TryShowInitFailure(null));
				Assert.AreEqual(1,messages.Count,messages.ToString());
			});
		}

		[TestMethod]
		public void Windows_ShowTheirOwnErrorOnlyThroughWebView2L() {
			string[][] arrayPaths={ new[] { "OpenPmsDental","Forms","FormCareCreditWeb.cs" },new[] { "OpenPmsDental","Forms","FormPayConnect2.cs" },
				new[] { "OpenPmsDental","Forms","FormPayConnect2iFrame.cs" },new[] { "OpenPmsDental","User Controls","ControlImageDisplay.cs" } };
			foreach(string[] arrayPath in arrayPaths) {
				string source=TestPaths.ReadSource(arrayPath);
				string name=string.Join("\\",arrayPath);
				StringAssert.Contains(source,"await WebView2L.TryInit(",name);
				//The old pattern: Init in a try whose catch shows FriendlyException, after Init's own message.
				Assert.IsFalse(Regex.IsMatch(source,@"await \w+\.Init\(\);[^}]*\}\s*catch\([^)]*\)\s*\{[^}]*FriendlyException\.Show"),name);
				Assert.IsFalse(Regex.IsMatch(source,@"FriendlyException\.Show\(""Error (initializing window|printing PDFs)\."""),name);
			}
			string helper=TestPaths.ReadSource("OpenPmsDental","Logic","WebView2L.cs");
			StringAssert.Contains(helper,"if(!ODWebView2.TryShowInitFailure(ex)) {");
		}
	}
}
