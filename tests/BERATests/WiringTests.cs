// The ERA windows, Display Fields and Preferences can't be opened in a test run (the GUI is not launched), and automatic ERA processing calls the
// auto split through a one-line hook in a shared file. These tests read the sources and check that the hooks are in place, so a merge that drops
// one fails here.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.IO;
using System.Text.RegularExpressions;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BERATests {
	[TestClass]
	public class WiringTests {
		private static string Read(params string[] arrayParts) {
			return File.ReadAllText(TestDb.FindRepoFile(Path.Combine(arrayParts)));
		}

		[TestMethod]
		public void AutoProcessing_SplitsBeforeTheChecks() {
			string source=Read("OpenPmsDentalBusiness","Data Interface","Etranss.cs");
			int indexMethod=source.IndexOf("public static EraAutomationResult TryAutoProcessEraEob(");
			Assert.IsTrue(indexMethod>=0);
			int indexHook=source.IndexOf("listClaimsPaidToProcess[i]=EraAutoSplit.SplitDeniedProcsIfNeeded(",indexMethod);
			int indexCheck=source.IndexOf("automationResult.CanClaimBeAutoProcessed(",indexMethod);
			int indexImport=source.IndexOf("TryImportEraClaimData(x835,listClaimsPaidToProcess[i],",indexMethod);
			Assert.IsTrue(indexHook>indexMethod,"The auto split hook is missing from TryAutoProcessEraEob.");
			Assert.IsTrue(indexHook<indexCheck && indexCheck<indexImport,"The auto split must run before CanClaimBeAutoProcessed and the import.");
			//Combined review (2026-11-01): the next insurance's claims that hold a split procedure are taken out right after the lookup, before any
			//of them is set to Waiting to Send.
			int indexLookup=source.IndexOf("List<Claim> listSecondaryClaims=Claims.GetPrimaryOrSecondaryClaimsNotReceived(listReceivedClaimProcs);",indexMethod);
			int indexKeep=source.IndexOf("EraAutoSplit.KeepNextInsuranceClaimsOnHold(x835,listSecondaryClaims,listPatients,automationResult);",indexMethod);
			int indexWaiting=source.IndexOf("listSecondaryClaims[i].ClaimStatus=\"W\";",indexMethod);
			Assert.IsTrue(indexLookup>indexImport,"The next-insurance lookup is missing from TryAutoProcessEraEob.");
			Assert.IsTrue(indexLookup<indexKeep && indexKeep<indexWaiting,"The claims of split procedures must be kept on hold before the others wait to send.");
		}

		[TestMethod]
		public void CarcCheck_UsesTheRulesAndReportsWhyNotSplit() {
			string source=Read("OpenPmsDentalBusiness","X12","X835.cs");
			StringAssert.Contains(source,"EraCarcRules.GetMatches(EraCarcRules.GetRulesFromPref(),this)");
			StringAssert.Contains(source,"GetAutoSplitSkipReason(claimPaid)");
			StringAssert.Contains(source,"AppendAutoSplitNotes(stringBuilderAutomationMessage,listAutomationResults)");
			StringAssert.Matches(source,new Regex(@"public partial class EraAutomationResult\s*\{"));
		}

		[TestMethod]
		public void DisplayFields_EraHooks() {
			string source=Read("OpenPmsDentalBusiness","Data Interface","DisplayFields.cs");
			StringAssert.Matches(source,new Regex(@"public partial class DisplayFields\s*\{"));
			StringAssert.Contains(source,"return GetDefaultListEra();");
			StringAssert.Contains(source,"return GetAllAvailableListEra();");
		}

		[TestMethod]
		public void EraWindow_GridFollowsDisplayFields() {
			string source=Read("OpenPmsDental","Forms","FormEtrans835Edit.cs");
			StringAssert.Contains(source,"DisplayFields.GetForEraClaimPaidGrid()");
			StringAssert.Contains(source,"DisplayFields.EraClaimPaid.GetCellText(");
			StringAssert.Contains(source,"Cells[indexColRecd]");
			StringAssert.Contains(source,"gridClaimDetails.HScrollVisible=");//Columns wider than the grid scroll sideways.
			StringAssert.Contains(source,"eraAutomationResult.ListAutoSplitNotes");
			StringAssert.DoesNotMatch(source,new Regex(@"Cells\[0\]\.Text"));
		}

		[TestMethod]
		public void DisplayFieldsWindows_RequiredAndPlaceholderCategories() {
			StringAssert.Contains(Read("OpenPmsDental","Forms","FormDisplayFields.cs"),"DisplayFields.IsRequiredField(DisplayFieldCategoryCur,");
			StringAssert.Contains(Read("OpenPmsDental","Forms","FormDisplayFieldCategories.cs"),"DisplayFields.HasAvailableFields(");
		}

		[TestMethod]
		public void ClaimSelect_WarningsNameExpectedValues() {
			string source=Read("OpenPmsDental","Forms","FormEtrans835ClaimSelect.cs");
			StringAssert.Contains(source,"EraClaimMismatch.GetClaimFeeMismatchMessage(");
			StringAssert.Contains(source,"EraClaimMismatch.GetDateOfServiceMismatchMessage(");
			StringAssert.Contains(source,"EraClaimMismatch.GetSplitClaimFeeMismatchMessage(");
			StringAssert.DoesNotMatch(source,new Regex("Claim fee on claim does not match ERA. Continue anyway"));
		}

		[TestMethod]
		public void Preferences_AutoSplitCheckboxAndValidation() {
			string source=Read("OpenPmsDental","User Controls","Preferences","UserControlManageGeneral.cs");
			StringAssert.Contains(source,"EraAutoSplit.IsEnabled()");
			StringAssert.Contains(source,"EraAutoSplit.SetEnabled(");
			StringAssert.Contains(source,"EraCarcRules.IsValid(textEraNoAutoProcessCarcCodes.Text");
			StringAssert.Contains(source,"EraCarcRules.GetSyntaxHelp()");
			string designer=Read("OpenPmsDental","User Controls","Preferences","UserControlManageGeneral.Designer.cs");
			StringAssert.Contains(designer,"this.groupBoxERA.Controls.Add(this.checkEraAutoSplitCarcDenials);");
			StringAssert.Contains(designer,"\"Auto split ERAs with CARC denial codes listed above\"");
		}

		[TestMethod]
		public void AutoProcessedReport_ShowsCarcHold() {
			string source=Read("OpenPmsDental","Forms","FormRpEraAutoProcessed.cs");
			StringAssert.Contains(source,"\"CARC Hold\"");
			StringAssert.Contains(source,"EraCarcRules.GetMatches(listEraCarcRules,hx835_Claim)");
		}

		[TestMethod]
		public void NewFiles_AreCompiled() {
			string csproj=Read("OpenPmsDentalBusiness","OpenPmsDentalBusiness.csproj");
			StringAssert.Contains(csproj,@"<Compile Include=""Data Interface\DisplayFields.Era.cs"" />");
			StringAssert.Contains(csproj,@"<Compile Include=""X12\EraAutoSplit.cs"" />");
			StringAssert.Contains(csproj,@"<Compile Include=""X12\EraCarcRules.cs"" />");
			StringAssert.Contains(csproj,@"<Compile Include=""X12\EraClaimMismatch.cs"" />");
		}
	}
}
