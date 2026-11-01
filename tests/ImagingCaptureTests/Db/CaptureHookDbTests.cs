// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ForkMigrationTests;
using ImagingCapture.Session;
using ImagingCapture.Settings;
using ImagingDeviceManager.Capture;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace ImagingCapture.Tests.Db {
	///<summary>The hook's database path (ControlImages.Acquire.cs, design 7.3 "The hook") on the scratch database (CaptureDb), through the same
	///business calls the partial makes (CaptureHookMirror) and the launcher on the real stores: the rollback, the launcher's refusals and what they
	///write, and the empty-mount rule (a mount created for the session and still empty is deleted and logged; one holding a document is kept).</summary>
	[TestClass]
	public class CaptureHookDbTests {
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

		private List<string> ImageCreateTexts() {
			return CaptureDb.GetLogsForPatient(EnumPermType.ImageCreate,_kit.Patient.PatNum).Select(x => x.LogText).ToList();
		}

		private string MountCreatedText(Mount mount) {
			return "Mount Created: "+mount.Description+" with category "+_kit.Category.ItemName;
		}

		private string MountDeletedText(Mount mount,string why) {
			return "Mount Deleted: "+mount.Description+" with category "+_kit.Category.ItemName+", "+why;
		}

		#region The mirror
		[TestMethod]
		public void Mirror_MatchesThePartial() {
			string text=File.ReadAllText(TestDb.FindRepoFile(Path.Combine("OpenDental","Main Modules","ControlImages.Acquire.cs")));
			List<string> listLines=text.Split('\n').Select(x => x.Trim()).ToList();
			foreach(string statement in CaptureHookMirror.ArrayStatementsCopied) {
				Assert.IsTrue(listLines.Any(x => x.StartsWith(statement,StringComparison.Ordinal)),"The partial no longer has this statement, which "
					+"CaptureHookMirror copies; update the mirror: "+statement);
			}
			string code=string.Join("\n",listLines.Where(x => !x.StartsWith("//",StringComparison.Ordinal)).Select(x => {
				int idx=x.IndexOf("//",StringComparison.Ordinal);
				return idx==-1 ? x : x.Substring(0,idx);
			}));
			foreach(KeyValuePair<string,int> pair in CaptureHookMirror.DictCallCounts) {
				//The class name as a whole word: "arrayDocuments.Any" is no call of Documents.
				int count=Regex.Matches(code,@"(?<![A-Za-z0-9_])"+Regex.Escape(pair.Key)).Count;
				Assert.AreEqual(pair.Value,count,"The partial's calls of "+pair.Key+" changed; update CaptureHookMirror.");
			}
		}
		#endregion The mirror

		#region NotStarted: the legacy loop runs
		[TestMethod]
		public void SessionOff_TheRollback_TheLegacyLoopRuns_TheNewMountStays_NothingLogged() {
			_kit=new CaptureSessionDbKit("hookoff",isSettingsSaved:false);
			CaptureDb.WriteCaptureIni(_kit.Folder,"[Session]","Enabled=0");
			Mount mount=_kit.CreateMount4Bw();
			CaptureHookOutcome outcome=_kit.RunHook(mount,_kit.MountDef4Bw);
			Assert.IsFalse(outcome.IsHandled,"TryAcquireWithCaptureSession returns false: the legacy loop runs.");
			Assert.AreEqual(CaptureSessionLaunchKind.NotStarted,outcome.Result.Kind);
			Assert.IsFalse(outcome.Result.IsEmptyMountToDelete,"The legacy loop uses the mount.");
			Assert.IsTrue(CaptureDb.MountExists(mount.MountNum));
			Assert.AreEqual(4,CaptureDb.GetMountItemRows(mount.MountNum).Count);
			CollectionAssert.AreEqual(new[] { MountCreatedText(mount) },ImageCreateTexts(),"Only ControlImages' own entry.");
			Assert.AreEqual(0,_kit.ListNotices.Count,"No notice with the rollback.");
			Assert.AreEqual(0,_kit.WindowRunner.CountShown);
			Assert.AreEqual(0,_kit.ListSessions.Count);
		}

		[TestMethod]
		public void SettingsNeverSaved_NotStarted_TheOperatorIsTold_TheNewMountStaysForTheLegacyLoop() {
			_kit=new CaptureSessionDbKit("hooknever",isSettingsSaved:false);
			Mount mount=_kit.CreateMount4Bw();
			CaptureHookOutcome outcome=_kit.RunHook(mount,_kit.MountDef4Bw);
			Assert.IsFalse(outcome.IsHandled);
			Assert.AreEqual(CaptureSessionLaunchKind.NotStarted,outcome.Result.Kind,"Design 9.3: a Setup save is needed before the session can be enabled.");
			StringAssert.Contains(outcome.Result.Reason,"NeverSaved");
			Assert.AreEqual(1,_kit.ListNotices.Count);
			StringAssert.StartsWith(_kit.ListNotices[0],CaptureSettingsCheck.TextNeverSaved);
			StringAssert.EndsWith(_kit.ListNotices[0],CaptureSessionText.LegacyLoopInstead);
			Assert.IsTrue(_kit.ListLog.Any(x => x.StartsWith("Capture session: Notice: "+CaptureSettingsCheck.TextNeverSaved)),"The capture log has it.");
			Assert.IsTrue(_kit.ListLog.Any(x => x.StartsWith("Capture session: Not started for patient "+_kit.Patient.PatNum+": NotStarted, ")));
			Assert.IsTrue(CaptureDb.MountExists(mount.MountNum));
			CollectionAssert.AreEqual(new[] { MountCreatedText(mount) },ImageCreateTexts(),"The security log gets nothing: the legacy loop runs.");
			Assert.AreEqual(0,_kit.WindowRunner.CountShown);
		}

		[TestMethod]
		public void SettingsChangedOutside_NotStarted_UntilASetupUserAcceptsThem_ThenTheSessionStarts() {
			_kit=new CaptureSessionDbKit("hookoutside");
			string path=Path.Combine(_kit.Folder,"capture.ini");
			File.AppendAllText(path,"[Twain]\r\nAllow16Bit=0\r\n");//a hand edit of a gated key (1 is the default since 2026-11-01, so 0 changes it)
			Mount mount=_kit.CreateMount4Bw();
			CaptureHookOutcome outcome=_kit.RunHook(mount,_kit.MountDef4Bw);
			Assert.AreEqual(CaptureSessionLaunchKind.NotStarted,outcome.Result.Kind,"The safe value of [Session] Enabled is off.");
			StringAssert.StartsWith(_kit.ListNotices.Single(),CaptureSettingsCheck.TextChangedOutside);
			StringAssert.Contains(_kit.ListNotices.Single(),"Twain.Allow16Bit","It names what changed.");
			Assert.AreEqual(0,_kit.WindowRunner.CountShown);
			//A Setup user reviews the file in Capture settings and saves it without changes; the next Mount/Acquire runs the session.
			CaptureDb.SaveSettings(_kit.ImagingDevice,_kit.Folder);
			outcome=_kit.RunHook(mount,_kit.MountDef4Bw);
			Assert.AreEqual(CaptureSessionLaunchKind.Completed,outcome.Result.Kind,outcome.Result.Reason);
			Assert.AreEqual(1,_kit.WindowRunner.CountShown);
		}
		#endregion NotStarted: the legacy loop runs

		#region Refused
		[TestMethod]
		public void NewMount_TheMainWindowShowsAnotherPatient_Refused_TheEmptyMountIsDeletedAndLogged() {
			_kit=new CaptureSessionDbKit("hookpat");
			Patient patientOther=CaptureDb.CreatePatient("HookOther",_kit.ProvNum);
			_kit.PatNumCur=patientOther.PatNum;
			Mount mount=_kit.CreateMount4Bw();
			CaptureHookOutcome outcome=_kit.RunHook(mount,_kit.MountDef4Bw);
			Assert.IsTrue(outcome.IsHandled,"The legacy loop does not run either.");
			Assert.AreEqual(CaptureSessionLaunchKind.Refused,outcome.Result.Kind);
			string reason="The patient in the main window is not "+_kit.Patient.GetNameFL()+" any more. Select the patient again before taking X-rays.";
			Assert.AreEqual(reason,outcome.Result.Reason);
			Assert.IsTrue(outcome.Result.IsEmptyMountToDelete);
			Assert.IsTrue(outcome.IsMountDeleted);
			Assert.IsFalse(CaptureDb.MountExists(mount.MountNum),"The mount row is deleted.");
			Assert.AreEqual(0,CaptureDb.GetMountItemRows(mount.MountNum).Count,"Its items too.");
			//Design 7.3: the refusal is logged under ImageCreate, in the deletion's entry; no second entry.
			CollectionAssert.AreEqual(new[] { MountCreatedText(mount),MountDeletedText(mount,"the capture session refused to start: "+reason) },ImageCreateTexts());
			SecurityLog securityLog=CaptureDb.GetLogsForPatient(EnumPermType.ImageCreate,_kit.Patient.PatNum).Last();
			Assert.AreEqual(Security.CurUser.UserNum,securityLog.UserNum);
			Assert.IsTrue(CaptureDb.HasLogHash(securityLog.SecurityLogNum));
			Assert.AreEqual(0,CaptureDb.GetLogsForPatient(EnumPermType.ImageCreate,patientOther.PatNum).Count,"Nothing for the other patient.");
			Assert.AreEqual(0,CaptureDb.GetDocuments(patientOther.PatNum).Count);
			CollectionAssert.AreEqual(new[] { reason },outcome.ListMessages,"The partial shows the reason.");
			Assert.AreEqual(0,_kit.WindowRunner.CountShown,"No window, no source.");
			Assert.AreEqual(0,_kit.ListSessions.Count);
		}

		[TestMethod]
		public void NewMount_FromADefinitionWithAScale_Refused_TheDeletedMountLeavesNoRowBehind() {
			//Decision 13: the clinic sets the scale of a mount definition. Mounts.CreateMountFromDef then adds an imagedraw row (ScaleValue) for
			//the new mount; deleting the empty mount must not leave it behind.
			_kit=new CaptureSessionDbKit("hookscale");
			_kit.MountDef4Bw.ScaleValue="50 1 mm";
			MountDefs.Update(_kit.MountDef4Bw);
			_kit.PatNumCur=CaptureDb.CreatePatient("HookScaleOther",_kit.ProvNum).PatNum;
			Mount mount=_kit.CreateMount4Bw();
			Assert.AreEqual(1,ImageDraws.RefreshForMount(mount.MountNum).Count,"The scale of the new mount.");
			CaptureHookOutcome outcome=_kit.RunHook(mount,_kit.MountDef4Bw);
			Assert.AreEqual(CaptureSessionLaunchKind.Refused,outcome.Result.Kind);
			Assert.IsTrue(outcome.IsMountDeleted);
			Assert.IsFalse(CaptureDb.MountExists(mount.MountNum));
			Assert.AreEqual(0,CaptureDb.GetMountItemRows(mount.MountNum).Count);
			Assert.AreEqual(0,ImageDraws.RefreshForMount(mount.MountNum).Count,"An imagedraw row of a deleted mount is left behind.");
		}

		[TestMethod]
		public void ExistingMount_TheMainWindowShowsAnotherPatient_Refused_TheMountIsKept_TheRefusalIsLogged() {
			_kit=new CaptureSessionDbKit("hookpatold");
			Mount mount=_kit.CreateMount4Bw();
			_kit.PatNumCur=CaptureDb.CreatePatient("HookOther",_kit.ProvNum).PatNum;
			CaptureHookOutcome outcome=_kit.RunHook(mount,null);//the mount was selected, not created in this call
			Assert.AreEqual(CaptureSessionLaunchKind.Refused,outcome.Result.Kind);
			Assert.IsFalse(outcome.Result.IsEmptyMountToDelete,"Only a mount created in this call may be deleted.");
			Assert.IsFalse(outcome.IsMountDeleted);
			Assert.IsTrue(CaptureDb.MountExists(mount.MountNum));
			Assert.AreEqual(4,CaptureDb.GetMountItemRows(mount.MountNum).Count);
			CollectionAssert.AreEqual(new[] { MountCreatedText(mount),"Capture session refused: "+outcome.Result.Reason },ImageCreateTexts());
		}

		[TestMethod]
		public void NewMount_NoImageCreatePermission_Refused_TheEmptyMountIsDeletedAndLoggedForThatUser() {
			_kit=new CaptureSessionDbKit("hookperm");
			Mount mount=_kit.CreateMount4Bw();
			Userod userod=CaptureDb.LogInWith(EnumPermType.ImageEdit,EnumPermType.Setup);//the permission was taken away meanwhile
			CaptureHookOutcome outcome=_kit.RunHook(mount,_kit.MountDef4Bw);
			Assert.AreEqual(CaptureSessionLaunchKind.Refused,outcome.Result.Kind);
			Assert.AreEqual("Not authorized for Image Create.",outcome.Result.Reason);
			Assert.IsTrue(outcome.IsMountDeleted);
			Assert.IsFalse(CaptureDb.MountExists(mount.MountNum));
			CollectionAssert.AreEqual(new[] { MountCreatedText(mount),MountDeletedText(mount,"the capture session refused to start: Not authorized for Image Create.") },
				ImageCreateTexts());
			Assert.AreEqual(userod.UserNum,CaptureDb.GetLogsForPatient(EnumPermType.ImageCreate,_kit.Patient.PatNum).Last().UserNum);
			Assert.AreEqual(0,_kit.WindowRunner.CountShown);
		}

		[TestMethod]
		public void NoMount_InvalidCategory_Refused_Logged() {
			_kit=new CaptureSessionDbKit("hookcat");
			CaptureHookOutcome outcome=CaptureHookMirror.TryAcquireWithCaptureSession(_kit.Launcher(),() => _kit.PatientShown,null,null,_kit.ImagingDevice,
				_kit.Category.DefNum+100000,() => _kit.PatNumCur);
			Assert.AreEqual(CaptureSessionLaunchKind.Refused,outcome.Result.Kind);
			Assert.AreEqual("Image category missing; choose a category.",outcome.Result.Reason,"Design 7.12 \"Invalid category\", before writing.");
			CollectionAssert.AreEqual(new[] { "Capture session refused: Image category missing; choose a category." },ImageCreateTexts());
			Assert.AreEqual(0,CaptureDb.GetDocuments(_kit.Patient.PatNum).Count);
		}
		#endregion Refused

		#region The empty-mount rule after a session
		[TestMethod]
		public void NewMount_TheSessionSavedNothing_TheMountIsDeletedAndLogged() {
			_kit=new CaptureSessionDbKit("hookempty");
			_kit.WindowRunner.ActionWhileShown=controller => controller.Begin();//then the operator closes the window
			Mount mount=_kit.CreateMount4Bw();
			CaptureHookOutcome outcome=_kit.RunHook(mount,_kit.MountDef4Bw);
			Assert.AreEqual(CaptureSessionLaunchKind.Completed,outcome.Result.Kind,outcome.Result.Reason);
			Assert.AreEqual(1,_kit.WindowRunner.CountShown);
			Assert.AreEqual(0,outcome.Result.Summary.CountImages);
			Assert.IsTrue(outcome.Result.IsEmptyMountToDelete);
			Assert.IsTrue(outcome.IsMountDeleted);
			Assert.IsFalse(CaptureDb.MountExists(mount.MountNum));
			Assert.AreEqual(0,CaptureDb.GetMountItemRows(mount.MountNum).Count);
			CollectionAssert.AreEqual(new[] { MountCreatedText(mount),MountDeletedText(mount,"the capture session saved no image") },ImageCreateTexts(),
				"Never started: no session summary either.");
			Assert.AreEqual(0,outcome.ListMessages.Count);
			Assert.IsTrue(outcome.IsSamePatient);
			Assert.IsTrue(outcome.IsDisplayClearedAfterDelete,"The module still shows the patient: the deleted mount is cleared from it.");
			Assert.AreEqual("",outcome.NodeSelectedAfter,"No mount and no image to show.");
		}

		[TestMethod]
		public void NewMount_AnotherWindowFiledADocumentIntoItDuringTheSession_TheLauncherKeepsIt() {
			_kit=new CaptureSessionDbKit("hookother");
			Mount mount=_kit.CreateMount4Bw();
			Document document=null;
			_kit.WindowRunner.ActionWhileShown=controller => {
				controller.Begin();
				//The quarantine window, or another window of this computer, files an image into slot 2; the session does not hear of it.
				document=CaptureDb.CreateDocumentInSlot(_kit.Patient,MountItems.GetItemsForMount(mount.MountNum).Single(x => x.ItemOrder==2),_kit.Category.DefNum);
			};
			CaptureHookOutcome outcome=_kit.RunHook(mount,_kit.MountDef4Bw);
			Assert.AreEqual(CaptureSessionLaunchKind.Completed,outcome.Result.Kind,outcome.Result.Reason);
			Assert.IsFalse(outcome.Result.IsEmptyMountToDelete,"The launcher read the mount's documents from the database.");
			Assert.IsFalse(outcome.IsMountDeleted);
			Assert.IsTrue(CaptureDb.MountExists(mount.MountNum));
			MountItem mountItem=CaptureDb.GetMountItemRows(mount.MountNum).Single(x => x.ItemOrder==2);
			Assert.AreEqual(mountItem.MountItemNum,Documents.GetByNum(document.DocNum).MountItemNum,"The document still points at an existing item.");
			Assert.IsTrue(_kit.ListLog.Any(x => x.Contains("created for the session is kept: it holds a document the session did not save")));
			Assert.IsFalse(ImageCreateTexts().Any(x => x.StartsWith("Mount Deleted: ")));
		}

		[TestMethod]
		public void NewMount_ThePatientChanged_TheImageWentToTheQuarantine_TheMountIsKeptForItsAssign_TheEventIsLogged() {
			_kit=new CaptureSessionDbKit("hookquar");
			Patient patientOther=CaptureDb.CreatePatient("HookQuarOther",_kit.ProvNum);
			Mount mount=_kit.CreateMount4Bw();
			_kit.WindowRunner.ActionWhileShown=controller => {
				_kit.Start(controller);
				//The main window changes to another patient (ODEvent Patient); then the source delivers an exposure.
				_kit.PatNumCur=patientOther.PatNum;
				_kit.PatientEvents.FirePatient(patientOther.PatNum);
				_kit.Dispatcher.RunPending();
				_kit.Status(TwainCaptureState.WaitingForImage);
				_kit.Image(90);
				_kit.Ended(TwainCaptureEndReason.Completed);
				Assert.AreEqual(1,controller.CountQuarantined,"Placing stopped at once (design 7.6).");
			};
			CaptureHookOutcome outcome=_kit.RunHook(mount,_kit.MountDef4Bw);
			Assert.AreEqual(CaptureSessionLaunchKind.Completed,outcome.Result.Kind,outcome.Result.Reason);
			Assert.AreEqual("",outcome.Result.Reason,"The window's script ran to its end.");
			Assert.AreEqual(1,outcome.Result.Summary.CountQuarantined);
			Assert.IsFalse(outcome.Result.IsEmptyMountToDelete,"Assign files the quarantined image into its original slot, so the mount stays.");
			Assert.IsTrue(CaptureDb.MountExists(mount.MountNum));
			Assert.AreEqual(4,CaptureDb.GetMountItemRows(mount.MountNum).Count);
			Assert.AreEqual(0,CaptureDb.GetDocuments(_kit.Patient.PatNum).Count);
			Assert.AreEqual(0,CaptureDb.GetDocuments(patientOther.PatNum).Count,"Never filed to the patient the main window shows now.");
			FakeQuarantineCall call=_kit.Twain.ListQuarantined.Single();
			Assert.AreEqual(_kit.Patient.PatNum,call.Context.PatNum,"Tagged with the bound patient (design 7.12).");
			//Design 7.13: the quarantine move is an ImageCreate "Capture event:" entry under the bound patient; the summary counts it.
			List<string> listTexts=ImageCreateTexts();
			Assert.AreEqual(1,listTexts.Count(x => x.StartsWith(CaptureAuditText.CaptureEventPrefix) && x.Contains(call.Path)),string.Join(" || ",listTexts));
			StringAssert.Contains(listTexts.Single(x => x.StartsWith("Capture session summary: ")),"0 images, 0 retakes, 1 quarantined");
			Assert.AreEqual(0,CaptureDb.GetLogsForPatient(EnumPermType.ImageCreate,patientOther.PatNum).Count);
		}

		[TestMethod]
		public void DeleteEmptyMount_ADocumentArrivedAfterTheLauncherLooked_ThePartialsReReadKeepsIt_AndLogsThat() {
			_kit=new CaptureSessionDbKit("hookreread");
			Mount mount=_kit.CreateMount4Bw();
			CaptureSessionLaunchResult result=new CaptureSessionLaunchResult { Kind=CaptureSessionLaunchKind.Completed,IsEmptyMountToDelete=true,
				Summary=new CaptureSessionSummary() };
			Document document=CaptureDb.CreateDocumentInSlot(_kit.Patient,MountItems.GetItemsForMount(mount.MountNum).Single(x => x.ItemOrder==1),
				_kit.Category.DefNum);
			CaptureHookOutcome outcome=new CaptureHookOutcome();
			Assert.IsFalse(CaptureHookMirror.DeleteEmptyMountAfterSession(_kit.Patient,mount,result,true,outcome),"The documents are read again right before the delete.");
			Assert.IsTrue(CaptureDb.MountExists(mount.MountNum));
			Assert.AreEqual(4,CaptureDb.GetMountItemRows(mount.MountNum).Count,"Mounts.Delete would have removed the item the document points at.");
			Assert.AreEqual(CaptureDb.GetMountItemRows(mount.MountNum).Single(x => x.ItemOrder==1).MountItemNum,Documents.GetByNum(document.DocNum).MountItemNum);
			Assert.AreEqual("Mount kept: "+mount.Description+", created for a capture session, holds a document after the session, so it was not deleted.",
				ImageCreateTexts().Last());
		}

		[TestMethod]
		public void ExistingMount_TheSessionSavedNothing_TheMountIsKept() {
			_kit=new CaptureSessionDbKit("hookkeep");
			_kit.WindowRunner.ActionWhileShown=controller => controller.Begin();
			Mount mount=_kit.CreateMount4Bw();
			CaptureHookOutcome outcome=_kit.RunHook(mount,null);
			Assert.AreEqual(CaptureSessionLaunchKind.Completed,outcome.Result.Kind,outcome.Result.Reason);
			Assert.IsFalse(outcome.Result.IsEmptyMountToDelete,"A mount the user selected is never deleted.");
			Assert.IsTrue(CaptureDb.MountExists(mount.MountNum));
			CollectionAssert.AreEqual(new[] { MountCreatedText(mount) },ImageCreateTexts());
		}
		#endregion The empty-mount rule after a session

		#region The main window's patient changes while the session window is open
		[TestMethod]
		public void PatientChangedDuringTheSession_TheModuleShowsTheOtherPatient_TheSessionsMountIsNotLoadedIntoIt_NothingLoggedForTheOther() {
			//A task popup opened during the session goes to another patient's task (TaskGoTo sets PatNumCur and refreshes the module, so
			//RefreshModuleData sets _patient to that patient). The session's mount holds the bound patient's image.
			_kit=new CaptureSessionDbKit("hookswap");
			Patient patientOther=CaptureDb.CreatePatient("HookSwapOther",_kit.ProvNum);
			Mount mount=_kit.CreateMount4Bw();
			_kit.WindowRunner.ActionWhileShown=controller => {
				_kit.Start(controller);
				_kit.Expose(60);
				Assert.AreEqual(1,controller.CountImages);
				_kit.ChangePatientShown(patientOther);//placing stops at once (design 7.6); the operator ends the session
				_kit.Execute(controller,CaptureSessionAction.End);
				_kit.Execute(controller,CaptureSessionAction.End);
				_kit.Ended(TwainCaptureEndReason.Stopped);
			};
			CaptureHookOutcome outcome=_kit.RunHook(mount,_kit.MountDef4Bw);
			Assert.AreEqual(CaptureSessionLaunchKind.Completed,outcome.Result.Kind,outcome.Result.Reason);
			Assert.AreEqual(1,outcome.Result.Summary.CountImages);
			Assert.IsFalse(outcome.IsSamePatient);
			Assert.IsFalse(outcome.IsMountDeleted);
			Assert.AreEqual(_kit.Patient.PatNum,Mounts.GetByNum(mount.MountNum).PatNum);
			Assert.AreEqual(_kit.Patient.PatNum,CaptureDb.GetDocuments(_kit.Patient.PatNum).Single().PatNum);
			Assert.AreEqual("",outcome.NodeSelectedAfter,"The bound patient's mount is not loaded into the other patient's Imaging module.");
			Assert.IsFalse(outcome.IsThumbnailRefreshed,"No thumbnail of the bound patient's mount is written into the other patient's folder.");
			Assert.AreEqual(0,CaptureDb.GetLogsForPatient(EnumPermType.ImageCreate,patientOther.PatNum).Count,"Nothing under the other patient.");
			Assert.AreEqual(0,CaptureDb.GetDocuments(patientOther.PatNum).Count);
			Assert.IsTrue(ImageCreateTexts().Any(x => x.StartsWith("Capture session summary: ")),"The session's entries are the bound patient's.");
		}

		[TestMethod]
		public void PatientClearedDuringTheSession_TheEmptyNewMountIsDeletedAndLoggedForTheBoundPatient() {
			//Windows locks with SecurityLogOffWithWindows: LogOffNow runs inside the session window's modal loop, and another user's logon with
			//clinics and PatientMaintainedOnUserChange off leaves PatNumCur 0, so RefreshModuleData(0) sets _patient to null. The partial used to
			//throw a NullReferenceException there, after the mount was already deleted, with its entry unwritten.
			_kit=new CaptureSessionDbKit("hooknull");
			Mount mount=_kit.CreateMount4Bw();
			_kit.WindowRunner.ActionWhileShown=controller => {
				controller.Begin();
				_kit.ChangePatientShown(null);
			};
			CaptureHookOutcome outcome=_kit.RunHook(mount,_kit.MountDef4Bw);
			Assert.AreEqual(CaptureSessionLaunchKind.Completed,outcome.Result.Kind,outcome.Result.Reason);
			Assert.AreEqual(0,outcome.Result.Summary.CountImages);
			Assert.IsFalse(outcome.IsSamePatient);
			Assert.IsTrue(outcome.Result.IsEmptyMountToDelete,"The bound patient's mount, empty, as the database shows.");
			Assert.IsTrue(outcome.IsMountDeleted);
			Assert.IsFalse(CaptureDb.MountExists(mount.MountNum));
			CollectionAssert.AreEqual(new[] { MountCreatedText(mount),MountDeletedText(mount,"the capture session saved no image") },ImageCreateTexts(),
				"The deletion is logged under the bound patient.");
			Assert.IsFalse(outcome.IsDisplayClearedAfterDelete,"The module shows no patient now: it is left alone.");
			Assert.AreEqual("",outcome.NodeSelectedAfter);
			Assert.IsFalse(outcome.IsThumbnailRefreshed);
		}

		[TestMethod]
		public void PatientUnchanged_TheModuleShowsTheSessionsMount_AndItsThumbnail() {
			_kit=new CaptureSessionDbKit("hooksame");
			Mount mount=_kit.CreateMount4Bw();
			_kit.WindowRunner.ActionWhileShown=controller => {
				_kit.Start(controller);
				_kit.Expose(90);
				Assert.IsTrue(_kit.Execute(controller,CaptureSessionAction.End));
				Assert.IsTrue(_kit.Execute(controller,CaptureSessionAction.End),"The second press within 10 s goes ahead (design 7.6).");
				_kit.Ended(TwainCaptureEndReason.Stopped);
			};
			CaptureHookOutcome outcome=_kit.RunHook(mount,_kit.MountDef4Bw);
			Assert.AreEqual(CaptureSessionLaunchKind.Completed,outcome.Result.Kind,outcome.Result.Reason);
			Assert.IsTrue(outcome.IsSamePatient);
			Assert.AreEqual("Mount "+mount.MountNum,outcome.NodeSelectedAfter);
			Assert.IsTrue(outcome.IsThumbnailRefreshed);
		}
		#endregion The main window's patient changes while the session window is open
	}
}
