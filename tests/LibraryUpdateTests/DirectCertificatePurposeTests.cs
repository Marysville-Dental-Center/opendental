// Which of an address's certificates the program takes for each use, when a Direct provider issued a signing and an encryption certificate
// for one address (DIRECT-CERT, third fix round, item 2, decided by the lead on 2026-11-09; docs/port-notes/DIRECT-CERT.md): signing (the
// signed e-mail's GetCertFromPrivateStore and the Direct agent's sender certificate) prefers one for signing, encrypting to a recipient and
// decrypting prefer one for encryption, of several the one valid longest, and without one any valid certificate, as before. The send and
// the receive go through the program's own Direct agent (GetDirectAgentForEmailAddress, as SendEmailDirect and DecryptIncomingMessage make
// it), without a database or a mail server. Every store is a store of the current user made for the test and deleted afterwards
// (DirectTestStores); the certificates are made at run time by a test CA (DirectTestPki). No network. Licensed under the GNU General Public
// License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MimeKit;
using OpenPmsDentalBusiness;

namespace LibraryUpdateTests {
	[TestClass]
	public class DirectCertificatePurposeTests {
		private const string SENDER="front@direct.example.test";
		private const string RECEIVER="pat@hisp.example.test";

		///<summary>The signed e-mail's certificate: the valid one for signing valid longest, though one for encryption is valid longer; one for
		///signing and encryption counts; one not valid yet does not; with only an encryption certificate, that one, as before.</summary>
		[TestMethod]
		public void GetCertFromPrivateStore_PrefersTheSigningCertificateValidLongest() {
			using(DirectTestPki directTestPki=new DirectTestPki())
			using(DirectTestStores directTestStores=new DirectTestStores()) {
				DirectTestPki.Issued issuedEncryption=IssueEncryption(directTestPki,SENDER,50);
				directTestStores.AddPrivate(issuedEncryption.X509Certificate2WithKey);
				Assert.AreEqual(issuedEncryption.Thumbprint,EmailMessages.GetCertFromPrivateStore(SENDER)?.Thumbprint,"Only one, for encryption: it, as before.");
				DirectTestPki.Issued issuedSigning=IssueSigning(directTestPki,SENDER,20);
				directTestStores.AddPrivate(issuedSigning.X509Certificate2WithKey);
				Assert.AreEqual(issuedSigning.Thumbprint,EmailMessages.GetCertFromPrivateStore(SENDER)?.Thumbprint,"The one for signing, though the other lasts longer.");
				DirectTestPki.Issued issuedSigningLonger=IssueSigning(directTestPki,SENDER,30);
				directTestStores.AddPrivate(issuedSigningLonger.X509Certificate2WithKey);
				Assert.AreEqual(issuedSigningLonger.Thumbprint,EmailMessages.GetCertFromPrivateStore(SENDER)?.Thumbprint,"Of two for signing, the one valid longest.");
				DirectTestPki.Issued issuedBoth=directTestPki.Issue(SENDER,dateTimeOffsetNotAfter:DateTimeOffset.UtcNow.AddDays(40));
				directTestStores.AddPrivate(issuedBoth.X509Certificate2WithKey);
				Assert.AreEqual(issuedBoth.Thumbprint,EmailMessages.GetCertFromPrivateStore(SENDER)?.Thumbprint,"One for signing and encryption is for signing.");
				DirectTestPki.Issued issuedNotYet=directTestPki.Issue(SENDER,keyUsageFlags:X509KeyUsageFlags.DigitalSignature,
					dateTimeOffsetNotBefore:DateTimeOffset.UtcNow.AddDays(5),dateTimeOffsetNotAfter:DateTimeOffset.UtcNow.AddDays(45));
				directTestStores.AddPrivate(issuedNotYet.X509Certificate2WithKey);
				Assert.AreEqual(issuedBoth.Thumbprint,EmailMessages.GetCertFromPrivateStore(SENDER)?.Thumbprint,"One not valid yet is not taken.");
				//Only a signing certificate for another address: it, as before.
				DirectTestPki.Issued issuedReceiverSigning=IssueSigning(directTestPki,RECEIVER,20);
				directTestStores.AddPrivate(issuedReceiverSigning.X509Certificate2WithKey);
				Assert.AreEqual(issuedReceiverSigning.Thumbprint,EmailMessages.GetCertFromPrivateStore(RECEIVER)?.Thumbprint);
			}
		}

