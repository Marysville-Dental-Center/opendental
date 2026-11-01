// The in-house DICOM reader (CAP-DR, design 7.8): the subset it reads, what it declines, and malformed files that must fail cleanly.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dicom;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using static DicomReaderTests.SyntheticDicom;

namespace DicomReaderTests {
	[TestClass]
	public class DicomSubsetReaderTests {
		private static DicomSubsetReadResult Read(byte[] bytes) {
			return DicomSubsetReader.Read(bytes);
		}

		private static void AssertStatus(DicomSubsetReadStatus status,byte[] bytes,string message) {
			DicomSubsetReadResult result=Read(bytes);
			Assert.AreEqual(status,result.Status,message+": "+result);
			Assert.IsNull(result.BitmapDicom,message);
			Assert.AreNotEqual("",result.Reason,message+": a declined file has a reason.");
		}

		private static byte[] Image16MakeFile(int rows,int columns,ushort bitsStored,ushort[] arraySamples,string photometric="MONOCHROME2") {
			return MakeFile(Image16(rows,columns,bitsStored,arraySamples,photometric));
		}

		#region The capture writer's files
		[TestMethod]
		public void CaptureWriterFiles_AreReadExactly_AndAsFoDicomReadsThem() {
			foreach(int? effectiveBits in new int?[] { null,12 }) {
				int width=37;//odd, so rows are not word-aligned by accident
				int height=23;
				ushort[] arrayPixels=Ramp(width*height,effectiveBits==12 ? 4095 : 65535);
				byte[] bytes=TestFiles.FromCaptureWriter(width,height,arrayPixels,effectiveBits);
				BitmapDicom bitmapDicom=TestFiles.AssertReadAndSameAsFoDicom(bytes,"writer, effectiveBits "+effectiveBits);
				Assert.AreEqual(width,bitmapDicom.Width);
				Assert.AreEqual(height,bitmapDicom.Height);
				Assert.AreEqual(16,bitmapDicom.BitsAllocated);
				Assert.AreEqual(effectiveBits==12 ? 12 : 16,bitmapDicom.BitsStored);
				Assert.AreEqual(EnumDicomPhotometricInterp.MONOCHROME2,bitmapDicom.PhotometricInterp);
				CollectionAssert.AreEqual(arrayPixels,bitmapDicom.ArrayDataRaw,"The samples exactly as written.");
			}
		}

		[TestMethod]
		public void CaptureWriterFile_DicomHelperFileStreamAndBase64_AllGiveTheSamePixels() {
			ushort[] arrayPixels=Ramp(64*48,65535);
			byte[] bytes=TestFiles.FromCaptureWriter(64,48,arrayPixels,null);
			string path=Path.Combine(Path.GetTempPath(),"dicomreadertests_"+Guid.NewGuid().ToString("N")+".dcm");
			try {
				System.IO.File.WriteAllBytes(path,bytes);
				BitmapDicom bitmapDicomFile=DicomHelper.GetFromFile(path);
				BitmapDicom bitmapDicomSubsetFile=DicomHelper.SubsetReader.ReadFile(path);
				BitmapDicom bitmapDicomStream;
				using(MemoryStream memoryStream=new MemoryStream(bytes)) {
					bitmapDicomStream=DicomHelper.GetFromStream(memoryStream);
				}
				BitmapDicom bitmapDicomBase64=DicomHelper.GetFromBase64(Convert.ToBase64String(bytes));
				foreach(BitmapDicom bitmapDicom in new[] { bitmapDicomFile,bitmapDicomSubsetFile,bitmapDicomStream,bitmapDicomBase64 }) {
					Assert.IsNotNull(bitmapDicom);
					CollectionAssert.AreEqual(arrayPixels,bitmapDicom.ArrayDataRaw);
					Assert.AreEqual(16,bitmapDicom.BitsStored);
				}
			}
			finally {
				System.IO.File.Delete(path);
			}
		}
		#endregion

		#region Bit depths, photometric interpretations, odd lengths
		[TestMethod]
		public void EightBit_OddSampleCount_PadByteIgnored() {
			byte[] arraySamples=Enumerable.Range(0,15).Select(x => (byte)(x*17)).ToArray();
			byte[] bytes=MakeFile(ImageDataset(3,5,8,8,7,0,"MONOCHROME2",Bytes8(arraySamples),"OB"));
			BitmapDicom bitmapDicom=TestFiles.AssertReadAndSameAsFoDicom(bytes,"8 bit, 3 x 5");
			Assert.AreEqual(8,bitmapDicom.BitsAllocated);
			Assert.AreEqual(8,bitmapDicom.BitsStored);
			CollectionAssert.AreEqual(arraySamples.Select(x => (ushort)x).ToArray(),bitmapDicom.ArrayDataRaw);
		}

