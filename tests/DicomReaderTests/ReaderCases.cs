// The synthetic DICOM files that every reader of DicomHelper is checked against: each encoding, photometric interpretation, bit depth and
// compressed format the program meets, made in the test from NEMA PS3.3, PS3.5 and PS3.10 (SyntheticDicom.cs, SyntheticEncodings.cs). No
// patient image. FoDicomBaseline.cs holds what fo-dicom 4.0.7, the reader before the in-house one, read from each of them.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static DicomReaderTests.SyntheticDicom;

namespace DicomReaderTests {
	internal sealed class ReaderCase {
		public string Name;
		public byte[] Bytes;

		public ReaderCase(string name,byte[] bytes) {
			Name=name;
			Bytes=bytes;
		}
	}

	internal static class ReaderCases {
		#region Pixel values
		///<summary>Words of 0..maxValue with many distinct values.</summary>
		public static ushort[] Words(int count,int maxValue) {
			return Ramp(count,maxValue);
		}

		///<summary>Bytes of 0..maxValue with many distinct values.</summary>
		public static byte[] Bytes(int count,int maxValue) {
			return Ramp(count,maxValue).Select(x => (byte)x).ToArray();
		}

		///<summary>Signed samples from minValue to maxValue, as two's complement words.</summary>
		public static ushort[] SignedWords(int count,int minValue,int maxValue) {
			ushort[] arrayWords=new ushort[count];
			int range=maxValue-minValue+1;
			for(int i=0;i<count;i++) {
				int value=minValue+(int)((i*7919L)%range);
				arrayWords[i]=unchecked((ushort)(short)value);
			}
			return arrayWords;
		}

		///<summary>Interleaved RGB (or any three samples) with runs and changes, 8 bits each.</summary>
		public static byte[] Color8(int countPixels) {
			byte[] bytes=new byte[countPixels*3];
			for(int p=0;p<countPixels;p++) {
				bytes[3*p]=(byte)((p*37)%256);
				bytes[3*p+1]=(byte)((p/3*53+11)%256);
				bytes[3*p+2]=(byte)(p%5==0 ? 255 : (p*91)%256);
			}
			return bytes;
		}

		///<summary>Interleaved samples to planar (all of sample 0, then 1, then 2).</summary>
		public static byte[] ToPlanar(byte[] bytesInterleaved,int samplesPerPixel) {
			int countPixels=bytesInterleaved.Length/samplesPerPixel;
			byte[] bytesPlanar=new byte[bytesInterleaved.Length];
			for(int p=0;p<countPixels;p++) {
				for(int s=0;s<samplesPerPixel;s++) {
					bytesPlanar[s*countPixels+p]=bytesInterleaved[p*samplesPerPixel+s];
				}
			}
			return bytesPlanar;
		}

		///<summary>Gray samples with flat areas (runs) and a gradient, for RLE and JPEG.</summary>
		public static byte[] Gray8(int width,int height) {
			byte[] bytes=new byte[width*height];
			for(int y=0;y<height;y++) {
				for(int x=0;x<width;x++) {
					bytes[y*width+x]=x<width/3 ? (byte)40 : (byte)Math.Min(255,60+x*9+y*7);
				}
			}
			return bytes;
		}

		///<summary>RGB with a gradient, a flat area and a hard edge, for JPEG.</summary>
		public static byte[] Rgb8Smooth(int width,int height) {
			byte[] bytes=new byte[width*height*3];
			for(int y=0;y<height;y++) {
				for(int x=0;x<width;x++) {
					int i=3*(y*width+x);
					bytes[i]=(byte)Math.Min(255,20+x*10);
					bytes[i+1]=(byte)Math.Min(255,30+y*15);
					bytes[i+2]=x>width/2 ? (byte)200 : (byte)50;
				}
			}
			return bytes;
		}
		#endregion Pixel values

		#region Images
		public static SynImage Mono16(int rows,int columns,ushort bitsStored,ushort highBit,ushort[] arrayWords,string photometric="MONOCHROME2",
			ushort pixelRepresentation=0)
		{
			return new SynImage { Rows=rows,Columns=columns,Photometric=photometric,BitsAllocated=16,BitsStored=bitsStored,HighBit=highBit,
				PixelRepresentation=pixelRepresentation,PixelBytes=SyntheticDicom.Words(arrayWords) };
		}

		public static SynImage Mono8(int rows,int columns,ushort bitsStored,ushort highBit,byte[] arrayBytes,string photometric="MONOCHROME2",
			ushort pixelRepresentation=0)
		{
			return new SynImage { Rows=rows,Columns=columns,Photometric=photometric,BitsAllocated=8,BitsStored=bitsStored,HighBit=highBit,
				PixelRepresentation=pixelRepresentation,PixelBytes=arrayBytes,PixelVr="OB" };
		}

		///<summary>The 12-bit image most encoding cases use: 7 x 5, values up to 4095.</summary>
		public static SynImage Twelve() {
			return Mono16(5,7,12,11,Words(35,4095));
		}

