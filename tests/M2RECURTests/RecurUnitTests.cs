// Checks of the M2-RECUR helpers that need no database, and source checks of the small hooks this package put into shared files (the hooks run
// inside the card processors' code paths and the WinForms windows, which these tests cannot drive).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace M2RECURTests {
	[TestClass]
	public class RecurUnitTests {
		private static string ReadRepoFile(string relativePath) {
			return File.ReadAllText(TestDb.FindRepoFile(relativePath));
		}

		///<summary>The body of the method that starts with the given signature text, up to the next method at the same indentation.</summary>
		private static string GetMethodBody(string source,string signature) {
			int index=source.IndexOf(signature,StringComparison.Ordinal);
			Assert.IsTrue(index>=0,"Not found: "+signature);
			int indexEnd=source.IndexOf("\n\t\t}",index,StringComparison.Ordinal);
			Assert.IsTrue(indexEnd>index);
			return source.Substring(index,indexEnd-index);
		}

		[TestMethod]
		public void CardLast4_TakesTheEndOfTheMaskedNumber() {
			Assert.AreEqual("1234",RecurringChargeFailEmails.GetCardLast4("XXXXXXXXXXXX1234"));
			Assert.AreEqual("4321",RecurringChargeFailEmails.GetCardLast4(" XXXX4321 "));
			Assert.AreEqual("12",RecurringChargeFailEmails.GetCardLast4("12"));
			Assert.AreEqual("",RecurringChargeFailEmails.GetCardLast4(null));
		}

		[TestMethod]
		public void RemoveDatesBefore_IgnoresTheTimeOfDay() {
			List<DateTime> listDates=new List<DateTime> { new DateTime(2026,3,9),new DateTime(2026,3,10),new DateTime(2026,3,11) };
			RepeatChargeBackdating.RemoveDatesBefore(listDates,new DateTime(2026,3,10,23,59,0));
			CollectionAssert.AreEqual(new List<DateTime> { new DateTime(2026,3,10),new DateTime(2026,3,11) },listDates);
			RepeatChargeBackdating.RemoveDatesBefore(null,DateTime.Today);//No list, no error.
		}

		[TestMethod]
		public void Source_SendChargesSendsTheEmailBeforeSavingEachCharge() {
			string source=ReadRepoFile(Path.Combine("OpenDentBusiness","Data Interface","RecurringCharges.cs")).Replace("\r\n","\n");
			string body=GetMethodBody(source,"public void SendCharges(");
			int indexEmail=body.IndexOf("SendFailEmailIfNeeded(chargeData);",StringComparison.Ordinal);
			int indexUpdate=body.IndexOf("RecurringCharges.Update(chargeData.RecurringCharge);",StringComparison.Ordinal);
			Assert.IsTrue(indexEmail>0,"SendCharges calls the failure e-mail hook.");
			Assert.IsTrue(indexEmail<indexUpdate,"The hook runs before the row is saved, so the outcome line is saved with it.");
			string bodyMarkDeclined=GetMethodBody(source,"private void MarkDeclined(");
			int indexGuard=bodyMarkDeclined.IndexOf("if(chargeData.RecurringCharge.ChargeStatus!=RecurringChargeStatus.ChargeSuccessful) {",StringComparison.Ordinal);
			int indexFlag=bodyMarkDeclined.IndexOf("chargeData.IsDeclinedByProcessor=true;",StringComparison.Ordinal);
			Assert.IsTrue(indexGuard>0 && indexFlag>indexGuard,"MarkDeclined never flags a charge that succeeded.");
			StringAssert.Contains(GetMethodBody(source,"private void ClearStats("),"ClearFailEmailStats();");
			Assert.IsFalse(GetMethodBody(source,"private void MarkFailed(").Contains("IsDeclinedByProcessor"),"A plain failure is not a decline.");
			//PaySimple's catch marks every exception declined; only PaySimple's own decline keeps the flag.
			string bodyPaySimple=GetMethodBody(source,"public void SendPaySimple(");
			int indexMarkDeclined=bodyPaySimple.IndexOf("MarkDeclined(chargeData,Lans.g(_lanThis,\"Error processing card:\")+\" \"+ex.Message,LogLevel.Information);",
				StringComparison.Ordinal);
			int indexPaySimpleDecline=bodyPaySimple.IndexOf("chargeData.IsDeclinedByProcessor=chargeData.IsDeclinedByProcessor && IsPaySimpleDecline(ex);",
				StringComparison.Ordinal);
			Assert.IsTrue(indexMarkDeclined>0 && indexPaySimpleDecline>indexMarkDeclined,"SendPaySimple keeps the flag only for PaySimple's own decline.");
		}

		[TestMethod]
		public void Source_EveryMarkDeclinedIsNarrowedByItsProcessorsFilter() {
			//MarkDeclined flags every call (it also drives "inactivate declined cards"). Each processor's call is followed by the line that keeps the
			//flag only for a card decline, so a setup problem, an outage or a duplicate e-mails nobody.
			string source=ReadRepoFile(Path.Combine("OpenDentBusiness","Data Interface","RecurringCharges.cs")).Replace("\r\n","\n");
			(string Method,string MarkDeclined,string Filter)[] arrayHooks=new[] {
				("public void SendEdgeExpress(",
					"MarkDeclined(chargeData,Lans.g(_lanThis,\"Response from Global Payments V1:\")+\" \"+response.XWebResponseCode.ToString(),LogLevel.Information);",
					"chargeData.IsDeclinedByProcessor=chargeData.IsDeclinedByProcessor && IsEdgeExpressCardDecline(response.XWebResponseCode);"),
				("private bool ProcessCardXChargeClientProgram(",
					"MarkDeclined(chargeData,Lans.g(_lanThis,\"Result from XCharge:\")+\" \"+strBuilderResultText.ToString(),LogLevel.Information);",
					"chargeData.IsDeclinedByProcessor=chargeData.IsDeclinedByProcessor && IsXChargeClientDecline(strBuilderResultText.ToString());"),
				("private bool ProcessCardXWebDTG(",
					"MarkDeclined(chargeData,Lans.g(_lanThis,\"Response from XWeb:\")+\" \"+response.XWebResponseCode.ToString(),LogLevel.Information);",
					"chargeData.IsDeclinedByProcessor=chargeData.IsDeclinedByProcessor && IsEdgeExpressCardDecline(response.XWebResponseCode);"),
				("private bool ProcessCardXWebDTG(",
					"MarkDeclined(chargeData,Lans.g(_lanThis,\"Response from XWeb:\")+\" \"+ex.Message,LogLevel.Information);",
					"chargeData.IsDeclinedByProcessor=chargeData.IsDeclinedByProcessor && IsXWebDtgCardDecline(ex);"),
				("private void SendPayConnectV2(",
					"MarkDeclined(chargeData,errorMessage);",
					"chargeData.IsDeclinedByProcessor=chargeData.IsDeclinedByProcessor && IsPayConnect2Decline(response);"),
			};
			foreach((string Method,string MarkDeclined,string Filter) hook in arrayHooks) {
				string body=GetMethodBody(source,hook.Method);
				int indexMarkDeclined=body.IndexOf(hook.MarkDeclined,StringComparison.Ordinal);
				Assert.IsTrue(indexMarkDeclined>0,"Not found in "+hook.Method+" "+hook.MarkDeclined);
				//The filter is the very next statement.
				string after=body.Substring(indexMarkDeclined+hook.MarkDeclined.Length).TrimStart();
				Assert.IsTrue(after.StartsWith(hook.Filter,StringComparison.Ordinal),hook.Method+" narrows the flag right after "+hook.MarkDeclined);
			}
			//Every MarkDeclined call in the file is one of the six this package narrows (five above, PaySimple's catch in the test before) or the
			//StatusCode!="0" decline in SendPayConnectV2, which stays a decline (an Error response never reaches it: its StatusCode is blank).
			int countMarkDeclinedCalls=Regex.Matches(source,@"\tMarkDeclined\(chargeData,").Count;
			Assert.AreEqual(7,countMarkDeclinedCalls,"A new MarkDeclined call needs its processor's filter.");
			StringAssert.Contains(GetMethodBody(source,"private void SendPayConnectV2("),
				"MarkDeclined(chargeData,Lans.g(_lanThis,\"Transaction Failed, error status\")+\" \"+payConnectResponse.Description,LogLevel.Information);");
		}

		[TestMethod]
		public void Source_GlobalPaymentsV2_MarkDeclinedOnlyForACardDecline() {
			//V-GP's Global Payments V2 branch (RecurringChargerator.GlobalPaymentsV2.cs, merged on port/int-13) calls MarkDeclined only for
			//GpV2Outcome.Declined, the card's or the issuer's decline (GlobalPaymentsV2Result.DecideOutcome). A refused request, a setup problem, a duplicate
			//or an unknown outcome goes through MarkFailed. So V2 needs no filter line after its MarkDeclined call: only its card declines e-mail the patient.
			string source=ReadRepoFile(Path.Combine("OpenDentBusiness","VendorDirect","GlobalPayments","RecurringChargerator.GlobalPaymentsV2.cs"))
				.Replace("\r\n","\n");
			Assert.AreEqual(1,Regex.Matches(source,@"\bMarkDeclined\(").Count,"One MarkDeclined call in the Global Payments V2 file.");
			string body=GetMethodBody(source,"private void SendGlobalPaymentsV2(");
			const string caseDeclined="case GpV2Outcome.Declined:";
			int indexCase=body.IndexOf(caseDeclined,StringComparison.Ordinal);
			Assert.IsTrue(indexCase>0,"The Declined case is in SendGlobalPaymentsV2.");
			Assert.IsTrue(body.Substring(indexCase+caseDeclined.Length).TrimStart().StartsWith("MarkDeclined(chargeData,",StringComparison.Ordinal),
				"The Declined case marks the charge declined.");
			int indexCaseFailed=body.IndexOf("case GpV2Outcome.Failed:",StringComparison.Ordinal);
			Assert.IsTrue(indexCaseFailed>indexCase);
			Assert.IsTrue(body.Substring(indexCaseFailed).TrimStart().Contains("MarkFailed(chargeData,"),"A refused request is a failure, not a decline.");
		}

		[TestMethod]
		public void IsEdgeExpressCardDecline_OnlyTheCardsOwnAnswers() {
			List<XWebResponseCodes> listCardDeclines=Enum.GetValues(typeof(XWebResponseCodes)).Cast<XWebResponseCodes>()
				.Where(x => RecurringChargerator.IsEdgeExpressCardDecline(x)).OrderBy(x => (int)x).ToList();
			CollectionAssert.AreEqual(new List<XWebResponseCodes> { XWebResponseCodes.Declined,XWebResponseCodes.AutoDecline,
				XWebResponseCodes.InvalidExpirationDate },listCardDeclines,"001, 009 and 010 only.");
			//The answers named in the review: setup, credentials, duplicate, outage, partial approval, pending, unknown.
			foreach(XWebResponseCodes code in new[] { XWebResponseCodes.AuthenticationError,XWebResponseCodes.ProductionMerchantSetUpError,
				XWebResponseCodes.TestMerchantSetUpError,XWebResponseCodes.DevelopmentMerchantSetUpError,XWebResponseCodes.DuplicateTransactionError,
				XWebResponseCodes.MerchantLockedForSettlement,XWebResponseCodes.MerchantLockedForMaintenance,XWebResponseCodes.TemporaryServiceOutage,
				XWebResponseCodes.ProcessingHostUnavailable,XWebResponseCodes.PartialApproval,XWebResponseCodes.Pending,XWebResponseCodes.Undefined,
				XWebResponseCodes.Approval,XWebResponseCodes.AliasSuccess })
			{
				Assert.IsFalse(RecurringChargerator.IsEdgeExpressCardDecline(code),code.ToString());
			}
			Assert.AreEqual(XWebResponseCodes.Undefined,XWebResponse.ConvertResponseCode(999),"A code this program does not know.");
		}

		[TestMethod]
		public void IsXWebDtgCardDecline_ReadsTheGatewaysCode() {
			//XWebs throws "DTG failed (code): description" with ErrorCodes.XWebDTGFailed for every answer that is not an approval.
			Assert.IsTrue(RecurringChargerator.IsXWebDtgCardDecline(new CodeBase.ODException("DTG failed (1): DECLINED",
				CodeBase.ODException.ErrorCodes.XWebDTGFailed)));
			Assert.IsTrue(RecurringChargerator.IsXWebDtgCardDecline(new CodeBase.ODException("DTG failed (9): AUTO DECLINE",
				CodeBase.ODException.ErrorCodes.XWebDTGFailed)));
			Assert.IsTrue(RecurringChargerator.IsXWebDtgCardDecline(new CodeBase.ODException("DTG failed (10): Invalid expiration date, declined",
				CodeBase.ODException.ErrorCodes.XWebDTGFailed)));
			//Text that says "declined" but a code that is not the card's, the wrong error code, or not the gateway's exception at all.
			Assert.IsFalse(RecurringChargerator.IsXWebDtgCardDecline(new CodeBase.ODException("DTG failed (813): Duplicate transaction declined",
				CodeBase.ODException.ErrorCodes.XWebDTGFailed)));
			Assert.IsFalse(RecurringChargerator.IsXWebDtgCardDecline(new CodeBase.ODException("DTG failed (805): Authentication declined",
				CodeBase.ODException.ErrorCodes.XWebDTGFailed)));
			Assert.IsFalse(RecurringChargerator.IsXWebDtgCardDecline(new CodeBase.ODException("DTG failed (1): DECLINED",
				CodeBase.ODException.ErrorCodes.XWebProgramProperties)));
			Assert.IsFalse(RecurringChargerator.IsXWebDtgCardDecline(new CodeBase.ODException("The card was declined",
				CodeBase.ODException.ErrorCodes.XWebDTGFailed)));
			Assert.IsFalse(RecurringChargerator.IsXWebDtgCardDecline(new Exception("DTG failed (1): DECLINED")));
			Assert.IsFalse(RecurringChargerator.IsXWebDtgCardDecline(null));
		}

		[TestMethod]
		public void IsXChargeClientDecline_OnlyADeclinedResultLine() {
			Assert.IsTrue(RecurringChargerator.IsXChargeClientDecline("RESULT=DECLINED\r\nDESCRIPTION=Insufficient funds\r\n"));
			Assert.IsTrue(RecurringChargerator.IsXChargeClientDecline("ACCOUNT=XXXXXXXXXXXX1111\nRESULT=Declined\n"));
			//"Transaction not completed", any other result, and a result file without a RESULT line: what MarkDeclined also gets.
			Assert.IsFalse(RecurringChargerator.IsXChargeClientDecline("RESULT=Transaction not completed\r\nDESCRIPTION=Cancelled\r\n"));
			Assert.IsFalse(RecurringChargerator.IsXChargeClientDecline("RESULT=ERROR\r\nDESCRIPTION=The card was declined earlier\r\n"));
			Assert.IsFalse(RecurringChargerator.IsXChargeClientDecline("AVSRESULT=DECLINE\r\nCVRESULT=M\r\n"),"Only the RESULT line counts.");
			Assert.IsFalse(RecurringChargerator.IsXChargeClientDecline(""));
			Assert.IsFalse(RecurringChargerator.IsXChargeClientDecline(null));
		}

		[TestMethod]
		public void IsPayConnect2Decline_OnlyPayConnectTurningTheCardDown() {
			Func<HttpStatusCode,string,PayConnect2.PayConnect2Response> parse=(httpStatusCode,body)
				=> PayConnect2.ParseHttpResponse(PayConnect2.ApiRoute.CreateTransaction,httpStatusCode,body);
			//PayConnect's own error with a 4xx status.
			Assert.IsTrue(RecurringChargerator.IsPayConnect2Decline(parse(HttpStatusCode.PaymentRequired,
				"{\"errorType\":\"RESPONSE\",\"error\":\"Card declined by issuer\"}")));
			Assert.IsTrue(RecurringChargerator.IsPayConnect2Decline(parse(HttpStatusCode.BadRequest,
				"{\"errorType\":\"RESPONSE\",\"error\":{\"message\":\"Transaction declined\",\"code\":5}}")));
			//The office's credentials, a validation error, PayConnect's own failures and a busy or timed-out request.
			Assert.IsFalse(RecurringChargerator.IsPayConnect2Decline(parse(HttpStatusCode.Unauthorized,"{\"error\":\"Authorization field missing\"}")));
			Assert.IsFalse(RecurringChargerator.IsPayConnect2Decline(parse(HttpStatusCode.Unauthorized,
				"{\"errorType\":\"RESPONSE\",\"error\":{\"message\":\"Invalid API key\"}}")));
			Assert.IsFalse(RecurringChargerator.IsPayConnect2Decline(parse(HttpStatusCode.Forbidden,"{\"error\":\"Access to this API has been disallowed\"}")));
			Assert.IsFalse(RecurringChargerator.IsPayConnect2Decline(parse(HttpStatusCode.BadRequest,
				"{\"errorType\":\"VALIDATION\",\"error\":[{\"message\":\"is required\",\"param\":\"expiry\",\"location\":\"body\"}]}")));
			Assert.IsFalse(RecurringChargerator.IsPayConnect2Decline(parse(HttpStatusCode.InternalServerError,"")));
			Assert.IsFalse(RecurringChargerator.IsPayConnect2Decline(parse(HttpStatusCode.BadGateway,"<html>Bad gateway</html>")));
			Assert.IsFalse(RecurringChargerator.IsPayConnect2Decline(parse(HttpStatusCode.ServiceUnavailable,"{\"error\":\"Card declined\"}")),
				"A 5xx is never the card's answer, whatever the text says.");
			Assert.IsFalse(RecurringChargerator.IsPayConnect2Decline(parse((HttpStatusCode)429,"{\"error\":\"Too many requests\"}")));
			Assert.IsFalse(RecurringChargerator.IsPayConnect2Decline(parse(HttpStatusCode.RequestTimeout,"")));
			Assert.IsFalse(RecurringChargerator.IsPayConnect2Decline(parse(HttpStatusCode.Conflict,"{\"error\":\"Duplicate transaction\"}")));
			//A 2xx answer is not an error response at all.
			Assert.IsFalse(RecurringChargerator.IsPayConnect2Decline(parse(HttpStatusCode.OK,"not json")));
			//A lost answer that PayConnect's transaction list shows as declined (as PayConnect2.Reconcile builds it), and one it shows as an error.
			Assert.IsTrue(RecurringChargerator.IsPayConnect2Decline(MakeReconciled("Declined")));
			Assert.IsFalse(RecurringChargerator.IsPayConnect2Decline(MakeReconciled("Error")));
			//No answer, nothing sent, missing settings.
			foreach((PayConnect2.PayConnectRequestOutcome Outcome,PayConnect2.PayConnectErrorKind Kind) pair in new[] {
				(PayConnect2.PayConnectRequestOutcome.SentNoResponse,PayConnect2.PayConnectErrorKind.NoAnswer),
				(PayConnect2.PayConnectRequestOutcome.ResponseUnreadable,PayConnect2.PayConnectErrorKind.NoAnswer),
				(PayConnect2.PayConnectRequestOutcome.NotSent,PayConnect2.PayConnectErrorKind.Network),
				(PayConnect2.PayConnectRequestOutcome.NotSent,PayConnect2.PayConnectErrorKind.NotConfigured),
				(PayConnect2.PayConnectRequestOutcome.SentNoResponse,PayConnect2.PayConnectErrorKind.Vendor) })
			{
				PayConnect2.PayConnect2Response response=new PayConnect2.PayConnect2Response {
					ResponseType=PayConnect2.ResponseType.Error,
					Outcome=pair.Outcome,
					ErrorResponse=new PayConnect2.ErrorResponse { Kind=pair.Kind,HttpStatus=402 },
				};
				Assert.IsFalse(RecurringChargerator.IsPayConnect2Decline(response),pair.Outcome+" "+pair.Kind);
			}
			Assert.IsFalse(RecurringChargerator.IsPayConnect2Decline(new PayConnect2.PayConnect2Response { ResponseType=PayConnect2.ResponseType.Error }));
			Assert.IsFalse(RecurringChargerator.IsPayConnect2Decline(null));
		}

		///<summary>The Error response PayConnect2.Reconcile returns for a lost create that the transaction list shows with a status that is not an
		///approval.</summary>
		private static PayConnect2.PayConnect2Response MakeReconciled(string status) {
			return new PayConnect2.PayConnect2Response {
				ResponseType=PayConnect2.ResponseType.Error,
				Outcome=PayConnect2.PayConnectRequestOutcome.Reconciled,
				httpStatusCode=HttpStatusCode.OK,
				ErrorResponse=new PayConnect2.ErrorResponse {
					Kind=PayConnect2.PayConnectErrorKind.Vendor,
					Message="PayConnect declined the transaction (status: "+status+").",
					VendorCode=status,
				},
			};
		}

		[TestMethod]
		public void IsPaySimpleDecline_OnlyPaySimplesOwnAnswer() {
			//The two messages PaySimpleApi.ToSaleResponse throws (PaySimpleDeclineTests runs them through the real code).
			Assert.IsTrue(RecurringChargerator.IsPaySimpleDecline(new Exception("Payment was declined by PaySimple.\r\nMessage From PaySimple: Insufficient funds")));
			Assert.IsTrue(RecurringChargerator.IsPaySimpleDecline(new Exception("Payment was not approved by PaySimple.  Auth Code from PaySimple:\r\nDECLINE")));
			//A result PaySimple never confirmed (may have been charged), and the office's setup problems.
			Assert.IsFalse(RecurringChargerator.IsPaySimpleDecline(new PaySimpleResultUnknownException("OPMS-0123")));
			Assert.IsFalse(RecurringChargerator.IsPaySimpleDecline(new CodeBase.ODException("PaySimple Username, Key, or PayType is empty.")));
			Assert.IsFalse(RecurringChargerator.IsPaySimpleDecline(new CodeBase.ODException("PaySimple Username or Key is empty.")));
			Assert.IsFalse(RecurringChargerator.IsPaySimpleDecline(new CodeBase.ODException("PaySimple is not enabled.")));
			Assert.IsFalse(RecurringChargerator.IsPaySimpleDecline(new PaySimpleCredentialsException()));
			//An ODException that happens to carry decline text is still not PaySimple's answer.
			Assert.IsFalse(RecurringChargerator.IsPaySimpleDecline(new CodeBase.ODException("Payment was declined by PaySimple.")));
			//PaySimple's error objects, HTTP and transport errors.
			Assert.IsFalse(RecurringChargerator.IsPaySimpleDecline(new PaySimpleException("Invalid input","InvalidInput",new List<string> { "Account id 1 does not exist" })));
			Assert.IsFalse(RecurringChargerator.IsPaySimpleDecline(new PaySimpleHttpException(503,"PaySimple returned HTTP 503")));
			Assert.IsFalse(RecurringChargerator.IsPaySimpleDecline(new System.Net.Http.HttpRequestException("Simulated network failure.")));
			Assert.IsFalse(RecurringChargerator.IsPaySimpleDecline(new System.Net.WebException("The remote name could not be resolved")));
			Assert.IsFalse(RecurringChargerator.IsPaySimpleDecline(new Exception("Could not reach PaySimple. Simulated",new System.Net.Http.HttpRequestException("x"))));
			Assert.IsFalse(RecurringChargerator.IsPaySimpleDecline(new Exception("Unexpected response from PaySimple")));
			//Errors after the card was charged.
			Assert.IsFalse(RecurringChargerator.IsPaySimpleDecline(new NullReferenceException()));
			Assert.IsFalse(RecurringChargerator.IsPaySimpleDecline(null));
		}

		[TestMethod]
		public void Source_BillingDatesHelperAppliesTheBackdatingPreference() {
			string source=ReadRepoFile(Path.Combine("OpenDentBusiness","Data Interface","RepeatCharges.cs")).Replace("\r\n","\n");
			string body=GetMethodBody(source,"public static List<DateTime> GetBillingDatesHelper(");
			int indexAfterToday=body.IndexOf("retVal.RemoveAll(x => x > dateRun);",StringComparison.Ordinal);
			int indexBackdating=body.IndexOf("RepeatChargeBackdating.RemoveBackdatedDates(retVal,dateRun);",StringComparison.Ordinal);
			Assert.IsTrue(indexAfterToday>0 && indexBackdating>indexAfterToday);
		}

		[TestMethod]
		public void Source_PreferencesWindowHasTheControlsPrefInfosNames() {
			string designer=ReadRepoFile(Path.Combine("OpenDental","User Controls","Preferences","UserControlAccountRecAndRepCharges.Designer.cs"));
			string code=ReadRepoFile(Path.Combine("OpenDental","User Controls","Preferences","UserControlAccountRecAndRepCharges.cs"));
			XDocument xDocument=XDocument.Load(TestDb.FindRepoFile(Path.Combine("OpenDental","Resources","PrefInfos.xml")));
			foreach(PrefName prefName in new[] { PrefName.RecurringChargesFailEmailEnabled,PrefName.RepeatingChargesAllowBackdating }) {
				XElement xElement=xDocument.Root.Elements("PrefInf").Single(x => (string)x.Element("PrefName")==prefName.ToString());
				string controlName=(string)xElement.Element("ControlName");
				string controlText=(string)xElement.Element("ControlText");
				StringAssert.Contains(designer,"this."+controlName+".Name = \""+controlName+"\";");
				StringAssert.Contains(designer,"this."+controlName+".Text = \""+controlText+"\";","The label matches the hover help.");
				StringAssert.Contains(code,controlName+".Checked=PrefC.GetBool(PrefName."+prefName+");");
				StringAssert.Contains(code,"Prefs.UpdateBool(PrefName."+prefName+","+controlName+".Checked);");
			}
			StringAssert.Contains(code,"Prefs.UpdateString(PrefName.RecurringChargesFailEmailSubject,_failEmailSubject);");
			StringAssert.Contains(code,"Prefs.UpdateString(PrefName.RecurringChargesFailEmailBody,_failEmailBody);");
			StringAssert.Contains(designer,"this.butRecurringChargesFailEmailTemplate.Click += new System.EventHandler(this.butRecurringChargesFailEmailTemplate_Click);");
			//The stored template text is loaded as it is, so saving the window with a blank template keeps it blank.
			StringAssert.Contains(code,"_failEmailSubject=PrefC.GetStringSilent(PrefName.RecurringChargesFailEmailSubject);");
			StringAssert.Contains(code,"_failEmailBody=PrefC.GetStringSilent(PrefName.RecurringChargesFailEmailBody);");
			//"Hide repeating charges" disables the backdating box with the other repeating charge settings.
			string codeLf=code.Replace("\r\n","\n");
			int indexEasyHide=codeLf.IndexOf("if(PrefC.GetBool(PrefName.EasyHideRepeatCharges)) {",StringComparison.Ordinal);
			Assert.IsTrue(indexEasyHide>0);
			string blockEasyHide=codeLf.Substring(indexEasyHide,codeLf.IndexOf("\n\t\t\t}",indexEasyHide,StringComparison.Ordinal)-indexEasyHide);
			StringAssert.Contains(blockEasyHide,"checkRepeatingChargesAllowBackdating.Enabled=false;");
		}

		[TestMethod]
		public void Source_WindowsUseTheNewRules() {
			string formRecurring=ReadRepoFile(Path.Combine("OpenDental","Forms","FormCreditRecurringCharges.cs"));
			StringAssert.Contains(formRecurring,"_recurringChargerator.GetFailEmailSummary()","The run summary shows the e-mail counts.");
			string formEdit=ReadRepoFile(Path.Combine("OpenDental","Forms","FormRepeatChargeEdit.cs"));
			StringAssert.Contains(formEdit,"RepeatChargeBackdating.IsStartDateSkipped(_repeatCharge.DateStart,DateTime.Today)");
			string formUpdate=ReadRepoFile(Path.Combine("OpenDental","Forms","FormRepeatChargesUpdate.cs"));
			StringAssert.Contains(formUpdate,"RepeatChargeBackdating.IsAllowed()");
			string formTemplate=ReadRepoFile(Path.Combine("OpenDental","Forms","FormRecurringChargeFailEmailTemplate.cs"));
			StringAssert.Contains(formTemplate,"RecurringChargeFailEmails.ListTags");
			string csproj=ReadRepoFile(Path.Combine("OpenDental","OpenDental.csproj"));
			StringAssert.Contains(csproj,"Forms\\FormRecurringChargeFailEmailTemplate.cs");
			StringAssert.Contains(csproj,"Forms\\FormRecurringChargeFailEmailTemplate.Designer.cs");
		}

		[TestMethod]
		public void Source_SendsOnlyThroughTheOfficesOwnAddress() {
			//Ground rule 6: no hosted service. The e-mail goes through EmailMessages.SendEmail with the office's EmailAddress row.
			string source=ReadRepoFile(Path.Combine("OpenDentBusiness","Data Interface","RecurringChargeFailEmails.cs"));
			StringAssert.Contains(source,"EmailMessages.SendEmail(emailMessage,emailAddress)");
			StringAssert.Contains(source,"EmailAddresses.GetByClinic(clinicNum,allowNullReturn:true)");
			Assert.IsFalse(Regex.IsMatch(source,@"https?://",RegexOptions.IgnoreCase),"No web address in the sending code.");
		}
	}
}
