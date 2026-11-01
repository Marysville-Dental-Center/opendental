// Source checks for the window code of package M2-CLAIMRCV (the OpenDental project has no test harness for windows): Edit Claim prompts and
// recalculates through the generalized lookup, the prompt's wording comes from ClaimReceiveRelated, the claim status drop-down shows the 26.1
// text, and the Claim Receive preferences carry the 26.1 labels.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.IO;
using System.Text;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace M2CLAIMRCVTests {
	[TestClass]
	public class WiringTests {
		private static string Read(string relativePath) {
			return File.ReadAllText(TestDb.FindRepoFile(relativePath),Encoding.UTF8);
		}

		///<summary>The text from the start marker up to the next occurrence of the end marker.</summary>
		private static string Between(string source,string startMarker,string endMarker) {
			int indexStart=source.IndexOf(startMarker);
			Assert.IsTrue(indexStart>=0,"Not found: "+startMarker);
			int indexEnd=source.IndexOf(endMarker,indexStart+startMarker.Length);
			Assert.IsTrue(indexEnd>indexStart,"Not found after the start: "+endMarker);
			return source.Substring(indexStart,indexEnd-indexStart);
		}

		private static void AssertBefore(string text,string first,string second) {
			int indexFirst=text.IndexOf(first);
			int indexSecond=text.IndexOf(second);
			Assert.IsTrue(indexFirst>=0,"Not found: "+first);
			Assert.IsTrue(indexSecond>indexFirst,"Expected '"+first+"' before '"+second+"'.");
		}

		[TestMethod]
		public void EditClaim_ReceivePromptsAndRecalculatesForAnyOrdinal() {
			string saveCleanup=Between(Read(Path.Combine("OpenDental","Forms","FormClaimEdit.cs")),"private void SaveCleanup(){",
				"private void FormClaimEdit_FormClosing(");
			//The trigger stays: the claim is saved as Received after payment was entered, and the user has ClaimSend (checked silently).
			AssertBefore(saveCleanup,"_isPaymentEntered","Security.IsAuthorized(EnumPermType.ClaimSend,true)");
			AssertBefore(saveCleanup,"Security.IsAuthorized(EnumPermType.ClaimSend,true)","ClaimL.PromptForPrimaryOrSecondaryClaim(_listClaimProcsForClaim,false);");
			AssertBefore(saveCleanup,"ClaimL.PromptForPrimaryOrSecondaryClaim(_listClaimProcsForClaim,false);","ClaimL.PromptForPrimaryOrSecondaryClaim(_listClaimProcsForClaim);");
			StringAssert.Contains(saveCleanup,"_claim.ClaimType!=\"PreAuth\"");
			//Recalculation after any claim, through the next-insurance lookup.
			StringAssert.Contains(saveCleanup,"ClaimReceiveRelated.IsRecalcNextInsuranceNeeded(_claim,receivedAsNoPay,PrefC.GetBool(PrefName.ClaimPrimaryReceivedRecalcSecondary))");
			StringAssert.Contains(saveCleanup,"Claims.GetNextOrdinalClaimsNotReceived(_listClaimProcsForClaim,includeHoldForInProcess:true)");
			Assert.IsFalse(saveCleanup.Contains("_claim.ClaimType==\"P\""),"No longer only after a primary claim.");
			//Combined review (2026-11-01): the lookup loads no attachments and CalculateAndUpdate saves with the one-argument Claims.Update, so each
			//claim's attachments are loaded onto the same object first (a medical EOB attached a moment before among them).
			string recalc=Between(saveCleanup,"Claims.GetNextOrdinalClaimsNotReceived(_listClaimProcsForClaim,includeHoldForInProcess:true)",
				"DialogResult=DialogResult.OK;");
			AssertBefore(recalc,"Claim claimDb=Claims.GetClaim(listClaims[i].ClaimNum);","Claims.CalculateAndUpdate(");
			AssertBefore(recalc,"listClaims[i].Attachments=claimDb.Attachments;","Claims.CalculateAndUpdate(listProcedures,_listInsPlans,listClaims[i],");
		}

		///<summary>Combined review (2026-11-01): CalculateAndUpdateSecondaries, which every silent receive path and the prompt use, loads each claim's
		///attachments onto the claim object it saves (not a reloaded copy, which would lose a Waiting to Send status set in memory).</summary>
		[TestMethod]
		public void Claims_RecalculationOfTheNextInsuranceKeepsAttachments() {
			string source=Read(Path.Combine("OpenDentBusiness","Data Interface","Claims.cs"));
			string secondaries=Between(source,"public static void CalculateAndUpdateSecondaries(","public static void CalculateAndUpdate(");
			AssertBefore(secondaries,"Claim claimDb=Claims.GetClaim(listSecondaryClaims[i].ClaimNum);","CalculateAndUpdate(listProceduresRelevant,");
			AssertBefore(secondaries,"listSecondaryClaims[i].Attachments=claimDb.Attachments;","CalculateAndUpdate(listProceduresRelevant,");
			StringAssert.Contains(secondaries,"CalculateAndUpdate(listProceduresRelevant,listInsPlansRelevant,listSecondaryClaims[i],");
		}

		[TestMethod]
		public void EditClaim_StatusDropDownShowsHoldUntilPriorReceived() {
			string source=Read(Path.Combine("OpenDental","Forms","FormClaimEdit.cs"));
			StringAssert.Contains(source,"comboClaimStatus.Items.Add(ClaimReceiveRelated.GetClaimStatusText(_listClaimStatuses[i]));");
			Assert.IsFalse(source.Contains("comboClaimStatus.Items.Add(_listClaimStatuses[i].GetDescription(false));"));
		}

		[TestMethod]
		public void Prompt_UsesTheGeneralizedWordingAndPreferences() {
			string source=Read(Path.Combine("OpenDental","Logic","ClaimL.cs"));
			string prompt=Between(source,"public static void PromptForPrimaryOrSecondaryClaim(","internal static List<ClaimSendQueueItem> SendClaimSendQueueItems(");
			StringAssert.Contains(prompt,"Claims.GetPrimaryOrSecondaryClaimsNotReceived(listClaimProcsForClaim,isPromptForSecondaryClaim)");
			StringAssert.Contains(prompt,"ClaimReceiveRelated.GetForceStatusPrefName(isPromptForSecondaryClaim)");
			StringAssert.Contains(prompt,"Lan.g(\"ContrAccount\",ClaimReceiveRelated.GetMessage(isPromptForSecondaryClaim))");
			StringAssert.Contains(prompt,"Lan.g(\"ContrAccount\",ClaimReceiveRelated.GetSendText(isPromptForSecondaryClaim))");
			StringAssert.Contains(prompt,"Lan.g(\"ContrAccount\",ClaimReceiveRelated.GetPrintText(isPromptForSecondaryClaim))");
			StringAssert.Contains(prompt,"inputBox.SetTitle(Lan.g(\"ContrAccount\",ClaimReceiveRelated.GetTitle(isPromptForSecondaryClaim)));");
			Assert.IsFalse(prompt.Contains("\"secondary\""),"No wording names the secondary insurance any more.");
			//Do nothing is hidden when the force-status preference is on; the choices are unchanged.
			StringAssert.Contains(prompt,"if(!PrefC.GetBool(prefName)) {");
			StringAssert.Contains(prompt,"\"Change the claim status to 'Waiting to send'\"");
			StringAssert.Contains(prompt,"\"Do nothing\"");
			//Claims created already received (Create Claim with a payment entered) prompt the same way.
			string create=Between(source,"public static CreateClaimDataWrapper CreateClaimFromWrapper(","public static void AllocateUnearnedPayment(");
			AssertBefore(create,"claimLeadOrdinal.ClaimStatus==\"R\"","PromptForPrimaryOrSecondaryClaim(listClaimProcs);");
		}

		[TestMethod]
		public void Claims_TheSecondaryLookupIsTheNextInsurance() {
			string source=Read(Path.Combine("OpenDentBusiness","Data Interface","Claims.cs"));
			string lookup=Between(source,"public static List<Claim> GetPrimaryOrSecondaryClaimsNotReceived(","public static List<ClaimPaySplit> GetInsPayNotAttachedForFixTool(");
			AssertBefore(lookup,"if(isSecondaryClaim) {","return GetNextOrdinalClaimsNotReceived(listClaimProcsForClaims);");
			string receiveNoPay=Between(source,"public static bool ReceiveAsNoPaymentIfNeeded(long claimNum) {","public static string HashFields(Claim claim) {");
			StringAssert.Contains(receiveNoPay,"if(PrefC.GetBool(PrefName.ClaimPrimaryReceivedRecalcSecondary)) {");
			Assert.IsFalse(receiveNoPay.Contains("claim.ClaimType==\"P\""),"Recalculates after a claim of any ordinal.");
			string fromPrimaries=Between(source,"public static void CalculateAndUpdateSecondariesFromPrimaries(","public static void CalculateAndUpdateSecondaries(");
			StringAssert.Contains(fromPrimaries,"Claims.GetNextOrdinalClaimsNotReceived(listClaimProcs,includeHoldForInProcess:true)");
		}

		[TestMethod]
		public void EraAndZeroClaims_RecalculateAfterAnyOrdinal() {
			//ERA payments (manual and automatic) and Outstanding Insurance's zero-claim tool receive claims silently: no prompt, only recalculation.
			string era=Between(Read(Path.Combine("OpenDentBusiness","Data Interface","Etranss.cs")),"public static void ReceiveEraPayment(",
				"PaymentEdit.MakeIncomeTransferForClaimProcs(claim.PatNum,listClaimProcsForClaim);");
			StringAssert.Contains(era,"claimOld.ClaimStatus!=\"R\" && claim.ClaimType!=\"PreAuth\")");
			Assert.IsFalse(era.Contains("claim.ClaimType==\"P\""));
			string zero=Between(Read(Path.Combine("OpenDentBusiness","Reports","RpOutstandingIns.cs")),"public static void ZeroClaims(","public static void ZeroClaim(");
			StringAssert.Contains(zero,"listClaims=listClaims.FindAll(x=>x.ClaimType!=\"PreAuth\");");
			Assert.IsFalse(zero.Contains("ClaimL."),"The business layer never prompts.");
		}

		[TestMethod]
		public void EraAutoProcessedReport_ProcessesTheNextInsurance() {
			//The ERA report's claim menu item checks for claims of the next insurance (any ordinal) the way the prompt finds them, not only for
			//secondary claims (type S), so a received secondary claim reaches the prompt for its tertiary claim.
			string source=Read(Path.Combine("OpenDental","Forms","FormRpEraAutoProcessed.cs"));
			string item=Between(source,"private void MenuItemProcessSecondaryClaims_Click(","private void gridClaims_CellDoubleClick(");
			AssertBefore(item,"Claims.GetNextOrdinalClaimsNotReceived(listClaimProcsForClaim,includeHoldForInProcess:true).Count==0",
				"ClaimL.PromptForPrimaryOrSecondaryClaim(listClaimProcsForClaim);");
			AssertBefore(item,"hx835_ShortClaim.ClaimStatus!=\"R\"","ClaimL.PromptForPrimaryOrSecondaryClaim(listClaimProcsForClaim);");
			Assert.IsFalse(item.Contains("x.ClaimType==\"S\""),"No longer only secondary claims.");
			StringAssert.Contains(source,"Lan.g(this,\"Process Unsent Claims of the Next Insurance\")");
		}

		[TestMethod]
		public void PrefInfos_MatchTheClaimReceiveLabels() {
			//The Preferences window's registry carries each control's text; the two relabelled check boxes keep it in step with the designer.
			string prefInfos=Read(Path.Combine("OpenDental","Resources","PrefInfos.xml")).Replace("\r\n","\n");
			StringAssert.Contains(prefInfos,"<ControlName>checkPromptForSecondaryClaim</ControlName>\n    <ControlText>Prompt for claims of the next insurance</ControlText>");
			StringAssert.Contains(prefInfos,"<ControlName>checkClaimPrimaryRecievedForceSecondaryStatus</ControlName>\n    <ControlText>Remove 'Do Nothing' from the next insurance claims prompt</ControlText>");
		}

		[TestMethod]
		public void ClaimReceivePreferences_26_1Labels() {
			string designer=Read(Path.Combine("OpenDental","User Controls","Preferences","UserControlAccountClaimReceive.Designer.cs"));
			StringAssert.Contains(designer,"this.checkPromptForSecondaryClaim.Text = \"Prompt for claims of the next insurance\";");
			StringAssert.Contains(designer,"this.checkClaimPrimaryRecievedForceSecondaryStatus.Text = \"Remove \\'Do Nothing\\' from the next insurance claims prompt\";");
			StringAssert.Contains(designer,"this.checkClaimPrimaryReceivedRecalcSecondary.Text = \"Auto update next insurance claim estimates when a claim is received\";");
			//The preference names stay; only the labels change.
			string control=Read(Path.Combine("OpenDental","User Controls","Preferences","UserControlAccountClaimReceive.cs"));
			StringAssert.Contains(control,"Prefs.UpdateBool(PrefName.PromptForSecondaryClaim,checkPromptForSecondaryClaim.Checked)");
			StringAssert.Contains(control,"Prefs.UpdateBool(PrefName.ClaimPrimaryRecievedForceSecondaryStatus,checkClaimPrimaryRecievedForceSecondaryStatus.Checked)");
			StringAssert.Contains(control,"Prefs.UpdateBool(PrefName.ClaimPrimaryReceivedRecalcSecondary,checkClaimPrimaryReceivedRecalcSecondary.Checked)");
		}
	}
}
