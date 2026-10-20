// Tests for DentalXChange XConnect claim validation (Open Dental 25.2, v25_2-account-xconnect-claim-validation): the on/off rule, turning the
// response into problems, re-validation, the send decision, and the HTTP call (fake handler, no network). No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using OpenDentBusiness;
using OpenDentBusiness.Eclaims;
using OpenDentBusiness.VendorDirect.DentalXChange;

namespace VendorDirectTests.DentalXChange {
	[TestClass]
	public class XConnectValidationTests {
		private const string API_KEY="test-xconnect-key-0001";

		[TestCleanup]
		public void TestCleanup() {
			XConnect.HttpHandlerForTests=null;
		}

		private static Clearinghouse MakeClearinghouse(bool enableXConnect=true,string locationID=API_KEY) {
			return new Clearinghouse {
				CommBridge=EclaimsCommBridge.ClaimConnect,
				EnableXConnect=enableXConnect,
				LocationID=locationID,
				LoginID="office-login",
				Password="office-password",
			};
		}

		#region IsEnabled
		[TestMethod]
		public void IsEnabled_NeedsClaimConnectTheSwitchAndTheKey() {
			Assert.IsTrue(XConnect.IsEnabled(MakeClearinghouse()));
			Assert.IsFalse(XConnect.IsEnabled(MakeClearinghouse(enableXConnect:false)),"25.2: a key alone no longer turns XConnect on.");
			Assert.IsFalse(XConnect.IsEnabled(MakeClearinghouse(locationID:" ")));
			Clearinghouse clearinghouseEds=MakeClearinghouse();
			clearinghouseEds.CommBridge=EclaimsCommBridge.EDS;
			Assert.IsFalse(XConnect.IsEnabled(clearinghouseEds));
			Assert.IsFalse(XConnect.IsEnabled(null));
		}

		[TestMethod]
		public void OverrideFields_TakesTheClinicSwitch() {
			Clearinghouse clearinghouseHq=MakeClearinghouse(enableXConnect:true);
			Clearinghouse clearinghouseClin=new Clearinghouse { ClinicNum=5,EnableXConnect=false,LocationID="" };
			Clearinghouse clearinghouseMerged=Clearinghouses.OverrideFields(clearinghouseHq,clearinghouseClin);
			Assert.IsFalse(clearinghouseMerged.EnableXConnect,"The clinic row decides, like attachment sending.");
			Assert.AreEqual(API_KEY,clearinghouseMerged.LocationID,"A blank clinic key inherits the HQ key.");
			clearinghouseClin.EnableXConnect=true;
			Assert.IsTrue(Clearinghouses.OverrideFields(MakeClearinghouse(enableXConnect:false),clearinghouseClin).EnableXConnect);
		}
		#endregion IsEnabled

		#region ShouldValidate
		[TestMethod]
		public void ShouldValidate_PrimarySecondaryTertiaryOnly() {
			Assert.IsTrue(XConnectValidation.ShouldValidate(new Claim { ClaimType="P" }));
			Assert.IsTrue(XConnectValidation.ShouldValidate(new Claim { ClaimType="S" }));
			Assert.IsTrue(XConnectValidation.ShouldValidate(new Claim { ClaimType="Other" }));
			Assert.IsFalse(XConnectValidation.ShouldValidate(new Claim { ClaimType="PreAuth" }));
			Assert.IsFalse(XConnectValidation.ShouldValidate(new Claim { ClaimType="Cap" }));
			Assert.IsFalse(XConnectValidation.ShouldValidate(null));
		}

		[TestMethod]
		public void Validate_PreauthIsSkippedWithoutACall() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler();
			XConnect.HttpHandlerForTests=fakeHttpHandler;
			XConnectValidationResult result=XConnectValidation.Validate(new Claim { ClaimNum=7,ClaimType="PreAuth" },MakeClearinghouse(),TimeSpan.FromSeconds(1));
			Assert.IsTrue(result.IsSkipped);
			Assert.AreEqual(XConnectSendDecision.Send,XConnectValidation.GetSendDecision(result));
			Assert.AreEqual(0,fakeHttpHandler.Requests.Count);
		}

