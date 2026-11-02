// The full reader's hardening (DicomImageReader): truncated files, lengths past the end, huge declared lengths, deep sequences, broken
// encapsulation, RLE and JPEG frames, and random damage are declined with a reason and never throw. Every file is synthetic.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using static DicomReaderTests.SyntheticDicom;

namespace DicomReaderTests {
	[TestClass]
	public class DicomImageReaderHardeningTests {
		private static DicomImageReadResult Read(byte[] bytes) {
			return DicomHelper.FullReader.Read(bytes);
		}

		private static void AssertDeclined(DicomImageReadStatus status,byte[] bytes,string message) {
			DicomImageReadResult result=Read(bytes);
			Assert.AreEqual(status,result.Status,message+": "+result);
			Assert.IsNull(result.BitmapDicom,message);
			Assert.AreNotEqual("",result.Reason,message+": a declined file has a reason.");
			using(DicomImageReaderTests.NonSeekableStream stream=new DicomImageReaderTests.NonSeekableStream(bytes)) {
				DicomImageReadResult resultStream=DicomHelper.FullReader.ReadStreamResult(stream);
				Assert.AreNotEqual(DicomImageReadStatus.Read,resultStream.Status,message+" (not seekable)");
			}
		}

		private static readonly string[] _arrayBaseCases={ "enc-implicit-le-12","enc-explicit-be-12","enc-explicit-be-8-ow","enc-deflate-12",
			"enc-explicit-le-sequences-12","enc-implicit-le-sequences-12","enc-explicit-be-sequences-12","enc-explicit-le-un-undefined-12",
			"enc-dataset-only-explicit-be-12","enc-no-preamble-meta-implicit-12","enc-ts-explicit-dataset-implicit-12","enc-explicit-le-vr-spaces-pixel-data-12",
			"enc-meta-implicit-no-preamble-12","enc-dataset-only-implicit-private-first-12","img-palette-8-lut16","img-ybr-full-422","img-rgb-planar1",
			"rle-mono2-16","rle-rgb","jpeg-gray","jpeg-ybr-full-422","jpeg-gray-two-fragments","jpeg-progressive-gray","encapsulated-uncompressed" };

		#region Truncation and lengths
		[TestMethod]
		public void EveryTruncation_FailsCleanly() {
			foreach(string caseName in _arrayBaseCases) {
				byte[] bytes=ReaderCases.Get(caseName).Bytes;
				DicomImageReadResult resultWhole=Read(bytes);
				Assert.AreEqual(DicomImageReadStatus.Read,resultWhole.Status,caseName);
				string fingerprintWhole=FoDicomBaseline.Fingerprint(resultWhole.BitmapDicom);
				for(int length=0;length<bytes.Length;length++) {
					DicomImageReadResult result=Read(bytes.Take(length).ToArray());//never throws
					if(result.Status==DicomImageReadStatus.Read) {
						//Only a deflated data set cut inside its last deflate block can still hold every byte of the image.
						Assert.AreEqual("enc-deflate-12",caseName,"Cut to "+length+" of "+bytes.Length+" bytes.");
						Assert.AreEqual(fingerprintWhole,FoDicomBaseline.Fingerprint(result.BitmapDicom));
						continue;
					}
					Assert.IsNull(result.BitmapDicom);
					Assert.AreNotEqual("",result.Reason,caseName+" cut to "+length);
				}
			}
		}

		[TestMethod]
		public void LengthsPastTheEnd_AreDeclined_WithoutReadingOrAllocating() {
			List<SynElement> listGood=ReaderCases.Twelve().ToDataset();
			foreach(string transferSyntax in new[] { ExplicitVrLittleEndian,ImplicitVrLittleEndian,ExplicitVrBigEndian,DeflatedExplicitVrLittleEndian }) {
				//A skipped value past the end.
				byte[] bytesSkipped=MakeFileIn(With(listGood,new SynElement { Tag=Tag(0x0009,0x1010),Vr="OB",Value=new byte[] { 1,2 },LengthOverride=0xFFFFFFF0 }),
					transferSyntax);
				AssertDeclined(DicomImageReadStatus.Malformed,bytesSkipped,transferSyntax+": huge skipped value");
				//Pixel Data longer than the file.
				List<SynElement> listHugePixels=Without(listGood,Tag(0x7FE0,0x0010));
				listHugePixels.Add(new SynElement { Tag=Tag(0x7FE0,0x0010),Vr="OW",Value=SyntheticDicom.Words(ReaderCases.Words(35,4095)),LengthOverride=0xFFFFFFF0 });
				AssertDeclined(DicomImageReadStatus.Malformed,MakeFileIn(listHugePixels,transferSyntax),transferSyntax+": huge pixel data");
				List<SynElement> listLonger=Without(listGood,Tag(0x7FE0,0x0010));
				listLonger.Add(new SynElement { Tag=Tag(0x7FE0,0x0010),Vr="OW",Value=SyntheticDicom.Words(ReaderCases.Words(35,4095)),LengthOverride=72 });
				AssertDeclined(DicomImageReadStatus.Malformed,MakeFileIn(listLonger,transferSyntax),transferSyntax+": pixel data 2 bytes past the end");
				//Pixel Data shorter than one frame.
				listLonger=Without(listGood,Tag(0x7FE0,0x0010));
				listLonger.Add(Raw(0x7FE0,0x0010,"OW",new byte[68]));
				DicomImageReadResult result=Read(MakeFileIn(listLonger,transferSyntax));
				Assert.AreEqual(DicomImageReadStatus.Malformed,result.Status,transferSyntax);
				StringAssert.Contains(result.Reason,"needs");
			}
			//The huge skipped value is refused from the stream's length: almost nothing is read.
			byte[] bytes=MakeFileIn(With(listGood,new SynElement { Tag=Tag(0x0009,0x1010),Vr="OB",Value=new byte[] { 1,2 },LengthOverride=0xFFFFFFF0 }),
				ExplicitVrLittleEndian);
			using(DicomImageReaderTests.CountingStream stream=new DicomImageReaderTests.CountingStream(bytes)) {
				Assert.AreEqual(DicomImageReadStatus.Malformed,DicomHelper.FullReader.ReadStreamResult(stream).Status);
				Assert.IsTrue(stream.CountRead<=bytes.Length);
			}
		}

