// MIME with MimeKit 4.18 (MimeKit 2.4.1 before): the messages the Gmail and Microsoft 365 sends build (SendEmail.CreateMIMEMsg) and read back
// field for field, attachments byte for byte, and the program's received-mail parser (EmailMessages.RawEmailToMimeMessage, MimeKit since package
// S0-3 of the .NET 10 plan replaced the Direct Project parser) reads them too; downloaded messages turn into the same text as MimeKit 2.4.1 made
// of them (MimeMessage.ToString, checked against 2.4.1 when the update was made); and the POP3 helpers that replace OpenPop's header fields.
// Synthetic addresses and files only. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MimeKit;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.Email;

namespace LibraryUpdateTests {
	[TestClass]
	public class MimeTests {
		private static string _folder;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_folder=Path.Combine(Path.GetTempPath(),"libraryupdatetests_mime_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_folder);
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			try {
				Directory.Delete(_folder,true);
			}
			catch(Exception) {
			}
		}

		///<summary>Every byte value, and the line ends that quoted-printable would break (a lone LF, a lone CR, CRLF).</summary>
		private static byte[] GetBinaryContent() {
			List<byte> listBytes=new List<byte>(Encoding.ASCII.GetBytes("%PDF-1.4\n%synthetic\r\nline\rend\n"));
			for(int i=0;i<256;i++) {
				listBytes.Add((byte)i);
			}
			return listBytes.ToArray();
		}

		private static string WriteFile(string name,byte[] arrayBytes) {
			string path=Path.Combine(_folder,name);
			File.WriteAllBytes(path,arrayBytes);
			return path;
		}

		private static MimeMessage Reparse(MimeMessage mimeMessage,out byte[] arrayBytes) {
			using(MemoryStream memoryStream=new MemoryStream()) {
				mimeMessage.WriteTo(memoryStream);
				arrayBytes=memoryStream.ToArray();
			}
			using(MemoryStream memoryStream=new MemoryStream(arrayBytes)) {
				return MimeMessage.Load(memoryStream);
			}
		}

