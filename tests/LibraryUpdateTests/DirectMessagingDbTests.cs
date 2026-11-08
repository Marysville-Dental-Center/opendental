// Direct secure messaging end to end (package S0-3D of the .NET 10 plan: Direct is kept, owner decision 2026-11-08), on a scratch database:
// a message sent with EmailMessages.SendEmail(useDirect) leaves encrypted through MailKit (to the fake SMTP server on 127.0.0.1, plain and on
// port 465, which the System.Net.Mail version refused) and is decrypted, parsed and stored by ProcessRawEmailMessageIn on the receiving side;
// the receiver's acknowledgement (MDN) is saved, resent by SendOldestUnsentAck after a failed first try, and decrypted by the sender; the
// corpus's encrypted message, made again for this run's certificates, decrypts (the recorded one can't: its key was never saved), stays
// ReceivedEncrypted without its certificate, and decrypts later as the Decrypt button does. Both offices' certificates are self-signed, made at
// run time and kept in stores of the current user that are deleted afterwards (DirectTestStores); the hosted-certificate search is stood in for.
// The database is named s0d_*, made from db/init/new-database.sql and dropped afterwards; inconclusive without the local MariaDB. Synthetic data
// only. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using CodeBase;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MimeKit;
using MySqlConnector;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.Email;
using OpenPmsDentalBusiness.ForkMigrations;
using UnitTestsCore;

namespace LibraryUpdateTests {
	[TestClass]
	public class DirectMessagingDbTests {
		private const string PREFIX="s0d_";
		private static readonly string DB_NAME=PREFIX+DateTime.UtcNow.ToString("yyMMddHHmmss",CultureInfo.InvariantCulture)+"_"
			+Process.GetCurrentProcess().Id+"_"+new Random().Next(1000,10000)+"_direct";
		private const string SENDER="front@direct.example.test";
		private const string RECEIVER="pat@hisp.example.test";
		private const string PASSWORD="synthetic-smtp-password";
		private const string USER_NAME_OD="S0dSender";
		private static string _skipReason;
		private static bool _isBuilt;
		private static string _folderAtoZ;
		private string _localAtoZpathOld;
		private bool _isUnitTestOld;
		private DirectTestStores _directTestStores;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=ScratchDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			try {
				DropDatabase(DB_NAME);
				using(MySqlConnection connection=ScratchDb.OpenServer()) {
					ScratchDb.Exec(connection,"CREATE DATABASE `"+DB_NAME+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
				}
				_isBuilt=true;
				ScratchDb.LoadNewDatabase(DB_NAME);
				ScratchDb.Connect(DB_NAME);
				Security.CurUser=new Userod { UserNum=1,UserName="s0d" };
				ForkMigrationRunner.Run();
				Cache.Refresh(InvalidType.AllLocal);
				//A user in the first user group (Admin in a new database), who may send email.
				UserodT.CreateUser(USER_NAME_OD,"synthetic-od-password",new List<long> { 1 });
				_folderAtoZ=Path.Combine(Path.GetTempPath(),"s0d_direct_atoz_"+Guid.NewGuid().ToString("N"));
				Directory.CreateDirectory(Path.Combine(_folderAtoZ,"A"));
			}
			catch(Exception ex) {
				_skipReason="Could not build the scratch database: "+ex.GetBaseException().Message;
			}
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_isBuilt) {
				try {
					DropDatabase(DB_NAME);
				}
				catch(Exception) {
					//Nothing else uses it; it holds synthetic data only.
				}
			}
			if(_folderAtoZ!=null) {
				try {
					Directory.Delete(_folderAtoZ,true);
				}
				catch(Exception) {
				}
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			_isUnitTestOld=ODBuild.IsUnitTest;
			ODBuild.IsUnitTest=true;
			ODInitialize.Initialize();
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
			bool isOtherDatabase=DataConnection.GetDatabaseName()!=DB_NAME;
			ScratchDb.Connect(DB_NAME);
			if(isOtherDatabase) {
				Cache.Refresh(InvalidType.AllLocal);
			}
			Security.CurUser=Userods.GetUserByName(USER_NAME_OD,false);
			_localAtoZpathOld=OpenPmsDentalBusiness.FileIO.FileAtoZ.LocalAtoZpath;
			OpenPmsDentalBusiness.FileIO.FileAtoZ.LocalAtoZpath=_folderAtoZ;
			_directTestStores=new DirectTestStores();
		}

