// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using ImagingDeviceManager.Capture;
using ImagingDeviceManager.Journal;
using ImagingDeviceManager.TwainProtocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingDeviceManager.Tests {
	///<summary>A TwainCaptureSession on a FakeTwainDsm with a FakeHelperSource (the contract 1.1 profile), capture folders in a temporary
	///folder, and every event recorded. The fake is driven on the session's worker thread; read its call list only after an Ended (the
	///worker is idle then) or after Close.</summary>
	internal sealed class CaptureSessionHarness:IDisposable {
		public readonly FakeTwainDsm Fake=new FakeTwainDsm();
		public readonly FakeHelperSource Helper=new FakeHelperSource();
		public readonly string Folder;
		public readonly CaptureFolders Folders;
		public readonly TwainCaptureSession Capture;
		public readonly TwainCaptureOptions Options=new TwainCaptureOptions {
			ImagingDeviceNum=12,
			TickMilliseconds=20,
			RefusedDisableWait=TimeSpan.FromSeconds(2),
			OpenTimeout=TimeSpan.FromSeconds(10),
		};
		public readonly List<AcquiredImage> ListImages=new List<AcquiredImage>();
		public readonly List<AcquiredImage> ListQuarantined=new List<AcquiredImage>();
		public readonly List<TwainCaptureEnded> ListEnded=new List<TwainCaptureEnded>();
		public readonly List<TwainCaptureStatus> ListStatus=new List<TwainCaptureStatus>();
		public readonly List<string> ListLog=new List<string>();
		///<summary>The managed thread id of every event, to check they are all raised on the worker.</summary>
		public readonly HashSet<int> HashSetEventThreads=new HashSet<int>();
		///<summary>Journal files that existed when ImageAcquired was raised.</summary>
		public readonly List<bool> ListJournalExistedAtEvent=new List<bool>();
		private readonly object _lock=new object();
		///<summary>Runs inside ImageAcquired, on the worker, after the image is recorded.</summary>
		public Action<AcquiredImage> ActionOnImage;
		public int CountLoads;

		public CaptureSessionHarness() {
			Fake.AddHelperSource(Helper);
			Folder=Path.Combine(Path.GetTempPath(),"CaptureSessionTests_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(Folder);
			Folders=CaptureFolders.CreateForFolder(Folder);
			Capture=new TwainCaptureSession(Load,Folders,() => DateTime.Now);
			Capture.ImageAcquired+=image => {
				Record(() => {
					ListImages.Add(image);
					ListJournalExistedAtEvent.Add(image.JournalPath!=null && File.Exists(image.JournalPath));
				});
				ActionOnImage?.Invoke(image);
			};
			Capture.ImageQuarantined+=image => Record(() => ListQuarantined.Add(image));
			Capture.Ended+=ended => Record(() => ListEnded.Add(ended));
			Capture.StatusChanged+=status => Record(() => ListStatus.Add(status));
			Capture.Logged+=line => Record(() => ListLog.Add(line));
		}

		private ITwainDsm Load(out string errorText) {
			errorText="";
			CountLoads++;
			return Fake;
		}

		private void Record(Action action) {
			lock(_lock) {
				HashSetEventThreads.Add(Thread.CurrentThread.ManagedThreadId);
				action();
				Monitor.PulseAll(_lock);
			}
		}

		public TwainCaptureOpenResult Open() {
			return Capture.Open(Helper.ProductName,Options);
		}

		public void OpenOrFail() {
			TwainCaptureOpenResult result=Open();
			Assert.IsTrue(result.IsOpen,result.ErrorText);
		}

		///<summary>Waits until condition is true (checked under the event lock), or fails after timeoutMs.</summary>
		public void WaitUntil(Func<bool> condition,string what,int timeoutMs=10000) {
			DateTime end=DateTime.UtcNow.AddMilliseconds(timeoutMs);
			lock(_lock) {
				while(!condition()) {
					TimeSpan left=end-DateTime.UtcNow;
					if(left<=TimeSpan.Zero) {
						Assert.Fail("Timed out waiting for "+what+".\r\n"+LogText);
					}
					Monitor.Wait(_lock,left);
				}
			}
		}

		public TwainCaptureEnded WaitForEnded(int enableId,int timeoutMs=10000) {
			Assert.IsTrue(enableId>0,"Enable returned 0.");
			WaitUntil(() => ListEnded.Any(x => x.EnableId==enableId),"the end of enable "+enableId,timeoutMs);
			lock(_lock) {
				return ListEnded.Single(x => x.EnableId==enableId);
			}
		}

		public List<AcquiredImage> Images {
			get {
				lock(_lock) {
					return new List<AcquiredImage>(ListImages);
				}
			}
		}

		public List<TwainCaptureState> States {
			get {
				lock(_lock) {
					return ListStatus.Select(x => x.State).ToList();
				}
			}
		}

		public List<TwainCaptureStatus> Statuses {
			get {
				lock(_lock) {
					return new List<TwainCaptureStatus>(ListStatus);
				}
			}
		}

		public int CountEnded {
			get {
				lock(_lock) {
					return ListEnded.Count;
				}
			}
		}

		///<summary>The log lines that contain fragment (any case).</summary>
		public int CountLogLines(string fragment) {
			lock(_lock) {
				return ListLog.Count(x => x.IndexOf(fragment,StringComparison.OrdinalIgnoreCase)>=0);
			}
		}

		public string LogText {
			get {
				lock(_lock) {
					return string.Join("\r\n",ListLog);
				}
			}
		}

		public bool HasLog(string fragment) {
			lock(_lock) {
				return ListLog.Any(x => x.IndexOf(fragment,StringComparison.OrdinalIgnoreCase)>=0);
			}
		}

		public string CallText => string.Join("\r\n",Fake.ListCalls);

		public int IndexOfCall(Func<FakeCall,bool> predicate,int start=0) {
			for(int i=start;i<Fake.ListCalls.Count;i++) {
				if(predicate(Fake.ListCalls[i])) {
					return i;
				}
			}
			return -1;
		}

		public void Close() {
			Capture.BeginClose();
			Assert.IsTrue(Capture.WaitForClose(TimeSpan.FromSeconds(15)),"The worker ends.\r\n"+LogText);
			Assert.AreEqual(TwainCaptureState.Closed,Capture.State);
		}

		///<summary>Every MSG_DISABLEDS went to the source in state 5.</summary>
		public void AssertDisabledOnlyInState5() {
			Assert.IsTrue(Fake.ListDisableStates.Count>0,"The scenario should disable the source.");
			Assert.IsTrue(Fake.ListDisableStates.All(x => x==5),"MSG_DISABLEDS outside state 5:\r\n"+CallText);
		}

		///<summary>No MSG_SET on any fixed capability of the contract (section 4).</summary>
		public void AssertNoFixedCapabilitySet() {
			ushort[] arrayFixed={ Tw.CAP_INDICATORS,Tw.ICAP_PIXELFLAVOR,Tw.ICAP_BITORDER,Tw.ICAP_PLANARCHUNKY,Tw.ICAP_SUPPORTEDSIZES,
				Tw.ICAP_UNDEFINEDIMAGESIZE,Tw.ICAP_PIXELTYPE,Tw.CAP_DEVICEONLINE,Tw.CAP_UICONTROLLABLE,Tw.ICAP_SUPPORTEDEXTIMAGEINFO };
			Assert.IsFalse(Fake.ListCalls.Any(x => x.Name=="CAP/SET" && arrayFixed.Contains(x.Cap)),CallText);
			Assert.AreEqual(0,Helper.CountFixedCapSet);
		}

		public void Dispose() {
			try {
				Capture.BeginClose();
				Capture.WaitForClose(TimeSpan.FromSeconds(15));
			}
			catch {
			}
			try {
				Directory.Delete(Folder,true);
			}
			catch {
			}
		}
	}
}
