// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using ImagingDeviceManager.Capture;
using ImagingDeviceManager.Journal;
using ImagingDeviceManager.TwainProtocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingDeviceManager.Tests {
	///<summary>TwainCaptureSession (design 7.1 CAP-3, 7.3, 7.4, 7.6, 7.12) against the contract 1.1 profile, with the published numbers
	///entered as device-profile data.</summary>
	[TestClass]
	public class TwainCaptureSessionTests {
		private CaptureSessionHarness _harness;

		[TestInitialize]
		public void Setup() {
			_harness=new CaptureSessionHarness();
			Helper.ListExposures.Add(SessionHarness.Exposure(8));
		}

		[TestCleanup]
		public void Cleanup() {
			_harness.Dispose();
		}

		private FakeHelperSource Helper => _harness.Helper;
		private TwainCaptureSession Capture => _harness.Capture;
		private TwainCaptureOptions Options => _harness.Options;
		private List<FakeCall> Calls => _harness.Fake.ListCalls;

		///<summary>The published W13 numbers as [Device.N] data.</summary>
		private void ConfigureSafeDisable() {
			Options.DisableIsSafe=true;
			Options.SafeDisableCap=FakeHelperSource.CapSafeDisable;
			Options.RefusedDisableCodes=new List<ushort> { FakeHelperSource.CodeDisableImagesPending,FakeHelperSource.CodeDisableClosing };
		}

		private int EnableOne(int bits=8,object tag=null) {
			return Capture.Enable(new TwainEnableRequest { Bits=bits,Tag=tag,TargetLabel="Slot 1" });
		}

		#region Open
		[TestMethod]
		public void Open_State4_SourceInfoRead_FixedCapabilitiesNeverSet() {
			Options.SafeDisableCap=FakeHelperSource.CapSafeDisable;
			Options.ContinuousCap=FakeHelperSource.CapContinuous;
			TwainCaptureOpenResult result=_harness.Open();
			Assert.IsTrue(result.IsOpen,result.ErrorText);
			Assert.AreEqual(TwainCaptureState.Open,Capture.State);
			Assert.AreEqual(Helper.ProductName,Capture.ProductName);
			Assert.AreEqual(4,Helper.State);
			TwainCaptureSourceInfo info=result.SourceInfo;
			Assert.AreSame(info,Capture.SourceInfo);
			Assert.AreEqual(Helper.ProductName,info.ProductName);
			Assert.AreEqual("FakeTwainDsm",info.DsmName);
			Assert.AreEqual(false,info.UiControllable);
			Assert.AreEqual(true,info.DeviceOnline);
			Assert.AreEqual(true,info.Indicators);
			Assert.AreEqual(true,info.UndefinedImageSize);
			Assert.AreEqual(Tw.TWPT_GRAY,info.PixelType);
			Assert.AreEqual(Tw.TWPF_CHOCOLATE,info.PixelFlavor);
			Assert.AreEqual(0,info.BitOrder);
			Assert.AreEqual(0,info.PlanarChunky);
			Assert.AreEqual(0,info.SupportedSizes);
			CollectionAssert.AreEqual(new List<int> { 8,16 },info.ListBitDepths);
			Assert.IsTrue(info.IsExtImageInfoSettable);
			Assert.AreEqual(7,info.ListSupportedExtImageInfo.Count);
			Assert.IsTrue(info.IsMetadataListed);
			Assert.AreEqual(2,info.ListCustomCapabilities.Count);
			Assert.IsTrue(info.ListCustomCapabilities[0].IsSettable,"SafeDisableCap: 0x1F.");
			Assert.IsFalse(info.ListCustomCapabilities[1].IsSettable,"ContinuousCap: reserved in 1.1.");
			Assert.AreEqual(Tw.TWCC_CAPUNSUPPORTED,info.ListCustomCapabilities[1].ConditionCode);
			_harness.Close();
			_harness.AssertNoFixedCapabilitySet();
		}

		[TestMethod]
		public void Open_SourceNotFound() {
			TwainCaptureOpenResult result=Capture.Open("No Such Source",Options);
			Assert.IsFalse(result.IsOpen);
			Assert.AreEqual(TwainCaptureOpenError.SourceNotFound,result.Error);
			Assert.AreEqual(TwainCaptureState.Closed,Capture.State);
			Assert.AreEqual(0,Capture.Enable(new TwainEnableRequest()),"Nothing to enable.");
		}

		[TestMethod]
		public void Open_SourceGate_Refuses() {
			Options.RestrictSources=true;
			Options.AllowedSources=new List<string> { "Other" };
			TwainCaptureOpenResult result=_harness.Open();
			Assert.AreEqual(TwainCaptureOpenError.SourceNotEnabledHere,result.Error);
			Assert.AreEqual(TwainSourceGate.RefusedText,result.ErrorText);
			Assert.IsFalse(_harness.Fake.ListLog.Any(x => x.StartsWith("OPENDS ")));
		}

		[TestMethod]
		public void Open_NoDsm() {
			TwainCaptureSession capture=new TwainCaptureSession((out string errorText) => {
				errorText="No TWAIN here.";
				return null;
			},_harness.Folders,() => DateTime.Now);
			TwainCaptureOpenResult result=capture.Open("X",Options);
			Assert.AreEqual(TwainCaptureOpenError.NoDsm,result.Error);
			Assert.AreEqual("No TWAIN here.",result.ErrorText);
		}

		[TestMethod]
		public void Open_Twice_AlreadyOpen_ThenReopenAfterClose() {
			_harness.OpenOrFail();
			Assert.AreEqual(TwainCaptureOpenError.AlreadyOpen,_harness.Open().Error);
			_harness.Close();
			_harness.OpenOrFail();
			Assert.AreEqual(TwainCaptureState.Open,Capture.State);
		}
		#endregion Open

		#region PerSlot
		[TestMethod]
		public void PerSlot_OneImagePerEnable_JournalFirst_DisabledAfterCountZero_SourceStaysOpen() {
			_harness.OpenOrFail();
			object tag=new object();
			int enableId=EnableOne(8,tag);
			TwainCaptureEnded ended=_harness.WaitForEnded(enableId);
			Assert.AreEqual(TwainCaptureEndReason.Completed,ended.Reason,ended.ErrorText);
			Assert.AreSame(tag,ended.Tag);
			Assert.AreEqual(1,ended.ImageCount);
			AcquiredImage image=_harness.Images.Single();
			Assert.AreEqual(enableId,image.EnableId);
			Assert.AreSame(tag,image.Tag);
			Assert.AreEqual(AcquiredImageKind.Normal,image.Kind);
			Assert.AreEqual(TwainCaptureStopReason.None,image.StopReason);
			Assert.AreEqual(1,image.IndexInEnable);
			Assert.IsNotNull(image.Dib);
			Assert.IsNotNull(image.MetadataJson);
			Assert.IsTrue(_harness.ListJournalExistedAtEvent.Single(),"The journal copy is written before ImageAcquired.");
			Assert.AreEqual(4,Helper.State,"The source stays open in state 4.");
			int enableId2=EnableOne();
			Assert.AreEqual(TwainCaptureEndReason.Completed,_harness.WaitForEnded(enableId2).Reason);
			Assert.AreEqual(2,Helper.CountEnable);
			Assert.AreEqual(2,_harness.Images.Count);
			Assert.AreEqual(1,_harness.Fake.ListLog.Count(x => x.StartsWith("OPENDS ")),"One MSG_OPENDS across slots.");
			Assert.IsFalse(_harness.Fake.ListLog.Contains("CLOSEDS"));
			Assert.AreEqual(2,Calls.Count(x => x.Name=="CAP/SET" && x.Cap==Tw.CAP_XFERCOUNT && x.Value==1),"CAP_XFERCOUNT=1 before each enable.");
			_harness.Close();
			Assert.IsTrue(_harness.Fake.ListLog.Contains("CLOSEDS"));
			_harness.AssertDisabledOnlyInState5();
			_harness.AssertNoFixedCapabilitySet();
		}

		[TestMethod]
		public void Events_RaisedOnTheStaWorker() {
			ApartmentState apartment=ApartmentState.Unknown;
			_harness.ActionOnImage=image => apartment=Thread.CurrentThread.GetApartmentState();
			_harness.OpenOrFail();
			_harness.WaitForEnded(EnableOne());
			_harness.Close();
			Assert.AreEqual(1,_harness.HashSetEventThreads.Count,"Every event on one thread.");
			Assert.AreNotEqual(Thread.CurrentThread.ManagedThreadId,_harness.HashSetEventThreads.Single());
			Assert.AreEqual(ApartmentState.STA,apartment);
		}

		[TestMethod]
		public void Statuses_FollowTheEnable() {
			_harness.OpenOrFail();
			_harness.WaitForEnded(EnableOne());
			_harness.WaitUntil(() => _harness.ListStatus.Last().State==TwainCaptureState.Open,"back to Open");
			List<TwainCaptureState> listStates=_harness.States;
			int idxEnabling=listStates.IndexOf(TwainCaptureState.Enabling);
			int idxWaiting=listStates.IndexOf(TwainCaptureState.WaitingForImage);
			int idxTransferring=listStates.IndexOf(TwainCaptureState.Transferring);
			Assert.IsTrue(idxEnabling>=0 && idxWaiting>idxEnabling && idxTransferring>idxWaiting,string.Join(",",listStates));
			Assert.AreEqual("Sensor software open, waiting for exposure",TwainCaptureSession.GetStateText(TwainCaptureState.WaitingForImage));
		}

		[TestMethod]
		public void PerSlot_ExtraImages_FlaggedExtra() {
			Helper.Mode=FakeHelperMode.Multi;//A source setting that overrides CAP_XFERCOUNT=1.
			Helper.ListExposures.Add(SessionHarness.Exposure(9));
			Helper.EndAfterMs=0;
			_harness.OpenOrFail();
			TwainCaptureEnded ended=_harness.WaitForEnded(EnableOne());
			Assert.AreEqual(TwainCaptureEndReason.Completed,ended.Reason);
			Assert.AreEqual(2,ended.ImageCount);
			Assert.AreEqual(1,ended.ExtraImageCount);
			CollectionAssert.AreEqual(new[] { AcquiredImageKind.Normal,AcquiredImageKind.Extra },_harness.Images.Select(x => x.Kind).ToList());
			Assert.IsTrue(_harness.HasLog("more images than the one asked for"),_harness.LogText);
		}

		[TestMethod]
		public void PerSlot_ImageOfferedAfterTheRefusedDisableAfterCountZero_FlaggedExtra() {
			ConfigureSafeDisable();
			Helper.LateExposure=SessionHarness.Exposure(9);//Read out while the disable after Count=0 is refused, then offered.
			_harness.OpenOrFail();
			TwainCaptureEnded ended=_harness.WaitForEnded(EnableOne());
			Assert.AreEqual(TwainCaptureEndReason.Completed,ended.Reason,ended.ErrorText);
			Assert.IsTrue(ended.WasDisableRefused,_harness.LogText);
			Assert.AreEqual(2,ended.ImageCount);
			Assert.AreEqual(1,ended.ExtraImageCount);
			Assert.AreEqual(0,ended.DrainedImageCount,"Drained is for the images of a stop the session started.");
			CollectionAssert.AreEqual(new[] { AcquiredImageKind.Normal,AcquiredImageKind.Extra },_harness.Images.Select(x => x.Kind).ToList(),
				"The slot already has its image: the second goes to the quarantine with a notice, not into the slot (design 7.12).");
			Assert.IsTrue(_harness.Images.All(x => x.StopReason==TwainCaptureStopReason.None));
			Assert.IsTrue(_harness.HasLog("after the disable was refused"),_harness.LogText);
			Assert.AreEqual(2,Helper.CountDelivered,"Nothing lost.");
			_harness.AssertDisabledOnlyInState5();
		}

		[TestMethod]
		public void Bits16_Gray16Raised_Then8BitEnable_SetsBitDepth8BeforeEnable() {
			Helper.ListExposures.Clear();
			FakePage page=TwainCapturePathTests.ExposureWith16(6,4);
			Helper.ListExposures.Add(page);
			_harness.OpenOrFail();
			TwainCaptureEnded ended16=_harness.WaitForEnded(EnableOne(16));
			Assert.IsTrue(ended16.IsBitDepth16InEffect);
			AcquiredImage image16=_harness.Images.Single();
			Assert.IsTrue(image16.IsGray16);
			Assert.AreEqual(TwainCapturePathTests.Sample16(5,3),image16.Gray16[3*6+5]);
			Assert.AreEqual(".tif",Path.GetExtension(image16.JournalPath));
			int countCalls=Calls.Count;
			TwainCaptureEnded ended8=_harness.WaitForEnded(EnableOne(8));
			Assert.IsFalse(ended8.IsBitDepth16InEffect);
			AcquiredImage image8=_harness.Images[1];
			Assert.IsFalse(image8.IsGray16);
			Assert.AreEqual(8,image8.BitsPerPixel);
			Assert.AreEqual(".bmp",Path.GetExtension(image8.JournalPath));
			int idxDepth8=_harness.IndexOfCall(x => x.Name=="CAP/SET" && x.Cap==Tw.ICAP_BITDEPTH && x.Value==8,countCalls);
			int idxEnable=_harness.IndexOfCall(x => x.Name=="ENABLEDS",countCalls);
			Assert.IsTrue(idxDepth8>=0 && idxEnable>idxDepth8,_harness.CallText);
			Assert.AreEqual(4,Calls[idxDepth8].StateBefore);
		}

		[TestMethod]
		public void Bits16_ImageWithout16BitOriginal_FlaggedAndJournaledAsBmp() {
			_harness.OpenOrFail();
			TwainCaptureEnded ended=_harness.WaitForEnded(EnableOne(16));
			Assert.IsTrue(ended.IsBitDepth16InEffect);
			AcquiredImage image=_harness.Images.Single();
			Assert.IsTrue(image.IsDeliveredAs8BitIn16BitEnable);
			Assert.AreEqual(".bmp",Path.GetExtension(image.JournalPath));
			Dictionary<string,string> dictSidecar=CaptureFiles.ReadSidecar(Path.ChangeExtension(image.JournalPath,".txt"));
			Assert.AreEqual("1",dictSidecar["DeliveredAs8BitIn16BitEnable"]);
		}

		[TestMethod]
		public void JournalSidecar_RecordsSessionEnableMetadataAndDeviceProfile() {
			Capture.QuarantineContext=new TwainCaptureQuarantineContext(77,"Doe, Jane");
			Capture.FuncReadMetadataKeys=json => new CaptureMetadataKeys { CaptureId="cid-from-parser",Sequence=5 };
			_harness.OpenOrFail();
			_harness.WaitForEnded(EnableOne());
			AcquiredImage image=_harness.Images.Single();
			Dictionary<string,string> dictSidecar=CaptureFiles.ReadSidecar(Path.ChangeExtension(image.JournalPath,".txt"));
			Assert.AreEqual("journal copy",dictSidecar["Kind"]);
			Assert.AreEqual(CaptureJournal.DeliveredCaller,dictSidecar["Delivered"]);
			Assert.AreEqual("77",dictSidecar["PatNum"]);
			Assert.AreEqual(Capture.SessionGuid.ToString("D"),dictSidecar["SessionGuid"]);
			Assert.AreEqual(image.EnableId.ToString(),dictSidecar["EnableId"]);
			Assert.AreEqual("Slot 1",dictSidecar["Target"]);
			Assert.AreEqual("cid-from-parser",dictSidecar["CaptureId"]);
			Assert.AreEqual("5",dictSidecar["Sequence"]);
			StringAssert.Contains(dictSidecar["Metadata"],"capture-metadata/1");
			Assert.AreEqual("PerSlot",dictSidecar["Device.12.SessionMode"]);
		}

		[TestMethod]
		public void CheckStatusOnEnable_EnableGoesOn() {
			Helper.EnableReturnsCheckStatus=true;
			_harness.OpenOrFail();
			Assert.AreEqual(TwainCaptureEndReason.Completed,_harness.WaitForEnded(EnableOne()).Reason);
			Assert.AreEqual(1,_harness.Images.Count);
			Assert.IsTrue(_harness.HasLog("TWRC_CHECKSTATUS"),_harness.LogText);
		}

		[TestMethod]
		public void Headless_ButNotUiControllable_ShowUI1() {
			Options.Headless=true;
			_harness.OpenOrFail();
			_harness.WaitForEnded(EnableOne());
			Assert.AreEqual(1,Helper.LastShowUI);
			Assert.IsTrue(_harness.HasLog("CAP_UICONTROLLABLE FALSE"),_harness.LogText);
		}

		[TestMethod]
		public void NoActivateCap_SetTrueBeforeEachEnable() {
			Options.NoActivateCap=FakeHelperSource.CapNoActivate;
			_harness.OpenOrFail();
			_harness.WaitForEnded(EnableOne());
			_harness.WaitForEnded(EnableOne());
			List<int> listSets=Enumerable.Range(0,Calls.Count).Where(i => Calls[i].Name=="CAP/SET" && Calls[i].Cap==FakeHelperSource.CapNoActivate).ToList();
			List<int> listEnables=Enumerable.Range(0,Calls.Count).Where(i => Calls[i].Name=="ENABLEDS").ToList();
			Assert.AreEqual(2,listSets.Count);
			Assert.IsTrue(listSets[0]<listEnables[0] && listSets[1]>listEnables[0] && listSets[1]<listEnables[1]);
			Assert.AreEqual(1,Helper.DictCaps[FakeHelperSource.CapNoActivate]);
		}
		#endregion PerSlot

		#region Ends without an image
		[TestMethod]
		public void CloseRequestWithEndCode_StatusReadBeforeDisable_CodeAndTextInEnded() {
			Helper.ListExposures.Clear();
			Helper.End=FakeHelperEnd.Fail;
			Helper.EndCode=FakeHelperSource.CodeNoSensor;
			_harness.OpenOrFail();
			TwainCaptureEnded ended=_harness.WaitForEnded(EnableOne());
			Assert.AreEqual(TwainCaptureEndReason.CloseRequested,ended.Reason);
			Assert.AreEqual(FakeHelperSource.CodeNoSensor,ended.ConditionCode);
			Assert.AreEqual(FakeHelperSource.GetText(FakeHelperSource.CodeNoSensor),ended.ConditionText);
			int idxClose=_harness.IndexOfCall(x => x.Name=="PROCESSEVENT" && x.Value==Tw.MSG_CLOSEDSREQ);
			int idxStatus=_harness.IndexOfCall(x => x.Name=="STATUS",idxClose);
			int idxText=_harness.IndexOfCall(x => x.Name=="STATUSUTF8",idxStatus);
			int idxDisable=_harness.IndexOfCall(x => x.Name=="DISABLEDS",idxClose);
			Assert.IsTrue(idxClose>=0 && idxStatus>idxClose && idxText>idxStatus && idxDisable>idxText,_harness.CallText);
			Assert.AreEqual(0,_harness.Images.Count);
		}

		[TestMethod]
		public void CloseRequestWithEndCode_NoStatusUtf8_CodeAlone() {
			Helper.ListExposures.Clear();
			Helper.End=FakeHelperEnd.Fail;
			Helper.EndCode=FakeHelperSource.CodeNoCalibration;
			Helper.SupportsStatusUtf8=false;
			_harness.OpenOrFail();
			TwainCaptureEnded ended=_harness.WaitForEnded(EnableOne());
			Assert.AreEqual(FakeHelperSource.CodeNoCalibration,ended.ConditionCode);
			Assert.IsNull(ended.ConditionText);
		}

		[TestMethod]
		public void CloseRequestPlainCancel_Success() {
			Helper.ListExposures.Clear();
			_harness.OpenOrFail();
			TwainCaptureEnded ended=_harness.WaitForEnded(EnableOne());
			Assert.AreEqual(TwainCaptureEndReason.CloseRequested,ended.Reason);
			Assert.AreEqual(Tw.TWCC_SUCCESS,ended.ConditionCode);
			Assert.IsNull(ended.ConditionText);
		}

		[TestMethod]
		public void EnableFails_OperationError_SourceStaysInState4_NextEnableWorks() {
			Helper.EnableFails=true;
			_harness.OpenOrFail();
			TwainCaptureEnded ended=_harness.WaitForEnded(EnableOne());
			Assert.AreEqual(TwainCaptureEndReason.EnableFailed,ended.Reason);
			Assert.AreEqual(Tw.TWCC_OPERATIONERROR,ended.ConditionCode);
			StringAssert.Contains(ended.ErrorText,"0x0005");
			Assert.AreEqual(4,Helper.State);
			Helper.EnableFails=false;
			Assert.AreEqual(TwainCaptureEndReason.Completed,_harness.WaitForEnded(EnableOne()).Reason);
		}

		[TestMethod]
		public void DeviceOnlineFalse_NothingEnabled() {
			Helper.DeviceOnline=false;
			_harness.OpenOrFail();
			TwainCaptureEnded ended=_harness.WaitForEnded(EnableOne());
			Assert.AreEqual(TwainCaptureEndReason.DeviceOffline,ended.Reason);
			Assert.AreEqual(0,Helper.CountEnable);
			Assert.AreEqual(false,Capture.SourceInfo.DeviceOnline);
		}

		[TestMethod]
		public void TransferFails_TransferFailed_SourceMayHoldImages() {
			Helper.ListExposures[0].FailCondition=Tw.TWCC_LOWMEMORY;
			_harness.OpenOrFail();
			TwainCaptureEnded ended=_harness.WaitForEnded(EnableOne());
			Assert.AreEqual(TwainCaptureEndReason.TransferFailed,ended.Reason);
			Assert.AreEqual(1,ended.TransferFailureCount);
			Assert.AreEqual(Tw.TWCC_LOWMEMORY,ended.ConditionCode);
			Assert.IsTrue(ended.SourceMayHoldImages);
			Assert.AreEqual(0,ended.ImageCount);
			Assert.AreEqual(1,Helper.CountRedeliverable,"The source keeps it for redelivery.");
			Assert.IsTrue(_harness.HasLog("Capture event: image not received"),_harness.LogText);
		}

		[TestMethod]
		public void TransferFailsWithoutAFailedTriplet_NoConditionCodeOfAnEarlierFailure() {
			Helper.SupportsExtImageInfo=false;//MSG_QUERYSUPPORT on ICAP_EXTIMAGEINFO fails with TWCC_CAPUNSUPPORTED before the enable.
			FakePage page=Helper.ListExposures[0];
			page.NativeDib=page.NativeDib.Take(page.NativeDib.Length/2).ToArray();//TWRC_XFERDONE with a DIB that cannot be read.
			_harness.OpenOrFail();
			TwainCaptureEnded ended=_harness.WaitForEnded(EnableOne());
			Assert.AreEqual(TwainCaptureEndReason.TransferFailed,ended.Reason);
			Assert.AreEqual(1,ended.TransferFailureCount);
			Assert.IsNull(ended.ConditionCode,"Not TWCC_CAPUNSUPPORTED from the earlier MSG_QUERYSUPPORT.");
			Assert.IsTrue(Calls.Any(x => x.Rc==Tw.TWRC_FAILURE && x.Condition==Tw.TWCC_CAPUNSUPPORTED),"The scenario has an earlier failure.\r\n"
				+_harness.CallText);
		}
		#endregion Ends without an image

		#region Stops
		[TestMethod]
		public void Cancel_State5_WithoutSafeDisable_StoppedAndLoggedAsCaptureEvent() {
			Helper.ListExposures.Clear();
			Helper.End=FakeHelperEnd.Never;
			Helper.ActionOnEnable=() => new Thread(() => Capture.Cancel()).Start();
			_harness.OpenOrFail();
			TwainCaptureEnded ended=_harness.WaitForEnded(EnableOne());
			Assert.AreEqual(TwainCaptureEndReason.Stopped,ended.Reason);
			Assert.AreEqual(TwainCaptureStopReason.Cancel,ended.StopReason);
			Assert.IsTrue(ended.WasDisableStartedUnsafe);
			Assert.IsFalse(ended.IsSafeDisableInEffect);
			Assert.IsTrue(_harness.HasLog("Capture event: enable"),_harness.LogText);
			Assert.AreEqual(4,Helper.State);
			_harness.AssertDisabledOnlyInState5();
		}

		[TestMethod]
		public void Cancel_State6_FromAnotherThread_PendingImagesDrainedWithTheStopReason() {
			Helper.Mode=FakeHelperMode.Multi;
			Helper.ListExposures.Add(SessionHarness.Exposure(9));
			Helper.ListExposures.Add(SessionHarness.Exposure(10));
			Helper.EndAfterMs=0;
			_harness.ActionOnImage=image => {
				if(image.IndexInEnable==1) {
					Thread thread=new Thread(() => Capture.Disable(TwainCaptureStopReason.PatientChanged));
					thread.Start();
					thread.Join();
				}
			};
			_harness.OpenOrFail();
			TwainCaptureEnded ended=_harness.WaitForEnded(EnableOne());
			Assert.AreEqual(TwainCaptureEndReason.Stopped,ended.Reason);
			Assert.AreEqual(TwainCaptureStopReason.PatientChanged,ended.StopReason);
			Assert.AreEqual(3,ended.ImageCount,"Nothing is dropped.");
			Assert.AreEqual(2,ended.DrainedImageCount);
			List<AcquiredImage> listImages=_harness.Images;
			Assert.AreEqual(AcquiredImageKind.Normal,listImages[0].Kind);
			Assert.IsTrue(listImages.Skip(1).All(x => x.Kind==AcquiredImageKind.Drained && x.StopReason==TwainCaptureStopReason.PatientChanged
				&& x.EnableId==ended.EnableId));
			Assert.AreEqual(3,Helper.CountDelivered);
			_harness.AssertDisabledOnlyInState5();
		}

		[TestMethod]
		public void Cancel_State7_FromAnotherThread_TransferFinishesThenSafeStop() {
			Helper.ListExposures[0].Original16=FakePage.CreateGray16(8,40,FakeHelperSource.Dpi,(x,y) => x+y);
			Helper.MemoryColumnsPerStrip=2;//Several strips: the Cancel comes in state 7.
			bool isCancelled=false;
			Helper.ActionOnTransfer=() => {
				if(!isCancelled && Helper.State==7) {
					isCancelled=true;
					Thread thread=new Thread(() => Capture.Cancel());
					thread.Start();
					thread.Join();
				}
			};
			_harness.OpenOrFail();
			TwainCaptureEnded ended=_harness.WaitForEnded(EnableOne(16));
			Assert.IsTrue(isCancelled);
			Assert.AreEqual(1,_harness.Images.Count,"The image in transfer arrives.");
			Assert.IsTrue(_harness.Images[0].IsGray16);
			Assert.AreEqual(1,Helper.CountDelivered);
			Assert.AreEqual(4,Helper.State);
			_harness.AssertDisabledOnlyInState5();
			Assert.IsTrue(ended.Reason==TwainCaptureEndReason.Completed || ended.Reason==TwainCaptureEndReason.Stopped,ended.Reason.ToString());
		}

		[TestMethod]
		public void SafeDisable_RefusedWhileClosing_ThenXferReady_ImageDrainedIntoTheEnable() {
			ConfigureSafeDisable();
			Helper.EndAfterMs=60000;//The helper is reading out the exposure.
			Helper.ActionOnEnable=() => Capture.Disable(TwainCaptureStopReason.TargetChanged);
			_harness.OpenOrFail();
			TwainCaptureEnded ended=_harness.WaitForEnded(EnableOne());
			Assert.AreEqual(TwainCaptureEndReason.Stopped,ended.Reason);
			Assert.IsTrue(ended.IsSafeDisableInEffect);
			Assert.IsFalse(ended.WasDisableStartedUnsafe);
			Assert.IsTrue(ended.WasDisableRefused);
			Assert.IsFalse(ended.WasSecondDisableAfterWait);
			AcquiredImage image=_harness.Images.Single();
			Assert.AreEqual(AcquiredImageKind.Drained,image.Kind);
			Assert.AreEqual(TwainCaptureStopReason.TargetChanged,image.StopReason);
			Assert.AreEqual(ended.EnableId,image.EnableId,"Placed in the target of the enable it came from.");
			Assert.IsTrue(_harness.States.Contains(TwainCaptureState.WaitingForSource));
			Assert.AreEqual(1,Helper.CountDelivered,"Nothing lost.");
			_harness.AssertDisabledOnlyInState5();
		}

		[TestMethod]
		public void SafeDisable_RefusedAndNoNotification_SecondDisable_SourceMayHoldImages() {
			ConfigureSafeDisable();
			Options.RefusedDisableWait=TimeSpan.FromMilliseconds(300);
			Helper.EndAfterMs=60000;
			Helper.IgnoresCloseRequest=true;//A source that never answers.
			Helper.ForcedEndMs=60000;
			Helper.ActionOnEnable=() => Capture.Cancel();
			_harness.OpenOrFail();
			TwainCaptureEnded ended=_harness.WaitForEnded(EnableOne());
			Assert.AreEqual(TwainCaptureEndReason.Stopped,ended.Reason);
			Assert.IsTrue(ended.WasSecondDisableAfterWait);
			Assert.IsTrue(ended.SourceMayHoldImages);
			Assert.AreEqual(1,Helper.CountKeptForRedelivery);
			Assert.IsTrue(_harness.HasLog("second consecutive MSG_DISABLEDS"),_harness.LogText);
		}

		[TestMethod]
		public void SourceNeverAnswers_CancelStillStops() {
			Helper.ListExposures.Clear();
			Helper.End=FakeHelperEnd.Never;
			_harness.OpenOrFail();
			int enableId=EnableOne();
			_harness.WaitUntil(() => _harness.ListStatus.Any(x => x.State==TwainCaptureState.WaitingForImage),"waiting for the image");
			Thread.Sleep(100);
			Capture.Cancel();
			Assert.AreEqual(TwainCaptureEndReason.Stopped,_harness.WaitForEnded(enableId,5000).Reason);
		}

		[TestMethod]
		public void SessionTimeout_HonouredWithSafeDisable() {
			ConfigureSafeDisable();
			Options.SessionTimeout=TimeSpan.FromMilliseconds(300);
			Helper.ListExposures.Clear();
			Helper.End=FakeHelperEnd.Never;
			Helper.CloseDelayMs=10;
			_harness.OpenOrFail();
			TwainCaptureEnded ended=_harness.WaitForEnded(EnableOne(),5000);
			Assert.AreEqual(TwainCaptureEndReason.Stopped,ended.Reason);
			Assert.AreEqual(TwainCaptureStopReason.SessionTimeout,ended.StopReason);
		}

		[TestMethod]
		public void SessionTimeout_NotHonouredWithoutSafeDisable() {
			Options.SessionTimeout=TimeSpan.FromMilliseconds(200);
			Helper.ListExposures.Clear();
			Helper.End=FakeHelperEnd.Never;
			_harness.OpenOrFail();
			int enableId=EnableOne();
			Thread.Sleep(700);
			Assert.IsFalse(_harness.ListEnded.Any(x => x.EnableId==enableId),"Still waiting for the exposure.");
			Assert.IsTrue(_harness.HasLog("SessionTimeoutMinutes is not honoured"),_harness.LogText);
			Capture.Cancel();
			Assert.AreEqual(TwainCaptureStopReason.Cancel,_harness.WaitForEnded(enableId).StopReason);
		}

		[TestMethod]
		public void EnableWhileEnabled_Refused() {
			Helper.ListExposures.Clear();
			Helper.End=FakeHelperEnd.Never;
			_harness.OpenOrFail();
			int enableId=EnableOne();
			_harness.WaitUntil(() => _harness.ListStatus.Any(x => x.State==TwainCaptureState.WaitingForImage),"waiting for the image");
			int enableId2=EnableOne();
			Assert.AreEqual(TwainCaptureEndReason.Refused,_harness.WaitForEnded(enableId2).Reason);
			Capture.Cancel();
			Assert.AreEqual(TwainCaptureEndReason.Stopped,_harness.WaitForEnded(enableId).Reason);
			Assert.AreEqual(1,Helper.CountEnable);
		}

		[TestMethod]
		public void CommandWhileEnabled_TheSourcesOwnMessageStillGoesToIt() {
			const int wmSource=0x0400+0x123;//WM_USER+0x123: a message of the source's own, on the window it was given.
			Helper.ListExposures.Clear();
			Helper.End=FakeHelperEnd.Never;
			Helper.ActionOnEnable=() => {
				//On the worker, inside MSG_ENABLEDS. The wake-up of the first enable may still be queued; it was handled, so it goes first.
				IntPtr pMsg=Marshal.AllocHGlobal(NativeMethods.MsgBufferSize);
				try {
					while(NativeMethods.PeekMessage(pMsg,_harness.Fake.HwndParent,TwainSession.WM_APP_WAKE,TwainSession.WM_APP_WAKE,NativeMethods.PM_REMOVE)) {
					}
				}
				finally {
					Marshal.FreeHGlobal(pMsg);
				}
				//The source posts a message of its own, then a command arrives: its wake-up is queued behind the source's message.
				NativeMethods.PostMessage(_harness.Fake.HwndParent,wmSource,IntPtr.Zero,IntPtr.Zero);
				EnableOne();
			};
			_harness.OpenOrFail();
			int enableId=EnableOne();
			_harness.WaitUntil(() => _harness.ListEnded.Any(x => x.Reason==TwainCaptureEndReason.Refused),"the refused second enable");
			Capture.Cancel();
			Assert.AreEqual(TwainCaptureEndReason.Stopped,_harness.WaitForEnded(enableId).Reason);
			Assert.IsTrue(Calls.Any(x => x.Name=="PROCESSEVENT" && x.EventMessage==wmSource),
				"Every message goes to the enabled source (TWAIN 2.4 p.12-3); a command never drops one.\r\n"+_harness.CallText);
		}

		[TestMethod]
		public void DisableWhileTheEnableIsPrepared_NoEnableDsSent_NothingStoppedUnsafely() {
			Options.RedeliverCap=FakeHelperSource.CapRedeliver;
			bool isDisablePending=true;
			Capture.StatusChanged+=status => {
				if(status.State==TwainCaptureState.Enabling && isDisablePending) {
					isDisablePending=false;
					Capture.Disable(TwainCaptureStopReason.TargetChanged);//Stop is clickable as soon as the enable is posted.
				}
			};
			_harness.OpenOrFail();
			TwainCaptureEnded ended=_harness.WaitForEnded(EnableOne());
			Assert.AreEqual(TwainCaptureEndReason.Stopped,ended.Reason);
			Assert.AreEqual(TwainCaptureStopReason.TargetChanged,ended.StopReason);
			Assert.IsFalse(ended.WasDisableStartedUnsafe);
			Assert.AreEqual(0,Helper.CountEnable,"MSG_ENABLEDS was never sent: the sensor software did not start.");
			Assert.IsFalse(_harness.HasLog("Capture event: enable"),"No false 'may hold an exposure' event.\r\n"+_harness.LogText);
			Assert.AreEqual(4,Helper.State);
			//The same for a redelivery: RedeliverCap goes back to FALSE without an enable.
			isDisablePending=true;
			int start=Calls.Count;
			ended=_harness.WaitForEnded(Capture.RequestRedelivery(null));
			Assert.AreEqual(TwainCaptureEndReason.Stopped,ended.Reason);
			Assert.AreEqual(0,Helper.CountEnable);
			int idxTrue=_harness.IndexOfCall(x => x.Name=="CAP/SET" && x.Cap==FakeHelperSource.CapRedeliver && x.Value==1,start);
			int idxFalse=_harness.IndexOfCall(x => x.Name=="CAP/SET" && x.Cap==FakeHelperSource.CapRedeliver && x.Value==0,start);
			Assert.IsTrue(idxTrue>=0 && idxFalse>idxTrue,_harness.CallText);
			Assert.AreEqual(TwainCaptureEndReason.Completed,_harness.WaitForEnded(EnableOne()).Reason,"The session stays usable.");
			Assert.AreEqual(1,Helper.CountEnable);
		}

		[TestMethod]
		public void DisableThenEnable_RunInOrder() {
			Helper.ListExposures.Clear();
			Helper.End=FakeHelperEnd.Never;
			_harness.OpenOrFail();
			int enableId=EnableOne();
			_harness.WaitUntil(() => _harness.ListStatus.Any(x => x.State==TwainCaptureState.WaitingForImage),"waiting for the image");
			Capture.Disable(TwainCaptureStopReason.TargetChanged);
			int enableId2=EnableOne();
			Assert.AreEqual(TwainCaptureStopReason.TargetChanged,_harness.WaitForEnded(enableId).StopReason);
			_harness.WaitUntil(() => Helper.CountEnable==2,"the second enable");
			Capture.Cancel();
			Assert.AreEqual(TwainCaptureEndReason.Stopped,_harness.WaitForEnded(enableId2).Reason);
		}

		[TestMethod]
		public void OpenFromAnEventHandler_Throws() {
			Exception exception=null;
			_harness.ActionOnImage=image => {
				try {
					Capture.Open("x",null);
				}
				catch(Exception ex) {
					exception=ex;
				}
			};
			_harness.OpenOrFail();
			_harness.WaitForEnded(EnableOne());
			Assert.IsInstanceOfType(exception,typeof(InvalidOperationException));
		}
		#endregion Stops

		#region Continuous
		[TestMethod]
		public void Continuous_StaysInState5AfterCountZero_UntilDisable_RepeatedXferReadyWithNothingPendingIgnored() {
			Helper.SupportsContinuous=true;
			Helper.SpuriousXferReadyAfterEachImage=true;
			Helper.End=FakeHelperEnd.Never;
			Helper.ListExposures.Add(SessionHarness.Exposure(9));
			Helper.ListExposures.Add(SessionHarness.Exposure(10));
			Options.SessionMode=TwainSessionMode.Continuous;
			Options.ContinuousCap=FakeHelperSource.CapContinuous;
			_harness.OpenOrFail();
			int enableId=EnableOne();
			_harness.WaitUntil(() => _harness.ListImages.Count==3,"three images");
			Thread.Sleep(100);
			Assert.IsFalse(_harness.ListEnded.Any(),"Still enabled after Count=0.");
			Assert.AreEqual(5,Helper.State);
			Assert.IsTrue(_harness.Images.All(x => x.Kind==AcquiredImageKind.Normal && x.EnableId==enableId));
			Capture.Disable(TwainCaptureStopReason.End);
			TwainCaptureEnded ended=_harness.WaitForEnded(enableId);
			Assert.AreEqual(TwainCaptureEndReason.Stopped,ended.Reason);
			Assert.AreEqual(TwainCaptureStopReason.End,ended.StopReason);
			Assert.AreEqual(3,ended.ImageCount);
			Assert.IsTrue(_harness.HasLog("MSG_XFERREADY with nothing pending; ignored"),_harness.LogText);
		}

		[TestMethod]
		public void Continuous_CapabilityUnsupported_RunsPerSlot() {
			Options.SessionMode=TwainSessionMode.Continuous;
			Options.ContinuousCap=FakeHelperSource.CapContinuous;//Reserved, not implemented in 1.1.
			_harness.OpenOrFail();
			TwainCaptureEnded ended=_harness.WaitForEnded(EnableOne());
			Assert.AreEqual(TwainCaptureEndReason.Completed,ended.Reason,"Never waits in state 5 after Count=0.");
			Assert.AreEqual(4,Helper.State);
			Assert.IsTrue(_harness.HasLog("the enable runs per slot"),_harness.LogText);
		}

		[TestMethod]
		public void PerSlot_NeverWaitsInState5_EvenWithContinuousSupported() {
			Helper.SupportsContinuous=true;
			Options.ContinuousCap=FakeHelperSource.CapContinuous;//SessionMode stays PerSlot.
			_harness.OpenOrFail();
			Assert.AreEqual(TwainCaptureEndReason.Completed,_harness.WaitForEnded(EnableOne()).Reason);
			Assert.IsFalse(Calls.Any(x => x.Name=="CAP/SET" && x.Cap==FakeHelperSource.CapContinuous),"Only SessionMode=Continuous sets it.");
		}
		#endregion Continuous

		#region Redelivery
		///<summary>An exposure the host lost: a disable without safe disable while the helper had it (contract section 7).</summary>
		private void LoseOneImage() {
			Helper.EndAfterMs=60000;
			Helper.ActionOnEnable=() => Capture.Cancel();
			TwainCaptureEnded ended=_harness.WaitForEnded(EnableOne());
			Assert.AreEqual(TwainCaptureEndReason.Stopped,ended.Reason);
			Assert.AreEqual(1,Helper.CountLostForHost);
			Assert.AreEqual(1,Helper.CountRedeliverable);
			Helper.ActionOnEnable=null;
			Helper.EndAfterMs=0;
		}

		[TestMethod]
		public void Redelivery_CapTrueRightBeforeTheEnable_FalseRightAfter_ImageFlagged() {
			Options.RedeliverCap=FakeHelperSource.CapRedeliver;
			_harness.OpenOrFail();
			LoseOneImage();
			string captureIdLost=Helper.GetCaptureId(Helper.ListExposures[0]);
			int start=Calls.Count;
			int enableId=Capture.RequestRedelivery(new TwainEnableRequest { Tag="redeliver" });
			TwainCaptureEnded ended=_harness.WaitForEnded(enableId);
			Assert.AreEqual(TwainCaptureEndReason.Completed,ended.Reason,ended.ErrorText);
			Assert.IsTrue(ended.IsRedelivery);
			AcquiredImage image=_harness.Images.Single();
			Assert.IsTrue(image.IsRedelivery);
			Assert.AreEqual(AcquiredImageKind.Normal,image.Kind);
			StringAssert.Contains(image.MetadataJson,"\"redelivered\":true");
			StringAssert.Contains(image.MetadataJson,captureIdLost,"The same captureId.");
			int idxTrue=_harness.IndexOfCall(x => x.Name=="CAP/SET" && x.Cap==FakeHelperSource.CapRedeliver && x.Value==1,start);
			int idxEnable=_harness.IndexOfCall(x => x.Name=="ENABLEDS",start);
			int idxDisable=_harness.IndexOfCall(x => x.Name=="DISABLEDS" && x.Rc==Tw.TWRC_SUCCESS,idxEnable);
			int idxFalse=_harness.IndexOfCall(x => x.Name=="CAP/SET" && x.Cap==FakeHelperSource.CapRedeliver && x.Value==0,start);
			Assert.IsTrue(idxTrue>=0 && idxEnable>idxTrue && idxDisable>idxEnable && idxFalse>idxDisable,_harness.CallText);
			Assert.IsFalse(Calls.Skip(idxTrue+1).Take(idxEnable-idxTrue-1).Any(x => x.Name=="CAP/SET"),"TRUE is the last setting before MSG_ENABLEDS.");
			Assert.AreEqual(4,Calls[idxTrue].StateBefore);
			Assert.AreEqual(4,Calls[idxFalse].StateBefore);
			Assert.AreEqual(1,Helper.CountRedeliveryEnables);
			Assert.AreEqual(0,Helper.CountRedeliverable);
		}

		[TestMethod]
		public void Redelivery_SourceKeepsTheCapUntilClose_NextNormalEnableStillCaptures() {
			Helper.RedeliverKeptUntilClose=true;
			Options.RedeliverCap=FakeHelperSource.CapRedeliver;
			_harness.OpenOrFail();
			LoseOneImage();
			_harness.WaitForEnded(Capture.RequestRedelivery(null));
			int countEnable=Helper.CountEnable;
			TwainCaptureEnded ended=_harness.WaitForEnded(EnableOne());
			Assert.AreEqual(TwainCaptureEndReason.Completed,ended.Reason);
			Assert.IsFalse(ended.IsRedelivery);
			Assert.AreEqual(1,Helper.CountRedeliveryEnables,"The normal enable started a capture, not another redelivery.");
			Assert.IsFalse(_harness.Images.Last().IsRedelivery);
			Assert.AreEqual(countEnable+1,Helper.CountEnable);
		}

		[TestMethod]
		public void Redelivery_NothingToRedeliver_CloseRequestedWithItsCode() {
			Options.RedeliverCap=FakeHelperSource.CapRedeliver;
			_harness.OpenOrFail();
			TwainCaptureEnded ended=_harness.WaitForEnded(Capture.RequestRedelivery(null));
			Assert.AreEqual(TwainCaptureEndReason.CloseRequested,ended.Reason);
			Assert.AreEqual(FakeHelperSource.CodeNothingToRedeliver,ended.ConditionCode);
			Assert.AreEqual(0,_harness.Images.Count);
		}

		[TestMethod]
		public void Redelivery_NoCapabilityInTheProfile_NothingEnabled() {
			_harness.OpenOrFail();
			TwainCaptureEnded ended=_harness.WaitForEnded(Capture.RequestRedelivery(null));
			Assert.AreEqual(TwainCaptureEndReason.RedeliveryUnavailable,ended.Reason);
			Assert.AreEqual(0,Helper.CountEnable,"No enable: it would start a capture.");
		}

		[TestMethod]
		public void Redelivery_Contract10Source_Unavailable() {
			Helper.SupportsSafeDisable=false;//Every custom capability returns TWCC_CAPUNSUPPORTED.
			Options.RedeliverCap=FakeHelperSource.CapRedeliver;
			_harness.OpenOrFail();
			Assert.AreEqual(TwainCaptureEndReason.RedeliveryUnavailable,_harness.WaitForEnded(Capture.RequestRedelivery(null)).Reason);
			Assert.AreEqual(0,Helper.CountEnable);
		}
		#endregion Redelivery

		#region Close
		[TestMethod]
		public void BeginClose_WhileWaiting_SafeStop_SourceAndDsmClosed() {
			Helper.ListExposures.Clear();
			Helper.End=FakeHelperEnd.Never;
			_harness.OpenOrFail();
			int enableId=EnableOne();
			_harness.WaitUntil(() => _harness.ListStatus.Any(x => x.State==TwainCaptureState.WaitingForImage),"waiting for the image");
			_harness.Close();
			TwainCaptureEnded ended=_harness.ListEnded.Single(x => x.EnableId==enableId);
			Assert.AreEqual(TwainCaptureStopReason.Close,ended.StopReason);
			Assert.IsTrue(_harness.Fake.ListLog.Contains("CLOSEDS"));
			Assert.IsTrue(_harness.Fake.ListLog.Contains("CLOSEDSM"));
			Assert.IsFalse(_harness.Fake.IsSourceOpen);
			Assert.AreEqual(0,Capture.Enable(new TwainEnableRequest()),"Closed.");
			_harness.AssertDisabledOnlyInState5();
		}

		[TestMethod]
		public void BeginClose_DuringARefusedDisableWait_LaterImageGoesStraightToTheQuarantine() {
			ConfigureSafeDisable();
			Helper.EndAfterMs=60000;
			Helper.CloseDelayMs=500;//The helper delivers its image 0.5 s into the stop.
			Capture.QuarantineContext=new TwainCaptureQuarantineContext(42,"Doe, Jane");
			_harness.OpenOrFail();
			int enableId=Capture.Enable(new TwainEnableRequest { TargetLabel="Slot 4" });
			_harness.WaitUntil(() => _harness.ListStatus.Any(x => x.State==TwainCaptureState.WaitingForImage),"waiting for the image");
			Capture.Disable(TwainCaptureStopReason.End);
			_harness.WaitUntil(() => _harness.ListStatus.Any(x => x.State==TwainCaptureState.WaitingForSource),"the refused-disable wait");
			Capture.BeginClose();//The window closes 1 s into the stop... here at once.
			Assert.IsTrue(Capture.WaitForClose(TimeSpan.FromSeconds(10)),_harness.LogText);
			Assert.AreEqual(0,_harness.ListImages.Count,"No ImageAcquired after BeginClose.");
			AcquiredImage image=_harness.ListQuarantined.Single();
			Assert.AreEqual(enableId,image.EnableId);
			Assert.IsTrue(File.Exists(image.QuarantinePath));
			Assert.AreEqual(Path.Combine(_harness.Folder,@"Capture\quarantine",Capture.SessionGuid.ToString("D")),Path.GetDirectoryName(image.QuarantinePath));
			Dictionary<string,string> dictSidecar=CaptureFiles.ReadSidecar(Path.ChangeExtension(image.QuarantinePath,".txt"));
			Assert.AreEqual("42",dictSidecar["PatNum"]);
			Assert.AreEqual("Doe, Jane",dictSidecar["Patient"]);
			Assert.AreEqual("Slot 4",dictSidecar["Slot"]);
			Assert.AreEqual(Capture.SessionGuid.ToString("D"),dictSidecar["SessionGuid"]);
			Dictionary<string,string> dictJournal=CaptureFiles.ReadSidecar(Path.ChangeExtension(image.JournalPath,".txt"));
			Assert.AreEqual(CaptureJournal.DeliveredQuarantine,dictJournal["Delivered"]);
			Assert.AreEqual(1,Helper.CountDelivered,"Nothing lost.");
		}

		///<summary>The metadata parser runs after the image arrived and before its journal copy is written, so a BeginClose from it is a close
		///that comes while the copy is written (slow I/O for a 16-bit image).</summary>
		private void CloseWhileTheJournalCopyIsWritten(Action actionBeforeClose=null) {
			bool isClosed=false;
			Capture.FuncReadMetadataKeys=json => {
				if(!isClosed) {
					isClosed=true;
					actionBeforeClose?.Invoke();
					Capture.BeginClose();
				}
				return new CaptureMetadataKeys { CaptureId="cid-late" };
			};
		}

		[TestMethod]
		public void BeginClose_WhileTheJournalCopyIsWritten_ImageQuarantinedNeverRaised() {
			Capture.QuarantineContext=new TwainCaptureQuarantineContext(42,"Doe, Jane");
			//The patient changes as well: the image keeps the patient it arrived with, PatNum and name together.
			CloseWhileTheJournalCopyIsWritten(() => Capture.QuarantineContext=new TwainCaptureQuarantineContext(43,"Roe, Ann"));
			_harness.OpenOrFail();
			int enableId=EnableOne();
			Assert.IsTrue(enableId>0);
			Assert.IsTrue(Capture.WaitForClose(TimeSpan.FromSeconds(10)),_harness.LogText);
			Assert.AreEqual(0,_harness.ListImages.Count,"No ImageAcquired after BeginClose.");
			AcquiredImage image=_harness.ListQuarantined.Single();
			Assert.AreEqual(enableId,image.EnableId);
			Assert.IsTrue(File.Exists(image.QuarantinePath));
			Dictionary<string,string> dictSidecar=CaptureFiles.ReadSidecar(Path.ChangeExtension(image.QuarantinePath,".txt"));
			Assert.AreEqual("42",dictSidecar["PatNum"]);
			Assert.AreEqual("Doe, Jane",dictSidecar["Patient"]);
			Assert.AreEqual("Slot 1",dictSidecar["Slot"]);
			Dictionary<string,string> dictJournal=CaptureFiles.ReadSidecar(Path.ChangeExtension(image.JournalPath,".txt"));
			Assert.AreEqual(CaptureJournal.DeliveredQuarantine,dictJournal["Delivered"],"The copy's sidecar says where the image went.");
			Assert.AreEqual(Path.GetFileName(image.QuarantinePath),dictJournal["DeliveredDetail"]);
			Assert.AreEqual("42",dictJournal["PatNum"]);
			Assert.AreEqual("cid-late",dictJournal["CaptureId"]);
			Assert.AreEqual(Path.GetFileName(image.JournalPath),dictJournal["ImageFile"]);
			Assert.AreEqual(1,Directory.GetFiles(Path.GetDirectoryName(image.JournalPath),"*.txt").Length,"One sidecar for the one copy.");
			Assert.IsTrue(_harness.HasLog("arrived while the session closed"),_harness.LogText);
			Assert.AreEqual(1,Helper.CountDelivered,"Nothing lost.");
		}

		[TestMethod]
		public void BeginClose_WhileTheJournalCopyIsWritten_QuarantineFails_CopyKeptAsTheOnlyOne() {
			CloseWhileTheJournalCopyIsWritten(() => {
				//A file where the quarantine folder should be: the quarantine cannot be written.
				Directory.CreateDirectory(Path.Combine(_harness.Folder,"Capture"));
				File.WriteAllText(Path.Combine(_harness.Folder,@"Capture\quarantine"),"not a folder");
			});
			_harness.OpenOrFail();
			EnableOne();
			Assert.IsTrue(Capture.WaitForClose(TimeSpan.FromSeconds(10)),_harness.LogText);
			Assert.AreEqual(0,_harness.ListImages.Count);
			AcquiredImage image=_harness.ListQuarantined.Single();
			Assert.IsNull(image.QuarantinePath);
			Assert.IsTrue(File.Exists(image.JournalPath),image.JournalPath);
			StringAssert.EndsWith(Path.GetFileNameWithoutExtension(image.JournalPath),CaptureJournal.OnlyCopySuffix,"Rotation never removes it.");
			Dictionary<string,string> dictJournal=CaptureFiles.ReadSidecar(Path.ChangeExtension(image.JournalPath,".txt"));
			Assert.AreEqual(CaptureJournal.DeliveredNone,dictJournal["Delivered"]);
			Assert.AreEqual(Path.GetFileName(image.JournalPath),dictJournal["ImageFile"]);
			string folderJournal=Path.GetDirectoryName(image.JournalPath);
			Assert.AreEqual(1,Directory.GetFiles(folderJournal,"*.txt").Length,"The old sidecar is gone.");
			Assert.AreEqual(1,Directory.GetFiles(folderJournal,"*.bmp").Length);
			Assert.IsTrue(_harness.HasLog("its journal copy is the only one"),_harness.LogText);
		}

		[TestMethod]
		public void QuarantineImage_FromTheWindow_WithTheBoundPatientOrUnknown() {
			Capture.QuarantineContext=new TwainCaptureQuarantineContext(9,"Roe, Ann");
			_harness.OpenOrFail();
			_harness.WaitForEnded(EnableOne());
			AcquiredImage image=_harness.Images.Single();
			string error;
			string path=Capture.QuarantineImage(image,"Could not save: disk full.",false,out error);
			Assert.IsNotNull(path,error);
			Assert.AreEqual("9",CaptureFiles.ReadSidecar(Path.ChangeExtension(path,".txt"))["PatNum"]);
			string pathUnknown=Capture.QuarantineImage(image,"Declined redelivered image.",true,out error);
			Assert.AreEqual(QuarantineWriter.PatientUnknown,CaptureFiles.ReadSidecar(Path.ChangeExtension(pathUnknown,".txt"))["Patient"]);
			DateTime? oldest;
			Assert.AreEqual(2,QuarantineWriter.CountImages(_harness.Folders,out oldest));
		}
		#endregion Close

		#region Other sources
		[TestMethod]
		public void Contract10Source_8BitsNoMetadataNoCustomCapabilities_FixedCapabilitiesStillNotSet() {
			Helper.Supports16Bit=false;
			Helper.SupportsExtImageInfo=false;
			Helper.SupportsSafeDisable=false;
			Helper.SupportsStatusUtf8=false;
			Helper.FixedCapsAcceptAnyValue=true;
			ConfigureSafeDisable();
			Options.NoActivateCap=FakeHelperSource.CapNoActivate;
			_harness.OpenOrFail();
			TwainCaptureEnded ended=_harness.WaitForEnded(EnableOne(16));
			Assert.AreEqual(TwainCaptureEndReason.Completed,ended.Reason);
			Assert.IsFalse(ended.IsBitDepth16InEffect);
			StringAssert.Contains(ended.Reason8Bit,"does not list 16");
			Assert.IsFalse(ended.IsSafeDisableInEffect,"TWCC_CAPUNSUPPORTED: not safe.");
			AcquiredImage image=_harness.Images.Single();
			Assert.IsNull(image.MetadataJson);
			Assert.AreEqual(8,image.BitsPerPixel);
			Assert.IsFalse(Capture.SourceInfo.IsMetadataListed);
			_harness.Close();
			_harness.AssertNoFixedCapabilitySet();
		}

		[TestMethod]
		public void MetadataItemNotSupported_NoMetadata_ImageStillRaised() {
			Helper.MetadataReturnCode=Tw.TWRC_INFONOTSUPPORTED;
			_harness.OpenOrFail();
			_harness.WaitForEnded(EnableOne());
			Assert.IsNull(_harness.Images.Single().MetadataJson);
			Assert.IsTrue(_harness.HasLog("has no TWEI_TWAINDIRECTMETADATA for this image"),_harness.LogText);
		}

		[TestMethod]
		public void ExceptionInState7_EndXferThenDisable_SessionUsableAfterwards() {
			bool isThrown=false;
			Helper.ActionOnTransfer=() => {
				if(!isThrown && Helper.State==7) {//The second strip: state 7.
					isThrown=true;
					throw new InvalidOperationException("Broken source.");
				}
			};
			Helper.ListExposures[0].Original16=FakePage.CreateGray16(8,5,FakeHelperSource.Dpi,(x,y) => x);
			Helper.MemoryColumnsPerStrip=4;
			_harness.OpenOrFail();
			TwainCaptureEnded ended=_harness.WaitForEnded(EnableOne(16));
			Assert.AreEqual(TwainCaptureEndReason.Error,ended.Reason);
			Assert.AreEqual(TwainCaptureStopReason.Error,ended.StopReason);
			StringAssert.Contains(ended.ErrorText,"Broken source.");
			Assert.AreEqual(4,Helper.State);
			Assert.AreEqual(TwainCaptureEndReason.Completed,_harness.WaitForEnded(EnableOne()).Reason,"The source stays open and usable.");
			_harness.AssertDisabledOnlyInState5();
		}

		[TestMethod]
		public void CallerWindows_NeverDisabled() {
			using(ProxyWindow window=new ProxyWindow()) {
				bool? isEnabledDuringEnable=null;
				Helper.ActionOnEnable=() => isEnabledDuringEnable=NativeMethods.IsWindowEnabled(window.Handle);
				_harness.OpenOrFail();
				_harness.WaitForEnded(EnableOne());
				Assert.AreEqual(true,isEnabledDuringEnable,"Only the worker's own windows fall under the thread-modal rule.");
			}
		}
		#endregion Other sources
	}
}