		[TestMethod]
		public void EightBit_InOW_ReadsTheSameBytes() {
			byte[] arraySamples=Enumerable.Range(0,12).Select(x => (byte)(255-x*20)).ToArray();
			byte[] bytes=MakeFile(ImageDataset(3,4,8,8,7,0,"MONOCHROME2",arraySamples,"OW"));
			BitmapDicom bitmapDicom=TestFiles.AssertReadAndSameAsFoDicom(bytes,"8 bit in OW");
			CollectionAssert.AreEqual(arraySamples.Select(x => (ushort)x).ToArray(),bitmapDicom.ArrayDataRaw);
		}

		[TestMethod]
		public void TwelveBit_BitsAboveHighBitAreCleared_AsFoDicomDoes() {
			ushort[] arrayStored={ 0x0000,0x0FFF,0xF123,0x8800,0x0ABC,0x7001 };
			byte[] bytes=Image16MakeFile(2,3,12,arrayStored);
			BitmapDicom bitmapDicom=TestFiles.AssertReadAndSameAsFoDicom(bytes,"12 bit with bits above High Bit set");
			CollectionAssert.AreEqual(arrayStored.Select(x => (ushort)(x & 0x0FFF)).ToArray(),bitmapDicom.ArrayDataRaw);
		}

		[TestMethod]
		public void EightBitsStoredInSixteen_KeepsTheLowByte() {
			ushort[] arrayStored={ 0x0012,0xFF34,0x0100,0x00FF };
			byte[] bytes=MakeFile(ImageDataset(2,2,16,8,7,0,"MONOCHROME2",Words(arrayStored)));
			BitmapDicom bitmapDicom=TestFiles.AssertReadAndSameAsFoDicom(bytes,"8 bits stored in 16");
			CollectionAssert.AreEqual(new ushort[] { 0x12,0x34,0x00,0xFF },bitmapDicom.ArrayDataRaw);
		}

		[TestMethod]
		public void SixteenBit_Monochrome1AndMonochrome2_ValuesUntouched() {
			ushort[] arrayStored={ 0,1,255,256,4095,4096,32768,65534,65535 };
			foreach(string photometric in new[] { "MONOCHROME1","MONOCHROME2" }) {
				BitmapDicom bitmapDicom=TestFiles.AssertReadAndSameAsFoDicom(Image16MakeFile(3,3,16,arrayStored,photometric),photometric);
				Assert.AreEqual(photometric,bitmapDicom.PhotometricInterp.ToString());
				CollectionAssert.AreEqual(arrayStored,bitmapDicom.ArrayDataRaw,"No inversion here; the windowing handles MONOCHROME1 as before.");
			}
		}

		[TestMethod]
		public void PixelDataLongerThanNeeded_ExtraBytesIgnored() {
			ushort[] arrayStored={ 10,20,30,40 };
			byte[] bytesPixels=Words(arrayStored).Concat(new byte[] { 1,2,3,4 }).ToArray();
			BitmapDicom bitmapDicom=Read(MakeFile(ImageDataset(2,2,16,16,15,0,"MONOCHROME2",bytesPixels))).BitmapDicom;
			Assert.IsNotNull(bitmapDicom);
			CollectionAssert.AreEqual(arrayStored,bitmapDicom.ArrayDataRaw);
		}

		[TestMethod]
		public void OddLengthValue_IsSkippedByItsLength() {
			//PS3.5 7.1.1 asks for even lengths; a writer that breaks it still tells where the next element starts.
			List<SynElement> listDataset=With(Image16(2,2,16,new ushort[] { 1,2,3,4 }),Raw(0x0008,0x1030,"LO",new byte[] { (byte)'A',(byte)'B',(byte)'C' }));
			BitmapDicom bitmapDicom=Read(MakeFile(listDataset)).BitmapDicom;
			Assert.IsNotNull(bitmapDicom);
			CollectionAssert.AreEqual(new ushort[] { 1,2,3,4 },bitmapDicom.ArrayDataRaw);
		}

		[TestMethod]
		public void FoDicomWrittenExplicitVrFile_IsReadBySubsetReader() {
			ushort[] arrayStored=Ramp(5*7,65535);
			byte[] bytes=TestFiles.FromFoDicom(DicomTransferSyntax.ExplicitVRLittleEndian,5,7,16,16,0,"MONOCHROME2",Words(arrayStored));
			BitmapDicom bitmapDicom=TestFiles.AssertReadAndSameAsFoDicom(bytes,"fo-dicom Explicit VR Little Endian");
			CollectionAssert.AreEqual(arrayStored,bitmapDicom.ArrayDataRaw);
			byte[] bytes12=TestFiles.FromFoDicom(DicomTransferSyntax.ExplicitVRLittleEndian,1,5,16,12,0,"MONOCHROME1",
				Words(new ushort[] { 0,1024,2048,3072,4095 }));
			TestFiles.AssertReadAndSameAsFoDicom(bytes12,"fo-dicom 12 bit MONOCHROME1");
		}
		#endregion

