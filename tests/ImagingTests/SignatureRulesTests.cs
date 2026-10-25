// 24.4 v24_4-imaging-signature-permissions: the bare rules for Image Signature Create (264) and Signed Image Edit (265). No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using static OpenDentBusiness.ImageSignaturePermissions;

namespace ImagingTests {
	[TestClass]
	public class SignatureRulesTests {
		private static Document Doc(string signature) {
			return new Document { Signature=signature,Note="" };
		}

		[TestMethod]
		public void PermissionNumbers_AreThePublished264And265() {
			Assert.AreEqual(264,(int)EnumPermType.ImageSignatureCreate);
			Assert.AreEqual(265,(int)EnumPermType.SignedImageEdit);
		}

		[TestMethod]
		public void NewPermissions_HaveNoDateRestriction_ImageEditAndDeleteKeepTheirs() {
			Assert.IsFalse(GroupPermissions.PermTakesDates(EnumPermType.ImageSignatureCreate));
			Assert.IsFalse(GroupPermissions.PermTakesDates(EnumPermType.SignedImageEdit));
			Assert.IsTrue(GroupPermissions.PermTakesDates(EnumPermType.ImageEdit));
			Assert.IsTrue(GroupPermissions.PermTakesDates(EnumPermType.ImageDelete));
		}

		[TestMethod]
		public void Signed_MeansSignatureNotEmpty() {
			Assert.IsFalse(IsSigned(null));
			Assert.IsFalse(IsSigned(Doc(null)));
			Assert.IsFalse(IsSigned(Doc("")));
			Assert.IsTrue(IsSigned(Doc("sig")));
			Assert.IsFalse(IsSigned(new Document { Signature="",Note="A note alone is not a signature" }));
		}

		[TestMethod]
		public void ChartLetters_AreNeverSignedImages() {
			//The spec: chart letters are not images, so the two signature permissions don't apply to them.
			Document chartLetter=new Document { Signature="sig",Note="",ChartLetterStatus=1 };
			Assert.IsFalse(IsSigned(chartLetter));
			Assert.IsFalse(IsAnySigned(new Document[] { chartLetter }));
		}

		[TestMethod]
		public void Mount_IsSignedWhenAnyItemIs_EmptyPositionsSkipped() {
			Assert.IsFalse(IsAnySigned(null));
			Assert.IsFalse(IsAnySigned(new Document[] { null,Doc(""),null }));
			Assert.IsTrue(IsAnySigned(new Document[] { null,Doc(""),Doc("sig") }));
		}

		[TestMethod]
		public void SignButton_NeedsCreateWhenUnsigned_SignedEditWhenSigned() {
			Assert.AreEqual(EnumPermType.ImageSignatureCreate,GetSignPermission(Doc("")));
			Assert.AreEqual(EnumPermType.SignedImageEdit,GetSignPermission(Doc("sig")));
		}

		///<summary>Every combination: signed or not, with or without each permission.</summary>
		[TestMethod]
		public void PermissionMatrix_SignEditDelete() {
			foreach(bool isSigned in new[] { false,true }) {
				foreach(bool hasCreate in new[] { false,true }) {
					foreach(bool hasSignedEdit in new[] { false,true }) {
						foreach(bool hasBase in new[] { false,true }) {
							string label="signed="+isSigned+" create="+hasCreate+" signedEdit="+hasSignedEdit+" base="+hasBase;
							Assert.AreEqual(isSigned ? hasSignedEdit : hasCreate,CanSign(isSigned,hasCreate,hasSignedEdit),"Sign, "+label);
							Assert.AreEqual(hasBase && (!isSigned || hasSignedEdit),CanEdit(isSigned,hasBase,hasSignedEdit),"Edit, "+label);
							Assert.AreEqual(hasBase && (!isSigned || hasSignedEdit),CanDelete(isSigned,hasBase,hasSignedEdit),"Delete, "+label);
						}
					}
				}
			}
		}

		[TestMethod]
		public void SignedImageEdit_NeverReplacesImageEditOrImageDelete() {
			Assert.IsFalse(CanEdit(isSigned:true,hasImageEdit:false,hasSignedImageEdit:true));
			Assert.IsFalse(CanDelete(isAnySigned:true,hasImageDelete:false,hasSignedImageEdit:true));
			Assert.IsTrue(CanEdit(isSigned:false,hasImageEdit:true,hasSignedImageEdit:false),"Unsigned images are unchanged by 24.4.");
			Assert.IsTrue(CanDelete(isAnySigned:false,hasImageDelete:true,hasSignedImageEdit:false));
		}

