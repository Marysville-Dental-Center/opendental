// Received mail on MimeKit (package S0-3 of the .NET 10 plan), the parts that need no database: the raw text of a received email turns back into
// the bytes it came as (each byte as a Latin-1 character from the Gmail and Microsoft 365 imports, the text in the message's charset from the POP3
// download), the signed message of the corpus verifies with MimeKit, an encrypted one is recognized, the preview's helpers read MimeKit parts,
// and the file name of a received attachment loses any path it came with.
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

		#region Attachment names with paths
		///<summary>A received message whose attachments' names carry paths, in the forms MimeKit decodes into a path (checked with MimeKit 4.18):
		///an RFC 2231 name with %5C, a quoted name with '/', an unquoted name with '\', a quoted name with escaped '\\', a Content-Disposition
		///filename with '/' and an unquoted one with '\', a name of "..", a name with characters a file name can't hold, and an inline image.
		///ReceivedMailCorpusTests.AttachmentNamesWithPaths_AreSavedAndShownWithoutThem receives it on the scratch database.</summary>
		public const string RAW_PATH_NAMES="From: Sender <sender@example.test>\r\n"
			+"To: inbox@example.test\r\n"
			+"Subject: Attachment names with paths\r\n"
			+"Date: Sun, 08 Nov 2026 09:00:00 +0000\r\n"
			+"MIME-Version: 1.0\r\n"
			+"Content-Type: multipart/mixed; boundary=\"mix-p\"\r\n"
			+"\r\n"
			+"--mix-p\r\n"
			+"Content-Type: text/plain; charset=us-ascii\r\n"
			+"\r\n"
			+"Attachments whose names carry paths.\r\n"
			+"--mix-p\r\n"
			+"Content-Type: application/octet-stream; name*=us-ascii''..%5C..%5CStartup%5Cevil.bat\r\n"
			+"Content-Transfer-Encoding: base64\r\n"
			+"Content-Disposition: attachment\r\n"
			+"\r\n"
			+"ZWNobyAx\r\n"
			+"--mix-p\r\n"
			+"Content-Type: application/octet-stream; name=\"../../Startup/evil2.bat\"\r\n"
			+"Content-Transfer-Encoding: base64\r\n"
			+"Content-Disposition: attachment\r\n"
			+"\r\n"
			+"ZWNobyAy\r\n"
			+"--mix-p\r\n"
			+@"Content-Type: application/octet-stream; name=..\..\evil3.bat"+"\r\n"
			+"Content-Transfer-Encoding: base64\r\n"
			+"Content-Disposition: attachment\r\n"
			+"\r\n"
			+"ZWNobyAz\r\n"
			+"--mix-p\r\n"
			+@"Content-Type: application/octet-stream; name=""..\\..\\evil5.bat"""+"\r\n"
			+"Content-Transfer-Encoding: base64\r\n"
			+"Content-Disposition: attachment\r\n"
			+"\r\n"
			+"ZWNobyA1\r\n"
			+"--mix-p\r\n"
			+"Content-Type: application/octet-stream\r\n"
			+"Content-Transfer-Encoding: base64\r\n"
			+"Content-Disposition: attachment; filename=\"../../../Roaming/Microsoft/Windows/Start Menu/Programs/Startup/x.bat\"\r\n"
			+"\r\n"
			+"ZWNobyA2\r\n"
			+"--mix-p\r\n"
			+"Content-Type: application/octet-stream\r\n"
			+"Content-Transfer-Encoding: base64\r\n"
			+@"Content-Disposition: attachment; filename=..\..\x4.bat"+"\r\n"
			+"\r\n"
			+"ZWNobyA3\r\n"
			+"--mix-p\r\n"
			+"Content-Type: application/octet-stream; name=\"..\"\r\n"
			+"Content-Transfer-Encoding: base64\r\n"
			+"Content-Disposition: attachment\r\n"
			+"\r\n"
			+"ZWNobyA4\r\n"
			+"--mix-p\r\n"
			+"Content-Type: text/plain; charset=us-ascii; name=\"a|b<c>:d.txt\"\r\n"
			+"Content-Transfer-Encoding: base64\r\n"
			+"Content-Disposition: attachment\r\n"
			+"\r\n"
			+"ZWNobyA5\r\n"
			+"--mix-p\r\n"
			+"Content-Type: image/png; name=\"../../img/logo.png\"\r\n"
			+"Content-Transfer-Encoding: base64\r\n"
			+"Content-ID: <logo-p@example.test>\r\n"
			+"Content-Disposition: inline\r\n"
			+"\r\n"
			+"iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==\r\n"
			+"--mix-p--\r\n";

		///<summary>The names the attachments of RAW_PATH_NAMES are shown and saved with, in order: "attach" for the one whose name is "..".</summary>
		public static readonly string[] ArrayPathNamesCleaned=new string[] { "evil.bat","evil2.bat","evil3.bat","evil5.bat","x.bat","x4.bat","attach",
			"a_b_c__d.txt","logo.png" };

		///<summary>True when the name has a '/' or '\' in it, or is (or has a segment that is) "." or "..".</summary>
		public static bool HasPath(string fileName) {
			return fileName.IndexOfAny(new char[] { '/','\\' })>=0 || fileName.Split('/','\\').Any(x => x=="." || x=="..");
		}

		[TestMethod]
		public void ReceivedFileName_LosesItsPath() {
			Assert.AreEqual("evil.bat",EmailMessages.CleanReceivedFileName(@"..\..\Startup\evil.bat"));
			Assert.AreEqual("evil2.bat",EmailMessages.CleanReceivedFileName("../../Startup/evil2.bat"));
			Assert.AreEqual("x.bat",EmailMessages.CleanReceivedFileName(@"..\..\..\Roaming\Microsoft\Windows\Start Menu\Programs\Startup\x.bat"));
			Assert.AreEqual("x.bat",EmailMessages.CleanReceivedFileName(@"C:\Users\Public\x.bat"),"A drive letter goes with the folders.");
			Assert.AreEqual("x.bat",EmailMessages.CleanReceivedFileName(@"\\files.example.test\share\x.bat"),"So does a share.");
			Assert.AreEqual("a_b_c__d.txt",EmailMessages.CleanReceivedFileName("a|b<c>:d.txt"),"Characters a file name can't hold become '_'.");
			Assert.AreEqual("x_.txt",EmailMessages.CleanReceivedFileName("x\".txt"),"A quote, which Path.GetFileName would throw for.");
			Assert.AreEqual("Statement 2026.pdf",EmailMessages.CleanReceivedFileName("Statement 2026.pdf"),"An ordinary name stays as it is.");
			Assert.AreEqual("R\u00e9sum\u00e9..final.bin",EmailMessages.CleanReceivedFileName("R\u00e9sum\u00e9..final.bin"),"Two dots inside a name are no path.");
			foreach(string fileName in new string[] { null,""," ",".","..","../",@"..\..",@"a\..","/","_"," . " }) {
				Assert.IsNull(EmailMessages.CleanReceivedFileName(fileName),"No name is left of \""+fileName+"\".");
			}
		}

		[TestMethod]
		public void PreviewFileNames_LoseTheirPaths_AsTheSavedAttachmentsDo() {
			List<List<EmailMimePart>> listListParts=EmailMessages.GetMimePartsForMimeTypes(RAW_PATH_NAMES,null,"application/octet-stream","text/plain",
				"image/");
			string[] arrayNames=listListParts[0].Select(x => EmailMessages.GetMimeImageFileName(x))
				.Concat(listListParts[1].Skip(1).Select(x => EmailMessages.GetMimeImageFileName(x)))//The first text part is the body: it has no name.
				.Concat(listListParts[2].Select(x => EmailMessages.GetMimeImageFileName(x))).ToArray();
			Assert.IsNull(EmailMessages.GetMimeImageFileName(listListParts[1][0]));
			//The preview has no name for the ".." part, where the saved attachment is named "attach".
			CollectionAssert.AreEqual(ArrayPathNamesCleaned.Select(x => x=="attach" ? null : x).ToArray(),arrayNames);
			Assert.AreEqual("logo-p@example.test",EmailMessages.GetMimeImageContentId(listListParts[2][0]));
			Assert.IsNotNull(EmailMessages.GetMimeImageBytes(listListParts[2][0],null),"The image is read from its part.");
		}
		#endregion Attachment names with paths
	}
}