		[TestCleanup]
		public void TestCleanup() {
			if(_skipReason!=null) {
				return;
			}
			_directTestStores?.Dispose();
			SendEmail.SmtpCertificateValidationForTests=null;
			OpenPmsDentalBusiness.FileIO.FileAtoZ.LocalAtoZpath=_localAtoZpathOld;
			ODBuild.IsUnitTest=_isUnitTestOld;
		}

		private static void DropDatabase(string name) {
			if(!name.StartsWith(PREFIX)) {
				throw new ArgumentException("These tests only drop databases whose name starts with "+PREFIX+".");
			}
			using(MySqlConnection connection=ScratchDb.OpenServer()) {
				ScratchDb.Exec(connection,"DROP DATABASE IF EXISTS `"+name+"`");
			}
		}

		#region Helpers
		private static EmailAddress MakeAddress(string userName,int port,bool useSsl) {
			EmailAddress emailAddress=new EmailAddress();
			emailAddress.EmailUsername=userName;
			emailAddress.EmailPassword=MiscUtils.Encrypt(PASSWORD);
			emailAddress.SMTPserver="127.0.0.1";
			emailAddress.ServerPort=port;
			emailAddress.UseSSL=useSsl;
			emailAddress.AuthenticationType=OAuthType.None;
			return emailAddress;
		}

		///<summary>The certificates of both offices: each has its own with its private key (the stores of this test hold both, as two offices'
		///stores would), the receiver's is hosted for its Direct address, and the receiver trusts the sender's signature.</summary>
		private void SetUpCertificates(out X509Certificate2 x509Certificate2Sender,out X509Certificate2 x509Certificate2Receiver) {
			x509Certificate2Sender=_directTestStores.CreateCertificate(SENDER);
			x509Certificate2Receiver=_directTestStores.CreateCertificate(RECEIVER);
			_directTestStores.AddPrivate(x509Certificate2Sender);
			_directTestStores.AddPrivate(x509Certificate2Receiver);
			EmailMessages.CreateCertificateStoresIfNeeded();
			_directTestStores.Host(RECEIVER,x509Certificate2Receiver);//The send discovers it (DNS or LDAP in production) and trusts it.
			_directTestStores.Host(SENDER,x509Certificate2Sender);//The receiver's acknowledgement goes back to the sender the same way.
			EmailMessages.TryAddTrustForSignature(DirectTestStores.Public(x509Certificate2Sender));//The receiver trusts the sender (Trust button).
		}

		private static EmailMessage MakeMessage(string subject,string bodyText,params EmailAttach[] arrayEmailAttaches) {
			EmailMessage emailMessage=new EmailMessage();
			emailMessage.FromAddress=SENDER;
			emailMessage.ToAddress=RECEIVER;
			emailMessage.Subject=subject;
			emailMessage.BodyText=bodyText;
			emailMessage.HtmlType=EmailType.Regular;
			emailMessage.SentOrReceived=EmailSentOrReceived.SentDirect;//As the message window sets it before a Direct send.
			emailMessage.MsgDateTime=DateTime.Now;
			emailMessage.Attachments=arrayEmailAttaches.ToList();
			return emailMessage;
		}

		///<summary>The raw text the POP3 download or a Gmail import hands over for these ASCII bytes.</summary>
		private static string ToRaw(byte[] arrayBytes) {
			return Encoding.GetEncoding(28591).GetString(arrayBytes);
		}

