// The raw received messages of the e-mail corpus (package S0-3 of the .NET 10 plan, step 0): the 9 ProcessRawEmailMessageIn cases moved out of
// the legacy UnitTests project (EmailMessagesTests), the downloaded messages of MimeTests as the Gmail and Microsoft 365 imports hand them over
// (each byte as one Latin-1 character), the same text as the POP3 download reads it, a signed and an S/MIME-encrypted message, windows-1252
// messages, a sender with consecutive dots, html with inline images, attachments and a forwarded message. ReceivedMailCorpusTests parses every
// one and compares the result with what the Direct Project parser gave (Corpus/ReceivedMailCorpus.expected.txt, recorded before the parser was
// replaced). The signed and the encrypted message were made once with a throw-away self-signed certificate (SignedCms and EnvelopedCms of the
// .NET Framework, SHA-256); its private key was never saved. Synthetic addresses and text only, ASCII source (\u escapes). Licensed under the
// GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Text;

namespace LibraryUpdateTests {
	///<summary>One raw message of the corpus: its name, the inbox that receives it and the raw text that ProcessRawEmailMessageIn gets.</summary>
	public class CorpusMessage {
		public string Name;
		public string Inbox;
		public string Raw;
	}

	public static class ReceivedMailCorpus {
		///<summary>The inbox of the 9 cases that came from UnitTests (they used this address as the receiving account).</summary>
		public const string INBOX_UNIT_TESTS="jdoe@machine.example";
		public const string INBOX="inbox@example.test";

		///<summary>Lines joined with CRLF, as on the wire.</summary>
		private static string L(params string[] arrayLines) {
			return string.Join("\r\n",arrayLines);
		}

		///<summary>What the Gmail and Microsoft 365 imports hand over: the bytes of the message as one Latin-1 character each.</summary>
		private static string Latin1OfUtf8(string text) {
			return Encoding.GetEncoding(28591).GetString(Encoding.UTF8.GetBytes(text));
		}

		///<summary>What the Gmail and Microsoft 365 imports hand over for a message whose bytes are windows-1252.</summary>
		private static string Latin1Of1252(string text) {
			return Encoding.GetEncoding(28591).GetString(Encoding.GetEncoding(1252).GetBytes(text));
		}

		private static string Base64Lines(byte[] arrayBytes) {
			string base64=Convert.ToBase64String(arrayBytes);
			StringBuilder stringBuilder=new StringBuilder();
			for(int i=0;i<base64.Length;i+=76) {
				if(i>0) {
					stringBuilder.Append("\r\n");
				}
				stringBuilder.Append(base64.Substring(i,Math.Min(76,base64.Length-i)));
			}
			return stringBuilder.ToString();
		}

		private const string GIF_ARGENTINA="R01GOD1hJQA1AKIAAP/////78P/omn19fQAAAAAAAAAAAAAAACwAAAAAJQA1AAAD7Qi63P5w\r\n"
			+"wEmjBCLrnQnhYCgM1wh+pkgqqeC9XrutmBm7hAK3tP31gFcAiFKVQrGFR6kscnonTe7FAAad\r\n"
			+"GugmRu3CmiBt57fsVq3Y0VFKnpYdxPC6M7Ze4crnnHum4oN6LFJ1bn5NXTN7OF5fQkN5WYow\r\n"
			+"BEN2dkGQGWJtSzqGTICJgnQuTJN/WJsojad9qXMuhIWdjXKjY4tenjo6tjVssk2gaWq3uGNX\r\n"
			+"U6ZGxseyk8SasGw3J9GRzdTQky1iHNvcPNNI4TLeKdfMvy0vMqLrItvuxfDW8ubjueDtJufz\r\n"
			+"7itICBxISKDBgwgTKjyYAAA7";

		private const string SIGNATURE="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\r\n"
			+"0gp/DCFAHmfQ2H0=";

