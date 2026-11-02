// The decoder seam (IDicomFrameDecoder, DicomFrameDecoderRegistry): which transfer syntaxes this version decodes, what a decoder gets and
// what the reader does with its answer, how the first frame's fragments are found, JPEG Baseline through the .NET Framework's decoder, and
// the message for compressed formats this version cannot open yet. Licensed under the GNU General Public License, version 2 or later. See
// LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using static DicomReaderTests.SyntheticDicom;

namespace DicomReaderTests {
	[TestClass]
	public class DicomFrameDecoderTests {
		///<summary>Records what it was given and returns the pixels it was told to.</summary>
		private sealed class FakeDecoder : IDicomFrameDecoder {
			public string Uid;
			public string UidSeen;
			public byte[] BytesSeen;
			public DicomFrameInfo FrameInfoSeen;
			public int CountCalls;
			public Func<byte[],DicomFrameInfo,DicomFrameDecodeResult> FuncDecode;

			public string Name => "fake";

			public IReadOnlyList<string> TransferSyntaxUids => new[] { Uid };

			public DicomFrameDecodeResult Decode(string transferSyntaxUid,byte[] frameBytes,DicomFrameInfo frameInfo) {
				CountCalls++;
				UidSeen=transferSyntaxUid;
				BytesSeen=frameBytes;
				FrameInfoSeen=frameInfo;
				return FuncDecode(frameBytes,frameInfo);
			}
		}

		private static DicomImageReadResult ReadWith(FakeDecoder decoder,byte[] bytes) {
			DicomFrameDecoderRegistry registry=DicomFrameDecoderRegistry.CreateWithInHouseDecoders();
			registry.Register(decoder);
			return new DicomImageReader(registry).Read(bytes);
		}

		private static byte[] Fragment(int length,byte seed) {
			return Enumerable.Range(0,length).Select(x => (byte)(seed+x)).ToArray();
		}

		#region The registry
		[TestMethod]
		public void DefaultRegistry_DecodesRleJpegBaselineAndEncapsulatedUncompressed() {
			CollectionAssert.AreEquivalent(new[] { RleLossless,JpegBaseline,EncapsulatedUncompressed },DicomFrameDecoderRegistry.Default.ListTransferSyntaxUids());
			Assert.IsInstanceOfType(DicomFrameDecoderRegistry.Default.Find(RleLossless),typeof(DicomRleDecoder));
			Assert.IsInstanceOfType(DicomFrameDecoderRegistry.Default.Find(JpegBaseline),typeof(DicomJpegBaselineDecoder));
			Assert.IsNull(DicomFrameDecoderRegistry.Default.Find(Jpeg2000));
			Assert.IsNull(DicomFrameDecoderRegistry.Default.Find(null));
			//Every encapsulated image syntax the standard lists has a name; only those three are decoded.
			List<DicomTransferSyntaxInfo> listEncapsulated=DicomTransferSyntaxes.All.Where(x => x.Kind==DicomTransferSyntaxKind.EncapsulatedImage).ToList();
			Assert.IsTrue(listEncapsulated.Count>=30);
			Assert.AreEqual(3,listEncapsulated.Count(x => DicomFrameDecoderRegistry.Default.Find(x.Uid)!=null));
			Assert.AreEqual(DicomTransferSyntaxes.All.Count,DicomTransferSyntaxes.All.Select(x => x.Uid).Distinct().Count());
		}

		[TestMethod]
		public void RegisteredDecoder_ReplacesTheEarlierOne_AndAnotherRegistryIsSeparate() {
			DicomFrameDecoderRegistry registry=DicomFrameDecoderRegistry.CreateWithInHouseDecoders();
			FakeDecoder decoder=new FakeDecoder { Uid=RleLossless };
			registry.Register(decoder);
			registry.Register(null);
			Assert.AreSame(decoder,registry.Find(RleLossless));
			Assert.IsInstanceOfType(DicomFrameDecoderRegistry.Default.Find(RleLossless),typeof(DicomRleDecoder),"The default registry is not changed.");
		}
		#endregion The registry

