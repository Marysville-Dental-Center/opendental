// Direct secure messaging and CDO are gone from the e-mail code (package S0-3 of the .NET 10 plan; owner decision 15): no Health.Direct type,
// no CDO, none of the removed methods, and none of the Direct controls of the e-mail, signature, email address and referral windows. The
// references and DLLs themselves go in package S0-2b. Reads the source files. Licensed under the GNU General Public License, version 2 or later.
// See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LibraryUpdateTests {
	[TestClass]
	public class DirectRemovedTests {
		private static readonly string[] _arrayFiles=new[] {
			@"OpenPmsDentalBusiness\Data Interface\EmailMessages.cs",
			@"OpenPmsDentalBusiness\Email\SendEmails.cs",
			@"OpenPmsDental\Forms\FormEmailInbox.cs",
			@"OpenPmsDental\Forms\FormEmailMessageEdit.cs",
			@"OpenPmsDental\Forms\FormEmailDigitalSignature.cs",
			@"OpenPmsDental\Forms\FormEmailDigitalSignature.designer.cs",
			@"OpenPmsDental\Forms\FormEmailAddressEdit.cs",
			@"OpenPmsDental\Forms\FormEmailAddressEdit.designer.cs",
			@"OpenPmsDental\User Controls\EmailPreviewControl.cs",
			@"WpfControlsOD\Frms\FrmReferralEdit.xaml",
			@"WpfControlsOD\Frms\FrmReferralEdit.xaml.cs",
		};

		private static string Read(string relativePath) {
			return File.ReadAllText(ScratchDb.FindRepoFile(relativePath));
		}

		[TestMethod]
		public void EmailCode_UsesNoDirectAndNoCdo() {
			List<string> listProblems=new List<string>();
			foreach(string file in _arrayFiles) {
				string text=Read(file);
				foreach(string name in new[] { "Health.Direct","using CDO","CDO.","SendEmailDirect","SendAckDirect","SendOldestUnsentAck","SendDirectUnsecure",
					"useDirect","GetDirectAgentForEmailAddress","GetReceiverUntrustedCount","IsSenderTrusted","TryAddTrustDirect","TryAddTrustForSignature",
					"RefreshCertStoreExternal","FindPublicCertForAddress","DnsQueryForCert","LdapCertResolver","EmailNameResolver","EmailPublicResolver",
					"EmailPrivateResolver","RawEmailToIncomingMessage","nameValueCollectionHeaders","SystemX509Store" })
				{
					if(text.Contains(name)) {
						listProblems.Add(file+": "+name);
					}
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
			string cleanUp=Read(@"OpenPmsDentalBusiness\Data Interface\DatabaseMaintenances.cs");
			int index=cleanUp.IndexOf("public static string CleanUpRawEmails()",StringComparison.Ordinal);
			Assert.IsTrue(index>0);
			Assert.IsFalse(cleanUp.Substring(index,cleanUp.IndexOf("public static string CleanUpAttachmentsRootDirectory()",StringComparison.Ordinal)-index)
				.Contains("Health.Direct"));
		}

		[TestMethod]
		public void Windows_HaveNoDirectControls() {
			string textMessage=Read(@"OpenPmsDental\Forms\FormEmailMessageEdit.cs");
			Assert.IsFalse(textMessage.Contains("Direct Messaging\");"),"No Direct Messaging send.");
			Assert.IsFalse(textMessage.Contains("butDirectMessage_Click"));
			StringAssert.Contains(textMessage,"butDecrypt.Visible=false;");
			Assert.IsFalse(Regex.IsMatch(textMessage,@"butDecrypt\.Visible=true"),"Decrypt is never shown.");
			string textSignature=Read(@"OpenPmsDental\Forms\FormEmailDigitalSignature.designer.cs");
			foreach(string name in new[] { "butTrust","textTrustStatus","textTrustExplanation" }) {
				Assert.IsFalse(textSignature.Contains(name),"FormEmailDigitalSignature: "+name);
			}
			StringAssert.Contains(textSignature,"textSignedBy","The window still shows the certificate.");
			Assert.IsFalse(Read(@"OpenPmsDental\Forms\FormEmailAddressEdit.designer.cs").Contains("butRegisterCertificate"));
			Assert.IsFalse(Read(@"OpenPmsDental\Forms\FormEmailAddressEdit.cs").Contains("FormEmailCertRegister"));
			Assert.IsFalse(Read(@"WpfControlsOD\Frms\FrmReferralEdit.xaml").Contains("checkEmailTrustDirect"));
			string textInbox=Read(@"OpenPmsDental\Forms\FormEmailInbox.cs");
			StringAssert.Contains(textInbox,"x.DisplayedFileName.ToLower()==\"smime.p7s\"","The Sig column still shows a signed message.");
		}
	}
}
