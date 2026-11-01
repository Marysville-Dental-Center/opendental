// HTML dunning and billing e-mails (Open Dental 26.1, item v26_1-manage-dunning-html; backlog package M2-DUN): statement e-mails built from a
// dunning message's e-mail override or from Billing Defaults, in each EmailType (plain text, HTML inside the master template, raw HTML), the way
// billing and the statement window build them (Statements.GetEmailMessageForStatement), and the autograph that billing adds.
// Runs on a scratch database built from the 24.3.63 seed plus the fork migrations, with synthetic patients and statements only. Skipped
// (inconclusive) when the local MariaDB does not answer.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.Email;

namespace M2DUNTests {
	[TestClass]
	public class DunningEmailRenderTests {
		private static readonly string DB_NAME=TestDb.ScratchName("render");
		private static string _skipReason;
		///<summary>The BillingEmailType preference row as the fork migrations left it, before any test changed it. Null when there is no row.</summary>
		private static string _billingEmailTypeSeeded;
		///<summary>A marker in the e-mail master template these tests use, so a test can tell whether the body was placed in it.</summary>
		private const string MASTER_MARKER="<!--m2dun master-->";
		private const string MASTER_TEMPLATE="<html><head><title>Statement</title></head>"+MASTER_MARKER+"@@@body@@@</html>";

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=TestDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			TestDb.DropStaleScratchDatabases();
			TestDb.CreateMigrated(DB_NAME);
			_billingEmailTypeSeeded=DataCore.GetScalar("SELECT ValueString FROM preference WHERE PrefName='BillingEmailType'");
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null || Environment.GetEnvironmentVariable("M2DUN_KEEP_DB")=="1") {
				return;
			}
			try {
				TestDb.Drop(DB_NAME);
			}
			catch(Exception) {
				//A later run drops it once it is stale.
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			TestDb.Connect(DB_NAME);
			Security.CurUser=Security.CurUser??new Userod();
			Prefs.UpdateString(PrefName.EmailMasterTemplate,MASTER_TEMPLATE);
			Prefs.UpdateInt(PrefName.BillingEmailType,(int)EmailType.Regular);
			Prefs.UpdateString(PrefName.BillingEmailSubject,"Statement for [nameF]");
			Prefs.UpdateString(PrefName.BillingEmailBodyText,"Dear [nameF], please see the attached statement.");
			Prefs.UpdateBool(PrefName.BillingEmailIncludeAutograph,false);
			Cache.Refresh(InvalidType.Prefs);
			DataCore.NonQ("DELETE FROM dunning");
		}

		#region Helpers
		///<summary>A made-up guarantor with an e-mail address.</summary>
		private static Patient CreatePatient(string fName) {
			Patient patient=new Patient();
			patient.LName="Dunning"+DateTime.Now.Ticks.ToString().Substring(10);
			patient.FName=fName;
			patient.PatStatus=PatientStatus.Patient;
			patient.Email="guarantor@example.test";
			patient.Address="1 Test Street";
			patient.City="Testville";
			patient.State="OR";
			patient.Zip="97000";
			patient.BillingType=PrefC.GetLong(PrefName.PracticeDefaultBillType);
			patient.Birthdate=new DateTime(1980,1,1);
			patient.PatNum=OpenDentBusiness.Crud.PatientCrud.Insert(patient);
			Patient patientOld=patient.Copy();
			patient.Guarantor=patient.PatNum;
			OpenDentBusiness.Crud.PatientCrud.Update(patient,patientOld);
			return patient;
		}

		private static Dunning InsertDunning(string emailSubject,string emailBody,EmailType emailType) {
			Dunning dunning=new Dunning();
			dunning.DunMessage="Your account is past due.";
			dunning.MessageBold="";
			dunning.EmailSubject=emailSubject;
			dunning.EmailBody=emailBody;
			dunning.EmailType=emailType;
			Dunnings.Insert(dunning);
			return dunning;
		}

		///<summary>An e-mail statement made the way billing makes one: the dunning message's e-mail override is copied to the statement.</summary>
		private static Statement InsertStatement(Patient patient,Dunning dunning) {
			Statement statement=new Statement();
			statement.PatNum=patient.PatNum;
			statement.DateSent=DateTime.Today;
			statement.DateRangeFrom=DateTime.Today.AddMonths(-1);
			statement.DateRangeTo=DateTime.Today;
			statement.Mode_=StatementMode.Email;
			statement.StatementType=StmtType.NotSet;
			statement.Note="";
			statement.NoteBold="";
			statement.IsBalValid=true;
			statement.BalTotal=123.45;
			statement.InsEst=0;
			if(dunning!=null) {
				statement.Note=dunning.DunMessage;
				statement.EmailSubject=dunning.EmailSubject;
				statement.EmailBody=dunning.EmailBody;
			}
			Statements.Insert(statement);
			return statement;
		}