		[TestMethod]
		public void Validate_XConnectOffIsSkippedWithoutACall() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler();
			XConnect.HttpHandlerForTests=fakeHttpHandler;
			XConnectValidationResult result=XConnectValidation.Validate(new Claim { ClaimNum=7,ClaimType="P" },MakeClearinghouse(enableXConnect:false),
				TimeSpan.FromSeconds(1));
			Assert.IsTrue(result.IsSkipped);
			Assert.AreEqual(0,fakeHttpHandler.Requests.Count);
		}
		#endregion ShouldValidate

		#region GetIssues
		private static XConnectWebResponse MakeResponse() {
			return new XConnectWebResponse {
				status=new XConnectResponseStatus { code=0,description="OK" },
				response=new XConnectClaimStatus {
					claimStatus=new XConnectStatus { message="" },
					dxcStatus=new XConnectDXCStatus(),
				},
			};
		}

		[TestMethod]
		public void GetIssues_NoProblems() {
			Assert.AreEqual(0,XConnectValidation.GetIssues(MakeResponse(),"").Count);
		}

		[TestMethod]
		public void GetIssues_ClaimErrorsAreErrorsWithLocationAndHint() {
			XConnectWebResponse response=MakeResponse();
			response.response.dxcStatus.claimErrors=new[] {
				new XConnectClaimError { claimPart="Billing Provider",entityId="NPI",code="E101",description="NPI is not valid",category="Provider" },
			};
			List<XConnectValidationIssue> listIssues=XConnectValidation.GetIssues(response,"");
			Assert.AreEqual(1,listIssues.Count);
			Assert.AreEqual(XConnectIssueSeverity.Error,listIssues[0].Severity);
			Assert.AreEqual("Billing Provider NPI",listIssues[0].Location);
			Assert.AreEqual("NPI is not valid (E101)",listIssues[0].Message);
			StringAssert.Contains(listIssues[0].HowToFix,"Providers");
		}

		[TestMethod]
		public void GetIssues_ClaimItemErrorsNameTheProcedureLine() {
			XConnectWebResponse response=MakeResponse();
			response.response.dxcStatus.claimItemErrors=new[] { new XConnectClaimItemError { claimItem=2,code="P20" } };
			XConnectValidationIssue issue=XConnectValidation.GetIssues(response,"").Single();
			Assert.AreEqual(XConnectIssueSeverity.Error,issue.Severity);
			Assert.AreEqual("Procedure line 2",issue.Location);
			StringAssert.Contains(issue.Message,"P20");
		}

		[TestMethod]
		public void GetIssues_ClaimStatusMessageIsAnError_ItemMessagesAndWarningsAreWarnings() {
			XConnectWebResponse response=MakeResponse();
			response.response.claimStatus.message="Subscriber ID missing";
			response.response.claimItems=new[] {
				new XConnectClaimItemResponse { itemStatus=new XConnectStatus { message="" } },
				new XConnectClaimItemResponse { itemStatus=new XConnectStatus { message="Tooth number required" } },
			};
			response.response.warnings=new[] { "Payer prefers electronic attachments" };
			List<XConnectValidationIssue> listIssues=XConnectValidation.GetIssues(response,"");
			Assert.AreEqual(3,listIssues.Count);
			Assert.AreEqual(XConnectIssueSeverity.Error,listIssues[0].Severity,"Errors come first.");
			Assert.AreEqual("Subscriber ID missing",listIssues[0].Message);
			Assert.AreEqual(XConnectIssueSeverity.Warning,listIssues[1].Severity);
			Assert.AreEqual("Procedure line 2",listIssues[1].Location);
			Assert.AreEqual(XConnectIssueSeverity.Warning,listIssues[2].Severity);
		}

		[TestMethod]
		public void GetIssues_AttachmentMessageSkippedOnceSubmitted() {
			XConnectWebResponse response=MakeResponse();
			response.response.claimItems=new[] { new XConnectClaimItemResponse { itemStatus=new XConnectStatus { message="Attachment required: X-rays" } } };
			Assert.AreEqual(1,XConnectValidation.GetIssues(response,"Misc").Count);
			Assert.AreEqual(0,XConnectValidation.GetIssues(response,"Misc,Submitted").Count,"24.4: no attachments warning once they were sent.");
		}

		[TestMethod]
		public void GetIssues_FailedStatusWithoutDetailIsAnError() {
			XConnectWebResponse response=new XConnectWebResponse {
				status=new XConnectResponseStatus { code=401,description="Invalid API key" },
				messages=new string[0],
			};
			XConnectValidationIssue issue=XConnectValidation.GetIssues(response,"").Single();
			Assert.AreEqual(XConnectIssueSeverity.Error,issue.Severity);
			Assert.AreEqual("Invalid API key",issue.Message);
			Assert.AreEqual(1,XConnectValidation.GetIssues(null,"").Count,"No response at all is an error.");
		}

		[TestMethod]
		public void GetIssues_DuplicatesCollapse() {
			XConnectWebResponse response=MakeResponse();
			response.response.warnings=new[] { "Check the payer ID","Check the payer ID" };
			Assert.AreEqual(1,XConnectValidation.GetIssues(response,"").Count);
		}
		#endregion GetIssues

		#region Re-validation and send decision
		private static XConnectValidationIssue MakeIssue(XConnectIssueSeverity severity,string message) {
			return new XConnectValidationIssue { Severity=severity,Location="Claim",Message=message };
		}

		[TestMethod]
		public void MergeRevalidation_ProblemsThatWentAwayAreResolved() {
			List<XConnectValidationIssue> listPrevious=new List<XConnectValidationIssue> {
				MakeIssue(XConnectIssueSeverity.Error,"Subscriber ID missing"),
				MakeIssue(XConnectIssueSeverity.Warning,"Check the payer ID"),
			};
			List<XConnectValidationIssue> listLatest=new List<XConnectValidationIssue> { MakeIssue(XConnectIssueSeverity.Warning,"Check the payer ID") };
			List<XConnectValidationIssue> listMerged=XConnectValidation.MergeRevalidation(listPrevious,listLatest);
			Assert.AreEqual(2,listMerged.Count);
			Assert.IsFalse(listMerged.Single(x => x.Message=="Check the payer ID").IsResolved);
			Assert.IsTrue(listMerged.Single(x => x.Message=="Subscriber ID missing").IsResolved);
			//A resolved problem that comes back is unresolved again.
			List<XConnectValidationIssue> listAgain=XConnectValidation.MergeRevalidation(listMerged,listPrevious);
			Assert.IsTrue(listAgain.All(x => !x.IsResolved));
		}

		[TestMethod]
		public void MergeRevalidation_FailedCallKeepsTheOldProblems() {
			XConnectValidationResult resultPrevious=new XConnectValidationResult { ClaimNum=1 };
			resultPrevious.ListIssues.Add(MakeIssue(XConnectIssueSeverity.Error,"Subscriber ID missing"));
			XConnectValidationResult resultLatest=new XConnectValidationResult { ClaimNum=1,CallFailedMessage="timed out" };
			XConnectValidationResult resultMerged=XConnectValidation.MergeRevalidation(resultPrevious,resultLatest);
			Assert.AreEqual(1,resultMerged.ListIssues.Count);
			Assert.IsFalse(resultMerged.ListIssues[0].IsResolved);
			Assert.AreEqual(XConnectSendDecision.CallFailed,XConnectValidation.GetSendDecision(resultMerged));
		}

		[TestMethod]
		public void GetSendDecision_ErrorsBlockWarningsAskFailuresBlock() {
			XConnectValidationResult resultClean=new XConnectValidationResult { ClaimNum=1 };
			XConnectValidationResult resultWarning=new XConnectValidationResult { ClaimNum=2 };
			resultWarning.ListIssues.Add(MakeIssue(XConnectIssueSeverity.Warning,"w"));
			XConnectValidationResult resultError=new XConnectValidationResult { ClaimNum=3 };
			resultError.ListIssues.Add(MakeIssue(XConnectIssueSeverity.Warning,"w"));
			resultError.ListIssues.Add(MakeIssue(XConnectIssueSeverity.Error,"e"));
			XConnectValidationResult resultFailed=new XConnectValidationResult { ClaimNum=4,CallFailedMessage="timed out" };
			XConnectValidationResult resultResolved=new XConnectValidationResult { ClaimNum=5 };
			resultResolved.ListIssues.Add(new XConnectValidationIssue { Severity=XConnectIssueSeverity.Error,Message="e",IsResolved=true });
			Assert.AreEqual(XConnectSendDecision.Send,XConnectValidation.GetSendDecision(resultClean));
			Assert.AreEqual(XConnectSendDecision.ConfirmWarnings,XConnectValidation.GetSendDecision(resultWarning));
			Assert.AreEqual(XConnectSendDecision.Block,XConnectValidation.GetSendDecision(resultError));
			Assert.AreEqual(XConnectSendDecision.CallFailed,XConnectValidation.GetSendDecision(resultFailed));
			Assert.AreEqual(XConnectSendDecision.Send,XConnectValidation.GetSendDecision(resultResolved),"Resolved problems do not count.");
			List<XConnectValidationResult> listResults=new List<XConnectValidationResult> { resultClean,resultWarning,resultError,resultFailed,resultResolved };
			CollectionAssert.AreEqual(new long[] { 1,5 },XConnectValidation.GetClaimNumsToSend(listResults,false));
			CollectionAssert.AreEqual(new long[] { 1,2,5 },XConnectValidation.GetClaimNumsToSend(listResults,true),"Send Anyway sends warning-only claims, never errors.");
			StringAssert.Contains(XConnectValidation.GetSummary(resultError),"errors: 1");
			StringAssert.Contains(XConnectValidation.GetSummary(resultWarning),"warnings: 1");
			Assert.AreEqual("",XConnectValidation.GetSummary(resultClean));
		}
		#endregion Re-validation and send decision

		#region HTTP call
		[TestMethod]
		public void PostValidation_SendsTheOfficesLoginAndKeyAndNoPartnerIdentity() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler();
			fakeHttpHandler.Enqueue(HttpStatusCode.OK,"{\"status\":{\"code\":0,\"description\":\"OK\"},\"transactionId\":42,\"response\":{\"warnings\":[\"w1\"]},\"newField\":1}");
			XConnect.HttpHandlerForTests=fakeHttpHandler;
			XConnectValidationCall call=XConnect.PostValidation(MakeClearinghouse(),"{\"claim\":{}}",TimeSpan.FromSeconds(5));
			RecordedRequest request=fakeHttpHandler.Requests.Single();
			Assert.AreEqual(HttpMethod.Post,request.Method);
			Assert.AreEqual("https://api.dentalxchange.com/claims/validation",request.RequestUri.ToString());
			Assert.AreEqual("office-login",request.GetHeader("username"));
			Assert.AreEqual("office-password",request.GetHeader("password"));
			Assert.AreEqual(API_KEY,request.GetHeader("API-Key"));
			Assert.IsNull(request.GetHeader("Client"));
			Assert.IsNull(request.GetHeader("ServiceID"));
			Assert.AreEqual("{\"claim\":{}}",request.Body);
			Assert.AreEqual(42,call.Response.transactionId,"Unknown response fields are ignored.");
			Assert.AreEqual("w1",call.Response.response.warnings[0]);
			StringAssert.Contains(call.ResponseJson,"transactionId");
		}

		[TestMethod]
		public void PostValidation_BlankKeyRefusesWithoutACall() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler();
			XConnect.HttpHandlerForTests=fakeHttpHandler;
			AssertEx.Throws<ODException>(() => XConnect.PostValidation(MakeClearinghouse(locationID:""),"{}",TimeSpan.FromSeconds(5)));
			Assert.AreEqual(0,fakeHttpHandler.Requests.Count);
		}

		[TestMethod]
		public void PostValidation_TimeoutSaysSo() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler();
			fakeHttpHandler.Enqueue(async (request,ct) => {
				await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(10),ct);
				return FakeHttpHandler.MakeResponse(HttpStatusCode.OK,"{}");
			});
			XConnect.HttpHandlerForTests=fakeHttpHandler;
			ODException ex=AssertEx.Throws<ODException>(() => XConnect.PostValidation(MakeClearinghouse(),"{}",TimeSpan.FromMilliseconds(200)));
			StringAssert.Contains(ex.Message,"did not answer");
		}

		[TestMethod]
		public void PostValidation_ErrorStatusWithXConnectBodyIsReturned() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler();
			fakeHttpHandler.Enqueue(HttpStatusCode.BadRequest,"{\"status\":{\"code\":400,\"description\":\"Validation failed\"},\"messages\":[\"Subscriber ID missing\"]}");
			XConnect.HttpHandlerForTests=fakeHttpHandler;
			XConnectValidationCall call=XConnect.PostValidation(MakeClearinghouse(),"{}",TimeSpan.FromSeconds(5));
			Assert.AreEqual(400,call.HttpStatusCode);
			XConnectValidationIssue issue=XConnectValidation.GetIssues(call.Response,"").Single();
			Assert.AreEqual(XConnectIssueSeverity.Error,issue.Severity);
			Assert.AreEqual("Subscriber ID missing",issue.Message);
		}

		[TestMethod]
		public void PostValidation_ServerErrorWithoutXConnectBodyThrows() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler();
			fakeHttpHandler.Enqueue(HttpStatusCode.InternalServerError,"<html>down</html>","text/html");
			XConnect.HttpHandlerForTests=fakeHttpHandler;
			ODException ex=AssertEx.Throws<ODException>(() => XConnect.PostValidation(MakeClearinghouse(),"{}",TimeSpan.FromSeconds(5)));
			StringAssert.Contains(ex.Message,"500");
		}

		[TestMethod]
		public void SerializeRequest_LeavesOutNullsAndWritesEnumNames() {
			string json=XConnect.SerializeRequest(new XConnectValidateClaim { validateForAttachment=true });
			Assert.IsFalse(json.Contains("\"claim\":null"));
			StringAssert.Contains(json,"\"validateForAttachment\":true");
		}
		#endregion HTTP call

		#region Clearinghouse setup log
		[TestMethod]
		public void GetXConnectChangeLogLines_HqAndClinicChanges() {
			Clearinghouse hqOld=new Clearinghouse { Description="ClaimConnect",EnableXConnect=false };
			Clearinghouse hqNew=new Clearinghouse { Description="ClaimConnect",EnableXConnect=true };
			List<Clearinghouse> listClinOld=new List<Clearinghouse> { new Clearinghouse { ClinicNum=7,EnableXConnect=true } };
			List<Clearinghouse> listClinNew=new List<Clearinghouse> {
				new Clearinghouse { ClinicNum=7,EnableXConnect=false },
				new Clearinghouse { ClinicNum=8,EnableXConnect=true },//New override; was HQ's old value (off)
				new Clearinghouse { ClinicNum=9,EnableXConnect=false },//New override, same as HQ's old value: no line
			};
			List<string> listLines=Clearinghouses.GetXConnectChangeLogLines(hqOld,hqNew,listClinOld,listClinNew,x => x==7 ? "North" : null);
			CollectionAssert.AreEqual(new[] {
				"XConnect enabled for clearinghouse ClaimConnect, clinic Headquarters",
				"XConnect disabled for clearinghouse ClaimConnect, clinic North",
				"XConnect enabled for clearinghouse ClaimConnect, clinic 8",
			},listLines);
		}
		#endregion Clearinghouse setup log
	}
}