		private static MimeMessage ToMimeMessage(byte[] arrayBytes) {
			using(MemoryStream memoryStream=new MemoryStream(arrayBytes)) {
				return MimeMessage.Load(memoryStream);
			}
		}

		private static byte[] ReadAttachment(EmailAttach emailAttach) {
			return File.ReadAllBytes(Path.Combine(EmailAttaches.GetAttachPath(),emailAttach.ActualFileName));
		}

		///<summary>Asserts the message arrived encrypted as Direct sends it: one application/pkcs7-mime body (enveloped data), the subject replaced,
		///and none of the plain text in it.</summary>
		private static void AssertEncrypted(byte[] arrayBytes,string bodyText) {
			MimeMessage mimeMessage=ToMimeMessage(arrayBytes);
			Assert.IsInstanceOfType(mimeMessage.Body,typeof(MimeKit.Cryptography.ApplicationPkcs7Mime),mimeMessage.Body.ContentType.MimeType);
			Assert.AreEqual(MimeKit.Cryptography.SecureMimeType.EnvelopedData,((MimeKit.Cryptography.ApplicationPkcs7Mime)mimeMessage.Body).SecureMimeType);
			Assert.AreEqual("Encrypted Message",mimeMessage.Subject);
			Assert.IsFalse(Encoding.ASCII.GetString(arrayBytes).Contains(bodyText),"The text is not readable on the wire.");
		}

		///<summary>A port of 127.0.0.1 that nothing listens on (taken and given back).</summary>
		private static int GetClosedPort() {
			TcpListener tcpListener=new TcpListener(IPAddress.Loopback,0);
			tcpListener.Start();
			int port=((IPEndPoint)tcpListener.LocalEndpoint).Port;
			tcpListener.Stop();
			return port;
		}
		#endregion Helpers