		///<summary>The 8-bit image most encoding cases use: 3 x 5, an odd sample count.</summary>
		public static SynImage Eight() {
			return Mono8(3,5,8,7,Bytes(15,255));
		}

		public static SynImage Color(string photometric,int rows,int columns,byte[] bytesPixels,ushort planarConfiguration) {
			return new SynImage { Rows=rows,Columns=columns,SamplesPerPixel=3,Photometric=photometric,PlanarConfiguration=planarConfiguration,
				BitsAllocated=8,BitsStored=8,HighBit=7,PixelRepresentation=0,PixelBytes=bytesPixels,PixelVr="OB" };
		}

		///<summary>Two sequences of undefined length with nested items of both kinds, and one of defined length, before the image module.</summary>
		public static SynImage WithSequences(SynImage synImage) {
			SynImage synImageNew=synImage.Clone();
			SynItem itemNested=Item(true,Text(0x0008,0x0100,"SH","12"),
				Sequence(0x0008,0x2228,true,Item(false,Text(0x0008,0x0100,"SH","13"),US(0x0028,0x0010,99)),Item(true,Text(0x0008,0x0100,"SH","14"))));
			synImageNew.ListExtra.Add(Sequence(0x0008,0x2218,true,Item(true,Text(0x0008,0x0102,"SH","SRT")),itemNested));
			synImageNew.ListExtra.Add(Sequence(0x0040,0x0260,false,Item(false,US(0x0028,0x0011,98))));
			synImageNew.ListExtra.Add(Raw(0x0009,0x1010,"OB",new byte[] { 1,2,3,4 }));
			return synImageNew;
		}

		private static SynElement Ds(ushort group,ushort element,string value) {
			return Text(group,element,"DS",value);
		}

		///<summary>Palette Color Lookup Table Descriptors and Data (PS3.3 C.7.6.3.1.5, C.7.6.3.1.6).</summary>
		public static void AddPalette(SynImage synImage,int countEntries,int firstMapped,int bitsPerEntry,Func<int,int,int> funcEntry) {
			ushort[] arrayDescriptor={ (ushort)(countEntries==65536 ? 0 : countEntries),(ushort)firstMapped,(ushort)bitsPerEntry };
			for(int color=0;color<3;color++) {
				synImage.ListExtra.Add(Raw(0x0028,(ushort)(0x1101+color),"US",SyntheticDicom.Words(arrayDescriptor)));
				byte[] bytesData;
				if(bitsPerEntry==16) {
					bytesData=SyntheticDicom.Words(Enumerable.Range(0,countEntries).Select(x => (ushort)funcEntry(color,x)).ToArray());
				}
				else {
					bytesData=Bytes8(Enumerable.Range(0,countEntries).Select(x => (byte)funcEntry(color,x)).ToArray());
				}
				synImage.ListExtra.Add(Raw(0x0028,(ushort)(0x1201+color),"OW",bytesData));
			}
		}

		///<summary>A 16-bit palette entry for color 0 (red), 1 (green) or 2 (blue).</summary>
		public static int PaletteEntry16(int color,int index) {
			switch(color) {
				case 0: return (index*257*3)%65536;
				case 1: return 65535-(index*500)%65536;
				default: return (index%7)*9000;
			}
		}
		#endregion Images

		#region The cases
		///<summary>Every case, by name. Deterministic: the same bytes on every run.</summary>
		public static List<ReaderCase> All() {
			List<ReaderCase> listCases=new List<ReaderCase>();
			listCases.AddRange(Encodings());
			listCases.AddRange(Images());
			listCases.AddRange(Compressed());
			return listCases;
		}

		public static ReaderCase Get(string name) {
			return All().Single(x => x.Name==name);
		}