		[TestMethod]
		public void BrokenElements_AreDeclinedWithAReason() {
			List<SynElement> listGood=ReaderCases.Twelve().ToDataset();
			Assert.AreEqual(DicomImageReadStatus.Read,Read(MakeFileIn(listGood,ExplicitVrLittleEndian)).Status);
			var listCases=new List<(string name,byte[] bytes,DicomImageReadStatus status)> {
				("unknown VR",MakeFileIn(With(listGood,Raw(0x0008,0x1030,"ZZ",new byte[] { 0x41,0x42 })),ExplicitVrLittleEndian),DicomImageReadStatus.Malformed),
				("lower-case VR",MakeFileIn(With(listGood,Raw(0x0008,0x1030,"lo",new byte[] { 0x41,0x42 })),ExplicitVrLittleEndian),DicomImageReadStatus.Malformed),
				("undefined length on UT",MakeFileIn(With(listGood,new SynElement { Tag=Tag(0x0008,0x1030),Vr="UT",Value=new byte[0],LengthOverride=0xFFFFFFFF }),
					ExplicitVrLittleEndian),DicomImageReadStatus.Malformed),
				("item at the top level",MakeFileIn(listGood.Take(3).ToList(),ExplicitVrLittleEndian).Concat(new byte[] { 0xFE,0xFF,0x00,0xE0,0,0,0,0 })
					.Concat(EncodeAs(listGood.Skip(3).ToList(),SynEncoding.ExplicitLittle)).ToArray(),DicomImageReadStatus.Malformed),
				("bytes left over",MakeFileIn(Without(listGood,Tag(0x7FE0,0x0010)),ExplicitVrLittleEndian).Concat(new byte[] { 1,2,3 }).ToArray(),
					DicomImageReadStatus.Malformed),
				("Rows as one byte",MakeFileIn(With(listGood,Raw(0x0028,0x0010,"US",new byte[] { 5 })),ExplicitVrLittleEndian),DicomImageReadStatus.Malformed),
				("Rows 300 KB long",MakeFileIn(With(listGood,Raw(0x0028,0x0010,"OB",new byte[300*1024])),ExplicitVrLittleEndian),DicomImageReadStatus.Malformed),
				("Rows 0",MakeFileIn(With(listGood,US(0x0028,0x0010,0)),ExplicitVrLittleEndian),DicomImageReadStatus.Malformed),
				("missing Rows",MakeFileIn(Without(listGood,Tag(0x0028,0x0010)),ExplicitVrLittleEndian),DicomImageReadStatus.Malformed),
				("missing Bits Stored",MakeFileIn(Without(listGood,Tag(0x0028,0x0101)),ExplicitVrLittleEndian),DicomImageReadStatus.Malformed),
				("missing Photometric Interpretation",MakeFileIn(Without(listGood,Tag(0x0028,0x0004)),ExplicitVrLittleEndian),DicomImageReadStatus.Malformed),
				("Bits Stored above Bits Allocated",MakeFileIn(With(listGood,US(0x0028,0x0101,17)),ExplicitVrLittleEndian),DicomImageReadStatus.Malformed),
				("High Bit below Bits Stored - 1",MakeFileIn(With(listGood,US(0x0028,0x0102,3)),ExplicitVrLittleEndian),DicomImageReadStatus.Malformed),
				("High Bit at Bits Allocated",MakeFileIn(With(listGood,US(0x0028,0x0102,16)),ExplicitVrLittleEndian),DicomImageReadStatus.Malformed),
				("Pixel Representation 2",MakeFileIn(With(listGood,US(0x0028,0x0103,2)),ExplicitVrLittleEndian),DicomImageReadStatus.Malformed),
				("Number of Frames 0",MakeFileIn(With(listGood,Text(0x0028,0x0008,"IS","0")),ExplicitVrLittleEndian),DicomImageReadStatus.Malformed),
				("Number of Frames not a number",MakeFileIn(With(listGood,Text(0x0028,0x0008,"IS","x1")),ExplicitVrLittleEndian),DicomImageReadStatus.Malformed),
				("three samples of MONOCHROME2",MakeFileIn(With(listGood,US(0x0028,0x0002,3)),ExplicitVrLittleEndian),DicomImageReadStatus.Malformed),
				("Planar Configuration 2",MakeFileIn(With(ReaderCases.Color("RGB",3,4,ReaderCases.Color8(12),0).ToDataset(),US(0x0028,0x0006,2)),
					ExplicitVrLittleEndian),DicomImageReadStatus.Malformed),
				("YBR_FULL_422 odd columns",MakeFileIn(ReaderCases.Color("YBR_FULL_422",2,3,new byte[12],0),ExplicitVrLittleEndian),DicomImageReadStatus.Malformed),
				("palette without tables",MakeFileIn(ReaderCases.Mono8(3,5,8,7,ReaderCases.Bytes(15,255),"PALETTE COLOR"),ExplicitVrLittleEndian),
					DicomImageReadStatus.Malformed),
				("no Pixel Data",MakeFileIn(Without(listGood,Tag(0x7FE0,0x0010)),ExplicitVrLittleEndian),DicomImageReadStatus.Unsupported),
				("Float Pixel Data",MakeFileIn(With(Without(listGood,Tag(0x7FE0,0x0010)),Raw(0x7FE0,0x0008,"OF",new byte[140])),ExplicitVrLittleEndian),
					DicomImageReadStatus.Unsupported),
				("32 bits allocated",MakeFileIn(ReaderCases.Mono16(1,2,16,15,new ushort[4]).Clone().ToDataset().Select(x => x.Tag==Tag(0x0028,0x0100)
					? US(0x0028,0x0100,32) : x).ToList(),ExplicitVrLittleEndian),DicomImageReadStatus.Unsupported),
				("1 bit allocated",MakeFileIn(With(listGood,US(0x0028,0x0100,1)),ExplicitVrLittleEndian),DicomImageReadStatus.Unsupported),
				("YBR_PARTIAL_420",MakeFileIn(ReaderCases.Color("YBR_PARTIAL_420",3,4,ReaderCases.Color8(12),0),ExplicitVrLittleEndian),DicomImageReadStatus.Unsupported),
				("CMYK",MakeFileIn(ReaderCases.Color("CMYK",3,4,ReaderCases.Color8(12),0),ExplicitVrLittleEndian),DicomImageReadStatus.Unsupported),
				("signed RGB",MakeFileIn(With(ReaderCases.Color("RGB",3,4,ReaderCases.Color8(12),0).ToDataset(),US(0x0028,0x0103,1)),ExplicitVrLittleEndian),
					DicomImageReadStatus.Unsupported),
				("encapsulated under Explicit VR Little Endian",MakeFileIn(ReaderCases.Encapsulated8("MONOCHROME2",6,10,1,Encapsulated(null,new byte[60])),
					ExplicitVrLittleEndian),DicomImageReadStatus.Malformed),
				("JPEG Baseline with native pixel data",MakeFileIn(ReaderCases.Eight(),JpegBaseline),DicomImageReadStatus.Malformed),
			};
			//A segmented palette (PS3.3 C.7.9) is not supported.
			SynImage synImageSegmented=ReaderCases.Mono8(3,5,8,7,ReaderCases.Bytes(15,255),"PALETTE COLOR");
			for(ushort color=0;color<3;color++) {
				synImageSegmented.ListExtra.Add(Raw(0x0028,(ushort)(0x1101+color),"US",SyntheticDicom.Words(new ushort[] { 256,0,16 })));
				synImageSegmented.ListExtra.Add(Raw(0x0028,(ushort)(0x1221+color),"OW",new byte[] { 0,0,1,0,0,0 }));
			}
			listCases.Add(("segmented palette",MakeFileIn(synImageSegmented,ExplicitVrLittleEndian),DicomImageReadStatus.Unsupported));
			foreach((string name,byte[] bytes,DicomImageReadStatus status) testCase in listCases) {
				AssertDeclined(testCase.status,testCase.bytes,testCase.name);
			}
		}

