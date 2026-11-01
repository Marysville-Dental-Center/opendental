// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using ImagingDeviceManager.Capture;
using ImagingDeviceManager.Imaging;
using ImagingDeviceManager.Journal;
using ImagingDeviceManager.TwainProtocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingDeviceManager.Tests {
	///<summary>CaptureFileStore (design 7.1, 7.12): the quarantine files and journal copies with their sidecars, read back as images, and filed
	///or discarded without losing the file or its audit trail. Temp folders only.</summary>
	[TestClass]
	public class CaptureFileStoreTests {
		private static readonly DateTime _now=new DateTime(2026,10,31,12,0,0);
		private string _folder;
		private CaptureFolders _folders;
		private CaptureFileStore _store;

		[TestInitialize]
		public void Setup() {
			_folder=Path.Combine(Path.GetTempPath(),"CaptureFileStoreTests_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_folder);
			_folders=CaptureFolders.CreateForFolder(_folder);
			_store=CaptureFileStore.CreateForFolder(_folder,() => _now);
		}

		[TestCleanup]
		public void Cleanup() {
			try {
				Directory.Delete(_folder,true);
			}
			catch {
			}
		}

		private static AcquiredImage Gray16Image(int width,int height,int flavor=AcquiredImage.PixelFlavorChocolate) {
			ushort[] samples=new ushort[width*height];
			for(int i=0;i<samples.Length;i++) {
				samples[i]=(ushort)(i*4099+0x0102);
			}
			return new AcquiredImage { Width=width,Height=height,BitsPerPixel=16,BitsPerSample=16,Gray16=samples,XDpi=1270,YDpi=1270,PixelFlavor=flavor };
		}

		private static AcquiredImage Gray8Image(int width,int height) {
			FakePage page=FakePage.CreateGray8(width,height,1270,(x,y) => (byte)(x*20+y));
			return new AcquiredImage { Width=width,Height=height,BitsPerPixel=8,Dib=page.NativeDib,XDpi=1270,YDpi=1270 };
		}

		///<summary>A session record as TwainCaptureSession.BuildRecord writes it, with the session's sidecar fields.</summary>
		private static CaptureRecord SessionRecord(DateTime receivedAt,Guid sessionGuid,bool is16) {
			CaptureRecord record=CaptureRecord.Create(receivedAt,"Example Sensor DS","s-1");
			record.PatNum=42;
			record.CaptureId="cap-7";
			record.Sequence=3;
			record.ListCaptureFields.Add(new SidecarField("SessionGuid",sessionGuid.ToString("D")));
			record.ListCaptureFields.Add(new SidecarField("Target","Slot 3 (teeth 2,3)"));
			record.ListCaptureFields.Add(new SidecarField("ImageKind","Normal"));
			record.ListCaptureFields.Add(new SidecarField("Redelivered","0"));
			record.ListCaptureFields.Add(new SidecarField("RequestedBits",is16 ? "16" : "8"));
			record.ListCaptureFields.Add(new SidecarField("TransferMechanism",is16 ? "Memory" : "Native"));
			record.ListCaptureFields.Add(new SidecarField("PixelFlavor","0"));
			record.ListCaptureFields.Add(new SidecarField(CaptureSidecarKeys.PixelType,"1"));
			record.ListCaptureFields.Add(new SidecarField(CaptureSidecarKeys.BitDepth16InEffect,is16 ? "1" : "0"));
			record.ListCaptureFields.Add(new SidecarField(CaptureSidecarKeys.MountNum,"7"));
			record.ListCaptureFields.Add(new SidecarField(CaptureSidecarKeys.MountItemNum,"103"));
			record.ListCaptureFields.Add(new SidecarField(CaptureSidecarKeys.SlotItemOrder,"3"));
			record.ListCaptureFields.Add(new SidecarField(CaptureSidecarKeys.ToothNumbers,"2,3"));
			record.ListCaptureFields.Add(new SidecarField(CaptureSidecarKeys.DocCategory,"20"));
			record.ListCaptureFields.Add(new SidecarField(CaptureSidecarKeys.ProvNum,"4"));
			record.ListCaptureFields.Add(new SidecarField(CaptureSidecarKeys.ImagingDeviceNum,"12"));
			record.ListCaptureFields.Add(new SidecarField("Metadata","{\"schema\":\"capture-metadata/1\",\"captureId\":\"cap-7\"}"));
			return record;
		}

		private string WriteSessionQuarantine(AcquiredImage image,Guid sessionGuid,long patNum,DateTime receivedAt) {
			string error;
			string path=new QuarantineWriter(_folders).WriteSessionCapture(CaptureImageFile.FromAcquiredImage(image),
				SessionRecord(receivedAt,sessionGuid,image.Gray16!=null),sessionGuid,patNum,"Doe, Jane","Slot 3 (teeth 2,3)",
				"It arrived after a patient change.",null,out error);
			Assert.IsNotNull(path,error);
			return path;
		}

		#region Gray16Tiff
		[TestMethod]
		public void Gray16Tiff_DecodeGivesBackTheSamples() {
			AcquiredImage image=Gray16Image(7,5);
			int width;
			int height;
			ushort[] samples;
			double xDpi;
			double yDpi;
			bool isWhiteIsZero;
			string error;
			Assert.IsTrue(Gray16Tiff.TryDecode(Gray16Tiff.Encode(7,5,image.Gray16,1270,600,false),out width,out height,out samples,out xDpi,out yDpi,
				out isWhiteIsZero,out error),error);
			Assert.AreEqual(7,width);
			Assert.AreEqual(5,height);
			CollectionAssert.AreEqual(image.Gray16,samples);
			Assert.AreEqual(1270,xDpi,0.01);
			Assert.AreEqual(600,yDpi,0.01);
			Assert.IsFalse(isWhiteIsZero);
			Assert.IsTrue(Gray16Tiff.TryDecode(Gray16Tiff.Encode(1,1,new ushort[] { 9 },0,0,true),out width,out height,out samples,out xDpi,out yDpi,
				out isWhiteIsZero,out error),error);
			Assert.IsTrue(isWhiteIsZero);
			Assert.AreEqual(0,xDpi,"0/1 is unknown.");
		}

		[TestMethod]
		public void Gray16Tiff_OtherFiles_Refused() {
			int width;
			int height;
			ushort[] samples;
			double xDpi;
			double yDpi;
			bool isWhiteIsZero;
			string error;
			Assert.IsFalse(Gray16Tiff.TryDecode(new byte[] { (byte)'B',(byte)'M',0,0,0,0,0,0,0,0 },out width,out height,out samples,out xDpi,out yDpi,
				out isWhiteIsZero,out error));
			byte[] bytes=Gray16Tiff.Encode(2,2,new ushort[] { 1,2,3,4 },0,0,false);
			byte[] bytesTruncated=bytes.Take(bytes.Length-2).ToArray();
			Assert.IsFalse(Gray16Tiff.TryDecode(bytesTruncated,out width,out height,out samples,out xDpi,out yDpi,out isWhiteIsZero,out error));
			StringAssert.Contains(error,"strip");
			string path=Path.Combine(_folder,"rgb.tif");
			using(Bitmap bitmap=new Bitmap(2,2,PixelFormat.Format24bppRgb)) {
				bitmap.Save(path,ImageFormat.Tiff);
			}
			Assert.IsFalse(Gray16Tiff.TryDecode(File.ReadAllBytes(path),out width,out height,out samples,out xDpi,out yDpi,out isWhiteIsZero,out error),
				"An 8-bit colour TIFF is not a 16-bit copy.");
		}
		#endregion Gray16Tiff

		#region List
		[TestMethod]
		public void ListQuarantine_SessionFile_EverySidecarValue() {
			Guid sessionGuid=Guid.NewGuid();
			string path=WriteSessionQuarantine(Gray8Image(4,3),sessionGuid,42,new DateTime(2026,10,31,10,0,5,123));
			CaptureFileEntry entry=_store.ListQuarantine().Single();
			Assert.AreEqual(CaptureFileKind.Quarantine,entry.Kind);
			Assert.AreEqual(path,entry.ImagePath);
			Assert.AreEqual(Path.ChangeExtension(path,".txt"),entry.SidecarPath);
			Assert.AreEqual(sessionGuid,entry.SessionGuid);
			Assert.AreEqual(42,entry.PatNum);
			Assert.IsFalse(entry.IsPatientUnknown);
			Assert.AreEqual("Doe, Jane",entry.PatientName);
			Assert.AreEqual("Slot 3 (teeth 2,3)",entry.Slot);
			Assert.AreEqual(7,entry.MountNum);
			Assert.AreEqual(103,entry.MountItemNum);
			Assert.AreEqual(3,entry.SlotItemOrder);
			Assert.AreEqual("2,3",entry.ToothNumbers);
			Assert.AreEqual(20,entry.DocCategory);
			Assert.AreEqual(4,entry.ProvNum);
			Assert.AreEqual(12,entry.ImagingDeviceNum);
			Assert.AreEqual(new DateTime(2026,10,31,10,0,5,123),entry.ReceivedAt);
			Assert.AreEqual("Example Sensor DS",entry.Source);
			Assert.AreEqual(Environment.MachineName,entry.Computer);
			StringAssert.EndsWith(entry.WindowsUser,Environment.UserName);
			Assert.AreEqual("cap-7",entry.CaptureId);
			Assert.AreEqual(3,entry.Sequence);
			StringAssert.Contains(entry.MetadataJson,"capture-metadata/1");
			Assert.AreEqual("It arrived after a patient change.",entry.Reason);
			Assert.AreEqual(4,entry.Width);
			Assert.AreEqual(3,entry.Height);
			Assert.IsFalse(entry.IsGray16);
			Assert.IsFalse(entry.IsResolved);
		}

		[TestMethod]
		public void ListQuarantine_LibraryFile_PatientUnknown() {
			string error;
			string path=new QuarantineWriter(_folders).Write(DibImage.FromPackedDib(Gray8Image(2,2).Dib),
				CaptureRecord.Create(new DateTime(2026,10,1,9,0,0),"Scanner","a-1"),TwainDrainReason.Stop,TwainStopReason.ParentDestroyed,null,out error);
			Assert.IsNotNull(path,error);
			CaptureFileEntry entry=_store.ListQuarantine().Single();
			Assert.IsTrue(entry.IsPatientUnknown);
			Assert.AreEqual(QuarantineWriter.PatientUnknown,entry.PatientName);
			Assert.IsNull(entry.SessionGuid);
			Assert.AreEqual(0,entry.MountNum);
			Assert.IsTrue(entry.IsOverdue(new DateTime(2026,11,5)),"More than 30 days.");
			Assert.IsFalse(entry.IsOverdue(new DateTime(2026,10,20)));
		}

		[TestMethod]
		public void ListQuarantine_NewestFirst_NoFolderNothing_TempFilesSkipped() {
			Assert.AreEqual(0,_store.ListQuarantine().Count);
			Assert.IsFalse(Directory.Exists(Path.Combine(_folder,@"Capture\quarantine")),"Listing creates nothing.");
			Guid sessionGuid=Guid.NewGuid();
			string pathOld=WriteSessionQuarantine(Gray8Image(2,2),sessionGuid,42,new DateTime(2026,10,30,9,0,0));
			string pathNew=WriteSessionQuarantine(Gray8Image(2,2),Guid.NewGuid(),0,new DateTime(2026,10,31,9,0,0));
			File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(pathOld),"half.tmp"),new byte[] { 1 });
			List<CaptureFileEntry> listEntries=_store.ListQuarantine();
			CollectionAssert.AreEqual(new[] { pathNew,pathOld },listEntries.Select(x => x.ImagePath).ToArray());
			Assert.IsTrue(listEntries[0].IsPatientUnknown,"PatNum 0 is patient unknown.");
		}

		[TestMethod]
		public void ListJournal_CopiesWithTheirPatNum() {
			CaptureJournal journal=new CaptureJournal(_folders);
			journal.Write(CaptureImageFile.FromAcquiredImage(Gray8Image(2,2)),SessionRecord(new DateTime(2026,10,31,10,0,0),Guid.NewGuid(),false),
				CaptureJournal.DeliveredCaller,null,200,null);
			CaptureRecord recordPhase0=CaptureRecord.Create(new DateTime(2026,10,31,10,1,0),"Src","a-2");
			journal.Write(CaptureImageFile.FromAcquiredImage(Gray8Image(2,2)),recordPhase0,CaptureJournal.DeliveredCaller,null,200,null);
			List<CaptureFileEntry> listEntries=_store.ListJournal();
			Assert.AreEqual(2,listEntries.Count);
			Assert.IsTrue(listEntries.All(x => x.Kind==CaptureFileKind.Journal));
			Assert.AreEqual(0,listEntries[0].PatNum,"The phase-0 copy records no PatNum.");
			Assert.AreEqual(42,listEntries[1].PatNum);
			Assert.AreEqual(CaptureJournal.DeliveredCaller,listEntries[1].Delivered);
			Assert.AreEqual("Slot 3 (teeth 2,3)",listEntries[1].Slot,"The journal's Target.");
			Assert.AreEqual(0,_store.ListQuarantine().Count);
		}
		#endregion List

		#region Read
		[TestMethod]
		public void ReadImage_Bmp_TheDibUnchanged_WithTheSidecarsValues() {
			AcquiredImage image=Gray8Image(5,4);
			WriteSessionQuarantine(image,Guid.NewGuid(),42,new DateTime(2026,10,31,10,0,5));
			CaptureFileEntry entry=_store.ListQuarantine().Single();
			string error;
			AcquiredImage imageRead=_store.ReadImage(entry,out error);
			Assert.IsNotNull(imageRead,error);
			CollectionAssert.AreEqual(image.Dib,imageRead.Dib);
			Assert.IsNull(imageRead.Gray16);
			Assert.AreEqual(5,imageRead.Width);
			Assert.AreEqual(4,imageRead.Height);
			Assert.AreEqual(8,imageRead.BitsPerPixel);
			Assert.AreEqual(AcquiredImage.PixelTypeGray,imageRead.PixelType);
			Assert.AreEqual(new DateTime(2026,10,31,10,0,5),imageRead.ReceivedAt);
			Assert.AreEqual("Example Sensor DS",imageRead.SourceName);
			StringAssert.Contains(imageRead.MetadataJson,"cap-7");
			Assert.AreEqual(TwainTransferMechanism.Native,imageRead.TransferMechanism);
			Assert.AreEqual(entry.ImagePath,imageRead.QuarantinePath);
			Assert.AreEqual("Slot 3 (teeth 2,3)",imageRead.TargetLabel);
		}

		[TestMethod]
		public void ReadImage_Tif_TheSamplesUnchanged_16BitInEffect() {
			AcquiredImage image=Gray16Image(6,4);
			WriteSessionQuarantine(image,Guid.NewGuid(),42,new DateTime(2026,10,31,10,0,5));
			CaptureFileEntry entry=_store.ListQuarantine().Single();
			Assert.IsTrue(entry.IsGray16);
			string error;
			AcquiredImage imageRead=_store.ReadImage(entry,out error);
			Assert.IsNotNull(imageRead,error);
			CollectionAssert.AreEqual(image.Gray16,imageRead.Gray16);
			Assert.IsNull(imageRead.Dib);
			Assert.AreEqual(16,imageRead.BitsPerSample);
			Assert.AreEqual(1270,imageRead.XDpi,0.01);
			Assert.AreEqual(TwainTransferMechanism.Memory,imageRead.TransferMechanism);
			Assert.IsTrue(imageRead.IsBitDepth16InEffect);
			Assert.AreEqual(16,imageRead.RequestedBits);
			Assert.AreEqual(AcquiredImage.PixelFlavorChocolate,imageRead.PixelFlavor);
		}

		[TestMethod]
		public void ReadImage_Png_A24BitDib() {
			string folder=Path.Combine(_folder,@"Capture\quarantine\x");
			Directory.CreateDirectory(folder);
			string path=Path.Combine(folder,"20261031-100000-000-000001-abcd.png");
			using(Bitmap bitmap=new Bitmap(3,2,PixelFormat.Format24bppRgb)) {
				bitmap.SetPixel(0,0,Color.FromArgb(10,20,30));
				bitmap.SetPixel(2,1,Color.FromArgb(200,100,50));
				bitmap.Save(path,ImageFormat.Png);
			}
			CaptureFileEntry entry=_store.ListQuarantine().Single();
			Assert.IsFalse(entry.HasSidecar);
			Assert.IsTrue(entry.IsPatientUnknown,"No sidecar: patient unknown.");
			string error;
			AcquiredImage imageRead=_store.ReadImage(entry,out error);
			Assert.IsNotNull(imageRead,error);
			DibImage dib=DibImage.FromPackedDib(imageRead.Dib);
			Assert.AreEqual(24,dib.BitCount);
			Assert.AreEqual(3,dib.Width);
			Assert.AreEqual(2,dib.Height);
			Assert.AreEqual(-1,imageRead.PixelType,"Not recorded: the saver decides from the pixels.");
			//Bottom-up: the top-left pixel is in the last row of the DIB, as B,G,R.
			int offsetTopLeft=dib.PixelOffset+dib.Stride;
			Assert.AreEqual(30,imageRead.Dib[offsetTopLeft]);
			Assert.AreEqual(20,imageRead.Dib[offsetTopLeft+1]);
			Assert.AreEqual(10,imageRead.Dib[offsetTopLeft+2]);
		}

		[TestMethod]
		public void ReadImage_MissingOrUnreadable_Error() {
			string path=WriteSessionQuarantine(Gray8Image(2,2),Guid.NewGuid(),42,new DateTime(2026,10,31,10,0,5));
			CaptureFileEntry entry=_store.ListQuarantine().Single();
			File.WriteAllBytes(path,new byte[] { 1,2,3 });
			string error;
			Assert.IsNull(_store.ReadImage(entry,out error));
			StringAssert.Contains(error,"not a bitmap");
			File.Delete(path);
			Assert.IsNull(_store.ReadImage(entry,out error));
			Assert.AreNotEqual("",error);
		}
		#endregion Read

		#region Resolve
		[TestMethod]
		public void Resolve_Quarantine_MovedWithItsSidecarAndTheDisposition_NoLongerCounted() {
			Guid sessionGuid=Guid.NewGuid();
			string path=WriteSessionQuarantine(Gray8Image(2,2),sessionGuid,42,new DateTime(2026,10,31,10,0,5));
			WriteSessionQuarantine(Gray8Image(2,2),sessionGuid,42,new DateTime(2026,10,31,10,0,6));
			Assert.AreEqual(2,CaptureQuarantine.GetUnassignedInfo(_folders,_now).Count);
			CaptureFileEntry entry=_store.ListQuarantine().Single(x => x.ImagePath==path);
			string pathResolved;
			string error;
			Assert.IsTrue(_store.Resolve(entry,CaptureFileDisposition.Assigned,new List<KeyValuePair<string,string>> {
				new KeyValuePair<string,string>("DispositionDocNum","1001"),
				new KeyValuePair<string,string>("DispositionUser","Admin"),
			},out pathResolved,out error),error);
			Assert.AreEqual("",error);
			Assert.IsFalse(File.Exists(path));
			Assert.IsFalse(File.Exists(Path.ChangeExtension(path,".txt")));
			Assert.AreEqual(Path.Combine(_folder,@"Capture\quarantine-resolved",sessionGuid.ToString("D"),Path.GetFileName(path)),pathResolved);
			Assert.IsTrue(File.Exists(pathResolved),"The image is kept.");
			Dictionary<string,string> dictSidecar=CaptureFiles.ReadSidecar(Path.ChangeExtension(pathResolved,".txt"));
			Assert.AreEqual("42",dictSidecar["PatNum"],"The original sidecar is kept.");
			Assert.AreEqual("Assigned",dictSidecar[CaptureSidecarKeys.Disposition]);
			Assert.AreEqual("2026-10-31T12:00:00.000"+_now.ToString("zzz"),dictSidecar[CaptureSidecarKeys.DispositionAt]);
			Assert.AreEqual(Environment.MachineName,dictSidecar[CaptureSidecarKeys.DispositionComputer]);
			Assert.AreEqual("1001",dictSidecar["DispositionDocNum"]);
			Assert.AreEqual("Admin",dictSidecar["DispositionUser"]);
			CaptureUnassignedInfo info=CaptureQuarantine.GetUnassignedInfo(_folders,_now);
			Assert.AreEqual(1,info.Count,"The resolved folder is outside the quarantine.");
			Assert.AreEqual(1,_store.ListQuarantine().Count);
			Assert.IsFalse(_store.Resolve(entry,CaptureFileDisposition.Discarded,null,out pathResolved,out error),"Already moved.");
			StringAssert.Contains(error,"no longer there");
		}

		[TestMethod]
		public void Resolve_Quarantine_TheFileCannotBeMoved_ItsSidecarRecordsIt_NeitherListedNorCountedAgain() {
			Guid sessionGuid=Guid.NewGuid();
			string path=WriteSessionQuarantine(Gray8Image(2,2),sessionGuid,42,new DateTime(2026,10,31,10,0,5));
			CaptureFileEntry entry=_store.ListQuarantine().Single();
			string pathResolved;
			string error;
			bool isResolved;
			using(new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)) {//another program (an antivirus scanner) holds the file
				isResolved=_store.Resolve(entry,CaptureFileDisposition.Assigned,null,out pathResolved,out error);
			}
			Assert.IsTrue(isResolved,error);
			StringAssert.Contains(error,"could not be moved out of the capture quarantine");
			Assert.AreEqual(path,pathResolved,"Still where it was.");
			Assert.IsTrue(File.Exists(path));
			Assert.AreEqual("Assigned",CaptureFiles.ReadSidecar(Path.ChangeExtension(path,".txt"))[CaptureSidecarKeys.Disposition]);
			Assert.AreEqual(0,_store.ListQuarantine().Count,"Not offered for Assign again (a second document of one exposure).");
			Assert.AreEqual(0,CaptureQuarantine.GetUnassignedInfo(_folders,_now).Count,"No longer an unassigned capture.");
		}

		[TestMethod]
		public void Resolve_Quarantine_FiledByAnotherUserMeanwhile_Refused_NoSidecarLeftBehind() {
			Guid sessionGuid=Guid.NewGuid();
			string path=WriteSessionQuarantine(Gray8Image(2,2),sessionGuid,42,new DateTime(2026,10,31,10,0,5));
			CaptureFileEntry entry=_store.ListQuarantine().Single();
			string folderSession=Path.GetDirectoryName(path);
			//Another Open Dental instance on this terminal server moves the file and its sidecar out between this one's look and its move.
			_store.ActionBeforeQuarantineMove=() => {
				File.Delete(path);
				File.Delete(Path.ChangeExtension(path,".txt"));
			};
			string pathResolved;
			string error;
			Assert.IsFalse(_store.Resolve(entry,CaptureFileDisposition.Discarded,null,out pathResolved,out error));
			Assert.AreEqual("The file "+entry.FileName+" is no longer there (another user may have filed it).",error);
			Assert.AreEqual(0,Directory.GetFiles(folderSession,"*.txt").Length,"No sidecar without its image in the quarantine.");
		}

		[TestMethod]
		public void Resolve_QuarantineRootFile_Discarded_IntoTheResolvedRoot() {
			string error;
			string path=new QuarantineWriter(_folders).Write(DibImage.FromPackedDib(Gray8Image(2,2).Dib),
				CaptureRecord.Create(new DateTime(2026,10,1,9,0,0),"Scanner","a-1"),TwainDrainReason.ExtraImage,TwainStopReason.None,null,out error);
			CaptureFileEntry entry=_store.ListQuarantine().Single();
			string pathResolved;
			Assert.IsTrue(_store.Resolve(entry,CaptureFileDisposition.Discarded,null,out pathResolved,out error),error);
			Assert.AreEqual(Path.Combine(_folder,@"Capture\quarantine-resolved",Path.GetFileName(path)),pathResolved);
			Assert.AreEqual("Discarded",CaptureFiles.ReadSidecar(Path.ChangeExtension(pathResolved,".txt"))[CaptureSidecarKeys.Disposition]);
			Assert.IsNull(CaptureQuarantine.GetUnassignedInfo(_folders,_now).Text);
		}

		[TestMethod]
		public void Resolve_Journal_SidecarMarked_InPlace_NotOfferedAgain() {
			CaptureJournal journal=new CaptureJournal(_folders);
			string path=journal.Write(CaptureImageFile.FromAcquiredImage(Gray8Image(2,2)),SessionRecord(new DateTime(2026,10,31,10,0,0),Guid.NewGuid(),false),
				CaptureJournal.DeliveredCaller,null,200,null);
			CaptureFileEntry entry=_store.ListJournal().Single();
			string pathResolved;
			string error;
			Assert.IsTrue(_store.Resolve(entry,CaptureFileDisposition.Assigned,null,out pathResolved,out error),error);
			Assert.AreEqual(path,pathResolved,"A journal copy stays where rotation expects it.");
			CaptureFileEntry entryAfter=_store.ListJournal().Single();
			Assert.IsTrue(entryAfter.IsResolved);
			Assert.AreEqual(42,entryAfter.PatNum,"The original fields are kept.");
		}

		[TestMethod]
		public void Resolve_JournalOnlyCopy_GetsItsPlainName() {
			CaptureJournal journal=new CaptureJournal(_folders);
			string path=journal.Write(CaptureImageFile.FromAcquiredImage(Gray8Image(2,2)),SessionRecord(new DateTime(2026,10,31,10,0,0),Guid.NewGuid(),false),
				CaptureJournal.DeliveredNone,"The quarantine could not be written.",200,null);
			StringAssert.EndsWith(Path.GetFileNameWithoutExtension(path),CaptureJournal.OnlyCopySuffix);
			CaptureFileEntry entry=_store.ListJournal().Single();
			string pathResolved;
			string error;
			Assert.IsTrue(_store.Resolve(entry,CaptureFileDisposition.Reassigned,null,out pathResolved,out error),error);
			Assert.AreEqual("",error);
			Assert.IsFalse(File.Exists(path));
			Assert.IsTrue(CaptureFiles.IsCaptureBaseName(Path.GetFileNameWithoutExtension(pathResolved)),"Rotation counts it again.");
			Assert.AreEqual("Reassigned",CaptureFiles.ReadSidecar(Path.ChangeExtension(pathResolved,".txt"))[CaptureSidecarKeys.Disposition]);
			Assert.AreEqual(1,Directory.GetFiles(Path.GetDirectoryName(path),"*.txt").Length);
		}

		[TestMethod]
		public void Resolve_JournalOnlyCopy_TheImageCannotBeRenamed_KeepsItsNameAndItsMarkedSidecar() {
			CaptureJournal journal=new CaptureJournal(_folders);
			string path=journal.Write(CaptureImageFile.FromAcquiredImage(Gray8Image(2,2)),SessionRecord(new DateTime(2026,10,31,10,0,0),Guid.NewGuid(),false),
				CaptureJournal.DeliveredNone,"The quarantine could not be written.",200,null);
			CaptureFileEntry entry=_store.ListJournal().Single();
			string pathResolved;
			string error;
			bool isResolved;
			using(new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)) {
				isResolved=_store.Resolve(entry,CaptureFileDisposition.Assigned,null,out pathResolved,out error);
			}
			Assert.IsTrue(isResolved,error);
			Assert.AreEqual(path,pathResolved);
			StringAssert.Contains(error,"keeps its name");
			Assert.AreEqual(1,Directory.GetFiles(Path.GetDirectoryName(path),"*.txt").Length,"No plain-named sidecar is left behind.");
			CaptureFileEntry entryAfter=_store.ListJournal().Single();
			Assert.IsTrue(entryAfter.HasSidecar,"Never an image without its sidecar.");
			Assert.IsTrue(entryAfter.IsResolved);
		}

		[TestMethod]
		public void Resolve_JournalOnlyCopy_TheRenamedImagesOldSidecarCannotBeRemoved_TheImageStillListsWithItsSidecar() {
			CaptureJournal journal=new CaptureJournal(_folders);
			string path=journal.Write(CaptureImageFile.FromAcquiredImage(Gray8Image(2,2)),SessionRecord(new DateTime(2026,10,31,10,0,0),Guid.NewGuid(),false),
				CaptureJournal.DeliveredNone,"The quarantine could not be written.",200,null);
			StringAssert.EndsWith(Path.GetFileNameWithoutExtension(path),CaptureJournal.OnlyCopySuffix);
			string pathSidecarOnly=Path.ChangeExtension(path,".txt");
			CaptureFileEntry entry=_store.ListJournal().Single();
			string pathResolved;
			string error;
			bool isResolved;
			//Another program reads the -only sidecar: appending and copying it still work, moving or deleting it does not. The image is renamed;
			//the sidecar cannot follow.
			using(new FileStream(pathSidecarOnly,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)) {
				isResolved=_store.Resolve(entry,CaptureFileDisposition.Assigned,null,out pathResolved,out error);
			}
			Assert.IsTrue(isResolved,error);
			Assert.AreEqual(Path.Combine(Path.GetDirectoryName(path),Path.GetFileName(path).Replace(CaptureJournal.OnlyCopySuffix,"")),pathResolved,
				"The plain name: rotation counts it again.");
			StringAssert.Contains(error,"The old sidecar "+Path.GetFileName(pathSidecarOnly));
			CaptureFileEntry entryAfter=_store.ListJournal().Single(x => x.ImagePath==pathResolved);
			Assert.IsTrue(entryAfter.HasSidecar,"The plain-named sidecar was written before the image was renamed.");
			Assert.IsTrue(entryAfter.IsResolved,"Not offered for filing again.");
			Assert.AreEqual(42,entryAfter.PatNum,"Not a patient-unknown copy.");
		}
		#endregion Resolve

		#region Journal SetDelivered
		[TestMethod]
		public void SetDelivered_None_AnotherProgramHoldsTheSidecar_TheOnlyCopyKeepsItsOnlyName_WithANewSidecar() {
			CaptureJournal journal=new CaptureJournal(_folders);
			CaptureImageFile file=CaptureImageFile.FromAcquiredImage(Gray8Image(2,2));
			CaptureRecord record=SessionRecord(new DateTime(2026,10,31,10,0,0),Guid.NewGuid(),false);
			string path=journal.Write(file,record,CaptureJournal.DeliveredCaller,null,200,null);
			string folder=Path.GetDirectoryName(path);
			string pathSidecarOld=Path.ChangeExtension(path,".txt");
			string baseOnly=Path.GetFileNameWithoutExtension(path)+CaptureJournal.OnlyCopySuffix;
			List<string> listLog=new List<string>();
			string pathNow;
			//Another user's Unassigned captures window (or a virus scanner) reads the sidecar just written: it cannot be moved or deleted now.
			using(new FileStream(pathSidecarOld,FileMode.Open,FileAccess.Read,FileShare.Read)) {
				pathNow=journal.SetDelivered(path,file,record,CaptureJournal.DeliveredNone,"The quarantine could not be written.",listLog.Add);
			}
			Assert.AreEqual(Path.Combine(folder,baseOnly+".bmp"),pathNow,"Never the plain name back: rotation would remove the only copy.");
			Assert.IsTrue(File.Exists(pathNow));
			Assert.IsFalse(File.Exists(path));
			CaptureFileEntry entry=_store.ListJournal().Single();
			Assert.AreEqual(pathNow,entry.ImagePath);
			Assert.IsTrue(entry.HasSidecar,"A new sidecar under the new name, written from the record.");
			Assert.AreEqual(42,entry.PatNum,"Its patient, session and slot are kept.");
			Assert.AreEqual(CaptureJournal.DeliveredNone,entry.Delivered);
			Assert.AreEqual(Path.GetFileName(pathNow),CaptureFiles.ReadSidecar(Path.Combine(folder,baseOnly+".txt"))["ImageFile"]);
			Assert.IsTrue(listLog.Any(x => x.Contains("could not follow")),string.Join("\r\n",listLog));
			Assert.IsTrue(listLog.Any(x => x.Contains("could not be removed")),"The old sidecar was still held.\r\n"+string.Join("\r\n",listLog));
			Assert.IsFalse(listLog.Any(x => x.Contains("rotation may remove")),string.Join("\r\n",listLog));
			Assert.AreEqual(0,CaptureJournal.Rotate(folder,0,out string error),"Rotation never removes it.");
			Assert.IsTrue(File.Exists(pathNow));
			Assert.AreEqual(0,Directory.GetFiles(folder,"*.tmp").Length);
		}

		[TestMethod]
		public void SetDelivered_None_AStaleSidecarHasTheNewName_ReplacedByTheNewSidecar_TheOldOneRemoved() {
			CaptureJournal journal=new CaptureJournal(_folders);
			CaptureImageFile file=CaptureImageFile.FromAcquiredImage(Gray8Image(2,2));
			CaptureRecord record=SessionRecord(new DateTime(2026,10,31,10,0,0),Guid.NewGuid(),false);
			string path=journal.Write(file,record,CaptureJournal.DeliveredCaller,null,200,null);
			string folder=Path.GetDirectoryName(path);
			string baseOnly=Path.GetFileNameWithoutExtension(path)+CaptureJournal.OnlyCopySuffix;
			File.WriteAllText(Path.Combine(folder,baseOnly+".txt"),"left over");//the move cannot replace a file
			List<string> listLog=new List<string>();
			string pathNow=journal.SetDelivered(path,file,record,CaptureJournal.DeliveredNone,"The quarantine could not be written.",listLog.Add);
			Assert.AreEqual(Path.Combine(folder,baseOnly+".bmp"),pathNow);
			CollectionAssert.AreEqual(new[] { baseOnly+".txt" },Directory.GetFiles(folder,"*.txt").Select(x => Path.GetFileName(x)).ToArray());
			CaptureFileEntry entry=_store.ListJournal().Single();
			Assert.AreEqual(42,entry.PatNum);
			Assert.AreEqual(CaptureJournal.DeliveredNone,entry.Delivered);
			Assert.IsTrue(listLog.Any(x => x.Contains("could not follow")),string.Join("\r\n",listLog));
		}

		[TestMethod]
		public void SetDelivered_None_NoSidecarCanBeWrittenUnderTheNewName_TheCopyKeepsItsOnlyNameAnyway_BothFailuresLogged() {
			CaptureJournal journal=new CaptureJournal(_folders);
			CaptureImageFile file=CaptureImageFile.FromAcquiredImage(Gray8Image(2,2));
			CaptureRecord record=SessionRecord(new DateTime(2026,10,31,10,0,0),Guid.NewGuid(),false);
			string path=journal.Write(file,record,CaptureJournal.DeliveredCaller,null,200,null);
			string folder=Path.GetDirectoryName(path);
			string baseOnly=Path.GetFileNameWithoutExtension(path)+CaptureJournal.OnlyCopySuffix;
			//The -only sidecar's name is taken (here by a folder): neither a move nor a write can put the sidecar there.
			Directory.CreateDirectory(Path.Combine(folder,baseOnly+".txt"));
			List<string> listLog=new List<string>();
			string pathNow=journal.SetDelivered(path,file,record,CaptureJournal.DeliveredNone,"The quarantine could not be written.",listLog.Add);
			Assert.AreEqual(Path.Combine(folder,baseOnly+".bmp"),pathNow,"A copy without a sidecar can still be filed; a rotated one is gone.");
			Assert.IsTrue(File.Exists(pathNow));
			Assert.AreEqual(0,CaptureJournal.Rotate(folder,0,out string error));
			Assert.IsTrue(File.Exists(pathNow),"Rotation keeps it.");
			Assert.AreEqual(pathNow,_store.ListJournal().Single().ImagePath,"Listed in Unassigned captures.");
			Assert.IsTrue(listLog.Any(x => x.Contains("could not follow")),string.Join("\r\n",listLog));
			Assert.IsTrue(listLog.Any(x => x.Contains("No sidecar could be written")),string.Join("\r\n",listLog));
			Assert.AreEqual(0,Directory.GetFiles(folder,"*.tmp").Length);
		}

		[TestMethod]
		public void SetDelivered_None_TheRewriteFailsAfterTheRename_TheOnlyCopyKeepsItsSidecar_NoOrphanSidecar() {
			CaptureJournal journal=new CaptureJournal(_folders);
			CaptureImageFile file=CaptureImageFile.FromAcquiredImage(Gray8Image(2,2));
			CaptureRecord record=SessionRecord(new DateTime(2026,10,31,10,0,0),Guid.NewGuid(),false);
			string path=journal.Write(file,record,CaptureJournal.DeliveredCaller,null,200,null);
			string folder=Path.GetDirectoryName(path);
			string baseOnly=Path.GetFileNameWithoutExtension(path)+CaptureJournal.OnlyCopySuffix;
			Directory.CreateDirectory(Path.Combine(folder,baseOnly+".txt.tmp"));//the rewrite through the temporary file fails, as on a full disk
			List<string> listLog=new List<string>();
			string pathNow=journal.SetDelivered(path,file,record,CaptureJournal.DeliveredNone,"The quarantine could not be written.",listLog.Add);
			Assert.AreEqual(Path.Combine(folder,baseOnly+".bmp"),pathNow,"Renamed, so that rotation keeps the only copy.");
			Assert.IsTrue(File.Exists(pathNow));
			CaptureFileEntry entry=_store.ListJournal().Single();
			Assert.AreEqual(pathNow,entry.ImagePath);
			Assert.IsTrue(entry.HasSidecar,"The sidecar followed the image before the rewrite.");
			Assert.AreEqual(42,entry.PatNum);
			CollectionAssert.AreEqual(new[] { baseOnly+".txt" },Directory.GetFiles(folder,"*.txt").Select(x => Path.GetFileName(x)).ToArray(),
				"No sidecar is left under the plain name.");
			Assert.IsTrue(listLog.Any(x => x.Contains("could not be updated")),string.Join("\r\n",listLog));
		}

		[TestMethod]
		public void SetDelivered_None_TheSidecarFollowsTheRename_RewrittenWithTheNewDelivered() {
			CaptureJournal journal=new CaptureJournal(_folders);
			CaptureImageFile file=CaptureImageFile.FromAcquiredImage(Gray8Image(2,2));
			CaptureRecord record=SessionRecord(new DateTime(2026,10,31,10,0,0),Guid.NewGuid(),false);
			string path=journal.Write(file,record,CaptureJournal.DeliveredCaller,null,200,null);
			string pathNow=journal.SetDelivered(path,file,record,CaptureJournal.DeliveredNone,"The quarantine could not be written.",null);
			StringAssert.EndsWith(pathNow,CaptureJournal.OnlyCopySuffix+".bmp");
			CaptureFileEntry entry=_store.ListJournal().Single();
			Assert.AreEqual(CaptureJournal.DeliveredNone,entry.Delivered);
			Assert.AreEqual(42,entry.PatNum);
			Assert.AreEqual(1,Directory.GetFiles(Path.GetDirectoryName(path),"*.txt").Length);
			Assert.AreEqual(0,Directory.GetFiles(Path.GetDirectoryName(path),"*.tmp").Length,"No temporary file is left.");
			Assert.AreEqual(pathNow,journal.SetDelivered(pathNow,file,record,CaptureJournal.DeliveredQuarantine,"q.bmp",null),"In place.");
			Assert.AreEqual(CaptureJournal.DeliveredQuarantine,_store.ListJournal().Single().Delivered);
		}
		#endregion Journal SetDelivered

		#region Unassigned count
		[TestMethod]
		public void UnassignedInfo_TurnsIntoTheWarningAfter30Days() {
			string path=WriteSessionQuarantine(Gray8Image(2,2),Guid.NewGuid(),42,new DateTime(2026,10,31,10,0,5));
			File.SetLastWriteTime(path,new DateTime(2026,10,20));
			CaptureUnassignedInfo info=CaptureQuarantine.GetUnassignedInfo(_folders,_now);
			Assert.AreEqual(1,info.Count);
			Assert.IsFalse(info.IsOverdue);
			Assert.AreEqual("1 unassigned capture(s) on this computer.",info.Text);
			File.SetLastWriteTime(path,new DateTime(2026,9,30));
			info=CaptureQuarantine.GetUnassignedInfo(_folders,_now);
			Assert.IsTrue(info.IsOverdue);
			StringAssert.Contains(info.Text,"The oldest is from 2026-09-30.");
			Assert.AreEqual(0,CaptureQuarantine.GetUnassignedInfo(CaptureFolders.CreateForFolder(null),_now).Count);
		}
		#endregion Unassigned count
	}
}
