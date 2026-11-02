// DICOM files made up in the test (uncompressed, written byte by byte from NEMA PS3.5 and PS3.10), read back by DicomHelper the way the
// Imaging module opens them, then windowed. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2IMGTests {
	[TestClass]
	public class DicomFileTests {
		///<summary>One Explicit VR Little Endian element (PS3.5 7.1.2): OB and OW have two reserved bytes and a 32-bit length.</summary>
		private static void WriteElement(BinaryWriter binaryWriter,ushort group,ushort element,string vr,byte[] value) {
			if(value.Length%2==1) {
				value=value.Concat(new byte[] { vr=="UI" ? (byte)0 : (byte)' ' }).ToArray();
			}
			binaryWriter.Write(group);
			binaryWriter.Write(element);
			binaryWriter.Write(Encoding.ASCII.GetBytes(vr));
			if(vr=="OB" || vr=="OW") {
				binaryWriter.Write((ushort)0);
				binaryWriter.Write((uint)value.Length);
			}
			else {
				binaryWriter.Write((ushort)value.Length);
			}
			binaryWriter.Write(value);
		}

		///<summary>An uncompressed grayscale DICOM file of one row with these pixel values: the PS3.10 preamble, "DICM", the File Meta
		///Information with Transfer Syntax Explicit VR Little Endian, then the Image Pixel module.</summary>
		private static byte[] MakeDicomFile(ushort bitsAllocated,ushort bitsStored,ushort[] arrayValues) {
			using MemoryStream memoryStreamMeta=new MemoryStream();
			using(BinaryWriter binaryWriter=new BinaryWriter(memoryStreamMeta,Encoding.ASCII,true)) {
				WriteElement(binaryWriter,0x0002,0x0001,"OB",new byte[] { 0,1 });
				WriteElement(binaryWriter,0x0002,0x0002,"UI",Encoding.ASCII.GetBytes("1.2.840.10008.5.1.4.1.1.1.3"));
				WriteElement(binaryWriter,0x0002,0x0003,"UI",Encoding.ASCII.GetBytes("2.25.1234567890"));
				WriteElement(binaryWriter,0x0002,0x0010,"UI",Encoding.ASCII.GetBytes("1.2.840.10008.1.2.1"));
			}
			using MemoryStream memoryStream=new MemoryStream();
			using(BinaryWriter binaryWriter=new BinaryWriter(memoryStream,Encoding.ASCII,true)) {
				binaryWriter.Write(new byte[128]);
				binaryWriter.Write(Encoding.ASCII.GetBytes("DICM"));
				WriteElement(binaryWriter,0x0002,0x0000,"UL",BitConverter.GetBytes((uint)memoryStreamMeta.Length));
				binaryWriter.Write(memoryStreamMeta.ToArray());
				WriteElement(binaryWriter,0x0008,0x0016,"UI",Encoding.ASCII.GetBytes("1.2.840.10008.5.1.4.1.1.1.3"));
				WriteElement(binaryWriter,0x0008,0x0018,"UI",Encoding.ASCII.GetBytes("2.25.1234567890"));
				WriteElement(binaryWriter,0x0028,0x0002,"US",BitConverter.GetBytes((ushort)1));
				WriteElement(binaryWriter,0x0028,0x0004,"CS",Encoding.ASCII.GetBytes("MONOCHROME2"));
				WriteElement(binaryWriter,0x0028,0x0010,"US",BitConverter.GetBytes((ushort)1));
				WriteElement(binaryWriter,0x0028,0x0011,"US",BitConverter.GetBytes((ushort)arrayValues.Length));
				WriteElement(binaryWriter,0x0028,0x0100,"US",BitConverter.GetBytes(bitsAllocated));
				WriteElement(binaryWriter,0x0028,0x0101,"US",BitConverter.GetBytes(bitsStored));
				WriteElement(binaryWriter,0x0028,0x0102,"US",BitConverter.GetBytes((ushort)(bitsStored-1)));
				WriteElement(binaryWriter,0x0028,0x0103,"US",BitConverter.GetBytes((ushort)0));
				byte[] arrayBytes=new byte[arrayValues.Length*2];
				Buffer.BlockCopy(arrayValues,0,arrayBytes,0,arrayBytes.Length);
				WriteElement(binaryWriter,0x7FE0,0x0010,"OW",arrayBytes);
			}
			return memoryStream.ToArray();
		}

		private static BitmapDicom ReadBack(byte[] bytesFile) {
			using MemoryStream memoryStream=new MemoryStream(bytesFile);
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
				File.WriteAllBytes(path,MakeDicomFile(16,16,new ushort[] { 0,40000,65535 }));
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
