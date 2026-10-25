// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ImagingDeviceManager.Acquire;
using ImagingDeviceManager.TwainProtocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingDeviceManager.Tests {
	///<summary>Which notice and LastErrorText a by-name acquisition ends with (design 7.1 "Error notice").</summary>
	[TestClass]
	public class AcquireNoticeSelectionTests {
		[TestMethod]
		public void EndedWithoutImage_TextOrHexCode() {
			Assert.AreEqual("Sensor software ended without an image: No sensor is connected. Do not re-expose until you have checked the sensor software.",
				CaptureNoticeText.EndedWithoutImage(0x8002,"No sensor is connected."));
			Assert.AreEqual("Sensor software ended without an image: 0x8001. Do not re-expose until you have checked the sensor software.",
				CaptureNoticeText.EndedWithoutImage(0x8001,null));
			StringAssert.Contains(CaptureNoticeText.EndedWithoutImage(0x8001,"  "),": 0x8001.");
		}

		[TestMethod]
		public void CloseRequest_FailureCode_NoticeAndErrorText() {
			AcquireOutcome outcome=new AcquireOutcome { ErrorCode=EZTwainErrorCode.EZTEC_USER_CANCEL,EndConditionCode=0x8002,EndConditionText="No sensor." };
			string expected=CaptureNoticeText.EndedWithoutImage(0x8002,"No sensor.");
			CollectionAssert.AreEqual(new List<string> { expected },AcquireNotices.Select(new CaptureCallState(),outcome));
			Assert.AreEqual(expected,AcquireNotices.GetErrorText(outcome));
		}

		[TestMethod]
		public void PlainCancel_Silent() {
			AcquireOutcome outcome=new AcquireOutcome { ErrorCode=EZTwainErrorCode.EZTEC_USER_CANCEL,EndConditionCode=Tw.TWCC_SUCCESS };
			Assert.AreEqual(0,AcquireNotices.Select(new CaptureCallState(),outcome).Count);
			Assert.IsNull(AcquireNotices.GetErrorText(outcome));
			outcome.EndConditionCode=null;//The status could not be read.
			Assert.AreEqual(0,AcquireNotices.Select(new CaptureCallState(),outcome).Count);
		}

		[TestMethod]
		public void TransferFailure_ErrorTextOnly_TheLoopShowsIt() {
			AcquireOutcome outcome=new AcquireOutcome { ErrorCode=EZTwainErrorCode.EZTEC_XFER_FAILURE,IsTransferFailed=true };
			Assert.AreEqual("Image not received. Do not re-expose until you have checked the sensor software.",AcquireNotices.GetErrorText(outcome));
			Assert.AreEqual(0,AcquireNotices.Select(new CaptureCallState(),outcome).Count);
		}

		[TestMethod]
		public void SourceStoppedResponding_ErrorTextAndNotice_TheLoopIgnoresIt() {
			AcquireOutcome outcome=new AcquireOutcome { ErrorCode=EZTwainErrorCode.EZTEC_DS_FAILURE };
			Assert.AreEqual(CaptureNoticeText.ImageNotReceived,AcquireNotices.GetErrorText(outcome));
			CollectionAssert.AreEqual(new List<string> { CaptureNoticeText.ImageNotReceived },AcquireNotices.Select(new CaptureCallState(),outcome));
		}

		[TestMethod]
		public void ImageDelivered_NoErrorText() {
			AcquireOutcome outcome=new AcquireOutcome { HasImage=true };
			Assert.IsNull(AcquireNotices.GetErrorText(outcome));
			Assert.AreEqual(0,AcquireNotices.Select(new CaptureCallState(),outcome).Count);
		}

		[TestMethod]
		public void OtherErrors_KeepTheSessionText() {
			Assert.IsNull(AcquireNotices.GetErrorText(new AcquireOutcome { ErrorCode=EZTwainErrorCode.EZTEC_ENABLE_FAILED }));
			Assert.IsNull(AcquireNotices.GetErrorText(new AcquireOutcome { ErrorCode=EZTwainErrorCode.EZTEC_USER_CANCEL }));
		}

		[TestMethod]
		public void Order_EndReasonFirst_ThenQuarantine_ThenMayStillHold() {
			CaptureCallState call=new CaptureCallState { IsMayStillHoldImages=true };
			call.ListQuarantined.Add(new QuarantinedImage { ReceivedAt=new DateTime(2026,10,25),PathImage="a.bmp" });
			AcquireOutcome outcome=new AcquireOutcome { ErrorCode=EZTwainErrorCode.EZTEC_USER_CANCEL,EndConditionCode=0x8005 };
			List<string> listNotices=AcquireNotices.Select(call,outcome);
			Assert.AreEqual(3,listNotices.Count);
			StringAssert.StartsWith(listNotices[0],"Sensor software ended without an image: 0x8005.");
			StringAssert.StartsWith(listNotices[1],"1 image(s) received");
			Assert.AreEqual(CaptureNoticeText.MayStillHoldImages,listNotices[2]);
		}
	}

	///<summary>The end-reason notice and the transfer-failure text through the facade.</summary>
	[TestClass]
	public class AcquireNoticeTests {
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

		private IntPtr AcquireByName() {
			Assert.IsTrue(EZTwain.OpenSource(Helper.ProductName),EZTwain.LastErrorText());
			return EZTwain.Acquire(IntPtr.Zero);
		}

		[TestMethod]
		public void CloseRequestWithAReason_NonModalNotice_SeriesEndsAsOnACancel() {
			Helper.End=FakeHelperEnd.Fail;
			Helper.EndCode=FakeHelperSource.CodeNoSensor;
			Assert.AreEqual(IntPtr.Zero,AcquireByName());
			Assert.AreEqual((int)EZTwainErrorCode.EZTEC_USER_CANCEL,EZTwain.LastErrorCode(),"The loop ends the series silently.");
			string expected=CaptureNoticeText.EndedWithoutImage(FakeHelperSource.CodeNoSensor,FakeHelperSource.GetText(FakeHelperSource.CodeNoSensor));
			CollectionAssert.AreEqual(new List<string> { expected },_harness.Ui.ListNotices);
			Assert.AreEqual(expected,EZTwain.LastErrorText());
		}

		[TestMethod]
		public void CloseRequestWithAReason_NoStatusText_HexCode() {
			Helper.End=FakeHelperEnd.Fail;
			Helper.EndCode=FakeHelperSource.CodeNoCalibration;
			Helper.SupportsStatusUtf8=false;
			Assert.AreEqual(IntPtr.Zero,AcquireByName());
			CollectionAssert.AreEqual(new List<string> {
				"Sensor software ended without an image: 0x8001. Do not re-expose until you have checked the sensor software." },_harness.Ui.ListNotices);
		}

		[TestMethod]
		public void PlainCancel_StaysSilent() {
			Assert.AreEqual(IntPtr.Zero,AcquireByName());//The operator closed the sensor software without an exposure.
			Assert.AreEqual((int)EZTwainErrorCode.EZTEC_USER_CANCEL,EZTwain.LastErrorCode());
			Assert.AreEqual(0,_harness.Ui.ListNotices.Count);
			Assert.AreEqual("The scan was cancelled.",EZTwain.LastErrorText());
		}

		[TestMethod]
		public void NextImageAfterAFailedSession_NoStaleNotice() {
			Helper.End=FakeHelperEnd.Fail;
			Helper.EndCode=FakeHelperSource.CodeNoSensor;
			Assert.AreEqual(IntPtr.Zero,AcquireByName());
			Helper.End=FakeHelperEnd.Done;
			Helper.ListExposures.Add(SessionHarness.Exposure(8));
			IntPtr hdib=AcquireByName();
			Assert.AreNotEqual(IntPtr.Zero,hdib,EZTwain.LastErrorText());
			EZTwain.DIB_Free(hdib);
			Assert.AreEqual(1,_harness.Ui.ListNotices.Count,"Only the first acquisition had a notice.");
		}

		[TestMethod]
		public void TransferFailure_ByName_ImageNotReceivedText() {
			_harness.Fake.ListPages.Add(FakePage.CreateGray8(8,4,300,(x,y) => (byte)x));
			_harness.Fake.ListPages[0].FailCondition=Tw.TWCC_OPERATIONERROR;
			Assert.IsTrue(EZTwain.OpenSource("Fake Sensor"));
			Assert.AreEqual(IntPtr.Zero,EZTwain.Acquire(IntPtr.Zero));
			Assert.AreEqual((int)EZTwainErrorCode.EZTEC_XFER_FAILURE,EZTwain.LastErrorCode());
			Assert.AreEqual(CaptureNoticeText.ImageNotReceived,EZTwain.LastErrorText(),"The Mount/Acquire loop shows it in its message box.");
			Assert.AreEqual(0,_harness.Ui.ListNotices.Count,"No second message.");
			StringAssert.Contains(System.IO.File.ReadAllText(_harness.PathCaptureLog),"transfer from the scanner failed");
		}

		[TestMethod]
		public void TransferFailure_Scan_KeepsItsText() {
			_harness.Fake.ListPages.Add(FakePage.CreateGray8(8,4,300,(x,y) => (byte)x));
			_harness.Fake.ListPages[0].FailCondition=Tw.TWCC_OPERATIONERROR;
			Assert.IsTrue(EZTwain.OpenDefaultSource());
			Assert.AreEqual(IntPtr.Zero,EZTwain.Acquire(IntPtr.Zero));
			StringAssert.StartsWith(EZTwain.LastErrorText(),"The image transfer from the scanner failed.");
		}

		[TestMethod]
		public void SourceStopsResponding_ByName_NoticeAndText() {
			FakeTwainDsm fake=_harness.Fake;
			fake.ListPages.Add(FakePage.CreateGray8(8,4,300,(x,y) => (byte)x));
			fake.EnableBehavior=FakeEnableBehavior.Nothing;
			using(Form form=new Form { ShowInTaskbar=false }) {
				IntPtr hwndForm=form.Handle;
				fake.ActionOnEnable=() => {
					fake.IsSourceOpen=false;//From now on the source answers every message with TWCC_BADDEST, as a source that is gone.
					for(int i=0;i<60;i++) {
						NativeMethods.PostMessage(hwndForm,NativeMethods.WM_NULL,IntPtr.Zero,IntPtr.Zero);
					}
				};
				Assert.IsTrue(EZTwain.OpenSource("Fake Sensor"));
				Assert.AreEqual(IntPtr.Zero,EZTwain.Acquire(IntPtr.Zero));
			}
			Assert.AreEqual((int)EZTwainErrorCode.EZTEC_DS_FAILURE,EZTwain.LastErrorCode());
			Assert.AreEqual(CaptureNoticeText.ImageNotReceived,EZTwain.LastErrorText());
			CollectionAssert.AreEqual(new List<string> { CaptureNoticeText.ImageNotReceived },_harness.Ui.ListNotices,
				"The Mount/Acquire loop ignores EZTEC_DS_FAILURE, so the library shows it.");
		}
	}
}
