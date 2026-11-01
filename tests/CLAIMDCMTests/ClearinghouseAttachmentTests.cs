// CLAIM-DCM: DICOM X-rays sent through the clearinghouse attachment paths, without any network call. The DentalXChange windows (image
// picker, paste, the claim attachment window) and the EDS claim attachment window all hand the 8-bit copy to the attachment builders,
// ClaimConnect.ImageAttachment.Create and EDS.ImageAttachment.Create, which encode it as a JPEG. The sends refuse DICOM bytes.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.Eclaims;

namespace CLAIMDCMTests {
	[TestClass]
	public class ClearinghouseAttachmentTests {
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

		private Bitmap RenderForDxc(bool isTwelveBit) {
			Document document=TestSupport.SaveDocument(_patFolder,isTwelveBit ? "dxc12.dcm" : "dxc16.dcm",
				isTwelveBit ? TestSupport.Write12() : TestSupport.Write16());
			//FormImagePickerDXC sends a DICOM document with its drawings, as it sends every other image (none here).
			return ClaimAttachDicom.Render(document,ClaimAttachDicom.ReadDocument(document,_patFolder),new List<ImageDraw>());
		}

		[TestMethod]
		public void DentalXChange_SixteenAndTwelveBit_SendsAnEightBitJpeg() {
			foreach(bool isTwelveBit in new[] { false,true }) {
				ClaimConnect.ImageAttachment imageAttachment;
				using(Bitmap bitmap=RenderForDxc(isTwelveBit)) {
					imageAttachment=ClaimConnect.ImageAttachment.Create("Bitewing",new DateTime(2026,11,1),ClaimConnect.ImageTypeCode.XRays,bitmap);
				}
				Assert.IsFalse(imageAttachment.IsPdf);
				Assert.AreEqual("Bitewing",imageAttachment.GetFileNameToSend());
				using Bitmap bitmapSent=TestSupport.DecodeJpeg(imageAttachment.ImageFileAsBase64);
				Assert.AreEqual(TestSupport.Width,bitmapSent.Width);
				Assert.AreEqual(TestSupport.Height,bitmapSent.Height);
				//DentalXChange's JPEG is saved at GDI+'s default quality, as for every image; flat 16 x 16 blocks keep their value.
				TestSupport.AssertAreaNear(bitmapSent,TestSupport.LeftHalf,TestSupport.ExpectedLeft,(isTwelveBit ? "12" : "16")+"-bit left");
				TestSupport.AssertAreaNear(bitmapSent,TestSupport.RightHalf,TestSupport.ExpectedRight,(isTwelveBit ? "12" : "16")+"-bit right");
				ClaimAttachDicom.ThrowIfAnyIsDicom(new List<ClaimConnect.ImageAttachment> { imageAttachment });//does not throw
				//The partner service request carries the same bytes.
				Assert.AreSame(imageAttachment.ImageFileAsBase64,imageAttachment.ConvertToAttachmentImage().ImageFileAsBase64);
			}
		}

		[TestMethod]
		public void DentalXChange_FileFromDiskOrPaste_SendsTheCopyWithTheImportWindow() {
			string path=Path.Combine(_patFolder,"pasted.dcm");
			File.WriteAllBytes(path,TestSupport.Write16());
			ClaimConnect.ImageAttachment imageAttachment;
			using(Bitmap bitmap=ClaimAttachDicom.RenderFile(ClaimAttachDicom.ReadFile(path))) {
				//FormClaimAttachPasteDXC creates its rows this way; the image picker's Send File the same with the type the user chose.
				imageAttachment=ClaimConnect.ImageAttachment.Create("pasted",DateTime.Today,ClaimConnect.ImageTypeCode.ReferralForm,bitmap);
			}
			using Bitmap bitmapSent=TestSupport.DecodeJpeg(imageAttachment.ImageFileAsBase64);
			Assert.AreEqual(TestSupport.Width,bitmapSent.Width);
			Assert.AreEqual(TestSupport.Height,bitmapSent.Height);
			TestSupport.AssertAreaNear(bitmapSent,TestSupport.LeftHalf,100,"16-bit import window: the full range");
			TestSupport.AssertAreaNear(bitmapSent,TestSupport.RightHalf,150,"16-bit import window: the full range");
		}

