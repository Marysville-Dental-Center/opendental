// HTML dunning and billing e-mails (Open Dental 26.1, item v26_1-manage-dunning-html; backlog package M2-DUN): the parts of BillingEmailHtml
// that need no database. How a statement finds the e-mail type of its body, raw HTML escapes, and autographs in raw HTML e-mails.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace M2DUNTests {
	[TestClass]
	public class BillingEmailHtmlTests {
		#region Helpers
		private static Dunning NewDunning(string emailSubject,string emailBody,EmailType emailType,long clinicNum=0) {
			Dunning dunning=new Dunning();
			dunning.EmailSubject=emailSubject;
			dunning.EmailBody=emailBody;
			dunning.EmailType=emailType;
			dunning.ClinicNum=clinicNum;
			dunning.DunMessage="";
			dunning.MessageBold="";
			return dunning;
		}

		private static EmailMessage NewMessage(string bodyText,EmailType emailType,string htmlText="") {
			EmailMessage emailMessage=new EmailMessage();
			emailMessage.BodyText=bodyText;
			emailMessage.HtmlType=emailType;
			emailMessage.HtmlText=htmlText;
			return emailMessage;
		}

		private static EmailAutograph NewAutograph(string text) {
			EmailAutograph emailAutograph=new EmailAutograph();
			emailAutograph.Description="Front desk";
			emailAutograph.AutographText=text;
			return emailAutograph;
		}
		#endregion Helpers

		#region EmailType
		///<summary>dunning.EmailType and the BillingEmailType preference store these numbers (OpenDentalDiffDocumentation26-1.xml: Regular 0,
		///Html 1, RawHtml 2).</summary>
		[TestMethod]
		public void EmailType_KeepsThePublishedNumbers() {
			Assert.AreEqual(0,(int)EmailType.Regular);
			Assert.AreEqual(1,(int)EmailType.Html);
			Assert.AreEqual(2,(int)EmailType.RawHtml);
		}

		[TestMethod]
		public void ToEmailType_UnknownNumbersArePlainText() {
			Assert.AreEqual(EmailType.Regular,BillingEmailHtml.ToEmailType(0));
			Assert.AreEqual(EmailType.Html,BillingEmailHtml.ToEmailType(1));
			Assert.AreEqual(EmailType.RawHtml,BillingEmailHtml.ToEmailType(2));
			Assert.AreEqual(EmailType.Regular,BillingEmailHtml.ToEmailType(3));
			Assert.AreEqual(EmailType.Regular,BillingEmailHtml.ToEmailType(-1));
		}
		#endregion EmailType

		#region FindDunningEmailType
		[TestMethod]
		public void FindDunningEmailType_TakesTheTypeOfTheDunningTheBodyCameFrom() {
			List<Dunning> listDunnings=new List<Dunning> {
				NewDunning("","Plain reminder",EmailType.Regular),
				NewDunning("","<b>Past due</b>",EmailType.Html),
				NewDunning("","<html><body>Final notice</body></html>",EmailType.RawHtml),
			};
			Assert.AreEqual(EmailType.Regular,BillingEmailHtml.FindDunningEmailType("","Plain reminder",0,listDunnings));
			Assert.AreEqual(EmailType.Html,BillingEmailHtml.FindDunningEmailType("","<b>Past due</b>",0,listDunnings));
			Assert.AreEqual(EmailType.RawHtml,BillingEmailHtml.FindDunningEmailType("","<html><body>Final notice</body></html>",0,listDunnings));
		}

		///<summary>The dunning message was changed or deleted after the statement was made: the statement's body is sent as plain text, as before
		///26.1.</summary>
		[TestMethod]
		public void FindDunningEmailType_NoMatchingDunningIsPlainText() {
			List<Dunning> listDunnings=new List<Dunning> { NewDunning("","<b>Past due</b> (edited)",EmailType.Html) };
			Assert.AreEqual(EmailType.Regular,BillingEmailHtml.FindDunningEmailType("","<b>Past due</b>",0,listDunnings));
			Assert.AreEqual(EmailType.Regular,BillingEmailHtml.FindDunningEmailType("","",0,listDunnings));
			Assert.AreEqual(EmailType.Regular,BillingEmailHtml.FindDunningEmailType("","<b>Past due</b>",0,null));
		}

		[TestMethod]
		public void FindDunningEmailType_SameBodyPrefersSameSubjectThenClinicThenAllClinics() {
			string body="Your account is past due.";
			//Same subject wins over the clinic.
			List<Dunning> listDunnings=new List<Dunning> {
				NewDunning("Other subject",body,EmailType.Regular,clinicNum:7),
				NewDunning("Past due",body,EmailType.Html,clinicNum:3),
			};
			Assert.AreEqual(EmailType.Html,BillingEmailHtml.FindDunningEmailType("Past due",body,7,listDunnings));
			//Same subject: the patient's clinic wins over all clinics (-2), which wins over another clinic.
			listDunnings=new List<Dunning> {
				NewDunning("Past due",body,EmailType.Regular,clinicNum:3),
				NewDunning("Past due",body,EmailType.RawHtml,clinicNum:-2),
			};
			Assert.AreEqual(EmailType.RawHtml,BillingEmailHtml.FindDunningEmailType("Past due",body,7,listDunnings));
			listDunnings.Add(NewDunning("Past due",body,EmailType.Html,clinicNum:7));
			Assert.AreEqual(EmailType.Html,BillingEmailHtml.FindDunningEmailType("Past due",body,7,listDunnings));
			//A full tie goes to the last one, as billing picks the last matching dunning.
			listDunnings=new List<Dunning> {
				NewDunning("Past due",body,EmailType.Regular),
				NewDunning("Past due",body,EmailType.Html),
			};
			Assert.AreEqual(EmailType.Html,BillingEmailHtml.FindDunningEmailType("Past due",body,0,listDunnings));
		}

		[TestMethod]
		public void FindDunningEmailType_BlankAndNullSubjectsAreTheSame() {
			List<Dunning> listDunnings=new List<Dunning> {
				NewDunning("Has subject","Body",EmailType.Regular),
				NewDunning(null,"Body",EmailType.Html),
			};
			Assert.AreEqual(EmailType.Html,BillingEmailHtml.FindDunningEmailType("","Body",0,listDunnings));
		}
		#endregion FindDunningEmailType

		#region ApplyEmailType
		[TestMethod]
		public void ApplyEmailType_RegularLeavesTheEmailAsBefore() {
			EmailMessage emailMessage=NewMessage("Hello &<you&>",EmailType.Regular);
			BillingEmailHtml.ApplyEmailType(emailMessage,EmailType.Regular);
			Assert.AreEqual("Hello &<you&>",emailMessage.BodyText);
			Assert.AreEqual(EmailType.Regular,emailMessage.HtmlType);
			Assert.AreEqual("",emailMessage.HtmlText);
		}

		///<summary>Raw HTML is sent as written; the escapes that tag replacement puts around "&lt;" and "&gt;" in a value become entities.</summary>
		[TestMethod]
		public void ApplyEmailType_RawHtmlIsSentAsWritten() {
			EmailMessage emailMessage=NewMessage("<html><body><p>Hello Ann&<x&></p></body></html>",EmailType.Regular);
			BillingEmailHtml.ApplyEmailType(emailMessage,EmailType.RawHtml);
			Assert.AreEqual(EmailType.RawHtml,emailMessage.HtmlType);
			Assert.AreEqual("<html><body><p>Hello Ann&lt;x&gt;</p></body></html>",emailMessage.HtmlText);
			Assert.AreEqual(emailMessage.HtmlText,emailMessage.BodyText);
			Assert.IsTrue(emailMessage.AreImagesDownloaded);
			Assert.IsTrue(EmailMessages.IsHtmlEmail(emailMessage.HtmlType));
		}

		[TestMethod]
		public void ConvertMarkupEscapesToHtml() {
			Assert.AreEqual("a &lt;b&gt; c &amp; d",BillingEmailHtml.ConvertMarkupEscapesToHtml("a &<b&> c &amp; d"));
			Assert.AreEqual("",BillingEmailHtml.ConvertMarkupEscapesToHtml(""));
			Assert.IsNull(BillingEmailHtml.ConvertMarkupEscapesToHtml(null));
		}
		#endregion ApplyEmailType

		#region Autographs
		[TestMethod]
		public void TryInsertAutograph_PlainTextEmailIsLeftToBilling() {
			EmailMessage emailMessage=NewMessage("Hello",EmailType.Regular);
			Assert.IsFalse(BillingEmailHtml.TryInsertAutograph(emailMessage,NewAutograph("Front Desk")));
			Assert.AreEqual("Hello",emailMessage.BodyText);
			Assert.AreEqual(EmailType.Regular,emailMessage.HtmlType);
		}

		[TestMethod]
		public void TryInsertAutograph_RawHtmlGetsTheAutographBeforeTheClosingBodyTag() {
			string html="<html><body><p>Balance due</p></BODY></html>";
			EmailMessage emailMessage=NewMessage(html,EmailType.RawHtml,html);
			Assert.IsTrue(BillingEmailHtml.TryInsertAutograph(emailMessage,NewAutograph("Front Desk & Billing\r\nPhone 555-0100")));
			Assert.AreEqual("<html><body><p>Balance due</p><br>\r\nFront Desk &amp; Billing<br>\r\nPhone 555-0100\r\n</BODY></html>",emailMessage.BodyText);
			Assert.AreEqual(emailMessage.BodyText,emailMessage.HtmlText);
			Assert.AreEqual(EmailType.RawHtml,emailMessage.HtmlType);
		}

		[TestMethod]
		public void TryInsertAutograph_RawHtmlWithoutBodyTagGetsItAtTheEnd() {
			EmailMessage emailMessage=NewMessage("<p>Balance due</p>",EmailType.RawHtml,"<p>Balance due</p>");
			Assert.IsTrue(BillingEmailHtml.TryInsertAutograph(emailMessage,NewAutograph("Front Desk")));
			Assert.AreEqual("<p>Balance due</p><br>\r\nFront Desk",emailMessage.HtmlText);
		}

		[TestMethod]
		public void TryInsertAutograph_NoAutographLeavesAnHtmlEmailAlone() {
			EmailMessage emailMessage=NewMessage("<p>Balance due</p>",EmailType.RawHtml,"<p>Balance due</p>");
			Assert.IsTrue(BillingEmailHtml.TryInsertAutograph(emailMessage,null));
			Assert.IsTrue(BillingEmailHtml.TryInsertAutograph(emailMessage,NewAutograph("  ")));
			Assert.AreEqual("<p>Balance due</p>",emailMessage.HtmlText);
			Assert.AreEqual("<p>Balance due</p>",emailMessage.BodyText);
		}

		[TestMethod]
		public void GetAutographHtml_PlainTextIsEncodedWithLineBreaks() {
			Assert.AreEqual("A &lt;b&gt;<br>\r\nC",BillingEmailHtml.GetAutographHtml("A <b>\nC",isAutographHtml:false));
			Assert.AreEqual("",BillingEmailHtml.GetAutographHtml("",isAutographHtml:false));
		}

		[TestMethod]
		public void InsertBeforeClosingBody() {
			Assert.AreEqual("<body>x<i>y</i>\r\n</body>",BillingEmailHtml.InsertBeforeClosingBody("<body>x</body>","<i>y</i>"));
			Assert.AreEqual("x<i>y</i>",BillingEmailHtml.InsertBeforeClosingBody("x","<i>y</i>"));
			Assert.AreEqual("<i>y</i>",BillingEmailHtml.InsertBeforeClosingBody(null,"<i>y</i>"));
		}
		#endregion Autographs

		#region GetBodyError
		[TestMethod]
		public void GetBodyError_OnlyHtmlBodiesAreChecked() {
			Assert.AreEqual("",BillingEmailHtml.GetBodyError("<b>Hello [nameF]</b>\r\nBalance [StatementBalance]",EmailType.Html));
			Assert.AreNotEqual("",BillingEmailHtml.GetBodyError("<b>Hello [nameF]",EmailType.Html));
			Assert.AreNotEqual("",BillingEmailHtml.GetBodyError("Balance < 5",EmailType.Html));
			Assert.AreEqual("",BillingEmailHtml.GetBodyError("Balance < 5",EmailType.Regular));
			Assert.AreEqual("",BillingEmailHtml.GetBodyError("<html><body>unclosed",EmailType.RawHtml));
			Assert.AreEqual("",BillingEmailHtml.GetBodyError("",EmailType.Html));
		}
		#endregion GetBodyError
	}
}
