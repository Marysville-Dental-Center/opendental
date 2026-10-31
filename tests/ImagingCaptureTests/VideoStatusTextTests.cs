// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using ImagingCapture.Camera;
using ImagingCapture.Uvc;
using ImagingCapture.Uvc.Interop;
using ImagingCapture.Video;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingCapture.Tests {
	///<summary>What the Video window shows for each camera condition (design 8.9), the resolution choice (8.8) and the trigger
	///notes of its settings.</summary>
	[TestClass]
	public class VideoStatusTextTests {
		private const int HR_SOME_FAILURE=unchecked((int)0x80004005);

		[TestMethod]
		public void OpenFailures_TextAndButton() {
			VideoStatus videoStatus=VideoStatusText.ForOpenFailure(UvcErrorKind.InUse,UvcErrors.HR_ERROR_NO_SYSTEM_RESOURCES);
			Assert.AreEqual(VideoStatusKind.InUse,videoStatus.Kind);
			Assert.AreEqual("Camera is in use by another program. Close other camera software and click Refresh.",videoStatus.Text,
				"The sentence of design 8.9, with no program or vendor named.");
			Assert.AreEqual(VideoStatusAction.Refresh,videoStatus.Action);
			Assert.IsTrue(videoStatus.IsProblem);
			Assert.AreEqual(VideoStatusKind.AccessDenied,VideoStatusText.ForOpenFailure(UvcErrorKind.AccessDenied,0).Kind);
			Assert.AreEqual(VideoStatusAction.Refresh,VideoStatusText.ForOpenFailure(UvcErrorKind.NoDevice,0).Action);
			VideoStatus videoStatusLost=VideoStatusText.ForOpenFailure(UvcErrorKind.DeviceLost,0);
			Assert.AreEqual("Camera disconnected.",videoStatusLost.Text);
			Assert.AreEqual(VideoStatusAction.Reconnect,videoStatusLost.Action);
			Assert.AreEqual(VideoStatusKind.NoVideoOutput,VideoStatusText.ForOpenFailure(UvcErrorKind.NoCapturePin,0).Kind);
			Assert.AreEqual(VideoStatusKind.NotAvailable,VideoStatusText.ForOpenFailure(UvcErrorKind.NotAvailable,Ds.REGDB_E_CLASSNOTREG).Kind);
			VideoStatus videoStatusFailed=VideoStatusText.ForOpenFailure(UvcErrorKind.Failed,HR_SOME_FAILURE);
			Assert.AreEqual(VideoStatusKind.Failed,videoStatusFailed.Kind);
			Assert.AreEqual("(0x80004005)",videoStatusFailed.Detail,"The error code follows the translated text.");
			Assert.AreEqual(VideoStatusAction.Refresh,videoStatusFailed.Action);
			Assert.AreEqual(VideoStatusKind.Failed,VideoStatusText.ForOpenFailure(UvcErrorKind.None,0).Kind);
		}

		[TestMethod]
		public void StreamErrors_DisconnectedOffersReconnect() {
			VideoStatus videoStatus=VideoStatusText.ForStreamError(UvcErrorKind.DeviceLost,0);
			Assert.AreEqual(VideoStatusKind.Disconnected,videoStatus.Kind);
			Assert.AreEqual(VideoStatusText.TextDisconnected,videoStatus.Text);
			Assert.AreEqual(VideoStatusAction.Reconnect,videoStatus.Action);
			VideoStatus videoStatusAbort=VideoStatusText.ForStreamError(UvcErrorKind.ErrorAbort,HR_SOME_FAILURE);
			Assert.AreEqual(VideoStatusKind.StreamError,videoStatusAbort.Kind);
			Assert.AreEqual(VideoStatusAction.Reconnect,videoStatusAbort.Action);
			Assert.AreEqual("The camera stopped with an error. (0x80004005)",videoStatusAbort.ToString());
			Assert.AreEqual(VideoStatusKind.InUse,VideoStatusText.ForStreamError(UvcErrorKind.InUse,0).Kind);
		}

		[TestMethod]
		public void OtherStates() {
			Assert.AreEqual(VideoStatusText.TextNoCamera,VideoStatusText.NoCamera().Text);
			Assert.AreEqual(VideoStatusAction.Refresh,VideoStatusText.NoCamera().Action);
			Assert.IsTrue(VideoStatusText.Streaming().IsStreaming);
			Assert.IsFalse(VideoStatusText.Streaming().IsProblem);
			Assert.IsFalse(VideoStatusText.Starting().IsProblem);
			Assert.AreEqual(VideoStatusKind.ListFailed,VideoStatusText.ForListFailure(UvcErrorKind.Failed,HR_SOME_FAILURE).Kind);
			Assert.AreEqual(VideoStatusKind.NotAvailable,VideoStatusText.ForListFailure(UvcErrorKind.NotAvailable,0).Kind);
			Assert.AreEqual("",VideoStatusText.FormatDetail(0));
		}

		[TestMethod]
		public void EveryKind_HasTextAndNoVendorWords() {
			List<VideoStatus> listStatuses=new List<VideoStatus>();
			foreach(UvcErrorKind uvcErrorKind in Enum.GetValues(typeof(UvcErrorKind))) {
				listStatuses.Add(VideoStatusText.ForOpenFailure(uvcErrorKind,HR_SOME_FAILURE));
				listStatuses.Add(VideoStatusText.ForStreamError(uvcErrorKind,HR_SOME_FAILURE));
			}
			foreach(VideoStatus videoStatus in listStatuses) {
				Assert.AreNotEqual("",videoStatus.Text,videoStatus.Kind.ToString());
				//The window's texts speak of "the camera" and "other camera software" only (VendorNeutralityTests scans the source too).
				StringAssert.DoesNotMatch(videoStatus.Text,new System.Text.RegularExpressions.Regex("driver|vendor|manufacturer",System.Text.RegularExpressions.RegexOptions.IgnoreCase));
			}
		}

		#region Resolution choice
		private static List<UvcMode> GetModes() {
			return new List<UvcMode>() {
				new UvcMode(0,640,480,Ds.MEDIASUBTYPE_YUY2,16,333333),
				new UvcMode(1,1920,1080,Ds.MEDIASUBTYPE_YUY2,16,2000000),
				new UvcMode(2,1920,1080,Ds.MEDIASUBTYPE_MJPG,24,333333),
				new UvcMode(3,1280,720,Ds.MEDIASUBTYPE_MJPG,24,333333),
			};
		}

		[TestMethod]
		public void Resolution_AutomaticSelectedWhenNothingSaved() {
			List<UvcMode> listModes=GetModes();
			List<ResolutionChoice> listChoices=ResolutionChoice.GetChoices(listModes,new CameraConfig());
			Assert.AreEqual(5,listChoices.Count);
			Assert.IsTrue(listChoices[0].IsAutomatic);
			Assert.AreEqual("Automatic (1920x1080 MJPG 30 fps)",listChoices[0].Text);
			Assert.IsTrue(listChoices[0].IsSelected);
			Assert.AreEqual(1,listChoices.Count(x => x.IsSelected));
			CollectionAssert.AreEqual(new[] { 2,1,3,0 },listChoices.Skip(1).Select(x => x.Mode.Index).ToArray(),
				"Largest first; MJPG before YUY2 at the same size.");
			Assert.AreEqual("1920x1080 YUY2 5 fps",listChoices[2].Text);
		}

		[TestMethod]
		public void Resolution_SavedChoiceSelected_AndApplied() {
			List<UvcMode> listModes=GetModes();
			CameraConfig cameraConfig=new CameraConfig();
			ResolutionChoice.Apply(cameraConfig,listModes[1]);
			Assert.AreEqual(1920,cameraConfig.Width);
			Assert.AreEqual(1080,cameraConfig.Height);
			Assert.AreEqual("YUY2",cameraConfig.Subtype);
			List<ResolutionChoice> listChoices=ResolutionChoice.GetChoices(listModes,cameraConfig);
			Assert.AreEqual(1,listChoices.Count(x => x.IsSelected));
			Assert.AreSame(listModes[1],listChoices.Single(x => x.IsSelected).Mode);
			Assert.AreSame(listModes[1],ModeSelector.Select(listModes,cameraConfig.Width,cameraConfig.Height,cameraConfig.Subtype).Mode,
				"What is saved is what ModeSelector opens.");
			ResolutionChoice.Apply(cameraConfig,null);
			Assert.AreEqual(0,cameraConfig.Width);
			Assert.AreEqual(0,cameraConfig.Height);
			Assert.AreEqual(ModeSelector.SubtypeAuto,cameraConfig.Subtype);
		}

		[TestMethod]
		public void Resolution_SavedButNotOffered_AutomaticSaysSo() {
			CameraConfig cameraConfig=new CameraConfig() { Width=4000,Height=3000,Subtype="MJPG" };
			List<ResolutionChoice> listChoices=ResolutionChoice.GetChoices(GetModes(),cameraConfig);
			Assert.IsTrue(listChoices[0].IsSelected);
			StringAssert.Contains(listChoices[0].Text,"not offered by this camera");
			Assert.AreEqual(1,listChoices.Count(x => x.IsSelected));
			List<ResolutionChoice> listNoModes=ResolutionChoice.GetChoices(new List<UvcMode>(),new CameraConfig());
			Assert.AreEqual(1,listNoModes.Count);
			Assert.AreEqual("Automatic",listNoModes[0].Text);
		}
		#endregion Resolution choice

		#region Trigger notes
		[TestMethod]
		public void TriggerNotes_NothingSetUp() {
			CollectionAssert.AreEqual(new[] { TriggerNotes.TextNoneSetUp },TriggerNotes.GetLines(null,new CameraConfig()));
			CameraConfig cameraConfig=new CameraConfig() { StillEnabled=true };
			CollectionAssert.AreEqual(new[] { TriggerNotes.TextNotRunning },TriggerNotes.GetLines(null,cameraConfig));
		}

		[TestMethod]
		public void TriggerNotes_UnsupportedPropertyGivesANote() {
			FakeUvcBackend fakeUvcBackend=new FakeUvcBackend();
			fakeUvcBackend.AddDevice();
			FakeFrameSource fakeFrameSource=(FakeFrameSource)fakeUvcBackend.Open(fakeUvcBackend.ListDevices[0]);
			fakeFrameSource.Start(null,false);
			CameraConfig cameraConfig=new CameraConfig() { StillEnabled=true };
			cameraConfig.ControlPoll.Enabled=true;
			cameraConfig.ControlPoll.SetBinding(new ControlBinding(new ControlId(UvcControlInterface.CameraControl,2),0x1,ControlLevel.High));
			LevelInputConfig levelInputConfig=new LevelInputConfig(1) { Enabled=true };
			cameraConfig.Levels.Add(levelInputConfig);
			using(TriggerEngine triggerEngine=new TriggerEngine(fakeFrameSource,cameraConfig,() => 0,null,false)) {
				triggerEngine.Start();
				triggerEngine.PollOnce();
				List<string> listLines=TriggerNotes.GetLines(triggerEngine,cameraConfig);
				Assert.AreEqual(3,listLines.Count);
				StringAssert.StartsWith(listLines[0],"Still-pin camera button: off, because ");
				StringAssert.StartsWith(listLines[1],"Control camera button: off, because the camera does not support");
				StringAssert.StartsWith(listLines[2],"Level 1 (pause preview): off, because it is enabled, but");
			}
			fakeFrameSource.SetControlValue(UvcControlInterface.CameraControl,2,0);
			using(TriggerEngine triggerEngine=new TriggerEngine(fakeFrameSource,cameraConfig,() => 0,null,false)) {
				triggerEngine.Start();
				triggerEngine.PollOnce();
				Assert.AreEqual("Control camera button: on",TriggerNotes.GetLines(triggerEngine,cameraConfig)[1]);
			}
			Assert.AreEqual("X: off",TriggerNotes.GetLine("X",TriggerInputState.Failed,""));
			Assert.AreEqual("X: off",TriggerNotes.GetLine("X",TriggerInputState.Off,"ignored"));
		}
		#endregion Trigger notes
	}
}
