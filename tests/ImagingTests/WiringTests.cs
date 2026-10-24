// The windows cannot be opened in a test run (the GUI is not launched), so these tests read the window sources and check that each one calls the
// shared rules, which the other files test. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.IO;
using System.Text.RegularExpressions;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingTests {
	[TestClass]
	public class WiringTests {
		private static string Read(params string[] arrayParts) {
			return File.ReadAllText(TestDb.FindRepoFile(Path.Combine(arrayParts)));
		}

		private static int Count(string source,string text) {
			return Regex.Matches(source,Regex.Escape(text)).Count;
		}

		[TestMethod]
		public void SignButtons_CheckTheSignPermission() {
			StringAssert.Contains(Read("OpenDental","Main Modules","ControlImages.cs"),"ImageSignaturePermissions.IsAuthorizedToSign(GetDocumentShowing(0))");
			StringAssert.Contains(Read("OpenDental","Main Modules","ControlImagesOld.cs"),"ImageSignaturePermissions.IsAuthorizedToSign(_documentShowing)");
			string frmDocSign=Read("WpfControlsOD","Frms","FrmDocSign.xaml.cs");
			StringAssert.Contains(frmDocSign,"ImageSignaturePermissions.IsAuthorizedToSign(_document)");
			StringAssert.Contains(frmDocSign,"ImageSignaturePermissions.LogSignatureChange(_documentOld,_document)");
		}

		[TestMethod]
		public void ImageDisplay_EveryDocumentEditCheckIncludesSignedImageEdit() {
			string source=Read("OpenDental","User Controls","ControlImageDisplay.cs");
			Assert.AreEqual(0,Regex.Matches(source,@"Security\.IsAuthorized\(EnumPermType\.ImageEdit,(?!_mountShowing)").Count,
				"Every document ImageEdit check goes through ImageSignaturePermissions.IsAuthorizedToEdit.");
			Assert.AreEqual(11,Count(source,"ImageSignaturePermissions.IsAuthorizedToEdit("),"Flip, rotate L/R/180 and size (doc and mount item), crop.");
			Assert.AreEqual(12,Count(source,"ImageSignaturePermissions.LogSignedImageEdited("),"One SignedImageEdit log per ImageEdit log.");
			Assert.AreEqual(Count(source,"SecurityLogs.MakeLogEntry(EnumPermType.ImageEdit,PatientCur.PatNum,logText);"),
				Count(source,"ImageSignaturePermissions.LogSignedImageEdited("));
			StringAssert.Contains(source,"ImageSignaturePermissions.IsAuthorizedToDelete(document)");
			StringAssert.Contains(source,"ImageSignaturePermissions.IsAuthorizedForSigned(_documentArrayShowing)");
			StringAssert.Contains(source,"ImageSignaturePermissions.IsAuthorizedForSigned(new List<Document> { _documentArrayShowing[_idxSelectedInMount],documentSwapped })");
		}

		[TestMethod]
		public void MoveToPatient_ChecksSignedImages() {
			string source=Read("OpenDental","Main Modules","ControlImages.cs");
			StringAssert.Contains(source,"ImageSignaturePermissions.IsAuthorizedForSigned(documentArrayMount)");
			StringAssert.Contains(source,"ImageSignaturePermissions.IsAuthorizedToDelete(GetDocumentShowing(0))");
		}

		[TestMethod]
		public void ItemInfo_SignedImageNeedsSignedImageEdit() {
			string source=Read("WpfControlsOD","Frms","FrmDocInfo.xaml.cs");
			StringAssert.Contains(source,"ImageSignaturePermissions.IsAuthorizedForSigned(_documentOld,suppressMessage:true)");
			StringAssert.Contains(source,"!ImageSignaturePermissions.IsAuthorizedForSigned(_documentOld)");
			StringAssert.Contains(source,"ImageSignaturePermissions.LogSignedImageEdited(_documentOld,");
			StringAssert.Contains(source,"listPermissions.Add(EnumPermType.ImageSignatureCreate);");
			StringAssert.Contains(source,"listPermissions.Add(EnumPermType.SignedImageEdit);");
		}

		[TestMethod]
		public void OldImagesModule_SignedChecksAndPreAuthMode() {
			string source=Read("OpenDental","Main Modules","ControlImagesOld.cs");
			StringAssert.Contains(source,"ImageSignaturePermissions.IsAuthorizedToDelete(doc)");
			Assert.AreEqual(4,Count(source,"ImageSignaturePermissions.IsAuthorizedForSigned(_documentShowing)"),"Flip, rotate left, rotate right, crop.");
			StringAssert.Contains(source,"public void ModuleSelectedPreAuth(long claimNumPreAuth)");
			StringAssert.Contains(source,"EobAttaches.RefreshForOwner(_claimPaymentNum,_claimNumPreAuth)");
			Assert.AreEqual(4,Count(source,"ImageStore.ImportEobAttach("),"Import, paste, scan, multi-page scan.");
			Assert.AreEqual(4,Count(source,",_claimPaymentNum,_claimNumPreAuth);"),"Every EOB import passes the preauth.");
			Assert.AreEqual(0,Count(source,"_claimPaymentNum==0)"),"EOB mode is IsEobMode, not the payment alone.");
		}

		[TestMethod]
		public void PreAuthWindows_ScanAndViewEob() {
			string formImages=Read("OpenDental","Forms","FormImages.cs");
			StringAssert.Contains(formImages,"contrImagesMain.ModuleSelectedPreAuth(ClaimNumPreAuth)");
			string estimates=Read("OpenDental","Forms","FormClaimPayPreAuth.cs");
			StringAssert.Contains(estimates,"formImages.ClaimNumPreAuth=_claimNumPreAuth");
			StringAssert.Contains(estimates,"EobAttaches.ExistsForPreAuth(_claimNumPreAuth)");
			StringAssert.Contains(estimates,"\"Scan EOB\"");
			StringAssert.Contains(estimates,"\"View EOB\"");
			string designer=Read("OpenDental","Forms","FormClaimPayPreAuth.designer.cs");
			StringAssert.Contains(designer,"this.Controls.Add(this.butViewEob);");
			StringAssert.Contains(designer,"this.textEobIsScanned.ReadOnly = true;");
		}

		[TestMethod]
		public void EditPreAuth_ViewEobAndDeleteRules() {
			string source=Read("OpenDental","Forms","FormClaimEdit.cs");
			StringAssert.Contains(source,"butViewEob.Enabled=EobAttaches.ExistsForPreAuth(_claim.ClaimNum);");
			StringAssert.Contains(source,"formImages.ClaimNumPreAuth=_claim.ClaimNum;");
			StringAssert.Contains(source,"Security.IsAuthorized(EnumPermType.ImageDelete,dateTimeOldestEob,suppressMessage:true)");
			StringAssert.Contains(source,"ImageStore.DeleteEobAttachesForPreAuth(_claim.ClaimNum)");
			int idxDeleteEobs=source.IndexOf("ImageStore.DeleteEobAttachesForPreAuth(_claim.ClaimNum)");
			int idxDeleteClaim=source.IndexOf("ClaimL.DeleteClaimHelper(_claim,listETrans835Attaches);");
			Assert.IsTrue(idxDeleteEobs>0 && idxDeleteEobs<idxDeleteClaim,"The EOBs go before the preauth.");
		}
	}
}
