// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ImagingDeviceManager.Imaging;
using ImagingDeviceManager.TwainProtocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingDeviceManager.Tests {
	[TestClass]
	public class DibTests {
		private static Color ColorAt(int x,int y) {
			return Color.FromArgb(255,(x*37+y*11)&0xff,(x*5+y*71)&0xff,(x*13+y*3)&0xff);
		}

		private static void AssertSameColor(Color expected,Color actual,string message) {
			Assert.AreEqual(expected.R,actual.R,message+" R");
			Assert.AreEqual(expected.G,actual.G,message+" G");
			Assert.AreEqual(expected.B,actual.B,message+" B");
		}

		[TestMethod]
		public void Resolution_ConvertsBetweenDpiAndPelsPerMeter() {
			Assert.AreEqual(11811,DibImage.DpiToPelsPerMeter(300));
			Assert.AreEqual(300,DibImage.PelsPerMeterToDpi(11811));
			Assert.AreEqual(3780,DibImage.DpiToPelsPerMeter(96));
			Assert.AreEqual(96,DibImage.PelsPerMeterToDpi(3780));
			Assert.AreEqual(0,DibImage.PelsPerMeterToDpi(0));
			Assert.AreEqual(0,DibImage.DpiToPelsPerMeter(0));
		}

		[TestMethod]
		public void Stride_IsPaddedToFourBytes() {
			Assert.AreEqual(4,DibImage.GetStride(1,1));
			Assert.AreEqual(4,DibImage.GetStride(32,1));
			Assert.AreEqual(8,DibImage.GetStride(33,1));
			Assert.AreEqual(12,DibImage.GetStride(3,24));
			Assert.AreEqual(16,DibImage.GetStride(5,24));
			Assert.AreEqual(8,DibImage.GetStride(5,8));
		}

		[TestMethod]
		public void RgbDib_ToBitmap_KeepsPixelsOrientationAndResolution() {
			FakePage page=FakePage.CreateRgb(7,5,200,ColorAt);
			DibImage dib=DibImage.FromPackedDib(page.NativeDib);
			Assert.AreEqual(7,dib.Width);
			Assert.AreEqual(5,dib.Height);
			Assert.AreEqual(24,dib.BitCount);
			Assert.IsFalse(dib.IsTopDown);
			Assert.AreEqual(200,dib.XResolutionDpi);
			using(Bitmap bitmap=dib.ToBitmap()) {
				Assert.AreEqual(PixelFormat.Format24bppRgb,bitmap.PixelFormat);
				Assert.AreEqual(200,bitmap.HorizontalResolution,0.01);
				for(int y=0;y<5;y++) {
					for(int x=0;x<7;x++) {
						AssertSameColor(ColorAt(x,y),bitmap.GetPixel(x,y),"pixel "+x+","+y);
					}
				}
			}
		}

		[TestMethod]
		public void TopDownDib_ToBitmap_IsNotFlipped() {
			int width=3;
			int height=2;
			int stride=DibImage.GetStride(width,24);
			byte[] pixels=new byte[stride*height];
			pixels[0]=255;//first row in memory, first pixel: blue
			pixels[stride+2]=255;//second row in memory, first pixel: red
			byte[] data=DibImage.Create(width,height,24,null,pixels,0,0).Data;
			//Make it top-down by negating biHeight.
			byte[] negativeHeight=BitConverter.GetBytes(-height);
			Buffer.BlockCopy(negativeHeight,0,data,8,4);
			DibImage dib=DibImage.FromPackedDib(data);
			Assert.IsTrue(dib.IsTopDown);
			Assert.AreEqual(2,dib.Height);
			using(Bitmap bitmap=dib.ToBitmap()) {
				AssertSameColor(Color.Blue,bitmap.GetPixel(0,0),"top");
				AssertSameColor(Color.Red,bitmap.GetPixel(0,1),"bottom");
			}
		}

		[TestMethod]
		public void PalettedDib_ToBitmap_KeepsPalette() {
			int width=4;
			int height=2;
			int stride=DibImage.GetStride(width,8);
			byte[] pixels=new byte[stride*height];
			for(int x=0;x<width;x++) {
				pixels[x]=(byte)(x*80);//bottom row
				pixels[stride+x]=255;//top row
			}
			DibImage dib=DibImage.Create(width,height,8,DibImage.CreateGrayPalette(256,false),pixels,0,0);
			using(Bitmap bitmap=dib.ToBitmap()) {
				Assert.AreEqual(PixelFormat.Format8bppIndexed,bitmap.PixelFormat);
				AssertSameColor(Color.FromArgb(240,240,240),bitmap.GetPixel(3,1),"bottom right");
				AssertSameColor(Color.White,bitmap.GetPixel(0,0),"top left");
			}
		}

		[TestMethod]
		public void BwDib_ToBitmap_KeepsBits() {
			FakePage page=FakePage.CreateBw(10,3,300,(x,y) => x==y);
			DibImage dib=DibImage.FromPackedDib(page.NativeDib);
			using(Bitmap bitmap=dib.ToBitmap()) {
				Assert.AreEqual(PixelFormat.Format1bppIndexed,bitmap.PixelFormat);
				AssertSameColor(Color.White,bitmap.GetPixel(1,1),"set bit is white with a black-to-white palette");
				AssertSameColor(Color.Black,bitmap.GetPixel(2,1),"clear bit");
			}
		}

		[TestMethod]
		public void ToHBitmap_CreatesGdiBitmapWithSamePixels() {
			FakePage page=FakePage.CreateRgb(6,4,150,ColorAt);
			DibImage dib=DibImage.FromPackedDib(page.NativeDib);
			IntPtr hBitmap=dib.ToHBitmap();
			try {
				Assert.AreNotEqual(IntPtr.Zero,hBitmap);
				using(Bitmap bitmap=Image.FromHbitmap(hBitmap)) {
					Assert.AreEqual(6,bitmap.Width);
					Assert.AreEqual(4,bitmap.Height);
					AssertSameColor(ColorAt(5,0),bitmap.GetPixel(5,0),"top right");
					AssertSameColor(ColorAt(0,3),bitmap.GetPixel(0,3),"bottom left");
				}
			}
			finally {
				Twain.DeleteObject(hBitmap);
			}
		}

		[TestMethod]
		public void FromPackedDib_RejectsBadData() {
			FakePage page=FakePage.CreateRgb(6,4,150,ColorAt);
			byte[] truncated=new byte[page.NativeDib.Length-1];
			Buffer.BlockCopy(page.NativeDib,0,truncated,0,truncated.Length);
			AssertBadDib(truncated);
			AssertBadDib(new byte[10]);
			AssertBadDib(null);
			byte[] compressed=(byte[])page.NativeDib.Clone();
			compressed[16]=1;//BI_RLE8
			AssertBadDib(compressed);
		}

		private static void AssertBadDib(byte[] data) {
			try {
				DibImage.FromPackedDib(data);
				Assert.Fail("Expected an exception.");
			}
			catch(ImagingDeviceException ex) {
				Assert.AreEqual(EZTwainErrorCode.EZTEC_BAD_DIB,ex.ErrorCode);
			}
		}

		[TestMethod]
		public void FromUnmanaged_ComputesLengthFromHeader() {
			FakePage page=FakePage.CreateBw(20,5,300,(x,y) => (x+y)%3==0);
			IntPtr p=Marshal.AllocHGlobal(page.NativeDib.Length+100);
			try {
				Marshal.Copy(page.NativeDib,0,p,page.NativeDib.Length);
				Assert.AreEqual(page.NativeDib.Length,DibImage.GetPackedLength(p));
				DibImage dib=DibImage.FromUnmanaged(p,page.NativeDib.Length+100);
				CollectionAssert.AreEqual(page.NativeDib,dib.Data);
				try {
					DibImage.FromUnmanaged(p,page.NativeDib.Length-1);
					Assert.Fail("A block smaller than the header says must be rejected.");
				}
				catch(ImagingDeviceException) {
				}
			}
			finally {
				Marshal.FreeHGlobal(p);
			}
		}

		[TestMethod]
		public void SetResolutionIfMissing_OnlyFillsBlanks() {
			DibImage dib=DibImage.Create(2,2,24,null,null,0,0);
			dib.SetResolutionIfMissing(300,150);
			Assert.AreEqual(300,dib.XResolutionDpi);
			Assert.AreEqual(150,dib.YResolutionDpi);
			dib.SetResolutionIfMissing(72,72);
			Assert.AreEqual(300,dib.XResolutionDpi);
		}

		[TestMethod]
		public void Gray16Shift_FollowsTheSignificantBits() {
			Assert.AreEqual(0,DibImage.GetGray16Shift(0));
			Assert.AreEqual(0,DibImage.GetGray16Shift(255));
			Assert.AreEqual(1,DibImage.GetGray16Shift(256));
			Assert.AreEqual(4,DibImage.GetGray16Shift(4095));//12-bit sensor
			Assert.AreEqual(8,DibImage.GetGray16Shift(65535));
		}

		[TestMethod]
		public void ConvertGray16ToGray8_ScalesSamples() {
			FakePage page=FakePage.CreateGray16(4,3,500,(x,y) => x*1000+y);//max 3002, 12 bits, shift 4
			DibImage dib=DibImage.FromPackedDib(page.NativeDib).ConvertGray16ToGray8();
			Assert.AreEqual(8,dib.BitCount);
			Assert.AreEqual(500,dib.XResolutionDpi);
			using(Bitmap bitmap=dib.ToBitmap()) {
				Assert.AreEqual((3000+2)>>4,bitmap.GetPixel(3,2).R);
				Assert.AreEqual((1000+0)>>4,bitmap.GetPixel(1,0).R);
			}
		}

		[TestMethod]
		public void DibBuilder_SwapsRgbToBgrAndFlipsRows() {
			FakePage page=FakePage.CreateRgb(5,4,300,ColorAt);
			DibBuilder dibBuilder=new DibBuilder(5,4,24,Tw.TWPT_RGB,null,false,300,300);
			for(int y=0;y<4;y++) {
				dibBuilder.AddRow(y,page.TwainRows[y]);
			}
			DibImage dib=dibBuilder.Build();
			CollectionAssert.AreEqual(page.NativeDib,dib.Data,"The assembled DIB must equal the native DIB of the same image.");
		}

		[TestMethod]
		public void DibBuilder_AssemblesStripsWithPaddedRows() {
			FakePage page=FakePage.CreateRgb(5,6,300,ColorAt);
			DibBuilder dibBuilder=new DibBuilder(5,6,24,Tw.TWPT_RGB,null,false,300,300);
			int bytesPerRow=16;//15 bytes of pixels plus padding
			foreach(int yStart in new[] { 0,4 }) {
				int rows=Math.Min(4,6-yStart);
				byte[] strip=new byte[rows*bytesPerRow];
				for(int r=0;r<rows;r++) {
					Buffer.BlockCopy(page.TwainRows[yStart+r],0,strip,r*bytesPerRow,15);
				}
				IntPtr p=Marshal.AllocHGlobal(strip.Length);
				try {
					Marshal.Copy(strip,0,p,strip.Length);
					TW_IMAGEMEMXFER xfer=new TW_IMAGEMEMXFER { Compression=Tw.TWCP_NONE,BytesPerRow=(uint)bytesPerRow,Columns=5,Rows=(uint)rows,
						YOffset=(uint)yStart,BytesWritten=(uint)strip.Length };
					dibBuilder.AddStrip(p,strip.Length,xfer);
				}
				finally {
					Marshal.FreeHGlobal(p);
				}
			}
			CollectionAssert.AreEqual(page.NativeDib,dibBuilder.Build().Data);
		}

		[TestMethod]
		public void DibBuilder_UnknownHeightUsesRowsReceived() {
			DibBuilder dibBuilder=new DibBuilder(3,-1,8,Tw.TWPT_GRAY,null,false,100,100);
			dibBuilder.AddRow(0,new byte[] { 1,2,3 });
			dibBuilder.AddRow(1,new byte[] { 4,5,6 });
			dibBuilder.AddRow(2,new byte[] { 7,8,9 });
			DibImage dib=dibBuilder.Build();
			Assert.AreEqual(3,dib.Height);
			using(Bitmap bitmap=dib.ToBitmap()) {
				Assert.AreEqual(1,bitmap.GetPixel(0,0).R);
				Assert.AreEqual(9,bitmap.GetPixel(2,2).R);
			}
		}

		[TestMethod]
		public void DibBuilder_VanillaFlavorInvertsPalette() {
			DibBuilder dibBuilder=new DibBuilder(8,1,1,Tw.TWPT_BW,null,true,300,300);
			dibBuilder.AddRow(0,new byte[] { 0x80 });//first pixel 1, the rest 0
			using(Bitmap bitmap=dibBuilder.Build().ToBitmap()) {
				AssertSameColor(Color.Black,bitmap.GetPixel(0,0),"1 is black when 0 means white");
				AssertSameColor(Color.White,bitmap.GetPixel(1,0),"0 is white");
			}
		}

		[TestMethod]
		public void DibBuilder_Rgb48TakesHighBytes() {
			DibBuilder dibBuilder=new DibBuilder(1,1,48,Tw.TWPT_RGB,null,false,300,300);
			dibBuilder.AddRow(0,new byte[] { 0x11,0xAA,0x22,0xBB,0x33,0xCC });//R=0xAA11 G=0xBB22 B=0xCC33, Intel byte order
			using(Bitmap bitmap=dibBuilder.Build().ToBitmap()) {
				AssertSameColor(Color.FromArgb(0xAA,0xBB,0xCC),bitmap.GetPixel(0,0),"48 bit");
			}
		}

		[TestMethod]
		public void DibBuilder_IgnoresRowsBeyondTheImageAndOversizedStrips() {
			DibBuilder dibBuilder=new DibBuilder(2,2,8,Tw.TWPT_GRAY,null,false,300,300);
			byte[] strip={ 10,20,30,40,50,60 };
			IntPtr p=Marshal.AllocHGlobal(strip.Length);
			try {
				Marshal.Copy(strip,0,p,strip.Length);
				//Claims 5 rows of 2 bytes, but the image has 2 rows and the buffer holds 3.
				dibBuilder.AddStrip(p,strip.Length,new TW_IMAGEMEMXFER { BytesPerRow=2,Columns=2,Rows=5,YOffset=0 });
			}
			finally {
				Marshal.FreeHGlobal(p);
			}
			DibImage dib=dibBuilder.Build();
			Assert.AreEqual(2,dib.Height);
			using(Bitmap bitmap=dib.ToBitmap()) {
				Assert.AreEqual(10,bitmap.GetPixel(0,0).R);
				Assert.AreEqual(40,bitmap.GetPixel(1,1).R);
			}
		}

		[TestMethod]
		public void DibBuilder_RejectsUnsupportedLayouts() {
			try {
				new DibBuilder(10,10,32,Tw.TWPT_RGB,null,false,300,300);
				Assert.Fail("32 bit RGB memory transfers are not supported.");
			}
			catch(ImagingDeviceException ex) {
				Assert.AreEqual(EZTwainErrorCode.EZTEC_BAD_MEMXFER,ex.ErrorCode);
			}
			try {
				new DibBuilder(10,10,8,Tw.TWPT_GRAY,null,false,300,300).Build();
				Assert.Fail("An image with no rows must be rejected.");
			}
			catch(ImagingDeviceException ex) {
				Assert.AreEqual(EZTwainErrorCode.EZTEC_BAD_MEMXFER,ex.ErrorCode);
			}
		}

		[TestMethod]
		public void DibHandleTable_FreeingTwiceIsHarmless() {
			int countBefore=DibHandleTable.Count;
			IntPtr handle=DibHandleTable.Add(DibImage.Create(1,1,24,null,null,0,0));
			IntPtr handle2=DibHandleTable.Add(DibImage.Create(1,1,24,null,null,0,0));
			Assert.AreNotEqual(IntPtr.Zero,handle);
			Assert.AreNotEqual(handle,handle2,"Handles are never reused.");
			Assert.IsNotNull(DibHandleTable.Get(handle));
			Assert.IsTrue(DibHandleTable.Free(handle));
			Assert.IsFalse(DibHandleTable.Free(handle));
			Assert.IsNull(DibHandleTable.Get(handle));
			Assert.AreEqual(IntPtr.Zero,DibHandleTable.Add(null));
			DibHandleTable.Free(handle2);
			Assert.AreEqual(countBefore,DibHandleTable.Count);
		}
	}
}
