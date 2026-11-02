// The other encodings of NEMA PS3.5 for the test files: Implicit VR Little Endian (7.1.3, A.1), Explicit VR Big Endian (7.3, A.3, retired),
// Deflated Explicit VR Little Endian (A.5), PS3.10 files without the preamble or without File Meta Information, encapsulated pixel data
// (A.4: Basic Offset Table, fragments, Sequence Delimitation Item), an RLE Lossless encoder (Annex G) and JPEG Baseline streams made by the
// .NET Framework's own JPEG encoder (WPF's JpegBitmapEncoder). Everything is synthetic. Licensed under the GNU General Public License, version 2
// or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DicomReaderTests {
	///<summary>How a data set is encoded (PS3.5 7.1 and 7.3).</summary>
	internal enum SynEncoding {
		ExplicitLittle,
		ImplicitLittle,
		ExplicitBig,
	}

	///<summary>What comes before the data set in a test file.</summary>
	internal enum SynLayout {
		///<summary>PS3.10: 128-byte preamble, "DICM", File Meta Information.</summary>
		Standard,
		///<summary>File Meta Information at the very start: no preamble and no "DICM".</summary>
		MetaWithoutPreamble,
		///<summary>"DICM" at the very start, then File Meta Information: no preamble.</summary>
		PrefixWithoutPreamble,
		///<summary>Only the data set, as a DICOM stream with no PS3.10 header at all.</summary>
		DatasetOnly,
		///<summary>PS3.10 preamble and prefix, File Meta Information without a Transfer Syntax UID.</summary>
		MetaWithoutTransferSyntax,
	}

	///<summary>One image for the test files: the Image Pixel module (PS3.3 C.7.6.3) and the pixel bytes of every frame, native and little-endian.
	///Null attributes are left out.</summary>
	internal sealed class SynImage {
		public int Rows;
		public int Columns;
		public ushort? SamplesPerPixel=1;
		public string Photometric="MONOCHROME2";
		public ushort? PlanarConfiguration;
		public ushort? BitsAllocated=16;
		public ushort? BitsStored=16;
		public ushort? HighBit=15;
		public ushort? PixelRepresentation=0;
		///<summary>Number of Frames (0028,0008) as written; null leaves it out.</summary>
		public string NumberOfFrames;
		///<summary>Elements added in tag order (rescale, window, palette tables, private elements).</summary>
		public List<SynElement> ListExtra=new List<SynElement>();
		///<summary>The native pixel data of all frames, little-endian, as it would be in Explicit VR Little Endian.</summary>
		public byte[] PixelBytes;
		///<summary>OW or OB for native pixel data.</summary>
		public string PixelVr="OW";
		///<summary>Replaces the native Pixel Data, for encapsulated pixel data.</summary>
		public SynElement PixelElement;

		public SynImage Clone() {
			SynImage synImage=(SynImage)MemberwiseClone();
			synImage.ListExtra=ListExtra.ToList();
			return synImage;
		}

		public List<SynElement> ToDataset() {
			List<SynElement> listElements=new List<SynElement>();
			listElements.Add(SyntheticDicom.Text(0x0008,0x0016,"UI",SyntheticDicom.SopClassIntraOral));
			listElements.Add(SyntheticDicom.Text(0x0008,0x0018,"UI","2.25.1234567890123456789"));
			listElements.Add(SyntheticDicom.Text(0x0008,0x0060,"CS","IO"));
			if(SamplesPerPixel!=null) {
				listElements.Add(SyntheticDicom.US(0x0028,0x0002,SamplesPerPixel.Value));
			}
			if(Photometric!=null) {
				listElements.Add(SyntheticDicom.Text(0x0028,0x0004,"CS",Photometric));
			}
			if(PlanarConfiguration!=null) {
				listElements.Add(SyntheticDicom.US(0x0028,0x0006,PlanarConfiguration.Value));
			}
			if(NumberOfFrames!=null) {
				listElements.Add(SyntheticDicom.Text(0x0028,0x0008,"IS",NumberOfFrames));
			}
			listElements.Add(SyntheticDicom.US(0x0028,0x0010,(ushort)Rows));
			listElements.Add(SyntheticDicom.US(0x0028,0x0011,(ushort)Columns));
			if(BitsAllocated!=null) {
				listElements.Add(SyntheticDicom.US(0x0028,0x0100,BitsAllocated.Value));
			}
			if(BitsStored!=null) {
				listElements.Add(SyntheticDicom.US(0x0028,0x0101,BitsStored.Value));
			}
			if(HighBit!=null) {
				listElements.Add(SyntheticDicom.US(0x0028,0x0102,HighBit.Value));
			}
			if(PixelRepresentation!=null) {
				listElements.Add(SyntheticDicom.US(0x0028,0x0103,PixelRepresentation.Value));
			}
			foreach(SynElement element in ListExtra) {
				listElements=SyntheticDicom.With(listElements,element);
			}
			if(PixelElement!=null) {
				listElements.Add(PixelElement);
			}
			else if(PixelBytes!=null) {
				byte[] bytes=PixelBytes;
				if(bytes.Length%2==1) {
					bytes=bytes.Concat(new byte[1]).ToArray();
				}
				listElements.Add(SyntheticDicom.Raw(0x7FE0,0x0010,PixelVr,bytes));
			}
			return listElements;
		}
	}

	internal static partial class SyntheticDicom {
		public const string EncapsulatedUncompressed="1.2.840.10008.1.2.1.98";
		public const string JpegBaseline="1.2.840.10008.1.2.4.50";
		public const string JpegExtended="1.2.840.10008.1.2.4.51";
		public const string JpegLosslessProcess14="1.2.840.10008.1.2.4.57";
		public const string JpegLsLossless="1.2.840.10008.1.2.4.80";
		public const string JpegLsNearLossless="1.2.840.10008.1.2.4.81";
		public const string Jpeg2000Lossless="1.2.840.10008.1.2.4.90";
		public const string Jpeg2000="1.2.840.10008.1.2.4.91";
		public const string HtJpeg2000Lossless="1.2.840.10008.1.2.4.201";
		public const string HtJpeg2000="1.2.840.10008.1.2.4.203";
		public const string JpegXl="1.2.840.10008.1.2.4.112";
		public const string Mpeg2="1.2.840.10008.1.2.4.100";
		public const string RleLossless="1.2.840.10008.1.2.5";
		public const string DeflatedImageFrame="1.2.840.10008.1.2.8.1";

		///<summary>The data set encoding of a transfer syntax (the deflated one deflates an Explicit VR Little Endian data set).</summary>
		public static SynEncoding EncodingOf(string transferSyntax) {
			if(transferSyntax==ImplicitVrLittleEndian) {
				return SynEncoding.ImplicitLittle;
			}
			if(transferSyntax==ExplicitVrBigEndian) {
				return SynEncoding.ExplicitBig;
			}
			return SynEncoding.ExplicitLittle;
		}

		///<summary>The size of the units a big-endian encoding swaps for a VR (PS3.5 7.3); 1 when nothing is swapped.</summary>
		private static int SwapUnit(string vr) {
			switch(vr) {
				case "US": case "SS": case "OW": case "AT":
					return 2;
				case "UL": case "SL": case "FL": case "OL": case "OF":
					return 4;
				case "FD": case "OD": case "OV": case "SV": case "UV":
					return 8;
				default:
					return 1;
			}
		}

		private static byte[] SwapUnits(byte[] bytes,int unit) {
			if(unit==1 || bytes==null) {
				return bytes;
			}
			byte[] bytesSwapped=(byte[])bytes.Clone();
			for(int i=0;i+unit<=bytes.Length;i+=unit) {
				for(int j=0;j<unit;j++) {
					bytesSwapped[i+j]=bytes[i+unit-1-j];
				}
			}
			return bytesSwapped;
		}

		private static void WriteU16(Stream stream,ushort value,bool isBig) {
			if(isBig) {
				stream.WriteByte((byte)(value>>8));
				stream.WriteByte((byte)value);
			}
			else {
				stream.WriteByte((byte)value);
				stream.WriteByte((byte)(value>>8));
			}
		}

		private static void WriteU32(Stream stream,uint value,bool isBig) {
			if(isBig) {
				WriteU16(stream,(ushort)(value>>16),true);
				WriteU16(stream,(ushort)value,true);
			}
			else {
				WriteU16(stream,(ushort)value,false);
				WriteU16(stream,(ushort)(value>>16),false);
			}
		}

		///<summary>The elements in the given encoding. Values are given little-endian and swapped by VR for big endian. A value given with
		///LengthOverride 0xFFFFFFFF (encapsulated pixel data) is written as it is.</summary>
		public static byte[] EncodeAs(List<SynElement> listElements,SynEncoding encoding) {
			bool isBig=encoding==SynEncoding.ExplicitBig;
			using(MemoryStream memoryStream=new MemoryStream()) {
				foreach(SynElement element in listElements) {
					WriteU16(memoryStream,(ushort)(element.Tag>>16),isBig);
					WriteU16(memoryStream,(ushort)(element.Tag & 0xFFFF),isBig);
					byte[] bytesValue;
					uint length;
					if(element.Vr=="SQ") {
						bytesValue=EncodeItemsAs(element.ListItems,encoding);
						if(element.IsUndefinedLength) {
							bytesValue=bytesValue.Concat(DelimiterAs(0xE0DD,isBig)).ToArray();
						}
						length=element.IsUndefinedLength ? 0xFFFFFFFF : (uint)bytesValue.Length;
					}
					else {
						bytesValue=element.LengthOverride==0xFFFFFFFF ? element.Value : SwapUnits(element.Value,isBig ? SwapUnit(element.Vr) : 1);
						length=(uint)bytesValue.Length;
					}
					if(element.LengthOverride!=null) {
						length=element.LengthOverride.Value;
					}
					if(encoding==SynEncoding.ImplicitLittle) {
						WriteU32(memoryStream,length,false);
					}
					else {
						byte[] bytesVr=Encoding.ASCII.GetBytes(element.Vr);
						memoryStream.Write(bytesVr,0,bytesVr.Length);
						if(_arrayLongVrs.Contains(element.Vr)) {
							WriteU16(memoryStream,0,isBig);
							WriteU32(memoryStream,length,isBig);
						}
						else {
							WriteU16(memoryStream,(ushort)length,isBig);
						}
					}
					memoryStream.Write(bytesValue,0,bytesValue.Length);
				}
				return memoryStream.ToArray();
			}
		}

		private static byte[] EncodeItemsAs(List<SynItem> listItems,SynEncoding encoding) {
			bool isBig=encoding==SynEncoding.ExplicitBig;
			using(MemoryStream memoryStream=new MemoryStream()) {
				foreach(SynItem item in listItems) {
					byte[] bytesItem=EncodeAs(item.ListElements,encoding);
					WriteU16(memoryStream,0xFFFE,isBig);
					WriteU16(memoryStream,0xE000,isBig);
					WriteU32(memoryStream,item.IsUndefinedLength ? 0xFFFFFFFF : (uint)bytesItem.Length,isBig);
					memoryStream.Write(bytesItem,0,bytesItem.Length);
					if(item.IsUndefinedLength) {
						byte[] bytesDelimiter=DelimiterAs(0xE00D,isBig);
						memoryStream.Write(bytesDelimiter,0,bytesDelimiter.Length);
					}
				}
				return memoryStream.ToArray();
			}
		}

		private static byte[] DelimiterAs(ushort element,bool isBig) {
			using(MemoryStream memoryStream=new MemoryStream()) {
				WriteU16(memoryStream,0xFFFE,isBig);
				WriteU16(memoryStream,element,isBig);
				WriteU32(memoryStream,0,isBig);
				return memoryStream.ToArray();
			}
		}

		///<summary>RFC 1951 deflate, with no zlib header, as PS3.5 A.5 asks.</summary>
		public static byte[] Deflate(byte[] bytes) {
			using(MemoryStream memoryStream=new MemoryStream()) {
				using(DeflateStream deflateStream=new DeflateStream(memoryStream,CompressionLevel.Optimal,true)) {
					deflateStream.Write(bytes,0,bytes.Length);
				}
				return memoryStream.ToArray();
			}
		}

		///<summary>A test file in any layout and transfer syntax. The data set is encoded as the transfer syntax says unless encoding is given;
		///the deflated transfer syntax deflates the Explicit VR Little Endian data set.</summary>
		public static byte[] MakeFileIn(List<SynElement> listDataset,string transferSyntax,SynLayout layout=SynLayout.Standard,SynEncoding? encoding=null) {
			byte[] bytesDataset=EncodeAs(listDataset,encoding ?? EncodingOf(transferSyntax));
			if(transferSyntax==DeflatedExplicitVrLittleEndian && encoding==null) {
				bytesDataset=Deflate(bytesDataset);
			}
			List<SynElement> listMeta=new List<SynElement>();
			listMeta.Add(Raw(0x0002,0x0001,"OB",new byte[] { 0x00,0x01 }));
			listMeta.Add(Text(0x0002,0x0002,"UI",SopClassIntraOral));
			listMeta.Add(Text(0x0002,0x0003,"UI","2.25.1234567890123456789"));
			if(layout!=SynLayout.MetaWithoutTransferSyntax) {
				listMeta.Add(Text(0x0002,0x0010,"UI",transferSyntax));
			}
			listMeta.Add(Text(0x0002,0x0012,"UI","2.25.98765432109876543210"));
			byte[] bytesMeta=Encode(listMeta);
			byte[] bytesGroupLength=Encode(new List<SynElement> { UL(0x0002,0x0000,(uint)bytesMeta.Length) });
			using(MemoryStream memoryStream=new MemoryStream()) {
				if(layout==SynLayout.Standard || layout==SynLayout.MetaWithoutTransferSyntax) {
					memoryStream.Write(new byte[128],0,128);
				}
				if(layout==SynLayout.Standard || layout==SynLayout.MetaWithoutTransferSyntax || layout==SynLayout.PrefixWithoutPreamble) {
					memoryStream.Write(Encoding.ASCII.GetBytes("DICM"),0,4);
				}
				if(layout!=SynLayout.DatasetOnly) {
					memoryStream.Write(bytesGroupLength,0,bytesGroupLength.Length);
					memoryStream.Write(bytesMeta,0,bytesMeta.Length);
				}
				memoryStream.Write(bytesDataset,0,bytesDataset.Length);
				return memoryStream.ToArray();
			}
		}

		public static byte[] MakeFileIn(SynImage synImage,string transferSyntax,SynLayout layout=SynLayout.Standard) {
			return MakeFileIn(synImage.ToDataset(),transferSyntax,layout);
		}

		///<summary>(FFFE,E000) with a length, little-endian.</summary>
		public static byte[] ItemHeaderLE(uint length) {
			return new byte[] { 0xFE,0xFF,0x00,0xE0 }.Concat(BitConverter.GetBytes(length)).ToArray();
		}

		///<summary>Encapsulated Pixel Data (PS3.5 A.4): OB of undefined length, a Basic Offset Table item (empty when arrayOffsets is null), one
		///item per fragment (odd fragments padded with a zero byte), then the Sequence Delimitation Item.</summary>
		public static SynElement Encapsulated(uint[] arrayOffsets,params byte[][] arrayFragments) {
			using(MemoryStream memoryStream=new MemoryStream()) {
				byte[] bytesTable=arrayOffsets==null ? new byte[0] : arrayOffsets.SelectMany(x => BitConverter.GetBytes(x)).ToArray();
				memoryStream.Write(ItemHeaderLE((uint)bytesTable.Length),0,8);
				memoryStream.Write(bytesTable,0,bytesTable.Length);
				foreach(byte[] bytesFragment in arrayFragments) {
					byte[] bytesPadded=bytesFragment.Length%2==1 ? bytesFragment.Concat(new byte[1]).ToArray() : bytesFragment;
					memoryStream.Write(ItemHeaderLE((uint)bytesPadded.Length),0,8);
					memoryStream.Write(bytesPadded,0,bytesPadded.Length);
				}
				memoryStream.Write(Delimiter(0xE0DD),0,8);
				return new SynElement { Tag=Tag(0x7FE0,0x0010),Vr="OB",Value=memoryStream.ToArray(),LengthOverride=0xFFFFFFFF };
			}
		}

		///<summary>The Basic Offset Table for frames that are one fragment each: the offset of each frame's item from the first fragment's item.</summary>
		public static uint[] OffsetsOneFragmentPerFrame(params byte[][] arrayFragments) {
			uint[] arrayOffsets=new uint[arrayFragments.Length];
			uint offset=0;
			for(int i=0;i<arrayFragments.Length;i++) {
				arrayOffsets[i]=offset;
				offset+=8+(uint)(arrayFragments[i].Length+arrayFragments[i].Length%2);
			}
			return arrayOffsets;
		}

		#region RLE Lossless (PS3.5 Annex G)
		///<summary>One frame in RLE Lossless: the 64-byte header, then one segment per byte of each sample, most significant byte first (G.2),
		///each segment the PackBits-style runs of G.3.1, row by row, padded to an even length. The frame is given native and little-endian, in
		///the planar configuration given.</summary>
		public static byte[] RleFrame(byte[] bytesFrame,int rows,int columns,int samplesPerPixel,int bytesPerSample,int planarConfiguration) {
			int countPixels=rows*columns;
			List<byte[]> listSegments=new List<byte[]>();
			for(int sample=0;sample<samplesPerPixel;sample++) {
				for(int byteIndex=bytesPerSample-1;byteIndex>=0;byteIndex--) {
					byte[] bytesPlane=new byte[countPixels];
					for(int p=0;p<countPixels;p++) {
						int idxSample=planarConfiguration==0 ? p*samplesPerPixel+sample : sample*countPixels+p;
						bytesPlane[p]=bytesFrame[idxSample*bytesPerSample+byteIndex];
					}
					List<byte> listSegment=new List<byte>();
					for(int r=0;r<rows;r++) {
						listSegment.AddRange(PackBits(bytesPlane,r*columns,columns));
					}
					if(listSegment.Count%2==1) {
						listSegment.Add(0);
					}
					listSegments.Add(listSegment.ToArray());
				}
			}
			byte[] bytesHeader=new byte[64];
			BitConverter.GetBytes((uint)listSegments.Count).CopyTo(bytesHeader,0);
			uint offset=64;
			for(int i=0;i<listSegments.Count;i++) {
				BitConverter.GetBytes(offset).CopyTo(bytesHeader,4+4*i);
				offset+=(uint)listSegments[i].Length;
			}
			return bytesHeader.Concat(listSegments.SelectMany(x => x)).ToArray();
		}

		///<summary>G.3.1: a run of 2 to 128 equal bytes is -(n-1) and the byte; up to 128 other bytes are n-1 and the bytes.</summary>
		private static List<byte> PackBits(byte[] bytes,int start,int count) {
			List<byte> listOut=new List<byte>();
			int i=0;
			while(i<count) {
				int run=1;
				while(i+run<count && run<128 && bytes[start+i+run]==bytes[start+i]) {
					run++;
				}
				if(run>=2) {
					listOut.Add((byte)(sbyte)(-(run-1)));
					listOut.Add(bytes[start+i]);
					i+=run;
					continue;
				}
				int literal=1;
				while(i+literal<count && literal<128 && !(i+literal+1<count && bytes[start+i+literal]==bytes[start+i+literal+1])) {
					literal++;
				}
				listOut.Add((byte)(literal-1));
				for(int j=0;j<literal;j++) {
					listOut.Add(bytes[start+i+j]);
				}
				i+=literal;
			}
			return listOut;
		}
		#endregion RLE Lossless

		#region JPEG Baseline from the .NET Framework's encoder
		///<summary>A one-component 8-bit JPEG (Process 1) of the gray samples, from WPF's JpegBitmapEncoder.</summary>
		public static byte[] JpegGray(byte[] arrayGray,int width,int height,int quality) {
			BitmapSource bitmapSource=BitmapSource.Create(width,height,96,96,PixelFormats.Gray8,null,arrayGray,width);
			return EncodeJpeg(bitmapSource,quality);
		}

		///<summary>A three-component 8-bit JPEG (YCbCr with JFIF, chroma subsampled) of the RGB samples, from WPF's JpegBitmapEncoder.</summary>
		public static byte[] JpegRgb(byte[] arrayRgb,int width,int height,int quality) {
			BitmapSource bitmapSource=BitmapSource.Create(width,height,96,96,PixelFormats.Rgb24,null,arrayRgb,width*3);
			return EncodeJpeg(bitmapSource,quality);
		}

		private static byte[] EncodeJpeg(BitmapSource bitmapSource,int quality) {
			JpegBitmapEncoder jpegBitmapEncoder=new JpegBitmapEncoder();
			jpegBitmapEncoder.QualityLevel=quality;
			jpegBitmapEncoder.Frames.Add(BitmapFrame.Create(bitmapSource));
			using(MemoryStream memoryStream=new MemoryStream()) {
				jpegBitmapEncoder.Save(memoryStream);
				return memoryStream.ToArray();
			}
		}
		#endregion JPEG Baseline
	}
}
