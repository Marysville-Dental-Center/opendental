// POP3 receive with MailKit 4.18 (OpenPop.NET before), against the fake POP3 server on 127.0.0.1, plain and with TLS from the first byte (UseSSL):
// the password sign-in, the UID list against the stored UIDs, the raw message handed on byte for byte (dot-stuffing undone) and read with its
// charset, the inbox's own sent mail left out but remembered, an unreadable message remembered, nothing deleted on the server, and a server
// certificate no authority signed refused unless the test trusts it. The database side is replaced (EmailMessages.DownloadPop3Inbox); the end to
// end run with a database is in Pop3DbTests. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CodeBase;
using MailKit.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace LibraryUpdateTests {
	[TestClass]
	public class Pop3Tests {
		public const string USER="front@example.test";
		public const string PASSWORD="synthetic-pop3-password";

		[TestCleanup]
		public void TestCleanup() {
			EmailMessages.Pop3CertificateValidationForTests=null;
		}

		public static EmailAddress MakeAddress(int port,bool useSsl,string password=PASSWORD) {
			EmailAddress emailAddress=new EmailAddress();
			emailAddress.EmailUsername=" "+USER+" ";//Trimmed for the sign-in, as before.
			emailAddress.EmailPassword=MiscUtils.Encrypt(password);
			emailAddress.Pop3ServerIncoming="127.0.0.1";
			emailAddress.ServerPortIncoming=port;
			emailAddress.UseSSL=useSsl;
			emailAddress.AuthenticationType=OAuthType.None;
			return emailAddress;
		}

		public static byte[] Message(string from,string to,string subject,string body,string contentType="text/plain; charset=us-ascii",
			Encoding encoding=null)
		{
			string text="From: "+from+"\r\nTo: "+to+"\r\nSubject: "+subject+"\r\nDate: Mon, 02 Nov 2026 08:30:00 -0800\r\nMessage-ID: <"
				+Guid.NewGuid().ToString("N")+"@example.test>\r\nMIME-Version: 1.0\r\nContent-Type: "+contentType+"\r\n\r\n"+body;
			return (encoding??Encoding.ASCII).GetBytes(text);
		}

		private class Run {
			public int CountNew;
			public List<string> ListImported=new List<string>();
			public List<string> ListUidsStored=new List<string>();
		}

		private static Run Download(EmailAddress emailAddress,params string[] arrayStoredUids) {
			Run run=new Run();
			run.CountNew=EmailMessages.DownloadPop3Inbox(emailAddress,() => arrayStoredUids.ToList(),x => run.ListImported.Add(x),
				x => run.ListUidsStored.Add(x));
			return run;
		}

		[TestMethod]
		public void PlainPop3_DownloadsOnlyNewMessages_AndDeletesNothing() {
			using(FakePop3Server fakePop3Server=new FakePop3Server(USER,PASSWORD,isTls:false)) {
				byte[] arrayOld=Message("pat@example.test",USER,"Old","Already downloaded.\r\n");
				byte[] arrayNew=Message("Pat Smith <pat@example.test>",USER,"New","Line one\r\n.Line with a leading dot\r\n..two dots\r\nend\r\n");
				fakePop3Server.AddMessage("uid-old-1",arrayOld);
				fakePop3Server.AddMessage("uid-new-2",arrayNew);
				Run run=Download(MakeAddress(fakePop3Server.Port,false),"uid-old-1");
				Assert.IsNull(fakePop3Server.ServerError,fakePop3Server.ServerError?.ToString());
				Assert.AreEqual(1,run.CountNew);
				CollectionAssert.AreEqual(new[] { "uid-new-2" },run.ListUidsStored);
				Assert.AreEqual(Encoding.ASCII.GetString(arrayNew),run.ListImported.Single(),"The raw message, with the dot-stuffing undone.");
				List<string> listCommands=fakePop3Server.GetCommands();
				CollectionAssert.Contains(listCommands,"USER "+USER,"The user name is trimmed.");
				CollectionAssert.Contains(listCommands,"PASS "+PASSWORD);
				CollectionAssert.Contains(listCommands,"UIDL");
				CollectionAssert.Contains(listCommands,"RETR 2");
				CollectionAssert.DoesNotContain(listCommands,"RETR 1","A stored UID is not downloaded again.");
				Assert.IsFalse(listCommands.Any(x => x.StartsWith("DELE",StringComparison.OrdinalIgnoreCase)),"Nothing is deleted from the server.");
				Assert.AreEqual("QUIT",listCommands.Last());
			}
		}

		[TestMethod]
		public void RawMessage_IsReadWithItsCharset() {
			using(FakePop3Server fakePop3Server=new FakePop3Server(USER,PASSWORD,isTls:false)) {
				fakePop3Server.AddMessage("u1",Message("pat@example.test",USER,"utf8","Café €\r\n","text/plain; charset=utf-8",new UTF8Encoding(false)));
				fakePop3Server.AddMessage("u2",Message("pat@example.test",USER,"latin1","Café\r\n","text/plain; charset=iso-8859-1",
					Encoding.GetEncoding(28591)));
				fakePop3Server.AddMessage("u3",Message("pat@example.test",USER,"none","Café\r\n","text/plain",Encoding.GetEncoding(28591)));
				Run run=Download(MakeAddress(fakePop3Server.Port,false));
				Assert.AreEqual(3,run.CountNew);
				StringAssert.EndsWith(run.ListImported[0],"\r\n\r\nCafé €\r\n");
				StringAssert.EndsWith(run.ListImported[1],"\r\n\r\nCafé\r\n");
				StringAssert.EndsWith(run.ListImported[2],"\r\n\r\nCaf?\r\n","No charset: US-ASCII, as OpenPop read it.");
			}
		}

		[TestMethod]
		public void OwnSentMail_IsLeftOut_AndAnUnreadableMessageIsRemembered() {
			using(FakePop3Server fakePop3Server=new FakePop3Server(USER,PASSWORD,isTls:false)) {
				//Gmail lists the inbox's sent mail in POP3: from the inbox to someone else.
				fakePop3Server.AddMessage("sent",Message(USER,"pat@example.test","Sent by us","x\r\n"));
				//Sent to itself: kept.
				fakePop3Server.AddMessage("self",Message(USER,"Front <"+USER+">","To ourselves","y\r\n"));
				//Nothing at all: not a message.
				fakePop3Server.AddMessage("empty",new byte[0]);
				Run run=Download(MakeAddress(fakePop3Server.Port,false));
				Assert.AreEqual(1,run.CountNew);
				StringAssert.Contains(run.ListImported.Single(),"Subject: To ourselves");
				CollectionAssert.AreEquivalent(new[] { "sent","self","empty" },run.ListUidsStored,"Every one is remembered, so none is downloaded again.");
			}
		}

		[TestMethod]
		public void WrongPassword_Throws_AndStoresNothing() {
			using(FakePop3Server fakePop3Server=new FakePop3Server(USER,PASSWORD,isTls:false)) {
				fakePop3Server.AddMessage("u1",Message("pat@example.test",USER,"x","x\r\n"));
				try {
					Download(MakeAddress(fakePop3Server.Port,false,"wrong-password"));
					Assert.Fail("The sign-in should fail.");
				}
				catch(AuthenticationException) {
					//MailKit's sign-in failure, which the inbox window shows as before.
				}
				Assert.IsFalse(fakePop3Server.GetCommands().Any(x => x.StartsWith("RETR")));
			}
		}

		[TestMethod]
		public void Pop3OverTls_Works_WithATrustedCertificate() {
			using(FakePop3Server fakePop3Server=new FakePop3Server(USER,PASSWORD,isTls:true)) {
				byte[] arrayMessage=Message("pat@example.test",USER,"Over TLS","Secret\r\n");
				fakePop3Server.AddMessage("tls-1",arrayMessage);
				EmailMessages.Pop3CertificateValidationForTests=TestCertificates.IsTestCertificate;
				Run run=Download(MakeAddress(fakePop3Server.Port,true));
				Assert.IsNull(fakePop3Server.ServerError,fakePop3Server.ServerError?.ToString());
				Assert.AreEqual(1,run.CountNew);
				Assert.AreEqual(Encoding.ASCII.GetString(arrayMessage),run.ListImported.Single());
				CollectionAssert.Contains(fakePop3Server.GetCommands(),"PASS "+PASSWORD,"The password went over the TLS connection.");
			}
		}

		[TestMethod]
		public void Pop3OverTls_RefusesAnUntrustedCertificate() {
			using(FakePop3Server fakePop3Server=new FakePop3Server(USER,PASSWORD,isTls:true)) {
				fakePop3Server.AddMessage("tls-1",Message("pat@example.test",USER,"Over TLS","Secret\r\n"));
				try {
					Download(MakeAddress(fakePop3Server.Port,true));
					Assert.Fail("A self-signed certificate must be refused.");
				}
				catch(SslHandshakeException) {
				}
				Assert.IsFalse(fakePop3Server.GetCommands().Any(x => x.StartsWith("PASS")),"No password is sent to a server that is not trusted.");
			}
		}

		[TestMethod]
		public void SignIn_IsUserAndPass_EvenWhenSaslAndApopAreOffered() {
			using(FakePop3Server fakePop3Server=new FakePop3Server(USER,PASSWORD,isTls:false)) {
				fakePop3Server.IsSaslAndApopOffered=true;
				fakePop3Server.AddMessage("u1",Message("pat@example.test",USER,"x","x\r\n"));
				Run run=Download(MakeAddress(fakePop3Server.Port,false));
				Assert.AreEqual(1,run.CountNew);
				List<string> listCommands=fakePop3Server.GetCommands();
				Assert.IsFalse(listCommands.Any(x => x.StartsWith("AUTH") || x.StartsWith("APOP")),string.Join(" | ",listCommands));
				CollectionAssert.Contains(listCommands,"USER "+USER);
			}
		}

		[TestMethod]
		public void PlainServer_IsNotSpokenToWithTls() {
			//UseSSL off means plain POP3 (port 110), as with OpenPop: no STLS is tried, and the fake server offers none.
			using(FakePop3Server fakePop3Server=new FakePop3Server(USER,PASSWORD,isTls:false)) {
				Run run=Download(MakeAddress(fakePop3Server.Port,false));
				Assert.AreEqual(0,run.CountNew);
				Assert.IsFalse(fakePop3Server.GetCommands().Any(x => x.StartsWith("STLS")));
			}
		}
	}
}