		#region What a decoder gets and what the reader does with its answer
		[TestMethod]
		public void PluggedDecoder_GetsTheFrameAndAttributes_AndItsPixelsAreShown() {
			FakeDecoder decoder=new FakeDecoder { Uid=Jpeg2000Lossless };
			ushort[] arrayDecoded=ReaderCases.Words(60,4095);
			decoder.FuncDecode=(bytes,frameInfo) => {
				DicomFrameInfo frameInfoOut=frameInfo.Clone();
				return DicomFrameDecodeResult.Decoded(SyntheticDicom.Words(arrayDecoded),frameInfoOut);
			};
			byte[] bytesFragment0=Fragment(40,1);
			byte[] bytesFragment1=Fragment(31,100);//odd: padded with one zero byte in the file
			SynImage synImage=ReaderCases.Mono16(6,10,12,11,new ushort[0],"MONOCHROME1");
			synImage.PixelElement=Encapsulated(null,bytesFragment0,bytesFragment1);
			DicomImageReadResult result=ReadWith(decoder,MakeFileIn(synImage,Jpeg2000Lossless));
			Assert.AreEqual(DicomImageReadStatus.Read,result.Status,result.ToString());
			Assert.AreEqual(1,decoder.CountCalls);
			Assert.AreEqual(Jpeg2000Lossless,decoder.UidSeen);
			CollectionAssert.AreEqual(bytesFragment0.Concat(bytesFragment1).Concat(new byte[1]).ToArray(),decoder.BytesSeen,"One frame: every fragment, as stored.");
			Assert.AreEqual(6,decoder.FrameInfoSeen.Rows);
			Assert.AreEqual(10,decoder.FrameInfoSeen.Columns);
			Assert.AreEqual(1,decoder.FrameInfoSeen.SamplesPerPixel);
			Assert.AreEqual("MONOCHROME1",decoder.FrameInfoSeen.PhotometricInterpretation);
			Assert.AreEqual(16,decoder.FrameInfoSeen.BitsAllocated);
			Assert.AreEqual(12,decoder.FrameInfoSeen.BitsStored);
			Assert.AreEqual(11,decoder.FrameInfoSeen.HighBit);
			Assert.AreEqual(0,decoder.FrameInfoSeen.PixelRepresentation);
			Assert.AreEqual(1,decoder.FrameInfoSeen.NumberOfFrames);
			Assert.AreEqual(0,decoder.FrameInfoSeen.FrameIndex);
			CollectionAssert.AreEqual(arrayDecoded,result.BitmapDicom.ArrayDataRaw);
			Assert.AreEqual(EnumDicomPhotometricInterp.MONOCHROME1,result.BitmapDicom.PhotometricInterp);
			Assert.AreEqual(12,result.BitmapDicom.BitsStored);
		}

