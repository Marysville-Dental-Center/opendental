// A small PNG encoder for the chart baselines: the bytes depend only on the pixels, and the files are smaller than GDI+'s.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using System.IO;
using System.IO.Compression;

namespace ToothChartImageTests {
	///<summary>Writes a bitmap as an 8-bit PNG: RGB when every alpha is 255, otherwise RGBA. Each row gets the PNG filter (None, Sub, Up, Average or
	///Paeth) with the smallest sum of absolute filtered bytes, and the image data is one zlib stream from the framework's DeflateStream. No other
	///chunks (no gamma, colour space or DPI), so the same pixels always give the same file. GDI+'s encoder, by contrast, can change with Windows and
	///makes files two to three times as large, which matters for several hundred baselines. A baseline's PixelFormat and alpha are recorded in its
	///.txt; the PNG holds the pixels only.</summary>
	public static class PngWriter {
		private static readonly byte[] _arraySignature={ 137,80,78,71,13,10,26,10 };
		private static uint[] _arrayCrcTable;

		public static void Save(Bitmap bitmap,string path) {
			File.WriteAllBytes(path,Encode(bitmap));
		}

		public static byte[] Encode(Bitmap bitmap) {
			int width=bitmap.Width;
			int height=bitmap.Height;
			byte[] arrayBgra=ChartImageCompare.GetPixelsArgb(bitmap);
			bool hasAlpha=false;
			for(int i=3;i<arrayBgra.Length;i+=4) {
				if(arrayBgra[i]!=255) {
					hasAlpha=true;
					break;
				}
			}
			int bytesPerPixel=hasAlpha?4:3;
			int stride=width*bytesPerPixel;
			byte[] arrayFiltered=new byte[height*(stride+1)];
			byte[] arrayRowPrevious=new byte[stride];
			byte[] arrayRow=new byte[stride];
			byte[][] arrayCandidates=new byte[5][];
			for(int f=0;f<5;f++) {
				arrayCandidates[f]=new byte[stride];
			}
			for(int y=0;y<height;y++) {
				for(int x=0;x<width;x++) {
					int indexSource=(y*width+x)*4;
					int indexRow=x*bytesPerPixel;
					arrayRow[indexRow]=arrayBgra[indexSource+2];//R
					arrayRow[indexRow+1]=arrayBgra[indexSource+1];//G
					arrayRow[indexRow+2]=arrayBgra[indexSource];//B
					if(hasAlpha) {
						arrayRow[indexRow+3]=arrayBgra[indexSource+3];
					}
				}
				int filterBest=0;
				long sumBest=long.MaxValue;
				for(int f=0;f<5;f++) {
					long sum=Filter(f,arrayRow,arrayRowPrevious,bytesPerPixel,arrayCandidates[f]);
					if(sum<sumBest) {
						sumBest=sum;
						filterBest=f;
					}
				}
				int indexOut=y*(stride+1);
				arrayFiltered[indexOut]=(byte)filterBest;
				Buffer.BlockCopy(arrayCandidates[filterBest],0,arrayFiltered,indexOut+1,stride);
				byte[] arraySwap=arrayRowPrevious;
				arrayRowPrevious=arrayRow;
				arrayRow=arraySwap;
			}
			using MemoryStream memoryStream=new MemoryStream();
			memoryStream.Write(_arraySignature,0,_arraySignature.Length);
			byte[] arrayHeader=new byte[13];
			WriteUInt32(arrayHeader,0,(uint)width);
			WriteUInt32(arrayHeader,4,(uint)height);
			arrayHeader[8]=8;//bit depth
			arrayHeader[9]=(byte)(hasAlpha?6:2);//colour type: RGBA or RGB
			arrayHeader[10]=0;//deflate
			arrayHeader[11]=0;//adaptive filtering
			arrayHeader[12]=0;//no interlace
			WriteChunk(memoryStream,"IHDR",arrayHeader);
			WriteChunk(memoryStream,"IDAT",Zlib(arrayFiltered));
			WriteChunk(memoryStream,"IEND",new byte[0]);
			return memoryStream.ToArray();
		}

