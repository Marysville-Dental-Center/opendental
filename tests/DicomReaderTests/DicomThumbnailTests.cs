// CAP-W1 (design 7.8): the explicit .dcm branches of Documents.GetThumbnail and ImageHelper.GetImageCropped. A local A to Z folder in a
// temporary directory and an in-memory preference cache stand in for an office; no database is used. The images are made up.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace DicomReaderTests {
	[TestClass]
	public class DicomThumbnailTests {
		private string _patFolder;

		[TestInitialize]
		public void SetUp() {
			DataTable table=new DataTable();
			table.Columns.Add("PrefNum");
			table.Columns.Add("PrefName");
			table.Columns.Add("ValueString");
			table.Rows.Add("1",nameof(PrefName.AtoZfolderUsed),((int)DataStorageType.LocalAtoZ).ToString());
			Prefs.FillCacheFromTable(table);
			_patFolder=Path.Combine(Path.GetTempPath(),"dicomreadertests_atoz_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_patFolder);
		}

		[TestCleanup]
		public void TearDown() {
			try {
				Directory.Delete(_patFolder,true);
			}
			catch(Exception) {
			}
		}

		///<summary>A 40 x 20 16-bit image from the capture writer: the left half raw 1000, the right half raw 50000.</summary>
		private Document SaveHalves(string fileName) {
			int width=40;
			int height=20;
			ushort[] arrayPixels=new ushort[width*height];
			for(int y=0;y<height;y++) {
				for(int x=0;x<width;x++) {
					arrayPixels[y*width+x]=x<width/2 ? (ushort)1000 : (ushort)50000;
				}
			}
			File.WriteAllBytes(Path.Combine(_patFolder,fileName),TestFiles.FromCaptureWriter(width,height,arrayPixels,null));
			Document document=new Document();
			document.FileName=fileName;
			document.WindowingMin=0;
			document.WindowingMax=255;
			document.DateTStamp=DateTime.Now.AddDays(-1);
			return document;
		}

		private static int Gray(Bitmap bitmap,int x,int y) {
			Color color=bitmap.GetPixel(x,y);
			Assert.AreEqual(color.R,color.G,"gray at "+x+","+y);
			Assert.AreEqual(color.R,color.B,"gray at "+x+","+y);
			return color.R;
		}

		private static void AssertNear(int expected,int actual,string message) {
			Assert.IsTrue(Math.Abs(expected-actual)<=2,message+": expected about "+expected+", got "+actual);
		}

		private static bool IsSameImage(Bitmap bitmapA,Bitmap bitmapB) {
			if(bitmapA.Width!=bitmapB.Width || bitmapA.Height!=bitmapB.Height) {
				return false;
			}
			for(int y=0;y<bitmapA.Height;y++) {
				for(int x=0;x<bitmapA.Width;x++) {
					if(bitmapA.GetPixel(x,y).ToArgb()!=bitmapB.GetPixel(x,y).ToArgb()) {
						return false;
					}
				}
			}
			return true;
		}

		[TestMethod]
		public void GetImageCropped_Dcm_IsWindowedOnce_ThenRotatedAndFlipped() {
			Document document=SaveHalves("halves.dcm");
			//Raw 1000 and 50000 on the 0-255 scale of a 16-bit image: 3.9 and 195.3.
			using(Bitmap bitmap=ImageHelper.GetImageCropped(document,_patFolder)) {
				Assert.AreEqual(40,bitmap.Width);
				Assert.AreEqual(20,bitmap.Height);
				AssertNear(4,Gray(bitmap,10,10),"left half");
				AssertNear(195,Gray(bitmap,30,10),"right half");
			}
			//A window of 50-200: 255*(195.3-50)/150=247. Applying the 8-bit color function again on top would give 255.
			document.WindowingMin=50;
			document.WindowingMax=200;
			using(Bitmap bitmap=ImageHelper.GetImageCropped(document,_patFolder)) {
				AssertNear(0,Gray(bitmap,10,10),"left half, below the window");
				AssertNear(247,Gray(bitmap,30,10),"right half, windowed once");
			}
			//Rotated 90 degrees clockwise: the left half becomes the top half.
			document.WindowingMin=0;
			document.WindowingMax=255;
			document.DegreesRotated=90;
			using(Bitmap bitmap=ImageHelper.GetImageCropped(document,_patFolder)) {
				Assert.AreEqual(20,bitmap.Width);
				Assert.AreEqual(40,bitmap.Height);
				AssertNear(4,Gray(bitmap,10,10),"top half after rotating");
				AssertNear(195,Gray(bitmap,10,30),"bottom half after rotating");
			}
			//Flipped: the halves swap sides.
			document.DegreesRotated=0;
			document.IsFlipped=true;
			using(Bitmap bitmap=ImageHelper.GetImageCropped(document,_patFolder)) {
				AssertNear(195,Gray(bitmap,10,10),"left half after flipping");
				AssertNear(4,Gray(bitmap,30,10),"right half after flipping");
			}
			//Cropped to the 10 x 10 square around the center of the right half (crop center relative to the image center, y up).
			document.IsFlipped=false;
			document.CropX=10;
			document.CropY=0;
			document.CropW=10;
			document.CropH=10;
			using(Bitmap bitmap=ImageHelper.GetImageCropped(document,_patFolder)) {
				Assert.AreEqual(10,bitmap.Width);
				Assert.AreEqual(10,bitmap.Height);
				AssertNear(195,Gray(bitmap,5,5),"inside the crop");
			}
			Assert.AreEqual(0,document.WindowingMin,"Nothing about the document changes.");
			Assert.AreEqual(255,document.WindowingMax);
		}

		[TestMethod]
		public void GetImageCropped_LegacyWindow_IsReadOnTheTwelveBitScale_AndNotWrittenBack() {
			Document document=SaveHalves("legacy.dcm");
			document.WindowingMin=800;//50 on the 0-255 scale
			document.WindowingMax=3200;//200
			using(Bitmap bitmap=ImageHelper.GetImageCropped(document,_patFolder)) {
				AssertNear(247,Gray(bitmap,30,10),"as the window 50-200");
			}
			Assert.AreEqual(800,document.WindowingMin);
			Assert.AreEqual(3200,document.WindowingMax);
		}

		[TestMethod]
		public void GetThumbnail_Dcm_MakesAndKeepsARealThumbnail() {
			Document document=SaveHalves("thumb.dcm");
			using(Bitmap bitmapNotAvailable=Documents.NoAvailablePhoto())
			using(Bitmap bitmapThumb=Documents.GetThumbnail(document,_patFolder)) {
				Assert.AreEqual(100,bitmapThumb.Width);
				Assert.AreEqual(100,bitmapThumb.Height);
				Assert.IsFalse(IsSameImage(bitmapNotAvailable,bitmapThumb),"A DICOM document gets a real thumbnail now.");
				//The 40 x 20 image fills the width and is centered: the left half dark, the right half bright.
				AssertNear(4,Gray(bitmapThumb,20,50),"thumbnail, left");
				AssertNear(195,Gray(bitmapThumb,80,50),"thumbnail, right");
			}
			string pathThumb=Path.Combine(_patFolder,"Thumbnails","thumb.dcm");
			Assert.IsTrue(File.Exists(pathThumb),"Saved as Thumbnails\\<file name>, as the Imaging module saves it.");
			using(Bitmap bitmapSaved=new Bitmap(pathThumb)) {//a normal image file under the document's name
				Assert.AreEqual(100,bitmapSaved.Width);
			}
			//The saved thumbnail is used next time, even after the DICOM file is gone.
			File.Delete(Path.Combine(_patFolder,"thumb.dcm"));
			File.Copy(pathThumb,Path.Combine(_patFolder,"thumb.dcm"));//GetThumbnail first checks that the document's file exists
			using(Bitmap bitmapThumb=Documents.GetThumbnail(document,_patFolder)) {
				AssertNear(195,Gray(bitmapThumb,80,50),"cached thumbnail");
			}
		}

		[TestMethod]
		public void GetThumbnail_UnreadableOrMissingDcm_ShowsNotAvailable_WithoutThrowing() {
			File.WriteAllBytes(Path.Combine(_patFolder,"broken.dcm"),new byte[] { 1,2,3,4,5,6,7,8 });
			byte[] bytesTruncated=TestFiles.FromCaptureWriter(8,8,SyntheticDicom.Ramp(64,65535),null).Take(500).ToArray();
			File.WriteAllBytes(Path.Combine(_patFolder,"truncated.dcm"),bytesTruncated);
			using(Bitmap bitmapNotAvailable=Documents.NoAvailablePhoto()) {
				foreach(string fileName in new[] { "broken.dcm","truncated.dcm","missing.dcm" }) {
					Document document=new Document { FileName=fileName,WindowingMax=255,DateTStamp=DateTime.Now.AddDays(-1) };
					using(Bitmap bitmapThumb=Documents.GetThumbnail(document,_patFolder)) {
						Assert.IsTrue(IsSameImage(bitmapNotAvailable,bitmapThumb),fileName);
					}
				}
			}
		}

		[TestMethod]
		public void IsDicomFileName_MatchesTheDisplayPath() {
			Assert.IsTrue(ImageHelper.IsDicomFileName("a.dcm"));
			Assert.IsFalse(ImageHelper.IsDicomFileName("a.DCM"),"The Imaging module's display path and ImageStore.OpenBitmapDicom test the same way.");
			Assert.IsFalse(ImageHelper.IsDicomFileName("a.dcm.jpg"));
			Assert.IsFalse(ImageHelper.IsDicomFileName(null));
			Assert.IsFalse(ImageHelper.HasImageExtension("a.dcm"),"HasImageExtension stays as it was: GDI+ cannot open DICOM.");
		}
	}
}
