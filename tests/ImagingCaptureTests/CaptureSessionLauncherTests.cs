// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ImagingCapture.Save;
using ImagingCapture.Session;
using ImagingCapture.Settings;
using ImagingCapture.UI;
using ImagingDeviceManager.Capture;
using ImagingDeviceManager.Config;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace ImagingCapture.Tests {
	///<summary>CaptureSessionLauncher (design 6.4, 7.3 "The hook", 7.12): the checks before anything opens, the rollback (NotStarted), the
	///refusals, and what the partial is told about a mount it created. The window is a stand-in that drives the controller.</summary>
	[TestClass]
	public class CaptureSessionLauncherTests {
		private string _tempDir;
		private FakeCaptureSessionStore _store;
		private FakeSessionRadiographStore _radiographStore;
		private FakeWindowRunner _windowRunner;
		///<summary>The test makes the window fail on purpose (Cleanup then accepts what it threw).</summary>
		private bool _isWindowFailureExpected;
		private List<FakeTwainCaptureSession> _listSessions;
		private List<string> _listLog;
		private LocalCaptureConfig _config;
		private QueueCaptureDispatcher _dispatcher;
		private FakeCaptureClock _clock;
		private long _patNumCur=55;
		///<summary>The Capture settings screen saved the file in effect for device 12 on this computer (design 9.3): the hash check passes.</summary>
		private bool _isSettingsSaved=true;
		private List<string> _listNotices;

		[TestInitialize]
		public void Init() {
			_tempDir=Path.Combine(Path.GetTempPath(),"CaptureSessionLauncherTests_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_tempDir);
			_store=new FakeCaptureSessionStore();
			_store.DictMountItems[7]=CaptureControllerHarness.Slots(3);
			_radiographStore=new FakeSessionRadiographStore();
			_windowRunner=new FakeWindowRunner();
			_listSessions=new List<FakeTwainCaptureSession>();
			_listLog=new List<string>();
			_dispatcher=new QueueCaptureDispatcher();
			_clock=new FakeCaptureClock();
			_config=Config("[Session]","Enabled=1");
			_listNotices=new List<string>();
			_store.FuncSetupLogsExtra=imagingDeviceNum => _isSettingsSaved
				? new List<SecurityLog> { CaptureSettingsTestKit.SavedLog(_config,imagingDeviceNum,"OPERATORY1") }
				: new List<SecurityLog>();
		}

		[TestCleanup]
		public void Cleanup() {
			try {
				Directory.Delete(_tempDir,true);
			}
			catch {
			}
			if(!_isWindowFailureExpected && _windowRunner.ExceptionThrown!=null) {
				//The launcher returns Completed for any exception of the window: an assertion inside the window must still fail the test.
				Assert.Fail("The window's script threw: "+_windowRunner.ExceptionThrown);
			}
		}

		private LocalCaptureConfig Config(params string[] arrayLines) {
			string path=Path.Combine(_tempDir,"capture.ini");
			File.WriteAllBytes(path,new UTF8Encoding(false).GetBytes(string.Join("\r\n",arrayLines)+"\r\n"));
			return LocalCaptureConfig.Load(path,Path.Combine(_tempDir,"fallback.ini"));
		}

		private CaptureSessionLauncher Launcher() {
			return new CaptureSessionLauncher(new CaptureSessionServices {
				Store=_store,
				RadiographStore=_radiographStore,
				FuncCreateSession=() => {
					FakeTwainCaptureSession session=new FakeTwainCaptureSession();
					_listSessions.Add(session);
					return session;
				},
				FuncCreateDispatcher=() => _dispatcher,
				Clock=_clock,
				FuncLoadConfig=() => _config,
				FuncGetComputerName=() => "OPERATORY1",
				FuncGetUnassignedText=() => null,
				ActionWriteLog=line => _listLog.Add(line),
				WindowRunner=_windowRunner,
				ActionShowNotice=(text,owner) => _listNotices.Add(text),
			});
		}

		private CaptureSessionLaunchRequest Request(bool isMountCreatedHere=true,bool isMount=true) {
			Mount mount=isMount ? new Mount { MountNum=7,PatNum=55,Description="FMX",DateCreated=_clock.Now,DocCategory=20 } : null;
			return new CaptureSessionLaunchRequest {
				Patient=CaptureControllerHarness.Pat(),
				PatNum=55,
				FuncGetPatNumCur=() => _patNumCur,
				Mount=mount,
				MountDef=isMount && isMountCreatedHere ? new MountDef { MountDefNum=3,Description="FMX" } : null,
				ImagingDevice=CaptureControllerHarness.Device(),
				CategoryDefNum=20,
				Owner="owner",
			};
		}

		#region NotStarted: the legacy loop runs
		[TestMethod]
		public void SessionDisabled_ByDefault_NotStarted_NothingOpens() {
			_config=Config("[Twain]","Allow16Bit=0");
			CaptureSessionLaunchResult result=Launcher().Run(Request());
			Assert.AreEqual(CaptureSessionLaunchKind.NotStarted,result.Kind);
			StringAssert.Contains(result.Reason,"[Session] Enabled");
			Assert.IsFalse(result.IsEmptyMountToDelete,"The legacy loop uses the mount.");
			Assert.AreEqual(0,_windowRunner.CountShown);
			Assert.AreEqual(0,_listSessions.Count);
		}

		[TestMethod]
		public void MissingConfigFile_NotStarted() {
			_config=LocalCaptureConfig.Load(Path.Combine(_tempDir,"none.ini"),Path.Combine(_tempDir,"none2.ini"));
			Assert.AreEqual(CaptureSessionLaunchKind.NotStarted,Launcher().Run(Request()).Kind);
		}

		[TestMethod]
		public void TwainMulti_NotStarted() {
			CaptureSessionLaunchRequest request=Request();
			request.ImagingDevice.DeviceType=EnumImgDeviceType.TwainMulti;
			CaptureSessionLaunchResult result=Launcher().Run(request);
			Assert.AreEqual(CaptureSessionLaunchKind.NotStarted,result.Kind);
			Assert.AreEqual(0,_windowRunner.CountShown);
		}

		[TestMethod]
		public void SettingsNeverSaved_NotStarted_TheOperatorIsTold() {
			_isSettingsSaved=false;
			CaptureSessionLaunchResult result=Launcher().Run(Request());
			Assert.AreEqual(CaptureSessionLaunchKind.NotStarted,result.Kind,"Design 9.3: a Setup save is needed before the session can be enabled.");
			StringAssert.Contains(result.Reason,"NeverSaved");
			Assert.IsFalse(result.IsEmptyMountToDelete,"The legacy loop uses the mount.");
			Assert.AreEqual(1,_listNotices.Count);
			StringAssert.StartsWith(_listNotices[0],CaptureSettingsCheck.TextNeverSaved);
			StringAssert.Contains(_listNotices[0],CaptureSessionText.LegacyLoopInstead);
			Assert.AreEqual(0,_windowRunner.CountShown);
			Assert.AreEqual(0,_listSessions.Count);
		}

		[TestMethod]
		public void SettingsChangedOutsideOpenDental_NotStarted_TheOperatorIsTold() {
			LocalCaptureConfig configSaved=Config("[Session]","Enabled=0");
			_store.FuncSetupLogsExtra=imagingDeviceNum => new List<SecurityLog> { CaptureSettingsTestKit.SavedLog(configSaved,imagingDeviceNum,"OPERATORY1") };
			_config=Config("[Session]","Enabled=1");//enabled by hand
			CaptureSessionLaunchResult result=Launcher().Run(Request());
			Assert.AreEqual(CaptureSessionLaunchKind.NotStarted,result.Kind,"The safe value of [Session] Enabled is off.");
			StringAssert.Contains(result.Reason,"ChangedOutside");
			StringAssert.StartsWith(_listNotices.Single(),"Capture settings were changed outside Open Dental; review them in Setup.");
			StringAssert.Contains(_listNotices.Single(),"Session.Enabled","It names what changed.");
			Assert.AreEqual(0,_windowRunner.CountShown);
		}

		[TestMethod]
		public void SettingsSavedForAnotherComputer_NeverSavedHere() {
			_store.FuncSetupLogsExtra=imagingDeviceNum => new List<SecurityLog> { CaptureSettingsTestKit.SavedLog(_config,imagingDeviceNum,"OPERATORY2") };
			Assert.AreEqual(CaptureSessionLaunchKind.NotStarted,Launcher().Run(Request()).Kind);
			StringAssert.StartsWith(_listNotices.Single(),CaptureSettingsCheck.TextNeverSaved);
		}

		[TestMethod]
		public void SecurityLogUnreadable_NotStarted_NotVerified() {
			_store.IsThrowOnSetupLogs=true;
			CaptureSessionLaunchResult result=Launcher().Run(Request());
			Assert.AreEqual(CaptureSessionLaunchKind.NotStarted,result.Kind);
			StringAssert.Contains(_listNotices.Single(),"could not be checked");
			StringAssert.Contains(_listNotices.Single(),"The database is not reachable");
		}

		[TestMethod]
		public void SessionDisabled_NoNoticeWhateverTheSettings() {
			_isSettingsSaved=false;
			_config=Config("[Session]","Enabled=0","[Twain]","Allow16Bit=1");
			Assert.AreEqual(CaptureSessionLaunchKind.NotStarted,Launcher().Run(Request()).Kind);
			Assert.AreEqual(0,_listNotices.Count,"The rollback by hand (Enabled=0) needs no Setup save and says nothing.");
		}

		[TestMethod]
		public void SettingsVerified_NoNotice_WindowShown() {
			_windowRunner.ActionWhileShown=controller => {
				controller.Begin();
				controller.OnClosing();
			};
			CaptureSessionLaunchResult result=Launcher().Run(Request());
			Assert.AreEqual(CaptureSessionLaunchKind.Completed,result.Kind);
			Assert.AreEqual("",result.Reason,"Not the window-failure path.");
			Assert.AreEqual(0,_listNotices.Count);
			Assert.AreEqual(1,_windowRunner.CountShown);
		}

		[TestMethod]
		public void NoWindowInThisBuild_NotStarted() {
			CaptureSessionLauncher launcher=new CaptureSessionLauncher(new CaptureSessionServices {
				Store=_store,FuncLoadConfig=() => _config,WindowRunner=null,
			});
			Assert.AreEqual(CaptureSessionLaunchKind.NotStarted,launcher.Run(Request()).Kind);
		}
		#endregion NotStarted: the legacy loop runs

		#region Refused
		[TestMethod]
		public void PatientChangedInTheMainWindow_Refused_CreatedMountToDelete() {
			_patNumCur=77;
			CaptureSessionLaunchResult result=Launcher().Run(Request());
			Assert.AreEqual(CaptureSessionLaunchKind.Refused,result.Kind);
			StringAssert.Contains(result.Reason,"Pat Test");
			Assert.IsTrue(result.IsEmptyMountToDelete,"The mount this call created is still empty.");
			Assert.AreEqual(0,_windowRunner.CountShown);
			_patNumCur=0;
			Assert.AreEqual(CaptureSessionLaunchKind.Refused,Launcher().Run(Request()).Kind,"No patient in the main window.");
		}

		[TestMethod]
		public void ExistingMount_RefusedButNotToDelete() {
			_patNumCur=77;
			CaptureSessionLaunchResult result=Launcher().Run(Request(isMountCreatedHere:false));
			Assert.AreEqual(CaptureSessionLaunchKind.Refused,result.Kind);
			Assert.IsFalse(result.IsEmptyMountToDelete,"Only a mount created in this call is deleted.");
		}

		[TestMethod]
		public void PatientAndPatNumDisagree_Refused() {
			CaptureSessionLaunchRequest request=Request();
			request.PatNum=56;
			_patNumCur=56;
			Assert.AreEqual(CaptureSessionLaunchKind.Refused,Launcher().Run(request).Kind);
			request=Request();
			request.Mount.PatNum=99;
			Assert.AreEqual(CaptureSessionLaunchKind.Refused,Launcher().Run(request).Kind,"The mount of another patient.");
		}

		[TestMethod]
		public void NotAuthorized_Refused() {
			_store.IsAuthorizedResult=false;
			CaptureSessionLaunchResult result=Launcher().Run(Request());
			Assert.AreEqual(CaptureSessionLaunchKind.Refused,result.Kind);
			Assert.AreEqual(EnumPermType.ImageCreate,_store.PermTypeChecked);
			Assert.AreEqual(0,_windowRunner.CountShown);
		}

		[TestMethod]
		public void InvalidCategory_Refused_BeforeAnythingOpens() {
			CaptureSessionLaunchRequest request=Request();
			request.CategoryDefNum=21;
			CaptureSessionLaunchResult result=Launcher().Run(request);
			Assert.AreEqual(CaptureSessionLaunchKind.Refused,result.Kind);
			Assert.AreEqual("Image category missing; choose a category.",result.Reason);
			request.CategoryDefNum=0;
			Assert.AreEqual("Image category missing; choose a category.",Launcher().Run(request).Reason);
			Assert.AreEqual(0,_listSessions.Count);
		}

		[TestMethod]
		public void SourceGate_Refused() {
			_config=Config("[Session]","Enabled=1","[Twain]","RestrictSources=1","AllowedSources=Scanner");
			CaptureSessionLaunchResult result=Launcher().Run(Request());
			Assert.AreEqual(CaptureSessionLaunchKind.Refused,result.Kind);
			Assert.AreEqual(CaptureEndText.SourceNotEnabledHere,result.Reason);
			_config=Config("[Session]","Enabled=1","[Twain]","RestrictSources=1","AllowedSources=Scanner|example sensor ds");
			Assert.AreEqual(CaptureSessionLaunchKind.Completed,Launcher().Run(Request()).Kind,"A listed source passes.");
		}

		[TestMethod]
		public void NoTwainSource_Refused() {
			CaptureSessionLaunchRequest request=Request();
			request.ImagingDevice.TwainName=" ";
			Assert.AreEqual(CaptureSessionLaunchKind.Refused,Launcher().Run(request).Kind);
		}

		[TestMethod]
		public void FullMount_Refused_NotToDelete() {
			_store.DictDocumentsByMountItem=_store.DictMountItems[7].Where(x => x.ItemOrder>0)
				.ToDictionary(x => x.MountItemNum,x => new Document { DocNum=x.MountItemNum*10,MountItemNum=x.MountItemNum });
			CaptureSessionLaunchResult result=Launcher().Run(Request(isMountCreatedHere:false));
			Assert.AreEqual(CaptureSessionLaunchKind.Refused,result.Kind);
			StringAssert.Contains(result.Reason,"already has an image");
			Assert.IsFalse(result.IsEmptyMountToDelete);
		}

		[TestMethod]
		public void UnexpectedException_Refused_WithTheMessage() {
			_store.IsThrowOnMountItems=true;
			CaptureSessionLaunchResult result=Launcher().Run(Request());
			Assert.AreEqual(CaptureSessionLaunchKind.Refused,result.Kind,"The legacy loop does not run instead without a word.");
			StringAssert.Contains(result.Reason,"The database is not reachable.");
			Assert.IsTrue(result.IsEmptyMountToDelete);
		}
		#endregion Refused

		#region Completed
		[TestMethod]
		public void Completed_NothingSaved_CreatedMountToDelete() {
			_windowRunner.ActionWhileShown=controller => {
				controller.Begin();
				controller.Execute(CaptureSessionAction.End);
			};
			CaptureSessionLaunchResult result=Launcher().Run(Request());
			Assert.AreEqual(CaptureSessionLaunchKind.Completed,result.Kind);
			Assert.AreEqual("",result.Reason);
			Assert.AreEqual("owner",_windowRunner.Owner);
			Assert.AreEqual(CaptureSessionEndReason.CancelledBeforeStart,result.Summary.EndReason);
			Assert.IsTrue(result.IsEmptyMountToDelete);
		}

		[TestMethod]
		public void Completed_NothingSaved_ButAnImageQuarantined_CreatedMountKeptForItsSlot() {
			_radiographStore.ThrowOn="Import";
			_windowRunner.ActionWhileShown=controller => {
				controller.Begin();
				_clock.Advance(CaptureSessionController.KeyIgnoreMs);
				controller.RequestStart();
				_dispatcher.RunPending();
				FakeTwainCaptureSession twain=_listSessions.Single();
				twain.RaiseStatus(TwainCaptureState.WaitingForImage);
				twain.RaiseImage(TestImages.Image8(4,4,(x,y) => 7));//its save fails: to the quarantine, for slot 1
				twain.RaiseEnded(TwainCaptureEndReason.Completed);
				_dispatcher.RunPending();
				controller.Execute(CaptureSessionAction.End);
			};
			CaptureSessionLaunchResult result=Launcher().Run(Request());
			Assert.AreEqual(0,result.Summary.CountImages);
			Assert.AreEqual(1,result.Summary.CountQuarantined);
			Assert.IsFalse(result.IsEmptyMountToDelete,"Design 7.12: Assign files the quarantined image into its original slot.");
		}

		[TestMethod]
		public void Completed_NothingSaved_TheQuarantineMoveFailed_CreatedMountKeptForTheJournalCopy() {
			//The save fails, then the move to unassigned captures fails too (a file conflict, folder rights, a full disk): the image's only copy is
			//its journal copy, whose sidecar names this mount and slot 1. Deleting the mount would leave Assign only an unmounted document, and a
			//16-bit capture nothing it may file into (design 7.8).
			_radiographStore.ThrowOn="Import";
			CaptureSessionController controllerShown=null;
			_windowRunner.ActionWhileShown=controller => {
				controllerShown=controller;
				controller.Begin();
				_clock.Advance(CaptureSessionController.KeyIgnoreMs);
				controller.RequestStart();
				_dispatcher.RunPending();
				FakeTwainCaptureSession twain=_listSessions.Single();
				twain.QuarantineError="There is not enough space on the disk.";//QuarantineImage returns null
				twain.RaiseStatus(TwainCaptureState.WaitingForImage);
				twain.RaiseImage(TestImages.Image8(4,4,(x,y) => 7));
				twain.RaiseEnded(TwainCaptureEndReason.Completed);
				_dispatcher.RunPending();
				controller.Execute(CaptureSessionAction.End);
			};
			CaptureSessionLaunchResult result=Launcher().Run(Request());
			Assert.AreEqual(CaptureSessionLaunchKind.Completed,result.Kind);
			Assert.AreEqual("",result.Reason);
			Assert.AreEqual(1,_listSessions.Single().ListQuarantined.Count,"The move was tried.");
			Assert.AreEqual(0,result.Summary.CountImages);
			Assert.AreEqual(0,result.Summary.CountQuarantined,"Nothing reached unassigned captures.");
			Assert.AreEqual(1,controllerShown.CountQuarantineAttempts);
			Assert.IsTrue(controllerShown.IsMountEmpty);
			Assert.IsFalse(result.IsEmptyMountToDelete,"The journal copy names this mount's slot: the mount stays for its Assign.");
			Assert.IsTrue(_listLog.Any(x => x.Contains("created for the session is kept: 1 image(s) of the session could not be moved to unassigned captures")),
				string.Join("\r\n",_listLog));
		}

		[TestMethod]
		public void Completed_TheDatabaseHasADocumentInTheMount_TheSessionDidNotSaveIt_MountKept() {
			_windowRunner.ActionWhileShown=controller => {
				controller.Begin();
				_store.DictDocumentsByMountItem[102]=new Document { DocNum=950,PatNum=55,MountItemNum=102 };//filed from another window
				controller.Execute(CaptureSessionAction.End);
			};
			CaptureSessionLaunchResult result=Launcher().Run(Request());
			Assert.AreEqual(CaptureSessionLaunchKind.Completed,result.Kind);
			Assert.AreEqual("",result.Reason);
			Assert.IsFalse(result.IsEmptyMountToDelete,"Mounts.Delete would leave the document pointing at a deleted mount item.");
			_store.DictDocumentsByMountItem.Clear();
			_windowRunner.ActionWhileShown=controller => {
				controller.Begin();
				_store.IsThrowOnMountItems=true;//after the checks before the start
				controller.Execute(CaptureSessionAction.End);
			};
			CaptureSessionLaunchResult resultUnreadable=Launcher().Run(Request());
			Assert.AreEqual("",resultUnreadable.Reason,"The session itself ran normally.");
			Assert.IsFalse(resultUnreadable.IsEmptyMountToDelete,"Kept when the mount cannot be read.");
		}

		[TestMethod]
		public void Completed_ImagesSaved_MountKept_SetupFromTheChecks() {
			_store.DefaultProvNum=8;
			_windowRunner.ActionWhileShown=controller => {
				controller.Begin();
				_clock.Advance(CaptureSessionController.KeyIgnoreMs);
				controller.RequestStart();
				_dispatcher.RunPending();
				FakeTwainCaptureSession twain=_listSessions.Single();
				twain.RaiseStatus(TwainCaptureState.WaitingForImage);
				twain.RaiseImage(TestImages.Image8(4,4,(x,y) => 7));
				twain.RaiseEnded(TwainCaptureEndReason.Completed);
				_dispatcher.RunPending();
				controller.Execute(CaptureSessionAction.End);//the second slot's enable is running: the first press warns
				controller.Execute(CaptureSessionAction.End);
				twain.RaiseEnded(TwainCaptureEndReason.Stopped,ended => ended.StopReason=TwainCaptureStopReason.End);
				_dispatcher.RunPending();
			};
			CaptureSessionLaunchResult result=Launcher().Run(Request());
			Assert.AreEqual(CaptureSessionLaunchKind.Completed,result.Kind);
			Assert.AreEqual("",result.Reason);
			Assert.AreEqual(1,result.Summary.CountImages);
			Assert.AreEqual(8,result.Summary.ProvNum,"The default provider from the store (design 10.8).");
			Assert.IsFalse(result.IsEmptyMountToDelete,"The mount holds an image.");
			CaptureSessionController controllerShown=_windowRunner.Controller;
			Assert.AreEqual("Example Sensor DS",_listSessions.Single().OpenProductName);
			Assert.AreEqual(12,controllerShown.Options.ImagingDeviceNum);
			Assert.AreEqual(3,controllerShown.MountSlots.ListMountItems.Count(x => MountPlacement.IsSlot(x)));
			Assert.AreEqual(8,_radiographStore.ListDocumentsUpdated.Single().ProvNum);
		}

		[TestMethod]
		public void WindowClosedWithoutTheController_SessionClosedSafely() {
			_windowRunner.ActionWhileShown=controller => {
				controller.Begin();
				_clock.Advance(CaptureSessionController.KeyIgnoreMs);
				controller.RequestStart();
				_dispatcher.RunPending();
			};
			CaptureSessionLaunchResult result=Launcher().Run(Request());
			Assert.AreEqual(CaptureSessionLaunchKind.Completed,result.Kind);
			Assert.AreEqual("",result.Reason);
			Assert.AreEqual(CaptureSessionEndReason.Closed,result.Summary.EndReason);
			Assert.AreEqual(1,_listSessions.Single().CountBeginClose,"The source closes in the safe order on its worker.");
		}

		[TestMethod]
		public void Completed_ClosedWhileTheSourceWasEnabled_CreatedMountKept_ALateQuarantineUpdatesTheSummaryEntry() {
			_windowRunner.ActionWhileShown=controller => {
				controller.Begin();
				_clock.Advance(CaptureSessionController.KeyIgnoreMs);
				controller.RequestStart();
				_dispatcher.RunPending();
				_listSessions.Single().RaiseStatus(TwainCaptureState.WaitingForImage);
				_dispatcher.RunPending();
				controller.OnClosing();//a logoff closes the window while the source is enabled: the forced close
			};
			CaptureSessionLaunchResult result=Launcher().Run(Request());
			Assert.AreEqual(CaptureSessionLaunchKind.Completed,result.Kind);
			Assert.AreEqual(0,result.Summary.CountQuarantined,"Nothing has arrived yet when the launcher decides.");
			Assert.IsFalse(result.IsEmptyMountToDelete,"The exposure the closing source still delivers names this mount's slot.");
			//The closing worker then quarantines the exposure it still held.
			_listSessions.Single().RaiseQuarantinedByLibrary(TestImages.Image8(4,4,(x,y) => 7));
			_dispatcher.RunPending();
			Assert.AreEqual(1,result.Summary.CountQuarantined);
			Assert.AreEqual(1,_store.ListLogTexts.Count(x => x.Contains("Capture session summary updated:") && x.Contains("1 quarantined in all")),
				"Design 10.7: the session's ImageCreate entries say how many images went to unassigned captures.\r\n"
				+string.Join("\r\n",_store.ListLogTexts));
		}

		[TestMethod]
		public void Completed_EndWhileEnabled_FinishedOnTheEnablesEnded_NothingExposed_CreatedMountToDelete() {
			_windowRunner.ActionWhileShown=controller => {
				controller.Begin();
				_clock.Advance(CaptureSessionController.KeyIgnoreMs);
				controller.RequestStart();
				_dispatcher.RunPending();
				FakeTwainCaptureSession twain=_listSessions.Single();
				twain.RaiseStatus(TwainCaptureState.WaitingForImage,isSafeDisableInEffect:true);
				_dispatcher.RunPending();
				Assert.IsTrue(controller.Execute(CaptureSessionAction.End),"Safe disable: no warning first.");
				Assert.AreEqual(CaptureSessionPhase.Stopping,controller.Phase);
				//The library raises Ended after the enable's last image: nothing more can arrive.
				twain.RaiseEnded(TwainCaptureEndReason.Stopped,ended => ended.StopReason=TwainCaptureStopReason.End);
				_dispatcher.RunPending();
				Assert.AreEqual(CaptureSessionPhase.Finished,controller.Phase);
				Assert.IsFalse(controller.IsSourceStillDelivering,"The enable had ended when the session finished.");
			};
			CaptureSessionLaunchResult result=Launcher().Run(Request());
			Assert.AreEqual(CaptureSessionLaunchKind.Completed,result.Kind);
			Assert.AreEqual(CaptureSessionEndReason.Ended,result.Summary.EndReason);
			CollectionAssert.AreEqual(new[] { TwainCaptureStopReason.End },_listSessions.Single().ListDisables);
			Assert.IsTrue(result.IsEmptyMountToDelete,"The session saved no image and quarantined none: the mount created for it is still empty.");
			Assert.IsFalse(_listLog.Any(x => x.Contains("still enabled or stopping")),string.Join("\r\n",_listLog));
		}

		[TestMethod]
		public void Completed_EndWhileOpening_NothingEnabled_CreatedMountToDelete_AlsoWhenTheOpenFails() {
			_windowRunner.ActionWhileShown=controller => {
				controller.Begin();
				_clock.Advance(CaptureSessionController.KeyIgnoreMs);
				controller.RequestStart();//the open runs in the background
				Assert.AreEqual(CaptureSessionPhase.Opening,controller.Phase);
				Assert.IsTrue(controller.Execute(CaptureSessionAction.End));
				_dispatcher.RunPending();//the open finishes: the session finishes without enabling
				Assert.AreEqual(CaptureSessionPhase.Finished,controller.Phase);
				Assert.IsFalse(controller.IsSourceStillDelivering,"A source that was never enabled delivers nothing.");
			};
			CaptureSessionLaunchResult result=Launcher().Run(Request());
			Assert.AreEqual(CaptureSessionLaunchKind.Completed,result.Kind);
			Assert.AreEqual(CaptureSessionEndReason.Ended,result.Summary.EndReason);
			Assert.AreEqual(0,_listSessions.Single().ListEnables.Count);
			Assert.AreEqual(1,_listSessions.Single().CountBeginClose);
			Assert.IsTrue(result.IsEmptyMountToDelete);
			_windowRunner.ActionWhileShown=controller => {
				controller.Begin();
				_clock.Advance(CaptureSessionController.KeyIgnoreMs);
				controller.RequestStart();
				controller.Execute(CaptureSessionAction.End);
				_listSessions.Last().OpenResult=new TwainCaptureOpenResult { Error=TwainCaptureOpenError.Other,ErrorText="The source did not open." };
				_dispatcher.RunPending();
				Assert.AreEqual(CaptureSessionPhase.Finished,controller.Phase);
			};
			CaptureSessionLaunchResult resultOpenFailed=Launcher().Run(Request());
			Assert.AreEqual(CaptureSessionEndReason.Ended,resultOpenFailed.Summary.EndReason);
			Assert.IsTrue(resultOpenFailed.IsEmptyMountToDelete,"The open failed: nothing was ever enabled.");
		}

		[TestMethod]
		public void WindowFailsAfterAnImageWasSaved_Completed_MountKept_SourceClosed() {
			_isWindowFailureExpected=true;
			_windowRunner.ActionWhileShown=controller => {
				controller.Begin();
				_clock.Advance(CaptureSessionController.KeyIgnoreMs);
				controller.RequestStart();
				_dispatcher.RunPending();
				FakeTwainCaptureSession twain=_listSessions.Single();
				twain.RaiseImage(TestImages.Image8(4,4,(x,y) => 7));
				_dispatcher.RunPending();
				throw new InvalidOperationException("The layout of the window failed.");
			};
			CaptureSessionLaunchResult result=Launcher().Run(Request());
			Assert.AreEqual(CaptureSessionLaunchKind.Completed,result.Kind);
			StringAssert.Contains(result.Reason,"The layout of the window failed.");
			Assert.AreEqual(1,result.Summary.CountImages);
			Assert.IsFalse(result.IsEmptyMountToDelete,"Never a mount that holds an image.");
			Assert.AreEqual(1,_listSessions.Single().CountBeginClose);
		}

		[TestMethod]
		public void SingleTarget_NoMount_NothingToDelete() {
			_windowRunner.ActionWhileShown=controller => {
				Assert.IsTrue(controller.IsSingleTarget);
				controller.Begin();
				controller.Execute(CaptureSessionAction.End);
			};
			CaptureSessionLaunchResult result=Launcher().Run(Request(isMount:false));
			Assert.AreEqual(CaptureSessionLaunchKind.Completed,result.Kind);
			Assert.AreEqual("",result.Reason,"The assertion inside the window held.");
			Assert.IsFalse(result.IsEmptyMountToDelete);
		}

		[TestMethod]
		public void VerificationRecord_LatestForThisDevice_MadeOnAnotherComputer_Gives16Bits() {
			//OPERATORY9's capture.ini differs from this one in the journal size and the allowed sources only, so its whole-file hash differs.
			LocalCaptureConfig configOther=Config("[Session]","Enabled=1","[Twain]","Allow16Bit=1","JournalKeep=50","AllowedSources=Example Sensor DS",
				"[Device.12]","Bits=16");
			string hashOther=GatedSettingsHash.Compute(configOther,12);
			_config=Config("[Session]","Enabled=1","[Twain]","Allow16Bit=1","[Device.12]","Bits=16");
			string hash=GatedSettingsHash.Compute(_config,12);
			Assert.AreNotEqual(hash,hashOther);
			CaptureVerificationRecord record=CaptureControllerHarness.PassedRecord(hashOther);
			record.ComputerName="OPERATORY9";//owner decision 2026-11-01: the record is not tied to the computer
			_store.ListSetupLogs.Add(new SecurityLog { SecurityLogNum=1,FKey=12,PermType=EnumPermType.Setup,LogText=record.ToLogText(),
				LogDateTime=new DateTime(2026,10,30,9,0,0) });
			int bits=0;
			_windowRunner.ActionWhileShown=controller => {
				controller.Begin();
				_clock.Advance(CaptureSessionController.KeyIgnoreMs);
				controller.RequestStart();
				_dispatcher.RunPending();
				bits=_listSessions.Single().LastRequest.Bits;
				controller.OnClosing();
			};
			Launcher().Run(Request());
			Assert.AreEqual(16,bits);
		}
		#endregion Completed
	}
}