		#region Sequences
		[TestMethod]
		public void Sequences_OfUndefinedAndDefinedLength_AreSkipped() {
			//An icon image inside a sequence has its own Pixel Data and image attributes; only the top-level ones count.
			SynItem itemIcon=Item(false,
				US(0x0028,0x0002,1),Text(0x0028,0x0004,"CS","MONOCHROME2"),US(0x0028,0x0010,1),US(0x0028,0x0011,2),
				US(0x0028,0x0100,8),US(0x0028,0x0101,8),US(0x0028,0x0102,7),US(0x0028,0x0103,0),Raw(0x7FE0,0x0010,"OB",new byte[] { 9,9 }));
			SynItem itemCode=Item(true,Text(0x0008,0x0100,"SH","T-11170"),Text(0x0008,0x0102,"SH","SRT"),Text(0x0008,0x0104,"LO","Maxilla"));
			SynItem itemNested=Item(true,
				Text(0x0008,0x0100,"SH","12"),
				Sequence(0x0008,0x2228,true,Item(false,Text(0x0008,0x0100,"SH","13")),Item(true,Text(0x0008,0x0100,"SH","14"))));
			List<SynElement> listDataset=Image16(2,3,16,new ushort[] { 100,200,300,400,500,600 });
			listDataset=With(listDataset,Sequence(0x0008,0x2218,true,itemCode,itemNested));//undefined length, undefined-length items
			listDataset=With(listDataset,Sequence(0x0040,0x0555,false));//empty, defined length 0 (Type 2)
			listDataset=With(listDataset,Sequence(0x0040,0x0260,true));//empty, undefined length
			listDataset=With(listDataset,Sequence(0x0088,0x0200,false,itemIcon));//Icon Image Sequence, defined length
			listDataset=With(listDataset,Raw(0x0009,0x1010,"UN",new byte[] { 1,2,3,4 }));//a private element of defined length
			byte[] bytes=MakeFile(listDataset);
			BitmapDicom bitmapDicom=TestFiles.AssertReadAndSameAsFoDicom(bytes,"sequences");
			Assert.AreEqual(3,bitmapDicom.Width);
			Assert.AreEqual(2,bitmapDicom.Height);
			Assert.AreEqual(16,bitmapDicom.BitsAllocated,"The icon's 8 bits do not count.");
			CollectionAssert.AreEqual(new ushort[] { 100,200,300,400,500,600 },bitmapDicom.ArrayDataRaw);
		}

		[TestMethod]
		public void DeeplyNestedSequences_AreDeclined_WithoutStackOverflow() {
			SynElement elementInner=Text(0x0008,0x0100,"SH","X");
			for(int depth=0;depth<200;depth++) {
				elementInner=Sequence(0x0008,0x2218,depth%2==0,Item(depth%3==0,elementInner));
			}
			byte[] bytes=MakeFile(With(Image16(1,1,16,new ushort[] { 7 }),elementInner));
			DicomSubsetReadResult result=Read(bytes);
			Assert.AreEqual(DicomSubsetReadStatus.Malformed,result.Status,result.ToString());
			StringAssert.Contains(result.Reason,"nested");
			//Within the limit it reads.
			SynElement elementShallow=Text(0x0008,0x0100,"SH","X");
			for(int depth=0;depth<DicomSubsetReader.MaxSequenceDepth;depth++) {
				elementShallow=Sequence(0x0008,0x2218,depth%2==0,Item(depth%3==0,elementShallow));
			}
			Assert.AreEqual(DicomSubsetReadStatus.Read,Read(MakeFile(With(Image16(1,1,16,new ushort[] { 7 }),elementShallow))).Status);
		}
		#endregion

		#region Declined, for fo-dicom to read as before
		[TestMethod]
		public void OtherTransferSyntaxes_AreNotTheSubset() {
			foreach(string transferSyntax in new[] { ImplicitVrLittleEndian,ExplicitVrBigEndian,DeflatedExplicitVrLittleEndian,JpegLossless }) {
				DicomSubsetReadResult result=Read(MakeFile(Image16(1,2,16,new ushort[] { 1,2 }),transferSyntax));
				Assert.AreEqual(DicomSubsetReadStatus.NotSubset,result.Status,transferSyntax);
				StringAssert.Contains(result.Reason,transferSyntax);
			}
		}

		[TestMethod]
		public void ImplicitVrFile_GoesToFoDicom_AsBefore() {
			ushort[] arrayStored=Ramp(4*6,4095);
			byte[] bytes=TestFiles.FromFoDicom(DicomTransferSyntax.ImplicitVRLittleEndian,4,6,16,12,0,"MONOCHROME2",Words(arrayStored));
			Assert.AreEqual(DicomSubsetReadStatus.NotSubset,Read(bytes).Status);
			BitmapDicom bitmapDicom;
			using(MemoryStream memoryStream=new MemoryStream(bytes)) {
				bitmapDicom=DicomHelper.GetFromStream(memoryStream);
			}
			BitmapDicom bitmapDicomFoDicom=TestFiles.ReadWithFoDicom(bytes,out string error);
			Assert.IsNull(error);
			TestFiles.AssertSame(bitmapDicomFoDicom,bitmapDicom,"implicit VR through DicomHelper");
			CollectionAssert.AreEqual(arrayStored,bitmapDicom.ArrayDataRaw);
		}