		public static List<ReaderCase> Encodings() {
			List<ReaderCase> listCases=new List<ReaderCase>();
			listCases.Add(new ReaderCase("enc-explicit-le-12",MakeFileIn(Twelve(),ExplicitVrLittleEndian)));
			listCases.Add(new ReaderCase("enc-explicit-le-8",MakeFileIn(Eight(),ExplicitVrLittleEndian)));
			listCases.Add(new ReaderCase("enc-implicit-le-12",MakeFileIn(Twelve(),ImplicitVrLittleEndian)));
			listCases.Add(new ReaderCase("enc-implicit-le-8",MakeFileIn(Eight(),ImplicitVrLittleEndian)));
			listCases.Add(new ReaderCase("enc-explicit-be-12",MakeFileIn(Twelve(),ExplicitVrBigEndian)));
			listCases.Add(new ReaderCase("enc-explicit-be-8-ob",MakeFileIn(Eight(),ExplicitVrBigEndian)));
			SynImage synImageEightOw=Mono8(3,4,8,7,Bytes(12,255));
			synImageEightOw.PixelVr="OW";
			listCases.Add(new ReaderCase("enc-explicit-be-8-ow",MakeFileIn(synImageEightOw,ExplicitVrBigEndian)));
			listCases.Add(new ReaderCase("enc-explicit-be-16",MakeFileIn(Mono16(4,6,16,15,Words(24,65535)),ExplicitVrBigEndian)));
			listCases.Add(new ReaderCase("enc-deflate-12",MakeFileIn(Twelve(),DeflatedExplicitVrLittleEndian)));
			listCases.Add(new ReaderCase("enc-deflate-8",MakeFileIn(Eight(),DeflatedExplicitVrLittleEndian)));
			listCases.Add(new ReaderCase("enc-no-preamble-meta-explicit-12",MakeFileIn(Twelve(),ExplicitVrLittleEndian,SynLayout.MetaWithoutPreamble)));
			listCases.Add(new ReaderCase("enc-no-preamble-meta-implicit-12",MakeFileIn(Twelve(),ImplicitVrLittleEndian,SynLayout.MetaWithoutPreamble)));
			listCases.Add(new ReaderCase("enc-prefix-no-preamble-12",MakeFileIn(Twelve(),ExplicitVrLittleEndian,SynLayout.PrefixWithoutPreamble)));
			listCases.Add(new ReaderCase("enc-dataset-only-implicit-12",MakeFileIn(Twelve(),ImplicitVrLittleEndian,SynLayout.DatasetOnly)));
			listCases.Add(new ReaderCase("enc-dataset-only-explicit-12",MakeFileIn(Twelve(),ExplicitVrLittleEndian,SynLayout.DatasetOnly)));
			listCases.Add(new ReaderCase("enc-dataset-only-explicit-be-12",MakeFileIn(Twelve(),ExplicitVrBigEndian,SynLayout.DatasetOnly)));
			listCases.Add(new ReaderCase("enc-meta-without-ts-implicit-12",MakeFileIn(Twelve(),ImplicitVrLittleEndian,SynLayout.MetaWithoutTransferSyntax)));
			listCases.Add(new ReaderCase("enc-meta-without-ts-explicit-12",MakeFileIn(Twelve(),ExplicitVrLittleEndian,SynLayout.MetaWithoutTransferSyntax)));
			listCases.Add(new ReaderCase("enc-explicit-le-sequences-12",MakeFileIn(WithSequences(Twelve()),ExplicitVrLittleEndian)));
			listCases.Add(new ReaderCase("enc-implicit-le-sequences-12",MakeFileIn(WithSequences(Twelve()),ImplicitVrLittleEndian)));
			listCases.Add(new ReaderCase("enc-explicit-be-sequences-12",MakeFileIn(WithSequences(Twelve()),ExplicitVrBigEndian)));
			listCases.Add(new ReaderCase("enc-deflate-sequences-12",MakeFileIn(WithSequences(Twelve()),DeflatedExplicitVrLittleEndian)));
			//UN of undefined length holds Implicit VR Little Endian items (PS3.5 6.2.2).
			SynImage synImageUn=Twelve();
			byte[] bytesUnValue=ItemHeaderLE(0xFFFFFFFF).Concat(EncodeAs(new List<SynElement> { Text(0x0008,0x0100,"SH","AB") },SynEncoding.ImplicitLittle))
				.Concat(Delimiter(0xE00D)).Concat(Delimiter(0xE0DD)).ToArray();
			synImageUn.ListExtra.Add(new SynElement { Tag=Tag(0x0009,0x1020),Vr="UN",Value=bytesUnValue,LengthOverride=0xFFFFFFFF });
			listCases.Add(new ReaderCase("enc-explicit-le-un-undefined-12",MakeFileIn(synImageUn,ExplicitVrLittleEndian)));
			listCases.AddRange(MislabelledEncodings());
			return listCases;
		}