		private static EmailMessage BuildEmail(Statement statement,Patient patient) {
			EmailAddress emailAddress=new EmailAddress();
			emailAddress.SenderAddress="billing@example.test";
			emailAddress.EmailUsername="billing@example.test";
			return Statements.GetEmailMessageForStatement(statement,patient,emailAddress);
		}

		private static void SetBillingDefaults(string body,EmailType emailType) {
			Prefs.UpdateString(PrefName.BillingEmailBodyText,body);
			Prefs.UpdateInt(PrefName.BillingEmailType,(int)emailType);
			Cache.Refresh(InvalidType.Prefs);
		}
		#endregion Helpers

		#region Schema and preference
		[TestMethod]
		public void Preference_BillingEmailTypeIsSeededAsPlainText() {
			Assert.AreEqual("0",_billingEmailTypeSeeded);
		}

		[TestMethod]
		public void Dunning_EmailTypeIsSavedAndLoaded() {
			Dunning dunning=InsertDunning("Subject","<html><body>Raw</body></html>",EmailType.RawHtml);
			Assert.AreEqual("2",DataCore.GetScalar("SELECT EmailType FROM dunning WHERE DunningNum="+POut.Long(dunning.DunningNum)));
			Dunning dunningDb=Dunnings.Refresh(null).Single(x => x.DunningNum==dunning.DunningNum);
			Assert.AreEqual(EmailType.RawHtml,dunningDb.EmailType);
			dunningDb.EmailType=EmailType.Html;
			Dunnings.Update(dunningDb);
			Assert.AreEqual(EmailType.Html,Dunnings.Refresh(null).Single(x => x.DunningNum==dunning.DunningNum).EmailType);
		}
		#endregion Schema and preference

		#region Dunning e-mail override in each EmailType
		[TestMethod]
		public void DunningOverride_PlainTextIsSentAsBefore() {
			Patient patient=CreatePatient("Ann");
			Dunning dunning=InsertDunning("Past due, [nameF]","Hello [nameF], your balance is [StatementBalance].",EmailType.Regular);
			EmailMessage emailMessage=BuildEmail(InsertStatement(patient,dunning),patient);
			Assert.AreEqual(EmailType.Regular,emailMessage.HtmlType);
			Assert.AreEqual("Hello Ann, your balance is 123.45.",emailMessage.BodyText);
			Assert.IsTrue(string.IsNullOrEmpty(emailMessage.HtmlText));
			Assert.AreEqual("Past due, Ann",emailMessage.Subject);
			Assert.AreEqual(EmailMessageSource.Statement,emailMessage.MsgType);
			BasicEmailMessage basicEmailMessage=EmailMessages.ODEmailMessageToBasic(emailMessage);
			Assert.IsFalse(basicEmailMessage.IsHtml);
			StringAssert.Contains(basicEmailMessage.BodyText,"Hello Ann");
		}

		[TestMethod]
		public void DunningOverride_HtmlIsPlacedInTheMasterTemplate() {
			Patient patient=CreatePatient("Ann");
			Dunning dunning=InsertDunning("Past due, [nameF]","Hello [nameF],\r\n<b>Your balance is [StatementBalance].</b>",EmailType.Html);
			EmailMessage emailMessage=BuildEmail(InsertStatement(patient,dunning),patient);
			Assert.AreEqual(EmailType.Html,emailMessage.HtmlType);
			//BodyText keeps the markup, tags replaced, as an HTML template's message does.
			Assert.AreEqual("Hello Ann,\r\n<b>Your balance is 123.45.</b>",emailMessage.BodyText);
			StringAssert.StartsWith(emailMessage.HtmlText,"<html><head><title>Statement</title></head>"+MASTER_MARKER);
			StringAssert.Contains(emailMessage.HtmlText,"Hello Ann,");
			StringAssert.Contains(emailMessage.HtmlText,"<b>Your balance is 123.45.</b>");
			Assert.IsFalse(emailMessage.HtmlText.Contains("@@@body@@@"));
			Assert.AreEqual("Past due, Ann",emailMessage.Subject);//The subject stays plain text.
			BasicEmailMessage basicEmailMessage=EmailMessages.ODEmailMessageToBasic(emailMessage);
			Assert.IsTrue(basicEmailMessage.IsHtml);
			StringAssert.Contains(basicEmailMessage.HtmlBody,"<b>Your balance is 123.45.</b>");
			StringAssert.Contains(basicEmailMessage.HtmlBody,MASTER_MARKER);
		}

