// V-EDS: what a validation means for sending (rules 3 and 5), re-validation, the Missing Info text, gates and the last-result display (rule 4).
// No database, no network.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.VendorDirect.Eds;

namespace VEDSTests {
	[TestClass]
	public class EdsDecisionTests {
		[TestInitialize]
		public void TestInitialize() {
			EdsTestDb.SetCulture();
			EdsClaimValidation.ClearLastResults();
		}

		[TestCleanup]
		public void TestCleanup() {
			EdsClaimValidation.ClearLastResults();
		}

		private static EdsValidationIssue Issue(EdsIssueSeverity severity,EdsIssueSource source,string checkId,string message) {
			return new EdsValidationIssue { Severity=severity,Source=source,CheckId=checkId,Location="Claim",Message=message };
		}

		private static EdsValidationResult Result(long claimNum,params EdsValidationIssue[] arrayIssues) {
			return new EdsValidationResult { ClaimNum=claimNum,ListIssues=arrayIssues.ToList(),DateTimeValidated=new DateTime(2026,10,20,9,30,0) };
		}

		[TestMethod]
		public void Decision_ErrorsBlock_WarningsAndUnreachableConfirm() {
			Assert.AreEqual(EdsSendDecision.Send,EdsClaimValidation.GetSendDecision(null));
			Assert.AreEqual(EdsSendDecision.Send,EdsClaimValidation.GetSendDecision(Result(1)));
			Assert.AreEqual(EdsSendDecision.Send,EdsClaimValidation.GetSendDecision(new EdsValidationResult { SkippedReason="Not EDS" }));
			Assert.AreEqual(EdsSendDecision.ConfirmWarnings,
				EdsClaimValidation.GetSendDecision(Result(1,Issue(EdsIssueSeverity.Warning,EdsIssueSource.Eds,"A","w"))));
			Assert.AreEqual(EdsSendDecision.Block,EdsClaimValidation.GetSendDecision(Result(1,
				Issue(EdsIssueSeverity.Warning,EdsIssueSource.Eds,"A","w"),Issue(EdsIssueSeverity.Error,EdsIssueSource.Local,"B","e"))));
			EdsValidationResult resultUnreachable=Result(1,Issue(EdsIssueSeverity.Warning,EdsIssueSource.Local,"A","w"));
			resultUnreachable.CallFailedMessage="EDS could not be reached: timeout";
			Assert.AreEqual(EdsSendDecision.ConfirmUnreachable,EdsClaimValidation.GetSendDecision(resultUnreachable),"Never blocks silently.");
			EdsValidationResult resultUnreachableWithError=Result(1,Issue(EdsIssueSeverity.Error,EdsIssueSource.Local,"A","e"));
			resultUnreachableWithError.CallFailedMessage="x";
			Assert.AreEqual(EdsSendDecision.Block,EdsClaimValidation.GetSendDecision(resultUnreachableWithError));
			EdsValidationIssue issueResolved=Issue(EdsIssueSeverity.Error,EdsIssueSource.Eds,"A","e");
			issueResolved.IsResolved=true;
			Assert.AreEqual(EdsSendDecision.Send,EdsClaimValidation.GetSendDecision(Result(1,issueResolved)),"Resolved problems do not count.");
		}

		[TestMethod]
		public void ClaimNumsToSend_DependOnSendAnyway() {
			EdsValidationResult resultClean=Result(1);
			EdsValidationResult resultWarning=Result(2,Issue(EdsIssueSeverity.Warning,EdsIssueSource.Eds,"A","w"));
			EdsValidationResult resultError=Result(3,Issue(EdsIssueSeverity.Error,EdsIssueSource.Local,"B","e"));
			EdsValidationResult resultUnreachable=Result(4);
			resultUnreachable.CallFailedMessage="down";
			List<EdsValidationResult> listResults=new List<EdsValidationResult> { resultClean,resultWarning,resultError,resultUnreachable };
			CollectionAssert.AreEqual(new long[] { 1 },EdsClaimValidation.GetClaimNumsToSend(listResults,false));
			CollectionAssert.AreEqual(new long[] { 1,2,4 },EdsClaimValidation.GetClaimNumsToSend(listResults,true));
		}

