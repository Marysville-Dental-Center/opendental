// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ImagingDeviceManager.TwainProtocol;

namespace ImagingDeviceManager.Imaging {
	///<summary>Assembles the strips of a TWAIN memory transfer into a DIB. TWAIN memory transfers deliver uncompressed rows
	///top to bottom, RGB in R,G,B order, and 16 bit samples in Intel byte order. DIBs are bottom-up and BGR.</summary>
	internal sealed class DibBuilder {
		private readonly int _width;
		private readonly int _heightExpected;
		private readonly int _bitsPerPixel;
		private readonly int _pixelType;
		private readonly byte[] _palette;
		private readonly bool _isVanilla;
		private readonly double _xDpi;
		private readonly double _yDpi;
		private readonly int _srcRowBytes;
		///<summary>Source rows, top to bottom. A row stays null until a strip covers it.</summary>
		private readonly List<byte[]> _listRows=new List<byte[]>();

		///<summary>width and height come from TW_IMAGEINFO. height is -1 (or 0) when the source does not know it in advance,
		///as with some document feeders. palette holds B,G,R,0 quads for TWPT_PALETTE images and is ignored otherwise.
		///isVanilla is true when ICAP_PIXELFLAVOR says 0 means white.</summary>
		public DibBuilder(int width,int height,int bitsPerPixel,int pixelType,byte[] palette,bool isVanilla,double xDpi,double yDpi) {
			if(width<=0) {
				throw new ImagingDeviceException("The scanner reported an image width of "+width+".",EZTwainErrorCode.EZTEC_BAD_MEMXFER);
			}
			if(!IsSupported(bitsPerPixel,pixelType)) {
				throw new ImagingDeviceException("Memory transfers of "+bitsPerPixel+" bit images of pixel type "+pixelType
					+" are not supported.",EZTwainErrorCode.EZTEC_BAD_MEMXFER);
			}
			_width=width;
			_heightExpected=height;
			_bitsPerPixel=bitsPerPixel;
			_pixelType=pixelType;
			_palette=palette;
			_isVanilla=isVanilla;
			_xDpi=xDpi;
			_yDpi=yDpi;
			_srcRowBytes=(int)(((long)width*bitsPerPixel+7)/8);
			if(height>0) {
				_listRows.Capacity=height;
			}
		}

		public static bool IsSupported(int bitsPerPixel,int pixelType) {
			switch(pixelType) {
				case Tw.TWPT_BW:
					return bitsPerPixel==1;
				case Tw.TWPT_GRAY:
					return bitsPerPixel==4 || bitsPerPixel==8 || bitsPerPixel==16;
				case Tw.TWPT_RGB:
					return bitsPerPixel==24 || bitsPerPixel==48;
				case Tw.TWPT_PALETTE:
					return bitsPerPixel==4 || bitsPerPixel==8;
				default:
					return false;
			}
		}

		///<summary>Number of rows received so far (the highest row index written, plus one).</summary>
		public int RowsReceived => _listRows.Count;

		///<summary>Copies one strip out of the transfer buffer. bufferLength is the size of the buffer at pBuffer.</summary>
		public void AddStrip(IntPtr pBuffer,int bufferLength,TW_IMAGEMEMXFER strip) {
			if(strip.Compression!=Tw.TWCP_NONE && strip.Compression!=Tw.TWON_DONTCARE16) {
				throw new ImagingDeviceException("The scanner sent compressed data in a memory transfer.",EZTwainErrorCode.EZTEC_BAD_MEMXFER);
			}
			if(strip.Rows==0) {
				return;
			}
			long bytesPerRow=strip.BytesPerRow;
			long columns=Math.Min(strip.Columns,(uint)_width);
			int xByteOffset=(int)(((long)strip.XOffset*_bitsPerPixel)/8);
			int bytesToCopy=(int)Math.Min((columns*_bitsPerPixel+7)/8,_srcRowBytes-(long)xByteOffset);
			if(bytesToCopy<=0) {
				return;
			}
			if(bytesPerRow<bytesToCopy) {
				bytesPerRow=bytesToCopy;
			}
			for(long r=0;r<strip.Rows;r++) {
				long srcOffset=r*bytesPerRow;
				if(srcOffset+bytesToCopy>bufferLength) {
					break;//The source claimed more rows than fit in the buffer.
				}
				long y=strip.YOffset+r;
				if(_heightExpected>0 && y>=_heightExpected) {
					break;
				}
				if(y>int.MaxValue-1) {
					break;
				}
				byte[] row=GetRow((int)y);
				Marshal.Copy(new IntPtr(pBuffer.ToInt64()+srcOffset),row,xByteOffset,bytesToCopy);
			}
		}

