// Test files from the two other writers: the program's own DicomIntraOralWriter (ImagingCapture), and fo-dicom. All pixel values are made up.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using Dicom;
using Dicom.Imaging;
using Dicom.IO.Buffer;
using ImagingCapture.Dicom;
using ImagingCapture.Metadata;
using ImagingDeviceManager.Capture;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace DicomReaderTests {
	internal static class TestFiles {
		///<summary>A file exactly as the capture session would write it (design 7.8): Explicit VR Little Endian, PS3.10 File Meta Information,
		///uncompressed MONOCHROME2, Bits Allocated 16, 2.25 UIDs, with the intra-oral sequences of defined length. effectiveBits 12 with every
		///value at most 4095 gives Bits Stored 12; null gives 16.</summary>
		public static byte[] FromCaptureWriter(int width,int height,ushort[] arrayPixels,int? effectiveBits) {
			DicomIntraOralImage image=new DicomIntraOralImage {
				Width=width,
				Height=height,
				Pixels=arrayPixels,
				XDpi=1270,
				YDpi=1270,
				PixelFlavor=AcquiredImage.PixelFlavorChocolate,
				Target=new IntraOralTarget { HasMountSlot=true,ToothNumbers="2,3,30,31",DegreesRotated=0 },
				Metadata=CaptureMetadataParser.Parse(@"{""schema"":""capture-metadata/1"",""manufacturer"":""Example"",""serial"":""SN-1"","
					+@"""acquiredAt"":""2026-10-31T14:25:30.123+01:00"",""effectiveBits"":"+(effectiveBits==null ? "null" : effectiveBits.ToString())
					+@",""redelivered"":false}"),
				StudyDateTime=new DateTime(2026,10,31),
				InstanceNumber=1,
			};
			DicomWriteResult result=DicomIntraOralWriter.Write(image);
			Assert.IsTrue(result.IsWritten,result.Error);
			return result.Bytes;
		}

		///<summary>A one-frame grayscale file written by fo-dicom in the given transfer syntax (uncompressed ones only).</summary>
		public static byte[] FromFoDicom(DicomTransferSyntax transferSyntax,int rows,int columns,ushort bitsAllocated,ushort bitsStored,
			ushort pixelRepresentation,string photometric,byte[] arrayPixelBytes)
		{
			DicomDataset dicomDataset=new DicomDataset(transferSyntax);
			dicomDataset.Add(DicomTag.SOPClassUID,DicomUID.DigitalIntraOralXRayImageStorageForPresentation);
			dicomDataset.Add(DicomTag.SOPInstanceUID,DicomUID.Generate());
			dicomDataset.Add(DicomTag.PhotometricInterpretation,photometric);
			dicomDataset.Add(DicomTag.Rows,(ushort)rows);
			dicomDataset.Add(DicomTag.Columns,(ushort)columns);
			dicomDataset.Add(DicomTag.BitsAllocated,bitsAllocated);
			dicomDataset.Add(DicomTag.BitsStored,bitsStored);
			dicomDataset.Add(DicomTag.HighBit,(ushort)(bitsStored-1));
			dicomDataset.Add(DicomTag.PixelRepresentation,pixelRepresentation);
			dicomDataset.Add(DicomTag.SamplesPerPixel,(ushort)1);
			DicomPixelData dicomPixelData=DicomPixelData.Create(dicomDataset,true);
			dicomPixelData.AddFrame(new MemoryByteBuffer(arrayPixelBytes));
			DicomFile dicomFile=new DicomFile(dicomDataset);
			using(MemoryStream memoryStream=new MemoryStream()) {
				dicomFile.Save(memoryStream,null);
				return memoryStream.ToArray();
			}
		}

		///<summary>What the old path (fo-dicom, unchanged) makes of the bytes, or null when it reads nothing; "throws" when it throws.</summary>
		public static BitmapDicom ReadWithFoDicom(byte[] bytes,out string error) {
			error=null;
			try {
				using(MemoryStream memoryStream=new MemoryStream(bytes)) {
					return DicomHelper.FoDicomReader.ReadStream(memoryStream);
				}
			}
			catch(Exception ex) {
				error=ex.GetType().Name+": "+ex.Message;
				return null;
			}
		}

		public static void AssertSame(BitmapDicom expected,BitmapDicom actual,string message) {
			Assert.IsNotNull(expected,message+": no expected image.");
			Assert.IsNotNull(actual,message+": no image read.");
			Assert.AreEqual(expected.Width,actual.Width,message+": Width");
			Assert.AreEqual(expected.Height,actual.Height,message+": Height");
			Assert.AreEqual(expected.BitsAllocated,actual.BitsAllocated,message+": BitsAllocated");
			Assert.AreEqual(expected.BitsStored,actual.BitsStored,message+": BitsStored");
			Assert.AreEqual(expected.PhotometricInterp,actual.PhotometricInterp,message+": PhotometricInterp");
			CollectionAssert.AreEqual(expected.ArrayDataRaw,actual.ArrayDataRaw,message+": pixels");
		}

		///<summary>The subset reader reads the bytes, and fo-dicom (the old path) reads them to the very same BitmapDicom.</summary>
		public static BitmapDicom AssertReadAndSameAsFoDicom(byte[] bytes,string message) {
			DicomSubsetReadResult result=DicomSubsetReader.Read(bytes);
			Assert.AreEqual(DicomSubsetReadStatus.Read,result.Status,message+": "+result);
			BitmapDicom bitmapDicomFoDicom=ReadWithFoDicom(bytes,out string error);
			Assert.IsNull(error,message+": fo-dicom "+error);
			AssertSame(bitmapDicomFoDicom,result.BitmapDicom,message+" (subset reader against fo-dicom)");
			return result.BitmapDicom;
		}
	}
}
