// DICOM files made byte by byte in the tests, from NEMA PS3.5 (Explicit VR Little Endian encoding, sequences and items) and PS3.10 (preamble,
// prefix and File Meta Information). Everything is synthetic. Licensed under the GNU General Public License, version 2 or later. See LICENSE in
// the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DicomReaderTests {
	///<summary>One element to encode. Elements are written in the order of the list, so a test can also put them out of order.</summary>
	internal sealed class SynElement {
		public uint Tag;
		public string Vr;
		///<summary>The value bytes as written; null for a sequence.</summary>
		public byte[] Value;
		///<summary>Written in the length field instead of the real length, to make broken files.</summary>
		public uint? LengthOverride;
		///<summary>A sequence's items; null for any other VR.</summary>
		public List<SynItem> ListItems;
		///<summary>A sequence of undefined length, closed by a Sequence Delimitation Item.</summary>
		public bool IsUndefinedLength;
		///<summary>The VR whose layout (PS3.5 7.1.2) and byte order the element is written in, when that is not Vr's: two spaces for the VR in
		///the layout of SQ, UT or LO. Null: Vr's own.</summary>
		public string LayoutVr;
	}

	internal sealed class SynItem {
		public List<SynElement> ListElements=new List<SynElement>();
		///<summary>An item of undefined length, closed by an Item Delimitation Item.</summary>
		public bool IsUndefinedLength;
	}

	///<summary>A small PS3.10 writer for test files. SyntheticEncodings.cs adds the other encodings, encapsulation, RLE and JPEG.</summary>
	internal static partial class SyntheticDicom {
		public const string ExplicitVrLittleEndian="1.2.840.10008.1.2.1";
		public const string ImplicitVrLittleEndian="1.2.840.10008.1.2";
		public const string ExplicitVrBigEndian="1.2.840.10008.1.2.2";
		public const string DeflatedExplicitVrLittleEndian="1.2.840.10008.1.2.1.99";
		public const string JpegLossless="1.2.840.10008.1.2.4.70";
		public const string SopClassIntraOral="1.2.840.10008.5.1.4.1.1.1.3";
		private static readonly string[] _arrayLongVrs={ "OB","OD","OF","OL","OV","OW","SQ","SV","UC","UN","UR","UT","UV" };

		public static uint Tag(ushort group,ushort element) {
			return ((uint)group<<16) | element;
		}

		public static SynElement US(ushort group,ushort element,ushort value) {
			return new SynElement { Tag=Tag(group,element),Vr="US",Value=BitConverter.GetBytes(value) };
		}

		public static SynElement UL(ushort group,ushort element,uint value) {
			return new SynElement { Tag=Tag(group,element),Vr="UL",Value=BitConverter.GetBytes(value) };
		}

		///<summary>A string, padded to an even length with a space (NUL for UI), as PS3.5 6.2 asks.</summary>
		public static SynElement Text(ushort group,ushort element,string vr,string text) {
			byte[] bytes=Encoding.ASCII.GetBytes(text);
			if(bytes.Length%2==1) {
				Array.Resize(ref bytes,bytes.Length+1);
				bytes[bytes.Length-1]=vr=="UI" ? (byte)0 : (byte)' ';
			}
			return new SynElement { Tag=Tag(group,element),Vr=vr,Value=bytes };
		}

		public static SynElement Raw(ushort group,ushort element,string vr,byte[] value) {
			return new SynElement { Tag=Tag(group,element),Vr=vr,Value=value };
		}

		public static SynElement Sequence(ushort group,ushort element,bool isUndefinedLength,params SynItem[] arrayItems) {
			return new SynElement { Tag=Tag(group,element),Vr="SQ",ListItems=arrayItems.ToList(),IsUndefinedLength=isUndefinedLength };
		}

		public static SynItem Item(bool isUndefinedLength,params SynElement[] arrayElements) {
			return new SynItem { ListElements=arrayElements.ToList(),IsUndefinedLength=isUndefinedLength };
		}

		///<summary>16-bit samples, little-endian.</summary>
		public static byte[] Words(ushort[] arraySamples) {
			byte[] bytes=new byte[arraySamples.Length*2];
			for(int i=0;i<arraySamples.Length;i++) {
				bytes[2*i]=(byte)(arraySamples[i] & 0xFF);
				bytes[2*i+1]=(byte)(arraySamples[i]>>8);
			}
			return bytes;
		}

		///<summary>8-bit samples, padded to an even length with one zero byte when the count is odd.</summary>
		public static byte[] Bytes8(byte[] arraySamples) {
			byte[] bytes=(byte[])arraySamples.Clone();
			if(bytes.Length%2==1) {
				Array.Resize(ref bytes,bytes.Length+1);
			}
			return bytes;
		}

		///<summary>The data set of a grayscale image: SOP Common, the Image Pixel module and Pixel Data, in ascending tag order. Null for any
		///argument leaves that attribute out.</summary>
		public static List<SynElement> ImageDataset(int rows,int columns,ushort? bitsAllocated,ushort? bitsStored,ushort? highBit,
			ushort? pixelRepresentation,string photometric,byte[] pixelData,string pixelVr="OW",ushort? samplesPerPixel=1)
		{
			List<SynElement> listElements=new List<SynElement>();
			listElements.Add(Text(0x0008,0x0016,"UI",SopClassIntraOral));
			listElements.Add(Text(0x0008,0x0018,"UI","2.25.1234567890123456789"));
			listElements.Add(Text(0x0008,0x0060,"CS","IO"));
			if(samplesPerPixel!=null) {
				listElements.Add(US(0x0028,0x0002,samplesPerPixel.Value));
			}
			if(photometric!=null) {
				listElements.Add(Text(0x0028,0x0004,"CS",photometric));
			}
			listElements.Add(US(0x0028,0x0010,(ushort)rows));
			listElements.Add(US(0x0028,0x0011,(ushort)columns));
			if(bitsAllocated!=null) {
				listElements.Add(US(0x0028,0x0100,bitsAllocated.Value));
			}
			if(bitsStored!=null) {
				listElements.Add(US(0x0028,0x0101,bitsStored.Value));
			}
			if(highBit!=null) {
				listElements.Add(US(0x0028,0x0102,highBit.Value));
			}
			if(pixelRepresentation!=null) {
				listElements.Add(US(0x0028,0x0103,pixelRepresentation.Value));
			}
			if(pixelData!=null) {
				listElements.Add(Raw(0x7FE0,0x0010,pixelVr,pixelData));
			}
			return listElements;
		}

		///<summary>An unsigned 16-bit MONOCHROME2 image of these samples.</summary>
		public static List<SynElement> Image16(int rows,int columns,ushort bitsStored,ushort[] arraySamples,string photometric="MONOCHROME2") {
			return ImageDataset(rows,columns,16,bitsStored,(ushort)(bitsStored-1),0,photometric,Words(arraySamples));
		}

		///<summary>Inserts an element in ascending tag order.</summary>
		public static List<SynElement> With(List<SynElement> listElements,SynElement element) {
			List<SynElement> listNew=listElements.Where(x => x.Tag!=element.Tag).ToList();
			int idx=listNew.FindIndex(x => x.Tag>element.Tag);
			if(idx<0) {
				listNew.Add(element);
			}
			else {
				listNew.Insert(idx,element);
			}
			return listNew;
		}

		public static List<SynElement> Without(List<SynElement> listElements,uint tag) {
			return listElements.Where(x => x.Tag!=tag).ToList();
		}

		///<summary>A whole PS3.10 file: preamble, "DICM", File Meta Information (with a correct group length unless told otherwise), data set.</summary>
		public static byte[] MakeFile(List<SynElement> listDataset,string transferSyntax=ExplicitVrLittleEndian,bool hasPreamble=true,
			bool hasGroupLength=true,int groupLengthError=0,bool hasTransferSyntax=true)
		{
			List<SynElement> listMeta=new List<SynElement>();
			listMeta.Add(Raw(0x0002,0x0001,"OB",new byte[] { 0x00,0x01 }));
			listMeta.Add(Text(0x0002,0x0002,"UI",SopClassIntraOral));
			listMeta.Add(Text(0x0002,0x0003,"UI","2.25.1234567890123456789"));
			if(hasTransferSyntax) {
				listMeta.Add(Text(0x0002,0x0010,"UI",transferSyntax));
			}
			listMeta.Add(Text(0x0002,0x0012,"UI","2.25.98765432109876543210"));
			byte[] bytesMeta=Encode(listMeta);
			using(MemoryStream memoryStream=new MemoryStream()) {
				if(hasPreamble) {
					memoryStream.Write(new byte[128],0,128);
					memoryStream.Write(Encoding.ASCII.GetBytes("DICM"),0,4);
				}
				if(hasGroupLength) {
					byte[] bytesGroupLength=Encode(new List<SynElement> { UL(0x0002,0x0000,(uint)(bytesMeta.Length+groupLengthError)) });
					memoryStream.Write(bytesGroupLength,0,bytesGroupLength.Length);
				}
				memoryStream.Write(bytesMeta,0,bytesMeta.Length);
				byte[] bytesDataset=Encode(listDataset);
				memoryStream.Write(bytesDataset,0,bytesDataset.Length);
				return memoryStream.ToArray();
			}
		}

		///<summary>Explicit VR Little Endian (PS3.5 7.1.2, 7.5).</summary>
		public static byte[] Encode(List<SynElement> listElements) {
			using(MemoryStream memoryStream=new MemoryStream()) {
				using(BinaryWriter binaryWriter=new BinaryWriter(memoryStream)) {
					foreach(SynElement element in listElements) {
						binaryWriter.Write((ushort)(element.Tag>>16));
						binaryWriter.Write((ushort)(element.Tag & 0xFFFF));
						binaryWriter.Write(Encoding.ASCII.GetBytes(element.Vr));
						string vrLayout=element.LayoutVr ?? element.Vr;
						byte[] bytesValue=element.Value;
						uint length;
						if(vrLayout=="SQ") {
							bytesValue=EncodeItems(element.ListItems);
							if(element.IsUndefinedLength) {
								bytesValue=bytesValue.Concat(Delimiter(0xE0DD)).ToArray();
							}
							length=element.IsUndefinedLength ? 0xFFFFFFFF : (uint)bytesValue.Length;
						}
						else {
							length=(uint)bytesValue.Length;
						}
						if(element.LengthOverride!=null) {
							length=element.LengthOverride.Value;
						}
						if(_arrayLongVrs.Contains(vrLayout)) {
							binaryWriter.Write((ushort)0);
							binaryWriter.Write(length);
						}
						else {
							binaryWriter.Write((ushort)length);
						}
						binaryWriter.Write(bytesValue);
					}
				}
				return memoryStream.ToArray();
			}
		}

		private static byte[] EncodeItems(List<SynItem> listItems) {
			using(MemoryStream memoryStream=new MemoryStream()) {
				using(BinaryWriter binaryWriter=new BinaryWriter(memoryStream)) {
					foreach(SynItem item in listItems) {
						byte[] bytesItem=Encode(item.ListElements);
						binaryWriter.Write((ushort)0xFFFE);
						binaryWriter.Write((ushort)0xE000);
						binaryWriter.Write(item.IsUndefinedLength ? 0xFFFFFFFF : (uint)bytesItem.Length);
						binaryWriter.Write(bytesItem);
						if(item.IsUndefinedLength) {
							binaryWriter.Write(Delimiter(0xE00D));
						}
					}
				}
				return memoryStream.ToArray();
			}
		}

		///<summary>(FFFE,element) with a zero length: an Item or Sequence Delimitation Item.</summary>
		public static byte[] Delimiter(ushort element) {
			byte[] bytes=new byte[8];
			BitConverter.GetBytes((ushort)0xFFFE).CopyTo(bytes,0);
			BitConverter.GetBytes(element).CopyTo(bytes,2);
			return bytes;
		}

		///<summary>Samples 0..count-1 spread over 0..maxValue, with a few repeats, so that every test image has many distinct values.</summary>
		public static ushort[] Ramp(int count,int maxValue) {
			ushort[] arraySamples=new ushort[count];
			for(int i=0;i<count;i++) {
				arraySamples[i]=(ushort)((i*7919L)%(maxValue+1));
			}
			return arraySamples;
		}
	}
}
