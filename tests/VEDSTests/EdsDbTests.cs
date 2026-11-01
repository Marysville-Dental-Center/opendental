// V-EDS against a scratch database: the claim snapshot, the local EDS checks inside Eclaims.GetMissingData, and validation with EDS through a
// scripted HTTP layer (EDS.HttpHandlerForTests). EDS is never called. Synthetic data only (made-up Z codes, NPIs and payer IDs).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.Eclaims;
using OpenDentBusiness.VendorDirect.Eds;
using UnitTestsCore;

namespace VEDSTests {
	[TestClass]
	public class EdsDbTests {
		private const string LOGIN_ID="office-login";
		private const string PASSWORD="office-secret-0001";
		private static int _counter;
		private FakeHttpHandler _fakeHttpHandler;

		[TestInitialize]
		public void TestInitialize() {
			EdsTestDb.Begin();
			DataCore.NonQ("DELETE FROM clearinghouse");
			DataCore.NonQ("DELETE FROM electid WHERE CommBridge="+(int)EclaimsCommBridge.EDS);
			Clearinghouses.RefreshCache();
			ElectIDs.RefreshCache();
			EdsClaimValidation.ClearLastResults();
			EdsClaimValidation.EnhancedValidator=null;
			_fakeHttpHandler=new FakeHttpHandler();
			EDS.HttpHandlerForTests=_fakeHttpHandler;
		}

		[TestCleanup]
		public void TestCleanup() {
			EDS.HttpHandlerForTests=null;
			EdsClaimValidation.EnhancedValidator=null;
			EdsClaimValidation.ClearLastResults();
		}

		#region Data
		private class TestClaim {
			public Claim Claim;
			public Patient Patient;
			public long ProvNum;
			public Procedure Procedure;
			public Carrier Carrier;
		}

		private static Clearinghouse CreateEdsClearinghouse(ElectronicClaimFormat eformat,bool isAttachmentSendAllowed,EclaimsCommBridge commBridge=EclaimsCommBridge.EDS) {
			string concealed;
			Assert.IsTrue(CodeBase.Secrets.ConcealClearinghouse(PASSWORD,out concealed));
			Clearinghouse clearinghouse=ClearinghouseT.CreateClearinghouse("Synthetic EDS",0,commBridge,eformat,0,isAttachmentSendAllowed,LOGIN_ID,concealed);
			Prefs.UpdateLong(PrefName.ClearinghouseDefaultDent,clearinghouse.ClearinghouseNum);
			Prefs.RefreshCache();
			Clearinghouses.RefreshCache();
			return Clearinghouses.OverrideFields(Clearinghouses.GetClearinghouse(clearinghouse.ClearinghouseNum),0);
		}

		private static TestClaim CreateClaim(string npi=null,DateTime procDate=default,string electId="ZZ123",string claimType="P") {
			int counter=++_counter;
			TestClaim testClaim=new TestClaim();
			testClaim.ProvNum=ProviderT.CreateProvider("VE"+counter,"Prov","Synthetic"+counter,nationalProvID:npi??EdsTestData.MakeNpi("1000000"+counter.ToString("00")));
			testClaim.Patient=PatientT.CreatePatient(lName:"Edstest",fName:"Pat"+counter,priProvNum:testClaim.ProvNum,birthDate:new DateTime(1980,1,1));
			testClaim.Carrier=CarrierT.CreateCarrier("Synthetic Carrier "+counter,electID:electId);
			InsPlan insPlan=InsPlanT.CreateInsPlan(testClaim.Carrier.CarrierNum);
			InsSub insSub=InsSubT.CreateInsSub(testClaim.Patient.PatNum,insPlan.PlanNum,"SUB"+counter.ToString("0000"));
			PatPlanT.CreatePatPlan(1,testClaim.Patient.PatNum,insSub.InsSubNum);
			if(procDate==default) {
				procDate=DateTime.Today.AddDays(-2);
			}
			testClaim.Procedure=ProcedureT.CreateProcedure(testClaim.Patient,"Z0001",ProcStat.C,"",100,procDate,provNum:testClaim.ProvNum);
			Claim claim=new Claim();
			claim.PatNum=testClaim.Patient.PatNum;
			claim.PlanNum=insPlan.PlanNum;
			claim.InsSubNum=insSub.InsSubNum;
			claim.ProvBill=testClaim.ProvNum;
			claim.ProvTreat=testClaim.ProvNum;
			claim.ClaimType=claimType;
			claim.ClaimStatus="W";
			claim.DateService=procDate;
			claim.MedType=EnumClaimMedType.Dental;
			claim.PatRelat=Relat.Self;
			claim.ClaimFee=100;
			claim.ClaimIdentifier="VEDS"+counter;
			Claims.Insert(claim);
			ClaimProc claimProc=new ClaimProc();
			claimProc.ProcNum=testClaim.Procedure.ProcNum;
			claimProc.ClaimNum=claim.ClaimNum;
			claimProc.PatNum=claim.PatNum;
			claimProc.ProvNum=testClaim.ProvNum;
			claimProc.PlanNum=claim.PlanNum;
			claimProc.InsSubNum=claim.InsSubNum;
			claimProc.Status=ClaimProcStatus.NotReceived;
			claimProc.ProcDate=procDate;
			claimProc.DateCP=procDate;
			claimProc.CodeSent="Z0001";
			claimProc.LineNumber=1;
			claimProc.FeeBilled=100;
			ClaimProcs.Insert(claimProc);
			testClaim.Claim=Claims.GetClaim(claim.ClaimNum);
			return testClaim;
		}

