// V-EDS: reading EDS's attachment validation answer into the claim's problem list (rules 2 and 3). Synthetic answers; no database, no network.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using OpenDentBusiness;
using OpenDentBusiness.Eclaims;
using OpenDentBusiness.VendorDirect.Eds;

namespace VEDSTests {
	[TestClass]
	public class EdsResponseTests {
		[TestInitialize]
		public void TestInitialize() {
			EdsTestDb.SetCulture();
		}

		private static EDS.ListPayerResponses Parse(string json) {
			return JsonConvert.DeserializeObject<EDS.ListPayerResponses>(json);
		}

		[TestMethod]
		public void ClaimLevelRequirement_WithoutAttachment_IsAWarning() {
			EdsClaimSnapshot snapshot=EdsTestData.MakeValidSnapshot();
			List<EdsValidationIssue> listIssues=EdsClaimChecks.GetIssuesFromAttachmentResponse(Parse(EdsTestData.MakeAttachmentAnswer(true)),snapshot);
			EdsValidationIssue issue=listIssues.Single();
			Assert.AreEqual(EdsClaimChecks.CHECK_ATTACHMENT_REQUIRED,issue.CheckId);
			Assert.AreEqual(EdsIssueSeverity.Warning,issue.Severity);
			Assert.AreEqual(EdsIssueSource.Eds,issue.Source);
			Assert.AreEqual("Claim",issue.Location);
		}

		[TestMethod]
		public void Requirement_IsMet_ByAnAttachmentIdOrSentAttachments() {
			string json=EdsTestData.MakeAttachmentAnswer(true,("Z0001",true,new[] { "Bitewings","Narrative" },null));
			EdsClaimSnapshot snapshot=EdsTestData.MakeValidSnapshot();
			snapshot.AttachmentID="EDS-ATTACH-1";
			Assert.AreEqual(0,EdsClaimChecks.GetIssuesFromAttachmentResponse(Parse(json),snapshot).Count);
			snapshot.AttachmentID="";
			snapshot.CountAttachments=2;
			Assert.AreEqual(0,EdsClaimChecks.GetIssuesFromAttachmentResponse(Parse(json),snapshot).Count);
		}

		[TestMethod]
		public void ProcedureRequirement_ListsTheDocumentsAndComment() {
			string json=EdsTestData.MakeAttachmentAnswer(false,
				("Z0001",true,new[] { "Bitewings","PeriodontalCharts" },"Synthetic comment"),
				("Z0002",false,new[] { "XRay" },null));
			List<EdsValidationIssue> listIssues=EdsClaimChecks.GetIssuesFromAttachmentResponse(Parse(json),EdsTestData.MakeValidSnapshot());
			EdsValidationIssue issue=listIssues.Single();
			Assert.AreEqual("Procedure Z0001",issue.Location);
			Assert.AreEqual(EdsIssueSeverity.Warning,issue.Severity);
			StringAssert.Contains(issue.Message,"Bitewings (Synthetic reason)");
			StringAssert.Contains(issue.Message,"Periodontal Charts (Synthetic reason)");
			StringAssert.Contains(issue.Message,"Synthetic comment");
		}

		[TestMethod]
		public void RequiredNarrative_IsItsOwnWarning_UntilTheClaimHasOne() {
			string json=EdsTestData.MakeAttachmentAnswer(false,("Z0001",true,new[] { "Narrative" },null));
			EdsClaimSnapshot snapshot=EdsTestData.MakeValidSnapshot();
			EdsValidationIssue issue=EdsClaimChecks.GetIssuesFromAttachmentResponse(Parse(json),snapshot).Single();
			Assert.AreEqual(EdsClaimChecks.CHECK_NARRATIVE_REQUIRED,issue.CheckId,"A narrative alone is not also reported as an attachment.");
			StringAssert.Contains(issue.Message,"Synthetic reason");
			snapshot.Narrative="Synthetic narrative text.";
			Assert.AreEqual(0,EdsClaimChecks.GetIssuesFromAttachmentResponse(Parse(json),snapshot).Count);
		}

