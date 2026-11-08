// Received mail on MimeKit (package S0-3 of the .NET 10 plan), the parts that need no database: the raw text of a received email turns back into
// the bytes it came as (each byte as a Latin-1 character from the Gmail and Microsoft 365 imports, the text in the message's charset from the POP3
// download), the signed message of the corpus verifies with MimeKit, an encrypted one is recognized, and the preview's helpers read MimeKit parts.
// The fields of whole messages are compared with the Direct-based parser in ReceivedMailCorpusTests. Synthetic data only. Licensed under the GNU
// General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MimeKit;
using MimeKit.Cryptography;
using OpenPmsDentalBusiness;

namespace LibraryUpdateTests {
	[TestClass]
	public class ReceivedMailMimeKitTests {
		private static readonly Encoding _encodingLatin1=Encoding.GetEncoding(28591);

		private static string Raw(string name) {
			return ReceivedMailCorpus.GetAll().Single(x => x.Name==name).Raw;
		}

		[TestMethod]
		public void RawText_TurnsBackIntoTheBytesItCameAs() {
			string headers="From: a@example.test\r\nTo: b@example.test\r\nSubject: x\r\nContent-Type: text/plain; charset=utf-8\r\n\r\n";
			byte[] arrayUtf8=Encoding.UTF8.GetBytes(headers+"Café €\r\n");
			//Gmail and Microsoft 365: each byte as one Latin-1 character.
			CollectionAssert.AreEqual(arrayUtf8,EmailMessages.GetRawEmailBytes(_encodingLatin1.GetString(arrayUtf8),out Encoding encoding));
			Assert.AreEqual(28591,encoding.CodePage);
			//POP3: the text in the charset of the Content-Type, with a character beyond Latin-1 (the euro sign) and without one.
			CollectionAssert.AreEqual(arrayUtf8,EmailMessages.GetRawEmailBytes(Encoding.UTF8.GetString(arrayUtf8),out encoding));
			Assert.AreEqual(65001,encoding.CodePage);
			byte[] arrayUtf8Latin=Encoding.UTF8.GetBytes(headers+"Résultats prêts\r\n");
			CollectionAssert.AreEqual(arrayUtf8Latin,EmailMessages.GetRawEmailBytes(Encoding.UTF8.GetString(arrayUtf8Latin),out encoding),
				"Text the POP3 download read as UTF-8 with accented letters only.");
			Assert.AreEqual(65001,encoding.CodePage);
			string headers1252=headers.Replace("utf-8","windows-1252");
			byte[] array1252=Encoding.GetEncoding(1252).GetBytes(headers1252+"Preis 100 € – Café\r\n");
			CollectionAssert.AreEqual(array1252,EmailMessages.GetRawEmailBytes(Encoding.GetEncoding(1252).GetString(array1252),out encoding));
			Assert.AreEqual(1252,encoding.CodePage);
			CollectionAssert.AreEqual(array1252,EmailMessages.GetRawEmailBytes(_encodingLatin1.GetString(array1252),out encoding));
			//ASCII stays ASCII.
			CollectionAssert.AreEqual(Encoding.ASCII.GetBytes(headers+"plain\r\n"),EmailMessages.GetRawEmailBytes(headers+"plain\r\n",out encoding));
		}

		[TestMethod]
		public void SignedMessage_VerifiesWithMimeKit_AndShowsItsText() {
			MimeMessage mimeMessage=EmailMessages.RawEmailToMimeMessage(Raw("signed"));
			MultipartSigned multipartSigned=mimeMessage.Body as MultipartSigned;
			Assert.IsNotNull(multipartSigned,"multipart/signed");
			Assert.AreEqual(ReceivedMailCorpus.SIGNED_PART.Replace("\r\n","\n"),multipartSigned[0].ToString().Replace("\r\n","\n"),
				"The signed part is read byte for byte.");
			using(TemporarySecureMimeContext temporarySecureMimeContext=new TemporarySecureMimeContext()) {
				DigitalSignatureCollection digitalSignatureCollection=multipartSigned.Verify(temporarySecureMimeContext);
				Assert.AreEqual(1,digitalSignatureCollection.Count);
				//The self-signed certificate is not trusted, so only the signature is checked.
				Assert.IsTrue(digitalSignatureCollection[0].Verify(true),"The signature matches the signed part.");
				Assert.AreEqual("signer@example.test",digitalSignatureCollection[0].SignerCertificate.Email);
			}
			List<List<EmailMimePart>> listListParts=EmailMessages.GetMimePartsForMimeTypes(Raw("signed"),null,"text/plain","application/pkcs7-signature");
			Assert.AreEqual(1,listListParts[0].Count);
			Assert.AreEqual("This message is signed.\r\nSecond line of the signed text.\r\n\r\n",EmailMessages.ProcessMimeTextPart(listListParts[0][0]));
			Assert.AreEqual("smime.p7s",EmailMessages.GetMimeImageFileName(listListParts[1][0]),"The signature stays an attachment named smime.p7s.");
		}

		[TestMethod]
		public void EncryptedMessage_HasNoPartsToShow() {
			MimeMessage mimeMessage=EmailMessages.RawEmailToMimeMessage(Raw("encrypted"));
			Assert.IsInstanceOfType(mimeMessage.Body,typeof(ApplicationPkcs7Mime));
			List<List<EmailMimePart>> listListParts=EmailMessages.GetMimePartsForMimeTypes(Raw("encrypted"),null,"text/html","text/plain","image/",
				"application/octet-stream");
			Assert.IsTrue(listListParts.All(x => x.Count==0));
		}

		[TestMethod]
		public void InlineImages_KeepTheirContentIds() {
			List<List<EmailMimePart>> listListParts=EmailMessages.GetMimePartsForMimeTypes(Raw("html with inline images"),null,"image/");
			CollectionAssert.AreEqual(new[] { "logo-1@example.test","dot-1@example.test" },listListParts[0].Select(x => EmailMessages.GetMimeImageContentId(x)).ToArray());
			CollectionAssert.AreEqual(new[] { "logo.png","dot.gif" },listListParts[0].Select(x => EmailMessages.GetMimeImageFileName(x)).ToArray());
			Assert.IsNotNull(EmailMessages.GetMimeImageBytes(listListParts[0][1],null),"Read from the part when there is no saved attachment.");
		}

		[TestMethod]
		public void TextThatIsNotAnEmail_HasNoParts() {
			Assert.IsTrue(EmailMessages.GetMimePartsForMimeTypes("",null,"text/plain").Single().Count==0);
			Assert.IsTrue(EmailMessages.GetMimePartsForMimeTypes(null,null,"text/plain").Single().Count==0);
		}
	}
}
