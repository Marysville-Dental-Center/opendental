// 25.2 v25_2-imaging-copy-without-drawings against a scratch 25.3.48 database: drawings are rows of the imagedraw table. Copy draws an image that has
// any, and Copy without drawings draws it too (just without them), so both commands put the same clipboard formats on the clipboard. Synthetic data
// only. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace BIMGTests {
	[TestClass]
	public class ImageCopyRulesDbTests {
		[TestInitialize]
		public void TestInitialize() {
			BImgDb.Begin();
		}

		///<summary>A document whose only change is a drawing: both commands draw it (spec: "render without them, even when the file itself is
		///unedited"), so Copy without drawings puts a bitmap without the drawing on the clipboard, not the stored file.</summary>
		[TestMethod]
		public void OnlyDrawings_BothCommandsDraw() {
			Patient patient=BImgDb.CreatePatient("draw1");
			Document document=BImgDb.CreateDocument(patient,"bimg_draw1.jpg");
			Assert.IsFalse(ImageCopyRules.MustDrawForCopy(document,includeDrawings:true),"No drawings yet: both copy the file.");
			Assert.IsFalse(ImageCopyRules.MustDrawForCopy(document,includeDrawings:false));
			BImgDb.AddDrawing(document,EnumImageAnnotVendor.OpenDental);
			Assert.IsTrue(ImageCopyRules.MustDrawForCopy(document,includeDrawings:true),"Copy shows the drawing, so it draws the image.");
			Assert.IsTrue(ImageCopyRules.MustDrawForCopy(document,includeDrawings:false),
				"Copy without drawings draws the image too, without the drawing, so it gives the same clipboard formats as Copy.");
			Assert.AreEqual(ImageHelper.HasImageBeenEdited(document),ImageCopyRules.MustDrawForCopy(document,includeDrawings:true),
				"Plain Copy decides exactly as before.");
		}

		///<summary>AI annotations are drawings too: Copy without drawings draws the image and leaves them out.</summary>
		[TestMethod]
		public void AiAnnotations_BothCommandsDraw() {
			Patient patient=BImgDb.CreatePatient("draw2");
			Document document=BImgDb.CreateDocument(patient,"bimg_draw2.png");
			BImgDb.AddDrawing(document,EnumImageAnnotVendor.Pearl);
			Assert.IsTrue(ImageCopyRules.MustDrawForCopy(document,includeDrawings:true));
			Assert.IsTrue(ImageCopyRules.MustDrawForCopy(document,includeDrawings:false));
		}

		///<summary>A DICOM with only a drawing: Copy without drawings draws it, so the clipboard gets a bitmap and a JPG, never the raw .dcm.</summary>
		[TestMethod]
		public void DicomWithOnlyDrawings_BothCommandsDraw() {
			Patient patient=BImgDb.CreatePatient("draw6");
			Document document=BImgDb.CreateDocument(patient,"bimg_draw6.dcm");
			Assert.IsFalse(ImageCopyRules.MustDrawForCopy(document,includeDrawings:false));
			BImgDb.AddDrawing(document,EnumImageAnnotVendor.OpenDental);
			Assert.IsTrue(ImageCopyRules.MustDrawForCopy(document,includeDrawings:true));
			Assert.IsTrue(ImageCopyRules.MustDrawForCopy(document,includeDrawings:false));
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

		///<summary>For every kind of document, with and without drawings, both commands decide exactly as ImageHelper.HasImageBeenEdited, so they
		///always take the same path (file copy or drawn bitmap).</summary>
		[TestMethod]
		public void BothCommands_AgreeWithHasImageBeenEdited() {
			Patient patient=BImgDb.CreatePatient("draw5");
			Document document=BImgDb.CreateDocument(patient,"bimg_draw5.jpg");
			Document documentDrawn=BImgDb.CreateDocument(patient,"bimg_draw5b.jpg");
			BImgDb.AddDrawing(documentDrawn,EnumImageAnnotVendor.OpenDental);
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
				new Document { DocNum=document.DocNum,FileName="bimg_draw5.dcm",WindowingMin=0,WindowingMax=0 },
				new Document { DocNum=document.DocNum,FileName="bimg_draw5.pdf",DegreesRotated=90 },
				new Document { DocNum=documentDrawn.DocNum,FileName=documentDrawn.FileName,WindowingMin=0,WindowingMax=0 },
				new Document { DocNum=documentDrawn.DocNum,FileName="bimg_draw5b.dcm",WindowingMin=0,WindowingMax=255 },
				new Document { DocNum=documentDrawn.DocNum,FileName=documentDrawn.FileName,DegreesRotated=90 },
				new Document { DocNum=documentDrawn.DocNum,FileName="bimg_draw5b.pdf",WindowingMin=0,WindowingMax=0 },
			};
			for(int i=0;i<arrayVariants.Length;i++) {
				bool isEdited=ImageHelper.HasImageBeenEdited(arrayVariants[i]);
				Assert.AreEqual(isEdited,ImageCopyRules.MustDrawForCopy(arrayVariants[i],includeDrawings:true),"Copy, variant "+i);
				Assert.AreEqual(isEdited,ImageCopyRules.MustDrawForCopy(arrayVariants[i],includeDrawings:false),"Copy without drawings, variant "+i);
			}
			Assert.IsTrue(ImageCopyRules.MustDrawForCopy(arrayVariants[11],includeDrawings:false),"The unedited image with a drawing is drawn.");
			Assert.IsFalse(ImageCopyRules.MustDrawForCopy(arrayVariants[0],includeDrawings:false),"The unedited image without drawings is not.");
		}
	}
}