		///<summary>What the Direct agent's resolvers give for each use: every valid certificate for Any, as before; only the one for signing valid
		///longest for Signing, the one for encryption valid longest for Encrypting, and that one first and then the others for Decrypting. With
		///only one certificate of either purpose, that one for every use, as before.</summary>
		[TestMethod]
		public void Resolvers_GiveTheAgentTheCertificateForItsUse() {
			using(DirectTestPki directTestPki=new DirectTestPki())
			using(DirectTestStores directTestStores=new DirectTestStores()) {
				DirectTestPki.Issued issuedSigning=IssueSigning(directTestPki,RECEIVER,20);
				DirectTestPki.Issued issuedEncryptionOld=IssueEncryption(directTestPki,RECEIVER,10);
				DirectTestPki.Issued issuedEncryption=IssueEncryption(directTestPki,RECEIVER,30);
				foreach(DirectTestPki.Issued issued in new[] { issuedEncryptionOld,issuedSigning,issuedEncryption }) {
					directTestStores.AddPrivate(issued.X509Certificate2WithKey);
					directTestStores.AddExternal(issued.X509Certificate2WithKey);
				}
				MailAddress mailAddress=new MailAddress(RECEIVER);
				CollectionAssert.AreEquivalent(new List<string> { issuedSigning.Thumbprint,issuedEncryptionOld.Thumbprint,issuedEncryption.Thumbprint },
					GetThumbprints(new EmailPrivateResolver().GetCertificates(mailAddress)),"Any: every valid one, as before.");
				CollectionAssert.AreEquivalent(new List<string> { issuedSigning.Thumbprint,issuedEncryptionOld.Thumbprint,issuedEncryption.Thumbprint },
					GetThumbprints(new EmailPublicResolver().GetCertificates(mailAddress)));
				CollectionAssert.AreEqual(new List<string> { issuedSigning.Thumbprint },
					GetThumbprints(new EmailPrivateResolver(emailCertificateUse:EmailCertificateUse.Signing).GetCertificates(mailAddress)));
				CollectionAssert.AreEqual(new List<string> { issuedEncryption.Thumbprint },
					GetThumbprints(new EmailPublicResolver(emailCertificateUse:EmailCertificateUse.Encrypting).GetCertificates(mailAddress)));
				List<string> listDecrypting=GetThumbprints(new EmailPrivateResolver(emailCertificateUse:EmailCertificateUse.Decrypting).GetCertificates(mailAddress));
				Assert.AreEqual(issuedEncryption.Thumbprint,listDecrypting[0],"The one for encryption valid longest first.");
				CollectionAssert.AreEquivalent(new List<string> { issuedSigning.Thumbprint,issuedEncryptionOld.Thumbprint },listDecrypting.Skip(1).ToList(),
					"Then the others: the agent tries each in turn.");
				//The other lookups give every certificate, whatever the use.
				Assert.AreEqual(3,new EmailPrivateResolver(emailCertificateUse:EmailCertificateUse.Signing).GetCertificatesForAddress(RECEIVER).Count);
				//Only one certificate of either purpose: that one for every use, as before.
				DirectTestPki.Issued issuedSenderSigning=IssueSigning(directTestPki,SENDER,20);
				directTestStores.AddPrivate(issuedSenderSigning.X509Certificate2WithKey);
				directTestStores.AddExternal(issuedSenderSigning.X509Certificate2WithKey);
				MailAddress mailAddressSender=new MailAddress(SENDER);
				foreach(EmailCertificateUse emailCertificateUse in new[] { EmailCertificateUse.Any,EmailCertificateUse.Signing,EmailCertificateUse.Decrypting }) {
					CollectionAssert.AreEqual(new List<string> { issuedSenderSigning.Thumbprint },
						GetThumbprints(new EmailPrivateResolver(emailCertificateUse:emailCertificateUse).GetCertificates(mailAddressSender)),emailCertificateUse.ToString());
				}
				CollectionAssert.AreEqual(new List<string> { issuedSenderSigning.Thumbprint },
					GetThumbprints(new EmailPublicResolver(emailCertificateUse:EmailCertificateUse.Encrypting).GetCertificates(mailAddressSender)),
					"Encrypted to the signing certificate when it is the only one, as before.");
				Assert.IsNull(new EmailPrivateResolver(emailCertificateUse:EmailCertificateUse.Signing).GetCertificates(new MailAddress("someone@other.example.test")));
			}
		}