		[TestMethod]
		public void OddLengths_AreSkippedByTheirLength() {
			//PS3.5 7.1.1 asks for even lengths; a writer that breaks it still tells where the next element starts.
			foreach(string transferSyntax in new[] { ExplicitVrLittleEndian,ImplicitVrLittleEndian,ExplicitVrBigEndian,DeflatedExplicitVrLittleEndian }) {
				List<SynElement> listDataset=With(ReaderCases.Twelve().ToDataset(),Raw(0x0008,0x1030,"LO",new byte[] { (byte)'A',(byte)'B',(byte)'C' }));
				DicomImageReadResult result=Read(MakeFileIn(listDataset,transferSyntax));
				Assert.AreEqual(DicomImageReadStatus.Read,result.Status,transferSyntax+": "+result);
				CollectionAssert.AreEqual(ReaderCases.Words(35,4095),result.BitmapDicom.ArrayDataRaw);
			}
		}
		#endregion Truncation and lengths

		#region Sequences
		[TestMethod]
		public void DeeplyNestedSequences_AreDeclined_WithoutStackOverflow() {
			foreach(string transferSyntax in new[] { ExplicitVrLittleEndian,ImplicitVrLittleEndian,ExplicitVrBigEndian,DeflatedExplicitVrLittleEndian }) {
				SynElement elementDeep=Text(0x0008,0x0100,"SH","X");
				for(int depth=0;depth<500;depth++) {
					elementDeep=Sequence(0x0008,0x2218,true,Item(true,elementDeep));
				}
				DicomImageReadResult result=Read(MakeFileIn(With(ReaderCases.Twelve().ToDataset(),elementDeep),transferSyntax));
				Assert.AreEqual(DicomImageReadStatus.Malformed,result.Status,transferSyntax+": "+result);
				StringAssert.Contains(result.Reason,"nested");
				SynElement elementShallow=Text(0x0008,0x0100,"SH","X");
				for(int depth=0;depth<DicomImageReader.MaxSequenceDepth;depth++) {
					elementShallow=Sequence(0x0008,0x2218,true,Item(true,elementShallow));
				}
				Assert.AreEqual(DicomImageReadStatus.Read,Read(MakeFileIn(With(ReaderCases.Twelve().ToDataset(),elementShallow),transferSyntax)).Status,transferSyntax);
				//Nesting of defined length is skipped by its length, so its depth costs nothing.
				SynElement elementDefined=Text(0x0008,0x0100,"SH","X");
				for(int depth=0;depth<500;depth++) {
					elementDefined=Sequence(0x0008,0x2218,false,Item(false,elementDefined));
				}
				Assert.AreEqual(DicomImageReadStatus.Read,Read(MakeFileIn(With(ReaderCases.Twelve().ToDataset(),elementDefined),transferSyntax)).Status,transferSyntax);
			}
		}

