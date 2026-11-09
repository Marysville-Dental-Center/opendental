// V-EDS: the windows call the EDS validation where rule 1 says (Send and Save of a new claim in Edit Claim, Send for every claim, batch sends
// from the Send Claims window), and the new files stay vendor-neutral and talk only to EDS. Reads the repository's source files; no database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectFileTests;

namespace VEDSTests {
	[TestClass]
	public class EdsWiringTests {
		private static string Read(string relativePath) {
			return File.ReadAllText(TestDb.FindRepoFile(relativePath));
		}

		[TestMethod]
		public void GetMissingData_RunsTheEdsLocalChecks_Last() {
			string source=Read(@"OpenPmsDentalBusiness\Eclaims\Eclaims.cs");
			int indexMethod=source.IndexOf("public static ClaimSendQueueItem GetMissingData(");
			int indexHook=source.IndexOf("EdsClaimValidation.AddLocalChecksToQueueItem(clearinghouseClin,queueItem);",indexMethod);
			int indexCatch=source.IndexOf("Unable to validate claim data:",indexMethod);
			Assert.IsTrue(indexMethod>0 && indexHook>indexCatch && indexCatch>0,"After the format validators, so the EDS part is the end of Missing Info.");
		}

		[TestMethod]
		public void ClaimEditSend_ValidatesBeforeTheMissingDataMessage() {
			string source=Read(@"OpenPmsDental\Forms\FormClaimEdit.cs");
			int indexSend=source.IndexOf("private void SendClaim() {");
			int indexHook=source.IndexOf("EdsValidationL.ValidateBeforeSend(_claim,clearinghouseClin,claimSendQueueItemsArrayCA[0])",indexSend);
			int indexMissing=source.IndexOf("Cannot send claim until missing/invalid data is fixed:",indexSend);
			int indexSendBatch=source.IndexOf("Eclaims.SendBatch(",indexSend);
			Assert.IsTrue(indexSend>0 && indexHook>indexSend && indexHook<indexMissing && indexMissing<indexSendBatch,
				"One validation window for an EDS claim, before anything is sent; existing claims too (no IsNew check).");
			//Re-validated in the window: the Missing Info that Edit Claim checks next is read again, so a problem fixed meanwhile does not block.
			string logic=Read(@"OpenPmsDental\Logic\EdsValidationL.cs");
			int indexMethod=logic.IndexOf("public static bool ValidateBeforeSend(");
			int indexRefresh=logic.IndexOf("RefreshMissingData(claim,clearinghouseClin,queueItem);",indexMethod);
			int indexAfterSave=logic.IndexOf("public static void ValidateAfterSave(",indexMethod);
			Assert.IsTrue(indexMethod>0 && indexRefresh>indexMethod && indexRefresh<indexAfterSave);
		}

		[TestMethod]
		public void ClaimEditSave_ValidatesNewEdsClaimsOnly() {
			string source=Read(@"OpenPmsDental\Forms\FormClaimEdit.cs");
			StringAssert.Contains(source,"else if(IsNew && EdsValidationL.IsEds(clearinghouseClin)) {");
			StringAssert.Contains(source,"EdsValidationL.ValidateAfterSave(_claim,clearinghouseClin,claimSendQueueItemsArray[0]);");
			StringAssert.Contains(source,"labelEdsValidationState.Text=EdsValidationL.GetEdsTabText(_claim,_clearinghouse);");
			string designer=Read(@"OpenPmsDental\Forms\FormClaimEdit.designer.cs");
			StringAssert.Contains(designer,"this.tabEDS.Controls.Add(this.labelEdsValidationState);","Rule 4: shown on the claim's EDS tab.");
		}

		[TestMethod]
		public void ClaimsSend_ValidatesTheBatchAfterXConnect() {
			string source=Read(@"OpenPmsDental\Forms\FormClaimsSend.cs");
			int indexXConnect=source.IndexOf("if(!ApplyXConnectResultsToSelection()) {");
			int indexEds=source.IndexOf("if(!ApplyEdsResultsToSelection(hqClearinghouseNum)) {");
			int indexSend=source.IndexOf("ClaimL.SendClaimSendQueueItems(listClaimSendQueueItemsSelected,hqClearinghouseNum)");
			Assert.IsTrue(indexXConnect>0 && indexEds>indexXConnect && indexSend>indexEds);
			StringAssert.Contains(Read(@"OpenPmsDental\Forms\FormClaimsSend.Eds.cs"),"EdsValidationL.ValidateBatchForSend(");
		}