		///<summary>Both offices have a signing-only and an encryption-only certificate (the store holds both offices', as two offices' stores would),
		///each office's encryption certificate valid longer than its signing one. The program's Direct send signs with the sender's signing
		///certificate only and encrypts to the receiver's encryption certificate only; the program's receive decrypts it and finds the sender's
		///signing certificate as the one signature.</summary>
		[TestMethod]
		public void DirectSend_SignsWithTheSigningCertificateAndEncryptsToTheEncryptionOne() {
			using(DirectTestPki directTestPki=new DirectTestPki())
			using(DirectTestStores directTestStores=new DirectTestStores()) {
				DirectTestPki.Issued issuedSenderSigning=IssueSigning(directTestPki,SENDER,20);
				DirectTestPki.Issued issuedSenderEncryption=IssueEncryption(directTestPki,SENDER,40);
				DirectTestPki.Issued issuedReceiverSigning=IssueSigning(directTestPki,RECEIVER,40);
				DirectTestPki.Issued issuedReceiverEncryption=IssueEncryption(directTestPki,RECEIVER,30);
				SetUpStores(directTestPki,directTestStores,issuedSenderEncryption,issuedSenderSigning,issuedReceiverSigning,issuedReceiverEncryption);
				Assert.AreEqual(2,new EmailPrivateResolver().GetCertificates(new MailAddress(SENDER)).Count,"Both of the sender's are valid: before, the agent signed with both.");
				string raw=Send("Referral for Pat, signed and encrypted by purpose.");
				EnvelopedCms envelopedCms=ReadEnvelope(raw);
				Assert.AreEqual(1,envelopedCms.RecipientInfos.Count,"Encrypted to one certificate of the receiver.");
				AssertRecipient(envelopedCms.RecipientInfos[0],issuedReceiverEncryption);
				IncomingMessageResult incomingMessageResult=Receive(raw);
				Assert.AreEqual(1,incomingMessageResult.ListSignerThumbprints.Count,"Signed with one certificate.");
				Assert.AreEqual(issuedSenderSigning.Thumbprint,incomingMessageResult.ListSignerThumbprints[0],"The sender's signing certificate.");
				StringAssert.Contains(incomingMessageResult.Text,"Referral for Pat, signed and encrypted by purpose.");
			}
		}

		///<summary>With only one certificate on either side, of either purpose, the send and the receive are as before: the sender's encryption
		///certificate signs and the receiver's signing certificate is encrypted to; the receiver decrypts with it.</summary>
		[TestMethod]
		public void DirectSend_WithOneCertificateOfEitherPurpose_IsAsBefore() {
			using(DirectTestPki directTestPki=new DirectTestPki())
			using(DirectTestStores directTestStores=new DirectTestStores()) {
				DirectTestPki.Issued issuedSenderEncryption=IssueEncryption(directTestPki,SENDER,30);
				DirectTestPki.Issued issuedReceiverSigning=IssueSigning(directTestPki,RECEIVER,30);
				SetUpStores(directTestPki,directTestStores,issuedSenderEncryption,issuedReceiverSigning);
				string raw=Send("One certificate each.");
				EnvelopedCms envelopedCms=ReadEnvelope(raw);
				Assert.AreEqual(1,envelopedCms.RecipientInfos.Count);
				AssertRecipient(envelopedCms.RecipientInfos[0],issuedReceiverSigning);
				IncomingMessageResult incomingMessageResult=Receive(raw);
				CollectionAssert.AreEqual(new List<string> { issuedSenderEncryption.Thumbprint },incomingMessageResult.ListSignerThumbprints);
				StringAssert.Contains(incomingMessageResult.Text,"One certificate each.");
			}
		}

		#region Helpers
		private static DirectTestPki.Issued IssueSigning(DirectTestPki directTestPki,string address,int daysValid) {
			return directTestPki.Issue(address,keyUsageFlags:X509KeyUsageFlags.DigitalSignature,dateTimeOffsetNotAfter:DateTimeOffset.UtcNow.AddDays(daysValid));
		}

		private static DirectTestPki.Issued IssueEncryption(DirectTestPki directTestPki,string address,int daysValid) {
			return directTestPki.Issue(address,keyUsageFlags:X509KeyUsageFlags.KeyEncipherment,dateTimeOffsetNotAfter:DateTimeOffset.UtcNow.AddDays(daysValid));
		}

		private static List<string> GetThumbprints(X509Certificate2Collection x509Certificate2Collection) {
			return x509Certificate2Collection.Cast<X509Certificate2>().Select(x => x.Thumbprint).ToList();
		}