		[TestMethod]
		public void BrokenSequences_AreDeclinedWithAReason() {
			List<SynElement> listGood=ReaderCases.Twelve().ToDataset();
			byte[] bytesHead=MakeFileIn(listGood.Where(x => x.Tag<Tag(0x0008,0x1115)).ToList(),ExplicitVrLittleEndian);
			byte[] bytesTail=EncodeAs(listGood.Where(x => x.Tag>Tag(0x0008,0x1115)).ToList(),SynEncoding.ExplicitLittle);
			byte[] bytesElement=EncodeAs(new List<SynElement> { Text(0x0008,0x0100,"SH","AB") },SynEncoding.ExplicitLittle);
			byte[] bytesSequenceUndefined={ 0x08,0x00,0x15,0x11,(byte)'S',(byte)'Q',0,0,0xFF,0xFF,0xFF,0xFF };
			var listCases=new List<(string name,byte[] bytes)> {
				("no Sequence Delimitation",bytesHead.Concat(bytesSequenceUndefined).Concat(ItemHeaderLE((uint)bytesElement.Length)).Concat(bytesElement).ToArray()),
				("no Item Delimitation",bytesHead.Concat(bytesSequenceUndefined).Concat(ItemHeaderLE(0xFFFFFFFF)).Concat(bytesElement).ToArray()),
				("not an item",bytesHead.Concat(bytesSequenceUndefined).Concat(bytesElement).Concat(Delimiter(0xE0DD)).Concat(bytesTail).ToArray()),
				("item past the end",bytesHead.Concat(bytesSequenceUndefined).Concat(ItemHeaderLE(100000)).Concat(bytesElement).Concat(bytesTail).ToArray()),
				("sequence of defined length past the end",bytesHead.Concat(new byte[] { 0x08,0x00,0x15,0x11,(byte)'S',(byte)'Q',0,0 })
					.Concat(BitConverter.GetBytes(100000)).Concat(bytesTail).ToArray()),
				("Item Delimitation at the top level",bytesHead.Concat(Delimiter(0xE00D)).Concat(bytesTail).ToArray()),
				("Sequence Delimitation inside an item",bytesHead.Concat(bytesSequenceUndefined).Concat(ItemHeaderLE(0xFFFFFFFF)).Concat(Delimiter(0xE0DD))
					.Concat(bytesTail).ToArray()),
			};
			foreach((string name,byte[] bytes) testCase in listCases) {
				AssertDeclined(DicomImageReadStatus.Malformed,testCase.bytes,testCase.name);
			}
		}
		#endregion Sequences

		#region Encapsulation, RLE and JPEG
		private static byte[] RleFile(byte[] bytesFrame) {
			SynImage synImage=ReaderCases.Mono16(6,10,16,15,new ushort[60]);
			synImage.PixelElement=Encapsulated(null,bytesFrame);
			return MakeFileIn(synImage,RleLossless);
		}

		[TestMethod]
		public void BrokenEncapsulation_IsDeclined() {
			byte[] bytesFrame=RleFrame(SyntheticDicom.Words(ReaderCases.Words(60,65535)),6,10,1,2,0);
			SynImage synImage=ReaderCases.Mono16(6,10,16,15,new ushort[60]);
			Func<byte[],byte[]> makeFile=bytesValue => {
				SynImage synImageCopy=synImage.Clone();
				synImageCopy.PixelElement=new SynElement { Tag=Tag(0x7FE0,0x0010),Vr="OB",Value=bytesValue,LengthOverride=0xFFFFFFFF };
				return MakeFileIn(synImageCopy,RleLossless);
			};
			byte[] bytesFragment=ItemHeaderLE((uint)bytesFrame.Length).Concat(bytesFrame).ToArray();
			Assert.AreEqual(DicomImageReadStatus.Read,Read(makeFile(ItemHeaderLE(0).Concat(bytesFragment).Concat(Delimiter(0xE0DD)).ToArray())).Status);
			var listCases=new List<(string name,byte[] bytes)> {
				("no Basic Offset Table item",makeFile(bytesFragment.Skip(8).ToArray())),
				("Basic Offset Table of undefined length",makeFile(ItemHeaderLE(0xFFFFFFFF).Concat(bytesFragment).Concat(Delimiter(0xE0DD)).ToArray())),
				("Basic Offset Table length 3",makeFile(ItemHeaderLE(3).Concat(new byte[3]).Concat(bytesFragment).Concat(Delimiter(0xE0DD)).ToArray())),
				("Basic Offset Table past the end",makeFile(ItemHeaderLE(0x7FFFFFF0).Concat(bytesFragment).ToArray())),
				("fragment past the end",makeFile(ItemHeaderLE(0).Concat(ItemHeaderLE(0x7FFFFFF0)).Concat(bytesFrame).ToArray())),
				("fragment of undefined length",makeFile(ItemHeaderLE(0).Concat(ItemHeaderLE(0xFFFFFFFF)).Concat(bytesFrame).Concat(Delimiter(0xE0DD)).ToArray())),
				("not an item among the fragments",makeFile(ItemHeaderLE(0).Concat(new byte[] { 0x08,0x00,0x10,0x00,4,0,0,0,1,2,3,4 }).Concat(bytesFragment)
					.Concat(Delimiter(0xE0DD)).ToArray())),
				("no fragment",makeFile(ItemHeaderLE(0).Concat(Delimiter(0xE0DD)).ToArray())),
				("no Sequence Delimitation Item",makeFile(ItemHeaderLE(0).Concat(bytesFragment).ToArray())),
			};
			//Two frames whose Basic Offset Table puts the second at the first one's offset.
			synImage.NumberOfFrames="2";
			listCases.Add(("second frame before the first",makeFile(ItemHeaderLE(8).Concat(BitConverter.GetBytes(0)).Concat(BitConverter.GetBytes(0))
				.Concat(bytesFragment).Concat(bytesFragment).Concat(Delimiter(0xE0DD)).ToArray())));
			foreach((string name,byte[] bytes) testCase in listCases) {
				AssertDeclined(DicomImageReadStatus.Malformed,testCase.bytes,testCase.name);
			}
		}