		[TestMethod]
		public void OutgoingMessage_BuildsAndReadsBack() {
			byte[] arrayPdf=GetBinaryContent();
			string pathPdf=WriteFile("1_20261102_abc.pdf",arrayPdf);
			byte[] arrayPng=new byte[] { 0x89,(byte)'P',(byte)'N',(byte)'G',0x0D,0x0A,0x1A,0x0A,0,0,0,0 };
			string pathPng=WriteFile("logo image.png",arrayPng);
			BasicEmailAddress basicEmailAddress=new BasicEmailAddress() { EmailUsername="front@example.test",AuthenticationType=BasicOAuthType.Google };
			BasicEmailMessage basicEmailMessage=new BasicEmailMessage() {
				FromAddress="Front Desk <front@example.test>",
				ToAddress="\"Smith, Pat\" <pat@example.test>, lee@example.test",
				CcAddress="Billing <billing@example.test>",
				BccAddress="audit@example.test",
				Subject="Café appointment ✓ reminder",
				IsHtml=true,
				HtmlBody="<html><body><p>Ihr Termin: Montag</p><img src=\"cid:logo+image.png\"/></body></html>",
				ListHtmlImages=new List<string> { pathPng },
				ListAttachments=new List<BasicEmailAttachment> { new BasicEmailAttachment(pathPdf,"Résumé 2026.pdf") },
			};
			MimeMessage mimeMessage=SendEmail.CreateMIMEMsg(basicEmailAddress,basicEmailMessage);
			MimeMessage mimeMessageRead=Reparse(mimeMessage,out byte[] arrayBytes);
			Assert.IsTrue(arrayBytes.All(x => x<128),"The message is 7-bit: Gmail and Graph get it as base64url/base64 of these bytes.");
			Assert.AreEqual("Café appointment ✓ reminder",mimeMessageRead.Subject);
			MailboxAddress mailboxAddressFrom=mimeMessageRead.From.Mailboxes.Single();
			Assert.AreEqual("Front Desk",mailboxAddressFrom.Name);
			Assert.AreEqual("front@example.test",mailboxAddressFrom.Address);
			CollectionAssert.AreEqual(new[] { "pat@example.test","lee@example.test" },mimeMessageRead.To.Mailboxes.Select(x => x.Address).ToArray());
			Assert.AreEqual("Smith, Pat",mimeMessageRead.To.Mailboxes.First().Name);
			Assert.AreEqual("billing@example.test",mimeMessageRead.Cc.Mailboxes.Single().Address);
			Assert.AreEqual("audit@example.test",mimeMessageRead.Bcc.Mailboxes.Single().Address,"Gmail takes the Bcc from the raw message.");
			Assert.AreEqual(basicEmailMessage.HtmlBody,mimeMessageRead.HtmlBody.TrimEnd('\r','\n'));
			MimePart mimePartAttachment=mimeMessageRead.Attachments.OfType<MimePart>().Single();
			Assert.AreEqual("Résumé 2026.pdf",mimePartAttachment.FileName,"The displayed file name, not the A to Z file name.");
			Assert.AreEqual(ContentEncoding.Base64,mimePartAttachment.ContentTransferEncoding,"Never quoted-printable, which breaks PDFs.");
			using(MemoryStream memoryStream=new MemoryStream()) {
				mimePartAttachment.Content.DecodeTo(memoryStream);
				CollectionAssert.AreEqual(arrayPdf,memoryStream.ToArray());
			}
			MimePart mimePartImage=mimeMessageRead.BodyParts.OfType<MimePart>().Single(x => x.ContentId=="logo+image.png");
			Assert.IsTrue(mimePartImage.ContentDisposition.Disposition.Equals(ContentDisposition.Inline,StringComparison.OrdinalIgnoreCase));
			using(MemoryStream memoryStream=new MemoryStream()) {
				mimePartImage.Content.DecodeTo(memoryStream);
				CollectionAssert.AreEqual(arrayPng,memoryStream.ToArray());
			}
			//The program's own received-mail parser reads what MimeKit wrote, as it reads every downloaded email.
			MimeMessage mimeMessageReceived=EmailMessages.RawEmailToMimeMessage(Encoding.ASCII.GetString(arrayBytes));
			Assert.AreEqual("Café appointment ✓ reminder",EmailMessages.ProcessInlineEncodedText(mimeMessageReceived.Headers[HeaderId.Subject]));
			StringAssert.Contains(mimeMessageReceived.Headers[HeaderId.From],"front@example.test");
			StringAssert.Contains(mimeMessageReceived.Headers[HeaderId.To],"lee@example.test");
		}

		[TestMethod]
		public void OutgoingTextMessage_KeepsItsLines() {
			BasicEmailAddress basicEmailAddress=new BasicEmailAddress() { EmailUsername="front@example.test",AuthenticationType=BasicOAuthType.Microsoft };
			BasicEmailMessage basicEmailMessage=new BasicEmailMessage() {
				FromAddress="front@example.test",
				ToAddress="pat@example.test",
				Subject="Statement",
				IsHtml=false,
				BodyText="Line one\r\n.Line starting with a dot\r\nFrom the office\r\nÄrger über Größe",
			};
			MimeMessage mimeMessageRead=Reparse(SendEmail.CreateMIMEMsg(basicEmailAddress,basicEmailMessage),out byte[] arrayBytes);
			Assert.AreEqual(basicEmailMessage.BodyText,mimeMessageRead.TextBody.TrimEnd('\r','\n'));
			Assert.AreEqual(0,mimeMessageRead.Attachments.Count());
			Assert.AreEqual(0,mimeMessageRead.Cc.Count);
		}