		///<summary>Each certificate with its key in the private store (both offices' own) and public in the external store (each office knows the
		///other's, as the hosted search would have stored them); the test CA is the trust anchor of both.</summary>
		private static void SetUpStores(DirectTestPki directTestPki,DirectTestStores directTestStores,params DirectTestPki.Issued[] arrayIssued) {
			foreach(DirectTestPki.Issued issued in arrayIssued) {
				directTestStores.AddPrivate(issued.X509Certificate2WithKey);
				directTestStores.AddExternal(issued.X509Certificate2WithKey);
			}
			using(X509Certificate2 x509Certificate2Ca=new X509Certificate2(directTestPki.CaRawData)) {
				directTestStores.AddAnchor(x509Certificate2Ca);
			}
		}

		///<summary>The program's Direct send without the mail server: a text message from SENDER to RECEIVER, signed and encrypted by the agent
		///SendEmailDirect makes (GetDirectAgentForEmailAddress with Signing). The raw message as it would go out.</summary>
		private static string Send(string bodyText) {
			Health.Direct.Common.Mail.Message message=new Health.Direct.Common.Mail.Message(RECEIVER,SENDER,bodyText);
			message.ContentType="text/plain";
			message.Headers.Add(new Health.Direct.Common.Mime.Header("Subject","Direct purpose test"));
			message.Date=new Health.Direct.Common.Mime.Header("Date","Mon, 09 Nov 2026 10:00:00 +0000");
			message.AssignMessageID();
			Health.Direct.Agent.OutgoingMessage outgoingMessage=new Health.Direct.Agent.OutgoingMessage(new Health.Direct.Agent.MessageEnvelope(message));
			Health.Direct.Agent.DirectAgent directAgent=EmailMessages.GetDirectAgentForEmailAddress(EmailCertificateUse.Signing,SENDER,RECEIVER);
			return directAgent.ProcessOutgoing(outgoingMessage).SerializeMessage();
		}

		///<summary>The enveloped data of the raw encrypted message.</summary>
		private static EnvelopedCms ReadEnvelope(string raw) {
			MimeMessage mimeMessage;
			using(MemoryStream memoryStream=new MemoryStream(Encoding.ASCII.GetBytes(raw))) {
				mimeMessage=MimeMessage.Load(memoryStream);
			}
			Assert.IsInstanceOfType(mimeMessage.Body,typeof(MimeKit.Cryptography.ApplicationPkcs7Mime),mimeMessage.Body.ContentType.MimeType);
			byte[] byteArrayEnveloped;
			using(MemoryStream memoryStream=new MemoryStream()) {
				((MimePart)mimeMessage.Body).Content.DecodeTo(memoryStream);
				byteArrayEnveloped=memoryStream.ToArray();
			}
			EnvelopedCms envelopedCms=new EnvelopedCms();
			envelopedCms.Decode(byteArrayEnveloped);
			return envelopedCms;
		}

		private static void AssertRecipient(RecipientInfo recipientInfo,DirectTestPki.Issued issued) {
			Assert.AreEqual(SubjectIdentifierType.IssuerAndSerialNumber,recipientInfo.RecipientIdentifier.Type);
			System.Security.Cryptography.Xml.X509IssuerSerial x509IssuerSerial=(System.Security.Cryptography.Xml.X509IssuerSerial)recipientInfo.RecipientIdentifier.Value;
			Assert.AreEqual(issued.SerialNumber,x509IssuerSerial.SerialNumber,true,"Encrypted to "+issued.Subject+" ("+issued.Thumbprint+").");
		}

		private class IncomingMessageResult {
			public List<string> ListSignerThumbprints=new List<string>();
			public string Text="";
		}

		///<summary>The program's Direct receive of the raw message: the agent DecryptIncomingMessage makes (GetDirectAgentForEmailAddress with
		///Decrypting) decrypts it and checks the signature and its trust; the signers' thumbprints and the decrypted text.</summary>
		private static IncomingMessageResult Receive(string raw) {
			Health.Direct.Agent.IncomingMessage incomingMessage=new Health.Direct.Agent.IncomingMessage(raw);
			Health.Direct.Agent.DirectAgent directAgent=EmailMessages.GetDirectAgentForEmailAddress(EmailCertificateUse.Decrypting,
				incomingMessage.Message.ToValue.Trim());
			Health.Direct.Agent.IncomingMessage incomingMessageDecrypted=directAgent.ProcessIncoming(incomingMessage);
			IncomingMessageResult incomingMessageResult=new IncomingMessageResult();
			SignedCms signedCms=incomingMessageDecrypted.Signatures;
			for(int i=0;i<signedCms.SignerInfos.Count;i++) {
				incomingMessageResult.ListSignerThumbprints.Add(signedCms.SignerInfos[i].Certificate.Thumbprint);
			}
			incomingMessageResult.Text=incomingMessageDecrypted.SerializeMessage();
			return incomingMessageResult;
		}
		#endregion Helpers
	}
}