		[TestMethod]
		public void BrokenRleFrames_AreDeclined() {
			byte[] bytesGood=RleFrame(SyntheticDicom.Words(ReaderCases.Words(60,65535)),6,10,1,2,0);
			Assert.AreEqual(DicomImageReadStatus.Read,Read(RleFile(bytesGood)).Status);
			byte[] bytesCountWrong=(byte[])bytesGood.Clone();
			bytesCountWrong[0]=3;
			byte[] bytesOffsetOut=(byte[])bytesGood.Clone();
			BitConverter.GetBytes(100000).CopyTo(bytesOffsetOut,8);
			byte[] bytesOffsetInHeader=(byte[])bytesGood.Clone();
			BitConverter.GetBytes(10).CopyTo(bytesOffsetInHeader,4);
			byte[] bytesOutOfOrder=(byte[])bytesGood.Clone();
			BitConverter.GetBytes(BitConverter.ToUInt32(bytesGood,8)).CopyTo(bytesOutOfOrder,4);
			BitConverter.GetBytes(BitConverter.ToUInt32(bytesGood,4)).CopyTo(bytesOutOfOrder,8);
			var listCases=new List<(string name,byte[] frame)> {
				("shorter than the header",bytesGood.Take(40).ToArray()),
				("three segments listed",bytesCountWrong),
				("an offset past the frame",bytesOffsetOut),
				("an offset inside the header",bytesOffsetInHeader),
				("offsets out of order",bytesOutOfOrder),
				("a short last segment",bytesGood.Take(bytesGood.Length-20).ToArray()),
				("only the header",bytesGood.Take(64).ToArray()),
			};
			foreach((string name,byte[] frame) testCase in listCases) {
				byte[] bytesFrame=testCase.frame.Length%2==1 ? testCase.frame.Concat(new byte[1]).ToArray() : testCase.frame;
				DicomImageReadResult result=Read(RleFile(bytesFrame));
				Assert.AreEqual(DicomImageReadStatus.Malformed,result.Status,testCase.name+": "+result);
				StringAssert.Contains(result.Reason,"RLE",testCase.name);
			}
			//Runs of -128 are no-ops; a literal run past the pixel count is cut off (pad bytes).
			byte[] bytesNoOp=BuildRleSegments(new byte[][] { new byte[] { 0x80,0x80,0xFB,7,0x80 },new byte[] { 0x05,1,2,3,4,5,6,0x80 } });
			SynImage synImageNoOp=ReaderCases.Mono16(1,6,16,15,new ushort[6]);
			synImageNoOp.PixelElement=Encapsulated(null,bytesNoOp);
			DicomImageReadResult resultNoOp=Read(MakeFileIn(synImageNoOp,RleLossless));
			Assert.AreEqual(DicomImageReadStatus.Read,resultNoOp.Status,resultNoOp.ToString());
			CollectionAssert.AreEqual(new ushort[] { 0x0701,0x0702,0x0703,0x0704,0x0705,0x0706 },resultNoOp.BitmapDicom.ArrayDataRaw);
		}

		///<summary>An RLE frame of the given raw segments.</summary>
		private static byte[] BuildRleSegments(byte[][] arraySegments) {
			byte[] bytesHeader=new byte[64];
			BitConverter.GetBytes((uint)arraySegments.Length).CopyTo(bytesHeader,0);
			uint offset=64;
			for(int i=0;i<arraySegments.Length;i++) {
				BitConverter.GetBytes(offset).CopyTo(bytesHeader,4+4*i);
				offset+=(uint)arraySegments[i].Length;
			}
			return bytesHeader.Concat(arraySegments.SelectMany(x => x)).ToArray();
		}

