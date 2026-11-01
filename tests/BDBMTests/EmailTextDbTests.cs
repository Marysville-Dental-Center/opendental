// EmailmessageInvalidCharactersInBodyText (Open Dental 25.1) and the Unicode white space handling of the Raw Emails tool (24.4), on the scratch
// database. Synthetic data only. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using UnitTestsCore;

namespace BDBMTests {
	[TestClass]
	public class EmailTextDbTests {
		[TestInitialize]
		public void Init() {
			BDbmDb.Begin();
		}

		private static EmailMessage CreateEmail(Patient patient,string bodyText) {
			EmailMessage emailMessage=EmailMessageT.CreateEmailMessage(patient.PatNum,fromAddress:"office@example.test",toAddress:"patient@example.test",
				subject:"Synthetic",bodyText:"placeholder",sentOrReceived:EmailSentOrReceived.Sent,msgDateTime:new DateTime(2025,1,2,10,0,0));
			BDbmDb.SetText("emailmessage","BodyText","EmailMessageNum",emailMessage.EmailMessageNum,bodyText);
			return emailMessage;
		}

		private static string Body(EmailMessage emailMessage) {
			return BDbmDb.GetText("emailmessage","BodyText","EmailMessageNum",emailMessage.EmailMessageNum);
		}

		[TestMethod]
		public void BodyText_CheckCountsFixRemovesOnlyTheBadCharacters() {
			DatabaseMaintenances.EmailmessageInvalidCharactersInBodyText(false,DbmMode.Fix);//Start clean: earlier tests may have left rows.
			Patient patient=BDbmDb.CreatePatient("EmailBody");
			string bodyBad="Dear patient,\r\n\u0001Your\u0000 visit\u000B is\ttomorrow.\u001F";
			EmailMessage emailBad1=CreateEmail(patient,bodyBad);
			EmailMessage emailBad2=CreateEmail(patient,"Second\u0008 message \uFFFE stays");
			string bodyGood="Clean body\r\nwith tabs\tand accents \u00E9 and U+FFFD \uFFFD.";
			EmailMessage emailGood=CreateEmail(patient,bodyGood);
			string logCheck=DatabaseMaintenances.EmailmessageInvalidCharactersInBodyText(false,DbmMode.Check);
			StringAssert.Contains(logCheck,"Email messages with invalid characters in the body text: 2");
			string logBreakdown=DatabaseMaintenances.EmailmessageInvalidCharactersInBodyText(false,DbmMode.Breakdown);
			StringAssert.Contains(logBreakdown,"EmailMessageNum "+emailBad1.EmailMessageNum+", PatNum "+patient.PatNum);
			StringAssert.Contains(logBreakdown,"EmailMessageNum "+emailBad2.EmailMessageNum+", PatNum "+patient.PatNum);
			Assert.IsFalse(logBreakdown.Contains("EmailMessageNum "+emailGood.EmailMessageNum+","));
			Assert.AreEqual(bodyBad,Body(emailBad1),"Check and break down change nothing.");
			string logFix=DatabaseMaintenances.EmailmessageInvalidCharactersInBodyText(false,DbmMode.Fix);
			StringAssert.Contains(logFix,"Email messages with invalid characters removed from the body text: 2");
			Assert.AreEqual("Dear patient,\r\nYour visit is\ttomorrow.",Body(emailBad1));
			Assert.AreEqual("Second message \uFFFE stays",Body(emailBad2),"The 25.1 rule leaves U+FFFE (the GDI+ tool's job).");
			Assert.AreEqual(bodyGood,Body(emailGood));
			Assert.AreEqual("Synthetic",BDbmDb.GetText("emailmessage","Subject","EmailMessageNum",emailBad1.EmailMessageNum),"Only BodyText is changed.");
			Assert.AreEqual("",DatabaseMaintenances.EmailmessageInvalidCharactersInBodyText(false,DbmMode.Check),"Nothing left.");
			StringAssert.Contains(DatabaseMaintenances.EmailmessageInvalidCharactersInBodyText(true,DbmMode.Check),": 0");
		}