		private const string ENVELOPED="MIICDQYJKoZIhvcNAQcDoIIB/jCCAfoCAQAxggFlMIIBYQIBADBJMD0xIjAgBgkqhkiG9w0BCQEW\r\n"
			+"E3NpZ25lckBleGFtcGxlLnRlc3QxFzAVBgNVBAMTDlNpZ25lciBFeGFtcGxlAggPVGQKHRwv2zAN\r\n"
			+"BgkqhkiG9w0BAQEFAASCAQAHSaTs1MPhKY/hGB8gBsQLcWB8G+E8H6+7bbUarAYdQqWKzA1vKV7B\r\n"
			+"d5Xnw5aTAuNPoxk2QohErrEnOgI5Xgwjr3uPgBSPPx6sLBXWL2u6WChMpwalT3JE+C4xCJaAPw6d\r\n"
			+"/lYTqYSbcCgRFTeLmD1dsw5yBHcz3tNi8DBGAhmyRYLpmxH7h1CmEk/2UZtuIhC+gy0j0QRuQiXO\r\n"
			+"qRLbVRo/VdtYEiVubUOEGmMn5vGdlByFAhZra+IWOen5iGTsDf6K6pHp0ekSjLTasRJ6C2P70y2V\r\n"
			+"GiAuW8FE5U5YswrlOtoOOGB/bi1SHZeAuk+niwmkJYgmwH0xtmJUZfda4g80MIGLBgkqhkiG9w0B\r\n"
			+"BwEwFAYIKoZIhvcNAwcECKCDnjD5ZuiJgGhEJ9wpsIu/KTlxUh5HJlenBYj4+ySi3sF54xB43v+F\r\n"
			+"UNPFSLdaApIxB5any+NGVixkdVGqTzSPQXMh6b/353ldLYi4CD8NiGORVsbRVnFK2DYWdxXEp91z\r\n"
			+"qEadOM7biGoZxw+o7RnBng==";

		///<summary>The signed text of the signed message (the first part, headers included), which the signature covers byte for byte.</summary>
		public const string SIGNED_PART="Content-Type: text/plain; charset=us-ascii\r\nContent-Transfer-Encoding: 7bit\r\n\r\n"
			+"This message is signed.\r\nSecond line of the signed text.\r\n";

		private static CorpusMessage M(string name,string inbox,string raw) {
			return new CorpusMessage() { Name=name,Inbox=inbox,Raw=raw };
		}

