// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ImagingDeviceManager.Acquire;
using ImagingDeviceManager.TwainProtocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingDeviceManager.Tests {
	///<summary>A scripted set of top-level windows for ThreadWindowDisabler.</summary>
	internal sealed class FakeThreadWindowApi:IThreadWindowApi {
		public sealed class FakeWindow {
			public IntPtr Hwnd;
			public bool IsVisible=true;
			public bool IsEnabled=true;
			public bool IsDestroyed;
		}

		public readonly List<FakeWindow> ListWindows=new List<FakeWindow>();
		public readonly List<string> ListEnableCalls=new List<string>();

		public FakeWindow Add(bool isVisible=true,bool isEnabled=true) {
			FakeWindow window=new FakeWindow { Hwnd=new IntPtr(0x1000+ListWindows.Count*4),IsVisible=isVisible,IsEnabled=isEnabled };
			ListWindows.Add(window);
			return window;
		}

		private FakeWindow Find(IntPtr hwnd) {
			return ListWindows.FirstOrDefault(x => x.Hwnd==hwnd && !x.IsDestroyed);
		}

		public List<IntPtr> GetThreadWindows() {
			return ListWindows.Where(x => !x.IsDestroyed).Select(x => x.Hwnd).ToList();
		}

		public bool IsWindow(IntPtr hwnd) {
			return Find(hwnd)!=null;
		}

		public bool IsWindowVisible(IntPtr hwnd) {
			return Find(hwnd)?.IsVisible??false;
		}

		public bool IsWindowEnabled(IntPtr hwnd) {
			return Find(hwnd)?.IsEnabled??false;
		}

		public void EnableWindow(IntPtr hwnd,bool isEnabled) {
			ListEnableCalls.Add((isEnabled ? "enable " : "disable ")+hwnd.ToInt32().ToString("X"));
			FakeWindow window=Find(hwnd);
			if(window!=null) {
				window.IsEnabled=isEnabled;
			}
		}
	}

	///<summary>Thread-window bookkeeping with a scripted set of windows (design 7.1 "Thread-modal device acquisitions").</summary>
	[TestClass]
	public class ThreadWindowDisablerTests {
		[TestMethod]
		public void DisablesOnlyVisibleEnabledWindows_ReenablesExactlyThose() {
			FakeThreadWindowApi api=new FakeThreadWindowApi();
			FakeThreadWindowApi.FakeWindow windowMain=api.Add();
			FakeThreadWindowApi.FakeWindow windowHidden=api.Add(isVisible:false);
			FakeThreadWindowApi.FakeWindow windowDisabled=api.Add(isEnabled:false);//For example the owner of a modal dialog.
			FakeThreadWindowApi.FakeWindow windowOther=api.Add();
			ThreadWindowDisabler disabler=new ThreadWindowDisabler(api);
			Assert.AreEqual(2,disabler.DisableAll(null));
			Assert.IsFalse(windowMain.IsEnabled);
			Assert.IsFalse(windowOther.IsEnabled);
			Assert.IsTrue(windowHidden.IsEnabled,"A hidden window is not touched.");
			CollectionAssert.AreEqual(new List<IntPtr> { windowMain.Hwnd,windowOther.Hwnd },disabler.ListDisabled);
			disabler.ReenableAll();
			Assert.IsTrue(windowMain.IsEnabled);
			Assert.IsTrue(windowOther.IsEnabled);
			Assert.IsFalse(windowDisabled.IsEnabled,"A window that was already disabled stays disabled.");
			Assert.AreEqual(0,disabler.ListDisabled.Count);
			Assert.AreEqual(4,api.ListEnableCalls.Count,"Two disables and two enables, nothing else.");
		}

		[TestMethod]
		public void ExcludedWindowsStayEnabled() {
			FakeThreadWindowApi api=new FakeThreadWindowApi();
			FakeThreadWindowApi.FakeWindow windowMain=api.Add();
			FakeThreadWindowApi.FakeWindow windowNotice=api.Add();
			ThreadWindowDisabler disabler=new ThreadWindowDisabler(api);
			Assert.AreEqual(1,disabler.DisableAll(hwnd => hwnd==windowNotice.Hwnd));
			Assert.IsFalse(windowMain.IsEnabled);
			Assert.IsTrue(windowNotice.IsEnabled);
		}

		[TestMethod]
		public void WindowsCreatedAfterDisableAll_AreNeverTouched() {
			FakeThreadWindowApi api=new FakeThreadWindowApi();
			FakeThreadWindowApi.FakeWindow windowMain=api.Add();
			ThreadWindowDisabler disabler=new ThreadWindowDisabler(api);
			disabler.DisableAll(null);
			FakeThreadWindowApi.FakeWindow windowSource=api.Add();//The source's own window, shown during MSG_ENABLEDS.
			FakeThreadWindowApi.FakeWindow windowSourceDisabled=api.Add();
			windowSourceDisabled.IsEnabled=false;
			disabler.ReenableAll();
			Assert.IsTrue(windowMain.IsEnabled);
			Assert.IsTrue(windowSource.IsEnabled);
			Assert.IsFalse(windowSourceDisabled.IsEnabled,"Re-enabling touches only the windows it disabled.");
			Assert.IsFalse(api.ListEnableCalls.Any(x => x.EndsWith(windowSource.Hwnd.ToInt32().ToString("X"))));
		}

		[TestMethod]
		public void DestroyedWindowsAreSkipped_SecondReenableIsHarmless() {
			FakeThreadWindowApi api=new FakeThreadWindowApi();
			FakeThreadWindowApi.FakeWindow windowMain=api.Add();
			FakeThreadWindowApi.FakeWindow windowParent=api.Add();
			ThreadWindowDisabler disabler=new ThreadWindowDisabler(api);
			disabler.DisableAll(null);
			windowParent.IsDestroyed=true;//An automatic logoff closed it during the acquisition.
			disabler.ReenableAll();
			disabler.ReenableAll();
			Assert.IsTrue(windowMain.IsEnabled);
			Assert.AreEqual(3,api.ListEnableCalls.Count,"Two disables, one enable for the window that still exists.");
		}

		[TestMethod]
		public void DisableAllTwice_KeepsTheFirstList() {
			FakeThreadWindowApi api=new FakeThreadWindowApi();
			FakeThreadWindowApi.FakeWindow windowMain=api.Add();
			ThreadWindowDisabler disabler=new ThreadWindowDisabler(api);
			disabler.DisableAll(null);
			FakeThreadWindowApi.FakeWindow windowLater=api.Add();
			Assert.AreEqual(1,disabler.DisableAll(null));
			disabler.ReenableAll();
			Assert.IsTrue(windowMain.IsEnabled);
			Assert.IsTrue(windowLater.IsEnabled);
		}
	}

	///<summary>Thread-modal device acquisitions through the facade, with real windows (design 7.1): from MSG_ENABLEDS until the acquire
	///ends, every visible, enabled top-level window of the calling thread is disabled and exactly those are enabled again on every exit
	///path; windows created after the enable, and scans, are unaffected.</summary>
	[TestClass]
	public class ThreadModalTests {
		private FacadeHarness _harness;
		private readonly List<Form> _listForms=new List<Form>();
		private Form _formMain;
		private Form _formParent;

		[TestInitialize]
		public void Setup() {
			_harness=new FacadeHarness();
			_formMain=ShowForm();
			_formParent=ShowForm();//Like the hidden form the Mount/Acquire loop passes to Acquire.
		}

		[TestCleanup]
		public void Cleanup() {
			foreach(Form form in _listForms) {
				form.Dispose();
			}
			_harness.Dispose();
		}

		private Form ShowForm() {
			Form form=new Form {
				ShowInTaskbar=false,
				StartPosition=FormStartPosition.Manual,
				Location=new Point(-3000,-3000),
				Size=new Size(120,80),
			};
			_listForms.Add(form);
			form.Show();
			return form;
		}

		private static bool IsEnabled(Form form) {
			return NativeMethods.IsWindowEnabled(form.Handle);
		}

		private FakeHelperSource Helper => _harness.Helper;

		private IntPtr AcquireByName() {
			Assert.IsTrue(EZTwain.OpenSource(Helper.ProductName),EZTwain.LastErrorText());
			return EZTwain.Acquire(_formParent.Handle);
		}

		[TestMethod]
		public void ByName_EveryVisibleWindowDisabled_ExactlyThoseEnabledAfterXferReady() {
			Form formDisabledBefore=ShowForm();
			formDisabledBefore.Enabled=false;
			Form formInvisible=new Form { ShowInTaskbar=false };
			_listForms.Add(formInvisible);
			IntPtr hwndInvisible=formInvisible.Handle;
			bool? isMainEnabled=null;
			bool? isParentEnabled=null;
			Helper.ListExposures.Add(SessionHarness.Exposure(8));
			Helper.ActionOnEnable=() => {
				isMainEnabled=IsEnabled(_formMain);
				isParentEnabled=IsEnabled(_formParent);
			};
			IntPtr hdib=AcquireByName();
			Assert.AreNotEqual(IntPtr.Zero,hdib,EZTwain.LastErrorText());
			EZTwain.DIB_Free(hdib);
			Assert.AreEqual(false,isMainEnabled,"The main window is disabled, not only the root of the window passed in.");
			Assert.AreEqual(false,isParentEnabled);
			Assert.IsTrue(IsEnabled(_formMain));
			Assert.IsTrue(IsEnabled(_formParent));
			Assert.IsFalse(IsEnabled(formDisabledBefore),"A window that was disabled before stays disabled.");
			Assert.IsTrue(NativeMethods.IsWindowEnabled(hwndInvisible));
		}

		[TestMethod]
		public void ByName_EnabledAgainAfterCloseRequest() {
			bool? isMainEnabled=null;
			Helper.ActionOnEnable=() => isMainEnabled=IsEnabled(_formMain);//No exposure: the operator closes the window.
			Assert.AreEqual(IntPtr.Zero,AcquireByName());
			Assert.AreEqual((int)EZTwainErrorCode.EZTEC_USER_CANCEL,EZTwain.LastErrorCode());
			Assert.AreEqual(false,isMainEnabled);
			Assert.IsTrue(IsEnabled(_formMain));
			Assert.IsTrue(IsEnabled(_formParent));
		}

		[TestMethod]
		public void ByName_EnabledAgainAfterAnException() {
			Helper.ActionOnEnable=() => throw new InvalidOperationException("The test source failed.");
			Assert.AreEqual(IntPtr.Zero,AcquireByName());
			Assert.AreNotEqual(0,EZTwain.LastErrorCode());
			Assert.IsTrue(IsEnabled(_formMain));
			Assert.IsTrue(IsEnabled(_formParent));
		}

		[TestMethod]
		public void ByName_EnabledAgainWhenTheEnableFails() {
			Helper.EnableFails=true;
			Assert.AreEqual(IntPtr.Zero,AcquireByName());
			Assert.AreEqual((int)EZTwainErrorCode.EZTEC_ENABLE_FAILED,EZTwain.LastErrorCode());
			Assert.IsTrue(IsEnabled(_formMain));
		}

		[TestMethod]
		public void ByName_WindowCreatedDuringTheAcquisition_StaysEnabled() {
			Form formSource=null;
			bool? isSourceEnabledDuring=null;
			Helper.ListExposures.Add(SessionHarness.Exposure(8));
			Helper.EndAfterMs=100;
			Helper.ActionOnEnable=() => formSource=ShowForm();//Like a window the source shows during MSG_ENABLEDS.
			using(Timer timer=new Timer { Interval=40 }) {//Ticks inside the acquisition's message loop.
				timer.Tick+=(sender,e) => {
					if(formSource!=null && isSourceEnabledDuring==null) {
						isSourceEnabledDuring=IsEnabled(formSource);
					}
				};
				timer.Start();
				IntPtr hdib=AcquireByName();
				Assert.AreNotEqual(IntPtr.Zero,hdib,EZTwain.LastErrorText());
				EZTwain.DIB_Free(hdib);
			}
			Assert.AreEqual(true,isSourceEnabledDuring,"A window created after the enable is not disabled.");
			Assert.IsTrue(IsEnabled(formSource));
		}

		[TestMethod]
		public void ByName_WaitAfterARefusedDisable_StaysThreadModal() {
			_harness.WriteSafeDisableConfig();
			Helper.ListExposures.Add(SessionHarness.Exposure(8));
			Helper.EndAfterMs=60000;
			Helper.ActionOnEnable=() => EZTwain.Session.Cancel();
			List<bool> listMainEnabledWhileWaiting=new List<bool>();
			EZTwain.Session.WaitingForSourceChanged+=isWaiting => {
				if(isWaiting) {
					listMainEnabledWhileWaiting.Add(IsEnabled(_formMain));
				}
			};
			Assert.AreEqual(IntPtr.Zero,AcquireByName());
			CollectionAssert.AreEqual(new List<bool> { false },listMainEnabledWhileWaiting,"The wait is part of the acquisition.");
			Assert.IsTrue(IsEnabled(_formMain));
			Assert.IsTrue(IsEnabled(_formParent));
		}

		[TestMethod]
		public void Scan_DefaultSource_OnlyTheRootOfTheWindowPassedIsDisabled() {
			_harness.Fake.ListPages.Add(FakePage.CreateGray8(8,4,300,(x,y) => (byte)x));
			bool? isMainEnabled=null;
			bool? isParentEnabled=null;
			_harness.Fake.ActionOnEnable=() => {
				isMainEnabled=IsEnabled(_formMain);
				isParentEnabled=IsEnabled(_formParent);
			};
			Assert.IsTrue(EZTwain.OpenDefaultSource());
			IntPtr hdib=EZTwain.Acquire(_formParent.Handle);
			Assert.AreNotEqual(IntPtr.Zero,hdib,EZTwain.LastErrorText());
			EZTwain.DIB_Free(hdib);
			Assert.AreEqual(true,isMainEnabled,"Scans keep today's behaviour.");
			Assert.AreEqual(false,isParentEnabled);
			Assert.IsTrue(IsEnabled(_formParent));
		}

		[TestMethod]
		public void OptOut_Static_OnlyTheRootIsDisabled() {
			EZTwain.DisableThreadWindowsDuringAcquire=false;
			bool? isMainEnabled=null;
			bool? isParentEnabled=null;
			Helper.ListExposures.Add(SessionHarness.Exposure(8));
			Helper.ActionOnEnable=() => {
				isMainEnabled=IsEnabled(_formMain);
				isParentEnabled=IsEnabled(_formParent);
			};
			IntPtr hdib=AcquireByName();
			EZTwain.DIB_Free(hdib);
			Assert.AreEqual(true,isMainEnabled);
			Assert.AreEqual(false,isParentEnabled);
			Assert.IsTrue(IsEnabled(_formParent));
		}

		[TestMethod]
		public void OptOut_ConfigThreadModalZero_OnlyTheRootIsDisabled() {
			_harness.WriteConfig("[Twain]","ThreadModal=0");
			bool? isMainEnabled=null;
			Helper.ListExposures.Add(SessionHarness.Exposure(8));
			Helper.ActionOnEnable=() => isMainEnabled=IsEnabled(_formMain);
			IntPtr hdib=AcquireByName();
			EZTwain.DIB_Free(hdib);
			Assert.AreEqual(true,isMainEnabled);
		}
	}
}