		///<summary>The message window's Direct Messaging send, and the receiving inbox: SendEmail(useDirect) finds the receiver's hosted
		///certificate, trusts it, has the Direct agent sign and encrypt the message with its attachment, and sends it through MailKit; the receiver
		///decrypts it with its private certificate and reads it with MimeKit, as ReceivedDirect with the attachment and the sender's signature.</summary>
		[TestMethod]
		public void DirectSend_ThenReceive_DecryptsAndParses() {
			SetUpCertificates(out X509Certificate2 x509Certificate2Sender,out X509Certificate2 x509Certificate2Receiver);
			byte[] arrayPdf=new byte[] { 0x25,0x50,0x44,0x46,0x2D,0x31,0x2E,0x34,0x0A,0x00,0xFF,0x0D,0x0A,0x2E };
			EmailAttach emailAttach=EmailAttaches.CreateAttach("Referral 2026.pdf","",arrayPdf,true);
			const string BODY="Referral for Pat attached.\r\nPlease call the front desk.";
			EmailMessage emailMessage=MakeMessage("Direct referral",BODY,emailAttach);
			byte[] arrayReceived;
			using(FakeSmtpServer fakeSmtpServer=new FakeSmtpServer(SENDER,PASSWORD,isStartTlsOffered:false)) {
				EmailMessages.SendEmail(emailMessage,MakeAddress(SENDER,fakeSmtpServer.Port,false),useDirect:true);
				Assert.IsNull(fakeSmtpServer.ServerError,fakeSmtpServer.ServerError?.ToString());
				FakeSmtpServer.Received received=fakeSmtpServer.ListReceived.Single();
				CollectionAssert.AreEqual(new List<string> { "<"+RECEIVER+">" },received.ListRcptTo);
				arrayReceived=received.Data;
			}
			AssertEncrypted(arrayReceived,"Please call the front desk.");
			CollectionAssert.Contains(_directTestStores.ListHostedLookups,RECEIVER,"The send looked for the receiver's hosted certificate.");
			CollectionAssert.Contains(_directTestStores.GetThumbprints(_directTestStores.NameAnchor),x509Certificate2Receiver.Thumbprint,"And trusts it.");
			Assert.AreNotEqual(0,emailMessage.EmailMessageNum,"Saved.");
			EmailMessage emailMessageSaved=EmailMessages.GetOne(emailMessage.EmailMessageNum);
			Assert.AreEqual(EmailSentOrReceived.SentDirect,emailMessageSaved.SentOrReceived);
			Assert.AreEqual(SENDER,emailMessageSaved.FromAddress);
			//The receiving inbox.
			string raw=ToRaw(arrayReceived);
			EmailMessage emailMessageReceived=EmailMessages.ProcessRawEmailMessageIn(raw,0,MakeAddress(RECEIVER,25,false),isAck:false);
			Assert.AreEqual(EmailSentOrReceived.ReceivedDirect,emailMessageReceived.SentOrReceived,emailMessageReceived.BodyText);
			Assert.AreEqual("Direct referral",emailMessageReceived.Subject,"The wrapped message's own subject, not the outer \"Encrypted Message\".");
			Assert.AreEqual(SENDER,emailMessageReceived.FromAddress);
			Assert.AreEqual(RECEIVER,emailMessageReceived.ToAddress);
			Assert.AreEqual(BODY,emailMessageReceived.BodyText.TrimEnd('\r','\n'));
			Assert.AreEqual(raw,emailMessageReceived.RawEmailIn,"RawEmailIn stays as received.");
			Assert.AreEqual(RECEIVER,emailMessageReceived.RecipientAddress);
			EmailAttach emailAttachPdf=emailMessageReceived.Attachments.Single(x => x.DisplayedFileName=="Referral 2026.pdf");
			CollectionAssert.AreEqual(arrayPdf,ReadAttachment(emailAttachPdf));
			EmailAttach emailAttachSignature=emailMessageReceived.Attachments.Single(x => x.DisplayedFileName=="smime.p7s");
			CollectionAssert.AreEqual(x509Certificate2Sender.RawData,ReadAttachment(emailAttachSignature),"The sender's signing certificate.");
			EmailMessage emailMessageStored=EmailMessages.GetOne(emailMessageReceived.EmailMessageNum);
			Assert.AreEqual(EmailSentOrReceived.ReceivedDirect,emailMessageStored.SentOrReceived);
			Assert.AreEqual(2,EmailAttaches.GetForEmail(emailMessageReceived.EmailMessageNum).Count);
			//The preview of the stored message reads the decrypted parts.
			List<List<EmailMimePart>> listListParts=EmailMessages.GetMimePartsForMimeTypes(emailMessageStored.RawEmailIn,MakeAddress(RECEIVER,25,false),"text/plain");
			Assert.AreEqual(BODY,EmailMessages.ProcessMimeTextPart(listListParts[0].Single()).TrimEnd('\r','\n'));
		}