		[TestMethod]
		public void PluggedDecoder_GetsColorWithTheCompressedColorSpaces_AndItsRgbIsShown() {
			//PS3.5 8.2.4 and A.4.4 ask for YBR_ICT or YBR_RCT on color JPEG 2000 and HTJ2K that use the multiple-component transform; YBR_PARTIAL_420
			//and YBR_PARTIAL_422 label other compressed color. The decoder returns RGB, which is shown.
			byte[] bytesRgb=ReaderCases.Color8(60);
			ushort[] arrayGray=Enumerable.Range(0,60).Select(p => (ushort)((bytesRgb[3*p]+bytesRgb[3*p+1]+bytesRgb[3*p+2])/3)).ToArray();
			foreach((string photometric,string uid) testCase in new[] { ("YBR_ICT",Jpeg2000),("YBR_RCT",Jpeg2000Lossless),("YBR_ICT",HtJpeg2000),
				("YBR_RCT",HtJpeg2000Lossless),("YBR_PARTIAL_420",Jpeg2000),("YBR_PARTIAL_422",JpegExtended) })
			{
				FakeDecoder decoder=new FakeDecoder { Uid=testCase.uid };
				decoder.FuncDecode=(bytes,frameInfo) => {
					DicomFrameInfo frameInfoOut=frameInfo.Clone();
					frameInfoOut.PhotometricInterpretation="RGB";
					frameInfoOut.PlanarConfiguration=0;
					return DicomFrameDecodeResult.Decoded(bytesRgb,frameInfoOut);
				};
				SynImage synImage=ReaderCases.Encapsulated8(testCase.photometric,6,10,3,Encapsulated(null,Fragment(40,1)));
				DicomImageReadResult result=ReadWith(decoder,MakeFileIn(synImage,testCase.uid));
				string name=testCase.photometric+" in "+testCase.uid;
				Assert.AreEqual(DicomImageReadStatus.Read,result.Status,name+": "+result);
				Assert.AreEqual(1,decoder.CountCalls,name);
				Assert.AreEqual(testCase.photometric,decoder.FrameInfoSeen.PhotometricInterpretation,name);
				Assert.AreEqual(3,decoder.FrameInfoSeen.SamplesPerPixel,name);
				Assert.AreEqual(EnumDicomPhotometricInterp.RGB,result.BitmapDicom.PhotometricInterp,name);
				CollectionAssert.AreEqual(arrayGray,result.BitmapDicom.ArrayDataRaw,name);
			}
			//Before decoding only the size, the bits and the samples are checked: one sample of YBR_ICT is broken, and the decoder is not asked.
			FakeDecoder decoderNotAsked=new FakeDecoder { Uid=Jpeg2000,FuncDecode=(bytes,frameInfo) => DicomFrameDecodeResult.Decoded(bytesRgb,frameInfo.Clone()) };
			DicomImageReadResult resultOneSample=ReadWith(decoderNotAsked,MakeFileIn(ReaderCases.Encapsulated8("YBR_ICT",6,10,1,Encapsulated(null,Fragment(40,1))),
				Jpeg2000));
			Assert.AreEqual(DicomImageReadStatus.Malformed,resultOneSample.Status,resultOneSample.ToString());
			Assert.AreEqual(0,decoderNotAsked.CountCalls);
			//Photometric interpretations the standard does not allow for any pixel data are still declined before decoding.
			DicomImageReadResult resultCmyk=ReadWith(decoderNotAsked,MakeFileIn(ReaderCases.Encapsulated8("CMYK",6,10,3,Encapsulated(null,Fragment(40,1))),Jpeg2000));
			Assert.AreEqual(DicomImageReadStatus.Unsupported,resultCmyk.Status,resultCmyk.ToString());
			Assert.AreEqual(0,decoderNotAsked.CountCalls);
			//Native pixel data in these color spaces is not shown (they describe compressed data only).
			foreach(string photometric in new[] { "YBR_ICT","YBR_RCT","YBR_PARTIAL_422","YBR_PARTIAL_420" }) {
				DicomImageReadResult resultNative=DicomHelper.FullReader.Read(MakeFileIn(ReaderCases.Color(photometric,3,4,ReaderCases.Color8(12),0),
					ExplicitVrLittleEndian));
				Assert.AreEqual(DicomImageReadStatus.Unsupported,resultNative.Status,photometric+": "+resultNative);
			}
		}

		[TestMethod]
		public void FirstFrame_IsFoundByTheOffsetTables_OrTheEndMarker() {
			byte[] bytesA=Fragment(20,1);
			byte[] bytesB=Fragment(22,50).Concat(new byte[] { 0xFF,0xD9 }).ToArray();//ends with FFD9
			byte[] bytesC=Fragment(18,120);
			byte[] bytesD=Fragment(16,200).Concat(new byte[] { 0xFF,0xD9 }).ToArray();
			uint offsetSecond=(uint)(8+bytesA.Length+8+bytesB.Length);
			var listCases=new List<(string name,SynElement element,List<SynElement> listExtra,byte[] expected)> {
				("Basic Offset Table",Encapsulated(new uint[] { 0,offsetSecond },bytesA,bytesB,bytesC,bytesD),null,bytesA.Concat(bytesB).ToArray()),
				("end marker, no tables",Encapsulated(null,bytesA,bytesB,bytesC,bytesD),null,bytesA.Concat(bytesB).ToArray()),
				("Extended Offset Table",Encapsulated(null,bytesA,bytesB,bytesC,bytesD),new List<SynElement> {
					Raw(0x7FE0,0x0001,"OV",BitConverter.GetBytes(0UL).Concat(BitConverter.GetBytes((ulong)offsetSecond)).ToArray()),
					Raw(0x7FE0,0x0002,"OV",BitConverter.GetBytes((ulong)(bytesA.Length+bytesB.Length)).Concat(BitConverter.GetBytes((ulong)(bytesC.Length+bytesD.Length))).ToArray()) },
					bytesA.Concat(bytesB).ToArray()),
				("Basic Offset Table, first frame one fragment",Encapsulated(new uint[] { 0,(uint)(8+bytesA.Length) },bytesA,bytesB,bytesC,bytesD),null,bytesA),
			};
			foreach((string name,SynElement element,List<SynElement> listExtra,byte[] expected) testCase in listCases) {
				FakeDecoder decoder=new FakeDecoder { Uid=Jpeg2000 };
				decoder.FuncDecode=(bytes,frameInfo) => DicomFrameDecodeResult.Decoded(new byte[60],frameInfo.Clone());
				SynImage synImage=ReaderCases.Mono8(6,10,8,7,null);
				synImage.NumberOfFrames="2";
				synImage.PixelElement=testCase.element;
				if(testCase.listExtra!=null) {
					synImage.ListExtra.AddRange(testCase.listExtra);
				}
				DicomImageReadResult result=ReadWith(decoder,MakeFileIn(synImage,Jpeg2000));
				Assert.AreEqual(DicomImageReadStatus.Read,result.Status,testCase.name+": "+result);
				CollectionAssert.AreEqual(testCase.expected,decoder.BytesSeen,testCase.name);
				Assert.AreEqual(2,decoder.FrameInfoSeen.NumberOfFrames,testCase.name);
			}
		}