		///<summary>Files that break PS3.5 or PS3.10 in ways real writers did, which fo-dicom 4 read: an Implicit VR data set under an Explicit VR
		///transfer syntax, two spaces where an Explicit VR element's VR goes, File Meta Information in Implicit VR, data sets with no File Meta
		///Information whose first element is private, of group 0004 or out of tag order, and a preamble of zeros without "DICM" (fo-dicom read
		///the Implicit VR data set after it, not the Explicit VR one).</summary>
		public static List<ReaderCase> MislabelledEncodings() {
			List<ReaderCase> listCases=new List<ReaderCase>();
			List<SynElement> listTwelve=Twelve().ToDataset();
			listCases.Add(new ReaderCase("enc-ts-explicit-dataset-implicit-12",MakeFileIn(listTwelve,ExplicitVrLittleEndian,encoding:SynEncoding.ImplicitLittle)));
			listCases.Add(new ReaderCase("enc-ts-deflate-dataset-implicit-12",MakeDeflatedFileOf(EncodeAs(listTwelve,SynEncoding.ImplicitLittle))));
			listCases.Add(new ReaderCase("enc-explicit-le-vr-spaces-12",MakeFileIn(WithVr(listTwelve,Tag(0x0028,0x0010),"  "),ExplicitVrLittleEndian)));
			//Big endian: the encoder swaps values by their VR, and "  " has none, so the value is given big-endian here (Rows 5).
			listCases.Add(new ReaderCase("enc-explicit-be-vr-spaces-12",MakeFileIn(With(listTwelve,Raw(0x0028,0x0010,"  ",new byte[] { 0x00,0x05 })),
				ExplicitVrBigEndian)));
			//Pixel Data with two spaces for its VR, in the layout of OW: two reserved bytes and a 32-bit length.
			SynElement elementPixels=listTwelve.Single(x => x.Tag==Tag(0x7FE0,0x0010));
			byte[] bytesPixelsSpaces=MakeFileIn(Without(listTwelve,Tag(0x7FE0,0x0010)),ExplicitVrLittleEndian)
				.Concat(new byte[] { 0xE0,0x7F,0x10,0x00,0x20,0x20,0x00,0x00 }).Concat(BitConverter.GetBytes((uint)elementPixels.Value.Length))
				.Concat(elementPixels.Value).ToArray();
			listCases.Add(new ReaderCase("enc-explicit-le-vr-spaces-pixel-data-12",bytesPixelsSpaces));
			listCases.Add(new ReaderCase("enc-meta-implicit-ts-implicit-12",MakeFileIn(listTwelve,ImplicitVrLittleEndian,SynLayout.MetaImplicit)));
			listCases.Add(new ReaderCase("enc-meta-implicit-ts-explicit-12",MakeFileIn(listTwelve,ExplicitVrLittleEndian,SynLayout.MetaImplicit)));
			listCases.Add(new ReaderCase("enc-meta-implicit-no-preamble-12",MakeFileIn(listTwelve,ImplicitVrLittleEndian,SynLayout.MetaImplicitWithoutPreamble)));
			listCases.Add(new ReaderCase("enc-meta-implicit-no-preamble-ts-explicit-12",
				MakeFileIn(listTwelve,ExplicitVrLittleEndian,SynLayout.MetaImplicitWithoutPreamble)));
			//No File Meta Information, and the first element is a private one (group 0009), or of group 0040 because the tags are out of order.
			List<SynElement> listPrivateFirst=With(listTwelve.Where(x => (x.Tag>>16)!=0x0008).ToList(),Text(0x0009,0x0010,"LO","PRIVATE CREATOR"));
			listCases.Add(new ReaderCase("enc-dataset-only-implicit-private-first-12",MakeFileIn(listPrivateFirst,ImplicitVrLittleEndian,SynLayout.DatasetOnly)));
			listCases.Add(new ReaderCase("enc-dataset-only-explicit-private-first-12",MakeFileIn(listPrivateFirst,ExplicitVrLittleEndian,SynLayout.DatasetOnly)));
			List<SynElement> listGroup40First=new List<SynElement> { Text(0x0040,0x0254,"LO","STUDY") }.Concat(listTwelve).ToList();
			listCases.Add(new ReaderCase("enc-dataset-only-implicit-group-0040-first-12",MakeFileIn(listGroup40First,ImplicitVrLittleEndian,SynLayout.DatasetOnly)));
			//Group 0004 (directory structuring, PS3.3 F.3) first.
			List<SynElement> listGroup4First=new List<SynElement> { Text(0x0004,0x1130,"CS","FILESET") }.Concat(listTwelve).ToList();
			listCases.Add(new ReaderCase("enc-dataset-only-implicit-group-0004-first-12",MakeFileIn(listGroup4First,ImplicitVrLittleEndian,SynLayout.DatasetOnly)));
			//A preamble of zeros without "DICM" or File Meta Information.
			listCases.Add(new ReaderCase("enc-zero-preamble-no-prefix-implicit-12",MakeFileIn(listTwelve,ImplicitVrLittleEndian,SynLayout.PreambleWithoutPrefix)));
			listCases.Add(new ReaderCase("enc-zero-preamble-no-prefix-explicit-12",MakeFileIn(listTwelve,ExplicitVrLittleEndian,SynLayout.PreambleWithoutPrefix)));
			return listCases;
		}

		///<summary>The elements, with another VR written for the one of the tag.</summary>
		public static List<SynElement> WithVr(List<SynElement> listElements,uint tag,string vr) {
			return listElements.Select(x => x.Tag!=tag ? x : new SynElement { Tag=x.Tag,Vr=vr,Value=x.Value,LengthOverride=x.LengthOverride }).ToList();
		}