		///<summary>Adds one row of source pixels (for tests and for callers that already have managed rows).</summary>
		public void AddRow(int y,byte[] source) {
			byte[] row=GetRow(y);
			Buffer.BlockCopy(source,0,row,0,Math.Min(source.Length,row.Length));
		}

		private byte[] GetRow(int y) {
			while(_listRows.Count<=y) {
				_listRows.Add(null);
			}
			if(_listRows[y]==null) {
				_listRows[y]=new byte[_srcRowBytes];
			}
			return _listRows[y];
		}

		///<summary>Creates the DIB. Throws if no rows were received.</summary>
		public DibImage Build() {
			int height=_heightExpected>0 ? _heightExpected : _listRows.Count;
			if(height<=0 || _listRows.Count==0) {
				throw new ImagingDeviceException("The scanner did not send any image data.",EZTwainErrorCode.EZTEC_BAD_MEMXFER);
			}
			int dstBitCount;
			byte[] palette;
			switch(_pixelType) {
				case Tw.TWPT_BW:
					dstBitCount=1;
					palette=DibImage.CreateGrayPalette(2,_isVanilla);
					break;
				case Tw.TWPT_GRAY:
					dstBitCount=_bitsPerPixel==16 ? 8 : _bitsPerPixel;
					palette=DibImage.CreateGrayPalette(1<<dstBitCount,_isVanilla);
					break;
				case Tw.TWPT_PALETTE:
					dstBitCount=_bitsPerPixel;
					palette=_palette ?? DibImage.CreateGrayPalette(1<<dstBitCount,false);
					break;
				default://RGB
					dstBitCount=24;
					palette=null;
					break;
			}
			int shift16=0;
			if(_bitsPerPixel==16) {
				shift16=DibImage.GetGray16Shift(GetMaxSample16());
			}
			int dstStride=DibImage.GetStride(_width,dstBitCount);
			byte[] pixels=new byte[(long)dstStride*height];
			for(int y=0;y<height;y++) {
				byte[] src=y<_listRows.Count ? _listRows[y] : null;
				if(src==null) {
					continue;
				}
				int dstStart=(height-1-y)*dstStride;//bottom-up
				ConvertRow(src,pixels,dstStart,shift16);
			}
			return DibImage.Create(_width,height,dstBitCount,palette,pixels,DibImage.DpiToPelsPerMeter(_xDpi),DibImage.DpiToPelsPerMeter(_yDpi));
		}

		private int GetMaxSample16() {
			int max=0;
			foreach(byte[] row in _listRows) {
				if(row==null) {
					continue;
				}
				for(int x=0;x+1<row.Length;x+=2) {
					int sample=row[x]|(row[x+1]<<8);
					if(sample>max) {
						max=sample;
					}
				}
			}
			return max;
		}

		private void ConvertRow(byte[] src,byte[] dst,int dstStart,int shift16) {
			if(_pixelType==Tw.TWPT_RGB) {
				int bytesPerSample=_bitsPerPixel==48 ? 2 : 1;
				for(int x=0;x<_width;x++) {
					int srcPixel=x*3*bytesPerSample;
					int dstPixel=dstStart+x*3;
					//Take the high byte of 16 bit samples (Intel byte order: high byte second).
					byte r=src[srcPixel+bytesPerSample-1];
					byte g=src[srcPixel+bytesPerSample*2-1];
					byte b=src[srcPixel+bytesPerSample*3-1];
					dst[dstPixel]=b;
					dst[dstPixel+1]=g;
					dst[dstPixel+2]=r;
				}
				return;
			}
			if(_bitsPerPixel==16) {
				for(int x=0;x<_width;x++) {
					int sample=src[x*2]|(src[x*2+1]<<8);
					dst[dstStart+x]=(byte)Math.Min(255,sample>>shift16);
				}
				return;
			}
			//1, 4 and 8 bit images have the same packing in TWAIN and in DIBs (most significant bits first).
			Buffer.BlockCopy(src,0,dst,dstStart,_srcRowBytes);
		}
	}
}
