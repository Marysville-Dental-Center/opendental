// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using ImagingCapture.Save;
using ImagingCapture.Session;
using ImagingDeviceManager.Capture;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace ImagingCapture.Tests.Db {
	///<summary>A whole capture session on the scratch database (CaptureDb) and its A to Z folder, through the hook's path: the launcher on the real
	///stores, the controller's save path (RadiographImport with OdRadiographStore), and stand-ins only for the TWAIN worker and the window (the
	///test raises the worker's events, as CaptureSessionControllerTests does). Each image has its own grey value, so the file in each slot shows
	///which exposure it is.</summary>
	[TestClass]
	public class CaptureSessionSaveDbTests {
		private CaptureSessionDbKit _kit;

		[TestInitialize]
		public void Init() {
			CaptureDb.Begin();
		}

		[TestCleanup]
		public void Cleanup() {
			_kit?.Dispose();
			if(_kit?.WindowRunner.ExceptionThrown!=null) {
				Assert.Fail("The window's script threw: "+_kit.WindowRunner.ExceptionThrown);
			}
		}

		///<summary>The grey value of the stored image of document (its PNG in the A to Z folder), and that it is 6 by 4.</summary>
		private static int GetGrey(Document document) {
			using(Bitmap bitmap=new Bitmap(new MemoryStream(File.ReadAllBytes(CaptureDb.GetDocumentPath(document))))) {
				Assert.AreEqual(6,bitmap.Width);
				Assert.AreEqual(4,bitmap.Height);
				Color color=bitmap.GetPixel(3,2);
				Assert.AreEqual(color.R,color.G,"Grey.");
				return color.R;
			}
		}

		[TestMethod]
		public void FourBw_SlotBySlot_EachExposureInItsSlot_ForThePatient_InTheAtoZFolder_Logged() {
			_kit=new CaptureSessionDbKit("save4bw");
			Patient patientOther=CaptureDb.CreatePatient("SaveOther",_kit.ProvNum);
			Mount mount=_kit.CreateMount4Bw();
			List<AcquiredImage> listImages=new List<AcquiredImage>();
			_kit.WindowRunner.ActionWhileShown=controller => {
				_kit.Start(controller);
				Assert.AreEqual(CaptureSessionPhase.Enabled,controller.Phase);
				Assert.AreEqual(1,_kit.Twain.CountOpen);
				Assert.AreEqual(CaptureDb.TwainName,_kit.Twain.OpenProductName);
				foreach(byte grey in new byte[] { 40,80,120,160 }) {
					listImages.Add(_kit.Expose(grey));
				}
				Assert.AreEqual(CaptureSessionPhase.Finished,controller.Phase,"The mount is full: the session finishes by itself.");
			};
			CaptureHookOutcome outcome=_kit.RunHook(mount,_kit.MountDef4Bw);
			Assert.AreEqual(CaptureSessionLaunchKind.Completed,outcome.Result.Kind,outcome.Result.Reason);
			Assert.AreEqual("",outcome.Result.Reason);
			Assert.IsFalse(outcome.Result.IsEmptyMountToDelete);
			Assert.IsFalse(outcome.IsMountDeleted);
			Assert.AreEqual(4,_kit.Twain.ListEnables.Count,"One enable per slot.");
			Assert.AreEqual(0,_kit.Twain.ListQuarantined.Count,"Nothing went to the quarantine.");
			CaptureSessionSummary summary=outcome.Result.Summary;
			Assert.AreEqual(CaptureSessionEndReason.MountFull,summary.EndReason);
			Assert.AreEqual(4,summary.CountImages);
			Assert.AreEqual(mount.MountNum,summary.MountNum);
			//The documents: the bound patient, one per slot in ItemOrder, the slot's teeth and rotation, the category and the provider.
			List<MountItem> listSlots=MountItems.GetItemsForMount(mount.MountNum);
			Assert.AreEqual(4,listSlots.Count,"No unmounted item was made.");
			List<Document> listDocuments=CaptureDb.GetDocuments(_kit.Patient.PatNum);
			Assert.AreEqual(4,listDocuments.Count);
			CollectionAssert.AreEquivalent(summary.ListDocNums,listDocuments.Select(x => x.DocNum).ToList());
			Document[] arrayDocumentsInSlots=Documents.GetDocumentsForMountItems(listSlots);
			byte[] arrayGreys={ 40,80,120,160 };
			for(int i=0;i<4;i++) {
				Document document=arrayDocumentsInSlots[i];
				Assert.IsNotNull(document,"Slot "+listSlots[i].ItemOrder+" holds a document.");
				Assert.AreEqual(i+1,listSlots[i].ItemOrder);
				Assert.AreEqual(_kit.Patient.PatNum,document.PatNum);
				Assert.AreEqual(listSlots[i].ToothNumbers,document.ToothNumbers);
				Assert.AreEqual(listSlots[i].RotateOnAcquire,(int)document.DegreesRotated);
				Assert.AreEqual(_kit.Category.DefNum,document.DocCategory);
				Assert.AreEqual(_kit.ProvNum,document.ProvNum);
				Assert.AreEqual(ImageType.Radiograph,document.ImgType);
				Assert.IsTrue(document.PrintHeading);
				StringAssert.EndsWith(document.FileName,".png","8-bit: lossless PNG (design 7.8).");
				Assert.AreEqual(listImages[i].ReceivedAt,document.DateCreated,"The receive time (design 10.8).");
				Assert.AreEqual(arrayGreys[i],GetGrey(document),"Exposure "+(i+1)+" is the file in slot "+(i+1)+".");
			}
			CollectionAssert.AreEqual(new[] { "2,3,30,31","4,5,28,29","14,15,18,19","12,13,20,21" },arrayDocumentsInSlots.Select(x => x.ToothNumbers).ToList());
			CollectionAssert.AreEqual(new[] { 0,0,180,180 },arrayDocumentsInSlots.Select(x => (int)x.DegreesRotated).ToList());
			Assert.AreEqual(0,CaptureDb.GetDocuments(patientOther.PatNum).Count,"Never another patient.");
			Assert.AreEqual(_kit.ProvNum,Mounts.GetByNum(mount.MountNum).ProvNum,"Mount.ProvNum (design 10.8).");
			//The security log (design 10.7): per document ImageStore's entry and the capture details with the captureId, both FKey=DocNum; one
			//session summary; no capture event (nothing was lost).
			for(int i=0;i<4;i++) {
				List<SecurityLog> listSecurityLogs=CaptureDb.GetLogsForPatient(EnumPermType.ImageCreate,_kit.Patient.PatNum)
					.Where(x => x.FKey==arrayDocumentsInSlots[i].DocNum).ToList();
				Assert.AreEqual(1,listSecurityLogs.Count(x => x.LogText.StartsWith("Radiograph Created: ")));
				string captureId=listImages[i].MetadataJson.Split('"')[7];
				Assert.AreEqual(1,listSecurityLogs.Count(x => x.LogText.StartsWith(RadiographSavePlanner.CaptureLogPrefix) && x.LogText.Contains("captureId \""+captureId+"\"")),
					"The capture details name the exposure's captureId (design 7.9).");
			}
			List<string> listTexts=CaptureDb.GetLogsForPatient(EnumPermType.ImageCreate,_kit.Patient.PatNum).Select(x => x.LogText).ToList();
			string textSummary=listTexts.Single(x => x.StartsWith("Capture session summary: "));
			Assert.AreEqual(CaptureAuditText.GetSessionSummaryText(summary),textSummary);
			StringAssert.Contains(textSummary,"4 images, 0 retakes, 0 quarantined");
			StringAssert.Contains(textSummary,"mount "+mount.MountNum);
			Assert.IsFalse(listTexts.Any(x => x.StartsWith(CaptureAuditText.CaptureEventPrefix)));
			Assert.IsFalse(listTexts.Any(x => x.StartsWith("Mount Deleted: ")));
		}

		[TestMethod]
		public void FourBw_RotateAlwaysRotateThisSlotAndRetake_TheirRowsAndEntries() {
			_kit=new CaptureSessionDbKit("saveretake");
			Mount mount=_kit.CreateMount4Bw();
			long docNumRetaken=0;
			_kit.WindowRunner.ActionWhileShown=controller => {
				_kit.Start(controller);
				_kit.Expose(40);//slot 1; the enable for slot 2 is posted
				docNumRetaken=controller.LastSavedDocument.DocNum;
				Assert.IsTrue(_kit.Execute(controller,CaptureSessionAction.Rotate));
				Assert.IsTrue(controller.CanAlwaysRotateSlot,"The mount was made from a definition in this call.");
				Assert.IsTrue(controller.AlwaysRotateSlot(),controller.NoticeText);
				Assert.IsTrue(_kit.Execute(controller,CaptureSessionAction.Retake),controller.RedStripText);
				_kit.Status(TwainCaptureState.WaitingForImage);
				_kit.Image(80);//the running enable's image goes to its own slot, 2
				_kit.Ended(TwainCaptureEndReason.Completed);//then the session enables for the retake of slot 1
				_kit.Expose(120);
				_kit.Expose(160);
				_kit.Expose(200);
				Assert.AreEqual(CaptureSessionPhase.Finished,controller.Phase);
			};
			CaptureHookOutcome outcome=_kit.RunHook(mount,_kit.MountDef4Bw);
			Assert.AreEqual(CaptureSessionLaunchKind.Completed,outcome.Result.Kind,outcome.Result.Reason);
			Assert.AreEqual("",outcome.Result.Reason,"The window's script ran to its end.");
			Assert.AreEqual(5,outcome.Result.Summary.CountImages);
			Assert.AreEqual(1,outcome.Result.Summary.CountRetakes);
			//The mount: 4 slots and the retake's unmounted item.
			List<MountItem> listMountItems=MountItems.GetItemsForMount(mount.MountNum);
			Assert.AreEqual(5,listMountItems.Count,string.Join(", ",listMountItems.Select(x => x.MountItemNum+":"+x.ItemOrder)));
			MountItem mountItemUnmounted=listMountItems.Single(x => x.ItemOrder==MountPlacement.ItemOrderUnmounted);
			Document documentRetaken=Documents.GetByNum(docNumRetaken);
			Assert.AreEqual(mountItemUnmounted.MountItemNum,documentRetaken.MountItemNum,"The rejected exposure is kept as an unmounted image.");
			Assert.AreEqual("Retake: Slot 1 (teeth 2,3,30,31)",documentRetaken.Description);
			Assert.AreEqual(90,(int)documentRetaken.DegreesRotated,"Rotated before the retake.");
			Assert.AreEqual(40,GetGrey(documentRetaken));
			List<MountItem> listSlots=listMountItems.Where(x => MountPlacement.IsSlot(x)).OrderBy(x => x.ItemOrder).ToList();
			Document[] arrayDocumentsInSlots=Documents.GetDocumentsForMountItems(listSlots);
			CollectionAssert.AreEqual(new[] { 120,80,160,200 },arrayDocumentsInSlots.Select(x => GetGrey(x)).ToList(),"The retake is in slot 1.");
			Assert.AreEqual(90,(int)arrayDocumentsInSlots[0].DegreesRotated,"\"Always rotate this slot\": the retake comes in turned.");
			Assert.AreEqual(90,listSlots[0].RotateOnAcquire,"This mount's slot 1.");
			//The mount definition's slot 1, read from the database.
			MountItemDef mountItemDef=MountItemDefs.GetForMountDef(_kit.MountDef4Bw.MountDefNum).Single(x => x.ItemOrder==1);
			Assert.AreEqual(90,mountItemDef.RotateOnAcquire);
			//The entries: Rotate and Retake under ImageEdit (FKey=DocNum), "Always rotate this slot" under Setup (FKey=MountDefNum).
			List<SecurityLog> listImageEdit=CaptureDb.GetLogsForPatient(EnumPermType.ImageEdit,_kit.Patient.PatNum);
			Assert.AreEqual(2,listImageEdit.Count);
			Assert.IsTrue(listImageEdit.All(x => x.FKey==docNumRetaken));
			StringAssert.StartsWith(listImageEdit[0].LogText,"Rotate: image DocNum "+docNumRetaken);
			StringAssert.Contains(listImageEdit[0].LogText,"DegreesRotated 0 -> 90");
			Assert.AreEqual(CaptureAuditText.GetRetakeText(docNumRetaken,"Slot 1 (teeth 2,3,30,31)",mount.MountNum,outcome.Result.Summary.SessionGuid),
				listImageEdit[1].LogText);
			List<SecurityLog> listSetup=CaptureDb.GetLogsByFKey(EnumPermType.Setup,_kit.MountDef4Bw.MountDefNum,"Always rotate this slot: ");
			Assert.AreEqual(1,listSetup.Count,CaptureDb.Texts(CaptureDb.GetLogsByFKey(EnumPermType.Setup,_kit.MountDef4Bw.MountDefNum)));
			SecurityLog securityLogSetup=listSetup[0];
			Assert.AreEqual(0,securityLogSetup.PatNum);
			StringAssert.StartsWith(securityLogSetup.LogText,"Always rotate this slot: mount definition \""+_kit.MountDef4Bw.Description+"\"");
			StringAssert.Contains(securityLogSetup.LogText,"RotateOnAcquire 0 -> 90. The slot of mount "+mount.MountNum+": RotateOnAcquire 0 -> 90.");
			StringAssert.Contains(CaptureDb.GetLogsForPatient(EnumPermType.ImageCreate,_kit.Patient.PatNum).Last(x => x.LogText.StartsWith("Capture session summary: ")).LogText,
				"5 images, 1 retake, 0 quarantined");
		}

		[TestMethod]
		public void ExistingMountPartlyFilled_TheNextSessionFillsOnlyItsEmptySlots_FromTheDatabase() {
			_kit=new CaptureSessionDbKit("savetwice");
			Mount mount=_kit.CreateMount4Bw();
			//The first session takes slots 1 and 2, then the operator ends it (twice: safe disable is not in effect) and the source stops.
			_kit.WindowRunner.ActionWhileShown=controller => {
				_kit.Start(controller);
				_kit.Expose(40);
				_kit.Expose(80);
				Assert.IsTrue(_kit.Execute(controller,CaptureSessionAction.End));
				Assert.IsTrue(_kit.Execute(controller,CaptureSessionAction.End),"The second press within 10 s goes ahead (design 7.6).");
				_kit.Ended(TwainCaptureEndReason.Stopped);
			};
			CaptureHookOutcome outcome=_kit.RunHook(mount,_kit.MountDef4Bw);
			Assert.AreEqual(CaptureSessionLaunchKind.Completed,outcome.Result.Kind,outcome.Result.Reason);
			Assert.AreEqual("",outcome.Result.Reason,"The window's script ran to its end.");
			Assert.AreEqual(2,outcome.Result.Summary.CountImages);
			Assert.IsFalse(outcome.IsMountDeleted);
			//The second Mount/Acquire selects the same mount: the launcher reads its slots and documents from the database.
			_kit.WindowRunner.ActionWhileShown=controller => {
				Assert.AreEqual(3,controller.MountSlots.Target.ItemOrder,"The first empty slot.");
				_kit.Start(controller);
				_kit.Expose(120);
				_kit.Expose(160);
				Assert.AreEqual(CaptureSessionPhase.Finished,controller.Phase);
			};
			outcome=_kit.RunHook(Mounts.GetByNum(mount.MountNum),null);
			Assert.AreEqual(CaptureSessionLaunchKind.Completed,outcome.Result.Kind,outcome.Result.Reason);
			Assert.AreEqual("",outcome.Result.Reason,"The window's script ran to its end.");
			Assert.AreEqual(CaptureSessionEndReason.MountFull,outcome.Result.Summary.EndReason);
			Assert.AreEqual(2,outcome.Result.Summary.CountImages);
			List<MountItem> listMountItems=MountItems.GetItemsForMount(mount.MountNum);
			Assert.AreEqual(4,listMountItems.Count,"No unmounted item: no slot was filled twice.");
			CollectionAssert.AreEqual(new[] { 40,80,120,160 },Documents.GetDocumentsForMountItems(listMountItems).Select(x => GetGrey(x)).ToList());
			Assert.AreEqual(4,CaptureDb.GetDocuments(_kit.Patient.PatNum).Count);
		}

		[TestMethod]
		public void NoMount_SingleImage_ADocumentWithTheBannersTeeth_NoMountToDelete() {
			_kit=new CaptureSessionDbKit("savesingle");
			_kit.WindowRunner.ActionWhileShown=controller => {
				string error;
				Assert.IsTrue(controller.SetToothNumbers("8,9",out error),error);
				_kit.Start(controller);
				_kit.Expose(100);
				Assert.IsTrue(_kit.Execute(controller,CaptureSessionAction.End));
				Assert.AreEqual(CaptureSessionPhase.Finished,controller.Phase);
			};
			CaptureHookOutcome outcome=CaptureHookMirror.TryAcquireWithCaptureSession(_kit.Launcher(),() => _kit.PatientShown,null,null,_kit.ImagingDevice,
				_kit.Category.DefNum,() => _kit.PatNumCur);
			Assert.AreEqual(CaptureSessionLaunchKind.Completed,outcome.Result.Kind,outcome.Result.Reason);
			Assert.IsFalse(outcome.Result.IsEmptyMountToDelete);
			Document document=CaptureDb.GetDocuments(_kit.Patient.PatNum).Single();
			Assert.AreEqual(0,document.MountItemNum,"A single document, no mount.");
			Assert.AreEqual("8,9",document.ToothNumbers);
			Assert.AreEqual(_kit.Category.DefNum,document.DocCategory,"The current category (GetCurrentCategory()).");
			Assert.AreEqual(100,GetGrey(document));
			StringAssert.Contains(CaptureDb.GetLogsForPatient(EnumPermType.ImageCreate,_kit.Patient.PatNum).Last().LogText,"single images, no mount");
		}
	}
}
