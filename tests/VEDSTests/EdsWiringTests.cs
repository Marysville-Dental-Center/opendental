// V-EDS: the windows call the EDS validation where rule 1 says (Send and Save of a new claim in Edit Claim, Send for every claim, batch sends
// from the Send Claims window), and the new files stay vendor-neutral and talk only to EDS. Reads the repository's source files; no database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VEDSTests {
	[TestClass]
	public class EdsWiringTests {
		private static string Read(string relativePath) {
			return File.ReadAllText(TestDb.FindRepoFile(relativePath));
		}

		[TestMethod]
		public void GetMissingData_RunsTheEdsLocalChecks_Last() {
			string source=Read(@"OpenDentBusiness\Eclaims\Eclaims.cs");
			int indexMethod=source.IndexOf("public static ClaimSendQueueItem GetMissingData(");
			int indexHook=source.IndexOf("EdsClaimValidation.AddLocalChecksToQueueItem(clearinghouseClin,queueItem);",indexMethod);
			int indexCatch=source.IndexOf("Unable to validate claim data:",indexMethod);
			Assert.IsTrue(indexMethod>0 && indexHook>indexCatch && indexCatch>0,"After the format validators, so the EDS part is the end of Missing Info.");
		}

		[TestMethod]
		public void ClaimEditSend_ValidatesBeforeTheMissingDataMessage() {
			string source=Read(@"OpenDental\Forms\FormClaimEdit.cs");
			int indexSend=source.IndexOf("private void SendClaim() {");
			int indexHook=source.IndexOf("EdsValidationL.ValidateBeforeSend(_claim,clearinghouseClin,claimSendQueueItemsArrayCA[0])",indexSend);
			int indexMissing=source.IndexOf("Cannot send claim until missing/invalid data is fixed:",indexSend);
			int indexSendBatch=source.IndexOf("Eclaims.SendBatch(",indexSend);
			Assert.IsTrue(indexSend>0 && indexHook>indexSend && indexHook<indexMissing && indexMissing<indexSendBatch,
				"One validation window for an EDS claim, before anything is sent; existing claims too (no IsNew check).");
		}

		[TestMethod]
		public void ClaimEditSave_ValidatesNewEdsClaimsOnly() {
			string source=Read(@"OpenDental\Forms\FormClaimEdit.cs");
			StringAssert.Contains(source,"else if(IsNew && EdsValidationL.IsEds(clearinghouseClin)) {");
			StringAssert.Contains(source,"EdsValidationL.ValidateAfterSave(_claim,clearinghouseClin,claimSendQueueItemsArray[0]);");
			StringAssert.Contains(source,"labelEdsValidationState.Text=EdsValidationL.GetEdsTabText(_claim,_clearinghouse);");
			string designer=Read(@"OpenDental\Forms\FormClaimEdit.designer.cs");
			StringAssert.Contains(designer,"this.tabEDS.Controls.Add(this.labelEdsValidationState);","Rule 4: shown on the claim's EDS tab.");
		}

		[TestMethod]
		public void ClaimsSend_ValidatesTheBatchAfterXConnect() {
			string source=Read(@"OpenDental\Forms\FormClaimsSend.cs");
			int indexXConnect=source.IndexOf("if(!ApplyXConnectResultsToSelection()) {");
			int indexEds=source.IndexOf("if(!ApplyEdsResultsToSelection(hqClearinghouseNum)) {");
			int indexSend=source.IndexOf("ClaimL.SendClaimSendQueueItems(listClaimSendQueueItemsSelected,hqClearinghouseNum)");
			Assert.IsTrue(indexXConnect>0 && indexEds>indexXConnect && indexSend>indexEds);
			StringAssert.Contains(Read(@"OpenDental\Forms\FormClaimsSend.Eds.cs"),"EdsValidationL.ValidateBatchForSend(");
		}

		[TestMethod]
		public void ClaimAttachmentWindow_ShowsTheLastValidation() {
			StringAssert.Contains(Read(@"OpenDental\Forms\FormClaimAttachment.cs"),"EdsValidationL.GetLastValidationText(_claim.ClaimNum)");
		}

		[TestMethod]
		public void NewFiles_AreInTheProjects() {
			string csproj=Read(@"OpenDental\OpenDental.csproj");
			StringAssert.Contains(csproj,@"<Compile Include=""Forms\FormEdsValidation.cs"">");
			StringAssert.Contains(csproj,@"<Compile Include=""Forms\FormEdsValidation.Designer.cs"">");
			StringAssert.Contains(csproj,@"<Compile Include=""Forms\FormClaimsSend.Eds.cs"">");
			StringAssert.Contains(csproj,@"<Compile Include=""Logic\EdsValidationL.cs"" />");
			StringAssert.Contains(Read(@"OpenDentBusiness\OpenDentBusiness.csproj"),@"<Compile Include=""VendorDirect\**\*.cs"" />");
		}

		[TestMethod]
		public void NewFiles_AreVendorNeutral_AndCallOnlyEds() {
			string[] arrayFiles={
				@"OpenDentBusiness\VendorDirect\Eds\EdsClaimValidation.cs",
				@"OpenDentBusiness\VendorDirect\Eds\EdsClaimChecks.cs",
				@"OpenDentBusiness\VendorDirect\Eds\EdsClaimSnapshot.cs",
				@"OpenDentBusiness\VendorDirect\Eds\EdsValidationModels.cs",
				@"OpenDental\Logic\EdsValidationL.cs",
				@"OpenDental\Forms\FormEdsValidation.cs",
				@"OpenDental\Forms\FormEdsValidation.Designer.cs",
				@"OpenDental\Forms\FormClaimsSend.Eds.cs",
			};
			for(int i=0;i<arrayFiles.Length;i++) {
				string source=Read(arrayFiles[i]);
				Assert.IsFalse(Regex.IsMatch(source,@"https?://",RegexOptions.IgnoreCase),arrayFiles[i]+": no links; the only EDS host is in EDS.cs.");
				Assert.IsFalse(Regex.IsMatch(source,@"www\.|sign up|signup|recommended|opendental\.com",RegexOptions.IgnoreCase),arrayFiles[i]);
			}
			string eds=Read(@"OpenDentBusiness\Eclaims\EDS.cs");
			string[] arrayHosts=Regex.Matches(eds,@"https://([a-z0-9.\-]+)",RegexOptions.IgnoreCase).Cast<Match>().Select(x => x.Groups[1].Value.ToLowerInvariant())
				.Distinct().ToArray();
			CollectionAssert.AreEqual(new[] { "web2.edsedi.com" },arrayHosts,"EDS.cs talks only to EDS.");
		}

		[TestMethod]
		public void Validation_IsOffUntilTheOfficeTurnsOnTheAttachmentService() {
			//The network part is gated on Clearinghouse.IsAttachmentSendAllowed, which the clearinghouse window starts unchecked (Allow attachment send).
			string source=Read(@"OpenDentBusiness\VendorDirect\Eds\EdsClaimValidation.cs");
			StringAssert.Contains(source,"&& clearinghouseClin.IsAttachmentSendAllowed");
			string window=Read(@"OpenDental\Forms\FormEdsValidation.cs");
			Assert.IsFalse(window.Contains("Password"),"The window never shows the login.");
		}
	}
}
