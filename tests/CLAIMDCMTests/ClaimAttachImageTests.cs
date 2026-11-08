// CLAIM-DCM 1 (owner question, fixed on 2026-11-07): Edit Claim's attachment of an image that is not DICOM. An image cropped, rotated,
// flipped or brightness-adjusted in the Imaging module is attached as it shows there, and the file is always written where the attachment
// points (before the fix, a local images folder got the attachment row but no file). The images are synthetic two-tone pictures made here;
// nothing touches the database or the network.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace CLAIMDCMTests {
	[TestClass]
	public class ClaimAttachImageTests {
		private const int LeftGray=100;
		private const int RightGray=150;

		///<summary>A 64 x 32 gray picture, left half LeftGray and right half RightGray, in the format.</summary>
		private static byte[] MakeTwoTone(ImageFormat imageFormat) {
			using Bitmap bitmap=new Bitmap(TestSupport.Width,TestSupport.Height,PixelFormat.Format24bppRgb);
			using(Graphics g=Graphics.FromImage(bitmap)) {
				using SolidBrush brushLeft=new SolidBrush(Color.FromArgb(LeftGray,LeftGray,LeftGray));
				using SolidBrush brushRight=new SolidBrush(Color.FromArgb(RightGray,RightGray,RightGray));
				g.FillRectangle(brushLeft,0,0,TestSupport.Width/2,TestSupport.Height);
				g.FillRectangle(brushRight,TestSupport.Width/2,0,TestSupport.Width/2,TestSupport.Height);
			}
			using MemoryStream memoryStream=new MemoryStream();
			bitmap.Save(memoryStream,imageFormat);
			return memoryStream.ToArray();
		}

		private static Bitmap Decode(byte[] arrayBytes,ImageFormat imageFormatExpected) {
			using MemoryStream memoryStream=new MemoryStream(arrayBytes);
			using Image image=Image.FromStream(memoryStream);
			Assert.AreEqual(imageFormatExpected.Guid,image.RawFormat.Guid,"written in "+imageFormatExpected);
			return new Bitmap(image);
		}

		///<summary>The area without its outer 2 pixels, where drawing the picture onto a new one may blend with the empty background.</summary>
		private static Rectangle Inset(Rectangle rectangle) {
			return new Rectangle(rectangle.X+2,rectangle.Y+2,rectangle.Width-4,rectangle.Height-4);
		}

		private static Document MakeDocument(string fileName) {
			return new Document { FileName=fileName,ImgType=ImageType.Radiograph };
		}

		[TestMethod]
		public void HasImageSettings_EachSettingCounts() {
			Assert.IsFalse(ClaimAttachImage.HasImageSettings(MakeDocument("a.jpg")));
			Assert.IsFalse(ClaimAttachImage.HasImageSettings(null));
			Action<Document>[] arraySetters={
				x => x.CropX=3,x => x.CropY=-3,x => x.CropW=10,x => x.CropH=10,x => x.DegreesRotated=90,x => x.IsFlipped=true,
				x => x.WindowingMin=10,x => x.WindowingMax=200,
			};
			for(int i=0;i<arraySetters.Length;i++) {
				Document document=MakeDocument("a.jpg");
				arraySetters[i](document);
				Assert.IsTrue(ClaimAttachImage.HasImageSettings(document),"setting "+i);
			}
		}

		[TestMethod]
		public void BuildFileBytes_WithoutSettings_IsTheFileItself() {
			byte[] arrayBytesFile=MakeTwoTone(ImageFormat.Jpeg);
			CollectionAssert.AreEqual(arrayBytesFile,ClaimAttachImage.BuildFileBytes(MakeDocument("x.jpg"),arrayBytesFile));
			byte[] arrayBytesText=System.Text.Encoding.ASCII.GetBytes("not an image");
			Document documentText=MakeDocument("note.txt");
			documentText.DegreesRotated=90;
			CollectionAssert.AreEqual(arrayBytesText,ClaimAttachImage.BuildFileBytes(documentText,arrayBytesText),"A file that is not an image is copied.");
		}

		[TestMethod]
		public void BuildFileBytes_Rotated_IsTheImageAsShown_AsAJpeg() {
			Document document=MakeDocument("rotated.jpg");
			document.DegreesRotated=90;
			byte[] arrayBytes=ClaimAttachImage.BuildFileBytes(document,MakeTwoTone(ImageFormat.Png));
			using Bitmap bitmap=TestSupport.DecodeJpeg(arrayBytes);
			Assert.AreEqual(TestSupport.Height,bitmap.Width,"Turned 90 degrees.");
			Assert.AreEqual(TestSupport.Width,bitmap.Height);
			//The left half (dark) is on top after a turn to the right.
			TestSupport.AssertAreaNear(bitmap,new Rectangle(2,2,bitmap.Width-4,bitmap.Height/2-6),LeftGray,"top");
			TestSupport.AssertAreaNear(bitmap,new Rectangle(2,bitmap.Height/2+4,bitmap.Width-4,bitmap.Height/2-6),RightGray,"bottom");
		}

		[TestMethod]
		public void BuildFileBytes_Flipped_KeepsTheFormat() {
			Document document=MakeDocument("flipped.png");
			document.IsFlipped=true;
			byte[] arrayBytes=ClaimAttachImage.BuildFileBytes(document,MakeTwoTone(ImageFormat.Png));
			using Bitmap bitmap=Decode(arrayBytes,ImageFormat.Png);
			Assert.AreEqual(TestSupport.Width,bitmap.Width);
			TestSupport.AssertAreaNear(bitmap,Inset(TestSupport.LeftHalf),RightGray,"left half after the flip");
			TestSupport.AssertAreaNear(bitmap,Inset(TestSupport.RightHalf),LeftGray,"right half after the flip");
			Assert.AreEqual(".png",ClaimAttachImage.GetAttachExtension(document));
		}

		[TestMethod]
		public void BuildFileBytes_Cropped_IsTheCrop() {
			Document document=MakeDocument("cropped.bmp");
			document.CropW=16;
			document.CropH=16;
			document.CropX=-16;//center of the crop, 16 pixels left of the image's center: inside the left half
			byte[] arrayBytes=ClaimAttachImage.BuildFileBytes(document,MakeTwoTone(ImageFormat.Bmp));
			using Bitmap bitmap=Decode(arrayBytes,ImageFormat.Bmp);
			Assert.AreEqual(16,bitmap.Width);
			Assert.AreEqual(16,bitmap.Height);
			TestSupport.AssertAreaNear(bitmap,new Rectangle(1,1,14,14),LeftGray,"the crop holds only the left half");
		}

		[TestMethod]
		public void BuildFileBytes_Windowed_AppliesTheBrightnessAndContrast() {
			Document document=MakeDocument("windowed.png");
			document.WindowingMin=TestSupport.WindowMin;
			document.WindowingMax=TestSupport.WindowMax;
			byte[] arrayBytes=ClaimAttachImage.BuildFileBytes(document,MakeTwoTone(ImageFormat.Png));
			using Bitmap bitmap=Decode(arrayBytes,ImageFormat.Png);
			TestSupport.AssertAreaNear(bitmap,Inset(TestSupport.LeftHalf),TestSupport.ExpectedLeft,"100 shows as 85");
			TestSupport.AssertAreaNear(bitmap,Inset(TestSupport.RightHalf),TestSupport.ExpectedRight,"150 shows as 170");
		}

		[TestMethod]
		public void BuildFileBytes_NeverChangesTheDocument() {
			Document document=MakeDocument("old-crop.jpg");
			document.CropW=16;
			document.CropH=16;
			document.CropX=4;
			document.CropY=4;
			document.IsCropOld=true;//a crop saved before 21.4: converted on a copy only
			document.WindowingMin=10;
			document.WindowingMax=240;
			string settingsBefore=TestSupport.Settings(document);
			ClaimAttachImage.BuildFileBytes(document,MakeTwoTone(ImageFormat.Jpeg));
			Assert.AreEqual(settingsBefore,TestSupport.Settings(document));
		}

		[TestMethod]
		public void GetAttachExtension_ThePngForAFormatThatCannotBeWritten() {
			Document document=MakeDocument("scan.tga");
			Assert.AreEqual(".tga",ClaimAttachImage.GetAttachExtension(document),"Unedited: the file itself.");
			document.DegreesRotated=180;
			Assert.AreEqual(ClaimAttachImage.PngExtension,ClaimAttachImage.GetAttachExtension(document));
			Assert.AreEqual(ImageFormat.Png.Guid,ClaimAttachImage.GetImageFormat("scan.tga").Guid);
			foreach(string fileName in new[] { "a.jpg","a.JPEG","a.png","a.bmp","a.gif","a.tif","a.TIFF" }) {
				Document documentEdited=MakeDocument(fileName);
				documentEdited.IsFlipped=true;
				Assert.AreEqual(Path.GetExtension(fileName),ClaimAttachImage.GetAttachExtension(documentEdited),fileName);
			}
			Assert.AreEqual(ImageFormat.Jpeg.Guid,ClaimAttachImage.GetImageFormat("A.JPG").Guid);
			Assert.AreEqual(ImageFormat.Tiff.Guid,ClaimAttachImage.GetImageFormat("a.tif").Guid);
			Document documentPdf=MakeDocument("letter.pdf");
			documentPdf.IsFlipped=true;
			Assert.AreEqual(".pdf",ClaimAttachImage.GetAttachExtension(documentPdf),"Not an image: its own extension.");
		}

		[TestMethod]
		public void EditClaim_AlwaysWritesTheFileWhereTheAttachmentPoints() {
			//The window itself is not opened in tests: source checks of SaveAttachment, the path of every image that is not DICOM.
			string source=TestSupport.ReadSource("OpenPmsDental","Forms","FormImageSelectClaimAttach.cs").Replace("\r\n","\n");
			int idxStart=source.IndexOf("private void SaveAttachment(Document document){",StringComparison.Ordinal);
			int idxEnd=source.IndexOf("private void SaveAttachmentDicom(",StringComparison.Ordinal);
			Assert.IsTrue(idxStart>=0 && idxEnd>idxStart);
			string save=source.Substring(idxStart,idxEnd-idxStart);
			StringAssert.Contains(save,"arrayBytesAttach=ClaimAttachImage.BuildFileBytes(document,arrayBytesFile);");
			StringAssert.Contains(save,"+ClaimAttachImage.GetAttachExtension(document);");
			StringAssert.Contains(save,"CloudStorage.Upload(attachPath,newName,arrayBytesAttach)");
			StringAssert.Contains(save,"File.WriteAllBytes(ODFileUtils.CombinePaths(attachPath,newName),arrayBytesAttach)");
			//One attachment row, made only after the file was written (local or cloud), never on a path that wrote nothing.
			Assert.AreEqual(1,Regex.Matches(save,@"ClaimAttachNew=new ClaimAttach\(\);").Count);
			int idxRow=save.IndexOf("ClaimAttachNew=new ClaimAttach();",StringComparison.Ordinal);
			Assert.IsTrue(save.IndexOf("CloudStorage.Upload(attachPath,newName,arrayBytesAttach)",StringComparison.Ordinal)<idxRow);
			Assert.IsTrue(save.IndexOf("File.WriteAllBytes(ODFileUtils.CombinePaths(attachPath,newName),arrayBytesAttach)",StringComparison.Ordinal)<idxRow);
			Assert.IsFalse(save.Contains("Bitmap.FromFile("),"The file is read as bytes, from the folder or from cloud storage.");
			Assert.IsFalse(save.Contains("AtoZfolderUsed!=DataStorageType.LocalAtoZ"),"No branch by storage type decides whether the edit is applied.");
		}

		[TestMethod]
		public void LocalFolder_TheEditedImageIsTheAttachedFile() {
			//What SaveAttachment does with a local images folder, step by step: read the patient's file, build the bytes, write them under the new
			//name in the attachments folder. Before the fix the last step was skipped for an edited image.
			string folderPatient=TestSupport.NewFolder();
			string folderAttach=TestSupport.NewFolder();
			try {
				File.WriteAllBytes(Path.Combine(folderPatient,"bw.png"),MakeTwoTone(ImageFormat.Png));
				Document document=MakeDocument("bw.png");
				document.DegreesRotated=90;
				byte[] arrayBytesFile=File.ReadAllBytes(Path.Combine(folderPatient,document.FileName));
				string newName="20261107_1"+ClaimAttachImage.GetAttachExtension(document);
				File.WriteAllBytes(Path.Combine(folderAttach,newName),ClaimAttachImage.BuildFileBytes(document,arrayBytesFile));
				string pathAttached=Path.Combine(folderAttach,newName);
				Assert.IsTrue(File.Exists(pathAttached));
				using Bitmap bitmap=Decode(File.ReadAllBytes(pathAttached),ImageFormat.Png);
				Assert.AreEqual(TestSupport.Height,bitmap.Width,"the rotated image");
			}
			finally {
				TestSupport.DeleteFolder(folderPatient);
				TestSupport.DeleteFolder(folderAttach);
			}
		}
	}
}
