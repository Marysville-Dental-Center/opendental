// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ImagingDeviceManager.Acquire;
using ImagingDeviceManager.Imaging;
using ImagingDeviceManager.Journal;
using ImagingDeviceManager.TwainProtocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingDeviceManager.Tests {
	///<summary>The quarantine writer, its sidecar and the quarantine texts, without a source (design 7.1 "Quarantine in phases 0-2").</summary>
	[TestClass]
	public class QuarantineWriterTests {
		private string _folderImaging;
		private CaptureFolders _folders;

		[TestInitialize]
		public void Setup() {
			_folderImaging=Path.Combine(Path.GetTempPath(),"ImagingDeviceManagerTests_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_folderImaging);
			_folders=CaptureFolders.CreateForFolder(_folderImaging);
		}

		[TestCleanup]
		public void Cleanup() {
			try {
				Directory.Delete(_folderImaging,true);
			}
			catch {
			}
		}

		private static DibImage Gray8() {
			return DibImage.Create(4,3,8,DibImage.CreateGrayPalette(256,false),new byte[12],0,0);
		}

		[TestMethod]
		public void Write_PatientUnknownSidecar() {
			CaptureRecord record=CaptureRecord.Create(new DateTime(2026,10,25,14,30,12),"Sensor X\0junk","abc");
			string error;
			string path=new QuarantineWriter(_folders).Write(Gray8(),record,TwainDrainReason.Stop,TwainStopReason.ParentDestroyed,null,out error);
			Assert.IsNotNull(path,error);
			Assert.AreEqual(Path.Combine(_folderImaging,@"Capture\quarantine"),Path.GetDirectoryName(path));
			Dictionary<string,string> dictFields=CaptureFiles.ReadSidecar(FacadeHarness.SidecarOf(path));
			Assert.AreEqual("quarantine",dictFields["Kind"]);
			StringAssert.StartsWith(dictFields["ReceivedAt"],"2026-10-25T14:30:12.000");
			Assert.AreEqual("Sensor X",dictFields["Source"],"Cut at the NUL.");
			Assert.AreEqual(Environment.MachineName,dictFields["Computer"]);
			Assert.AreNotEqual("",dictFields["WindowsUser"]);
			Assert.AreEqual("patient unknown",dictFields["Patient"]);
			Assert.AreEqual("",dictFields["PatNum"]);
			StringAssert.Contains(dictFields["Reason"],"automatic logoff");
			Assert.AreEqual("Stop",dictFields["DrainReason"]);
			Assert.AreEqual("ParentDestroyed",dictFields["StopReason"]);
			Assert.AreEqual("abc",dictFields["AcquireId"]);
			Assert.IsFalse(dictFields.ContainsKey("CaptureId"));
			Assert.IsFalse(dictFields.ContainsKey("Sequence"));
		}

		[TestMethod]
		public void Write_MetadataWhenKnown() {
			CaptureRecord record=CaptureRecord.Create(DateTime.Now,"S","id");
			record.CaptureId="cap-7";
			record.Sequence=3;
			string error;
			string path=new QuarantineWriter(_folders).Write(Gray8(),record,TwainDrainReason.ExtraImage,TwainStopReason.None,null,out error);
			Dictionary<string,string> dictFields=CaptureFiles.ReadSidecar(FacadeHarness.SidecarOf(path));
			Assert.AreEqual("cap-7",dictFields["CaptureId"]);
			Assert.AreEqual("3",dictFields["Sequence"]);
		}

		[TestMethod]
		public void Write_FolderUnusable_Error() {
			Directory.CreateDirectory(Path.Combine(_folderImaging,"Capture"));
			File.WriteAllText(Path.Combine(_folderImaging,@"Capture\quarantine"),"a file where the folder should be");
			string error;
			Assert.IsNull(new QuarantineWriter(_folders).Write(Gray8(),CaptureRecord.Create(DateTime.Now,"S","id"),TwainDrainReason.Stop,
				TwainStopReason.Cancel,null,out error));
			StringAssert.Contains(error,"not usable");
		}

		[TestMethod]
		public void DescribeReason_EveryCase() {
			StringAssert.Contains(QuarantineWriter.DescribeReason(TwainDrainReason.ExtraImage,TwainStopReason.None),"CAP_XFERCOUNT=1");
			StringAssert.Contains(QuarantineWriter.DescribeReason(TwainDrainReason.RefusedDisable,TwainStopReason.Cancel),"refused to stop");
			foreach(TwainStopReason stopReason in Enum.GetValues(typeof(TwainStopReason))) {
				string text=QuarantineWriter.DescribeReason(TwainDrainReason.Stop,stopReason);
				StringAssert.StartsWith(text,"It was waiting to be transferred when the series stopped: ");
			}
			StringAssert.Contains(QuarantineWriter.DescribeReason(TwainDrainReason.Stop,TwainStopReason.Timeout),"AcquireTimeoutMinutes");
		}

		[TestMethod]
		public void CountImages_AllImagesBelowTheFolder_NeverCreatesIt() {
			DateTime? oldest;
			Assert.AreEqual(0,QuarantineWriter.CountImages(_folders,out oldest));
			Assert.IsNull(oldest);
			Assert.IsFalse(Directory.Exists(Path.Combine(_folderImaging,@"Capture\quarantine")),"Counting creates nothing.");
			string folder=Path.Combine(_folderImaging,@"Capture\quarantine");
			Directory.CreateDirectory(Path.Combine(folder,"session-1"));
			File.WriteAllBytes(Path.Combine(folder,"a.bmp"),new byte[] { 1 });
			File.WriteAllBytes(Path.Combine(folder,@"session-1\b.png"),new byte[] { 1 });
			File.WriteAllText(Path.Combine(folder,"a.txt"),"sidecar");
			File.SetLastWriteTime(Path.Combine(folder,"a.bmp"),new DateTime(2026,1,2));
			Assert.AreEqual(2,QuarantineWriter.CountImages(_folders,out oldest));
			Assert.AreEqual(new DateTime(2026,1,2),oldest);
		}

		[TestMethod]
		public void NoticeTexts() {
			DateTime time1=new DateTime(2026,10,25,14,30,12);
			Assert.AreEqual("1 image(s) received after the series stopped were saved to the capture quarantine folder (2026-10-25 14:30:12)."
				+" Do not re-expose. Ask an administrator to recover them (see the runbook).",CaptureNoticeText.Quarantined(1,time1,time1));
			StringAssert.Contains(CaptureNoticeText.Quarantined(2,time1,time1.AddSeconds(9)),"(2026-10-25 14:30:12 to 2026-10-25 14:30:21)");
			string textFailed=CaptureNoticeText.QuarantineFailed(1,time1,time1,"disk full.",true);
			StringAssert.Contains(textFailed,"could not be saved to the capture quarantine folder (2026-10-25 14:30:12): disk full.");
			StringAssert.Contains(textFailed,"A copy is in the capture journal folder.");
			StringAssert.Contains(textFailed,"Do not re-expose.");
			Assert.IsFalse(CaptureNoticeText.QuarantineFailed(1,time1,time1,"x",false).Contains("journal"));
			Assert.AreEqual("The sensor software may still hold images from this series. Do not re-expose. See the runbook.",
				CaptureNoticeText.MayStillHoldImages);
		}

		[TestMethod]
		public void UnassignedText_CountThenWarningAfter30Days() {
			DateTime now=new DateTime(2026,10,25);
			Assert.IsNull(CaptureNoticeText.UnassignedCaptures(0,null,now));
			Assert.AreEqual("3 unassigned capture(s) on this computer.",CaptureNoticeText.UnassignedCaptures(3,now.AddDays(-30),now));
			string textOld=CaptureNoticeText.UnassignedCaptures(3,now.AddDays(-31),now);
			StringAssert.StartsWith(textOld,"3 unassigned capture(s) on this computer. The oldest is from 2026-09-24.");
			StringAssert.Contains(textOld,"recover them now");
		}

		[TestMethod]
		public void Select_QuarantineThenFailuresThenMayStillHold_InOneList() {
			DateTime time1=new DateTime(2026,10,25,14,30,12);
			CaptureCallState call=new CaptureCallState();
			Assert.AreEqual(0,AcquireNotices.Select(call).Count,"Silent when nothing happened.");
			call.ListQuarantined.Add(new QuarantinedImage { ReceivedAt=time1,PathImage="a.bmp" });
			call.ListQuarantined.Add(new QuarantinedImage { ReceivedAt=time1.AddSeconds(1),PathImage="b.bmp" });
			call.ListQuarantined.Add(new QuarantinedImage { ReceivedAt=time1.AddSeconds(2),Error="no disk",PathJournal="j.bmp" });
			call.IsMayStillHoldImages=true;
			List<string> listNotices=AcquireNotices.Select(call);
			Assert.AreEqual(3,listNotices.Count);
			Assert.AreEqual(CaptureNoticeText.Quarantined(2,time1,time1.AddSeconds(1)),listNotices[0]);
			StringAssert.StartsWith(listNotices[1],"1 image(s) received after the series stopped could not be saved");
			StringAssert.Contains(listNotices[1],"journal");
			Assert.AreEqual(CaptureNoticeText.MayStillHoldImages,listNotices[2]);
		}
	}

	///<summary>The facade's drain sink: images received after the caller could no longer take them go to the quarantine with a "patient
	///unknown" sidecar and to the journal, and the operator gets one non-modal notice.</summary>
	[TestClass]
	public class QuarantineTests {
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

		private IntPtr AcquireByName(IntPtr hwnd) {
			Assert.IsTrue(EZTwain.OpenSource(Helper.ProductName),EZTwain.LastErrorText());
			return EZTwain.Acquire(hwnd);
		}

		///<summary>The operator took an image, the helper is still reading it out, and Cancel is clicked (safe disable in effect).</summary>
		private void CancelWhileReadingOut() {
			_harness.WriteSafeDisableConfig();
			Helper.ListExposures.Add(SessionHarness.Exposure(8));
			Helper.EndAfterMs=60000;
			Helper.ActionOnEnable=() => _harness.Ui.StatusLast.Controller.ClickCancel();
		}

		[TestMethod]
		public void CancelWithSafeDisable_ImageQuarantined_JournalCopy_OneNotice() {
			CancelWhileReadingOut();
			Assert.AreEqual(IntPtr.Zero,AcquireByName(IntPtr.Zero));
			List<string> listQuarantine=FacadeHarness.ListImages(_harness.FolderQuarantine);
			Assert.AreEqual(1,listQuarantine.Count);
			Dictionary<string,string> dictFields=CaptureFiles.ReadSidecar(FacadeHarness.SidecarOf(listQuarantine[0]));
			Assert.AreEqual("patient unknown",dictFields["Patient"]);
			Assert.AreEqual(Helper.ProductName,dictFields["Source"]);
			Assert.AreEqual("RefusedDisable",dictFields["DrainReason"]);
			Assert.AreEqual("Cancel",dictFields["StopReason"]);
			List<string> listJournal=FacadeHarness.ListImages(_harness.FolderJournal);
			Assert.AreEqual(1,listJournal.Count);
			Dictionary<string,string> dictJournal=CaptureFiles.ReadSidecar(FacadeHarness.SidecarOf(listJournal[0]));
			Assert.AreEqual(CaptureJournal.DeliveredQuarantine,dictJournal["Delivered"]);
			Assert.AreEqual(Path.GetFileName(listQuarantine[0]),dictJournal["DeliveredDetail"]);
			Assert.AreEqual(dictFields["AcquireId"],dictJournal["AcquireId"]);
			CollectionAssert.AreEqual(File.ReadAllBytes(listQuarantine[0]),File.ReadAllBytes(listJournal[0]));
			Assert.AreEqual(1,_harness.Ui.ListNotices.Count);
			string notice=_harness.Ui.ListNotices[0];
			StringAssert.StartsWith(notice,"1 image(s) received after the series stopped were saved to the capture quarantine folder (");
			string receivedAt=dictFields["ReceivedAt"];
			StringAssert.Contains(notice,receivedAt.Substring(0,10)+" "+receivedAt.Substring(11,8),"The receive time.");
			StringAssert.EndsWith(notice,"Do not re-expose. Ask an administrator to recover them (see the runbook).");
			Assert.AreEqual(1,Helper.CountDelivered,"Nothing is lost.");
			StringAssert.Contains(File.ReadAllText(_harness.PathCaptureLog),"Quarantine image "+Path.GetFileName(listQuarantine[0]));
		}

		[TestMethod]
		public void ExtraSingleModeImages_Quarantined_CallerGetsTheFirst() {
			Helper.Mode=FakeHelperMode.Multi;//An installation setting turns CAP_XFERCOUNT=1 into a batch.
			Helper.ListExposures.Add(SessionHarness.Exposure(8));
			Helper.ListExposures.Add(SessionHarness.Exposure(9));
			Helper.ListExposures.Add(SessionHarness.Exposure(10));
			IntPtr hdib=AcquireByName(IntPtr.Zero);
			Assert.AreNotEqual(IntPtr.Zero,hdib,EZTwain.LastErrorText());
			Assert.AreEqual(8,EZTwain.DIB_Width(hdib));
			EZTwain.DIB_Free(hdib);
			List<string> listQuarantine=FacadeHarness.ListImages(_harness.FolderQuarantine);
			Assert.AreEqual(2,listQuarantine.Count);
			Assert.IsTrue(listQuarantine.All(x => CaptureFiles.ReadSidecar(FacadeHarness.SidecarOf(x))["DrainReason"]=="ExtraImage"));
			Assert.AreEqual(3,FacadeHarness.ListImages(_harness.FolderJournal).Count,"Every transferred image has a journal copy.");
			Assert.AreEqual(1,_harness.Ui.ListNotices.Count);
			StringAssert.StartsWith(_harness.Ui.ListNotices[0],"2 image(s) received after the series stopped");
		}

		[TestMethod]
		public void ParentDestroyed_ImageQuarantined() {
			using(Form form=new Form { ShowInTaskbar=false }) {
				IntPtr hwnd=form.Handle;
				Helper.ListExposures.Add(SessionHarness.Exposure(8));
				Helper.ActionOnEnable=() => form.Dispose();//An automatic logoff closes the hidden parent form.
				Assert.AreEqual(IntPtr.Zero,AcquireByName(hwnd));
			}
			List<string> listQuarantine=FacadeHarness.ListImages(_harness.FolderQuarantine);
			Assert.AreEqual(1,listQuarantine.Count);
			Assert.AreEqual("ParentDestroyed",CaptureFiles.ReadSidecar(FacadeHarness.SidecarOf(listQuarantine[0]))["StopReason"]);
			Assert.AreEqual(1,_harness.Ui.ListNotices.Count);
		}

		[TestMethod]
		public void QuarantineUnusable_JournalCopyIsTheOnlyOne_NoticeSaysSo() {
			Directory.CreateDirectory(Path.Combine(_harness.FolderImaging,"Capture"));
			File.WriteAllText(_harness.FolderQuarantine,"a file where the folder should be");
			CancelWhileReadingOut();
			Assert.AreEqual(IntPtr.Zero,AcquireByName(IntPtr.Zero));
			List<string> listJournal=FacadeHarness.ListImages(_harness.FolderJournal);
			Assert.AreEqual(1,listJournal.Count);
			Assert.AreEqual(CaptureJournal.DeliveredNone,CaptureFiles.ReadSidecar(FacadeHarness.SidecarOf(listJournal[0]))["Delivered"]);
			Assert.AreEqual(1,_harness.Ui.ListNotices.Count);
			StringAssert.Contains(_harness.Ui.ListNotices[0],"could not be saved to the capture quarantine folder");
			StringAssert.Contains(_harness.Ui.ListNotices[0],"A copy is in the capture journal folder.");
		}

		[TestMethod]
		public void MayStillHoldImages_Notice() {
			_harness.Host.AdjustSettings=settings => settings.RefusedDisableWait=TimeSpan.FromMilliseconds(200);
			CancelWhileReadingOut();
			Helper.IgnoresCloseRequest=true;
			Helper.ForcedEndMs=30000;
			Assert.AreEqual(IntPtr.Zero,AcquireByName(IntPtr.Zero));
			Assert.IsTrue(EZTwain.Session.SourceMayHoldImages);
			CollectionAssert.AreEqual(new List<string> { CaptureNoticeText.MayStillHoldImages },_harness.Ui.ListNotices);
		}

		[TestMethod]
		public void PlainAcquire_NoNotice() {
			Helper.ListExposures.Add(SessionHarness.Exposure(8));
			EZTwain.DIB_Free(AcquireByName(IntPtr.Zero));
			Assert.AreEqual(0,_harness.Ui.ListNotices.Count);
			Assert.AreEqual(0,FacadeHarness.ListImages(_harness.FolderQuarantine).Count);
		}

		[TestMethod]
		public void StatusWindow_ShowsTheUnassignedCount() {
			Directory.CreateDirectory(_harness.FolderQuarantine);
			File.WriteAllBytes(Path.Combine(_harness.FolderQuarantine,"left over.bmp"),new byte[] { 1 });
			Helper.ListExposures.Add(SessionHarness.Exposure(8));
			EZTwain.DIB_Free(AcquireByName(IntPtr.Zero));
			Assert.AreEqual("1 unassigned capture(s) on this computer.",_harness.Ui.StatusLast.Controller.UnassignedText);
		}
	}
}
