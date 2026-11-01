// V-DXC2 (Open Dental 26.1.64): which files are PDFs, which PDFs can go to DentalXChange, and which clearinghouses take them. No database and
// no network.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.Eclaims;
using OpenPmsDentalBusiness.VendorDirect.DentalXChange;

namespace VDXC2Tests {
	[TestClass]
	public class VDXC2Assembly {
		[AssemblyInitialize]
		public static void AssemblyInitialize(TestContext testContext) {
			//Lans.g returns the English text without touching the database only in en-US.
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			Thread.CurrentThread.CurrentUICulture=new CultureInfo("en-US");
			CultureInfo.DefaultThreadCurrentCulture=new CultureInfo("en-US");
			CultureInfo.DefaultThreadCurrentUICulture=new CultureInfo("en-US");
		}
	}

	[TestClass]
	public class DxcPdfAttachmentTests {
		#region File names (rule 5)
		[TestMethod]
		public void IsPdfFileName_MatchesAnyCase() {
			Assert.IsTrue(DxcPdfAttachment.IsPdfFileName("chart.pdf"));
			Assert.IsTrue(DxcPdfAttachment.IsPdfFileName("CHART.PDF"),"24.3 missed upper case and then failed in Image.FromFile.");
			Assert.IsTrue(DxcPdfAttachment.IsPdfFileName("Chart.Pdf"));
			Assert.IsTrue(DxcPdfAttachment.IsPdfFileName(@"C:\Images\Smith\perio chart.PDF "),"Trailing space ignored.");
			Assert.IsFalse(DxcPdfAttachment.IsPdfFileName("chart.jpg"));
			Assert.IsFalse(DxcPdfAttachment.IsPdfFileName("pdf"));
			Assert.IsFalse(DxcPdfAttachment.IsPdfFileName("chart.pdf.jpg"));
			Assert.IsFalse(DxcPdfAttachment.IsPdfFileName(""));
			Assert.IsFalse(DxcPdfAttachment.IsPdfFileName(null));
		}

		[TestMethod]
		public void GetFileNameToSend_EndsInPdfOnce() {
			Assert.AreEqual("Perio chart.pdf",DxcPdfAttachment.GetFileNameToSend("Perio chart"));
			Assert.AreEqual("Perio chart.PDF",DxcPdfAttachment.GetFileNameToSend("Perio chart.PDF"),"Already a .pdf name in any case: kept.");
			Assert.AreEqual("Perio chart.pdf",DxcPdfAttachment.GetFileNameToSend("  Perio chart.  "),"Trimmed, trailing dots dropped.");
			Assert.AreEqual("Attachment.pdf",DxcPdfAttachment.GetFileNameToSend(""));
			Assert.AreEqual("Attachment.pdf",DxcPdfAttachment.GetFileNameToSend(null));
		}
		#endregion File names (rule 5)

		#region Clearinghouses (rule 3)
		[TestMethod]
		public void IsPdfAccepted_DentalXChangeOnly() {
			Assert.IsTrue(DxcPdfAttachment.IsPdfAccepted(EclaimsCommBridge.ClaimConnect));
			Assert.IsFalse(DxcPdfAttachment.IsPdfAccepted(EclaimsCommBridge.EDS),"EDS documents images only.");
			Assert.IsFalse(DxcPdfAttachment.IsPdfAccepted(EclaimsCommBridge.None));
			StringAssert.Contains(DxcPdfAttachment.GetPdfRefusedMessage(EclaimsCommBridge.EDS),"EDS does not accept PDF attachments");
		}
		#endregion Clearinghouses (rule 3)

		#region The bytes (rules 1 and 2)
		[TestMethod]
		public void GetProblem_PlainPdf_None() {
			Assert.IsNull(DxcPdfAttachment.GetProblem(SyntheticPdf.Build()));
			Assert.IsNull(DxcPdfAttachment.GetProblem(SyntheticPdf.Build(pageCount:3)));
		}

		[TestMethod]
		public void HasPdfHeader_AllowsLeadingBytesUpTo1024() {
			Assert.IsTrue(DxcPdfAttachment.HasPdfHeader(SyntheticPdf.Build(leadingBytes:new string(' ',1000))));
			Assert.IsTrue(DxcPdfAttachment.HasPdfHeader(SyntheticPdf.Build(leadingBytes:new string(' ',1024))));
			Assert.IsFalse(DxcPdfAttachment.HasPdfHeader(SyntheticPdf.Build(leadingBytes:new string(' ',1025))));
			Assert.IsFalse(DxcPdfAttachment.HasPdfHeader(Encoding.ASCII.GetBytes("%PDF")),"Too short to hold the header.");
			Assert.IsFalse(DxcPdfAttachment.HasPdfHeader(null));
		}

