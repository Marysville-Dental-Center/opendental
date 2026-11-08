// S0-F (3): the prompt "Are you sure you would like to log off?" (and "...close?"). Its X has always meant No in the code (IsDialogCancel); what
// logged off after X in the window check was the prompt's own 15-second countdown, which closed it as Yes while the user was still on it (a
// computer-use click takes longer than that) and which ran on whatever the user did, against its own description "if no user input". Now any
// input in the box (a key, a mouse button, the mouse moved over it or its title bar) and the box closing stop the countdown for good; X and
// Esc stay No; only a box nobody touched for 15 seconds goes ahead. The countdown (InputBoxTimeout) is tested alone, then inside a real InputBox
// on an STA thread through its event handlers, without showing it; and the prompt's wiring by its source. No window is shown.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;

namespace S0FTests {
	[TestClass]
	public class LogOffPromptTests {
		#region InputBoxTimeout
		[TestMethod]
		public void Timeout_NobodyTouchesIt_TimesOut() {
			InputBoxTimeout inputBoxTimeout=new InputBoxTimeout();
			Assert.IsFalse(inputBoxTimeout.TryTimeOut(),"Not started: nothing to time out.");
			inputBoxTimeout.Start();
			Assert.IsTrue(inputBoxTimeout.IsRunning);
			Assert.IsFalse(inputBoxTimeout.NoteMousePosition(new Point(500,400)),"The first position is where the cursor rests, not input.");
			Assert.IsFalse(inputBoxTimeout.NoteMousePosition(new Point(500+InputBoxTimeout.MouseMoveTolerance,400-InputBoxTimeout.MouseMoveTolerance)),
				"A jitter within the tolerance is no input.");
			Assert.IsTrue(inputBoxTimeout.TryTimeOut(),"15 seconds without input: the box closes by itself.");
			Assert.IsTrue(inputBoxTimeout.IsTimedOut);
			Assert.IsFalse(inputBoxTimeout.IsRunning);
			Assert.IsFalse(inputBoxTimeout.TryTimeOut(),"Only once.");
			inputBoxTimeout.Start();
			Assert.IsFalse(inputBoxTimeout.IsRunning,"An ended countdown never runs again.");
		}

		[TestMethod]
		public void Timeout_InputOrClose_StopsItForGood() {
			//A key or a mouse button (Stop), and the box closing with OK, X or Esc (Stop).
			InputBoxTimeout inputBoxTimeout=new InputBoxTimeout();
			inputBoxTimeout.Start();
			inputBoxTimeout.Stop();
			Assert.IsFalse(inputBoxTimeout.IsRunning);
			Assert.IsFalse(inputBoxTimeout.TryTimeOut(),"After input or a close, the time running out does nothing.");
			Assert.IsFalse(inputBoxTimeout.IsTimedOut);
			inputBoxTimeout.Start();
			Assert.IsFalse(inputBoxTimeout.IsRunning,"It never starts again.");
			Assert.IsFalse(inputBoxTimeout.TryTimeOut());
			//Input before the box was shown keeps the countdown from starting at all.
			InputBoxTimeout inputBoxTimeoutEarly=new InputBoxTimeout();
			inputBoxTimeoutEarly.Stop();
			inputBoxTimeoutEarly.Start();
			Assert.IsFalse(inputBoxTimeoutEarly.IsRunning);
			Assert.IsFalse(inputBoxTimeoutEarly.TryTimeOut());
		}

		[TestMethod]
		public void Timeout_MouseMovedOverTheBox_IsInput() {
			InputBoxTimeout inputBoxTimeout=new InputBoxTimeout();
			Assert.IsFalse(inputBoxTimeout.NoteMousePosition(new Point(10,10)),"Not running: never input.");
			inputBoxTimeout.Start();
			Assert.IsFalse(inputBoxTimeout.NoteMousePosition(new Point(300,200)));
			Assert.IsTrue(inputBoxTimeout.NoteMousePosition(new Point(300+InputBoxTimeout.MouseMoveTolerance+1,200)),"Moved toward the X: input.");
			Assert.IsTrue(inputBoxTimeout.NoteMousePosition(new Point(300,200-InputBoxTimeout.MouseMoveTolerance-1)));
		}
		#endregion InputBoxTimeout

