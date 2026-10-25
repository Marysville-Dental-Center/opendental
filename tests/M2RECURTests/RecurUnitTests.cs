// Checks of the M2-RECUR helpers that need no database, and source checks of the small hooks this package put into shared files (the hooks run
// inside the card processors' code paths and the WinForms windows, which these tests cannot drive).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