		public static List<ReaderCase> Images() {
			List<ReaderCase> listCases=new List<ReaderCase>();
			string ts=ExplicitVrLittleEndian;
			//Grayscale depths. Words carry noise outside the stored bits, so that masking and shifting show.
			listCases.Add(new ReaderCase("img-mono1-16-16-15",MakeFileIn(Mono16(4,5,16,15,Words(20,65535),"MONOCHROME1"),ts)));
			listCases.Add(new ReaderCase("img-mono1-16-12-11",MakeFileIn(Mono16(4,5,12,11,Words(20,4095),"MONOCHROME1"),ts)));
			listCases.Add(new ReaderCase("img-mono1-8-8-7",MakeFileIn(Mono8(3,5,8,7,Bytes(15,255),"MONOCHROME1"),ts)));
			listCases.Add(new ReaderCase("img-mono2-16-12-11-noise",MakeFileIn(Mono16(4,5,12,11,Noisy(Words(20,4095),12,11)),ts)));
			foreach((ushort bitsStored,ushort highBit) pair in new (ushort,ushort)[] { (8,7),(8,15),(10,9),(12,15),(12,13),(13,12),(14,13),(15,14),(16,15),
				(1,0),(4,3) })
			{
				int maxValue=(1<<pair.bitsStored)-1;
				listCases.Add(new ReaderCase("img-mono2-16-"+pair.bitsStored+"-"+pair.highBit,
					MakeFileIn(Mono16(4,5,pair.bitsStored,pair.highBit,Noisy(Words(20,maxValue),pair.bitsStored,pair.highBit)),ts)));
			}
			foreach((ushort bitsStored,ushort highBit) pair in new (ushort,ushort)[] { (8,7),(6,5),(4,7),(1,0),(7,6) }) {
				int maxValue=(1<<pair.bitsStored)-1;
				byte[] arrayBytes=Noisy(Words(15,maxValue),pair.bitsStored,pair.highBit).Select(x => (byte)x).ToArray();
				listCases.Add(new ReaderCase("img-mono2-8-"+pair.bitsStored+"-"+pair.highBit,MakeFileIn(Mono8(3,5,pair.bitsStored,pair.highBit,arrayBytes),ts)));
			}
			//Signed.
			listCases.Add(new ReaderCase("img-signed-16-16-15",MakeFileIn(Mono16(4,5,16,15,SignedWords(20,-32768,32767),pixelRepresentation:1),ts)));
			listCases.Add(new ReaderCase("img-signed-16-12-11",MakeFileIn(Mono16(4,5,12,11,SignedWords(20,-2048,2047),pixelRepresentation:1),ts)));
			listCases.Add(new ReaderCase("img-signed-16-12-11-masked",
				MakeFileIn(Mono16(4,5,12,11,SignedWords(20,-2048,2047).Select(x => (ushort)(x & 0x0FFF)).ToArray(),pixelRepresentation:1),ts)));
			listCases.Add(new ReaderCase("img-signed-16-14-13",MakeFileIn(Mono16(4,5,14,13,SignedWords(20,-8192,8191),pixelRepresentation:1),ts)));
			listCases.Add(new ReaderCase("img-signed-8-8-7",
				MakeFileIn(Mono8(3,5,8,7,SignedWords(15,-128,127).Select(x => (byte)x).ToArray(),pixelRepresentation:1),ts)));
			listCases.Add(new ReaderCase("img-signed-mono1-16-16-15",
				MakeFileIn(Mono16(4,5,16,15,SignedWords(20,-1000,3000),"MONOCHROME1",1),ts)));
			//Rescale and VOI window: BitmapDicom carries neither.
			SynImage synImageRescale=Twelve();
			synImageRescale.ListExtra.Add(Ds(0x0028,0x1052,"-1024"));
			synImageRescale.ListExtra.Add(Ds(0x0028,0x1053,"2"));
			synImageRescale.ListExtra.Add(Text(0x0028,0x1054,"LO","HU"));
			listCases.Add(new ReaderCase("img-rescale-12",MakeFileIn(synImageRescale,ts)));
			SynImage synImageWindow=Twelve();
			synImageWindow.ListExtra.Add(Ds(0x0028,0x1050,"2048\\1000"));
			synImageWindow.ListExtra.Add(Ds(0x0028,0x1051,"1000\\500"));
			synImageWindow.ListExtra.Add(Text(0x0028,0x1056,"CS","LINEAR"));
			listCases.Add(new ReaderCase("img-window-12",MakeFileIn(synImageWindow,ts)));
			SynImage synImageSignedRescale=Mono16(4,5,16,15,SignedWords(20,-1024,3071),pixelRepresentation:1);
			synImageSignedRescale.ListExtra.Add(Ds(0x0028,0x1052,"0"));
			synImageSignedRescale.ListExtra.Add(Ds(0x0028,0x1053,"1"));
			synImageSignedRescale.ListExtra.Add(Ds(0x0028,0x1050,"40"));
			synImageSignedRescale.ListExtra.Add(Ds(0x0028,0x1051,"400"));
			listCases.Add(new ReaderCase("img-signed-rescale-window-16",MakeFileIn(synImageSignedRescale,ts)));
			//Color.
			byte[] bytesColor=Color8(12);
			listCases.Add(new ReaderCase("img-rgb-planar0",MakeFileIn(Color("RGB",3,4,bytesColor,0),ts)));
			listCases.Add(new ReaderCase("img-rgb-planar1",MakeFileIn(Color("RGB",3,4,ToPlanar(bytesColor,3),1),ts)));
			listCases.Add(new ReaderCase("img-rgb-implicit",MakeFileIn(Color("RGB",3,4,bytesColor,0),ImplicitVrLittleEndian)));
			listCases.Add(new ReaderCase("img-rgb-big-endian",MakeFileIn(Color("RGB",3,4,bytesColor,0),ExplicitVrBigEndian)));
			SynImage synImageRgb16=new SynImage { Rows=2,Columns=3,SamplesPerPixel=3,Photometric="RGB",PlanarConfiguration=0,BitsAllocated=16,BitsStored=16,
				HighBit=15,PixelRepresentation=0,PixelBytes=SyntheticDicom.Words(Words(18,65535)) };
			listCases.Add(new ReaderCase("img-rgb-16",MakeFileIn(synImageRgb16,ts)));
			listCases.Add(new ReaderCase("img-ybr-full-planar0",MakeFileIn(Color("YBR_FULL",3,4,bytesColor,0),ts)));
			listCases.Add(new ReaderCase("img-ybr-full-planar1",MakeFileIn(Color("YBR_FULL",3,4,ToPlanar(bytesColor,3),1),ts)));
			//YBR_FULL_422 (PS3.3 C.7.6.3.1.2): Y1 Y2 Cb Cr for each pair of pixels in a row, so 2 bytes per pixel.
			listCases.Add(new ReaderCase("img-ybr-full-422",MakeFileIn(Color("YBR_FULL_422",3,4,Bytes(24,255),0),ts)));
			//Palette color.
			SynImage synImagePalette8=Mono8(3,5,8,7,Bytes(15,255),"PALETTE COLOR");
			AddPalette(synImagePalette8,256,0,16,PaletteEntry16);
			listCases.Add(new ReaderCase("img-palette-8-lut16",MakeFileIn(synImagePalette8,ts)));
			SynImage synImagePaletteLut8=Mono8(3,5,8,7,Bytes(15,255),"PALETTE COLOR");
			AddPalette(synImagePaletteLut8,256,0,8,(color,index) => (index*(color+1)*31)%256);
			listCases.Add(new ReaderCase("img-palette-8-lut8",MakeFileIn(synImagePaletteLut8,ts)));
			SynImage synImagePalette16=Mono16(4,5,16,15,Words(20,4095).Select(x => (ushort)(x+100)).ToArray(),"PALETTE COLOR");
			AddPalette(synImagePalette16,4096,100,16,PaletteEntry16);
			listCases.Add(new ReaderCase("img-palette-16-lut16",MakeFileIn(synImagePalette16,ts)));
			SynImage synImagePaletteFirst=Mono8(3,5,8,7,Bytes(15,255),"PALETTE COLOR");
			AddPalette(synImagePaletteFirst,100,50,16,PaletteEntry16);//indices below 50 and above 149 are clamped to the first and last entry
			listCases.Add(new ReaderCase("img-palette-8-first-mapped",MakeFileIn(synImagePaletteFirst,ts)));
			//Multi-frame: the program shows the first frame.
			SynImage synImageFrames=Mono16(4,5,12,11,Words(60,4095));
			synImageFrames.NumberOfFrames="3";
			listCases.Add(new ReaderCase("img-multiframe-12",MakeFileIn(synImageFrames,ts)));
			listCases.Add(new ReaderCase("img-multiframe-12-implicit",MakeFileIn(synImageFrames,ImplicitVrLittleEndian)));
			SynImage synImageFramesRgb=Color("RGB",3,4,Color8(24),0);
			synImageFramesRgb.NumberOfFrames="2";
			listCases.Add(new ReaderCase("img-multiframe-rgb",MakeFileIn(synImageFramesRgb,ts)));
			SynImage synImageFrames8=Mono8(3,5,8,7,Bytes(30,255));
			synImageFrames8.NumberOfFrames="2";
			listCases.Add(new ReaderCase("img-multiframe-8-odd",MakeFileIn(synImageFrames8,ts)));
			//Larger than the small test images: 1200 x 1000 at 16 bits (2.4 MB).
			listCases.Add(new ReaderCase("img-large-16",MakeFileIn(Mono16(1000,1200,16,15,Words(1200*1000,65535)),ts)));
			//Tags out of order: Columns before Rows.
			List<SynElement> listOutOfOrder=Twelve().ToDataset();
			SynElement elementRows=listOutOfOrder.Single(x => x.Tag==Tag(0x0028,0x0010));
			listOutOfOrder.Remove(elementRows);
			listOutOfOrder.Insert(listOutOfOrder.FindIndex(x => x.Tag==Tag(0x0028,0x0011))+1,elementRows);
			listCases.Add(new ReaderCase("img-out-of-order-12",MakeFileIn(listOutOfOrder,ts)));
			return listCases;
		}

