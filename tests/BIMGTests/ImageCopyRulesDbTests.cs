// 25.2 v25_2-imaging-copy-without-drawings against a scratch 25.3.48 database: drawings are rows of the imagedraw table, so Copy has to read them
// and Copy without drawings must not depend on them. Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace BIMGTests {
	[TestClass]
	public class ImageCopyRulesDbTests {
		[TestInitialize]
		public void TestInitialize() {
			BImgDb.Begin();
		}

		///<summary>A document whose only change is a drawing: Copy draws it (as before 25.2), Copy without drawings copies the stored file.</summary>
		[TestMethod]
		public void OnlyDrawings_CopyDraws_CopyWithoutDrawingsUsesTheFile() {
			Patient patient=BImgDb.CreatePatient("draw1");
			Document document=BImgDb.CreateDocument(patient,"bimg_draw1.jpg");
			Assert.IsFalse(ImageCopyRules.MustDrawForCopy(document,includeDrawings:true),"No drawings yet: both copy the file.");
			Assert.IsFalse(ImageCopyRules.MustDrawForCopy(document,includeDrawings:false));
			BImgDb.AddDrawing(document,EnumImageAnnotVendor.OpenDental);
			Assert.IsTrue(ImageCopyRules.MustDrawForCopy(document,includeDrawings:true),"Copy shows the drawing, so it draws the image.");
			Assert.IsFalse(ImageCopyRules.MustDrawForCopy(document,includeDrawings:false),"Copy without drawings leaves it out.");
			Assert.AreEqual(ImageHelper.HasImageBeenEdited(document),ImageCopyRules.MustDrawForCopy(document,includeDrawings:true),
				"Plain Copy decides exactly as before.");
		}

		///<summary>AI annotations are drawings too: Copy without drawings leaves them out.</summary>
		[TestMethod]
		public void AiAnnotations_LeftOutWithoutDrawings() {
			Patient patient=BImgDb.CreatePatient("draw2");
			Document document=BImgDb.CreateDocument(patient,"bimg_draw2.png");
			BImgDb.AddDrawing(document,EnumImageAnnotVendor.Pearl);
			Assert.IsTrue(ImageCopyRules.MustDrawForCopy(document,includeDrawings:true));
			Assert.IsFalse(ImageCopyRules.MustDrawForCopy(document,includeDrawings:false));
		}

		///<summary>Crop, rotation, flip and brightness/contrast stay in the copy: with them, both commands draw the image (Copy without drawings then
		///skips the drawings while drawing).</summary>
		[TestMethod]
		public void ViewEditsAndDrawings_BothCommandsDraw() {
			Patient patient=BImgDb.CreatePatient("draw3");
			Document document=BImgDb.CreateDocument(patient,"bimg_draw3.jpg");
			BImgDb.AddDrawing(document,EnumImageAnnotVendor.OpenDental);
			Document documentOld=document.Copy();
			document.DegreesRotated=90;
			document.WindowingMin=10;
			document.WindowingMax=240;
			Documents.Update(document,documentOld);
			document=Documents.GetByNum(document.DocNum);
			Assert.IsTrue(ImageCopyRules.MustDrawForCopy(document,includeDrawings:true));
			Assert.IsTrue(ImageCopyRules.MustDrawForCopy(document,includeDrawings:false));
		}

		///<summary>A PDF with drawings stored against it is still copied as the stored file by both commands.</summary>
		[TestMethod]
		public void Pdf_BehavesLikePlainCopy() {
			Patient patient=BImgDb.CreatePatient("draw4");
			Document document=BImgDb.CreateDocument(patient,"bimg_draw4.pdf");
			BImgDb.AddDrawing(document,EnumImageAnnotVendor.OpenDental);
			Assert.IsFalse(ImageCopyRules.MustDrawForCopy(document,includeDrawings:true));
			Assert.IsFalse(ImageCopyRules.MustDrawForCopy(document,includeDrawings:false));
		}

		///<summary>HasViewEdits repeats the checks of ImageHelper.HasImageBeenEdited that come before the drawings. For documents without drawings
		///the two must agree.</summary>
		[TestMethod]
		public void HasViewEdits_AgreesWithHasImageBeenEdited_WhenThereAreNoDrawings() {
			Patient patient=BImgDb.CreatePatient("draw5");
			Document document=BImgDb.CreateDocument(patient,"bimg_draw5.jpg");
			Document[] arrayVariants=new[] {
				new Document { DocNum=document.DocNum,FileName=document.FileName,WindowingMin=0,WindowingMax=0 },
				new Document { DocNum=document.DocNum,FileName=document.FileName,WindowingMin=0,WindowingMax=255 },
				new Document { DocNum=document.DocNum,FileName=document.FileName,WindowingMin=0,WindowingMax=254 },
				new Document { DocNum=document.DocNum,FileName=document.FileName,WindowingMin=1,WindowingMax=255 },
				new Document { DocNum=document.DocNum,FileName=document.FileName,CropW=10,CropH=10 },
				new Document { DocNum=document.DocNum,FileName=document.FileName,CropY=3 },
				new Document { DocNum=document.DocNum,FileName=document.FileName,DegreesRotated=270 },
				new Document { DocNum=document.DocNum,FileName=document.FileName,IsFlipped=true },
				new Document { DocNum=document.DocNum,FileName="bimg_draw5.dcm",DegreesRotated=90 },
				new Document { DocNum=document.DocNum,FileName="bimg_draw5.pdf",DegreesRotated=90 },
			};
			for(int i=0;i<arrayVariants.Length;i++) {
				Assert.AreEqual(ImageHelper.HasImageBeenEdited(arrayVariants[i]),ImageCopyRules.HasViewEdits(arrayVariants[i]),"Variant "+i);
			}
		}
	}
}
