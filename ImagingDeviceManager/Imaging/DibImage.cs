// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ImagingDeviceManager.TwainProtocol;

namespace ImagingDeviceManager.Imaging {
	///<summary>A device independent bitmap held in managed memory as a "packed DIB": a BITMAPINFOHEADER (or a larger V4/V5
	///header), an optional color table, then the pixel rows. This is the format TWAIN native transfers deliver.</summary>
	internal sealed class DibImage {
		public const int BitmapInfoHeaderSize=40;
		public const int BI_RGB=0;
		public const int BI_BITFIELDS=3;
		private const double MetersPerInch=0.0254;

		///<summary>The packed DIB bytes.</summary>
		public byte[] Data { get; }

		private DibImage(byte[] data) {
			Data=data;
		}

		#region Header fields
		public int HeaderSize => BitConverter.ToInt32(Data,0);
		public int Width => BitConverter.ToInt32(Data,4);
		///<summary>Height in rows. Always positive.</summary>
		public int Height => Math.Abs(BitConverter.ToInt32(Data,8));
		///<summary>True when the first row in memory is the top row of the image (negative biHeight).</summary>
		public bool IsTopDown => BitConverter.ToInt32(Data,8)<0;
		public int BitCount => BitConverter.ToInt16(Data,14);
		public int Compression => BitConverter.ToInt32(Data,16);
		public int XPelsPerMeter => BitConverter.ToInt32(Data,24);
		public int YPelsPerMeter => BitConverter.ToInt32(Data,28);
		public int ColorCount => GetColorCount(BitCount,BitConverter.ToInt32(Data,32));
		///<summary>Offset of the color table (or of the BI_BITFIELDS masks that follow a 40 byte header).</summary>
		public int ColorTableOffset => HeaderSize+(HeaderSize==BitmapInfoHeaderSize && Compression==BI_BITFIELDS ? 12 : 0);
		public int PixelOffset => ColorTableOffset+ColorCount*4;
		public int Stride => GetStride(Width,BitCount);
		#endregion Header fields

		///<summary>Horizontal resolution in dots per inch, rounded. 0 if the DIB does not say.</summary>
		public int XResolutionDpi => PelsPerMeterToDpi(XPelsPerMeter);

		///<summary>Vertical resolution in dots per inch, rounded. 0 if the DIB does not say.</summary>
		public int YResolutionDpi => PelsPerMeterToDpi(YPelsPerMeter);

		public static int GetStride(int width,int bitCount) {
			return (int)(((long)width*bitCount+31)/32*4);
		}

		public static int GetColorCount(int bitCount,int clrUsed) {
			if(clrUsed>0) {
				return clrUsed;
			}
			if(bitCount<=8) {
				return 1<<bitCount;
			}
			return 0;
		}

		public static int DpiToPelsPerMeter(double dpi) {
			if(dpi<=0) {
				return 0;
			}
			return (int)Math.Round(dpi/MetersPerInch);
		}

		public static int PelsPerMeterToDpi(int pelsPerMeter) {
			if(pelsPerMeter<=0) {
				return 0;
			}
			return (int)Math.Round(pelsPerMeter*MetersPerInch);
		}

		///<summary>Validates and wraps packed DIB bytes. Throws ImagingDeviceException if the bytes are not a usable DIB.</summary>
		public static DibImage FromPackedDib(byte[] data) {
			if(data==null || data.Length<BitmapInfoHeaderSize) {
				throw new ImagingDeviceException("The image is not a valid bitmap.",EZTwainErrorCode.EZTEC_BAD_DIB);
			}
			DibImage dib=new DibImage(data);
			int headerSize=dib.HeaderSize;
			if(headerSize<BitmapInfoHeaderSize || headerSize>data.Length) {
				throw new ImagingDeviceException("The image header is not valid.",EZTwainErrorCode.EZTEC_BAD_DIB);
			}
			if(dib.Width<=0 || dib.Height<=0) {
				throw new ImagingDeviceException("The image has no pixels.",EZTwainErrorCode.EZTEC_BAD_DIB);
			}
			switch(dib.BitCount) {
				case 1:
				case 4:
				case 8:
				case 16:
				case 24:
				case 32:
					break;
				default:
					throw new ImagingDeviceException("Unsupported bits per pixel: "+dib.BitCount,EZTwainErrorCode.EZTEC_BAD_DIB);
			}
			if(dib.Compression!=BI_RGB && dib.Compression!=BI_BITFIELDS) {
				throw new ImagingDeviceException("Compressed bitmaps are not supported.",EZTwainErrorCode.EZTEC_BAD_DIB);
			}
			if(dib.ColorCount>256) {
				throw new ImagingDeviceException("The image color table is not valid.",EZTwainErrorCode.EZTEC_BAD_DIB);
			}
			long requiredLength=(long)dib.PixelOffset+(long)dib.Stride*dib.Height;
			if(requiredLength>data.Length) {
				throw new ImagingDeviceException("The image data is truncated.",EZTwainErrorCode.EZTEC_BAD_DIB);
			}
			return dib;
		}