		///<summary>Stored values placed at High Bit, with the other bits of each word set from a pattern (PS3.5 8.1.1: the bits outside the
		///stored ones are not part of the value).</summary>
		public static ushort[] Noisy(ushort[] arrayValues,int bitsStored,int highBit) {
			int shift=highBit-bitsStored+1;
			int maskStored=((1<<bitsStored)-1)<<shift;
			ushort[] arrayWords=new ushort[arrayValues.Length];
			for(int i=0;i<arrayValues.Length;i++) {
				int noise=(i*0x5A5B+0x1234) & 0xFFFF;
				arrayWords[i]=(ushort)(((arrayValues[i]<<shift) & maskStored) | (noise & ~maskStored & 0xFFFF));
			}
			return arrayWords;
		}

		///<summary>The fixed JPEG Baseline streams the JPEG cases use (FoDicomBaseline.cs records what fo-dicom decoded from exactly these bytes).
		///They were made by the .NET Framework's JpegBitmapEncoder from Gray8(21,13) at quality 90 and Rgb8Smooth(22,14) at quality 90; the
		///round-trip tests encode afresh.</summary>
		public static byte[] JpegGrayFixed() {
			return Convert.FromBase64String(FoDicomBaseline.JpegGrayBase64);
		}

		public static byte[] JpegRgbFixed() {
			return Convert.FromBase64String(FoDicomBaseline.JpegRgbBase64);
		}