		[TestMethod]
		public void DecoderAnswers_ThatAreNotPixels_AreDeclinedWithoutAnException() {
			SynImage synImage=ReaderCases.Mono8(6,10,8,7,null);
			synImage.PixelElement=Encapsulated(null,Fragment(10,1));
			byte[] bytesFile=MakeFileIn(synImage,Jpeg2000);
			var listCases=new List<(string name,Func<byte[],DicomFrameInfo,DicomFrameDecodeResult> func,DicomImageReadStatus status)> {
				("throws",(b,f) => throw new InvalidOperationException("decoder bug"),DicomImageReadStatus.Malformed),
				("null",(b,f) => null,DicomImageReadStatus.Malformed),
				("failed",(b,f) => DicomFrameDecodeResult.Failed("broken codestream"),DicomImageReadStatus.Malformed),
				("not supported",(b,f) => DicomFrameDecodeResult.NotSupported("a feature this decoder lacks"),DicomImageReadStatus.Unsupported),
				("too few pixels",(b,f) => DicomFrameDecodeResult.Decoded(new byte[59],f.Clone()),DicomImageReadStatus.Malformed),
				("another size",(b,f) => {
					DicomFrameInfo frameInfo=f.Clone();
					frameInfo.Rows=5;
					return DicomFrameDecodeResult.Decoded(new byte[60],frameInfo);
				},DicomImageReadStatus.Malformed),
				("an unknown photometric interpretation",(b,f) => {
					DicomFrameInfo frameInfo=f.Clone();
					frameInfo.PhotometricInterpretation="YBR_ICT";
					frameInfo.SamplesPerPixel=3;
					return DicomFrameDecodeResult.Decoded(new byte[180],frameInfo);
				},DicomImageReadStatus.Unsupported),
			};
			foreach((string name,Func<byte[],DicomFrameInfo,DicomFrameDecodeResult> func,DicomImageReadStatus status) testCase in listCases) {
				DicomImageReadResult result=ReadWith(new FakeDecoder { Uid=Jpeg2000,FuncDecode=testCase.func },bytesFile);
				Assert.AreEqual(testCase.status,result.Status,testCase.name+": "+result);
				Assert.AreNotEqual("",result.Reason,testCase.name);
			}
		}
		#endregion What a decoder gets and what the reader does with its answer

		#region JPEG Baseline through the .NET Framework's decoder
		[TestMethod]
		public void JpegBaseline_GivesTheSamePixelsFoDicomDecoded() {
			//jpeg-gray matches fo-dicom's record exactly (EveryCase test). The color stream fo-dicom decoded under the RGB label gives the same gray
			//as under YBR_FULL_422, YBR_FULL and YBR_PARTIAL_422, which fo-dicom's path did not show: the JPEG stream's own transform decides, not
			//the label.
			string expected=FoDicomBaseline.DictRecorded["jpeg-rgb-declared"];
			foreach(string caseName in new[] { "jpeg-ybr-full-422","jpeg-ybr-full","jpeg-ybr-partial-422","jpeg-rgb-declared" }) {
				DicomImageReadResult result=DicomHelper.FullReader.Read(ReaderCases.Get(caseName).Bytes);
				Assert.AreEqual(expected,FoDicomBaseline.Fingerprint(result.BitmapDicom),caseName);
			}
		}