		[TestMethod]
		public void GetProblem_NotAPdf() {
			byte[] arrayBytesJpegStart={ 0xFF,0xD8,0xFF,0xE0,0x00,0x10,(byte)'J',(byte)'F',(byte)'I',(byte)'F',0x00 };
			StringAssert.Contains(DxcPdfAttachment.GetProblem(arrayBytesJpegStart),"not a PDF");
			StringAssert.Contains(DxcPdfAttachment.GetProblem(Encoding.ASCII.GetBytes("Renamed text file.pdf")),"not a PDF");
		}

		[TestMethod]
		public void GetProblem_Empty() {
			StringAssert.Contains(DxcPdfAttachment.GetProblem(new byte[0]),"empty");
			StringAssert.Contains(DxcPdfAttachment.GetProblem(null),"empty");
		}

		[TestMethod]
		public void GetProblem_TooLarge() {
			byte[] arrayBytesPdf=SyntheticPdf.Build();
			byte[] arrayBytesBig=new byte[DxcPdfAttachment.MaxPdfBytes+1];
			Buffer.BlockCopy(arrayBytesPdf,0,arrayBytesBig,0,arrayBytesPdf.Length);
			string problem=DxcPdfAttachment.GetProblem(arrayBytesBig);
			StringAssert.Contains(problem,"too large");
			StringAssert.Contains(problem,"10 MB");
			//Exactly at the limit is allowed.
			byte[] arrayBytesAtLimit=new byte[DxcPdfAttachment.MaxPdfBytes];
			Buffer.BlockCopy(arrayBytesPdf,0,arrayBytesAtLimit,0,arrayBytesPdf.Length);
			Assert.IsNull(DxcPdfAttachment.GetProblem(arrayBytesAtLimit));
		}

		[TestMethod]
		public void IsEncrypted_EncryptedVariantsAreRefused() {
			Assert.IsTrue(DxcPdfAttachment.IsEncrypted(SyntheticPdf.BuildEncryptedIndirect()),"/Encrypt 99 0 R in the trailer.");
			Assert.IsTrue(DxcPdfAttachment.IsEncrypted(SyntheticPdf.BuildEncryptedDirect()),"/Encrypt << ... >> in the trailer.");
			Assert.IsTrue(DxcPdfAttachment.IsEncrypted(SyntheticPdf.BuildEncryptedXRefStream()),"/Encrypt in a cross-reference stream dictionary.");
			string problem=DxcPdfAttachment.GetProblem(SyntheticPdf.BuildEncryptedIndirect());
			StringAssert.Contains(problem,"encrypted or password-protected");
			StringAssert.Contains(problem,"without a password");
		}

		[TestMethod]
		public void IsEncrypted_LookalikesAreNotEncrypted() {
			Assert.IsFalse(DxcPdfAttachment.IsEncrypted(SyntheticPdf.Build()));
			//The word in page text is not an /Encrypt entry.
			Assert.IsFalse(DxcPdfAttachment.IsEncrypted(SyntheticPdf.Build(pageText:"Use /Encrypt to protect")));
			//A longer name that starts with /Encrypt.
			Assert.IsFalse(DxcPdfAttachment.IsEncrypted(SyntheticPdf.Build(trailerExtra:"/EncryptMetadata false")));
			//A malformed reference ("/Encrypt 99 R") is not an indirect reference.
			Assert.IsFalse(DxcPdfAttachment.IsEncrypted(SyntheticPdf.Build(trailerExtra:"/Encrypt 99 R")));
			Assert.IsFalse(DxcPdfAttachment.IsEncrypted(null));
		}

		[TestMethod]
		public void CountPages_CountsPagesNotThePageTree() {
			Assert.AreEqual(1,DxcPdfAttachment.CountPages(SyntheticPdf.Build(pageCount:1)));
			Assert.AreEqual(4,DxcPdfAttachment.CountPages(SyntheticPdf.Build(pageCount:4)));
			Assert.AreEqual(0,DxcPdfAttachment.CountPages(SyntheticPdf.BuildEncryptedXRefStream()),"No page objects: no count shown.");
			Assert.AreEqual(0,DxcPdfAttachment.CountPages(null));
		}
		#endregion The bytes (rules 1 and 2)

