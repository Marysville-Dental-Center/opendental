// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Runtime.InteropServices;
using ImagingCapture.Uvc;
using ImagingCapture.Uvc.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingCapture.Tests {
	///<summary>Stride, bottom-up order and 24/32-bit conversion of Sample Grabber buffers, on synthetic samples.</summary>
	[TestClass]
	public class FrameCopierTests {
		private const byte Padding=0xEE;

		private static VIDEOINFOHEADER MakeHeader(int width,int height,int bitCount,int compression=0) {
			VIDEOINFOHEADER videoInfoHeader=new VIDEOINFOHEADER();
			videoInfoHeader.bmiHeader.biSize=40;
			videoInfoHeader.bmiHeader.biWidth=width;
			videoInfoHeader.bmiHeader.biHeight=height;
			videoInfoHeader.bmiHeader.biPlanes=1;
			videoInfoHeader.bmiHeader.biBitCount=(short)bitCount;
			videoInfoHeader.bmiHeader.biCompression=compression;
			return videoInfoHeader;
		}

		private static FrameGeometry Geometry(VIDEOINFOHEADER videoInfoHeader) {
			FrameGeometry geometry;
			string error;
			Assert.IsTrue(FrameGeometry.TryCreate(videoInfoHeader,out geometry,out error),error);
			return geometry;
		}

		///<summary>The colour of image pixel (x,y), y counted from the top: blue = x, green = y, red = 100+x+y.</summary>
		private static byte[] ExpectedBgr(int x,int y) {
			return new byte[] { (byte)x,(byte)y,(byte)(100+x+y) };
		}

		///<summary>A DIB sample of the given geometry: every image pixel set by ExpectedBgr, the unused fourth byte of 32-bit
		///pixels 0, and row padding filled with Padding.</summary>
		private static byte[] MakeSample(int width,int height,int bitCount,bool isBottomUp) {
			int stride=FrameGeometry.GetDibStride(width,bitCount);
			byte[] arraySample=new byte[stride*height];
			for(int i=0;i<arraySample.Length;i++) {
				arraySample[i]=Padding;
			}
			int bytesPerPixel=bitCount/8;
			for(int y=0;y<height;y++) {
				int rowMemory=isBottomUp ? height-1-y : y;
				for(int x=0;x<width;x++) {
					int index=rowMemory*stride+x*bytesPerPixel;
					Array.Copy(ExpectedBgr(x,y),0,arraySample,index,3);
					if(bitCount==32) {
						arraySample[index+3]=0;
					}
				}
			}
			return arraySample;
		}

		private static void AssertFrame(UvcFrame frame,int offsetX,int offsetY) {
			for(int y=0;y<frame.Height;y++) {
				for(int x=0;x<frame.Width;x++) {
					int index=y*frame.Stride+x*4;
					byte[] arrayExpected=ExpectedBgr(x+offsetX,y+offsetY);
					string where="("+x+","+y+")";
					Assert.AreEqual(arrayExpected[0],frame.Pixels[index],"blue "+where);
					Assert.AreEqual(arrayExpected[1],frame.Pixels[index+1],"green "+where);
					Assert.AreEqual(arrayExpected[2],frame.Pixels[index+2],"red "+where);
					Assert.AreEqual(0xFF,frame.Pixels[index+3],"fourth byte "+where);
				}
			}
		}

		[TestMethod]
		public void Stride_IsPaddedToFourBytes() {
			Assert.AreEqual(16,FrameGeometry.GetDibStride(4,32));
			Assert.AreEqual(12,FrameGeometry.GetDibStride(4,24));
			Assert.AreEqual(16,FrameGeometry.GetDibStride(5,24));
			Assert.AreEqual(4,FrameGeometry.GetDibStride(1,24));
		}

		[TestMethod]
		public void Geometry_PositiveHeightIsBottomUp() {
			FrameGeometry geometry=Geometry(MakeHeader(4,3,32));
			Assert.IsTrue(geometry.IsBottomUp);
			Assert.AreEqual(4,geometry.Width);
			Assert.AreEqual(3,geometry.Height);
			Assert.AreEqual(16,geometry.SourceStride);
			Assert.AreEqual(48,geometry.RequiredLength);
			geometry=Geometry(MakeHeader(5,-2,24));
			Assert.IsFalse(geometry.IsBottomUp);
			Assert.AreEqual(2,geometry.Height);
			Assert.AreEqual(16,geometry.SourceStride);
		}

		[TestMethod]
		public void Geometry_RejectsOtherFormats() {
			FrameGeometry geometry;
			string error;
			Assert.IsFalse(FrameGeometry.TryCreate(MakeHeader(4,3,16),out geometry,out error));
			Assert.IsFalse(FrameGeometry.TryCreate(MakeHeader(4,3,24,0x47504A4D),out geometry,out error),"MJPG is compressed.");
			Assert.IsFalse(FrameGeometry.TryCreate(MakeHeader(0,3,32),out geometry,out error));
			Assert.IsFalse(FrameGeometry.TryCreate(MakeHeader(4,0,32),out geometry,out error));
			Assert.IsTrue(FrameGeometry.TryCreate(MakeHeader(4,3,32,Ds.BI_BITFIELDS),out geometry,out error));
			Assert.IsFalse(FrameGeometry.TryCreate(MakeHeader(4,3,24,Ds.BI_BITFIELDS),out geometry,out error));
		}

		[TestMethod]
		public void Geometry_TargetRectangleInsideIsUsed_OutsideIsIgnored() {
			VIDEOINFOHEADER videoInfoHeader=MakeHeader(8,6,32);
			videoInfoHeader.rcTarget=new RECT { left=2,top=1,right=6,bottom=4 };
			FrameGeometry geometry=Geometry(videoInfoHeader);
			Assert.AreEqual(4,geometry.Width);
			Assert.AreEqual(3,geometry.Height);
			Assert.AreEqual(2,geometry.OffsetX);
			Assert.AreEqual(1,geometry.OffsetY);
			videoInfoHeader.rcTarget=new RECT { left=0,top=0,right=9,bottom=6 };
			geometry=Geometry(videoInfoHeader);
			Assert.AreEqual(8,geometry.Width);
			Assert.AreEqual(0,geometry.OffsetX);
		}

		[TestMethod]
		public void Copy32_BottomUp_RowsComeOutTopDown() {
			FrameGeometry geometry=Geometry(MakeHeader(4,3,32));
			UvcFrame frame=new UvcFrame(4,3);
			Assert.IsTrue(FrameCopier.Copy(MakeSample(4,3,32,true),geometry,frame));
			AssertFrame(frame,0,0);
		}

		[TestMethod]
		public void Copy32_TopDown() {
			FrameGeometry geometry=Geometry(MakeHeader(4,-3,32));
			UvcFrame frame=new UvcFrame(4,3);
			Assert.IsTrue(FrameCopier.Copy(MakeSample(4,3,32,false),geometry,frame));
			AssertFrame(frame,0,0);
		}

		[TestMethod]
		public void Copy24_BottomUp_PaddedStrideIsSkipped() {
			FrameGeometry geometry=Geometry(MakeHeader(5,3,24));
			Assert.AreEqual(16,geometry.SourceStride,"5 pixels x 3 bytes = 15, padded to 16.");
			UvcFrame frame=new UvcFrame(5,3);
			Assert.IsTrue(FrameCopier.Copy(MakeSample(5,3,24,true),geometry,frame));
			AssertFrame(frame,0,0);
		}

		[TestMethod]
		public void Copy24_TopDown() {
			FrameGeometry geometry=Geometry(MakeHeader(7,-2,24));
			UvcFrame frame=new UvcFrame(7,2);
			Assert.IsTrue(FrameCopier.Copy(MakeSample(7,2,24,false),geometry,frame));
			AssertFrame(frame,0,0);
		}

		[TestMethod]
		public void Copy_TargetRectangle_BottomUp() {
			VIDEOINFOHEADER videoInfoHeader=MakeHeader(8,6,32);
			videoInfoHeader.rcTarget=new RECT { left=2,top=1,right=6,bottom=4 };
			FrameGeometry geometry=Geometry(videoInfoHeader);
			UvcFrame frame=new UvcFrame(4,3);
			Assert.IsTrue(FrameCopier.Copy(MakeSample(8,6,32,true),geometry,frame));
			AssertFrame(frame,2,1);
		}

		[TestMethod]
		public void Copy_ShortSampleOrWrongFrame_IsRefusedAndLeavesTheFrame() {
			FrameGeometry geometry=Geometry(MakeHeader(4,3,32));
			byte[] arraySample=MakeSample(4,3,32,true);
			byte[] arrayShort=new byte[arraySample.Length-1];
			Array.Copy(arraySample,arrayShort,arrayShort.Length);
			UvcFrame frame=new UvcFrame(4,3);
			frame.Pixels[0]=42;
			Assert.IsFalse(FrameCopier.Copy(arrayShort,geometry,frame));
			Assert.AreEqual(42,frame.Pixels[0]);
			Assert.IsFalse(FrameCopier.Copy(arraySample,geometry,new UvcFrame(3,3)));
			Assert.IsFalse(FrameCopier.Copy((byte[])null,geometry,frame));
			Assert.IsFalse(FrameCopier.Copy(IntPtr.Zero,100,geometry,frame));
		}

		[TestMethod]
		public void Copy_FromUnmanagedMemory() {
			FrameGeometry geometry=Geometry(MakeHeader(6,4,24));
			byte[] arraySample=MakeSample(6,4,24,true);
			IntPtr pSample=Marshal.AllocHGlobal(arraySample.Length);
			try {
				Marshal.Copy(arraySample,0,pSample,arraySample.Length);
				UvcFrame frame=new UvcFrame(6,4);
				Assert.IsTrue(FrameCopier.Copy(pSample,arraySample.Length,geometry,frame));
				AssertFrame(frame,0,0);
			}
			finally {
				Marshal.FreeHGlobal(pSample);
			}
		}
	}
}
