// SMTP send with a password (SendEmail.WireEmailUnsecure, System.Net.Mail on ports other than 465), against the fake SMTP server on 127.0.0.1,
// plain and with STARTTLS: the sign-in, the envelope (Bcc only there), and the message MimeKit 4.18 reads back field for field and attachment byte
// for byte. The library updates do not change this path; these tests pin it next to the MIME and POP3 tests. Port 465 (implicit TLS) goes through
// Windows' CDO, which only a real server can test. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository
// root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MimeKit;
using OpenPmsDentalBusiness.Email;

namespace LibraryUpdateTests {
	[TestClass]
	public class SmtpTests {
		private const string USER="front@example.test";
		private const string PASSWORD="synthetic-smtp-password";
		private static string _folder;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_folder=Path.Combine(Path.GetTempPath(),"libraryupdatetests_smtp_"+Guid.NewGuid().ToString("N"));
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

		private static BasicEmailAddress MakeAddress(int port,bool useSsl) {
			return new BasicEmailAddress() { SMTPserver="127.0.0.1",ServerPort=port,UseSSL=useSsl,EmailUsername=" "+USER+" ",EmailPassword=PASSWORD,
				AuthenticationType=BasicOAuthType.None };
		}

		private static BasicEmailMessage MakeMessage(string pathAttachment) {
			return new BasicEmailMessage() {
				FromAddress=USER,
				ToAddress="Pat Smith <pat@example.test>",
				CcAddress="billing@example.test",
				BccAddress="audit@example.test",
				Subject="Café statement",
				IsHtml=false,
				BodyText="Your statement is attached.\r\n.A line with a leading dot\r\nThank you",
				ListAttachments=new List<BasicEmailAttachment> { new BasicEmailAttachment(pathAttachment,"Statement 2026.pdf") },
			};
		}

		private static byte[] GetAttachmentBytes() {
			List<byte> listBytes=new List<byte>(Encoding.ASCII.GetBytes("%PDF-1.4\r\n"));
			for(int i=0;i<256;i++) {
				listBytes.Add((byte)i);
			}
			return listBytes.ToArray();
		}

		private static void AssertReceived(FakeSmtpServer fakeSmtpServer,byte[] arrayAttachment,bool isTls) {
			Assert.IsNull(fakeSmtpServer.ServerError,fakeSmtpServer.ServerError?.ToString());
			FakeSmtpServer.Received received=fakeSmtpServer.ListReceived.Single();
			Assert.AreEqual(isTls,received.WasTls);
			StringAssert.Contains(received.MailFrom,USER);
			CollectionAssert.AreEquivalent(new[] { "<pat@example.test>","<billing@example.test>","<audit@example.test>" },
				received.ListRcptTo.Select(x => x.Split(' ')[0]).ToArray());
			MimeMessage mimeMessage;
			using(MemoryStream memoryStream=new MemoryStream(received.Data)) {
				mimeMessage=MimeMessage.Load(memoryStream);
			}
			Assert.AreEqual("Café statement",mimeMessage.Subject);
			Assert.AreEqual("pat@example.test",mimeMessage.To.Mailboxes.Single().Address);
			Assert.AreEqual("Pat Smith",mimeMessage.To.Mailboxes.Single().Name);
			Assert.AreEqual("billing@example.test",mimeMessage.Cc.Mailboxes.Single().Address);
			Assert.AreEqual(0,mimeMessage.Bcc.Count,"Bcc is only in the envelope.");
			Assert.AreEqual("Your statement is attached.\r\n.A line with a leading dot\r\nThank you",mimeMessage.TextBody.TrimEnd('\r','\n'));
			MimePart mimePart=mimeMessage.Attachments.OfType<MimePart>().Single();
			Assert.AreEqual("Statement 2026.pdf",mimePart.ContentType.Name,"The displayed name (Attachment.Name), not the A to Z file name.");
			using(MemoryStream memoryStream=new MemoryStream()) {
				mimePart.Content.DecodeTo(memoryStream);
				CollectionAssert.AreEqual(arrayAttachment,memoryStream.ToArray());
			}
			Assert.IsTrue(fakeSmtpServer.GetCommands().Contains("(AUTH LOGIN "+USER+")") || fakeSmtpServer.GetCommands().Any(x => x.StartsWith("AUTH PLAIN")),
				"Signed in with the trimmed user name: "+string.Join(" | ",fakeSmtpServer.GetCommands()));
		}

		[TestMethod]
		public void PlainSmtp_SendsTheMessage() {
			byte[] arrayAttachment=GetAttachmentBytes();
			string path=Path.Combine(_folder,"1_plain.pdf");
			File.WriteAllBytes(path,arrayAttachment);
			using(FakeSmtpServer fakeSmtpServer=new FakeSmtpServer(USER,PASSWORD,isStartTlsOffered:false)) {
				SendEmail.WireEmailUnsecure(MakeAddress(fakeSmtpServer.Port,false),MakeMessage(path),null);
				AssertReceived(fakeSmtpServer,arrayAttachment,false);
			}
		}

		[TestMethod]
		public void StartTls_SendsTheMessageOverTls() {
			byte[] arrayAttachment=GetAttachmentBytes();
			string path=Path.Combine(_folder,"2_tls.pdf");
			File.WriteAllBytes(path,arrayAttachment);
			RemoteCertificateValidationCallback callbackOld=ServicePointManager.ServerCertificateValidationCallback;
			//System.Net.Mail checks the certificate through ServicePointManager; the test trusts its own certificate there for this test only.
			ServicePointManager.ServerCertificateValidationCallback=TestCertificates.IsTestCertificate;
			try {
				using(FakeSmtpServer fakeSmtpServer=new FakeSmtpServer(USER,PASSWORD,isStartTlsOffered:true)) {
					SendEmail.WireEmailUnsecure(MakeAddress(fakeSmtpServer.Port,true),MakeMessage(path),null);
					AssertReceived(fakeSmtpServer,arrayAttachment,true);
					List<string> listCommands=fakeSmtpServer.GetCommands();
					Assert.IsTrue(listCommands.IndexOf("STARTTLS")>=0 && listCommands.IndexOf("STARTTLS")<listCommands.FindIndex(x => x.StartsWith("(AUTH")),
						"The password goes only over TLS: "+string.Join(" | ",listCommands));
				}
			}
			finally {
				ServicePointManager.ServerCertificateValidationCallback=callbackOld;
			}
		}

		[TestMethod]
		public void WrongPassword_IsAnError() {
			string path=Path.Combine(_folder,"3_wrong.pdf");
			File.WriteAllBytes(path,GetAttachmentBytes());
			using(FakeSmtpServer fakeSmtpServer=new FakeSmtpServer(USER,"another-password",isStartTlsOffered:false)) {
				try {
					SendEmail.WireEmailUnsecure(MakeAddress(fakeSmtpServer.Port,false),MakeMessage(path),null);
					Assert.Fail("The send should fail.");
				}
				catch(CodeBase.ODException ex) {
					Assert.IsNotNull(ex.InnerException);
				}
				Assert.AreEqual(0,fakeSmtpServer.ListReceived.Count);
			}
		}
	}
}
