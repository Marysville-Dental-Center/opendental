// The Treatment Plan module and the Add Procedures window can't be opened in a test run (the GUI is not launched), so these tests read their
// sources and check that they use the shared rules in ProcAddHelper, which the other files test.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.IO;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectFileTests;

namespace BTPTests {
	[TestClass]
	public class WiringTests {
		private static string Read(params string[] arrayParts) {
			return File.ReadAllText(TestDb.FindRepoFile(Path.Combine(arrayParts)));
		}

		[TestMethod]
		public void Module_ToolbarButtonClickAndEnabledState() {
			string source=Read("OpenPmsDental","Main Modules","ControlTreat.cs");
			StringAssert.Contains(source,"new ODToolBarButton(Lan.g(this,\"Add Procs\"),-1,");
			StringAssert.Contains(source,"case \"AddProcs\":");
			StringAssert.Contains(source,"ToolBarMainAddProcs_Click();");
			StringAssert.Contains(source,"ToolBarMain.Buttons[\"AddProcs\"].Enabled=true;");
			StringAssert.Contains(source,"ToolBarMain.Buttons[\"AddProcs\"].Enabled=(PatientCur!=null);");
		}

		[TestMethod]
		public void Module_AddsThroughTheHelperAndRefreshes() {
			string source=Read("OpenPmsDental","Main Modules","ControlTreat.AddProcs.cs");
			StringAssert.Contains(source,"private void ToolBarMainAddProcs_Click()");
			StringAssert.Contains(source,"TreatPlans.CanAddProcsTo(treatPlan)");
			StringAssert.Contains(source,"_listTreatPlans.Any(x => TreatPlans.CanAddProcsTo(x))");
			StringAssert.Contains(source,"Security.IsAuthorized(EnumPermType.ChartModule)");
			StringAssert.Contains(source,"ProcAddHelper.AddToActivePlan(PatientCur.PatNum,treatPlanTarget?.TreatPlanNum??0,");
			StringAssert.Contains(source,"ProcAddHelper.GetRecommendedCodeNum(");
			StringAssert.Contains(source,"new FrmAutoCodeLessIntrusive(");
			StringAssert.Contains(source,"PrefC.GetBool(PrefName.ProcEditRequireAutoCodes)");
			int idxAdd=source.IndexOf("ProcAddHelper.AddToActivePlan(");
			int idxRefresh=source.IndexOf("RefreshAfterAddProcs(procAddResult);",idxAdd);
			Assert.IsTrue(idxAdd>0 && idxRefresh>idxAdd,"The module refreshes after adding.");
			StringAssert.Contains(source,"ModuleSelected(PatientCur.PatNum);");
		}

		[TestMethod]
		public void Window_PickersValidationAndDesigner() {
			string source=Read("OpenPmsDental","Forms","FormTreatPlanAddProcs.cs");
			StringAssert.Contains(source,"formProcCodes.IsSelectionMode=true;");
			StringAssert.Contains(source,"formProcCodes.CanAllowMultipleSelections=true;");
			StringAssert.Contains(source,"ProcAddHelper.GetQuickButtons(ProcButtonQuicks.GetAll())");
			StringAssert.Contains(source,"ProcAddHelper.FindTypedCode(textCode.Text)");
			StringAssert.Contains(source,"ProcAddHelper.GetEntryError(_listProcAddEntries[i])");
			StringAssert.Contains(source,"ProcAddHelper.GetTargetError(TreatPlans.GetOne(_treatPlan.TreatPlanNum),_patient.PatNum)");
			StringAssert.Contains(source,"comboPriority.Items.AddDefs(Defs.GetDefsForCategory(DefCat.TxPriorities,true));");
			string designer=Read("OpenPmsDental","Forms","FormTreatPlanAddProcs.Designer.cs");
			StringAssert.Contains(designer,"this.gridQuick.CellClick += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridQuick_CellClick);");
			StringAssert.Contains(designer,"this.gridMain.CellLeave += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridMain_CellLeave);");
			StringAssert.Contains(designer,"this.gridMain.SelectionMode = OpenPmsDental.UI.GridSelectionMode.OneCell;");
			StringAssert.Contains(designer,"this.butSearch.Click += new System.EventHandler(this.butSearch_Click);");
			StringAssert.Contains(designer,"this.butAddCode.Click += new System.EventHandler(this.butAddCode_Click);");
			StringAssert.Contains(designer,"this.textCode.KeyDown += new System.Windows.Forms.KeyEventHandler(this.textCode_KeyDown);");
			StringAssert.Contains(designer,"this.butRemove.Click += new System.EventHandler(this.butRemove_Click);");
			StringAssert.Contains(designer,"this.butOK.Click += new System.EventHandler(this.butOK_Click);");
			StringAssert.Contains(designer,"this.Load += new System.EventHandler(this.FormTreatPlanAddProcs_Load);");
		}

		[TestMethod]
		public void ChartModuleUntouched_AndFilesInTheProjects() {
			StringAssert.DoesNotMatch(Read("OpenPmsDental","Main Modules","ControlChart.cs"),new System.Text.RegularExpressions.Regex("ProcAddHelper"));
			//The evaluated items, so the check holds for the classic project files and the SDK-style ones of the .NET 10 move alike.
			ProjectFile.Load(TestDb.FindRepoFile(Path.Combine("OpenPmsDental","OpenPmsDental.csproj")))
				.AssertCompiles(@"Main Modules\ControlTreat.AddProcs.cs",@"Forms\FormTreatPlanAddProcs.cs",@"Forms\FormTreatPlanAddProcs.Designer.cs");
			ProjectFile.Load(TestDb.FindRepoFile(Path.Combine("OpenPmsDentalBusiness","OpenPmsDentalBusiness.csproj")))
				.AssertCompiles(@"Data Interface\ProcAddHelper.cs");
		}
	}
}
