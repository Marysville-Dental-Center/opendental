// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using ImagingCapture.UI;
using ImagingDeviceManager.Capture;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingCapture.Tests {
	///<summary>CapturePreview: the session window's 8-bit picture of a received image (design 7.3 step 4), as stored and unrotated.</summary>
	[TestClass]
	public class CapturePreviewTests {
		[TestMethod]
		public void Gray8_AsDelivered_TopDown() {
			AcquiredImage image=TestImages.Image8(3,2,(x,y) => (byte)(10*x+100*y));
			string error;
			CapturePreviewPixels pixels=CapturePreview.FromImage(image,false,out error);
			Assert.AreEqual("",error);
			Assert.IsTrue(pixels.IsGray);
			Assert.AreEqual(3,pixels.Width);
			Assert.AreEqual(2,pixels.Height);
			Assert.AreEqual(3,pixels.Stride);
			CollectionAssert.AreEqual(new byte[] { 0,10,20,100,110,120 },pixels.Pixels,"Top row first.");
		}

		[TestMethod]
		public void Color24_ThreeBytesPerPixel() {
			AcquiredImage image=TestImages.Image8(2,1,(x,y) => 0);
			image.Dib=TestImages.Dib24(2,1,(x,y) => x==0 ? new byte[] { 255,0,0 } : new byte[] { 0,0,255 });
			string error;
			CapturePreviewPixels pixels=CapturePreview.FromImage(image,false,out error);
			Assert.IsFalse(pixels.IsGray);
			Assert.AreEqual(6,pixels.Stride);
		}

		[TestMethod]
		public void Gray16_ThroughItsWindow_InvertedWhenSavedInverted() {
			AcquiredImage image=TestImages.Image16(4,1,(x,y) => (ushort)(1000+x*1000));
			string error;
			CapturePreviewPixels pixels=CapturePreview.FromImage(image,false,out error);
			Assert.AreEqual("",error);
			Assert.IsTrue(pixels.IsGray);
			Assert.AreEqual(4,pixels.Pixels.Length);
			Assert.AreEqual(0,pixels.Pixels[0],"The window's low end is black.");
			Assert.AreEqual(255,pixels.Pixels[3],"Its high end white.");
			CapturePreviewPixels pixelsInverted=CapturePreview.FromImage(image,true,out error);
			Assert.AreEqual(255,pixelsInverted.Pixels[0],"Polarity=Invert applied: as stored.");
			Assert.AreEqual(0,pixelsInverted.Pixels[3]);
		}

		[TestMethod]
		public void Unreadable_NullWithReason() {
			string error;
			Assert.IsNull(CapturePreview.FromImage(null,false,out error));
			Assert.AreEqual("No image.",error);
			AcquiredImage image=TestImages.Image8(2,2,(x,y) => 0);
			image.Dib=new byte[] { 1,2,3 };
			Assert.IsNull(CapturePreview.FromImage(image,false,out error));
			Assert.AreNotEqual("",error);
			AcquiredImage image16=TestImages.Image16(4,4,(x,y) => 5);
			image16.Gray16=new ushort[3];
			Assert.IsNull(CapturePreview.FromImage(image16,false,out error));
			StringAssert.Contains(error,"fewer samples");
		}
	}
}