		///<summary>Every message of the corpus, in a fixed order.</summary>
		public static List<CorpusMessage> GetAll() {
			List<CorpusMessage> listMessages=new List<CorpusMessage>();
			string hello=L("","This is a message just to say hello.","So, \"Hello\".");
			#region The 9 cases from UnitTests
			listMessages.Add(M("unittests DateExtraSpace",INBOX_UNIT_TESTS,L("From: John Doe <jdoe@machine.example>","To: Mary Smith <mary@example.net>",
				"Subject: Saying Hello","Date: Sat,  1 Nov 1997 09:55:06 -0600","Message-ID: <1234@local.machine.example>")+"\r\n"+hello));
			listMessages.Add(M("unittests DateSingleDigitHour",INBOX_UNIT_TESTS,L("From: John Doe <jdoe@machine.example>","To: Mary Smith <mary@example.net>",
				"Subject: Saying Hello","Date: Sat, 1 Nov 1997 9:55:06 -0600","Message-ID: <1234@local.machine.example>")+"\r\n"+hello));
			listMessages.Add(M("unittests DateTimeZoneAbbreviation",INBOX_UNIT_TESTS,L("From: John Doe <jdoe@machine.example>",
				"To: Mary Smith <mary@example.net>","Subject: Saying Hello","Date: Thu, 11 Feb 2016 06:58:09 CST","Message-ID: <1234@local.machine.example>")
				+"\r\n"+hello));
			string multipartArgentina(string boundaryInner) {
				return L("From: John Doe <jdoe@machine.example>","To: Mary Smith <mary@example.net>","Subject: Saying Hello",
					"Date: Thu, 11 Feb 2016 06:58:09 CST","Message-ID: <1234@local.machine.example>","Content-Type: multipart/mixed; boundary=\"B1\"","",
					"This is a multi-part message in MIME format.","--B1","Content-Type: multipart/alternative; boundary=\""+boundaryInner+"\"","MIME-Version: 1.0","",
					"--"+boundaryInner,"Content-Type: text/plain; charset=us-ascii","Content-Transfer-Encoding: 7bit","","Hello from Argentina!","",
					"--"+boundaryInner,"Content-Type: text/plain; charset=us-ascii","Content-Transfer-Encoding: 7bit","","This is a message just to say hello.",
					"So, \"Hello\".","","--"+boundaryInner+"--","--B1","Content-Type: image/gif","Content-Transfer-Encoding: base64",
					"Content-Disposition: attachment; filename=\"map_of_Argentina.gif\"","",GIF_ARGENTINA,"--B1--");
			}
			listMessages.Add(M("unittests MimePartAttachmentNoName",INBOX_UNIT_TESTS,multipartArgentina("B2")));
			listMessages.Add(M("unittests InvalidBoundaries",INBOX_UNIT_TESTS,multipartArgentina("B12")));
			foreach(KeyValuePair<string,string> pair in new Dictionary<string,string> { { "UndisclosedRecipients","undisclosed-recipients:;" },
				{ "UndisclosedRecipients_NoHyphen","undisclosed recipients:;" },{ "UndisclosedRecipients_ExtraSpace","undisclosed-recipients: ;" } })
			{
				listMessages.Add(M("unittests "+pair.Key,INBOX_UNIT_TESTS,L("From: John Doe <jdoe@machine.example>","To: "+pair.Value,
					"Bcc: Mary Smith <mary@example.net>","Subject: Saying Hello","Date: Sat,  1 Nov 1997 09:55:06 -0600",
					"Message-ID: <1234@local.machine.example>")+"\r\n"+hello));
			}
			listMessages.Add(M("unittests UndisclosedRecipients_NoRecipients",INBOX_UNIT_TESTS,L("From: John Doe <jdoe@machine.example>",
				"Subject: Saying Hello","Date: Sat,  1 Nov 1997 09:55:06 -0600","Message-ID: <1234@local.machine.example>")+"\r\n"+hello));
			#endregion The 9 cases from UnitTests
			#region The downloaded messages of MimeTests, as the Gmail and Microsoft 365 imports hand them over
			listMessages.Add(M("mimetests plain",INBOX,L("From: Front Desk <front@example.test>","To: patient@example.test","Subject: Appointment",
				"Date: Mon, 02 Nov 2026 08:30:00 -0800","Message-ID: <abc123@example.test>","MIME-Version: 1.0","Content-Type: text/plain; charset=us-ascii",
				"","See you at 9.",".leading dot","")));
			string utf8Body=L("From: a@example.test","To: b@example.test","Subject: =?utf-8?B?Q2Fmw6k=?=","MIME-Version: 1.0",
				"Content-Type: text/plain; charset=utf-8","Content-Transfer-Encoding: 8bit","","Café €","");
			listMessages.Add(M("mimetests utf8 body",INBOX,Latin1OfUtf8(utf8Body)));
			listMessages.Add(M("mimetests raw 8-bit header",INBOX,Latin1OfUtf8(L("From: a@example.test","To: b@example.test","Subject: Café raw",
				"Content-Type: text/plain","","body",""))));
			listMessages.Add(M("mimetests multipart",INBOX,L("From: a@example.test","To: b@example.test, c@example.test","Cc: d@example.test",
				"Subject: folded"," subject line","MIME-Version: 1.0","Content-Type: multipart/mixed; boundary=\"XYZ\"","","preamble","--XYZ",
				"Content-Type: text/html; charset=iso-8859-1","Content-Transfer-Encoding: quoted-printable","","<p>Caf=E9</p>","--XYZ",
				"Content-Type: application/pdf; name=\"x.pdf\"","Content-Disposition: attachment; filename=\"x.pdf\"","Content-Transfer-Encoding: base64",
				"","JVBERi0xLjQKJcOkw7zDtsOfCg==","--XYZ--","epilogue","")));
			listMessages.Add(M("mimetests no content type",INBOX,L("From: a@example.test","To: b@example.test","Subject: none","","just text","")));
			listMessages.Add(M("mimetests bcc only",INBOX,L("From: a@example.test","To: undisclosed-recipients:;","Bcc: b@example.test","Subject: bcc only",
				"","x","")));
			//The same 8-bit UTF-8 message as the POP3 download reads it: the text of the bytes in the charset of the top-level Content-Type.
			listMessages.Add(M("pop3 utf8 body",INBOX,utf8Body));
			#endregion
			#region windows-1252
			string text1252=L("From: =?windows-1252?Q?Ren=E9e_M=FCller?= <renee@example.test>","To: inbox@example.test",
				"Subject: =?windows-1252?Q?Rechnung_=80_100?=","Date: Wed, 04 Nov 2026 09:15:00 +0100","MIME-Version: 1.0",
				"Content-Type: text/plain; charset=windows-1252","Content-Transfer-Encoding: quoted-printable","","Preis: 100 =80 =96 Caf=E9","Gr=FC=DFe","");
			listMessages.Add(M("windows-1252 quoted-printable",INBOX,text1252));
			string text1252EightBit=L("From: =?windows-1252?Q?Ren=E9e_M=FCller?= <renee@example.test>","To: inbox@example.test",
				"Subject: Rechnung 8bit","Date: Wed, 04 Nov 2026 09:20:00 +0100","MIME-Version: 1.0","Content-Type: text/plain; charset=windows-1252",
				"Content-Transfer-Encoding: 8bit","","Preis: 100 € – Café","Grüße","");
			listMessages.Add(M("windows-1252 8bit gmail",INBOX,Latin1Of1252(text1252EightBit)));
			listMessages.Add(M("windows-1252 8bit pop3",INBOX,text1252EightBit));
			#endregion windows-1252
			listMessages.Add(M("consecutive dots",INBOX,L("From: \"Pat Q. Smith\" <pat..q.smith@example.test>","To: inbox@example.test",
				"Cc: front..desk@example.test","Subject: Dots in the address","Date: Thu, 05 Nov 2026 08:00:00 -0700","MIME-Version: 1.0",
				"Content-Type: text/plain; charset=us-ascii","","Hello.","")));
			#region html with inline images
			string html="<html><body><table><tr><td width=100>Report</td></tr></table><p>Café =3D</p><img src=\"cid:logo-1@example.test\">"
				+"<img src=\"cid:dot-1@example.test\"></body></html>";
			listMessages.Add(M("html with inline images",INBOX,L("From: Clinic Lab <lab@example.test>","To: inbox@example.test","Subject: Your x-ray report",
				"Date: Fri, 06 Nov 2026 14:30:00 +0000","MIME-Version: 1.0","Content-Type: multipart/related; boundary=\"rel-1\"; type=\"multipart/alternative\"",
				"","--rel-1","Content-Type: multipart/alternative; boundary=\"alt-1\"","","--alt-1","Content-Type: text/plain; charset=utf-8",
				"Content-Transfer-Encoding: quoted-printable","","Report attached. Caf=C3=A9 =3D good.","--alt-1","Content-Type: text/html; charset=utf-8",
				"Content-Transfer-Encoding: base64","",Base64Lines(Encoding.UTF8.GetBytes(html)),"--alt-1--","--rel-1","Content-Type: image/png; name=\"logo.png\"",
				"Content-Transfer-Encoding: base64","Content-ID: <logo-1@example.test>","Content-Disposition: inline; filename=\"logo.png\"","",
				"iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==","--rel-1",
				"Content-Type: image/gif; name=\"dot.gif\"","Content-Transfer-Encoding: base64","Content-ID: <dot-1@example.test>","",
				"R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7","--rel-1--","")));
			#endregion html with inline images
			listMessages.Add(M("attachments",INBOX,L("From: Billing <billing@example.test>","To: \"Smith, Pat\" <pat@example.test>, inbox@example.test",
				"Cc: =?utf-8?B?SsO8cmdlbg==?= <juergen@example.test>","Subject: =?utf-8?Q?Statement_for_Ren=C3=A9e?= and"," more",
				"Date: Sat, 07 Nov 2026 10:00:00 -0500","MIME-Version: 1.0","Content-Type: multipart/mixed; boundary=\"mix-1\"","","--mix-1",
				"Content-Type: text/plain; charset=iso-8859-1","Content-Transfer-Encoding: quoted-printable","","Your statement is attached. =C9t=E9 soft=",
				" break joined.","--mix-1","Content-Type: application/pdf; name=\"Statement 2026.pdf\"","Content-Transfer-Encoding: base64",
				"Content-Disposition: attachment; filename=\"Statement 2026.pdf\"","","JVBERi0xLjQKJcOkw7zDtsOfCjEgMCBvYmoKPDw+PgplbmRvYmoKdHJhaWxlcgo8PD4+CiUlRU9GCg==",
				"--mix-1","Content-Type: text/plain; charset=utf-8; name=\"notes.txt\"","Content-Transfer-Encoding: quoted-printable",
				"Content-Disposition: attachment; filename=\"notes.txt\"","","Line one =3D one.","Caf=C3=A9","--mix-1","Content-Type: application/octet-stream",
				"Content-Transfer-Encoding: base64","Content-Disposition: attachment; filename*=utf-8''R%C3%A9sum%C3%A9.bin","","AAECAwQF","--mix-1--","")));
			listMessages.Add(M("forwarded message",INBOX,L("From: Front Desk <front@example.test>","To: inbox@example.test","Subject: Fwd: Lab results",
				"Date: Tue, 09 Sep 2014 23:16:36 GMT","MIME-Version: 1.0","Content-Type: multipart/mixed; boundary=\"fwd-1\"","","--fwd-1",
				"Content-Type: text/plain; charset=us-ascii","","See the forwarded message.","--fwd-1","Content-Type: message/rfc822",
				"Content-Disposition: attachment; filename=\"results.eml\"","","From: Lab <lab@example.test>","To: front@example.test","Subject: Lab results","",
				"Results are ready.","--fwd-1--","")));
			listMessages.Add(M("blank cc",INBOX,L("From: Mail Host <welcome@example.test>","To: inbox@example.test","Cc: ","Subject: Welcome to Email",
				"Date: Mon, 02 Nov 2026 07:00:00 -0700","MIME-Version: 1.0","Content-Type: text/html; charset=us-ascii","",
				"<html><body><p>Welcome!</p></body></html>","")));
			listMessages.Add(M("equals signs in 7bit text",INBOX,L("From: a@example.test","To: inbox@example.test","Subject: Equations",
				"Date: Tue, 5 Nov 2013 17:10:37 +0000 (UTC)","MIME-Version: 1.0","Content-Type: text/plain; charset=us-ascii","Content-Transfer-Encoding: 7bit",
				"","a=41 stays as typed","this line ends with =","next line","")));
			listMessages.Add(M("signed",INBOX,L("From: Signer Example <signer@example.test>","To: inbox@example.test","Subject: Signed message",
				"Date: Tue, 03 Nov 2026 10:00:00 -0800","Message-ID: <signed-1@example.test>","MIME-Version: 1.0",
				"Content-Type: multipart/signed; protocol=\"application/pkcs7-signature\"; micalg=sha-256; boundary=\"sig-boundary-1\"","",
				"This is a cryptographically signed message in MIME format.","","--sig-boundary-1")+"\r\n"+SIGNED_PART+L("","--sig-boundary-1",
				"Content-Type: application/pkcs7-signature; name=\"smime.p7s\"","Content-Transfer-Encoding: base64",
				"Content-Disposition: attachment; filename=\"smime.p7s\"","Content-Description: S/MIME Cryptographic Signature","",SIGNATURE,"",
				"--sig-boundary-1--","")));
			listMessages.Add(M("encrypted",INBOX,L("From: Sender Example <sender@example.test>","To: inbox@example.test","Subject: Encrypted message",
				"Date: Tue, 03 Nov 2026 11:00:00 -0800","Message-ID: <encrypted-1@example.test>","MIME-Version: 1.0",
				"Content-Type: application/pkcs7-mime; smime-type=enveloped-data; name=\"smime.p7m\"","Content-Transfer-Encoding: base64",
				"Content-Disposition: attachment; filename=\"smime.p7m\"","",ENVELOPED,"")));
			return listMessages;
		}
	}
}