		[TestMethod]
		public void EncapsulatedPixelData_IsNotTheSubset() {
			List<SynElement> listDataset=Image16(1,2,16,new ushort[] { 1,2 });
			listDataset=Without(listDataset,Tag(0x7FE0,0x0010));
			//Basic Offset Table item, one fragment, Sequence Delimitation (PS3.5 A.4).
			byte[] bytesEncapsulated=new byte[] { 0xFE,0xFF,0x00,0xE0,0,0,0,0, 0xFE,0xFF,0x00,0xE0,4,0,0,0,1,2,3,4 }.Concat(Delimiter(0xE0DD)).ToArray();
			listDataset.Add(new SynElement { Tag=Tag(0x7FE0,0x0010),Vr="OB",Value=bytesEncapsulated,LengthOverride=0xFFFFFFFF });
			DicomSubsetReadResult result=Read(MakeFile(listDataset,JpegLossless));
			Assert.AreEqual(DicomSubsetReadStatus.NotSubset,result.Status,result.ToString());
			StringAssert.Contains(result.Reason,JpegLossless);
			//Encapsulated pixel data under the Explicit VR Little Endian UID (not conformant) is still declined, not misread.
			result=Read(MakeFile(listDataset));
			Assert.AreEqual(DicomSubsetReadStatus.NotSubset,result.Status,result.ToString());
			StringAssert.Contains(result.Reason,"encapsulated");
		}

		[TestMethod]
		public void NoPreambleOrPrefix_IsNotTheSubset() {
			byte[] bytes=MakeFile(Image16(1,1,16,new ushort[] { 5 }),hasPreamble:false);
			AssertStatus(DicomSubsetReadStatus.NotSubset,bytes,"no preamble");
			byte[] bytesBadPrefix=MakeFile(Image16(1,1,16,new ushort[] { 5 }));
			bytesBadPrefix[131]=(byte)'X';
			AssertStatus(DicomSubsetReadStatus.NotSubset,bytesBadPrefix,"DICX");
			AssertStatus(DicomSubsetReadStatus.NotSubset,new byte[0],"empty");
			AssertStatus(DicomSubsetReadStatus.NotSubset,new byte[131],"shorter than the preamble");
			byte[] bytesOnlyPrefix=new byte[132];
			"DICM".Select(x => (byte)x).ToArray().CopyTo(bytesOnlyPrefix,128);
			AssertStatus(DicomSubsetReadStatus.NotSubset,bytesOnlyPrefix,"no File Meta Information");
		}

		[TestMethod]
		public void SignedValues_AreDeclined_AndDicomHelperBehavesAsBefore() {
			short[] arraySigned={ -2048,-1,0,1,2047,-100 };
			ushort[] arrayWords=arraySigned.Select(x => unchecked((ushort)x)).ToArray();
			foreach(ushort bitsStored in new ushort[] { 12,16 }) {
				byte[] bytes=MakeFile(ImageDataset(2,3,16,bitsStored,(ushort)(bitsStored-1),1,"MONOCHROME2",Words(arrayWords)));
				DicomSubsetReadResult result=Read(bytes);
				Assert.AreEqual(DicomSubsetReadStatus.Unsupported,result.Status,result.ToString());
				StringAssert.Contains(result.Reason,"signed");
				//DicomHelper hands the file to fo-dicom, which gives what it always gave for signed data (no image).
				BitmapDicom bitmapDicomFoDicom=TestFiles.ReadWithFoDicom(bytes,out string error);
				BitmapDicom bitmapDicomHelper;
				using(MemoryStream memoryStream=new MemoryStream(bytes)) {
					bitmapDicomHelper=DicomHelper.GetFromStream(memoryStream);
				}
				Assert.IsNull(error,error);
				Assert.IsNull(bitmapDicomFoDicom,"fo-dicom's path never showed signed data.");
				Assert.IsNull(bitmapDicomHelper);
			}
		}

