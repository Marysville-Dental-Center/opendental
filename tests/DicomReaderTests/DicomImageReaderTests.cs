// The in-house full DICOM reader (DicomImageReader, docs/imaging/dicom-reader.md) against what fo-dicom 4 read from the same synthetic files
// (FoDicomBaseline.cs), and against values computed in the test from PS3.3 and PS3.5 for the images fo-dicom's path did not show.
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
	public class DicomImageReaderTests {
		///<summary>Cases where the in-house reader deliberately shows another image than fo-dicom's path did, and why. Each claim about the old
		///image is checked in OldImagesOfTheDeliberateDifferences_AreWhatTheRecordSays.</summary>
		public static readonly Dictionary<string,string> DictDeliberateDifferences=new Dictionary<string,string> {
			{ "img-signed-8-8-7","fo-dicom's path showed 8-bit two's complement values as if unsigned, so -1 was white next to a black 0; signed values are "
				+"now moved up by half their range, as for every other signed image" },
			{ "img-rgb-16","fo-dicom's path averaged the first bytes of 16-bit RGB samples as if they were 8-bit triples and kept Bits Allocated 16, which "
				+"showed a dark, scrambled image; each sample is now taken at 8 bits" },
		};

		///<summary>What the in-house reader does with each case fo-dicom's path showed nothing for.</summary>
		public static readonly Dictionary<string,DicomImageReadStatus> DictNewOutcomes=BuildNewOutcomes();

		private static Dictionary<string,DicomImageReadStatus> BuildNewOutcomes() {
			Dictionary<string,DicomImageReadStatus> dict=new Dictionary<string,DicomImageReadStatus>();
			dict.Add("enc-prefix-no-preamble-12",DicomImageReadStatus.NotDicom);
			//fo-dicom read the Implicit VR data set after a preamble of zeros without "DICM", but not the Explicit VR one; both are read now.
			dict.Add("enc-zero-preamble-no-prefix-explicit-12",DicomImageReadStatus.Read);
			foreach(string name in new[] { "img-mono2-16-10-9","img-mono2-16-13-12","img-mono2-16-14-13","img-mono2-16-15-14","img-mono2-16-1-0",
				"img-mono2-16-4-3","img-mono2-8-6-5","img-mono2-8-4-7","img-mono2-8-1-0","img-mono2-8-7-6","img-signed-16-16-15","img-signed-16-12-11",
				"img-signed-16-12-11-masked","img-signed-16-14-13","img-signed-mono1-16-16-15","img-signed-rescale-window-16","img-ybr-full-planar0",
				"img-ybr-full-planar1","img-ybr-full-422","img-palette-8-lut16","img-palette-8-lut8","img-palette-16-lut16","img-palette-8-first-mapped",
				"rle-signed-16","rle-ybr-full","jpeg-ybr-full-422","jpeg-ybr-full","jpeg-ybr-partial-422","encapsulated-uncompressed" })
			{
				dict.Add(name,DicomImageReadStatus.Read);
			}
			foreach(string name in FoDicomBaseline.DictRecorded.Keys.Where(x => x.StartsWith("unsupported-"))) {
				dict.Add(name,DicomImageReadStatus.UnsupportedCompression);
			}
			return dict;
		}

		private static DicomImageReadResult ReadThroughHelper(byte[] bytes) {
			using(MemoryStream memoryStream=new MemoryStream(bytes)) {
				return DicomHelper.ReadStreamResult(memoryStream);
			}
		}

		#region Against fo-dicom's record
		[TestMethod]
		public void EveryCase_ReadsAsFoDicomDid_OrAsTheNewOutcomesSay() {
			List<string> listProblems=new List<string>();
			foreach(ReaderCase readerCase in ReaderCases.All()) {
				Assert.IsTrue(FoDicomBaseline.DictRecorded.TryGetValue(readerCase.Name,out string recorded),"Not recorded: "+readerCase.Name);
				DicomImageReadResult result=ReadThroughHelper(readerCase.Bytes);
				string actual=FoDicomBaseline.Fingerprint(result.BitmapDicom);
				//GetFromStream, the full reader alone and a file on disk give the same.
				BitmapDicom bitmapDicomChain;
				using(MemoryStream memoryStream=new MemoryStream(readerCase.Bytes)) {
					bitmapDicomChain=DicomHelper.GetFromStream(memoryStream);
				}
				if(FoDicomBaseline.Fingerprint(bitmapDicomChain)!=actual) {
					listProblems.Add(readerCase.Name+": GetFromStream gave "+FoDicomBaseline.Fingerprint(bitmapDicomChain)+", ReadStreamResult "+actual);
				}
				if(FoDicomBaseline.IsRead(recorded) && !DictDeliberateDifferences.ContainsKey(readerCase.Name)) {
					if(actual!=recorded) {
						listProblems.Add(readerCase.Name+": fo-dicom read "+recorded+", the in-house reader "+actual+" ("+result+")");
					}
					continue;
				}
				if(DictDeliberateDifferences.ContainsKey(readerCase.Name)) {
					if(result.Status!=DicomImageReadStatus.Read || actual==recorded) {
						listProblems.Add(readerCase.Name+": a deliberate difference, but the in-house reader gave "+actual+" ("+result+")");
					}
					continue;
				}
				if(!DictNewOutcomes.TryGetValue(readerCase.Name,out DicomImageReadStatus statusExpected)) {
					listProblems.Add(readerCase.Name+": fo-dicom read nothing and no new outcome is listed; the in-house reader: "+result);
					continue;
				}
				if(result.Status!=statusExpected) {
					listProblems.Add(readerCase.Name+": expected "+statusExpected+", got "+result);
				}
				if(result.Status!=DicomImageReadStatus.Read && result.Reason=="") {
					listProblems.Add(readerCase.Name+": declined without a reason");
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
		}

		[TestMethod]
		public void EveryCase_FileAndFullReaderAlone_GiveTheSameAsTheStream() {
			string folder=Path.Combine(Path.GetTempPath(),"dicomreadertests_cases_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(folder);
			try {
				foreach(ReaderCase readerCase in ReaderCases.All()) {
					string expected=FoDicomBaseline.Fingerprint(ReadThroughHelper(readerCase.Bytes).BitmapDicom);
					string path=Path.Combine(folder,"case.dcm");
					File.WriteAllBytes(path,readerCase.Bytes);
					Assert.AreEqual(expected,FoDicomBaseline.Fingerprint(DicomHelper.GetFromFile(path)),readerCase.Name+": GetFromFile");
					Assert.AreEqual(expected,FoDicomBaseline.Fingerprint(DicomHelper.ReadFileResult(path).BitmapDicom),readerCase.Name+": ReadFileResult");
					Assert.AreEqual(expected,FoDicomBaseline.Fingerprint(DicomHelper.FullReader.Read(readerCase.Bytes).BitmapDicom),readerCase.Name+": full reader");
					Assert.AreEqual(expected,FoDicomBaseline.Fingerprint(DicomHelper.GetFromBase64(Convert.ToBase64String(readerCase.Bytes))),readerCase.Name+": base64");
					using(NonSeekableStream stream=new NonSeekableStream(readerCase.Bytes)) {
						Assert.AreEqual(expected,FoDicomBaseline.Fingerprint(DicomHelper.FullReader.ReadStream(stream)),readerCase.Name+": not seekable");
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

		[TestMethod]
		public void OldImagesOfTheDeliberateDifferences_AreWhatTheRecordSays() {
			//Signed 8-bit: the old image was the raw bytes.
			byte[] bytesSigned=ReaderCases.SignedWords(15,-128,127).Select(x => (byte)x).ToArray();
			Assert.AreEqual(FoDicomBaseline.DictRecorded["img-signed-8-8-7"],"5x3 8/8 MONOCHROME2 "+FoDicomBaseline.HashValues(bytesSigned.Select(x => (ushort)x).ToArray()));
			//16-bit RGB: the old image averaged the first 3 x 6 bytes of the 16-bit samples as 8-bit triples, with Bits Allocated 16.
			byte[] bytesRgb16=SyntheticDicom.Words(ReaderCases.Words(18,65535));
			ushort[] arrayOld=Enumerable.Range(0,6).Select(p => (ushort)((bytesRgb16[3*p]+bytesRgb16[3*p+1]+bytesRgb16[3*p+2])/3)).ToArray();
			Assert.AreEqual(FoDicomBaseline.DictRecorded["img-rgb-16"],"3x2 16/16 RGB "+FoDicomBaseline.HashValues(arrayOld));
		}
		#endregion Against fo-dicom's record

		#region Values of the images fo-dicom's path did not show
		///<summary>PS3.5 8.1.1 and PS3.3 C.7.6.3.1.1, computed here independently: the stored value is the Bits Stored bits ending at High Bit;
		///a signed one is moved up by 2^(BitsStored-1); then it is shifted up to the depth BitmapDicom shows (8, 12 or 16).</summary>
		private static ushort[] ExpectedGray(ushort[] arrayWords,int bitsAllocated,int bitsStored,int highBit,bool isSigned,out int bitsShown) {
			bitsShown=bitsAllocated==8 || bitsStored<=8 ? 8 : bitsStored<=12 ? 12 : 16;
			int shown=bitsShown;
			return arrayWords.Select(word => {
				int value=(word>>(highBit-bitsStored+1)) & ((1<<bitsStored)-1);
				if(isSigned) {
					int signed=value>=(1<<(bitsStored-1)) ? value-(1<<bitsStored) : value;
					value=signed+(1<<(bitsStored-1));
				}
				return (ushort)(value<<(shown-bitsStored));
			}).ToArray();
		}

		private static void AssertGray(string caseName,ushort[] arrayWords,int bitsAllocated,int bitsStored,int highBit,bool isSigned,
			EnumDicomPhotometricInterp photometric)
		{
			DicomImageReadResult result=ReadThroughHelper(ReaderCases.Get(caseName).Bytes);
			Assert.AreEqual(DicomImageReadStatus.Read,result.Status,caseName+": "+result);
			ushort[] arrayExpected=ExpectedGray(arrayWords,bitsAllocated,bitsStored,highBit,isSigned,out int bitsShown);
			Assert.AreEqual(bitsAllocated,result.BitmapDicom.BitsAllocated,caseName);
			Assert.AreEqual(bitsShown,result.BitmapDicom.BitsStored,caseName);
			Assert.AreEqual(photometric,result.BitmapDicom.PhotometricInterp,caseName);
			CollectionAssert.AreEqual(arrayExpected,result.BitmapDicom.ArrayDataRaw,caseName);
			Assert.IsTrue(result.BitmapDicom.ArrayDataRaw.All(x => x<(1<<bitsShown)),caseName+": within the range windowing expects");
		}

		[TestMethod]
		public void BitsStoredOtherThan8_12_16_AreShiftedUpToTheNextDepth_WithAnyHighBit() {
			foreach((int bitsStored,int highBit) pair in new[] { (10,9),(13,12),(14,13),(15,14),(1,0),(4,3),(12,15),(12,13),(8,15) }) {
				ushort[] arrayWords=ReaderCases.Noisy(ReaderCases.Words(20,(1<<pair.bitsStored)-1),pair.bitsStored,pair.highBit);
				AssertGray("img-mono2-16-"+pair.bitsStored+"-"+pair.highBit,arrayWords,16,pair.bitsStored,pair.highBit,false,EnumDicomPhotometricInterp.MONOCHROME2);
			}
			foreach((int bitsStored,int highBit) pair in new[] { (6,5),(4,7),(1,0),(7,6),(8,7) }) {
				ushort[] arrayWords=ReaderCases.Noisy(ReaderCases.Words(15,(1<<pair.bitsStored)-1),pair.bitsStored,pair.highBit).Select(x => (ushort)(x & 0xFF)).ToArray();
				AssertGray("img-mono2-8-"+pair.bitsStored+"-"+pair.highBit,arrayWords,8,pair.bitsStored,pair.highBit,false,EnumDicomPhotometricInterp.MONOCHROME2);
			}
		}

		[TestMethod]
		public void SignedValues_AreMovedUpByHalfTheirRange_InOrder() {
			AssertGray("img-signed-16-16-15",ReaderCases.SignedWords(20,-32768,32767),16,16,15,true,EnumDicomPhotometricInterp.MONOCHROME2);
			AssertGray("img-signed-16-12-11",ReaderCases.SignedWords(20,-2048,2047),16,12,11,true,EnumDicomPhotometricInterp.MONOCHROME2);
			AssertGray("img-signed-16-12-11-masked",ReaderCases.SignedWords(20,-2048,2047).Select(x => (ushort)(x & 0x0FFF)).ToArray(),16,12,11,true,
				EnumDicomPhotometricInterp.MONOCHROME2);
			AssertGray("img-signed-16-14-13",ReaderCases.SignedWords(20,-8192,8191),16,14,13,true,EnumDicomPhotometricInterp.MONOCHROME2);
			AssertGray("img-signed-8-8-7",ReaderCases.SignedWords(15,-128,127).Select(x => (ushort)(x & 0xFF)).ToArray(),8,8,7,true,
				EnumDicomPhotometricInterp.MONOCHROME2);
			AssertGray("img-signed-mono1-16-16-15",ReaderCases.SignedWords(20,-1000,3000),16,16,15,true,EnumDicomPhotometricInterp.MONOCHROME1);
			//Rescale and window are not applied: the same values as without them.
			AssertGray("img-signed-rescale-window-16",ReaderCases.SignedWords(20,-1024,3071),16,16,15,true,EnumDicomPhotometricInterp.MONOCHROME2);
			AssertGray("rle-signed-16",ReaderCases.SignedWords(60,-2048,2047),16,16,15,true,EnumDicomPhotometricInterp.MONOCHROME2);
			//The order of the values is kept: -32768 is the darkest, 32767 the brightest.
			BitmapDicom bitmapDicom=ReadThroughHelper(ReaderCases.Get("img-signed-16-16-15").Bytes).BitmapDicom;
			short[] arraySigned=ReaderCases.SignedWords(20,-32768,32767).Select(x => unchecked((short)x)).ToArray();
			for(int i=0;i<arraySigned.Length;i++) {
				for(int j=0;j<arraySigned.Length;j++) {
					Assert.AreEqual(arraySigned[i]<arraySigned[j],bitmapDicom.ArrayDataRaw[i]<bitmapDicom.ArrayDataRaw[j]);
				}
			}
		}

		[TestMethod]
		public void SignedImages_WindowOnImport_WithoutErrors() {
			foreach(string caseName in new[] { "img-signed-16-12-11","img-signed-16-16-15","img-signed-16-14-13","img-mono2-16-13-12","img-mono2-16-10-9" }) {
				BitmapDicom bitmapDicom=ReadThroughHelper(ReaderCases.Get(caseName).Bytes).BitmapDicom;
				DicomHelper.CalculateWindowingOnImport(bitmapDicom);//indexes a 4096-entry histogram for 12 bits: values must fit
				using(System.Drawing.Bitmap bitmap=DicomHelper.ApplyWindowing(bitmapDicom,bitmapDicom.WindowingMin,bitmapDicom.WindowingMax)) {
					Assert.AreEqual(bitmapDicom.Width,bitmap.Width,caseName);
				}
			}
		}

		///<summary>PS3.3 C.7.6.3.1.2, computed here: YBR_FULL to RGB.</summary>
		private static int[] YbrToRgb(int y,int cb,int cr) {
			Func<double,int> clamp=x => (int)Math.Max(0,Math.Min(255,Math.Round(x,MidpointRounding.AwayFromZero)));
			return new[] { clamp(y+1.402*(cr-128)),clamp(y-0.344136*(cb-128)-0.714136*(cr-128)),clamp(y+1.772*(cb-128)) };
		}

		private static void AssertColorGray(string caseName,int[][] arrayRgb) {
			DicomImageReadResult result=ReadThroughHelper(ReaderCases.Get(caseName).Bytes);
			Assert.AreEqual(DicomImageReadStatus.Read,result.Status,caseName+": "+result);
			Assert.AreEqual(EnumDicomPhotometricInterp.RGB,result.BitmapDicom.PhotometricInterp,caseName);
			Assert.AreEqual(8,result.BitmapDicom.BitsAllocated,caseName);
			Assert.AreEqual(8,result.BitmapDicom.BitsStored,caseName);
			CollectionAssert.AreEqual(arrayRgb.Select(x => (ushort)((x[0]+x[1]+x[2])/3)).ToArray(),result.BitmapDicom.ArrayDataRaw,caseName);
		}

		[TestMethod]
		public void Color_RgbYbrAndPalette_BecomeTheGrayOfTheirRgb() {
			byte[] bytesColor=ReaderCases.Color8(12);
			int[][] arrayRgb=Enumerable.Range(0,12).Select(p => new int[] { bytesColor[3*p],bytesColor[3*p+1],bytesColor[3*p+2] }).ToArray();
			AssertColorGray("img-rgb-planar0",arrayRgb);
			AssertColorGray("img-rgb-planar1",arrayRgb);
			int[][] arrayFromYbr=Enumerable.Range(0,12).Select(p => YbrToRgb(bytesColor[3*p],bytesColor[3*p+1],bytesColor[3*p+2])).ToArray();
			AssertColorGray("img-ybr-full-planar0",arrayFromYbr);
			AssertColorGray("img-ybr-full-planar1",arrayFromYbr);
			byte[] bytes422=ReaderCases.Bytes(24,255);
			int[][] arrayFrom422=Enumerable.Range(0,12).Select(p => {
				int pair=p/2;
				return YbrToRgb(bytes422[4*pair+p%2],bytes422[4*pair+2],bytes422[4*pair+3]);
			}).ToArray();
			AssertColorGray("img-ybr-full-422",arrayFrom422);
			byte[] bytesRgb60=ReaderCases.Color8(60);
			AssertColorGray("rle-ybr-full",Enumerable.Range(0,60).Select(p => YbrToRgb(bytesRgb60[3*p],bytesRgb60[3*p+1],bytesRgb60[3*p+2])).ToArray());
			//16-bit RGB at 8 bits: the high byte of each sample.
			ushort[] arrayWords=ReaderCases.Words(18,65535);
			AssertColorGray("img-rgb-16",Enumerable.Range(0,6).Select(p => new int[] { arrayWords[3*p]>>8,arrayWords[3*p+1]>>8,arrayWords[3*p+2]>>8 }).ToArray());
		}

		[TestMethod]
		public void Palette_IndicesGoThroughTheTables_ClampedAtBothEnds() {
			byte[] bytesIndices=ReaderCases.Bytes(15,255);
			AssertColorGray("img-palette-8-lut16",bytesIndices.Select(i => Enumerable.Range(0,3).Select(c => ReaderCases.PaletteEntry16(c,i)>>8).ToArray()).ToArray());
			AssertColorGray("img-palette-8-lut8",bytesIndices.Select(i => Enumerable.Range(0,3).Select(c => (i*(c+1)*31)%256).ToArray()).ToArray());
			AssertColorGray("img-palette-8-first-mapped",bytesIndices.Select(i => Enumerable.Range(0,3)
				.Select(c => ReaderCases.PaletteEntry16(c,Math.Max(0,Math.Min(99,i-50)))>>8).ToArray()).ToArray());
			ushort[] arrayIndices16=ReaderCases.Words(20,4095).Select(x => (ushort)(x+100)).ToArray();
			AssertColorGray("img-palette-16-lut16",arrayIndices16.Select(i => Enumerable.Range(0,3).Select(c => ReaderCases.PaletteEntry16(c,i-100)>>8).ToArray()).ToArray());
		}

		[TestMethod]
		public void EncapsulatedUncompressed_IsTheNativeFrame() {
			DicomImageReadResult result=ReadThroughHelper(ReaderCases.Get("encapsulated-uncompressed").Bytes);
			Assert.AreEqual(DicomImageReadStatus.Read,result.Status,result.ToString());
			CollectionAssert.AreEqual(ReaderCases.Gray8(10,6).Select(x => (ushort)x).ToArray(),result.BitmapDicom.ArrayDataRaw);
		}
		#endregion Values of the images fo-dicom's path did not show

		#region Encodings
		[TestMethod]
		public void EveryEncodingAndLayout_GivesTheExplicitLittleEndianImage() {
			string expected12=FoDicomBaseline.Fingerprint(ReadThroughHelper(ReaderCases.Get("enc-explicit-le-12").Bytes).BitmapDicom);
			foreach(ReaderCase readerCase in ReaderCases.Encodings().Where(x => x.Name.EndsWith("-12") && x.Name!="enc-prefix-no-preamble-12")) {
				Assert.AreEqual(expected12,FoDicomBaseline.Fingerprint(ReadThroughHelper(readerCase.Bytes).BitmapDicom),readerCase.Name);
			}
			CollectionAssert.AreEqual(ReaderCases.Words(35,4095),ReadThroughHelper(ReaderCases.Get("enc-deflate-12").Bytes).BitmapDicom.ArrayDataRaw);
			CollectionAssert.AreEqual(ReaderCases.Words(24,65535),ReadThroughHelper(ReaderCases.Get("enc-explicit-be-16").Bytes).BitmapDicom.ArrayDataRaw);
			//"DICM" at byte 0 without the preamble: refused, as fo-dicom refused it.
			DicomImageReadResult result=ReadThroughHelper(ReaderCases.Get("enc-prefix-no-preamble-12").Bytes);
			Assert.AreEqual(DicomImageReadStatus.NotDicom,result.Status);
			StringAssert.Contains(result.Reason,"preamble");
		}

		[TestMethod]
		public void MislabelledEncodings_AreReadFromTheData_AsFoDicomReadThem() {
			//Every one gives the source pixels, as fo-dicom 4 did for all but the Explicit VR data set after a preamble of zeros, which it did not
			//show (FoDicomBaseline.cs); the reported transfer syntax is still the file's label.
			ushort[] arraySource=ReaderCases.Words(35,4095);
			foreach(ReaderCase readerCase in ReaderCases.MislabelledEncodings()) {
				DicomImageReadResult result=ReadThroughHelper(readerCase.Bytes);
				Assert.AreEqual(DicomImageReadStatus.Read,result.Status,readerCase.Name+": "+result);
				CollectionAssert.AreEqual(arraySource,result.BitmapDicom.ArrayDataRaw,readerCase.Name);
				Assert.AreEqual(DicomImageReadStatus.Read,DicomHelper.FullReader.Read(readerCase.Bytes).Status,readerCase.Name+": the full reader alone");
			}
			Assert.AreEqual(ExplicitVrLittleEndian,ReadThroughHelper(ReaderCases.Get("enc-ts-explicit-dataset-implicit-12").Bytes).TransferSyntaxUid);
			Assert.AreEqual(ImplicitVrLittleEndian,ReadThroughHelper(ReaderCases.Get("enc-meta-implicit-no-preamble-12").Bytes).TransferSyntaxUid);
			Assert.IsNull(ReadThroughHelper(ReaderCases.Get("enc-dataset-only-implicit-private-first-12").Bytes).TransferSyntaxUid);
		}

		[TestMethod]
		public void MislabelledEncodings_ThatFoDicomDidNotShowEither_AreDeclined() {
			//fo-dicom 4.0.7 found no Rows in any of these when the cases above were recorded, so version 0.0 showed none of them.
			List<SynElement> listTwelve=ReaderCases.Twelve().ToDataset();
			var listCases=new List<(string name,byte[] bytes)> {
				("zero bytes for the VR",MakeFileIn(ReaderCases.WithVr(listTwelve,Tag(0x0028,0x0010),"\0\0"),ExplicitVrLittleEndian)),
				("an unknown VR",MakeFileIn(ReaderCases.WithVr(listTwelve,Tag(0x0028,0x0010),"XX"),ExplicitVrLittleEndian)),
				("an Explicit VR data set labelled Implicit VR",MakeFileIn(listTwelve,ImplicitVrLittleEndian,encoding:SynEncoding.ExplicitLittle)),
			};
			foreach((string name,byte[] bytes) testCase in listCases) {
				DicomImageReadResult result=ReadThroughHelper(testCase.bytes);
				Assert.AreEqual(DicomImageReadStatus.Malformed,result.Status,testCase.name+": "+result);
				Assert.AreNotEqual("",result.Reason,testCase.name);
			}
		}

		[TestMethod]
		public void BlankVr_OnTagsTheReaderDoesNotKeep_TakesTheLayoutTheDataShows() {
			//The ReaderCases (vr-spaces) give the source pixels, as fo-dicom 4 did. Here the layout decision is pushed further.
			ushort[] arraySource=ReaderCases.Words(35,4095);
			List<SynElement> listTwelve=ReaderCases.Twelve().ToDataset();
			//The 32-bit layout in a deflated data set, where the reader cannot seek and looks ahead in memory: a private OB whose value starts like
			//an element header, as in the ReaderCases, and a sequence.
			List<SynElement> listPrivate=With(listTwelve,ReaderCases.BlankVr(Raw(0x0009,0x1010,"OB",
				new byte[] { (byte)'A',(byte)'E',4,0,1,2,3,4,5,6,7,8,9,10,11,12 })));
			List<SynElement> listSequence=With(listTwelve,ReaderCases.BlankVr(Sequence(0x0008,0x1140,false,Item(false,Text(0x0008,0x1150,"UI","2.25.7")))));
			foreach((string name,List<SynElement> listElements) testCase in new[] { ("private OB",listPrivate),("sequence",listSequence) }) {
				DicomImageReadResult result=ReadThroughHelper(MakeFileIn(testCase.listElements,DeflatedExplicitVrLittleEndian));
				Assert.AreEqual(DicomImageReadStatus.Read,result.Status,testCase.name+", deflated: "+result);
				CollectionAssert.AreEqual(arraySource,result.BitmapDicom.ArrayDataRaw,testCase.name);
			}
			//A private OB of 100 KB whose value starts like an element header: only the bytes after the value tell the layout. A seekable stream
			//looks there; a stream that cannot seek looks at most MaxLookAheadBytes ahead, so there the value is taken as empty and the file is
			//declined (the one blank-VR layout this version does not read the way fo-dicom did, and only in a stream that cannot seek).
			byte[] bytesLargeValue=Enumerable.Range(0,100*1024).Select(i => (byte)(i%251)).ToArray();
			bytesLargeValue[0]=(byte)'A';
			bytesLargeValue[1]=(byte)'E';
			bytesLargeValue[2]=4;
			bytesLargeValue[3]=0;
			List<SynElement> listLarge=With(listTwelve,ReaderCases.BlankVr(Raw(0x0009,0x1010,"OB",bytesLargeValue)));
			byte[] bytesLarge=MakeFileIn(listLarge,ExplicitVrLittleEndian);
			DicomImageReadResult resultLarge=ReadThroughHelper(bytesLarge);
			Assert.AreEqual(DicomImageReadStatus.Read,resultLarge.Status,resultLarge.ToString());
			CollectionAssert.AreEqual(arraySource,resultLarge.BitmapDicom.ArrayDataRaw);
			using(NonSeekableStream stream=new NonSeekableStream(bytesLarge)) {
				DicomImageReadResult resultNotSeekable=DicomHelper.FullReader.ReadStreamResult(stream);
				Assert.AreEqual(DicomImageReadStatus.Malformed,resultNotSeekable.Status,resultNotSeekable.ToString());
			}
			Assert.AreEqual(DicomImageReadStatus.Malformed,ReadThroughHelper(MakeFileIn(listLarge,DeflatedExplicitVrLittleEndian)).Status);
			//An empty LO with two spaces for its VR in a 2.4 MB file: read as a 32-bit length, the next tag, (0028,0002), is 128 KB, which fits,
			//and the next element looks like one; the look past those 128 KB lands in the pixel data, which does not, so the LO is empty.
			ushort[] arrayLarge=ReaderCases.Words(1200*1000,65535);
			List<SynElement> listEmpty=With(ReaderCases.Mono16(1000,1200,16,15,arrayLarge).ToDataset(),ReaderCases.BlankVr(Text(0x0008,0x1030,"LO","")));
			DicomImageReadResult resultEmpty=ReadThroughHelper(MakeFileIn(listEmpty,ExplicitVrLittleEndian));
			Assert.AreEqual(DicomImageReadStatus.Read,resultEmpty.Status,resultEmpty.ToString());
			CollectionAssert.AreEqual(arrayLarge,resultEmpty.BitmapDicom.ArrayDataRaw);
			//A blank-VR private OB followed by elements of a lower group (tags out of order): the bytes after its value are not of a later group,
			//but the 16-bit reading does not look like the next header either, so the long layout is taken, and the file reads, as fo-dicom read it
			//(checked with the version 0.0 DLLs).
			List<SynElement> listOutOfOrder=listTwelve.Where(x => (x.Tag>>16)!=0x0008).ToList();
			listOutOfOrder.Insert(0,ReaderCases.BlankVr(Raw(0x0009,0x1010,"OB",new byte[] { 1,2,3,4,5,6,7,8 })));
			listOutOfOrder.InsertRange(1,listTwelve.Where(x => (x.Tag>>16)==0x0008));
			DicomImageReadResult resultOutOfOrder=ReadThroughHelper(MakeFileIn(listOutOfOrder,ExplicitVrLittleEndian));
			Assert.AreEqual(DicomImageReadStatus.Read,resultOutOfOrder.Status,resultOutOfOrder.ToString());
			CollectionAssert.AreEqual(arraySource,resultOutOfOrder.BitmapDicom.ArrayDataRaw);
			//Two spaces with a 16-bit length other than 0 are the 16-bit layout whatever follows; an undefined 32-bit length is a sequence.
			DicomImageReadResult resultShort=ReadThroughHelper(MakeFileIn(With(listTwelve,ReaderCases.BlankVr(Text(0x0008,0x1030,"LO","00"))),ExplicitVrLittleEndian));
			Assert.AreEqual(DicomImageReadStatus.Read,resultShort.Status,resultShort.ToString());
			SynElement elementUndefinedBig=ReaderCases.BlankVr(Sequence(0x0008,0x1140,true,Item(true,Text(0x0008,0x1150,"UI","2.25.7"))));
			DicomImageReadResult resultUndefinedBig=ReadThroughHelper(MakeFileIn(With(listTwelve,elementUndefinedBig),ExplicitVrBigEndian));
			Assert.AreEqual(DicomImageReadStatus.Read,resultUndefinedBig.Status,resultUndefinedBig.ToString());
			CollectionAssert.AreEqual(arraySource,resultUndefinedBig.BitmapDicom.ArrayDataRaw);
		}

		[TestMethod]
		public void TransferSyntax_IsReported() {
			Assert.AreEqual(ImplicitVrLittleEndian,ReadThroughHelper(ReaderCases.Get("enc-implicit-le-12").Bytes).TransferSyntaxUid);
			Assert.AreEqual(DeflatedExplicitVrLittleEndian,ReadThroughHelper(ReaderCases.Get("enc-deflate-12").Bytes).TransferSyntaxUid);
			Assert.IsNull(ReadThroughHelper(ReaderCases.Get("enc-dataset-only-implicit-12").Bytes).TransferSyntaxUid);
			DicomImageReadResult result=ReadThroughHelper(ReaderCases.Get("unsupported-"+Jpeg2000Lossless).Bytes);
			Assert.AreEqual(Jpeg2000Lossless,result.TransferSyntaxUid);
			Assert.AreEqual("JPEG 2000 Image Compression (Lossless Only)",result.TransferSyntaxName);
		}

		[TestMethod]
		public void NotDicom_IsDeclined() {
			foreach((string name,byte[] bytes) testCase in new[] {
				("empty",new byte[0]),
				("short",new byte[] { 1,2,3 }),
				("zeros",new byte[400]),
				("a JPEG file",ReaderCases.JpegGrayFixed()),
				("text",System.Text.Encoding.ASCII.GetBytes("This is a text file that someone named .dcm by mistake.")),
				("a preamble that is not zeros, without DICM",Enumerable.Repeat((byte)0x41,128).Concat(EncodeAs(ReaderCases.Twelve().ToDataset(),
					SynEncoding.ExplicitLittle)).ToArray()),
				("a first group above 00FF",MakeFileIn(new List<SynElement> { Text(0x0100,0x0010,"LO","X") }.Concat(ReaderCases.Twelve().ToDataset()).ToList(),
					ImplicitVrLittleEndian,SynLayout.DatasetOnly)),
				("a first group below 0008 other than 0004",MakeFileIn(new List<SynElement> { Text(0x0006,0x0010,"LO","X") }
					.Concat(ReaderCases.Twelve().ToDataset()).ToList(),ImplicitVrLittleEndian,SynLayout.DatasetOnly)),
				("an Implicit VR length past the end",MakeFileIn(With(ReaderCases.Twelve().ToDataset().Where(x => (x.Tag>>16)!=0x0008).ToList(),
					new SynElement { Tag=Tag(0x0009,0x0010),Vr="LO",Value=new byte[2],LengthOverride=0x00FFFFF0 }),ImplicitVrLittleEndian,SynLayout.DatasetOnly)),
				("group 0002 at byte 0 with an Implicit VR length past the end",new byte[] { 0x02,0x00,0x10,0x00,0xF0,0xFF,0xFF,0x00 }
					.Concat(EncodeAs(ReaderCases.Twelve().ToDataset(),SynEncoding.ImplicitLittle)).ToArray()),
			}) {
				DicomImageReadResult result=DicomHelper.FullReader.Read(testCase.bytes);
				Assert.AreEqual(DicomImageReadStatus.NotDicom,result.Status,testCase.name+": "+result);
				Assert.AreNotEqual("",result.Reason);
			}
		}
		#endregion Encodings

		#region Multi-frame and large files
		[TestMethod]
		public void MultiFrame_ShowsTheFirstFrame() {
			CollectionAssert.AreEqual(ReaderCases.Words(20,4095),ReadThroughHelper(ReaderCases.Get("img-multiframe-12").Bytes).BitmapDicom.ArrayDataRaw);
			CollectionAssert.AreEqual(ReaderCases.Words(20,4095),ReadThroughHelper(ReaderCases.Get("img-multiframe-12-implicit").Bytes).BitmapDicom.ArrayDataRaw);
			ushort[] arrayFrame0=ReaderCases.Words(60,65535);
			arrayFrame0[3]=arrayFrame0[4]=arrayFrame0[5]=arrayFrame0[6]=1234;
			CollectionAssert.AreEqual(arrayFrame0,ReadThroughHelper(ReaderCases.Get("rle-multiframe-bot").Bytes).BitmapDicom.ArrayDataRaw);
			CollectionAssert.AreEqual(arrayFrame0,ReadThroughHelper(ReaderCases.Get("rle-multiframe-no-bot").Bytes).BitmapDicom.ArrayDataRaw);
		}

		[TestMethod]
		public void MultiFrame_FramesAfterTheFirstAreNotRead() {
			//1000 frames of 64 x 64 at 16 bits (8 MB): only the first is read from a seekable stream.
			SynImage synImage=ReaderCases.Mono16(64,64,16,15,new ushort[0]);
			synImage.PixelBytes=new byte[64*64*2*1000];
			for(int i=0;i<64*64;i++) {
				synImage.PixelBytes[2*i]=(byte)i;
				synImage.PixelBytes[2*i+1]=(byte)(i>>8);
			}
			synImage.NumberOfFrames="1000";
			byte[] bytes=MakeFileIn(synImage,ExplicitVrLittleEndian);
			using(CountingStream stream=new CountingStream(bytes)) {
				DicomImageReadResult result=DicomHelper.FullReader.ReadStreamResult(stream);
				Assert.AreEqual(DicomImageReadStatus.Read,result.Status,result.ToString());
				CollectionAssert.AreEqual(Enumerable.Range(0,64*64).Select(x => (ushort)x).ToArray(),result.BitmapDicom.ArrayDataRaw);
				Assert.IsTrue(stream.CountRead<64*64*2+64*1024,"Read "+stream.CountRead+" of "+bytes.Length+" bytes.");
			}
		}

		[TestMethod]
		public void LargeFile_SkippedValuesAreNotRead_AndTheFirstFrameIs() {
			//A private OB value of 3.75 GB before the image: larger than any file the subset reader takes, and far larger than memory.
			uint lengthFiller=0xF0000000;
			ushort[] arrayPixels=ReaderCases.Words(300*200,65535);
			SynImage synImage=ReaderCases.Mono16(200,300,16,15,arrayPixels);
			List<SynElement> listDataset=synImage.ToDataset();
			int idxBefore=listDataset.FindIndex(x => x.Tag>Tag(0x0009,0x1010));
			byte[] bytesBefore=MakeFileIn(listDataset.Take(idxBefore).ToList(),ExplicitVrLittleEndian);
			byte[] bytesFillerHeader=new byte[] { 0x09,0x00,0x10,0x10,(byte)'O',(byte)'B',0,0 }.Concat(BitConverter.GetBytes(lengthFiller)).ToArray();
			byte[] bytesAfter=EncodeAs(listDataset.Skip(idxBefore).ToList(),SynEncoding.ExplicitLittle);
			using(GeneratedStream stream=new GeneratedStream(bytesBefore.Concat(bytesFillerHeader).ToArray(),lengthFiller,bytesAfter,isSeekable:true)) {
				BitmapDicom bitmapDicom=DicomHelper.GetFromStream(stream);
				Assert.IsNotNull(bitmapDicom);
				CollectionAssert.AreEqual(arrayPixels,bitmapDicom.ArrayDataRaw);
				Assert.IsTrue(stream.CountRead<2*1024*1024,"Read "+stream.CountRead+" bytes.");
			}
			//Not seekable: the skipped value is read through, never held.
			uint lengthSmallerFiller=300*1024*1024;
			byte[] bytesSmallerHeader=new byte[] { 0x09,0x00,0x10,0x10,(byte)'O',(byte)'B',0,0 }.Concat(BitConverter.GetBytes(lengthSmallerFiller)).ToArray();
			using(GeneratedStream stream=new GeneratedStream(bytesBefore.Concat(bytesSmallerHeader).ToArray(),lengthSmallerFiller,bytesAfter,isSeekable:false)) {
				long memoryBefore=GC.GetTotalMemory(true);
				DicomImageReadResult result=DicomHelper.FullReader.ReadStreamResult(stream);
				Assert.AreEqual(DicomImageReadStatus.Read,result.Status,result.ToString());
				CollectionAssert.AreEqual(arrayPixels,result.BitmapDicom.ArrayDataRaw);
				Assert.IsTrue(GC.GetTotalMemory(false)-memoryBefore<64*1024*1024,"The skipped value was not kept in memory.");
			}
		}

		[TestMethod]
		public void LargeFile_ThroughTheChain_NonSeekable_IsReadByTheFullReader() {
			uint lengthFiller=(uint)DicomPixelReaderChain.MaxReplayBytes+1024*1024;
			ushort[] arrayPixels=ReaderCases.Words(30*20,4095);
			List<SynElement> listDataset=ReaderCases.Mono16(20,30,12,11,arrayPixels).ToDataset();
			int idxBefore=listDataset.FindIndex(x => x.Tag>Tag(0x0009,0x1010));
			byte[] bytesBefore=MakeFileIn(listDataset.Take(idxBefore).ToList(),ExplicitVrLittleEndian)
				.Concat(new byte[] { 0x09,0x00,0x10,0x10,(byte)'O',(byte)'B',0,0 }).Concat(BitConverter.GetBytes(lengthFiller)).ToArray();
			byte[] bytesAfter=EncodeAs(listDataset.Skip(idxBefore).ToList(),SynEncoding.ExplicitLittle);
			using(GeneratedStream stream=new GeneratedStream(bytesBefore,lengthFiller,bytesAfter,isSeekable:false)) {
				CollectionAssert.AreEqual(arrayPixels,DicomHelper.GetFromStream(stream).ArrayDataRaw);
			}
		}

		[TestMethod]
		public void HugeImageSize_IsDeclinedFromTheAttributes_WithoutAllocating() {
			List<SynElement> listDataset=With(With(ReaderCases.Twelve().ToDataset(),US(0x0028,0x0010,65535)),US(0x0028,0x0011,65535));
			DicomImageReadResult result=ReadThroughHelper(MakeFileIn(listDataset,ExplicitVrLittleEndian));
			Assert.AreEqual(DicomImageReadStatus.TooLarge,result.Status,result.ToString());
			//Within the pixel limit but more than the file holds: malformed, again from the lengths alone.
			listDataset=With(With(ReaderCases.Twelve().ToDataset(),US(0x0028,0x0010,8000)),US(0x0028,0x0011,8000));
			result=ReadThroughHelper(MakeFileIn(listDataset,ExplicitVrLittleEndian));
			Assert.AreEqual(DicomImageReadStatus.Malformed,result.Status,result.ToString());
			StringAssert.Contains(result.Reason,"needs");
			//In a deflated data set the reader cannot know the end in advance: a 128 MB frame declared, 70 bytes there, is read in pieces and
			//refused when the data ends.
			List<SynElement> listDeflated=Without(listDataset,Tag(0x7FE0,0x0010));
			listDeflated.Add(new SynElement { Tag=Tag(0x7FE0,0x0010),Vr="OW",Value=new byte[70],LengthOverride=8000*8000*2 });
			result=ReadThroughHelper(MakeFileIn(listDeflated,DeflatedExplicitVrLittleEndian));
			Assert.AreEqual(DicomImageReadStatus.Malformed,result.Status,result.ToString());
			StringAssert.Contains(result.Reason,"ends inside");
			Assert.IsTrue(DicomImageReader.MaxFramePixels>=8192*8192);
		}

		//Deflated data sets that inflate past the limits: DicomImageReaderHardeningTests, Work limits.
		#endregion Multi-frame and large files

		#region Test streams
		internal sealed class NonSeekableStream : MemoryStream {
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

		internal sealed class CountingStream : MemoryStream {
			public long CountRead;

			public CountingStream(byte[] bytes) : base(bytes) {
			}

			public override int Read(byte[] buffer,int offset,int count) {
				int countRead=base.Read(buffer,offset,count);
				CountRead+=countRead;
				return countRead;
			}
		}

		///<summary>Head bytes, then countZeros zero bytes that are never stored, then tail bytes.</summary>
		internal sealed class GeneratedStream : Stream {
			private readonly byte[] _bytesHead;
			private readonly long _countZeros;
			private readonly byte[] _bytesTail;
			private readonly bool _isSeekable;
			private long _position;
			public long CountRead;

			public GeneratedStream(byte[] bytesHead,long countZeros,byte[] bytesTail,bool isSeekable) {
				_bytesHead=bytesHead;
				_countZeros=countZeros;
				_bytesTail=bytesTail;
				_isSeekable=isSeekable;
			}

			public override bool CanRead => true;
			public override bool CanSeek => _isSeekable;
			public override bool CanWrite => false;
			public override long Length => _isSeekable ? _bytesHead.Length+_countZeros+_bytesTail.Length : throw new NotSupportedException();

			public override long Position {
				get => _isSeekable ? _position : throw new NotSupportedException();
				set {
					if(!_isSeekable) {
						throw new NotSupportedException();
					}
					_position=value;
				}
			}

			public override int Read(byte[] buffer,int offset,int count) {
				long length=_bytesHead.Length+_countZeros+_bytesTail.Length;
				int countRead=(int)Math.Max(0,Math.Min(count,length-_position));
				int done=0;
				while(done<countRead) {
					long at=_position+done;
					int countPart;
					if(at<_bytesHead.Length) {
						countPart=(int)Math.Min(countRead-done,_bytesHead.Length-at);
						Buffer.BlockCopy(_bytesHead,(int)at,buffer,offset+done,countPart);
					}
					else if(at<_bytesHead.Length+_countZeros) {
						countPart=(int)Math.Min(countRead-done,_bytesHead.Length+_countZeros-at);
						Array.Clear(buffer,offset+done,countPart);
					}
					else {
						countPart=countRead-done;
						Buffer.BlockCopy(_bytesTail,(int)(at-_bytesHead.Length-_countZeros),buffer,offset+done,countPart);
					}
					done+=countPart;
				}
				_position+=countRead;
				CountRead+=countRead;
				return countRead;
			}

			public override long Seek(long offset,SeekOrigin origin) {
				if(!_isSeekable) {
					throw new NotSupportedException();
				}
				long length=_bytesHead.Length+_countZeros+_bytesTail.Length;
				_position=origin==SeekOrigin.Begin ? offset : origin==SeekOrigin.Current ? _position+offset : length+offset;
				return _position;
			}

			public override void Flush() {
			}

			public override void SetLength(long value) {
				throw new NotSupportedException();
			}

			public override void Write(byte[] buffer,int offset,int count) {
				throw new NotSupportedException();
			}
		}
		#endregion Test streams
	}
}
