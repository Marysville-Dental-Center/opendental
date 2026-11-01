// CLAIM-DCM: the 8-bit copy of a DICOM X-ray for a claim, and Edit Claim's attachment file (FormImageSelectClaimAttach's
// ClaimAttachDicom.BuildClaimAttachFile). 16-bit and 12-bit X-rays from DicomIntraOralWriter, read through the program's own DICOM display
// path, give an 8-bit image of the right size with the stored window's values; crop, rotation and flip are kept; the document and its
// file never change. No database is used. All images are made up.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace CLAIMDCMTests {
	[TestClass]
	public class ClaimRenderTests {
		private string _patFolder;

		[TestInitialize]
		public void SetUp() {
			TestSupport.UseLocalAtoZ();
			_patFolder=TestSupport.NewFolder();
		}

		[TestCleanup]
		public void TearDown() {
			TestSupport.DeleteFolder(_patFolder);
		}

		[TestMethod]
		public void Read_UsesTheSubsetReaderFirst_ForTheCaptureWritersFiles() {
			foreach(byte[] arrayBytes in new[] { TestSupport.Write16(),TestSupport.Write12() }) {
				DicomSubsetReadResult result=DicomSubsetReader.Read(arrayBytes);
				Assert.AreEqual(DicomSubsetReadStatus.Read,result.Status,"The in-house reader reads what the capture writer writes: "+result);
				Document document=TestSupport.SaveDocument(_patFolder,"read.dcm",arrayBytes);
				BitmapDicom bitmapDicom=ClaimAttachDicom.ReadDocument(document,_patFolder);
				Assert.AreEqual(TestSupport.Width,bitmapDicom.Width);
				Assert.AreEqual(TestSupport.Height,bitmapDicom.Height);
				Assert.AreEqual(result.BitmapDicom.BitsStored,bitmapDicom.BitsStored);
				CollectionAssert.AreEqual(result.BitmapDicom.ArrayDataRaw,bitmapDicom.ArrayDataRaw,"the same pixels as the subset reader");
				CollectionAssert.AreEqual(result.BitmapDicom.ArrayDataRaw,ClaimAttachDicom.ReadFile(Path.Combine(_patFolder,"read.dcm")).ArrayDataRaw);
			}
		}

		[TestMethod]
		public void Render_SixteenBit_StoredWindow() {
			Document document=TestSupport.SaveDocument(_patFolder,"xray16.dcm",TestSupport.Write16());
			using Bitmap bitmap=ClaimAttachDicom.Render(document,ClaimAttachDicom.ReadDocument(document,_patFolder));
			Assert.AreEqual(TestSupport.Width,bitmap.Width);
			Assert.AreEqual(TestSupport.Height,bitmap.Height);
			TestSupport.AssertArea(bitmap,TestSupport.LeftHalf,TestSupport.ExpectedLeft,"16-bit left half, window 50-200");
			TestSupport.AssertArea(bitmap,TestSupport.RightHalf,TestSupport.ExpectedRight,"16-bit right half, window 50-200");
		}

		[TestMethod]
		public void Render_TwelveBit_StoredWindow() {
			byte[] arrayBytes=TestSupport.Write12();
			Assert.AreEqual(12,DicomSubsetReader.Read(arrayBytes).BitmapDicom.BitsStored);
			Document document=TestSupport.SaveDocument(_patFolder,"xray12.dcm",arrayBytes);
			using Bitmap bitmap=ClaimAttachDicom.Render(document,ClaimAttachDicom.ReadDocument(document,_patFolder));
			Assert.AreEqual(TestSupport.Width,bitmap.Width);
			Assert.AreEqual(TestSupport.Height,bitmap.Height);
			TestSupport.AssertArea(bitmap,TestSupport.LeftHalf,TestSupport.ExpectedLeft,"12-bit left half, window 50-200");
			TestSupport.AssertArea(bitmap,TestSupport.RightHalf,TestSupport.ExpectedRight,"12-bit right half, window 50-200");
		}

		[TestMethod]
		public void Render_WindowNeverSet_ShowsTheFullRange_AndLegacyWindowReadsOnTheTwelveBitScale() {
			Document document=TestSupport.SaveDocument(_patFolder,"full.dcm",TestSupport.Write16());
			document.WindowingMin=0;
			document.WindowingMax=0;
			BitmapDicom bitmapDicom=ClaimAttachDicom.ReadDocument(document,_patFolder);
			using(Bitmap bitmap=ClaimAttachDicom.Render(document,bitmapDicom)) {
				TestSupport.AssertArea(bitmap,TestSupport.LeftHalf,100,"0/0 is the full range: 100 stays 100");
				TestSupport.AssertArea(bitmap,TestSupport.RightHalf,150,"0/0 is the full range: 150 stays 150");
			}
			document.WindowingMin=800;//a window stored on the 12-bit scale before 25.4: 50
			document.WindowingMax=3200;//200
			using(Bitmap bitmap=ClaimAttachDicom.Render(document,bitmapDicom)) {
				TestSupport.AssertArea(bitmap,TestSupport.LeftHalf,TestSupport.ExpectedLeft,"legacy window 800-3200 is 50-200");
				TestSupport.AssertArea(bitmap,TestSupport.RightHalf,TestSupport.ExpectedRight,"legacy window 800-3200 is 50-200");
			}
			Assert.AreEqual(800,document.WindowingMin,"not written back");
			Assert.AreEqual(3200,document.WindowingMax);
		}

		[TestMethod]
		public void Render_KeepsRotationFlipAndCrop() {
			Document document=TestSupport.SaveDocument(_patFolder,"turned.dcm",TestSupport.Write16());
			BitmapDicom bitmapDicom=ClaimAttachDicom.ReadDocument(document,_patFolder);
			document.DegreesRotated=90;//clockwise: the left half becomes the top half
			using(Bitmap bitmap=ClaimAttachDicom.Render(document,bitmapDicom)) {
				Assert.AreEqual(TestSupport.Height,bitmap.Width);
				Assert.AreEqual(TestSupport.Width,bitmap.Height);
				//Within 2: GDI+ may place a rotated image a fraction of a pixel off, which only shows at the edges.
				TestSupport.AssertAreaNear(bitmap,new Rectangle(1,1,30,27),TestSupport.ExpectedLeft,"top after rotating");
				TestSupport.AssertAreaNear(bitmap,new Rectangle(1,36,30,27),TestSupport.ExpectedRight,"bottom after rotating");
			}
			document.DegreesRotated=0;
			document.IsFlipped=true;
			using(Bitmap bitmap=ClaimAttachDicom.Render(document,bitmapDicom)) {
				TestSupport.AssertArea(bitmap,TestSupport.LeftHalf,TestSupport.ExpectedRight,"left after flipping");
				TestSupport.AssertArea(bitmap,TestSupport.RightHalf,TestSupport.ExpectedLeft,"right after flipping");
			}
			document.IsFlipped=false;
			document.CropX=16;//the 16 x 16 square at the center of the right half (crop center from the image center, y up)
			document.CropY=0;
			document.CropW=16;
			document.CropH=16;
			using(Bitmap bitmap=ClaimAttachDicom.Render(document,bitmapDicom)) {
				Assert.AreEqual(16,bitmap.Width);
				Assert.AreEqual(16,bitmap.Height);
				TestSupport.AssertArea(bitmap,new Rectangle(0,0,16,16),TestSupport.ExpectedRight,"inside the crop");
			}
		}

		[TestMethod]
		public void Render_PreTwentyOneFourCrop_IsConvertedOnACopy_WithoutTheDatabase() {
			Document document=TestSupport.SaveDocument(_patFolder,"oldcrop.dcm",TestSupport.Write16());
			document.IsCropOld=true;//the crop as stored before 21.4: the upper left corner, in image pixels
			document.CropX=40;
			document.CropY=8;
			document.CropW=16;
			document.CropH=16;
			string settingsBefore=TestSupport.Settings(document);
			using(Bitmap bitmap=ClaimAttachDicom.Render(document,ClaimAttachDicom.ReadDocument(document,_patFolder))) {
				Assert.AreEqual(16,bitmap.Width);
				Assert.AreEqual(16,bitmap.Height);
				TestSupport.AssertArea(bitmap,new Rectangle(0,0,16,16),TestSupport.ExpectedRight,"the old crop of the right half");
			}
			Assert.AreEqual(settingsBefore,TestSupport.Settings(document),"The document keeps its old crop; nothing was converted or saved.");
		}

		[TestMethod]
		public void ConvertCropIfNeeded_WithoutSaving_ConvertsTheDocumentInMemoryOnly() {
			Document document=new Document { IsCropOld=true,CropX=40,CropY=8,CropW=16,CropH=16 };
			using Bitmap bitmap=new Bitmap(TestSupport.Width,TestSupport.Height);
			ImageHelper.ConvertCropIfNeeded(document,bitmap,doSave:false);//there is no database here, so saving would throw
			Assert.IsFalse(document.IsCropOld);
			Assert.AreEqual(16,document.CropX,"the crop center from the image center");
			Assert.AreEqual(0,document.CropY);
		}

		[TestMethod]
		public void Render_WithDrawings_MatchesTheEditClaimCopy_WhenThereAreNone() {
			Document document=TestSupport.SaveDocument(_patFolder,"same.dcm",TestSupport.Write12());
			BitmapDicom bitmapDicom=ClaimAttachDicom.ReadDocument(document,_patFolder);
			Action[] arraySettings={
				() => { },
				() => document.DegreesRotated=90,
				() => { document.DegreesRotated=270; document.IsFlipped=true; },
				() => { document.DegreesRotated=0; document.IsFlipped=false; document.CropX=-16; document.CropY=0; document.CropW=16; document.CropH=16; },
			};
			foreach(Action action in arraySettings) {
				action();
				using Bitmap bitmapEditClaim=ClaimAttachDicom.Render(document,bitmapDicom);
				using Bitmap bitmapDxc=ClaimAttachDicom.Render(document,bitmapDicom,new List<ImageDraw>());
				using Bitmap bitmapDxcNull=ClaimAttachDicom.Render(document,bitmapDicom,null);
				Assert.AreEqual(bitmapEditClaim.Size,bitmapDxc.Size,TestSupport.Settings(document));
				Assert.AreEqual(bitmapEditClaim.Size,bitmapDxcNull.Size);
				for(int y=0;y<bitmapEditClaim.Height;y++) {
					for(int x=0;x<bitmapEditClaim.Width;x++) {
						Assert.AreEqual(bitmapEditClaim.GetPixel(x,y).ToArgb(),bitmapDxc.GetPixel(x,y).ToArgb(),TestSupport.Settings(document)+" at "+x+","+y);
					}
				}
			}
		}

		[TestMethod]
		public void EditClaimFile_SixteenAndTwelveBit_IsAnEightBitJpegOfTheRightSizeAndValues() {
			foreach(bool isTwelveBit in new[] { false,true }) {
				string fileName=isTwelveBit ? "Xray12.dcm" : "Xray16.dcm";
				Document document=TestSupport.SaveDocument(_patFolder,fileName,isTwelveBit ? TestSupport.Write12() : TestSupport.Write16());
				ClaimAttachDicomFile claimAttachDicomFile=ClaimAttachDicom.BuildClaimAttachFile(document,_patFolder);
				Assert.AreEqual(Path.ChangeExtension(fileName,".jpg"),claimAttachDicomFile.DisplayedFileName,"Edit Claim shows the .jpg that is attached");
				Assert.AreEqual(TestSupport.Width,claimAttachDicomFile.Width);
				Assert.AreEqual(TestSupport.Height,claimAttachDicomFile.Height);
				using Bitmap bitmap=TestSupport.DecodeJpeg(claimAttachDicomFile.FileBytes);
				Assert.AreEqual(TestSupport.Width,bitmap.Width,fileName);
				Assert.AreEqual(TestSupport.Height,bitmap.Height,fileName);
				TestSupport.AssertAreaNear(bitmap,TestSupport.LeftHalf,TestSupport.ExpectedLeft,fileName+" left");
				TestSupport.AssertAreaNear(bitmap,TestSupport.RightHalf,TestSupport.ExpectedRight,fileName+" right");
			}
		}

		[TestMethod]
		public void EditClaimFile_NeverChangesTheDicomFileOrTheDocument() {
			Document document=TestSupport.SaveDocument(_patFolder,"kept.dcm",TestSupport.Write16());
			document.DegreesRotated=180;
			document.IsFlipped=true;
			document.CropX=4;
			document.CropY=-2;
			document.CropW=40;
			document.CropH=20;
			string path=Path.Combine(_patFolder,"kept.dcm");
			string hashBefore=TestSupport.Hash(path);
			string settingsBefore=TestSupport.Settings(document);
			string[] arrayFilesBefore=Directory.GetFiles(_patFolder,"*",SearchOption.AllDirectories);
			ClaimAttachDicomFile claimAttachDicomFile=ClaimAttachDicom.BuildClaimAttachFile(document,_patFolder);
			Assert.AreEqual(40,claimAttachDicomFile.Width,"the crop's size");
			Assert.AreEqual(20,claimAttachDicomFile.Height);
			Assert.IsFalse(ClaimAttachDicom.IsDicomBytes(claimAttachDicomFile.FileBytes),"the copy, not the DICOM file");
			Assert.AreEqual(hashBefore,TestSupport.Hash(path),"The DICOM file is not changed.");
			Assert.AreEqual(settingsBefore,TestSupport.Settings(document),"The document is not changed.");
			CollectionAssert.AreEquivalent(arrayFilesBefore,Directory.GetFiles(_patFolder,"*",SearchOption.AllDirectories),
				"Nothing is written into the patient's folder; the window stores the copy in the EmailAttachments folder.");
		}

		[TestMethod]
		public void ReadDocument_UpperCaseExtension_IsReadWithTheSameReader() {
			byte[] arrayBytes=TestSupport.Write16();
			Document document=TestSupport.SaveDocument(_patFolder,"UPPER.DCM",arrayBytes);
			Assert.IsFalse(ImageHelper.IsDicomFileName(document.FileName),"ImageStore.OpenBitmapDicom does not open this name");
			Assert.IsTrue(ClaimAttachDicom.IsDicomFileName(document.FileName),"ImageStore.Import does import it as DICOM");
			CollectionAssert.AreEqual(DicomSubsetReader.Read(arrayBytes).BitmapDicom.ArrayDataRaw,ClaimAttachDicom.ReadDocument(document,_patFolder).ArrayDataRaw);
			Assert.AreEqual("UPPER.jpg",ClaimAttachDicom.BuildClaimAttachFile(document,_patFolder).DisplayedFileName);
		}

		[TestMethod]
		public void RenderFile_UsesTheWindowTheImportWouldStore_AndDoesNotChangeThePixels() {
			string path16=Path.Combine(_patFolder,"disk16.dcm");
			File.WriteAllBytes(path16,TestSupport.Write16());
			BitmapDicom bitmapDicom16=ClaimAttachDicom.ReadFile(path16);
			using(Bitmap bitmap=ClaimAttachDicom.RenderFile(bitmapDicom16)) {
				Assert.AreEqual(TestSupport.Width,bitmap.Width);
				Assert.AreEqual(TestSupport.Height,bitmap.Height);
				TestSupport.AssertArea(bitmap,TestSupport.LeftHalf,100,"16-bit import window: the full range");
				TestSupport.AssertArea(bitmap,TestSupport.RightHalf,150,"16-bit import window: the full range");
			}
			Assert.AreEqual(0,bitmapDicom16.WindowingMin,"the pixels read are not changed");
			Assert.AreEqual(0,bitmapDicom16.WindowingMax);
			string path12=Path.Combine(_patFolder,"disk12.dcm");
			File.WriteAllBytes(path12,TestSupport.Write12());
			BitmapDicom bitmapDicom12=ClaimAttachDicom.ReadFile(path12);
			BitmapDicom bitmapDicomImport=DicomHelper.GetFromFile(path12);
			DicomHelper.CalculateWindowingOnImport(bitmapDicomImport);//what ImageStore.Import stores for this file
			using Bitmap bitmapExpected=DicomHelper.ApplyWindowing(bitmapDicomImport,bitmapDicomImport.WindowingMin,bitmapDicomImport.WindowingMax);
			using Bitmap bitmap12=ClaimAttachDicom.RenderFile(bitmapDicom12);
			Assert.AreEqual(bitmapExpected.Size,bitmap12.Size);
			for(int y=0;y<bitmap12.Height;y++) {
				for(int x=0;x<bitmap12.Width;x++) {
					Assert.AreEqual(bitmapExpected.GetPixel(x,y).ToArgb(),bitmap12.GetPixel(x,y).ToArgb(),"12-bit at "+x+","+y);
				}
			}
		}

		[TestMethod]
		public void FindDocumentForFile_OnlyForAFileInThePatientsFolder() {
			Document document=TestSupport.SaveDocument(_patFolder,"BobXray.dcm",TestSupport.Write16());
			Document[] arrayDocuments={ new Document { FileName="other.jpg" },document };
			Assert.AreSame(document,ClaimAttachDicom.FindDocumentForFile(arrayDocuments,_patFolder,Path.Combine(_patFolder,"BobXray.dcm")));
			Assert.AreSame(document,ClaimAttachDicom.FindDocumentForFile(arrayDocuments,_patFolder+"\\",Path.Combine(_patFolder,"bobxray.DCM")),
				"Windows file names ignore case");
			string folderOther=TestSupport.NewFolder();
			try {
				Assert.IsNull(ClaimAttachDicom.FindDocumentForFile(arrayDocuments,_patFolder,Path.Combine(folderOther,"BobXray.dcm")),"another folder");
			}
			finally {
				TestSupport.DeleteFolder(folderOther);
			}
			Assert.IsNull(ClaimAttachDicom.FindDocumentForFile(arrayDocuments,_patFolder,Path.Combine(_patFolder,"missing.dcm")));
			Assert.IsNull(ClaimAttachDicom.FindDocumentForFile(null,_patFolder,Path.Combine(_patFolder,"BobXray.dcm")));
			Assert.IsNull(ClaimAttachDicom.FindDocumentForFile(arrayDocuments,"",Path.Combine(_patFolder,"BobXray.dcm")));
		}

		[TestMethod]
		public void FileNames() {
			Assert.IsTrue(ClaimAttachDicom.IsDicomFileName("a.dcm"));
			Assert.IsTrue(ClaimAttachDicom.IsDicomFileName("C:\\x\\a.DCM"));
			Assert.IsTrue(ClaimAttachDicom.IsDicomFileName(" a.Dcm "));
			Assert.IsFalse(ClaimAttachDicom.IsDicomFileName("a.dcm.jpg"));
			Assert.IsFalse(ClaimAttachDicom.IsDicomFileName("a.jpg"));
			Assert.IsFalse(ClaimAttachDicom.IsDicomFileName(null));
			Assert.IsFalse(ClaimAttachDicom.IsDicomFileName(""));
			Assert.AreEqual("PatXray12.jpg",ClaimAttachDicom.GetDisplayedFileName("PatXray12.dcm"));
			Assert.AreEqual("Radiograph.jpg",ClaimAttachDicom.GetDisplayedFileName(""));
			StringAssert.Contains(ClaimAttachDicom.GetUnreadableMessage("C:\\x\\a.dcm"),"(a.dcm)");
			StringAssert.Contains(ClaimAttachDicom.GetUnreadableMessage("a<|>.dcm"),"(a<|>.dcm)","a name a path cannot hold is shown as it is");
			Assert.IsFalse(ClaimAttachDicom.GetUnreadableMessage(null).Contains("()"));
			Assert.IsFalse(ImageHelper.HasImageExtension("a.dcm"),"HasImageExtension stays as it was: its callers take it to mean GDI+ can open the file.");
		}
	}
}
