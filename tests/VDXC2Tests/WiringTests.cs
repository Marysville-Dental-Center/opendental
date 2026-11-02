// V-DXC2: the attachment windows use the PDF rules (source checks, since the windows themselves are not opened in tests): no case-sensitive
// ".pdf" check is left, the DentalXChange picker and the claim attachment window accept PDFs through DxcPdfAttachment, EDS refuses them, and
// Snip and Paste stay image-only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.IO;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VDXC2Tests {
	[TestClass]
	public class WiringTests {
		private static string ReadSource(string relativePath) {
			return File.ReadAllText(TestDb.FindRepoFile(relativePath));
		}

		[TestMethod]
		public void NoCaseSensitivePdfCheckIsLeft() {
			string[] arrayPaths={
				Path.Combine("OpenPmsDental","Forms","FormImagePickerDXC.cs"),
				Path.Combine("OpenPmsDental","Forms","FormClaimAttachment.cs"),
				Path.Combine("OpenPmsDental","Forms","FormClaimAttachmentItemEdit.cs"),
			};
			foreach(string path in arrayPaths) {
				string source=ReadSource(path);
				Assert.IsFalse(source.Contains("EndsWith(\".pdf\")"),path);
				Assert.IsFalse(source.Contains("PDF attachments are not supported"),path);
			}
		}

		[TestMethod]
		public void ImagePickerSendsPdfsFromImagesAndFromDisk() {
			string source=ReadSource(Path.Combine("OpenPmsDental","Forms","FormImagePickerDXC.cs"));
			StringAssert.Contains(source,"DxcPdfAttachment.ReadPdfDocument(");
			StringAssert.Contains(source,"DxcPdfAttachment.ReadPdfFile(");
			StringAssert.Contains(source,"DxcPdfAttachment.CreateImageAttachment(");
			string designer=ReadSource(Path.Combine("OpenPmsDental","Forms","FormImagePickerDXC.designer.cs"));
			StringAssert.Contains(designer,"this.butSendFile.Click += new System.EventHandler(this.butSendFile_Click);");
			StringAssert.Contains(designer,"this.Controls.Add(this.butSendFile);");
			StringAssert.Contains(designer,"this.Text = \"Select Image\";","The window title is unchanged.");
		}

		[TestMethod]
		public void ClaimAttachmentWindowAcceptsPdfsForDentalXChangeOnly() {
			string source=ReadSource(Path.Combine("OpenPmsDental","Forms","FormClaimAttachment.cs"));
			StringAssert.Contains(source,"DxcPdfAttachment.IsPdfFileName(selectedFile)");
			StringAssert.Contains(source,"DxcPdfAttachment.IsPdfAccepted(_eclaimsCommBridge)");
			StringAssert.Contains(source,"DxcPdfAttachment.GetPdfRefusedMessage(_eclaimsCommBridge)");
			string sourceItemEdit=ReadSource(Path.Combine("OpenPmsDental","Forms","FormClaimAttachmentItemEdit.cs"));
			StringAssert.Contains(sourceItemEdit,"_eclaimsCommBridge==EclaimsCommBridge.ClaimConnect && _arrayBytesPdf!=null");
		}

		[TestMethod]
		public void SnipAndPasteStayImageOnly() {
			string[] arrayPaths={
				Path.Combine("OpenPmsDental","Forms","FormClaimAttachSnipDXC.cs"),
				Path.Combine("OpenPmsDental","Forms","FormClaimAttachPasteDXC.cs"),
				Path.Combine("OpenPmsDental","Forms","FormClaimAttachPasteDXCItem.cs"),
			};
			foreach(string path in arrayPaths) {
				Assert.IsFalse(ReadSource(path).Contains("DxcPdfAttachment"),path);
			}
		}

		[TestMethod]
		public void SendingChecksPdfsFirst() {
			string source=ReadSource(Path.Combine("OpenPmsDentalBusiness","Eclaims","ClaimConnect.cs"));
			int idxSend=source.IndexOf("public static List<int> SendAttachmentImages(");
			Assert.IsTrue(idxSend>0);
			int idxCheck=source.IndexOf("DxcPdfAttachment.ThrowIfAnyCannotBeSent(listImages);",idxSend);
			int idxOpen=source.IndexOf("OpenAttachment(claim,narrative)",idxSend);
			Assert.IsTrue(idxCheck>0 && idxCheck<idxOpen,"PDFs are checked before the first call.");
			//Both services get the .pdf file name.
			StringAssert.Contains(source,"attachImage.ImageFileName=GetFileNameToSend();");
			StringAssert.Contains(source,"xConnectAttachmentAddImage.imageFileName=listImages[i].GetFileNameToSend();");
		}
	}
}