		[TestMethod]
		public void ImagesOutsideTheSubset_AreUnsupported() {
			byte[] bytesPixels=Words(new ushort[] { 1,2,3,4 });
			var listCases=new List<(string name,List<SynElement> dataset)> {
				("RGB",ImageDataset(1,4,16,16,15,0,"RGB",bytesPixels,samplesPerPixel:3)),
				("YBR",ImageDataset(1,4,16,16,15,0,"YBR_FULL",bytesPixels)),
				("three samples",ImageDataset(1,4,16,16,15,0,"MONOCHROME2",bytesPixels,samplesPerPixel:3)),
				("two frames",With(ImageDataset(1,2,16,16,15,0,"MONOCHROME2",bytesPixels),Text(0x0028,0x0008,"IS","2"))),
				("14 bits stored",ImageDataset(1,4,16,14,13,0,"MONOCHROME2",bytesPixels)),
				("32 bits allocated",ImageDataset(1,2,32,32,31,0,"MONOCHROME2",bytesPixels)),
				("12 stored in 8",ImageDataset(1,4,8,12,11,0,"MONOCHROME2",bytesPixels)),
				("High Bit not Bits Stored - 1",ImageDataset(1,4,16,12,15,0,"MONOCHROME2",bytesPixels)),
				("no Pixel Data",ImageDataset(1,4,16,16,15,0,"MONOCHROME2",null)),
				("Pixel Data as OF",ImageDataset(1,2,16,16,15,0,"MONOCHROME2",bytesPixels,"OF")),
				("Float Pixel Data",With(ImageDataset(1,4,16,16,15,0,"MONOCHROME2",null),Raw(0x7FE0,0x0008,"OF",bytesPixels))),
				("Rows as SS",With(ImageDataset(1,4,16,16,15,0,"MONOCHROME2",bytesPixels),Raw(0x0028,0x0010,"SS",BitConverter.GetBytes((short)1)))),
			};
			foreach((string name,List<SynElement> dataset) testCase in listCases) {
				AssertStatus(DicomSubsetReadStatus.Unsupported,MakeFile(testCase.dataset),testCase.name);
			}
			//A frame count of 1 is fine.
			Assert.AreEqual(DicomSubsetReadStatus.Read,Read(MakeFile(With(ImageDataset(1,2,16,16,15,0,"MONOCHROME2",Words(new ushort[] { 1,2 })),
				Text(0x0028,0x0008,"IS","1")))).Status);
		}
		#endregion

		#region Malformed files fail cleanly
		[TestMethod]
		public void MalformedFiles_AreDeclinedWithAReason() {
			List<SynElement> listGood=Image16(2,2,16,new ushort[] { 1,2,3,4 });
			Assert.AreEqual(DicomSubsetReadStatus.Read,Read(MakeFile(listGood)).Status,"The base file is fine.");
			List<SynElement> listOutOfOrder=listGood.ToList();
			SynElement elementRows=listOutOfOrder.Single(x => x.Tag==Tag(0x0028,0x0010));
			listOutOfOrder.Remove(elementRows);
			listOutOfOrder.Add(elementRows);
			List<SynElement> listDuplicate=listGood.ToList();
			listDuplicate.Insert(listDuplicate.FindIndex(x => x.Tag==Tag(0x0028,0x0010)),US(0x0028,0x0010,2));
			var listCases=new List<(string name,byte[] bytes)> {
				("no Transfer Syntax UID",MakeFile(listGood,hasTransferSyntax:false)),
				("group length too long",MakeFile(listGood,groupLengthError:2)),
				("group length too short",MakeFile(listGood,groupLengthError:-2)),
				("tags out of order",MakeFile(listOutOfOrder)),
				("a tag twice",MakeFile(listDuplicate)),
				("unknown VR",MakeFile(With(listGood,Raw(0x0008,0x1030,"ZZ",new byte[] { 0x41,0x42 })))),
				("lower-case VR",MakeFile(With(listGood,Raw(0x0008,0x1030,"lo",new byte[] { 0x41,0x42 })))),
				("value past the end",MakeFile(With(listGood,new SynElement { Tag=Tag(0x7FE1,0x0010),Vr="OB",Value=new byte[] { 1,2 },LengthOverride=1000 }))),
				("huge pixel data length",MakeFile(Without(listGood,Tag(0x7FE0,0x0010)).Concat(new[] {
					new SynElement { Tag=Tag(0x7FE0,0x0010),Vr="OW",Value=new byte[8],LengthOverride=0xFFFFFFF0 } }).ToList())),
				("undefined length on UT",MakeFile(With(listGood,new SynElement { Tag=Tag(0x0008,0x1030),Vr="UT",Value=new byte[0],LengthOverride=0xFFFFFFFF }))),
				("item at the top level",MakeFile(listGood).Concat(new byte[] { 0xFE,0xFF,0x00,0xE0,0,0,0,0 }).ToArray()),
				("bytes left over",MakeFile(listGood).Concat(new byte[] { 1,2,3 }).ToArray()),
				("missing Bits Stored",MakeFile(Without(listGood,Tag(0x0028,0x0101)))),
				("missing Pixel Representation",MakeFile(Without(listGood,Tag(0x0028,0x0103)))),
				("missing Photometric Interpretation",MakeFile(Without(listGood,Tag(0x0028,0x0004)))),
				("Rows 0",MakeFile(With(listGood,US(0x0028,0x0010,0)))),
				("Rows as two values",MakeFile(With(listGood,Raw(0x0028,0x0010,"US",new byte[] { 2,0,2,0 })))),
				("Pixel Representation 2",MakeFile(With(listGood,US(0x0028,0x0103,2)))),
				("Number of Frames not a number",MakeFile(With(listGood,Text(0x0028,0x0008,"IS","x1")))),
				("pixel data too short",MakeFile(With(listGood,US(0x0028,0x0011,3)))),
			};
			//65535 x 65535 samples declared, 8 bytes present: refused from the lengths, not by trying to allocate 8 GB.
			listCases.Add(("65535 x 65535 with 8 bytes",MakeFile(With(With(listGood,US(0x0028,0x0010,65535)),US(0x0028,0x0011,65535)))));
			//Reserved bytes of a long VR that are not zero.
			byte[] bytesReserved=MakeFile(listGood);
			int idxPixelVr=FindPixelDataVr(bytesReserved);
			bytesReserved[idxPixelVr+2]=1;
			listCases.Add(("reserved bytes",bytesReserved));
			foreach((string name,byte[] bytes) testCase in listCases) {
				AssertStatus(DicomSubsetReadStatus.Malformed,testCase.bytes,testCase.name);
			}
			StringAssert.Contains(Read(listCases.Single(x => x.name=="65535 x 65535 with 8 bytes").bytes).Reason,"Pixel Data has 8 bytes");
		}

