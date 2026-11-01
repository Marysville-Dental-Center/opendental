// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ImagingCapture.Recovery;
using ImagingCapture.Save;
using ImagingCapture.Session;
using ImagingDeviceManager.Capture;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

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

		[TestMethod]
		public void Continuous_FullMountStop_ADrainedImage_QuarantinedWithNoSlotOfItsOwn_AssignFilesItWithoutTheEnablesSlotTeethOrRotation() {
			CaptureControllerHarness harness=new CaptureControllerHarness(3);
			harness.Setup.Options.SessionMode=TwainSessionMode.Continuous;
			harness.Started();
			harness.Status(TwainCaptureState.WaitingForImage,isSafeDisableInEffect:true);
			harness.Image();
			harness.Image();
			harness.Image();
			CollectionAssert.AreEqual(new[] { TwainCaptureStopReason.End },harness.Twain.ListDisables,"The full mount disables the continuous enable.");
			//An exposure taken after the last slot, transferred during that safe stop.
			AcquiredImage image=harness.Image(AcquiredImageKind.Drained,TwainCaptureStopReason.End);
			Assert.AreEqual(3,harness.RadiographStore.ListDocumentsUpdated.Count);
			FakeQuarantineCall call=harness.Twain.ListQuarantined.Single();
			Assert.AreEqual("Every slot of the mount already holds an image.",call.Reason);
			Dictionary<string,string> dictImage=ToDictionary(call.Image.SidecarFields);
			Assert.IsFalse(dictImage.ContainsKey(CaptureSidecarKeys.SlotOfImage),"The session names no slot as this image's own.");
			Assert.AreEqual("101",dictImage[CaptureSidecarKeys.MountItemNum],"Only its enable's slot, which is not the image's.");
			using(RecoveryHarness recovery=new RecoveryHarness()) {
				//Slot 1 is empty in the store (a Retake moved its image), with a rotation of its own.
				recovery.Slot(1).RotateOnAcquire=90;
				//The quarantine sidecar as the library writes it: the session's fields and the image's own.
				Dictionary<string,string> dictFile=RecoveryFiles.SessionFields();
				foreach(KeyValuePair<string,string> pair in call.Image.SidecarFields) {
					dictFile[pair.Key]=pair.Value;
				}
				dictFile["Slot"]=call.Image.TargetLabel;
				dictFile["Target"]=call.Image.TargetLabel;
				dictFile["ImageKind"]=image.Kind.ToString();
				dictFile[CaptureSidecarKeys.IndexInEnable]=image.IndexInEnable.ToString(CultureInfo.InvariantCulture);
				dictFile[CaptureSidecarKeys.ContinuousEnable]=image.IsContinuousEnable ? "1" : "0";
				CaptureFileEntry entry=recovery.Files.Quarantine(dictFile,new DateTime(2026,10,31,10,5,0));
				Assert.AreEqual(4,entry.IndexInEnable);
				Assert.IsTrue(entry.IsContinuousEnable);
				Assert.IsFalse(entry.IsSlotKnown);
				string error;
				CaptureAssignPlan plan=recovery.Filer.PlanAssign(entry,0,out error);
				Assert.IsNotNull(plan,error);
				Assert.IsFalse(plan.IsIntoOriginalSlot,"Not into slot 1, although it is empty.");
				Assert.AreEqual(CaptureRecoveryFiler.SlotUnknownNote,plan.Note);
				CaptureFilingResult result=recovery.Filer.Assign(entry,0,0);
				Assert.IsTrue(result.IsDone,result.Error);
				Assert.IsTrue(result.IsMountItemCreated);
				Document document=recovery.RadiographStore.ListDocumentsUpdated.Single();
				Assert.AreNotEqual(101,document.MountItemNum);
				Assert.AreEqual("",document.ToothNumbers,"Not slot 1's teeth...");
				Assert.AreEqual(0,document.DegreesRotated,"...nor its rotation.");
			}
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
		public void OnCaptureFiled_TheLastEmptySlots_TheMountIsFull_TheSessionFinishesWithoutOpeningTheSensorSoftware() {
			CaptureControllerHarness harness=new CaptureControllerHarness(3);
			harness.Controller.Begin();
			Assert.IsTrue(harness.Controller.OnCaptureFiled(Filed(harness,1,900)));
			Assert.IsTrue(harness.Controller.OnCaptureFiled(Filed(harness,2,901)));
			Assert.AreEqual(CaptureSessionPhase.Ready,harness.Controller.Phase,"One slot is still empty.");
			Assert.IsTrue(harness.Controller.OnCaptureFiled(Filed(harness,3,902)));
			Assert.AreEqual(CaptureSessionPhase.Finished,harness.Controller.Phase,"Design 7.3 step 6.");
			Assert.AreEqual(CaptureSessionEndReason.MountFull,harness.Controller.Summary.EndReason);
			Assert.AreEqual(0,harness.ListSessions.Count,"The sensor software was never opened.");
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
