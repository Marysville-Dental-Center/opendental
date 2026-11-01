// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using ImagingDeviceManager.TwainProtocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingDeviceManager.Tests {
	///<summary>DAT_STATUS (design 7.1 "Error notice"): read after every TWRC_FAILURE, and after every MSG_CLOSEDSREQ before
	///MSG_DISABLEDS; for a code other than TWCC_SUCCESS the text comes from DAT_STATUSUTF8 when the source has it.</summary>
	[TestClass]
	public class TwainStatusTests {
		private SessionHarness _harness;

		[TestInitialize]
		public void Setup() {
			_harness=new SessionHarness();
		}

		[TestCleanup]
		public void Cleanup() {
			_harness.Dispose();
		}

		[TestMethod]
		public void CloseRequest_EndReasonReadBeforeDisable_WithText() {
			_harness.Helper.End=FakeHelperEnd.Fail;
			_harness.Helper.EndCode=FakeHelperSource.CodeNoSensor;
			Assert.IsNull(_harness.AcquireByName());
			TwainSession session=_harness.Session;
			Assert.AreEqual(EZTwainErrorCode.EZTEC_USER_CANCEL,session.ErrorCode,"The series ends as on a cancel.");
			Assert.AreEqual(FakeHelperSource.CodeNoSensor,session.EndConditionCode);
			Assert.AreEqual(FakeHelperSource.GetText(FakeHelperSource.CodeNoSensor),session.EndConditionText,"Decoded as UTF-8.");
			StringAssert.Contains(session.ErrorText,session.EndConditionText);
			int idxCloseRequest=_harness.IndexOfCall(x => x.Name=="PROCESSEVENT" && x.Value==Tw.MSG_CLOSEDSREQ);
			int idxStatus=_harness.IndexOfCall(x => x.Name=="STATUS" && x.Condition==FakeHelperSource.CodeNoSensor,idxCloseRequest);
			int idxDisable=_harness.IndexOfCall(x => x.Name=="DISABLEDS");
			Assert.IsTrue(idxCloseRequest>=0 && idxStatus>idxCloseRequest && idxDisable>idxStatus,_harness.CallText);
			Assert.IsTrue(_harness.HasLog("0x8002"),_harness.LogText);
		}

		[TestMethod]
		public void CloseRequest_StatusText_GetsTheWholeStatusBack() {
			_harness.Helper.End=FakeHelperEnd.Fail;
			_harness.Helper.EndCode=FakeHelperSource.CodeSoftwareFailed;
			_harness.Helper.EndStatusData=0x1234;
			Assert.IsNull(_harness.AcquireByName());
			Assert.AreEqual(1,_harness.Fake.CallsNamed("STATUS").Count,"One read after MSG_CLOSEDSREQ; DAT_STATUSUTF8 reuses it.");
			Assert.AreEqual(FakeHelperSource.CodeSoftwareFailed,_harness.Helper.LastStatusTranslated.ConditionCode);
			Assert.AreEqual(0x1234,_harness.Helper.LastStatusTranslated.Data,"TW_STATUSUTF8.Status is the TW_STATUS DAT_STATUS returned.");
		}

		[TestMethod]
		public void CloseRequest_PlainCancel_SilentWithoutText() {
			Assert.IsNull(_harness.AcquireByName());
			TwainSession session=_harness.Session;
			Assert.AreEqual(EZTwainErrorCode.EZTEC_USER_CANCEL,session.ErrorCode);
			Assert.AreEqual(Tw.TWCC_SUCCESS,session.EndConditionCode);
			Assert.IsNull(session.EndConditionText);
			Assert.AreEqual("The scan was cancelled.",session.ErrorText);
			Assert.AreEqual(0,_harness.Fake.CallsNamed("STATUSUTF8").Count,"No text is asked for TWCC_SUCCESS.");
			Assert.AreEqual(1,_harness.Fake.ListCalls.FindAll(x => x.Name=="STATUS").Count,"Exactly the one read after MSG_CLOSEDSREQ.");
		}

		[TestMethod]
		public void CloseRequest_NoStatusText_ReasonIsTheCodeInHex() {
			_harness.Helper.End=FakeHelperEnd.Fail;
			_harness.Helper.EndCode=FakeHelperSource.CodeNoCalibration;
			_harness.Helper.SupportsStatusUtf8=false;
			Assert.IsNull(_harness.AcquireByName());
			Assert.AreEqual(FakeHelperSource.CodeNoCalibration,_harness.Session.EndConditionCode);
			Assert.IsNull(_harness.Session.EndConditionText,"TWCC_BADPROTOCOL from DAT_STATUSUTF8 means no text.");
			StringAssert.Contains(_harness.Session.ErrorText,"0x8001");
		}

		[TestMethod]
		public void CloseRequest_DefaultSource_KeepsTheCancelText() {
			_harness.Fake.DefaultSource=_harness.Helper.ProductName;
			_harness.Helper.End=FakeHelperEnd.Fail;
			_harness.Helper.EndCode=FakeHelperSource.CodeNoSensor;
			Assert.IsTrue(_harness.Session.OpenSource(null));
			Assert.IsNull(_harness.Session.Acquire(IntPtr.Zero));
			Assert.AreEqual("The scan was cancelled.",_harness.Session.ErrorText,"Scans keep their text.");
			Assert.AreEqual(FakeHelperSource.CodeNoSensor,_harness.Session.EndConditionCode,"The code is still read.");
			Assert.AreEqual(0,_harness.Fake.CallsNamed("STATUSUTF8").Count,"The optional DAT_STATUSUTF8 is not sent to a scanner.");
			Assert.IsNull(_harness.Session.EndConditionText);
			Assert.IsFalse(_harness.HasLog("ended the session"),_harness.LogText);
		}

		[TestMethod]
		public void NextEnable_ClearsTheEndReason() {
			_harness.Helper.End=FakeHelperEnd.Fail;
			_harness.Helper.EndCode=FakeHelperSource.CodeNoSensor;
			Assert.IsNull(_harness.AcquireByName());
			_harness.Helper.End=FakeHelperEnd.Done;
			_harness.Helper.ListExposures.Add(SessionHarness.Exposure(9));
			Assert.IsNotNull(_harness.AcquireByName(),_harness.Session.ErrorText);
			Assert.IsNull(_harness.Session.EndConditionCode,"The enable that delivered an image has no end condition.");
			Assert.IsNull(_harness.Session.EndConditionText);
		}

		[TestMethod]
		public void EveryFailure_FollowedByStatusRead_HelperProfile() {
			_harness.Helper.End=FakeHelperEnd.Fail;
			_harness.Helper.EndCode=FakeHelperSource.CodeSoftwareFailed;
			_harness.Helper.SupportsStatusUtf8=false;
			_harness.Session.HideUI=true;
			Assert.IsNull(_harness.AcquireByName());
			_harness.AssertStatusReadAfterEveryFailure();
		}

		[TestMethod]
		public void EveryFailure_FollowedByStatusRead_GenericProfile() {
			FakeTwainDsm fake=_harness.Fake;
			fake.HashSetRejectedCaps.Add(Tw.CAP_XFERCOUNT);
			fake.HashSetRejectedCaps.Add(Tw.ICAP_XFERMECH);
			fake.ListPages.Add(FakePage.CreateRgb(6,4,300,(x,y) => Color.Gray));
			fake.ListPages[0].FailCondition=Tw.TWCC_PAPERJAM;
			Assert.IsTrue(_harness.Session.OpenSource("Fake Sensor"));
			Assert.IsNull(_harness.Session.Acquire(IntPtr.Zero));
			Assert.AreEqual(EZTwainErrorCode.EZTEC_DEVICE_PAPERJAM,_harness.Session.ErrorCode,"The code read right after the failure.");
			_harness.AssertStatusReadAfterEveryFailure();
		}

		[TestMethod]
		public void StatusText_LegacyDsm_HandleFreedWithGlobalFree() {
			RecordingGlobalMemory globalMemory=new RecordingGlobalMemory();
			_harness.Session.GlobalMemory=globalMemory;
			_harness.Helper.End=FakeHelperEnd.Fail;
			Assert.IsNull(_harness.AcquireByName());
			Assert.AreEqual(1,_harness.Helper.CountStatusText);
			Assert.IsNotNull(_harness.Session.EndConditionText);
			foreach(IntPtr handle in _harness.Fake.ListSourceHandles) {
				Assert.IsTrue(globalMemory.ListFreed.Contains(handle));
			}
		}

		[TestMethod]
		public void StatusText_Dsm2_HandleFreedWithDsmMemFree() {
			_harness.Fake.SupplyEntryPoints=true;
			_harness.Helper.End=FakeHelperEnd.Fail;
			Assert.IsNull(_harness.AcquireByName());
			Assert.AreEqual(1,_harness.Helper.CountStatusText);
			Assert.IsNotNull(_harness.Session.EndConditionText);
			Assert.AreEqual(0,_harness.Fake.DsmMemory.CountOutstanding,"The text handle and every container went back through DSM_MemFree.");
			Assert.AreEqual(0,_harness.Fake.DsmMemory.CountForeignFree);
		}
	}
}