		[TestMethod]
		public void BrokenJpegFrames_AreDeclined() {
			byte[] bytesJpeg=ReaderCases.JpegGrayFixed();
			Func<byte[],int,int,ushort,string,byte[]> makeFile=(bytesFrame,rows,columns,samplesPerPixel,photometric) =>
				MakeFileIn(ReaderCases.Encapsulated8(photometric,rows,columns,samplesPerPixel,Encapsulated(null,bytesFrame)),JpegBaseline);
			Assert.AreEqual(DicomImageReadStatus.Read,Read(makeFile(bytesJpeg,13,21,1,"MONOCHROME2")).Status);
			byte[] bytesPrecision12=(byte[])bytesJpeg.Clone();
			int idxSof=FindMarker(bytesJpeg,0xC0);
			bytesPrecision12[idxSof+4]=12;
			byte[] bytesLossless=(byte[])bytesJpeg.Clone();
			bytesLossless[idxSof+1]=0xC3;
			var listCases=new List<(string name,byte[] bytes)> {
				("not a JPEG stream",makeFile(new byte[] { 1,2,3,4,5,6 },13,21,1,"MONOCHROME2")),
				("cut off",makeFile(bytesJpeg.Take(bytesJpeg.Length-40).ToArray(),13,21,1,"MONOCHROME2")),
				("no frame header",makeFile(new byte[] { 0xFF,0xD8,0xFF,0xD9 },13,21,1,"MONOCHROME2")),
				("12-bit precision",makeFile(bytesPrecision12,13,21,1,"MONOCHROME2")),
				("lossless process",makeFile(bytesLossless,13,21,1,"MONOCHROME2")),
				("other size than Rows and Columns",makeFile(bytesJpeg,14,21,1,"MONOCHROME2")),
				("one component, three samples",makeFile(bytesJpeg,13,21,3,"YBR_FULL_422")),
				("scrambled entropy data",makeFile(bytesJpeg.Take(bytesJpeg.Length-60).Concat(Enumerable.Repeat((byte)0xFF,58)).Concat(new byte[] { 0xFF,0xD9 }).ToArray(),
					13,21,1,"MONOCHROME2")),
			};
			foreach((string name,byte[] bytes) testCase in listCases) {
				DicomImageReadResult result=Read(testCase.bytes);
				if(testCase.name=="scrambled entropy data" && result.Status==DicomImageReadStatus.Read) {
					continue;//a decoder may show damaged entropy data as it can; it must not throw
				}
				Assert.AreEqual(DicomImageReadStatus.Malformed,result.Status,testCase.name+": "+result);
				Assert.AreNotEqual("",result.Reason,testCase.name);
			}
			//A palette color JPEG is not supported.
			Assert.AreEqual(DicomImageReadStatus.Unsupported,Read(makeFile(bytesJpeg,13,21,1,"PALETTE COLOR")).Status);
		}

		private static int FindMarker(byte[] bytes,byte marker) {
			for(int i=0;i+1<bytes.Length;i++) {
				if(bytes[i]==0xFF && bytes[i+1]==marker) {
					return i;
				}
			}
			Assert.Fail("No marker "+marker);
			return -1;
		}
		#endregion Encapsulation, RLE and JPEG

		#region Work limits
		private static byte[] Repeat(byte[] bytesUnit,int count) {
			byte[] bytes=new byte[(long)bytesUnit.Length*count];
			for(int i=0;i<count;i++) {
				Buffer.BlockCopy(bytesUnit,0,bytes,i*bytesUnit.Length,bytesUnit.Length);
			}
			return bytes;
		}

		private static byte[] Join(params byte[][] arrayParts) {
			byte[] bytes=new byte[arrayParts.Sum(x => (long)x.Length)];
			int offset=0;
			foreach(byte[] bytesPart in arrayParts) {
				Buffer.BlockCopy(bytesPart,0,bytes,offset,bytesPart.Length);
				offset+=bytesPart.Length;
			}
			return bytes;
		}

		private static void AssertTooManyHeaders(byte[] bytes,string message) {
			System.Diagnostics.Stopwatch stopwatch=System.Diagnostics.Stopwatch.StartNew();
			DicomImageReadResult result=Read(bytes);
			stopwatch.Stop();
			Assert.AreEqual(DicomImageReadStatus.TooLarge,result.Status,message+": "+result);
			StringAssert.Contains(result.Reason,"elements, items and fragments",message);
			//About half a second at the measured cost of a header; the bound only catches work that grows without the limit.
			Assert.IsTrue(stopwatch.ElapsedMilliseconds<10000,message+": took "+stopwatch.ElapsedMilliseconds+" ms.");
		}

		[TestMethod]
		public void FloodOfTinyElements_InADeflatedDataSet_IsDeclined() {
			//Empty private elements of 8 bytes: past MaxElements they are 8 MB inflated and a few kilobytes in the file. Without the limit, a
			//1 GB flood of them took over a minute.
			List<SynElement> listGood=ReaderCases.Twelve().ToDataset();
			byte[] bytesHead=EncodeAs(listGood.Where(x => x.Tag<Tag(0x0009,0x0010)).ToList(),SynEncoding.ExplicitLittle);
			byte[] bytesTail=EncodeAs(listGood.Where(x => x.Tag>Tag(0x0009,0x0010)).ToList(),SynEncoding.ExplicitLittle);
			byte[] bytesEmptyElement={ 0x09,0x00,0x10,0x00,(byte)'L',(byte)'O',0x00,0x00 };
			byte[] bytesFlood=MakeDeflatedFileOf(Join(bytesHead,Repeat(bytesEmptyElement,DicomImageReader.MaxElements),bytesTail));
			Assert.IsTrue(bytesFlood.Length<256*1024,"The file is small: "+bytesFlood.Length);
			AssertTooManyHeaders(bytesFlood,"deflated");
			AssertTooManyHeaders(Join(MakeFileIn(new List<SynElement>(),ExplicitVrLittleEndian),bytesHead,Repeat(bytesEmptyElement,DicomImageReader.MaxElements),
				bytesTail),"not deflated");
			//A large data set within the limit reads.
			DicomImageReadResult result=Read(MakeDeflatedFileOf(Join(bytesHead,Repeat(bytesEmptyElement,200000),bytesTail)));
			Assert.AreEqual(DicomImageReadStatus.Read,result.Status,result.ToString());
			CollectionAssert.AreEqual(ReaderCases.Words(35,4095),result.BitmapDicom.ArrayDataRaw);
		}