		private static ClaimSendQueueItem GetQueueItem(Claim claim,Clearinghouse clearinghouseClin) {
			ClaimSendQueueItem queueItem=Claims.GetQueueList(claim.ClaimNum,0,0).Single();
			return Eclaims.GetMissingData(clearinghouseClin,queueItem);
		}

		private static string AttachmentAnswerBitewings() {
			return EdsTestData.MakeAttachmentAnswer(false,("Z0001",true,new[] { "Bitewings" },"Synthetic comment"));
		}
		#endregion Data

		#region Snapshot and local checks
		[TestMethod]
		public void Snapshot_ReadsTheClaimAsTheEclaimSendsIt() {
			Clearinghouse clearinghouse=CreateEdsClearinghouse(ElectronicClaimFormat.x837D_5010_dental,true);
			TestClaim testClaim=CreateClaim();
			ElectIDs.Insert(new ElectID { PayorID="ZZ123",CarrierName="Synthetic payer",CommBridge=EclaimsCommBridge.EDS,Attributes="0,6",
				ProviderTypes="",Comments="" });
			ElectIDs.RefreshCache();
			EdsClaimSnapshot snapshot=EdsClaimSnapshot.FromDb(testClaim.Claim,clearinghouse);
			Assert.AreEqual(testClaim.Claim.ClaimNum,snapshot.ClaimNum);
			Assert.AreEqual("P",snapshot.ClaimType);
			Assert.AreEqual(testClaim.ProvNum,snapshot.ProvBill.ProvNum);
			Assert.IsTrue(snapshot.ProvBill.IsFound);
			Assert.AreEqual(Providers.GetProv(testClaim.ProvNum).NationalProvID,snapshot.ProvTreat.Npi);
			Assert.AreEqual("1223G0001X",snapshot.ProvBill.Taxonomy,"No override and no specialty: the general dentistry code the X12 writer uses.");
			Assert.AreEqual("ZZ123",snapshot.PayerId);
			Assert.AreEqual(testClaim.Carrier.CarrierName,snapshot.CarrierName);
			StringAssert.StartsWith(snapshot.SubscriberId,"SUB");
			Assert.AreEqual(1,snapshot.ListProcLines.Count);
			Assert.AreEqual("Z0001",snapshot.ListProcLines[0].ProcCode);
			Assert.AreEqual(testClaim.Procedure.ProcDate.Date,snapshot.ListProcLines[0].ProcDate.Date);
			Assert.IsTrue(snapshot.IsX12Validated);
			Assert.IsTrue(snapshot.IsEdsPayerListLoaded);
			Assert.IsTrue(snapshot.ListEdsPayers.Single().ListAttributes.Contains(EnumEDSPayerAttributes.DefaultClaimTP));
			Assert.IsFalse(snapshot.HasAttachments);
			Assert.AreEqual(0,EdsClaimChecks.GetLocalIssues(snapshot).Count);
		}

		[TestMethod]
		public void GetMissingData_AddsEdsErrors_ForEdsClaimsOnly() {
			TestClaim testClaim=CreateClaim(npi:EdsTestData.MakeBadNpi("100000099"));
			Clearinghouse clearinghouseEds=CreateEdsClearinghouse(ElectronicClaimFormat.None,false);
			ClaimSendQueueItem queueItem=GetQueueItem(testClaim.Claim,clearinghouseEds);
			StringAssert.StartsWith(queueItem.MissingData,"EDS: Billing and treating provider");
			StringAssert.Contains(queueItem.MissingData,"check digit");
			Clearinghouse clearinghouseOther=CreateEdsClearinghouse(ElectronicClaimFormat.None,false,EclaimsCommBridge.None);
			ClaimSendQueueItem queueItemOther=GetQueueItem(testClaim.Claim,clearinghouseOther);
			Assert.IsFalse((queueItemOther.MissingData??"").Contains("EDS:"),"Other clearinghouses are unchanged.");
		}

		[TestMethod]
		public void GetMissingData_EdsWarningsGoToWarnings() {
			TestClaim testClaim=CreateClaim(electId:"ZZ777");
			ElectIDs.Insert(new ElectID { PayorID="ZZ123",CarrierName="Another payer",CommBridge=EclaimsCommBridge.EDS,Attributes="0,6",ProviderTypes="",Comments="" });
			ElectIDs.RefreshCache();
			Clearinghouse clearinghouseEds=CreateEdsClearinghouse(ElectronicClaimFormat.None,false);
			ClaimSendQueueItem queueItem=GetQueueItem(testClaim.Claim,clearinghouseEds);
			Assert.AreEqual("",queueItem.MissingData);
			StringAssert.Contains(queueItem.Warnings,"EDS: ");
			StringAssert.Contains(queueItem.Warnings,"ZZ777");
		}

