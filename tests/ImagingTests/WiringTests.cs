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

		///<summary>The text of one method of a window class: from its signature to its closing brace, which is the first brace at the method's own
		///indent (two tabs).</summary>
		private static string MethodBody(string source,string signature) {
			int idxStart=source.IndexOf(signature);
			Assert.IsTrue(idxStart>=0,signature+" not found.");
			int idxEnd=source.IndexOf("\n\t\t}",idxStart);
			Assert.IsTrue(idxEnd>idxStart,signature+" has no closing brace.");
			return source.Substring(idxStart,idxEnd-idxStart);
		}

		///<summary>Asserts that before comes earlier in the method than after, and that both are there.</summary>
		private static void AssertBefore(string body,string before,string after) {
			int idxBefore=body.IndexOf(before);
			int idxAfter=body.IndexOf(after);
			Assert.IsTrue(idxBefore>=0,before+" not found.");
			Assert.IsTrue(idxAfter>=0,after+" not found.");
			Assert.IsTrue(idxBefore<idxAfter,before+" must come before "+after);
		}

		[TestMethod]
		public void SignButtons_CheckTheSignPermission() {
			StringAssert.Contains(Read("OpenPmsDental","Main Modules","ControlImages.cs"),"ImageSignaturePermissions.IsAuthorizedToSign(GetDocumentShowing(0))");
			string frmDocSign=Read("WpfControlsOD","Frms","FrmDocSign.xaml.cs");
			StringAssert.Contains(frmDocSign,"ImageSignaturePermissions.IsAuthorizedToSign(_document)");
			StringAssert.Contains(frmDocSign,"ImageSignaturePermissions.LogSignatureChange(_documentOld,_document)");
		}

		[TestMethod]
		public void ImageDisplay_EveryDocumentEditCheckIncludesSignedImageEdit() {
			string source=Read("OpenPmsDental","User Controls","ControlImageDisplay.cs");
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
			string source=Read("OpenPmsDental","Main Modules","ControlImages.cs");
			StringAssert.Contains(source,"ImageSignaturePermissions.IsAuthorizedForSigned(documentArrayMount)");
			StringAssert.Contains(source,"ImageSignaturePermissions.IsAuthorizedToDelete(GetDocumentShowing(0))");
		}

		[TestMethod]
		public void DragToCategory_SignedImageNeedsSignedImageEdit() {
			//The category is an Item Info field, so dragging a signed image (or a mount holding one) to another category needs SignedImageEdit.
			string body=MethodBody(Read("OpenPmsDental","Main Modules","ControlImages.cs"),
				"private void imageSelector_DraggedToCategory(object sender,DragEventArgsOD e)");
			AssertBefore(body,"if(!ImageSignaturePermissions.IsAuthorizedForSigned(document)) {","SecurityLogs.MakeLogEntry(EnumPermType.ImageEdit,document.PatNum");
			AssertBefore(body,"if(!ImageSignaturePermissions.IsAuthorizedForSigned(document)) {","document.DocCategory=e.DefNumNew;");
			AssertBefore(body,"Documents.Update(document,docOld);","ImageSignaturePermissions.LogSignedImageEdited(document,\"moved to category \"+docNewCat);");
			AssertBefore(body,"Documents.GetDocumentsForMountItems(MountItems.GetItemsForMount(mountShowing.MountNum))",
				"if(!ImageSignaturePermissions.IsAuthorizedForSigned(documentArrayMountItems)) {");
			AssertBefore(body,"if(!ImageSignaturePermissions.IsAuthorizedForSigned(documentArrayMountItems)) {","mountShowing.DocCategory=e.DefNumNew;");
			AssertBefore(body,"Documents.UpdateDocCategoryForMountItems(","ImageSignaturePermissions.LogSignedImageEdited(documentArrayMountItems[i],");
			Assert.AreEqual(2,Count(body,"FillImageSelector(true);//The drag snaps back."),"A refused drag refills the selector.");
			//S0-6 removed the old Images module, whose tree drag and drop had the same rule; this module is the only one left.
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
		public void PreAuthWindows_ScanAndViewEob() {
			//S0-6: the EOB and amendment window, rebuilt on the current Imaging code, opens the preauthorization's EOBs from ClaimNumPreAuth (the
			//preauth wins over a claim payment, as before) and adds every new EOB with both owner keys.
			string formImages=Read("OpenPmsDental","Forms","FormImages.cs");
			StringAssert.Contains(formImages,"_mode=EobAmendmentFiles.GetMode(ClaimPaymentNum,ClaimNumPreAuth,EhrAmendmentCur);");
			StringAssert.Contains(formImages,"EobAmendmentFiles.GetFileEntries(_mode,ClaimPaymentNum,ClaimNumPreAuth,_ehrAmendment)");
			Assert.AreEqual(4,Count(formImages,"EobAmendmentFiles.AddEob("),"Import, paste, scan, multi-page scan.");
			Assert.AreEqual(4,Count(formImages,",_mode,ClaimPaymentNum,ClaimNumPreAuth)"),"Every EOB import passes the preauth.");
			Assert.AreEqual(0,Count(formImages+Read("OpenPmsDental","Forms","FormImages.designer.cs"),"ControlImagesOld"),"Not the old module.");
			string imageStore=Read("OpenPmsDentalBusiness","Imaging","ImageStore.cs");
			Assert.IsTrue(Regex.IsMatch(MethodBody(imageStore,"public static EnumMode GetMode("),@"^[^}]*if\(claimNumPreAuth!=0\) \{\s*return EnumMode\.PreAuth;"),
				"The preauth is checked first.");
			StringAssert.Contains(imageStore,"EobAttaches.RefreshForOwner(claimPaymentNumOwner,claimNumPreAuthOwner)");
			string estimates=Read("OpenPmsDental","Forms","FormClaimPayPreAuth.cs");
			StringAssert.Contains(estimates,"formImages.ClaimNumPreAuth=_claimNumPreAuth");
			StringAssert.Contains(estimates,"EobAttaches.ExistsForPreAuth(_claimNumPreAuth)");
			StringAssert.Contains(estimates,"\"Scan EOB\"");
			StringAssert.Contains(estimates,"\"View EOB\"");
			string designer=Read("OpenPmsDental","Forms","FormClaimPayPreAuth.designer.cs");
			StringAssert.Contains(designer,"this.Controls.Add(this.butViewEob);");
			StringAssert.Contains(designer,"this.textEobIsScanned.ReadOnly = true;");
		}

		[TestMethod]
		public void EditPreAuth_ViewEobAndDeleteRules() {
			string source=Read("OpenPmsDental","Forms","FormClaimEdit.cs");
			StringAssert.Contains(source,"butViewEob.Enabled=EobAttaches.ExistsForPreAuth(_claim.ClaimNum);");
			StringAssert.Contains(source,"formImages.ClaimNumPreAuth=_claim.ClaimNum;");
			StringAssert.Contains(source,"Security.IsAuthorized(EnumPermType.ImageDelete,dateTimeOldestEob,suppressMessage:true)");
			StringAssert.Contains(source,"ImageStore.DeleteEobAttachesForPreAuth(_claim.ClaimNum)");
			int idxDeleteEobs=source.IndexOf("ImageStore.DeleteEobAttachesForPreAuth(_claim.ClaimNum)");
			int idxDeleteClaim=source.IndexOf("ClaimL.DeleteClaimHelper(_claim,listETrans835Attaches);");
			Assert.IsTrue(idxDeleteEobs>0 && idxDeleteEobs<idxDeleteClaim,"The EOBs go before the preauth.");
		}

		///<summary>The shared helper makes the checks of the Edit Preauthorization Delete button, with the same messages, in the same order.</summary>
		[TestMethod]
		public void PreAuthEobDeleteHelper_SameChecksAndMessagesAsEditPreAuth() {
			string helper=MethodBody(Read("OpenPmsDental","Logic","ClaimL.cs"),"public static bool TryDeletePreAuthEobs(Claim claim)");
			Assert.IsTrue(Regex.IsMatch(helper,@"EobAttaches\.GetForClaimDelete\(claim\);\s*if\(listEobAttaches\.Count==0\) \{\s*return true;"),
				"Any other claim, and a preauth with no EOBs, goes ahead at once.");
			AssertBefore(helper,"if(listEobAttaches.Count==0) {","if(!EobAttaches.IsAuthorizedToDeleteWithPreAuth(listEobAttaches)) {");
			AssertBefore(helper,"if(!EobAttaches.IsAuthorizedToDeleteWithPreAuth(listEobAttaches)) {","MessageBoxButtons.OKCancel)!=DialogResult.OK)");
			AssertBefore(helper,"MessageBoxButtons.OKCancel)!=DialogResult.OK)","ImageStore.DeleteEobAttachesForPreAuth(claim.ClaimNum);");
			AssertBefore(helper,"ImageStore.DeleteEobAttachesForPreAuth(claim.ClaimNum);","catch(Exception ex) {");
			Assert.AreEqual(3,Count(helper,"return false;"),"No ImageDelete, cancelled, or the EOBs could not be deleted.");
			StringAssert.Contains(helper,"\"EOBs attached to this preauthorization\")+\": \"+listEobAttaches.Count+","The confirmation names the count.");
			StringAssert.Contains(helper,"GroupPermissions.GetDesc(EnumPermType.ImageDelete)");
			//The same English text, under the same translation category as the window, so both read the same in every language.
			string formClaimEdit=Read("OpenPmsDental","Forms","FormClaimEdit.cs");
			string[] arrayTexts=new[] {
				"This preauthorization has EOBs attached. Deleting it also deletes them, which needs the permission",
				"EOBs attached to this preauthorization",
				"They will be deleted too. Continue?",
				"Could not delete the EOBs of this preauthorization, so it was not deleted.",
			};
			for(int i=0;i<arrayTexts.Length;i++) {
				StringAssert.Contains(helper,"Lan.g(\"FormClaimEdit\",\""+arrayTexts[i]+"\")");
				StringAssert.Contains(formClaimEdit,"Lan.g(this,\""+arrayTexts[i]+"\")");
			}
		}

		///<summary>The other window that deletes a claim (Send Claims) runs the helper after its own refusals and confirmation, and before any
		///claimproc or the claim is deleted. A false from the helper ends the delete.</summary>
		[TestMethod]
		public void DeleteClaimElsewhere_PreAuthEobsGoFirst() {
			string send=MethodBody(Read("OpenPmsDental","Forms","FormClaimsSend.cs"),"private void menuItemDeleteClaim_Click(object sender,EventArgs e)");
			Assert.AreEqual(1,Count(send,"ClaimL.TryDeletePreAuthEobs("));
			Assert.IsTrue(Regex.IsMatch(send,@"if\(!ClaimL\.TryDeletePreAuthEobs\(claim\)\) \{\s*return;\s*\}"),"A false ends the delete.");
			AssertBefore(send,"Cannot delete, there are claim procedures attached to this claim.","ClaimL.TryDeletePreAuthEobs(claim)");
			AssertBefore(send,"ClaimL.TryDeletePreAuthEobs(claim)","ClaimProcs.DeleteMany(listClaimProcs);");
			AssertBefore(send,"ClaimL.TryDeletePreAuthEobs(claim)","Claims.Delete(claim,");
			//The institutional claim window FormClaimInstEdit was never compiled and was deleted with the other uncompiled sources.
		}
	}
}
