// 25.2 v25_2-imaging-copy-without-drawings: which single documents Copy and Copy without drawings take from the stored file and which they draw, and
// the security log text. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace BIMGTests {
	[TestClass]
	public class ImageCopyRulesTests {
		///<summary>A document as the image window sees it right after import: no crop, rotation or flip, and no brightness/contrast change.</summary>
		private static Document MakeDocument(string fileName) {
			return new Document { FileName=fileName,WindowingMin=0,WindowingMax=0 };
		}

		[TestMethod]
		public void IsImageFile_BitmapsAndDicomOnly() {
			foreach(string fileName in new[] { "a.jpg","a.JPEG","a.png","a.bmp","a.tif","a.tiff","a.gif","a.tig","a.dcm" }) {
				Assert.IsTrue(ImageCopyRules.IsImageFile(fileName),fileName);
			}
			foreach(string fileName in new[] { "a.pdf","a.docx","a.txt","a.mp4","a",null,"" }) {
				Assert.IsFalse(ImageCopyRules.IsImageFile(fileName),fileName??"null");
			}
		}

		[TestMethod]
		public void HasViewEdits_UneditedImage_False() {
			Assert.IsFalse(ImageCopyRules.HasViewEdits(MakeDocument("x.jpg")));
			Document document=MakeDocument("x.jpg");
			document.WindowingMax=255;//full range, the same as no change
			Assert.IsFalse(ImageCopyRules.HasViewEdits(document));
			Assert.IsFalse(ImageCopyRules.HasViewEdits(null));
		}

		[TestMethod]
		public void HasViewEdits_EachViewEditCounts() {
			Document document=MakeDocument("x.jpg");
			document.CropW=100;
			document.CropH=80;
			Assert.IsTrue(ImageCopyRules.HasViewEdits(document),"Crop");
			document=MakeDocument("x.jpg");
			document.CropX=5;
			Assert.IsTrue(ImageCopyRules.HasViewEdits(document),"Crop offset");
			document=MakeDocument("x.jpg");
			document.DegreesRotated=90;
			Assert.IsTrue(ImageCopyRules.HasViewEdits(document),"Rotation");
			document=MakeDocument("x.png");
			document.IsFlipped=true;
			Assert.IsTrue(ImageCopyRules.HasViewEdits(document),"Flip");
			document=MakeDocument("x.dcm");
			document.WindowingMin=20;
			document.WindowingMax=255;
			Assert.IsTrue(ImageCopyRules.HasViewEdits(document),"Brightness/contrast low end");
			document=MakeDocument("x.jpg");
			document.WindowingMax=200;
			Assert.IsTrue(ImageCopyRules.HasViewEdits(document),"Brightness/contrast high end");
		}

		[TestMethod]
		public void HasViewEdits_FilesThatAreNotImages_AlwaysFalse() {
			Document document=MakeDocument("scan.pdf");
			document.DegreesRotated=90;
			document.IsFlipped=true;
			document.WindowingMin=30;
			Assert.IsFalse(ImageCopyRules.HasViewEdits(document),"PDFs are copied as the stored file, like plain Copy.");
			Assert.IsFalse(ImageCopyRules.MustDrawForCopy(document,includeDrawings:false));
		}

		[TestMethod]
		public void MustDrawForCopy_WithoutDrawings_OnlyViewEditsCount() {
			Document document=MakeDocument("x.jpg");
			Assert.IsFalse(ImageCopyRules.MustDrawForCopy(document,includeDrawings:false),"The stored file has no drawings, so it is the copy.");
			document.DegreesRotated=180;
			Assert.IsTrue(ImageCopyRules.MustDrawForCopy(document,includeDrawings:false),"Rotation is kept, so the image is drawn.");
		}

		[TestMethod]
		public void GetLogText_CopyUnchanged_WithoutDrawingsSaysSo() {
			string logText="Patient image x.jpg copied to clipboard";
			Assert.AreEqual(logText,ImageCopyRules.GetLogText(logText,includeDrawings:true));
			Assert.AreEqual("Patient image x.jpg copied to clipboard without drawings",ImageCopyRules.GetLogText(logText,includeDrawings:false));
		}
	}
}
