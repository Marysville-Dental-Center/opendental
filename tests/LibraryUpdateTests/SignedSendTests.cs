// Signed e-mail (S/MIME, multipart/signed) on MimeKit (package S0-3 of the .NET 10 plan, step 4; open point O-1: signed sending is kept): the
// program signs with MimeKit's Windows S/MIME context on the certificate GetCertFromPrivateStore finds, MimeKit and BouncyCastle verify what
// it sent, and it goes out through MailKit on every port (port 465 too, which the Direct-based code refused), against the fake SMTP server on
// 127.0.0.1. The certificates are self-signed, made in memory for each test and removed afterwards; the store test uses a store of the current
// user with a name of its own, which it deletes. The real NHINDPrivate store (Local Computer) is only read, never written. Licensed under the
// GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MimeKit;
using MimeKit.Cryptography;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.Email;

namespace LibraryUpdateTests {
	[TestClass]
	public class SignedSendTests {
		private const string USER="front@example.test";
		private const string PASSWORD="synthetic-smtp-password";
		private static string _folder;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_folder=Path.Combine(Path.GetTempPath(),"libraryupdatetests_signed_"+Guid.NewGuid().ToString("N"));
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
			return new BasicEmailAddress() { SMTPserver="127.0.0.1",ServerPort=port,UseSSL=useSsl,EmailUsername=USER,EmailPassword=PASSWORD,
				AuthenticationType=BasicOAuthType.None };
		}

		private static BasicEmailMessage MakeMessage() {
			byte[] arrayAttachment=new byte[] { 0x25,0x50,0x44,0x46,0x0A,0x00,0xFF,0x0D,0x0A,0x2E };
			string path=Path.Combine(_folder,Guid.NewGuid().ToString("N")+".pdf");
			File.WriteAllBytes(path,arrayAttachment);
			return new BasicEmailMessage() {
				FromAddress=USER,
				ToAddress="Pat Smith <pat@example.test>",
				CcAddress="",
				BccAddress="",
				Subject="Signed statement",
				IsHtml=false,
				//A line with trailing spaces, one that starts with "From " and one with a dot: what the signing filters and dot-stuffing touch.
				BodyText="Your statement is attached.   \r\nFrom the front desk\r\n.\r\nCafé ends here",
				ListAttachments=new List<BasicEmailAttachment> { new BasicEmailAttachment(path,"Statement 2026.pdf") },
			};
		}

		///<summary>Checks a received signed message: multipart/signed with SHA-256, the signature verifies (MimeKit with BouncyCastle; the
		///self-signed certificate is not trusted, so only the signature is checked) and carries the certificate, and the signed part holds the text
		///and the attachment.</summary>
		private static void AssertSignedAndVerifies(byte[] arrayMessage,X509Certificate2 x509Certificate2) {
			MimeMessage mimeMessage;
			using(MemoryStream memoryStream=new MemoryStream(arrayMessage)) {
				mimeMessage=MimeMessage.Load(memoryStream);
			}
			MultipartSigned multipartSigned=mimeMessage.Body as MultipartSigned;
			Assert.IsNotNull(multipartSigned,"multipart/signed, not "+mimeMessage.Body.ContentType.MimeType);
			Assert.AreEqual("application/pkcs7-signature",multipartSigned.ContentType.Parameters["protocol"]);
			Assert.AreEqual("sha-256",multipartSigned.ContentType.Parameters["micalg"]);
			using(TemporarySecureMimeContext temporarySecureMimeContext=new TemporarySecureMimeContext()) {
				DigitalSignatureCollection digitalSignatureCollection=multipartSigned.Verify(temporarySecureMimeContext);
				Assert.AreEqual(1,digitalSignatureCollection.Count);
				Assert.IsTrue(digitalSignatureCollection[0].Verify(true),"The signature matches the signed part as it arrived.");
				Assert.AreEqual(x509Certificate2.Thumbprint,digitalSignatureCollection[0].SignerCertificate.Fingerprint.ToUpperInvariant());
			}
			//Trailing spaces may go before signing (RFC 3156 5.4) unless the encoding keeps them; the rest arrives as written.
			string textBody=string.Join("\r\n",mimeMessage.TextBody.TrimEnd('\r','\n').Split(new[] { "\r\n" },StringSplitOptions.None).Select(x => x.TrimEnd(' ')));
			Assert.AreEqual("Your statement is attached.\r\nFrom the front desk\r\n.\r\nCafé ends here",textBody);
			MimePart mimePartAttachment=mimeMessage.Attachments.OfType<MimePart>().Single(x => x.FileName!="smime.p7s");
			Assert.AreEqual("Statement 2026.pdf",mimePartAttachment.FileName);
			Assert.AreEqual("Signed statement",mimeMessage.Subject);
		}