		#region InputBox
		///<summary>Runs action on a new STA thread (WPF controls need one) and rethrows what it threw.</summary>
		private static void RunSta(Action action) {
			Exception exception=null;
			Thread thread=new Thread(() => {
				try {
					action();
				}
				catch(Exception ex) {
					exception=ex;
				}
			});
			thread.SetApartmentState(ApartmentState.STA);
			thread.IsBackground=true;
			thread.Start();
			Assert.IsTrue(thread.Join(TimeSpan.FromMinutes(2)),"The STA thread finished.");
			if(exception!=null) {
				throw new AssertFailedException("On the STA thread: "+exception,exception);
			}
		}

		private static void Raise(InputBox inputBox,string handlerName,object eventArgs) {
			MethodInfo methodInfo=typeof(InputBox).GetMethod(handlerName,BindingFlags.Instance|BindingFlags.NonPublic);
			Assert.IsNotNull(methodInfo,handlerName);
			methodInfo.Invoke(inputBox,new object[] { inputBox,eventArgs });
		}

		private static T GetField<T>(InputBox inputBox,string fieldName) {
			FieldInfo fieldInfo=typeof(InputBox).GetField(fieldName,BindingFlags.Instance|BindingFlags.NonPublic);
			Assert.IsNotNull(fieldInfo,fieldName);
			return (T)fieldInfo.GetValue(inputBox);
		}

		private static InputBox NewPrompt() {
			InputBoxParam inputBoxParam=new InputBoxParam();
			inputBoxParam.InputBoxType_=InputBoxType.CheckBox;
			inputBoxParam.LabelText="Are you sure you would like to log off?";
			inputBoxParam.Text="Do not show me this message again.";
			InputBox inputBox=new InputBox(inputBoxParam);
			inputBox.HasTimeout=true;
			return inputBox;
		}

		[TestMethod]
		public void InputBox_ClickInTheBox_TheCountdownNeverCloses() {
			RunSta(() => {
				InputBox inputBox=NewPrompt();
				Raise(inputBox,"InputBox_Shown",EventArgs.Empty);
				System.Windows.Threading.DispatcherTimer dispatcherTimer=GetField<System.Windows.Threading.DispatcherTimer>(inputBox,"_dispatcherTimer");
				InputBoxTimeout inputBoxTimeout=GetField<InputBoxTimeout>(inputBox,"_inputBoxTimeout");
				Assert.IsTrue(dispatcherTimer.IsEnabled,"Shown starts the countdown.");
				Assert.IsTrue(inputBoxTimeout.IsRunning);
				Raise(inputBox,"InputBox_PreviewMouseDown",null);
				Assert.IsFalse(dispatcherTimer.IsEnabled,"A click in the box stops it.");
				Assert.IsFalse(inputBoxTimeout.IsRunning);
				Raise(inputBox,"Timer_Tick",EventArgs.Empty);//as if a tick was already queued
				Assert.IsFalse(inputBox.HasTimedOut,"The box did not close by itself.");
				Assert.IsTrue(inputBox.IsDialogCancel,"Not OK: closing it now with X or Esc is No.");
			});
		}

		[TestMethod]
		public void InputBox_ClosedWithX_ALateTickChangesNothing() {
			RunSta(() => {
				InputBox inputBox=NewPrompt();
				Raise(inputBox,"InputBox_Shown",EventArgs.Empty);
				Raise(inputBox,"InputBox_FormClosed",EventArgs.Empty);//X: the frame closes with Cancel
				Raise(inputBox,"Timer_Tick",EventArgs.Empty);
				Assert.IsFalse(inputBox.HasTimedOut);
				Assert.IsTrue(inputBox.IsDialogCancel,"X is No.");
			});
		}

