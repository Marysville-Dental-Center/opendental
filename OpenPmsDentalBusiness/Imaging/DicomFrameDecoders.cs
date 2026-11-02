// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace OpenPmsDentalBusiness {
	///<summary>The Image Pixel attributes of one frame (NEMA PS3.3 C.7.6.3). DicomImageReader gives a decoder the attributes of the file; the
	///decoder gives back the attributes of the pixels it returns, which may differ (a JPEG decoder returns YBR_FULL_422 data as RGB).</summary>
	public sealed class DicomFrameInfo {
		public int Rows;
		public int Columns;
		public int SamplesPerPixel;
		public string PhotometricInterpretation;
		///<summary>0: the samples of each pixel follow each other; 1: each sample has its own plane (PS3.3 C.7.6.3.1.3).</summary>
		public int PlanarConfiguration;
		public int BitsAllocated;
		public int BitsStored;
		public int HighBit;
		///<summary>0 unsigned, 1 two's complement.</summary>
		public int PixelRepresentation;
		///<summary>Number of Frames of the file (1 when absent).</summary>
		public int NumberOfFrames;
		///<summary>Which frame this is, from 0. The program shows frame 0.</summary>
		public int FrameIndex;

		public DicomFrameInfo Clone() {
			return (DicomFrameInfo)MemberwiseClone();
		}
	}

	///<summary>What a decoder made of one frame: the pixels, or why there are none.</summary>
	public sealed class DicomFrameDecodeResult {
		public bool IsDecoded { get; }
		///<summary>True when the frame is valid but uses something the decoder does not do (for example 12-bit JPEG); false when it is broken.
		///Only meaningful when IsDecoded is false.</summary>
		public bool IsUnsupported { get; }
		///<summary>Why the frame was not decoded, as a sentence. "" when IsDecoded.</summary>
		public string Reason { get; }
		///<summary>The uncompressed pixels, exactly as native pixel data of one frame in Explicit VR Little Endian would hold them (PS3.5 8.1 and
		///8.2) for the attributes in FrameInfo: little-endian samples of BitsAllocated bits, interleaved or planar as PlanarConfiguration says.</summary>
		public byte[] Pixels { get; }
		///<summary>The attributes of Pixels. Rows and Columns are those of the file.</summary>
		public DicomFrameInfo FrameInfo { get; }

		private DicomFrameDecodeResult(bool isDecoded,bool isUnsupported,string reason,byte[] pixels,DicomFrameInfo frameInfo) {
			IsDecoded=isDecoded;
			IsUnsupported=isUnsupported;
			Reason=reason ?? "";
			Pixels=pixels;
			FrameInfo=frameInfo;
		}

		public static DicomFrameDecodeResult Decoded(byte[] pixels,DicomFrameInfo frameInfo) {
			return new DicomFrameDecodeResult(true,false,"",pixels,frameInfo);
		}

		///<summary>The frame is broken.</summary>
		public static DicomFrameDecodeResult Failed(string reason) {
			return new DicomFrameDecodeResult(false,false,reason,null,null);
		}

		///<summary>The frame uses a feature this decoder does not have.</summary>
		public static DicomFrameDecodeResult NotSupported(string reason) {
			return new DicomFrameDecodeResult(false,true,reason,null,null);
		}
	}

	///<summary>The one seam for compressed pixel data: given the transfer syntax, the compressed bytes of one frame (its fragments put together,
	///PS3.5 A.4) and the image attributes of the file, a decoder returns the uncompressed pixels. DicomImageReader parses the data set, the
	///items, the fragments and the Basic Offset Table itself and asks DicomFrameDecoderRegistry for the decoder of the file's transfer syntax.
	///A decoder never needs to read the file. It should not throw; the reader catches and declines the file if one does.</summary>
	public interface IDicomFrameDecoder {
		///<summary>A short name for messages and logs, such as "RLE Lossless (in-house)".</summary>
		string Name { get; }
		///<summary>The transfer syntax UIDs this decoder decodes.</summary>
		IReadOnlyList<string> TransferSyntaxUids { get; }
		DicomFrameDecodeResult Decode(string transferSyntaxUid,byte[] frameBytes,DicomFrameInfo frameInfo);
	}

	///<summary>Which decoder decodes which transfer syntax. Default holds the in-house decoders of this version: RLE Lossless, JPEG Baseline
	///(through the .NET Framework's own JPEG decoder) and Encapsulated Uncompressed. A later package plugs in another decoder with Register; a
	///decoder registered later for a transfer syntax replaces the earlier one. Thread-safe.</summary>
	public sealed class DicomFrameDecoderRegistry {
		private readonly object _lock=new object();
		private readonly Dictionary<string,IDicomFrameDecoder> _dictDecoders=new Dictionary<string,IDicomFrameDecoder>();

		///<summary>The registry DicomHelper reads with.</summary>
		public static DicomFrameDecoderRegistry Default { get; }=CreateWithInHouseDecoders();

		///<summary>A new registry with the in-house decoders only.</summary>
		public static DicomFrameDecoderRegistry CreateWithInHouseDecoders() {
			DicomFrameDecoderRegistry registry=new DicomFrameDecoderRegistry();
			registry.Register(new DicomRleDecoder());
			registry.Register(new DicomJpegBaselineDecoder());
			registry.Register(new DicomEncapsulatedUncompressedDecoder());
			return registry;
		}

		public void Register(IDicomFrameDecoder decoder) {
			if(decoder==null || decoder.TransferSyntaxUids==null) {
				return;
			}
			lock(_lock) {
				foreach(string uid in decoder.TransferSyntaxUids) {
					if(!string.IsNullOrEmpty(uid)) {
						_dictDecoders[uid]=decoder;
					}
				}
			}
		}

		///<summary>The decoder of the transfer syntax, or null when none is registered.</summary>
		public IDicomFrameDecoder Find(string transferSyntaxUid) {
			if(transferSyntaxUid==null) {
				return null;
			}
			lock(_lock) {
				_dictDecoders.TryGetValue(transferSyntaxUid,out IDicomFrameDecoder decoder);
				return decoder;
			}
		}

		///<summary>The transfer syntax UIDs that have a decoder, in ascending order.</summary>
		public List<string> ListTransferSyntaxUids() {
			lock(_lock) {
				return _dictDecoders.Keys.OrderBy(x => x,StringComparer.Ordinal).ToList();
			}
		}
	}

	///<summary>RLE Lossless (PS3.5 Annex G), in-house. A frame is a 64-byte header of 16 UL values (the number of segments, then the offset of
	///each segment from the start of the header) and the segments: one per byte of each sample, the most significant byte first, each holding
	///that byte of every pixel in order (G.2). A segment is a series of runs (G.3.2): a header byte n from 0 to 127 copies the next n+1 bytes,
	///-1 to -127 repeats the next byte -n+1 times, -128 does nothing. The pixels are returned interleaved (Planar Configuration 0).</summary>
	public sealed class DicomRleDecoder : IDicomFrameDecoder {
		public string Name {
			get { return "RLE Lossless (in-house)"; }
		}

		public IReadOnlyList<string> TransferSyntaxUids {
			get { return new[] { DicomTransferSyntaxes.RleLossless }; }
		}

		public DicomFrameDecodeResult Decode(string transferSyntaxUid,byte[] frameBytes,DicomFrameInfo frameInfo) {
			if(frameBytes==null || frameInfo==null) {
				return DicomFrameDecodeResult.Failed("No RLE frame.");
			}
			if(frameInfo.BitsAllocated!=8 && frameInfo.BitsAllocated!=16) {
				return DicomFrameDecodeResult.NotSupported("RLE with "+frameInfo.BitsAllocated+" bits allocated is not supported.");
			}
			if(frameInfo.PhotometricInterpretation!=null && frameInfo.PhotometricInterpretation.StartsWith("YBR_") && frameInfo.PhotometricInterpretation!="YBR_FULL") {
				return DicomFrameDecodeResult.NotSupported("RLE with Photometric Interpretation "+frameInfo.PhotometricInterpretation+" is not supported.");
			}
			int bytesPerSample=frameInfo.BitsAllocated/8;
			int countSegments=frameInfo.SamplesPerPixel*bytesPerSample;
			long countPixelsLong=(long)frameInfo.Rows*frameInfo.Columns;
			if(countSegments<1 || countSegments>15 || countPixelsLong<1 || countPixelsLong>DicomImageReader.MaxFramePixels) {
				return DicomFrameDecodeResult.Failed("The image attributes do not fit RLE Lossless.");
			}
			int countPixels=(int)countPixelsLong;
			if(frameBytes.Length<64) {
				return DicomFrameDecodeResult.Failed("The RLE frame is shorter than its 64-byte header.");
			}
			uint countInHeader=BitConverter.ToUInt32(frameBytes,0);
			if(countInHeader!=countSegments) {
				return DicomFrameDecodeResult.Failed("The RLE header lists "+countInHeader.ToString(CultureInfo.InvariantCulture)+" segments; the image needs "
					+countSegments+".");
			}
			long[] arrayOffsets=new long[countSegments+1];
			for(int i=0;i<countSegments;i++) {
				arrayOffsets[i]=BitConverter.ToUInt32(frameBytes,4+4*i);
				if(arrayOffsets[i]<64 || arrayOffsets[i]>=frameBytes.Length || (i>0 && arrayOffsets[i]<=arrayOffsets[i-1])) {
					return DicomFrameDecodeResult.Failed("RLE segment "+(i+1)+" has an offset outside the frame or out of order.");
				}
			}
			arrayOffsets[countSegments]=frameBytes.Length;
			byte[] arrayPixels=new byte[(long)countPixels*countSegments];
			byte[] arraySegment=new byte[countPixels];
			for(int segment=0;segment<countSegments;segment++) {
				if(!DecodeSegment(frameBytes,(int)arrayOffsets[segment],(int)arrayOffsets[segment+1],arraySegment)) {
					return DicomFrameDecodeResult.Failed("RLE segment "+(segment+1)+" ends before it holds every pixel.");
				}
				int sample=segment/bytesPerSample;
				int byteInSample=bytesPerSample-1-segment%bytesPerSample;//the most significant byte comes first (G.2)
				int stride=countSegments;
				int start=sample*bytesPerSample+byteInSample;
				for(int p=0;p<countPixels;p++) {
					arrayPixels[(long)p*stride+start]=arraySegment[p];
				}
			}
			DicomFrameInfo frameInfoOut=frameInfo.Clone();
			frameInfoOut.PlanarConfiguration=0;
			return DicomFrameDecodeResult.Decoded(arrayPixels,frameInfoOut);
		}

		///<summary>Fills arrayOut from the runs between start and end (G.3.2). False when the runs end first; bytes past arrayOut's end are
		///ignored (a pad byte, PS3.5 G.3).</summary>
		private static bool DecodeSegment(byte[] bytes,int start,int end,byte[] arrayOut) {
			int pos=start;
			int countOut=0;
			while(countOut<arrayOut.Length) {
				if(pos>=end) {
					return false;
				}
				int n=unchecked((sbyte)bytes[pos]);
				pos++;
				if(n>=0) {
					int count=n+1;
					if(pos+count>end) {
						return false;
					}
					int countCopy=Math.Min(count,arrayOut.Length-countOut);
					Buffer.BlockCopy(bytes,pos,arrayOut,countOut,countCopy);
					countOut+=countCopy;
					pos+=count;
				}
				else if(n>=-127) {
					if(pos>=end) {
						return false;
					}
					byte value=bytes[pos];
					pos++;
					int count=Math.Min(-n+1,arrayOut.Length-countOut);
					for(int i=0;i<count;i++) {
						arrayOut[countOut+i]=value;
					}
					countOut+=count;
				}
				//n==-128: no operation
			}
			return true;
		}
	}

	///<summary>JPEG Baseline (Process 1, PS3.5 A.4.1 and 8.2.1) through the .NET Framework's own JPEG decoder (GDI+), which applies the
	///JPEG stream's own color transform, so YBR_FULL_422, YBR_PARTIAL_422 and YBR_FULL data come back as RGB. The stream's markers are checked
	///first (ITU-T T.81 B.2): a frame header of baseline, extended or progressive Huffman coding (fo-dicom 4 decoded progressive streams under
	///this transfer syntax too), 8-bit precision, one or three components as Samples per Pixel says, the size of Rows and Columns, an end of
	///image marker, and at most MaxScans scans.</summary>
	public sealed class DicomJpegBaselineDecoder : IDicomFrameDecoder {
		///<summary>The most scans a stream may have. A sequential stream has one per component at most, and a progressive one as encoders write it
		///about ten (T.81 Annex G). The decoder passes over every block of the image in each scan, and an end-of-band run lets a few bytes cover
		///a whole scan, so a small hostile stream of thousands of scans would take minutes; it is declined before the decoder runs.</summary>
		public const int MaxScans=32;

		public string Name {
			get { return "JPEG Baseline (.NET Framework decoder)"; }
		}

		public IReadOnlyList<string> TransferSyntaxUids {
			get { return new[] { DicomTransferSyntaxes.JpegBaseline }; }
		}

		public DicomFrameDecodeResult Decode(string transferSyntaxUid,byte[] frameBytes,DicomFrameInfo frameInfo) {
			if(frameBytes==null || frameInfo==null) {
				return DicomFrameDecodeResult.Failed("No JPEG frame.");
			}
			if(frameInfo.PhotometricInterpretation=="PALETTE COLOR") {
				return DicomFrameDecodeResult.NotSupported("Palette color in a lossy JPEG is not supported.");
			}
			string error=CheckStream(frameBytes,frameInfo,out int countComponents,out bool isUnsupported);
			if(error!=null) {
				return isUnsupported ? DicomFrameDecodeResult.NotSupported(error) : DicomFrameDecodeResult.Failed(error);
			}
			byte[] arrayPixels;
			try {
				arrayPixels=DecodeWithGdiPlus(frameBytes,frameInfo.Rows,frameInfo.Columns,countComponents);
			}
			catch(Exception ex) {
				return DicomFrameDecodeResult.Failed("The .NET Framework's JPEG decoder could not decode the frame ("+ex.Message+").");
			}
			if(arrayPixels==null) {
				return DicomFrameDecodeResult.Failed("The decoded JPEG frame is not "+frameInfo.Columns+" x "+frameInfo.Rows+".");
			}
			DicomFrameInfo frameInfoOut=frameInfo.Clone();
			frameInfoOut.BitsAllocated=8;
			frameInfoOut.BitsStored=8;
			frameInfoOut.HighBit=7;
			frameInfoOut.PixelRepresentation=0;
			frameInfoOut.SamplesPerPixel=countComponents;
			frameInfoOut.PlanarConfiguration=0;
			if(countComponents==3) {
				frameInfoOut.PhotometricInterpretation="RGB";
			}
			return DicomFrameDecodeResult.Decoded(arrayPixels,frameInfoOut);
		}

		///<summary>Null when the JPEG stream's markers (ITU-T T.81 B.1 and B.2) fit the image; otherwise why not, with isUnsupported true for a
		///valid stream this decoder does not take. Walks every marker segment by its length, and every scan's entropy-coded data (and, after
		///the frame header, any bytes outside a marker segment) up to the next marker (B.1.1.5: in entropy-coded data an FF byte is followed by
		///a stuffed 00 or a restart marker D0 to D7, so any other FF xx is a marker), so each SOS marker counted is a scan the decoder sees.</summary>
		private static string CheckStream(byte[] bytes,DicomFrameInfo frameInfo,out int countComponents,out bool isUnsupported) {
			countComponents=0;
			isUnsupported=false;
			if(bytes.Length<4 || bytes[0]!=0xFF || bytes[1]!=0xD8) {
				return "The frame is not a JPEG stream (no start of image marker).";
			}
			int end=bytes.Length;
			while(end>2 && bytes[end-1]==0x00) {//fragments are padded to an even length (PS3.5 A.4)
				end--;
			}
			if(end<4 || bytes[end-2]!=0xFF || bytes[end-1]!=0xD9) {
				return "The JPEG stream is cut off (no end of image marker).";
			}
			bool hasFrameHeader=false;
			int countScans=0;
			int pos=2;
			while(true) {
				if(pos+2>end) {
					return "The JPEG stream is cut off (no end of image marker).";
				}
				if(bytes[pos]!=0xFF) {
					if(!hasFrameHeader) {
						return "The JPEG stream has no frame header before its data.";
					}
					//Bytes that belong to no marker segment, such as a pad byte where a writer split the stream into odd fragments (PS3.5 A.4): the
					//decoder passes over them to the next marker, and so does the count.
					pos=FindMarkerAfterEntropyData(bytes,pos,end);
					continue;
				}
				byte marker=bytes[pos+1];
				if(marker==0xFF) {//fill byte
					pos++;
					continue;
				}
				if(marker==0xD9) {//end of image
					break;
				}
				if(marker==0x00 && hasFrameHeader) {//FF 00 is not a marker: more bytes outside a marker segment
					pos=FindMarkerAfterEntropyData(bytes,pos,end);
					continue;
				}
				if(marker==0x01 || (marker>=0xD0 && marker<=0xD7)) {//markers without a length
					pos+=2;
					continue;
				}
				if(pos+4>end) {
					return "A JPEG marker segment runs past the end of the frame.";
				}
				int length=(bytes[pos+2]<<8) | bytes[pos+3];
				if(length<2 || pos+2+length>end) {
					return "A JPEG marker segment runs past the end of the frame.";
				}
				bool isFrameHeader=marker>=0xC0 && marker<=0xCF && marker!=0xC4 && marker!=0xC8 && marker!=0xCC;
				if(isFrameHeader) {
					if(hasFrameHeader) {
						return "The JPEG stream has more than one frame header.";
					}
					string error=CheckFrameHeader(bytes,pos,length,marker,frameInfo,out countComponents);
					if(error!=null) {
						return error;
					}
					hasFrameHeader=true;
				}
				if(marker==0xDA) {
					if(!hasFrameHeader) {
						return "The JPEG stream has no frame header before its data.";
					}
					countScans++;
					if(countScans>MaxScans) {
						isUnsupported=true;
						return "The JPEG stream has more than "+MaxScans+" scans; this version does not decode such streams.";
					}
					pos=FindMarkerAfterEntropyData(bytes,pos+2+length,end);
					continue;
				}
				pos+=2+length;
			}
			if(!hasFrameHeader) {
				return "The JPEG stream has no frame header.";
			}
			if(countScans==0) {
				return "The JPEG stream has no scan.";
			}
			return null;
		}

		///<summary>Null when the frame header (SOFn, T.81 B.2.2) at pos fits the image; otherwise why not.</summary>
		private static string CheckFrameHeader(byte[] bytes,int pos,int length,byte marker,DicomFrameInfo frameInfo,out int countComponents) {
			countComponents=0;
			if(marker!=0xC0 && marker!=0xC1 && marker!=0xC2) {
				return "The JPEG stream uses a process other than baseline, extended or progressive Huffman coding (SOF marker 0x"
					+marker.ToString("X2",CultureInfo.InvariantCulture)+").";
			}
			if(length<8) {
				return "The JPEG frame header is too short.";
			}
			int precision=bytes[pos+4];
			int height=(bytes[pos+5]<<8) | bytes[pos+6];
			int width=(bytes[pos+7]<<8) | bytes[pos+8];
			countComponents=bytes[pos+9];
			if(precision!=8) {
				return "The JPEG stream has "+precision+"-bit samples; JPEG Baseline has 8.";
			}
			if(width!=frameInfo.Columns || height!=frameInfo.Rows) {
				return "The JPEG stream is "+width+" x "+height+", the image attributes say "+frameInfo.Columns+" x "+frameInfo.Rows+".";
			}
			if(countComponents!=frameInfo.SamplesPerPixel || (countComponents!=1 && countComponents!=3)) {
				return "The JPEG stream has "+countComponents+" components; Samples per Pixel is "+frameInfo.SamplesPerPixel+".";
			}
			return null;
		}

		///<summary>The position of the next marker from start, past entropy-coded data or other bytes outside a marker segment: the first FF
		///followed by neither 00 (a stuffed byte) nor D0 to D7 (a restart marker) nor another FF (fill). The stream's end of image marker is
		///found at the latest.</summary>
		private static int FindMarkerAfterEntropyData(byte[] bytes,int start,int end) {
			int pos=start;
			while(pos+1<end) {
				if(bytes[pos]!=0xFF) {
					pos++;
					continue;
				}
				byte next=bytes[pos+1];
				if(next==0x00 || (next>=0xD0 && next<=0xD7)) {
					pos+=2;
					continue;
				}
				if(next==0xFF) {
					pos++;
					continue;
				}
				return pos;
			}
			return end-2;
		}

		///<summary>The pixels of a JPEG stream through GDI+ (System.Drawing), with no color management: gray samples for one component, RGB
		///triples for three. Null when the decoded size is not the expected one.</summary>
		private static byte[] DecodeWithGdiPlus(byte[] bytes,int rows,int columns,int countComponents) {
			using(MemoryStream memoryStream=new MemoryStream(bytes,false))
			using(Bitmap bitmap=new Bitmap(memoryStream)) {
				if(bitmap.Width!=columns || bitmap.Height!=rows) {
					return null;
				}
				byte[] arrayPixels=new byte[(long)rows*columns*countComponents];
				BitmapData bitmapData=bitmap.LockBits(new Rectangle(0,0,columns,rows),ImageLockMode.ReadOnly,PixelFormat.Format24bppRgb);
				try {
					byte[] arrayRow=new byte[columns*3];
					for(int y=0;y<rows;y++) {
						Marshal.Copy(IntPtr.Add(bitmapData.Scan0,y*bitmapData.Stride),arrayRow,0,arrayRow.Length);
						for(int x=0;x<columns;x++) {
							//GDI+ holds 24-bit pixels as blue, green, red.
							if(countComponents==1) {
								arrayPixels[(long)y*columns+x]=arrayRow[3*x+1];
							}
							else {
								long idx=((long)y*columns+x)*3;
								arrayPixels[idx]=arrayRow[3*x+2];
								arrayPixels[idx+1]=arrayRow[3*x+1];
								arrayPixels[idx+2]=arrayRow[3*x];
							}
						}
					}
				}
				finally {
					bitmap.UnlockBits(bitmapData);
				}
				return arrayPixels;
			}
		}
	}

	///<summary>Encapsulated Uncompressed Explicit VR Little Endian (PS3.5 A.4.11): each fragment holds one frame of native pixel data, so the
	///frame is returned as it is.</summary>
	public sealed class DicomEncapsulatedUncompressedDecoder : IDicomFrameDecoder {
		public string Name {
			get { return "Encapsulated Uncompressed (in-house)"; }
		}

		public IReadOnlyList<string> TransferSyntaxUids {
			get { return new[] { DicomTransferSyntaxes.EncapsulatedUncompressed }; }
		}

		public DicomFrameDecodeResult Decode(string transferSyntaxUid,byte[] frameBytes,DicomFrameInfo frameInfo) {
			if(frameBytes==null || frameInfo==null) {
				return DicomFrameDecodeResult.Failed("No frame.");
			}
			return DicomFrameDecodeResult.Decoded(frameBytes,frameInfo.Clone());
		}
	}
}
