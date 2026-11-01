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