		///<summary>Filters one row with PNG filter type f into arrayOut and returns the sum of the filtered bytes taken as signed values.</summary>
		private static long Filter(int f,byte[] arrayRow,byte[] arrayRowPrevious,int bytesPerPixel,byte[] arrayOut) {
			long sum=0;
			for(int i=0;i<arrayRow.Length;i++) {
				int left=i>=bytesPerPixel?arrayRow[i-bytesPerPixel]:0;
				int up=arrayRowPrevious[i];
				int upLeft=i>=bytesPerPixel?arrayRowPrevious[i-bytesPerPixel]:0;
				int predictor;
				switch(f) {
					case 1:
						predictor=left;
						break;
					case 2:
						predictor=up;
						break;
					case 3:
						predictor=(left+up)/2;
						break;
					case 4:
						predictor=Paeth(left,up,upLeft);
						break;
					default:
						predictor=0;
						break;
				}
				byte value=(byte)(arrayRow[i]-predictor);
				arrayOut[i]=value;
				sum+=Math.Abs((int)(sbyte)value);
			}
			return sum;
		}

		private static int Paeth(int a,int b,int c) {
			int p=a+b-c;
			int pa=Math.Abs(p-a);
			int pb=Math.Abs(p-b);
			int pc=Math.Abs(p-c);
			if(pa<=pb && pa<=pc) {
				return a;
			}
			if(pb<=pc) {
				return b;
			}
			return c;
		}

		///<summary>A zlib stream (RFC 1950): the two header bytes, the deflate data and the Adler-32 of the data.</summary>
		private static byte[] Zlib(byte[] arrayData) {
			using MemoryStream memoryStream=new MemoryStream();
			memoryStream.WriteByte(0x78);
			memoryStream.WriteByte(0xDA);//highest compression hint; (0x78*256+0xDA)%31==0
			using(DeflateStream deflateStream=new DeflateStream(memoryStream,CompressionLevel.Optimal,true)) {
				deflateStream.Write(arrayData,0,arrayData.Length);
			}
			uint a=1;
			uint b=0;
			for(int i=0;i<arrayData.Length;i++) {
				a=(a+arrayData[i])%65521;
				b=(b+a)%65521;
			}
			byte[] arrayAdler=new byte[4];
			WriteUInt32(arrayAdler,0,(b<<16)|a);
			memoryStream.Write(arrayAdler,0,4);
			return memoryStream.ToArray();
		}

		private static void WriteChunk(Stream stream,string type,byte[] arrayData) {
			byte[] arrayLength=new byte[4];
			WriteUInt32(arrayLength,0,(uint)arrayData.Length);
			stream.Write(arrayLength,0,4);
			byte[] arrayType=System.Text.Encoding.ASCII.GetBytes(type);
			stream.Write(arrayType,0,4);
			stream.Write(arrayData,0,arrayData.Length);
			uint crc=Crc32(arrayType,0xFFFFFFFF);
			crc=Crc32(arrayData,crc)^0xFFFFFFFF;
			byte[] arrayCrc=new byte[4];
			WriteUInt32(arrayCrc,0,crc);
			stream.Write(arrayCrc,0,4);
		}

		private static uint Crc32(byte[] arrayData,uint crc) {
			if(_arrayCrcTable==null) {
				uint[] arrayTable=new uint[256];
				for(uint n=0;n<256;n++) {
					uint c=n;
					for(int k=0;k<8;k++) {
						c=(c&1)!=0?0xEDB88320^(c>>1):c>>1;
					}
					arrayTable[n]=c;
				}
				_arrayCrcTable=arrayTable;
			}
			for(int i=0;i<arrayData.Length;i++) {
				crc=_arrayCrcTable[(crc^arrayData[i])&0xFF]^(crc>>8);
			}
			return crc;
		}

		private static void WriteUInt32(byte[] array,int index,uint value) {
			array[index]=(byte)(value>>24);
			array[index+1]=(byte)(value>>16);
			array[index+2]=(byte)(value>>8);
			array[index+3]=(byte)value;
		}
	}
}