		[TestMethod]
		public void NotSendable_IsNeverSent_EvenWithSendAnyway() {
			EdsValidationResult resultDeleted=EdsClaimValidation.MakeNotSendableResult(null,7,"The claim has been deleted.");
			Assert.IsTrue(resultDeleted.IsNotSendable);
			Assert.IsFalse(resultDeleted.IsSkipped);
			Assert.AreEqual(7,resultDeleted.ClaimNum);
			Assert.AreEqual(EdsSendDecision.Block,EdsClaimValidation.GetSendDecision(resultDeleted));
			Assert.IsTrue(resultDeleted.NeedsAttention,"Listed in the validation window.");
			Assert.AreEqual(EdsClaimChecks.CHECK_CLAIM_NOT_SENDABLE,resultDeleted.ListIssues.Single().CheckId);
			//The flag decides, whatever else the result says.
			EdsValidationResult resultFlagOnly=new EdsValidationResult { ClaimNum=8,NotSendableReason="Sent.",SkippedReason="Not EDS" };
			Assert.AreEqual(EdsSendDecision.Block,EdsClaimValidation.GetSendDecision(resultFlagOnly));
			List<EdsValidationResult> listResults=new List<EdsValidationResult> { Result(1),resultDeleted,resultFlagOnly,
				Result(2,Issue(EdsIssueSeverity.Warning,EdsIssueSource.Eds,"A","w")) };
			CollectionAssert.AreEqual(new long[] { 1 },EdsClaimValidation.GetClaimNumsToSend(listResults,false));
			CollectionAssert.AreEqual(new long[] { 1,2 },EdsClaimValidation.GetClaimNumsToSend(listResults,true));
			EdsClaimValidation.SetLastResult(resultDeleted);
			Assert.IsNull(EdsClaimValidation.GetLastResult(7),"Not kept as the claim's last validation.");
			Assert.IsFalse(EdsClaimValidation.IsWaitingToSend(null));
			Assert.IsTrue(EdsClaimValidation.IsWaitingToSend(new Claim { ClaimStatus="W" }));
			Assert.IsTrue(EdsClaimValidation.IsWaitingToSend(new Claim { ClaimStatus="P" }));
			foreach(string status in new[] { "U","H","S","R","I" }) {
				Assert.IsFalse(EdsClaimValidation.IsWaitingToSend(new Claim { ClaimStatus=status }),status);
			}
		}

		[TestMethod]
		public void Revalidation_NotSendable_KeepsTheHeader_AndResolvesNothing() {
			EdsValidationResult resultPrevious=Result(3,Issue(EdsIssueSeverity.Warning,EdsIssueSource.Local,"A","w"));
			resultPrevious.PatNum=11;
			resultPrevious.PatientName="Synthetic, Pat";
			resultPrevious.DateService=new DateTime(2026,10,1);
			resultPrevious.CarrierName="Synthetic Carrier";
			EdsValidationResult resultMerged=EdsClaimValidation.MergeRevalidation(resultPrevious,
				EdsClaimValidation.MakeNotSendableResult(null,3,"The claim has been deleted."));
			Assert.IsTrue(resultMerged.IsNotSendable);
			Assert.AreEqual("Synthetic, Pat",resultMerged.PatientName);
			Assert.AreEqual(new DateTime(2026,10,1),resultMerged.DateService);
			Assert.AreEqual("Synthetic Carrier",resultMerged.CarrierName);
			Assert.AreEqual(EdsClaimChecks.CHECK_CLAIM_NOT_SENDABLE,resultMerged.ListIssues.Single().CheckId,"The earlier warning is not shown as resolved.");
			Assert.AreEqual(EdsSendDecision.Block,EdsClaimValidation.GetSendDecision(resultMerged));
		}

