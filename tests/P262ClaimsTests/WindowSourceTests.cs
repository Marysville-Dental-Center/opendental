// P262-CLAIMS: the window changes, checked in the source (no window is created or shown): the Edit Claim tab order (item 10), the Default Claim
// Form of Edit Provider (item 20), the detailed warning of Combine Insurance Plans (item 21) and the trigger in Procedure Info (item 22).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace P262ClaimsTests {
	[TestClass]
	public class WindowSourceTests {
		private static string ReadSource(string relativePath) {
			return File.ReadAllText(ScratchDb.FindRepoFile(relativePath.Replace('/',Path.DirectorySeparatorChar)));
		}

		///<summary>The body of a method: from its signature to the next method or region of the class (good enough for these checks).</summary>
		private static string GetMethodBody(string source,string signatureStart) {
			int start=source.IndexOf(signatureStart);
			Assert.IsTrue(start>=0,"Not found: "+signatureStart);
			Match match=new Regex(@"\n\t\t(private|public|protected|internal|#region|#endregion)").Match(source,start+signatureStart.Length);
			return match.Success ? source.Substring(start,match.Index-start) : source.Substring(start);
		}

		[TestMethod]
		public void EditClaim_StatusHistoryAndMiscFollowGeneral() {
			string source=ReadSource("OpenPmsDental/Forms/FormClaimEdit.designer.cs");
			List<string> listTabs=Regex.Matches(source,@"this\.tabControlMain\.Controls\.Add\(this\.(\w+)\);").Cast<Match>().Select(x => x.Groups[1].Value)
				.ToList();
			CollectionAssert.AreEqual(new List<string>() { "tabGeneral","tabHistory","tabMisc","tabAttachments","tabUB04","tabCanadian" },listTabs,
				"The tab order of Edit Claim (the custom TabControl orders tabs as they are added).");
			StringAssert.Contains(source,"this.tabHistory.Text = \"Status History\";");
			StringAssert.Contains(source,"this.tabMisc.Text = \"Misc\";");
		}

		[TestMethod]
		public void EditProvider_DefaultClaimFormIsLoadedAndSaved() {
			string designer=ReadSource("OpenPmsDental/Forms/FormProvEdit.designer.cs");
			StringAssert.Contains(designer,"this.tabGeneral.Controls.Add(this.comboClaimForm);");
			StringAssert.Contains(designer,"this.labelClaimForm.Text = \"Default Claim Form\";");
			string source=ReadSource("OpenPmsDental/Forms/FormProvEdit.cs");
			StringAssert.Contains(GetMethodBody(source,"private void FormProvEdit_Load("),"FillComboClaimForm();");
			StringAssert.Contains(GetMethodBody(source,"private void FillComboClaimForm("),"SetSelectedKey<long>(ProviderCur.ClaimFormNum");
			StringAssert.Contains(GetMethodBody(source,"private void butSave_Click("),"ProviderCur.ClaimFormNum=comboClaimForm.GetSelected<long>();");
		}

		[TestMethod]
		public void NewClaims_EveryCreationTakesTheProvidersForm() {
			//Every place that creates a claim or preauthorization for a plan picks the claim form through ClaimFormDefaults.
			Dictionary<string,int> dictExpected=new Dictionary<string,int>() {
				{ "OpenPmsDentalBusiness/Db Multi Table/AccountModules.cs",1 },
				{ "OpenPmsDentalBusiness/Data Interface/Claims.cs",2 },
				{ "OpenPmsDental/Logic/PreAuthL.cs",1 },
				{ "OpenPmsDental/Main Modules/ControlTreat.cs",1 },
			};
			//The billing provider's default claim form first, then the treating provider's (owner decision of 2026-11-07): each call passes both, after
			//the claim's billing provider was set.
			foreach(KeyValuePair<string,int> pair in dictExpected) {
				string source=ReadSource(pair.Key);
				MatchCollection matches=Regex.Matches(source,@"\.ClaimForm=ClaimFormDefaults\.GetClaimFormNumForNewClaim\(claim\.ProvBill,claim\.ProvTreat,");
				Assert.AreEqual(pair.Value,matches.Count,pair.Key);
				foreach(Match match in matches) {
					int indexBill=source.LastIndexOf("claim.ProvBill=",match.Index,StringComparison.Ordinal);
					Assert.IsTrue(indexBill>0 && match.Index-indexBill<600,pair.Key+": the billing provider is set just before.");
				}
				Assert.AreEqual(0,Regex.Matches(source,@"GetClaimFormNumForNewClaim\(claim\.ProvTreat,").Count,pair.Key+": no call without the billing provider.");
			}
		}

		[TestMethod]
		public void CombinePlans_DetailedWarningBeforeCombining() {
			string body=GetMethodBody(ReadSource("OpenPmsDental/Forms/FormInsPlansMerge.cs"),"private void butOK_Click(");
			StringAssert.Contains(body,"new FormInsPlansMergeWarning(");
			Assert.IsTrue(body.IndexOf("formInsPlansMergeWarning.ShowDialog()")<body.IndexOf("DialogResult=DialogResult.OK;"),
				"The warning comes before the plans are handed back to be combined.");
		}

		[TestMethod]
		public void ProcEdit_DoNotBillInsFiresTheTrigger() {
			string body=GetMethodBody(ReadSource("OpenPmsDental/Forms/FormProcEdit.cs"),"private void checkNoBillIns_Click(");
			StringAssert.Contains(body,"AutomationDoNotBillIns.DoesFire(isNoBillInsOld,checkNoBillIns.CheckState==CheckState.Checked)");
			StringAssert.Contains(body,"AutomationL.Trigger(EnumAutomationTrigger.DoNotBillInsuranceCheck,");
			Assert.IsTrue(body.IndexOf("Procedures.SetNoBillInsFromUser(")<body.IndexOf("EnumAutomationTrigger.DoNotBillInsuranceCheck"),
				"The trigger fires after the change is saved.");
		}

		[TestMethod]
		public void AutomationWindows_CarriersMatchOnlyWithItsTrigger() {
			string condition=ReadSource("OpenPmsDental/Forms/FormAutomationConditionEdit.cs");
			StringAssert.Contains(GetMethodBody(condition,"private bool IsFieldListed("),"AutomationDoNotBillIns.IsTriggerAllowed(");
			StringAssert.Contains(GetMethodBody(condition,"private void butSave_Click("),"AutomationConditionCur.CompareString=POut.Long(_carrierNumSelected);");
			string automation=ReadSource("OpenPmsDental/Forms/FormAutomationEdit.cs");
			StringAssert.Contains(GetMethodBody(automation,"private void butSave_Click("),"AutomationDoNotBillIns.GetSaveError(");
		}
	}
}