		private static byte[] ToBytes(MimeMessage mimeMessage) {
			using(MemoryStream memoryStream=new MemoryStream()) {
				mimeMessage.WriteTo(memoryStream);
				return memoryStream.ToArray();
			}
		}

		[TestMethod]
		public void SignMimeMessage_IsMultipartSignedThatVerifies() {
			X509Certificate2 x509Certificate2=TestCertificates.CreateEmail(USER);
			try {
				MimeMessage mimeMessage=SendEmail.CreateMimeMessageForSmtp(MakeAddress(587,false),MakeMessage());
				SendEmail.SignMimeMessage(mimeMessage,x509Certificate2);
				AssertSignedAndVerifies(ToBytes(mimeMessage),x509Certificate2);
				//Windows' own S/MIME (SignedCms, through MimeKit's Windows context) verifies it too.
				MultipartSigned multipartSigned=(MultipartSigned)mimeMessage.Body;
				using(WindowsSecureMimeContext windowsSecureMimeContext=new WindowsSecureMimeContext()) {
					Assert.IsTrue(multipartSigned.Verify(windowsSecureMimeContext)[0].Verify(true));
				}
			}
			finally {
				x509Certificate2.Reset();
			}
		}

		[TestMethod]
		public void SignMimeMessage_WorksWithAKeyThatCantBeExported() {
			X509Certificate2 x509Certificate2=TestCertificates.CreateEmail(USER,isExportable:false);
			try {
				MimeMessage mimeMessage=SendEmail.CreateMimeMessageForSmtp(MakeAddress(587,false),MakeMessage());
				SendEmail.SignMimeMessage(mimeMessage,x509Certificate2);
				AssertSignedAndVerifies(ToBytes(mimeMessage),x509Certificate2);
			}
			finally {
				x509Certificate2.Reset();
			}
		}

		[TestMethod]
		public void SignMimeMessage_RefusesACertificateWithoutItsKey() {
			X509Certificate2 x509Certificate2=TestCertificates.CreateEmail(USER);
			X509Certificate2 x509Certificate2Public=new X509Certificate2(x509Certificate2.RawData);
			try {
				MimeMessage mimeMessage=SendEmail.CreateMimeMessageForSmtp(MakeAddress(587,false),MakeMessage());
				try {
					SendEmail.SignMimeMessage(mimeMessage,x509Certificate2Public);
					Assert.Fail("No private key, no signature.");
				}
				catch(ApplicationException) {
				}
			}
			finally {
				x509Certificate2.Reset();
			}
		}

		[TestMethod]
		public void SignedSend_PlainSmtp_ArrivesSignedAndVerifies() {
			X509Certificate2 x509Certificate2=TestCertificates.CreateEmail(USER);
			try {
				using(FakeSmtpServer fakeSmtpServer=new FakeSmtpServer(USER,PASSWORD,isStartTlsOffered:false)) {
					SendEmail.WireEmailUnsecure(MakeAddress(fakeSmtpServer.Port,false),MakeMessage(),x509Certificate2);
					Assert.IsNull(fakeSmtpServer.ServerError,fakeSmtpServer.ServerError?.ToString());
					FakeSmtpServer.Received received=fakeSmtpServer.ListReceived.Single();
					Assert.IsFalse(received.WasTls);
					AssertSignedAndVerifies(received.Data,x509Certificate2);
				}
			}
			finally {
				x509Certificate2.Reset();
			}
		}

		[TestMethod]
		public void SignedSend_StartTls_ArrivesSignedAndVerifies() {
			X509Certificate2 x509Certificate2=TestCertificates.CreateEmail(USER);
			SendEmail.SmtpCertificateValidationForTests=TestCertificates.IsTestCertificate;
			try {
				using(FakeSmtpServer fakeSmtpServer=new FakeSmtpServer(USER,PASSWORD,isStartTlsOffered:true)) {
					SendEmail.WireEmailUnsecure(MakeAddress(fakeSmtpServer.Port,true),MakeMessage(),x509Certificate2);
					Assert.IsNull(fakeSmtpServer.ServerError,fakeSmtpServer.ServerError?.ToString());
					FakeSmtpServer.Received received=fakeSmtpServer.ListReceived.Single();
					Assert.IsTrue(received.WasTls,"UseSSL on another port than 465: STARTTLS, as System.Net.Mail did.");
					List<string> listCommands=fakeSmtpServer.GetCommands();
					Assert.IsTrue(listCommands.IndexOf("STARTTLS")>=0 && listCommands.IndexOf("STARTTLS")<listCommands.FindIndex(x => x.StartsWith("AUTH")),
						"The password goes only over TLS: "+string.Join(" | ",listCommands));
					AssertSignedAndVerifies(received.Data,x509Certificate2);
				}
			}
			finally {
				SendEmail.SmtpCertificateValidationForTests=null;
				x509Certificate2.Reset();
			}
		}