		[TestMethod]
		public void Revalidation_MarksProblemsThatWentAwayResolved() {
			EdsValidationResult resultPrevious=Result(1,Issue(EdsIssueSeverity.Error,EdsIssueSource.Local,"A","first"),
				Issue(EdsIssueSeverity.Warning,EdsIssueSource.Eds,"B","second"));
			resultPrevious.IsRemoteChecked=true;
			EdsValidationResult resultLatest=Result(1,Issue(EdsIssueSeverity.Warning,EdsIssueSource.Eds,"B","second"),
				Issue(EdsIssueSeverity.Warning,EdsIssueSource.Eds,"C","new"));
			resultLatest.IsRemoteChecked=true;
			EdsValidationResult resultMerged=EdsClaimValidation.MergeRevalidation(resultPrevious,resultLatest);
			Assert.AreEqual(3,resultMerged.ListIssues.Count);
			Assert.IsTrue(resultMerged.ListIssues.Single(x => x.CheckId=="A").IsResolved);
			Assert.IsFalse(resultMerged.ListIssues.Single(x => x.CheckId=="B").IsResolved);
			Assert.IsFalse(resultMerged.ListIssues.Single(x => x.CheckId=="C").IsResolved);
			Assert.AreEqual(EdsSendDecision.ConfirmWarnings,EdsClaimValidation.GetSendDecision(resultMerged));
		}

		[TestMethod]
		public void Revalidation_WhenEdsIsUnreachable_KeepsEdsProblems() {
			EdsValidationResult resultPrevious=Result(1,Issue(EdsIssueSeverity.Warning,EdsIssueSource.Eds,"B","attachment"),
				Issue(EdsIssueSeverity.Warning,EdsIssueSource.Local,"L","payer list"));
			resultPrevious.IsRemoteChecked=true;
			EdsValidationResult resultLatest=Result(1);
			resultLatest.CallFailedMessage="EDS could not be reached: timeout";
			EdsValidationResult resultMerged=EdsClaimValidation.MergeRevalidation(resultPrevious,resultLatest);
			Assert.IsFalse(resultMerged.ListIssues.Single(x => x.CheckId=="B").IsResolved,"Nothing new is known about EDS's problem.");
			Assert.IsTrue(resultMerged.ListIssues.Single(x => x.CheckId=="L").IsResolved,"The local check ran again and passed.");
			Assert.IsTrue(resultMerged.IsCallFailed);
		}

		[TestMethod]
		public void Revalidation_ResolvedProblemsDoNotPileUp() {
			EdsValidationResult result1=Result(1,Issue(EdsIssueSeverity.Error,EdsIssueSource.Local,"A","x"));
			EdsValidationResult result2=EdsClaimValidation.MergeRevalidation(result1,Result(1));
			EdsValidationResult result3=EdsClaimValidation.MergeRevalidation(result2,Result(1));
			Assert.AreEqual(1,result2.ListIssues.Count);
			Assert.AreEqual(0,result3.ListIssues.Count,"A problem already resolved is not carried again.");
		}

		[TestMethod]
		public void MissingData_EdsPartRoundTrips() {
			List<EdsValidationIssue> listIssues=new List<EdsValidationIssue> {
				new EdsValidationIssue { Severity=EdsIssueSeverity.Error,CheckId="A",Location="Billing provider (BILL)",Message="The NPI is bad." },
				new EdsValidationIssue { Severity=EdsIssueSeverity.Warning,CheckId="B",Location="Carrier",Message="Not on the list." },
				new EdsValidationIssue { Severity=EdsIssueSeverity.Error,CheckId="C",Location="",Message="Date in the future." },
			};
			string part=EdsClaimValidation.FormatErrorsForMissingData(listIssues);
			Assert.AreEqual("EDS: Billing provider (BILL): The NPI is bad., EDS: Date in the future.",part);
			Assert.AreEqual("EDS: Carrier: Not on the list.",EdsClaimValidation.FormatWarningsForQueue(listIssues));
			Assert.AreEqual("SubscriberID",EdsClaimValidation.GetMissingDataWithoutEds("SubscriberID, "+part,listIssues));
			Assert.AreEqual("",EdsClaimValidation.GetMissingDataWithoutEds(part,listIssues));
			Assert.AreEqual("SubscriberID",EdsClaimValidation.GetMissingDataWithoutEds("SubscriberID",new List<EdsValidationIssue>()));
			Assert.AreEqual("",EdsClaimValidation.GetMissingDataWithoutEds(null,null));
		}

