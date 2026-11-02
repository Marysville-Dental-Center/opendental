// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenPmsDentalBusiness {
	///<summary>What DicomSubsetReader did with one file.</summary>
	public enum DicomSubsetReadStatus {
		///<summary>Read: the result has the pixels.</summary>
		Read,
		///<summary>Not the subset's encoding: no PS3.10 preamble and prefix, another transfer syntax, compressed (encapsulated) pixel data, a
		///UN element of undefined length, or a file too large for the subset reader. Another reader may read it.</summary>
		NotSubset,
		///<summary>In the subset's encoding, but an image the subset reader does not take: color, signed values, several frames, a bit depth
		///other than 8, 12 or 16 bits stored in 8 or 16 bits allocated, a High Bit other than Bits Stored - 1, or no pixel data at all.</summary>
		Unsupported,
		///<summary>The encoding is broken: a length past the end, an unknown VR, an odd structure, tags out of order, a missing attribute, or
		///pixel data shorter than Rows x Columns.</summary>
		Malformed,
		///<summary>The file or stream could not be read.</summary>
		FileError,
	}

	///<summary>The outcome of DicomSubsetReader: the pixels, or why there are none.</summary>
	public sealed class DicomSubsetReadResult {
		public DicomSubsetReadStatus Status { get; }
		///<summary>Why the file was not read, as a sentence. "" when Status is Read.</summary>
		public string Reason { get; }
		///<summary>The pixels; null unless Status is Read.</summary>
		public BitmapDicom BitmapDicom { get; }

		public DicomSubsetReadResult(DicomSubsetReadStatus status,string reason,BitmapDicom bitmapDicom) {
			Status=status;
			Reason=reason ?? "";
			BitmapDicom=bitmapDicom;
		}

		public override string ToString() {
			return Status==DicomSubsetReadStatus.Read ? "Read" : Status+": "+Reason;
		}
	}

	///<summary>The in-house DICOM reader of design 7.8 (CAP-DR, owner decision 7a). Written from the public DICOM standard only (NEMA PS3.5 Data
	///Structures and Encoding, PS3.10 Media Storage and File Format; the attributes of PS3.3 C.7.6.3 Image Pixel), with no DICOM library. It
	///reads exactly one subset, the one ImagingCapture's DicomIntraOralWriter writes:
	///- a PS3.10 file: a 128-byte preamble, the "DICM" prefix, then File Meta Information (group 0002, always Explicit VR Little Endian);
	///- Transfer Syntax UID (0002,0010) 1.2.840.10008.1.2.1, Explicit VR Little Endian, so the pixel data is native (uncompressed);
	///- one frame of grayscale pixels: Samples per Pixel 1, MONOCHROME1 or MONOCHROME2, Bits Allocated 8 or 16, Bits Stored 8, 12 or 16 (no
	///  more than Bits Allocated), High Bit = Bits Stored - 1, Pixel Representation 0 (unsigned), Number of Frames absent or 1, and Pixel Data
	///  (7FE0,0010) as OB or OW of defined length, at least Rows x Columns samples long (a trailing pad byte or more is ignored).
	///Every other attribute is skipped by its length, including sequences of defined or undefined length with items of defined or undefined
	///length, nested up to MaxSequenceDepth deep. The values it returns are the ones fo-dicom returned for the same file through the old
	///path, and the ones DicomImageReader returns: the stored samples, with the bits above High Bit cleared when Bits Stored is less than 16,
	///and no inversion for MONOCHROME1 (the windowing handles that, as before).
	///
	///Anything else is declined with a status and a reason (NotSubset, Unsupported, Malformed, FileError), never by an exception, so that
	///DicomHelper.PixelReader hands the file to the full reader, DicomImageReader. The reader allocates only what the file holds: it reads at most
	///MaxFileBytes, checks every length against the bytes that are really there before it uses it, and sizes the pixel array from Rows x
	///Columns only after checking that the pixel data holds that many samples.</summary>
	public sealed class DicomSubsetReader : IDicomPixelReader {
		///<summary>PS3.5 Annex A.2 (PS3.6 Table A-1).</summary>
		public const string TransferSyntaxExplicitVrLittleEndian="1.2.840.10008.1.2.1";
		///<summary>Files and streams larger than this are declined (NotSubset) without being read, so a huge file never fills memory twice;
		///the full reader, which streams, reads them instead. A 16-bit intra-oral image is a few megabytes.</summary>
		public const int MaxFileBytes=256*1024*1024;
		///<summary>Sequences nested deeper than this are declined (Malformed), so a hostile file cannot exhaust the stack.</summary>
		public const int MaxSequenceDepth=16;
		///<summary>How much of a file is read first to see whether it is in the subset at all. The File Meta Information is a few hundred bytes.</summary>
		private const int _headerBytes=64*1024;
		private const uint _undefinedLength=0xFFFFFFFF;
		private const uint _tagItem=0xFFFEE000;
		private const uint _tagItemDelimitation=0xFFFEE00D;
		private const uint _tagSequenceDelimitation=0xFFFEE0DD;
		private const uint _tagSamplesPerPixel=0x00280002;
		private const uint _tagPhotometricInterpretation=0x00280004;
		private const uint _tagNumberOfFrames=0x00280008;
		private const uint _tagRows=0x00280010;
		private const uint _tagColumns=0x00280011;
		private const uint _tagBitsAllocated=0x00280100;
		private const uint _tagBitsStored=0x00280101;
		private const uint _tagHighBit=0x00280102;
		private const uint _tagPixelRepresentation=0x00280103;
		private const uint _tagFloatPixelData=0x7FE00008;
		private const uint _tagDoubleFloatPixelData=0x7FE00009;
		private const uint _tagPixelData=0x7FE00010;
		///<summary>Every VR of PS3.5 Table 6.2-1. Anything else in the VR position means the file is not Explicit VR, or is broken.</summary>
		private static readonly string[] _arrayVrs={ "AE","AS","AT","CS","DA","DS","DT","FL","FD","IS","LO","LT","OB","OD","OF","OL","OV","OW",
			"PN","SH","SL","SQ","SS","ST","SV","TM","UC","UI","UL","UN","UR","US","UT","UV" };
		///<summary>The VRs with two reserved bytes and a 32-bit length (PS3.5 7.1.2, Table 7.1-1); all others have a 16-bit length.</summary>
		private static readonly string[] _arrayLongVrs={ "OB","OD","OF","OL","OV","OW","SQ","SV","UC","UN","UR","UT","UV" };

		///<summary>The pixels, or null when the file is not read (see ReadFileResult for why).</summary>
		public BitmapDicom ReadFile(string path) {
			return ReadFileResult(path).BitmapDicom;
		}

		///<summary>The pixels, or null when the stream is not read (see ReadStreamResult for why).</summary>
		public BitmapDicom ReadStream(Stream stream) {
			return ReadStreamResult(stream).BitmapDicom;
		}

		///<summary>Reads the file at path. Never throws.</summary>
		public static DicomSubsetReadResult ReadFileResult(string path) {
			try {
				using(FileStream fileStream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)) {
					return ReadStreamResult(fileStream);
				}
			}
			catch(Exception ex) {
				return new DicomSubsetReadResult(DicomSubsetReadStatus.FileError,"The file could not be read: "+ex.Message,null);
			}
		}

		///<summary>Reads a DICOM file from the stream's current position to its end. It looks at the first 64 KB before reading the rest, so a
		///file outside the subset costs little. It consumes what it reads; DicomPixelReaderChain sets the position back before the full reader reads.
		///Never throws.</summary>
		public static DicomSubsetReadResult ReadStreamResult(Stream stream) {
			try {
				if(stream==null || !stream.CanRead) {
					return new DicomSubsetReadResult(DicomSubsetReadStatus.FileError,"No stream to read.",null);
				}
				byte[] bytes;
				if(stream.CanSeek) {
					long remaining=stream.Length-stream.Position;
					if(remaining>MaxFileBytes) {
						return NotSubsetTooLarge();
					}
					int countHeader=(int)Math.Min(remaining,_headerBytes);
					byte[] bytesHeader=ReadUpTo(stream,countHeader);
					if(bytesHeader.Length<remaining) {
						DicomSubsetReadResult resultHeader=CheckHeader(bytesHeader);
						if(resultHeader!=null) {
							return resultHeader;
						}
					}
					bytes=new byte[remaining];
					Buffer.BlockCopy(bytesHeader,0,bytes,0,bytesHeader.Length);
					int countRest=ReadInto(stream,bytes,bytesHeader.Length);
					if(bytesHeader.Length+countRest<bytes.Length) {//the stream ended before its Length said
						Array.Resize(ref bytes,bytesHeader.Length+countRest);
					}
				}
				else {
					byte[] bytesAll=ReadUpTo(stream,MaxFileBytes+1);
					if(bytesAll.Length>MaxFileBytes) {
						return NotSubsetTooLarge();
					}
					bytes=bytesAll;
				}
				return Read(bytes);
			}
			catch(Exception ex) {
				return new DicomSubsetReadResult(DicomSubsetReadStatus.FileError,"The file could not be read: "+ex.Message,null);
			}
		}

		///<summary>Reads a complete DICOM file held in bytes. Never throws.</summary>
		public static DicomSubsetReadResult Read(byte[] bytes) {
			if(bytes==null) {
				return new DicomSubsetReadResult(DicomSubsetReadStatus.FileError,"No bytes to read.",null);
			}
			try {
				Parser parser=new Parser(bytes);
				return new DicomSubsetReadResult(DicomSubsetReadStatus.Read,"",parser.Parse());
			}
			catch(DeclineException ex) {
				return new DicomSubsetReadResult(ex.Status,ex.Message,null);
			}
			catch(Exception ex) {
				//Not expected: every length is checked first. Declining hands the file to the full reader.
				return new DicomSubsetReadResult(DicomSubsetReadStatus.Malformed,"The file could not be parsed ("+ex.GetType().Name+").",null);
			}
		}

		///<summary>Null when the first bytes of a longer file leave the question open; otherwise the NotSubset result that the whole file would
		///give too (no preamble, no File Meta Information, or another transfer syntax).</summary>
		private static DicomSubsetReadResult CheckHeader(byte[] bytesHeader) {
			try {
				Parser parser=new Parser(bytesHeader);
				parser.ParseFileMeta();
				return null;
			}
			catch(DeclineException ex) {
				if(ex.Status==DicomSubsetReadStatus.NotSubset) {
					return new DicomSubsetReadResult(ex.Status,ex.Message,null);
				}
				return null;//possibly cut off by the 64 KB limit; the whole file decides
			}
			catch(Exception) {
				return null;
			}
		}

		private static DicomSubsetReadResult NotSubsetTooLarge() {
			return new DicomSubsetReadResult(DicomSubsetReadStatus.NotSubset,
				"The file is larger than "+(MaxFileBytes/1024/1024).ToString(CultureInfo.InvariantCulture)+" MB.",null);
		}

		///<summary>Up to count bytes from the stream; fewer when it ends first.</summary>
		private static byte[] ReadUpTo(Stream stream,int count) {
			if(count<=_headerBytes) {
				byte[] bytes=new byte[count];
				int countRead=ReadInto(stream,bytes,0);
				if(countRead<count) {
					Array.Resize(ref bytes,countRead);
				}
				return bytes;
			}
			//A stream of unknown length: grow as the data arrives, never beyond count.
			using(MemoryStream memoryStream=new MemoryStream()) {
				byte[] buffer=new byte[81920];
				long total=0;
				while(total<count) {
					int countRead=stream.Read(buffer,0,(int)Math.Min(buffer.Length,count-total));
					if(countRead<=0) {
						break;
					}
					memoryStream.Write(buffer,0,countRead);
					total+=countRead;
				}
				return memoryStream.ToArray();
			}
		}

		///<summary>Fills bytes from offset to its end, or until the stream ends. Returns the count read.</summary>
		private static int ReadInto(Stream stream,byte[] bytes,int offset) {
			int total=0;
			while(offset+total<bytes.Length) {
				int countRead=stream.Read(bytes,offset+total,bytes.Length-offset-total);
				if(countRead<=0) {
					break;
				}
				total+=countRead;
			}
			return total;
		}

		///<summary>Ends the parse with a status and reason. Caught in Read and CheckHeader; it never leaves this class.</summary>
		private sealed class DeclineException : Exception {
			public DicomSubsetReadStatus Status { get; }

			public DeclineException(DicomSubsetReadStatus status,string reason) : base(reason) {
				Status=status;
			}
		}

		///<summary>One pass over the bytes. Positions are ints because the reader never holds more than MaxFileBytes.</summary>
		private sealed class Parser {
			private readonly byte[] _bytes;
			private int _pos;
			private ushort? _samplesPerPixel;
			private string _photometricInterpretation;
			private int? _numberOfFrames;
			private ushort? _rows;
			private ushort? _columns;
			private ushort? _bitsAllocated;
			private ushort? _bitsStored;
			private ushort? _highBit;
			private ushort? _pixelRepresentation;
			private int _pixelDataOffset=-1;
			private int _pixelDataLength;

			public Parser(byte[] bytes) {
				_bytes=bytes;
			}

			private static DeclineException NotSubset(string reason) {
				return new DeclineException(DicomSubsetReadStatus.NotSubset,reason);
			}

			private static DeclineException Unsupported(string reason) {
				return new DeclineException(DicomSubsetReadStatus.Unsupported,reason);
			}

			private static DeclineException Malformed(string reason) {
				return new DeclineException(DicomSubsetReadStatus.Malformed,reason);
			}

			private ushort U16(int at) {
				return (ushort)(_bytes[at] | (_bytes[at+1]<<8));
			}

			private uint U32(int at) {
				return (uint)_bytes[at] | ((uint)_bytes[at+1]<<8) | ((uint)_bytes[at+2]<<16) | ((uint)_bytes[at+3]<<24);
			}

			private uint TagAt(int at) {
				return ((uint)U16(at)<<16) | U16(at+2);
			}

			private static string FormatTag(uint tag) {
				return "("+(tag>>16).ToString("X4",CultureInfo.InvariantCulture)+","+(tag & 0xFFFF).ToString("X4",CultureInfo.InvariantCulture)+")";
			}

			///<summary>Whether length bytes from the current position stay within end.</summary>
			private bool Fits(long length,int end) {
				return length>=0 && _pos+length<=end;
			}

			///<summary>Reads the tag, VR and length of the element at the current position (PS3.5 7.1.2) and moves to its value.</summary>
			private void ReadHeader(int end,out uint tag,out string vr,out uint length) {
				if(!Fits(8,end)) {
					throw Malformed("An element header runs past the end at byte "+_pos.ToString(CultureInfo.InvariantCulture)+".");
				}
				tag=TagAt(_pos);
				byte vr1=_bytes[_pos+4];
				byte vr2=_bytes[_pos+5];
				vr=""+(char)vr1+(char)vr2;
				if(vr1<'A' || vr1>'Z' || vr2<'A' || vr2>'Z' || !_arrayVrs.Contains(vr)) {
					throw Malformed("Element "+FormatTag(tag)+" has no valid VR; the data set is not Explicit VR.");
				}
				if(_arrayLongVrs.Contains(vr)) {
					if(!Fits(12,end)) {
						throw Malformed("The header of element "+FormatTag(tag)+" runs past the end.");
					}
					if(U16(_pos+6)!=0) {
						throw Malformed("The reserved bytes of element "+FormatTag(tag)+" are not zero.");
					}
					length=U32(_pos+8);
					_pos+=12;
				}
				else {
					length=U16(_pos+6);
					_pos+=8;
				}
			}

			///<summary>The preamble, the "DICM" prefix and the File Meta Information (PS3.10 7.1). Checks that the transfer syntax is Explicit VR
			///Little Endian and leaves the position at the first data set element.</summary>
			public void ParseFileMeta() {
				if(_bytes.Length<132) {
					throw NotSubset("Shorter than the PS3.10 preamble and prefix.");
				}
				if(_bytes[128]!='D' || _bytes[129]!='I' || _bytes[130]!='C' || _bytes[131]!='M') {
					throw NotSubset("No PS3.10 \"DICM\" prefix after the 128-byte preamble.");
				}
				_pos=132;
				int end=_bytes.Length;
				string transferSyntax=null;
				long endGroupLength=-1;
				while(Fits(8,end) && U16(_pos)==0x0002) {
					ReadHeader(end,out uint tag,out string vr,out uint length);
					if(length==_undefinedLength || !Fits(length,end)) {
						throw Malformed("File Meta element "+FormatTag(tag)+" has a length past the end.");
					}
					if(tag==0x00020000) {
						if(vr!="UL" || length!=4) {
							throw Malformed("File Meta Information Group Length (0002,0000) is not one UL.");
						}
						endGroupLength=_pos+4+(long)U32(_pos);
					}
					else if(tag==0x00020010) {
						transferSyntax=Encoding.ASCII.GetString(_bytes,_pos,(int)length).Trim('\0',' ');
					}
					_pos+=(int)length;
				}
				if(_pos==132) {
					throw NotSubset("No File Meta Information (group 0002) after the prefix.");
				}
				if(transferSyntax==null) {
					throw Malformed("The File Meta Information has no Transfer Syntax UID (0002,0010).");
				}
				if(transferSyntax!=TransferSyntaxExplicitVrLittleEndian) {
					throw NotSubset("Transfer syntax "+transferSyntax+", not Explicit VR Little Endian.");
				}
				if(endGroupLength>=0 && endGroupLength!=_pos) {
					throw Malformed("File Meta Information Group Length (0002,0000) does not match the group.");
				}
			}

			public BitmapDicom Parse() {
				ParseFileMeta();
				ParseDataset(_bytes.Length);
				return BuildBitmap();
			}

			///<summary>The top-level data set: the image attributes are kept, every other element is skipped by its length.</summary>
			private void ParseDataset(int end) {
				uint tagPrevious=0;
				bool isFirst=true;
				while(_pos<end) {
					if(!Fits(8,end)) {
						throw Malformed("Bytes left over after the last element.");
					}
					uint tagPeek=TagAt(_pos);
					if((tagPeek>>16)==0xFFFE) {
						throw Malformed("Item or delimiter "+FormatTag(tagPeek)+" outside a sequence.");
					}
					ReadHeader(end,out uint tag,out string vr,out uint length);
					if(!isFirst && tag<=tagPrevious) {
						throw Malformed("Element "+FormatTag(tag)+" comes after "+FormatTag(tagPrevious)+"; tags must ascend (PS3.5 7.1).");
					}
					isFirst=false;
					tagPrevious=tag;
					if(vr=="SQ") {
						SkipSequence(length,end,1);
						continue;
					}
					if(length==_undefinedLength) {
						if(tag==_tagPixelData) {
							throw NotSubset("The pixel data is encapsulated (compressed).");
						}
						if(vr=="UN") {
							throw NotSubset("Element "+FormatTag(tag)+" is UN of undefined length.");
						}
						throw Malformed("Element "+FormatTag(tag)+" has an undefined length but is not a sequence.");
					}
					if(!Fits(length,end)) {
						throw Malformed("The value of element "+FormatTag(tag)+" runs past the end.");
					}
					Keep(tag,vr,(int)length);
					_pos+=(int)length;
				}
			}

			///<summary>Stores the value at the current position when it is one of the image attributes.</summary>
			private void Keep(uint tag,string vr,int length) {
				switch(tag) {
					case _tagSamplesPerPixel: _samplesPerPixel=GetUS(tag,vr,length); break;
					case _tagPhotometricInterpretation: _photometricInterpretation=GetText(length); break;
					case _tagNumberOfFrames: _numberOfFrames=GetIS(tag,length); break;
					case _tagRows: _rows=GetUS(tag,vr,length); break;
					case _tagColumns: _columns=GetUS(tag,vr,length); break;
					case _tagBitsAllocated: _bitsAllocated=GetUS(tag,vr,length); break;
					case _tagBitsStored: _bitsStored=GetUS(tag,vr,length); break;
					case _tagHighBit: _highBit=GetUS(tag,vr,length); break;
					case _tagPixelRepresentation: _pixelRepresentation=GetUS(tag,vr,length); break;
					case _tagFloatPixelData:
					case _tagDoubleFloatPixelData:
						throw Unsupported("The image has floating point pixel data.");
					case _tagPixelData:
						if(vr!="OB" && vr!="OW") {
							throw Unsupported("Pixel Data is "+vr+", not OB or OW.");
						}
						_pixelDataOffset=_pos;
						_pixelDataLength=length;
						break;
				}
			}

			///<summary>One US value (PS3.5 Table 6.2-1). Null for an empty value.</summary>
			private ushort? GetUS(uint tag,string vr,int length) {
				if(vr!="US") {
					throw Unsupported("Element "+FormatTag(tag)+" is "+vr+", not US.");
				}
				if(length==0) {
					return null;
				}
				if(length!=2) {
					throw Malformed("Element "+FormatTag(tag)+" is not a single US value.");
				}
				return U16(_pos);
			}

			///<summary>A string value without its padding (spaces, or a NUL).</summary>
			private string GetText(int length) {
				return Encoding.ASCII.GetString(_bytes,_pos,length).Trim(' ','\0');
			}

			private int? GetIS(uint tag,int length) {
				string text=GetText(length);
				if(text=="") {
					return null;
				}
				if(!int.TryParse(text,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out int value)) {
					throw Malformed("Element "+FormatTag(tag)+" is not an integer string: "+text);
				}
				return value;
			}

			///<summary>Skips a sequence whose header was just read (PS3.5 7.5): items of defined or undefined length up to the sequence's length,
			///or up to the Sequence Delimitation Item when the length is undefined.</summary>
			private void SkipSequence(uint length,int end,int depth) {
				if(depth>MaxSequenceDepth) {
					throw Malformed("Sequences are nested more than "+MaxSequenceDepth.ToString(CultureInfo.InvariantCulture)+" deep.");
				}
				if(length!=_undefinedLength) {
					if(!Fits(length,end)) {
						throw Malformed("A sequence runs past the end.");
					}
					int endSequence=_pos+(int)length;
					while(_pos<endSequence) {
						SkipItem(endSequence,depth);
					}
					return;
				}
				while(true) {
					if(!Fits(8,end)) {
						throw Malformed("A sequence of undefined length has no Sequence Delimitation Item.");
					}
					if(TagAt(_pos)==_tagSequenceDelimitation) {
						if(U32(_pos+4)!=0) {
							throw Malformed("The Sequence Delimitation Item has a length.");
						}
						_pos+=8;
						return;
					}
					SkipItem(end,depth);
				}
			}

			private void SkipItem(int end,int depth) {
				if(!Fits(8,end)) {
					throw Malformed("An item header runs past the end of its sequence.");
				}
				uint tag=TagAt(_pos);
				if(tag!=_tagItem) {
					throw Malformed("Expected an Item (FFFE,E000) in a sequence, found "+FormatTag(tag)+".");
				}
				uint length=U32(_pos+4);
				_pos+=8;
				if(length==_undefinedLength) {
					SkipElements(end,depth,true);
					return;
				}
				if(!Fits(length,end)) {
					throw Malformed("An item runs past the end of its sequence.");
				}
				SkipElements(_pos+(int)length,depth,false);
			}

			///<summary>Skips the elements of one item, up to end, or up to its Item Delimitation Item when the item's length is undefined.</summary>
			private void SkipElements(int end,int depth,bool isUntilDelimiter) {
				while(_pos<end) {
					if(!Fits(8,end)) {
						throw Malformed("Bytes left over at the end of an item.");
					}
					uint tagPeek=TagAt(_pos);
					if((tagPeek>>16)==0xFFFE) {
						if(isUntilDelimiter && tagPeek==_tagItemDelimitation) {
							if(U32(_pos+4)!=0) {
								throw Malformed("The Item Delimitation Item has a length.");
							}
							_pos+=8;
							return;
						}
						throw Malformed("Unexpected "+FormatTag(tagPeek)+" inside an item.");
					}
					ReadHeader(end,out uint tag,out string vr,out uint length);
					if(vr=="SQ") {
						SkipSequence(length,end,depth+1);
						continue;
					}
					if(length==_undefinedLength) {
						if(vr=="UN" || vr=="OB" || vr=="OW") {
							throw NotSubset("Element "+FormatTag(tag)+" inside a sequence has an undefined length (implicit VR or encapsulated data).");
						}
						throw Malformed("Element "+FormatTag(tag)+" inside a sequence has an undefined length but is not a sequence.");
					}
					if(!Fits(length,end)) {
						throw Malformed("The value of element "+FormatTag(tag)+" runs past the end of its item.");
					}
					_pos+=(int)length;
				}
				if(isUntilDelimiter) {
					throw Malformed("An item of undefined length has no Item Delimitation Item.");
				}
			}

			///<summary>Checks the image attributes against the subset and copies the samples (PS3.5 8.1.1, PS3.3 C.7.6.3).</summary>
			private BitmapDicom BuildBitmap() {
				if(_pixelDataOffset<0) {
					throw Unsupported("The data set has no Pixel Data (7FE0,0010).");
				}
				if(_photometricInterpretation==null) {
					throw Malformed("Photometric Interpretation (0028,0004) is missing.");
				}
				EnumDicomPhotometricInterp photometricInterp;
				if(_photometricInterpretation=="MONOCHROME1") {
					photometricInterp=EnumDicomPhotometricInterp.MONOCHROME1;
				}
				else if(_photometricInterpretation=="MONOCHROME2") {
					photometricInterp=EnumDicomPhotometricInterp.MONOCHROME2;
				}
				else {
					throw Unsupported("Photometric Interpretation "+_photometricInterpretation+" is not grayscale.");
				}
				if(_samplesPerPixel==null || _rows==null || _columns==null || _bitsAllocated==null || _bitsStored==null || _highBit==null
					|| _pixelRepresentation==null)
				{
					throw Malformed("An Image Pixel attribute is missing (Samples per Pixel, Rows, Columns, Bits Allocated, Bits Stored, High Bit or Pixel "
						+"Representation).");
				}
				if(_samplesPerPixel!=1) {
					throw Unsupported("Samples per Pixel is "+_samplesPerPixel+", not 1.");
				}
				if(_numberOfFrames!=null && _numberOfFrames!=1) {
					throw Unsupported("The image has "+_numberOfFrames+" frames.");
				}
				if(_pixelRepresentation==1) {
					throw Unsupported("The pixel values are signed (Pixel Representation 1).");
				}
				if(_pixelRepresentation!=0) {
					throw Malformed("Pixel Representation is "+_pixelRepresentation+".");
				}
				int bitsAllocated=_bitsAllocated.Value;
				int bitsStored=_bitsStored.Value;
				if(bitsAllocated!=8 && bitsAllocated!=16) {
					throw Unsupported("Bits Allocated is "+bitsAllocated+".");
				}
				if((bitsStored!=8 && bitsStored!=12 && bitsStored!=16) || bitsStored>bitsAllocated) {
					throw Unsupported("Bits Stored is "+bitsStored+" with Bits Allocated "+bitsAllocated+".");
				}
				if(_highBit!=bitsStored-1) {
					throw Unsupported("High Bit is "+_highBit+", not Bits Stored - 1.");
				}
				int rows=_rows.Value;
				int columns=_columns.Value;
				if(rows==0 || columns==0) {
					throw Malformed("Rows or Columns is 0.");
				}
				int bytesPerSample=bitsAllocated/8;
				long countSamples=(long)rows*columns;
				long countBytesNeeded=countSamples*bytesPerSample;
				if(countBytesNeeded>_pixelDataLength) {
					throw Malformed("Pixel Data has "+_pixelDataLength.ToString(CultureInfo.InvariantCulture)+" bytes; "+rows+" x "+columns+" samples need "
						+countBytesNeeded.ToString(CultureInfo.InvariantCulture)+".");
				}
				//countSamples is now at most the bytes that are really in the file, so this allocation is bounded by the file size.
				ushort[] arrayDataRaw=new ushort[countSamples];
				int offset=_pixelDataOffset;
				if(bytesPerSample==1) {
					for(int i=0;i<arrayDataRaw.Length;i++) {
						arrayDataRaw[i]=_bytes[offset+i];
					}
				}
				else {
					//Bits above High Bit are not part of the value (PS3.5 8.1.1); fo-dicom cleared them too.
					int mask=bitsStored==16 ? 0xFFFF : (1<<bitsStored)-1;
					for(int i=0;i<arrayDataRaw.Length;i++) {
						arrayDataRaw[i]=(ushort)((_bytes[offset+2*i] | (_bytes[offset+2*i+1]<<8)) & mask);
					}
				}
				BitmapDicom bitmapDicom=new BitmapDicom();
				bitmapDicom.ArrayDataRaw=arrayDataRaw;
				bitmapDicom.Width=columns;
				bitmapDicom.Height=rows;
				bitmapDicom.BitsAllocated=(ushort)bitsAllocated;
				bitmapDicom.BitsStored=(ushort)bitsStored;
				bitmapDicom.PhotometricInterp=photometricInterp;
				return bitmapDicom;
			}
		}
	}
}
