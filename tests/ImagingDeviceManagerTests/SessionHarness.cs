// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using ImagingDeviceManager.TwainProtocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingDeviceManager.Tests {
	///<summary>A TwainSession on a FakeTwainDsm that also offers a FakeHelperSource (the contract 1.1 profile), with the session's log
	///collected. Each test class creates one in its TestInitialize and disposes it in TestCleanup.</summary>
	internal sealed class SessionHarness:IDisposable {
		public readonly FakeTwainDsm Fake=new FakeTwainDsm();
		public readonly FakeHelperSource Helper=new FakeHelperSource();
		public readonly TwainSession Session;
		public readonly List<string> ListLog=new List<string>();
		public int CountLoads;

		public SessionHarness() {
			Fake.AddHelperSource(Helper);
			Session=new TwainSession(Load);
			Session.LoopTimeoutMilliseconds=10000;//A broken test fails instead of hanging.
			Session.Logged+=ListLog.Add;
		}

		private ITwainDsm Load(out string errorText) {
			errorText="";
			CountLoads++;
			return Fake;
		}

		public static FakePage Exposure(int width) {
			return FakePage.CreateGray8(width,5,FakeHelperSource.Dpi,(x,y) => (byte)(x*7+y));
		}

		///<summary>Opens the helper source by name, as Mount/Acquire does, and acquires one image.</summary>
		public Imaging.DibImage AcquireByName() {
			Assert.IsTrue(Session.OpenSource(Helper.ProductName),Session.ErrorText);
			return Session.Acquire(IntPtr.Zero);
		}

		public bool HasLog(string fragment) {
			return ListLog.Any(x => x.IndexOf(fragment,StringComparison.OrdinalIgnoreCase)>=0);
		}

		public string LogText => string.Join("\r\n",ListLog);

		public string CallText => string.Join("\r\n",Fake.ListCalls);

		public int IndexOfCall(Func<FakeCall,bool> predicate,int start=0) {
			for(int i=start;i<Fake.ListCalls.Count;i++) {
				if(predicate(Fake.ListCalls[i])) {
					return i;
				}
			}
			return -1;
		}

		///<summary>Every TWRC_FAILURE from the source is followed at once by a DAT_STATUS that returns its condition code.</summary>
		public void AssertStatusReadAfterEveryFailure() {
			List<FakeCall> listCalls=Fake.ListCalls;
			int countFailures=0;
			for(int i=0;i<listCalls.Count;i++) {
				if(listCalls[i].Rc!=Tw.TWRC_FAILURE || listCalls[i].Name=="STATUS") {
					continue;
				}
				countFailures++;
				Assert.IsTrue(i+1<listCalls.Count && listCalls[i+1].Name=="STATUS","No DAT_STATUS right after "+listCalls[i]+"\r\n"+CallText);
				Assert.AreEqual(listCalls[i].Condition,listCalls[i+1].Condition,"DAT_STATUS after "+listCalls[i]);
			}
			Assert.IsTrue(countFailures>0,"The scenario should contain failures.");
		}

		public void Dispose() {
			Session.Dispose();
			//These tests run Win32 message loops on the test adapter's STA thread; see EZTwainTests.Cleanup.
			GC.Collect();
			GC.WaitForPendingFinalizers();
		}
	}
}
