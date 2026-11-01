// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using ImagingDeviceManager.Imaging;
using ImagingDeviceManager.TwainProtocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingDeviceManager.Tests {
	///<summary>Checks the simulated sources themselves, with raw triplets: the "single-shot helper DS, contract 1.1" profile against
	///twain-contract 1.1 sections 4-11, and the strict generic profile against TWAIN 2.4. The session tests rely on these profiles,
	///so they are pinned here.</summary>
	[TestClass]
	public class FakeProfileTests {
		private FakeTwainDsm _fake;
		private FakeHelperSource _helper;

		[TestInitialize]
		public void Setup() {
			_fake=new FakeTwainDsm();
			_helper=new FakeHelperSource();
			_fake.AddHelperSource(_helper);
		}

		[TestCleanup]
		public void Cleanup() {
			GC.Collect();
			GC.WaitForPendingFinalizers();
		}

		private static FakePage Exposure(int width) {
			return FakePage.CreateGray8(width,6,FakeHelperSource.Dpi,(x,y) => (byte)(x*10+y));
		}

		private FakeRawClient OpenHelper() {
			return new FakeRawClient(_fake,_helper.ProductName);
		}

		private static void TransferAll(FakeRawClient client,int countExpected) {
			for(int i=0;i<countExpected;i++) {
				byte[] dib;
				IntPtr hDib;
				Assert.AreEqual(Tw.TWRC_XFERDONE,client.NativeTransfer(out dib,out hDib),"image "+i);
				short left=client.EndTransfer();
				Assert.AreEqual(countExpected-i-1,left,"pending after image "+i);
			}
		}

		#region Capabilities (section 4)
		[TestMethod]
		public void Helper_XferCount_DefaultMinusOne_ZeroStoresMinusOne() {
			using(FakeRawClient client=OpenHelper()) {
				Assert.AreEqual(-1,client.GetCapValue(Tw.CAP_XFERCOUNT));
				Assert.AreEqual(Tw.TWRC_SUCCESS,client.SetCap(Tw.CAP_XFERCOUNT,Tw.TWTY_INT16,0));
				Assert.AreEqual(-1,client.GetCapValue(Tw.CAP_XFERCOUNT));
				Assert.AreEqual(Tw.TWRC_SUCCESS,client.SetCap(Tw.CAP_XFERCOUNT,Tw.TWTY_INT16,3));
				Assert.AreEqual(3,client.GetCapValue(Tw.CAP_XFERCOUNT));
			}
			using(FakeRawClient client=OpenHelper()) {
				Assert.AreEqual(-1,client.GetCapValue(Tw.CAP_XFERCOUNT),"Every MSG_OPENDS resets the capabilities.");
			}
		}

		[TestMethod]
		public void Helper_FixedCapabilities_ReadOnlyValuesAndRefusals() {
			using(FakeRawClient client=OpenHelper()) {
				Assert.AreEqual(0,client.GetCapValue(Tw.CAP_UICONTROLLABLE),"CAP_UICONTROLLABLE is FALSE in 1.1.");
				Assert.AreEqual(1,client.GetCapValue(FakeHelperSource.CapIndicators));
				Assert.AreEqual(Tw.TWRC_SUCCESS,client.SetCap(Tw.ICAP_PIXELTYPE,Tw.TWTY_UINT16,Tw.TWPT_GRAY),"Its one value is accepted.");
				Assert.AreEqual(Tw.TWRC_FAILURE,client.SetCap(Tw.ICAP_PIXELTYPE,Tw.TWTY_UINT16,Tw.TWPT_RGB));
				Assert.AreEqual(Tw.TWCC_BADVALUE,client.Status(),"Any other value: TWCC_BADVALUE.");
				Assert.AreEqual(Tw.TWRC_CHECKSTATUS,client.SetCap(FakeHelperSource.CapIndicators,Tw.TWTY_BOOL,0));
				Assert.AreEqual(1,client.GetCapValue(FakeHelperSource.CapIndicators),"CAP_INDICATORS FALSE changes nothing.");
				Assert.AreEqual(Tw.TWRC_CHECKSTATUS,client.SetCap(Tw.ICAP_XRESOLUTION,Tw.TWTY_FIX32,(int)TW_FIX32.FromDouble(300).ToUInt32()));
				Assert.AreEqual(FakeHelperSource.Dpi,TW_FIX32.FromUInt32((uint)client.GetCapValue(Tw.ICAP_XRESOLUTION)).ToDouble());
				int support;
				Assert.AreEqual(Tw.TWRC_SUCCESS,client.GetCap(Tw.CAP_XFERCOUNT,Tw.MSG_QUERYSUPPORT,out support));
				Assert.AreEqual(0x1F,support);
				Assert.AreEqual(Tw.TWRC_SUCCESS,client.GetCap(Tw.ICAP_PIXELTYPE,Tw.MSG_QUERYSUPPORT,out support));
				Assert.AreEqual(0x0D,support);
				Assert.AreEqual(Tw.TWRC_SUCCESS,client.GetCap(FakeHelperSource.CapSafeDisable,Tw.MSG_QUERYSUPPORT,out support));
				Assert.AreEqual(0x1F,support,"Custom capabilities report 0x1F (section 10.1).");
				Assert.AreEqual(Tw.TWRC_FAILURE,client.GetCap(Tw.CAP_DUPLEXENABLED,Tw.MSG_GETCURRENT,out support));
				Assert.AreEqual(Tw.TWCC_CAPUNSUPPORTED,client.Status());
				Assert.AreEqual(Tw.TWRC_SUCCESS,client.SetCap(Tw.ICAP_XFERMECH,Tw.TWTY_UINT16,Tw.TWSX_MEMORY));
				Assert.AreEqual(Tw.TWRC_SUCCESS,client.SetCap(FakeHelperSource.IcapBitDepth,Tw.TWTY_UINT16,16));
				Assert.AreEqual(Tw.TWRC_CHECKSTATUS,client.SetCap(Tw.ICAP_XFERMECH,Tw.TWTY_UINT16,Tw.TWSX_NATIVE));
				Assert.AreEqual(8,client.GetCapValue(FakeHelperSource.IcapBitDepth),"Native resets 16 bits to 8.");
				Assert.AreEqual(Tw.TWRC_FAILURE,client.SetCap(FakeHelperSource.IcapBitDepth,Tw.TWTY_UINT16,16),"16 bits only after memory.");
			}
		}

		[TestMethod]
		public void Helper_SetCapabilityWhileEnabled_SeqError() {
			_helper.End=FakeHelperEnd.Never;
			using(FakeRawClient client=OpenHelper()) {
				Assert.AreEqual(Tw.TWRC_SUCCESS,client.Enable());
				Assert.AreEqual(Tw.TWRC_FAILURE,client.SetCap(Tw.CAP_XFERCOUNT,Tw.TWTY_INT16,1));
				Assert.AreEqual(Tw.TWCC_SEQERROR,client.Status());
			}
		}

		[TestMethod]
		public void Helper_Contract10Build_CustomCapabilitiesUnsupported() {
			_helper.SupportsSafeDisable=false;
			using(FakeRawClient client=OpenHelper()) {
				int support;
				Assert.AreEqual(Tw.TWRC_FAILURE,client.GetCap(FakeHelperSource.CapSafeDisable,Tw.MSG_QUERYSUPPORT,out support));
				Assert.AreEqual(Tw.TWCC_CAPUNSUPPORTED,client.Status());
				Assert.AreEqual(Tw.TWRC_FAILURE,client.SetCap(FakeHelperSource.CapSafeDisable,Tw.TWTY_BOOL,1));
				Assert.AreEqual(Tw.TWCC_CAPUNSUPPORTED,client.Status());
			}
		}
		#endregion Capabilities (section 4)

		#region Sessions (section 5)
		[TestMethod]
		public void Helper_XferCountOne_EndsByItselfAfterOneImage_OneNotification() {
			_helper.ListExposures.Add(Exposure(10));
			_helper.ListExposures.Add(Exposure(11));
			_helper.End=FakeHelperEnd.Never;
			using(FakeRawClient client=OpenHelper()) {
				client.SetCap(Tw.CAP_XFERCOUNT,Tw.TWTY_INT16,1);
				Assert.AreEqual(Tw.TWRC_SUCCESS,client.Enable());
				short count;
				client.Pending(Tw.MSG_GET,out count);
				Assert.AreEqual(0,count,"0 pending before the notification.");
				Assert.AreEqual(Tw.MSG_XFERREADY,client.WaitForNotification(1000));
				Assert.AreEqual(6,_helper.State);
				client.Pending(Tw.MSG_GET,out count);
				Assert.AreEqual(1,count);
				TransferAll(client,1);
				Assert.AreEqual(5,_helper.State,"With 0 left the source waits in state 5.");
				Assert.AreEqual(Tw.MSG_NULL,client.WaitForNotification(60),"Exactly one notification per enable.");
				Assert.AreEqual(Tw.TWRC_SUCCESS,client.Disable());
				Assert.AreEqual(1,_helper.CountDelivered);
			}
		}

		[TestMethod]
		public void Helper_XferCountN_EndsAfterN_AndMinusOneAtDone() {
			for(int i=0;i<3;i++) {
				_helper.ListExposures.Add(Exposure(10+i));
			}
			using(FakeRawClient client=OpenHelper()) {
				client.SetCap(Tw.CAP_XFERCOUNT,Tw.TWTY_INT16,2);
				_helper.End=FakeHelperEnd.Never;
				client.Enable();
				Assert.AreEqual(Tw.MSG_XFERREADY,client.WaitForNotification(1000),"N=2 ends by itself after 2 images.");
				TransferAll(client,2);
				Assert.AreEqual(Tw.TWRC_SUCCESS,client.Disable());
				client.SetCap(Tw.CAP_XFERCOUNT,Tw.TWTY_INT16,-1);
				_helper.End=FakeHelperEnd.Done;
				_helper.EndAfterMs=40;
				client.Enable();
				Assert.AreEqual(Tw.MSG_XFERREADY,client.WaitForNotification(1000),"-1 ends at Done with every image.");
				TransferAll(client,3);
				Assert.AreEqual(Tw.TWRC_SUCCESS,client.Disable());
			}
		}

		[TestMethod]
		public void Helper_ModeMulti_IgnoresXferCountOne() {
			_helper.Mode=FakeHelperMode.Multi;
			for(int i=0;i<3;i++) {
				_helper.ListExposures.Add(Exposure(10+i));
			}
			using(FakeRawClient client=OpenHelper()) {
				client.SetCap(Tw.CAP_XFERCOUNT,Tw.TWTY_INT16,1);
				client.Enable();
				Assert.AreEqual(Tw.MSG_XFERREADY,client.WaitForNotification(1000));
				TransferAll(client,3);
			}
		}

		[TestMethod]
		public void Helper_ModeSingle_OneImageWhateverTheCount() {
			_helper.Mode=FakeHelperMode.Single;
			_helper.ListExposures.Add(Exposure(10));
			_helper.ListExposures.Add(Exposure(11));
			using(FakeRawClient client=OpenHelper()) {
				client.Enable();
				Assert.AreEqual(Tw.MSG_XFERREADY,client.WaitForNotification(1000));
				TransferAll(client,1);
			}
		}

		[TestMethod]
		public void Helper_UnreadableImage_EndXferReturnsZero_RestNotDelivered() {
			for(int i=0;i<3;i++) {
				_helper.ListExposures.Add(Exposure(10+i));
			}
			_helper.FailReadAtImage=2;
			using(FakeRawClient client=OpenHelper()) {
				client.Enable();
				Assert.AreEqual(Tw.MSG_XFERREADY,client.WaitForNotification(1000));
				short count;
				client.Pending(Tw.MSG_GET,out count);
				Assert.AreEqual(3,count);
				TransferAll(client,1);//MSG_ENDXFER returns 0 after the first image, as at the end of the batch.
				Assert.AreEqual(5,_helper.State);
				Assert.AreEqual(1,_helper.CountDelivered);
				Assert.AreEqual(2,_helper.CountKeptForRedelivery);
				Assert.AreEqual(Tw.TWRC_SUCCESS,client.Disable());
			}
		}

		[TestMethod]
		public void Helper_FailWithImages_XferReady_NoEndReason() {
			_helper.ListExposures.Add(Exposure(10));
			_helper.End=FakeHelperEnd.Fail;
			_helper.EndCode=FakeHelperSource.CodeSoftwareFailed;
			using(FakeRawClient client=OpenHelper()) {
				client.Enable();
				Assert.AreEqual(Tw.MSG_XFERREADY,client.WaitForNotification(1000));
				Assert.AreEqual(Tw.TWCC_SUCCESS,client.Status(),"An end reason follows only a MSG_CLOSEDSREQ (section 8).");
			}
		}

		[TestMethod]
		public void Helper_ClosedWithoutImage_CloseRequestWithSuccessCode() {
			using(FakeRawClient client=OpenHelper()) {
				client.Enable();
				Assert.AreEqual(Tw.MSG_CLOSEDSREQ,client.WaitForNotification(1000));
				Assert.AreEqual(Tw.TWCC_SUCCESS,client.Status(),"A plain cancel keeps TWCC_SUCCESS.");
			}
		}

		[TestMethod]
		public void Helper_EnableFails_OperationErrorStaysInState4() {
			_helper.EnableFails=true;
			using(FakeRawClient client=OpenHelper()) {
				Assert.AreEqual(Tw.TWRC_FAILURE,client.Enable());
				Assert.AreEqual(Tw.TWCC_OPERATIONERROR,client.Status());
				Assert.AreEqual(4,_helper.State);
			}
		}

		[TestMethod]
		public void Helper_MemoryTransfer_TopDownUnpaddedStrips() {
			_helper.ListExposures.Add(FakePage.CreateGray8(13,9,FakeHelperSource.Dpi,(x,y) => (byte)(x+y*13)));
			using(FakeRawClient client=OpenHelper()) {
				client.SetCap(Tw.ICAP_XFERMECH,Tw.TWTY_UINT16,Tw.TWSX_MEMORY);
				client.Enable();
				Assert.AreEqual(Tw.MSG_XFERREADY,client.WaitForNotification(1000));
				TW_IMAGEMEMXFER xfer;
				byte[] strip;
				Assert.AreEqual(Tw.TWRC_FAILURE,client.MemoryTransfer(12,out xfer,out strip));
				Assert.AreEqual(Tw.TWCC_BADVALUE,client.Status(),"A buffer smaller than one row.");
				List<byte> listBytes=new List<byte>();
				ushort rc;
				do {
					rc=client.MemoryTransfer(13*4,out xfer,out strip);
					Assert.AreEqual(13u,xfer.BytesPerRow,"No row padding.");
					Assert.AreEqual((uint)listBytes.Count/13,xfer.YOffset);
					listBytes.AddRange(strip);
				}
				while(rc==Tw.TWRC_SUCCESS);
				Assert.AreEqual(Tw.TWRC_XFERDONE,rc);
				for(int i=0;i<listBytes.Count;i++) {
					Assert.AreEqual((byte)i,listBytes[i],"top-down byte "+i);
				}
				Assert.AreEqual(0,client.EndTransfer());
			}
		}
		#endregion Sessions (section 5)

		#region Disable without safe disable (section 7) and the strict generic profile
		[TestMethod]
		public void Helper_DisableWithoutSafeDisable_AcceptedInStates5To7() {
			_helper.ListExposures.Add(Exposure(10));
			_helper.ListExposures.Add(Exposure(11));
			using(FakeRawClient client=OpenHelper()) {
				_helper.End=FakeHelperEnd.Never;
				client.Enable();
				Assert.AreEqual(Tw.TWRC_SUCCESS,client.Disable(),"State 5, helper running.");
				Assert.AreEqual(2,_helper.CountLostForHost,"Images not transferred are not delivered after a disable.");
				_helper.End=FakeHelperEnd.Done;
				client.Enable();
				Assert.AreEqual(Tw.MSG_XFERREADY,client.WaitForNotification(1000));
				Assert.AreEqual(Tw.TWRC_SUCCESS,client.Disable(),"State 6.");
				client.Enable();
				Assert.AreEqual(Tw.MSG_XFERREADY,client.WaitForNotification(1000));
				byte[] dib;
				IntPtr hDib;
				client.NativeTransfer(out dib,out hDib);
				Assert.AreEqual(7,_helper.State);
				Assert.AreEqual(Tw.TWRC_SUCCESS,client.Disable(),"State 7.");
				CollectionAssert.AreEqual(new List<int> { 5,6,7 },_fake.ListDisableStates);
			}
		}

		[TestMethod]
		public void StrictGeneric_DisableOutsideState5_SeqError() {
			_fake.IsStrict=true;
			_fake.ListPages.Add(FakePage.CreateGray8(4,4,300,(x,y) => 0));
			using(FakeRawClient client=new FakeRawClient(_fake,"Fake Sensor")) {
				Assert.AreEqual(Tw.TWRC_SUCCESS,client.Enable());
				Assert.AreEqual(Tw.MSG_XFERREADY,client.ProcessEvent());
				Assert.AreEqual(Tw.TWRC_FAILURE,client.Disable(),"MSG_DISABLEDS is valid in state 5 only.");
				Assert.AreEqual(Tw.TWCC_SEQERROR,client.Status());
				short count;
				client.Pending(Tw.MSG_RESET,out count);
				Assert.AreEqual(Tw.TWRC_SUCCESS,client.Disable());
				CollectionAssert.AreEqual(new List<int> { 6,5 },_fake.ListDisableStates);
			}
		}

		[TestMethod]
		public void LenientGeneric_DisableInState6_Accepted() {
			_fake.ListPages.Add(FakePage.CreateGray8(4,4,300,(x,y) => 0));
			using(FakeRawClient client=new FakeRawClient(_fake,"Fake Sensor")) {
				client.Enable();
				Assert.AreEqual(Tw.MSG_XFERREADY,client.ProcessEvent());
				Assert.AreEqual(Tw.TWRC_SUCCESS,client.Disable());
			}
		}
		#endregion Disable without safe disable (section 7) and the strict generic profile

		#region Safe disable (section 10.2)
		//The table of twain-contract 1.1 (main 2c96a4d) section 10.2, row by row.
		private void EnableWithSafeDisable(FakeRawClient client) {
			Assert.AreEqual(Tw.TWRC_SUCCESS,client.SetCap(FakeHelperSource.CapSafeDisable,Tw.TWTY_BOOL,1));
			Assert.AreEqual(Tw.TWRC_SUCCESS,client.Enable());
		}

		[TestMethod]
		public void SafeDisable_HelperRunning_RefusedClosingAtOnce_ThenXferReadyWithItsImages() {
			_helper.ListExposures.Add(Exposure(10));
			_helper.End=FakeHelperEnd.Never;
			using(FakeRawClient client=OpenHelper()) {
				EnableWithSafeDisable(client);
				long start=_fake.NowMs;
				Assert.AreEqual(Tw.TWRC_FAILURE,client.Disable());
				Assert.IsTrue(_fake.NowMs-start<100,"The refusal comes at once.");
				Assert.AreEqual(FakeHelperSource.CodeDisableClosing,client.Status());
				Assert.AreEqual(5,_helper.State,"It stays in state 5.");
				Assert.AreEqual(Tw.MSG_XFERREADY,client.WaitForNotification(1000),"The helper exits, then its image is offered.");
				TransferAll(client,1);
				Assert.AreEqual(Tw.TWRC_SUCCESS,client.Disable(),"Nothing running and nothing undelivered.");
				Assert.AreEqual(1,_helper.CountDelivered);
			}
		}

		[TestMethod]
		public void SafeDisable_HelperRunningWithoutImage_RefusedThenCloseRequest() {
			_helper.End=FakeHelperEnd.Never;
			using(FakeRawClient client=OpenHelper()) {
				EnableWithSafeDisable(client);
				Assert.AreEqual(Tw.TWRC_FAILURE,client.Disable());
				Assert.AreEqual(FakeHelperSource.CodeDisableClosing,client.Status());
				Assert.AreEqual(Tw.MSG_CLOSEDSREQ,client.WaitForNotification(1000));
				Assert.AreEqual(Tw.TWCC_SUCCESS,client.Status());
				Assert.AreEqual(Tw.TWRC_SUCCESS,client.Disable());
			}
		}

		[TestMethod]
		public void SafeDisable_EndedButNotificationNotSent_RefusedClosing_NotificationFollows() {
			_helper.ListExposures.Add(Exposure(10));
			using(FakeRawClient client=OpenHelper()) {
				client.SetCap(Tw.CAP_XFERCOUNT,Tw.TWTY_INT16,1);
				EnableWithSafeDisable(client);//The session ends at once after its one image; the notification is not read yet.
				Assert.AreEqual(Tw.TWRC_FAILURE,client.Disable());
				Assert.AreEqual(FakeHelperSource.CodeDisableClosing,client.Status());
				Assert.AreEqual(Tw.MSG_XFERREADY,client.ProcessEvent());
			}
		}

		[TestMethod]
		public void SafeDisable_State6_RefusedImagesPending_AndXferReadyAgain() {
			_helper.ListExposures.Add(Exposure(10));
			_helper.ListExposures.Add(Exposure(11));
			using(FakeRawClient client=OpenHelper()) {
				EnableWithSafeDisable(client);
				Assert.AreEqual(Tw.MSG_XFERREADY,client.WaitForNotification(1000));
				Assert.AreEqual(Tw.TWRC_FAILURE,client.Disable());
				Assert.AreEqual(FakeHelperSource.CodeDisableImagesPending,client.Status());
				Assert.AreEqual(6,_helper.State);
				Assert.AreEqual(Tw.MSG_XFERREADY,client.ProcessEvent(),"The images are offered again.");
				TransferAll(client,2);
				Assert.AreEqual(Tw.TWRC_SUCCESS,client.Disable());
			}
		}

		[TestMethod]
		public void SafeDisable_RetryWhileHelperRunning_RefusedClosingAtOnceEveryTime() {
			_helper.ListExposures.Add(Exposure(10));
			_helper.End=FakeHelperEnd.Never;
			_helper.IgnoresCloseRequest=true;
			using(FakeRawClient client=OpenHelper()) {
				EnableWithSafeDisable(client);
				long start=_fake.NowMs;
				for(int i=0;i<3;i++) {
					Assert.AreEqual(Tw.TWRC_FAILURE,client.Disable(),"disable "+i);
					Assert.AreEqual(FakeHelperSource.CodeDisableClosing,client.Status(),"Refused the same way until the notification: disable "+i);
				}
				Assert.IsTrue(_fake.NowMs-start<100,"Every refusal comes at once.");
				Assert.AreEqual(5,_helper.State);
				Assert.IsTrue(_helper.IsHelperRunning);
				Assert.AreEqual(1,_helper.CountUndelivered,"No way out while the helper runs.");
				Assert.AreEqual(0,_helper.CountKeptForRedelivery);
				Assert.AreEqual(Tw.MSG_XFERREADY,client.WaitForNotification(2000),"Ended at ForcedEndMs; its image is offered.");
				TransferAll(client,1);
				Assert.AreEqual(Tw.TWRC_SUCCESS,client.Disable());
				Assert.AreEqual(1,_helper.CountDelivered);
			}
		}

		[TestMethod]
		public void SafeDisable_RetryAfterHelperEndedBeforeNotification_RefusedClosing() {
			_helper.ListExposures.Add(Exposure(10));
			using(FakeRawClient client=OpenHelper()) {
				client.SetCap(Tw.CAP_XFERCOUNT,Tw.TWTY_INT16,1);
				EnableWithSafeDisable(client);//The session ends at once after its one image; the notification is not read yet.
				Assert.AreEqual(Tw.TWRC_FAILURE,client.Disable());
				Assert.AreEqual(FakeHelperSource.CodeDisableClosing,client.Status());
				Assert.IsFalse(_helper.IsHelperRunning);
				Assert.AreEqual(Tw.TWRC_FAILURE,client.Disable(),"Not the way out: the notification is not sent yet.");
				Assert.AreEqual(FakeHelperSource.CodeDisableClosing,client.Status());
				Assert.AreEqual(5,_helper.State);
				Assert.AreEqual(0,_helper.CountKeptForRedelivery);
				Assert.AreEqual(Tw.MSG_XFERREADY,client.ProcessEvent());
				TransferAll(client,1);
				Assert.AreEqual(Tw.TWRC_SUCCESS,client.Disable());
				Assert.AreEqual(1,_helper.CountDelivered);
			}
		}

		[TestMethod]
		public void SafeDisable_WayOut_SucceedsOnlyAfterTheNotification_ImagesKeptForRedelivery() {
			_helper.ListExposures.Add(Exposure(10));
			_helper.End=FakeHelperEnd.Never;
			_helper.IgnoresCloseRequest=true;
			_helper.ForcedEndMs=150;
			using(FakeRawClient client=OpenHelper()) {
				EnableWithSafeDisable(client);
				Assert.AreEqual(Tw.TWRC_FAILURE,client.Disable());
				Assert.AreEqual(Tw.TWRC_FAILURE,client.Disable(),"No way out while the helper runs.");
				Assert.AreEqual(FakeHelperSource.CodeDisableClosing,client.Status());
				Assert.AreEqual(Tw.MSG_XFERREADY,client.WaitForNotification(2000));
				Assert.AreEqual(6,_helper.State);
				Assert.AreEqual(Tw.TWRC_SUCCESS,client.Disable(),"Refused before, no MSG_ENDXFER since, the notification sent: the way out.");
				Assert.AreEqual(4,_helper.State);
				Assert.AreEqual(1,_helper.CountKeptForRedelivery);
				Assert.AreEqual(1,_helper.CountRedeliverable,"A redelivery can still offer it.");
				Assert.AreEqual(0,_helper.CountLostForHost);
				Assert.AreEqual(0,_helper.CountDelivered);
			}
		}

		[TestMethod]
		public void SafeDisable_WayOut_AfterAnImagesPendingRefusal_TheRepeatedOfferIsNotTheNotification() {
			_helper.ListExposures.Add(Exposure(10));
			_helper.ListExposures.Add(Exposure(11));
			using(FakeRawClient client=OpenHelper()) {
				EnableWithSafeDisable(client);
				Assert.AreEqual(Tw.MSG_XFERREADY,client.WaitForNotification(1000));
				Assert.AreEqual(Tw.TWRC_FAILURE,client.Disable());
				Assert.AreEqual(FakeHelperSource.CodeDisableImagesPending,client.Status());
				Assert.AreEqual(Tw.TWRC_SUCCESS,client.Disable(),"The MSG_XFERREADY sent again is still pending, yet this is the way out.");
				Assert.AreEqual(4,_helper.State);
				Assert.AreEqual(2,_helper.CountKeptForRedelivery);
				Assert.AreEqual(0,_helper.CountLostForHost);
			}
		}

		[TestMethod]
		public void SafeDisable_State7Unfinished_ResetToState6_RefusedImagesPending_OfferedAgainFromTheStart() {
			_helper.ListExposures.Add(FakePage.CreateGray8(13,9,FakeHelperSource.Dpi,(x,y) => (byte)(x+y*13)));
			using(FakeRawClient client=OpenHelper()) {
				client.SetCap(Tw.ICAP_XFERMECH,Tw.TWTY_UINT16,Tw.TWSX_MEMORY);
				EnableWithSafeDisable(client);
				Assert.AreEqual(Tw.MSG_XFERREADY,client.WaitForNotification(1000));
				TW_IMAGEMEMXFER xfer;
				byte[] strip;
				Assert.AreEqual(Tw.TWRC_SUCCESS,client.MemoryTransfer(13*4,out xfer,out strip),"The first strip of several.");
				Assert.AreEqual(7,_helper.State);
				Assert.AreEqual(Tw.TWRC_FAILURE,client.Disable());
				Assert.AreEqual(FakeHelperSource.CodeDisableImagesPending,client.Status());
				Assert.AreEqual(6,_helper.State,"The unfinished transfer is reset.");
				Assert.AreEqual(Tw.MSG_XFERREADY,client.ProcessEvent(),"The image is offered again.");
				List<byte> listBytes=new List<byte>();
				ushort rc;
				do {
					rc=client.MemoryTransfer(13*4,out xfer,out strip);
					Assert.AreEqual((uint)listBytes.Count/13,xfer.YOffset,"From the first row again.");
					listBytes.AddRange(strip);
				}
				while(rc==Tw.TWRC_SUCCESS);
				Assert.AreEqual(Tw.TWRC_XFERDONE,rc);
				Assert.AreEqual(13*9,listBytes.Count);
				for(int i=0;i<listBytes.Count;i++) {
					Assert.AreEqual((byte)i,listBytes[i],"byte "+i);
				}
				Assert.AreEqual(0,client.EndTransfer());
				Assert.AreEqual(Tw.TWRC_SUCCESS,client.Disable());
				Assert.AreEqual(1,_helper.CountDelivered);
				Assert.AreEqual(0,_helper.CountKeptForRedelivery);
			}
		}

		[TestMethod]
		public void SafeDisable_State7AfterXferDone_SeqError_NotTheWayOut_NeverOfferedTwice() {
			_helper.ListExposures.Add(Exposure(10));
			_helper.ListExposures.Add(Exposure(11));
			using(FakeRawClient client=OpenHelper()) {
				EnableWithSafeDisable(client);
				Assert.AreEqual(Tw.MSG_XFERREADY,client.WaitForNotification(1000));
				Assert.AreEqual(Tw.TWRC_FAILURE,client.Disable(),"A refused disable first.");
				Assert.AreEqual(FakeHelperSource.CodeDisableImagesPending,client.Status());
				Assert.AreEqual(Tw.MSG_XFERREADY,client.ProcessEvent());
				byte[] dib;
				IntPtr hDib;
				Assert.AreEqual(Tw.TWRC_XFERDONE,client.NativeTransfer(out dib,out hDib));
				Assert.AreEqual(Tw.TWRC_FAILURE,client.Disable(),"Not the way out although the previous disable was refused.");
				Assert.AreEqual(Tw.TWCC_SEQERROR,client.Status());
				Assert.AreEqual(7,_helper.State,"Nothing changes.");
				Assert.AreEqual(Tw.MSG_NULL,client.ProcessEvent(),"Not offered again.");
				Assert.AreEqual(1,client.EndTransfer());
				Assert.AreEqual(Tw.TWRC_FAILURE,client.Disable(),"MSG_ENDXFER came since: refused, one image is pending.");
				Assert.AreEqual(FakeHelperSource.CodeDisableImagesPending,client.Status());
				Assert.AreEqual(Tw.MSG_XFERREADY,client.ProcessEvent());
				TransferAll(client,1);
				Assert.AreEqual(Tw.TWRC_SUCCESS,client.Disable());
				Assert.AreEqual(2,_helper.CountDelivered);
				Assert.AreEqual(2,_helper.ListDeliveredCaptureIds.Count);
				Assert.AreEqual(0,_helper.CountKeptForRedelivery,"Each image delivered once, none kept.");
			}
		}

		[TestMethod]
		public void SafeDisable_EndXferBetweenDisables_NotConsecutive() {
			_helper.ListExposures.Add(Exposure(10));
			_helper.ListExposures.Add(Exposure(11));
			using(FakeRawClient client=OpenHelper()) {
				EnableWithSafeDisable(client);
				Assert.AreEqual(Tw.MSG_XFERREADY,client.WaitForNotification(1000));
				Assert.AreEqual(Tw.TWRC_FAILURE,client.Disable());
				client.ProcessEvent();
				byte[] dib;
				IntPtr hDib;
				client.NativeTransfer(out dib,out hDib);
				client.EndTransfer();
				Assert.AreEqual(Tw.TWRC_FAILURE,client.Disable(),"MSG_ENDXFER came in between: refused again, one image is pending.");
				Assert.AreEqual(FakeHelperSource.CodeDisableImagesPending,client.Status());
			}
		}

		[TestMethod]
		public void SafeDisable_NothingRunningNothingUndelivered_SucceedsAtOnce() {
			_helper.ListExposures.Add(Exposure(10));
			using(FakeRawClient client=OpenHelper()) {
				client.SetCap(Tw.CAP_XFERCOUNT,Tw.TWTY_INT16,1);
				EnableWithSafeDisable(client);
				Assert.AreEqual(Tw.MSG_XFERREADY,client.WaitForNotification(1000));
				TransferAll(client,1);
				Assert.AreEqual(Tw.TWRC_SUCCESS,client.Disable());
			}
		}

		[TestMethod]
		public void SafeDisable_HelperNeverExits_EndedAtForcedEnd_SavedImagesStillOffered() {
			_helper.ListExposures.Add(Exposure(10));
			_helper.End=FakeHelperEnd.Never;
			_helper.IgnoresCloseRequest=true;
			_helper.ForcedEndMs=150;
			using(FakeRawClient client=OpenHelper()) {
				EnableWithSafeDisable(client);
				long start=_fake.NowMs;
				Assert.AreEqual(Tw.TWRC_FAILURE,client.Disable());
				Assert.AreEqual(Tw.MSG_XFERREADY,client.WaitForNotification(2000));
				Assert.IsTrue(_fake.NowMs-start>=140,"Offered only after the forced end.");
				TransferAll(client,1);
				Assert.AreEqual(Tw.TWRC_SUCCESS,client.Disable());
			}
		}
		#endregion Safe disable (section 10.2)

		#region End reasons and text (sections 8 and 11.1)
		[TestMethod]
		public void EndReason_KeptUntilDisableSucceeds_AfterAnyFailedCallsCode() {
			_helper.End=FakeHelperEnd.Fail;
			_helper.EndCode=FakeHelperSource.CodeNoSensor;
			using(FakeRawClient client=OpenHelper()) {
				client.Enable();
				Assert.AreEqual(Tw.MSG_CLOSEDSREQ,client.WaitForNotification(1000));
				Assert.AreEqual(FakeHelperSource.CodeNoSensor,client.Status());
				Assert.AreEqual(FakeHelperSource.CodeNoSensor,client.Status(),"The end reason is kept.");
				int value;
				Assert.AreEqual(Tw.TWRC_FAILURE,client.GetCap(Tw.CAP_DUPLEXENABLED,Tw.MSG_GETCURRENT,out value));
				Assert.AreEqual(Tw.TWCC_CAPUNSUPPORTED,client.Status(),"A failed call's code comes first and is cleared by the read.");
				Assert.AreEqual(FakeHelperSource.CodeNoSensor,client.Status());
				Assert.AreEqual(Tw.TWRC_SUCCESS,client.Disable());
				Assert.AreEqual(Tw.TWCC_SUCCESS,client.Status(),"A successful disable clears it.");
				_helper.End=FakeHelperEnd.Never;
				client.Enable();
				Assert.AreEqual(Tw.TWCC_SUCCESS,client.Status(),"A new enable starts without one.");
			}
		}

		[TestMethod]
		public void StatusText_ForEveryEndReason_Utf8WithSizeIncludingNul() {
			_fake.SupplyEntryPoints=true;
			using(FakeRawClient client=OpenHelper()) {
				foreach(ushort code in new[] { FakeHelperSource.CodeNoCalibration,FakeHelperSource.CodeNoSensor,FakeHelperSource.CodeDeviceNotAccepted,
					FakeHelperSource.CodeSessionTimeout,FakeHelperSource.CodeSoftwareFailed })
				{
					string text;
					uint size;
					Assert.AreEqual(Tw.TWRC_SUCCESS,client.StatusText(code,out text,out size));
					Assert.AreEqual(FakeHelperSource.GetText(code),text);
					Assert.AreEqual((uint)System.Text.Encoding.UTF8.GetByteCount(text)+1,size);
				}
				Assert.AreEqual(5,_fake.DsmMemory.CountFree,"Each text handle came from DSM_MemAllocate (section 6.4).");
				Assert.AreEqual(0,_fake.DsmMemory.CountForeignFree);
			}
		}

		[TestMethod]
		public void StatusText_Unsupported_BadProtocol() {
			_helper.SupportsStatusUtf8=false;
			using(FakeRawClient client=OpenHelper()) {
				string text;
				uint size;
				Assert.AreEqual(Tw.TWRC_FAILURE,client.StatusText(FakeHelperSource.CodeNoSensor,out text,out size));
				Assert.AreEqual(Tw.TWCC_BADPROTOCOL,client.Status());
			}
		}

		[TestMethod]
		public void Dsm2_NativeDibComesFromDsmMemAllocate() {
			_fake.SupplyEntryPoints=true;
			_helper.ListExposures.Add(Exposure(10));
			using(FakeRawClient client=OpenHelper()) {
				client.Enable();
				Assert.AreEqual(Tw.MSG_XFERREADY,client.WaitForNotification(1000));
				byte[] dib;
				IntPtr hDib;
				Assert.AreEqual(Tw.TWRC_XFERDONE,client.NativeTransfer(out dib,out hDib));
				Assert.IsTrue(_fake.DsmMemory.ListFreed.Contains(hDib));
				DibImage image=DibImage.FromPackedDib(dib);
				Assert.AreEqual(8,image.BitCount);
				Assert.AreEqual(10,image.Width);
				Assert.AreEqual(FakeHelperSource.Dpi,image.XResolutionDpi);
			}
		}
		#endregion End reasons and text (sections 8 and 11.1)
	}
}