		[TestMethod]
		public void QueueItem_LocalChecksOnlyForEds() {
			Clearinghouse clearinghouseOther=new Clearinghouse { CommBridge=EclaimsCommBridge.ClaimConnect };
			ClaimSendQueueItem queueItem=new ClaimSendQueueItem { ClaimNum=1,MissingData="",Warnings="" };
			EdsClaimValidation.AddLocalChecksToQueueItem(clearinghouseOther,queueItem);//Returns before reading the database.
			Assert.AreEqual("",queueItem.MissingData);
			EdsClaimValidation.AddLocalChecksToQueueItem(null,queueItem);
			EdsClaimValidation.AddLocalChecksToQueueItem(new Clearinghouse { CommBridge=EclaimsCommBridge.EDS },null);
		}

		[TestMethod]
		public void Gates_RemoteNeedsAttachmentServiceAndLogin() {
			Clearinghouse clearinghouse=new Clearinghouse { CommBridge=EclaimsCommBridge.EDS,IsAttachmentSendAllowed=true,LoginID="office",Password="secret" };
			Assert.IsTrue(EdsClaimValidation.IsEds(clearinghouse));
			Assert.IsTrue(EdsClaimValidation.IsRemoteValidationOn(clearinghouse));
			Assert.AreEqual("",EdsClaimValidation.GetRemoteOffReason(clearinghouse));
			clearinghouse.IsAttachmentSendAllowed=false;
			Assert.IsFalse(EdsClaimValidation.IsRemoteValidationOn(clearinghouse),"Off by default: the attachment service box starts unchecked.");
			StringAssert.Contains(EdsClaimValidation.GetRemoteOffReason(clearinghouse),"attachment service");
			clearinghouse.IsAttachmentSendAllowed=true;
			clearinghouse.Password="";
			Assert.IsFalse(EdsClaimValidation.IsRemoteValidationOn(clearinghouse));
			StringAssert.Contains(EdsClaimValidation.GetRemoteOffReason(clearinghouse),"password");
			clearinghouse.Password="secret";
			clearinghouse.CommBridge=EclaimsCommBridge.EdsMedical;
			Assert.IsFalse(EdsClaimValidation.IsEds(clearinghouse),"EDS medical claims are not part of this feature.");
			Assert.IsFalse(EdsClaimValidation.IsRemoteValidationOn(clearinghouse));
			Assert.IsFalse(EdsClaimValidation.IsEds(null));
		}

		[TestMethod]
		public void ClaimTypes_Validated() {
			Assert.IsTrue(EdsClaimValidation.ShouldValidate(new Claim { ClaimType="P" }));
			Assert.IsTrue(EdsClaimValidation.ShouldValidate(new Claim { ClaimType="S" }));
			Assert.IsTrue(EdsClaimValidation.ShouldValidate(new Claim { ClaimType="Other" }));
			Assert.IsTrue(EdsClaimValidation.ShouldValidate(new Claim { ClaimType="PreAuth" }));
			Assert.IsFalse(EdsClaimValidation.ShouldValidate(new Claim { ClaimType="Cap" }));
			Assert.IsFalse(EdsClaimValidation.ShouldValidate(null));
		}

		[TestMethod]
		public void CanAskEds_OnlyWithoutErrors() {
			Clearinghouse clearinghouse=new Clearinghouse { CommBridge=EclaimsCommBridge.EDS,IsAttachmentSendAllowed=true,LoginID="office",Password="secret" };
			Assert.IsTrue(EdsClaimValidation.CanAskEds(Result(1,Issue(EdsIssueSeverity.Warning,EdsIssueSource.Local,"A","w")),clearinghouse));
			Assert.IsFalse(EdsClaimValidation.CanAskEds(Result(1,Issue(EdsIssueSeverity.Error,EdsIssueSource.Local,"A","e")),clearinghouse));
			Assert.IsFalse(EdsClaimValidation.CanAskEds(new EdsValidationResult { SkippedReason="x" },clearinghouse));
			Assert.IsFalse(EdsClaimValidation.CanAskEds(null,clearinghouse));
		}