		[TestMethod]
		public void MalformedSequences_AreDeclinedWithAReason() {
			List<SynElement> listGood=Image16(1,2,16,new ushort[] { 1,2 });
			var listCases=new List<(string name,byte[] bytes)>();
			byte[] bytesElementInItem=Encode(new List<SynElement> { Text(0x0008,0x0100,"SH","AB") });
			//An undefined-length sequence that never ends: the file ends after its one item.
			listCases.Add(("no Sequence Delimitation",MakeFile(listGood.Take(3).ToList()).Concat(SeqHeader(0x0008,0x1115,0xFFFFFFFF))
				.Concat(ItemHeader((uint)bytesElementInItem.Length)).Concat(bytesElementInItem).ToArray()));
			//An undefined-length item that never ends.
			listCases.Add(("no Item Delimitation",MakeFile(listGood.Take(3).ToList()).Concat(SeqHeader(0x0008,0x1115,0xFFFFFFFF))
				.Concat(ItemHeader(0xFFFFFFFF)).Concat(bytesElementInItem).ToArray()));
			//An item longer than its sequence.
			listCases.Add(("item past its sequence",BuildWithRawSequence(listGood,SeqHeader(0x0008,0x1115,12).Concat(ItemHeader(40)).Concat(new byte[4]).ToArray())));
			//Something other than an item inside a sequence.
			listCases.Add(("not an item",BuildWithRawSequence(listGood,SeqHeader(0x0008,0x1115,8).Concat(new byte[] { 0x08,0x00,0x00,0x01,0,0,0,0 }).ToArray())));
			//A sequence longer than the file.
			listCases.Add(("sequence past the end",BuildWithRawSequence(listGood,SeqHeader(0x0008,0x1115,100000).ToArray())));
			//A Sequence Delimitation Item with a length.
			listCases.Add(("delimiter with a length",BuildWithRawSequence(listGood,SeqHeader(0x0008,0x1115,0xFFFFFFFF)
				.Concat(new byte[] { 0xFE,0xFF,0xDD,0xE0,4,0,0,0 }).ToArray())));
			foreach((string name,byte[] bytes) testCase in listCases) {
				AssertStatus(DicomSubsetReadStatus.Malformed,testCase.bytes,testCase.name);
			}
			//UN of undefined length (implicit VR inside, PS3.5 6.2.2) is not the subset.
			byte[] bytesUn=BuildWithRawSequence(listGood,new byte[] { 0x09,0x00,0x10,0x10,(byte)'U',(byte)'N',0,0,0xFF,0xFF,0xFF,0xFF }
				.Concat(Delimiter(0xE0DD)).ToArray());
			AssertStatus(DicomSubsetReadStatus.NotSubset,bytesUn,"UN of undefined length");
		}

		[TestMethod]
		public void EveryTruncation_FailsCleanly() {
			ushort[] arrayPixels=Ramp(9*7,65535);
			byte[] bytes=TestFiles.FromCaptureWriter(9,7,arrayPixels,null);
			Assert.AreEqual(DicomSubsetReadStatus.Read,Read(bytes).Status);
			for(int length=0;length<bytes.Length;length++) {
				byte[] bytesCut=bytes.Take(length).ToArray();
				DicomSubsetReadResult result=Read(bytesCut);//never throws
				Assert.AreNotEqual(DicomSubsetReadStatus.Read,result.Status,"Cut to "+length+" of "+bytes.Length+" bytes.");
				Assert.IsNull(result.BitmapDicom);
			}
		}

		[TestMethod]
		public void RandomDamage_NeverThrows_AndAnythingReadIsConsistent() {
			byte[] bytes=TestFiles.FromCaptureWriter(11,5,Ramp(11*5,65535),null);
			Random random=new Random(20261101);
			int countRead=0;
			for(int round=0;round<4000;round++) {
				byte[] bytesDamaged=(byte[])bytes.Clone();
				int countChanges=1+random.Next(4);
				for(int i=0;i<countChanges;i++) {
					int idx=random.Next(bytesDamaged.Length);
					bytesDamaged[idx]=round%3==0 ? (byte)0xFF : (byte)random.Next(256);
				}
				DicomSubsetReadResult result=Read(bytesDamaged);
				if(result.Status==DicomSubsetReadStatus.Read) {
					countRead++;
					BitmapDicom bitmapDicom=result.BitmapDicom;
					Assert.AreEqual(bitmapDicom.Width*bitmapDicom.Height,bitmapDicom.ArrayDataRaw.Length);
					Assert.IsTrue(bitmapDicom.Width>0 && bitmapDicom.Height>0);
					Assert.IsTrue(bitmapDicom.BitsStored==8 || bitmapDicom.BitsStored==12 || bitmapDicom.BitsStored==16);
				}
				else {
					Assert.IsNull(result.BitmapDicom);
					Assert.AreNotEqual("",result.Reason);
				}
			}
			Assert.IsTrue(countRead>0,"Damage to a pixel or a skipped value still reads.");
		}
		#endregion