		[TestMethod]
		public void InputBox_NobodyTouchesIt_ClosesByItselfAsTimedOut() {
			RunSta(() => {
				InputBox inputBox=NewPrompt();
				Raise(inputBox,"InputBox_Shown",EventArgs.Empty);
				Raise(inputBox,"Timer_Tick",EventArgs.Empty);
				Assert.IsTrue(inputBox.HasTimedOut,"15 seconds without input: the log off the user asked for goes ahead.");
				Assert.IsFalse(GetField<InputBoxTimeout>(inputBox,"_inputBoxTimeout").IsRunning);
			});
		}

		[TestMethod]
		public void InputBox_NoTimeout_NeverCountsDown() {
			RunSta(() => {
				InputBox inputBox=NewPrompt();
				inputBox.HasTimeout=false;
				Raise(inputBox,"InputBox_Shown",EventArgs.Empty);
				Assert.IsFalse(GetField<System.Windows.Threading.DispatcherTimer>(inputBox,"_dispatcherTimer").IsEnabled);
				Raise(inputBox,"Timer_Tick",EventArgs.Empty);
				Assert.IsFalse(inputBox.HasTimedOut);
			});
		}
		#endregion InputBox

		#region Wiring
		private static string ReadSource(string relativePath) {
			return File.ReadAllText(S0FDb.FindRepoFile(relativePath));
		}

		[TestMethod]
		public void Wiring_InputBox_EveryInputStopsTheCountdown() {
			string source=ReadSource(Path.Combine("WpfControlsOD","Frms","InputBox.xaml.cs"));
			foreach(string subscription in new[] { "PreviewKeyDown+=InputBox_PreviewKeyDown;","PreviewMouseDown+=InputBox_PreviewMouseDown;",
				"PreviewMouseMove+=InputBox_PreviewMouseMove;","FormClosed+=InputBox_FormClosed;","_formFrame.MouseMove+=FormFrame_MouseMove;",
				"_formFrame.MouseDown+=FormFrame_MouseDown;" })
			{
				StringAssert.Contains(source,subscription);
			}
			Match match=Regex.Match(source,@"private void InputBox_PreviewKeyDown\(object sender,KeyEventArgs e\) \{\s*StopTimeout\(\);");
			Assert.IsTrue(match.Success,"A key stops the countdown before anything else.");
			Match matchTick=Regex.Match(source,@"private void Timer_Tick\(object sender,EventArgs e\) \{(?<body>.*?)\r?\n\t\t\}",RegexOptions.Singleline);
			Assert.IsTrue(matchTick.Success);
			string body=matchTick.Groups["body"].Value;
			Assert.IsTrue(body.IndexOf("TryTimeOut()")>=0 && body.IndexOf("TryTimeOut()")<body.IndexOf("HasTimedOut=true;"),
				"The tick closes the box only when the countdown was still running.");
		}

		[TestMethod]
		public void Wiring_LogOffAndClosePrompt_XIsNo() {
			string source=ReadSource(Path.Combine("OpenPmsDental","Main Modules","FormOpenDental.cs"));
			Match match=Regex.Match(source,@"private bool AreYouSurePrompt\(long userNum,string message\) \{(?<body>.*?)\r?\n\t\t\}",RegexOptions.Singleline);
			Assert.IsTrue(match.Success);
			string body=match.Groups["body"].Value;
			StringAssert.Contains(body,"inputBox.HasTimeout=true;");
			Assert.IsTrue(Regex.IsMatch(body,@"if\(inputBox\.IsDialogCancel\) \{[^\r\n]*\r?\n\s*return false;"),"X or Esc (IsDialogCancel) answers No.");
			Assert.IsTrue(Regex.IsMatch(source,@"if\(!AreYouSurePrompt\(Security\.CurUser\.UserNum,Lan\.g\(this,""Are you sure you would like to log off\?""\)\)\) \{\s*return;\s*\}\s*LogOffNow\(false\);"),
				"No means no log off.");
			Assert.IsTrue(Regex.IsMatch(source,@"if\(!AreYouSurePrompt\(Security\.CurUser\.UserNum,Lan\.g\(this,""Are you sure you would like to close\?""\)\)\) \{\s*e\.Cancel=true;"),
				"No means the program stays open.");
		}
		#endregion Wiring
	}
}
