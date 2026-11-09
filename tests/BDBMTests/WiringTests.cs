// The Database Maintenance window cannot be opened in a test run (the GUI is not launched), so these tests read the window sources and check that
// each B-DBM tool is on the Tools tab and calls the business methods the other files test.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.IO;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectFileTests;

namespace BDBMTests {
	[TestClass]
	public class WiringTests {
		private static string Read(params string[] arrayParts) {
			return File.ReadAllText(TestDb.FindRepoFile(Path.Combine(arrayParts)));
		}

		///<summary>The text of one method: from its signature to its closing brace, the first brace at the method's own indent (two tabs).</summary>
		private static string MethodBody(string source,string signature) {
			int idxStart=source.IndexOf(signature);
			Assert.IsTrue(idxStart>=0,signature+" not found.");
			int idxEnd=source.IndexOf("\n\t\t}",idxStart);
			Assert.IsTrue(idxEnd>idxStart,signature+" has no closing brace.");
			return source.Substring(idxStart,idxEnd-idxStart);
		}

		private static void AssertBefore(string body,string before,string after) {
			int idxBefore=body.IndexOf(before);
			int idxAfter=body.IndexOf(after);
			Assert.IsTrue(idxBefore>=0,before+" not found.");
			Assert.IsTrue(idxAfter>=0,after+" not found.");
			Assert.IsTrue(idxBefore<idxAfter,before+" must come before "+after);
		}

		[TestMethod]
		public void ToolsTab_HasTheThreeNewTools() {
			string fillGridTools=MethodBody(Read("OpenPmsDental","Forms","FormDatabaseMaintenance.cs"),"private void FillGridTools()");
			StringAssert.Contains(fillGridTools,"Lan.g(this,\"Base64 Documents\")");
			StringAssert.Contains(fillGridTools,"row.Tag=new Action(Base64DocumentsFix);");
			StringAssert.Contains(fillGridTools,"Lan.g(this,\"Invalid Characters (GDI+)\")");
			StringAssert.Contains(fillGridTools,"row.Tag=new Action(InvalidCharsGdiFix);");
			StringAssert.Contains(fillGridTools,"Lan.g(this,\"Notes White Space\")");
			StringAssert.Contains(fillGridTools,"row.Tag=new Action(NotesWhiteSpaceFix);");
		}

		[TestMethod]
		public void ToolHandlers_CallTheBusinessMethods() {
			string source=Read("OpenPmsDental","Forms","FormDatabaseMaintenance.Tools25.cs");
			string whiteSpace=MethodBody(source,"private void NotesWhiteSpaceFix()");
			AssertBefore(whiteSpace,"MsgBox.Show(this,MsgBoxButtons.OKCancel","DatabaseMaintenances.NotesWithTooMuchWhiteSpace(true,DbmMode.Fix)");
			StringAssert.Contains(whiteSpace,"MsgBoxCopyPaste");
			string base64=MethodBody(source,"private void Base64DocumentsFix()");
			AssertBefore(base64,"DatabaseMaintenances.GetDocNumsWithBase64Notes()","MessageBox.Show(msg");
			AssertBefore(base64,"MessageBox.Show(msg","DatabaseMaintenances.CreateDocumentsFromBase64Notes()");
			string invalidChars=MethodBody(source,"private void InvalidCharsGdiFix()");
			AssertBefore(invalidChars,"VerifyPermissionToRunReplicationUnsafeMethods()","new FormDatabaseMaintInvalidChars(canFix)");
			StringAssert.Contains(invalidChars,"_isReplicationPasswordEntered");
			StringAssert.Contains(invalidChars,"_isCacheInvalid=true");
		}

		[TestMethod]
		public void InvalidCharsWindow_FixNeedsTheReplicationCheckAndAConfirmation() {
			string source=Read("OpenPmsDental","Forms","FormDatabaseMaintInvalidChars.cs");
			StringAssert.Contains(MethodBody(source,"private void Scan(List<string> listLabels)"),"butFix.Enabled=_canFix && ");
			string fix=MethodBody(source,"private void butFix_Click(object sender,EventArgs e)");
			AssertBefore(fix,"if(!_canFix","DatabaseMaintenances.InvalidCharsGdiFix(listLabels)");
			AssertBefore(fix,"Make a backup of the database before you continue.","DatabaseMaintenances.InvalidCharsGdiFix(listLabels)");
			AssertBefore(fix,"MessageBoxButtons.OKCancel","DatabaseMaintenances.InvalidCharsGdiFix(listLabels)");
			StringAssert.Contains(MethodBody(source,"private void butScan_Click(object sender,EventArgs e)"),"Scan(listLabels)");
			StringAssert.Contains(MethodBody(source,"private void Scan(List<string> listLabels)"),"DatabaseMaintenances.InvalidCharsGdiScan(listLabels)");
		}

		[TestMethod]
		public void RawEmails_NormalizesWhiteSpaceBeforeComparing() {
			string cleanUp=MethodBody(Read("OpenPmsDentalBusiness","Data Interface","DatabaseMaintenances.cs"),"public static string CleanUpRawEmails()");
			StringAssert.Contains(cleanUp,"FindEmailAddressForRawEmail(listEmailAddresses,emailMessage.RecipientAddress)");
			//S0-3: the copies hold the subject and body text MimeKit read (as stored), no longer the raw Subject header and MIME body of the Direct parser.
			AssertBefore(cleanUp,"emailMessageNewCopy.BodyText=SOut.StringParam(emailMessageNew.BodyText);","NormalizeRawEmailWhiteSpace(emailMessageNewCopy,emailMessageOldCopy)");
			Assert.IsFalse(cleanUp.Contains("Health.Direct"),"No Direct Project types are left in the tool.");
			AssertBefore(cleanUp,"NormalizeRawEmailWhiteSpace(emailMessageNewCopy,emailMessageOldCopy)","Crud.EmailMessageCrud.UpdateComparison(");
		}

		[TestMethod]
		public void NewFiles_AreInTheProjects() {
			//The evaluated items, so the check holds for the classic project files and the SDK-style ones of the .NET 10 move alike.
			ProjectFile openDental=ProjectFile.Load(TestDb.FindRepoFile(Path.Combine("OpenPmsDental","OpenPmsDental.csproj")));
			openDental.AssertCompiles(@"Forms\FormDatabaseMaintInvalidChars.cs",@"Forms\FormDatabaseMaintInvalidChars.Designer.cs",
				@"Forms\FormDatabaseMaintenance.Tools25.cs");
			ProjectFile business=ProjectFile.Load(TestDb.FindRepoFile(Path.Combine("OpenPmsDentalBusiness","OpenPmsDentalBusiness.csproj")));
			business.AssertCompiles(@"Misc\DbmTextCleaner.cs",@"Data Interface\DatabaseMaintenances.Base64Documents.cs",
				@"Data Interface\DatabaseMaintenances.EmailText.cs",@"Data Interface\DatabaseMaintenances.EmployeeSupervisor.cs",
				@"Data Interface\DatabaseMaintenances.InvalidCharsGdi.cs");
		}
	}
}