		///<summary>Port 465: the System.Net.Mail version of the Direct send refused implicit SSL ("Direct messages cannot be sent over implicit
		///SSL"); MailKit sends it with TLS from the first byte. Inconclusive when something else has the port on 127.0.0.1.</summary>
		[TestMethod]
		public void DirectSend_Port465_ArrivesEncrypted() {
			FakeSmtpServer fakeSmtpServer;
			try {
				fakeSmtpServer=new FakeSmtpServer(SENDER,PASSWORD,isStartTlsOffered:false,isImplicitTls:true,port:465);
			}
			catch(SocketException ex) {
				Assert.Inconclusive("Port 465 on 127.0.0.1 is in use: "+ex.Message);
				return;
			}
			SetUpCertificates(out X509Certificate2 x509Certificate2Sender,out X509Certificate2 x509Certificate2Receiver);
			SendEmail.SmtpCertificateValidationForTests=TestCertificates.IsTestCertificate;
			byte[] arrayReceived;
			using(fakeSmtpServer) {
				EmailMessages.SendEmail(MakeMessage("Direct over 465","Sent over implicit TLS."),MakeAddress(SENDER,465,true),useDirect:true);
				Assert.IsNull(fakeSmtpServer.ServerError,fakeSmtpServer.ServerError?.ToString());
				FakeSmtpServer.Received received=fakeSmtpServer.ListReceived.Single();
				Assert.IsTrue(received.WasTls);
				arrayReceived=received.Data;
			}
			AssertEncrypted(arrayReceived,"Sent over implicit TLS.");
			EmailMessage emailMessageReceived=EmailMessages.ProcessRawEmailMessageIn(ToRaw(arrayReceived),0,MakeAddress(RECEIVER,25,false),isAck:false);
			Assert.AreEqual(EmailSentOrReceived.ReceivedDirect,emailMessageReceived.SentOrReceived,emailMessageReceived.BodyText);
			Assert.AreEqual("Sent over implicit TLS.",emailMessageReceived.BodyText.TrimEnd('\r','\n'));
		}

		///<summary>The private store also holds a valid certificate without its private key for the sender's address (a .cer imported into the
		///wrong store, the public half of an old certificate), and one for the receiver's: the agent is not given them (the store's criteria, as
		///the Direct Project's SystemX509Store.OpenPrivate had it before S0-3), so it still signs with the sender's own certificate, encrypts, and
		///the receiver decrypts with its own. Handed to the agent, the keyless certificate made the send fail ("Cannot recover from an encryption
		///error"): the agent signs with every certificate it is given.</summary>
		[TestMethod]
		public void DirectSend_WithAKeylessCertificateInThePrivateStore_StillSignsAndEncrypts() {
			SetUpCertificates(out X509Certificate2 x509Certificate2Sender,out X509Certificate2 x509Certificate2Receiver);
			_directTestStores.AddPrivateWithoutKey(_directTestStores.CreateCertificate(SENDER));
			_directTestStores.AddPrivateWithoutKey(_directTestStores.CreateCertificate(RECEIVER));
			Assert.AreEqual(4,_directTestStores.GetThumbprints(_directTestStores.NamePrivate).Count);
			EmailMessage emailMessage=MakeMessage("Direct with a keyless certificate","Signed with the key the office has.");
			byte[] arrayReceived;
			using(FakeSmtpServer fakeSmtpServer=new FakeSmtpServer(SENDER,PASSWORD,isStartTlsOffered:false)) {
				EmailMessages.SendEmail(emailMessage,MakeAddress(SENDER,fakeSmtpServer.Port,false),useDirect:true);
				Assert.IsNull(fakeSmtpServer.ServerError,fakeSmtpServer.ServerError?.ToString());
				arrayReceived=fakeSmtpServer.ListReceived.Single().Data;
			}
			Assert.AreEqual(EmailSentOrReceived.SentDirect,emailMessage.SentOrReceived,emailMessage.FailReason);
			AssertEncrypted(arrayReceived,"Signed with the key the office has.");
			EmailMessage emailMessageReceived=EmailMessages.ProcessRawEmailMessageIn(ToRaw(arrayReceived),0,MakeAddress(RECEIVER,25,false),isAck:false);
			Assert.AreEqual(EmailSentOrReceived.ReceivedDirect,emailMessageReceived.SentOrReceived,emailMessageReceived.BodyText);
			Assert.AreEqual("Signed with the key the office has.",emailMessageReceived.BodyText.TrimEnd('\r','\n'));
			EmailAttach emailAttachSignature=emailMessageReceived.Attachments.Single(x => x.DisplayedFileName=="smime.p7s");
			CollectionAssert.AreEqual(x509Certificate2Sender.RawData,ReadAttachment(emailAttachSignature),"Signed by the sender's certificate with its key only.");
		}

