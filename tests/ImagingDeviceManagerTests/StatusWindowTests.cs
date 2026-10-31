// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ImagingDeviceManager.Acquire;
using ImagingDeviceManager.TwainProtocol;
using ImagingDeviceManager.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingDeviceManager.Tests {
	///<summary>The status and Cancel window's logic (design 7.1 "Status and Cancel window").</summary>
	[TestClass]
	public class AcquireStatusControllerTests {
		private int _countCancel;

		private AcquireStatusController Create(bool isSafe,bool isBatch,string unassignedText=null) {
			_countCancel=0;
			return new AcquireStatusController(() => isSafe,isBatch,() => _countCancel++,unassignedText);
		}

		[TestMethod]
		public void Open_ShowsTheStatusText() {
			AcquireStatusController controller=Create(false,false);
			Assert.AreEqual("Sensor software is open. Cancel ends this series.",controller.MessageText);
			Assert.AreEqual("Cancel",controller.CancelText);
			Assert.IsTrue(controller.IsCancelEnabled);
			Assert.IsFalse(controller.IsBackVisible);
			Assert.IsNull(controller.UnassignedText);
		}

		[TestMethod]
		public void SafeDisable_CancelStopsAtOnce() {
			AcquireStatusController controller=Create(true,false);
			int countChanged=0;
			controller.Changed+=() => countChanged++;
			controller.ClickCancel();
			Assert.AreEqual(1,_countCancel);
			Assert.IsTrue(controller.IsCancelRequested);
			Assert.IsFalse(controller.IsCancelEnabled);
			Assert.AreEqual("Cancelling",controller.CancelText);
			Assert.IsTrue(countChanged>0);
			controller.ClickCancel();
			Assert.AreEqual(1,_countCancel,"Only once.");
		}

		[TestMethod]
		public void NoSafeDisable_Single_WarnsFirst_ThenCancels() {
			AcquireStatusController controller=Create(false,false);
			controller.ClickCancel();
			Assert.AreEqual(0,_countCancel);
			Assert.IsTrue(controller.IsWarningShown);
			Assert.AreEqual("If an exposure was just taken, wait until it appears before cancelling.",controller.MessageText);
			Assert.AreEqual("Cancel anyway",controller.CancelText);
			Assert.IsTrue(controller.IsBackVisible);
			controller.ClickCancel();
			Assert.AreEqual(1,_countCancel);
			Assert.IsFalse(controller.IsBackVisible);
		}

		[TestMethod]
		public void NoSafeDisable_Batch_WarnsAboutImagesTaken() {
			AcquireStatusController controller=Create(false,true);
			controller.ClickCancel();
			Assert.AreEqual("Cancel here may lose images already taken in this series; use Done in the sensor software to keep them.",controller.MessageText);
			Assert.AreEqual(0,_countCancel);
		}

		[TestMethod]
		public void Warning_KeepWaiting_BackToOpen() {
			AcquireStatusController controller=Create(false,false);
			controller.ClickCancel();
			controller.ClickBack();
			Assert.IsFalse(controller.IsWarningShown);
			Assert.AreEqual(CaptureNoticeText.StatusOpen,controller.MessageText);
			controller.ClickCancel();
			Assert.IsTrue(controller.IsWarningShown,"The warning comes again.");
			Assert.AreEqual(0,_countCancel);
		}

		[TestMethod]
		public void SafeDisableAskedAtClickTime() {
			bool isSafe=false;
			AcquireStatusController controller=new AcquireStatusController(() => isSafe,false,() => _countCancel++,null);
			isSafe=true;//The source accepted the capability just before the enable, after the window was shown.
			controller.ClickCancel();
			Assert.AreEqual(1,_countCancel);
		}

		[TestMethod]
		public void Waiting_ShowsTheWaitText_CancelOff() {
			AcquireStatusController controller=Create(false,false);
			controller.ClickCancel();
			controller.SetWaiting(true);
			Assert.AreEqual("Waiting for the sensor software to finish.",controller.MessageText);
			Assert.IsFalse(controller.IsCancelEnabled);
			Assert.IsFalse(controller.IsBackVisible);
			controller.ClickCancel();
			Assert.AreEqual(0,_countCancel,"Cancel does nothing while waiting.");
			controller.SetWaiting(false);
			Assert.IsTrue(controller.IsWarningShown);
		}

		[TestMethod]
		public void UnassignedText_BlankIsNone() {
			Assert.IsNull(Create(false,false,"  ").UnassignedText);
			Assert.AreEqual("x",Create(false,false,"x").UnassignedText);
		}
	}

	///<summary>The WinForms status window shows what its controller says and passes its buttons on.</summary>
	[TestClass]
	public class AcquireStatusWindowTests {
		private int _countCancel;

		[TestCleanup]
		public void Cleanup() {
			GC.Collect();
			GC.WaitForPendingFinalizers();
		}

		[TestMethod]
		public void Buttons_DriveTheController() {
			AcquireStatusController controller=new AcquireStatusController(() => false,false,() => _countCancel++,"3 unassigned captures on this computer.");
			using(AcquireStatusWindow window=new AcquireStatusWindow(controller)) {
				window.Show();
				Assert.IsTrue(window.TopMost);
				Assert.IsNull(window.Owner);
				Assert.AreEqual(CaptureNoticeText.StatusOpen,window.MessageShown);
				Assert.IsFalse(window.ButtonBack.Visible);
				window.ButtonCancel.PerformClick();
				Assert.AreEqual(CaptureNoticeText.CancelWarningSingle,window.MessageShown);
				Assert.IsTrue(window.ButtonBack.Visible);
				window.ButtonBack.PerformClick();
				Assert.AreEqual(CaptureNoticeText.StatusOpen,window.MessageShown);
				window.ButtonCancel.PerformClick();
				window.ButtonCancel.PerformClick();
				Assert.AreEqual(1,_countCancel);
				Assert.IsFalse(window.ButtonCancel.Enabled);
				controller.SetWaiting(true);
				Assert.AreEqual(CaptureNoticeText.Waiting,window.MessageShown);
			}
		}

		[TestMethod]
		public void UserClose_IsACancelRequest_LibraryCloseCloses() {
			AcquireStatusController controller=new AcquireStatusController(() => true,false,() => _countCancel++,null);
			AcquireStatusWindow window=new AcquireStatusWindow(controller);
			window.Show();
			window.Close();//As Alt+F4 does.
			Assert.IsFalse(window.IsDisposed,"The window stays until the acquisition ends.");
			Assert.AreEqual(1,_countCancel);
			((IAcquireStatusView)window).Close();
			Assert.IsTrue(window.IsDisposed);
			((IAcquireStatusView)window).Close();//Harmless.
		}
	}

	///<summary>The status and Cancel window around facade acquisitions.</summary>
	[TestClass]
	public class StatusWindowTests {
		private FacadeHarness _harness;

		[TestInitialize]
		public void Setup() {
			_harness=new FacadeHarness();
		}

		[TestCleanup]
		public void Cleanup() {
			_harness.Dispose();
		}

		private FakeHelperSource Helper => _harness.Helper;
		private RecordingCaptureUi Ui => _harness.Ui;

		private IntPtr AcquireByName() {
			Assert.IsTrue(EZTwain.OpenSource(Helper.ProductName),EZTwain.LastErrorText());
			return EZTwain.Acquire(IntPtr.Zero);
		}

		///<summary>The operator took an image and the helper is still reading it out.</summary>
		private void HelperBusyWithOneImage() {
			Helper.ListExposures.Add(SessionHarness.Exposure(8));
			Helper.EndAfterMs=60000;
		}

		[TestMethod]
		public void ShownWhileTheSourceIsEnabled_ClosedAfter() {
			bool? isShownDuringEnable=null;
			Helper.ListExposures.Add(SessionHarness.Exposure(8));
			Helper.ActionOnEnable=() => isShownDuringEnable=Ui.StatusLast!=null && !Ui.StatusLast.IsClosed;
			IntPtr hdib=AcquireByName();
			Assert.AreNotEqual(IntPtr.Zero,hdib,EZTwain.LastErrorText());
			EZTwain.DIB_Free(hdib);
			Assert.AreEqual(true,isShownDuringEnable);
			Assert.AreEqual(1,Ui.ListStatus.Count);
			Assert.IsTrue(Ui.StatusLast.IsClosed);
			Assert.AreEqual(CaptureNoticeText.StatusOpen,Ui.StatusLast.Controller.MessageText);
		}

		[TestMethod]
		public void NotShownForScans() {
			_harness.Fake.ListPages.Add(FakePage.CreateGray8(8,4,300,(x,y) => (byte)x));
			Assert.IsTrue(EZTwain.OpenDefaultSource());
			EZTwain.DIB_Free(EZTwain.Acquire(IntPtr.Zero));
			Assert.AreEqual(0,Ui.ListStatus.Count);
		}

		[TestMethod]
		public void NotShownWhenThreadModalIsOff() {
			_harness.WriteConfig("[Twain]","ThreadModal=0");
			Helper.ListExposures.Add(SessionHarness.Exposure(8));
			EZTwain.DIB_Free(AcquireByName());
			Assert.AreEqual(0,Ui.ListStatus.Count);
		}

		[TestMethod]
		public void Cancel_SafeDisableInEffect_StopsAtOnce() {
			_harness.WriteSafeDisableConfig();
			HelperBusyWithOneImage();
			Helper.ActionOnEnable=() => Ui.StatusLast.Controller.ClickCancel();
			Assert.AreEqual(IntPtr.Zero,AcquireByName());
			Assert.AreEqual((int)EZTwainErrorCode.EZTEC_USER_CANCEL,EZTwain.LastErrorCode());
			Assert.AreEqual(TwainStopReason.Cancel,EZTwain.Session.LastStopReason);
			Assert.IsFalse(Ui.StatusLast.Controller.IsWarningShown,"No warning when the disable is safe.");
			Assert.AreEqual(0,Helper.CountLostForHost);
		}

		[TestMethod]
		public void Cancel_NoSafeDisable_Single_WarnsAndDoesNotStop() {
			Helper.ListExposures.Add(SessionHarness.Exposure(8));
			Helper.EndAfterMs=150;
			Helper.ActionOnEnable=() => Ui.StatusLast.Controller.ClickCancel();
			IntPtr hdib=AcquireByName();
			Assert.AreNotEqual(IntPtr.Zero,hdib,"The exposure still arrives: "+EZTwain.LastErrorText());
			EZTwain.DIB_Free(hdib);
			Assert.IsTrue(Ui.StatusLast.Controller.IsWarningShown);
			Assert.AreEqual(CaptureNoticeText.CancelWarningSingle,Ui.StatusLast.Controller.MessageText);
			Assert.AreEqual(TwainStopReason.None,EZTwain.Session.LastStopReason);
		}

		[TestMethod]
		public void Cancel_NoSafeDisable_CancelAnyway_Stops() {
			Helper.End=FakeHelperEnd.Never;
			Helper.ActionOnEnable=() => {
				Ui.StatusLast.Controller.ClickCancel();
				Ui.StatusLast.Controller.ClickCancel();
			};
			Assert.AreEqual(IntPtr.Zero,AcquireByName());
			Assert.AreEqual((int)EZTwainErrorCode.EZTEC_USER_CANCEL,EZTwain.LastErrorCode());
			Assert.AreEqual(TwainStopReason.Cancel,EZTwain.Session.LastStopReason);
		}

		[TestMethod]
		public void Cancel_NoSafeDisable_Batch_WarnsAboutImagesTaken() {
			Helper.ListExposures.Add(SessionHarness.Exposure(8));
			Helper.ListExposures.Add(SessionHarness.Exposure(9));
			Helper.EndAfterMs=150;//The operator clicks Done.
			Helper.ActionOnEnable=() => Ui.StatusLast.Controller.ClickCancel();
			Assert.IsTrue(EZTwain.OpenSource(Helper.ProductName));
			EZTwain.SetMultiTransfer(true);
			int count=0;
			while(true) {
				IntPtr hdib=EZTwain.Acquire(IntPtr.Zero);
				if(hdib==IntPtr.Zero) {
					break;
				}
				count++;
				EZTwain.DIB_Free(hdib);
				if(EZTwain.IsDone()) {
					break;
				}
			}
			EZTwain.CloseSource();
			Assert.AreEqual(2,count,"Done delivers every image.");
			Assert.AreEqual(1,Ui.ListStatus.Count,"The second image of the batch needs no enable.");
			Assert.AreEqual(CaptureNoticeText.CancelWarningBatch,Ui.StatusLast.Controller.MessageText);
		}

		[TestMethod]
		public void WaitAfterRefusedDisable_SaysSo_CancelOff() {
			_harness.WriteSafeDisableConfig();
			HelperBusyWithOneImage();
			List<string> listTexts=new List<string>();
			Helper.ActionOnEnable=() => {
				//Subscribed after the status window, so it sees what the window shows.
				EZTwain.Session.WaitingForSourceChanged+=isWaiting => {
					listTexts.Add(Ui.StatusLast.Controller.MessageText+"|"+Ui.StatusLast.Controller.IsCancelEnabled);
				};
				Ui.StatusLast.Controller.ClickCancel();
			};
			Assert.AreEqual(IntPtr.Zero,AcquireByName());
			Assert.AreEqual(2,listTexts.Count);
			Assert.AreEqual(CaptureNoticeText.Waiting+"|False",listTexts[0]);
			Assert.IsTrue(Ui.StatusLast.IsClosed);
		}

		[TestMethod]
		public void Cancel_SourceThatNeverAnswers_Works() {
			Helper.End=FakeHelperEnd.Never;
			using(Timer timer=new Timer { Interval=50 }) {
				timer.Tick+=(sender,e) => {
					timer.Stop();
					Ui.StatusLast.Controller.ClickCancel();
					Ui.StatusLast.Controller.ClickCancel();
				};
				timer.Start();
				Assert.AreEqual(IntPtr.Zero,AcquireByName());
			}
			Assert.AreEqual((int)EZTwainErrorCode.EZTEC_USER_CANCEL,EZTwain.LastErrorCode());
			Assert.AreEqual(TwainStopReason.Cancel,EZTwain.Session.LastStopReason);
		}
	}
}