		[TestMethod]
		public void GetMissingData_BlankOrShortPayerId_IsAWarning_NotMissingData() {
			//New carriers send electronically with a blank payer ID, and X12 claims then go out with the paper-claim payer ID. Missing Info stays empty,
			//so Edit Claim and Send Claims can still send, Add Attachment is allowed, and ClaimEditRequireNoMissingData does not refuse the claim.
			Clearinghouse clearinghouseEds=CreateEdsClearinghouse(ElectronicClaimFormat.None,false);
			TestClaim testClaimBlank=CreateClaim(electId:"");
			ClaimSendQueueItem queueItem=GetQueueItem(testClaimBlank.Claim,clearinghouseEds);
			Assert.AreEqual("",queueItem.MissingData);
			StringAssert.Contains(queueItem.Warnings,"EDS: Carrier ("+testClaimBlank.Carrier.CarrierName+"): The carrier has no electronic payer ID.");
			ClaimSendQueueItem queueItemNewClaim=Eclaims.GetMissingData(clearinghouseEds,Claims.GetQueueList(testClaimBlank.Claim.ClaimNum,0,0).Single(),
				true,true);//As ClaimL.CreateClaim checks a new claim.
			Assert.AreEqual("",queueItemNewClaim.MissingData);
			EdsValidationResult result=EdsClaimValidation.ValidateLocal(testClaimBlank.Claim,clearinghouseEds,queueItem);
			Assert.AreEqual(EdsSendDecision.ConfirmWarnings,EdsClaimValidation.GetSendDecision(result),"Listed in the window, with Send Anyway.");
			Assert.AreEqual(EdsIssueSeverity.Warning,result.ListIssues.Single(x => x.CheckId==EdsClaimChecks.CHECK_PAYER_ID_MISSING).Severity);
			TestClaim testClaimShort=CreateClaim(electId:"ZZ");
			queueItem=GetQueueItem(testClaimShort.Claim,clearinghouseEds);
			Assert.AreEqual("",queueItem.MissingData);
			StringAssert.Contains(queueItem.Warnings,"too short to be a payer ID");
			Clearinghouse clearinghouseX12=CreateEdsClearinghouse(ElectronicClaimFormat.x837D_5010_dental,false);
			queueItem=GetQueueItem(testClaimBlank.Claim,clearinghouseX12);
			Assert.IsFalse((queueItem.MissingData??"").Contains("EDS:"),"With the X12 validator too, the EDS part adds nothing to Missing Info.");
			StringAssert.Contains(queueItem.Warnings,"no electronic payer ID");
		}

		[TestMethod]
		public void GetMissingData_WithX12_TheEdsPartComesLastAndSplitsOff() {
			TestClaim testClaim=CreateClaim(procDate:DateTime.Today.AddDays(3));
			Clearinghouse clearinghouseEds=CreateEdsClearinghouse(ElectronicClaimFormat.x837D_5010_dental,false);
			ClaimSendQueueItem queueItem=GetQueueItem(testClaim.Claim,clearinghouseEds);
			StringAssert.Contains(queueItem.MissingData,"EDS: ");
			EdsValidationResult result=EdsClaimValidation.ValidateLocal(testClaim.Claim,clearinghouseEds,queueItem);
			Assert.AreEqual(EdsSendDecision.Block,EdsClaimValidation.GetSendDecision(result));
			Assert.IsTrue(result.ListIssues.Any(x => x.CheckId==EdsClaimChecks.CHECK_PROC_DATE_FUTURE));
			EdsValidationIssue issueOther=result.ListIssues.FirstOrDefault(x => x.CheckId==EdsClaimChecks.CHECK_OTHER_MISSING_DATA);
			if(issueOther!=null) {
				Assert.IsFalse(issueOther.Message.Contains("EDS: "),"The format validators' part is listed once, without the EDS part.");
			}
		}
		#endregion Snapshot and local checks

		#region Validation with EDS
		[TestMethod]
		public void Validate_AsksEdsWithTheOfficeLogin_AndListsWhatEdsSaid() {
			Clearinghouse clearinghouse=CreateEdsClearinghouse(ElectronicClaimFormat.None,true);
			TestClaim testClaim=CreateClaim();
			_fakeHttpHandler.Enqueue(HttpStatusCode.OK,AttachmentAnswerBitewings());
			EdsValidationResult result=EdsClaimValidation.Validate(testClaim.Claim,clearinghouse,GetQueueItem(testClaim.Claim,clearinghouse),
				EDS.TimeoutValidateOnSend,true);
			Assert.IsTrue(result.IsRemoteChecked,result.CallFailedMessage);
			Assert.AreEqual(EdsSendDecision.ConfirmWarnings,EdsClaimValidation.GetSendDecision(result));
			EdsValidationIssue issue=result.ListIssues.Single();
			Assert.AreEqual(EdsIssueSource.Eds,issue.Source);
			StringAssert.Contains(issue.Message,"Bitewings");
			RecordedRequest request=_fakeHttpHandler.Requests.Single();
			Assert.AreEqual(HttpMethod.Post,request.Method);
			Assert.AreEqual("https://web2.edsedi.com/api/validateattachment",request.RequestUri.ToString());
			Assert.AreEqual("Basic "+Convert.ToBase64String(Encoding.UTF8.GetBytes(LOGIN_ID+":"+PASSWORD)),request.Authorization,
				"The office's own EDS login, revealed from the concealed clearinghouse password.");
			Assert.AreEqual("application/json",request.ContentType);
			StringAssert.Contains(request.Body,"\"payerId\":\"ZZ123\"");
			StringAssert.Contains(request.Body,"\"procId\":\"Z0001\"");
			Assert.IsFalse(request.Body.Contains(PASSWORD),"The password goes only in the header.");
			Assert.IsNotNull(EdsClaimValidation.GetLastResult(testClaim.Claim.ClaimNum),"Rule 4: the last validation is kept for the claim.");
			StringAssert.Contains(EdsClaimValidation.GetLastValidationText(testClaim.Claim.ClaimNum),"Bitewings");
		}