		///<summary>No certificate can be found for the receiver: the Direct send fails with the reason, nothing goes out, and the message is not
		///saved (as before S0-3).</summary>
		[TestMethod]
		public void DirectSend_WithoutTheReceiversCertificate_FailsAndSavesNothing() {
			X509Certificate2 x509Certificate2Sender=_directTestStores.CreateCertificate(SENDER);
			_directTestStores.AddPrivate(x509Certificate2Sender);
			EmailMessages.CreateCertificateStoresIfNeeded();
			EmailMessage emailMessage=MakeMessage("No certificate","Nobody can read this.");
			using(FakeSmtpServer fakeSmtpServer=new FakeSmtpServer(SENDER,PASSWORD,isStartTlsOffered:false)) {
				Exception ex=AssertThrows(() => EmailMessages.SendEmail(emailMessage,MakeAddress(SENDER,fakeSmtpServer.Port,false),useDirect:true));
				StringAssert.Contains(ex.Message,"No active certificates discovered for recipient");
				Assert.AreEqual(0,fakeSmtpServer.ListReceived.Count);
			}
			Assert.AreEqual(0,emailMessage.EmailMessageNum,"A Direct message that could not be sent is not saved.");
			Assert.AreEqual(EmailSentOrReceived.SendFailed,emailMessage.SentOrReceived);
		}

		///<summary>The acknowledgement (MDN) of a received Direct message: saved as not sent when the first try fails (the SMTP server is down),
		///resent by SendOldestUnsentAck (as the inbox and each POP3 check do), encrypted for the sender, who decrypts it. An acknowledgement gets
		///none back.</summary>
		[TestMethod]
		public void Acknowledgement_IsSavedResentAndDecryptedBySender() {
			SetUpCertificates(out X509Certificate2 x509Certificate2Sender,out X509Certificate2 x509Certificate2Receiver);
			byte[] arrayReceived;
			using(FakeSmtpServer fakeSmtpServer=new FakeSmtpServer(SENDER,PASSWORD,isStartTlsOffered:false)) {
				EmailMessages.SendEmail(MakeMessage("Please confirm","Acknowledge this message."),MakeAddress(SENDER,fakeSmtpServer.Port,false),useDirect:true);
				arrayReceived=fakeSmtpServer.ListReceived.Single().Data;
			}
			//The receiver's SMTP server can't be reached: the acknowledgement stays unsent.
			EmailAddress emailAddressReceiver=MakeAddress(RECEIVER,GetClosedPort(),false);
			EmailMessage emailMessageReceived=EmailMessages.ProcessRawEmailMessageIn(ToRaw(arrayReceived),0,emailAddressReceiver,isAck:true);
			Assert.AreEqual(EmailSentOrReceived.ReceivedDirect,emailMessageReceived.SentOrReceived,emailMessageReceived.BodyText);
			List<EmailMessage> listAcks=GetAcks();
			Assert.AreEqual(1,listAcks.Count,"One acknowledgement, for the one sender.");
			Assert.AreEqual(EmailSentOrReceived.AckDirectNotSent,listAcks[0].SentOrReceived);
			Assert.AreEqual(RECEIVER,listAcks[0].FromAddress);
			StringAssert.Contains(listAcks[0].ToAddress,SENDER);
			Assert.AreEqual(emailMessageReceived.PatNum,listAcks[0].PatNum);
			StringAssert.Contains(listAcks[0].BodyText,"disposition-notification","The whole MDN is kept to send it again.");
			//The resend.
			using(FakeSmtpServer fakeSmtpServer=new FakeSmtpServer(RECEIVER,PASSWORD,isStartTlsOffered:false)) {
				emailAddressReceiver.ServerPort=fakeSmtpServer.Port;
				EmailMessages.SendOldestUnsentAck(emailAddressReceiver);
				Assert.IsNull(fakeSmtpServer.ServerError,fakeSmtpServer.ServerError?.ToString());
				FakeSmtpServer.Received received=fakeSmtpServer.ListReceived.Single();
				CollectionAssert.AreEqual(new List<string> { "<"+SENDER+">" },received.ListRcptTo);
				arrayReceived=received.Data;
				//Throttled: no other acknowledgement goes out within a minute of the last one.
				EmailMessages.SendOldestUnsentAck(emailAddressReceiver);
				Assert.AreEqual(1,fakeSmtpServer.ListReceived.Count);
			}
			listAcks=GetAcks();
			Assert.AreEqual(EmailSentOrReceived.AckDirectProcessed,listAcks.Single().SentOrReceived);
			MimeMessage mimeMessageAck=ToMimeMessage(arrayReceived);
			Assert.IsInstanceOfType(mimeMessageAck.Body,typeof(MimeKit.Cryptography.ApplicationPkcs7Mime),"The acknowledgement is encrypted too.");
			//The sender decrypts it.
			EmailMessage emailMessageAckReceived=EmailMessages.ProcessRawEmailMessageIn(ToRaw(arrayReceived),0,MakeAddress(SENDER,GetClosedPort(),false),isAck:true);
			Assert.AreEqual(EmailSentOrReceived.ReceivedDirect,emailMessageAckReceived.SentOrReceived,emailMessageAckReceived.BodyText);
			Assert.AreEqual(RECEIVER,emailMessageAckReceived.FromAddress);
			List<List<EmailMimePart>> listListParts=EmailMessages.GetMimePartsForMimeTypes(emailMessageAckReceived.RawEmailIn,MakeAddress(SENDER,25,false),
				"message/disposition-notification");
			StringAssert.Contains(EmailMessages.ProcessMimeTextPart(listListParts[0].Single()).ToLowerInvariant(),"processed");
			Assert.AreEqual(1,GetAcks().Count,"No acknowledgement of an acknowledgement.");
		}

