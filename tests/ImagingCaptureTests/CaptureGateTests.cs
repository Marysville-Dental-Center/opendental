// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Threading;
using ImagingCapture.Video;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingCapture.Tests {
	///<summary>CaptureGate: the Video window's re-entrancy guard and drop rules (design 8.4 hand-off safety, 8.10).</summary>
	[TestClass]
	public class CaptureGateTests {
		private CaptureGate _captureGate;

		[TestInitialize]
		public void Init() {
			_captureGate=new CaptureGate();
		}

		private CaptureDropReason TryBegin(CaptureRequestSource captureRequestSource=CaptureRequestSource.Button,bool isWindowEnabled=true,
			bool isMainWindowSpaceOn=true,bool hasPatient=true,bool isCameraStreaming=true)
		{
			return _captureGate.TryBegin(captureRequestSource,isWindowEnabled,isMainWindowSpaceOn,hasPatient,isCameraStreaming);
		}

		[TestMethod]
		public void Accepted_HoldsTheGateUntilEnd() {
			Assert.AreEqual(CaptureDropReason.None,TryBegin());
			Assert.IsTrue(_captureGate.IsHandlerRunning);
			foreach(CaptureRequestSource captureRequestSource in Enum.GetValues(typeof(CaptureRequestSource))) {
				Assert.AreEqual(CaptureDropReason.Busy,TryBegin(captureRequestSource),
					"No new photo while the previous handler runs, whatever asks: "+captureRequestSource);
			}
			_captureGate.End();
			Assert.IsFalse(_captureGate.IsHandlerRunning);
			Assert.AreEqual(CaptureDropReason.None,TryBegin(CaptureRequestSource.Trigger));
			_captureGate.End();
			_captureGate.End();
			Assert.AreEqual(CaptureDropReason.None,TryBegin(),"End twice is harmless.");
		}

		[TestMethod]
		public void Closing_DropsEverythingFromThenOn() {
			Assert.AreEqual(CaptureDropReason.None,TryBegin());
			_captureGate.BeginClosing();
			Assert.IsTrue(_captureGate.IsClosing);
			Assert.AreEqual(CaptureDropReason.Closing,TryBegin(),"Closing is checked before Busy.");
			_captureGate.End();
			foreach(CaptureRequestSource captureRequestSource in Enum.GetValues(typeof(CaptureRequestSource))) {
				Assert.AreEqual(CaptureDropReason.Closing,TryBegin(captureRequestSource),"A capture requested while closing is dropped.");
			}
		}

		[TestMethod]
		public void Closing_FromAnotherThread() {
			Thread thread=new Thread(() => _captureGate.BeginClosing());
			thread.Start();
			Assert.IsTrue(thread.Join(5000));
			Assert.AreEqual(CaptureDropReason.Closing,TryBegin());
		}

		[TestMethod]
		public void WindowDisabled_PressesIgnored() {
			Assert.AreEqual(CaptureDropReason.WindowDisabled,TryBegin(CaptureRequestSource.Trigger,isWindowEnabled:false));
			Assert.AreEqual(CaptureDropReason.WindowDisabled,TryBegin(CaptureRequestSource.SpaceInMainWindow,isWindowEnabled:false));
			Assert.IsFalse(_captureGate.IsHandlerRunning,"A dropped request does not hold the gate.");
			Assert.AreEqual(CaptureDropReason.None,TryBegin(CaptureRequestSource.Trigger),"Accepted again once the window is enabled.");
		}

		[TestMethod]
		public void MainWindowSpace_OffDropsOnlyThatSource() {
			Assert.AreEqual(CaptureDropReason.MainWindowSpaceOff,TryBegin(CaptureRequestSource.SpaceInMainWindow,isMainWindowSpaceOn:false));
			Assert.AreEqual(CaptureDropReason.None,TryBegin(CaptureRequestSource.SpaceInWindow,isMainWindowSpaceOn:false),
				"Space typed in the Video window itself still works.");
			_captureGate.End();
			Assert.AreEqual(CaptureDropReason.None,TryBegin(CaptureRequestSource.SpaceInMainWindow,isMainWindowSpaceOn:true));
		}

		[TestMethod]
		public void NoPatientAndNoCamera() {
			Assert.AreEqual(CaptureDropReason.NoPatient,TryBegin(hasPatient:false));
			Assert.AreEqual(CaptureDropReason.NoCamera,TryBegin(isCameraStreaming:false));
			Assert.IsFalse(_captureGate.IsHandlerRunning);
		}

		[TestMethod]
		public void DropTexts() {
			Assert.AreEqual("",CaptureGate.GetDropText(CaptureDropReason.None));
			Assert.AreEqual("",CaptureGate.GetDropText(CaptureDropReason.Closing));
			Assert.AreEqual("",CaptureGate.GetDropText(CaptureDropReason.MainWindowSpaceOff));
			foreach(CaptureDropReason captureDropReason in new[] { CaptureDropReason.Busy,CaptureDropReason.WindowDisabled,CaptureDropReason.NoPatient,
				CaptureDropReason.NoCamera,CaptureDropReason.NoPicture,CaptureDropReason.Failed })
			{
				Assert.AreNotEqual("",CaptureGate.GetDropText(captureDropReason),captureDropReason.ToString());
			}
			Assert.IsTrue(CaptureGate.IsLogged(CaptureDropReason.Busy));
			Assert.IsTrue(CaptureGate.IsLogged(CaptureDropReason.WindowDisabled));
			Assert.IsFalse(CaptureGate.IsLogged(CaptureDropReason.Closing));
			Assert.IsFalse(CaptureGate.IsLogged(CaptureDropReason.MainWindowSpaceOff));
		}
	}
}