		[TestMethod]
		public void Validate_EdsDown_LetsTheUserDecide() {
			Clearinghouse clearinghouse=CreateEdsClearinghouse(ElectronicClaimFormat.None,true);
			TestClaim testClaim=CreateClaim();
			_fakeHttpHandler.Enqueue(HttpStatusCode.InternalServerError,"Synthetic server error");
			EdsValidationResult result=EdsClaimValidation.Validate(testClaim.Claim,clearinghouse,GetQueueItem(testClaim.Claim,clearinghouse),
				EDS.TimeoutValidateOnSend,true);
			Assert.IsTrue(result.IsCallFailed);
			Assert.IsFalse(result.IsRemoteChecked);
			StringAssert.StartsWith(result.CallFailedMessage,"EDS could not be reached:");
			StringAssert.Contains(result.CallFailedMessage,"500");
			StringAssert.Contains(result.CallFailedMessage,"Synthetic server error");
			Assert.IsFalse(result.CallFailedMessage.Contains(PASSWORD));
			Assert.AreEqual(EdsSendDecision.ConfirmUnreachable,EdsClaimValidation.GetSendDecision(result));
		}

		[TestMethod]
		public void Validate_LoginRefused_SaysSo() {
			Clearinghouse clearinghouse=CreateEdsClearinghouse(ElectronicClaimFormat.None,true);
			TestClaim testClaim=CreateClaim();
			_fakeHttpHandler.Enqueue(HttpStatusCode.Unauthorized,"");
			EdsValidationResult result=EdsClaimValidation.Validate(testClaim.Claim,clearinghouse,GetQueueItem(testClaim.Claim,clearinghouse),
				EDS.TimeoutValidateOnSend,true);
			StringAssert.Contains(result.CallFailedMessage,"login ID and password");
			Assert.AreEqual(EdsSendDecision.ConfirmUnreachable,EdsClaimValidation.GetSendDecision(result));
		}

		[TestMethod]
		public void Validate_EdsTooSlow_TimesOut() {
			Clearinghouse clearinghouse=CreateEdsClearinghouse(ElectronicClaimFormat.None,true);
			TestClaim testClaim=CreateClaim();
			_fakeHttpHandler.Enqueue(async (request,cancellationToken) => {
				await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(20),cancellationToken).ConfigureAwait(false);
				return FakeHttpHandler.MakeResponse(HttpStatusCode.OK,AttachmentAnswerBitewings(),"application/json");
			});
			DateTime dateTimeStart=DateTime.Now;
			EdsValidationResult result=EdsClaimValidation.Validate(testClaim.Claim,clearinghouse,GetQueueItem(testClaim.Claim,clearinghouse),
				TimeSpan.FromMilliseconds(500),true);
			Assert.IsTrue(DateTime.Now-dateTimeStart<TimeSpan.FromSeconds(10));
			StringAssert.Contains(result.CallFailedMessage,"did not answer");
			Assert.AreEqual(EdsSendDecision.ConfirmUnreachable,EdsClaimValidation.GetSendDecision(result));
		}

		[TestMethod]
		public void Validate_UnreadableAnswer_IsAFailedCall() {
			Clearinghouse clearinghouse=CreateEdsClearinghouse(ElectronicClaimFormat.None,true);
			TestClaim testClaim=CreateClaim();
			_fakeHttpHandler.Enqueue(HttpStatusCode.OK,"<html>not json</html>","text/html");
			EdsValidationResult result=EdsClaimValidation.Validate(testClaim.Claim,clearinghouse,GetQueueItem(testClaim.Claim,clearinghouse),
				EDS.TimeoutValidateOnSend,true);
			Assert.IsTrue(result.IsCallFailed);
			_fakeHttpHandler.Enqueue(HttpStatusCode.OK,"null");
			result=EdsClaimValidation.Validate(testClaim.Claim,clearinghouse,GetQueueItem(testClaim.Claim,clearinghouse),EDS.TimeoutValidateOnSend,true);
			StringAssert.Contains(result.CallFailedMessage,"empty answer");
		}

		[TestMethod]
		public void Validate_AttachmentServiceOff_RunsLocalChecksOnly() {
			Clearinghouse clearinghouse=CreateEdsClearinghouse(ElectronicClaimFormat.None,false);
			TestClaim testClaim=CreateClaim();
			EdsValidationResult result=EdsClaimValidation.Validate(testClaim.Claim,clearinghouse,GetQueueItem(testClaim.Claim,clearinghouse),
				EDS.TimeoutValidateOnSend,true);
			Assert.AreEqual(0,_fakeHttpHandler.Requests.Count,"Off by default: no network call.");
			Assert.IsFalse(result.IsRemoteChecked);
			StringAssert.Contains(result.RemoteNote,"attachment service");
			Assert.AreEqual(EdsSendDecision.Send,EdsClaimValidation.GetSendDecision(result));
		}

