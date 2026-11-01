// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using ImagingCapture.Save;
using ImagingCapture.Session;
using ImagingCapture.UI;
using ImagingDeviceManager.Capture;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace ImagingCapture.Tests {
	///<summary>CaptureSessionViewModel, the thin view model of the session window (design 7.3, 7.5, 7.12, 10.2, 10.3), over the controller on
	///fakes (CaptureControllerHarness): what each part of the window shows, the keys in the window and as hotkeys, the sounds, the slot
	///mini-map, the tooth picker, the preview with its display transform, the redelivered image, the summary and the close paths.</summary>
	[TestClass]
	public class CaptureSessionViewModelTests {
		private const int VkF9=0x78;
		private const int VkF10=0x79;
		private const int VkF12=0x7B;
		private CaptureControllerHarness _harness;
		private CaptureSessionViewModel _viewModel;
		private FakeHotkeyRegistrar _registrar;
		private List<CaptureSystemSound> _listPlayed;

		[TestInitialize]
		public void Init() {
			_harness=new CaptureControllerHarness();
			_registrar=new FakeHotkeyRegistrar();
			_listPlayed=new List<CaptureSystemSound>();
		}

		///<summary>The view model over the harness's controller, with [Keys] lines, and the stand-in registrar attached as the window does.</summary>
		private CaptureSessionViewModel ViewModel(params string[] arrayKeyLines) {
			if(arrayKeyLines.Length>0) {
				_harness.Setup.SessionConfig=CaptureSessionTestConfig.Read(new[] { "[Keys]" }.Concat(arrayKeyLines).ToArray());
			}
			_viewModel=new CaptureSessionViewModel(_harness.Controller,_listPlayed.Add);
			_viewModel.SetHotkeyRegistrar(_registrar);
			return _viewModel;
		}

		private void BeginArmed() {
			_viewModel.Begin();
			_harness.Clock.Advance(CaptureSessionController.KeyIgnoreMs);
			_viewModel.OnTick();
		}

		private void Started() {
			BeginArmed();
			Assert.IsTrue(_viewModel.RequestStart());
			_harness.Dispatcher.RunPending();
		}

		#region Banner and start
		[TestMethod]
		public void Banner_PatientProviderMount() {
			_harness.Setup.Patient.Birthdate=new DateTime(1980,1,2);
			ViewModel();
			_viewModel.Begin();
			Assert.AreEqual("X-ray capture for Pat Test",_viewModel.WindowTitle);
			Assert.AreEqual("Pat Test",_viewModel.PatientName);
			Assert.AreEqual("Born "+new DateTime(1980,1,2).ToShortDateString()+", age 46",_viewModel.PatientDetailsText);
			Assert.AreEqual("Provider: DOC4",_viewModel.ProviderText);
			Assert.IsTrue(_viewModel.IsMountVisible);
			Assert.AreEqual("Mount: FMX, "+_harness.Clock.Now.Date.ToShortDateString(),_viewModel.MountText);
			Assert.IsTrue(_viewModel.CanEditProvider);
			Assert.IsTrue(_viewModel.SetProvider(9));
			Assert.AreEqual("Provider: DOC9",_viewModel.ProviderText);
			Assert.AreEqual("Target: 1 of 3: Max R, #1,2,3",_viewModel.TargetText);
		}

		[TestMethod]
		public void Banner_NoDateOfBirth() {
			ViewModel();
			Assert.AreEqual("Date of birth not entered",_viewModel.PatientDetailsText);
		}

		[TestMethod]
		public void Start_ShownWithItsText_ArmedAfter2Seconds_HiddenWhileEnabled_ProviderLocked() {
			ViewModel();
			Assert.IsFalse(_viewModel.IsStartVisible,"Not before Begin.");
			_viewModel.Begin();
			Assert.IsTrue(_viewModel.IsStartVisible);
			Assert.IsFalse(_viewModel.IsStartEnabled,"Not armed in the first 2 s.");
			Assert.AreEqual("Start X-rays for Pat Test",_viewModel.StartText);
			Assert.AreEqual("",_viewModel.StartHintText,"No Confirm key bound.");
			Assert.AreEqual(CaptureIndicatorTone.Neutral,_viewModel.IndicatorTone);
			_harness.Clock.Advance(CaptureSessionController.KeyIgnoreMs);
			int version=_viewModel.Version;
			_viewModel.OnTick();
			Assert.IsTrue(_viewModel.IsStartEnabled);
			Assert.AreNotEqual(version,_viewModel.Version,"The window refreshes when Start is armed.");
			Assert.IsTrue(_viewModel.RequestStart());
			_harness.Dispatcher.RunPending();
			Assert.AreEqual(CaptureSessionPhase.Enabled,_viewModel.Phase);
			Assert.IsFalse(_viewModel.IsStartVisible);
			Assert.IsFalse(_viewModel.CanEditProvider,"Editable before Start only.");
			Assert.IsFalse(_viewModel.SetProvider(9));
			Assert.AreEqual(CaptureIndicatorTone.Busy,_viewModel.IndicatorTone);
			_harness.Status(TwainCaptureState.WaitingForImage);
			Assert.AreEqual(CaptureIndicatorTone.Waiting,_viewModel.IndicatorTone);
			StringAssert.StartsWith(_viewModel.IndicatorText,"Sensor software open, waiting for exposure");
		}

		[TestMethod]
		public void OlderMount_QuestionShows_StartHiddenUntilConfirmed_DeclineClosesAtOnce() {
			_harness=new CaptureControllerHarness(3,new DateTime(2026,10,20,9,0,0));
			ViewModel();
			_viewModel.Begin();
			Assert.IsTrue(_viewModel.IsOlderMountQuestionVisible);
			Assert.IsFalse(_viewModel.IsStartVisible);
			Assert.AreEqual(CaptureIndicatorTone.Warning,_viewModel.IndicatorTone);
			StringAssert.Contains(_viewModel.OlderMountText,"before today");
			_viewModel.ConfirmOlderMount();
			Assert.IsFalse(_viewModel.IsOlderMountQuestionVisible);
			Assert.IsTrue(_viewModel.IsStartVisible);
			//Another session declines it: nothing was taken, so the window closes without waiting.
			_harness=new CaptureControllerHarness(3,new DateTime(2026,10,20,9,0,0));
			ViewModel();
			_viewModel.Begin();
			_viewModel.DeclineOlderMount();
			Assert.IsTrue(_viewModel.IsSummaryVisible);
			Assert.AreEqual("Nothing was taken.",_viewModel.SummaryText);
			Assert.IsTrue(_viewModel.IsCloseDue);
		}
		#endregion Banner and start

		#region Keys and hotkeys
		[TestMethod]
		public void KeysInTheWindow_BoundChordsGoToTheController_UnboundIgnored() {
			ViewModel("Skip=F10","NextSlot=Ctrl+F10");
			BeginArmed();
			Assert.IsFalse(_viewModel.OnKeyDown(0x41,CaptureKeyModifiers.None,false),"A is not bound.");
			Assert.IsFalse(_viewModel.OnKeyDown(0x11,CaptureKeyModifiers.Control,false),"Ctrl alone is never a key.");
			Assert.IsTrue(_viewModel.OnKeyDown(VkF10,CaptureKeyModifiers.None,false));
			Assert.AreEqual(2,_harness.Controller.MountSlots.Target.ItemOrder,"Skip ran in the window.");
			Assert.IsTrue(_viewModel.OnKeyDown(VkF10,CaptureKeyModifiers.None,true),"A repeat is still swallowed...");
			Assert.AreEqual(2,_harness.Controller.MountSlots.Target.ItemOrder,"...but runs nothing.");
			Assert.IsTrue(_viewModel.OnKeyUp(VkF10));
			Assert.IsTrue(_viewModel.OnKeyDown(VkF10,CaptureKeyModifiers.Control,false));
			Assert.AreEqual(3,_harness.Controller.MountSlots.Target.ItemOrder,"Ctrl+F10 is NextSlot.");
			Assert.IsFalse(_viewModel.OnKeyUp(0x41));
			Assert.AreEqual(CaptureSessionPhase.Ready,_viewModel.Phase,"No session key starts anything.");
		}

		[TestMethod]
		public void ConfirmKey_HeldOneSecond_Starts_AndIsNeverAHotkey() {
			ViewModel("Confirm=F12","End=F9");
			BeginArmed();
			Assert.AreEqual("Or hold F12 for 1 s",_viewModel.StartHintText);
			Assert.IsTrue(_viewModel.OnKeyDown(VkF12,CaptureKeyModifiers.None,false));
			_harness.Clock.Advance(500);
			Assert.IsTrue(_viewModel.OnKeyDown(VkF12,CaptureKeyModifiers.None,true));
			_viewModel.OnTick();
			Assert.AreEqual(CaptureSessionPhase.Ready,_viewModel.Phase,"Not yet: held 0.5 s.");
			_harness.Clock.Advance(500);
			_viewModel.OnTick();
			_harness.Dispatcher.RunPending();
			Assert.AreEqual(CaptureSessionPhase.Enabled,_viewModel.Phase,"Held 1 s: started.");
			Assert.IsTrue(_viewModel.OnKeyUp(VkF12));
			CollectionAssert.AreEqual(new[] { CaptureSessionKey.End },_viewModel.Hotkeys.GetKeysRegistered(),"Confirm is not registered.");
		}

		[TestMethod]
		public void Hotkeys_RegisteredWhileTheSourceIsEnabled_PedalRunsTheCommand_ReleasedWhenDisabledAndOnClose() {
			ViewModel("Skip=F10","Retake=F9");
			BeginArmed();
			Assert.AreEqual(0,_registrar.DictRegistered.Count,"Not before the source is enabled.");
			Assert.IsTrue(_viewModel.RequestStart());
			_harness.Dispatcher.RunPending();
			Assert.IsTrue(_harness.Controller.AreSessionKeysActive);
			Assert.AreEqual(2,_registrar.DictRegistered.Count,"Registered while the source is enabled.");
			int idSkip=_registrar.DictRegistered.Single(x => x.Value.VirtualKey==VkF10).Key;
			Assert.IsTrue(_viewModel.OnHotkey(idSkip),"The pedal while the sensor software has the foreground.");
			_harness.Dispatcher.RunPending();
			Assert.AreEqual(2,_harness.Controller.MountSlots.Target.ItemOrder);
			Assert.IsFalse(_viewModel.OnHotkey(idSkip+50),"Not one of ours.");
			//The sensor software closes: the source is not enabled any more, so the hotkeys go.
			_harness.Ended(TwainCaptureEndReason.CloseRequested);
			Assert.AreEqual(CaptureSessionPhase.Ready,_viewModel.Phase);
			Assert.AreEqual(0,_registrar.DictRegistered.Count,"Released when the source is disabled.");
			Assert.IsFalse(_viewModel.OnHotkey(idSkip),"A late WM_HOTKEY does nothing.");
			//Re-enabled, then the window closes: released, and the controller takes its forced-close path.
			_harness.Clock.Advance(CaptureSessionController.KeyIgnoreMs);
			_viewModel.OnTick();
			Assert.IsTrue(_viewModel.RequestStart());
			_harness.Dispatcher.RunPending();
			Assert.AreEqual(2,_registrar.DictRegistered.Count);
			_viewModel.OnClosing();
			Assert.AreEqual(0,_registrar.DictRegistered.Count,"Released on close.");
			Assert.AreEqual(CaptureSessionPhase.Finished,_harness.Controller.Phase);
			Assert.AreEqual(CaptureSessionEndReason.Closed,_harness.Controller.Summary.EndReason);
			Assert.AreEqual(1,_harness.Twain.CountBeginClose,"The source closes on its worker; nothing waits.");
			Assert.IsFalse(_viewModel.OnKeyDown(VkF10,CaptureKeyModifiers.None,false),"Nothing after close.");
		}

		[TestMethod]
		public void KeyBindingWarnings_GoToTheSessionLog() {
			ViewModel("Retake=Pedal","Confirm=F9","Skip=F9");
			Assert.IsTrue(_harness.Controller.ListLog.Any(x => x.StartsWith("Capture session: Window: [Keys] Retake is not used: ")));
			Assert.IsTrue(_harness.Controller.ListLog.Any(x => x.StartsWith("Capture session: Window: [Keys] Confirm is not used: F9 is also Skip")));
			Assert.IsTrue(_harness.ListLogFile.Any(x => x.Contains("[Keys] Retake is not used")),"Also in the capture log file.");
		}
		#endregion Keys and hotkeys

		#region Commands
		[TestMethod]
		public void Commands_TextsWithTheirKeys_VisibleAndEnabledAsTheControllerSays() {
			ViewModel("Retake=F9","Disable=Shift+F11");
			BeginArmed();
			Assert.AreEqual("Retake (F9)",_viewModel.GetCommandText(CaptureSessionAction.Retake));
			Assert.AreEqual("Disable (Shift+F11)",_viewModel.GetCommandText(CaptureSessionAction.Disable));
			Assert.AreEqual("Next slot",_viewModel.GetCommandText(CaptureSessionAction.NextSlot));
			Assert.AreEqual("End",_viewModel.GetCommandText(CaptureSessionAction.End));
			foreach(CaptureSessionAction action in Enum.GetValues(typeof(CaptureSessionAction))) {
				Assert.IsTrue(_viewModel.IsCommandVisible(action),action.ToString());
			}
			Assert.IsFalse(_viewModel.IsCommandEnabled(CaptureSessionAction.Retake),"No image yet.");
			Assert.IsFalse(_viewModel.IsCommandEnabled(CaptureSessionAction.Disable),"Not enabled.");
			Assert.IsTrue(_viewModel.IsCommandEnabled(CaptureSessionAction.Skip));
			Assert.IsTrue(_viewModel.IsCommandEnabled(CaptureSessionAction.End));
			Assert.IsFalse(_viewModel.IsAlwaysRotateVisible,"No mount definition from this call.");
			Assert.IsTrue(_viewModel.RequestStart());
			_harness.Dispatcher.RunPending();
			Assert.IsTrue(_viewModel.IsCommandEnabled(CaptureSessionAction.Disable));
			_harness.Expose();
			Assert.IsTrue(_viewModel.IsCommandEnabled(CaptureSessionAction.Retake));
			Assert.IsTrue(_viewModel.IsCommandEnabled(CaptureSessionAction.Rotate));
		}

		[TestMethod]
		public void AlwaysRotate_ShownWithAMountDefinition_OfferedAfterACorrection() {
			_harness.Setup.MountDef=new MountDef { MountDefNum=3,Description="FMX" };
			ViewModel();
			Started();
			Assert.IsTrue(_viewModel.IsAlwaysRotateVisible);
			Assert.IsFalse(_viewModel.IsAlwaysRotateEnabled,"No correction yet.");
			_harness.Expose();
			Assert.IsTrue(_viewModel.Execute(CaptureSessionAction.Rotate));
			Assert.IsTrue(_viewModel.IsAlwaysRotateEnabled);
		}

		[TestMethod]
		public void AlwaysRotate_HiddenWithoutSetup() {
			_harness.Setup.MountDef=new MountDef { MountDefNum=3,Description="FMX" };
			_harness.Store.HashSetPermsDenied.Add(EnumPermType.Setup);
			ViewModel();
			Started();
			_harness.Expose();
			_viewModel.Execute(CaptureSessionAction.Rotate);
			Assert.IsFalse(_viewModel.IsAlwaysRotateVisible);
			Assert.IsFalse(_viewModel.IsAlwaysRotateEnabled);
		}

		[TestMethod]
		public void UnsafeEnd_WarningShows_SecondPressStops() {
			ViewModel();
			Started();
			Assert.IsTrue(_viewModel.Execute(CaptureSessionAction.End));
			Assert.AreEqual("If an exposure was just taken, wait until it appears before cancelling. Press End again to stop now.",_viewModel.WarningText);
			Assert.AreEqual(0,_harness.Twain.ListDisables.Count,"No disable on the first press without safe disable.");
			Assert.IsTrue(_viewModel.Execute(CaptureSessionAction.End));
			Assert.AreEqual(1,_harness.Twain.ListDisables.Count);
			Assert.AreEqual("",_viewModel.WarningText);
		}

		[TestMethod]
		public void SingleTarget_MountCommandsHidden() {
			_harness=new CaptureControllerHarness(0);
			ViewModel();
			BeginArmed();
			Assert.IsFalse(_viewModel.IsCommandVisible(CaptureSessionAction.Retake));
			Assert.IsFalse(_viewModel.IsCommandVisible(CaptureSessionAction.Skip));
			Assert.IsFalse(_viewModel.IsCommandVisible(CaptureSessionAction.NextSlot));
			Assert.IsFalse(_viewModel.IsCommandVisible(CaptureSessionAction.PreviousSlot));
			Assert.IsTrue(_viewModel.IsCommandVisible(CaptureSessionAction.Rotate));
			Assert.IsTrue(_viewModel.IsCommandVisible(CaptureSessionAction.End));
			Assert.IsFalse(_viewModel.IsAlwaysRotateVisible);
			Assert.IsFalse(_viewModel.IsMountVisible);
			Assert.AreEqual("",_viewModel.MountText);
			Assert.IsFalse(_viewModel.IsSlotMapVisible);
			Assert.IsTrue(_viewModel.IsToothPickerVisible);
		}
		#endregion Commands

		#region Slots and teeth
		[TestMethod]
		public void SlotTiles_InItemOrder_TargetFilledClickable_GridWhenSlotsOverlap() {
			ViewModel();
			BeginArmed();
			List<CaptureSlotTile> listTiles=_viewModel.GetSlotTiles(300,100);
			CollectionAssert.AreEqual(new[] { "1","2","3" },listTiles.Select(x => x.Number).ToList(),"Every slot in ItemOrder; not the text item.");
			CollectionAssert.AreEqual(new[] { "#1,2,3","#4,5","#6" },listTiles.Select(x => x.TeethText).ToList());
			Assert.AreEqual("1 of 3: Max R, #1,2,3",listTiles[0].ToolTip);
			Assert.AreEqual(CaptureSlotTileState.Target,listTiles[0].State);
			Assert.IsFalse(listTiles[0].IsClickable,"The target already.");
			Assert.AreEqual(CaptureSlotTileState.Empty,listTiles[1].State);
			Assert.IsTrue(listTiles[1].IsClickable);
			//The harness's slots all sit at 0,0: a grid, here one row of three squares.
			Assert.AreEqual(2,listTiles[0].Left,0.001);
			Assert.AreEqual(102,listTiles[1].Left,0.001);
			Assert.AreEqual(202,listTiles[2].Left,0.001);
			Assert.AreEqual(96,listTiles[0].Width,0.001);
			Assert.AreEqual(listTiles[0].Top,listTiles[2].Top,0.001);
			Assert.IsTrue(_viewModel.RequestStart());
			_harness.Dispatcher.RunPending();
			listTiles=_viewModel.GetSlotTiles(300,100);
			Assert.IsTrue(listTiles[0].IsEnableTarget,"The running enable's slot.");
			_harness.Expose();
			listTiles=_viewModel.GetSlotTiles(300,100);
			Assert.AreEqual(CaptureSlotTileState.Filled,listTiles[0].State);
			Assert.IsFalse(listTiles[0].IsClickable,"A filled slot cannot be tapped.");
			Assert.AreEqual(CaptureSlotTileState.Target,listTiles[1].State);
		}

		[TestMethod]
		public void SlotTiles_PlacedAsTheMountPlacesThem_TapAnEmptySlot() {
			List<MountItem> listMountItems=_harness.Setup.MountSlots.ListMountItems.ToList();
			listMountItems[1].Xpos=0;
			listMountItems[1].Ypos=0;
			listMountItems[2].Xpos=200;
			listMountItems[2].Ypos=0;
			listMountItems[3].Xpos=400;
			listMountItems[3].Ypos=0;
			ViewModel();
			BeginArmed();
			List<CaptureSlotTile> listTiles=_viewModel.GetSlotTiles(300,150);
			//600x300 of slots into 300x150: scale 0.5, each slot 100x150 less the 1 px gap on each side.
			Assert.AreEqual(1,listTiles[0].Left,0.001);
			Assert.AreEqual(101,listTiles[1].Left,0.001);
			Assert.AreEqual(201,listTiles[2].Left,0.001);
			Assert.AreEqual(98,listTiles[0].Width,0.001);
			Assert.AreEqual(148,listTiles[0].Height,0.001);
			Assert.IsTrue(_viewModel.SelectSlot(listTiles[2].Idx));
			Assert.AreEqual(3,_harness.Controller.MountSlots.Target.ItemOrder);
			Assert.AreEqual(CaptureSlotTileState.Target,_viewModel.GetSlotTiles(300,150)[2].State);
		}

		[TestMethod]
		public void SlotTiles_Skipped() {
			ViewModel();
			BeginArmed();
			Assert.IsTrue(_viewModel.Execute(CaptureSessionAction.Skip));
			List<CaptureSlotTile> listTiles=_viewModel.GetSlotTiles(300,100);
			Assert.AreEqual(CaptureSlotTileState.Skipped,listTiles[0].State);
			Assert.IsTrue(listTiles[0].IsClickable,"A skipped slot can be tapped to take it now.");
			Assert.AreEqual(CaptureSlotTileState.Target,listTiles[1].State);
		}

		[TestMethod]
		public void ToothPicker_TapAddsAndRemoves_TypedTeeth_RefusedEntryShowsWhy_LockedWhileEnabled() {
			_harness=new CaptureControllerHarness(0);
			ViewModel();
			BeginArmed();
			List<CaptureToothTile> listTiles=_viewModel.GetToothTiles();
			Assert.AreEqual(32,listTiles.Count);
			CollectionAssert.AreEqual(Enumerable.Range(1,16).Select(x => x.ToString()).ToList(),
				listTiles.Where(x => x.Row==0).Select(x => x.Universal).ToList(),"Maxillary 1-16 from the patient's right.");
			CollectionAssert.AreEqual(Enumerable.Range(17,16).Reverse().Select(x => x.ToString()).ToList(),
				listTiles.Where(x => x.Row==1).Select(x => x.Universal).ToList(),"Mandibular 32-17 below them.");
			Assert.IsTrue(_viewModel.IsToothPickerEnabled);
			Assert.IsTrue(_viewModel.ToggleTooth("9"));
			Assert.IsTrue(_viewModel.ToggleTooth("8"));
			Assert.AreEqual("8,9",_viewModel.ToothNumbersText,"In tooth order.");
			Assert.IsTrue(_viewModel.GetToothTiles().Single(x => x.Universal=="8").IsSelected);
			Assert.AreEqual("Target: Single image, Max anterior, #8,9",_viewModel.TargetText);
			Assert.IsTrue(_viewModel.ToggleTooth("9"));
			Assert.AreEqual("8",_viewModel.ToothNumbersText);
			Assert.IsTrue(_viewModel.SetToothNumbersText("8, 9 ,E"));
			Assert.AreEqual("8,9,E",_viewModel.ToothNumbersText,"Typed, primary teeth too.");
			Assert.IsFalse(_viewModel.SetToothNumbersText("8-10"));
			StringAssert.Contains(_viewModel.ToothErrorText,"'8-10'");
			Assert.AreEqual("8,9,E",_viewModel.ToothNumbersText,"Unchanged.");
			Assert.IsTrue(_viewModel.SetToothNumbersText("30"));
			Assert.AreEqual("",_viewModel.ToothErrorText);
			Assert.IsTrue(_viewModel.RequestStart());
			_harness.Dispatcher.RunPending();
			Assert.IsFalse(_viewModel.IsToothPickerEnabled,"Between images only.");
			Assert.IsFalse(_viewModel.ToggleTooth("31"));
			Assert.AreNotEqual("",_viewModel.ToothErrorText);
			Assert.AreEqual("30",_viewModel.ToothNumbersText);
		}

		[TestMethod]
		public void SingleTarget_TeethTypedButNotApplied_TheStartAppliesThemFirst() {
			_harness=new CaptureControllerHarness(0);
			ViewModel();
			BeginArmed();
			Assert.IsTrue(_viewModel.SetToothNumbersText("8,9"));
			//Typed into the box, then Start clicked: the session's buttons take no focus, so the box never lost it.
			_viewModel.SetToothNumbersTyped("10");
			Assert.IsTrue(_viewModel.RequestStart());
			_harness.Dispatcher.RunPending();
			Assert.AreEqual("10",_harness.LastRecord.ToothNumbers,"Design 10.3: the image gets the teeth on the banner.");
			Assert.AreEqual("10",_viewModel.ToothNumbersText);
			_harness.Expose();
			Assert.AreEqual("10",_harness.RadiographStore.ListDocumentsUpdated.Single().ToothNumbers);
			_harness.Clock.Advance(CaptureSessionController.KeyIgnoreMs);
			_viewModel.OnTick();
			_viewModel.SetToothNumbersTyped("11");//then Take another
			Assert.IsTrue(_viewModel.RequestStart());
			_harness.Dispatcher.RunPending();
			Assert.AreEqual("11",_harness.LastRecord.ToothNumbers);
		}

		[TestMethod]
		public void SingleTarget_TeethTypedThatDoNotRead_NothingStarts() {
			_harness=new CaptureControllerHarness(0);
			ViewModel("Confirm=F12");
			BeginArmed();
			Assert.IsTrue(_viewModel.SetToothNumbersText("8"));
			_viewModel.SetToothNumbersTyped("8-10");
			Assert.IsFalse(_viewModel.RequestStart());
			StringAssert.Contains(_viewModel.ToothErrorText,"'8-10'");
			Assert.IsTrue(_viewModel.OnKeyDown(VkF12,CaptureKeyModifiers.None,false),"The Confirm key is handled...");
			_harness.Clock.Advance(1500);
			_viewModel.OnTick();
			_viewModel.OnKeyUp(VkF12);
			_harness.Dispatcher.RunPending();
			Assert.AreEqual(0,_harness.ListSessions.Count,"...but nothing starts with the teeth from before: the sensor software is not even opened.");
			Assert.AreEqual(CaptureSessionPhase.Ready,_viewModel.Phase);
			Assert.IsFalse(_viewModel.ToggleTooth("9"),"A tap does not replace the typed teeth with the banner's either.");
			Assert.AreEqual("8",_viewModel.ToothNumbersText);
		}

		[TestMethod]
		public void SingleTarget_TeethTyped_TheConfirmKeyAndATapApplyThemFirst() {
			_harness=new CaptureControllerHarness(0);
			ViewModel("Confirm=F12");
			BeginArmed();
			_viewModel.SetToothNumbersTyped("10");
			Assert.IsTrue(_viewModel.ToggleTooth("8"));
			Assert.AreEqual("8,10",_viewModel.ToothNumbersText,"The tap toggles the typed teeth.");
			_viewModel.SetToothNumbersTyped("12");
			Assert.IsTrue(_viewModel.OnKeyDown(VkF12,CaptureKeyModifiers.None,false));
			Assert.AreEqual("12",_viewModel.ToothNumbersText,"Applied when the Confirm key goes down, before its hold can start anything.");
		}

		[TestMethod]
		public void SingleTarget_TakeAnother_AfterTheImage() {
			_harness=new CaptureControllerHarness(0);
			ViewModel();
			Started();
			_harness.Expose();
			Assert.IsTrue(_viewModel.IsStartVisible);
			Assert.AreEqual("Take another",_viewModel.StartText);
			Assert.AreEqual("Last image: Single image",_viewModel.PreviewCaption);
		}
		#endregion Slots and teeth

		#region Preview, sounds, notices
		[TestMethod]
		public void Preview_TheSavedImage_WithTheSlotsRotation_RotateTurnsIt_FreshForFourSeconds_GoneAfterRetake() {
			_harness.Setup.MountSlots.ListMountItems[1].RotateOnAcquire=270;
			_harness.Setup.Mount.FlipOnAcquire=true;
			ViewModel();
			Started();
			Assert.IsNull(_viewModel.PreviewImage);
			Assert.AreEqual("",_viewModel.PreviewCaption);
			string error;
			Assert.IsNull(_viewModel.GetPreviewPixels(out error));
			_harness.Expose();
			Assert.AreSame(_harness.Controller.LastSavedImage,_viewModel.PreviewImage);
			Assert.AreEqual(270,_viewModel.PreviewDegrees,"The slot's RotateOnAcquire as a display transform.");
			Assert.IsTrue(_viewModel.IsPreviewFlipped,"The mount's FlipOnAcquire.");
			Assert.AreEqual("Last image: 1 of 3: Max R, #1,2,3",_viewModel.PreviewCaption);
			Assert.IsTrue(_viewModel.IsPreviewFresh);
			CapturePreviewPixels pixels=_viewModel.GetPreviewPixels(out error);
			Assert.AreEqual("",error);
			Assert.AreEqual(6,pixels.Width);
			Assert.AreEqual(4,pixels.Height);
			Assert.IsTrue(pixels.IsGray);
			Assert.AreSame(pixels,_viewModel.GetPreviewPixels(out error),"Made once per image.");
			Assert.IsTrue(_viewModel.Execute(CaptureSessionAction.Rotate));
			Assert.AreEqual(0,_viewModel.PreviewDegrees,"The operator's Rotate: 270 + 90.");
			_harness.Clock.Advance(CaptureSessionViewModel.PreviewFreshMs);
			int version=_viewModel.Version;
			_viewModel.OnTick();
			Assert.IsFalse(_viewModel.IsPreviewFresh);
			Assert.AreNotEqual(version,_viewModel.Version,"The window drops the highlight.");
			Assert.IsTrue(_viewModel.Execute(CaptureSessionAction.Retake));
			Assert.IsNull(_viewModel.PreviewImage,"The retaken image is no longer the shown one.");
		}

		[TestMethod]
		public void Sounds_TheControllersSoundsPlayAsSystemSounds() {
			ViewModel();
			Started();
			_harness.Expose();
			Assert.AreEqual(CaptureSystemSound.Asterisk,_listPlayed.Single(),"Saved: SystemAsterisk by default.");
			_harness.Ended(TwainCaptureEndReason.CloseRequested);
			Assert.AreEqual(CaptureSystemSound.Exclamation,_listPlayed.Last(),"Disabled: SystemExclamation.");
		}

		[TestMethod]
		public void Notices_RedStrip_PatientChanged_Unassigned() {
			ViewModel();
			Started();
			Assert.AreEqual("",_viewModel.RedStripText);
			Assert.AreEqual("",_viewModel.UnassignedText);
			Assert.IsFalse(_viewModel.CanOpenCaptureLinks,"Not while the source is enabled.");
			_harness.RadiographStore.ThrowOn="Import";
			_harness.Status(TwainCaptureState.WaitingForImage);
			_harness.Image();
			StringAssert.StartsWith(_viewModel.RedStripText,"Could not save: ");
			Assert.AreEqual(CaptureIndicatorTone.Error,_viewModel.IndicatorTone);
			Assert.AreEqual("2 unassigned captures on this computer.",_viewModel.UnassignedText,"The image went to the quarantine.");
			_harness.ChangePatient(56);
			Assert.AreEqual("Patient changed in the main window. This session is still for Pat Test.",_viewModel.PatientChangedText);
			_harness.Ended(TwainCaptureEndReason.Completed);
			Assert.IsTrue(_viewModel.CanOpenCaptureLinks,"The source is not enabled now.");
		}

		[TestMethod]
		public void RefreshUnassignedText_ReadsTheCountAgain() {
			ViewModel();
			BeginArmed();
			Assert.AreEqual("",_viewModel.UnassignedText);
			int version=_viewModel.Version;
			_viewModel.RefreshUnassignedText();
			Assert.AreEqual("2 unassigned captures on this computer.",_viewModel.UnassignedText);
			Assert.AreNotEqual(version,_viewModel.Version);
		}
		#endregion Preview, sounds, notices

		#region Redelivered image
		[TestMethod]
		public void Redelivered_PanelShowsAcquiredAtAndCaptureId_ConfirmPlacesIt() {
			_harness.Setup.Options.RedeliverCap=0x9001;
			ViewModel();
			Started();
			_harness.Status(TwainCaptureState.WaitingForImage);
			_harness.Ended(TwainCaptureEndReason.TransferFailed,ended => {
				ended.TransferFailureCount=1;
				ended.SourceMayHoldImages=true;
			});
			Assert.IsTrue(_viewModel.IsRequestRedeliveryVisible);
			Assert.IsFalse(_viewModel.IsRequestRedeliveryEnabled,"A deliberate start: 2 s after the banner.");
			Assert.AreEqual("Request undelivered images again",_viewModel.RequestRedeliveryText);
			_harness.Clock.Advance(CaptureSessionController.KeyIgnoreMs);
			_viewModel.OnTick();
			Assert.IsTrue(_viewModel.IsRequestRedeliveryEnabled);
			Assert.IsTrue(_viewModel.RequestRedelivery());
			_harness.Dispatcher.RunPending();
			AcquiredImage image=TestImages.Image8(6,4,(x,y) => (byte)(x*40+y),TestImages.Json8Bit);
			image.IsRedelivery=true;
			_harness.Twain.RaiseImage(image);
			_harness.Dispatcher.RunPending();
			Assert.IsTrue(_viewModel.IsRedeliveredVisible);
			_harness.Ended(TwainCaptureEndReason.Completed);
			Assert.AreEqual(CaptureSessionPhase.Ready,_viewModel.Phase);
			Assert.IsFalse(_viewModel.CanOpenCaptureLinks,"Design 7.12: a waiting redelivered image is placed only through Confirm, never from Recover.");
			Assert.AreSame(image,_viewModel.PreviewImage,"The preview shows the redelivered image...");
			Assert.AreEqual(0,_viewModel.PreviewDegrees,"...as delivered.");
			Assert.AreEqual("Redelivered image, not placed yet",_viewModel.PreviewCaption);
			Assert.AreEqual("Acquired: 2026-10-31T09:15:30.123+00:00\r\nCapture ID: c-7f3a91",_viewModel.RedeliveredDetailsText);
			StringAssert.Contains(_viewModel.RedeliveredText,"only if it is Pat Test's image.");
			Assert.AreEqual("",_viewModel.RedeliveredCountText);
			Assert.IsFalse(_viewModel.IsConfirmRedeliveredEnabled,"Armed 2 s after it shows.");
			Assert.IsTrue(_viewModel.IsDeclineRedeliveredEnabled);
			_harness.Ended(TwainCaptureEndReason.Completed,ended => ended.ImageCount=1);
			_harness.Clock.Advance(CaptureSessionController.KeyIgnoreMs);
			_viewModel.OnTick();
			Assert.IsTrue(_viewModel.IsConfirmRedeliveredEnabled);
			Assert.IsTrue(_viewModel.ConfirmRedelivered());
			Assert.IsFalse(_viewModel.IsRedeliveredVisible);
			Assert.AreEqual(CaptureSlotTileState.Filled,_viewModel.GetSlotTiles(300,100)[0].State,"Placed in the slot.");
		}
		#endregion Redelivered image

		#region Summary and closing
		[TestMethod]
		public void Summary_MountFull_ShowsThenClosesAfterTenSeconds() {
			_harness=new CaptureControllerHarness(1);
			ViewModel();
			Started();
			_harness.Expose();
			Assert.AreEqual(CaptureSessionPhase.Finished,_viewModel.Phase);
			Assert.IsTrue(_viewModel.IsSummaryVisible);
			Assert.AreEqual("",_viewModel.TargetText);
			Assert.AreEqual("Finished. 1 image, 0 retakes, 0 quarantined. Patient: Pat Test. Provider: DOC4.",_viewModel.IndicatorText,
				"The counts only, so the indicator stays at two lines.");
			StringAssert.StartsWith(_viewModel.EffectiveSettingsText,"In effect: PerSlot, 8-bit (fallback: no Test capture record",
				"Design 9.3, on a line of its own, with the depth actually used.");
			Assert.AreEqual(10,_viewModel.SecondsToClose);
			Assert.AreEqual("Every slot of the mount has an image. This window closes in 10 s.",_viewModel.SummaryText);
			Assert.IsFalse(_viewModel.IsCloseDue);
			foreach(CaptureSessionAction action in Enum.GetValues(typeof(CaptureSessionAction))) {
				Assert.IsFalse(_viewModel.IsCommandVisible(action),"No commands after the finish: "+action);
			}
			Assert.AreEqual(0,_registrar.DictRegistered.Count);
			_harness.Clock.Advance(4500);
			_viewModel.OnTick();
			Assert.AreEqual(6,_viewModel.SecondsToClose);
			_harness.Clock.Advance(5500);
			_viewModel.OnTick();
			Assert.AreEqual(0,_viewModel.SecondsToClose);
			Assert.AreEqual("Every slot of the mount has an image.",_viewModel.SummaryText);
			Assert.IsTrue(_viewModel.IsCloseDue);
			_viewModel.OnClosing();
			Assert.IsFalse(_viewModel.IsCloseDue);
			Assert.AreEqual(CaptureSessionEndReason.MountFull,_harness.Controller.Summary.EndReason,"A finished session is not closed again.");
		}

		[TestMethod]
		public void EndBeforeStart_ClosesAtOnce() {
			ViewModel();
			BeginArmed();
			Assert.IsTrue(_viewModel.Execute(CaptureSessionAction.End));
			Assert.AreEqual(CaptureSessionEndReason.CancelledBeforeStart,_harness.Controller.Summary.EndReason);
			Assert.IsTrue(_viewModel.IsCloseDue);
		}

		[TestMethod]
		public void CloseByTheX_WhileEnabled_IsEnd_StaysOpen_OtherwiseCloses() {
			ViewModel();
			BeginArmed();
			Assert.IsTrue(_viewModel.OnCloseRequestedByOperator(),"Not enabled: the window may close.");
			Assert.IsTrue(_viewModel.RequestStart());
			_harness.Dispatcher.RunPending();
			Assert.IsFalse(_viewModel.OnCloseRequestedByOperator(),"Enabled: End instead of a close.");
			StringAssert.StartsWith(_viewModel.WarningText,"If an exposure was just taken, wait until it appears before cancelling.");
			Assert.AreEqual(0,_harness.Twain.ListDisables.Count);
			Assert.IsFalse(_viewModel.OnCloseRequestedByOperator(),"The second X stops in the safe order...");
			Assert.AreEqual(TwainCaptureStopReason.End,_harness.Twain.ListDisables.Single());
			Assert.AreEqual(CaptureSessionPhase.Stopping,_viewModel.Phase);
			Assert.IsTrue(_viewModel.OnCloseRequestedByOperator(),"...and while it stops, the X closes (the forced-close path).");
			_harness.Ended(TwainCaptureEndReason.Stopped,ended => ended.StopReason=TwainCaptureStopReason.End);
			Assert.AreEqual(CaptureSessionEndReason.Ended,_harness.Controller.Summary.EndReason);
		}

		[TestMethod]
		public void AltF4AndTheTaskbarsClose_AreOperatorCloses_LikeTheX() {
			Assert.IsTrue(CaptureSessionViewModel.IsOperatorCloseCommand(0x0112,0xF060),"WM_SYSCOMMAND SC_CLOSE (Alt+F4, the taskbar's Close).");
			Assert.IsTrue(CaptureSessionViewModel.IsOperatorCloseCommand(0x0112,0xF063),"The four low bits are the system's own.");
			Assert.IsFalse(CaptureSessionViewModel.IsOperatorCloseCommand(0x0112,0xF020),"SC_MINIMIZE.");
			Assert.IsFalse(CaptureSessionViewModel.IsOperatorCloseCommand(0x0010,0),"WM_CLOSE itself (a logoff, the owner): the forced close.");
			//The window's hook on the frame sends an operator close to OnCloseRequestedByOperator: while enabled without safe disable, the
			//message is dropped, with the warning, and nothing is disabled or closed.
			ViewModel();
			Started();
			List<string> listLog=new List<string>();
			Assert.IsTrue(CaptureSessionViewModel.IsFrameMessageDropped(0x0112,0xF063,_viewModel.OnCloseRequestedByOperator,listLog.Add),
				"Alt+F4 while enabled: End, the window stays.");
			StringAssert.StartsWith(_viewModel.WarningText,"If an exposure was just taken, wait until it appears before cancelling.");
			Assert.AreEqual(0,_harness.Twain.ListDisables.Count);
			Assert.AreEqual(0,_harness.Twain.CountBeginClose,"Not the forced close.");
			Assert.AreEqual(CaptureSessionPhase.Enabled,_viewModel.Phase);
			Assert.IsFalse(CaptureSessionViewModel.IsFrameMessageDropped(0x0010,0,_viewModel.OnCloseRequestedByOperator,listLog.Add),
				"WM_CLOSE always goes on: the forced close of FormClosing.");
			Assert.AreEqual(0,listLog.Count);
		}

		[TestMethod]
		public void FrameCloseFilter_NotEnabled_TheCloseGoesOn_ACheckThatThrows_GoesOnAndIsLogged() {
			ViewModel();
			BeginArmed();
			List<string> listLog=new List<string>();
			Assert.IsFalse(CaptureSessionViewModel.IsFrameMessageDropped(0x0112,0xF060,_viewModel.OnCloseRequestedByOperator,listLog.Add),
				"Not enabled: Alt+F4 closes the window.");
			Assert.IsFalse(CaptureSessionViewModel.IsFrameMessageDropped(0x0112,0xF020,() => false,listLog.Add),"SC_MINIMIZE is never dropped.");
			Assert.IsFalse(CaptureSessionViewModel.IsFrameMessageDropped(0x0112,0xF060,() => { throw new InvalidOperationException("broken"); },
				listLog.Add),"A check that fails never makes the window impossible to close.");
			Assert.AreEqual("The close could not be checked: broken",listLog.Single());
		}

		[TestMethod]
		public void UnassignedCaptures_AfterThirtyDays_TheLineIsTheWarning_FlaggedForTheWarningColour() {
			_harness.Setup.UnassignedText="2 unassigned capture(s) on this computer.";
			bool isOverdue=false;
			_harness.FuncGetUnassignedInfo=() => new CaptureUnassignedInfo { Count=2,IsOverdue=isOverdue,
				Text="2 unassigned capture(s) on this computer."+(isOverdue ? " The oldest is from 2026-09-20. Ask an administrator to recover them now (see "
					+"the runbook)." : "") };
			ViewModel();
			_viewModel.Begin();
			Assert.IsFalse(_viewModel.IsUnassignedOverdue);
			isOverdue=true;
			_viewModel.RefreshUnassignedText();
			Assert.IsTrue(_viewModel.IsUnassignedOverdue,"Design 7.12: after 30 days the count turns into an escalating warning.");
			StringAssert.EndsWith(_viewModel.UnassignedText,"Ask an administrator to recover them now (see the runbook).");
			isOverdue=false;
			_viewModel.RefreshUnassignedText();
			Assert.IsFalse(_viewModel.IsUnassignedOverdue);
			_harness.Setup.IsUnassignedOverdue=true;
			_harness.Create();
			ViewModel();
			Assert.IsTrue(_viewModel.IsUnassignedOverdue,"As the launcher read it when the session started.");
		}

		[TestMethod]
		public void OnClosing_TwiceAndAfterTheFinish_Harmless_NoSoundAfterClose() {
			ViewModel();
			Started();
			_viewModel.OnClosing();
			_viewModel.OnClosing();
			_viewModel.ReleaseHotkeys();
			Assert.AreEqual(1,_harness.Twain.CountBeginClose);
			Assert.IsTrue(_viewModel.IsClosing);
			Assert.IsFalse(_viewModel.IsStartVisible);
			int countPlayed=_listPlayed.Count;
			_harness.Image();//arrives after the end: to the quarantine, and the window is gone
			Assert.AreEqual(countPlayed,_listPlayed.Count);
			Assert.IsFalse(_viewModel.RequestStart());
			Assert.IsFalse(_viewModel.Execute(CaptureSessionAction.Skip));
		}
		#endregion Summary and closing
	}
}
