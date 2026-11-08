// SMTP send with a password (SendEmail.WireEmailUnsecure), against the fake SMTP server on 127.0.0.1: System.Net.Mail on ports other than 465,
// plain and with STARTTLS; MailKit with TLS from the first byte on port 465 (implicit SSL, which Windows' CDO sent before package S0-3 of the
// .NET 10 plan). The sign-in, the envelope (Bcc only there), and the message MimeKit 4.18 reads back field for field and attachment byte for
// byte; on port 465 also what CDO set: the inline image's Content-ID, each attachment's Content-ID and its Content-Disposition with the displayed
// file name, for an attached saved e-mail (.eml, a MimeKit MessagePart) as for a file. Licensed under the GNU General Public License, version 2 or
// later. See LICENSE in the repository root.
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
				SendEmail.WireEmailUnsecure(MakeAddress(fakeSmtpServer.Port,false),MakeMessage(path));
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
					SendEmail.WireEmailUnsecure(MakeAddress(fakeSmtpServer.Port,true),MakeMessage(path));
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

		///<summary>An html message with one inline image and one attachment, as ODEmailMessageToBasic hands them over.</summary>
		private static BasicEmailMessage MakeHtmlMessage(string pathImage,string pathAttachment) {
			return new BasicEmailMessage() {
				FromAddress=USER,
				ToAddress="Pat Smith <pat@example.test>",
				CcAddress="",
				BccAddress="audit@example.test",
				Subject="Your x-ray",
				IsHtml=true,
				HtmlBody="<html><body><p>See below.</p><img src=\"cid:"+UrlEncodeName(Path.GetFileName(pathImage))+"\"/></body></html>",
				ListHtmlImages=new List<string> { pathImage },
				ListAttachments=new List<BasicEmailAttachment> { new BasicEmailAttachment(pathAttachment,"Statement 2026.pdf") },
			};
		}

		///<summary>HttpUtility.UrlEncode for the file names of these tests (letters, digits, '.', '-' and spaces): a space becomes '+'.</summary>
		private static string UrlEncodeName(string fileName) {
			return fileName.Replace(" ","+");
		}

		private static readonly byte[] _arrayPng=new byte[] { 0x89,(byte)'P',(byte)'N',(byte)'G',0x0D,0x0A,0x1A,0x0A,0,0,0,0 };

		///<summary>What CDO set on port 465, now on the MailKit message: the inline image's Content-ID is the url-encoded file name the html refers
		///to, and each attachment has the A to Z file name as Content-ID and "Content-Disposition: attachment" with the displayed file name.</summary>
		private static void AssertLikeCdo(MimeMessage mimeMessage,string pathImage,string pathAttachment,byte[] arrayAttachment) {
			string cidImage=UrlEncodeName(Path.GetFileName(pathImage));
			MimePart mimePartImage=mimeMessage.BodyParts.OfType<MimePart>().Single(x => x.ContentType.MimeType=="image/png");
			Assert.AreEqual(cidImage,mimePartImage.ContentId);
			Assert.IsTrue(string.Equals(ContentDisposition.Inline,mimePartImage.ContentDisposition.Disposition,StringComparison.OrdinalIgnoreCase));
			StringAssert.Contains(mimeMessage.HtmlBody,"cid:"+cidImage);
			using(MemoryStream memoryStream=new MemoryStream()) {
				mimePartImage.Content.DecodeTo(memoryStream);
				CollectionAssert.AreEqual(_arrayPng,memoryStream.ToArray());
			}
			MimePart mimePartAttachment=mimeMessage.Attachments.OfType<MimePart>().Single();
			Assert.AreEqual(Path.GetFileName(pathAttachment),mimePartAttachment.ContentId,"The A to Z file name, as CDO set it.");
			Assert.IsTrue(string.Equals(ContentDisposition.Attachment,mimePartAttachment.ContentDisposition.Disposition,StringComparison.OrdinalIgnoreCase));
			Assert.AreEqual("Statement 2026.pdf",mimePartAttachment.ContentDisposition.FileName,"The displayed file name.");
			using(MemoryStream memoryStream=new MemoryStream()) {
				mimePartAttachment.Content.DecodeTo(memoryStream);
				CollectionAssert.AreEqual(arrayAttachment,memoryStream.ToArray());
			}
			Assert.AreEqual("Your x-ray",mimeMessage.Subject);
			Assert.AreEqual("pat@example.test",mimeMessage.To.Mailboxes.Single().Address);
		}

		[TestMethod]
		public void ImplicitTls_MessageCarriesWhatCdoSet() {
			byte[] arrayAttachment=GetAttachmentBytes();
			string pathAttachment=Path.Combine(_folder,"4_cdo.pdf");
			File.WriteAllBytes(pathAttachment,arrayAttachment);
			string pathImage=Path.Combine(_folder,"x-ray image.png");
			File.WriteAllBytes(pathImage,_arrayPng);
			MimeMessage mimeMessage=SendEmail.CreateMimeMessageForSmtp(MakeAddress(465,true),MakeHtmlMessage(pathImage,pathAttachment));
			AssertLikeCdo(mimeMessage,pathImage,pathAttachment,arrayAttachment);
			Assert.IsTrue(SendEmail.IsImplicitSsl(MakeAddress(465,true)));
			Assert.IsFalse(SendEmail.IsImplicitSsl(MakeAddress(587,true)));
		}

		[TestMethod]
		public void ImplicitTls_SendsWithMailKitOverTls() {
			byte[] arrayAttachment=GetAttachmentBytes();
			string pathAttachment=Path.Combine(_folder,"5_implicit.pdf");
			File.WriteAllBytes(pathAttachment,arrayAttachment);
			string pathImage=Path.Combine(_folder,"logo.png");
			File.WriteAllBytes(pathImage,_arrayPng);
			SendEmail.SmtpCertificateValidationForTests=TestCertificates.IsTestCertificate;
			try {
				using(FakeSmtpServer fakeSmtpServer=new FakeSmtpServer(USER,PASSWORD,isStartTlsOffered:false,isImplicitTls:true)) {
					BasicEmailAddress basicEmailAddress=MakeAddress(fakeSmtpServer.Port,true);
					SendEmail.SendSmtpMailKit(basicEmailAddress,SendEmail.CreateMimeMessageForSmtp(basicEmailAddress,MakeHtmlMessage(pathImage,pathAttachment)),
						MailKit.Security.SecureSocketOptions.SslOnConnect,SendEmail.EMAIL_SEND_TIMEOUT_MS);
					Assert.IsNull(fakeSmtpServer.ServerError,fakeSmtpServer.ServerError?.ToString());
					FakeSmtpServer.Received received=fakeSmtpServer.ListReceived.Single();
					Assert.IsTrue(received.WasTls,"TLS from the first byte.");
					CollectionAssert.AreEquivalent(new[] { "<pat@example.test>","<audit@example.test>" },received.ListRcptTo.Select(x => x.Split(' ')[0]).ToArray());
					MimeMessage mimeMessage;
					using(MemoryStream memoryStream=new MemoryStream(received.Data)) {
						mimeMessage=MimeMessage.Load(memoryStream);
					}
					Assert.AreEqual(0,mimeMessage.Bcc.Count,"Bcc is only in the envelope.");
					AssertLikeCdo(mimeMessage,pathImage,pathAttachment,arrayAttachment);
					Assert.IsTrue(fakeSmtpServer.GetCommands().Contains("(AUTH LOGIN "+USER+")") || fakeSmtpServer.GetCommands().Any(x => x.StartsWith("AUTH PLAIN")),
						"Signed in with the trimmed user name: "+string.Join(" | ",fakeSmtpServer.GetCommands()));
				}
			}
			finally {
				SendEmail.SmtpCertificateValidationForTests=null;
			}
		}

		///<summary>A saved e-mail (message/rfc822), as the inbox stores a forwarded-as-attachment message under its displayed name: MimeKit attaches
		///it as a MessagePart, not a MimePart. Synthetic, CRLF line ends.</summary>
		internal static void WriteForwardedEml(string path) {
			string eml="From: Lab Desk <lab@example.test>\r\n"
				+"To: front@example.test\r\n"
				+"Subject: Lab results\r\n"
				+"Date: Mon, 02 Nov 2026 09:30:00 +0000\r\n"
				+"Message-ID: <lab-results-1@example.test>\r\n"
				+"MIME-Version: 1.0\r\n"
				+"Content-Type: text/plain; charset=us-ascii\r\n"
				+"\r\n"
				+"The crown for Pat Smith is ready.\r\n";
			File.WriteAllText(path,eml,Encoding.ASCII);
		}

		///<summary>The attached e-mail as it arrived: message/rfc822 holding the saved message, the A to Z file name as Content-ID (as CDO set it) and
		///"Content-Disposition: attachment" with the displayed file name, which is also the Content-Type name.</summary>
		internal static void AssertForwardedEml(MimeMessage mimeMessage,string pathEml) {
			MessagePart messagePart=mimeMessage.Attachments.OfType<MessagePart>().Single();
			Assert.AreEqual("message/rfc822",messagePart.ContentType.MimeType);
			Assert.AreEqual(Path.GetFileName(pathEml),messagePart.ContentId,"The A to Z file name, as CDO set it.");
			Assert.IsTrue(string.Equals(ContentDisposition.Attachment,messagePart.ContentDisposition.Disposition,StringComparison.OrdinalIgnoreCase));
			Assert.AreEqual("Lab results.eml",messagePart.ContentDisposition.FileName,"The displayed file name.");
			Assert.AreEqual("Lab results.eml",messagePart.ContentType.Name);
			Assert.AreEqual("Lab results",messagePart.Message.Subject);
			Assert.AreEqual("lab@example.test",messagePart.Message.From.Mailboxes.Single().Address);
			Assert.AreEqual("The crown for Pat Smith is ready.",messagePart.Message.TextBody.TrimEnd('\r','\n'));
		}

		///<summary>Port 465 with an attached .eml (a MessagePart) before a pdf: the message builds (no cast to MimePart) and goes out with TLS from the
		///first byte; the .eml and the pdf each keep their own Content-ID, disposition and displayed file name, and the pdf its bytes.</summary>
		[TestMethod]
		public void ImplicitTls_SendsAnAttachedEmlMessage() {
			byte[] arrayAttachment=GetAttachmentBytes();
			string pathAttachment=Path.Combine(_folder,"8_after_eml.pdf");
			File.WriteAllBytes(pathAttachment,arrayAttachment);
			string pathEml=Path.Combine(_folder,"8_forward.eml");
			WriteForwardedEml(pathEml);
			BasicEmailMessage basicEmailMessage=MakeMessage(pathAttachment);
			basicEmailMessage.ListAttachments.Insert(0,new BasicEmailAttachment(pathEml,"Lab results.eml"));
			Assert.IsTrue(SendEmail.HasAttachedMessage(basicEmailMessage));
			Assert.IsFalse(SendEmail.HasAttachedMessage(MakeMessage(pathAttachment)),"A pdf is a MimePart.");
			SendEmail.SmtpCertificateValidationForTests=TestCertificates.IsTestCertificate;
			try {
				using(FakeSmtpServer fakeSmtpServer=new FakeSmtpServer(USER,PASSWORD,isStartTlsOffered:false,isImplicitTls:true)) {
					BasicEmailAddress basicEmailAddress=MakeAddress(fakeSmtpServer.Port,true);
					SendEmail.SendSmtpMailKit(basicEmailAddress,SendEmail.CreateMimeMessageForSmtp(basicEmailAddress,basicEmailMessage),
						MailKit.Security.SecureSocketOptions.SslOnConnect,SendEmail.EMAIL_SEND_TIMEOUT_MS);
					Assert.IsNull(fakeSmtpServer.ServerError,fakeSmtpServer.ServerError?.ToString());
					FakeSmtpServer.Received received=fakeSmtpServer.ListReceived.Single();
					Assert.IsTrue(received.WasTls,"TLS from the first byte.");
					MimeMessage mimeMessage;
					using(MemoryStream memoryStream=new MemoryStream(received.Data)) {
						mimeMessage=MimeMessage.Load(memoryStream);
					}
					Assert.AreEqual(2,mimeMessage.Attachments.Count());
					AssertForwardedEml(mimeMessage,pathEml);
					MimePart mimePartAttachment=mimeMessage.Attachments.OfType<MimePart>().Single();
					Assert.AreEqual(Path.GetFileName(pathAttachment),mimePartAttachment.ContentId,"The pdf's own A to Z file name, not the .eml's.");
					Assert.IsTrue(string.Equals(ContentDisposition.Attachment,mimePartAttachment.ContentDisposition.Disposition,StringComparison.OrdinalIgnoreCase));
					Assert.AreEqual("Statement 2026.pdf",mimePartAttachment.ContentDisposition.FileName);
					using(MemoryStream memoryStream=new MemoryStream()) {
						mimePartAttachment.Content.DecodeTo(memoryStream);
						CollectionAssert.AreEqual(arrayAttachment,memoryStream.ToArray());
					}
					Assert.AreEqual("Café statement",mimeMessage.Subject);
				}
			}
			finally {
				SendEmail.SmtpCertificateValidationForTests=null;
			}
		}

		[TestMethod]
		public void ImplicitTls_RefusesAnUntrustedCertificate() {
			string pathAttachment=Path.Combine(_folder,"6_untrusted.pdf");
			File.WriteAllBytes(pathAttachment,GetAttachmentBytes());
			using(FakeSmtpServer fakeSmtpServer=new FakeSmtpServer(USER,PASSWORD,isStartTlsOffered:false,isImplicitTls:true)) {
				BasicEmailAddress basicEmailAddress=MakeAddress(fakeSmtpServer.Port,true);
				try {
					SendEmail.SendSmtpMailKit(basicEmailAddress,SendEmail.CreateMimeMessageForSmtp(basicEmailAddress,MakeMessage(pathAttachment)),
						MailKit.Security.SecureSocketOptions.SslOnConnect,SendEmail.EMAIL_SEND_TIMEOUT_MS);
					Assert.Fail("A self-signed server certificate is refused.");
				}
				catch(MailKit.Security.SslHandshakeException) {
				}
				Assert.AreEqual(0,fakeSmtpServer.ListReceived.Count);
				Assert.IsFalse(fakeSmtpServer.GetCommands().Any(x => x.StartsWith("AUTH")),"No password went out.");
			}
		}

		///<summary>WireEmailUnsecure itself on port 465: when the port is free on 127.0.0.1, the fake server listens there and the message goes through
		///MailKit with TLS from the first byte. Inconclusive when something else has the port.</summary>
		[TestMethod]
		public void Port465_WireEmailUnsecureUsesImplicitTls() {
			byte[] arrayAttachment=GetAttachmentBytes();
			string path=Path.Combine(_folder,"7_port465.pdf");
			File.WriteAllBytes(path,arrayAttachment);
			FakeSmtpServer fakeSmtpServer;
			try {
				fakeSmtpServer=new FakeSmtpServer(USER,PASSWORD,isStartTlsOffered:false,isImplicitTls:true,port:465);
			}
			catch(System.Net.Sockets.SocketException ex) {
				Assert.Inconclusive("Port 465 on 127.0.0.1 is in use: "+ex.Message);
				return;
			}
			SendEmail.SmtpCertificateValidationForTests=TestCertificates.IsTestCertificate;
			try {
				using(fakeSmtpServer) {
					SendEmail.WireEmailUnsecure(MakeAddress(465,true),MakeMessage(path));
					AssertReceived(fakeSmtpServer,arrayAttachment,true);
				}
			}
			finally {
				SendEmail.SmtpCertificateValidationForTests=null;
			}
		}

		[TestMethod]
		public void WrongPassword_IsAnError() {
			string path=Path.Combine(_folder,"3_wrong.pdf");
			File.WriteAllBytes(path,GetAttachmentBytes());
			using(FakeSmtpServer fakeSmtpServer=new FakeSmtpServer(USER,"another-password",isStartTlsOffered:false)) {
				try {
					SendEmail.WireEmailUnsecure(MakeAddress(fakeSmtpServer.Port,false),MakeMessage(path));
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