		[TestMethod]
		public void Validate_LocalErrors_EdsIsNotAsked() {
			Clearinghouse clearinghouse=CreateEdsClearinghouse(ElectronicClaimFormat.None,true);
			TestClaim testClaim=CreateClaim(procDate:DateTime.Today.AddDays(5));
			EdsValidationResult result=EdsClaimValidation.Validate(testClaim.Claim,clearinghouse,GetQueueItem(testClaim.Claim,clearinghouse),
				EDS.TimeoutValidateOnSend,true);
			Assert.AreEqual(0,_fakeHttpHandler.Requests.Count);
			Assert.AreEqual(EdsSendDecision.Block,EdsClaimValidation.GetSendDecision(result));
			CollectionAssert.AreEquivalent(new[] { EdsClaimChecks.CHECK_DATE_SERVICE_FUTURE,EdsClaimChecks.CHECK_PROC_DATE_FUTURE },
				EdsTestData.CheckIds(result.ListIssues));
			StringAssert.Contains(result.RemoteNote,"fix the errors first");
		}

		[TestMethod]
		public void Validate_NotEds_IsSkipped() {
			Clearinghouse clearinghouse=CreateEdsClearinghouse(ElectronicClaimFormat.None,true,EclaimsCommBridge.ClaimConnect);
			TestClaim testClaim=CreateClaim();
			EdsValidationResult result=EdsClaimValidation.Validate(testClaim.Claim,clearinghouse,GetQueueItem(testClaim.Claim,clearinghouse),
				EDS.TimeoutValidateOnSend,true);
			Assert.IsTrue(result.IsSkipped);
			Assert.AreEqual(0,_fakeHttpHandler.Requests.Count);
			Assert.AreEqual(EdsSendDecision.Send,EdsClaimValidation.GetSendDecision(result));
			Assert.IsNull(EdsClaimValidation.GetLastResult(testClaim.Claim.ClaimNum));
		}

		[TestMethod]
		public void Validate_AttachmentAlreadySent_NoWarning() {
			Clearinghouse clearinghouse=CreateEdsClearinghouse(ElectronicClaimFormat.None,true);
			TestClaim testClaim=CreateClaim();
			testClaim.Claim.AttachmentID="EDS-SYNTH-1";
			Claims.Update(testClaim.Claim);
			_fakeHttpHandler.Enqueue(HttpStatusCode.OK,AttachmentAnswerBitewings());
			EdsValidationResult result=EdsClaimValidation.Validate(testClaim.Claim,clearinghouse,GetQueueItem(testClaim.Claim,clearinghouse),
				EDS.TimeoutValidateOnSend,true);
			Assert.IsTrue(result.IsRemoteChecked);
			Assert.AreEqual(0,result.ListIssues.Count);
			Assert.AreEqual(EdsSendDecision.Send,EdsClaimValidation.GetSendDecision(result));
		}