		#region Files on disk
		[TestMethod]
		public void ReadPdfFile_ReadsAPlainPdf() {
			string path=WriteTemp(SyntheticPdf.Build(pageCount:2),".PDF");
			try {
				CollectionAssert.AreEqual(SyntheticPdf.Build(pageCount:2),DxcPdfAttachment.ReadPdfFile(path));
			}
			finally {
				File.Delete(path);
			}
		}

		[TestMethod]
		public void ReadPdfFile_RefusesWithAMessage() {
			string pathEncrypted=WriteTemp(SyntheticPdf.BuildEncryptedIndirect(),".pdf");
			string pathBig=Path.Combine(Path.GetTempPath(),"vdxc2_"+Guid.NewGuid().ToString("N")+".pdf");
			try {
				ODException ex=ThrowsOD(() => DxcPdfAttachment.ReadPdfFile(pathEncrypted));
				StringAssert.Contains(ex.Message,"password-protected");
				//An oversized file is refused from its length, before it is read.
				using(FileStream fileStream=new FileStream(pathBig,FileMode.CreateNew)) {
					fileStream.SetLength(DxcPdfAttachment.MaxPdfBytes+1);
				}
				ex=ThrowsOD(() => DxcPdfAttachment.ReadPdfFile(pathBig));
				StringAssert.Contains(ex.Message,"too large");
				ex=ThrowsOD(() => DxcPdfAttachment.ReadPdfFile(Path.Combine(Path.GetTempPath(),"vdxc2_missing_"+Guid.NewGuid().ToString("N")+".pdf")));
				StringAssert.Contains(ex.Message,"could not be found");
			}
			finally {
				File.Delete(pathEncrypted);
				File.Delete(pathBig);
			}
		}
		#endregion Files on disk

		#region Attachments
		[TestMethod]
		public void CreateImageAttachment_KeepsThePdfBytes() {
			byte[] arrayBytesPdf=SyntheticPdf.Build(pageCount:2);
			ClaimConnect.ImageAttachment imageAttachment=DxcPdfAttachment.CreateImageAttachment(arrayBytesPdf,"Perio chart",new DateTime(2026,10,1),
				ClaimConnect.ImageTypeCode.PeriodontalCharts);
			Assert.IsTrue(imageAttachment.IsPdf);
			CollectionAssert.AreEqual(arrayBytesPdf,imageAttachment.ImageFileAsBase64,"Sent as it is, never re-encoded as a JPEG.");
			Assert.AreEqual("Perio chart",imageAttachment.ImageFileNameDisplay);
			Assert.AreEqual("Perio chart.pdf",imageAttachment.GetFileNameToSend());
			Assert.AreEqual(new DateTime(2026,10,1),imageAttachment.ImageDate);
			Assert.AreEqual(ClaimConnect.ImageTypeCode.PeriodontalCharts,imageAttachment.ImageType);
			Assert.AreEqual("right",imageAttachment.ImageOrientationType);
		}

		[TestMethod]
		public void CreateImageAttachment_RefusesEncrypted() {
			ODException ex=ThrowsOD(() => DxcPdfAttachment.CreateImageAttachment(SyntheticPdf.BuildEncryptedDirect(),"Locked",DateTime.Today,
				ClaimConnect.ImageTypeCode.OtherAttachments));
			StringAssert.Contains(ex.Message,"encrypted");
		}

		[TestMethod]
		public void ThrowIfAnyCannotBeSent_ChecksPdfsOnly() {
			ClaimConnect.ImageAttachment imageAttachmentImage=new ClaimConnect.ImageAttachment {
				ImageFileNameDisplay="Bitewing",ImageFileAsBase64=new byte[] { 0xFF,0xD8 } };
			ClaimConnect.ImageAttachment imageAttachmentPdf=DxcPdfAttachment.CreateImageAttachment(SyntheticPdf.Build(),"Narrative",DateTime.Today,
				ClaimConnect.ImageTypeCode.Narrative);
			DxcPdfAttachment.ThrowIfAnyCannotBeSent(new System.Collections.Generic.List<ClaimConnect.ImageAttachment> { imageAttachmentImage,imageAttachmentPdf });
			DxcPdfAttachment.ThrowIfAnyCannotBeSent(null);
			//A PDF changed after it was checked is caught before sending, and named.
			imageAttachmentPdf.ImageFileAsBase64=SyntheticPdf.BuildEncryptedIndirect();
			ODException ex=ThrowsOD(() => DxcPdfAttachment.ThrowIfAnyCannotBeSent(
				new System.Collections.Generic.List<ClaimConnect.ImageAttachment> { imageAttachmentImage,imageAttachmentPdf }));
			StringAssert.StartsWith(ex.Message,"Narrative.pdf: ");
		}

