// Tests for the claim.AttachedFlags rules of the DentalXChange "Submitted" flag (Open Dental 24.4, v24_4-claims-dxc-attachment-submitted).
// No database. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace VendorDirectTests.DentalXChange {
	[TestClass]
	public class ClaimAttachedFlagsTests {
		[TestMethod]
		public void ParseAttachedFlags_TrimsDropsBlanksAndDuplicates() {
			CollectionAssert.AreEqual(new[] { "Misc","Submitted" },ClaimAttaches.ParseAttachedFlags(" Misc,,Submitted ,Misc").ToArray());
			Assert.AreEqual(0,ClaimAttaches.ParseAttachedFlags(null).Count);
			Assert.AreEqual(0,ClaimAttaches.ParseAttachedFlags("").Count);
		}

		[TestMethod]
		public void RebuildFromCheckBoxes_KeepsKeywordsTheCheckBoxesDoNotManage() {
			//The check boxes now say EoB and Elect; the old Misc and Mail go, Submitted and Unsup stay, an unknown keyword stays.
			string flags=ClaimAttaches.RebuildFromCheckBoxes("Misc,Mail,Submitted,Unsup,Future",new List<string> { "EoB","Elect" });
			Assert.AreEqual("EoB,Elect,Submitted,Unsup,Future",flags);
		}

		[TestMethod]
		public void RebuildFromCheckBoxes_DuplicateMiscFromAttachmentIdIsWrittenOnce() {
			//Edit Claim adds Misc for the Misc box and again when an attachment ID is typed.
			Assert.AreEqual("Misc,Mail",ClaimAttaches.RebuildFromCheckBoxes("",new List<string> { "Misc","Mail","Misc" }));
		}

		[TestMethod]
		public void MarkDxcAttachmentSubmitted_DropsMailAddsMiscAndSubmitted() {
			Assert.AreEqual("Misc,Submitted",ClaimAttaches.MarkDxcAttachmentSubmitted("Mail"));
			Assert.AreEqual("Misc,Submitted",ClaimAttaches.MarkDxcAttachmentSubmitted(""));
			Assert.AreEqual("Misc,EoB,Unsup,Submitted",ClaimAttaches.MarkDxcAttachmentSubmitted("EoB,Unsup"));
			//Idempotent.
			Assert.AreEqual("Misc,Submitted",ClaimAttaches.MarkDxcAttachmentSubmitted("Misc,Submitted"));
		}

		[TestMethod]
		public void ClearDxcAttachmentSubmitted_RemovesOnlySubmitted() {
			Assert.AreEqual("Misc,Unsup",ClaimAttaches.ClearDxcAttachmentSubmitted("Misc,Submitted,Unsup"));
			Assert.AreEqual("Mail",ClaimAttaches.ClearDxcAttachmentSubmitted("Mail"));
		}

		[TestMethod]
		public void SyncDxcSubmittedWithAttachments_ClearsWhenEveryAttachmentIsGone() {
			Assert.AreEqual("Misc",ClaimAttaches.SyncDxcSubmittedWithAttachments("Misc,Submitted",new List<ClaimAttach>()));
			Assert.AreEqual("Misc",ClaimAttaches.SyncDxcSubmittedWithAttachments("Misc,Submitted",null));
			Assert.AreEqual("Misc,Submitted",ClaimAttaches.SyncDxcSubmittedWithAttachments("Misc,Submitted",new List<ClaimAttach> { new ClaimAttach() }));
		}

		[TestMethod]
		public void IsDxcAttachmentSubmitted_MatchesTheWholeKeyword() {
			Assert.IsTrue(ClaimAttaches.IsDxcAttachmentSubmitted("Misc,Submitted"));
			Assert.IsTrue(ClaimAttaches.IsDxcAttachmentSubmitted("Submitted"));
			Assert.IsFalse(ClaimAttaches.IsDxcAttachmentSubmitted("Misc"));
			Assert.IsFalse(ClaimAttaches.IsDxcAttachmentSubmitted("NotSubmitted"));
			Assert.IsFalse(ClaimAttaches.IsDxcAttachmentSubmitted(null));
		}

		[TestMethod]
		public void HasPaperworkFlags_IgnoresSubmitted() {
			//The 837 builders' old test was AttachedFlags!="Mail". Submitted must not turn "Mail" into "paperwork attached".
			Assert.IsFalse(ClaimAttaches.HasPaperworkFlags("Mail"));
			Assert.IsFalse(ClaimAttaches.HasPaperworkFlags("Mail,Submitted"));
			Assert.IsFalse(ClaimAttaches.HasPaperworkFlags("Submitted"));
			Assert.IsTrue(ClaimAttaches.HasPaperworkFlags("Misc,Submitted"));
			Assert.IsTrue(ClaimAttaches.HasPaperworkFlags("Mail,EoB"));
			Assert.IsTrue(ClaimAttaches.HasPaperworkFlags("Mail,Unsup"),"Unchanged from 24.3.");
			Assert.IsTrue(ClaimAttaches.HasPaperworkFlags(""),"Unchanged from 24.3: blank is not \"Mail\".");
		}

		[TestMethod]
		public void IsAttachmentIdMissing_SkippedOnceTheAttachmentWasSubmitted() {
			Assert.IsTrue(ClaimAttaches.IsAttachmentIdMissing("Misc","",true));
			Assert.IsFalse(ClaimAttaches.IsAttachmentIdMissing("Misc,Submitted","",true));
			Assert.IsFalse(ClaimAttaches.IsAttachmentIdMissing("Misc","DXC123",true));
			Assert.IsFalse(ClaimAttaches.IsAttachmentIdMissing("Mail","",false));
		}

		[TestMethod]
		public void X837Builders_UseTheHelpers() {
			//Both builders must ask ClaimAttaches instead of comparing AttachedFlags with "Mail" themselves.
			foreach(string relativePath in new[] { @"OpenDentBusiness\X12\X837_5010.cs",@"OpenDentBusiness\X12\X837_4010.cs" }) {
				string source=DxcTestFiles.ReadRepoFile(relativePath);
				StringAssert.Contains(source,"ClaimAttaches.HasPaperworkFlags(claim.AttachedFlags)",relativePath);
				StringAssert.Contains(source,"ClaimAttaches.IsAttachmentIdMissing(claim.AttachedFlags,claim.AttachmentID,pwkNeeded)",relativePath);
				Assert.IsFalse(source.Contains("if(claim.AttachedFlags!=\"Mail\")"),relativePath);
			}
		}
	}
}