		[TestMethod]
		public void NarrativeCheck_RepeatsEdsLastAnswer_InGetMissingData() {
			Clearinghouse clearinghouse=CreateEdsClearinghouse(ElectronicClaimFormat.None,true);
			TestClaim testClaim=CreateClaim();
			long claimNum=testClaim.Claim.ClaimNum;
			string answerNarrative=EdsTestData.MakeAttachmentAnswer(false,("Z0001",true,new[] { "Narrative" },null));
			ClaimSendQueueItem queueItem=GetQueueItem(testClaim.Claim,clearinghouse);
			Assert.IsFalse((queueItem.Warnings??"").Contains("narrative"),"EDS has not answered for the claim: nothing to check yet.");
			_fakeHttpHandler.Enqueue(HttpStatusCode.OK,answerNarrative);
			EdsValidationResult result=EdsClaimValidation.Validate(testClaim.Claim,clearinghouse,queueItem,EDS.TimeoutValidateOnSend,true);
			Assert.IsTrue(result.IsRemoteChecked,result.CallFailedMessage);
			EdsValidationIssue issue=result.ListIssues.Single();
			Assert.AreEqual(EdsClaimChecks.CHECK_NARRATIVE_REQUIRED,issue.CheckId);
			Assert.AreEqual(EdsIssueSource.Eds,issue.Source);
			CollectionAssert.AreEqual(new[] { "Z0001" },EdsClaimValidation.GetProcCodesNarrativeRequiredByEds(claimNum));
			//The local check now knows: Missing Info's Warnings list it, without a network call.
			queueItem=GetQueueItem(testClaim.Claim,clearinghouse);
			Assert.AreEqual("",queueItem.MissingData,"A warning: sending, claim creation and attachments are not blocked.");
			StringAssert.Contains(queueItem.Warnings,"EDS: Procedure line 1 (Z0001): EDS's last answer for this claim said the payer requires a narrative");
			Assert.AreEqual(1,_fakeHttpHandler.Requests.Count,"GetMissingData never calls EDS.");
			//EDS is down at the next send: the local check still lists the problem.
			_fakeHttpHandler.Enqueue(HttpStatusCode.InternalServerError,"Synthetic server error");
			result=EdsClaimValidation.Validate(testClaim.Claim,clearinghouse,queueItem,EDS.TimeoutValidateOnSend,true);
			Assert.IsTrue(result.IsCallFailed);
			issue=result.ListIssues.Single();
			Assert.AreEqual(EdsClaimChecks.CHECK_NARRATIVE_REQUIRED,issue.CheckId);
			Assert.AreEqual(EdsIssueSource.Local,issue.Source);
			//EDS answers again: the problem is listed once, from EDS's fresh answer.
			_fakeHttpHandler.Enqueue(HttpStatusCode.OK,answerNarrative);
			result=EdsClaimValidation.Validate(testClaim.Claim,clearinghouse,queueItem,EDS.TimeoutValidateOnSend,true);
			Assert.IsTrue(result.IsRemoteChecked,result.CallFailedMessage);
			Assert.AreEqual(EdsIssueSource.Eds,result.ListIssues.Single().Source);
			//The office enters the narrative: met.
			testClaim.Claim.Narrative="Synthetic narrative text.";
			Claims.Update(testClaim.Claim);
			queueItem=GetQueueItem(testClaim.Claim,clearinghouse);
			Assert.IsFalse((queueItem.Warnings??"").Contains("narrative"));
			//EDS no longer asks for a narrative: the local check forgets it.
			testClaim.Claim.Narrative="";
			Claims.Update(testClaim.Claim);
			_fakeHttpHandler.Enqueue(HttpStatusCode.OK,EdsTestData.MakeAttachmentAnswer(false));
			result=EdsClaimValidation.Validate(testClaim.Claim,clearinghouse,GetQueueItem(testClaim.Claim,clearinghouse),EDS.TimeoutValidateOnSend,true);
			Assert.IsTrue(result.IsRemoteChecked,result.CallFailedMessage);
			Assert.AreEqual(0,EdsClaimValidation.GetProcCodesNarrativeRequiredByEds(claimNum).Count);
			Assert.IsFalse((GetQueueItem(testClaim.Claim,clearinghouse).Warnings??"").Contains("narrative"));
		}

		[TestMethod]
		public void NarrativeCheck_AttachmentServiceOff_HasNothingToGoOn() {
			//Default setup: EDS is never asked, so the local narrative check stays silent (port note, deviations and owner question 7).
			Clearinghouse clearinghouse=CreateEdsClearinghouse(ElectronicClaimFormat.None,false);
			TestClaim testClaim=CreateClaim();
			EdsValidationResult result=EdsClaimValidation.Validate(testClaim.Claim,clearinghouse,GetQueueItem(testClaim.Claim,clearinghouse),
				EDS.TimeoutValidateOnSend,true);
			Assert.AreEqual(0,_fakeHttpHandler.Requests.Count);
			Assert.AreEqual(0,result.ListIssues.Count);
			Assert.AreEqual(0,EdsClaimValidation.GetProcCodesNarrativeRequiredByEds(testClaim.Claim.ClaimNum).Count);
		}

		[TestMethod]
		public void Validate_RemoteNotAllowed_NoNetworkCall() {
			Clearinghouse clearinghouse=CreateEdsClearinghouse(ElectronicClaimFormat.None,true);
			TestClaim testClaim=CreateClaim();
			EdsValidationResult result=EdsClaimValidation.Validate(testClaim.Claim,clearinghouse,GetQueueItem(testClaim.Claim,clearinghouse),
				EDS.TimeoutValidateOnSend,false);
			Assert.AreEqual(0,_fakeHttpHandler.Requests.Count);
			Assert.AreEqual("Only local checks ran.",result.RemoteNote);
		}

		private class FakeEnhancedValidator:IEdsEnhancedValidator {
			public bool DoThrow;
			public int CountCalls;

			public List<EdsValidationIssue> Validate(Claim claim,Clearinghouse clearinghouseClin,TimeSpan timeout) {
				CountCalls++;
				if(DoThrow) {
					throw new InvalidOperationException("Synthetic enhanced failure");
				}
				return new List<EdsValidationIssue> { EdsClaimChecks.MakeEds(EdsIssueSeverity.Error,"EnhancedSynthetic","Claim","Synthetic enhanced error","") };
			}
		}

		[TestMethod]
		public void EnhancedValidator_PartnerGatedSeam_MergesIntoTheSameList() {
			Clearinghouse clearinghouse=CreateEdsClearinghouse(ElectronicClaimFormat.None,true);
			TestClaim testClaim=CreateClaim();
			FakeEnhancedValidator fakeEnhancedValidator=new FakeEnhancedValidator();
			EdsClaimValidation.EnhancedValidator=fakeEnhancedValidator;
			_fakeHttpHandler.Enqueue(HttpStatusCode.OK,AttachmentAnswerBitewings());
			EdsValidationResult result=EdsClaimValidation.Validate(testClaim.Claim,clearinghouse,GetQueueItem(testClaim.Claim,clearinghouse),
				EDS.TimeoutValidateOnSend,true);
			Assert.AreEqual(1,fakeEnhancedValidator.CountCalls);
			CollectionAssert.AreEqual(new[] { "EnhancedSynthetic",EdsClaimChecks.CHECK_ATTACHMENT_REQUIRED },EdsTestData.CheckIds(result.ListIssues),
				"Errors first.");
			Assert.AreEqual(EdsSendDecision.Block,EdsClaimValidation.GetSendDecision(result));
			fakeEnhancedValidator.DoThrow=true;
			_fakeHttpHandler.Enqueue(HttpStatusCode.OK,AttachmentAnswerBitewings());
			result=EdsClaimValidation.Validate(testClaim.Claim,clearinghouse,GetQueueItem(testClaim.Claim,clearinghouse),EDS.TimeoutValidateOnSend,true);
			StringAssert.Contains(result.CallFailedMessage,"Synthetic enhanced failure");
		}

