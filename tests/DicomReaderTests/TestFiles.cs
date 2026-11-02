// Test files from the program's own DicomIntraOralWriter (ImagingCapture) and from the test's PS3.10 writer in other layouts and transfer
// syntaxes. All pixel values are made up. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
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

		///<summary>A one-frame grayscale file in the given transfer syntax (uncompressed ones), as another writer than the capture writer would
		///make it: the test's PS3.10 writer, with the elements of SynImage (no intra-oral sequences).</summary>
		public static byte[] FromOtherWriter(string transferSyntax,int rows,int columns,ushort bitsAllocated,ushort bitsStored,ushort pixelRepresentation,
			string photometric,byte[] arrayPixelBytes)
		{
			SynImage synImage=new SynImage { Rows=rows,Columns=columns,Photometric=photometric,BitsAllocated=bitsAllocated,BitsStored=bitsStored,
				HighBit=(ushort)(bitsStored-1),PixelRepresentation=pixelRepresentation,PixelBytes=arrayPixelBytes,PixelVr=bitsAllocated==8 ? "OB" : "OW" };
			return SyntheticDicom.MakeFileIn(synImage,transferSyntax);
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

		///<summary>The subset reader reads the bytes, and the full reader reads them to the very same BitmapDicom (the full reader gives what
		///fo-dicom's old path gave for every such file, FoDicomBaseline.cs).</summary>
		public static BitmapDicom AssertReadAndSameAsFullReader(byte[] bytes,string message) {
			DicomSubsetReadResult result=DicomSubsetReader.Read(bytes);
			Assert.AreEqual(DicomSubsetReadStatus.Read,result.Status,message+": "+result);
			DicomImageReadResult resultFull=DicomHelper.FullReader.Read(bytes);
			Assert.AreEqual(DicomImageReadStatus.Read,resultFull.Status,message+": full reader "+resultFull);
			AssertSame(resultFull.BitmapDicom,result.BitmapDicom,message+" (subset reader against the full reader)");
			return result.BitmapDicom;
		}
	}
}
