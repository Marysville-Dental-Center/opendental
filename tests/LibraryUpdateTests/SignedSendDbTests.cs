// Signed sending end to end (package S0-3 of the .NET 10 plan, step 4): EmailMessages.SendEmail with the signing certificate, as the email window
// sends when Signed By shows a certificate, on a scratch database: the message arrives at the fake SMTP server on 127.0.0.1 as multipart/signed
// that MimeKit verifies, from the account's user name (the certificate's address), and is saved as sent. The database is named s03_*, made
// from db/init/new-database.sql and dropped afterwards; inconclusive without the local MariaDB. The certificate is self-signed and made in
// memory. Synthetic data only. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using CodeBase;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MimeKit;
using MimeKit.Cryptography;
using MySqlConnector;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;
using UnitTestsCore;

namespace LibraryUpdateTests {
	[TestClass]
	public class SignedSendDbTests {
		private const string PREFIX="s03_";
		private static readonly string DB_NAME=PREFIX+DateTime.UtcNow.ToString("yyMMddHHmmss",CultureInfo.InvariantCulture)+"_"
			+Process.GetCurrentProcess().Id+"_"+new Random().Next(1000,10000)+"_signed";
		private const string USER="front@example.test";
		private const string PASSWORD="synthetic-smtp-password";
		private const string USER_NAME_OD="S03Sender";
		private static string _skipReason;
		private static bool _isBuilt;
		private bool _isUnitTestOld;

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
				//A user in the first user group (Admin in a new database), who may send email.
				UserodT.CreateUser(USER_NAME_OD,"synthetic-od-password",new List<long> { 1 });
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
		}

		[TestCleanup]
		public void TestCleanup() {
			if(_skipReason!=null) {
				return;
			}
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

		[TestMethod]
		public void SendEmail_WithTheCertificate_ArrivesSignedAndIsSaved() {
			Assert.IsNotNull(Security.CurUser,"The sending user exists.");
			X509Certificate2 x509Certificate2=TestCertificates.CreateEmail(USER);
			try {
				using(FakeSmtpServer fakeSmtpServer=new FakeSmtpServer(USER,PASSWORD,isStartTlsOffered:false)) {
					EmailAddress emailAddress=new EmailAddress();
					emailAddress.EmailUsername=USER;
					emailAddress.SenderAddress="Front Desk <frontdesk@example.test>";
					emailAddress.EmailPassword=MiscUtils.Encrypt(PASSWORD);
					emailAddress.SMTPserver="127.0.0.1";
					emailAddress.ServerPort=fakeSmtpServer.Port;
					emailAddress.UseSSL=false;
					emailAddress.AuthenticationType=OAuthType.None;
					EmailMessage emailMessage=new EmailMessage();
					emailMessage.FromAddress="Front Desk <frontdesk@example.test>";
					emailMessage.ToAddress="pat@example.test";
					emailMessage.Subject="Signed reminder";
					emailMessage.BodyText="Your appointment is on Tuesday.";
					emailMessage.HtmlType=EmailType.Regular;
					emailMessage.SentOrReceived=EmailSentOrReceived.Sent;
					emailMessage.MsgDateTime=DateTime.Now;
					EmailMessages.SendEmail(emailMessage,emailAddress,x509Certificate2);
					Assert.IsNull(fakeSmtpServer.ServerError,fakeSmtpServer.ServerError?.ToString());
					FakeSmtpServer.Received received=fakeSmtpServer.ListReceived.Single();
					MimeMessage mimeMessage;
					using(MemoryStream memoryStream=new MemoryStream(received.Data)) {
						mimeMessage=MimeMessage.Load(memoryStream);
					}
					MultipartSigned multipartSigned=mimeMessage.Body as MultipartSigned;
					Assert.IsNotNull(multipartSigned);
					using(TemporarySecureMimeContext temporarySecureMimeContext=new TemporarySecureMimeContext()) {
						Assert.IsTrue(multipartSigned.Verify(temporarySecureMimeContext).Single().Verify(true));
					}
					Assert.AreEqual(USER,mimeMessage.From.Mailboxes.Single().Address,"Signed messages come from the user name, the certificate's address.");
					Assert.AreEqual("Your appointment is on Tuesday.",mimeMessage.TextBody.TrimEnd('\r','\n'));
					Assert.AreNotEqual(0,emailMessage.EmailMessageNum,"Saved.");
					EmailMessage emailMessageSaved=EmailMessages.GetOne(emailMessage.EmailMessageNum);
					Assert.AreEqual(EmailSentOrReceived.Sent,emailMessageSaved.SentOrReceived);
					Assert.AreEqual(USER,emailMessageSaved.FromAddress);
				}
			}
			finally {
				x509Certificate2.Reset();
			}
		}
	}
}