		///<summary>Computes the size of a packed DIB in unmanaged memory from its header. Returns 0 if the header is not valid.</summary>
		public static long GetPackedLength(IntPtr pDib) {
			int headerSize=Marshal.ReadInt32(pDib,0);
			if(headerSize<BitmapInfoHeaderSize || headerSize>4096) {
				return 0;
			}
			int width=Marshal.ReadInt32(pDib,4);
			int height=Math.Abs(Marshal.ReadInt32(pDib,8));
			int bitCount=Marshal.ReadInt16(pDib,14);
			int compression=Marshal.ReadInt32(pDib,16);
			int clrUsed=Marshal.ReadInt32(pDib,32);
			if(width<=0 || height<=0 || bitCount<=0 || bitCount>64 || clrUsed<0 || clrUsed>256) {
				return 0;
			}
			int colorTableOffset=headerSize+(headerSize==BitmapInfoHeaderSize && compression==BI_BITFIELDS ? 12 : 0);
			return colorTableOffset+GetColorCount(bitCount,clrUsed)*4L+(long)GetStride(width,bitCount)*height;
		}

		///<summary>Copies a packed DIB out of unmanaged memory. availableLength is the size of the memory block, or 0 if unknown.</summary>
		public static DibImage FromUnmanaged(IntPtr pDib,long availableLength) {
			if(pDib==IntPtr.Zero) {
				throw new ImagingDeviceException("The scanner returned no image.",EZTwainErrorCode.EZTEC_BAD_DIB);
			}
			long length=GetPackedLength(pDib);
			if(length<=0 || length>int.MaxValue) {
				throw new ImagingDeviceException("The scanner returned an image that is not a valid bitmap.",EZTwainErrorCode.EZTEC_BAD_DIB);
			}
			if(availableLength>0 && length>availableLength) {
				throw new ImagingDeviceException("The scanner returned a truncated image.",EZTwainErrorCode.EZTEC_BAD_DIB);
			}
			byte[] data=new byte[length];
			Marshal.Copy(pDib,data,0,(int)length);
			return FromPackedDib(data);
		}

		///<summary>Builds a bottom-up packed DIB with a 40 byte header. palette holds B,G,R,0 quads, or is null.
		///pixels holds the rows bottom-up with DIB stride.</summary>
		public static DibImage Create(int width,int height,int bitCount,byte[] palette,byte[] pixels,int xPelsPerMeter,int yPelsPerMeter) {
			int colorCount=palette==null ? 0 : palette.Length/4;
			int stride=GetStride(width,bitCount);
			byte[] data=new byte[BitmapInfoHeaderSize+colorCount*4+stride*height];
			WriteInt32(data,0,BitmapInfoHeaderSize);
			WriteInt32(data,4,width);
			WriteInt32(data,8,height);
			WriteInt16(data,12,1);//planes
			WriteInt16(data,14,(short)bitCount);
			WriteInt32(data,16,BI_RGB);
			WriteInt32(data,20,stride*height);
			WriteInt32(data,24,xPelsPerMeter);
			WriteInt32(data,28,yPelsPerMeter);
			WriteInt32(data,32,colorCount);
			WriteInt32(data,36,0);
			if(colorCount>0) {
				Buffer.BlockCopy(palette,0,data,BitmapInfoHeaderSize,colorCount*4);
			}
			if(pixels!=null) {
				Buffer.BlockCopy(pixels,0,data,BitmapInfoHeaderSize+colorCount*4,Math.Min(pixels.Length,stride*height));
			}
			return FromPackedDib(data);
		}

