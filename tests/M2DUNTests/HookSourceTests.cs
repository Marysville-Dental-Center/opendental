// HTML dunning and billing e-mails (backlog package M2-DUN): the small hooks in shared files that call BillingEmailHtml. The windows can't be
// opened in a test run, and other packages edit the same files in this wave, so these checks make sure a merge keeps every hook.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace M2DUNTests {
	[TestClass]
	public class HookSourceTests {
		private static string ReadRepoFile(string relativePath) {
			return File.ReadAllText(TestDb.FindRepoFile(relativePath));
		}

		private static string GetMethodBody(string source,string methodSignatureStart) {
			int start=source.IndexOf(methodSignatureStart);
			Assert.IsTrue(start>=0,"Not found: "+methodSignatureStart);
			int end=source.IndexOf("\n\t\t}",start);
			Assert.IsTrue(end>start);
			return source.Substring(start,end-start);
		}

		///<summary>Every statement e-mail (billing, the statement window, the sheet window, the Account module) is built here, and gets the type of
		///its body after the tags are replaced.</summary>
		[TestMethod]
		public void Statements_EmailForStatementAppliesTheEmailType() {
			string body=GetMethodBody(ReadRepoFile(Path.Combine("OpenPmsDentalBusiness","Data Interface","Statements.cs")),
				"public static EmailMessage GetEmailMessageForStatement(");
			int indexReplace=body.IndexOf("emailMessage.BodyText=MessageTagReplacer.ReplaceTagsForStatement(");
			int indexApply=body.IndexOf("BillingEmailHtml.ApplyEmailType(emailMessage,BillingEmailHtml.GetEmailTypeForStatement(statement,patient))");
			Assert.IsTrue(indexReplace>=0);
			Assert.IsTrue(indexApply>indexReplace,"The e-mail type must be applied after the body's tags are replaced.");
		}

		[TestMethod]
		public void Statements_ReceiptErrorEmailIsPlainText() {
			string source=ReadRepoFile(Path.Combine("OpenPmsDentalBusiness","Data Interface","Statements.cs"));
			StringAssert.Contains(source,"emailMessageError.BodyText=errorMessage;");
			Assert.IsTrue(Regex.IsMatch(source,@"emailMessageError\.BodyText=errorMessage;\s*emailMessageError\.HtmlType=EmailType\.Regular;"));
		}

		[TestMethod]
		public void BillingL_HtmlEmailTakesTheAutographAsHtml() {
			string source=ReadRepoFile(Path.Combine("OpenPmsDentalBusiness","Logic","BillingL.cs"));
			Assert.IsTrue(Regex.IsMatch(source,
				@"if\(BillingEmailHtml\.TryInsertAutograph\(emailMessage,emailAutograph\)\)\s*\{[^}]*emailAutograph=null;\s*\}\s*if\(emailAutograph!=null\)"));
		}

		[TestMethod]
		public void FormDunningEdit_LoadsAndSavesTheEmailType() {
			string source=ReadRepoFile(Path.Combine("OpenPmsDental","Forms","FormDunningEdit.cs"));
			StringAssert.Contains(source,"comboEmailType.SetSelectedEnum(BillingEmailHtml.ToEmailType((int)_dunning.EmailType));");
			StringAssert.Contains(source,"_dunning.EmailType=comboEmailType.GetSelected<EmailType>();");
			StringAssert.Contains(source,"BillingEmailHtml.GetBodyError(textEmailBody.Text,comboEmailType.GetSelected<EmailType>())");
			StringAssert.Contains(source,"FormEmailEdit formEmailEdit=new FormEmailEdit();");
			string designer=ReadRepoFile(Path.Combine("OpenPmsDental","Forms","FormDunningEdit.designer.cs"));
			StringAssert.Contains(designer,"this.groupBox4.Controls.Add(this.comboEmailType);");
			StringAssert.Contains(designer,"this.groupBox4.Controls.Add(this.butEditHtml);");
			StringAssert.Contains(designer,"this.butEditHtml.Click += new System.EventHandler(this.butEditHtml_Click);");
		}

		///<summary>Since Open Dental 26.2 (P262-BILLING) the e-mail type is one of the Billing Defaults messages a clinic can have of its own: the window
		///shows the type of the clinic shown, keeps it with the clinic's settings, and BillingDefaultsByClinic saves it (the BillingEmailType
		///preference for the practice defaults, a clinicpref row for a clinic).</summary>
		[TestMethod]
		public void FormBillingDefaults_LoadsAndSavesTheEmailType() {
			string source=ReadRepoFile(Path.Combine("OpenPmsDental","Forms","FormBillingDefaults.cs"));
			StringAssert.Contains(source,"comboBillingEmailType.SetSelectedEnum(BillingEmailHtml.GetBillingDefaultsEmailType());");
			StringAssert.Contains(source,"comboBillingEmailType.SetSelectedEnum(settingsShown.EmailType);");
			StringAssert.Contains(source,"settings.EmailType=comboBillingEmailType.GetSelected<EmailType>();");
			StringAssert.Contains(source,"BillingEmailHtml.GetBodyError(settings.EmailBody,settings.EmailType)");
			string save=GetMethodBody(source,"private void butSave_Click(");
			StringAssert.Contains(save,"StoreSettings();");
			StringAssert.Contains(save,"if(!IsBillingEmailBodyValid()) {");
			StringAssert.Contains(save,"BillingDefaultsByClinic.SaveSettings(settings,_dictionarySettingsOld[clinicNum])");
			Assert.IsTrue(save.IndexOf("StoreSettings();")<save.IndexOf("if(!IsBillingEmailBodyValid()) {"),"The boxes are kept before they are checked.");
			string saveSettings=GetMethodBody(ReadRepoFile(Path.Combine("OpenPmsDentalBusiness","Logic","BillingDefaultsByClinic.cs")),
				"public static List<KeyValuePair<PrefName,string>> GetValues(");
			StringAssert.Contains(saveSettings,"new KeyValuePair<PrefName,string>(PrefName.BillingEmailType,POut.Int((int)settings.EmailType))");
			string load=GetMethodBody(source,"private void FormBillingDefaults_Load(");
			StringAssert.Contains(load,"FillBillingEmailType();");
			string designer=ReadRepoFile(Path.Combine("OpenPmsDental","Forms","FormBillingDefaults.Designer.cs"));
			StringAssert.Contains(designer,"this.groupBox3.Controls.Add(this.comboBillingEmailType);");
			StringAssert.Contains(designer,"this.butBillingEmailEditHtml.Click += new System.EventHandler(this.butBillingEmailEditHtml_Click);");
		}
	}
}
