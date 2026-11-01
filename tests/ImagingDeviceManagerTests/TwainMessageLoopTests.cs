// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ImagingDeviceManager.TwainProtocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingDeviceManager.Tests {
	///<summary>A message-only window of the test thread, like COM's OLE window for calls into the apartment: counts the probe message it
	///is sent.</summary>
	internal sealed class ProbeWindow:NativeWindow,IDisposable {
		public const int WmProbe=0x8000+0x2F1;
		public int CountReceived;

		public ProbeWindow() {
			CreateHandle(new CreateParams { Parent=(IntPtr)(-3) });//HWND_MESSAGE
		}

		public void Post() {
			Assert.IsTrue(NativeMethods.PostMessage(Handle,WmProbe,IntPtr.Zero,IntPtr.Zero),"PostMessage");
		}

		protected override void WndProc(ref Message m) {
			if(m.Msg==WmProbe) {
				CountReceived++;
				return;
			}
			base.WndProc(ref m);
		}

		public void Dispose() {
			DestroyHandle();
		}
	}

	///<summary>The session's message loop and the program's messages: a source may report its notification on whatever message the loop
	///hands it next (twain-contract 1.1 section 5.3), and a message of the program's (a COM call into the apartment, a WinForms Invoke)
	///must still be dispatched, never claimed with the notification and lost.</summary>
	[TestClass]
	public class TwainMessageLoopTests {
		private SessionHarness _harness;
		private ProbeWindow _probe;

		[TestInitialize]
		public void Setup() {
			_harness=new SessionHarness();
			_probe=new ProbeWindow();
		}

		[TestCleanup]
		public void Cleanup() {
			_probe.Dispose();
			_harness.Dispose();
		}

		///<summary>Dispatches what is left in the thread's queue, as the program's own loop would after the acquisition.</summary>
		private static void DispatchQueued() {
			IntPtr pMsg=Marshal.AllocHGlobal(NativeMethods.MsgBufferSize);
			try {
				while(NativeMethods.PeekMessage(pMsg,IntPtr.Zero,0,0,NativeMethods.PM_REMOVE)) {
					NativeMethods.TranslateMessage(pMsg);
					NativeMethods.DispatchMessage(pMsg);
				}
			}
			finally {
				Marshal.FreeHGlobal(pMsg);
			}
		}

		private void AssertProbeDispatchedOnce() {
			DispatchQueued();
			Assert.AreEqual(1,_probe.CountReceived,"The program's message was claimed with the notification and lost.\r\n"+_harness.CallText);
		}

		[TestMethod]
		public void HelperProfile_NotificationOnTheNextMessage_TheProgramsMessageIsStillDispatched() {
			_harness.Helper.ListExposures.Add(SessionHarness.Exposure(8));
			//Inside MSG_ENABLEDS, before the source's own wake-up: the program's message is the first one the loop hands to the source.
			_harness.Helper.ActionOnEnable=() => _probe.Post();
			Assert.IsNotNull(_harness.AcquireByName(),_harness.Session.ErrorText);
			AssertProbeDispatchedOnce();
			Assert.IsTrue(_harness.Fake.ListCalls.Any(x => x.Name=="PROCESSEVENT" && x.EventMessage==ProbeWindow.WmProbe),
				"Every message still goes to the enabled source (TWAIN 2.4 p.12-3).\r\n"+_harness.CallText);
		}

		[TestMethod]
		public void HelperProfile_CloseRequestOnTheNextMessage_TheProgramsMessageIsStillDispatched() {
			_harness.Helper.ActionOnEnable=() => _probe.Post();
			Assert.IsNull(_harness.AcquireByName(),"No exposure: the helper ends with MSG_CLOSEDSREQ.");
			AssertProbeDispatchedOnce();
		}

		[TestMethod]
		public void DefaultSource_XferReadyOnTheNextMessage_TheProgramsMessageIsStillDispatched() {
			_harness.Fake.ListPages.Add(FakePage.CreateRgb(4,4,300,(x,y) => Color.Gray));
			_harness.Fake.ActionOnEnable=() => _probe.Post();
			Assert.IsTrue(_harness.Session.OpenSource(null),_harness.Session.ErrorText);
			Assert.IsNotNull(_harness.Session.Acquire(IntPtr.Zero),_harness.Session.ErrorText);
			AssertProbeDispatchedOnce();
		}

		[TestMethod]
		public void DefaultSource_CloseRequestOnTheNextMessage_TheProgramsMessageIsStillDispatched() {
			_harness.Fake.EnableBehavior=FakeEnableBehavior.CloseRequest;
			_harness.Fake.ActionOnEnable=() => _probe.Post();
			Assert.IsTrue(_harness.Session.OpenSource(null),_harness.Session.ErrorText);
			Assert.IsNull(_harness.Session.Acquire(IntPtr.Zero));
			AssertProbeDispatchedOnce();
		}

		[TestMethod]
		public void DefaultSource_NotificationNotClaimed_TheProgramsMessageIsDispatched() {
			//A source that reports its notification on the program's message but says it is not its own (TWRC_NOTDSEVENT).
			_harness.Fake.ListPages.Add(FakePage.CreateRgb(4,4,300,(x,y) => Color.Gray));
			_harness.Fake.IsNotificationNotDsEvent=true;
			_harness.Fake.ActionOnEnable=() => _probe.Post();
			Assert.IsTrue(_harness.Session.OpenSource(null),_harness.Session.ErrorText);
			Assert.IsNotNull(_harness.Session.Acquire(IntPtr.Zero),_harness.Session.ErrorText);
			AssertProbeDispatchedOnce();
		}

		[TestMethod]
		public void StopRequest_OnTheProgramsMessage_TheMessageIsStillDispatched() {
			//The stop request is seen on the program's message, which the loop already took from the queue.
			_harness.Fake.EnableBehavior=FakeEnableBehavior.Nothing;
			_harness.Fake.ListPages.Add(FakePage.CreateRgb(4,4,300,(x,y) => Color.Gray));
			_harness.Fake.ActionOnEnable=() => {
				_probe.Post();
				_harness.Session.Cancel();
			};
			Assert.IsTrue(_harness.Session.OpenSource(null),_harness.Session.ErrorText);
			Assert.IsNull(_harness.Session.Acquire(IntPtr.Zero));
			Assert.AreEqual(TwainStopReason.Cancel,_harness.Session.LastStopReason);
			AssertProbeDispatchedOnce();
		}
	}
}