		[TestMethod]
		public void Eds_SixteenAndTwelveBit_SendsAnEightBitJpeg() {
			foreach(bool isTwelveBit in new[] { false,true }) {
				Document document=TestSupport.SaveDocument(_patFolder,isTwelveBit ? "eds12.dcm" : "eds16.dcm",
					isTwelveBit ? TestSupport.Write12() : TestSupport.Write16());
				string path=Path.Combine(_patFolder,document.FileName);
				//FormClaimAttachment (EDS): a .dcm chosen in the patient's folder is that document, so its stored window is used.
				Document documentFound=ClaimAttachDicom.FindDocumentForFile(new[] { document },_patFolder,path);
				Assert.AreSame(document,documentFound);
				EDS.ImageAttachment imageAttachment;
				using(Bitmap bitmap=ClaimAttachDicom.Render(documentFound,ClaimAttachDicom.ReadFile(path))) {
					imageAttachment=EDS.ImageAttachment.Create("Periapical",new DateTime(2026,11,1),EDS.EnumDocumentTypeCode.XRay,bitmap,"",isRightOriented:true);
				}
				Assert.AreEqual(".jpg",imageAttachment.FileExt,"an in-memory image goes to EDS as a JPEG");
				Assert.AreEqual(1,imageAttachment.PageCount);
				Assert.AreEqual(imageAttachment.FileData.LongLength,imageAttachment.FileSize);
				Assert.AreEqual("Right",imageAttachment.OrientationCode);
				using Bitmap bitmapSent=TestSupport.DecodeJpeg(imageAttachment.FileData);
				Assert.AreEqual(TestSupport.Width,bitmapSent.Width);
				Assert.AreEqual(TestSupport.Height,bitmapSent.Height);
				TestSupport.AssertAreaNear(bitmapSent,TestSupport.LeftHalf,TestSupport.ExpectedLeft,(isTwelveBit ? "12" : "16")+"-bit left");
				TestSupport.AssertAreaNear(bitmapSent,TestSupport.RightHalf,TestSupport.ExpectedRight,(isTwelveBit ? "12" : "16")+"-bit right");
				ClaimAttachDicom.ThrowIfAnyIsDicom(new List<EDS.ImageAttachment> { imageAttachment });//does not throw
			}
		}

		[TestMethod]
		public void Sends_RefuseTheDicomFileItself() {
			byte[] arrayBytesDicom=TestSupport.Write16();
			Assert.IsTrue(ClaimAttachDicom.IsDicomBytes(arrayBytesDicom));
			Assert.IsFalse(ClaimAttachDicom.IsDicomBytes(null));
			Assert.IsFalse(ClaimAttachDicom.IsDicomBytes(new byte[131]));
			Assert.IsFalse(ClaimAttachDicom.IsDicomBytes(new byte[2000]),"128 zero bytes without DICM");
			List<ClaimConnect.ImageAttachment> listDxc=new List<ClaimConnect.ImageAttachment> {
				new ClaimConnect.ImageAttachment { ImageFileNameDisplay="ok",ImageFileAsBase64=new byte[] { 0xFF,0xD8,0xFF } },
				null,
				new ClaimConnect.ImageAttachment { ImageFileNameDisplay="raw",ImageFileAsBase64=arrayBytesDicom },
			};
			ODException exceptionDxc=TestSupport.AssertThrowsOD(() => ClaimAttachDicom.ThrowIfAnyIsDicom(listDxc),"DentalXChange");
			StringAssert.StartsWith(exceptionDxc.Message,"raw: ");
			StringAssert.Contains(exceptionDxc.Message,"never sent");
			List<EDS.ImageAttachment> listEds=new List<EDS.ImageAttachment> { new EDS.ImageAttachment { FileDisplayName="raw",FileData=arrayBytesDicom } };
			StringAssert.Contains(TestSupport.AssertThrowsOD(() => ClaimAttachDicom.ThrowIfAnyIsDicom(listEds),"EDS").Message,"never sent");
			ClaimAttachDicom.ThrowIfAnyIsDicom((List<ClaimConnect.ImageAttachment>)null);
			ClaimAttachDicom.ThrowIfAnyIsDicom((List<EDS.ImageAttachment>)null);
		}
	}
}
