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
		public void DragToCategory_SignedImageNeedsSignedImageEdit() {
			//The category is an Item Info field, so dragging a signed image (or a mount holding one) to another category needs SignedImageEdit.
			string body=MethodBody(Read("OpenDental","Main Modules","ControlImages.cs"),
				"private void imageSelector_DraggedToCategory(object sender,DragEventArgsOD e)");
			AssertBefore(body,"if(!ImageSignaturePermissions.IsAuthorizedForSigned(document)) {","SecurityLogs.MakeLogEntry(EnumPermType.ImageEdit,document.PatNum");
			AssertBefore(body,"if(!ImageSignaturePermissions.IsAuthorizedForSigned(document)) {","document.DocCategory=e.DefNumNew;");
			AssertBefore(body,"Documents.Update(document,docOld);","ImageSignaturePermissions.LogSignedImageEdited(document,\"moved to category \"+docNewCat);");
			AssertBefore(body,"Documents.GetDocumentsForMountItems(MountItems.GetItemsForMount(mountShowing.MountNum))",
				"if(!ImageSignaturePermissions.IsAuthorizedForSigned(documentArrayMountItems)) {");
			AssertBefore(body,"if(!ImageSignaturePermissions.IsAuthorizedForSigned(documentArrayMountItems)) {","mountShowing.DocCategory=e.DefNumNew;");
			AssertBefore(body,"Documents.UpdateDocCategoryForMountItems(","ImageSignaturePermissions.LogSignedImageEdited(documentArrayMountItems[i],");
			Assert.AreEqual(2,Count(body,"FillImageSelector(true);//The drag snaps back."),"A refused drag refills the selector.");
			//The old Images module (tree drag and drop) has the same rule for documents and for mounts.
			string bodyOld=MethodBody(Read("OpenDental","Main Modules","ControlImagesOld.cs"),
				"private void TreeDocuments_MouseUp(object sender,System.Windows.Forms.MouseEventArgs e)");
			AssertBefore(bodyOld,"if(!ImageSignaturePermissions.IsAuthorizedForSigned(doc)) {","doc.DocCategory=nodeOverCategoryDefNum;");
			AssertBefore(bodyOld,"Documents.Update(doc);","ImageSignaturePermissions.LogSignedImageEdited(doc,\"moved to category \"+docDestCat);");
			AssertBefore(bodyOld,"if(!ImageSignaturePermissions.IsAuthorizedForSigned(documentArrayMountItems)) {","mount.DocCategory=nodeOverCategoryDefNum;");
			AssertBefore(bodyOld,"Mounts.Update(mount);","ImageSignaturePermissions.LogSignedImageEdited(documentArrayMountItems[i],");
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

		///<summary>The shared helper makes the checks of the Edit Preauthorization Delete button, with the same messages, in the same order.</summary>
		[TestMethod]
		public void PreAuthEobDeleteHelper_SameChecksAndMessagesAsEditPreAuth() {
			string helper=MethodBody(Read("OpenDental","Logic","ClaimL.cs"),"public static bool TryDeletePreAuthEobs(Claim claim)");
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
			string formClaimEdit=Read("OpenDental","Forms","FormClaimEdit.cs");
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

		///<summary>The two other windows that delete a claim run the helper after their own refusals and confirmation, and before any claimproc or
		///the claim is deleted. A false from the helper ends the delete.</summary>
		[TestMethod]
		public void DeleteClaimElsewhere_PreAuthEobsGoFirst() {
			string send=MethodBody(Read("OpenDental","Forms","FormClaimsSend.cs"),"private void menuItemDeleteClaim_Click(object sender,EventArgs e)");
			Assert.AreEqual(1,Count(send,"ClaimL.TryDeletePreAuthEobs("));
			Assert.IsTrue(Regex.IsMatch(send,@"if\(!ClaimL\.TryDeletePreAuthEobs\(claim\)\) \{\s*return;\s*\}"),"A false ends the delete.");
			AssertBefore(send,"Cannot delete, there are claim procedures attached to this claim.","ClaimL.TryDeletePreAuthEobs(claim)");
			AssertBefore(send,"ClaimL.TryDeletePreAuthEobs(claim)","ClaimProcs.DeleteMany(listClaimProcs);");
			AssertBefore(send,"ClaimL.TryDeletePreAuthEobs(claim)","Claims.Delete(claim,");
			//The institutional claim window (kept in the source tree, not in OpenDental.csproj).
			string inst=MethodBody(Read("OpenDental","Forms","FormClaimInstEdit.cs"),"private void butDelete_Click(object sender, System.EventArgs e)");
			Assert.AreEqual(1,Count(inst,"ClaimL.TryDeletePreAuthEobs("));
			Assert.IsTrue(Regex.IsMatch(inst,@"if\(!ClaimL\.TryDeletePreAuthEobs\(ClaimCur\)\) \{\s*return;\s*\}"),"A false ends the delete.");
			AssertBefore(inst,"Delete PreAuthorization?","ClaimL.TryDeletePreAuthEobs(ClaimCur)");
			AssertBefore(inst,"Delete Claim?","ClaimL.TryDeletePreAuthEobs(ClaimCur)");
			AssertBefore(inst,"ClaimL.TryDeletePreAuthEobs(ClaimCur)","ClaimProcs.Delete(");
			AssertBefore(inst,"ClaimL.TryDeletePreAuthEobs(ClaimCur)","Claims.Delete(ClaimCur);");
		}
	}
}