		[TestMethod]
		public void DunningOverride_RawHtmlIsSentAsWritten() {
			Patient patient=CreatePatient("Ann");
			string body="<html><body><h1>Statement</h1><p>Hello [nameF]</p><p>Balance [StatementBalance], account [PatNum]</p></body></html>";
			Dunning dunning=InsertDunning("Past due",body,EmailType.RawHtml);
			EmailMessage emailMessage=BuildEmail(InsertStatement(patient,dunning),patient);
			string expected="<html><body><h1>Statement</h1><p>Hello Ann</p><p>Balance 123.45, account "+patient.PatNum+"</p></body></html>";
			Assert.AreEqual(EmailType.RawHtml,emailMessage.HtmlType);
			Assert.AreEqual(expected,emailMessage.HtmlText);
			Assert.AreEqual(expected,emailMessage.BodyText);
			Assert.IsFalse(emailMessage.HtmlText.Contains(MASTER_MARKER));
			BasicEmailMessage basicEmailMessage=EmailMessages.ODEmailMessageToBasic(emailMessage);
			Assert.IsTrue(basicEmailMessage.IsHtml);
			StringAssert.Contains(basicEmailMessage.HtmlBody,"<p>Hello Ann</p>");
		}

		///<summary>A value with "&lt;" or "&gt;" can't break the HTML of either HTML type.</summary>
		[TestMethod]
		public void DunningOverride_TagValuesAreEscapedInHtml() {
			Patient patient=CreatePatient("Ann<x>");
			Dunning dunningHtml=InsertDunning("","<b>Hello [nameF]</b>",EmailType.Html);
			Dunning dunningRaw=InsertDunning("","<p>Hello [nameF]</p>",EmailType.RawHtml);
			EmailMessage emailMessageHtml=BuildEmail(InsertStatement(patient,dunningHtml),patient);
			EmailMessage emailMessageRaw=BuildEmail(InsertStatement(patient,dunningRaw),patient);
			Assert.AreEqual(EmailType.Html,emailMessageHtml.HtmlType);
			StringAssert.Contains(emailMessageHtml.HtmlText,"<b>Hello Ann&lt;x&gt;</b>");
			Assert.AreEqual(EmailType.RawHtml,emailMessageRaw.HtmlType);
			Assert.AreEqual("<p>Hello Ann&lt;x&gt;</p>",emailMessageRaw.HtmlText);
		}

		///<summary>The dunning message was edited after billing made the statement, so its override no longer matches: the statement's own text is
		///sent as plain text, as before 26.1.</summary>
		[TestMethod]
		public void DunningOverride_EditedAfterBillingIsSentAsPlainText() {
			Patient patient=CreatePatient("Ann");
			Dunning dunning=InsertDunning("","<b>Past due</b>",EmailType.Html);
			Statement statement=InsertStatement(patient,dunning);
			dunning.EmailBody="<b>Past due!</b>";
			Dunnings.Update(dunning);
			EmailMessage emailMessage=BuildEmail(statement,patient);
			Assert.AreEqual(EmailType.Regular,emailMessage.HtmlType);
			Assert.AreEqual("<b>Past due</b>",emailMessage.BodyText);
		}

		///<summary>A dunning message with only a subject override: the body, and its type, come from Billing Defaults.</summary>
		[TestMethod]
		public void DunningOverride_SubjectOnlyUsesTheBillingDefaultsBodyAndType() {
			SetBillingDefaults("<i>Dear [nameF]</i>",EmailType.Html);
			Patient patient=CreatePatient("Ann");
			Dunning dunning=InsertDunning("Final notice","",EmailType.RawHtml);
			EmailMessage emailMessage=BuildEmail(InsertStatement(patient,dunning),patient);
			Assert.AreEqual("Final notice",emailMessage.Subject);
			Assert.AreEqual(EmailType.Html,emailMessage.HtmlType);
			StringAssert.Contains(emailMessage.HtmlText,"<i>Dear Ann</i>");
			StringAssert.Contains(emailMessage.HtmlText,MASTER_MARKER);
		}

		///<summary>An HTML body that is not valid markup (the windows refuse to save one) is sent as plain text rather than not at all.</summary>
		[TestMethod]
		public void DunningOverride_InvalidHtmlFallsBackToPlainText() {
			Patient patient=CreatePatient("Ann");
			Dunning dunning=InsertDunning("","Balance < [StatementBalance]",EmailType.Html);
			EmailMessage emailMessage=BuildEmail(InsertStatement(patient,dunning),patient);
			Assert.AreEqual(EmailType.Regular,emailMessage.HtmlType);
			Assert.AreEqual("Balance < 123.45",emailMessage.BodyText);
		}
		#endregion Dunning e-mail override in each EmailType

