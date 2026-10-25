// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ImagingDeviceManager.Imaging;
using ImagingDeviceManager.TwainProtocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingDeviceManager.Tests {
	///<summary>End to end tests of the EZTwain facade against the simulated data source manager, following the call sequences
	///that ControlImages, FormImagingDeviceEdit and FormImagingSetup use.</summary>
	[TestClass]
	public class EZTwainTests {
		private FakeTwainDsm _fake;
		private int _countLoads;
		private string _tempDir;

		private static Color ColorAt(int x,int y) {
			return Color.FromArgb(255,(x*29+y*7)&0xff,(x*3+y*41)&0xff,(x*17+y*5)&0xff);
		}

		[TestInitialize]
		public void Setup() {
			_fake=new FakeTwainDsm();
			_fake.ListPages.Add(FakePage.CreateRgb(40,30,300,ColorAt));
			_countLoads=0;
			EZTwain.Reset((out string errorText) => {
				errorText="";
				_countLoads++;
				return _fake;
			});
			EZTwain.Session.LoopTimeoutMilliseconds=10000;//A broken test fails instead of hanging.
			_tempDir=Path.Combine(Path.GetTempPath(),"ImagingDeviceManagerTests_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_tempDir);
		}

		[TestCleanup]
		public void Cleanup() {
			EZTwain.Reset(null);
			//These tests run Win32 message loops on the MSTest v1 adapter's STA thread. Without finalizing here, while that
			//thread can still pump messages, the test host deadlocks in finalization when it unloads the test AppDomain after
			//the last test (seen with vstest.console 17.14; the same tests in a plain console process exit normally).
			GC.Collect();
			GC.WaitForPendingFinalizers();
			try {
				Directory.Delete(_tempDir,true);
			}
			catch {
			}
		}

		private void AssertAllClosed() {
			Assert.IsFalse(_fake.IsEnabled,"source still enabled");
			Assert.IsFalse(_fake.IsSourceOpen,"source still open");
			Assert.IsFalse(_fake.IsDsmOpen,"DSM still open");
			Assert.AreEqual(1,EZTwain.Session.State,"TWAIN state");
		}

		private void AssertMatchesPage(Bitmap bitmap,FakePage page) {
			Assert.AreEqual(page.Width,bitmap.Width);
			Assert.AreEqual(page.Height,bitmap.Height);
			for(int y=0;y<page.Height;y+=3) {
				for(int x=0;x<page.Width;x+=3) {
					Color expected=ColorAt(x,y);
					Color actual=bitmap.GetPixel(x,y);
					Assert.AreEqual(expected.ToArgb(),actual.ToArgb(),"pixel "+x+","+y);
				}
			}
		}

		[TestMethod]
		public void GetSourceList_ListsInstalledSources() {
			Assert.IsTrue(EZTwain.GetSourceList());
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.Append("left over text");
			Assert.IsTrue(EZTwain.GetNextSourceName(stringBuilder));
			Assert.AreEqual("Fake Scanner",stringBuilder.ToString());
			Assert.IsTrue(EZTwain.GetNextSourceName(stringBuilder));
			Assert.AreEqual("Fake Sensor",stringBuilder.ToString());
			Assert.IsFalse(EZTwain.GetNextSourceName(stringBuilder));
			AssertAllClosed();
		}

		[TestMethod]
		public void GetSourceList_NoDsmInstalled_ReportsLoadError() {
			EZTwain.Reset((out string errorText) => {
				errorText="nothing installed";
				return null;
			});
			Assert.IsFalse(EZTwain.GetSourceList());
			Assert.IsFalse(EZTwain.GetNextSourceName(new StringBuilder()));
			Assert.AreEqual((int)EZTwainErrorCode.EZTEC_LOAD_DSM,EZTwain.LastErrorCode());
			Assert.AreEqual("nothing installed",EZTwain.LastErrorText());
			Assert.IsFalse(EZTwain.OpenDefaultSource());
			Assert.AreEqual(IntPtr.Zero,EZTwain.Acquire(IntPtr.Zero));
			Assert.AreEqual((int)EZTwainErrorCode.EZTEC_LOAD_DSM,EZTwain.LastErrorCode());
		}

		[TestMethod]
		public void Acquire_DocumentScanSequence_ReturnsImage() {
			//Same calls as ControlImages.ToolBarScan_Click with the scanner dialog hidden.
			EZTwain.SetHideUI(true);
			Assert.IsTrue(EZTwain.OpenDefaultSource());
			Assert.AreEqual("Fake Scanner",_fake.OpenSourceName);
			Assert.IsTrue(EZTwain.SetResolution(300));
			Assert.IsTrue(EZTwain.SetPixelType(2));
			EZTwain.SetJpegQuality(80);
			Assert.IsTrue(EZTwain.SetXferMech(EZTwain.XFERMECH_MEMORY));
			IntPtr hdib=EZTwain.Acquire(IntPtr.Zero);
			Assert.AreEqual(0,EZTwain.LastErrorCode(),EZTwain.LastErrorText());
			Assert.AreNotEqual(IntPtr.Zero,hdib);
			Assert.AreEqual(TW_FIX32.FromDouble(300).ToUInt32(),(uint)_fake.DictCapsSet[Tw.ICAP_XRESOLUTION]);
			Assert.AreEqual(TW_FIX32.FromDouble(300).ToUInt32(),(uint)_fake.DictCapsSet[Tw.ICAP_YRESOLUTION]);
			Assert.AreEqual(2,_fake.DictCapsSet[Tw.ICAP_PIXELTYPE]);
			Assert.AreEqual(1,_fake.DictCapsSet[Tw.CAP_XFERCOUNT],"A single acquire asks for one image.");
			Assert.AreEqual(Tw.TWSX_MEMORY,_fake.DictCapsSet[Tw.ICAP_XFERMECH]);
			Assert.AreEqual(0,_fake.LastShowUI);
			Assert.AreEqual(_fake.HwndDsm,_fake.LastParent,"The source's parent is the window given to the DSM.");
			Assert.IsTrue(_fake.ListLog.Count(x => x=="MEMXFER")>1,"The image should arrive in several strips.");
			Assert.AreEqual(300,EZTwain.DIB_XResolution(hdib));
			Assert.AreEqual(300,EZTwain.DIB_YResolution(hdib));
			Assert.AreEqual(40,EZTwain.DIB_Width(hdib));
			Assert.AreEqual(30,EZTwain.DIB_Height(hdib));
			Assert.AreEqual(24,EZTwain.DIB_Depth(hdib));
			IntPtr hBitmap=EZTwain.DIB_ToDibSection(hdib);
			Assert.AreNotEqual(IntPtr.Zero,hBitmap);
			using(Bitmap bitmap=Image.FromHbitmap(hBitmap)) {
				AssertMatchesPage(bitmap,_fake.ListPages[0]);
			}
			Twain.DeleteObject(hBitmap);
			Assert.IsNull(DibHandleTable.Get(hdib),"DIB_ToDibSection frees the image handle.");
			EZTwain.DIB_Free(hdib);//Callers still free it afterwards; that must be harmless.
			AssertAllClosed();
		}

		[TestMethod]
		public void Acquire_NativeTransfer_ShowsUIAndReturnsImage() {
			EZTwain.SetHideUI(false);
			Assert.IsTrue(EZTwain.OpenDefaultSource());
			IntPtr hdib=EZTwain.Acquire(IntPtr.Zero);
			Assert.AreEqual(0,EZTwain.LastErrorCode(),EZTwain.LastErrorText());
			Assert.AreEqual(1,_fake.LastShowUI);
			Assert.IsTrue(_fake.ListLog.Contains("NATIVEXFER"));
			using(Bitmap bitmap=(Bitmap)EZTwain.DIB_ToImage(hdib)) {
				AssertMatchesPage(bitmap,_fake.ListPages[0]);
				Assert.AreEqual(300,bitmap.HorizontalResolution,0.01);
			}
			Assert.IsNotNull(DibHandleTable.Get(hdib),"DIB_ToImage leaves the handle valid.");
			EZTwain.DIB_Free(hdib);
			Assert.IsNull(DibHandleTable.Get(hdib));
			AssertAllClosed();
		}

		[TestMethod]
		public void Acquire_WithoutOpenSource_OpensDefault() {
			IntPtr hdib=EZTwain.Acquire(IntPtr.Zero);
			Assert.AreNotEqual(IntPtr.Zero,hdib);
			Assert.AreEqual("Fake Scanner",_fake.OpenSourceName);
			EZTwain.DIB_Free(hdib);
			AssertAllClosed();
		}

		[TestMethod]
		public void Acquire_SingleMode_DiscardsRestOfBatch() {
			_fake.ListPages.Add(FakePage.CreateRgb(10,10,300,ColorAt));
			_fake.ListPages.Add(FakePage.CreateRgb(10,10,300,ColorAt));
			IntPtr hdib=EZTwain.Acquire(IntPtr.Zero);
			Assert.AreNotEqual(IntPtr.Zero,hdib);
			Assert.AreEqual(40,EZTwain.DIB_Width(hdib),"The first page is returned.");
			Assert.IsTrue(_fake.ListLog.Contains("RESET"),"Remaining pages are discarded.");
			EZTwain.DIB_Free(hdib);
			AssertAllClosed();
		}

		[TestMethod]
		public void Acquire_UserClosesScannerDialog_ReturnsUserCancel() {
			_fake.EnableBehavior=FakeEnableBehavior.CloseRequest;
			EZTwain.OpenDefaultSource();
			Assert.AreEqual(IntPtr.Zero,EZTwain.Acquire(IntPtr.Zero));
			Assert.AreEqual((int)EZTwainErrorCode.EZTEC_USER_CANCEL,EZTwain.LastErrorCode());
			AssertAllClosed();
		}

		[TestMethod]
		public void Acquire_EnableCancelled_ReturnsUserCancel() {
			_fake.EnableResult=Tw.TWRC_CANCEL;
			EZTwain.OpenDefaultSource();
			Assert.AreEqual(IntPtr.Zero,EZTwain.Acquire(IntPtr.Zero));
			Assert.AreEqual((int)EZTwainErrorCode.EZTEC_USER_CANCEL,EZTwain.LastErrorCode());
			AssertAllClosed();
		}

		[TestMethod]
		public void Acquire_EnableFails_ReportsEnableFailure() {
			_fake.EnableResult=Tw.TWRC_FAILURE;
			_fake.EnableFailCondition=Tw.TWCC_CHECKDEVICEONLINE;
			EZTwain.OpenDefaultSource();
			Assert.AreEqual(IntPtr.Zero,EZTwain.Acquire(IntPtr.Zero));
			Assert.AreEqual((int)EZTwainErrorCode.EZTEC_ENABLE_FAILED,EZTwain.LastErrorCode());
			StringAssert.Contains(EZTwain.LastErrorText(),"not connected");
			AssertAllClosed();
		}

		[TestMethod]
		public void Acquire_TransferCancelled_ReturnsUserCancel() {
			_fake.ListPages[0].IsCancelled=true;
			EZTwain.OpenDefaultSource();
			Assert.AreEqual(IntPtr.Zero,EZTwain.Acquire(IntPtr.Zero));
			Assert.AreEqual((int)EZTwainErrorCode.EZTEC_USER_CANCEL,EZTwain.LastErrorCode());
			AssertAllClosed();
		}

		[TestMethod]
		public void Acquire_PaperJam_ReportsPaperJam() {
			_fake.ListPages[0].FailCondition=Tw.TWCC_PAPERJAM;
			EZTwain.OpenDefaultSource();
			Assert.AreEqual(IntPtr.Zero,EZTwain.Acquire(IntPtr.Zero));
			Assert.AreEqual((int)EZTwainErrorCode.EZTEC_DEVICE_PAPERJAM,EZTwain.LastErrorCode());
			AssertAllClosed();
		}

		[TestMethod]
		public void Acquire_SourceNeverResponds_TimesOutAndCleansUp() {
			_fake.EnableBehavior=FakeEnableBehavior.Nothing;
			EZTwain.Session.LoopTimeoutMilliseconds=300;
			EZTwain.OpenDefaultSource();
			Assert.AreEqual(IntPtr.Zero,EZTwain.Acquire(IntPtr.Zero));
			Assert.AreEqual((int)EZTwainErrorCode.EZTEC_INTERNAL,EZTwain.LastErrorCode());
			AssertAllClosed();
		}

		[TestMethod]
		public void Acquire_DisablesCallersWindowWhileScanning() {
			using(Form form=new Form()) {
				form.ShowInTaskbar=false;
				IntPtr hwnd=form.Handle;
				bool? wasEnabledDuringScan=null;
				_fake.ActionOnEnable=() => wasEnabledDuringScan=NativeMethods.IsWindowEnabled(hwnd);
				EZTwain.OpenDefaultSource();
				IntPtr hdib=EZTwain.Acquire(hwnd);
				Assert.AreNotEqual(IntPtr.Zero,hdib);
				Assert.AreEqual(false,wasEnabledDuringScan,"The caller's window is disabled while the source is enabled.");
				Assert.IsTrue(NativeMethods.IsWindowEnabled(hwnd),"The caller's window is enabled again afterwards.");
				EZTwain.DIB_Free(hdib);
			}
		}

		[TestMethod]
		public void Acquire_ErrorFromEarlierSettingDoesNotLeak() {
			_fake.HashSetRejectedCaps.Add(Tw.ICAP_PIXELTYPE);
			EZTwain.OpenDefaultSource();
			Assert.IsFalse(EZTwain.SetPixelType(1));
			Assert.AreEqual((int)EZTwainErrorCode.EZTEC_CAP_SET,EZTwain.LastErrorCode());
			IntPtr hdib=EZTwain.Acquire(IntPtr.Zero);
			Assert.AreNotEqual(IntPtr.Zero,hdib);
			Assert.AreEqual(0,EZTwain.LastErrorCode(),"A successful acquire reports no error.");
			EZTwain.DIB_Free(hdib);
		}

		[TestMethod]
		public void Acquire_Gray16NativeImage_ConvertedTo8Bit() {
			_fake.ListPages[0]=FakePage.CreateGray16(8,4,500,(x,y) => x*500);//max 3500, 12 bits
			EZTwain.OpenDefaultSource();
			IntPtr hdib=EZTwain.Acquire(IntPtr.Zero);
			Assert.AreEqual(8,EZTwain.DIB_Depth(hdib));
			using(Bitmap bitmap=(Bitmap)EZTwain.DIB_ToImage(hdib)) {
				Assert.AreEqual(3500>>4,bitmap.GetPixel(7,0).R);
				Assert.AreEqual(0,bitmap.GetPixel(0,3).R);
			}
			EZTwain.DIB_Free(hdib);
		}

		[TestMethod]
		public void Acquire_MemoryTransferVanillaBw_UsesInvertedPalette() {
			_fake.ListPages[0]=FakePage.CreateBw(16,2,200,(x,y) => x<4);
			_fake.PixelFlavor=Tw.TWPF_VANILLA;
			EZTwain.OpenDefaultSource();
			EZTwain.SetXferMech(EZTwain.XFERMECH_MEMORY);
			IntPtr hdib=EZTwain.Acquire(IntPtr.Zero);
			using(Bitmap bitmap=(Bitmap)EZTwain.DIB_ToImage(hdib)) {
				Assert.AreEqual(Color.Black.ToArgb(),bitmap.GetPixel(0,0).ToArgb());
				Assert.AreEqual(Color.White.ToArgb(),bitmap.GetPixel(10,1).ToArgb());
			}
			EZTwain.DIB_Free(hdib);
		}

		[TestMethod]
		public void Acquire_MemoryTransferUnknownLength_UsesRowsReceived() {
			_fake.IsImageLengthUnknown=true;
			EZTwain.OpenDefaultSource();
			EZTwain.SetXferMech(EZTwain.XFERMECH_MEMORY);
			IntPtr hdib=EZTwain.Acquire(IntPtr.Zero);
			Assert.AreEqual(0,EZTwain.LastErrorCode(),EZTwain.LastErrorText());
			using(Bitmap bitmap=(Bitmap)EZTwain.DIB_ToImage(hdib)) {
				AssertMatchesPage(bitmap,_fake.ListPages[0]);
			}
			EZTwain.DIB_Free(hdib);
		}

		[TestMethod]
		public void SetXferMech_RefusedBySource_FallsBackToNative() {
			_fake.HashSetRejectedCaps.Add(Tw.ICAP_XFERMECH);
			EZTwain.OpenDefaultSource();
			Assert.IsFalse(EZTwain.SetXferMech(EZTwain.XFERMECH_MEMORY));
			IntPtr hdib=EZTwain.Acquire(IntPtr.Zero);
			Assert.AreNotEqual(IntPtr.Zero,hdib);
			Assert.IsTrue(_fake.ListLog.Contains("NATIVEXFER"));
			EZTwain.DIB_Free(hdib);
		}

		[TestMethod]
		public void MultiTransfer_SensorSequence_ReturnsEveryImage() {
			//Same calls as ControlImages.AcquireMulti.
			_fake.ListPages.Add(FakePage.CreateRgb(20,10,300,ColorAt));
			_fake.ListPages.Add(FakePage.CreateRgb(30,10,300,ColorAt));
			Assert.IsTrue(EZTwain.OpenSource("Fake Sensor"));
			EZTwain.SetMultiTransfer(true);
			EZTwain.SetHideUI(true);
			int[] widthsExpected={ 40,20,30 };
			bool[] isDoneExpected={ false,false,true };
			for(int i=0;i<3;i++) {
				IntPtr hdib=EZTwain.Acquire(IntPtr.Zero);
				Assert.AreNotEqual(IntPtr.Zero,hdib,"image "+i+": "+EZTwain.LastErrorText());
				using(Bitmap bitmap=(Bitmap)EZTwain.DIB_ToImage(hdib)) {
					Assert.AreEqual(widthsExpected[i],bitmap.Width);
				}
				EZTwain.DIB_Free(hdib);
				Assert.AreEqual(isDoneExpected[i],EZTwain.IsDone(),"IsDone after image "+i);
			}
			Assert.AreEqual(1,_fake.CountEnable,"The whole batch comes from one session.");
			Assert.AreEqual(-1,_fake.DictCapsSet[Tw.CAP_XFERCOUNT],"Multi transfer accepts any number of images.");
			Assert.IsTrue(_fake.IsSourceOpen,"The source stays open until CloseSource.");
			Assert.IsFalse(_fake.IsEnabled,"The scanner dialog closes when the batch is done.");
			EZTwain.CloseSource();
			AssertAllClosed();
			Assert.IsFalse(EZTwain.Session.MultiTransfer,"CloseSource turns multi transfer off.");
		}

		[TestMethod]
		public void MultiTransfer_UnknownPendingCount_StillFinishes() {
			_fake.IsPendingCountUnknown=true;
			_fake.ListPages.Add(FakePage.CreateRgb(20,10,300,ColorAt));
			EZTwain.OpenSource("Fake Sensor");
			EZTwain.SetMultiTransfer(true);
			int count=0;
			while(true) {
				IntPtr hdib=EZTwain.Acquire(IntPtr.Zero);
				if(hdib==IntPtr.Zero) {
					break;
				}
				count++;
				EZTwain.DIB_Free(hdib);
				if(EZTwain.IsDone()) {
					break;
				}
			}
			Assert.AreEqual(2,count);
			EZTwain.CloseSource();
			AssertAllClosed();
		}

		[TestMethod]
		public void MultiTransfer_UserClosesDialog_ReturnsZeroAndKeepsSourceOpen() {
			_fake.EnableBehavior=FakeEnableBehavior.CloseRequest;
			EZTwain.OpenSource("Fake Sensor");
			EZTwain.SetMultiTransfer(true);
			Assert.AreEqual(IntPtr.Zero,EZTwain.Acquire(IntPtr.Zero));
			Assert.IsTrue(EZTwain.IsDone());
			Assert.IsTrue(_fake.IsSourceOpen);
			EZTwain.CloseSource();
			AssertAllClosed();
		}

		[TestMethod]
		public void AcquireMultipageFile_WritesEveryPageToPdf() {
			//Same calls as ControlImages.ToolBarScanMulti_Click.
			_fake.ListPages.Add(FakePage.CreateBw(80,100,300,(x,y) => (x+y)%5==0));
			_fake.ListPages.Add(FakePage.CreateRgb(50,60,150,ColorAt));
			string path=Path.Combine(_tempDir,"multi.pdf");
			EZTwain.SetHideUI(true);
			EZTwain.PDF_SetCompression(12345,70);
			Assert.IsTrue(EZTwain.OpenDefaultSource());
			Assert.IsTrue(EZTwain.EnableDuplex(true));
			Assert.AreEqual(1,_fake.DictCapsSet[Tw.CAP_DUPLEXENABLED]);
			EZTwain.SetPixelType(2);
			EZTwain.SetResolution(300);
			int pages=EZTwain.AcquireMultipageFile(IntPtr.Zero,path);
			Assert.AreEqual(0,EZTwain.LastErrorCode(),EZTwain.LastErrorText());
			Assert.AreEqual(3,pages);
			Assert.AreEqual(3,PdfPageWriterTests.GetPageCount(path));
			Assert.AreEqual(-1,_fake.DictCapsSet[Tw.CAP_XFERCOUNT]);
			AssertAllClosed();
		}

		[TestMethod]
		public void AcquireMultipageFile_PaperJam_KeepsEarlierPagesAndReportsJam() {
			_fake.ListPages.Add(FakePage.CreateRgb(10,10,300,ColorAt));
			_fake.ListPages.Add(FakePage.CreateRgb(10,10,300,ColorAt));
			_fake.ListPages[1].FailCondition=Tw.TWCC_PAPERJAM;
			string path=Path.Combine(_tempDir,"jam.pdf");
			EZTwain.OpenDefaultSource();
			int pages=EZTwain.AcquireMultipageFile(IntPtr.Zero,path);
			Assert.AreEqual((int)EZTwainErrorCode.EZTEC_DEVICE_PAPERJAM,EZTwain.LastErrorCode());
			Assert.AreEqual(1,pages);
			Assert.AreEqual(1,PdfPageWriterTests.GetPageCount(path));
			AssertAllClosed();
		}

		[TestMethod]
		public void AcquireMultipageFile_Cancelled_WritesNothing() {
			_fake.EnableBehavior=FakeEnableBehavior.CloseRequest;
			string path=Path.Combine(_tempDir,"cancel.pdf");
			EZTwain.OpenDefaultSource();
			Assert.AreEqual(0,EZTwain.AcquireMultipageFile(IntPtr.Zero,path));
			Assert.AreEqual((int)EZTwainErrorCode.EZTEC_USER_CANCEL,EZTwain.LastErrorCode());
			Assert.IsFalse(File.Exists(path));
			AssertAllClosed();
		}

		[TestMethod]
		public void AcquireMultipageFile_CancelMidBatch_WritesNothing() {
			_fake.ListPages.Add(FakePage.CreateRgb(10,10,300,ColorAt));
			_fake.ListPages[1].IsCancelled=true;
			string path=Path.Combine(_tempDir,"cancel2.pdf");
			EZTwain.OpenDefaultSource();
			Assert.AreEqual(0,EZTwain.AcquireMultipageFile(IntPtr.Zero,path));
			Assert.AreEqual((int)EZTwainErrorCode.EZTEC_USER_CANCEL,EZTwain.LastErrorCode());
			Assert.IsFalse(File.Exists(path));
			AssertAllClosed();
		}

		[TestMethod]
		public void AcquireMultipageFile_NotPdf_IsRejectedAndClosesSource() {
			EZTwain.OpenDefaultSource();
			Assert.AreEqual(0,EZTwain.AcquireMultipageFile(IntPtr.Zero,Path.Combine(_tempDir,"scan.tif")));
			Assert.AreEqual((int)EZTwainErrorCode.EZTEC_UNK_WRITE_FF,EZTwain.LastErrorCode());
			AssertAllClosed();
		}

		[TestMethod]
		public void OpenSource_UnknownName_ReportsNoSuchSource() {
			Assert.IsFalse(EZTwain.OpenSource("Not Installed"));
			Assert.AreEqual((int)EZTwainErrorCode.EZTEC_NO_SUCH_DS,EZTwain.LastErrorCode());
			StringAssert.Contains(EZTwain.LastErrorText(),"Not Installed");
			AssertAllClosed();
		}

		[TestMethod]
		public void OpenDefaultSource_NoDefault_ReportsError() {
			_fake.DefaultSource=null;
			Assert.IsFalse(EZTwain.OpenDefaultSource());
			Assert.AreEqual((int)EZTwainErrorCode.EZTEC_OPEN_DEFAULT_DS,EZTwain.LastErrorCode());
			AssertAllClosed();
		}

		[TestMethod]
		public void OpenSource_WhileAnotherIsOpen_SwitchesSource() {
			Assert.IsTrue(EZTwain.OpenSource("Fake Scanner"));
			Assert.IsTrue(EZTwain.OpenSource("fake sensor"));
			Assert.AreEqual("Fake Sensor",_fake.OpenSourceName);
			Assert.AreEqual(1,_fake.ListLog.Count(x => x=="CLOSEDS"));
			EZTwain.CloseSource();
			AssertAllClosed();
		}

		[TestMethod]
		public void Settings_WithoutOpenSource_ReportNoSourceOpen() {
			Assert.IsFalse(EZTwain.SetResolution(200));
			Assert.AreEqual((int)EZTwainErrorCode.EZTEC_NO_DS_OPEN,EZTwain.LastErrorCode());
			Assert.IsFalse(EZTwain.EnableDuplex(true));
			Assert.IsTrue(EZTwain.IsDone());
			Assert.IsTrue(EZTwain.CloseSource());
		}

		[TestMethod]
		public void SelectImageSource_ChoiceBecomesDefault() {
			Assert.IsTrue(EZTwain.SelectImageSource(IntPtr.Zero));
			Assert.AreEqual("Fake Sensor",_fake.DefaultSource);
			AssertAllClosed();
			Assert.IsTrue(EZTwain.OpenDefaultSource());
			Assert.AreEqual("Fake Sensor",_fake.OpenSourceName);
			EZTwain.CloseSource();
		}

		[TestMethod]
		public void SelectImageSource_Cancelled_ReturnsFalse() {
			_fake.UserSelectResult=Tw.TWRC_CANCEL;
			Assert.IsFalse(EZTwain.SelectImageSource(IntPtr.Zero));
			Assert.AreEqual((int)EZTwainErrorCode.EZTEC_USER_CANCEL,EZTwain.LastErrorCode());
			AssertAllClosed();
		}

		[TestMethod]
		public void DibFunctions_InvalidHandle_FailSafely() {
			Assert.AreEqual(IntPtr.Zero,EZTwain.DIB_ToDibSection(new IntPtr(12345)));
			Assert.AreEqual((int)EZTwainErrorCode.EZTEC_BAD_DIB,EZTwain.LastErrorCode());
			Assert.IsNull(EZTwain.DIB_ToImage(IntPtr.Zero));
			Assert.AreEqual(0,EZTwain.DIB_XResolution(IntPtr.Zero));
			EZTwain.DIB_Free(IntPtr.Zero);
			EZTwain.DIB_Free(new IntPtr(12345));
		}

		[TestMethod]
		public void DibXResolution_UnknownResolution_ReturnsDefault() {
			IntPtr hdib=DibHandleTable.Add(DibImage.Create(4,4,24,null,null,0,0));
			try {
				Assert.AreEqual(EZTwain.DefaultResolution,EZTwain.DIB_XResolution(hdib));
				Assert.AreEqual(EZTwain.DefaultResolution,EZTwain.DIB_YResolution(hdib));
			}
			finally {
				EZTwain.DIB_Free(hdib);
			}
		}
	}
}
