// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using ImagingCapture.Recovery;
using ImagingCapture.UI;
using ImagingDeviceManager.Capture;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace ImagingCapture.Tests {
	///<summary>CaptureRecoveryModel, the quarantine window's state (design 7.1, 7.12, 10.6): what each view lists, which actions each capture
	///offers, the permission check when an action is begun, Reassign with no preselected patient, and the journal's own-patient copies that
	///have no document yet.</summary>
	[TestClass]
	public class CaptureRecoveryModelTests {
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

		private CaptureRecoveryModel Model(CaptureRecoveryView view,Patient patientSession=null) {
			CaptureRecoveryModel model=new CaptureRecoveryModel(new CaptureRecoveryContext { View=view,SessionPatient=patientSession },_harness.Services);
			model.Load();
			return model;
		}

		[TestMethod]
		public void Unassigned_ListsTheQuarantine_PatientUnknownOnlyThroughReassign() {
			_harness.Files.Quarantine(RecoveryFiles.SessionFields(),_receivedAt);
			_harness.Files.Quarantine(RecoveryFiles.SessionFields(patNum:0),_receivedAt.AddMinutes(1),isRoot:true);
			_harness.Files.Journal(RecoveryFiles.SessionFields(),_receivedAt);
			CaptureRecoveryModel model=Model(CaptureRecoveryView.Unassigned);
			Assert.AreEqual("Unassigned Captures",model.WindowTitle);
			Assert.AreEqual(2,model.Items.Count,"Journal copies are not unassigned captures.");
			CaptureRecoveryItem itemUnknown=model.Items[0];
			Assert.IsTrue(itemUnknown.Entry.IsPatientUnknown);
			Assert.IsFalse(itemUnknown.IsAssignOffered);
			Assert.IsTrue(itemUnknown.IsReassignOffered);
			Assert.IsTrue(itemUnknown.IsDiscardOffered);
			Assert.AreEqual("Reassign only: no originating patient.",itemUnknown.NoteText);
			StringAssert.StartsWith(itemUnknown.TitleText,"patient unknown | 2026-10-31 10:01:05");
			CaptureRecoveryItem itemJane=model.Items[1];
			Assert.IsTrue(itemJane.IsAssignOffered);
			Assert.AreEqual("Doe, Jane | 2026-10-31 10:00:05 | Slot 3 (teeth 6)",itemJane.TitleText);
			Assert.AreEqual(0,model.IdxSelected,"The first is selected.");
			Assert.IsFalse(model.IsAssignEnabled);
			model.Select(1);
			Assert.IsTrue(model.IsAssignEnabled);
			StringAssert.Contains(model.DetailsText,"Session: "+RecoveryFiles.SessionGuid.ToString("D"));
			StringAssert.Contains(model.DetailsText,"Windows user: CLINIC\\xray");
			StringAssert.Contains(model.DetailsText,"Acquired at: 2026-10-31T10:00:04.500+00:00");
		}

		[TestMethod]
		public void Assign_PermissionWhenBegun_ThenTheExplicitConfirmation() {
			_harness.Files.Quarantine(RecoveryFiles.SessionFields(),_receivedAt);
			CaptureRecoveryModel model=Model(CaptureRecoveryView.Unassigned);
			_harness.Store.HashSetDenied.Add(EnumPermType.ImageCreate);
			Assert.IsFalse(model.BeginAssign());
			Assert.AreEqual("Not authorized for ImageCreate.",model.ErrorText);
			Assert.AreEqual(CaptureRecoveryStep.None,model.Step);
			_harness.Store.HashSetDenied.Clear();
			Assert.IsTrue(model.BeginAssign());
			Assert.AreEqual(CaptureRecoveryStep.ConfirmAssign,model.Step);
			Assert.AreEqual("",model.ErrorText);
			Assert.AreEqual("Patient: Jane Doe, born "+new DateTime(1980,1,2).ToShortDateString()+" (PatNum 42)",model.StepPatientText);
			StringAssert.Contains(model.StepDetailsText,"It goes into Slot 3 (teeth 6) of the mount \"FMX\"");
			Assert.AreEqual("Assign to Jane Doe",model.ConfirmText);
			Assert.AreEqual(20,model.CategoryDefNum);
			Assert.AreEqual(0,_harness.RadiographStore.ListPatientsImported.Count,"Nothing is filed before the confirmation.");
			Assert.IsTrue(model.Confirm(""));
			Assert.AreEqual(CaptureRecoveryStep.None,model.Step);
			Assert.IsTrue(model.Items[0].IsDone);
			Assert.IsFalse(model.IsAssignEnabled);
			StringAssert.StartsWith(model.ResultText,"Assigned: DocNum 1000 in Slot 3 (teeth 6)");
			Assert.AreEqual(42,model.ListResults.Single().PatNum);
		}

		[TestMethod]
		public void Reassign_NeverPreselected_NotEvenTheSessionsPatient_NeedsAFoundPatientAndAReason() {
			_harness.Files.Quarantine(RecoveryFiles.SessionFields(patNum:0),_receivedAt);
			CaptureRecoveryModel model=Model(CaptureRecoveryView.Unassigned,_harness.PatientJane);
			Assert.IsTrue(model.BeginReassign());
			Assert.AreEqual(CaptureRecoveryStep.Reassign,model.Step);
			Assert.IsNull(model.ReassignPatient,"No patient is preselected.");
			Assert.IsFalse(model.IsConfirmEnabled);
			Assert.AreEqual("Taken for: Patient unknown\r\nReassign to: (find the patient)",model.StepPatientText);
			StringAssert.Contains(model.StepDetailsText,"Received 2026-10-31 10:00:05.");
			Assert.IsFalse(model.Confirm("reason"));
			Assert.AreEqual("Find the patient to reassign it to first.",model.ErrorText);
			Assert.AreEqual(0,_harness.RadiographStore.ListPatientsImported.Count);
			Assert.IsTrue(model.SetReassignPatient(77));
			Assert.AreEqual("Taken for: Patient unknown\r\nReassign to: John Roe, born "+new DateTime(1975,5,6).ToShortDateString()+" (PatNum 77)",
				model.StepPatientText);
			Assert.AreEqual("Reassign to John Roe",model.ConfirmText);
			Assert.IsFalse(model.Confirm("  "));
			Assert.AreEqual("Type the reason for the reassignment.",model.ErrorText);
			Assert.IsTrue(model.Confirm("Checked against the chart."));
			Assert.AreEqual(77,_harness.RadiographStore.ListPatientsImported.Single().PatNum);
			Assert.AreEqual(77,model.ListResults.Single().PatNum);
		}

		[TestMethod]
		public void Reassign_TheFoundPatientIsClearedByANewSelectionOrStep() {
			_harness.Files.Quarantine(RecoveryFiles.SessionFields(patNum:0),_receivedAt);
			_harness.Files.Quarantine(RecoveryFiles.SessionFields(patNum:0),_receivedAt.AddMinutes(1));
			CaptureRecoveryModel model=Model(CaptureRecoveryView.Unassigned);
			model.BeginReassign();
			model.SetReassignPatient(77);
			model.Select(1);
			Assert.AreEqual(CaptureRecoveryStep.None,model.Step);
			Assert.IsNull(model.ReassignPatient);
			model.BeginReassign();
			Assert.IsNull(model.ReassignPatient,"A new Reassign starts with no patient.");
			model.SetReassignPatient(77);
			model.CancelStep();
			model.BeginReassign();
			Assert.IsNull(model.ReassignPatient);
		}

		[TestMethod]
		public void Reassign_And_Discard_PermissionsWhenBegun_DateRestricted() {
			_harness.Files.Quarantine(RecoveryFiles.SessionFields(),_receivedAt);
			CaptureRecoveryModel model=Model(CaptureRecoveryView.Unassigned);
			_harness.Store.HashSetDenied.Add(EnumPermType.ImageEdit);
			_harness.Store.HashSetDenied.Add(EnumPermType.ImageDelete);
			Assert.IsFalse(model.BeginReassign());
			Assert.AreEqual("Not authorized for ImageEdit.",model.ErrorText);
			Assert.IsFalse(model.BeginDiscard());
			Assert.AreEqual("Not authorized for ImageDelete.",model.ErrorText);
			Assert.IsTrue(_harness.Store.ListAuthorizationChecks.All(x => x.Item2==_receivedAt));
			_harness.Store.HashSetDenied.Clear();
			Assert.IsTrue(model.BeginDiscard());
			Assert.AreEqual("Bound patient: Doe, Jane (PatNum 42)",model.StepPatientText);
			Assert.IsTrue(model.Confirm(""));
			Assert.AreEqual("Discarded.",model.ResultText);
			Assert.AreEqual(0,_harness.Files.CountQuarantineFiles());
		}

		[TestMethod]
		public void Recover_TheSessionPatientsCopiesWithNoDocumentYet() {
			CaptureFileEntry entryMissing=_harness.Files.Journal(RecoveryFiles.SessionFields(captureId:"cap-1",acquiredAt:"2026-10-31T09:00:00.000+00:00"),
				_receivedAt);
			CaptureFileEntry entrySaved=_harness.Files.Journal(RecoveryFiles.SessionFields(captureId:"cap-2",acquiredAt:"2026-10-31T09:01:00.000+00:00"),
				_receivedAt.AddMinutes(1));
			_harness.Files.Journal(RecoveryFiles.SessionFields(captureId:"cap-3"),_receivedAt.AddMinutes(2),"Quarantine");
			_harness.Files.Journal(RecoveryFiles.SessionFields(patNum:77,captureId:"cap-4"),_receivedAt.AddMinutes(3));
			_harness.Files.Journal(RecoveryFiles.SessionFields(patNum:0,captureId:"cap-5"),_receivedAt.AddMinutes(4));
			DateTime dateSaved=CaptureJournalMatch.GetExpectedDateCreated(entrySaved);
			_harness.Store.ListPatientDocuments.Add(new Document { DocNum=800,PatNum=42,
				DateCreated=new DateTime(dateSaved.Year,dateSaved.Month,dateSaved.Day,dateSaved.Hour,dateSaved.Minute,dateSaved.Second) });
			CaptureRecoveryModel model=Model(CaptureRecoveryView.Journal,_harness.PatientJane);
			Assert.AreEqual("Recover Recent Captures for Jane Doe",model.WindowTitle);
			Assert.IsTrue(model.IsOthersOptionVisible);
			Assert.AreEqual(entryMissing.ImagePath,model.Items.Single().Entry.ImagePath,
				"The saved copy, the quarantined copy and other patients' copies are not listed.");
			Assert.IsTrue(model.Items[0].IsAssignOffered);
			Assert.IsTrue(model.Items[0].IsReassignOffered);
			Assert.IsFalse(model.Items[0].IsDiscardOffered);
			Assert.IsFalse(model.IsDiscardVisible);
			model.SetOthersShown(true);
			Assert.AreEqual(3,model.Items.Count);
			List<CaptureRecoveryItem> listOthers=model.Items.Skip(1).ToList();
			Assert.IsTrue(listOthers.All(x => !x.IsAssignOffered && x.IsReassignOffered),"Other patients' copies and copies with no patient: Reassign only.");
			Assert.AreEqual("Reassign only: a copy from another patient's session.",listOthers.Single(x => x.Entry.PatNum==77).NoteText);
			Assert.AreEqual("Reassign only: no originating patient.",listOthers.Single(x => x.Entry.PatNum==0).NoteText);
			model.Select(0);
			Assert.IsTrue(model.BeginAssign());
			Assert.IsTrue(model.Confirm(""));
			Assert.AreEqual(42,_harness.RadiographStore.ListPatientsImported.Single().PatNum);
			model.Load();
			Assert.IsFalse(model.Items.Any(x => x.Entry.ImagePath==entryMissing.ImagePath),"Filed: not listed again.");
		}

		[TestMethod]
		public void Recover_DocumentsUnreadable_NothingOfThePatientOffered() {
			_harness.Files.Journal(RecoveryFiles.SessionFields(),_receivedAt);
			FakeCaptureRecoveryStoreThrowing store=new FakeCaptureRecoveryStoreThrowing(_harness.Store);
			CaptureRecoveryServices services=_harness.Services;
			services.Store=store;
			CaptureRecoveryModel model=new CaptureRecoveryModel(new CaptureRecoveryContext { View=CaptureRecoveryView.Journal,
				SessionPatient=_harness.PatientJane },services);
			model.Load();
			Assert.AreEqual(0,model.Items.Count);
			StringAssert.Contains(model.ErrorText,"cannot be found");
		}

		[TestMethod]
		public void Journal_OutsideASession_EveryCopyReassignOnly() {
			_harness.Files.Journal(RecoveryFiles.SessionFields(),_receivedAt);
			_harness.Files.Journal(RecoveryFiles.SessionFields(patNum:0),_receivedAt.AddMinutes(1));
			CaptureRecoveryModel model=Model(CaptureRecoveryView.Journal);
			Assert.AreEqual("Recent Captures",model.WindowTitle);
			Assert.IsFalse(model.IsOthersOptionVisible);
			Assert.AreEqual(2,model.Items.Count);
			Assert.IsTrue(model.Items.All(x => !x.IsAssignOffered && x.IsReassignOffered && !x.IsDiscardOffered));
			Assert.AreEqual("PatNum 42 | 2026-10-31 10:00:05 | Slot 3 (teeth 6)",model.Items[1].TitleText);
		}

		[TestMethod]
		public void SetView_SwitchesTheList() {
			_harness.Files.Quarantine(RecoveryFiles.SessionFields(),_receivedAt);
			CaptureRecoveryModel model=Model(CaptureRecoveryView.Unassigned);
			Assert.AreEqual(1,model.Items.Count);
			model.SetView(CaptureRecoveryView.Journal);
			Assert.AreEqual(0,model.Items.Count);
			Assert.AreEqual("The capture journal on this computer is empty.",model.EmptyText);
		}

		[TestMethod]
		public void Overdue_FlaggedInTheNote() {
			_harness.Files.Quarantine(RecoveryFiles.SessionFields(),new DateTime(2026,9,1));
			CaptureRecoveryModel model=Model(CaptureRecoveryView.Unassigned);
			Assert.IsTrue(model.Items[0].IsOverdue);
			Assert.AreEqual("More than 30 days old.",model.Items[0].NoteText);
		}

		[TestMethod]
		public void Pictures_ThumbnailAndPreview_Downscaled() {
			_harness.Files.Quarantine(RecoveryFiles.SessionFields(),_receivedAt);
			CaptureRecoveryModel model=Model(CaptureRecoveryView.Unassigned);
			string error;
			CapturePreviewPixels pixels=model.GetThumbnail(model.Items[0],out error);
			Assert.IsNotNull(pixels,error);
			Assert.AreEqual(6,pixels.Width,"Smaller than the thumbnail: unchanged.");
			Assert.AreSame(pixels,model.GetThumbnail(model.Items[0],out error),"Made once.");
			Assert.IsNotNull(model.GetPreview(out error),error);
			CapturePreviewPixels pixelsLarge=new CapturePreviewPixels { Width=200,Height=100,IsGray=true,Pixels=Enumerable.Range(0,20000).Select(x => (byte)x).ToArray() };
			CapturePreviewPixels pixelsSmall=CaptureRecoveryModel.Downscale(pixelsLarge,72);
			Assert.AreEqual(72,pixelsSmall.Width);
			Assert.AreEqual(36,pixelsSmall.Height);
			Assert.AreEqual(72*36,pixelsSmall.Pixels.Length);
		}

		///<summary>The fake store whose document reads fail.</summary>
		private sealed class FakeCaptureRecoveryStoreThrowing:ICaptureRecoveryStore {
			private readonly FakeCaptureRecoveryStore _store;

			public FakeCaptureRecoveryStoreThrowing(FakeCaptureRecoveryStore store) {
				_store=store;
			}

			public bool IsAuthorized(EnumPermType permType,DateTime date,out string message) {
				return _store.IsAuthorized(permType,date,out message);
			}

			public Patient GetPatient(long patNum) {
				return _store.GetPatient(patNum);
			}

			public Mount GetMount(long mountNum) {
				return _store.GetMount(mountNum);
			}

			public List<MountItem> GetMountItems(long mountNum) {
				return _store.GetMountItems(mountNum);
			}

			public List<Document> GetDocumentsForMountItems(List<MountItem> listMountItems) {
				return _store.GetDocumentsForMountItems(listMountItems);
			}

			public List<Document> GetPatientDocuments(long patNum) {
				throw new Exception("database gone");
			}

			public List<SecurityLog> GetImageCreateLogEntries(long patNum,List<long> listDocNums) {
				throw new Exception("database gone");
			}

			public bool IsCategoryValid(long defNum) {
				return _store.IsCategoryValid(defNum);
			}

			public List<Def> GetImageCategories() {
				return _store.GetImageCategories();
			}

			public long GetDefaultProvNum(Patient patient) {
				return _store.GetDefaultProvNum(patient);
			}

			public ImagingDevice GetImagingDevice(long imagingDeviceNum) {
				return _store.GetImagingDevice(imagingDeviceNum);
			}

			public List<SecurityLog> GetSetupLogEntries(long imagingDeviceNum) {
				return _store.GetSetupLogEntries(imagingDeviceNum);
			}

			public void MakeLogEntry(EnumPermType permType,long patNum,string logText,long fKey) {
				_store.MakeLogEntry(permType,patNum,logText,fKey);
			}

			public string GetUserName() {
				return _store.GetUserName();
			}
		}
	}
}