		#region Billing Defaults
		[TestMethod]
		public void BillingDefaults_EachEmailType() {
			Patient patient=CreatePatient("Ann");
			Statement statement=InsertStatement(patient,null);
			SetBillingDefaults("Dear [nameF], balance [StatementBalance].",EmailType.Regular);
			EmailMessage emailMessage=BuildEmail(statement,patient);
			Assert.AreEqual(EmailType.Regular,emailMessage.HtmlType);
			Assert.AreEqual("Dear Ann, balance 123.45.",emailMessage.BodyText);
			Assert.AreEqual("Statement for Ann",emailMessage.Subject);
			SetBillingDefaults("Dear [nameF],\r\n<u>balance [StatementBalance]</u>",EmailType.Html);
			emailMessage=BuildEmail(statement,patient);
			Assert.AreEqual(EmailType.Html,emailMessage.HtmlType);
			StringAssert.Contains(emailMessage.HtmlText,MASTER_MARKER);
			StringAssert.Contains(emailMessage.HtmlText,"<u>balance 123.45</u>");
			SetBillingDefaults("<html><body>Dear [nameF]</body></html>",EmailType.RawHtml);
			emailMessage=BuildEmail(statement,patient);
			Assert.AreEqual(EmailType.RawHtml,emailMessage.HtmlType);
			Assert.AreEqual("<html><body>Dear Ann</body></html>",emailMessage.HtmlText);
			Assert.AreEqual(EmailType.RawHtml,BillingEmailHtml.GetBillingDefaultsEmailType());
		}

		[TestMethod]
		public void BillingDefaults_UnknownStoredTypeIsPlainText() {
			Prefs.UpdateString(PrefName.BillingEmailType,"9");
			Cache.Refresh(InvalidType.Prefs);
			Assert.AreEqual(EmailType.Regular,BillingEmailHtml.GetBillingDefaultsEmailType());
		}
		#endregion Billing Defaults

		#region Autographs (billing adds the sender's autograph when Billing Defaults asks for it)
		[TestMethod]
		public void Autograph_HtmlEmailGetsItInTheMarkup() {
			Patient patient=CreatePatient("Ann");
			Dunning dunning=InsertDunning("","<b>Past due</b>",EmailType.Html);
			EmailMessage emailMessage=BuildEmail(InsertStatement(patient,dunning),patient);
			EmailAutograph emailAutograph=new EmailAutograph { Description="Desk",AutographText="Front Desk <billing@example.test>" };
			Assert.IsTrue(BillingEmailHtml.TryInsertAutograph(emailMessage,emailAutograph));
			Assert.AreEqual(EmailType.Html,emailMessage.HtmlType);
			StringAssert.Contains(emailMessage.HtmlText,MASTER_MARKER);
			StringAssert.Contains(emailMessage.HtmlText,"<b>Past due</b>");
			StringAssert.Contains(emailMessage.HtmlText,"Front Desk &lt;billing@example.test&gt;");
			//An HTML autograph keeps its tags.
			emailMessage=BuildEmail(InsertStatement(patient,dunning),patient);
			emailAutograph=new EmailAutograph { Description="Desk",AutographText="<i>Front Desk</i>" };
			Assert.IsTrue(BillingEmailHtml.TryInsertAutograph(emailMessage,emailAutograph));
			StringAssert.Contains(emailMessage.HtmlText,"<i>Front Desk</i>");
		}

		[TestMethod]
		public void Autograph_RawHtmlEmailGetsItBeforeTheClosingBodyTag() {
			Patient patient=CreatePatient("Ann");
			Dunning dunning=InsertDunning("","<html><body><p>Past due</p></body></html>",EmailType.RawHtml);
			EmailMessage emailMessage=BuildEmail(InsertStatement(patient,dunning),patient);
			EmailAutograph emailAutograph=new EmailAutograph { Description="Desk",AutographText="<i>Front Desk</i>" };
			Assert.IsTrue(BillingEmailHtml.TryInsertAutograph(emailMessage,emailAutograph));
			StringAssert.StartsWith(emailMessage.HtmlText,"<html><body><p>Past due</p><br>\r\n");
			StringAssert.Contains(emailMessage.HtmlText,"<i>Front Desk</i>");
			StringAssert.EndsWith(emailMessage.HtmlText,"</body></html>");
		}

		[TestMethod]
		public void Autograph_PlainTextEmailIsLeftToBilling() {
			Patient patient=CreatePatient("Ann");
			Dunning dunning=InsertDunning("","Past due",EmailType.Regular);
			EmailMessage emailMessage=BuildEmail(InsertStatement(patient,dunning),patient);
			Assert.IsFalse(BillingEmailHtml.TryInsertAutograph(emailMessage,new EmailAutograph { AutographText="Front Desk" }));
			Assert.AreEqual("Past due",emailMessage.BodyText);
		}
		#endregion Autographs
	}
}