		[TestMethod]
		public void FloodsOfItemsAndFragments_AreDeclined() {
			List<SynElement> listGood=ReaderCases.Twelve().ToDataset();
			byte[] bytesHead=MakeFileIn(listGood.Where(x => x.Tag<Tag(0x0008,0x1115)).ToList(),ExplicitVrLittleEndian);
			byte[] bytesTail=EncodeAs(listGood.Where(x => x.Tag>Tag(0x0008,0x1115)).ToList(),SynEncoding.ExplicitLittle);
			byte[] bytesSequenceUndefined={ 0x08,0x00,0x15,0x11,(byte)'S',(byte)'Q',0,0,0xFF,0xFF,0xFF,0xFF };
			//Empty items of defined length in a sequence of undefined length.
			AssertTooManyHeaders(Join(bytesHead,bytesSequenceUndefined,Repeat(ItemHeaderLE(0),DicomImageReader.MaxElements),Delimiter(0xE0DD),bytesTail),
				"items of defined length");
			//Empty items of undefined length, each closed by its Item Delimitation Item.
			AssertTooManyHeaders(Join(bytesHead,bytesSequenceUndefined,Repeat(Join(ItemHeaderLE(0xFFFFFFFF),Delimiter(0xE00D)),DicomImageReader.MaxElements/2),
				Delimiter(0xE0DD),bytesTail),"items of undefined length");
			//Empty fragments that the Basic Offset Table puts before the first frame.
			byte[] bytesFrame=RleFrame(SyntheticDicom.Words(ReaderCases.Words(60,65535)),6,10,1,2,0);
			byte[] bytesPixels=Join(ItemHeaderLE(4),BitConverter.GetBytes(8*DicomImageReader.MaxElements),Repeat(ItemHeaderLE(0),DicomImageReader.MaxElements),
				ItemHeaderLE((uint)bytesFrame.Length),bytesFrame,Delimiter(0xE0DD));
			SynImage synImage=ReaderCases.Mono16(6,10,16,15,new ushort[60]);
			synImage.PixelElement=new SynElement { Tag=Tag(0x7FE0,0x0010),Vr="OB",Value=bytesPixels,LengthOverride=0xFFFFFFFF };
			AssertTooManyHeaders(MakeFileIn(synImage,RleLossless),"fragments");
		}

		///<summary>A PS3.10 file in Deflated Explicit VR Little Endian whose data set is bytesStart, countZeros zero bytes and bytesEnd, deflated as
		///it is written, so that a data set of a gigabyte never exists in memory.</summary>
		private static byte[] DeflatedFile(byte[] bytesStart,long countZeros,byte[] bytesEnd) {
			byte[] bytesDeflated;
			using(MemoryStream memoryStream=new MemoryStream()) {
				using(System.IO.Compression.DeflateStream deflateStream=new System.IO.Compression.DeflateStream(memoryStream,
					System.IO.Compression.CompressionLevel.Fastest,true))
				{
					deflateStream.Write(bytesStart,0,bytesStart.Length);
					byte[] bytesZeros=new byte[1024*1024];
					for(long written=0;written<countZeros;written+=bytesZeros.Length) {
						deflateStream.Write(bytesZeros,0,(int)Math.Min(bytesZeros.Length,countZeros-written));
					}
					deflateStream.Write(bytesEnd,0,bytesEnd.Length);
				}
				bytesDeflated=memoryStream.ToArray();
			}
			byte[] bytesEmpty=MakeFileIn(new List<SynElement>(),DeflatedExplicitVrLittleEndian);
			return bytesEmpty.Take(bytesEmpty.Length-Deflate(new byte[0]).Length).Concat(bytesDeflated).ToArray();
		}

		///<summary>The data set of Twelve() with Number of Frames countFrames, up to the Pixel Data header (OW, 32-bit length of every frame),
		///then the first frame; the other frames are the zeros DeflatedFile adds.</summary>
		private static byte[] MultiFrameStart(int countFrames,out long countZerosAfter) {
			SynImage synImage=ReaderCases.Twelve();
			synImage.NumberOfFrames=countFrames.ToString(System.Globalization.CultureInfo.InvariantCulture);
			List<SynElement> listDataset=Without(synImage.ToDataset(),Tag(0x7FE0,0x0010));
			byte[] bytesFrame0=SyntheticDicom.Words(ReaderCases.Words(35,4095));
			countZerosAfter=(long)bytesFrame0.Length*(countFrames-1);
			return Join(EncodeAs(listDataset,SynEncoding.ExplicitLittle),new byte[] { 0xE0,0x7F,0x10,0x00,(byte)'O',(byte)'W',0,0 },
				BitConverter.GetBytes((uint)(bytesFrame0.Length*(long)countFrames)),bytesFrame0);
		}

		[TestMethod]
		public void DeflatedDataSet_ThatInflatesPastTheLimitBeforeItsPixelData_IsDeclined() {
			//A private OB value of zeros larger than MaxInflatedBytesBeforePixelData deflates to about a thousandth of its size.
			long lengthFiller=DicomImageReader.MaxInflatedBytesBeforePixelData+1024*1024;
			List<SynElement> listDataset=ReaderCases.Twelve().ToDataset();
			int idxBefore=listDataset.FindIndex(x => x.Tag>Tag(0x0009,0x1010));
			byte[] bytesStart=EncodeAs(listDataset.Take(idxBefore).ToList(),SynEncoding.ExplicitLittle)
				.Concat(new byte[] { 0x09,0x00,0x10,0x10,(byte)'O',(byte)'B',0,0 }).Concat(BitConverter.GetBytes((uint)lengthFiller)).ToArray();
			byte[] bytesFile=DeflatedFile(bytesStart,lengthFiller,EncodeAs(listDataset.Skip(idxBefore).ToList(),SynEncoding.ExplicitLittle));
			Assert.IsTrue(bytesFile.Length<1024*1024,"The file is small: "+bytesFile.Length);
			DicomImageReadResult result=Read(bytesFile);
			Assert.AreEqual(DicomImageReadStatus.TooLarge,result.Status,result.ToString());
			StringAssert.Contains(result.Reason,"inflated data set before its Pixel Data");
		}

