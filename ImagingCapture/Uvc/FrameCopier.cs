// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Runtime.InteropServices;
using ImagingCapture.Uvc.Interop;

namespace ImagingCapture.Uvc {
	///<summary>How to read the pixels of one sample: an uncompressed 24 or 32-bit RGB DIB as described by a VIDEOINFOHEADER.</summary>
	internal struct FrameGeometry {
		///<summary>Visible width in pixels.</summary>
		public int Width;
		///<summary>Visible height in pixels.</summary>
		public int Height;
		///<summary>24 or 32.</summary>
		public int BitCount;
		///<summary>Bytes per row in the sample, including padding.</summary>
		public int SourceStride;
		///<summary>Rows in the sample.</summary>
		public int SourceRows;
		///<summary>True when the first row in memory is the bottom of the image (a positive biHeight).</summary>
		public bool IsBottomUp;
		///<summary>Top-left corner of the visible area, in image coordinates (x to the right, y down).</summary>
		public int OffsetX;
		public int OffsetY;

		///<summary>The smallest sample, in bytes, that holds every row.</summary>
		public long RequiredLength {
			get {
				return (long)SourceStride*SourceRows;
			}
		}

		///<summary>Bytes per row of a DIB: width*bitCount bits rounded up to a multiple of 4 bytes.</summary>
		public static int GetDibStride(int width,int bitCount) {
			return checked(((width*bitCount+31)/32)*4);
		}

		///<summary>Reads the geometry from the format the Sample Grabber connected with. The visible area is rcTarget when it is
		///set and lies inside the bitmap, otherwise the whole bitmap. Fails for anything but uncompressed 24 or 32-bit RGB.</summary>
		public static bool TryCreate(VIDEOINFOHEADER videoInfoHeader,out FrameGeometry geometry,out string error) {
			geometry=new FrameGeometry();
			error="";
			BITMAPINFOHEADER bmi=videoInfoHeader.bmiHeader;
			if(bmi.biBitCount!=24 && bmi.biBitCount!=32) {
				error="Unsupported bit count "+bmi.biBitCount+".";
				return false;
			}
			if(bmi.biCompression!=Ds.BI_RGB && !(bmi.biCompression==Ds.BI_BITFIELDS && bmi.biBitCount==32)) {
				error="Unsupported compression "+bmi.biCompression+".";
				return false;
			}
			if(bmi.biWidth<=0 || bmi.biHeight==0 || bmi.biHeight==int.MinValue) {
				error="Invalid size "+bmi.biWidth+"x"+bmi.biHeight+".";
				return false;
			}
			geometry.BitCount=bmi.biBitCount;
			geometry.SourceStride=GetDibStride(bmi.biWidth,bmi.biBitCount);
			geometry.SourceRows=Math.Abs(bmi.biHeight);
			geometry.IsBottomUp=bmi.biHeight>0;
			RECT rect=videoInfoHeader.rcTarget;
			bool isTargetSet=rect.right>rect.left && rect.bottom>rect.top;
			bool isTargetInside=rect.left>=0 && rect.top>=0 && rect.right<=bmi.biWidth && rect.bottom<=geometry.SourceRows;
			if(isTargetSet && isTargetInside) {
				geometry.OffsetX=rect.left;
				geometry.OffsetY=rect.top;
				geometry.Width=rect.right-rect.left;
				geometry.Height=rect.bottom-rect.top;
			}
			else {
				geometry.Width=bmi.biWidth;
				geometry.Height=geometry.SourceRows;
			}
			return true;
		}
	}

	///<summary>Copies a sample into a UvcFrame: honours the source stride, turns bottom-up rows top-down, and expands 24-bit
	///pixels to 32 bits. The fourth byte of every destination pixel is 0xFF.</summary>
	internal static class FrameCopier {
		///<summary>Copies from unmanaged memory (the Sample Grabber's buffer, valid only during BufferCB). Returns false, and
		///leaves the frame unchanged, when the sample is smaller than the geometry needs or the frame has the wrong size.</summary>
		public static bool Copy(IntPtr source,int sourceLength,FrameGeometry geometry,UvcFrame frame) {
			if(source==IntPtr.Zero || sourceLength<geometry.RequiredLength) {
				return false;
			}
			if(frame==null || frame.Width!=geometry.Width || frame.Height!=geometry.Height) {
				return false;
			}
			int bytesPerPixel=geometry.BitCount/8;
			byte[] arrayRow=geometry.BitCount==24 ? new byte[geometry.Width*3] : null;
			byte[] arrayPixels=frame.Pixels;
			for(int y=0;y<geometry.Height;y++) {
				int rowImage=geometry.OffsetY+y;
				int rowMemory=geometry.IsBottomUp ? geometry.SourceRows-1-rowImage : rowImage;
				IntPtr pRow=IntPtr.Add(source,rowMemory*geometry.SourceStride+geometry.OffsetX*bytesPerPixel);
				int indexDest=y*frame.Stride;
				if(geometry.BitCount==32) {
					Marshal.Copy(pRow,arrayPixels,indexDest,geometry.Width*4);
					for(int i=indexDest+3;i<indexDest+frame.Stride;i+=4) {
						arrayPixels[i]=0xFF;
					}
				}
				else {
					Marshal.Copy(pRow,arrayRow,0,arrayRow.Length);
					for(int x=0,i=0;x<geometry.Width;x++,i+=3) {
						arrayPixels[indexDest]=arrayRow[i];
						arrayPixels[indexDest+1]=arrayRow[i+1];
						arrayPixels[indexDest+2]=arrayRow[i+2];
						arrayPixels[indexDest+3]=0xFF;
						indexDest+=4;
					}
				}
			}
			return true;
		}

		///<summary>Copies from a managed array. Same rules as the unmanaged overload.</summary>
		public static bool Copy(byte[] source,FrameGeometry geometry,UvcFrame frame) {
			if(source==null) {
				return false;
			}
			GCHandle handle=GCHandle.Alloc(source,GCHandleType.Pinned);
			try {
				return Copy(handle.AddrOfPinnedObject(),source.Length,geometry,frame);
			}
			finally {
				handle.Free();
			}
		}
	}
}
