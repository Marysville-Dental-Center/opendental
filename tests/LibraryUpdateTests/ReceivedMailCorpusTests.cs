// The received-mail corpus (package S0-3 of the .NET 10 plan): every message of ReceivedMailCorpus goes through ProcessRawEmailMessageIn on a
// scratch database, and the e-mail preview's parts (GetMimePartsForMimeTypes, ProcessMimeTextPart, the image helpers) are read from it. The
// fields that come out are compared with Corpus/ReceivedMailCorpus.expected.txt, which was recorded with the Direct Project parser before
// received mail moved to MimeKit (step 0 of S0-3). A field that differs on purpose is listed in _dictExplained with the new value and the reason;
// any other difference fails the test, and so does an explanation that no longer matches. Each run writes what it got to
// %TEMP%\s03_corpus_actual.txt. The database is named s03_*, made from db/init/new-database.sql, and dropped afterwards; inconclusive without
// the local MariaDB. Synthetic data only. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CodeBase;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;

namespace LibraryUpdateTests {
	[TestClass]
	public class ReceivedMailCorpusTests {
		private const string PREFIX="s03_";
		private static readonly string DB_NAME=PREFIX+DateTime.UtcNow.ToString("yyMMddHHmmss",CultureInfo.InvariantCulture)+"_"
			+Process.GetCurrentProcess().Id+"_"+new Random().Next(1000,10000)+"_corpus";
		private const string EXPECTED_FILE=@"tests\LibraryUpdateTests\Corpus\ReceivedMailCorpus.expected.txt";
		private static string _skipReason;
		private static bool _isBuilt;
		///<summary>The A to Z folder of this class (with the A folder FileAtoZ looks for): the attachments of the corpus are written here.</summary>
		private static string _folderAtoZ;
		private string _localAtoZpathOld;
		private bool _isUnitTestOld;

		#region The differences from the Direct-based parser, and why
		private const string R_DEFAULT_TEXT="A message part without a Content-Type is text/plain (RFC 2045 5.2): the preview now gets that text part and shows the same text the stored body showed before.";
		private const string R_BOUNDARIES="MimeKit reads boundaries that start with another boundary, so the boundaries stay as received; the Direct-based parser replaced them with new GUIDs before it could parse the message.";
		private const string R_TRAILING_CRLF="The raw message with its attachment content cleared ends as the received message ended; the Direct serializer added one more CRLF (the database tool used to cut it off again).";
		private const string R_UTF8_BYTES="An 8-bit UTF-8 body of a Gmail or Microsoft 365 import is decoded from its bytes; the Direct-based parser decoded the Latin-1 text of those bytes as UTF-8 a second time (mojibake).";
		private const string R_RAW_HEADER="A raw 8-bit UTF-8 header is decoded from its bytes; the Direct-based parser kept the Latin-1 text of the bytes (mojibake).";
		private const string R_FOLDED="A folded header is unfolded as RFC 5322 2.2.3 says (the line break goes, the space stays); the Direct-based parser dropped the space and joined two words.";
		private const string R_1252="windows-1252 bytes 0x80 to 0x9F (the euro sign, the en dash) come out right; the Direct-based parser turned them into question marks.";
		private const string R_BASE64_HTML="A base64 html part is only base64-decoded; the Direct-based parser also undid =XX as if the html were quoted-printable, which turned width=100 into a control character and the literal text =3D into =.";
		private const string R_QP_ATTACHMENT="A quoted-printable attachment is saved decoded (the 22 bytes of the text); the Direct-based parser saved the encoded text (28 bytes).";
		private const string R_RFC2231="An RFC 2231 file name (filename*=utf-8''...) is read; the Direct-based parser only read a quoted filename=, so the attachment was named \"attach\" and the preview part had no name.";
		private const string R_7BIT_EQUALS="7bit text is not quoted-printable: \"=41\" and a line that ends with \"=\" stay as typed; the Direct-based parser decoded them anyway.";
		private const string R_ENCRYPTED="An S/MIME-encrypted message is stored with a body that says it cannot be shown: only the Direct agent could decrypt it, with the office's Direct certificate, and Direct is removed. The body was the whole raw message before; RawEmailIn still holds it unchanged.";