		[TestMethod]
		public void ValidateClaimNum_ReadsTheQueueItemAndClearinghouseAfresh() {
			Clearinghouse clearinghouse=CreateEdsClearinghouse(ElectronicClaimFormat.None,true);
			TestClaim testClaim=CreateClaim();
			_fakeHttpHandler.Enqueue(HttpStatusCode.OK,EdsTestData.MakeAttachmentAnswer(false));
			EdsValidationResult result=EdsClaimValidation.ValidateClaimNum(testClaim.Claim.ClaimNum,0,EDS.TimeoutValidateOnSend);
			Assert.IsTrue(result.IsRemoteChecked,result.CallFailedMessage+result.SkippedReason+result.RemoteNote);
			Assert.AreEqual(0,result.ListIssues.Count);
			StringAssert.StartsWith(result.PatientName,"Edstest, "+testClaim.Patient.FName);
			Assert.AreEqual(testClaim.Carrier.CarrierName,result.CarrierName);
			Assert.AreEqual(1,_fakeHttpHandler.Requests.Count);
			Assert.AreEqual("EDS validation passed.",EdsClaimValidation.GetSummary(EdsClaimValidation.GetLastResult(testClaim.Claim.ClaimNum)));
			EdsValidationResult resultMissing=EdsClaimValidation.ValidateClaimNum(987654321,0,EDS.TimeoutValidateOnSend);
			Assert.IsFalse(resultMissing.IsSkipped,"A deleted claim is not 'not validated, send as before'.");
			Assert.IsTrue(resultMissing.IsNotSendable);
			Assert.AreEqual(EdsSendDecision.Block,EdsClaimValidation.GetSendDecision(resultMissing));
			Assert.AreEqual(EdsClaimChecks.CHECK_CLAIM_NOT_SENDABLE,resultMissing.ListIssues.Single().CheckId);
		}