		public static SynImage Encapsulated8(string photometric,int rows,int columns,ushort samplesPerPixel,SynElement elementPixels) {
			return new SynImage { Rows=rows,Columns=columns,SamplesPerPixel=samplesPerPixel,Photometric=photometric,
				PlanarConfiguration=samplesPerPixel==3 ? (ushort?)0 : null,BitsAllocated=8,BitsStored=8,HighBit=7,PixelRepresentation=0,PixelElement=elementPixels };
		}

		public static List<ReaderCase> Compressed() {
			List<ReaderCase> listCases=new List<ReaderCase>();
			//RLE Lossless, encoded by the test's own RLE encoder (SyntheticEncodings.cs).
			byte[] bytesGray=Gray8(10,6);
			listCases.Add(new ReaderCase("rle-mono2-8",MakeFileIn(Encapsulated8("MONOCHROME2",6,10,1,
				Encapsulated(null,RleFrame(bytesGray,6,10,1,1,0))),RleLossless)));
			ushort[] arrayWords16=Words(60,65535);
			arrayWords16[3]=arrayWords16[4]=arrayWords16[5]=arrayWords16[6]=1234;//a run
			SynImage synImageRle16=Mono16(6,10,16,15,arrayWords16);
			synImageRle16.PixelElement=Encapsulated(null,RleFrame(SyntheticDicom.Words(arrayWords16),6,10,1,2,0));
			listCases.Add(new ReaderCase("rle-mono2-16",MakeFileIn(synImageRle16,RleLossless)));
			ushort[] arrayWords12=Words(60,4095);
			SynImage synImageRle12=Mono16(6,10,12,11,arrayWords12,"MONOCHROME1");
			synImageRle12.PixelElement=Encapsulated(new uint[] { 0 },RleFrame(SyntheticDicom.Words(arrayWords12),6,10,1,2,0));
			listCases.Add(new ReaderCase("rle-mono1-12-bot",MakeFileIn(synImageRle12,RleLossless)));
			ushort[] arrayWordsSigned=SignedWords(60,-2048,2047);
			SynImage synImageRleSigned=Mono16(6,10,16,15,arrayWordsSigned,pixelRepresentation:1);
			synImageRleSigned.PixelElement=Encapsulated(null,RleFrame(SyntheticDicom.Words(arrayWordsSigned),6,10,1,2,0));
			listCases.Add(new ReaderCase("rle-signed-16",MakeFileIn(synImageRleSigned,RleLossless)));
			byte[] bytesRgb=Color8(60);
			listCases.Add(new ReaderCase("rle-rgb",MakeFileIn(Encapsulated8("RGB",6,10,3,Encapsulated(null,RleFrame(bytesRgb,6,10,3,1,0))),RleLossless)));
			SynImage synImageRlePlanar1=Encapsulated8("RGB",6,10,3,Encapsulated(null,RleFrame(bytesRgb,6,10,3,1,0)));
			synImageRlePlanar1.PlanarConfiguration=1;
			listCases.Add(new ReaderCase("rle-rgb-planar1-declared",MakeFileIn(synImageRlePlanar1,RleLossless)));
			listCases.Add(new ReaderCase("rle-ybr-full",MakeFileIn(Encapsulated8("YBR_FULL",6,10,3,Encapsulated(null,RleFrame(bytesRgb,6,10,3,1,0))),RleLossless)));
			byte[] bytesFrame0=RleFrame(SyntheticDicom.Words(arrayWords16),6,10,1,2,0);
			byte[] bytesFrame1=RleFrame(SyntheticDicom.Words(Words(60,1000)),6,10,1,2,0);
			SynImage synImageRleFrames=Mono16(6,10,16,15,arrayWords16);
			synImageRleFrames.NumberOfFrames="2";
			synImageRleFrames.PixelElement=Encapsulated(OffsetsOneFragmentPerFrame(bytesFrame0,bytesFrame1),bytesFrame0,bytesFrame1);
			listCases.Add(new ReaderCase("rle-multiframe-bot",MakeFileIn(synImageRleFrames,RleLossless)));
			SynImage synImageRleFramesNoBot=synImageRleFrames.Clone();
			synImageRleFramesNoBot.PixelElement=Encapsulated(null,bytesFrame0,bytesFrame1);
			listCases.Add(new ReaderCase("rle-multiframe-no-bot",MakeFileIn(synImageRleFramesNoBot,RleLossless)));
			//JPEG Baseline from the .NET Framework's encoder.
			byte[] bytesJpegGray=JpegGrayFixed();
			listCases.Add(new ReaderCase("jpeg-gray",MakeFileIn(Encapsulated8("MONOCHROME2",13,21,1,Encapsulated(null,bytesJpegGray)),JpegBaseline)));
			listCases.Add(new ReaderCase("jpeg-gray-mono1",MakeFileIn(Encapsulated8("MONOCHROME1",13,21,1,Encapsulated(null,bytesJpegGray)),JpegBaseline)));
			int half=bytesJpegGray.Length/2+1;//odd, so the first fragment is padded
			listCases.Add(new ReaderCase("jpeg-gray-two-fragments",MakeFileIn(Encapsulated8("MONOCHROME2",13,21,1,
				Encapsulated(null,bytesJpegGray.Take(half).ToArray(),bytesJpegGray.Skip(half).ToArray())),JpegBaseline)));
			SynImage synImageJpegFrames=Encapsulated8("MONOCHROME2",13,21,1,Encapsulated(null,bytesJpegGray,JpegGray(Gray8(21,13).Select(x => (byte)(255-x)).ToArray(),21,13,90)));
			synImageJpegFrames.NumberOfFrames="2";
			listCases.Add(new ReaderCase("jpeg-gray-multiframe-no-bot",MakeFileIn(synImageJpegFrames,JpegBaseline)));
			byte[] bytesJpegRgb=JpegRgbFixed();
			listCases.Add(new ReaderCase("jpeg-ybr-full-422",MakeFileIn(Encapsulated8("YBR_FULL_422",14,22,3,Encapsulated(null,bytesJpegRgb)),JpegBaseline)));
			listCases.Add(new ReaderCase("jpeg-ybr-full",MakeFileIn(Encapsulated8("YBR_FULL",14,22,3,Encapsulated(null,bytesJpegRgb)),JpegBaseline)));
			listCases.Add(new ReaderCase("jpeg-rgb-declared",MakeFileIn(Encapsulated8("RGB",14,22,3,Encapsulated(null,bytesJpegRgb)),JpegBaseline)));
			//YBR_PARTIAL_422, which older writers put on lossy JPEG color: the stream's own color transform decides, as for the other labels.
			listCases.Add(new ReaderCase("jpeg-ybr-partial-422",MakeFileIn(Encapsulated8("YBR_PARTIAL_422",14,22,3,Encapsulated(null,bytesJpegRgb)),JpegBaseline)));
			//Progressive JPEG (SOF2) under the JPEG Baseline transfer syntax, which PS3.5 A.4.1 reserves for Process 1: a few scans, as encoders write.
			listCases.Add(new ReaderCase("jpeg-progressive-gray",MakeFileIn(Encapsulated8("MONOCHROME2",16,24,1,
				Encapsulated(null,JpegProgressiveFlat(24,16,80,5))),JpegBaseline)));
			//Encapsulated Uncompressed (PS3.5 A.4.11): one native frame per fragment.
			listCases.Add(new ReaderCase("encapsulated-uncompressed",MakeFileIn(Encapsulated8("MONOCHROME2",6,10,1,Encapsulated(null,bytesGray)),
				EncapsulatedUncompressed)));
			//Compressed formats this version does not decode: the bytes are not a real stream of the format.
			byte[] bytesJunk=Encoding.ASCII.GetBytes("not a real codestream of the format");
			foreach(string transferSyntax in new[] { JpegExtended,JpegLosslessProcess14,JpegLossless,JpegLsLossless,JpegLsNearLossless,Jpeg2000Lossless,Jpeg2000,
				HtJpeg2000Lossless,HtJpeg2000,JpegXl,Mpeg2,DeflatedImageFrame })
			{
				listCases.Add(new ReaderCase("unsupported-"+transferSyntax,MakeFileIn(Encapsulated8("MONOCHROME2",6,10,1,Encapsulated(null,bytesJunk)),transferSyntax)));
			}
			return listCases;
		}
		#endregion The cases
	}
}