		///<summary>The pixels of a JPEG stream through WPF's own decoder (WIC), a second .NET decoder: gray, or RGB triples.</summary>
		private static byte[] DecodeWithWpf(byte[] bytesJpeg,int samplesPerPixel) {
			using(System.IO.MemoryStream memoryStream=new System.IO.MemoryStream(bytesJpeg)) {
				System.Windows.Media.Imaging.JpegBitmapDecoder decoder=new System.Windows.Media.Imaging.JpegBitmapDecoder(memoryStream,
					System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat|System.Windows.Media.Imaging.BitmapCreateOptions.IgnoreColorProfile,
					System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
				System.Windows.Media.PixelFormat pixelFormat=samplesPerPixel==1 ? System.Windows.Media.PixelFormats.Gray8 : System.Windows.Media.PixelFormats.Rgb24;
				System.Windows.Media.Imaging.BitmapSource bitmapSource=new System.Windows.Media.Imaging.FormatConvertedBitmap(decoder.Frames[0],pixelFormat,null,0);
				byte[] arrayPixels=new byte[bitmapSource.PixelWidth*bitmapSource.PixelHeight*samplesPerPixel];
				bitmapSource.CopyPixels(arrayPixels,bitmapSource.PixelWidth*samplesPerPixel,0);
				return arrayPixels;
			}
		}

		[TestMethod]
		public void JpegBaseline_RoundTripThroughTheDotNetEncoder_IsCorrect() {
			foreach((int width,int height) size in new[] { (64,48),(33,17),(8,8),(250,3) }) {
				//Gray: close to the source, and the same as WPF's decoder gives.
				byte[] arrayGray=ReaderCases.Gray8(size.width,size.height);
				byte[] bytesJpeg=JpegGray(arrayGray,size.width,size.height,100);
				DicomImageReadResult result=DicomHelper.FullReader.Read(MakeFileIn(ReaderCases.Encapsulated8("MONOCHROME2",size.height,size.width,1,
					Encapsulated(null,bytesJpeg)),JpegBaseline));
				Assert.AreEqual(DicomImageReadStatus.Read,result.Status,result.ToString());
				int maxDifference=Enumerable.Range(0,arrayGray.Length).Max(i => Math.Abs(arrayGray[i]-result.BitmapDicom.ArrayDataRaw[i]));
				Assert.IsTrue(maxDifference<=4,size+": gray differs from the source by up to "+maxDifference);
				byte[] arrayWpf=DecodeWithWpf(bytesJpeg,1);
				int maxDifferenceWpf=Enumerable.Range(0,arrayGray.Length).Max(i => Math.Abs(arrayWpf[i]-result.BitmapDicom.ArrayDataRaw[i]));
				Assert.IsTrue(maxDifferenceWpf<=2,size+": gray differs from WPF's decoder by up to "+maxDifferenceWpf);
				//Color, YCbCr in the JPEG stream (YBR_FULL_422): the YBR to RGB conversion matches WPF's decoder, and the gray is near the source's.
				byte[] arrayRgb=ReaderCases.Rgb8Smooth(size.width,size.height);
				byte[] bytesJpegRgb=JpegRgb(arrayRgb,size.width,size.height,100);
				result=DicomHelper.FullReader.Read(MakeFileIn(ReaderCases.Encapsulated8("YBR_FULL_422",size.height,size.width,3,Encapsulated(null,bytesJpegRgb)),
					JpegBaseline));
				Assert.AreEqual(DicomImageReadStatus.Read,result.Status,result.ToString());
				Assert.AreEqual(EnumDicomPhotometricInterp.RGB,result.BitmapDicom.PhotometricInterp);
				byte[] arrayRgbWpf=DecodeWithWpf(bytesJpegRgb,3);
				int countPixels=size.width*size.height;
				int maxDifferenceColorWpf=Enumerable.Range(0,countPixels)
					.Max(p => Math.Abs((arrayRgbWpf[3*p]+arrayRgbWpf[3*p+1]+arrayRgbWpf[3*p+2])/3-result.BitmapDicom.ArrayDataRaw[p]));
				Assert.IsTrue(maxDifferenceColorWpf<=3,size+": color differs from WPF's decoder by up to "+maxDifferenceColorWpf);
				double meanDifference=Enumerable.Range(0,countPixels)
					.Average(p => Math.Abs((arrayRgb[3*p]+arrayRgb[3*p+1]+arrayRgb[3*p+2])/3-result.BitmapDicom.ArrayDataRaw[p]));
				Assert.IsTrue(meanDifference<=8,size+": color differs from the source by "+meanDifference+" on average (chroma is subsampled)");
			}
		}
		private static byte[] ProgressiveFile(int width,int height,int countAcScans) {
			return MakeFileIn(ReaderCases.Encapsulated8("MONOCHROME2",height,width,1,Encapsulated(null,JpegProgressiveFlat(width,height,80,countAcScans))),JpegBaseline);
		}

		[TestMethod]
		public void JpegProgressive_WithTheScansEncodersWrite_IsDecodedAsFoDicomDid() {
			//fo-dicom 4 decoded progressive streams under the JPEG Baseline transfer syntax (jpeg-progressive-gray in FoDicomBaseline.cs): a flat 138.
			Assert.AreEqual(FoDicomBaseline.DictRecorded["jpeg-progressive-gray"],
				"24x16 8/8 MONOCHROME2 "+FoDicomBaseline.HashValues(Enumerable.Repeat((ushort)138,24*16).ToArray()));
			//A DC scan and up to MaxScans - 1 AC scans: MaxScans in all.
			foreach(int countAcScans in new[] { 1,5,DicomJpegBaselineDecoder.MaxScans-1 }) {
				DicomImageReadResult result=DicomHelper.FullReader.Read(ProgressiveFile(24,16,countAcScans));
				Assert.AreEqual(DicomImageReadStatus.Read,result.Status,countAcScans+": "+result);
				Assert.IsTrue(result.BitmapDicom.ArrayDataRaw.All(x => x==138),countAcScans+": "+string.Join(",",result.BitmapDicom.ArrayDataRaw.Distinct()));
			}
			DicomImageReadResult resultOver=DicomHelper.FullReader.Read(ProgressiveFile(24,16,DicomJpegBaselineDecoder.MaxScans));
			Assert.AreEqual(DicomImageReadStatus.Unsupported,resultOver.Status,resultOver.ToString());
			StringAssert.Contains(resultOver.Reason,"scans");
		}

		[TestMethod]
		public void JpegWithThousandsOfScans_IsDeclinedBeforeTheDecoderRuns() {
			//The hostile shape: 8192 x 8192 pixels, a DC scan and 2000 AC scans of end-of-band runs, a third of a megabyte. GDI+ passes over all
			//million blocks in each scan, about half a minute in all; the scans are counted first.
			byte[] bytesFile=ProgressiveFile(8192,8192,2000);
			Assert.IsTrue(bytesFile.Length<512*1024,"The file is small: "+bytesFile.Length);
			System.Diagnostics.Stopwatch stopwatch=System.Diagnostics.Stopwatch.StartNew();
			DicomImageReadResult result=DicomHelper.ReadStreamResult(new System.IO.MemoryStream(bytesFile));
			stopwatch.Stop();
			Assert.AreEqual(DicomImageReadStatus.Unsupported,result.Status,result.ToString());
			StringAssert.Contains(result.Reason,"more than "+DicomJpegBaselineDecoder.MaxScans+" scans");
			Assert.IsTrue(stopwatch.ElapsedMilliseconds<5000,"Took "+stopwatch.ElapsedMilliseconds+" ms.");
			//Scan markers inside another marker segment are not scans: an APP1 segment full of FF DA before the frame header.
			byte[] bytesJpeg=ReaderCases.JpegGrayFixed();
			byte[] bytesApp=new byte[] { 0xFF,0xE1,0x00,202 }.Concat(Enumerable.Range(0,100).SelectMany(x => new byte[] { 0xFF,0xDA })).ToArray();
			byte[] bytesWithApp=bytesJpeg.Take(2).Concat(bytesApp).Concat(bytesJpeg.Skip(2)).ToArray();
			DicomImageReadResult resultApp=DicomHelper.FullReader.Read(MakeFileIn(ReaderCases.Encapsulated8("MONOCHROME2",13,21,1,Encapsulated(null,bytesWithApp)),
				JpegBaseline));
			Assert.AreEqual(FoDicomBaseline.DictRecorded["jpeg-gray"],FoDicomBaseline.Fingerprint(resultApp.BitmapDicom),resultApp.ToString());
		}
		#endregion JPEG Baseline through the .NET Framework's decoder

		#region Formats this version cannot open yet
		[TestMethod]
		public void UnsupportedCompression_NamesTheFormat_AndSaysThisVersionCannotOpenItYet() {
			foreach(ReaderCase readerCase in ReaderCases.Compressed().Where(x => x.Name.StartsWith("unsupported-"))) {
				string uid=readerCase.Name.Substring("unsupported-".Length);
				string name=DicomTransferSyntaxes.Find(uid).Name;
				DicomImageReadResult result=DicomHelper.ReadStreamResult(new System.IO.MemoryStream(readerCase.Bytes));
				Assert.AreEqual(DicomImageReadStatus.UnsupportedCompression,result.Status,readerCase.Name);
				Assert.IsNull(result.BitmapDicom);
				Assert.AreEqual(uid,result.TransferSyntaxUid);
				StringAssert.Contains(result.Reason,name,readerCase.Name);
				string message=DicomHelper.GetNotReadMessage(result);
				StringAssert.Contains(message,name,readerCase.Name);
				StringAssert.Contains(message,"cannot open that format yet",readerCase.Name);
			}
			//Named in the message: JPEG Extended, JPEG Lossless, JPEG-LS, JPEG 2000, HTJ2K.
			foreach(string uid in new[] { JpegExtended,JpegLosslessProcess14,JpegLossless,JpegLsLossless,Jpeg2000Lossless,Jpeg2000,HtJpeg2000Lossless,HtJpeg2000 }) {
				string message=DicomHelper.GetNotReadMessage(DicomHelper.ReadStreamResult(new System.IO.MemoryStream(ReaderCases.Get("unsupported-"+uid).Bytes)));
				Assert.IsTrue(message.Contains("JPEG") || message.Contains("High-Throughput"),message);
			}
		}

		[TestMethod]
		public void VideoReferencedPixelsAndUnknownSyntaxes_AreDeclinedWithTheirName() {
			foreach((string uid,string text) testCase in new[] { ("1.2.840.10008.1.2.4.102","H.264"),("1.2.840.10008.1.2.4.94","JPIP"),
				("1.2.840.10008.1.2.6.2","XML"),("1.2.3.4.5.6","1.2.3.4.5.6") })
			{
				DicomImageReadResult result=DicomHelper.FullReader.Read(MakeFileIn(ReaderCases.Twelve(),testCase.uid));
				Assert.AreEqual(DicomImageReadStatus.UnsupportedCompression,result.Status,testCase.uid);
				StringAssert.Contains(DicomHelper.GetNotReadMessage(result),testCase.text,testCase.uid);
			}
			StringAssert.Contains(DicomHelper.GetNotReadMessage(DicomHelper.FullReader.Read(MakeFileIn(ReaderCases.Twelve(),"1.2.3.4.5.6"))),"unknown format");
		}

		[TestMethod]
		public void NotReadMessage_EveryStatus_HasText() {
			Assert.AreEqual("",DicomHelper.GetNotReadMessage(DicomHelper.FullReader.Read(ReaderCases.Get("enc-implicit-le-12").Bytes)));
			Assert.AreNotEqual("",DicomHelper.GetNotReadMessage(null));
			foreach(DicomImageReadStatus status in Enum.GetValues(typeof(DicomImageReadStatus)).Cast<DicomImageReadStatus>().Where(x => x!=DicomImageReadStatus.Read)) {
				string message=DicomHelper.GetNotReadMessage(new DicomImageReadResult(status,"the reason",null,null));
				Assert.IsTrue(message.Length>20,status+": "+message);
				if(status!=DicomImageReadStatus.UnsupportedCompression) {
					StringAssert.Contains(message,"the reason",status.ToString());
				}
			}
		}
		#endregion Formats this version cannot open yet
	}
}
