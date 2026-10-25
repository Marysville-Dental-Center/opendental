// 25.2 v25_2-imaging-copy-without-drawings: which single documents Copy and Copy without drawings take from the stored file and which they draw, and
// the security log text. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace BIMGTests {
	///<summary>The cases that are decided before the drawings are looked up, so no database is needed. Unedited images and drawings are in
	///ImageCopyRulesDbTests.</summary>
	[TestClass]
	public class ImageCopyRulesTests {
		///<summary>A document as the image window sees it right after import: no crop, rotation or flip, and no brightness/contrast change.</summary>
		private static Document MakeDocument(string fileName) {
			return new Document { FileName=fileName,WindowingMin=0,WindowingMax=0 };
		}

		///<summary>Both commands must reach the same decision, so they put the same clipboard formats on the clipboard.</summary>
		private static void AssertBothDraw(Document document,string message) {
			Assert.IsTrue(ImageCopyRules.MustDrawForCopy(document,includeDrawings:true),message+" (Copy)");
			Assert.IsTrue(ImageCopyRules.MustDrawForCopy(document,includeDrawings:false),message+" (Copy without drawings)");
		}

		[TestMethod]
		public void MustDrawForCopy_EachViewEdit_BothCommandsDraw() {
			Document document=MakeDocument("x.jpg");
			document.CropW=100;
			document.CropH=80;
			AssertBothDraw(document,"Crop");
			document=MakeDocument("x.jpg");
			document.CropX=5;
			AssertBothDraw(document,"Crop offset");
			document=MakeDocument("x.jpg");
			document.DegreesRotated=90;
			AssertBothDraw(document,"Rotation");
			document=MakeDocument("x.png");
			document.IsFlipped=true;
			AssertBothDraw(document,"Flip");
			document=MakeDocument("x.dcm");
			document.WindowingMin=20;
			document.WindowingMax=255;
			AssertBothDraw(document,"Brightness/contrast low end");
			document=MakeDocument("x.jpg");
			document.WindowingMax=200;
			AssertBothDraw(document,"Brightness/contrast high end");
		}

		[TestMethod]
		public void MustDrawForCopy_FilesThatAreNotImages_NeitherCommandDraws() {
			Document document=MakeDocument("scan.pdf");
			document.DegreesRotated=90;
			document.IsFlipped=true;
			document.WindowingMin=30;
			Assert.IsFalse(ImageCopyRules.MustDrawForCopy(document,includeDrawings:true),"PDFs are copied as the stored file.");
			Assert.IsFalse(ImageCopyRules.MustDrawForCopy(document,includeDrawings:false),"Copy without drawings behaves like plain Copy for PDFs.");
			Assert.IsFalse(ImageCopyRules.MustDrawForCopy(null,includeDrawings:true));
			Assert.IsFalse(ImageCopyRules.MustDrawForCopy(null,includeDrawings:false));
		}

		[TestMethod]
		public void GetLogText_CopyUnchanged_WithoutDrawingsSaysSo() {
			string logText="Patient image x.jpg copied to clipboard";
			Assert.AreEqual(logText,ImageCopyRules.GetLogText(logText,includeDrawings:true));
			Assert.AreEqual("Patient image x.jpg copied to clipboard without drawings",ImageCopyRules.GetLogText(logText,includeDrawings:false));
		}
	}
}