		///<summary>Port 465: the Direct-based code refused to send a signed message over implicit SSL; MailKit sends it. Inconclusive when something
		///else has the port on 127.0.0.1.</summary>
		[TestMethod]
		public void SignedSend_Port465_ArrivesSignedAndVerifies() {
			FakeSmtpServer fakeSmtpServer;
			try {
				fakeSmtpServer=new FakeSmtpServer(USER,PASSWORD,isStartTlsOffered:false,isImplicitTls:true,port:465);
			}
			catch(System.Net.Sockets.SocketException ex) {
				Assert.Inconclusive("Port 465 on 127.0.0.1 is in use: "+ex.Message);
				return;
			}
			X509Certificate2 x509Certificate2=TestCertificates.CreateEmail(USER);
			SendEmail.SmtpCertificateValidationForTests=TestCertificates.IsTestCertificate;
			try {
				using(fakeSmtpServer) {
					SendEmail.WireEmailUnsecure(MakeAddress(465,true),MakeMessage(),x509Certificate2);
					Assert.IsNull(fakeSmtpServer.ServerError,fakeSmtpServer.ServerError?.ToString());
					FakeSmtpServer.Received received=fakeSmtpServer.ListReceived.Single();
					Assert.IsTrue(received.WasTls);
					AssertSignedAndVerifies(received.Data,x509Certificate2);
				}
			}
			finally {
				SendEmail.SmtpCertificateValidationForTests=null;
				x509Certificate2.Reset();
			}
		}

		///<summary>Every signed send builds the message as port 465 does: an attached .eml (a MimeKit MessagePart, not a MimePart) before the pdf is
		///signed with it and arrives, over STARTTLS through WireEmailUnsecure, with its Content-ID, disposition and displayed file name; the pdf
		///keeps its own Content-ID; the signature still verifies.</summary>
		[TestMethod]
		public void SignedSend_WithAnAttachedEml_ArrivesSignedAndVerifies() {
			X509Certificate2 x509Certificate2=TestCertificates.CreateEmail(USER);
			string pathEml=Path.Combine(_folder,Guid.NewGuid().ToString("N")+".eml");
			SmtpTests.WriteForwardedEml(pathEml);
			BasicEmailMessage basicEmailMessage=MakeMessage();
			string pathPdf=basicEmailMessage.ListAttachments[0].FullPath;
			basicEmailMessage.ListAttachments.Insert(0,new BasicEmailAttachment(pathEml,"Lab results.eml"));
			SendEmail.SmtpCertificateValidationForTests=TestCertificates.IsTestCertificate;
			try {
				using(FakeSmtpServer fakeSmtpServer=new FakeSmtpServer(USER,PASSWORD,isStartTlsOffered:true)) {
					SendEmail.WireEmailUnsecure(MakeAddress(fakeSmtpServer.Port,true),basicEmailMessage,x509Certificate2);
					Assert.IsNull(fakeSmtpServer.ServerError,fakeSmtpServer.ServerError?.ToString());
					FakeSmtpServer.Received received=fakeSmtpServer.ListReceived.Single();
					Assert.IsTrue(received.WasTls);
					AssertSignedAndVerifies(received.Data,x509Certificate2);
					MimeMessage mimeMessage;
					using(MemoryStream memoryStream=new MemoryStream(received.Data)) {
						mimeMessage=MimeMessage.Load(memoryStream);
					}
					SmtpTests.AssertForwardedEml(mimeMessage,pathEml);
					MimePart mimePartAttachment=mimeMessage.Attachments.OfType<MimePart>().Single(x => x.FileName!="smime.p7s");
					Assert.AreEqual(Path.GetFileName(pathPdf),mimePartAttachment.ContentId,"The pdf's own A to Z file name, not the .eml's.");
				}
			}
			finally {
				SendEmail.SmtpCertificateValidationForTests=null;
				x509Certificate2.Reset();
			}
		}

		[TestMethod]
		public void SecureSocketOptions_AreAsBefore() {
			Assert.AreEqual(MailKit.Security.SecureSocketOptions.SslOnConnect,SendEmail.GetSecureSocketOptions(MakeAddress(465,true)));
			Assert.AreEqual(MailKit.Security.SecureSocketOptions.SslOnConnect,SendEmail.GetSecureSocketOptions(MakeAddress(465,false)),"CDO used SSL on 465 always.");
			Assert.AreEqual(MailKit.Security.SecureSocketOptions.StartTls,SendEmail.GetSecureSocketOptions(MakeAddress(587,true)));
			Assert.AreEqual(MailKit.Security.SecureSocketOptions.None,SendEmail.GetSecureSocketOptions(MakeAddress(25,false)));
		}

