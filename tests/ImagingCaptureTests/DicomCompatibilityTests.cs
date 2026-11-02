// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using ImagingCapture.Dicom;
using ImagingDeviceManager.Capture;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace ImagingCapture.Tests {
	///<summary>The compatibility test of design 7.8: a file from DicomIntraOralWriter opens through the program's own DICOM loading
	///(DicomHelper, which ImageStore.OpenBitmapDicom calls; read-only use, no database) with identical pixels, through the subset reader that
	///reads it and through the full reader that would read it if the subset reader declined. fo-dicom, whose PS3.6 dictionary this test also
	///checked the writer's tags against, is gone (docs/imaging/dicom-reader.md); DicomIntraOralWriterTests check the tags and VRs.</summary>
	[TestClass]
	public class DicomCompatibilityTests {
		[TestMethod]
		public void WrittenFile_OpensThroughTheProgramsDicomLoader() {
			string folder=Path.Combine(Path.GetTempPath(),"ImagingCaptureTests_dicom_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(folder);
			try {
				foreach(int bitsStoredExpected in new[] { 16,12 }) {
					int width=37;//odd, so rows are not word-aligned by accident
					int height=23;
					ushort[] arrayPixels=new ushort[width*height];
					int maxValue=bitsStoredExpected==12 ? 4095 : 65535;
					for(int i=0;i<arrayPixels.Length;i++) {
						arrayPixels[i]=(ushort)((i*7919L)%(maxValue+1));
					}
					DicomIntraOralImage image=new DicomIntraOralImage {
						Width=width,
						Height=height,
						Pixels=arrayPixels,
						XDpi=1270,
						YDpi=1270,
						PixelFlavor=AcquiredImage.PixelFlavorChocolate,
						Target=new IntraOralTarget { HasMountSlot=true,ToothNumbers="2,3,30,31",DegreesRotated=0 },
						Metadata=ImagingCapture.Metadata.CaptureMetadataParser.Parse(@"{""schema"":""capture-metadata/1"",""manufacturer"":""Example"","
							+@"""serial"":""SN-1"",""acquiredAt"":""2026-10-31T14:25:30.123+01:00"",""effectiveBits"":"
							+(bitsStoredExpected==12 ? "12" : "null")+@",""redelivered"":false}"),
						StudyDateTime=new DateTime(2026,10,31),
						InstanceNumber=1,
					};
					DicomWriteResult result=DicomIntraOralWriter.Write(image);
					Assert.IsTrue(result.IsWritten,result.Error);
					Assert.AreEqual(bitsStoredExpected,result.BitsStored);
					string path=Path.Combine(folder,"image"+bitsStoredExpected+".dcm");
					File.WriteAllBytes(path,result.Bytes);
					BitmapDicom bitmapDicomStream;
					using(MemoryStream memoryStream=new MemoryStream(result.Bytes)) {
						bitmapDicomStream=DicomHelper.GetFromStream(memoryStream);
					}
					//DicomHelper reads the writer's subset with the subset reader (CAP-DR); the full reader reads the same bytes to the same image.
					Assert.AreEqual(DicomSubsetReadStatus.Read,DicomSubsetReader.Read(result.Bytes).Status);
					BitmapDicom bitmapDicomFull=DicomHelper.FullReader.Read(result.Bytes).BitmapDicom;
					BitmapDicom bitmapDicom=DicomHelper.GetFromFile(path);
					foreach(BitmapDicom bitmapDicomRead in new[] { bitmapDicomStream,bitmapDicom,bitmapDicomFull }) {
						Assert.IsNotNull(bitmapDicomRead,"The loader refused the file (BitsStored "+bitsStoredExpected+").");
						Assert.AreEqual(width,bitmapDicomRead.Width);
						Assert.AreEqual(height,bitmapDicomRead.Height);
						Assert.AreEqual(16,bitmapDicomRead.BitsAllocated);
						Assert.AreEqual(bitsStoredExpected,bitmapDicomRead.BitsStored);
						Assert.AreEqual(EnumDicomPhotometricInterp.MONOCHROME2,bitmapDicomRead.PhotometricInterp);
						CollectionAssert.AreEqual(arrayPixels,bitmapDicomRead.ArrayDataRaw,"Identical pixels (BitsStored "+bitsStoredExpected+").");
					}
				}
			}
			finally {
				try {
					Directory.Delete(folder,true);
				}
				catch(Exception) {
				}
			}
		}
	}
}
