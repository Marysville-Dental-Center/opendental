// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace OpenPmsDentalBusiness {
	///<summary>What DicomImageReader did with one file.</summary>
	public enum DicomImageReadStatus {
		///<summary>Read: the result has the pixels.</summary>
		Read,
		///<summary>Not a DICOM file: no PS3.10 header and no data set the reader recognizes.</summary>
		NotDicom,
		///<summary>A valid DICOM file whose pixel data is compressed in a format this version cannot decode yet (no decoder is registered for
		///its transfer syntax), video, pixel data that is not in the file, or a transfer syntax the standard does not list.</summary>
		UnsupportedCompression,
		///<summary>A valid image the program does not show: floating point pixels, 1 or 32 bits allocated, a photometric interpretation other
		///than MONOCHROME1, MONOCHROME2, PALETTE COLOR, RGB, YBR_FULL or YBR_FULL_422, signed color, a segmented palette, or no pixel data.</summary>
		Unsupported,
		///<summary>The encoding is broken: a length past the end of the file, an unknown VR, a missing attribute, an item where none belongs,
		///sequences nested too deep, a frame that cannot be decoded.</summary>
		Malformed,
		///<summary>The first frame is larger than MaxFramePixels or its compressed bytes larger than MaxCompressedFrameBytes, a deflated data
		///set inflates past MaxInflatedBytesBeforePixelData before its Pixel Data or past MaxInflatedBytes in all, or the file has more than
		///MaxElements elements, items and fragments.</summary>
		TooLarge,
		///<summary>The file or stream could not be read.</summary>
		FileError,
	}

	///<summary>The outcome of DicomImageReader: the pixels, or why there are none.</summary>
	public sealed class DicomImageReadResult {
		public DicomImageReadStatus Status { get; }
		///<summary>Why the file was not read, as a sentence. "" when Status is Read.</summary>
		public string Reason { get; }
		///<summary>The pixels; null unless Status is Read.</summary>
		public BitmapDicom BitmapDicom { get; }
		///<summary>The Transfer Syntax UID of the File Meta Information; null when the file has none or the reader did not get that far.</summary>
		public string TransferSyntaxUid { get; }

		public DicomImageReadResult(DicomImageReadStatus status,string reason,BitmapDicom bitmapDicom,string transferSyntaxUid) {
			Status=status;
			Reason=reason ?? "";
			BitmapDicom=bitmapDicom;
			TransferSyntaxUid=transferSyntaxUid;
		}

		///<summary>The PS3.6 name of the transfer syntax, the UID when the standard does not list it, or "" when there is none.</summary>
		public string TransferSyntaxName {
			get {
				if(string.IsNullOrEmpty(TransferSyntaxUid)) {
					return "";
				}
				DicomTransferSyntaxInfo transferSyntax=DicomTransferSyntaxes.Find(TransferSyntaxUid);
				return transferSyntax==null ? TransferSyntaxUid : transferSyntax.Name;
			}
		}

		public override string ToString() {
			return Status==DicomImageReadStatus.Read ? "Read" : Status+": "+Reason;
		}
	}

	///<summary>The in-house DICOM reader of every file the program shows (owner decision 2026-11-02, option C; docs/imaging/dicom-reader.md).
	///Written from the public DICOM standard only: NEMA PS3.5 (encodings, encapsulation, RLE), PS3.3 C.7.6.3 (Image Pixel module), PS3.6
	///(transfer syntaxes) and PS3.10 (file format). No DICOM library is used and no other implementation's code.
	///
	///Encodings: Implicit VR Little Endian, Explicit VR Little Endian, Explicit VR Big Endian (retired) and Deflated Explicit VR Little Endian
	///(System.IO.Compression), in a PS3.10 file (128-byte preamble, "DICM", File Meta Information), in a file that starts directly with the
	///File Meta Information, or as a data set with no File Meta Information or without a Transfer Syntax UID, whose encoding is then told from
	///its first element. The encoding is decided from the data where writers broke the standard in ways fo-dicom 4 read: File Meta
	///Information in Implicit VR, an Implicit VR data set labelled Explicit VR Little Endian, two spaces where an element's VR goes. fo-dicom
	///refused "DICM" at byte 0 without the preamble, and so does this reader (docs/imaging/dicom-reader.md lists exactly what is accepted).
	///Encapsulated pixel data (PS3.5 A.4): the items, the fragments, the Basic Offset Table and the Extended Offset Table are parsed here, and
	///the first frame's bytes go to the decoder DicomFrameDecoderRegistry has for the transfer syntax. A compressed transfer syntax without a
	///decoder is declined as UnsupportedCompression with the format's name, before any fragment is read.
	///
	///The result is what the old path through fo-dicom gave wherever it gave an image (tests/DicomReaderTests/FoDicomBaseline.cs): the stored
	///values of frame 0, the bits above High Bit cleared and the value moved down from High Bit, no rescale and no VOI window, no inversion for
	///MONOCHROME1, and color averaged to gray as (R+G+B)/3 with PhotometricInterp RGB. What fo-dicom's path did not show is now shown in the same
	///terms: signed values are moved up by half their range (2^(BitsStored-1)) so that they are unsigned in the same order; Bits Stored other
	///than 8, 12 and 16 is moved up to the next of those (shifted left), because BitmapDicom's windowing knows only those; YBR_FULL,
	///YBR_FULL_422 and PALETTE COLOR become RGB first, and 16-bit color is taken at 8 bits.
	///
	///Hardening: it never throws to callers; it reads the stream forward once and checks every length against what is left before it reads or
	///skips (a seekable stream knows its end; a non-seekable one is read, never trusted); sequences of undefined length are nested at most
	///MaxSequenceDepth deep; it allocates only the attributes it keeps (each at most a quarter megabyte), the first frame (at most MaxFramePixels
	///pixels) and that frame's compressed bytes (at most MaxCompressedFrameBytes); a deflated data set may inflate to at most
	///MaxInflatedBytesBeforePixelData before its Pixel Data and MaxInflatedBytes in all; at most MaxElements headers are read, which bounds
	///the work. Everything else is skipped, and frames after the first are never read.</summary>
	public sealed class DicomImageReader : IDicomPixelReader {
		///<summary>The largest first frame read: 2^26 pixels (8192 x 8192). A panoramic or cephalometric image is a few million.</summary>
		public const int MaxFramePixels=1<<26;
		///<summary>The most compressed bytes the first frame of encapsulated pixel data may have.</summary>
		public const int MaxCompressedFrameBytes=256*1024*1024;
		///<summary>Sequences of undefined length nested deeper than this are declined (Malformed).</summary>
		public const int MaxSequenceDepth=16;
		///<summary>A deflated data set that inflates past this many bytes is declined (TooLarge), so a small file cannot expand without end.</summary>
		public const long MaxInflatedBytes=1L<<30;
		///<summary>A deflated data set that inflates past this many bytes before its Pixel Data is declined (TooLarge): only the pixels need to
		///be large, and the attributes of a real image are kilobytes.</summary>
		public const long MaxInflatedBytesBeforePixelData=64L*1024*1024;
		///<summary>Fragments of the first frame at most.</summary>
		public const int MaxFragmentsPerFrame=65536;
		///<summary>The most element, item, delimiter and fragment headers the reader reads in one file (TooLarge past it). Reading a header costs
		///about half a microsecond, so this bounds the work of a file of millions of tiny elements, which deflate makes small on disk, to about
		///half a second. A real data set has a few thousand.</summary>
		public const int MaxElements=1000000;
		private const int _maxKeptValueBytes=256*1024;
		private const uint _undefinedLength=0xFFFFFFFF;
		private const uint _tagItem=0xFFFEE000;
		private const uint _tagItemDelimitation=0xFFFEE00D;
		private const uint _tagSequenceDelimitation=0xFFFEE0DD;
		private const uint _tagTransferSyntaxUid=0x00020010;
		private const uint _tagSamplesPerPixel=0x00280002;
		private const uint _tagPhotometricInterpretation=0x00280004;
		private const uint _tagPlanarConfiguration=0x00280006;
		private const uint _tagNumberOfFrames=0x00280008;
		private const uint _tagRows=0x00280010;
		private const uint _tagColumns=0x00280011;
		private const uint _tagBitsAllocated=0x00280100;
		private const uint _tagBitsStored=0x00280101;
		private const uint _tagHighBit=0x00280102;
		private const uint _tagPixelRepresentation=0x00280103;
		private const uint _tagRedDescriptor=0x00281101;
		private const uint _tagBlueDescriptor=0x00281103;
		private const uint _tagRedData=0x00281201;
		private const uint _tagBlueData=0x00281203;
		private const uint _tagSegmentedRedData=0x00281221;
		private const uint _tagSegmentedBlueData=0x00281223;
		private const uint _tagExtendedOffsetTable=0x7FE00001;
		private const uint _tagExtendedOffsetTableLengths=0x7FE00002;
		private const uint _tagFloatPixelData=0x7FE00008;
		private const uint _tagDoubleFloatPixelData=0x7FE00009;
		private const uint _tagPixelData=0x7FE00010;
		///<summary>Every VR of PS3.5 Table 6.2-1.</summary>
		private static readonly HashSet<string> _hashSetVrs=new HashSet<string> { "AE","AS","AT","CS","DA","DS","DT","FL","FD","IS","LO","LT","OB","OD",
			"OF","OL","OV","OW","PN","SH","SL","SQ","SS","ST","SV","TM","UC","UI","UL","UN","UR","US","UT","UV" };
		///<summary>The VRs with two reserved bytes and a 32-bit length (PS3.5 7.1.2).</summary>
		private static readonly HashSet<string> _hashSetLongVrs=new HashSet<string> { "OB","OD","OF","OL","OV","OW","SQ","SV","UC","UN","UR","UT","UV" };

		private readonly DicomFrameDecoderRegistry _registry;

		///<summary>Reads with DicomFrameDecoderRegistry.Default.</summary>
		public DicomImageReader() : this(null) {
		}

		///<summary>Reads with the decoders of registry (Default when null).</summary>
		public DicomImageReader(DicomFrameDecoderRegistry registry) {
			_registry=registry ?? DicomFrameDecoderRegistry.Default;
		}

		///<summary>The pixels, or null when the file is not read (see ReadFileResult for why).</summary>
		public BitmapDicom ReadFile(string path) {
			return ReadFileResult(path).BitmapDicom;
		}

		///<summary>The pixels, or null when the stream is not read (see ReadStreamResult for why).</summary>
		public BitmapDicom ReadStream(Stream stream) {
			return ReadStreamResult(stream).BitmapDicom;
		}

		///<summary>Reads the file at path. Never throws.</summary>
		public DicomImageReadResult ReadFileResult(string path) {
			try {
				using(FileStream fileStream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,81920,FileOptions.SequentialScan)) {
					return ReadStreamResult(fileStream);
				}
			}
			catch(Exception ex) {
				return new DicomImageReadResult(DicomImageReadStatus.FileError,"The file could not be read: "+ex.Message,null,null);
			}
		}

		///<summary>Reads a DICOM file from the stream's current position. It reads forward only and stops after the first frame, so it consumes
		///part of the stream. Never throws.</summary>
		public DicomImageReadResult ReadStreamResult(Stream stream) {
			if(stream==null || !stream.CanRead) {
				return new DicomImageReadResult(DicomImageReadStatus.FileError,"No stream to read.",null,null);
			}
			Parser parser=new Parser(stream,_registry);
			try {
				BitmapDicom bitmapDicom=parser.Parse();
				return new DicomImageReadResult(DicomImageReadStatus.Read,"",bitmapDicom,parser.TransferSyntaxUid);
			}
			catch(DeclineException ex) {
				return new DicomImageReadResult(ex.Status,ex.Message,null,parser.TransferSyntaxUid);
			}
			catch(OutOfMemoryException) {
				return new DicomImageReadResult(DicomImageReadStatus.TooLarge,"There is not enough memory to read the image.",null,parser.TransferSyntaxUid);
			}
			catch(IOException ex) {
				return new DicomImageReadResult(DicomImageReadStatus.FileError,"The file could not be read: "+ex.Message,null,parser.TransferSyntaxUid);
			}
			catch(InvalidDataException ex) {
				return new DicomImageReadResult(DicomImageReadStatus.Malformed,"The deflated data set is not valid deflate data: "+ex.Message,null,
					parser.TransferSyntaxUid);
			}
			catch(Exception ex) {
				//Not expected: every length is checked first.
				return new DicomImageReadResult(DicomImageReadStatus.Malformed,"The file could not be parsed ("+ex.GetType().Name+": "+ex.Message+").",null,
					parser.TransferSyntaxUid);
			}
			finally {
				parser.Close();
			}
		}

		///<summary>Reads a complete DICOM file held in bytes. Never throws.</summary>
		public DicomImageReadResult Read(byte[] bytes) {
			if(bytes==null) {
				return new DicomImageReadResult(DicomImageReadStatus.FileError,"No bytes to read.",null,null);
			}
			using(MemoryStream memoryStream=new MemoryStream(bytes,false)) {
				return ReadStreamResult(memoryStream);
			}
		}

		///<summary>Ends the parse with a status and reason. Caught in ReadStreamResult; it never leaves this class.</summary>
		private sealed class DeclineException : Exception {
			public DicomImageReadStatus Status { get; }

			public DeclineException(DicomImageReadStatus status,string reason) : base(reason) {
				Status=status;
			}
		}

		private static DeclineException Malformed(string reason) {
			return new DeclineException(DicomImageReadStatus.Malformed,reason);
		}

		private static DeclineException Unsupported(string reason) {
			return new DeclineException(DicomImageReadStatus.Unsupported,reason);
		}

		private static DeclineException TooLarge(string reason) {
			return new DeclineException(DicomImageReadStatus.TooLarge,reason);
		}

		private static string FormatTag(uint tag) {
			return "("+(tag>>16).ToString("X4",CultureInfo.InvariantCulture)+","+(tag & 0xFFFF).ToString("X4",CultureInfo.InvariantCulture)+")";
		}

		private static string FormatCount(long count) {
			return count.ToString(CultureInfo.InvariantCulture);
		}

		#region ByteSource
		///<summary>A forward-only reader over a stream, which can look ahead a few bytes and skips by seeking when the stream can seek. Every read
		///and skip is checked against the end: the stream's length when it can seek, the bytes really there when it cannot, and maxBytes.</summary>
		private sealed class ByteSource {
			///<summary>Reads larger than this from a stream that cannot seek go in pieces of this size.</summary>
			private const int _chunkBytes=4*1024*1024;
			private readonly Stream _stream;
			private readonly bool _canSeek;
			private readonly long _streamEnd;
			private long _maxBytes;
			private string _nameLimit;
			private byte[] _arrayPeek=new byte[0];
			private int _peekStart;
			private long _consumed;

			public ByteSource(Stream stream,long maxBytes,string nameLimit) {
				_stream=stream;
				_canSeek=stream.CanSeek;
				_streamEnd=_canSeek ? stream.Length : -1;
				_maxBytes=maxBytes;
				_nameLimit=nameLimit;
			}

			private int PeekCount {
				get { return _arrayPeek.Length-_peekStart; }
			}

			///<summary>From now on, at most maxBytes may be consumed in all, counted from the start of this source.</summary>
			public void SetLimit(long maxBytes,string nameLimit) {
				_maxBytes=maxBytes;
				_nameLimit=nameLimit;
			}

			///<summary>Bytes left when the stream can seek; -1 when that is unknown.</summary>
			public long Remaining {
				get {
					if(!_canSeek) {
						return -1;
					}
					return Math.Max(0,_streamEnd-_stream.Position)+PeekCount;
				}
			}

			///<summary>Up to count bytes from the current position, without consuming them. Fewer at the end of the stream.</summary>
			public byte[] Peek(int count) {
				if(PeekCount<count) {
					byte[] arrayNew=new byte[count];
					int countHave=PeekCount;
					Buffer.BlockCopy(_arrayPeek,_peekStart,arrayNew,0,countHave);
					int countRead=ReadFromStream(arrayNew,countHave,count-countHave);
					if(countHave+countRead<count) {
						Array.Resize(ref arrayNew,countHave+countRead);
					}
					_arrayPeek=arrayNew;
					_peekStart=0;
				}
				byte[] arrayOut=new byte[Math.Min(count,PeekCount)];
				Buffer.BlockCopy(_arrayPeek,_peekStart,arrayOut,0,arrayOut.Length);
				return arrayOut;
			}

			public bool IsAtEnd() {
				return Peek(1).Length==0;
			}

			private int ReadFromStream(byte[] buffer,int offset,int count) {
				int total=0;
				while(total<count) {
					int countRead=_stream.Read(buffer,offset+total,count-total);
					if(countRead<=0) {
						break;
					}
					total+=countRead;
				}
				return total;
			}

			private void CountConsumed(long count) {
				_consumed+=count;
				if(_consumed>_maxBytes) {
					throw TooLarge("The "+_nameLimit+" is larger than "+FormatCount(_maxBytes/1024/1024)+" MB.");
				}
			}

			///<summary>Fills count bytes from the current position, or declines when the stream ends first.</summary>
			public void ReadExact(byte[] buffer,int offset,int count,string what) {
				int countFromPeek=Math.Min(count,PeekCount);
				Buffer.BlockCopy(_arrayPeek,_peekStart,buffer,offset,countFromPeek);
				_peekStart+=countFromPeek;
				int countRead=countFromPeek;
				if(countFromPeek<count) {
					countRead+=ReadFromStream(buffer,offset+countFromPeek,count-countFromPeek);
				}
				CountConsumed(countRead);
				if(countRead<count) {
					throw Malformed("The file ends inside "+what+".");
				}
			}

			public byte[] ReadBytes(int count,string what) {
				if(_canSeek || count<=_chunkBytes) {
					//A seekable stream's lengths were checked against its end before anything this large is read.
					byte[] bytes=new byte[count];
					ReadExact(bytes,0,count,what);
					return bytes;
				}
				//A stream that cannot tell its end: memory grows with the bytes that are really there, not with the declared length.
				List<byte[]> listChunks=new List<byte[]>();
				int countLeft=count;
				while(countLeft>0) {
					byte[] bytesChunk=new byte[Math.Min(_chunkBytes,countLeft)];
					ReadExact(bytesChunk,0,bytesChunk.Length,what);
					listChunks.Add(bytesChunk);
					countLeft-=bytesChunk.Length;
				}
				byte[] bytesAll=new byte[count];
				int offset=0;
				foreach(byte[] bytesChunk in listChunks) {
					Buffer.BlockCopy(bytesChunk,0,bytesAll,offset,bytesChunk.Length);
					offset+=bytesChunk.Length;
				}
				return bytesAll;
			}

			///<summary>Moves count bytes ahead, or declines when that is past the end.</summary>
			public void Skip(long count,string what) {
				if(count<0) {
					throw Malformed("Negative length of "+what+".");
				}
				int countFromPeek=(int)Math.Min(count,PeekCount);
				_peekStart+=countFromPeek;
				long countLeft=count-countFromPeek;
				if(countLeft==0) {
					CountConsumed(countFromPeek);
					return;
				}
				if(_canSeek) {
					if(_stream.Position+countLeft>_streamEnd) {
						throw Malformed(char.ToUpper(what[0])+what.Substring(1)+" runs past the end of the file.");
					}
					CountConsumed(count);
					_stream.Seek(countLeft,SeekOrigin.Current);
					return;
				}
				CountConsumed(countFromPeek);
				byte[] buffer=new byte[(int)Math.Min(81920,countLeft)];
				while(countLeft>0) {
					int countWant=(int)Math.Min(buffer.Length,countLeft);
					int countRead=ReadFromStream(buffer,0,countWant);
					CountConsumed(countRead);
					if(countRead<countWant) {
						throw Malformed(char.ToUpper(what[0])+what.Substring(1)+" runs past the end of the file.");
					}
					countLeft-=countRead;
				}
			}

			///<summary>The rest of this source as a forward-only stream, for the deflated data set.</summary>
			public Stream AsStream() {
				return new SourceStream(this);
			}

			private sealed class SourceStream : Stream {
				private readonly ByteSource _source;

				public SourceStream(ByteSource source) {
					_source=source;
				}

				public override bool CanRead => true;
				public override bool CanSeek => false;
				public override bool CanWrite => false;
				public override long Length => throw new NotSupportedException();

				public override long Position {
					get => throw new NotSupportedException();
					set => throw new NotSupportedException();
				}

				public override int Read(byte[] buffer,int offset,int count) {
					if(count<=0) {
						return 0;
					}
					if(_source.PeekCount>0) {
						int countFromPeek=Math.Min(count,_source.PeekCount);
						Buffer.BlockCopy(_source._arrayPeek,_source._peekStart,buffer,offset,countFromPeek);
						_source._peekStart+=countFromPeek;
						_source.CountConsumed(countFromPeek);
						return countFromPeek;
					}
					int countRead=_source._stream.Read(buffer,offset,count);
					if(countRead>0) {
						_source.CountConsumed(countRead);
					}
					return countRead;
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
		}
		#endregion ByteSource

		#region Layout
		private enum PhotometricKind {
			Monochrome1,
			Monochrome2,
			Palette,
			Rgb,
			YbrFull,
			YbrFull422,
			///<summary>YBR_PARTIAL_422, YBR_PARTIAL_420, YBR_ICT or YBR_RCT: these describe compressed pixel data only (PS3.3 C.7.6.3.1.2; PS3.5
			///8.2), which a decoder returns as RGB or YBR_FULL. Accepted for the attributes a decoder is given, never shown as they are.</summary>
			YbrCompressed,
		}

		///<summary>The checked attributes of one frame and how many bytes it has.</summary>
		private sealed class ImageLayout {
			public DicomFrameInfo FrameInfo;
			public PhotometricKind Kind;
			public int CountPixels;
			public int FrameBytes;
		}

		///<summary>The palette tables (PS3.3 C.7.6.3.1.5 and C.7.6.3.1.6), as 8-bit values, with the first mapped stored value.</summary>
		private sealed class Palette {
			public int FirstMapped;
			public byte[][] ArrayTables=new byte[3][];
		}
		#endregion Layout

		///<summary>One pass over the stream.</summary>
		private sealed class Parser {
			private readonly DicomFrameDecoderRegistry _registry;
			private ByteSource _source;
			private bool _isBigEndian;
			private bool _isImplicit;
			private DicomTransferSyntaxInfo _transferSyntax;
			private ushort? _samplesPerPixel;
			private string _photometricInterpretation;
			private ushort? _planarConfiguration;
			private int? _numberOfFrames;
			private ushort? _rows;
			private ushort? _columns;
			private ushort? _bitsAllocated;
			private ushort? _bitsStored;
			private ushort? _highBit;
			private ushort? _pixelRepresentation;
			private readonly ushort[][] _arrayPaletteDescriptors=new ushort[3][];
			private readonly byte[][] _arrayPaletteData=new byte[3][];
			private bool _hasSegmentedPalette;
			private ulong[] _arrayExtendedOffsets;
			private ulong[] _arrayExtendedLengths;
			private Stream _streamInflated;
			///<summary>Element, item, delimiter and fragment headers read so far (MaxElements).</summary>
			private int _countHeaders;

			///<summary>The Transfer Syntax UID of the File Meta Information, once read.</summary>
			public string TransferSyntaxUid { get; private set; }

			public Parser(Stream stream,DicomFrameDecoderRegistry registry) {
				_source=new ByteSource(stream,long.MaxValue,"file");
				_registry=registry;
			}

			///<summary>Releases the inflater of a deflated data set. The caller's stream stays open.</summary>
			public void Close() {
				try {
					_streamInflated?.Dispose();
				}
				catch(Exception) {
				}
			}

			#region Numbers
			private ushort U16(byte[] bytes,int at,bool isBig) {
				if(isBig) {
					return (ushort)((bytes[at]<<8) | bytes[at+1]);
				}
				return (ushort)(bytes[at] | (bytes[at+1]<<8));
			}

			private uint U32(byte[] bytes,int at,bool isBig) {
				if(isBig) {
					return ((uint)U16(bytes,at,true)<<16) | U16(bytes,at+2,true);
				}
				return U16(bytes,at,false) | ((uint)U16(bytes,at+2,false)<<16);
			}

			private static bool IsVr(byte byte1,byte byte2) {
				if(byte1<'A' || byte1>'Z' || byte2<'A' || byte2>'Z') {
					return false;
				}
				return _hashSetVrs.Contains(""+(char)byte1+(char)byte2);
			}

			///<summary>Two spaces where an Explicit VR element's VR goes: some writers put them for a VR they did not know.</summary>
			private static bool IsBlankVr(byte byte1,byte byte2) {
				return byte1==0x20 && byte2==0x20;
			}

			///<summary>Whether the 8-byte element header at the current position is Explicit VR (PS3.5 7.1.2): VR letters, or two spaces, at
			///bytes 4 and 5.</summary>
			private static bool IsExplicitHeader(byte[] bytesHeader) {
				return IsVr(bytesHeader[4],bytesHeader[5]) || IsBlankVr(bytesHeader[4],bytesHeader[5]);
			}

			///<summary>Whether the 8-byte element header at the current position, read as Implicit VR Little Endian (PS3.5 7.1.3), has a length
			///that fits what is left of the file: undefined, within the file, or the end is not known.</summary>
			private bool IsImplicitLengthThatFits(byte[] bytesHeader) {
				uint length=U32(bytesHeader,4,false);
				long remaining=_source.Remaining;
				return length==_undefinedLength || remaining<0 || length<=remaining-8;
			}

			///<summary>Counts one element, item, delimiter or fragment header, and declines the file past MaxElements.</summary>
			private void CountHeader() {
				_countHeaders++;
				if(_countHeaders>MaxElements) {
					throw TooLarge("The file has more than "+FormatCount(MaxElements)+" elements, items and fragments; this version reads at most that many.");
				}
			}
			#endregion Numbers

			public BitmapDicom Parse() {
				ReadHeader();
				return ParseDataset();
			}

			#region File header and encoding
			///<summary>The PS3.10 header, if any, and the encoding of the data set (PS3.10 7.1; PS3.5 10).</summary>
			private void ReadHeader() {
				byte[] bytesHead=_source.Peek(132);
				bool hasMeta=false;
				if(bytesHead.Length>=132 && bytesHead[128]=='D' && bytesHead[129]=='I' && bytesHead[130]=='C' && bytesHead[131]=='M') {
					_source.Skip(132,"the PS3.10 preamble");
					hasMeta=true;
				}
				else if(bytesHead.Length>=4 && bytesHead[0]=='D' && bytesHead[1]=='I' && bytesHead[2]=='C' && bytesHead[3]=='M') {
					throw new DeclineException(DicomImageReadStatus.NotDicom,"The file starts with \"DICM\" without the 128-byte preamble before it (PS3.10 7.1).");
				}
				else if(bytesHead.Length>=8 && bytesHead[0]==0x02 && bytesHead[1]==0x00
					&& (IsExplicitHeader(bytesHead) || IsImplicitLengthThatFits(bytesHead)))
				{
					hasMeta=true;//File Meta Information at the very start, without the preamble, in Explicit or Implicit VR
				}
				if(hasMeta) {
					ReadFileMeta();
				}
				if(TransferSyntaxUid==null) {
					DetectEncoding();
					return;
				}
				_transferSyntax=DicomTransferSyntaxes.Find(TransferSyntaxUid);
				if(_transferSyntax==null) {
					throw new DeclineException(DicomImageReadStatus.UnsupportedCompression,"The transfer syntax "+TransferSyntaxUid
						+" is not one of the DICOM standard's (PS3.6 Table A-1).");
				}
				switch(_transferSyntax.Kind) {
					case DicomTransferSyntaxKind.ImplicitLittleEndian:
						_isImplicit=true;
						break;
					case DicomTransferSyntaxKind.ExplicitBigEndian:
						_isBigEndian=true;
						break;
					case DicomTransferSyntaxKind.DeflatedExplicitLittleEndian:
						//PS3.5 A.5: everything after the File Meta Information is deflated, with no zlib header.
						_streamInflated=new DeflateStream(_source.AsStream(),CompressionMode.Decompress,true);
						_source=new ByteSource(_streamInflated,MaxInflatedBytesBeforePixelData,"inflated data set before its Pixel Data");
						CheckExplicitVrLabel();
						break;
					case DicomTransferSyntaxKind.ExplicitLittleEndian:
					case DicomTransferSyntaxKind.EncapsulatedImage:
						CheckExplicitVrLabel();
						break;
					case DicomTransferSyntaxKind.EncapsulatedVideo:
						throw new DeclineException(DicomImageReadStatus.UnsupportedCompression,"The file holds video ("+_transferSyntax.Name+").");
					case DicomTransferSyntaxKind.NoPixelsInFile:
						throw new DeclineException(DicomImageReadStatus.UnsupportedCompression,"The file's pixel data is not stored in it as an image ("
							+_transferSyntax.Name+").");
				}
			}

			///<summary>A data set the transfer syntax says is Explicit VR Little Endian, whose first element has neither VR letters nor two spaces
			///where the VR goes but an Implicit VR length that fits the file, is read as Implicit VR Little Endian: some writers labelled Implicit
			///VR data sets Explicit VR in the File Meta Information. The same test DetectEncoding applies, made on the data, not the label.</summary>
			private void CheckExplicitVrLabel() {
				byte[] bytesPeek=_source.Peek(8);
				if(bytesPeek.Length==8 && !IsExplicitHeader(bytesPeek) && IsImplicitLengthThatFits(bytesPeek)) {
					_isImplicit=true;
				}
			}

			///<summary>The group 0002 elements (PS3.10 7.1). Only the Transfer Syntax UID is kept; the group length is not needed, since the group
			///ends where the elements of group 0002 end. PS3.10 asks for Explicit VR Little Endian; when the first element has no VR letters where
			///the VR goes, the group is read as Implicit VR Little Endian, as some writers wrote it.</summary>
			private void ReadFileMeta() {
				byte[] bytesFirst=_source.Peek(8);
				bool isImplicit=bytesFirst.Length==8 && U16(bytesFirst,0,false)==0x0002 && !IsExplicitHeader(bytesFirst);
				while(true) {
					byte[] bytesPeek=_source.Peek(2);
					if(bytesPeek.Length<2 || U16(bytesPeek,0,false)!=0x0002) {
						return;
					}
					ReadElementHeader(isImplicit,false,out uint tag,out string vr,out uint length);
					if(length==_undefinedLength) {
						throw Malformed("File Meta element "+FormatTag(tag)+" has an undefined length.");
					}
					if(tag==_tagTransferSyntaxUid) {
						if(length>256) {
							throw Malformed("The Transfer Syntax UID is "+FormatCount(length)+" bytes long.");
						}
						TransferSyntaxUid=Encoding.ASCII.GetString(_source.ReadBytes((int)length,"the Transfer Syntax UID")).Trim('\0',' ');
						if(TransferSyntaxUid=="") {
							TransferSyntaxUid=null;
						}
						continue;
					}
					_source.Skip(length,"File Meta element "+FormatTag(tag));
				}
			}

			///<summary>A data set without a Transfer Syntax UID: Explicit VR when the first element has VR letters (or two spaces) where the VR
			///goes (PS3.5 7.1.2), big endian when its group reads smaller that way, otherwise Implicit VR Little Endian. The first element must
			///look like the start of a data set: a group from 0008 to 00FF, standard or private (groups below 0008 are command and File Meta
			///groups, PS3.5 7.1; a file of zeros has group 0000), and in Implicit VR a length that fits the file. Otherwise the file is not
			///DICOM.</summary>
			private void DetectEncoding() {
				byte[] bytesPeek=_source.Peek(8);
				string notDicom="Neither a PS3.10 file (no \"DICM\" prefix after a 128-byte preamble) nor a DICOM data set.";
				if(bytesPeek.Length<8) {
					throw new DeclineException(DicomImageReadStatus.NotDicom,"The file is too short to be DICOM.");
				}
				bool isExplicit=IsExplicitHeader(bytesPeek);
				ushort groupLittle=U16(bytesPeek,0,false);
				ushort groupBig=U16(bytesPeek,0,true);
				bool isBig=isExplicit && groupBig<groupLittle;
				ushort group=isBig ? groupBig : groupLittle;
				if(group<0x0008 || group>0x00FF) {
					throw new DeclineException(DicomImageReadStatus.NotDicom,notDicom);
				}
				if(!isExplicit && !IsImplicitLengthThatFits(bytesPeek)) {
					throw new DeclineException(DicomImageReadStatus.NotDicom,notDicom);
				}
				_isImplicit=!isExplicit;
				_isBigEndian=isBig;
			}
			#endregion File header and encoding

			#region Elements
			///<summary>The tag, VR and length of the element at the current position (PS3.5 7.1.2 and 7.1.3). In Implicit VR, the VR is the one
			///the reader knows for the attributes it keeps, otherwise "UN". In Explicit VR, two spaces where the VR goes take the VR the reader
			///knows for the tag the same way, with the layout of that VR: OW (two reserved bytes and a 32-bit length) for Pixel Data and the
			///palette tables, and a 16-bit length for every other element.</summary>
			private void ReadElementHeader(bool isImplicit,bool isBig,out uint tag,out string vr,out uint length) {
				CountHeader();
				byte[] bytes=new byte[8];
				_source.ReadExact(bytes,0,8,"an element header");
				tag=((uint)U16(bytes,0,isBig)<<16) | U16(bytes,2,isBig);
				if(isImplicit) {
					vr=ImplicitVr(tag);
					length=U32(bytes,4,false);
					return;
				}
				bool isLongLayout;
				if(IsVr(bytes[4],bytes[5])) {
					vr=""+(char)bytes[4]+(char)bytes[5];
					isLongLayout=_hashSetLongVrs.Contains(vr);
				}
				else if(IsBlankVr(bytes[4],bytes[5])) {
					vr=ImplicitVr(tag);
					isLongLayout=vr=="OW";
				}
				else {
					throw Malformed("Element "+FormatTag(tag)+" has no valid VR; the data set is not "+(isBig ? "Explicit VR Big Endian." : "Explicit VR."));
				}
				if(isLongLayout) {
					byte[] bytesLength=_source.ReadBytes(4,"an element header");
					length=U32(bytesLength,0,isBig);
				}
				else {
					length=U16(bytes,6,isBig);
				}
			}

			///<summary>The VRs of the attributes the reader keeps (PS3.6 Table 6-1), for Implicit VR.</summary>
			private static string ImplicitVr(uint tag) {
				switch(tag) {
					case _tagPhotometricInterpretation:
						return "CS";
					case _tagNumberOfFrames:
						return "IS";
					case _tagPixelData:
					case _tagRedData:
					case 0x00281202:
					case _tagBlueData:
						return "OW";
					case 0xFFFEE000:
					case 0xFFFEE00D:
					case 0xFFFEE0DD:
						return "";
				}
				if((tag>>16)==0x0028 && IsKept(tag)) {
					return "US";
				}
				return "UN";
			}

			private static bool IsKept(uint tag) {
				switch(tag) {
					case _tagSamplesPerPixel:
					case _tagPhotometricInterpretation:
					case _tagPlanarConfiguration:
					case _tagNumberOfFrames:
					case _tagRows:
					case _tagColumns:
					case _tagBitsAllocated:
					case _tagBitsStored:
					case _tagHighBit:
					case _tagPixelRepresentation:
					case _tagExtendedOffsetTable:
					case _tagExtendedOffsetTableLengths:
						return true;
				}
				return (tag>=_tagRedDescriptor && tag<=_tagBlueDescriptor) || (tag>=_tagRedData && tag<=_tagBlueData);
			}

			///<summary>The top-level data set: the image attributes are kept, everything else is skipped, and the pixel data ends the parse.</summary>
			private BitmapDicom ParseDataset() {
				while(true) {
					byte[] bytesPeek=_source.Peek(4);
					if(bytesPeek.Length==0) {
						throw Unsupported("The data set has no Pixel Data (7FE0,0010).");
					}
					if(bytesPeek.Length<4) {
						throw Malformed("Bytes left over after the last element.");
					}
					uint tagPeek=((uint)U16(bytesPeek,0,_isBigEndian)<<16) | U16(bytesPeek,2,_isBigEndian);
					if((tagPeek>>16)==0xFFFE) {
						throw Malformed("Item or delimiter "+FormatTag(tagPeek)+" outside a sequence.");
					}
					ReadElementHeader(_isImplicit,_isBigEndian,out uint tag,out string vr,out uint length);
					if(tag==_tagPixelData) {
						if(_streamInflated!=null) {
							_source.SetLimit(MaxInflatedBytes,"inflated data set");
						}
						return ReadPixelData(vr,length);
					}
					if(tag==_tagFloatPixelData || tag==_tagDoubleFloatPixelData) {
						throw Unsupported("The image has floating point pixel data.");
					}
					if(length==_undefinedLength) {
						SkipUndefinedLength(tag,vr,_isImplicit,_isBigEndian,1);
						continue;
					}
					if(tag>=_tagSegmentedRedData && tag<=_tagSegmentedBlueData) {
						_hasSegmentedPalette=true;
					}
					if(vr=="SQ" || !IsKept(tag)) {
						_source.Skip(length,"the value of element "+FormatTag(tag));
						continue;
					}
					Keep(tag,vr,length);
				}
			}

			///<summary>Skips an element of undefined length whose header was just read: a sequence (SQ, or any element in Implicit VR, PS3.5
			///7.5.1), UN holding Implicit VR Little Endian items (PS3.5 6.2.2), or OB/OW inside an item (an icon image's encapsulated pixels).</summary>
			private void SkipUndefinedLength(uint tag,string vr,bool isImplicit,bool isBig,int depth) {
				if(vr=="SQ" || isImplicit) {
					SkipSequence(isImplicit,isBig,depth);
					return;
				}
				if(vr=="UN") {
					SkipSequence(true,false,depth);
					return;
				}
				if((vr=="OB" || vr=="OW") && depth>1) {
					SkipSequence(isImplicit,isBig,depth);
					return;
				}
				throw Malformed("Element "+FormatTag(tag)+" has an undefined length but is not a sequence.");
			}

			///<summary>The items of a sequence of undefined length, up to its Sequence Delimitation Item. Items of defined length are skipped by
			///their length; items of undefined length are read element by element up to their Item Delimitation Item.</summary>
			private void SkipSequence(bool isImplicit,bool isBig,int depth) {
				if(depth>MaxSequenceDepth) {
					throw Malformed("Sequences are nested more than "+MaxSequenceDepth.ToString(CultureInfo.InvariantCulture)+" deep.");
				}
				while(true) {
					CountHeader();
					byte[] bytes=_source.ReadBytes(8,"a sequence of undefined length (no Sequence Delimitation Item)");
					uint tag=((uint)U16(bytes,0,isBig)<<16) | U16(bytes,2,isBig);
					uint length=U32(bytes,4,isBig);
					if(tag==_tagSequenceDelimitation) {
						return;
					}
					if(tag!=_tagItem) {
						throw Malformed("Expected an Item (FFFE,E000) in a sequence, found "+FormatTag(tag)+".");
					}
					if(length!=_undefinedLength) {
						_source.Skip(length,"an item");
						continue;
					}
					SkipItemElements(isImplicit,isBig,depth);
				}
			}

			private void SkipItemElements(bool isImplicit,bool isBig,int depth) {
				while(true) {
					byte[] bytesPeek=_source.Peek(4);
					if(bytesPeek.Length<4) {
						throw Malformed("An item of undefined length has no Item Delimitation Item.");
					}
					uint tagPeek=((uint)U16(bytesPeek,0,isBig)<<16) | U16(bytesPeek,2,isBig);
					if(tagPeek==_tagItemDelimitation) {
						CountHeader();
						_source.Skip(8,"the Item Delimitation Item");
						return;
					}
					if((tagPeek>>16)==0xFFFE) {
						throw Malformed("Unexpected "+FormatTag(tagPeek)+" inside an item.");
					}
					ReadElementHeader(isImplicit,isBig,out uint tag,out string vr,out uint length);
					if(length==_undefinedLength) {
						SkipUndefinedLength(tag,vr,isImplicit,isBig,depth+1);
						continue;
					}
					_source.Skip(length,"the value of element "+FormatTag(tag));
				}
			}

			///<summary>Reads the value of a kept attribute and stores it.</summary>
			private void Keep(uint tag,string vr,uint length) {
				if(tag==_tagExtendedOffsetTable || tag==_tagExtendedOffsetTableLengths) {
					//Only the first two entries matter for the first frame; the rest is skipped.
					int countRead=(int)Math.Min(length,16);
					byte[] bytesTable=_source.ReadBytes(countRead,"the Extended Offset Table");
					_source.Skip(length-countRead,"the Extended Offset Table");
					ulong[] arrayEntries=new ulong[countRead/8];
					for(int i=0;i<arrayEntries.Length;i++) {
						arrayEntries[i]=((ulong)U32(bytesTable,8*i+4,false)<<32) | U32(bytesTable,8*i,false);
					}
					if(tag==_tagExtendedOffsetTable) {
						_arrayExtendedOffsets=arrayEntries;
					}
					else {
						_arrayExtendedLengths=arrayEntries;
					}
					return;
				}
				if(length>_maxKeptValueBytes) {
					throw Malformed("The value of element "+FormatTag(tag)+" is "+FormatCount(length)+" bytes long.");
				}
				byte[] bytes=_source.ReadBytes((int)length,"the value of element "+FormatTag(tag));
				switch(tag) {
					case _tagSamplesPerPixel: _samplesPerPixel=GetUS(tag,bytes); break;
					case _tagPhotometricInterpretation: _photometricInterpretation=GetText(bytes); break;
					case _tagPlanarConfiguration: _planarConfiguration=GetUS(tag,bytes); break;
					case _tagNumberOfFrames: _numberOfFrames=GetIS(tag,bytes); break;
					case _tagRows: _rows=GetUS(tag,bytes); break;
					case _tagColumns: _columns=GetUS(tag,bytes); break;
					case _tagBitsAllocated: _bitsAllocated=GetUS(tag,bytes); break;
					case _tagBitsStored: _bitsStored=GetUS(tag,bytes); break;
					case _tagHighBit: _highBit=GetUS(tag,bytes); break;
					case _tagPixelRepresentation: _pixelRepresentation=GetUS(tag,bytes); break;
					default:
						if(tag>=_tagRedDescriptor && tag<=_tagBlueDescriptor) {
							if(bytes.Length!=6) {
								throw Malformed("Palette Color Lookup Table Descriptor "+FormatTag(tag)+" is not three 16-bit values.");
							}
							_arrayPaletteDescriptors[tag-_tagRedDescriptor]=new[] { U16(bytes,0,_isBigEndian),U16(bytes,2,_isBigEndian),U16(bytes,4,_isBigEndian) };
						}
						else if(tag>=_tagRedData && tag<=_tagBlueData) {
							_arrayPaletteData[tag-_tagRedData]=_isBigEndian ? SwapWords(bytes) : bytes;
						}
						break;
				}
			}

			///<summary>The first 16-bit value; null for an empty value. A value that is not 16 bits or a multiple of them is broken.</summary>
			private ushort? GetUS(uint tag,byte[] bytes) {
				if(bytes.Length==0) {
					return null;
				}
				if(bytes.Length%2!=0) {
					throw Malformed("Element "+FormatTag(tag)+" is not a 16-bit value.");
				}
				return U16(bytes,0,_isBigEndian);
			}

			private static string GetText(byte[] bytes) {
				return Encoding.ASCII.GetString(bytes).Trim(' ','\0');
			}

			private static int? GetIS(uint tag,byte[] bytes) {
				string text=GetText(bytes);
				if(text=="") {
					return null;
				}
				if(!int.TryParse(text,NumberStyles.AllowLeadingSign|NumberStyles.AllowLeadingWhite|NumberStyles.AllowTrailingWhite,CultureInfo.InvariantCulture,
					out int value))
				{
					throw Malformed("Element "+FormatTag(tag)+" is not an integer string: "+text);
				}
				return value;
			}

			private static byte[] SwapWords(byte[] bytes) {
				byte[] bytesSwapped=(byte[])bytes.Clone();
				for(int i=0;i+1<bytes.Length;i+=2) {
					bytesSwapped[i]=bytes[i+1];
					bytesSwapped[i+1]=bytes[i];
				}
				return bytesSwapped;
			}
			#endregion Elements

			#region Pixel data
			private BitmapDicom ReadPixelData(string vr,uint length) {
				bool isEncapsulatedSyntax=_transferSyntax!=null && _transferSyntax.Kind==DicomTransferSyntaxKind.EncapsulatedImage;
				if(isEncapsulatedSyntax) {
					IDicomFrameDecoder decoder=_registry.Find(_transferSyntax.Uid);
					if(decoder==null) {
						throw new DeclineException(DicomImageReadStatus.UnsupportedCompression,"The pixel data is compressed as "+_transferSyntax.Name
							+", which this version cannot decode yet.");
					}
					if(length!=_undefinedLength) {
						throw Malformed("The transfer syntax is "+_transferSyntax.Name+", but Pixel Data is not encapsulated.");
					}
					ImageLayout layoutFile=CheckLayout(GetFrameInfo(),isNativeLayout:false);
					byte[] bytesFrame=ReadFirstFrameFragments();
					DicomFrameDecodeResult decodeResult;
					try {
						decodeResult=decoder.Decode(_transferSyntax.Uid,bytesFrame,layoutFile.FrameInfo.Clone());
					}
					catch(Exception ex) {
						throw Malformed("The "+decoder.Name+" decoder failed: "+ex.Message);
					}
					if(decodeResult==null || !decodeResult.IsDecoded || decodeResult.Pixels==null || decodeResult.FrameInfo==null) {
						string reason=decodeResult==null ? "no result" : decodeResult.Reason;
						if(decodeResult!=null && decodeResult.IsUnsupported) {
							throw Unsupported(reason);
						}
						throw Malformed("The first frame could not be decoded: "+reason);
					}
					DicomFrameInfo frameInfoDecoded=decodeResult.FrameInfo;
					if(frameInfoDecoded.Rows!=layoutFile.FrameInfo.Rows || frameInfoDecoded.Columns!=layoutFile.FrameInfo.Columns) {
						throw Malformed("The decoder returned a frame of another size.");
					}
					ImageLayout layoutDecoded=CheckLayout(frameInfoDecoded,isNativeLayout:true);
					if(decodeResult.Pixels.Length<layoutDecoded.FrameBytes) {
						throw Malformed("The decoded frame has "+FormatCount(decodeResult.Pixels.Length)+" bytes; it needs "+FormatCount(layoutDecoded.FrameBytes)+".");
					}
					return ToBitmapDicom(decodeResult.Pixels,layoutDecoded);
				}
				if(length==_undefinedLength) {
					throw Malformed("The pixel data is encapsulated, but the transfer syntax "+(_transferSyntax==null ? "(none in the file)" : _transferSyntax.Name)
						+" is not a compressed one.");
				}
				ImageLayout layout=CheckLayout(GetFrameInfo(),isNativeLayout:true);
				if(length<layout.FrameBytes) {
					throw Malformed("Pixel Data has "+FormatCount(length)+" bytes; one frame of "+layout.FrameInfo.Columns+" x "+layout.FrameInfo.Rows+" needs "
						+FormatCount(layout.FrameBytes)+".");
				}
				long remaining=_source.Remaining;
				if(remaining>=0 && length>remaining) {
					throw Malformed("Pixel Data says it has "+FormatCount(length)+" bytes, but the file has "+FormatCount(remaining)+" left.");
				}
				byte[] bytesPixels=_source.ReadBytes(layout.FrameBytes,"the pixel data");
				if(remaining<0) {
					//A stream that cannot tell its end: the rest of the declared length must really be there.
					_source.Skip(length-layout.FrameBytes,"Pixel Data");
				}
				if(_isBigEndian && vr=="OW") {
					//PS3.5 7.3: OW is sent as 16-bit words in big endian; the samples are read little-endian below.
					bytesPixels=SwapWords(bytesPixels);
				}
				return ToBitmapDicom(bytesPixels,layout);
			}

			///<summary>The attributes of the file, with the defaults the reader allows: High Bit Bits Stored - 1, Pixel Representation 0, Planar
			///Configuration 0, Samples per Pixel from the photometric interpretation, one frame. Rows, Columns, Bits Allocated, Bits Stored and
			///Photometric Interpretation have no default.</summary>
			private DicomFrameInfo GetFrameInfo() {
				if(_photometricInterpretation==null || _photometricInterpretation=="") {
					throw Malformed("Photometric Interpretation (0028,0004) is missing.");
				}
				if(_rows==null || _columns==null || _bitsAllocated==null || _bitsStored==null) {
					throw Malformed("An Image Pixel attribute is missing (Rows, Columns, Bits Allocated or Bits Stored).");
				}
				if(_numberOfFrames!=null && _numberOfFrames<1) {
					throw Malformed("Number of Frames is "+_numberOfFrames+".");
				}
				DicomFrameInfo frameInfo=new DicomFrameInfo();
				frameInfo.Rows=_rows.Value;
				frameInfo.Columns=_columns.Value;
				frameInfo.PhotometricInterpretation=_photometricInterpretation;
				bool isColor=_photometricInterpretation=="RGB" || _photometricInterpretation.StartsWith("YBR_");
				frameInfo.SamplesPerPixel=_samplesPerPixel ?? (isColor ? 3 : 1);
				frameInfo.PlanarConfiguration=_planarConfiguration ?? 0;
				frameInfo.BitsAllocated=_bitsAllocated.Value;
				frameInfo.BitsStored=_bitsStored.Value;
				frameInfo.HighBit=_highBit ?? (_bitsStored.Value-1);
				frameInfo.PixelRepresentation=_pixelRepresentation ?? 0;
				frameInfo.NumberOfFrames=_numberOfFrames ?? 1;
				frameInfo.FrameIndex=0;
				return frameInfo;
			}

			///<summary>Checks the attributes against what the reader shows and computes the size of one frame of native pixel data (PS3.5 8.1.1;
			///PS3.3 C.7.6.3). isNativeLayout is false for the attributes of encapsulated pixel data before decoding: then only the size, the bits
			///and the samples are checked, every photometric interpretation PS3.5 8.2 allows for compressed data is accepted (YBR_PARTIAL_422,
			///YBR_PARTIAL_420, YBR_ICT and YBR_RCT too, which describe the compressed stream), and the rules of what is shown apply to the decoder's
			///answer, which is checked again as native pixel data. YBR_FULL_422 there describes the stream's subsampling (PS3.5 8.2.1), so its
			///native layout rules do not apply either.</summary>
			private ImageLayout CheckLayout(DicomFrameInfo frameInfo,bool isNativeLayout) {
				ImageLayout layout=new ImageLayout();
				layout.FrameInfo=frameInfo;
				switch(frameInfo.PhotometricInterpretation) {
					case "MONOCHROME1": layout.Kind=PhotometricKind.Monochrome1; break;
					case "MONOCHROME2": layout.Kind=PhotometricKind.Monochrome2; break;
					case "PALETTE COLOR": layout.Kind=PhotometricKind.Palette; break;
					case "RGB": layout.Kind=PhotometricKind.Rgb; break;
					case "YBR_FULL": layout.Kind=PhotometricKind.YbrFull; break;
					case "YBR_FULL_422": layout.Kind=PhotometricKind.YbrFull422; break;
					case "YBR_PARTIAL_422":
					case "YBR_PARTIAL_420":
					case "YBR_ICT":
					case "YBR_RCT":
						if(isNativeLayout) {
							throw Unsupported("Photometric Interpretation "+frameInfo.PhotometricInterpretation+" is not one this version shows.");
						}
						layout.Kind=PhotometricKind.YbrCompressed;
						break;
					default:
						throw Unsupported("Photometric Interpretation "+frameInfo.PhotometricInterpretation+" is not one this version shows.");
				}
				bool isColor=layout.Kind==PhotometricKind.Rgb || layout.Kind==PhotometricKind.YbrFull || layout.Kind==PhotometricKind.YbrFull422
					|| layout.Kind==PhotometricKind.YbrCompressed;
				if(frameInfo.SamplesPerPixel!=(isColor ? 3 : 1)) {
					throw Malformed("Samples per Pixel is "+frameInfo.SamplesPerPixel+" with Photometric Interpretation "+frameInfo.PhotometricInterpretation+".");
				}
				if(frameInfo.Rows<1 || frameInfo.Columns<1) {
					throw Malformed("Rows or Columns is 0.");
				}
				long countPixels=(long)frameInfo.Rows*frameInfo.Columns;
				if(countPixels>MaxFramePixels) {
					throw TooLarge("The image is "+frameInfo.Columns+" x "+frameInfo.Rows+" pixels; this version shows at most "+FormatCount(MaxFramePixels)+".");
				}
				int bitsAllocated=frameInfo.BitsAllocated;
				if(bitsAllocated!=8 && bitsAllocated!=16) {
					throw Unsupported("Bits Allocated is "+bitsAllocated+"; this version shows 8 or 16.");
				}
				if(frameInfo.BitsStored<1 || frameInfo.BitsStored>bitsAllocated) {
					throw Malformed("Bits Stored is "+frameInfo.BitsStored+" with Bits Allocated "+bitsAllocated+".");
				}
				if(frameInfo.HighBit<frameInfo.BitsStored-1 || frameInfo.HighBit>=bitsAllocated) {
					throw Malformed("High Bit is "+frameInfo.HighBit+" with Bits Stored "+frameInfo.BitsStored+" and Bits Allocated "+bitsAllocated+".");
				}
				if(frameInfo.PixelRepresentation!=0 && frameInfo.PixelRepresentation!=1) {
					throw Malformed("Pixel Representation is "+frameInfo.PixelRepresentation+".");
				}
				if(isNativeLayout && frameInfo.PixelRepresentation==1 && (isColor || layout.Kind==PhotometricKind.Palette)) {
					throw Unsupported("Signed color or palette pixel values are not supported.");
				}
				if(isColor && frameInfo.PlanarConfiguration!=0 && frameInfo.PlanarConfiguration!=1) {
					throw Malformed("Planar Configuration is "+frameInfo.PlanarConfiguration+".");
				}
				layout.CountPixels=(int)countPixels;
				if(layout.Kind==PhotometricKind.YbrFull422 && isNativeLayout) {
					if(bitsAllocated!=8) {
						throw Unsupported("YBR_FULL_422 with "+bitsAllocated+" bits allocated is not supported.");
					}
					if(frameInfo.Columns%2!=0) {
						throw Malformed("YBR_FULL_422 needs an even number of columns.");
					}
					layout.FrameBytes=layout.CountPixels*2;//Y Y Cb Cr for each two pixels (PS3.3 C.7.6.3.1.2)
				}
				else {
					layout.FrameBytes=layout.CountPixels*frameInfo.SamplesPerPixel*(bitsAllocated/8);
				}
				return layout;
			}
			#endregion Pixel data

			#region Encapsulated pixel data
			///<summary>The bytes of the first frame of encapsulated pixel data (PS3.5 A.4): the Basic Offset Table item, then the fragments that
			///make up frame 0. Where frame 0 ends: the second offset of the Basic Offset Table or of the Extended Offset Table; with one frame,
			///every fragment; with one fragment per frame (RLE, Encapsulated Uncompressed), the first fragment; otherwise the first fragment that
			///ends with the JPEG family's end marker FFD9 (EOI, and the EOC of JPEG 2000), as PS3.5 A.4 lets a reader find frames.</summary>
			private byte[] ReadFirstFrameFragments() {
				CountHeader();
				byte[] bytesItem=_source.ReadBytes(8,"the Basic Offset Table");
				uint tagTable=((uint)U16(bytesItem,0,false)<<16) | U16(bytesItem,2,false);
				uint lengthTable=U32(bytesItem,4,false);
				if(tagTable!=_tagItem) {
					throw Malformed("Encapsulated pixel data does not start with the Basic Offset Table item.");
				}
				if(lengthTable==_undefinedLength || lengthTable%4!=0) {
					throw Malformed("The Basic Offset Table has a length of "+FormatCount(lengthTable)+".");
				}
				CheckFits(lengthTable,"The Basic Offset Table");
				int countTableRead=(int)Math.Min(lengthTable,8);
				byte[] bytesTable=_source.ReadBytes(countTableRead,"the Basic Offset Table");
				_source.Skip(lengthTable-countTableRead,"the Basic Offset Table");
				long offsetStart=0;
				long offsetEnd=long.MaxValue;
				bool isFirstFragmentOnly=false;
				bool isUntilEndMarker=false;
				int countFrames=_numberOfFrames ?? 1;
				if(lengthTable>=4) {
					offsetStart=U32(bytesTable,0,false);
					if(lengthTable>=8 && countFrames>1) {
						offsetEnd=U32(bytesTable,4,false);
					}
				}
				else if(_arrayExtendedOffsets!=null && _arrayExtendedOffsets.Length>0) {
					offsetStart=(long)Math.Min(_arrayExtendedOffsets[0],long.MaxValue);
					if(_arrayExtendedOffsets.Length>1 && countFrames>1) {
						offsetEnd=(long)Math.Min(_arrayExtendedOffsets[1],long.MaxValue);
					}
				}
				else if(countFrames>1) {
					if(_transferSyntax.IsOneFragmentPerFrame) {
						isFirstFragmentOnly=true;
					}
					else {
						isUntilEndMarker=true;
					}
				}
				if(offsetEnd<=offsetStart) {
					throw Malformed("The offset table puts the second frame before the first.");
				}
				List<byte[]> listFragments=new List<byte[]>();
				long countBytes=0;
				long offset=0;//of the current item's tag, from the first fragment's item tag
				while(true) {
					if(offset>=offsetEnd && listFragments.Count>0) {
						break;
					}
					CountHeader();
					byte[] bytesHeader=_source.ReadBytes(8,"the encapsulated pixel data (no Sequence Delimitation Item)");
					uint tag=((uint)U16(bytesHeader,0,false)<<16) | U16(bytesHeader,2,false);
					uint length=U32(bytesHeader,4,false);
					if(tag==_tagSequenceDelimitation) {
						break;
					}
					if(tag!=_tagItem) {
						throw Malformed("Expected a fragment item in the encapsulated pixel data, found "+FormatTag(tag)+".");
					}
					if(length==_undefinedLength) {
						throw Malformed("A fragment of the encapsulated pixel data has an undefined length.");
					}
					CheckFits(length,"A fragment");
					if(offset<offsetStart) {
						_source.Skip(length,"a fragment");
						offset+=8+(long)length;
						continue;
					}
					if(countBytes+length>MaxCompressedFrameBytes) {
						throw TooLarge("The first frame has more than "+FormatCount(MaxCompressedFrameBytes/1024/1024)+" MB of compressed data.");
					}
					if(listFragments.Count>=MaxFragmentsPerFrame) {
						throw Malformed("The first frame has more than "+FormatCount(MaxFragmentsPerFrame)+" fragments.");
					}
					byte[] bytesFragment=_source.ReadBytes((int)length,"a fragment of the encapsulated pixel data");
					listFragments.Add(bytesFragment);
					countBytes+=length;
					offset+=8+(long)length;
					if(isFirstFragmentOnly || (isUntilEndMarker && EndsWithEndMarker(bytesFragment))) {
						break;
					}
				}
				if(listFragments.Count==0) {
					throw Malformed("The encapsulated pixel data has no fragment for the first frame.");
				}
				if(listFragments.Count==1) {
					return listFragments[0];
				}
				byte[] bytesFrame=new byte[countBytes];
				long position=0;
				foreach(byte[] bytesFragment in listFragments) {
					Buffer.BlockCopy(bytesFragment,0,bytesFrame,(int)position,bytesFragment.Length);
					position+=bytesFragment.Length;
				}
				return bytesFrame;
			}

			private void CheckFits(uint length,string what) {
				long remaining=_source.Remaining;
				if(remaining>=0 && length>remaining) {
					throw Malformed(what+" says it has "+FormatCount(length)+" bytes, but the file has "+FormatCount(remaining)+" left.");
				}
			}

			///<summary>FFD9 at the end, or before one pad byte of 00 (fragments have even lengths, PS3.5 A.4).</summary>
			private static bool EndsWithEndMarker(byte[] bytes) {
				int n=bytes.Length;
				if(n>=2 && bytes[n-2]==0xFF && bytes[n-1]==0xD9) {
					return true;
				}
				return n>=3 && bytes[n-3]==0xFF && bytes[n-2]==0xD9 && bytes[n-1]==0x00;
			}
			#endregion Encapsulated pixel data

			#region To BitmapDicom
			///<summary>One frame of native little-endian pixels into a BitmapDicom (see the class summary for the rules).</summary>
			private BitmapDicom ToBitmapDicom(byte[] bytes,ImageLayout layout) {
				DicomFrameInfo frameInfo=layout.FrameInfo;
				BitmapDicom bitmapDicom=new BitmapDicom();
				bitmapDicom.Width=frameInfo.Columns;
				bitmapDicom.Height=frameInfo.Rows;
				if(layout.Kind==PhotometricKind.Monochrome1 || layout.Kind==PhotometricKind.Monochrome2) {
					int bitsTarget=GetGrayBitsShown(frameInfo.BitsAllocated,frameInfo.BitsStored);
					bitmapDicom.ArrayDataRaw=ReadGray(bytes,layout,bitsTarget);
					bitmapDicom.BitsAllocated=(ushort)frameInfo.BitsAllocated;
					bitmapDicom.BitsStored=(ushort)bitsTarget;
					bitmapDicom.PhotometricInterp=layout.Kind==PhotometricKind.Monochrome1 ? EnumDicomPhotometricInterp.MONOCHROME1 : EnumDicomPhotometricInterp.MONOCHROME2;
					return bitmapDicom;
				}
				byte[] arrayRgb;
				if(layout.Kind==PhotometricKind.Palette) {
					arrayRgb=ReadPalette(bytes,layout,GetPalette());
				}
				else if(layout.Kind==PhotometricKind.YbrFull422) {
					arrayRgb=ReadYbrFull422(bytes,layout);
				}
				else {
					arrayRgb=ReadColor(bytes,layout);
				}
				//As the fo-dicom path did for RGB: the gray of each pixel is the average of red, green and blue.
				ushort[] arrayGray=new ushort[layout.CountPixels];
				for(int p=0;p<arrayGray.Length;p++) {
					arrayGray[p]=(ushort)((arrayRgb[3*p]+arrayRgb[3*p+1]+arrayRgb[3*p+2])/3);
				}
				bitmapDicom.ArrayDataRaw=arrayGray;
				bitmapDicom.BitsAllocated=8;
				bitmapDicom.BitsStored=8;
				bitmapDicom.PhotometricInterp=EnumDicomPhotometricInterp.RGB;
				return bitmapDicom;
			}

			///<summary>The Bits Stored a gray image is shown with: 8, 12 or 16, the depths BitmapDicom's windowing knows. 8 bits allocated show 8;
			///in 16, Bits Stored up to 8 shows 8 (8 itself as the old path did), 9 to 12 shows 12, 13 to 16 shows 16.</summary>
			private static int GetGrayBitsShown(int bitsAllocated,int bitsStored) {
				if(bitsAllocated==8 || bitsStored<=8) {
					return 8;
				}
				if(bitsStored<=12) {
					return 12;
				}
				return 16;
			}

			///<summary>The stored value of sample idxSample (PS3.5 8.1.1): taken from High Bit down, the other bits cleared.</summary>
			private static int ReadStored(byte[] bytes,long idxSample,int bitsAllocated,int shift,int mask) {
				int word=bitsAllocated==8 ? bytes[idxSample] : bytes[2*idxSample] | (bytes[2*idxSample+1]<<8);
				return (word>>shift) & mask;
			}

			private static ushort[] ReadGray(byte[] bytes,ImageLayout layout,int bitsTarget) {
				DicomFrameInfo frameInfo=layout.FrameInfo;
				int bitsStored=frameInfo.BitsStored;
				int shift=frameInfo.HighBit-bitsStored+1;
				int mask=bitsStored==32 ? -1 : (1<<bitsStored)-1;
				int signBit=1<<(bitsStored-1);
				int shiftUp=bitsTarget-bitsStored;
				bool isSigned=frameInfo.PixelRepresentation==1;
				ushort[] arrayValues=new ushort[layout.CountPixels];
				for(int i=0;i<arrayValues.Length;i++) {
					int value=ReadStored(bytes,i,frameInfo.BitsAllocated,shift,mask);
					if(isSigned) {
						//Two's complement of Bits Stored bits, moved up by half the range: the most negative value becomes 0.
						value=(value ^ signBit);
					}
					arrayValues[i]=(ushort)(value<<shiftUp);
				}
				return arrayValues;
			}

			///<summary>RGB or YBR_FULL samples, interleaved or planar, at 8 bits, as RGB triples.</summary>
			private static byte[] ReadColor(byte[] bytes,ImageLayout layout) {
				DicomFrameInfo frameInfo=layout.FrameInfo;
				int bitsStored=frameInfo.BitsStored;
				int shift=frameInfo.HighBit-bitsStored+1;
				int mask=(1<<bitsStored)-1;
				int countPixels=layout.CountPixels;
				byte[] arrayRgb=new byte[(long)countPixels*3];
				for(int p=0;p<countPixels;p++) {
					for(int c=0;c<3;c++) {
						long idxSample=frameInfo.PlanarConfiguration==0 ? (long)p*3+c : (long)c*countPixels+p;
						arrayRgb[3*p+c]=To8Bits(ReadStored(bytes,idxSample,frameInfo.BitsAllocated,shift,mask),bitsStored);
					}
					if(layout.Kind==PhotometricKind.YbrFull) {
						YbrToRgb(arrayRgb,3*p,arrayRgb[3*p],arrayRgb[3*p+1],arrayRgb[3*p+2]);
					}
				}
				return arrayRgb;
			}

			private static byte To8Bits(int value,int bitsStored) {
				if(bitsStored>8) {
					return (byte)(value>>(bitsStored-8));
				}
				return (byte)(value<<(8-bitsStored));
			}

			///<summary>YBR_FULL_422 (PS3.3 C.7.6.3.1.2): for each two pixels of a row, Y1 Y2 Cb Cr.</summary>
			private static byte[] ReadYbrFull422(byte[] bytes,ImageLayout layout) {
				int countPixels=layout.CountPixels;
				byte[] arrayRgb=new byte[(long)countPixels*3];
				for(int pair=0;pair<countPixels/2;pair++) {
					int at=4*pair;
					byte cb=bytes[at+2];
					byte cr=bytes[at+3];
					YbrToRgb(arrayRgb,6*pair,bytes[at],cb,cr);
					YbrToRgb(arrayRgb,6*pair+3,bytes[at+1],cb,cr);
				}
				return arrayRgb;
			}

			///<summary>YBR_FULL to RGB, the inverse of the equations of PS3.3 C.7.6.3.1.2 (Y = .2990R + .5870G + .1140B, Cb = -.1687R - .3313G
			///+ .5B + 128, Cr = .5R - .4187G - .0813B + 128), rounded and kept within 0 to 255.</summary>
			private static void YbrToRgb(byte[] arrayRgb,int at,int y,int cb,int cr) {
				double cbCentered=cb-128;
				double crCentered=cr-128;
				arrayRgb[at]=ClampToByte(y+1.402*crCentered);
				arrayRgb[at+1]=ClampToByte(y-0.344136*cbCentered-0.714136*crCentered);
				arrayRgb[at+2]=ClampToByte(y+1.772*cbCentered);
			}

			private static byte ClampToByte(double value) {
				double rounded=Math.Round(value,MidpointRounding.AwayFromZero);
				if(rounded<0) {
					return 0;
				}
				if(rounded>255) {
					return 255;
				}
				return (byte)rounded;
			}

			///<summary>The three palette tables, checked against their descriptors (PS3.3 C.7.6.3.1.5 and C.7.6.3.1.6).</summary>
			private Palette GetPalette() {
				if(_hasSegmentedPalette && _arrayPaletteData.Any(x => x==null)) {
					throw Unsupported("Segmented palette color lookup tables are not supported.");
				}
				Palette palette=new Palette();
				for(int color=0;color<3;color++) {
					ushort[] arrayDescriptor=_arrayPaletteDescriptors[color];
					byte[] bytesData=_arrayPaletteData[color];
					if(arrayDescriptor==null || bytesData==null) {
						throw Malformed("PALETTE COLOR without its Palette Color Lookup Table Descriptors and Data (0028,1101-1103, 0028,1201-1203).");
					}
					int countEntries=arrayDescriptor[0]==0 ? 65536 : arrayDescriptor[0];
					int bitsPerEntry=arrayDescriptor[2];
					if(color==0) {
						palette.FirstMapped=arrayDescriptor[1];
					}
					else if(arrayDescriptor[1]!=palette.FirstMapped) {
						throw Malformed("The palette tables map different first values.");
					}
					byte[] arrayTable=new byte[countEntries];
					if(bitsPerEntry==16) {
						if(bytesData.Length<2*countEntries) {
							throw Malformed("A palette table has fewer entries than its descriptor says.");
						}
						for(int i=0;i<countEntries;i++) {
							arrayTable[i]=bytesData[2*i+1];//the high byte of each 16-bit entry
						}
					}
					else if(bitsPerEntry==8) {
						if(bytesData.Length>=2*countEntries && bytesData.Length>countEntries+1) {
							//Each 8-bit entry in a 16-bit word, as some writers did.
							for(int i=0;i<countEntries;i++) {
								int word=bytesData[2*i] | (bytesData[2*i+1]<<8);
								arrayTable[i]=(byte)(word>255 ? word>>8 : word);
							}
						}
						else if(bytesData.Length>=countEntries) {
							Buffer.BlockCopy(bytesData,0,arrayTable,0,countEntries);
						}
						else {
							throw Malformed("A palette table has fewer entries than its descriptor says.");
						}
					}
					else {
						throw Malformed("A palette table has "+bitsPerEntry+" bits per entry; it must be 8 or 16.");
					}
					palette.ArrayTables[color]=arrayTable;
				}
				return palette;
			}

			///<summary>Palette indices through the tables: an index below the first mapped value takes the first entry, one past the last entry
			///takes the last (PS3.3 C.7.6.3.1.5).</summary>
			private static byte[] ReadPalette(byte[] bytes,ImageLayout layout,Palette palette) {
				DicomFrameInfo frameInfo=layout.FrameInfo;
				int bitsStored=frameInfo.BitsStored;
				int shift=frameInfo.HighBit-bitsStored+1;
				int mask=(1<<bitsStored)-1;
				byte[] arrayRgb=new byte[(long)layout.CountPixels*3];
				for(int p=0;p<layout.CountPixels;p++) {
					int index=ReadStored(bytes,p,frameInfo.BitsAllocated,shift,mask)-palette.FirstMapped;
					for(int color=0;color<3;color++) {
						byte[] arrayTable=palette.ArrayTables[color];
						int idxEntry=Math.Max(0,Math.Min(arrayTable.Length-1,index));
						arrayRgb[3*p+color]=arrayTable[idxEntry];
					}
				}
				return arrayRgb;
			}
			#endregion To BitmapDicom
		}
	}
}