		///<summary>Downloaded messages (Gmail raw, Graph MIME) as MimeKit 2.4.1 and 4.18 both read them. Every value is what 2.4.1 returned.</summary>
		private static readonly Dictionary<string,string> _dictDownloaded=new Dictionary<string,string> {
			{ "plain","From: Front Desk <front@example.test>\r\nTo: patient@example.test\r\nSubject: Appointment\r\nDate: Mon, 02 Nov 2026 08:30:00 -0800\r\n"
				+"Message-ID: <abc123@example.test>\r\nMIME-Version: 1.0\r\nContent-Type: text/plain; charset=us-ascii\r\n\r\nSee you at 9.\r\n.leading dot\r\n" },
			{ "utf8 body","From: a@example.test\r\nTo: b@example.test\r\nSubject: =?utf-8?B?Q2Fmw6k=?=\r\nMIME-Version: 1.0\r\n"
				+"Content-Type: text/plain; charset=utf-8\r\nContent-Transfer-Encoding: 8bit\r\n\r\nCafé €\r\n" },
			{ "raw 8-bit header","From: a@example.test\r\nTo: b@example.test\r\nSubject: Café raw\r\nContent-Type: text/plain\r\n\r\nbody\r\n" },
			{ "multipart","From: a@example.test\r\nTo: b@example.test, c@example.test\r\nCc: d@example.test\r\nSubject: folded\r\n subject line\r\n"
				+"MIME-Version: 1.0\r\nContent-Type: multipart/mixed; boundary=\"XYZ\"\r\n\r\npreamble\r\n--XYZ\r\n"
				+"Content-Type: text/html; charset=iso-8859-1\r\nContent-Transfer-Encoding: quoted-printable\r\n\r\n<p>Caf=E9</p>\r\n--XYZ\r\n"
				+"Content-Type: application/pdf; name=\"x.pdf\"\r\nContent-Disposition: attachment; filename=\"x.pdf\"\r\nContent-Transfer-Encoding: base64\r\n"
				+"\r\nJVBERi0xLjQKJcOkw7zDtsOfCg==\r\n--XYZ--\r\nepilogue\r\n" },
			{ "no content type","From: a@example.test\r\nTo: b@example.test\r\nSubject: none\r\n\r\njust text\r\n" },
			{ "bcc only","From: a@example.test\r\nTo: undisclosed-recipients:;\r\nBcc: b@example.test\r\nSubject: bcc only\r\n\r\nx\r\n" },
		};

		[TestMethod]
		public void DownloadedMessage_TurnsIntoTheSameTextAsBefore() {
			Encoding latin1=Encoding.GetEncoding(28591);
			Dictionary<string,string> dictSubjects=new Dictionary<string,string> { { "plain","Appointment" },{ "utf8 body","Café" },
				{ "raw 8-bit header","Café raw" },{ "multipart","folded subject line" },{ "no content type","none" },{ "bcc only","bcc only" } };
			foreach(KeyValuePair<string,string> pair in _dictDownloaded) {
				foreach(Encoding encoding in new[] { latin1,new UTF8Encoding(false) }) {
					byte[] arrayBytes=encoding.GetBytes(pair.Value);
					MimeMessage mimeMessage;
					using(MemoryStream memoryStream=new MemoryStream(arrayBytes)) {
						mimeMessage=MimeMessage.Load(memoryStream);
					}
					//What ImportGmailMessage and RetrieveFromMicrosoftInbox hand ProcessRawEmailMessageIn: each byte as one character, unchanged.
					Assert.AreEqual(latin1.GetString(arrayBytes),mimeMessage.ToString(),pair.Key+" "+encoding.WebName);
					Assert.AreEqual(dictSubjects[pair.Key],mimeMessage.Subject,pair.Key+" "+encoding.WebName);
				}
			}
			//A message with bare LF line ends comes out with CRLF, as with 2.4.1.
			byte[] arrayLf=Encoding.ASCII.GetBytes("From: a@example.test\nTo: b@example.test\nSubject: lf\n\nline1\nline2\n");
			using(MemoryStream memoryStream=new MemoryStream(arrayLf)) {
				Assert.AreEqual("From: a@example.test\r\nTo: b@example.test\r\nSubject: lf\r\n\r\nline1\r\nline2\r\n",MimeMessage.Load(memoryStream).ToString());
			}
		}

		[TestMethod]
		public void DownloadedMultipart_GivesItsAttachmentToGraphAndTheInbox() {
			byte[] arrayBytes=Encoding.ASCII.GetBytes(_dictDownloaded["multipart"]);
			MimeMessage mimeMessage;
			using(MemoryStream memoryStream=new MemoryStream(arrayBytes)) {
				mimeMessage=MimeMessage.Load(memoryStream);
			}
			List<MimePart> listParts=OpenPmsDentalBusiness.Microsoft365.Microsoft365GraphClient.GetAttachmentParts(mimeMessage);
			Assert.AreEqual(1,listParts.Count);
			Assert.AreEqual("x.pdf",listParts[0].FileName);
			Assert.AreEqual("<p>Café</p>",mimeMessage.HtmlBody.TrimEnd('\r','\n'));
			CollectionAssert.AreEqual(new[] { "b@example.test","c@example.test" },mimeMessage.To.Mailboxes.Select(x => x.Address).ToArray());
		}