		///<summary>The fields that differ from the Direct-based parser on purpose (step 2 of S0-3, received mail on MimeKit): "case|field" gives the
		///new value, as the actual file writes it, and why.</summary>
		private static readonly Dictionary<string,Tuple<string,string>> _dictExplained=new Dictionary<string,Tuple<string,string>> {
			{ "unittests DateExtraSpace|Preview parts",Tuple.Create(@"0 html, 1 text, 0 image, 0 octet-stream",R_DEFAULT_TEXT) },
			{ "unittests DateExtraSpace|Preview body",Tuple.Create(@"text This is a message just to say hello.\r\nSo, ""Hello"".\r\n",R_DEFAULT_TEXT) },
			{ "unittests DateSingleDigitHour|Preview parts",Tuple.Create(@"0 html, 1 text, 0 image, 0 octet-stream",R_DEFAULT_TEXT) },
			{ "unittests DateSingleDigitHour|Preview body",Tuple.Create(@"text This is a message just to say hello.\r\nSo, ""Hello"".\r\n",R_DEFAULT_TEXT) },
			{ "unittests DateTimeZoneAbbreviation|Preview parts",Tuple.Create(@"0 html, 1 text, 0 image, 0 octet-stream",R_DEFAULT_TEXT) },
			{ "unittests DateTimeZoneAbbreviation|Preview body",Tuple.Create(@"text This is a message just to say hello.\r\nSo, ""Hello"".\r\n",R_DEFAULT_TEXT) },
			{ "unittests UndisclosedRecipients|Preview parts",Tuple.Create(@"0 html, 1 text, 0 image, 0 octet-stream",R_DEFAULT_TEXT) },
			{ "unittests UndisclosedRecipients|Preview body",Tuple.Create(@"text This is a message just to say hello.\r\nSo, ""Hello"".\r\n",R_DEFAULT_TEXT) },
			{ "unittests UndisclosedRecipients_NoHyphen|Preview parts",Tuple.Create(@"0 html, 1 text, 0 image, 0 octet-stream",R_DEFAULT_TEXT) },
			{ "unittests UndisclosedRecipients_NoHyphen|Preview body",Tuple.Create(@"text This is a message just to say hello.\r\nSo, ""Hello"".\r\n",R_DEFAULT_TEXT) },
			{ "unittests UndisclosedRecipients_ExtraSpace|Preview parts",Tuple.Create(@"0 html, 1 text, 0 image, 0 octet-stream",R_DEFAULT_TEXT) },
			{ "unittests UndisclosedRecipients_ExtraSpace|Preview body",Tuple.Create(@"text This is a message just to say hello.\r\nSo, ""Hello"".\r\n",R_DEFAULT_TEXT) },
			{ "unittests UndisclosedRecipients_NoRecipients|Preview parts",Tuple.Create(@"0 html, 1 text, 0 image, 0 octet-stream",R_DEFAULT_TEXT) },
			{ "unittests UndisclosedRecipients_NoRecipients|Preview body",Tuple.Create(@"text This is a message just to say hello.\r\nSo, ""Hello"".\r\n",R_DEFAULT_TEXT) },
			{ "mimetests no content type|Preview parts",Tuple.Create(@"0 html, 1 text, 0 image, 0 octet-stream",R_DEFAULT_TEXT) },
			{ "mimetests no content type|Preview body",Tuple.Create(@"text just text\r\n\r\n",R_DEFAULT_TEXT) },
			{ "mimetests bcc only|Preview parts",Tuple.Create(@"0 html, 1 text, 0 image, 0 octet-stream",R_DEFAULT_TEXT) },
			{ "mimetests bcc only|Preview body",Tuple.Create(@"text x\r\n\r\n",R_DEFAULT_TEXT) },
			{ "unittests InvalidBoundaries|RawEmailIn",Tuple.Create(@"(changed) From: John Doe <jdoe@machine.example>\r\nTo: Mary Smith <mary@example.net>\r\nSubject: Saying Hello\r\nDate: Thu, 11 Feb 2016 06:58:09 CST\r\nMessage-ID: <1234@local.machine.example>\r\nContent-Type: multipart/mixed; boundary=""B1""\r\n\r\nThis is a multi-part message in MIME format.\r\n--B1\r\nContent-Type: multipart/alternative; boundary=""B12""\r\nMIME-Version: 1.0\r\n\r\n--B12\r\nContent-Type: text/plain; charset=us-ascii\r\nContent-Transfer-Encoding: 7bit\r\n\r\nHello from Argentina!\r\n\r\n--B12\r\nContent-Type: text/plain; charset=us-ascii\r\nContent-Transfer-Encoding: 7bit\r\n\r\nThis is a message just to say hello.\r\nSo, ""Hello"".\r\n\r\n--B12--\r\n--B1\r\nContent-Type: image/gif\r\nContent-Transfer-Encoding: base64\r\nContent-Disposition: attachment; filename=""map_of_Argentina.gif""\r\n\r\n\r\n--B1--\r\n",R_BOUNDARIES) },
			{ "mimetests utf8 body|BodyText",Tuple.Create(@"Caf\u00e9 \u20ac\r\n\r\n",R_UTF8_BYTES) },
			{ "mimetests utf8 body|Preview body",Tuple.Create(@"text Caf\u00e9 \u20ac\r\n\r\n",R_UTF8_BYTES) },
			{ "mimetests raw 8-bit header|Subject",Tuple.Create(@"Caf\u00e9 raw",R_RAW_HEADER) },
			{ "mimetests multipart|Subject",Tuple.Create(@"folded subject line",R_FOLDED) },
			{ "mimetests multipart|RawEmailIn",Tuple.Create(@"(changed) From: a@example.test\r\nTo: b@example.test, c@example.test\r\nCc: d@example.test\r\nSubject: folded\r\n subject line\r\nMIME-Version: 1.0\r\nContent-Type: multipart/mixed; boundary=""XYZ""\r\n\r\npreamble\r\n--XYZ\r\nContent-Type: text/html; charset=iso-8859-1\r\nContent-Transfer-Encoding: quoted-printable\r\n\r\n<p>Caf=E9</p>\r\n--XYZ\r\nContent-Type: application/pdf; name=""x.pdf""\r\nContent-Disposition: attachment; filename=""x.pdf""\r\nContent-Transfer-Encoding: base64\r\n\r\n\r\n--XYZ--\r\nepilogue\r\n",R_TRAILING_CRLF) },
			{ "windows-1252 8bit gmail|BodyText",Tuple.Create(@"Preis: 100 \u20ac \u2013 Caf\u00e9\r\nGr\u00fc\u00dfe\r\n\r\n",R_1252) },
			{ "windows-1252 8bit gmail|Preview body",Tuple.Create(@"text Preis: 100 \u20ac \u2013 Caf\u00e9\r\nGr\u00fc\u00dfe\r\n\r\n",R_1252) },
			{ "html with inline images|RawEmailIn",Tuple.Create(@"(changed) From: Clinic Lab <lab@example.test>\r\nTo: inbox@example.test\r\nSubject: Your x-ray report\r\nDate: Fri, 06 Nov 2026 14:30:00 +0000\r\nMIME-Version: 1.0\r\nContent-Type: multipart/related; boundary=""rel-1""; type=""multipart/alternative""\r\n\r\n--rel-1\r\nContent-Type: multipart/alternative; boundary=""alt-1""\r\n\r\n--alt-1\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Transfer-Encoding: quoted-printable\r\n\r\nReport attached. Caf=C3=A9 =3D good.\r\n--alt-1\r\nContent-Type: text/html; charset=utf-8\r\nContent-Transfer-Encoding: base64\r\n\r\nPGh0bWw+PGJvZHk+PHRhYmxlPjx0cj48dGQgd2lkdGg9MTAwPlJlcG9ydDwvdGQ+PC90cj48L3Rh\r\nYmxlPjxwPkNhZsOpID0zRDwvcD48aW1nIHNyYz0iY2lkOmxvZ28tMUBleGFtcGxlLnRlc3QiPjxp\r\nbWcgc3JjPSJjaWQ6ZG90LTFAZXhhbXBsZS50ZXN0Ij48L2JvZHk+PC9odG1sPg==\r\n--alt-1--\r\n--rel-1\r\nContent-Type: image/png; name=""logo.png""\r\nContent-Transfer-Encoding: base64\r\nContent-ID: <logo-1@example.test>\r\nContent-Disposition: inline; filename=""logo.png""\r\n\r\n\r\n--rel-1\r\nContent-Type: image/gif; name=""dot.gif""\r\nContent-Transfer-Encoding: base64\r\nContent-ID: <dot-1@example.test>\r\n\r\nR0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7\r\n--rel-1--\r\n",R_TRAILING_CRLF) },
			{ "html with inline images|Preview body",Tuple.Create(@"html <html><body><table><tr><td width=100>Report</td></tr></table><p>Caf\u00e9 =3D</p><img src=""cid:logo-1@example.test""><img src=""cid:dot-1@example.test""></body></html>\r\n",R_BASE64_HTML) },
			{ "attachments|Subject",Tuple.Create(@"Statement for Ren\u00e9e and more",R_FOLDED) },
			{ "attachments|RawEmailIn",Tuple.Create(@"(changed) From: Billing <billing@example.test>\r\nTo: ""Smith, Pat"" <pat@example.test>, inbox@example.test\r\nCc: =?utf-8?B?SsO8cmdlbg==?= <juergen@example.test>\r\nSubject: =?utf-8?Q?Statement_for_Ren=C3=A9e?= and\r\n more\r\nDate: Sat, 07 Nov 2026 10:00:00 -0500\r\nMIME-Version: 1.0\r\nContent-Type: multipart/mixed; boundary=""mix-1""\r\n\r\n--mix-1\r\nContent-Type: text/plain; charset=iso-8859-1\r\nContent-Transfer-Encoding: quoted-printable\r\n\r\nYour statement is attached. =C9t=E9 soft=\r\n break joined.\r\n--mix-1\r\nContent-Type: application/pdf; name=""Statement 2026.pdf""\r\nContent-Transfer-Encoding: base64\r\nContent-Disposition: attachment; filename=""Statement 2026.pdf""\r\n\r\n\r\n--mix-1\r\nContent-Type: text/plain; charset=utf-8; name=""notes.txt""\r\nContent-Transfer-Encoding: quoted-printable\r\nContent-Disposition: attachment; filename=""notes.txt""\r\n\r\n\r\n--mix-1\r\nContent-Type: application/octet-stream\r\nContent-Transfer-Encoding: base64\r\nContent-Disposition: attachment; filename*=utf-8''R%C3%A9sum%C3%A9.bin\r\n\r\n\r\n--mix-1--\r\n",R_TRAILING_CRLF) },
			{ "attachments|Attachment 1",Tuple.Create(@"notes.txt 22 bytes sha f09d21f89a0ee665",R_QP_ATTACHMENT) },
			{ "attachments|Attachment 2",Tuple.Create(@"R\u00e9sum\u00e9.bin 6 bytes sha 17e88db187afd62c",R_RFC2231) },
			{ "attachments|Preview octet-stream 0",Tuple.Create(@"cid  name R\u00e9sum\u00e9.bin",R_RFC2231) },
			{ "forwarded message|RawEmailIn",Tuple.Create(@"(changed) From: Front Desk <front@example.test>\r\nTo: inbox@example.test\r\nSubject: Fwd: Lab results\r\nDate: Tue, 09 Sep 2014 23:16:36 GMT\r\nMIME-Version: 1.0\r\nContent-Type: multipart/mixed; boundary=""fwd-1""\r\n\r\n--fwd-1\r\nContent-Type: text/plain; charset=us-ascii\r\n\r\nSee the forwarded message.\r\n--fwd-1\r\nContent-Type: message/rfc822\r\nContent-Disposition: attachment; filename=""results.eml""\r\n\r\n\r\n--fwd-1--\r\n",R_TRAILING_CRLF) },
			{ "equals signs in 7bit text|BodyText",Tuple.Create(@"a=41 stays as typed\r\nthis line ends with =\r\nnext line\r\n\r\n",R_7BIT_EQUALS) },
			{ "equals signs in 7bit text|Preview body",Tuple.Create(@"text a=41 stays as typed\r\nthis line ends with =\r\nnext line\r\n\r\n",R_7BIT_EQUALS) },
			{ "signed|RawEmailIn",Tuple.Create(@"(changed) From: Signer Example <signer@example.test>\r\nTo: inbox@example.test\r\nSubject: Signed message\r\nDate: Tue, 03 Nov 2026 10:00:00 -0800\r\nMessage-ID: <signed-1@example.test>\r\nMIME-Version: 1.0\r\nContent-Type: multipart/signed; protocol=""application/pkcs7-signature""; micalg=sha-256; boundary=""sig-boundary-1""\r\n\r\nThis is a cryptographically signed message in MIME format.\r\n\r\n--sig-boundary-1\r\nContent-Type: text/plain; charset=us-ascii\r\nContent-Transfer-Encoding: 7bit\r\n\r\nThis message is signed.\r\nSecond line of the signed text.\r\n\r\n--sig-boundary-1\r\nContent-Type: application/pkcs7-signature; name=""smime.p7s""\r\nContent-Transfer-Encoding: base64\r\nContent-Disposition: attachment; filename=""smime.p7s""\r\nContent-Description: S/MIME Cryptographic Signature\r\n\r\n\r\n--sig-boundary-1--\r\n",R_TRAILING_CRLF) },
			{ "encrypted|BodyText",Tuple.Create(@"This message is protected with S/MIME (application/pkcs7-mime) and cannot be shown in this program. The original message is kept unchanged: click Raw Message to see it.",R_ENCRYPTED) },
		};
		#endregion

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
				Security.CurUser=new Userod { UserNum=1,UserName="s03" };
				ForkMigrationRunner.Run();
				Cache.Refresh(InvalidType.AllLocal);
				_folderAtoZ=Path.Combine(Path.GetTempPath(),"s03_corpus_atoz_"+Guid.NewGuid().ToString("N"));
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
			Security.CurUser=new Userod { UserNum=1,UserName="s03" };
			_localAtoZpathOld=OpenPmsDentalBusiness.FileIO.FileAtoZ.LocalAtoZpath;
			OpenPmsDentalBusiness.FileIO.FileAtoZ.LocalAtoZpath=_folderAtoZ;
		}

