// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using ImagingDeviceManager.Capture;
using ImagingDeviceManager.Imaging;
using ImagingDeviceManager.Journal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingDeviceManager.Tests {
	///<summary>Reads the baseline TIFF that Gray16Tiff writes, for the tests.</summary>
	internal static class TiffReader {
		public sealed class Tiff {
			public int Width;
			public int Height;
			public int BitsPerSample;
			public int Compression;
			public int Photometric;
			public double XDpi;
			public ushort[] Samples;
		}

		public static Tiff Read(byte[] bytes) {
			Assert.AreEqual((byte)'I',bytes[0]);
			Assert.AreEqual((byte)'I',bytes[1]);
			Assert.AreEqual(42,BitConverter.ToUInt16(bytes,2));
			int offsetIfd=BitConverter.ToInt32(bytes,4);
			int count=BitConverter.ToUInt16(bytes,offsetIfd);
			Dictionary<int,int> dictValues=new Dictionary<int,int>();
			int lastTag=0;
			for(int i=0;i<count;i++) {
				int offset=offsetIfd+2+i*12;
				int tag=BitConverter.ToUInt16(bytes,offset);
				Assert.IsTrue(tag>lastTag,"Tags ascend.");
				lastTag=tag;
				int type=BitConverter.ToUInt16(bytes,offset+2);
				dictValues[tag]=type==3 ? BitConverter.ToUInt16(bytes,offset+8) : BitConverter.ToInt32(bytes,offset+8);
			}
			Tiff tiff=new Tiff {
				Width=dictValues[256],
				Height=dictValues[257],
				BitsPerSample=dictValues[258],
				Compression=dictValues[259],
				Photometric=dictValues[262],
			};
			int offsetX=dictValues[282];
			tiff.XDpi=(double)BitConverter.ToUInt32(bytes,offsetX)/BitConverter.ToUInt32(bytes,offsetX+4);
			int offsetPixels=dictValues[273];
			Assert.AreEqual(tiff.Width*tiff.Height*2,dictValues[279]);
			tiff.Samples=new ushort[tiff.Width*tiff.Height];
			for(int i=0;i<tiff.Samples.Length;i++) {
				tiff.Samples[i]=BitConverter.ToUInt16(bytes,offsetPixels+i*2);
			}
			return tiff;
		}
	}

	///<summary>The journal and quarantine files of capture-session images (design 7.1 CAP-3, 7.12): 16-bit copies as TIFF, the session
	///quarantine folder and its sidecar.</summary>
	[TestClass]
	public class CaptureSessionFilesTests {
		private string _folder;
		private CaptureFolders _folders;

		[TestInitialize]
		public void Setup() {
			_folder=Path.Combine(Path.GetTempPath(),"CaptureSessionFilesTests_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_folder);
			_folders=CaptureFolders.CreateForFolder(_folder);
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
			FakePage page=FakePage.CreateGray8(width,height,1270,(x,y) => (byte)(x+y));
			return new AcquiredImage { Width=width,Height=height,BitsPerPixel=8,Dib=page.NativeDib,XDpi=1270,YDpi=1270 };
		}

		[TestMethod]
		public void Gray16Tiff_SamplesUnchanged_BaselineTags() {
			AcquiredImage image=Gray16Image(5,3);
			TiffReader.Tiff tiff=TiffReader.Read(Gray16Tiff.Encode(5,3,image.Gray16,1270,1270,false));
			Assert.AreEqual(5,tiff.Width);
			Assert.AreEqual(3,tiff.Height);
			Assert.AreEqual(16,tiff.BitsPerSample);
			Assert.AreEqual(1,tiff.Compression);
			Assert.AreEqual(1,tiff.Photometric,"BlackIsZero for chocolate.");
			Assert.AreEqual(1270,tiff.XDpi,0.01);
			CollectionAssert.AreEqual(image.Gray16,tiff.Samples);
			Assert.AreEqual(0,TiffReader.Read(Gray16Tiff.Encode(1,1,new ushort[] { 7 },0,0,true)).Photometric,"WhiteIsZero for vanilla.");
		}

		[TestMethod]
		public void Gray16Tiff_ReadableByGdiPlus() {
			string path=Path.Combine(_folder,"x.tif");
			File.WriteAllBytes(path,Gray16Tiff.Encode(4,2,Gray16Image(4,2).Gray16,300,300,false));
			try {
				using(Image image=Image.FromFile(path)) {
					Assert.AreEqual(4,image.Width);
					Assert.AreEqual(2,image.Height);
				}
			}
			catch(OutOfMemoryException) {
				Assert.Inconclusive("GDI+ on this computer does not read 16-bit grayscale TIFF; the file itself is checked above.");
			}
		}

		[TestMethod]
		public void Journal_Gray16CopyAsTif_WithCaptureFields() {
			CaptureJournal journal=new CaptureJournal(_folders);
			CaptureRecord record=CaptureRecord.Create(new DateTime(2026,10,31,10,0,0),"Src","s-1");
			record.ListCaptureFields.Add(new SidecarField("EnableId","3"));
			record.CaptureId="cap-9";
			AcquiredImage image=Gray16Image(6,4);
			string path=journal.Write(CaptureImageFile.FromAcquiredImage(image),record,CaptureJournal.DeliveredCaller,null,200,null);
			Assert.IsNotNull(path);
			Assert.AreEqual(".tif",Path.GetExtension(path));
			CollectionAssert.AreEqual(image.Gray16,TiffReader.Read(File.ReadAllBytes(path)).Samples);
			Dictionary<string,string> dictSidecar=CaptureFiles.ReadSidecar(Path.ChangeExtension(path,".txt"));
			Assert.AreEqual("3",dictSidecar["EnableId"]);
			Assert.AreEqual("cap-9",dictSidecar["CaptureId"]);
			Assert.AreEqual("16",dictSidecar["BitsPerPixel"]);
			Assert.AreEqual(Path.GetFileName(path),dictSidecar["ImageFile"]);
			StringAssert.StartsWith(dictSidecar["ImageFormat"],"TIFF");
			Assert.AreEqual(CaptureFiles.Sha256Hex(File.ReadAllBytes(path)),dictSidecar["Sha256"]);
		}

		[TestMethod]
		public void Journal_RotationCountsBmpAndTifTogether() {
			CaptureJournal journal=new CaptureJournal(_folders);
			DateTime start=new DateTime(2026,10,31,10,0,0);
			List<string> listPaths=new List<string>();
			for(int i=0;i<4;i++) {
				AcquiredImage image=i%2==0 ? Gray16Image(2,2) : Gray8Image(2,2);
				listPaths.Add(journal.Write(CaptureImageFile.FromAcquiredImage(image),CaptureRecord.Create(start.AddSeconds(i),"Src","a"),
					CaptureJournal.DeliveredCaller,null,3,null));
			}
			Assert.IsFalse(File.Exists(listPaths[0]),"The oldest copy, a .tif, was rotated out.");
			Assert.IsFalse(File.Exists(Path.ChangeExtension(listPaths[0],".txt")));
			Assert.IsTrue(listPaths.Skip(1).All(File.Exists));
		}

		[TestMethod]
		public void Quarantine_SessionFolder_SidecarNamesPatientSessionAndSlot() {
			QuarantineWriter quarantine=new QuarantineWriter(_folders);
			Guid sessionGuid=Guid.NewGuid();
			CaptureRecord record=CaptureRecord.Create(new DateTime(2026,10,31,10,0,0),"Src","s-1");
			record.Sequence=2;
			string error;
			string path=quarantine.WriteSessionCapture(CaptureImageFile.FromAcquiredImage(Gray16Image(3,3)),record,sessionGuid,42,"Doe, Jane",
				"Slot 3 (teeth 2,3)","It arrived after a patient change.",null,out error);
			Assert.IsNotNull(path,error);
			Assert.AreEqual(Path.Combine(_folder,@"Capture\quarantine",sessionGuid.ToString("D")),Path.GetDirectoryName(path));
			Dictionary<string,string> dictSidecar=CaptureFiles.ReadSidecar(Path.ChangeExtension(path,".txt"));
			Assert.AreEqual("quarantine",dictSidecar["Kind"]);
			Assert.AreEqual("Doe, Jane",dictSidecar["Patient"]);
			Assert.AreEqual("42",dictSidecar["PatNum"]);
			Assert.AreEqual(sessionGuid.ToString("D"),dictSidecar["SessionGuid"]);
			Assert.AreEqual("Slot 3 (teeth 2,3)",dictSidecar["Slot"]);
			Assert.AreEqual("It arrived after a patient change.",dictSidecar["Reason"]);
			Assert.AreEqual("2",dictSidecar["Sequence"]);
			Assert.AreEqual("Src",dictSidecar["Source"]);
			DateTime? oldest;
			Assert.AreEqual(1,QuarantineWriter.CountImages(_folders,out oldest),"Counted in its session folder.");
		}

		[TestMethod]
		public void Quarantine_NoPatient_PatientUnknown() {
			QuarantineWriter quarantine=new QuarantineWriter(_folders);
			string error;
			string path=quarantine.WriteSessionCapture(CaptureImageFile.FromAcquiredImage(Gray8Image(2,2)),
				CaptureRecord.Create(DateTime.Now,"Src","s"),Guid.NewGuid(),0,"Doe, Jane","","Declined redelivered image.",null,out error);
			Assert.IsNotNull(path,error);
			Assert.AreEqual(".bmp",Path.GetExtension(path));
			Dictionary<string,string> dictSidecar=CaptureFiles.ReadSidecar(Path.ChangeExtension(path,".txt"));
			Assert.AreEqual(QuarantineWriter.PatientUnknown,dictSidecar["Patient"]);
			Assert.AreEqual("",dictSidecar["PatNum"]);
		}

		[TestMethod]
		public void ImageFile_NoPixels_Throws() {
			try {
				CaptureImageFile.FromAcquiredImage(new AcquiredImage());
				Assert.Fail();
			}
			catch(ArgumentException) {
			}
		}
	}
}