		private static HeaderList ParseHeaders(string headers) {
			using(MemoryStream memoryStream=new MemoryStream(Encoding.ASCII.GetBytes(headers+"\r\n"))) {
				return HeaderList.Load(memoryStream);
			}
		}

		[TestMethod]
		public void Pop3HeaderId_IsBuiltAsOpenPopBuiltIt() {
			//Date sent in UTC, the inbox, the sender's address and the decoded subject.
			HeaderList headerList=ParseHeaders("Date: Tue, 03 Nov 2026 16:05:09 -0800\r\nFrom: \"Lab, Results\" <results@lab.example.test>\r\n"
				+"Subject: =?utf-8?Q?R=C3=A9sultats?= ready\r\nMessage-ID: <x7Pumwpcn9NjhAJn5A@mail.example.test>\r\n");
			Assert.AreEqual("20261104000509front@example.testresults@lab.example.testRésultats ready",
				EmailMessages.GetPop3HeaderMsgId(headerList,"front@example.test"));
			Assert.AreEqual("x7Pumwpcn9NjhAJn5A@mail.example.test",EmailMessages.GetPop3MessageId(headerList),"Without the angle brackets.");
			//Without a date or sender, DateTime.MinValue and nothing.
			HeaderList headerListBare=ParseHeaders("Subject: hello\r\n");
			Assert.AreEqual("00010101000000front@example.testhello",EmailMessages.GetPop3HeaderMsgId(headerListBare,"front@example.test"));
			Assert.AreEqual("",EmailMessages.GetPop3MessageId(headerListBare));
			Assert.AreEqual("plain-id@example.test",EmailMessages.GetPop3MessageId(ParseHeaders("Message-ID:  plain-id@example.test \r\n")));
		}

		[TestMethod]
		public void Pop3RawText_IsReadWithTheMessagesCharset() {
			Assert.AreEqual(Encoding.ASCII,EmailMessages.GetPop3BodyEncoding(Load("Subject: x\r\n\r\nbody\r\n")),"No Content-Type: US-ASCII.");
			Assert.AreEqual(Encoding.ASCII,EmailMessages.GetPop3BodyEncoding(Load("Content-Type: text/plain\r\n\r\nbody\r\n")),"No charset: US-ASCII.");
			Assert.AreEqual(65001,EmailMessages.GetPop3BodyEncoding(Load("Content-Type: text/plain; charset=UTF-8\r\n\r\nbody\r\n")).CodePage);
			Assert.AreEqual(1252,EmailMessages.GetPop3BodyEncoding(Load("Content-Type: text/plain; charset=windows-1252\r\n\r\nbody\r\n")).CodePage);
			Assert.AreEqual(28591,EmailMessages.GetPop3BodyEncoding(Load("Content-Type: text/plain; charset=\"iso-8859-1\"\r\n\r\nbody\r\n")).CodePage);
			//A multipart message: the top-level Content-Type has no charset.
			Assert.AreEqual(Encoding.ASCII,EmailMessages.GetPop3BodyEncoding(Load("Content-Type: multipart/mixed; boundary=b\r\n\r\n--b\r\n"
				+"Content-Type: text/plain; charset=utf-8\r\n\r\nx\r\n--b--\r\n")));
			//OpenPop could not read a charset .NET does not know and skipped the message for good; now it is read as US-ASCII.
			Assert.AreEqual(Encoding.ASCII,EmailMessages.GetPop3BodyEncoding(Load("Content-Type: text/plain; charset=x-no-such-charset\r\n\r\nbody\r\n")));
		}

		private static MimeMessage Load(string text) {
			using(MemoryStream memoryStream=new MemoryStream(Encoding.ASCII.GetBytes(text))) {
				return MimeMessage.Load(memoryStream);
			}
		}
	}
}