		[TestMethod]
		public void DeflatedPixelData_MayInflatePastTheLimitBeforeIt_ButNotPastMaxInflatedBytes() {
			//1.2 million frames of 70 bytes: 84 MB of Pixel Data, more than the limit before it. The frames after the first are read through to
			//check that they are there, never held.
			byte[] bytesStart=MultiFrameStart(1200000,out long countZeros);
			DicomImageReadResult result=Read(DeflatedFile(bytesStart,countZeros,new byte[0]));
			Assert.AreEqual(DicomImageReadStatus.Read,result.Status,result.ToString());
			CollectionAssert.AreEqual(ReaderCases.Words(35,4095),result.BitmapDicom.ArrayDataRaw);
			//Pixel Data that inflates past MaxInflatedBytes is declined: a small file cannot expand without end.
			int countFrames=(int)(DicomImageReader.MaxInflatedBytes/70)+20000;
			bytesStart=MultiFrameStart(countFrames,out countZeros);
			byte[] bytesFile=DeflatedFile(bytesStart,countZeros,new byte[0]);
			Assert.IsTrue(bytesFile.Length<16*1024*1024,"The file is small: "+bytesFile.Length);
			result=Read(bytesFile);
			Assert.AreEqual(DicomImageReadStatus.TooLarge,result.Status,result.ToString());
			StringAssert.Contains(result.Reason,"inflated data set is larger than");
		}
		#endregion Work limits

		#region Random damage and inputs that are not files
		[TestMethod]
		public void RandomDamage_NeverThrows_AndAnythingReadIsConsistent() {
			Random random=new Random(20261102);
			int countRead=0;
			foreach(string caseName in _arrayBaseCases) {
				byte[] bytes=ReaderCases.Get(caseName).Bytes;
				for(int round=0;round<400;round++) {
					byte[] bytesDamaged=(byte[])bytes.Clone();
					int countChanges=1+random.Next(4);
					for(int i=0;i<countChanges;i++) {
						int idx=random.Next(bytesDamaged.Length);
						bytesDamaged[idx]=round%3==0 ? (byte)0xFF : (byte)random.Next(256);
					}
					DicomImageReadResult result=Read(bytesDamaged);
					if(result.Status==DicomImageReadStatus.Read) {
						countRead++;
						BitmapDicom bitmapDicom=result.BitmapDicom;
						Assert.AreEqual(bitmapDicom.Width*bitmapDicom.Height,bitmapDicom.ArrayDataRaw.Length,caseName);
						Assert.IsTrue(bitmapDicom.Width>0 && bitmapDicom.Height>0);
						Assert.IsTrue(bitmapDicom.BitsStored==8 || bitmapDicom.BitsStored==12 || bitmapDicom.BitsStored==16,caseName);
						Assert.IsTrue(bitmapDicom.ArrayDataRaw.All(x => x<(1<<bitmapDicom.BitsStored)),caseName+": values within Bits Stored");
					}
					else {
						Assert.IsNull(result.BitmapDicom);
						Assert.AreNotEqual("",result.Reason);
					}
				}
			}
			Assert.IsTrue(countRead>0,"Damage to a pixel or a skipped value still reads.");
		}

		private sealed class ThrowingStream : MemoryStream {
			public ThrowingStream() : base(new byte[1000]) {
			}

			public override int Read(byte[] buffer,int offset,int count) {
				throw new IOException("The network share went away.");
			}
		}

		private sealed class WriteOnlyStream : MemoryStream {
			public override bool CanRead => false;
		}

		[TestMethod]
		public void MissingOrUnreadableInput_IsAFileError_NotAnException() {
			string pathMissing=Path.Combine(Path.GetTempPath(),"dicomreadertests_missing_"+Guid.NewGuid().ToString("N")+".dcm");
			foreach(string path in new[] { pathMissing,null,"",Path.GetTempPath(),"<>|?" }) {
				Assert.AreEqual(DicomImageReadStatus.FileError,DicomHelper.FullReader.ReadFileResult(path).Status,path ?? "null");
				Assert.AreEqual(DicomImageReadStatus.FileError,DicomHelper.ReadFileResult(path).Status,path ?? "null");
				Assert.IsNull(DicomHelper.GetFromFile(path));
			}
			Assert.AreEqual(DicomImageReadStatus.FileError,DicomHelper.FullReader.ReadStreamResult(null).Status);
			Assert.AreEqual(DicomImageReadStatus.FileError,DicomHelper.ReadStreamResult(null).Status);
			Assert.AreEqual(DicomImageReadStatus.FileError,DicomHelper.FullReader.Read(null).Status);
			Assert.IsNull(DicomHelper.GetFromStream(null));
			Assert.IsNull(DicomHelper.GetFromBase64(null));
			Assert.IsNull(DicomHelper.GetFromBase64("not base64 at all!"));
			using(WriteOnlyStream stream=new WriteOnlyStream()) {
				Assert.AreEqual(DicomImageReadStatus.FileError,DicomHelper.FullReader.ReadStreamResult(stream).Status);
			}
			using(ThrowingStream stream=new ThrowingStream()) {
				Assert.AreEqual(DicomImageReadStatus.FileError,DicomHelper.FullReader.ReadStreamResult(stream).Status);
			}
			using(ThrowingStream stream=new ThrowingStream()) {
				Assert.AreEqual(DicomImageReadStatus.FileError,DicomHelper.ReadStreamResult(stream).Status);
			}
			using(ThrowingStream stream=new ThrowingStream()) {
				Assert.IsNull(DicomHelper.GetFromStream(stream));
			}
		}
		#endregion Random damage and inputs that are not files
	}
}