		#region Streams and files
		[TestMethod]
		public void Streams_NonSeekableAndOffset_AreRead() {
			ushort[] arrayPixels=Ramp(6*4,65535);
			byte[] bytes=TestFiles.FromCaptureWriter(6,4,arrayPixels,null);
			using(NonSeekableStream stream=new NonSeekableStream(bytes)) {
				DicomSubsetReadResult result=DicomSubsetReader.ReadStreamResult(stream);
				Assert.AreEqual(DicomSubsetReadStatus.Read,result.Status,result.ToString());
				CollectionAssert.AreEqual(arrayPixels,result.BitmapDicom.ArrayDataRaw);
			}
			using(NonSeekableStream stream=new NonSeekableStream(bytes)) {
				CollectionAssert.AreEqual(arrayPixels,DicomHelper.GetFromStream(stream).ArrayDataRaw,"Through DicomHelper's chain too.");
			}
			using(MemoryStream memoryStream=new MemoryStream(new byte[] { 1,2,3 }.Concat(bytes).ToArray())) {
				memoryStream.Position=3;
				CollectionAssert.AreEqual(arrayPixels,DicomHelper.GetFromStream(memoryStream).ArrayDataRaw,"Read from the current position.");
			}
		}

		[TestMethod]
		public void LargeFileOutsideTheSubset_OnlyTheHeaderIsRead() {
			//An implicit VR file of about 200 KB: the transfer syntax in the first 64 KB settles it.
			ushort[] arrayStored=Ramp(320*320,4095);
			byte[] bytes=TestFiles.FromFoDicom(DicomTransferSyntax.ImplicitVRLittleEndian,320,320,16,12,0,"MONOCHROME2",Words(arrayStored));
			Assert.IsTrue(bytes.Length>3*64*1024);
			using(CountingStream stream=new CountingStream(bytes)) {
				DicomSubsetReadResult result=DicomSubsetReader.ReadStreamResult(stream);
				Assert.AreEqual(DicomSubsetReadStatus.NotSubset,result.Status);
				Assert.IsTrue(stream.CountRead<=64*1024,"Read "+stream.CountRead+" bytes.");
			}
			using(MemoryStream memoryStream=new MemoryStream(bytes)) {
				CollectionAssert.AreEqual(arrayStored,DicomHelper.GetFromStream(memoryStream).ArrayDataRaw,"fo-dicom reads it after the subset reader declined.");
			}
		}

		[TestMethod]
		public void LargeFileInTheSubset_HeaderThenRest_ReadsEverySample() {
			//About 1.4 MB, so the first 64 KB are only the start: the header check passes and the rest is read after it.
			int width=1001;
			int height=701;
			ushort[] arrayPixels=Ramp(width*height,65535);
			byte[] bytes=TestFiles.FromCaptureWriter(width,height,arrayPixels,null);
			Assert.IsTrue(bytes.Length>10*64*1024);
			string path=Path.Combine(Path.GetTempPath(),"dicomreadertests_large_"+Guid.NewGuid().ToString("N")+".dcm");
			try {
				System.IO.File.WriteAllBytes(path,bytes);
				DicomSubsetReadResult resultFile=DicomSubsetReader.ReadFileResult(path);
				Assert.AreEqual(DicomSubsetReadStatus.Read,resultFile.Status,resultFile.ToString());
				CollectionAssert.AreEqual(arrayPixels,resultFile.BitmapDicom.ArrayDataRaw);
			}
			finally {
				System.IO.File.Delete(path);
			}
			using(CountingStream stream=new CountingStream(bytes)) {
				DicomSubsetReadResult resultStream=DicomSubsetReader.ReadStreamResult(stream);
				Assert.AreEqual(DicomSubsetReadStatus.Read,resultStream.Status,resultStream.ToString());
				Assert.AreEqual(bytes.Length,stream.CountRead,"Each byte is read once.");
				CollectionAssert.AreEqual(arrayPixels,resultStream.BitmapDicom.ArrayDataRaw);
			}
			using(NonSeekableStream stream=new NonSeekableStream(bytes)) {
				CollectionAssert.AreEqual(arrayPixels,DicomSubsetReader.ReadStreamResult(stream).BitmapDicom.ArrayDataRaw);
			}
		}

