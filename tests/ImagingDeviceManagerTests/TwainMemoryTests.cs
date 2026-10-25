// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using ImagingDeviceManager.TwainProtocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingDeviceManager.Tests {
	///<summary>Handle ownership (design 7.1, twain-contract 1.1 section 6.4): handles the source hands over are freed with the DSM's
	///DSM_MemFree when the DSM handed out its memory functions (DAT_ENTRYPOINT), otherwise with GlobalFree, and the session's own
	///capability containers are allocated the same way.</summary>
	[TestClass]
	public class TwainMemoryTests {
		private FakeTwainDsm _fake;
		private RecordingGlobalMemory _globalMemory;

		[TestInitialize]
		public void Setup() {
			_fake=new FakeTwainDsm();
			_fake.ListPages.Add(FakePage.CreateRgb(12,9,300,(x,y) => Color.FromArgb(x*20,y*25,90)));
			_fake.PixelFlavor=Tw.TWPF_CHOCOLATE;//The source then hands over a capability container too.
			EZTwain.Reset((out string errorText) => {
				errorText="";
				return _fake;
			});
			EZTwain.Session.LoopTimeoutMilliseconds=10000;
			_globalMemory=new RecordingGlobalMemory();
			EZTwain.Session.GlobalMemory=_globalMemory;
		}

		[TestCleanup]
		public void Cleanup() {
			EZTwain.Reset(null);
			GC.Collect();
			GC.WaitForPendingFinalizers();
		}

		private void AcquireOneNativeImage() {
			Assert.IsTrue(EZTwain.OpenSource("Fake Sensor"),EZTwain.LastErrorText());
			IntPtr hdib=EZTwain.Acquire(IntPtr.Zero);
			Assert.AreNotEqual(IntPtr.Zero,hdib,EZTwain.LastErrorText());
			Assert.AreEqual(12,EZTwain.DIB_Width(hdib));
			EZTwain.DIB_Free(hdib);
			Assert.IsTrue(_fake.ListLog.Contains("NATIVEXFER"));
		}

		[TestMethod]
		public void LegacyDsm_SourceHandlesFreedWithGlobalFree() {
			AcquireOneNativeImage();
			Assert.AreEqual(0,_fake.CountEntryPointGet,"A DSM without DF_DSM2 is not asked for entry points.");
			Assert.AreEqual(2,_fake.ListSourceHandles.Count,"The native DIB and the pixel flavor container.");
			foreach(IntPtr handle in _fake.ListSourceHandles) {
				Assert.IsTrue(_globalMemory.ListFreed.Contains(handle),"Every handle from the source is freed with GlobalFree.");
			}
			Assert.AreEqual(0,_fake.DsmMemory.CountFree+_fake.DsmMemory.CountForeignFree,"DSM_MemFree is never called.");
			Assert.IsTrue(_globalMemory.CountAllocate>0,"Capability containers come from GlobalAlloc.");
		}

		[TestMethod]
		public void Dsm2_SourceHandlesFreedWithDsmMemFree() {
			_fake.SupplyEntryPoints=true;
			AcquireOneNativeImage();
			Assert.AreEqual(1,_fake.CountEntryPointGet,"The session asks a DF_DSM2 DSM for its memory functions once it is open.");
			Assert.AreEqual(2,_fake.ListSourceHandles.Count);
			foreach(IntPtr handle in _fake.ListSourceHandles) {
				Assert.IsTrue(_fake.DsmMemory.ListFreed.Contains(handle),"Every handle from the source is freed with DSM_MemFree.");
				Assert.IsFalse(_globalMemory.ListFreed.Contains(handle),"No handle from the source is freed with GlobalFree.");
			}
			Assert.AreEqual(0,_fake.DsmMemory.CountForeignFree,"Nothing else is passed to DSM_MemFree.");
			Assert.AreEqual(0,_fake.DsmMemory.CountOutstanding,"Capability containers are allocated and freed with the DSM functions too.");
			Assert.IsTrue(_fake.DsmMemory.CountAllocate>_fake.ListSourceHandles.Count,"The session's own containers come from DSM_MemAllocate.");
			Assert.AreEqual(_fake.DsmMemory.CountLock,_fake.DsmMemory.CountUnlock,"Every DSM_MemLock is matched by DSM_MemUnlock.");
			Assert.AreEqual(0,_globalMemory.ListFreed.Count+_globalMemory.CountAllocate,"The Global functions are not used at all.");
		}

		[TestMethod]
		public void Dsm2_EntryPointsAskedAgainAfterTheDsmReopens() {
			_fake.SupplyEntryPoints=true;
			AcquireOneNativeImage();
			AcquireOneNativeImage();
			Assert.AreEqual(2,_fake.CountEntryPointGet,"The memory functions belong to one MSG_OPENDSM.");
			Assert.AreEqual(0,_fake.DsmMemory.CountOutstanding);
			Assert.AreEqual(0,_fake.DsmMemory.CountForeignFree);
		}

		[TestMethod]
		public void DsmMemory_NeedsAllFourFunctions() {
			TW_ENTRYPOINT entryPoint=new TW_ENTRYPOINT { DSM_MemAllocate=new IntPtr(4),DSM_MemFree=new IntPtr(8),DSM_MemLock=new IntPtr(12) };
			Assert.IsNull(DsmTwainMemory.TryCreate(entryPoint),"Without DSM_MemUnlock the Global functions are used.");
		}

		[TestMethod]
		public void GlobalMemory_ReportsBlockSizeButDsmMemoryDoesNot() {
			IntPtr handle=GlobalTwainMemory.Instance.Allocate(100);
			try {
				Assert.IsTrue(GlobalTwainMemory.Instance.GetSize(handle)>=100);
			}
			finally {
				GlobalTwainMemory.Instance.Free(handle);
			}
			FakeDsmMemory fakeDsmMemory=new FakeDsmMemory();
			TW_ENTRYPOINT entryPoint=new TW_ENTRYPOINT();
			IntPtr pEntryPoint=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TW_ENTRYPOINT)));
			try {
				Marshal.StructureToPtr(entryPoint,pEntryPoint,false);
				fakeDsmMemory.WriteEntryPoint(pEntryPoint);
				entryPoint=(TW_ENTRYPOINT)Marshal.PtrToStructure(pEntryPoint,typeof(TW_ENTRYPOINT));
			}
			finally {
				Marshal.FreeHGlobal(pEntryPoint);
			}
			ITwainMemory dsmMemory=DsmTwainMemory.TryCreate(entryPoint);
			IntPtr handleDsm=dsmMemory.Allocate(100);
			Assert.AreEqual(0,dsmMemory.GetSize(handleDsm),"A DSM handle is opaque: GlobalSize is never called on it.");
			dsmMemory.Free(handleDsm);
			Assert.AreEqual(1,fakeDsmMemory.CountFree);
			Assert.AreEqual(0,fakeDsmMemory.CountOutstanding);
		}
	}
}
