// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImagingCapture.Recovery;
using ImagingCapture.Save;
using ImagingDeviceManager.Capture;
using ImagingDeviceManager.Config;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace ImagingCapture.Tests {
	///<summary>The quarantine's filing rules (design 7.1, 7.12, 10.6, 10.7) behind the store seam: Assign into the original slot or as a
	///"Recovered: &lt;slot&gt;" unmounted item, Reassign to a patient the user found with a reason, Discard, the permissions and the log texts, and
	///"patient unknown" files and other patients' journal copies only through Reassign.</summary>
	[TestClass]
	public class CaptureRecoveryFilerTests {
		private static readonly DateTime _receivedAt=new DateTime(2026,10,31,10,0,5);
		private RecoveryHarness _harness;

		[TestInitialize]
		public void Setup() {
			_harness=new RecoveryHarness();
		}

		[TestCleanup]
		public void Cleanup() {
			_harness.Dispose();
		}

		private FakeCaptureRecoveryStore Store => _harness.Store;
		private FakeSessionRadiographStore RadiographStore => _harness.RadiographStore;

		#region Assign
		[TestMethod]
		public void Assign_OriginalSlotEmpty_FiledIntoIt_LoggedWithTheSession_FileMovedOut() {
			CaptureFileEntry entry=_harness.Files.Quarantine(RecoveryFiles.SessionFields(),_receivedAt);
			Assert.IsTrue(CaptureRecoveryFiler.IsAssignOffered(entry,0));
			string error;
			CaptureAssignPlan plan=_harness.Filer.PlanAssign(entry,0,out error);
			Assert.IsNotNull(plan,error);
			Assert.IsTrue(plan.IsIntoOriginalSlot);
			Assert.AreSame(_harness.PatientJane,plan.Patient);
			Assert.AreEqual(20,plan.DocCategory,"The category the session saved into.");
			Assert.AreEqual(4,plan.ProvNum,"The session's provider.");
			Assert.AreEqual("Slot 3 (teeth 6) of the mount \"FMX\" ("+_harness.Mount.DateCreated.ToShortDateString()+")",plan.TargetText);
			CaptureFilingResult result=_harness.Filer.Assign(entry,0,0);
			Assert.IsTrue(result.IsDone,result.Error);
			Assert.AreEqual(42,result.PatNum);
			Assert.AreEqual(42,RadiographStore.ListPatientsImported.Single().PatNum,"Never another patient.");
			Assert.AreEqual(20,RadiographStore.ListCategoriesImported.Single());
			Document document=RadiographStore.ListDocumentsUpdated.Single();
			Assert.AreEqual(103,document.MountItemNum);
			Assert.AreEqual("6",document.ToothNumbers);
			Assert.AreEqual("",document.Description,"A slot has no label.");
			Assert.AreEqual(4,document.ProvNum);
			Assert.AreSame(_harness.Slot(3),result.MountItem);
			Assert.IsFalse(result.IsMountItemCreated);
			Assert.AreEqual(0,RadiographStore.ListMountItemsInserted.Count);
			Assert.IsTrue(Store.ListAuthorizationChecks.Any(x => x.Item1==EnumPermType.ImageCreate));
			SecurityLog securityLog=Store.ListLogEntries.Single();
			Assert.AreEqual(EnumPermType.ImageCreate,securityLog.PermType);
			Assert.AreEqual(42,securityLog.PatNum);
			Assert.AreEqual(document.DocNum,securityLog.FKey);
			StringAssert.StartsWith(securityLog.LogText,CaptureRecoveryText.AssignLogPrefix);
			StringAssert.Contains(securityLog.LogText,"session "+RecoveryFiles.SessionGuid.ToString("D"));
			Assert.AreEqual(0,_harness.Files.CountQuarantineFiles(),"Moved out of the quarantine.");
			Assert.IsTrue(File.Exists(result.PathResolved),"Kept in the resolved folder.");
			StringAssert.Contains(result.PathResolved,@"Capture\quarantine-resolved\"+RecoveryFiles.SessionGuid.ToString("D"));
			Assert.AreEqual(0,_harness.Files.Store.ListQuarantine().Count);
		}

		[TestMethod]
		public void Assign_TheExposureAlreadyHasADocument_ByCaptureIdOrTime_NotFiledAgain() {
			//Design 10.6: one document per TWAIN transfer. Its journal copy was recovered before (or another window assigned the file).
			CaptureFileEntry entry=_harness.Files.Quarantine(RecoveryFiles.SessionFields(captureId:"cap-7"),_receivedAt);
			Store.ListPatientDocuments.Add(new Document { DocNum=900,PatNum=42,DateCreated=new DateTime(2026,10,31,8,0,0) });
			Store.ListSecurityLogsExisting.Add(new SecurityLog { PatNum=42,FKey=900,PermType=EnumPermType.ImageCreate,
				LogText="Capture: captureId "+CaptureJournalMatch.QuoteLikeTheSaver("cap-7")+", sequence 3." });
			Assert.IsTrue(CaptureRecoveryFiler.IsAssignOffered(entry,0),"Offered; the plan checks the documents.");
			string error;
			Assert.IsNull(_harness.Filer.PlanAssign(entry,0,out error));
			StringAssert.Contains(error,"already has a document of this exposure (DocNum 900, matched by its capture ID)");
			CaptureFilingResult result=_harness.Filer.Assign(entry,0,0);
			Assert.IsFalse(result.IsDone);
			Assert.AreEqual(0,RadiographStore.ListPatientsImported.Count);
			Assert.AreEqual(1,_harness.Files.CountQuarantineFiles(),"Stays where it is.");
			Store.ListSecurityLogsExisting.Clear();
			DateTime dateExpected=CaptureJournalMatch.GetExpectedDateCreated(entry);
			Store.ListPatientDocuments[0].DateCreated=new DateTime(dateExpected.Year,dateExpected.Month,dateExpected.Day,dateExpected.Hour,
				dateExpected.Minute,dateExpected.Second);
			Assert.IsNull(_harness.Filer.PlanAssign(entry,0,out error));
			StringAssert.Contains(error,"matched by its time");
		}

		[TestMethod]
		public void Assign_OriginalSlotTaken_RecoveredUnmountedItemOfTheOriginalMount_KeepingTheSlotsTeethAndRotation() {
			Store.DictSlotDocuments[103]=new Document { DocNum=900,PatNum=42,MountItemNum=103 };
			_harness.Slot(3).RotateOnAcquire=90;
			CaptureFileEntry entry=_harness.Files.Quarantine(RecoveryFiles.SessionFields(),_receivedAt);
			string error;
			CaptureAssignPlan plan=_harness.Filer.PlanAssign(entry,0,out error);
			Assert.IsFalse(plan.IsIntoOriginalSlot);
			Assert.AreEqual("Recovered: Slot 3 (teeth 6)",plan.Description);
			Assert.AreEqual("The original slot already holds an image.",plan.Note);
			StringAssert.StartsWith(plan.TargetText,"an unmounted item of the mount \"FMX\"");
			CaptureFilingResult result=_harness.Filer.Assign(entry,0,0);
			Assert.IsTrue(result.IsDone,result.Error);
			MountItem mountItemNew=RadiographStore.ListMountItemsInserted.Single();
			Assert.AreEqual(7,mountItemNew.MountNum,"An item of the original mount.");
			Assert.AreEqual(MountPlacement.ItemOrderUnmounted,mountItemNew.ItemOrder);
			Assert.IsTrue(result.IsMountItemCreated);
			Assert.AreSame(mountItemNew,result.MountItem);
			Document document=RadiographStore.ListDocumentsUpdated.Single();
			Assert.AreEqual(mountItemNew.MountItemNum,document.MountItemNum);
			Assert.AreEqual("Recovered: Slot 3 (teeth 6)",document.Description);
			Assert.AreEqual("6",document.ToothNumbers);
			Assert.AreEqual(90,document.DegreesRotated);
			Assert.AreEqual(900,Store.DictSlotDocuments[103].DocNum,"The slot's image is untouched.");
		}

		[TestMethod]
		public void Assign_ALaterImageOfAContinuousEnable_ItsSlotNotRecorded_UnmountedWithNoTeeth_NeverTheEnablesSlot() {
			//The sidecar names slot 3, where the continuous enable started; image 2 was meant for whatever slot was the target when it came.
			Dictionary<string,string> dictFields=RecoveryFiles.SessionFields();
			dictFields[CaptureSidecarKeys.ContinuousEnable]="1";
			dictFields[CaptureSidecarKeys.IndexInEnable]="2";
			_harness.Slot(3).RotateOnAcquire=90;
			CaptureFileEntry entry=_harness.Files.Quarantine(dictFields,_receivedAt);
			Assert.IsFalse(entry.IsSlotKnown);
			string error;
			CaptureAssignPlan plan=_harness.Filer.PlanAssign(entry,0,out error);
			Assert.IsNotNull(plan,error);
			Assert.IsFalse(plan.IsIntoOriginalSlot,"Not into slot 3, although it is empty.");
			Assert.AreEqual("Recovered: slot unknown",plan.Description);
			Assert.AreEqual(CaptureRecoveryFiler.SlotUnknownNote,plan.Note);
			CaptureFilingResult result=_harness.Filer.Assign(entry,0,0);
			Assert.IsTrue(result.IsDone,result.Error);
			Assert.IsTrue(result.IsMountItemCreated);
			Document document=RadiographStore.ListDocumentsUpdated.Single();
			Assert.AreEqual("",document.ToothNumbers,"Not slot 3's teeth...");
			Assert.AreEqual(0,document.DegreesRotated,"...nor its rotation.");
		}

		[TestMethod]
		public void Assign_ALaterImageOfAContinuousEnable_TheSessionRecordedItsSlot_FiledThere() {
			Dictionary<string,string> dictFields=RecoveryFiles.SessionFields();
			dictFields[CaptureSidecarKeys.ContinuousEnable]="1";
			dictFields[CaptureSidecarKeys.IndexInEnable]="2";
			dictFields[CaptureSidecarKeys.SlotOfImage]="1";
			CaptureFileEntry entry=_harness.Files.Quarantine(dictFields,_receivedAt);
			Assert.IsTrue(entry.IsSlotKnown);
			string error;
			Assert.IsTrue(_harness.Filer.PlanAssign(entry,0,out error).IsIntoOriginalSlot,error);
			dictFields=RecoveryFiles.SessionFields(captureId:"cap-9");
			dictFields[CaptureSidecarKeys.ContinuousEnable]="1";
			dictFields[CaptureSidecarKeys.IndexInEnable]="2";
			dictFields["ImageKind"]="Drained";
			Assert.IsFalse(_harness.Files.Quarantine(dictFields,_receivedAt.AddSeconds(1)).IsSlotKnown,
				"A later image of a continuous enable drained during its stop was exposed for the target as it was then, not its enable's slot.");
			dictFields=RecoveryFiles.SessionFields(captureId:"cap-10");
			dictFields[CaptureSidecarKeys.ContinuousEnable]="0";
			dictFields[CaptureSidecarKeys.IndexInEnable]="1";
			dictFields["ImageKind"]="Drained";
			Assert.IsTrue(_harness.Files.Quarantine(dictFields,_receivedAt.AddSeconds(2)).IsSlotKnown,"A drained image of a per-slot enable goes to its enable's slot.");
			dictFields=RecoveryFiles.SessionFields(captureId:"cap-11");
			dictFields[CaptureSidecarKeys.ContinuousEnable]="0";
			dictFields[CaptureSidecarKeys.IndexInEnable]="2";
			dictFields["ImageKind"]="Drained";
			dictFields[CaptureSidecarKeys.SlotOfImage]="1";
			Assert.IsFalse(_harness.Files.Quarantine(dictFields,_receivedAt.AddSeconds(3)).IsSlotKnown,
				"A second image of a per-slot enable (it asked for one) is never its enable's slot.");
			dictFields=RecoveryFiles.SessionFields(captureId:"cap-12");
			dictFields["ImageKind"]="Extra";
			Assert.IsFalse(_harness.Files.Quarantine(dictFields,_receivedAt.AddSeconds(4)).IsSlotKnown,"An extra image is never its enable's slot.");
		}

		[TestMethod]
		public void Assign_AnExtraImageOfASingleImageSession_FiledWithNoTeeth() {
			Dictionary<string,string> dictFields=RecoveryFiles.SessionFields();
			dictFields.Remove(CaptureSidecarKeys.MountNum);
			dictFields.Remove(CaptureSidecarKeys.MountItemNum);
			dictFields.Remove(CaptureSidecarKeys.SlotItemOrder);
			dictFields["Slot"]="Single image, #6";
			dictFields["Target"]="Single image, #6";
			dictFields["ImageKind"]="Extra";
			dictFields[CaptureSidecarKeys.IndexInEnable]="2";
			CaptureFileEntry entry=_harness.Files.Quarantine(dictFields,_receivedAt);
			Assert.IsFalse(entry.IsSlotKnown);
			string error;
			CaptureAssignPlan plan=_harness.Filer.PlanAssign(entry,0,out error);
			Assert.IsNotNull(plan,error);
			Assert.AreEqual("",plan.ToothNumbers,"Not the banner's teeth of its enable.");
		}

		[TestMethod]
		public void Assign_MountGone_SingleDocumentWithTheSlotsTeeth() {
			Store.DictMounts.Clear();
			CaptureFileEntry entry=_harness.Files.Quarantine(RecoveryFiles.SessionFields(),_receivedAt);
			CaptureFilingResult result=_harness.Filer.Assign(entry,0,0);
			Assert.IsTrue(result.IsDone,result.Error);
			Assert.IsNull(result.Mount);
			Document document=RadiographStore.ListDocumentsUpdated.Single();
			Assert.AreEqual(0,document.MountItemNum);
			Assert.AreEqual("6",document.ToothNumbers);
			Assert.AreEqual("Recovered: Slot 3 (teeth 6)",document.Description);
		}

		[TestMethod]
		public void Assign_SingleImageSession_TheBannersTeeth() {
			Dictionary<string,string> dictFields=RecoveryFiles.SessionFields();
			dictFields[CaptureSidecarKeys.MountNum]="";
			dictFields[CaptureSidecarKeys.MountItemNum]="";
			dictFields[CaptureSidecarKeys.ToothNumbers]="8,9";
			dictFields["Slot"]="Single image (teeth 8,9)";
			CaptureFileEntry entry=_harness.Files.Quarantine(dictFields,_receivedAt);
			CaptureFilingResult result=_harness.Filer.Assign(entry,0,0);
			Assert.IsTrue(result.IsDone,result.Error);
			Document document=RadiographStore.ListDocumentsUpdated.Single();
			Assert.AreEqual("8,9",document.ToothNumbers);
			Assert.AreEqual("",document.Description);
			Assert.AreEqual("a single document (teeth 8,9)",result.TargetText);
		}

		[TestMethod]
		public void Assign_PatientUnknown_NeverOffered_Refused() {
			CaptureFileEntry entry=_harness.Files.Quarantine(RecoveryFiles.SessionFields(patNum:0),_receivedAt);
			Assert.IsTrue(entry.IsPatientUnknown);
			Assert.IsFalse(CaptureRecoveryFiler.IsAssignOffered(entry,42),"Not even in a session of a patient.");
			Assert.IsTrue(CaptureRecoveryFiler.IsReassignOffered(entry));
			CaptureFilingResult result=_harness.Filer.Assign(entry,20,42);
			Assert.IsFalse(result.IsDone);
			StringAssert.Contains(result.Error,"no originating patient");
			Assert.AreEqual(0,RadiographStore.ListPatientsImported.Count);
			Assert.AreEqual(1,_harness.Files.CountQuarantineFiles());
		}

		[TestMethod]
		public void Assign_RedeliveredNotConfirmed_NeverOffered_EvenWithAPatNum_ReassignToThatPatientAllowed() {
			//A sidecar that records the bound patient for a redelivered image the operator never confirmed (the library before this fix wrote one).
			Dictionary<string,string> dictFields=RecoveryFiles.SessionFields();
			dictFields["Redelivered"]="1";
			CaptureFileEntry entry=_harness.Files.Quarantine(dictFields,_receivedAt);
			Assert.IsFalse(entry.IsPatientUnknown);
			Assert.IsFalse(CaptureRecoveryFiler.IsAssignOffered(entry,42),"Design 7.12: placed only after the operator confirms it.");
			CaptureFilingResult result=_harness.Filer.Assign(entry,20,42);
			Assert.IsFalse(result.IsDone);
			StringAssert.Contains(result.Error,"no originating patient");
			Assert.AreEqual(0,RadiographStore.ListPatientsImported.Count);
			Assert.IsTrue(CaptureRecoveryFiler.IsReassignOffered(entry));
			CaptureFilingResult resultReassign=_harness.Filer.Reassign(entry,_harness.PatientJane,"Checked against the patient.",20);
			Assert.IsTrue(resultReassign.IsDone,resultReassign.Error);
			Assert.AreEqual(42,RadiographStore.ListPatientsImported.Single().PatNum);
			Dictionary<string,string> dictConfirmed=RecoveryFiles.SessionFields(captureId:"cap-8");
			dictConfirmed["Redelivered"]="1";
			dictConfirmed[CaptureSidecarKeys.RedeliveryConfirmed]="1";
			CaptureFileEntry entryConfirmed=_harness.Files.Quarantine(dictConfirmed,_receivedAt.AddSeconds(1));
			Assert.IsTrue(CaptureRecoveryFiler.IsAssignOffered(entryConfirmed,42),"Confirmed in the session for the bound patient.");
		}

		[TestMethod]
		public void Assign_NeedsImageCreate_NothingChanged() {
			Store.HashSetDenied.Add(EnumPermType.ImageCreate);
			CaptureFileEntry entry=_harness.Files.Quarantine(RecoveryFiles.SessionFields(),_receivedAt);
			CaptureFilingResult result=_harness.Filer.Assign(entry,0,0);
			Assert.IsFalse(result.IsDone);
			Assert.AreEqual("Not authorized for ImageCreate.",result.Error);
			Assert.AreEqual(DateTime.MinValue,Store.ListAuthorizationChecks.Single().Item2,"ImageCreate has no date restriction (design 10.7).");
			Assert.AreEqual(0,RadiographStore.ListPatientsImported.Count);
			Assert.AreEqual(0,Store.ListLogEntries.Count);
			Assert.AreEqual(1,_harness.Files.CountQuarantineFiles());
		}

		[TestMethod]
		public void Assign_OriginatingPatientGone_UseReassign() {
			Store.DictPatients.Remove(42);
			CaptureFileEntry entry=_harness.Files.Quarantine(RecoveryFiles.SessionFields(),_receivedAt);
			CaptureFilingResult result=_harness.Filer.Assign(entry,0,0);
			Assert.IsFalse(result.IsDone);
			StringAssert.Contains(result.Error,"Use Reassign");
		}

		[TestMethod]
		public void Assign_OriginatingPatientMergedIntoAnother_Refused_NamesTheSurvivingPatient_NothingFiled() {
			//The merge archived patient 42 and moved its mount to patient 43: filing for 42 would put the image into the archived chart.
			Store.DictPatients[42].PatStatus=PatientStatus.Archived;
			Store.DictMergedInto[42]=43;
			CaptureFileEntry entry=_harness.Files.Quarantine(RecoveryFiles.SessionFields(),_receivedAt);
			string error;
			Assert.IsNull(_harness.Filer.PlanAssign(entry,0,out error));
			Assert.AreEqual("The originating patient (PatNum 42) was merged into PatNum 43. Use Reassign to file the image for that patient.",error);
			CaptureFilingResult result=_harness.Filer.Assign(entry,0,0);
			Assert.IsFalse(result.IsDone);
			Assert.AreEqual(error,result.Error);
			Assert.AreEqual(0,RadiographStore.ListPatientsImported.Count);
			Assert.AreEqual(0,Store.ListLogEntries.Count);
			Assert.AreEqual(1,_harness.Files.CountQuarantineFiles(),"The file stays for Reassign.");
		}

		[TestMethod]
		public void Assign_OriginatingPatientDeleted_Refused_UseReassign() {
			Store.DictPatients[42].PatStatus=PatientStatus.Deleted;
			CaptureFileEntry entry=_harness.Files.Quarantine(RecoveryFiles.SessionFields(),_receivedAt);
			CaptureFilingResult result=_harness.Filer.Assign(entry,0,0);
			Assert.IsFalse(result.IsDone);
			Assert.AreEqual("The originating patient (PatNum 42) was deleted. Use Reassign.",result.Error);
			Assert.AreEqual(0,RadiographStore.ListPatientsImported.Count);
			Assert.AreEqual(1,_harness.Files.CountQuarantineFiles());
		}

		[TestMethod]
		public void Assign_OriginatingPatientArchivedNotMerged_Filed_TheConfirmationSaysArchived() {
			//Offices archive inactive patients, and a session can be bound to one: only a merge or a deletion refuses Assign.
			Store.DictPatients[42].PatStatus=PatientStatus.Archived;
			CaptureFileEntry entry=_harness.Files.Quarantine(RecoveryFiles.SessionFields(),_receivedAt);
			string error;
			CaptureAssignPlan plan=_harness.Filer.PlanAssign(entry,0,out error);
			Assert.IsNotNull(plan,error);
			StringAssert.EndsWith(CaptureRecoveryText.GetPatientLine(plan.Patient),"(PatNum 42, Archived)");
			CaptureFilingResult result=_harness.Filer.Assign(entry,0,0);
			Assert.IsTrue(result.IsDone,result.Error);
		}

		[TestMethod]
		public void Assign_SaveFails_FileStays() {
			RadiographStore.ThrowOn="Import";
			CaptureFileEntry entry=_harness.Files.Quarantine(RecoveryFiles.SessionFields(),_receivedAt);
			CaptureFilingResult result=_harness.Filer.Assign(entry,0,0);
			Assert.IsFalse(result.IsDone);
			StringAssert.StartsWith(result.Error,"The image was not filed: ");
			Assert.AreEqual(1,_harness.Files.CountQuarantineFiles());
			Assert.AreEqual(0,Store.ListLogEntries.Count);
		}

		[TestMethod]
		public void Assign_NoValidCategory_UserChooses() {
			Store.HashSetCategories.Clear();
			Store.HashSetCategories.Add(30);
			CaptureFileEntry entry=_harness.Files.Quarantine(RecoveryFiles.SessionFields(),_receivedAt);
			string error;
			Assert.AreEqual(0,_harness.Filer.PlanAssign(entry,0,out error).DocCategory,"Neither the session's nor the mount's category is valid.");
			Assert.IsFalse(_harness.Filer.Assign(entry,0,0).IsDone);
			Assert.IsTrue(_harness.Filer.Assign(entry,30,0).IsDone);
			Assert.AreEqual(30,RadiographStore.ListCategoriesImported.Single());
		}

		[TestMethod]
		public void Assign_16Bit_OwnEmptySlot_The16BitRulesHold_16BitDicomInItsSlot_NeverACopy() {
			CaptureFileEntry entry=_harness.Files.Quarantine16(RecoveryFiles.SessionFieldsWithDriver(),_receivedAt);
			Assert.IsTrue(entry.IsGray16);
			_harness.SetGatesPassedAt(16);
			string error;
			CaptureAssignPlan plan=_harness.Filer.PlanAssign(entry,0,out error);
			Assert.IsNotNull(plan,error);
			Assert.IsTrue(plan.IsIntoOriginalSlot);
			Assert.IsFalse(plan.Is8BitCopy,"Its own empty slot under the session's conditions: 16-bit DICOM, as before.");
			Assert.IsTrue(plan.Is16BitAllowed);
			Assert.AreEqual("",plan.Reason16BitRefused);
			Assert.AreEqual("",plan.Note);
			CaptureFilingResult result=_harness.Filer.Assign(entry,0,0,plan);
			Assert.IsTrue(result.IsDone,result.Error);
			Assert.AreEqual(".dcm",RadiographStore.ListExtensions.Single(),"16-bit DICOM, never converted to 8 bits.");
			Assert.IsFalse(result.IsCopy8BitOf16Bit);
			Assert.AreEqual("",CaptureRecoveryText.GetCopyDoneText(result));
			Document document=RadiographStore.ListDocumentsUpdated.Single();
			Assert.AreEqual(103,document.MountItemNum,"Its own empty slot.");
			Assert.AreEqual("6",document.ToothNumbers);
			SecurityLog securityLog=Store.ListLogEntries.Single(x => x.PermType==EnumPermType.ImageCreate);
			Assert.IsFalse(securityLog.LogText.Contains(RadiographSavePlanner.Copy8BitText),securityLog.LogText);
			Assert.AreEqual(0,RadiographStore.CountPearl,"16-bit DICOM is never sent.");
		}

		[TestMethod]
		public void Assign_JournalCopy_OnlyInItsPatientsSession_MarkedInPlace() {
			CaptureFileEntry entry=_harness.Files.Journal(RecoveryFiles.SessionFields(),_receivedAt);
			Assert.IsFalse(CaptureRecoveryFiler.IsAssignOffered(entry,0),"Outside a session: Reassign only.");
			Assert.IsFalse(CaptureRecoveryFiler.IsAssignOffered(entry,77),"Another patient's copy: Reassign only.");
			Assert.IsTrue(CaptureRecoveryFiler.IsAssignOffered(entry,42));
			Assert.IsFalse(CaptureRecoveryFiler.IsDiscardOffered(entry),"Rotation removes journal copies.");
			Assert.IsFalse(_harness.Filer.Assign(entry,0,77).IsDone);
			Assert.AreEqual(0,RadiographStore.ListPatientsImported.Count);
			CaptureFilingResult result=_harness.Filer.Assign(entry,0,42);
			Assert.IsTrue(result.IsDone,result.Error);
			Assert.AreEqual(entry.ImagePath,result.PathResolved,"The copy stays in the journal.");
			StringAssert.StartsWith(Store.ListLogEntries.Single().LogText,CaptureRecoveryText.RecoverLogPrefix);
			CaptureFileEntry entryAfter=_harness.Files.Store.ListJournal().Single();
			Assert.IsTrue(entryAfter.IsResolved);
			Assert.IsFalse(CaptureRecoveryFiler.IsAssignOffered(entryAfter,42),"Not offered again.");
		}

		[TestMethod]
		public void JournalCopyOfAQuarantinedImage_FiledOnlyFromTheQuarantine() {
			CaptureFileEntry entry=_harness.Files.Journal(RecoveryFiles.SessionFields(),_receivedAt,"Quarantine");
			Assert.IsTrue(entry.IsCopyOfQuarantined);
			Assert.IsFalse(CaptureRecoveryFiler.IsAssignOffered(entry,42));
			Assert.IsFalse(CaptureRecoveryFiler.IsReassignOffered(entry));
		}
		#endregion Assign

		#region Reassign
		[TestMethod]
		public void Reassign_ToTheFoundPatient_UnmountedDocumentNoTeeth_BothPatientsAndTheReasonLogged() {
			CaptureFileEntry entry=_harness.Files.Quarantine(RecoveryFiles.SessionFields(),_receivedAt);
			CaptureFilingResult result=_harness.Filer.Reassign(entry,_harness.PatientJohn," Wrong patient selected at the chair. ",21);
			Assert.IsTrue(result.IsDone,result.Error);
			Assert.AreEqual(77,result.PatNum);
			Assert.AreEqual(77,RadiographStore.ListPatientsImported.Single().PatNum);
			Document document=RadiographStore.ListDocumentsUpdated.Single();
			Assert.AreEqual(0,document.MountItemNum,"Not in any mount.");
			Assert.AreEqual("",document.ToothNumbers,"No tooth numbers.");
			Assert.AreEqual(CaptureRecoveryText.ReassignedDescription,document.Description);
			Assert.AreEqual(3,document.ProvNum,"The target patient's default provider.");
			Assert.AreEqual(21,RadiographStore.ListCategoriesImported.Single());
			Tuple<EnumPermType,DateTime> check=Store.ListAuthorizationChecks.Single();
			Assert.AreEqual(EnumPermType.ImageEdit,check.Item1);
			Assert.AreEqual(_receivedAt,check.Item2,"Date-restricted by the receive time.");
			Assert.AreEqual(2,Store.ListLogEntries.Count);
			SecurityLog securityLogTarget=Store.ListLogEntries[0];
			Assert.AreEqual(EnumPermType.ImageEdit,securityLogTarget.PermType);
			Assert.AreEqual(77,securityLogTarget.PatNum);
			Assert.AreEqual(document.DocNum,securityLogTarget.FKey);
			string expected=CaptureRecoveryText.ReassignLogPrefix+"file "+entry.FileName+" (unassigned captures, session "+RecoveryFiles.SessionGuid.ToString("D")
				+", received 2026-10-31 10:00:05, source \"Example Sensor DS\", captureId \"cap-7\", sequence 3, slot Slot 3 (teeth 6)) was for Doe, Jane "
				+"(PatNum 42); reassigned to Roe, John (PatNum 77) as DocNum "+document.DocNum+", an unmounted document with no tooth numbers. Reason: "
				+"Wrong patient selected at the chair.";
			Assert.AreEqual(expected,securityLogTarget.LogText);
			Assert.AreEqual(42,Store.ListLogEntries[1].PatNum,"The originating patient's audit shows it too.");
			Assert.AreEqual(0,Store.ListLogEntries[1].FKey);
			Assert.AreEqual(0,_harness.Files.CountQuarantineFiles());
			Dictionary<string,string> dictSidecar=ReadSidecar(result.PathResolved);
			Assert.AreEqual("Reassigned",dictSidecar[CaptureSidecarKeys.Disposition]);
			Assert.AreEqual("77",dictSidecar["DispositionPatNum"]);
			Assert.AreEqual("42",dictSidecar["DispositionPatNumFrom"]);
			Assert.AreEqual("Wrong patient selected at the chair.",dictSidecar["DispositionReason"]);
		}

		[TestMethod]
		public void Reassign_PatientUnknown_Allowed_LogSaysPatientUnknown() {
			CaptureFileEntry entry=_harness.Files.Quarantine(RecoveryFiles.SessionFields(patNum:0),_receivedAt,isRoot:true);
			CaptureFilingResult result=_harness.Filer.Reassign(entry,_harness.PatientJane,"Declined redelivery, checked the image.",20);
			Assert.IsTrue(result.IsDone,result.Error);
			Assert.AreEqual(1,Store.ListLogEntries.Count,"No originating patient to log under.");
			StringAssert.Contains(Store.ListLogEntries[0].LogText," was for patient unknown; reassigned to Doe, Jane (PatNum 42)");
		}

		[TestMethod]
		public void Reassign_NeedsImageEditAPatientAndAReason() {
			CaptureFileEntry entry=_harness.Files.Quarantine(RecoveryFiles.SessionFields(),_receivedAt);
			Assert.AreEqual("Find the patient to reassign it to first.",_harness.Filer.Reassign(entry,null,"reason",20).Error,"No patient is ever picked for the user.");
			Assert.AreEqual("Type the reason for the reassignment.",_harness.Filer.Reassign(entry,_harness.PatientJohn,"   ",20).Error);
			Assert.AreEqual("Image category missing; choose a category.",_harness.Filer.Reassign(entry,_harness.PatientJohn,"reason",0).Error);
			Store.HashSetDenied.Add(EnumPermType.ImageEdit);
			Assert.AreEqual("Not authorized for ImageEdit.",_harness.Filer.Reassign(entry,_harness.PatientJohn,"reason",20).Error);
			Assert.AreEqual(0,RadiographStore.ListPatientsImported.Count);
			Assert.AreEqual(1,_harness.Files.CountQuarantineFiles());
		}

		[TestMethod]
		public void Reassign_ToTheOriginatingPatientOfAQuarantineFile_UseAssign() {
			CaptureFileEntry entry=_harness.Files.Quarantine(RecoveryFiles.SessionFields(),_receivedAt);
			CaptureFilingResult result=_harness.Filer.Reassign(entry,_harness.PatientJane,"reason",20);
			Assert.IsFalse(result.IsDone);
			StringAssert.Contains(result.Error,"use Assign");
		}

		[TestMethod]
		public void Reassign_JournalCopyOfAnotherPatient() {
			CaptureFileEntry entry=_harness.Files.Journal(RecoveryFiles.SessionFields(patNum:77),_receivedAt);
			Assert.IsFalse(CaptureRecoveryFiler.IsAssignOffered(entry,42));
			CaptureFilingResult result=_harness.Filer.Reassign(entry,_harness.PatientJane,"The journal copy was taken for Jane.",20);
			Assert.IsTrue(result.IsDone,result.Error);
			StringAssert.Contains(Store.ListLogEntries[0].LogText,"(capture journal,");
			Assert.IsTrue(_harness.Files.Store.ListJournal().Single().IsResolved);
		}

		[TestMethod]
		public void Reassign_AJournalCopyOfAnImageSavedNormally_Refused_ForEitherPatient() {
			//Outside a session the journal view lists every copy; most of them were saved normally (design 10.6: one document per transfer).
			CaptureFileEntry entry=_harness.Files.Journal(RecoveryFiles.SessionFields(captureId:"cap-7"),_receivedAt);
			Store.ListPatientDocuments.Add(new Document { DocNum=900,PatNum=42,DateCreated=new DateTime(2026,10,31,8,0,0) });
			Store.ListSecurityLogsExisting.Add(new SecurityLog { PatNum=42,FKey=900,PermType=EnumPermType.ImageCreate,
				LogText="Capture: captureId "+CaptureJournalMatch.QuoteLikeTheSaver("cap-7")+", sequence 3." });
			CaptureFilingResult result=_harness.Filer.Reassign(entry,_harness.PatientJohn,"Taken for John.",20);
			Assert.IsFalse(result.IsDone,"The exposure would be in both charts.");
			StringAssert.Contains(result.Error,"This exposure is already DocNum 900 of Jane Doe (PatNum 42");
			StringAssert.Contains(result.Error,"move that document in the Imaging module instead");
			CaptureFilingResult resultOwn=_harness.Filer.Reassign(entry,_harness.PatientJane,"Again.",20);
			Assert.IsFalse(resultOwn.IsDone,"A second, unmounted document of the same exposure.");
			StringAssert.Contains(resultOwn.Error,"originating patient");
			Assert.AreEqual(0,RadiographStore.ListPatientsImported.Count);
			Assert.IsFalse(_harness.Files.Store.ListJournal().Single().IsResolved);
		}

		[TestMethod]
		public void Reassign_ACopyWithNoPatientOfAnImageTheTargetAlreadyHas_Refused() {
			//A confirmed and saved redelivered image: its journal copy was written before the confirmation and records no patient.
			CaptureFileEntry entry=_harness.Files.Journal(RecoveryFiles.SessionFields(patNum:0,captureId:"cap-7"),_receivedAt);
			Assert.IsTrue(entry.IsPatientUnknown);
			Store.ListPatientDocuments.Add(new Document { DocNum=901,PatNum=42,DateCreated=new DateTime(2026,10,31,8,0,0) });
			Store.ListSecurityLogsExisting.Add(new SecurityLog { PatNum=42,FKey=901,PermType=EnumPermType.ImageCreate,
				LogText="Capture: captureId "+CaptureJournalMatch.QuoteLikeTheSaver("cap-7")+", sequence 3." });
			CaptureFilingResult result=_harness.Filer.Reassign(entry,_harness.PatientJane,"It is Jane's.",20);
			Assert.IsFalse(result.IsDone);
			StringAssert.Contains(result.Error,"already DocNum 901 of Jane Doe");
			Assert.AreEqual(0,RadiographStore.ListPatientsImported.Count);
		}

		[TestMethod]
		public void Reassign_TheCopyOfAConfirmedRedeliveredImage_RewrittenWithItsPatient_RefusedForAnotherPatientToo() {
			//TwainCaptureSession.ConfirmRedeliveredJournalCopy after the save: the copy names the bound patient and RedeliveryConfirmed=1.
			Dictionary<string,string> dictFields=RecoveryFiles.SessionFields(patNum:42,captureId:"cap-7");
			dictFields["Redelivered"]="1";
			dictFields[CaptureSidecarKeys.RedeliveryConfirmed]="1";
			CaptureFileEntry entry=_harness.Files.Journal(dictFields,_receivedAt);
			Assert.IsFalse(entry.IsPatientUnknown);
			Assert.IsTrue(entry.IsRedeliveryConfirmed);
			Store.ListPatientDocuments.Add(new Document { DocNum=901,PatNum=42,DateCreated=new DateTime(2026,10,31,8,0,0) });
			Store.ListSecurityLogsExisting.Add(new SecurityLog { PatNum=42,FKey=901,PermType=EnumPermType.ImageCreate,
				LogText="Capture: captureId "+CaptureJournalMatch.QuoteLikeTheSaver("cap-7")+", sequence 3." });
			CaptureFilingResult result=_harness.Filer.Reassign(entry,_harness.PatientJohn,"It is John's.",20);
			Assert.IsFalse(result.IsDone,"The exposure of Jane would also become a document of John.");
			StringAssert.Contains(result.Error,"This exposure is already DocNum 901 of Jane Doe (PatNum 42");
			CaptureFilingResult resultOwn=_harness.Filer.Reassign(entry,_harness.PatientJane,"Again.",20);
			Assert.IsFalse(resultOwn.IsDone);
			StringAssert.Contains(resultOwn.Error,"originating patient");
			Assert.AreEqual(0,RadiographStore.ListPatientsImported.Count);
			Assert.IsFalse(_harness.Files.Store.ListJournal().Single().IsResolved);
		}

		[TestMethod]
		public void Reassign_16Bit_ToTheFoundPatient_An8BitCopy_ImageEditSaysSo_OriginalKept() {
			CaptureFileEntry entry=_harness.Files.Quarantine16(RecoveryFiles.SessionFields(patNum:0),_receivedAt);
			Assert.IsTrue(CaptureRecoveryFiler.IsReassignOffered(entry),"Owner decision 2026-11-01: reassigned as its explicit 8-bit copy.");
			CaptureFilingResult result=_harness.Filer.Reassign(entry,_harness.PatientJane,"The phantom was Jane's test.",20);
			Assert.IsTrue(result.IsDone,result.Error);
			Assert.AreEqual(".png",RadiographStore.ListExtensions.Single(),"An 8-bit PNG, never 16-bit DICOM, for an unmounted document.");
			Document document=RadiographStore.ListDocumentsUpdated.Single();
			Assert.AreEqual(CaptureRecoveryText.ReassignedDescription,document.Description);
			Assert.AreEqual("",document.ToothNumbers);
			Assert.AreEqual(0,document.MountItemNum);
			SecurityLog securityLog=Store.ListLogEntries.Single(x => x.PermType==EnumPermType.ImageEdit);
			StringAssert.Contains(securityLog.LogText,"an 8-bit copy of a 16-bit capture (made through the window 1000 to 3231 of its values; the 16-bit "
				+"original "+entry.FileName+" and its sidecar are kept)");
			Assert.IsFalse(securityLog.LogText.Contains("quarantine-resolved"),"Written before the original moves: no folder it may not reach.");
			StringAssert.Contains(securityLog.LogText,"Reason: The phantom was Jane's test.");
			AssertOriginalResolved(entry,document.DocNum,1000,3231);
			Assert.AreEqual(0,RadiographStore.CountPearl,"The copy is a rendering of a 16-bit capture: never sent.");
		}

		///<summary>The 16-bit original moved to Capture\quarantine-resolved with its sidecar, whose disposition names the copy's DocNum and window.</summary>
		private void AssertOriginalResolved(CaptureFileEntry entry,long docNumCopy,int windowLow,int windowHigh) {
			AssertOriginalResolved(_harness,entry,docNumCopy,windowLow,windowHigh);
		}

		///<summary>As above, in harness, for an original taken at bitDepth.</summary>
		private static void AssertOriginalResolved(RecoveryHarness harness,CaptureFileEntry entry,long docNumCopy,int windowLow,int windowHigh,
			int bitDepth=16,string message="")
		{
			Assert.AreEqual(0,harness.Files.CountQuarantineFiles(),message+" No longer an unassigned capture.");
			string folderResolved=Path.Combine(harness.Files.Folder,@"Capture\quarantine-resolved",RecoveryFiles.SessionGuid.ToString("D"));
			string pathImage=Path.Combine(folderResolved,entry.FileName);
			Assert.IsTrue(File.Exists(pathImage),message+" The 16-bit original is kept, never deleted.");
			string sidecar=File.ReadAllText(Path.ChangeExtension(pathImage,".txt"));
			StringAssert.Contains(sidecar,"DispositionDocNum="+docNumCopy,message);
			StringAssert.Contains(sidecar,"DispositionCopy="+RadiographSavePlanner.Copy8BitText,message);
			StringAssert.Contains(sidecar,"DispositionCopyWindowLow="+windowLow,message);
			StringAssert.Contains(sidecar,"DispositionCopyWindowHigh="+windowHigh,message);
			StringAssert.Contains(sidecar,"DispositionCopyBitDepth="+bitDepth,message);
		}

		///<summary>The grey value of pixel (x,y) of the copy's PNG.</summary>
		private int GrayOfCopy(int x,int y) {
			using(MemoryStream memoryStream=new MemoryStream(RadiographStore.ListBytesImported.Last()))
			using(System.Drawing.Bitmap bitmap=new System.Drawing.Bitmap(memoryStream)) {
				return bitmap.GetPixel(x,y).R;
			}
		}
		#endregion Reassign

		#region The 16-bit filing route
		[TestMethod]
		public void Assign_16Bit_SlotTaken_An8BitCopyAsRecoveredItem_WithTheSlotsTeeth_OriginalKept() {
			CaptureFileEntry entry=_harness.Files.Quarantine16(RecoveryFiles.SessionFields(),_receivedAt);
			Store.DictSlotDocuments[103]=new Document { DocNum=900,PatNum=42,MountItemNum=103 };
			string error;
			CaptureAssignPlan plan=_harness.Filer.PlanAssign(entry,0,out error);
			Assert.IsNotNull(plan,error);
			Assert.IsFalse(plan.IsIntoOriginalSlot);
			Assert.IsTrue(plan.Is8BitCopy);
			Assert.AreEqual("Recovered: Slot 3 (teeth 6)",plan.Description);
			StringAssert.Contains(plan.Note,"filed as an 8-bit copy: a gray PNG made with its 0.5-99.5 % window, not the 16-bit original. The 16-bit "
				+"original and its sidecar stay in Capture\\quarantine-resolved\\"+RecoveryFiles.SessionGuid.ToString("D")+" on this computer.");
			CaptureFilingResult result=_harness.Filer.Assign(entry,0,0);
			Assert.IsTrue(result.IsDone,result.Error);
			Assert.AreEqual(".png",RadiographStore.ListExtensions.Single());
			Assert.IsTrue(result.IsMountItemCreated,"An unmounted item of the mount.");
			Document document=RadiographStore.ListDocumentsUpdated.Single();
			Assert.AreEqual("Recovered: Slot 3 (teeth 6)",document.Description);
			Assert.AreEqual("6",document.ToothNumbers,"The slot's teeth.");
			Assert.AreEqual(0,GrayOfCopy(0,0),"The 0.5 % end of the window is black.");
			Assert.AreEqual(255,GrayOfCopy(5,3),"The 99.5 % end is white.");
			SecurityLog securityLog=Store.ListLogEntries.Single(x => x.PermType==EnumPermType.ImageCreate);
			StringAssert.StartsWith(securityLog.LogText,CaptureRecoveryText.AssignLogPrefix);
			StringAssert.Contains(securityLog.LogText,"as DocNum "+document.DocNum+", an 8-bit copy of a 16-bit capture (made through the window 1000 "
				+"to 3231 of its values;");
			StringAssert.Contains(RadiographStore.ListLogTexts.Single(),RadiographSavePlanner.Copy8BitText,"The capture details entry says so too.");
			AssertOriginalResolved(entry,document.DocNum,1000,3231);
			Assert.AreEqual(0,RadiographStore.CountPearl);
		}

		[TestMethod]
		public void Assign_16Bit_SlotTaken_TheOriginalCannotLeaveTheQuarantine_TheAuditAndDoneTextsNameNoFolderItIsNotIn() {
			CaptureFileEntry entry=_harness.Files.Quarantine16(RecoveryFiles.SessionFields(),_receivedAt);
			Store.DictSlotDocuments[103]=new Document { DocNum=900,PatNum=42,MountItemNum=103 };
			//A different file of the same name is already in quarantine-resolved: the move fails, and the sidecar records the filing in place.
			string folderResolved=Path.Combine(_harness.Files.Folder,@"Capture\quarantine-resolved",RecoveryFiles.SessionGuid.ToString("D"));
			Directory.CreateDirectory(folderResolved);
			File.WriteAllText(Path.Combine(folderResolved,entry.FileName),"another file");
			CaptureFilingResult result=_harness.Filer.Assign(entry,0,0);
			Assert.IsTrue(result.IsDone,result.Error);
			Assert.IsTrue(result.IsCopy8BitOf16Bit);
			Assert.IsTrue(result.ListWarnings.Any(x => x.Contains("could not be moved out of the capture quarantine")),string.Join(" ",result.ListWarnings));
			Assert.AreEqual(entry.ImagePath,result.PathResolved,"The 16-bit original is still in the quarantine.");
			Assert.IsTrue(File.Exists(entry.ImagePath));
			Assert.AreEqual(" as an 8-bit copy of a 16-bit capture; the 16-bit original is kept in Capture\\quarantine\\"
				+RecoveryFiles.SessionGuid.ToString("D"),CaptureRecoveryText.GetCopyDoneText(result));
			SecurityLog securityLog=Store.ListLogEntries.Single(x => x.PermType==EnumPermType.ImageCreate);
			StringAssert.Contains(securityLog.LogText,"the 16-bit original "+entry.FileName+" and its sidecar are kept)");
			Assert.IsFalse(securityLog.LogText.Contains("quarantine-resolved"),securityLog.LogText);
			Assert.AreEqual(0,_harness.Files.Store.ListQuarantine().Count(x => !x.IsResolved),"Its sidecar records the filing: not offered again.");
		}

		[TestMethod]
		public void Assign_TheSlotFilledAfterTheConfirmation_Refused_NeverASilent8BitCopy_NorAnotherTarget() {
			CaptureFileEntry entry=_harness.Files.Quarantine16(RecoveryFiles.SessionFieldsWithDriver(),_receivedAt);
			_harness.SetGatesPassedAt(16);
			string error;
			CaptureAssignPlan plan=_harness.Filer.PlanAssign(entry,0,out error);
			Assert.IsNotNull(plan,error);
			Assert.IsTrue(plan.IsIntoOriginalSlot);
			Assert.IsFalse(plan.Is8BitCopy,"Confirmed: 16-bit DICOM into its own empty slot.");
			Store.DictSlotDocuments[103]=new Document { DocNum=900,PatNum=42,MountItemNum=103 };//another computer filed a retake meanwhile
			CaptureFilingResult result=_harness.Filer.Assign(entry,0,0,plan);
			Assert.IsFalse(result.IsDone);
			Assert.IsTrue(result.IsPlanChanged);
			Assert.AreEqual(CaptureRecoveryText.AssignPlanChanged,result.Error);
			Assert.AreEqual(0,RadiographStore.ListPatientsImported.Count,"Nothing filed, least of all an 8-bit PNG the operator never saw a note for.");
			Assert.AreEqual(1,_harness.Files.CountQuarantineFiles());
			CaptureAssignPlan planNow=_harness.Filer.PlanAssign(entry,0,out error);
			Assert.IsTrue(planNow.Is8BitCopy);
			CaptureFilingResult resultNow=_harness.Filer.Assign(entry,0,0,planNow);
			Assert.IsTrue(resultNow.IsDone,resultNow.Error);
			Assert.IsTrue(resultNow.IsCopy8BitOf16Bit);
			StringAssert.StartsWith(CaptureRecoveryText.GetCopyDoneText(resultNow)," as an 8-bit copy of a 16-bit capture; the 16-bit original is kept in "
				+"Capture\\quarantine-resolved\\"+RecoveryFiles.SessionGuid.ToString("D"));
			//An 8-bit capture whose slot is filled meanwhile is not silently filed as a "Recovered:" item either.
			CaptureFileEntry entry8=_harness.Files.Quarantine(RecoveryFiles.SessionFields(captureId:"cap-8"),_receivedAt.AddMinutes(1));
			Store.DictSlotDocuments.Remove(103);
			CaptureAssignPlan plan8=_harness.Filer.PlanAssign(entry8,0,out error);
			Assert.IsTrue(plan8.IsIntoOriginalSlot,error);
			Store.DictSlotDocuments[103]=new Document { DocNum=901,PatNum=42,MountItemNum=103 };
			CaptureFilingResult result8=_harness.Filer.Assign(entry8,0,0,plan8);
			Assert.IsFalse(result8.IsDone);
			Assert.IsTrue(result8.IsPlanChanged);
			Assert.AreEqual(1,RadiographStore.ListPatientsImported.Count,"Only the copy confirmed above.");
		}

		[TestMethod]
		public void Assign_16Bit_MountDeleted_ASingleDocument8BitCopy_WithTheSlotsTeeth() {
			CaptureFileEntry entry=_harness.Files.Quarantine16(RecoveryFiles.SessionFields(),_receivedAt);
			Store.DictMounts.Remove(7);
			CaptureFilingResult result=_harness.Filer.Assign(entry,20,0);
			Assert.IsTrue(result.IsDone,result.Error);
			Assert.AreEqual(".png",RadiographStore.ListExtensions.Single());
			Assert.AreEqual(0,RadiographStore.ListMountItemsInserted.Count);
			Document document=RadiographStore.ListDocumentsUpdated.Single();
			Assert.AreEqual("6",document.ToothNumbers);
			Assert.AreEqual("Recovered: Slot 3 (teeth 6)",document.Description);
		}

		[TestMethod]
		public void Assign_16Bit_SlotNotRecorded_RecoveredItemWithNoTeeth() {
			Dictionary<string,string> dictFields=RecoveryFiles.SessionFields();
			dictFields[CaptureSidecarKeys.ContinuousEnable]="1";
			dictFields[CaptureSidecarKeys.IndexInEnable]="2";
			CaptureFileEntry entry=_harness.Files.Quarantine16(dictFields,_receivedAt);
			Assert.IsFalse(entry.IsSlotKnown);
			CaptureFilingResult result=_harness.Filer.Assign(entry,0,0);
			Assert.IsTrue(result.IsDone,result.Error);
			Document document=RadiographStore.ListDocumentsUpdated.Single();
			Assert.AreEqual("",document.ToothNumbers,"No teeth when the slot is not known.");
			Assert.AreEqual("Recovered: slot unknown",document.Description);
			Assert.AreEqual(".png",RadiographStore.ListExtensions.Single());
		}

		[TestMethod]
		public void Assign_16Bit_OwnEmptySlot_16BitsNotAllowedNow_The8BitCopyAsThatSlotsDocument_ForEveryRefusal() {
			//Owner decision 2026-11-01, "fall back to 8": a capture of more than 8 bits whose own slot is still empty, but which may not be stored as
			//16-bit now, goes into that slot as the explicit 8-bit copy (the same copy as the slot-taken route), the 16-bit original kept.
			foreach(Gray16Refusal refusal in Gray16Refusal.All()) {
				using(RecoveryHarness harness=new RecoveryHarness()) {
					refusal.ActionSetup(harness);
					harness.Slot(3).RotateOnAcquire=90;
					CaptureFileEntry entry=harness.Files.Quarantine16(refusal.FuncFields(),_receivedAt);
					string error;
					CaptureAssignPlan plan=harness.Filer.PlanAssign(entry,0,out error);
					Assert.IsNotNull(plan,refusal+": "+error);
					Assert.IsTrue(plan.IsIntoOriginalSlot,refusal.Name);
					Assert.IsTrue(plan.Is8BitCopy,refusal.Name);
					Assert.IsFalse(plan.Is16BitAllowed,refusal.Name);
					StringAssert.StartsWith(plan.Reason16BitRefused,refusal.ReasonStart,refusal.Name);
					StringAssert.Contains(plan.Reason16BitRefused,refusal.ReasonContains,refusal+": the gate that refused.");
					Assert.AreEqual("",plan.Description,refusal+": that slot's document, with no label.");
					StringAssert.StartsWith(plan.Note,"8-bit copy: 16 bits are not allowed for it now ("+plan.Reason16BitRefused+"); the 16-bit original is "
						+"kept.",refusal.Name);
					StringAssert.Contains(plan.Note,"stay in Capture\\quarantine-resolved\\"+RecoveryFiles.SessionGuid.ToString("D")+" on this computer.",
						refusal.Name);
					Assert.IsFalse(plan.Note.Contains("can only stay") || plan.Note.Contains("discarded"),refusal+": "+plan.Note);
					string slotLabel="Slot 3 (teeth "+harness.Slot(3).ToothNumbers+")";
					StringAssert.StartsWith(plan.TargetText,slotLabel+" of the mount \"FMX\"",refusal.Name);
					CaptureFilingResult result=harness.Filer.Assign(entry,0,0,plan);
					Assert.IsTrue(result.IsDone,refusal+": "+result.Error);
					Assert.IsTrue(result.IsCopy8BitOf16Bit,refusal.Name);
					Assert.AreEqual(".png",harness.RadiographStore.ListExtensions.Single(),refusal+": an 8-bit PNG, never 16-bit DICOM now.");
					Document document=harness.RadiographStore.ListDocumentsUpdated.Single();
					Assert.AreEqual(103,document.MountItemNum,refusal+": its own slot.");
					Assert.AreSame(harness.Slot(3),result.MountItem,refusal.Name);
					Assert.IsFalse(result.IsMountItemCreated,refusal+": no unmounted item.");
					Assert.AreEqual(0,harness.RadiographStore.ListMountItemsInserted.Count,refusal.Name);
					Assert.AreEqual(harness.Slot(3).ToothNumbers,document.ToothNumbers,refusal+": the slot's teeth.");
					Assert.AreEqual(90,document.DegreesRotated,refusal+": the slot's rotation.");
					Assert.AreEqual("",document.Description,refusal.Name);
					SecurityLog securityLog=harness.Store.ListLogEntries.Single(x => x.PermType==EnumPermType.ImageCreate);
					StringAssert.StartsWith(securityLog.LogText,CaptureRecoveryText.AssignLogPrefix,refusal.Name);
					StringAssert.Contains(securityLog.LogText,"as DocNum "+document.DocNum+", an 8-bit copy of a 16-bit capture (made through the window 1000 to "
						+"3231 of its values; the 16-bit original "+entry.FileName+" and its sidecar are kept), into "+slotLabel+" of the mount",refusal.Name);
					Assert.IsFalse(securityLog.LogText.Contains("quarantine-resolved"),refusal+": names no folder.");
					StringAssert.Contains(harness.RadiographStore.ListLogTexts.Single(),RadiographSavePlanner.Copy8BitText,refusal.Name);
					Assert.AreEqual(" as an 8-bit copy of a 16-bit capture; the 16-bit original is kept in Capture\\quarantine-resolved\\"
						+RecoveryFiles.SessionGuid.ToString("D"),CaptureRecoveryText.GetCopyDoneText(result),refusal.Name);
					AssertOriginalResolved(harness,entry,document.DocNum,1000,3231,message:refusal.Name);
					Assert.AreEqual(0,harness.RadiographStore.CountPearl,refusal+": the copy is never sent.");
				}
			}
		}

		[TestMethod]
		public void Assign_14Bit_OwnEmptySlot_16BitsNotAllowedNow_The8BitCopyInItsSlot_SaysSo() {
			Dictionary<string,string> dictFields=RecoveryFiles.SessionFieldsWithDriver();
			dictFields[CaptureSidecarKeys.BitDepthInEffect]="14";
			dictFields[CaptureSidecarKeys.BitsPerSample]="14";
			CaptureFileEntry entry=_harness.Files.Quarantine16(dictFields,_receivedAt);
			Assert.AreEqual(14,entry.BitDepthInEffect);
			string error;
			CaptureAssignPlan plan=_harness.Filer.PlanAssign(entry,0,out error);
			Assert.IsNotNull(plan,error);
			Assert.IsTrue(plan.IsIntoOriginalSlot);
			Assert.IsTrue(plan.Is8BitCopy,"The capture settings are not verified here.");
			StringAssert.StartsWith(plan.Note,"8-bit copy: 16 bits are not allowed for it now (the capture settings of this device have never been saved in "
				+"Open PMS - Dental on this computer (not verified)); the 16-bit original is kept.");
			CaptureFilingResult result=_harness.Filer.Assign(entry,0,0,plan);
			Assert.IsTrue(result.IsDone,result.Error);
			Assert.AreEqual(".png",RadiographStore.ListExtensions.Single());
			Assert.AreEqual(103,RadiographStore.ListDocumentsUpdated.Single().MountItemNum);
			StringAssert.StartsWith(CaptureRecoveryText.GetCopyDoneText(result)," as an 8-bit copy of a 14-bit capture; the 16-bit original is kept in ");
			AssertOriginalResolved(_harness,entry,result.Document.DocNum,1000,3231,bitDepth:14);
		}

		[TestMethod]
		public void Assign_16BitsAllowedOrNotChangedAfterTheConfirmation_Refused_ThenFiledAsTheNewConfirmationSays() {
			CaptureFileEntry entry=_harness.Files.Quarantine16(RecoveryFiles.SessionFieldsWithDriver(),_receivedAt);
			_harness.SetGatesPassedAt(16);
			string error;
			CaptureAssignPlan plan=_harness.Filer.PlanAssign(entry,0,out error);
			Assert.IsNotNull(plan,error);
			Assert.IsFalse(plan.Is8BitCopy,"Confirmed: 16-bit DICOM into its own empty slot.");
			LocalCaptureConfig configDefault=_harness.Config;
			_harness.Config=CaptureSessionTestConfig.Load("[Twain]","Allow16Bit=0");//more than 8 bits switched off on this computer meanwhile
			CaptureFilingResult result=_harness.Filer.Assign(entry,0,0,plan);
			Assert.IsFalse(result.IsDone);
			Assert.IsTrue(result.IsPlanChanged);
			Assert.AreEqual(CaptureRecoveryText.AssignPlanChanged,result.Error);
			Assert.AreEqual(0,RadiographStore.ListPatientsImported.Count,"Never an 8-bit copy the operator saw no note for.");
			CaptureAssignPlan planNow=_harness.Filer.PlanAssign(entry,0,out error);
			Assert.IsTrue(planNow.IsIntoOriginalSlot);
			Assert.IsTrue(planNow.Is8BitCopy);
			StringAssert.Contains(planNow.Note,"([Twain] Allow16Bit is 0 (more than 8 bits are switched off on this computer))");
			//And the other way: the copy confirmed, 16 bits allowed again meanwhile: not stored as 16-bit DICOM without a new confirmation either.
			_harness.Config=configDefault;
			CaptureFilingResult resultDicom=_harness.Filer.Assign(entry,0,0,planNow);
			Assert.IsFalse(resultDicom.IsDone);
			Assert.IsTrue(resultDicom.IsPlanChanged);
			Assert.AreEqual(0,RadiographStore.ListPatientsImported.Count);
			_harness.Config=CaptureSessionTestConfig.Load("[Twain]","Allow16Bit=0");
			CaptureFilingResult resultCopy=_harness.Filer.Assign(entry,0,0,planNow);
			Assert.IsTrue(resultCopy.IsDone,resultCopy.Error);
			Assert.IsTrue(resultCopy.IsCopy8BitOf16Bit);
			Assert.AreEqual(103,RadiographStore.ListDocumentsUpdated.Single().MountItemNum);
		}

		[TestMethod]
		public void RecoverRecentCaptures_16BitJournalCopy_OwnEmptySlot_16BitsNotAllowedNow_The8BitCopyInItsSlot_OriginalKeptPastRotation() {
			CaptureFileEntry entry=_harness.Files.Journal16(RecoveryFiles.SessionFields(),_receivedAt);//no driver version: never 16-bit
			string error;
			CaptureAssignPlan plan=_harness.Filer.PlanAssign(entry,42,out error);
			Assert.IsNotNull(plan,error);
			Assert.IsTrue(plan.IsIntoOriginalSlot);
			Assert.IsTrue(plan.Is8BitCopy);
			StringAssert.Contains(plan.Note,"stay in "+CaptureFileStore.SubfolderJournalKept);
			CaptureFilingResult result=_harness.Filer.Assign(entry,0,42,plan);
			Assert.IsTrue(result.IsDone,result.Error);
			Assert.AreEqual(".png",RadiographStore.ListExtensions.Single());
			Assert.AreEqual(103,RadiographStore.ListDocumentsUpdated.Single().MountItemNum);
			string pathKept=Path.Combine(_harness.Files.Folder,CaptureFileStore.SubfolderJournalKept,entry.FileName);
			Assert.AreEqual(pathKept,result.PathResolved);
			CollectionAssert.AreEqual(File.ReadAllBytes(entry.ImagePath),File.ReadAllBytes(pathKept),"Kept byte for byte where rotation does not reach.");
			StringAssert.Contains(File.ReadAllText(Path.ChangeExtension(pathKept,".txt")),"DispositionDocNum="+result.Document.DocNum);
			Assert.IsTrue(_harness.Files.Store.ListJournal().Single().IsResolved);
		}

		[TestMethod]
		public void Assign_14BitCaptureOfA16BitEnable_UnderARecordThatPassedAt16_IntoItsEmptySlotAsDicom_EffectiveBits14() {
			//Bits=16 and a record that passed at 16; SET 16 was refused this time, the source ran at 14, and the save failed: the session would
			//have stored it under that record, so recovery decides for the 16 bits asked for, not the 14 in effect.
			Dictionary<string,string> dictFields=RecoveryFiles.SessionFieldsWithDriver();
			dictFields[CaptureSidecarKeys.BitDepthInEffect]="14";
			dictFields[CaptureSidecarKeys.BitsPerSample]="14";
			CaptureFileEntry entry=_harness.Files.Quarantine16(dictFields,_receivedAt);
			Assert.AreEqual(14,entry.BitDepthInEffect);
			Assert.AreEqual("16",entry.Get("RequestedBits"));
			_harness.SetGatesPassedAt(16);
			string error;
			CaptureAssignPlan plan=_harness.Filer.PlanAssign(entry,0,out error);
			Assert.IsNotNull(plan,error);
			Assert.IsFalse(plan.Is8BitCopy,"The 16-bit rules hold: never the 8-bit copy.");
			CaptureFilingResult result=_harness.Filer.Assign(entry,0,0,plan);
			Assert.IsTrue(result.IsDone,result.Error);
			Assert.AreEqual(".dcm",RadiographStore.ListExtensions.Single(),"14-bit data in a 16-bit DICOM file, never an 8-bit copy.");
			Assert.IsFalse(result.IsCopy8BitOf16Bit);
			TestDicomFile testDicomFile=TestDicomReader.Read(RadiographStore.ListBytesImported.Single());
			Assert.AreEqual(16,testDicomFile.Get(0x0028,0x0101).US,"BitsStored 16.");
			StringAssert.Contains(testDicomFile.Get(0x0020,0x4000).Text,"effectiveBits=14");
			Assert.AreEqual(103,RadiographStore.ListDocumentsUpdated.Single().MountItemNum,"Its own empty slot.");
		}

		[TestMethod]
		public void TestCaptureImage_NeverFiledToAPatient_NeitherAssignedNorReassigned_EvenAs8BitCopy() {
			//Test capture asks for 16 bits without any gate, and its polarity check may have failed: its images are never filed, so an Invert in the
			//sidecar is never taken as verified for a patient's copy.
			Dictionary<string,string> dictFields=RecoveryFiles.SessionFields(patNum:0);
			dictFields[CaptureSidecarKeys.TestCapture]="1";
			dictFields["Device.12.Polarity"]="Invert";
			CaptureFileEntry entry=_harness.Files.Journal16(dictFields,_receivedAt);
			Assert.IsTrue(CaptureRecoveryFiler.IsTestCapture(entry));
			Assert.IsFalse(CaptureRecoveryFiler.IsAssignOffered(entry,42));
			Assert.IsFalse(CaptureRecoveryFiler.IsReassignOffered(entry));
			CaptureFilingResult result=_harness.Filer.Reassign(entry,_harness.PatientJane,"It looked like Jane's.",20);
			Assert.IsFalse(result.IsDone);
			Assert.AreEqual(CaptureRecoveryText.TestCaptureNote,result.Error);
			Assert.AreEqual(0,RadiographStore.ListPatientsImported.Count);
			Assert.IsFalse(_harness.Files.Store.ListJournal().Single().IsResolved);
			//A Test capture image from before the key: no patient, and Test capture's own target (journal) or patient name (quarantine).
			Dictionary<string,string> dictFieldsOld=RecoveryFiles.SessionFields(patNum:0,captureId:"cap-8");
			dictFieldsOld["Target"]=ImagingCapture.UI.TestCaptureModel.TargetLabel;
			Assert.IsTrue(CaptureRecoveryFiler.IsTestCapture(_harness.Files.Journal(dictFieldsOld,_receivedAt.AddMinutes(1))));
			Dictionary<string,string> dictFieldsQuarantine=RecoveryFiles.SessionFields(patNum:0,captureId:"cap-9");
			dictFieldsQuarantine["Patient"]=ImagingCapture.UI.TestCaptureModel.QuarantinePatientName;
			CaptureFileEntry entryQuarantine=_harness.Files.Quarantine(dictFieldsQuarantine,_receivedAt.AddMinutes(2));
			Assert.IsTrue(CaptureRecoveryFiler.IsTestCapture(entryQuarantine));
			Assert.IsFalse(CaptureRecoveryFiler.IsReassignOffered(entryQuarantine));
			Assert.IsTrue(CaptureRecoveryFiler.IsDiscardOffered(entryQuarantine),"A test image in the quarantine can still be discarded.");
			Assert.IsFalse(CaptureRecoveryFiler.IsTestCapture(_harness.Files.Quarantine(RecoveryFiles.SessionFields(patNum:0,captureId:"cap-10"),
				_receivedAt.AddMinutes(3))),"Any other image with no patient is reassigned as before.");
		}

		[TestMethod]
		public void Assign_16Bit_PolarityInvert_TheCopyIsInverted() {
			Dictionary<string,string> dictFields=RecoveryFiles.SessionFields();
			dictFields["Device.12.Polarity"]="Invert";//as the library's sidecar records the settings in effect
			CaptureFileEntry entry=_harness.Files.Quarantine16(dictFields,_receivedAt);
			Assert.AreEqual(TwainPolarity.Invert,CaptureRecoveryFiler.GetCapturePolarity(entry));
			Store.DictSlotDocuments[103]=new Document { DocNum=900,PatNum=42,MountItemNum=103 };
			CaptureFilingResult result=_harness.Filer.Assign(entry,0,0);
			Assert.IsTrue(result.IsDone,result.Error);
			Assert.AreEqual(255,GrayOfCopy(0,0),"The lowest raw value is the most radiopaque after the inversion.");
			Assert.AreEqual(0,GrayOfCopy(5,3));
			Assert.AreEqual(TwainPolarity.AsDelivered,CaptureRecoveryFiler.GetCapturePolarity(_harness.Files.Quarantine16(RecoveryFiles.SessionFields(),
				_receivedAt.AddMinutes(1))),"No Polarity in the sidecar: as delivered.");
			//Into its own empty slot as the 8-bit copy ("fall back to 8"): the sidecar's polarity too.
			Store.DictSlotDocuments.Remove(103);
			Dictionary<string,string> dictFieldsOwn=RecoveryFiles.SessionFields(captureId:"cap-8",acquiredAt:"2026-10-31T10:02:04.500+00:00");
			dictFieldsOwn["Device.12.Polarity"]="Invert";
			CaptureFileEntry entryOwn=_harness.Files.Quarantine16(dictFieldsOwn,_receivedAt.AddMinutes(2));
			string error;
			CaptureAssignPlan plan=_harness.Filer.PlanAssign(entryOwn,0,out error);
			Assert.IsNotNull(plan,error);
			Assert.IsTrue(plan.IsIntoOriginalSlot && plan.Is8BitCopy);
			CaptureFilingResult resultOwn=_harness.Filer.Assign(entryOwn,0,0,plan);
			Assert.IsTrue(resultOwn.IsDone,resultOwn.Error);
			Assert.AreEqual(103,RadiographStore.ListDocumentsUpdated.Last().MountItemNum);
			Assert.AreEqual(255,GrayOfCopy(0,0),"Inverted as the capture was taken.");
			Assert.AreEqual(0,GrayOfCopy(5,3));
		}

		[TestMethod]
		public void Assign_16Bit_Vanilla_NotInverted_NoCopy_StaysPut() {
			Dictionary<string,string> dictFields=RecoveryFiles.SessionFields();
			dictFields["PixelFlavor"]="1";//TWPF_VANILLA
			CaptureFileEntry entry=_harness.Files.Quarantine16(dictFields,_receivedAt);
			Store.DictSlotDocuments[103]=new Document { DocNum=900,PatNum=42,MountItemNum=103 };
			CaptureFilingResult result=_harness.Filer.Assign(entry,0,0);
			Assert.IsFalse(result.IsDone);
			StringAssert.Contains(result.Error,"0 as white");
			Assert.AreEqual(1,_harness.Files.CountQuarantineFiles());
		}

		[TestMethod]
		public void RecoverRecentCaptures_16BitJournalCopy_SlotTaken_8BitCopy_OriginalKeptPastRotation() {
			CaptureFileEntry entry=_harness.Files.Journal16(RecoveryFiles.SessionFields(),_receivedAt);
			Store.DictSlotDocuments[103]=new Document { DocNum=900,PatNum=42,MountItemNum=103 };
			string error;
			CaptureAssignPlan plan=_harness.Filer.PlanAssign(entry,42,out error);
			Assert.IsNotNull(plan,error);
			StringAssert.Contains(plan.Note,"stay in "+CaptureFileStore.SubfolderJournalKept);
			CaptureFilingResult result=_harness.Filer.Assign(entry,0,42);
			Assert.IsTrue(result.IsDone,result.Error);
			Assert.AreEqual(".png",RadiographStore.ListExtensions.Single());
			string pathKept=Path.Combine(_harness.Files.Folder,CaptureFileStore.SubfolderJournalKept,entry.FileName);
			Assert.AreEqual(pathKept,result.PathResolved);
			Assert.IsTrue(File.Exists(pathKept),"Kept where the journal's rotation does not reach.");
			CollectionAssert.AreEqual(File.ReadAllBytes(entry.ImagePath),File.ReadAllBytes(pathKept),"Byte for byte.");
			StringAssert.Contains(File.ReadAllText(Path.ChangeExtension(pathKept,".txt")),"DispositionDocNum="+result.Document.DocNum);
			Assert.IsTrue(_harness.Files.Store.ListJournal().Single().IsResolved,"The journal copy itself is marked as filed, as before.");
		}
		[TestMethod]
		public void Reassign_16BitOnlyJournalCopy_TheOriginalKeptBeforeTheCopyIsFiled_TheDispositionOnce() {
			//The quarantine write failed (Delivered=None): the journal's -only copy is the 16-bit original's only copy. Resolve gives it its plain
			//name and marks it as filed, so rotation may remove it: it must be kept first.
			CaptureFileEntry entry=_harness.Files.Journal16(RecoveryFiles.SessionFields(),_receivedAt,"None");
			StringAssert.Contains(entry.FileName,"-only");
			CaptureFilingResult result=_harness.Filer.Reassign(entry,_harness.PatientJohn,"Taken under the wrong chart.",20);
			Assert.IsTrue(result.IsDone,result.Error);
			Assert.AreEqual(".png",RadiographStore.ListExtensions.Single());
			string pathKept=Path.Combine(_harness.Files.Folder,CaptureFileStore.SubfolderJournalKept,entry.FileName);
			Assert.IsTrue(File.Exists(pathKept),"The 16-bit original is kept: "+string.Join(" ",result.ListWarnings));
			Assert.AreEqual(pathKept,result.PathResolved);
			Assert.AreEqual(0,result.ListWarnings.Count,string.Join(" ",result.ListWarnings));
			string textKept=File.ReadAllText(Path.ChangeExtension(pathKept,".txt"));
			Assert.AreEqual(1,textKept.Split(new[] { "Disposition=" },StringSplitOptions.None).Length-1,"The disposition once: "+textKept);
			StringAssert.Contains(textKept,"DispositionDocNum="+result.Document.DocNum);
			StringAssert.Contains(textKept,"DispositionCopy="+RadiographSavePlanner.Copy8BitText);
			CaptureFileEntry entryAfter=_harness.Files.Store.ListJournal().Single();
			Assert.IsTrue(entryAfter.IsResolved,"The journal copy itself is marked as filed, as before.");
			Assert.IsFalse(entryAfter.FileName.Contains("-only"),"Once filed it is no longer the only copy.");
			CollectionAssert.AreEqual(File.ReadAllBytes(entryAfter.ImagePath),File.ReadAllBytes(pathKept),"Byte for byte.");
		}

		[TestMethod]
		public void Assign_16BitJournalCopy_TheOriginalCannotBeKept_NothingFiled() {
			CaptureFileEntry entry=_harness.Files.Journal16(RecoveryFiles.SessionFields(),_receivedAt);
			Store.DictSlotDocuments[103]=new Document { DocNum=900,PatNum=42,MountItemNum=103 };
			string folderKept=Path.Combine(_harness.Files.Folder,CaptureFileStore.SubfolderJournalKept);
			Directory.CreateDirectory(folderKept);
			File.WriteAllBytes(Path.Combine(folderKept,entry.FileName),new byte[] { 1,2,3 });//another file of the same name
			CaptureFilingResult result=_harness.Filer.Assign(entry,0,42);
			Assert.IsFalse(result.IsDone);
			StringAssert.StartsWith(result.Error,"The 16-bit original could not be kept in "+CaptureFileStore.SubfolderJournalKept);
			StringAssert.EndsWith(result.Error,"so its 8-bit copy was not filed.");
			Assert.AreEqual(0,RadiographStore.ListPatientsImported.Count,"Nothing filed.");
			Assert.IsFalse(_harness.Files.Store.ListJournal().Single().IsResolved,"The journal copy is still offered.");
			CollectionAssert.AreEqual(new byte[] { 1,2,3 },File.ReadAllBytes(Path.Combine(folderKept,entry.FileName)),"Never overwritten.");
		}
		#endregion The 16-bit filing route

		#region Discard
		[TestMethod]
		public void Discard_NeedsImageDelete_LoggedWithTheSessionThePatientAndTheFile_Kept() {
			CaptureFileEntry entry=_harness.Files.Quarantine(RecoveryFiles.SessionFields(),_receivedAt);
			Store.HashSetDenied.Add(EnumPermType.ImageDelete);
			CaptureFilingResult resultDenied=_harness.Filer.Discard(entry);
			Assert.AreEqual("Not authorized for ImageDelete.",resultDenied.Error);
			Assert.AreEqual(_receivedAt,Store.ListAuthorizationChecks.Single().Item2,"Date-restricted by the receive time.");
			Assert.AreEqual(1,_harness.Files.CountQuarantineFiles());
			Store.HashSetDenied.Clear();
			CaptureFilingResult result=_harness.Filer.Discard(entry);
			Assert.IsTrue(result.IsDone,result.Error);
			SecurityLog securityLog=Store.ListLogEntries.Single();
			Assert.AreEqual(EnumPermType.ImageDelete,securityLog.PermType);
			Assert.AreEqual(42,securityLog.PatNum,"Under the bound patient.");
			Assert.AreEqual(CaptureRecoveryText.DiscardLogPrefix+"file "+entry.FileName+" (unassigned captures, session "+RecoveryFiles.SessionGuid.ToString("D")
				+", received 2026-10-31 10:00:05, source \"Example Sensor DS\", captureId \"cap-7\", sequence 3, slot Slot 3 (teeth 6)), bound patient: "
				+"Doe, Jane (PatNum 42). The image and its sidecar are kept in Capture\\quarantine-resolved\\"+RecoveryFiles.SessionGuid.ToString("D")+".",
				securityLog.LogText);
			Assert.AreEqual(0,_harness.Files.CountQuarantineFiles());
			Assert.IsTrue(File.Exists(result.PathResolved));
			Assert.AreEqual("Discarded",ReadSidecar(result.PathResolved)[CaptureSidecarKeys.Disposition]);
			Assert.AreEqual(0,RadiographStore.ListPatientsImported.Count,"Nothing filed.");
		}

		[TestMethod]
		public void Discard_LogFails_NotDiscarded() {
			CaptureFileEntry entry=_harness.Files.Quarantine(RecoveryFiles.SessionFields(patNum:0),_receivedAt);
			Store.ThrowOnMakeLogEntry="database gone";
			CaptureFilingResult result=_harness.Filer.Discard(entry);
			Assert.IsFalse(result.IsDone);
			StringAssert.Contains(result.Error,"was not discarded");
			Assert.AreEqual(1,_harness.Files.CountQuarantineFiles());
		}

		[TestMethod]
		public void Discard_PatientUnknown_LoggedWithNoPatient() {
			CaptureFileEntry entry=_harness.Files.Quarantine(RecoveryFiles.SessionFields(patNum:0),_receivedAt,isRoot:true);
			Assert.IsTrue(_harness.Filer.Discard(entry).IsDone);
			SecurityLog securityLog=Store.ListLogEntries.Single();
			Assert.AreEqual(0,securityLog.PatNum);
			StringAssert.Contains(securityLog.LogText,"bound patient: patient unknown. The image and its sidecar are kept in Capture\\quarantine-resolved.");
		}

		[TestMethod]
		public void NoAutomaticPurge_FilesStayUntilAnAction() {
			_harness.Files.Quarantine(RecoveryFiles.SessionFields(),new DateTime(2025,1,1));
			List<CaptureFileEntry> listEntries=_harness.Files.Store.ListQuarantine();
			Assert.AreEqual(1,listEntries.Count,"Listing never removes an old file.");
			Assert.IsTrue(listEntries[0].IsOverdue(new DateTime(2026,10,31)));
			new CaptureRecoveryFiler(_harness.Services).PlanAssign(listEntries[0],0,out string error);
			Assert.AreEqual(1,_harness.Files.CountQuarantineFiles(),"Planning moves nothing.");
		}
		#endregion Discard

		#region Texts
		[TestMethod]
		public void Texts_PatientLinesAndLabels() {
			Assert.AreEqual("Recovered: Slot 2 (teeth 4,5)",CaptureRecoveryText.GetRecoveredLabel(" Slot 2 (teeth 4,5) "));
			Assert.AreEqual("Recovered: slot unknown",CaptureRecoveryText.GetRecoveredLabel(""));
			Assert.AreEqual("Jane Doe, born "+new DateTime(1980,1,2).ToShortDateString()+" (PatNum 42)",
				CaptureRecoveryText.GetPatientLine(_harness.PatientJane));
			Assert.AreEqual("Patient unknown",CaptureRecoveryText.GetPatientLine(null));
			Patient patientInactive=_harness.PatientJane.Copy();
			patientInactive.PatStatus=PatientStatus.Inactive;
			Assert.AreEqual("Jane Doe, born "+new DateTime(1980,1,2).ToShortDateString()+" (PatNum 42, Inactive)",CaptureRecoveryText.GetPatientLine(patientInactive));
			patientInactive.PatStatus=PatientStatus.NonPatient;
			StringAssert.EndsWith(CaptureRecoveryText.GetPatientLine(patientInactive),"(PatNum 42, Non-patient)");
			Assert.AreEqual("Date of birth not entered",CaptureRecoveryText.GetBirthdateText(new Patient { PatNum=1 }));
			Assert.AreEqual("Doe, Jane (PatNum 42)",CaptureRecoveryText.GetPatientLogText(_harness.PatientJane));
		}
		#endregion Texts

		private static Dictionary<string,string> ReadSidecar(string pathImage) {
			Dictionary<string,string> dictFields=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
			foreach(string line in File.ReadAllLines(Path.ChangeExtension(pathImage,".txt"))) {
				int idx=line.IndexOf('=');
				if(line.StartsWith(";") || idx<=0) {
					continue;
				}
				dictFields[line.Substring(0,idx)]=line.Substring(idx+1);
			}
			return dictFields;
		}
	}
}