		[TestMethod]
		public void NarrativeAndImages_GiveTwoWarnings() {
			string json=EdsTestData.MakeAttachmentAnswer(false,("Z0001",true,new[] { "Narrative","Periapical" },null));
			List<EdsValidationIssue> listIssues=EdsClaimChecks.GetIssuesFromAttachmentResponse(Parse(json),EdsTestData.MakeValidSnapshot());
			CollectionAssert.AreEquivalent(new[] { EdsClaimChecks.CHECK_ATTACHMENT_REQUIRED,EdsClaimChecks.CHECK_NARRATIVE_REQUIRED },
				EdsTestData.CheckIds(listIssues));
			Assert.IsFalse(listIssues.Single(x => x.CheckId==EdsClaimChecks.CHECK_ATTACHMENT_REQUIRED).Message.Contains("Narrative"));
		}

		[TestMethod]
		public void RequirementWithNoDocumentList_StillWarns() {
			string json=EdsTestData.MakeAttachmentAnswer(false,("Z0001",true,new string[0],null));
			EdsValidationIssue issue=EdsClaimChecks.GetIssuesFromAttachmentResponse(Parse(json),EdsTestData.MakeValidSnapshot()).Single();
			Assert.AreEqual(EdsClaimChecks.CHECK_ATTACHMENT_REQUIRED,issue.CheckId);
		}

		[TestMethod]
		public void NothingRequired_NoProblems() {
			string json=EdsTestData.MakeAttachmentAnswer(false,("Z0001",false,null,null),("Z0002",false,null,null));
			Assert.AreEqual(0,EdsClaimChecks.GetIssuesFromAttachmentResponse(Parse(json),EdsTestData.MakeValidSnapshot()).Count);
		}

		[TestMethod]
		public void MissingParts_AreIgnored() {
			EdsClaimSnapshot snapshot=EdsTestData.MakeValidSnapshot();
			Assert.AreEqual(0,EdsClaimChecks.GetIssuesFromAttachmentResponse(null,snapshot).Count);
			Assert.AreEqual(0,EdsClaimChecks.GetIssuesFromAttachmentResponse(Parse("{}"),snapshot).Count);
			Assert.AreEqual(0,EdsClaimChecks.GetIssuesFromAttachmentResponse(Parse("{\"payers\":[null,{\"id\":\"1\"}]}"),snapshot).Count);
			Assert.AreEqual(0,EdsClaimChecks.GetIssuesFromAttachmentResponse(Parse(EdsTestData.MakeAttachmentAnswer(true)),null).Count);
		}

		[TestMethod]
		public void NarrativeCodes_AreTheProceduresWhoseAnswerAsksForANarrative() {
			string json=EdsTestData.MakeAttachmentAnswer(false,
				("Z0001",true,new[] { "Narrative","Periapical" },null),
				("Z0002",true,new[] { "Bitewings" },null),
				("Z0003",false,new[] { "Narrative" },null),
				("z0001",true,new[] { "Narrative" },null));
			CollectionAssert.AreEqual(new[] { "Z0001" },EdsClaimChecks.GetProcCodesNarrativeRequired(Parse(json)),
				"Only response=true with a Narrative document; each code once.");
			Assert.AreEqual(0,EdsClaimChecks.GetProcCodesNarrativeRequired(null).Count);
			Assert.AreEqual(0,EdsClaimChecks.GetProcCodesNarrativeRequired(Parse("{\"payers\":[null,{\"id\":\"1\"}]}")).Count);
		}

		[TestMethod]
		public void SameRequirementFromTwoPayers_IsListedOnce() {
			string json="{\"payers\":[{\"id\":\"1\",\"claimLevelResponse\":true},{\"id\":\"2\",\"claimLevelResponse\":true}]}";
			Assert.AreEqual(1,EdsClaimChecks.GetIssuesFromAttachmentResponse(Parse(json),EdsTestData.MakeValidSnapshot()).Count);
		}
	}
}
