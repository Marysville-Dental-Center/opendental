// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ImagingDeviceManager.TwainProtocol;

namespace ImagingDeviceManager.Imaging {
	///<summary>Assembles the strips of a 16-bit grayscale memory transfer into raw samples (design 7.1 CAP-3): chunky, top-down rows, each
	///sample 2 bytes, right-justified, little-endian (TWAIN 2.4 p.4-30; twain-contract 1.1 section 6.2), or big-endian when the device
	///profile says so. Rows may be padded (BytesPerRow larger than 2 x Columns) and strips may be tiles (XOffset, Columns): both are honoured.
	///Values are kept exactly as delivered: no shift, no window, no inversion.</summary>
	internal sealed class Gray16Builder {
		private readonly int _width;
		private readonly int _heightExpected;
		private readonly bool _isBigEndian;
		///<summary>Rows top to bottom; a row stays null until a strip covers it.</summary>
		private readonly List<ushort[]> _listRows=new List<ushort[]>();

		///<summary>width and height come from TW_IMAGEINFO; height is -1 (or 0) when the source does not know it in advance.</summary>
		public Gray16Builder(int width,int height,bool isBigEndian) {
			if(width<=0) {
				throw new ImagingDeviceException("The device reported an image width of "+width+".",EZTwainErrorCode.EZTEC_BAD_MEMXFER);
			}
			_width=width;
			_heightExpected=height;
			_isBigEndian=isBigEndian;
			if(height>0) {
				_listRows.Capacity=height;
			}
		}

		///<summary>The number of rows received so far (the highest row index written, plus one).</summary>
		public int RowsReceived => _listRows.Count;

		///<summary>Copies one strip out of the transfer buffer. bufferLength is the size of the buffer at pBuffer.</summary>
		public void AddStrip(IntPtr pBuffer,int bufferLength,TW_IMAGEMEMXFER strip) {
			if(strip.Compression!=Tw.TWCP_NONE && strip.Compression!=Tw.TWON_DONTCARE16) {
				throw new ImagingDeviceException("The device sent compressed data in a memory transfer.",EZTwainErrorCode.EZTEC_BAD_MEMXFER);
			}
			if(strip.Rows==0 || strip.Rows==Tw.TWON_DONTCARE32) {
				return;
			}
			long xOffset=strip.XOffset==Tw.TWON_DONTCARE32 ? 0 : strip.XOffset;
			long yOffset=strip.YOffset==Tw.TWON_DONTCARE32 ? 0 : strip.YOffset;
			if(xOffset>=_width) {
				return;
			}
			long columns=strip.Columns==Tw.TWON_DONTCARE32 ? _width-xOffset : Math.Min(strip.Columns,(uint)(_width-xOffset));
			if(columns<=0) {
				return;
			}
			long bytesToCopy=columns*2;
			long bytesPerRow=strip.BytesPerRow==Tw.TWON_DONTCARE32 ? bytesToCopy : Math.Max((long)strip.BytesPerRow,bytesToCopy);
			byte[] bytesRow=new byte[bytesToCopy];
			for(long r=0;r<strip.Rows;r++) {
				long srcOffset=r*bytesPerRow;
				if(srcOffset+bytesToCopy>bufferLength) {
					break;//The source claimed more rows than fit in the buffer.
				}
				long y=yOffset+r;
				if((_heightExpected>0 && y>=_heightExpected) || y>int.MaxValue-1) {
					break;
				}
				Marshal.Copy(new IntPtr(pBuffer.ToInt64()+srcOffset),bytesRow,0,(int)bytesToCopy);
				ushort[] row=GetRow((int)y);
				for(long x=0;x<columns;x++) {
					byte b0=bytesRow[x*2];
					byte b1=bytesRow[x*2+1];
					row[xOffset+x]=_isBigEndian ? (ushort)((b0<<8)|b1) : (ushort)(b0|(b1<<8));
				}
			}
		}

		private ushort[] GetRow(int y) {
			while(_listRows.Count<=y) {
				_listRows.Add(null);
			}
			if(_listRows[y]==null) {
				_listRows[y]=new ushort[_width];
			}
			return _listRows[y];
		}

		///<summary>The image height: the expected one, or the rows received when it was unknown.</summary>
		public int Height => _heightExpected>0 ? _heightExpected : _listRows.Count;

		///<summary>Width*Height samples, rows top to bottom; rows no strip covered are 0. Throws if no rows were received.</summary>
		public ushort[] Build() {
			int height=Height;
			if(height<=0 || _listRows.Count==0) {
				throw new ImagingDeviceException("The device did not send any image data.",EZTwainErrorCode.EZTEC_BAD_MEMXFER);
			}
			ushort[] samples=new ushort[(long)_width*height];
			for(int y=0;y<height && y<_listRows.Count;y++) {
				if(_listRows[y]!=null) {
					Array.Copy(_listRows[y],0,samples,(long)y*_width,_width);
				}
			}
			return samples;
		}

		///<summary>The raw samples of a 16-bit grayscale DIB, rows top to bottom, without the shift the scan path applies. Only for a source
		///that delivers one in a native transfer although the capture path never asks for it (a 16bpp DIB is RGB555/565 by definition).</summary>
		public static ushort[] FromGray16Dib(DibImage dib) {
			int width=dib.Width;
			int height=dib.Height;
			int stride=dib.Stride;
			int pixelOffset=dib.PixelOffset;
			ushort[] samples=new ushort[(long)width*height];
			for(int y=0;y<height;y++) {
				int srcRow=dib.IsTopDown ? y : height-1-y;
				int srcStart=pixelOffset+srcRow*stride;
				for(int x=0;x<width;x++) {
					samples[(long)y*width+x]=(ushort)(dib.Data[srcStart+x*2]|(dib.Data[srcStart+x*2+1]<<8));
				}
			}
			return samples;
		}
	}
}