		[TestMethod]
		public void ConvertToAttachmentImage_PartnerServiceGetsThePdf() {
			byte[] arrayBytesPdf=SyntheticPdf.Build();
			ClaimConnect.ImageAttachment imageAttachment=DxcPdfAttachment.CreateImageAttachment(arrayBytesPdf,"Referral",new DateTime(2026,10,3),
				ClaimConnect.ImageTypeCode.ReferralForm);
			OpenPmsDentalBusiness.DentalxchangePartnerService.AttachmentImage attachmentImage=imageAttachment.ConvertToAttachmentImage();
			CollectionAssert.AreEqual(arrayBytesPdf,attachmentImage.ImageFileAsBase64);
			Assert.AreEqual("Referral.pdf",attachmentImage.ImageFileName);
			Assert.AreEqual(OpenPmsDentalBusiness.DentalxchangePartnerService.imageTypeCode.B4,attachmentImage.ImageTypeCode);
			Assert.IsFalse(attachmentImage.ImageOrientationTypeSpecified,"Not an X-ray.");
			//On the wire the bytes are base64Binary.
			System.Xml.Serialization.XmlSerializer xmlSerializer=new System.Xml.Serialization.XmlSerializer(attachmentImage.GetType());
			using StringWriter stringWriter=new StringWriter();
			xmlSerializer.Serialize(stringWriter,attachmentImage);
			StringAssert.Contains(stringWriter.ToString(),"<ImageFileAsBase64>"+Convert.ToBase64String(arrayBytesPdf)+"</ImageFileAsBase64>");
			StringAssert.Contains(stringWriter.ToString(),"<ImageFileName>Referral.pdf</ImageFileName>");
		}

		[TestMethod]
		public void ImageAttachment_ImagesKeepTheirName() {
			ClaimConnect.ImageAttachment imageAttachment=new ClaimConnect.ImageAttachment { ImageFileNameDisplay="Bitewing" };
			Assert.IsFalse(imageAttachment.IsPdf);
			Assert.AreEqual("Bitewing",imageAttachment.GetFileNameToSend(),"Image attachments are sent as before.");
		}
		#endregion Attachments

		#region Preview
		[TestMethod]
		public void CreatePreviewBitmap_DrawsAnIcon() {
			using Bitmap bitmap=DxcPdfAttachment.CreatePreviewBitmap(400,520,"Perio chart.pdf",3);
			Assert.AreEqual(400,bitmap.Width);
			Assert.AreEqual(520,bitmap.Height);
			Assert.AreEqual(Color.White.ToArgb(),bitmap.GetPixel(1,1).ToArgb(),"White background.");
			//The red PDF band crosses the middle of the page (sampled left of the centered "PDF" text).
			Color colorBand=bitmap.GetPixel(70,(int)(520*0.06+520*0.70*0.53));
			Assert.IsTrue(colorBand.R>140 && colorBand.G<90,"Band color: "+colorBand);
			using Bitmap bitmapTiny=DxcPdfAttachment.CreatePreviewBitmap(0,0,null);
			Assert.AreEqual(60,bitmapTiny.Width,"Never smaller than 60 by 80.");
			Assert.AreEqual(80,bitmapTiny.Height);
		}
		#endregion Preview

		private static string WriteTemp(byte[] arrayBytes,string extension) {
			string path=Path.Combine(Path.GetTempPath(),"vdxc2_"+Guid.NewGuid().ToString("N")+extension);
			File.WriteAllBytes(path,arrayBytes);
			return path;
		}

		private static ODException ThrowsOD(Action action) {
			try {
				action();
			}
			catch(ODException ex) {
				return ex;
			}
			Assert.Fail("Expected an ODException.");
			return null;
		}
	}
}