		[TestMethod]
		public void BodyText_FixWorksInBatches() {
			DatabaseMaintenances.EmailmessageInvalidCharactersInBodyText(false,DbmMode.Fix);
			Patient patient=BDbmDb.CreatePatient("EmailBatch");
			List<EmailMessage> listEmails=new List<EmailMessage>();
			for(int i=0;i<230;i++) {//More than two batches of 100.
				listEmails.Add(CreateEmail(patient,"Row "+i+"\u0002"));
			}
			string logFix=DatabaseMaintenances.EmailmessageInvalidCharactersInBodyText(false,DbmMode.Fix);
			StringAssert.Contains(logFix,": 230");
			Assert.AreEqual("Row 0",Body(listEmails[0]));
			Assert.AreEqual("Row 229",Body(listEmails[229]));
		}

		[TestMethod]
		public void RawEmails_NormalizeMapsSpacesInBothCopies() {
			EmailMessage emailNew=new EmailMessage { Subject="Hello\u00A0there\u200B",BodyText="Body\u2003text\r\nline two\uFEFF" };
			EmailMessage emailOld=new EmailMessage { Subject="Hello there",BodyText="Body\u3000text\r\nline two" };
			DatabaseMaintenances.NormalizeRawEmailWhiteSpace(emailNew,emailOld);
			Assert.AreEqual("Hello there",emailNew.Subject);
			Assert.AreEqual("Body text\r\nline two",emailNew.BodyText);
			Assert.AreEqual(emailOld.Subject,emailNew.Subject,"A difference only in white space is no longer a change.");
			Assert.AreEqual(emailOld.BodyText,emailNew.BodyText);
		}

		[TestMethod]
		public void RawEmails_RecipientWithUnicodeSpaceStillFindsTheInbox() {
			List<EmailAddress> listEmailAddresses=new List<EmailAddress> {
				new EmailAddress { EmailUsername="other@example.test" },
				new EmailAddress { EmailUsername="Inbox@Example.test" },
			};
			Assert.AreSame(listEmailAddresses[1],DatabaseMaintenances.FindEmailAddressForRawEmail(listEmailAddresses,"inbox@example.test"));
			Assert.AreSame(listEmailAddresses[1],DatabaseMaintenances.FindEmailAddressForRawEmail(listEmailAddresses,"inbox@example.test\u00A0"));
			Assert.AreSame(listEmailAddresses[1],DatabaseMaintenances.FindEmailAddressForRawEmail(listEmailAddresses,"\uFEFFinbox@example.test"));
			Assert.IsNull(DatabaseMaintenances.FindEmailAddressForRawEmail(listEmailAddresses,"nobody@example.test"));
		}

		[TestMethod]
		public void RawEmails_ToolProcessesAMessageWithUnicodeWhiteSpace() {
			string inbox="bdbm.inbox."+Guid.NewGuid().ToString("N").Substring(0,8)+"@example.test";
			EmailAddressT.CreateEmailAddress(senderAddress:inbox,emailUserName:inbox);
			Patient patient=BDbmDb.CreatePatient("RawEmail");
			string raw="From: sender@example.test\r\n"
				+"To: "+inbox+"\r\n"
				+"Subject: Hello\u00A0there\r\n"
				+"Date: Mon, 6 Jan 2025 10:00:00 -0700\r\n"
				+"MIME-Version: 1.0\r\n"
				+"Content-Type: text/plain; charset=utf-8\r\n"
				+"\r\n"
				+"Body\u00A0text\u200B with unusual white space.\r\n";
			EmailMessage emailMessage=EmailMessageT.CreateEmailMessage(patient.PatNum,fromAddress:"sender@example.test",toAddress:inbox,
				recipientAddress:inbox+"\u00A0",subject:"Hello there",bodyText:"Body text with unusual white space.",
				sentOrReceived:EmailSentOrReceived.Received,msgDateTime:new DateTime(2025,1,6,10,0,0));
			BDbmDb.SetText("emailmessage","RawEmailIn","EmailMessageNum",emailMessage.EmailMessageNum,raw);
			BDbmDb.SetText("emailmessage","RecipientAddress","EmailMessageNum",emailMessage.EmailMessageNum,inbox+"\u00A0");
			string result=DatabaseMaintenances.CleanUpRawEmails();
			Match match=Regex.Match(result,@"failed to be cleaned up: (\d+)");
			if(match.Success) {
				Assert.AreEqual("0",match.Groups[1].Value,result);
			}
			else {
				StringAssert.Contains(result,"No clean up required",result);
			}
		}
	}
}