		///<summary>Sets the resolution in the header when the source left it blank.</summary>
		public void SetResolutionIfMissing(double xDpi,double yDpi) {
			if(XPelsPerMeter<=0 && xDpi>0) {
				WriteInt32(Data,24,DpiToPelsPerMeter(xDpi));
			}
			if(YPelsPerMeter<=0 && yDpi>0) {
				WriteInt32(Data,28,DpiToPelsPerMeter(yDpi));
			}
		}

		///<summary>Some sources deliver 16 bit grayscale in a native transfer, which Windows would misread as 16 bit color.
		///Converts it to an 8 bit grayscale DIB by scaling the samples into 0 to 255.</summary>
		public DibImage ConvertGray16ToGray8() {
			if(BitCount!=16) {
				return this;
			}
			int width=Width;
			int height=Height;
			int srcStride=Stride;
			int pixelOffset=PixelOffset;
			int maxSample=0;
			for(int row=0;row<height;row++) {
				int rowStart=pixelOffset+row*srcStride;
				for(int x=0;x<width;x++) {
					int sample=Data[rowStart+x*2]|(Data[rowStart+x*2+1]<<8);
					if(sample>maxSample) {
						maxSample=sample;
					}
				}
			}
			int shift=GetGray16Shift(maxSample);
			int dstStride=GetStride(width,8);
			byte[] pixels=new byte[dstStride*height];
			for(int row=0;row<height;row++) {
				//Keep the DIB orientation: produce bottom-up rows.
				int srcRow=IsTopDown ? height-1-row : row;
				int srcStart=pixelOffset+srcRow*srcStride;
				int dstStart=row*dstStride;
				for(int x=0;x<width;x++) {
					int sample=Data[srcStart+x*2]|(Data[srcStart+x*2+1]<<8);
					pixels[dstStart+x]=(byte)Math.Min(255,sample>>shift);
				}
			}
			return Create(width,height,8,CreateGrayPalette(256,false),pixels,XPelsPerMeter,YPelsPerMeter);
		}

		///<summary>Right shift that maps 16 bit samples whose largest value is maxSample into 8 bits. Many sensors put 10 to 14
		///significant bits into a 16 bit sample, so the shift follows the data instead of always taking the high byte.</summary>
		public static int GetGray16Shift(int maxSample) {
			int bits=0;
			while(maxSample>0) {
				bits++;
				maxSample>>=1;
			}
			return Math.Max(0,bits-8);
		}

		///<summary>B,G,R,0 quads for a gray ramp. inverted gives white for 0 ("vanilla" pixel flavor).</summary>
		public static byte[] CreateGrayPalette(int colorCount,bool inverted) {
			byte[] palette=new byte[colorCount*4];
			for(int i=0;i<colorCount;i++) {
				int level=colorCount==1 ? 0 : i*255/(colorCount-1);
				if(inverted) {
					level=255-level;
				}
				palette[i*4]=(byte)level;
				palette[i*4+1]=(byte)level;
				palette[i*4+2]=(byte)level;
			}
			return palette;
		}