		#region The store of private certificates
		[DllImport("crypt32.dll",SetLastError=true,CharSet=CharSet.Unicode)]
		private static extern bool CertUnregisterSystemStore(string systemStore,uint flags);
		private const uint CERT_SYSTEM_STORE_CURRENT_USER=0x00010000;
		private const uint CERT_STORE_DELETE_FLAG=0x00000010;

		///<summary>GetCertFromPrivateStore's search, on a store of the current user made for the test, then deleted: a valid certificate for the
		///address before an expired one, a domain-level certificate when the address has none, nothing for another domain or a store that does not
		///exist. A certificate of the store without its private key can't sign and is skipped, as the Direct Project's store of private certificates
		///(SystemX509Store.OpenPrivate) skipped it before S0-3. Opening the real NHINDPrivate store (Local Computer) is not tested: the same code
		///with that name.</summary>
		[TestMethod]
		public void PrivateStore_FindsTheValidCertificateForTheAddressOrItsDomain() {
			string storeName="OpenPmsS03Test_"+Guid.NewGuid().ToString("N");
			X509Certificate2 x509Certificate2Expired=TestCertificates.CreateEmail("pat.q@example.test",dateTimeOffsetNotAfter:DateTimeOffset.UtcNow.AddDays(-3));
			X509Certificate2 x509Certificate2Valid=TestCertificates.CreateEmail("pat.q@example.test");
			X509Certificate2 x509Certificate2NoKey=TestCertificates.CreateEmail("pat.q@example.test");
			X509Certificate2 x509Certificate2NoKeyOnly=TestCertificates.CreateEmail("lee.r@example.test");
			X509Certificate2 x509Certificate2Domain=TestCertificates.CreateEmail(null,dnsName:"clinic.example.test");
			try {
				Assert.IsNull(EmailMessages.GetCertFromStore("pat.q@example.test",storeName,StoreLocation.CurrentUser),"No such store yet.");
				using(X509Store x509Store=new X509Store(storeName,StoreLocation.CurrentUser)) {
					x509Store.Open(OpenFlags.ReadWrite);//Creates the store.
					x509Store.Add(x509Certificate2Expired);
					x509Store.Add(new X509Certificate2(x509Certificate2NoKey.RawData));//Valid for the address, but without its private key.
					x509Store.Add(x509Certificate2Valid);
					x509Store.Add(new X509Certificate2(x509Certificate2NoKeyOnly.RawData));
					x509Store.Add(x509Certificate2Domain);
				}
				X509Certificate2 x509Certificate2Found=EmailMessages.GetCertFromStore("Pat Q <PAT.Q@example.test>",storeName,StoreLocation.CurrentUser);
				Assert.AreEqual(x509Certificate2Valid.Thumbprint,x509Certificate2Found?.Thumbprint,
					"The valid certificate of the address with its key, the expired one and the one without a key skipped; the display name and case do not matter.");
				Assert.IsTrue(x509Certificate2Found.HasPrivateKey);
				Assert.AreEqual(x509Certificate2Domain.Thumbprint,EmailMessages.GetCertFromStore("front@clinic.example.test",storeName,StoreLocation.CurrentUser)?.Thumbprint,
					"No certificate for the address: the domain's.");
				Assert.IsNull(EmailMessages.GetCertFromStore("lee.r@example.test",storeName,StoreLocation.CurrentUser),
					"The address's only certificate has no private key: none to sign with.");
				Assert.IsNull(EmailMessages.GetCertFromStore("someone@other.example.test",storeName,StoreLocation.CurrentUser));
				Assert.IsNull(EmailMessages.GetCertFromStore("not an address",storeName,StoreLocation.CurrentUser));
				Assert.AreEqual("pat.q@example.test",EmailCertificates.GetCertSubjectName(x509Certificate2Valid));
				Assert.AreEqual("pat.q@example.test",EmailCertificates.GetCertRfc822Name(x509Certificate2Valid));
				Assert.AreEqual("clinic.example.test",EmailCertificates.GetCertDnsName(x509Certificate2Domain));
				Assert.IsFalse(EmailCertificates.IsCertValid(x509Certificate2Expired));
			}
			finally {
				CertUnregisterSystemStore(storeName,CERT_SYSTEM_STORE_CURRENT_USER|CERT_STORE_DELETE_FLAG);
				x509Certificate2Expired.Reset();//Removes the key containers of the PFX round trips.
				x509Certificate2Valid.Reset();
				x509Certificate2NoKey.Reset();
				x509Certificate2NoKeyOnly.Reset();
				x509Certificate2Domain.Reset();
			}
			using(X509Store x509Store=new X509Store(storeName,StoreLocation.CurrentUser)) {
				try {
					x509Store.Open(OpenFlags.ReadOnly|OpenFlags.OpenExistingOnly);
					Assert.Fail("The test store is deleted.");
				}
				catch(System.Security.Cryptography.CryptographicException) {
				}
			}
		}
		#endregion
	}
}