		///<summary>The corpus's encrypted message, made again for this run's certificates (the recorded one's private key was never saved): the
		///Direct agent decrypts it and MimeKit reads it, with the fields the recording has for its headers. Without the inbox's certificate it is
		///stored as ReceivedEncrypted with a body that says so, and decrypts later, as the email window's Decrypt button does it (from RawEmailIn).
		///</summary>
		[TestMethod]
		public void EncryptedCorpusMessage_DecryptsWhenItsCertificateIsAvailable() {
			X509Certificate2 x509Certificate2Inbox=_directTestStores.CreateCertificate(ReceivedMailCorpus.INBOX);
			X509Certificate2 x509Certificate2Sender=_directTestStores.CreateCertificate("sender@example.test");
			EmailMessages.CreateCertificateStoresIfNeeded();
			_directTestStores.AddAnchor(x509Certificate2Sender);
			string raw=ReceivedMailCorpus.MakeEncrypted(x509Certificate2Inbox,x509Certificate2Sender);
			EmailAddress emailAddressInbox=new EmailAddress() { EmailUsername=ReceivedMailCorpus.INBOX };
			//Without the inbox's private certificate.
			EmailMessage emailMessage=EmailMessages.ProcessRawEmailMessageIn(raw,0,emailAddressInbox,isAck:false);
			Assert.AreEqual(EmailSentOrReceived.ReceivedEncrypted,emailMessage.SentOrReceived);
			Assert.AreEqual(EmailMessages.ENCRYPTED_BODY_TEXT,emailMessage.BodyText);
			Assert.AreEqual(raw,emailMessage.RawEmailIn);
			Assert.AreEqual("Encrypted message",emailMessage.Subject);
			Assert.AreNotEqual(0,emailMessage.EmailMessageNum,"Stored, to decrypt later.");
			Assert.IsNotNull(AssertThrows(() => EmailMessages.ProcessRawEmailMessageIn(emailMessage.RawEmailIn,emailMessage.EmailMessageNum,emailAddressInbox,
				isAck:false,emailMessage.SentOrReceived)),"Decrypt of a stored message that still can't be decrypted shows the error.");
			Assert.AreEqual(EmailSentOrReceived.ReceivedEncrypted,EmailMessages.GetOne(emailMessage.EmailMessageNum).SentOrReceived);
			//The certificate is installed: Decrypt.
			_directTestStores.AddPrivate(x509Certificate2Inbox);
			EmailMessage emailMessageDecrypted=EmailMessages.ProcessRawEmailMessageIn(emailMessage.RawEmailIn,emailMessage.EmailMessageNum,emailAddressInbox,
				isAck:false,emailMessage.SentOrReceived);
			AssertCorpusFields(emailMessageDecrypted,raw,x509Certificate2Sender);
			EmailMessage emailMessageStored=EmailMessages.GetOne(emailMessage.EmailMessageNum);
			Assert.AreEqual(EmailSentOrReceived.ReceivedDirect,emailMessageStored.SentOrReceived,"Updated in the database.");
			Assert.AreEqual(emailMessageDecrypted.BodyText,emailMessageStored.BodyText);
			//A new download of it decrypts at once.
			AssertCorpusFields(EmailMessages.ProcessRawEmailMessageIn(raw,0,emailAddressInbox,isAck:false),raw,x509Certificate2Sender);
		}