		[TestMethod]
		public void SignatureChange_EveryCase() {
			Assert.AreEqual(SignatureChange.None,GetSignatureChange("","","",""));
			Assert.AreEqual(SignatureChange.None,GetSignatureChange(null,"",null,""),"Null and empty are the same.");
			Assert.AreEqual(SignatureChange.NoteChanged,GetSignatureChange("","","","new note"));
			Assert.AreEqual(SignatureChange.Added,GetSignatureChange("","sig","","note"));
			Assert.AreEqual(SignatureChange.Removed,GetSignatureChange("sig","","note","note"));
			Assert.AreEqual(SignatureChange.Changed,GetSignatureChange("sig","sig2","note","note"));
			Assert.AreEqual(SignatureChange.Changed,GetSignatureChange("sig","sig","note","note 2"),"A new note on a signed image changes it.");
			Assert.AreEqual(SignatureChange.None,GetSignatureChange("sig","sig","note","note"));
		}

		[TestMethod]
		public void SignatureChange_LoggedUnderTheRightPermission() {
			Assert.AreEqual(EnumPermType.ImageSignatureCreate,GetSignatureChangePermission(SignatureChange.Added));
			Assert.AreEqual(EnumPermType.ImageSignatureCreate,GetSignatureChangePermission(SignatureChange.NoteChanged));
			Assert.AreEqual(EnumPermType.SignedImageEdit,GetSignatureChangePermission(SignatureChange.Changed));
			Assert.AreEqual(EnumPermType.SignedImageEdit,GetSignatureChangePermission(SignatureChange.Removed));
			Assert.AreEqual("Signature added",GetSignatureChangeLogText(SignatureChange.Added));
			Assert.AreEqual("Signature changed",GetSignatureChangeLogText(SignatureChange.Changed));
			Assert.AreEqual("Signature removed",GetSignatureChangeLogText(SignatureChange.Removed));
			Assert.AreEqual("",GetSignatureChangeLogText(SignatureChange.None));
		}

		[TestMethod]
		public void RefusalMessage_NamesTheMissingPermission() {
			string text=GetNotAuthorizedText(EnumPermType.SignedImageEdit);
			StringAssert.Contains(text,"Signed Image Edit");
			StringAssert.Contains(text,"signed");
			StringAssert.Contains(GetNotAuthorizedText(EnumPermType.ImageSignatureCreate),"Image Signature Create");
		}

		[TestMethod]
		public void PreAuthEobLogText_SaysPreauthEob() {
			StringAssert.Contains(ImageStore.GetPreAuthEobLogText(EnumPermType.ImageCreate,"20261024_1.pdf"),"Preauth EOB attached: 20261024_1.pdf");
			StringAssert.Contains(ImageStore.GetPreAuthEobLogText(EnumPermType.ImageDelete,"20261024_1.pdf"),"Preauth EOB deleted");
		}

		[TestMethod]
		public void EobOwner_ExactlyOneOfPaymentOrPreAuth() {
			Assert.IsTrue(EobAttaches.HasValidOwner(new EobAttach { ClaimPaymentNum=5 }));
			Assert.IsTrue(EobAttaches.HasValidOwner(new EobAttach { ClaimNumPreAuth=7 }));
			Assert.IsFalse(EobAttaches.HasValidOwner(new EobAttach()),"Neither.");
			Assert.IsFalse(EobAttaches.HasValidOwner(new EobAttach { ClaimPaymentNum=5,ClaimNumPreAuth=7 }),"Both.");
			Assert.IsFalse(EobAttaches.HasValidOwner(new EobAttach { ClaimPaymentNum=-1,ClaimNumPreAuth=7 }));
			Assert.IsFalse(EobAttaches.HasValidOwner(null));
		}

		[TestMethod]
		public void PreAuthEstimatesWindow_FindsThePreAuthFromItsClaimProcs() {
			Assert.AreEqual(0,EobAttaches.GetClaimNumPreAuth(null));
			Assert.AreEqual(0,EobAttaches.GetClaimNumPreAuth(new List<ClaimProc>()));
			Assert.AreEqual(42,EobAttaches.GetClaimNumPreAuth(new List<ClaimProc> { null,new ClaimProc { ClaimNum=0 },new ClaimProc { ClaimNum=42 } }));
		}

		///<summary>Only a preauthorization takes EOBs with it when it is deleted. Every other claim is let through at once, without a query.</summary>
		[TestMethod]
		public void ClaimDelete_OnlyAPreAuthTakesEobsWithIt() {
			Assert.AreEqual(0,EobAttaches.GetForClaimDelete(null).Count);
			foreach(string claimType in new[] { "P","S","Tertiary","Med","Cap","Other","" }) {
				Assert.AreEqual(0,EobAttaches.GetForClaimDelete(new Claim { ClaimNum=42,ClaimType=claimType }).Count,"ClaimType "+claimType);
			}
			Assert.AreEqual(0,EobAttaches.GetForClaimDelete(new Claim { ClaimNum=0,ClaimType="PreAuth" }).Count,"An unsaved preauth has none.");
		}

		[TestMethod]
		public void ClaimDelete_NoEobsNeedsNoImageDelete() {
			Assert.IsTrue(EobAttaches.IsAuthorizedToDeleteWithPreAuth(null));
			Assert.IsTrue(EobAttaches.IsAuthorizedToDeleteWithPreAuth(new List<EobAttach>()));
		}
	}
}