		[TestMethod]
		public void StreamLongerThanTheLimit_IsDeclinedWithoutReading() {
			using(HugeStream stream=new HugeStream()) {
				DicomSubsetReadResult result=DicomSubsetReader.ReadStreamResult(stream);
				Assert.AreEqual(DicomSubsetReadStatus.NotSubset,result.Status);
				StringAssert.Contains(result.Reason,"larger than");
				Assert.AreEqual(0,stream.CountRead);
			}
		}

		[TestMethod]
		public void MissingOrUnreadableInput_IsAFileError_NotAnException() {
			string pathMissing=Path.Combine(Path.GetTempPath(),"dicomreadertests_missing_"+Guid.NewGuid().ToString("N")+".dcm");
			Assert.AreEqual(DicomSubsetReadStatus.FileError,DicomSubsetReader.ReadFileResult(pathMissing).Status);
			Assert.AreEqual(DicomSubsetReadStatus.FileError,DicomSubsetReader.ReadFileResult(null).Status);
			Assert.AreEqual(DicomSubsetReadStatus.FileError,DicomSubsetReader.ReadFileResult("").Status);
			Assert.AreEqual(DicomSubsetReadStatus.FileError,DicomSubsetReader.ReadStreamResult(null).Status);
			Assert.AreEqual(DicomSubsetReadStatus.FileError,DicomSubsetReader.Read(null).Status);
			Assert.IsNull(DicomHelper.GetFromFile(pathMissing),"DicomHelper still returns null for a missing file.");
			using(ThrowingStream stream=new ThrowingStream()) {
				Assert.AreEqual(DicomSubsetReadStatus.FileError,DicomSubsetReader.ReadStreamResult(stream).Status);
			}
		}
		#endregion

		#region Helpers
		///<summary>The index of the VR of the top-level Pixel Data element (the last element in the test files).</summary>
		private static int FindPixelDataVr(byte[] bytes) {
			for(int i=bytes.Length-12;i>=132;i--) {
				if(bytes[i]==0xE0 && bytes[i+1]==0x7F && bytes[i+2]==0x10 && bytes[i+3]==0x00 && bytes[i+4]=='O' && bytes[i+5]=='W') {
					return i+4;
				}
			}
			Assert.Fail("No Pixel Data.");
			return -1;
		}

		private static IEnumerable<byte> SeqHeader(ushort group,ushort element,uint length) {
			return BitConverter.GetBytes(group).Concat(BitConverter.GetBytes(element)).Concat(new byte[] { (byte)'S',(byte)'Q',0,0 })
				.Concat(BitConverter.GetBytes(length));
		}

		private static IEnumerable<byte> ItemHeader(uint length) {
			return new byte[] { 0xFE,0xFF,0x00,0xE0 }.Concat(BitConverter.GetBytes(length));
		}

		///<summary>The file with raw sequence bytes placed in tag order, at (0008,1115), before the Image Pixel module.</summary>
		private static byte[] BuildWithRawSequence(List<SynElement> listDataset,byte[] bytesSequence) {
			List<SynElement> listBefore=listDataset.Where(x => x.Tag<Tag(0x0008,0x1115)).ToList();
			List<SynElement> listAfter=listDataset.Where(x => x.Tag>Tag(0x0008,0x1115)).ToList();
			byte[] bytesFile=MakeFile(listBefore);
			return bytesFile.Concat(bytesSequence).Concat(Encode(listAfter)).ToArray();
		}

		private sealed class NonSeekableStream : MemoryStream {
			public NonSeekableStream(byte[] bytes) : base(bytes) {
			}

			public override bool CanSeek => false;

			public override long Length => throw new NotSupportedException();

			public override long Position {
				get => throw new NotSupportedException();
				set => throw new NotSupportedException();
			}

			public override long Seek(long offset,SeekOrigin loc) {
				throw new NotSupportedException();
			}
		}

		private sealed class CountingStream : MemoryStream {
			public long CountRead;

			public CountingStream(byte[] bytes) : base(bytes) {
			}

			public override int Read(byte[] buffer,int offset,int count) {
				int countRead=base.Read(buffer,offset,count);
				CountRead+=countRead;
				return countRead;
			}
		}

		///<summary>Claims 2 GB without holding them.</summary>
		private sealed class HugeStream : Stream {
			public long CountRead;
			public override bool CanRead => true;
			public override bool CanSeek => true;
			public override bool CanWrite => false;
			public override long Length => 2L*1024*1024*1024;
			public override long Position { get; set; }

			public override int Read(byte[] buffer,int offset,int count) {
				CountRead+=count;
				return count;
			}

			public override void Flush() {
			}

			public override long Seek(long offset,SeekOrigin origin) {
				throw new NotSupportedException();
			}

			public override void SetLength(long value) {
				throw new NotSupportedException();
			}

			public override void Write(byte[] buffer,int offset,int count) {
				throw new NotSupportedException();
			}
		}

		private sealed class ThrowingStream : MemoryStream {
			public ThrowingStream() : base(new byte[1000]) {
			}

			public override int Read(byte[] buffer,int offset,int count) {
				throw new IOException("The network share went away.");
			}
		}
		#endregion
	}
}
