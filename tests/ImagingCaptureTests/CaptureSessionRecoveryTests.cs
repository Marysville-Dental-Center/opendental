// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using ImagingCapture.Recovery;
using ImagingCapture.Save;
using ImagingCapture.Session;
using ImagingDeviceManager.Capture;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace ImagingCapture.Tests {
	///<summary>The capture session's part in the quarantine (design 7.12): every enable names its mount, slot, teeth, category, provider and device
	///in the sidecars, and a capture filed from the quarantine window into this session's mount updates the mini-map and the target.</summary>
	[TestClass]
	public class CaptureSessionRecoveryTests {
		private static Dictionary<string,string> ToDictionary(List<KeyValuePair<string,string>> listFields) {
			return listFields.ToDictionary(x => x.Key,x => x.Value);
		}

		[TestMethod]
		public void Enable_SidecarFields_NameTheMountSlotCategoryProviderAndDevice() {
			CaptureControllerHarness harness=new CaptureControllerHarness(3);
			harness.Started();
			Dictionary<string,string> dictFields=ToDictionary(harness.Twain.LastRequest.SidecarFields);
			Assert.AreEqual("7",dictFields[CaptureSidecarKeys.MountNum]);
			Assert.AreEqual("101",dictFields[CaptureSidecarKeys.MountItemNum]);
			Assert.AreEqual("1",dictFields[CaptureSidecarKeys.SlotItemOrder]);
			Assert.AreEqual("1,2,3",dictFields[CaptureSidecarKeys.ToothNumbers]);
			Assert.AreEqual("20",dictFields[CaptureSidecarKeys.DocCategory]);
			Assert.AreEqual("4",dictFields[CaptureSidecarKeys.ProvNum]);
			Assert.AreEqual("12",dictFields[CaptureSidecarKeys.ImagingDeviceNum]);
		}

		[TestMethod]
		public void Enable_SingleTarget_SidecarFieldsHaveTheBannersTeethAndNoMount() {
			CaptureControllerHarness harness=new CaptureControllerHarness(0);
			harness.Controller.Begin();
			string error;
			Assert.IsTrue(harness.Controller.SetToothNumbers("8,9",out error),error);
			harness.Arm();
			Assert.IsTrue(harness.Start(),harness.Controller.IndicatorText);
			Dictionary<string,string> dictFields=ToDictionary(harness.Twain.LastRequest.SidecarFields);
			Assert.AreEqual("",dictFields[CaptureSidecarKeys.MountNum]);
			Assert.AreEqual("",dictFields[CaptureSidecarKeys.MountItemNum]);
			Assert.AreEqual("8,9",dictFields[CaptureSidecarKeys.ToothNumbers]);
		}

		private static CaptureFilingResult Filed(CaptureControllerHarness harness,int idxSlot,long docNum,long patNum=CaptureControllerHarness.PatNum) {
			return new CaptureFilingResult {
				Action=CaptureRecoveryAction.Assign,IsDone=true,PatNum=patNum,Document=new Document { DocNum=docNum,PatNum=patNum },Mount=harness.Setup.Mount,
				MountItem=harness.Setup.MountSlots.ListMountItems[idxSlot],
			};
		}

		[TestMethod]
		public void OnCaptureFiled_AnotherSlot_FilledThere_TheTargetStays() {
			CaptureControllerHarness harness=new CaptureControllerHarness(3);
			harness.Controller.Begin();
			Assert.AreEqual(1,harness.Controller.MountSlots.IdxTarget);
			int version=harness.Controller.Version;
			Assert.IsTrue(harness.Controller.OnCaptureFiled(Filed(harness,2,900)));
			Assert.AreEqual(900,harness.Controller.MountSlots.ListDocuments[2].DocNum);
			Assert.AreEqual(1,harness.Controller.MountSlots.IdxTarget,"The target stays.");
			Assert.IsTrue(harness.Controller.Version>version,"Changed was raised.");
			Assert.IsFalse(harness.Controller.IsMountEmpty);
			Assert.IsTrue(harness.Controller.GetSlotViews().Single(x => x.MountItem.MountItemNum==102).IsFilled);
		}

		[TestMethod]
		public void OnCaptureFiled_TheTarget_TheTargetMovesOn_AndTheSessionNeverPlacesIntoIt() {
			CaptureControllerHarness harness=new CaptureControllerHarness(3);
			harness.Controller.Begin();
			Assert.IsTrue(harness.Controller.OnCaptureFiled(Filed(harness,1,900)));
			Assert.AreEqual(2,harness.Controller.MountSlots.IdxTarget);
			harness.Arm();
			Assert.IsTrue(harness.Start());
			Assert.AreEqual("102",ToDictionary(harness.Twain.LastRequest.SidecarFields)[CaptureSidecarKeys.MountItemNum],"The next enable is for the next slot.");
			Assert.IsFalse(harness.Controller.OnCaptureFiled(Filed(harness,1,901)),"A slot that holds an image is left alone.");
		}

		[TestMethod]
		public void OnCaptureFiled_NewUnmountedItem_Appended() {
			CaptureControllerHarness harness=new CaptureControllerHarness(3);
			harness.Controller.Begin();
			MountItem mountItemNew=MountPlacement.CreateUnmountedItem(7);
			mountItemNew.MountItemNum=555;
			CaptureFilingResult result=Filed(harness,1,900);
			result.MountItem=mountItemNew;
			result.IsMountItemCreated=true;
			Assert.IsTrue(harness.Controller.OnCaptureFiled(result));
			Assert.AreEqual(5,harness.Controller.MountSlots.ListMountItems.Count);
			Assert.AreEqual(1,harness.Controller.MountSlots.IdxTarget);
			Assert.IsFalse(harness.Controller.IsMountEmpty);
		}

		[TestMethod]
		public void OnCaptureFiled_AnotherPatientOrMountOrNotDone_Ignored() {
			CaptureControllerHarness harness=new CaptureControllerHarness(3);
			harness.Controller.Begin();
			Assert.IsFalse(harness.Controller.OnCaptureFiled(Filed(harness,2,900,patNum:77)));
			CaptureFilingResult resultOtherMount=Filed(harness,2,900);
			resultOtherMount.Mount=new Mount { MountNum=8,PatNum=CaptureControllerHarness.PatNum };
			Assert.IsFalse(harness.Controller.OnCaptureFiled(resultOtherMount));
			CaptureFilingResult resultRefused=Filed(harness,2,900);
			resultRefused.IsDone=false;
			Assert.IsFalse(harness.Controller.OnCaptureFiled(resultRefused));
			Assert.IsTrue(harness.Controller.IsMountEmpty);
			Assert.IsFalse(new CaptureControllerHarness(0).Controller.OnCaptureFiled(Filed(harness,2,900)),"No mount: nothing to update.");
		}
	}
}