		[TestMethod]
		public void NextInsurancePrompt_ValidatesEdsClaimsBeforeSending() {
			//Integration of V-EDS with M2-CLAIMRCV (port/int-9): "Send claim(s) now" in the prompt for the next insurance's claims sends through
			//SendClaimSendQueueItems, which the Send Claims window also calls after its own validation. So the EDS validation runs at the prompt's
			//call site, after the XConnect one and before the send, and never inside SendClaimSendQueueItems.
			string source=Read(@"OpenPmsDental\Logic\ClaimL.cs");
			int indexPrompt=source.IndexOf("public static void PromptForPrimaryOrSecondaryClaim(");
			int indexSendMethod=source.IndexOf("internal static List<ClaimSendQueueItem> SendClaimSendQueueItems(");
			Assert.IsTrue(indexPrompt>0 && indexSendMethod>indexPrompt);
			string prompt=source.Substring(indexPrompt,indexSendMethod-indexPrompt);
			int indexXConnect=prompt.IndexOf("FilterQueueItemsByXConnect(");
			int indexEds=prompt.IndexOf("listClaimSendQueueItemsToSend=EdsValidationL.FilterQueueItemsForSend(listClaimSendQueueItemsToSend);");
			int indexSend=prompt.IndexOf("SendClaimSendQueueItems(listClaimSendQueueItemsToSend,");
			Assert.IsTrue(indexXConnect>0 && indexEds>indexXConnect && indexSend>indexEds,"Validated, then only the claims that passed are sent.");
			int indexSendEnd=source.IndexOf("#region XConnect claim validation",indexSendMethod);
			Assert.IsTrue(indexSendEnd>indexSendMethod);
			Assert.IsFalse(source.Substring(indexSendMethod,indexSendEnd-indexSendMethod).Contains("EdsValidationL."),"Not validated twice from Send Claims.");
			//The filter keeps the claims that do not go to EDS and the EDS claims allowed, and drops claims no longer waiting to send.
			string logic=Read(@"OpenPmsDental\Logic\EdsValidationL.cs");
			int indexFilter=logic.IndexOf("public static List<ClaimSendQueueItem> FilterQueueItemsForSend(");
			int indexFilterEnd=logic.IndexOf("private static void SetAllowed(",indexFilter);
			Assert.IsTrue(indexFilter>0 && indexFilterEnd>indexFilter);
			string filter=logic.Substring(indexFilter,indexFilterEnd-indexFilter);
			StringAssert.Contains(filter,"ValidateBatchForSend(listClaimSendQueueItems,0)");
			StringAssert.Contains(filter,"if(batchOutcome.IsCancelled) {");
			StringAssert.Contains(filter,"!batchOutcome.ListClaimNumsNotWaiting.Contains(x.ClaimNum)");
			StringAssert.Contains(filter,"(!batchOutcome.ListClaimNumsValidated.Contains(x.ClaimNum) || batchOutcome.ListClaimNumsAllowed.Contains(x.ClaimNum))");
		}

		[TestMethod]
		public void ClaimsSend_NeverResendsClaimsSentOrDeletedFromTheWindow() {
			//Edit Claim opened from the batch validation window can send, delete or hold a claim. The window re-validates it as a claim that must be
			//waiting to send, the final list is read again from the database, and the Send Claims window deselects and drops such claims.
			string window=Read(@"OpenPmsDental\Forms\FormEdsValidation.cs");
			StringAssert.Contains(window,"bool isWaitingToSendRequired=_openedFrom==EnumOpenedFrom.ClaimsSend;");
			StringAssert.Contains(window,"_hqClearinghouseNumOverride,EDS.TimeoutValidateOnSend,isWaitingToSendRequired);");
			string logic=Read(@"OpenPmsDental\Logic\EdsValidationL.cs");
			StringAssert.Contains(logic,"batchOutcome.ListClaimNumsAllowed=EdsClaimValidation.GetClaimNumsToSendForBatch(listResultsFinal,isSendAnywayConfirmed);");
			Assert.IsFalse(logic.Contains("batchOutcome.ListClaimNumsAllowed=EdsClaimValidation.GetClaimNumsToSend("),"Every path reads the claims again.");
			StringAssert.Contains(logic,"if(!EdsClaimValidation.IsWaitingToSend(claim)) {");
			string send=Read(@"OpenPmsDental\Forms\FormClaimsSend.Eds.cs");
			StringAssert.Contains(send,"if(batchOutcome.ListClaimNumsNotWaiting.Contains(claimSendQueueItem.ClaimNum)");
			StringAssert.Contains(send,"FillGrid(true);");
		}