		///<summary>The fields of the decrypted corpus message: those the recording has for its headers (Corpus/ReceivedMailCorpus.expected.txt,
		///"encrypted"), the part's text as the body, ReceivedDirect, the raw message as received, and the sender's certificate as smime.p7s.</summary>
		private static void AssertCorpusFields(EmailMessage emailMessage,string raw,X509Certificate2 x509Certificate2Sender) {
			Assert.AreEqual(EmailSentOrReceived.ReceivedDirect,emailMessage.SentOrReceived,emailMessage.BodyText);
			Assert.AreEqual("Sender Example <sender@example.test>",emailMessage.FromAddress);
			Assert.AreEqual("inbox@example.test",emailMessage.ToAddress);
			Assert.AreEqual("",emailMessage.CcAddress);
			Assert.AreEqual("",emailMessage.BccAddress);
			Assert.AreEqual("Encrypted message",emailMessage.Subject);
			Assert.AreEqual("2026-11-03 19:00:00",emailMessage.MsgDateTime.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture));
			//The part's text. The Direct serializer ends the decrypted message with one more CRLF, which shows as one more line end.
			Assert.AreEqual("Only the holder of the private key can read this.",emailMessage.BodyText.TrimEnd('\r','\n'));
			StringAssert.EndsWith(emailMessage.BodyText,"\r\n");
			Assert.AreEqual("inbox@example.test",emailMessage.RecipientAddress);
			Assert.AreEqual(raw,emailMessage.RawEmailIn);
			Assert.AreEqual(1,emailMessage.Attachments.Count);
			Assert.AreEqual("smime.p7s",emailMessage.Attachments[0].DisplayedFileName);
			CollectionAssert.AreEqual(x509Certificate2Sender.RawData,ReadAttachment(emailMessage.Attachments[0]));
		}

		///<summary>The acknowledgements stored so far (sent or not), oldest first.</summary>
		private static List<EmailMessage> GetAcks() {
			List<EmailSentOrReceived> listAckTypes=EmailMessages.GetUnsentTypes(EmailPlatform.Ack).Concat(EmailMessages.GetSentTypes(EmailPlatform.Ack)).ToList();
			string command="SELECT * FROM emailmessage WHERE SentOrReceived IN ("+string.Join(",",listAckTypes.Select(x => (int)x))+") ORDER BY EmailMessageNum";
			return OpenPmsDentalBusiness.Crud.EmailMessageCrud.SelectMany(command);
		}

		private static Exception AssertThrows(Action action) {
			try {
				action();
			}
			catch(Exception ex) {
				return ex;
			}
			Assert.Fail("Expected an exception.");
			return null;
		}
	}
}
