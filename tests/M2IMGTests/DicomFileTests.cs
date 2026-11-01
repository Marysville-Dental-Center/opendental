// DICOM files made up in the test (fo-dicom, uncompressed), read back by DicomHelper the way the Imaging module opens them, then windowed.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Linq;
using Dicom;
using Dicom.Imaging;
using Dicom.IO.Buffer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace M2IMGTests {
	[TestClass]
	public class DicomFileTests {
		///<summary>An uncompressed grayscale DICOM file of one row with these pixel values.</summary>
		private static DicomFile MakeDicomFile(ushort bitsAllocated,ushort bitsStored,ushort[] arrayValues) {
			DicomDataset dicomDataset=new DicomDataset(DicomTransferSyntax.ExplicitVRLittleEndian);
			dicomDataset.Add(DicomTag.SOPClassUID,DicomUID.DigitalIntraOralXRayImageStorageForPresentation);
			dicomDataset.Add(DicomTag.SOPInstanceUID,DicomUID.Generate());
			dicomDataset.Add(DicomTag.PhotometricInterpretation,"MONOCHROME2");
			dicomDataset.Add(DicomTag.Rows,(ushort)1);
			dicomDataset.Add(DicomTag.Columns,(ushort)arrayValues.Length);
			dicomDataset.Add(DicomTag.BitsAllocated,bitsAllocated);
			dicomDataset.Add(DicomTag.BitsStored,bitsStored);
			dicomDataset.Add(DicomTag.HighBit,(ushort)(bitsStored-1));
			dicomDataset.Add(DicomTag.PixelRepresentation,(ushort)0);
			dicomDataset.Add(DicomTag.SamplesPerPixel,(ushort)1);
			DicomPixelData dicomPixelData=DicomPixelData.Create(dicomDataset,true);
			byte[] arrayBytes=new byte[arrayValues.Length*2];
			Buffer.BlockCopy(arrayValues,0,arrayBytes,0,arrayBytes.Length);
			dicomPixelData.AddFrame(new MemoryByteBuffer(arrayBytes));
			return new DicomFile(dicomDataset);
		}

		private static BitmapDicom ReadBack(DicomFile dicomFile) {
			using MemoryStream memoryStream=new MemoryStream();
			dicomFile.Save(memoryStream,null);
			memoryStream.Position=0;
			BitmapDicom bitmapDicom=DicomHelper.GetFromStream(memoryStream);
			Assert.IsNotNull(bitmapDicom,"The file must be readable.");
			return bitmapDicom;
		}

		[TestMethod]
		public void TrueSixteenBitFile_ImportsAndShowsWithoutSaturation() {
			ushort[] arrayValues=new ushort[] { 0,16384,32768,49152,65535 };
			BitmapDicom bitmapDicom=ReadBack(MakeDicomFile(16,16,arrayValues));
			Assert.AreEqual(16,bitmapDicom.BitsAllocated);
			Assert.AreEqual(16,bitmapDicom.BitsStored);
			CollectionAssert.AreEqual(arrayValues,bitmapDicom.ArrayDataRaw);
			DicomHelper.CalculateWindowingOnImport(bitmapDicom);
			Assert.AreEqual(0,bitmapDicom.WindowingMin);
			Assert.AreEqual(255,bitmapDicom.WindowingMax);
			byte[] arrayGray=WindowingTests.Window(bitmapDicom,bitmapDicom.WindowingMin,bitmapDicom.WindowingMax);
			CollectionAssert.AreEqual(new byte[] { 0,64,128,192,255 },arrayGray,string.Join(",",arrayGray));
			//24.3 showed everything but the black pixel as white with the windowing a user sets on the slider (0-255).
			Assert.AreEqual(4,arrayValues.Count(x => WindowingTests.Window24_3(bitmapDicom,x,0,255)==255));
		}

		[TestMethod]
		public void TwelveBitFile_ShowsAsBefore() {
			ushort[] arrayValues=new ushort[] { 0,1024,2048,3072,4095 };
			BitmapDicom bitmapDicom=ReadBack(MakeDicomFile(16,12,arrayValues));
			Assert.AreEqual(16,bitmapDicom.BitsAllocated);
			Assert.AreEqual(12,bitmapDicom.BitsStored,"BitsStored, not BitsAllocated, tells 12 bit from 16 bit.");
			CollectionAssert.AreEqual(arrayValues,bitmapDicom.ArrayDataRaw);
			byte[] arrayGray=WindowingTests.Window(bitmapDicom,0,255);
			CollectionAssert.AreEqual(new byte[] { 0,64,128,192,255 },arrayGray,string.Join(",",arrayGray));
			CollectionAssert.AreEqual(arrayValues.Select(x => WindowingTests.Window24_3(bitmapDicom,x,0,255)).ToArray(),arrayGray);
		}

		[TestMethod]
		public void TrueSixteenBitFile_FromDisk_LikeTheImagingModule() {
			//ImageStore.OpenBitmapDicom reads documents in a local A-to-Z folder with DicomHelper.GetFromFile.
			string path=Path.Combine(Path.GetTempPath(),"m2img_"+Guid.NewGuid().ToString("N")+".dcm");
			try {
				MakeDicomFile(16,16,new ushort[] { 0,40000,65535 }).Save(path,null);
				BitmapDicom bitmapDicom=DicomHelper.GetFromFile(path);
				Assert.IsNotNull(bitmapDicom);
				Assert.AreEqual(16,bitmapDicom.BitsStored);
				CollectionAssert.AreEqual(new byte[] { 0,156,255 },WindowingTests.Window(bitmapDicom,0,255));
			}
			finally {
				try {
					File.Delete(path);
				}
				catch(Exception) {
				}
			}
		}
	}
}
