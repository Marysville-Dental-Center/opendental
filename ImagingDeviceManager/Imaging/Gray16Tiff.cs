// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace ImagingDeviceManager.Imaging {
	///<summary>A baseline TIFF 6.0 file of 16-bit grayscale samples (one uncompressed strip, little-endian, PhotometricInterpretation
	///BlackIsZero or WhiteIsZero), written by hand so that the journal and quarantine copy of a 16-bit capture is lossless and readable by
	///common viewers without a codec. It is a recovery copy, not the stored document (that is the saver's DICOM file, design 7.8).</summary>
	internal static class Gray16Tiff {
		public const string Extension=".tif";
		private const int CountEntries=12;
		private const int OffsetIfd=8;
		private const int SizeIfd=2+CountEntries*12+4;
		private const int OffsetXResolution=OffsetIfd+SizeIfd;
		private const int OffsetYResolution=OffsetXResolution+8;
		private const int OffsetPixels=OffsetYResolution+8;
		private const ushort TypeShort=3;
		private const ushort TypeLong=4;
		private const ushort TypeRational=5;

		///<summary>samples: width*height values, rows top to bottom. isWhiteIsZero for a TWPF_VANILLA image (0 is white), so that the file
		///displays as delivered; the samples are never changed. A resolution of 0 or less is written as 0/1 (unknown).</summary>
		public static byte[] Encode(int width,int height,ushort[] samples,double xDpi,double yDpi,bool isWhiteIsZero) {
			if(width<=0 || height<=0 || samples==null || samples.Length<(long)width*height) {
				throw new ArgumentException("The samples do not match the size.");
			}
			long sizePixels=(long)width*height*2;
			byte[] bytes=new byte[OffsetPixels+sizePixels];
			bytes[0]=(byte)'I';
			bytes[1]=(byte)'I';
			WriteUInt16(bytes,2,42);
			WriteUInt32(bytes,4,OffsetIfd);
			int offset=OffsetIfd;
			WriteUInt16(bytes,offset,CountEntries);
			offset+=2;
			//Entries in ascending tag order (TIFF 6.0 section 2).
			offset=WriteEntry(bytes,offset,256,TypeLong,(uint)width);//ImageWidth
			offset=WriteEntry(bytes,offset,257,TypeLong,(uint)height);//ImageLength
			offset=WriteEntry(bytes,offset,258,TypeShort,16);//BitsPerSample
			offset=WriteEntry(bytes,offset,259,TypeShort,1);//Compression: none
			offset=WriteEntry(bytes,offset,262,TypeShort,isWhiteIsZero ? 0u : 1u);//PhotometricInterpretation
			offset=WriteEntry(bytes,offset,273,TypeLong,OffsetPixels);//StripOffsets
			offset=WriteEntry(bytes,offset,277,TypeShort,1);//SamplesPerPixel
			offset=WriteEntry(bytes,offset,278,TypeLong,(uint)height);//RowsPerStrip
			offset=WriteEntry(bytes,offset,279,TypeLong,(uint)sizePixels);//StripByteCounts
			offset=WriteEntry(bytes,offset,282,TypeRational,OffsetXResolution);//XResolution
			offset=WriteEntry(bytes,offset,283,TypeRational,OffsetYResolution);//YResolution
			offset=WriteEntry(bytes,offset,296,TypeShort,2);//ResolutionUnit: inch
			WriteUInt32(bytes,offset,0);//No next IFD.
			WriteRational(bytes,OffsetXResolution,xDpi);
			WriteRational(bytes,OffsetYResolution,yDpi);
			for(long i=0;i<(long)width*height;i++) {
				bytes[OffsetPixels+i*2]=(byte)(samples[i]&0xFF);
				bytes[OffsetPixels+i*2+1]=(byte)(samples[i]>>8);
			}
			return bytes;
		}

		///<summary>Reads a file Encode wrote back into its samples (the journal's and the quarantine's 16-bit copies, design 7.12): uncompressed
		///16-bit grayscale with one sample per pixel, in one or more strips, either byte order. Returns false with error for anything else, so a
		///file that is not such a copy is never misread. samples are top-down, as Encode took them; a resolution that is missing or 0/1 reads as 0.</summary>
		public static bool TryDecode(byte[] bytes,out int width,out int height,out ushort[] samples,out double xDpi,out double yDpi,
			out bool isWhiteIsZero,out string error)
		{
			width=0;
			height=0;
			samples=null;
			xDpi=0;
			yDpi=0;
			isWhiteIsZero=false;
			error="";
			try {
				if(bytes==null || bytes.Length<8) {
					error="The file is too short for a TIFF file.";
					return false;
				}
				bool isLittleEndian;
				if(bytes[0]=='I' && bytes[1]=='I') {
					isLittleEndian=true;
				}
				else if(bytes[0]=='M' && bytes[1]=='M') {
					isLittleEndian=false;
				}
				else {
					error="The file is not a TIFF file.";
					return false;
				}
				if(ReadUInt16(bytes,2,isLittleEndian)!=42) {
					error="The file is not a TIFF file.";
					return false;
				}
				long offsetIfd=ReadUInt32(bytes,4,isLittleEndian);
				if(offsetIfd<8 || offsetIfd+2>bytes.Length) {
					error="The TIFF file's directory is outside the file.";
					return false;
				}
				int countEntries=ReadUInt16(bytes,(int)offsetIfd,isLittleEndian);
				if(offsetIfd+2+(long)countEntries*12>bytes.Length) {
					error="The TIFF file's directory is truncated.";
					return false;
				}
				int bitsPerSample=1;
				int compression=1;
				int photometric=-1;
				int samplesPerPixel=1;
				uint[] arrayStripOffsets=null;
				uint[] arrayStripByteCounts=null;
				for(int i=0;i<countEntries;i++) {
					int offsetEntry=(int)offsetIfd+2+i*12;
					ushort tag=ReadUInt16(bytes,offsetEntry,isLittleEndian);
					ushort type=ReadUInt16(bytes,offsetEntry+2,isLittleEndian);
					uint count=ReadUInt32(bytes,offsetEntry+4,isLittleEndian);
					switch(tag) {
						case 256: width=(int)ReadValue(bytes,offsetEntry,type,isLittleEndian); break;
						case 257: height=(int)ReadValue(bytes,offsetEntry,type,isLittleEndian); break;
						case 258: bitsPerSample=(int)ReadValue(bytes,offsetEntry,type,isLittleEndian); break;
						case 259: compression=(int)ReadValue(bytes,offsetEntry,type,isLittleEndian); break;
						case 262: photometric=(int)ReadValue(bytes,offsetEntry,type,isLittleEndian); break;
						case 273: arrayStripOffsets=ReadArray(bytes,offsetEntry,type,count,isLittleEndian); break;
						case 277: samplesPerPixel=(int)ReadValue(bytes,offsetEntry,type,isLittleEndian); break;
						case 279: arrayStripByteCounts=ReadArray(bytes,offsetEntry,type,count,isLittleEndian); break;
						case 282: xDpi=ReadRational(bytes,offsetEntry,type,isLittleEndian); break;
						case 283: yDpi=ReadRational(bytes,offsetEntry,type,isLittleEndian); break;
					}
				}
				if(width<=0 || height<=0) {
					error="The TIFF file has no size.";
					return false;
				}
				if(bitsPerSample!=16 || samplesPerPixel!=1 || compression!=1 || (photometric!=0 && photometric!=1)) {
					error="The TIFF file is not an uncompressed 16-bit grayscale image (BitsPerSample "+bitsPerSample+", SamplesPerPixel "
						+samplesPerPixel+", Compression "+compression+", PhotometricInterpretation "+photometric+").";
					return false;
				}
				if(arrayStripOffsets==null || arrayStripByteCounts==null || arrayStripOffsets.Length!=arrayStripByteCounts.Length) {
					error="The TIFF file's strips are not described.";
					return false;
				}
				isWhiteIsZero=photometric==0;
				long countSamples=(long)width*height;
				samples=new ushort[countSamples];
				long idxSample=0;
				for(int s=0;s<arrayStripOffsets.Length && idxSample<countSamples;s++) {
					long offsetStrip=arrayStripOffsets[s];
					long lengthStrip=arrayStripByteCounts[s];
					if(offsetStrip+lengthStrip>bytes.Length) {
						error="A strip of the TIFF file is outside the file.";
						samples=null;
						return false;
					}
					for(long b=0;b+1<lengthStrip && idxSample<countSamples;b+=2) {
						samples[idxSample++]=ReadUInt16(bytes,(int)(offsetStrip+b),isLittleEndian);
					}
				}
				if(idxSample<countSamples) {
					error="The TIFF file has fewer samples than its size.";
					samples=null;
					return false;
				}
				return true;
			}
			catch(Exception ex) {
				error="The TIFF file could not be read: "+ex.Message;
				samples=null;
				return false;
			}
		}

		///<summary>A SHORT or LONG value of one entry, left-justified in its value field.</summary>
		private static uint ReadValue(byte[] bytes,int offsetEntry,ushort type,bool isLittleEndian) {
			if(type==TypeShort) {
				return ReadUInt16(bytes,offsetEntry+8,isLittleEndian);
			}
			return ReadUInt32(bytes,offsetEntry+8,isLittleEndian);
		}

		///<summary>The SHORT or LONG values of an entry: in its value field when they fit in 4 bytes, otherwise at the offset there.</summary>
		private static uint[] ReadArray(byte[] bytes,int offsetEntry,ushort type,uint count,bool isLittleEndian) {
			if(count==0 || count>1000000) {
				return null;
			}
			int sizeValue=type==TypeShort ? 2 : 4;
			long offsetValues=(long)count*sizeValue<=4 ? (long)offsetEntry+8 : (long)ReadUInt32(bytes,offsetEntry+8,isLittleEndian);
			if(offsetValues+(long)count*sizeValue>bytes.Length) {
				return null;
			}
			uint[] arrayValues=new uint[count];
			for(int i=0;i<count;i++) {
				arrayValues[i]=type==TypeShort ? (uint)ReadUInt16(bytes,(int)offsetValues+i*2,isLittleEndian) : ReadUInt32(bytes,(int)offsetValues+i*4,isLittleEndian);
			}
			return arrayValues;
		}

		private static double ReadRational(byte[] bytes,int offsetEntry,ushort type,bool isLittleEndian) {
			if(type!=TypeRational) {
				return 0;
			}
			long offset=ReadUInt32(bytes,offsetEntry+8,isLittleEndian);
			if(offset+8>bytes.Length) {
				return 0;
			}
			uint numerator=ReadUInt32(bytes,(int)offset,isLittleEndian);
			uint denominator=ReadUInt32(bytes,(int)offset+4,isLittleEndian);
			if(denominator==0 || numerator==0) {
				return 0;
			}
			return (double)numerator/denominator;
		}

		private static ushort ReadUInt16(byte[] bytes,int offset,bool isLittleEndian) {
			return isLittleEndian ? (ushort)(bytes[offset]|bytes[offset+1]<<8) : (ushort)(bytes[offset]<<8|bytes[offset+1]);
		}

		private static uint ReadUInt32(byte[] bytes,int offset,bool isLittleEndian) {
			if(isLittleEndian) {
				return (uint)(bytes[offset]|bytes[offset+1]<<8|bytes[offset+2]<<16|bytes[offset+3]<<24);
			}
			return (uint)(bytes[offset]<<24|bytes[offset+1]<<16|bytes[offset+2]<<8|bytes[offset+3]);
		}

		private static int WriteEntry(byte[] bytes,int offset,ushort tag,ushort type,uint value) {
			WriteUInt16(bytes,offset,tag);
			WriteUInt16(bytes,offset+2,type);
			WriteUInt32(bytes,offset+4,1);
			if(type==TypeShort) {
				WriteUInt16(bytes,offset+8,(ushort)value);//Left-justified in the value field.
			}
			else {
				WriteUInt32(bytes,offset+8,value);
			}
			return offset+12;
		}

		private static void WriteRational(byte[] bytes,int offset,double dpi) {
			if(dpi>0 && dpi<1000000) {
				WriteUInt32(bytes,offset,(uint)Math.Round(dpi*100));
				WriteUInt32(bytes,offset+4,100);
			}
			else {
				WriteUInt32(bytes,offset,0);
				WriteUInt32(bytes,offset+4,1);
			}
		}

		private static void WriteUInt16(byte[] bytes,int offset,ushort value) {
			bytes[offset]=(byte)value;
			bytes[offset+1]=(byte)(value>>8);
		}

		private static void WriteUInt32(byte[] bytes,int offset,uint value) {
			bytes[offset]=(byte)value;
			bytes[offset+1]=(byte)(value>>8);
			bytes[offset+2]=(byte)(value>>16);
			bytes[offset+3]=(byte)(value>>24);
		}
	}
}
