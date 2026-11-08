// Direct secure messaging is kept (owner decision 2026-11-08; package S0-3D of the .NET 10 plan restores what S0-3 removed): the e-mail code
// has the Direct methods again and the windows their Direct controls, on top of S0-3's MailKit and MimeKit code; CDO stays gone, and the
// certificate-name helpers exist once (EmailCertificates). The behaviour is tested in DirectCertificateTests and DirectMessagingDbTests; this
// reads the source files. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LibraryUpdateTests {
	[TestClass]
	public class DirectRestoredTests {
		private static string Read(string relativePath) {
			return File.ReadAllText(ScratchDb.FindRepoFile(relativePath));
		}

		[TestMethod]
		public void EmailCode_HasDirectAgainAndNoCdo() {
			string textEmail=Read(@"OpenPmsDentalBusiness\Data Interface\EmailMessages.cs");
			foreach(string name in new[] { "private static string SendEmailDirect(EmailMessage emailMessage,EmailAddress emailAddressFrom)",
				"private static string SendAckDirect(","public static void SendOldestUnsentAck(EmailAddress emailAddressFrom)","private static void SendDirectUnsecure(",
				"bool useDirect=false","private static Health.Direct.Agent.DirectAgent GetDirectAgentForEmailAddress(","public static int GetReceiverUntrustedCount(",
				"public static bool IsSenderTrusted(","public static bool TryAddTrustDirect(","public static void TryAddTrustForSignature(",
				"public static void RefreshCertStoreExternal(","private static bool FindPublicCertForAddress(","private static void DnsQueryForCert(",
				"Health.Direct.ResolverPlugins.LdapCertResolver","public class EmailNameResolver : ICertificateResolver","public class EmailPublicResolver",
				"public class EmailPrivateResolver","directAgent.ProcessIncoming(incomingMessage)","SendAckDirect(incomingMessage,emailAddressReceiver,emailMessage.PatNum);" })
			{
				StringAssert.Contains(textEmail,name);
			}
			//After each POP3 check, the oldest unsent acknowledgement is sent.
			int indexReceive=textEmail.IndexOf("public static int ReceiveFromInbox(",StringComparison.Ordinal);
			Assert.IsTrue(textEmail.IndexOf("SendOldestUnsentAck(emailAddress);",indexReceive,StringComparison.Ordinal)
				<textEmail.IndexOf("internal static int DownloadPop3Inbox(",StringComparison.Ordinal));
			//One copy of the certificate-name and validity helpers: the resolvers call EmailCertificates.
			foreach(string helper in new[] { "public static string GetCertSubjectName(","public static string GetCertRfc822Name(","public static string GetCertDnsName(",
				"public static bool IsCertValid(" })
			{
				Assert.AreEqual(1,Regex.Matches(textEmail,Regex.Escape(helper)).Count,helper);
			}
			Assert.IsFalse(textEmail.Contains("Health.Direct.Agent.TrustChainValidator.DefaultProblemFlags"),"EmailCertificates.ProblemFlags stays.");
			//S0-3's improvements stay: received mail is parsed by MimeKit, the Direct parser only hands encrypted mail to the agent.
			StringAssert.Contains(textEmail,"public static MimeKit.MimeMessage RawEmailToMimeMessage(string strRawEmailIn)");
			StringAssert.Contains(textEmail,"private static Health.Direct.Agent.IncomingMessage RawEmailToIncomingMessage(");
			Assert.AreEqual(3,Regex.Matches(textEmail,@"RawEmailToIncomingMessage\(").Count,"Defined once, called for decryption and the preview only.");
			Assert.IsFalse(textEmail.Contains("ConvertMessageToEmailMessage("),"The Direct parser's conversion stays gone: MimeKit reads the decrypted message.");
			foreach(string file in new[] { @"OpenPmsDentalBusiness\Data Interface\EmailMessages.cs",@"OpenPmsDentalBusiness\Email\SendEmails.cs" }) {
				string text=Read(file);
				foreach(string name in new[] { "using CDO","CDO.","nameValueCollectionHeaders","AlternateView[]" }) {
					Assert.IsFalse(text.Contains(name),file+": "+name);
				}
			}
			StringAssert.Contains(Read(@"OpenPmsDentalBusiness\Email\SendEmails.cs"),"public static void WireMimeMessage(BasicEmailAddress address,MimeMessage mimeMessage)");
			string cleanUp=Read(@"OpenPmsDentalBusiness\Data Interface\DatabaseMaintenances.cs");
			int index=cleanUp.IndexOf("public static string CleanUpRawEmails()",StringComparison.Ordinal);
			Assert.IsTrue(index>0);
			Assert.IsFalse(cleanUp.Substring(index,cleanUp.IndexOf("public static string CleanUpAttachmentsRootDirectory()",StringComparison.Ordinal)-index)
				.Contains("Health.Direct"),"The database tool reads clear text messages with MimeKit (S0-3).");
		}

		[TestMethod]
		public void Windows_HaveTheirDirectControlsAgain() {
			string textMessage=Read(@"OpenPmsDental\Forms\FormEmailMessageEdit.cs");
			StringAssert.Contains(textMessage,"menuItemSendDirect.Text=Lan.g(this,\"Direct Messaging\");");
			StringAssert.Contains(textMessage,"private void butDirectMessage_Click(object sender,EventArgs e) {");
			StringAssert.Contains(textMessage,"EmailMessages.SendEmail(_emailMessage,emailAddressFrom,useDirect:true);");
			Assert.IsTrue(Regex.IsMatch(textMessage,@"butDecrypt\.Visible=true"),"Decrypt shows for a ReceivedEncrypted message.");
			StringAssert.Contains(textMessage,"string strRawEmail=_emailMessage.RawEmailIn;","Decrypt reads the raw message from RawEmailIn.");
			StringAssert.Contains(textMessage,"Use the Direct Message button instead.");
			string textSignature=Read(@"OpenPmsDental\Forms\FormEmailDigitalSignature.designer.cs");
			foreach(string name in new[] { "butTrust","textTrustStatus","textTrustExplanation","textSignedBy" }) {
				StringAssert.Contains(textSignature,name,"FormEmailDigitalSignature: "+name);
			}
			StringAssert.Contains(Read(@"OpenPmsDental\Forms\FormEmailDigitalSignature.cs"),"EmailMessages.TryAddTrustForSignature(_x509Certificate2);");
			StringAssert.Contains(Read(@"OpenPmsDental\Forms\FormEmailAddressEdit.designer.cs"),"butRegisterCertificate");
			StringAssert.Contains(Read(@"OpenPmsDental\Forms\FormEmailAddressEdit.cs"),"new FormEmailCertRegister(textUsername.Text)");
			StringAssert.Contains(Read(@"WpfControlsOD\Frms\FrmReferralEdit.xaml"),"x:Name=\"checkEmailTrustDirect\"");
			string textReferral=Read(@"WpfControlsOD\Frms\FrmReferralEdit.xaml.cs");
			StringAssert.Contains(textReferral,"ReferralCur.IsTrustedDirect=checkEmailTrustDirect.Checked==true;");
			StringAssert.Contains(textReferral,"EmailMessages.TryAddTrustDirect(textEmail.Text)");
			string textInbox=Read(@"OpenPmsDental\Forms\FormEmailInbox.cs");
			StringAssert.Contains(textInbox,"EmailMessages.GetReceiverUntrustedCount(emailMessage.FromAddress)==-1","The Sig column shows Direct trust (Y or N).");
			StringAssert.Contains(textInbox,"EmailMessages.IsSenderTrusted(listEmailsFiltered[i].FromAddress)");
			StringAssert.Contains(textInbox,"EmailMessages.RefreshCertStoreExternal(GetSelectedAddress());");
			StringAssert.Contains(Read(@"OpenPmsDental\User Controls\EmailPreviewControl.cs"),"EmailMessages.RefreshCertStoreExternal(EmailAddressPreview);");
			//No window keeps a second copy of the certificate-name helper.
			foreach(string file in new[] { @"OpenPmsDental\Forms\FormEmailDigitalSignature.cs",@"OpenPmsDental\Forms\FormEmailCertRegister.cs",
				@"OpenPmsDental\User Controls\EmailPreviewControl.cs" })
			{
				string text=Read(file);
				Assert.IsFalse(text.Contains("EmailNameResolver.GetCertSubjectName"),file);
				StringAssert.Contains(text,"EmailCertificates.GetCertSubjectName(",file);
			}
		}
	}
}
