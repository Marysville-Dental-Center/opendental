// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using ImagingCapture.Save;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingCapture.Tests {
	///<summary>The saver's pure helpers: DibDecoder (the DIBs a native transfer gives), PngEncoder and Gray16Samples (inversion, the stored
	///0-255 window and the preview LUT).</summary>
	[TestClass]
	public class SaveHelpersTests {
		#region DibDecoder
		[TestMethod]
		public void Dib8_BottomUpAndTopDown_SamePixels() {
			Func<int,int,byte> grey=(x,y) => (byte)(x*10+y*50);
			foreach(bool isTopDown in new[] { false,true }) {
				string error;
				DecodedImage decodedImage=DibDecoder.Decode(TestImages.Dib8Gray(5,3,grey,isTopDown),out error);
				Assert.IsNotNull(decodedImage,error);
				Assert.IsTrue(decodedImage.IsGray);
				Assert.AreEqual(5,decodedImage.Width);
				Assert.AreEqual(3,decodedImage.Height);
				Assert.AreEqual(8,decodedImage.BitCount);
				Assert.AreEqual(1270,decodedImage.XDpi,0.01,"50000 pixels per meter.");
				for(int y=0;y<3;y++) {
					for(int x=0;x<5;x++) {
						Assert.AreEqual(grey(x,y),decodedImage.Pixels[y*5+x],"top-down rows, isTopDown="+isTopDown);
					}
				}
			}
		}

		[TestMethod]
		public void Dib24_ColorAndGrey() {
			string error;
			DecodedImage decodedImage=DibDecoder.Decode(TestImages.Dib24(3,2,(x,y) => new byte[] { 1,(byte)(x+2),(byte)(y+3) }),out error);
			Assert.IsNotNull(decodedImage,error);
			Assert.IsFalse(decodedImage.IsGray);
			Assert.AreEqual(9,decodedImage.Stride);
			CollectionAssert.AreEqual(new byte[] { 1,2,3 },decodedImage.Pixels.Take(3).ToArray(),"B, G, R of the top-left pixel.");
			CollectionAssert.AreEqual(new byte[] { 1,4,4 },decodedImage.Pixels.Skip(9+3*2).Take(3).ToArray(),"x=2, y=1.");
			decodedImage=DibDecoder.Decode(TestImages.Dib24(3,2,(x,y) => new byte[] { (byte)(x*7),(byte)(x*7),(byte)(x*7) }),out error);
			Assert.IsTrue(decodedImage.IsGray,"A 24-bit image of greys decodes as grey.");
			CollectionAssert.AreEqual(new byte[] { 0,7,14,0,7,14 },decodedImage.Pixels);
		}

		[TestMethod]
		public void Dib1_BlackAndWhite() {
			string error;
			DecodedImage decodedImage=DibDecoder.Decode(TestImages.Dib1(10,2,(x,y) => x%3==0),out error);
			Assert.IsNotNull(decodedImage,error);
			Assert.IsTrue(decodedImage.IsGray);
			for(int x=0;x<10;x++) {
				Assert.AreEqual(x%3==0 ? 255 : 0,decodedImage.Pixels[x]);
			}
		}

		[TestMethod]
		public void Dib_Refused() {
			string error;
			Assert.IsNull(DibDecoder.Decode(null,out error));
			Assert.IsNull(DibDecoder.Decode(new byte[39],out error));
			byte[] dib=TestImages.Dib8Gray(5,3,(x,y) => 0);
			Assert.IsNull(DibDecoder.Decode(dib.Take(dib.Length-1).ToArray(),out error),"Truncated pixels.");
			StringAssert.Contains(error,"shorter");
			using(MemoryStream memoryStream=new MemoryStream())
			using(BinaryWriter binaryWriter=new BinaryWriter(memoryStream)) {
				TestImages.WriteHeader(binaryWriter,2,2,16,0,0);
				binaryWriter.Write(new byte[8]);
				Assert.IsNull(DibDecoder.Decode(memoryStream.ToArray(),out error),"16 bits per pixel is RGB555/565.");
			}
			using(MemoryStream memoryStream=new MemoryStream())
			using(BinaryWriter binaryWriter=new BinaryWriter(memoryStream)) {
				TestImages.WriteHeader(binaryWriter,2,2,8,256,0,compression:1);//BI_RLE8
				binaryWriter.Write(new byte[1024+8]);
				Assert.IsNull(DibDecoder.Decode(memoryStream.ToArray(),out error));
			}
		}
		#endregion DibDecoder

		#region PngEncoder
		[TestMethod]
		public void Png_Gray8_ReadsBackThroughGdiPlus() {
			byte[] arrayPixels=Enumerable.Range(0,7*5).Select(i => (byte)(i*7)).ToArray();
			byte[] bytes=PngEncoder.Encode(7,5,arrayPixels,true,1270,1270);
			Assert.AreEqual(0,bytes[25],"Greyscale.");
			Assert.AreEqual(8,bytes[24]);
			using(MemoryStream memoryStream=new MemoryStream(bytes))
			using(Bitmap bitmap=new Bitmap(memoryStream)) {
				for(int i=0;i<arrayPixels.Length;i++) {
					Assert.AreEqual(arrayPixels[i],bitmap.GetPixel(i%7,i/7).R);
				}
			}
		}

		[TestMethod]
		public void Png_NoDpi_UsesDefault() {
			byte[] bytes=PngEncoder.Encode(2,2,new byte[4],true,0,double.NaN);
			using(MemoryStream memoryStream=new MemoryStream(bytes))
			using(Bitmap bitmap=new Bitmap(memoryStream)) {
				Assert.AreEqual(PngEncoder.DpiDefault,bitmap.HorizontalResolution,1);
			}
			TestUtil.AssertThrows<ArgumentException>(() => PngEncoder.Encode(2,2,new byte[3],true,96,96));
		}
		#endregion PngEncoder

		#region Gray16Samples
		[TestMethod]
		public void Invert_LosslessAndZerosKept() {
			ushort[] arrayPixels={ 0,1,100,4095,2048,0 };
			ushort[] arrayInverted=Gray16Samples.Invert(arrayPixels,12);
			CollectionAssert.AreEqual(new ushort[] { 0,4095,3996,1,2048,0 },arrayInverted);
			CollectionAssert.AreEqual(arrayPixels,Gray16Samples.Invert(arrayInverted,12),"Inverting twice gives the delivered samples.");
			Assert.AreEqual(4095,Gray16Samples.GetInversionMax(arrayPixels,12));
			Assert.AreEqual(65535,Gray16Samples.GetInversionMax(arrayPixels,null),"Unknown effective bits: the full 16 bits.");
			Assert.AreEqual(65535,Gray16Samples.GetInversionMax(new ushort[] { 5000 },12),"A sample above 12 bits.");
			CollectionAssert.AreEqual(new ushort[] { 0,65535,1 },Gray16Samples.Invert(new ushort[] { 0,1,65535 },null));
			Assert.IsNull(Gray16Samples.Invert(null,12));
		}

		[TestMethod]
		public void StoredWindow_On255Scale() {
			int min;
			int max;
			Gray16Samples.GetStoredWindow(100,4095,12,out min,out max);
			Assert.AreEqual(6,min,"100/16 rounded down.");
			Assert.AreEqual(255,max,"4095/16 rounded up, at most 255.");
			Gray16Samples.GetStoredWindow(256,65535,16,out min,out max);
			Assert.AreEqual(1,min);
			Assert.AreEqual(255,max);
			Gray16Samples.GetStoredWindow(1000,1010,16,out min,out max);
			Assert.AreEqual(3,min);
			Assert.AreEqual(4,max,"Rounded up, so the stored window covers the raw one.");
			Gray16Samples.GetStoredWindow(0,0,12,out min,out max);
			Assert.AreEqual(0,min);
			Assert.AreEqual(1,max,"Max stays above min.");
			Gray16Samples.GetStoredWindow(65535,65535,16,out min,out max);
			Assert.AreEqual(254,min);
			Assert.AreEqual(255,max);
		}

		[TestMethod]
		public void Preview_FixedLutFromWindow() {
			byte[] arrayPreview=Gray16Samples.ToPreview(new ushort[] { 0,100,150,200,300 },100,200);
			CollectionAssert.AreEqual(new byte[] { 0,0,128,255,255 },arrayPreview);
		}

		[TestMethod]
		public void SwapBytes() {
			CollectionAssert.AreEqual(new ushort[] { 0x3412,0x00FF,0xFF00 },Gray16Samples.SwapBytes(new ushort[] { 0x1234,0xFF00,0x00FF }));
		}
		#endregion Gray16Samples
	}
}