		[TestMethod]
		public void Batch_ClaimSentDeletedOrHeldDuringTheWindow_IsNeverSent() {
			//Send Claims validates a batch; the validation window's Edit Claim (or another workstation) then sends one claim, deletes another and holds a
			//third. Re-validation and the final list of claims to send must leave all three out, even with Send Anyway.
			Clearinghouse clearinghouse=CreateEdsClearinghouse(ElectronicClaimFormat.None,false);
			long hqEds=Clearinghouses.GetDeepCopy().First(x => x.CommBridge==EclaimsCommBridge.EDS).ClearinghouseNum;
			TestClaim testClaimSent=CreateClaim(electId:"");//A warning (blank payer ID): listed in the window, sent only with Send Anyway.
			TestClaim testClaimDeleted=CreateClaim(electId:"");
			TestClaim testClaimHeld=CreateClaim(electId:"");
			TestClaim testClaimKept=CreateClaim(electId:"");
			TestClaim testClaimProbablySent=CreateClaim(electId:"");
			List<TestClaim> listTestClaims=new List<TestClaim> { testClaimSent,testClaimDeleted,testClaimHeld,testClaimKept,testClaimProbablySent };
			//1. The batch, as EdsValidationL.ValidateBatchForSend validates it.
			List<EdsValidationResult> listResults=listTestClaims
				.Select(x => EdsClaimValidation.ValidateLocal(x.Claim,clearinghouse,GetQueueItem(x.Claim,clearinghouse))).ToList();
			Assert.IsTrue(listResults.All(x => EdsClaimValidation.GetSendDecision(x)==EdsSendDecision.ConfirmWarnings));
			List<long> listClaimNumsAll=listTestClaims.Select(x => x.Claim.ClaimNum).ToList();
			CollectionAssert.AreEqual(listClaimNumsAll,EdsClaimValidation.GetClaimNumsToSendForBatch(listResults,true));
			//2. While the window is open: sent, deleted, held, and one marked probably sent (still in the queue).
			Claim claimSent=Claims.GetClaim(testClaimSent.Claim.ClaimNum);
			claimSent.ClaimStatus="S";
			Claims.Update(claimSent);
			Claims.Delete(Claims.GetClaim(testClaimDeleted.Claim.ClaimNum));
			Claim claimHeld=Claims.GetClaim(testClaimHeld.Claim.ClaimNum);
			claimHeld.ClaimStatus="H";
			Claims.Update(claimHeld);
			Claim claimProbablySent=Claims.GetClaim(testClaimProbablySent.Claim.ClaimNum);
			claimProbablySent.ClaimStatus="P";
			Claims.Update(claimProbablySent);
			List<long> listClaimNumsExpected=new List<long> { testClaimKept.Claim.ClaimNum,testClaimProbablySent.Claim.ClaimNum };
			//Without a re-validation, the final list still reads the claims again.
			CollectionAssert.AreEqual(listClaimNumsExpected,EdsClaimValidation.GetClaimNumsToSendForBatch(listResults,true));
			CollectionAssert.AreEquivalent(new[] { testClaimSent.Claim.ClaimNum,testClaimDeleted.Claim.ClaimNum,testClaimHeld.Claim.ClaimNum },
				EdsClaimValidation.GetClaimNumsNotWaitingToSend(listClaimNumsAll));
			//3. The window re-validates each claim (Edit Claim, or Re-validate), as FormEdsValidation does from the Send Claims window.
			List<EdsValidationResult> listResultsFinal=listResults
				.Select(x => EdsClaimValidation.MergeRevalidation(x,EdsClaimValidation.ValidateClaimNum(x.ClaimNum,hqEds,EDS.TimeoutValidateOnSend,true)))
				.ToList();
			EdsValidationResult resultSent=listResultsFinal[0];
			Assert.IsTrue(resultSent.IsNotSendable,"Not 'clean, send': the claim was sent already.");
			StringAssert.Contains(resultSent.NotSendableReason,"Sent - Verified");
			Assert.IsFalse(resultSent.ListIssues.Any(x => x.IsResolved),"The earlier warning is not shown as resolved.");
			Assert.AreEqual(testClaimSent.Claim.ClaimNum,resultSent.ClaimNum);
			EdsValidationResult resultDeleted=listResultsFinal[1];
			Assert.IsFalse(resultDeleted.IsSkipped,"Not 'skipped, send as before'.");
			Assert.IsTrue(resultDeleted.IsNotSendable);
			Assert.AreEqual(listResults[1].PatientName,resultDeleted.PatientName,"The batch window still names the deleted claim.");
			Assert.IsTrue(listResultsFinal[2].IsNotSendable);
			Assert.IsFalse(listResultsFinal[3].IsNotSendable);
			Assert.IsFalse(listResultsFinal[4].IsNotSendable,"Probably sent (P) is listed in the Send Claims queue.");
			for(int i=0;i<3;i++) {
				Assert.AreEqual(EdsSendDecision.Block,EdsClaimValidation.GetSendDecision(listResultsFinal[i]));
			}
			CollectionAssert.AreEqual(listClaimNumsExpected,EdsClaimValidation.GetClaimNumsToSend(listResultsFinal,true),"Send Anyway does not send them.");
			CollectionAssert.AreEqual(listClaimNumsExpected,EdsClaimValidation.GetClaimNumsToSendForBatch(listResultsFinal,true));
			CollectionAssert.AreEqual(new long[0],EdsClaimValidation.GetClaimNumsToSendForBatch(listResultsFinal,false),"Warnings need Send Anyway.");
			Assert.IsNotNull(EdsClaimValidation.GetLastResult(testClaimSent.Claim.ClaimNum));
			Assert.IsFalse(EdsClaimValidation.GetLastResult(testClaimSent.Claim.ClaimNum).IsNotSendable,
				"The claim's EDS tab keeps its last real validation.");
			//Edit Claim's own Send may resend a sent claim on purpose: only a deleted claim is refused there.
			Assert.IsFalse(EdsClaimValidation.ValidateClaimNum(testClaimSent.Claim.ClaimNum,hqEds,EDS.TimeoutValidateOnSend).IsNotSendable);
			Assert.IsTrue(EdsClaimValidation.ValidateClaimNum(testClaimDeleted.Claim.ClaimNum,hqEds,EDS.TimeoutValidateOnSend).IsNotSendable);
			Assert.AreEqual(0,_fakeHttpHandler.Requests.Count,"The attachment service is off: no network call.");
		}

		[TestMethod]
		public void ValidateClaimNum_ClearinghouseOverride_IsUsed() {
			CreateEdsClearinghouse(ElectronicClaimFormat.None,true);
			Clearinghouse clearinghouseOther=CreateEdsClearinghouse(ElectronicClaimFormat.None,true,EclaimsCommBridge.None);//Now the default.
			TestClaim testClaim=CreateClaim();
			Assert.IsTrue(EdsClaimValidation.ValidateClaimNum(testClaim.Claim.ClaimNum,0,EDS.TimeoutValidateOnSend).IsSkipped,"The default is not EDS.");
			long hqEds=Clearinghouses.GetDeepCopy().First(x => x.CommBridge==EclaimsCommBridge.EDS).ClearinghouseNum;
			_fakeHttpHandler.Enqueue(HttpStatusCode.OK,EdsTestData.MakeAttachmentAnswer(false));
			EdsValidationResult result=EdsClaimValidation.ValidateClaimNum(testClaim.Claim.ClaimNum,hqEds,EDS.TimeoutValidateOnSend);
			Assert.IsFalse(result.IsSkipped);
			Assert.IsTrue(result.IsRemoteChecked);
			Assert.AreNotEqual(hqEds,clearinghouseOther.ClearinghouseNum);
		}
		#endregion Validation with EDS
	}
}
