// CLAIM-DCM: the claim attachment windows use the DICOM rules. Source checks, since the windows themselves are not opened in tests: each
// place a claim image can be chosen takes DICOM through ClaimAttachDicom or ClaimAttachDicomL, no window attaches or names the .dcm file
// itself, and the DentalXChange and EDS sends refuse DICOM bytes before their first call.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CLAIMDCMTests {
	[TestClass]
	public class WiringTests {
		///<summary>The text of one method: from its signature to the next method's "\r\n\t\t}\r\n" at the same depth.</summary>
		private static string MethodBody(string source,string signature) {
			int idxStart=source.IndexOf(signature,StringComparison.Ordinal);
			Assert.IsTrue(idxStart>=0,"not found: "+signature);
			int idxEnd=source.IndexOf("\n\t\t}",idxStart,StringComparison.Ordinal);
			Assert.IsTrue(idxEnd>idxStart,"no end: "+signature);
			return source.Substring(idxStart,idxEnd-idxStart);
		}

		private static void AssertBefore(string source,string first,string second,string message) {
			int idxFirst=source.IndexOf(first,StringComparison.Ordinal);
			int idxSecond=source.IndexOf(second,StringComparison.Ordinal);
			Assert.IsTrue(idxFirst>=0,"not found: "+first);
			Assert.IsTrue(idxSecond>=0,"not found: "+second);
			Assert.IsTrue(idxFirst<idxSecond,message);
		}

		[TestMethod]
		public void EditClaim_AttachesTheJpegCopy_NeverTheDcm() {
			string source=TestSupport.ReadSource("OpenDental","Forms","FormImageSelectClaimAttach.cs").Replace("\r\n","\n");
			string save=MethodBody(source,"private void SaveAttachment(Document document){");
			AssertBefore(save,"ClaimAttachDicom.IsDicomFileName(document.FileName)","ImageHelper.HasImageExtension(document.FileName)",
				"A DICOM document goes to SaveAttachmentDicom before the image-extension check refuses it.");
			AssertBefore(save,"SaveAttachmentDicom(document,patientfolder);","File.Copy(",
				"The DICOM branch returns before any copy of the original file.");
			string saveDicom=MethodBody(source,"private void SaveAttachmentDicom(Document document,string patientFolder) {");
			StringAssert.Contains(saveDicom,"ClaimAttachDicom.BuildClaimAttachFile(document,patientFolder)");
			StringAssert.Contains(saveDicom,"+ClaimAttachDicom.JpegExtension;");
			StringAssert.Contains(saveDicom,"ClaimAttachNew.DisplayedFileName=claimAttachDicomFile.DisplayedFileName;");
			StringAssert.Contains(saveDicom,"CloudStorage.Upload(attachPath,newName,claimAttachDicomFile.FileBytes)");
			StringAssert.Contains(saveDicom,"File.WriteAllBytes(ODFileUtils.CombinePaths(attachPath,newName),claimAttachDicomFile.FileBytes)");
			Assert.IsFalse(saveDicom.Contains("File.Copy("),"The DICOM file is never copied as the attachment.");
			Assert.IsFalse(saveDicom.Contains("Documents.Update"),"The DICOM document is never changed.");
			string import=MethodBody(source,"private void butImport_Click(object sender,EventArgs e) {");
			AssertBefore(import,"ClaimAttachDicomL.CanReadFile(this,selectedFilePath)","ImportDocument(selectedFilePath);",
				"An unreadable .dcm is neither imported nor attached.");
			string clipboard=MethodBody(source,"private Bitmap GetImageFromClipboard(");
			AssertBefore(clipboard,"ClaimAttachDicomL.GetBitmapForFile(this,_patient,fileName)","new Bitmap(fileName)","A pasted .dcm file is its copy.");
		}

		[TestMethod]
		public void DentalXChangePicker_SendsTheCopy_AndNeverNamesTheDcm() {
			string source=TestSupport.ReadSource("OpenDental","Forms","FormImagePickerDXC.cs").Replace("\r\n","\n");
			string send=MethodBody(source,"private bool SendSelectedToDXCAndSaveLocally() {");
			AssertBefore(send,"ClaimAttachDicomL.GetBitmapForDocument(this,document,_patFolder,doIncludeDrawings:true)",
				"else if(!fileName.ToLower().EndsWith(\".bmp\")","A DICOM document is rendered before the image-extension check refuses it.");
			StringAssert.Contains(send,"ClaimAttachDicomL.GetBitmapForFile(this,PatientCur,_pathFileFromDisk)");
			StringAssert.Contains(send,"if(arrayBytesPdf==null && !isDicomDocument) {");
			StringAssert.Contains(send,"if(_docNumSelected>0 && !isDicomDocument) {",
				"Only a non-DICOM document is named as the attachment's file; a DICOM one is saved as the copy that was sent.");
			StringAssert.Contains(send,"MountHelper.GetBitmapOfMountFromDb(_mountNumSelected,listDocumentsDicomUnreadable)");
			StringAssert.Contains(send,"ClaimAttachDicomL.GetMountUnreadableMessage(listDocumentsDicomUnreadable)");
			StringAssert.Contains(source,"*.tiff;*.dcm|","Send File offers .dcm files.");
			string preview=MethodBody(source,"private void imageSelector_SelectionChangeCommitted(object sender,EventArgs e) {");
			StringAssert.Contains(preview,"doShowMessage:false");
			StringAssert.Contains(preview,"MountHelper.GetBitmapOfMountFromDb(mountNum,new List<Document>())");
		}

		[TestMethod]
		public void ClaimAttachmentWindowAndPaste_TakeDcmFilesAsTheCopy() {
			string attachment=TestSupport.ReadSource("OpenDental","Forms","FormClaimAttachment.cs").Replace("\r\n","\n");
			string addImage=MethodBody(attachment,"private void buttonAddImage_Click(object sender,EventArgs e) {");
			AssertBefore(addImage,"ClaimAttachDicomL.GetBitmapForFile(this,_patient,selectedFile)","Image.FromFile(selectedFile)",
				"Add Image renders a .dcm before GDI+ is asked to open it.");
			string clipboard=MethodBody(attachment,"private Bitmap GetImageFromClipboard(");
			AssertBefore(clipboard,"ClaimAttachDicomL.GetBitmapForFile(this,_patient,fileName)","new Bitmap(fileName)","Paste renders a .dcm file.");
			string paste=TestSupport.ReadSource("OpenDental","Forms","FormClaimAttachPasteDXC.cs").Replace("\r\n","\n");
			string pasteFiles=MethodBody(paste,"private bool GetImagesFromClipboard() {");
			AssertBefore(pasteFiles,"ClaimAttachDicomL.GetBitmapForFile(this,PatientCur,listFilePaths[i])","new Bitmap(listFilePaths[i])",
				"Paste renders a .dcm file.");
			Assert.IsFalse(paste.Contains("DxcPdfAttachment"),"Paste stays image-only (V-DXC2).");
		}

		[TestMethod]
		public void Sends_RefuseDicomBytesBeforeTheFirstCall() {
			string claimConnect=TestSupport.ReadSource("OpenDentBusiness","Eclaims","ClaimConnect.cs").Replace("\r\n","\n");
			string send=MethodBody(claimConnect,"public static List<int> SendAttachmentImages(Claim claim,List<ImageAttachment> listImages,string narrative) {");
			AssertBefore(send,"ClaimAttachDicom.ThrowIfAnyIsDicom(listImages);","OpenAttachment(claim,narrative)","DentalXChange: refused before the first call.");
			string add=MethodBody(claimConnect,"public static List<int> AddAttachmentImage(Claim claim,List<ImageAttachment> listImages) {");
			AssertBefore(add,"ClaimAttachDicom.ThrowIfAnyIsDicom(listImages);","XConnect.AttachmentAddImage(claim,listImages)","Every image call checks too.");
			string eds=TestSupport.ReadSource("OpenDentBusiness","Eclaims","EDS.cs").Replace("\r\n","\n");
			string save=MethodBody(eds,"public static EDS.SaveAttachmentsResponse SaveAttachments(");
			AssertBefore(save,"ClaimAttachDicom.ThrowIfAnyIsDicom(listImageAttachments);","MakeWebRequest(","EDS: refused before the call.");
		}

		[TestMethod]
		public void UnreadableDicom_ShowsOneClearMessage() {
			string source=TestSupport.ReadSource("OpenDental","Logic","ClaimAttachDicomL.cs").Replace("\r\n","\n");
			StringAssert.Contains(source,"ClaimAttachDicom.ReadDocument(document,patFolder)");
			StringAssert.Contains(source,"ClaimAttachDicom.ReadFile(path)");
			StringAssert.Contains(source,"ShowMessage(owner,ex.Message);");
			StringAssert.Contains(TestSupport.ReadSource("OpenDental","OpenDental.csproj"),@"<Compile Include=""Logic\ClaimAttachDicomL.cs"" />");
			StringAssert.Contains(TestSupport.ReadSource("OpenDentBusiness","OpenDentBusiness.csproj"),@"<Compile Include=""Imaging\ClaimAttachDicom.cs"" />");
		}
	}
}