		[TestMethod]
		public void ClaimAttachmentWindow_ShowsTheLastValidation() {
			StringAssert.Contains(Read(@"OpenPmsDental\Forms\FormClaimAttachment.cs"),"EdsValidationL.GetLastValidationText(_claim.ClaimNum)");
		}

		[TestMethod]
		public void NewFiles_AreInTheProjects() {
			//The evaluated items, so the checks hold for the classic project files and the SDK-style ones of the .NET 10 move alike.
			ProjectFile.Load(TestDb.FindRepoFile(@"OpenPmsDental\OpenPmsDental.csproj")).AssertCompiles(@"Forms\FormEdsValidation.cs",
				@"Forms\FormEdsValidation.Designer.cs",@"Forms\FormClaimsSend.Eds.cs",@"Logic\EdsValidationL.cs");
			//The EDS business classes are compiled with the whole VendorDirect folder (its wildcard in the classic project file, the default glob in
			//an SDK-style one).
			ProjectFile projectFileBusiness=ProjectFile.Load(TestDb.FindRepoFile(@"OpenPmsDentalBusiness\OpenPmsDentalBusiness.csproj"));
			List<string> listNotCompiled=projectFileBusiness.GetFilesNotCompiled(@"VendorDirect\Eds");
			Assert.AreEqual(0,listNotCompiled.Count,"Not compiled: "+string.Join(", ",listNotCompiled));
			//By that wildcard or glob, so the folder needs no line of its own in the project file.
			List<string> listNotByWildcard=projectFileBusiness.GetFilesNotCompiledByWildcard(@"VendorDirect\Eds");
			Assert.AreEqual(0,listNotByWildcard.Count,"Not compiled by a wildcard: "+string.Join(", ",listNotByWildcard));
		}

		[TestMethod]
		public void NewFiles_AreVendorNeutral_AndCallOnlyEds() {
			string[] arrayFiles={
				@"OpenPmsDentalBusiness\VendorDirect\Eds\EdsClaimValidation.cs",
				@"OpenPmsDentalBusiness\VendorDirect\Eds\EdsClaimChecks.cs",
				@"OpenPmsDentalBusiness\VendorDirect\Eds\EdsClaimSnapshot.cs",
				@"OpenPmsDentalBusiness\VendorDirect\Eds\EdsValidationModels.cs",
				@"OpenPmsDental\Logic\EdsValidationL.cs",
				@"OpenPmsDental\Forms\FormEdsValidation.cs",
				@"OpenPmsDental\Forms\FormEdsValidation.Designer.cs",
				@"OpenPmsDental\Forms\FormClaimsSend.Eds.cs",
			};
			for(int i=0;i<arrayFiles.Length;i++) {
				string source=Read(arrayFiles[i]);
				Assert.IsFalse(Regex.IsMatch(source,@"https?://",RegexOptions.IgnoreCase),arrayFiles[i]+": no links; the only EDS host is in EDS.cs.");
				Assert.IsFalse(Regex.IsMatch(source,@"www\.|sign up|signup|recommended|opendental\.com",RegexOptions.IgnoreCase),arrayFiles[i]);
			}
			string eds=Read(@"OpenPmsDentalBusiness\Eclaims\EDS.cs");
			string[] arrayHosts=Regex.Matches(eds,@"https://([a-z0-9.\-]+)",RegexOptions.IgnoreCase).Cast<Match>().Select(x => x.Groups[1].Value.ToLowerInvariant())
				.Distinct().ToArray();
			CollectionAssert.AreEqual(new[] { "web2.edsedi.com" },arrayHosts,"EDS.cs talks only to EDS.");
		}

		[TestMethod]
		public void Validation_IsOffUntilTheOfficeTurnsOnTheAttachmentService() {
			//The network part is gated on Clearinghouse.IsAttachmentSendAllowed, which the clearinghouse window starts unchecked (Allow attachment send).
			string source=Read(@"OpenPmsDentalBusiness\VendorDirect\Eds\EdsClaimValidation.cs");
			StringAssert.Contains(source,"&& clearinghouseClin.IsAttachmentSendAllowed");
			string window=Read(@"OpenPmsDental\Forms\FormEdsValidation.cs");
			Assert.IsFalse(window.Contains("Password"),"The window never shows the login.");
		}
	}
}