		///<summary>Creates an independent GDI+ bitmap with the same pixels, palette and resolution.</summary>
		public Bitmap ToBitmap() {
			PixelFormat pixelFormat;
			switch(BitCount) {
				case 1:
					pixelFormat=PixelFormat.Format1bppIndexed;
					break;
				case 4:
					pixelFormat=PixelFormat.Format4bppIndexed;
					break;
				case 8:
					pixelFormat=PixelFormat.Format8bppIndexed;
					break;
				case 24:
					pixelFormat=PixelFormat.Format24bppRgb;
					break;
				case 32:
					pixelFormat=(Compression==BI_RGB ? PixelFormat.Format32bppRgb : PixelFormat.Undefined);
					break;
				case 16:
					pixelFormat=(Compression==BI_RGB ? PixelFormat.Format16bppRgb555 : PixelFormat.Undefined);
					break;
				default:
					pixelFormat=PixelFormat.Undefined;
					break;
			}
			if(pixelFormat==PixelFormat.Undefined) {
				return ToBitmapThroughGdi();
			}
			int width=Width;
			int height=Height;
			Bitmap bitmap=new Bitmap(width,height,pixelFormat);
			try {
				if(BitCount<=8) {
					ColorPalette colorPalette=bitmap.Palette;
					int colorCount=Math.Min(ColorCount,colorPalette.Entries.Length);
					int offset=ColorTableOffset;
					for(int i=0;i<colorPalette.Entries.Length;i++) {
						if(i<colorCount) {
							colorPalette.Entries[i]=Color.FromArgb(255,Data[offset+i*4+2],Data[offset+i*4+1],Data[offset+i*4]);
						}
						else {
							colorPalette.Entries[i]=Color.Black;
						}
					}
					bitmap.Palette=colorPalette;
				}
				BitmapData bitmapData=bitmap.LockBits(new Rectangle(0,0,width,height),ImageLockMode.WriteOnly,pixelFormat);
				try {
					int stride=Stride;
					int bytesPerRow=Math.Min(stride,Math.Abs(bitmapData.Stride));
					int pixelOffset=PixelOffset;
					for(int y=0;y<height;y++) {
						int srcRow=IsTopDown ? y : height-1-y;
						IntPtr pDest=new IntPtr(bitmapData.Scan0.ToInt64()+(long)y*bitmapData.Stride);
						Marshal.Copy(Data,pixelOffset+srcRow*stride,pDest,bytesPerRow);
					}
				}
				finally {
					bitmap.UnlockBits(bitmapData);
				}
				SetBitmapResolution(bitmap);
				return bitmap;
			}
			catch {
				bitmap.Dispose();
				throw;
			}
		}

		private Bitmap ToBitmapThroughGdi() {
			IntPtr hBitmap=ToHBitmap();
			try {
				Bitmap bitmap=Image.FromHbitmap(hBitmap);
				SetBitmapResolution(bitmap);
				return bitmap;
			}
			finally {
				NativeMethods.DeleteObject(hBitmap);
			}
		}

		private void SetBitmapResolution(Bitmap bitmap) {
			int xDpi=XResolutionDpi;
			int yDpi=YResolutionDpi;
			if(xDpi>0 && yDpi>0) {
				bitmap.SetResolution(xDpi,yDpi);
			}
		}

		///<summary>Creates a GDI DIB section (HBITMAP) with the same pixels. The caller owns it and must call DeleteObject.</summary>
		public IntPtr ToHBitmap() {
			GCHandle gcHandle=GCHandle.Alloc(Data,GCHandleType.Pinned);
			try {
				IntPtr pBitmapInfo=gcHandle.AddrOfPinnedObject();
				IntPtr pBits;
				IntPtr hBitmap=NativeMethods.CreateDIBSection(IntPtr.Zero,pBitmapInfo,NativeMethods.DIB_RGB_COLORS,out pBits,IntPtr.Zero,0);
				if(hBitmap==IntPtr.Zero || pBits==IntPtr.Zero) {
					if(hBitmap!=IntPtr.Zero) {
						NativeMethods.DeleteObject(hBitmap);
					}
					throw new ImagingDeviceException("Could not create a bitmap for the scanned image (Windows error "
						+Marshal.GetLastWin32Error()+").",EZTwainErrorCode.EZTEC_MEMORY);
				}
				Marshal.Copy(Data,PixelOffset,pBits,Stride*Height);
				return hBitmap;
			}
			finally {
				gcHandle.Free();
			}
		}

		private static void WriteInt32(byte[] data,int offset,int value) {
			data[offset]=(byte)value;
			data[offset+1]=(byte)(value>>8);
			data[offset+2]=(byte)(value>>16);
			data[offset+3]=(byte)(value>>24);
		}

		private static void WriteInt16(byte[] data,int offset,short value) {
			data[offset]=(byte)value;
			data[offset+1]=(byte)(value>>8);
		}
	}
}