		[TestMethod]
		public void CallFailedMessage_SaysEdsWasUnreachable() {
			StringAssert.StartsWith(EdsClaimValidation.GetCallFailedMessage(new TimeoutException("EDS did not answer within 30 seconds.")),
				"EDS could not be reached: EDS did not answer");
			StringAssert.Contains(EdsClaimValidation.GetCallFailedMessage(new AggregateException(new InvalidOperationException("inner"))),"inner");
			StringAssert.Contains(EdsClaimValidation.GetCallFailedMessage(new Newtonsoft.Json.JsonReaderException("bad token")),"could not be read");
			StringAssert.Contains(EdsClaimValidation.GetCallFailedMessage(new Exception("")),"validation call failed");
		}

		[TestMethod]
		public void Summary_CountsOpenProblems() {
			Assert.AreEqual("",EdsClaimValidation.GetSummary(null));
			Assert.AreEqual("",EdsClaimValidation.GetSummary(new EdsValidationResult { SkippedReason="x" }));
			Assert.AreEqual("The local EDS checks passed.",EdsClaimValidation.GetSummary(Result(1)));
			EdsValidationResult resultRemote=Result(1);
			resultRemote.IsRemoteChecked=true;
			Assert.AreEqual("EDS validation passed.",EdsClaimValidation.GetSummary(resultRemote));
			EdsValidationResult result=Result(1,Issue(EdsIssueSeverity.Error,EdsIssueSource.Local,"A","e"),
				Issue(EdsIssueSeverity.Warning,EdsIssueSource.Eds,"B","w1"),Issue(EdsIssueSeverity.Warning,EdsIssueSource.Eds,"C","w2"));
			result.CallFailedMessage="EDS could not be reached: down";
			Assert.AreEqual("EDS errors: 1; EDS warnings: 2; EDS could not be reached: down",EdsClaimValidation.GetSummary(result));
		}

		[TestMethod]
		public void LastResult_IsKeptPerClaimAndShownAsText() {
			Assert.AreEqual("",EdsClaimValidation.GetLastValidationText(5));
			EdsValidationResult result=Result(5,Issue(EdsIssueSeverity.Warning,EdsIssueSource.Eds,"B","Attachment needed."));
			result.RemoteNote="A note.";
			EdsClaimValidation.SetLastResult(result);
			result.ListIssues.Clear();//The kept copy does not change with the caller's object.
			string text=EdsClaimValidation.GetLastValidationText(5);
			StringAssert.StartsWith(text,"Last EDS validation: ");
			StringAssert.Contains(text,"EDS warnings: 1");
			StringAssert.Contains(text,"A note.");
			StringAssert.Contains(text,"Warning: Claim: Attachment needed.");
			EdsClaimValidation.SetLastResult(new EdsValidationResult { ClaimNum=6,SkippedReason="Not EDS" });
			Assert.IsNull(EdsClaimValidation.GetLastResult(6),"Skipped results are not kept.");
			EdsValidationResult resultCopy=EdsClaimValidation.GetLastResult(5);
			resultCopy.ListIssues.Clear();
			Assert.AreEqual(1,EdsClaimValidation.GetLastResult(5).ListIssues.Count);
		}

		[TestMethod]
		public void IssueOrder_CollapsesDuplicates() {
			List<EdsValidationIssue> listIssues=EdsClaimChecks.Order(new List<EdsValidationIssue> {
				Issue(EdsIssueSeverity.Warning,EdsIssueSource.Eds,"B","w"),
				Issue(EdsIssueSeverity.Error,EdsIssueSource.Local,"A","e"),
				Issue(EdsIssueSeverity.Warning,EdsIssueSource.Eds,"B","w"),
			});
			Assert.AreEqual(2,listIssues.Count);
			Assert.AreEqual("A",listIssues[0].CheckId);
			Assert.AreEqual(0,EdsClaimChecks.Order(null).Count);
		}
	}
}
