// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace ImagingCapture.Save {
	///<summary>An 8-bit image decoded from a DIB: top-down rows with no padding, either one grey byte per pixel (IsGray) or three bytes per
	///pixel in B, G, R order.</summary>
	public sealed class DecodedImage {
		public int Width { get; set; }
		public int Height { get; set; }
		///<summary>Every pixel is a shade of grey (R=G=B): Pixels holds one byte per pixel. Otherwise three (B, G, R).</summary>
		public bool IsGray { get; set; }
		///<summary>Top-down rows, Width (IsGray) or 3*Width bytes each, no padding.</summary>
		public byte[] Pixels { get; set; }
		///<summary>From biXPelsPerMeter, or 0 when the DIB has none.</summary>
		public double XDpi { get; set; }
		public double YDpi { get; set; }
		///<summary>biBitCount of the DIB.</summary>
		public int BitCount { get; set; }

		public int Stride => IsGray ? Width : Width*3;
	}

	///<summary>Reads the packed DIBs a TWAIN transfer gives (AcquiredImage.Dib: BITMAPINFOHEADER, color table, rows): 1, 4 and 8 bits with a
	///palette, and 24 and 32 bits, uncompressed (BI_RGB, and BI_BITFIELDS with the standard 32-bit masks), bottom-up or top-down. A palette
	///image whose palette is all greys, or a 24 or 32-bit image whose pixels are all greys, decodes as grey. Other DIBs (compressed, 16 bits
	///per pixel, which is RGB555/565) are refused. Never throws for bad input.</summary>
	public static class DibDecoder {
		private const int BI_RGB=0;
		private const int BI_BITFIELDS=3;
		private const int SizeInfoHeader=40;

		///<summary>The image in dib, or null with error.</summary>
		public static DecodedImage Decode(byte[] dib,out string error) {
			error="";
			try {
				return DecodeChecked(dib,out error);
			}
			catch(Exception ex) {
				error="The image could not be read: "+ex.Message;
				return null;
			}
		}

		private static DecodedImage DecodeChecked(byte[] dib,out string error) {
			error="";
			if(dib==null || dib.Length<SizeInfoHeader) {
				error="The image has no bitmap header.";
				return null;
			}
			int biSize=BitConverter.ToInt32(dib,0);
			int width=BitConverter.ToInt32(dib,4);
			int heightSigned=BitConverter.ToInt32(dib,8);
			int bitCount=BitConverter.ToUInt16(dib,14);
			int compression=BitConverter.ToInt32(dib,16);
			int xPelsPerMeter=BitConverter.ToInt32(dib,24);
			int yPelsPerMeter=BitConverter.ToInt32(dib,28);
			int clrUsed=BitConverter.ToInt32(dib,32);
			if(biSize<SizeInfoHeader || biSize>dib.Length) {
				error="The image's bitmap header is not a BITMAPINFOHEADER.";
				return null;
			}
			if(width<=0 || heightSigned==0 || heightSigned==int.MinValue) {
				error="The image has no size.";
				return null;
			}
			bool isTopDown=heightSigned<0;
			int height=Math.Abs(heightSigned);
			if(bitCount!=1 && bitCount!=4 && bitCount!=8 && bitCount!=24 && bitCount!=32) {
				error="Images of "+bitCount+" bits per pixel are not supported.";
				return null;
			}
			int offsetColors=biSize;
			if(compression==BI_BITFIELDS) {
				if(bitCount!=32) {
					error="The image's compression is not supported.";
					return null;
				}
				if(biSize==SizeInfoHeader) {//the three masks follow a 40-byte header; larger headers hold them
					offsetColors+=12;
				}
				if(dib.Length<SizeInfoHeader+12 || BitConverter.ToUInt32(dib,40)!=0x00FF0000 || BitConverter.ToUInt32(dib,44)!=0x0000FF00
					|| BitConverter.ToUInt32(dib,48)!=0x000000FF)
				{
					error="The image's color masks are not supported.";
					return null;
				}
			}
			else if(compression!=BI_RGB) {
				error="Compressed images are not supported.";
				return null;
			}
			int countColors=clrUsed;
			if(bitCount<=8 && countColors==0) {
				countColors=1<<bitCount;
			}
			if(countColors<0 || countColors>(bitCount<=8 ? (1<<bitCount) : 65536)) {
				error="The image's color table is not valid.";
				return null;
			}
			long offsetPixels=(long)offsetColors+4L*countColors;
			long stride=((long)width*bitCount+31)/32*4;
			if(offsetPixels+stride*height>dib.Length) {
				error="The image's pixel data is shorter than its header says.";
				return null;
			}
			DecodedImage decodedImage=new DecodedImage();
			decodedImage.Width=width;
			decodedImage.Height=height;
			decodedImage.BitCount=bitCount;
			decodedImage.XDpi=xPelsPerMeter>0 ? xPelsPerMeter*0.0254 : 0;
			decodedImage.YDpi=yPelsPerMeter>0 ? yPelsPerMeter*0.0254 : 0;
			byte[] arrayBgr=new byte[(long)width*height*3];
			for(int y=0;y<height;y++) {
				int rowSource=isTopDown ? y : height-1-y;
				long offsetRow=offsetPixels+stride*rowSource;
				long offsetOut=(long)y*width*3;
				for(int x=0;x<width;x++) {
					byte b;
					byte g;
					byte r;
					if(bitCount<=8) {
						int index=GetIndex(dib,offsetRow,x,bitCount);
						if(index>=countColors) {
							b=g=r=0;
						}
						else {
							long offsetColor=offsetColors+4L*index;
							b=dib[offsetColor];
							g=dib[offsetColor+1];
							r=dib[offsetColor+2];
						}
					}
					else {
						long offsetPixel=offsetRow+(long)x*(bitCount/8);
						b=dib[offsetPixel];
						g=dib[offsetPixel+1];
						r=dib[offsetPixel+2];
					}
					arrayBgr[offsetOut+x*3]=b;
					arrayBgr[offsetOut+x*3+1]=g;
					arrayBgr[offsetOut+x*3+2]=r;
				}
			}
			bool isGray=true;
			for(long i=0;i<arrayBgr.LongLength;i+=3) {
				if(arrayBgr[i]!=arrayBgr[i+1] || arrayBgr[i]!=arrayBgr[i+2]) {
					isGray=false;
					break;
				}
			}
			decodedImage.IsGray=isGray;
			if(!isGray) {
				decodedImage.Pixels=arrayBgr;
				return decodedImage;
			}
			byte[] arrayGray=new byte[(long)width*height];
			for(long i=0;i<arrayGray.LongLength;i++) {
				arrayGray[i]=arrayBgr[i*3];
			}
			decodedImage.Pixels=arrayGray;
			return decodedImage;
		}

		private static int GetIndex(byte[] dib,long offsetRow,int x,int bitCount) {
			switch(bitCount) {
				case 8:
					return dib[offsetRow+x];
				case 4: {
					byte value=dib[offsetRow+x/2];
					return x%2==0 ? value>>4 : value&0x0F;
				}
				default: {//1
					byte value=dib[offsetRow+x/8];
					return (value>>(7-x%8))&1;
				}
			}
		}
	}
}
