// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using ImagingDeviceManager.Imaging;
using ImagingDeviceManager.Journal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingDeviceManager.Tests {
	///<summary>Journal and sidecar files without a source (design 7.1 "Journal").</summary>
	[TestClass]
	public class CaptureFilesTests {
		private string _folder;

		[TestInitialize]
		public void Setup() {
			_folder=Path.Combine(Path.GetTempPath(),"ImagingDeviceManagerTests_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_folder);
		}

		[TestCleanup]
		public void Cleanup() {
			try {
				Directory.Delete(_folder,true);
			}
			catch {
			}
		}

		private static DibImage Gray8(int width,int height) {
			byte[] pixels=new byte[DibImage.GetStride(width,8)*height];
			for(int i=0;i<pixels.Length;i++) {
				pixels[i]=(byte)(i*7);
			}
			return DibImage.Create(width,height,8,DibImage.CreateGrayPalette(256,false),pixels,DibImage.DpiToPelsPerMeter(1270),
				DibImage.DpiToPelsPerMeter(1270));
		}

		private string WriteCopy(DateTime receivedAt) {
			string error;
			string path=CaptureFiles.WritePair(_folder,receivedAt,CaptureFiles.ToBmpBytes(Gray8(4,3)),baseName => "Kind=test\r\n",out error);
			Assert.AreEqual("",error);
			return path;
		}

		[TestMethod]
		public void Bmp_IsTheDibUnchanged_AndReadable() {
			DibImage dib=Gray8(7,5);
			byte[] bytes=CaptureFiles.ToBmpBytes(dib);
			Assert.AreEqual((byte)'B',bytes[0]);
			Assert.AreEqual((byte)'M',bytes[1]);
			Assert.AreEqual(bytes.Length,BitConverter.ToInt32(bytes,2));
			Assert.AreEqual(14+dib.PixelOffset,BitConverter.ToInt32(bytes,10));
			CollectionAssert.AreEqual(dib.Data,bytes.Skip(14).ToArray(),"Lossless: the DIB the caller gets, byte for byte.");
			string path=Path.Combine(_folder,"x.bmp");
			File.WriteAllBytes(path,bytes);
			using(Bitmap bitmap=new Bitmap(path)) {
				Assert.AreEqual(7,bitmap.Width);
				Assert.AreEqual(5,bitmap.Height);
				Assert.AreEqual(1270,bitmap.HorizontalResolution,1);
			}
		}

		[TestMethod]
		public void BaseNames_SortInArrivalOrder_EvenWithinAMillisecond() {
			DateTime receivedAt=new DateTime(2026,10,25,14,30,12,123);
			List<string> listNames=Enumerable.Range(0,20).Select(x => CaptureFiles.NewBaseName(receivedAt)).ToList();
			CollectionAssert.AreEqual(listNames,listNames.OrderBy(x => x,StringComparer.Ordinal).ToList());
			Assert.AreEqual(20,listNames.Distinct().Count());
			Assert.IsTrue(listNames.All(CaptureFiles.IsCaptureBaseName));
			StringAssert.StartsWith(listNames[0],"20261025-143012-123-");
			Assert.IsFalse(CaptureFiles.IsCaptureBaseName("notes"));
			Assert.IsFalse(CaptureFiles.IsCaptureBaseName("20261025-143012-123"));
		}

		[TestMethod]
		public void WritePair_ImageAndSidecarWithTheSameBaseName_NoTempLeft() {
			string pathImage=WriteCopy(new DateTime(2026,1,2,3,4,5));
			Assert.IsTrue(File.Exists(pathImage));
			Assert.AreEqual("Kind=test\r\n",File.ReadAllText(FacadeHarness.SidecarOf(pathImage)));
			Assert.AreEqual(0,Directory.GetFiles(_folder,"*.tmp").Length);
		}

		[TestMethod]
		public void WritePair_FolderMissing_NullWithError() {
			string error;
			Assert.IsNull(CaptureFiles.WritePair(Path.Combine(_folder,"missing"),DateTime.Now,new byte[] { 1 },baseName => "",out error));
			StringAssert.Contains(error,"could not be written");
		}

		[TestMethod]
		public void Rotate_KeepsTheNewest_RemovesOnlyJournalCopies() {
			List<string> listPaths=new List<string>();
			for(int i=0;i<5;i++) {
				listPaths.Add(WriteCopy(new DateTime(2026,10,25,9,0,i)));
			}
			File.WriteAllText(Path.Combine(_folder,"readme.txt"),"not a copy");
			File.WriteAllBytes(Path.Combine(_folder,"saved by hand.bmp"),new byte[] { 1 });
			string error;
			Assert.AreEqual(2,CaptureJournal.Rotate(_folder,3,out error));
			Assert.AreEqual("",error);
			for(int i=0;i<5;i++) {
				bool isKept=i>=2;
				Assert.AreEqual(isKept,File.Exists(listPaths[i]),"image "+i);
				Assert.AreEqual(isKept,File.Exists(FacadeHarness.SidecarOf(listPaths[i])),"sidecar "+i);
			}
			Assert.IsTrue(File.Exists(Path.Combine(_folder,"readme.txt")));
			Assert.IsTrue(File.Exists(Path.Combine(_folder,"saved by hand.bmp")));
			Assert.AreEqual(0,CaptureJournal.Rotate(_folder,3,out error),"Nothing more to remove.");
		}

		[TestMethod]
		public void Clean_CutsAtNulAndRemovesLineBreaks() {
			Assert.AreEqual("Sensor A",CaptureFiles.Clean("Sensor A\0garbage"));
			Assert.AreEqual("a  b",CaptureFiles.Clean(" a\r\nb "),"Each control character becomes a space.");
			Assert.AreEqual("",CaptureFiles.Clean(null));
		}

		[TestMethod]
		public void Journal_FolderUnusable_NoCopyLogged() {
			CaptureJournal journal=new CaptureJournal(CaptureFolders.CreateForFolder(null));
			List<string> listLog=new List<string>();
			Assert.IsNull(journal.Write(Gray8(2,2),CaptureRecord.Create(DateTime.Now,"S","id"),CaptureJournal.DeliveredCaller,null,200,listLog.Add));
			Assert.IsTrue(listLog.Any(x => x.Contains("not usable")),string.Join("\r\n",listLog));
		}

		[TestMethod]
		public void Journal_KeepZero_IsOff() {
			CaptureJournal journal=new CaptureJournal(CaptureFolders.CreateForFolder(_folder));
			List<string> listLog=new List<string>();
			Assert.IsNull(journal.Write(Gray8(2,2),CaptureRecord.Create(DateTime.Now,"S","id"),CaptureJournal.DeliveredCaller,null,0,listLog.Add));
			Assert.IsFalse(Directory.Exists(Path.Combine(_folder,CaptureFolders.SubfolderJournal)));
			Assert.IsTrue(listLog.Any(x => x.Contains("JournalKeep is 0")));
		}
	}

	///<summary>The journal around facade acquisitions: a copy of every image a by-name acquisition transferred, written before the image
	///goes back to the caller, with its sidecar; rotation keeps [Twain] JournalKeep copies.</summary>
	[TestClass]
	public class JournalTests {
		private FacadeHarness _harness;

		[TestInitialize]
		public void Setup() {
			_harness=new FacadeHarness();
		}

		[TestCleanup]
		public void Cleanup() {
			_harness.Dispose();
		}

		private IntPtr AcquireByName(long patNum=0) {
			_harness.Helper.ListExposures.Clear();
			_harness.Helper.ListExposures.Add(SessionHarness.Exposure(8));
			Assert.IsTrue(EZTwain.OpenSource(_harness.Helper.ProductName),EZTwain.LastErrorText());
			IntPtr hdib=patNum==0 ? EZTwain.Acquire(IntPtr.Zero) : EZTwain.Acquire(IntPtr.Zero,patNum);
			Assert.AreNotEqual(IntPtr.Zero,hdib,EZTwain.LastErrorText());
			return hdib;
		}

		[TestMethod]
		public void ByName_CopyWrittenBeforeReturn_WithSidecar() {
			_harness.WriteConfig("[Twain]","RestrictSources=1","AllowedSources=Fake Scanner|"+_harness.Helper.ProductName,"JournalKeep=50");
			IntPtr hdib=AcquireByName();
			List<string> listImages=FacadeHarness.ListImages(_harness.FolderJournal);
			Assert.AreEqual(1,listImages.Count,"Written by the time the caller has the image.");
			DibImage dib=DibHandleTable.Get(hdib);
			CollectionAssert.AreEqual(CaptureFiles.ToBmpBytes(dib),File.ReadAllBytes(listImages[0]));
			EZTwain.DIB_Free(hdib);
			Dictionary<string,string> dictFields=CaptureFiles.ReadSidecar(FacadeHarness.SidecarOf(listImages[0]));
			Assert.AreEqual("journal copy",dictFields["Kind"]);
			StringAssert.StartsWith(dictFields["ReceivedAt"],"2026-10-25T09:00:");
			Assert.AreEqual(_harness.Helper.ProductName,dictFields["Source"]);
			Assert.AreEqual(Environment.MachineName,dictFields["Computer"]);
			StringAssert.EndsWith(dictFields["WindowsUser"],Environment.UserName);
			Assert.AreEqual("",dictFields["PatNum"],"The Mount/Acquire loop passes no patient.");
			Assert.AreEqual(CaptureJournal.DeliveredCaller,dictFields["Delivered"]);
			Assert.AreEqual(Path.GetFileName(listImages[0]),dictFields["ImageFile"]);
			Assert.AreEqual("8",dictFields["Width"]);
			Assert.AreEqual("1270",dictFields["XDpi"]);
			Assert.AreEqual(CaptureFiles.Sha256Hex(File.ReadAllBytes(listImages[0])),dictFields["Sha256"]);
			Assert.AreEqual("1",dictFields["Twain.RestrictSources"],"The gated values in effect.");
			Assert.AreEqual("Fake Scanner|"+_harness.Helper.ProductName,dictFields["Twain.AllowedSources"]);
			Assert.AreEqual("50",dictFields["Twain.JournalKeep"]);
			Assert.AreEqual("1",dictFields["ThreadModalInEffect"]);
			Assert.AreEqual("0",dictFields["SafeDisableInEffect"]);
			Assert.AreEqual(_harness.PathConfig,dictFields["ConfigFile"]);
			Assert.IsFalse(dictFields.ContainsKey("CaptureId"),"Only when a metadata interface supplies it.");
			StringAssert.Contains(File.ReadAllText(_harness.PathCaptureLog),"Journal copy "+Path.GetFileName(listImages[0]));
		}

		[TestMethod]
		public void ByName_PatNumPassed_InTheSidecar() {
			EZTwain.DIB_Free(AcquireByName(4321));
			Dictionary<string,string> dictFields=CaptureFiles.ReadSidecar(FacadeHarness.SidecarOf(FacadeHarness.ListImages(_harness.FolderJournal)[0]));
			Assert.AreEqual("4321",dictFields["PatNum"]);
		}

		[TestMethod]
		public void ByName_SafeDisable_RecordedAsInEffect() {
			_harness.WriteSafeDisableConfig();
			EZTwain.DIB_Free(AcquireByName());
			Dictionary<string,string> dictFields=CaptureFiles.ReadSidecar(FacadeHarness.SidecarOf(FacadeHarness.ListImages(_harness.FolderJournal)[0]));
			Assert.AreEqual("1",dictFields["SafeDisableInEffect"]);
			Assert.AreEqual("0x8002",dictFields["Twain.SafeDisableCap"]);
			Assert.AreEqual("0x8006|0x8007",dictFields["Twain.RefusedDisableCodes"]);
		}

		[TestMethod]
		public void Rotation_KeepsJournalKeepCopies() {
			_harness.WriteConfig("[Twain]","JournalKeep=3");
			for(int i=0;i<5;i++) {
				EZTwain.DIB_Free(AcquireByName());
			}
			List<string> listImages=FacadeHarness.ListImages(_harness.FolderJournal);
			Assert.AreEqual(3,listImages.Count);
			Assert.AreEqual(3,Directory.GetFiles(_harness.FolderJournal,"*"+CaptureFiles.SidecarExtension).Length);
			List<string> listTimes=listImages.Select(x => CaptureFiles.ReadSidecar(FacadeHarness.SidecarOf(x))["ReceivedAt"]).ToList();
			CollectionAssert.AreEqual(listTimes.OrderBy(x => x).ToList(),listTimes);
			Assert.IsFalse(listTimes.Any(x => x.StartsWith("2026-10-25T09:00:00")),"The oldest went first.");
		}

		[TestMethod]
		public void JournalKeepZero_NoCopy() {
			_harness.WriteConfig("[Twain]","JournalKeep=0");
			EZTwain.DIB_Free(AcquireByName());
			Assert.AreEqual(0,FacadeHarness.ListImages(_harness.FolderJournal).Count);
		}

		[TestMethod]
		public void Scans_NoCopy() {
			_harness.Fake.ListPages.Add(FakePage.CreateGray8(8,4,300,(x,y) => (byte)x));
			Assert.IsTrue(EZTwain.OpenDefaultSource());
			EZTwain.DIB_Free(EZTwain.Acquire(IntPtr.Zero));
			Assert.AreEqual(0,FacadeHarness.ListImages(_harness.FolderJournal).Count);
		}

		[TestMethod]
		public void NoImage_NoCopy() {
			Assert.IsTrue(EZTwain.OpenSource(_harness.Helper.ProductName));
			Assert.AreEqual(IntPtr.Zero,EZTwain.Acquire(IntPtr.Zero));//The operator closed the sensor software.
			Assert.AreEqual(0,FacadeHarness.ListImages(_harness.FolderJournal).Count);
		}
	}
}