		[TestCleanup]
		public void TestCleanup() {
			if(_skipReason!=null) {
				return;
			}
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

		///<summary>Text as one line of ASCII: backslash, CR, LF, tab and every character outside printable ASCII as an escape.</summary>
		public static string Escape(string text) {
			if(text==null) {
				return "(null)";
			}
			StringBuilder stringBuilder=new StringBuilder();
			foreach(char c in text) {
				if(c=='\\') {
					stringBuilder.Append("\\\\");
				}
				else if(c=='\r') {
					stringBuilder.Append("\\r");
				}
				else if(c=='\n') {
					stringBuilder.Append("\\n");
				}
				else if(c=='\t') {
					stringBuilder.Append("\\t");
				}
				else if(c<32 || c>126) {
					stringBuilder.Append("\\u"+((int)c).ToString("x4"));
				}
				else {
					stringBuilder.Append(c);
				}
			}
			return stringBuilder.ToString();
		}

		private static string Sha(byte[] arrayBytes) {
			using(SHA256 sha256=SHA256.Create()) {
				return BitConverter.ToString(sha256.ComputeHash(arrayBytes)).Replace("-","").Substring(0,16).ToLowerInvariant();
			}
		}

		private static string DescribeDate(DateTime dateTime) {
			if(Math.Abs((dateTime-DateTime.Now).TotalMinutes)<10) {
				return "(now)";
			}
			if(dateTime.Kind==DateTimeKind.Local) {
				return "utc "+dateTime.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture);
			}
			return dateTime.Kind.ToString().ToLowerInvariant()+" "+dateTime.ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture);
		}

		///<summary>The fields of one message as "field: value" lines, in a fixed order.</summary>
		private static List<KeyValuePair<string,string>> Describe(CorpusMessage corpusMessage) {
			List<KeyValuePair<string,string>> listFields=new List<KeyValuePair<string,string>>();
			void add(string field,string value) {
				listFields.Add(new KeyValuePair<string,string>(field,value));
			}
			EmailAddress emailAddressInbox=new EmailAddress() { EmailUsername=corpusMessage.Inbox };
			EmailMessage emailMessage=null;
			try {
				emailMessage=EmailMessages.ProcessRawEmailMessageIn(corpusMessage.Raw,0,emailAddressInbox,false);
			}
			catch(Exception ex) {
				add("Error",Escape(ex.GetType().Name+": "+ex.Message));
			}
			if(emailMessage!=null) {
				add("FromAddress",Escape(emailMessage.FromAddress));
				add("ToAddress",Escape(emailMessage.ToAddress));
				add("CcAddress",Escape(emailMessage.CcAddress));
				add("BccAddress",Escape(emailMessage.BccAddress));
				add("Subject",Escape(emailMessage.Subject));
				add("MsgDateTime",DescribeDate(emailMessage.MsgDateTime));
				add("BodyText",Escape(emailMessage.BodyText));
				add("SentOrReceived",emailMessage.SentOrReceived.ToString());
				add("RecipientAddress",Escape(emailMessage.RecipientAddress));
				if(emailMessage.RawEmailIn==corpusMessage.Raw) {
					add("RawEmailIn","(as received)");
				}
				else {
					//The Direct-based parser replaced boundaries that start with another boundary by new GUIDs (32 hex digits), different each run.
					add("RawEmailIn","(changed) "+Escape(System.Text.RegularExpressions.Regex.Replace(emailMessage.RawEmailIn,"[0-9a-f]{32}","(guid)")));
				}
				add("Attachments",emailMessage.Attachments.Count.ToString());
				for(int i=0;i<emailMessage.Attachments.Count;i++) {
					EmailAttach emailAttach=emailMessage.Attachments[i];
					byte[] arrayBytes=File.ReadAllBytes(Path.Combine(EmailAttaches.GetAttachPath(),emailAttach.ActualFileName));
					add("Attachment "+i,Escape(emailAttach.DisplayedFileName)+" "+arrayBytes.Length+" bytes sha "+Sha(arrayBytes));
				}
			}
			//The e-mail preview (EmailPreviewControl.ParseMIME and Show Images).
			var listListParts=EmailMessages.GetMimePartsForMimeTypes(corpusMessage.Raw,emailAddressInbox,
				"text/html","text/plain","image/","application/octet-stream");
			add("Preview parts",listListParts[0].Count+" html, "+listListParts[1].Count+" text, "+listListParts[2].Count+" image, "
				+listListParts[3].Count+" octet-stream");
			if(listListParts[0].Count>0) {
				add("Preview body","html "+Escape(EmailMessages.ProcessMimeTextPart(listListParts[0][0])));
			}
			else if(listListParts[1].Count>0) {
				add("Preview body","text "+Escape(EmailMessages.ProcessMimeTextPart(listListParts[1][0])));
			}
			else {
				add("Preview body","(the stored body text)");
			}
			for(int i=2;i<4;i++) {
				for(int j=0;j<listListParts[i].Count;j++) {
					add("Preview "+(i==2 ? "image " : "octet-stream ")+j,"cid "+Escape(EmailMessages.GetMimeImageContentId(listListParts[i][j]))
						+" name "+Escape(EmailMessages.GetMimeImageFileName(listListParts[i][j])));
				}
			}
			return listFields;
		}

		///<summary>Reads the recorded file: case name to its "field: value" lines.</summary>
		private static Dictionary<string,List<KeyValuePair<string,string>>> ReadRecording(string text) {
			Dictionary<string,List<KeyValuePair<string,string>>> dictCases=new Dictionary<string,List<KeyValuePair<string,string>>>();
			List<KeyValuePair<string,string>> listFields=null;
			foreach(string line in text.Replace("\r\n","\n").Split('\n')) {
				if(line.StartsWith("=== ")) {
					listFields=new List<KeyValuePair<string,string>>();
					dictCases[line.Substring(4)]=listFields;
					continue;
				}
				int index=line.IndexOf(": ",StringComparison.Ordinal);
				if(listFields==null || index<0) {
					continue;
				}
				listFields.Add(new KeyValuePair<string,string>(line.Substring(0,index),line.Substring(index+2)));
			}
			return dictCases;
		}

		[TestMethod]
		public void Corpus_MatchesTheDirectRecordingOrAnExplainedDifference() {
			List<CorpusMessage> listMessages=ReceivedMailCorpus.GetAll();
			StringBuilder stringBuilderActual=new StringBuilder();
			Dictionary<string,List<KeyValuePair<string,string>>> dictActual=new Dictionary<string,List<KeyValuePair<string,string>>>();
			foreach(CorpusMessage corpusMessage in listMessages) {
				List<KeyValuePair<string,string>> listFields=Describe(corpusMessage);
				dictActual[corpusMessage.Name]=listFields;
				stringBuilderActual.Append("=== "+corpusMessage.Name+"\r\n");
				foreach(KeyValuePair<string,string> pair in listFields) {
					stringBuilderActual.Append(pair.Key+": "+pair.Value+"\r\n");
				}
			}
			string pathActual=Path.Combine(Path.GetTempPath(),"s03_corpus_actual.txt");
			File.WriteAllText(pathActual,stringBuilderActual.ToString(),Encoding.ASCII);
			string pathExpected;
			try {
				pathExpected=ScratchDb.FindRepoFile(EXPECTED_FILE);
			}
			catch(FileNotFoundException) {
				Assert.Inconclusive("Nothing recorded yet. This run wrote "+pathActual);
				return;
			}
			Dictionary<string,List<KeyValuePair<string,string>>> dictExpected=ReadRecording(File.ReadAllText(pathExpected,Encoding.ASCII));
			List<string> listProblems=new List<string>();
			HashSet<string> hashSetExplainedUsed=new HashSet<string>();
			foreach(CorpusMessage corpusMessage in listMessages) {
				if(!dictExpected.TryGetValue(corpusMessage.Name,out List<KeyValuePair<string,string>> listExpected)) {
					listProblems.Add(corpusMessage.Name+": not recorded");
					continue;
				}
				List<KeyValuePair<string,string>> listActual=dictActual[corpusMessage.Name];
				List<string> listFieldNames=listExpected.Select(x => x.Key).Union(listActual.Select(x => x.Key)).ToList();
				foreach(string field in listFieldNames) {
					string valueExpected=listExpected.Where(x => x.Key==field).Select(x => x.Value).FirstOrDefault()??"(none)";
					string valueActual=listActual.Where(x => x.Key==field).Select(x => x.Value).FirstOrDefault()??"(none)";
					string key=corpusMessage.Name+"|"+field;
					if(_dictExplained.TryGetValue(key,out Tuple<string,string> tupleExplained)) {
						hashSetExplainedUsed.Add(key);
						if(valueActual!=tupleExplained.Item1) {
							listProblems.Add(key+": explained as \""+tupleExplained.Item1+"\" but is \""+valueActual+"\"");
						}
						continue;
					}
					if(valueActual!=valueExpected) {
						listProblems.Add(key+": recorded \""+valueExpected+"\", now \""+valueActual+"\"");
					}
				}
			}
			foreach(string key in _dictExplained.Keys.Where(x => !hashSetExplainedUsed.Contains(x))) {
				listProblems.Add(key+": explained, but not a field of the corpus");
			}
			Assert.AreEqual(0,listProblems.Count,"Differences from the recording (all results are in "+pathActual+"):\r\n"+string.Join("\r\n",listProblems));
		}
	}
}
